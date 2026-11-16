namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// An admin's rubric override. Rows are kept as history and the newest wins; a row with no rubric is a reset to configuration.
  /// </summary>
  public class CourseQualityGradeRubricRecord
  {
    public Guid Id { get; set; }

    /// <summary>
    /// JSON of <see cref="CourseGradeRubric"/>, or null when this row reset the rubric to configuration.
    /// </summary>
    public string? Rubric { get; set; }

    public string UpdatedByEmail { get; set; } = null!;

    public DateTimeOffset DateCreated { get; set; }
  }
}
