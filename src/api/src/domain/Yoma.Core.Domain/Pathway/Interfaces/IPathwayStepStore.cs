namespace Yoma.Core.Domain.Pathway.Interfaces
{
  /// <summary>
  /// A youth's choices on their path, kept between rebuilds (iteration 4): steps they pinned, and steps they swapped for an
  /// alternative. The engine reads it; the step service (lock, swap, events) owns the storage. The default registration is a
  /// no-op store, so the engine works before the storage exists.
  /// </summary>
  public interface IPathwayStepStore
  {
    /// <summary>Opportunities the youth pinned on their path.</summary>
    Task<List<Guid>> ListLocked(Guid userId);

    /// <summary>Swaps in the order made: the step replaced and the opportunity chosen instead.</summary>
    Task<List<(Guid Replaced, Guid Replacement)>> ListSwaps(Guid userId);
  }
}
