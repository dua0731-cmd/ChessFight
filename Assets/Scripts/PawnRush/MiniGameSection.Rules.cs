using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R703 (the user 2026-11-17: five ways for the round's flow, picked by the room's manager - PawnRushRules): what a
    // mini-game room does with the rules of the round it stands in.
    //
    //   Option 4 (After = AfterDelay)   one team has cleared its game and gone through; PawnRushRules.GateDelay
    //       seconds later the other team's gate opens as well, cleared or not (MiniGameBase.CompleteLate, on the PC
    //       that judges the games - offline or the host; the others hear it as that game's done). The team still at
    //       it sees the seconds count down.
    //   Option 3 (After = AfterGather)  R704: a cleared game does not open its gate; the gate opens when most of
    //       the team's players stand together on the gold pad before it (GatherPad - the win's own majority). The
    //       PC that judges the games opens it; the others hear it with the course state's gate flag. The team
    //       sees how many are on the pad and how many it takes, wherever on the course its players are.
    //   Options 1 and 2 (Relief)        once one team (ReliefFirst) or both (ReliefBoth; a gate opened late counts)
    //       have cleared, the obstacle section before this room gets its relief (SectionRelief). Every PC works this
    //       out from the cleared flags it has, so nothing more is sent.
    //
    // The moment of the first clear is this PC's own (the shared obstacle clock when it saw it): the host's for the
    // gate, each PC's for its countdown - a snapshot apart at most.
    public sealed partial class MiniGameSection
    {
        double firstClearAt = -1;
        int firstTeam = -1;
        bool relieved;
        // For this PC's team: seconds until its gate opens by itself (option 4), below 0 for no such wait.
        float gateOpensIn = -1f;
        // R704, option 3: the pads before the two gates, and for this PC's team how many of its players stand on its
        // pad of how many needed while its gate waits for them (gatherOn below 0: no such wait).
        readonly GatherPad[] pads = new GatherPad[2];
        int gatherOn = -1, gatherNeed;

        void Update()
        {
            if (stations == null || stations.Length != 2 || stations[0] == null || stations[1] == null) return;
            var a = stations[0].Game;
            var b = stations[1].Game;
            if (a == null || b == null) return;
            var rules = PawnRushRules.Current;
            Gather(rules.After == PawnRushRules.AfterGather);
            bool doneA = a.Completed, doneB = b.Completed;
            if (!doneA && !doneB)
            {
                // A new round (the games were reset).
                firstClearAt = -1;
                firstTeam = -1;
                relieved = false;
                gateOpensIn = -1f;
                return;
            }
            if (firstClearAt < 0)
            {
                firstClearAt = ObstacleClock.Now;
                firstTeam = doneA ? stations[0].Team : stations[1].Team;
            }

            gateOpensIn = -1f;
            if (rules.After == PawnRushRules.AfterDelay && doneA != doneB)
            {
                double left = PawnRushRules.GateDelay - (ObstacleClock.Now - firstClearAt);
                int waiting = doneA ? stations[1].Team : stations[0].Team;
                if (waiting == MissionStation.LocalTeam) gateOpensIn = Mathf.Max(0f, (float)left);
                if (left <= 0 && !MiniGameBase.Remote)
                {
                    var late = doneA ? b : a;
                    Debug.Log($"[PawnRush] 시차 개방: {Teams.Name(waiting)}팀 문이 {PawnRushRules.GateDelay:0}초 뒤 열림 ({GetComponent<PawnRushSection>()?.DisplayName})");
                    late.CompleteLate();
                }
            }

            if (!relieved && rules.Relief != PawnRushRules.ReliefOff && (rules.Relief == PawnRushRules.ReliefFirst || (doneA && doneB)))
            {
                relieved = true;
                SectionRelief.Apply(SectionBefore());
            }
        }

        // Option 3: the pads' pictures on every PC, the gates' opening where the games are judged.
        void Gather(bool rule)
        {
            gatherOn = -1;
            for (int i = 0; i < 2; i++)
            {
                var station = stations[i];
                if (!rule)
                {
                    if (pads[i] != null) pads[i].Hide();
                    continue;
                }
                if (pads[i] == null) pads[i] = GatherPad.Make(station);
                var pad = pads[i];
                if (pad == null) continue;
                var game = station.Game;
                bool cleared = game != null && game.Completed, open = station.Gate.IsOpen;
                pad.Count(out int on, out int players);
                pad.Show(cleared, open, on, players);
                if (!pad.Waiting) continue;
                if (!MiniGameBase.Remote && on >= pad.Need)
                {
                    Debug.Log($"[PawnRush] 모여서 발판: {Teams.Name(station.Team)}팀 {on}/{pad.Need}명이 모여 문이 열림 ({GetComponent<PawnRushSection>()?.DisplayName})");
                    station.OpenGateNow();
                    continue;
                }
                if (station.Team == MissionStation.LocalTeam)
                {
                    gatherOn = on;
                    gatherNeed = pad.Need;
                }
            }
        }

        // The obstacle section this room's players came through.
        PawnRushSection SectionBefore()
        {
            var own = GetComponent<PawnRushSection>();
            var round = GetComponentInParent<PawnRushRound>();
            if (round == null) round = FindFirstObjectByType<PawnRushRound>();
            if (own == null || round == null) return null;
            foreach (var section in round.Sections)
                if (section != null && section.Slot == own.Slot - 1 && section.Kind == PawnRushSection.SlotKind.Obstacle) return section;
            return null;
        }

        // Guidance's OnGUI draws it over the lane hint. The gather line goes to the whole team wherever its players
        // are (the ones behind are who the gate waits for); the countdown only to the ones in the room.
        void RulesLine(float scale, bool inRoom)
        {
            string words = null;
            if (gatherOn >= 0)
                words = Loc.T(inRoom ? "미니게임 클리어 · 문 앞 금색 발판에 모이세요" : "우리 팀이 미니게임을 깼습니다 · 문 앞 금색 발판으로 오세요")
                        + "  " + Loc.F("{0} / {1}명", gatherOn, gatherNeed);
            else if (inRoom && gateOpensIn >= 0f)
                words = Loc.F("상대 팀이 먼저 통과했습니다 · {0}초 뒤 우리 문도 열립니다", Mathf.CeilToInt(gateOpensIn));
            if (words == null) return;
            float width = Mathf.Min(620f * scale, Screen.width - 8f);
            var rect = new Rect((Screen.width - width) * .5f, Screen.height - 176f * scale, width, 38f * scale);
            GUI.color = new Color(.13f, .07f, .05f, .9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, .78f, .35f, 1f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 4f * scale, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            directionStyle.fontSize = Mathf.RoundToInt(17f * scale);
            GUI.Label(rect, words, directionStyle);
        }
    }
}
