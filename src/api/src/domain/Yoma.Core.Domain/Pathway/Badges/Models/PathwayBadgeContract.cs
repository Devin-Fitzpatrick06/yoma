namespace Yoma.Core.Domain.Pathway.Badges.Models
{
  // Mirrors LearnerBadge and PartnerBadge in src/web/src/api/models/pathway.ts field for field (camelCase JSON, enum names as
  // strings). Change only through the lead, together with that file.

  public enum LearnerBadgeKey
  {
    Pathfinder,
    FirstStep,
    StageComplete,
    HalfwayThere,
    JobReady,
    VerifiedSkill,
    TopPickLearner
  }

  public class LearnerBadge
  {
    public LearnerBadgeKey Key { get; set; }

    public string Label { get; set; } = null!;

    public string Description { get; set; } = null!;

    public bool Earned { get; set; }

    /// <summary>
    /// The date of the evidence that earned it, such as the completion that finished a stage. Null when not earned.
    /// </summary>
    public DateTimeOffset? DateEarned { get; set; }

    /// <summary>
    /// 0 to 1 towards the badge when not earned; null when earned.
    /// </summary>
    public decimal? Progress { get; set; }
  }

  public enum PartnerBadgeLevel
  {
    None,
    Bronze,
    Silver,
    Gold,
    NotEnoughGraded
  }

  public class PartnerBadge
  {
    public Guid OrganizationId { get; set; }

    public string OrganizationName { get; set; } = null!;

    public PartnerBadgeLevel Level { get; set; }

    public int GradedCourses { get; set; }

    /// <summary>
    /// Share of graded courses at A or B. Null below the minimum number of graded courses.
    /// </summary>
    public decimal? ShareTopGrades { get; set; }

    /// <summary>
    /// Share of decided courses (graded or Not verified) held back as Not verified. Courses waiting for review are not counted.
    /// Null below the minimum number of graded courses.
    /// </summary>
    public decimal? ShareHeldBack { get; set; }

    public int MinimumGradedCourses { get; set; }

    /// <summary>
    /// What the partner would need to reach the next level, in plain words, and how many courses are waiting for review.
    /// </summary>
    public string? NextLevelHint { get; set; }
  }
}
