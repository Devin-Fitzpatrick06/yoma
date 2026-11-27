using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Pathway.Badges.Models;
using Yoma.Core.Domain.Pathway.Badges.Services;

namespace Yoma.Core.Domain.Pathway.Helpers
{
  /// <summary>
  /// Partner badges for every provider on a path at once. Counts each organisation's publicly visible Learning courses the way the
  /// badge service does for one (grades A to D; held back = latest Not verified without a grade; waiting = latest needs review
  /// without a grade), then hands them to <see cref="PartnerBadgeCalculator"/>, so the level is the one the public badge shows.
  /// </summary>
  public static class PathwayPartnerBadges
  {
    #region Public Members
    /// <param name="courses">Each visible Learning course and its organisation.</param>
    /// <param name="grades">Stored grades of those courses, Ungraded included.</param>
    /// <param name="latest">The latest assessment status of those courses.</param>
    public static Dictionary<Guid, PartnerBadgeCounts> Count(IEnumerable<(Guid OpportunityId, Guid OrganizationId)> courses,
      IEnumerable<(Guid OpportunityId, CourseGrade Grade)> grades, IEnumerable<(Guid OpportunityId, CourseQualityStatus Status)> latest)
    {
      ArgumentNullException.ThrowIfNull(courses, nameof(courses));
      ArgumentNullException.ThrowIfNull(grades, nameof(grades));
      ArgumentNullException.ThrowIfNull(latest, nameof(latest));

      var organizationOf = new Dictionary<Guid, Guid>();
      foreach (var (opportunityId, organizationId) in courses) organizationOf.TryAdd(opportunityId, organizationId);

      var graded = new Dictionary<Guid, CourseGrade>();
      foreach (var (opportunityId, grade) in grades)
        if (grade != CourseGrade.Ungraded && organizationOf.ContainsKey(opportunityId)) graded[opportunityId] = grade;

      var statuses = new Dictionary<Guid, CourseQualityStatus>();
      foreach (var (opportunityId, status) in latest)
        if (organizationOf.ContainsKey(opportunityId)) statuses[opportunityId] = status;

      return organizationOf.GroupBy(o => o.Value).ToDictionary(o => o.Key, o =>
      {
        var ids = o.Select(c => c.Key).ToList();
        return new PartnerBadgeCounts(
          TopGrades: ids.Count(id => graded.TryGetValue(id, out var grade) && grade is CourseGrade.A or CourseGrade.B),
          Graded: ids.Count(graded.ContainsKey),
          HeldBack: ids.Count(id => !graded.ContainsKey(id) && statuses.TryGetValue(id, out var status) && status == CourseQualityStatus.NotVerified),
          WaitingForReview: ids.Count(id => !graded.ContainsKey(id) && statuses.TryGetValue(id, out var status) && status == CourseQualityStatus.NeedsReview));
      });
    }

    /// <summary>
    /// The public level of each organisation; an organisation without a visible course is NotEnoughGraded like any other below the
    /// minimum.
    /// </summary>
    public static Dictionary<Guid, PartnerBadgeLevel> Levels(IEnumerable<(Guid OrganizationId, string OrganizationName)> organizations,
      Dictionary<Guid, PartnerBadgeCounts> counts, PartnerBadgeOptions options)
    {
      ArgumentNullException.ThrowIfNull(organizations, nameof(organizations));
      ArgumentNullException.ThrowIfNull(counts, nameof(counts));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var result = new Dictionary<Guid, PartnerBadgeLevel>();
      foreach (var (organizationId, organizationName) in organizations)
      {
        if (organizationId == Guid.Empty || result.ContainsKey(organizationId)) continue;

        var name = string.IsNullOrWhiteSpace(organizationName) ? "Organisation" : organizationName;
        result[organizationId] = PartnerBadgeCalculator.Calculate(organizationId, name, counts.GetValueOrDefault(organizationId) ?? new PartnerBadgeCounts(0, 0, 0, 0), options, DateTimeOffset.UtcNow).Level;
      }

      return result;
    }
    #endregion
  }
}
