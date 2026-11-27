using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ICourseQualityDraftService
  {
    /// <summary>
    /// Runs the checks a listing of this type gets after publishing on a draft, storing nothing. Throws EntityNotFoundException
    /// while CourseQuality:PrePublish:Enabled is off, and an unauthorized error unless the caller is an Admin or an admin of the
    /// organisation. A link that cannot be reached in time is reported as not checked, never as a failure.
    /// </summary>
    Task<DraftCheckResult> Check(Guid organizationId, CourseQualityDraftRequest request, CancellationToken cancellationToken);
  }
}
