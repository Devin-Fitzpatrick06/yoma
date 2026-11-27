using Yoma.Core.Domain.Pathway.Interfaces;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// No pins or swaps. Replaced by the stored implementation when it is registered.
  /// </summary>
  public sealed class NoOpPathwayStepStore : IPathwayStepStore
  {
    public Task<List<Guid>> ListLocked(Guid userId) => Task.FromResult(new List<Guid>());

    public Task<List<(Guid Replaced, Guid Replacement)>> ListSwaps(Guid userId) => Task.FromResult(new List<(Guid Replaced, Guid Replacement)>());
  }
}
