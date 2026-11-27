using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Helpers;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Lookups.Interfaces;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Models;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Grades the whole catalogue in one pass in memory, because grades are relative, then writes only the rows that changed.
  /// Runs one at a time behind a distributed lock, so a reviewer decision and a batch cannot write over each other.
  /// </summary>
  public class CourseQualityGradingService : ICourseQualityGradingService
  {
    #region Class Variables
    private const string LockIdentifier = "course_quality_grading";
    private const string PendingIdentifier = "course_quality_grading_pending";
    private const int GradeRankNone = 5;
    private static readonly TimeSpan PendingBuffer = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);

    private readonly ILogger<CourseQualityGradingService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CourseQualityOptions _options;
    private readonly IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> _opportunityRepository;
    private readonly IRepository<OpportunitySkill> _opportunitySkillRepository;
    private readonly IRepository<OpportunityLanguage> _opportunityLanguageRepository;
    private readonly ILanguageService _languageService;
    private readonly IMyOpportunityService _myOpportunityService;
    private readonly IRepository<CourseQualityAssessmentRecord> _assessmentRepository;
    private readonly IRepositoryBatched<CourseQualityGradeRecord> _gradeRepository;
    private readonly IRepository<CourseQualityGradeRubricRecord> _rubricRepository;
    private readonly IRepository<CourseQualityLearnerFeedbackRecord> _feedbackRepository;
    private readonly IDistributedLockService _distributedLockService;
    private readonly IExecutionStrategyService _executionStrategyService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IPathwayOutcomeService _outcomeService;
    #endregion

    #region Constructor
    public CourseQualityGradingService(ILogger<CourseQualityGradingService> logger,
      IHttpContextAccessor httpContextAccessor,
      IOptions<CourseQualityOptions> options,
      IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> opportunityRepository,
      IRepository<OpportunitySkill> opportunitySkillRepository,
      IRepository<OpportunityLanguage> opportunityLanguageRepository,
      ILanguageService languageService,
      IMyOpportunityService myOpportunityService,
      IRepository<CourseQualityAssessmentRecord> assessmentRepository,
      IRepositoryBatched<CourseQualityGradeRecord> gradeRepository,
      IRepository<CourseQualityGradeRubricRecord> rubricRepository,
      IRepository<CourseQualityLearnerFeedbackRecord> feedbackRepository,
      IDistributedLockService distributedLockService,
      IExecutionStrategyService executionStrategyService,
      IBackgroundJobClient backgroundJobClient,
      IPathwayOutcomeService outcomeService)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
      _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
      _opportunityRepository = opportunityRepository ?? throw new ArgumentNullException(nameof(opportunityRepository));
      _opportunitySkillRepository = opportunitySkillRepository ?? throw new ArgumentNullException(nameof(opportunitySkillRepository));
      _opportunityLanguageRepository = opportunityLanguageRepository ?? throw new ArgumentNullException(nameof(opportunityLanguageRepository));
      _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
      _myOpportunityService = myOpportunityService ?? throw new ArgumentNullException(nameof(myOpportunityService));
      _assessmentRepository = assessmentRepository ?? throw new ArgumentNullException(nameof(assessmentRepository));
      _gradeRepository = gradeRepository ?? throw new ArgumentNullException(nameof(gradeRepository));
      _rubricRepository = rubricRepository ?? throw new ArgumentNullException(nameof(rubricRepository));
      _feedbackRepository = feedbackRepository ?? throw new ArgumentNullException(nameof(feedbackRepository));
      _distributedLockService = distributedLockService ?? throw new ArgumentNullException(nameof(distributedLockService));
      _executionStrategyService = executionStrategyService ?? throw new ArgumentNullException(nameof(executionStrategyService));
      _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
      _outcomeService = outcomeService ?? throw new ArgumentNullException(nameof(outcomeService));
    }
    #endregion

    #region Public Members
    public CourseGradeRubric GetRubric()
    {
      var saved = GetLatestRubricOrNull();
      if (saved?.Rubric == null) return CourseQualityGrading.Configured(_options);

      var rubric = CourseQualityGrading.WithNewFactors(CourseQualityGrading.Deserialize(saved.Rubric));
      if (CourseQualityGrading.Validate(rubric).Count == 0) return CourseQualityGrading.Normalize(rubric);

      // a stored override that no longer passes the rules must not stop grading; configuration takes over
      if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning("Saved course grade rubric {rubricId} is not valid; using configuration", saved.Id);
      return CourseQualityGrading.Configured(_options);
    }

    public CourseGradeRubricState GetRubricState()
    {
      var saved = GetLatestRubricOrNull();

      // the rubric page is the course rubric, so its distribution counts courses only
      var jobIds = QueryJobIds();
      var counts = _gradeRepository.Query().Where(o => !jobIds.Contains(o.OpportunityId)).GroupBy(o => o.Grade).Select(o => new { Grade = o.Key, Count = o.Count() }).ToDictionary(o => o.Grade, o => o.Count);

      return new CourseGradeRubricState
      {
        Rubric = GetRubric(),
        IsDefault = saved?.Rubric == null,
        UpdatedByEmail = saved?.UpdatedByEmail,
        DateUpdated = saved?.DateCreated,
        Distribution = Enum.GetValues<CourseGrade>().ToDictionary(o => o, o => counts.GetValueOrDefault(o))
      };
    }

    public async Task<CourseGradeRubricState> SaveRubric(CourseGradeRubric rubric)
    {
      var errors = CourseQualityGrading.Validate(rubric);
      if (errors.Count > 0) throw new ValidationException(string.Join(". ", errors));

      var normalized = CourseQualityGrading.Normalize(rubric!);

      // graded once on today's catalogue before it is kept, so a rubric the rules allow but the arithmetic does not is never saved
      var inputs = LoadInputs();
      try
      {
        CourseQualityGradingEngine.Grade(inputs, normalized, DateTimeOffset.UtcNow);
      }
      catch (Exception ex) when (ex is ArithmeticException or ArgumentException or InvalidOperationException)
      {
        throw new ValidationException($"This rubric cannot grade the current catalogue: {ex.Message}");
      }

      await SaveRubricRecord(CourseQualityGrading.Serialize(normalized));

      await Regrade();
      return GetRubricState();
    }

    public async Task<CourseGradeRubricState> ResetRubric()
    {
      if (GetLatestRubricOrNull()?.Rubric != null) await SaveRubricRecord(null);

      await Regrade();
      return GetRubricState();
    }

    public async Task<CourseGradeRubricState> Run()
    {
      await Regrade();
      return GetRubricState();
    }

    public async Task RequestRegrade()
    {
      try
      {
        var delay = TimeSpan.FromSeconds(Math.Max(_options.RegradeDelayInSeconds, 1));

        // one pending run at a time: every change until it starts is graded by it
        if (!await _distributedLockService.TryAcquireLockAsync(PendingIdentifier, delay + PendingBuffer)) return;

        try
        {
          _backgroundJobClient.Schedule<ICourseQualityBackgroundService>(o => o.ProcessRegrade(), delay);
        }
        catch
        {
          await _distributedLockService.ReleaseLockAsync(PendingIdentifier);
          throw;
        }
      }
      catch (Exception ex)
      {
        // the change is saved; its grade follows the next requested or on-demand run
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Failed to schedule a course regrade: {errorMessage}", ex.Message);
      }
    }

    public async Task RunRequestedRegrade()
    {
      // cleared before the catalogue is read, so a change made during this run asks for the next one
      await _distributedLockService.ReleaseLockAsync(PendingIdentifier);
      await Regrade();
    }

    public async Task Regrade()
    {
      await _distributedLockService.RunWithLockAsync(LockIdentifier, LockDuration, async () =>
      {
        var now = DateTimeOffset.UtcNow;
        var inputs = LoadInputs();
        var results = CourseQualityGradingEngine.Grade(inputs, GetRubric(), now);

        // status and scored answers per graded listing, which the stored row keeps beside the grade
        var byId = inputs.ToDictionary(o => o.OpportunityId, o => (o.Status, Responses: CourseQualityGradingEngine.ScoredResponses(o.Feedback)));

        // jobs are ranked only against jobs, with their own rubric, in the same run so neither catalogue deletes the other's rows
        if (_options.Jobs.Enabled)
        {
          var jobInputs = LoadJobInputs();
          results.AddRange(CourseQualityJobGradingEngine.Grade(jobInputs, CourseQualityJobGradingDefaults.Configured(_options), now));
          foreach (var job in jobInputs) byId[job.OpportunityId] = (job.Status, CourseQualityGradingEngine.ScoredResponses(job.Feedback));
        }

        var existing = _gradeRepository.Query().ToList().ToDictionary(o => o.OpportunityId);

        var creates = new List<CourseQualityGradeRecord>();
        var updates = new List<CourseQualityGradeRecord>();
        foreach (var result in results)
        {
          var (status, responses) = byId[result.OpportunityId];

          if (!existing.TryGetValue(result.OpportunityId, out var record))
          {
            creates.Add(new CourseQualityGradeRecord { Id = Guid.NewGuid() }.ApplyResult(result, status, responses));
            continue;
          }

          // the date moves only with the grade, so an unchanged course costs no write
          result.DateGraded = record.DateGraded;
          if (record.Differs(result, status, responses))
          {
            result.DateGraded = now;
            updates.Add(record.ApplyResult(result, status, responses));
          }
        }

        // a course that is no longer published or assessed has no grade
        var deletes = existing.Values.Where(o => !byId.ContainsKey(o.OpportunityId)).ToList();

        await _executionStrategyService.ExecuteInExecutionStrategyAsync(async () =>
        {
          using var scope = TransactionScopeHelper.CreateReadCommitted();

          if (deletes.Count > 0) await _gradeRepository.Delete(deletes);
          if (creates.Count > 0) await _gradeRepository.Create(creates);
          if (updates.Count > 0) await _gradeRepository.Update(updates);

          scope.Complete();
        });

        if (_logger.IsEnabled(LogLevel.Information))
          _logger.LogInformation("Graded {count} course(s): {created} new, {updated} changed, {deleted} removed", results.Count, creates.Count, updates.Count, deletes.Count);
      });
    }

    public bool HasUngradedFeedback()
    {
      return QueryUngradedFeedback(_feedbackRepository.Query(), _gradeRepository.Query()).Any();
    }

    /// <summary>
    /// Courses with a Taken answer newer than their stored grade. A new answer always changes the stored grade (its response count),
    /// so the flag clears after one regrade; an edit that leaves the grade unchanged costs at most one regrade per batch.
    /// </summary>
    public static IQueryable<Guid> QueryUngradedFeedback(IQueryable<CourseQualityLearnerFeedbackRecord> feedback, IQueryable<CourseQualityGradeRecord> grades)
    {
      ArgumentNullException.ThrowIfNull(feedback, nameof(feedback));
      ArgumentNullException.ThrowIfNull(grades, nameof(grades));

      return feedback.Where(o => o.Answer == CourseFeedbackAnswer.Taken && grades.Any(g => g.OpportunityId == o.OpportunityId && g.DateGraded < o.DateModified))
        .Select(o => o.OpportunityId);
    }

    public List<CourseGradeFactorRow> ListFactors()
    {
      // the rubric page regrades these rows in the browser with the course rubric, so jobs stay out
      var jobIds = QueryJobIds();
      return [.. _gradeRepository.Query().Where(o => !jobIds.Contains(o.OpportunityId)).OrderByDescending(o => o.Score).ThenBy(o => o.OpportunityId).ToList().Select(o => o.ToFactorRow())];
    }

    public CourseGradeResult GetGrade(Guid opportunityId)
    {
      if (opportunityId == Guid.Empty) throw new ArgumentNullException(nameof(opportunityId));

      var record = _gradeRepository.Query().SingleOrDefault(o => o.OpportunityId == opportunityId)
        ?? throw new EntityNotFoundException($"Course grade for opportunity with id '{opportunityId}' does not exist");

      return record.ToResult();
    }

    public Dictionary<Guid, CourseGradeResult> ListGrades(List<Guid> opportunityIds)
    {
      ArgumentNullException.ThrowIfNull(opportunityIds, nameof(opportunityIds));
      if (opportunityIds.Count == 0) return [];

      var ids = opportunityIds.Distinct().ToList();
      return _gradeRepository.Query().Where(o => ids.Contains(o.OpportunityId)).ToList().ToDictionary(o => o.OpportunityId, o => o.ToResult());
    }

    public Expression<Func<Opportunity.Models.Opportunity, object>> GradeRank()
    {
      var grades = _gradeRepository.Query();

      // job grades rank job search only once jobs are shown in browse
      if (!(_options.Jobs.Enabled && _options.Jobs.ShowInBrowse))
      {
        var jobIds = QueryJobIds();
        grades = grades.Where(o => !jobIds.Contains(o.OpportunityId));
      }

      // a correlated subquery, so search joins the grade table in SQL rather than sending every graded id as a parameter
      return opportunity => grades.Where(g => g.OpportunityId == opportunity.Id)
        .Select(g => (int?)(g.Grade == CourseGrade.A ? 1 : g.Grade == CourseGrade.B ? 2 : g.Grade == CourseGrade.C ? 3 : g.Grade == CourseGrade.D ? 4 : GradeRankNone))
        .FirstOrDefault() ?? GradeRankNone;
    }

    public IQueryable<Guid> QueryByGrade(CourseGrade grade)
    {
      return _gradeRepository.Query().Where(o => o.Grade == grade).Select(o => o.OpportunityId);
    }

    public IQueryable<Guid> QueryGraded()
    {
      return _gradeRepository.Query().Where(o => o.Grade != CourseGrade.Ungraded).Select(o => o.OpportunityId);
    }
    #endregion

    #region Private Members
    private CourseQualityGradeRubricRecord? GetLatestRubricOrNull()
    {
      return _rubricRepository.Query().OrderByDescending(o => o.DateCreated).ThenByDescending(o => o.Id).FirstOrDefault();
    }

    private async Task SaveRubricRecord(string? rubric)
    {
      await _rubricRepository.Create(new CourseQualityGradeRubricRecord
      {
        Id = Guid.NewGuid(),
        Rubric = rubric,
        UpdatedByEmail = HttpContextAccessorHelper.GetUsername(_httpContextAccessor, false),
        DateCreated = DateTimeOffset.UtcNow
      });
    }

    /// <summary>
    /// The latest assessment of every publicly visible course, with what its listing, its provider and its learners add.
    /// Each source is read once for the whole catalogue.
    /// </summary>
    private List<CourseGradeInput> LoadInputs()
    {
      var latest = _assessmentRepository.Query().Where(o => o.IsLatest).ToList();
      var assessedIds = latest.Select(o => o.OpportunityId).ToList();

      var opportunities = _opportunityRepository.Query().Where(o => assessedIds.Contains(o.Id)).ToList()
        .Where(IsPubliclyVisible).ToDictionary(o => o.Id);
      var ids = opportunities.Keys.ToList();

      var skills = _opportunitySkillRepository.Query().Where(o => ids.Contains(o.OpportunityId))
        .GroupBy(o => o.OpportunityId).Select(o => new { OpportunityId = o.Key, Count = o.Count() })
        .ToDictionary(o => o.OpportunityId, o => o.Count);

      var languageNames = _languageService.List().ToDictionary(o => o.Id, o => o.Name);
      var languages = _opportunityLanguageRepository.Query().Where(o => ids.Contains(o.OpportunityId))
        .Select(o => new { o.OpportunityId, o.LanguageId }).ToList()
        .GroupBy(o => o.OpportunityId)
        .ToDictionary(o => o.Key, o => o.Select(l => languageNames.GetValueOrDefault(l.LanguageId)).Where(l => l != null).Select(l => l!).ToList());

      var participation = _myOpportunityService.ListParticipationCounts().ToDictionary(o => o.OpportunityId);

      var feedback = _feedbackRepository.Query().Where(o => o.Answer == CourseFeedbackAnswer.Taken && ids.Contains(o.OpportunityId))
        .Select(o => new { o.OpportunityId, o.Rating, o.WouldRecommend }).ToList()
        .GroupBy(o => o.OpportunityId)
        .ToDictionary(o => o.Key, o => o.Select(f => new CourseGradeFeedbackAnswer(f.Rating, f.WouldRecommend)).ToList());

      var outcomes = GetOutcomesOrNull()?.Courses ?? [];

      return [.. latest.Where(o => opportunities.ContainsKey(o.OpportunityId)).Select(record =>
      {
        var opportunity = opportunities[record.OpportunityId];
        var assessment = record.ToAssessment();
        var counts = participation.GetValueOrDefault(record.OpportunityId);
        var outcome = outcomes.GetValueOrDefault(record.OpportunityId);

        return new CourseGradeInput
        {
          OpportunityId = opportunity.Id,
          OpportunityTitle = opportunity.Title,
          OrganizationId = opportunity.OrganizationId,
          OrganizationName = opportunity.OrganizationName,
          Status = record.Status,
          Confidence = record.Confidence,
          Criteria = assessment.Criteria,
          CostType = assessment.LearnerCard.Cost?.Type ?? CourseCostType.Unknown,
          SelfPaced = assessment.LearnerCard.SelfPaced,
          PageEvidence = record.ToPageEvidence(),
          Description = opportunity.Description,
          SkillCount = skills.GetValueOrDefault(opportunity.Id),
          Languages = languages.GetValueOrDefault(opportunity.Id) ?? [],
          CommitmentInterval = opportunity.CommitmentInterval,
          CommitmentIntervalCount = opportunity.CommitmentIntervalCount,
          DateCreated = opportunity.DateCreated,
          Completed = counts?.Completed ?? 0,
          Started = counts?.Started ?? 0,
          Feedback = feedback.GetValueOrDefault(opportunity.Id) ?? [],
          OutcomeCompleters = outcome?.Completers ?? 0,
          ProgressionRate = outcome?.ProgressionRate,
          EmploymentRate = outcome?.EmploymentRate
        };
      })];
    }

    /// <summary>
    /// The Outcomes factor is neutral without data, so a failure to work outcomes out must not stop grading.
    /// </summary>
    private PathwayOutcomeSnapshot? GetOutcomesOrNull()
    {
      try
      {
        return _outcomeService.GetSnapshot();
      }
      catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
      {
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Failed to work out course outcomes for grading: {errorMessage}", ex.Message);
        return null;
      }
    }

    /// <summary>
    /// The latest assessment of every publicly visible job, with what its listing states. Listings load with their custom
    /// fields, which carry the pay, schedule and experience a feed sent.
    /// </summary>
    private List<JobGradeInput> LoadJobInputs()
    {
      var latest = _assessmentRepository.Query().Where(o => o.IsLatest && o.Format == CourseFormat.Job).ToList();
      var assessedIds = latest.Select(o => o.OpportunityId).ToList();

      var opportunities = _opportunityRepository.Query(true).Where(o => assessedIds.Contains(o.Id)).ToList()
        .Where(o => IsPubliclyVisible(o, Opportunity.Type.Job)).ToDictionary(o => o.Id);

      return [.. latest.Where(o => opportunities.ContainsKey(o.OpportunityId)).Select(record =>
      {
        var opportunity = opportunities[record.OpportunityId];

        return new JobGradeInput
        {
          OpportunityId = opportunity.Id,
          OpportunityTitle = opportunity.Title,
          OrganizationId = opportunity.OrganizationId,
          OrganizationName = opportunity.OrganizationName,
          Status = record.Status,
          Criteria = record.ToAssessment().Criteria,
          Facts = CourseQualityJobRules.Facts(opportunity)
        };
      })];
    }

    private IQueryable<Guid> QueryJobIds()
    {
      return _assessmentRepository.Query().Where(o => o.IsLatest && o.Format == CourseFormat.Job).Select(o => o.OpportunityId);
    }

    private static bool IsPubliclyVisible(Opportunity.Models.Opportunity opportunity) => IsPubliclyVisible(opportunity, Opportunity.Type.Learning);

    private static bool IsPubliclyVisible(Opportunity.Models.Opportunity opportunity, Opportunity.Type type)
    {
      return opportunity.Type == type
        && opportunity.Status == Status.Active
        && opportunity.OrganizationStatus == OrganizationStatus.Active
        && opportunity.Hidden != true;
    }
    #endregion
  }
}
