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

        /// <summary>The metrics box's line: on a client its route to the host; on the host how many players go straight
        /// and how many through relays, and the worst ping. A string a call: read it once a second, not every frame.</summary>
        public static string Line(SteamSession session)
        {
            if (session == null || session.Match == 0) return "";
            if (!session.IsHost)
                return Read(session.Host, out bool relayed, out int ping) ? $"{(relayed ? "릴레이" : "직접")} · Steam 핑 {ping} ms" : "방장과 연결 정보 없음";
            int direct = 0, relays = 0, worst = -1;
            foreach (ulong id in session.Roster.Keys)
            {
                if (id == session.Self || BotIdentity.IsBot(id) || !Read(id, out bool viaRelay, out int ms)) continue;
                if (viaRelay) relays++;
                else direct++;
                if (ms > worst) worst = ms;
            }
            return direct + relays == 0 ? "참가자와 연결 정보 없음" : $"직접 {direct}명 · 릴레이 {relays}명 · Steam 핑 최대 {worst} ms";
        }
    }
}
