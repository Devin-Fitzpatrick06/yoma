using System.Linq.Expressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ICourseQualityService
  {
    /// <summary>
    /// Throws EntityNotFoundException unless an assessment exists and the opportunity is publicly visible.
    /// </summary>
    LearnerCard GetPublicCard(Guid opportunityId);

    /// <summary>
    /// Null when ShowInBrowse is off. Otherwise the courses to rank first: verified by a current assessment.
    /// </summary>
    List<Guid>? ListVerifiedForBrowse();

    /// <summary>
    /// Null when ShowInBrowse is off. Otherwise a sort key to rank by after verified: A first, ungraded last. Translates to SQL.
    /// </summary>
    Expression<Func<Opportunity.Models.Opportunity, object>>? GradeRankForBrowse();

    /// <summary>
    /// Courses with any participation, most popular first. Null when ShowInBrowse is off.
    /// </summary>
    List<Guid>? RankByPopularity(List<MyOpportunity.Models.MyOpportunityParticipationCounts> counts);

    /// <summary>
    /// Sets the status and learner card shown in search on each course, by the same rules as the public card. A course with no current card reads as NeedsReview, never as Verified.
    /// </summary>
    void SetBrowseStatus(List<Opportunity.Models.Opportunity>? opportunities, List<Opportunity.Models.OpportunityInfo>? results);

    /// <summary>
    /// Assesses one course now and regrades the catalogue when anything grading reads changed.
    /// </summary>
    Task<CourseQualityAssessment> Assess(Guid opportunityId, CancellationToken cancellationToken);

    /// <summary>
    /// For the batch job: assesses one course without regrading, and says whether anything grading reads changed.
    /// </summary>
    Task<bool> AssessDue(Guid opportunityId, CancellationToken cancellationToken);

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
