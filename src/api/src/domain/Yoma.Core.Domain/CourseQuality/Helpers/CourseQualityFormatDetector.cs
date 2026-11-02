using System.Text.RegularExpressions;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Tells a course from a webinar using listing wording, commitment and engagement type. Yoma has no format field,
  /// so anything contradictory or silent stays Unknown for a person to settle: this never guesses.
  /// </summary>
  public static partial class CourseQualityFormatDetector
  {
    #region Public Members
    public static CourseFormat Detect(Opportunity.Models.Opportunity opportunity, CourseQualityOptions options)
    {
      ArgumentNullException.ThrowIfNull(opportunity, nameof(opportunity));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var hours = opportunity.CommitmentIntervalTotalHours;
      var runsLong = hours >= options.CourseMinCommitmentHours;
      var runsShort = hours <= options.WebinarMaxCommitmentHours;
      var onSite = opportunity.EngagementType == EngagementTypeOption.OnSite;

      // the title is the partner's own label, so it outranks wording deeper in the description
      var (webinar, course) = Read(opportunity.Title);
      if (!webinar && !course) (webinar, course) = Read(opportunity.Description);

      if (webinar && course)
      {
        if (runsShort && !onSite) return CourseFormat.Webinar;
        return runsLong ? CourseFormat.Course : CourseFormat.Unknown;
      }

      if (webinar) return runsLong || onSite ? CourseFormat.Unknown : CourseFormat.Webinar;
      if (course) return CourseFormat.Course;

      // too long for a single session; a short listing proves nothing, because short courses exist
      return runsLong ? CourseFormat.Course : CourseFormat.Unknown;
    }
    #endregion

    #region Private Members
    private static (bool Webinar, bool Course) Read(string? text)
    {
      if (string.IsNullOrWhiteSpace(text)) return (false, false);

      return (WebinarRegex().IsMatch(text), CourseRegex().IsMatch(text));
    }

    [GeneratedRegex(@"\b(webinars?|web[\s-]?seminars?|online\s+seminars?|live\s+(online\s+)?sessions?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WebinarRegex();

    // "of course" is ordinary English, not a label
    [GeneratedRegex(@"(?<!\bof\s+)\bcourses?\b|\bbootcamps?\b|\bcurriculum\b|\blearnerships?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CourseRegex();
    #endregion
  }
}
