using System.Globalization;
using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// Pure outcome figures from completion and start records. Every figure describes what happened next, not what a course caused:
  /// completers differ from non-completers, so no ratio between groups is ever computed or shown.
  ///
  /// - Progression: share of a course's completers who later completed another Learning opportunity in a goal the course belongs to.
  /// - Employment: share of a course's completers who later completed a Job opportunity. Yoma issues the employment credential on YoID
  ///   from that same Job completion, so counting the credential as well would count one event twice.
  /// - Continuation: per goal stage, share of youth who completed a course of the stage before and then started this course.
  ///
  /// No rate is given from fewer youth than the minimum group size.
  /// </summary>
  public static class PathwayOutcomeEngine
  {
    #region Public Members
    /// <param name="dataNote">Added to every note, for example to say the records are synthetic demo data.</param>
    public static PathwayOutcomeSnapshot Compute(PathwayOutcomeInput input, List<PathwayCatalogueCourse> courses, List<PathwayGoalOptions> goals, int minimumGroupSize,
      string? dataNote = null)
    {
      ArgumentNullException.ThrowIfNull(input, nameof(input));
      ArgumentNullException.ThrowIfNull(courses, nameof(courses));
      ArgumentNullException.ThrowIfNull(goals, nameof(goals));

      var min = Math.Max(minimumGroupSize, 1);
      var snapshot = PathwayOutcomeSnapshot.Empty(min);
      snapshot.DataNote = string.IsNullOrWhiteSpace(dataNote) ? null : dataNote.Trim();

      // which goals and stages each catalogue course belongs to
      var stagesOf = courses.ToDictionary(o => o.OpportunityId, o => goals.SelectMany((g, gi) => g.Stages.Select((s, si) => (Goal: gi, Stage: si, Match: PathwayMatching.InStage(s, o))))
        .Where(m => m.Match).Select(m => (m.Goal, m.Stage)).ToList());
      var goalsOf = stagesOf.ToDictionary(o => o.Key, o => o.Value.Select(m => m.Goal).ToHashSet());

      // each youth's completions, earliest first, one per opportunity
      var learning = First(input.LearningCompletions);
      // the latest job completion is enough: a youth has a job after a course when their latest job came after it
      var jobs = input.JobCompletions.GroupBy(o => o.UserId).ToDictionary(o => o.Key, o => o.Max(j => j.Date));
      var byUser = learning.GroupBy(o => o.Key.UserId).ToDictionary(o => o.Key, o => o.Select(c => (c.Key.OpportunityId, Date: c.Value)).ToList());
      var byCourse = learning.GroupBy(o => o.Key.OpportunityId).ToDictionary(o => o.Key, o => o.Select(c => (c.Key.UserId, c.Value)).ToList());

      // every catalogue course, and every course youth completed even if it is no longer listed
      foreach (var id in courses.Select(o => o.OpportunityId).Union(byCourse.Keys))
        snapshot.Courses[id] = Outcome(id, byCourse.GetValueOrDefault(id) ?? [], goalsOf, byUser, jobs, min);

      Continuation(snapshot, input, courses, goals, stagesOf, learning, min);

      foreach (var outcome in snapshot.Courses.Values) outcome.Note = WithNote(outcome.Note, snapshot.DataNote);

      return snapshot;
    }

    public static string NotEnough(int minimumGroupSize) =>
      $"Not enough learners yet to show outcomes: figures appear once {minimumGroupSize} youth have completed this course on Yoma.";

    public static string WithNote(string note, string? dataNote) => string.IsNullOrWhiteSpace(dataNote) ? note : $"{note} {dataNote.Trim()}";
    #endregion

    #region Private Members
    private static Dictionary<(Guid UserId, Guid OpportunityId), DateTimeOffset> First(List<PathwayActivity> items) =>
      items.GroupBy(o => (o.UserId, o.OpportunityId)).ToDictionary(o => o.Key, o => o.Min(a => a.Date));

    private static CourseOutcome Outcome(Guid opportunityId, List<(Guid UserId, DateTimeOffset Date)> completers, Dictionary<Guid, HashSet<int>> goalsOf,
      Dictionary<Guid, List<(Guid OpportunityId, DateTimeOffset Date)>> byUser, Dictionary<Guid, DateTimeOffset> jobs, int min)
    {
      var outcome = new CourseOutcome { OpportunityId = opportunityId, Completers = completers.Count, MinimumGroupSize = min };
      if (completers.Count < min)
      {
        outcome.Note = NotEnough(min);
        return outcome;
      }

      var goals = goalsOf.GetValueOrDefault(opportunityId) ?? [];

      if (goals.Count > 0)
      {
        var progressed = completers.Count(c => byUser[c.UserId].Any(o => o.OpportunityId != opportunityId && o.Date > c.Date
          && goalsOf.TryGetValue(o.OpportunityId, out var other) && other.Overlaps(goals)));
        outcome.ProgressionRate = Rate(progressed, completers.Count);
      }

      // with no job completions anywhere on Yoma there is no employment data, which is unknown, not 0%
      if (jobs.Count > 0)
      {
        var employed = completers.Count(c => jobs.TryGetValue(c.UserId, out var latest) && latest > c.Date);
        outcome.EmploymentRate = Rate(employed, completers.Count);
      }

      var parts = new List<string>();
      if (outcome.ProgressionRate.HasValue) parts.Add($"{Percent(outcome.ProgressionRate.Value)} later completed another course towards the same goal");
      if (outcome.EmploymentRate.HasValue) parts.Add($"{Percent(outcome.EmploymentRate.Value)} later completed a job opportunity on Yoma");
      if (parts.Count == 0)
      {
        outcome.Note = "No outcome data yet: Yoma has no job completions recorded and this course is not on a goal's path.";
        return outcome;
      }

      outcome.Note = $"Of {completers.Count} youth who completed this course on Yoma, {PathwayMatching.JoinList(parts)}. "
        + "This shows what happened next, not what the course caused.";
      return outcome;
    }

    private static void Continuation(PathwayOutcomeSnapshot snapshot, PathwayOutcomeInput input, List<PathwayCatalogueCourse> courses, List<PathwayGoalOptions> goals,
      Dictionary<Guid, List<(int Goal, int Stage)>> stagesOf, Dictionary<(Guid UserId, Guid OpportunityId), DateTimeOffset> learning, int min)
    {
      // a completion is also a start
      var starts = First([.. input.LearningStarts, .. input.LearningCompletions]);
      var startersOf = starts.GroupBy(o => o.Key.OpportunityId).ToDictionary(o => o.Key, o => o.ToDictionary(s => s.Key.UserId, s => s.Value));

      for (var gi = 0; gi < goals.Count; gi++)
      {
        var goal = goals[gi];
        var byStage = new Dictionary<int, Dictionary<Guid, decimal>>();

        for (var si = 1; si < goal.Stages.Count; si++)
        {
          var previous = stagesOf.Where(o => o.Value.Contains((gi, si - 1))).Select(o => o.Key).ToHashSet();
          var current = stagesOf.Where(o => o.Value.Contains((gi, si))).Select(o => o.Key).ToList();

          // each youth's first completion of the stage before
          var finished = learning.Where(o => previous.Contains(o.Key.OpportunityId)).GroupBy(o => o.Key.UserId).ToDictionary(o => o.Key, o => o.Min(c => c.Value));
          if (finished.Count < min) continue;

          var rates = new Dictionary<Guid, decimal>();
          foreach (var id in current)
          {
            var starters = startersOf.GetValueOrDefault(id);
            var continued = starters == null ? 0 : finished.Count(o => starters.TryGetValue(o.Key, out var started) && started > o.Value);
            rates[id] = Rate(continued, finished.Count);
          }

          byStage[si] = rates;
        }

        if (byStage.Count > 0) snapshot.Continuation[goal.Key] = byStage;
      }
    }

    private static decimal Rate(int count, int total) => total <= 0 ? 0 : Math.Round((decimal)count / total, 4, MidpointRounding.AwayFromZero);

    private static string Percent(decimal rate) => (rate * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    #endregion
  }
}
