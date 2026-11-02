namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Stored form of an assessment. Every run adds a row and only the newest has IsLatest set, so history is kept.
  /// </summary>
  public class CourseQualityAssessmentRecord
  {
    public Guid Id { get; set; }

    public Guid OpportunityId { get; set; }

    /// <summary>
    /// Read from the opportunity, so it is always current.
    /// </summary>
    public string OpportunityTitle { get; set; } = null!;

    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Read from the organization, so it is always current.
    /// </summary>
    public string OrganizationName { get; set; } = null!;

    public string RubricVersion { get; set; } = null!;

    public int Score { get; set; }

    public decimal Confidence { get; set; }

    public CourseQualityStatus Status { get; set; }

    public CourseQualityReviewStatus ReviewStatus { get; set; }

    /// <summary>
    /// Queryable copies of assessment facts, so coverage can be counted without reading the JSON columns.
    /// </summary>
    public CourseFormat Format { get; set; }

    public bool DatesSuspect { get; set; }

    public bool CardReady { get; set; }

    public bool AIUsed { get; set; }

    public string? AIModel { get; set; }

    public string? AISummary { get; set; }

    public string Criteria { get; set; } = null!;

    public string LearnerCard { get; set; } = null!;

    public string? FixNote { get; set; }

    public string? ReviewedByEmail { get; set; }

    public string? ReviewNote { get; set; }

    public DateTimeOffset? DateReviewed { get; set; }

    public DateTimeOffset DateAssessed { get; set; }

    /// <summary>
    /// The opportunity's DateModified when assessed. Tells the job a listing may need assessing again; nothing more,
    /// because it also moves on participant and reward updates.
    /// </summary>
    public DateTimeOffset OpportunityDateModified { get; set; }

    /// <summary>
    /// Hash of the learner-relevant listing content when assessed. A reviewer decision lasts while this still matches.
    /// </summary>
    public string ContentFingerprint { get; set; } = null!;

    /// <summary>
    /// The host the course link ended on. A reviewer's Verified does not follow a link to another site.
    /// </summary>
    public string? FinalHost { get; set; }

    /// <summary>
    /// When a check last proved a reviewer's Verified, which is how long an unreachable page may keep it.
    /// </summary>
    public DateTimeOffset? DateConfirmed { get; set; }

    public bool IsLatest { get; set; }
  }
}
