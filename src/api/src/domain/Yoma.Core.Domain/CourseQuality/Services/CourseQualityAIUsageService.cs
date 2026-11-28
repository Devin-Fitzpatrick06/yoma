using Yoma.Core.Domain.Core.Interfaces;
using Yoma.Core.Domain.CourseQuality.Extensions;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// Every assessment row keeps its AI usage, so a reassessment adds a run rather than replacing one. The totals read the
  /// usage column alone; the recent list reads 20 rows.
  /// </summary>
  public class CourseQualityAIUsageService : ICourseQualityAIUsageService
  {
    #region Class Variables
    public const int RecentLimit = 20;

    private readonly IRepository<CourseQualityAssessmentRecord> _assessmentRepository;
    private readonly CourseQualityAIEndpoint _endpoint;
    #endregion

    #region Constructor
    public CourseQualityAIUsageService(IRepository<CourseQualityAssessmentRecord> assessmentRepository, CourseQualityAIEndpoint endpoint)
    {
      _assessmentRepository = assessmentRepository ?? throw new ArgumentNullException(nameof(assessmentRepository));
      _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }
    #endregion

    #region Public Members
    public CourseQualityAIUsageSummary GetSummary()
    {
      var metered = _assessmentRepository.Query().Where(o => o.AIUsage != null);

      // one column per row: the totals never load the assessments themselves
      var usages = metered.Select(o => o.AIUsage).ToList()
        .Select(CourseQualityAssessmentExtensions.ParseAIUsage)
        .OfType<CourseQualityAIUsage>()
        .ToList();

      var recent = metered
        .OrderByDescending(o => o.DateAssessed).ThenByDescending(o => o.Id)
        .Take(RecentLimit)
        .Select(o => new { o.Id, o.OpportunityId, o.OrganizationId, o.OpportunityTitle, o.AIUsage })
        .ToList()
        .Select(o => (Row: o, Usage: CourseQualityAssessmentExtensions.ParseAIUsage(o.AIUsage)))
        .Where(o => o.Usage != null)
        .OrderByDescending(o => o.Usage!.DateRun)
        .Select(o => new CourseQualityAIRun { AssessmentId = o.Row.Id, OpportunityId = o.Row.OpportunityId, OrganizationId = o.Row.OrganizationId, OpportunityTitle = o.Row.OpportunityTitle, Usage = o.Usage! })
        .ToList();

      return new CourseQualityAIUsageSummary
      {
        Provider = _endpoint.Provider,
        Model = _endpoint.Model,
        EndpointHost = _endpoint.EndpointHost,
        IsLocal = _endpoint.IsLocal,
        Configured = _endpoint.Provider != AIProviderKind.None && _endpoint.Configured,
        Runs = usages.Count,
        Answered = usages.Count(o => o.Outcome == CourseQualityAIOutcome.Answered),
        InputTokens = usages.Sum(o => o.InputTokens),
        OutputTokens = usages.Sum(o => o.OutputTokens),
        CostUsd = usages.Sum(o => o.CostUsd ?? 0m),
        AverageDurationMs = usages.Count == 0 ? null : (long)Math.Round(usages.Average(o => (double)o.DurationMs)),
        Recent = recent
      };
    }
    #endregion
  }
}
