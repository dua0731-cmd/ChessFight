using System;

namespace ChessFight.Network
{
    // One minigame the lobby can queue for. A match is exactly one mode: the party
    // leader picks it, matchmaking only pairs parties that picked the same one,
    // and the match scene is loaded from it.
    public sealed class GameModeInfo
    {
        // Written to lobby data and compared by matchmaking. Never shown and never
        // renamed: a new key is a new mode to every build that has not seen it.
        public string Key { get; }
        public string Name { get; }
        // One line under the name on the mode card, e.g. "팀 레이스".
        public string Tagline { get; }
        // A sentence for the mode picker.
        public string Summary { get; }
        // Two letters on the card's tile until the mode has artwork.
        public string Badge { get; }
        // The Unity scene a match of this mode loads. Empty while the mode is only
        // planned: the lobby shows it as "준비 중" and it cannot be picked.
        public string Scene { get; }
        public bool Playable => !string.IsNullOrEmpty(Scene);

        public GameModeInfo(string key, string name, string tagline, string summary, string badge, string scene)
        { Key = key; Name = name; Tagline = tagline; Summary = summary; Badge = badge; Scene = scene ?? ""; }
    }

    // Every mode the game knows, in the order the picker lists them. Adding a mode:
    // a new entry here with an empty scene, then its scene (and the scene name here)
    // once the scene is in the build list. See Docs/GameModes/README.md.
    public static class GameModes
    {
        // The key keeps the old name (keys never change); the mode is shown as
        // 폰 러쉬 since 2026-10-03. Its match was Pawn Rush course 01 from 2026-10-08
        // (the KingRush capsule scene is gone) and is the section round since R508
        // (PawnRush_Round: seven sections drawn by the match's number).
        public static readonly GameModeInfo KingRush = new GameModeInfo(
            "kingrush", "폰 러쉬", "팀 레이스",
            "장애물 구간과 미니게임을 지나 작전 회의실의 골에 팀 과반이 먼저 서면 승리", "PR", "PawnRush_Round");

        public static readonly GameModeInfo SwordFight = new GameModeInfo(
            "swordfight", "소드 파이트", "대난투",   // 10-27 (승규 님): was "3 VS 3 · 떨어뜨리고 빼앗기"
            "상대를 판 밖으로 떨어뜨리면 1점, 먼저 10점", "SF", "SwordFight");   // R640: the steal sentence gone

        public static readonly GameModeInfo[] All = { KingRush, SwordFight };

        // What a fresh party queues for.
        public static GameModeInfo Default => KingRush;

        // Null for a key this build does not know.
        public static GameModeInfo Find(string key)
        {
            foreach (var mode in All) if (string.Equals(mode.Key, key, StringComparison.Ordinal)) return mode;
            return null;
        }

        // Lobby data can be empty for a moment after a lobby is created; the party
        // then reads as the default mode rather than as nothing.
        public static GameModeInfo Resolve(string key) => Find(key) ?? Default;

        public static int IndexOf(string key)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Key == key) return i;
            return -1;
        }
    }
}
