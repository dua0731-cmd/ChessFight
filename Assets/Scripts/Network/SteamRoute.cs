using Steamworks;

namespace ChessFight.Network
{
    /// <summary>
    /// R306 [0] (HOST_AUTHORITY §5): how this PC's Steam session with a player goes - straight between the two PCs, or
    /// through Valve's relays (SteamNetConnectionInfo_t's Relayed flag) - and Steam's ping on it, for the metrics box only.
    /// Nothing here changes how Steam connects: whether a direct route may be tried is each player's own Steam setting
    /// (Settings, In Game, Steam Networking), and a direct route shows each side the other's IP address.
    /// </summary>
    public static class SteamRoute
    {
        /// <summary>False while there is no connected session with that player.</summary>
        public static bool Read(ulong id, out bool relayed, out int pingMs)
        {
            relayed = false;
            pingMs = -1;
            if (id == 0) return false;
            var remote = new SteamNetworkingIdentity();
            remote.SetSteamID64(id);
            var state = SteamNetworkingMessages.GetSessionConnectionInfo(ref remote, out SteamNetConnectionInfo_t info, out SteamNetConnectionRealTimeStatus_t status);
            if (state != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected) return false;
            relayed = (info.m_nFlags & Constants.k_nSteamNetworkConnectionInfoFlags_Relayed) != 0;
            pingMs = status.m_nPing;
            return true;
        }

        /// <summary>R521: the routes as numbers for the metrics box: on a client its route to the host; on the host how many
        /// players go straight and how many through relays (bots and itself not counted), and the worst ping. It asks Steam
        /// once a player: read it once a second, not every frame.</summary>
        public static RouteSummary Summary(SteamSession session)
        {
            var route = RouteSummary.None;
            if (session == null || session.Match == 0) return route;
            if (!session.IsHost)
            {
                if (Read(session.Host, out bool relayed, out int ping))
                {
                    if (relayed) route.Relayed = 1;
                    else route.Direct = 1;
                    route.PingMs = ping;
                }
                return route;
            }
            foreach (ulong id in session.Roster.Keys)
            {
                if (id == session.Self || BotIdentity.IsBot(id) || !Read(id, out bool viaRelay, out int ms)) continue;
                if (viaRelay) route.Relayed++;
                else route.Direct++;
                if (ms > route.PingMs) route.PingMs = ms;
            }
            return route;
        }

        /// <summary>The metrics box's line for <paramref name="route"/> (<see cref="Summary"/>): on a client its route to the
        /// host, on the host how many players go straight and how many through relays, and the worst ping. Empty outside a
        /// match.</summary>
        public static string Line(SteamSession session, RouteSummary route)
        {
            if (session == null || session.Match == 0) return "";
            if (!session.IsHost)
                return route.Known ? $"{(route.Relayed > 0 ? "릴레이" : "직접")} · Steam 핑 {route.PingMs} ms" : "방장과 연결 정보 없음";
            return route.Known ? $"직접 {route.Direct}명 · 릴레이 {route.Relayed}명 · Steam 핑 최대 {route.PingMs} ms" : "참가자와 연결 정보 없음";
        }
    }
}
