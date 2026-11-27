namespace Yoma.Core.Domain.Pathway.Models
{
  // Iteration 4 contract for the step endpoints (lock, swap, alternatives, events, health, YoID summary). Mirrors
  // src/web/src/api/models/pathway.ts field for field (camelCase JSON, enums as PascalCase strings). The web file is the source of
  // truth; change both only through the lead.
  // StepDropOff is also carried by PathwayCourse (the engine's iteration 4 fields); it is declared here, once.

  public enum PathwayStepEventType
  {
    Shown,
    Started,
    Completed,
    Swapped,
    Dismissed
  }

  public enum StepDropOffStatus
  {
    Collecting,
    Available
  }

  /// <summary>
  /// Drop-off is reserved for the pilot: no data yet, so the status is Collecting and the rate null until then.
  /// </summary>
  public class StepDropOff
  {
    public const string CollectingNote = "Collecting from the pilot";

    public StepDropOffStatus Status { get; set; }

    /// <summary>
    /// Share who started a step and did not complete it (0 to 1), once Available; otherwise null.
    /// </summary>
    public decimal? Rate { get; set; }

    public string Note { get; set; } = null!;

    public static StepDropOff Collecting(string? note = null) => new() { Status = StepDropOffStatus.Collecting, Rate = null, Note = note ?? CollectingNote };
  }

  public class PathwayStepLockRequest
  {
    public bool Locked { get; set; }
  }

  public class PathwayStepSwapRequest
  {
    public Guid ReplacementOpportunityId { get; set; }
  }

  public class PathwayStepEventRequest
  {
    public Guid OpportunityId { get; set; }

    public PathwayStepEventType Event { get; set; }
  }

  public class PathwayHealth
  {
    /// <summary>
    /// Distinct path steps (goal, stage, opportunity) with at least one event.
    /// </summary>
    public int StepsTracked { get; set; }

    public int Events { get; set; }

    /// <summary>
    /// Aggregate over every step; Collecting until the pilot.
    /// </summary>
    public StepDropOff DropOff { get; set; } = null!;

    public string Note { get; set; } = null!;
  }

  public class MyPathNextStep
  {
    public Guid OpportunityId { get; set; }

    public string Title { get; set; } = null!;
  }

  public class MyPathStage
  {
    public string Name { get; set; } = null!;

    public int Completed { get; set; }

    public int Total { get; set; }
  }

  /// <summary>
  /// The YoID page's "My path" card.
  /// </summary>
  public class MyPathSummary
  {
    public string GoalLabel { get; set; } = null!;

    public int TotalSteps { get; set; }

    public int CompletedSteps { get; set; }

    public MyPathNextStep? NextStep { get; set; }

    public List<MyPathStage> Stages { get; set; } = [];
  }
}
