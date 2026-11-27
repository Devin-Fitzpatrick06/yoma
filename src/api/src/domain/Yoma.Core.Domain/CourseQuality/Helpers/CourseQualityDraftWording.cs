using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Turns an assessment of a draft into what the partner form shows: each fix on the field it belongs to, and one plain sentence.
  /// The reasons and recommendations are the checks' own partner wording, so a fix reads the same before and after publishing.
  /// </summary>
  public static class CourseQualityDraftWording
  {
    #region Class Variables
    private static readonly string[] Numbers = ["No", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine"];
    #endregion

    #region Public Members
    /// <summary>
    /// The criteria a partner can act on: the same ones a fix note lists. A self-paced course has no dates to fix.
    /// </summary>
    public static List<CourseQualityCriterionResult> Issues(CourseQualityAssessment assessment)
    {
      ArgumentNullException.ThrowIfNull(assessment, nameof(assessment));

      return [.. assessment.Criteria.Where(o => o.HardGateFailed || o.Score < 100)
        .Where(o => !(assessment.LearnerCard.SelfPaced && o.Criterion == CourseQualityCriterion.DatesAccurate))];
    }

    /// <summary>
    /// The form field a criterion's fix belongs to.
    /// </summary>
    public static string Field(CourseQualityCriterionResult criterion, Opportunity.Models.Opportunity opportunity, CourseFormat format,
      CourseQualityOptions options, DateTimeOffset now)
    {
      ArgumentNullException.ThrowIfNull(criterion, nameof(criterion));
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var endPassed = opportunity.DateEnd.HasValue && opportunity.DateEnd.Value < now;
      var startPlausible = IsPlausible(opportunity.DateStart, options, now);

      return criterion.Criterion switch
      {
        CourseQualityCriterion.LinkWorks => DraftCheckField.Url,
        CourseQualityCriterion.FormatIdentified => DraftCheckField.Title,
        CourseQualityCriterion.DatesAccurate => !startPlausible || !opportunity.DateEnd.HasValue ? DraftCheckField.DateStart : DraftCheckField.DateEnd,
        CourseQualityCriterion.DataComplete => DataCompleteField(opportunity, startPlausible),
        CourseQualityCriterion.ListingLive => endPassed ? DraftCheckField.DateEnd
          : format == CourseFormat.Webinar && !opportunity.DateEnd.HasValue && opportunity.DateStart < now ? DraftCheckField.DateStart
          : DraftCheckField.Other,
        CourseQualityCriterion.JobOpen => opportunity.Hidden == true || opportunity.OrganizationStatus != Entity.OrganizationStatus.Active ? DraftCheckField.Other
          : endPassed ? DraftCheckField.DateEnd
          : DraftCheckField.Url,
        CourseQualityCriterion.JobDetailsClear => DraftCheckField.Description,
        CourseQualityCriterion.NoScamSignals => DraftCheckField.Description,
        CourseQualityCriterion.JobFresh => DraftCheckField.DateStart,
        _ => DraftCheckField.Other
      };
    }

    /// <summary>
    /// One plain sentence for the top of the panel, e.g. "Two fixes and this course will be verified."
    /// </summary>
    public static string Summary(CourseQualityStatus status, Opportunity.Type type, int fixes, bool linkChecked)
    {
      var noun = CourseQualityRules.Noun(type);

      return status switch
      {
        CourseQualityStatus.Verified => fixes == 0
          ? $"This {noun} will be verified."
          : $"This {noun} will be verified, and {Count(fixes, true)} would make it stronger.",
        CourseQualityStatus.NotVerified => fixes == 0
          ? $"This {noun} is not verified yet."
          : $"{Count(fixes, false)} and this {noun} will be verified.",
        CourseQualityStatus.NeedsReview => !linkChecked
          ? $"The link could not be opened just now, so a reviewer will look at this {noun} after you publish."
          : fixes == 0
            ? $"A reviewer will look at this {noun} after you publish."
            : $"A reviewer will look at this {noun} after you publish, and {Count(fixes, true)} would help.",
        _ => throw new InvalidOperationException($"{nameof(CourseQualityStatus)} of '{status}' not supported")
      };
    }
    #endregion

    #region Private Members
    private static string DataCompleteField(Opportunity.Models.Opportunity opportunity, bool startPlausible)
    {
      if (!startPlausible) return DraftCheckField.DateStart;
      if (opportunity.Type == Opportunity.Type.Job) return (opportunity.Countries?.Count ?? 0) == 0 ? DraftCheckField.Countries : DraftCheckField.Description;

      // the timeline is built from the time commitment, so both are fixed in one place
      return opportunity.CommitmentIntervalTotalHours.HasValue && opportunity.CommitmentIntervalCount > 0 ? DraftCheckField.Description : DraftCheckField.Commitment;
    }

    private static bool IsPlausible(DateTimeOffset value, CourseQualityOptions options, DateTimeOffset now)
    {
      return value >= now.AddYears(-options.DatePlausibleYearsPast) && value <= now.AddYears(options.DatePlausibleYearsFuture);
    }

    private static string Count(int fixes, bool lowerCase)
    {
      var number = fixes < Numbers.Length ? Numbers[fixes] : fixes.ToString(System.Globalization.CultureInfo.InvariantCulture);
      if (lowerCase) number = number.ToLowerInvariant();
      return $"{number} {(fixes == 1 ? "fix" : "fixes")}";
    }
    #endregion
  }
}
