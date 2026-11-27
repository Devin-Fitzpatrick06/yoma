using Xunit;
using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Domain.Pathway.Services;
using static Yoma.Core.Test.Pathway.PathwayTestData;

namespace Yoma.Core.Test.Pathway
{
  public class PathwayEngineTests
  {
    private static PathwayEngineResult Build(List<PathwayCatalogueCourse> courses, PathwayEngineRequest request, List<PathwayCatalogueJob>? jobs = null,
      PathwayOutcomeSnapshot? outcomes = null) =>
      PathwayEngine.Build(new PathwayCatalogue { Courses = courses, Jobs = jobs ?? [] }, request, Options, outcomes);

    [Fact]
    public void Build_GivesFiveStagesInOrder_WithEightToTwelveSteps()
    {
      var result = Build(MarketingCatalogue(), Request());

      Assert.Equal(["Get confident online", "Learn how marketing works", "Master the digital channels", "Practise on a real campaign", "Measure and sell"],
        result.Stages.Select(o => o.Name));
      Assert.Equal([1, 2, 3, 4, 5], result.Stages.Select(o => o.Order));
      Assert.InRange(result.TotalCourses, Options.MinCourses, Options.MaxCourses);
      Assert.Equal(2, result.Stages[0].Courses.Count);
      Assert.Equal("Learn how marketing works", result.Stages[0].LeadsTo);
      Assert.Null(result.Stages[^1].LeadsTo);
      Assert.DoesNotContain(result.Stages.SelectMany(o => o.Courses), o => o.Title == "Plumbing Basics");
    }

    [Fact]
    public void Build_PicksEachCourseOnce_AndExplainsIt()
    {
      var result = Build(MarketingCatalogue(), Request());
      var courses = result.Stages.SelectMany(o => o.Courses).ToList();

      Assert.Equal(courses.Count, courses.Select(o => o.OpportunityId).Distinct().Count());
      Assert.All(courses, o => Assert.False(string.IsNullOrWhiteSpace(o.Reason)));
      Assert.All(courses, o => Assert.NotEmpty(o.SkillsAdded));
      Assert.All(courses, o => Assert.Equal(PathwayStepStatus.NotStarted, o.Status));
      Assert.Contains("Principles of Marketing", result.Stages[1].Courses.Select(o => o.Title));
      Assert.Equal(["Marketing"], result.Stages[1].Courses.Single(o => o.Title == "Principles of Marketing").SkillsAdded);
    }

    [Fact]
    public void Build_ShortensAStageTheYouthAlreadyKnows()
    {
      var result = Build(MarketingCatalogue(), Request(known: ["digital-literacy", "marketing-basics"]));

      Assert.True(result.Stages[0].Shortened);
      Assert.Single(result.Stages[0].Courses);
      Assert.True(result.Stages[1].Shortened);
      Assert.Single(result.Stages[1].Courses);
      Assert.False(result.Stages[2].Shortened);
    }

    [Fact]
    public void Build_ADegreeShortensTheFirstStage()
    {
      var result = Build(MarketingCatalogue(), Request(background: PathwayBackground.Degree));

      Assert.True(result.Stages[0].Shortened);
      Assert.Single(result.Stages[0].Courses);
    }

    [Fact]
    public void Build_TopsUpTheMiddleStages_WhenShortenedStagesLeaveThePathShort()
    {
      var options = PathwayDefaults.Configured(new PathwayOptions { MinCourses = 11 });
      var result = PathwayEngine.Build(new PathwayCatalogue { Courses = MarketingCatalogue() }, Request(known: ["digital-literacy", "marketing-basics"]), options);

      // 1 + 1 + 3 + 2 + 3 before the top-up; the middle stage gets the extra step
      Assert.Equal(11, result.TotalCourses);
      Assert.Equal(4, result.Stages[2].Courses.Count);
      Assert.All(result.Stages, o => Assert.InRange(o.Courses.Count, 1, options.MaxStageCourses));
    }

    [Fact]
    public void Build_CountsCompletedCourses_AndLeavesThemOutOfTheWeeksStillToGo()
    {
      var courses = MarketingCatalogue();
      var done = courses.Single(o => o.Title == "Marketing Strategy Basics");
      var started = courses.Single(o => o.Title == "Email Marketing Campaigns");

      var statuses = new Dictionary<Guid, PathwayStepStatus> { [done.OpportunityId] = PathwayStepStatus.Completed, [started.OpportunityId] = PathwayStepStatus.InProgress };
      var result = Build(courses, Request(statuses: statuses));

      var stage = result.Stages[1];
      Assert.Equal(done.OpportunityId, stage.Courses[0].OpportunityId);
      Assert.Equal(PathwayStepStatus.Completed, stage.Courses[0].Status);
      Assert.StartsWith("You completed this", stage.Courses[0].Reason);
      Assert.Equal(PathwayStepStatus.InProgress, result.Stages[2].Courses[0].Status);
      Assert.Equal(1, result.CompletedCourses);

      var remaining = result.Stages.SelectMany(o => o.Courses).Where(o => o.Status != PathwayStepStatus.Completed).Sum(o => o.Hours ?? Options.DefaultCourseHours);
      Assert.Equal((int)Math.Ceiling(remaining / 5), result.EstimatedWeeks);
      Assert.True(result.TotalHours > remaining);
    }

