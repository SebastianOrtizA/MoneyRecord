using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MoneyRecord.Services.Sync.Lan
{
    public static class LanAddresses
    {
        /// <summary>
        /// The IPv4 addresses of this device on the networks it is connected to (Wi-Fi, Ethernet, a hotspot), most likely first.
        /// Loopback, link-local and addresses of interfaces that are down are left out.
        /// </summary>
        public static IReadOnlyList<string> Local() =>
            NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .SelectMany(n => n.GetIPProperties().UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => (Address: a.Address, HasGateway: n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))))
                .Where(a => !IPAddress.IsLoopback(a.Address) && !IsLinkLocal(a.Address))
                .OrderByDescending(a => a.HasGateway)
                .Select(a => a.Address.ToString())
                .Distinct()
                .ToList();

        private static bool IsLinkLocal(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 169 && bytes[1] == 254;
        }
    }
}
