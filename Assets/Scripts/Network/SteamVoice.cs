using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

namespace ChessFight.Network
{
    // Voice chat over Steam (Docs/Architecture/UI.md "음성 채팅", R131). ISteamUser records the microphone
    // and compresses it, SteamNetworkingMessages carries each piece to everyone in the channel my voice
    // goes to (unreliable, VoiceWire.SteamChannel), and ISteamUser decompresses what arrives; the game
    // plays it (VoicePlayback) and draws who is talking (VoiceBoard).
    //
    // Two channels (Core/Voice.cs): the party everywhere, the team in a match room. My voice goes to the
    // match sheet's pick, else to the party while someone else is in it, else to the team. Sound that
    // arrives is played only from someone really in that channel with me, so the other team is never
    // heard, whatever a packet claims. Muted people are dropped before decoding and never show as talking.
    public sealed class SteamVoice : IDisposable
    {
        // How long after the last piece of sound someone still shows as talking, in seconds.
        public const float TalkingFor = .3f;
        // Push to talk: Steam keeps recording a moment after the key is let go; that tail still goes out.
        const float ReleaseTail = .4f;
        // Someone silent this long starts a fresh sequence (they may have restarted the game).
        const float SequenceReset = 2f;

        readonly SteamSession session;
        readonly Callback<SteamNetworkingMessagesSessionRequest_t> requests;
        readonly IntPtr[] incoming = new IntPtr[32];
        readonly byte[] captured = new byte[VoiceWire.MaxPayload];
        readonly byte[] arrived = new byte[VoiceWire.HeaderBytes + VoiceWire.MaxPayload];
        readonly byte[] compressed = new byte[VoiceWire.MaxPayload];
        byte[] pcm = new byte[48000];
        readonly VoiceGate gate = new VoiceGate();
        readonly Dictionary<ulong, ushort> sequences = new Dictionary<ulong, ushort>();
        readonly Dictionary<ulong, float> heardAt = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, VoiceChannel> heardOn = new Dictionary<ulong, VoiceChannel>();
        readonly HashSet<ulong> party = new HashSet<ulong>();
        readonly List<ulong> recipients = new List<ulong>();
        readonly List<ulong> stale = new List<ulong>();
        bool recording, held, disposed;
        ushort sequence;
        float sentAt = -10f, releasedAt = -10f, level, nextPrune;
        VoiceChannel sentOn = VoiceChannel.Team;

        // ---------- what the runtime sets every frame ----------
        public bool Enabled = true;                           // 설정 → 음성 채팅
        public bool ListenParty = true, ListenTeam = true;    // 파티·팀 음성 자동 참여
        public bool PushToTalk = true;                        // 누르고 말하기; false: 자동으로 켜지기
        public bool TalkHeld;                                 // the talk key is down
        public float Threshold = .3f;                         // 켜지는 크기 (VoiceLevel scale)
        public int GainPercent = 100;                         // 마이크 크기
        public VoiceChannel? Picked;                          // the match sheet's pick; null: the default
        public bool Testing;                                  // the settings page is open: record and meter
        public bool Echo;                                     // ... and play my own voice back
        public int SampleRate = 48000;                        // what the playback wants (11025-48000)
        public Func<ulong, bool> Muted;
        // Decoded sound for the speakers: who (Self for the echo), 16-bit PCM, its length in bytes, gain.
        public Action<ulong, byte[], int, float> Sink;

        // ---------- what it says back ----------
        public VoiceChannel? Target { get; private set; }
        public bool PartyUsable { get; private set; }
        public bool TeamUsable { get; private set; }
        public int PartyOthers { get; private set; }
        public int TeamOthers { get; private set; }
        public float MicLevel => level;
        public bool Recording => recording;
        public bool Sending => Time.realtimeSinceStartup - sentAt < TalkingFor;
        // Why the microphone gives nothing, for the settings page; "" when all is well.
        public string Problem { get; private set; } = "";

        public SteamVoice(SteamSession session)
        {
            this.session = session;
            requests = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(c =>
            {
                if (disposed || !Enabled) return;
                ulong id = c.m_identityRemote.GetSteamID64();
                // Party voice comes from outside any match, where the movement layer accepts nobody.
                if (session.IsPeer(id) || Array.IndexOf(session.PartyMembers, id) >= 0)
                    SteamNetworkingMessages.AcceptSessionWithUser(ref c.m_identityRemote);
            });
        }

