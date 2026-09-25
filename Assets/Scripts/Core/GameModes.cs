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
        // One line under the name on the mode card, e.g. "6 vs 6 · 장애물 레이스".
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
        public static readonly GameModeInfo KingRush = new GameModeInfo(
            "kingrush", "킹 러시", "6 vs 6 · 레이스와 팀 배틀",
            "장애물 달리기와 팀 배틀 구간이 세 번 번갈아 나옵니다.", "KR", "KingRush");

        public static readonly GameModeInfo QueenOfTheHill = new GameModeInfo(
            "queenhill", "퀸 오브 더 힐", "6 vs 6 · 정상 쟁탈",
            "가운데 성을 올라 정상에 먼저 닿은 폰이 퀸으로 승격합니다.", "QH", null);

        public static readonly GameModeInfo SwordFight = new GameModeInfo(
            "swordfight", "소드 파이트", "6 vs 6 · 팀 데스매치",
            "모든 기물이 물리 칼을 들고 싸우는 팀 데스매치입니다.", "SF", null);

        public static readonly GameModeInfo[] All = { KingRush, QueenOfTheHill, SwordFight };

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
