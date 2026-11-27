using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Xunit;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;
using OpportunityModel = Yoma.Core.Domain.Opportunity.Models.Opportunity;
using OpportunityType = Yoma.Core.Domain.Opportunity.Type;

namespace Yoma.Core.Test.CourseQuality
{
  /// <summary>
  /// Listing checks for Event and Job (iteration 3, F5 step 1): the course rubric's listing checks without its course-only ones.
  /// </summary>
  public class CourseQualityListingTypesTests
  {
    #region Helpers
    private static readonly CourseQualityOptions Options = new();

    private static OpportunityModel Listing(OpportunityType type)
    {
      var listing = CleanListing();
      listing.Type = type;
      listing.Title = type == OpportunityType.Job ? "Junior Data Analyst" : "Youth Climate Summit";
      return listing;
    }

    private static CourseQualityAssessment Assess(OpportunityModel listing, CourseQualityAIResult? ai = null, CourseQualityLinkCheckResult? link = null) =>
      CourseQualityEngine.Assess(listing, link ?? WorkingLink(), ai, Options, Now);

    private static CourseQualityAIResult JobAi(decimal confidence = 0.75m) => new()
    {
      IsAI = false,
      Model = "test-reader",
      Confidence = confidence,
      Summary = "The page matches this job listing.",
      StrongMatch = true,
      Cost = new LearnerCardCost { Type = CourseCostType.Unknown },
      Criteria = [new CourseQualityAICriterionResult { Criterion = CourseQualityCriterion.DataComplete, Score = 100, Reason = "The job page matches this listing." }]
    };
    #endregion

    #region Rules
    [Theory]
    [InlineData(OpportunityType.Event)]
    [InlineData(OpportunityType.Job)]
    public void ListingTypes_HaveNoFormatCriterion(OpportunityType type)
    {
      var rules = CourseQualityRules.Evaluate(Listing(type), WorkingLink(), CourseFormat.Unknown, "4 weeks", Options, Now);

      Assert.Equal([CourseQualityCriterion.ListingLive, CourseQualityCriterion.LinkWorks, CourseQualityCriterion.DatesAccurate, CourseQualityCriterion.DataComplete],
        rules.Select(o => o.Criterion));
      Assert.All(rules, o => Assert.False(o.Failed));
    }

    [Fact]
    public void Learning_KeepsAllFiveCriteria()
    {
      var rules = CourseQualityRules.Evaluate(CleanListing(), WorkingLink(), CourseFormat.Course, "4 weeks", Options, Now);

      Assert.Equal(5, rules.Count);
      Assert.Contains(rules, o => o.Criterion == CourseQualityCriterion.FormatIdentified);
      Assert.Equal("The course link opens a usable page.", rules.Single(o => o.Criterion == CourseQualityCriterion.LinkWorks).Reason);
    }

    [Fact]
    public void Job_WithoutTimeCommitment_IsComplete()
    {
      var job = Listing(OpportunityType.Job);
      job.CommitmentInterval = null;
      job.CommitmentIntervalCount = null;

      var data = CourseQualityRules.Evaluate(job, WorkingLink(), CourseFormat.Unknown, null, Options, Now).Single(o => o.Criterion == CourseQualityCriterion.DataComplete);

      Assert.False(data.Failed);
      Assert.Equal(100, data.Score);
    }

    [Fact]
    public void Job_WithoutLocation_IsIncomplete()
    {
      var job = Listing(OpportunityType.Job);
      job.Countries = [];

      var data = CourseQualityRules.Evaluate(job, WorkingLink(), CourseFormat.Unknown, null, Options, Now).Single(o => o.Criterion == CourseQualityCriterion.DataComplete);

      Assert.True(data.Failed);
      Assert.Equal(50, data.Score);
      Assert.Equal("Add to the listing: location.", data.Recommendation);
    }

    [Fact]
    public void Event_WithoutTimeCommitment_IsIncomplete()
    {
      var listing = Listing(OpportunityType.Event);
      listing.CommitmentInterval = null;
      listing.CommitmentIntervalCount = null;

      var data = CourseQualityRules.Evaluate(listing, WorkingLink(), CourseFormat.Unknown, null, Options, Now).Single(o => o.Criterion == CourseQualityCriterion.DataComplete);

      Assert.True(data.Failed);
      Assert.Contains("time commitment", data.Reason);
      Assert.Contains("timeline", data.Reason);
    }

    [Fact]
    public void Event_BrokenLink_SpeaksOfTheEvent()
    {
      var link = CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, null, 404);

      var rule = CourseQualityRules.Evaluate(Listing(OpportunityType.Event), link, CourseFormat.Unknown, "4 weeks", Options, Now).Single(o => o.Criterion == CourseQualityCriterion.LinkWorks);

