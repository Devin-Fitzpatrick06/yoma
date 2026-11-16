using Xunit;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityReviewReasonsTests
  {
    private static readonly CourseQualityOptions Options = new();

    private static CourseQualityAssessment Assess(Domain.Opportunity.Models.Opportunity? o = null, CourseQualityLinkCheckResult? link = null, CourseQualityAIResult? ai = null) =>
      CourseQualityEngine.Assess(o ?? CleanListing(), link ?? WorkingLink(), ai, Options, Now);

    [Fact]
    public void Verified_HasNoReasons()
    {
      var result = Assess(ai: Ai());

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.Empty(result.ReviewReasons);
    }

    [Fact]
    public void UnreadablePage_SaysConfidenceIsCapped()
    {
      var result = Assess(link: WorkingLink(null), ai: null);

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Contains("The course page could not be read, so confidence is capped at 0.69.", result.ReviewReasons);
      Assert.Contains("Confidence is 0.69, below the review threshold of 0.7.", result.ReviewReasons);
    }

    [Fact]
    public void InconclusiveLink_SaysWhatHappened_AndThePenalty()
    {
      var result = Assess(link: CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout), ai: Ai());

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Contains("The course link could not be confirmed either way (it timed out), so confidence was lowered by 0.3.", result.ReviewReasons);
      Assert.DoesNotContain(result.ReviewReasons, o => o.Contains("could not be read", StringComparison.Ordinal));
    }

    [Fact]
    public void WeakTitleMatch_IsNamed()
    {
      var ai = Ai(0.4m);
      ai.StrongMatch = false;

      var result = Assess(ai: ai);

      Assert.Contains("The course page's title does not clearly match the listing title.", result.ReviewReasons);
    }

    [Fact]
    public void UnknownAndDisputedFormat_AreNamed()
    {
      var listing = CleanListing();
      listing.Title = "Data";
      listing.Description = "Data.";
      listing.CommitmentInterval = null;
      var ai = Ai();
      ai.FormatDisputed = true;

      var result = Assess(listing, ai: ai);

      Assert.Equal(CourseFormat.Unknown, result.LearnerCard.Format);
      Assert.Contains("The format, course or webinar, could not be told from the listing or the page.", result.ReviewReasons);
      Assert.Contains("The page reader and the fixed checks read a different format.", result.ReviewReasons);
    }

    [Fact]
    public void DatesThatDiffer_AreNamed()
    {
      // confident, it would be a failed gate and a fix note; unsure, a person compares the dates
      var ai = Ai(0.5m);
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 20;

      var result = Assess(ai: ai);

      Assert.Contains("The dates on the course page differ from the listing.", result.ReviewReasons);
    }

    [Fact]
    public void OpenEndedListing_WithDifferentPageDates_IsNamed()
    {
      var listing = CleanListing();
      listing.DateEnd = null;
      var ai = Ai();
      ai.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score = 20;

      var result = Assess(listing, ai: ai);

      Assert.Contains("The listing has no end date, and the dates on the course page differ from it.", result.ReviewReasons);
    }

    [Fact]
    public void UnconfirmedDates_AreNamed()
    {
      var ai = Ai();
      ai.DatesStartConfirmed = false;
      ai.DatesEndConfirmed = false;

      var result = Assess(ai: ai);

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Contains("The course page does not confirm the listing's dates, and the course is not self-paced.", result.ReviewReasons);
    }

    [Fact]
    public void DefiniteFailure_HasNoReasons()
    {
      var result = Assess(link: CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, statusCode: 404), ai: null);

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.Empty(result.ReviewReasons);
    }

    [Fact]
    public void Reasons_SurviveTheStoredRow_AndAReview()
    {
      var result = Assess(link: WorkingLink(null), ai: null);
      var record = result.ToRecord(Now, new string('a', 64));

      Assert.NotNull(record.ReviewReasons);
      Assert.Equal(result.ReviewReasons, record.ToAssessment().ReviewReasons);

      var reviewed = record.ToAssessment();
      CourseQualityReviewPolicy.ApplyDecision(reviewed, new CourseQualityReviewDecision { Status = CourseQualityStatus.NotVerified, Format = CourseFormat.Course }, "r@yoma.world", Now, Options);
      Assert.Equal(result.ReviewReasons, record.ApplyAssessment(reviewed).ToAssessment().ReviewReasons);
    }

    [Fact]
    public void OldRows_WithoutReasons_ReadAsEmpty()
    {
      var record = Assess(ai: Ai()).ToRecord(Now, new string('a', 64));
      record.ReviewReasons = null;

      Assert.Empty(record.ToAssessment().ReviewReasons);
    }

    [Fact]
    public void StoredCard_NeverCarriesAGrade()
    {
      var assessment = Assess(ai: Ai());
      assessment.LearnerCard.Grade = CourseGrade.A;
      assessment.LearnerCard.GradeLabel = "Top pick";

      var card = assessment.ToRecord(Now, new string('a', 64)).ToAssessment().LearnerCard;

      Assert.Null(card.Grade);
      Assert.Null(card.GradeLabel);
    }
  }
}
