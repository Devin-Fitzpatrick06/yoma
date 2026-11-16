using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Infrastructure.Database.CourseQuality.Entities
{
  [Table("LearnerFeedback", Schema = "CourseQuality")]
  [Index(nameof(UserId), nameof(OpportunityId), IsUnique = true)]
  [Index(nameof(OpportunityId))]
  public class CourseQualityLearnerFeedback : Shared.Entities.BaseEntity<Guid>
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
    public CourseFeedbackAnswer Answer { get; set; }

    [Required]
    public DateTimeOffset DateLeft { get; set; }

    public DateTimeOffset? DateAsked { get; set; }

    public DateTimeOffset? SnoozeUntil { get; set; }

    public int? Rating { get; set; }

    public bool? Completed { get; set; }

    public bool? WouldRecommend { get; set; }

    public bool? MatchedDescription { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public string Good { get; set; } = null!;

    [Required]
    [Column(TypeName = "jsonb")]
    public string Bad { get; set; } = null!;

    [Column(TypeName = "text")] //length is set by CourseQuality:LearnerFeedback:CommentMaxLength and checked by the service
    public string? Comment { get; set; }

    [Required]
    public DateTimeOffset DateCreated { get; set; }

    [Required]
    public DateTimeOffset DateModified { get; set; }
  }
}
