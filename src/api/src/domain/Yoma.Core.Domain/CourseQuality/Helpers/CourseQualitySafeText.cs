using System.Globalization;
using System.Text;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Makes page-derived text safe to show. Anything from a fetched page passes through here before it can reach a
  /// learner or partner: control, format (bidi, zero-width) and private-use characters go, whitespace collapses and length is capped.
  /// </summary>
  public static partial class CourseQualitySafeText
  {
    #region Class Variables
    public const int MaxQuoteLength = 160;
    private const int QuoteContext = 40;
    private const string UrlRemoved = "[link removed]";
    #endregion

    #region Public Members
    public static string? Clean(string? value, int maxLength)
    {
      if (string.IsNullOrEmpty(value)) return null;

      var builder = new StringBuilder(Math.Min(value.Length, maxLength));
      var pendingSpace = false;

      for (var i = 0; i < value.Length && builder.Length < maxLength; i++)
      {
        var c = value[i];

        if (char.IsWhiteSpace(c))
        {
          pendingSpace = builder.Length > 0;
          continue;
        }

        if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
        {
          if (IsUnsafe(CharUnicodeInfo.GetUnicodeCategory(value, i)))
          {
            i++;
            continue;
          }

          if (builder.Length + (pendingSpace ? 3 : 2) > maxLength) break;
          if (pendingSpace) builder.Append(' ');
          pendingSpace = false;
          builder.Append(c).Append(value[++i]);
          continue;
        }

        if (IsUnsafe(char.GetUnicodeCategory(c))) continue;

        if (pendingSpace && builder.Length + 1 < maxLength) builder.Append(' ');
        pendingSpace = false;
        builder.Append(c);
      }

      var result = builder.ToString().TrimEnd();
      return result.Length == 0 ? null : result;
    }

    public static bool ContainsUrl(string? value)
    {
      return !string.IsNullOrEmpty(value) && UrlRegex().IsMatch(value);
    }

    /// <summary>
    /// For model text that stays visible to staff, so a page cannot pass a link through it.
    /// </summary>
    public static string? StripUrls(string? value)
    {
      return string.IsNullOrEmpty(value) ? value : UrlRegex().Replace(value, UrlRemoved);
    }

    /// <summary>
    /// A short quote around a match in already-cleaned text, cut on word boundaries.
    /// </summary>
    public static string? Quote(string text, int index, int length)
    {
      ArgumentNullException.ThrowIfNull(text, nameof(text));

      var start = Math.Max(0, index - QuoteContext);
      var end = Math.Min(text.Length, index + length + QuoteContext);

      if (start > 0)
      {
        var space = text.IndexOf(' ', start, index - start);
        if (space >= 0) start = space + 1;
      }

      if (end < text.Length)
      {
        var space = text.LastIndexOf(' ', end - 1, end - (index + length));
        if (space >= 0) end = space;
      }

      return Clean(text[start..end], MaxQuoteLength);
    }

    /// <summary>
    /// Whether a quote really came from the page. Compared after cleaning, the only change a quote ever goes through.
    /// </summary>
    public static bool AppearsInPage(string? quote, string? pageText, CourseQualityPageSignals? signals)
    {
      var needle = Clean(quote, int.MaxValue);
      if (needle == null) return false;

      var sources = new List<string?> { pageText };
      if (signals != null)
      {
        sources.Add(signals.Title);
        sources.Add(signals.Heading);
        sources.AddRange(signals.JsonLdBlocks);
        sources.AddRange(signals.MetaTags.Values);
      }

      return sources.Any(o => Clean(o, int.MaxValue)?.Contains(needle, StringComparison.Ordinal) == true);
    }
    #endregion

    #region Private Members
    [System.Text.RegularExpressions.GeneratedRegex(@"\b[a-z][a-z0-9+.-]*://\S+|\bwww\.\S+|\b[a-z0-9-]+(?:\.[a-z0-9-]+)*\.[a-z]{2,}\b(?:/\S*)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.NonBacktracking, 1000)]
    private static partial System.Text.RegularExpressions.Regex UrlRegex();

    private static bool IsUnsafe(UnicodeCategory category)
    {
      return category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
        or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned;
    }
    #endregion
  }
}
