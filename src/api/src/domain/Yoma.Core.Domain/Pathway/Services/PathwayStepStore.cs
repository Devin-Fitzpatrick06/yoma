using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// Pins and swaps from the database (Pathway.StepLock, Pathway.StepSwap). Registered with AddScoped, so it replaces the no-op default.
  /// </summary>
  public sealed class PathwayStepStore : IPathwayStepStore
  {
    #region Class Variables
    private readonly IRepository<PathwayStepLockRecord> _lockRepository;
    private readonly IRepository<PathwayStepSwapRecord> _swapRepository;
    #endregion

    #region Constructor
    public PathwayStepStore(IRepository<PathwayStepLockRecord> lockRepository, IRepository<PathwayStepSwapRecord> swapRepository)
    {
      _lockRepository = lockRepository ?? throw new ArgumentNullException(nameof(lockRepository));
      _swapRepository = swapRepository ?? throw new ArgumentNullException(nameof(swapRepository));
    }
    #endregion

    #region Public Members
    public Task<List<Guid>> ListLocked(Guid userId)
    {
      var result = _lockRepository.Query().Where(o => o.UserId == userId)
        .OrderBy(o => o.DateCreated).ThenBy(o => o.OpportunityId)
        .Select(o => o.OpportunityId).ToList();

      return Task.FromResult(result);
    }

    public Task<List<(Guid Replaced, Guid Replacement)>> ListSwaps(Guid userId)
    {
      var rows = _swapRepository.Query().Where(o => o.UserId == userId)
        .OrderBy(o => o.DateCreated).ThenBy(o => o.Id)
        .Select(o => new { o.ReplacedOpportunityId, o.ReplacementOpportunityId }).ToList();

      return Task.FromResult(rows.Select(o => (o.ReplacedOpportunityId, o.ReplacementOpportunityId)).ToList());
    }
    #endregion
  }
}
