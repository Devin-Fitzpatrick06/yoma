using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using Xunit;
using Yoma.Core.Api.Controllers;
using Yoma.Core.Domain;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Pathway.Badges.Interfaces;
using Yoma.Core.Domain.Pathway.Badges.Models;
using Yoma.Core.Domain.Pathway.Badges.Services;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.CourseQuality.Repositories;
using Yoma.Core.Infrastructure.Database.MyOpportunity.Repositories;
using Yoma.Core.Infrastructure.Database.Opportunity.Repositories;
using OpportunityType = Yoma.Core.Domain.Opportunity.Type;

namespace Yoma.Core.Test.Pathway
{
  public class PathwayBadgeTests
  {
    #region Helpers
    private static readonly DateTimeOffset Day0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly LearnerBadgeOptions LearnerOptions = new();
    private static readonly PartnerBadgeOptions PartnerOptions = new();

    private static readonly Guid[] Ids = [.. Enumerable.Range(0, 10).Select(_ => Guid.NewGuid())];

    /// <summary>
    /// Two stages: Ids 0-1, then Ids 2-4.
    /// </summary>
    private static LearnerBadgePath Path(DateTimeOffset? goalSetOn = null) => new(goalSetOn ?? Day0,
    [
      new LearnerBadgeStage(1, "Foundations", [Ids[0], Ids[1]]),
      new LearnerBadgeStage(2, "Core skills", [Ids[2], Ids[3], Ids[4]])
    ]);

    private static LearnerBadgeCompletion Done(int id, int day, LearnerBadgeEvidence evidence = LearnerBadgeEvidence.Verified, int skills = 1,
      CourseGrade? grade = null, OpportunityType type = OpportunityType.Learning, bool credential = false) =>
      new(Ids[id], type, Day0.AddDays(day), evidence, skills, credential, grade);

    private static List<LearnerBadge> Badges(LearnerBadgePath? path, params LearnerBadgeCompletion[] completions) =>
      LearnerBadgeCalculator.Calculate(path, completions, LearnerOptions);

    private static LearnerBadge Badge(List<LearnerBadge> badges, LearnerBadgeKey key) => badges.Single(o => o.Key == key);

    private static PartnerBadge Partner(int top, int graded, int heldBack = 0, int waiting = 0, PartnerBadgeOptions? options = null) =>
      PartnerBadgeCalculator.Calculate(Guid.NewGuid(), "Northwind Learning", new PartnerBadgeCounts(top, graded, heldBack, waiting), options ?? PartnerOptions);
    #endregion

    #region Learner badges
    [Fact]
    public void NoPathNoCompletions_ListsEveryBadge_NoneEarned()
    {
      var badges = Badges(null);

      Assert.Equal(Enum.GetValues<LearnerBadgeKey>(), badges.Select(o => o.Key));
      Assert.All(badges, o =>
      {
        Assert.False(o.Earned);
        Assert.Null(o.DateEarned);
        Assert.Equal(0m, o.Progress);
        Assert.False(string.IsNullOrWhiteSpace(o.Label));
        Assert.False(string.IsNullOrWhiteSpace(o.Description));
      });
    }

    [Fact]
    public void Pathfinder_IsEarned_OnTheDayTheGoalWasSet()
    {
      var badge = Badge(Badges(Path(Day0.AddDays(3))), LearnerBadgeKey.Pathfinder);

      Assert.True(badge.Earned);
      Assert.Equal(Day0.AddDays(3), badge.DateEarned);
      Assert.Null(badge.Progress);
    }

    [Fact]
    public void FirstStep_TakesTheEarliestPathCompletion_AndIgnoresCoursesOffThePath()
    {
      var badge = Badge(Badges(Path(), Done(7, 1), Done(3, 10), Done(0, 5)), LearnerBadgeKey.FirstStep);

      Assert.True(badge.Earned);
      Assert.Equal(Day0.AddDays(5), badge.DateEarned);
    }

    [Fact]
    public void SelfReportedCompletions_EarnNothing()
    {
      var self = Enumerable.Range(0, 5).Select(o => Done(o, o, LearnerBadgeEvidence.SelfReported, grade: CourseGrade.A)).ToArray();

      var badges = Badges(Path(), self);

      Assert.All(badges.Where(o => o.Key != LearnerBadgeKey.Pathfinder), o => Assert.False(o.Earned));
    }

