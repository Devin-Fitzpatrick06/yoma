using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Helpers;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces.Lookups;
using Yoma.Core.Domain.Entity.Models;
using Yoma.Core.Domain.Lookups.Interfaces;
using Yoma.Core.Domain.MyOpportunity;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using Yoma.Core.Domain.Pathway.Badges.Interfaces;
using Yoma.Core.Domain.Pathway.Badges.Models;
using Yoma.Core.Domain.Pathway.Helpers;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Domain.SSI;
using Yoma.Core.Domain.SSI.Interfaces.Lookups;
using Yoma.Core.Domain.SSI.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// The youth's answers, and their path built from them. The path is computed on every request from the cached catalogue and outcome
  /// figures, so it follows the catalogue and the youth's own progress without being stored; only the youth's pins and swaps are kept
  /// (<see cref="IPathwayStepStore"/>).
  ///
  /// Yoma's own preferences come first (iteration 4, W3): a youth who has not set up My Yoma is asked only what their preferences and
  /// profile do not answer, and saved answers that have a home in Yoma's preferences are written back to them. The rules are in
  /// <see cref="PathwayPreferences"/>.
  /// </summary>
  public class PathwayService : IPathwayService
  {
    #region Class Variables
    private const string EnglishLanguage = "English";
    private const string UsuallyNextCacheKey = "pathway_usually_next";

    private readonly ILogger<PathwayService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PathwayOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly IUserService _userService;
    private readonly IUserProfileService _userProfileService;
    private readonly IUserGoalService _userGoalService;
    private readonly ITimeIntervalService _timeIntervalService;
    private readonly ISkillService _skillService;
    private readonly IOpportunityCategoryService _opportunityCategoryService;
    private readonly ICountryService _countryService;
    private readonly ILanguageService _languageService;
    private readonly IPathwayCatalogueService _catalogueService;
    private readonly IPathwayOutcomeService _outcomeService;
    private readonly IPathwayBadgeSource _badgeSource;
    private readonly IPathwayStepStore _stepStore;
    private readonly IRepository<PathwayProfileRecord> _profileRepository;
    private readonly IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> _myOpportunityRepository;
    private readonly IMyOpportunityActionService _myOpportunityActionService;
    private readonly IMyOpportunityVerificationStatusService _myOpportunityVerificationStatusService;
    private readonly IRepository<UserPreferenceLanguage> _userPreferenceLanguageRepository;
    private readonly IRepository<SSICredentialIssuance> _credentialIssuanceRepository;
    private readonly ISSICredentialIssuanceStatusService _credentialIssuanceStatusService;
    private readonly CourseGradeRubric _rubric;
    #endregion

    #region Constructor
    public PathwayService(ILogger<PathwayService> logger,
      IHttpContextAccessor httpContextAccessor,
      IOptions<PathwayOptions> options,
      IMemoryCache memoryCache,
      IUserService userService,
      IUserProfileService userProfileService,
      IUserGoalService userGoalService,
      ITimeIntervalService timeIntervalService,
      ISkillService skillService,
      IOpportunityCategoryService opportunityCategoryService,
      ICountryService countryService,
      ILanguageService languageService,
      IPathwayCatalogueService catalogueService,
      IPathwayOutcomeService outcomeService,
      IPathwayBadgeSource badgeSource,
      IPathwayStepStore stepStore,
      IRepository<PathwayProfileRecord> profileRepository,
      IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> myOpportunityRepository,
      IMyOpportunityActionService myOpportunityActionService,
      IMyOpportunityVerificationStatusService myOpportunityVerificationStatusService,
      IRepository<UserPreferenceLanguage> userPreferenceLanguageRepository,
      IRepository<SSICredentialIssuance> credentialIssuanceRepository,
      ISSICredentialIssuanceStatusService credentialIssuanceStatusService,
      IOptions<CourseQualityOptions> courseQualityOptions)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
      _options = PathwayDefaults.Configured(options?.Value ?? throw new ArgumentNullException(nameof(options)));
      _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
      _userService = userService ?? throw new ArgumentNullException(nameof(userService));
      _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
      _userGoalService = userGoalService ?? throw new ArgumentNullException(nameof(userGoalService));
      _timeIntervalService = timeIntervalService ?? throw new ArgumentNullException(nameof(timeIntervalService));
      _skillService = skillService ?? throw new ArgumentNullException(nameof(skillService));
      _opportunityCategoryService = opportunityCategoryService ?? throw new ArgumentNullException(nameof(opportunityCategoryService));
      _countryService = countryService ?? throw new ArgumentNullException(nameof(countryService));
      _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
      _catalogueService = catalogueService ?? throw new ArgumentNullException(nameof(catalogueService));
      _outcomeService = outcomeService ?? throw new ArgumentNullException(nameof(outcomeService));
      _badgeSource = badgeSource ?? throw new ArgumentNullException(nameof(badgeSource));
      _stepStore = stepStore ?? throw new ArgumentNullException(nameof(stepStore));
      _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
      _myOpportunityRepository = myOpportunityRepository ?? throw new ArgumentNullException(nameof(myOpportunityRepository));
      _myOpportunityActionService = myOpportunityActionService ?? throw new ArgumentNullException(nameof(myOpportunityActionService));
      _myOpportunityVerificationStatusService = myOpportunityVerificationStatusService ?? throw new ArgumentNullException(nameof(myOpportunityVerificationStatusService));
      _userPreferenceLanguageRepository = userPreferenceLanguageRepository ?? throw new ArgumentNullException(nameof(userPreferenceLanguageRepository));
      _credentialIssuanceRepository = credentialIssuanceRepository ?? throw new ArgumentNullException(nameof(credentialIssuanceRepository));
      _credentialIssuanceStatusService = credentialIssuanceStatusService ?? throw new ArgumentNullException(nameof(credentialIssuanceStatusService));
      // commitment hours use the same study-hour conversion as course lengths
      _rubric = CourseQualityGrading.Configured(courseQualityOptions?.Value ?? throw new ArgumentNullException(nameof(courseQualityOptions)));
    }
    #endregion

    #region Public Members
    public PathwayQuestions GetQuestions()
    {
      var countries = _options.Countries.Select(o => _countryService.GetByCodeAlpha2OrNull(o)).Where(o => o != null).Select(o => o!).ToList();

      // signed in and not set up yet: leave out what Yoma's preferences already answer
      PathwayPreferenceAnswers? answers = null;
      var user = GetUserOrNull();
      if (user != null && GetRecordOrNull(user.Id) == null) answers = ReadPreferencesOrNull(user);

      return PathwayQuestionBuilder.Build(_options, countries, answers);
    }

    public PathwayProfile GetProfile()
    {
      var user = GetUser();
      var record = GetRecordOrNull(user.Id) ?? throw new EntityNotFoundException("You have not set up My Yoma yet");

      return ToProfile(record);
    }

    public async Task<PathwayProfile> UpsertProfile(PathwayProfileRequest request)
    {
      var user = GetUser(true);
      var record = GetRecordOrNull(user.Id);

      // the first time, answers to the questions left out come from Yoma's preferences
      var input = ReadPreferenceInputOrNull(user);
      // and the request's values for them are fallbacks the web sent for hidden questions
      List<string> fromPreferences = [];
      if (request != null && record == null && input != null)
      {
        var answers = PathwayPreferences.Read(input, _options);
        request = PathwayPreferences.Fill(request, answers, _options);
        fromPreferences = answers.Answered;
      }

      var errors = PathwayQuestionBuilder.Validate(request, _options, id => _countryService.GetByIdOrNull(id) != null);
      if (request == null || errors.Count > 0) throw new ValidationException(string.Join(". ", errors));

      var keys = (request.KnownSkillKeys ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

      if (record == null)
      {
        record = await _profileRepository.Create(new PathwayProfileRecord
        {
          Id = Guid.NewGuid(),
          UserId = user.Id,
          GoalKey = request.GoalKey,
          Background = request.Background,
          KnownSkillKeys = keys,
          HoursPerWeek = request.HoursPerWeek,
          CountryId = request.CountryId
        });
      }
      else
      {
        record.GoalKey = request.GoalKey;
        record.Background = request.Background;
        record.KnownSkillKeys = keys;
        record.HoursPerWeek = request.HoursPerWeek;
        record.CountryId = request.CountryId;
        record = await _profileRepository.Update(record);
      }

      if (input != null) await WriteBackOrLog(user, input, request, fromPreferences);

      return ToProfile(record);
    }

    public async Task<MyYoma> Get()
    {
      var user = GetUser(true);
      var (record, goal, request, catalogue, outcomes, activity) = await Prepare(user);
      var profile = ToProfile(record);

      var result = PathwayEngine.Build(catalogue, request, _options, outcomes);

      var myYoma = new MyYoma
      {
        Profile = profile,
        YoId = new YoIdSummary
        {
          DisplayName = DisplayName(user),
          CompletedOpportunities = activity.CompletedOpportunities,
          Skills = user.Skills?.Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList() ?? [],
          Credentials = CountCredentials(user.Id),
          // the balance lives with the ZLTO provider; one call per page view is not cheap, so My Yoma leaves it out
          ZltoBalance = null
        },
        GoalLabel = goal.Label,
        ThroughLine = result.ThroughLine,
        TotalCourses = result.TotalCourses,
        TotalHours = result.TotalHours,
        EstimatedWeeks = result.EstimatedWeeks,
        CompletedCourses = result.CompletedCourses,
        Stages = result.Stages,
        Jobs = result.Jobs,
        StartLabel = PathwayEngine.StartLabel(request.Background, profile.CountryName),
        DestinationLabel = PathwayEngine.DestinationLabel(goal)
      };

      myYoma.Badges = await _badgeSource.ListForUser(user.Id, ToBadgePath(myYoma)) ?? [];
      return myYoma;
    }

    public async Task<List<PathwayCourse>> ListAlternatives(Guid opportunityId)
    {
      if (opportunityId == Guid.Empty) throw new ArgumentNullException(nameof(opportunityId));

      var user = GetUser();
      var (_, _, request, catalogue, outcomes, _) = await Prepare(user);

      var path = PathwayEngine.Build(catalogue, request, _options, outcomes);
      if (!path.Stages.Any(o => o.Courses.Any(c => c.OpportunityId == opportunityId)))
        throw new EntityNotFoundException($"Opportunity with id '{opportunityId}' is not a step on your path");

      return PathwayEngine.Alternatives(catalogue, request, _options, path, opportunityId, outcomes);
    }
    #endregion

    #region Private Members
    /// <summary>
    /// The path in the shape the badge calculator reads: when the goal was set and each stage's course ids.
    /// </summary>
    public static LearnerBadgePath ToBadgePath(MyYoma myYoma) =>
      new(myYoma.Profile.DateCreated,
        [.. myYoma.Stages.Select(o => new LearnerBadgeStage(o.Order, o.Name, [.. o.Courses.Select(c => c.OpportunityId)]))]);

    /// <summary>
    /// Everything the engine needs for the youth's path. Throws EntityNotFoundException (404) when they have not onboarded.
    /// </summary>
    private async Task<(PathwayProfileRecord Record, PathwayGoalOptions Goal, PathwayEngineRequest Request, PathwayCatalogue Catalogue, PathwayOutcomeSnapshot? Outcomes,
      (Dictionary<Guid, PathwayStepStatus> Statuses, int CompletedOpportunities) Activity)> Prepare(User user)
    {
      var record = GetRecordOrNull(user.Id) ?? throw new EntityNotFoundException("You have not set up My Yoma yet");

      var goal = _options.Goals.SingleOrDefault(o => o.Key.Equals(record.GoalKey, StringComparison.OrdinalIgnoreCase))
        ?? throw new EntityNotFoundException($"The goal '{record.GoalKey}' is no longer offered: choose another one");

      var activity = LoadActivity(user.Id);
      var catalogue = _catalogueService.Get();
      var outcomes = GetOutcomesOrNull();

      var request = new PathwayEngineRequest
      {
        Goal = goal,
        Background = _options.Backgrounds.FirstOrDefault(o => o.Background == record.Background) ?? _options.Backgrounds[0],
        Hours = _options.Hours.FirstOrDefault(o => o.HoursPerWeek == record.HoursPerWeek) ?? _options.Hours.OrderBy(o => Math.Abs(o.HoursPerWeek - record.HoursPerWeek)).First(),
        KnownSkills = [.. _options.KnownSkills.Where(o => record.KnownSkillKeys.Contains(o.Key, StringComparer.OrdinalIgnoreCase))],
        CountryId = record.CountryId,
        EnglishFirst = IsEnglishFirst(user.Id),
        Statuses = activity.Statuses,
        Locked = await _stepStore.ListLocked(user.Id) ?? [],
        Swaps = await _stepStore.ListSwaps(user.Id) ?? [],
        UsuallyNext = GetUsuallyNextOrEmpty(catalogue)
      };

      return (record, goal, request, catalogue, outcomes, activity);
    }

    private User GetUser(bool includeChildItems = false)
    {
      return _userService.GetByUsername(HttpContextAccessorHelper.GetUsername(_httpContextAccessor, false), includeChildItems, false);
    }

    /// <summary>
    /// The signed-in youth on an endpoint that also answers anonymously; null when signed out or not yet known to Yoma.
    /// </summary>
    private User? GetUserOrNull()
    {
      if (!HttpContextAccessorHelper.UserContextAvailable(_httpContextAccessor)) return null;

      try
      {
        return GetUser(true);
      }
      catch (EntityNotFoundException)
      {
        return null;
      }
    }

    private PathwayProfileRecord? GetRecordOrNull(Guid userId)
    {
      return _profileRepository.Query().SingleOrDefault(o => o.UserId == userId);
    }

    /// <summary>
    /// The signed-in youth's Yoma preferences and profile country in the pathway's terms. Preferences add to the questions; failing to
    /// read them must not take the questions down, so a failure is logged and the youth is asked everything.
    /// </summary>
    private PathwayPreferenceInput? ReadPreferenceInputOrNull(User user)
    {
      try
      {
        var preferences = _userProfileService.GetPreferences();
        var hours = CourseQualityGradingEngine.StudyHours(preferences.CommitmentInterval, preferences.CommitmentIntervalCount, _rubric);
        var skills = (user.Skills?.Select(o => o.Name) ?? []).Concat(preferences.SkillsSelfAttested.Select(o => o.Name))
          .Where(o => !string.IsNullOrWhiteSpace(o)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();

        return new PathwayPreferenceInput(preferences.Goal, hours, [.. preferences.Categories.Select(o => o.Name)], skills, user.CountryId);
      }
      catch (Exception ex) when (ex is EntityNotFoundException or InvalidOperationException or ArgumentException)
      {
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Failed to read Yoma preferences for My Yoma: {errorMessage}", ex.Message);
        return null;
      }
    }

    private PathwayPreferenceAnswers? ReadPreferencesOrNull(User user)
    {
      var input = ReadPreferenceInputOrNull(user);
      return input == null ? null : PathwayPreferences.Read(input, _options);
    }

    /// <summary>
    /// Saves the answers that have a home in Yoma's preferences back to them, through the preference service, keeping every other
    /// preference as it is. The pathway profile is already saved; a failure here is logged and does not fail the request.
    /// </summary>
    private async Task WriteBackOrLog(User user, PathwayPreferenceInput current, PathwayProfileRequest saved, List<string> fromPreferences)
    {
      try
      {
        var categories = _opportunityCategoryService.List();
        var plan = PathwayPreferences.PlanWriteBack(current, saved, _options, [.. categories.Select(o => o.Name)], fromPreferences);
        if (!plan.Any) return;

        var preferences = _userProfileService.GetPreferences();
        var changed = false;

        var goalId = preferences.GoalId;
        if (plan.SetUserGoal != null && !goalId.HasValue && _userGoalService.GetByNameOrNull(plan.SetUserGoal) is { } userGoal)
        {
          goalId = userGoal.Id;
          changed = true;
        }

        var intervalId = preferences.CommitmentIntervalId;
        var intervalCount = preferences.CommitmentIntervalCount;
        if (plan.CommitmentHours.HasValue)
        {
          intervalId = _timeIntervalService.GetByName(TimeIntervalOption.Hour.ToString()).Id;
          intervalCount = (short)Math.Clamp(plan.CommitmentHours.Value, 1, short.MaxValue);
          changed = true;
        }

        var categoryIds = preferences.Categories.Select(o => o.Id).ToList();
        if (plan.AddCategory != null && categories.FirstOrDefault(o => o.Name == plan.AddCategory) is { } category && !categoryIds.Contains(category.Id))
        {
          categoryIds.Add(category.Id);
          changed = true;
        }

        var owned = (user.Skills?.Select(o => o.Id) ?? []).ToHashSet();
        var skillIds = preferences.SkillsSelfAttested.Select(o => o.Id).ToList();
        foreach (var key in plan.AddSkillKeys)
        {
          var known = _options.KnownSkills.First(o => o.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
          var skill = known.Probes.Select(_skillService.GetByNameOrNull).FirstOrDefault(o => o != null);
          if (skill == null || owned.Contains(skill.Id) || skillIds.Contains(skill.Id)) continue;

          skillIds.Add(skill.Id);
          changed = true;
        }

        if (!changed) return;

        // the preference request replaces every preference, so everything not written back is sent as it is
        await _userProfileService.UpdatePreferences(new UserPreferencesRequest
        {
          GoalId = goalId,
          CommitmentIntervalId = intervalId,
          CommitmentIntervalCount = intervalCount,
          EngagementTypes = [.. preferences.EngagementTypes.Select(o => o.Id)],
          Incentivized = preferences.Incentivized,
          Categories = categoryIds,
          AccessibilityRequirements = [.. preferences.AccessibilityRequirements.Select(o => o.Id)],
          AccessibilityRequirementOtherDescription = preferences.AccessibilityRequirementOtherDescription,
          Languages = [.. preferences.Languages.Select(o => o.Id)],
          SkillsSelfAttested = skillIds
        });
      }
      catch (Exception ex) when (ex is ValidationException or EntityNotFoundException or InvalidOperationException or ArgumentException)
      {
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Failed to save My Yoma answers back to Yoma preferences: {errorMessage}", ex.Message);
      }
    }

    private PathwayProfile ToProfile(PathwayProfileRecord record)
    {
      var goal = _options.Goals.FirstOrDefault(o => o.Key.Equals(record.GoalKey, StringComparison.OrdinalIgnoreCase));

      return new PathwayProfile
      {
        GoalKey = record.GoalKey,
        GoalLabel = goal?.Label ?? record.GoalKey,
        Background = record.Background,
        KnownSkillKeys = [.. record.KnownSkillKeys],
        HoursPerWeek = record.HoursPerWeek,
        CountryId = record.CountryId,
        CountryName = record.CountryId.HasValue ? _countryService.GetByIdOrNull(record.CountryId.Value)?.Name : null,
        DateCreated = record.DateCreated,
        DateModified = record.DateModified
      };
    }

    /// <summary>
    /// The youth's own completions and starts: completed wins over started, whatever the order.
    /// </summary>
    private (Dictionary<Guid, PathwayStepStatus> Statuses, int CompletedOpportunities) LoadActivity(Guid userId)
    {
      var verificationId = _myOpportunityActionService.GetByName(MyOpportunity.Action.Verification.ToString()).Id;
      var navigatedId = _myOpportunityActionService.GetByName(MyOpportunity.Action.NavigatedExternalLink.ToString()).Id;
      var completedId = _myOpportunityVerificationStatusService.GetByName(VerificationStatus.Completed.ToString()).Id;
      var pendingId = _myOpportunityVerificationStatusService.GetByName(VerificationStatus.Pending.ToString()).Id;

      var rows = _myOpportunityRepository.Query()
        .Where(o => o.UserId == userId && (o.ActionId == navigatedId || (o.ActionId == verificationId && (o.VerificationStatusId == completedId || o.VerificationStatusId == pendingId))))
        .Select(o => new { o.OpportunityId, o.ActionId, o.VerificationStatusId })
        .ToList();

      var completed = rows.Where(o => o.ActionId == verificationId && o.VerificationStatusId == completedId).Select(o => o.OpportunityId).ToHashSet();
      var statuses = rows.Select(o => o.OpportunityId).Distinct()
        .ToDictionary(o => o, o => completed.Contains(o) ? PathwayStepStatus.Completed : PathwayStepStatus.InProgress);

      return (statuses, completed.Count);
    }

    /// <summary>
    /// "Usually go on to" for every opportunity, from every completed verification on Yoma, cached like the outcome figures. Only steps
    /// and jobs youth can open today are named.
    /// </summary>
    private Dictionary<Guid, List<PathwayNextStep>> GetUsuallyNextOrEmpty(PathwayCatalogue catalogue)
    {
      try
      {
        return _memoryCache.GetOrCreate(UsuallyNextCacheKey, entry =>
        {
          entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheInMinutes);

          var verificationId = _myOpportunityActionService.GetByName(MyOpportunity.Action.Verification.ToString()).Id;
          var completedId = _myOpportunityVerificationStatusService.GetByName(VerificationStatus.Completed.ToString()).Id;

          var completions = _myOpportunityRepository.Query()
            .Where(o => o.ActionId == verificationId && o.VerificationStatusId == completedId)
            .Select(o => new { o.UserId, o.OpportunityId, Date = o.DateCompleted ?? o.DateModified }).ToList()
            .Select(o => new PathwayActivity(o.UserId, o.OpportunityId, o.Date));

          var titles = new Dictionary<Guid, string>();
          foreach (var course in catalogue.Courses) titles.TryAdd(course.OpportunityId, course.Title);
          foreach (var job in catalogue.Jobs) titles.TryAdd(job.OpportunityId, job.Title);

          return PathwayNextSteps.Compute(completions, id => titles.GetValueOrDefault(id), _options.MinimumGroupSize, _options.MaxUsuallyNext);
        }) ?? [];
      }
      catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
      {
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Failed to work out where pathway completers go next: {errorMessage}", ex.Message);
        return [];
      }
    }

    /// <summary>
    /// English-first unless the youth chose preferred languages and English is not one of them.
    /// </summary>
    private bool IsEnglishFirst(Guid userId)
    {
      var ids = _userPreferenceLanguageRepository.Query().Where(o => o.UserId == userId).Select(o => o.LanguageId).ToList();
      if (ids.Count == 0) return true;

      var names = _languageService.List().Where(o => ids.Contains(o.Id)).Select(o => o.Name);
      return names.Any(o => o.Equals(EnglishLanguage, StringComparison.OrdinalIgnoreCase));
    }

    private int CountCredentials(Guid userId)
    {
      var issuedId = _credentialIssuanceStatusService.GetByName(CredentialIssuanceStatus.Issued.ToString()).Id;
      return _credentialIssuanceRepository.Query().Count(o => o.UserId == userId && o.StatusId == issuedId);
    }

    /// <summary>
    /// Outcomes add figures to the path; a failure to work them out must not take the path down with it.
    /// </summary>
    private PathwayOutcomeSnapshot? GetOutcomesOrNull()
    {
      try
      {
        return _outcomeService.GetSnapshot();
      }
      catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
      {
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Failed to work out pathway outcomes: {errorMessage}", ex.Message);
        return null;
      }
    }

    private static string DisplayName(User user)
    {
      if (!string.IsNullOrWhiteSpace(user.DisplayName)) return user.DisplayName.Trim();

      var name = string.Join(' ', new[] { user.FirstName, user.Surname }.Where(o => !string.IsNullOrWhiteSpace(o))).Trim();
      return name.Length > 0 ? name : user.Username;
    }
    #endregion
  }
}
