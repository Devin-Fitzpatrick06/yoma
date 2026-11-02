using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using System.Data.Common;
using Xunit;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Entity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity.Models.Lookups;
using Yoma.Core.Infrastructure.Database.Context;
using Yoma.Core.Infrastructure.Database.CourseQuality.Repositories;
using Yoma.Core.Infrastructure.Database.Opportunity.Repositories;
using Yoma.Core.Infrastructure.Shared.Interceptors;
using static Yoma.Core.Test.CourseQuality.CourseQualityTestData;

namespace Yoma.Core.Test.CourseQuality
{
  /// <summary>
  /// Runs only with COURSE_QUALITY_TEST_DB set to a disposable PostGIS database (superuser): it applies every migration
  /// and writes rows with foreign keys off. Proves what fakes cannot: the migration, jsonb columns, SQL translation and locking.
  /// </summary>
  public class CourseQualityDatabaseTests
  {
    private sealed class CommandRecorder : DbCommandInterceptor
    {
      public List<string> Commands { get; } = [];

      public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
      {
        Commands.Add(command.CommandText);
        return ValueTask.FromResult(result);
      }

      public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
      {
        Commands.Add(command.CommandText);
        return result;
      }
    }

    private static string? ConnectionString => System.Environment.GetEnvironmentVariable("COURSE_QUALITY_TEST_DB");

    private static ApplicationDbContext Context(CommandRecorder? recorder = null)
    {
      var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(ConnectionString, o => o.UseNetTopologySuite())
        .ConfigureWarnings(o => o.Ignore(RelationalEventId.MultipleCollectionIncludeWarning))
        .AddInterceptors(new ForUpdateInterceptor());
      if (recorder != null) builder.AddInterceptors(recorder);
      return new ApplicationDbContext(builder.Options);
    }