    [Fact]
    public void Build_SkipsNearDuplicateTitles()
    {
      var courses = MarketingCatalogue();
      courses.Add(Course("Principles of Marketing 2nd Edition", ["Marketing"], completed: 899));

      var result = Build(courses, Request());
      var titles = result.Stages.SelectMany(o => o.Courses).Select(o => o.Title).ToList();

      Assert.Contains("Principles of Marketing", titles);
      Assert.DoesNotContain("Principles of Marketing 2nd Edition", titles);
    }

    [Fact]
    public void Build_PrefersTheBetterGradedCourse_OnAnOtherwiseEqualCall()
    {
      var plain = Course("Search Ads Fundamentals", ["Advertising"], difficulty: "Intermediate", completed: 10);
      var graded = Course("Paid Media Fundamentals", ["Advertising"], difficulty: "Intermediate", completed: 10, grade: "A");

      var stage = Goal("marketing").Stages[4];
      var request = Request();

      Assert.True(PathwayEngine.ScoreCourse(graded, stage, 4, request, Options) > PathwayEngine.ScoreCourse(plain, stage, 4, request, Options));

      var result = Build([plain, graded], request);
      var picked = result.Stages[4].Courses;
      Assert.Equal(graded.OpportunityId, picked[0].OpportunityId);
      Assert.Equal("A", picked[0].Grade);
      Assert.Equal("Top pick", picked[0].GradeLabel);
      Assert.Null(picked[1].GradeLabel);
    }

    [Fact]
    public void Build_PushesNonEnglishCoursesDown_OnlyForAnEnglishFirstYouth()
    {
      var english = Course("Social Media for Business", ["Social Media"], difficulty: "Intermediate", completed: 10);
      var french = Course("Marketing des réseaux sociaux", ["Social Media"], difficulty: "Intermediate", completed: 10, languages: ["French"]);
      var stage = Goal("marketing").Stages[2];

      Assert.True(PathwayEngine.ScoreCourse(english, stage, 2, Request(), Options) > PathwayEngine.ScoreCourse(french, stage, 2, Request(), Options));
      // only the title hit differs once language does not count
      Assert.Equal(PathwayEngine.ScoreCourse(english, stage, 2, Request(englishFirst: false), Options) - 4m,
        PathwayEngine.ScoreCourse(french, stage, 2, Request(englishFirst: false), Options));
    }

    [Fact]
    public void Build_PrefersCoursesThatFitTheWeeklyTime()
    {
      var stage = Goal("marketing").Stages[2];
      var shortCourse = Course("Social Media Quick Start", ["Social Media"], difficulty: "Intermediate", hours: 4);
      var longCourse = Course("Social Media Diploma", ["Social Media"], difficulty: "Intermediate", hours: 30);

      Assert.True(PathwayEngine.ScoreCourse(shortCourse, stage, 2, Request(hours: 2), Options) > PathwayEngine.ScoreCourse(longCourse, stage, 2, Request(hours: 2), Options));
      Assert.Equal(PathwayEngine.ScoreCourse(shortCourse, stage, 2, Request(hours: 10), Options), PathwayEngine.ScoreCourse(longCourse, stage, 2, Request(hours: 10), Options));
    }

    [Fact]
    public void Build_LeavesAStageEmpty_WhenNoCourseMatchesIt()
    {
      var result = Build([Course("Principles of Marketing", ["Marketing"])], Request());

      Assert.Empty(result.Stages[0].Courses);
      Assert.Single(result.Stages[1].Courses);
      Assert.Equal(1, result.TotalCourses);
    }

    [Fact]
    public void ThroughLine_RunsThroughTheStagesToJobs()
    {
      var line = PathwayEngine.ThroughLine(Goal("web"));

      Assert.Contains("computer foundations, then build your first web pages, then learn to program, then build something real, then ship real projects", line);
      Assert.StartsWith("Each step adds", line);
      Assert.EndsWith("web developer jobs on Yoma.", line);
    }

    [Fact]
    public void MatchJobs_PrefersTheYouthsCountry_AndFallsBackToEveryCountry()
    {
      var nigeria = (Guid.NewGuid(), "Nigeria");
      var ghana = (Guid.NewGuid(), "Ghana");
      var jobs = new List<PathwayCatalogueJob>
      {
        Job("Digital Marketing Assistant", nigeria),
        Job("Social Media Coordinator", ghana),
        Job("Brand Ambassador", nigeria, ghana),
        Job("Plumber", nigeria)
      };

      var local = PathwayEngine.MatchJobs(jobs, Goal("marketing"), nigeria.Item1, 6);
      Assert.Equal(["Digital Marketing Assistant", "Brand Ambassador"], local.Select(o => o.Title).OrderByDescending(o => o));

      var elsewhere = PathwayEngine.MatchJobs(jobs, Goal("marketing"), Guid.NewGuid(), 6);
      Assert.Equal(3, elsewhere.Count);

      Assert.Equal(3, PathwayEngine.MatchJobs(jobs, Goal("marketing"), null, 6).Count);
      Assert.Single(PathwayEngine.MatchJobs(jobs, Goal("marketing"), null, 1));
    }

