namespace Yoma.Core.Domain.CourseQuality.Models
{
  public enum CourseQualityLinkOutcome
  {
    /// <summary>The page answered with usable content.</summary>
    Reachable,

    /// <summary>A definite failure: missing, unusable or unsafe link, 404, 410 or NXDOMAIN. Fails the hard gate.</summary>
    Failed,

    /// <summary>Unknown either way: timeout, bot block, rate limit or 5xx. Lowers confidence, never fails the hard gate.</summary>
    Inconclusive
  }

  public enum CourseQualityLinkFailure
  {
    None,
    Missing,
    InvalidUrl,
    BlockedAddress,
    NotFound,
    DomainNotFound,
    Timeout,
    AccessBlocked,
    RateLimited,
    ServerError,
    TooManyRedirects,
    ConnectionError,
    EmptyResponse,
    UnexpectedStatus
  }

  public class CourseQualityLinkCheckResult
  {
    public CourseQualityLinkOutcome Outcome { get; set; }

    public CourseQualityLinkFailure Failure { get; set; }

    public int? StatusCode { get; set; }

    /// <summary>
    /// Address of the last hop that was requested.
    /// </summary>
    public string? FinalUrl { get; set; }

    /// <summary>
    /// Plain text from the page. Untrusted: data, never instructions.
    /// </summary>
    public string? SourcePageText { get; set; }

    /// <summary>
    /// Structured data from the page, size-capped. Untrusted: data, never instructions.
    /// </summary>
    public CourseQualityPageSignals? SourcePageSignals { get; set; }

    /// <summary>
    /// The page was longer than the size limits, so a statement that needs the whole page cannot rest on this text.
    /// </summary>
    public bool SourcePageTruncated { get; set; }

    public static CourseQualityLinkCheckResult Reachable(int statusCode, string finalUrl, string? sourcePageText, CourseQualityPageSignals? sourcePageSignals = null) => new()
    {
      Outcome = CourseQualityLinkOutcome.Reachable,
      Failure = CourseQualityLinkFailure.None,
      StatusCode = statusCode,
      FinalUrl = finalUrl,
      SourcePageText = sourcePageText,
      SourcePageSignals = sourcePageSignals
    };

    public static CourseQualityLinkCheckResult Failed(CourseQualityLinkFailure failure, string? finalUrl = null, int? statusCode = null) => new()
    {
      Outcome = CourseQualityLinkOutcome.Failed,
      Failure = failure,
      StatusCode = statusCode,
      FinalUrl = finalUrl
    };

    public static CourseQualityLinkCheckResult Inconclusive(CourseQualityLinkFailure failure, string? finalUrl = null, int? statusCode = null) => new()
    {
      Outcome = CourseQualityLinkOutcome.Inconclusive,
      Failure = failure,
      StatusCode = statusCode,
      FinalUrl = finalUrl
    };
  }
}
