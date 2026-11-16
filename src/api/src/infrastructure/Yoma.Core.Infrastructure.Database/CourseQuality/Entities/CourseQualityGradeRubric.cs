using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yoma.Core.Infrastructure.Database.CourseQuality.Entities
{
  [Table("GradeRubric", Schema = "CourseQuality")]
  [Index(nameof(DateCreated))]
  public class CourseQualityGradeRubric : Shared.Entities.BaseEntity<Guid>
  {
    [Column(TypeName = "text")] //stored as text, because it is optional: null resets to configuration; see the note on Assessment.Criteria
    public string? Rubric { get; set; }

    [Required]
    [Column(TypeName = "varchar(320)")]
    public string UpdatedByEmail { get; set; } = null!;

    [Required]
    public DateTimeOffset DateCreated { get; set; }
  }
}
