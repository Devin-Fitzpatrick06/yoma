namespace Yoma.Core.Infrastructure.OpenAICompatible.Models
{
  public sealed record OpenAICompatibleChatRequest(string System, string User, string JsonSchema, string SchemaName);

  /// <summary>
  /// choices[0]: the message content, finish_reason and any refusal; usage: prompt_tokens and completion_tokens (0 when the server sends none).
  /// </summary>
  public sealed record OpenAICompatibleCompletion(string? FinishReason, string? Content, string? Refusal, long PromptTokens, long CompletionTokens);
}
