using Xunit;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Pathway.Badges.Models;
using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Test.Pathway
{
  public class PathwayNextStepsAndBadgesTests
  {
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// n youth who completed <paramref name="first"/>, then <paramref name="then"/> a day later.
    /// </summary>
    private static IEnumerable<PathwayActivity> Youth(int n, Guid first, Guid? then)
    {
      for (var i = 0; i < n; i++)
      {
        var user = Guid.NewGuid();
        yield return new PathwayActivity(user, first, Start.AddDays(i));
        if (then.HasValue) yield return new PathwayActivity(user, then.Value, Start.AddDays(i + 1));
      }
    }

    [Fact]
    public void UsuallyNext_KeepsTheTopTwo_WithShareAndCount()
    {
      var (a, b, c, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
      var completions = Youth(10, a, b).Concat(Youth(6, a, c)).Concat(Youth(3, a, d)).Concat(Youth(1, a, null)).ToList();
      var titles = new Dictionary<Guid, string> { [a] = "A", [b] = "B", [c] = "C", [d] = "D" };

      var result = PathwayNextSteps.Compute(completions, id => titles.GetValueOrDefault(id), 20, 2);

      var next = result[a];
      Assert.Equal(["B", "C"], next.Select(o => o.Title));
      Assert.Equal([10, 6], next.Select(o => o.Count));
      Assert.Equal([0.5m, 0.3m], next.Select(o => o.Share));
      // B's 10 completers are below the group size
      Assert.False(result.ContainsKey(b));
    }

    [Fact]
    public void UsuallyNext_IsHiddenBelowTheMinimumGroupSize()
    {
      var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

      Assert.Empty(PathwayNextSteps.Compute(Youth(19, a, b), _ => "x", 20, 2));
      Assert.Single(PathwayNextSteps.Compute(Youth(20, a, b), _ => "x", 20, 2));
    }

    [Fact]
    public void UsuallyNext_NamesOnlyListedOpportunities_AndNeverOnesDoneAtTheSameMoment()
    {
      var (a, gone, b) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
      var completions = Youth(20, a, gone).ToList();
      var user = Guid.NewGuid();
      completions.Add(new PathwayActivity(user, a, Start));
      completions.Add(new PathwayActivity(user, b, Start));

      var result = PathwayNextSteps.Compute(completions, id => id == gone ? null : "listed", 20, 2);

      Assert.Empty(result);
    }

    [Fact]
    public void PartnerBadges_CountEachOrganisation_LikeTheBadgeService()
    {
      var (gold, small) = (Guid.NewGuid(), Guid.NewGuid());
      var courses = Enumerable.Range(0, 12).Select(_ => (Guid.NewGuid(), gold)).Concat(Enumerable.Range(0, 3).Select(_ => (Guid.NewGuid(), small))).ToList();
      var goldIds = courses.Where(o => o.Item2 == gold).Select(o => o.Item1).ToList();

      var grades = goldIds.Take(10).Select((id, i) => (id, i < 8 ? CourseGrade.A : CourseGrade.C))
        .Append((goldIds[10], CourseGrade.Ungraded))
        .Append((Guid.NewGuid(), CourseGrade.A)) // not a visible course
        .ToList();
      var latest = new List<(Guid, CourseQualityStatus)> { (goldIds[10], CourseQualityStatus.NeedsReview), (goldIds[11], CourseQualityStatus.NotVerified), (goldIds[0], CourseQualityStatus.Verified) };

      var counts = PathwayPartnerBadges.Count(courses, grades, latest);

      Assert.Equal(new PartnerBadgeCounts(TopGrades: 8, Graded: 10, HeldBack: 1, WaitingForReview: 1), counts[gold]);
      Assert.Equal(new PartnerBadgeCounts(0, 0, 0, 0), counts[small]);

      var levels = PathwayPartnerBadges.Levels([(gold, "Gold Org"), (small, "Small Org"), (Guid.NewGuid(), "")], counts, new PartnerBadgeOptions());

      // 80% at A or B, but 1 of 11 decided held back (9%) is over the Gold limit
      Assert.Equal(PartnerBadgeLevel.Silver, levels[gold]);
      Assert.Equal(PartnerBadgeLevel.NotEnoughGraded, levels[small]);
      Assert.Equal(3, levels.Count);
    }
  }
}
