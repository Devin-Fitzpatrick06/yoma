using Yoma.Core.Domain.Pathway.Badges.Models;

namespace Yoma.Core.Domain.Pathway.Badges.Interfaces
{
  /// <summary>
  /// Learner badges, computed from the learner's YoID (verified completions, skills, credentials) and Zeki grades. The pathway
  /// engine calls this with the path it built, for MyYoma.badges.
  /// </summary>
  public interface IPathwayBadgeSource
  {
    /// <summary>
    /// Every badge, earned or not, in a fixed order. With no path, the path badges are not earned and VerifiedSkill and
    /// TopPickLearner still are when the learner's completions earn them.
    /// </summary>
    Task<List<LearnerBadge>> ListForUser(Guid userId, LearnerBadgePath? path);
  }

  /// <summary>
  /// The signed-in learner's path for GET pathway/badges/me. The default has none; the pathway engine registers its own.
  /// </summary>
  public interface IPathwayBadgePathProvider
  {
    Task<LearnerBadgePath?> GetForUser(Guid userId);
  }

  public interface IPathwayBadgeService
  {
    /// <summary>
    /// The signed-in learner's badges.
    /// </summary>
    Task<List<LearnerBadge>> ListMine();

    /// <summary>
    /// An active organisation's partner badge. Throws EntityNotFoundException for one that does not exist or is not active.
    /// </summary>
    PartnerBadge GetPartnerBadge(Guid organizationId);

    /// <summary>
    /// Partner badges for a comma-separated list of organisation ids (course cards), in the order asked, each once. Organisations that
    /// do not exist or are not active are left out. Throws ValidationException for no ids, a malformed id or more than
    /// Pathway:PartnerBadges:MaxOrganizationsPerRequest (50).
    /// </summary>
    List<PartnerBadge> ListPartnerBadges(string? organizationIds);
  }
}
