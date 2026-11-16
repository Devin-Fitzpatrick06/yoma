using Microsoft.EntityFrameworkCore;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.Core.Repositories;
using Yoma.Core.Infrastructure.Shared.Extensions;

namespace Yoma.Core.Infrastructure.Database.CourseQuality.Repositories
{
  /// <summary>
  /// One row per opportunity. A grading run writes only what changed, in one save per kind of change.
  /// </summary>
  public class CourseQualityGradeRepository : BaseRepository<Entities.CourseQualityGrade, Guid>, IRepositoryBatched<CourseQualityGradeRecord>
  {
    #region Constructor
    public CourseQualityGradeRepository(ApplicationDbContext context) : base(context) { }
    #endregion

    #region Public Members
    public IQueryable<CourseQualityGradeRecord> Query()
    {
      return _context.CourseQualityGrade.Select(entity => new CourseQualityGradeRecord
      {
        Id = entity.Id,
        OpportunityId = entity.OpportunityId,
        OpportunityTitle = entity.Opportunity.Title,
        OrganizationName = entity.Opportunity.Organization.Name,
        Status = entity.Status,
        Grade = entity.Grade,
        GradeLabel = entity.GradeLabel,
        Score = entity.Score,
        Percentile = entity.Percentile,
        FeedbackResponses = entity.FeedbackResponses,
        Factors = entity.Factors,
        Caps = entity.Caps,
        RubricVersion = entity.RubricVersion,
        DateGraded = entity.DateGraded
      });
    }

    public IQueryable<CourseQualityGradeRecord> Query(LockMode lockMode)
    {
      return Query().WithLock(lockMode);
    }

    public async Task<CourseQualityGradeRecord> Create(CourseQualityGradeRecord item)
    {
      return (await Create([item])).Single();
    }

    public async Task<List<CourseQualityGradeRecord>> Create(List<CourseQualityGradeRecord> items)
    {
      if (items == null || items.Count == 0)
        throw new ArgumentNullException(nameof(items));

      var entities = items.Select(item =>
      {
        var entity = new Entities.CourseQualityGrade { Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id };
        Apply(entity, item);
        item.Id = entity.Id;
        return entity;
      }).ToList();

      _context.CourseQualityGrade.AddRange(entities);
      await _context.SaveChangesAsync();

      return items;
    }

    public async Task<CourseQualityGradeRecord> Update(CourseQualityGradeRecord item)
    {
      return (await Update([item])).Single();
    }

    public async Task<List<CourseQualityGradeRecord>> Update(List<CourseQualityGradeRecord> items)
    {
      if (items == null || items.Count == 0)
        throw new ArgumentNullException(nameof(items));

      var itemIds = items.Select(o => o.Id).ToList();
      var entities = _context.CourseQualityGrade.Where(o => itemIds.Contains(o.Id)).ToDictionary(o => o.Id);

      foreach (var item in items)
      {
        var entity = entities.GetValueOrDefault(item.Id) ?? throw new InvalidOperationException($"{nameof(Entities.CourseQualityGrade)} with id '{item.Id}' does not exist");
        Apply(entity, item);
      }

      await _context.SaveChangesAsync();

      return items;
    }

    public async Task Delete(CourseQualityGradeRecord item)
    {
      await Delete([item]);
    }

    public async Task Delete(List<CourseQualityGradeRecord> items)
    {
      if (items == null || items.Count == 0)
        throw new ArgumentNullException(nameof(items));

      var itemIds = items.Select(o => o.Id).ToList();
      await _context.CourseQualityGrade.Where(o => itemIds.Contains(o.Id)).ExecuteDeleteAsync();
    }
    #endregion

    #region Private Members
    private static void Apply(Entities.CourseQualityGrade entity, CourseQualityGradeRecord item)
    {
      entity.OpportunityId = item.OpportunityId;
      entity.Status = item.Status;
      entity.Grade = item.Grade;
      entity.GradeLabel = item.GradeLabel;
      entity.Score = item.Score;
      entity.Percentile = item.Percentile;
      entity.FeedbackResponses = item.FeedbackResponses;
      entity.Factors = item.Factors;
      entity.Caps = item.Caps;
      entity.RubricVersion = item.RubricVersion;
      entity.DateGraded = item.DateGraded;
    }
    #endregion
  }
}
