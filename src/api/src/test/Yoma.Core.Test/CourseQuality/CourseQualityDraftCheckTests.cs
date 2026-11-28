using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using System.Security.Claims;
using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces.Lookups;
using Yoma.Core.Domain.Entity.Models;
using Yoma.Core.Domain.Lookups.Interfaces;
using Yoma.Core.Domain.Lookups.Models;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity.Models;
using OpportunityModel = Yoma.Core.Domain.Opportunity.Models.Opportunity;
using OpportunityType = Yoma.Core.Domain.Opportunity.Type;
using OpportunityTypeLookup = Yoma.Core.Domain.Opportunity.Models.Lookups.OpportunityType;

namespace Yoma.Core.Test.CourseQuality
{
  /// <summary>
  /// Iteration 4, W1 and W4: the pre-publish check on a draft, its equivalence with the check after publishing, and Impact
  /// Action listing checks.
  /// </summary>
  public class CourseQualityDraftCheckTests
  {
    #region Harness
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid WeekId = Guid.NewGuid();
    private static readonly Guid OnSiteId = Guid.NewGuid();
    private static readonly Dictionary<OpportunityType, Guid> TypeIds = Enum.GetValues<OpportunityType>().ToDictionary(o => o, _ => Guid.NewGuid());

    private const string CourseUrl = "https://courses.example.org/data";
    private const string PageText = "Data Analysis Course. Learn to clean, explore and present data. Self-paced, start anytime.";

    private readonly Mock<ICourseQualityLinkChecker> _links = new();
    private readonly Mock<ICourseQualityAIProvider> _ai = new();
    private readonly Mock<IOrganizationService> _organizations = new();
    private readonly Organization _organization = new() { Id = OrganizationId, Name = "Open Learning Org", Status = OrganizationStatus.Active };
    private readonly CourseQualityOptions _options = new()
    {
      Enabled = true,
      // UseAI on, so the provider each test passes is the one the draft calls; the default (off) is covered in "AI in the draft check"
      PrePublish = new CourseQualityPrePublishOptions { Enabled = true, LinkTimeoutMilliseconds = 2000, UseAI = true },
      VerifyTypes = [OpportunityType.Learning, OpportunityType.Event, OpportunityType.ImpactAction],
      Jobs = new CourseQualityJobOptions { Enabled = true }
    };

    public CourseQualityDraftCheckTests()
    {
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync((string? url, CancellationToken _) =>
        string.IsNullOrWhiteSpace(url) ? CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.Missing) : CourseQualityLinkCheckResult.Reachable(200, url, PageText));
      _ai.SetupGet(o => o.IsEnabled).Returns(false);
      _organizations.Setup(o => o.IsAdmin(OrganizationId, true)).Returns(true);
      _organizations.Setup(o => o.GetById(OrganizationId, false, false, false, null)).Returns(_organization);
    }

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private CourseQualityDraftService Draft(ICourseQualityAIProvider? ai = null, params ICourseQualityDraftSuggester[] suggesters)
    {
      var types = new Mock<IOpportunityTypeService>();
      foreach (var (type, id) in TypeIds)
        types.Setup(o => o.GetByIdOrNull(id)).Returns(new OpportunityTypeLookup { Id = id, Name = type.ToString(), DisplayName = type.ToString() });

      var intervals = new Mock<ITimeIntervalService>();
      intervals.Setup(o => o.GetByIdOrNull(WeekId)).Returns(new TimeInterval { Id = WeekId, Name = "Week" });

      var engagement = new Mock<IEngagementTypeService>();
      engagement.Setup(o => o.GetByIdOrNull(OnSiteId)).Returns(new EngagementType { Id = OnSiteId, Name = "OnSite", DisplayName = "On-site" });

      return new CourseQualityDraftService(NullLogger<CourseQualityDraftService>.Instance, Microsoft.Extensions.Options.Options.Create(_options), _organizations.Object,
        types.Object, intervals.Object, engagement.Object, _links.Object, ai ?? _ai.Object, suggesters)
      {
        Cache = new CourseQualityDraftCache()
      };
    }

    private static CourseQualityDraftRequest Request(OpportunityType type = OpportunityType.Learning) => new()
    {
      Title = type switch
      {
        OpportunityType.Job => "Junior Data Analyst",
        OpportunityType.Event => "Youth Climate Summit",
        OpportunityType.ImpactAction => "Plant a School Garden",
        _ => "Data Analysis Course"
      },
      Description = type == OpportunityType.Job
        ? "Full-time role based in Cape Town. Salary R12 000 per month. Requirements: matric and Excel. Apply on our careers page."
        : "Learn to clean, explore and present data through short practical exercises.",
      TypeId = TypeIds[type],
      URL = CourseUrl,
      DateStart = Now.AddDays(-5),
      DateEnd = Now.AddDays(30),
      CommitmentIntervalId = WeekId,
      CommitmentIntervalCount = 4,
      Countries = [new OpportunityRequestCountry { CountryId = Guid.NewGuid() }],
      Languages = [Guid.NewGuid()],
      Skills = [Guid.NewGuid()]
    };