    [Fact]
    public void StageComplete_IsDatedWhenTheFirstStageWasFinished()
    {
      // stage 2 finished on day 9, stage 1 only on day 20
      var badge = Badge(Badges(Path(), Done(2, 3), Done(3, 9), Done(4, 6), Done(0, 1), Done(1, 20)), LearnerBadgeKey.StageComplete);

      Assert.True(badge.Earned);
      Assert.Equal(Day0.AddDays(9), badge.DateEarned);
    }

    [Fact]
    public void StageComplete_Progress_IsTheFurthestStage()
    {
      var badge = Badge(Badges(Path(), Done(0, 1), Done(2, 2), Done(3, 3)), LearnerBadgeKey.StageComplete);

      Assert.False(badge.Earned);
      Assert.Equal(0.67m, badge.Progress);
    }

    [Fact]
    public void HalfwayThere_NeedsHalfRoundedUp_AndIsDatedByThatCompletion()
    {
      var notYet = Badge(Badges(Path(), Done(0, 1), Done(4, 2)), LearnerBadgeKey.HalfwayThere);
      Assert.False(notYet.Earned);
      Assert.Equal(0.67m, notYet.Progress);

      var earned = Badge(Badges(Path(), Done(0, 1), Done(4, 2), Done(2, 7), Done(3, 8)), LearnerBadgeKey.HalfwayThere);
      Assert.True(earned.Earned);
      Assert.Equal(Day0.AddDays(7), earned.DateEarned);
    }

    [Fact]
    public void JobReady_NeedsEveryCourse_AndCountsACourseInTwoStagesOnce()
    {
      var path = new LearnerBadgePath(Day0, [new LearnerBadgeStage(1, "A", [Ids[0], Ids[1]]), new LearnerBadgeStage(2, "B", [Ids[1], Ids[2]])]);

      var notYet = Badge(Badges(path, Done(0, 1), Done(1, 2)), LearnerBadgeKey.JobReady);
      Assert.False(notYet.Earned);
      Assert.Equal(0.67m, notYet.Progress);

      var earned = Badges(path, Done(0, 1), Done(1, 2), Done(2, 4));
      Assert.True(Badge(earned, LearnerBadgeKey.JobReady).Earned);
      Assert.Equal(Day0.AddDays(4), Badge(earned, LearnerBadgeKey.JobReady).DateEarned);
      Assert.Equal(Day0.AddDays(2), Badge(earned, LearnerBadgeKey.StageComplete).DateEarned);
    }

    [Fact]
    public void EmptyStages_AreIgnored()
    {
      var path = new LearnerBadgePath(Day0, [new LearnerBadgeStage(1, "Already known", []), new LearnerBadgeStage(2, "B", [Ids[0]])]);

      var badges = Badges(path, Done(0, 2));

      Assert.True(Badge(badges, LearnerBadgeKey.StageComplete).Earned);
      Assert.True(Badge(badges, LearnerBadgeKey.JobReady).Earned);
    }

    [Fact]
    public void ActionLinkCompletions_MoveThePath_ButBackNoSkill()
    {
      var badges = Badges(Path(), Done(0, 1, LearnerBadgeEvidence.ActionLink, skills: 3, credential: true));

      Assert.True(Badge(badges, LearnerBadgeKey.FirstStep).Earned);
      Assert.False(Badge(badges, LearnerBadgeKey.VerifiedSkill).Earned);
    }

    [Fact]
    public void VerifiedSkill_NeedsAVerifiedCompletionThatAwardsASkill()
    {
      Assert.False(Badge(Badges(null, Done(5, 1, skills: 0)), LearnerBadgeKey.VerifiedSkill).Earned);

      var badge = Badge(Badges(null, Done(5, 1, skills: 0), Done(6, 4, type: OpportunityType.Event), Done(7, 9)), LearnerBadgeKey.VerifiedSkill);

      Assert.True(badge.Earned);
      Assert.Equal(Day0.AddDays(4), badge.DateEarned);
    }

    [Fact]
    public void TopPickLearner_NeedsThreeAGradeCourses()
    {
      var two = Badge(Badges(null, Done(0, 1, grade: CourseGrade.A), Done(1, 2, grade: CourseGrade.A), Done(2, 3, grade: CourseGrade.B),
        Done(3, 4, grade: CourseGrade.A, type: OpportunityType.Event)), LearnerBadgeKey.TopPickLearner);
      Assert.False(two.Earned);
      Assert.Equal(0.67m, two.Progress);

      var three = Badge(Badges(null, Done(0, 1, grade: CourseGrade.A), Done(1, 8, grade: CourseGrade.A), Done(2, 5, grade: CourseGrade.A),
        Done(4, 2, LearnerBadgeEvidence.SelfReported, grade: CourseGrade.A)), LearnerBadgeKey.TopPickLearner);
      Assert.True(three.Earned);
      Assert.Equal(Day0.AddDays(8), three.DateEarned);
    }

