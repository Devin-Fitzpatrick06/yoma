using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Infrastructure.Anthropic;
using Yoma.Core.Infrastructure.Anthropic.Client;
using Yoma.Core.Infrastructure.Anthropic.Interfaces;
using Yoma.Core.Infrastructure.Anthropic.Models;
using Yoma.Core.Infrastructure.Anthropic.Services;
using Yoma.Core.Infrastructure.OpenAICompatible;
using Yoma.Core.Infrastructure.OpenAICompatible.Client;
using Yoma.Core.Infrastructure.OpenAICompatible.Interfaces;
using Yoma.Core.Infrastructure.OpenAICompatible.Services;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  /// <summary>
  /// Iteration 5: the provider switch, the OpenAI-compatible provider and usage + cost on every AI call. No paid API is
  /// called: Anthropic through a fake seam, OpenAI-compatible through a fake HTTP handler. One opt-in test runs a real
  /// local model when ZIQI_LOCAL_AI_URL is set.
  /// </summary>
  public class CourseQualityAIProviderTests
  {
    #region Fakes
    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
      public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

      public static FakeHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
        Requests.Add((request, await request.Content!.ReadAsStringAsync(cancellationToken)));
        return await respond(request, cancellationToken);
      }
    }

    private sealed class RecordingClient(IOpenAICompatibleChatClient inner) : IOpenAICompatibleChatClient
    {
      public Infrastructure.OpenAICompatible.Models.OpenAICompatibleCompletion? Last { get; private set; }

      public async Task<Infrastructure.OpenAICompatible.Models.OpenAICompatibleCompletion> Create(Infrastructure.OpenAICompatible.Models.OpenAICompatibleChatRequest request, CancellationToken cancellationToken)
      {
        Last = await inner.Create(request, cancellationToken);
        return Last;
      }
    }

    private sealed class FakeAnthropicClient(AnthropicCompletion completion) : IAnthropicMessageClient
    {
      public Task<AnthropicCompletion> Create(AnthropicCompletionRequest request, CancellationToken cancellationToken) => Task.FromResult(completion);
    }
    #endregion

    #region Helpers
    private const string BaseUrl = "http://localhost:11434/v1";
    private const string Model = "qwen2.5:3b";

    private static readonly string LongText = string.Concat(Enumerable.Repeat("About this course: practical units with exercises and quizzes every week. ", 10));

    private const string DatedPage = "About this course: Starts 12 November 2026. Ends 24 December 2026. Fee: $49 ";

    private static CourseQualityAIRequest Request(string text)
    {
      const string heading = "Introduction to Data Analysis";
      return new CourseQualityAIRequest
      {
        Title = heading,
        Description = "Learn data.",
        URL = "https://courses.example.org/data",
        DateStart = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero),
        DateEnd = new DateTimeOffset(2026, 12, 24, 0, 0, 0, TimeSpan.Zero),
        SourcePageText = $"{heading} {text}",
        SourcePageSignals = new CourseQualityPageSignals { Title = heading, Heading = heading },
        SourcePageFinalUrl = "https://courses.example.org/data"
      };
    }

    private static string Reply(string confidence = "0.9", string score = "95", string format = "Course") => $$"""
      {"confidence":{{confidence}},"summary":"The page matches.","format":"{{format}}",
       "cost":{"type":"Paid","amount":49,"currency":"USD","evidence":"Fee: $49"},
       "criteria":[{"criterion":"DatesAccurate","score":{{score}},"reason":"The dates agree.","recommendation":null},
         {"criterion":"LinkWorks","score":{{score}},"reason":"It loads.","recommendation":null},
         {"criterion":"DataComplete","score":{{score}},"reason":"It matches.","recommendation":null},
         {"criterion":"FormatIdentified","score":{{score}},"reason":"A course.","recommendation":null}]}
      """;

    private static string Completion(string? content, string finishReason = "stop", int promptTokens = 812, int completionTokens = 143, string? refusal = null)
    {
      var message = new JObject { ["role"] = "assistant", ["content"] = content };
      if (refusal != null) message["refusal"] = refusal;

      return new JObject
      {
        ["id"] = "chatcmpl-1",
        ["object"] = "chat.completion",
        ["model"] = Model,
        ["choices"] = new JArray { new JObject { ["index"] = 0, ["message"] = message, ["finish_reason"] = finishReason } },
        ["usage"] = new JObject { ["prompt_tokens"] = promptTokens, ["completion_tokens"] = completionTokens, ["total_tokens"] = promptTokens + completionTokens }
      }.ToString(Formatting.None);
    }

    private static CourseQualityAIOptions AIOptions(Action<CourseQualityOpenAICompatibleOptions>? configure = null)
    {
      var options = new CourseQualityAIOptions { Provider = AIProviderKind.OpenAICompatible };
      options.OpenAICompatible.BaseUrl = BaseUrl;
      options.OpenAICompatible.Model = Model;
      configure?.Invoke(options.OpenAICompatible);
      return options;
    }

    private static OpenAICompatibleCourseQualityProvider OpenAIProvider(HttpMessageHandler handler, CourseQualityAIOptions? options = null)
    {
      var ai = Options.Create(options ?? AIOptions());
      return new OpenAICompatibleCourseQualityProvider(NullLogger<OpenAICompatibleCourseQualityProvider>.Instance, ai, Options.Create(new CourseQualityOptions()),
        new OpenAICompatibleChatClient(ai, handler));
    }

    private static Task<CourseQualityAICall> CallOpenAI(HttpMessageHandler handler, CourseQualityAIRequest? request = null, CourseQualityAIOptions? options = null) =>
      OpenAIProvider(handler, options).Call(request ?? Request(DatedPage + LongText), CancellationToken.None);

    private static (IServiceCollection Services, IConfiguration Configuration) Host(Dictionary<string, string?> values)
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

      var services = new ServiceCollection();
      services.AddLogging();
      services.Configure<CourseQualityOptions>(o => o.SourcePageReaderEnabled = true);
      services.Configure<CourseQualityAIOptions>(o => configuration.GetSection(CourseQualityAIOptions.Section).Bind(o));
      services.ConfigureServices_CourseQualityAIProvider(configuration);
      services.AddSingleton(CourseQualityAIEndpoint.None);
      services.AddScoped<ICourseQualityAIProvider, CourseQualitySourcePageReader>();
      services.ConfigureServices_InfrastructureCourseQualityAIProvider(configuration);
      services.ConfigureServices_InfrastructureCourseQualityOpenAICompatibleProvider(configuration);

      return (services, configuration);
    }

    private static ICourseQualityAIProvider Selected(IServiceCollection services, out CourseQualityAIEndpoint endpoint)
    {
      var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
      var scope = provider.CreateScope();
      endpoint = scope.ServiceProvider.GetRequiredService<CourseQualityAIEndpoint>();
      return scope.ServiceProvider.GetRequiredService<ICourseQualityAIProvider>();
    }

    private static string FindAppSettings()
    {
      for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
      {
        var path = Path.Combine(dir.FullName, "src", "application", "Yoma.Core.Api", "appsettings.json");
        if (File.Exists(path)) return path;
        path = Path.Combine(dir.FullName, "application", "Yoma.Core.Api", "appsettings.json");
        if (File.Exists(path)) return path;
      }

      throw new FileNotFoundException("API appsettings.json not found");
    }
    #endregion

    #region Provider selection
    [Fact]
    public void None_RunsTheReaderOnly_EvenWithAKey()
    {
      var (services, _) = Host(new() { ["AI:Provider"] = "None", ["Anthropic:ApiKey"] = "test-key", ["AI:OpenAICompatible:BaseUrl"] = BaseUrl, ["AI:OpenAICompatible:Model"] = Model });

      Assert.DoesNotContain(services, o => o.ServiceType == typeof(IAnthropicMessageClient));
      Assert.DoesNotContain(services, o => o.ServiceType == typeof(IOpenAICompatibleChatClient));
      Assert.IsType<CourseQualitySourcePageReader>(Selected(services, out var endpoint));
      Assert.Equal(AIProviderKind.None, endpoint.Provider);
      Assert.False(endpoint.Configured);
    }

    [Fact]
    public void Default_IsAnthropic_AndKeylessWithoutAKey()
    {
      var (services, _) = Host([]);

      Assert.IsType<CourseQualitySourcePageReader>(Selected(services, out var endpoint));
      Assert.Equal(new CourseQualityAIEndpoint(AIProviderKind.Anthropic, "claude-opus-5-5", null, false, false), endpoint);
    }

    [Fact]
    public void Anthropic_WithAKey_IsTheProvider_WithTheReaderBehindIt()
    {
      var (services, _) = Host(new() { ["Anthropic:ApiKey"] = "test-key" });

      Assert.IsType<CourseQualityProviderFallback>(Selected(services, out var endpoint));
      Assert.True(endpoint.Configured);
      Assert.DoesNotContain(services, o => o.ServiceType == typeof(IOpenAICompatibleChatClient));
    }

    [Fact]
    public void OpenAICompatible_IsTheProvider_AndAnAnthropicKeyIsIgnored()
    {
      var (services, _) = Host(new() { ["AI:Provider"] = "OpenAICompatible", ["Anthropic:ApiKey"] = "test-key", ["AI:OpenAICompatible:BaseUrl"] = BaseUrl, ["AI:OpenAICompatible:Model"] = Model });

      Assert.DoesNotContain(services, o => o.ServiceType == typeof(IAnthropicMessageClient));
      Assert.IsType<CourseQualityProviderFallback>(Selected(services, out var endpoint));
      Assert.Equal(new CourseQualityAIEndpoint(AIProviderKind.OpenAICompatible, Model, "localhost:11434", true, true), endpoint);

      using var provider = services.BuildServiceProvider();
      using var scope = provider.CreateScope();
      Assert.True(scope.ServiceProvider.GetRequiredService<OpenAICompatibleCourseQualityProvider>().IsEnabled);
    }

    [Theory]
    [InlineData(null, Model)]
    [InlineData("", Model)]
    [InlineData("not a url", Model)]
    [InlineData("ftp://localhost/v1", Model)]
    [InlineData(BaseUrl, null)]
    [InlineData(BaseUrl, " ")]
    public void OpenAICompatible_WithoutABaseUrlAndModel_LeavesTheReader(string? baseUrl, string? model)
    {
      var (services, _) = Host(new() { ["AI:Provider"] = "OpenAICompatible", ["AI:OpenAICompatible:BaseUrl"] = baseUrl, ["AI:OpenAICompatible:Model"] = model });

      Assert.DoesNotContain(services, o => o.ServiceType == typeof(IOpenAICompatibleChatClient));
      Assert.IsType<CourseQualitySourcePageReader>(Selected(services, out var endpoint));
      Assert.Equal(AIProviderKind.OpenAICompatible, endpoint.Provider);
      Assert.False(endpoint.Configured);
    }

    [Fact]
    public void AppSettings_DefaultToAnthropic_WithNoOpenAICompatibleServerAndNoPrices()
    {
      var options = new ConfigurationBuilder().AddJsonFile(FindAppSettings()).Build().GetSection(CourseQualityAIOptions.Section).Get<CourseQualityAIOptions>()!;

      Assert.Equal(AIProviderKind.Anthropic, options.Provider);
      Assert.False(options.OpenAICompatible.IsConfigured);
      Assert.False(options.OpenAICompatible.HasApiKey);
      Assert.True(options.OpenAICompatible.IsLocal);
      Assert.Equal(300, options.OpenAICompatible.TimeoutInSeconds);
      Assert.Equal(1024, options.OpenAICompatible.MaxTokens);
      Assert.Equal(6000, options.OpenAICompatible.MaxPageChars);
      Assert.Empty(options.Prices);
    }

    [Theory]
    [InlineData("None", null)]
    [InlineData("Anthropic", null)]
    [InlineData("OpenAICompatible", typeof(OpenAICompatibleCourseQualityProvider))]
    public void ApiStartup_FollowsAIProvider(string kind, Type? registered)
    {
      var values = new Dictionary<string, string?> { ["AI:Provider"] = kind, ["AI:OpenAICompatible:BaseUrl"] = BaseUrl, ["AI:OpenAICompatible:Model"] = Model };
      var configuration = new ConfigurationBuilder().AddJsonFile(FindAppSettings()).AddInMemoryCollection(values).Build();
      var environment = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
      environment.SetupGet(o => o.EnvironmentName).Returns("Development");
      var services = new ServiceCollection();

      try
      {
        new Api.Startup(configuration, environment.Object).ConfigureServices(services);
      }
      catch (StackExchange.Redis.RedisConnectionException)
      {
        // Redis comes last, after every provider is registered; a test machine has none
      }

      var last = services.Last(o => o.ServiceType == typeof(ICourseQualityAIProvider));
      Assert.Equal(registered == null ? typeof(CourseQualitySourcePageReader) : null, last.ImplementationType);
      Assert.Equal(registered != null, services.Any(o => o.ServiceType == registered && registered != null));
      Assert.Contains(services, o => o.ServiceType == typeof(ICourseQualityAIUsageService));

      var endpoint = (CourseQualityAIEndpoint)services.Last(o => o.ServiceType == typeof(CourseQualityAIEndpoint)).ImplementationInstance!;
      Assert.Equal(kind, endpoint.Provider.ToString());
    }
    #endregion

    #region OpenAI-compatible request shape
    [Fact]
    public async Task Request_IsAStrictStructuredChatCompletion_AtTemperatureZero()
    {
      var handler = FakeHandler.Json(Completion(Reply()));

      await CallOpenAI(handler);

      var (request, body) = handler.Requests.Single();
      Assert.Equal(HttpMethod.Post, request.Method);
      Assert.Equal("http://localhost:11434/v1/chat/completions", request.RequestUri!.ToString());
      Assert.Null(request.Headers.Authorization);

      using var json = JsonDocument.Parse(body);
      var root = json.RootElement;
      Assert.Equal(Model, root.GetProperty("model").GetString());
      Assert.Equal(0, root.GetProperty("temperature").GetInt32());
      Assert.Equal(1024, root.GetProperty("max_tokens").GetInt32());
      Assert.False(root.GetProperty("stream").GetBoolean());

      var format = root.GetProperty("response_format");
      Assert.Equal("json_schema", format.GetProperty("type").GetString());
      Assert.Equal("course_check", format.GetProperty("json_schema").GetProperty("name").GetString());
      Assert.True(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
      Assert.False(format.GetProperty("json_schema").GetProperty("schema").GetProperty("additionalProperties").GetBoolean());

      var messages = root.GetProperty("messages");
      Assert.Equal(2, messages.GetArrayLength());
      Assert.Equal("system", messages[0].GetProperty("role").GetString());
      Assert.Equal(Domain.CourseQuality.Helpers.CourseQualityAIPrompt.SystemPrompt, messages[0].GetProperty("content").GetString());
      Assert.Equal("user", messages[1].GetProperty("role").GetString());
      Assert.Contains("<page_text_", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Request_SendsAKey_OnlyWhenOneIsConfigured()
    {
      var handler = FakeHandler.Json(Completion(Reply()));

      await CallOpenAI(handler, options: AIOptions(o => o.ApiKey = "local-key"));

      Assert.Equal("Bearer local-key", handler.Requests.Single().Request.Headers.Authorization!.ToString());
    }

    [Theory]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://localhost:11434/v1/")]
    public async Task Request_KeepsTheBaseUrlPath(string baseUrl)
    {
      var handler = FakeHandler.Json(Completion(Reply()));

      await CallOpenAI(handler, options: AIOptions(o => o.BaseUrl = baseUrl));

      Assert.Equal("http://localhost:11434/v1/chat/completions", handler.Requests.Single().Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task PageText_IsCutToMaxPageChars_InsideItsDelimiters()
    {
      var handler = FakeHandler.Json(Completion(Reply()));
      var page = DatedPage + LongText + new string('x', 5000) + "TAIL-MARKER";

      await CallOpenAI(handler, Request(page), AIOptions(o => o.MaxPageChars = 300));

      using var json = JsonDocument.Parse(handler.Requests.Single().Body);
      var user = json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
      Assert.DoesNotContain("TAIL-MARKER", user);
      Assert.Contains("Starts 12 November 2026", user);

      var open = user.IndexOf("<page_text_", StringComparison.Ordinal);
      var name = user[(open + 1)..user.IndexOf('>', open)];
      // the first mention is in the opening sentence; the section starts on its own line
      var start = user.IndexOf($"\n<{name}>", StringComparison.Ordinal) + name.Length + 3;
      var section = user[start..user.IndexOf($"</{name}>", StringComparison.Ordinal)];
      Assert.True(section.Trim().Length <= 300);
    }

    [Fact]
    public async Task NoPageText_CallsNoModel_AndRecordsNoUsage()
    {
      var handler = FakeHandler.Json(Completion(Reply()));
      var request = Request(string.Empty);
      request.SourcePageText = null;

      var call = await CallOpenAI(handler, request);

      Assert.Null(call.Result);
      Assert.Null(call.Usage);
      Assert.Empty(handler.Requests);
    }
    #endregion

    #region OpenAI-compatible outcomes
    [Fact]
    public async Task Answered_MapsTheReply_WithUsageAndAZeroLocalCost()
    {
      var call = await CallOpenAI(FakeHandler.Json(Completion(Reply())));

      Assert.NotNull(call.Result);
      Assert.True(call.Result.IsAI);
      Assert.Equal(Model, call.Result.Model);
      Assert.Same(call.Usage, call.Result.Usage);

      var usage = call.Usage!;
      Assert.Equal(AIProviderKind.OpenAICompatible, usage.Provider);
      Assert.Equal(Model, usage.Model);
      Assert.Equal("localhost:11434", usage.EndpointHost);
      Assert.True(usage.IsLocal);
      Assert.Equal(812, usage.InputTokens);
      Assert.Equal(143, usage.OutputTokens);
      Assert.Equal(0m, usage.CostUsd);
      Assert.True(usage.DurationMs >= 0);
      Assert.True(usage.DateRun > DateTimeOffset.UtcNow.AddMinutes(-1));
      Assert.Equal(CourseQualityAIOutcome.Answered, usage.Outcome);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("content_filter")]
    [InlineData("tool_calls")]
    [InlineData("")]
    public async Task AnyFinishButStop_IsDeclined(string finishReason)
    {
      var call = await CallOpenAI(FakeHandler.Json(Completion(Reply(), finishReason)));

      Assert.Null(call.Result);
      Assert.Equal(CourseQualityAIOutcome.Declined, call.Usage!.Outcome);
      Assert.Equal(812, call.Usage.InputTokens);
    }

    [Fact]
    public async Task ARefusal_IsDeclined_EvenWithStop()
    {
      var call = await CallOpenAI(FakeHandler.Json(Completion(null, refusal: "I can't help with that.")));

      Assert.Null(call.Result);
      Assert.Equal(CourseQualityAIOutcome.Declined, call.Usage!.Outcome);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"confidence":0.9}""")]
    [InlineData(null)]
    public async Task AReplyOutsideTheSchema_IsInvalid(string? content)
    {
      var call = await CallOpenAI(FakeHandler.Json(Completion(content)));

      Assert.Null(call.Result);
      Assert.Equal(CourseQualityAIOutcome.Invalid, call.Usage!.Outcome);
      Assert.Equal(143, call.Usage.OutputTokens);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.OK, "<html>proxy error</html>")]
    [InlineData(HttpStatusCode.OK, """{"choices":[]}""")]
    [InlineData(HttpStatusCode.Found, "")]
    public async Task AnHttpOrEnvelopeFailure_IsFailed(HttpStatusCode status, string body)
    {
      var call = await CallOpenAI(FakeHandler.Json(body, status));

      Assert.Null(call.Result);
      Assert.Equal(CourseQualityAIOutcome.Failed, call.Usage!.Outcome);
      Assert.Equal(0, call.Usage.InputTokens);
      Assert.Equal(0m, call.Usage.CostUsd);
    }

    [Fact]
    public async Task AConnectionFailure_IsFailed()
    {
      var call = await CallOpenAI(new FakeHandler((_, _) => throw new HttpRequestException("connection refused")));

      Assert.Equal(CourseQualityAIOutcome.Failed, call.Usage!.Outcome);
    }

    [Fact]
    public async Task ATimeout_IsFailed_ButRealCancellationPropagates()
    {
      var slow = new FakeHandler(async (_, token) =>
      {
        await Task.Delay(Timeout.Infinite, token);
        return new HttpResponseMessage(HttpStatusCode.OK);
      });
      var options = AIOptions(o => o.TimeoutInSeconds = 1);

      var call = await CallOpenAI(slow, options: options);

      Assert.Null(call.Result);
      Assert.Equal(CourseQualityAIOutcome.Failed, call.Usage!.Outcome);
      Assert.True(call.Usage.DurationMs >= 900);

      using var cancelled = new CancellationTokenSource();
      await cancelled.CancelAsync();
      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => OpenAIProvider(slow, options).Call(Request(DatedPage + LongText), cancelled.Token));
    }

    [Theory]
    [InlineData("length")]
    [InlineData("invalid")]
    [InlineData("failed")]
    public async Task Fallback_RunsTheReader_AndKeepsTheUsageOfTheCallThatGaveNoAnswer(string kind)
    {
      var handler = kind switch
      {
        "length" => FakeHandler.Json(Completion(Reply(), "length")),
        "invalid" => FakeHandler.Json(Completion("not json")),
        _ => FakeHandler.Json("{}", HttpStatusCode.BadGateway)
      };
      var reader = new CourseQualitySourcePageReader(Options.Create(new CourseQualityOptions { SourcePageReaderEnabled = true }));

      var result = await new CourseQualityProviderFallback(OpenAIProvider(handler), reader).Evaluate(Request(DatedPage + LongText), CancellationToken.None);

      Assert.NotNull(result);
      Assert.False(result.IsAI);
      Assert.Equal(CourseQualitySourcePageReader.ModelName, result.Model);
      Assert.NotNull(result.Usage);
      Assert.NotEqual(CourseQualityAIOutcome.Answered, result.Usage.Outcome);
    }
    #endregion

    #region The ceiling holds for the new provider
    [Fact]
    public async Task OpenAICompatible_CannotConfirmDates_ThatFixedChecksCannotFind()
    {
      var call = await CallOpenAI(FakeHandler.Json(Completion(Reply(confidence: "1", score: "100"))), Request(LongText));

      Assert.NotNull(call.Result);
      Assert.False(call.Result.DatesStartConfirmed);
      Assert.False(call.Result.DatesEndConfirmed);
      Assert.True(call.Result.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score < 100);
      Assert.True(call.Result.Confidence <= 0.7m);
    }

    [Fact]
    public async Task OpenAICompatible_CannotRaiseAnyCriterion_AboveTheReader()
    {
      var request = Request(DatedPage + LongText);
      var ceiling = (await new CourseQualitySourcePageReader(Options.Create(new CourseQualityOptions())).Evaluate(request, CancellationToken.None))!;

      var call = await CallOpenAI(FakeHandler.Json(Completion(Reply(confidence: "1", score: "100"))), request);

      Assert.NotNull(call.Result);
      Assert.True(call.Result.Confidence <= ceiling.Confidence);
      foreach (var limit in ceiling.Criteria)
        Assert.True(call.Result.Criteria.Single(o => o.Criterion == limit.Criterion).Score <= limit.Score);
    }

    [Fact]
    public async Task OpenAICompatible_LowerScore_IsRespected()
    {
      var call = await CallOpenAI(FakeHandler.Json(Completion(Reply(score: "30"))));

      Assert.Equal(30, call.Result!.Criteria.Single(o => o.Criterion == CourseQualityCriterion.DatesAccurate).Score);
      Assert.False(call.Result.DatesStartConfirmed);
    }

    [Fact]
    public async Task OpenAICompatible_FormatThatDiffersFromTheReader_IsUnknownAndDisputed()
    {
      var call = await CallOpenAI(FakeHandler.Json(Completion(Reply(format: "Webinar"))));

      Assert.Equal(CourseFormat.Unknown, call.Result!.Format);
      Assert.True(call.Result.FormatDisputed);
    }
    #endregion

    #region Usage and cost maths
    [Fact]
    public void Cost_IsZero_OnALocalHost_WhateverThePrice()
    {
      var options = new CourseQualityAIOptions { Prices = { ["m"] = new CourseQualityAIPrice { InputPerMillionUsd = 5, OutputPerMillionUsd = 15 } } };

      Assert.Equal(0m, options.Cost("m", true, 1_000_000, 1_000_000));
    }

    [Fact]
    public void Cost_IsPriceTimesTokens_ForAKnownRemoteModel()
    {
      var options = new CourseQualityAIOptions { Prices = { ["gpt-x"] = new CourseQualityAIPrice { InputPerMillionUsd = 2.5m, OutputPerMillionUsd = 10m } } };

      // 1200 × 2.5 / 1e6 + 300 × 10 / 1e6 = 0.003 + 0.003
      Assert.Equal(0.006m, options.Cost("gpt-x", false, 1200, 300));
      Assert.Equal(0.006m, options.Cost("GPT-X", false, 1200, 300));
    }

    [Fact]
    public void Cost_IsNull_ForARemoteModelWithNoPrice()
    {
      Assert.Null(new CourseQualityAIOptions().Cost("gpt-x", false, 1200, 300));
      Assert.Null(new CourseQualityAIOptions().Cost(null, false, 1200, 300));
    }

    [Fact]
    public void Cost_FindsAModelWithAColon_ByItsUnderscoreKey()
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["AI:Prices:qwen2.5_3b:InputPerMillionUsd"] = "1",
        ["AI:Prices:qwen2.5_3b:OutputPerMillionUsd"] = "2"
      }).Build();
      var options = configuration.GetSection(CourseQualityAIOptions.Section).Get<CourseQualityAIOptions>()!;

      Assert.Equal(0.000003m, options.Cost("qwen2.5:3b", false, 1, 1));
    }

    [Fact]
    public async Task RemoteOpenAICompatible_UsesTheConfiguredPrice_OrNull()
    {
      var priced = AIOptions(o => { o.IsLocal = false; o.BaseUrl = "https://models.example.gov:8443/v1"; });
      priced.Prices[Model.Replace(':', '_')] = new CourseQualityAIPrice { InputPerMillionUsd = 1m, OutputPerMillionUsd = 4m };
      var unpriced = AIOptions(o => o.IsLocal = false);

      var withPrice = (await CallOpenAI(FakeHandler.Json(Completion(Reply(), promptTokens: 1000, completionTokens: 250)), options: priced)).Usage!;
      var withoutPrice = (await CallOpenAI(FakeHandler.Json(Completion(Reply())), options: unpriced)).Usage!;

      Assert.Equal("models.example.gov:8443", withPrice.EndpointHost);
      Assert.False(withPrice.IsLocal);
      Assert.Equal(0.002m, withPrice.CostUsd);
      Assert.Null(withoutPrice.CostUsd);
    }

    [Fact]
    public async Task Anthropic_RecordsUsage_FromTheResponse_WithNoHostAndAPriceOnlyWhenConfigured()
    {
      static AnthropicCourseQualityProvider Provider(CourseQualityAIOptions ai) => new(NullLogger<AnthropicCourseQualityProvider>.Instance,
        Options.Create(new AnthropicOptions { ApiKey = "test-key" }), Options.Create(new CourseQualityOptions()),
        new FakeAnthropicClient(new AnthropicCompletion("end_turn", Reply(), 2000, 500)), Options.Create(ai));

      var priced = new CourseQualityAIOptions { Prices = { ["claude-opus-5-5"] = new CourseQualityAIPrice { InputPerMillionUsd = 3m, OutputPerMillionUsd = 15m } } };

      var unknown = (await Provider(new CourseQualityAIOptions()).Call(Request(DatedPage + LongText), CancellationToken.None)).Usage!;
      var known = (await Provider(priced).Call(Request(DatedPage + LongText), CancellationToken.None)).Usage!;

      Assert.Equal(AIProviderKind.Anthropic, unknown.Provider);
      Assert.Equal("claude-opus-5-5", unknown.Model);
      Assert.Null(unknown.EndpointHost);
      Assert.False(unknown.IsLocal);
      Assert.Equal(2000, unknown.InputTokens);
      Assert.Equal(500, unknown.OutputTokens);
      Assert.Null(unknown.CostUsd);
      Assert.Equal(CourseQualityAIOutcome.Answered, unknown.Outcome);
      Assert.Equal(0.0135m, known.CostUsd);
    }

    [Fact]
    public async Task Anthropic_DeclinedAndFailedCalls_AreMetered()
    {
      var declined = await new AnthropicCourseQualityProvider(NullLogger<AnthropicCourseQualityProvider>.Instance, Options.Create(new AnthropicOptions { ApiKey = "k" }),
        Options.Create(new CourseQualityOptions()), new FakeAnthropicClient(new AnthropicCompletion("max_tokens", null, 100, 16000))).Call(Request(DatedPage + LongText), CancellationToken.None);

      var failing = new Mock<IAnthropicMessageClient>();
      failing.Setup(o => o.Create(It.IsAny<AnthropicCompletionRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));
      var failed = await new AnthropicCourseQualityProvider(NullLogger<AnthropicCourseQualityProvider>.Instance, Options.Create(new AnthropicOptions { ApiKey = "k" }),
        Options.Create(new CourseQualityOptions()), failing.Object).Call(Request(DatedPage + LongText), CancellationToken.None);

      Assert.Equal(CourseQualityAIOutcome.Declined, declined.Usage!.Outcome);
      Assert.Equal(16000, declined.Usage.OutputTokens);
      Assert.Equal(CourseQualityAIOutcome.Failed, failed.Usage!.Outcome);
    }

    [Fact]
    public async Task AnthropicSdk_ReadsTheUsageTokens()
    {
      const string response = """
        {"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","stop_reason":"end_turn","stop_sequence":null,
         "content":[{"type":"text","text":"{}"}],"usage":{"input_tokens":1234,"output_tokens":56}}
        """;
      using var handler = FakeHandler.Json(response);
      using var http = new HttpClient(handler);
      using var client = new AnthropicMessageClient(Options.Create(new AnthropicOptions { ApiKey = "test-key" }), http);

      var completion = await client.Create(new AnthropicCompletionRequest("s", "u", """{"type":"object","additionalProperties":false}"""), CancellationToken.None);

      Assert.Equal(1234, completion.InputTokens);
      Assert.Equal(56, completion.OutputTokens);
    }
    #endregion

    #region Storage and the API shape
    private static CourseQualityAIUsage Usage(CourseQualityAIOutcome outcome = CourseQualityAIOutcome.Answered, decimal? cost = 0m, long duration = 41000, DateTimeOffset? dateRun = null) => new()
    {
      Provider = AIProviderKind.OpenAICompatible,
      Model = Model,
      EndpointHost = "localhost:11434",
      IsLocal = cost == 0m,
      InputTokens = 1312,
      OutputTokens = 201,
      CostUsd = cost,
      DurationMs = duration,
      DateRun = dateRun ?? Now,
      Outcome = outcome
    };

    [Fact]
    public void Engine_CarriesTheUsage_OntoTheAssessment()
    {
      var ai = Ai();
      ai.Usage = Usage();

      var assessment = CourseQualityEngine.Assess(CleanListing(), WorkingLink(), ai, new CourseQualityOptions(), Now);

      Assert.Same(ai.Usage, assessment.AIUsage);
      Assert.Null(CourseQualityEngine.Assess(CleanListing(), WorkingLink(), Ai(), new CourseQualityOptions(), Now).AIUsage);
    }

    [Fact]
    public void AIUsage_RoundTripsThroughTheStoredRecord()
    {
      var ai = Ai();
      ai.Usage = Usage(cost: null);
      var assessment = CourseQualityEngine.Assess(CleanListing(), WorkingLink(), ai, new CourseQualityOptions(), Now);

      var record = assessment.ToRecord(Now, new string('a', 64));
      var stored = JObject.Parse(record.AIUsage!);
      var back = record.ToAssessment().AIUsage!;

      Assert.Equal("OpenAICompatible", stored["Provider"]!.Value<string>());
      Assert.Equal("Answered", stored["Outcome"]!.Value<string>());
      Assert.Equal(JTokenType.Null, stored["CostUsd"]!.Type);
      Assert.Equal(JsonConvert.SerializeObject(assessment.AIUsage), JsonConvert.SerializeObject(back));
    }

    [Fact]
    public void NoAIUsage_IsStoredAsNull_NotAsAnEmptyValue()
    {
      var record = CourseQualityEngine.Assess(CleanListing(), WorkingLink(), Ai(), new CourseQualityOptions(), Now).ToRecord(Now, new string('a', 64));

      Assert.Null(record.AIUsage);
      Assert.Null(record.ToAssessment().AIUsage);
    }

    [Fact]
    public void AnUnreadableStoredUsage_ReadsAsNull()
    {
      Assert.Null(CourseQualityAssessmentExtensions.ParseAIUsage("{not json"));
      Assert.Null(CourseQualityAssessmentExtensions.ParseAIUsage(" "));
    }

    [Fact]
    public void AIUsage_SerializesLikeTheWebModel()
    {
      var settings = new JsonSerializerSettings
      {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
        Converters = [new StringEnumConverter()]
      };

      var usage = JObject.Parse(JsonConvert.SerializeObject(Usage(cost: null), settings));
      Assert.Equal(
        new[] { "provider", "model", "endpointHost", "isLocal", "inputTokens", "outputTokens", "costUsd", "durationMs", "dateRun", "outcome" }.Order(),
        usage.Properties().Select(o => o.Name).Order());
      Assert.Equal("OpenAICompatible", usage["provider"]!.Value<string>());
      Assert.Equal("Answered", usage["outcome"]!.Value<string>());
      Assert.Equal(JTokenType.Null, usage["costUsd"]!.Type);

      var summary = JObject.Parse(JsonConvert.SerializeObject(new CourseQualityAIUsageSummary { Recent = [new CourseQualityAIRun { OpportunityTitle = "t", Usage = Usage() }] }, settings));
      Assert.Equal(
        new[] { "provider", "model", "endpointHost", "isLocal", "configured", "runs", "answered", "inputTokens", "outputTokens", "costUsd", "averageDurationMs", "recent" }.Order(),
        summary.Properties().Select(o => o.Name).Order());
      Assert.Equal(new[] { "assessmentId", "opportunityId", "organizationId", "opportunityTitle", "usage" }.Order(), ((JObject)summary["recent"]![0]!).Properties().Select(o => o.Name).Order());
    }
    #endregion

    #region Usage summary
    private static CourseQualityAssessmentRecord Row(CourseQualityAIUsage? usage, DateTimeOffset dateAssessed, string title = "Course") => new()
    {
      Id = Guid.NewGuid(),
      OpportunityId = Guid.NewGuid(),
      OrganizationId = Guid.NewGuid(),
      OpportunityTitle = title,
      DateAssessed = dateAssessed,
      AIUsage = usage == null ? null : JsonConvert.SerializeObject(usage, new StringEnumConverter())
    };

    private static CourseQualityAIUsageService UsageService(List<CourseQualityAssessmentRecord> rows, CourseQualityAIEndpoint endpoint)
    {
      var repository = new Mock<IRepository<CourseQualityAssessmentRecord>>();
      repository.Setup(o => o.Query()).Returns(() => rows.AsQueryable());
      return new CourseQualityAIUsageService(repository.Object, endpoint);
    }

    [Fact]
    public void Summary_TotalsEveryMeteredRun_AndListsTheNewestFirst()
    {
      var rows = new List<CourseQualityAssessmentRecord>
      {
        Row(Usage(duration: 40000, dateRun: Now.AddMinutes(-3)), Now.AddMinutes(-3), "Oldest"),
        Row(Usage(CourseQualityAIOutcome.Failed, cost: null, duration: 1000, dateRun: Now.AddMinutes(-2)), Now.AddMinutes(-2), "Failed"),
        Row(Usage(cost: 0.0042m, duration: 3000, dateRun: Now.AddMinutes(-1)), Now.AddMinutes(-1), "Newest"),
        Row(null, Now, "Keyless")
      };

      var summary = UsageService(rows, new CourseQualityAIEndpoint(AIProviderKind.OpenAICompatible, Model, "localhost:11434", true, true)).GetSummary();

      Assert.Equal(AIProviderKind.OpenAICompatible, summary.Provider);
      Assert.Equal(Model, summary.Model);
      Assert.Equal("localhost:11434", summary.EndpointHost);
      Assert.True(summary.IsLocal);
      Assert.True(summary.Configured);
      Assert.Equal(3, summary.Runs);
      Assert.Equal(2, summary.Answered);
      Assert.Equal(3 * 1312, summary.InputTokens);
      Assert.Equal(3 * 201, summary.OutputTokens);
      Assert.Equal(0.0042m, summary.CostUsd);
      Assert.Equal(14667, summary.AverageDurationMs);
      Assert.Equal(["Newest", "Failed", "Oldest"], summary.Recent.Select(o => o.OpportunityTitle));
      Assert.Equal(rows[2].Id, summary.Recent[0].AssessmentId);
      Assert.Equal(rows[2].OpportunityId, summary.Recent[0].OpportunityId);
      Assert.Equal(rows[2].OrganizationId, summary.Recent[0].OrganizationId);
    }

    [Fact]
    public void Summary_KeepsAtMostTwentyRecentRuns()
    {
      var rows = Enumerable.Range(0, 25).Select(i => Row(Usage(dateRun: Now.AddMinutes(i)), Now.AddMinutes(i), $"Run {i}")).ToList();

      var summary = UsageService(rows, CourseQualityAIEndpoint.None).GetSummary();

      Assert.Equal(25, summary.Runs);
      Assert.Equal(20, summary.Recent.Count);
      Assert.Equal("Run 24", summary.Recent[0].OpportunityTitle);
    }

    [Fact]
    public void Summary_WithNoRuns_HasNoAverage_AndNoneIsNotConfigured()
    {
      var summary = UsageService([Row(null, Now)], CourseQualityAIEndpoint.None).GetSummary();

      Assert.Equal(AIProviderKind.None, summary.Provider);
      Assert.False(summary.Configured);
      Assert.Equal(0, summary.Runs);
      Assert.Null(summary.AverageDurationMs);
      Assert.Empty(summary.Recent);
      Assert.Equal(0m, summary.CostUsd);
    }
    #endregion

    #region Live, opt-in: a real local model
    /// <summary>
    /// Free and local only: set ZIQI_LOCAL_AI_URL (for example http://localhost:11434/v1) and optionally ZIQI_LOCAL_AI_MODEL
    /// (default qwen2.5:3b). A CPU-only model can take a minute or more, so the timeout is generous.
    /// </summary>
    [Fact]
    public async Task Live_LocalModel_AnswersThroughTheProvider()
    {
      var baseUrl = Environment.GetEnvironmentVariable("ZIQI_LOCAL_AI_URL");
      Assert.SkipWhen(string.IsNullOrWhiteSpace(baseUrl), "ZIQI_LOCAL_AI_URL is not set");

      var model = Environment.GetEnvironmentVariable("ZIQI_LOCAL_AI_MODEL");
      var options = AIOptions(o =>
      {
        o.BaseUrl = baseUrl;
        o.Model = string.IsNullOrWhiteSpace(model) ? Model : model;
        o.IsLocal = true;
        o.TimeoutInSeconds = 600;
      });
      var ai = Options.Create(options);
      using var http = new OpenAICompatibleChatClient(ai);
      var client = new RecordingClient(http);
      var provider = new OpenAICompatibleCourseQualityProvider(NullLogger<OpenAICompatibleCourseQualityProvider>.Instance, ai, Options.Create(new CourseQualityOptions()), client);

      var call = await provider.Call(Request(DatedPage + LongText), TestContext.Current.CancellationToken);

      var usage = call.Usage!;
      TestContext.Current.SendDiagnosticMessage(
        $"Live local model {usage.Model} at {usage.EndpointHost}: outcome {usage.Outcome}, input {usage.InputTokens} tokens, output {usage.OutputTokens} tokens, " +
        $"cost {usage.CostUsd}, {usage.DurationMs} ms, summary \"{call.Result?.Summary}\", confidence {call.Result?.Confidence}, format {call.Result?.Format}");

      if (usage.Outcome != CourseQualityAIOutcome.Answered)
        TestContext.Current.SendDiagnosticMessage($"Raw reply ({client.Last?.FinishReason}): {client.Last?.Content}");

      Assert.Equal(CourseQualityAIOutcome.Answered, usage.Outcome);
      Assert.NotNull(call.Result);
      Assert.True(usage.InputTokens > 0);
      Assert.True(usage.OutputTokens > 0);
      Assert.Equal(0m, usage.CostUsd);
    }
    #endregion
  }
}
