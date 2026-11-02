using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Course or webinar from the page's own type and wording. A Course type outranks everything, because a course may
  /// include live sessions. Contradictory wording stays Unknown for a person.
  /// </summary>
  public static partial class CourseQualityPageFormat
  {
    #region Class Variables
    private const int RegexTimeoutMilliseconds = 1000;
    private const RegexOptions SafeOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    #endregion

    #region Public Members
    public static CourseFormat Read(CourseQualityPageSignals? signals, string text, CourseQualityStructuredFacts facts)
    {
      ArgumentNullException.ThrowIfNull(facts, nameof(facts));

      if (facts.HasCourseType) return CourseFormat.Course;
      if (signals?.MetaTags.GetValueOrDefault("og:type")?.Contains("course", StringComparison.OrdinalIgnoreCase) == true) return CourseFormat.Course;

      var name = string.Join(" | ", signals?.Title, signals?.Heading, signals?.MetaTags.GetValueOrDefault("og:title"));
      var nameWebinar = WebinarNameRegex().IsMatch(name);
      var nameCourse = CourseNameRegex().IsMatch(name);

      if (nameWebinar != nameCourse && !facts.HasEventType) return nameWebinar ? CourseFormat.Webinar : CourseFormat.Course;

      var webinarWording = WebinarWordingRegex().IsMatch(text);
      var liveSession = LiveSessionRegex().IsMatch(text);
      var courseWording = nameCourse || CourseWordingRegex().IsMatch(text);

      if (facts.HasEventType)
      {
        if (facts.EventIsOffline) return CourseFormat.Unknown; // an in-person event is not a webinar
        if (nameWebinar || webinarWording) return CourseFormat.Webinar;
        return courseWording ? CourseFormat.Unknown : CourseFormat.Webinar;
      }

      if (webinarWording) return courseWording ? CourseFormat.Unknown : CourseFormat.Webinar;
      if (courseWording) return CourseFormat.Course;

      return liveSession ? CourseFormat.Webinar : CourseFormat.Unknown;
    }
    #endregion

    #region Private Members
    [GeneratedRegex(@"\b(?:webinars?|web[\s-]seminars?|online\s+seminars?)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex WebinarNameRegex();

    // singular: "Courses" in a page title names a catalogue, not this page
    [GeneratedRegex(@"\b(?:course|bootcamp)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex CourseNameRegex();

    [GeneratedRegex(@"\b(?:register\s+(?:now\s+)?for\s+(?:this|the|our)\s+(?:free\s+)?(?:webinar|online\s+seminar)|(?:join|attend)\s+(?:this|the|our)\s+(?:free\s+)?webinar|this\s+webinar)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex WebinarWordingRegex();

    [GeneratedRegex(@"\blive\s+(?:online\s+)?sessions?\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex LiveSessionRegex();

    [GeneratedRegex(@"\b(?:this\s+course|about\s+the\s+course|course\s+(?:overview|syllabus|curriculum|content|outline|modules?)|(?:enrol|enroll)\s+in\s+(?:this|the)\s+course|start\s+(?:this\s+)?course)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex CourseWordingRegex();
    #endregion
  }
}
