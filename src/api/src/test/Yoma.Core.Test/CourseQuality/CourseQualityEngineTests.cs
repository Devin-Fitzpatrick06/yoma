using Yoma.Core.Domain.CourseQuality.Extensions;
using Xunit;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Opportunity;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityEngineTests
  {
    private static readonly CourseQualityOptions Options = new();

    private static CourseQualityAssessment Assess(Domain.Opportunity.Models.Opportunity? o = null, CourseQualityLinkCheckResult? link = null, CourseQualityAIResult? ai = null, CourseQualityOptions? options = null) =>
      CourseQualityEngine.Assess(o ?? CleanListing(), link ?? WorkingLink(), ai, options ?? Options, Now);

    #region Routing
    [Fact]
    public void Verified_WhenScoreAndConfidenceMeetThresholdsAndNoGateFailed()
    {
      var result = Assess(ai: Ai());

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.Equal(CourseQualityReviewStatus.NotRequired, result.ReviewStatus);
      Assert.Equal(100, result.Score);
      Assert.Null(result.FixNote);
      Assert.Equal(CourseQualityStatus.Verified, result.LearnerCard.Status);
    }

    [Fact]
    public void Verified_AtExactThresholds()
    {
      var ai = Ai(0.7m);
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DataComplete).Score = 50;
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.FormatIdentified).Score = 17; // 70 + 0.15 * 67 = 80.05

      var result = Assess(ai: ai);

      Assert.Equal(80, result.Score);
      Assert.Equal(CourseQualityStatus.Verified, result.Status);
    }

    [Fact]
    public void NeedsReview_WhenConfidenceJustBelowThreshold()
    {
      var result = Assess(ai: Ai(0.699m));

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Equal(CourseQualityReviewStatus.Pending, result.ReviewStatus);
      Assert.Null(result.FixNote);
    }

    [Fact]
    public void NotVerified_WhenConfidentButScoreBelowThreshold()
    {
      var ai = Ai();
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DataComplete).Score = 50;
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.FormatIdentified).Score = 10; // 70 + 0.15 * 60 = 79

      var result = Assess(ai: ai);

      Assert.Equal(79, result.Score);
      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.Equal(CourseQualityReviewStatus.NotRequired, result.ReviewStatus);
      Assert.DoesNotContain(result.Criteria, o => o.HardGateFailed);
      Assert.NotNull(result.FixNote);
    }

    [Fact]
    public void NotVerified_WhenConfidentAndHardGateFailed()
    {
      var opportunity = CleanListing();
      opportunity.Hidden = true;

      var result = Assess(opportunity, ai: Ai());

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.NotNull(result.FixNote);
      Assert.Contains(result.FixNote!.Issues, o => o.Criterion == CourseQualityCriterion.ListingLive && o.Recommendation != null);
    }

    public static TheoryData<string> DefiniteFailures => ["deadLink", "goneLink", "noDomain", "noLink", "blockedLink", "expired", "inactive", "hidden", "ended", "organisation", "endBeforeStart", "placeholderStart"];

    private static (Domain.Opportunity.Models.Opportunity Listing, CourseQualityLinkCheckResult Link) Failing(string failure)
    {
      var listing = CleanListing();
      var link = WorkingLink(null);

      switch (failure)
      {
        case "deadLink": link = CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, statusCode: 404); break;
        case "goneLink": link = CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, statusCode: 410); break;
        case "noDomain": link = CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.DomainNotFound); break;
        case "noLink": link = CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.Missing); break;
        case "blockedLink": link = CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.BlockedAddress); break;
        case "expired": listing.Status = Status.Expired; break;
        case "inactive": listing.Status = Status.Inactive; break;
        case "hidden": listing.Hidden = true; break;
        case "ended": listing.DateEnd = Now.AddDays(-1); break;
        case "organisation": listing.OrganizationStatus = OrganizationStatus.Inactive; break;
        case "endBeforeStart": listing.DateEnd = listing.DateStart.AddDays(-1); break;
        case "placeholderStart": listing.DateStart = default; listing.DateEnd = null; break;
      }

      return (listing, link);
    }

    [Theory]
    [MemberData(nameof(DefiniteFailures))]
    public void DefiniteRuleFailure_GoesToNotVerified_WithoutPageEvidenceOrProvider(string failure)
    {
      var (listing, link) = Failing(failure);

      var result = Assess(listing, link);

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.Equal(CourseQualityReviewStatus.NotRequired, result.ReviewStatus);
      Assert.Equal(Options.HardGateScoreCap, Math.Max(result.Score, Options.HardGateScoreCap));
      Assert.Equal(Options.RuleOnlyConfidenceCap, result.Confidence); // the cap itself still stands
      Assert.NotNull(result.FixNote);
      Assert.Contains(result.FixNote!.Issues, o => o.HardGateFailed && o.Recommendation != null);
      Assert.Equal(CourseQualityStatus.NotVerified, result.LearnerCard.Status);
    }

    [Theory]
    [MemberData(nameof(DefiniteFailures))]
    public void DefiniteRuleFailure_IsReportedSeparately(string failure)
    {
      var (listing, link) = Failing(failure);

      CourseQualityEngine.Assess(listing, link, null, Options, Now, out var fresh);

      Assert.True(fresh.DefiniteFailure || fresh.PageGone);
    }

    [Fact]
    public void DefiniteRuleFailure_BeatsUnknownFormat_AndTheNoteCoversBoth()
    {
      var listing = CleanListing();
      listing.Title = "Budgeting Basics";
      listing.CommitmentInterval = Domain.Core.TimeIntervalOption.Hour;
      listing.CommitmentIntervalCount = 1;

      var result = Assess(listing, CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, statusCode: 404));

      Assert.Equal(CourseFormat.Unknown, result.LearnerCard.Format);
      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.Contains(result.FixNote!.Issues, o => o.Criterion == CourseQualityCriterion.LinkWorks);
      Assert.Contains(result.FixNote!.Issues, o => o.Criterion == CourseQualityCriterion.FormatIdentified);
    }

    [Fact]
    public void DefiniteRuleFailure_BeatsADisputedEndDate()
    {
      var listing = CleanListing();
      listing.DateEnd = null;
      listing.Hidden = true;
      var ai = Ai(0.9m);
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 10;

      Assert.Equal(CourseQualityStatus.NotVerified, Assess(listing, ai: ai).Status);
    }

    [Theory]
    [InlineData(CourseQualityLinkFailure.Timeout)]
    [InlineData(CourseQualityLinkFailure.AccessBlocked)]
    [InlineData(CourseQualityLinkFailure.ServerError)]
    public void InconclusiveLink_StillGoesToReview(CourseQualityLinkFailure failure)
    {
      var link = CourseQualityLinkCheckResult.Inconclusive(failure);

      var result = CourseQualityEngine.Assess(CleanListing(), link, null, Options, Now, out var fresh);

      Assert.False(fresh.DefiniteFailure);
      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Null(result.FixNote);
    }

    [Fact]
    public void CleanRuleOnlyListing_StillGoesToReview()
    {
      var result = CourseQualityEngine.Assess(CleanListing(), WorkingLink(null), null, Options, Now, out var fresh);

      Assert.False(fresh.DefiniteFailure);
      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
    }

    [Fact]
    public void ProviderHardGateFailure_IsNotDefinite_AndStillNeedsConfidence()
    {
      var ai = Ai(0.5m);
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 10;

      var result = CourseQualityEngine.Assess(CleanListing(), WorkingLink(), ai, Options, Now, out var fresh);

      Assert.False(fresh.DefiniteFailure);
      Assert.True(result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).HardGateFailed);
      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
    }

    [Fact]
    public void RuleFailureOnACriterionThatIsNotAGate_IsNotDefinite()
    {
      var options = new CourseQualityOptions();
      options.Criteria[CourseQualityCriterion.LinkWorks].HardGate = false;

      var result = CourseQualityEngine.Assess(CleanListing(), CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound), null, options, Now, out var fresh);

      Assert.False(fresh.DefiniteFailure);
      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
    }

    [Fact]
    public void MissingCardData_IsNotDefinite()
    {
      var listing = CleanListing();
      listing.CommitmentInterval = null;
      listing.CommitmentIntervalCount = null;

      CourseQualityEngine.Assess(listing, WorkingLink(), null, Options, Now, out var fresh);

      Assert.False(fresh.DefiniteFailure);
    }
    #endregion

    #region Hard gates
    [Theory]
    [InlineData(CourseQualityCriterion.ListingLive)]
    [InlineData(CourseQualityCriterion.LinkWorks)]
    [InlineData(CourseQualityCriterion.DatesAccurate)]
    public void HardGateFailure_CapsScore(CourseQualityCriterion criterion)
    {
      var opportunity = CleanListing();
      var link = WorkingLink();
      if (criterion == CourseQualityCriterion.ListingLive) opportunity.Status = Status.Expired;
      if (criterion == CourseQualityCriterion.LinkWorks) link = CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.DomainNotFound);
      if (criterion == CourseQualityCriterion.DatesAccurate) opportunity.DateEnd = opportunity.DateStart.AddDays(-1);

      var result = Assess(opportunity, link, Ai());

      Assert.True(result.Criteria.Single(o => o.Criterion == criterion).HardGateFailed);
      Assert.Equal(Options.HardGateScoreCap, result.Score);
    }

    [Fact]
    public void AICannotClearFailedHardGate()
    {
      var opportunity = CleanListing();
      opportunity.Hidden = true;

      var result = Assess(opportunity, ai: Ai(1m, 100));

      var criterion = result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.ListingLive);
      Assert.True(criterion.HardGateFailed);
      Assert.Equal(0, criterion.Score);
      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
    }

    [Fact]
    public void AICanFailHardGate_WhenItSawTheSourcePage()
    {
      var ai = Ai();
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 10;

      var result = Assess(ai: ai);

      Assert.True(result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).HardGateFailed);
      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
    }

    [Fact]
    public void AICannotFailHardGate_WithoutTheSourcePage()
    {
      var ai = Ai();
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 10;

      var result = Assess(link: WorkingLink(null), ai: ai);

      Assert.False(result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).HardGateFailed);
    }

    [Fact]
    public void DataComplete_IsNotAHardGate_ByDefault()
    {
      var opportunity = CleanListing();
      opportunity.Skills = null;
      opportunity.Description = "short";

      var result = Assess(opportunity, ai: Ai());

      var criterion = result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DataComplete);
      Assert.False(criterion.HardGateFailed);
      Assert.Equal(100, criterion.Score); // neither field is on the learner card
    }
    #endregion

    #region Rule-only and inconclusive
    [Fact]
    public void RuleOnly_CleanListing_GoesToReviewWithCappedConfidence()
    {
      var result = Assess();

      Assert.Equal(Options.RuleOnlyConfidenceCap, result.Confidence);
      Assert.False(result.AIUsed);
      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
    }

    [Theory]
    [InlineData(CourseQualityLinkFailure.Timeout, null)]
    [InlineData(CourseQualityLinkFailure.AccessBlocked, 403)]
    [InlineData(CourseQualityLinkFailure.RateLimited, 429)]
    [InlineData(CourseQualityLinkFailure.ServerError, 503)]
    public void InconclusiveLink_LowersConfidence_AndIsNotAHardGateFailure(CourseQualityLinkFailure failure, int? statusCode)
    {
      var result = Assess(link: CourseQualityLinkCheckResult.Inconclusive(failure, statusCode: statusCode), ai: Ai(0.9m));

      var link = result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.LinkWorks);
      Assert.False(link.HardGateFailed);
      Assert.Equal(Options.InconclusiveLinkScore, link.Score);
      Assert.Equal(0.39m, result.Confidence); // no page evidence: capped at 0.69, then the 0.3 penalty
      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
    }

    [Fact]
    public void InconclusiveLink_AICannotRaiseOrFailIt()
    {
      var low = Ai(0.95m, 0);
      var result = Assess(link: CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout), ai: low);

      var link = result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.LinkWorks);
      Assert.False(link.HardGateFailed);
      Assert.Equal(Options.InconclusiveLinkScore, link.Score);
    }
    #endregion

    #region Cost
    [Fact]
    public void Cost_WithoutAI_IsUnknown()
    {
      Assert.Equal(CourseCostType.Unknown, Assess().LearnerCard.Cost.Type);
    }

    [Theory]
    [InlineData(CourseCostType.Free, null)]
    [InlineData(CourseCostType.Free, "   ")]
    [InlineData(CourseCostType.Paid, "")]
    public void Cost_WithoutEvidence_IsForcedToUnknown(CourseCostType type, string? evidence)
    {
      var ai = Ai();
      ai.Cost = new LearnerCardCost { Type = type, Amount = 10, Currency = "USD", Evidence = evidence };

      var cost = Assess(ai: ai).LearnerCard.Cost;

      Assert.Equal(CourseCostType.Unknown, cost.Type);
      Assert.Null(cost.Amount);
      Assert.Null(cost.Currency);
      Assert.Null(cost.Evidence);
    }

    [Fact]
    public void Cost_WithEvidence_IsKept()
    {
      var ai = Ai();
      ai.Cost = new LearnerCardCost { Type = CourseCostType.Paid, Amount = 49, Currency = "USD", Evidence = "Enrol for $49" };

      var cost = Assess(ai: ai).LearnerCard.Cost;

      Assert.Equal(CourseCostType.Paid, cost.Type);
      Assert.Equal(49, cost.Amount);
      Assert.Equal("Enrol for $49", cost.Evidence);
    }
    #endregion

    #region Rubric is data
    [Fact]
    public void Weights_ComeFromConfiguration()
    {
      var options = new CourseQualityOptions();
      options.Criteria[CourseQualityCriterion.DataComplete].Weight = 65;
      var ai = Ai();
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DataComplete).Score = 50;

      Assert.Equal(93, Assess(ai: ai).Score); // (8500 + 15 * 50) / 100 = 92.5
      Assert.Equal(78, Assess(ai: ai, options: options).Score); // (8500 + 65 * 50) / 150 = 78.3
    }

    [Fact]
    public void AllZeroWeights_Throw()
    {
      var options = new CourseQualityOptions();
      foreach (var item in options.Criteria.Values) item.Weight = 0;

      Assert.Throws<InvalidOperationException>(() => Assess(options: options));
    }

    [Fact]
    public void HardGateFlag_ComesFromConfiguration()
    {
      var options = new CourseQualityOptions();
      options.Criteria[CourseQualityCriterion.LinkWorks].HardGate = false;

      var result = Assess(link: CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound), ai: Ai(), options: options);

      Assert.DoesNotContain(result.Criteria, o => o.HardGateFailed);
      Assert.Equal(80, result.Score);
    }
    #endregion

    #region Self-paced
    private static CourseQualityAIResult SelfPacedAi()
    {
      var ai = Ai();
      ai.Format = CourseFormat.Course;
      ai.SelfPaced = true;
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 60;
      return ai;
    }

    private static Domain.Opportunity.Models.Opportunity OpenEnded(DateTimeOffset start)
    {
      var listing = CleanListing();
      listing.DateEnd = null;
      listing.DateStart = start;
      return listing;
    }

    [Fact]
    public void SelfPaced_IsVerified_WithoutDatesConfirmed()
    {
      var result = Assess(OpenEnded(Now.AddYears(-3)), ai: SelfPacedAi());

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.True(result.LearnerCard.SelfPaced);
      Assert.False(result.LearnerCard.DatesConfirmed);
      Assert.Contains("Start anytime", result.LearnerCard.Highlights);
      Assert.DoesNotContain("Dates checked", result.LearnerCard.Highlights);
    }

    [Fact]
    public void SelfPaced_OldImportDate_IsNotAPlaceholder()
    {
      var result = Assess(OpenEnded(Now.AddYears(-20)), ai: SelfPacedAi());

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.DoesNotContain(result.Criteria, o => o.HardGateFailed);
    }

    [Fact]
    public void SelfPaced_TrulyInvalidDate_StillFails()
    {
      var result = Assess(OpenEnded(new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero)), ai: SelfPacedAi());

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.False(result.LearnerCard.SelfPaced);
    }

    [Fact]
    public void SelfPaced_EndBeforeStart_StillFails()
    {
      var listing = OpenEnded(Now.AddDays(-5));
      listing.DateEnd = Now.AddDays(-10);

      var result = Assess(listing, ai: SelfPacedAi());

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
    }

    [Fact]
    public void WithoutSelfPaced_OldDate_IsStillAPlaceholder()
    {
      var result = Assess(OpenEnded(Now.AddYears(-20)), ai: Ai());

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
    }

    [Fact]
    public void SelfPaced_InconclusiveLink_GoesToReview()
    {
      var result = Assess(OpenEnded(Now.AddYears(-3)), CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.AccessBlocked), SelfPacedAi());

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.False(result.LearnerCard.SelfPaced);
    }

    [Fact]
    public void SelfPaced_DeadLink_IsNotVerified()
    {
      var result = Assess(OpenEnded(Now.AddYears(-3)), CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound), SelfPacedAi());

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.False(result.LearnerCard.SelfPaced);
    }

    [Fact]
    public void SelfPaced_NotOpenYet_GoesToReview()
    {
      var result = Assess(OpenEnded(Now.AddDays(30)), ai: SelfPacedAi());

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.False(result.LearnerCard.SelfPaced);
    }

    [Fact]
    public void SelfPaced_UnknownFormat_GoesToReview()
    {
      var ai = SelfPacedAi();
      ai.Format = CourseFormat.Unknown;
      var listing = OpenEnded(Now.AddYears(-3));
      listing.Title = "Data Analysis";
      listing.Description = "Clean and present data.";
      listing.CommitmentInterval = Domain.Core.TimeIntervalOption.Hour;

      var result = Assess(listing, ai: ai);

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.False(result.LearnerCard.SelfPaced);
    }

    [Fact]
    public void SelfPaced_Verified_DatesAreNotSuspect_ButUnverifiedStayCounted()
    {
      var verified = Assess(OpenEnded(Now.AddYears(-3)), ai: SelfPacedAi()).ToRecord(Now, "f");
      var review = Assess(OpenEnded(Now.AddYears(-3)), CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.AccessBlocked), SelfPacedAi()).ToRecord(Now, "f");

      Assert.False(verified.DatesSuspect);
      Assert.True(verified.CardReady);
      Assert.True(review.DatesSuspect);
    }

    [Fact]
    public void SelfPaced_RejectedByAReviewer_FixNoteDoesNotAskForDates()
    {
      var result = Assess(OpenEnded(Now.AddYears(-3)), ai: SelfPacedAi());

      CourseQualityReviewPolicy.ApplyDecision(result, new CourseQualityReviewDecision { Status = CourseQualityStatus.NotVerified }, "r@example.org", Now, Options);

      Assert.NotNull(result.FixNote);
      Assert.DoesNotContain(result.FixNote.Issues, o => o.Criterion == CourseQualityCriterion.DatesAccurate);
    }
    #endregion

    #region Time-bound listings (C)
    private static CourseQualityAIResult Dated(bool start, bool end, CourseFormat format = CourseFormat.Course, bool selfPaced = false)
    {
      var ai = Ai();
      ai.Format = format;
      ai.DatesStartConfirmed = start;
      ai.DatesEndConfirmed = end;
      ai.SelfPaced = selfPaced;
      return ai;
    }

    private static Domain.Opportunity.Models.Opportunity Span(DateTimeOffset start, DateTimeOffset? end)
    {
      var listing = CleanListing();
      listing.DateStart = start;
      listing.DateEnd = end;
      return listing;
    }

    [Fact]
    public void Past_WebinarWithFarEndDate_AndNoConfirmedEnd_IsNotVerified()
    {
      var result = Assess(Span(new DateTimeOffset(2024, 3, 12, 0, 0, 0, TimeSpan.Zero), Now.AddYears(3)), ai: Dated(true, false, CourseFormat.Webinar));

      Assert.NotEqual(CourseQualityStatus.Verified, result.Status);
      Assert.False(result.LearnerCard.DatesConfirmed);
    }

    [Fact]
    public void Past_CohortWithFarEndDate_AndNoConfirmedEnd_IsNotVerified()
    {
      var result = Assess(Span(new DateTimeOffset(2024, 3, 12, 0, 0, 0, TimeSpan.Zero), Now.AddYears(3)), ai: Dated(true, false));

      Assert.NotEqual(CourseQualityStatus.Verified, result.Status);
    }

    [Fact]
    public void Past_Start_WithPageConfirmedFutureEnd_IsVerified()
    {
      Assert.Equal(CourseQualityStatus.Verified, Assess(Span(Now.AddDays(-10), Now.AddDays(30)), ai: Dated(true, true)).Status);
    }

    [Fact]
    public void Future_Start_ConfirmedByThePage_IsVerified()
    {
      Assert.Equal(CourseQualityStatus.Verified, Assess(Span(Now.AddDays(10), Now.AddDays(30)), ai: Dated(true, false)).Status);
    }

    [Fact]
    public void Future_Webinar_NeedsAConfirmedDate()
    {
      Assert.Equal(CourseQualityStatus.Verified, Assess(Span(Now.AddDays(10), null), ai: Dated(true, false, CourseFormat.Webinar)).Status);
      Assert.NotEqual(CourseQualityStatus.Verified, Assess(Span(Now.AddDays(10), null), ai: Dated(false, false, CourseFormat.Webinar)).Status);
    }

    [Fact]
    public void Unconfirmed_Dates_NeverVerify_WithoutSelfPaced()
    {
      Assert.NotEqual(CourseQualityStatus.Verified, Assess(Span(Now.AddDays(10), Now.AddDays(30)), ai: Dated(false, false)).Status);
      Assert.NotEqual(CourseQualityStatus.Verified, Assess(Span(Now.AddDays(-10), Now.AddDays(30)), ai: Dated(false, false)).Status);
    }

    [Fact]
    public void StartAnytime_NeverShows_OnAListingWithAnEndDate()
    {
      var result = Assess(Span(Now.AddYears(-1), Now.AddDays(5)), ai: Dated(false, false, selfPaced: true));

      Assert.False(result.LearnerCard.SelfPaced);
      Assert.DoesNotContain("Start anytime", result.LearnerCard.Highlights);
      Assert.NotEqual(CourseQualityStatus.Verified, result.Status);
    }
    #endregion

    #region Provider findings a person must settle
    [Fact]
    public void FormatDispute_GoesToReview_NotTheListingDetector()
    {
      var ai = Ai();
      ai.Format = CourseFormat.Unknown;
      ai.FormatDisputed = true;

      Assert.Equal(CourseQualityStatus.NeedsReview, Assess(ai: ai).Status);
    }

    [Fact]
    public void LowDataComplete_PreventsVerified_OnADatedListing()
    {
      var ai = Ai();
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DataComplete).Score = 10;

      Assert.NotEqual(CourseQualityStatus.Verified, Assess(ai: ai).Status);
    }
    #endregion

    #region Review findings
    // finding 4
    [Fact]
    public void DatedWebinarInThePast_NoEnd_IsNotVerifiedNorLive_NorDatesChecked()
    {
      var ai = Ai();
      ai.Format = CourseFormat.Webinar;
      var listing = OpenEnded(new DateTimeOffset(2024, 3, 12, 0, 0, 0, TimeSpan.Zero));

      var result = Assess(listing, ai: ai);

      Assert.NotEqual(CourseQualityStatus.Verified, result.Status);
      Assert.False(result.LearnerCard.DatesConfirmed);
      Assert.DoesNotContain("Dates checked", result.LearnerCard.Highlights);
      Assert.DoesNotContain("Listing is live", result.LearnerCard.Highlights);
    }

    // finding 4
    [Fact]
    public void DatedCourseInThePast_NoEnd_GoesToReview_WithoutDatesOrLiveClaims()
    {
      var result = Assess(OpenEnded(Now.AddYears(-1)), ai: Ai());

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.False(result.LearnerCard.DatesConfirmed);
      Assert.DoesNotContain("Dates checked", result.LearnerCard.Highlights);
      Assert.DoesNotContain("Listing is live", result.LearnerCard.Highlights);
    }

    // finding 4
    [Fact]
    public void ImportDate_WorksOnlyThroughTheSelfPacedPath()
    {
      Assert.Equal(CourseQualityStatus.Verified, Assess(OpenEnded(Now.AddYears(-3)), ai: SelfPacedAi()).Status);
      Assert.NotEqual(CourseQualityStatus.Verified, Assess(OpenEnded(Now.AddYears(-3)), ai: Ai()).Status);
    }

    // finding 6
    [Fact]
    public void PageGone_IsDefiniteForAReviewerDecision_ButRoutesToReview()
    {
      var ai = Ai();
      ai.PageGone = true;

      var result = CourseQualityEngine.Assess(CleanListing(), WorkingLink(), ai, Options, Now, out var fresh);

      Assert.True(fresh.DefiniteFailure || fresh.PageGone);
      Assert.NotEqual(CourseQualityStatus.Verified, result.Status);
    }

    // finding 8
    [Fact]
    public void ConfidenceAlone_NeverVerifies_WithoutTheProviderJudgingThePage()
    {
      var empty = Ai();
      empty.Criteria.Clear();
      var partial = Ai();
      partial.Criteria.RemoveAll(o => o.Criterion == CourseQualityCriterion.DataComplete);

      Assert.NotEqual(CourseQualityStatus.Verified, Assess(ai: empty).Status);
      Assert.NotEqual(CourseQualityStatus.Verified, Assess(ai: partial).Status);
    }
    #endregion

    #region Learner card
    [Fact]
    public void Card_DatesAreConfirmed_OnlyWhenAPageConfirmedThem()
    {
      var result = Assess(link: WorkingLink(), ai: Ai());

      Assert.True(result.LearnerCard.DatesConfirmed);
      Assert.Contains("Dates checked", result.LearnerCard.Highlights);
    }

    [Fact]
    public void Card_DatesAreNotConfirmed_WithoutAPageToCompare()
    {
      var unread = Assess(link: WorkingLink(), ai: null);
      var blocked = Assess(link: CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.AccessBlocked), ai: null);

      Assert.False(unread.LearnerCard.DatesConfirmed);
      Assert.False(blocked.LearnerCard.DatesConfirmed);
      Assert.DoesNotContain("Dates checked", unread.LearnerCard.Highlights);
      Assert.DoesNotContain("Dates checked", blocked.LearnerCard.Highlights);
    }

    [Fact]
    public void Card_DatesAreNotConfirmed_WhenTheProviderDidNotJudgeThem()
    {
      var ai = Ai();
      ai.Criteria.RemoveAll(o => o.Criterion == CourseQualityCriterion.DatesAccurate);

      var result = Assess(link: WorkingLink(), ai: ai);

      Assert.False(result.LearnerCard.DatesConfirmed);
    }

    [Fact]
    public void Card_StatesOnlyPassedChecks_InPlainWords()
    {
      var result = Assess(link: CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound), ai: Ai());

      Assert.DoesNotContain("Course link works", result.LearnerCard.Highlights);
      Assert.Contains("Listing is live", result.LearnerCard.Highlights);
      Assert.Equal("4 weeks", result.LearnerCard.TimelineSummary);
    }

    [Fact]
    public void Inactive_Organization_FailsListingLive()
    {
      var opportunity = CleanListing();
      opportunity.OrganizationStatus = OrganizationStatus.Inactive;

      Assert.True(Assess(opportunity, ai: Ai()).Criteria.Single(o => o.Criterion == CourseQualityCriterion.ListingLive).HardGateFailed);
    }
    #endregion
  }
}
