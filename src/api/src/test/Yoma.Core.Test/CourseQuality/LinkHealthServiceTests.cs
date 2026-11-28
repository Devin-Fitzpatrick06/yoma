using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using Xunit;
using Yoma.Core.Api.Controllers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;

namespace Yoma.Core.Test.CourseQuality
{
  /// <summary>
  /// The link health check runs the real safe link checker over a fake network: a fake resolver and a fake handler
  /// keyed by address, so every status is reached the way production reaches it.
  /// </summary>
  public class LinkHealthServiceTests
  {
    #region Fakes
    private sealed class FakeResolver : ICourseQualityHostResolver
    {
      public ConcurrentDictionary<string, Func<IPAddress[]>> Hosts { get; } = new(StringComparer.OrdinalIgnoreCase);

      public Task<IPAddress[]> Resolve(string host, CancellationToken cancellationToken) =>
        Task.FromResult(IPAddress.TryParse(host, out var ip) ? [ip] : Hosts.TryGetValue(host, out var value) ? value() : [IPAddress.Parse("8.8.8.8")]);
    }

    private sealed class FakeNetwork : HttpMessageHandler
    {
      private int _inFlight;

      public ConcurrentDictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> Pages { get; } = new(StringComparer.Ordinal);

      public ConcurrentQueue<string> Requested { get; } = new();

      public ConcurrentDictionary<string, int> InFlightByHost { get; } = new(StringComparer.OrdinalIgnoreCase);

      public int MaxInFlight { get; private set; }

      public int MaxInFlightOnOneHost { get; private set; }

      public TimeSpan Delay { get; set; }

      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
        var url = request.RequestUri!.AbsoluteUri;
        Requested.Enqueue(url);

        var inFlight = Interlocked.Increment(ref _inFlight);
        var onHost = InFlightByHost.AddOrUpdate(request.RequestUri.Host, 1, (_, value) => value + 1);
        lock (this)
        {
          MaxInFlight = Math.Max(MaxInFlight, inFlight);
          MaxInFlightOnOneHost = Math.Max(MaxInFlightOnOneHost, onHost);
        }

        try
        {
          if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
          return Pages.TryGetValue(url, out var page) ? await page(cancellationToken) : Html(RealPage);
        }
        finally
        {
          Interlocked.Decrement(ref _inFlight);
          InFlightByHost.AddOrUpdate(request.RequestUri.Host, 0, (_, value) => value - 1);
        }
      }
    }
    #endregion

    #region Helpers
    private static readonly string RealPage =
      "<!doctype html><html><head><title>Introduction to Data Analysis | Northwind Academy</title></head><body><h1>Introduction to Data Analysis</h1>"
      + "<p>" + string.Concat(Enumerable.Repeat("Learn to clean, explore and chart data with spreadsheets and simple statistics. ", 20)) + "</p>"
      + "<p>Sign in to save your progress. You must be logged in to post a comment.</p></body></html>";

    private static HttpResponseMessage Html(string body, HttpStatusCode status = HttpStatusCode.OK) =>
      new(status) { Content = new StringContent(body, Encoding.UTF8, "text/html") };

    private static HttpResponseMessage Status(HttpStatusCode status) => Html("<html><body>error</body></html>", status);

    private static HttpResponseMessage Redirect(string location, HttpStatusCode status = HttpStatusCode.MovedPermanently) =>
      new(status) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CourseQuality", "Fixtures", name));

    private static (LinkHealthService Service, FakeNetwork Network, FakeResolver Resolver) Create(Action<CourseQualityLinkCheckOptions>? configure = null)
    {
      var options = new CourseQualityOptions { Enabled = false }; // a tool: works with course quality off
      options.LinkCheck.MinHostIntervalInMilliseconds = 0;
      configure?.Invoke(options.LinkCheck);

      var network = new FakeNetwork();
      var resolver = new FakeResolver();
      var checker = new CourseQualityLinkChecker(NullLogger<CourseQualityLinkChecker>.Instance, Options.Create(options), resolver, network);

      return (new LinkHealthService(NullLogger<LinkHealthService>.Instance, checker), network, resolver);
    }

