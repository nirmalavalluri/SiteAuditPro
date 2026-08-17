using System.Net;
using System.Net.Sockets;

namespace AuditVitals.Application.Security;

/// <summary>
/// Determines whether an IP address falls in a range that must never be reached
/// by the crawler (loopback, private, link-local/metadata, CGNAT, multicast,
/// reserved). Used both by the submission-time <see cref="SsrfSafeUrlValidator"/>
/// and by the connect-time guard in the safe HTTP fetcher, so both layers apply
/// exactly the same rules.
/// </summary>
public static class IpAddressSafetyClassifier
{
    private static readonly IPNetwork[] ProhibitedIPv4Networks =
    [
        IPNetwork.Parse("0.0.0.0/8"),          // "this" network
        IPNetwork.Parse("10.0.0.0/8"),         // private
        IPNetwork.Parse("100.64.0.0/10"),      // carrier-grade NAT
        IPNetwork.Parse("127.0.0.0/8"),        // loopback
        IPNetwork.Parse("169.254.0.0/16"),     // link-local (covers 169.254.169.254 cloud metadata)
        IPNetwork.Parse("172.16.0.0/12"),      // private
        IPNetwork.Parse("192.0.0.0/24"),       // IETF protocol assignments
        IPNetwork.Parse("192.0.2.0/24"),       // TEST-NET-1
        IPNetwork.Parse("192.168.0.0/16"),     // private
        IPNetwork.Parse("198.18.0.0/15"),      // benchmarking
        IPNetwork.Parse("198.51.100.0/24"),    // TEST-NET-2
        IPNetwork.Parse("203.0.113.0/24"),     // TEST-NET-3
        IPNetwork.Parse("224.0.0.0/4"),        // multicast
        IPNetwork.Parse("240.0.0.0/4"),        // reserved
        IPNetwork.Parse("255.255.255.255/32"), // limited broadcast
    ];

    private static readonly IPNetwork[] ProhibitedIPv6Networks =
    [
        IPNetwork.Parse("::1/128"),            // loopback
        IPNetwork.Parse("::/128"),              // unspecified
        IPNetwork.Parse("::ffff:0:0/96"),       // IPv4-mapped (unwrapped separately, kept as belt-and-braces)
        IPNetwork.Parse("64:ff9b::/96"),        // NAT64 well-known prefix
        IPNetwork.Parse("100::/64"),            // discard-only
        IPNetwork.Parse("2001:db8::/32"),       // documentation
        IPNetwork.Parse("fc00::/7"),            // unique local (private)
        IPNetwork.Parse("fe80::/10"),           // link-local (covers AWS IMDSv6 fd00:ec2::254 too, see fc00::/7)
        IPNetwork.Parse("ff00::/8"),            // multicast
    ];

    public static bool IsProhibited(IPAddress address, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(address);

        // Unwrap IPv4-mapped IPv6 addresses (e.g. ::ffff:127.0.0.1) so the IPv4
        // rules apply to the embedded address instead of being bypassed.
        var effectiveAddress = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        var networks = effectiveAddress.AddressFamily switch
        {
            AddressFamily.InterNetwork => ProhibitedIPv4Networks,
            AddressFamily.InterNetworkV6 => ProhibitedIPv6Networks,
            _ => null,
        };

        if (networks is null)
        {
            reason = $"Unsupported address family '{effectiveAddress.AddressFamily}'.";
            return true;
        }

        foreach (var network in networks)
        {
            if (network.Contains(effectiveAddress))
            {
                reason = $"Address {address} falls in prohibited range {network}.";
                return true;
            }
        }

        reason = null;
        return false;
    }
}
