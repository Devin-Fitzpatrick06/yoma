using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Pathway.Badges.Models;

namespace Yoma.Core.Domain.Pathway.Badges.Services
{
  /// <summary>
  /// Pure: the same path and completions always give the same badges. Badges are computed, never stored, so they cannot disagree
  /// with the records behind them: a completion that is deleted or declined takes its badge with it.
  ///
  /// Anti-gaming rules:
  /// - Only completions Yoma recorded count. A self-reported "I took it" (the course survey) never counts.
  /// - VerifiedSkill needs a completion a third party stood behind (organisation-approved proof, an organisation's import, or a
  ///   partner's sync) on an opportunity that awards a skill. An action-link completion moves a learner along the path but does not
  ///   back a skill, because anyone holding the link can claim it; an issued credential is not enough on its own either, because
  ///   one is issued for action-link completions too.
  /// - Each opportunity counts once, however often it appears in the path.
  /// - TopPickLearner reads each course's current grade. Grades are relative and can move, so the badge can be lost if a course
  ///   drops out of A; dates still come from the completions.
  /// - Dates come from the completion records (set when the completion was finalised, or the partner's own date for an import or
  ///   sync), never from when the badge was first shown. A path badge can therefore predate the goal, when the work did.
  /// </summary>
  public static class LearnerBadgeCalculator
  {
    #region Class Variables
    private static readonly Dictionary<LearnerBadgeKey, (string Label, string Description)> Text = new()
    {
      [LearnerBadgeKey.Pathfinder] = ("Pathfinder", "You set a goal and have a path to follow."),
      [LearnerBadgeKey.FirstStep] = ("First step", "You completed your first course on your path."),
      [LearnerBadgeKey.StageComplete] = ("Stage complete", "You completed every course in one stage of your path."),
      [LearnerBadgeKey.HalfwayThere] = ("Halfway there", "You completed half of the courses on your path."),
      [LearnerBadgeKey.JobReady] = ("Job ready", "You completed every stage of your path."),
      [LearnerBadgeKey.VerifiedSkill] = ("Verified skill", "A skill on your YoID is backed by a completion the provider verified."),
      [LearnerBadgeKey.TopPickLearner] = ("Top pick learner", "You completed courses graded A, Top pick.")
    };
    #endregion

    #region Public Members
    public static List<LearnerBadge> Calculate(LearnerBadgePath? path, IEnumerable<LearnerBadgeCompletion> completions, LearnerBadgeOptions options)
    {
      ArgumentNullException.ThrowIfNull(completions, nameof(completions));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      // one per opportunity, the earliest recorded; a self-reported one is not a record
      var recorded = completions.Where(o => o.Evidence != LearnerBadgeEvidence.SelfReported)
        .GroupBy(o => o.OpportunityId)
        .ToDictionary(o => o.Key, o => o.OrderBy(c => c.DateCompleted).ToList());

      var first = recorded.ToDictionary(o => o.Key, o => o.Value[0].DateCompleted);

      var stages = path?.Stages.Where(o => o.OpportunityIds.Count > 0).OrderBy(o => o.Order).ToList() ?? [];
      var pathIds = stages.SelectMany(o => o.OpportunityIds).Distinct().ToList();
      var pathDates = pathIds.Where(first.ContainsKey).Select(o => first[o]).OrderBy(o => o).ToList();

      return
      [
        Pathfinder(path),
        FirstStep(pathDates),
        StageComplete(stages, first),
        HalfwayThere(pathIds.Count, pathDates),
        JobReady(pathIds.Count, pathDates),
        VerifiedSkill(recorded.Values.SelectMany(o => o)),
        TopPickLearner(recorded.Values.Select(o => o[0]), options)
      ];
    }
    #endregion

    #region Private Members
    private static LearnerBadge Pathfinder(LearnerBadgePath? path)
    {
      return path?.GoalSetOn is { } date ? Earned(LearnerBadgeKey.Pathfinder, date) : Progress(LearnerBadgeKey.Pathfinder, 0, 1);
    }

