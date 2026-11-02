using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ICourseQualityLinkChecker
  {
    /// <summary>
    /// Never throws for a bad link: failures come back as a result. Only caller cancellation throws.
    /// </summary>
    Task<CourseQualityLinkCheckResult> Check(string? url, CancellationToken cancellationToken);
  }
}
