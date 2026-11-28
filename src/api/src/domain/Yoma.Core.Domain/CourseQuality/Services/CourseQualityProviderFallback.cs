using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  /// <summary>
  /// The configured model first, the keyless reader when the model declines, fails or answers outside the schema. A metered
  /// model's usage stays on the result either way, so a call that cost tokens and gave no answer is still accounted for.
  /// </summary>
  public sealed class CourseQualityProviderFallback : ICourseQualityAIProvider
  {
    #region Class Variables
    private readonly ICourseQualityAIProvider _primary;
    private readonly ICourseQualityAIProvider _fallback;
    #endregion

    #region Constructor
    public CourseQualityProviderFallback(ICourseQualityAIProvider primary, ICourseQualityAIProvider fallback)
    {
      _primary = primary ?? throw new ArgumentNullException(nameof(primary));
      _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }
    #endregion

    #region Public Members
    public bool IsEnabled => _primary.IsEnabled || _fallback.IsEnabled;

    public async Task<CourseQualityAIResult?> Evaluate(CourseQualityAIRequest request, CancellationToken cancellationToken)
    {
      var call = !_primary.IsEnabled ? CourseQualityAICall.NotCalled
        : _primary is ICourseQualityMeteredAIProvider metered ? await metered.Call(request, cancellationToken)
        : new CourseQualityAICall(await _primary.Evaluate(request, cancellationToken), null);

      if (call.Result != null)
      {
        call.Result.Usage ??= call.Usage;
        return call.Result;
      }

      if (!_fallback.IsEnabled) return null;

      var result = await _fallback.Evaluate(request, cancellationToken);
      if (result != null && call.Usage != null) result.Usage = call.Usage;
      return result;
    }
    #endregion
  }
}
