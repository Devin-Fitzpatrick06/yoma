using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Infrastructure.OpenAICompatible.Interfaces;
using Yoma.Core.Infrastructure.OpenAICompatible.Models;

namespace Yoma.Core.Infrastructure.OpenAICompatible.Services
{
  /// <summary>
  /// The course page check on any OpenAI-compatible model server (Ollama, vLLM, LM Studio, a government host). Same prompt,
  /// schema, delimiters and "can only lower" rule as Claude, through <see cref="CourseQualityModelProvider"/>. Only a
  /// finish_reason of "stop" without a refusal is an answer; anything else, an invalid reply or a failed call gives null, and
  /// null falls back to the keyless reader. Page text is cut to MaxPageChars for small local models.
  /// </summary>
  public sealed class OpenAICompatibleCourseQualityProvider : CourseQualityModelProvider
  {
    #region Class Variables
    private const string Stop = "stop";

    private readonly CourseQualityOpenAICompatibleOptions _options;
    private readonly IOpenAICompatibleChatClient _client;
    #endregion

    #region Constructor
    public OpenAICompatibleCourseQualityProvider(ILogger<OpenAICompatibleCourseQualityProvider> logger, IOptions<CourseQualityAIOptions> aiOptions,
      IOptions<CourseQualityOptions> courseQualityOptions, IOpenAICompatibleChatClient client)
      : base(logger, courseQualityOptions?.Value!, aiOptions?.Value!)
    {
      _options = aiOptions!.Value.OpenAICompatible ?? throw new ArgumentNullException(nameof(aiOptions));
      _client = client ?? throw new ArgumentNullException(nameof(client));
    }
    #endregion

    #region Public Members
    public override bool IsEnabled => _options.IsConfigured;

    public override CourseQualityAIEndpoint Endpoint => new(AIProviderKind.OpenAICompatible, _options.Model, _options.EndpointHost, _options.IsLocal, _options.IsConfigured);
    #endregion

    #region Protected Members
    protected override int? MaxPageChars => _options.MaxPageChars > 0 ? _options.MaxPageChars : null;

    protected override async Task<CourseQualityModelReply> Send(CourseQualityModelPrompt prompt, CancellationToken cancellationToken)
    {
      var completion = await _client.Create(new OpenAICompatibleChatRequest(prompt.System, prompt.User, prompt.JsonSchema, prompt.SchemaName), cancellationToken);

      // structured output reports a refusal beside a "stop"
      var finished = completion.FinishReason == Stop && string.IsNullOrEmpty(completion.Refusal);
      var stopReason = string.IsNullOrEmpty(completion.Refusal) ? completion.FinishReason : "refusal";

      return new CourseQualityModelReply(finished, stopReason, completion.Content, completion.PromptTokens, completion.CompletionTokens);
    }
    #endregion
  }
}