    /// <summary>
    /// The same listing as the database would load it once published.
    /// </summary>
    private static OpportunityModel Published(CourseQualityDraftRequest request, OpportunityType type) => new()
    {
      Id = Guid.NewGuid(),
      Title = request.Title ?? string.Empty,
      Description = request.Description ?? string.Empty,
      Type = type,
      TypeId = TypeIds[type],
      OrganizationId = OrganizationId,
      OrganizationName = "Open Learning Org",
      OrganizationStatus = OrganizationStatus.Active,
      Status = Status.Active,
      Published = true,
      URL = request.URL,
      DateStart = request.DateStart ?? default,
      DateEnd = request.DateEnd,
      DateModified = Now.AddDays(-1),
      CommitmentInterval = request.CommitmentIntervalId == WeekId ? TimeIntervalOption.Week : null,
      CommitmentIntervalCount = request.CommitmentIntervalCount,
      Countries = request.Countries?.Select(o => new OpportunityCountryInfo { Id = o.CountryId }).ToList(),
      Languages = request.Languages?.Select(o => new Domain.Lookups.Models.Language { Id = o, Name = "English" }).ToList(),
      Skills = request.Skills?.Select(o => new Skill { Id = o, Name = "Data analysis" }).ToList()
    };

    /// <summary>
    /// The check after publishing, through the real service with the same link checker and page reader.
    /// </summary>
    private async Task<CourseQualityAssessment> AssessPublished(OpportunityModel opportunity, ICourseQualityAIProvider? ai = null)
    {
      var records = new List<CourseQualityAssessmentRecord>();
      var opportunities = new Mock<IOpportunityService>();
      opportunities.Setup(o => o.GetById(opportunity.Id, true, false, false, null)).Returns(opportunity);
      opportunities.Setup(o => o.GetById(opportunity.Id, true, true, false, null)).Returns(opportunity);

      var repository = new Mock<IRepository<CourseQualityAssessmentRecord>>();
      repository.Setup(o => o.Query()).Returns(() => records.AsQueryable());
      repository.Setup(o => o.Query(LockMode.Wait)).Returns(() => records.AsQueryable());
      repository.Setup(o => o.Create(It.IsAny<CourseQualityAssessmentRecord>())).Returns((CourseQualityAssessmentRecord r) => { records.Add(r); return Task.FromResult(r); });
      repository.Setup(o => o.Update(It.IsAny<CourseQualityAssessmentRecord>())).Returns((CourseQualityAssessmentRecord r) => Task.FromResult(r));

      var strategy = new Mock<IExecutionStrategyService>();
      strategy.Setup(o => o.ExecuteInExecutionStrategyAsync(It.IsAny<Func<Task>>(), It.IsAny<bool>())).Returns((Func<Task> body, bool _) => body());

      var accessor = new Mock<IHttpContextAccessor>();
      accessor.SetupGet(o => o.HttpContext).Returns(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "a@yoma.world")], "test")) });

      var grading = new Mock<ICourseQualityGradingService>();
      grading.Setup(o => o.ListGrades(It.IsAny<List<Guid>>())).Returns([]);
      var feedback = new Mock<ICourseQualityFeedbackService>();
      feedback.Setup(o => o.ListSummaries(It.IsAny<Dictionary<Guid, Guid>>())).Returns([]);

      var service = new CourseQualityService(NullLogger<CourseQualityService>.Instance, accessor.Object, Microsoft.Extensions.Options.Options.Create(_options), opportunities.Object,
        new Mock<IOrganizationService>().Object, new Mock<IOpportunityTypeService>().Object, new Mock<IOpportunityStatusService>().Object,
        new Mock<IOrganizationStatusService>().Object, new Mock<IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<OpportunityModel>>().Object,
        _links.Object, ai ?? _ai.Object, repository.Object, strategy.Object, grading.Object, feedback.Object);

      return await service.Assess(opportunity.Id, CancellationToken.None);
    }

    private static CourseQualitySourcePageReader Reader() => new(Microsoft.Extensions.Options.Options.Create(new CourseQualityOptions()));

    private sealed class Suggester(bool enabled, List<DraftCheckSuggestion>? suggestions) : ICourseQualityDraftSuggester
    {
      public int Calls { get; private set; }
      public bool IsEnabled => enabled;

      public Task<List<DraftCheckSuggestion>?> Suggest(CourseQualityDraftSuggestionRequest request, CancellationToken cancellationToken)
      {
        Calls++;
        return Task.FromResult(suggestions);
      }
    }
    #endregion

    #region Switch and authorization
    [Fact]
    public async Task Off_IsNotFound_BeforeAnythingElse()
    {
      _options.PrePublish.Enabled = false;

      await Assert.ThrowsAsync<EntityNotFoundException>(() => Draft().Check(OrganizationId, Request(), CancellationToken.None));

      _organizations.Verify(o => o.IsAdmin(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
      _links.Verify(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Off_ByDefault()
    {
      Assert.False(new CourseQualityOptions().PrePublish.Enabled);
      Assert.True(new CourseQualityOptions().PrePublish.LinkTimeoutMilliseconds > 0);
    }

    [Fact]
    public void Switch_BindsFromConfiguration()
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["CourseQuality:PrePublish:Enabled"] = "true",
        ["CourseQuality:PrePublish:LinkTimeoutMilliseconds"] = "1500"
      }).Build();

      var options = new CourseQualityOptions();
      configuration.GetSection(CourseQualityOptions.Section).Bind(options);

      Assert.True(options.PrePublish.Enabled);
      Assert.Equal(1500, options.PrePublish.LinkTimeoutMilliseconds);
    }

    [Fact]
    public async Task NotAnAdminOfTheOrganisation_IsRefused_BeforeTheLinkIsFetched()
    {
      _organizations.Setup(o => o.IsAdmin(OrganizationId, true)).Throws(new SecurityException("Unauthorized"));

      await Assert.ThrowsAsync<SecurityException>(() => Draft().Check(OrganizationId, Request(), CancellationToken.None));

      _links.Verify(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheRouteOrganisation_IsChecked_NotTheBodyOne()
    {
      var request = Request();
      request.OrganizationId = Guid.NewGuid();

      await Draft().Check(OrganizationId, request, CancellationToken.None);

      _organizations.Verify(o => o.IsAdmin(OrganizationId, true), Times.Once);
      _organizations.Verify(o => o.IsAdmin(request.OrganizationId.Value, It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void StoresNothing_NoRepositoryIsInjected()
    {
      var parameters = typeof(CourseQualityDraftService).GetConstructors().Single().GetParameters();

      Assert.DoesNotContain(parameters, o => o.ParameterType.IsGenericType && o.ParameterType.Name.StartsWith("IRepository", StringComparison.Ordinal));
      Assert.DoesNotContain(parameters, o => o.ParameterType == typeof(ICourseQualityService) || o.ParameterType == typeof(IExecutionStrategyService));
    }
    #endregion

    #region Types
    [Fact]
    public async Task NoType_AsksForOne()
    {
      var ex = await Assert.ThrowsAsync<ValidationException>(() => Draft().Check(OrganizationId, new CourseQualityDraftRequest { Title = "Data" }, CancellationToken.None));
      Assert.Equal("Choose the opportunity type to check this listing", ex.Message);
    }

    [Fact]
    public async Task ATypeNotCheckedAfterPublishing_IsNotCheckedBefore()
    {
      _options.VerifyTypes = [OpportunityType.Learning];
      _options.Jobs.Enabled = false;

      var ex = await Assert.ThrowsAsync<ValidationException>(() => Draft().Check(OrganizationId, Request(OpportunityType.Event), CancellationToken.None));
      Assert.Equal("Only opportunities of type 'Learning' are checked", ex.Message);
      await Assert.ThrowsAsync<ValidationException>(() => Draft().Check(OrganizationId, Request(OpportunityType.Job), CancellationToken.None));
      await Assert.ThrowsAsync<ValidationException>(() => Draft().Check(OrganizationId, Request(OpportunityType.Other), CancellationToken.None));
    }

    [Fact]
    public async Task AnEmptyDraft_IsChecked_NotRejected()
    {
      var result = await Draft().Check(OrganizationId, new CourseQualityDraftRequest { TypeId = TypeIds[OpportunityType.Learning] }, CancellationToken.None);

      Assert.Equal(CourseQualityStatus.NotVerified, result.PredictedStatus);
      Assert.False(result.LinkChecked);
      Assert.Contains(result.Fixes, o => o.Criterion == CourseQualityCriterion.LinkWorks && o.Field == DraftCheckField.Url && o.HardGate);
      Assert.Contains(result.Fixes, o => o.Criterion == CourseQualityCriterion.DatesAccurate && o.Field == DraftCheckField.DateStart);
      Assert.Contains(result.Fixes, o => o.Criterion == CourseQualityCriterion.DataComplete && o.Field == DraftCheckField.DateStart);
      Assert.Contains(result.Fixes, o => o.Criterion == CourseQualityCriterion.FormatIdentified && o.Field == DraftCheckField.Title);
    }
    #endregion

    #region Equivalence
    public static TheoryData<OpportunityType, string> Scenarios()
    {
      var data = new TheoryData<OpportunityType, string>();
      foreach (var type in new[] { OpportunityType.Learning, OpportunityType.Event, OpportunityType.Job, OpportunityType.ImpactAction })
        foreach (var scenario in new[] { "clean", "brokenLink", "slowLink", "noCommitment", "ended", "placeholderStart", "noLink", "unknownFormat", "scam" })
          data.Add(type, scenario);
      return data;
    }

    private CourseQualityDraftRequest Scenario(OpportunityType type, string scenario)
    {
      var request = Request(type);
      switch (scenario)
      {
        case "brokenLink":
          _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, CourseUrl, 404));
          break;
        case "slowLink":
          _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.ServerError, CourseUrl, 503));
          break;
        case "noCommitment":
          request.CommitmentIntervalId = null;
          request.CommitmentIntervalCount = null;
          break;
        case "ended":
          request.DateStart = Now.AddDays(-60);
          request.DateEnd = Now.AddDays(-2);
          break;
        case "placeholderStart":
          request.DateStart = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
          break;
        case "noLink":
          request.URL = null;
          break;
        case "unknownFormat":
          request.Title = "Data Analysis";
          request.DateEnd = null;
          break;
        case "scam":
          request.Description += " Pay an application fee of R500 to secure your place.";
          break;
      }
      return request;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task DraftAndPublished_AgreeOnStatusAndScore_RulesOnly(OpportunityType type, string scenario)
    {
      var request = Scenario(type, scenario);

      var draft = await Draft().Check(OrganizationId, request, CancellationToken.None);
      var published = await AssessPublished(Published(request, type));

      Assert.Equal(published.Status, draft.PredictedStatus);
      Assert.Equal(published.Score, draft.Score);
      Assert.Equal(published.Criteria.Select(o => (o.Criterion, o.Score)), draft.Checks.Select(o => (o.Criterion, o.Score)));
      Assert.Equal(type, draft.OpportunityType);
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task DraftAndPublished_AgreeOnStatusAndScore_WithThePageReader(OpportunityType type, string scenario)
    {
      var request = Scenario(type, scenario);

      var draft = await Draft(Reader()).Check(OrganizationId, request, CancellationToken.None);
      var published = await AssessPublished(Published(request, type), Reader());

      Assert.Equal(published.Status, draft.PredictedStatus);
      Assert.Equal(published.Score, draft.Score);
      Assert.Equal(published.Criteria.Select(o => (o.Criterion, o.Score)), draft.Checks.Select(o => (o.Criterion, o.Score)));
    }

    [Theory]
    [InlineData(OpportunityType.Learning)]
    [InlineData(OpportunityType.Event)]
    [InlineData(OpportunityType.ImpactAction)]
    public async Task DraftAndPublished_AgreeOnAVerifiedListing_WithAConfirmingPage(OpportunityType type)
    {
      var ai = new Mock<ICourseQualityAIProvider>();
      ai.SetupGet(o => o.IsEnabled).Returns(true);
      ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityTestData.Ai());
      var request = Request(type);

      var draft = await Draft(ai.Object).Check(OrganizationId, request, CancellationToken.None);
      var published = await AssessPublished(Published(request, type), ai.Object);

      Assert.Equal(CourseQualityStatus.Verified, published.Status);
      Assert.Equal(published.Status, draft.PredictedStatus);
      Assert.Equal(published.Score, draft.Score);
      Assert.Empty(draft.Fixes);
      Assert.True(draft.LinkChecked);
      Assert.Equal($"This {CourseQualityRules.Noun(type)} will be verified.", draft.Summary);
    }

    [Fact]
    public async Task DraftAndPublished_AgreeOnAJob_WithItsCustomFields()
    {
      var request = Request(OpportunityType.Job);
      request.Description = "We are hiring.";
      request.CustomFields =
      [
        new CustomFieldValueRequest { Key = CustomFieldConstants.Job.Salary.Minimum, Value = "12000" },
        new CustomFieldValueRequest { Key = CustomFieldConstants.Job.Employment.Type, Values = ["FullTime"] }
      ];

      var draft = await Draft().Check(OrganizationId, request, CancellationToken.None);

      var stored = Published(request, OpportunityType.Job);
      stored.CustomFields =
      [
        new CustomFieldValueItem { Key = CustomFieldConstants.Job.Salary.Minimum, DataType = CustomFieldDataType.Decimal, ValueRaw = "12000" },
        new CustomFieldValueItem { Key = CustomFieldConstants.Job.Employment.Type, DataType = CustomFieldDataType.Option, ValueRaw = "FullTime" }
      ];
      var published = await AssessPublished(stored);

      Assert.Equal(published.Status, draft.PredictedStatus);
      Assert.Equal(published.Score, draft.Score);
      Assert.Equal(published.Criteria.Select(o => (o.Criterion, o.Score)), draft.Checks.Select(o => (o.Criterion, o.Score)));
    }
    #endregion

    #region Fixes and wording
    [Fact]
    public async Task TwoFixes_SaySo_AndPointAtTheirFields()
    {
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, CourseUrl, 404));
      var request = Request();
      request.CommitmentIntervalId = null;
      request.CommitmentIntervalCount = null;

      var result = await Draft().Check(OrganizationId, request, CancellationToken.None);

      Assert.Equal(CourseQualityStatus.NotVerified, result.PredictedStatus);
      Assert.Equal("Two fixes and this course will be verified.", result.Summary);
      Assert.Equal([(CourseQualityCriterion.LinkWorks, DraftCheckField.Url, true), (CourseQualityCriterion.DataComplete, DraftCheckField.Commitment, false)],
        result.Fixes.Select(o => (o.Criterion, o.Field, o.HardGate)));
      Assert.Equal("The course link returned page not found (404).", result.Fixes[0].Reason);
      Assert.Equal("Update the listing with a working public link to the course.", result.Fixes[0].Recommendation);
      Assert.True(result.LinkChecked);
      Assert.Equal(result.Checks.Count(o => !o.Passed), result.Fixes.Count);
    }

    [Fact]
    public async Task EndedListing_PointsAtTheEndDate()
    {
      var request = Request();
      request.DateStart = Now.AddDays(-60);
      request.DateEnd = Now.AddDays(-1);

      var result = await Draft().Check(OrganizationId, request, CancellationToken.None);

      var live = result.Fixes.Single(o => o.Criterion == CourseQualityCriterion.ListingLive);
      Assert.Equal(DraftCheckField.DateEnd, live.Field);
      Assert.True(live.HardGate);
    }

    [Fact]
    public async Task EndBeforeStart_PointsAtTheEndDate()
    {
      var request = Request();
      request.DateEnd = request.DateStart!.Value.AddDays(-1);

      var result = await Draft().Check(OrganizationId, request, CancellationToken.None);

      Assert.Equal(DraftCheckField.DateEnd, result.Fixes.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Field);
    }

    [Fact]
    public async Task Job_FixesPointAtTheDescriptionAndCountries()
    {
      var request = Request(OpportunityType.Job);
      request.Description = "Join us. Pay an application fee to be considered.";
      request.Countries = [];

      var result = await Draft().Check(OrganizationId, request, CancellationToken.None);

      Assert.Equal(DraftCheckField.Description, result.Fixes.Single(o => o.Criterion == CourseQualityCriterion.JobDetailsClear).Field);
      var scam = result.Fixes.Single(o => o.Criterion == CourseQualityCriterion.NoScamSignals);
      Assert.Equal(DraftCheckField.Description, scam.Field);
      Assert.True(scam.HardGate);
      Assert.Equal(CourseQualityStatus.NeedsReview, result.PredictedStatus);
      Assert.Contains("job", result.Summary);
    }

    [Fact]
    public async Task HiddenListing_PointsAtOther()
    {
      var request = Request(OpportunityType.Event);
      request.Hidden = true;

      var result = await Draft().Check(OrganizationId, request, CancellationToken.None);

      Assert.Equal(DraftCheckField.Other, result.Fixes.Single(o => o.Criterion == CourseQualityCriterion.ListingLive).Field);
    }

    [Theory]
    [InlineData(CourseQualityStatus.Verified, OpportunityType.Learning, 0, true, "This course will be verified.")]
    [InlineData(CourseQualityStatus.Verified, OpportunityType.Job, 1, true, "This job will be verified, and one fix would make it stronger.")]
    [InlineData(CourseQualityStatus.NotVerified, OpportunityType.Learning, 2, true, "Two fixes and this course will be verified.")]
    [InlineData(CourseQualityStatus.NotVerified, OpportunityType.ImpactAction, 1, true, "One fix and this impact action will be verified.")]
    [InlineData(CourseQualityStatus.NotVerified, OpportunityType.Event, 12, true, "12 fixes and this event will be verified.")]
    [InlineData(CourseQualityStatus.NeedsReview, OpportunityType.Event, 0, true, "A reviewer will look at this event after you publish.")]
    [InlineData(CourseQualityStatus.NeedsReview, OpportunityType.Learning, 3, true, "A reviewer will look at this course after you publish, and three fixes would help.")]
    [InlineData(CourseQualityStatus.NeedsReview, OpportunityType.Learning, 1, false, "The link could not be opened just now, so a reviewer will look at this course after you publish.")]
    public void Summary_IsOnePlainSentence(CourseQualityStatus status, OpportunityType type, int fixes, bool linkChecked, string expected)
    {
      Assert.Equal(expected, CourseQualityDraftWording.Summary(status, type, fixes, linkChecked));
    }

    [Fact]
    public void EveryFixField_IsInTheWebContract()
    {
      var listing = CourseQualityTestData.CleanListing();
      foreach (var criterion in Enum.GetValues<CourseQualityCriterion>())
        Assert.Contains(CourseQualityDraftWording.Field(new CourseQualityCriterionResult { Criterion = criterion, Reason = "r" }, listing, CourseFormat.Unknown, _options, Now), DraftCheckField.All);
    }
    #endregion

    #region Link check while typing
    [Fact]
    public async Task SlowLink_IsNotChecked_AndNeverAHardFail()
    {
      _options.PrePublish.LinkTimeoutMilliseconds = 50;
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(async (string? _, CancellationToken token) =>
      {
        await Task.Delay(Timeout.Infinite, token);
        return CourseQualityLinkCheckResult.Reachable(200, CourseUrl, PageText);
      });

      var result = await Draft().Check(OrganizationId, Request(), CancellationToken.None);

      Assert.False(result.LinkChecked);
      var link = result.Fixes.Single(o => o.Criterion == CourseQualityCriterion.LinkWorks);
      Assert.False(link.HardGate);
      Assert.Equal(DraftCheckField.Url, link.Field);
      Assert.NotEqual(CourseQualityStatus.NotVerified, result.PredictedStatus);
      Assert.Equal("The link could not be opened just now, so a reviewer will look at this course after you publish.", result.Summary);
    }

    [Fact]
    public async Task ACancelledRequest_StillThrows()
    {
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(async (string? _, CancellationToken token) =>
      {
        await Task.Delay(Timeout.Infinite, token);
        return CourseQualityLinkCheckResult.Reachable(200, CourseUrl, PageText);
      });
      using var cancelled = new CancellationTokenSource();
      await cancelled.CancelAsync();

      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Draft().Check(OrganizationId, Request(), cancelled.Token));
    }

    [Fact]
    public async Task UnconfirmedLink_IsNotChecked_ButABrokenOneIs()
    {
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.AccessBlocked, CourseUrl, 403));
      Assert.False((await Draft().Check(OrganizationId, Request(), CancellationToken.None)).LinkChecked);

      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.DomainNotFound, CourseUrl));
      Assert.True((await Draft().Check(OrganizationId, Request(), CancellationToken.None)).LinkChecked);
    }

    [Fact]
    public async Task RepeatedChecks_FetchTheLinkAndReadThePageOnce()
    {
      var ai = new Mock<ICourseQualityAIProvider>();
      ai.SetupGet(o => o.IsEnabled).Returns(true);
      ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityTestData.Ai(0.5m));
      var service = Draft(ai.Object);
      var request = Request();

      var first = await service.Check(OrganizationId, request, CancellationToken.None);
      var second = await service.Check(OrganizationId, request, CancellationToken.None);

      Assert.Equal(first.Score, second.Score);
      _links.Verify(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
      ai.Verify(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>()), Times.Once);

      // an edit is read again, but the link is not fetched again
      request.Title = "Data Analysis Course for Beginners";
      await service.Check(OrganizationId, request, CancellationToken.None);

      _links.Verify(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
      ai.Verify(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    #region AI in the draft check
    [Fact]
    public void UseAI_IsOffByDefault()
    {
      Assert.False(new CourseQualityPrePublishOptions().UseAI);
    }

    [Fact]
    public async Task UseAIOff_ReadsThePageWithTheKeylessReader_AndNeverCallsTheModel()
    {
      _options.PrePublish.UseAI = false;
      var ai = new Mock<ICourseQualityAIProvider>();
      ai.SetupGet(o => o.IsEnabled).Returns(true);
      ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityTestData.Ai());

      var withModel = await Draft(ai.Object).Check(OrganizationId, Request(), CancellationToken.None);
      var withReader = await Draft(Reader()).Check(OrganizationId, Request(), CancellationToken.None);

      ai.Verify(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>()), Times.Never);
      Assert.Equal(withReader.PredictedStatus, withModel.PredictedStatus);
      Assert.Equal(withReader.Score, withModel.Score);
      Assert.Equal(withReader.Checks.Select(o => (o.Criterion, o.Score)), withModel.Checks.Select(o => (o.Criterion, o.Score)));
    }

    [Fact]
    public async Task UseAIOn_CallsTheConfiguredProvider()
    {
      _options.PrePublish.UseAI = true;
      var ai = new Mock<ICourseQualityAIProvider>();
      ai.SetupGet(o => o.IsEnabled).Returns(true);
      ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityTestData.Ai());

      var result = await Draft(ai.Object).Check(OrganizationId, Request(), CancellationToken.None);

      ai.Verify(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>()), Times.Once);
      Assert.Equal(CourseQualityStatus.Verified, result.PredictedStatus);
    }

    #endregion

    [Fact]
    public async Task ATimedOutLink_IsCachedBriefly()
    {
      _options.PrePublish.LinkTimeoutMilliseconds = 20;
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(async (string? _, CancellationToken token) =>
      {
        await Task.Delay(Timeout.Infinite, token);
        return CourseQualityLinkCheckResult.Reachable(200, CourseUrl, PageText);
      });
      var service = Draft();

      await service.Check(OrganizationId, Request(), CancellationToken.None);
      await service.Check(OrganizationId, Request(), CancellationToken.None);

      _links.Verify(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
      Assert.True(CourseQualityDraftService.TimedOutLinkCacheDuration < CourseQualityDraftService.LinkCacheDuration);
    }

    [Fact]
    public void Cache_ExpiresAndStaysBounded()
    {
      var cache = new CourseQualityDraftCache();
      var now = Now;

      cache.Set("a", "value", now.AddMinutes(1), now);
      Assert.True(cache.TryGet<string>("a", now, out var value));
      Assert.Equal("value", value);
      Assert.False(cache.TryGet<string>("a", now.AddMinutes(2), out _));

      for (var i = 0; i < CourseQualityDraftCache.MaxEntries + 10; i++) cache.Set($"k{i}", "v", now.AddMinutes(1), now);
      Assert.True(cache.Count <= CourseQualityDraftCache.MaxEntries);
    }
    #endregion

    #region Suggestions
    [Fact]
    public async Task NoSuggester_GivesNoSuggestions()
    {
      Assert.Null((await Draft().Check(OrganizationId, Request(), CancellationToken.None)).Suggestions);
      Assert.Null((await Draft(null, new Suggester(false, [new() { Field = DraftCheckField.Summary, Value = "x" }])).Check(OrganizationId, Request(), CancellationToken.None)).Suggestions);
    }

    [Fact]
    public async Task ASuggester_FillsOnlyTheFieldsItMay()
    {
      var suggester = new Suggester(true,
      [
        new() { Field = DraftCheckField.Summary, Value = "  A short practical data course.  " },
        new() { Field = DraftCheckField.Summary, Value = "A second summary" },
        new() { Field = DraftCheckField.Title, Value = "Ignore the title" },
        new() { Field = DraftCheckField.Skills, Value = "Data analysis, Excel" },
        new() { Field = DraftCheckField.DateStart, Value = " " }
      ]);
      var service = Draft(null, suggester);
      var request = Request();

      var result = await service.Check(OrganizationId, request, CancellationToken.None);
      await service.Check(OrganizationId, request, CancellationToken.None);

      Assert.Equal([(DraftCheckField.Summary, "A short practical data course."), (DraftCheckField.Skills, "Data analysis, Excel")],
        result.Suggestions!.Select(o => (o.Field, o.Value)));
      Assert.Equal(1, suggester.Calls);
    }

    [Fact]
    public async Task ASuggester_WithNoPage_GivesAnEmptyList()
    {
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, CourseUrl, 404));
      var suggester = new Suggester(true, [new() { Field = DraftCheckField.Summary, Value = "x" }]);

      var result = await Draft(null, suggester).Check(OrganizationId, Request(), CancellationToken.None);

      Assert.NotNull(result.Suggestions);
      Assert.Empty(result.Suggestions);
      Assert.Equal(0, suggester.Calls);
    }
    #endregion

    #region Contract
    [Fact]
    public async Task Result_SerializesLikeTheWebModel()
    {
      var settings = new Newtonsoft.Json.JsonSerializerSettings
      {
        ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver { NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy() },
        Converters = [new Newtonsoft.Json.Converters.StringEnumConverter()]
      };
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, CourseUrl, 404));
      var result = await Draft().Check(OrganizationId, Request(OpportunityType.ImpactAction), CancellationToken.None);

      var json = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(result, settings));

      Assert.Equal(["checks", "fixes", "linkChecked", "opportunityType", "predictedStatus", "score", "suggestions", "summary"], json.Properties().Select(o => o.Name).Order());
      Assert.Equal("ImpactAction", json["opportunityType"]!.Value<string>());
      Assert.Equal("NotVerified", json["predictedStatus"]!.Value<string>());
      Assert.Equal(["criterion", "passed", "score"], ((Newtonsoft.Json.Linq.JObject)json["checks"]![0]!).Properties().Select(o => o.Name).Order());
      var fix = (Newtonsoft.Json.Linq.JObject)json["fixes"]![0]!;
      Assert.Equal(["criterion", "field", "hardGate", "reason", "recommendation"], fix.Properties().Select(o => o.Name).Order());
      Assert.Equal("LinkWorks", fix["criterion"]!.Value<string>());
      Assert.Equal("url", fix["field"]!.Value<string>());
      Assert.Equal(Newtonsoft.Json.Linq.JTokenType.Null, json["suggestions"]!.Type);
    }

    [Fact]
    public void Request_TakesTheFormBody_WithFieldsMissing()
    {
      var body = $$"""{ "title": "Data", "typeId": "{{TypeIds[OpportunityType.Learning]}}", "postAsActive": true, "zltoReward": 5, "countries": [{ "countryId": "{{Guid.NewGuid()}}" }] }""";

      var request = Newtonsoft.Json.JsonConvert.DeserializeObject<CourseQualityDraftRequest>(body)!;

      Assert.Equal("Data", request.Title);
      Assert.Null(request.DateStart);
      Assert.Single(request.Countries!);
    }

    [Fact]
    public void Resolves_WithNoSuggesterRegistered()
    {
      var services = new ServiceCollection();
      services.AddLogging();
      services.AddSingleton(Microsoft.Extensions.Options.Options.Create(_options));
      services.AddSingleton(_organizations.Object);
      services.AddSingleton(Mock.Of<IOpportunityTypeService>());
      services.AddSingleton(Mock.Of<ITimeIntervalService>());
      services.AddSingleton(Mock.Of<IEngagementTypeService>());
      services.AddSingleton(_links.Object);
      services.AddSingleton(_ai.Object);
      services.AddScoped<ICourseQualityDraftService, CourseQualityDraftService>();

      using var provider = services.BuildServiceProvider();
      using var scope = provider.CreateScope();

      Assert.IsType<CourseQualityDraftService>(scope.ServiceProvider.GetRequiredService<ICourseQualityDraftService>());
    }
    #endregion

    #region Impact actions (W4)
    [Fact]
    public void VerifyTypes_AcceptsImpactAction()
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["CourseQuality:VerifyTypes:0"] = "ImpactAction"
      }).Build();

      var options = new CourseQualityOptions();
      configuration.GetSection(CourseQualityOptions.Section).Bind(options);

      Assert.Equal([OpportunityType.Learning, OpportunityType.ImpactAction], options.VerifiedTypes());
      Assert.Equal([OpportunityType.Learning], new CourseQualityOptions().VerifiedTypes());
    }

    [Fact]
    public async Task ImpactAction_GetsTheListingChecks_InItsOwnWords_AndNoGrade()
    {
      var listing = Published(Request(OpportunityType.ImpactAction), OpportunityType.ImpactAction);
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, CourseUrl, 404));

      var assessment = await AssessPublished(listing);

      Assert.Equal([CourseQualityCriterion.ListingLive, CourseQualityCriterion.LinkWorks, CourseQualityCriterion.DatesAccurate, CourseQualityCriterion.DataComplete],
        assessment.Criteria.Select(o => o.Criterion));
      Assert.Equal("The impact action link returned page not found (404).", assessment.Criteria.Single(o => o.Criterion == CourseQualityCriterion.LinkWorks).Reason);
      Assert.Equal(CourseQualityStatus.NotVerified, assessment.Status);
      Assert.Equal(CourseFormat.Unknown, assessment.LearnerCard.Format);
      Assert.True(CourseQualityEngine.IsListingOnly(assessment));
      Assert.StartsWith("This listing is not verified yet", assessment.FixNote!.Summary);
    }

    [Fact]
    public async Task ImpactAction_IsRefused_UntilConfigured()
    {
      _options.VerifyTypes = [OpportunityType.Learning, OpportunityType.Event];

      await Assert.ThrowsAsync<ValidationException>(() => AssessPublished(Published(Request(OpportunityType.ImpactAction), OpportunityType.ImpactAction)));
    }

    [Fact]
    public async Task ReviewerAssessment_CarriesTheReservedDropOffSlot_NeverANumber()
    {
      var assessment = await AssessPublished(Published(Request(), OpportunityType.Learning));

      Assert.NotNull(assessment.DropOff);
      Assert.Equal(CourseQualityDropOffStatus.Collecting, assessment.DropOff.Status);
      Assert.Null(assessment.DropOff.Rate);
      Assert.Equal("Collecting from the pilot", assessment.DropOff.Note);

      var json = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(assessment.DropOff, new Newtonsoft.Json.JsonSerializerSettings
      {
        ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver { NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy() },
        Converters = [new Newtonsoft.Json.Converters.StringEnumConverter()]
      }));
      Assert.Equal(["note", "rate", "status"], json.Properties().Select(o => o.Name).Order());
      Assert.Equal("Collecting", json["status"]!.Value<string>());
    }

    [Fact]
    public void Event_KeepsItsOwnWords()
    {
      Assert.Equal("event", CourseQualityRules.Noun(OpportunityType.Event));
      Assert.Equal("impact action", CourseQualityRules.Noun(OpportunityType.ImpactAction));
    }
    #endregion
  }
}
