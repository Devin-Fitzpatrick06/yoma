using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Infrastructure.Database.CourseQuality.Entities
{
  [Table("Grade", Schema = "CourseQuality")]
  public class CourseQualityGrade : Shared.Entities.BaseEntity<Guid>
  {
    [Required]
    [ForeignKey("OpportunityId")]
    public Guid OpportunityId { get; set; }
    public Opportunity.Entities.Opportunity Opportunity { get; set; } = null!;

    [Required]
    public CourseQualityStatus Status { get; set; }

    [Required]
    public CourseGrade Grade { get; set; }

    [Required]
    [Column(TypeName = "varchar(50)")]
    public string GradeLabel { get; set; } = null!;

    [Required]
    public int Score { get; set; }

    [Required]
    public int Percentile { get; set; }

    [Required]
    public int FeedbackResponses { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public string Factors { get; set; } = null!;

    [Required]
    [Column(TypeName = "jsonb")]
    public string Caps { get; set; } = null!;

    [Required]
    [Column(TypeName = "varchar(50)")]
    public string RubricVersion { get; set; } = null!;

    [Required]
    public DateTimeOffset DateGraded { get; set; }
  }
}
