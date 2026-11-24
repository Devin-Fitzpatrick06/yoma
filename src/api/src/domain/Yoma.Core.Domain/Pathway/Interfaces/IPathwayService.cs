using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Interfaces
{
  public interface IPathwayService
  {
    /// <summary>
    /// The onboarding questions, from configuration.
    /// </summary>
    PathwayQuestions GetQuestions();

    /// <summary>
    /// The signed-in youth's answers. Throws EntityNotFoundException (404) when they have not onboarded.
    /// </summary>
    PathwayProfile GetProfile();

    Task<PathwayProfile> UpsertProfile(PathwayProfileRequest request);

    /// <summary>
    /// The signed-in youth's path, YoID summary and jobs. Throws EntityNotFoundException (404) when they have not onboarded.
    /// </summary>
    Task<MyYoma> Get();
  }
}
