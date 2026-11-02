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
  }
}
