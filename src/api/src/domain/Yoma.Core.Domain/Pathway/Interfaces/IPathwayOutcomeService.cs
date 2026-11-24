using Yoma.Core.Domain.Pathway.Models;

namespace Yoma.Core.Domain.Pathway.Interfaces
{
  public interface IPathwayOutcomeService
  {
    /// <summary>
    /// Outcome figures for every course, cached for Pathway:CacheInMinutes.
    /// </summary>
    PathwayOutcomeSnapshot GetSnapshot();

    /// <summary>
    /// One Learning opportunity's figures, without continuation. Throws EntityNotFoundException for anything else.
    /// </summary>
    CourseOutcome GetOutcome(Guid opportunityId);
  }
}
