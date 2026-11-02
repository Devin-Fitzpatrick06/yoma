namespace Yoma.Core.Infrastructure.Anthropic.Models
{
  public class AnthropicOptions
  {
    public const string Section = "Anthropic";

    /// <summary>
    /// From configuration only, in deployment through the Anthropic__ApiKey environment variable. Empty means the keyless reader runs.
    /// </summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>
    /// low, medium or high.
    /// </summary>
    public string Effort { get; set; } = "low";

    public int MaxTokens { get; set; } = 16000;

    public int TimeoutInSeconds { get; set; } = 60;

    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Empty, or a "[apiKey]" placeholder as used across appsettings, is not a key.
    /// </summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.TrimStart().StartsWith('[');
  }
}
