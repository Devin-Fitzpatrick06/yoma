namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Names of the rubric's factor parameters. Shares inside a factor are relative: they need not add up to 100.
  /// </summary>
  public static class CourseGradeParameter
  {
    // LearnerFeedback
    public const string PriorResponses = "PriorResponses";
    public const string PriorMean = "PriorMean";

    // SyllabusAvailable
    public const string CourseJsonLdPoints = "CourseJsonLdPoints";
    public const string JsonLdFieldsPoints = "JsonLdFieldsPoints";
    public const string OutcomesPoints = "OutcomesPoints";
    public const string ModulesPoints = "ModulesPoints";
    public const string PrerequisitesPoints = "PrerequisitesPoints";
    public const string MinListItems = "MinListItems";
    public const string ListingTextFactor = "ListingTextFactor";

    // ProviderReliability: every graded course's organisation is approved and active, so that is not a signal and has no share
    public const string PassRateShare = "PassRateShare";
    public const string DeadLinkShare = "DeadLinkShare";
    public const string LearnerShare = "LearnerShare";
    public const string ListingCountShare = "ListingCountShare";
    public const string PriorPasses = "PriorPasses";
    public const string PriorChecks = "PriorChecks";
    public const string ListingCountCap = "ListingCountCap";

    // Comprehensiveness
    public const string ModulesShare = "ModulesShare";
    public const string ModulesCap = "ModulesCap";
    public const string AssessmentShare = "AssessmentShare";
    public const string CertificateShare = "CertificateShare";
    public const string SkillsShare = "SkillsShare";
    public const string SkillsCap = "SkillsCap";
    public const string DescriptionShare = "DescriptionShare";
    public const string DescriptionCap = "DescriptionCap";

    // CourseLength: Yoma's CommitmentHours counts a day as 24 hours and a week as 168, which is time on the calendar, not
    // study. These turn the listing's interval into realistic study hours; a minute and an hour are taken literally.
    public const string StudyHoursPerDay = "StudyHoursPerDay";
    public const string StudyHoursPerWeek = "StudyHoursPerWeek";
    public const string StudyHoursPerMonth = "StudyHoursPerMonth";
    public const string ShortHours = "ShortHours";
    public const string IdealFromHours = "IdealFromHours";
    public const string IdealToHours = "IdealToHours";
    public const string LongHours = "LongHours";
    public const string VeryLongHours = "VeryLongHours";
    public const string ShortScore = "ShortScore";
    public const string LongScore = "LongScore";
    public const string VeryLongScore = "VeryLongScore";

    // Completion
    public const string PriorStarts = "PriorStarts";

    // ListingIntegrity
    public const string StatusShare = "StatusShare";
    public const string ConfidenceShare = "ConfidenceShare";
    public const string LinkShare = "LinkShare";
    public const string AgeShare = "AgeShare";
    public const string MaxAgeYears = "MaxAgeYears";

    // Accessibility
    public const string FreeScore = "FreeScore";
    public const string FreePaidCertificateScore = "FreePaidCertificateScore";
    public const string PaidScore = "PaidScore";
    public const string UnknownCostScore = "UnknownCostScore";
    public const string LanguageBonus = "LanguageBonus";
    public const string LowDataBonus = "LowDataBonus";

    // Outcomes: each rate is scored against a target that earns 100, so a few percent employment is not read as failure
    public const string MinGroupSize = "MinGroupSize";
    public const string ProgressionShare = "ProgressionShare";
    public const string EmploymentShare = "EmploymentShare";
    public const string ProgressionTarget = "ProgressionTarget";
    public const string EmploymentTarget = "EmploymentTarget";
  }

  /// <summary>
  /// The default rubric from iteration 2's research brief. A placeholder: every number is configuration
  /// (CourseQuality:Grading) and an admin can override it.
  /// </summary>
  public static class CourseQualityGradingDefaults
  {
    public const string Version = "g2-verified-only";

    public static readonly IReadOnlyDictionary<CourseGradeFactor, (decimal Weight, int NeutralScore, IReadOnlyDictionary<string, decimal> Parameters)> Factors =
      new Dictionary<CourseGradeFactor, (decimal, int, IReadOnlyDictionary<string, decimal>)>
      {
        [CourseGradeFactor.LearnerFeedback] = (20, 50, Parameters(
          (CourseGradeParameter.PriorResponses, 10), (CourseGradeParameter.PriorMean, 0.7m))),
        [CourseGradeFactor.SyllabusAvailable] = (18, 0, Parameters(
          (CourseGradeParameter.CourseJsonLdPoints, 15), (CourseGradeParameter.JsonLdFieldsPoints, 15), (CourseGradeParameter.OutcomesPoints, 30),
          (CourseGradeParameter.ModulesPoints, 30), (CourseGradeParameter.PrerequisitesPoints, 10), (CourseGradeParameter.MinListItems, 3),
          (CourseGradeParameter.ListingTextFactor, 0.5m))),
        [CourseGradeFactor.ProviderReliability] = (15, 50, Parameters(
          (CourseGradeParameter.PassRateShare, 40), (CourseGradeParameter.DeadLinkShare, 20),
          (CourseGradeParameter.LearnerShare, 33.33m), (CourseGradeParameter.ListingCountShare, 6.67m), (CourseGradeParameter.PriorPasses, 8),
          (CourseGradeParameter.PriorChecks, 10), (CourseGradeParameter.ListingCountCap, 20))),
        [CourseGradeFactor.Comprehensiveness] = (12, 50, Parameters(
          (CourseGradeParameter.ModulesShare, 30), (CourseGradeParameter.ModulesCap, 15), (CourseGradeParameter.AssessmentShare, 15),
          (CourseGradeParameter.CertificateShare, 15), (CourseGradeParameter.SkillsShare, 20), (CourseGradeParameter.SkillsCap, 5),
          (CourseGradeParameter.DescriptionShare, 20), (CourseGradeParameter.DescriptionCap, 1500))),
        [CourseGradeFactor.CourseLength] = (10, 50, Parameters(
          (CourseGradeParameter.StudyHoursPerDay, 2), (CourseGradeParameter.StudyHoursPerWeek, 5), (CourseGradeParameter.StudyHoursPerMonth, 20),
          (CourseGradeParameter.ShortHours, 0.5m), (CourseGradeParameter.IdealFromHours, 2), (CourseGradeParameter.IdealToHours, 20),
          (CourseGradeParameter.LongHours, 60), (CourseGradeParameter.VeryLongHours, 100), (CourseGradeParameter.ShortScore, 40),
          (CourseGradeParameter.LongScore, 60), (CourseGradeParameter.VeryLongScore, 40))),
        [CourseGradeFactor.Completion] = (10, 50, Parameters((CourseGradeParameter.PriorStarts, 20))),
        [CourseGradeFactor.ListingIntegrity] = (8, 50, Parameters(
          (CourseGradeParameter.StatusShare, 40), (CourseGradeParameter.ConfidenceShare, 20), (CourseGradeParameter.LinkShare, 25),
          (CourseGradeParameter.AgeShare, 15), (CourseGradeParameter.MaxAgeYears, 3))),
        [CourseGradeFactor.Accessibility] = (7, 20, Parameters(
          (CourseGradeParameter.FreeScore, 40), (CourseGradeParameter.FreePaidCertificateScore, 25), (CourseGradeParameter.PaidScore, 0),
          (CourseGradeParameter.UnknownCostScore, 20), (CourseGradeParameter.LanguageBonus, 30), (CourseGradeParameter.LowDataBonus, 30))),
        // weight 0 until outcome data is sufficient (iteration 3, F3); the slot exists so the rubric page can show it
        [CourseGradeFactor.Outcomes] = (0, 50, Parameters(
          (CourseGradeParameter.MinGroupSize, 20), (CourseGradeParameter.ProgressionShare, 60), (CourseGradeParameter.EmploymentShare, 40),
          (CourseGradeParameter.ProgressionTarget, 0.5m), (CourseGradeParameter.EmploymentTarget, 0.2m)))
      };

    public static List<CourseGradeBand> Bands() =>
    [
      // 17, not 15: on the real catalogue large tied groups at the cut-off fall to B, so 15 gave 8.6% A and 17 gives 16.1%
      new() { Grade = CourseGrade.A, Label = "Top pick", TopPercent = 17, MinScore = 65 },
      new() { Grade = CourseGrade.B, Label = "Strong", TopPercent = 40, MinScore = 0 },
      new() { Grade = CourseGrade.C, Label = "Good", TopPercent = 75, MinScore = 0 },
      new() { Grade = CourseGrade.D, Label = "Basic", TopPercent = 100, MinScore = 0 }
    ];

    /// <summary>
    /// With no bands, because configuration binding adds to a list rather than replacing it; <see cref="CourseQualityGrading"/>
    /// fills the default bands when none are configured.
    /// </summary>
    public static CourseGradeRubric Rubric() => new()
    {
      Version = Version,
      Factors = Factors.ToDictionary(o => o.Key, o => new CourseGradeFactorOptions
      {
        Weight = o.Value.Weight,
        NeutralScore = o.Value.NeutralScore,
        Parameters = new Dictionary<string, decimal>(o.Value.Parameters, StringComparer.OrdinalIgnoreCase)
      }),
      NeedsReviewMaxGrade = CourseGrade.Ungraded,
      AMinSyllabusScore = 50,
      AMinFeedbackScore = 60,
      AFeedbackFloorMinResponses = 5,
      MinFactorsWithData = 3
    };

    private static IReadOnlyDictionary<string, decimal> Parameters(params (string Name, decimal Value)[] values) =>
      values.ToDictionary(o => o.Name, o => o.Value, StringComparer.OrdinalIgnoreCase);
  }
}
