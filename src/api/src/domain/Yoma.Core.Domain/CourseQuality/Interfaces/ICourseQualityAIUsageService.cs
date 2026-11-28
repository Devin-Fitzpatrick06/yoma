using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ICourseQualityAIUsageService
  {
    /// <summary>
    /// The configured model, totals over every stored assessment an AI call ran for (history included), and the most recent runs.
    /// </summary>
    CourseQualityAIUsageSummary GetSummary();
  }
}
