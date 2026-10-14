using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // What the voice chat shows (Docs/Architecture/UI.md "음성 채팅", R131), Steam-agnostic like the rest of
    // this assembly: the runtime fills it from SteamVoice every frame; the lobby's nameplates, the match's
    // talker list and the match sheet read it. The mute list lives here, on this PC only: nobody is told.
    public static class VoiceBoard
    {
        public struct Talker
        {
            public ulong Id;
            public VoiceChannel Channel;
        }

        // Steam is up and the player has voice chat on.
        public static bool Running { get; set; }
        public static ulong Self { get; set; }
        // Where my voice goes now (null: nowhere), which channels could take it, and how many others each has.
        public static VoiceChannel? Target { get; set; }
        public static bool PartyUsable { get; set; }
        public static bool TeamUsable { get; set; }
        public static int PartyOthers { get; set; }
        public static int TeamOthers { get; set; }
        // The match sheet's choice of channel (null: the default); the runtime hands it to the voice.
        public static VoiceChannel? Picked { get; set; }
        // Why the microphone gives nothing, for the settings page ("" when all is well).
        public static string Problem { get; set; } = "";

        // Who is talking this moment; the runtime rewrites it every frame.
        public static readonly List<Talker> Talking = new List<Talker>();
        static readonly Dictionary<ulong, VoiceChannel> talking = new Dictionary<ulong, VoiceChannel>();

        public static void SetTalking(List<(ulong id, VoiceChannel channel)> now)
        {
            Talking.Clear();
            talking.Clear();
            if (now == null) return;
            foreach (var (id, channel) in now)
            {
                if (talking.ContainsKey(id)) continue;
                talking[id] = channel;
                Talking.Add(new Talker { Id = id, Channel = channel });
            }
        }

        public static bool IsTalking(ulong id, out VoiceChannel channel) => talking.TryGetValue(id, out channel);

        // ---------- mutes (PlayerPrefs "cf.vc.mutes", Steam IDs) ----------

        const string MuteKey = "cf.vc.mutes";
        static VoiceMuteList mutes;

        // Bumped on every change, for views that redraw only then.
        public static int MuteVersion { get; private set; }

        public static bool IsMuted(ulong id)
        {
            Load();
            return mutes.Contains(id);
        }

        public static void SetMuted(ulong id, bool muted)
        {
            Load();
            if (!mutes.Set(id, muted)) return;
            PlayerPrefs.SetString(MuteKey, mutes.Format());
            PlayerPrefs.Save();
            MuteVersion++;
        }

        public static void ToggleMuted(ulong id) => SetMuted(id, !IsMuted(id));

        static void Load()
        {
            if (mutes == null) mutes = VoiceMuteList.Parse(PlayerPrefs.GetString(MuteKey, ""));
        }

        // The channel colours: party green, team blue (the user's pick, 10-13).
        public static readonly Color PartyColor = new Color(111 / 255f, 203 / 255f, 122 / 255f);
        public static readonly Color TeamColor = new Color(125 / 255f, 180 / 255f, 224 / 255f);
        public static readonly Color MutedColor = new Color(200 / 255f, 69 / 255f, 59 / 255f);

        public static Color ColorOf(VoiceChannel channel) => channel == VoiceChannel.Party ? PartyColor : TeamColor;
    }

    // Who has been promoted in the running match, and to what (R131, the match sheet's piece icons). The
    // match's own link writes it (Pawn Rush's promotion pads, SteamPawnRushLink), the sheet reads it.
    // A pawn is not listed: the sheet shows no icon until a promotion really happens.
    public static class MatchPieces
    {
        static readonly Dictionary<ulong, PieceKind> pieces = new Dictionary<ulong, PieceKind>();

        public static void Set(ulong id, PieceKind kind)
        {
            if (kind == PieceKind.Pawn) pieces.Remove(id);
            else pieces[id] = kind;
        }

        public static bool Promoted(ulong id, out PieceKind kind) => pieces.TryGetValue(id, out kind);

        public static void Clear() => pieces.Clear();
    }
}
