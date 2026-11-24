using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// The rubric's shape rules: what an override must satisfy, how it is normalised, and how it is stored.
  /// </summary>
  public static class CourseQualityGrading
  {
    #region Class Variables
    public const int MaxVersionLength = 50;
    public const int MaxLabelLength = 50;
    public const decimal WeightTotal = 100m;

    public const decimal MaxWeight = 1000m;
    public const decimal MaxParameter = 100_000m;
    public const int MaxAgeYears = 50;

    private const int WeightDecimals = 2;

    /// <summary>
    /// Parameters that divide or are divided by: zero would make every regrade fail.
    /// </summary>
    private static readonly (CourseGradeFactor Factor, string Name)[] Positive =
    [
      (CourseGradeFactor.LearnerFeedback, CourseGradeParameter.PriorResponses),
      (CourseGradeFactor.ProviderReliability, CourseGradeParameter.PriorChecks),
      (CourseGradeFactor.ProviderReliability, CourseGradeParameter.ListingCountCap),
      (CourseGradeFactor.Comprehensiveness, CourseGradeParameter.ModulesCap),
      (CourseGradeFactor.Comprehensiveness, CourseGradeParameter.SkillsCap),
      (CourseGradeFactor.Comprehensiveness, CourseGradeParameter.DescriptionCap),
      (CourseGradeFactor.Completion, CourseGradeParameter.PriorStarts),
      (CourseGradeFactor.Outcomes, CourseGradeParameter.MinGroupSize),
      (CourseGradeFactor.Outcomes, CourseGradeParameter.ProgressionTarget),
      (CourseGradeFactor.Outcomes, CourseGradeParameter.EmploymentTarget)
    ];

    /// <summary>
    /// Parameters that are a share of one or a score out of 100.
    /// </summary>
    private static readonly (CourseGradeFactor Factor, string Name, decimal Max)[] Bounded =
    [
      (CourseGradeFactor.LearnerFeedback, CourseGradeParameter.PriorMean, 1),
      (CourseGradeFactor.SyllabusAvailable, CourseGradeParameter.ListingTextFactor, 1),
      (CourseGradeFactor.CourseLength, CourseGradeParameter.ShortScore, 100),
      (CourseGradeFactor.CourseLength, CourseGradeParameter.LongScore, 100),
      (CourseGradeFactor.CourseLength, CourseGradeParameter.VeryLongScore, 100),
      (CourseGradeFactor.ListingIntegrity, CourseGradeParameter.MaxAgeYears, MaxAgeYears),
      (CourseGradeFactor.Accessibility, CourseGradeParameter.FreeScore, 100),
      (CourseGradeFactor.Accessibility, CourseGradeParameter.FreePaidCertificateScore, 100),
      (CourseGradeFactor.Accessibility, CourseGradeParameter.PaidScore, 100),
      (CourseGradeFactor.Accessibility, CourseGradeParameter.UnknownCostScore, 100),
      (CourseGradeFactor.Accessibility, CourseGradeParameter.LanguageBonus, 100),
      (CourseGradeFactor.Accessibility, CourseGradeParameter.LowDataBonus, 100),
      (CourseGradeFactor.Outcomes, CourseGradeParameter.ProgressionTarget, 1),
      (CourseGradeFactor.Outcomes, CourseGradeParameter.EmploymentTarget, 1)
    ];

    private static readonly string[] LengthCurve =
    [
      CourseGradeParameter.ShortHours, CourseGradeParameter.IdealFromHours, CourseGradeParameter.IdealToHours, CourseGradeParameter.LongHours, CourseGradeParameter.VeryLongHours
    ];

    private static readonly JsonSerializerSettings Settings = new() { Converters = [new StringEnumConverter()], ObjectCreationHandling = ObjectCreationHandling.Replace };

    private static readonly CourseGrade[] Graded = [CourseGrade.A, CourseGrade.B, CourseGrade.C, CourseGrade.D];
    #endregion

    #region Public Members
    /// <summary>
    /// The configured rubric, normalised, with the default bands when configuration sets none.
    /// </summary>
    public static CourseGradeRubric Configured(CourseQualityOptions options)
    {
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var rubric = Clone(options.Grading);
      if (rubric.Bands.Count == 0) rubric.Bands = CourseQualityGradingDefaults.Bands();
      if (string.IsNullOrWhiteSpace(rubric.Version)) rubric.Version = CourseQualityGradingDefaults.Version;

      // a factor left out of configuration keeps its default, rather than silently dropping out of the score
      foreach (var factor in CourseQualityGradingDefaults.Rubric().Factors)
        rubric.Factors.TryAdd(factor.Key, factor.Value);

      return Normalize(rubric);
    }

    /// <summary>
    /// A saved rubric from before a factor existed, with that factor added at weight 0, so it keeps grading exactly as it did instead
    /// of failing validation and falling back to configuration.
    /// </summary>
    public static CourseGradeRubric WithNewFactors(CourseGradeRubric rubric)
    {
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      rubric.Factors ??= [];
      foreach (var factor in CourseQualityGradingDefaults.Factors)
      {
        if (rubric.Factors.ContainsKey(factor.Key)) continue;

        rubric.Factors[factor.Key] = new CourseGradeFactorOptions
        {
          Weight = 0,
          NeutralScore = factor.Value.NeutralScore,
          Parameters = new Dictionary<string, decimal>(factor.Value.Parameters, StringComparer.OrdinalIgnoreCase)
        };
      }

      return rubric;
    }

    /// <summary>
    /// Plain messages for an admin, empty when the rubric can be used.
    /// </summary>
    public static List<string> Validate(CourseGradeRubric? rubric)
    {
      if (rubric == null) return ["A rubric is required"];

      var errors = new List<string>();

      if (string.IsNullOrWhiteSpace(rubric.Version) || rubric.Version.Trim().Length > MaxVersionLength)
        errors.Add($"The version must be 1 to {MaxVersionLength} characters");

      rubric.Factors ??= [];
      foreach (var factor in Enum.GetValues<CourseGradeFactor>())
      {
        if (!rubric.Factors.TryGetValue(factor, out var options) || options == null)
        {
          errors.Add($"Factor '{factor}' needs a weight");
          continue;
        }

        if (options.Weight < 0) errors.Add($"Factor '{factor}' has a negative weight");
        if (options.Weight > MaxWeight) errors.Add($"Factor '{factor}' has a weight above {MaxWeight}");
        if (options.NeutralScore is < 0 or > 100) errors.Add($"Factor '{factor}' needs a neutral score from 0 to 100");
        if (options.Parameters?.Values.Any(o => o < 0) == true) errors.Add($"Factor '{factor}' has a negative parameter");
        if (options.Parameters?.Values.Any(o => o > MaxParameter) == true) errors.Add($"Factor '{factor}' has a parameter above {MaxParameter}");
      }

      // each parameter as the engine will read it, the default filling a missing one
      decimal? Value(CourseGradeFactor factor, string name) =>
        rubric.Factors.TryGetValue(factor, out var options) && options?.Parameters != null && options.Parameters.TryGetValue(name, out var value) ? value
          : CourseQualityGradingDefaults.Factors[factor].Parameters.TryGetValue(name, out var fallback) ? fallback : null;

      foreach (var (factor, name) in Positive)
        if (Value(factor, name) <= 0) errors.Add($"Factor '{factor}' needs {name} above 0");

      foreach (var (factor, name, max) in Bounded)
        if (Value(factor, name) > max) errors.Add($"Factor '{factor}' needs {name} of at most {max}");

      var curve = LengthCurve.Select(o => Value(CourseGradeFactor.CourseLength, o) ?? 0).ToList();
      if (curve.Zip(curve.Skip(1)).Any(o => o.Second < o.First))
        errors.Add($"Factor '{CourseGradeFactor.CourseLength}' needs its hours in order: {string.Join(", ", LengthCurve)}");

      if (rubric.Factors.Values.Where(o => o != null).Sum(o => Math.Max(o.Weight, 0)) <= 0) errors.Add("The weights must add up to more than zero");

      // all four, so the review cap always has its band and a lone band cannot sidestep the A floors
      var bands = rubric.Bands ?? [];
      if (bands.Count != Graded.Length || bands.Any(o => o == null) || !bands.Select(o => o.Grade).SequenceEqual(Graded))
        errors.Add($"The bands must be exactly {string.Join(", ", Graded)}, in that order");

      for (var i = 0; i < bands.Count; i++)
      {
        var band = bands[i];
        if (band == null)
        {
          errors.Add($"Band {i + 1} is empty");
          continue;
        }

        if (!Graded.Contains(band.Grade)) errors.Add($"Band {i + 1} must be one of {string.Join(", ", Graded)}");
        if (i > 0 && bands[i - 1] != null && band.Grade <= bands[i - 1].Grade) errors.Add("Bands must run from the best grade to the lowest, each grade once");
        if (string.IsNullOrWhiteSpace(band.Label) || band.Label.Trim().Length > MaxLabelLength) errors.Add($"Band {band.Grade} needs a label of 1 to {MaxLabelLength} characters");
        if (band.TopPercent is <= 0 or > 100) errors.Add($"Band {band.Grade} needs a top percent above 0 and at most 100");
        if (i > 0 && bands[i - 1] != null && band.TopPercent <= bands[i - 1].TopPercent) errors.Add("Top percents must rise from band to band");
        if (band.MinScore is < 0 or > 100) errors.Add($"Band {band.Grade} needs a minimum score from 0 to 100");
      }

      if (bands.Count > 0 && bands[^1] != null && bands[^1].TopPercent != 100) errors.Add("The last band must reach 100 percent");

      // Ungraded here means courses waiting for review are not graded at all
      if (!Enum.IsDefined(rubric.NeedsReviewMaxGrade))
        errors.Add($"The best grade for courses waiting for review must be one of {string.Join(", ", Graded)}, or {CourseGrade.Ungraded} to leave them ungraded");
      if (rubric.AMinSyllabusScore is < 0 or > 100) errors.Add("The syllabus floor for A must be from 0 to 100");
      if (rubric.AMinFeedbackScore is < 0 or > 100) errors.Add("The feedback floor for A must be from 0 to 100");
      if (rubric.AFeedbackFloorMinResponses < 0) errors.Add("The answers needed before the feedback floor applies cannot be negative");
      if (rubric.MinFactorsWithData < 0 || rubric.MinFactorsWithData > Enum.GetValues<CourseGradeFactor>().Length)
        errors.Add($"The factors needed to grade must be from 0 to {Enum.GetValues<CourseGradeFactor>().Length}");

      return [.. errors.Distinct()];
    }

    /// <summary>
    /// A copy with weights scaled to add up to 100, trimmed text, bands best first and case-insensitive parameter names.
    /// </summary>
    public static CourseGradeRubric Normalize(CourseGradeRubric rubric)
    {
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      var result = Clone(rubric);
      result.Version = result.Version.Trim();

      var total = result.Factors.Values.Sum(o => Math.Max(o.Weight, 0));
      foreach (var factor in result.Factors.Values)
      {
        factor.Weight = total <= 0 ? 0 : Math.Round(Math.Max(factor.Weight, 0) * WeightTotal / total, WeightDecimals, MidpointRounding.AwayFromZero);
        factor.Parameters = new Dictionary<string, decimal>(factor.Parameters ?? [], StringComparer.OrdinalIgnoreCase);
      }

      result.Bands = [.. result.Bands.OrderBy(o => o.Grade)];
      result.Bands.ForEach(o => o.Label = o.Label.Trim());

      return result;
    }

    public static string Serialize(CourseGradeRubric rubric)
    {
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      return JsonConvert.SerializeObject(rubric, Settings);
    }

    public static CourseGradeRubric Deserialize(string value)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

      return JsonConvert.DeserializeObject<CourseGradeRubric>(value, Settings)
        ?? throw new InvalidOperationException($"Failed to deserialize stored {nameof(CourseGradeRubric)}");
    }

    /// <summary>
    /// The rubric's value for a parameter, or the default when it is not set. Unknown names are ignored.
    /// </summary>
    public static decimal Parameter(CourseGradeRubric rubric, CourseGradeFactor factor, string name)
    {
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      if (rubric.Factors.TryGetValue(factor, out var options) && options.Parameters != null && options.Parameters.TryGetValue(name, out var value)) return value;

      return CourseQualityGradingDefaults.Factors[factor].Parameters.TryGetValue(name, out var fallback)
        ? fallback
        : throw new InvalidOperationException($"No default for parameter '{name}' of factor '{factor}'");
    }

    public static string Label(CourseGradeRubric rubric, CourseGrade grade)
    {
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      return grade == CourseGrade.Ungraded ? "Not graded" : rubric.Bands.FirstOrDefault(o => o.Grade == grade)?.Label ?? grade.ToString();
    }

    public static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    #endregion

    #region Private Members
    private static CourseGradeRubric Clone(CourseGradeRubric rubric) => Deserialize(Serialize(rubric));
    #endregion
  }
}
