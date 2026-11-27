using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.Core.Repositories;
using Yoma.Core.Infrastructure.Shared.Extensions;

namespace Yoma.Core.Infrastructure.Database.Pathway.Repositories
{
  public class PathwayStepEventRepository : BaseRepository<Entities.PathwayStepEvent, Guid>, IRepository<PathwayStepEventRecord>
  {
    #region Constructor
    public PathwayStepEventRepository(ApplicationDbContext context) : base(context) { }
    #endregion

    #region Public Members
    public IQueryable<PathwayStepEventRecord> Query()
    {
      return _context.PathwayStepEvent.Select(entity => new PathwayStepEventRecord
      {
        Id = entity.Id,
        UserId = entity.UserId,
        OpportunityId = entity.OpportunityId,
        GoalKey = entity.GoalKey,
        StageOrder = entity.StageOrder,
        Event = entity.Event,
        DateCreated = entity.DateCreated
      });
    }

    public IQueryable<PathwayStepEventRecord> Query(LockMode lockMode)
    {
      return Query().WithLock(lockMode);
    }

    public async Task<PathwayStepEventRecord> Create(PathwayStepEventRecord item)
    {
      item.DateCreated = DateTimeOffset.UtcNow;

      var entity = new Entities.PathwayStepEvent
      {
        Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
        UserId = item.UserId,
        OpportunityId = item.OpportunityId,
        GoalKey = item.GoalKey,
        StageOrder = item.StageOrder,
        Event = item.Event,
        DateCreated = item.DateCreated
      };

      _context.PathwayStepEvent.Add(entity);
      await _context.SaveChangesAsync();

      item.Id = entity.Id;
      return item;
    }

    /// <summary>
    /// Events are an append-only record.
    /// </summary>
    public Task<PathwayStepEventRecord> Update(PathwayStepEventRecord item)
    {
      throw new NotSupportedException("A step event is recorded once, never updated");
    }

    public async Task Delete(PathwayStepEventRecord item)
    {
      var entity = _context.PathwayStepEvent.SingleOrDefault(o => o.Id == item.Id)
        ?? throw new InvalidOperationException($"{nameof(Entities.PathwayStepEvent)} with id '{item.Id}' does not exist");

      _context.PathwayStepEvent.Remove(entity);
      await _context.SaveChangesAsync();
    }
    #endregion
  }
}
