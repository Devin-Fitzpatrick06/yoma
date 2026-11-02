using Yoma.Core.Infrastructure.Anthropic.Models;

namespace Yoma.Core.Infrastructure.Anthropic.Interfaces
{
  /// <summary>
  /// The one seam around the SDK, so the provider can be tested without a key.
  /// </summary>
  public interface IAnthropicMessageClient
  {
    Task<AnthropicCompletion> Create(AnthropicCompletionRequest request, CancellationToken cancellationToken);
  }
}
