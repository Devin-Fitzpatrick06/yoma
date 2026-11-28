using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yoma.Core.Domain.CourseQuality.Helpers;
using Yoma.Core.Domain.CourseQuality.Models;
using Yoma.Core.Domain.CourseQuality.Services;
using Yoma.Core.Infrastructure.Anthropic.Interfaces;
using Yoma.Core.Infrastructure.Anthropic.Models;

namespace Yoma.Core.Infrastructure.Anthropic.Services
{
  /// <summary>
  /// Asks Claude to compare a listing with its source page. The prompt, the schema, the untrusted-data delimiters and the
  /// reply parser are shared with every provider (<see cref="CourseQualityAIPrompt"/>), and so is the rule that Claude
  /// can only lower the keyless reader's result (<see cref="CourseQualityModelProvider"/>). Anything other than end_turn,
  /// an invalid reply or a failed call gives null, and null falls back to the keyless reader.
  /// </summary>
  public sealed class AnthropicCourseQualityProvider : CourseQualityModelProvider
  {
    #region Class Variables
    private const string EndTurn = "end_turn";

    // the handover package (docs/ziqi-handover/prompts) is generated from these, by reflection in HandoverExportTests
    private const string SystemPrompt = CourseQualityAIPrompt.SystemPrompt;
    private const string OutputSchema = CourseQualityAIPrompt.OutputSchema;

    private readonly AnthropicOptions _options;
    private readonly IAnthropicMessageClient _client;
    #endregion

    #region Constructor
    public AnthropicCourseQualityProvider(ILogger<AnthropicCourseQualityProvider> logger, IOptions<AnthropicOptions> options, IOptions<CourseQualityOptions> courseQualityOptions,
      IAnthropicMessageClient client, IOptions<CourseQualityAIOptions>? aiOptions = null)
      : base(logger, courseQualityOptions?.Value!, aiOptions?.Value ?? new CourseQualityAIOptions())
    {
      _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
      _client = client ?? throw new ArgumentNullException(nameof(client));
    }
    #endregion

    #region Public Members
    public override bool IsEnabled => _options.HasApiKey;

    public override CourseQualityAIEndpoint Endpoint => new(AIProviderKind.Anthropic, _options.Model, null, false, _options.HasApiKey);
    #endregion

    #region Protected Members
    protected override async Task<CourseQualityModelReply> Send(CourseQualityModelPrompt prompt, CancellationToken cancellationToken)
    {
      var completion = await _client.Create(new AnthropicCompletionRequest(SystemPrompt, prompt.User, OutputSchema), cancellationToken);

      return new CourseQualityModelReply(completion.StopReason == EndTurn, completion.StopReason, completion.Text, completion.InputTokens, completion.OutputTokens);
    }
    #endregion
  }
}
