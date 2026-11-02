namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Rubric and runtime settings. Everything that the final criteria may change lives here, not in code.
  /// </summary>
  public class CourseQualityOptions
  {
    public const string Section = "CourseQuality";

    /// <summary>
    /// Gates the job and on-demand assessments, so nothing fetches external sites or spends API credit until turned on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Shows each course's status in opportunity search and lists verified courses first. Off by default, so search results and ranking stay as they are until turned on.
    /// </summary>
    public bool ShowInBrowse { get; set; }

    public string RubricVersion { get; set; } = "v5-visible-proof";

    /// <summary>
    /// Configuration binding merges over these defaults, so a section may override a single criterion.
    /// </summary>
    public Dictionary<CourseQualityCriterion, CourseQualityCriterionOptions> Criteria { get; set; } = new()
    {
      [CourseQualityCriterion.ListingLive] = new() { Weight = 20, HardGate = true },
      [CourseQualityCriterion.LinkWorks] = new() { Weight = 20, HardGate = true },
      [CourseQualityCriterion.DatesAccurate] = new() { Weight = 30, HardGate = true },
      [CourseQualityCriterion.DataComplete] = new() { Weight = 15, HardGate = false },
      [CourseQualityCriterion.FormatIdentified] = new() { Weight = 15, HardGate = false }
    };

    public int VerifiedScoreThreshold { get; set; } = 80;

    public decimal ReviewConfidenceThreshold { get; set; } = 0.7m;

    public int HardGateScoreCap { get; set; } = 49;

    public decimal RuleOnlyConfidenceCap { get; set; } = 0.69m;

    /// <summary>
    /// Criterion score of a link that could not be confirmed either way (timeout, bot block, rate limit, 5xx).
    /// </summary>
    public int InconclusiveLinkScore { get; set; } = 50;

    /// <summary>
    /// Subtracted from confidence when the link could not be confirmed either way.
    /// </summary>
    public decimal InconclusiveLinkConfidencePenalty { get; set; } = 0.3m;

    /// <summary>
    /// An AI criterion score below this fails a hard gate, provided the AI actually saw the source page.
    /// </summary>
    public int AIHardGateFailScore { get; set; } = 50;

    /// <summary>
    /// Criterion score of a listing whose format cannot be told. Such a listing always goes to review.
    /// </summary>
    public int FormatUnknownScore { get; set; } = 20;

    /// <summary>
    /// At or below this, a listing with mixed wording is read as a single session.
    /// </summary>
    public int WebinarMaxCommitmentHours { get; set; } = 3;

    /// <summary>
    /// At or above this, a listing runs too long to be a webinar. Yoma counts a week as 168 hours.
    /// </summary>
    public int CourseMinCommitmentHours { get; set; } = 168;

    /// <summary>
    /// A date further back than this is treated as a placeholder.
    /// </summary>
    public int DatePlausibleYearsPast { get; set; } = 15;

    /// <summary>
    /// A date further ahead than this is treated as a placeholder.
    /// </summary>
    public int DatePlausibleYearsFuture { get; set; } = 5;

    /// <summary>
    /// With the hourly schedule this assesses 2,400 listings a day, while a batch on one paced host still fits in under an hour.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    public string Schedule { get; set; } = "0 * * * *";

    /// <summary>
    /// An unchanged listing is assessed again after this many days, because a link can die without the listing changing.
    /// </summary>
    public int ReassessIntervalInDays { get; set; } = 7;

    /// <summary>
    /// Read by the keyless source-page reader for its IsEnabled. The engine itself only ever asks the provider.
    /// </summary>
    public bool SourcePageReaderEnabled { get; set; } = true;

    /// <summary>
    /// Organisations whose courses are self-paced even when a page does not say so. The name is matched exactly on the
    /// verified organisation, ignoring case, never on the partner-supplied provider field, and the page must end up on one
    /// of the organisation's domains. Configuration adds to this list. Providers read it, the engine does not.
    /// </summary>
    public List<CourseQualitySelfPacedProvider> SelfPacedProviders { get; set; } = [new() { Name = "Alison", Domains = ["alison.com"] }];

    /// <summary>
    /// A public card older than this is not shown. Keep it above ReassessIntervalInDays.
    /// </summary>
    public int CardMaxAgeInDays { get; set; } = 14;

    public CourseQualityLinkCheckOptions LinkCheck { get; set; } = new();
  }

  public class CourseQualitySelfPacedProvider
  {
    public string Name { get; set; } = null!;

    /// <summary>
    /// The domain and its subdomains, for example alison.com.
    /// </summary>
    public List<string> Domains { get; set; } = [];
  }

  public class CourseQualityCriterionOptions
  {
    public int Weight { get; set; }

    public bool HardGate { get; set; }
  }

  public class CourseQualityLinkCheckOptions
  {
    public int MaxRedirects { get; set; } = 5;

    public int TimeoutInSeconds { get; set; } = 15;

    public int MaxResponseBytes { get; set; } = 1_048_576;

    /// <summary>
    /// The least time between two requests to one host, so a catalogue on a single site is walked politely.
    /// </summary>
    public int MinHostIntervalInMilliseconds { get; set; } = 1100;

    public int MaxSourceTextLength { get; set; } = 20_000;

    public string UserAgent { get; set; } = "YomaCourseQualityBot/1.0 (+https://yoma.world)";
  }
}
