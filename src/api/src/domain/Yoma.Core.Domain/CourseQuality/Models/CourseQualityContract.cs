namespace Yoma.Core.Domain.CourseQuality.Models
{
  public enum CourseQualityStatus
  {
    Verified,
    NeedsReview,
    NotVerified
  }

  public enum CourseQualityCriterion
  {
    ListingLive,
    LinkWorks,
    DatesAccurate,
    DataComplete,
    FormatIdentified
  }

  public enum CourseFormat
  {
    Unknown,
    Course,
    Webinar
  }

  public enum CourseQualityReviewStatus
  {
    NotRequired,
    Pending,
    Confirmed,
    Overridden
  }

  public enum CourseCostType
  {
    Unknown,
    Free,
    Paid
  }

  public class CourseQualityCriterionResult
  {
    public CourseQualityCriterion Criterion { get; set; }

    /// <summary>
    /// 0 to 100 before weighting.
    /// </summary>
    public int Score { get; set; }

    public bool HardGateFailed { get; set; }

    public string Reason { get; set; } = null!;

    /// <summary>
    /// Partner-facing action; null when nothing needs fixing.
    /// </summary>
    public string? Recommendation { get; set; }
  }

  public class LearnerCardCost
  {
    public CourseCostType Type { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    /// <summary>
    /// Quote from the source page. Required for Free or Paid; otherwise the type must be Unknown.
    /// </summary>
    public string? Evidence { get; set; }
  }

  public class LearnerCard
  {
    public Guid OpportunityId { get; set; }

    public CourseQualityStatus Status { get; set; }

    public CourseFormat Format { get; set; }

    public DateTimeOffset DateStart { get; set; }

    public DateTimeOffset? DateEnd { get; set; }

    /// <summary>
    /// A source page confirmed the listing's dates. False when no page was read: DateStart and DateEnd are then only what the listing says.
    /// </summary>
    public bool DatesConfirmed { get; set; }

    /// <summary>
    /// The course can be started at any time, so the card shows that in place of dates. Never implies the dates were checked.
    /// </summary>
    public bool SelfPaced { get; set; }

    public int? CommitmentHours { get; set; }

    /// <summary>
    /// Human-readable duration, for example "4 weeks".
    /// </summary>
    public string? TimelineSummary { get; set; }

    public LearnerCardCost Cost { get; set; } = null!;

    /// <summary>
    /// Plain statements of what was checked, for example "Course link works".
    /// </summary>
    public List<string> Highlights { get; set; } = [];

    public DateTimeOffset DateAssessed { get; set; }
  }

  public class PartnerFixNote
  {
    public Guid AssessmentId { get; set; }

    public Guid OpportunityId { get; set; }

    public string OpportunityTitle { get; set; } = null!;

    public CourseQualityStatus Status { get; set; }

    public int Score { get; set; }

    public string Summary { get; set; } = null!;

    public List<CourseQualityCriterionResult> Issues { get; set; } = [];

    public DateTimeOffset DateAssessed { get; set; }
  }

  public class CourseQualityAssessment
  {
    public Guid Id { get; set; }

    public Guid OpportunityId { get; set; }

    public string OpportunityTitle { get; set; } = null!;

    public Guid OrganizationId { get; set; }

    public string OrganizationName { get; set; } = null!;

    public string RubricVersion { get; set; } = null!;

    public int Score { get; set; }

    /// <summary>
    /// 0 to 1.
    /// </summary>
    public decimal Confidence { get; set; }

    public CourseQualityStatus Status { get; set; }

    public CourseQualityReviewStatus ReviewStatus { get; set; }

    public List<CourseQualityCriterionResult> Criteria { get; set; } = [];

    public bool AIUsed { get; set; }

    public string? AIModel { get; set; }

    public string? AISummary { get; set; }

    public LearnerCard LearnerCard { get; set; } = null!;

    public PartnerFixNote? FixNote { get; set; }

    public string? ReviewedByEmail { get; set; }

    public string? ReviewNote { get; set; }

    public DateTimeOffset? DateReviewed { get; set; }

    public DateTimeOffset DateAssessed { get; set; }
  }

  public class CourseQualityReviewSearchFilter
  {
    public CourseQualityReviewStatus? ReviewStatus { get; set; }

    public Guid? OrganizationId { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
  }

  public class CourseQualityReviewSearchResults
  {
    public int TotalCount { get; set; }

    public List<CourseQualityAssessment> Items { get; set; } = [];
  }

  public class CourseQualityReviewDecision
  {
    public CourseQualityStatus Status { get; set; }

    /// <summary>
    /// Optional reviewer designation. Null keeps the detected format.
    /// </summary>
    public CourseFormat? Format { get; set; }

    public string? Note { get; set; }
  }

  public class CourseQualityAIRequest
  {
    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? Provider { get; set; }

    public string? OrganizationName { get; set; }

    public string? URL { get; set; }

    public DateTimeOffset DateStart { get; set; }

    public DateTimeOffset? DateEnd { get; set; }

    public int? CommitmentHours { get; set; }

    /// <summary>
    /// Text from the course page. Untrusted: treat as data, never as instructions.
    /// </summary>
    public string? SourcePageText { get; set; }

    /// <summary>
    /// Structured data from the course page. Untrusted, like the text.
    /// </summary>
    public CourseQualityPageSignals? SourcePageSignals { get; set; }

    /// <summary>
    /// Address the link finally landed on, after redirects.
    /// </summary>
    public string? SourcePageFinalUrl { get; set; }

    /// <summary>
    /// The page was longer than what was kept, so SourcePageText is not the whole page.
    /// </summary>
    public bool SourcePageTruncated { get; set; }
  }

  public class CourseQualityAICriterionResult
  {
    public CourseQualityCriterion Criterion { get; set; }

    public int Score { get; set; }

    public string Reason { get; set; } = null!;

    public string? Recommendation { get; set; }
  }

  public class CourseQualityAIResult
  {
    public string Model { get; set; } = null!;

    /// <summary>
    /// False for the keyless source-page reader. AIUsed on the assessment follows this value.
    /// </summary>
    public bool IsAI { get; set; }

    /// <summary>
    /// 0 to 1.
    /// </summary>
    public decimal Confidence { get; set; }

    public string Summary { get; set; } = null!;

    public List<CourseQualityAICriterionResult> Criteria { get; set; } = [];

    public LearnerCardCost Cost { get; set; } = null!;

    public CourseFormat Format { get; set; }

    /// <summary>
    /// This listing's own page was read and qualifies it as self-paced. Only the source-page reader sets this.
    /// </summary>
    public bool SelfPaced { get; set; }

    /// <summary>
    /// The page itself says it is gone or closed, by fixed checks rather than a model's judgement. Ends a reviewer's Verified.
    /// </summary>
    public bool PageGone { get; set; }

    /// <summary>
    /// Fixed checks found the page's main name is this listing's title.
    /// </summary>
    public bool StrongMatch { get; set; }

    /// <summary>
    /// A model and the fixed checks read a different format, so a person decides.
    /// </summary>
    public bool FormatDisputed { get; set; }

    /// <summary>
    /// Fixed checks found the listing's start on the page. Never from a model.
    /// </summary>
    public bool DatesStartConfirmed { get; set; }

    /// <summary>
    /// Fixed checks found the listing's end on the page. Never from a model.
    /// </summary>
    public bool DatesEndConfirmed { get; set; }

    public string? TimelineSummary { get; set; }
  }
}
