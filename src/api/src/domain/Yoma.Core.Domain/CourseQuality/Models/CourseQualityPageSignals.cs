namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Structured data lost when a page is reduced to plain text. Untrusted and size-capped: data, never instructions.
  /// </summary>
  public class CourseQualityPageSignals
  {
    public string? Title { get; set; }

    /// <summary>
    /// The first h1 on the page.
    /// </summary>
    public string? Heading { get; set; }

    /// <summary>
    /// The page footer, which page text leaves out, so a fee printed there is still seen.
    /// </summary>
    public string? Footer { get; set; }

    /// <summary>
    /// Raw schema.org JSON-LD blocks, exactly as served.
    /// </summary>
    public List<string> JsonLdBlocks { get; set; } = [];

    /// <summary>
    /// Allow-listed OpenGraph and meta tags, keyed by name or property.
    /// </summary>
    public Dictionary<string, string> MetaTags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Headings in page order with the list items and sub-headings under each, so a curriculum or outcomes list can be counted.
    /// </summary>
    public List<CourseQualityPageSection> Sections { get; set; } = [];
  }

  public class CourseQualityPageSection
  {
    /// <summary>
    /// 1 to 6, from h1 to h6.
    /// </summary>
    public int Level { get; set; }

    public string Heading { get; set; } = null!;

    /// <summary>
    /// List items between this heading and the next heading of any level.
    /// </summary>
    public int ListItems { get; set; }

    /// <summary>
    /// Deeper headings before the next heading at this level or above, for example one h3 per module under an h2.
    /// </summary>
    public int SubHeadings { get; set; }
  }
}
