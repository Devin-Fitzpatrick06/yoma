using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.MyOpportunity;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// Reads completion and start records across Yoma once, works out every course's outcome figures and keeps them in memory.
  /// The read is platform-wide; at today's volumes a cache of minutes is enough, at larger ones it belongs in a nightly job.
  /// </summary>
  public class PathwayOutcomeService : IPathwayOutcomeService
  {
    #region Class Variables
    private const string CacheKey = "pathway_outcomes";

    private readonly PathwayOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly IPathwayCatalogueService _catalogueService;
    private readonly IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> _myOpportunityRepository;
    private readonly IMyOpportunityActionService _myOpportunityActionService;
    private readonly IMyOpportunityVerificationStatusService _myOpportunityVerificationStatusService;
    private readonly IOpportunityTypeService _opportunityTypeService;
    private readonly IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> _opportunityRepository;
    #endregion

    #region Constructor
    public PathwayOutcomeService(IOptions<PathwayOptions> options,
      IMemoryCache memoryCache,
      IPathwayCatalogueService catalogueService,
      IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> myOpportunityRepository,
      IMyOpportunityActionService myOpportunityActionService,
      IMyOpportunityVerificationStatusService myOpportunityVerificationStatusService,
      IOpportunityTypeService opportunityTypeService,
      IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Opportunity.Models.Opportunity> opportunityRepository)
    {
      _options = PathwayDefaults.Configured(options?.Value ?? throw new ArgumentNullException(nameof(options)));
      _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
      _catalogueService = catalogueService ?? throw new ArgumentNullException(nameof(catalogueService));
      _myOpportunityRepository = myOpportunityRepository ?? throw new ArgumentNullException(nameof(myOpportunityRepository));
      _myOpportunityActionService = myOpportunityActionService ?? throw new ArgumentNullException(nameof(myOpportunityActionService));
      _myOpportunityVerificationStatusService = myOpportunityVerificationStatusService ?? throw new ArgumentNullException(nameof(myOpportunityVerificationStatusService));
      _opportunityTypeService = opportunityTypeService ?? throw new ArgumentNullException(nameof(opportunityTypeService));
      _opportunityRepository = opportunityRepository ?? throw new ArgumentNullException(nameof(opportunityRepository));
    }
    #endregion

    #region Public Members
    public PathwayOutcomeSnapshot GetSnapshot()
    {
      return _memoryCache.GetOrCreate(CacheKey, entry =>
      {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheInMinutes);
        return PathwayOutcomeEngine.Compute(LoadInput(), _catalogueService.Get().Courses, _options.Goals, _options.MinimumGroupSize, _options.DemoDataNote);
      })!;
    }

    public CourseOutcome GetOutcome(Guid opportunityId)
    {
      if (opportunityId == Guid.Empty) throw new ArgumentNullException(nameof(opportunityId));

      if (GetSnapshot().Courses.TryGetValue(opportunityId, out var outcome)) return outcome;

      // a course nobody has completed yet, listed or not
      var learningTypeId = _opportunityTypeService.GetByName(Opportunity.Type.Learning.ToString()).Id;
      if (!_opportunityRepository.Query().Any(o => o.Id == opportunityId && o.TypeId == learningTypeId))
        throw new EntityNotFoundException($"Learning opportunity with id '{opportunityId}' does not exist");

      return new CourseOutcome
      {
        OpportunityId = opportunityId,
        Completers = 0,
        MinimumGroupSize = _options.MinimumGroupSize,
        Note = PathwayOutcomeEngine.WithNote(PathwayOutcomeEngine.NotEnough(_options.MinimumGroupSize), _options.DemoDataNote)
      };
    }
    #endregion

    #region Private Members
    private PathwayOutcomeInput LoadInput()
    {
      var verificationId = _myOpportunityActionService.GetByName(MyOpportunity.Action.Verification.ToString()).Id;
      var navigatedId = _myOpportunityActionService.GetByName(MyOpportunity.Action.NavigatedExternalLink.ToString()).Id;
      var completedId = _myOpportunityVerificationStatusService.GetByName(VerificationStatus.Completed.ToString()).Id;
      var learningTypeId = _opportunityTypeService.GetByName(Opportunity.Type.Learning.ToString()).Id;
      var jobTypeId = _opportunityTypeService.GetByName(Opportunity.Type.Job.ToString()).Id;

      var learningIds = _opportunityRepository.Query().Where(o => o.TypeId == learningTypeId).Select(o => o.Id);
      var jobIds = _opportunityRepository.Query().Where(o => o.TypeId == jobTypeId).Select(o => o.Id);

      var completed = _myOpportunityRepository.Query()
        .Where(o => o.ActionId == verificationId && o.VerificationStatusId == completedId);

      static PathwayActivity Activity(Guid userId, Guid opportunityId, DateTimeOffset date) => new(userId, opportunityId, date);

      return new PathwayOutcomeInput
      {
        LearningCompletions = [.. completed.Where(o => learningIds.Contains(o.OpportunityId))
          .Select(o => new { o.UserId, o.OpportunityId, Date = o.DateCompleted ?? o.DateModified }).ToList()
          .Select(o => Activity(o.UserId, o.OpportunityId, o.Date))],
        JobCompletions = [.. completed.Where(o => jobIds.Contains(o.OpportunityId))
          .Select(o => new { o.UserId, o.OpportunityId, Date = o.DateCompleted ?? o.DateModified }).ToList()
          .Select(o => Activity(o.UserId, o.OpportunityId, o.Date))],
        LearningStarts = [.. _myOpportunityRepository.Query()
          .Where(o => (o.ActionId == navigatedId || o.ActionId == verificationId) && learningIds.Contains(o.OpportunityId))
          .Select(o => new { o.UserId, o.OpportunityId, o.DateCreated }).ToList()
          .Select(o => Activity(o.UserId, o.OpportunityId, o.DateCreated))]
      };
    }
    #endregion
  }
}
