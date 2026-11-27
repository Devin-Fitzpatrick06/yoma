namespace Yoma.Core.Domain.Pathway.Models
{
  /// <summary>
  /// Goals, questions and engine numbers for "My Yoma" (section Pathway). Goals are configuration, not code, so the team can change
  /// them without a release. Lists are left empty here and filled from <see cref="PathwayDefaults"/> when configuration sets none,
  /// because configuration binding adds to a list rather than replacing it: a configured list replaces the default one whole.
  /// </summary>
  public class PathwayOptions
  {
    public const string Section = "Pathway";

    /// <summary>
    /// Production switch for every /pathway/* endpoint (My Yoma, badges, steps). Off by default: the endpoints answer 404 until it is
    /// turned on (the demo turns it on). Applied with the API's [PathwayEnabled] attribute.
    /// </summary>
    public bool Enabled { get; set; }

    public List<PathwayGoalOptions> Goals { get; set; } = [];

    public List<PathwayKnownSkillOptions> KnownSkills { get; set; } = [];

    public List<PathwayBackgroundOptions> Backgrounds { get; set; } = [];

    public List<PathwayHoursOptions> Hours { get; set; } = [];

    /// <summary>
    /// ISO alpha-2 codes offered by the "where do you want to work" question, in order. "Anywhere" is always offered last.
    /// </summary>
    public List<string> Countries { get; set; } = [];

    /// <summary>
    /// No outcome figure is shown from fewer youth than this.
    /// </summary>
    public int MinimumGroupSize { get; set; } = 20;

    /// <summary>
    /// When set, added to every outcome note, for example "Demo data: these learners are synthetic." Set it wherever completion
    /// records are not real youth (the integrated demo), so no synthetic figure is read as real.
    /// </summary>
    public string? DemoDataNote { get; set; }

    public int MinCourses { get; set; } = 8;

    public int MaxCourses { get; set; } = 12;

    /// <summary>
    /// Courses in the first stage, before any top-up.
    /// </summary>
    public int FirstStageCourses { get; set; } = 2;

    /// <summary>
    /// Courses in every later stage, before any top-up.
    /// </summary>
    public int StageCourses { get; set; } = 3;

    /// <summary>
    /// A stage is topped up to at most this many courses while the path is under MinCourses.
    /// </summary>
    public int MaxStageCourses { get; set; } = 4;

    /// <summary>
    /// Study hours assumed for a course whose listing does not say.
    /// </summary>
    public decimal DefaultCourseHours { get; set; } = 8;

    public int MaxJobs { get; set; } = 6;

    /// <summary>
    /// A course is flagged when its continuation rate is below this share of the median of its stage siblings'.
    /// </summary>
    public decimal DropOffRatio { get; set; } = 0.5m;

    /// <summary>
    /// How long the catalogue and the outcome figures are kept in memory. Both are read across the whole platform.
    /// </summary>
    public int CacheInMinutes { get; set; } = 15;

    /// <summary>
    /// Added to a course's score by its Zeki grade, so a better graded course wins a close call.
    /// </summary>
    public Dictionary<string, decimal> GradeBonus { get; set; } = new(StringComparer.OrdinalIgnoreCase);
  }

  public class PathwayGoalOptions
  {
    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string Blurb { get; set; } = null!;

    /// <summary>
    /// Case-insensitive regular expression over Job opportunity titles.
    /// </summary>
    public string JobsPattern { get; set; } = null!;

    public List<PathwayStageOptions> Stages { get; set; } = [];
  }

  public class PathwayStageOptions
  {
    public string Name { get; set; } = null!;

    public string Why { get; set; } = null!;

    /// <summary>
    /// Case-insensitive regular expression over a course's skill names and title.
    /// </summary>
    public string Pattern { get; set; } = null!;

    /// <summary>
    /// Preferred difficulties, best first, for example ["Beginner", "Any level"].
    /// </summary>
    public List<string> Difficulties { get; set; } = [];
  }

  public class PathwayKnownSkillOptions
  {
    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;

    /// <summary>
    /// Case-insensitive regular expression over a course's skill names: a course built on what the youth already knows scores lower.
    /// </summary>
    public string Pattern { get; set; } = null!;

    /// <summary>
    /// Representative skill names. A stage whose pattern matches one of them is one the youth already knows, and is shortened.
    /// </summary>
    public List<string> Probes { get; set; } = [];
  }

  public class PathwayBackgroundOptions
  {
    public PathwayBackground Background { get; set; }

    public string Label { get; set; } = null!;

    public string Description { get; set; } = null!;

    /// <summary>
    /// Stages before this one are treated as known and shortened.
    /// </summary>
    public int StartStage { get; set; }

    /// <summary>
    /// 0 new, 1 some study, 2 degree. At 2, Beginner courses past the first stage score lower.
    /// </summary>
    public int Level { get; set; }
  }

  public class PathwayHoursOptions
  {
    public int HoursPerWeek { get; set; }

    public string Label { get; set; } = null!;

    public string Description { get; set; } = null!;

    /// <summary>
    /// Courses up to this many study hours suit this pace; up to twice it is a stretch; longer scores lower.
    /// </summary>
    public decimal MaxCourseHours { get; set; }
  }
}
