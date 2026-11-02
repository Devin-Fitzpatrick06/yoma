using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Default until a real provider is registered. The engine then runs on rules alone.
  /// </summary>
  public class NoOpCourseQualityAIProvider : ICourseQualityAIProvider
  {
    public bool IsEnabled => false;

    public Task<CourseQualityAIResult?> Evaluate(CourseQualityAIRequest request, CancellationToken cancellationToken)
    {
      return Task.FromResult<CourseQualityAIResult?>(null);
    }
  }
}
