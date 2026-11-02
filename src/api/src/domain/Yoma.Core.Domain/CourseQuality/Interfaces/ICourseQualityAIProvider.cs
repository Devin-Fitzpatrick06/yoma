using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ICourseQualityAIProvider
  {
    /// <summary>
    /// False when no credentials are configured; the engine then runs on rules alone.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns null when the provider declines or fails. Callers treat null as rules-only, never as a pass.
    /// </summary>
    Task<CourseQualityAIResult?> Evaluate(CourseQualityAIRequest request, CancellationToken cancellationToken);
  }
}
