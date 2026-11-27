using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Infrastructure.Database.Pathway.Entities
{
  /// <summary>
  /// W5 drop-off collection. Indexed for the aggregates (per step and event, per goal and stage) and for the repeat check on Shown
  /// (user, step, event, time).
  /// </summary>
  [Table("StepEvent", Schema = "Pathway")]
  [Index(nameof(UserId), nameof(OpportunityId), nameof(Event), nameof(DateCreated))]
  [Index(nameof(OpportunityId), nameof(Event))]
  [Index(nameof(GoalKey), nameof(StageOrder), nameof(Event))]
  [Index(nameof(Event), nameof(DateCreated))]
  public class PathwayStepEvent : Shared.Entities.BaseEntity<Guid>
  {
    [Required]
    [ForeignKey("UserId")]
    public Guid UserId { get; set; }
    public Entity.Entities.User User { get; set; } = null!;

    [Required]
    [ForeignKey("OpportunityId")]
    public Guid OpportunityId { get; set; }
    public Opportunity.Entities.Opportunity Opportunity { get; set; } = null!;

    /// <summary>
    /// A goal key from configuration (Pathway:Goals), not a foreign key, like the profile's.
    /// </summary>
    [Column(TypeName = "varchar(50)")]
    public string? GoalKey { get; set; }

    public int? StageOrder { get; set; }

    [Required]
    public PathwayStepEventType Event { get; set; }

    [Required]
    public DateTimeOffset DateCreated { get; set; }
  }
}
