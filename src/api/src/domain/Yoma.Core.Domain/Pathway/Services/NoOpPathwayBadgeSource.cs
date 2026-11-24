using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// No badges until the badges work is wired in, so My Yoma returns an empty list.
  /// </summary>
  public sealed class NoOpPathwayBadgeSource : IPathwayBadgeSource
  {
    public Task<List<LearnerBadge>> ListForUser(Guid userId, MyYoma pathway) => Task.FromResult(new List<LearnerBadge>());
  }
}
