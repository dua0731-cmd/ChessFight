using System.Collections;
using System.IO;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class SwordFightAutoTest
    {
        IEnumerator TestSwordVisibility(SwordFightPawn a, string dir)
        {
            game.CameraRig.yaw = 0; game.CameraRig.pitch = 20;
            yield return null; yield return new WaitForEndOfFrame();
            var camera = game.CameraRig.Cam;
            int visible = 0;
            for (int i = 1; i <= 6; i++)
            {
                Vector3 point = Vector3.Lerp(a.BladeRoot, a.BladeTip, i / 6f);
                Vector3 screen = camera.WorldToViewportPoint(point);
                bool blocked = false;
                Vector3 ray = point - camera.transform.position;
                foreach (var hit in Physics.RaycastAll(camera.transform.position, ray.normalized, ray.magnitude - .025f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.GetComponent<SwordBladeContact>() == null) blocked = true;
                if (!blocked && screen.z > 0 && screen.x > 0 && screen.x < 1 && screen.y > 0 && screen.y < 1) visible++;
            }
            Check(visible >= 3 && Vector3.Angle(a.BladeTip - a.BladeRoot, game.CameraRig.AimForward) > 15,
                $"Blade side is visible outside the pawn silhouette ({visible}/6 samples)");
            Capture(Path.Combine(dir, "readable-guard.png"));
        }

        void TestWeightedLook()
        {
            game.Local.StopCombat();
            float yaw = game.CameraRig.yaw, pitch = game.CameraRig.pitch, sensitivity = game.CameraRig.mouseSensitivity;
            game.StepSwordLook(true, new Vector2(20 / sensitivity, 0), 1f / 60);
            float first = game.CameraRig.yaw - yaw;
            Check(first > 0 && first < 10 && game.CameraRig.mouseSensitivity == 0, "Weighted drag starts gradually, without double mouse input");
            for (int i = 0; i < 60; i++) game.StepSwordLook(true, Vector2.zero, 1f / 60);
            Check(Mathf.Abs(game.CameraRig.yaw - yaw - 20) < .05f, "Weighted drag catches the full requested angle after mouse stops");
            game.StepSwordLook(false, Vector2.zero, 1f / 60);
            Check(game.CameraRig.mouseSensitivity == sensitivity, "Sheathing restores normal camera input and discards queued drift");
            float[] results = new float[3]; int index = 0;
            foreach (int rate in new[] { 30, 60, 144 })
            {
                game.CameraRig.yaw = 0; game.CameraRig.pitch = 20;
                for (int i = 0; i < rate; i++) game.StepSwordLook(true, new Vector2(90f / rate / sensitivity, 0), 1f / rate);
                results[index++] = game.CameraRig.yaw;
                game.ResetSwordLook();
            }
            Check(Mathf.Abs(results[0] - results[2]) < 3, $"Look response is comparable at 30/60/144 FPS ({results[0]:0.0}/{results[1]:0.0}/{results[2]:0.0})");
            game.StepSwordLook(true, new Vector2(10000, 10000), 1f / 60);
            float before = game.CameraRig.yaw;
            for (int i = 0; i < 120; i++) game.StepSwordLook(true, Vector2.zero, 1f / 60);
            Check(game.CameraRig.yaw - before < 39 && game.CameraRig.pitch <= game.CameraRig.maxPitch, "Huge input has a bounded catch-up tail and valid pitch");
            game.SetMenu(true);
            Check(game.CameraRig.mouseSensitivity == sensitivity, "Menu restores input ownership immediately");
            game.SetMenu(false);
            game.CameraRig.yaw = yaw; game.CameraRig.pitch = pitch;
        }

        IEnumerator TestWeightedStrike(SwordFightPawn a, SwordFightPawn b)
        {
            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back);
            yield return new WaitForSeconds(1.5f);
            float yaw = game.CameraRig.yaw, pitch = game.CameraRig.pitch;
            float sensitivity = game.CameraRig.mouseSensitivity;
            game.CameraRig.yaw = -85; game.CameraRig.pitch = 0;
            a.SetInput(new PawnInput { shoveHeld = true, aim = game.CameraRig.AimForward });
            yield return new WaitForSeconds(.25f);
            int hits = b.Pawn.Hits;
            float dt = Time.fixedDeltaTime;
            int steps = Mathf.CeilToInt(.32f / dt);
            for (int i = 0; i < steps + Mathf.CeilToInt(.5f / dt); i++)
            {
                game.StepSwordLook(true, new Vector2(i < steps ? 130f / steps / sensitivity : 0, 0), dt);
                a.SetInput(new PawnInput { shoveHeld = true, aim = game.CameraRig.AimForward });
                yield return new WaitForFixedUpdate();
            }
            Check(b.Pawn.Hits > hits, "Strong mouse drag still lands a real hit through weighted camera and blade physics");
            game.ResetSwordLook(); a.StopCombat();
            game.CameraRig.yaw = yaw; game.CameraRig.pitch = pitch;
        }

        IEnumerator TestSoftContact(SwordFightPawn a, SwordFightPawn b)
        {
            Place(a, new Vector3(0, 0, -.65f), Vector3.forward);
            Place(b, new Vector3(0, 0, .45f), Vector3.back);
            yield return new WaitForSeconds(1.5f);
            int hits = b.Pawn.Hits, falls = b.Pawn.Knockdowns, soft = a.SoftContacts;
            a.SetInput(new PawnInput { shoveHeld = true, aim = Look(-85, 0) });
            yield return new WaitForSeconds(.25f);
            yield return Drag(a, -85, 45, 0, 2.5f);
            yield return new WaitForSeconds(.25f);
            Check(a.SoftContacts > soft && b.Pawn.Hits == hits && b.Pawn.Knockdowns == falls && b.Pawn.State == PawnState.Active,
                $"Slow real blade contact does not knock down (contacts={a.SoftContacts-soft}, hits={b.Pawn.Hits-hits}, falls={b.Pawn.Knockdowns-falls})");
            a.StopCombat();
        }
    }
}
