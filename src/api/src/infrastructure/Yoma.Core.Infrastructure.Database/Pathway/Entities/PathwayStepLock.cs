using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yoma.Core.Infrastructure.Database.Pathway.Entities
{
  [Table("StepLock", Schema = "Pathway")]
  [Index(nameof(UserId), nameof(OpportunityId), IsUnique = true)]
  public class PathwayStepLock : Shared.Entities.BaseEntity<Guid>
  {
    [Required]
    [ForeignKey("UserId")]
    public Guid UserId { get; set; }
    public Entity.Entities.User User { get; set; } = null!;

    [Required]
    [ForeignKey("OpportunityId")]
    public Guid OpportunityId { get; set; }
    public Opportunity.Entities.Opportunity Opportunity { get; set; } = null!;

    [Required]
    public DateTimeOffset DateCreated { get; set; }
  }
}