    [Fact]
    public void Progress_NeverReadsAsDone_UntilEarned()
    {
      var stage = Enumerable.Range(0, 10).Select(o => Ids[o]).ToList();
      var path = new LearnerBadgePath(Day0, [new LearnerBadgeStage(1, "Long", stage)]);
      var nearly = Enumerable.Range(0, 9).Select(o => Done(o, o)).ToArray();

      var badge = Badge(Badges(path, nearly), LearnerBadgeKey.JobReady);

      Assert.False(badge.Earned);
      Assert.Equal(0.9m, badge.Progress);
    }

    [Theory]
    [InlineData("Auto-verification: Action Link", LearnerBadgeEvidence.ActionLink)]
    [InlineData("Auto-verification: Action Link; ZLTO not awarded as reward pool has been depleted", LearnerBadgeEvidence.ActionLink)]
    [InlineData("Auto-verification: Partner Sync", LearnerBadgeEvidence.Verified)]
    [InlineData("Auto-verification: CSV Import", LearnerBadgeEvidence.Verified)]
    [InlineData("Well done", LearnerBadgeEvidence.Verified)]
    [InlineData(null, LearnerBadgeEvidence.Verified)]
    public void Evidence_IsReadFromTheVerificationComment(string? comment, LearnerBadgeEvidence expected)
    {
      Assert.Equal(expected, PathwayBadgeService.Evidence(comment));
    }
    #endregion

    #region Partner badges
    [Fact]
    public void Partner_BelowTheMinimum_IsNotEnoughGraded_WithoutShares()
    {
      var badge = Partner(top: 6, graded: 9, heldBack: 1, waiting: 40);

      Assert.Equal(PartnerBadgeLevel.NotEnoughGraded, badge.Level);
      Assert.Null(badge.ShareTopGrades);
      Assert.Null(badge.ShareHeldBack);
      Assert.Equal(10, badge.MinimumGradedCourses);
      Assert.Equal("9 of the 10 graded courses needed for a badge. 40 courses are waiting for review and are not counted.", badge.NextLevelHint);
    }

    [Fact]
    public void Partner_Gold_HasNoNextLevel()
    {
      var badge = Partner(top: 14, graded: 20);

      Assert.Equal(PartnerBadgeLevel.Gold, badge.Level);
      Assert.Equal(0.7m, badge.ShareTopGrades);
      Assert.Equal(0m, badge.ShareHeldBack);
      Assert.Null(badge.NextLevelHint);
    }

    [Fact]
    public void Partner_HeldBackCoursesCapTheLevel_AndTheHintSaysHowManyToFix()
    {
      // 70% at A or B, but 3 of 23 decided courses (13%) held back: Bronze allows 20%, Silver 10%
      var badge = Partner(top: 14, graded: 20, heldBack: 3);

      Assert.Equal(PartnerBadgeLevel.Bronze, badge.Level);
      Assert.Equal(0.13m, Math.Round(badge.ShareHeldBack!.Value, 2));
      Assert.Equal("For Silver: 1 fewer course held back as Not verified (13% now, at most 10%). The fix notes say what each held-back course needs.", badge.NextLevelHint);
    }

    [Fact]
    public void Partner_None_HintCountsCoursesToReachBronze()
    {
      var badge = Partner(top: 4, graded: 20);

      Assert.Equal(PartnerBadgeLevel.None, badge.Level);
      Assert.Equal("For Bronze: about 2 more courses at grade A or B (20% now, 30% needed). The fix notes say what each held-back course needs.", badge.NextLevelHint);
    }

    [Fact]
    public void Partner_CoursesWaitingForReview_DoNotCountAgainstThePartner()
    {
      var without = Partner(top: 10, graded: 20, heldBack: 1);
      var with = Partner(top: 10, graded: 20, heldBack: 1, waiting: 300);

      Assert.Equal(without.Level, with.Level);
      Assert.Equal(without.ShareTopGrades, with.ShareTopGrades);
      Assert.Equal(without.ShareHeldBack, with.ShareHeldBack);
      Assert.EndsWith("300 courses are waiting for review and are not counted.", with.NextLevelHint);
    }

