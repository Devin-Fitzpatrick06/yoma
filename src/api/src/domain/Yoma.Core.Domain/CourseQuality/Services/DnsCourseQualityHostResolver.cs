using System.Net;
using Yoma.Core.Domain.CourseQuality.Interfaces;

namespace Yoma.Core.Domain.CourseQuality.Services
{
  public class DnsCourseQualityHostResolver : ICourseQualityHostResolver
  {
    public async Task<IPAddress[]> Resolve(string host, CancellationToken cancellationToken)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(host, nameof(host));

      // the OS lookup may ignore cancellation, so the wait must not depend on it
      return IPAddress.TryParse(host, out var address)
        ? [address]
        : await Dns.GetHostAddressesAsync(host, cancellationToken).WaitAsync(cancellationToken);
    }
  }
}
