using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using P = Yoma.Core.Domain.CourseQuality.Models.CourseQualityJobGradingDefaults.Parameter;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Pure grader for jobs: the same jobs, rubric and clock give the same grades. Each factor is an absolute 0 to 100 score with a
  /// plain reason; the bands are set by percentile across the jobs that can be graded, never mixed with courses.
  ///
  /// NotVerified and waiting-for-review jobs are not graded and are left out of the percentiles, but still count towards their
  /// partner's record. Ties break on pay, then youth fit, then clarity; a group still tied takes the band of its middle rank,
  /// except that it reaches A only when it fits there whole.
  /// </summary>
  public static class CourseQualityJobGradingEngine
  {
    #region Class Variables
    private const decimal Percent = 100m;

    private static readonly CourseGradeFactor[] TieBreak = [CourseGradeFactor.PayTransparency, CourseGradeFactor.YouthFit, CourseGradeFactor.JobClarity];
    #endregion

    #region Public Members
    public static List<CourseGradeResult> Grade(List<JobGradeInput> inputs, CourseGradeRubric rubric, DateTimeOffset now)
    {
      ArgumentNullException.ThrowIfNull(inputs, nameof(inputs));
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      var partners = inputs.GroupBy(o => o.OrganizationId)
        .ToDictionary(o => o.Key, o => (Listings: o.Count(), Passed: o.Count(j => j.Status == CourseQualityStatus.Verified)));

      var scored = inputs.Select(o => Score(o, partners[o.OrganizationId], rubric, now)).ToList();

      AssignBands([.. scored.Where(o => o.Result.Caps.Count == 0)], rubric);

      return [.. scored.Select(o => o.Result)];
    }

    /// <summary>
    /// Youth fit as 0 to 100 from what the listing states, or null when it says nothing about experience.
    /// </summary>
    public static (decimal? Score, string Reason) YouthFit(CourseQualityJobRules.JobFacts facts)
    {
      ArgumentNullException.ThrowIfNull(facts, nameof(facts));

      // the field a partner set comes first: it is what the partner meant
      switch (facts.ExperienceLevel)
      {
        case Opportunity.CustomFieldConstants.Job.Experience.Options.None: return (100, "No experience needed.");
        case Opportunity.CustomFieldConstants.Job.Experience.Options.EntryJunior: return (100, "Entry or junior level.");
        case Opportunity.CustomFieldConstants.Job.Experience.Options.Mid: return (50, "Mid level.");
        case Opportunity.CustomFieldConstants.Job.Experience.Options.Senior: return (15, "Senior level.");
      }

      if (facts.YearsRequired.HasValue)
      {
        var years = facts.YearsRequired.Value;
        var score = years switch { 0 => 100, 1 => 90, 2 => 70, 3 => 50, 4 => 35, _ => 15 };
        return (score, years == 1 ? "Asks for 1 year of experience." : $"Asks for {years} years of experience.");
      }

      if (facts.EntryLevelWording) return (95, "Entry level: no experience needed, or an internship, learnership or graduate route.");

      if (facts.MonthsRequired.HasValue)
        return (90, facts.MonthsRequired.Value == 1 ? "Asks for 1 month of experience." : $"Asks for {facts.MonthsRequired.Value} months of experience.");

      if (facts.SchoolLevelOnly) return (85, "Asks for a school-leaving qualification only, such as matric.");

      return (null, "The listing does not say how much experience it needs.");
    }
    #endregion

    #region Private Members
    private sealed class JobGradeScored
    {
      public JobGradeInput Input { get; init; } = null!;

      public CourseGradeResult Result { get; init; } = null!;

      public int Factor(CourseGradeFactor factor) => Result.Factors.Single(o => o.Factor == factor).Score;
    }

    private static JobGradeScored Score(JobGradeInput input, (int Listings, int Passed) partner, CourseGradeRubric rubric, DateTimeOffset now)
    {
      var raw = new List<(CourseGradeFactor Factor, decimal? Score, string Reason)>
      {
        Pay(input, rubric),
        Clarity(input),
        YouthFitFactor(input),
        Safety(input, rubric),
        Partner(input, partner, rubric),
        Feedback(input, rubric)
      };

      var totalWeight = raw.Sum(o => Math.Max(rubric.Factors.TryGetValue(o.Factor, out var value) ? value.Weight : 0, 0));
      if (totalWeight <= 0) throw new InvalidOperationException("Job grading rubric weights must add up to more than zero");

      var factors = raw.Select(o =>
      {
        var options = rubric.Factors.TryGetValue(o.Factor, out var value) ? value : new CourseGradeFactorOptions();
        return new CourseGradeFactorResult
        {
          Factor = o.Factor,
          HasData = o.Score.HasValue,
          Score = o.Score.HasValue ? Clamp(o.Score.Value) : Math.Clamp(options.NeutralScore, 0, 100),
          Weight = Math.Round(Math.Max(options.Weight, 0) * Percent / totalWeight, 2, MidpointRounding.AwayFromZero),
          Reason = o.Reason
        };
      }).ToList();

      var exact = factors.Sum(o => o.Score * Math.Max(rubric.Factors.TryGetValue(o.Factor, out var value) ? value.Weight : 0, 0)) / totalWeight;

      var result = new CourseGradeResult
      {
        OpportunityId = input.OpportunityId,
        Grade = CourseGrade.Ungraded,
        GradeLabel = CourseQualityGrading.Label(rubric, CourseGrade.Ungraded),
        Score = Clamp(exact),
        Factors = factors,
        RubricVersion = rubric.Version,
        DateGraded = now
      };

      var withData = factors.Count(o => o.HasData);
      if (input.Status == CourseQualityStatus.NotVerified)
        result.Caps.Add("Not verified jobs are not graded");
      else if (input.Status == CourseQualityStatus.NeedsReview && rubric.NeedsReviewMaxGrade == CourseGrade.Ungraded)
        result.Caps.Add(CourseQualityGradingEngine.NotReviewedCap);
      else if (withData < rubric.MinFactorsWithData)
        result.Caps.Add($"Data on {withData} of the {rubric.MinFactorsWithData} factors needed to grade");

      return new JobGradeScored { Input = input, Result = result };
    }

    /// <summary>
    /// The same steps as course bands: percentile bands, then the score minimums, the review cap and the A floors, each only
    /// ever moving a job down. One difference: feed jobs are templated, so hundreds can tie, and a tied group that only had to
    /// fit whole would fall past every band it spans. Below A a tied group takes the band of its middle rank; A still takes a
    /// group only when the whole group fits, so A never exceeds its share.
    /// </summary>
    private static void AssignBands(List<JobGradeScored> graded, CourseGradeRubric rubric)
    {
      var ordered = graded
        .OrderByDescending(o => o.Result.Score)
        .ThenByDescending(o => o.Factor(TieBreak[0]))
        .ThenByDescending(o => o.Factor(TieBreak[1]))
        .ThenByDescending(o => o.Factor(TieBreak[2]))
        .ThenBy(o => o.Input.OpportunityId)
        .ToList();

      var count = ordered.Count;
      if (count == 0 || rubric.Bands.Count == 0) return;

      var cutoffs = rubric.Bands.Select((o, i) => i == rubric.Bands.Count - 1 ? count : (int)Math.Floor(count * o.TopPercent / Percent)).ToList();

      var start = 0;
      while (start < count)
      {
        var end = start;
        while (end + 1 < count && SameKey(ordered[start], ordered[end + 1])) end++;

        var middle = (start + end) / 2 + 1;
        var rankBand = cutoffs.FindIndex(o => middle <= o);
        if (rankBand == 0 && end + 1 > cutoffs[0]) rankBand = 1;
        var percentile = (int)Math.Round((count - start) * Percent / count, MidpointRounding.AwayFromZero);

        for (var i = start; i <= end; i++)
        {
          ordered[i].Result.Percentile = percentile;
          Place(ordered[i], rankBand < 0 ? rubric.Bands.Count - 1 : rankBand, rubric);
        }

        start = end + 1;
      }
    }

    private static bool SameKey(JobGradeScored a, JobGradeScored b) =>
      a.Result.Score == b.Result.Score && TieBreak.All(o => a.Factor(o) == b.Factor(o));

    private static void Place(JobGradeScored item, int band, CourseGradeRubric rubric)
    {
      var result = item.Result;
      var bands = rubric.Bands;
      var last = bands.Count - 1;

      void ApplyMinScore()
      {
        while (band < last && result.Score < bands[band].MinScore)
        {
          result.Caps.Add($"Score {result.Score} is below the {bands[band].Grade} minimum of {bands[band].MinScore}");
          band++;
        }
      }

      ApplyMinScore();

      if (item.Input.Status == CourseQualityStatus.NeedsReview)
      {
        var maxIndex = bands.FindIndex(o => o.Grade == rubric.NeedsReviewMaxGrade);
        if (maxIndex > band)
        {
          result.Caps.Add($"Jobs waiting for review reach {rubric.NeedsReviewMaxGrade} at most");
          band = maxIndex;
          ApplyMinScore();
        }
      }

      if (bands[band].Grade == CourseGrade.A)
      {
        var floors = new List<string>();
        if (item.Input.Status != CourseQualityStatus.Verified) floors.Add("A needs a verified job");

        var pay = item.Factor(CourseGradeFactor.PayTransparency);
        var minPay = (int)Get(rubric, CourseGradeFactor.PayTransparency, P.AMinScore);
        if (pay < minPay) floors.Add($"Pay {pay} is below the A floor of {minPay}: a top pick states what it pays");

        if (floors.Count > 0 && band < last)
        {
          result.Caps.AddRange(floors);
          band++;
          ApplyMinScore();
        }
      }

      result.Grade = bands[band].Grade;
      result.GradeLabel = bands[band].Label;
    }

    private static (CourseGradeFactor, decimal?, string) Pay(JobGradeInput input, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.PayTransparency;

      if (input.Facts.PayAmount) return (factor, Get(rubric, factor, P.AmountScore), "The listing states a pay amount.");
      if (input.Facts.PayWithoutAmount) return (factor, Get(rubric, factor, P.PeriodOnlyScore), "The listing names a pay period or \"market related\", but no amount.");
      return (factor, Get(rubric, factor, P.NoneScore), "The listing says nothing about pay.");
    }

    private static (CourseGradeFactor, decimal?, string) Clarity(JobGradeInput input)
    {
      var facts = input.Facts;
      var fields = new (string Name, bool Present)[]
      {
        ("schedule or contract type", facts.Schedule),
        ("location", facts.Location),
        ("requirements", facts.Requirements),
        ("how to apply", facts.HowToApply)
      };

      var missing = fields.Where(o => !o.Present).Select(o => o.Name).ToList();
      var score = Percent * (fields.Length - missing.Count) / fields.Length;

      return (CourseGradeFactor.JobClarity, score, missing.Count == 0
        ? "States the schedule, location, requirements and how to apply."
        : $"Does not state: {string.Join(", ", missing)}.");
    }

    private static (CourseGradeFactor, decimal?, string) YouthFitFactor(JobGradeInput input)
    {
      var (score, reason) = YouthFit(input.Facts);
      return (CourseGradeFactor.YouthFit, score, reason);
    }

    private static (CourseGradeFactor, decimal?, string) Safety(JobGradeInput input, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.ListingSafety;

      var status = input.Status switch
      {
        CourseQualityStatus.Verified => 1m,
        CourseQualityStatus.NeedsReview => 0.5m,
        _ => 0m
      };

      var scam = input.Criteria.FirstOrDefault(o => o.Criterion == CourseQualityCriterion.NoScamSignals);
      var scamScore = scam == null ? 0.5m : Math.Clamp(scam.Score, 0, 100) / Percent;

      var score = Weighted((status, Get(rubric, factor, P.StatusShare)), (scamScore, Get(rubric, factor, P.ScamShare)));
      var scamText = scam == null ? "scam signs not checked" : scam.Score == 100 ? "no scam warning signs" : scam.HardGateFailed ? "a scam warning sign" : "a weak scam warning sign";

      return (factor, score, $"{input.Status}, {scamText}.");
    }

    private static (CourseGradeFactor, decimal?, string) Partner(JobGradeInput input, (int Listings, int Passed) partner, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.PartnerReliability;

      var checks = partner.Listings + Get(rubric, factor, P.PriorChecks);
      var rate = checks > 0 ? (partner.Passed + Get(rubric, factor, P.PriorPasses)) / checks : 0;

      return (factor, rate * Percent, $"{input.OrganizationName}: {partner.Passed} of {partner.Listings} job{Plural(partner.Listings)} verified.");
    }

    private static (CourseGradeFactor, decimal?, string) Feedback(JobGradeInput input, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.ApplicantFeedback;

      var answers = input.Feedback.Select(CourseQualityGradingEngine.AnswerScore).Where(o => o.HasValue).Select(o => o!.Value).ToList();
      if (answers.Count == 0) return (factor, null, "No applicant has answered the survey yet.");

      // shrunk towards the prior until there are enough answers, as course feedback is
      var m = Get(rubric, factor, P.PriorResponses);
      var adjusted = (answers.Sum() + m * Get(rubric, factor, P.PriorMean)) / (answers.Count + m);

      return (factor, adjusted * Percent, $"{answers.Count} applicant answer{Plural(answers.Count)}, averaging {Clamp(answers.Average() * Percent)} out of 100 before adjusting for so few.");
    }

    private static decimal Get(CourseGradeRubric rubric, CourseGradeFactor factor, string name)
    {
      if (rubric.Factors.TryGetValue(factor, out var options) && options.Parameters != null && options.Parameters.TryGetValue(name, out var value)) return value;
      if (CourseQualityJobGradingDefaults.Factors.TryGetValue(factor, out var defaults) && defaults.Parameters.TryGetValue(name, out var fallback)) return fallback;
      throw new InvalidOperationException($"Job grading parameter '{factor}.{name}' is not defined");
    }

    private static decimal Weighted(params (decimal Value, decimal Share)[] parts)
    {
      var total = parts.Sum(o => Math.Max(o.Share, 0));
      if (total <= 0) return 0;

      return parts.Sum(o => Math.Clamp(o.Value, 0, 1) * Math.Max(o.Share, 0)) / total * Percent;
    }

    private static int Clamp(decimal value) => (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
    #endregion
  }
}
