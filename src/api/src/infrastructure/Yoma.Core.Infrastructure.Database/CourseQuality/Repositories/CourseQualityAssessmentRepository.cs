using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.Core.Repositories;
using Yoma.Core.Infrastructure.Shared.Extensions;

namespace Yoma.Core.Infrastructure.Database.CourseQuality.Repositories
{
  public class CourseQualityAssessmentRepository : BaseRepository<Entities.CourseQualityAssessment, Guid>, IRepository<CourseQualityAssessmentRecord>
  {
    #region Constructor
    public CourseQualityAssessmentRepository(ApplicationDbContext context) : base(context) { }
    #endregion

    #region Public Members
    public IQueryable<CourseQualityAssessmentRecord> Query()
    {
      return _context.CourseQualityAssessment.Select(entity => new CourseQualityAssessmentRecord
      {
        Id = entity.Id,
        OpportunityId = entity.OpportunityId,
        OpportunityTitle = entity.Opportunity.Title,
        OrganizationId = entity.OrganizationId,
        OrganizationName = entity.Organization.Name,
        RubricVersion = entity.RubricVersion,
        Score = entity.Score,
        Confidence = entity.Confidence,
        Status = entity.Status,
        ReviewStatus = entity.ReviewStatus,
        Format = entity.Format,
        DatesSuspect = entity.DatesSuspect,
        CardReady = entity.CardReady,
        AIUsed = entity.AIUsed,
        AIModel = entity.AIModel,
        AISummary = entity.AISummary,
        Criteria = entity.Criteria,
        LearnerCard = entity.LearnerCard,
        FixNote = entity.FixNote,
        ReviewedByEmail = entity.ReviewedByEmail,
        ReviewNote = entity.ReviewNote,
        DateReviewed = entity.DateReviewed,
        DateAssessed = entity.DateAssessed,
        OpportunityDateModified = entity.OpportunityDateModified,
        ContentFingerprint = entity.ContentFingerprint,
        FinalHost = entity.FinalHost,
        DateConfirmed = entity.DateConfirmed,
        IsLatest = entity.IsLatest
      });
    }

    public IQueryable<CourseQualityAssessmentRecord> Query(LockMode lockMode)
    {
      return Query().WithLock(lockMode);
    }

    public async Task<CourseQualityAssessmentRecord> Create(CourseQualityAssessmentRecord item)
    {
      var entity = new Entities.CourseQualityAssessment { Id = item.Id, OpportunityId = item.OpportunityId, OrganizationId = item.OrganizationId };
      Apply(entity, item);

      _context.CourseQualityAssessment.Add(entity);
      await _context.SaveChangesAsync();

      item.Id = entity.Id;
      return item;
    }

    public async Task<CourseQualityAssessmentRecord> Update(CourseQualityAssessmentRecord item)
    {
      var entity = _context.CourseQualityAssessment.SingleOrDefault(o => o.Id == item.Id)
        ?? throw new InvalidOperationException($"{nameof(Entities.CourseQualityAssessment)} with id '{item.Id}' does not exist");

      Apply(entity, item);

      await _context.SaveChangesAsync();

      return item;
    }

    public Task Delete(CourseQualityAssessmentRecord item)
    {
      throw new NotSupportedException("Assessments are kept as history");
    }
    #endregion

    #region Private Members
    private static void Apply(Entities.CourseQualityAssessment entity, CourseQualityAssessmentRecord item)
    {
      entity.RubricVersion = item.RubricVersion;
      entity.Score = item.Score;
      entity.Confidence = item.Confidence;
      entity.Status = item.Status;
      entity.ReviewStatus = item.ReviewStatus;
      entity.Format = item.Format;
      entity.DatesSuspect = item.DatesSuspect;
      entity.CardReady = item.CardReady;
      entity.AIUsed = item.AIUsed;
      entity.AIModel = item.AIModel;
      entity.AISummary = item.AISummary;
      entity.Criteria = item.Criteria;
      entity.LearnerCard = item.LearnerCard;
      entity.FixNote = item.FixNote;
      entity.ReviewedByEmail = item.ReviewedByEmail;
      entity.ReviewNote = item.ReviewNote;
      entity.DateReviewed = item.DateReviewed;
      entity.DateAssessed = item.DateAssessed;
      entity.OpportunityDateModified = item.OpportunityDateModified;
      entity.ContentFingerprint = item.ContentFingerprint;
      entity.FinalHost = item.FinalHost;
      entity.DateConfirmed = item.DateConfirmed;
      entity.IsLatest = item.IsLatest;
    }
    #endregion
  }
}
