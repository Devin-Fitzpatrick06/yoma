using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Reads a page that loaded (2xx) for the link health check: a bot wall, a page that says it is gone, or a sign-in wall.
  ///
  /// The gone and sign-in wording is the assessment's own (<see cref="CourseQualityPageMatch"/>), so a link the check
  /// calls gone is one an assessment would too. Only the bot-wall and "expired" wording is added here. The standalone
  /// CLI keeps the same rules in scripts/link-health/link-health-rules.json; change both together.
  ///
  /// The body is untrusted. It is only matched, never returned: reasons are fixed wording.
  /// </summary>
  public static partial class LinkHealthPageRules
  {
    #region Class Variables
    /// <summary>The opening of a page, where an error page says what it is. Matches CourseQualityPageMatch.</summary>
    public const int LeadTextLength = 500;

    /// <summary>A page this short is all wall; on a longer one the same words are only a menu or a footer.</summary>
    public const int ShortPageLength = 1000;

    private const int RegexTimeoutMilliseconds = 1000;
    private const RegexOptions SafeOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    #endregion

    #region Public Members
    public static bool IsBotWall(CourseQualityPageSignals? signals, string? text)
    {
      if (BotWallRegex().IsMatch(signals?.Title ?? string.Empty) || BotWallRegex().IsMatch(signals?.Heading ?? string.Empty)) return true;

      return text != null && text.Length < ShortPageLength && BotWallRegex().IsMatch(text);
    }

    public static bool IsGone(CourseQualityPageSignals? signals, string? text)
    {
      var lead = text == null ? string.Empty : text.Length <= LeadTextLength ? text : text[..LeadTextLength];

      if (CourseQualityPageMatch.IsSoftNotFound(Request(signals, null), lead)) return true;

      return ExpiredRegex().IsMatch(signals?.Title ?? string.Empty)
        || ExpiredRegex().IsMatch(signals?.Heading ?? string.Empty)
        || ExpiredRegex().IsMatch(lead);
    }

    /// <summary>
    /// A sign-in address, a page named "Sign in", or, on a short page only, wording that signing in is required. A long
    /// page that mentions signing in (a comment box, a menu) is a real page.
    /// </summary>
    public static bool IsSignInWall(CourseQualityPageSignals? signals, string? text, string? finalUrl)
    {
      var request = Request(signals, finalUrl);

      if (CourseQualityPageMatch.IsLoginWalled(request, string.Empty)) return true;

      return text != null && text.Length < ShortPageLength && CourseQualityPageMatch.IsLoginWalled(request, text);
    }
    #endregion

    #region Private Members
    private static CourseQualityAIRequest Request(CourseQualityPageSignals? signals, string? finalUrl) => new()
    {
      Title = string.Empty,
      Description = string.Empty,
      SourcePageSignals = signals,
      SourcePageFinalUrl = finalUrl
    };

    [GeneratedRegex(@"just\s+a\s+moment|checking\s+your\s+browser|verify\s+(?:that\s+)?you\s+are\s+(?:a\s+)?human|attention\s+required|are\s+you\s+a\s+robot|access\s+denied|captcha|unusual\s+traffic|request\s+unsuccessful|ddos\s+protection|security\s+check|client\s+challenge|enable\s+javascript\s+and\s+cookies", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex BotWallRegex();

    [GeneratedRegex(@"\b(?:page|link|listing|offer|opportunity|course|job|vacancy|posting|advert|event)\s+(?:has\s+|is\s+|was\s+)?(?:now\s+)?expired\b|\bthis\s+(?:page|link)\s+(?:is\s+)?(?:no\s+longer\s+(?:available|active|valid)|has\s+been\s+(?:removed|deleted|taken\s+down))\b|\bpage\s+gone\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex ExpiredRegex();
    #endregion
  }
}
