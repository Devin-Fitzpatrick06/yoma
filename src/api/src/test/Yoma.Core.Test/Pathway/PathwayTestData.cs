using Newtonsoft.Json.Linq;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Test.Pathway
{
  internal static class PathwayTestData
  {
    public static readonly PathwayOptions Options = PathwayDefaults.Configured(new PathwayOptions());

    public static PathwayGoalOptions Goal(string key) => Options.Goals.Single(o => o.Key == key);

    public static PathwayCatalogueCourse Course(string title, string[] skills, string? difficulty = "Beginner", decimal? hours = 4, int completed = 0,
      string? grade = null, string[]? languages = null, Guid? id = null) => new()
      {
        OpportunityId = id ?? Guid.NewGuid(),
        Title = title,
        OrganizationName = "Open Learning Org",
        Difficulty = difficulty,
        Hours = hours,
        URL = "https://example.org/course",
        Skills = [.. skills],
        Languages = [.. languages ?? ["English"]],
        CompletedCount = completed,
        Grade = grade,
        GradeLabel = grade == "A" ? "Top pick" : grade == null ? null : "Strong"
      };

    public static PathwayCatalogueJob Job(string title, params (Guid Id, string Name)[] countries) => new()
    {
      OpportunityId = Guid.NewGuid(),
      Title = title,
      OrganizationName = "Employer",
      CountryIds = [.. countries.Select(o => o.Id)],
      Countries = [.. countries.Select(o => o.Name)],
      DateStart = DateTimeOffset.UtcNow.AddDays(-countries.Length)
    };

    public static PathwayEngineRequest Request(string goal = "marketing", PathwayBackground background = PathwayBackground.FinishedSchool, int hours = 5,
      string[]? known = null, Guid? countryId = null, Dictionary<Guid, PathwayStepStatus>? statuses = null, bool englishFirst = true) => new()
      {
        Goal = Goal(goal),
        Background = Options.Backgrounds.Single(o => o.Background == background),
        Hours = Options.Hours.Single(o => o.HoursPerWeek == hours),
        KnownSkills = [.. Options.KnownSkills.Where(o => (known ?? []).Contains(o.Key))],
        CountryId = countryId,
        EnglishFirst = englishFirst,
        Statuses = statuses ?? []
      };

    /// <summary>
    /// Five distinct courses for every stage of the marketing goal, more than any path needs.
    /// </summary>
    public static List<PathwayCatalogueCourse> MarketingCatalogue() =>
    [
      Course("Digital Literacy Essentials", ["Digital Literacy"], completed: 400),
      Course("Business Writing Skills", ["Writing", "Communication"], completed: 150),
      Course("Microsoft Office Basics", ["Microsoft Office"], completed: 300),
      Course("English Language Foundations", ["English Language"], completed: 90),
      Course("Effective Communication at Work", ["Communication"], completed: 50),

      Course("Principles of Marketing", ["Marketing"], completed: 900),
      Course("Brand Building Fundamentals", ["Branding"], completed: 120),
      Course("Consumer Behaviour Explained", ["Consumer Behavior"], completed: 60, difficulty: "Intermediate"),
      Course("Marketing Strategy Basics", ["Marketing Strategy"], completed: 30),
      Course("Positioning Your Product", ["Marketing Strategy", "Branding"], completed: 10),

      Course("Social Media Marketing", ["Social Media"], difficulty: "Intermediate", completed: 700),
      Course("Search Engine Optimisation Fundamentals", ["Search Engine Optimization"], difficulty: "Intermediate", completed: 200),
      Course("Content Marketing Playbook", ["Content Marketing"], difficulty: "Intermediate", completed: 80),
      Course("Email Marketing Campaigns", ["Email Marketing"], difficulty: "Intermediate", completed: 40),
      Course("Digital Marketing Channels Overview", ["Digital Marketing"], completed: 20),

      Course("Google Analytics for Beginners", ["Google Analytics", "Analytics"], difficulty: "Intermediate", completed: 500),
      Course("Sales Techniques That Work", ["Sales"], difficulty: "Intermediate", completed: 250),
      Course("Online Advertising Essentials", ["Advertising"], difficulty: "Advanced", completed: 70),
      Course("Running an E-commerce Store", ["E-Commerce"], difficulty: "Intermediate", completed: 35),
      Course("Measuring Campaign Analytics", ["Analytics"], difficulty: "Advanced", completed: 15),

      Course("Plumbing Basics", ["Plumbing"], completed: 5000)
    ];

    /// <summary>
    /// The live catalogue sample from the course-quality demo cache, when it is on this machine.
    /// </summary>
    public static List<PathwayCatalogueCourse>? LiveCatalogue()
    {
      var directory = new DirectoryInfo(AppContext.BaseDirectory);
      FileInfo? file = null;
      while (directory != null && file == null)
      {
        var candidate = new FileInfo(Path.Combine(directory.FullName, "scripts", "course-quality-demo", ".cache", "live-catalogue.json"));
        if (candidate.Exists) file = candidate;
        directory = directory.Parent;
      }

      if (file == null) return null;

      var items = (JArray)JObject.Parse(File.ReadAllText(file.FullName))["items"]!;
      return [.. items.Select(o => new PathwayCatalogueCourse
      {
        OpportunityId = Guid.Parse((string)o["id"]!),
        Title = (string)o["title"]!,
        OrganizationName = (string?)o["organizationName"] ?? string.Empty,
        Difficulty = (string?)o["difficulty"],
        Hours = (int?)o["commitmentIntervalTotalHours"] is { } hours && hours <= 40 ? hours : null,
        Skills = [.. ((JArray?)o["skills"] ?? []).Select(s => (string)s["name"]!)],
        Languages = [.. ((JArray?)o["languages"] ?? []).Select(s => (string)s["name"]!)],
        CompletedCount = (int?)o["participantCountCompleted"] ?? 0,
        StartedCount = Math.Max(0, ((int?)o["participantCountTotal"] ?? 0) - ((int?)o["participantCountCompleted"] ?? 0))
      })];
    }
  }
}
