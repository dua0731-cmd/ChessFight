using System;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R707 (the flow rules' option 5 - the user 2026-11-17: "현 엔딩 구간의 마지막 문을 열면 모두가 마지막 왕좌 차지 게임으로
    // 텔레포트되고 과반수 이상이 발판 위에 일정 시간 올라와 있어야 승리 ... 미니게임 클리어 시 엔딩구간에 올라가있어야하는
    // 시간 차감 (ex. 원래 총 30초인데 미니게임 클리어 팀은 5초씩 차감)"; the R699 proposal's numbers, taken as they
    // stood): the round's ending under PawnRushRules.EndingThrone. It stands on the round's ending section (the one
    // with the finish), put there by PawnRushRound when the round is built, on every PC.
    //
    //   begins   the moment the great gate of the section before the ending is open (PushGate: its bar full), or
    //            somebody is already through it and past the ending's entry. Everyone - players and bots - is stood
    //            beside the crown platform, white on the left of the way in and black on the right, the same distance
    //            from it, facing it; from then on a pawn that falls comes back at the ending's own checkpoint.
    //   the time while most of a team's players stand on the platform (the win's own majority, CrownFinish) that
    //            team's time runs; with both teams' majorities on it at once nobody's does. Time is kept when a
    //            team steps off. The first team to fill its time wins (CrownFinish.Win).
    //   how much PawnRushThrone.Seconds (30), less PerClear (5) for each mini-game the team cleared by playing it -
    //            a gate that opened by the clock (option 4, MiniGameBase.Late) is not a clear.
    //
    // The PC that judges (offline, the host: CrownFinish.Authority) begins it, moves the pawns and counts; the others
    // hear it with the course state (ISectionNetPart, seven bytes: begun and who holds, the two times, the two
    // targets) and draw the same bars. The platform's first-stood times (the result board) count from the beginning.
    //
    // An ending section built for the contest (the audience hall, AudienceHallSection; JY 2026-11-21) says where the
    // teams are stood and for how long after that the platform counts for nobody (IThroneStage). The standoff needs no
    // bytes: the host sends "nobody holds" through it, and every PC counts it from when it heard the beginning.
    [DefaultExecutionOrder(-40)]   // after CrownFinish (-50) has seen who stands on the platform this step
    public sealed class ThroneContest : MonoBehaviour, ISectionNetPart
    {
        // Where the teams are stood: this far outside the platform's edge, this far apart along it (unless the ending
        // says otherwise, IThroneStage).
        const float SideGap = 2.2f, Along = 1.2f;
        const float GoSeconds = .8f;
        const float BannerSeconds = 5f, RingEvery = 1f;

        public static ThroneContest Current { get; private set; }
        // On every PC, the moment the contest begins there (the pawns have just been stood by the platform).
        public static event Action Began;
        // The lower edge of the bars on screen (0 while they are not drawn): the finish's note goes under them.
        public static float HudBottom { get; private set; }

        CrownFinish finish;
        PawnRushRound round;
        PawnRushSection own;
        PushGate gate;
        Checkpoint checkpoint;
        readonly float[] held = new float[2];
        readonly float[] target = { PawnRushThrone.Seconds, PawnRushThrone.Seconds };
        int holder = PawnRushThrone.Nobody;
        float beganAt = -1f, nextRing;
        float standGap = SideGap, standoff;
        GUIStyle big, small;

        public bool Started { get; private set; }
        // The first seconds after the teams are stood, while the platform counts for nobody (IThroneStage).
        public bool Waiting => Started && standoff > 0f && beganAt >= 0f && Time.time < beganAt + standoff;
        public int Holder => holder;
        public float Held(int team) => held[team & 1];
        public float Target(int team) => target[team & 1];
        public PushGate Gate => gate;

        // The round's finish under the rule (PawnRushRound.BindFinish, on every PC, each time the round is built).
        public static void Attach(PawnRushRound round, CrownFinish finish)
        {
            if (finish == null) return;
            finish.Contest = null;
            if (round == null || PawnRushRules.Current.Ending != PawnRushRules.EndingThrone) return;
            PawnRushSection ending = null;
            foreach (var section in round.Sections)
                if (section != null && section.GetComponentInChildren<FinishZone>(true) != null) ending = section;
            if (ending == null)
            {
                Debug.LogWarning("[PawnRush] 왕좌 차지전: 이번 판에 골(FinishZone)이 있는 구간이 없습니다. 왕관 발판 규칙으로 진행합니다.");
                return;
            }
            var contest = ending.GetComponent<ThroneContest>();
            if (contest == null) contest = ending.gameObject.AddComponent<ThroneContest>();
            contest.Setup(round, finish, ending);
            finish.Contest = contest;
        }

        void Setup(PawnRushRound ofRound, CrownFinish ofFinish, PawnRushSection ending)
        {
            round = ofRound;
            finish = ofFinish;
            own = ending;
            var stage = ending.GetComponent<IThroneStage>();
            standGap = stage != null ? stage.StandGap : SideGap;
            standoff = stage != null ? Mathf.Max(0f, stage.StandoffSeconds) : 0f;
            Current = this;
            gate = null;
            foreach (var section in round.Sections)
                if (section != null && section.Slot == ending.Slot - 1)
                    foreach (var g in section.GetComponentsInChildren<PushGate>(true)) gate = g;
            checkpoint = null;
            foreach (var c in ending.GetComponentsInChildren<Checkpoint>(true))
                if (checkpoint == null || c.Order < checkpoint.Order) checkpoint = c;
            ResetContest();
            Debug.Log($"[PawnRush] 왕좌 차지전: {ending.DisplayName}에서 · 시작 신호 {(gate != null ? "앞 구간의 큰 문(" + gate.name + ")" : "엔딩 구간에 들어서는 첫 폰")}");
        }

        // The race from the start again (CrownFinish.Restart, the offline playtest).
        public void ResetContest()
        {
            Started = false;
            held[0] = held[1] = 0f;
            target[0] = target[1] = PawnRushThrone.Seconds;
            holder = PawnRushThrone.Nobody;
            beganAt = -1f;
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                HudBottom = 0f;
            }
        }

        // ------------------------------------------------------------------ judging

        void FixedUpdate()
        {
            if (finish == null || !finish.Found || finish.Winner != PawnRushFinish.None) return;
            if (!finish.Authority) return;   // the host's word comes with the course state
            if (!Started)
            {
                if (Due()) Begin();
                return;
            }
            if (Waiting)
            {
                holder = PawnRushThrone.Nobody;
                return;
            }
            holder = PawnRushThrone.Holder(finish.OnPlatform(Teams.White), finish.Players(Teams.White),
                                           finish.OnPlatform(Teams.Black), finish.Players(Teams.Black));
            int winner = PawnRushThrone.Step(ref held[0], ref held[1], holder, Time.fixedDeltaTime, target[0], target[1]);
            if (winner != PawnRushThrone.Nobody) finish.Win(winner);
        }

        // The great gate is open, or somebody is already through it and past the ending's entry (the ending's frame:
        // its entry, the way on along +z).
        bool Due()
        {
            if (gate != null && gate.IsOpen) return true;
            var frame = own.EntryWorld;
            var back = Quaternion.Inverse(frame.rotation);
            var runners = finish.Runners;
            for (int i = 0; i < runners.Count; i++)
            {
                var hips = runners[i].Driver != null ? runners[i].Driver.FollowTarget : null;
                if (hips == null) continue;
                var p = back * (hips.position - frame.position);
                if (p.z > .5f && p.z < 80f && Mathf.Abs(p.x) < 20f && Mathf.Abs(p.y) < 12f) return true;
            }
            return false;
        }

        void Begin()
        {
            for (int team = 0; team < 2; team++) target[team] = PawnRushThrone.Target(Clears(team));
            Started = true;
            Place();
            OnBegan();
        }

        // The mini-games this team cleared by playing them.
        int Clears(int team)
        {
            int n = 0;
            foreach (var section in round.Sections)
            {
                var room = section != null ? section.GetComponent<MiniGameSection>() : null;
                if (room == null || room.Stations == null) continue;
                foreach (var station in room.Stations)
                {
                    var game = station != null && station.Team == team ? station.Game : null;
                    if (game != null && game.Completed && !game.Late) n++;
                }
            }
            return n;
        }

        // Everyone beside the platform: white on the left of the way in, black on the right, in a row along it.
        void Place()
        {
            var frame = own.EntryWorld;
            Vector3 middle = finish.Middle, right = frame.rotation * Vector3.right, forward = frame.rotation * Vector3.forward;
            var runners = finish.Runners;
            for (int team = 0; team < 2; team++)
            {
                int n = 0, i = 0;
                for (int k = 0; k < runners.Count; k++) if (runners[k].Team == team && runners[k].Driver != null) n++;
                for (int k = 0; k < runners.Count; k++)
                {
                    var r = runners[k];
                    if (r.Team != team || r.Driver == null) continue;
                    Vector3 at = middle + right * ((team == Teams.White ? -1f : 1f) * (finish.Radius + standGap))
                                 + forward * ((i - (n - 1) * .5f) * Along);
                    i++;
                    at = Ground(at);
                    Vector3 look = middle - at;
                    look.y = 0f;
                    r.Driver.Teleport(at, look.sqrMagnitude > .01f ? Quaternion.LookRotation(look) : frame.rotation);
                }
            }
        }

        // The floor under a spot (a pawn or a loose thing there is skipped); where there is none, the ending's own
        // checkpoint.
        Vector3 Ground(Vector3 at)
        {
            float best = float.MaxValue, y = at.y;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 2.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                var body = hit.collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                if (hit.collider.GetComponentInParent<ICharacterDriver>() != null) continue;
                if (hit.distance < best) { best = hit.distance; y = hit.point.y; }
            }
            if (best == float.MaxValue && checkpoint != null) return checkpoint.RespawnPosition;
            return new Vector3(at.x, y, at.z);
        }

        // On every PC, once.
        void OnBegan()
        {
            beganAt = Time.time;
            // From here a pawn that falls comes back at the ending's start, not where it was on the course.
            if (checkpoint != null)
            {
                var runners = finish.Runners;
                for (int i = 0; i < runners.Count; i++)
                    if (runners[i].Driver != null) Checkpoint.Report(runners[i].Driver, checkpoint);
            }
            Debug.Log($"[PawnRush] 왕좌 차지전 시작: 백 {target[0]:0}초 · 흑 {target[1]:0}초");
            Began?.Invoke();
        }

        // ------------------------------------------------------------------ online (ISectionNetPart)

        public int NetBytes => 7;

        public void WriteNet(ref NetBlock block)
        {
            block.U8((Started ? 1 : 0) | ((holder + 1) << 1));   // who holds: -1..2 as 0..3
            block.U16(Mathf.Clamp(Mathf.RoundToInt(held[0] * 100f), 0, 65535));
            block.U16(Mathf.Clamp(Mathf.RoundToInt(held[1] * 100f), 0, 65535));
            block.U8(Mathf.Clamp(Mathf.RoundToInt(target[0]), 0, 255));
            block.U8(Mathf.Clamp(Mathf.RoundToInt(target[1]), 0, 255));
        }

        public void ReadNet(ref NetBlock block)
        {
            int flags = block.ReadU8();
            float white = block.ReadU16() / 100f, black = block.ReadU16() / 100f;
            int whiteTarget = block.ReadU8(), blackTarget = block.ReadU8();
            if ((flags & 1) == 0) return;   // not begun there yet (it never un-begins)
            holder = ((flags >> 1) & 3) - 1;
            held[0] = white;
            held[1] = black;
            target[0] = whiteTarget;
            target[1] = blackTarget;
            if (Started) return;
            Started = true;
            OnBegan();
        }

        // ------------------------------------------------------------------ what is seen

        void Update()
        {
            if (!Started || finish == null || finish.Winner != PawnRushFinish.None) return;
            if ((holder != Teams.White && holder != Teams.Black) || Time.time < nextRing) return;
            nextRing = Time.time + RingEvery;
            ArenaFx.Ring(finish.Middle + Vector3.up * .08f, finish.Radius * .35f, finish.Radius + .5f, .8f, Arena.TeamColor(holder, true));
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            HudBottom = 0f;
            if (!Started || finish == null || finish.Winner != PawnRushFinish.None || LoadingScreenView.Showing) return;
            float unit = Mathf.Clamp(Screen.height / 1080f, .6f, 2f);
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false };
                small = new GUIStyle(big);
            }
            var was = GUI.color;
            float top = PawnRushStandingsHud.Showing ? PawnRushStandingsHud.Bottom + 8f * unit : 46f * unit;
            float width = Mathf.Min(860f * unit, Screen.width - 16f), height = 78f * unit, mid = 190f * unit;
            var box = new Rect((Screen.width - width) * .5f, top, width, height);
            GUI.color = new Color(.06f, .05f, .04f, .86f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            float side = (width - mid) * .5f - 14f * unit;
            Bar(new Rect(box.x + 10f * unit, box.y, side, height), Teams.White, unit);
            Bar(new Rect(box.xMax - 10f * unit - side, box.y, side, height), Teams.Black, unit);
            // Between the bars: who holds the platform now.
            bool waiting = Waiting;
            string state = waiting ? "곧 발판이 켜집니다"
                         : holder == Teams.White ? "백팀 점유 중" : holder == Teams.Black ? "흑팀 점유 중"
                         : holder == PawnRushThrone.Both ? "동시 점유 · 멈춤" : "발판이 비었습니다";
            GUI.color = waiting ? new Color(1f, .7f, .35f)
                      : holder == PawnRushThrone.Both ? new Color(1f, .55f, .45f) : holder == PawnRushThrone.Nobody ? new Color(.8f, .76f, .66f) : new Color(1f, .86f, .45f);
            small.fontSize = Mathf.RoundToInt(17f * unit);
            GUI.Label(new Rect(box.center.x - mid * .5f, box.y + 30f * unit, mid, 26f * unit), state, small);
            GUI.color = new Color(1f, .9f, .6f);
            small.fontSize = Mathf.RoundToInt(13f * unit);
            GUI.Label(new Rect(box.center.x - mid * .5f, box.y + 8f * unit, mid, 20f * unit), "왕좌 차지전", small);
            HudBottom = box.yMax;

            // The first seconds: what this is.
            float since = Time.time - beganAt;
            if (beganAt >= 0f && since < BannerSeconds)
            {
                float alpha = Mathf.Clamp01(since / .25f) * Mathf.Clamp01((BannerSeconds - since) / .6f);
                var band = new Rect(0f, Screen.height * .3f, Screen.width, 150f * unit);
                GUI.color = new Color(.05f, .04f, .03f, .8f * alpha);
                GUI.DrawTexture(band, Texture2D.whiteTexture);
                GUI.color = new Color(1f, .86f, .45f, alpha);
                big.fontSize = Mathf.RoundToInt(46f * unit);
                GUI.Label(new Rect(band.x, band.y + 10f * unit, band.width, 62f * unit), "왕좌 차지전!", big);
                GUI.color = new Color(.96f, .92f, .82f, alpha);
                small.fontSize = Mathf.RoundToInt(19f * unit);
                GUI.Label(new Rect(band.x, band.y + 76f * unit, band.width, 28f * unit), "금색 발판 위에 팀 과반이 올라가 있는 동안 시간이 쌓입니다 · 먼저 채우는 팀이 승리", small);
                small.fontSize = Mathf.RoundToInt(16f * unit);
                GUI.Label(new Rect(band.x, band.y + 108f * unit, band.width, 26f * unit),
                          $"백팀 {target[0]:0}초 · 흑팀 {target[1]:0}초   (스스로 깬 미니게임 하나에 {PawnRushThrone.PerClear:0}초 단축)", small);
            }
            // The standoff (IThroneStage): 3, 2, 1 under the banner, then a moment of "on".
            if (standoff > 0f && beganAt >= 0f)
            {
                float left = beganAt + standoff - Time.time;
                if (left > -GoSeconds)
                {
                    GUI.color = new Color(1f, .86f, .45f, left > 0f ? 1f : Mathf.Clamp01((left + GoSeconds) / GoSeconds));
                    big.fontSize = Mathf.RoundToInt(80f * unit);
                    GUI.Label(new Rect(0f, Screen.height * .3f + 160f * unit, Screen.width, 100f * unit),
                              left > 0f ? Mathf.CeilToInt(left).ToString() : "발판 켜짐!", big);
                }
            }
            GUI.color = was;
        }

        // One team's line and bar: "백팀 14.2 / 20초 · 발판 3/4", the bar filled to its time.
        void Bar(Rect area, int team, float unit)
        {
            bool holds = holder == team;
            float share = target[team] > 0f ? Mathf.Clamp01(held[team] / target[team]) : 0f;
            int players = finish.Players(team);
            string need = players > 0 ? finish.OnPlatform(team) + "/" + PawnRushFinish.Needed(players) : "-";
            GUI.color = holds ? new Color(1f, .9f, .6f) : new Color(.93f, .89f, .8f);
            small.fontSize = Mathf.RoundToInt(15f * unit);
            GUI.Label(new Rect(area.x, area.y + 8f * unit, area.width, 22f * unit),
                      $"{Teams.Name(team)}팀  {held[team]:0.0} / {target[team]:0}초  ·  발판 {need}", small);
            var track = new Rect(area.x, area.y + 38f * unit, area.width, 24f * unit);
            GUI.color = holds ? new Color(1f, .82f, .35f) : new Color(.45f, .4f, .32f);
            GUI.DrawTexture(new Rect(track.x - 2f, track.y - 2f, track.width + 4f, track.height + 4f), Texture2D.whiteTexture);
            GUI.color = new Color(.12f, .1f, .09f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = team == Teams.White ? new Color(.95f, .91f, .8f) : new Color(.42f, .36f, .5f);
            // White's bar fills from the left, black's from the right: toward each other.
            float w = track.width * share;
            GUI.DrawTexture(team == Teams.White ? new Rect(track.x, track.y, w, track.height) : new Rect(track.xMax - w, track.y, w, track.height), Texture2D.whiteTexture);
        }
    }

    // An ending section built for the throne contest (AudienceHallSection): where the teams are stood and how long the
    // platform waits after that. Every PC builds the same section, so every PC reads the same numbers.
    public interface IThroneStage
    {
        // Metres from the platform's edge to each team's row, across the way in (white left, black right).
        float StandGap { get; }
        // Seconds after the teams are stood in which the platform counts for nobody (0: at once).
        float StandoffSeconds { get; }
    }
}
