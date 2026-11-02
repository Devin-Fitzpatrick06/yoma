namespace Yoma.Core.Infrastructure.Anthropic.Models
{
  public sealed record AnthropicCompletionRequest(string System, string User, string JsonSchema);

  public sealed record AnthropicCompletion(string? StopReason, string? Text);
}
