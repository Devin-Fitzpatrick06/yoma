namespace Yoma.Core.Domain.CourseQuality.Models
{
  public enum CourseQualityEvidenceSource
  {
    /// <summary>The course's own page, read and strongly matched to the listing.</summary>
    Page,

    /// <summary>The listing's description only, because no matching page was read.</summary>
    Listing
  }

  /// <summary>
  /// What a learner can read about a course before starting, found by fixed checks on the page or the listing. Stored on the
  /// assessment for grading. Counts and flags only: no page text is kept.
  /// </summary>
  public class CourseQualityPageEvidence
  {
    public CourseQualityEvidenceSource Source { get; set; }

    public bool HasCourseJsonLd { get; set; }

    /// <summary>
    /// The Course JSON-LD lists syllabusSections.
    /// </summary>
    public bool JsonLdSyllabus { get; set; }

    /// <summary>
    /// The Course JSON-LD says what it teaches.
    /// </summary>
    public bool JsonLdTeaches { get; set; }

    /// <summary>
    /// The Course JSON-LD names an educationalCredentialAwarded.
    /// </summary>
    public bool JsonLdCredential { get; set; }

    /// <summary>
    /// Items in a "what you will learn", "learning outcomes" or "objectives" list.
    /// </summary>
    public int OutcomeCount { get; set; }

    /// <summary>
    /// Modules, lessons or units in a curriculum list, or numbered on the page.
    /// </summary>
    public int ModuleCount { get; set; }

    public bool PrerequisitesStated { get; set; }

    public bool AudienceStated { get; set; }

    public bool CertificateMentioned { get; set; }

    /// <summary>
    /// A certificate is mentioned next to a price or a payment, so the course may be free but its certificate is not.
    /// </summary>
    public bool CertificatePaid { get; set; }

    public bool AssessmentMentioned { get; set; }

    /// <summary>
    /// Languages the page says the course is in or has subtitles in.
    /// </summary>
    public List<string> Languages { get; set; } = [];

    public bool MobileMentioned { get; set; }

    public bool OfflineMentioned { get; set; }

    public bool DownloadableMentioned { get; set; }

    /// <summary>
    /// Text-based lessons, transcripts or reading material, which work on a weak connection.
    /// </summary>
    public bool TextMentioned { get; set; }

    public int WordCount { get; set; }
  }
}
