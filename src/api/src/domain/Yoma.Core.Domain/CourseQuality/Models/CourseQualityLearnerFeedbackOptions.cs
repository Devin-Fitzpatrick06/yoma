namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// The "Did you take it?" prompt and survey. Off by default, so nobody is asked until it is turned on.
  /// </summary>
  public class CourseQualityLearnerFeedbackOptions
  {
    public bool Enabled { get; set; }

    /// <summary>
    /// How long after leaving for a course a learner is first asked about it. One day by default, a minute in the demo.
    /// </summary>
    public int PromptDelayInMinutes { get; set; } = 1440;

    /// <summary>
    /// After "Not yet", ask again after this many days.
    /// </summary>
    public int SnoozeInDays { get; set; } = 3;

    public int CommentMaxLength { get; set; } = 500;

    /// <summary>
    /// Configuration replaces these lists whole; with none configured, <see cref="ListGood"/> and <see cref="ListBad"/> give the defaults.
    /// </summary>
    public List<CourseFeedbackTag> Good { get; set; } = [];

    public List<CourseFeedbackTag> Bad { get; set; } = [];

    public List<CourseFeedbackTag> ListGood() => Distinct(Good.Count > 0 ? Good : DefaultGood);

    public List<CourseFeedbackTag> ListBad() => Distinct(Bad.Count > 0 ? Bad : DefaultBad);

    public static readonly IReadOnlyList<CourseFeedbackTag> DefaultGood =
    [
      new() { Key = "clearExplanations", Label = "Clear explanations" },
      new() { Key = "practicalSkills", Label = "Practical skills I can use" },
      new() { Key = "goodLength", Label = "Good length" },
      new() { Key = "usefulCertificate", Label = "Useful certificate" },
      new() { Key = "workedOnPhone", Label = "Worked well on my phone" }
    ];

    public static readonly IReadOnlyList<CourseFeedbackTag> DefaultBad =
    [
      new() { Key = "tooLong", Label = "Too long" },
      new() { Key = "tooHard", Label = "Too hard" },
      new() { Key = "linkProblems", Label = "Link or page problems" },
      new() { Key = "notAsDescribed", Label = "Not as described" },
      new() { Key = "hiddenCosts", Label = "Hidden costs" },
      new() { Key = "tooMuchData", Label = "Used too much data" },
      new() { Key = "outdated", Label = "Outdated" }
    ];

    private static List<CourseFeedbackTag> Distinct(IEnumerable<CourseFeedbackTag> tags)
    {
      return [.. tags.Where(o => !string.IsNullOrWhiteSpace(o.Key)).GroupBy(o => o.Key, StringComparer.OrdinalIgnoreCase)
        .Select(o => new CourseFeedbackTag { Key = o.Last().Key, Label = o.Last().Label })];
    }
  }
}
