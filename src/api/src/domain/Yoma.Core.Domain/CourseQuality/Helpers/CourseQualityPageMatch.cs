using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  public enum CourseQualityPageMatchLevel
  {
    /// <summary>Nothing ties the page to the listing.</summary>
    None,

    /// <summary>The body mentions the listing's words, but the page is not about it.</summary>
    Weak,

    /// <summary>The page's own name is the listing's.</summary>
    Strong
  }

  /// <summary>
  /// Decides whether a working link leads to this listing's page. This stops a link to the wrong page earning a badge.
  /// </summary>
  public static partial class CourseQualityPageMatch
  {
    #region Class Variables
    private const int CatalogueNames = 2;
    private const decimal WeakCoverage = 0.8m;
    private const int MaxNameWords = 64;
    private const int MinBadgeBodyLength = 400;
    private const string TrailingCourseWord = "course";
    private const int ThinTextLength = 200;
    private const int ShortTextLength = 500;
    private const int LeadTextLength = 500;
    private const int RegexTimeoutMilliseconds = 1000;
    private const RegexOptions SafeOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    // words that say nothing about the subject, so "Free Online Courses" does not match "Free Online Course in Marketing"
    private static readonly HashSet<string> GenericWords = new(StringComparer.OrdinalIgnoreCase)
    {
      "a", "an", "and", "at", "by", "for", "from", "in", "into", "of", "on", "or", "the", "to", "with", "your", "you",
      "course", "class", "online", "free", "introduction", "intro", "basic", "fundamental", "learn", "learning",
      "beginner", "guide", "certificate", "diploma", "training", "programme", "program", "webinar", "module"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
      "a", "an", "and", "at", "by", "for", "from", "in", "into", "of", "on", "or", "the", "to", "with", "your", "you"
    };
    #endregion

    #region Public Members
    public static CourseQualityPageMatchLevel Assess(CourseQualityAIRequest request, string text, CourseQualityStructuredFacts facts)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));

      if (RedirectedHome(request)) return CourseQualityPageMatchLevel.None;

      var listing = Tokens(request.Title);
      if (listing.Count == 0) return CourseQualityPageMatchLevel.Weak; // nothing distinctive to compare

      var signals = request.SourcePageSignals;
      var listPage = IsListPage(request, facts);

      // only the heading a visitor sees: structured data is the partner's own claim, and a name in it proves nothing alone
      var heading = signals?.Heading;
      var visible = !string.IsNullOrWhiteSpace(heading) && text.Contains(heading, StringComparison.OrdinalIgnoreCase);

      // only the verified organisation: Provider is partner input, and could erase a word such as "Advanced"
      var providers = new[] { request.OrganizationName };
      if (!listPage && visible && NameWords(heading, providers).SequenceEqual(NameWords(request.Title, providers))) return CourseQualityPageMatchLevel.Strong;

      var named = new List<string?> { heading, signals?.Title, signals?.MetaTags.GetValueOrDefault("og:title"), signals?.MetaTags.GetValueOrDefault("twitter:title") };
      named.AddRange(facts.Names);

      return listPage || named.Any(o => Coverage(listing, o) >= WeakCoverage) || Coverage(listing, text) >= WeakCoverage
        ? CourseQualityPageMatchLevel.Weak
        : CourseQualityPageMatchLevel.None;
    }

    public static bool NameMatches(string listingTitle, string? name)
    {
      var listing = NameWords(listingTitle);
      return listing.Count > 0 && NameWords(name).SequenceEqual(listing);
    }

    /// <summary>
    /// An error page served with HTTP 200. A listing that is itself about such errors is not one.
    /// </summary>
    public static bool IsSoftNotFound(CourseQualityAIRequest request, string text)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));

      if (SoftNotFoundRegex().IsMatch(request.Title)) return false;

      var signals = request.SourcePageSignals;
      var lead = text.Length <= LeadTextLength ? text : text[..LeadTextLength];

      return SoftNotFoundRegex().IsMatch(signals?.Title ?? string.Empty)
        || SoftNotFoundRegex().IsMatch(signals?.Heading ?? string.Empty)
        || SoftNotFoundRegex().IsMatch(lead);
    }

    /// <summary>
    /// A page with some text, but too little visible body to stand behind a badge.
    /// </summary>
    public static bool IsThinBody(string text)
    {
      ArgumentNullException.ThrowIfNull(text, nameof(text));

      return text.Length < MinBadgeBodyLength;
    }

    /// <summary>
    /// A statement anywhere on the page that it is gone or closed, for keeping a reviewer's Verified, where a false alarm
    /// only means another look. Unlike <see cref="IsSoftNotFound"/> it looks past the opening and ignores the listing's title.
    /// </summary>
    public static bool IsGoneAnywhere(string text)
    {
      ArgumentNullException.ThrowIfNull(text, nameof(text));

      return SoftNotFoundRegex().IsMatch(text) || ClosedRegex().IsMatch(text);
    }

    /// <summary>
    /// Too little to judge: a thin page, a JavaScript shell or a bot challenge. Structured data cannot carry one, because a
    /// partner's claim needs a visible page behind it.
    /// </summary>
    public static bool IsUnreadable(CourseQualityPageSignals? signals, string text, CourseQualityStructuredFacts facts)
    {
      if (BotChallengeRegex().IsMatch(signals?.Title ?? string.Empty)) return true;

      return text.Length < ThinTextLength
        || (text.Length < ShortTextLength && (JavaScriptRegex().IsMatch(text) || BotChallengeRegex().IsMatch(text)));
    }

    /// <summary>
    /// The page in front of the course is a sign-in. A menu with a sign-in link is not one, so this needs the address, the
    /// page's own name or a statement that signing in is required.
    /// </summary>
    public static bool IsLoginWalled(CourseQualityAIRequest request, string text)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));

      var signals = request.SourcePageSignals;

      return (Uri.TryCreate(request.SourcePageFinalUrl, UriKind.Absolute, out var final) && LoginPathRegex().IsMatch(final.AbsolutePath))
        || LoginNameRegex().IsMatch(signals?.Title ?? string.Empty)
        || LoginNameRegex().IsMatch(signals?.Heading ?? string.Empty)
        || LoginWordingRegex().IsMatch(text);
    }
    #endregion

    #region Private Members
    /// <summary>
    /// A catalogue, category or search page can carry a listing's words without being that course.
    /// </summary>
    private static bool IsListPage(CourseQualityAIRequest request, CourseQualityStructuredFacts facts)
    {
      var signals = request.SourcePageSignals;

      if (facts.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count() >= CatalogueNames) return true;
      if (signals?.JsonLdBlocks.Any(o => ListSchemaRegex().IsMatch(o)) == true) return true;

      if (Uri.TryCreate(request.SourcePageFinalUrl, UriKind.Absolute, out var final) && ListAddressRegex().IsMatch(final.PathAndQuery)) return true;

      return CatalogueNameRegex().IsMatch(signals?.Heading ?? string.Empty)
        || CatalogueNameRegex().IsMatch(signals?.MetaTags.GetValueOrDefault("og:title") ?? string.Empty)
        || facts.Names.Any(o => CatalogueNameRegex().IsMatch(o));
    }

    private static bool RedirectedHome(CourseQualityAIRequest request)
    {
      if (!Uri.TryCreate(request.URL, UriKind.Absolute, out var listed) || !Uri.TryCreate(request.SourcePageFinalUrl, UriKind.Absolute, out var final)) return false;

      return listed.AbsolutePath.Trim('/').Length > 0 && final.AbsolutePath.Trim('/').Length == 0;
    }

    private static HashSet<string> Tokens(string? value)
    {
      if (string.IsNullOrWhiteSpace(value)) return [];

      return [.. TokenRegex().Matches(value).Take(2000)
        .Select(o => Singular(o.Value.ToLowerInvariant()))
        .Where(o => !GenericWords.Contains(o))];
    }

    /// <summary>
    /// The words that name a course, in order. Only joining words go, so "Introduction" and "Advanced" stay. A provider's
    /// name goes only as a separate label such as "Python | Northwind": as loose words it could take an identifying word
    /// with it. One closing "course" goes too, so a heading of "Data Analysis course" names "Data Analysis".
    /// </summary>
    private static List<string> NameWords(string? name, params string?[] providers)
    {
      if (string.IsNullOrWhiteSpace(name)) return [];

      var provider = providers.SelectMany(Words).ToHashSet();
      var segments = LabelSeparatorRegex().Split(name).Select(Words).Where(o => o.Count > 0).ToList();
      var subject = segments.Where(o => !o.All(provider.Contains)).ToList();

      var words = (subject.Count > 0 ? subject : segments).SelectMany(o => o).Take(MaxNameWords).ToList();

      if (words.Count > 1 && words[^1] == TrailingCourseWord) words.RemoveAt(words.Count - 1);

      return words;
    }

    private static List<string> Words(string? value)
    {
      return string.IsNullOrWhiteSpace(value) ? [] : [.. TokenRegex().Matches(value).Take(MaxNameWords).Select(o => o.Value.ToLowerInvariant()).Where(o => !StopWords.Contains(o))];
    }

    private static string Singular(string word)
    {
      return word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) && !word.EndsWith("is", StringComparison.Ordinal)
        ? word[..^1]
        : word;
    }

    private static decimal Coverage(HashSet<string> listing, string? candidate)
    {
      var tokens = Tokens(candidate);
      return (decimal)listing.Count(tokens.Contains) / listing.Count;
    }

    [GeneratedRegex(@"\s+[-–—|:]\s+|\s*\|\s*", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex LabelSeparatorRegex();

    [GeneratedRegex(@"""@type""\s*:\s*(?:\[[^\]]{0,120}?)?""(?:https?://schema\.org/|schema:)?(?:CollectionPage|SearchResultsPage|ItemList)""", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex ListSchemaRegex();

    [GeneratedRegex(@"[?&](?:category|categoryid|search|q)=|/(?:courses|catalog|catalogue|categories|category|search|browse|programmes|programs|topics?|subjects?|skills?|learn|paths?|tags?|collections?)(?:/?$|/?\?|/index\.|/?#)|/(?:categor(?:y|ies)|topics?|subjects?|skills?|learn|paths?|tags?|collections?|learning-paths?)/", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex ListAddressRegex();

    [GeneratedRegex(@"\b(?:courses|classes|programmes|programs|catalog|catalogue|categories|topics|paths?|collections?|bundles?|library|series|tracks?)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex CatalogueNameRegex();

    [GeneratedRegex(@"\b(?:enrol+ments?|registrations?|applications?|admissions?)\b[^.!?]{0,40}\b(?:closed|ended)\b|\b(?:course|programme|program|class|cohort|session|webinar|event|intake)\b[^.!?]{0,20}\b(?:has|have|is|was|are)\s+(?:now\s+|already\s+|been\s+)*(?:ended|finished|over|concluded|closed|archived|discontinued|cancell?ed|full)\b|\bhas\s+ended\b|\bhas\s+been\s+(?:archived|discontinued)\b|\bno\s+longer\s+offered\b|\bwill\s+not\s+run\b|\bnot\s+running\b|\bnot\s+accepting\s+(?:new\s+)?\w+|\bsold\s+out\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex ClosedRegex();

    [GeneratedRegex(@"/(?:log[-_]?in|sign[-_]?in)(?:/|\.|$)", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex LoginPathRegex();

    [GeneratedRegex(@"^\s*(?:log\s*in|sign\s*in)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex LoginNameRegex();

    [GeneratedRegex(@"redirecting\s+to\s+(?:the\s+)?(?:log\s*in|sign\s*in)|(?:must|need\s+to)\s+(?:be\s+)?(?:log\s*in|logged\s+in|sign\s*in|signed\s+in)|please\s+(?:log|sign)\s*in\s+to\s+(?:continue|view|access)", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex LoginWordingRegex();

    [GeneratedRegex(@"[\p{L}\p{Nd}]+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\b404\b|\bnot\s+found\b|\bpage\s+(?:does\s+not|doesn'?t)\s+exist\b|(?:could\s+not|couldn'?t|can'?t|cannot)\s+find\s+(?:the|that|this)\s+page\b|\b(?:page|course|listing)\s+(?:is\s+|has\s+been\s+)?(?:no\s+longer\s+available|removed|retired)\b|\bno\s+longer\s+accepting\s+(?:responses|applications|registrations?|enrol+ments?)\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex SoftNotFoundRegex();

    [GeneratedRegex(@"(?:enable|requires?|turn\s+on)\s+javascript|javascript\s+is\s+(?:required|disabled)|^\s*loading\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex JavaScriptRegex();

    [GeneratedRegex(@"just\s+a\s+moment|checking\s+your\s+browser|verify\s+you\s+are\s+(?:a\s+)?human|attention\s+required|are\s+you\s+a\s+robot|access\s+denied", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex BotChallengeRegex();
    #endregion
  }
}
