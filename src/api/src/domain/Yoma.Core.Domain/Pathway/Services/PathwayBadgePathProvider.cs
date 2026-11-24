using Microsoft.Extensions.DependencyInjection;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Pathway.Badges.Interfaces;
using Yoma.Core.Domain.Pathway.Badges.Models;
using Yoma.Core.Domain.Pathway.Interfaces;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// Gives the badges endpoint the signed-in youth's current path. Resolves the pathway service when asked, because the
  /// pathway service itself depends on the badge source.
  /// </summary>
  public sealed class PathwayBadgePathProvider : IPathwayBadgePathProvider
  {
    private readonly IServiceProvider _serviceProvider;

    public PathwayBadgePathProvider(IServiceProvider serviceProvider)
    {
      _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task<LearnerBadgePath?> GetForUser(Guid userId)
    {
      try
      {
        var myYoma = await _serviceProvider.GetRequiredService<IPathwayService>().Get();
        return PathwayService.ToBadgePath(myYoma);
      }
      catch (EntityNotFoundException)
      {
        return null; // not onboarded yet: only the badges that need no path can be earned
      }
    }
  }
}