    /// <summary>
    /// Inserts one row, filling every required column by type. Foreign keys are off for this session only.
    /// </summary>
    private static async Task<Guid> Insert(NpgsqlConnection connection, string schema, string table, Dictionary<string, object> values)
    {
      var columns = new List<(string Name, string Type)>();
      await using (var read = new NpgsqlCommand("select column_name, data_type from information_schema.columns where table_schema=@s and table_name=@t and is_nullable='NO' and column_default is null", connection))
      {
        read.Parameters.AddWithValue("s", schema);
        read.Parameters.AddWithValue("t", table);
        await using var reader = await read.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns.Add((reader.GetString(0), reader.GetString(1)));
      }

      var id = Guid.NewGuid();
      var row = new Dictionary<string, object> { ["Id"] = id };
      foreach (var (name, type) in columns)
        row.TryAdd(name, type switch
        {
          "uuid" => Guid.NewGuid(),
          "boolean" => false,
          "smallint" => (short)0,
          "integer" => 0,
          "numeric" => 0m,
          "timestamp with time zone" => DateTimeOffset.UtcNow,
          _ => id.ToString("N")[..10]
        });
      foreach (var item in values) row[item.Key] = item.Value;

      await using var insert = new NpgsqlCommand($"insert into \"{schema}\".\"{table}\" ({string.Join(",", row.Keys.Select(o => $"\"{o}\""))}) values ({string.Join(",", row.Keys.Select((_, i) => $"@p{i}"))})", connection);
      var index = 0;
      foreach (var value in row.Values) insert.Parameters.AddWithValue($"p{index++}", value);
      await insert.ExecuteNonQueryAsync();
      return id;
    }

    private static async Task<(Guid OpportunityId, Guid OrganizationId, Guid TypeId, Guid StatusId, Guid OrganizationStatusId)> SeedCourse(ApplicationDbContext context)
    {
      var typeId = context.Set<Infrastructure.Database.Opportunity.Entities.Lookups.OpportunityType>().Single(o => o.Name == "Learning").Id;
      var statusId = context.Set<Infrastructure.Database.Opportunity.Entities.Lookups.OpportunityStatus>().Single(o => o.Name == "Active").Id;
      var organizationStatusId = context.OrganizationStatus.Single(o => o.Name == "Active").Id;

      await using var connection = new NpgsqlConnection(ConnectionString);
      await connection.OpenAsync(TestContext.Current.CancellationToken);
      await using (var replica = new NpgsqlCommand("set session_replication_role = replica", connection)) await replica.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

      var organizationId = await Insert(connection, "Entity", "Organization", new() { ["StatusId"] = organizationStatusId, ["Name"] = $"Org {Guid.NewGuid():N}" });
      var opportunityId = await Insert(connection, "Opportunity", "Opportunity", new()
      {
        ["OrganizationId"] = organizationId,
        ["TypeId"] = typeId,
        ["StatusId"] = statusId,
        ["Title"] = $"Course {Guid.NewGuid():N}"
      });

      return (opportunityId, organizationId, typeId, statusId, organizationStatusId);
    }

    private static CourseQualityService Service(ApplicationDbContext context, Guid typeId, Guid statusId, Guid organizationStatusId, CourseQualityOptions options)
    {
      var types = new Mock<IOpportunityTypeService>();
      types.Setup(o => o.GetByName("Learning")).Returns(new OpportunityType { Id = typeId, Name = "Learning" });
      var statuses = new Mock<IOpportunityStatusService>();
      statuses.Setup(o => o.GetByName("Active")).Returns(new OpportunityStatus { Id = statusId, Name = "Active" });
      var organizationStatuses = new Mock<IOrganizationStatusService>();
      organizationStatuses.Setup(o => o.GetByName("Active")).Returns(new Domain.Entity.Models.Lookups.OrganizationStatus { Id = organizationStatusId, Name = "Active" });

      return new CourseQualityService(NullLogger<CourseQualityService>.Instance, Mock.Of<IHttpContextAccessor>(), Options.Create(options), Mock.Of<IOpportunityService>(), Mock.Of<IOrganizationService>(),
        types.Object, statuses.Object, organizationStatuses.Object, new OpportunityRepository(context), Mock.Of<ICourseQualityLinkChecker>(), Mock.Of<ICourseQualityAIProvider>(),
        new CourseQualityAssessmentRepository(context), Mock.Of<IExecutionStrategyService>());
    }

    private static CourseQualityAssessment Assessment(Guid opportunityId, Guid organizationId, bool withFixNote)
    {
      var opportunity = CleanListing();
      opportunity.Id = opportunityId;
      opportunity.OrganizationId = organizationId;
      opportunity.Hidden = withFixNote;
      return CourseQualityEngine.Assess(opportunity, WorkingLink(), Ai(), new CourseQualityOptions(), DateTimeOffset.UtcNow);
    }

    // finding 5
    [Fact]
    public async Task QueryDue_PutsNeverAssessedFirst_ThenTheOldestAssessed_OnPostgres()
    {
      Assert.SkipWhen(string.IsNullOrEmpty(ConnectionString), "COURSE_QUALITY_TEST_DB is not set");

      await using (var migrate = Context()) await migrate.Database.MigrateAsync(TestContext.Current.CancellationToken);

      await using var context = Context();
      var (oldest, organizationId, typeId, statusId, organizationStatusId) = await SeedCourse(context);
      var (older, _, _, _, _) = await SeedCourse(context);
      var (never, _, _, _, _) = await SeedCourse(context);
      var repository = new CourseQualityAssessmentRepository(context);
      var service = Service(context, typeId, statusId, organizationStatusId, new CourseQualityOptions());

      var now = DateTimeOffset.UtcNow;
      foreach (var (id, age) in new[] { (older, 20), (oldest, 40) })
      {
        var assessment = Assessment(id, organizationId, false);
        assessment.DateAssessed = now.AddDays(-age);
        await repository.Create(assessment.ToRecord(now, new string('c', 64)));
      }

      var due = service.QueryDue(now).ToList().Where(o => o == never || o == older || o == oldest).ToList();

      Assert.Equal([never, oldest, older], due);
    }

    [Fact]
    public async Task Migration_Repository_Locking_DueAndCoverage_WorkOnPostgres()
    {
      Assert.SkipWhen(string.IsNullOrEmpty(ConnectionString), "COURSE_QUALITY_TEST_DB is not set");

      await using (var migrate = Context()) await migrate.Database.MigrateAsync(TestContext.Current.CancellationToken);

      var recorder = new CommandRecorder();
      await using var context = Context(recorder);
      var (opportunityId, organizationId, typeId, statusId, organizationStatusId) = await SeedCourse(context);
      var repository = new CourseQualityAssessmentRepository(context);
      var options = new CourseQualityOptions();
      var service = Service(context, typeId, statusId, organizationStatusId, options);

      // due before any assessment
      var modified = context.Set<Infrastructure.Database.Opportunity.Entities.Opportunity>().Where(o => o.Id == opportunityId).Select(o => o.DateModified).Single();
      Assert.Contains(opportunityId, service.QueryDue(DateTimeOffset.UtcNow).ToList());

      // nullable fix note and jsonb round trip
      var first = Assessment(opportunityId, organizationId, false);
      Assert.Null(first.FixNote);
      await repository.Create(first.ToRecord(modified, new string('a', 64)));
      var stored = repository.Query().Single(o => o.Id == first.Id);
      Assert.Equal(first.LearnerCard.Format, stored.ToAssessment().LearnerCard.Format);
      Assert.Null(stored.FixNote);
      Assert.Equal(new string('a', 64), stored.ContentFingerprint);

      // assessed, so no longer due; DateModified survives the round trip exactly
      Assert.DoesNotContain(opportunityId, service.QueryDue(DateTimeOffset.UtcNow).ToList());
      Assert.Equal(modified, stored.OpportunityDateModified);

      // only one latest row is allowed
      var duplicate = Assessment(opportunityId, organizationId, false);
      await Assert.ThrowsAnyAsync<DbUpdateException>(() => repository.Create(duplicate.ToRecord(modified, new string('b', 64))));
      context.ChangeTracker.Clear();

      // the interceptor applies the lock, and it covers only the assessment row
      recorder.Commands.Clear();
      var locked = repository.Query(LockMode.Wait).Single(o => o.OpportunityId == opportunityId && o.IsLatest);
      Assert.Contains(recorder.Commands, o => o.Contains("FOR UPDATE OF a", StringComparison.Ordinal));

      // history
      locked.IsLatest = false;
      await repository.Update(locked);
      var second = Assessment(opportunityId, organizationId, true);
      await repository.Create(second.ToRecord(modified, new string('c', 64)));
      Assert.Equal(second.Id, repository.Query().Single(o => o.OpportunityId == opportunityId && o.IsLatest).Id);
      Assert.Equal(2, repository.Query().Count(o => o.OpportunityId == opportunityId));
      Assert.NotNull(repository.Query().Single(o => o.Id == second.Id).ToAssessment().FixNote);

      var coverage = service.GetCoverage();
      Assert.True(coverage.TotalCourses >= 1);
      Assert.True(coverage.Assessed >= 1);
      Assert.Equal(coverage.TotalCourses, coverage.Assessed + coverage.NotAssessed);
      Assert.Equal(coverage.Assessed, coverage.Course + coverage.Webinar + coverage.Unknown);
    }
  }
}
