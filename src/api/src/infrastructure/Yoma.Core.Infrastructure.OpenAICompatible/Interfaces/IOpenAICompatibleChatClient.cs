using Yoma.Core.Infrastructure.OpenAICompatible.Models;

namespace Yoma.Core.Infrastructure.OpenAICompatible.Interfaces
{
  /// <summary>
  /// The one seam around the HTTP call, so the provider can be tested without a server.
  /// </summary>
  public interface IOpenAICompatibleChatClient
  {
    /// <summary>
    /// Throws on an HTTP error, a timeout or a response that is not a chat completion.
    /// </summary>
    Task<OpenAICompatibleCompletion> Create(OpenAICompatibleChatRequest request, CancellationToken cancellationToken);
  }
}
