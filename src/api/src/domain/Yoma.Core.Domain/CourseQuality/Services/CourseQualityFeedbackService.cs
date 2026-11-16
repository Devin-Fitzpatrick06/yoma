using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Helpers;
using Yoma.Core.Domain.Core.Interfaces;
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
  /// <summary>
  /// One row per learner and course: "left" opens it, the prompt asks once it is due, and the answer closes it. Taken answers feed the
  /// LearnerFeedback grade factor on the next batch regrade. Comment text is never logged and is returned to Admins only.
  /// </summary>
  public class CourseQualityFeedbackService : ICourseQualityFeedbackService
  {
    #region Class Variables
    private const int MaxRecentComments = 10;
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);

    private readonly ILogger<CourseQualityFeedbackService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CourseQualityLearnerFeedbackOptions _options;
    private readonly IUserService _userService;
    private readonly IOpportunityService _opportunityService;
    private readonly IOrganizationService _organizationService;
    private readonly IOpportunityTypeService _opportunityTypeService;
    private readonly IOpportunityStatusService _opportunityStatusService;
    private readonly IOrganizationStatusService _organizationStatusService;
    private readonly IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> _opportunityRepository;
    private readonly IRepository<CourseQualityAssessmentRecord> _assessmentRepository;
    private readonly IRepository<CourseQualityLearnerFeedbackRecord> _feedbackRepository;
    private readonly ICourseQualityGradingService _gradingService;
    private readonly IDistributedLockService _distributedLockService;
    #endregion

    #region Constructor
    public CourseQualityFeedbackService(ILogger<CourseQualityFeedbackService> logger,
      IHttpContextAccessor httpContextAccessor,
      IOptions<CourseQualityOptions> options,
      IUserService userService,
      IOpportunityService opportunityService,
      IOrganizationService organizationService,
      IOpportunityTypeService opportunityTypeService,
      IOpportunityStatusService opportunityStatusService,
      IOrganizationStatusService organizationStatusService,
      IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> opportunityRepository,
      IRepository<CourseQualityAssessmentRecord> assessmentRepository,
      IRepository<CourseQualityLearnerFeedbackRecord> feedbackRepository,
      ICourseQualityGradingService gradingService,
      IDistributedLockService distributedLockService)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
      _options = options?.Value?.LearnerFeedback ?? throw new ArgumentNullException(nameof(options));
      _userService = userService ?? throw new ArgumentNullException(nameof(userService));
      _opportunityService = opportunityService ?? throw new ArgumentNullException(nameof(opportunityService));
      _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
      _opportunityTypeService = opportunityTypeService ?? throw new ArgumentNullException(nameof(opportunityTypeService));
      _opportunityStatusService = opportunityStatusService ?? throw new ArgumentNullException(nameof(opportunityStatusService));
      _organizationStatusService = organizationStatusService ?? throw new ArgumentNullException(nameof(organizationStatusService));
      _opportunityRepository = opportunityRepository ?? throw new ArgumentNullException(nameof(opportunityRepository));
      _assessmentRepository = assessmentRepository ?? throw new ArgumentNullException(nameof(assessmentRepository));
      _feedbackRepository = feedbackRepository ?? throw new ArgumentNullException(nameof(feedbackRepository));
      _gradingService = gradingService ?? throw new ArgumentNullException(nameof(gradingService));
      _distributedLockService = distributedLockService ?? throw new ArgumentNullException(nameof(distributedLockService));
    }
    #endregion

    #region Public Members
    public CourseFeedbackOptions GetOptions()
    {
      return new CourseFeedbackOptions { Good = _options.ListGood(), Bad = _options.ListBad() };
    }

    public async Task RecordLeft(Guid opportunityId)
    {
      if (opportunityId == Guid.Empty) throw new ArgumentNullException(nameof(opportunityId));
      if (!_options.Enabled) return;

      GetVisibleCourse(opportunityId);
      var userId = GetUserId();

      await WithRowLock(userId, opportunityId, async () =>
      {
        var now = DateTimeOffset.UtcNow;
        var record = GetRecordOrNull(userId, opportunityId);

        if (record == null)
        {
          await _feedbackRepository.Create(new CourseQualityLearnerFeedbackRecord
          {
            Id = Guid.NewGuid(),
            UserId = userId,
            OpportunityId = opportunityId,
            Answer = CourseFeedbackAnswer.Pending,
            DateLeft = now
          });
          return;
        }

        // an answer for good is never reopened by another visit
        if (record.Answer is not (CourseFeedbackAnswer.Pending or CourseFeedbackAnswer.NotYet)) return;

        record.DateLeft = now;
        await _feedbackRepository.Update(record);
      });
    }

    public async Task<List<CourseFeedbackPrompt>> ListPending()
    {
      if (!_options.Enabled) return [];

      var userId = GetUserId();
      var now = DateTimeOffset.UtcNow;
      var leftBefore = now.AddMinutes(-_options.PromptDelayInMinutes);
      var visible = QueryPublishedCourses().Select(o => o.Id);

      var due = _feedbackRepository.Query()
        .Where(o => o.UserId == userId
          && ((o.Answer == CourseFeedbackAnswer.Pending && o.DateLeft <= leftBefore)
            || (o.Answer == CourseFeedbackAnswer.NotYet && o.SnoozeUntil.HasValue && o.SnoozeUntil <= now))
          && visible.Contains(o.OpportunityId))
        .OrderBy(o => o.DateLeft).ThenBy(o => o.Id)
        .FirstOrDefault();

      if (due == null) return [];

      // with computed fields, for the organisation's logo
      var opportunity = _opportunityService.GetByIdOrNull(due.OpportunityId, false, true, false);
      if (opportunity == null) return [];

      due.DateAsked = now;
      await _feedbackRepository.Update(due);

      return
      [
        new CourseFeedbackPrompt
        {
          OpportunityId = opportunity.Id,
          OpportunityTitle = opportunity.Title,
          OrganizationName = opportunity.OrganizationName,
          OrganizationLogoURL = opportunity.OrganizationLogoURL,
          DateLeft = due.DateLeft
        }
      ];
    }

    public async Task Submit(Guid opportunityId, CourseFeedbackSubmission submission)
    {
      if (opportunityId == Guid.Empty) throw new ArgumentNullException(nameof(opportunityId));
      ArgumentNullException.ThrowIfNull(submission, nameof(submission));

      if (!_options.Enabled) throw new ValidationException("Course feedback is turned off");

      var answer = Normalize(submission);

      GetVisibleCourse(opportunityId);
      var userId = GetUserId();

      var saved = false;
      await WithRowLock(userId, opportunityId, async () =>
      {
        var now = DateTimeOffset.UtcNow;
        var record = GetRecordOrNull(userId, opportunityId);

        if (record != null)
        {
          switch (record.Answer)
          {
            case CourseFeedbackAnswer.NotTaken:
            case CourseFeedbackAnswer.Dismissed:
              throw new ValidationException("This course has already been answered");

            case CourseFeedbackAnswer.Taken when answer.Answer != CourseFeedbackAnswer.Taken:
              throw new ValidationException($"A course already answered as '{CourseFeedbackAnswer.Taken}' can only have its survey updated");

            case CourseFeedbackAnswer.Taken when SameAnswers(record, answer):
              return; // nothing changed, so nothing to regrade
          }
        }

        var create = record == null;
        record ??= new CourseQualityLearnerFeedbackRecord { Id = Guid.NewGuid(), UserId = userId, OpportunityId = opportunityId, DateLeft = now };

        Apply(record, answer);
        record.SnoozeUntil = answer.Answer == CourseFeedbackAnswer.NotYet ? now.AddDays(_options.SnoozeInDays) : null;

        if (create) await _feedbackRepository.Create(record);
        else await _feedbackRepository.Update(record);
        saved = true;
      });

      // only a Taken answer feeds the grade; it reaches it within the regrade delay, and a burst of answers shares one run
      if (saved && answer.Answer == CourseFeedbackAnswer.Taken) await _gradingService.RequestRegrade();

      // the opportunity and answer only: the survey, and above all the comment, stay out of the logs
      if (_logger.IsEnabled(LogLevel.Information))
        _logger.LogInformation("Course feedback '{answer}' saved for opportunity {opportunityId}", answer.Answer, opportunityId);
    }

    public CourseFeedbackSummary GetSummary(Guid opportunityId)
    {
      if (opportunityId == Guid.Empty) throw new ArgumentNullException(nameof(opportunityId));

      var opportunity = _opportunityService.GetByIdOrNull(opportunityId, false, false, false)
        ?? throw new EntityNotFoundException($"Opportunity with id '{opportunityId}' does not exist");

      _organizationService.IsAdmin(opportunity.OrganizationId, true);

      return BuildSummaries(new Dictionary<Guid, Guid> { [opportunityId] = opportunity.OrganizationId }).GetValueOrDefault(opportunityId)
        ?? Empty(opportunityId);
    }

    public Dictionary<Guid, CourseFeedbackSummary> ListSummaries(Dictionary<Guid, Guid> organizationIdsByOpportunityId)
    {
      ArgumentNullException.ThrowIfNull(organizationIdsByOpportunityId, nameof(organizationIdsByOpportunityId));
      if (organizationIdsByOpportunityId.Count == 0) return [];

      return BuildSummaries(organizationIdsByOpportunityId);
    }
    #endregion

    #region Private Members
    private Opportunity.Models.Opportunity GetVisibleCourse(Guid opportunityId)
    {
      var opportunity = _opportunityService.GetByIdOrNull(opportunityId, false, false, false)
        ?? throw new EntityNotFoundException($"Opportunity with id '{opportunityId}' does not exist");

      if (opportunity.Type != Opportunity.Type.Learning)
        throw new ValidationException($"Only opportunities of type '{Opportunity.Type.Learning}' take course feedback");

      if (!IsPubliclyVisible(opportunity))
        throw new ValidationException($"Opportunity '{opportunity.Title}' is not published and cannot take course feedback");

      return opportunity;
    }

    private Guid GetUserId()
    {
      return _userService.GetByUsername(HttpContextAccessorHelper.GetUsername(_httpContextAccessor, false), false, false).Id;
    }

    private CourseQualityLearnerFeedbackRecord? GetRecordOrNull(Guid userId, Guid opportunityId)
    {
      return _feedbackRepository.Query().SingleOrDefault(o => o.UserId == userId && o.OpportunityId == opportunityId);
    }

    /// <summary>
    /// One row per learner and course is unique in the database; the lock keeps two quick clicks from both creating it.
    /// </summary>
    private Task WithRowLock(Guid userId, Guid opportunityId, Func<Task> action)
    {
      return _distributedLockService.RunWithLockAsync($"course_quality_feedback:{userId}:{opportunityId}", LockDuration, action);
    }

    /// <summary>
    /// Validates the submission and returns it normalised: tags de-duplicated in their configured spelling, the comment trimmed.
    /// The survey is accepted only with Taken; the web sends nulls and empty lists with every other answer.
    /// </summary>
    private CourseFeedbackSubmission Normalize(CourseFeedbackSubmission submission)
    {
      if (submission.Answer == CourseFeedbackAnswer.Pending || !Enum.IsDefined(submission.Answer))
        throw new ValidationException($"An answer must be '{CourseFeedbackAnswer.Taken}', '{CourseFeedbackAnswer.NotYet}', '{CourseFeedbackAnswer.NotTaken}' or '{CourseFeedbackAnswer.Dismissed}'");

      var good = submission.Good ?? [];
      var bad = submission.Bad ?? [];
      var comment = string.IsNullOrWhiteSpace(submission.Comment) ? null : submission.Comment.Trim();

      if (submission.Answer != CourseFeedbackAnswer.Taken)
      {
        if (submission.Rating.HasValue || submission.Completed.HasValue || submission.WouldRecommend.HasValue || submission.MatchedDescription.HasValue
          || good.Count > 0 || bad.Count > 0 || comment != null)
          throw new ValidationException($"Survey answers are accepted only with '{CourseFeedbackAnswer.Taken}'");

        return new CourseFeedbackSubmission { Answer = submission.Answer };
      }

      if (submission.Rating is < 1 or > 5) throw new ValidationException("A rating must be from 1 to 5");

      if (comment?.Length > _options.CommentMaxLength)
        throw new ValidationException($"A comment must be {_options.CommentMaxLength} characters or fewer");

      return new CourseFeedbackSubmission
      {
        Answer = submission.Answer,
        Rating = submission.Rating,
        Completed = submission.Completed,
        WouldRecommend = submission.WouldRecommend,
        MatchedDescription = submission.MatchedDescription,
        Good = Tags(good, _options.ListGood(), "good"),
        Bad = Tags(bad, _options.ListBad(), "bad"),
        Comment = comment
      };
    }

    private static List<string> Tags(List<string> keys, List<CourseFeedbackTag> allowed, string kind)
    {
      var known = allowed.ToDictionary(o => o.Key, o => o.Key, StringComparer.OrdinalIgnoreCase);

      var result = new List<string>();
      foreach (var key in keys)
      {
        if (string.IsNullOrWhiteSpace(key) || !known.TryGetValue(key.Trim(), out var configured))
          throw new ValidationException($"Unknown {kind} tag '{key}'");

        if (!result.Contains(configured)) result.Add(configured);
      }

      return result;
    }

    private static void Apply(CourseQualityLearnerFeedbackRecord record, CourseFeedbackSubmission answer)
    {
      record.Answer = answer.Answer;
      record.Rating = answer.Rating;
      record.Completed = answer.Completed;
      record.WouldRecommend = answer.WouldRecommend;
      record.MatchedDescription = answer.MatchedDescription;
      record.Good = JsonSerializer.Serialize(answer.Good);
      record.Bad = JsonSerializer.Serialize(answer.Bad);
      record.Comment = answer.Comment;
    }

    private static bool SameAnswers(CourseQualityLearnerFeedbackRecord record, CourseFeedbackSubmission answer)
    {
      return record.Rating == answer.Rating && record.Completed == answer.Completed && record.WouldRecommend == answer.WouldRecommend
        && record.MatchedDescription == answer.MatchedDescription && record.Comment == answer.Comment
        && ReadTags(record.Good).SequenceEqual(answer.Good) && ReadTags(record.Bad).SequenceEqual(answer.Bad);
    }

    private static List<string> ReadTags(string? json)
    {
      if (string.IsNullOrWhiteSpace(json)) return [];

      try
      {
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
      }
      catch (JsonException)
      {
        return [];
      }
    }

    /// <summary>
    /// The LearnerFeedback factor score exactly as a grading run rounds it (0 to 100), put on the rating's scale: 0 is 1 star and 100 is
    /// 5 stars, so AdjustedRating = 1 + score / 25 and score = 25 × (AdjustedRating − 1), with no rounding lost either way.
    /// </summary>
    private static decimal ToRatingScale(decimal adjusted)
    {
      var score = Math.Clamp(Math.Round(adjusted * 100, MidpointRounding.AwayFromZero), 0, 100);
      return 1 + score / 25;
    }

    private static CourseFeedbackSummary Empty(Guid opportunityId) => new() { OpportunityId = opportunityId };

    /// <summary>
    /// Four queries whatever the number of courses: their Taken answers, the rubric, the graded population of their organisations, and
    /// that population's answers, which set each provider's prior exactly as a grading run does.
    /// </summary>
    private Dictionary<Guid, CourseFeedbackSummary> BuildSummaries(Dictionary<Guid, Guid> organizationIdsByOpportunityId)
    {
      var ids = organizationIdsByOpportunityId.Keys.ToList();

      var rows = _feedbackRepository.Query().Where(o => o.Answer == CourseFeedbackAnswer.Taken && ids.Contains(o.OpportunityId)).ToList();
      if (rows.Count == 0) return [];

      var rubric = _gradingService.GetRubric();
      var providerAnswers = ListProviderAnswers([.. organizationIdsByOpportunityId.Where(o => rows.Any(r => r.OpportunityId == o.Key)).Select(o => o.Value).Distinct()]);
      var includeComments = HttpContextAccessorHelper.IsAdminRole(_httpContextAccessor);
      var good = _options.ListGood();
      var bad = _options.ListBad();

      return rows.GroupBy(o => o.OpportunityId).ToDictionary(o => o.Key, o =>
      {
        var answers = o.ToList();
        var ratings = answers.Where(a => a.Rating.HasValue).Select(a => a.Rating!.Value).ToList();

        var adjusted = CourseQualityGradingEngine.AdjustedFeedback(
          answers.Select(a => new CourseGradeFeedbackAnswer(a.Rating, a.WouldRecommend)),
          providerAnswers.GetValueOrDefault(organizationIdsByOpportunityId[o.Key]) ?? [],
          rubric);

        return new CourseFeedbackSummary
        {
          OpportunityId = o.Key,
          Responses = answers.Count,
          Completed = answers.Count(a => a.Completed == true),
          AverageRating = ratings.Count == 0 ? null : Math.Round((decimal)ratings.Sum() / ratings.Count, 2, MidpointRounding.AwayFromZero),
          AdjustedRating = adjusted.HasValue ? ToRatingScale(adjusted.Value) : null,
          WouldRecommendYes = answers.Count(a => a.WouldRecommend == true),
          WouldRecommendNo = answers.Count(a => a.WouldRecommend == false),
          MatchedDescriptionYes = answers.Count(a => a.MatchedDescription == true),
          MatchedDescriptionNo = answers.Count(a => a.MatchedDescription == false),
          Good = CountTags(answers.Select(a => a.Good), good),
          Bad = CountTags(answers.Select(a => a.Bad), bad),
          RecentComments = includeComments
            ? [.. answers.Where(a => !string.IsNullOrWhiteSpace(a.Comment)).OrderByDescending(a => a.DateModified).ThenBy(a => a.Id).Take(MaxRecentComments).Select(a => a.Comment!)]
            : []
        };
      });
    }

    /// <summary>
    /// Configured tags only, most picked first. Tags no longer configured have no label and are left out.
    /// </summary>
    private static List<CourseFeedbackTagCount> CountTags(IEnumerable<string> stored, List<CourseFeedbackTag> configured)
    {
      var counts = stored.SelectMany(o => ReadTags(o).Distinct(StringComparer.OrdinalIgnoreCase))
        .GroupBy(o => o, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(o => o.Key, o => o.Count(), StringComparer.OrdinalIgnoreCase);

      return [.. configured.Select((tag, index) => (Tag: tag, Index: index, Count: counts.GetValueOrDefault(tag.Key)))
        .Where(o => o.Count > 0)
        .OrderByDescending(o => o.Count).ThenBy(o => o.Index)
        .Select(o => new CourseFeedbackTagCount { Key = o.Tag.Key, Label = o.Tag.Label, Count = o.Count })];
    }

    /// <summary>
    /// The Taken answers of every publicly visible course with a latest assessment, by organisation: the population a grading run reads.
    /// </summary>
    private Dictionary<Guid, List<CourseGradeFeedbackAnswer>> ListProviderAnswers(List<Guid> organizationIds)
    {
      if (organizationIds.Count == 0) return [];

      var latest = _assessmentRepository.Query().Where(o => o.IsLatest).Select(o => o.OpportunityId);
      var population = QueryPublishedCourses()
        .Where(o => organizationIds.Contains(o.OrganizationId) && latest.Contains(o.Id))
        .Select(o => new { o.Id, o.OrganizationId })
        .ToList()
        .ToDictionary(o => o.Id, o => o.OrganizationId);

      var populationIds = population.Keys.ToList();
      if (populationIds.Count == 0) return [];

      return _feedbackRepository.Query().Where(o => o.Answer == CourseFeedbackAnswer.Taken && populationIds.Contains(o.OpportunityId))
        .Select(o => new { o.OpportunityId, o.Rating, o.WouldRecommend })
        .ToList()
        .GroupBy(o => population[o.OpportunityId])
        .ToDictionary(o => o.Key, o => o.Select(a => new CourseGradeFeedbackAnswer(a.Rating, a.WouldRecommend)).ToList());
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
    #endregion
  }
}
