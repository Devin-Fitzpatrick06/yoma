using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using Yoma.Core.Domain.Core;
using Yoma.Core.Domain.Core.Exceptions;
using Yoma.Core.Domain.Core.Models;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.Entity.Interfaces;
using Yoma.Core.Domain.Lookups.Interfaces;
using Yoma.Core.Domain.Opportunity.Interfaces.Lookups;
using Yoma.Core.Domain.Opportunity.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// The pre-publish check (iteration 4, W1). Builds the listing the partner is typing in memory, as it would read once published,
  /// and runs the same engine a published listing of its type gets: the course rubric with format detection and the page reader
  /// for Learning, the listing checks for Event and Impact Action, the job checks for Job. Nothing is stored.
  ///
  /// A form asks again on every pause in typing, so a link's check and its page reading are cached for a few minutes, and the
  /// link check has a short timeout of its own: a slow link is "could not confirm", which never fails a hard gate.
  /// </summary>
  public sealed class CourseQualityDraftService : ICourseQualityDraftService
  {
    #region Class Variables
    public static readonly TimeSpan LinkCacheDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan TimedOutLinkCacheDuration = TimeSpan.FromSeconds(30);

    private const int MaxSuggestionLength = 1000;
    private static readonly string[] SuggestionFields = [DraftCheckField.Summary, DraftCheckField.DateStart, DraftCheckField.DateEnd, DraftCheckField.Skills];

    private readonly ILogger<CourseQualityDraftService> _logger;
    private readonly CourseQualityOptions _options;
    private readonly IOrganizationService _organizationService;
    private readonly IOpportunityTypeService _opportunityTypeService;
    private readonly ITimeIntervalService _timeIntervalService;
    private readonly IEngagementTypeService _engagementTypeService;
    private readonly ICourseQualityLinkChecker _linkChecker;
    private readonly ICourseQualityAIProvider _aiProvider;
    private readonly ICourseQualityDraftSuggester? _suggester;
    #endregion

    #region Constructor
    /// <param name="suggesters">Empty unless a suggestion provider is registered; the first enabled one is used.</param>
    public CourseQualityDraftService(ILogger<CourseQualityDraftService> logger,
      IOptions<CourseQualityOptions> options,
      IOrganizationService organizationService,
      IOpportunityTypeService opportunityTypeService,
      ITimeIntervalService timeIntervalService,
      IEngagementTypeService engagementTypeService,
      ICourseQualityLinkChecker linkChecker,
      ICourseQualityAIProvider aiProvider,
      IEnumerable<ICourseQualityDraftSuggester> suggesters)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
      _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
      _opportunityTypeService = opportunityTypeService ?? throw new ArgumentNullException(nameof(opportunityTypeService));
      _timeIntervalService = timeIntervalService ?? throw new ArgumentNullException(nameof(timeIntervalService));
      _engagementTypeService = engagementTypeService ?? throw new ArgumentNullException(nameof(engagementTypeService));
      _linkChecker = linkChecker ?? throw new ArgumentNullException(nameof(linkChecker));
      ArgumentNullException.ThrowIfNull(aiProvider, nameof(aiProvider));
      // CourseQuality:PrePublish:UseAI off (the default): the keyless reader, never the configured model, which can take minutes and is not metered here
      _aiProvider = _options.PrePublish.UseAI || aiProvider is CourseQualitySourcePageReader ? aiProvider : new CourseQualitySourcePageReader(options);
      _suggester = suggesters?.FirstOrDefault(o => o.IsEnabled);
    }
    #endregion

    #region Public Members
    /// <summary>
    /// Shared by every instance; a test gives each service its own.
    /// </summary>
    public CourseQualityDraftCache Cache { get; init; } = CourseQualityDraftCache.Shared;

    public async Task<DraftCheckResult> Check(Guid organizationId, CourseQualityDraftRequest request, CancellationToken cancellationToken)
    {
      // off means the endpoint does not exist, for everyone
      if (!_options.PrePublish.Enabled) throw new EntityNotFoundException("The pre-publish check is turned off");

      if (organizationId == Guid.Empty) throw new ArgumentNullException(nameof(organizationId));
      ArgumentNullException.ThrowIfNull(request, nameof(request));

      _organizationService.IsAdmin(organizationId, true);

      var organization = _organizationService.GetById(organizationId, false, false, false);
      var opportunity = ToOpportunity(request, organization);
      var now = DateTimeOffset.UtcNow;

      CourseQualityAssessment assessment;
      CourseQualityLinkCheckResult link;
      var format = CourseFormat.Unknown;

      if (opportunity.Type == Opportunity.Type.Job)
      {
        if (!_options.Jobs.Enabled) throw new ValidationException(NotCheckedMessage());

        // a partner posts a job on Yoma, so its link is checked; only a feed's job skips it, and a feed has no draft
        link = await CheckLink(opportunity.URL, cancellationToken);
        assessment = CourseQualityEngine.AssessJob(opportunity, link, _options, now, out _);
        format = CourseFormat.Job;
      }
      else
      {
        if (!_options.VerifiedTypes().Contains(opportunity.Type)) throw new ValidationException(NotCheckedMessage());

        link = await CheckLink(opportunity.URL, cancellationToken);
        var ai = await EvaluatePage(opportunity, link, cancellationToken);
        assessment = CourseQualityEngine.Assess(opportunity, link, ai, _options, now);
        format = assessment.LearnerCard.Format;
      }

      var issues = CourseQualityDraftWording.Issues(assessment);
      var linkChecked = link.Outcome == CourseQualityLinkOutcome.Reachable
        || (link.Outcome == CourseQualityLinkOutcome.Failed && link.Failure != CourseQualityLinkFailure.Missing);

      return new DraftCheckResult
      {
        OpportunityType = opportunity.Type,
        PredictedStatus = assessment.Status,
        Score = assessment.Score,
        Checks = [.. assessment.Criteria.Select(o => new DraftCheckCheck { Criterion = o.Criterion, Score = o.Score, Passed = !issues.Contains(o) })],
        Fixes = [.. issues.Select(o => new DraftCheckFix
        {
          Criterion = o.Criterion,
          Field = CourseQualityDraftWording.Field(o, opportunity, format, _options, now),
          HardGate = o.HardGateFailed,
          Reason = o.Reason,
          Recommendation = o.Recommendation
        })],
        LinkChecked = linkChecked,
        Summary = CourseQualityDraftWording.Summary(assessment.Status, opportunity.Type, issues.Count, linkChecked),
        Suggestions = await Suggest(opportunity, link, request.Skills?.Count > 0, cancellationToken)
      };
    }

    /// <summary>
    /// The draft as the listing would read once published and loaded: active, in its organisation, with lookups resolved. A field
    /// the partner has not filled in yet stays empty, which is what the checks then report.
    /// </summary>
    public Opportunity.Models.Opportunity ToOpportunity(CourseQualityDraftRequest request, Entity.Models.Organization organization)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));
      ArgumentNullException.ThrowIfNull(organization, nameof(organization));

      if (!request.TypeId.HasValue || request.TypeId == Guid.Empty) throw new ValidationException("Choose the opportunity type to check this listing");

      var typeLookup = _opportunityTypeService.GetByIdOrNull(request.TypeId.Value) ?? throw new ValidationException("The opportunity type is not recognised");
      var type = Enum.Parse<Opportunity.Type>(typeLookup.Name, true);

      TimeIntervalOption? interval = null;
      if (request.CommitmentIntervalId.HasValue && request.CommitmentIntervalId != Guid.Empty)
      {
        var lookup = _timeIntervalService.GetByIdOrNull(request.CommitmentIntervalId.Value);
        if (lookup != null) interval = Enum.Parse<TimeIntervalOption>(lookup.Name, true);
      }

      EngagementTypeOption? engagement = null;
      if (request.EngagementTypeId.HasValue && request.EngagementTypeId != Guid.Empty)
      {
        var lookup = _engagementTypeService.GetByIdOrNull(request.EngagementTypeId.Value);
        if (lookup != null) engagement = Enum.Parse<EngagementTypeOption>(lookup.Name, true);
      }

      return new Opportunity.Models.Opportunity
      {
        Id = request.Id ?? Guid.Empty,
        Title = request.Title?.Trim() ?? string.Empty,
        Description = request.Description ?? string.Empty,
        TypeId = request.TypeId.Value,
        Type = type,
        OrganizationId = organization.Id,
        OrganizationName = organization.Name,
        OrganizationStatusId = organization.StatusId,
        OrganizationStatus = organization.Status,
        Summary = request.Summary,
        Instructions = request.Instructions,
        URL = string.IsNullOrWhiteSpace(request.URL) ? null : request.URL.Trim(),
        Provider = request.Provider,
        CommitmentIntervalId = interval.HasValue ? request.CommitmentIntervalId : null,
        CommitmentInterval = interval,
        CommitmentIntervalCount = request.CommitmentIntervalCount,
        EngagementTypeId = engagement.HasValue ? request.EngagementTypeId : null,
        EngagementType = engagement,
        // the prediction is for the listing once published
        Status = Opportunity.Status.Active,
        Published = true,
        Hidden = request.Hidden,
        Keywords = request.Keywords,
        DateStart = request.DateStart ?? default,
        DateEnd = request.DateEnd,
        // only whether each is present is read, never which
        Countries = request.Countries?.Select(o => new OpportunityCountryInfo { Id = o.CountryId }).ToList(),
        Languages = request.Languages?.Select(o => new Lookups.Models.Language { Id = o }).ToList(),
        Skills = request.Skills?.Select(o => new Lookups.Models.Skill { Id = o }).ToList(),
        CustomFields = request.CustomFields?.Where(o => !string.IsNullOrWhiteSpace(o.Key)).Select(ToValue).ToList()
      };
    }
    #endregion

    #region Private Members
    private string NotCheckedMessage()
    {
      var types = _options.VerifiedTypes();
      if (_options.Jobs.Enabled) types.Add(Opportunity.Type.Job);
      return $"Only opportunities of type {string.Join(", ", types.Select(o => $"'{o}'"))} are checked";
    }

    /// <summary>
    /// Stored the way the custom field service stores it: one option as its key, several between delimiters.
    /// </summary>
    private static CustomFieldValueItem ToValue(CustomFieldValueRequest value)
    {
      var options = value.Values?.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).Distinct().ToList();

      return new CustomFieldValueItem
      {
        Key = value.Key.Trim(),
        DataType = options != null ? CustomFieldDataType.Option : CustomFieldDataType.String,
        ValueRaw = options == null ? value.Value
          : options.Count == 0 ? null
          : options.Count == 1 ? options[0]
          : $"{CustomFieldValue.Value_Delimiter}{string.Join(CustomFieldValue.Value_Delimiter, options)}{CustomFieldValue.Value_Delimiter}"
      };
    }

    /// <summary>
    /// The safe checker with the pre-publish timeout. The timeout is this call's own: a request the caller cancels still throws.
    /// </summary>
    private async Task<CourseQualityLinkCheckResult> CheckLink(string? url, CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(url)) return await _linkChecker.Check(url, cancellationToken);

      var key = $"link:{url}";
      var now = DateTimeOffset.UtcNow;
      if (Cache.TryGet<CourseQualityLinkCheckResult>(key, now, out var cached) && cached != null) return cached;

      using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, _options.PrePublish.LinkTimeoutMilliseconds)));

      try
      {
        var result = await _linkChecker.Check(url, timeout.Token);
        Cache.Set(key, result, DateTimeOffset.UtcNow.Add(LinkCacheDuration), DateTimeOffset.UtcNow);
        return result;
      }
      catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
      {
        var result = CourseQualityLinkCheckResult.Inconclusive(CourseQualityLinkFailure.Timeout, url);
        Cache.Set(key, result, DateTimeOffset.UtcNow.Add(TimedOutLinkCacheDuration), DateTimeOffset.UtcNow);
        return result;
      }
    }

    /// <summary>
    /// The page reader the published listing gets, with the same request. Cached on everything it reads, so an edit is read again.
    /// </summary>
    private async Task<CourseQualityAIResult?> EvaluatePage(Opportunity.Models.Opportunity opportunity, CourseQualityLinkCheckResult link, CancellationToken cancellationToken)
    {
      if (!_aiProvider.IsEnabled) return null;

      var request = new CourseQualityAIRequest
      {
        Title = opportunity.Title,
        Description = opportunity.Description,
        Type = opportunity.Type,
        Provider = string.IsNullOrWhiteSpace(opportunity.Provider) ? opportunity.OrganizationName : opportunity.Provider,
        OrganizationName = opportunity.OrganizationName,
        URL = opportunity.URL,
        DateStart = opportunity.DateStart,
        DateEnd = opportunity.DateEnd,
        CommitmentHours = opportunity.CommitmentIntervalTotalHours,
        SourcePageText = link.SourcePageText,
        SourcePageSignals = link.SourcePageSignals,
        SourcePageFinalUrl = link.FinalUrl,
        SourcePageTruncated = link.SourcePageTruncated
      };

      var key = $"page:{(_options.PrePublish.UseAI ? "ai" : "reader")}:{CourseQualityDraftCache.Hash(request.Title, request.Description, request.Type.ToString(), request.Provider, request.OrganizationName,
        request.URL, request.DateStart.ToString("O", CultureInfo.InvariantCulture), request.DateEnd?.ToString("O", CultureInfo.InvariantCulture),
        request.CommitmentHours?.ToString(CultureInfo.InvariantCulture), link.Outcome.ToString(), link.FinalUrl, link.SourcePageText,
        System.Text.Json.JsonSerializer.Serialize(link.SourcePageSignals), link.SourcePageTruncated.ToString())}";

      if (Cache.TryGet<CourseQualityAIResult>(key, DateTimeOffset.UtcNow, out var cached)) return cached;

      try
      {
        var result = await _aiProvider.Evaluate(request, cancellationToken);
        Cache.Set(key, result, DateTimeOffset.UtcNow.Add(LinkCacheDuration), DateTimeOffset.UtcNow);
        return result;
      }
      catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
      {
        // as after publishing: a provider failure means rules alone, never a pass; not cached, so the next pause tries again
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Course quality page reading failed for a draft of organization {organizationId}", opportunity.OrganizationId);
        return null;
      }
    }

    /// <summary>
    /// "Draft this for me": null unless a provider is registered and enabled; then only what it proposes for the fields it may fill.
    /// </summary>
    private async Task<List<DraftCheckSuggestion>?> Suggest(Opportunity.Models.Opportunity opportunity, CourseQualityLinkCheckResult link, bool hasSkills,
      CancellationToken cancellationToken)
    {
      if (_suggester == null) return null;

      // nothing to draft from
      if (link.Outcome != CourseQualityLinkOutcome.Reachable || string.IsNullOrWhiteSpace(link.SourcePageText)) return [];

      var request = new CourseQualityDraftSuggestionRequest
      {
        Type = opportunity.Type,
        Title = opportunity.Title,
        Summary = opportunity.Summary,
        Description = opportunity.Description,
        URL = opportunity.URL,
        DateStart = opportunity.DateStart == default ? null : opportunity.DateStart,
        DateEnd = opportunity.DateEnd,
        HasSkills = hasSkills,
        SourcePageText = link.SourcePageText,
        SourcePageSignals = link.SourcePageSignals,
        SourcePageFinalUrl = link.FinalUrl
      };

      var key = $"suggest:{CourseQualityDraftCache.Hash(request.Type.ToString(), request.Title, request.Summary, request.Description, request.URL,
        request.DateStart?.ToString("O", CultureInfo.InvariantCulture), request.DateEnd?.ToString("O", CultureInfo.InvariantCulture), hasSkills.ToString(),
        link.FinalUrl, link.SourcePageText)}";

      if (Cache.TryGet<List<DraftCheckSuggestion>>(key, DateTimeOffset.UtcNow, out var cached) && cached != null) return cached;

      List<DraftCheckSuggestion>? proposed;
      try
      {
        proposed = await _suggester.Suggest(request, cancellationToken);
      }
      catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
      {
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning(ex, "Draft suggestions failed for organization {organizationId}", opportunity.OrganizationId);
        return [];
      }

      List<DraftCheckSuggestion> results = [.. (proposed ?? [])
        .Where(o => o != null && SuggestionFields.Contains(o.Field) && !string.IsNullOrWhiteSpace(o.Value))
        .GroupBy(o => o.Field).Select(o => o.First())
        .Select(o => new DraftCheckSuggestion { Field = o.Field, Value = o.Value.Trim().Length <= MaxSuggestionLength ? o.Value.Trim() : o.Value.Trim()[..MaxSuggestionLength] })];

      Cache.Set(key, results, DateTimeOffset.UtcNow.Add(LinkCacheDuration), DateTimeOffset.UtcNow);
      return results;
    }
    #endregion
  }
}
