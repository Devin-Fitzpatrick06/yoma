using Xunit;
using Yoma.Core.Domain.Pathway.Badges.Models;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Domain.Pathway.Services;
using static Yoma.Core.Test.Pathway.PathwayTestData;

namespace Yoma.Core.Test.Pathway
{
  /// <summary>
  /// Iteration 4 (W4, W5 slot, locks and swaps): steps of several kinds, partner badges, "usually go on to", the reserved drop-off,
  /// pinned and swapped steps, alternatives and the map's labels.
  /// </summary>
  public class PathwayMixedStepsTests
  {
    private const int Practice = 3;

    private static PathwayEngineResult Build(List<PathwayCatalogueCourse> courses, PathwayEngineRequest request, PathwayOptions? options = null,
      Dictionary<Guid, PartnerBadgeLevel>? badges = null) =>
      PathwayEngine.Build(new PathwayCatalogue { Courses = courses, PartnerBadges = badges ?? [] }, request, options ?? Options);

    private static List<PathwayCourse> Steps(PathwayEngineResult result) => [.. result.Stages.SelectMany(o => o.Courses)];

    [Fact]
    public void Build_PutsImpactActionsInThePracticeStage_AndOnlyThere()
    {
      var result = Build(MarketingCatalogue(), Request());

      var practice = result.Stages[Practice];
      Assert.Equal("Practise on a real campaign", practice.Name);
      Assert.Equal(2, practice.Courses.Count);
      Assert.All(practice.Courses, o => Assert.Equal(PathwayStepType.ImpactAction, o.Type));
      Assert.DoesNotContain(PathwayDefaults.PracticeFallbackNote, practice.Why);
      Assert.StartsWith("Puts ", practice.Courses[0].Reason);

      Assert.All(result.Stages.Where((_, index) => index != Practice).SelectMany(o => o.Courses), o => Assert.Equal(PathwayStepType.Learning, o.Type));
      Assert.InRange(result.TotalCourses, Options.MinCourses, Options.MaxCourses);
    }

    [Fact]
    public void Build_TakesTasksAsPracticeToo()
    {
      var courses = MarketingCatalogue().Where(o => o.Type == PathwayStepType.Learning).ToList();
      courses.Add(Impact("Share a Campaign Post for Clean Water", ["Social Media Campaigns"], type: PathwayStepType.Task));

      var practice = Build(courses, Request()).Stages[Practice];

      Assert.Equal([PathwayStepType.Task], practice.Courses.Select(o => o.Type));
    }

    [Fact]
    public void Build_FallsBackToCourses_WhenNoImpactActionFitsThePracticeStage_AndSaysSo()
    {
      var courses = MarketingCatalogue().Where(o => o.Type == PathwayStepType.Learning).ToList();
      courses.Add(Course("Community Engagement Basics", ["Community Engagement"], completed: 40));

      var practice = Build(courses, Request()).Stages[Practice];

      Assert.EndsWith(PathwayDefaults.PracticeFallbackNote, practice.Why);
      Assert.Equal(["Community Engagement Basics"], practice.Courses.Select(o => o.Title));
      Assert.Equal(PathwayStepType.Learning, practice.Courses[0].Type);
    }

    [Fact]
    public void Build_TakesRelatedImpactActions_BeforeFallingBackToCourses_SharedSkillsFirst()
    {
      List<PathwayCatalogueCourse> Catalogue() =>
      [
        Course("Excel Basics", ["Microsoft Excel"], completed: 100),
        Course("Statistics for Everyone", ["Statistics"], completed: 80),
        Course("SQL for Beginners", ["SQL"], difficulty: "Intermediate", completed: 60),
        Course("Power BI Dashboards", ["Power BI"], difficulty: "Intermediate", completed: 40),
        Course("Gathering Field Data", ["Data Collection"], completed: 20),
        Impact("Count Litter at a Beach Clean-up", ["Microsoft Excel"], completed: 1),
        Impact("Map Water Points in Your Town", [], completed: 500),
        Impact("Plant Trees in Your Neighbourhood", ["Gardening"], completed: 900)
      ];

      var courses = Catalogue();
      courses[6].Categories = ["AI, Data and Analytics"];

      var practice = Build(courses, Request("data")).Stages[Practice];

      // no impact action matches the stage's own pattern; the one sharing a skill comes before the one sharing only a category
      Assert.Equal(["Count Litter at a Beach Clean-up", "Map Water Points in Your Town"], practice.Courses.Select(o => o.Title));
      Assert.All(practice.Courses, o => Assert.Equal(PathwayStepType.ImpactAction, o.Type));
      Assert.EndsWith(PathwayDefaults.PracticeRelatedNote, practice.Why);

      // deterministic, and without a related one the stage falls back to courses
      Assert.Equal(practice.Courses.Select(o => o.OpportunityId), Build(Enumerable.Reverse(courses).ToList(), Request("data")).Stages[Practice].Courses.Select(o => o.OpportunityId));
      var unrelated = Catalogue().Where(o => o.Title != "Count Litter at a Beach Clean-up").ToList();
      var fallback = Build(unrelated, Request("data")).Stages[Practice];
      Assert.Equal(["Gathering Field Data"], fallback.Courses.Select(o => o.Title));
      Assert.EndsWith(PathwayDefaults.PracticeFallbackNote, fallback.Why);
    }

