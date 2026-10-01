using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // The last scene (Docs/Architecture/UI.md "결과 화면", REQUIREMENTS R62): every player
    // on the winning team sees the victory ceremony and every player on the losing team
    // the defeat, at the Pawn Rush finish arch, with the result board over it.
    //
    // Opened on its own (ChessFight > Scenes > Last Scene) it previews the design
    // sample's match: F1 / F2 switch whose screen it is, R replays, H hides the board.
    // The match flow does not load it yet - whether the result plays here or at the
    // course's own finish line is still open - so Show() is the way in for whoever
    // decides that, and LobbyRequested / RequeueRequested are the way out.
    [DisallowMultipleComponent]
    public sealed class LastSceneDirector : MonoBehaviour
    {
        public const float WinBoardAt = 3.2f, LoseBoardAt = 3.8f;

        [Tooltip("Preview the losing team's screen first (F1 / F2 switch while playing).")]
        [SerializeField] bool startAsLoser = false;
        [Tooltip("Preview keys and labels, for looking at the design without a match.")]
        [SerializeField] bool preview = true;

        // Raised by the buttons and when the 12 s countdown runs out.
        public event Action LobbyRequested, RequeueRequested;

        MatchResult result;
        int myTeam;
        bool winView, boardOn = true, timeUp;
        Camera view;
        Transform world;
        LastSceneHud hud;
        LastSceneStage stage;
        LastSceneCannons cannons;
        LastSceneConfetti confetti;
        LastSceneCeremony ceremony;
        float startedAt, frameShift, savedShadowDistance = -1;

        // Show `match` as the player on `team` sees it.
        public void Show(MatchResult match, int team)
        {
            result = match;
            myTeam = team == 1 ? 1 : 0;
            if (view != null) Rebuild();
        }

        void Start()
        {
            view = Camera.main;
            if (view == null)
            {
                var host = new GameObject("Main Camera") { tag = "MainCamera" };
                view = host.AddComponent<Camera>();
                host.AddComponent<AudioListener>();
            }
            view.nearClipPlane = .1f;
            // Keep the show running when the window is behind another (as NetworkRuntime does).
            Application.runInBackground = true;
            savedShadowDistance = QualitySettings.shadowDistance;
            QualitySettings.shadowDistance = 90f;

            var hudHost = new GameObject("Last Scene HUD");
            hudHost.transform.SetParent(transform, false);
            hud = hudHost.AddComponent<LastSceneHud>();
            hud.Build(preview);
            hud.ToggleBoard += () => SetBoard(!boardOn);
            hud.Requeue += () => { RequeueRequested?.Invoke(); if (preview) hud.Toast("미리보기라 다시 매칭하지 않아요. 게임에서는 파티 그대로 다음 경기를 찾아요."); };
            hud.Lobby += () => { LobbyRequested?.Invoke(); if (preview) hud.Toast("미리보기라 로비로 가지 않아요. 게임에서는 바로 로비로 가요."); };

            // Nothing passed in: the design sample's match, where white wins.
            if (result == null)
            {
                myTeam = startAsLoser ? 1 : 0;
                result = MatchResult.Example(myTeam);
            }
            Rebuild();
        }

        void Rebuild()
        {
            if (world != null) Destroy(world.gameObject);
            world = new GameObject("Last Scene World").transform;
            world.SetParent(transform, false);
            // A draw has no losers' show; both teams get the sunny stage.
            winView = result.WinningTeam < 0 || result.WinningTeam == myTeam;
            stage = LastSceneStage.Build(world, view, winView);
            if (winView)
            {
                cannons = new LastSceneCannons(world);
                confetti = new LastSceneConfetti(cannons);
                ceremony = new LastSceneVictory(world, result, myTeam);
            }
            else
            {
                cannons = null;
                confetti = null;
                ceremony = new LastSceneDefeat(world, result, myTeam);
            }
            hud.Show(result, myTeam, winView ? WinBoardAt : LoseBoardAt);
            Replay();
        }

        void Replay()
        {
            startedAt = Time.time;
            frameShift = 0f;
            timeUp = false;
        }

        void SetBoard(bool on)
        {
            boardOn = on;
            hud.SetBoard(on);
        }

        void Update()
        {
            if (ceremony == null) return;
            if (preview)
            {
                int winner = result.WinningTeam < 0 ? 0 : result.WinningTeam;
                if (Input.GetKeyDown(KeyCode.F1)) { myTeam = winner; result = MatchResult.Example(myTeam); Rebuild(); }
                if (Input.GetKeyDown(KeyCode.F2)) { myTeam = 1 - winner; result = MatchResult.Example(myTeam); Rebuild(); }
                if (Input.GetKeyDown(KeyCode.R)) Replay();
            }
            if (Input.GetKeyDown(KeyCode.H)) SetBoard(!boardOn);

            float time = Time.time, tp = time - startedAt, boardAt = winView ? WinBoardAt : LoseBoardAt;
            float target = boardOn && tp >= boardAt ? 1f : 0f;
            frameShift += (target - frameShift) * Mathf.Min(1f, Time.deltaTime * 2.6f);
            stage.Update(time, tp);
            cannons?.Update(tp);
            ceremony.Update(time, tp, frameShift, view);
            confetti?.Draw(tp);
            hud.Tick(tp);
            var self = ceremony.Self;
            hud.PlaceMarker(self != null ? self.Root.transform.TransformPoint(new Vector3(0, 2.45f, 0)) : Vector3.zero, view, self != null);
            if (!timeUp && tp >= boardAt + LastSceneHud.LobbySeconds)
            {
                timeUp = true;
                LobbyRequested?.Invoke();
                if (preview) hud.Toast("12초가 지나 게임이라면 로비로 가요. 미리보기에서는 여기 머물러요 (R 다시 재생).");
            }
        }

        void OnDestroy()
        {
            if (savedShadowDistance >= 0) QualitySettings.shadowDistance = savedShadowDistance;
        }
    }
}
