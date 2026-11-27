using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.Core.Repositories;
using Yoma.Core.Infrastructure.Shared.Extensions;

namespace Yoma.Core.Infrastructure.Database.Pathway.Repositories
{
  public class PathwayStepLockRepository : BaseRepository<Entities.PathwayStepLock, Guid>, IRepository<PathwayStepLockRecord>
  {
    #region Constructor
    public PathwayStepLockRepository(ApplicationDbContext context) : base(context) { }
    #endregion

    #region Public Members
    public IQueryable<PathwayStepLockRecord> Query()
    {
      return _context.PathwayStepLock.Select(entity => new PathwayStepLockRecord
      {
        Id = entity.Id,
        UserId = entity.UserId,
        OpportunityId = entity.OpportunityId,
        DateCreated = entity.DateCreated
      });
    }

    public IQueryable<PathwayStepLockRecord> Query(LockMode lockMode)
    {
      return Query().WithLock(lockMode);
    }

    public async Task<PathwayStepLockRecord> Create(PathwayStepLockRecord item)
    {
      item.DateCreated = DateTimeOffset.UtcNow;

      var entity = new Entities.PathwayStepLock
      {
        Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
        UserId = item.UserId,
        OpportunityId = item.OpportunityId,
        DateCreated = item.DateCreated
      };

      _context.PathwayStepLock.Add(entity);
      await _context.SaveChangesAsync();

      item.Id = entity.Id;
      return item;
    }

    /// <summary>
    /// A pin has nothing to change: it exists or it does not.
    /// </summary>
    public Task<PathwayStepLockRecord> Update(PathwayStepLockRecord item)
    {
      throw new NotSupportedException("A step lock is created or deleted, never updated");
    }

    public async Task Delete(PathwayStepLockRecord item)
    {
      var entity = _context.PathwayStepLock.SingleOrDefault(o => o.Id == item.Id)
        ?? throw new InvalidOperationException($"{nameof(Entities.PathwayStepLock)} with id '{item.Id}' does not exist");

      _context.PathwayStepLock.Remove(entity);
      await _context.SaveChangesAsync();
    }
    #endregion
  }
}
