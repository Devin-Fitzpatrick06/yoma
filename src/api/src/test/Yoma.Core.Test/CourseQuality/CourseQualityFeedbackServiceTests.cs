using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces.Lookups;
using Yoma.Core.Domain.Entity.Models;
using Yoma.Core.Domain.Lookups.Interfaces;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity.Models;
using Yoma.Core.Domain.Opportunity.Models.Lookups;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.CourseQuality.Repositories;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;
using OpportunityModel = Yoma.Core.Domain.Opportunity.Models.Opportunity;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityFeedbackServiceTests(ITestOutputHelper output)
  {
    #region Harness
    private static readonly Guid TypeLearningId = Guid.NewGuid();
    private static readonly Guid StatusActiveId = Guid.NewGuid();
    private static readonly Guid OrganizationActiveId = Guid.NewGuid();
    private const string Learner = "learner@example.org";
    private const string SecretComment = "My teacher's name is Ms Example and I am 14";

    private readonly List<OpportunityModel> _opportunities = [];
    private readonly List<CourseQualityAssessmentRecord> _assessments = [];
    private readonly List<CourseQualityGradeRecord> _grades = [];
    private readonly List<CourseQualityLearnerFeedbackRecord> _feedback = [];
    private readonly Dictionary<string, Guid> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _adminOf = [];
    private readonly List<string> _logs = [];
    private readonly CourseQualityOptions _options = new() { LearnerFeedback = new CourseQualityLearnerFeedbackOptions { Enabled = true } };
    private ClaimsPrincipal _principal = Principal(Learner, Constants.Role_User);

    private static ClaimsPrincipal Principal(string name, params string[] roles) =>
      new(new ClaimsIdentity([new Claim(ClaimTypes.Name, name), .. roles.Select(o => new Claim(ClaimTypes.Role, o))], "test"));

    private sealed class ListLogger<T>(List<string> logs) : ILogger<T>
    {
      public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

      public bool IsEnabled(LogLevel logLevel) => true;

      public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        logs.Add(formatter(state, exception) + (exception?.Message ?? string.Empty));
    }

    private Mock<IOpportunityTypeService> Types()
    {
      var types = new Mock<IOpportunityTypeService>();
      types.Setup(o => o.GetByName("Learning")).Returns(new OpportunityType { Id = TypeLearningId, Name = "Learning" });
      return types;
    }

    private Mock<IRepository<CourseQualityLearnerFeedbackRecord>> FeedbackRepository()
    {
      var repository = new Mock<IRepository<CourseQualityLearnerFeedbackRecord>>();
      repository.Setup(o => o.Query()).Returns(() => _feedback.AsQueryable());
      repository.Setup(o => o.Create(It.IsAny<CourseQualityLearnerFeedbackRecord>())).Returns((CourseQualityLearnerFeedbackRecord r) =>
      {
        Assert.DoesNotContain(_feedback, o => o.UserId == r.UserId && o.OpportunityId == r.OpportunityId);
        r.DateCreated = r.DateModified = DateTimeOffset.UtcNow;
        _feedback.Add(r);
        return Task.FromResult(r);
      });
      repository.Setup(o => o.Update(It.IsAny<CourseQualityLearnerFeedbackRecord>())).Returns((CourseQualityLearnerFeedbackRecord r) =>
      {
        r.DateModified = DateTimeOffset.UtcNow;
        return Task.FromResult(r);
      });
      return repository;
    }

    private static Mock<IDistributedLockService> Locks()
    {
      var locks = new Mock<IDistributedLockService>();
      locks.Setup(o => o.RunWithLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<Task>>(), It.IsAny<string>()))
        .Returns((string _, TimeSpan _, Func<Task> action, string _) => action());
      return locks;
    }

    private IHttpContextAccessor Accessor()
    {
      var accessor = new Mock<IHttpContextAccessor>();
      accessor.SetupGet(o => o.HttpContext).Returns(() => new DefaultHttpContext { User = _principal });
      return accessor.Object;
    }

    private Mock<IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<OpportunityModel>> OpportunityRepository()
    {
      var repository = new Mock<IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<OpportunityModel>>();
      repository.Setup(o => o.Query()).Returns(() => _opportunities.AsQueryable());
      return repository;
    }

    private CourseQualityGradingService Grading()
    {
      var gradeRepository = new Mock<IRepositoryBatched<CourseQualityGradeRecord>>();
      gradeRepository.Setup(o => o.Query()).Returns(() => _grades.AsQueryable());
      gradeRepository.Setup(o => o.Create(It.IsAny<List<CourseQualityGradeRecord>>())).Returns((List<CourseQualityGradeRecord> items) => { _grades.AddRange(items); return Task.FromResult(items); });
      gradeRepository.Setup(o => o.Update(It.IsAny<List<CourseQualityGradeRecord>>())).Returns((List<CourseQualityGradeRecord> items) => Task.FromResult(items));
      gradeRepository.Setup(o => o.Delete(It.IsAny<List<CourseQualityGradeRecord>>())).Returns((List<CourseQualityGradeRecord> items) => { _grades.RemoveAll(items.Contains); return Task.CompletedTask; });

      var skills = new Mock<IRepository<OpportunitySkill>>();
      skills.Setup(o => o.Query()).Returns(() => Array.Empty<OpportunitySkill>().AsQueryable());
      var languages = new Mock<IRepository<OpportunityLanguage>>();
      languages.Setup(o => o.Query()).Returns(() => Array.Empty<OpportunityLanguage>().AsQueryable());
      var languageService = new Mock<ILanguageService>();
      languageService.Setup(o => o.List()).Returns([]);
      var myOpportunities = new Mock<IMyOpportunityService>();
      myOpportunities.Setup(o => o.ListParticipationCounts()).Returns([]);
      var assessments = new Mock<IRepository<CourseQualityAssessmentRecord>>();
      assessments.Setup(o => o.Query()).Returns(() => _assessments.AsQueryable());
      var rubrics = new Mock<IRepository<CourseQualityGradeRubricRecord>>();
      rubrics.Setup(o => o.Query()).Returns(() => Array.Empty<CourseQualityGradeRubricRecord>().AsQueryable());
      var strategy = new Mock<IExecutionStrategyService>();
      strategy.Setup(o => o.ExecuteInExecutionStrategyAsync(It.IsAny<Func<Task>>(), It.IsAny<bool>())).Returns((Func<Task> body, bool _) => body());

      return new CourseQualityGradingService(NullLogger<CourseQualityGradingService>.Instance, Accessor(), Options.Create(_options), OpportunityRepository().Object,
        skills.Object, languages.Object, languageService.Object, myOpportunities.Object, assessments.Object, gradeRepository.Object, rubrics.Object,
        FeedbackRepository().Object, Locks().Object, strategy.Object, Mock.Of<Hangfire.IBackgroundJobClient>(), Mock.Of<Domain.Pathway.Interfaces.IPathwayOutcomeService>());
    }

    private CourseQualityFeedbackService Service(ICourseQualityGradingService? grading = null)
    {
      var users = new Mock<IUserService>();
      users.Setup(o => o.GetByUsername(It.IsAny<string>(), false, false)).Returns((string name, bool _, bool _) =>
      {
        if (!_users.TryGetValue(name, out var id)) _users[name] = id = Guid.NewGuid();
        return new User { Id = id, Email = name };
      });

      var opportunities = new Mock<IOpportunityService>();
      opportunities.Setup(o => o.GetByIdOrNull(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), null))
        .Returns((Guid id, bool _, bool _, bool _, LockMode? _) => _opportunities.SingleOrDefault(o => o.Id == id));

      // the real rule: an Admin passes; an OrganizationAdmin passes only for an organisation they administer
      var organizations = new Mock<IOrganizationService>();
      organizations.Setup(o => o.IsAdmin(It.IsAny<Guid>(), true)).Returns((Guid id, bool _) =>
        _principal.IsInRole(Constants.Role_Admin) || (_principal.IsInRole(Constants.Role_OrganizationAdmin) && _adminOf.Contains(id))
          ? true : throw new SecurityException("Unauthorized"));

      var statuses = new Mock<IOpportunityStatusService>();
      statuses.Setup(o => o.GetByName("Active")).Returns(new OpportunityStatus { Id = StatusActiveId, Name = "Active" });
      var organizationStatuses = new Mock<IOrganizationStatusService>();
      organizationStatuses.Setup(o => o.GetByName("Active")).Returns(new Domain.Entity.Models.Lookups.OrganizationStatus { Id = OrganizationActiveId, Name = "Active" });

      var assessments = new Mock<IRepository<CourseQualityAssessmentRecord>>();
      assessments.Setup(o => o.Query()).Returns(() => _assessments.AsQueryable());

      return new CourseQualityFeedbackService(new ListLogger<CourseQualityFeedbackService>(_logs), Accessor(), Options.Create(_options), users.Object,
        opportunities.Object, organizations.Object, Types().Object, statuses.Object, organizationStatuses.Object, OpportunityRepository().Object,
        assessments.Object, FeedbackRepository().Object, grading ?? Grading(), Locks().Object);
    }

    private OpportunityModel AddCourse(Guid? organizationId = null)
    {
      var opportunity = CleanListing();
      opportunity.OrganizationId = organizationId ?? opportunity.OrganizationId;
      opportunity.TypeId = TypeLearningId;
      opportunity.StatusId = StatusActiveId;
      opportunity.OrganizationStatusId = OrganizationActiveId;
      _opportunities.Add(opportunity);

      var assessment = CourseQualityEngine.Assess(opportunity, WorkingLink(), Ai(), new CourseQualityOptions(), Now);
      var record = assessment.ToRecord(Now, new string('a', 64));
      record.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, HasCourseJsonLd = true, OutcomeCount = 4, ModuleCount = 6 }.Serialize();
      _assessments.Add(record);

      return opportunity;
    }

    private Guid UserId(string name = Learner)
    {
      if (!_users.TryGetValue(name, out var id)) _users[name] = id = Guid.NewGuid();
      return id;
    }

    private CourseQualityLearnerFeedbackRecord Row(OpportunityModel course, string user = Learner) =>
      _feedback.Single(o => o.OpportunityId == course.Id && o.UserId == UserId(user));

    private CourseQualityLearnerFeedbackRecord Seed(OpportunityModel course, CourseFeedbackAnswer answer, string user = Learner, int? rating = null, bool? recommend = null,
      string? comment = null, DateTimeOffset? modified = null, List<string>? good = null)
    {
      var record = new CourseQualityLearnerFeedbackRecord
      {
        Id = Guid.NewGuid(),
        UserId = UserId(user),
        OpportunityId = course.Id,
        Answer = answer,
        DateLeft = DateTimeOffset.UtcNow.AddDays(-10),
        Rating = rating,
        WouldRecommend = recommend,
        Comment = comment,
        Good = JsonSerializer.Serialize(good ?? []),
        DateCreated = modified ?? DateTimeOffset.UtcNow,
        DateModified = modified ?? DateTimeOffset.UtcNow
      };
      _feedback.Add(record);
      return record;
    }

    private static CourseFeedbackSubmission Taken(int? rating = 4, bool? recommend = true, string? comment = null, List<string>? good = null, List<string>? bad = null) => new()
    {
      Answer = CourseFeedbackAnswer.Taken,
      Rating = rating,
      Completed = true,
      WouldRecommend = recommend,
      MatchedDescription = true,
      Good = good ?? [],
      Bad = bad ?? [],
      Comment = comment
    };

    private static CourseFeedbackSubmission Answer(CourseFeedbackAnswer answer) => new() { Answer = answer };
    #endregion

    #region Options
    [Fact]
    public void Options_AreTheConfiguredTags_EvenWhenTurnedOff()
    {
      _options.LearnerFeedback.Enabled = false;

      var options = Service().GetOptions();

      Assert.Equal(CourseQualityLearnerFeedbackOptions.DefaultGood.Select(o => o.Key), options.Good.Select(o => o.Key));
      Assert.Equal(CourseQualityLearnerFeedbackOptions.DefaultBad.Select(o => o.Key), options.Bad.Select(o => o.Key));

      _options.LearnerFeedback.Good = [new CourseFeedbackTag { Key = "fun", Label = "Fun" }];
      Assert.Equal("fun", Assert.Single(Service().GetOptions().Good).Key);
    }
    #endregion

    #region Left
    [Fact]
    public async Task Left_CreatesAPendingRow()
    {
      var course = AddCourse();

      await Service().RecordLeft(course.Id);

      var row = Row(course);
      Assert.Equal(CourseFeedbackAnswer.Pending, row.Answer);
      Assert.True(row.DateLeft > DateTimeOffset.UtcNow.AddMinutes(-1));
      Assert.Null(row.DateAsked);
    }

    [Theory]
    [InlineData(CourseFeedbackAnswer.Pending, true)]
    [InlineData(CourseFeedbackAnswer.NotYet, true)]
    [InlineData(CourseFeedbackAnswer.Taken, false)]
    [InlineData(CourseFeedbackAnswer.NotTaken, false)]
    [InlineData(CourseFeedbackAnswer.Dismissed, false)]
    public async Task Left_MovesDateLeft_OnlyForOpenRows_AndNeverReopens(CourseFeedbackAnswer answer, bool moves)
    {
      var course = AddCourse();
      var row = Seed(course, answer);
      var before = row.DateLeft;

      await Service().RecordLeft(course.Id);

      Assert.Equal(answer, row.Answer);
      Assert.Equal(moves, row.DateLeft > before);
      Assert.Single(_feedback);
    }

    [Fact]
    public async Task Left_DoesNothing_WhenTurnedOff()
    {
      _options.LearnerFeedback.Enabled = false;
      var course = AddCourse();

      await Service().RecordLeft(course.Id);

      Assert.Empty(_feedback);
    }

    [Fact]
    public async Task Left_RejectsMissing_NonLearning_AndHiddenCourses()
    {
      await Assert.ThrowsAsync<EntityNotFoundException>(() => Service().RecordLeft(Guid.NewGuid()));

      var job = AddCourse();
      job.Type = Domain.Opportunity.Type.Job;
      await Assert.ThrowsAsync<ValidationException>(() => Service().RecordLeft(job.Id));

      var hidden = AddCourse();
      hidden.Hidden = true;
      await Assert.ThrowsAsync<ValidationException>(() => Service().RecordLeft(hidden.Id));

      Assert.Empty(_feedback);
    }
    #endregion

    #region Pending
    [Fact]
    public async Task Pending_WaitsForTheDelay_ThenMarksAsked()
    {
      var course = AddCourse();
      await Service().RecordLeft(course.Id);

      Assert.Empty(await Service().ListPending());

      Row(course).DateLeft = DateTimeOffset.UtcNow.AddMinutes(-_options.LearnerFeedback.PromptDelayInMinutes - 1);
      var prompt = Assert.Single(await Service().ListPending());

      Assert.Equal(course.Id, prompt.OpportunityId);
      Assert.Equal(course.Title, prompt.OpportunityTitle);
      Assert.Equal(course.OrganizationName, prompt.OrganizationName);
      Assert.NotNull(Row(course).DateAsked);
    }

    [Fact]
    public async Task Pending_ReturnsOnlyTheOldest_OfThisLearner_ForVisibleCourses()
    {
      var older = AddCourse();
      var newer = AddCourse();
      var hidden = AddCourse();
      Seed(newer, CourseFeedbackAnswer.Pending).DateLeft = DateTimeOffset.UtcNow.AddDays(-2);
      Seed(older, CourseFeedbackAnswer.Pending).DateLeft = DateTimeOffset.UtcNow.AddDays(-3);
      Seed(hidden, CourseFeedbackAnswer.Pending).DateLeft = DateTimeOffset.UtcNow.AddDays(-9);
      hidden.Hidden = true;
      Seed(newer, CourseFeedbackAnswer.Pending, "someone@example.org").DateLeft = DateTimeOffset.UtcNow.AddDays(-30);

      var prompt = Assert.Single(await Service().ListPending());

      Assert.Equal(older.Id, prompt.OpportunityId);
    }

    [Fact]
    public async Task Pending_IsEmpty_WhenTurnedOff()
    {
      var course = AddCourse();
      Seed(course, CourseFeedbackAnswer.Pending);
      _options.LearnerFeedback.Enabled = false;

      Assert.Empty(await Service().ListPending());
      Assert.Null(Row(course).DateAsked);
    }

    [Fact]
    public async Task NotYet_SnoozesForTheConfiguredDays_ThenAsksAgain()
    {
      var course = AddCourse();
      Seed(course, CourseFeedbackAnswer.Pending);

      await Service().Submit(course.Id, Answer(CourseFeedbackAnswer.NotYet));

      var row = Row(course);
      Assert.Equal(CourseFeedbackAnswer.NotYet, row.Answer);
      Assert.InRange(row.SnoozeUntil!.Value, DateTimeOffset.UtcNow.AddDays(3).AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(3).AddMinutes(1));
      Assert.Empty(await Service().ListPending());

      row.SnoozeUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
      Assert.Single(await Service().ListPending());
    }
    #endregion

    #region Submit lifecycle
    [Theory]
    [InlineData(CourseFeedbackAnswer.Dismissed)]
    [InlineData(CourseFeedbackAnswer.NotTaken)]
    public async Task DismissedAndNotTaken_AreFinal(CourseFeedbackAnswer final)
    {
      var course = AddCourse();
      Seed(course, CourseFeedbackAnswer.Pending);

      await Service().Submit(course.Id, Answer(final));

      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(course.Id, Answer(CourseFeedbackAnswer.NotYet)));
      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(course.Id, Taken()));
      await Service().RecordLeft(course.Id);

      Assert.Equal(final, Row(course).Answer);
      Assert.Empty(await Service().ListPending());
    }

    [Fact]
    public async Task TakenAnswers_RequestARegrade_OthersAndRepeatsDoNot()
    {
      var course = AddCourse();
      Seed(course, CourseFeedbackAnswer.Pending);
      var grading = new Mock<ICourseQualityGradingService>();
      grading.Setup(o => o.GetRubric()).Returns(Domain.CourseQuality.Helpers.CourseQualityGrading.Configured(new CourseQualityOptions()));

      await Service(grading.Object).Submit(course.Id, Answer(CourseFeedbackAnswer.NotYet));
      grading.Verify(o => o.RequestRegrade(), Times.Never);

      await Service(grading.Object).Submit(course.Id, Taken(rating: 4));
      await Service(grading.Object).Submit(course.Id, Taken(rating: 4));
      grading.Verify(o => o.RequestRegrade(), Times.Once);
      grading.Verify(o => o.Regrade(), Times.Never);
    }

    [Fact]
    public async Task Taken_CanUpdateItsSurvey_ButCannotChangeAnswer()
    {
      var course = AddCourse();
      Seed(course, CourseFeedbackAnswer.Pending);

      await Service().Submit(course.Id, Taken(rating: 3));
      await Service().Submit(course.Id, Taken(rating: 5, good: ["practicalSkills"]));

      var row = Row(course);
      Assert.Equal(5, row.Rating);
      Assert.Equal(["practicalSkills"], JsonSerializer.Deserialize<List<string>>(row.Good));
      Assert.Null(row.SnoozeUntil);

      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(course.Id, Answer(CourseFeedbackAnswer.NotTaken)));
      Assert.Equal(CourseFeedbackAnswer.Taken, row.Answer);
    }

    [Fact]
    public async Task Taken_SameSurveyAgain_WritesNothing()
    {
      var course = AddCourse();
      await Service().Submit(course.Id, Taken(good: ["goodLength"]));
      var modified = Row(course).DateModified = DateTimeOffset.UtcNow.AddHours(-1);

      await Service().Submit(course.Id, Taken(good: ["goodLength"]));

      Assert.Equal(modified, Row(course).DateModified);
    }

    [Fact]
    public async Task Submit_WithoutALeftRow_CreatesOne()
    {
      var course = AddCourse();

      await Service().Submit(course.Id, Taken());

      var row = Row(course);
      Assert.Equal(CourseFeedbackAnswer.Taken, row.Answer);
      Assert.True(row.DateLeft > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Submit_IsRejected_WhenTurnedOff()
    {
      _options.LearnerFeedback.Enabled = false;
      var course = AddCourse();

      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(course.Id, Taken()));
      Assert.Empty(_feedback);
    }

    [Fact]
    public async Task Submit_AcceptsTheWebsNonTakenBody_AndTakenWithEverythingSkipped()
    {
      var course = AddCourse();
      var other = AddCourse();

      // exactly what buildFeedbackSubmission sends for any answer but Taken
      await Service().Submit(course.Id, new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.NotTaken, Good = [], Bad = [], Comment = null });
      await Service().Submit(other.Id, new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.Taken, Good = null!, Bad = null! });

      Assert.Equal(CourseFeedbackAnswer.NotTaken, Row(course).Answer);
      Assert.Null(Row(other).Rating);
      Assert.Equal("[]", Row(other).Good);
    }
    #endregion

    #region Validation
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Rating_MustBeOneToFive(int rating)
    {
      var course = AddCourse();

      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(course.Id, Taken(rating: rating)));
      Assert.Empty(_feedback);
    }

    [Fact]
    public async Task Tags_MustBeConfigured_AndAreDeduplicated()
    {
      var course = AddCourse();

      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(course.Id, Taken(good: ["madeUp"])));
      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(course.Id, Taken(bad: ["clearExplanations"])));

      await Service().Submit(course.Id, Taken(good: ["goodLength", "GOODLENGTH", "goodLength"], bad: ["tooLong", "tooLong"]));

      Assert.Equal(["goodLength"], JsonSerializer.Deserialize<List<string>>(Row(course).Good));
      Assert.Equal(["tooLong"], JsonSerializer.Deserialize<List<string>>(Row(course).Bad));
    }

    [Fact]
    public async Task Comment_IsTrimmed_BlankIsNull_AndLengthIsCapped()
    {
      var a = AddCourse();
      var b = AddCourse();
      var c = AddCourse();

      await Service().Submit(a.Id, Taken(comment: "   "));
      await Service().Submit(b.Id, Taken(comment: "  " + new string('x', 500) + "  "));
      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(c.Id, Taken(comment: new string('x', 501))));

      Assert.Null(Row(a).Comment);
      Assert.Equal(500, Row(b).Comment!.Length);
      Assert.DoesNotContain(_feedback, o => o.OpportunityId == c.Id);
    }

    public static TheoryData<CourseFeedbackSubmission> SurveyWithoutTaken() =>
    [
      new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.NotYet, Rating = 4 },
      new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.NotTaken, Completed = false },
      new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.Dismissed, WouldRecommend = true },
      new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.NotYet, MatchedDescription = true },
      new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.NotTaken, Good = ["goodLength"] },
      new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.NotTaken, Bad = ["tooLong"] },
      new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.Dismissed, Comment = "hello" },
      new CourseFeedbackSubmission { Answer = CourseFeedbackAnswer.Pending },
      new CourseFeedbackSubmission { Answer = (CourseFeedbackAnswer)42 }
    ];

    [Theory]
    [MemberData(nameof(SurveyWithoutTaken))]
    public async Task SurveyFields_AreRejected_WithoutTaken(CourseFeedbackSubmission submission)
    {
      var course = AddCourse();
      Seed(course, CourseFeedbackAnswer.Pending);

      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(course.Id, submission));
      Assert.Equal(CourseFeedbackAnswer.Pending, Row(course).Answer);
    }

    [Fact]
    public async Task Comment_IsNeverLogged()
    {
      var course = AddCourse();

      await Service().Submit(course.Id, Taken(comment: SecretComment));
      await Assert.ThrowsAsync<ValidationException>(() => Service().Submit(course.Id, Taken(comment: SecretComment + new string('x', 500))));

      Assert.NotEmpty(_logs);
      Assert.DoesNotContain(_logs, o => o.Contains("Ms Example", StringComparison.Ordinal));
    }
    #endregion

    #region Summary
    [Fact]
    public void Summary_CountsTakenAnswers()
    {
      _principal = Principal("admin@yoma.world", Constants.Role_Admin, Constants.Role_User);
      var course = AddCourse();
      Seed(course, CourseFeedbackAnswer.Taken, "a@x.org", rating: 5, recommend: true, good: ["goodLength", "practicalSkills"]).Completed = true;
      Seed(course, CourseFeedbackAnswer.Taken, "b@x.org", rating: 2, recommend: false, good: ["goodLength"]).MatchedDescription = false;
      Seed(course, CourseFeedbackAnswer.Taken, "c@x.org");
      Seed(course, CourseFeedbackAnswer.NotTaken, "d@x.org", rating: 1);


      var summary = Service().GetSummary(course.Id);

      Assert.Equal(3, summary.Responses);
      Assert.Equal(1, summary.Completed);
      Assert.Equal(3.5m, summary.AverageRating);
      Assert.Equal(1, summary.WouldRecommendYes);
      Assert.Equal(1, summary.WouldRecommendNo);
      Assert.Equal(0, summary.MatchedDescriptionYes);
      Assert.Equal(1, summary.MatchedDescriptionNo);
      Assert.Equal(new[] { ("goodLength", 2), ("practicalSkills", 1) }, summary.Good.Select(o => (o.Key, o.Count)));
      Assert.Equal("Good length", summary.Good[0].Label);
      Assert.Empty(summary.Bad);
    }

    [Fact]
    public void Summary_IsZeros_WithNoAnswers_And404_ForAMissingCourse()
    {
      _principal = Principal("admin@yoma.world", Constants.Role_Admin);
      var course = AddCourse();
      Seed(course, CourseFeedbackAnswer.Dismissed);

      var summary = Service().GetSummary(course.Id);

      Assert.Equal(course.Id, summary.OpportunityId);
      Assert.Equal(0, summary.Responses);
      Assert.Null(summary.AverageRating);
      Assert.Null(summary.AdjustedRating);
      Assert.Empty(summary.RecentComments);
      Assert.Throws<EntityNotFoundException>(() => Service().GetSummary(Guid.NewGuid()));
    }

    [Fact]
    public async Task Summary_AdjustedRating_IsTheGradingFactor_OnTheRatingScale()
    {
      var organizationId = Guid.NewGuid();
      var course = AddCourse(organizationId);
      var sibling = AddCourse(organizationId);
      AddCourse();
      Seed(course, CourseFeedbackAnswer.Taken, "a@x.org", rating: 5, recommend: true);
      Seed(course, CourseFeedbackAnswer.Taken, "b@x.org", rating: 4, recommend: true);
      Seed(course, CourseFeedbackAnswer.Taken, "c@x.org", rating: 5);
      Seed(sibling, CourseFeedbackAnswer.Taken, "a@x.org", rating: 1, recommend: false);
      Seed(sibling, CourseFeedbackAnswer.Taken, "b@x.org", rating: 2, recommend: false);

      await Grading().Regrade();

      _principal = Principal("admin@yoma.world", Constants.Role_Admin);
      foreach (var opportunity in new[] { course, sibling })
      {
        var summary = Service().GetSummary(opportunity.Id);
        var factor = _grades.Single(o => o.OpportunityId == opportunity.Id).ToResult().Factors.Single(o => o.Factor == CourseGradeFactor.LearnerFeedback);

        Assert.True(factor.HasData);
        Assert.Equal(factor.Score, 25 * (summary.AdjustedRating!.Value - 1));
      }

      // shrunk: towards the provider's mean, so above the raw average for the weak sibling and below for the strong course
      var strong = Service().GetSummary(course.Id);
      Assert.True(strong.AdjustedRating < strong.AverageRating);
      var weak = Service().GetSummary(sibling.Id);
      Assert.True(weak.AdjustedRating > weak.AverageRating);
    }

    [Fact]
    public void Summary_OrganizationAdmin_OfTheCourse_GetsNoComments_AndOfAnotherIsRefused()
    {
      var course = AddCourse();
      Seed(course, CourseFeedbackAnswer.Taken, "a@x.org", rating: 5, comment: SecretComment);

      _principal = Principal("partner@example.org", Constants.Role_OrganizationAdmin, Constants.Role_User);
      Assert.Throws<SecurityException>(() => Service().GetSummary(course.Id));

      _adminOf.Add(course.OrganizationId);
      var summary = Service().GetSummary(course.Id);

      Assert.Equal(1, summary.Responses);
      Assert.Empty(summary.RecentComments);
      Assert.DoesNotContain("Ms Example", JsonSerializer.Serialize(summary));

      _principal = Principal(Learner, Constants.Role_User);
      Assert.Throws<SecurityException>(() => Service().GetSummary(course.Id));
    }

    [Fact]
    public void Summary_Admin_GetsTheTenNewestComments()
    {
      _principal = Principal("admin@yoma.world", Constants.Role_Admin);
      var course = AddCourse();
      for (var i = 0; i < 12; i++)
        Seed(course, CourseFeedbackAnswer.Taken, $"{i}@x.org", rating: 4, comment: $"comment {i}", modified: DateTimeOffset.UtcNow.AddMinutes(i));
      Seed(course, CourseFeedbackAnswer.Taken, "blank@x.org", rating: 4, modified: DateTimeOffset.UtcNow.AddHours(1));

      var comments = Service().GetSummary(course.Id).RecentComments;

      Assert.Equal(Enumerable.Range(2, 10).Reverse().Select(i => $"comment {i}"), comments);
    }
    #endregion

    #region Assessment mapping
    [Fact]
    public void ListSummaries_LeavesOutCoursesNobodyTook_AndHidesCommentsFromNonAdmins()
    {
      var answered = AddCourse();
      var notTaken = AddCourse();
      var silent = AddCourse();
      Seed(answered, CourseFeedbackAnswer.Taken, rating: 4, comment: SecretComment);
      Seed(notTaken, CourseFeedbackAnswer.NotTaken);
      var map = new[] { answered, notTaken, silent }.ToDictionary(o => o.Id, o => o.OrganizationId);

      _principal = Principal("admin@yoma.world", Constants.Role_Admin);
      var summaries = Service().ListSummaries(map);
      Assert.Equal([answered.Id], summaries.Keys);
      Assert.Equal([SecretComment], summaries[answered.Id].RecentComments);

      _principal = Principal("partner@example.org", Constants.Role_OrganizationAdmin);
      Assert.Empty(Service().ListSummaries(map)[answered.Id].RecentComments);

      Assert.Empty(Service().ListSummaries([]));
    }
    #endregion

    #region Regrade
    [Fact]
    public async Task TakenAnswer_MarksGradesStale_UntilTheNextRegrade()
    {
      var course = AddCourse();
      var grading = Grading();
      await grading.Regrade();
      foreach (var grade in _grades) grade.DateGraded = DateTimeOffset.UtcNow.AddMinutes(-5);
      Assert.False(grading.HasUngradedFeedback());

      await Service().Submit(course.Id, Answer(CourseFeedbackAnswer.NotYet));
      Assert.False(grading.HasUngradedFeedback());

      await Service().Submit(course.Id, Taken(rating: 5));
      Assert.True(grading.HasUngradedFeedback());

      await grading.Regrade();
      Assert.False(grading.HasUngradedFeedback());
      Assert.Equal(1, _grades.Single().FeedbackResponses);
    }

    [Fact]
    public async Task Job_RequestsARegrade_WhenOnlyFeedbackChanged()
    {
      var service = new Mock<ICourseQualityService>();
      service.Setup(o => o.QueryDue(It.IsAny<DateTimeOffset>())).Returns(Array.Empty<Guid>().AsQueryable());
      var grading = new Mock<ICourseQualityGradingService>();
      var locks = new Mock<IDistributedLockService>();
      locks.Setup(o => o.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>())).ReturnsAsync(true);
      var lifetime = new Mock<IHostApplicationLifetime>();
      lifetime.SetupGet(o => o.ApplicationStopping).Returns(CancellationToken.None);
      var job = new CourseQualityBackgroundService(NullLogger<CourseQualityBackgroundService>.Instance,
        Options.Create(new ScheduleJobOptions { DefaultScheduleMaxIntervalInHours = 2 }), Options.Create(new CourseQualityOptions { Enabled = true }), service.Object, grading.Object, locks.Object, lifetime.Object);

      await job.ProcessAssessments();
      grading.Verify(o => o.RequestRegrade(), Times.Never);

      grading.Setup(o => o.HasUngradedFeedback()).Returns(true);
      await job.ProcessAssessments();
      grading.Verify(o => o.RequestRegrade(), Times.Once);
    }

    [Fact]
    public void UngradedFeedbackQuery_TranslatesToOneSqlQuery()
    {
      using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=localhost;Database=none", o => o.UseNetTopologySuite()).Options);

      var sql = CourseQualityGradingService.QueryUngradedFeedback(new CourseQualityLearnerFeedbackRepository(context).Query(), new CourseQualityGradeRepository(context).Query()).ToQueryString();

      Assert.Contains("\"CourseQuality\".\"LearnerFeedback\"", sql);
      Assert.Contains("\"CourseQuality\".\"Grade\"", sql);
      Assert.Contains("EXISTS", sql);
    }

    /// <summary>
    /// The measurement behind debouncing: one regrade reads and grades the whole catalogue, so it runs once per batch, not per answer.
    /// </summary>
    [Fact]
    public async Task Regrade_OfTheFullCatalogue_IsMeasured()
    {
      var organizations = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToList();
      for (var i = 0; i < 2400; i++) AddCourse(organizations[i % organizations.Count]);
      for (var i = 0; i < 600; i++) Seed(_opportunities[i], CourseFeedbackAnswer.Taken, $"{i}@x.org", rating: 1 + i % 5, recommend: i % 3 != 0);

      var grading = Grading();
      var watch = Stopwatch.StartNew();
      await grading.Regrade();
      var first = watch.Elapsed;

      watch.Restart();
      await grading.Regrade();
      var second = watch.Elapsed;

      output.WriteLine($"Regrade of 2,400 courses in memory: {first.TotalMilliseconds:F0} ms first, {second.TotalMilliseconds:F0} ms unchanged");
      Assert.Equal(2400, _grades.Count);
      Assert.True(first < TimeSpan.FromSeconds(60), $"{first}");
    }
    #endregion
  }
}
