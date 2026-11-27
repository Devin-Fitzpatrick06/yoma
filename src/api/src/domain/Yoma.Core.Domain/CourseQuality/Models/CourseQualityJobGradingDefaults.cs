using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Services;

namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// What a job grade reads, worked out once per grading run from the job's latest assessment and listing.
  /// </summary>
  public sealed class JobGradeInput
  {
    public Guid OpportunityId { get; set; }

    public string OpportunityTitle { get; set; } = null!;

    public Guid OrganizationId { get; set; }

    public string OrganizationName { get; set; } = null!;

    public CourseQualityStatus Status { get; set; }

    public List<CourseQualityCriterionResult> Criteria { get; set; } = [];

    public CourseQualityJobRules.JobFacts Facts { get; set; } = null!;

    /// <summary>
    /// Applicant survey answers. Empty until applicants are asked.
    /// </summary>
    public List<CourseGradeFeedbackAnswer> Feedback { get; set; } = [];
  }

  /// <summary>
  /// The default job rubric. A placeholder like the course one: every number is configuration (CourseQuality:Jobs:Grading).
  /// </summary>
  public static class CourseQualityJobGradingDefaults
  {
    public const string Version = "jobs-g1";

    public static class Parameter
    {
      // PayTransparency
      public const string AmountScore = "AmountScore";
      public const string PeriodOnlyScore = "PeriodOnlyScore";
      public const string NoneScore = "NoneScore";
      public const string AMinScore = "AMinScore";

      // ListingSafety
      public const string StatusShare = "StatusShare";
      public const string ScamShare = "ScamShare";

      // PartnerReliability
      public const string PriorPasses = "PriorPasses";
      public const string PriorChecks = "PriorChecks";

      // ApplicantFeedback
      public const string PriorResponses = "PriorResponses";
      public const string PriorMean = "PriorMean";
    }

    public static readonly IReadOnlyDictionary<CourseGradeFactor, (decimal Weight, int NeutralScore, IReadOnlyDictionary<string, decimal> Parameters)> Factors =
      new Dictionary<CourseGradeFactor, (decimal, int, IReadOnlyDictionary<string, decimal>)>
      {
        [CourseGradeFactor.PayTransparency] = (25, 0, Parameters(
          (Parameter.AmountScore, 100), (Parameter.PeriodOnlyScore, 30), (Parameter.NoneScore, 0), (Parameter.AMinScore, 100))),
        [CourseGradeFactor.JobClarity] = (20, 50, Parameters()),
        [CourseGradeFactor.YouthFit] = (20, 50, Parameters()),
        [CourseGradeFactor.ListingSafety] = (15, 50, Parameters((Parameter.StatusShare, 50), (Parameter.ScamShare, 50))),
        [CourseGradeFactor.PartnerReliability] = (10, 50, Parameters((Parameter.PriorPasses, 8), (Parameter.PriorChecks, 10))),
        [CourseGradeFactor.ApplicantFeedback] = (10, 50, Parameters((Parameter.PriorResponses, 10), (Parameter.PriorMean, 0.7m)))
      };

    public static CourseGradeRubric Rubric() => new()
    {
      Version = Version,
      Factors = Factors.ToDictionary(o => o.Key, o => new CourseGradeFactorOptions
      {
        Weight = o.Value.Weight,
        NeutralScore = o.Value.NeutralScore,
        Parameters = new Dictionary<string, decimal>(o.Value.Parameters, StringComparer.OrdinalIgnoreCase)
      }),
      Bands = CourseQualityGradingDefaults.Bands(),
      NeedsReviewMaxGrade = CourseGrade.Ungraded,
      MinFactorsWithData = 3
    };

    /// <summary>
    /// The configured job rubric, with any factor left out of configuration kept at its default.
    /// </summary>
    public static CourseGradeRubric Configured(CourseQualityOptions options)
    {
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var configured = options.Jobs.Grading ?? Rubric();
      var rubric = new CourseGradeRubric
      {
        Version = string.IsNullOrWhiteSpace(configured.Version) ? Version : configured.Version,
        Factors = configured.Factors.Where(o => Factors.ContainsKey(o.Key)).ToDictionary(o => o.Key, o => o.Value),
        Bands = configured.Bands.Count == 0 ? CourseQualityGradingDefaults.Bands() : configured.Bands,
        NeedsReviewMaxGrade = configured.NeedsReviewMaxGrade,
        MinFactorsWithData = configured.MinFactorsWithData
      };

      foreach (var factor in Rubric().Factors) rubric.Factors.TryAdd(factor.Key, factor.Value);

      return CourseQualityGrading.Normalize(rubric);
    }

    private static IReadOnlyDictionary<string, decimal> Parameters(params (string Name, decimal Value)[] values) =>
      values.ToDictionary(o => o.Name, o => o.Value, StringComparer.OrdinalIgnoreCase);
  }
}
