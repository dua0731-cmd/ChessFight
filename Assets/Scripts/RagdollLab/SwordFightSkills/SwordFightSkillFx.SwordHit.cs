using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 10-29 (승규 님: "칼로 상대 맞출 때 vfx 만들어줘"): a sword's cut (click or drag) landing on a piece, the same for every
    /// piece and on every screen (<see cref="SwordFightPawn.Cut"/>, raised with the hit sound). Drawn for real, no rings and
    /// no puffs: a thin bright slash across the piece along the way the blade went, a short white flash where it bit, a
    /// warm light for a frame or two, a few chips of the piece's own wood thrown on the blade's way, and a small kick of the
    /// view for the two pieces in it. <see cref="SwordHitVfxOn"/> false turns it off.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        public static bool SwordHitVfxOn = true;

        // Each sword's tip a frame ago, for the way a blade was going when it bit.
        readonly Dictionary<SwordFightPawn, Vector3> lastTips = new Dictionary<SwordFightPawn, Vector3>();

        void OnEnable() => SwordFightPawn.Cut += OnCut;
        void OnDisable() => SwordFightPawn.Cut -= OnCut;

        /// <summary>Remembers each sword's tip (after this frame's effects, so a cut next frame sees the way it went).</summary>
        void TrackTips()
        {
            if (game == null) return;
            foreach (var f in game.Fighters.Values)
                if (f != null && f.SwordBody != null) lastTips[f] = f.BladeTip;
        }

        void OnCut(SwordFightPawn victim)
        {
            if (!SwordHitVfxOn || !effects || kit == null || victim == null || victim.Pawn == null) return;
            Vector3 chest = victim.Pawn.bodies[(int)BodyId.Chest].position;
            // Whose blade bit: the other side's sword nearest the chest.
            SwordFightPawn by = null;
            float best = 2.5f;
            Vector3 onBlade = chest;
            if (game != null)
                foreach (var f in game.Fighters.Values)
                {
                    if (f == null || f == victim || f.Pawn == null || f.SwordBody == null || f.Pawn.Team == victim.Pawn.Team) continue;
                    Vector3 a = f.BladeRoot, ab = f.BladeTip - a;
                    float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(chest - a, ab) / ab.sqrMagnitude) : 0f;
                    Vector3 p = a + ab * t;
                    float dist = (p - chest).magnitude;
                    if (dist < best) { best = dist; by = f; onBlade = p; }
                }
            // On the piece's face where the blade meets it.
            Vector3 at = by != null ? Vector3.Lerp(chest, onBlade, 0.45f) : chest;
            Vector3 away = by != null ? chest - by.Pawn.Hips.position : victim.Pawn.Facing;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
            // The way the blade was going: its tip since last frame, else across the blade toward the piece.
            Vector3 swing = Vector3.zero;
            if (by != null && lastTips.TryGetValue(by, out var was)) swing = by.BladeTip - was;
            if (swing.sqrMagnitude < 1e-5f && by != null) swing = Vector3.Cross(by.BladeTip - by.BladeRoot, Vector3.up);
            if (swing.sqrMagnitude < 1e-5f) swing = Vector3.Cross(away, Vector3.up);
            swing.Normalize();

            CutSlash(at, swing);
            ClashFlash(at, 0.4f);
            SparkLight(at, 0.9f, 0.07f, 1.4f, new Color(1f, 0.9f, 0.75f));
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color c = BodyColor(victim.Pawn);
            chipA = ChipsOf(c); chipB = ChipsOf(c, 0.75f); chipShare = 0.6f;
            WallChips(at, swing * 0.6f + away * 0.4f + Vector3.up * 0.35f, 5, 2.6f, 0.5f, 0.006f, 0.018f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            var attacker = by != null ? by.Pawn : null;
            Shake(attacker, victim.Pawn, 0.02f, 5);
            Punch(attacker, victim.Pawn, 1.2f, 8);
        }

        /// <summary>The slash a cut leaves for a moment: two thin bright lines across <paramref name="at"/> along
        /// <paramref name="swing"/> as the eye sees it (a long soft one and a sharp core), opening along the cut in two
        /// frames and thinning out in seven.</summary>
        void CutSlash(Vector3 at, Vector3 swing)
        {
            var parts = new List<(Transform t, MeshRenderer r, MaterialPropertyBlock b)>();
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Cut slash", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(kit.root, false);
                go.GetComponent<MeshFilter>().sharedMesh = kit.meshQuad;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = FlashMaterial();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                parts.Add((go.transform, r, new MaterialPropertyBlock()));
            }
            var f = kit.Run(9f * F, (fx, d) =>
            {
                var cam = ViewCamera;
                if (cam == null) return true;
                var view = cam.transform;
                float sx = Vector3.Dot(swing, view.right), sy = Vector3.Dot(swing, view.up);
                float ang = sx * sx + sy * sy > 1e-4f ? Mathf.Atan2(sy, sx) * Mathf.Rad2Deg : 0f;
                Quaternion face = view.rotation * Quaternion.Euler(0f, 0f, ang);
                Vector3 toEye = (view.position - at).normalized;
                float a = fx.age / F;
                float open = Mathf.Clamp01(a / 2f), k = Mathf.Clamp01((a - 1f) / 8f);
                for (int i = 0; i < parts.Count; i++)
                {
                    var (t, r, b) = parts[i];
                    // Swept on a little in the blade's way as it opens, drawn a little toward the eye.
                    t.SetPositionAndRotation(at + toEye * 0.12f + swing * (0.08f * open), face);
                    // The glow falls off soft, so only about the middle half of each line shows: as long as the piece is wide.
                    float len = (i == 0 ? 1.3f : 0.95f) * (0.35f + 0.65f * open);
                    float thick = (i == 0 ? 0.08f : 0.026f) * (1f - 0.8f * k);
                    t.localScale = new Vector3(len, thick, 1f);
                    Color c = (i == 0 ? new Color(1f, 0.86f, 0.62f) * 1.8f : new Color(1f, 0.97f, 0.9f) * 3.4f);
                    c.a = (1f - k) * (1f - k) * (i == 0 ? 0.6f : 1f);
                    b.SetColor(LitMaterials.BaseColorId, c);
                    r.SetPropertyBlock(b);
                }
                return true;
            });
            f.end = () => { foreach (var p in parts) if (p.t != null) Destroy(p.t.gameObject); };
        }
    }
}
