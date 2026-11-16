using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using P = Yoma.Core.Domain.CourseQuality.Models.CourseGradeParameter;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Pure grader: the same catalogue, rubric and clock give the same grades. Each factor is an absolute 0 to 100 score with a
  /// plain reason; only the bands are relative, set by percentile across the courses that can be graded.
  ///
  /// NotVerified courses are not graded and are left out of the percentiles, but still count towards their provider's record.
  /// Ties break on feedback, then syllabus, then completion, then length, and a group still tied across a cut-off goes to the
  /// lower band, so A never exceeds its share.
  /// </summary>
  public static class CourseQualityGradingEngine
  {
    #region Class Variables
    private const decimal Percent = 100m;

    /// <summary>
    /// With NeedsReviewMaxGrade set to Ungraded, a course waiting for review is not graded: an unread page grades missing evidence, not
    /// the course. A reviewer's Verified brings it into the next run.
    /// </summary>
    public const string NotReviewedCap = "Not graded until a reviewer confirms it";

    private static readonly CourseGradeFactor[] TieBreak = [CourseGradeFactor.LearnerFeedback, CourseGradeFactor.SyllabusAvailable, CourseGradeFactor.Completion, CourseGradeFactor.CourseLength];
    #endregion

    #region Public Members
    public static List<CourseGradeResult> Grade(List<CourseGradeInput> inputs, CourseGradeRubric rubric, DateTimeOffset now)
    {
      ArgumentNullException.ThrowIfNull(inputs, nameof(inputs));
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      var catalogue = new CourseGradeCatalogue(inputs, rubric);
      var scored = inputs.Select(o => Score(o, catalogue, rubric, now)).ToList();

      var graded = scored.Where(o => o.Result.Caps.Count == 0).ToList();
      AssignBands(graded, rubric);

      return [.. scored.Select(o => o.Result)];
    }

    /// <summary>
    /// Study hours from the listing's commitment. Unlike CommitmentHours, a day or a week is not counted round the clock.
    /// </summary>
    public static decimal? StudyHours(TimeIntervalOption? interval, short? count, CourseGradeRubric rubric)
    {
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      if (!interval.HasValue || !count.HasValue || count.Value <= 0) return null;

      var perUnit = interval.Value switch
      {
        TimeIntervalOption.Minute => 1m / 60,
        TimeIntervalOption.Hour => 1m,
        TimeIntervalOption.Day => CourseQualityGrading.Parameter(rubric, CourseGradeFactor.CourseLength, P.StudyHoursPerDay),
        TimeIntervalOption.Week => CourseQualityGrading.Parameter(rubric, CourseGradeFactor.CourseLength, P.StudyHoursPerWeek),
        TimeIntervalOption.Month => CourseQualityGrading.Parameter(rubric, CourseGradeFactor.CourseLength, P.StudyHoursPerMonth),
        _ => throw new InvalidOperationException($"{nameof(TimeIntervalOption)} of '{interval}' not supported")
      };

      return count.Value * perUnit;
    }

    /// <summary>
    /// Under ShortHours a floor; up to IdealFromHours rising to 100; flat to IdealToHours; then falling to LongScore at LongHours
    /// and VeryLongScore at VeryLongHours, where it stays.
    /// </summary>
    public static int LengthScore(decimal hours, CourseGradeRubric rubric)
    {
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      decimal Get(string name) => CourseQualityGrading.Parameter(rubric, CourseGradeFactor.CourseLength, name);

      var shortHours = Get(P.ShortHours);
      var idealFrom = Get(P.IdealFromHours);
      var idealTo = Get(P.IdealToHours);
      var longHours = Get(P.LongHours);
      var veryLong = Get(P.VeryLongHours);
      var shortScore = Get(P.ShortScore);
      var longScore = Get(P.LongScore);
      var veryLongScore = Get(P.VeryLongScore);

      decimal score;
      if (hours < shortHours) score = shortScore;
      else if (hours < idealFrom) score = Between(hours, shortHours, idealFrom, shortScore, Percent);
      else if (hours <= idealTo) score = Percent;
      else if (hours <= longHours) score = Between(hours, idealTo, longHours, Percent, longScore);
      else if (hours <= veryLong) score = Between(hours, longHours, veryLong, longScore, veryLongScore);
      else score = veryLongScore;

      return Clamp(score);
    }

    /// <summary>
    /// One answer as 0 to 1: half the rating, half whether they would recommend it. With one of the two missing, the other stands alone.
    /// </summary>
    public static decimal? AnswerScore(CourseGradeFeedbackAnswer answer)
    {
      ArgumentNullException.ThrowIfNull(answer, nameof(answer));

      decimal? rating = answer.Rating is >= 1 and <= 5 ? (answer.Rating.Value - 1) / 4m : null;
      decimal? recommend = answer.WouldRecommend.HasValue ? (answer.WouldRecommend.Value ? 1m : 0m) : null;

      if (rating.HasValue && recommend.HasValue) return 0.5m * rating.Value + 0.5m * recommend.Value;
      return rating ?? recommend;
    }
    /// <summary>
    /// Answers that say something about the course: a rating or a recommendation. A "Yes, I took it" that skipped both says nothing, so
    /// it neither moves the LearnerFeedback score nor counts towards the A feedback floor.
    /// </summary>
    public static int ScoredResponses(IEnumerable<CourseGradeFeedbackAnswer> answers)
    {
      ArgumentNullException.ThrowIfNull(answers, nameof(answers));

      return answers.Count(o => AnswerScore(o).HasValue);
    }

    /// <summary>
    /// The LearnerFeedback factor before rounding, 0 to 1: the course's answers shrunk towards its provider's mean, which is itself
    /// shrunk towards the rubric's prior. Null when no answer has a rating or a recommendation. The provider's answers are those of
    /// every graded or gradeable course of the organisation, this one included, exactly as a grading run reads them.
    /// </summary>
    public static decimal? AdjustedFeedback(IEnumerable<CourseGradeFeedbackAnswer> answers, IEnumerable<CourseGradeFeedbackAnswer> providerAnswers, CourseGradeRubric rubric)
    {
      ArgumentNullException.ThrowIfNull(answers, nameof(answers));
      ArgumentNullException.ThrowIfNull(providerAnswers, nameof(providerAnswers));
      ArgumentNullException.ThrowIfNull(rubric, nameof(rubric));

      var scores = answers.Select(AnswerScore).Where(o => o.HasValue).Select(o => o!.Value).ToList();
      if (scores.Count == 0) return null;

      var provider = providerAnswers.Select(AnswerScore).Where(o => o.HasValue).Select(o => o!.Value).ToList();
      return Adjusted(scores, provider.Sum(), provider.Count, rubric);
    }
    #endregion

    #region Private Members
    private static decimal Adjusted(List<decimal> scores, decimal providerSum, int providerResponses, CourseGradeRubric rubric)
    {
      var m = CourseQualityGrading.Parameter(rubric, CourseGradeFactor.LearnerFeedback, P.PriorResponses);
      var cataloguePrior = CourseQualityGrading.Parameter(rubric, CourseGradeFactor.LearnerFeedback, P.PriorMean);

      // the provider's own mean, itself shrunk towards the catalogue prior, so one provider's few answers do not set it
      var prior = providerResponses + m > 0 ? (providerSum + m * cataloguePrior) / (providerResponses + m) : cataloguePrior;
      return (scores.Sum() + m * prior) / (scores.Count + m);
    }

    private sealed class CourseGradeScored
    {
      public CourseGradeInput Input { get; init; } = null!;

      public CourseGradeResult Result { get; init; } = null!;

      public int Responses { get; init; }

      public int Factor(CourseGradeFactor factor) => Result.Factors.Single(o => o.Factor == factor).Score;
    }

    /// <summary>
    /// Catalogue-wide figures worked out once: provider records, feedback priors, the completion prior and length buckets.
    /// </summary>
    private sealed class CourseGradeCatalogue
    {
      public Dictionary<Guid, (int Listings, int Passed, int DeadLinks, decimal FeedbackSum, int Responses)> Providers { get; }

      public decimal FeedbackPrior { get; }

      public decimal CompletionPrior { get; }

      public Dictionary<int, List<decimal>> CompletionRates { get; }

      private readonly CourseGradeRubric _rubric;

      public CourseGradeCatalogue(List<CourseGradeInput> inputs, CourseGradeRubric rubric)
      {
        _rubric = rubric;
        FeedbackPrior = CourseQualityGrading.Parameter(rubric, CourseGradeFactor.LearnerFeedback, P.PriorMean);

        Providers = inputs.GroupBy(o => o.OrganizationId).ToDictionary(o => o.Key, o =>
        {
          var answers = o.SelectMany(c => c.Feedback).Select(AnswerScore).Where(a => a.HasValue).Select(a => a!.Value).ToList();
          return (o.Count(), o.Count(c => c.Status == CourseQualityStatus.Verified), o.Count(LinkDead), answers.Sum(), answers.Count);
        });

        var rates = inputs.Where(o => o.Completed + o.Started > 0).Select(o => (Input: o, Rate: (decimal)o.Completed / (o.Completed + o.Started))).ToList();
        CompletionPrior = Median([.. rates.Select(o => o.Rate)]);

        var priorStarts = CourseQualityGrading.Parameter(rubric, CourseGradeFactor.Completion, P.PriorStarts);
        CompletionRates = rates.GroupBy(o => LengthBucket(o.Input)).ToDictionary(o => o.Key, o => o.Select(r => SmoothedCompletion(r.Input, priorStarts)).OrderBy(r => r).ToList());
      }

      public decimal SmoothedCompletion(CourseGradeInput input, decimal priorStarts)
      {
        var alpha = priorStarts * CompletionPrior;
        return (input.Completed + alpha) / (input.Completed + input.Started + priorStarts);
      }

      /// <summary>
      /// Completion is compared only with courses of a similar length: 0 unknown, 1 short, 2 ideal, 3 long.
      /// </summary>
      public int LengthBucket(CourseGradeInput input)
      {
        var hours = StudyHours(input.CommitmentInterval, input.CommitmentIntervalCount, _rubric);
        if (!hours.HasValue) return 0;
        if (hours < CourseQualityGrading.Parameter(_rubric, CourseGradeFactor.CourseLength, P.IdealFromHours)) return 1;
        return hours <= CourseQualityGrading.Parameter(_rubric, CourseGradeFactor.CourseLength, P.IdealToHours) ? 2 : 3;
      }

      private static decimal Median(List<decimal> values)
      {
        if (values.Count == 0) return 0;

        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
      }
    }

    private static bool LinkDead(CourseGradeInput input) =>
      input.Criteria.Any(o => o.Criterion == CourseQualityCriterion.LinkWorks && o.HardGateFailed);

    private static CourseGradeScored Score(CourseGradeInput input, CourseGradeCatalogue catalogue, CourseGradeRubric rubric, DateTimeOffset now)
    {
      var evidence = input.PageEvidence ?? CourseQualityPageEvidenceExtractor.Extract(null, null, false, input.Description, input.OpportunityTitle);
      var responses = ScoredResponses(input.Feedback);

      var raw = new List<(CourseGradeFactor Factor, decimal? Score, string Reason)>
      {
        LearnerFeedback(input, catalogue, rubric),
        Syllabus(evidence, rubric),
        Provider(input, catalogue, rubric),
        Comprehensiveness(input, evidence, rubric),
        Length(input, rubric),
        Completion(input, catalogue, rubric),
        Integrity(input, rubric, now),
        Accessibility(input, evidence, rubric)
      };

      var totalWeight = rubric.Factors.Values.Sum(o => Math.Max(o.Weight, 0));
      if (totalWeight <= 0) throw new InvalidOperationException("Grading rubric weights must add up to more than zero");

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
        result.Caps.Add("Not verified courses are not graded");
      else if (input.Status == CourseQualityStatus.NeedsReview && rubric.NeedsReviewMaxGrade == CourseGrade.Ungraded)
        result.Caps.Add(NotReviewedCap);
      else if (withData < rubric.MinFactorsWithData)
        result.Caps.Add($"Data on {withData} of the {rubric.MinFactorsWithData} factors needed to grade");

      return new CourseGradeScored { Input = input, Result = result, Responses = responses };
    }

    /// <summary>
    /// Percentile bands, then the score minimums, the review cap and the A floors, each only ever moving a course down. The
    /// rubric page repeats these steps in the browser (src/web/src/lib/courseGrading.ts), so the two must stay in step.
    /// </summary>
    private static void AssignBands(List<CourseGradeScored> graded, CourseGradeRubric rubric)
    {
      var ordered = graded
        .OrderByDescending(o => o.Result.Score)
        .ThenByDescending(o => o.Factor(TieBreak[0]))
        .ThenByDescending(o => o.Factor(TieBreak[1]))
        .ThenByDescending(o => o.Factor(TieBreak[2]))
        .ThenByDescending(o => o.Factor(TieBreak[3]))
        .ThenBy(o => o.Input.OpportunityId)
        .ToList();

      var count = ordered.Count;
      if (count == 0 || rubric.Bands.Count == 0) return;

      // band i holds ranks 1 to cutoffs[i]; the last band holds the rest
      var cutoffs = rubric.Bands.Select((o, i) => i == rubric.Bands.Count - 1 ? count : (int)Math.Floor(count * o.TopPercent / Percent)).ToList();

      var start = 0;
      while (start < count)
      {
        // a tied group shares one band, the best one it fits inside whole, so a group crossing a cut-off falls below it
        var end = start;
        while (end + 1 < count && SameKey(ordered[start], ordered[end + 1])) end++;

        var rankBand = cutoffs.FindIndex(o => end + 1 <= o);
        var percentile = (int)Math.Round((count - start) * Percent / count, MidpointRounding.AwayFromZero);

        for (var i = start; i <= end; i++)
        {
          ordered[i].Result.Percentile = percentile;
          Place(ordered[i], rankBand < 0 ? rubric.Bands.Count - 1 : rankBand, rubric);
        }

        start = end + 1;
      }
    }

    private static bool SameKey(CourseGradeScored a, CourseGradeScored b) =>
      a.Result.Score == b.Result.Score && TieBreak.All(o => a.Factor(o) == b.Factor(o));

    private static void Place(CourseGradeScored item, int band, CourseGradeRubric rubric)
    {
      var result = item.Result;
      var bands = rubric.Bands;
      var last = bands.Count - 1;

      // below a band's minimum a course falls to the next band; the last band takes everything left
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
          result.Caps.Add($"Courses waiting for review reach {rubric.NeedsReviewMaxGrade} at most");
          band = maxIndex;
          ApplyMinScore();
        }
      }

      if (bands[band].Grade == CourseGrade.A)
      {
        var floors = new List<string>();
        if (item.Input.Status != CourseQualityStatus.Verified) floors.Add("A needs a verified course");

        var syllabus = item.Factor(CourseGradeFactor.SyllabusAvailable);
        if (syllabus < rubric.AMinSyllabusScore) floors.Add($"Syllabus {syllabus} is below the A floor of {rubric.AMinSyllabusScore}");

        var feedback = item.Factor(CourseGradeFactor.LearnerFeedback);
        if (item.Responses >= rubric.AFeedbackFloorMinResponses && feedback < rubric.AMinFeedbackScore)
          floors.Add($"Feedback {feedback} is below the A floor of {rubric.AMinFeedbackScore}");

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

    private static (CourseGradeFactor, decimal?, string) LearnerFeedback(CourseGradeInput input, CourseGradeCatalogue catalogue, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.LearnerFeedback;

      var answers = input.Feedback.Select(AnswerScore).Where(o => o.HasValue).Select(o => o!.Value).ToList();
      if (answers.Count == 0) return (factor, null, "No learner has answered the survey yet.");

      var provider = catalogue.Providers[input.OrganizationId];
      var adjusted = Adjusted(answers, provider.FeedbackSum, provider.Responses, rubric);

      return (factor, adjusted * Percent, $"{answers.Count} learner answer{Plural(answers.Count)}, averaging {Clamp(answers.Average() * Percent)} out of 100 before adjusting for so few.");
    }

    private static (CourseGradeFactor, decimal?, string) Syllabus(CourseQualityPageEvidence evidence, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.SyllabusAvailable;
      decimal Get(string name) => CourseQualityGrading.Parameter(rubric, factor, name);

      var minItems = Get(P.MinListItems);
      var found = new List<string>();
      var points = 0m;

      if (evidence.HasCourseJsonLd) { points += Get(P.CourseJsonLdPoints); found.Add("course data"); }
      if (evidence.JsonLdSyllabus || evidence.JsonLdTeaches || evidence.JsonLdCredential) { points += Get(P.JsonLdFieldsPoints); found.Add("syllabus fields in that data"); }
      if (evidence.OutcomeCount >= minItems) { points += Get(P.OutcomesPoints); found.Add($"{evidence.OutcomeCount} learning outcomes"); }
      if (evidence.ModuleCount >= minItems) { points += Get(P.ModulesPoints); found.Add($"{evidence.ModuleCount} modules"); }
      if (evidence.PrerequisitesStated || evidence.AudienceStated) { points += Get(P.PrerequisitesPoints); found.Add("who it is for"); }

      var page = evidence.Source == CourseQualityEvidenceSource.Page;
      if (!page) points *= Get(P.ListingTextFactor);

      var where = page ? "on the course page" : "in the listing text";
      if (found.Count == 0)
        return (factor, page ? 0 : null, page ? "No syllabus, outcomes or module list on the course page." : "No course page was read, and the listing text shows no syllabus.");

      return (factor, points, $"{Capitalise(string.Join(", ", found))} {where}.");
    }

    private static (CourseGradeFactor, decimal?, string) Provider(CourseGradeInput input, CourseGradeCatalogue catalogue, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.ProviderReliability;
      decimal Get(string name) => CourseQualityGrading.Parameter(rubric, factor, name);

      var provider = catalogue.Providers[input.OrganizationId];
      var checks = provider.Listings + Get(P.PriorChecks);
      var passRate = checks > 0 ? (provider.Passed + Get(P.PriorPasses)) / checks : 0;
      var liveShare = 1 - (decimal)provider.DeadLinks / provider.Listings;

      var m = CourseQualityGrading.Parameter(rubric, CourseGradeFactor.LearnerFeedback, P.PriorResponses);
      var learner = provider.Responses + m > 0 ? (provider.FeedbackSum + m * catalogue.FeedbackPrior) / (provider.Responses + m) : catalogue.FeedbackPrior;
      var listings = Math.Min(provider.Listings, Get(P.ListingCountCap)) / Math.Max(Get(P.ListingCountCap), 1);

      // every graded course's organisation is approved and active, so that is no signal and carries no share
      var score = Weighted(
        (passRate, Get(P.PassRateShare)),
        (liveShare, Get(P.DeadLinkShare)),
        (learner, Get(P.LearnerShare)),
        (listings, Get(P.ListingCountShare)));

      return (factor, score, $"{input.OrganizationName}: {provider.Passed} of {provider.Listings} listing{Plural(provider.Listings)} verified, {provider.DeadLinks} with a dead link, {provider.Responses} learner answer{Plural(provider.Responses)}.");
    }

    private static (CourseGradeFactor, decimal?, string) Comprehensiveness(CourseGradeInput input, CourseQualityPageEvidence evidence, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.Comprehensiveness;
      decimal Get(string name) => CourseQualityGrading.Parameter(rubric, factor, name);

      var length = input.Description?.Trim().Length ?? 0;
      if (evidence.ModuleCount == 0 && !evidence.AssessmentMentioned && !evidence.CertificateMentioned && input.SkillCount == 0 && length == 0)
        return (factor, null, "Nothing is known about the course's structure.");

      var modulesCap = Math.Max(Get(P.ModulesCap), 1);
      var modules = (decimal)(Math.Log(1 + Math.Min(evidence.ModuleCount, (double)modulesCap)) / Math.Log(1 + (double)modulesCap));

      var score = Weighted(
        (modules, Get(P.ModulesShare)),
        (evidence.AssessmentMentioned ? 1 : 0, Get(P.AssessmentShare)),
        (evidence.CertificateMentioned ? 1 : 0, Get(P.CertificateShare)),
        (Math.Min(input.SkillCount, Get(P.SkillsCap)) / Math.Max(Get(P.SkillsCap), 1), Get(P.SkillsShare)),
        (Math.Min(length, Get(P.DescriptionCap)) / Math.Max(Get(P.DescriptionCap), 1), Get(P.DescriptionShare)));

      var parts = new List<string> { $"{evidence.ModuleCount} module{Plural(evidence.ModuleCount)}", $"{input.SkillCount} skill{Plural(input.SkillCount)}", $"a {length}-character description" };
      if (evidence.AssessmentMentioned) parts.Add("assessment");
      if (evidence.CertificateMentioned) parts.Add("a certificate");

      return (factor, score, $"{Capitalise(string.Join(", ", parts))}.");
    }

    private static (CourseGradeFactor, decimal?, string) Length(CourseGradeInput input, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.CourseLength;

      var hours = StudyHours(input.CommitmentInterval, input.CommitmentIntervalCount, rubric);
      if (!hours.HasValue) return (factor, null, "The listing does not say how long the course takes.");

      var unit = input.CommitmentInterval!.Value.ToString().ToLowerInvariant();
      return (factor, LengthScore(hours.Value, rubric),
        $"{input.CommitmentIntervalCount} {unit}{Plural(input.CommitmentIntervalCount!.Value)}, about {CourseQualityGrading.Number(Math.Round(hours.Value, 1))} study hours.");
    }

    private static (CourseGradeFactor, decimal?, string) Completion(CourseGradeInput input, CourseGradeCatalogue catalogue, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.Completion;

      var total = input.Completed + input.Started;
      if (total == 0) return (factor, null, "No learner has started this course through Yoma yet.");

      var rate = catalogue.SmoothedCompletion(input, CourseQualityGrading.Parameter(rubric, factor, P.PriorStarts));
      var peers = catalogue.CompletionRates[catalogue.LengthBucket(input)];

      // mid-rank: the share of similar-length courses below, plus half of those level with it
      var below = peers.Count(o => o < rate);
      var level = peers.Count(o => o == rate);
      var percentile = (below + level / 2m) / peers.Count * Percent;

      return (factor, percentile, $"{input.Completed} of {total} learner{Plural(total)} completed, compared with {peers.Count} course{Plural(peers.Count)} of a similar length.");
    }

    private static (CourseGradeFactor, decimal?, string) Integrity(CourseGradeInput input, CourseGradeRubric rubric, DateTimeOffset now)
    {
      const CourseGradeFactor factor = CourseGradeFactor.ListingIntegrity;
      decimal Get(string name) => CourseQualityGrading.Parameter(rubric, factor, name);

      var status = input.Status switch
      {
        CourseQualityStatus.Verified => 1m,
        CourseQualityStatus.NeedsReview => 0.5m,
        _ => 0m
      };

      var link = input.Criteria.FirstOrDefault(o => o.Criterion == CourseQualityCriterion.LinkWorks);
      var linkScore = link == null ? 0.5m : link.HardGateFailed ? 0 : Math.Clamp(link.Score, 0, 100) / Percent;
      var old = input.DateCreated < now.AddYears(-(int)Math.Clamp(Get(P.MaxAgeYears), 0, CourseQualityGrading.MaxAgeYears));

      var score = Weighted(
        (status, Get(P.StatusShare)),
        (Math.Clamp(input.Confidence, 0, 1), Get(P.ConfidenceShare)),
        (linkScore, Get(P.LinkShare)),
        (old ? 0 : 1, Get(P.AgeShare)));

      var linkText = link == null ? "link not checked" : link.HardGateFailed ? "link failed" : link.Score < 100 ? "link not confirmed" : "link works";
      var ageText = old ? $"listed more than {CourseQualityGrading.Number(Get(P.MaxAgeYears))} years ago" : "listed recently";

      return (factor, score, $"{input.Status}, confidence {CourseQualityGrading.Number(Math.Round(input.Confidence, 2))}, {linkText}, {ageText}.");
    }

    private static (CourseGradeFactor, decimal?, string) Accessibility(CourseGradeInput input, CourseQualityPageEvidence evidence, CourseGradeRubric rubric)
    {
      const CourseGradeFactor factor = CourseGradeFactor.Accessibility;
      decimal Get(string name) => CourseQualityGrading.Parameter(rubric, factor, name);

      var parts = new List<string>();
      decimal score;
      switch (input.CostType)
      {
        case CourseCostType.Free when evidence.CertificatePaid:
          score = Get(P.FreePaidCertificateScore);
          parts.Add("free with a paid certificate");
          break;
        case CourseCostType.Free:
          score = Get(P.FreeScore);
          parts.Add("free");
          break;
        case CourseCostType.Paid:
          score = Get(P.PaidScore);
          parts.Add("paid");
          break;
        default:
          score = Get(P.UnknownCostScore);
          parts.Add("cost unknown");
          break;
      }

      var languages = input.Languages.Concat(evidence.Languages).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
      var language = languages.Count >= 2 || languages.Any(o => !o.Equals("English", StringComparison.OrdinalIgnoreCase));
      if (language)
      {
        score += Get(P.LanguageBonus);
        parts.Add(languages.Count >= 2 ? $"{languages.Count} languages" : $"in {languages[0]}");
      }

      var lowData = evidence.MobileMentioned || evidence.OfflineMentioned || evidence.DownloadableMentioned || evidence.TextMentioned;
      if (lowData)
      {
        score += Get(P.LowDataBonus);
        parts.Add("works on a phone, offline or as text");
      }

      var hasData = input.CostType != CourseCostType.Unknown || language || lowData;
      return (factor, hasData ? score : null, $"{Capitalise(string.Join(", ", parts))}.");
    }

    /// <summary>
    /// 0 to 1 parts weighted by relative shares, as 0 to 100.
    /// </summary>
    private static decimal Weighted(params (decimal Value, decimal Share)[] parts)
    {
      var total = parts.Sum(o => Math.Max(o.Share, 0));
      if (total <= 0) return 0;

      return parts.Sum(o => Math.Clamp(o.Value, 0, 1) * Math.Max(o.Share, 0)) / total * Percent;
    }

    private static decimal Between(decimal value, decimal from, decimal to, decimal fromScore, decimal toScore) =>
      to <= from ? toScore : fromScore + (toScore - fromScore) * (value - from) / (to - from);

    private static int Clamp(decimal value) => (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private static string Capitalise(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
    #endregion
  }
}
