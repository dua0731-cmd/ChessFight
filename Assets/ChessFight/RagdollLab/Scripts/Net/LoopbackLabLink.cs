using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The lab's network match over a plain UDP socket, no Steam (R111, development). Steam allows one account per PC, so
    /// two copies of the game on one PC cannot meet over Steam; this carrier lets them (the editor as the host, a built
    /// copy as a client, or two PCs on the same LAN by address). Everything above the socket is the real match
    /// (LabNetLink): the same packets, the same skills, the same playback. What it does not do: rooms, parties, the host
    /// election or hand-over (the first copy hosts until it closes).
    ///
    /// Start it with <c>-labLoopback host</c> / <c>-labLoopback client [address]</c> on the command line (port
    /// <c>-labLoopbackPort</c>, default 47611; <c>-labTrace</c> logs what a client is sent and plays), from the Steam
    /// link's panel (F3), or from the editor: <see cref="StartHost"/>, <see cref="StartClient"/>. The host can drive its
    /// clients with development commands (<see cref="LabNetLink.SendDev"/>): that is how the AI tries a client's skills on
    /// one PC and takes screenshots of what the client sees.
    /// </summary>
    public sealed class LoopbackLabLink : LabNetLink
    {
        public const int DefaultPort = 47611;
        const ulong HostId = 1;
        const ulong MatchId = 0x4C4F4F50;   // "LOOP"
        const uint ControlMagic = 0x424C4643;   // "CFLB"
        const byte Hello = 1, Welcome = 2, Roster = 3, Bye = 4;
        const float ClientTimeout = 5f;

        /// <summary>The command line asks for a loopback test (the Steam link stays out of the way then).</summary>
        public static bool Requested => Arg("-labLoopback") != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string mode = Arg("-labLoopback");
            if (mode == null || FindFirstObjectByType<LabGame>() == null || FindFirstObjectByType<LabNetLink>() != null) return;
            if (HasArg("-labTrace")) Trace = true;
            int port = int.TryParse(Arg("-labLoopbackPort"), out int p) ? p : DefaultPort;
            if (mode == "host") StartHost(port);
            else StartClient(Arg("-labLoopback", 2) ?? "127.0.0.1", port);
        }

        /// <summary>Host a loopback match here (any other link in the scene is closed first).</summary>
        public static LoopbackLabLink StartHost(int port = DefaultPort) => Launch(true, "", port);

        /// <summary>Join the loopback match at <paramref name="address"/> (this PC by default).</summary>
        public static LoopbackLabLink StartClient(string address = "127.0.0.1", int port = DefaultPort) => Launch(false, address, port);

        /// <summary>Close the loopback link (back to no network; reopen the scene for Steam).</summary>
        public static void Stop()
        {
            var link = Current as LoopbackLabLink;
            if (link == null) return;
            link.Shutdown();
            Destroy(link.gameObject);
        }

        static LoopbackLabLink Launch(bool host, string address, int port)
        {
            if (Current != null)
            {
                // Possibly called from that link's own panel: stop it now (a loopback link lets go of its port at once,
                // so this one can take it), take it away at the end of the frame.
                Current.Shutdown();
                Destroy(Current.gameObject);
            }
            var link = new GameObject(host ? "ChessFight Loopback Net (host)" : "ChessFight Loopback Net (client)").AddComponent<LoopbackLabLink>();
            link.Open(host, address, port);
            return link;
        }

        UdpClient socket;
        bool hosting, welcomed;
        ulong self;
        IPEndPoint hostEnd;
        int port;
        string address = "";
        string status = "";
        float helloAt, rosterAt;
        ulong nextClient = HostId + 1;
        readonly Dictionary<ulong, IPEndPoint> peers = new Dictionary<ulong, IPEndPoint>();
        readonly Dictionary<IPEndPoint, ulong> peerIds = new Dictionary<IPEndPoint, ulong>();
        readonly Dictionary<ulong, float> heardAt = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, LabSeat> seats = new Dictionary<ulong, LabSeat>();
        readonly List<ulong> stale = new List<ulong>();

        protected override bool NetOnline => socket != null;
        protected override bool NetActive => hosting ? peers.Count > 0 : welcomed && seats.Count > 1;
        protected override bool NetIsHost => hosting;
        protected override ulong NetSelf => self;
        protected override ulong NetHost => HostId;
        protected override ulong NetMatch => MatchId;
        protected override IReadOnlyDictionary<ulong, LabSeat> NetRoster => seats;
        protected override string NetName(ulong id) => id == HostId ? "방장 PC" : $"참가자 {id - HostId}";
        protected override string NetKind => "로컬 시험";
        protected override bool AcceptsDev => true;
        protected override double NetServerSeconds() => (DateTime.UtcNow - new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        int openTries;
        float retryAt;

        void Open(bool host, string to, int at)
        {
            hosting = host;
            port = at;
            address = to;
            openTries++;
            try
            {
                socket = host ? new UdpClient(new IPEndPoint(IPAddress.Any, at)) : new UdpClient(new IPEndPoint(IPAddress.Any, 0));
                socket.Client.Blocking = false;
                // Windows reports "port unreachable" from an earlier send as an error on the next receive; switch it off
                // (SIO_UDP_CONNRESET), or a closed copy would break the other one's socket.
                try { socket.Client.IOControl(-1744830452, new byte[] { 0 }, null); } catch (Exception) { }
            }
            catch (Exception e)
            {
                status = $"소켓을 열 수 없어요 (포트 {at}, {openTries}번째): {e.Message.Trim()}";
                socket = null;
                retryAt = Time.realtimeSinceStartup + 1f;   // a port still held a moment by a closing copy: try again (NetTick)
                Debug.LogWarning("[LabNet] loopback: " + status);
                return;
            }
            if (host)
            {
                self = HostId;
                seats[HostId] = new LabSeat { Team = 0, Slot = 0 };
                status = $"방장: 포트 {at}에서 참가자를 기다려요";
            }
            else
            {
                hostEnd = new IPEndPoint(ResolveAddress(to), at);
                status = $"참가자: {to}:{at}에 접속 중";
            }
            hudOpen = true;
            Log("loopback " + status);
        }

        static IPAddress ResolveAddress(string text)
        {
            if (IPAddress.TryParse(text, out var ip)) return ip;
            try
            {
                foreach (var a in Dns.GetHostAddresses(text))
                    if (a.AddressFamily == AddressFamily.InterNetwork) return a;
            }
            catch (Exception) { }
            return IPAddress.Loopback;
        }

        protected override void OnDestroy()
        {
            CloseSocket();
            base.OnDestroy();
        }

        protected override void OnShutdown() => CloseSocket();

        void CloseSocket()
        {
            if (socket == null) return;
            try
            {
                if (hosting) foreach (var end in peers.Values) SendControl(end, Bye, 0);
                else if (welcomed) SendControl(hostEnd, Bye, self);
            }
            catch (Exception) { }
            socket.Close();
            socket = null;
            openTries = 99;   // closed on purpose: no reopening
        }

        protected override void NetTick()
        {
            float now = Time.realtimeSinceStartup;
            if (socket == null)
            {
                if (openTries < 20 && now >= retryAt) Open(hosting, address, port);
                return;
            }
            if (hosting)
            {
                // A client silent for a while has gone (closed without a word).
                stale.Clear();
                foreach (var pair in heardAt) if (now - pair.Value > ClientTimeout) stale.Add(pair.Key);
                foreach (ulong id in stale) Drop(id, "응답 없음");
                if (now >= rosterAt && peers.Count > 0)
                {
                    rosterAt = now + 1f;
                    foreach (var end in peers.Values) SendRoster(end);
                }
            }
            else if (!welcomed && now >= helloAt)
            {
                helloAt = now + 0.5f;
                SendControl(hostEnd, Hello, 0);
            }
        }

        protected override void NetLeave(string why)
        {
            // A client whose host went quiet: start over (it says hello again until a host answers).
            if (hosting) return;
            welcomed = false;
            seats.Clear();
            status = "방장 응답이 없어 다시 접속 중";
        }

        void Drop(ulong id, string why)
        {
            if (peers.TryGetValue(id, out var end)) peerIds.Remove(end);
            peers.Remove(id);
            heardAt.Remove(id);
            seats.Remove(id);
            message = $"{NetName(id)} 나감 ({why})";
            foreach (var other in peers.Values) SendRoster(other);
        }

        // ---------------------------------------------------------------- transport

        protected override void NetSend(ulong id, byte[] bytes)
        {
            if (socket == null) return;
            IPEndPoint end = hosting ? (peers.TryGetValue(id, out var peer) ? peer : null) : (id == HostId ? hostEnd : null);
            if (end == null) return;
            try { socket.Send(bytes, bytes.Length, end); }
            catch (Exception e) { message = "보내기 오류: " + e.Message; }
        }

        protected override void NetReceive(Action<ulong, byte[]> deliver)
        {
            if (socket == null) return;
            for (int n = 0; n < 512; n++)
            {
                byte[] bytes;
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    if (socket.Available <= 0) break;
                    bytes = socket.Receive(ref from);
                }
                catch (SocketException)
                {
                    break;
                }
                if (bytes == null || bytes.Length < 4) continue;
                if (BitConverter.ToUInt32(bytes, 0) == ControlMagic)
                {
                    Control(from, bytes);
                    continue;
                }
                if (hosting)
                {
                    if (!peerIds.TryGetValue(from, out ulong id)) continue;
                    heardAt[id] = Time.realtimeSinceStartup;
                    deliver(id, bytes);
                }
                else if (from.Equals(hostEnd)) deliver(HostId, bytes);
            }
        }

        void Control(IPEndPoint from, byte[] bytes)
        {
            if (bytes.Length < 5 + 8) return;
            byte type = bytes[4];
            ulong value = BitConverter.ToUInt64(bytes, 5);
            if (hosting)
            {
                if (type == Hello)
                {
                    if (!peerIds.TryGetValue(from, out ulong id))
                    {
                        if (peers.Count >= RagdollNetProtocol.MaxPawns - 1) return;
                        id = nextClient++;
                        // Alternate sides: the first client plays the other team, the next one the host's, and so on.
                        int index = (int)(id - HostId);
                        int team = index % 2, slot = index / 2;
                        peers[id] = from;
                        peerIds[from] = id;
                        seats[id] = new LabSeat { Team = team, Slot = slot };
                        message = $"{NetName(id)} 접속 ({from})";
                        foreach (var end in peers.Values) SendRoster(end);
                    }
                    heardAt[id] = Time.realtimeSinceStartup;
                    SendControl(from, Welcome, id);
                    SendRoster(from);
                }
                else if (type == Bye && peerIds.TryGetValue(from, out ulong gone)) Drop(gone, "종료");
                return;
            }
            if (!from.Equals(hostEnd)) return;
            switch (type)
            {
                case Welcome:
                    if (!welcomed)
                    {
                        welcomed = true;
                        self = value;
                        status = $"참가자 {self - HostId}: {address}:{port}의 방장에 접속";
                        message = status;
                    }
                    break;
                case Roster:
                    ReadRoster(bytes);
                    break;
                case Bye:
                    welcomed = false;
                    seats.Clear();
                    status = "방장이 끝냄 — 다시 접속 중";
                    break;
            }
        }

        void SendControl(IPEndPoint to, byte type, ulong value)
        {
            if (socket == null || to == null) return;
            var bytes = new byte[5 + 8];
            BitConverter.GetBytes(ControlMagic).CopyTo(bytes, 0);
            bytes[4] = type;
            BitConverter.GetBytes(value).CopyTo(bytes, 5);
            try { socket.Send(bytes, bytes.Length, to); } catch (Exception) { }
        }

        void SendRoster(IPEndPoint to)
        {
            if (socket == null || to == null) return;
            var bytes = new byte[5 + 8 + seats.Count * 10];
            BitConverter.GetBytes(ControlMagic).CopyTo(bytes, 0);
            bytes[4] = Roster;
            BitConverter.GetBytes((ulong)seats.Count).CopyTo(bytes, 5);
            int at = 13;
            foreach (var pair in seats)
            {
                BitConverter.GetBytes(pair.Key).CopyTo(bytes, at);
                bytes[at + 8] = (byte)pair.Value.Team;
                bytes[at + 9] = (byte)pair.Value.Slot;
                at += 10;
            }
            try { socket.Send(bytes, bytes.Length, to); } catch (Exception) { }
        }

        void ReadRoster(byte[] bytes)
        {
            ulong count = BitConverter.ToUInt64(bytes, 5);
            if (count > (ulong)RagdollNetProtocol.MaxPawns || bytes.Length != 13 + (int)count * 10) return;
            seats.Clear();
            for (int i = 0; i < (int)count; i++)
            {
                int at = 13 + i * 10;
                seats[BitConverter.ToUInt64(bytes, at)] = new LabSeat { Team = bytes[at + 8], Slot = bytes[at + 9] };
            }
        }

        // ---------------------------------------------------------------- scripted runs (development)

        /// <summary>
        /// A timed run on the host, one command a line (the AI's way to try the skills online on one PC):
        /// <c>wait 0.3</c>; <c>host skill</c> / <c>client skill</c> (any development command, see RunDev: piece, skill,
        /// click, jump, rclick, move, look, lag, shot, trace) for this PC's own player or sent to the clients;
        /// <c>place host|client x z faceX faceZ</c> (the host moves a pawn); <c>log text</c>. Returns at once; the run
        /// goes on in the background (<see cref="ScriptRunning"/>).
        /// </summary>
        public static string Script(string text)
        {
            var link = Current as LoopbackLabLink;
            if (link == null || !link.hosting) return "no loopback host";
            link.StopAllCoroutines();
            link.StartCoroutine(link.RunScript(text.Replace("\r", "").Split('\n')));
            return "script started";
        }

        public static bool ScriptRunning { get; private set; }

        System.Collections.IEnumerator RunScript(string[] lines)
        {
            ScriptRunning = true;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] p = line.Split(' ');
                switch (p[0])
                {
                    case "wait":
                        float seconds = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                        float until = Time.realtimeSinceStartup + seconds;
                        while (Time.realtimeSinceStartup < until) yield return null;
                        break;
                    case "host":
                        RunDev(line.Substring(5));
                        break;
                    case "client":
                        SendDev(line.Substring(7));
                        break;
                    case "place":
                        PlaceForScript(p);
                        break;
                    case "log":
                        Log("script: " + line.Substring(4));
                        break;
                }
            }
            ScriptRunning = false;
        }

        void PlaceForScript(string[] p)
        {
            if (p.Length < 6) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var pawn = PawnFor(p[1] == "host" ? HostId : HostId + 1);
            if (pawn == null) return;
            var at = new Vector3(float.Parse(p[2], inv), 0f, float.Parse(p[3], inv));
            var face = new Vector3(float.Parse(p[4], inv), 0f, float.Parse(p[5], inv));
            pawn.Teleport(at + Vector3.up * (pawn.standHeight + 0.02f), face);
        }

        // ---------------------------------------------------------------- HUD

        protected override void DrawLobby()
        {
            GUILayout.Label(status, Small);
            GUILayout.Label(hosting
                ? "참가자 창(빌드)을 -labLoopback client 로 켜거나, 다른 창의 온라인 창(F3)에서 \"로컬 시험: 참가\"를 누르세요."
                : "방장 창(에디터 또는 빌드)이 -labLoopback host 로 켜져 있어야 해요.", Small);
            if (GUILayout.Button("로컬 시험 끄기 (Steam은 씬을 다시 열면 돌아와요)", Button)) Stop();
        }

        protected override void DrawMatchExtras()
        {
            GUILayout.Label(status + (hosting ? $" · 참가자 {peers.Count}명" : ""), Small);
            if (GUILayout.Button("로컬 시험 끄기", Button)) Stop();
        }

        // ---------------------------------------------------------------- command line

        static string Arg(string name, int nth = 1)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - nth; i++)
                if (args[i] == name)
                {
                    string value = args[i + nth];
                    return value.StartsWith("-") ? (nth == 1 ? "" : null) : value;
                }
            return null;
        }

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
    }
}
