using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using System.Security.Claims;
using Xunit;
using Yoma.Core.Domain.ActionLink.Interfaces;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Lookups.Interfaces;
using Yoma.Core.Domain.MyOpportunity.Interfaces;
using Yoma.Core.Domain.MyOpportunity.Models;
using Yoma.Core.Domain.Opportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Models;
using Yoma.Core.Domain.Opportunity.Services;
using Yoma.Core.Domain.Treasury.Interfaces;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;
using OpportunityModel = Yoma.Core.Domain.Opportunity.Models.Opportunity;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityGradingServiceTests
  {
    #region Harness
    private readonly List<OpportunityModel> _opportunities = [];
    private readonly List<CourseQualityAssessmentRecord> _assessments = [];
    private readonly List<CourseQualityGradeRecord> _grades = [];
    private readonly List<CourseQualityGradeRubricRecord> _rubrics = [];
    private readonly List<CourseQualityLearnerFeedbackRecord> _feedback = [];
    private readonly Mock<IRepositoryBatched<CourseQualityGradeRecord>> _gradeRepository = new();
    private readonly CourseQualityOptions _options = new();
    private readonly Mock<IDistributedLockService> _locks = new();
    private readonly Mock<IBackgroundJobClient> _jobs = new();
    private readonly HashSet<string> _held = [];

    public CourseQualityGradingServiceTests()
    {
      _gradeRepository.Setup(o => o.Query()).Returns(() => _grades.AsQueryable());
      _gradeRepository.Setup(o => o.Create(It.IsAny<List<CourseQualityGradeRecord>>())).Returns((List<CourseQualityGradeRecord> items) =>
      {
        foreach (var item in items)
        {
          var opportunity = _opportunities.Single(o => o.Id == item.OpportunityId);
          item.OpportunityTitle = opportunity.Title;
          item.OrganizationName = opportunity.OrganizationName;
        }

        _grades.AddRange(items);
        return Task.FromResult(items);
      });
      _gradeRepository.Setup(o => o.Update(It.IsAny<List<CourseQualityGradeRecord>>())).Returns((List<CourseQualityGradeRecord> items) => Task.FromResult(items));
      _gradeRepository.Setup(o => o.Delete(It.IsAny<List<CourseQualityGradeRecord>>())).Returns((List<CourseQualityGradeRecord> items) =>
      {
        _grades.RemoveAll(items.Contains);
        return Task.CompletedTask;
      });
    }

    private CourseQualityGradingService Service()
    {
      var opportunityRepository = new Mock<IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<OpportunityModel>>();
      opportunityRepository.Setup(o => o.Query()).Returns(() => _opportunities.AsQueryable());

      var skills = new Mock<IRepository<OpportunitySkill>>();
      skills.Setup(o => o.Query()).Returns(() => _opportunities.SelectMany(o => (o.Skills ?? []).Select(s => new OpportunitySkill { OpportunityId = o.Id, SkillId = s.Id })).AsQueryable());

      var english = new Domain.Lookups.Models.Language { Id = Guid.NewGuid(), Name = "English", CodeAlpha2 = "en" };
      var languages = new Mock<IRepository<OpportunityLanguage>>();
      languages.Setup(o => o.Query()).Returns(() => _opportunities.Select(o => new OpportunityLanguage { OpportunityId = o.Id, LanguageId = english.Id }).AsQueryable());
      var languageService = new Mock<ILanguageService>();
      languageService.Setup(o => o.List()).Returns([english]);

      var myOpportunities = new Mock<IMyOpportunityService>();
      myOpportunities.Setup(o => o.ListParticipationCounts()).Returns([]);

      var assessments = new Mock<IRepository<CourseQualityAssessmentRecord>>();
      assessments.Setup(o => o.Query()).Returns(() => _assessments.AsQueryable());

      var rubrics = new Mock<IRepository<CourseQualityGradeRubricRecord>>();
      rubrics.Setup(o => o.Query()).Returns(() => _rubrics.AsQueryable());
      rubrics.Setup(o => o.Create(It.IsAny<CourseQualityGradeRubricRecord>())).Returns((CourseQualityGradeRubricRecord r) => { _rubrics.Add(r); return Task.FromResult(r); });

      var feedback = new Mock<IRepository<CourseQualityLearnerFeedbackRecord>>();
      feedback.Setup(o => o.Query()).Returns(() => _feedback.AsQueryable());

      _locks.Setup(o => o.RunWithLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<Func<Task>>(), It.IsAny<string>()))
        .Returns((string _, TimeSpan _, Func<Task> action, string _) => action());
      _locks.Setup(o => o.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>())).ReturnsAsync((string key, TimeSpan _, string _) => _held.Add(key));
      _locks.Setup(o => o.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<string>())).Returns((string key, string _) => { _held.Remove(key); return Task.CompletedTask; });

      var strategy = new Mock<IExecutionStrategyService>();
      strategy.Setup(o => o.ExecuteInExecutionStrategyAsync(It.IsAny<Func<Task>>(), It.IsAny<bool>())).Returns((Func<Task> body, bool _) => body());

      var accessor = new Mock<IHttpContextAccessor>();
      accessor.SetupGet(o => o.HttpContext).Returns(new DefaultHttpContext
      {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin@yoma.world")], "test"))
      });

      return new CourseQualityGradingService(NullLogger<CourseQualityGradingService>.Instance, accessor.Object, Options.Create(_options), opportunityRepository.Object,
        skills.Object, languages.Object, languageService.Object, myOpportunities.Object, assessments.Object, _gradeRepository.Object, rubrics.Object, feedback.Object,
        _locks.Object, strategy.Object, _jobs.Object, Mock.Of<Domain.Pathway.Interfaces.IPathwayOutcomeService>());
    }

    private OpportunityModel AddCourse(CourseQualityStatus status = CourseQualityStatus.Verified)
    {
      var opportunity = CleanListing();
      _opportunities.Add(opportunity);

      var ai = Ai();
      var assessment = CourseQualityEngine.Assess(opportunity, WorkingLink(), ai, new CourseQualityOptions(), Now);
      assessment.Status = status;
      var record = assessment.ToRecord(Now, new string('a', 64));
      record.PageEvidence = new CourseQualityPageEvidence { Source = CourseQualityEvidenceSource.Page, HasCourseJsonLd = true, OutcomeCount = 4, ModuleCount = 6 }.Serialize();
      _assessments.Add(record);

      return opportunity;
    }

    private static CourseGradeRubric ValidRubric()
    {
      var rubric = CourseQualityGrading.Configured(new CourseQualityOptions());
      rubric.Version = "admin-1";
      foreach (var factor in rubric.Factors.Values) factor.Weight *= 2;
      return rubric;
    }
    #endregion

    #region Regrade
    [Fact]
    public async Task Regrade_GradesVisibleAssessedCourses_AndStoresTheFactors()
    {
      var course = AddCourse();
      var hidden = AddCourse();
      hidden.Hidden = true;

      await Service().Regrade();

      var grade = Assert.Single(_grades);
      Assert.Equal(course.Id, grade.OpportunityId);
      Assert.Equal(CourseQualityStatus.Verified, grade.Status);
      Assert.Equal(CourseQualityGradingDefaults.Version, grade.RubricVersion);
      Assert.Equal(CourseQualityGradingDefaults.Factors.Count, grade.ToResult().Factors.Count);
    }

    [Fact]
    public async Task Regrade_WritesNothing_WhenNothingChanged()
    {
      AddCourse();
      AddCourse();
      await Service().Regrade();
      var dates = _grades.Select(o => o.DateGraded).ToList();

      await Service().Regrade();

      _gradeRepository.Verify(o => o.Create(It.IsAny<List<CourseQualityGradeRecord>>()), Times.Once);
      _gradeRepository.Verify(o => o.Update(It.IsAny<List<CourseQualityGradeRecord>>()), Times.Never);
      Assert.Equal(dates, _grades.Select(o => o.DateGraded));
    }

    [Fact]
    public async Task Regrade_UpdatesChangedCourses_AndRemovesOnesNoLongerShown()
    {
      var stays = AddCourse();
      var goes = AddCourse();
      await Service().Regrade();

      stays.Description = new string('a', 1500);
      goes.Status = Domain.Opportunity.Status.Expired;
      await Service().Regrade();

      _gradeRepository.Verify(o => o.Update(It.Is<List<CourseQualityGradeRecord>>(l => l.Single().OpportunityId == stays.Id)), Times.Once);
      Assert.Equal(stays.Id, Assert.Single(_grades).OpportunityId);
    }

    [Fact]
    public async Task Regrade_ReadsOnlyTakenFeedback()
    {
      var course = AddCourse();
      var user = Guid.NewGuid();
      _feedback.Add(new CourseQualityLearnerFeedbackRecord { UserId = user, OpportunityId = course.Id, Answer = CourseFeedbackAnswer.Taken, Rating = 5, WouldRecommend = true });
      _feedback.Add(new CourseQualityLearnerFeedbackRecord { UserId = Guid.NewGuid(), OpportunityId = course.Id, Answer = CourseFeedbackAnswer.Taken });
      _feedback.Add(new CourseQualityLearnerFeedbackRecord { UserId = Guid.NewGuid(), OpportunityId = course.Id, Answer = CourseFeedbackAnswer.NotTaken, Rating = 1 });

      await Service().Regrade();

      // the Taken answer with no rating or recommendation says nothing, so only the scored one counts
      var row = Service().ListFactors().Single();
      Assert.Equal(1, row.FeedbackResponses);
      Assert.Contains(CourseGradeFactor.LearnerFeedback, row.FactorsWithData);
      Assert.True(row.Factors[CourseGradeFactor.LearnerFeedback] > 50);
    }

    [Fact]
    public async Task NotVerified_IsStoredAsUngraded()
    {
      var course = AddCourse(CourseQualityStatus.NotVerified);

      await Service().Regrade();

      var grade = Service().GetGrade(course.Id);
      Assert.Equal(CourseGrade.Ungraded, grade.Grade);
      Assert.Equal("Not graded", grade.GradeLabel);
      Assert.Equal(5, Service().GradeRank().Compile()(course));
      Assert.Equal([course.Id], Service().QueryByGrade(CourseGrade.Ungraded).ToList());
    }

    [Fact]
    public void GetGrade_Is404_BeforeGrading()
    {
      Assert.Throws<EntityNotFoundException>(() => Service().GetGrade(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListFactors_AndGrades_ComeFromTheStoredRows()
    {
      var courses = Enumerable.Range(0, 10).Select(_ => AddCourse()).ToList();

      await Service().Regrade();

      var rows = Service().ListFactors();
      Assert.Equal(10, rows.Count);
      Assert.All(rows, o => Assert.Equal(CourseQualityGradingDefaults.Factors.Count, o.Factors.Count));
      Assert.Equal(courses[0].Title, rows[0].OpportunityTitle);

      var grades = Service().ListGrades([courses[0].Id, courses[1].Id]);
      Assert.Equal(2, grades.Count);
      Assert.Equal(10, Service().GetRubricState().Distribution.Values.Sum());
      Assert.Equal(5, Service().GetRubricState().Distribution.Count);
    }
    #endregion

    #region Rubric
    [Fact]
    public void RubricState_IsTheConfiguration_ByDefault()
    {
      var state = Service().GetRubricState();

      Assert.True(state.IsDefault);
      Assert.Null(state.UpdatedByEmail);
      Assert.Equal(CourseQualityGradingDefaults.Version, state.Rubric.Version);
      Assert.Equal(4, state.Rubric.Bands.Count);
    }

    [Fact]
    public async Task SaveRubric_StoresANormalisedOverride_AndRegrades()
    {
      AddCourse();

      var state = await Service().SaveRubric(ValidRubric());

      Assert.False(state.IsDefault);
      Assert.Equal("admin@yoma.world", state.UpdatedByEmail);
      Assert.Equal("admin-1", state.Rubric.Version);
      Assert.Equal(100m, state.Rubric.Factors.Values.Sum(o => o.Weight));
      Assert.Equal("admin-1", Assert.Single(_grades).RubricVersion);
    }

    [Fact]
    public async Task SaveRubric_RejectsAnInvalidRubric_AndSavesNothing()
    {
      var rubric = ValidRubric();
      rubric.Bands[^1].TopPercent = 90;

      var ex = await Assert.ThrowsAsync<ValidationException>(() => Service().SaveRubric(rubric));

      Assert.Contains("100 percent", ex.Message);
      Assert.Empty(_rubrics);
    }

    [Fact]
    public async Task ResetRubric_ReturnsToConfiguration_AndKeepsHistory()
    {
      AddCourse();
      await Service().SaveRubric(ValidRubric());

      var state = await Service().ResetRubric();

      Assert.True(state.IsDefault);
      Assert.Equal(CourseQualityGradingDefaults.Version, state.Rubric.Version);
      Assert.Equal(2, _rubrics.Count);
      Assert.Null(_rubrics[^1].Rubric);
      Assert.Equal(CourseQualityGradingDefaults.Version, Assert.Single(_grades).RubricVersion);
    }

    [Fact]
    public void ConfiguredBands_ReplaceTheDefaults_Whole()
    {
      _options.Grading.Bands =
      [
        new() { Grade = CourseGrade.A, Label = "Best", TopPercent = 20 },
        new() { Grade = CourseGrade.B, Label = "Good", TopPercent = 50 },
        new() { Grade = CourseGrade.C, Label = "Fair", TopPercent = 80 },
        new() { Grade = CourseGrade.D, Label = "Rest", TopPercent = 100 }
      ];

      var rubric = Service().GetRubric();

      Assert.Equal(["Best", "Good", "Fair", "Rest"], rubric.Bands.Select(o => o.Label));
    }

    [Fact]
    public async Task SaveRubric_RejectsParametersThatWouldBreakEveryRegrade()
    {
      var rubric = ValidRubric();
      rubric.Factors[CourseGradeFactor.LearnerFeedback].Parameters[CourseGradeParameter.PriorResponses] = 0;

      var ex = await Assert.ThrowsAsync<ValidationException>(() => Service().SaveRubric(rubric));

      Assert.Contains("PriorResponses above 0", ex.Message);
      Assert.Empty(_rubrics);
    }

    [Fact]
    public async Task SaveRubric_DryRunsTheCatalogue_AndSavesNothingWhenItFails()
    {
      var course = AddCourse();
      course.CommitmentInterval = (TimeIntervalOption)99;

      var ex = await Assert.ThrowsAsync<ValidationException>(() => Service().SaveRubric(ValidRubric()));

      Assert.StartsWith("This rubric cannot grade the current catalogue", ex.Message);
      Assert.Empty(_rubrics);
      Assert.Empty(_grades);
    }
    #endregion

    #region Requested regrades
    [Fact]
    public async Task RequestRegrade_SchedulesOneRun_WhilePending()
    {
      _options.RegradeDelayInSeconds = 45;

      await Service().RequestRegrade();
      await Service().RequestRegrade();
      await Service().RequestRegrade();

      _jobs.Verify(o => o.Create(It.Is<Hangfire.Common.Job>(j => j.Method.Name == nameof(ICourseQualityBackgroundService.ProcessRegrade)),
        It.Is<Hangfire.States.ScheduledState>(s => s.EnqueueAt > DateTime.UtcNow.AddSeconds(30))), Times.Once);
      _gradeRepository.Verify(o => o.Query(), Times.Never);
    }

    [Fact]
    public async Task RunRequestedRegrade_ClearsTheRequestFirst_SoTheNextChangeSchedulesAgain()
    {
      AddCourse();
      await Service().RequestRegrade();

      await Service().RunRequestedRegrade();
      Assert.Single(_grades);

      await Service().RequestRegrade();
      _jobs.Verify(o => o.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RequestRegrade_NeverThrows_AndFreesTheRequestWhenSchedulingFails()
    {
      _jobs.Setup(o => o.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>())).Throws(new InvalidOperationException("storage down"));

      await Service().RequestRegrade();
      Assert.Empty(_held);

      _locks.Setup(o => o.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>())).ThrowsAsync(new TimeoutException("redis"));
      await Service().RequestRegrade();
    }

    [Fact]
    public async Task GradeRank_OrdersAToD_ThenUngradedAndUnknown()
    {
      var courses = Enumerable.Range(0, 3).Select(_ => AddCourse()).ToList();
      await Service().Regrade();
      _grades.Single(o => o.OpportunityId == courses[0].Id).Grade = CourseGrade.C;
      _grades.Single(o => o.OpportunityId == courses[1].Id).Grade = CourseGrade.A;
      _grades.Single(o => o.OpportunityId == courses[2].Id).Grade = CourseGrade.Ungraded;

      var rank = Service().GradeRank().Compile();

      Assert.Equal(3, rank(courses[0]));
      Assert.Equal(1, rank(courses[1]));
      Assert.Equal(5, rank(courses[2]));
      Assert.Equal(5, rank(CleanListing()));
    }
    #endregion
  }

  public class CourseQualityBrowseOrderTests
  {
    private static readonly PropertyInfo OrderInstructions = typeof(OpportunitySearchFilterBase).GetProperty("OrderInstructions", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>
    /// Runs the search, captures the order it hands to the opportunity search, and applies that order to the given courses.
    /// </summary>
    private static List<Guid> Order(List<OpportunityModel> courses, OpportunitySearchFilter filter, List<Guid>? verified, Dictionary<CourseGrade, List<Guid>>? graded, List<Guid>? popular)
    {
      List<FilterOrdering<OpportunityModel>>? captured = null;
      var opportunities = new Mock<IOpportunityService>();
      opportunities.Setup(o => o.Search(It.IsAny<OpportunitySearchFilterAdmin>(), false))
        .Callback((OpportunitySearchFilterAdmin f, bool _) => captured = (List<FilterOrdering<OpportunityModel>>)OrderInstructions.GetValue(f)!)
        .Returns(new OpportunitySearchResults { TotalCount = courses.Count });

      var courseQuality = new Mock<ICourseQualityService>();
      courseQuality.Setup(o => o.ListVerifiedForBrowse()).Returns(verified);
      // the same shape as the grading service's rank, over the given grades
      courseQuality.Setup(o => o.GradeRankForBrowse()).Returns(graded == null ? null
        : opportunity => graded.Where(g => g.Value.Contains(opportunity.Id)).Select(g => (int?)g.Key).FirstOrDefault() ?? 5);
      courseQuality.Setup(o => o.RankByPopularity(It.IsAny<List<MyOpportunityParticipationCounts>>())).Returns(popular);

      var myOpportunities = new Mock<IMyOpportunityService>();
      myOpportunities.Setup(o => o.ListParticipationCounts()).Returns([]);

      var service = new OpportunityInfoService(Options.Create(new AppSettings()), Mock.Of<IHttpContextAccessor>(), opportunities.Object, myOpportunities.Object,
        Mock.Of<ILinkService>(), Mock.Of<IUserService>(), Mock.Of<IDownloadService>(), Mock.Of<ITreasuryService>(), courseQuality.Object);

      filter.TotalCountOnly = true;
      service.Search(filter);

      IOrderedEnumerable<OpportunityModel>? ordered = null;
      foreach (var instruction in captured!)
      {
        var key = instruction.OrderBy.Compile();
        var descending = instruction.SortOrder == FilterSortOrder.Descending;
        ordered = ordered == null
          ? descending ? courses.OrderByDescending(key) : courses.OrderBy(key)
          : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
      }

      return [.. ordered!.Select(o => o.Id)];
    }

    private static List<OpportunityModel> Courses(int count) => [.. Enumerable.Range(0, count).Select(i =>
    {
      var course = CleanListing();
      course.Title = $"Course {i}";
      course.DateStart = Now.AddDays(-i);
      return course;
    })];

    [Fact]
    public void Browse_IsVerifiedFirst_ThenGrade_ThenPopularity()
    {
      var c = Courses(6);
      var verified = new List<Guid> { c[0].Id, c[1].Id, c[2].Id };
      var graded = new Dictionary<CourseGrade, List<Guid>> { [CourseGrade.A] = [c[2].Id, c[4].Id], [CourseGrade.C] = [c[0].Id, c[1].Id] };
      var popular = new List<Guid> { c[1].Id, c[5].Id, c[0].Id };

      var order = Order(c, new OpportunitySearchFilter(), verified, graded, popular);

      // verified: A first, then the two Cs by popularity; then the rest: A, then ungraded by popularity, then the default order
      Assert.Equal([c[2].Id, c[1].Id, c[0].Id, c[4].Id, c[5].Id, c[3].Id], order);
    }

    [Fact]
    public void Browse_WithoutGrades_KeepsVerifiedThenPopularity()
    {
      var c = Courses(3);

      var order = Order(c, new OpportunitySearchFilter(), [c[2].Id], [], [c[1].Id]);

      Assert.Equal([c[2].Id, c[1].Id, c[0].Id], order);
    }

    [Fact]
    public void Newest_KeepsVerifiedThenNewest_WithoutGrades()
    {
      var c = Courses(3);
      var graded = new Dictionary<CourseGrade, List<Guid>> { [CourseGrade.A] = [c[2].Id] };

      var order = Order(c, new OpportunitySearchFilter { Newest = true }, [c[1].Id], graded, [c[2].Id]);

      Assert.Equal([c[1].Id, c[0].Id, c[2].Id], order);
    }

    [Fact]
    public void ExplicitOrdering_IsLeftAlone()
    {
      var c = Courses(3);
      var order = Order(c, new OpportunitySearchFilter { Ordering = [new() { Field = default, Direction = FilterSortOrder.Descending }] }, [c[0].Id],
        new Dictionary<CourseGrade, List<Guid>> { [CourseGrade.A] = [c[0].Id] }, [c[0].Id]);

      // the default order stands, because the explicit sort is applied by the opportunity search itself
      Assert.Equal([c[0].Id, c[1].Id, c[2].Id], order);
    }

    [Fact]
    public void ShowInBrowseOff_ChangesNothing()
    {
      var c = Courses(3);

      var order = Order(c, new OpportunitySearchFilter(), null, null, null);

      Assert.Equal([c[0].Id, c[1].Id, c[2].Id], order);
    }
  }
}
