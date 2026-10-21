using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The prototype match's development keys (spec §12), in development builds and the editor only, so one person can
    /// check the rules without a fighting bot. They act on the host (this PC offline); a client's F8 asks the host.
    /// <list type="table">
    /// <item>F7: the enemy you look at is thrown off the board where it stands (the credit is yours).</item>
    /// <item>F8: your piece goes to the next kind (the pawn included).</item>
    /// <item>F9: the clock to 1:55 of play (just before the board starts to shrink).</item>
    /// <item>F10: every cooldown over.</item>
    /// <item>F11: a still dummy on the other side, each press the next piece (seen only on the host's screen online).</item>
    /// <item>F12: the hit volumes shown: the sword's cone, the ring-out line, when a falling piece "starts to fall".</item>
    /// </list>
    /// </summary>
    public sealed partial class SwordFightMatch
    {
        bool debugView;
        int dummies;
        readonly List<LineRenderer> lines = new List<LineRenderer>();
        LineRenderer outLine;

        static readonly PieceKind[] Cycle = { PieceKind.Pawn, PieceKind.King, PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight };

        void DevKeys()
        {
            if (!Debug.isDebugBuild || ChatBox.KeysHeld || game.MenuOpen) return;
            if (Input.GetKeyDown(KeyCode.F12)) { debugView = !debugView; if (!debugView) ClearLines(); }
            if (debugView) DrawLines();
            var me = game.Local;
            if (Input.GetKeyDown(KeyCode.F8) && me != null)
            {
                int i = System.Array.IndexOf(Cycle, me.Pawn.Piece);
                var next = Cycle[(i + 1) % Cycle.Length];
                if (Authority) DevSetPiece(me.Id, next);
                else LabNetLink.Current?.AskSwordPiece(next);
            }
            if (!Authority) return;
            if (Input.GetKeyDown(KeyCode.F7) && me != null) ThrowLookedAt(me);
            if (Input.GetKeyDown(KeyCode.F9)) rules.SetClock(115);
            if (Input.GetKeyDown(KeyCode.F10))
                foreach (var f in game.Fighters.Values) f?.Skills?.ClearCooldowns();
            if (Input.GetKeyDown(KeyCode.F11) && me != null) AddDummy(1 - me.Pawn.Team);
        }

        void DevSetPiece(ulong id, PieceKind kind)
        {
            if (!game.Fighters.TryGetValue(id, out var f) || f == null) return;
            var r = rules.Get(id);
            if (r != null) { r.Piece = kind; r.Picked = true; }
            f.Pawn.SetPiece(kind);
            f.Skills?.Ready(0f);
        }

        void ThrowLookedAt(SwordFightPawn me)
        {
            var cam = game.CameraRig != null ? game.CameraRig.Cam : Camera.main;
            if (cam == null) return;
            SwordFightPawn best = null;
            float bestAngle = 35f;
            foreach (var f in game.Fighters.Values)
            {
                if (f == null || !f.Alive || f.Pawn.Team == me.Pawn.Team) continue;
                float a = Vector3.Angle(cam.transform.forward, f.Pawn.Hips.position - cam.transform.position);
                if (a < bestAngle) { bestAngle = a; best = f; }
            }
            if (best == null) return;
            rules.Hit(me.Id, best.Id, SwordCause.Debug, (byte)me.Pawn.Piece);
            // Off the nearest side of the board, from where it stands.
            Vector3 p = best.Pawn.Hips.position;
            float half = SwordBoard.Size * Cell * 0.5f;
            Vector3 off = Mathf.Abs(p.x) > Mathf.Abs(p.z) ? new Vector3(Mathf.Sign(p.x) * (half + 1.5f), 0f, p.z) : new Vector3(p.x, 0f, Mathf.Sign(p.z == 0 ? 1 : p.z) * (half + 1.5f));
            best.Pawn.Teleport(off + Vector3.up * (floor.Top + best.Pawn.standHeight + 0.5f), best.Pawn.Facing);
            best.StopCombat();
        }

        void AddDummy(int team)
        {
            dummies++;
            ulong id = 9000UL + (ulong)dummies;
            int slot = 0;
            foreach (var f in game.Fighters.Values) if (f != null && f.Pawn.Team == team) slot++;
            var piece = SwordMatchRules.Pickable[(dummies - 1) % SwordMatchRules.Pickable.Length];
            var d = game.Add(id, team, slot, true, $"더미 {dummies}");
            var r = rules.Get(id);
            if (r != null) { r.Piece = piece; r.Picked = true; }
            if (rules.Phase != SwordPhase.Pick) { d.Pawn.SetPiece(piece); d.Skills?.Ready(0f); }
        }

        // ---------------------------------------------------------------- F12

        void ClearLines()
        {
            foreach (var l in lines) if (l != null) Destroy(l.gameObject);
            lines.Clear();
            if (outLine != null) Destroy(outLine.gameObject);
            outLine = null;
        }

        LineRenderer Line(Color c)
        {
            var go = new GameObject("Debug line");
            var l = go.AddComponent<LineRenderer>();
            l.sharedMaterial = Made(SwordFightShapes.Glow("#ffffff", 1f));
            l.startColor = l.endColor = c;
            l.startWidth = l.endWidth = 0.04f;
            l.useWorldSpace = true;
            return l;
        }

        void DrawLines()
        {
            // The ring-out line: 3 m under the board's top, round the board.
            if (outLine == null)
            {
                outLine = Line(new Color(1f, 0.2f, 0.2f, 0.9f));
                float half = SwordBoard.Size * Cell * 0.5f, y = floor.Top - 3f;
                outLine.loop = true;
                outLine.positionCount = 4;
                outLine.SetPositions(new[] { new Vector3(-half, y, -half), new Vector3(half, y, -half), new Vector3(half, y, half), new Vector3(-half, y, half) });
            }
            // The sword's cone (±45°, 1.45 m, spec §7.1) for every fighter.
            int n = 0;
            foreach (var f in game.Fighters.Values)
            {
                if (f == null || !f.Alive) continue;
                if (n >= lines.Count) lines.Add(Line(new Color(1f, 0.9f, 0.2f, 0.8f)));
                var l = lines[n++];
                l.gameObject.SetActive(true);
                Vector3 o = f.Pawn.Hips.position;
                o.y = floor.Top + 0.05f;
                Vector3 fwd = f.Pawn.Facing;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                l.loop = true;
                l.positionCount = 7;
                l.SetPosition(0, o);
                for (int k = 0; k <= 4; k++) l.SetPosition(k + 1, o + Quaternion.AngleAxis(-45f + 22.5f * k, Vector3.up) * fwd * 1.45f);
                l.SetPosition(6, o);
            }
            for (int i = n; i < lines.Count; i++) lines[i].gameObject.SetActive(false);
        }

        /// <summary>"떨어지기 시작" over a piece once it is off the board's edge in the air, or 0.25 m under its top (spec §5).</summary>
        void DrawFallStart(SwordFightPawn f, Vector3 screen, float h)
        {
            Vector3 p = f.Pawn.Hips.position;
            bool off = !floor.OnFloor(p) || p.y - f.Pawn.standHeight < floor.Top - 0.25f;
            if (off) Text(new Rect(screen.x - 110f, h - screen.y - 44f, 220f, 22f), "떨어지기 시작", bodyStyle, 13, new Color(1f, 0.35f, 0.3f));
        }
    }
}