      Assert.True(rule.Failed);
      Assert.Equal("The event link returned page not found (404).", rule.Reason);
      Assert.Equal("Update the listing with a working public link to the event.", rule.Recommendation);
    }

    [Fact]
    public void Event_NoEndDateAndPastStart_IsNotTreatedAsAnEndedWebinar()
    {
      var listing = Listing(OpportunityType.Event);
      listing.DateEnd = null;
      listing.DateStart = Now.AddDays(-30);

      var live = CourseQualityRules.Evaluate(listing, WorkingLink(), CourseFormat.Webinar, "4 weeks", Options, Now).Single(o => o.Criterion == CourseQualityCriterion.ListingLive);

      Assert.False(live.Failed);
    }
    #endregion

    #region Engine
    [Fact]
    public void Event_MatchedPageWithConfirmedDates_IsVerified()
    {
      var result = Assess(Listing(OpportunityType.Event), Ai());

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.Equal(100, result.Score);
      Assert.Equal(CourseFormat.Unknown, result.LearnerCard.Format);
      Assert.False(result.LearnerCard.SelfPaced);
      Assert.DoesNotContain(result.Criteria, o => o.Criterion == CourseQualityCriterion.FormatIdentified);
      Assert.True(CourseQualityEngine.IsListingOnly(result));
    }

