using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.Pathway.Badges.Models
{
  /// <summary>
  /// The learner's current path, as the pathway engine built it: the goal and its stages, in order, with the course ids in each.
  /// The badge calculator needs nothing else from the engine, so the engine can change without touching badges.
  /// </summary>
  /// <param name="GoalSetOn">When the learner set the goal (their pathway profile's creation date); null when they have none.</param>
  public sealed record LearnerBadgePath(DateTimeOffset? GoalSetOn, IReadOnlyList<LearnerBadgeStage> Stages);

  public sealed record LearnerBadgeStage(int Order, string Name, IReadOnlyList<Guid> OpportunityIds);

  /// <summary>
  /// How a completion came to be recorded. Only what a third party stood behind counts as verified.
  /// </summary>
  public enum LearnerBadgeEvidence
  {
    /// <summary>
    /// The organisation approved the learner's proof, imported the completion (CSV) or its platform reported it (partner sync).
    /// </summary>
    Verified,

    /// <summary>
    /// Completed through an organisation's instant-verification action link. Recorded by Yoma, but anyone holding the link can
    /// claim it, so it moves a learner along the path but does not back a skill.
    /// </summary>
    ActionLink,

    /// <summary>
    /// The learner's own word, such as "Yes, I took it" in the course survey. Never counts towards a badge.
    /// </summary>
    SelfReported
  }

  /// <param name="SkillCount">Skills the opportunity awards; a verified completion puts them on the learner's YoID.</param>
  /// <param name="CredentialIssued">A credential for this completion was issued to the learner's wallet.</param>
  /// <param name="Grade">The course's current Zeki grade; null when not graded.</param>
  public sealed record LearnerBadgeCompletion(Guid OpportunityId, Opportunity.Type Type, DateTimeOffset DateCompleted, LearnerBadgeEvidence Evidence,
    int SkillCount, bool CredentialIssued, CourseGrade? Grade);
}
