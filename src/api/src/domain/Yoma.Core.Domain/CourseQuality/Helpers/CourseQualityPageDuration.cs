using System.Globalization;
using System.Text.RegularExpressions;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Turns a page's duration into the engine's timeline shape, for example "6 weeks". The output is rebuilt from parsed
  /// numbers, so no page text survives into it.
  /// </summary>
  public static partial class CourseQualityPageDuration
  {
    #region Class Variables
    private const int MaxCount = 999;
    private const int RegexTimeoutMilliseconds = 1000;
    #endregion

    #region Public Members
    /// <summary>
    /// ISO 8601, for example P6W or PT3H. A mix such as P1M2W has no single honest unit, so it gives null.
    /// </summary>
    public static string? FromIso(string? value)
    {
      if (string.IsNullOrWhiteSpace(value)) return null;

      var match = IsoRegex().Match(value.Trim());
      if (!match.Success) return null;

      var parts = new (string Group, string Unit)[] { ("y", "year"), ("mo", "month"), ("w", "week"), ("d", "day") }
        .Select(o => (o.Unit, Count: Number(match, o.Group)))
        .Where(o => o.Count > 0)
        .ToList();

      var hours = Number(match, "h");
      var minutes = Number(match, "mi");

      if (parts.Count == 1 && hours == 0 && minutes == 0) return Render(parts[0].Count, parts[0].Unit);
      if (parts.Count != 0) return null;

      if (hours > 0 && minutes == 0) return Render(hours, "hour");
      return hours + minutes > 0 ? Render(hours * 60 + minutes, "minute") : null;
    }

    /// <summary>
    /// Labelled wording only, such as "Duration: 6 weeks" or "a 6-week course". A range stays null, because it is not one duration.
    /// </summary>
    public static string? FromText(string? text)
    {
      if (string.IsNullOrWhiteSpace(text)) return null;

      var match = TextRegex().Match(text);
      if (!match.Success) return null;

      var count = match.Groups["a"].Success ? match.Groups["a"].Value : match.Groups["b"].Value;
      var unit = match.Groups["au"].Success ? match.Groups["au"].Value : match.Groups["bu"].Value;

      return int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? Render(value, unit.ToLowerInvariant()) : null;
    }
    #endregion

    #region Private Members
    private static int Number(Match match, string group)
    {
      return int.TryParse(match.Groups[group].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static string? Render(int count, string unit)
    {
      if (count <= 0 || count > MaxCount) return null;

      return $"{count} {unit}{(count > 1 ? "s" : string.Empty)}";
    }

    [GeneratedRegex(@"^P(?:(?<y>\d{1,4})Y)?(?:(?<mo>\d{1,4})M)?(?:(?<w>\d{1,4})W)?(?:(?<d>\d{1,4})D)?(?:T(?:(?<h>\d{1,4})H)?(?:(?<mi>\d{1,4})M)?(?:\d{1,6}S)?)?$",
      RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex IsoRegex();

    [GeneratedRegex(@"\b(?:duration|length|time\s+to\s+complete|time\s+commitment)\s*:?\s*(?:about\s+|approximately\s+)?(?<a>\d{1,3})\s*(?<au>hour|day|week|month)s?\b(?![-–])|\b(?<b>\d{1,3})[\s-](?<bu>hour|day|week|month)[\s-](?:online\s+)?(?:course|programme|program|bootcamp)\b",
      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex TextRegex();
    #endregion
  }
}
