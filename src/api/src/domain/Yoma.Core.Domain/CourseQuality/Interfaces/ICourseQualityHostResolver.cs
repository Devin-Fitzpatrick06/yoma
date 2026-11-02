using System.Net;

namespace Yoma.Core.Domain.CourseQuality.Interfaces
{
  public interface ICourseQualityHostResolver
  {
    /// <summary>
    /// Throws <see cref="System.Net.Sockets.SocketException"/> when the name cannot be resolved.
    /// </summary>
    Task<IPAddress[]> Resolve(string host, CancellationToken cancellationToken);
  }
}
