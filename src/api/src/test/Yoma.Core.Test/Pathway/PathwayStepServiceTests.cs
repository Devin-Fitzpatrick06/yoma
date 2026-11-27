using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Models;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
using Yoma.Core.Domain.MyOpportunity.Models.Lookups;
using Yoma.Core.Domain.Opportunity.Interfaces;
using Yoma.Core.Domain.Pathway.Interfaces;
using Yoma.Core.Domain.Pathway.Models;
using Yoma.Core.Domain.Pathway.Services;
using MyOpportunityModel = Yoma.Core.Domain.MyOpportunity.Models.MyOpportunity;

namespace Yoma.Core.Test.Pathway
{
  /// <summary>
  /// The step service (iteration 4): lock, swap and alternatives, step events with the Shown repeat window, pathway health's empty
  /// state and the drop-off aggregate behind its minimum group size, and the YoID summary. Storage is in memory; the database
  /// shape is covered by the migration check in the wiring tests.
  /// </summary>
  public class PathwayStepServiceTests
  {
    #region Fakes
    internal sealed class MemoryRepository<T>(Action<T> stamp) : IRepository<T> where T : class
    {
      public List<T> Items { get; } = [];

      public IQueryable<T> Query() => Items.AsQueryable();

      public IQueryable<T> Query(LockMode lockMode) => Query();

      public Task<T> Create(T item)
      {
        stamp(item);
        Items.Add(item);
        return Task.FromResult(item);
      }

      public Task<T> Update(T item) => throw new NotSupportedException();

      public Task Delete(T item)
      {
        Items.Remove(item);
        return Task.CompletedTask;
      }
    }

    private sealed class Harness
    {
      public Guid UserId { get; } = Guid.NewGuid();
      public MyYoma MyYoma { get; set; } = null!;
      public bool Onboarded { get; set; } = true;
      public int GetCalls { get; private set; }
      public List<PathwayCourse> Alternatives { get; set; } = [];
      public HashSet<Guid> Opportunities { get; } = [];
      public Dictionary<string, string?> Configuration { get; } = [];
      public List<MyOpportunityModel> MyOpportunities { get; } = [];

      public MemoryRepository<PathwayStepLockRecord> Locks { get; } = new(o => o.DateCreated = o.DateCreated == default ? DateTimeOffset.UtcNow : o.DateCreated);
      public MemoryRepository<PathwayStepSwapRecord> Swaps { get; } = new(o => o.DateCreated = o.DateCreated == default ? DateTimeOffset.UtcNow : o.DateCreated);
      public MemoryRepository<PathwayStepEventRecord> Events { get; } = new(o => o.DateCreated = o.DateCreated == default ? DateTimeOffset.UtcNow : o.DateCreated);

      public static readonly Guid VerificationId = Guid.NewGuid();
      public static readonly Guid CompletedId = Guid.NewGuid();

