using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// The keyless provider: reads schema.org JSON-LD, meta tags and labelled page text. It makes no network call of its
  /// own, working only from what the link checker already fetched.
  ///
  /// Confidence reaches the review threshold only when the page is this listing's page, its dates are confirmed and its
  /// format is known. A page cannot clear a failed rule: the engine lets a provider only lower a criterion.
  ///
  /// A self-paced course can be Verified with no date confirmed, because it has none to confirm: the badge then means live
  /// and matching its page, and the card says "Start anytime". The rules are in <see cref="CourseQualitySelfPaced"/>, and
  /// dates that disagree always go to a person.
  ///
  /// Reasons and summaries are fixed wording. The only page text in the output is the short cost quote, cleaned by
  /// <see cref="CourseQualitySafeText"/>; the timeline is rebuilt from parsed numbers.
  /// </summary>
  public sealed class CourseQualitySourcePageReader : ICourseQualityAIProvider
  {
    #region Class Variables
    public const string ModelName = "source-page-reader";

    private const int MaxTextLength = 200_000;

    private const int DatesConfirmedScore = 100;
    private const int DatesStartConfirmedScore = 90;
    private const int DatesEndConfirmedScore = 70;
    private const int DatesSelfPacedScore = 60;
    private const int DatesSilentScore = 55; // above AIHardGateFailScore: silence is not a failure
    private const int DatesDisagreeScore = 20;
    private const int MatchWeakScore = 40;
    private const int MatchNoneScore = 20;
    private const int SoftNotFoundScore = 10;

    private const decimal ConfidenceConfirmed = 0.9m;
    private const decimal ConfidenceStartConfirmed = 0.75m;
    private const decimal ConfidenceSelfPaced = 0.75m;
    private const decimal ConfidencePartial = 0.6m;
    private const decimal ConfidenceUnconfirmed = 0.5m;
    private const decimal ConfidenceWeakMatch = 0.4m;
    private const decimal ConfidenceNoMatch = 0.2m;
    private const decimal ConfidenceSoftNotFound = 0.1m;
    private const decimal BelowThresholdMargin = 0.01m;

    private readonly CourseQualityOptions _options;
    #endregion

    #region Constructor
    public CourseQualitySourcePageReader(IOptions<CourseQualityOptions> options)
    {
      _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }
    #endregion

    #region Public Members
    public bool IsEnabled => _options.SourcePageReaderEnabled;

    public Task<CourseQualityAIResult?> Evaluate(CourseQualityAIRequest request, CancellationToken cancellationToken)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      cancellationToken.ThrowIfCancellationRequested();

      try
      {
        return Task.FromResult(Read(request));
      }
      catch (RegexMatchTimeoutException)
      {
        // treated as an unread page, which goes to review
        return Task.FromResult<CourseQualityAIResult?>(null);
      }
    }
    #endregion

    #region Private Members
    private CourseQualityAIResult? Read(CourseQualityAIRequest request)
    {
      var text = CourseQualitySafeText.Clean(request.SourcePageText, MaxTextLength);
      if (text == null) return null;

      var result = ReadPage(request, text);
      if (result != null) result.PageGone |= CourseQualityPageMatch.IsGoneAnywhere(text);

      return result;
    }

    private CourseQualityAIResult? ReadPage(CourseQualityAIRequest request, string text)
    {
      if (CourseQualityPageMatch.IsSoftNotFound(request, text))
      {
        var gone = Result(Below(ConfidenceSoftNotFound), "The course link opens a page saying it is closed or cannot be found.", CourseFormat.Unknown, null, Unknown(), Criterion(CourseQualityCriterion.LinkWorks, SoftNotFoundScore,
          "The course link opens a page that says it is closed or cannot be found.", "Update the listing with a working link to the course page."));
        gone.PageGone = true;
        return gone;
      }

      var facts = CourseQualityStructuredData.Read(request.SourcePageSignals, request.Title);

      if (CourseQualityPageMatch.IsUnreadable(request.SourcePageSignals, text, facts)) return null;

      var match = CourseQualityPageMatch.Assess(request, text, facts);

      // nothing on a page that may be another course's says anything about this listing
      if (match != CourseQualityPageMatchLevel.Strong) return Unmatched(match);

      var dates = CourseQualityPageDates.Read(request, text, facts);
      var format = CourseQualityPageFormat.Read(request.SourcePageSignals, text, facts);
      var cost = CourseQualityPageCost.Read(request.SourcePageSignals, text, facts, request.SourcePageTruncated, CourseQualitySelfPaced.IsListed(request, _options));
      var timeline = CourseQualityPageDuration.FromIso(facts.Duration) ?? CourseQualityPageDuration.FromText(text);

      var thin = CourseQualityPageMatch.IsThinBody(text);
      var selfPaced = !thin && CourseQualitySelfPaced.Qualifies(request, text, facts, dates, format, _options);

      var criteria = new List<CourseQualityAICriterionResult>
      {
        selfPaced
          ? Criterion(CourseQualityCriterion.DatesAccurate, DatesSelfPacedScore, "Verified as self-paced: the course can be started at any time, so it has no dates to confirm.", null)
          : DatesCriterion(dates),
        Criterion(CourseQualityCriterion.DataComplete, 100, "The course page matches this listing.", null)
      };

      var result = Result(selfPaced ? ConfidenceSelfPaced : Confidence(dates, format), Summary(dates, format, selfPaced), format, timeline, cost, [.. criteria]);
      result.SelfPaced = selfPaced;
      result.StrongMatch = true;
      result.DatesStartConfirmed = !thin && dates is (CourseQualityDateEvidence.Confirmed or CourseQualityDateEvidence.StartConfirmed);
      result.DatesEndConfirmed = !thin && (dates == CourseQualityDateEvidence.EndConfirmed || (dates == CourseQualityDateEvidence.Confirmed && request.DateEnd.HasValue));

      return result;
    }

    private CourseQualityAIResult Unmatched(CourseQualityPageMatchLevel match)
    {
      var weak = match == CourseQualityPageMatchLevel.Weak;

      return Result(Below(weak ? ConfidenceWeakMatch : ConfidenceNoMatch),
        "The course page could not be matched to this listing.", CourseFormat.Unknown, null, Unknown(),
        Criterion(CourseQualityCriterion.DatesAccurate, DatesSilentScore, "The dates could not be compared, because the page does not match this listing.", null),
        Criterion(CourseQualityCriterion.DataComplete, weak ? MatchWeakScore : MatchNoneScore,
          "The course link opens a page that does not appear to describe this listing.", "Link directly to this course's own page, and keep the title the same on both."));
    }

    private static CourseQualityAICriterionResult DatesCriterion(CourseQualityDateEvidence dates)
    {
      return dates switch
      {
        CourseQualityDateEvidence.Confirmed => Criterion(CourseQualityCriterion.DatesAccurate, DatesConfirmedScore, "The course page confirms the listing dates.", null),
        CourseQualityDateEvidence.StartConfirmed => Criterion(CourseQualityCriterion.DatesAccurate, DatesStartConfirmedScore, "The course page confirms the start date, but does not show an end date.", null),
        CourseQualityDateEvidence.EndConfirmed => Criterion(CourseQualityCriterion.DatesAccurate, DatesEndConfirmedScore, "The course page confirms the end date, but does not show a start date.", "Show the start date on the course page."),
        CourseQualityDateEvidence.SelfPaced => Criterion(CourseQualityCriterion.DatesAccurate, DatesSelfPacedScore, "The course page is self-paced and shows no dates to compare.", null),
        CourseQualityDateEvidence.Silent => Criterion(CourseQualityCriterion.DatesAccurate, DatesSilentScore, "The course page shows no dates to compare with the listing.", "Show the course dates on the course page."),
        CourseQualityDateEvidence.Disagrees => Criterion(CourseQualityCriterion.DatesAccurate, DatesDisagreeScore, "The dates on the course page differ from the listing.", "Update the listing so its dates match the course page."),
        _ => throw new InvalidOperationException($"{nameof(CourseQualityDateEvidence)} of '{dates}' not supported")
      };
    }

    private decimal Confidence(CourseQualityDateEvidence dates, CourseFormat format)
    {
      if (format == CourseFormat.Unknown) return Below(ConfidencePartial);

      return dates switch
      {
        CourseQualityDateEvidence.Confirmed => ConfidenceConfirmed,
        CourseQualityDateEvidence.StartConfirmed => ConfidenceStartConfirmed,
        CourseQualityDateEvidence.EndConfirmed or CourseQualityDateEvidence.SelfPaced => Below(ConfidencePartial),
        _ => Below(ConfidenceUnconfirmed)
      };
    }

    /// <summary>
    /// Weak evidence stays under the threshold wherever the rubric sets it.
    /// </summary>
    private decimal Below(decimal confidence)
    {
      return Math.Max(0m, Math.Min(confidence, _options.ReviewConfidenceThreshold - BelowThresholdMargin));
    }

    private static string Summary(CourseQualityDateEvidence dates, CourseFormat format, bool selfPaced)
    {
      if (selfPaced) return $"Verified as self-paced: the page matches this listing, and its dates were not checked. The page describes a {format.ToString().ToLowerInvariant()}.";

      var read = format == CourseFormat.Unknown ? "The format could not be told from the page." : $"The page describes a {format.ToString().ToLowerInvariant()}.";

      return dates switch
      {
        CourseQualityDateEvidence.Confirmed or CourseQualityDateEvidence.StartConfirmed => $"The page matches this listing and confirms its dates. {read}",
        CourseQualityDateEvidence.Disagrees => $"The page matches this listing, but its dates differ. {read}",
        _ => $"The page matches this listing, but does not confirm its dates. {read}"
      };
    }

    private static LearnerCardCost Unknown() => new() { Type = CourseCostType.Unknown };

    private static CourseQualityAICriterionResult Criterion(CourseQualityCriterion criterion, int score, string reason, string? recommendation) => new()
    {
      Criterion = criterion,
      Score = score,
      Reason = reason,
      Recommendation = recommendation
    };

    private static CourseQualityAIResult Result(decimal confidence, string summary, CourseFormat format, string? timeline, LearnerCardCost cost, params CourseQualityAICriterionResult[] criteria) => new()
    {
      Model = ModelName,
      IsAI = false,
      Confidence = confidence,
      Summary = summary,
      Format = format,
      TimelineSummary = timeline,
      Cost = cost,
      Criteria = [.. criteria]
    };
    #endregion
  }
}
