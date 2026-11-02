using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Opportunity;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// The rubric checks. Pure: same listing, link result and clock give the same answer.
  /// Reasons and recommendations are partner-facing, so they state facts and next steps, never blame.
  /// </summary>
  public static class CourseQualityRules
  {
    #region Class Variables
    private const int WebinarGraceInDays = 1;
    private const int EarliestSelfPacedYear = 2000; // before any web course; the page reader treats earlier years the same way
    #endregion

    #region Public Members
    public static List<CourseQualityRuleResult> Evaluate(Opportunity.Models.Opportunity opportunity, CourseQualityLinkCheckResult link,
      CourseFormat format, string? timelineSummary, CourseQualityOptions options, DateTimeOffset now, bool selfPaced = false)
    {
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));
      ArgumentNullException.ThrowIfNull(link, nameof(link));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      return
      [
        ListingLive(opportunity, format, now, selfPaced),
        LinkWorks(link, options),
        DatesAccurate(opportunity, options, now, selfPaced),
        DataComplete(opportunity, timelineSummary, options, now, selfPaced),
        FormatIdentified(format, options)
      ];
    }
    #endregion

    #region Private Members
    private static CourseQualityRuleResult ListingLive(Opportunity.Models.Opportunity opportunity, CourseFormat format, DateTimeOffset now, bool selfPaced)
    {
      var problems = new List<(string Reason, string Recommendation)>();

      if (opportunity.Status != Status.Active)
        problems.Add(($"The listing status is {opportunity.Status}, not Active.", "Set the listing to Active when the course is open to learners."));

      if (opportunity.OrganizationStatus != OrganizationStatus.Active)
        problems.Add(("The organisation is not active.", "Complete the organisation's approval so that its listings can be shown."));

      if (opportunity.Hidden == true)
        problems.Add(("The listing is hidden.", "Unhide the listing when it is ready for learners."));

      if (opportunity.DateEnd.HasValue && opportunity.DateEnd.Value < now)
        problems.Add(("The end date has passed.", "Update the end date if the course is still running."));

      // a webinar is one session, so with no end date its start is when it was over
      if (format == CourseFormat.Webinar && !selfPaced && !opportunity.DateEnd.HasValue && opportunity.DateStart < now.AddDays(-WebinarGraceInDays))
        problems.Add(("The webinar date has passed.", "Update the date if another session is planned."));

      if (problems.Count == 0)
        return new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.ListingLive,
          Score = 100,
          Reason = "The listing is active, published and within its dates."
        };

      return new CourseQualityRuleResult
      {
        Criterion = CourseQualityCriterion.ListingLive,
        Score = 0,
        Failed = true,
        Reason = string.Join(" ", problems.Select(o => o.Reason)),
        Recommendation = string.Join(" ", problems.Select(o => o.Recommendation))
      };
    }

    private static CourseQualityRuleResult LinkWorks(CourseQualityLinkCheckResult link, CourseQualityOptions options)
    {
      return link.Outcome switch
      {
        CourseQualityLinkOutcome.Reachable => new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.LinkWorks,
          Score = 100,
          Reason = "The course link opens a usable page."
        },
        CourseQualityLinkOutcome.Failed => new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.LinkWorks,
          Score = 0,
          Failed = true,
          Reason = FailedLinkReason(link),
          Recommendation = link.Failure == CourseQualityLinkFailure.Missing
            ? "Add the link where learners can start the course."
            : "Update the listing with a working public link to the course."
        },
        CourseQualityLinkOutcome.Inconclusive => new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.LinkWorks,
          Score = Math.Clamp(options.InconclusiveLinkScore, 0, 100),
          Inconclusive = true,
          Reason = InconclusiveLinkReason(link),
          Recommendation = "Check that the course link opens without a sign-in or access restriction."
        },
        _ => throw new InvalidOperationException($"{nameof(CourseQualityLinkOutcome)} of '{link.Outcome}' not supported")
      };
    }

    private static string FailedLinkReason(CourseQualityLinkCheckResult link)
    {
      return link.Failure switch
      {
        CourseQualityLinkFailure.Missing => "The listing has no course link.",
        CourseQualityLinkFailure.InvalidUrl => "The course link is not a valid web address.",
        CourseQualityLinkFailure.BlockedAddress => "The course link does not point to a public web address.",
        CourseQualityLinkFailure.NotFound => $"The course link returned page not found ({link.StatusCode}).",
        CourseQualityLinkFailure.DomainNotFound => "The domain in the course link does not exist.",
        _ => "The course link does not open a usable page."
      };
    }

    private static string InconclusiveLinkReason(CourseQualityLinkCheckResult link)
    {
      return link.Failure switch
      {
        CourseQualityLinkFailure.Timeout => "The course page took too long to respond.",
        CourseQualityLinkFailure.AccessBlocked => "The course page refused the automated check.",
        CourseQualityLinkFailure.RateLimited => "The course site asked the automated check to slow down.",
        CourseQualityLinkFailure.ServerError => "The course site reported a temporary error.",
        _ => "The course link could not be confirmed either way."
      };
    }

    /// <summary>
    /// DateStart is required on the model, so a partner or import with no real date has to enter something.
    /// A date outside the plausible window is treated as that something. A self-paced listing's start only says when it
    /// was listed, so an old date is fine there; it still fails when it cannot be a date at all or is too far ahead.
    /// </summary>
    private static CourseQualityRuleResult DatesAccurate(Opportunity.Models.Opportunity opportunity, CourseQualityOptions options, DateTimeOffset now, bool selfPaced)
    {
      if (!IsRealDate(opportunity.DateStart, options, now, selfPaced))
        return FailedDates("The start date looks like a placeholder rather than a real date.", "Enter the real course start date.");

      if (opportunity.DateEnd.HasValue && !IsRealDate(opportunity.DateEnd.Value, options, now, selfPaced))
        return FailedDates("The end date looks like a placeholder rather than a real date.", "Enter the real course end date, or remove it if the course has none.");

      if (opportunity.DateEnd.HasValue && opportunity.DateEnd.Value < opportunity.DateStart)
        return FailedDates("The end date is before the start date.", "Correct the start and end dates.");

      return new CourseQualityRuleResult
      {
        Criterion = CourseQualityCriterion.DatesAccurate,
        Score = 100,
        Reason = "The listing dates are real and coherent."
      };
    }

    private static bool IsRealDate(DateTimeOffset value, CourseQualityOptions options, DateTimeOffset now, bool selfPaced)
    {
      var earliest = selfPaced ? new DateTimeOffset(EarliestSelfPacedYear, 1, 1, 0, 0, 0, TimeSpan.Zero) : now.AddYears(-options.DatePlausibleYearsPast);

      return value >= earliest && value <= now.AddYears(options.DatePlausibleYearsFuture);
    }

    private static CourseQualityRuleResult FailedDates(string reason, string recommendation)
    {
      return new CourseQualityRuleResult
      {
        Criterion = CourseQualityCriterion.DatesAccurate,
        Score = 0,
        Failed = true,
        Reason = reason,
        Recommendation = recommendation
      };
    }

    /// <summary>
    /// Exactly what the learner card shows and must come from the listing or page: start date, commitment hours and
    /// timeline. An end date is optional for self-paced courses, cost may honestly be Unknown, and format has its own criterion.
    /// </summary>
    private static CourseQualityRuleResult DataComplete(Opportunity.Models.Opportunity opportunity, string? timelineSummary, CourseQualityOptions options, DateTimeOffset now, bool selfPaced)
    {
      var fields = new (string Name, bool Present)[]
      {
        ("start date", IsRealDate(opportunity.DateStart, options, now, selfPaced)),
        ("time commitment", opportunity.CommitmentIntervalTotalHours.HasValue),
        ("timeline", !string.IsNullOrWhiteSpace(timelineSummary))
      };

      var missing = fields.Where(o => !o.Present).Select(o => o.Name).ToList();

      if (missing.Count == 0)
        return new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.DataComplete,
          Score = 100,
          Reason = "Everything the learner card needs is present."
        };

      return new CourseQualityRuleResult
      {
        Criterion = CourseQualityCriterion.DataComplete,
        Score = (int)Math.Round(100m * (fields.Length - missing.Count) / fields.Length, MidpointRounding.AwayFromZero),
        Failed = true,
        Reason = $"The learner card is missing: {string.Join(", ", missing)}.",
        Recommendation = $"Add to the listing: {string.Join(", ", missing)}."
      };
    }

    private static CourseQualityRuleResult FormatIdentified(CourseFormat format, CourseQualityOptions options)
    {
      if (format == CourseFormat.Unknown)
        return new CourseQualityRuleResult
        {
          Criterion = CourseQualityCriterion.FormatIdentified,
          Score = Math.Clamp(options.FormatUnknownScore, 0, 100),
          Failed = true,
          Reason = "The listing does not make clear whether this is a course or a webinar.",
          Recommendation = "State in the title or description whether this is a course or a webinar."
        };

      return new CourseQualityRuleResult
      {
        Criterion = CourseQualityCriterion.FormatIdentified,
        Score = 100,
        Reason = $"This listing is a {format.ToString().ToLowerInvariant()}."
      };
    }
    #endregion
  }
}
