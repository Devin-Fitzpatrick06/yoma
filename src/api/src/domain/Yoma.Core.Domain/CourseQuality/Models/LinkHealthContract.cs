namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Mirrors the "Iteration 5" block of src/web/src/api/models/courseQuality.ts. Keep the two in step.
  /// </summary>
  public class LinkHealthRequest
  {
    /// <summary>
    /// At most 200 distinct addresses; duplicates are checked once.
    /// </summary>
    public List<string>? Urls { get; set; }

    /// <summary>
    /// Free text label carried into the report, for example "YouthHub".
    /// </summary>
    public string? Platform { get; set; }
  }

  public enum LinkHealthStatus
  {
    /// <summary>Loaded, and looks like a real page.</summary>
    Ok,

    /// <summary>Loaded after redirects to another address.</summary>
    Redirected,

    /// <summary>404 or 410, or a page that says it is gone.</summary>
    NotFound,

    /// <summary>Another 4xx or 5xx, a domain that does not exist, or a connection or TLS failure.</summary>
    Broken,

    /// <summary>Bot protection, a sign-in wall or a rate limit: could not confirm either way, so not counted as dead.</summary>
    Blocked,

    Timeout,

    /// <summary>Not an http(s) address.</summary>
    Invalid,

    /// <summary>A private, local or otherwise unsafe target: never fetched.</summary>
    Refused
  }

  public class LinkHealthResult
  {
    public string Url { get; set; } = null!;

    public LinkHealthStatus Status { get; set; }

    public int? HttpStatus { get; set; }

    public string? FinalUrl { get; set; }

    public long? DurationMs { get; set; }

    /// <summary>
    /// Plain words, fixed wording: never page text.
    /// </summary>
    public string Reason { get; set; } = null!;
  }

  public class LinkHealthReport
  {
    public string? Platform { get; set; }

    public int Checked { get; set; }

    /// <summary>
    /// Every status, zero included.
    /// </summary>
    public Dictionary<LinkHealthStatus, int> Counts { get; set; } = [];

    public List<LinkHealthResult> Results { get; set; } = [];

    public DateTimeOffset DateChecked { get; set; }
  }
}
