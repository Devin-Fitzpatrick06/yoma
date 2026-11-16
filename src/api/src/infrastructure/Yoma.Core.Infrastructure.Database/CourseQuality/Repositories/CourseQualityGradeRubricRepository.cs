using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.Core.Repositories;
using Yoma.Core.Infrastructure.Shared.Extensions;

namespace Yoma.Core.Infrastructure.Database.CourseQuality.Repositories
{
  public class CourseQualityGradeRubricRepository : BaseRepository<Entities.CourseQualityGradeRubric, Guid>, IRepository<CourseQualityGradeRubricRecord>
  {
    #region Constructor
    public CourseQualityGradeRubricRepository(ApplicationDbContext context) : base(context) { }
    #endregion

    #region Public Members
    public IQueryable<CourseQualityGradeRubricRecord> Query()
    {
      return _context.CourseQualityGradeRubric.Select(entity => new CourseQualityGradeRubricRecord
      {
        Id = entity.Id,
        Rubric = entity.Rubric,
        UpdatedByEmail = entity.UpdatedByEmail,
        DateCreated = entity.DateCreated
      });
    }

    public IQueryable<CourseQualityGradeRubricRecord> Query(LockMode lockMode)
    {
      return Query().WithLock(lockMode);
    }

    public async Task<CourseQualityGradeRubricRecord> Create(CourseQualityGradeRubricRecord item)
    {
      var entity = new Entities.CourseQualityGradeRubric
      {
        Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
        Rubric = item.Rubric,
        UpdatedByEmail = item.UpdatedByEmail,
        DateCreated = item.DateCreated
      };

      _context.CourseQualityGradeRubric.Add(entity);
      await _context.SaveChangesAsync();

      item.Id = entity.Id;
      return item;
    }

    public Task<CourseQualityGradeRubricRecord> Update(CourseQualityGradeRubricRecord item)
    {
      throw new NotSupportedException("Rubric overrides are kept as history; save a new one instead");
    }

    public Task Delete(CourseQualityGradeRubricRecord item)
    {
      throw new NotSupportedException("Rubric overrides are kept as history; save a reset instead");
    }
    #endregion
  }
}
