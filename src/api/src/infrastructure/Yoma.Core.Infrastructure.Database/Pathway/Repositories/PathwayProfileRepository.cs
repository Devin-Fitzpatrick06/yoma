using System.Text.Json;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.Core.Repositories;
using Yoma.Core.Infrastructure.Shared.Extensions;

namespace Yoma.Core.Infrastructure.Database.Pathway.Repositories
{
  public class PathwayProfileRepository : BaseRepository<Entities.PathwayProfile, Guid>, IRepository<PathwayProfileRecord>
  {
    #region Constructor
    public PathwayProfileRepository(ApplicationDbContext context) : base(context) { }
    #endregion

    #region Public Members
    /// <summary>
    /// Known skill keys are stored as a JSON array and read back after the query, so a filter on them is not translated to SQL.
    /// </summary>
    public IQueryable<PathwayProfileRecord> Query()
    {
      return _context.PathwayProfile.Select(entity => new PathwayProfileRecord
      {
        Id = entity.Id,
        UserId = entity.UserId,
        GoalKey = entity.GoalKey,
        Background = entity.Background,
        KnownSkillKeys = Deserialize(entity.KnownSkillKeys),
        HoursPerWeek = entity.HoursPerWeek,
        CountryId = entity.CountryId,
        DateCreated = entity.DateCreated,
        DateModified = entity.DateModified
      });
    }

    public IQueryable<PathwayProfileRecord> Query(LockMode lockMode)
    {
      return Query().WithLock(lockMode);
    }

    public async Task<PathwayProfileRecord> Create(PathwayProfileRecord item)
    {
      var now = DateTimeOffset.UtcNow;
      item.DateCreated = now;
      item.DateModified = now;

      var entity = new Entities.PathwayProfile
      {
        Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
        UserId = item.UserId,
        DateCreated = item.DateCreated
      };
      Apply(entity, item);

      _context.PathwayProfile.Add(entity);
      await _context.SaveChangesAsync();

      item.Id = entity.Id;
      return item;
    }

    public async Task<PathwayProfileRecord> Update(PathwayProfileRecord item)
    {
      var entity = _context.PathwayProfile.SingleOrDefault(o => o.Id == item.Id)
        ?? throw new InvalidOperationException($"{nameof(Entities.PathwayProfile)} with id '{item.Id}' does not exist");

      item.DateModified = DateTimeOffset.UtcNow;
      Apply(entity, item);

      await _context.SaveChangesAsync();

      item.DateCreated = entity.DateCreated;
      return item;
    }

    public async Task Delete(PathwayProfileRecord item)
    {
      var entity = _context.PathwayProfile.SingleOrDefault(o => o.Id == item.Id)
        ?? throw new InvalidOperationException($"{nameof(Entities.PathwayProfile)} with id '{item.Id}' does not exist");

      _context.PathwayProfile.Remove(entity);
      await _context.SaveChangesAsync();
    }
    #endregion

    #region Private Members
    private static void Apply(Entities.PathwayProfile entity, PathwayProfileRecord item)
    {
      entity.GoalKey = item.GoalKey;
      entity.Background = item.Background;
      entity.KnownSkillKeys = JsonSerializer.Serialize(item.KnownSkillKeys ?? []);
      entity.HoursPerWeek = item.HoursPerWeek;
      entity.CountryId = item.CountryId;
      entity.DateModified = item.DateModified;
    }

    private static List<string> Deserialize(string? value)
    {
      if (string.IsNullOrWhiteSpace(value)) return [];
      return JsonSerializer.Deserialize<List<string>>(value) ?? [];
    }
    #endregion
  }
}
