using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>What a model provider sends: the shared system prompt, the per-call user message and the output schema.</summary>
  public sealed record CourseQualityModelPrompt(string System, string User, string JsonSchema, string SchemaName);

  /// <summary>
  /// What came back. Finished is the provider's own "ended normally" (Anthropic end_turn, OpenAI stop); anything else
  /// (a refusal, a length cut) is not an answer.
  /// </summary>
  public sealed record CourseQualityModelReply(bool Finished, string? StopReason, string? Text, long InputTokens, long OutputTokens);

  /// <summary>
  /// The flow every model provider shares, so the safety design cannot drift between them: no page means no call; the
  /// keyless reader's result is computed first and is the ceiling; the one prompt and schema are sent; and the one parser
  /// validates the reply, which can only lower that ceiling. Every call is metered (tokens, cost, duration, outcome),
  /// including calls that fail, are declined or answer outside the schema. A subclass only moves bytes.
  /// </summary>
  public abstract class CourseQualityModelProvider : ICourseQualityMeteredAIProvider
  {
    #region Class Variables
    private readonly ILogger _logger;
    private readonly CourseQualityOptions _courseQualityOptions;
    private readonly CourseQualityAIOptions _aiOptions;
    #endregion

    #region Constructor
    protected CourseQualityModelProvider(ILogger logger, CourseQualityOptions courseQualityOptions, CourseQualityAIOptions aiOptions)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _courseQualityOptions = courseQualityOptions ?? throw new ArgumentNullException(nameof(courseQualityOptions));
      _aiOptions = aiOptions ?? throw new ArgumentNullException(nameof(aiOptions));
    }
    #endregion

    #region Public Members
    public abstract bool IsEnabled { get; }

    /// <summary>
    /// The model this provider calls, for usage records and the usage summary.
    /// </summary>
    public abstract CourseQualityAIEndpoint Endpoint { get; }

    public async Task<CourseQualityAIResult?> Evaluate(CourseQualityAIRequest request, CancellationToken cancellationToken)
    {
      return (await Call(request, cancellationToken)).Result;
    }

    public async Task<CourseQualityAICall> Call(CourseQualityAIRequest request, CancellationToken cancellationToken)
    {
      ArgumentNullException.ThrowIfNull(request, nameof(request));

      // nothing to compare: no model is called and the rules decide
      if (string.IsNullOrWhiteSpace(request.SourcePageText)) return CourseQualityAICall.NotCalled;

      var ceiling = await new CourseQualitySourcePageReader(Options.Create(_courseQualityOptions)).Evaluate(request, cancellationToken);
      if (ceiling == null) return CourseQualityAICall.NotCalled;

      var endpoint = Endpoint;
      var model = endpoint.Model ?? string.Empty;
      var prompt = new CourseQualityModelPrompt(CourseQualityAIPrompt.SystemPrompt, CourseQualityAIPrompt.BuildUserMessage(request, MaxPageChars),
        CourseQualityAIPrompt.OutputSchema, CourseQualityAIPrompt.SchemaName);

      var dateRun = DateTimeOffset.UtcNow;
      var stopwatch = Stopwatch.StartNew();

      CourseQualityModelReply reply;
      try
      {
        reply = await Send(prompt, cancellationToken);
      }
      catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
      {
        // the type only: an SDK or server message can echo request details. A timeout lands here too
        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning("Course quality {provider} call failed: {errorType}", endpoint.Provider, ex.GetType().Name);
        return new CourseQualityAICall(null, Usage(endpoint, 0, 0, stopwatch, dateRun, CourseQualityAIOutcome.Failed));
      }

      stopwatch.Stop();

      // a refusal, a length cut or anything else unexpected is not an answer
      if (!reply.Finished)
      {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Course quality {provider} call stopped with {stopReason}", endpoint.Provider, reply.StopReason);
        return new CourseQualityAICall(null, Usage(endpoint, reply.InputTokens, reply.OutputTokens, stopwatch, dateRun, CourseQualityAIOutcome.Declined));
      }

      var result = CourseQualityAIPrompt.Parse(reply.Text, ceiling, string.IsNullOrWhiteSpace(model) ? endpoint.Provider.ToString() : model);
      var usage = Usage(endpoint, reply.InputTokens, reply.OutputTokens, stopwatch, dateRun, result == null ? CourseQualityAIOutcome.Invalid : CourseQualityAIOutcome.Answered);

      if (result == null)
      {
        if (_logger.IsEnabled(LogLevel.Information)) _logger.LogInformation("Course quality {provider} reply was not in the schema", endpoint.Provider);
        return new CourseQualityAICall(null, usage);
      }

      result.Usage = usage;
      return new CourseQualityAICall(result, usage);
    }
    #endregion

    #region Protected Members
    /// <summary>
    /// Null sends the full page reading; a provider for a small model sets a limit.
    /// </summary>
    protected virtual int? MaxPageChars => null;

    /// <summary>
    /// Sends the prompt. Throws on transport failure or timeout; the base turns that into a Failed call.
    /// </summary>
    protected abstract Task<CourseQualityModelReply> Send(CourseQualityModelPrompt prompt, CancellationToken cancellationToken);
    #endregion

    #region Private Members
    private CourseQualityAIUsage Usage(CourseQualityAIEndpoint endpoint, long inputTokens, long outputTokens, Stopwatch stopwatch, DateTimeOffset dateRun,
      CourseQualityAIOutcome outcome)
    {
      inputTokens = Math.Max(0, inputTokens);
      outputTokens = Math.Max(0, outputTokens);

      return new CourseQualityAIUsage
      {
        Provider = endpoint.Provider,
        Model = endpoint.Model ?? string.Empty,
        EndpointHost = endpoint.EndpointHost,
        IsLocal = endpoint.IsLocal,
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        CostUsd = _aiOptions.Cost(endpoint.Model, endpoint.IsLocal, inputTokens, outputTokens),
        DurationMs = stopwatch.ElapsedMilliseconds,
        DateRun = dateRun,
        Outcome = outcome
      };
    }
    #endregion
  }
}
