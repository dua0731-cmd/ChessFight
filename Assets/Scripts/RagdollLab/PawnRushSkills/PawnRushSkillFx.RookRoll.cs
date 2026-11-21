using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 룩앤롤 on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.RookRoll.cs): the rook's own tower, toppled
    /// and rolling.
    ///
    ///   the column   the rook's model as it was at the key, baked once and drawn on its side, 3 m across and 4.5 m long,
    ///                turning with its speed (its own skin hidden meanwhile); dust and grit off the ground behind it, the
    ///                ground shaking under whoever it passes, a rumble growing with its speed
    ///   the pins     a red flash and a clack for each enemy; four in a moment: "스트라이크!" with the pins' clatter
    ///   the rest     a crash into a wall (grit, a boom), the big jump and its landing, a teammate thrown ahead ("슝!")
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class RrFx
        {
            public Mesh baked;
            public Material[] mats;
            public Matrix4x4 canon = Matrix4x4.identity;
            public float angle, width = 1f, length = 1f, nextRumble, nextDust;
            public bool rolling, stood;
            public AudioSource loop;
        }

        readonly HashSet<RagdollPawn> rollHidden = new HashSet<RagdollPawn>();

        static float RrSpeedOf(RagdollPawn p, PawnRushSkillParams s) =>
            p != null && s != null ? (p.PlanNote & RagdollPawn.RrSpeedMask) / 15f * s.rrSpeed1 : 0f;

        void RrCast(UltShow u)
        {
            u.state = new RrFx();
        }

        /// <summary>The topple: the tower as it stands now, baked, laid on its side; dust.</summary>
        void RrTopple(UltShow u, RrFx f, SkillFxEvent e)
        {
            var s = Params;
            var skin = u.by != null ? u.by.skin : null;
            if (skin == null || s == null) return;
            f.baked = new Mesh { name = "Rook roll", hideFlags = HideFlags.HideAndDontSave };
            skin.BakeMesh(f.baked, true);
            f.baked.RecalculateBounds();
            Bounds b = f.baked.bounds;
            Quaternion skinRot = skin.transform.rotation;
            Vector3 skinPos = skin.transform.position, worldCenter = skinPos + skinRot * b.center;
            Vector3 face = Flat(u.by.Facing);
            Quaternion facing = Quaternion.LookRotation(face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward, Vector3.up);
            f.canon = Matrix4x4.Rotate(Quaternion.Inverse(facing)) * Matrix4x4.Translate(skinPos - worldCenter) * Matrix4x4.Rotate(skinRot);
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
                Vector3 c = f.canon.MultiplyPoint3x4(corner);
                lo = Vector3.Min(lo, c);
                hi = Vector3.Max(hi, c);
            }
            Vector3 size = hi - lo;
            f.width = 2f * s.rrRadius / Mathf.Max(0.2f, Mathf.Max(size.x, size.z));
            f.length = s.rrLength / Mathf.Max(0.2f, size.y);
            f.mats = skin.sharedMaterials;
            f.rolling = true;
            Vector3 floor = Ground(e.at);
            DustRing(floor, 18, 1.2f, 6f, new Color(0.55f, 0.52f, 0.48f), 1f, 1f);
            UltSquareWave(floor, FamSquare * 0.5f, 9f, 0.6f, Teal, 2.4f);
            UltPopWord(floor + Vector3.up * 4f, "Rook'n'Roll!", HdrA(Teal, 1.4f), 0.9f, 1.4f);
            UltSound(UltSfx.Boom, floor);
            UltAudioBuild();
            if (sfxRumble != null)
            {
                f.loop = root.gameObject.AddComponent<AudioSource>();
                f.loop.playOnAwake = false;
                f.loop.spatialBlend = 0f;
                f.loop.clip = sfxRumble;
                f.loop.loop = true;
                f.loop.volume = 0f;
                f.loop.Play();
            }
        }

        void RrStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is RrFx f) || u.by == null) return;
            var s = Params;
            bool roll = f.rolling && !f.stood && u.Playing && s != null && f.baked != null;
            if (!roll)
            {
                if (f.loop != null) { Destroy(f.loop); f.loop = null; }
                if (f.baked != null && (f.stood || !u.Playing)) { Destroy(f.baked); f.baked = null; }
                return;
            }
            var skin = u.by.skin;
            if (skin != null)
            {
                skin.forceRenderingOff = true;
                rollHidden.Add(u.by);
            }
            float speed = RrSpeedOf(u.by, s);
            bool air = (u.by.PlanNote & RagdollPawn.RrAir) != 0;
            Vector3 center = u.by.Hips.position, dir = u.by.PlanDir;
            dir = Flat(dir).sqrMagnitude > 1e-4f ? Flat(dir).normalized : Vector3.forward;
            f.angle += speed / Mathf.Max(0.3f, s.rrRadius) * Mathf.Rad2Deg * dt;
            Quaternion turn = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.AngleAxis(f.angle, Vector3.right) * Quaternion.Euler(0f, 0f, -90f);
            Matrix4x4 m = Matrix4x4.TRS(center, turn, Vector3.one) * Matrix4x4.Scale(new Vector3(f.width, f.length, f.width)) * f.canon;
            var bounds = new Bounds(center, Vector3.one * (s.rrLength + 2f));
            for (int i = 0; i < f.baked.subMeshCount && f.mats != null && f.mats.Length > 0; i++)
            {
                var mat = f.mats[Mathf.Min(i, f.mats.Length - 1)];
                if (mat == null) continue;
                var rp = new RenderParams(mat)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    receiveShadows = true,
                    layer = skin != null ? skin.gameObject.layer : 0,
                    worldBounds = bounds,
                };
                Graphics.RenderMesh(rp, f.baked, i, m);
            }
            // Dust and grit off the ground behind it; the ground shaking under whoever it passes; its rumble.
            Vector3 ground = center - Vector3.up * s.rrRadius;
            float fast = Mathf.Clamp01(speed / Mathf.Max(1f, s.rrSpeed1));
            if (!air && dt > 0f && Clock >= f.nextDust)
            {
                f.nextDust = Clock + Mathf.Lerp(0.06f, 0.02f, fast);
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                Vector3 at = ground + right * Random.Range(-s.rrLength, s.rrLength) * 0.45f - dir * s.rrRadius * 0.6f;
                smoke.Emit(at, -dir * speed * 0.15f + Vector3.up * Random.Range(0.6f, 1.6f), Random.Range(0.6f, 1.2f), Random.Range(0.6f, 1f), Tint(new Color(0.58f, 0.55f, 0.5f), 0.1f));
                if (fast > 0.5f) sparks.Emit(at, -dir * speed * 0.3f + Vector3.up * 2f + Random.insideUnitSphere, 0.04f, 0.4f, Tint(Ember, 0.2f));
            }
            if (cam != null && !air && Clock >= f.nextRumble)
            {
                f.nextRumble = Clock + 0.15f;
                float near = Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, center) / 18f);
                if (near > 0f) Shake(0.05f * near * (0.4f + fast), 0.2f);
            }
            if (f.loop != null)
            {
                float d = cam != null ? Vector3.Distance(cam.transform.position, center) : 10f;
                f.loop.volume = (air ? 0.15f : 0.75f) * (0.35f + 0.65f * fast) * Mathf.Clamp01(1.2f - d / 60f) * ChessFight.Game.GameSettings.Effects;
                f.loop.pitch = 0.7f + 0.6f * fast;
            }
        }

        /// <summary>Every frame: a rook whose roll is over shows its own skin again.</summary>
        void UltRollRestore()
        {
            if (rollHidden.Count == 0) return;
            rollHiddenGone.Clear();
            foreach (var p in rollHidden)
            {
                bool still = false;
                foreach (var u in ults)
                    if (u.by == p && u.state is RrFx f && f.rolling && !f.stood && u.Playing) still = true;
                if (still) continue;
                if (p != null && p.skin != null) p.skin.forceRenderingOff = false;
                rollHiddenGone.Add(p);
            }
            foreach (var p in rollHiddenGone) rollHidden.Remove(p);
        }

        readonly List<RagdollPawn> rollHiddenGone = new List<RagdollPawn>();

        void DestroyRolls()
        {
            foreach (var p in rollHidden) if (p != null && p.skin != null) p.skin.forceRenderingOff = false;
            rollHidden.Clear();
            foreach (var u in ults)
                if (u.state is RrFx f)
                {
                    if (f.baked != null) Destroy(f.baked);
                    if (f.loop != null) Destroy(f.loop);
                }
        }

        void RrStateFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is RrFx f)) return;
            Vector3 floor = Ground(e.at);
            switch (e.count)
            {
                case RagdollPawn.RrTopple:
                    RrTopple(u, f, e);
                    break;
                case RagdollPawn.RrJump:
                    DustRing(floor, 16, 1f, 5f, new Color(0.55f, 0.52f, 0.48f), 0.9f, 0.9f);
                    Flare(e.at, Electric, 2.5f, 7f, 0.3f);
                    UltSound(UltSfx.Leap, e.at, 4);
                    break;
                case RagdollPawn.RrCrash:
                    DustRing(floor, 20, 1.4f, 7f, new Color(0.55f, 0.52f, 0.48f), 1f, 1.1f);
                    SparkBurst(e.at + Vector3.up, 40, Ember, -e.dir, 70f, 4f, 12f, 0.7f, 0.06f);
                    UltPlay(sfxImpactLow, 0.9f, 0f, 1.2f);
                    UltPlay(clipWall, 0.8f);
                    {
                        var cam = ViewCamera;
                        float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, e.at) / 30f) : 0.5f;
                        Shake(Viewer == u.by ? 0.25f : 0.2f * near, 0.5f);
                    }
                    break;
                case RagdollPawn.RrLift:
                    Flare(e.at, Teal, 2.5f, 6f, 0.3f);
                    if (e.target != null) UltPopWord(HeadOf(e.target) + Vector3.up * 1.2f, "슝!", HdrA(Teal, 1.4f), 0.6f, 1f);
                    UltPlay(sfxChime[5], 0.8f);
                    break;
                case RagdollPawn.RrStrike:
                    Flare(e.at + Vector3.up * 2f, Color.white, 5f, 16f, 0.4f);
                    UltPopWord(e.at + Vector3.up * 5f, "스트라이크!", HdrA(GradeColors[0], 1.6f), 1.4f, 1.8f);
                    for (int i = 0; i < 6; i++) UltPlay(sfxClack, 0.8f, i * 0.045f, 0.85f + i * 0.07f);
                    UltPlay(sfxGrade[0], 0.9f, 0.25f);
                    UltSquareWave(floor, FamSquare * 0.5f, 16f, 0.9f, GradeColors[0], 2.6f);
                    HitStop(0.1f);
                    break;
                case RagdollPawn.RrStand:
                    f.stood = true;
                    DustRing(floor, 18, 1f, 5f, new Color(0.55f, 0.52f, 0.48f), 0.9f, 0.9f);
                    UltSquareWave(floor, FamSquare * 0.5f, 6f, 0.5f, Teal, 2f);
                    break;
            }
        }

        void RrStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null) return;
            Vector3 at = ChestOf(e.target);
            Flare(at, Blood, 3f, 7f, 0.3f);
            SparkBurst(at, 26, Color.white, e.dir, 70f, 4f, 12f, 0.5f, 0.05f);
            UltPlay(sfxClack, 0.9f, 0f, Random.Range(0.9f, 1.15f));
            UltPlay(clipStomp, 0.6f);
            UltScreenStrike(u, e);
        }

        string RrHint(UltShow u)
        {
            if (u.by == null) return null;
            byte n = u.by.PlanNote;
            if ((n & RagdollPawn.RrAir) != 0) return "공중!";
            return (n & RagdollPawn.RrJumped) != 0 ? "이동 키 = 몰기 (곧게 가면 빨라지고, 꺾으면 느려져요)" : "이동 키 = 몰기  ·  점프 = 크게 튀기 (한 번)";
        }

        string RrLine(UltShow u) => $"빠르기 {RrSpeedOf(u.by, Params):0} m/s  ·  쓰러뜨린 적 {u.struckSet.Count}";

        string RrWarning(UltShow u, RagdollPawn me)
        {
            var s = Params;
            if (u.by == null || me == null || s == null || !(u.state is RrFx f) || !f.rolling || f.stood) return null;
            Vector3 d = Flat(me.Hips.position - u.by.Hips.position), dir = Flat(u.by.PlanDir).normalized;
            float along = Vector3.Dot(d, dir), across = Mathf.Abs(Vector3.Dot(d, new Vector3(dir.z, 0f, -dir.x)));
            return along > 0f && along < 30f && across < s.rrLength * 0.5f + 1f ? "룩이 굴러와요 — 옆으로 비켜요!" : null;
        }

        /// <summary>Before the key: enemies in the lane straight ahead along the camera's way (60 m), on the rook's screen.</summary>
        void RrPreview(RagdollPawn me, PawnRushSkillParams s, Camera cam, HashSet<RagdollPawn> wanted)
        {
            Vector3 dir = Flat(ViewAim(cam));
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            Vector3 right = new Vector3(dir.z, 0f, -dir.x), from = me.Hips.position;
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == me || !Teams.AreEnemies(e.Team, me.Team) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                Vector3 d = Flat(e.Hips.position - from);
                float along = Vector3.Dot(d, dir);
                if (along > 3f && along < 60f && Mathf.Abs(Vector3.Dot(d, right)) < s.rrLength * 0.5f + 0.5f) wanted.Add(e);
            }
        }
    }
}
