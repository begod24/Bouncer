using Unity.Netcode;

namespace Bouncer.Net
{
    public static class NetClock
    {
        public static double HostNow(NetworkManager network) => network.IsServer
            ? network.ServerTime.Time
            : network.LocalTime.Time - network.NetworkTimeSystem.LocalBufferSec;
    }
}
