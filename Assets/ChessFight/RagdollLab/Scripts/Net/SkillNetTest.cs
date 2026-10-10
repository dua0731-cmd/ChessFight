using System.Text;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skill network test screen (R112, branch skill-network-test). The skill test beds add it and switch the lab into
    /// <see cref="LabGame.NetTestMode"/>: the lab's own tools (tuning panel, slow motion, free camera, split screen, the [7]
    /// test bed) are gone, and this draws instead
    /// <list type="bullet">
    /// <item>a short key line at the top left (what is left to press);</item>
    /// <item>the numbers at the top right, always on: the link (round trip, playback delay, jitter, loss, snapshots, bytes,
    /// the lag simulator), what the skills cost (skills packets a second, their size), how long a press takes to come back
    /// from the host, the press guesses, the skill moments (how far from their pose they were drawn, how long they waited,
    /// repeats, missed, dropped), the hit stops; on the host, every player's line as that player measures it.</item>
    /// </list>
    /// Green is within the R96 bar (D-S9: 150 ms round trip, 5% loss), yellow up to 300 ms and 10% (it must hold up),
    /// red past that. While no match is on, the online panel (F3) is open to make or join one.
    /// </summary>
    [DefaultExecutionOrder(220)]
    public class SkillNetTest : MonoBehaviour
    {
        LabGame game;
        GUIStyle line, head, hint;
        Texture2D panel;
        bool openedPanel;
        readonly StringBuilder sb = new StringBuilder(1024);

        static readonly Color Good = new Color(0.55f, 0.95f, 0.6f), Fair = new Color(1f, 0.85f, 0.35f), Bad = new Color(1f, 0.45f, 0.4f);

        void Awake()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
            if (game != null) game.NetTestMode = true;
        }

        void Update()
        {
            // Nothing to test until a match is on: show how to make or join one (once; F3 closes it).
            var link = LabNetLink.Current;
            if (!openedPanel && link != null && !link.MatchActive && game != null && !game.AutoTest)
            {
                link.PanelShown = true;
                openedPanel = true;
            }
        }

        void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }

        void EnsureStyles()
        {
            if (line != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 13);
            // Wrapped: a long line (a player on the host's list) goes on to the next line instead of off the box.
            line = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, richText = true, wordWrap = true };
            line.normal.textColor = new Color(0.9f, 0.92f, 0.95f);
            head = new GUIStyle(line) { fontSize = 14, fontStyle = FontStyle.Bold };
            hint = new GUIStyle(line) { fontSize = 13, wordWrap = false };
            panel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            panel.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.1f, 0.84f));
            panel.Apply();
        }

        void OnGUI()
        {
            if (game == null || game.AutoTest) return;
            EnsureStyles();
            DrawHint();
            DrawNumbers();
        }

        // ---------------------------------------------------------------- the key line

        void DrawHint()
        {
            var link = LabNetLink.Current;
            bool online = link != null && link.MatchActive;
            string text = online
                ? "스킬 네트워크 시험   ·   F3 온라인 창   ·   F12 지연 시뮬레이터   ·   Esc 마우스 풀기"
                : "스킬 네트워크 시험   ·   F3 온라인 창: Steam 방 만들기·참가 또는 로컬 시험(이 PC 두 창)   ·   R 리스폰   ·   Esc 마우스 풀기";
            if (Cursor.lockState != CursorLockMode.Locked && !game.UiWantsCursor)
                text = "▶ 화면을 클릭하면 마우스 조작(시점 · 좌클릭 · 우클릭)이 켜져요     " + text;
            var size = hint.CalcSize(new GUIContent(text));
            GUI.DrawTexture(new Rect(8, 8, size.x + 16, size.y + 8), panel);
            GUI.Label(new Rect(16, 12, size.x, size.y), text, hint);
        }

        // ---------------------------------------------------------------- the numbers

        void DrawNumbers()
        {
            var link = LabNetLink.Current;
            sb.Clear();
            if (link == null || !link.MatchActive)
            {
                sb.Append("<b>네트워크: 연결 안 됨</b>\n");
                sb.Append("F3 → Steam 테스트 방 만들기 / 방 번호로 참가 → 시작\n");
                sb.Append("PC 한 대: Unity에서 \"로컬 시험: 방장\", 빌드에서 \"로컬 시험: 참가\"\n");
                sb.Append("연결되면 여기에 왕복 지연·재생 지연·스킬 반응 시간이 나와요");
                Box(sb.ToString(), 430f);
                return;
            }
            if (link.IsHost) HostNumbers(link);
            else ClientNumbers(link);
            Box(sb.ToString(), 470f);
        }

        void ClientNumbers(LabNetLink link)
        {
            sb.Append($"<b>네트워크 · 참가자 ({link.CarrierName}) · {link.PlayerCount}명</b>\n");
            sb.Append($"왕복 지연 {Ms(link.RoundTripMs, 150, 300)}   재생 지연 {link.PlaybackDelayMs:0} ms");
            sb.Append($" (흔들림 {link.JitterMs:0} ms · 손실 {Pct(link.LossShare)}{(link.LossWait ? " · 한 장 더" : "")})\n");
            sb.Append($"스냅샷 {Rate(link.SnapshotsPerSecond, 28, 24)}개/초 · {link.SnapshotBytes} 바이트 · 버퍼 {link.BufferedSnapshots}장\n");
            sb.Append($"보내기 {link.SentPerSecond / 1024f:0.0} KB/s · 받기 {link.ReceivedPerSecond / 1024f:0.0} KB/s · 스킬 {link.SkillBytesPerSecond / 1024f:0.0} KB/s\n");
            sb.Append($"스킬 패킷 {link.SkillPacketsPerSecond}개/초 · 평균 {link.SkillPacketAverage} · 최대 {link.SkillPacketLargest} 바이트\n");
            sb.Append($"지연 시뮬레이터(F12): {link.Lag}\n");

            sb.Append("\n<b>내 스킬</b>\n");
            float avg = link.AverageReactionMs;
            sb.Append($"누름 → 방장 반응: 마지막 {Ms(link.LastReactionMs, 200, 400)} · 평균 {Ms(avg, 200, 400)}");
            sb.Append(link.ReactionsLost > 0 ? $" · 반응 없음 {link.ReactionsLost}번\n" : "\n");
            if (avg >= 0f) sb.Append($"내 몸이 움직여 보이기까지 약 {avg + (float)link.PlaybackDelayMs:0} ms (반응 + 재생 지연)\n");
            sb.Append($"스킬 키 {link.SkillPresses}번 → 방장에서 시작 {link.SkillStarts}번\n");
            sb.Append($"미리 그림 {RagdollPawn.NetStats.Guesses}번 · 방장과 맞음 {RagdollPawn.NetStats.GuessesMatched} · 되돌림 {RagdollPawn.NetStats.GuessesDropped}\n");

            sb.Append("\n<b>스킬 신호</b> (맞음 · 폭발 · 착지)\n");
            float late = link.MomentLateMs, wait = link.MomentWaitMs;
            sb.Append($"받아 그림 {link.EventsPlayed}개");
            if (late > -0.5f || link.EventsPlayed > 0) sb.Append($" · 몸과 차이 {Ms(late, 20, 60)} · 받고 기다림 {wait:0} ms");
            sb.Append("\n");
            sb.Append($"중복 거름 {link.MomentsRepeated} · 놓침 {Count(link.MomentsMissed)} · 늦어서 버림 {Count(link.MomentsStale)} · 대기 {link.MomentsQueued}\n");
            sb.Append($"히트스톱(내 화면만) {RagdollPawn.NetStats.HitStops}번" + (RagdollPawn.NetStats.HitStops > 0 ? $" · 마지막 {RagdollPawn.NetStats.LastHitStopFrames}장면" : ""));
        }

        void HostNumbers(LabNetLink link)
        {
            sb.Append($"<b>네트워크 · 방장 ({link.CarrierName}) · {link.PlayerCount}명</b>\n");
            sb.Append("이 PC가 물리와 스킬을 판정해요 (내 입력 지연 0)\n");
            sb.Append($"스냅샷 보냄 {Rate(link.SnapshotsSentPerSecond, 28, 24)}개/초 · {link.SnapshotBytes} 바이트 · 보내기 {link.SentPerSecond / 1024f:0.0} KB/s · 받기 {link.ReceivedPerSecond / 1024f:0.0} KB/s\n");
            sb.Append($"스킬 패킷 {link.SkillPacketsPerSecond}개/초 · 평균 {link.SkillPacketAverage} · 최대 {link.SkillPacketLargest} 바이트 · {link.SkillBytesPerSecond / 1024f:0.0} KB/s\n");
            sb.Append($"보관 중인 스킬 신호 {link.MomentsLogged}개 · 지연 시뮬레이터(F12): {link.Lag}\n");

            sb.Append("\n<b>참가자</b> (참가자 PC가 잰 값)\n");
            if (link.Peers.Count == 0) sb.Append("아직 없음\n");
            foreach (var p in link.Peers)
            {
                sb.Append($"{p.Name}: ");
                if (p.SilentMs > 500f)
                {
                    sb.Append($"<color=#ff7468>입력이 {p.SilentMs / 1000f:0.0}초째 없음</color>\n");
                    continue;
                }
                sb.Append($"왕복 {Ms(p.RoundTripMs, 150, 300)} · 재생 {p.DelayMs} ms · 손실 {LossPct(p.LossPercent)}");
                sb.Append($" · 입력 {Rate(p.InputRate, 55, 45)}/초 · 스킬 키 {p.SkillPresses}번 · 미확인 신호 {p.Unconfirmed}\n");
            }
            sb.Append($"\n히트스톱(내 화면만) {RagdollPawn.NetStats.HitStops}번" + (RagdollPawn.NetStats.HitStops > 0 ? $" · 마지막 {RagdollPawn.NetStats.LastHitStopFrames}장면" : ""));
        }

        // ---------------------------------------------------------------- drawing

        void Box(string text, float width)
        {
            var content = new GUIContent(text);
            float height = line.CalcHeight(content, width - 20f) + 14f;
            var r = new Rect(Screen.width - width - 8f, 8f, width, height);
            GUI.DrawTexture(r, panel);
            GUI.Label(new Rect(r.x + 10f, r.y + 7f, width - 20f, height - 14f), content, line);
        }

        static string Paint(string text, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";

        /// <summary>A time in ms, green up to <paramref name="good"/>, yellow up to <paramref name="fair"/>, red past it.</summary>
        static string Ms(float ms, float good, float fair)
        {
            if (ms < 0f) return "—";
            return Paint($"{ms:0} ms", ms <= good ? Good : ms <= fair ? Fair : Bad);
        }

        static string Pct(double share)
        {
            double p = share * 100d;
            return Paint($"{p:0.0}%", p < 5d ? Good : p < 10d ? Fair : Bad);
        }

        static string LossPct(int percent) => percent < 0 ? "—" : Paint($"{percent}%", percent < 5 ? Good : percent < 10 ? Fair : Bad);

        static string Rate(int perSecond, int good, int fair) => Paint(perSecond.ToString(), perSecond >= good ? Good : perSecond >= fair ? Fair : Bad);

        static string Count(int n) => n == 0 ? Paint("0", Good) : Paint(n.ToString(), Bad);
    }
}
