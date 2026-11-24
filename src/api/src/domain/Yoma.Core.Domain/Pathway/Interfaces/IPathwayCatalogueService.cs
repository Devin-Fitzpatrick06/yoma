using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Interfaces
{
  public interface IPathwayCatalogueService
  {
    /// <summary>
    /// Published, active Learning courses and live Job opportunities, cached for Pathway:CacheInMinutes.
    /// </summary>
    PathwayCatalogue Get();
  }
}
