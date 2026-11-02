using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Fixed rules for "Start anytime". Only one proof counts: the verified organisation on its own configured domain. Body
  /// wording can say the opposite of the words it contains, and structured data is the partner's own claim, so neither
  /// qualifies a listing elsewhere. Any date, cohort or schedule on the page sends the listing to a person. Closure wording
  /// is an extra stop here, not the safety boundary.
  /// </summary>
  public static partial class CourseQualitySelfPaced
  {
    #region Class Variables
    private const int RegexTimeoutMilliseconds = 1000;
    private const RegexOptions SafeOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    #endregion

    #region Public Members
    /// <summary>
    /// The caller has already proved a strong match to this listing's own page.
    /// </summary>
    public static bool Qualifies(CourseQualityAIRequest request, string text, CourseQualityStructuredFacts facts, CourseQualityDateEvidence dates, CourseFormat format, CourseQualityOptions options)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      ArgumentNullException.ThrowIfNull(text, nameof(text));
      ArgumentNullException.ThrowIfNull(facts, nameof(facts));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      if (format != CourseFormat.Course || request.DateEnd.HasValue) return false;
      if (dates is not (CourseQualityDateEvidence.Silent or CourseQualityDateEvidence.SelfPaced)) return false;

      if (facts.HasSchedule || CourseQualityPageDates.MentionsDate(text) || ScheduleRegex().IsMatch(text)) return false;
      if (CourseQualityPageMatch.IsGoneAnywhere(text) || CourseQualityPageMatch.IsLoginWalled(request, text)) return false;

      return IsListed(request, options);
    }

    /// <summary>
    /// The listing belongs to a configured organisation and its page really is on that organisation's domain.
    /// </summary>
    public static bool IsListed(CourseQualityAIRequest request, CourseQualityOptions options)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      if (string.IsNullOrWhiteSpace(request.OrganizationName) || !Uri.TryCreate(request.SourcePageFinalUrl, UriKind.Absolute, out var final)) return false;

      var name = request.OrganizationName.Trim();
      var host = final.IdnHost.ToLowerInvariant();

      return options.SelfPacedProviders.Any(o => string.Equals(o.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)
        && o.Domains.Any(domain => OnDomain(host, domain)));
    }
    #endregion

    #region Private Members
    private static bool OnDomain(string host, string domain)
    {
      var value = domain?.Trim().TrimStart('.').ToLowerInvariant();

      return !string.IsNullOrEmpty(value) && (host == value || host.EndsWith($".{value}", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\b(?:classes?\s+meet|meets?\s+(?:on|every|weekly)|\d{1,2}:\d{2}|(?:mon|tues|wednes|thurs|fri|satur|sun)days|cohorts?|intakes?|semesters?|timetable|live\s+(?:online\s+)?(?:class|classes|session|sessions|lecture|lectures)|every\s+(?:mon|tues|wednes|thurs|fri|satur|sun)day|weekly\s+(?:class|classes|session|sessions)|(?:jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|june?|july?|aug(?:ust)?|sep(?:t(?:ember)?)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\s+20\d{2})\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex ScheduleRegex();
    #endregion
  }
}
