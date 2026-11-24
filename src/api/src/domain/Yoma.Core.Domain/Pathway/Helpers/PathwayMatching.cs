using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Helpers
{
  /// <summary>
  /// How a course is matched to a stage, and the small text rules the engine shares with outcomes.
  /// </summary>
  public static class PathwayMatching
  {
    #region Class Variables
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.Ordinal);
    /// <summary>
    /// Words that tell editions of one course apart rather than different courses.
    /// </summary>
    private static readonly HashSet<string> Noise = new(["edition", "revised", "updated", "version", "course", "complete"], StringComparer.Ordinal);
    private static readonly Regex Accented = new("[éèàçù]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, MatchTimeout);
    #endregion

    #region Public Members
    /// <summary>
    /// A configured pattern as a case-insensitive regular expression, compiled once.
    /// </summary>
    public static Regex Pattern(string pattern)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(pattern, nameof(pattern));

      return Cache.GetOrAdd(pattern, o => new Regex(o, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout));
    }

    public static bool IsMatch(string pattern, string? value)
    {
      if (string.IsNullOrWhiteSpace(value)) return false;

      try
      {
        return Pattern(pattern).IsMatch(value);
      }
      catch (RegexMatchTimeoutException)
      {
        return false;
      }
    }

    /// <summary>
    /// The course's skills that match the stage.
    /// </summary>
    public static List<string> MatchingSkills(PathwayStageOptions stage, PathwayCatalogueCourse course)
    {
      ArgumentNullException.ThrowIfNull(stage, nameof(stage));
      ArgumentNullException.ThrowIfNull(course, nameof(course));

      return [.. course.Skills.Where(o => IsMatch(stage.Pattern, o))];
    }

    /// <summary>
    /// A course belongs to a stage when one of its skills or its title matches the stage's pattern.
    /// </summary>
    public static bool InStage(PathwayStageOptions stage, PathwayCatalogueCourse course)
    {
      ArgumentNullException.ThrowIfNull(stage, nameof(stage));
      ArgumentNullException.ThrowIfNull(course, nameof(course));

      return IsMatch(stage.Pattern, course.Title) || course.Skills.Any(o => IsMatch(stage.Pattern, o));
    }

    /// <summary>
    /// The first three words of more than three letters, lower case: two courses with the same stem are near duplicates
    /// ("Introduction to Excel 2016" and "Introduction to Excel 2019").
    /// </summary>
    public static string Stem(string title)
    {
      ArgumentNullException.ThrowIfNull(title, nameof(title));

      var letters = new StringBuilder(title.Length);
      foreach (var c in title.ToLowerInvariant())
        letters.Append(c is >= 'a' and <= 'z' or ' ' ? c : ' ');

      var words = letters.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(o => o.Length > 3 && !Noise.Contains(o)).Take(3);
      var stem = string.Join(' ', words);

      // a title with no long English word keeps itself, so it never collides with another such title
      return stem.Length > 0 ? stem : title.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Listed in English, and not a title with French or Portuguese accents that was listed as English.
    /// </summary>
    public static bool LooksEnglish(PathwayCatalogueCourse course)
    {
      ArgumentNullException.ThrowIfNull(course, nameof(course));

      var listed = course.Languages.Count == 0 || course.Languages.Any(o => o.Equals("English", StringComparison.OrdinalIgnoreCase));
      return listed && !Accented.IsMatch(course.Title);
    }

    /// <summary>
    /// "a", "a and b", "a, b and c".
    /// </summary>
    public static string JoinList(IReadOnlyList<string> items)
    {
      ArgumentNullException.ThrowIfNull(items, nameof(items));

      return items.Count switch
      {
        0 => string.Empty,
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}"
      };
    }

    public static string Lower(string value) => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
    #endregion
  }
}