    [Fact]
    public void Build_AddsContinuation_AndFlagsADropOffAgainstItsSiblings()
    {
      var courses = MarketingCatalogue();
      var result = Build(courses, Request());
      var stage = result.Stages[2];

      var rates = stage.Courses.ToDictionary(o => o.OpportunityId, _ => 0.30m);
      var weak = stage.Courses[^1].OpportunityId;
      rates[weak] = 0.05m;

      var outcomes = PathwayOutcomeSnapshot.Empty(20);
      outcomes.Continuation["marketing"] = new() { [2] = rates };

      var flagged = Build(courses, Request(), outcomes: outcomes).Stages[2].Courses;
      Assert.True(flagged.Single(o => o.OpportunityId == weak).DropOffFlag);
      Assert.Equal(0.05m, flagged.Single(o => o.OpportunityId == weak).Outcome!.ContinuationRate);
      Assert.All(flagged.Where(o => o.OpportunityId != weak), o => Assert.False(o.DropOffFlag));
    }

    [Fact]
    public void IsDropOff_NeedsTwoSiblings_AndAMedianAboveZero()
    {
      var id = Guid.NewGuid();
      Assert.False(PathwayEngine.IsDropOff(id, 0, new() { [id] = 0, [Guid.NewGuid()] = 0.5m }, 0.5m));
      Assert.False(PathwayEngine.IsDropOff(id, 0, new() { [id] = 0, [Guid.NewGuid()] = 0, [Guid.NewGuid()] = 0 }, 0.5m));
      Assert.True(PathwayEngine.IsDropOff(id, 0.1m, new() { [id] = 0.1m, [Guid.NewGuid()] = 0.4m, [Guid.NewGuid()] = 0.5m }, 0.5m));
      Assert.False(PathwayEngine.IsDropOff(id, 0.3m, new() { [id] = 0.3m, [Guid.NewGuid()] = 0.4m, [Guid.NewGuid()] = 0.5m }, 0.5m));
    }

    [Fact]
    public void Stem_TreatsEditionsOfOneCourseAsTheSame()
    {
      Assert.Equal(PathwayMatching.Stem("Introduction to Microsoft Excel 2016"), PathwayMatching.Stem("Introduction to Microsoft Excel 2019"));
      Assert.NotEqual(PathwayMatching.Stem("Introduction to Python"), PathwayMatching.Stem("Introduction to Excel"));
    }

    [Fact]
    public void Configured_FillsEveryListFromTheDefaults_AndKeepsAConfiguredListWhole()
    {
      Assert.Equal(6, Options.Goals.Count);
      Assert.All(Options.Goals, o => Assert.Equal(5, o.Stages.Count));
      // learn, practise, prove: one practice stage of impact actions, just before the last stage
      Assert.All(Options.Goals, o => Assert.Equal(3, o.Stages.FindIndex(s => s.Types.Contains(PathwayStepType.ImpactAction))));
      Assert.All(Options.Goals, o => Assert.Single(o.Stages, s => s.Types.Contains(PathwayStepType.ImpactAction)));
      Assert.All(Options.Goals.SelectMany(o => o.Stages), o => PathwayMatching.Pattern(o.Pattern));

      var custom = PathwayDefaults.Configured(new PathwayOptions { Goals = [new() { Key = "one", Label = "One", Blurb = "b", JobsPattern = "x", Stages = [] }] });
      Assert.Equal(["one"], custom.Goals.Select(o => o.Key));
      Assert.Equal(8, custom.KnownSkills.Count);
    }

    [Fact]
    public void Live_EveryGoalGivesAFullPath_FromTheRealCatalogue()
    {
      var catalogue = LiveCatalogue();
      Assert.SkipWhen(catalogue == null, "scripts/course-quality-demo/.cache/live-catalogue.json is not on this machine");

      foreach (var goal in Options.Goals)
      {
        foreach (var hours in Options.Hours)
        {
          var result = Build(catalogue!, Request(goal.Key, hours: hours.HoursPerWeek));
          var courses = result.Stages.SelectMany(o => o.Courses).ToList();
          if (hours.HoursPerWeek == 5)
            TestContext.Current.TestOutputHelper?.WriteLine($"{goal.Key}: " + string.Join(" | ", result.Stages.Select(s => $"{s.Name}: {string.Join("; ", s.Courses.Select(c => $"{c.Title} [{c.Difficulty}, {c.Hours}h]"))}")));

          Assert.True(result.TotalCourses is >= 8 and <= 12, $"{goal.Key} at {hours.HoursPerWeek} h: {result.TotalCourses} courses");
          Assert.All(result.Stages, o => Assert.NotEmpty(o.Courses));
          Assert.Equal(courses.Count, courses.Select(o => PathwayMatching.Stem(o.Title)).Distinct().Count());
        }
      }
    }
  }
}
