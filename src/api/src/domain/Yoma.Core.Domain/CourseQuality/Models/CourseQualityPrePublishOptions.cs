namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// TEMPORARY (engineer A2, iteration 4): the shape engineer A1 owns in <see cref="CourseQualityOptions"/>. It is here only so
  /// the draft check builds on a base without A1's section; the lead keeps A1's class and deletes this file on merge.
  /// </summary>
  public class CourseQualityPrePublishOptions
  {
    /// <summary>
    /// Off by default: the draft check endpoint answers 404 until turned on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The link check while a partner is typing. A link slower than this is "could not confirm", never a failure.
    /// </summary>
    public int LinkTimeoutMilliseconds { get; set; } = 4000;
  }
}
