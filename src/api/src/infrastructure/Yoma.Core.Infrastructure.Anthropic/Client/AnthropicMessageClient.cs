using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Yoma.Core.Infrastructure.Anthropic.Interfaces;
using Yoma.Core.Infrastructure.Anthropic.Models;

namespace Yoma.Core.Infrastructure.Anthropic.Client
{
  /// <summary>
  /// Registered only when a key is configured. The key is always passed explicitly, so a stray ANTHROPIC_API_KEY in the
  /// environment is never picked up. Temperature, top_p, prefill and a thinking setting are deliberately not sent.
  /// </summary>
  public sealed class AnthropicMessageClient : IAnthropicMessageClient, IDisposable
  {
    #region Class Variables
    private readonly AnthropicOptions _options;
    private readonly AnthropicClient _client;
    #endregion

    #region Constructor
    public AnthropicMessageClient(IOptions<AnthropicOptions> options, HttpClient? httpClient = null)
    {
      _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

      if (!_options.HasApiKey)
        throw new InvalidOperationException($"'{AnthropicOptions.Section}' API key is not configured");

      _client = httpClient == null
        ? new AnthropicClient { ApiKey = _options.ApiKey, Timeout = TimeSpan.FromSeconds(_options.TimeoutInSeconds), MaxRetries = _options.MaxRetries }
        : new AnthropicClient { ApiKey = _options.ApiKey, Timeout = TimeSpan.FromSeconds(_options.TimeoutInSeconds), MaxRetries = _options.MaxRetries, HttpClient = httpClient };
    }
    #endregion

    #region Public Members
    public async Task<AnthropicCompletion> Create(AnthropicCompletionRequest request, CancellationToken cancellationToken)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));

      var message = await _client.Messages.Create(new MessageCreateParams
      {
        Model = _options.Model,
        MaxTokens = _options.MaxTokens,
        System = request.System,
        Messages = [new UserMessageParam { Content = request.User }],
        OutputConfig = new OutputConfig
        {
          Effort = ParseEffort(_options.Effort),
          Format = new JsonOutputFormat
          {
            Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.JsonSchema)!
          }
        }
      }, cancellationToken);

      var text = string.Concat(message.Content.Select(o => o.Value).OfType<TextBlock>().Select(o => o.Text));

      return new AnthropicCompletion(message.StopReason?.Raw(), text);
    }

    public void Dispose()
    {
      _client.Dispose();
    }
    #endregion

    #region Private Members
    private static Effort ParseEffort(string? value)
    {
      return value?.Trim().ToLowerInvariant() switch
      {
        "medium" => Effort.Medium,
        "high" => Effort.High,
        _ => Effort.Low
      };
    }
    #endregion
  }
}
