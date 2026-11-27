using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  /// <summary>
  /// "Draft this for me": proposes a summary, dates and skills from the page a draft's link opens. Optional: with none registered
  /// the draft check returns no suggestions (null). An implementation belongs with the AI provider (Anthropic infrastructure)
  /// and is registered only when that provider has a key.
  /// </summary>
  public interface ICourseQualityDraftSuggester
  {
    bool IsEnabled { get; }

    /// <summary>
    /// Null when the provider declines or fails. Fields other than summary, dateStart, dateEnd and skills are dropped.
    /// </summary>
    Task<List<DraftCheckSuggestion>?> Suggest(CourseQualityDraftSuggestionRequest request, CancellationToken cancellationToken);
  }
}
