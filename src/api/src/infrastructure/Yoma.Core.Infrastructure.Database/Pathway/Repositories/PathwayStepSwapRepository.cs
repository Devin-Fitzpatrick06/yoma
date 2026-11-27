using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.Core.Repositories;
using Yoma.Core.Infrastructure.Shared.Extensions;

namespace Yoma.Core.Infrastructure.Database.Pathway.Repositories
{
  public class PathwayStepSwapRepository : BaseRepository<Entities.PathwayStepSwap, Guid>, IRepository<PathwayStepSwapRecord>
  {
    #region Constructor
    public PathwayStepSwapRepository(ApplicationDbContext context) : base(context) { }
    #endregion

    #region Public Members
    public IQueryable<PathwayStepSwapRecord> Query()
    {
      return _context.PathwayStepSwap.Select(entity => new PathwayStepSwapRecord
      {
        Id = entity.Id,
        UserId = entity.UserId,
        ReplacedOpportunityId = entity.ReplacedOpportunityId,
        ReplacementOpportunityId = entity.ReplacementOpportunityId,
        DateCreated = entity.DateCreated
      });
    }

    public IQueryable<PathwayStepSwapRecord> Query(LockMode lockMode)
    {
      return Query().WithLock(lockMode);
    }

    public async Task<PathwayStepSwapRecord> Create(PathwayStepSwapRecord item)
    {
      item.DateCreated = DateTimeOffset.UtcNow;

      var entity = new Entities.PathwayStepSwap
      {
        Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
        UserId = item.UserId,
        ReplacedOpportunityId = item.ReplacedOpportunityId,
        ReplacementOpportunityId = item.ReplacementOpportunityId,
        DateCreated = item.DateCreated
      };

      _context.PathwayStepSwap.Add(entity);
      await _context.SaveChangesAsync();

      item.Id = entity.Id;
      return item;
    }

    /// <summary>
    /// Swaps are a history in the order made; a later swap is a new row.
    /// </summary>
    public Task<PathwayStepSwapRecord> Update(PathwayStepSwapRecord item)
    {
      throw new NotSupportedException("A step swap is recorded once, never updated");
    }

    public async Task Delete(PathwayStepSwapRecord item)
    {
      var entity = _context.PathwayStepSwap.SingleOrDefault(o => o.Id == item.Id)
        ?? throw new InvalidOperationException($"{nameof(Entities.PathwayStepSwap)} with id '{item.Id}' does not exist");

      _context.PathwayStepSwap.Remove(entity);
      await _context.SaveChangesAsync();
    }
    #endregion
  }
}