    private static LearnerBadge FirstStep(List<DateTimeOffset> pathDates)
    {
      return pathDates.Count > 0 ? Earned(LearnerBadgeKey.FirstStep, pathDates[0]) : Progress(LearnerBadgeKey.FirstStep, 0, 1);
    }

    /// <summary>
    /// Earned on the date the first stage was finished, which is the last completion within it. Progress is the furthest stage.
    /// </summary>
    private static LearnerBadge StageComplete(List<LearnerBadgeStage> stages, Dictionary<Guid, DateTimeOffset> first)
    {
      if (stages.Count == 0) return Progress(LearnerBadgeKey.StageComplete, 0, 1);

      var finished = stages.Select(o => o.OpportunityIds.Distinct().ToList())
        .Where(ids => ids.All(first.ContainsKey))
        .Select(ids => ids.Max(o => first[o]))
        .ToList();

      if (finished.Count > 0) return Earned(LearnerBadgeKey.StageComplete, finished.Min());

      var best = stages.Select(o => o.OpportunityIds.Distinct().ToList()).Max(ids => (decimal)ids.Count(first.ContainsKey) / ids.Count);
      return Progress(LearnerBadgeKey.StageComplete, best, 1);
    }

    private static LearnerBadge HalfwayThere(int total, List<DateTimeOffset> pathDates)
    {
      if (total == 0) return Progress(LearnerBadgeKey.HalfwayThere, 0, 1);

      var needed = (total + 1) / 2;
      return pathDates.Count >= needed ? Earned(LearnerBadgeKey.HalfwayThere, pathDates[needed - 1]) : Progress(LearnerBadgeKey.HalfwayThere, pathDates.Count, needed);
    }

    private static LearnerBadge JobReady(int total, List<DateTimeOffset> pathDates)
    {
      if (total == 0) return Progress(LearnerBadgeKey.JobReady, 0, 1);

      return pathDates.Count >= total ? Earned(LearnerBadgeKey.JobReady, pathDates[^1]) : Progress(LearnerBadgeKey.JobReady, pathDates.Count, total);
    }

    private static LearnerBadge VerifiedSkill(IEnumerable<LearnerBadgeCompletion> recorded)
    {
      var dates = recorded.Where(o => o.Evidence == LearnerBadgeEvidence.Verified && o.SkillCount > 0).Select(o => o.DateCompleted).ToList();

      return dates.Count > 0 ? Earned(LearnerBadgeKey.VerifiedSkill, dates.Min()) : Progress(LearnerBadgeKey.VerifiedSkill, 0, 1);
    }

    private static LearnerBadge TopPickLearner(IEnumerable<LearnerBadgeCompletion> recorded, LearnerBadgeOptions options)
    {
      var needed = Math.Max(options.TopPickCompletions, 1);
      var dates = recorded.Where(o => o.Type == Opportunity.Type.Learning && o.Grade == CourseGrade.A).Select(o => o.DateCompleted).OrderBy(o => o).ToList();

      return dates.Count >= needed ? Earned(LearnerBadgeKey.TopPickLearner, dates[needed - 1]) : Progress(LearnerBadgeKey.TopPickLearner, dates.Count, needed);
    }

    private static LearnerBadge Earned(LearnerBadgeKey key, DateTimeOffset date) => new()
    {
      Key = key,
      Label = Text[key].Label,
      Description = Text[key].Description,
      Earned = true,
      DateEarned = date
    };

    private static LearnerBadge Progress(LearnerBadgeKey key, decimal done, decimal needed) => new()
    {
      Key = key,
      Label = Text[key].Label,
      Description = Text[key].Description,
      Earned = false,
      // rounded, but never to 1 while it is not earned
      Progress = needed <= 0 ? 0 : Math.Min(Math.Round(Math.Clamp(done / needed, 0m, 1m), 2, MidpointRounding.AwayFromZero), 0.99m)
    };
    #endregion
  }
}
