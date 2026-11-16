using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.Core.Repositories;
using Yoma.Core.Infrastructure.Shared.Extensions;

namespace Yoma.Core.Infrastructure.Database.CourseQuality.Repositories
{
  public class CourseQualityLearnerFeedbackRepository : BaseRepository<Entities.CourseQualityLearnerFeedback, Guid>, IRepository<CourseQualityLearnerFeedbackRecord>
  {
    #region Constructor
    public CourseQualityLearnerFeedbackRepository(ApplicationDbContext context) : base(context) { }
    #endregion

    #region Public Members
    public IQueryable<CourseQualityLearnerFeedbackRecord> Query()
    {
      return _context.CourseQualityLearnerFeedback.Select(entity => new CourseQualityLearnerFeedbackRecord
      {
        Id = entity.Id,
        UserId = entity.UserId,
        OpportunityId = entity.OpportunityId,
        Answer = entity.Answer,
        DateLeft = entity.DateLeft,
        DateAsked = entity.DateAsked,
        SnoozeUntil = entity.SnoozeUntil,
        Rating = entity.Rating,
        Completed = entity.Completed,
        WouldRecommend = entity.WouldRecommend,
        MatchedDescription = entity.MatchedDescription,
        Good = entity.Good,
        Bad = entity.Bad,
        Comment = entity.Comment,
        DateCreated = entity.DateCreated,
        DateModified = entity.DateModified
      });
    }

    public IQueryable<CourseQualityLearnerFeedbackRecord> Query(LockMode lockMode)
    {
      return Query().WithLock(lockMode);
    }

    public async Task<CourseQualityLearnerFeedbackRecord> Create(CourseQualityLearnerFeedbackRecord item)
    {
      var now = DateTimeOffset.UtcNow;
      item.DateCreated = now;
      item.DateModified = now;

      var entity = new Entities.CourseQualityLearnerFeedback
      {
        Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
        UserId = item.UserId,
        OpportunityId = item.OpportunityId,
        DateCreated = item.DateCreated
      };
      Apply(entity, item);

      _context.CourseQualityLearnerFeedback.Add(entity);
      await _context.SaveChangesAsync();

      item.Id = entity.Id;
      return item;
    }

    public async Task<CourseQualityLearnerFeedbackRecord> Update(CourseQualityLearnerFeedbackRecord item)
    {
      var entity = _context.CourseQualityLearnerFeedback.SingleOrDefault(o => o.Id == item.Id)
        ?? throw new InvalidOperationException($"{nameof(Entities.CourseQualityLearnerFeedback)} with id '{item.Id}' does not exist");

      item.DateModified = DateTimeOffset.UtcNow;
      Apply(entity, item);

      await _context.SaveChangesAsync();

      return item;
    }

    public Task Delete(CourseQualityLearnerFeedbackRecord item)
    {
      throw new NotSupportedException("Learner feedback is kept; record a new answer instead");
    }
    #endregion

    #region Private Members
    private static void Apply(Entities.CourseQualityLearnerFeedback entity, CourseQualityLearnerFeedbackRecord item)
    {
      entity.Answer = item.Answer;
      entity.DateLeft = item.DateLeft;
      entity.DateAsked = item.DateAsked;
      entity.SnoozeUntil = item.SnoozeUntil;
      entity.Rating = item.Rating;
      entity.Completed = item.Completed;
      entity.WouldRecommend = item.WouldRecommend;
      entity.MatchedDescription = item.MatchedDescription;
      entity.Good = string.IsNullOrWhiteSpace(item.Good) ? "[]" : item.Good;
      entity.Bad = string.IsNullOrWhiteSpace(item.Bad) ? "[]" : item.Bad;
      entity.Comment = item.Comment;
      entity.DateModified = item.DateModified;
    }
    #endregion
  }
}
