using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Helpers;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Models;
using Yoma.Core.Domain.Lookups.Interfaces;
using Yoma.Core.Domain.MyOpportunity;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
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
  /// figures, so it follows the catalogue and the youth's own progress without being stored.
  /// </summary>
  public class PathwayService : IPathwayService
  {
    #region Class Variables
    private const string EnglishLanguage = "English";

    private readonly ILogger<PathwayService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PathwayOptions _options;
    private readonly IUserService _userService;
    private readonly ICountryService _countryService;
    private readonly ILanguageService _languageService;
    private readonly IPathwayCatalogueService _catalogueService;
    private readonly IPathwayOutcomeService _outcomeService;
    private readonly IPathwayBadgeSource _badgeSource;
    private readonly IRepository<PathwayProfileRecord> _profileRepository;
    private readonly IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> _myOpportunityRepository;
    private readonly IMyOpportunityActionService _myOpportunityActionService;
    private readonly IMyOpportunityVerificationStatusService _myOpportunityVerificationStatusService;
    private readonly IRepository<UserPreferenceLanguage> _userPreferenceLanguageRepository;
    private readonly IRepository<SSICredentialIssuance> _credentialIssuanceRepository;
    private readonly ISSICredentialIssuanceStatusService _credentialIssuanceStatusService;
    #endregion

    #region Constructor
    public PathwayService(ILogger<PathwayService> logger,
      IHttpContextAccessor httpContextAccessor,
      IOptions<PathwayOptions> options,
      IUserService userService,
      ICountryService countryService,
      ILanguageService languageService,
      IPathwayCatalogueService catalogueService,
      IPathwayOutcomeService outcomeService,
      IPathwayBadgeSource badgeSource,
      IRepository<PathwayProfileRecord> profileRepository,
      IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> myOpportunityRepository,
      IMyOpportunityActionService myOpportunityActionService,
      IMyOpportunityVerificationStatusService myOpportunityVerificationStatusService,
      IRepository<UserPreferenceLanguage> userPreferenceLanguageRepository,
      IRepository<SSICredentialIssuance> credentialIssuanceRepository,
      ISSICredentialIssuanceStatusService credentialIssuanceStatusService)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
      _options = PathwayDefaults.Configured(options?.Value ?? throw new ArgumentNullException(nameof(options)));
      _userService = userService ?? throw new ArgumentNullException(nameof(userService));
      _countryService = countryService ?? throw new ArgumentNullException(nameof(countryService));
      _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
      _catalogueService = catalogueService ?? throw new ArgumentNullException(nameof(catalogueService));
      _outcomeService = outcomeService ?? throw new ArgumentNullException(nameof(outcomeService));
      _badgeSource = badgeSource ?? throw new ArgumentNullException(nameof(badgeSource));
      _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
      _myOpportunityRepository = myOpportunityRepository ?? throw new ArgumentNullException(nameof(myOpportunityRepository));
      _myOpportunityActionService = myOpportunityActionService ?? throw new ArgumentNullException(nameof(myOpportunityActionService));
      _myOpportunityVerificationStatusService = myOpportunityVerificationStatusService ?? throw new ArgumentNullException(nameof(myOpportunityVerificationStatusService));
      _userPreferenceLanguageRepository = userPreferenceLanguageRepository ?? throw new ArgumentNullException(nameof(userPreferenceLanguageRepository));
      _credentialIssuanceRepository = credentialIssuanceRepository ?? throw new ArgumentNullException(nameof(credentialIssuanceRepository));
      _credentialIssuanceStatusService = credentialIssuanceStatusService ?? throw new ArgumentNullException(nameof(credentialIssuanceStatusService));
    }
    #endregion

    #region Public Members
    public PathwayQuestions GetQuestions()
    {
      var countries = _options.Countries.Select(o => _countryService.GetByCodeAlpha2OrNull(o)).Where(o => o != null).Select(o => o!).ToList();
      return PathwayQuestionBuilder.Build(_options, countries);
    }

    public PathwayProfile GetProfile()
    {
      var user = GetUser();
      var record = GetRecordOrNull(user.Id) ?? throw new EntityNotFoundException("You have not set up My Yoma yet");

      return ToProfile(record);
    }

    public async Task<PathwayProfile> UpsertProfile(PathwayProfileRequest request)
    {
      var errors = PathwayQuestionBuilder.Validate(request, _options, id => _countryService.GetByIdOrNull(id) != null);
      if (errors.Count > 0) throw new ValidationException(string.Join(". ", errors));

      var user = GetUser();
      var record = GetRecordOrNull(user.Id);
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

      return ToProfile(record);
    }

    public async Task<MyYoma> Get()
    {
      var user = GetUser(true);
      var record = GetRecordOrNull(user.Id) ?? throw new EntityNotFoundException("You have not set up My Yoma yet");
      var profile = ToProfile(record);

      var goal = _options.Goals.SingleOrDefault(o => o.Key.Equals(record.GoalKey, StringComparison.OrdinalIgnoreCase))
        ?? throw new EntityNotFoundException($"The goal '{record.GoalKey}' is no longer offered: choose another one");

      var activity = LoadActivity(user.Id);

      var request = new PathwayEngineRequest
      {
        Goal = goal,
        Background = _options.Backgrounds.FirstOrDefault(o => o.Background == record.Background) ?? _options.Backgrounds[0],
        Hours = _options.Hours.FirstOrDefault(o => o.HoursPerWeek == record.HoursPerWeek) ?? _options.Hours.OrderBy(o => Math.Abs(o.HoursPerWeek - record.HoursPerWeek)).First(),
        KnownSkills = [.. _options.KnownSkills.Where(o => record.KnownSkillKeys.Contains(o.Key, StringComparer.OrdinalIgnoreCase))],
        CountryId = record.CountryId,
        EnglishFirst = IsEnglishFirst(user.Id),
        Statuses = activity.Statuses
      };

      var result = PathwayEngine.Build(_catalogueService.Get(), request, _options, GetOutcomesOrNull());

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
        Jobs = result.Jobs
      };

      myYoma.Badges = await _badgeSource.ListForUser(user.Id, myYoma) ?? [];
      return myYoma;
    }
    #endregion

    #region Private Members
    private User GetUser(bool includeChildItems = false)
    {
      return _userService.GetByUsername(HttpContextAccessorHelper.GetUsername(_httpContextAccessor, false), includeChildItems, false);
    }

    private PathwayProfileRecord? GetRecordOrNull(Guid userId)
    {
      return _profileRepository.Query().SingleOrDefault(o => o.UserId == userId);
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
