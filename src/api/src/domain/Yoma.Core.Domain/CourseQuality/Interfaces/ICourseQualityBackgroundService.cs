namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ICourseQualityBackgroundService
  {
    Task ProcessAssessments();

    /// <summary>
    /// Runs a requested regrade. Scheduled by ICourseQualityGradingService.RequestRegrade, whatever CourseQuality:Enabled says.
    /// </summary>
    Task ProcessRegrade();
  }
}
