namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Outcome of one rule check, before rubric weights and the AI result are applied.
  /// </summary>
  public class CourseQualityRuleResult
  {
    public CourseQualityCriterion Criterion { get; set; }

    public int Score { get; set; }

    /// <summary>
    /// The check definitely failed. Whether that fails a hard gate is the rubric's call.
    /// </summary>
    public bool Failed { get; set; }

    /// <summary>
    /// The check could not reach an answer. Never a hard-gate failure.
    /// </summary>
    public bool Inconclusive { get; set; }

    public string Reason { get; set; } = null!;

    public string? Recommendation { get; set; }
  }
}
