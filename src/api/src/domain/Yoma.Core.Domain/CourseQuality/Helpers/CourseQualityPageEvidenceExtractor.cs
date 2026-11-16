using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Finds what a learner can read before starting: a syllabus, outcomes, a certificate, assessment, languages and low-data
  /// signals. Page text is untrusted, so this only counts structure and matches fixed words with non-backtracking regexes on
  /// size-capped input; nothing from the page is kept but counts and flags.
  ///
  /// The page counts only when it was read and strongly matched to the listing, so another course's syllabus is never lent to
  /// it. Otherwise the listing's own description is read the same way, line by line.
  /// </summary>
  public static partial class CourseQualityPageEvidenceExtractor
  {
    #region Class Variables
    public const int MaxTextLength = 100_000;
    public const int MaxCount = 100;
    public const int MaxLanguages = 10;

    private const int MinListLineItems = 1;
    private const int MaxHeadingLineLength = 80;
    private const int LanguageWindow = 80;
    private const int RegexTimeoutMilliseconds = 1000;
    private const RegexOptions SafeOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    private static readonly string[] KnownLanguages =
    [
      "English", "French", "Spanish", "Portuguese", "Arabic", "Swahili", "Kiswahili", "Hausa", "Yoruba", "Igbo", "Amharic", "Oromo",
      "Somali", "Zulu", "isiZulu", "Xhosa", "isiXhosa", "Afrikaans", "Sesotho", "Setswana", "Shona", "Kinyarwanda", "Luganda",
      "Lingala", "Wolof", "Twi", "Malagasy", "Hindi", "Bengali", "Urdu", "Tagalog", "Indonesian", "Chinese", "Mandarin", "Russian",
      "German", "Italian", "Turkish", "Vietnamese", "Thai"
    ];
    #endregion

    #region Public Members
    /// <param name="pageText">Plain text of the course page; ignored unless <paramref name="pageMatched"/>.</param>
    /// <param name="pageMatched">The page was read and its main name is this listing's title.</param>
    public static CourseQualityPageEvidence Extract(string? pageText, CourseQualityPageSignals? signals, bool pageMatched, string? listingText, string listingTitle)
    {
      var text = pageMatched ? CourseQualitySafeText.Clean(pageText, MaxTextLength) : null;

      try
      {
        return text != null ? FromPage(text, signals, listingTitle) : FromListing(listingText);
      }
      catch (RegexMatchTimeoutException)
      {
        // nothing could be read safely, which grades as no syllabus rather than failing the assessment
        return new CourseQualityPageEvidence { Source = text != null ? CourseQualityEvidenceSource.Page : CourseQualityEvidenceSource.Listing };
      }
    }
    #endregion

    #region Private Members
    private static CourseQualityPageEvidence FromPage(string text, CourseQualityPageSignals? signals, string listingTitle)
    {
      var facts = CourseQualityStructuredData.Read(signals, listingTitle);
      var sections = signals?.Sections ?? [];
      var structured = string.Join(' ', signals?.JsonLdBlocks ?? []);

      var evidence = new CourseQualityPageEvidence
      {
        Source = CourseQualityEvidenceSource.Page,
        HasCourseJsonLd = facts.HasCourseType,
        JsonLdSyllabus = facts.SyllabusSections > 0,
        JsonLdTeaches = facts.Teaches > 0,
        JsonLdCredential = facts.HasCredential,
        OutcomeCount = Cap(Math.Max(facts.Teaches, SectionItems(sections, OutcomesRegex()))),
        ModuleCount = Cap(Math.Max(facts.SyllabusSections, Math.Max(SectionItems(sections, CurriculumRegex()), Numbered(text)))),
        PrerequisitesStated = facts.HasPrerequisites || PrerequisitesRegex().IsMatch(text),
        AudienceStated = facts.HasAudience || AudienceRegex().IsMatch(text),
        CertificateMentioned = facts.HasCredential || CertificateRegex().IsMatch(text),
        CertificatePaid = PaidCertificateRegex().IsMatch(text) || PaidCertificateRegex().IsMatch(structured),
        WordCount = Words(text)
      };

      Mentions(text, evidence);
      foreach (var language in facts.Languages) AddLanguage(evidence, language);

      return evidence;
    }

    private static CourseQualityPageEvidence FromListing(string? listingText)
    {
      var text = CourseQualitySafeText.Clean(listingText, MaxTextLength);
      var evidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Listing };
      if (text == null) return evidence;

      // a description may carry its own markup, which keeps more structure than its lines do
      var lines = ListingLines(listingText!);
      var sections = listingText!.Contains('<') ? CourseQualityPageSignalExtractor.FromHtml(listingText)?.Sections ?? [] : [];

      evidence.OutcomeCount = Cap(Math.Max(LineItems(lines, OutcomesRegex()), SectionItems(sections, OutcomesRegex())));
      evidence.ModuleCount = Cap(Math.Max(Math.Max(LineItems(lines, CurriculumRegex()), SectionItems(sections, CurriculumRegex())), Numbered(text)));
      evidence.PrerequisitesStated = PrerequisitesRegex().IsMatch(text);
      evidence.AudienceStated = AudienceRegex().IsMatch(text);
      evidence.CertificateMentioned = CertificateRegex().IsMatch(text);
      evidence.CertificatePaid = PaidCertificateRegex().IsMatch(text);
      evidence.WordCount = Words(text);

      Mentions(text, evidence);
      return evidence;
    }

    private static void Mentions(string text, CourseQualityPageEvidence evidence)
    {
      evidence.AssessmentMentioned = AssessmentRegex().IsMatch(text);
      evidence.MobileMentioned = MobileRegex().IsMatch(text);
      evidence.OfflineMentioned = OfflineRegex().IsMatch(text);
      evidence.DownloadableMentioned = DownloadableRegex().IsMatch(text);
      evidence.TextMentioned = TextBasedRegex().IsMatch(text);

      // only where the text speaks about language, so "Business English" in a title is not read as a course language
      foreach (Match context in LanguageContextRegex().Matches(text))
      {
        var window = text.Substring(context.Index, Math.Min(LanguageWindow, text.Length - context.Index));
        foreach (var language in KnownLanguages.Where(o => window.Contains(o, StringComparison.OrdinalIgnoreCase))) AddLanguage(evidence, language);
        if (evidence.Languages.Count >= MaxLanguages) break;
      }
    }

    private static void AddLanguage(CourseQualityPageEvidence evidence, string language)
    {
      if (evidence.Languages.Count >= MaxLanguages || evidence.Languages.Contains(language, StringComparer.OrdinalIgnoreCase)) return;
      evidence.Languages.Add(language);
    }

    /// <summary>
    /// The longest list under a heading that names it: list items directly under it, or one sub-heading per item.
    /// </summary>
    private static int SectionItems(List<CourseQualityPageSection> sections, Regex heading)
    {
      return sections.Where(o => heading.IsMatch(o.Heading)).Select(o => Math.Max(o.ListItems, o.SubHeadings)).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// "Module 1", "Lesson 2" and the like: the most distinct numbers under any one word.
    /// </summary>
    private static int Numbered(string text)
    {
      return NumberedRegex().Matches(text)
        .GroupBy(o => o.Groups[1].Value.ToLowerInvariant())
        .Select(o => o.Select(m => int.Parse(m.Groups[2].Value)).Distinct().Count())
        .DefaultIfEmpty(0).Max();
    }

    private static List<string> ListingLines(string value)
    {
      return [.. value.Replace("\r", string.Empty).Split('\n').Select(o => CourseQualitySafeText.Clean(o, MaxTextLength) ?? string.Empty)];
    }

    /// <summary>
    /// Bulleted or numbered lines after a short line that names the list, ending at the first line of plain prose.
    /// </summary>
    private static int LineItems(List<string> lines, Regex heading)
    {
      var best = 0;

      for (var i = 0; i < lines.Count; i++)
      {
        if (lines[i].Length == 0 || lines[i].Length > MaxHeadingLineLength || !heading.IsMatch(lines[i]) || BulletRegex().IsMatch(lines[i])) continue;

        var items = 0;
        for (var j = i + 1; j < lines.Count; j++)
        {
          if (lines[j].Length == 0) continue;
          if (!BulletRegex().IsMatch(lines[j])) break;
          items++;
        }

        if (items >= MinListLineItems) best = Math.Max(best, items);
      }

      return best;
    }

    private static int Words(string text)
    {
      var count = 0;
      var inWord = false;

      foreach (var c in text)
      {
        var letter = char.IsLetterOrDigit(c);
        if (letter && !inWord) count++;
        inWord = letter;
      }

      return count;
    }

    private static int Cap(int value) => Math.Min(value, MaxCount);

    [GeneratedRegex(@"what\s+you('|’)?(ll|\s+will)\s+learn|learning\s+outcomes?|course\s+outcomes?|objectives?|you\s+will\s+be\s+able\s+to|skills\s+you('|’)?(ll|\s+will)\s+gain|what\s+you\s+will\s+gain|by\s+the\s+end\s+of\s+(this|the)\s+course|key\s+takeaways", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex OutcomesRegex();

    [GeneratedRegex(@"curriculum|syllabus|course\s+(content|outline|structure|plan)|modules?\b|lessons?\b|units?\b|chapters?\b|topics\s+covered|what('|’)?s\s+inside|course\s+breakdown", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex CurriculumRegex();

    [GeneratedRegex(@"\b(module|lesson|unit|chapter|week|topic|session)\s+(\d{1,2})\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex NumberedRegex();

    [GeneratedRegex(@"prerequisites?|entry\s+requirements|before\s+you\s+(start|begin)|what\s+you\s+(will\s+)?need|no\s+prior\s+(experience|knowledge)|requirements\s*:", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex PrerequisitesRegex();

    [GeneratedRegex(@"who\s+(is\s+)?this\s+course\s+is\s+for|who\s+is\s+this\s+(course\s+)?for|who\s+should\s+(take|attend|enrol|enroll)|target\s+audience|intended\s+(for|audience)|suitable\s+for|designed\s+for", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex AudienceRegex();

    [GeneratedRegex(@"certificate|certification|diploma", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex CertificateRegex();

    [GeneratedRegex(@"(certificate|certification|diploma)[^.]{0,60}(\$|€|£|₦|\busd\b|\beur\b|\bgbp\b|\bngn\b|\bkes\b|\bzar\b|\bpay\b|purchase|\bbuy\b|\bfee\b|upgrade)|(\$|€|£|₦|\bpay\b|purchase|\bbuy\b|\bfee\b|upgrade)[^.]{0,60}(certificate|certification|diploma)", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex PaidCertificateRegex();

    [GeneratedRegex(@"\bquiz(zes)?\b|assessments?\b|\bexams?\b|examination|test\s+your|\bgraded\b|assignments?\b|final\s+test", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex AssessmentRegex();

    [GeneratedRegex(@"mobile[\s-]?(friendly|app|phone|device)|on\s+your\s+(phone|mobile)|smartphone|android|\bios\b|any\s+device", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex MobileRegex();

    [GeneratedRegex(@"offline|without\s+(an\s+)?internet|low[\s-]bandwidth|low[\s-]data|data[\s-]light", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex OfflineRegex();

    [GeneratedRegex(@"downloadable|\bdownload\b|\bpdfs?\b", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex DownloadableRegex();

    [GeneratedRegex(@"text[\s-]based|transcripts?\b|reading\s+materials?|printable", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex TextBasedRegex();

    [GeneratedRegex(@"\blanguages?\b|available\s+in|taught\s+in|subtitles?|captions?|translated", SafeOptions, RegexTimeoutMilliseconds)]
    private static partial Regex LanguageContextRegex();

    [GeneratedRegex(@"^\s*([-*•▪●◦–]|\d{1,2}[.)])\s+\S", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, RegexTimeoutMilliseconds)]
    private static partial Regex BulletRegex();
    #endregion
  }
}
