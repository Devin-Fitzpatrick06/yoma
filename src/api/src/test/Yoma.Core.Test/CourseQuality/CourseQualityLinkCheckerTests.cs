using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Xunit;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityLinkCheckerTests
  {
    #region Fakes
    private sealed class FakeResolver : ICourseQualityHostResolver
    {
      public Dictionary<string, Func<IPAddress[]>> Hosts { get; } = [];

      public Task<IPAddress[]> Resolve(string host, CancellationToken cancellationToken) =>
        Task.FromResult(IPAddress.TryParse(host, out var ip) ? [ip] : Hosts.TryGetValue(host, out var value) ? value() : [IPAddress.Parse("8.8.8.8")]);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
      public List<HttpRequestMessage> Requests { get; } = [];

      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
        Requests.Add(request);
        return respond(request, cancellationToken);
      }
    }

    private sealed class ThrowingStream(byte[] prefix, Exception error) : MemoryStream(prefix)
    {
      public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      {
        var read = await base.ReadAsync(buffer, cancellationToken);
        return read > 0 ? read : throw error;
      }
    }

    private sealed class StallingStream(byte[] prefix) : MemoryStream(prefix)
    {
      public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      {
        var read = await base.ReadAsync(buffer, cancellationToken);
        if (read > 0) return read;

        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
      }
    }

    private sealed class CountingStream(byte[] content) : MemoryStream(content)
    {
      public int BytesRead { get; private set; }

      public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      {
        var read = await base.ReadAsync(buffer, cancellationToken);
        BytesRead += read;
        return read;
      }
    }

    private sealed class CountingInflateStream(Stream inner) : Stream
    {
      public int BytesRead { get; private set; }
      public override bool CanRead => true;
      public override bool CanSeek => false;
      public override bool CanWrite => false;
      public override long Length => throw new NotSupportedException();
      public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
      public override void Flush() { }
      public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
      public override void SetLength(long value) => throw new NotSupportedException();
      public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

      public override int Read(byte[] buffer, int offset, int count)
      {
        var read = inner.Read(buffer, offset, count);
        BytesRead += read;
        return read;
      }

      public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
      {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        BytesRead += read;
        return read;
      }
    }

    private static CourseQualityLinkChecker Checker(HttpMessageHandler handler, ICourseQualityHostResolver? resolver = null, Action<CourseQualityLinkCheckOptions>? configure = null)
    {
      var options = new CourseQualityOptions();
      options.LinkCheck.MinHostIntervalInMilliseconds = 0;
      configure?.Invoke(options.LinkCheck);
      return new CourseQualityLinkChecker(NullLogger<CourseQualityLinkChecker>.Instance, Options.Create(options), resolver ?? new FakeResolver(), handler);
    }

    private static HttpResponseMessage Html(string body, HttpStatusCode status = HttpStatusCode.OK) =>
      new(status) { Content = new StringContent(body, Encoding.UTF8, "text/html") };

    private static HttpResponseMessage Redirect(string location) => new(HttpStatusCode.Found) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    private static FakeHandler Respond(params HttpResponseMessage[] responses)
    {
      var queue = new Queue<HttpResponseMessage>(responses);
      return new FakeHandler((_, _) => Task.FromResult(queue.Dequeue()));
    }
    #endregion

    #region Address policy
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.8.8.8")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.100.100.200")]
    [InlineData("168.63.129.16")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:0:127.0.0.1")]
    [InlineData("::127.0.0.1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("fc00::1")]
    [InlineData("fd00:ec2::254")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    public void NonPublicAddresses_AreRejected(string address)
    {
      Assert.False(CourseQualityAddressPolicy.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("2606:4700:4700::1111")]
    public void PublicAddresses_AreAllowed(string address)
    {
      Assert.True(CourseQualityAddressPolicy.IsPublic(IPAddress.Parse(address)));
    }
    #endregion

    #region SSRF
    [Theory]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[::ffff:7f00:1]/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://2130706433/")]
    [InlineData("http://0x7f.0.0.1/")]
    [InlineData("http://017700000001/")]
    public async Task PrivateTargets_AreBlocked_WithoutARequest(string url)
    {
      var handler = Respond(Html("secret"));

      var result = await Checker(handler).Check(url, CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Failed, result.Outcome);
      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
      Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.org/x")]
    [InlineData("gopher://example.org/")]
    [InlineData("https://user:pass@example.org/")]
    public async Task UnsafeSchemesAndCredentials_AreBlocked(string url)
    {
      var handler = Respond(Html("x"));

      var result = await Checker(handler).Check(url, CancellationToken.None);

      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
      Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task HostResolvingToPrivateAddress_IsBlocked()
    {
      var resolver = new FakeResolver();
      resolver.Hosts["internal.example.org"] = () => [IPAddress.Parse("10.0.0.5")];
      var handler = Respond(Html("x"));

      var result = await Checker(handler, resolver).Check("https://internal.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
      Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task HostWithOnePrivateAnswerAmongPublicOnes_IsBlocked()
    {
      var resolver = new FakeResolver();
      resolver.Hosts["mixed.example.org"] = () => [IPAddress.Parse("8.8.8.8"), IPAddress.Parse("127.0.0.1")];
      var handler = Respond(Html("x"));

      var result = await Checker(handler, resolver).Check("https://mixed.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
      Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:pass@example.org/")]
    public async Task RedirectToUnsafeTarget_IsBlockedBeforeTheSecondRequest(string location)
    {
      var handler = Respond(Redirect(location), Html("secret"));

      var result = await Checker(handler).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
      Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RedirectToHostResolvingPrivate_IsBlocked()
    {
      var resolver = new FakeResolver();
      resolver.Hosts["evil.example.org"] = () => [IPAddress.Parse("192.168.0.10")];
      var handler = Respond(Redirect("https://evil.example.org/"), Html("secret"));

      var result = await Checker(handler, resolver).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
      Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RebindingBetweenHops_IsCaught_BecauseEveryHopResolvesAgain()
    {
      var calls = 0;
      var resolver = new FakeResolver();
      resolver.Hosts["rebind.example.org"] = () => ++calls == 1 ? [IPAddress.Parse("8.8.8.8")] : [IPAddress.Parse("127.0.0.1")];
      var handler = Respond(Redirect("/next"), Html("secret"));

      var result = await Checker(handler, resolver).Check("https://rebind.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
      Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PinnedHandler_RefusesARequestWithoutValidatedAddresses()
    {
      using var client = new HttpClient(CourseQualityLinkChecker.CreatePinnedHandler());

      var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:9/", TestContext.Current.CancellationToken));

      Assert.Contains("address policy", error.InnerException?.Message ?? error.Message);
    }

    [Fact]
    public void PinnedHandler_DisablesAutomaticBehaviours()
    {
      using var handler = CourseQualityLinkChecker.CreatePinnedHandler();

      Assert.False(handler.AllowAutoRedirect);
      Assert.False(handler.UseCookies);
      Assert.False(handler.UseProxy);
      Assert.Null(handler.Credentials);
    }

    [Fact]
    public async Task Requests_CarryNoCredentialsOrCookies()
    {
      var handler = Respond(Html("<p>Hello</p>"));

      await Checker(handler).Check("https://courses.example.org/", CancellationToken.None);

      var headers = handler.Requests.Single().Headers;
      Assert.Null(headers.Authorization);
      Assert.False(headers.Contains("Cookie"));
    }
    #endregion

    #region Production resolver and handler
    [Fact]
    public async Task Check_CarriesPageSignals_ForHtml()
    {
      const string html = """<html><head><title>Data</title><script type="application/ld+json">{"@type":"Course"}</script></head><body><nav>Free</nav><footer>Free</footer><p>Body</p></body></html>""";
      using var checker = Checker(new FakeHandler((_, _) => Task.FromResult(Html(html))));

      var result = await checker.Check("https://example.org/", CancellationToken.None);

      Assert.Equal("Data", result.SourcePageSignals!.Title);
      Assert.Single(result.SourcePageSignals.JsonLdBlocks);
      Assert.DoesNotContain("Free", result.SourcePageText);
    }

    [Fact]
    public async Task Check_HasNoSignals_ForPlainText()
    {
      using var checker = Checker(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent("<title>Data</title>", Encoding.UTF8, "text/plain")
      })));

      Assert.Null((await checker.Check("https://example.org/", CancellationToken.None)).SourcePageSignals);
    }

    private static CourseQualityLinkChecker RealChecker() => new(NullLogger<CourseQualityLinkChecker>.Instance, Options.Create(new CourseQualityOptions()),
      new DnsCourseQualityHostResolver(), CourseQualityLinkChecker.CreatePinnedHandler());

    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("http://localhost:5432/")]
    [InlineData("http://localhost./")]
    [InlineData("http://127.1/")]
    [InlineData("http://0/")]
    [InlineData("http://[::]/")]
    [InlineData("http://example.org%2f@127.0.0.1/")]
    public async Task RealResolver_RefusesLocalTargets(string url)
    {
      using var checker = RealChecker();

      var result = await checker.Check(url, TestContext.Current.CancellationToken);

      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
    }

    /// <summary>
    /// Off by default because it needs the internet. Proves the pinned connection, TLS to the original host and real text extraction.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/", CourseQualityLinkOutcome.Reachable)]
    [InlineData("http://example.com/", CourseQualityLinkOutcome.Reachable)]
    [InlineData("https://www.iana.org/nonexistent-page-for-yoma-check", CourseQualityLinkOutcome.Failed)]
    [InlineData("https://no-such-host-yoma-check.invalid/", CourseQualityLinkOutcome.Failed)]
    public async Task Live_PinnedConnection(string url, CourseQualityLinkOutcome outcome)
    {
      Assert.SkipWhen(string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("COURSE_QUALITY_LIVE")), "COURSE_QUALITY_LIVE is not set");
      using var checker = RealChecker();

      var result = await checker.Check(url, TestContext.Current.CancellationToken);

      Assert.True(result.Outcome == outcome, $"{result.Outcome} {result.Failure} {result.StatusCode}");
      if (outcome == CourseQualityLinkOutcome.Reachable) Assert.Contains("Example Domain", result.SourcePageText);
    }
    #endregion

    #region Ports and pacing
    // finding 11
    [Theory]
    [InlineData("http://courses.example.org:8080/x")]
    [InlineData("https://courses.example.org:8443/x")]
    [InlineData("http://courses.example.org:22/x")]
    public async Task Port_OtherThan80Or443_IsBlocked(string url)
    {
      var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
      using var checker = new CourseQualityLinkChecker(NullLogger<CourseQualityLinkChecker>.Instance, Options.Create(Fast()), new FakeResolver(), handler);

      var result = await checker.Check(url, TestContext.Current.CancellationToken);

      Assert.Equal(CourseQualityLinkOutcome.Failed, result.Outcome);
      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
      Assert.Empty(handler.Requests);
    }

    // finding 11
    [Fact]
    public async Task Redirect_ToAnotherPort_IsBlocked()
    {
      var handler = new FakeHandler((_, _) =>
      {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri("http://courses.example.org:6379/");
        return Task.FromResult(response);
      });
      using var checker = new CourseQualityLinkChecker(NullLogger<CourseQualityLinkChecker>.Instance, Options.Create(Fast()), new FakeResolver(), handler);

      var result = await checker.Check("https://courses.example.org/x", TestContext.Current.CancellationToken);

      Assert.Equal(CourseQualityLinkFailure.BlockedAddress, result.Failure);
    }

    // finding 5
    [Fact]
    public async Task Pacing_SpacesRequestsToOneHost()
    {
      var options = new CourseQualityOptions();
      options.LinkCheck.MinHostIntervalInMilliseconds = 300;
      var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
      using var checker = new CourseQualityLinkChecker(NullLogger<CourseQualityLinkChecker>.Instance, Options.Create(options), new FakeResolver(), handler);

      var watch = System.Diagnostics.Stopwatch.StartNew();
      await checker.Check("https://courses.example.org/a", TestContext.Current.CancellationToken);
      await checker.Check("https://courses.example.org/b", TestContext.Current.CancellationToken);

      Assert.True(watch.ElapsedMilliseconds >= 280, $"{watch.ElapsedMilliseconds}");
    }

    // finding 7
    [Fact]
    public async Task LongPage_IsMarkedTruncated()
    {
      var options = Fast();
      options.LinkCheck.MaxSourceTextLength = 100;
      var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"<html><body><p>{new string('x', 50)} {new string('y', 500)} Fee: $49</p></body></html>", Encoding.UTF8, "text/html") }));
      using var checker = new CourseQualityLinkChecker(NullLogger<CourseQualityLinkChecker>.Instance, Options.Create(options), new FakeResolver(), handler);

      var result = await checker.Check("https://courses.example.org/x", TestContext.Current.CancellationToken);

      Assert.True(result.SourcePageTruncated);
    }

    private static CourseQualityOptions Fast()
    {
      var options = new CourseQualityOptions();
      options.LinkCheck.MinHostIntervalInMilliseconds = 0;
      return options;
    }
    #endregion

    #region Bot walls
    [Theory]
    [InlineData(HttpStatusCode.MethodNotAllowed, "x-amzn-waf-action", "captcha")]
    [InlineData(HttpStatusCode.Accepted, "x-amzn-waf-action", "challenge")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "cf-mitigated", "challenge")]
    public async Task BotWall_IsReportedAsBlocked_WhateverItsStatus(HttpStatusCode status, string header, string value)
    {
      var handler = new FakeHandler((_, _) =>
      {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("<html>Human verification</html>")) };
        response.Headers.TryAddWithoutValidation(header, value);
        return Task.FromResult(response);
      });
      using var checker = new CourseQualityLinkChecker(NullLogger<CourseQualityLinkChecker>.Instance, Options.Create(Fast()), new FakeResolver(), handler);

      var result = await checker.Check("https://courses.example.org/x", TestContext.Current.CancellationToken);

      Assert.Equal(CourseQualityLinkOutcome.Inconclusive, result.Outcome);
      Assert.Equal(CourseQualityLinkFailure.AccessBlocked, result.Failure);
    }
    #endregion

    #region Limits
    [Fact]
    public async Task CompressedBomb_IsCappedAfterDecompression()
    {
      var compressed = new MemoryStream();
      await using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true))
        gzip.Write(new byte[20_000_000]);
      compressed.Position = 0;

      var inflated = new CountingInflateStream(new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionMode.Decompress));
      var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(inflated) };
      response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

      var result = await Checker(Respond(response), configure: o => o.MaxResponseBytes = 4096).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Reachable, result.Outcome);
      Assert.Equal(4096, inflated.BytesRead);
    }

    [Fact]
    public async Task TooManyRedirects_IsInconclusive()
    {
      var handler = new FakeHandler((_, _) => Task.FromResult(Redirect("https://courses.example.org/again")));

      var result = await Checker(handler, configure: o => o.MaxRedirects = 2).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Inconclusive, result.Outcome);
      Assert.Equal(CourseQualityLinkFailure.TooManyRedirects, result.Failure);
      Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task ResponseBytes_AreCapped_AndNotReadBeyondTheLimit()
    {
      var stream = new CountingStream(Encoding.UTF8.GetBytes(new string('a', 5000)));
      var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
      response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

      var result = await Checker(Respond(response), configure: o => o.MaxResponseBytes = 1000).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Reachable, result.Outcome);
      Assert.Equal(1000, result.SourcePageText!.Length);
      Assert.Equal(1000, stream.BytesRead);
    }

    [Fact]
    public async Task SourceText_IsCapped()
    {
      var result = await Checker(Respond(Html($"<p>{new string('a', 500)}</p>")), configure: o => o.MaxSourceTextLength = 100).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(100, result.SourcePageText!.Length);
    }

    [Fact]
    public async Task Timeout_IsInconclusive()
    {
      var handler = new FakeHandler(async (_, token) =>
      {
        await Task.Delay(Timeout.Infinite, token);
        return Html("never");
      });

      var result = await Checker(handler, configure: o => o.TimeoutInSeconds = 1).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkFailure.Timeout, result.Failure);
      Assert.Equal(CourseQualityLinkOutcome.Inconclusive, result.Outcome);
    }

    [Fact]
    public async Task CallerCancellation_Throws()
    {
      using var source = new CancellationTokenSource();
      await source.CancelAsync();
      var handler = new FakeHandler((_, token) => Task.FromCanceled<HttpResponseMessage>(token));

      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Checker(handler).Check("https://courses.example.org/", source.Token));
    }
    #endregion

    #region Classification
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task DefiniteFailures(HttpStatusCode status)
    {
      var result = await Checker(Respond(Html("gone", status))).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Failed, result.Outcome);
      Assert.Equal(CourseQualityLinkFailure.NotFound, result.Failure);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, CourseQualityLinkFailure.AccessBlocked)]
    [InlineData(HttpStatusCode.Unauthorized, CourseQualityLinkFailure.AccessBlocked)]
    [InlineData(HttpStatusCode.TooManyRequests, CourseQualityLinkFailure.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, CourseQualityLinkFailure.ServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, CourseQualityLinkFailure.ServerError)]
    [InlineData(HttpStatusCode.BadRequest, CourseQualityLinkFailure.UnexpectedStatus)]
    public async Task InconclusiveStatuses(HttpStatusCode status, CourseQualityLinkFailure failure)
    {
      var result = await Checker(Respond(Html("x", status))).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Inconclusive, result.Outcome);
      Assert.Equal(failure, result.Failure);
    }

    [Theory]
    [InlineData(SocketError.HostNotFound, CourseQualityLinkOutcome.Failed)]
    [InlineData(SocketError.TryAgain, CourseQualityLinkOutcome.Inconclusive)]
    public async Task DnsFailures(SocketError error, CourseQualityLinkOutcome outcome)
    {
      var resolver = new Mock<ICourseQualityHostResolver>();
      resolver.Setup(o => o.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new SocketException((int)error));

      var result = await Checker(Respond(), resolver.Object).Check("https://nope.example.org/", CancellationToken.None);

      Assert.Equal(outcome, result.Outcome);
    }

    [Theory]
    [InlineData(null, CourseQualityLinkFailure.Missing)]
    [InlineData("  ", CourseQualityLinkFailure.Missing)]
    [InlineData("not a url", CourseQualityLinkFailure.InvalidUrl)]
    public async Task MissingOrInvalidUrls_Fail(string? url, CourseQualityLinkFailure failure)
    {
      var result = await Checker(Respond()).Check(url, CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Failed, result.Outcome);
      Assert.Equal(failure, result.Failure);
    }

    [Fact]
    public async Task Html_IsReducedToPlainText()
    {
      var html = "<html><head><title>Data course</title><style>p{color:red}</style></head><body><!-- hidden --><script>alert(1)</script><p>Fee:&nbsp;free &amp; open</p></body></html>";

      var result = await Checker(Respond(Html(html))).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal("Data course Fee: free & open", result.SourcePageText!.Replace(' ', ' '));
    }
    #endregion

    #region Defects found on review
    [Theory]
    [InlineData("utf-7")]
    [InlineData("not-a-charset")]
    public async Task UnsupportedCharset_FallsBackToUtf8_NotAnException(string charset)
    {
      var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("<p>Hello</p>")) };
      response.Content.Headers.TryAddWithoutValidation("Content-Type", $"text/html; charset={charset}");

      var result = await Checker(Respond(response)).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Reachable, result.Outcome);
      Assert.Equal("Hello", result.SourcePageText);
    }

    [Fact]
    public async Task ResolverRejectingTheHostName_IsAnInvalidUrl_NotAnException()
    {
      var resolver = new Mock<ICourseQualityHostResolver>();
      resolver.Setup(o => o.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentOutOfRangeException("hostName"));

      var result = await Checker(Respond(), resolver.Object).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkFailure.InvalidUrl, result.Failure);
    }

    [Fact]
    public async Task CorruptCompressedBody_IsInconclusive_NotAnException()
    {
      var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new ThrowingStream([], new InvalidDataException("bad gzip"))) };

      var result = await Checker(Respond(response)).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Inconclusive, result.Outcome);
    }

    [Fact]
    public async Task BodyThatStallsAfterContent_IsReachable_WithWhatArrived()
    {
      var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream(Encoding.UTF8.GetBytes("Partial page"))) };
      response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

      var result = await Checker(Respond(response), configure: o => o.TimeoutInSeconds = 1).Check("https://courses.example.org/", CancellationToken.None);

      Assert.Equal(CourseQualityLinkOutcome.Reachable, result.Outcome);
      Assert.Equal("Partial page", result.SourcePageText);
    }
    #endregion
  }
}
