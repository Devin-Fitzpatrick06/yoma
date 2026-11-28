using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Link health for addresses from any platform (Yoma, YouthHub, 1MiO, ...). Every fetch goes through the safe link
  /// checker, so private, local and unsafe targets are refused, resolved addresses are pinned and redirects are checked
  /// hop by hop; this service only validates the list, schedules it and names each outcome.
  ///
  /// Hosts run in parallel up to <see cref="MaxConcurrentHosts"/>; one host's links run one after another, so the
  /// checker's per-host pacing (CourseQuality:LinkCheck:MinHostIntervalInMilliseconds) spaces them without holding up
  /// other hosts. Nothing is stored.
  ///
  /// One request has a time budget (CourseQuality:LinkHealth:BudgetInSeconds): a link not started before it runs out is
  /// returned as Timeout with a "not checked" reason, so a long list on one slow host still answers before the ingress
  /// gives up. Only that budget is read from the options; CourseQuality:Enabled is not, because this is a tool.
  /// </summary>
  public sealed partial class LinkHealthService : ILinkHealthService
  {
    #region Class Variables
    public const int MaxUrls = 200;
    public const int MaxConcurrentHosts = 8;
    public const int MaxUrlLength = 2048;
    public const int MaxPlatformLength = 100;

    public const string NotCheckedReason = "Not checked: the check ran out of time. Run these links again.";

    private const int RegexTimeoutMilliseconds = 1000;

    private readonly ILogger<LinkHealthService> _logger;
    private readonly ICourseQualityLinkChecker _linkChecker;
    private readonly TimeSpan _budget;
    private readonly TimeProvider _clock;
    #endregion

    #region Constructor
    /// <param name="clock">The system clock unless a test passes its own.</param>
    public LinkHealthService(ILogger<LinkHealthService> logger, ICourseQualityLinkChecker linkChecker, IOptions<CourseQualityOptions> options, TimeProvider? clock = null)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _linkChecker = linkChecker ?? throw new ArgumentNullException(nameof(linkChecker));
      ArgumentNullException.ThrowIfNull(options?.Value, nameof(options));
      _budget = TimeSpan.FromSeconds(Math.Max(1, options.Value.LinkHealth.BudgetInSeconds));
      _clock = clock ?? TimeProvider.System;
    }
    #endregion

    #region Public Members
    public async Task<LinkHealthReport> Check(LinkHealthRequest request, CancellationToken cancellationToken)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));

      var urls = Distinct(request.Urls);
      if (urls.Count == 0) throw new ValidationException("Add at least one link to check");
      if (urls.Count > MaxUrls) throw new ValidationException($"Check at most {MaxUrls} distinct links at a time; this list has {urls.Count}");

      var results = new LinkHealthResult?[urls.Count];
      var byHost = new Dictionary<string, List<(int Index, string Url, Uri Uri)>>(StringComparer.OrdinalIgnoreCase);

      for (var i = 0; i < urls.Count; i++)
      {
        var invalid = Invalid(urls[i], out var uri);
        if (invalid != null) { results[i] = invalid; continue; }

        if (!byHost.TryGetValue(uri!.IdnHost, out var group)) byHost[uri.IdnHost] = group = [];
        group.Add((i, urls[i], uri));
      }

      var deadline = _clock.GetUtcNow().Add(_budget);
      var notChecked = 0;

      await Parallel.ForEachAsync(byHost.Values, new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentHosts, CancellationToken = cancellationToken },
        async (group, token) =>
        {
          foreach (var (index, url, uri) in group)
          {
            // out of time: answer now rather than after the ingress has given up; a link already started finishes within its own timeout
            if (_clock.GetUtcNow() >= deadline)
            {
              results[index] = new LinkHealthResult { Url = url, Status = LinkHealthStatus.Timeout, Reason = NotCheckedReason };
              Interlocked.Increment(ref notChecked);
              continue;
            }

            results[index] = await CheckOne(url, uri, token);
          }
        });

      if (notChecked > 0 && _logger.IsEnabled(LogLevel.Warning))
        _logger.LogWarning("Link health ran out of its {budget} s budget; {count} links were not checked", _budget.TotalSeconds, notChecked);

      var report = new LinkHealthReport
      {
        Platform = Platform(request.Platform),
        Checked = urls.Count,
        Results = [.. results.Select(o => o!)],
        DateChecked = _clock.GetUtcNow()
      };

      foreach (var status in Enum.GetValues<LinkHealthStatus>()) report.Counts[status] = report.Results.Count(o => o.Status == status);

      if (_logger.IsEnabled(LogLevel.Information))
        _logger.LogInformation("Link health checked {count} links: {counts}", report.Checked, string.Join(", ", report.Counts.Where(o => o.Value > 0).Select(o => $"{o.Key} {o.Value}")));

      return report;
    }

    /// <summary>
    /// Names the safe checker's outcome for one address. Pure, so every status can be tested without a network.
    /// </summary>
    public static LinkHealthResult Classify(string url, Uri requested, CourseQualityLinkCheckResult check, long? durationMs)
    {
      ArgumentNullException.ThrowIfNull(requested, nameof(requested));
      ArgumentNullException.ThrowIfNull(check, nameof(check));

      var redirected = check.FinalUrl != null && Uri.TryCreate(check.FinalUrl, UriKind.Absolute, out var final) && MateriallyDifferent(requested, final);

      LinkHealthResult Result(LinkHealthStatus status, string reason) => new()
      {
        Url = url,
        Status = status,
        HttpStatus = check.StatusCode,
        FinalUrl = check.FinalUrl,
        DurationMs = durationMs,
        Reason = reason
      };

      return check.Failure switch
      {
        CourseQualityLinkFailure.None => Reachable(check, requested, redirected, Result),
        CourseQualityLinkFailure.NotFound => Result(LinkHealthStatus.NotFound, check.StatusCode == 410
          ? "The site says this page has been removed for good (410)."
          : redirected ? "The link redirects to a page that does not exist (404)." : "The page does not exist (404)."),
        CourseQualityLinkFailure.DomainNotFound => Result(LinkHealthStatus.Broken, "The website's domain does not exist, so the link cannot open."),
        CourseQualityLinkFailure.AccessBlocked => Result(LinkHealthStatus.Blocked, check.StatusCode is 401 or 403
          ? $"The site refused an automated check ({check.StatusCode}). It may work in a browser, so it is not counted as dead."
          : "The site's bot protection stopped the check. It may work in a browser, so it is not counted as dead."),
        CourseQualityLinkFailure.RateLimited => Result(LinkHealthStatus.Blocked, "The site asked the checker to slow down (429). Try again later; it is not counted as dead."),
        CourseQualityLinkFailure.ServerError => Result(LinkHealthStatus.Broken, $"The site had an error ({check.StatusCode}). It may be down for now or for good."),
        CourseQualityLinkFailure.UnexpectedStatus => Result(LinkHealthStatus.Broken, check.StatusCode.HasValue
          ? $"The site answered with an error or an unusable response ({check.StatusCode})."
          : "The site answered with an unusable response."),
        CourseQualityLinkFailure.TooManyRedirects => Result(LinkHealthStatus.Broken, "The link redirects too many times, so it never reaches a page."),
        CourseQualityLinkFailure.ConnectionError => Result(LinkHealthStatus.Broken, "Could not connect to the site securely: it is down, unreachable, or its certificate is not valid."),
        CourseQualityLinkFailure.EmptyResponse => Result(LinkHealthStatus.Broken, "The site answered with an empty page."),
        CourseQualityLinkFailure.Timeout => Result(LinkHealthStatus.Timeout, "The site took too long to answer."),
        CourseQualityLinkFailure.BlockedAddress => Result(LinkHealthStatus.Refused, RefusedReason(requested, check)),
        CourseQualityLinkFailure.InvalidUrl => check.StatusCode.HasValue
          ? Result(LinkHealthStatus.Broken, "The link redirects to an address that cannot be read.")
          : Result(LinkHealthStatus.Invalid, "This is not a web address that can be checked."),
        CourseQualityLinkFailure.Missing => Result(LinkHealthStatus.Invalid, "The link is empty."),
        _ => throw new InvalidOperationException($"{nameof(CourseQualityLinkFailure)} of '{check.Failure}' not supported")
      };
    }

    /// <summary>
    /// Another site, or another page on it. http to https, "www.", letter case and a trailing slash are not differences,
    /// and neither is the query, where tracking parameters come and go.
    /// </summary>
    public static bool MateriallyDifferent(Uri requested, Uri final)
    {
      ArgumentNullException.ThrowIfNull(requested, nameof(requested));
      ArgumentNullException.ThrowIfNull(final, nameof(final));

      return !string.Equals(Host(requested), Host(final), StringComparison.OrdinalIgnoreCase)
        || !string.Equals(Path(requested), Path(final), StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Private Members
    private async Task<LinkHealthResult> CheckOne(string url, Uri uri, CancellationToken cancellationToken)
    {
      var watch = Stopwatch.StartNew();
      try
      {
        var check = await _linkChecker.Check(uri.AbsoluteUri, cancellationToken);
        return Classify(url, uri, check, watch.ElapsedMilliseconds);
      }
      catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
      {
        // the checker reports bad links as results; anything else is a fault here, not a verdict on the link
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Link health could not check a link on {host}", uri.IdnHost);

        return new LinkHealthResult { Url = url, Status = LinkHealthStatus.Broken, DurationMs = watch.ElapsedMilliseconds, Reason = "The link could not be checked because of an unexpected error." };
      }
    }

    private static LinkHealthResult Reachable(CourseQualityLinkCheckResult check, Uri requested, bool redirected, Func<LinkHealthStatus, string, LinkHealthResult> result)
    {
      var signals = check.SourcePageSignals;
      var text = check.SourcePageText;

      if (LinkHealthPageRules.IsBotWall(signals, text))
        return result(LinkHealthStatus.Blocked, "The site showed a bot check or captcha instead of the page. It may work in a browser, so it is not counted as dead.");

      if (LinkHealthPageRules.IsGone(signals, text))
        return result(LinkHealthStatus.NotFound, "The page loads, but says it is gone, removed, expired or cannot be found.");

      if (LinkHealthPageRules.IsSignInWall(signals, text, check.FinalUrl))
        return result(LinkHealthStatus.Blocked, "The link leads to a sign-in page, so the page itself could not be checked. It is not counted as dead.");

      if (!redirected) return result(LinkHealthStatus.Ok, "The page loaded.");

      var final = new Uri(check.FinalUrl!);
      if (Path(final) == "/" && Path(requested) != "/")
        return result(LinkHealthStatus.Redirected, "The link now sends people to the site's home page; the page itself may have been removed.");

      return result(LinkHealthStatus.Redirected, string.Equals(Host(requested), Host(final), StringComparison.OrdinalIgnoreCase)
        ? "The page moved to another address on the same site. Update the link to the new address."
        : $"The link now leads to another site ({final.IdnHost}). Check that it is still the right page.");
    }

    private static LinkHealthResult? Invalid(string url, out Uri? uri)
    {
      uri = null;

      if (url.Length > MaxUrlLength) return InvalidResult(url, $"The link is longer than {MaxUrlLength} characters.");

      if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || !SchemeRegex().IsMatch(url))
        return InvalidResult(url, "This is not a full web address. It must start with http:// or https://.");

      if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        return InvalidResult(url, $"The link starts with \"{parsed.Scheme}:\", so it is not a web page (http or https).");

      if (string.IsNullOrEmpty(parsed.IdnHost)) return InvalidResult(url, "The web address has no site name.");

      uri = parsed;
      return null;
    }

    private static LinkHealthResult InvalidResult(string url, string reason) => new() { Url = url, Status = LinkHealthStatus.Invalid, Reason = reason };

    private static string RefusedReason(Uri requested, CourseQualityLinkCheckResult check)
    {
      if (!string.IsNullOrEmpty(requested.UserInfo)) return "The link contains a user name or password, which would be sent to the site, so it was not opened.";

      if (requested.Port is not (80 or 443)) return $"The link uses port {requested.Port}. Only the standard web ports (80 and 443) are checked.";

      if (check.FinalUrl != null && Uri.TryCreate(check.FinalUrl, UriKind.Absolute, out var final) && MateriallyDifferent(requested, final))
        return "The link redirects to a private, local or otherwise unsafe address, so the redirect was not followed.";

      return "The link points to a private, local or otherwise unsafe address, so it was not opened.";
    }

    private static List<string> Distinct(List<string>? urls)
    {
      var seen = new HashSet<string>(StringComparer.Ordinal);
      var result = new List<string>();

      foreach (var value in urls ?? [])
      {
        var url = value?.Trim();
        if (string.IsNullOrEmpty(url)) continue;

        // the same address written two ways (host case, default port) is one link
        var key = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri && SchemeRegex().IsMatch(url) ? uri.AbsoluteUri : url;
        if (seen.Add(key)) result.Add(url);
      }

      return result;
    }

    private static string? Platform(string? platform)
    {
      var value = platform?.Trim();
      if (string.IsNullOrEmpty(value)) return null;

      return value.Length <= MaxPlatformLength ? value : value[..MaxPlatformLength];
    }

    private static string Host(Uri uri)
    {
      var host = uri.IdnHost;
      return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    private static string Path(Uri uri)
    {
      var path = uri.AbsolutePath.TrimEnd('/');
      return path.Length == 0 ? "/" : path;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex SchemeRegex();
    #endregion
  }
}
