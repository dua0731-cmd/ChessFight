using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.RagdollLab
{
    // Opt-in player smoke test. Automated evidence is not a human playtest.
    public sealed class SwordFightAutoTest : MonoBehaviour
    {
        int passed, failed;
        SwordFightGame game;
        void Check(bool condition, string name)
        {
            if (condition) { passed++; Debug.Log("[SwordFightTest] PASS " + name); }
            else { failed++; Debug.LogError("[SwordFightTest] FAIL " + name); }
        }
        IEnumerator Start()
        {
            game = GetComponent<SwordFightGame>();
            yield return TestLobbyHud();
            yield return Run();
            Debug.Log($"[SwordFightTest] RESULT {passed} passed / {failed} failed");
            Application.Quit(failed == 0 ? 0 : 1);
        }
        IEnumerator Run()
        {
            var a = game.Add(1, 0, 0, false, "White test pawn");
            var b = game.Add(2, 1, 0, true, "Black test dummy");
            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back);
            yield return new WaitForSeconds(1.5f);
            var document = game.GetComponent<UIDocument>();
            var score = document == null ? null : document.rootVisualElement.Q<Label>("sf-score");
            Check(score != null && score.text.Contains("백팀") && score.worldBound.width > 100 && score.worldBound.height > 20,
                "UI Toolkit score layout is loaded and sized");
            a.SetInput(new PawnInput { shove = true, grab = true, ability = true, ability2 = true, interact = true, aim = Vector3.forward });
            var colliders = b.Pawn.GetComponentsInChildren<Collider>();
            float closest = float.MaxValue;
            for (int step = 0; step < 48; step++)
            {
                yield return new WaitForFixedUpdate();
                if (a.SwingAge < SwordFightPawn.Windup || a.SwingAge > SwordFightPawn.Windup + SwordFightPawn.ActiveTime) continue;
                foreach (var c in colliders)
                    for (int sample = 0; sample <= 8; sample++)
                    {
                        var point = Vector3.Lerp(a.BladeRoot, a.BladeTip, sample / 8f);
                        closest = Mathf.Min(closest, Vector3.Distance(point, c.ClosestPoint(point)));
                    }
                if (step % 6 == 0) Debug.Log($"[SwordTrace] age={a.SwingAge:0.00} root={a.BladeRoot:F2} tip={a.BladeTip:F2} target={b.Pawn.Hips.position:F2} minGap={closest:0.000}");
            }
            Check(a.Swings == 1 && !a.Pawn.Diving && !a.Pawn.Grabbing && !a.Pawn.Climbing, "Mode input: sword replaces tackle/grab/hook");
            Check(a.HitsLanded == 1 && b.Pawn.Hits == 1 && b.Pawn.State == PawnState.Ragdoll, $"Blade sweep hits one target once and knocks it down (landed={a.HitsLanded}, hits={b.Pawn.Hits}, state={b.Pawn.State})");
            string dir = Path.Combine(Application.persistentDataPath, "SwordFightTest"); Directory.CreateDirectory(dir);
            Capture(Path.Combine(dir, "swing.png"));
            yield return new WaitForSeconds(.5f);
            Vector3 target = b.Pawn.Hips.position;
            Place(a, new Vector3(target.x, 0, target.z - .85f), Vector3.forward);
            b.Pawn.TakeHit(Vector3.zero, 2, 0, true);
            yield return new WaitForSeconds(.15f);
            int previousHits = b.Pawn.Hits;
            bool downBefore = b.Pawn.State == PawnState.Ragdoll;
            // Pursue the sliding victim instead of testing a stationary swing after it has left reach.
            for (int step = 0; step < 50; step++)
            {
                Vector3 toward = b.Pawn.Hips.position - a.Pawn.Hips.position; toward.y = 0;
                a.SetInput(new PawnInput { shove = step == 0, move = toward.normalized, aim = toward.normalized });
                yield return new WaitForFixedUpdate();
            }
            a.SetInput(default);
            Check(downBefore && b.Pawn.Hits == previousHits + 1 && a.HitsLanded == 2, $"Pursuit swing may hit a downed enemy again (hits={b.Pawn.Hits - previousHits}, swings={a.Swings}, a={a.Pawn.State}, gap={Vector3.Distance(a.Pawn.Hips.position,b.Pawn.Hips.position):0.00})");

            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back); b.Pawn.Team = 0;
            yield return new WaitForSeconds(1.5f);
            previousHits = b.Pawn.Hits;
            a.SetInput(new PawnInput { shove = true, aim = Vector3.forward });
            yield return new WaitForSeconds(.75f);
            Check(b.Pawn.Hits == previousHits, "No sword friendly fire"); b.Pawn.Team = 1;

            b.Pawn.Teleport(new Vector3(0, -4, 0), Vector3.back);
            game.StepRound(.01f); game.StepRound(.01f);
            Check(game.White == 1 && !b.Alive, "Ring-out scores exactly once");
            game.StepRound(2.9f); Check(!b.Alive, "Respawn waits three seconds");
            game.StepRound(.11f);
            Check(b.Alive && b.Protection > 1 && b.Pawn.Hips.position.y > 0 && b.Pawn.State == PawnState.Active, "Respawn restores the pawn and temporary spawn protection");
            yield return null; yield return new WaitForEndOfFrame();
            Check(b.WeaponVisible, "Respawn restores inactive sword renderers too");
            b.ApplyRemote(false, 0, SwordFightPawn.SwingDuration, 0, Vector3.back, 3);
            yield return null; yield return new WaitForEndOfFrame();
            b.ApplyRemote(true, 1, SwordFightPawn.SwingDuration, 0, Vector3.back, 0);
            yield return null; yield return new WaitForEndOfFrame();
            Check(b.WeaponVisible, "Remote dead-to-alive state restores the sword");
            b.Authority = true;
            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back);
            b.Respawn(new Vector3(0, 0, .45f), Vector3.back, true);
            previousHits = b.Pawn.Hits;
            a.SetInput(new PawnInput { shove = true, aim = Vector3.forward });
            yield return new WaitForSeconds(.5f);
            Check(b.Pawn.Hits == previousHits, "Spawn protection rejects sword hits");

            Place(a, new Vector3(0, 0, -2), Vector3.forward);
            yield return new WaitForSeconds(.15f);
            a.Pawn.SetInput(new PawnInput { shove = true });
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(a.Pawn.Diving, "Shared pawn LMB still dives when not intercepted");
            Place(a, new Vector3(0, 0, -2), Vector3.forward);
            Vector3 before = a.Pawn.Hips.position;
            for (int i = 0; i < 100; i++) { a.SetInput(new PawnInput { move = Vector3.right, sprint = true }); yield return new WaitForFixedUpdate(); }
            Check(a.Pawn.Hips.position.x > before.x + 1 && !a.Pawn.Diving, "Shared sprint movement still runs in Sword Fight");
            a.SetInput(default);
            // A close enemy must not trigger the old pursuit/attack AI. Physics
            // idle sway is allowed; zero displacement would require freezing it.
            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back);
            yield return new WaitForSeconds(1.5f);
            Vector3 dummyStart = b.Pawn.Hips.position;
            int dummySwings = b.Swings, received = a.Pawn.Hits;
            for (int i = 0; i < 480; i++) { game.StepRound(Time.fixedDeltaTime); yield return new WaitForFixedUpdate(); }
            Vector3 drift = b.Pawn.Hips.position - dummyStart; drift.y = 0;
            Check(b.Swings == dummySwings && a.Pawn.Hits == received && drift.magnitude < .25f,
                $"Nearby dummy neither pursues nor attacks (drift={drift.magnitude:0.000})");
            b.Pawn.Teleport(new Vector3(0, -4, 0), Vector3.back);
            int previousScore = game.White; game.StepRound(.01f); game.StepRound(3.01f);
            Check(game.White == previousScore + 1 && b.Alive && b.Bot, "Dummy ring-out and respawn keep the dummy identity");
            Place(a, SwordFightGame.SpawnPoint(0, 0), Vector3.forward);
            Place(b, SwordFightGame.SpawnPoint(1, 0), Vector3.back);
            for (ulong id = 3; id <= 12; id++) game.Add(id, (int)(id % 2), (int)((id - 1) / 2), true, "Bot " + id);
            for (int i = 0; i < 720; i++) { game.StepRound(Time.fixedDeltaTime); yield return new WaitForFixedUpdate(); }
            bool finite = true; int attacks = 0;
            foreach (var f in game.Fighters.Values) { finite &= f.Pawn.IsFinite(); if (f.Bot) attacks += f.Swings; }
            Check(game.Fighters.Count == 12 && finite && attacks == 0, "Twelve-pawn dummy room remains finite without AI attacks");
            Capture(Path.Combine(dir, "brawl.png"));
            yield return new WaitForSeconds(.2f);
            Debug.Log("[SwordFightTest] screenshots: " + dir);
        }
        IEnumerator TestLobbyHud()
        {
            var obj = new GameObject("Test lobby HUD");
            var view = obj.AddComponent<ChessFight.Game.NetworkHudView>();
            view.Build(Resources.Load<VisualTreeAsset>("NetworkHud"), Resources.Load<ThemeStyleSheet>("NetworkTheme"), null, new Vector2Int(1280, 720));
            view.Render(new ChessFight.Game.HudModel {
                Online = true, ModeKey = "swordfight", Busy = true, ShowMatch = true, ShowRoomTools = true,
                ShowTestBots = true, ShowStart = true, CanStart = true, CanAddAlly = true, CanAddEnemy = true,
                CanRemoveEnemy = true, EnemyDummies = 1, OursFilled = 1, TheirsFilled = 1, PartySize = 1,
                MatchTitle = "더미를 추가하고 시작하세요", MatchKicker = "PRIVATE ROOM · 소드 파이트",
                MatchNote = "12명 없이 시작 가능", MatchTimer = "00:03", RoomCode = "방 번호  10 9775 2430 2300 4055",
                TestHint = "이동·공격 없는 더미 · 팀당 최대 6명 (나 포함)\n준비되면 오른쪽 아래 테스트 시작을 누르세요.",
                BusyTitle = "비공개 방", BusySub = "00:03 · 2/12", PartyCode = "10 9775 2430 2300 4055", Bots = "0"
            });
            yield return null; yield return new WaitForEndOfFrame();
            var root = obj.GetComponent<UIDocument>().rootVisualElement;
            var panel = root.Q<VisualElement>("test-bots");
            bool layout = panel != null && panel.worldBound.width > 300 && panel.worldBound.height > 60;
            foreach (string name in new[] { "ally-less", "ally-more", "enemy-less", "enemy-more", "start" })
            {
                var button = root.Q<Button>(name);
                layout &= button != null && button.worldBound.width > 20 && button.worldBound.height > 20 &&
                    root.worldBound.Contains(button.worldBound.center);
            }
            Check(layout && root.Q<Label>("enemy-dummies").text == "1" && !root.Q<Button>("ally-less").enabledSelf &&
                root.Q<Button>("enemy-more").enabledSelf && root.Q<Button>("start").enabledSelf,
                "Lobby dummy controls are visible, sized and reflect limits");
            int clicked = 0;
            view.AddEnemyDummy += () => clicked++;
            var add = root.Q<Button>("enemy-more");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = add; add.SendEvent(submit); }
            yield return null;
            Check(clicked == 1, "Lobby enemy-plus dispatches exactly one UI action");
            // Render the UI itself: a camera capture excludes screen-space panels.
            var settings = obj.GetComponent<UIDocument>().panelSettings;
            var uiTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            uiTexture.Create(); settings.targetTexture = uiTexture;
            yield return null; yield return new WaitForEndOfFrame();
            var oldActive = RenderTexture.active; RenderTexture.active = uiTexture;
            var uiImage = new Texture2D(uiTexture.width, uiTexture.height, TextureFormat.RGBA32, false);
            uiImage.ReadPixels(new Rect(0, 0, uiTexture.width, uiTexture.height), 0, 0); uiImage.Apply();
            string dir = Path.Combine(Application.persistentDataPath, "SwordFightTest"); Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "dummy-lobby.png"), uiImage.EncodeToPNG());
            RenderTexture.active = oldActive; settings.targetTexture = null;
            Destroy(uiImage); uiTexture.Release(); Destroy(uiTexture);
            Destroy(obj); yield return null;
        }
        static void Place(SwordFightPawn f, Vector3 floor, Vector3 facing)
        {
            f.Pawn.Teleport(floor + Vector3.up * (f.Pawn.standHeight + .02f), facing);
            f.SetInput(default);
        }
        void Capture(string path)
        {
            // Hidden test windows do not reliably present a backbuffer; render the game camera explicitly.
            var camera = game.CameraRig.GetComponent<Camera>();
            var rt = RenderTexture.GetTemporary(1280, 720, 24);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Destroy(image);
        }
    }
}
