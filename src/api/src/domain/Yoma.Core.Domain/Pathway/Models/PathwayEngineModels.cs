namespace Yoma.Core.Domain.Pathway.Models
{
  /// <summary>
  /// A published, active Learning opportunity as the pathway engine sees it.
  /// </summary>
  public sealed class PathwayCatalogueCourse
  {
    public Guid OpportunityId { get; set; }

    public string Title { get; set; } = null!;

    public string OrganizationName { get; set; } = null!;

    public string? OrganizationLogoURL { get; set; }

    /// <summary>
    /// Display label, for example "Beginner" or "Any level".
    /// </summary>
    public string? Difficulty { get; set; }

    /// <summary>
    /// Study hours, not calendar hours; null when the listing does not say.
    /// </summary>
    public decimal? Hours { get; set; }

    public string? URL { get; set; }

    public List<string> Skills { get; set; } = [];

    public List<string> Languages { get; set; } = [];

    public int CompletedCount { get; set; }

    /// <summary>
    /// Started and not completed, each person once.
    /// </summary>
    public int StartedCount { get; set; }

    /// <summary>
    /// Zeki grade A to D, null when not graded.
    /// </summary>
    public string? Grade { get; set; }

    /// <summary>
    /// The grade band's label as stored with the grade, for example "Top pick".
    /// </summary>
    public string? GradeLabel { get; set; }
  }

  /// <summary>
  /// A live Job opportunity.
  /// </summary>
  public sealed class PathwayCatalogueJob
  {
    public Guid OpportunityId { get; set; }

    public string Title { get; set; } = null!;

    public string OrganizationName { get; set; } = null!;

    public List<Guid> CountryIds { get; set; } = [];

    public List<string> Countries { get; set; } = [];

    public DateTimeOffset DateStart { get; set; }
  }

  public sealed class PathwayCatalogue
  {
    public List<PathwayCatalogueCourse> Courses { get; set; } = [];

    public List<PathwayCatalogueJob> Jobs { get; set; } = [];
  }

  /// <summary>
  /// Everything the engine needs about one youth, already resolved against configuration.
  /// </summary>
  public sealed class PathwayEngineRequest
  {
    public PathwayGoalOptions Goal { get; set; } = null!;

    public PathwayBackgroundOptions Background { get; set; } = null!;

    public PathwayHoursOptions Hours { get; set; } = null!;

    public List<PathwayKnownSkillOptions> KnownSkills { get; set; } = [];

    public Guid? CountryId { get; set; }

    /// <summary>
    /// False when the youth prefers another language, so non-English courses are not pushed down.
    /// </summary>
    public bool EnglishFirst { get; set; } = true;

    /// <summary>
    /// The youth's own courses: completed or started. Anything missing is NotStarted.
    /// </summary>
    public Dictionary<Guid, PathwayStepStatus> Statuses { get; set; } = [];
  }

  public sealed class PathwayEngineResult
  {
    public string ThroughLine { get; set; } = null!;

    public List<PathwayStage> Stages { get; set; } = [];

    public List<PathwayJob> Jobs { get; set; } = [];

    public int TotalCourses { get; set; }

    public decimal TotalHours { get; set; }

    public int EstimatedWeeks { get; set; }

    public int CompletedCourses { get; set; }
  }

  /// <summary>
  /// One completion or start on Yoma, the raw material of outcomes.
  /// </summary>
  public readonly record struct PathwayActivity(Guid UserId, Guid OpportunityId, DateTimeOffset Date);

  public sealed class PathwayOutcomeInput
  {
    /// <summary>
    /// Completed Learning opportunities, any status of the opportunity today.
    /// </summary>
    public List<PathwayActivity> LearningCompletions { get; set; } = [];

    /// <summary>
    /// Completed Job opportunities.
    /// </summary>
    public List<PathwayActivity> JobCompletions { get; set; } = [];

    /// <summary>
    /// Learning opportunities a youth left Yoma for or submitted for verification, completed or not.
    /// </summary>
    public List<PathwayActivity> LearningStarts { get; set; } = [];
  }

  /// <summary>
  /// Outcome figures for the whole catalogue, worked out in one pass.
  /// </summary>
  public sealed class PathwayOutcomeSnapshot
  {
    public int MinimumGroupSize { get; set; }

    /// <summary>
    /// Added to every note, for example to say the records are synthetic demo data. Null for real data.
    /// </summary>
    public string? DataNote { get; set; }

    /// <summary>
    /// Per course, without continuation, which needs a pathway step before it.
    /// </summary>
    public Dictionary<Guid, CourseOutcome> Courses { get; set; } = [];

    /// <summary>
    /// Goal key, then stage index, then course: the share of youth who completed a course of the stage before and then started this
    /// one. A stage is missing when too few youth completed the stage before it.
    /// </summary>
    public Dictionary<string, Dictionary<int, Dictionary<Guid, decimal>>> Continuation { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static PathwayOutcomeSnapshot Empty(int minimumGroupSize) => new() { MinimumGroupSize = minimumGroupSize };
  }
}
