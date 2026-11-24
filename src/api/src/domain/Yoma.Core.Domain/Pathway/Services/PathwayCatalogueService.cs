using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Extensions;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity;
using Yoma.Core.Domain.Entity.Interfaces.Lookups;
using Yoma.Core.Domain.Lookups.Interfaces;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
using Yoma.Core.Domain.Opportunity;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity.Models;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// Reads the courses and jobs the engine chooses from, each source once for the whole catalogue, and keeps the result in memory.
  /// </summary>
  public class PathwayCatalogueService : IPathwayCatalogueService
  {
    #region Class Variables
    private const string CacheKey = "pathway_catalogue";

    private readonly PathwayOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> _opportunityRepository;
    private readonly IRepository<OpportunitySkill> _opportunitySkillRepository;
    private readonly IRepository<OpportunityLanguage> _opportunityLanguageRepository;
    private readonly IRepository<OpportunityCountry> _opportunityCountryRepository;
    private readonly IRepository<CustomFieldValue> _customFieldValueRepository;
    private readonly ICustomFieldDefinitionService _customFieldDefinitionService;
    private readonly IOpportunityTypeService _opportunityTypeService;
    private readonly IOpportunityStatusService _opportunityStatusService;
    private readonly IOrganizationStatusService _organizationStatusService;
    private readonly ISkillService _skillService;
    private readonly ILanguageService _languageService;
    private readonly IBlobService _blobService;
    private readonly IMyOpportunityService _myOpportunityService;
    private readonly IRepositoryBatched<CourseQualityGradeRecord> _gradeRepository;
    private readonly CourseGradeRubric _rubric;
    #endregion

    #region Constructor
    public PathwayCatalogueService(IOptions<PathwayOptions> options,
      IMemoryCache memoryCache,
      IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> opportunityRepository,
      IRepository<OpportunitySkill> opportunitySkillRepository,
      IRepository<OpportunityLanguage> opportunityLanguageRepository,
      IRepository<OpportunityCountry> opportunityCountryRepository,
      IRepository<CustomFieldValue> customFieldValueRepository,
      ICustomFieldDefinitionService customFieldDefinitionService,
      IOpportunityTypeService opportunityTypeService,
      IOpportunityStatusService opportunityStatusService,
      IOrganizationStatusService organizationStatusService,
      ISkillService skillService,
      ILanguageService languageService,
      IBlobService blobService,
      IMyOpportunityService myOpportunityService,
      IRepositoryBatched<CourseQualityGradeRecord> gradeRepository,
      IOptions<CourseQualityOptions> courseQualityOptions)
    {
      _options = PathwayDefaults.Configured(options?.Value ?? throw new ArgumentNullException(nameof(options)));
      _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
      _opportunityRepository = opportunityRepository ?? throw new ArgumentNullException(nameof(opportunityRepository));
      _opportunitySkillRepository = opportunitySkillRepository ?? throw new ArgumentNullException(nameof(opportunitySkillRepository));
      _opportunityLanguageRepository = opportunityLanguageRepository ?? throw new ArgumentNullException(nameof(opportunityLanguageRepository));
      _opportunityCountryRepository = opportunityCountryRepository ?? throw new ArgumentNullException(nameof(opportunityCountryRepository));
      _customFieldValueRepository = customFieldValueRepository ?? throw new ArgumentNullException(nameof(customFieldValueRepository));
      _customFieldDefinitionService = customFieldDefinitionService ?? throw new ArgumentNullException(nameof(customFieldDefinitionService));
      _opportunityTypeService = opportunityTypeService ?? throw new ArgumentNullException(nameof(opportunityTypeService));
      _opportunityStatusService = opportunityStatusService ?? throw new ArgumentNullException(nameof(opportunityStatusService));
      _organizationStatusService = organizationStatusService ?? throw new ArgumentNullException(nameof(organizationStatusService));
      _skillService = skillService ?? throw new ArgumentNullException(nameof(skillService));
      _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
      _blobService = blobService ?? throw new ArgumentNullException(nameof(blobService));
      _myOpportunityService = myOpportunityService ?? throw new ArgumentNullException(nameof(myOpportunityService));
      _gradeRepository = gradeRepository ?? throw new ArgumentNullException(nameof(gradeRepository));
      // study hours use the configured length parameters; read here rather than through the grading service, which itself reads outcomes
      _rubric = CourseQualityGrading.Configured(courseQualityOptions?.Value ?? throw new ArgumentNullException(nameof(courseQualityOptions)));
    }
    #endregion

    #region Public Members
    public PathwayCatalogue Get()
    {
      return _memoryCache.GetOrCreate(CacheKey, entry =>
      {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheInMinutes);
        return Load();
      })!;
    }
    #endregion

    #region Private Members
    private PathwayCatalogue Load()
    {
      var now = DateTimeOffset.UtcNow;
      var learningTypeId = _opportunityTypeService.GetByName(Opportunity.Type.Learning.ToString()).Id;
      var jobTypeId = _opportunityTypeService.GetByName(Opportunity.Type.Job.ToString()).Id;
      var statusActiveId = _opportunityStatusService.GetByName(Status.Active.ToString()).Id;
      var organizationStatusActiveId = _organizationStatusService.GetByName(OrganizationStatus.Active.ToString()).Id;

      // live: active, of an active organisation, not hidden, started and not ended
      var live = _opportunityRepository.Query()
        .Where(o => (o.TypeId == learningTypeId || o.TypeId == jobTypeId)
          && o.StatusId == statusActiveId
          && o.OrganizationStatusId == organizationStatusActiveId
          && (!o.Hidden.HasValue || o.Hidden == false)
          && o.DateStart <= now
          && (!o.DateEnd.HasValue || o.DateEnd > now))
        .ToList();

      var courses = live.Where(o => o.TypeId == learningTypeId).ToList();
      var jobs = live.Where(o => o.TypeId == jobTypeId).ToList();
      var courseIds = courses.Select(o => o.Id).ToList();
      var jobIds = jobs.Select(o => o.Id).ToList();

      var skillLinks = _opportunitySkillRepository.Query().Where(o => courseIds.Contains(o.OpportunityId)).Select(o => new { o.OpportunityId, o.SkillId }).ToList();
      var skillNames = _skillService.ListByIds([.. skillLinks.Select(o => o.SkillId).Distinct()]).ToDictionary(o => o.Id, o => o.Name);
      var skills = skillLinks.GroupBy(o => o.OpportunityId)
        .ToDictionary(o => o.Key, o => o.Select(s => skillNames.GetValueOrDefault(s.SkillId)).Where(s => s != null).Select(s => s!).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList());

      var languageNames = _languageService.List().ToDictionary(o => o.Id, o => o.Name);
      var languages = _opportunityLanguageRepository.Query().Where(o => courseIds.Contains(o.OpportunityId))
        .Select(o => new { o.OpportunityId, o.LanguageId }).ToList()
        .GroupBy(o => o.OpportunityId)
        .ToDictionary(o => o.Key, o => o.Select(l => languageNames.GetValueOrDefault(l.LanguageId)).Where(l => l != null).Select(l => l!).ToList());

      var countries = _opportunityCountryRepository.Query().Where(o => jobIds.Contains(o.OpportunityId))
        .Select(o => new { o.OpportunityId, o.CountryId, o.CountryName }).ToList()
        .GroupBy(o => o.OpportunityId)
        .ToDictionary(o => o.Key, o => o.ToList());

      var difficulties = LoadDifficulties(courseIds);
      var participation = _myOpportunityService.ListParticipationCounts().ToDictionary(o => o.OpportunityId);
      var grades = _gradeRepository.Query().Where(o => courseIds.Contains(o.OpportunityId) && o.Grade != CourseGrade.Ungraded)
        .Select(o => new { o.OpportunityId, o.Grade, o.GradeLabel }).ToList()
        .ToDictionary(o => o.OpportunityId, o => (Grade: o.Grade.ToString(), Label: o.GradeLabel));

      return new PathwayCatalogue
      {
        Courses = [.. courses.OrderBy(o => o.Id).Select(o => new PathwayCatalogueCourse
        {
          OpportunityId = o.Id,
          Title = o.Title,
          OrganizationName = o.OrganizationName,
          OrganizationLogoURL = LogoURL(o),
          Difficulty = difficulties.GetValueOrDefault(o.Id),
          Hours = CourseQualityGradingEngine.StudyHours(o.CommitmentInterval, o.CommitmentIntervalCount, _rubric) is { } hours ? Math.Round(hours, 1, MidpointRounding.AwayFromZero) : null,
          URL = o.URL,
          Skills = skills.GetValueOrDefault(o.Id) ?? [],
          Languages = languages.GetValueOrDefault(o.Id) ?? [],
          CompletedCount = participation.GetValueOrDefault(o.Id)?.Completed ?? 0,
          StartedCount = participation.GetValueOrDefault(o.Id)?.Started ?? 0,
          Grade = grades.TryGetValue(o.Id, out var grade) ? grade.Grade : null,
          GradeLabel = grades.TryGetValue(o.Id, out var graded) ? graded.Label : null
        })],
        Jobs = [.. jobs.OrderBy(o => o.Id).Select(o => new PathwayCatalogueJob
        {
          OpportunityId = o.Id,
          Title = o.Title,
          OrganizationName = o.OrganizationName,
          CountryIds = countries.GetValueOrDefault(o.Id)?.Select(c => c.CountryId).ToList() ?? [],
          Countries = countries.GetValueOrDefault(o.Id)?.Select(c => c.CountryName).Distinct().OrderBy(c => c).ToList() ?? [],
          DateStart = o.DateStart
        })]
      };
    }

    /// <summary>
    /// Learning difficulty is a custom field; its option keys are Beginner, Intermediate, Advanced and AnyLevel.
    /// </summary>
    private Dictionary<Guid, string> LoadDifficulties(List<Guid> courseIds)
    {
      CustomFieldDefinition definition;
      try
      {
        definition = _customFieldDefinitionService.GetByKey(CustomFieldEntityType.Opportunity, CustomFieldConstants.Difficulty.Keys.Learning, false, false);
      }
      catch (Exception ex) when (ex is Core.Exceptions.EntityNotFoundException or InvalidOperationException)
      {
        return [];
      }

      return _customFieldValueRepository.Query()
        .Where(o => o.CustomFieldDefinitionId == definition.Id && o.OpportunityId.HasValue && courseIds.Contains(o.OpportunityId.Value))
        .Select(o => new { o.OpportunityId, o.Value }).ToList()
        .GroupBy(o => o.OpportunityId!.Value)
        .ToDictionary(o => o.Key, o => DifficultyLabel(o.First().Value));
    }

    public static string DifficultyLabel(string value)
    {
      var key = value.Split(CustomFieldValue.Value_Delimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? value;
      return Enum.TryParse<Difficulty>(key, true, out var known) ? known.ToDescription() : key;
    }

    private string? LogoURL(Opportunity.Models.Opportunity opportunity)
    {
      if (!opportunity.OrganizationLogoStorageType.HasValue || string.IsNullOrEmpty(opportunity.OrganizationLogoKey)) return null;
      return _blobService.GetURL(opportunity.OrganizationLogoStorageType.Value, opportunity.OrganizationLogoKey);
    }
    #endregion
  }
}
