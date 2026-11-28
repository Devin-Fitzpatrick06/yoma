namespace Yoma.Core.Domain.CourseQuality.Models
{
  /// <summary>
  /// Which model service runs the course page check. Mirrors AIProviderKind in the web's courseQuality.ts.
  /// </summary>
  public enum AIProviderKind
  {
    None,
    Anthropic,
    OpenAICompatible
  }

  /// <summary>
  /// The "AI" configuration section. Whatever the provider, the prompt, the schema and the rule that the AI can only lower
  /// a result are the same; only the model service changes.
  /// </summary>
  public class CourseQualityAIOptions
  {
    public const string Section = "AI";

    /// <summary>
    /// Anthropic by default, which is keyless (the source-page reader, no AI) until an Anthropic key is configured. None never calls a model.
    /// </summary>
    public AIProviderKind Provider { get; set; } = AIProviderKind.Anthropic;

    public CourseQualityOpenAICompatibleOptions OpenAICompatible { get; set; } = new();

    /// <summary>
    /// Per model, in USD per million tokens. Empty by default: no vendor price is assumed, so a remote model without one has a null cost.
    /// Configuration keys cannot contain ':', so a model such as "qwen2.5:3b" is keyed "qwen2.5_3b".
    /// </summary>
    public Dictionary<string, CourseQualityAIPrice> Prices { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 0 on the operator's own host; null for a remote model with no configured price, never a guess.
    /// </summary>
    public decimal? Cost(string? model, bool isLocal, long inputTokens, long outputTokens)
    {
      if (isLocal) return 0m;
      if (string.IsNullOrWhiteSpace(model)) return null;

      if (!Prices.TryGetValue(model, out var price) && !Prices.TryGetValue(model.Replace(':', '_'), out price)) return null;
      if (price == null) return null;

      return Math.Round((inputTokens * price.InputPerMillionUsd + outputTokens * price.OutputPerMillionUsd) / 1_000_000m, 6);
    }
  }

  public class CourseQualityAIPrice
  {
    public decimal InputPerMillionUsd { get; set; }

    public decimal OutputPerMillionUsd { get; set; }
  }

  /// <summary>
  /// Any server speaking the OpenAI chat completions API: Ollama, vLLM, LM Studio, a government-hosted endpoint.
  /// </summary>
  public class CourseQualityOpenAICompatibleOptions
  {
    /// <summary>
    /// For example http://localhost:11434/v1. Operator configuration, so a local address is legitimate here.
    /// </summary>
    public string? BaseUrl { get; set; }

    public string? Model { get; set; }

    /// <summary>
    /// Optional, from the environment only (AI__OpenAICompatible__ApiKey). Sent as a bearer token when set.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Served by the operator's own host: no per-token charge, so the cost is 0. False by default, so a remote paid endpoint
    /// left at the default is never recorded as free; set it to true only for a model on the operator's own host.
    /// </summary>
    public bool IsLocal { get; set; }

    public int TimeoutInSeconds { get; set; } = 300;

    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// Page text sent to the model is cut to this length: a small local model has a small context and a slow prompt.
    /// </summary>
    public int MaxPageChars { get; set; } = 6000;

    /// <summary>
    /// Empty, or a "[apiKey]" placeholder as used across appsettings, is not a key.
    /// </summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.TrimStart().StartsWith('[');

    public Uri? BaseUri => Uri.TryCreate(BaseUrl?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;

    public bool IsConfigured => BaseUri != null && !string.IsNullOrWhiteSpace(Model);

    /// <summary>
    /// host:port of the base URL, for example "localhost:11434".
    /// </summary>
    public string? EndpointHost => BaseUri is { } uri ? $"{uri.Host}:{uri.Port}" : null;
  }
}
