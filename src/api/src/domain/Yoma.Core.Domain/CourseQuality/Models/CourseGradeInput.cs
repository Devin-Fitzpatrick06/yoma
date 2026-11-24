using Yoma.Core.Domain.Core;

namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Everything grading knows about one course: its latest assessment, its listing, its participation and its feedback.
  /// Built in one pass for the whole catalogue, because grades are relative.
  /// </summary>
  public sealed class CourseGradeInput
  {
    public Guid OpportunityId { get; set; }

    public string OpportunityTitle { get; set; } = null!;

    public Guid OrganizationId { get; set; }

    public string OrganizationName { get; set; } = null!;

    public CourseQualityStatus Status { get; set; }

    public decimal Confidence { get; set; }

    public List<CourseQualityCriterionResult> Criteria { get; set; } = [];

    public CourseCostType CostType { get; set; }

    public bool SelfPaced { get; set; }

    /// <summary>
    /// Null on assessments from before page evidence was kept; grading then reads the listing text.
    /// </summary>
    public CourseQualityPageEvidence? PageEvidence { get; set; }

    public string? Description { get; set; }

    public int SkillCount { get; set; }

    public List<string> Languages { get; set; } = [];

    public TimeIntervalOption? CommitmentInterval { get; set; }

    public short? CommitmentIntervalCount { get; set; }

    public DateTimeOffset DateCreated { get; set; }

    public int Completed { get; set; }

    /// <summary>
    /// Started and not completed, each person once.
    /// </summary>
    public int Started { get; set; }

    public List<CourseGradeFeedbackAnswer> Feedback { get; set; } = [];

    /// <summary>
    /// Youth who completed the course on Yoma, for the Outcomes factor.
    /// </summary>
    public int OutcomeCompleters { get; set; }

    /// <summary>
    /// Share of completers who later completed another course towards the same goal, 0 to 1; null below the minimum group size.
    /// </summary>
    public decimal? ProgressionRate { get; set; }

    /// <summary>
    /// Share of completers who later completed a Job opportunity, 0 to 1; null below the minimum group size.
    /// </summary>
    public decimal? EmploymentRate { get; set; }
  }

  /// <summary>
  /// One learner's answer to "Did you take it?" when it was yes.
  /// </summary>
  public sealed record CourseGradeFeedbackAnswer(int? Rating, bool? WouldRecommend);
}
