using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Infrastructure.Database.Pathway.Entities
{
  [Table("Profile", Schema = "Pathway")]
  [Index(nameof(UserId), IsUnique = true)]
  [Index(nameof(GoalKey))]
  public class PathwayProfile : Shared.Entities.BaseEntity<Guid>
  {
    [Required]
    [ForeignKey("UserId")]
    public Guid UserId { get; set; }
    public Entity.Entities.User User { get; set; } = null!;

    /// <summary>
    /// A goal key from configuration (Pathway:Goals). Not a foreign key: goals are configuration, and a goal removed later is handled
    /// by the service rather than the schema.
    /// </summary>
    [Required]
    [Column(TypeName = "varchar(50)")]
    public string GoalKey { get; set; } = null!;

    [Required]
    public PathwayBackground Background { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public string KnownSkillKeys { get; set; } = null!;

    [Required]
    public int HoursPerWeek { get; set; }

    [ForeignKey("CountryId")]
    public Guid? CountryId { get; set; }
    public Lookups.Entities.Country? Country { get; set; }

    [Required]
    public DateTimeOffset DateCreated { get; set; }

    [Required]
    public DateTimeOffset DateModified { get; set; }
  }
}
