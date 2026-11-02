using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ICourseQualityService
  {
    /// <summary>
    /// Throws EntityNotFoundException unless an assessment exists and the opportunity is publicly visible.
    /// </summary>
    LearnerCard GetPublicCard(Guid opportunityId);

    Task<CourseQualityAssessment> Assess(Guid opportunityId, CancellationToken cancellationToken);

    CourseQualityReviewSearchResults SearchReviews(CourseQualityReviewSearchFilter filter);

    Task<CourseQualityAssessment> Review(Guid assessmentId, CourseQualityReviewDecision decision);

    List<PartnerFixNote> ListFixNotes(Guid organizationId);

    CourseQualityCoverage GetCoverage();

    /// <summary>
    /// Publicly visible courses with no assessment, a changed listing, an older rubric, or an assessment past its interval.
    /// </summary>
    IQueryable<Guid> QueryDue(DateTimeOffset now);
  }
}
