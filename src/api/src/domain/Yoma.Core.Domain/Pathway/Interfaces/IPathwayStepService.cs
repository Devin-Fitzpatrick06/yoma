using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Interfaces
{
  /// <summary>
  /// The youth's choices on their path (lock, swap), the step events behind drop-off (W5), pathway health, and the YoID "My path"
  /// summary. Every member for the signed-in youth throws EntityNotFoundException (404) when they have not onboarded.
  /// </summary>
  public interface IPathwayStepService
  {
    /// <summary>
    /// Pins or unpins a step on the youth's path, then returns the rebuilt path.
    /// </summary>
    Task<MyYoma> SetLocked(Guid opportunityId, PathwayStepLockRequest request);

    /// <summary>
    /// Up to three alternatives for a step on the youth's path: the same stage, not already on the path.
    /// </summary>
    Task<List<PathwayCourse>> ListAlternatives(Guid opportunityId);

    /// <summary>
    /// Replaces a step with one of its alternatives, then returns the rebuilt path.
    /// </summary>
    Task<MyYoma> Swap(Guid opportunityId, PathwayStepSwapRequest request);

    /// <summary>
    /// Records one step event. A Shown event repeated for the same step within a short window is dropped.
    /// </summary>
    Task RecordEvent(PathwayStepEventRequest request);

    /// <summary>
    /// Counts of tracked steps and events, and the aggregate drop-off: Collecting until Pathway:DropOffAvailable is on.
    /// </summary>
    PathwayHealth GetHealth();

    /// <summary>
    /// The YoID page's "My path" card.
    /// </summary>
    Task<MyPathSummary> GetSummary();
  }
}
