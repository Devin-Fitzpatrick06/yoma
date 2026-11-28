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

    // a job page rarely shows the listing's dates, so a job is judged on its page matching, and its dates on the fixed rules
    private static readonly CourseQualityCriterion[] JudgedCriteriaJob = [CourseQualityCriterion.DataComplete];
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

      // an event or a job gets the listing checks only: no format, no self-paced, no course wording
      var listingOnly = IsListingOnly(opportunity.Type);
      var format = listingOnly ? CourseFormat.Unknown
        : ai != null && ai.Format != CourseFormat.Unknown ? ai.Format : CourseQualityFormatDetector.Detect(opportunity, options);
      var timelineSummary = TimelineSummary(opportunity, ai);
      var hasPageEvidence = link.Outcome == CourseQualityLinkOutcome.Reachable && !string.IsNullOrWhiteSpace(link.SourcePageText);

      var rules = CourseQualityRules.Evaluate(opportunity, link, format, timelineSummary, options, now, !listingOnly && ai?.SelfPaced == true);
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
      var selfPaced = !listingOnly && ai?.SelfPaced == true && hasPageEvidence && format != CourseFormat.Unknown && opportunity.DateStart <= now
        && !opportunity.DateEnd.HasValue && !rules.Any(o => o.Inconclusive) && !criteria.Any(o => o.HardGateFailed);
      var selfPacedHeld = !listingOnly && ai?.SelfPaced == true && !selfPaced;

      // everything else stands on dates a page confirmed: a start still ahead, or an end still ahead
      var startAhead = ai?.DatesStartConfirmed == true && opportunity.DateStart >= now;
      var endAhead = ai?.DatesEndConfirmed == true && opportunity.DateEnd > now;
      var datesCurrent = hasPageEvidence && !dates.HardGateFailed && (startAhead || endAhead);

      // a rule only proves that the listing's dates look real; only a page that was read can confirm them
      var datesConfirmed = datesCurrent && dates.Score == 100 && ai?.Criteria?.Any(o => o.Criterion == CourseQualityCriterion.DatesAccurate) == true;

      // a confidence figure says nothing unless the provider also judged the page
      var isJob = opportunity.Type == Opportunity.Type.Job;
      var judged = ai?.Criteria != null && ai.StrongMatch && (isJob ? JudgedCriteriaJob : JudgedCriteria).All(o => ai.Criteria.Any(c => c.Criterion == o));

      // a person, not the score, settles whether this is the right page
      var pageDoubted = ai?.Criteria?.Any(o => o.Criterion == CourseQualityCriterion.DataComplete && o.Score < options.AIHardGateFailScore) == true;

      var formatInDoubt = !listingOnly && (format == CourseFormat.Unknown || ai?.FormatDisputed == true);
      var needsPerson = formatInDoubt || endDateDisputed || selfPacedHeld || !judged || ai?.PageGone == true || pageDoubted || !(selfPaced || datesCurrent || isJob || criteria.Any(o => o.HardGateFailed));

      var (status, reviewStatus) = Route(score, confidence, criteria.Any(o => o.HardGateFailed), definite, needsPerson, options);

      List<string> reasons = reviewStatus != CourseQualityReviewStatus.Pending ? [] : ReviewReasons(new CourseQualityReviewFacts
      {
        Link = link,
        AI = ai,
        HasPageEvidence = hasPageEvidence,
        Format = format,
        EndDateDisputed = endDateDisputed,
        DatesDiffer = hasPageEvidence && opportunity.DateEnd.HasValue && dates.Score < options.AIHardGateFailScore,
        SelfPacedHeld = selfPacedHeld,
        DatesCurrent = datesCurrent,
        SelfPaced = selfPaced,
        PageDoubted = pageDoubted,
        Judged = judged,
        Confidence = confidence,
        ListingOnly = listingOnly,
        IsJob = isJob
      }, options);

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
        AIUsage = ai?.Usage,
        DateAssessed = now,
        ReviewReasons = reasons,
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
    /// A job: checked from the listing and its feed, with no page reader. A job the feed still lists is open; one a partner
    /// posted on Yoma is open while its link works. A strong scam signal or a link that said nothing either way goes to a
    /// person; a job that is definitely closed is not verified without one.
    /// </summary>
    /// <param name="link">Null for a job from a partner feed.</param>
    public static CourseQualityAssessment AssessJob(Opportunity.Models.Opportunity opportunity, CourseQualityLinkCheckResult? link,
      CourseQualityOptions options, DateTimeOffset now, out CourseQualityFreshCheck fresh)
    {
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var rules = CourseQualityJobRules.Evaluate(opportunity, link, options.Jobs, now, out var signals);
      var criteria = rules.Select(o => new CourseQualityCriterionResult
      {
        Criterion = o.Criterion,
        Score = o.Score,
        Reason = o.Reason,
        Recommendation = o.Recommendation,
        HardGateFailed = o.Failed && options.Criteria.TryGetValue(o.Criterion, out var criterion) && criterion.HardGate
      }).ToList();

      var open = rules.Single(o => o.Criterion == CourseQualityCriterion.JobOpen);
      var definite = open.Failed;
      var inconclusive = rules.Any(o => o.Inconclusive);
      var scam = signals.Any(o => o.Strong);
      var partner = CourseQualityJobRules.FeedPartner(opportunity);

      fresh = new CourseQualityFreshCheck
      {
        DefiniteFailure = definite,
        // the feed is the proof for a pulled job, so its partner stands in for the host a reviewer's decision was made on
        HttpSuccess = partner.HasValue || link?.Outcome == CourseQualityLinkOutcome.Reachable,
        Inconclusive = inconclusive,
        FinalHost = partner.HasValue ? $"feed:{partner.Value.ToString().ToLowerInvariant()}"
          : Uri.TryCreate(link?.FinalUrl, UriKind.Absolute, out var final) ? final.IdnHost.ToLowerInvariant() : null,
        PageGone = false,
        StrongMatch = true,
        ProviderLinkFailed = false
      };

      var score = Score(criteria, options);
      var confidence = Math.Clamp(options.Jobs.RuleConfidence, 0m, 1m);
      if (inconclusive) confidence -= options.InconclusiveLinkConfidencePenalty;
      confidence = Math.Round(Math.Clamp(confidence, 0m, 1m), ConfidenceDecimals, MidpointRounding.AwayFromZero);

      var (status, reviewStatus) = Route(score, confidence, criteria.Any(o => o.HardGateFailed), definite, scam || inconclusive, options);

      List<string> reasons = [];
      if (reviewStatus == CourseQualityReviewStatus.Pending)
      {
        reasons.AddRange(signals.Where(o => o.Strong).Select(o => $"{o.Reason} It reads: \"{o.Quote}\"."));
        if (inconclusive) reasons.Add($"The link to apply could not be confirmed either way ({LinkFailure(link!)}), so confidence was lowered by {options.InconclusiveLinkConfidencePenalty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}.");
      }

      var assessment = new CourseQualityAssessment
      {
        Id = Guid.NewGuid(),
        OpportunityId = opportunity.Id,
        OpportunityTitle = opportunity.Title,
        OrganizationId = opportunity.OrganizationId,
        OrganizationName = opportunity.OrganizationName,
        RubricVersion = options.Jobs.RubricVersion,
        Score = score,
        Confidence = confidence,
        Status = status,
        ReviewStatus = reviewStatus,
        Criteria = criteria,
        AIUsed = false,
        DateAssessed = now,
        ReviewReasons = reasons,
        LearnerCard = new LearnerCard
        {
          OpportunityId = opportunity.Id,
          Status = status,
          Format = CourseFormat.Job,
          DateStart = opportunity.DateStart,
          DateEnd = opportunity.DateEnd,
          CommitmentHours = opportunity.CommitmentIntervalTotalHours,
          Cost = new LearnerCardCost { Type = CourseCostType.Unknown },
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

      // a job's format comes from its type, and a course's can never be Job
      if ((format == CourseFormat.Job) != (assessment.LearnerCard.Format == CourseFormat.Job))
        throw new InvalidOperationException("A job's format cannot be changed, and a course cannot be marked as a job");
      if (format == CourseFormat.Job) return;

      // an event or a job has no format to designate
      if (IsListingOnly(assessment)) return;

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

    /// <summary>
    /// Event and Job get the listing checks only; Learning keeps the full course rubric.
    /// </summary>
    public static bool IsListingOnly(Opportunity.Type type)
    {
      return type != Opportunity.Type.Learning;
    }

    /// <summary>
    /// A stored assessment does not carry its opportunity's type. One from the listing checks only is the one without a format
    /// criterion, because every course assessment has one.
    /// </summary>
    public static bool IsListingOnly(CourseQualityAssessment assessment)
    {
      ArgumentNullException.ThrowIfNull(assessment, nameof(assessment));

      return !assessment.Criteria.Any(o => o.Criterion == CourseQualityCriterion.FormatIdentified);
    }
    #endregion

    #region Private Members
    private sealed class CourseQualityReviewFacts
    {
      public CourseQualityLinkCheckResult Link { get; init; } = null!;
      public CourseQualityAIResult? AI { get; init; }
      public bool HasPageEvidence { get; init; }
      public CourseFormat Format { get; init; }
      public bool EndDateDisputed { get; init; }
      public bool DatesDiffer { get; init; }
      public bool SelfPacedHeld { get; init; }
      public bool DatesCurrent { get; init; }
      public bool SelfPaced { get; init; }
      public bool PageDoubted { get; init; }
      public bool Judged { get; init; }
      public decimal Confidence { get; init; }
      public bool ListingOnly { get; init; }
      public bool IsJob { get; init; }
    }

    /// <summary>
    /// Reviewer-facing: why this result went to a person, one fact per line, in the order a reviewer would check them.
    /// </summary>
    private static List<string> ReviewReasons(CourseQualityReviewFacts facts, CourseQualityOptions options)
    {
      var reasons = new List<string>();
      var inconclusive = facts.Link.Outcome == CourseQualityLinkOutcome.Inconclusive;

      if (inconclusive)
        reasons.Add($"The course link could not be confirmed either way ({LinkFailure(facts.Link)}), so confidence was lowered by {options.InconclusiveLinkConfidencePenalty.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}.");

      if (!facts.HasPageEvidence && !inconclusive)
        reasons.Add($"The course page could not be read, so confidence is capped at {options.RuleOnlyConfidenceCap.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}.");
      else if (facts.HasPageEvidence && facts.AI == null)
        reasons.Add("The course page was fetched, but no page reader checked it.");

      if (facts.AI?.PageGone == true) reasons.Add("The course page says the course is closed or cannot be found.");

      if (facts.HasPageEvidence && facts.AI != null && !facts.AI.StrongMatch && facts.AI.PageGone != true)
        reasons.Add("The course page's title does not clearly match the listing title.");
      else if (facts.PageDoubted) reasons.Add("The course page may not be this listing's page.");
      else if (facts.HasPageEvidence && facts.AI?.StrongMatch == true && !facts.Judged) reasons.Add("The page reader did not judge the dates and details.");

      if (!facts.ListingOnly && facts.Format == CourseFormat.Unknown) reasons.Add("The format, course or webinar, could not be told from the listing or the page.");
      if (!facts.ListingOnly && facts.AI?.FormatDisputed == true) reasons.Add("The page reader and the fixed checks read a different format.");

      if (facts.EndDateDisputed) reasons.Add("The listing has no end date, and the dates on the course page differ from it.");
      else if (facts.DatesDiffer) reasons.Add("The dates on the course page differ from the listing.");

      if (facts.SelfPacedHeld) reasons.Add("The page reads as self-paced, but the listing has an end date, starts later or did not pass every check.");

      if (facts.HasPageEvidence && !facts.SelfPaced && !facts.DatesCurrent && !facts.EndDateDisputed && !facts.DatesDiffer && facts.AI?.StrongMatch == true && !facts.IsJob)
        reasons.Add(facts.ListingOnly
          ? "The page does not confirm the listing's dates."
          : "The course page does not confirm the listing's dates, and the course is not self-paced.");

      if (facts.Confidence < options.ReviewConfidenceThreshold)
        reasons.Add($"Confidence is {facts.Confidence.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}, below the review threshold of {options.ReviewConfidenceThreshold.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}.");

      return reasons;
    }

    private static string LinkFailure(CourseQualityLinkCheckResult link)
    {
      return link.Failure switch
      {
        CourseQualityLinkFailure.Timeout => "it timed out",
        CourseQualityLinkFailure.AccessBlocked => "the site blocked the check",
        CourseQualityLinkFailure.RateLimited => "the site asked to slow down",
        CourseQualityLinkFailure.ServerError => $"the site had a server error{(link.StatusCode.HasValue ? $", {link.StatusCode}" : string.Empty)}",
        CourseQualityLinkFailure.EmptyResponse => "the page was empty",
        CourseQualityLinkFailure.ConnectionError => "the connection failed",
        _ => link.StatusCode.HasValue ? $"status {link.StatusCode}" : "no clear answer"
      };
    }

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
          CourseQualityCriterion.JobOpen => "Job is open",
          CourseQualityCriterion.JobDetailsClear => "Pay and key details stated",
          CourseQualityCriterion.JobFresh => "Recently posted",
          CourseQualityCriterion.NoScamSignals => "No scam warning signs",
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

      var job = assessment.LearnerCard.Format == CourseFormat.Job;
      var noun = job ? "job" : "course";

      string summary;
      // jobs have their own checks and wording; events are the listing-only type
      if (!job && IsListingOnly(assessment))
        summary = assessment.ReviewStatus is CourseQualityReviewStatus.Confirmed or CourseQualityReviewStatus.Overridden
          ? issues.Count > 0
            ? "A reviewer could not verify this listing yet. The items below need attention."
            : "A reviewer could not verify this listing yet. Please check that the listing matches its page."
          : issues.Any(o => o.HardGateFailed)
            ? "This listing is not verified yet because a required check did not pass. The items below need attention."
            : "This listing is not verified yet because its score is below the required level. The items below will raise it.";
      else if (assessment.ReviewStatus is CourseQualityReviewStatus.Confirmed or CourseQualityReviewStatus.Overridden)
        summary = issues.Count > 0
          ? $"A reviewer could not verify this {noun} yet. The items below need attention."
          : job
            ? "A reviewer could not verify this job yet. Please check that the listing matches the job advert."
            : "A reviewer could not verify this course yet. Please check that the listing matches the course page.";
      else if (issues.Any(o => o.HardGateFailed))
        summary = $"This {noun} is not verified yet because a required check did not pass. The items below need attention.";
      else
        summary = $"This {noun} is not verified yet because its score is below the required level. The items below will raise it.";

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
