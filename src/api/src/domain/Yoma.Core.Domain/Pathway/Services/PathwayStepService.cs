using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Helpers;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.MyOpportunity;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Services
{
  /// <summary>
  /// Lock and swap on the youth's path, the step events behind drop-off (W5), pathway health and the YoID summary. The path itself is
  /// always the engine's (IPathwayService.Get()); this service only stores the youth's choices and events.
  ///
  /// Completion for drop-off is not written by a hook on verification: a step counts as completed when it has a Completed event or the
  /// youth's verification of that opportunity is Completed in MyOpportunity, read when the aggregate is worked out.
  /// </summary>
  public class PathwayStepService : IPathwayStepService
  {
    #region Class Variables
    /// <summary>
    /// Configuration switch: until it is true, drop-off is Collecting everywhere, whatever the data.
    /// </summary>
    public const string DropOffAvailableKey = "Pathway:DropOffAvailable";

    /// <summary>
    /// A Shown event for the same youth and step within this window is a repeat (a re-render, a second tab) and is dropped.
    /// </summary>
    public static readonly TimeSpan ShownWindow = TimeSpan.FromMinutes(30);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly PathwayOptions _options;
    private readonly IUserService _userService;
    private readonly IPathwayService _pathwayService;
    private readonly IPathwayStepAlternatives _alternatives;
    private readonly IOpportunityService _opportunityService;
    private readonly IRepository<PathwayStepLockRecord> _lockRepository;
    private readonly IRepository<PathwayStepSwapRecord> _swapRepository;
    private readonly IRepository<PathwayStepEventRecord> _eventRepository;
    private readonly IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> _myOpportunityRepository;
    private readonly IMyOpportunityActionService _myOpportunityActionService;
    private readonly IMyOpportunityVerificationStatusService _myOpportunityVerificationStatusService;
    #endregion

    #region Constructor
    public PathwayStepService(IHttpContextAccessor httpContextAccessor,
      IConfiguration configuration,
      IOptions<PathwayOptions> options,
      IUserService userService,
      IPathwayService pathwayService,
      IPathwayStepAlternatives alternatives,
      IOpportunityService opportunityService,
      IRepository<PathwayStepLockRecord> lockRepository,
      IRepository<PathwayStepSwapRecord> swapRepository,
      IRepository<PathwayStepEventRecord> eventRepository,
      IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunity.Models.MyOpportunity> myOpportunityRepository,
      IMyOpportunityActionService myOpportunityActionService,
      IMyOpportunityVerificationStatusService myOpportunityVerificationStatusService)
    {
      _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
      _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
      _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
      _userService = userService ?? throw new ArgumentNullException(nameof(userService));
      _pathwayService = pathwayService ?? throw new ArgumentNullException(nameof(pathwayService));
      _alternatives = alternatives ?? throw new ArgumentNullException(nameof(alternatives));
      _opportunityService = opportunityService ?? throw new ArgumentNullException(nameof(opportunityService));
      _lockRepository = lockRepository ?? throw new ArgumentNullException(nameof(lockRepository));
      _swapRepository = swapRepository ?? throw new ArgumentNullException(nameof(swapRepository));
      _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
      _myOpportunityRepository = myOpportunityRepository ?? throw new ArgumentNullException(nameof(myOpportunityRepository));
      _myOpportunityActionService = myOpportunityActionService ?? throw new ArgumentNullException(nameof(myOpportunityActionService));
      _myOpportunityVerificationStatusService = myOpportunityVerificationStatusService ?? throw new ArgumentNullException(nameof(myOpportunityVerificationStatusService));
    }
    #endregion

    #region Public Members
    public async Task<MyYoma> SetLocked(Guid opportunityId, PathwayStepLockRequest request)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));

      var userId = GetUserId();
      var myYoma = await _pathwayService.Get();
      if (FindStage(myYoma, opportunityId) == null) throw new EntityNotFoundException("This step is not on your path");

      var existing = _lockRepository.Query().FirstOrDefault(o => o.UserId == userId && o.OpportunityId == opportunityId);
      if (request.Locked && existing == null)
        await _lockRepository.Create(new PathwayStepLockRecord { Id = Guid.NewGuid(), UserId = userId, OpportunityId = opportunityId });
      else if (!request.Locked && existing != null)
        await _lockRepository.Delete(existing);
      else
        return myYoma;

      return await _pathwayService.Get();
    }

    public async Task<List<PathwayCourse>> ListAlternatives(Guid opportunityId)
    {
      var myYoma = await _pathwayService.Get();
      return [.. _alternatives.List(myYoma, opportunityId).Take(PathwayStepAlternatives.MaxAlternatives)];
    }

    public async Task<MyYoma> Swap(Guid opportunityId, PathwayStepSwapRequest request)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      if (request.ReplacementOpportunityId == Guid.Empty) throw new ValidationException("Choose the step to swap in");
      if (request.ReplacementOpportunityId == opportunityId) throw new ValidationException("A step cannot be swapped for itself");

      var userId = GetUserId();
      var myYoma = await _pathwayService.Get();
      var stage = FindStage(myYoma, opportunityId) ?? throw new EntityNotFoundException("This step is not on your path");

      var alternatives = _alternatives.List(myYoma, opportunityId).Take(PathwayStepAlternatives.MaxAlternatives);
      if (!alternatives.Any(o => o.OpportunityId == request.ReplacementOpportunityId))
        throw new ValidationException("The replacement must be one of this step's alternatives");

      await _swapRepository.Create(new PathwayStepSwapRecord
      {
        Id = Guid.NewGuid(),
        UserId = userId,
        ReplacedOpportunityId = opportunityId,
        ReplacementOpportunityId = request.ReplacementOpportunityId
      });

      // a pin on the step swapped out would keep it on the path
      var pinned = _lockRepository.Query().FirstOrDefault(o => o.UserId == userId && o.OpportunityId == opportunityId);
      if (pinned != null) await _lockRepository.Delete(pinned);

      await _eventRepository.Create(new PathwayStepEventRecord
      {
        Id = Guid.NewGuid(),
        UserId = userId,
        OpportunityId = opportunityId,
        GoalKey = myYoma.Profile.GoalKey,
        StageOrder = stage.Order,
        Event = PathwayStepEventType.Swapped
      });

      return await _pathwayService.Get();
    }

    public async Task RecordEvent(PathwayStepEventRequest request)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      if (request.OpportunityId == Guid.Empty) throw new ValidationException("An opportunity is required");
      if (!Enum.IsDefined(request.Event)) throw new ValidationException($"Unknown step event '{request.Event}'");
      if (_opportunityService.GetByIdOrNull(request.OpportunityId, false, false, false) == null)
        throw new ValidationException($"Opportunity '{request.OpportunityId}' does not exist");

      var userId = GetUserId();

      if (request.Event == PathwayStepEventType.Shown)
      {
        var since = DateTimeOffset.UtcNow - ShownWindow;
        var repeat = _eventRepository.Query().Any(o => o.UserId == userId && o.OpportunityId == request.OpportunityId
          && o.Event == PathwayStepEventType.Shown && o.DateCreated >= since);
        if (repeat) return;
      }

      var myYoma = await _pathwayService.Get();
      var stage = FindStage(myYoma, request.OpportunityId);

      await _eventRepository.Create(new PathwayStepEventRecord
      {
        Id = Guid.NewGuid(),
        UserId = userId,
        OpportunityId = request.OpportunityId,
        GoalKey = myYoma.Profile.GoalKey,
        StageOrder = stage?.Order,
        Event = request.Event
      });
    }

    public PathwayHealth GetHealth()
    {
      var events = _eventRepository.Query().Count();
      var steps = _eventRepository.Query().Select(o => new { o.GoalKey, o.StageOrder, o.OpportunityId }).Distinct().Count();

      return new PathwayHealth
      {
        StepsTracked = steps,
        Events = events,
        DropOff = GetDropOff(),
        Note = events == 0
          ? "No step events yet: My Yoma records them as youth see, start, finish and swap steps."
          : $"Drop-off is shown only in aggregate, from at least {MinimumGroupSize} youth."
      };
    }

    public async Task<MyPathSummary> GetSummary()
    {
      return ToSummary(await _pathwayService.Get());
    }

    /// <summary>
    /// The YoID card from the path: steps per stage, and the next step, the first not completed in path order.
    /// </summary>
    public static MyPathSummary ToSummary(MyYoma myYoma)
    {
      ArgumentNullException.ThrowIfNull(myYoma, nameof(myYoma));

      var ordered = myYoma.Stages.OrderBy(o => o.Order).ToList();
      var steps = ordered.SelectMany(o => o.Courses).ToList();
      var next = steps.FirstOrDefault(o => o.Status != PathwayStepStatus.Completed);

      return new MyPathSummary
      {
        GoalLabel = myYoma.GoalLabel,
        TotalSteps = steps.Count,
        CompletedSteps = steps.Count(o => o.Status == PathwayStepStatus.Completed),
        NextStep = next == null ? null : new MyPathNextStep { OpportunityId = next.OpportunityId, Title = next.Title },
        Stages = [.. ordered.Select(o => new MyPathStage
        {
          Name = o.Name,
          Completed = o.Courses.Count(c => c.Status == PathwayStepStatus.Completed),
          Total = o.Courses.Count
        })]
      };
    }

    /// <summary>
    /// Share of started steps (youth and opportunity) that were not completed. Collecting unless the switch is on and at least
    /// <paramref name="minimumGroupSize"/> distinct youth started a step: no figure is ever shown from fewer.
    /// </summary>
    public static StepDropOff ComputeDropOff(IReadOnlyCollection<(Guid UserId, Guid OpportunityId)> started,
      IReadOnlySet<(Guid UserId, Guid OpportunityId)> completed, int minimumGroupSize, bool available)
    {
      ArgumentNullException.ThrowIfNull(started, nameof(started));
      ArgumentNullException.ThrowIfNull(completed, nameof(completed));

      if (!available) return StepDropOff.Collecting();

      var pairs = started.Distinct().ToList();
      var youth = pairs.Select(o => o.UserId).Distinct().Count();
      var minimum = Math.Max(minimumGroupSize, 1);
      if (youth < minimum) return StepDropOff.Collecting($"{StepDropOff.CollectingNote}: drop-off shows once {minimum} youth have started a step");

      var dropped = pairs.Count(o => !completed.Contains(o));
      return new StepDropOff
      {
        Status = StepDropOffStatus.Available,
        Rate = Math.Round((decimal)dropped / pairs.Count, 2, MidpointRounding.AwayFromZero),
        Note = $"Share of started steps not completed, from {youth} youth"
      };
    }
    #endregion

    #region Private Members
    private int MinimumGroupSize => Math.Max(_options.MinimumGroupSize, 1);

    private Guid GetUserId()
    {
      return _userService.GetByUsername(HttpContextAccessorHelper.GetUsername(_httpContextAccessor, false), false, false).Id;
    }

    private static PathwayStage? FindStage(MyYoma myYoma, Guid opportunityId) =>
      myYoma.Stages.FirstOrDefault(o => o.Courses.Any(c => c.OpportunityId == opportunityId));

    /// <summary>
    /// Reads nothing while the switch is off; reads completions only once the group is large enough to show a figure.
    /// </summary>
    private StepDropOff GetDropOff()
    {
      var available = _configuration.GetValue<bool>(DropOffAvailableKey);
      if (!available) return StepDropOff.Collecting();

      var started = _eventRepository.Query().Where(o => o.Event == PathwayStepEventType.Started)
        .Select(o => new { o.UserId, o.OpportunityId }).Distinct().ToList()
        .Select(o => (o.UserId, o.OpportunityId)).ToList();

      var completed = new HashSet<(Guid UserId, Guid OpportunityId)>();
      if (started.Select(o => o.UserId).Distinct().Count() >= MinimumGroupSize)
      {
        completed.UnionWith(_eventRepository.Query().Where(o => o.Event == PathwayStepEventType.Completed)
          .Select(o => new { o.UserId, o.OpportunityId }).Distinct().ToList().Select(o => (o.UserId, o.OpportunityId)));

        var userIds = started.Select(o => o.UserId).Distinct().ToList();
        var verificationId = _myOpportunityActionService.GetByName(MyOpportunity.Action.Verification.ToString()).Id;
        var completedId = _myOpportunityVerificationStatusService.GetByName(VerificationStatus.Completed.ToString()).Id;

        completed.UnionWith(_myOpportunityRepository.Query()
          .Where(o => userIds.Contains(o.UserId) && o.ActionId == verificationId && o.VerificationStatusId == completedId)
          .Select(o => new { o.UserId, o.OpportunityId }).ToList().Select(o => (o.UserId, o.OpportunityId)));
      }

      return ComputeDropOff(started, completed, MinimumGroupSize, true);
    }
    #endregion
  }
}
