using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Infrastructure.Database.CourseQuality.Entities
{
  [Table("Assessment", Schema = "CourseQuality")]
  [Index(nameof(OrganizationId), nameof(IsLatest), nameof(ReviewStatus))]
  public class CourseQualityAssessment : Shared.Entities.BaseEntity<Guid>
  {
    [Required]
    [ForeignKey("OpportunityId")]
    public Guid OpportunityId { get; set; }
    public Opportunity.Entities.Opportunity Opportunity { get; set; } = null!;

    [Required]
    [ForeignKey("OrganizationId")]
    public Guid OrganizationId { get; set; }
    public Entity.Entities.Organization Organization { get; set; } = null!;

    [Required]
    [Column(TypeName = "varchar(50)")]
    public string RubricVersion { get; set; } = null!;

    [Required]
    public int Score { get; set; }

    [Required]
    [Column(TypeName = "decimal(4,3)")]
    public decimal Confidence { get; set; }

    [Required]
    public CourseQualityStatus Status { get; set; }

    [Required]
    public CourseQualityReviewStatus ReviewStatus { get; set; }

    [Required]
    public CourseFormat Format { get; set; }

    [Required]
    public bool DatesSuspect { get; set; }

    [Required]
    public bool CardReady { get; set; }

    [Required]
    public bool AIUsed { get; set; }

    [Column(TypeName = "varchar(100)")]
    public string? AIModel { get; set; }

    [Column(TypeName = "text")]
    public string? AISummary { get; set; }

    [Required] //nullable column results in an EF error: 22P02 invalid input syntax for type json
    [Column(TypeName = "jsonb")]
    public string Criteria { get; set; } = null!;

    [Required]
    [Column(TypeName = "jsonb")]
    public string LearnerCard { get; set; } = null!;

    [Column(TypeName = "text")] //stored as text, because it is optional; see the note on Criteria
    public string? FixNote { get; set; }

    [Column(TypeName = "varchar(320)")]
    public string? ReviewedByEmail { get; set; }

    [Column(TypeName = "varchar(1000)")]
    public string? ReviewNote { get; set; }

    public DateTimeOffset? DateReviewed { get; set; }

    [Required]
    public DateTimeOffset DateAssessed { get; set; }

    [Required]
    public DateTimeOffset OpportunityDateModified { get; set; }

    [Required]
    [Column(TypeName = "varchar(64)")]
    public string ContentFingerprint { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string? FinalHost { get; set; }

    public DateTimeOffset? DateConfirmed { get; set; }

    [Required]
    public bool IsLatest { get; set; }
  }
}
