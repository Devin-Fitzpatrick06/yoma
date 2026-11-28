namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Declined is a refusal or a cut-off answer; Invalid is a reply not in the schema. Only Answered is used.
  /// </summary>
  public enum CourseQualityAIOutcome
  {
    Answered,
    Declined,
    Failed,
    Invalid
  }

  /// <summary>
  /// One AI call behind an assessment: what ran, what it used and what it cost. Mirrors CourseQualityAIUsage in the web's courseQuality.ts.
  /// </summary>
  public class CourseQualityAIUsage
  {
    public AIProviderKind Provider { get; set; }

    public string Model { get; set; } = null!;

    /// <summary>
    /// host:port of the model server, for example "localhost:11434"; null for Anthropic.
    /// </summary>
    public string? EndpointHost { get; set; }

    public bool IsLocal { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    /// <summary>
    /// Null when no price is configured for a remote model; 0 for a local one.
    /// </summary>
    public decimal? CostUsd { get; set; }

    public long DurationMs { get; set; }

    public DateTimeOffset DateRun { get; set; }

    public CourseQualityAIOutcome Outcome { get; set; }
  }

  /// <summary>
  /// The model a provider is configured to call. Registered by the selected provider's startup; None when no model runs.
  /// </summary>
  public sealed record CourseQualityAIEndpoint(AIProviderKind Provider, string? Model, string? EndpointHost, bool IsLocal, bool Configured)
  {
    public static CourseQualityAIEndpoint None { get; } = new(AIProviderKind.None, null, null, false, false);
  }

  /// <summary>
  /// A metered call: the result (null when the model declined, failed or answered outside the schema) and its usage
  /// (null when no model was called).
  /// </summary>
  public sealed record CourseQualityAICall(CourseQualityAIResult? Result, CourseQualityAIUsage? Usage)
  {
    public static CourseQualityAICall NotCalled { get; } = new(null, null);
  }

  /// <summary>
  /// GET courseQuality/ai/usage: the configured model and what AI runs have used. Mirrors CourseQualityAIUsageSummary.
  /// </summary>
  public class CourseQualityAIUsageSummary
  {
    public AIProviderKind Provider { get; set; }

    public string? Model { get; set; }

    public string? EndpointHost { get; set; }

    public bool IsLocal { get; set; }

    /// <summary>
    /// A provider is set and its configuration exists (a key, or a base URL and model).
    /// </summary>
    public bool Configured { get; set; }

    public int Runs { get; set; }

    public int Answered { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    /// <summary>
    /// Sum of the known costs; runs with an unknown cost add nothing.
    /// </summary>
    public decimal CostUsd { get; set; }

    public long? AverageDurationMs { get; set; }

    /// <summary>
    /// Newest first, at most 20.
    /// </summary>
    public List<CourseQualityAIRun> Recent { get; set; } = [];
  }

  public class CourseQualityAIRun
  {
    public Guid AssessmentId { get; set; }

    public Guid OpportunityId { get; set; }

    /// <summary>
    /// The listing's organisation (added at the lead's request; not yet in the TS contract until merge).
    /// </summary>
    public Guid OrganizationId { get; set; }

    public string OpportunityTitle { get; set; } = null!;

    public CourseQualityAIUsage Usage { get; set; } = null!;
  }
}
