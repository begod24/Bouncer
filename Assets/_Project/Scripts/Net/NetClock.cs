using Unity.Netcode;

namespace Bouncer.Net
{
    /// <summary>
    /// Общие часы комнаты — время хозяина. У хозяина оно точное, у гостя — оценка: время сети NGO у гостя впереди
    /// на задержку и запас в один такт (LocalTime), запас вычитаем.
    /// </summary>
    public static class NetClock
    {
        public static double HostNow(NetworkManager network) => network.IsServer
            ? network.ServerTime.Time
            : network.LocalTime.Time - network.NetworkTimeSystem.LocalBufferSec;
    }
}
