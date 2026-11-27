using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using Xunit;
using Yoma.Core.Api.Controllers;
using Yoma.Core.Domain;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.CourseQuality.Repositories;
using Yoma.Core.Infrastructure.Shared;

namespace Yoma.Core.Test.CourseQuality
{
  public class CourseQualityWiringTests
  {
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
      .UseNpgsql("Host=localhost;Database=none", o => o.UseNetTopologySuite()).Options);

    private static string ApiAppSettings()
    {
      for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
      {
        var path = Path.Combine(dir.FullName, "src", "application", "Yoma.Core.Api", "appsettings.json");
        if (File.Exists(path)) return path;
        path = Path.Combine(dir.FullName, "application", "Yoma.Core.Api", "appsettings.json");
        if (File.Exists(path)) return path;
      }

      throw new FileNotFoundException("API appsettings.json not found");
    }

    #region Configuration
    [Fact]
    public void AppSettings_BindRubric_OffByDefault_WithoutSecrets()
    {
      var configuration = new ConfigurationBuilder().AddJsonFile(ApiAppSettings()).Build();
      var section = configuration.GetSection(CourseQualityOptions.Section);

      Assert.True(section.Exists());
      var options = section.Get<CourseQualityOptions>()!;

      Assert.False(options.Enabled);
      Assert.Equal(80, options.VerifiedScoreThreshold);
      Assert.Equal(0.7m, options.ReviewConfidenceThreshold);
      Assert.Equal(49, options.HardGateScoreCap);
      Assert.Equal(0.69m, options.RuleOnlyConfidenceCap);
      Assert.Equal(9, options.Criteria.Count); // five course criteria and four job ones
      Assert.False(options.Jobs.Enabled);
      Assert.True(options.Criteria[CourseQualityCriterion.NoScamSignals].HardGate);
      Assert.Equal(30, options.Criteria[CourseQualityCriterion.DatesAccurate].Weight);
      Assert.Equal(15, options.Criteria[CourseQualityCriterion.FormatIdentified].Weight);
      Assert.False(options.Criteria[CourseQualityCriterion.DataComplete].HardGate);
      Assert.True(options.BatchSize > 0);
      Assert.False(string.IsNullOrWhiteSpace(options.Schedule));
      Assert.True(options.LinkCheck.MaxResponseBytes > 0);
    }