    [Fact]
    public void Build_ReservesDropOff_AndFillsBadgesAndUsuallyNext()
    {
      var courses = MarketingCatalogue();
      var other = Guid.NewGuid();
      courses[5].OrganizationId = other;
      var badges = new Dictionary<Guid, PartnerBadgeLevel> { [OpenLearningOrgId] = PartnerBadgeLevel.Gold };

      var request = Request();
      var next = new PathwayNextStep { OpportunityId = courses[10].OpportunityId, Title = courses[10].Title, Share = 0.4m, Count = 12 };
      request.UsuallyNext[courses[5].OpportunityId] = [next];

      var steps = Steps(Build(courses, request, badges: badges));

      Assert.All(steps, o =>
      {
        Assert.Equal(StepDropOffStatus.Collecting, o.DropOff.Status);
        Assert.Null(o.DropOff.Rate);
        Assert.Equal("Collecting from the pilot", o.DropOff.Note);
        Assert.False(o.Locked);
      });

      var marketing = steps.Single(o => o.OpportunityId == courses[5].OpportunityId);
      Assert.Null(marketing.PartnerBadge);
      Assert.Equal(next.OpportunityId, Assert.Single(marketing.UsuallyNext).OpportunityId);
      Assert.Equal(0.4m, marketing.UsuallyNext[0].Share);

      Assert.All(steps.Where(o => o.OpportunityId != marketing.OpportunityId), o =>
      {
        Assert.Equal(PartnerBadgeLevel.Gold, o.PartnerBadge);
        Assert.Empty(o.UsuallyNext);
      });
    }

    [Fact]
    public void Build_IsDeterministic_WhateverTheCatalogueOrder()
    {
      var courses = MarketingCatalogue();
      var first = Steps(Build(courses, Request())).Select(o => o.OpportunityId).ToList();

      var reversed = Enumerable.Reverse(courses).ToList();
      var second = Steps(Build(reversed, Request())).Select(o => o.OpportunityId).ToList();

      Assert.Equal(first, second);
    }

    [Fact]
    public void Live_EveryGoalKeepsEightToTwelveSteps_WithAPracticeStage()
    {
      var catalogue = LiveCatalogue();
      Assert.SkipWhen(catalogue == null, "scripts/course-quality-demo/.cache/live-catalogue.json is not on this machine");

      foreach (var goal in Options.Goals)
      {
        var result = Build(catalogue!, Request(goal.Key));
        Assert.True(result.TotalCourses is >= 8 and <= 12, $"{goal.Key}: {result.TotalCourses} steps");
        Assert.NotEmpty(result.Stages[Practice].Courses);
        // the cached live sample is courses only, so every practice stage says courses stand in
        Assert.EndsWith(PathwayDefaults.PracticeFallbackNote, result.Stages[Practice].Why);
      }
    }

    [Fact]
    public void Build_KeepsALockedStep_ThatWouldNotOtherwiseBePicked()
    {
      var courses = MarketingCatalogue();
      var weak = Course("Marketing for Beginners Workbook", ["Marketing"], completed: 0, hours: 30);
      courses.Add(weak);
      Assert.DoesNotContain(weak.OpportunityId, Steps(Build(courses, Request())).Select(o => o.OpportunityId));

      var request = Request();
      request.Locked = [weak.OpportunityId];
      var result = Build(courses, request);

      var stage = result.Stages.Single(o => o.Courses.Any(c => c.OpportunityId == weak.OpportunityId));
      Assert.Equal("Learn how marketing works", stage.Name);
      Assert.True(stage.Courses[0].Locked);
      Assert.Equal(weak.OpportunityId, stage.Courses[0].OpportunityId);
      Assert.InRange(result.TotalCourses, Options.MinCourses, Options.MaxCourses);
    }

    [Fact]
    public void Build_NeverTrimsALockedStep()
    {
      var courses = MarketingCatalogue();
      var request = Request();
      request.Locked = [.. courses.Where(o => o.Type == PathwayStepType.Learning && o.Title != "Plumbing Basics").Take(5).Select(o => o.OpportunityId)];
      var options = PathwayDefaults.Configured(new PathwayOptions { MinCourses = 4, MaxCourses = 6 });

      var result = Build(courses, request, options);

      Assert.Equal(6, result.TotalCourses);
      Assert.All(request.Locked, id => Assert.Contains(id, Steps(result).Where(o => o.Locked).Select(o => o.OpportunityId)));
    }

    [Fact]
    public void Build_LeavesOutALockedStepThatIsNoLongerListed()
    {
      var request = Request();
      request.Locked = [Guid.NewGuid()];

      var result = Build(MarketingCatalogue(), request);

      Assert.DoesNotContain(Steps(result), o => o.Locked);
    }

