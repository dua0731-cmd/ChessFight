using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.RagdollLab
{
    // Opt-in player smoke test. Automated evidence is not a human playtest.
    public sealed partial class SwordFightAutoTest : MonoBehaviour
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
            string dir = Path.Combine(Application.persistentDataPath, "SwordFightTest"); Directory.CreateDirectory(dir);
            var score = game.GetComponent<UIDocument>().rootVisualElement.Q<Label>("sf-score");
            Check(score != null && score.text.Contains("백팀") && score.worldBound.width > 100, "Score HUD is visible");
            Check(!a.Drawn && a.SwordBody.isKinematic && a.WeaponVisible, "Idle sword is holstered without active physics");
            Capture(Path.Combine(dir, "holstered.png"));
            float normalSensitivity = game.CameraRig.mouseSensitivity;
            var input = new PawnInput { shoveHeld = true, aim = Look(-65, 0), grab = true, ability = true, shove = true };
            var bytes = RagdollNetProtocol.Input(1, 2, 3, RagdollNetInput.From(input));
            Check(RagdollNetProtocol.ReadInput(bytes, 1, out _, out _, out var decoded) && decoded.shoveHeld &&
                Vector3.Angle(decoded.aim, input.aim) < 1f, "CFR4 carries held sword and pitched aim");
            a.SetInput(input);
            yield return new WaitForSeconds(.6f);
            Check(a.Drawn && !a.SwordBody.isKinematic && !a.Pawn.Diving && !a.Pawn.Grabbing && !a.Pawn.Climbing,
                "Holding LMB draws a physical sword without shared tackle/grab/hook");
            Check(a.HitsLanded == 0 && b.Pawn.Hits == 0, "Drawing and holding still do not deal sword hits");
            Capture(Path.Combine(dir, "guard.png"));
            yield return Drag(a, -85, 45, 0, .32f, dir);
            Check(a.HitsLanded == 1 && b.Pawn.Hits == 1,
                $"Real blade drag hits once (landed={a.HitsLanded}, state={b.Pawn.State})");
            int received = b.Pawn.Hits;
            yield return new WaitForSeconds(.75f);
            Check(b.Pawn.Hits == received, "A stopped held blade does not repeatedly damage");
            a.SetInput(default); yield return new WaitForFixedUpdate();
            Check(!a.Drawn && a.SwordBody.isKinematic, "Release sheathes and disables sword physics");

            // A real second low cut can hit an already downed victim. No test-only hit dispatch.
            Vector3 target = b.Pawn.Hips.position;
            Place(a, new Vector3(target.x - .2f, 0, target.z - .65f), Vector3.forward);
            b.Pawn.TakeHit(Vector3.zero, 3, 0, true);
            bool wasDown = b.Pawn.State == PawnState.Ragdoll;
            received = b.Pawn.Hits;
            a.SetInput(new PawnInput { shoveHeld = true, aim = Look(-60, 55) });
            yield return new WaitForSeconds(.5f);
            yield return Drag(a, -80, 50, 55, .32f, dir, "low");
            Check(wasDown && b.Pawn.Hits > received, $"Downed enemy permits a physical follow-up (extra={b.Pawn.Hits-received})");

            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back); b.Pawn.Team = 0;
            yield return new WaitForSeconds(1.5f); received = b.Pawn.Hits;
            a.SetInput(new PawnInput { shoveHeld = true, aim = Look(-85, 0) });
            yield return new WaitForSeconds(.5f); yield return Drag(a, -85, 45, 0, .32f);
            Check(b.Pawn.Hits == received, "No sword friendly fire"); b.Pawn.Team = 1;
            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back);
            a.SetInput(new PawnInput { shoveHeld = true, aim = Look(-85, 0) });
            yield return new WaitForSeconds(.35f); received = b.Pawn.Hits;
            yield return Drag(a, -85, 45, 0, .32f);
            Check(b.Pawn.Hits == received && b.Protection > 0, "Spawn protection rejects sword hits");
            a.StopCombat();
            b.Pawn.Teleport(new Vector3(0, -4, 0), Vector3.back);
            game.StepRound(.01f); game.StepRound(.01f);
            Check(game.White == 1 && !b.Alive && !b.Drawn, "Ring-out scores once and disables weapon");
            game.StepRound(2.9f); Check(!b.Alive, "Respawn waits three seconds");
            game.StepRound(.11f); yield return null; yield return new WaitForEndOfFrame();
            Check(b.Alive && b.Protection > 1 && b.WeaponVisible && b.SwordBody.isKinematic, "Respawn restores protected pawn with holstered sword");
            b.ApplyRemote(false, false, 0, Vector3.zero, Quaternion.identity, 3, 0);
            yield return null;
            b.ApplyRemote(true, true, 0, new Vector3(.3f, .2f, .4f), Quaternion.Euler(0, 50, 0), 0, .1f);
            yield return null; yield return new WaitForEndOfFrame();
            Check(b.WeaponVisible && b.SwordBody.isKinematic && Vector3.Distance(b.WeaponOffset, new Vector3(.3f,.2f,.4f)) < .01f,
                "Remote sword follows replicated relative pose without authority physics");
            Place(b, new Vector3(0, 0, 3), Vector3.back);

            Check(game.CameraRig.mouseSensitivity == normalSensitivity, "Sword combat leaves normal camera sensitivity unchanged");

            Place(a, new Vector3(0, 0, -3), Vector3.forward);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Physical sword test wall";
            wall.transform.position = new Vector3(0, 1, -2.45f); wall.transform.localScale = new Vector3(4, 2, .16f);
            a.SetInput(new PawnInput { shoveHeld = true, aim = Look(-90, 20) });
            yield return new WaitForSeconds(.5f);
            yield return Drag(a, -90, 0, 20, .75f);
            yield return new WaitForSeconds(.4f);
            float front = wall.GetComponent<Collider>().bounds.min.z;
            bool wouldCross = a.BladeRoot.z + .75f * Mathf.Cos(35 * Mathf.Deg2Rad) > front + .1f;
            Check(wouldCross && a.BladeTip.z < front + .1f && a.Pawn.IsFinite(),
                $"Physical blade is blocked by a wall (tip={a.BladeTip.z:0.00}, wall={front:0.00})");
            Capture(Path.Combine(dir, "wall-contact.png"));
            a.StopCombat(); Destroy(wall); yield return null;

            Place(a, new Vector3(0, 0, -2), Vector3.forward);
            yield return new WaitForSeconds(.2f);
            a.SetInput(new PawnInput { shoveHeld = true, aim = Look(0, 20) });
            for (int i = 0; i < Mathf.CeilToInt(.1f / Time.fixedDeltaTime); i++) yield return new WaitForFixedUpdate();
            float drawError = Vector3.Angle(a.BladeTip - a.BladeRoot, SwordFightPawn.GuardDirection(Look(0, 20)));
            Check(a.Drawn && !a.SwordBody.isKinematic && drawError < 15 && SwordFightPawn.DrawTime <= .05f,
                $"Fast draw reaches the visible guard within 0.1 seconds (error={drawError:0.0})");
            yield return new WaitForSeconds(.75f);
            float guardError = Vector3.Angle(a.BladeTip - a.BladeRoot, SwordFightPawn.GuardDirection(Look(0, 20)));
            Check(guardError < 10, $"Sword settles to an offset from camera aim (error={guardError:0.0} degrees)");
            Capture(Path.Combine(dir, "forward-guard.png"));
            yield return TestSwordVisibility(a, dir);

            float maxGuardError = 0; bool stableDraws = true;
            for (int cycle = 0; cycle < 10; cycle++)
            {
                a.SetInput(default);
                for (int i = 0; i < Mathf.CeilToInt(.1f / Time.fixedDeltaTime); i++) yield return new WaitForFixedUpdate();
                a.SetInput(new PawnInput { shoveHeld = true, aim = Look(0, 20) });
                for (int i = 0; i < Mathf.CeilToInt(.1f / Time.fixedDeltaTime); i++) yield return new WaitForFixedUpdate();
                maxGuardError = Mathf.Max(maxGuardError, Vector3.Angle(a.BladeTip - a.BladeRoot, SwordFightPawn.GuardDirection(Look(0, 20))));
                stableDraws &= a.Drawn && a.Pawn.IsFinite() && a.Pawn.State == PawnState.Active &&
                    !a.SwordBody.automaticCenterOfMass && !a.SwordBody.automaticInertiaTensor && a.SwordBody.centerOfMass.sqrMagnitude < 1e-8f;
            }
            Check(stableDraws && maxGuardError < 15, $"Ten sheath/redraw cycles align in 0.1 seconds (worst={maxGuardError:0.0} degrees)");

            float worstTracking = 0;
            for (int i = 0; i < 120; i++)
            {
                var look = Look(i * 90f * Time.fixedDeltaTime, 0);
                a.SetInput(new PawnInput { shoveHeld = true, aim = look });
                yield return new WaitForFixedUpdate();
                if (i > 24) worstTracking = Mathf.Max(worstTracking, Vector3.Angle(a.BladeTip - a.BladeRoot, SwordFightPawn.GuardDirection(look)));
            }
            Check(worstTracking < 15, $"Physical blade follows a 90-degree/second camera drag (worst={worstTracking:0.0})");
            TestWeightedLook();
            yield return TestSoftContact(a, b);
            yield return TestWeightedStrike(a, b);

            Place(a, new Vector3(0, 0, -2), Vector3.forward);
            a.SetInput(new PawnInput { shoveHeld = true, aim = Vector3.forward });
            yield return new WaitForSeconds(.3f);
            Vector3 beforeAim = a.Aim;
            a.SetInput(new PawnInput { shoveHeld = true, aim = Vector3.back });
            yield return new WaitForFixedUpdate();
            Check(Vector3.Angle(beforeAim, a.Aim) > 179 && Vector3.Angle(a.Aim, Vector3.back) < .1f, "Host aim has no artificial turn-speed cap");
            yield return new WaitForSeconds(.3f);
            Check(a.Pawn.IsFinite() && Vector3.Distance(a.SwordBody.position, a.Pawn.bodies[(int)BodyId.HandR].position) < .2f,
                "Sudden unrestricted aim still keeps a finite physical hand joint");
            a.Pawn.TakeHit(Vector3.zero, 1, 0, true);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(!a.Drawn && a.SwordBody.isKinematic, "Knockdown disables sword collision and attack");
            Place(a, new Vector3(0, 0, -2), Vector3.forward);
            yield return new WaitForSeconds(.15f);
            a.Pawn.SetInput(new PawnInput { shove = true });
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(a.Pawn.Diving, "Shared pawn LMB still dives when not intercepted");
            Place(a, new Vector3(0, 0, -2), Vector3.forward);
            Vector3 before = a.Pawn.Hips.position;
            for (int i = 0; i < 100; i++) { a.SetInput(new PawnInput { move = Vector3.right, sprint = true }); yield return new WaitForFixedUpdate(); }
            Check(a.Pawn.Hips.position.x > before.x + 1 && !a.Pawn.Diving, "Shared sprint movement still runs");
            yield return TestControlSwitch(a, b, dir);
            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back);
            yield return new WaitForSeconds(1.5f);
            Vector3 dummyStart = b.Pawn.Hips.position; int dummySwings = b.Swings; received = a.Pawn.Hits;
            for (int i = 0; i < 480; i++) { game.StepRound(Time.fixedDeltaTime); yield return new WaitForFixedUpdate(); }
            Vector3 drift = b.Pawn.Hips.position - dummyStart; drift.y = 0;
            Check(b.Swings == dummySwings && a.Pawn.Hits == received && drift.magnitude < .25f, "Stationary dummy neither pursues nor attacks");
            b.Pawn.Teleport(new Vector3(0, -4, 0), Vector3.back);
            int previousScore = game.White; game.StepRound(.01f); game.StepRound(3.01f);
            Check(game.White == previousScore + 1 && b.Alive && b.Bot, "Dummy ring-out/respawn keeps identity");
            Place(a, SwordFightGame.SpawnPoint(0, 0), Vector3.forward);
            Place(b, SwordFightGame.SpawnPoint(1, 0), Vector3.back);
            for (ulong id = 3; id <= 12; id++) game.Add(id, (int)(id % 2), (int)((id - 1) / 2), true, "Dummy " + id);
            for (int i = 0; i < 360; i++) { game.StepRound(Time.fixedDeltaTime); yield return new WaitForFixedUpdate(); }
            bool finite = true;
            foreach (var f in game.Fighters.Values) finite &= f.Pawn.IsFinite() && !f.Drawn;
            Check(game.Fighters.Count == 12 && finite, "Twelve-pawn idle dummy room stays finite and sheathed");
            // Stress every weapon, including downward contact with the floor.
            float jointError = 0, minimumTip = 99;
            for (int i = 0; i < 720; i++)
            {
                foreach (var f in game.Fighters.Values)
                    f.SetInput(new PawnInput { shoveHeld = true, aim = Look(Mathf.Sin(i * .016f) * 75f, 25f + 40f * Mathf.Sin(i * .009f)) });
                yield return new WaitForFixedUpdate();
                foreach (var f in game.Fighters.Values)
                {
                    finite &= f.Pawn.IsFinite();
                    if (f.Drawn)
                    {
                        jointError = Mathf.Max(jointError, Vector3.Distance(f.SwordBody.position, f.Pawn.bodies[(int)BodyId.HandR].position));
                        minimumTip = Mathf.Min(minimumTip, f.BladeTip.y);
                    }
                }
            }
            Check(finite && jointError < .2f && minimumTip > -.15f, $"Twelve physical swords: stable grip and floor contact (gap={jointError:0.000}, tip={minimumTip:0.000})");
            Capture(Path.Combine(dir, "physical-swords.png"));
            foreach (var f in game.Fighters.Values) f.StopCombat();
            Debug.Log("[SwordFightTest] screenshots: " + dir);
        }

        IEnumerator TestControlSwitch(SwordFightPawn a, SwordFightPawn b, string dir)
        {
            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back);
            var hud = game.GetComponent<ChessFight.Game.SwordFightHud>();
            game.SetMenu(true);
            yield return null; yield return new WaitForEndOfFrame();
            var root = hud.GetComponent<UIDocument>().rootVisualElement;
            var button = root.Q<Button>("sf-switch-controls");
            Check(button != null && button.worldBound.width > 200 && button.worldBound.height > 30 &&
                root.worldBound.Contains(button.worldBound.center), "Control switch button fits the visible menu");
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
            yield return null; yield return new WaitForEndOfFrame();
            Check(game.ClassicControls && a.ClassicControls && !a.Drawn && a.SwordBody.isKinematic &&
                root.Q<Label>("sf-controls").text.Contains("클릭 베기"), "Menu switch selects classic controls and updates help exactly once");
            yield return CaptureHud(hud.GetComponent<UIDocument>(), Path.Combine(dir, "control-menu.png"));
            game.SetMenu(false);
            var neutral = new PawnInput { ability2 = true };
            var packet = RagdollNetProtocol.Input(1, 2, 3, RagdollNetInput.From(neutral));
            Check(RagdollNetProtocol.ReadInput(packet, 1, out _, out _, out var selected) && selected.ability2 && !selected.shove,
                "Control selection is a repeatable absolute CFR4 bit, not a toggle edge");
            a.SetInput(neutral);
            yield return new WaitForSeconds(1.5f);
            int before = b.Pawn.Hits, swings = a.Swings;
            a.SetInput(new PawnInput { ability2 = true, shove = true, shoveHeld = true, aim = Vector3.forward });
            yield return new WaitForFixedUpdate();
            a.SetInput(new PawnInput { ability2 = true, shoveHeld = true, aim = Vector3.forward });
            yield return new WaitForSeconds(.25f);
            Capture(Path.Combine(dir, "classic-swing.png"));
            yield return new WaitForSeconds(1.5f);
            Check(b.Pawn.Hits == before + 1 && a.Swings == swings + 1 && !a.Pawn.Diving && a.SwordBody.isKinematic,
                $"Original click arc hits once and holding does not repeat (hits={b.Pawn.Hits-before}, swings={a.Swings-swings})");
            Place(a, new Vector3(0, 0, -3), Vector3.forward);
            Check(a.ClassicControls, "Respawn preserves selected click controls");
            a.SetInput(neutral);
            a.SetInput(new PawnInput { ability2 = true, shove = true, shoveHeld = true, aim = Vector3.forward });
            yield return new WaitForFixedUpdate();
            bool started = a.Attacking;
            game.ToggleControls();
            a.SetInput(new PawnInput { shoveHeld = true, aim = Vector3.forward });
            yield return new WaitForFixedUpdate();
            Check(started && !a.ClassicControls && !a.Attacking && !a.Drawn, "Switch during click cancels it and requires button release");
            a.SetInput(default); yield return new WaitForFixedUpdate();
            a.SetInput(new PawnInput { shoveHeld = true, aim = Vector3.forward });
            yield return new WaitForFixedUpdate();
            Check(a.Drawn && !a.SwordBody.isKinematic, "Fresh press restores physical controls after switch");
            game.ToggleControls();
            a.SetInput(new PawnInput { ability2 = true, shoveHeld = true, shove = true, aim = Vector3.forward });
            yield return new WaitForFixedUpdate();
            Check(a.ClassicControls && !a.Drawn && !a.Attacking && a.SwordBody.isKinematic,
                "Switch during physical draw removes collider and does not create a click attack");
            a.StopCombat();
            Check(a.ClassicControls && game.ReadLocalInput().ability2, "Neutral menu/focus input and stopping preserve control selection");
            b.ApplyRemote(true, false, 0, new Vector3(.2f, .3f, .4f), Quaternion.Euler(0, 80, 0), 0, 0, true);
            yield return null; yield return new WaitForEndOfFrame();
            Check(b.ClassicControls && b.SwordBody.isKinematic && Vector3.Distance(b.WeaponOffset, new Vector3(.2f,.3f,.4f)) < .01f,
                "Remote classic idle sword uses host pose, not a holster pose");
            game.ToggleControls(); a.SetInput(default);
        }

        IEnumerator CaptureHud(UIDocument document, string path)
        {
            var settings = document.panelSettings;
            var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            rt.Create(); settings.targetTexture = rt;
            yield return null; yield return new WaitForEndOfFrame();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previous; settings.targetTexture = null;
            Destroy(texture); rt.Release(); Destroy(rt);
        }

        static Vector3 Look(float yaw, float pitch) => Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
        IEnumerator Drag(SwordFightPawn f, float from, float to, float pitch, float duration, string dir = null, string prefix = "drag")
        {
            int count = Mathf.CeilToInt(duration / Time.fixedDeltaTime);
            for (int i = 0; i <= count; i++)
            {
                f.SetInput(new PawnInput { shoveHeld = true, aim = Look(Mathf.Lerp(from, to, i / (float)count), pitch) });
                yield return new WaitForFixedUpdate();
                if (dir != null && i % 15 == 0)
                {
                    Debug.Log($"[PhysicalSword] {prefix} {i} root={f.BladeRoot:F2} tip={f.BladeTip:F2} vel={f.SwordBody.linearVelocity:F2} ang={f.SwordBody.angularVelocity:F2} state={f.Pawn.State}");
                    Capture(Path.Combine(dir, $"{prefix}-{i:000}.png"));
                }
            }
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
            f.Respawn(floor, facing, true);
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
