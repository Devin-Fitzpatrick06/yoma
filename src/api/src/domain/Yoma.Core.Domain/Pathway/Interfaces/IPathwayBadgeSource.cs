using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Interfaces
{
  /// <summary>
  /// Learner badges for "My Yoma". The badges work registers its implementation in place of <see cref="Services.NoOpPathwayBadgeSource"/>.
  /// </summary>
  public interface IPathwayBadgeSource
  {
    Task<List<LearnerBadge>> ListForUser(Guid userId, MyYoma pathway);
  }
}
