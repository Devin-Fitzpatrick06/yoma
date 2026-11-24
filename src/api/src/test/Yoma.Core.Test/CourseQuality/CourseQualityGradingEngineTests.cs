using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityGradingEngineTests
  {
    private static readonly CourseGradeRubric Rubric = CourseQualityGrading.Configured(new CourseQualityOptions());

    private static CourseGradeInput Input(Guid? organizationId = null, CourseQualityStatus status = CourseQualityStatus.Verified) => new()
    {
      OpportunityId = Guid.NewGuid(),
      OpportunityTitle = "Introduction to Data Analysis",
      OrganizationId = organizationId ?? Guid.NewGuid(),
      OrganizationName = "Open Learning Org",
      Status = status,
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

    /// <summary>
    /// Strong on the factors the A floors do not look at, so a floor is what decides.
    /// </summary>
    private static CourseGradeInput Strong()
    {
      var input = Input();
      input.SkillCount = 5;
      input.Description = new string('a', 1500);
      input.Languages = ["English", "Swahili"];
      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, HasCourseJsonLd = true, OutcomeCount = 5, ModuleCount = 10, AssessmentMentioned = true, CertificateMentioned = true };
      return input;
    }

    private static CourseGradeResult One(CourseGradeInput input, CourseGradeRubric? rubric = null) =>
      CourseQualityGradingEngine.Grade([input], rubric ?? Rubric, Now).Single();

    /// <summary>
    /// The course ranked first among 19 weaker ones, so its rank alone earns A: 3 of 20 fit in the top 15%.
    /// </summary>
    private static CourseGradeResult Best(CourseGradeInput input, CourseGradeRubric? rubric = null)
    {
      var weaker = Enumerable.Range(0, 19).Select(_ =>
      {
        var o = Input();
        o.CostType = CourseCostType.Paid;
        o.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page };
        o.SkillCount = 0;
        o.Description = "x";
        o.CommitmentInterval = null;
        return o;
      });

      return CourseQualityGradingEngine.Grade([input, .. weaker], rubric ?? Rubric, Now)[0];
    }

    private static CourseGradeFactorResult Factor(CourseGradeResult result, CourseGradeFactor factor) => result.Factors.Single(o => o.Factor == factor);

    #region Factor curves
    [Theory]
    [InlineData(0.25, 40)]
    [InlineData(0.5, 40)]
    [InlineData(1.25, 70)]
    [InlineData(2, 100)]
    [InlineData(10, 100)]
    [InlineData(20, 100)]
    [InlineData(40, 80)]
    [InlineData(60, 60)]
    [InlineData(80, 50)]
    [InlineData(100, 40)]
    [InlineData(400, 40)]
    public void Length_FollowsTheRubricCurve(double hours, int score)
    {
      Assert.Equal(score, CourseQualityGradingEngine.LengthScore((decimal)hours, Rubric));
    }

    [Theory]
    [InlineData(TimeIntervalOption.Minute, 90, 1.5)]
    [InlineData(TimeIntervalOption.Hour, 6, 6)]
    [InlineData(TimeIntervalOption.Day, 3, 6)]
    [InlineData(TimeIntervalOption.Week, 4, 20)]
    [InlineData(TimeIntervalOption.Month, 2, 40)]
    public void StudyHours_CountRealisticStudyTime_NotTheCalendar(TimeIntervalOption interval, short count, double hours)
    {
      Assert.Equal((decimal)hours, CourseQualityGradingEngine.StudyHours(interval, count, Rubric)!.Value, 3);
    }

    [Fact]
    public void StudyHours_FollowTheRubricParameters()
    {
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      rubric.Factors[CourseGradeFactor.CourseLength].Parameters[CourseGradeParameter.StudyHoursPerWeek] = 10;

      Assert.Equal(40m, CourseQualityGradingEngine.StudyHours(TimeIntervalOption.Week, 4, rubric));
    }

    [Fact]
    public void Length_WithoutACommitment_IsNeutralAndHasNoData()
    {
      var input = Input();
      input.CommitmentInterval = null;

      var factor = Factor(One(input), CourseGradeFactor.CourseLength);

      Assert.False(factor.HasData);
      Assert.Equal(50, factor.Score);
    }

    [Theory]
    [InlineData(5, true, 1)]
    [InlineData(1, false, 0)]
    [InlineData(3, true, 0.75)]
    [InlineData(null, true, 1)]
    [InlineData(5, null, 1)]
    public void AnswerScore_IsHalfRatingHalfRecommend(int? rating, bool? recommend, double expected)
    {
      Assert.Equal((decimal)expected, CourseQualityGradingEngine.AnswerScore(new CourseGradeFeedbackAnswer(rating, recommend)));
    }

    [Fact]
    public void Feedback_IsShrunkTowardsTheProvider_UntilThereAreEnoughAnswers()
    {
      var input = Input();
      input.Feedback = [new CourseGradeFeedbackAnswer(5, true)];

      // provider prior (1 + 10 x 0.7) / 11 = 0.727; course (1 + 10 x 0.727) / 11 = 0.752
      var factor = Factor(One(input), CourseGradeFactor.LearnerFeedback);

      Assert.True(factor.HasData);
      Assert.Equal(75, factor.Score);

      input.Feedback = [.. Enumerable.Repeat(new CourseGradeFeedbackAnswer(5, true), 200)];
      Assert.True(Factor(One(input), CourseGradeFactor.LearnerFeedback).Score >= 98);
    }

    [Fact]
    public void Feedback_WithNoAnswers_IsNeutral()
    {
      var factor = Factor(One(Input()), CourseGradeFactor.LearnerFeedback);

      Assert.False(factor.HasData);
      Assert.Equal(50, factor.Score);
    }

    [Fact]
    public void Syllabus_AddsTheRubricPoints_FromThePage()
    {
      var input = Input();
      input.PageEvidence = new CourseQualityPageEvidence
      {
        Source = CourseQualityEvidenceSource.Page,
        HasCourseJsonLd = true,
        JsonLdSyllabus = true,
        OutcomeCount = 4,
        ModuleCount = 12,
        PrerequisitesStated = true
      };

      var factor = Factor(One(input), CourseGradeFactor.SyllabusAvailable);

      Assert.Equal(100, factor.Score);
      Assert.Contains("12 modules", factor.Reason);
    }

    [Fact]
    public void Syllabus_FromListingText_CountsHalf()
    {
      var input = Input();
      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Listing, OutcomeCount = 4, ModuleCount = 5 };

      var factor = Factor(One(input), CourseGradeFactor.SyllabusAvailable);

      Assert.True(factor.HasData);
      Assert.Equal(30, factor.Score);
      Assert.Contains("listing text", factor.Reason);
    }

    [Fact]
    public void Syllabus_ListsUnderThree_DoNotCount()
    {
      var input = Input();
      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, OutcomeCount = 2, ModuleCount = 2 };

      var factor = Factor(One(input), CourseGradeFactor.SyllabusAvailable);

      Assert.True(factor.HasData);
      Assert.Equal(0, factor.Score);
    }

    [Fact]
    public void Syllabus_WithoutStoredEvidence_ReadsTheDescription()
    {
      var input = Input();
      input.PageEvidence = null;
      input.Description = "What you will learn:\n- Clean data\n- Explore data\n- Present data\n";

      Assert.Equal(15, Factor(One(input), CourseGradeFactor.SyllabusAvailable).Score);
    }

    [Fact]
    public void Provider_UsesThePriorPassRate_AndCountsDeadLinks()
    {
      var organizationId = Guid.NewGuid();
      var verified = Input(organizationId);
      var dead = Input(organizationId, CourseQualityStatus.NotVerified);
      dead.Criteria = [new CourseQualityCriterionResult { Criterion = CourseQualityCriterion.LinkWorks, Score = 0, HardGateFailed = true, Reason = "404" }];

      var results = CourseQualityGradingEngine.Grade([verified, dead], Rubric, Now);
      var factor = Factor(results[0], CourseGradeFactor.ProviderReliability);

      // pass (1 + 8) / (2 + 10) = 0.75 x 40, live 0.5 x 20, learner prior 0.7 x 33.33, listings 2 / 20 x 6.67
      Assert.Equal(64, factor.Score);
      Assert.Contains("1 of 2 listings verified, 1 with a dead link", factor.Reason);
      Assert.Equal(factor.Score, Factor(results[1], CourseGradeFactor.ProviderReliability).Score);
    }

    [Fact]
    public void Comprehensiveness_RewardsModulesAssessmentCertificateSkillsAndDescription()
    {
      var input = Input();
      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, ModuleCount = 15, AssessmentMentioned = true, CertificateMentioned = true };
      input.SkillCount = 5;
      input.Description = new string('a', 1500);

      Assert.Equal(100, Factor(One(input), CourseGradeFactor.Comprehensiveness).Score);

      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page };
      input.SkillCount = 0;
      input.Description = new string('a', 750);
      Assert.Equal(10, Factor(One(input), CourseGradeFactor.Comprehensiveness).Score);
    }

    [Fact]
    public void Completion_IsAPercentile_WithinTheLengthBucket()
    {
      CourseGradeInput Course(int completed, int started, short hours)
      {
        var input = Input();
        input.Completed = completed;
        input.Started = started;
        input.CommitmentIntervalCount = hours;
        return input;
      }

      var low = Course(1, 49, 6);
      var high = Course(40, 10, 6);
      var longCourse = Course(0, 50, 80);

      var results = CourseQualityGradingEngine.Grade([low, high, longCourse], Rubric, Now).ToDictionary(o => o.OpportunityId);

      Assert.Equal(25, Factor(results[low.OpportunityId], CourseGradeFactor.Completion).Score);
      Assert.Equal(75, Factor(results[high.OpportunityId], CourseGradeFactor.Completion).Score);

      // alone in its bucket, so level with itself
      Assert.Equal(50, Factor(results[longCourse.OpportunityId], CourseGradeFactor.Completion).Score);
    }

    [Fact]
    public void Completion_WithNoStarts_HasNoData()
    {
      Assert.False(Factor(One(Input()), CourseGradeFactor.Completion).HasData);
    }

    [Fact]
    public void Integrity_ReadsStatusConfidenceLinkAndAge()
    {
      var input = Input();
      Assert.Equal(98, Factor(One(input), CourseGradeFactor.ListingIntegrity).Score);

      input.DateCreated = Now.AddYears(-4);
      input.Status = CourseQualityStatus.NeedsReview;
      input.Confidence = 0.5m;
      input.Criteria = [new CourseQualityCriterionResult { Criterion = CourseQualityCriterion.LinkWorks, Score = 50, Reason = "timeout" }];

      // 0.5 x 40 + 0.5 x 20 + 0.5 x 25 + 0
      var factor = Factor(One(input), CourseGradeFactor.ListingIntegrity);
      Assert.Equal(43, factor.Score);
      Assert.Contains("more than 3 years ago", factor.Reason);
    }

    [Theory]
    [InlineData(CourseCostType.Free, false, 40)]
    [InlineData(CourseCostType.Free, true, 25)]
    [InlineData(CourseCostType.Paid, false, 0)]
    [InlineData(CourseCostType.Unknown, false, 20)]
    public void Accessibility_ScoresTheCost(CourseCostType cost, bool paidCertificate, int score)
    {
      var input = Input();
      input.CostType = cost;
      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, CertificatePaid = paidCertificate };

      Assert.Equal(score, Factor(One(input), CourseGradeFactor.Accessibility).Score);
    }

    [Fact]
    public void Accessibility_AddsLanguageAndLowDataBonuses_UpToAHundred()
    {
      var input = Input();
      input.Languages = ["English", "Swahili"];
      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, OfflineMentioned = true };

      Assert.Equal(100, Factor(One(input), CourseGradeFactor.Accessibility).Score);

      input.Languages = ["French"];
      input.CostType = CourseCostType.Unknown;
      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page };
      Assert.Equal(50, Factor(One(input), CourseGradeFactor.Accessibility).Score);
    }

    [Fact]
    public void Accessibility_WithNothingKnown_HasNoData()
    {
      var input = Input();
      input.CostType = CourseCostType.Unknown;

      var factor = Factor(One(input), CourseGradeFactor.Accessibility);

      Assert.False(factor.HasData);
      Assert.Equal(20, factor.Score);
    }

    [Fact]
    public void Score_IsTheWeightedMean_AndWeightsAreShares()
    {
      var result = One(Input());

      Assert.Equal(100m, result.Factors.Sum(o => o.Weight));
      var expected = (int)Math.Round(result.Factors.Sum(o => o.Score * o.Weight) / 100m, MidpointRounding.AwayFromZero);
      Assert.Equal(expected, result.Score);
    }
    #endregion

    #region Bands
    /// <summary>
    /// 1,000 courses in 40 groups of 25 identical courses, so every cut-off falls inside a tied group.
    /// </summary>
    private static List<CourseGradeInput> TiedCatalogue()
    {
      var inputs = new List<CourseGradeInput>();
      for (var group = 0; group < 40; group++)
        for (var i = 0; i < 25; i++)
        {
          var input = Input();
          input.SkillCount = group % 6;
          input.Description = new string('a', 100 + group * 35);
          input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, HasCourseJsonLd = true, OutcomeCount = 5, ModuleCount = 3 + group % 10 };
          input.CommitmentIntervalCount = (short)(1 + group % 4);
          input.OrganizationId = Guid.Empty;
          inputs.Add(input);
        }

      return inputs;
    }

    [Fact]
    public void Bands_KeepAAtOrUnderFifteenPercent_WithHeavyTies()
    {
      var results = CourseQualityGradingEngine.Grade(TiedCatalogue(), Rubric, Now);
      var counts = results.GroupBy(o => o.Grade).ToDictionary(o => o.Key, o => o.Count());

      Assert.Equal(1000, results.Count);
      Assert.True(counts.GetValueOrDefault(CourseGrade.A) <= 150, $"{counts.GetValueOrDefault(CourseGrade.A)}");
      Assert.True(counts.GetValueOrDefault(CourseGrade.A) + counts.GetValueOrDefault(CourseGrade.B) <= 400);
      Assert.DoesNotContain(results, o => o.Grade == CourseGrade.Ungraded);

      // identical courses always share a grade
      foreach (var group in results.GroupBy(o => (o.Score, string.Join(",", o.Factors.Select(f => f.Score)))))
        Assert.Single(group.Select(o => o.Grade).Distinct());
    }

    [Fact]
    public void Bands_PutATiedGroupOnTheCutOff_InTheLowerBand()
    {
      // 10 courses, 2 tied at the top; A at 15% holds one course, so the tied pair falls to B together
      var inputs = Enumerable.Range(0, 10).Select(_ => Input()).ToList();
      for (var i = 2; i < 10; i++) inputs[i].SkillCount = 0;

      var results = CourseQualityGradingEngine.Grade(inputs, Rubric, Now);

      Assert.DoesNotContain(results, o => o.Grade == CourseGrade.A);
      Assert.Equal(CourseGrade.B, results[0].Grade);
      Assert.Equal(CourseGrade.B, results[1].Grade);
      Assert.Equal(100, results[0].Percentile);
      Assert.Equal(100, results[1].Percentile);
      Assert.Equal(80, results[2].Percentile);
    }

    [Fact]
    public void Bands_GiveA_ToTheTopShare()
    {
      // 20 distinct courses: floor(20 x 15%) = 3 earn A, then floor(20 x 40%) = 8 reach B
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      foreach (var factor in rubric.Factors) factor.Value.Weight = factor.Key == CourseGradeFactor.Comprehensiveness ? 100 : 0;
      var shares = rubric.Factors[CourseGradeFactor.Comprehensiveness].Parameters;
      foreach (var name in new[] { CourseGradeParameter.ModulesShare, CourseGradeParameter.AssessmentShare, CourseGradeParameter.CertificateShare, CourseGradeParameter.SkillsShare }) shares[name] = 0;
      rubric.Bands[0].MinScore = 0;

      var inputs = Enumerable.Range(0, 20).Select(i =>
      {
        var input = Strong();
        input.Description = new string('a', 1500 - i * 70);
        return input;
      }).ToList();

      var results = CourseQualityGradingEngine.Grade(inputs, rubric, Now);

      Assert.Equal(3, results.Count(o => o.Grade == CourseGrade.A));
      Assert.Equal(5, results.Count(o => o.Grade == CourseGrade.B));
      Assert.Equal(CourseGrade.A, results[0].Grade);
      Assert.Equal(CourseGrade.D, results[^1].Grade);
    }

    [Fact]
    public void Bands_BreakTiesOnFeedbackFirst()
    {
      // feedback carries no weight here, so every score is level and only the tie-break tells them apart
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      rubric.Factors[CourseGradeFactor.LearnerFeedback].Weight = 0;

      var inputs = Enumerable.Range(0, 10).Select(_ => Input()).ToList();
      var liked = inputs[5];
      liked.Feedback = [new CourseGradeFeedbackAnswer(5, true)];

      var results = CourseQualityGradingEngine.Grade(inputs, rubric, Now).ToDictionary(o => o.OpportunityId);

      Assert.Equal(results[inputs[0].OpportunityId].Score, results[liked.OpportunityId].Score);
      Assert.Equal(CourseGrade.A, results[liked.OpportunityId].Grade);
      Assert.Equal(CourseGrade.D, results[inputs[0].OpportunityId].Grade);
    }

    [Fact]
    public void NotVerified_IsUngraded_AndLeftOutOfThePercentiles()
    {
      var inputs = Enumerable.Range(0, 7).Select(_ => Input()).ToList();
      inputs[0].SkillCount = 5;
      var notVerified = Input(status: CourseQualityStatus.NotVerified);
      notVerified.SkillCount = 5;
      notVerified.Description = new string('a', 1500);

      var results = CourseQualityGradingEngine.Grade([notVerified, .. inputs], Rubric, Now);

      Assert.Equal(CourseGrade.Ungraded, results[0].Grade);
      Assert.Equal("Not graded", results[0].GradeLabel);
      Assert.Equal(0, results[0].Percentile);
      Assert.Contains("Not verified courses are not graded", results[0].Caps);

      // the best graded course is still the top of 7
      Assert.Equal(100, results[1].Percentile);
      Assert.Equal(CourseGrade.A, results[1].Grade);
    }

    [Fact]
    public void NeedsReview_IsNotGraded_ByDefault_AndLeftOutOfThePercentiles()
    {
      var waiting = Strong();
      waiting.Status = CourseQualityStatus.NeedsReview;
      var inputs = Enumerable.Range(0, 7).Select(_ => Input()).ToList();
      inputs[0].SkillCount = 5;

      var results = CourseQualityGradingEngine.Grade([waiting, .. inputs], Rubric, Now);

      Assert.Equal(CourseGrade.Ungraded, results[0].Grade);
      Assert.Equal("Not graded", results[0].GradeLabel);
      Assert.Equal([CourseQualityGradingEngine.NotReviewedCap], results[0].Caps);
      Assert.Equal(0, results[0].Percentile);

      // the best of the 7 graded courses is still at the top
      Assert.Equal(100, results[1].Percentile);
    }

    [Fact]
    public void NeedsReview_IsGraded_OnceAReviewerVerifiesIt()
    {
      var input = Strong();
      input.Status = CourseQualityStatus.NeedsReview;
      Assert.Equal(CourseGrade.Ungraded, Best(input).Grade);

      input.Status = CourseQualityStatus.Verified;
      Assert.Equal(CourseGrade.A, Best(input).Grade);
    }

    [Fact]
    public void TooFewFactorsWithData_IsUngraded()
    {
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      rubric.MinFactorsWithData = 7;

      var result = One(Input(), rubric);

      Assert.Equal(CourseGrade.Ungraded, result.Grade);
      Assert.Contains(result.Caps, o => o.StartsWith("Data on 6 of the 7 factors", StringComparison.Ordinal));
    }

    [Fact]
    public void NeedsReview_IsCappedAtB()
    {
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      rubric.NeedsReviewMaxGrade = CourseGrade.B;
      var input = Strong();
      input.Status = CourseQualityStatus.NeedsReview;
      var result = Best(input, rubric);

      Assert.Equal(CourseGrade.B, result.Grade);
      Assert.Equal("Strong", result.GradeLabel);
      Assert.Contains("Courses waiting for review reach B at most", result.Caps);
    }

    [Fact]
    public void A_NeedsTheSyllabusFloor()
    {
      var input = Strong();
      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, HasCourseJsonLd = true, OutcomeCount = 5, ModuleCount = 2, CertificateMentioned = true, AssessmentMentioned = true };

      var result = Best(input);

      Assert.Equal(CourseGrade.B, result.Grade);
      Assert.Contains("Syllabus 45 is below the A floor of 50", result.Caps);
    }

    [Fact]
    public void A_NeedsTheFeedbackFloor_OnlyOnceEnoughLearnersAnswered()
    {
      var input = Strong();
      input.Feedback = [.. Enumerable.Repeat(new CourseGradeFeedbackAnswer(1, false), 4)];
      Assert.Equal(CourseGrade.A, Best(input).Grade);

      input.Feedback = [.. Enumerable.Repeat(new CourseGradeFeedbackAnswer(1, false), 5)];
      var result = Best(input);

      Assert.Equal(CourseGrade.B, result.Grade);
      Assert.Contains(result.Caps, o => o.StartsWith("Feedback", StringComparison.Ordinal));
    }

    [Fact]
    public void A_NeedsTheMinimumScore()
    {
      var input = Input();
      input.CostType = CourseCostType.Paid;
      input.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, OutcomeCount = 3, ModuleCount = 3 };
      input.SkillCount = 0;
      input.Description = "Short.";

      var result = Best(input);

      Assert.True(result.Score < 65, $"{result.Score}");
      Assert.Equal(CourseGrade.B, result.Grade);
      Assert.Contains($"Score {result.Score} is below the A minimum of 65", result.Caps);
    }

    [Fact]
    public void Grading_TheFullCatalogue_IsQuick()
    {
      var inputs = Enumerable.Range(0, 2400).Select(i =>
      {
        var input = Input(Guid.Parse($"00000000-0000-0000-0000-{i % 30:000000000000}"));
        input.SkillCount = i % 6;
        input.Completed = i % 7;
        input.Started = i % 11;
        input.Feedback = [.. Enumerable.Repeat(new CourseGradeFeedbackAnswer(1 + i % 5, i % 2 == 0), i % 4)];
        return input;
      }).ToList();

      var watch = System.Diagnostics.Stopwatch.StartNew();
      var results = CourseQualityGradingEngine.Grade(inputs, Rubric, Now);

      Assert.Equal(2400, results.Count);
      Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"{watch.Elapsed}");
      Assert.True(results.Count(o => o.Grade == CourseGrade.A) <= 408);
    }
    #endregion

    #region Rubric
    [Fact]
    public void Configured_HasTheDefaultBands_AndWeightsAddUpToAHundred()
    {
      Assert.Equal([CourseGrade.A, CourseGrade.B, CourseGrade.C, CourseGrade.D], Rubric.Bands.Select(o => o.Grade));
      Assert.Equal([17m, 40m, 75m, 100m], Rubric.Bands.Select(o => o.TopPercent));
      Assert.Equal(CourseGrade.Ungraded, Rubric.NeedsReviewMaxGrade);
      Assert.Equal("g2-verified-only", Rubric.Version);
      Assert.Equal(100m, Rubric.Factors.Values.Sum(o => o.Weight));
      Assert.Equal(20m, Rubric.Factors[CourseGradeFactor.LearnerFeedback].Weight);
      Assert.Empty(CourseQualityGrading.Validate(Rubric));
    }

    [Fact]
    public void Normalize_ScalesWeightsToAHundred()
    {
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      foreach (var factor in rubric.Factors.Values) factor.Weight = 1;
      rubric.Factors[CourseGradeFactor.LearnerFeedback].Weight = 0;

      var normalized = CourseQualityGrading.Normalize(rubric);

      Assert.Equal(0m, normalized.Factors[CourseGradeFactor.LearnerFeedback].Weight);
      // every other factor at 1, so each is an equal share of 100
      Assert.Equal(Math.Round(100m / (Enum.GetValues<CourseGradeFactor>().Length - 1), 2), normalized.Factors[CourseGradeFactor.Accessibility].Weight);
    }

    [Fact]
    public void Validate_RejectsBadRubrics()
    {
      static CourseGradeRubric Broken(Action<CourseGradeRubric> change)
      {
        var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
        change(rubric);
        return rubric;
      }

      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Factors[CourseGradeFactor.Completion].Weight = -1)), e => e.Contains("negative weight"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => { foreach (var f in o.Factors.Values) f.Weight = 0; })), e => e.Contains("more than zero"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Factors.Remove(CourseGradeFactor.Completion))), e => e.Contains("needs a weight"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Bands[^1].TopPercent = 90)), e => e.Contains("reach 100"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Bands[1].TopPercent = 10)), e => e.Contains("rise"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => (o.Bands[0], o.Bands[1]) = (o.Bands[1], o.Bands[0]))), e => e.Contains("exactly A, B, C, D"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Bands[0].Grade = CourseGrade.Ungraded)), e => e.Contains("exactly A, B, C, D"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Bands.Clear())), e => e.Contains("exactly A, B, C, D"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Bands.RemoveRange(1, 3))), e => e.Contains("exactly A, B, C, D"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Bands.RemoveAt(1))), e => e.Contains("exactly A, B, C, D"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.NeedsReviewMaxGrade = (CourseGrade)99)), e => e.Contains("waiting for review"));
      Assert.DoesNotContain(CourseQualityGrading.Validate(Broken(o => o.NeedsReviewMaxGrade = CourseGrade.Ungraded)), e => e.Contains("waiting for review"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Bands[0].Label = " ")), e => e.Contains("label"));
      Assert.Contains(CourseQualityGrading.Validate(Broken(o => o.Version = "")), e => e.Contains("version"));
      Assert.Contains(CourseQualityGrading.Validate(null), e => e.Contains("required"));
    }

    [Fact]
    public void Validate_RejectsParametersThatWouldBreakTheArithmetic()
    {
      static List<string> With(CourseGradeFactor factor, string name, decimal value)
      {
        var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
        rubric.Factors[factor].Parameters[name] = value;
        return CourseQualityGrading.Validate(rubric);
      }

      Assert.Contains(With(CourseGradeFactor.LearnerFeedback, CourseGradeParameter.PriorResponses, 0), e => e.Contains("PriorResponses above 0"));
      Assert.Contains(With(CourseGradeFactor.ProviderReliability, CourseGradeParameter.PriorChecks, 0), e => e.Contains("PriorChecks above 0"));
      Assert.Contains(With(CourseGradeFactor.Completion, CourseGradeParameter.PriorStarts, 0), e => e.Contains("PriorStarts above 0"));
      Assert.Contains(With(CourseGradeFactor.LearnerFeedback, CourseGradeParameter.PriorMean, 2), e => e.Contains("PriorMean of at most 1"));
      Assert.Contains(With(CourseGradeFactor.ListingIntegrity, CourseGradeParameter.MaxAgeYears, 51), e => e.Contains("MaxAgeYears of at most 50"));
      Assert.Contains(With(CourseGradeFactor.ListingIntegrity, CourseGradeParameter.MaxAgeYears, 1_000_000), e => e.Contains("above 100000"));
      Assert.Contains(With(CourseGradeFactor.CourseLength, CourseGradeParameter.IdealToHours, 1), e => e.Contains("hours in order"));
      Assert.Empty(With(CourseGradeFactor.ListingIntegrity, CourseGradeParameter.MaxAgeYears, 50));

      var heavy = CourseQualityGrading.Configured(new CourseQualityOptions());
      heavy.Factors[CourseGradeFactor.Completion].Weight = 1001;
      Assert.Contains(CourseQualityGrading.Validate(heavy), e => e.Contains("weight above 1000"));
    }

    [Fact]
    public void Feedback_WithoutARatingOrRecommendation_CountsForNothing()
    {
      // "Yes, I took it", then skipped the survey: no score, so no data and no step towards the A feedback floor
      var input = Strong();
      input.Feedback = [.. Enumerable.Repeat(new CourseGradeFeedbackAnswer(null, null), 10)];

      var result = Best(input);

      Assert.False(Factor(result, CourseGradeFactor.LearnerFeedback).HasData);
      Assert.Equal(0, CourseQualityGradingEngine.ScoredResponses(input.Feedback));
      Assert.Equal(CourseGrade.A, result.Grade);
    }

    [Fact]
    public void Serialize_RoundTrips_WithCaseInsensitiveParameters()
    {
      var copy = CourseQualityGrading.Normalize(CourseQualityGrading.Deserialize(CourseQualityGrading.Serialize(Rubric)));

      Assert.Equal(Rubric.Factors.Count, copy.Factors.Count);
      Assert.Equal(10m, copy.Factors[CourseGradeFactor.LearnerFeedback].Parameters["priorresponses"]);
      Assert.Equal(4, copy.Bands.Count);
    }

    [Fact]
    public void Parameter_FallsBackToTheDefault_WhenMissing()
    {
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      rubric.Factors[CourseGradeFactor.CourseLength].Parameters.Clear();

      Assert.Equal(5m, CourseQualityGrading.Parameter(rubric, CourseGradeFactor.CourseLength, CourseGradeParameter.StudyHoursPerWeek));
    }
    #endregion
  }
}
