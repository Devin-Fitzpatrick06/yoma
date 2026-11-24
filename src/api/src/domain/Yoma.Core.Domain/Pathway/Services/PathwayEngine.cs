using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// Pure path builder, ported from the approved prototype's buildPath: the same catalogue, request and options give the same path.
  ///
  /// Each stage takes the best-scoring courses whose skills or title match it: skill and title hits, preferred difficulty, length for
  /// the youth's weekly time, completions on Yoma, the Zeki grade where there is one, and a push down for non-English courses for an
  /// English-first youth. A stage the youth already knows keeps one course. Courses the youth completed or started count towards the
  /// path. Near-duplicate titles are skipped, and short paths are topped up, middle stages first, to the configured minimum.
  /// </summary>
  public static class PathwayEngine
  {
    #region Class Variables
    private const decimal CompletedBonus = 10;
    private const decimal InProgressBonus = 5;
    private const decimal RepeatPenalty = 2;
    #endregion

    #region Public Members
    public static PathwayEngineResult Build(PathwayCatalogue catalogue, PathwayEngineRequest request, PathwayOptions options, PathwayOutcomeSnapshot? outcomes = null)
    {
      ArgumentNullException.ThrowIfNull(catalogue, nameof(catalogue));
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var goal = request.Goal;
      var used = new HashSet<Guid>();
      var stems = new HashSet<string>(StringComparer.Ordinal);
      var stages = new List<(PathwayStageOptions Stage, bool Shortened, List<Scored> Picks, List<Scored> Others)>();

      for (var index = 0; index < goal.Stages.Count; index++)
      {
        var stage = goal.Stages[index];
        var shortened = index < request.Background.StartStage || request.KnownSkills.Any(o => o.Probes.Any(p => PathwayMatching.IsMatch(stage.Pattern, p)));
        var want = shortened ? 1 : index == 0 ? options.FirstStageCourses : options.StageCourses;

        var scored = catalogue.Courses.Where(o => !used.Contains(o.OpportunityId))
          .Select(o => Score(o, stage, index, request, options))
          .Where(o => o != null).Select(o => o!)
          .OrderByDescending(o => o.Score).ThenBy(o => o.Course.OpportunityId)
          .ToList();

        // what the youth has done here counts towards the path, even past the stage's usual size
        var own = scored.Count(o => o.Status != PathwayStepStatus.NotStarted);
        want = Math.Max(want, Math.Min(own, options.MaxStageCourses));

        var picks = new List<Scored>();
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rest = scored.ToList();
        while (picks.Count < want)
        {
          // best first, but a course that only repeats skills the stage already has gives way to one that adds a new skill
          var next = rest.Where(o => !stems.Contains(PathwayMatching.Stem(o.Course.Title)))
            .OrderByDescending(o => o.Score - (o.Skills.Count > 0 && o.Skills.All(covered.Contains) ? RepeatPenalty : 0))
            .ThenBy(o => o.Course.OpportunityId)
            .FirstOrDefault();
          if (next == null) break;

          rest.Remove(next);
          stems.Add(PathwayMatching.Stem(next.Course.Title));
          used.Add(next.Course.OpportunityId);
          covered.UnionWith(next.Skills);
          picks.Add(next);
        }

        stages.Add((stage, shortened, picks, rest));
      }

      TopUp(stages, used, stems, options);
      Trim(stages, options);

      var result = new PathwayEngineResult();
      for (var index = 0; index < stages.Count; index++)
      {
        var (stage, shortened, picks, _) = stages[index];
        var next = index < stages.Count - 1 ? stages[index + 1].Stage : null;

        var ordered = picks.OrderBy(o => o.Status switch { PathwayStepStatus.Completed => 0, PathwayStepStatus.InProgress => 1, _ => 2 })
          .ThenByDescending(o => o.Score).ThenBy(o => o.Course.OpportunityId).ToList();

        var courses = ordered.Select((o, position) => ToCourse(o, stage, next, position, goal, outcomes)).ToList();
        result.Stages.Add(new PathwayStage
        {
          Order = index + 1,
          Name = stage.Name,
          Why = stage.Why,
          LeadsTo = next?.Name,
          Shortened = shortened,
          Courses = courses
        });
      }

      ApplyContinuation(result.Stages, goal, outcomes, options);

      var all = result.Stages.SelectMany(o => o.Courses).ToList();
      decimal Hours(PathwayCourse o) => o.Hours ?? options.DefaultCourseHours;

      result.TotalCourses = all.Count;
      result.CompletedCourses = all.Count(o => o.Status == PathwayStepStatus.Completed);
      result.TotalHours = Math.Round(all.Sum(Hours), 1, MidpointRounding.AwayFromZero);

      var remaining = all.Where(o => o.Status != PathwayStepStatus.Completed).Sum(Hours);
      result.EstimatedWeeks = (int)Math.Ceiling(remaining / Math.Max(request.Hours.HoursPerWeek, 1));

      result.ThroughLine = ThroughLine(goal);
      result.Jobs = MatchJobs(catalogue.Jobs, goal, request.CountryId, options.MaxJobs);

      return result;
    }

    /// <summary>
    /// Live jobs whose title fits the goal, in the youth's country when there are any there, otherwise from every country. Newest first.
    /// </summary>
    public static List<PathwayJob> MatchJobs(List<PathwayCatalogueJob> jobs, PathwayGoalOptions goal, Guid? countryId, int max)
    {
      ArgumentNullException.ThrowIfNull(jobs, nameof(jobs));
      ArgumentNullException.ThrowIfNull(goal, nameof(goal));

      var matching = jobs.Where(o => PathwayMatching.IsMatch(goal.JobsPattern, o.Title)).ToList();
      if (countryId.HasValue)
      {
        var local = matching.Where(o => o.CountryIds.Contains(countryId.Value)).ToList();
        if (local.Count > 0) matching = local;
      }

      return [.. matching.OrderByDescending(o => o.DateStart).ThenBy(o => o.OpportunityId).Take(Math.Max(max, 0)).Select(o => new PathwayJob
      {
        OpportunityId = o.OpportunityId,
        Title = o.Title,
        OrganizationName = o.OrganizationName,
        Countries = [.. o.Countries]
      })];
    }

    public static string ThroughLine(PathwayGoalOptions goal)
    {
      ArgumentNullException.ThrowIfNull(goal, nameof(goal));

      var steps = string.Join(", then ", goal.Stages.Select(o => PathwayMatching.Lower(o.Name)));
      return $"Each course adds a skill the next one uses: {steps}, and then open {PathwayMatching.Lower(goal.Label)} jobs on Yoma.";
    }

    /// <summary>
    /// The prototype's score, plus the grade and the youth's own progress. Null when the course does not belong to the stage.
    /// </summary>
    public static decimal? ScoreCourse(PathwayCatalogueCourse course, PathwayStageOptions stage, int stageIndex, PathwayEngineRequest request, PathwayOptions options)
    {
      return Score(course, stage, stageIndex, request, options)?.Score;
    }
    #endregion

    #region Private Members
    private sealed record Scored(PathwayCatalogueCourse Course, decimal Score, List<string> Skills, PathwayStepStatus Status);

    private static Scored? Score(PathwayCatalogueCourse course, PathwayStageOptions stage, int stageIndex, PathwayEngineRequest request, PathwayOptions options)
    {
      var skills = PathwayMatching.MatchingSkills(stage, course);
      var titleHit = PathwayMatching.IsMatch(stage.Pattern, course.Title);
      if (skills.Count == 0 && !titleHit) return null;

      // a course whose other skills are ones the youth already has adds little
      var knownHit = skills.Count == 0 && course.Skills.Any(s => request.KnownSkills.Any(k => PathwayMatching.IsMatch(k.Pattern, s)));

      var difficulty = stage.Difficulties.Count > 0 && Same(stage.Difficulties[0], course.Difficulty) ? 2m
        : stage.Difficulties.Any(o => Same(o, course.Difficulty)) ? 1m : 0m;
      if (request.Background.Level >= 2 && Same("Beginner", course.Difficulty) && stageIndex > 0) difficulty -= 1;

      var hours = course.Hours ?? options.DefaultCourseHours;
      var maxHours = request.Hours.MaxCourseHours;
      var length = hours <= maxHours ? 1m : hours <= maxHours * 2 ? 0.3m : -1m;

      var quality = (decimal)Math.Min(1, Math.Log10(course.CompletedCount + 1) / 3.5);
      var reach = (decimal)Math.Log10(course.CompletedCount + course.StartedCount + 1) * 0.6m;
      var language = request.EnglishFirst && !PathwayMatching.LooksEnglish(course) ? -4m : 0m;
      var grade = course.Grade != null && options.GradeBonus.TryGetValue(course.Grade, out var bonus) ? bonus : 0m;

      var status = request.Statuses.GetValueOrDefault(course.OpportunityId, PathwayStepStatus.NotStarted);
      var own = status switch { PathwayStepStatus.Completed => CompletedBonus, PathwayStepStatus.InProgress => InProgressBonus, _ => 0m };

      var score = skills.Count * 2m + (titleHit ? 2.5m : -1.5m) + difficulty * 1.5m + quality * 6 + reach + length - (knownHit ? 2 : 0) + language + grade + own;

      return new Scored(course, score, skills, status);
    }

    private static bool Same(string a, string? b) => b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Under the minimum, add the next best course to stages that are not shortened, the middle stages first, one at a time.
    /// </summary>
    private static void TopUp(List<(PathwayStageOptions Stage, bool Shortened, List<Scored> Picks, List<Scored> Others)> stages, HashSet<Guid> used, HashSet<string> stems, PathwayOptions options)
    {
      var middle = (stages.Count - 1) / 2m;
      var order = Enumerable.Range(0, stages.Count).Where(o => !stages[o].Shortened).OrderBy(o => Math.Abs(o - middle)).ThenByDescending(o => o).ToList();

      bool added;
      do
      {
        added = false;
        foreach (var index in order)
        {
          if (stages.Sum(o => o.Picks.Count) >= options.MinCourses) return;

          var (_, _, picks, rest) = stages[index];
          if (picks.Count >= options.MaxStageCourses) continue;

          var next = rest.FirstOrDefault(o => !used.Contains(o.Course.OpportunityId) && !stems.Contains(PathwayMatching.Stem(o.Course.Title)));
          if (next == null) continue;

          rest.Remove(next);
          used.Add(next.Course.OpportunityId);
          stems.Add(PathwayMatching.Stem(next.Course.Title));
          picks.Add(next);
          added = true;
        }
      } while (added);
    }

    /// <summary>
    /// Over the maximum, drop the weakest course the youth has not started from the largest stage, until it fits.
    /// </summary>
    private static void Trim(List<(PathwayStageOptions Stage, bool Shortened, List<Scored> Picks, List<Scored> Others)> stages, PathwayOptions options)
    {
      while (stages.Sum(o => o.Picks.Count) > options.MaxCourses)
      {
        var largest = stages.Where(o => o.Picks.Any(p => p.Status == PathwayStepStatus.NotStarted)).OrderByDescending(o => o.Picks.Count).FirstOrDefault();
        if (largest.Picks == null) return;

        var weakest = largest.Picks.Where(o => o.Status == PathwayStepStatus.NotStarted).OrderBy(o => o.Score).ThenByDescending(o => o.Course.OpportunityId).First();
        largest.Picks.Remove(weakest);
      }
    }

    private static PathwayCourse ToCourse(Scored item, PathwayStageOptions stage, PathwayStageOptions? next, int position, PathwayGoalOptions goal, PathwayOutcomeSnapshot? outcomes)
    {
      var course = item.Course;
      var skills = item.Skills.Take(3).ToList();
      if (skills.Count == 0) skills = [.. course.Skills.Take(3)];

      CourseOutcome? outcome = null;
      if (outcomes != null && outcomes.Courses.TryGetValue(course.OpportunityId, out var found)) outcome = Copy(found);

      return new PathwayCourse
      {
        OpportunityId = course.OpportunityId,
        Title = course.Title,
        OrganizationName = course.OrganizationName,
        OrganizationLogoURL = course.OrganizationLogoURL,
        Difficulty = course.Difficulty,
        Hours = course.Hours,
        URL = course.URL,
        SkillsAdded = skills,
        Reason = Reason(item.Status, skills, stage, next, position, goal),
        Grade = course.Grade,
        GradeLabel = course.Grade == null ? null : course.GradeLabel,
        Status = item.Status,
        CompletedCount = course.CompletedCount,
        Outcome = outcome
      };
    }

    private static string Reason(PathwayStepStatus status, List<string> skills, PathwayStageOptions stage, PathwayStageOptions? next, int position, PathwayGoalOptions goal)
    {
      var what = skills.Count > 0 ? PathwayMatching.JoinList(skills) : $"the basics of {PathwayMatching.Lower(stage.Name)}";

      return status switch
      {
        PathwayStepStatus.Completed => $"You completed this, so it already counts towards \"{stage.Name}\" with {what}.",
        PathwayStepStatus.InProgress => $"You have started this: finishing it gives you {what} for \"{stage.Name}\".",
        _ when next == null => $"Adds {what}, the kind of skill employers list for {PathwayMatching.Lower(goal.Label)} roles.",
        _ when position == 0 => $"Starts \"{stage.Name}\" with {what}, which \"{next.Name}\" builds on.",
        _ => $"Adds {what} to \"{stage.Name}\" before you move on to \"{next.Name}\"."
      };
    }

    /// <summary>
    /// Continuation into each course from the stage before it, and the drop-off flag when it is well below the median of the other
    /// courses of the same stage. The flag asks for a review; it never changes a grade.
    /// </summary>
    private static void ApplyContinuation(List<PathwayStage> stages, PathwayGoalOptions goal, PathwayOutcomeSnapshot? outcomes, PathwayOptions options)
    {
      if (outcomes == null || !outcomes.Continuation.TryGetValue(goal.Key, out var byStage)) return;

      for (var index = 0; index < stages.Count; index++)
      {
        if (!byStage.TryGetValue(index, out var rates)) continue;

        foreach (var course in stages[index].Courses)
        {
          if (!rates.TryGetValue(course.OpportunityId, out var rate)) continue;

          course.Outcome ??= new CourseOutcome
          {
            OpportunityId = course.OpportunityId,
            MinimumGroupSize = outcomes.MinimumGroupSize,
            Note = PathwayOutcomeEngine.WithNote(PathwayOutcomeEngine.NotEnough(outcomes.MinimumGroupSize), outcomes.DataNote)
          };
          course.Outcome.ContinuationRate = rate;
          course.DropOffFlag = IsDropOff(course.OpportunityId, rate, rates, options.DropOffRatio);
        }
      }
    }

    /// <summary>
    /// Below ratio times the median of at least two siblings with a median above zero.
    /// </summary>
    public static bool IsDropOff(Guid opportunityId, decimal rate, Dictionary<Guid, decimal> stageRates, decimal ratio)
    {
      ArgumentNullException.ThrowIfNull(stageRates, nameof(stageRates));

      var siblings = stageRates.Where(o => o.Key != opportunityId).Select(o => o.Value).OrderBy(o => o).ToList();
      if (siblings.Count < 2) return false;

      var middle = siblings.Count / 2;
      var median = siblings.Count % 2 == 1 ? siblings[middle] : (siblings[middle - 1] + siblings[middle]) / 2;
      return median > 0 && rate < ratio * median;
    }

    private static CourseOutcome Copy(CourseOutcome o) => new()
    {
      OpportunityId = o.OpportunityId,
      Completers = o.Completers,
      ProgressionRate = o.ProgressionRate,
      EmploymentRate = o.EmploymentRate,
      ContinuationRate = o.ContinuationRate,
      MinimumGroupSize = o.MinimumGroupSize,
      Note = o.Note
    };
    #endregion
  }
}
