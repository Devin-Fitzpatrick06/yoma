using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Pure scorer: combines rule checks and the optional AI result into an assessment and routes it.
  /// A provider result can only lower a criterion, so a failed rule can never be cleared.
  /// </summary>
  public static class CourseQualityEngine
  {
    #region Class Variables
    private const int MaxTextLength = 1000;
    private const int MaxEvidenceLength = 500;
    private const int MaxTimelineLength = 120;
    private const int ConfidenceDecimals = 3;

    private static readonly CourseQualityCriterion[] JudgedCriteria = [CourseQualityCriterion.DatesAccurate, CourseQualityCriterion.DataComplete];
    #endregion

    #region Public Members
    public static CourseQualityAssessment Assess(Opportunity.Models.Opportunity opportunity, CourseQualityLinkCheckResult link,
      CourseQualityAIResult? ai, CourseQualityOptions options, DateTimeOffset now)
    {
      return Assess(opportunity, link, ai, options, now, out _);
    }

    /// <param name="fresh">
    /// What this check proved. A reviewer's Verified lasts only as long as a fresh check backs it.
    /// </param>
    public static CourseQualityAssessment Assess(Opportunity.Models.Opportunity opportunity, CourseQualityLinkCheckResult link,
      CourseQualityAIResult? ai, CourseQualityOptions options, DateTimeOffset now, out CourseQualityFreshCheck fresh)
    {
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));
      ArgumentNullException.ThrowIfNull(link, nameof(link));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var format = ai != null && ai.Format != CourseFormat.Unknown ? ai.Format : CourseQualityFormatDetector.Detect(opportunity, options);
      var timelineSummary = TimelineSummary(opportunity, ai);
      var hasPageEvidence = link.Outcome == CourseQualityLinkOutcome.Reachable && !string.IsNullOrWhiteSpace(link.SourcePageText);

      var rules = CourseQualityRules.Evaluate(opportunity, link, format, timelineSummary, options, now, ai?.SelfPaced == true);
      var criteria = rules.Select(o => Combine(o, opportunity, ai, hasPageEvidence, options)).ToList();

      var definite = rules.Any(o => o.Failed && options.Criteria.TryGetValue(o.Criterion, out var criterion) && criterion.HardGate);

      fresh = new CourseQualityFreshCheck
      {
        DefiniteFailure = definite,
        HttpSuccess = link.Outcome == CourseQualityLinkOutcome.Reachable,
        Inconclusive = link.Outcome == CourseQualityLinkOutcome.Inconclusive,
        FinalHost = Uri.TryCreate(link.FinalUrl, UriKind.Absolute, out var final) ? final.IdnHost.ToLowerInvariant() : null,
        PageGone = ai?.PageGone == true,
        StrongMatch = ai?.StrongMatch == true,
        ProviderLinkFailed = ai?.Criteria?.Any(o => o.Criterion == CourseQualityCriterion.LinkWorks && o.Score < options.AIHardGateFailScore) == true
      };

      var score = Score(criteria, options);
      var confidence = Confidence(rules, ai, hasPageEvidence, options);

      // a page that disagrees with an open-ended listing is a question for a person, not proof of a bad date
      var dates = criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate);
      var endDateDisputed = !opportunity.DateEnd.HasValue && hasPageEvidence && !dates.HardGateFailed && dates.Score < options.AIHardGateFailScore;

      // the provider read the page; the rest it could not know. "Start anytime" is also untrue before the listing opens
      var selfPaced = ai?.SelfPaced == true && hasPageEvidence && format != CourseFormat.Unknown && opportunity.DateStart <= now
        && !opportunity.DateEnd.HasValue && !rules.Any(o => o.Inconclusive) && !criteria.Any(o => o.HardGateFailed);
      var selfPacedHeld = ai?.SelfPaced == true && !selfPaced;

      // everything else stands on dates a page confirmed: a start still ahead, or an end still ahead
      var startAhead = ai?.DatesStartConfirmed == true && opportunity.DateStart >= now;
      var endAhead = ai?.DatesEndConfirmed == true && opportunity.DateEnd > now;
      var datesCurrent = hasPageEvidence && !dates.HardGateFailed && (startAhead || endAhead);

      // a rule only proves that the listing's dates look real; only a page that was read can confirm them
      var datesConfirmed = datesCurrent && dates.Score == 100 && ai?.Criteria?.Any(o => o.Criterion == CourseQualityCriterion.DatesAccurate) == true;

      // a confidence figure says nothing unless the provider also judged the page
      var judged = ai?.Criteria != null && ai.StrongMatch && JudgedCriteria.All(o => ai.Criteria.Any(c => c.Criterion == o));

      // a person, not the score, settles whether this is the right page
      var pageDoubted = ai?.Criteria?.Any(o => o.Criterion == CourseQualityCriterion.DataComplete && o.Score < options.AIHardGateFailScore) == true;

      var needsPerson = format == CourseFormat.Unknown || endDateDisputed || selfPacedHeld || !judged || ai?.PageGone == true || ai?.FormatDisputed == true || pageDoubted || !(selfPaced || datesCurrent || criteria.Any(o => o.HardGateFailed));

      var (status, reviewStatus) = Route(score, confidence, criteria.Any(o => o.HardGateFailed), definite, needsPerson, options);

      var assessment = new CourseQualityAssessment
      {
        Id = Guid.NewGuid(),
        OpportunityId = opportunity.Id,
        OpportunityTitle = opportunity.Title,
        OrganizationId = opportunity.OrganizationId,
        OrganizationName = opportunity.OrganizationName,
        RubricVersion = options.RubricVersion,
        Score = score,
        Confidence = confidence,
        Status = status,
        ReviewStatus = reviewStatus,
        Criteria = criteria,
        AIUsed = ai?.IsAI ?? false,
        AIModel = ai?.Model,
        AISummary = ai == null ? null : Trim(ai.Summary, MaxTextLength),
        DateAssessed = now,
        LearnerCard = new LearnerCard
        {
          OpportunityId = opportunity.Id,
          Status = status,
          Format = format,
          DateStart = opportunity.DateStart,
          DateEnd = opportunity.DateEnd,
          DatesConfirmed = datesConfirmed,
          SelfPaced = selfPaced,
          CommitmentHours = opportunity.CommitmentIntervalTotalHours,
          TimelineSummary = timelineSummary,
          Cost = Cost(ai),
          DateAssessed = now
        }
      };

      RefreshOutputs(assessment);
      return assessment;
    }

    /// <summary>
    /// Rebuilds the card status, highlights and fix note from the assessment's final status.
    /// A fix note exists only for NotVerified, so a NeedsReview listing gets one only once a reviewer resolves it that way.
    /// </summary>
    public static void RefreshOutputs(CourseQualityAssessment assessment)
    {
      ArgumentNullException.ThrowIfNull(assessment, nameof(assessment));

      assessment.LearnerCard.Status = assessment.Status;
      assessment.LearnerCard.Highlights = Highlights(assessment);
      assessment.FixNote = assessment.Status == CourseQualityStatus.NotVerified ? FixNote(assessment) : null;
    }

    /// <summary>
    /// Records a reviewer's format designation and scores the assessment again with it.
    /// </summary>
    public static void ApplyFormat(CourseQualityAssessment assessment, CourseFormat format, CourseQualityOptions options)
    {
      ArgumentNullException.ThrowIfNull(assessment, nameof(assessment));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      assessment.LearnerCard.Format = format;

      var criterion = assessment.Criteria.Single(o => o.Criterion == CourseQualityCriterion.FormatIdentified);
      criterion.Score = 100;
      criterion.HardGateFailed = false;
      criterion.Reason = $"A reviewer confirmed this listing is a {format.ToString().ToLowerInvariant()}.";
      criterion.Recommendation = null;

      assessment.Score = Score(assessment.Criteria, options);
    }

    /// <summary>
    /// What the rubric says once confidence is set aside. A reviewer who agrees with this has confirmed it.
    /// </summary>
    public static CourseQualityStatus ProposedStatus(CourseQualityAssessment assessment, CourseQualityOptions options)
    {
      ArgumentNullException.ThrowIfNull(assessment, nameof(assessment));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      return assessment.Criteria.Any(o => o.HardGateFailed) || assessment.Score < options.VerifiedScoreThreshold
        ? CourseQualityStatus.NotVerified
        : CourseQualityStatus.Verified;
    }
    #endregion

    #region Private Members
    private static CourseQualityCriterionResult Combine(CourseQualityRuleResult rule, Opportunity.Models.Opportunity opportunity, CourseQualityAIResult? ai, bool hasPageEvidence, CourseQualityOptions options)
    {
      var result = new CourseQualityCriterionResult
      {
        Criterion = rule.Criterion,
        Score = rule.Score,
        Reason = rule.Reason,
        Recommendation = rule.Recommendation
      };

      var aiFailed = false;

      // the AI cannot judge a link that the checker could not open
      var aiCriterion = rule.Inconclusive ? null : ai?.Criteria?.Where(o => o.Criterion == rule.Criterion).MinBy(o => o.Score);
      if (aiCriterion != null)
      {
        var aiScore = Math.Clamp(aiCriterion.Score, 0, 100);

        if (aiScore < rule.Score)
        {
          result.Score = aiScore;
          result.Reason = Trim(aiCriterion.Reason, MaxTextLength) ?? rule.Reason;
          result.Recommendation = Trim(aiCriterion.Recommendation, MaxTextLength) ?? rule.Recommendation;
        }

        var openEnded = rule.Criterion == CourseQualityCriterion.DatesAccurate && !opportunity.DateEnd.HasValue;
        aiFailed = hasPageEvidence && !openEnded && aiScore < options.AIHardGateFailScore;
      }

      result.HardGateFailed = (rule.Failed || aiFailed)
        && options.Criteria.TryGetValue(rule.Criterion, out var criterionOptions) && criterionOptions.HardGate;

      return result;
    }

    private static int Score(List<CourseQualityCriterionResult> criteria, CourseQualityOptions options)
    {
      var weighted = criteria.Select(o => (o.Score, Weight: options.Criteria.TryGetValue(o.Criterion, out var value) ? Math.Max(value.Weight, 0) : 0)).ToList();

      var totalWeight = weighted.Sum(o => o.Weight);
      if (totalWeight <= 0) throw new InvalidOperationException($"'{CourseQualityOptions.Section}' criteria weights must add up to more than zero");

      var score = (int)Math.Round((decimal)weighted.Sum(o => o.Score * o.Weight) / totalWeight, MidpointRounding.AwayFromZero);

      return criteria.Any(o => o.HardGateFailed) ? Math.Min(score, options.HardGateScoreCap) : score;
    }

    /// <summary>
    /// Whether the result came from AI or a keyless reader does not matter, only whether a page backs it.
    /// </summary>
    private static decimal Confidence(List<CourseQualityRuleResult> rules, CourseQualityAIResult? ai, bool hasPageEvidence, CourseQualityOptions options)
    {
      var confidence = ai == null ? options.RuleOnlyConfidenceCap : Math.Clamp(ai.Confidence, 0m, 1m);

      if (!hasPageEvidence) confidence = Math.Min(confidence, options.RuleOnlyConfidenceCap);

      if (rules.Any(o => o.Inconclusive)) confidence -= options.InconclusiveLinkConfidencePenalty;

      return Math.Round(Math.Clamp(confidence, 0m, 1m), ConfidenceDecimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// A rule that proved a failure needs no confidence behind it, and the fix note is the useful output, so it comes
    /// first, ahead of an unknown format. Anything else that is unsure goes to a person.
    /// </summary>
    private static (CourseQualityStatus Status, CourseQualityReviewStatus ReviewStatus) Route(int score, decimal confidence, bool hardGateFailed, bool definiteHardGateFailure, bool needsPerson, CourseQualityOptions options)
    {
      if (definiteHardGateFailure)
        return (CourseQualityStatus.NotVerified, CourseQualityReviewStatus.NotRequired);

      if (needsPerson || confidence < options.ReviewConfidenceThreshold)
        return (CourseQualityStatus.NeedsReview, CourseQualityReviewStatus.Pending);

      if (hardGateFailed || score < options.VerifiedScoreThreshold)
        return (CourseQualityStatus.NotVerified, CourseQualityReviewStatus.NotRequired);

      return (CourseQualityStatus.Verified, CourseQualityReviewStatus.NotRequired);
    }

    /// <summary>
    /// A cost without a quote from the source page is a guess, so it stays Unknown.
    /// </summary>
    private static LearnerCardCost Cost(CourseQualityAIResult? ai)
    {
      var cost = ai?.Cost;
      var evidence = Trim(cost?.Evidence, MaxEvidenceLength);

      if (cost == null || cost.Type == CourseCostType.Unknown || evidence == null)
        return new LearnerCardCost { Type = CourseCostType.Unknown };

      var isPaid = cost.Type == CourseCostType.Paid;
      return new LearnerCardCost
      {
        Type = cost.Type,
        Amount = isPaid && cost.Amount >= 0 ? cost.Amount : null,
        Currency = isPaid ? Trim(cost.Currency, 10) : null,
        Evidence = evidence
      };
    }

    /// <summary>
    /// The partner's own listing comes first. Page-derived AI text only fills a gap.
    /// </summary>
    private static string? TimelineSummary(Opportunity.Models.Opportunity opportunity, CourseQualityAIResult? ai)
    {
      if (opportunity.CommitmentInterval.HasValue && opportunity.CommitmentIntervalCount > 0)
      {
        var count = opportunity.CommitmentIntervalCount.Value;
        var interval = opportunity.CommitmentInterval.Value.ToString().ToLowerInvariant();
        return $"{count} {interval}{(count > 1 ? "s" : string.Empty)}";
      }

      return Trim(ai?.TimelineSummary, MaxTimelineLength);
    }

    /// <summary>
    /// Learner-facing, so only checks that passed are stated. A check that did not pass is simply not mentioned.
    /// </summary>
    private static List<string> Highlights(CourseQualityAssessment assessment)
    {
      var results = new List<string>();

      foreach (var item in assessment.Criteria.Where(o => o.Score == 100 && !o.HardGateFailed))
      {
        if (item.Criterion == CourseQualityCriterion.DatesAccurate && !assessment.LearnerCard.DatesConfirmed) continue;
        if (item.Criterion == CourseQualityCriterion.ListingLive && MayBeOver(assessment.LearnerCard)) continue;

        results.Add(item.Criterion switch
        {
          CourseQualityCriterion.ListingLive => "Listing is live",
          CourseQualityCriterion.LinkWorks => "Course link works",
          CourseQualityCriterion.DatesAccurate => "Dates checked",
          CourseQualityCriterion.DataComplete => "Key details provided",
          CourseQualityCriterion.FormatIdentified => "Format confirmed",
          _ => throw new InvalidOperationException($"{nameof(CourseQualityCriterion)} of '{item.Criterion}' not supported")
        });
      }

      if (assessment.LearnerCard.SelfPaced) results.Add("Start anytime");

      if (assessment.ReviewStatus is CourseQualityReviewStatus.Confirmed or CourseQualityReviewStatus.Overridden
        && assessment.Status == CourseQualityStatus.Verified)
        results.Add("Reviewed by the Yoma team");

      return results;
    }

    private static bool MayBeOver(LearnerCard card)
    {
      return !card.SelfPaced && !card.DateEnd.HasValue && card.DateStart < card.DateAssessed;
    }

    private static PartnerFixNote FixNote(CourseQualityAssessment assessment)
    {
      // a self-paced course has no dates to fix, so a partner is not asked for any
      var issues = assessment.Criteria.Where(o => o.HardGateFailed || o.Score < 100)
        .Where(o => !(assessment.LearnerCard.SelfPaced && o.Criterion == CourseQualityCriterion.DatesAccurate))
        .ToList();

      string summary;
      if (assessment.ReviewStatus is CourseQualityReviewStatus.Confirmed or CourseQualityReviewStatus.Overridden)
        summary = issues.Count > 0
          ? "A reviewer could not verify this course yet. The items below need attention."
          : "A reviewer could not verify this course yet. Please check that the listing matches the course page.";
      else if (issues.Any(o => o.HardGateFailed))
        summary = "This course is not verified yet because a required check did not pass. The items below need attention.";
      else
        summary = "This course is not verified yet because its score is below the required level. The items below will raise it.";

      return new PartnerFixNote
      {
        AssessmentId = assessment.Id,
        OpportunityId = assessment.OpportunityId,
        OpportunityTitle = assessment.OpportunityTitle,
        Status = assessment.Status,
        Score = assessment.Score,
        Summary = summary,
        Issues = issues,
        DateAssessed = assessment.DateAssessed
      };
    }

    private static string? Trim(string? value, int maxLength)
    {
      if (string.IsNullOrWhiteSpace(value)) return null;

      var result = value.Trim();
      return result.Length <= maxLength ? result : result[..maxLength];
    }
    #endregion
  }
}
