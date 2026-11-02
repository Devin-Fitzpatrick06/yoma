using System.Net;
using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Pulls JSON-LD, meta tags, title and h1 out of raw HTML. No HTML parser is referenced in this solution, so this uses
  /// non-backtracking regexes with timeouts: linear in the input, which is already capped by the link checker.
  /// </summary>
  public static partial class CourseQualityPageSignalExtractor
  {
    #region Class Variables
    public const int MaxJsonLdBlocks = 8;
    public const int MaxJsonLdBlockLength = 64_000;
    public const int MaxJsonLdTotalLength = 128_000;
    public const int MaxTextLength = 1000;

    private const int MaxMetaTagsScanned = 256;
    private const int RegexTimeoutMilliseconds = 2000;
    private const RegexOptions SafeOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.NonBacktracking;

    private static readonly HashSet<string> AllowedMetaTags = new(StringComparer.OrdinalIgnoreCase)
    {
      "title", "description", "og:title", "og:description", "og:type", "twitter:title",
      "event:start_time", "event:end_time", "product:price:amount", "product:price:currency", "og:price:amount", "og:price:currency"
    };
    #endregion

    #region Public Members
    /// <summary>
    /// Returns null for an empty page, or when a regex ran out of time: page text still stands without signals.
    /// </summary>
    public static CourseQualityPageSignals? FromHtml(string? html)
    {
      if (string.IsNullOrWhiteSpace(html)) return null;

      try
      {
        var content = CommentRegex().Replace(html, " ");

        var signals = new CourseQualityPageSignals
        {
          Title = InnerText(TitleRegex().Match(content)),
          Heading = InnerText(HeadingRegex().Match(content)),
          Footer = InnerText(FooterRegex().Match(content))
        };

        ReadJsonLd(content, signals);
        ReadMetaTags(content, signals);

        return signals;
      }
      catch (RegexMatchTimeoutException)
      {
        return null;
      }
    }
    #endregion

    #region Private Members
    private static string? InnerText(Match match)
    {
      if (!match.Success) return null;

      return CourseQualitySafeText.Clean(CourseQualityPageText.FromHtml(match.Groups[1].Value, MaxTextLength), MaxTextLength);
    }

    private static void ReadJsonLd(string content, CourseQualityPageSignals signals)
    {
      var total = 0;

      foreach (Match match in JsonLdRegex().Matches(content))
      {
        if (signals.JsonLdBlocks.Count >= MaxJsonLdBlocks) break;

        // a truncated block is not valid JSON, so an oversized one is dropped whole
        var block = match.Groups[1].Value.Trim();
        if (block.Length == 0 || block.Length > MaxJsonLdBlockLength || total + block.Length > MaxJsonLdTotalLength) continue;

        signals.JsonLdBlocks.Add(block);
        total += block.Length;
      }
    }

    private static void ReadMetaTags(string content, CourseQualityPageSignals signals)
    {
      foreach (var tag in MetaRegex().Matches(content).Take(MaxMetaTagsScanned))
      {
        string? key = null;
        string? value = null;

        foreach (Match attribute in AttributeRegex().Matches(tag.Value))
        {
          var raw = attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Value;

          if (attribute.Groups[1].Value.Equals("content", StringComparison.OrdinalIgnoreCase)) value = raw;
          else key ??= raw.Trim();
        }

        if (key == null || value == null || !AllowedMetaTags.Contains(key) || signals.MetaTags.ContainsKey(key)) continue;

        var cleaned = CourseQualitySafeText.Clean(WebUtility.HtmlDecode(value), MaxTextLength);
        if (cleaned != null) signals.MetaTags[key] = cleaned;
      }
    }

    [GeneratedRegex(@"<!--.*?-->", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"<title\b[^>]*>(.*?)</title\s*>", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<h1\b[^>]*>(.*?)</h1\s*>", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"<footer\b[^>]*>(.*?)</footer\s*>", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex FooterRegex();

    [GeneratedRegex(@"<script\b[^>]*application/ld\+json[^>]*>(.*?)</script\s*>", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex JsonLdRegex();

    [GeneratedRegex(@"<meta\b[^>]*>", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex MetaRegex();

    [GeneratedRegex(@"\b(name|property|itemprop|content)\s*=\s*(?:""([^""]*)""|'([^']*)')", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex AttributeRegex();
    #endregion
  }
}
