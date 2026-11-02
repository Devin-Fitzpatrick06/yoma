using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;
using OpportunityModel = Yoma.Core.Domain.Opportunity.Models.Opportunity;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityServiceTests
  {
    #region Harness
    private readonly List<CourseQualityAssessmentRecord> _records = [];
    private readonly Mock<IOpportunityService> _opportunities = new();
    private readonly Mock<IOrganizationService> _organizations = new();
    private readonly Mock<IOpportunityTypeService> _types = new();
    private readonly Mock<IOpportunityStatusService> _statuses = new();
    private readonly Mock<IOrganizationStatusService> _organizationStatuses = new();
    private readonly Mock<IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<OpportunityModel>> _opportunityRepository = new();
    private readonly Mock<ICourseQualityLinkChecker> _links = new();
    private readonly Mock<ICourseQualityAIProvider> _ai = new();
    private readonly OpportunityModel _opportunity = CleanListing();
    private readonly CourseQualityOptions _options = new() { Enabled = true };

    public CourseQualityServiceTests()
    {
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(WorkingLink());
      _ai.SetupGet(o => o.IsEnabled).Returns(true);
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ai(0.5m));
      _opportunities.Setup(o => o.GetById(_opportunity.Id, true, false, false, null)).Returns(() => _opportunity);
      _opportunities.Setup(o => o.GetByIdOrNull(_opportunity.Id, true, false, false, null)).Returns(() => _opportunity);
    }

    private CourseQualityService Service()
    {
      var repository = new Mock<IRepository<CourseQualityAssessmentRecord>>();
      repository.Setup(o => o.Query()).Returns(() => _records.AsQueryable());
      repository.Setup(o => o.Query(LockMode.Wait)).Returns(() => _records.AsQueryable());
      repository.Setup(o => o.Create(It.IsAny<CourseQualityAssessmentRecord>())).Returns((CourseQualityAssessmentRecord r) => { _records.Add(r); return Task.FromResult(r); });
      repository.Setup(o => o.Update(It.IsAny<CourseQualityAssessmentRecord>())).Returns((CourseQualityAssessmentRecord r) => Task.FromResult(r));

      var strategy = new Mock<IExecutionStrategyService>();
      strategy.Setup(o => o.ExecuteInExecutionStrategyAsync(It.IsAny<Func<Task>>(), It.IsAny<bool>())).Returns((Func<Task> body, bool _) => body());

      var accessor = new Mock<IHttpContextAccessor>();
      accessor.SetupGet(o => o.HttpContext).Returns(new DefaultHttpContext
      {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "reviewer@yoma.world")], "test"))
      });

      return new CourseQualityService(NullLogger<CourseQualityService>.Instance, accessor.Object, Options.Create(_options), _opportunities.Object,
        _organizations.Object, _types.Object, _statuses.Object, _organizationStatuses.Object, _opportunityRepository.Object, _links.Object, _ai.Object, repository.Object, strategy.Object);
    }

    private static CourseQualityReviewDecision Decide(CourseQualityStatus status) => new() { Status = status, Note = "Checked by hand" };

    private Task<CourseQualityAssessment> AssessNow() => Service().Assess(_opportunity.Id, CancellationToken.None);
    #endregion

    #region Page signals
    [Fact]
    public async Task Assess_PassesPageSignalsAndFinalUrlToTheProvider()
    {
      var signals = new CourseQualityPageSignals { Title = "Data" };
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(CourseQualityLinkCheckResult.Reachable(200, "https://final.example.org/data", "text", signals));

      await AssessNow();

      _ai.Verify(o => o.Evaluate(It.Is<CourseQualityAIRequest>(r => r.SourcePageSignals == signals && r.SourcePageFinalUrl == "https://final.example.org/data"), It.IsAny<CancellationToken>()));
    }
    #endregion

    #region Reviewer override
    [Fact]
    public async Task Decision_SurvivesReassessment_WhileListingIsUnchanged()
    {
      var first = await AssessNow();
      await Service().Review(first.Id, Decide(CourseQualityStatus.Verified));

      var second = await AssessNow();

      Assert.Equal(CourseQualityStatus.Verified, second.Status);
      Assert.Equal(CourseQualityReviewStatus.Confirmed, second.ReviewStatus);
      Assert.Equal("reviewer@yoma.world", second.ReviewedByEmail);
      Assert.Equal("Checked by hand", second.ReviewNote);
      Assert.NotNull(second.DateReviewed);
      Assert.Equal(CourseQualityStatus.Verified, second.LearnerCard.Status);
    }

    [Fact]
    public async Task Decision_Lapses_WhenTheListingContentChanges()
    {
      var first = await AssessNow();
      await Service().Review(first.Id, Decide(CourseQualityStatus.Verified));

      _opportunity.Description += " Now with a new final project.";
      var second = await AssessNow();

      Assert.Equal(CourseQualityStatus.NeedsReview, second.Status);
      Assert.Equal(CourseQualityReviewStatus.Pending, second.ReviewStatus);
      Assert.Null(second.ReviewedByEmail);
    }

    [Fact]
    public async Task Decision_Survives_ParticipantAndRewardUpdates()
    {
      var first = await AssessNow();
      await Service().Review(first.Id, Decide(CourseQualityStatus.Verified));

      _opportunity.ParticipantCount = 41;
      _opportunity.ZltoRewardCumulative = 900;
      _opportunity.DateModified = _opportunity.DateModified.AddHours(3);
      var second = await AssessNow();

      Assert.Equal(CourseQualityStatus.Verified, second.Status);
      Assert.Equal(CourseQualityReviewStatus.Confirmed, second.ReviewStatus);
    }

    [Fact]
    public async Task StoredRow_RecordsAContentFingerprint()
    {
      await AssessNow();

      Assert.Equal(Domain.CourseQuality.Helpers.CourseQualityFingerprint.Compute(_opportunity), _records.Single().ContentFingerprint);
      Assert.Equal(_opportunity.DateModified, _records.Single().OpportunityDateModified);
    }

    #region A definite failure ends a reviewer's Verified
    private async Task ReviewedVerified()
    {
      var first = await AssessNow();
      await Service().Review(first.Id, Decide(CourseQualityStatus.Verified));
    }

    private void LinkReturns(CourseQualityLinkCheckResult link) =>
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(link);

    [Fact]
    public async Task DeadLink_EndsAReviewersVerified_WithoutTheListingChanging()
    {
      await ReviewedVerified();
      LinkReturns(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, statusCode: 404));

      var second = await AssessNow();

      Assert.Equal(CourseQualityStatus.NotVerified, second.Status);
      Assert.Equal(CourseQualityReviewStatus.NotRequired, second.ReviewStatus);
      Assert.Equal(CourseQualityStatus.NotVerified, second.LearnerCard.Status);
      Assert.DoesNotContain("Reviewed by the Yoma team", second.LearnerCard.Highlights);
      Assert.Null(second.ReviewedByEmail);
      Assert.Null(second.ReviewNote);
      Assert.Null(second.DateReviewed);
      Assert.NotNull(second.FixNote);
    }

    [Fact]
    public async Task ExpiredListing_EndsAReviewersVerified()
    {
      await ReviewedVerified();
      _opportunity.Status = Status.Expired;

      Assert.Equal(CourseQualityStatus.NotVerified, (await AssessNow()).Status);
    }

    [Fact]
    public async Task EndedVerified_DoesNotComeBack_WhenTheLinkRecovers()
    {
      await ReviewedVerified();
      LinkReturns(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound));
      await AssessNow();
      LinkReturns(WorkingLink());

      var third = await AssessNow();

      Assert.Equal(CourseQualityStatus.NeedsReview, third.Status);
      Assert.Null(third.ReviewedByEmail);
    }

    [Fact]
    public async Task InconclusiveLink_DoesNotEndAReviewersVerified()
    {
      await ReviewedVerified();
      LinkReturns(CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout));

      Assert.Equal(CourseQualityStatus.Verified, (await AssessNow()).Status);
    }

    [Fact]
    public async Task ProviderFailure_DoesNotEndAReviewersVerified_BecauseTheReviewerOverridesIt()
    {
      await ReviewedVerified();
      var ai = Ai(0.95m);
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 10;
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(ai);

      var second = await AssessNow();

      Assert.Equal(CourseQualityStatus.Verified, second.Status);
      Assert.Equal("reviewer@yoma.world", second.ReviewedByEmail);
    }

    [Fact]
    public async Task DefiniteFailure_KeepsAReviewersNotVerified()
    {
      var first = await AssessNow();
      await Service().Review(first.Id, Decide(CourseQualityStatus.NotVerified));
      LinkReturns(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound));

      var second = await AssessNow();

      Assert.Equal(CourseQualityStatus.NotVerified, second.Status);
      Assert.Equal("reviewer@yoma.world", second.ReviewedByEmail);
    }

    [Fact]
    public async Task DeadLink_ListingProducesAFixNote_WithNoProvider()
    {
      _ai.SetupGet(o => o.IsEnabled).Returns(false);
      LinkReturns(CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, statusCode: 404));

      var result = await AssessNow();

      Assert.Equal(result.Id, Assert.Single(Service().ListFixNotes(_opportunity.OrganizationId)).AssessmentId);
    }
    #endregion

    [Fact]
    public async Task Decision_OverridesTheAI_AcrossReassessment()
    {
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ai(0.95m));
      var first = await AssessNow();
      Assert.Equal(CourseQualityStatus.Verified, first.Status);

      await Service().Review(first.Id, Decide(CourseQualityStatus.NotVerified));
      var second = await AssessNow();

      Assert.Equal(CourseQualityStatus.NotVerified, second.Status);
      Assert.Equal(CourseQualityReviewStatus.Overridden, second.ReviewStatus);
      Assert.NotNull(second.FixNote);
    }

    [Fact]
    public async Task Decision_MadeWhileAssessmentIsRunning_IsNotLost()
    {
      var first = await AssessNow();
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(async () =>
      {
        await Service().Review(first.Id, Decide(CourseQualityStatus.Verified));
        return WorkingLink();
      });

      var second = await AssessNow();

      Assert.Equal(CourseQualityStatus.Verified, second.Status);
    }

    [Fact]
    public async Task FixNote_ForNeedsReview_AppearsOnlyAfterReviewerResolvesNotVerified()
    {
      var first = await AssessNow();
      Assert.Null(first.FixNote);
      Assert.Empty(Service().ListFixNotes(_opportunity.OrganizationId));

      var reviewed = await Service().Review(first.Id, Decide(CourseQualityStatus.NotVerified));

      Assert.NotNull(reviewed.FixNote);
      Assert.Equal(first.Id, Assert.Single(Service().ListFixNotes(_opportunity.OrganizationId)).AssessmentId);
    }

    [Fact]
    public async Task Review_ToVerified_ClearsTheFixNote()
    {
      _opportunity.Hidden = true;
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ai(0.95m));
      var first = await AssessNow();
      Assert.NotNull(first.FixNote);

      var reviewed = await Service().Review(first.Id, Decide(CourseQualityStatus.Verified));

      Assert.Null(reviewed.FixNote);
      Assert.Equal(CourseQualityReviewStatus.Overridden, reviewed.ReviewStatus);
    }

    [Fact]
    public async Task Review_CannotResolveToNeedsReview()
    {
      var first = await AssessNow();

      await Assert.ThrowsAsync<ValidationException>(() => Service().Review(first.Id, Decide(CourseQualityStatus.NeedsReview)));
    }

    [Fact]
    public async Task Review_OfAReplacedAssessment_IsRejected()
    {
      var first = await AssessNow();
      await AssessNow();

      await Assert.ThrowsAsync<ValidationException>(() => Service().Review(first.Id, Decide(CourseQualityStatus.Verified)));
    }
    #endregion

    #region History
    [Fact]
    public async Task Reassessment_KeepsHistory_WithOneLatestRow()
    {
      await AssessNow();
      await AssessNow();
      var third = await AssessNow();

      Assert.Equal(3, _records.Count);
      Assert.Equal(third.Id, _records.Single(o => o.IsLatest).Id);
    }

    [Fact]
    public async Task StoredAssessment_RoundTrips()
    {
      var assessed = await AssessNow();

      var restored = _records.Single().ToAssessment();

      Assert.Equal(assessed.Criteria.Count, restored.Criteria.Count);
      Assert.Equal(assessed.LearnerCard.Status, restored.LearnerCard.Status);
      Assert.Equal(CourseCostType.Unknown, restored.LearnerCard.Cost.Type);
    }
    #endregion

    #region Gates and failures
    [Fact]
    public async Task Assess_IsRefused_WhenTurnedOff_WithoutFetching()
    {
      _options.Enabled = false;

      await Assert.ThrowsAsync<ValidationException>(AssessNow);

      _links.Verify(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Assess_RefusesOtherOpportunityTypes()
    {
      _opportunity.Type = Domain.Opportunity.Type.Job;

      await Assert.ThrowsAsync<ValidationException>(AssessNow);
    }

    [Fact]
    public async Task AIFailure_FallsBackToRules_NeverAPass()
    {
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));

      var result = await AssessNow();

      Assert.False(result.AIUsed);
      Assert.Equal(_options.RuleOnlyConfidenceCap, result.Confidence);
      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
    }

    [Fact]
    public async Task DisabledProvider_IsNotCalled()
    {
      _ai.SetupGet(o => o.IsEnabled).Returns(false);

      var result = await AssessNow();

      _ai.Verify(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>()), Times.Never);
      Assert.Equal(_options.RuleOnlyConfidenceCap, result.Confidence);
    }

    [Fact]
    public async Task NoOpProvider_RunsOnRulesAlone()
    {
      var provider = new NoOpCourseQualityAIProvider();

      Assert.False(provider.IsEnabled);
      Assert.Null(await provider.Evaluate(new CourseQualityAIRequest(), CancellationToken.None));
    }
    #endregion

    #region Public card
    [Fact]
    public async Task Browse_IsOff_ByDefault()
    {
      await AssessNow();
      var result = new Domain.Opportunity.Models.OpportunityInfo { Id = _opportunity.Id };

      Service().SetBrowseStatus([_opportunity], [result]);

      Assert.Null(Service().ListVerifiedForBrowse());
      Assert.Null(result.QualityStatus);
      Assert.Null(result.LearnerCard);
    }

    [Fact]
    public async Task Browse_ShowsTheStatusAndCard_OfACurrentAssessment()
    {
      _options.ShowInBrowse = true;
      await AssessNow();
      var result = new Domain.Opportunity.Models.OpportunityInfo { Id = _opportunity.Id };

      Service().SetBrowseStatus([_opportunity], [result]);

      Assert.Equal(_records.Single().Status, result.QualityStatus);
      Assert.Equal(_opportunity.Id, result.LearnerCard?.OpportunityId);
    }

    [Fact]
    public async Task Browse_NeverKeepsABadge_OnAnEditedListing()
    {
      _options.ShowInBrowse = true;
      await AssessNow();
      _records.Single().Status = CourseQualityStatus.Verified;
      _records.Single().ContentFingerprint = "edited";
      var result = new Domain.Opportunity.Models.OpportunityInfo { Id = _opportunity.Id };

      Service().SetBrowseStatus([_opportunity], [result]);

      Assert.Equal(CourseQualityStatus.NeedsReview, result.QualityStatus);
      Assert.Null(result.LearnerCard);
    }

    [Fact]
    public async Task Browse_RanksOnlyCurrentVerifiedAssessments()
    {
      _options.ShowInBrowse = true;
      await AssessNow();
      var record = _records.Single();

      record.Status = CourseQualityStatus.Verified;
      Assert.Equal([_opportunity.Id], Service().ListVerifiedForBrowse());

      record.DateAssessed = DateTimeOffset.UtcNow.AddDays(-(_options.CardMaxAgeInDays + 1));
      Assert.Empty(Service().ListVerifiedForBrowse()!);
    }

    [Fact]
    public void Browse_ReadsAsNotYetVerified_WithoutAnAssessment()
    {
      _options.ShowInBrowse = true;
      var result = new Domain.Opportunity.Models.OpportunityInfo { Id = _opportunity.Id };

      Service().SetBrowseStatus([_opportunity], [result]);

      Assert.Equal(CourseQualityStatus.NeedsReview, result.QualityStatus);
    }

    [Fact]
    public async Task PublicCard_IsReturned_ForAVisibleCourse()
    {
      await AssessNow();

      Assert.Equal(_opportunity.Id, Service().GetPublicCard(_opportunity.Id).OpportunityId);
    }

    [Fact]
    public void PublicCard_Is404_WithoutAnAssessment()
    {
      Assert.Throws<EntityNotFoundException>(() => Service().GetPublicCard(_opportunity.Id));
    }

    [Fact]
    public void PublicCard_Is404_ForAnUnknownOpportunity()
    {
      Assert.Throws<EntityNotFoundException>(() => Service().GetPublicCard(Guid.NewGuid()));
    }

    public static TheoryData<string> NotPublic => ["inactive", "expired", "hidden", "job", "orgInactive"];

    [Theory]
    [MemberData(nameof(NotPublic))]
    public async Task PublicCard_Is404_WhenNotPubliclyVisible(string state)
    {
      await AssessNow();

      switch (state)
      {
        case "inactive": _opportunity.Status = Status.Inactive; break;
        case "expired": _opportunity.Status = Status.Expired; break;
        case "hidden": _opportunity.Hidden = true; break;
        case "job": _opportunity.Type = Domain.Opportunity.Type.Job; break;
        case "orgInactive": _opportunity.OrganizationStatus = OrganizationStatus.Inactive; break;
      }

      Assert.Throws<EntityNotFoundException>(() => Service().GetPublicCard(_opportunity.Id));
    }
    #endregion

    #region Organisation access
    [Fact]
    public void FixNotes_ChecksOrganisationAccess_BeforeReading()
    {
      var other = Guid.NewGuid();
      _organizations.Setup(o => o.IsAdmin(other, true)).Throws(new SecurityException("Unauthorized"));

      Assert.Throws<SecurityException>(() => Service().ListFixNotes(other));
    }

    [Fact]
    public async Task FixNotes_ReturnOnlyTheRequestedOrganisation_AndLatestRows()
    {
      _opportunity.Hidden = true;
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ai(0.95m));
      await AssessNow();
      var latest = await AssessNow();

      Assert.Equal(latest.Id, Assert.Single(Service().ListFixNotes(_opportunity.OrganizationId)).AssessmentId);
      Assert.Empty(Service().ListFixNotes(Guid.NewGuid()));
    }
    #endregion

    #region Coverage
    private static readonly Guid LearningId = Guid.NewGuid();
    private static readonly Guid ActiveId = Guid.NewGuid();
    private static readonly Guid OrganizationActiveId = Guid.NewGuid();

    private OpportunityModel Published(Action<OpportunityModel>? change = null)
    {
      var result = CleanListing();
      result.TypeId = LearningId;
      result.StatusId = ActiveId;
      result.OrganizationStatusId = OrganizationActiveId;
      change?.Invoke(result);
      return result;
    }

    private void Stored(OpportunityModel opportunity, CourseFormat format, bool datesSuspect, bool cardReady, bool isLatest = true) => _records.Add(new CourseQualityAssessmentRecord
    {
      Id = Guid.NewGuid(),
      OpportunityId = opportunity.Id,
      Format = format,
      DatesSuspect = datesSuspect,
      CardReady = cardReady,
      IsLatest = isLatest
    });

    private void SetupCoverage(params OpportunityModel[] opportunities)
    {
      _types.Setup(o => o.GetByName("Learning")).Returns(new Domain.Opportunity.Models.Lookups.OpportunityType { Id = LearningId, Name = "Learning" });
      _statuses.Setup(o => o.GetByName("Active")).Returns(new Domain.Opportunity.Models.Lookups.OpportunityStatus { Id = ActiveId, Name = "Active" });
      _organizationStatuses.Setup(o => o.GetByName("Active")).Returns(new Domain.Entity.Models.Lookups.OrganizationStatus { Id = OrganizationActiveId, Name = "Active" });
      _opportunityRepository.Setup(o => o.Query()).Returns(opportunities.AsQueryable());
    }

    [Fact]
    public void Coverage_CountsPublishedCourses_FromTheirLatestAssessment()
    {
      var course = Published();
      var webinar = Published();
      var unknown = Published();
      var notAssessed = Published();
      var job = Published(o => o.TypeId = Guid.NewGuid());
      var hidden = Published(o => o.Hidden = true);
      SetupCoverage(course, webinar, unknown, notAssessed, job, hidden);

      Stored(course, CourseFormat.Unknown, true, false, isLatest: false);
      Stored(course, CourseFormat.Course, false, true);
      Stored(webinar, CourseFormat.Webinar, true, false);
      Stored(unknown, CourseFormat.Unknown, false, false);
      Stored(job, CourseFormat.Course, true, true);
      Stored(hidden, CourseFormat.Course, true, true);

      var result = Service().GetCoverage();

      Assert.Equal(4, result.TotalCourses);
      Assert.Equal(3, result.Assessed);
      Assert.Equal(1, result.NotAssessed);
      Assert.Equal(1, result.MissingOrSuspectDates);
      Assert.Equal(1, result.Course);
      Assert.Equal(1, result.Webinar);
      Assert.Equal(1, result.Unknown);
      Assert.Equal(1, result.CardReady);
    }

    [Fact]
    public async Task StoredRow_RecordsCoverageFacts()
    {
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Ai(0.95m));

      await AssessNow();

      var record = _records.Single();
      Assert.Equal(CourseFormat.Course, record.Format);
      Assert.False(record.DatesSuspect);
      Assert.True(record.CardReady);
    }
    #endregion

    #region Stale cards (finding 5)
    [Fact]
    public async Task PublicCard_ListingChangedSinceAssessment_IsNotShown()
    {
      await AssessNow();
      Assert.NotNull(Service().GetPublicCard(_opportunity.Id));

      _opportunity.URL = "https://elsewhere.example.org/bait";

      Assert.Throws<EntityNotFoundException>(() => Service().GetPublicCard(_opportunity.Id));
    }

    [Fact]
    public async Task PublicCard_OlderThanTheMaxAge_IsNotShown()
    {
      await AssessNow();
      _records.Single().DateAssessed = DateTimeOffset.UtcNow.AddDays(-(_options.CardMaxAgeInDays + 1));

      Assert.Throws<EntityNotFoundException>(() => Service().GetPublicCard(_opportunity.Id));
    }

    [Fact]
    public async Task PublicCard_EndDatePassedSinceAssessment_IsNotShown()
    {
      await AssessNow();
      _opportunity.DateEnd = DateTimeOffset.UtcNow.AddDays(-1);
      _records.Single().ContentFingerprint = Domain.CourseQuality.Helpers.CourseQualityFingerprint.Compute(_opportunity);

      Assert.Throws<EntityNotFoundException>(() => Service().GetPublicCard(_opportunity.Id));
    }
    #endregion

    #region Public card (C, H)
    [Fact]
    public void PublicCard_WithNoAssessment_DoesNotLoadTheListing()
    {
      Assert.Throws<EntityNotFoundException>(() => Service().GetPublicCard(_opportunity.Id));

      _opportunities.Verify(o => o.GetByIdOrNull(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), null), Times.Never);
    }

    [Fact]
    public async Task PublicCard_TooOld_DoesNotLoadTheListing()
    {
      await AssessNow();
      _records.Single().DateAssessed = DateTimeOffset.UtcNow.AddDays(-(_options.CardMaxAgeInDays + 1));
      _opportunities.Invocations.Clear();

      Assert.Throws<EntityNotFoundException>(() => Service().GetPublicCard(_opportunity.Id));

      _opportunities.Verify(o => o.GetByIdOrNull(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), null), Times.Never);
    }

    [Fact]
    public async Task PublicCard_DatedWebinarWithNoEnd_ExpiresOnceItsStartHasPassed()
    {
      _opportunity.DateStart = DateTimeOffset.UtcNow.AddDays(3);
      _opportunity.DateEnd = null;
      await AssessNow();
      var record = _records.Single();
      record.Format = CourseFormat.Webinar;
      Assert.NotNull(Service().GetPublicCard(_opportunity.Id));

      _opportunity.DateStart = DateTimeOffset.UtcNow.AddHours(-1);
      record.ContentFingerprint = Domain.CourseQuality.Helpers.CourseQualityFingerprint.Compute(_opportunity);

      Assert.Throws<EntityNotFoundException>(() => Service().GetPublicCard(_opportunity.Id));
    }
    #endregion

    #region Reviewer's Verified must be earned again (D)
    private async Task<CourseQualityAssessment> FreshlyReviewedVerified()
    {
      var first = await AssessNow();
      await Service().Review(first.Id, new CourseQualityReviewDecision { Status = CourseQualityStatus.Verified });
      return first;
    }

    [Fact]
    public async Task ReviewerVerified_Carries_WhenTheFreshCheckBacksIt()
    {
      await FreshlyReviewedVerified();

      Assert.Equal(CourseQualityStatus.Verified, (await AssessNow()).Status);
      Assert.NotNull(_records.Last().DateConfirmed);
    }

    [Fact]
    public async Task ReviewerVerified_Ends_WhenTheLinkLandsOnAnotherHost()
    {
      await FreshlyReviewedVerified();
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Reachable(200, "https://elsewhere.example.net/x", "Course page text"));

      var second = await AssessNow();

      Assert.NotEqual(CourseQualityStatus.Verified, second.Status);
      Assert.Equal(CourseQualityReviewStatus.Pending, second.ReviewStatus);
    }

    [Fact]
    public async Task ReviewerVerified_Ends_WhenThePageNoLongerMatches()
    {
      await FreshlyReviewedVerified();
      var weak = Ai();
      weak.StrongMatch = false;
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(weak);

      Assert.NotEqual(CourseQualityStatus.Verified, (await AssessNow()).Status);
    }

    [Fact]
    public async Task ReviewerVerified_Ends_WhenAProviderScoresTheLinkLow()
    {
      await FreshlyReviewedVerified();
      var ai = Ai();
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.LinkWorks).Score = 10;
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(ai);

      Assert.NotEqual(CourseQualityStatus.Verified, (await AssessNow()).Status);
    }

    [Fact]
    public async Task ReviewerVerified_Ends_WhenThePageCouldNotBeRead()
    {
      await FreshlyReviewedVerified();
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((CourseQualityAIResult?)null);

      Assert.NotEqual(CourseQualityStatus.Verified, (await AssessNow()).Status);
    }

    [Fact]
    public async Task ReviewerVerified_SurvivesANetworkBlip_WithinTheMaxAge()
    {
      await FreshlyReviewedVerified();
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout));

      Assert.Equal(CourseQualityStatus.Verified, (await AssessNow()).Status);
    }

    [Fact]
    public async Task ReviewerVerified_Ends_WhenTheNetworkStaysSilentPastTheMaxAge()
    {
      await FreshlyReviewedVerified();
      var record = _records.Single();
      record.DateReviewed = DateTimeOffset.UtcNow.AddDays(-(_options.CardMaxAgeInDays + 1));
      record.DateConfirmed = record.DateReviewed;
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout));

      Assert.NotEqual(CourseQualityStatus.Verified, (await AssessNow()).Status);
    }

    [Fact]
    public async Task ReviewerVerified_HeldThroughABlip_KeepsTheLastConfirmedHost()
    {
      await FreshlyReviewedVerified();
      var host = _records.Single().FinalHost;
      Assert.False(string.IsNullOrEmpty(host));
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout, "https://parked.example.net/x"));

      await AssessNow();

      Assert.Equal(host, _records.Last().FinalHost);
    }

    [Fact]
    public async Task ReviewerVerified_RepeatedBlips_DoNotExtendTheWindow()
    {
      await FreshlyReviewedVerified();
      var first = _records.Single();
      first.DateConfirmed = DateTimeOffset.UtcNow.AddDays(-(_options.CardMaxAgeInDays - 1));
      first.DateReviewed = first.DateConfirmed;
      var confirmed = first.DateConfirmed;
      _links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout));

      await AssessNow();

      Assert.Equal(confirmed, _records.Last().DateConfirmed);
    }
    #endregion

    #region Reviewer and gone pages (finding 6)
    [Fact]
    public async Task ReviewerVerified_EndsWhenThePageSaysItIsGone()
    {
      var first = await AssessNow();
      await Service().Review(first.Id, new CourseQualityReviewDecision { Status = CourseQualityStatus.Verified });

      var gone = Ai();
      gone.PageGone = true;
      _ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(gone);

      var second = await AssessNow();

      Assert.NotEqual(CourseQualityStatus.Verified, second.Status);
      Assert.Equal(CourseQualityReviewStatus.Pending, second.ReviewStatus);
    }
    #endregion

    #region Reviewer format
    private void UnknownFormatListing()
    {
      _opportunity.Title = "Data skills";
      _opportunity.CommitmentInterval = Domain.Core.TimeIntervalOption.Hour;
      _opportunity.CommitmentIntervalCount = 2;
    }

    private static CourseQualityReviewDecision Decide(CourseQualityStatus status, CourseFormat format) => new() { Status = status, Format = format };

    [Fact]
    public async Task Reviewer_CanSetTheFormat_AndTheCriterionPasses()
    {
      UnknownFormatListing();
      var first = await AssessNow();
      Assert.Equal(CourseFormat.Unknown, first.LearnerCard.Format);

      var reviewed = await Service().Review(first.Id, Decide(CourseQualityStatus.Verified, CourseFormat.Webinar));

      Assert.Equal(CourseFormat.Webinar, reviewed.LearnerCard.Format);
      Assert.Equal(100, reviewed.Criteria.Single(o => o.Criterion == CourseQualityCriterion.FormatIdentified).Score);
      Assert.Equal(CourseFormat.Webinar, _records.Single().Format);
      Assert.True(reviewed.Score > first.Score);
    }

    [Fact]
    public async Task ReviewerFormat_SurvivesReassessment_WhileListingIsUnchanged()
    {
      UnknownFormatListing();
      var first = await AssessNow();
      await Service().Review(first.Id, Decide(CourseQualityStatus.Verified, CourseFormat.Webinar));

      var second = await AssessNow();

      Assert.Equal(CourseFormat.Webinar, second.LearnerCard.Format);
      Assert.Equal(CourseFormat.Webinar, _records.Single(o => o.IsLatest).Format);
    }

    [Fact]
    public async Task ReviewerFormat_Lapses_WhenTheListingChanges()
    {
      UnknownFormatListing();
      var first = await AssessNow();
      await Service().Review(first.Id, Decide(CourseQualityStatus.Verified, CourseFormat.Webinar));

      _opportunity.Description += " Updated outline.";

      Assert.Equal(CourseFormat.Unknown, (await AssessNow()).LearnerCard.Format);
    }

    [Fact]
    public async Task Review_CannotVerifyAListingWithoutAFormat()
    {
      UnknownFormatListing();
      var first = await AssessNow();

      await Assert.ThrowsAsync<ValidationException>(() => Service().Review(first.Id, Decide(CourseQualityStatus.Verified)));
    }

    [Fact]
    public async Task Review_CannotSetFormatToUnknown()
    {
      var first = await AssessNow();

      await Assert.ThrowsAsync<ValidationException>(() => Service().Review(first.Id, Decide(CourseQualityStatus.NotVerified, CourseFormat.Unknown)));
    }

    [Fact]
    public async Task Review_CanHoldBackAListingWithoutAFormat()
    {
      UnknownFormatListing();
      var first = await AssessNow();

      var reviewed = await Service().Review(first.Id, Decide(CourseQualityStatus.NotVerified));

      Assert.Equal(CourseQualityStatus.NotVerified, reviewed.Status);
      Assert.Contains(reviewed.FixNote!.Issues, o => o.Criterion == CourseQualityCriterion.FormatIdentified);
    }
    #endregion

    #region Search
    [Fact]
    public async Task Search_FiltersLatestRows_AndPages()
    {
      await AssessNow();
      await AssessNow();

      var results = Service().SearchReviews(new CourseQualityReviewSearchFilter { ReviewStatus = CourseQualityReviewStatus.Pending });

      Assert.Equal(1, results.TotalCount);
      Assert.Single(results.Items);
      Assert.Equal(0, Service().SearchReviews(new CourseQualityReviewSearchFilter { ReviewStatus = CourseQualityReviewStatus.Confirmed }).TotalCount);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Search_RejectsBadPaging(int pageNumber, int pageSize)
    {
      Assert.Throws<ValidationException>(() => Service().SearchReviews(new CourseQualityReviewSearchFilter { PageNumber = pageNumber, PageSize = pageSize }));
    }
    #endregion
  }
}
