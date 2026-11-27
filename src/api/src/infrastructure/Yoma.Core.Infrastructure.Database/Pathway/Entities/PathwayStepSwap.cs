using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yoma.Core.Infrastructure.Database.Pathway.Entities
{
  [Table("StepSwap", Schema = "Pathway")]
  [Index(nameof(UserId), nameof(DateCreated))]
  public class PathwayStepSwap : Shared.Entities.BaseEntity<Guid>
  {
    [Required]
    [ForeignKey("UserId")]
    public Guid UserId { get; set; }
    public Entity.Entities.User User { get; set; } = null!;

    [Required]
    [ForeignKey("ReplacedOpportunityId")]
    public Guid ReplacedOpportunityId { get; set; }
    public Opportunity.Entities.Opportunity ReplacedOpportunity { get; set; } = null!;

    [Required]
    [ForeignKey("ReplacementOpportunityId")]
    public Guid ReplacementOpportunityId { get; set; }
    public Opportunity.Entities.Opportunity ReplacementOpportunity { get; set; } = null!;

    [Required]
    public DateTimeOffset DateCreated { get; set; }
  }
}
