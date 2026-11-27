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

    /// <summary>
    /// Weights of the popularity score that orders courses within verified and within the rest. Tentative, set by the project lead.
    /// </summary>
    public decimal PopularityCompletedWeight { get; set; } = 0.7m;

    public decimal PopularityStartedWeight { get; set; } = 0.3m;

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
      [CourseQualityCriterion.FormatIdentified] = new() { Weight = 15, HardGate = false },
      [CourseQualityCriterion.JobOpen] = new() { Weight = 25, HardGate = true },
      [CourseQualityCriterion.NoScamSignals] = new() { Weight = 25, HardGate = true },
      [CourseQualityCriterion.JobDetailsClear] = new() { Weight = 35, HardGate = false },
      [CourseQualityCriterion.JobFresh] = new() { Weight = 15, HardGate = false }
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

    /// <summary>
    /// How long after a change (an assessment, a reviewer decision, a survey answer) the catalogue is regraded. Changes in between share
    /// the one run, so a full assessment pass costs a handful of regrades, not one per course.
    /// </summary>
    public int RegradeDelayInSeconds { get; set; } = 30;

    /// <summary>
    /// The default grading rubric. An admin override saved from the rubric page replaces it until reset. Bands configured here
    /// replace the default bands whole.
    /// </summary>
    public CourseGradeRubric Grading { get; set; } = CourseQualityGradingDefaults.Rubric();

    public CourseQualityLearnerFeedbackOptions LearnerFeedback { get; set; } = new();

    /// <summary>
    /// Opportunity types whose listings are checked (live, link works, real dates, enough data). Only Learning is graded or
    /// shown on a learner card; Event and ImpactAction get the listing checks only, with no grade (jobs have their own checks and
    /// grades under Jobs), so a held-back one reaches the review queue and the partner's fix notes. Configuration adds to this
    /// list, so Learning is always checked. Other types are ignored until a product decision says what "good" means for them.
    /// </summary>
    public List<Opportunity.Type> VerifyTypes { get; set; } = [Opportunity.Type.Learning];

    public static readonly Opportunity.Type[] SupportedVerifyTypes = [Opportunity.Type.Learning, Opportunity.Type.Event, Opportunity.Type.ImpactAction];

    /// <summary>
    /// The types checked by the listing rules: Learning always, plus Event and ImpactAction when configured. Jobs are added by Jobs:Enabled.
    /// </summary>
    public List<Opportunity.Type> VerifiedTypes()
    {
      return [.. SupportedVerifyTypes.Where(o => o == Opportunity.Type.Learning || VerifyTypes.Contains(o))];
    }

    public CourseQualityJobOptions Jobs { get; set; } = new();

    // TEMPORARY (A2): engineer A1 owns this section; see CourseQualityPrePublishOptions.cs
    public CourseQualityPrePublishOptions PrePublish { get; set; } = new();
  }

  /// <summary>
  /// Job listings. Their criteria and weights sit in Criteria with the course ones; a score only ever counts the criteria
  /// its own check produced, so the two never mix.
  /// </summary>
  public class CourseQualityJobOptions
  {
    /// <summary>
    /// Off by default, so jobs are neither assessed nor queued for review until turned on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Separate from the course rubric version, so a change to one does not reassess the other catalogue.
    /// </summary>
    public string RubricVersion { get; set; } = "jobs-v1";

    /// <summary>
    /// Posted this many days ago or fewer counts as fresh.
    /// </summary>
    public int FreshDays { get; set; } = 45;

    /// <summary>
    /// Posted this many days ago or more counts as stale.
    /// </summary>
    public int StaleDays { get; set; } = 90;

    /// <summary>
    /// The checks read only the listing, so a clear result is near certain. Below 1, because the listing can still be wrong.
    /// </summary>
    public decimal RuleConfidence { get; set; } = 0.9m;

    /// <summary>
    /// Shows each job's status and grade in opportunity search and ranks graded jobs first, as ShowInBrowse does for courses.
    /// Off by default, so job search stays as it is until turned on. Needs Enabled.
    /// </summary>
    public bool ShowInBrowse { get; set; }

    /// <summary>
    /// The job grading rubric. Jobs are ranked only against other jobs. Configuration only for now: the rubric page edits courses.
    /// </summary>
    public CourseGradeRubric Grading { get; set; } = CourseQualityJobGradingDefaults.Rubric();
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