    private static async Task<LinkHealthResult> CheckOne(string url, Action<FakeNetwork, FakeResolver>? arrange = null, Action<CourseQualityLinkCheckOptions>? configure = null)
    {
      var (service, network, resolver) = Create(configure);
      arrange?.Invoke(network, resolver);

      var report = await service.Check(new LinkHealthRequest { Urls = [url] }, CancellationToken.None);

      var result = Assert.Single(report.Results);
      Assert.Equal(url, result.Url);
      Assert.False(string.IsNullOrWhiteSpace(result.Reason));
      return result;
    }

    private static void Page(FakeNetwork network, string url, Func<HttpResponseMessage> response) =>
      network.Pages[url] = _ => Task.FromResult(response());
    #endregion

    #region Ok and Redirected
    [Fact]
    public async Task RealPage_IsOk_EvenWhenItMentionsSigningIn()
    {
      var result = await CheckOne("https://courses.example.org/data-analysis");

      Assert.Equal(LinkHealthStatus.Ok, result.Status);
      Assert.Equal(200, result.HttpStatus);
      Assert.Equal("https://courses.example.org/data-analysis", result.FinalUrl);
      Assert.NotNull(result.DurationMs);
    }

    [Theory]
    [InlineData("http://courses.example.org/data-analysis", "https://courses.example.org/data-analysis")]
    [InlineData("https://courses.example.org/data-analysis", "https://www.courses.example.org/data-analysis/")]
    [InlineData("https://courses.example.org/data-analysis", "https://courses.example.org/Data-Analysis?utm_source=yoma")]
    public async Task TrivialRedirects_AreOk(string url, string target)
    {
      var result = await CheckOne(url, (network, _) => Page(network, url, () => Redirect(target)));

      Assert.Equal(LinkHealthStatus.Ok, result.Status);
      Assert.Equal(new Uri(target).AbsoluteUri, result.FinalUrl);
    }

    [Theory]
    [InlineData("https://courses.example.org/old-course", "https://learn.example.net/new-course", "another site")]
    [InlineData("https://courses.example.org/old-course", "https://courses.example.org/catalogue/new-course", "same site")]
    [InlineData("https://courses.example.org/old-course", "https://courses.example.org/", "home page")]
    public async Task MaterialRedirects_AreRedirected(string url, string target, string reasonWords)
    {
      var result = await CheckOne(url, (network, _) => Page(network, url, () => Redirect(target)));

      Assert.Equal(LinkHealthStatus.Redirected, result.Status);
      Assert.Equal(target, result.FinalUrl);
      Assert.Contains(reasonWords, result.Reason);
    }
    #endregion

    #region NotFound
    [Theory]
    [InlineData(HttpStatusCode.NotFound, "404")]
    [InlineData(HttpStatusCode.Gone, "410")]
    public async Task NotFoundAndGone_AreNotFound(HttpStatusCode status, string reasonWords)
    {
      const string url = "https://courses.example.org/removed";
      var result = await CheckOne(url, (network, _) => Page(network, url, () => Status(status)));

      Assert.Equal(LinkHealthStatus.NotFound, result.Status);
      Assert.Equal((int)status, result.HttpStatus);
      Assert.Contains(reasonWords, result.Reason);
    }

    [Theory]
    [InlineData("soft-404.html")]
    [InlineData(null)]
    public async Task PageThatSaysItIsGone_IsNotFound(string? fixture)
    {
      const string url = "https://jobs.example.org/vacancy/42";
      var body = fixture != null ? Fixture(fixture)
        : "<html><head><title>Graduate Trainee | Jobs</title></head><body><h1>This vacancy has expired</h1><p>Browse other jobs.</p></body></html>";

      var result = await CheckOne(url, (network, _) => Page(network, url, () => Html(body)));

      Assert.Equal(LinkHealthStatus.NotFound, result.Status);
      Assert.Equal(200, result.HttpStatus);
      Assert.Contains("says it is gone", result.Reason);
    }
    #endregion

    #region Broken
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task OtherErrorStatuses_AreBroken(HttpStatusCode status)
    {
      const string url = "https://courses.example.org/broken";
      var result = await CheckOne(url, (network, _) => Page(network, url, () => Status(status)));

      Assert.Equal(LinkHealthStatus.Broken, result.Status);
      Assert.Equal((int)status, result.HttpStatus);
    }

