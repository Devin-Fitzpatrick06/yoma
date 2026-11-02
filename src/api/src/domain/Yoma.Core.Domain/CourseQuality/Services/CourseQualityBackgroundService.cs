using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.Core.Helpers;
using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  public class CourseQualityBackgroundService : ICourseQualityBackgroundService
  {
    #region Class Variables
    private readonly ILogger<CourseQualityBackgroundService> _logger;
    private readonly ScheduleJobOptions _scheduleJobOptions;
    private readonly CourseQualityOptions _options;
    private readonly ICourseQualityService _courseQualityService;
    private readonly IDistributedLockService _distributedLockService;
    private readonly IHostApplicationLifetime _applicationLifetime;
    #endregion

    #region Constructor
    public CourseQualityBackgroundService(ILogger<CourseQualityBackgroundService> logger,
      IOptions<ScheduleJobOptions> scheduleJobOptions,
      IOptions<CourseQualityOptions> options,
      ICourseQualityService courseQualityService,
      IDistributedLockService distributedLockService,
      IHostApplicationLifetime applicationLifetime)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _scheduleJobOptions = scheduleJobOptions?.Value ?? throw new ArgumentNullException(nameof(scheduleJobOptions));
      _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
      _courseQualityService = courseQualityService ?? throw new ArgumentNullException(nameof(courseQualityService));
      _distributedLockService = distributedLockService ?? throw new ArgumentNullException(nameof(distributedLockService));
      _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
    }
    #endregion

    #region Public Members
    public async Task ProcessAssessments()
    {
      const string lockIdentifier = "course_quality_process_assessments";
      var dateTimeNow = DateTimeOffset.UtcNow;
      var executeUntil = dateTimeNow.AddHours(_scheduleJobOptions.DefaultScheduleMaxIntervalInHours);
      var lockDuration = executeUntil - dateTimeNow + TimeSpan.FromMinutes(_scheduleJobOptions.DistributedLockDurationBufferInMinutes);

      await using var job = BackgroundJobExecutionScope.CreateWithLock(_applicationLifetime, _distributedLockService, _logger);

      try
      {
        if (!await job.TryAcquireLockAsync(lockIdentifier, lockDuration)) return;

        // checked after the lock, so a disabled job still honours application shutdown like every other job
        if (!_options.Enabled) return;

        var opportunityIds = _courseQualityService.QueryDue(dateTimeNow).Take(_options.BatchSize).ToList();

        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Assessing {count} course(s)", opportunityIds.Count);

        foreach (var opportunityId in opportunityIds)
        {
          job.ThrowIfStopping();
          if (DateTimeOffset.UtcNow >= executeUntil) break;

          try
          {
            await _courseQualityService.Assess(opportunityId, _applicationLifetime.ApplicationStopping);
          }
          catch (Exception ex)
          {
            job.ThrowIfStopping(ex);

            // one bad listing must not hold up the rest of the batch
            if (_logger.IsEnabled(LogLevel.Error)) _logger.LogError(ex, "Failed to assess course quality for opportunity {opportunityId}", opportunityId);
          }
        }
      }
      catch (Exception ex)
      {
        job.ThrowIfStopping(ex);

        if (_logger.IsEnabled(LogLevel.Error)) _logger.LogError(ex, "Failed to execute {process}: {errorMessage}", nameof(ProcessAssessments), ex.Message);
      }
    }
    #endregion
  }
}
