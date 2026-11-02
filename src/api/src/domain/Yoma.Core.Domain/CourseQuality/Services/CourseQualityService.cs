using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Helpers;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  public class CourseQualityService : ICourseQualityService
  {
    #region Class Variables
    private const int MaxPageSize = 100;
    private const int MaxReviewNoteLength = 1000;

    private readonly ILogger<CourseQualityService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CourseQualityOptions _options;
    private readonly IOpportunityService _opportunityService;
    private readonly IOrganizationService _organizationService;
    private readonly IOpportunityTypeService _opportunityTypeService;
    private readonly IOpportunityStatusService _opportunityStatusService;
    private readonly IOrganizationStatusService _organizationStatusService;
    private readonly IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> _opportunityRepository;
    private readonly ICourseQualityLinkChecker _linkChecker;
    private readonly ICourseQualityAIProvider _aiProvider;
    private readonly IRepository<CourseQualityAssessmentRecord> _assessmentRepository;
    private readonly IExecutionStrategyService _executionStrategyService;
    #endregion

    #region Constructor
    public CourseQualityService(ILogger<CourseQualityService> logger,
      IHttpContextAccessor httpContextAccessor,
      IOptions<CourseQualityOptions> options,
      IOpportunityService opportunityService,
      IOrganizationService organizationService,
      IOpportunityTypeService opportunityTypeService,
      IOpportunityStatusService opportunityStatusService,
      IOrganizationStatusService organizationStatusService,
      IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> opportunityRepository,
      ICourseQualityLinkChecker linkChecker,
      ICourseQualityAIProvider aiProvider,
      IRepository<CourseQualityAssessmentRecord> assessmentRepository,
      IExecutionStrategyService executionStrategyService)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
      _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
      _opportunityService = opportunityService ?? throw new ArgumentNullException(nameof(opportunityService));
      _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
      _opportunityTypeService = opportunityTypeService ?? throw new ArgumentNullException(nameof(opportunityTypeService));
      _opportunityStatusService = opportunityStatusService ?? throw new ArgumentNullException(nameof(opportunityStatusService));
      _organizationStatusService = organizationStatusService ?? throw new ArgumentNullException(nameof(organizationStatusService));
      _opportunityRepository = opportunityRepository ?? throw new ArgumentNullException(nameof(opportunityRepository));
      _linkChecker = linkChecker ?? throw new ArgumentNullException(nameof(linkChecker));
      _aiProvider = aiProvider ?? throw new ArgumentNullException(nameof(aiProvider));
      _assessmentRepository = assessmentRepository ?? throw new ArgumentNullException(nameof(assessmentRepository));
      _executionStrategyService = executionStrategyService ?? throw new ArgumentNullException(nameof(executionStrategyService));
    }
    #endregion

    #region Public Members
    public LearnerCard GetPublicCard(Guid opportunityId)
    {
      if (opportunityId == Guid.Empty) throw new ArgumentNullException(nameof(opportunityId));

      // one message for every case, so the response does not reveal why a listing is not visible
      var notFound = new EntityNotFoundException($"Course quality card for opportunity with id '{opportunityId}' does not exist");

      // the assessment first: most listings have no current card, and the child load below is the costly part
      var latest = GetLatestOrNull(opportunityId) ?? throw notFound;

      var now = DateTimeOffset.UtcNow;
      if (latest.DateAssessed < now.AddDays(-_options.CardMaxAgeInDays)) throw notFound;

      // with its child items, because the fingerprint covers them
      var opportunity = _opportunityService.GetByIdOrNull(opportunityId, true, false, false) ?? throw notFound;
      if (!IsPubliclyVisible(opportunity)) throw notFound;
      if (!IsCurrent(latest, opportunity, now)) throw notFound;

      return latest.ToAssessment().LearnerCard;
    }

    public List<Guid>? ListVerifiedForBrowse()
    {
      if (!_options.ShowInBrowse) return null;

      var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.CardMaxAgeInDays);
      return [.. _assessmentRepository.Query()
        .Where(o => o.IsLatest && o.Status == CourseQualityStatus.Verified && o.DateAssessed >= cutoff)
        .Select(o => o.OpportunityId)];
    }

    public void SetBrowseStatus(List<Opportunity.Models.Opportunity>? opportunities, List<Opportunity.Models.OpportunityInfo>? results)
    {
      if (!_options.ShowInBrowse || opportunities == null || results == null) return;

      var courses = opportunities.Where(o => o.Type == Opportunity.Type.Learning).ToDictionary(o => o.Id);
      if (courses.Count == 0) return;

      var ids = courses.Keys.ToList();
      var latest = _assessmentRepository.Query().Where(o => o.IsLatest && ids.Contains(o.OpportunityId)).ToDictionary(o => o.OpportunityId);

      var now = DateTimeOffset.UtcNow;
      foreach (var result in results)
      {
        if (!courses.TryGetValue(result.Id, out var course)) continue;

        var current = latest.TryGetValue(result.Id, out var assessment)
          && assessment.DateAssessed >= now.AddDays(-_options.CardMaxAgeInDays) && IsCurrent(assessment, course, now);

        result.QualityStatus = current ? assessment!.Status : CourseQualityStatus.NeedsReview;
        result.LearnerCard = current ? assessment!.ToAssessment().LearnerCard : null;
      }
    }

    public async Task<CourseQualityAssessment> Assess(Guid opportunityId, CancellationToken cancellationToken)
    {
      if (opportunityId == Guid.Empty) throw new ArgumentNullException(nameof(opportunityId));

      if (!_options.Enabled)
        throw new ValidationException("Course quality assessment is turned off");

      var opportunity = _opportunityService.GetById(opportunityId, true, false, false);
      if (opportunity.Type != Opportunity.Type.Learning)
        throw new ValidationException($"Only opportunities of type '{Opportunity.Type.Learning}' can be assessed");

      var link = await _linkChecker.Check(opportunity.URL, cancellationToken);
      var ai = await EvaluateAI(opportunity, link, cancellationToken);

      var assessment = CourseQualityEngine.Assess(opportunity, link, ai, _options, DateTimeOffset.UtcNow, out var fresh);

      await Persist(assessment, opportunity.DateModified, CourseQualityFingerprint.Compute(opportunity), fresh);

      return assessment;
    }

    public CourseQualityReviewSearchResults SearchReviews(CourseQualityReviewSearchFilter filter)
    {
      ArgumentNullException.ThrowIfNull(filter, nameof(filter));

      if (filter.PageNumber < 1) throw new ValidationException("Page number must be 1 or greater");
      if (filter.PageSize < 1 || filter.PageSize > MaxPageSize) throw new ValidationException($"Page size must be between 1 and {MaxPageSize}");

      var query = _assessmentRepository.Query().Where(o => o.IsLatest);

      if (filter.ReviewStatus.HasValue) query = query.Where(o => o.ReviewStatus == filter.ReviewStatus.Value);
      if (filter.OrganizationId.HasValue) query = query.Where(o => o.OrganizationId == filter.OrganizationId.Value);

      var totalCount = query.Count();

      // oldest first, so the queue is worked in the order it arrived
      var items = query.OrderBy(o => o.DateAssessed).ThenBy(o => o.Id)
        .Skip((filter.PageNumber - 1) * filter.PageSize)
        .Take(filter.PageSize)
        .ToList();

      return new CourseQualityReviewSearchResults
      {
        TotalCount = totalCount,
        Items = [.. items.Select(o => o.ToAssessment())]
      };
    }

    public async Task<CourseQualityAssessment> Review(Guid assessmentId, CourseQualityReviewDecision decision)
    {
      if (assessmentId == Guid.Empty) throw new ArgumentNullException(nameof(assessmentId));
      ArgumentNullException.ThrowIfNull(decision, nameof(decision));

      if (decision.Status == CourseQualityStatus.NeedsReview)
        throw new ValidationException($"A review must resolve to '{CourseQualityStatus.Verified}' or '{CourseQualityStatus.NotVerified}'");

      if (decision.Format == CourseFormat.Unknown)
        throw new ValidationException($"A format must be '{CourseFormat.Course}' or '{CourseFormat.Webinar}'");

      if (decision.Note?.Length > MaxReviewNoteLength)
        throw new ValidationException($"The review note must be {MaxReviewNoteLength} characters or fewer");

      var reviewerEmail = HttpContextAccessorHelper.GetUsername(_httpContextAccessor, false);
      CourseQualityAssessment assessment = null!;

      await _executionStrategyService.ExecuteInExecutionStrategyAsync(async () =>
      {
        using var scope = TransactionScopeHelper.CreateReadCommitted();

        var record = _assessmentRepository.Query(LockMode.Wait).SingleOrDefault(o => o.Id == assessmentId)
          ?? throw new EntityNotFoundException($"Course quality assessment with id '{assessmentId}' does not exist");

        if (!record.IsLatest)
          throw new ValidationException("This assessment has been replaced by a newer one. Please review the latest assessment");

        assessment = record.ToAssessment();

        if (decision.Status == CourseQualityStatus.Verified && (decision.Format ?? assessment.LearnerCard.Format) == CourseFormat.Unknown)
          throw new ValidationException("Set the format to verify this listing");

        CourseQualityReviewPolicy.ApplyDecision(assessment, decision, reviewerEmail, DateTimeOffset.UtcNow, _options);

        await _assessmentRepository.Update(record.ApplyAssessment(assessment));

        scope.Complete();
      });

      return assessment;
    }

    public List<PartnerFixNote> ListFixNotes(Guid organizationId)
    {
      if (organizationId == Guid.Empty) throw new ArgumentNullException(nameof(organizationId));

      _organizationService.IsAdmin(organizationId, true);

      var records = _assessmentRepository.Query()
        .Where(o => o.IsLatest && o.OrganizationId == organizationId && o.FixNote != null)
        .OrderByDescending(o => o.DateAssessed).ThenBy(o => o.Id)
        .ToList();

      return [.. records.Select(o => o.ToAssessment().FixNote!)];
    }

    public CourseQualityCoverage GetCoverage()
    {
      var courseIds = QueryPublishedCourses().Select(o => o.Id);
      var totalCourses = courseIds.Count();

      var latest = _assessmentRepository.Query()
        .Where(o => o.IsLatest && courseIds.Contains(o.OpportunityId))
        .Select(o => new { o.Format, o.DatesSuspect, o.CardReady })
        .ToList();

      return new CourseQualityCoverage
      {
        TotalCourses = totalCourses,
        Assessed = latest.Count,
        NotAssessed = totalCourses - latest.Count,
        MissingOrSuspectDates = latest.Count(o => o.DatesSuspect),
        Course = latest.Count(o => o.Format == CourseFormat.Course),
        Webinar = latest.Count(o => o.Format == CourseFormat.Webinar),
        Unknown = latest.Count(o => o.Format == CourseFormat.Unknown),
        CardReady = latest.Count(o => o.CardReady)
      };
    }

    public IQueryable<Guid> QueryDue(DateTimeOffset now)
    {
      var cutoff = now.AddDays(-_options.ReassessIntervalInDays);
      var rubricVersion = _options.RubricVersion;

      var latest = _assessmentRepository.Query().Where(o => o.IsLatest);
      var current = latest.Where(o => o.DateAssessed >= cutoff && o.RubricVersion == rubricVersion);

      // never assessed first, then the oldest check, so the whole catalogue keeps moving
      return QueryPublishedCourses()
        .Where(o => !current.Any(a => a.OpportunityId == o.Id && a.OpportunityDateModified == o.DateModified))
        .OrderBy(o => latest.Any(a => a.OpportunityId == o.Id) ? 1 : 0)
        .ThenBy(o => latest.Where(a => a.OpportunityId == o.Id).Select(a => a.DateAssessed).FirstOrDefault())
        .ThenBy(o => o.Id)
        .Select(o => o.Id);
    }
    #endregion

    #region Private Members
    // a badge speaks for what was assessed: not for a listing edited since, or a time that has gone by
    private static bool IsCurrent(CourseQualityAssessmentRecord latest, Opportunity.Models.Opportunity opportunity, DateTimeOffset now)
    {
      if (latest.ContentFingerprint != CourseQualityFingerprint.Compute(opportunity)) return false;
      if (opportunity.DateEnd.HasValue && opportunity.DateEnd.Value < now) return false;
      return latest.Format != CourseFormat.Webinar || opportunity.DateEnd.HasValue || opportunity.DateStart >= now;
    }

    private static bool IsPubliclyVisible(Opportunity.Models.Opportunity opportunity)
    {
      return opportunity.Type == Opportunity.Type.Learning
        && opportunity.Status == Status.Active
        && opportunity.OrganizationStatus == OrganizationStatus.Active
        && opportunity.Hidden != true;
    }

    private IQueryable<Opportunity.Models.Opportunity> QueryPublishedCourses()
    {
      var typeLearningId = _opportunityTypeService.GetByName(Opportunity.Type.Learning.ToString()).Id;
      var statusActiveId = _opportunityStatusService.GetByName(Status.Active.ToString()).Id;
      var organizationStatusActiveId = _organizationStatusService.GetByName(OrganizationStatus.Active.ToString()).Id;

      return _opportunityRepository.Query().Where(o => o.TypeId == typeLearningId
        && o.StatusId == statusActiveId
        && o.OrganizationStatusId == organizationStatusActiveId
        && (!o.Hidden.HasValue || o.Hidden == false));
    }

    private CourseQualityAssessmentRecord? GetLatestOrNull(Guid opportunityId)
    {
      return _assessmentRepository.Query().SingleOrDefault(o => o.OpportunityId == opportunityId && o.IsLatest);
    }

    private async Task<CourseQualityAIResult?> EvaluateAI(Opportunity.Models.Opportunity opportunity, CourseQualityLinkCheckResult link, CancellationToken cancellationToken)
    {
      if (!_aiProvider.IsEnabled) return null;

      try
      {
        return await _aiProvider.Evaluate(new CourseQualityAIRequest
        {
          Title = opportunity.Title,
          Description = opportunity.Description,
          Provider = string.IsNullOrWhiteSpace(opportunity.Provider) ? opportunity.OrganizationName : opportunity.Provider,
          OrganizationName = opportunity.OrganizationName,
          URL = opportunity.URL,
          DateStart = opportunity.DateStart,
          DateEnd = opportunity.DateEnd,
          CommitmentHours = opportunity.CommitmentIntervalTotalHours,
          SourcePageText = link.SourcePageText,
          SourcePageSignals = link.SourcePageSignals,
          SourcePageFinalUrl = link.FinalUrl,
          SourcePageTruncated = link.SourcePageTruncated
        }, cancellationToken);
      }
      catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
      {
        // a provider failure means rules alone, never a pass
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Course quality AI evaluation failed for opportunity {opportunityId}", opportunity.Id);
        return null;
      }
    }

    /// <summary>
    /// The newest row is locked and read only here, after the slow link and AI calls, so a decision made while they
    /// were running is not lost.
    /// </summary>
    private async Task Persist(CourseQualityAssessment assessment, DateTimeOffset opportunityDateModified, string contentFingerprint, CourseQualityFreshCheck fresh)
    {
      await _executionStrategyService.ExecuteInExecutionStrategyAsync(async () =>
      {
        using var scope = TransactionScopeHelper.CreateReadCommitted();

        var prior = _assessmentRepository.Query(LockMode.Wait).SingleOrDefault(o => o.OpportunityId == assessment.OpportunityId && o.IsLatest);

        if (CourseQualityReviewPolicy.ShouldCarry(prior, contentFingerprint, fresh, _options, DateTimeOffset.UtcNow))
          CourseQualityReviewPolicy.CarryDecision(assessment, prior!.ToAssessment(), _options);

        var backed = prior != null && CourseQualityReviewPolicy.Backs(prior, fresh);
        var carried = assessment.ReviewStatus is CourseQualityReviewStatus.Confirmed or CourseQualityReviewStatus.Overridden;

        if (prior != null)
        {
          prior.IsLatest = false;
          await _assessmentRepository.Update(prior);
        }

        var record = assessment.ToRecord(opportunityDateModified, contentFingerprint);

        // a decision only being held has not seen that host prove anything
        record.FinalHost = carried && !backed ? prior!.FinalHost : fresh.FinalHost;
        record.DateConfirmed = !carried ? null : backed ? DateTimeOffset.UtcNow : prior!.DateConfirmed ?? prior.DateReviewed;

        await _assessmentRepository.Create(record);

        scope.Complete();
      });
    }
    #endregion
  }
}
