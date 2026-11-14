using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChessFight.Network
{
    // R624 (11-13, 사용자: "플레이어 찾기 후 매칭 스러운 걸 뭔가 추가 후 지금 소드파이트 기물 선택을 매칭으로 가지고 오고 싶어",
    // design A, picked from the canvas https://claude.ai/artifact/P4pMoyFbZdQrjixsfSfbNe): Sword Fight's piece pick moved out
    // of the match scene onto the entrance screen. From the moment the host starts the match (the room's "begun", on the
    // shared clock) every PC shows "매칭 완료!" with the two teams for FoundSeconds, then "기물을 선택하십시오" with the five
    // piece cards for PickSeconds while the match scene loads behind, then "우리 팀이 선택한 기물" until the start.
    //
    // Each player writes only its own pick, as Steam member data "pick" ("<piece>,<shared time>", SteamSession.PickSword),
    // so a change of host loses nothing (HANDOFF rule 15). Two teammates may take the same piece at nearly the same moment
    // before either sees the other's: the earlier pick keeps it (the lower id on a tie) and the other's card frees again,
    // the same answer on every PC. When the picks close (time up, or every player holds a piece) the players and bots
    // still without one get one of their side's pieces left, drawn from the match's seed (Final), as
    // SwordMatchRules.PickRest does in the scene. The scene's host puts that draw into the rules at the start
    // (SwordFightMatch.PresetPicks). Pure rules: SteamSession stores the values, the entrance screen and the scene ask here.
    public static class SwordPickDraft
    {
        // "매칭 완료!" (the two teams, VS) before the cards, and how long the cards stay open. R624 11-13 (사용자 "매칭 완료
        // 화면 좀 더 길게 해"): 3 -> 5 s.
        public const double FoundSeconds = 5.0, PickSeconds = 15.0;
        public const double CloseAt = FoundSeconds + PickSeconds;
        // A pick written this long before the match began is left over from an earlier round in the same room.
        public const double Stale = 1.0;

        public struct Entry
        {
            public ulong Id;
            public int Team;
            public bool Bot;
            // PieceKind.Pawn: no pick (yet).
            public PieceKind Piece;
            public double At;
        }

        public enum Stage { Found, Picking, Closed }

        public static bool Pickable(PieceKind piece) => Array.IndexOf(SwordMatchRules.Pickable, piece) >= 0;

        public static string Encode(PieceKind piece, double at) =>
            ((int)piece).ToString(CultureInfo.InvariantCulture) + "," + at.ToString("F3", CultureInfo.InvariantCulture);

        // False (and no pick) for an empty or broken text, or a piece that cannot be picked.
        public static bool Decode(string text, out PieceKind piece, out double at)
        {
            piece = PieceKind.Pawn;
            at = 0;
            if (string.IsNullOrEmpty(text)) return false;
            int comma = text.IndexOf(',');
            if (comma <= 0) return false;
            int kind;
            double time;
            if (!int.TryParse(text.Substring(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture, out kind)) return false;
            if (!double.TryParse(text.Substring(comma + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out time)) return false;
            if (double.IsNaN(time) || double.IsInfinity(time) || kind < 0 || kind > 255 || !Pickable((PieceKind)kind)) return false;
            piece = (PieceKind)kind;
            at = time;
            return true;
        }

        // The seed of the leftover draw: the match room's id, folded.
        public static int Seed(ulong match) => (int)((match ^ (match >> 32)) & 0x7FFFFFFF) | 1;

        // A pick that counts this round: a human's, on a piece that can be picked, written once the match began.
        static bool Counts(Entry e, double begun) => !e.Bot && Pickable(e.Piece) && e.At >= begun - Stale;

        static int Order(Entry a, Entry b)
        {
            int c = a.At.CompareTo(b.At);
            return c != 0 ? c : a.Id.CompareTo(b.Id);
        }

        static int ById(Entry a, Entry b) => a.Id.CompareTo(b.Id);

        /// <summary>Who holds which piece now: the earliest pick of each piece on each side (the lower id on a tie).
        /// The players missing are still picking, or lost their piece to a teammate and pick again.</summary>
        public static Dictionary<ulong, PieceKind> Resolve(IList<Entry> entries, double begun)
        {
            var held = new Dictionary<ulong, PieceKind>();
            var picks = new List<Entry>();
            foreach (var e in entries) if (Counts(e, begun)) picks.Add(e);
            picks.Sort(Order);
            var taken = new HashSet<int>();
            foreach (var e in picks)
            {
                int key = (e.Team == 0 ? 0 : 1) * 256 + (int)e.Piece;
                if (!taken.Add(key)) continue;
                held[e.Id] = e.Piece;
            }
            return held;
        }

        /// <summary>Every human player holds a piece (bots are drawn at the close). False with no human at all.</summary>
        public static bool AllPicked(IList<Entry> entries, double begun)
        {
            var held = Resolve(entries, begun);
            bool any = false;
            foreach (var e in entries)
            {
                if (e.Bot) continue;
                any = true;
                if (!held.ContainsKey(e.Id)) return false;
            }
            return any;
        }

        public static Stage StageAt(double sinceBegun, bool allPicked)
        {
            if (sinceBegun < FoundSeconds) return Stage.Found;
            if (sinceBegun < CloseAt && !allPicked) return Stage.Picking;
            return Stage.Closed;
        }

        // Seconds left on the cards (PickSeconds before they open, 0 once the time is up).
        public static double Left(double sinceBegun) => Math.Max(0, Math.Min(PickSeconds, CloseAt - sinceBegun));

        /// <summary>Everyone's piece once the picks close: the picks held (Resolve), then everyone else - bots and players
        /// who never picked, in id order - one of the pieces left on their side drawn from the seed (more than five a side:
        /// the sixth doubles one up). The same on every PC that sees the same picks.</summary>
        public static Dictionary<ulong, PieceKind> Final(IList<Entry> entries, double begun, int seed)
        {
            var result = Resolve(entries, begun);
            var rest = new List<Entry>();
            foreach (var e in entries) if (!result.ContainsKey(e.Id)) rest.Add(e);
            rest.Sort(ById);
            var draw = new Random(seed);
            foreach (var e in rest)
            {
                var left = new List<PieceKind>();
                foreach (var p in SwordMatchRules.Pickable) if (!Holds(result, entries, e.Team, p)) left.Add(p);
                var pool = left.Count > 0 ? left : new List<PieceKind>(SwordMatchRules.Pickable);
                result[e.Id] = pool[draw.Next(pool.Count)];
            }
            return result;
        }

        static bool Holds(Dictionary<ulong, PieceKind> held, IList<Entry> entries, int team, PieceKind piece)
        {
            foreach (var e in entries)
            {
                PieceKind p;
                if ((e.Team == 0) == (team == 0) && held.TryGetValue(e.Id, out p) && p == piece) return true;
            }
            return false;
        }
    }

    // The Sword Fight pieces' skill names, for the scene's HUD and the entrance screen's cards alike (R624: they were the
    // scene's own; SwordFightSkills.Name / EdgeName now read these).
    public static class SwordSkillNames
    {
        public static string Skill(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.King: return "왕의 반격";
                case PieceKind.Queen: return "꼬치 베기";
                case PieceKind.Rook: return "열린 파일 포격";
                case PieceKind.Bishop: return "관통 핀";
                case PieceKind.Knight: return "포크 강하";
                default: return "(폰은 스킬 없음)";
            }
        }

        public static string Edge(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.King: return "왕의 귀환";
                case PieceKind.Queen: return "체크메이트 일섬";
                case PieceKind.Rook: return "성벽 붕괴";
                case PieceKind.Bishop: return "대각선 미끄럼";
                case PieceKind.Knight: return "벼랑 끝 역전";
                default: return "(폰은 스킬 없음)";
            }
        }
    }
}
