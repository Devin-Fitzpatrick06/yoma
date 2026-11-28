using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Infrastructure.OpenAICompatible.Interfaces;
using Yoma.Core.Infrastructure.OpenAICompatible.Models;

namespace Yoma.Core.Infrastructure.OpenAICompatible.Client
{
  /// <summary>
  /// POSTs {BaseUrl}/chat/completions with structured output (a strict JSON schema), temperature 0 and max_tokens. The base
  /// URL is operator configuration and may be a local host, so it is deliberately not routed through the course-page link
  /// checker. Redirects are not followed, so a key is only ever sent to the configured host. Registered only when a base URL
  /// and model are configured.
  /// </summary>
  public sealed class OpenAICompatibleChatClient : IOpenAICompatibleChatClient, IDisposable
  {
    #region Class Variables
    private const int MaxResponseBytes = 1_048_576;

    private readonly CourseQualityOpenAICompatibleOptions _options;
    private readonly Uri _endpoint;
    private readonly HttpClient _httpClient;
    #endregion

    #region Constructor
    public OpenAICompatibleChatClient(IOptions<CourseQualityAIOptions> options, HttpMessageHandler? handler = null)
    {
      _options = options?.Value?.OpenAICompatible ?? throw new ArgumentNullException(nameof(options));

      if (!_options.IsConfigured)
        throw new InvalidOperationException($"'{CourseQualityAIOptions.Section}:{nameof(CourseQualityAIOptions.OpenAICompatible)}' base URL and model are not configured");

      _endpoint = Endpoint(_options.BaseUri!);

      _httpClient = handler == null
        ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        : new HttpClient(handler, disposeHandler: false);

      _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutInSeconds));
      _httpClient.MaxResponseContentBufferSize = MaxResponseBytes;
    }
    #endregion

    #region Public Members
    /// <summary>
    /// {BaseUrl}/chat/completions. The path is built on the base URL's path alone, so a query some hosts need (for example
    /// "?api-version=...") is kept after "/chat/completions" instead of swallowing it; a fragment is never sent, so it is dropped.
    /// The base URL's path is kept whether or not it ends in a slash ("/v1" is not lost).
    /// </summary>
    public static Uri Endpoint(Uri baseUri)
    {
      ArgumentNullException.ThrowIfNull(baseUri, nameof(baseUri));

      return new Uri(baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/chat/completions" + baseUri.Query);
    }

    public async Task<OpenAICompatibleCompletion> Create(OpenAICompatibleChatRequest request, CancellationToken cancellationToken)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));

      var body = new JsonObject
      {
        ["model"] = _options.Model,
        ["messages"] = new JsonArray
        {
          new JsonObject { ["role"] = "system", ["content"] = request.System },
          new JsonObject { ["role"] = "user", ["content"] = request.User }
        },
        ["temperature"] = 0,
        ["max_tokens"] = _options.MaxTokens,
        ["stream"] = false,
        ["response_format"] = new JsonObject
        {
          ["type"] = "json_schema",
          ["json_schema"] = new JsonObject
          {
            ["name"] = request.SchemaName,
            ["strict"] = true,
            ["schema"] = JsonNode.Parse(request.JsonSchema)
          }
        }
      };

      using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint)
      {
        Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
      };
      message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      if (_options.HasApiKey) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey!.Trim());

      using var response = await _httpClient.SendAsync(message, cancellationToken);

      // the status only: an error body can echo the request
      if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Chat completion returned {(int)response.StatusCode}", null, response.StatusCode);

      var text = await response.Content.ReadAsStringAsync(cancellationToken);

      return ParseCompletion(text);
    }

    public void Dispose()
    {
      _httpClient.Dispose();
    }
    #endregion

    #region Private Members
    /// <summary>
    /// The envelope only: the content is the model's reply, which the shared parser validates.
    /// </summary>
    internal static OpenAICompatibleCompletion ParseCompletion(string text)
    {
      using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
      var root = document.RootElement;

      if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        throw new InvalidDataException("The response is not a chat completion");

      var choice = choices[0];
      var finishReason = String(choice, "finish_reason");

      string? content = null;
      string? refusal = null;
      if (choice.TryGetProperty("message", out var reply) && reply.ValueKind == JsonValueKind.Object)
      {
        content = String(reply, "content");
        refusal = String(reply, "refusal");
      }

      long promptTokens = 0, completionTokens = 0;
      if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
      {
        promptTokens = Number(usage, "prompt_tokens");
        completionTokens = Number(usage, "completion_tokens");
      }

      return new OpenAICompatibleCompletion(finishReason, content, refusal, promptTokens, completionTokens);
    }

    private static string? String(JsonElement element, string name) =>
      element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long Number(JsonElement element, string name) =>
      element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number > 0 ? number : 0;
    #endregion
  }
}