    [Fact]
    public async Task DomainThatDoesNotExist_IsBroken()
    {
      var result = await CheckOne("https://no-such-site.example/", (_, resolver) =>
        resolver.Hosts["no-such-site.example"] = () => throw new SocketException((int)SocketError.HostNotFound));

      Assert.Equal(LinkHealthStatus.Broken, result.Status);
      Assert.Contains("domain does not exist", result.Reason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConnectionAndTlsFailures_AreBroken(bool tls)
    {
      const string url = "https://courses.example.org/";
      var result = await CheckOne(url, (network, _) => network.Pages[url] = _ =>
        throw new HttpRequestException("failed", tls ? new AuthenticationException("certificate") : new SocketException((int)SocketError.ConnectionRefused)));

      Assert.Equal(LinkHealthStatus.Broken, result.Status);
      Assert.Contains("connect", result.Reason);
    }

    [Fact]
    public async Task EndlessRedirects_AreBroken()
    {
      const string url = "https://courses.example.org/loop";
      var result = await CheckOne(url, (network, _) => Page(network, url, () => Redirect(url, HttpStatusCode.Found)));

      Assert.Equal(LinkHealthStatus.Broken, result.Status);
      Assert.Contains("too many times", result.Reason);
    }

    [Fact]
    public async Task EmptyPage_IsBroken()
    {
      const string url = "https://courses.example.org/empty";
      var result = await CheckOne(url, (network, _) => Page(network, url, () => Html(string.Empty)));

      Assert.Equal(LinkHealthStatus.Broken, result.Status);
    }
    #endregion

    #region Blocked
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task RefusedOrRateLimited_IsBlocked_NotDead(HttpStatusCode status)
    {
      const string url = "https://courses.example.org/members";
      var result = await CheckOne(url, (network, _) => Page(network, url, () => Status(status)));

      Assert.Equal(LinkHealthStatus.Blocked, result.Status);
      Assert.Equal((int)status, result.HttpStatus);
      Assert.Contains("not counted as dead", result.Reason);
    }

    [Fact]
    public async Task BotWallHeader_IsBlocked_WhateverTheStatus()
    {
      const string url = "https://courses.example.org/";
      var result = await CheckOne(url, (network, _) => Page(network, url, () =>
      {
        var response = Status(HttpStatusCode.ServiceUnavailable);
        response.Headers.Add("cf-mitigated", "challenge");
        return response;
      }));

      Assert.Equal(LinkHealthStatus.Blocked, result.Status);
    }

    [Theory]
    [InlineData("bot-challenge.html")]
    [InlineData(null)]
    public async Task BotChallengePage_IsBlocked(string? fixture)
    {
      const string url = "https://courses.example.org/course";
      var body = fixture != null ? Fixture(fixture) : "<html><head><title>Security check</title></head><body><p>Please complete the captcha to continue.</p></body></html>";

      var result = await CheckOne(url, (network, _) => Page(network, url, () => Html(body)));

      Assert.Equal(LinkHealthStatus.Blocked, result.Status);
      Assert.Contains("bot check", result.Reason);
    }

    [Fact]
    public async Task RedirectToSignIn_IsBlocked()
    {
      const string url = "https://courses.example.org/course/7";
      var result = await CheckOne(url, (network, _) =>
      {
        Page(network, url, () => Redirect("https://courses.example.org/login?next=/course/7"));
        Page(network, "https://courses.example.org/login?next=/course/7", () => Html("<html><head><title>Sign in | Academy</title></head><body><form>Email Password</form></body></html>"));
      });

      Assert.Equal(LinkHealthStatus.Blocked, result.Status);
      Assert.Contains("sign-in", result.Reason);
    }
    #endregion

    #region Timeout
    [Fact]
    public async Task SlowSite_TimesOut()
    {
      const string url = "https://slow.example.org/";
      var result = await CheckOne(url, (network, _) => network.Pages[url] = async token =>
      {
        await Task.Delay(Timeout.Infinite, token);
        return Status(HttpStatusCode.OK);
      }, options => options.TimeoutInSeconds = 1);

      Assert.Equal(LinkHealthStatus.Timeout, result.Status);
    }
    #endregion

    #region Invalid and Refused
    [Theory]
    [InlineData("not a link")]
    [InlineData("www.example.org/course")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.org/file")]
    [InlineData("mailto:someone@example.org")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    public async Task NonWebAddresses_AreInvalid_WithoutARequest(string url)
    {
      var (service, network, _) = Create();

      var report = await service.Check(new LinkHealthRequest { Urls = [url] }, CancellationToken.None);

      var result = Assert.Single(report.Results);
      Assert.Equal(LinkHealthStatus.Invalid, result.Status);
      Assert.Null(result.HttpStatus);
      Assert.Empty(network.Requested);
    }

    [Theory]
    [InlineData("http://127.0.0.1/admin", "private")]
    [InlineData("http://10.0.0.8/", "private")]
    [InlineData("http://192.168.1.1/", "private")]
    [InlineData("http://169.254.169.254/latest/meta-data/", "private")]
    [InlineData("http://[::1]/", "private")]
    [InlineData("https://courses.example.org:8443/", "port 8443")]
    [InlineData("https://user:secret@courses.example.org/", "password")]
    public async Task UnsafeTargets_AreRefused_WithoutARequest(string url, string reasonWords)
    {
      var (service, network, _) = Create();

      var report = await service.Check(new LinkHealthRequest { Urls = [url] }, CancellationToken.None);

      var result = Assert.Single(report.Results);
      Assert.Equal(LinkHealthStatus.Refused, result.Status);
      Assert.Contains(reasonWords, result.Reason);
      Assert.Empty(network.Requested);
    }

    [Fact]
    public async Task HostResolvingToAPrivateAddress_IsRefused()
    {
      var (service, network, resolver) = Create();
      resolver.Hosts["intranet.example.org"] = () => [IPAddress.Parse("10.1.2.3")];

      var report = await service.Check(new LinkHealthRequest { Urls = ["https://intranet.example.org/"] }, CancellationToken.None);

      Assert.Equal(LinkHealthStatus.Refused, Assert.Single(report.Results).Status);
      Assert.Empty(network.Requested);
    }

    [Fact]
    public async Task RedirectToAPrivateAddress_IsRefused_AndNotFollowed()
    {
      const string url = "https://courses.example.org/go";
      var (service, network, _) = Create();
      Page(network, url, () => Redirect("http://169.254.169.254/latest/meta-data/"));

      var report = await service.Check(new LinkHealthRequest { Urls = [url] }, CancellationToken.None);

      var result = Assert.Single(report.Results);
      Assert.Equal(LinkHealthStatus.Refused, result.Status);
      Assert.Contains("redirects", result.Reason);
      Assert.Equal([url], network.Requested);
    }
    #endregion

    #region Limits
    [Fact]
    public async Task EmptyLists_AreRejected()
    {
      var (service, _, _) = Create();

      await Assert.ThrowsAsync<ValidationException>(() => service.Check(new LinkHealthRequest(), CancellationToken.None));
      await Assert.ThrowsAsync<ValidationException>(() => service.Check(new LinkHealthRequest { Urls = [] }, CancellationToken.None));
      await Assert.ThrowsAsync<ValidationException>(() => service.Check(new LinkHealthRequest { Urls = ["", "   "] }, CancellationToken.None));
    }

    [Fact]
    public async Task MoreThan200DistinctLinks_AreRejected_BeforeAnyRequest()
    {
      var (service, network, _) = Create();
      var urls = Enumerable.Range(0, LinkHealthService.MaxUrls + 1).Select(i => $"https://courses.example.org/c/{i}").ToList();

      var ex = await Assert.ThrowsAsync<ValidationException>(() => service.Check(new LinkHealthRequest { Urls = urls }, CancellationToken.None));

      Assert.Contains("200", ex.Message);
      Assert.Empty(network.Requested);
    }

    [Fact]
    public async Task Exactly200_WithDuplicatesOnTop_IsAccepted()
    {
      var (service, network, _) = Create();
      var urls = Enumerable.Range(0, LinkHealthService.MaxUrls).Select(i => $"https://host{i % 20}.example.org/c/{i}").ToList();
      urls.AddRange(urls.Take(50));

      var report = await service.Check(new LinkHealthRequest { Urls = urls }, CancellationToken.None);

      Assert.Equal(200, report.Checked);
      Assert.Equal(200, report.Results.Count);
      Assert.Equal(200, network.Requested.Count);
    }

    [Fact]
    public async Task Duplicates_AreCheckedOnce_InFirstSeenOrder()
    {
      var (service, network, _) = Create();

      var report = await service.Check(new LinkHealthRequest
      {
        Urls = ["https://b.example.org/x", " https://a.example.org/y ", "https://B.EXAMPLE.ORG/x", "https://a.example.org:443/y", "https://b.example.org/x"]
      }, CancellationToken.None);

      Assert.Equal(["https://b.example.org/x", "https://a.example.org/y"], report.Results.Select(o => o.Url));
      Assert.Equal(2, report.Checked);
      Assert.Equal(2, network.Requested.Count);
    }
    #endregion

    #region Report
    [Fact]
    public async Task Counts_CoverEveryStatus_AndAddUp()
    {
      var (service, network, _) = Create();
      Page(network, "https://a.example.org/gone", () => Status(HttpStatusCode.NotFound));
      Page(network, "https://b.example.org/down", () => Status(HttpStatusCode.InternalServerError));
      Page(network, "https://c.example.org/members", () => Status(HttpStatusCode.Forbidden));

      var report = await service.Check(new LinkHealthRequest
      {
        Urls = ["https://a.example.org/ok", "https://a.example.org/gone", "https://b.example.org/down", "https://c.example.org/members", "nonsense", "http://127.0.0.1/"],
        Platform = "  YouthHub  "
      }, CancellationToken.None);

      Assert.Equal("YouthHub", report.Platform);
      Assert.Equal(6, report.Checked);
      Assert.Equal(Enum.GetValues<LinkHealthStatus>().Order(), report.Counts.Keys.Order());
      Assert.Equal(report.Checked, report.Counts.Values.Sum());
      Assert.Equal(1, report.Counts[LinkHealthStatus.Ok]);
      Assert.Equal(1, report.Counts[LinkHealthStatus.NotFound]);
      Assert.Equal(1, report.Counts[LinkHealthStatus.Broken]);
      Assert.Equal(1, report.Counts[LinkHealthStatus.Blocked]);
      Assert.Equal(1, report.Counts[LinkHealthStatus.Invalid]);
      Assert.Equal(1, report.Counts[LinkHealthStatus.Refused]);
      Assert.Equal(0, report.Counts[LinkHealthStatus.Timeout]);
      Assert.Equal(0, report.Counts[LinkHealthStatus.Redirected]);
      Assert.True(report.DateChecked > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task BlankPlatform_IsNull()
    {
      var (service, _, _) = Create();

      var report = await service.Check(new LinkHealthRequest { Urls = ["https://a.example.org/"], Platform = " " }, CancellationToken.None);

      Assert.Null(report.Platform);
    }
    #endregion

    #region Scheduling
    [Fact]
    public async Task Hosts_RunInParallel_UpToTheLimit_AndOneHostRunsOneAtATime()
    {
      var (service, network, _) = Create();
      network.Delay = TimeSpan.FromMilliseconds(40);
      var urls = Enumerable.Range(0, 60).Select(i => $"https://host{i % 15}.example.org/c/{i}").ToList();

      await service.Check(new LinkHealthRequest { Urls = urls }, CancellationToken.None);

      Assert.InRange(network.MaxInFlight, 2, LinkHealthService.MaxConcurrentHosts);
      Assert.Equal(1, network.MaxInFlightOnOneHost);
    }

    [Fact]
    public async Task OneHost_IsPaced_ByTheCheckerSetting()
    {
      var (service, _, _) = Create(options => options.MinHostIntervalInMilliseconds = 150);
      var watch = Stopwatch.StartNew();

      await service.Check(new LinkHealthRequest { Urls = ["https://a.example.org/1", "https://a.example.org/2", "https://a.example.org/3"] }, CancellationToken.None);

      Assert.True(watch.ElapsedMilliseconds >= 280, $"took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task CallerCancellation_Throws()
    {
      var (service, network, _) = Create();
      network.Delay = TimeSpan.FromSeconds(30);
      using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.Check(new LinkHealthRequest { Urls = ["https://a.example.org/"] }, cancel.Token));
    }
    #endregion

    #region Classification
    [Theory]
    [InlineData("https://a.example.org/x", "https://a.example.org/x/", false)]
    [InlineData("http://a.example.org/x", "https://www.a.example.org/X", false)]
    [InlineData("https://a.example.org/x?id=1", "https://a.example.org/x?id=2&utm=y", false)]
    [InlineData("https://a.example.org/x", "https://b.example.org/x", true)]
    [InlineData("https://a.example.org/x", "https://a.example.org/y", true)]
    public void MaterialDifference_IgnoresTrivia(string requested, string final, bool expected)
    {
      Assert.Equal(expected, LinkHealthService.MateriallyDifferent(new Uri(requested), new Uri(final)));
    }

    [Fact]
    public void EveryCheckerFailure_HasAStatus()
    {
      var uri = new Uri("https://a.example.org/");
      foreach (var failure in Enum.GetValues<CourseQualityLinkFailure>())
      {
        var check = failure == CourseQualityLinkFailure.None
          ? CourseQualityLinkCheckResult.Reachable(200, uri.AbsoluteUri, "text")
          : CourseQualityLinkCheckResult.Failed(failure, uri.AbsoluteUri, 400);

        var result = LinkHealthService.Classify(uri.AbsoluteUri, uri, check, 5);

        Assert.True(Enum.IsDefined(result.Status));
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
      }
    }
    #endregion

    #region Shared rules
    /// <summary>
    /// The standalone CLI reads its page rules from scripts/link-health/link-health-rules.json. This keeps that file and
    /// the patterns the API runs from drifting apart.
    /// </summary>
    [Fact]
    public void CliRulesFile_MatchesTheApiPatterns()
    {
      var root = new DirectoryInfo(AppContext.BaseDirectory);
      while (root != null && !File.Exists(Path.Combine(root.FullName, "scripts", "link-health", "link-health-rules.json"))) root = root.Parent;
      Assert.SkipWhen(root == null, "The repository's scripts folder is not next to this build");

      var rules = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(root!.FullName, "scripts", "link-health", "link-health-rules.json")));
      var domain = Path.Combine(root.FullName, "src", "api", "src", "domain", "Yoma.Core.Domain", "CourseQuality");
      var api = Patterns(Path.Combine(domain, "Helpers", "CourseQualityPageMatch.cs"))
        .Concat(Patterns(Path.Combine(domain, "Services", "LinkHealthPageRules.cs")))
        .ToDictionary(o => o.Name, o => o.Pattern);

      Assert.Equal(api["SoftNotFoundRegex"], (string?)rules["gone"]!["softNotFound"]);
      Assert.Equal(api["ExpiredRegex"], (string?)rules["gone"]!["expired"]);
      Assert.Equal(api["BotWallRegex"], (string?)rules["botWall"]!["pattern"]);
      Assert.Equal(api["LoginPathRegex"], (string?)rules["signIn"]!["path"]);
      Assert.Equal(api["LoginNameRegex"], (string?)rules["signIn"]!["name"]);
      Assert.Equal(api["LoginWordingRegex"], (string?)rules["signIn"]!["wording"]);
      Assert.Equal(LinkHealthPageRules.LeadTextLength, (int)rules["leadTextLength"]!);
      Assert.Equal(LinkHealthPageRules.ShortPageLength, (int)rules["shortPageLength"]!);
    }

    private static IEnumerable<(string Name, string Pattern)> Patterns(string file) =>
      System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file),
        @"\[GeneratedRegex\(@""((?:[^""]|"""")*)"".*?\)\]\s*private static partial Regex (\w+)\(\);", System.Text.RegularExpressions.RegexOptions.Singleline)
      .Select(o => (o.Groups[2].Value, o.Groups[1].Value.Replace("\"\"", "\"")));
    #endregion

    #region Controller
    [Fact]
    public void Endpoint_IsAdminOnly_PostLinksCheck()
    {
      var action = typeof(CourseQualityController).GetMethod(nameof(CourseQualityController.CheckLinks))!;

      var route = action.GetCustomAttribute<HttpMethodAttribute>()!;
      Assert.Equal("POST", route.HttpMethods.Single());
      Assert.Equal("links/check", route.Template);
      Assert.Equal("api/v3/courseQuality", typeof(CourseQualityController).GetCustomAttribute<RouteAttribute>()!.Template);

      Assert.Equal("Admin", action.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
      Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
      Assert.NotNull(typeof(CourseQualityController).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void Service_DependsOnlyOnTheSafeChecker_NotOnTheCourseQualitySwitch()
    {
      var parameters = typeof(LinkHealthService).GetConstructors().Single().GetParameters().Select(o => o.ParameterType).ToList();

      Assert.Contains(typeof(ICourseQualityLinkChecker), parameters);
      Assert.DoesNotContain(parameters, o => o.IsGenericType && o.GetGenericTypeDefinition() == typeof(IOptions<>));
    }
    #endregion
  }
}
