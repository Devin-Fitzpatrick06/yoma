using Yoma.Core.Domain.CourseQuality.Models;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ILinkHealthService
  {
    /// <summary>
    /// Checks up to 200 distinct addresses from any platform with the safe link checker and reports each in plain words.
    /// Throws a validation error for an empty list or one over the limit. A bad link never throws: it is a result. Works
    /// whether or not CourseQuality:Enabled is on, because it stores nothing and changes no listing.
    /// </summary>
    Task<LinkHealthReport> Check(LinkHealthRequest request, CancellationToken cancellationToken);
  }
}
