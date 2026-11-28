namespace Yoma.Core.Infrastructure.Anthropic.Models
{
  public sealed record AnthropicCompletionRequest(string System, string User, string JsonSchema);

  /// <summary>
  /// The reply text and stop reason, with the token counts from the response's usage (input_tokens, output_tokens).
  /// </summary>
  public sealed record AnthropicCompletion(string? StopReason, string? Text, long InputTokens = 0, long OutputTokens = 0);
}
