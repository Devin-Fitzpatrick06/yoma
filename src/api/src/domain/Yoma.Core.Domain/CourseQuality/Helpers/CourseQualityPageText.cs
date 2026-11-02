using System.Net;
using System.Text.RegularExpressions;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Reduces a fetched page to plain text. Regexes are non-backtracking because the input is untrusted.
  /// </summary>
  public static partial class CourseQualityPageText
  {
    #region Public Members
    public static string? FromHtml(string? html, int maxLength)
    {
      if (string.IsNullOrWhiteSpace(html)) return null;

      var text = NonContentRegex().Replace(html, " ");
      text = TagRegex().Replace(text, " ");
      text = WebUtility.HtmlDecode(text);
      return Normalize(text, maxLength);
    }

    public static string? FromPlainText(string? value, int maxLength)
    {
      return Normalize(value, maxLength);
    }
    #endregion

    #region Private Members
    private static string? Normalize(string? value, int maxLength)
    {
      if (string.IsNullOrWhiteSpace(value)) return null;

      var text = WhiteSpaceRegex().Replace(value, " ").Trim();
      if (text.Length == 0) return null;

      return text.Length <= maxLength ? text : text[..maxLength];
    }

    [GeneratedRegex(@"<!--.*?-->|<(script|style|noscript|template|svg|nav|footer)\b[^>]*>.*?</(script|style|noscript|template|svg|nav|footer)\s*>",
      RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex NonContentRegex();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.NonBacktracking)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.NonBacktracking)]
    private static partial Regex WhiteSpaceRegex();
    #endregion
  }
}
