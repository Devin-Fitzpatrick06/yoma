namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// How far the publicly visible Learning opportunities are covered. Format, date and card counts come from each
  /// course's latest assessment, so they add up to Assessed, not TotalCourses.
  /// </summary>
  public class CourseQualityCoverage
  {
    public int TotalCourses { get; set; }

    public int Assessed { get; set; }

    public int NotAssessed { get; set; }

    public int MissingOrSuspectDates { get; set; }

    public int Course { get; set; }

    public int Webinar { get; set; }

    public int Unknown { get; set; }

    public int CardReady { get; set; }
  }
}
