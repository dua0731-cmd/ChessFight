using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The end of the race (R125): the crown platform at the top of the tower (the gold circle under the crown,
    // the course's FinishZone). A team wins the moment more than half of its players stand on it at once
    // (PawnRushFinish). Then every screen shows who won for a few seconds, the winners' PCs load PawnRushVictory
    // and the losers' PawnRushLose (PawnRushResultFlow), and from there everyone goes back to the lobby.
    //
    // Offline the playtest registers its one pawn; online the match link registers the roster (players count,
    // bots do not: they only stand at the start, R92) and only the host judges - the others take its word with
    // the course state (ApplyRemote). The finish times on the result board are when each pawn first stood on it.
    [DefaultExecutionOrder(-50)]
    public sealed class CrownFinish : MonoBehaviour
    {
        // How long the winner shows on the course before the result scenes load.
        public const float BannerSeconds = 3.5f;

        public sealed class Runner
        {
            public ICharacterDriver Driver;
            public int Team;
            public string Name = "";
            public byte Seat = SkillWireNone;   // the roster place (SkillWire.Ref), online
            public bool Counts = true;          // a player (a bot does not count toward the majority)
            public bool Self, Host;
            public float FirstOn = -1f;         // the race clock when it first stood on the platform
            public bool On;
        }

        const byte SkillWireNone = 255;

        public static CrownFinish Current { get; private set; }

        // The host's (or offline) decision, for the link to tell the room it ended.
        public event Action<int> Decided;

        // Judged here (offline, or this PC hosts); otherwise the host's word arrives with the course state.
        public bool Authority = true;

        readonly List<Runner> runners = new List<Runner>();
        Vector3 middle;
        float radius = PawnRushFinish.Radius;
        bool found;
        float raceStart;
        float decidedAt = -1f;
        bool shown;
        GUIStyle banner, line;

        public int Winner { get; private set; } = PawnRushFinish.None;
        public float Clock => Time.time - raceStart;
        public float WinClock { get; private set; }
        public IReadOnlyList<Runner> Runners => runners;
        public bool Found => found;
        public Vector3 Middle => middle;
        public float Radius => radius;

        void Awake()
        {
            Current = this;
            raceStart = Time.time;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start() => FindPlatform();

        // The platform: the course's finish circle (its trigger's middle, on its floor, and its radius).
        void FindPlatform()
        {
            foreach (var zone in FindObjectsByType<FinishZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!Platform(zone, out middle, out radius)) continue;
                found = true;
                return;
            }
            Debug.LogWarning("[PawnRush] 결승 왕관 발판(FinishZone)을 찾지 못했습니다. 승리 판정이 꺼집니다.");
        }

        // A finish circle's middle on its floor and its radius: the tower's 6 m crown circle, the endgame throne's 8.5 m.
        public static bool Platform(FinishZone zone, out Vector3 middle, out float radius)
        {
            middle = default;
            radius = PawnRushFinish.Radius;
            var collider = zone != null ? zone.GetComponent<Collider>() : null;
            if (collider == null) return false;
            // The circle is a cylinder primitive (CourseBuilder.Cylinder): its collider is a capsule, which Unity rounds
            // into a ball when it is wider than tall, so its bounds are not the circle. A cylinder's half height is its
            // y scale and its diameter its x scale.
            if (collider is CapsuleCollider)
            {
                var scale = zone.transform.lossyScale;
                middle = zone.transform.position - Vector3.up * Mathf.Abs(scale.y);
                radius = Mathf.Abs(scale.x) * .5f;
            }
            else
            {
                var b = collider.bounds;
                middle = new Vector3(b.center.x, b.min.y, b.center.z);
                radius = Mathf.Min(b.extents.x, b.extents.z);
            }
            return true;
        }

        // ------------------------------------------------------------------ who runs

        public Runner Register(ICharacterDriver driver, int team, string name, bool counts, bool self, byte seat = SkillWireNone, bool host = false)
        {
            if (driver == null) return null;
            var r = Find(driver);
            if (r == null) runners.Add(r = new Runner { Driver = driver });
            r.Team = team;
            r.Name = name ?? "";
            r.Counts = counts;
            r.Self = self;
            r.Seat = seat;
            r.Host = host;
            return r;
        }

        public void Unregister(ICharacterDriver driver)
        {
            var r = Find(driver);
            if (r != null) runners.Remove(r);
        }

        public Runner Find(ICharacterDriver driver)
        {
            foreach (var r in runners) if (r.Driver == driver) return r;
            return null;
        }

        public Runner BySeat(byte seat)
        {
            foreach (var r in runners) if (r.Seat == seat) return r;
            return null;
        }

        // The race from the start again (the offline playtest's restart): nobody has finished, nobody won.
        public void Restart()
        {
            raceStart = Time.time;
            Winner = PawnRushFinish.None;
            decidedAt = -1f;
            shown = false;
            foreach (var r in runners) { r.FirstOn = -1f; r.On = false; }
        }

        // ------------------------------------------------------------------ judging

        void FixedUpdate()
        {
            if (!found) return;
            int whiteOn = 0, blackOn = 0, whitePlayers = 0, blackPlayers = 0;
            foreach (var r in runners)
            {
                var hips = r.Driver != null ? r.Driver.FollowTarget : null;
                r.On = hips != null && PawnRushFinish.OnPlatform(hips.position.x - middle.x, hips.position.y - middle.y, hips.position.z - middle.z, radius);
                if (!Authority) continue;
                if (r.On && r.FirstOn < 0f && Winner == PawnRushFinish.None) r.FirstOn = Clock;
                if (!r.Counts) continue;
                if (r.Team == Teams.White) { whitePlayers++; if (r.On) whiteOn++; }
                else if (r.Team == Teams.Black) { blackPlayers++; if (r.On) blackOn++; }
            }
            if (!Authority || Winner != PawnRushFinish.None) return;
            int winner = PawnRushFinish.Winner(whiteOn, whitePlayers, blackOn, blackPlayers);
            if (winner != PawnRushFinish.None) Decide(winner, Clock);
        }

        void Decide(int team, float clock)
        {
            Winner = team;
            WinClock = clock;
            decidedAt = Time.time;
            Debug.Log($"[PawnRush] 승리: {Teams.Name(team)}팀 (왕관 발판 과반, {MatchResult.Clock(clock)})");
            Decided?.Invoke(team);
        }

        // The host's word (online, not the host): the winner, the race clock and who has stood on the platform when.
        public void ApplyRemote(int winner, float clock, byte[] seats, float[] times, int count)
        {
            raceStart = Time.time - clock;
            for (int i = 0; i < count; i++)
            {
                var r = BySeat(seats[i]);
                if (r != null) r.FirstOn = times[i];
            }
            if (Winner == PawnRushFinish.None && (winner == 0 || winner == 1)) Decide(winner, clock);
        }

        // Counts for the HUD and the course state.
        public int OnPlatform(int team)
        {
            int n = 0;
            foreach (var r in runners) if (r.Counts && r.Team == team && r.On) n++;
            return n;
        }

        public int Players(int team)
        {
            int n = 0;
            foreach (var r in runners) if (r.Counts && r.Team == team) n++;
            return n;
        }

        // ------------------------------------------------------------------ the result

        void Update()
        {
            if (Winner == PawnRushFinish.None || shown || Time.time - decidedAt < BannerSeconds) return;
            shown = true;
            var self = Self;
            int team = self != null ? self.Team : Teams.White;
            try { PawnRushResultFlow.Show(Result(), team); }
            catch (Exception e)
            {
                Debug.LogException(e);
                PawnRushResultFlow.ToLobby();
            }
        }

        Runner Self
        {
            get
            {
                foreach (var r in runners) if (r.Self) return r;
                return null;
            }
        }

        // The race as the result scenes show it: everyone registered, the finish clock of those who reached the
        // platform, and how far the others still were.
        public MatchResult Result()
        {
            var course = PawnRushCourse.Current;
            var result = new MatchResult
            {
                WinningTeam = Winner,
                Needed = PawnRushFinish.Needed(Players(Winner)),
                MatchSeconds = WinClock,
            };
            foreach (var r in runners)
            {
                var player = new ResultPlayer
                {
                    Name = r.Name, Team = r.Team == Teams.Black ? 1 : 0, Self = r.Self, Host = r.Host,
                    Piece = r.Driver is IPromotable p ? p.Piece : PieceKind.Pawn,
                    FinishSeconds = r.FirstOn >= 0f ? r.FirstOn : -1,
                };
                var hips = r.Driver != null ? r.Driver.FollowTarget : null;
                if (!player.Finished && course != null && hips != null)
                {
                    int team = Mathf.Max(0, r.Team);
                    float total = ProgressPath.Length(course.Path(team));
                    player.MetersLeft = Mathf.Max(0f, total - course.Along(hips.position, team, out _));
                }
                result.Players.Add(player);
            }
            return result;
        }

        // ------------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (!found) return;
            if (banner == null)
            {
                banner = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 40, alignment = TextAnchor.MiddleCenter, richText = true };
                line = new GUIStyle(banner) { fontSize = 15 };
            }
            int w = Players(Teams.White), b = Players(Teams.Black);
            string count = $"왕관 발판  백 {OnPlatform(Teams.White)}/{PawnRushFinish.Needed(w)}  ·  흑 {OnPlatform(Teams.Black)}/{PawnRushFinish.Needed(b)}  (팀 과반이 동시에 올라가면 승리)";
            var box = new Rect((Screen.width - 520) * .5f, 10, 520, 26);
            GUI.Box(box, GUIContent.none);
            GUI.Label(box, count, line);
            if (Winner == PawnRushFinish.None) return;
            var self = Self;
            bool won = self != null && self.Team == Winner;
            string title = $"<b>{Teams.Name(Winner)}팀 승리!</b>";
            string sub = self == null ? "" : won ? "우리 팀이 왕관 발판을 차지했습니다" : "상대 팀이 왕관 발판을 차지했습니다";
            var area = new Rect(0, Screen.height * .32f, Screen.width, 120);
            GUI.Box(new Rect(area.x, area.y, area.width, area.height), GUIContent.none);
            GUI.Label(new Rect(area.x, area.y + 8, area.width, 60), title, banner);
            GUI.Label(new Rect(area.x, area.y + 74, area.width, 30), $"{sub} · 곧 결과 화면", line);
        }
    }
}
