using System.Net;
using System.Net.Sockets;

namespace Yoma.Core.Domain.CourseQuality.Helpers
{
  /// <summary>
  /// Decides which addresses the link checker may connect to. Listing URLs are partner input, so anything
  /// that is not plainly on the public internet is refused.
  /// </summary>
  public static class CourseQualityAddressPolicy
  {
    #region Class Variables
    private static readonly (IPAddress Network, int PrefixLength)[] BlockedRanges =
    [
      (IPAddress.Parse("0.0.0.0"), 8),          // this network
      (IPAddress.Parse("10.0.0.0"), 8),         // private
      (IPAddress.Parse("100.64.0.0"), 10),      // carrier-grade NAT; includes Alibaba metadata 100.100.100.200
      (IPAddress.Parse("127.0.0.0"), 8),        // loopback
      (IPAddress.Parse("168.63.129.16"), 32),   // Azure platform endpoint; publicly routed range, but internal
      (IPAddress.Parse("169.254.0.0"), 16),     // link-local; includes AWS/GCP/Azure metadata 169.254.169.254
      (IPAddress.Parse("172.16.0.0"), 12),      // private
      (IPAddress.Parse("192.0.0.0"), 24),       // IETF protocol assignments; includes Oracle metadata 192.0.0.192
      (IPAddress.Parse("192.0.2.0"), 24),       // documentation
      (IPAddress.Parse("192.88.99.0"), 24),     // 6to4 relay
      (IPAddress.Parse("192.168.0.0"), 16),     // private
      (IPAddress.Parse("198.18.0.0"), 15),      // benchmarking
      (IPAddress.Parse("198.51.100.0"), 24),    // documentation
      (IPAddress.Parse("203.0.113.0"), 24),     // documentation
      (IPAddress.Parse("224.0.0.0"), 4),        // multicast
      (IPAddress.Parse("240.0.0.0"), 4),        // reserved and broadcast
      (IPAddress.Parse("::"), 96),              // unspecified, loopback and IPv4-compatible
      (IPAddress.Parse("::ffff:0:0:0"), 96),    // IPv4-translated
      (IPAddress.Parse("64:ff9b::"), 96),       // NAT64 can reach embedded IPv4 addresses
      (IPAddress.Parse("64:ff9b:1::"), 48),     // local-use NAT64
      (IPAddress.Parse("100::"), 64),           // discard
      (IPAddress.Parse("2001::"), 32),          // Teredo tunnelling
      (IPAddress.Parse("2001:db8::"), 32),      // documentation
      (IPAddress.Parse("2002::"), 16),          // 6to4 tunnelling
      (IPAddress.Parse("fc00::"), 7),           // unique local; includes AWS metadata fd00:ec2::254
      (IPAddress.Parse("fe80::"), 10),          // link-local
      (IPAddress.Parse("fec0::"), 10),          // site-local
      (IPAddress.Parse("ff00::"), 8),           // multicast
    ];
    #endregion

    #region Public Members
    public static bool IsPublic(IPAddress address)
    {
      ArgumentNullException.ThrowIfNull(address, nameof(address));

      if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

      // an IPv6 scope id only exists on local addresses
      if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0) return false;
      if (address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)) return false;

      return !BlockedRanges.Any(o => InRange(address, o.Network, o.PrefixLength));
    }
    #endregion

    #region Private Members
    private static bool InRange(IPAddress address, IPAddress network, int prefixLength)
    {
      if (address.AddressFamily != network.AddressFamily) return false;

      var addressBytes = address.GetAddressBytes();
      var networkBytes = network.GetAddressBytes();

      var wholeBytes = prefixLength / 8;
      for (var i = 0; i < wholeBytes; i++)
        if (addressBytes[i] != networkBytes[i]) return false;

      var remainingBits = prefixLength % 8;
      if (remainingBits == 0) return true;

      var mask = (byte)(0xFF << (8 - remainingBits));
      return (addressBytes[wholeBytes] & mask) == (networkBytes[wholeBytes] & mask);
    }
    #endregion
  }
}
