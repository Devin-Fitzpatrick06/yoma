using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  /// <summary>
  /// The "Did you take it?" prompt and survey. Comments are for Yoma admins only: never returned to partners or the public, and never logged.
  /// </summary>
  public interface ICourseQualityFeedbackService
  {
    /// <summary>
    /// The configured good and bad tags. Returned whether or not feedback is turned on.
    /// </summary>
    CourseFeedbackOptions GetOptions();

    /// <summary>
    /// Records that the signed-in learner left Yoma for a publicly visible course. Does nothing when feedback is turned off, and never
    /// reopens a course the learner has already answered for good.
    /// </summary>
    Task RecordLeft(Guid opportunityId);

    /// <summary>
    /// The one course the signed-in learner is due to be asked about, oldest first, marked as asked. Empty when none is due or feedback
    /// is turned off.
    /// </summary>
    Task<List<CourseFeedbackPrompt>> ListPending();

    /// <summary>
    /// Saves the signed-in learner's answer. A Taken answer may be sent again to update the survey; NotTaken and Dismissed are final.
    /// A Taken answer asks for a regrade, so the grade picks it up within CourseQuality:RegradeDelayInSeconds.
    /// </summary>
    Task Submit(Guid opportunityId, CourseFeedbackSubmission submission);

    /// <summary>
    /// For an Admin, or an OrganizationAdmin of the course's organisation. Comments are filled only for an Admin.
    /// </summary>
    CourseFeedbackSummary GetSummary(Guid opportunityId);

    /// <summary>
    /// Summaries of many courses in a fixed number of queries, keyed by opportunity. Courses nobody said they took are left out.
    /// Comments are filled only when the caller is an Admin.
    /// </summary>
    Dictionary<Guid, CourseFeedbackSummary> ListSummaries(Dictionary<Guid, Guid> organizationIdsByOpportunityId);
  }
}
