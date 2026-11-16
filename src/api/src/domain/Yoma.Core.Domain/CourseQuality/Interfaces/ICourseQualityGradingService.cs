using System.Linq.Expressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ICourseQualityGradingService
  {
    /// <summary>
    /// The rubric in force: the admin override when one is saved, otherwise configuration.
    /// </summary>
    CourseGradeRubric GetRubric();

    CourseGradeRubricState GetRubricState();

    /// <summary>
    /// Validates and normalises the rubric, saves it as the override and regrades the catalogue.
    /// </summary>
    Task<CourseGradeRubricState> SaveRubric(CourseGradeRubric rubric);

    /// <summary>
    /// Returns to the configured rubric and regrades the catalogue.
    /// </summary>
    Task<CourseGradeRubricState> ResetRubric();

    /// <summary>
    /// Regrades the catalogue now.
    /// </summary>
    Task<CourseGradeRubricState> Run();

    /// <summary>
    /// Grades every publicly visible course with a latest assessment in one pass and stores the results. Runs one at a time.
    /// </summary>
    Task Regrade();

    /// <summary>
    /// Asks for one regrade a short delay from now (CourseQuality:RegradeDelayInSeconds), unless one is already pending, so a burst of
    /// changes costs one run and the caller does not wait for it. Never throws.
    /// </summary>
    Task RequestRegrade();

    /// <summary>
    /// The scheduled run behind <see cref="RequestRegrade"/>: clears the pending request, then regrades.
    /// </summary>
    Task RunRequestedRegrade();

    /// <summary>
    /// True when a learner's Taken answer was saved after its course's grade was last written, so the next batch regrades.
    /// </summary>
    bool HasUngradedFeedback();

    List<CourseGradeFactorRow> ListFactors();

    /// <summary>
    /// Throws EntityNotFoundException when the course has not been graded.
    /// </summary>
    CourseGradeResult GetGrade(Guid opportunityId);

    Dictionary<Guid, CourseGradeResult> ListGrades(List<Guid> opportunityIds);

    /// <summary>
    /// A sort key for search: 1 for A to 4 for D, 5 for ungraded or not graded yet. Translates to SQL against the grade table.
    /// </summary>
    Expression<Func<Opportunity.Models.Opportunity, object>> GradeRank();

    /// <summary>
    /// Courses whose stored grade is this one. For Ungraded, those with no grade stored are not included.
    /// </summary>
    IQueryable<Guid> QueryByGrade(CourseGrade grade);

    /// <summary>
    /// Courses with a grade from A to D.
    /// </summary>
    IQueryable<Guid> QueryGraded();
  }
}
