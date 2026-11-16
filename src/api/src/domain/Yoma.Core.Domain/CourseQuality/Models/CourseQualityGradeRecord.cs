namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Stored form of a course's grade. One row per opportunity, replaced by every grading run.
  /// </summary>
  public class CourseQualityGradeRecord
  {
    public Guid Id { get; set; }

    public Guid OpportunityId { get; set; }

    /// <summary>
    /// Read from the opportunity, so it is always current.
    /// </summary>
    public string OpportunityTitle { get; set; } = null!;

    /// <summary>
    /// Read from the opportunity's organization, so it is always current.
    /// </summary>
    public string OrganizationName { get; set; } = null!;

    /// <summary>
    /// The assessment status the grade was worked out from.
    /// </summary>
    public CourseQualityStatus Status { get; set; }

    public CourseGrade Grade { get; set; }

    public string GradeLabel { get; set; } = null!;

    public int Score { get; set; }

    public int Percentile { get; set; }

    /// <summary>
    /// Learners who said they took the course when it was graded, which decides whether the A feedback floor applies.
    /// </summary>
    public int FeedbackResponses { get; set; }

    /// <summary>
    /// JSON list of <see cref="CourseGradeFactorResult"/>.
    /// </summary>
    public string Factors { get; set; } = null!;

    /// <summary>
    /// JSON list of the rules that held the grade down.
    /// </summary>
    public string Caps { get; set; } = null!;

    public string RubricVersion { get; set; } = null!;

    public DateTimeOffset DateGraded { get; set; }
  }
}
