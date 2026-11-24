namespace Yoma.Core.Domain.Pathway.Badges.Models
{
  /// <summary>
  /// Partner badge thresholds (configuration section Pathway:PartnerBadges). Placeholders until the project lead sets them.
  /// </summary>
  public class PartnerBadgeOptions
  {
    public const string Section = "Pathway:PartnerBadges";

    /// <summary>
    /// Below this many graded courses the level is NotEnoughGraded, so a provider is not labelled from a handful of courses.
    /// </summary>
    public int MinimumGradedCourses { get; set; } = 10;

    /// <summary>
    /// Each level needs at least this share of graded courses at A or B and at most this share held back as Not verified.
    /// Binding merges by level, so configuration may override one threshold. Grades are catalogue percentiles (A and B are
    /// about 40% of all graded courses), so these shares are relative to the rest of the catalogue.
    /// </summary>
    public Dictionary<PartnerBadgeLevel, PartnerBadgeThreshold> Levels { get; set; } = new()
    {
      [PartnerBadgeLevel.Gold] = new() { MinShareTopGrades = 0.60m, MaxShareHeldBack = 0.05m },
      [PartnerBadgeLevel.Silver] = new() { MinShareTopGrades = 0.45m, MaxShareHeldBack = 0.10m },
      [PartnerBadgeLevel.Bronze] = new() { MinShareTopGrades = 0.30m, MaxShareHeldBack = 0.20m }
    };
  }

  public class PartnerBadgeThreshold
  {
    public decimal MinShareTopGrades { get; set; }

    public decimal MaxShareHeldBack { get; set; }
  }

  /// <summary>
  /// Learner badge settings (configuration section Pathway:LearnerBadges).
  /// </summary>
  public class LearnerBadgeOptions
  {
    public const string Section = "Pathway:LearnerBadges";

    /// <summary>
    /// A-grade courses completed for TopPickLearner.
    /// </summary>
    public int TopPickCompletions { get; set; } = 3;
  }

  /// <summary>
  /// What a partner badge is computed from: the organisation's publicly visible Learning courses, counted by their latest
  /// assessment and grade.
  /// </summary>
  /// <param name="TopGrades">Courses graded A or B.</param>
  /// <param name="Graded">Courses graded A to D.</param>
  /// <param name="HeldBack">Courses whose latest assessment is Not verified.</param>
  /// <param name="WaitingForReview">Courses whose latest assessment needs review and that have no grade. Not counted either way.</param>
  public sealed record PartnerBadgeCounts(int TopGrades, int Graded, int HeldBack, int WaitingForReview);
}
