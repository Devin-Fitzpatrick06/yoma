namespace Yoma.Core.Domain.Pathway.Models
{
  // Iteration 3 contract: "My Yoma" pathways. Mirrors src/web/src/api/models/pathway.ts field for field (camelCase JSON, enums
  // as PascalCase strings). The web file is the source of truth; change both only through the lead.
  // PartnerBadge and PartnerBadgeLevel belong to the badges work (Pathway/Badges) and are not declared here.

  public enum PathwayBackground
  {
    InSchool,
    FinishedSchool,
    SomeStudy,
    Degree
  }

  public enum PathwayStepStatus
  {
    Completed,
    InProgress,
    NotStarted
  }

  public enum LearnerBadgeKey
  {
    /// <summary>Set a goal.</summary>
    Pathfinder,

    /// <summary>Completed the first course on the path.</summary>
    FirstStep,

    /// <summary>Completed every course in a stage.</summary>
    StageComplete,

    /// <summary>Half of the path completed.</summary>
    HalfwayThere,

    /// <summary>Every stage completed.</summary>
    JobReady,

    /// <summary>A skill backed by a verified completion on YoID.</summary>
    VerifiedSkill,

    /// <summary>Completed three A-grade courses.</summary>
    TopPickLearner
  }

  public static class PathwayQuestionId
  {
    public const string Goal = "goal";
    public const string Background = "background";
    public const string KnownSkills = "knownSkills";
    public const string HoursPerWeek = "hoursPerWeek";
    public const string Country = "country";
  }

  public class PathwayOption
  {
    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string? Description { get; set; }
  }

  public class PathwayQuestion
  {
    /// <summary>
    /// One of <see cref="PathwayQuestionId"/>.
    /// </summary>
    public string Id { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Hint { get; set; } = null!;

    public bool MultiSelect { get; set; }

    public List<PathwayOption> Options { get; set; } = [];
  }

  public class PathwayQuestions
  {
    public List<PathwayQuestion> Questions { get; set; } = [];
  }

  public class PathwayProfileRequest
  {
    public string GoalKey { get; set; } = null!;

    public PathwayBackground Background { get; set; }

    public List<string> KnownSkillKeys { get; set; } = [];

    /// <summary>
    /// One of the configured hours options: 2, 5 or 10 by default.
    /// </summary>
    public int HoursPerWeek { get; set; }

    /// <summary>
    /// Null means anywhere.
    /// </summary>
    public Guid? CountryId { get; set; }
  }

  public class PathwayProfile : PathwayProfileRequest
  {
    public string GoalLabel { get; set; } = null!;

    public string? CountryName { get; set; }

    public DateTimeOffset DateCreated { get; set; }

    public DateTimeOffset DateModified { get; set; }
  }

  public class CourseOutcome
  {
    public Guid OpportunityId { get; set; }

    /// <summary>
    /// Youth who completed this course on Yoma.
    /// </summary>
    public int Completers { get; set; }

    /// <summary>
    /// Share of completers who went on to complete a later Learning opportunity in the same goal (0 to 1), null below the minimum group size.
    /// </summary>
    public decimal? ProgressionRate { get; set; }

    /// <summary>
    /// Share of completers with a completed Job opportunity after completing (0 to 1), null below the minimum group size.
    /// </summary>
    public decimal? EmploymentRate { get; set; }

    /// <summary>
    /// Share of youth who completed the previous step of a pathway and then started this course, null when not computed.
    /// </summary>
    public decimal? ContinuationRate { get; set; }

    public int MinimumGroupSize { get; set; }

    /// <summary>
    /// Plain-language explanation, for example "Not enough learners yet to show outcomes".
    /// </summary>
    public string Note { get; set; } = null!;
  }

  public class PathwayCourse
  {
    public Guid OpportunityId { get; set; }

    public string Title { get; set; } = null!;

    public string OrganizationName { get; set; } = null!;

    public string? OrganizationLogoURL { get; set; }

    public string? Difficulty { get; set; }

    public decimal? Hours { get; set; }

    public string? URL { get; set; }

    /// <summary>
    /// The skills this course contributes to the stage.
    /// </summary>
    public List<string> SkillsAdded { get; set; } = [];

    /// <summary>
    /// One sentence: why this course is at this point of the path.
    /// </summary>
    public string Reason { get; set; } = null!;

    /// <summary>
    /// Zeki grade A to D when graded.
    /// </summary>
    public string? Grade { get; set; }

    /// <summary>
    /// The grade band's learner-facing label, for example "Top pick" for A; null when not graded.
    /// </summary>
    public string? GradeLabel { get; set; }

    /// <summary>
    /// For the signed-in youth.
    /// </summary>
    public PathwayStepStatus Status { get; set; }

    /// <summary>
    /// Completions on Yoma.
    /// </summary>
    public int CompletedCount { get; set; }

    public CourseOutcome? Outcome { get; set; }

    /// <summary>
    /// Continuation into this course is well below its siblings'. A signal for review, never a grade penalty.
    /// </summary>
    public bool DropOffFlag { get; set; }
  }

  public class PathwayStage
  {
    public int Order { get; set; }

    public string Name { get; set; } = null!;

    public string Why { get; set; } = null!;

    public string? LeadsTo { get; set; }

    /// <summary>
    /// The youth said they already have this.
    /// </summary>
    public bool Shortened { get; set; }

    public List<PathwayCourse> Courses { get; set; } = [];
  }

  public class PathwayJob
  {
    public Guid OpportunityId { get; set; }

    public string Title { get; set; } = null!;

    public string OrganizationName { get; set; } = null!;

    public List<string> Countries { get; set; } = [];
  }

  public class YoIdSummary
  {
    public string DisplayName { get; set; } = null!;

    public int CompletedOpportunities { get; set; }

    public List<string> Skills { get; set; } = [];

    public int Credentials { get; set; }

    public decimal? ZltoBalance { get; set; }
  }

  public class LearnerBadge
  {
    public LearnerBadgeKey Key { get; set; }

    public string Label { get; set; } = null!;

    public string Description { get; set; } = null!;

    public bool Earned { get; set; }

    public DateTimeOffset? DateEarned { get; set; }

    /// <summary>
    /// 0 to 1 towards the badge when not earned.
    /// </summary>
    public decimal? Progress { get; set; }
  }

  public class MyYoma
  {
    public PathwayProfile Profile { get; set; } = null!;

    public YoIdSummary YoId { get; set; } = null!;

    public string GoalLabel { get; set; } = null!;

    public string ThroughLine { get; set; } = null!;

    public int TotalCourses { get; set; }

    public decimal TotalHours { get; set; }

    public int EstimatedWeeks { get; set; }

    public int CompletedCourses { get; set; }

    public List<PathwayStage> Stages { get; set; } = [];

    public List<PathwayJob> Jobs { get; set; } = [];

    public List<LearnerBadge> Badges { get; set; } = [];
  }
}
