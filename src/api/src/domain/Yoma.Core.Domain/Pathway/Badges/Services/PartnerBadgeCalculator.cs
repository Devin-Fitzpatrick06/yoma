using System.Globalization;
using Yoma.Core.Domain.Pathway.Badges.Models;

namespace Yoma.Core.Domain.Pathway.Badges.Services
{
  /// <summary>
  /// Pure: a partner's badge from the counts of its courses' grades and verification results.
  ///
  /// Fairness guard: a course waiting for a reviewer has neither a grade nor a verdict, so it counts neither for nor against the
  /// partner, and the hint says how many are waiting. Shares are only shown from the minimum number of graded courses, so a
  /// provider is not labelled from a handful of them.
  /// </summary>
  public static class PartnerBadgeCalculator
  {
    #region Class Variables
    private const int ShareDecimals = 3;
    private static readonly PartnerBadgeLevel[] Ladder = [PartnerBadgeLevel.Bronze, PartnerBadgeLevel.Silver, PartnerBadgeLevel.Gold];
    #endregion

    #region Public Members
    public static PartnerBadge Calculate(Guid organizationId, string organizationName, PartnerBadgeCounts counts, PartnerBadgeOptions options)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(organizationName, nameof(organizationName));
      ArgumentNullException.ThrowIfNull(counts, nameof(counts));
      ArgumentNullException.ThrowIfNull(options, nameof(options));

      var minimum = Math.Max(options.MinimumGradedCourses, 1);
      var badge = new PartnerBadge
      {
        OrganizationId = organizationId,
        OrganizationName = organizationName,
        GradedCourses = counts.Graded,
        MinimumGradedCourses = minimum
      };

      if (counts.Graded < minimum)
      {
        badge.Level = PartnerBadgeLevel.NotEnoughGraded;
        badge.NextLevelHint = Join(
          $"{counts.Graded} of the {minimum} graded courses needed for a badge.",
          Waiting(counts.WaitingForReview));
        return badge;
      }

      var decided = counts.Graded + counts.HeldBack;
      var shareTop = (decimal)counts.TopGrades / counts.Graded;
      var shareHeld = decided == 0 ? 0m : (decimal)counts.HeldBack / decided;

      badge.ShareTopGrades = Math.Round(shareTop, ShareDecimals, MidpointRounding.AwayFromZero);
      badge.ShareHeldBack = Math.Round(shareHeld, ShareDecimals, MidpointRounding.AwayFromZero);

      var levels = Ladder.Where(options.Levels.ContainsKey).ToList();
      var level = levels.LastOrDefault(o => Meets(options.Levels[o], shareTop, shareHeld));
      badge.Level = level == default ? PartnerBadgeLevel.None : level;

      var next = levels.FirstOrDefault(o => Rank(o) > Rank(badge.Level));
      badge.NextLevelHint = Join(next == default ? null : Hint(next, options.Levels[next], counts, shareTop, shareHeld), Waiting(counts.WaitingForReview));

      return badge;
    }
    #endregion

    #region Private Members
    private static bool Meets(PartnerBadgeThreshold threshold, decimal shareTop, decimal shareHeld)
    {
      return shareTop >= threshold.MinShareTopGrades && shareHeld <= threshold.MaxShareHeldBack;
    }

    private static int Rank(PartnerBadgeLevel level) => Array.IndexOf(Ladder, level);

    /// <summary>
    /// In courses rather than percentages where it can: how many more at A or B, and how many held-back courses to fix. Approximate,
    /// because grades are relative and a fixed course joins the graded ones.
    /// </summary>
    private static string Hint(PartnerBadgeLevel next, PartnerBadgeThreshold threshold, PartnerBadgeCounts counts, decimal shareTop, decimal shareHeld)
    {
      var steps = new List<string>();

      if (shareTop < threshold.MinShareTopGrades)
      {
        var more = (int)Math.Ceiling(threshold.MinShareTopGrades * counts.Graded) - counts.TopGrades;
        steps.Add($"about {Math.Max(more, 1)} more {Courses(Math.Max(more, 1))} at grade A or B ({Percent(shareTop)} now, {Percent(threshold.MinShareTopGrades)} needed)");
      }

      if (shareHeld > threshold.MaxShareHeldBack)
      {
        var decided = counts.Graded + counts.HeldBack;
        var fix = counts.HeldBack - (int)Math.Floor(threshold.MaxShareHeldBack * decided);
        steps.Add($"{Math.Max(fix, 1)} fewer {Courses(Math.Max(fix, 1))} held back as Not verified ({Percent(shareHeld)} now, at most {Percent(threshold.MaxShareHeldBack)})");
      }

      return $"For {next}: {string.Join(", and ", steps)}. The fix notes say what each held-back course needs.";
    }

    private static string? Waiting(int waiting)
    {
      return waiting switch
      {
        <= 0 => null,
        1 => "1 course is waiting for review and is not counted.",
        _ => $"{waiting} courses are waiting for review and are not counted."
      };
    }

    private static string Courses(int count) => count == 1 ? "course" : "courses";

    private static string Percent(decimal share) => share.ToString("0%", CultureInfo.InvariantCulture);

    private static string? Join(params string?[] parts)
    {
      var text = string.Join(" ", parts.Where(o => !string.IsNullOrWhiteSpace(o)));
      return text.Length == 0 ? null : text;
    }
    #endregion
  }
}
