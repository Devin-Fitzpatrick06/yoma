using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  /// <summary>
  /// A provider that calls a model and accounts for the call, whether it answered or not.
  /// </summary>
  public interface ICourseQualityMeteredAIProvider : ICourseQualityAIProvider
  {
    /// <summary>
    /// The result (null unless the model answered within the schema) and the usage (null when no model was called).
    /// Only caller cancellation throws.
    /// </summary>
    Task<CourseQualityAICall> Call(CourseQualityAIRequest request, CancellationToken cancellationToken);
  }
}