    [Fact]
    public void Event_SelfPacedClaim_IsIgnored()
    {
      var ai = Ai();
      ai.SelfPaced = true;
      var listing = Listing(OpportunityType.Event);
      listing.DateEnd = null;

      var result = Assess(listing, ai);

      Assert.False(result.LearnerCard.SelfPaced);
      Assert.DoesNotContain(result.ReviewReasons, o => o.Contains("self-paced", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(OpportunityType.Event)]
    [InlineData(OpportunityType.Job)]
    public void UnreadPage_GoesToReview_WithoutAFormatReason(OpportunityType type)
    {
      var result = Assess(Listing(type));

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.NotEmpty(result.ReviewReasons);
      Assert.DoesNotContain(result.ReviewReasons, o => o.Contains("format", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Course_UnreadPage_StillGivesTheFormatReason()
    {
      var listing = CleanListing();
      listing.Title = "Data things";
      listing.Description = "Data things.";
      listing.CommitmentInterval = Domain.Core.TimeIntervalOption.Day;
      listing.CommitmentIntervalCount = 1;

      var result = Assess(listing);

      Assert.Contains(result.ReviewReasons, o => o.Contains("format", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Job_MatchedPage_IsVerified_WithoutPageDates()
    {
      var job = Listing(OpportunityType.Job);
      job.CommitmentInterval = null;
      job.CommitmentIntervalCount = null;

      var result = Assess(job, JobAi());

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.Empty(result.ReviewReasons);
    }

    [Fact]
    public void Event_MatchedPageWithoutDates_GoesToReview()
    {
      var ai = Ai();
      ai.DatesStartConfirmed = false;
      ai.DatesEndConfirmed = false;

      var result = Assess(Listing(OpportunityType.Event), ai);

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Contains("The page does not confirm the listing's dates.", result.ReviewReasons);
    }

    [Fact]
    public void Job_PageSaysFilled_GoesToReview()
    {
      var ai = JobAi(0.1m);
      ai.PageGone = true;

      var result = Assess(Listing(OpportunityType.Job), ai);

      Assert.NotEqual(CourseQualityStatus.Verified, result.Status);
    }

    [Fact]
    public void Event_Ended_IsNotVerified_WithAListingFixNote()
    {
      var listing = Listing(OpportunityType.Event);
      listing.DateEnd = Now.AddDays(-1);

      var result = Assess(listing, Ai());

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.NotNull(result.FixNote);
      Assert.Equal("This listing is not verified yet because a required check did not pass. The items below need attention.", result.FixNote.Summary);
      Assert.Contains("if the event is still running", result.FixNote.Issues.Single(o => o.Criterion == CourseQualityCriterion.ListingLive).Recommendation);
    }

    [Fact]
    public void ReviewDecision_WithAFormat_LeavesAnEventWithoutOne()
    {
      var assessment = Assess(Listing(OpportunityType.Event));

      CourseQualityReviewPolicy.ApplyDecision(assessment, new CourseQualityReviewDecision { Status = CourseQualityStatus.Verified, Format = CourseFormat.Course },
        "reviewer@yoma.world", Now, Options);

      Assert.Equal(CourseQualityStatus.Verified, assessment.Status);
      Assert.Equal(CourseFormat.Unknown, assessment.LearnerCard.Format);
      Assert.DoesNotContain(assessment.Criteria, o => o.Criterion == CourseQualityCriterion.FormatIdentified);
    }
    #endregion

    #region Source page reader
    private static string JobPage(string extra = "") =>
      "<html><head><title>Junior Data Analyst | Northwind</title></head><body><h1>Junior Data Analyst</h1>" +
      $"<p>Northwind is hiring a junior data analyst to clean, explore and present data. {extra}</p>" +
      $"<p>{string.Concat(Enumerable.Repeat("You will work with the analytics team on weekly reports and dashboards. ", 8))}</p></body></html>";

    private static CourseQualityAIRequest ReaderRequest(OpportunityType type, string html, string title = "Junior Data Analyst")
    {
      var link = CourseQualityLinkCheckResult.Reachable(200, "https://jobs.example.org/junior-data-analyst",
        CourseQualityPageText.FromHtml(html, Options.LinkCheck.MaxSourceTextLength), CourseQualityPageSignalExtractor.FromHtml(html));

      return new CourseQualityAIRequest
      {
        Title = title,
        Description = "Clean, explore and present data with the analytics team.",
        Type = type,
        URL = "https://jobs.example.org/junior-data-analyst",
        DateStart = Now.AddDays(-5),
        DateEnd = Now.AddDays(25),
        SourcePageText = link.SourcePageText,
        SourcePageSignals = link.SourcePageSignals,
        SourcePageFinalUrl = link.FinalUrl
      };
    }

    private static Task<CourseQualityAIResult?> Read(CourseQualityAIRequest request) =>
      new CourseQualitySourcePageReader(Microsoft.Extensions.Options.Options.Create(new CourseQualityOptions())).Evaluate(request, CancellationToken.None);

    [Fact]
    public async Task Reader_Job_MatchedPage_IsJudgedWithoutDatesOrFormat()
    {
      var result = await Read(ReaderRequest(OpportunityType.Job, JobPage()));

      Assert.NotNull(result);
      Assert.True(result.StrongMatch);
      Assert.Equal(0.75m, result.Confidence);
      Assert.Equal(CourseFormat.Unknown, result.Format);
      Assert.False(result.SelfPaced);
      Assert.Equal([CourseQualityCriterion.DataComplete], result.Criteria.Select(o => o.Criterion));
    }

    [Theory]
    [InlineData("This position has been filled.")]
    [InlineData("This job has expired.")]
    [InlineData("We are no longer accepting applications for this role.")]
    public async Task Reader_Job_ClosedPost_IsGone(string wording)
    {
      var result = await Read(ReaderRequest(OpportunityType.Job, JobPage(wording)));

      Assert.NotNull(result);
      Assert.True(result.PageGone);
      Assert.True(result.Confidence < Options.ReviewConfidenceThreshold);
    }

    [Fact]
    public void JobClosedWording_IsNotReadIntoCoursePages()
    {
      Assert.False(CourseQualityPageMatch.IsJobClosed("Learn how a job interview works and how to apply for a post."));
      Assert.True(CourseQualityPageMatch.IsJobClosed("Applications are now closed."));
    }

    [Fact]
    public async Task Reader_Event_DoesNotPenaliseAnUnknownFormat()
    {
      var html = "<html><head><title>Youth Climate Summit | Northwind</title></head><body><h1>Youth Climate Summit</h1>" +
        $"<p>{string.Concat(Enumerable.Repeat("Join young leaders for talks and workshops on climate action in your city. ", 8))}</p></body></html>";

      var result = await Read(ReaderRequest(OpportunityType.Event, html, "Youth Climate Summit"));

      Assert.NotNull(result);
      Assert.True(result.StrongMatch);
      Assert.Equal(CourseFormat.Unknown, result.Format);
      Assert.Contains(result.Criteria, o => o.Criterion == CourseQualityCriterion.DatesAccurate);
      Assert.DoesNotContain("format", result.Summary, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Options
    [Fact]
    public void VerifyTypes_DefaultsToLearningOnly()
    {
      Assert.Equal([OpportunityType.Learning], new CourseQualityOptions().VerifiedTypes());
    }

    [Fact]
    public void VerifyTypes_FromConfiguration_AddsEvent_AndIgnoresTheRest_JobsHaveTheirOwnChecks()
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["CourseQuality:VerifyTypes:0"] = "Learning",
        ["CourseQuality:VerifyTypes:1"] = "Event",
        ["CourseQuality:VerifyTypes:2"] = "Job",
        ["CourseQuality:VerifyTypes:3"] = "ImpactAction"
      }).Build();

      var options = new CourseQualityOptions();
      configuration.GetSection(CourseQualityOptions.Section).Bind(options);

      // jobs are checked and graded by their own pipeline (CourseQuality:Jobs:Enabled), not by the listing rules
      Assert.Equal([OpportunityType.Learning, OpportunityType.Event], options.VerifiedTypes());
    }
    #endregion

    #region Service
    private readonly List<CourseQualityAssessmentRecord> _records = [];
    private readonly Mock<ICourseQualityGradingService> _grading = new();

    private CourseQualityService Service(OpportunityModel opportunity, CourseQualityOptions options, CourseQualityAIResult? aiResult = null)
    {
      var opportunities = new Mock<IOpportunityService>();
      opportunities.Setup(o => o.GetById(opportunity.Id, true, false, false, null)).Returns(opportunity);

      var links = new Mock<ICourseQualityLinkChecker>();
      links.Setup(o => o.Check(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(WorkingLink());

      var ai = new Mock<ICourseQualityAIProvider>();
      ai.SetupGet(o => o.IsEnabled).Returns(true);
      ai.Setup(o => o.Evaluate(It.IsAny<CourseQualityAIRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiResult ?? Ai());

      var repository = new Mock<IRepository<CourseQualityAssessmentRecord>>();
      repository.Setup(o => o.Query()).Returns(() => _records.AsQueryable());
      repository.Setup(o => o.Query(Domain.Core.LockMode.Wait)).Returns(() => _records.AsQueryable());
      repository.Setup(o => o.Create(It.IsAny<CourseQualityAssessmentRecord>())).Returns((CourseQualityAssessmentRecord r) => { _records.Add(r); return Task.FromResult(r); });
      repository.Setup(o => o.Update(It.IsAny<CourseQualityAssessmentRecord>())).Returns((CourseQualityAssessmentRecord r) => Task.FromResult(r));

      var strategy = new Mock<IExecutionStrategyService>();
      strategy.Setup(o => o.ExecuteInExecutionStrategyAsync(It.IsAny<Func<Task>>(), It.IsAny<bool>())).Returns((Func<Task> body, bool _) => body());

      var accessor = new Mock<IHttpContextAccessor>();
      accessor.SetupGet(o => o.HttpContext).Returns(new DefaultHttpContext
      {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "reviewer@yoma.world")], "test"))
      });

      _grading.Setup(o => o.ListGrades(It.IsAny<List<Guid>>())).Returns([]);
      var feedback = new Mock<ICourseQualityFeedbackService>();
      feedback.Setup(o => o.ListSummaries(It.IsAny<Dictionary<Guid, Guid>>())).Returns([]);

      return new CourseQualityService(NullLogger<CourseQualityService>.Instance, accessor.Object, Microsoft.Extensions.Options.Options.Create(options), opportunities.Object,
        new Mock<IOrganizationService>().Object, new Mock<IOpportunityTypeService>().Object, new Mock<IOpportunityStatusService>().Object,
        new Mock<IOrganizationStatusService>().Object, new Mock<IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<OpportunityModel>>().Object,
        links.Object, ai.Object, repository.Object, strategy.Object, _grading.Object, feedback.Object);
    }

    [Fact]
    public async Task Service_RefusesAnEvent_UntilConfigured()
    {
      var listing = Listing(OpportunityType.Event);

      var ex = await Assert.ThrowsAsync<ValidationException>(() => Service(listing, new CourseQualityOptions { Enabled = true }).Assess(listing.Id, CancellationToken.None));
      Assert.Equal("Only opportunities of type 'Learning' can be assessed", ex.Message);
    }

    [Fact]
    public async Task Service_AssessesAConfiguredEvent_WithoutAskingForARegrade()
    {
      var listing = Listing(OpportunityType.Event);
      var options = new CourseQualityOptions { Enabled = true, VerifyTypes = [OpportunityType.Event] };

      var result = await Service(listing, options).Assess(listing.Id, CancellationToken.None);

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.Single(_records);
      _grading.Verify(o => o.RequestRegrade(), Times.Never);
    }

    [Fact]
    public async Task Service_ReviewVerifiesAnEvent_WithoutAFormat()
    {
      var listing = Listing(OpportunityType.Event);
      var options = new CourseQualityOptions { Enabled = true, VerifyTypes = [OpportunityType.Event] };
      var ai = Ai(0.5m);

      var service = Service(listing, options, ai);
      var first = await service.Assess(listing.Id, CancellationToken.None);
      Assert.Equal(CourseQualityStatus.NeedsReview, first.Status);

      var reviewed = await service.Review(first.Id, new CourseQualityReviewDecision { Status = CourseQualityStatus.Verified });

      Assert.Equal(CourseQualityStatus.Verified, reviewed.Status);
      Assert.Equal(CourseFormat.Unknown, reviewed.LearnerCard.Format);
    }
    #endregion
  }
}
