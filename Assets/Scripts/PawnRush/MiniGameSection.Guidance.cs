using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public sealed partial class MiniGameSection
    {
        readonly MgCheckmate[] teamHunts = new MgCheckmate[2];
        GUIStyle directionStyle;
        string visibleHint;
        int hintTeam;

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

        void LateUpdate()
        {
            var driver = MiniGameBase.LocalDriver;
            var target = driver?.FollowTarget;
            hintTeam = MissionStation.LocalTeam;
            visibleHint = target != null && !GameMenu.IsOpen ? DirectionHint(hintTeam, target.position) : null;
            if (visibleHint != null && StationPromotion.ChoosingFor(driver) != null) visibleHint = null;
        }

        void OnGUI()
        {
            if (visibleHint == null || Event.current.type != EventType.Repaint) return;
            if (directionStyle == null)
                directionStyle = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            float scale = Mathf.Clamp(Screen.height / 900f, .72f, 1.8f);
            float width = Mathf.Min(390f * scale, Screen.width - 8f);
            var rect = new Rect((Screen.width - width) * .5f, Screen.height - 128f * scale, width, 36f * scale);
            var previous = GUI.color;
            GUI.color = new Color(.07f, .09f, .13f, .9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Arena.TeamColor(hintTeam, true);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 4f * scale, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            directionStyle.fontSize = Mathf.RoundToInt(17f * scale);
            GUI.Label(new Rect(rect.x, rect.y, 56f * scale, rect.height), hintTeam == Teams.White ? "백팀" : "흑팀", directionStyle);
            GUI.Label(new Rect(rect.x + 56f * scale, rect.y, rect.width - 60f * scale, rect.height), visibleHint, directionStyle);
            GUI.color = previous;
        }
    }
}
