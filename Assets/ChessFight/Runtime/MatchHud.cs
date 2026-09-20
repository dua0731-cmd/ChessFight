using UnityEngine;

namespace ChessFight.ProtectKing
{
    public sealed class MatchHud : MonoBehaviour
    {
        public LocalPlaySession session;
        public ProtectTheKingMatchController match;
        GUIStyle title, small, body, center, button;
        string lobbyCode = "";
        readonly Color blue = new Color(.25f,.7f,1);
        readonly Color red = new Color(1,.38f,.42f);
        void Init()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            center = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }

        void Panel(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
        }

        void OnGUI()
        {
            Init();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            var active = session.Active.Identity;
            var online = session.online;
            Panel(new Rect(18,18,width-36,90), new Color(.035f,.045f,.065f,.94f));
            GUI.Label(new Rect(36,26,440,32), "CHESS FIGHT  /  PROTECT THE KING", title);
            GUI.Label(new Rect(36,64,700,22), online == null ? "LOCAL GRAYBOX" : online.Online ? "STEAM ONLINE  |  Humans: " + online.Roster.Count + " / 12  |  AI: " + (12-online.Roster.Count) : "AI PRACTICE  |  1 human + 11 AI", small);
            GUI.color = match.Phase == MatchPhase.Overtime ? new Color(1,.7f,.25f) : Color.white;
            GUI.Label(new Rect(width-230,28,190,38),
                (Mathf.CeilToInt(match.Remaining) / 60).ToString("00") + ":" +
                (Mathf.CeilToInt(match.Remaining) % 60).ToString("00"), center);
            GUI.color = Color.white;
            GUI.Label(new Rect(width-230,68,190,24), match.Phase.ToString().ToUpperInvariant(), small);

            Panel(new Rect(18,120,222,260), new Color(.035f,.045f,.065f,.91f));
            GUI.Label(new Rect(32,130,208,25), online != null && online.Online ? "TEAM ROSTER" : "SELECT PLAYER  [TAB]", body);
            GUI.enabled = online == null || !online.Online;
            for (int i=0; i<6; i++)
            {
                GUI.color = i==active.playerId ? blue : Color.white;
                if (GUI.Button(new Rect(30,166+i*30,98,26), (online != null && online.IsBot(i) ? "AI " : "B ") + (PieceType)i, button)) session.SelectPlayer(i);
                GUI.color = i+6==active.playerId ? red : Color.white;
                if (GUI.Button(new Rect(132,166+i*30,98,26), (online != null && online.IsBot(i+6) ? "AI " : "R ") + (PieceType)i, button)) session.SelectPlayer(i+6);
            }
            GUI.color = Color.white;
            GUI.enabled = true;
            if(online != null) DrawOnline(online,width);
            Panel(new Rect(18,height-103,width-36,85), new Color(.035f,.045f,.065f,.94f));
            GUI.color = active.team == TeamId.Blue ? blue : red;
            GUI.Label(new Rect(34,height-94,280,30), active.DisplayName.ToUpperInvariant(), title);
            GUI.color = Color.white;
            GUI.Label(new Rect(330,height-89,450,26),
                "CP " + active.checkpoint + " / 5    |    Falls / returns: " + active.falls, body);
            GUI.Label(new Rect(34,height-55,width-68,24),
                "WASD / Arrows  Move     SPACE  Jump     RMB + Mouse  Camera     E  Hold at throne     R  Return to checkpoint", small);
            if (match.NoticeUntil > Time.unscaledTime)
                GUI.Label(new Rect(260,120,width-520,36), match.Notice, center);

            bool near = Vector3.Distance(active.transform.position, match.throne.transform.position) < 7;
            if (near && match.IsRunning)
            {
                string prompt = active.piece != PieceType.King ? "Only a KING can claim the throne" :
                    active.checkpoint < 5 ? "Pass every checkpoint before claiming the throne" : "HOLD E  -  CLAIM THE THRONE";
                GUI.Label(new Rect(width/2-280,height-220,560,36), prompt, center);
                Panel(new Rect(width/2-180,height-173,360,14), new Color(.12f,.14f,.18f,.95f));
                Panel(new Rect(width/2-180,height-173,360*match.throne.GetFraction(active.playerId),14), new Color(1,.73f,.25f));
            }
            if (!match.IsRunning)
            {
                Panel(new Rect(width/2-275,height/2-122,550,244), new Color(.025f,.035f,.05f,.97f));
                GUI.Label(new Rect(width/2-260,height/2-100,520,44),
                    match.Phase == MatchPhase.Finished ? match.Result : "PROTECT THE KING", center);
                GUI.Label(new Rect(width/2-240,height/2-48,500,56),
                    "Pass CP1 - CP5 in order. Help your King reach the shared throne.\nKing only: hold E for 1.75 seconds to win.", body);
                GUI.enabled = online == null || online.Authority;
                if (GUI.Button(new Rect(width/2-155,height/2+38,310,46),
                    match.Phase == MatchPhase.Finished ? "RESTART  [ENTER]" : online != null && online.Online ? "HOST: START MATCH [ENTER]" : "START AI PRACTICE [ENTER]", button))
                    { if(online != null)online.StartMatch();else match.StartMatch(); }
                GUI.enabled = true;
            }
        }
        void DrawOnline(OnlineMatchSession online,float width)
        {
            float x=width-322;
            Panel(new Rect(x,122,304,230),new Color(.035f,.045f,.065f,.95f));
            GUI.Label(new Rect(x+12,132,280,24),online.Online ? "STEAM ROOM" : "PLAY WITH FRIENDS",body);
            var steam=online.transport;
            if(!online.Online) {
                GUI.enabled=!steam.Busy;
                if(GUI.Button(new Rect(x+12,165,280,30),"CREATE STEAM ROOM",button))steam.Host();
                lobbyCode=GUI.TextField(new Rect(x+12,204,180,28),lobbyCode,20);
                if(GUI.Button(new Rect(x+200,204,92,28),"JOIN",button) && ulong.TryParse(lobbyCode,out var id))steam.Join(id);
                GUI.enabled=true;
            } else {
                GUI.Label(new Rect(x+12,165,280,24),"Room: "+steam.LobbyId,small);
                if(GUI.Button(new Rect(x+12,199,135,30),"COPY ROOM ID",button))GUIUtility.systemCopyBuffer=steam.LobbyId.ToString();
                if(GUI.Button(new Rect(x+157,199,135,30),"INVITE",button))steam.InviteFriends();
                if(GUI.Button(new Rect(x+12,238,280,28),"LEAVE ROOM",button))steam.Leave();
            }
            GUI.Label(new Rect(x+12,278,280,65),steam.Status,small);
        }
    }
}
