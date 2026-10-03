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
        // One line under the name on the mode card, e.g. "6 VS 6 · 팀 레이스".
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
        // The key and scene keep the old name (keys never change); the mode is
        // shown as 폰 러쉬 since 2026-10-03.
        public static readonly GameModeInfo KingRush = new GameModeInfo(
            "kingrush", "폰 러쉬", "6 VS 6 · 팀 레이스",
            "장애물 코스를 달려 팀원 4명이 먼저 결승선을 넘으면 승리", "PR", "KingRush");

        public static readonly GameModeInfo QueenOfTheHill = new GameModeInfo(
            "queenhill", "퀸 오브 더 힐", "6 VS 6 · 정상 쟁탈",
            "가운데 성을 올라 정상에 먼저 닿은 폰이 퀸으로 승격", "QH", null);

        public static readonly GameModeInfo SwordFight = new GameModeInfo(
            "swordfight", "소드 파이트", "6 VS 6 · 팀 데스매치",
            "칼로 상대를 넘어뜨려 장외로 보내세요", "SF", "SwordFight");

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
