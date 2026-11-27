using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Models;
using Yoma.Core.Domain.PartnerSync.Models;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityJobTests
  {
    private static readonly CourseQualityOptions Options = new() { Jobs = new() { Enabled = true } };

    // shaped on a live Jobberman listing: markdown headings, entities, a salary in naira
    private const string JobbermanDescription =
      "**Responsibilities:** &#x20; - Manage and maintain the company’s financial records accurately. &#x20; - Perform regular account reconciliation. " +
      "**Requirements:** - B.Sc in Accounting with 2 years experience. **Salary:** ₦150,000 - ₦250,000 monthly. Full-time, based in Lagos.";

    private static Opportunity FeedJob(string? description = null, SyncPartner partner = SyncPartner.Jobberman) => new()
    {
      Id = Guid.NewGuid(),
      Title = "Accountant at InnovateOne Company",
      Description = description ?? JobbermanDescription,
      Type = Domain.Opportunity.Type.Job,
      OrganizationId = Guid.NewGuid(),
      OrganizationName = "Jobberman Nigeria",
      OrganizationStatus = OrganizationStatus.Active,
      Status = Status.Active,
      URL = "https://www.jobberman.com/listings/accountant-45pe8m",
      DateStart = Now.AddDays(-10),
      DateModified = Now.AddDays(-10),
      Countries = [new OpportunityCountryInfo()],
      SyncedInfo = new SyncInfoEntity
      {
        SyncType = SyncType.Pull,
        Partners = [new SyncInfoEntityPartner { Partner = partner, EntityType = Domain.PartnerSync.EntityType.Opportunity }]
      }
    };

    private static Opportunity PostedJob()
    {
      var job = FeedJob();
      job.SyncedInfo = null;
      job.OrganizationName = "Local Employer";
      return job;
    }

    private static CourseQualityLinkCheckResult PostedLink() =>
      CourseQualityLinkCheckResult.Reachable(200, "https://careers.example.org/accountant", "page");

    private static CourseQualityAssessment Assess(Opportunity job, CourseQualityLinkCheckResult? link = null, CourseQualityOptions? options = null) =>
      CourseQualityEngine.AssessJob(job, link, options ?? Options, Now, out _);

    private static CourseQualityCriterionResult Criterion(CourseQualityAssessment assessment, CourseQualityCriterion criterion) =>
      assessment.Criteria.Single(o => o.Criterion == criterion);

    #region Routing
    [Fact]
    public void FeedJob_WithClearDetails_IsVerifiedWithoutALinkCheck()
    {
      var result = CourseQualityEngine.AssessJob(FeedJob(), null, Options, Now, out var fresh);

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.Equal(CourseQualityReviewStatus.NotRequired, result.ReviewStatus);
      Assert.Equal(100, result.Score);
      Assert.Equal(CourseFormat.Job, result.LearnerCard.Format);
      Assert.Equal("jobs-v1", result.RubricVersion);
      Assert.Contains("Jobberman feed still lists this job", Criterion(result, CourseQualityCriterion.JobOpen).Reason);
      Assert.True(fresh.HttpSuccess);
      Assert.Equal("feed:jobberman", fresh.FinalHost);
      Assert.Null(result.FixNote);
    }

    [Fact]
    public void JobChecks_NeverIncludeCourseCriteria_AndSkipAgeForFeedJobs()
    {
      Assert.Equal(
        [CourseQualityCriterion.JobOpen, CourseQualityCriterion.NoScamSignals, CourseQualityCriterion.JobDetailsClear],
        Assess(FeedJob()).Criteria.Select(o => o.Criterion));

      Assert.Equal(
        [CourseQualityCriterion.JobOpen, CourseQualityCriterion.NoScamSignals, CourseQualityCriterion.JobDetailsClear, CourseQualityCriterion.JobFresh],
        Assess(PostedJob(), PostedLink()).Criteria.Select(o => o.Criterion));
    }

    [Fact]
    public void ApplicationFee_HoldsTheJobForAPerson_AndQuotesIt()
    {
      var result = Assess(FeedJob(JobbermanDescription + " Shortlisted candidates must pay an application fee of R350 before the interview."));

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Equal(CourseQualityReviewStatus.Pending, result.ReviewStatus);
      var scam = Criterion(result, CourseQualityCriterion.NoScamSignals);
      Assert.True(scam.HardGateFailed);
      Assert.Equal(0, scam.Score);
      Assert.Contains(result.ReviewReasons, o => o.Contains("pay a fee") && o.Contains("application fee"));
      Assert.Equal(CourseQualityStatus.NotVerified, CourseQualityEngine.ProposedStatus(result, Options));
    }

    [Theory]
    [InlineData("We will never charge an application fee.")]
    [InlineData("Jobberman does not ask candidates to pay a fee at any stage.")]
    [InlineData("No registration fee is required.")]
    public void ScamWarnings_InTheListing_AreNotScamSignals(string warning)
    {
      var result = Assess(FeedJob($"{JobbermanDescription} {warning}"));

      Assert.Equal(100, Criterion(result, CourseQualityCriterion.NoScamSignals).Score);
      Assert.Equal(CourseQualityStatus.Verified, result.Status);
    }

    [Theory]
    [InlineData("Send your CV and ID copy on WhatsApp to 082 555 1234.", "WhatsApp")]
    [InlineData("To be considered, provide your bank details and BVN.", "bank, ID or login")]
    [InlineData("Guaranteed job for every applicant, no interview needed.", "without a normal hiring process")]
    public void StrongScamSignals_GoToReview(string text, string reasonPart)
    {
      var result = Assess(FeedJob($"{JobbermanDescription} {text}"));

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Contains(result.ReviewReasons, o => o.Contains(reasonPart));
    }

    [Fact]
    public void PersonalEmail_LowersTheScoreButDoesNotHoldTheJob()
    {
      var result = Assess(FeedJob($"{JobbermanDescription} Send your CV to hiring.innovateone@gmail.com."));

      var scam = Criterion(result, CourseQualityCriterion.NoScamSignals);
      Assert.Equal(60, scam.Score);
      Assert.False(scam.HardGateFailed);
      Assert.Equal(CourseQualityStatus.Verified, result.Status); // (25 + 15 + 35) / 85 of the weight
      Assert.Equal(88, result.Score);
    }

    [Fact]
    public void ClosingDatePassed_IsNotVerified_WithAJobFixNote()
    {
      var job = FeedJob();
      job.DateEnd = Now.AddDays(-1);

      var result = Assess(job);

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.Equal(CourseQualityReviewStatus.NotRequired, result.ReviewStatus);
      Assert.NotNull(result.FixNote);
      Assert.StartsWith("This job is not verified yet", result.FixNote!.Summary);
      Assert.Contains(result.FixNote.Issues, o => o.Criterion == CourseQualityCriterion.JobOpen && o.Reason.Contains("closing date"));
    }

    [Fact]
    public void PostedJob_WithABrokenLink_IsNotVerified()
    {
      var result = CourseQualityEngine.AssessJob(PostedJob(), CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, statusCode: 404), Options, Now, out var fresh);

      Assert.Equal(CourseQualityStatus.NotVerified, result.Status);
      Assert.True(fresh.DefiniteFailure);
      Assert.Contains("does not open", Criterion(result, CourseQualityCriterion.JobOpen).Reason);
    }

    [Fact]
    public void PostedJob_WithAnUnconfirmedLink_GoesToReview()
    {
      var result = Assess(PostedJob(), CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout));

      Assert.Equal(CourseQualityStatus.NeedsReview, result.Status);
      Assert.Equal(0.6m, result.Confidence);
      Assert.Contains(result.ReviewReasons, o => o.Contains("timed out"));
    }

    [Fact]
    public void PostedJob_WithAWorkingLink_IsVerifiedOnItsHost()
    {
      var result = CourseQualityEngine.AssessJob(PostedJob(), CourseQualityLinkCheckResult.Reachable(200, "https://careers.example.org/accountant", "page"), Options, Now, out var fresh);

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.Equal("careers.example.org", fresh.FinalHost);
      Assert.Contains("link to apply works", Criterion(result, CourseQualityCriterion.JobOpen).Reason);
    }
    #endregion

    #region Details and freshness
    [Fact]
    public void FeedShellText_MissesPayScheduleAndRequirements()
    {
      // a live JobJack listing: company blurb only
      var job = FeedJob("Join Smollan, a company that empowers growth and development. Our Smollan Academy offers innovative learning experiences. **Salary:** Per Month, Market Related", SyncPartner.JobJack);
      job.Title = "Merchandiser - Smollan (Oudtshoorn)";

      var details = Criterion(Assess(job), CourseQualityCriterion.JobDetailsClear);

      Assert.Equal(40, details.Score);
      Assert.Equal("The listing does not state: pay amount, work schedule or contract type, requirements.", details.Reason);
      Assert.Equal("Add to the listing: pay amount, work schedule or contract type, requirements.", details.Recommendation);
    }

    [Fact]
    public void StructuredFields_CountAsDetails()
    {
      var job = FeedJob("Merchandiser role.");
      job.CustomFields =
      [
        new CustomFieldValueItem { Key = CustomFieldConstants.Job.Salary.Minimum, ValueRaw = "6000" },
        new CustomFieldValueItem { Key = CustomFieldConstants.Job.Employment.Schedule, ValueRaw = CustomFieldConstants.Job.Employment.ScheduleOptions.FullTime },
        new CustomFieldValueItem { Key = CustomFieldConstants.Job.Experience.Level, ValueRaw = CustomFieldConstants.Job.Experience.Options.EntryJunior }
      ];

      Assert.Equal(100, Criterion(Assess(job), CourseQualityCriterion.JobDetailsClear).Score);
    }

    [Theory]
    [InlineData(45, 100, false)]
    [InlineData(60, 83, false)]
    [InlineData(89, 51, false)]
    [InlineData(90, 0, true)]
    public void Freshness_FallsFromTheFreshLimitAndFailsWhenStale(int days, int score, bool failed)
    {
      var job = PostedJob();
      job.DateStart = Now.AddDays(-days);

      var fresh = Assess(job, PostedLink()).Criteria.Single(o => o.Criterion == CourseQualityCriterion.JobFresh);

      Assert.Equal(score, fresh.Score);
      Assert.False(fresh.HardGateFailed);
      Assert.Equal(failed, fresh.Recommendation?.StartsWith("Confirm the job is still open, or remove it") == true);
    }

    [Fact]
    public void StalePostedJob_WithFullDetails_StaysVerified()
    {
      var job = PostedJob();
      job.DateStart = Now.AddDays(-120);

      var result = Assess(job, PostedLink());

      Assert.Equal(85, result.Score);
      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.DoesNotContain("Recently posted", result.LearnerCard.Highlights);
    }

    [Fact]
    public void OldFeedJob_IsNotMarkedDownForAge()
    {
      var job = FeedJob();
      job.DateStart = Now.AddDays(-300);

      var result = Assess(job);

      Assert.Equal(100, result.Score);
      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.DoesNotContain(result.Criteria, o => o.Criterion == CourseQualityCriterion.JobFresh);
    }
    #endregion

    #region Outputs and review
    [Fact]
    public void Highlights_DescribeTheJobChecks()
    {
      Assert.Equal(["Job is open", "No scam warning signs", "Pay and key details stated"], Assess(FeedJob()).LearnerCard.Highlights);
      Assert.Equal(["Job is open", "No scam warning signs", "Pay and key details stated", "Recently posted"], Assess(PostedJob(), PostedLink()).LearnerCard.Highlights);
    }

    [Fact]
    public void ReviewerVerifyingAHeldJob_OverridesWithoutAFormat()
    {
      var result = Assess(FeedJob(JobbermanDescription + " Pay a small fee to secure your interview slot."));

      CourseQualityReviewPolicy.ApplyDecision(result, new CourseQualityReviewDecision { Status = CourseQualityStatus.Verified }, "reviewer@example.org", Now, Options);

      Assert.Equal(CourseQualityStatus.Verified, result.Status);
      Assert.Equal(CourseQualityReviewStatus.Overridden, result.ReviewStatus);
      Assert.Equal(CourseFormat.Job, result.LearnerCard.Format);
      Assert.Contains("Reviewed by the Yoma team", result.LearnerCard.Highlights);
    }

    [Fact]
    public void JobFormat_CannotBeChangedOrGivenToACourse()
    {
      var job = Assess(FeedJob());
      CourseQualityEngine.ApplyFormat(job, CourseFormat.Job, Options);
      Assert.Equal(CourseFormat.Job, job.LearnerCard.Format);
      Assert.Throws<InvalidOperationException>(() => CourseQualityEngine.ApplyFormat(job, CourseFormat.Course, Options));

      var course = CourseQualityEngine.Assess(CleanListing(), WorkingLink(), Ai(), Options, Now);
      Assert.Throws<InvalidOperationException>(() => CourseQualityEngine.ApplyFormat(course, CourseFormat.Job, Options));
    }

    [Fact]
    public void Record_RoundTripsTheJobFormatAndCriteria()
    {
      var result = Assess(FeedJob());

      var record = result.ToRecord(Now, "fingerprint");
      var back = record.ToAssessment();

      Assert.Equal(CourseFormat.Job, record.Format);
      Assert.Equal(CourseFormat.Job, back.LearnerCard.Format);
      Assert.Equal(result.Criteria.Select(o => (o.Criterion, o.Score)), back.Criteria.Select(o => (o.Criterion, o.Score)));
    }

    [Fact]
    public void CourseScores_IgnoreTheJobWeights()
    {
      var withJobs = CourseQualityEngine.Assess(CleanListing(), WorkingLink(), Ai(score: 90), Options, Now);
      var courseOnly = new CourseQualityOptions();
      foreach (var key in new[] { CourseQualityCriterion.JobOpen, CourseQualityCriterion.JobDetailsClear, CourseQualityCriterion.JobFresh, CourseQualityCriterion.NoScamSignals })
        courseOnly.Criteria.Remove(key);

      Assert.Equal(CourseQualityEngine.Assess(CleanListing(), WorkingLink(), Ai(score: 90), courseOnly, Now).Score, withJobs.Score);
    }
    #endregion

    #region Calibration (off by default)
    /// <summary>
    /// Runs the job checks over a saved sample of live search results (a JSON array of Yoma opportunity search items) and
    /// writes what they would decide. Read-only: nothing is fetched or stored.
    /// </summary>
    [Fact]
    public void Calibration_SummarisesASavedJobSample()
    {
      var path = System.Environment.GetEnvironmentVariable("COURSE_QUALITY_JOB_SAMPLE");
      Assert.SkipWhen(string.IsNullOrEmpty(path), "COURSE_QUALITY_JOB_SAMPLE is not set");

      var items = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(path!));
      var now = DateTimeOffset.UtcNow;
      var results = items.Select(o =>
      {
        var partner = Enum.TryParse<SyncPartner>((string?)o["syncedInfo"]?["partners"]?[0]?["partner"], true, out var p) ? p : (SyncPartner?)null;
        var job = new Opportunity
        {
          Id = Guid.NewGuid(),
          Title = (string?)o["title"] ?? string.Empty,
          Description = (string?)o["description"] ?? string.Empty,
          Summary = (string?)o["summary"],
          Instructions = (string?)o["instructions"],
          Type = Domain.Opportunity.Type.Job,
          OrganizationName = (string?)o["organizationName"] ?? string.Empty,
          OrganizationStatus = OrganizationStatus.Active,
          Status = Status.Active,
          URL = (string?)o["url"],
          DateStart = (DateTimeOffset?)o["dateStart"] ?? now,
          DateEnd = (DateTimeOffset?)o["dateEnd"],
          Countries = [.. (o["countries"] as Newtonsoft.Json.Linq.JArray ?? []).Select(_ => new OpportunityCountryInfo())],
          SyncedInfo = partner.HasValue ? new SyncInfoEntity
          {
            SyncType = SyncType.Pull,
            Partners = [new SyncInfoEntityPartner { Partner = partner.Value, EntityType = Domain.PartnerSync.EntityType.Opportunity }]
          } : null
        };
        return (Organisation: job.OrganizationName, Job: job, Assessment: CourseQualityEngine.AssessJob(job, partner.HasValue ? null : WorkingLink(), Options, now, out _));
      }).ToList();

      // organisations by name, since the sample carries no organisation ids
      var organisations = results.Select(o => o.Organisation).Distinct().ToDictionary(o => o, _ => Guid.NewGuid());
      var grades = CourseQualityJobGradingEngine.Grade([.. results.Select(o => new JobGradeInput
      {
        OpportunityId = o.Assessment.OpportunityId,
        OpportunityTitle = o.Assessment.OpportunityTitle,
        OrganizationId = organisations[o.Organisation],
        OrganizationName = o.Organisation,
        Status = o.Assessment.Status,
        Criteria = o.Assessment.Criteria,
        Facts = CourseQualityJobRules.Facts(o.Job)
      })], CourseQualityJobGradingDefaults.Configured(Options), now).ToDictionary(o => o.OpportunityId);

      var lines = new List<string> { $"{results.Count} jobs; grades " + string.Join(", ", grades.Values.GroupBy(o => o.Grade).OrderBy(o => o.Key).Select(o => $"{o.Key} {o.Count()}")) };
      foreach (var group in results.GroupBy(o => o.Organisation))
      {
        var list = group.Select(o => o.Assessment).ToList();
        lines.Add($"{group.Key} ({list.Count}): " + string.Join(", ", list.GroupBy(o => o.Status).Select(o => $"{o.Key} {o.Count()}"))
          + $"; mean score {list.Average(o => o.Score):0}");
        lines.Add("  missing: " + string.Join(", ", list
          .Select(o => o.Criteria.Single(c => c.Criterion == CourseQualityCriterion.JobDetailsClear).Reason)
          .Where(o => o.StartsWith("The listing does not state: ", StringComparison.Ordinal))
          .SelectMany(o => o["The listing does not state: ".Length..].TrimEnd('.').Split(", "))
          .GroupBy(o => o).OrderByDescending(o => o.Count()).Select(o => $"{o.Key} {o.Count()}")));
        var graded = list.Select(o => grades[o.OpportunityId]).ToList();
        lines.Add("  grades: " + string.Join(", ", graded.GroupBy(o => o.Grade).OrderBy(o => o.Key).Select(o => $"{o.Key} {o.Count()}"))
          + $"; grade score min {graded.Min(o => o.Score)} max {graded.Max(o => o.Score)}");
        lines.Add("  youth fit: " + string.Join(", ", graded.Select(o => o.Factors.Single(f => f.Factor == CourseGradeFactor.YouthFit))
          .GroupBy(o => o.HasData ? o.Score.ToString(System.Globalization.CultureInfo.InvariantCulture) : "no data").OrderByDescending(o => o.Count()).Select(o => $"{o.Key}: {o.Count()}")));
        lines.Add("  stale: " + list.Count(o => o.Criteria.SingleOrDefault(c => c.Criterion == CourseQualityCriterion.JobFresh)?.Score == 0));
      }

      foreach (var held in results.Where(o => o.Assessment.Criteria.Any(c => c.Criterion == CourseQualityCriterion.NoScamSignals && c.Score < 100)))
        lines.Add($"SIGNAL {held.Organisation} | {held.Assessment.OpportunityTitle} | {held.Assessment.Criteria.Single(c => c.Criterion == CourseQualityCriterion.NoScamSignals).Reason}");

      var output = System.Environment.GetEnvironmentVariable("COURSE_QUALITY_JOB_SAMPLE_OUT");
      if (!string.IsNullOrEmpty(output)) File.WriteAllLines(output, lines);
      Assert.NotEmpty(results);
    }
    #endregion
  }
}