        int MyTeam => session.Match != 0 && session.Roster.TryGetValue(session.Self, out var me) ? me.Team : -1;

        public void Tick()
        {
            if (disposed || !session.Online) return;
            float now = Time.realtimeSinceStartup;
            session.VoiceOn = Enabled;
            Refresh();
            if (held && !TalkHeld) releasedAt = now;
            held = TalkHeld;
            Capture(now);
            Receive(now);
            if (now >= nextPrune) Prune(now);
        }

        // Who is in my channels right now, and where my voice goes.
        void Refresh()
        {
            party.Clear();
            foreach (ulong id in session.PartyMembers)
                if (id != session.Self && !BotIdentity.IsBot(id)) party.Add(id);
            PartyOthers = party.Count;
            int team = MyTeam;
            TeamOthers = 0;
            if (team >= 0)
                foreach (var p in session.Roster.Values)
                    if (p.Id != session.Self && p.Team == team && !BotIdentity.IsBot(p.Id)) TeamOthers++;
            PartyUsable = Enabled && ListenParty && PartyOthers > 0;
            TeamUsable = Enabled && ListenTeam && team >= 0;
            Target = Enabled ? VoiceRules.Resolve(Picked, PartyUsable, TeamUsable, PartyOthers > 0) : null;
        }

        // ---------- my microphone ----------

        void Capture(float now)
        {
            bool want = Enabled && (Testing || (Target != null && (!PushToTalk || TalkHeld)));
            if (want && !recording)
            {
                SteamUser.StartVoiceRecording();
                recording = true;
                gate.Reset();
            }
            else if (!want && recording)
            {
                SteamUser.StopVoiceRecording();
                recording = false;
            }
            bool got = false;
            // Read even after stopping: the tail Steam keeps comes out here, and reading empties it.
            for (int round = 0; round < 8; round++)
            {
                var available = SteamUser.GetAvailableVoice(out uint size);
                if (available == EVoiceResult.k_EVoiceResultRestricted) { Problem = "Steam이 이 계정의 음성을 막았어요."; break; }
                if (available != EVoiceResult.k_EVoiceResultOK || size == 0) break;
                var result = SteamUser.GetVoice(true, captured, (uint)captured.Length, out uint written);
                if (result != EVoiceResult.k_EVoiceResultOK || written == 0 || written > captured.Length) break;
                got = true;
                Problem = "";
                Spoken(captured, (int)written, now);
            }
            if (!got) level = Mathf.MoveTowards(level, 0f, Time.unscaledDeltaTime * 1.5f);
        }

        // One piece of my voice: measured (the meter and the voice-activated mode), sent if it should be,
        // played back to me on the settings page if asked.
        void Spoken(byte[] voice, int length, float now)
        {
            float gain = GainPercent / 100f;
            int bytes = Decode(voice, length);
            float piece = VoiceLevel.FromRms(VoiceLevel.Rms(pcm, bytes) * gain);
            level = piece > level ? piece : Mathf.Lerp(level, piece, .35f);
            // Push to talk: while the key is down and for Steam's tail after it. On the settings page the
            // microphone runs for the meter without the key, and that sound stays here.
            bool talking = PushToTalk ? TalkHeld || now - releasedAt < ReleaseTail : gate.Update(now, piece, Threshold);
            if (talking && Target != null) Send(voice, length, Target.Value, now);
            if (Testing && Echo && bytes > 0) Sink?.Invoke(session.Self, pcm, bytes, gain);
        }

        void Send(byte[] voice, int length, VoiceChannel channel, float now)
        {
            recipients.Clear();
            if (channel == VoiceChannel.Party) recipients.AddRange(party);
            else
            {
                int team = MyTeam;
                foreach (var p in session.Roster.Values)
                    if (p.Team == team && p.Id != session.Self && !BotIdentity.IsBot(p.Id)) recipients.Add(p.Id);
            }
            sentAt = now;
            sentOn = channel;
            if (recipients.Count == 0) return;
            var bytes = VoiceWire.Encode(channel, GainPercent, ++sequence, voice, length);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                foreach (ulong id in recipients)
                {
                    var remote = new SteamNetworkingIdentity();
                    remote.SetSteamID64(id);
                    SteamNetworkingMessages.SendMessageToUser(ref remote, handle.AddrOfPinnedObject(), (uint)bytes.Length,
                        Constants.k_nSteamNetworkingSend_Unreliable | Constants.k_nSteamNetworkingSend_NoNagle, VoiceWire.SteamChannel);
                }
            }
            finally { handle.Free(); }
        }

