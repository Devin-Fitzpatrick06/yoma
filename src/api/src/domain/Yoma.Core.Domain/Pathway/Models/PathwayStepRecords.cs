namespace Yoma.Core.Domain.Pathway.Models
{
  /// <summary>
  /// A step the youth pinned on their path. One row per user and opportunity; unpinning deletes it.
  /// </summary>
  public class PathwayStepLockRecord
  {
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OpportunityId { get; set; }

    public DateTimeOffset DateCreated { get; set; }
  }

  /// <summary>
  /// A step the youth replaced with one of its alternatives, in the order made.
  /// </summary>
  public class PathwayStepSwapRecord
  {
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid ReplacedOpportunityId { get; set; }

    public Guid ReplacementOpportunityId { get; set; }

    public DateTimeOffset DateCreated { get; set; }
  }

  /// <summary>
  /// One thing that happened to a step on a youth's path (W5, drop-off collection). Read only in aggregate.
  /// </summary>
  public class PathwayStepEventRecord
  {
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OpportunityId { get; set; }

    /// <summary>
    /// The goal the path was built for (Pathway:Goals key), null when the step was not on a path.
    /// </summary>
    public string? GoalKey { get; set; }

    /// <summary>
    /// The stage's order on the path (1 first), null when the step was not on the path when the event arrived.
    /// </summary>
    public int? StageOrder { get; set; }

    public PathwayStepEventType Event { get; set; }

    public DateTimeOffset DateCreated { get; set; }
  }
}
