using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Fetches a listing URL safely. Each hop is resolved and validated here, and the connection is pinned to the
  /// validated addresses, so a second DNS answer cannot redirect it (DNS rebinding). Redirects are followed by hand so
  /// every hop passes the same checks. No proxy, cookies or credentials are used.
  /// </summary>
  public sealed class CourseQualityLinkChecker : ICourseQualityLinkChecker, IDisposable
  {
    #region Class Variables
    private const int MaxPacedHosts = 10_000;

    private static readonly HttpRequestOptionsKey<IPAddress[]> PinnedAddressesKey = new("CourseQualityPinnedAddresses");

    private readonly ILogger<CourseQualityLinkChecker> _logger;
    private readonly CourseQualityLinkCheckOptions _options;
    private readonly ICourseQualityHostResolver _hostResolver;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nextRequestByHost = new(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Constructor
    public CourseQualityLinkChecker(ILogger<CourseQualityLinkChecker> logger,
      IOptions<CourseQualityOptions> options,
      ICourseQualityHostResolver hostResolver,
      HttpMessageHandler messageHandler)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      ArgumentNullException.ThrowIfNull(options, nameof(options));
      _options = options.Value.LinkCheck;
      _hostResolver = hostResolver ?? throw new ArgumentNullException(nameof(hostResolver));
      ArgumentNullException.ThrowIfNull(messageHandler, nameof(messageHandler));

      // limits are enforced per check through a linked token, not per client
      _httpClient = new HttpClient(messageHandler, true) { Timeout = Timeout.InfiniteTimeSpan };
    }
    #endregion

    #region Public Members
    /// <summary>
    /// The production handler. It connects only to the addresses pinned on the request, and fails closed without them.
    /// </summary>
    public static SocketsHttpHandler CreatePinnedHandler()
    {
      return new SocketsHttpHandler
      {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        Credentials = null,
        PreAuthenticate = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        MaxResponseHeadersLength = 64,
        ConnectCallback = ConnectToPinnedAddress
      };
    }

    public async Task<CourseQualityLinkCheckResult> Check(string? url, CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(url)) return CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.Missing);

      if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        return CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.InvalidUrl, url);

      using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutInSeconds));

      try
      {
        for (var hop = 0; hop <= _options.MaxRedirects; hop++)
        {
          var (blocked, addresses) = await ValidateHop(uri, timeout.Token);
          if (blocked != null) return blocked;

          await PaceHost(uri.IdnHost, cancellationToken);

          using var request = CreateRequest(uri, addresses!);
          using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

          if (IsRedirect(response))
          {
            if (response.Headers.Location == null)
              return CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.UnexpectedStatus, uri.AbsoluteUri, (int)response.StatusCode);

            if (!Uri.TryCreate(uri, response.Headers.Location, out var next))
              return CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.InvalidUrl, uri.AbsoluteUri, (int)response.StatusCode);

            uri = next;
            continue;
          }

          return await Classify(response, uri, timeout.Token);
        }

        return CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.TooManyRedirects, uri.AbsoluteUri);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (OperationCanceledException)
      {
        return CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout, uri.AbsoluteUri);
      }
      catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException or AuthenticationException or InvalidDataException)
      {
        if (FindInner<CourseQualityBlockedConnectionException>(ex) != null)
          return CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.BlockedAddress, uri.AbsoluteUri);

        if (_logger.IsEnabled(LogLevel.Information))
          _logger.LogInformation("Course quality link check could not connect: {errorMessage}", ex.Message);

        return CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.ConnectionError, uri.AbsoluteUri);
      }
    }

    public void Dispose()
    {
      _httpClient.Dispose();
    }
    #endregion

    #region Private Members
    private async Task<(CourseQualityLinkCheckResult? Blocked, IPAddress[]? Addresses)> ValidateHop(Uri uri, CancellationToken cancellationToken)
    {
      if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        return (CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.BlockedAddress, uri.AbsoluteUri), null);

      // a web page has no business on a database or admin port of a public host
      if (uri.Port is not (80 or 443))
        return (CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.BlockedAddress, uri.AbsoluteUri), null);

      // credentials in a URL would be sent to the host
      if (!string.IsNullOrEmpty(uri.UserInfo))
        return (CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.BlockedAddress), null);

      IPAddress[] addresses;
      try
      {
        addresses = await _hostResolver.Resolve(uri.IdnHost.Trim('[', ']'), cancellationToken);
      }
      catch (SocketException ex)
      {
        return (ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData
          ? CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.DomainNotFound, uri.AbsoluteUri)
          : CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.ConnectionError, uri.AbsoluteUri), null);
      }
      catch (ArgumentException)
      {
        // the resolver refuses names that DNS could never answer, for example over 255 characters
        return (CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.InvalidUrl, uri.AbsoluteUri), null);
      }

      if (addresses.Length == 0)
        return (CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.ConnectionError, uri.AbsoluteUri), null);

      // one internal answer is enough to refuse: a mixed result is how a rebinding host hides
      if (!addresses.All(CourseQualityAddressPolicy.IsPublic))
        return (CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.BlockedAddress, uri.AbsoluteUri), null);

      return (null, addresses);
    }

    /// <summary>
    /// Takes this host's next free slot and waits for it. The checker is a singleton, so the spacing holds across a whole run.
    /// </summary>
    private async Task PaceHost(string host, CancellationToken cancellationToken)
    {
      var interval = TimeSpan.FromMilliseconds(Math.Max(0, _options.MinHostIntervalInMilliseconds));
      if (interval == TimeSpan.Zero) return;

      var now = DateTimeOffset.UtcNow;
      var slot = _nextRequestByHost.AddOrUpdate(host, now + interval, (_, next) => (next > now ? next : now) + interval) - interval;

      if (_nextRequestByHost.Count > MaxPacedHosts) _nextRequestByHost.Clear();

      var wait = slot - now;
      if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(Uri uri, IPAddress[] addresses)
    {
      var request = new HttpRequestMessage(HttpMethod.Get, uri);
      request.Options.Set(PinnedAddressesKey, addresses);
      request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
      request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,text/plain;q=0.9,*/*;q=0.5");
      request.Headers.ConnectionClose = true; // a pooled connection must not outlive this check's validated addresses
      return request;
    }

    private static bool IsRedirect(HttpResponseMessage response)
    {
      return response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
        or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
    }

    /// <summary>
    /// These walls answer with whatever status suits them, 202, 405 or 503 among them, so only their own header tells.
    /// </summary>
    private static bool IsBotWall(HttpResponseMessage response)
    {
      return response.Headers.Contains("x-amzn-waf-action") || response.Headers.Contains("cf-mitigated");
    }

    private async Task<CourseQualityLinkCheckResult> Classify(HttpResponseMessage response, Uri uri, CancellationToken cancellationToken)
    {
      var statusCode = (int)response.StatusCode;
      var finalUrl = uri.AbsoluteUri;

      if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        return CourseQualityLinkCheckResult.Failed(CourseQualityLinkFailure.NotFound, finalUrl, statusCode);

      if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized || IsBotWall(response))
        return CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.AccessBlocked, finalUrl, statusCode);

      if (response.StatusCode == HttpStatusCode.TooManyRequests)
        return CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.RateLimited, finalUrl, statusCode);

      if (statusCode >= 500)
        return CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.ServerError, finalUrl, statusCode);

      if (!response.IsSuccessStatusCode)
        return CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.UnexpectedStatus, finalUrl, statusCode);

      var body = await ReadCapped(response, cancellationToken);
      if (body.Length == 0)
        return CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.EmptyResponse, finalUrl, statusCode);

      var (text, signals, truncated) = ExtractContent(response, body);
      var result = CourseQualityLinkCheckResult.Reachable(statusCode, finalUrl, text, signals);
      result.SourcePageTruncated = truncated || body.Length >= _options.MaxResponseBytes;
      return result;
    }

    private async Task<byte[]> ReadCapped(HttpResponseMessage response, CancellationToken cancellationToken)
    {
      var limit = _options.MaxResponseBytes;
      var buffer = new byte[limit];
      var total = 0;

      try
      {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        while (total < limit)
        {
          var read = await stream.ReadAsync(buffer.AsMemory(total, limit - total), cancellationToken);
          if (read == 0) break;
          total += read;
        }
      }
      catch (Exception ex) when (total > 0 && ex is OperationCanceledException or HttpRequestException or IOException or InvalidDataException)
      {
        // headers said 2xx and some content arrived, so the page exists; keep what was read, including at the time limit
      }

      return buffer[..total];
    }

    private (string? Text, CourseQualityPageSignals? Signals, bool Truncated) ExtractContent(HttpResponseMessage response, byte[] body)
    {
      var mediaType = response.Content.Headers.ContentType?.MediaType;
      var isHtml = mediaType is null or "text/html" or "application/xhtml+xml";
      if (!isHtml && mediaType != "text/plain") return (null, null, false);

      var content = Decode(body, response.Content.Headers.ContentType?.CharSet);

      var text = isHtml ? CourseQualityPageText.FromHtml(content, int.MaxValue) : CourseQualityPageText.FromPlainText(content, int.MaxValue);
      var truncated = text?.Length > _options.MaxSourceTextLength;

      return (truncated ? text![.._options.MaxSourceTextLength] : text, isHtml ? CourseQualityPageSignalExtractor.FromHtml(content) : null, truncated);
    }

    private static string Decode(byte[] body, string? charset)
    {
      try
      {
        if (!string.IsNullOrWhiteSpace(charset)) return Encoding.GetEncoding(charset.Trim('"', ' ')).GetString(body);
      }
      catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
      {
        // charset unknown to .NET or disabled, as UTF-7 is; fall through to UTF-8
      }

      return Encoding.UTF8.GetString(body);
    }

    private static T? FindInner<T>(Exception? ex) where T : Exception
    {
      for (; ex != null; ex = ex.InnerException)
        if (ex is T match) return match;

      return null;
    }

    private static async ValueTask<Stream> ConnectToPinnedAddress(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
      if (!context.InitialRequestMessage.Options.TryGetValue(PinnedAddressesKey, out var addresses) || addresses.Length == 0)
        throw new CourseQualityBlockedConnectionException();

      Exception? lastError = null;
      foreach (var address in addresses)
      {
        if (!CourseQualityAddressPolicy.IsPublic(address)) throw new CourseQualityBlockedConnectionException();

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
          await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
          return new NetworkStream(socket, ownsSocket: true);
        }
        catch (SocketException ex)
        {
          socket.Dispose();
          lastError = ex;
        }
        catch
        {
          socket.Dispose();
          throw;
        }
      }

      throw lastError!;
    }
    #endregion

    #region Nested Types
    private sealed class CourseQualityBlockedConnectionException : Exception
    {
      public CourseQualityBlockedConnectionException() : base("Connection refused by the course quality address policy") { }
    }
    #endregion
  }
}