    [Fact]
    public void Build_SwapsAStep_ForTheChosenAlternative_InTheSameStage()
    {
      var courses = MarketingCatalogue();
      var plain = Build(courses, Request());
      var replaced = plain.Stages[2].Courses[0];

      var request = Request();
      var alternative = PathwayEngine.Alternatives(new PathwayCatalogue { Courses = courses }, request, Options, plain, replaced.OpportunityId)[0];
      request.Swaps = [(replaced.OpportunityId, alternative.OpportunityId)];

      var swapped = Build(courses, request);

      Assert.DoesNotContain(replaced.OpportunityId, Steps(swapped).Select(o => o.OpportunityId));
      Assert.Contains(alternative.OpportunityId, swapped.Stages[2].Courses.Select(o => o.OpportunityId));
      Assert.False(swapped.Stages[2].Courses.Single(o => o.OpportunityId == alternative.OpportunityId).Locked);
      Assert.Equal(plain.TotalCourses, swapped.TotalCourses);
    }

    [Fact]
    public void Build_SwapsAnImpactAction_ForAnother()
    {
      var courses = MarketingCatalogue();
      var plain = Build(courses, Request());
      var replaced = plain.Stages[Practice].Courses[0];

      var alternatives = PathwayEngine.Alternatives(new PathwayCatalogue { Courses = courses }, Request(), Options, plain, replaced.OpportunityId);
      var alternative = Assert.Single(alternatives);
      Assert.Equal("Volunteer at a Youth Market Day", alternative.Title);
      Assert.Equal(PathwayStepType.ImpactAction, alternative.Type);

      var request = Request();
      request.Swaps = [(replaced.OpportunityId, alternative.OpportunityId)];
      var practice = Build(courses, request).Stages[Practice];

      Assert.Contains(alternative.OpportunityId, practice.Courses.Select(o => o.OpportunityId));
      Assert.DoesNotContain(replaced.OpportunityId, practice.Courses.Select(o => o.OpportunityId));
    }

    [Fact]
    public void ResolveSwaps_FollowsChains_AndSwappingBackUndoesIt()
    {
      var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

      var chain = PathwayEngine.ResolveSwaps([(a, b), (b, c)]);
      Assert.Equal([a, b], chain.Banned.OrderBy(o => o == b));
      Assert.Equal([(a, c)], chain.Replacements);

      var back = PathwayEngine.ResolveSwaps([(a, b), (b, a)]);
      Assert.Equal([b], back.Banned);
      Assert.Empty(back.Replacements);

      Assert.Empty(PathwayEngine.ResolveSwaps([(a, a)]).Banned);
    }

    [Fact]
    public void Alternatives_AreUpToThree_OfTheSameStageAndKind_AndNotOnThePath()
    {
      var courses = MarketingCatalogue();
      courses.Add(Course("Instagram for Small Brands", ["Social Media"], difficulty: "Intermediate", completed: 30));
      courses.Add(Course("TikTok Marketing Basics", ["Social Media"], difficulty: "Intermediate", completed: 20));
      courses.Add(Course("YouTube Channel Growth", ["Social Media", "Content Marketing"], difficulty: "Intermediate", completed: 25));
      courses.Add(Course("LinkedIn Content Planning", ["Content Marketing"], difficulty: "Intermediate", completed: 15));

      var catalogue = new PathwayCatalogue { Courses = courses };
      var path = PathwayEngine.Build(catalogue, Request(), Options);
      var step = path.Stages[2].Courses[1];

      var alternatives = PathwayEngine.Alternatives(catalogue, Request(), Options, path, step.OpportunityId);
      var onPath = Steps(path).Select(o => o.OpportunityId).ToHashSet();
      var stage = Goal("marketing").Stages[2];

      Assert.Equal(3, alternatives.Count);
      Assert.All(alternatives, o =>
      {
        Assert.DoesNotContain(o.OpportunityId, onPath);
        Assert.Equal(PathwayStepType.Learning, o.Type);
        Assert.NotNull(PathwayEngine.ScoreCourse(courses.Single(c => c.OpportunityId == o.OpportunityId), stage, 2, Request(), Options));
      });
      Assert.Equal(alternatives.Select(o => o.OpportunityId), PathwayEngine.Alternatives(catalogue, Request(), Options, path, step.OpportunityId).Select(o => o.OpportunityId));

      Assert.Empty(PathwayEngine.Alternatives(catalogue, Request(), Options, path, Guid.NewGuid()));
    }

    [Fact]
    public void Labels_SayWhereTheMapStartsAndEnds()
    {
      var finished = Options.Backgrounds.Single(o => o.Background == PathwayBackground.FinishedSchool);

      Assert.Equal("Finished school · Nigeria", PathwayEngine.StartLabel(finished, "Nigeria"));
      Assert.Equal("Finished school", PathwayEngine.StartLabel(finished, null));
      Assert.Equal("Your first digital marketing job", PathwayEngine.DestinationLabel(Goal("marketing")));
      Assert.Equal("Your first nurse job", PathwayEngine.DestinationLabel(new PathwayGoalOptions { Key = "nurse", Label = "Nurse", Blurb = "b", JobsPattern = "x" }));
      Assert.All(Options.Goals, o => Assert.False(string.IsNullOrWhiteSpace(PathwayEngine.DestinationLabel(o))));
    }
  }
}