    [Fact]
    public void Weights_ChangeThroughConfigurationAlone()
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["CourseQuality:Criteria:LinkWorks:Weight"] = "40"
      }).Build();

      var options = configuration.GetSection(CourseQualityOptions.Section).Get<CourseQualityOptions>()!;

      Assert.Equal(40, options.Criteria[CourseQualityCriterion.LinkWorks].Weight);
      Assert.True(options.Criteria[CourseQualityCriterion.LinkWorks].HardGate);
      Assert.Equal(20, options.Criteria[CourseQualityCriterion.ListingLive].Weight);
    }
    #endregion

    #region Dependency injection
    private sealed class RealProvider : ICourseQualityAIProvider
    {
      public bool IsEnabled => true;

      public Task<CourseQualityAIResult?> Evaluate(CourseQualityAIRequest request, CancellationToken cancellationToken) => Task.FromResult<CourseQualityAIResult?>(null);
    }

    [Fact]
    public void Domain_RegistersServices_WithANoOpProviderThatCanBeReplaced()
    {
      var services = new ServiceCollection();
      services.ConfigureServices_DomainServices();

      Assert.Contains(services, o => o.ServiceType == typeof(ICourseQualityService));
      Assert.Contains(services, o => o.ServiceType == typeof(ICourseQualityBackgroundService));
      Assert.Contains(services, o => o.ServiceType == typeof(ICourseQualityLinkChecker));
      Assert.Contains(services, o => o.ServiceType == typeof(ICourseQualityHostResolver));
      Assert.Equal(typeof(NoOpCourseQualityAIProvider), services.Last(o => o.ServiceType == typeof(ICourseQualityAIProvider)).ImplementationType);

      services.AddScoped<ICourseQualityAIProvider, RealProvider>();

      Assert.Equal(typeof(RealProvider), services.Last(o => o.ServiceType == typeof(ICourseQualityAIProvider)).ImplementationType);
    }
    private sealed class OptionsDrivenReader(IOptions<CourseQualityOptions> options) : ICourseQualityAIProvider
    {
      public bool IsEnabled => options.Value.SourcePageReaderEnabled;

      public Task<CourseQualityAIResult?> Evaluate(CourseQualityAIRequest request, CancellationToken cancellationToken) => Task.FromResult<CourseQualityAIResult?>(null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reader_RegisteredWithPlainAddScoped_ReplacesTheNoOp_InEitherOrder(bool readerFirst)
    {
      var services = new ServiceCollection();
      services.Configure<CourseQualityOptions>(o => o.SourcePageReaderEnabled = true);

      if (readerFirst) services.AddScoped<ICourseQualityAIProvider, OptionsDrivenReader>();
      services.ConfigureServices_DomainServices();
      if (!readerFirst) services.AddScoped<ICourseQualityAIProvider, OptionsDrivenReader>();

      var descriptor = services.Last(o => o.ServiceType == typeof(ICourseQualityAIProvider));
      Assert.Equal(typeof(OptionsDrivenReader), descriptor.ImplementationType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reader_CanBeSwitchedThroughCourseQualityOptions(bool enabled)
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["CourseQuality:SourcePageReaderEnabled"] = enabled.ToString()
      }).Build();
      var services = new ServiceCollection();
      services.Configure<CourseQualityOptions>(o => configuration.GetSection(CourseQualityOptions.Section).Bind(o));
      services.AddScoped<ICourseQualityAIProvider, OptionsDrivenReader>();

      using var provider = services.BuildServiceProvider();

      Assert.Equal(enabled, provider.GetRequiredService<ICourseQualityAIProvider>().IsEnabled);
    }

    [Fact]
    public void AppSettings_BindTheGradingRubric_ToTheDefaults()
    {
      var configured = CourseQualityGrading.Configured(ShippedOptions());
      var defaults = CourseQualityGrading.Configured(new CourseQualityOptions());

      Assert.Equal(CourseQualityGrading.Serialize(defaults), CourseQualityGrading.Serialize(configured));
      Assert.Equal(4, configured.Bands.Count);
      Assert.Equal(CourseGrade.Ungraded, configured.NeedsReviewMaxGrade);
      Assert.Empty(CourseQualityGrading.Validate(configured));
    }

    [Fact]
    public void AppSettings_BindLearnerFeedback_OffByDefault()
    {
      var feedback = ShippedOptions().LearnerFeedback;

      Assert.False(feedback.Enabled);
      Assert.Equal(1440, feedback.PromptDelayInMinutes);
      Assert.Equal(3, feedback.SnoozeInDays);
      Assert.Equal(500, feedback.CommentMaxLength);
      Assert.Equal(CourseQualityLearnerFeedbackOptions.DefaultGood.Select(o => o.Key), feedback.ListGood().Select(o => o.Key));
      Assert.Equal(CourseQualityLearnerFeedbackOptions.DefaultBad.Select(o => o.Key), feedback.ListBad().Select(o => o.Key));
      Assert.Equal(7, new CourseQualityLearnerFeedbackOptions().ListBad().Count);
    }

    [Fact]
    public void AppSettings_ListTheReaderSwitch()
    {
      var configuration = new ConfigurationBuilder().AddJsonFile(ApiAppSettings()).Build();

      Assert.Equal("True", configuration["CourseQuality:SourcePageReaderEnabled"], ignoreCase: true);
    }
    #endregion

    #region Persistence
    [Fact]
    public void Migration_MatchesTheModel()
    {
      using var context = Context();

      Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Model_AllowsOneLatestRowPerOpportunity()
    {
      using var context = Context();

      var entity = context.Model.FindEntityType(typeof(Infrastructure.Database.CourseQuality.Entities.CourseQualityAssessment))!;
      Assert.Equal("CourseQuality", entity.GetSchema());

      var index = entity.GetIndexes().Single(o => o.IsUnique);
      Assert.Equal("OpportunityId", index.Properties.Single().Name);
      Assert.Equal("\"IsLatest\" = true", index.GetFilter());

      var fingerprint = entity.FindProperty(nameof(CourseQualityAssessmentRecord.ContentFingerprint))!;
      Assert.False(fingerprint.IsNullable);
      Assert.Equal("varchar(64)", fingerprint.GetColumnType());
    }

    [Fact]
    public void Model_HasTheGradeRubricAndFeedbackTables()
    {
      using var context = Context();

      var grade = context.Model.FindEntityType(typeof(Infrastructure.Database.CourseQuality.Entities.CourseQualityGrade))!;
      Assert.Equal("Grade", grade.GetTableName());
      Assert.Equal("CourseQuality", grade.GetSchema());
      Assert.Equal("OpportunityId", grade.GetIndexes().Single(o => o.IsUnique).Properties.Single().Name);
      Assert.Equal("varchar(20)", grade.FindProperty("Grade")!.GetColumnType());
      Assert.Equal("jsonb", grade.FindProperty("Factors")!.GetColumnType());

      var rubric = context.Model.FindEntityType(typeof(Infrastructure.Database.CourseQuality.Entities.CourseQualityGradeRubric))!;
      Assert.Equal("GradeRubric", rubric.GetTableName());
      Assert.True(rubric.FindProperty("Rubric")!.IsNullable);

      var feedback = context.Model.FindEntityType(typeof(Infrastructure.Database.CourseQuality.Entities.CourseQualityLearnerFeedback))!;
      Assert.Equal("LearnerFeedback", feedback.GetTableName());
      Assert.Equal(["UserId", "OpportunityId"], feedback.GetIndexes().Single(o => o.IsUnique).Properties.Select(o => o.Name));
      Assert.Contains(feedback.GetIndexes(), o => !o.IsUnique && o.Properties.Single().Name == "OpportunityId");
      Assert.Equal("varchar(20)", feedback.FindProperty("Answer")!.GetColumnType());

      var assessment = context.Model.FindEntityType(typeof(Infrastructure.Database.CourseQuality.Entities.CourseQualityAssessment))!;
      Assert.True(assessment.FindProperty("PageEvidence")!.IsNullable);
      Assert.True(assessment.FindProperty("ReviewReasons")!.IsNullable);
    }

    [Fact]
    public void Repository_GradeFilter_TranslatesToOneQuery()
    {
      using var context = Context();
      var assessments = new CourseQualityAssessmentRepository(context);
      var grades = new CourseQualityGradeRepository(context);

      var withGrade = grades.Query().Where(o => o.Grade == CourseGrade.A).Select(o => o.OpportunityId);
      var sql = assessments.Query().Where(o => o.IsLatest && withGrade.Contains(o.OpportunityId)).ToQueryString();

      Assert.Contains("\"CourseQuality\".\"Grade\"", sql);
      Assert.Contains("'A'", sql);

      var feedback = new CourseQualityLearnerFeedbackRepository(context).Query().Where(o => o.Answer == CourseFeedbackAnswer.Taken).ToQueryString();
      Assert.Contains("'Taken'", feedback);

      Assert.Contains("ORDER BY", new CourseQualityGradeRubricRepository(context).Query().OrderByDescending(o => o.DateCreated).Take(1).ToQueryString());
    }

    [Fact]
    public void BrowseGradeRank_JoinsTheGradeTableInSql_WithoutIdParameters()
    {
      using var context = Context();
      var grades = new CourseQualityGradeRepository(context);
      var grading = new CourseQualityGradingService(NullLogger<CourseQualityGradingService>.Instance, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
        Options.Create(new CourseQualityOptions()), Mock.Of<Domain.Core.Interfaces.IRepositoryBatchedValueContainsWithNavigationAndCustomFieldFilter<Domain.Opportunity.Models.Opportunity>>(),
        Mock.Of<IRepository<Domain.Opportunity.Models.OpportunitySkill>>(), Mock.Of<IRepository<Domain.Opportunity.Models.OpportunityLanguage>>(), Mock.Of<Domain.Lookups.Interfaces.ILanguageService>(),
        Mock.Of<Domain.MyOpportunity.Interfaces.IMyOpportunityService>(), new CourseQualityAssessmentRepository(context), grades, Mock.Of<IRepository<CourseQualityGradeRubricRecord>>(),
        Mock.Of<IRepository<CourseQualityLearnerFeedbackRecord>>(), Mock.Of<IDistributedLockService>(), Mock.Of<IExecutionStrategyService>(), Mock.Of<Hangfire.IBackgroundJobClient>());

      var sql = new Infrastructure.Database.Opportunity.Repositories.OpportunityRepository(context).Query().OrderBy(grading.GradeRank()).Take(20).ToQueryString();

      Assert.Contains("\"CourseQuality\".\"Grade\"", sql);
      Assert.Contains("CASE", sql);
      Assert.DoesNotContain("= ANY", sql);

      // jobs are kept out of the ranking while they are not shown in browse, also in SQL
      Assert.Contains("\"CourseQuality\".\"Assessment\"", sql);
      Assert.Contains("'Job'", sql);
    }

    [Fact]
    public void Repository_LatestQuery_TranslatesToSql()
    {
      using var context = Context();
      var repository = new CourseQualityAssessmentRepository(context);

      var sql = repository.Query().Where(o => o.IsLatest && o.FixNote != null && o.ReviewStatus == CourseQualityReviewStatus.Pending)
        .OrderBy(o => o.DateAssessed).Skip(1).Take(2).ToQueryString();

      Assert.Contains("\"CourseQuality\".\"Assessment\"", sql);
      Assert.Contains("'Pending'", sql);
    }

    [Fact]
    public void Repository_LockQuery_IsTagged()
    {
      using var context = Context();
      var repository = new CourseQualityAssessmentRepository(context);

      // the interceptor turns this tag into FOR UPDATE on execution; see CourseQualityDatabaseTests
      Assert.Contains(HintConfig.LockHints[LockMode.Wait], repository.Query(LockMode.Wait).ToQueryString());
    }
    #endregion

    #region Background job
    [Fact]
    public async Task Job_DoesNothing_WhenTurnedOff()
    {
      var service = new Mock<ICourseQualityService>(MockBehavior.Strict);
      var grading = new Mock<ICourseQualityGradingService>(MockBehavior.Strict);
      var locks = new Mock<IDistributedLockService>();
      locks.Setup(o => o.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>())).ReturnsAsync(true);
      var lifetime = new Mock<IHostApplicationLifetime>();
      lifetime.SetupGet(o => o.ApplicationStopping).Returns(CancellationToken.None);
      var job = new CourseQualityBackgroundService(NullLogger<CourseQualityBackgroundService>.Instance,
        Options.Create(new ScheduleJobOptions { DefaultScheduleMaxIntervalInHours = 2 }), Options.Create(new CourseQualityOptions { Enabled = false }), service.Object, grading.Object, locks.Object, lifetime.Object);

      await job.ProcessAssessments();

      service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Job_AssessesTheDueBatch_AndOneFailureDoesNotStopIt()
    {
      var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
      var service = new Mock<ICourseQualityService>();
      service.Setup(o => o.QueryDue(It.IsAny<DateTimeOffset>())).Returns(ids.AsQueryable());
      service.Setup(o => o.AssessDue(ids[0], It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("bad row"));
      service.Setup(o => o.AssessDue(ids[1], It.IsAny<CancellationToken>())).ReturnsAsync(true);
      var grading = new Mock<ICourseQualityGradingService>();
      var locks = new Mock<IDistributedLockService>();
      locks.Setup(o => o.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<string>())).ReturnsAsync(true);
      var lifetime = new Mock<IHostApplicationLifetime>();
      lifetime.SetupGet(o => o.ApplicationStopping).Returns(CancellationToken.None);
      var job = new CourseQualityBackgroundService(NullLogger<CourseQualityBackgroundService>.Instance,
        Options.Create(new ScheduleJobOptions { DefaultScheduleMaxIntervalInHours = 2 }), Options.Create(new CourseQualityOptions { Enabled = true, BatchSize = 2 }), service.Object, grading.Object, locks.Object, lifetime.Object);

      await job.ProcessAssessments();

      service.Verify(o => o.AssessDue(ids[0], It.IsAny<CancellationToken>()), Times.Once);
      service.Verify(o => o.AssessDue(ids[1], It.IsAny<CancellationToken>()), Times.Once);
      service.Verify(o => o.AssessDue(ids[2], It.IsAny<CancellationToken>()), Times.Never);

      // one requested regrade for the batch, because a course changed; the job does not wait for it
      grading.Verify(o => o.RequestRegrade(), Times.Once);
      grading.Verify(o => o.Regrade(), Times.Never);
    }

    [Fact]
    public async Task RegradeJob_Runs_EvenWhenAssessmentIsTurnedOff()
    {
      var grading = new Mock<ICourseQualityGradingService>();
      var lifetime = new Mock<IHostApplicationLifetime>();
      lifetime.SetupGet(o => o.ApplicationStopping).Returns(CancellationToken.None);
      var job = new CourseQualityBackgroundService(NullLogger<CourseQualityBackgroundService>.Instance,
        Options.Create(new ScheduleJobOptions()), Options.Create(new CourseQualityOptions { Enabled = false, BatchSize = 0 }), Mock.Of<ICourseQualityService>(), grading.Object,
        Mock.Of<IDistributedLockService>(), lifetime.Object);

      await job.ProcessRegrade();

      grading.Verify(o => o.RunRequestedRegrade(), Times.Once);
    }

    [Fact]
    public async Task RegradeJob_LogsAFailure_WithoutThrowing()
    {
      var grading = new Mock<ICourseQualityGradingService>();
      grading.Setup(o => o.RunRequestedRegrade()).ThrowsAsync(new TimeoutException("lock"));
      var lifetime = new Mock<IHostApplicationLifetime>();
      lifetime.SetupGet(o => o.ApplicationStopping).Returns(CancellationToken.None);
      var job = new CourseQualityBackgroundService(NullLogger<CourseQualityBackgroundService>.Instance,
        Options.Create(new ScheduleJobOptions()), Options.Create(new CourseQualityOptions()), Mock.Of<ICourseQualityService>(), grading.Object,
        Mock.Of<IDistributedLockService>(), lifetime.Object);

      await job.ProcessRegrade();
    }

    [Fact]
    public void ShippedRegradeDelay_IsShort()
    {
      Assert.Equal(30, ShippedOptions().RegradeDelayInSeconds);
    }
    #endregion

    #region Controller
    private static MethodInfo Action(string name) => typeof(CourseQualityController).GetMethod(name) ?? throw new InvalidOperationException(name);

    [Theory]
    [InlineData(nameof(CourseQualityController.GetCard), "GET", "opportunity/{opportunityId}/card")]
    [InlineData(nameof(CourseQualityController.Assess), "POST", "opportunity/{opportunityId}/assess")]
    [InlineData(nameof(CourseQualityController.SearchReviews), "POST", "review/search")]
    [InlineData(nameof(CourseQualityController.Review), "POST", "review/{assessmentId}")]
    [InlineData(nameof(CourseQualityController.ListFixNotes), "GET", "organization/{organizationId}/fixNotes")]
    [InlineData(nameof(CourseQualityController.GetCoverage), "GET", "coverage")]
    [InlineData(nameof(CourseQualityController.GetGradeRubric), "GET", "grading/rubric")]
    [InlineData(nameof(CourseQualityController.SaveGradeRubric), "PUT", "grading/rubric")]
    [InlineData(nameof(CourseQualityController.ResetGradeRubric), "DELETE", "grading/rubric")]
    [InlineData(nameof(CourseQualityController.ListGradeFactors), "GET", "grading/factors")]
    [InlineData(nameof(CourseQualityController.RunGrading), "POST", "grading/run")]
    [InlineData(nameof(CourseQualityController.GetGrade), "GET", "opportunity/{opportunityId}/grade")]
    [InlineData(nameof(CourseQualityController.GetFeedbackOptions), "GET", "feedback/options")]
    [InlineData(nameof(CourseQualityController.RecordFeedbackLeft), "POST", "opportunity/{opportunityId}/feedback/left")]
    [InlineData(nameof(CourseQualityController.ListPendingFeedback), "GET", "feedback/pending")]
    [InlineData(nameof(CourseQualityController.SubmitFeedback), "POST", "opportunity/{opportunityId}/feedback")]
    [InlineData(nameof(CourseQualityController.GetFeedbackSummary), "GET", "opportunity/{opportunityId}/feedback/summary")]
    public void Routes_MatchTheContract(string action, string method, string template)
    {
      var attribute = Action(action).GetCustomAttribute<HttpMethodAttribute>()!;

      Assert.Equal(method, attribute.HttpMethods.Single());
      Assert.Equal(template, attribute.Template);
      Assert.Equal("api/v3/courseQuality", typeof(CourseQualityController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Theory]
    [InlineData(nameof(CourseQualityController.Assess), "Admin")]
    [InlineData(nameof(CourseQualityController.SearchReviews), "Admin")]
    [InlineData(nameof(CourseQualityController.Review), "Admin")]
    [InlineData(nameof(CourseQualityController.GetCoverage), "Admin")]
    [InlineData(nameof(CourseQualityController.GetGradeRubric), "Admin")]
    [InlineData(nameof(CourseQualityController.SaveGradeRubric), "Admin")]
    [InlineData(nameof(CourseQualityController.ResetGradeRubric), "Admin")]
    [InlineData(nameof(CourseQualityController.ListGradeFactors), "Admin")]
    [InlineData(nameof(CourseQualityController.RunGrading), "Admin")]
    [InlineData(nameof(CourseQualityController.GetGrade), "Admin")]
    [InlineData(nameof(CourseQualityController.ListFixNotes), "Admin, OrganisationAdmin")]
    [InlineData(nameof(CourseQualityController.RecordFeedbackLeft), "User")]
    [InlineData(nameof(CourseQualityController.ListPendingFeedback), "User")]
    [InlineData(nameof(CourseQualityController.SubmitFeedback), "User")]
    [InlineData(nameof(CourseQualityController.GetFeedbackSummary), "Admin, OrganisationAdmin")]
    public void Roles_MatchTheContract(string action, string roles)
    {
      Assert.Equal(roles, Action(action).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public void OnlyTheCardAndFeedbackOptionsAreAnonymous()
    {
      Assert.NotNull(typeof(CourseQualityController).GetCustomAttribute<AuthorizeAttribute>()?.Policy);
      Assert.Equal([nameof(CourseQualityController.GetCard), nameof(CourseQualityController.GetFeedbackOptions)],
        typeof(CourseQualityController).GetMethods().Where(o => o.GetCustomAttribute<AllowAnonymousAttribute>() != null).Select(o => o.Name).Order());
    }
    #endregion

    #region Serialization
    /// <summary>
    /// MVC's Newtonsoft default is camelCase; Startup adds a string enum converter with no naming strategy.
    /// </summary>
    private static readonly JsonSerializerSettings ApiSettings = new()
    {
      ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
      Converters = [new StringEnumConverter()]
    };

    // G: from the shipped configuration and the real cron, not from what a comment says
    private static CourseQualityOptions ShippedOptions()
    {
      var directory = new DirectoryInfo(AppContext.BaseDirectory);
      while (directory != null && !File.Exists(Path.Combine(directory.FullName, "src", "application", "Yoma.Core.Api", "appsettings.json"))) directory = directory.Parent;
      Assert.NotNull(directory);

      var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
        .AddJsonFile(Path.Combine(directory.FullName, "src", "application", "Yoma.Core.Api", "appsettings.json")).Build();

      var options = new CourseQualityOptions();
      Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(configuration.GetSection(CourseQualityOptions.Section), options);
      return options;
    }

    // a daily-repeating cron only: anything narrower is refused here, so this figure cannot quietly overstate a schedule
    private static int RunsPerDay(string cron)
    {
      var fields = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      Assert.Equal(5, fields.Length);
      Assert.True(fields[2] == "*" && fields[3] == "*" && fields[4] == "*", cron);

      static int Count(string field, int size) => field == "*" ? size
        : field.StartsWith("*/", StringComparison.Ordinal) ? (size + int.Parse(field[2..]) - 1) / int.Parse(field[2..])
        : field.Split(',').Length;

      return Count(fields[0], 60) * Count(fields[1], 24);
    }

    [Theory]
    [InlineData("0 3 * * *", 1)]
    [InlineData("0 * * * *", 24)]
    [InlineData("*/30 * * * *", 48)]
    public void RunsPerDay_ReadsTheCron(string cron, int runs)
    {
      Assert.Equal(runs, RunsPerDay(cron));
    }

    [Fact]
    public void ShippedSchedule_AssessesTheWholeCatalogue_WithinTheReassessInterval()
    {
      const int catalogue = 2393;
      var options = ShippedOptions();

      var runsPerDay = RunsPerDay(options.Schedule);
      var perDay = runsPerDay * options.BatchSize;
      var passDays = (int)Math.Ceiling((decimal)catalogue / perDay);

      Assert.True(passDays <= options.ReassessIntervalInDays, $"{perDay} a day needs {passDays} days");

      // a card survives one full cycle, one full pass and a missed week of the job
      Assert.True(options.CardMaxAgeInDays >= options.ReassessIntervalInDays + passDays + 6, $"{options.CardMaxAgeInDays}");

      // a batch on one paced host must end before the next run
      var batchSeconds = options.BatchSize * (options.LinkCheck.MinHostIntervalInMilliseconds / 1000m + 1);
      Assert.True(batchSeconds < 86400m / runsPerDay, $"{batchSeconds}");
    }

    [Fact]
    public void ShippedProviders_IncludeTheProductionOrganisation_OnItsDomain()
    {
      var provider = ShippedOptions().SelfPacedProviders.Single(o => o.Name == "Alison Learning");

      Assert.Contains("alison.com", provider.Domains);
    }

    [Fact]
    public void Assessment_SerializesLikeTheWebModel()
    {
      var assessment = new CourseQualityAssessment
      {
        Status = CourseQualityStatus.NeedsReview,
        ReviewStatus = CourseQualityReviewStatus.Pending,
        AIUsed = true,
        AIModel = "m",
        AISummary = "s",
        Criteria = [new CourseQualityCriterionResult { Criterion = CourseQualityCriterion.LinkWorks, Reason = "r" }],
        LearnerCard = new LearnerCard { Cost = new LearnerCardCost { Type = CourseCostType.Unknown } },
        FixNote = null
      };

      var json = JObject.Parse(JsonConvert.SerializeObject(assessment, ApiSettings));

      var expected = new[]
      {
        "id", "opportunityId", "opportunityTitle", "organizationId", "organizationName", "rubricVersion", "score", "confidence", "status",
        "reviewStatus", "criteria", "aiUsed", "aiModel", "aiSummary", "learnerCard", "fixNote", "reviewedByEmail", "reviewNote", "dateReviewed", "dateAssessed",
        "reviewReasons", "grade", "feedback"
      };
      Assert.Equal(expected.Order(), json.Properties().Select(o => o.Name).Order());

      Assert.Equal("NeedsReview", json["status"]!.Value<string>());
      Assert.Equal("Pending", json["reviewStatus"]!.Value<string>());
      Assert.Equal("LinkWorks", json["criteria"]![0]!["criterion"]!.Value<string>());
      Assert.Equal("Unknown", json["learnerCard"]!["cost"]!["type"]!.Value<string>());
      Assert.Equal(["cost", "commitmentHours", "dateAssessed", "dateEnd", "datesConfirmed", "dateStart", "format", "grade", "gradeLabel", "highlights", "opportunityId", "selfPaced", "status", "timelineSummary"],
        ((JObject)json["learnerCard"]!).Properties().Select(o => o.Name).OrderBy(o => o == "cost" ? "" : o));
    }
    #endregion
  }
}