      public PathwayStepService Service()
      {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "youth@example.org")], "test")) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(o => o.HttpContext).Returns(context);

        var users = new Mock<IUserService>();
        users.Setup(o => o.GetByUsername("youth@example.org", It.IsAny<bool>(), It.IsAny<bool>())).Returns(new User { Id = UserId, Username = "youth@example.org" });

        var pathway = new Mock<IPathwayService>();
        pathway.Setup(o => o.Get()).Returns(() =>
        {
          GetCalls++;
          return Onboarded ? Task.FromResult(MyYoma) : throw new EntityNotFoundException("You have not set up My Yoma yet");
        });

        var alternatives = new Mock<IPathwayStepAlternatives>();
        alternatives.Setup(o => o.List(It.IsAny<MyYoma>(), It.IsAny<Guid>())).Returns((MyYoma m, Guid id) =>
          m.Stages.Any(s => s.Courses.Any(c => c.OpportunityId == id)) ? [.. Alternatives] : throw new EntityNotFoundException("This step is not on your path"));

        var opportunities = new Mock<IOpportunityService>();
        opportunities.Setup(o => o.GetByIdOrNull(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<LockMode?>()))
          .Returns((Guid id, bool _, bool _, bool _, LockMode? _) => Opportunities.Contains(id) ? new Domain.Opportunity.Models.Opportunity { Id = id } : null);

        var myOpportunities = new Mock<IRepositoryBatchedWithNavigationAndCustomFieldFilter<MyOpportunityModel>>();
        myOpportunities.Setup(o => o.Query()).Returns(() => MyOpportunities.AsQueryable());

        var actions = new Mock<IMyOpportunityActionService>();
        actions.Setup(o => o.GetByName(It.IsAny<string>())).Returns(new MyOpportunityAction { Id = VerificationId, Name = "Verification" });
        var statuses = new Mock<IMyOpportunityVerificationStatusService>();
        statuses.Setup(o => o.GetByName(It.IsAny<string>())).Returns(new MyOpportunityVerificationStatus { Id = CompletedId, Name = "Completed" });

        return new PathwayStepService(accessor.Object,
          new ConfigurationBuilder().AddInMemoryCollection(Configuration).Build(),
          Options.Create(new PathwayOptions()),
          users.Object, pathway.Object, alternatives.Object, opportunities.Object,
          Locks, Swaps, Events, myOpportunities.Object, actions.Object, statuses.Object);
      }

      public PathwayStepStore Store() => new(Locks, Swaps);
    }

    private static PathwayCourse Step(string title, PathwayStepStatus status = PathwayStepStatus.NotStarted) =>
      new() { OpportunityId = Guid.NewGuid(), Title = title, OrganizationName = "Org", Reason = "Because", Status = status };

    private static MyYoma Path(params PathwayStage[] stages) => new()
    {
      Profile = new PathwayProfile { GoalKey = "marketing", GoalLabel = "Digital marketer" },
      GoalLabel = "Digital marketer",
      ThroughLine = "line",
      Stages = [.. stages]
    };

    private static PathwayStage Stage(int order, string name, params PathwayCourse[] courses) =>
      new() { Order = order, Name = name, Why = "why", Courses = [.. courses] };

    private static (Harness Harness, PathwayCourse First, PathwayCourse Second, PathwayCourse Third) Setup()
    {
      var first = Step("Digital Literacy", PathwayStepStatus.Completed);
      var second = Step("Social Media Basics", PathwayStepStatus.InProgress);
      var third = Step("Marketing Analytics");
      var harness = new Harness { MyYoma = Path(Stage(1, "Foundations", first), Stage(2, "Build skills", second, third)) };
      harness.Opportunities.UnionWith([first.OpportunityId, second.OpportunityId, third.OpportunityId]);
      harness.Alternatives = [Step("Alt one"), Step("Alt two"), Step("Alt three"), Step("Alt four")];
      return (harness, first, second, third);
    }
    #endregion

    #region Lock
    [Fact]
    public async Task Lock_StoresThePin_TheStoreListsIt_AndUnlockRemovesIt()
    {
      var (harness, _, second, _) = Setup();
      var service = harness.Service();

      await service.SetLocked(second.OpportunityId, new PathwayStepLockRequest { Locked = true });
      await service.SetLocked(second.OpportunityId, new PathwayStepLockRequest { Locked = true });

      Assert.Equal([second.OpportunityId], await harness.Store().ListLocked(harness.UserId));
      Assert.Empty(await harness.Store().ListLocked(Guid.NewGuid()));

      await service.SetLocked(second.OpportunityId, new PathwayStepLockRequest { Locked = false });

      Assert.Empty(await harness.Store().ListLocked(harness.UserId));
    }

    [Fact]
    public async Task Lock_ReturnsTheRebuiltPath()
    {
      var (harness, _, second, _) = Setup();

      var result = await harness.Service().SetLocked(second.OpportunityId, new PathwayStepLockRequest { Locked = true });

      Assert.Same(harness.MyYoma, result);
      Assert.Equal(2, harness.GetCalls); // once to check the step, once after storing
    }

    [Fact]
    public async Task Lock_AStepNotOnThePath_IsNotFound_AndStoresNothing()
    {
      var (harness, _, _, _) = Setup();

      await Assert.ThrowsAsync<EntityNotFoundException>(() => harness.Service().SetLocked(Guid.NewGuid(), new PathwayStepLockRequest { Locked = true }));
      Assert.Empty(harness.Locks.Items);
    }

    [Fact]
    public async Task Lock_NotOnboarded_IsNotFound()
    {
      var (harness, _, second, _) = Setup();
      harness.Onboarded = false;

      await Assert.ThrowsAsync<EntityNotFoundException>(() => harness.Service().SetLocked(second.OpportunityId, new PathwayStepLockRequest { Locked = true }));
    }
    #endregion

    #region Alternatives and swap
    [Fact]
    public async Task Alternatives_AreAtMostThree()
    {
      var (harness, _, _, third) = Setup();

      var result = await harness.Service().ListAlternatives(third.OpportunityId);

      Assert.Equal(3, result.Count);
      Assert.Equal(["Alt one", "Alt two", "Alt three"], result.Select(o => o.Title));
    }

    [Fact]
    public async Task Swap_StoresTheSwap_DropsThePin_RecordsASwappedEvent()
    {
      var (harness, _, _, third) = Setup();
      var service = harness.Service();
      var replacement = harness.Alternatives[1].OpportunityId;
      await service.SetLocked(third.OpportunityId, new PathwayStepLockRequest { Locked = true });

      var result = await service.Swap(third.OpportunityId, new PathwayStepSwapRequest { ReplacementOpportunityId = replacement });

      Assert.Same(harness.MyYoma, result);
      Assert.Equal([(third.OpportunityId, replacement)], await harness.Store().ListSwaps(harness.UserId));
      Assert.Empty(await harness.Store().ListLocked(harness.UserId));

      var swapped = Assert.Single(harness.Events.Items);
      Assert.Equal(PathwayStepEventType.Swapped, swapped.Event);
      Assert.Equal(third.OpportunityId, swapped.OpportunityId);
      Assert.Equal(2, swapped.StageOrder);
      Assert.Equal("marketing", swapped.GoalKey);
    }

    [Fact]
    public async Task Swaps_AreListedInTheOrderMade()
    {
      var (harness, _, second, third) = Setup();
      var service = harness.Service();

      await service.Swap(third.OpportunityId, new PathwayStepSwapRequest { ReplacementOpportunityId = harness.Alternatives[0].OpportunityId });
      harness.Swaps.Items[0].DateCreated = DateTimeOffset.UtcNow.AddMinutes(-5);
      await service.Swap(second.OpportunityId, new PathwayStepSwapRequest { ReplacementOpportunityId = harness.Alternatives[2].OpportunityId });

      Assert.Equal([(third.OpportunityId, harness.Alternatives[0].OpportunityId), (second.OpportunityId, harness.Alternatives[2].OpportunityId)],
        await harness.Store().ListSwaps(harness.UserId));
    }

    [Fact]
    public async Task Swap_ToSomethingNotAnAlternative_IsRejected()
    {
      var (harness, first, _, third) = Setup();
      var service = harness.Service();

      // the fourth alternative is past the three offered; another step on the path is not an alternative either
      await Assert.ThrowsAsync<ValidationException>(() => service.Swap(third.OpportunityId, new PathwayStepSwapRequest { ReplacementOpportunityId = first.OpportunityId }));
      await Assert.ThrowsAsync<ValidationException>(() => service.Swap(third.OpportunityId, new PathwayStepSwapRequest { ReplacementOpportunityId = harness.Alternatives[3].OpportunityId }));
      await Assert.ThrowsAsync<ValidationException>(() => service.Swap(third.OpportunityId, new PathwayStepSwapRequest { ReplacementOpportunityId = Guid.NewGuid() }));
      await Assert.ThrowsAsync<ValidationException>(() => service.Swap(third.OpportunityId, new PathwayStepSwapRequest { ReplacementOpportunityId = Guid.Empty }));
      await Assert.ThrowsAsync<ValidationException>(() => service.Swap(third.OpportunityId, new PathwayStepSwapRequest { ReplacementOpportunityId = third.OpportunityId }));

      Assert.Empty(harness.Swaps.Items);
      Assert.Empty(harness.Events.Items);
    }

    [Fact]
    public async Task Swap_AStepNotOnThePath_IsNotFound()
    {
      var (harness, _, _, _) = Setup();

      await Assert.ThrowsAsync<EntityNotFoundException>(() =>
        harness.Service().Swap(Guid.NewGuid(), new PathwayStepSwapRequest { ReplacementOpportunityId = harness.Alternatives[0].OpportunityId }));
    }
    #endregion

    #region Events
    [Fact]
    public async Task Event_StampsTheGoalAndTheStage()
    {
      var (harness, _, second, _) = Setup();

      await harness.Service().RecordEvent(new PathwayStepEventRequest { OpportunityId = second.OpportunityId, Event = PathwayStepEventType.Started });

      var row = Assert.Single(harness.Events.Items);
      Assert.Equal(harness.UserId, row.UserId);
      Assert.Equal("marketing", row.GoalKey);
      Assert.Equal(2, row.StageOrder);
      Assert.Equal(PathwayStepEventType.Started, row.Event);
    }

    [Fact]
    public async Task Event_ForAnOpportunityOffThePath_HasNoStage()
    {
      var (harness, _, _, _) = Setup();
      var other = Guid.NewGuid();
      harness.Opportunities.Add(other);

      await harness.Service().RecordEvent(new PathwayStepEventRequest { OpportunityId = other, Event = PathwayStepEventType.Dismissed });

      Assert.Null(Assert.Single(harness.Events.Items).StageOrder);
    }

    [Fact]
    public async Task Event_ForAnUnknownOpportunity_IsRejected()
    {
      var (harness, _, _, _) = Setup();
      var service = harness.Service();

      await Assert.ThrowsAsync<ValidationException>(() => service.RecordEvent(new PathwayStepEventRequest { OpportunityId = Guid.NewGuid(), Event = PathwayStepEventType.Shown }));
      await Assert.ThrowsAsync<ValidationException>(() => service.RecordEvent(new PathwayStepEventRequest { OpportunityId = Guid.Empty, Event = PathwayStepEventType.Shown }));
      Assert.Empty(harness.Events.Items);
    }

    [Fact]
    public async Task Event_OfAnUnknownType_IsRejected()
    {
      var (harness, first, _, _) = Setup();

      await Assert.ThrowsAsync<ValidationException>(() =>
        harness.Service().RecordEvent(new PathwayStepEventRequest { OpportunityId = first.OpportunityId, Event = (PathwayStepEventType)42 }));
    }

    [Fact]
    public async Task Shown_RepeatedWithinTheWindow_IsDropped_OtherEventsAreNot()
    {
      var (harness, first, second, _) = Setup();
      var service = harness.Service();

      await service.RecordEvent(new PathwayStepEventRequest { OpportunityId = first.OpportunityId, Event = PathwayStepEventType.Shown });
      await service.RecordEvent(new PathwayStepEventRequest { OpportunityId = first.OpportunityId, Event = PathwayStepEventType.Shown });
      await service.RecordEvent(new PathwayStepEventRequest { OpportunityId = second.OpportunityId, Event = PathwayStepEventType.Shown });
      await service.RecordEvent(new PathwayStepEventRequest { OpportunityId = first.OpportunityId, Event = PathwayStepEventType.Started });
      await service.RecordEvent(new PathwayStepEventRequest { OpportunityId = first.OpportunityId, Event = PathwayStepEventType.Started });

      Assert.Equal(1, harness.Events.Items.Count(o => o.OpportunityId == first.OpportunityId && o.Event == PathwayStepEventType.Shown));
      Assert.Equal(1, harness.Events.Items.Count(o => o.OpportunityId == second.OpportunityId && o.Event == PathwayStepEventType.Shown));
      Assert.Equal(2, harness.Events.Items.Count(o => o.Event == PathwayStepEventType.Started));
    }

    [Fact]
    public async Task Shown_AfterTheWindow_IsRecordedAgain()
    {
      var (harness, first, _, _) = Setup();
      var service = harness.Service();

      await service.RecordEvent(new PathwayStepEventRequest { OpportunityId = first.OpportunityId, Event = PathwayStepEventType.Shown });
      harness.Events.Items[0].DateCreated = DateTimeOffset.UtcNow - PathwayStepService.ShownWindow - TimeSpan.FromMinutes(1);
      await service.RecordEvent(new PathwayStepEventRequest { OpportunityId = first.OpportunityId, Event = PathwayStepEventType.Shown });

      Assert.Equal(2, harness.Events.Items.Count);
    }
    #endregion

    #region Health
    [Fact]
    public void Health_Empty_IsCollecting_WithNoRate()
    {
      var (harness, _, _, _) = Setup();

      var health = harness.Service().GetHealth();

      Assert.Equal(0, health.StepsTracked);
      Assert.Equal(0, health.Events);
      Assert.Equal(StepDropOffStatus.Collecting, health.DropOff.Status);
      Assert.Null(health.DropOff.Rate);
      Assert.Equal("Collecting from the pilot", health.DropOff.Note);
      Assert.False(string.IsNullOrWhiteSpace(health.Note));
    }

    private static void AddStarts(Harness harness, Guid opportunityId, int youth, int completing)
    {
      for (var index = 0; index < youth; index++)
      {
        var userId = Guid.NewGuid();
        harness.Events.Items.Add(new PathwayStepEventRecord { Id = Guid.NewGuid(), UserId = userId, OpportunityId = opportunityId, GoalKey = "marketing", StageOrder = 2, Event = PathwayStepEventType.Started, DateCreated = DateTimeOffset.UtcNow });
        if (index >= completing) continue;

        // half complete through an event, half only through a verified completion on Yoma
        if (index % 2 == 0)
          harness.Events.Items.Add(new PathwayStepEventRecord { Id = Guid.NewGuid(), UserId = userId, OpportunityId = opportunityId, GoalKey = "marketing", StageOrder = 2, Event = PathwayStepEventType.Completed, DateCreated = DateTimeOffset.UtcNow });
        else
          harness.MyOpportunities.Add(new MyOpportunityModel { Id = Guid.NewGuid(), UserId = userId, OpportunityId = opportunityId, ActionId = Harness.VerificationId, VerificationStatusId = Harness.CompletedId });
      }
    }

    [Fact]
    public void Health_CountsStepsAndEvents_AndStaysCollecting_WhileTheSwitchIsOff()
    {
      var (harness, _, second, third) = Setup();
      AddStarts(harness, second.OpportunityId, 30, 10);
      AddStarts(harness, third.OpportunityId, 1, 0);

      var health = harness.Service().GetHealth();

      Assert.Equal(2, health.StepsTracked);
      Assert.Equal(31 + 5, health.Events);
      Assert.Equal(StepDropOffStatus.Collecting, health.DropOff.Status);
      Assert.Null(health.DropOff.Rate);
      Assert.Equal(StepDropOff.CollectingNote, health.DropOff.Note);
    }

    [Fact]
    public void Health_SwitchOn_BelowTheMinimumGroup_StaysCollecting()
    {
      var (harness, _, second, _) = Setup();
      harness.Configuration[PathwayStepService.DropOffAvailableKey] = "true";
      AddStarts(harness, second.OpportunityId, 19, 5);

      var health = harness.Service().GetHealth();

      Assert.Equal(StepDropOffStatus.Collecting, health.DropOff.Status);
      Assert.Null(health.DropOff.Rate);
      Assert.StartsWith(StepDropOff.CollectingNote, health.DropOff.Note);
    }

    [Fact]
    public void Health_SwitchOn_FromTheMinimumGroup_ComputesTheRate_WithYomaCompletions()
    {
      var (harness, _, second, _) = Setup();
      harness.Configuration[PathwayStepService.DropOffAvailableKey] = "true";
      AddStarts(harness, second.OpportunityId, 20, 15);

      var health = harness.Service().GetHealth();

      Assert.Equal(StepDropOffStatus.Available, health.DropOff.Status);
      Assert.Equal(0.25m, health.DropOff.Rate);
    }

    [Fact]
    public void DropOff_CountsEachYouthAndStepOnce()
    {
      var user = Guid.NewGuid();
      var step = Guid.NewGuid();
      var started = Enumerable.Range(0, 20).Select(_ => (Guid.NewGuid(), step)).Append((user, step)).Append((user, step)).ToList();

      var result = PathwayStepService.ComputeDropOff(started, new HashSet<(Guid, Guid)> { (user, step) }, 20, true);

      Assert.Equal(StepDropOffStatus.Available, result.Status);
      Assert.Equal(Math.Round(20m / 21, 2), result.Rate);
      Assert.Equal(StepDropOff.CollectingNote, PathwayStepService.ComputeDropOff(started, new HashSet<(Guid, Guid)>(), 20, false).Note);
    }
    #endregion

    #region YoID summary
    [Fact]
    public async Task Summary_CountsSteps_PerStage_AndNamesTheFirstUnfinishedStep()
    {
      var (harness, _, second, _) = Setup();

      var summary = await harness.Service().GetSummary();

      Assert.Equal("Digital marketer", summary.GoalLabel);
      Assert.Equal(3, summary.TotalSteps);
      Assert.Equal(1, summary.CompletedSteps);
      Assert.Equal(second.OpportunityId, summary.NextStep!.OpportunityId);
      Assert.Equal("Social Media Basics", summary.NextStep.Title);
      Assert.Equal(["Foundations", "Build skills"], summary.Stages.Select(o => o.Name));
      Assert.Equal([1, 0], summary.Stages.Select(o => o.Completed));
      Assert.Equal([1, 2], summary.Stages.Select(o => o.Total));
    }

    [Fact]
    public void Summary_OfAFinishedPath_HasNoNextStep()
    {
      var summary = PathwayStepService.ToSummary(Path(Stage(1, "Only", Step("Done", PathwayStepStatus.Completed))));

      Assert.Null(summary.NextStep);
      Assert.Equal(1, summary.CompletedSteps);
    }

    [Fact]
    public async Task Summary_NotOnboarded_IsNotFound()
    {
      var (harness, _, _, _) = Setup();
      harness.Onboarded = false;

      await Assert.ThrowsAsync<EntityNotFoundException>(() => harness.Service().GetSummary());
    }
    #endregion
  }
}