    [Fact]
    public void Partner_ThresholdsChangeThroughConfiguration()
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Pathway:PartnerBadges:MinimumGradedCourses"] = "5",
        ["Pathway:PartnerBadges:Levels:Gold:MinShareTopGrades"] = "0.5"
      }).Build();
      var options = new PartnerBadgeOptions();
      configuration.GetSection(PartnerBadgeOptions.Section).Bind(options);

      Assert.Equal(5, options.MinimumGradedCourses);
      Assert.Equal(0.5m, options.Levels[PartnerBadgeLevel.Gold].MinShareTopGrades);
      Assert.Equal(0.05m, options.Levels[PartnerBadgeLevel.Gold].MaxShareHeldBack);
      Assert.Equal(3, options.Levels.Count);

      Assert.Equal(PartnerBadgeLevel.Gold, Partner(top: 3, graded: 6, options: options).Level);
    }
    #endregion

    #region Wiring
    private static readonly JsonSerializerSettings ApiSettings = new()
    {
      ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
      Converters = [new StringEnumConverter()]
    };

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
      .UseNpgsql("Host=localhost;Database=none", o => o.UseNetTopologySuite()).Options);

    [Fact]
    public void Badges_SerializeLikeTheWebModel()
    {
      var learner = JObject.Parse(JsonConvert.SerializeObject(Badges(Path())[0], ApiSettings));
      Assert.Equal(["dateEarned", "description", "earned", "key", "label", "progress"], learner.Properties().Select(o => o.Name).Order());
      Assert.Equal("Pathfinder", learner["key"]!.Value<string>());

      var partner = JObject.Parse(JsonConvert.SerializeObject(Partner(top: 2, graded: 3), ApiSettings));
      Assert.Equal(["gradedCourses", "level", "minimumGradedCourses", "nextLevelHint", "organizationId", "organizationName", "shareHeldBack", "shareTopGrades"],
        partner.Properties().Select(o => o.Name).Order());
      Assert.Equal("NotEnoughGraded", partner["level"]!.Value<string>());
    }

    [Theory]
    [InlineData(nameof(PathwayBadgeController.ListMine), "me", "User")]
    [InlineData(nameof(PathwayBadgeController.GetPartnerBadge), "organization/{organizationId}", null)]
    public void Routes_MatchTheContract(string action, string template, string? roles)
    {
      var method = typeof(PathwayBadgeController).GetMethod(action)!;
      var attribute = method.GetCustomAttribute<HttpMethodAttribute>()!;

      Assert.Equal("GET", attribute.HttpMethods.Single());
      Assert.Equal(template, attribute.Template);
      Assert.Equal("api/v3/pathway/badges", typeof(PathwayBadgeController).GetCustomAttribute<RouteAttribute>()!.Template);
      Assert.Equal(roles, method.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
      Assert.Equal(roles == null, method.GetCustomAttribute<AllowAnonymousAttribute>() != null);
    }

    [Fact]
    public void Domain_RegistersOneBadgeService_AndThePathwayPathProvider()
    {
      var services = new ServiceCollection();
      services.ConfigureServices_DomainServices();

      Assert.Contains(services, o => o.ServiceType == typeof(IPathwayBadgeService));
      Assert.Contains(services, o => o.ServiceType == typeof(IPathwayBadgeSource));
      Assert.Equal(typeof(Yoma.Core.Domain.Pathway.Services.PathwayBadgePathProvider), services.Last(o => o.ServiceType == typeof(IPathwayBadgePathProvider)).ImplementationType);
    }

    [Fact]
    public void CompletionQuery_TranslatesToSql()
    {
      using var context = Context();

      var sql = PathwayBadgeService.QueryCompletions(new MyOpportunityRepository(context).Query(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).ToQueryString();

      Assert.Contains("\"VerificationStatusId\"", sql);
      Assert.Contains("\"DateCompleted\" IS NOT NULL", sql);
    }

    [Fact]
    public void PartnerQueries_TranslateToSql_WithTheCoursesAsASubquery()
    {
      using var context = Context();
      var courseIds = PathwayBadgeService.QueryPartnerCourseIds(new OpportunityRepository(context).Query(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

      var grades = new CourseQualityGradeRepository(context).Query().Where(o => courseIds.Contains(o.OpportunityId)).Select(o => new { o.OpportunityId, o.Grade }).ToQueryString();
      var latest = new CourseQualityAssessmentRepository(context).Query().Where(o => o.IsLatest && courseIds.Contains(o.OpportunityId))
        .Select(o => new { o.OpportunityId, o.Status }).ToQueryString();

      Assert.Contains("\"Opportunity\"", grades);
      Assert.Contains("\"Hidden\"", latest);
      Assert.DoesNotContain("= ANY", grades);
    }
    #endregion
  }
}
