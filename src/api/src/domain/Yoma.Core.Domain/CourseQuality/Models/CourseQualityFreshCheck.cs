namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// What this assessment's fetch and page read proved, used to decide whether an earlier reviewer decision still stands.
  /// </summary>
  public sealed class CourseQualityFreshCheck
  {
    /// <summary>
    /// A rule proved a hard gate failed.
    /// </summary>
    public bool DefiniteFailure { get; set; }

    public bool HttpSuccess { get; set; }

    /// <summary>
    /// The network said nothing either way: a timeout, a block, a server error.
    /// </summary>
    public bool Inconclusive { get; set; }

    public string? FinalHost { get; set; }

    /// <summary>
    /// The page says it is gone or closed anywhere on it.
    /// </summary>
    public bool PageGone { get; set; }

    public bool StrongMatch { get; set; }

    public bool ProviderLinkFailed { get; set; }
  }
}
