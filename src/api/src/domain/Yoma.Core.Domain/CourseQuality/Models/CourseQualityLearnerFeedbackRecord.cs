namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Stored form of one learner's feedback on one course: when they left for it, when they were asked, and what they said.
  /// One row per user and course.
  /// </summary>
  public class CourseQualityLearnerFeedbackRecord
  {
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OpportunityId { get; set; }

    public CourseFeedbackAnswer Answer { get; set; }

    public DateTimeOffset DateLeft { get; set; }

    public DateTimeOffset? DateAsked { get; set; }

    /// <summary>
    /// After "Not yet", the learner is not asked again before this.
    /// </summary>
    public DateTimeOffset? SnoozeUntil { get; set; }

    public int? Rating { get; set; }

    public bool? Completed { get; set; }

    public bool? WouldRecommend { get; set; }

    public bool? MatchedDescription { get; set; }

    /// <summary>
    /// JSON list of good tag keys.
    /// </summary>
    public string Good { get; set; } = "[]";

    /// <summary>
    /// JSON list of bad tag keys.
    /// </summary>
    public string Bad { get; set; } = "[]";

    /// <summary>
    /// Seen by Yoma reviewers only, never by partners or the public.
    /// </summary>
    public string? Comment { get; set; }

    public DateTimeOffset DateCreated { get; set; }

    public DateTimeOffset DateModified { get; set; }
  }
}
