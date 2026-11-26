namespace ChessFight.Network
{
    // R703 (the user 2026-11-17: "게임 흐름이 의도되지 않아서 5개의 방법을 방장 권한으로 켜고 끌 수 있게 만들어 적용시켜보고
    // 제일 나은 것으로 채택하려고 해" - a team that clears a mini-game first runs away with the round, and a player left
    // behind has no way back): the rules of a round's flow, as the manager of a room a player made picks them. Three
    // settings, each with the way the game has played so far as its first choice:
    //
    //   Relief   the obstacle section before a mini-game gets easier - an easy way opens and what strikes pawns
    //            switches off - once one team has cleared that mini-game (the user's option 1), or once both have
    //            (option 2).
    //   After    what a team that has cleared its mini-game meets: its gate open (as before), a pad at the gate that
    //            most of the team must stand on together (option 3), or its gate open and the other team's gate
    //            opening GateDelay seconds later whether that team has cleared or not (option 4).
    //   Ending   the crown platform a team's majority must stand on at once (as before), or the hold-the-throne game
    //            everyone is taken to when the war room's great gate opens (option 5).
    //   EndingMap (JY-audience-hall, 11-21: "아예 이 구간을 여러개를 만들어보고 직접 플레이하며 비교해보려고 해") which
    //            audience hall the round ends in: drawn from the seed like any slot (as before), or one of the halls A
    //            to D (the last slot's prefabs in their order). Bits 8-10: bits 5-7 are the skill lab's (JY-skill-lab:
    //            Skills, Hold), so the two branches' rules can be put together.
    //   Summon   (JY-audience-hall, 11-21: "1등 주자가 엔드게임 코스를 통과하면 30초정도 뒤 텔포로 모든 유저 전원이 이동",
    //            "30초 소환 규칙 … 만들어줘") under the throne contest: everyone is stood in the hall at once (as before),
    //            or the first runner through the endgame course starts PawnRushThrone.SummonSeconds in which anyone may
    //            walk in and take a place, the platform off, and then the rest are summoned. Bit 11.
    //
    //   Skills   which skill plan the pieces play by (JY-skill-lab, 2026-11-19, the user: "프로토타입을 만들어보고, 다 직접
    //            인게임에서 해보고 판단" with the manager's switches): the skills as they are (plan 1-가), or plan 1-나 of
    //            Docs/Skills/PAWN_RUSH_2_SKILL_PLANS.md (pawn en passant, knight fork, bishop pin, rook castling, queen
    //            check), or the loadout the host's PC last made in the developers' skill tree (K). Whatever the match
    //            starts with, the skill tree changes it on the way. Prototypes: rules and physics only.
    //
    // The room carries them as one number (room data "rules"); every PC builds its round from it, and a room found by
    // random matching has none - the game as it has played so far. Core: no Unity, no Steam.
    public struct PawnRushRules
    {
        public const int ReliefOff = 0, ReliefFirst = 1, ReliefBoth = 2;
        public const int AfterStraight = 0, AfterGather = 1, AfterDelay = 2;
        public const int EndingCrown = 0, EndingThrone = 1;
        public const int MapDrawn = 0, MapLast = 4;
        public const int SummonNow = 0, SummonLater = 1;
        public const int SkillsNow = 0, SkillsChessMoves = 1, SkillsTree = 2;
        // JY-skill-lab (2026-11-21, the skill system's X-판17, a must with the team's comeback ultimates): the crown
        // platform's majority wins at once (as before), or only after holding it PawnRushFinish.HoldSeconds.
        public const int HoldOff = 0, HoldOn = 1;

        // Option 4: how long after one team's clear the other team's gate opens.
        public const double GateDelay = 20;

        public int Relief, After, Ending, EndingMap, Summon;
        // JY-skill-lab: the skill plan (bits 5-6) and the crown platform's hold (bit 7). Not on the room panel: the skill
        // test mode (GameModes.PawnRushSkillTest) plays the skill tree, every other Pawn Rush the skills as they are.
        public int Skills, Hold;

        // What the running round plays by: the match link sets it from the room before the round is built; offline it
        // stays as it is (a test may set it).
        public static PawnRushRules Current;

        public int Encode() => Clamp(Relief, 2) + Clamp(After, 2) * 4 + Clamp(Ending, 1) * 16 + Clamp(Skills, 2) * 32 + Clamp(Hold, 1) * 128
                               + Clamp(EndingMap, MapLast) * 256 + Clamp(Summon, 1) * 2048;

        public static PawnRushRules Decode(int packed)
        {
            if (packed < 0) packed = 0;
            return new PawnRushRules { Relief = Clamp(packed & 3, 2), After = Clamp((packed >> 2) & 3, 2), Ending = Clamp((packed >> 4) & 1, 1),
                                       EndingMap = Clamp((packed >> 8) & 7, MapLast), Summon = Clamp((packed >> 11) & 1, 1),
                                       Skills = Clamp((packed >> 5) & 3, 2), Hold = Clamp((packed >> 7) & 1, 1) };
        }

        static int Clamp(int value, int max) => value < 0 ? 0 : value > max ? 0 : value;

        // A choice not built yet: the room's panel shows it and does not let it be picked. (R704: the pad at the gate
        // is built; R707: the throne contest is - every choice can be picked now.)
        public static bool Ready(int setting, int choice) => true;

        // The room's panel: the settings' names and their choices' short names.
        public static readonly string[] Settings = { "낙오자 구제", "미니게임을 깬 뒤", "엔딩", "엔딩 맵", "엔딩 시작" };
        public static readonly string[][] Choices =
        {
            new[] { "끔", "한 팀이 깨면", "두 팀이 깨면" },
            new[] { "바로 통과", "모여서 발판", "상대 문 20초 뒤" },
            new[] { "왕관 발판", "왕좌 차지전" },
            new[] { "무작위", "A 지금 초안", "B 대성당", "C 원형 돔", "D 거인의 왕좌" },
            new[] { "바로 모두 소환", "30초 뒤 소환" },
        };

        public int Of(int setting) => setting == 0 ? Relief : setting == 1 ? After : setting == 2 ? Ending : setting == 3 ? EndingMap : Summon;

        public PawnRushRules With(int setting, int choice)
        {
            var r = this;
            if (setting == 0) r.Relief = choice; else if (setting == 1) r.After = choice; else if (setting == 2) r.Ending = choice;
            else if (setting == 3) r.EndingMap = choice; else r.Summon = choice;
            return Decode(r.Encode());
        }

        // One line for the log and the match's first banner.
        public override string ToString() =>
            Settings[0] + " " + Choices[0][Relief] + " · " + Settings[1] + " " + Choices[1][After] + " · " + Settings[2] + " " + Choices[2][Ending]
            + " · " + Settings[3] + " " + Choices[3][EndingMap] + " · " + Settings[4] + " " + Choices[4][Summon];
    }

    // R707 (option 5, PawnRushRules.EndingThrone - the user 2026-11-17: "과반수 이상이 발판 위에 일정 시간 올라와 있어야
    // 승리 ... 원래 총 30초인데 미니게임 클리어 팀은 5초씩 차감"): the hold-the-throne contest's sums. While most of a
    // team's players stand on the crown platform its time runs; with both teams' majorities on it at once nobody's
    // does; time is kept when a team steps off; the first to fill its time wins. A team's time is Seconds less
    // PerClear for each mini-game it cleared by playing it. Core: no Unity.
    public static class PawnRushThrone
    {
        public const float Seconds = 30f, PerClear = 5f, Least = 10f;
        // PawnRushRules.SummonLater: from the first runner through the endgame course to the summons.
        public const float SummonSeconds = 30f;
        // Who holds the platform: 0 white, 1 black, or one of these.
        public const int Nobody = -1, Both = 2;

        public static float Target(int clears) => System.Math.Max(Least, Seconds - PerClear * System.Math.Max(0, clears));

        public static int Holder(int whiteOn, int whitePlayers, int blackOn, int blackPlayers)
        {
            bool white = PawnRushFinish.Reached(whiteOn, whitePlayers), black = PawnRushFinish.Reached(blackOn, blackPlayers);
            return white && black ? Both : white ? 0 : black ? 1 : Nobody;
        }

        // One step: the holder's time runs. The winner (0, 1), or Nobody.
        public static int Step(ref float white, ref float black, int holder, float dt, float whiteTarget, float blackTarget)
        {
            if (dt < 0f) dt = 0f;
            if (holder == 0) white = System.Math.Min(whiteTarget, white + dt);
            else if (holder == 1) black = System.Math.Min(blackTarget, black + dt);
            return holder == 0 && white >= whiteTarget ? 0 : holder == 1 && black >= blackTarget ? 1 : Nobody;
        }
    }
}
