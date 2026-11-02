using Yoma.Core.Domain.CourseQuality.Interfaces;
using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Infrastructure.Anthropic.Services
{
  /// <summary>
  /// Claude first, the keyless reader when Claude declines or fails.
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
      var result = _primary.IsEnabled ? await _primary.Evaluate(request, cancellationToken) : null;
      if (result != null || !_fallback.IsEnabled) return result;

      return await _fallback.Evaluate(request, cancellationToken);
    }
    #endregion
  }
}