        // ---------- what arrives ----------

        void Receive(float now)
        {
            for (int round = 0; round < 8; round++)
            {
                int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(VoiceWire.SteamChannel, incoming, incoming.Length);
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var message = SteamNetworkingMessage_t.FromIntPtr(incoming[i]);
                        ulong sender = message.m_identityPeer.GetSteamID64();
                        int size = message.m_cbSize;
                        // Off means off: what arrives is only emptied out.
                        if (!Enabled || sender == session.Self || BotIdentity.IsBot(sender) || size <= VoiceWire.HeaderBytes || size > arrived.Length) continue;
                        Marshal.Copy(message.m_pData, arrived, 0, size);
                        if (!VoiceWire.TryDecode(arrived, size, out var channel, out int gain, out ushort seq, out int offset, out int length)) continue;
                        if (!Hears(sender, channel)) continue;
                        if (Muted != null && Muted(sender)) continue;
                        if (sequences.TryGetValue(sender, out ushort last) && heardAt.TryGetValue(sender, out float lastAt) &&
                            now - lastAt < SequenceReset && !VoiceWire.Newer(seq, last)) continue;
                        sequences[sender] = seq;
                        heardAt[sender] = now;
                        heardOn[sender] = channel;
                        Buffer.BlockCopy(arrived, offset, compressed, 0, length);
                        int bytes = Decode(compressed, length);
                        if (bytes > 0) Sink?.Invoke(sender, pcm, bytes, gain / 100f);
                    }
                    finally
                    {
                        SteamNetworkingMessage_t.Release(incoming[i]);
                        incoming[i] = IntPtr.Zero;
                    }
                }
                if (count < incoming.Length) break;
            }
        }

        bool Hears(ulong sender, VoiceChannel channel)
        {
            int team = MyTeam;
            bool mate = team >= 0 && session.Roster.TryGetValue(sender, out var p) && p.Team == team;
            return VoiceRules.Hear(channel, party.Contains(sender), mate, ListenParty, ListenTeam);
        }

        // Steam's compressed voice into `pcm` (16-bit mono at SampleRate); its length in bytes, 0 if none.
        int Decode(byte[] voice, int length)
        {
            uint rate = (uint)Mathf.Clamp(SampleRate, 11025, 48000);
            var result = SteamUser.DecompressVoice(voice, (uint)length, pcm, (uint)pcm.Length, out uint written, rate);
            if (result == EVoiceResult.k_EVoiceResultBufferTooSmall && written > pcm.Length && written < 1 << 22)
            {
                pcm = new byte[written];
                result = SteamUser.DecompressVoice(voice, (uint)length, pcm, (uint)pcm.Length, out written, rate);
            }
            return result == EVoiceResult.k_EVoiceResultOK ? (int)Math.Min(written, (uint)pcm.Length) : 0;
        }

        // ---------- who is talking ----------

        public bool IsTalking(ulong id, out VoiceChannel channel)
        {
            channel = VoiceChannel.Team;
            if (id == session.Self)
            {
                channel = sentOn;
                return Sending;
            }
            if (!heardAt.TryGetValue(id, out float at) || Time.realtimeSinceStartup - at > TalkingFor) return false;
            channel = heardOn[id];
            return true;
        }

        // Everyone heard within TalkingFor, me included while my voice goes out.
        public void Talkers(List<(ulong id, VoiceChannel channel)> into)
        {
            into.Clear();
            float now = Time.realtimeSinceStartup;
            if (Sending) into.Add((session.Self, sentOn));
            foreach (var pair in heardAt)
                if (now - pair.Value <= TalkingFor) into.Add((pair.Key, heardOn[pair.Key]));
        }

        void Prune(float now)
        {
            nextPrune = now + 10f;
            stale.Clear();
            foreach (var pair in heardAt) if (now - pair.Value > 30f) stale.Add(pair.Key);
            foreach (ulong id in stale) { heardAt.Remove(id); heardOn.Remove(id); sequences.Remove(id); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            requests?.Dispose();
            if (recording && session.Online)
            {
                try { SteamUser.StopVoiceRecording(); } catch (InvalidOperationException) { }
            }
            recording = false;
        }
    }
}
