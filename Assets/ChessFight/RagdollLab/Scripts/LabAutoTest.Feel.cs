using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Control-feel recordings: every basic action played from the player's own camera with scripted input,
    /// recorded at 30 fps together with a telemetry table, so how the controls feel can be looked at frame by
    /// frame and measured, and compared before and after a change.
    ///
    ///   RagdollLab.exe -batchmode -ragdollFeel &lt;folder&gt; [-ragdollFeelOnly run,tackle_run] [-ragdollTuning x.json]
    ///
    /// Needs graphics (no -nographics). Each scenario writes &lt;folder&gt;/&lt;name&gt;/f_0000.png... and
    /// &lt;folder&gt;/&lt;name&gt;.csv (one row per recorded frame: input, hips, speeds, state, stamina...).
    /// Tools/RagdollPreview/feel_sheets.py turns them into contact sheets and GIFs.
    /// </summary>
    public partial class LabAutoTest
    {
        int shotWidth = 1280, shotHeight = 720;
        // A second, close-up view from the side of the pawn, recorded next to the player's view (s_*.png):
        // the player camera shows how it feels, this one shows what the body is doing.
        Camera sideCamera;
        Vector3 sideFocus;
        StringBuilder feelCsv;
        float feelClock;
        string feelMark = "";
        RagdollPawn feelPawn, feelOther;
        PawnInput feelInput;

        static bool FeelRequested => Arg("-ragdollFeel") != null;

        IEnumerator RunFeel(string folder)
        {
            Directory.CreateDirectory(folder);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 1f / game.physicsRate;
            Time.captureFramerate = 30;
            shotWidth = 800;
            shotHeight = 450;
            var cam = game.labCamera;
            cam.enabled = true;
            LabCamera.GameTimeClock = true;
            shotCamera = cam.GetComponent<Camera>();
            var side = new GameObject("Feel Side Camera");
            sideCamera = side.AddComponent<Camera>();
            sideCamera.CopyFrom(shotCamera);
            sideCamera.fieldOfView = 40f;
            sideCamera.enabled = false;
            string only = Arg("-ragdollFeelOnly");
            bool Want(string name) => string.IsNullOrEmpty(only) || Array.IndexOf(only.Split(','), name) >= 0;

            if (Want("run")) yield return FeelScenario(folder, "run", FeelRun());
            if (Want("turn")) yield return FeelScenario(folder, "turn", FeelTurn());
            if (Want("sprint")) yield return FeelScenario(folder, "sprint", FeelSprint());
            if (Want("jump")) yield return FeelScenario(folder, "jump", FeelJump());
            if (Want("tackle_run")) yield return FeelScenario(folder, "tackle_run", FeelTackleRun());
            if (Want("tackle_stand")) yield return FeelScenario(folder, "tackle_stand", FeelTackleStand());
            if (Want("dive_run")) yield return FeelScenario(folder, "dive_run", FeelDiveRun());
            if (Want("grab_throw")) yield return FeelScenario(folder, "grab_throw", FeelGrabThrow());
            if (Want("struggle_tap")) yield return FeelScenario(folder, "struggle_tap", FeelStruggle(false));
            if (Want("struggle_power")) yield return FeelScenario(folder, "struggle_power", FeelStruggle(true));
            if (Want("climb_2m")) yield return FeelScenario(folder, "climb_2m", FeelClimb(0));
            if (Want("climb_4m")) yield return FeelScenario(folder, "climb_4m", FeelClimb(2));
            if (Want("bump")) yield return FeelScenario(folder, "bump", FeelBump());

            Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            Destroy(side);
            sideCamera = null;
            // The autotest's own shots run at full size if it comes next.
            shotWidth = 1280;
            shotHeight = 720;
        }

        // ------------------------------------------------------------------ plumbing

        /// <summary>The side camera: 3.2 m to the pawn's right of its travel, level with it, smoothed.</summary>
        void PlaceSideCamera()
        {
            if (sideCamera == null || feelPawn == null) return;
            Vector3 c = feelPawn.CameraPoint;
            sideFocus = sideFocus == Vector3.zero ? c : Vector3.Lerp(sideFocus, c, 0.25f);
            Vector3 right = game.labCamera.FlatRight;
            Vector3 eye = sideFocus + right * 3.2f + Vector3.up * 0.55f - game.labCamera.FlatForward * 0.6f;
            sideCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(sideFocus + Vector3.up * 0.15f - eye, Vector3.up));
        }

        IEnumerator FeelScenario(string root, string name, IEnumerator body)
        {
            yield return Clear();
            feelPawn = feelOther = null;
            feelMark = "";
            feelInput = default;
            string folder = Path.Combine(root, name);
            Directory.CreateDirectory(folder);
            clipFrame = 0;
            feelCsv = new StringBuilder();
            feelCsv.AppendLine("t,mark,in_x,in_y,jump,shove,grab,sprint,hx,hy,hz,vx,vy,vz,speed,ground_speed,state,grounded,"
                               + "tilt,facing,stamina,climbing,grabbing,held,diving,sprinting,knockdowns,anchor_lead,other_state,other_grabbing,other_held");
            feelClock = 0f;
            // The body sets feelPawn, pre-rolls (not recorded) and then records by calling FeelRecord.
            yield return body;
            clipFolder = null;
            File.WriteAllText(Path.Combine(root, name + ".csv"), feelCsv.ToString());
            feelCsv = null;
            yield return Clear();
        }

        /// <summary>Player input for this frame, turned into world space through the lab camera like
        /// LabGame.ReadInput does. Edge buttons are sent once.</summary>
        void Feel(float x, float y, bool jump = false, bool shove = false, bool grab = false, bool sprint = false)
        {
            var cam = game.labCamera;
            Vector2 mv = new Vector2(x, y);
            if (mv.sqrMagnitude > 1f) mv.Normalize();
            feelInput = new PawnInput
            {
                move = cam.FlatRight * mv.x + cam.FlatForward * mv.y,
                jump = jump, shove = shove, grab = grab, sprint = sprint,
                aim = cam.AimForward,
            };
            if (feelPawn != null) feelPawn.SetInput(feelInput);
        }

        IEnumerator FeelPreroll(RagdollPawn pawn, float seconds, float yaw)
        {
            var cam = game.labCamera;
            cam.soloTarget = pawn;
            cam.yaw = yaw;
            cam.pitch = 14f;
            cam.distance = 3.6f;
            feelPawn = pawn;
            sideFocus = Vector3.zero;
            float t = 0f;
            while (t < seconds)
            {
                Feel(0f, 0f);
                yield return null;
                t += Time.deltaTime;
            }
        }

        /// <summary>Record for a while. perFrame runs before each frame with the time into this segment.</summary>
        IEnumerator FeelRecord(float seconds, string mark, Action<float> perFrame)
        {
            float t = 0f;
            feelMark = mark;
            while (t < seconds)
            {
                perFrame?.Invoke(t);
                FeelRow();
                PlaceSideCamera();
                yield return null;
                t += Time.deltaTime;
                feelClock += Time.deltaTime;
            }
        }

        void FeelRow()
        {
            var p = feelPawn;
            if (p == null || feelCsv == null) return;
            var c = CultureInfo.InvariantCulture;
            Vector3 h = p.Hips.position, v = p.Hips.linearVelocity;
            Vector3 lead = p.AnchorPosition - h;
            lead.y = 0f;
            var cam = game.labCamera;
            float inX = Vector3.Dot(feelInput.move, cam.FlatRight), inY = Vector3.Dot(feelInput.move, cam.FlatForward);
            string other = feelOther == null ? ",," : $"{(int)feelOther.State},{(feelOther.Grabbing ? 1 : 0)},{(feelOther.BeingHeld ? 1 : 0)}";
            feelCsv.AppendLine(string.Format(c,
                "{0:F3},{1},{2:F2},{3:F2},{4},{5},{6},{7},{8:F3},{9:F3},{10:F3},{11:F2},{12:F2},{13:F2},{14:F2},{15:F2},{16},{17},{18:F1},{19:F1},{20:F2},{21},{22},{23},{24},{25},{26},{27:F2},{28}",
                feelClock, feelMark, inX, inY, feelInput.jump ? 1 : 0, feelInput.shove ? 1 : 0, feelInput.grab ? 1 : 0, feelInput.sprint ? 1 : 0,
                h.x, h.y, h.z, v.x, v.y, v.z, p.HorizontalSpeed, p.GroundSpeed, (int)p.State, p.Grounded ? 1 : 0,
                p.HipsTilt, Mathf.Atan2(p.Facing.x, p.Facing.z) * Mathf.Rad2Deg, p.Stamina, p.Climbing ? 1 : 0, p.Grabbing ? 1 : 0,
                p.BeingHeld ? 1 : 0, p.Diving ? 1 : 0, p.Sprinting ? 1 : 0, p.Knockdowns, lead.magnitude, other));
        }

        // ------------------------------------------------------------------ scenarios

        /// <summary>Start, run, stop, run again, reverse, stop.</summary>
        IEnumerator FeelRun()
        {
            var pawn = Spawn(new Vector3(-6f, 0f, -12f), Vector3.forward, "feel");
            yield return FeelPreroll(pawn, 0.8f, 0f);
            clipFolder = FeelFolder("run");
            yield return FeelRecord(0.5f, "idle", _ => Feel(0f, 0f));
            yield return FeelRecord(2.5f, "W", _ => Feel(0f, 1f));
            yield return FeelRecord(1.2f, "stop", _ => Feel(0f, 0f));
            yield return FeelRecord(1.0f, "W", _ => Feel(0f, 1f));
            yield return FeelRecord(1.5f, "S", _ => Feel(0f, -1f));
            yield return FeelRecord(1.0f, "stop", _ => Feel(0f, 0f));
        }

        /// <summary>Run while turning the camera with the mouse, then strafe both ways.</summary>
        IEnumerator FeelTurn()
        {
            var pawn = Spawn(new Vector3(-4f, 0f, -6f), Vector3.forward, "feel");
            yield return FeelPreroll(pawn, 0.8f, 0f);
            clipFolder = FeelFolder("turn");
            yield return FeelRecord(0.8f, "W", _ => Feel(0f, 1f));
            yield return FeelRecord(3.0f, "W+mouse", _ => { game.labCamera.AddYaw(120f * Time.deltaTime); Feel(0f, 1f); });
            yield return FeelRecord(1.5f, "A", _ => Feel(-1f, 0f));
            yield return FeelRecord(1.2f, "D", _ => Feel(1f, 0f));
            yield return FeelRecord(0.8f, "stop", _ => Feel(0f, 0f));
        }

        IEnumerator FeelSprint()
        {
            // Along the south edge, clear of the tunnel and the climbing lanes.
            var pawn = Spawn(new Vector3(-13f, 0f, -14f), Vector3.right, "feel");
            yield return FeelPreroll(pawn, 0.8f, 90f);
            clipFolder = FeelFolder("sprint");
            yield return FeelRecord(0.4f, "idle", _ => Feel(0f, 0f));
            yield return FeelRecord(3.0f, "shift+W", _ => Feel(0f, 1f, sprint: true));
            yield return FeelRecord(1.0f, "W", _ => Feel(0f, 1f));
            yield return FeelRecord(1.2f, "stop", _ => Feel(0f, 0f));
        }

        IEnumerator FeelJump()
        {
            var pawn = Spawn(new Vector3(-8f, 0f, -12f), Vector3.forward, "feel");
            yield return FeelPreroll(pawn, 0.8f, 0f);
            clipFolder = FeelFolder("jump");
            yield return FeelRecord(0.4f, "idle", _ => Feel(0f, 0f));
            yield return FeelRecord(1.3f, "jump", t => Feel(0f, 0f, jump: t == 0f));
            yield return FeelRecord(1.0f, "W", _ => Feel(0f, 1f));
            yield return FeelRecord(1.3f, "W+jump", t => Feel(0f, 1f, jump: t == 0f));
            yield return FeelRecord(0.6f, "W", _ => Feel(0f, 1f));
            yield return FeelRecord(1.0f, "stop", _ => Feel(0f, 0f));
        }

        /// <summary>Run at a standing pawn and slide-tackle it, holding W through the slide as players do.</summary>
        IEnumerator FeelTackleRun()
        {
            var pawn = Spawn(new Vector3(0f, 0f, -12f), Vector3.forward, "feel");
            feelOther = Spawn(new Vector3(0f, 0f, -5.5f), Vector3.back, "target");
            yield return FeelPreroll(pawn, 0.8f, 0f);
            clipFolder = FeelFolder("tackle_run");
            yield return FeelRecord(0.3f, "idle", _ => Feel(0f, 0f));
            yield return FeelRecord(0.95f, "W", _ => Feel(0f, 1f));
            yield return FeelRecord(3.2f, "click+W", t => Feel(0f, 1f, shove: t == 0f));
            yield return FeelRecord(0.8f, "stop", _ => Feel(0f, 0f));
        }

        /// <summary>The dive on its own, from a run: no target in the way, to see the shape of it.</summary>
        IEnumerator FeelDiveRun()
        {
            var pawn = Spawn(new Vector3(-10f, 0f, -12f), Vector3.forward, "feel");
            yield return FeelPreroll(pawn, 0.8f, 0f);
            clipFolder = FeelFolder("dive_run");
            yield return FeelRecord(0.3f, "idle", _ => Feel(0f, 0f));
            yield return FeelRecord(0.95f, "W", _ => Feel(0f, 1f));
            yield return FeelRecord(2.6f, "click+W", t => Feel(0f, 1f, shove: t == 0f));
            yield return FeelRecord(0.6f, "stop", _ => Feel(0f, 0f));
        }

        IEnumerator FeelTackleStand()
        {
            var pawn = Spawn(new Vector3(-3f, 0f, -12f), Vector3.forward, "feel");
            yield return FeelPreroll(pawn, 0.8f, 0f);
            clipFolder = FeelFolder("tackle_stand");
            yield return FeelRecord(0.4f, "idle", _ => Feel(0f, 0f));
            yield return FeelRecord(2.8f, "click", t => Feel(0f, 0f, shove: t == 0f));
        }

        /// <summary>Grab a pawn, drag it back, swing round, throw it.</summary>
        IEnumerator FeelGrabThrow()
        {
            var pawn = Spawn(new Vector3(-6f, 0f, 2f), Vector3.forward, "feel");
            feelOther = Spawn(new Vector3(-6f, 0f, 3.1f), Vector3.back, "target");
            yield return FeelPreroll(pawn, 0.8f, 0f);
            clipFolder = FeelFolder("grab_throw");
            yield return FeelRecord(0.9f, "W+grab", _ => Feel(0f, 0.4f, grab: true));
            yield return FeelRecord(1.5f, "S+grab", _ => Feel(0f, -1f, grab: true));
            yield return FeelRecord(1.2f, "A+grab", _ => { game.labCamera.AddYaw(-90f * Time.deltaTime); Feel(-1f, 0f, grab: true); });
            yield return FeelRecord(1.6f, "throw", t => Feel(0f, 1f, grab: true, shove: t == 0f));
            yield return FeelRecord(0.6f, "stop", _ => Feel(0f, 0f));
        }

        /// <summary>A pawn grabs the player; the player mashes the left button (power: while pulling away and
        /// jumping as well). Taps every 0.15 s from 1 s in.</summary>
        IEnumerator FeelStruggle(bool power)
        {
            var pawn = Spawn(new Vector3(6f, 0f, 2f), Vector3.forward, "feel");
            var holder = feelOther = Spawn(new Vector3(6f, 0f, 3.05f), Vector3.back, "holder");
            yield return FeelPreroll(pawn, 0.6f, 0f);
            clipFolder = FeelFolder(power ? "struggle_power" : "struggle_tap");
            float nextTap = 1.0f;
            int taps = 0;
            yield return FeelRecord(5.0f, power ? "mash+S+jump" : "mash", t =>
            {
                // The holder keeps grabbing and backs off, hauling the player along.
                holder.SetInput(new PawnInput { move = t < 0.5f ? Vector3.back * 0.3f : Vector3.forward * 0.6f, grab = true });
                bool tap = t >= nextTap && (holder.Grabbing || pawn.BeingHeld);
                if (tap)
                {
                    nextTap = t + 0.15f;
                    taps++;
                    feelMark = $"tap{taps}";
                }
                if (power) Feel(0f, pawn.BeingHeld ? -1f : 0f, shove: tap, jump: tap && taps % 2 == 0);
                else Feel(0f, 0f, shove: tap);
            });
        }

        /// <summary>Walk up to a wall and climb it (2 m: over the top; 4 m: up, sideways, down, drop, kick off).</summary>
        IEnumerator FeelClimb(int wall)
        {
            float z = LabLayout.WallZ[wall];
            var pawn = Spawn(new Vector3(LabLayout.WallFrontX - 2.5f, 0f, z), Vector3.right, "feel");
            yield return FeelPreroll(pawn, 0.8f, 90f);
            clipFolder = FeelFolder(wall == 0 ? "climb_2m" : "climb_4m");
            yield return FeelRecord(0.6f, "W", _ => Feel(0f, 1f));
            if (wall == 0)
            {
                yield return FeelRecord(3.2f, "W+grab", _ => Feel(0f, 1f, grab: true));
                yield return FeelRecord(1.2f, "W", _ => Feel(0f, 1f));
                yield return FeelRecord(0.6f, "stop", _ => Feel(0f, 0f));
            }
            else
            {
                yield return FeelRecord(2.4f, "W+grab", _ => Feel(0f, 1f, grab: true));
                yield return FeelRecord(1.4f, "D+grab", _ => Feel(1f, 0f, grab: true));
                yield return FeelRecord(0.8f, "S+grab", _ => Feel(0f, -1f, grab: true));
                yield return FeelRecord(0.9f, "W+grab", _ => Feel(0f, 1f, grab: true));
                yield return FeelRecord(1.6f, "jump", t => Feel(0f, 0f, grab: true, jump: t == 0f));
                yield return FeelRecord(0.8f, "stop", _ => Feel(0f, 0f));
            }
        }

        /// <summary>Run into a standing pawn shoulder first, and into a wall.</summary>
        IEnumerator FeelBump()
        {
            var pawn = Spawn(new Vector3(LabLayout.WallFrontX - 7f, 0f, LabLayout.WallZ[1]), Vector3.right, "feel");
            feelOther = Spawn(new Vector3(LabLayout.WallFrontX - 4.2f, 0f, LabLayout.WallZ[1]), Vector3.left, "target");
            yield return FeelPreroll(pawn, 0.8f, 90f);
            clipFolder = FeelFolder("bump");
            yield return FeelRecord(2.2f, "W", _ => Feel(0f, 1f));
            yield return FeelRecord(0.6f, "W+sprint", _ => Feel(0f, 1f, sprint: true));
            yield return FeelRecord(1.2f, "stop", _ => Feel(0f, 0f));
        }

        string FeelFolder(string name) => Path.Combine(Arg("-ragdollFeel") ?? "feel", name);
    }
}
