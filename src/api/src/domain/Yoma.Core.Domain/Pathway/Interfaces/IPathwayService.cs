using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Interfaces
{
  public interface IPathwayService
  {
    /// <summary>
    /// The onboarding questions, from configuration. For a signed-in youth who has not set up My Yoma, the questions Yoma's own
    /// preferences answer are left out and listed in AnsweredFromPreferences.
    /// </summary>
    PathwayQuestions GetQuestions();

    /// <summary>
    /// The signed-in youth's answers. Throws EntityNotFoundException (404) when they have not onboarded.
    /// </summary>
    PathwayProfile GetProfile();

    /// <summary>
    /// Saves the answers. The first time, the request's values for questions answered from Yoma's preferences (the web sends fallbacks
    /// for those hidden questions) are ignored and the preferences used instead. Answers the youth gave that have a home in Yoma's
    /// preferences are then saved back to them; values taken from preferences never are.
    /// </summary>
    Task<PathwayProfile> UpsertProfile(PathwayProfileRequest request);

    /// <summary>
    /// The signed-in youth's path, YoID summary and jobs. Throws EntityNotFoundException (404) when they have not onboarded.
    /// </summary>
    Task<MyYoma> Get();

    /// <summary>
    /// Up to Pathway:MaxAlternatives (3) steps that could replace one on the signed-in youth's path: the same stage and step type, not
    /// already on the path, best first; empty when there are none. Throws EntityNotFoundException (404) when they have not onboarded or
    /// the opportunity is not a step on their path.
    /// </summary>
    Task<List<PathwayCourse>> ListAlternatives(Guid opportunityId);
  }
}
