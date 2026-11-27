using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.Opportunity.Models;

namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// The listing as the partner form sends it for create or update (an <see cref="OpportunityRequestBase"/> body). Every field is
  /// optional because the partner is still typing; anything else the form sends is ignored. Nothing here is stored.
  /// </summary>
  public class CourseQualityDraftRequest
  {
    /// <summary>
    /// Set when editing an existing listing; only echoed into the in-memory listing.
    /// </summary>
    public Guid? Id { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public Guid? TypeId { get; set; }

    /// <summary>
    /// Ignored: the organisation is the one in the route.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public string? Summary { get; set; }

    public string? Instructions { get; set; }

    public string? URL { get; set; }

    public string? Provider { get; set; }

    public Guid? CommitmentIntervalId { get; set; }

    public short? CommitmentIntervalCount { get; set; }

    public DateTimeOffset? DateStart { get; set; }

    public DateTimeOffset? DateEnd { get; set; }

    public Guid? EngagementTypeId { get; set; }

    public bool? Hidden { get; set; }

    public List<OpportunityRequestCountry>? Countries { get; set; }

    public List<Guid>? Languages { get; set; }

    public List<Guid>? Skills { get; set; }

    public List<Guid>? Categories { get; set; }

    public List<string>? Keywords { get; set; }

    public List<CustomFieldValueRequest>? CustomFields { get; set; }
  }

  /// <summary>
  /// Where a fix belongs on the partner form, so the panel can jump to the field. Mirrors the web's DraftCheckField: the values
  /// are camelCase strings, not an enum, because the API writes enums in PascalCase.
  /// </summary>
  public static class DraftCheckField
  {
    public const string Title = "title";
    public const string Summary = "summary";
    public const string Description = "description";
    public const string Url = "url";
    public const string DateStart = "dateStart";
    public const string DateEnd = "dateEnd";
    public const string Commitment = "commitment";
    public const string Skills = "skills";
    public const string Languages = "languages";
    public const string Countries = "countries";
    public const string Difficulty = "difficulty";
    public const string Other = "other";

    public static readonly string[] All = [Title, Summary, Description, Url, DateStart, DateEnd, Commitment, Skills, Languages, Countries, Difficulty, Other];
  }

  public class DraftCheckFix
  {
    /// <summary>
    /// The check's name, e.g. LinkWorks, DatesAccurate, JobDetailsClear.
    /// </summary>
    public CourseQualityCriterion Criterion { get; set; }

    /// <summary>
    /// One of <see cref="DraftCheckField"/>.
    /// </summary>
    public string Field { get; set; } = null!;

    /// <summary>
    /// True when this alone keeps the listing from being verified.
    /// </summary>
    public bool HardGate { get; set; }

    /// <summary>
    /// Partner wording.
    /// </summary>
    public string Reason { get; set; } = null!;

    public string? Recommendation { get; set; }
  }

  public class DraftCheckCheck
  {
    public CourseQualityCriterion Criterion { get; set; }

    public int Score { get; set; }

    public bool Passed { get; set; }
  }

  public class DraftCheckSuggestion
  {
    /// <summary>
    /// One of <see cref="DraftCheckField"/>: summary, dateStart, dateEnd or skills.
    /// </summary>
    public string Field { get; set; } = null!;

    public string Value { get; set; } = null!;
  }

  /// <summary>
  /// Mirrors DraftCheckResult in the web's courseQuality.ts ("Iteration 4: pre-publish check").
  /// </summary>
  public class DraftCheckResult
  {
    /// <summary>
    /// Learning, Event, Job or ImpactAction.
    /// </summary>
    public Opportunity.Type OpportunityType { get; set; }

    /// <summary>
    /// Verified, NeedsReview or NotVerified: what the check after publishing would give the same listing.
    /// </summary>
    public CourseQualityStatus PredictedStatus { get; set; }

    /// <summary>
    /// 0 to 100.
    /// </summary>
    public int Score { get; set; }

    public List<DraftCheckCheck> Checks { get; set; } = [];

    public List<DraftCheckFix> Fixes { get; set; } = [];

    /// <summary>
    /// False when the link could not be confirmed while typing; never a hard fail.
    /// </summary>
    public bool LinkChecked { get; set; }

    /// <summary>
    /// One plain sentence, e.g. "Two fixes and this course will be verified."
    /// </summary>
    public string Summary { get; set; } = null!;

    /// <summary>
    /// "Draft this for me": null unless a suggestion provider is configured.
    /// </summary>
    public List<DraftCheckSuggestion>? Suggestions { get; set; }
  }

  /// <summary>
  /// What a suggestion provider reads: the draft and the page its link opened. Page text is untrusted data, never instructions.
  /// </summary>
  public class CourseQualityDraftSuggestionRequest
  {
    public Opportunity.Type Type { get; set; }

    public string Title { get; set; } = null!;

    public string? Summary { get; set; }

    public string Description { get; set; } = null!;

    public string? URL { get; set; }

    public DateTimeOffset? DateStart { get; set; }

    public DateTimeOffset? DateEnd { get; set; }

    public bool HasSkills { get; set; }

    public string? SourcePageText { get; set; }

    public CourseQualityPageSignals? SourcePageSignals { get; set; }

    public string? SourcePageFinalUrl { get; set; }
  }
}
