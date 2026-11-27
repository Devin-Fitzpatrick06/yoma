using Xunit;
using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Models;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityJobGradingTests
  {
    private static readonly CourseGradeRubric Rubric = CourseQualityJobGradingDefaults.Configured(new CourseQualityOptions());

    private static readonly Guid Partner = Guid.NewGuid();

    private static Opportunity Listing(string description, List<CustomFieldValueItem>? fields = null) => new()
    {
      Id = Guid.NewGuid(),
      Title = "Shop assistant",
      Description = description,
      Type = Domain.Opportunity.Type.Job,
      URL = "https://jobs.example.org/1",
      Countries = [new OpportunityCountryInfo()],
      CustomFields = fields
    };

    private static JobGradeInput Input(string description = "Full-time in Lagos. Requirements: matric. Salary: R6,500 per month.",
      CourseQualityStatus status = CourseQualityStatus.Verified, Guid? organization = null, int scamScore = 100, List<CustomFieldValueItem>? fields = null) => new()
      {
        OpportunityId = Guid.NewGuid(),
        OpportunityTitle = "Shop assistant",
        OrganizationId = organization ?? Partner,
        OrganizationName = "Feed partner",
        Status = status,
        Criteria = [new CourseQualityCriterionResult { Criterion = CourseQualityCriterion.NoScamSignals, Score = scamScore, Reason = "r", HardGateFailed = scamScore == 0 }],
        Facts = CourseQualityJobRules.Facts(Listing(description, fields))
      };

    private static CourseGradeResult One(JobGradeInput input) => CourseQualityJobGradingEngine.Grade([input], Rubric, Now).Single();

    private static int Factor(CourseGradeResult result, CourseGradeFactor factor) => result.Factors.Single(o => o.Factor == factor).Score;

    #region Rubric
    [Fact]
    public void Rubric_HasOnlyJobFactors_AndPassesTheCourseRulesItShares()
    {
      Assert.Equal(
        [CourseGradeFactor.PayTransparency, CourseGradeFactor.JobClarity, CourseGradeFactor.YouthFit, CourseGradeFactor.ListingSafety, CourseGradeFactor.PartnerReliability, CourseGradeFactor.ApplicantFeedback],
        Rubric.Factors.Keys);
      Assert.Equal(100m, Rubric.Factors.Values.Sum(o => o.Weight));
      Assert.Equal("jobs-g1", Rubric.Version);
      Assert.Equal(CourseGrade.Ungraded, Rubric.NeedsReviewMaxGrade);
    }

    [Fact]
    public void CourseRubric_StillValidates_WithoutTheJobFactors()
    {
      Assert.Empty(CourseQualityGrading.Validate(CourseQualityGrading.Configured(new CourseQualityOptions())));
      Assert.DoesNotContain(CourseGradeFactor.PayTransparency, CourseQualityGrading.Configured(new CourseQualityOptions()).Factors.Keys);
    }
    #endregion

    #region Factors
    [Theory]
    [InlineData("Salary: R6,500 per month.", 100)]
    [InlineData("**Salary:** Per Month, Market Related", 30)]
    [InlineData("Join a growing team.", 0)]
    public void Pay_RewardsAStatedAmount(string description, int score)
    {
      Assert.Equal(score, Factor(One(Input(description)), CourseGradeFactor.PayTransparency));
    }

    [Theory]
    [InlineData("No experience needed. Salary R5000.", 95)]
    [InlineData("Graduate trainee programme. Salary R5000.", 95)]
    [InlineData("B.Sc with 2 years experience. Salary ₦150,000.", 70)]
    [InlineData("Minimum of 5 years in sales. Salary ₦150,000.", 15)]
    [InlineData("3+ years of relevant experience, or 1 year experience in retail. Salary R5000.", 90)]
    [InlineData("At least three (3) years experience at supervisory level. Salary R5000.", 50)]
    [InlineData("At least 6 months experience in sales. Salary R5000.", 90)]
    [InlineData("Education: Matric Required. Salary R5000.", 85)]
    [InlineData("Education: Matric and a diploma in logistics. Salary R5000.", 50)]
    public void YouthFit_ReadsExperienceFromTheText(string description, int score)
    {
      Assert.Equal(score, Factor(One(Input(description)), CourseGradeFactor.YouthFit));
    }

    [Fact]
    public void YouthFit_PrefersTheExperienceField_AndIsNeutralWithoutData()
    {
      var senior = Input("5 years experience? No: entry level. Salary R5000.", fields:
        [new CustomFieldValueItem { Key = CustomFieldConstants.Job.Experience.Level, ValueRaw = CustomFieldConstants.Job.Experience.Options.Senior }]);
      Assert.Equal(15, Factor(One(senior), CourseGradeFactor.YouthFit));

      var unknown = One(Input("Salary R5000 per month, full-time."));
      var youth = unknown.Factors.Single(o => o.Factor == CourseGradeFactor.YouthFit);
      Assert.False(youth.HasData);
      Assert.Equal(50, youth.Score);
    }

    [Fact]
    public void Clarity_CountsScheduleLocationRequirementsAndHowToApply()
    {
      var result = One(Input("Salary R5000."));

      Assert.Equal(50, Factor(result, CourseGradeFactor.JobClarity)); // location and how to apply, from the country and the link
      Assert.Equal("Does not state: schedule or contract type, requirements.", result.Factors.Single(o => o.Factor == CourseGradeFactor.JobClarity).Reason);
    }

    [Fact]
    public void Safety_FallsWithAWeakScamSign()
    {
      Assert.Equal(100, Factor(One(Input()), CourseGradeFactor.ListingSafety));
      Assert.Equal(80, Factor(One(Input(scamScore: 60)), CourseGradeFactor.ListingSafety));
    }

    [Fact]
    public void PartnerReliability_IsSmoothedOverThePartnersJobs()
    {
      var other = Guid.NewGuid();
      var inputs = new List<JobGradeInput> { Input(organization: other), Input(organization: other, status: CourseQualityStatus.NotVerified) };

      var results = CourseQualityJobGradingEngine.Grade(inputs, Rubric, Now);

      // (1 + 8) / (2 + 10)
      Assert.All(results, o => Assert.Equal(75, Factor(o, CourseGradeFactor.PartnerReliability)));
    }

    [Fact]
    public void ApplicantFeedback_IsNeutralUntilThereAreAnswers()
    {
      var feedback = One(Input()).Factors.Single(o => o.Factor == CourseGradeFactor.ApplicantFeedback);

      Assert.False(feedback.HasData);
      Assert.Equal(50, feedback.Score);
      Assert.Equal("No applicant has answered the survey yet.", feedback.Reason);
    }
    #endregion

    #region Bands and caps
    [Fact]
    public void NotVerifiedAndHeldJobs_AreNotGraded()
    {
      Assert.Equal(CourseGrade.Ungraded, One(Input(status: CourseQualityStatus.NotVerified)).Grade);

      var held = One(Input(status: CourseQualityStatus.NeedsReview, scamScore: 0));
      Assert.Equal(CourseGrade.Ungraded, held.Grade);
      Assert.Contains(CourseQualityGradingEngine.NotReviewedCap, held.Caps);
    }

    [Fact]
    public void Bands_SpreadJobsByPercentile_WithATopShareAsA()
    {
      // 100 jobs: the more experience asked for, the lower the score
      var inputs = Enumerable.Range(0, 100).Select(i => Input($"Full-time. Requirements: {i % 6} years experience. Salary: R{5000 + i}.")).ToList();

      var results = CourseQualityJobGradingEngine.Grade(inputs, Rubric, Now);
      var counts = results.GroupBy(o => o.Grade).ToDictionary(o => o.Key, o => o.Count());

      Assert.True(counts.GetValueOrDefault(CourseGrade.A) <= 17);
      Assert.True(counts.GetValueOrDefault(CourseGrade.A) > 0);
      Assert.Contains(CourseGrade.D, counts.Keys);
      Assert.All(results.Where(o => o.Grade == CourseGrade.A), o => Assert.Equal(100, Factor(o, CourseGradeFactor.YouthFit)));
    }

    [Fact]
    public void ATiedBlock_TakesTheBandOfItsMiddleRank_ButNeverOverfillsA()
    {
      // 10 leaders, then 30 identical templated jobs spanning the B and C cut-offs (ranks 11 to 40 of 50), then 10 weak ones
      var leaders = Enumerable.Range(0, 10).Select(i => Input($"Full-time. Requirements: matric. No experience needed. Salary: R{6000 + i}.")).ToList();
      var block = Enumerable.Range(0, 30).Select(_ => Input("Full-time. Requirements: Matric. **Salary:** Per Month, Market Related")).ToList();
      var weak = Enumerable.Range(0, 10).Select(_ => Input("Requirements: 5 years experience.")).ToList();

      var results = CourseQualityJobGradingEngine.Grade([.. leaders, .. block, .. weak], Rubric, Now).ToDictionary(o => o.OpportunityId);

      // the 10 leaders tie too: A holds 8 of 50, so the group does not fit there whole and takes B
      Assert.All(leaders, o => Assert.Equal(CourseGrade.B, results[o.OpportunityId].Grade));

      // middle rank 25 of 50: past B's 20, within C's 37
      Assert.All(block, o => Assert.Equal(CourseGrade.C, results[o.OpportunityId].Grade));
      Assert.All(weak, o => Assert.Equal(CourseGrade.D, results[o.OpportunityId].Grade));
    }

    [Fact]
    public void A_NeedsAStatedPayAmount()
    {
      // a lone job ranks first; without a pay amount it is held to B
      var withoutPay = Input("No experience needed. Full-time. Requirements: matric. **Salary:** Per Month, Market Related");
      var result = CourseQualityJobGradingEngine.Grade([withoutPay, .. Enumerable.Range(0, 9).Select(_ => Input("Full-time. Requirements: 5 years experience."))], Rubric, Now)
        .Single(o => o.OpportunityId == withoutPay.OpportunityId);

      Assert.NotEqual(CourseGrade.A, result.Grade);
      Assert.Contains(result.Caps, o => o.StartsWith("Pay 30 is below the A floor of 100"));
    }

    [Fact]
    public void JobGrades_UseTheJobRubricVersion()
    {
      Assert.Equal("jobs-g1", One(Input()).RubricVersion);
    }
    #endregion
  }
}
