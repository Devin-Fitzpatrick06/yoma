namespace Yoma.Core.Domain.Pathway.Models
{
  /// <summary>
  /// The goals, stages and questions of the approved "My Yoma" prototype (2026-11-24), with a practice stage of impact actions before
  /// each goal's last stage (iteration 4: learn, practise, prove, first job). A starting point: every value is configuration (Pathway)
  /// and a configured list replaces its default whole.
  /// </summary>
  public static class PathwayDefaults
  {
    public const string AnywhereKey = "anywhere";

    /// <summary>
    /// Yoma's seeded user goals (Entity.UserGoal).
    /// </summary>
    public const string UserGoalGetAJob = "Get a job";
    public const string UserGoalStartABusiness = "Start a business";

    /// <summary>
    /// Added to a practice stage's why when no impact action or task fits it and courses stand in.
    /// </summary>
    public const string PracticeFallbackNote = "No impact action on Yoma fits this stage yet, so courses stand in for now.";

    /// <summary>
    /// Added to a practice stage's why when no impact action matches the stage itself and related ones (sharing a skill or category
    /// with the rest of the path) stand in.
    /// </summary>
    public const string PracticeRelatedNote = "These impact actions share skills or a category with the rest of your path.";

    private static readonly string[] JobSeeker = [UserGoalGetAJob];
    private static readonly string[] BusinessStarter = [UserGoalStartABusiness];

    public static List<PathwayGoalOptions> Goals() =>
    [
      Goal("marketing", "Digital marketer", "Grow brands online", @"market|brand|social media|content|digital",
        JobSeeker, @"business, finance|marketing|creative|media", "Your first digital marketing job",
        Stage("Get confident online", "Everything later assumes you can work comfortably with digital tools and write clearly.",
          @"digital literacy|communication|writing|english language|microsoft office", "Beginner", "Any level"),
        Stage("Learn how marketing works", "Customers, positioning and the marketing mix: the base every channel builds on.",
          @"^marketing$|marketing strategy|branding|consumer behavio", "Beginner", "Intermediate"),
        Stage("Master the digital channels", "Apply the basics to social media, search and content, where most entry-level marketing jobs sit.",
          @"digital marketing|social media|search engine optimi|seo|content marketing|email marketing", "Intermediate", "Beginner"),
        Practise("Practise on a real campaign", "Running a real post, campaign or community project shows you can do the work, not only describe it.",
          @"social media campaign|community engagement|content creation|copywriting|storytelling|blog|advocacy|volunteer"),
        Stage("Measure and sell", "Employers hire marketers who can show results: analytics and sales close the loop.",
          @"analytics|google analytics|sales|e-commerce|advertising", "Intermediate", "Advanced")),
      Goal("sales", "Sales & customer service", "Help customers, close deals", @"sales|customer|retail|cashier|representative|agent",
        JobSeeker, @"retail|hospitality|tourism|business, finance", "Your first sales or customer service job",
        Stage("Communicate with confidence", "Every customer conversation starts here.",
          @"communication|customer service|english language|interpersonal", "Beginner", "Any level"),
        Stage("Learn to sell", "Turn conversations into sales: prospecting, pitching and handling objections.",
          @"^sales$|\bsales\b|\bselling|negotiation|persuasi", "Beginner", "Intermediate"),
        Stage("Keep customers coming back", "Retail and relationship skills that employers in Lagos, Accra and Johannesburg ask for.",
          @"customer relationship|crm|retail|customer experience|complaint", "Intermediate", "Beginner"),
        Practise("Practise with real people", "Helping real people, in a community project or a drive, builds the confidence customers notice.",
          @"customer service|community engagement|fundrais|teamwork|interpersonal|volunteer"),
        Stage("Step up to lead", "Team leader and supervisor roles open once you can manage people and targets.",
          @"^management$|leadership|team|supervis|operations", "Intermediate", "Advanced")),
      Goal("web", "Web developer", "Build websites and apps", @"develop|engineer|software|web|\bit\b|programm",
        JobSeeker, @"technology", "Your first web developer job",
        Stage("Computer foundations", "How computers, files and the internet work, so code makes sense.",
          @"digital literacy|computer|information technology|internet", "Beginner", "Any level"),
        Stage("Build your first web pages", "HTML and CSS: you see results from day one.",
          @"html|css|web development|web design", "Beginner", "Intermediate"),
        Stage("Learn to program", "JavaScript or Python turns static pages into working software.",
          @"javascript|python|software development|programming", "Intermediate", "Beginner"),
        Practise("Build something real", "A small real project, for a cause or your community, is the first thing on a junior developer's portfolio.",
          @"website|web design|open source|github|project|volunteer"),
        Stage("Ship real projects", "Databases, version control and testing are what a junior developer is hired to do.",
          @"database|\bsql\b|\bgit\b|software testing|\bapi\b|react|php|laravel", "Intermediate", "Advanced")),
      Goal("data", "Data analyst", "Turn numbers into decisions", @"data|analyst|account|audit|finance|statistic",
        JobSeeker, @"\bdata\b|analytics", "Your first data analyst job",
        Stage("Spreadsheets first", "Most analyst work still starts in Excel.",
          @"excel|spreadsheet", "Beginner", "Any level"),
        Stage("Think like an analyst", "Statistics and analysis methods tell you which numbers matter.",
          @"statistic|data analysis|analytics|mathemat", "Beginner", "Intermediate"),
        Stage("Query and code", "SQL and Python let you work with data too big for a spreadsheet.",
          @"sql|python|data science|database", "Intermediate", "Beginner"),
        Practise("Collect real data", "Running a survey or a data project in your community turns methods into experience.",
          @"data collection|survey|research|data entry|volunteer"),
        Stage("Show what the data says", "Visualisation and machine learning basics make your analysis useful to others.",
          @"visuali|power bi|tableau|machine learning|business intelligence", "Intermediate", "Advanced")),
      Goal("business", "Run my own business", "Start and grow a business", @"business|entrepreneur|trainee|manager|founder|operations",
        BusinessStarter, @"business|entrepreneur", "Your own business, up and running",
        Stage("Shape your idea", "Test whether your idea solves a real problem before spending money.",
          @"entrepreneurship|business plan|startup|small business|innovation", "Beginner", "Any level"),
        Stage("Get the money right", "Bookkeeping, pricing and cash flow keep a young business alive.",
          @"accounting|bookkeeping|financ|budget|pricing", "Beginner", "Intermediate"),
        Stage("Find customers", "A business grows only when people buy: marketing and sales.",
          @"marketing|sales|customer", "Beginner", "Intermediate"),
        Practise("Test it in your community", "A real community project tests your idea with the people who would buy it.",
          @"community|market research|pitch|social enterprise|entrepreneur|volunteer"),
        Stage("Run and grow it", "Operations, people and risk once the business is working.",
          @"^management$|operations|risk management|leadership|project management", "Intermediate", "Advanced")),
      Goal("health", "Health & care worker", "Care for people and communities", @"care|health|nurse|pharm|medical|clinic|hospital",
        JobSeeker, @"health|\bcare\b", "Your first health and care job",
        Stage("Health and safety basics", "First aid, hygiene and safety are the entry ticket for any care role.",
          @"first aid|health and safety|hygiene|infection|public health", "Beginner", "Any level"),
        Stage("Caring for people", "Core caregiving, nursing and child development skills.",
          @"nursing|caregiv|child development|elderly|patient care", "Beginner", "Intermediate"),
        Stage("Support mental health", "Mental health skills are in demand and set you apart.",
          @"mental health|counsel|psycholog|wellbeing", "Intermediate", "Beginner"),
        Practise("Help in your community", "Helping in a community health project is the hands-on experience care employers look for.",
          @"community health|health promotion|hygiene|first aid|wellbeing|awareness|volunteer"),
        Stage("Take on responsibility", "Health management and quality roles come after hands-on experience.",
          @"health ?care management|health management|nutrition|community health|public health", "Intermediate", "Advanced"))
    ];

    public static List<PathwayKnownSkillOptions> KnownSkills() =>
    [
      Known("digital-literacy", "Digital literacy", @"digital literacy|computer|internet", "Digital Literacy", "Computer Literacy", "Internet"),
      Known("communication", "Communication", @"communication|interpersonal", "Communication", "Interpersonal Communications"),
      Known("excel", "Excel", @"excel|spreadsheet", "Microsoft Excel", "Spreadsheets"),
      Known("customer-service", "Customer service", @"customer service", "Customer Service"),
      Known("marketing-basics", "Marketing basics", @"^marketing$|branding", "Marketing", "Branding"),
      Known("programming", "Programming", @"programming|python|javascript", "Programming", "Python (Programming Language)", "JavaScript (Programming Language)"),
      Known("bookkeeping", "Bookkeeping", @"bookkeeping|accounting", "Bookkeeping", "Accounting"),
      Known("first-aid", "First aid", @"first aid", "First Aid")
    ];

    public static List<PathwayBackgroundOptions> Backgrounds() =>
    [
      new() { Background = PathwayBackground.InSchool, Label = "Still in school", Description = "Secondary or high school", StartStage = 0, Level = 0 },
      new() { Background = PathwayBackground.FinishedSchool, Label = "Finished school", Description = "No further study yet", StartStage = 0, Level = 0 },
      new() { Background = PathwayBackground.SomeStudy, Label = "Some study or training", Description = "A course, certificate or diploma", StartStage = 0, Level = 1 },
      new() { Background = PathwayBackground.Degree, Label = "University degree", Description = "Or working in a related field", StartStage = 1, Level = 2 }
    ];

    public static List<PathwayHoursOptions> Hours() =>
    [
      new() { HoursPerWeek = 2, Label = "Up to 2 hours a week", Description = "Short courses first", MaxCourseHours = 6 },
      new() { HoursPerWeek = 5, Label = "About 5 hours a week", Description = "A course every few weeks", MaxCourseHours = 15 },
      new() { HoursPerWeek = 10, Label = "10 or more hours a week", Description = "Longer, deeper courses", MaxCourseHours = 40 }
    ];

    public static List<string> Countries() => ["NG", "GH", "ZA"];

    public static Dictionary<string, decimal> GradeBonus() => new(StringComparer.OrdinalIgnoreCase) { ["A"] = 3, ["B"] = 2, ["C"] = 1, ["D"] = 0 };

    /// <summary>
    /// A copy of the options with every empty list filled from the defaults.
    /// </summary>
    public static PathwayOptions Configured(PathwayOptions options)
    {
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      return new PathwayOptions
      {
        Goals = options.Goals.Count > 0 ? options.Goals : Goals(),
        KnownSkills = options.KnownSkills.Count > 0 ? options.KnownSkills : KnownSkills(),
        Backgrounds = options.Backgrounds.Count > 0 ? options.Backgrounds : Backgrounds(),
        Hours = options.Hours.Count > 0 ? options.Hours : Hours(),
        Countries = options.Countries.Count > 0 ? options.Countries : Countries(),
        MinimumGroupSize = Math.Max(options.MinimumGroupSize, 1),
        DemoDataNote = string.IsNullOrWhiteSpace(options.DemoDataNote) ? null : options.DemoDataNote.Trim(),
        MinCourses = Math.Max(options.MinCourses, 0),
        MaxCourses = Math.Max(options.MaxCourses, Math.Max(options.MinCourses, 1)),
        FirstStageCourses = Math.Max(options.FirstStageCourses, 1),
        StageCourses = Math.Max(options.StageCourses, 1),
        MaxStageCourses = Math.Max(options.MaxStageCourses, Math.Max(options.FirstStageCourses, options.StageCourses)),
        DefaultCourseHours = options.DefaultCourseHours > 0 ? options.DefaultCourseHours : 8,
        MaxJobs = Math.Max(options.MaxJobs, 0),
        DropOffRatio = Math.Clamp(options.DropOffRatio, 0, 1),
        CacheInMinutes = Math.Max(options.CacheInMinutes, 1),
        GradeBonus = options.GradeBonus.Count > 0 ? new Dictionary<string, decimal>(options.GradeBonus, StringComparer.OrdinalIgnoreCase) : GradeBonus(),
        MaxAlternatives = Math.Max(options.MaxAlternatives, 0),
        MaxUsuallyNext = Math.Max(options.MaxUsuallyNext, 0)
      };
    }

    private static PathwayGoalOptions Goal(string key, string label, string blurb, string jobs, string[] userGoals, string categories, string destination,
      params PathwayStageOptions[] stages) =>
      new()
      {
        Key = key,
        Label = label,
        Blurb = blurb,
        JobsPattern = jobs,
        UserGoals = [.. userGoals],
        CategoriesPattern = categories,
        Destination = destination,
        Stages = [.. stages]
      };

    private static PathwayStageOptions Stage(string name, string why, string pattern, params string[] difficulties) =>
      new() { Name = name, Why = why, Pattern = pattern, Difficulties = [.. difficulties], Types = [PathwayStepType.Learning] };

    /// <summary>
    /// Learn, then practise: impact actions or tasks before the goal's last stage, two steps by default.
    /// </summary>
    private static PathwayStageOptions Practise(string name, string why, string pattern) =>
      new()
      {
        Name = name,
        Why = why,
        Pattern = pattern,
        Difficulties = ["Beginner", "Any level"],
        Types = [PathwayStepType.ImpactAction, PathwayStepType.Task],
        Steps = 2
      };

    private static PathwayKnownSkillOptions Known(string key, string label, string pattern, params string[] probes) =>
      new() { Key = key, Label = label, Pattern = pattern, Probes = [.. probes] };
  }
}
