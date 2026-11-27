using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  /// <summary>
  /// The DropOff factor (iteration 4, W5): a slot at weight 0, neutral while drop-off is collecting from the pilot, never for jobs.
  /// </summary>
  public class CourseQualityDropOffFactorTests
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

    [Fact]
    public void Default_IsACourseFactor_AtWeightZero_NeutralFifty()
    {
      Assert.True(CourseQualityGradingDefaults.Factors.ContainsKey(CourseGradeFactor.DropOff));
      Assert.Equal(0m, CourseQualityGradingDefaults.Factors[CourseGradeFactor.DropOff].Weight);
      Assert.Equal(50, CourseQualityGradingDefaults.Factors[CourseGradeFactor.DropOff].NeutralScore);
      Assert.Equal(0m, Rubric.Factors[CourseGradeFactor.DropOff].Weight);
      Assert.Empty(CourseQualityGrading.Validate(Rubric));
    }

    [Fact]
    public void Grade_HasTheFactor_WithoutData_Neutral_AndWeightZero()
    {
      var result = CourseQualityGradingEngine.Grade([Input()], Rubric, Now).Single();
      var factor = result.Factors.Single(o => o.Factor == CourseGradeFactor.DropOff);

      Assert.False(factor.HasData);
      Assert.Equal(50, factor.Score);
      Assert.Equal(0m, factor.Weight);
      Assert.Contains("collecting from the pilot", factor.Reason);
      Assert.Equal(CourseQualityGradingDefaults.Factors.Count, result.Factors.Count);
    }

    [Fact]
    public void JobRubric_NeverHasTheFactor()
    {
      Assert.DoesNotContain(CourseGradeFactor.DropOff, CourseQualityJobGradingDefaults.Rubric().Factors.Keys);
    }

    [Fact]
    public void SavedRubric_FromBeforeTheFactor_GetsItAtWeightZero()
    {
      var saved = CourseQualityGrading.Configured(new CourseQualityOptions());
      saved.Factors.Remove(CourseGradeFactor.DropOff);

      var upgraded = CourseQualityGrading.WithNewFactors(saved);

      Assert.Equal(0m, upgraded.Factors[CourseGradeFactor.DropOff].Weight);
      Assert.Empty(CourseQualityGrading.Validate(upgraded));
    }
  }
}
