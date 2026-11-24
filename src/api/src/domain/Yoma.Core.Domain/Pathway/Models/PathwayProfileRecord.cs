namespace Yoma.Core.Domain.Pathway.Models
{
  /// <summary>
  /// Stored form of one youth's answers to the "My Yoma" questions. One row per user.
  /// </summary>
  public class PathwayProfileRecord
  {
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string GoalKey { get; set; } = null!;

    public PathwayBackground Background { get; set; }

    public List<string> KnownSkillKeys { get; set; } = [];

    public int HoursPerWeek { get; set; }

    public Guid? CountryId { get; set; }

    public DateTimeOffset DateCreated { get; set; }

    public DateTimeOffset DateModified { get; set; }
  }
}
