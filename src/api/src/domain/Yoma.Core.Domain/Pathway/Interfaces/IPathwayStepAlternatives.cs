using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Interfaces
{
  /// <summary>
  /// Adapter for a step's alternatives, owned by the step work until the engine's own IPathwayService.ListAlternatives lands
  /// (iteration 4, engineer A3). The lead reconciles: once that method exists, the step service calls it and this adapter goes.
  /// </summary>
  public interface IPathwayStepAlternatives
  {
    /// <summary>
    /// Up to three courses for the stage that holds <paramref name="opportunityId"/> on <paramref name="myYoma"/>, best first, none
    /// already on the path. Throws EntityNotFoundException when the step is not on the path.
    /// </summary>
    List<PathwayCourse> List(MyYoma myYoma, Guid opportunityId);
  }
}
