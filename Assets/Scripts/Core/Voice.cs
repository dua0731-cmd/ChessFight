using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChessFight.Network
{
    // Voice chat (Docs/Architecture/UI.md "음성 채팅", R131): two channels as in Overwatch. The party
    // channel is everyone in my party, in the lobby and in a match; the team channel is everyone on
    // my team in the match room. Nobody ever hears the other team, and everyone is heard at the same
    // loudness wherever they stand.
    public enum VoiceChannel : byte { Party = 0, Team = 1 }

    // One voice packet: "CFV1", the channel it was spoken on, the speaker's microphone gain in percent
    // (the receiver applies it: Steam has no gain of its own), a sequence number, then Steam's
    // compressed voice as GetVoice returned it.
    public static class VoiceWire
    {
        public const uint Magic = 0x43465631;   // "CFV1"
        public const int HeaderBytes = 4 + 1 + 1 + 2;
        // Steam's voice packets are a few hundred bytes; anything far bigger is not ours.
        public const int MaxPayload = 8 * 1024;
        // The SteamNetworkingMessages channel: 31 capsule motion, 32 lab, 33 sword fight, 34 Pawn Rush.
        public const int SteamChannel = 40;
        public const int MaxGain = 200;

        public static byte[] Encode(VoiceChannel channel, int gainPercent, ushort sequence, byte[] payload, int length)
        {
            length = Math.Max(0, Math.Min(length, Math.Min(MaxPayload, payload?.Length ?? 0)));
            var b = new byte[HeaderBytes + length];
            unchecked { b[0] = (byte)Magic; b[1] = (byte)(Magic >> 8); b[2] = (byte)(Magic >> 16); b[3] = (byte)(Magic >> 24); }
            b[4] = (byte)channel;
            b[5] = (byte)Math.Max(0, Math.Min(MaxGain, gainPercent));
            b[6] = (byte)sequence; b[7] = (byte)(sequence >> 8);
            if (length > 0) Array.Copy(payload, 0, b, HeaderBytes, length);
            return b;
        }

        // False for anything that is not a voice packet: another magic, an unknown channel, no voice.
        public static bool TryDecode(byte[] b, int length, out VoiceChannel channel, out int gainPercent, out ushort sequence,
                                     out int payloadOffset, out int payloadLength)
        {
            channel = VoiceChannel.Party; gainPercent = 100; sequence = 0; payloadOffset = HeaderBytes; payloadLength = 0;
            if (b == null) return false;
            length = Math.Min(length, b.Length);
            if (length <= HeaderBytes || length > HeaderBytes + MaxPayload) return false;
            uint magic = b[0] | (uint)b[1] << 8 | (uint)b[2] << 16 | (uint)b[3] << 24;
            if (magic != Magic || b[4] > (byte)VoiceChannel.Team) return false;
            channel = (VoiceChannel)b[4];
            gainPercent = Math.Min(MaxGain, (int)b[5]);
            sequence = (ushort)(b[6] | b[7] << 8);
            payloadLength = length - HeaderBytes;
            return true;
        }

        // Unreliable packets can come out of order; an older one is dropped (wrapping at 65536).
        public static bool Newer(ushort next, ushort last) => (short)(next - last) > 0;
    }

    public static class VoiceRules
    {
        public static string Label(VoiceChannel channel) => channel == VoiceChannel.Party ? "파티" : "팀";

        // Where my voice goes when nobody picked: the party while someone else is in it, otherwise the
        // whole team (the user's rule, 10-13: "초기값은 팀 전체, 파티가 있으면 자동으로 파티").
        public static VoiceChannel Default(bool partyHasOthers) => partyHasOthers ? VoiceChannel.Party : VoiceChannel.Team;

        // The channel my voice really goes to: the pick (else the default) when it can be used, the
        // other one when only that can, nothing when neither can.
        public static VoiceChannel? Resolve(VoiceChannel? picked, bool partyUsable, bool teamUsable, bool partyHasOthers)
        {
            var want = picked ?? Default(partyHasOthers);
            if (Usable(want, partyUsable, teamUsable)) return want;
            var other = want == VoiceChannel.Party ? VoiceChannel.Team : VoiceChannel.Party;
            if (Usable(other, partyUsable, teamUsable)) return other;
            return null;
        }

        static bool Usable(VoiceChannel c, bool party, bool team) => c == VoiceChannel.Party ? party : team;

        // Whether a packet that arrived is played: party voice from someone in my party while I am in
        // the party channel, team voice from a teammate in my match while I am in the team channel.
        public static bool Hear(VoiceChannel channel, bool senderInParty, bool senderTeammate, bool listenParty, bool listenTeam) =>
            channel == VoiceChannel.Party ? senderInParty && listenParty : senderTeammate && listenTeam;
    }

    // The meters and the voice-activated mode read loudness on one scale: 0 at -60 dBFS and below,
    // 1 at full scale, so ordinary speech sits around the middle.
    public static class VoiceLevel
    {
        public const double FloorDb = -60;

        public static float FromRms(double rms)
        {
            if (!(rms > 0)) return 0f;
            double db = 20 * Math.Log10(rms);
            return (float)Math.Max(0, Math.Min(1, (db - FloorDb) / -FloorDb));
        }

        // Root mean square of little-endian 16-bit PCM, 0..1.
        public static double Rms(byte[] pcm16, int bytes)
        {
            if (pcm16 == null) return 0;
            int n = Math.Min(bytes, pcm16.Length) / 2;
            if (n <= 0) return 0;
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                double s = (short)(pcm16[2 * i] | pcm16[2 * i + 1] << 8) / 32768.0;
                sum += s * s;
            }
            return Math.Sqrt(sum / n);
        }
    }

    // The voice-activated mode (자동으로 켜지기): open when the level reaches the threshold, and held
    // open a moment after it drops, so the ends of words are not clipped.
    public sealed class VoiceGate
    {
        public const double Hold = .35;
        double openUntil = double.NegativeInfinity;

        public bool Open { get; private set; }

        public bool Update(double now, float level, float threshold)
        {
            if (level >= threshold) openUntil = now + Hold;
            Open = now < openUntil;
            return Open;
        }

        public void Reset()
        {
            openUntil = double.NegativeInfinity;
            Open = false;
        }
    }

    // The people I muted, by Steam ID. Kept on this PC only (nobody is told), as "id,id,...".
    public sealed class VoiceMuteList
    {
        public const int Capacity = 500;
        readonly HashSet<ulong> ids = new HashSet<ulong>();

        public int Count => ids.Count;
        public bool Contains(ulong id) => ids.Contains(id);

        // True when it changed.
        public bool Set(ulong id, bool muted)
        {
            if (id == 0) return false;
            if (!muted) return ids.Remove(id);
            if (ids.Count >= Capacity && !ids.Contains(id)) return false;
            return ids.Add(id);
        }

        public string Format() => string.Join(",", ids.OrderBy(x => x).Select(x => x.ToString(CultureInfo.InvariantCulture)));

        public static VoiceMuteList Parse(string text)
        {
            var list = new VoiceMuteList();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var part in text.Split(','))
                if (ulong.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong id)) list.Set(id, true);
            return list;
        }
    }

    // One speaker's sound between the network and the audio thread: 16-bit PCM in on the main thread,
    // floats out on the audio thread at the output's rate (linear interpolation when the two differ).
    // Playback starts once Prime seconds are waiting, so a late packet does not crackle, and the oldest
    // sound is dropped past MaxLatency, so a stall never leaves someone talking seconds behind.
    public sealed class VoiceStream
    {
        readonly object gate = new object();
        readonly float[] ring;
        readonly int prime, cap;
        int read, count;
        bool playing;
        double fraction;

        public int SampleRate { get; }
        public double LastWrite { get; private set; } = double.NegativeInfinity;

        public VoiceStream(int sampleRate, double primeSeconds = .06, double maxSeconds = .4)
        {
            SampleRate = Math.Max(8000, sampleRate);
            ring = new float[SampleRate];   // one second
            prime = Math.Max(2, (int)(SampleRate * primeSeconds));
            cap = Math.Max(prime + 2, Math.Min(ring.Length, (int)(SampleRate * maxSeconds)));
        }

        public int Buffered { get { lock (gate) return count; } }
        public bool Playing { get { lock (gate) return playing; } }

        public void Write(byte[] pcm16, int bytes, float gain, double now)
        {
            if (pcm16 == null) return;
            int n = Math.Min(bytes, pcm16.Length) / 2;
            if (n <= 0) return;
            if (float.IsNaN(gain) || gain < 0) gain = 0;
            lock (gate)
            {
                for (int i = 0; i < n; i++)
                {
                    float s = (short)(pcm16[2 * i] | pcm16[2 * i + 1] << 8) / 32768f * gain;
                    if (s > 1f) s = 1f; else if (s < -1f) s = -1f;
                    if (count >= cap) { read = (read + 1) % ring.Length; count--; }
                    ring[(read + count) % ring.Length] = s;
                    count++;
                }
                LastWrite = now;
            }
        }

        // Fills `frames` frames of `channels` interleaved samples; `step` is the stream's rate over the
        // output's. Returns how many frames carried sound (the rest are silence).
        public int Read(float[] data, int frames, int channels, double step, float volume)
        {
            if (data == null || channels <= 0) return 0;
            frames = Math.Min(frames, data.Length / channels);
            if (!(step > 0)) step = 1;
            int sounded = 0;
            lock (gate)
            {
                for (int f = 0; f < frames; f++)
                {
                    if (!playing && count >= prime) playing = true;
                    float s = 0f;
                    if (playing)
                    {
                        if (count < 2) { playing = false; fraction = 0; }
                        else
                        {
                            float a = ring[read], b = ring[(read + 1) % ring.Length];
                            s = (float)(a + (b - a) * fraction) * volume;
                            fraction += step;
                            while (fraction >= 1 && count >= 2) { fraction -= 1; read = (read + 1) % ring.Length; count--; }
                            sounded++;
                        }
                    }
                    for (int c = 0; c < channels; c++) data[f * channels + c] = s;
                }
            }
            return sounded;
        }

        public void Clear()
        {
            lock (gate) { read = count = 0; playing = false; fraction = 0; }
        }
    }
}
