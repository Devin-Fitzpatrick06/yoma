using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityDatesAndFormatTests
  {
    private static readonly CourseQualityOptions Options = new();

    private static CourseQualityAssessment Assess(Domain.Opportunity.Models.Opportunity o, CourseQualityLinkCheckResult? link = null, CourseQualityAIResult? ai = null) =>
      CourseQualityEngine.Assess(o, link ?? WorkingLink(), ai, Options, Now);

    private static CourseQualityCriterionResult Criterion(CourseQualityAssessment assessment, CourseQualityCriterion criterion) =>
      assessment.Criteria.Single(o => o.Criterion == criterion);

    #region Real dates
    public static TheoryData<string> PlaceholderStarts =>
    [
      "0001-01-01T00:00:00+00:00",
      "1900-01-01T00:00:00+00:00",
      "1970-01-01T00:00:00+00:00",
      "2000-01-01T00:00:00+00:00",
      "2099-12-31T00:00:00+00:00",
      "9999-12-31T00:00:00+00:00"
    ];

    [Theory]
    [MemberData(nameof(PlaceholderStarts))]
    public void PlaceholderStartDate_FailsTheGate(string value)
    {
      var opportunity = CleanListing();
      opportunity.DateStart = DateTimeOffset.Parse(value);
      opportunity.DateEnd = null;

      var result = Assess(opportunity, ai: Ai());

      Assert.True(Criterion(result, CourseQualityCriterion.DatesAccurate).HardGateFailed);
      Assert.Equal(0, Criterion(result, CourseQualityCriterion.DatesAccurate).Score);
      Assert.Equal(Options.HardGateScoreCap, result.Score);
      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
    }

    [Theory]
    [InlineData("2099-12-31T00:00:00+00:00")]
    [InlineData("9999-12-31T23:59:59+00:00")]
    public void PlaceholderEndDate_FailsTheGate(string value)
    {
      var opportunity = CleanListing();
      opportunity.DateEnd = DateTimeOffset.Parse(value);

      Assert.True(Criterion(Assess(opportunity, ai: Ai()), CourseQualityCriterion.DatesAccurate).HardGateFailed);
    }

    [Fact]
    public void EndBeforeStart_FailsTheGate()
    {
      var opportunity = CleanListing();
      opportunity.DateEnd = opportunity.DateStart.AddDays(-1);

      Assert.True(Criterion(Assess(opportunity, ai: Ai()), CourseQualityCriterion.DatesAccurate).HardGateFailed);
    }

    [Fact]
    public void DatesAtThePlausibleLimits_Pass()
    {
      var opportunity = CleanListing();
      opportunity.DateStart = Now.AddYears(-Options.DatePlausibleYearsPast);
      opportunity.DateEnd = Now.AddYears(Options.DatePlausibleYearsFuture);

      Assert.False(Criterion(Assess(opportunity, ai: Ai()), CourseQualityCriterion.DatesAccurate).HardGateFailed);
    }

    [Fact]
    public void NoEndDate_IsAllowed()
    {
      var opportunity = CleanListing();
      opportunity.DateEnd = null;
      opportunity.DateStart = Now.AddDays(10);

      var result = Assess(opportunity, ai: Ai());

      Assert.Equal(100, Criterion(result, CourseQualityCriterion.DatesAccurate).Score);
      Assert.Equal(CourseQualityStatus.Verified, result.Status);
    }

    [Fact]
    public void NoEndDate_WhenTheSourcePageDisagrees_GoesToReview_NotAHardFailure()
    {
      var opportunity = CleanListing();
      opportunity.DateEnd = null;
      var ai = Ai(0.95m);
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 10;

      var result = Assess(opportunity, ai: ai);

      Assert.False(Criterion(result, CourseQualityCriterion.DatesAccurate).HardGateFailed);
      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Equal(CourseQualityReviewStatus.Pending, result.ReviewStatus);
    }

    [Fact]
    public void EndDatePresent_WhenTheSourcePageDisagrees_StillFailsTheGate()
    {
      var ai = Ai(0.95m);
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 10;

      Assert.True(Criterion(Assess(CleanListing(), ai: ai), CourseQualityCriterion.DatesAccurate).HardGateFailed);
    }
    #endregion

    #region Format
    private static Domain.Opportunity.Models.Opportunity Listing(string title, string description = "An introduction with short exercises for new learners.",
      TimeIntervalOption interval = TimeIntervalOption.Hour, short count = 10, EngagementTypeOption? engagement = EngagementTypeOption.Remote)
    {
      var result = CleanListing();
      result.Title = title;
      result.Description = description;
      result.CommitmentInterval = interval;
      result.CommitmentIntervalCount = count;
      result.EngagementType = engagement;
      return result;
    }

    public static TheoryData<string, string, TimeIntervalOption, short, EngagementTypeOption?, CourseFormat> FormatCases => new()
    {
      // title wins over the description
      { "Budgeting Webinar", "A short session.", TimeIntervalOption.Hour, 1, EngagementTypeOption.Remote, CourseFormat.Webinar },
      { "Budgeting Course", "Starts with a webinar.", TimeIntervalOption.Hour, 10, EngagementTypeOption.Remote, CourseFormat.Course },
      // description decides when the title is silent
      { "Budgeting Basics", "Join this live webinar.", TimeIntervalOption.Hour, 1, EngagementTypeOption.Remote, CourseFormat.Webinar },
      { "Budgeting Basics", "A self-paced course with quizzes.", TimeIntervalOption.Hour, 10, EngagementTypeOption.Remote, CourseFormat.Course },
      // a long commitment alone rules out a single session
      { "Budgeting Basics", "Learn to plan.", TimeIntervalOption.Week, 4, EngagementTypeOption.Remote, CourseFormat.Course },
      // mixed wording is settled by the commitment, and stays unknown otherwise
      { "Course webinar", "Mixed.", TimeIntervalOption.Hour, 2, EngagementTypeOption.Remote, CourseFormat.Webinar },
      { "Course webinar", "Mixed.", TimeIntervalOption.Month, 2, EngagementTypeOption.Remote, CourseFormat.Course },
      { "Course webinar", "Mixed.", TimeIntervalOption.Hour, 10, EngagementTypeOption.Remote, CourseFormat.Unknown },
      // contradictions are not guessed through
      { "Budgeting Webinar", "In person.", TimeIntervalOption.Hour, 1, EngagementTypeOption.OnSite, CourseFormat.Unknown },
      { "Budgeting Webinar", "Runs for a month.", TimeIntervalOption.Month, 1, EngagementTypeOption.Remote, CourseFormat.Unknown },
      // nothing to go on: a short listing is not assumed to be a webinar
      { "Budgeting Basics", "Learn to plan.", TimeIntervalOption.Hour, 1, EngagementTypeOption.Remote, CourseFormat.Unknown },
      { "Budgeting Basics", "Of course you can learn to plan.", TimeIntervalOption.Hour, 2, null, CourseFormat.Unknown }
    };

    [Theory]
    [MemberData(nameof(FormatCases))]
    public void RuleDetection(string title, string description, TimeIntervalOption interval, short count, EngagementTypeOption? engagement, CourseFormat expected)
    {
      var result = Assess(Listing(title, description, interval, count, engagement));

      Assert.Equal(expected, result.LearnerCard.Format);
    }

    [Fact]
    public void Detection_StaysFast_OnHostileDescriptions()
    {
      var opportunity = CleanListing();
      opportunity.Title = "Budgeting Basics";
      opportunity.Description = new string(' ', 400_000) + "course";

      var watch = System.Diagnostics.Stopwatch.StartNew();
      var format = Domain.CourseQuality.Helpers.CourseQualityFormatDetector.Detect(opportunity, Options);
      watch.Stop();

      Assert.Equal(CourseFormat.Course, format);
      Assert.True(watch.ElapsedMilliseconds < 1000, $"took {watch.ElapsedMilliseconds}ms");
    }

    [Theory]
    [InlineData(CourseFormat.Course)]
    [InlineData(CourseFormat.Webinar)]
    public void AIFormat_IsPreferred(CourseFormat format)
    {
      var ai = Ai();
      ai.Format = format;

      Assert.Equal(format, Assess(Listing("Budgeting Webinar", count: 1), ai: ai).LearnerCard.Format);
      Assert.Equal(format, Assess(Listing("Budgeting Course"), ai: ai).LearnerCard.Format);
    }

    [Fact]
    public void AIUnknown_FallsBackToTheRules()
    {
      Assert.Equal(CourseFormat.Webinar, Assess(Listing("Budgeting Webinar", count: 1), ai: Ai()).LearnerCard.Format);
    }

    [Fact]
    public void KnownFormat_Passes()
    {
      var result = Assess(Listing("Budgeting Course"), ai: Ai());

      Assert.Equal(100, Criterion(result, CourseQualityCriterion.FormatIdentified).Score);
      Assert.Contains("Format confirmed", result.LearnerCard.Highlights);
    }

    [Fact]
    public void UnknownFormat_ScoresLow_AndGoesToReview_EvenWhenOtherwiseVerified()
    {
      var result = Assess(Listing("Budgeting Basics", count: 1), ai: Ai(0.99m));

      var criterion = Criterion(result, CourseQualityCriterion.FormatIdentified);
      Assert.Equal(CourseFormat.Unknown, result.LearnerCard.Format);
      Assert.Equal(Options.FormatUnknownScore, criterion.Score);
      Assert.False(criterion.HardGateFailed);
      Assert.NotNull(criterion.Recommendation);
      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
    }

    [Fact]
    public void AICriterionScore_CannotRaiseAnUnknownFormat()
    {
      var result = Assess(Listing("Budgeting Basics", count: 1), ai: Ai(0.99m, 100));

      Assert.Equal(Options.FormatUnknownScore, Criterion(result, CourseQualityCriterion.FormatIdentified).Score);
    }
    #endregion

    #region Data the card needs
    [Fact]
    public void DataComplete_RequiresOnlyWhatTheCardNeeds()
    {
      var opportunity = CleanListing();
      opportunity.Skills = null;
      opportunity.Languages = null;
      opportunity.Countries = null;
      opportunity.URL = null;

      Assert.Equal(100, Criterion(Assess(opportunity, ai: Ai()), CourseQualityCriterion.DataComplete).Score);
    }

    [Fact]
    public void DataComplete_MissingCommitment_IsReported()
    {
      var opportunity = Listing("Budgeting Course");
      opportunity.CommitmentInterval = null;
      opportunity.CommitmentIntervalCount = null;

      var criterion = Criterion(Assess(opportunity), CourseQualityCriterion.DataComplete);

      Assert.Equal(33, criterion.Score); // only the start date of the three card fields
      Assert.Contains("time commitment", criterion.Recommendation);
    }

    [Fact]
    public void DataComplete_TimelineFromTheSourcePage_Counts()
    {
      var opportunity = Listing("Budgeting Course");
      opportunity.CommitmentInterval = null;
      opportunity.CommitmentIntervalCount = null;
      var ai = Ai();
      ai.TimelineSummary = "About six weeks";

      Assert.Equal(67, Criterion(Assess(opportunity, ai: ai), CourseQualityCriterion.DataComplete).Score);
    }

    [Fact]
    public void DataComplete_PlaceholderStart_IsMissing()
    {
      var opportunity = CleanListing();
      opportunity.DateStart = default;
      opportunity.DateEnd = null;

      Assert.Equal(67, Criterion(Assess(opportunity), CourseQualityCriterion.DataComplete).Score);
    }

    [Fact]
    public void DataComplete_UnknownCost_IsNotAGap()
    {
      Assert.Equal(100, Criterion(Assess(CleanListing()), CourseQualityCriterion.DataComplete).Score);
    }
    #endregion

    #region Keyless reader
    private static CourseQualityAIResult Reader(decimal confidence = 0.8m)
    {
      var result = Ai(confidence);
      result.IsAI = false;
      result.Model = "source-page-reader";
      return result;
    }

    [Fact]
    public void ReaderResult_CanEarnVerified_WithoutAI()
    {
      var result = Assess(CleanListing(), ai: Reader());

      Assert.Equal(0.8m, result.Confidence);
      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.False(result.AIUsed);
      Assert.Equal("source-page-reader", result.AIModel);
    }

    [Fact]
    public void AIUsed_FollowsIsAI()
    {
      Assert.True(Assess(CleanListing(), ai: Ai()).AIUsed);
    }

    [Fact]
    public void ReaderCost_IsUsed_WithEvidence()
    {
      var reader = Reader();
      reader.Cost = new LearnerCardCost { Type = CourseCostType.Free, Evidence = "This course is free" };

      Assert.Equal(CourseCostType.Free, Assess(CleanListing(), ai: reader).LearnerCard.Cost.Type);
    }

    [Fact]
    public void NullResult_CapsConfidence()
    {
      Assert.Equal(Options.RuleOnlyConfidenceCap, Assess(CleanListing()).Confidence);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NoPageEvidence_CapsConfidence_EvenWithAResult(bool isAI)
    {
      var result = Ai(0.95m);
      result.IsAI = isAI;

      Assert.Equal(Options.RuleOnlyConfidenceCap, Assess(CleanListing(), WorkingLink(null), result).Confidence);
      Assert.Equal(Options.RuleOnlyConfidenceCap, Assess(CleanListing(), CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound), result).Confidence);
    }

    [Fact]
    public void NoPageEvidence_KeepsALowerResultConfidence()
    {
      Assert.Equal(0.4m, Assess(CleanListing(), WorkingLink(null), Ai(0.4m)).Confidence);
    }

    [Fact]
    public void ReaderCannotClearAFailedHardGate()
    {
      var opportunity = CleanListing();
      opportunity.Hidden = true;

      var result = Assess(opportunity, ai: Reader(1m));

      Assert.True(Criterion(result, CourseQualityCriterion.ListingLive).HardGateFailed);
      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
    }
    #endregion
  }
}
