using System.Net;
using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Pulls JSON-LD, meta tags, title, h1 and the heading outline out of raw HTML. No HTML parser is referenced in this solution, so this uses
  /// non-backtracking regexes with timeouts: linear in the input, which is already capped by the link checker.
  /// </summary>
  public static partial class CourseQualityPageSignalExtractor
  {
    #region Class Variables
    public const int MaxJsonLdBlocks = 8;
    public const int MaxJsonLdBlockLength = 64_000;
    public const int MaxJsonLdTotalLength = 128_000;
    public const int MaxTextLength = 1000;
    public const int MaxSections = 60;
    public const int MaxHeadingLength = 200;
    public const int MaxSectionCount = 500;

    private const int MaxSectionTokensScanned = 4000;

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
        ReadSections(content, signals);

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

    /// <summary>
    /// Every heading, with the list items directly under it and the deeper headings in its span. Scripts, navigation and
    /// footers are left out first, so a site menu is not read as a curriculum.
    /// </summary>
    private static void ReadSections(string content, CourseQualityPageSignals signals)
    {
      var body = NonContentRegex().Replace(content, " ");
      var sections = new List<CourseQualityPageSection>();

      foreach (var token in SectionTokenRegex().Matches(body).Take(MaxSectionTokensScanned))
      {
        if (!token.Groups[1].Success)
        {
          if (sections.Count > 0 && sections[^1].ListItems < MaxSectionCount) sections[^1].ListItems++;
          continue;
        }

        var level = token.Groups[1].Value[0] - '0';

        // a deeper heading belongs to every heading still open above it: each nearer one at a higher level, up to the top
        var bound = level;
        for (var i = sections.Count - 1; i >= 0 && bound > 1; i--)
        {
          if (sections[i].Level >= bound) continue;

          if (sections[i].SubHeadings < MaxSectionCount) sections[i].SubHeadings++;
          bound = sections[i].Level;
        }

        var heading = CourseQualitySafeText.Clean(CourseQualityPageText.FromHtml(token.Groups[2].Value, MaxHeadingLength), MaxHeadingLength);
        if (heading == null) continue;

        sections.Add(new CourseQualityPageSection { Level = level, Heading = heading });
      }

      signals.Sections = [.. sections.Take(MaxSections)];
    }

    [GeneratedRegex(@"<(script|style|noscript|template|svg|nav|footer)\b[^>]*>.*?</(script|style|noscript|template|svg|nav|footer)\s*>", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex NonContentRegex();

    [GeneratedRegex(@"<h([1-6])\b[^>]*>(.*?)</h[1-6]\s*>|<li\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex SectionTokenRegex();

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
