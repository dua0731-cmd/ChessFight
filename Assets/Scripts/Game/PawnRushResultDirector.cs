using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // The Pawn Rush result scenes (Docs/Architecture/UI.md §13, REQUIREMENTS R65):
    // PawnRushVictory.unity for the team that won and PawnRushLose.unity for the team that
    // lost, the same director with one switch. The stage (a chessboard on a table under a
    // hanging lamp) and the broadcast package over it are built from code when Play starts.
    //
    // Opened on its own it shows the design sample's match (MatchResult.Example): R replays,
    // H hides the package. The match flow does not load these scenes yet; Show() is the way
    // in for whoever wires that, and LobbyRequested / RequeueRequested are the way out.
    [DisallowMultipleComponent]
    public sealed class PawnRushResultDirector : MonoBehaviour
    {
        [Tooltip("Victory scene (true) or the losing team's scene (false).")]
        [SerializeField] bool victory = true;
        [Tooltip("Preview keys and labels, for looking at the design without a match.")]
        [SerializeField] bool preview = true;
        [Tooltip("Preview only: hold the show at this many seconds (negative plays it). For looking at one moment.")]
        [SerializeField] float holdAt = -1f;
        [Tooltip("Preview only: start with the result package hidden, as if H was pressed.")]
        [SerializeField] bool holdHidden = false;

        // Raised by the buttons and when the 12 s countdown runs out.
        public event Action LobbyRequested, RequeueRequested;

        MatchResult result;
        int myTeam;
        bool boardOn = true, timeUp, hiddenApplied;
        Camera view;
        Transform world;
        PawnRushResultHud hud;
        PawnRushResultStage stage;
        float startedAt, savedShadowDistance = -1;

        // Show `match` as the player on `team` sees it.
        public void Show(MatchResult match, int team)
        {
            result = match;
            myTeam = team == 1 ? 1 : 0;
            if (view != null) Rebuild();
        }

        // Preview only, the same as setting holdAt and holdHidden in the Inspector.
        public void Hold(float seconds, bool hidden)
        {
            holdAt = seconds;
            holdHidden = hidden;
        }

        float BoardAt => victory ? PawnRushResultStage.WinBoardAt : PawnRushResultStage.LoseBoardAt;

        void Start()
        {
            view = Camera.main;
            if (view == null)
            {
                var host = new GameObject("Main Camera") { tag = "MainCamera" };
                view = host.AddComponent<Camera>();
                host.AddComponent<AudioListener>();
            }
            // Keep the show running when the window is behind another (as NetworkRuntime does).
            Application.runInBackground = true;
            savedShadowDistance = QualitySettings.shadowDistance;
            QualitySettings.shadowDistance = 90f;

            var hudHost = new GameObject("Result HUD");
            hudHost.transform.SetParent(transform, false);
            hud = hudHost.AddComponent<PawnRushResultHud>();
            hud.Build(preview);
            hud.ToggleBoard += () => SetBoard(!boardOn);
            hud.Requeue += () => { RequeueRequested?.Invoke(); if (preview) hud.Toast("미리보기라 다시 매칭하지 않아요. 게임에서는 파티 그대로 다음 경기를 찾아요."); };
            hud.Lobby += () => { LobbyRequested?.Invoke(); if (preview) hud.Toast("미리보기라 로비로 가지 않아요. 게임에서는 바로 로비로 가요."); };

            // Nothing passed in: the design sample's match, where white wins.
            if (result == null)
            {
                myTeam = victory ? 0 : 1;
                result = MatchResult.Example(myTeam);
            }
            Rebuild();
        }

        void Rebuild()
        {
            if (world != null) Destroy(world.gameObject);
            world = new GameObject("Result World").transform;
            world.SetParent(transform, false);
            stage = new PawnRushResultStage(world, view, result, myTeam, victory);
            hud.Show(result, myTeam, victory, BoardAt);
            Replay();
        }

        void Replay()
        {
            startedAt = Time.time;
            timeUp = false;
            stage.Reset();
            hiddenApplied = preview && holdHidden;
            SetBoard(!hiddenApplied);
        }

        void SetBoard(bool on)
        {
            boardOn = on;
            hud.SetBoard(on);
        }

        void Update()
        {
            if (stage == null) return;
            if (preview && Input.GetKeyDown(KeyCode.R)) Replay();
            // A change to holdHidden in the Inspector while playing hides or shows the package.
            if (preview && holdHidden != hiddenApplied) { hiddenApplied = holdHidden; SetBoard(!holdHidden); }
            float tp = preview && holdAt >= 0 ? holdAt : Time.time - startedAt;
            if (Input.GetKeyDown(KeyCode.H) && tp >= BoardAt) SetBoard(!boardOn);

            stage.Update(Time.time, tp, boardOn);
            hud.Tick(tp);
            hud.PlaceMarker(stage.SelfHead, view, stage.HasSelf && tp > .6f);
            if (!timeUp && tp >= BoardAt + PawnRushResultHud.LobbySeconds)
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
