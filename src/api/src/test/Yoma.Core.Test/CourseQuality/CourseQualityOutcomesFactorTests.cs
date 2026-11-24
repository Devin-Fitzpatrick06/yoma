using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  /// <summary>
  /// The Outcomes factor (iteration 3, F3): a slot at weight 0, neutral without data, fed by progression and employment.
  /// </summary>
  public class CourseQualityOutcomesFactorTests
  {
    private static readonly CourseGradeRubric Rubric = CourseQualityGrading.Configured(new CourseQualityOptions());

    private static CourseGradeInput Input() => new()
    {
      OpportunityId = Guid.NewGuid(),
      OpportunityTitle = "Introduction to Data Analysis",
      OrganizationId = Guid.NewGuid(),
      OrganizationName = "Open Learning Org",
      Status = CourseQualityStatus.Verified,
      Confidence = 0.9m,
      Criteria = [new CourseQualityCriterionResult { Criterion = CourseQualityCriterion.LinkWorks, Score = 100, Reason = "ok" }],
      CostType = CourseCostType.Free,
      PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, HasCourseJsonLd = true, OutcomeCount = 5, ModuleCount = 8 },
      Description = new string('a', 600),
      SkillCount = 3,
      Languages = ["English"],
      CommitmentInterval = TimeIntervalOption.Hour,
      CommitmentIntervalCount = 6,
      DateCreated = Now.AddMonths(-6)
    };

    private static CourseGradeResult One(CourseGradeInput input, CourseGradeRubric? rubric = null) =>
      CourseQualityGradingEngine.Grade([input], rubric ?? Rubric, Now).Single();

    private static CourseGradeFactorResult Outcomes(CourseGradeResult result) => result.Factors.Single(o => o.Factor == CourseGradeFactor.Outcomes);

    [Fact]
    public void Default_IsWeightZero_SoNoGradeMoves()
    {
      Assert.Equal(0m, Rubric.Factors[CourseGradeFactor.Outcomes].Weight);

      var without = Input();
      var with = Input();
      with.OutcomeCompleters = 100;
      with.ProgressionRate = 0.5m;
      with.EmploymentRate = 0.2m;

      Assert.Equal(One(without).Score, One(with).Score);
      Assert.Equal(0m, Outcomes(One(with)).Weight);
    }

    [Fact]
    public void NoData_IsNeutral()
    {
      var factor = Outcomes(One(Input()));

      Assert.False(factor.HasData);
      Assert.Equal(50, factor.Score);
      Assert.Contains("No learner has completed", factor.Reason);
    }

    [Fact]
    public void FewerCompletersThanTheMinimum_IsNeutral_EvenWithRates()
    {
      var input = Input();
      input.OutcomeCompleters = 19;
      input.ProgressionRate = 1;
      input.EmploymentRate = 1;

      var factor = Outcomes(One(input));
      Assert.False(factor.HasData);
      Assert.Equal(50, factor.Score);
      Assert.Contains("outcomes count from 20", factor.Reason);
    }

    [Fact]
    public void Rates_AreScoredAgainstTheirTargets()
    {
      var input = Input();
      input.OutcomeCompleters = 40;
      input.ProgressionRate = 0.25m; // half the 0.5 target
      input.EmploymentRate = 0.2m; // the target

      var factor = Outcomes(One(input));
      Assert.True(factor.HasData);
      Assert.Equal(70, factor.Score); // 0.5 * 60 + 1 * 40
      Assert.Contains("Of 40 completers, 25% went on", factor.Reason);
      Assert.DoesNotContain("times", factor.Reason);
    }

    [Fact]
    public void OnlyOneRate_StandsAlone()
    {
      var input = Input();
      input.OutcomeCompleters = 40;
      input.EmploymentRate = 0.1m;

      Assert.Equal(50, Outcomes(One(input)).Score);
    }

    [Fact]
    public void WithWeight_GoodOutcomesRaiseTheScore()
    {
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      rubric.Factors[CourseGradeFactor.Outcomes].Weight = 20;

      var weak = Input();
      weak.OutcomeCompleters = 40;
      weak.ProgressionRate = 0;
      weak.EmploymentRate = 0;
      var strong = Input();
      strong.OutcomeCompleters = 40;
      strong.ProgressionRate = 0.5m;
      strong.EmploymentRate = 0.2m;

      Assert.True(One(strong, rubric).Score > One(weak, rubric).Score);
    }

    [Fact]
    public void ASavedRubricFromBeforeOutcomes_KeepsGrading_AtWeightZero()
    {
      var old = CourseQualityGrading.Configured(new CourseQualityOptions());
      old.Factors.Remove(CourseGradeFactor.Outcomes);
      var stored = CourseQualityGrading.Serialize(old);

      Assert.NotEmpty(CourseQualityGrading.Validate(CourseQualityGrading.Deserialize(stored)));

      var upgraded = CourseQualityGrading.WithNewFactors(CourseQualityGrading.Deserialize(stored));
      Assert.Empty(CourseQualityGrading.Validate(upgraded));
      Assert.Equal(0m, upgraded.Factors[CourseGradeFactor.Outcomes].Weight);
      Assert.Equal(old.Factors[CourseGradeFactor.LearnerFeedback].Weight, upgraded.Factors[CourseGradeFactor.LearnerFeedback].Weight);
    }

    [Fact]
    public void Validate_RejectsOutcomeTargetsOutsideZeroToOne()
    {
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      rubric.Factors[CourseGradeFactor.Outcomes].Parameters[CourseGradeParameter.EmploymentTarget] = 2;
      Assert.Contains(CourseQualityGrading.Validate(rubric), e => e.Contains("EmploymentTarget of at most 1"));

      rubric.Factors[CourseGradeFactor.Outcomes].Parameters[CourseGradeParameter.EmploymentTarget] = 0;
      Assert.Contains(CourseQualityGrading.Validate(rubric), e => e.Contains("EmploymentTarget above 0"));
    }
  }
}
