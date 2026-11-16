using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public sealed partial class MiniGameSection
    {
        readonly MgCheckmate[] teamHunts = new MgCheckmate[2];
        GUIStyle directionStyle, briefTitle, briefBody, briefSmall;
        string visibleHint;
        int hintTeam;
        // R679: the briefing card, from the moment this PC's pawn walks into the room
        const float BriefSeconds = 9f, BriefAgain = 20f;
        float briefAt = -100f, lastInside = -100f;
        bool teamGateOpen;

        // Left and right refer to the view from the entrance. In A both teams play the same board.
        public string DirectionHint(int team, Vector3 world)
        {
            if ((team != Teams.White && team != Teams.Black) || stations == null || stations.Length != 2) return null;
            var p = transform.InverseTransformPoint(world);
            if (p.z < -8f || p.z > 28.5f || Mathf.Abs(p.x) > (p.z < 0f ? 6.5f : 22.5f) || p.y < -2f || p.y > 20f) return null;
            var station = stations[0] != null && stations[0].Team == team ? stations[0] : stations[1];
            if (station == null || station.Team != team) return null;
            bool left = transform.InverseTransformPoint(station.transform.position).x < 0f;
            if (station.Gate != null && station.Gate.IsOpen) return left ? "입구 기준 왼쪽 출구가 열렸습니다" : "입구 기준 오른쪽 출구가 열렸습니다";
            if (game == PawnRushMissions.Paint)
                return team == Teams.White ? "중앙 공용 판  ·  밝은 칸을 칠하세요" : "중앙 공용 판  ·  어두운 칸을 칠하세요";
            if (p.z < 5.5f || Mathf.Abs(p.x) < 2f)
                return left ? "입구에서 왼쪽으로  ·  우리 팀 진영" : "입구에서 오른쪽으로  ·  우리 팀 진영";
            if ((p.x < 0f) != left)
                return left ? "상대 진영  ·  우리 진영은 입구 기준 왼쪽" : "상대 진영  ·  우리 진영은 입구 기준 오른쪽";
            return teamHunts[team] != null ? teamHunts[team].HuntHint : null;
        }

        // R679: is this team's station the one on the left, seen from the entrance (white's is, as built).
        bool OnLeft(int team)
        {
            if (stations == null) return team == Teams.White;
            foreach (var station in stations)
                if (station != null && station.Team == team) return transform.InverseTransformPoint(station.transform.position).x < 0f;
            return team == Teams.White;
        }

        void LateUpdate()
        {
            var driver = MiniGameBase.LocalDriver;
            var target = driver?.FollowTarget;
            hintTeam = MissionStation.LocalTeam;
            bool inside = target != null && InRoom(hintTeam, target.position);
            visibleHint = inside && !GameMenu.IsOpen ? DirectionHint(hintTeam, target.position) : null;
            if (StationPromotion.ChoosingFor(driver) != null) { visibleHint = null; inside = false; }
            if (GameMenu.IsOpen) inside = false;
            // R679: the card opens as the pawn walks in, and again if it comes back after a while away (a respawn
            // at the room's own checkpoint does not open it again)
            if (inside)
            {
                if (Time.time - lastInside > BriefAgain) briefAt = Time.time;
                lastInside = Time.time;
            }
            teamGateOpen = false;
            if (inside && stations != null)
                foreach (var station in stations)
                    if (station != null && station.Team == hintTeam && station.Gate != null && station.Gate.IsOpen) teamGateOpen = true;
            showBrief = inside;
        }

        bool showBrief;

        // The room's box, as DirectionHint's (from 8 m before the entrance to the gates' line).
        bool InRoom(int team, Vector3 world)
        {
            if ((team != Teams.White && team != Teams.Black) || stations == null || stations.Length != 2) return false;
            var p = transform.InverseTransformPoint(world);
            return !(p.z < -8f || p.z > 28.5f || Mathf.Abs(p.x) > (p.z < 0f ? 6.5f : 22.5f) || p.y < -2f || p.y > 20f);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || LoadingScreenView.Showing) return;
            if (directionStyle == null)
            {
                directionStyle = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                briefTitle = new GUIStyle(directionStyle) { richText = true };
                briefBody = new GUIStyle(directionStyle) { fontStyle = FontStyle.Normal, wordWrap = true };
                briefSmall = new GUIStyle(directionStyle) { richText = true };
            }
            float scale = Mathf.Clamp(Screen.height / 900f, .72f, 1.8f);
            var previous = GUI.color;
            if (showBrief && !teamGateOpen) Briefing(scale);
            if (visibleHint != null)
            {
                float width = Mathf.Min(390f * scale, Screen.width - 8f);
                var rect = new Rect((Screen.width - width) * .5f, Screen.height - 128f * scale, width, 36f * scale);
                GUI.color = new Color(.07f, .09f, .13f, .9f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Arena.TeamColor(hintTeam, true);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 4f * scale, rect.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                directionStyle.fontSize = Mathf.RoundToInt(17f * scale);
                GUI.Label(new Rect(rect.x, rect.y, 56f * scale, rect.height), hintTeam == Teams.White ? "백팀" : "흑팀", directionStyle);
                GUI.Label(new Rect(rect.x + 56f * scale, rect.y, rect.width - 60f * scale, rect.height), visibleHint, directionStyle);
            }
            GUI.color = previous;
        }

        // R679 (the user after the 6 vs 6 test: "미니게임 설명 더 확실히 (흑 백 진영 구분 및 미니게임 방법)"): until now
        // a section round told a player only which way its lane was - the rule itself was never shown (the one-line
        // hints were course 01's). For BriefSeconds from walking in: a card at the top with the room, the game, what
        // to do (PawnRushMissions.HowTo) and which side is whose - white's lane and colour, black's lane and colour,
        // this player's own marked "우리 팀". After that the rule stays as one small line under the top edge until
        // the team's gate opens.
        void Briefing(float scale)
        {
            if (game < 0 || game >= PawnRushMissions.Count) return;
            float since = Time.time - briefAt;
            string[] how = PawnRushMissions.HowTo[game].Split(new[] { " · " }, System.StringSplitOptions.None);
            bool full = since < BriefSeconds;
            float alpha = full ? Mathf.Clamp01(since / .25f) * Mathf.Clamp01((BriefSeconds - since) / .5f) : Mathf.Clamp01((since - BriefSeconds) / .4f);
            Color ink = new Color(.96f, .92f, .82f, alpha), brass = new Color(.85f, .7f, .35f, alpha);
            if (!full)
            {
                // the rule in one line, kept in sight
                float w = Mathf.Min(760f * scale, Screen.width - 16f), h = 30f * scale;
                var strip = new Rect((Screen.width - w) * .5f, 64f * scale, w, h);
                GUI.color = new Color(.07f, .09f, .13f, .78f * alpha);
                GUI.DrawTexture(strip, Texture2D.whiteTexture);
                GUI.color = ink;
                briefBody.fontSize = Mathf.RoundToInt(14f * scale);
                briefBody.wordWrap = false;
                GUI.Label(strip, PawnRushMissions.Names[game].Substring(2) + "  ·  " + how[0], briefBody);
                return;
            }
            float cw = Mathf.Min(640f * scale, Screen.width - 16f), ch = 196f * scale;
            var card = new Rect((Screen.width - cw) * .5f, 64f * scale, cw, ch);
            GUI.color = new Color(brass.r, brass.g, brass.b, .9f * alpha);
            GUI.DrawTexture(new Rect(card.x - 2f, card.y - 2f, card.width + 4f, card.height + 4f), Texture2D.whiteTexture);
            GUI.color = new Color(.07f, .09f, .13f, .95f * alpha);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            // the room and the game
            GUI.color = brass;
            briefSmall.fontSize = Mathf.RoundToInt(13f * scale);
            GUI.Label(new Rect(card.x, card.y + 8f * scale, cw, 20f * scale), MiniGameSectionBuilder.Names[game], briefSmall);
            GUI.color = ink;
            briefTitle.fontSize = Mathf.RoundToInt(24f * scale);
            GUI.Label(new Rect(card.x, card.y + 28f * scale, cw, 34f * scale), PawnRushMissions.Names[game].Substring(2), briefTitle);
            // what to do
            briefBody.fontSize = Mathf.RoundToInt(15f * scale);
            briefBody.wordWrap = true;
            GUI.Label(new Rect(card.x + 16f * scale, card.y + 66f * scale, cw - 32f * scale, 24f * scale), how[0], briefBody);
            GUI.color = new Color(ink.r, ink.g, ink.b, .82f * alpha);
            if (how.Length > 1) GUI.Label(new Rect(card.x + 16f * scale, card.y + 90f * scale, cw - 32f * scale, 24f * scale), how[1], briefBody);
            // whose side is which: white's chip and black's, in the order they stand seen from the entrance
            bool shared = PawnRushMissions.Shared(game);
            int leftTeam = OnLeft(Teams.White) ? Teams.White : Teams.Black;
            float gap = 10f * scale, chipW = (cw - 32f * scale - gap) * .5f, chipH = 54f * scale, cy = card.y + 126f * scale;
            for (int side = 0; side < 2; side++)
            {
                int team = side == 0 ? leftTeam : 1 - leftTeam;
                bool mine = team == hintTeam;
                var chip = new Rect(card.x + 16f * scale + side * (chipW + gap), cy, chipW, chipH);
                var colour = Arena.TeamColor(team, true);
                GUI.color = new Color(colour.r, colour.g, colour.b, (mine ? 1f : .45f) * alpha);
                GUI.DrawTexture(chip, Texture2D.whiteTexture);
                GUI.color = new Color(.07f, .09f, .13f, (mine ? .78f : .9f) * alpha);
                float rim = (mine ? 3f : 1f) * scale;
                GUI.DrawTexture(new Rect(chip.x + rim, chip.y + rim, chip.width - 2f * rim, chip.height - 2f * rim), Texture2D.whiteTexture);
                GUI.color = new Color(ink.r, ink.g, ink.b, (mine ? 1f : .7f) * alpha);
                briefSmall.fontSize = Mathf.RoundToInt(16f * scale);
                string who = (team == Teams.White ? "백팀" : "흑팀") + (mine ? "  <b>← 우리 팀</b>" : "  (상대 팀)");
                GUI.Label(new Rect(chip.x, chip.y + 6f * scale, chip.width, 22f * scale), who, briefSmall);
                briefSmall.fontSize = Mathf.RoundToInt(13f * scale);
                string where = shared
                    ? (team == Teams.White ? "가운데 공용 판 · 밝은 칸" : "가운데 공용 판 · 어두운 칸")
                    : (side == 0 ? "입구에서 왼쪽 진영" : "입구에서 오른쪽 진영") + (team == Teams.White ? " · 청록색" : " · 주황색");
                GUI.Label(new Rect(chip.x, chip.y + 28f * scale, chip.width, 20f * scale), where, briefSmall);
            }
        }
    }
}
