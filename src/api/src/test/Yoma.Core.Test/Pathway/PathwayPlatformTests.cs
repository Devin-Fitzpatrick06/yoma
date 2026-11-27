using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Xunit;
using Yoma.Core.Api.Attributes;
using Yoma.Core.Api.Controllers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Pathway.Badges.Models;
using Yoma.Core.Domain.Pathway.Badges.Services;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Test.Pathway
{
  /// <summary>
  /// Iteration 4, A1: the Pathway:Enabled switch, the CourseQuality:PrePublish options, Platinum, reviewDue and the batched partner badges.
  /// </summary>
  public class PathwayPlatformTests
  {
    #region Helpers
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly PartnerBadgeOptions Options = new();

    private static PartnerBadge Partner(int top, int graded, int heldBack = 0, int waiting = 0, PartnerBadgeOptions? options = null) =>
      PartnerBadgeCalculator.Calculate(Guid.NewGuid(), "Northwind Learning", new PartnerBadgeCounts(top, graded, heldBack, waiting), options ?? Options, Now);

    private static string ApiAppSettings()
    {
      for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
      {
        var path = Path.Combine(dir.FullName, "src", "application", "Yoma.Core.Api", "appsettings.json");
        if (File.Exists(path)) return path;
        path = Path.Combine(dir.FullName, "application", "Yoma.Core.Api", "appsettings.json");
        if (File.Exists(path)) return path;
      }

      throw new FileNotFoundException("API appsettings.json not found");
    }

    private static IServiceProvider Services(bool? enabled)
    {
      var services = new ServiceCollection();
      if (enabled.HasValue) services.Configure<PathwayOptions>(o => o.Enabled = enabled.Value);
      return services.BuildServiceProvider();
    }

    private static ResourceExecutingContext ResourceContext(bool? enabled)
    {
      var http = new DefaultHttpContext { RequestServices = Services(enabled) };
      return new ResourceExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [], new List<IValueProviderFactory>());
    }
    #endregion

    #region Switch
    [Fact]
    public void Switch_Off_FilterAnswers404()
    {
      var context = ResourceContext(false);

      new PathwayEnabledAttribute().OnResourceExecuting(context);

      Assert.Equal(404, Assert.IsType<StatusCodeResult>(context.Result).StatusCode);
    }

    [Fact]
    public void Switch_NotConfigured_IsOff()
    {
      var context = ResourceContext(null);

      new PathwayEnabledAttribute().OnResourceExecuting(context);

      Assert.Equal(404, Assert.IsType<StatusCodeResult>(context.Result).StatusCode);
    }

    [Fact]
    public void Switch_On_FilterLetsTheRequestThrough()
    {
      var context = ResourceContext(true);

      new PathwayEnabledAttribute().OnResourceExecuting(context);

      Assert.Null(context.Result);
    }

    [Fact]
    public void Switch_RunsFirstAmongFilters()
    {
      Assert.Equal(int.MinValue, new PathwayEnabledAttribute().Order);
    }

    [Theory]
    [InlineData(false, true, 404, false)]
    [InlineData(true, true, 200, true)]
    [InlineData(false, false, 200, true)]
    public async Task Switch_Middleware_GatesOnlyMarkedEndpoints(bool enabled, bool marked, int status, bool nextCalled)
    {
      var http = new DefaultHttpContext { RequestServices = Services(enabled) };
      var metadata = marked ? new EndpointMetadataCollection(new PathwayEnabledAttribute()) : new EndpointMetadataCollection();
      http.SetEndpoint(new Endpoint(null, metadata, "test"));
      var called = false;

      await PathwayEnabledApplicationBuilderExtensions.Gate(http, _ => { called = true; return Task.CompletedTask; });

      Assert.Equal(status, http.Response.StatusCode);
      Assert.Equal(nextCalled, called);
    }

    [Fact]
    public void Switch_IsOnEveryPathwayController()
    {
      var controllers = typeof(PathwayController).Assembly.GetTypes()
        .Where(o => typeof(ControllerBase).IsAssignableFrom(o) && !o.IsAbstract)
        .Where(o => o.GetCustomAttribute<RouteAttribute>()?.Template.StartsWith("api/v3/pathway", StringComparison.OrdinalIgnoreCase) == true)
        .ToList();

      Assert.Contains(typeof(PathwayController), controllers);
      Assert.Contains(typeof(PathwayBadgeController), controllers);
      Assert.All(controllers, o => Assert.NotNull(o.GetCustomAttribute<PathwayEnabledAttribute>()));
    }

    [Fact]
    public void AppSettings_SwitchesOffByDefault()
    {
      var configuration = new ConfigurationBuilder().AddJsonFile(ApiAppSettings()).Build();

      var pathway = new PathwayOptions();
      configuration.GetSection(PathwayOptions.Section).Bind(pathway);
      var courseQuality = configuration.GetSection(CourseQualityOptions.Section).Get<CourseQualityOptions>()!;

      Assert.False(pathway.Enabled);
      Assert.False(courseQuality.PrePublish.Enabled);
      Assert.Equal(3000, courseQuality.PrePublish.LinkTimeoutMilliseconds);
    }

    [Fact]
    public void Switches_DefaultOff_AndBindFromConfiguration()
    {
      Assert.False(new PathwayOptions().Enabled);
      Assert.False(new CourseQualityOptions().PrePublish.Enabled);
      Assert.Equal(3000, new CourseQualityOptions().PrePublish.LinkTimeoutMilliseconds);

      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Pathway:Enabled"] = "true",
        ["CourseQuality:PrePublish:Enabled"] = "true",
        ["CourseQuality:PrePublish:LinkTimeoutMilliseconds"] = "1500"
      }).Build();

      var pathway = new PathwayOptions();
      configuration.GetSection(PathwayOptions.Section).Bind(pathway);
      var courseQuality = configuration.GetSection(CourseQualityOptions.Section).Get<CourseQualityOptions>()!;

      Assert.True(pathway.Enabled);
      Assert.True(courseQuality.PrePublish.Enabled);
      Assert.Equal(1500, courseQuality.PrePublish.LinkTimeoutMilliseconds);
    }
    #endregion

    #region Platinum
    [Fact]
    public void Platinum_AtTheThresholds()
    {
      // 75% at A or B, 0 held back, exactly 25 graded
      var badge = Partner(top: 75, graded: 100);

      Assert.Equal(PartnerBadgeLevel.Platinum, badge.Level);
      Assert.Equal(PartnerBadgeLevel.Platinum, Partner(top: 19, graded: 25).Level);
    }

    [Fact]
    public void Platinum_HasNoNextLevel_ButStillCountsWaitingCourses()
    {
      Assert.Null(Partner(top: 30, graded: 30).NextLevelHint);
      Assert.Equal("4 courses are waiting for review and are not counted.", Partner(top: 30, graded: 30, waiting: 4).NextLevelHint);
    }

    [Fact]
    public void Platinum_NeedsItsOwnMinimumOfGradedCourses()
    {
      // 100% at A or B over 24 graded courses: above Gold's minimum (10), below Platinum's (25)
      var badge = Partner(top: 24, graded: 24);

      Assert.Equal(PartnerBadgeLevel.Gold, badge.Level);
      Assert.Equal("For Platinum: 1 more graded course (24 now, 25 needed). The fix notes say what each held-back course needs.", badge.NextLevelHint);
    }

    [Fact]
    public void Platinum_TopGradesJustShort_HintCountsCourses()
    {
      // 74 of 100 (74%): one more at A or B reaches 75%
      var badge = Partner(top: 74, graded: 100);

      Assert.Equal(PartnerBadgeLevel.Gold, badge.Level);
      Assert.Equal("For Platinum: about 1 more course at grade A or B (74% now, 75% needed). The fix notes say what each held-back course needs.", badge.NextLevelHint);
    }

    [Fact]
    public void Platinum_HeldBackAbove2Percent_StaysGold_AndTheHintSaysHowManyToFix()
    {
      // 80% at A or B; 3 of 103 decided (2.9%) held back: Gold allows 5%, Platinum 2%
      var badge = Partner(top: 80, graded: 100, heldBack: 3);

      Assert.Equal(PartnerBadgeLevel.Gold, badge.Level);
      Assert.Equal("For Platinum: 1 fewer course held back as Not verified (3% now, at most 2%). The fix notes say what each held-back course needs.", badge.NextLevelHint);
    }

    [Fact]
    public void Platinum_EveryShortfallInOneHint()
    {
      var badge = Partner(top: 14, graded: 20, heldBack: 1);

      Assert.Equal(PartnerBadgeLevel.Gold, badge.Level);
      Assert.Equal("For Platinum: 5 more graded courses (20 now, 25 needed), and about 1 more course at grade A or B (70% now, 75% needed), and 1 fewer course held back as Not verified (5% now, at most 2%). "
        + "The fix notes say what each held-back course needs.", badge.NextLevelHint);
    }

    [Fact]
    public void Platinum_FairnessGuard_WaitingCoursesCountNeitherWay()
    {
      var without = Partner(top: 80, graded: 100);
      var with = Partner(top: 80, graded: 100, waiting: 500);

      Assert.Equal(PartnerBadgeLevel.Platinum, with.Level);
      Assert.Equal(without.ShareTopGrades, with.ShareTopGrades);
      Assert.Equal(without.ShareHeldBack, with.ShareHeldBack);
    }

    [Fact]
    public void Platinum_BelowTheBadgeMinimum_IsStillNotEnoughGraded()
    {
      var badge = Partner(top: 9, graded: 9);

      Assert.Equal(PartnerBadgeLevel.NotEnoughGraded, badge.Level);
      Assert.Null(badge.ShareTopGrades);
    }

    [Fact]
    public void Platinum_ThresholdsChangeThroughConfiguration()
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Pathway:PartnerBadges:Levels:Platinum:MinShareTopGrades"] = "0.9",
        ["Pathway:PartnerBadges:Levels:Platinum:MinimumGradedCourses"] = "50"
      }).Build();
      var options = new PartnerBadgeOptions();
      configuration.GetSection(PartnerBadgeOptions.Section).Bind(options);

      Assert.Equal(0.9m, options.Levels[PartnerBadgeLevel.Platinum].MinShareTopGrades);
      Assert.Equal(0.02m, options.Levels[PartnerBadgeLevel.Platinum].MaxShareHeldBack);
      Assert.Equal(50, options.Levels[PartnerBadgeLevel.Platinum].MinimumGradedCourses);
      Assert.Null(options.Levels[PartnerBadgeLevel.Gold].MinimumGradedCourses);

      Assert.Equal(PartnerBadgeLevel.Gold, Partner(top: 85, graded: 100, options: options).Level);
      Assert.Equal(PartnerBadgeLevel.Platinum, Partner(top: 95, graded: 100, options: options).Level);
    }

    [Fact]
    public void Platinum_RemovedFromConfiguration_GoldIsTheTop()
    {
      var options = new PartnerBadgeOptions();
      options.Levels.Remove(PartnerBadgeLevel.Platinum);

      var badge = Partner(top: 90, graded: 100, options: options);

      Assert.Equal(PartnerBadgeLevel.Gold, badge.Level);
      Assert.Null(badge.NextLevelHint);
    }
    #endregion

    #region Review date
    [Theory]
    [InlineData("2026-01-01T00:00:00+00:00", "2026-04-01")]
    [InlineData("2026-03-31T23:59:59+00:00", "2026-04-01")]
    [InlineData("2026-04-01T00:00:00+00:00", "2026-07-01")]
    [InlineData("2026-06-30T12:00:00+00:00", "2026-07-01")]
    [InlineData("2026-07-01T00:00:00+00:00", "2026-10-01")]
    [InlineData("2026-09-30T23:59:59+00:00", "2026-10-01")]
    [InlineData("2026-10-01T00:00:00+00:00", "2027-01-01")]
    [InlineData("2026-11-27T09:00:00+00:00", "2027-01-01")]
    [InlineData("2026-12-31T23:59:59+00:00", "2027-01-01")]
    // read in UTC: 1 April 01:00 in Johannesburg is still 31 March in UTC; 31 December 20:00 in New York is already 1 January in UTC
    [InlineData("2026-04-01T01:00:00+02:00", "2026-04-01")]
    [InlineData("2026-12-31T20:00:00-05:00", "2027-04-01")]
    public void ReviewDue_IsTheFirstDayOfTheNextQuarter_InUtc(string now, string expected)
    {
      Assert.Equal(DateOnly.Parse(expected), PartnerBadgeCalculator.ReviewDue(DateTimeOffset.Parse(now)));
    }

    [Fact]
    public void ReviewDue_IsOnEveryBadge_WhateverTheLevel()
    {
      var expected = new DateOnly(2027, 1, 1);

      Assert.Equal(expected, Partner(top: 1, graded: 2).ReviewDue);
      Assert.Equal(expected, Partner(top: 0, graded: 20).ReviewDue);
      Assert.Equal(expected, Partner(top: 90, graded: 100).ReviewDue);
    }

    [Fact]
    public void ReviewDue_SerializesAsAnIsoDate()
    {
      var settings = new JsonSerializerSettings
      {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
        Converters = [new StringEnumConverter()]
      };

      var json = JObject.Parse(JsonConvert.SerializeObject(Partner(top: 90, graded: 100), settings));

      Assert.Equal("2027-01-01", json["reviewDue"]!.Value<string>());
      Assert.Equal("Platinum", json["level"]!.Value<string>());
    }
    #endregion

    #region Batched badges
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), Hidden = Guid.NewGuid();

    [Fact]
    public void Batch_ParsesIdsInOrder_EachOnce()
    {
      var ids = PathwayBadgeService.ParseOrganizationIds([$" {C}, {A},{C},,{B} "], 50);

      Assert.Equal([C, A, B], ids);
    }

    [Fact]
    public void Batch_AcceptsRepeatedKeys_AndCommaSeparatedValues_Mixed()
    {
      Assert.Equal([C, A, B], PathwayBadgeService.ParseOrganizationIds([C.ToString(), A.ToString(), B.ToString()], 50));
      Assert.Equal([C, A, B], PathwayBadgeService.ParseOrganizationIds([$"{C},{A}", B.ToString(), A.ToString()], 50));
      Assert.Equal([B], PathwayBadgeService.ParseOrganizationIds([null, "", B.ToString()], 50));
    }

    [Fact]
    public void Batch_QueryValues_RepeatedKeysAndCommaValues_BothReachTheService()
    {
      var parameter = typeof(PathwayBadgeController).GetMethod(nameof(PathwayBadgeController.ListPartnerBadges))!.GetParameters().Single();
      Assert.Equal(typeof(string[]), parameter.ParameterType);

      var query = new Microsoft.AspNetCore.Http.QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
      {
        ["ids"] = new([$"{C},{A}", B.ToString()])
      });
      var provider = new QueryStringValueProvider(BindingSource.Query, query, System.Globalization.CultureInfo.InvariantCulture);

      Assert.Equal([$"{C},{A}", B.ToString()], provider.GetValue("ids").Values.ToArray());
      Assert.Equal([C, A, B], PathwayBadgeService.ParseOrganizationIds(provider.GetValue("ids").Values, 50));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ,")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Batch_RejectsMissingOrMalformedIds(string? ids)
    {
      Assert.Throws<ValidationException>(() => PathwayBadgeService.ParseOrganizationIds([ids], 50));
      Assert.Throws<ValidationException>(() => PathwayBadgeService.ParseOrganizationIds(null, 50));
      Assert.Throws<ValidationException>(() => PathwayBadgeService.ParseOrganizationIds([], 50));
    }

    [Fact]
    public void Batch_AcceptsFifty_RejectsFiftyOne()
    {
      var fifty = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();

      var max = Options.MaxOrganizationsPerRequest;
      Assert.Equal(50, PathwayBadgeService.ParseOrganizationIds([string.Join(",", fifty)], max).Count);
      Assert.Equal(50, PathwayBadgeService.ParseOrganizationIds(fifty.Select(o => (string?)o.ToString()), max).Count);
      Assert.Throws<ValidationException>(() => PathwayBadgeService.ParseOrganizationIds([string.Join(",", fifty.Append(Guid.NewGuid()))], max));
      Assert.Throws<ValidationException>(() => PathwayBadgeService.ParseOrganizationIds(fifty.Append(Guid.NewGuid()).Select(o => (string?)o.ToString()), max));

      // duplicates do not count towards the limit
      Assert.Equal(50, PathwayBadgeService.ParseOrganizationIds([string.Join(",", fifty.Concat(fifty))], max).Count);
      Assert.Equal(50, Options.MaxOrganizationsPerRequest);
    }

    [Fact]
    public void Batch_KeepsTheRequestedOrder_LeavesOutOrganisationsThatAreNotPublic()
    {
      var names = new Dictionary<Guid, string> { [A] = "Alpha", [B] = "Beta", [C] = "Gamma" };
      var counts = new Dictionary<Guid, PartnerBadgeCounts>
      {
        [A] = new(TopGrades: 80, Graded: 100, HeldBack: 0, WaitingForReview: 0),
        [B] = new(TopGrades: 2, Graded: 3, HeldBack: 0, WaitingForReview: 7)
      };

      var badges = PathwayBadgeService.BuildPartnerBadges([B, Hidden, C, A, B], names, counts, Options, Now);

      Assert.Equal([B, C, A], badges.Select(o => o.OrganizationId));
      Assert.Equal(["Beta", "Gamma", "Alpha"], badges.Select(o => o.OrganizationName));
      Assert.Equal([PartnerBadgeLevel.NotEnoughGraded, PartnerBadgeLevel.NotEnoughGraded, PartnerBadgeLevel.Platinum], badges.Select(o => o.Level));
      Assert.Equal(0, badges[1].GradedCourses); // no courses at all: counted as nothing graded
      Assert.All(badges, o => Assert.Equal(new DateOnly(2027, 1, 1), o.ReviewDue));
    }

    [Fact]
    public void Batch_SameBadgeAsTheSingleEndpoint()
    {
      var counts = new PartnerBadgeCounts(TopGrades: 14, Graded: 20, HeldBack: 3, WaitingForReview: 2);

      var single = PartnerBadgeCalculator.Calculate(A, "Alpha", counts, Options, Now);
      var batched = PathwayBadgeService.BuildPartnerBadges([A], new Dictionary<Guid, string> { [A] = "Alpha" }, new Dictionary<Guid, PartnerBadgeCounts> { [A] = counts }, Options, Now).Single();

      Assert.Equal(JsonConvert.SerializeObject(single), JsonConvert.SerializeObject(batched));
    }

    [Fact]
    public void Batch_CountsEachOrganisationFromOneSetOfRows()
    {
      var c1 = Guid.NewGuid(); var c2 = Guid.NewGuid(); var c3 = Guid.NewGuid(); var c4 = Guid.NewGuid(); var c5 = Guid.NewGuid(); var c6 = Guid.NewGuid();
      PartnerCourseGradeRow Grade(Guid org, Guid course, CourseGrade grade) => new() { OrganizationId = org, OpportunityId = course, Grade = grade };
      PartnerCourseAssessmentRow Latest(Guid org, Guid course, CourseQualityStatus status) => new() { OrganizationId = org, OpportunityId = course, Status = status };

      var counts = PathwayBadgeService.CountByOrganization(
        [Grade(A, c1, CourseGrade.A), Grade(A, c2, CourseGrade.C), Grade(A, c3, CourseGrade.Ungraded), Grade(B, c4, CourseGrade.B)],
        [
          Latest(A, c1, CourseQualityStatus.Verified),
          Latest(A, c3, CourseQualityStatus.NeedsReview),
          Latest(A, c5, CourseQualityStatus.NotVerified),
          Latest(B, c4, CourseQualityStatus.NotVerified), // graded, so not held back
          Latest(C, c6, CourseQualityStatus.NeedsReview)
        ]);

      Assert.Equal(new PartnerBadgeCounts(TopGrades: 1, Graded: 2, HeldBack: 1, WaitingForReview: 1), counts[A]);
      Assert.Equal(new PartnerBadgeCounts(TopGrades: 1, Graded: 1, HeldBack: 0, WaitingForReview: 0), counts[B]);
      Assert.Equal(new PartnerBadgeCounts(TopGrades: 0, Graded: 0, HeldBack: 0, WaitingForReview: 1), counts[C]);
      Assert.False(counts.ContainsKey(Hidden));
    }

    [Fact]
    public void Batch_Route_IsAnonymous_AndTakesIdsFromTheQuery()
    {
      var method = typeof(PathwayBadgeController).GetMethod(nameof(PathwayBadgeController.ListPartnerBadges))!;

      Assert.NotNull(method.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
      Assert.Equal("ids", method.GetParameters().Single().Name);
      Assert.NotNull(method.GetParameters().Single().GetCustomAttribute<FromQueryAttribute>());
    }
    #endregion
  }
}
