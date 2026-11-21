using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 롱 다이애거널 (a1–h8!!) on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.GreatDiagonal.cs): one
    /// diagonal of light across the whole board.
    ///
    ///   gathering   a ring of light closing on the bishop's chest (a chime when it is ready); the line it would take - teal
    ///               on the bishop's screen, faint white on the others' - with its right-angle turns; a red aim mark on the
    ///               chest of every enemy standing on it, and on that enemy's screen a warning
    ///   the shot    a white core in a teal glow along every stretch, the board's squares lighting along it on the floor as
    ///               it goes; a flash and a square wave at each turn; every skewered enemy red, pulled back along the light
    ///               with a red streak
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class GdFx
        {
            public readonly Vector3[] path = new Vector3[RagdollPawn.GdBounces + 2];
            public int count;
            public readonly HashSet<RagdollPawn> onLine = new HashSet<RagdollPawn>();
            public readonly Dictionary<RagdollPawn, Shape> marks = new Dictionary<RagdollPawn, Shape>();
            public float gatherAt = -1f;
            public bool chimed;
        }

        static bool GdChargingNow(UltShow u) => u.by != null && u.Playing && (u.by.PlanNote & RagdollPawn.GdCharging) != 0;

        void GdCast(UltShow u)
        {
            var f = new GdFx();
            u.state = f;
            bool mine = Viewer == u.by;
            // The line it would take: a thin beam a stretch.
            for (int i = 0; i <= RagdollPawn.GdBounces; i++)
            {
                int k = i;
                Add(new Shape(root, "Gd aim line", meshCylinder, matPillar)
                {
                    life = 1e9f,
                    animate = (s, t) =>
                    {
                        if (!u.Playing) { s.life = 0f; return; }
                        bool show = GdChargingNow(u) && k + 1 < f.count;
                        s.bright = show ? (mine ? 0.9f : 0.35f) * (0.8f + 0.2f * Mathf.Sin(Clock * 14f)) : 0f;
                        if (!show) return;
                        Vector3 a = f.path[k], b = f.path[k + 1], ab = b - a;
                        float len = ab.magnitude;
                        if (len < 0.05f) { s.bright = 0f; return; }
                        s.at = a;
                        s.rotation = Quaternion.FromToRotation(Vector3.up, ab / len);
                        float w = mine ? 0.05f : 0.035f;
                        s.scale = new Vector3(w, len, w);
                        s.color = mine ? HdrA(Teal, 2.4f) : new Color(1.3f, 1.3f, 1.4f);
                    },
                });
            }
            // The gathering: a ring of light closing on the chest.
            Add(new Shape(root, "Gd gather ring", meshQuad, matRing)
            {
                life = 1e9f, billboard = true,
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool on = GdChargingNow(u);
                    if (!on) { f.gatherAt = -1f; f.chimed = false; s.bright = 0f; return; }
                    if (f.gatherAt < 0f) f.gatherAt = Clock;
                    float need = Params != null ? Params.gdCharge : 0.5f, k = Mathf.Clamp01((Clock - f.gatherAt) / need);
                    bool ready = (u.by.PlanNote & RagdollPawn.GdCharged) != 0 || k >= 1f;
                    if (ready && !f.chimed)
                    {
                        f.chimed = true;
                        Flare(u.by.GdMuzzle, TealPale, 1.6f, 4f, 0.2f);
                        UltSound(UltSfx.Perfect, u.by.GdMuzzle);
                    }
                    float size = Mathf.Lerp(2.6f, 0.5f, Smooth01(k));
                    s.at = u.by.GdMuzzle;
                    s.scale = new Vector3(size, size, 1f);
                    s.color = ready ? HdrA(Teal, 3f) : HdrA(Color.white, 1.6f);
                    s.bright = ready ? 0.9f + 0.3f * Mathf.Sin(Clock * 20f) : 0.5f + 0.5f * k;
                    if (Random.value < 0.6f)
                    {
                        Vector3 from = s.at + Random.onUnitSphere * size * 0.6f;
                        motes.Emit(from, (s.at - from) * 3f, Random.Range(0.05f, 0.1f), 0.3f, Tint(ready ? Teal : Color.white, 0.2f));
                    }
                },
            });
        }

        void GdStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is GdFx f) || u.by == null) return;
            var s = Params;
            f.onLine.Clear();
            if (s == null || !GdChargingNow(u)) return;
            // The bishop's own screen from its own look (no lag); the others' from the way the host was told.
            Vector3 way = Viewer == u.by && cam != null ? ViewAim(cam) : u.by.PlanDir;
            f.count = RagdollPawn.GdPath(u.by.GdMuzzle, way, s.gdLength, f.path);
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == u.by || !Teams.AreEnemies(e.Team, u.by.Team) || e.State != PawnState.Active || u.struckSet.Contains(e)) continue;
                if (!RagdollPawn.GdOnLine(s, f.path, f.count, e.Hips.position, out _)) continue;
                f.onLine.Add(e);
                if (!f.marks.ContainsKey(e)) f.marks[e] = GdMark(u, f, e);
            }
        }

        /// <summary>The red aim on the chest of an enemy standing on the line while it gathers.</summary>
        Shape GdMark(UltShow u, GdFx f, RagdollPawn e) => Add(new Shape(root, "Gd aim mark", meshQuad, matRing)
        {
            life = 1e9f, billboard = true,
            animate = (s, t) =>
            {
                if (!u.Playing || e == null) { s.life = 0f; return; }
                bool on = f.onLine.Contains(e);
                s.bright = on ? 1f + 0.4f * Mathf.Sin(Clock * 24f) : 0f;
                if (!on) return;
                s.at = ChestOf(e);
                s.scale = new Vector3(0.42f, 0.42f, 1f);
                s.color = HdrA(Blood, 3f);
                if (Random.value < 0.3f) sparks.Emit(s.at, Random.insideUnitSphere * 0.3f, 0.03f, 0.2f, Tint(Blood, 0.1f));
            },
        });

        /// <summary>Before the key: whoever stands on the line the camera looks along now, on the bishop's screen.</summary>
        void GdPreview(RagdollPawn me, PawnRushSkillParams s, Camera cam, HashSet<RagdollPawn> wanted)
        {
            int count = RagdollPawn.GdPath(me.GdMuzzle, ViewAim(cam), s.gdLength, gdPreviewPath);
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == me || !Teams.AreEnemies(e.Team, me.Team) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                if (RagdollPawn.GdOnLine(s, gdPreviewPath, count, e.Hips.position, out _)) wanted.Add(e);
            }
        }

        readonly Vector3[] gdPreviewPath = new Vector3[RagdollPawn.GdBounces + 2];

        /// <summary>A stretch of the shot: the light along it, the squares on the floor, a flash where it turned.</summary>
        void GdLineFx(UltShow u, SkillFxEvent e)
        {
            int seg = e.count & 3;
            Vector3 a = e.at, b = e.normal, ab = b - a;
            float len = ab.magnitude, width = Mathf.Max(0.5f, e.size);
            if (len < 0.1f) return;
            Vector3 d = ab / len;
            const float Speed = 420f;   // m/s: how fast the light runs out
            float wait = seg * 0.04f, grow = len / Speed;
            // The core and the glow.
            for (int layer = 0; layer < 2; layer++)
            {
                bool core = layer == 0;
                Add(new Shape(root, core ? "Gd beam core" : "Gd beam glow", meshCylinder, matPillar)
                {
                    life = wait + grow + 1.1f, at = a, rotation = Quaternion.FromToRotation(Vector3.up, d),
                    animate = (s, t) =>
                    {
                        float age = s.Age - wait;
                        if (age < 0f) { s.bright = 0f; return; }
                        float k = Mathf.Clamp01(age / Mathf.Max(0.01f, grow)), fade = 1f - Mathf.Clamp01((age - grow - 0.15f) / 0.9f);
                        float r = (core ? width * 0.28f : width) * (1f + 0.25f * Mathf.Exp(-age / 0.1f));
                        s.scale = new Vector3(r * fade + 0.01f, len * k, r * fade + 0.01f);
                        s.color = core ? new Color(3f, 3f, 3.2f) : HdrA(Teal, 2.2f);
                        s.bright = fade * (core ? 1f : 0.7f);
                    },
                });
            }
            // The board's squares lighting along it, on the floor, as the light passes.
            float sq = FamSquare;
            for (float x = sq; x < len; x += sq * 2f)
            {
                Vector3 p = a + d * x;
                var floor = RagdollPawn.DriveFloor(p, 2f, 6f);
                if (!floor.HasValue) continue;
                float delay = wait + x / Speed;
                Vector3 at = floor.Value + Vector3.up * 0.05f;
                Add(new Shape(root, "Gd lit square", meshQuad, matTile)
                {
                    life = delay + 0.8f, at = at, rotation = Quaternion.LookRotation(Vector3.down, d), scale = new Vector3(sq * 0.94f, sq * 0.94f, 1f), paint = 0.4f,
                    animate = (s, t) =>
                    {
                        float age = s.Age - delay;
                        if (age < 0f) { s.bright = 0f; return; }
                        s.color = HdrA(Color.Lerp(Color.white, Teal, Mathf.Clamp01(age / 0.25f)), 1.8f);
                        s.bright = Mathf.Clamp01(1f - age / 0.8f);
                    },
                });
            }
            if (seg == 0)
            {
                Flare(a, Color.white, 4f, 9f, 0.25f);
                SparkBurst(a, 30, TealPale, d, 25f, 6f, 14f, 0.4f, 0.04f);
                UltSound(UltSfx.Boom, a);
                UltPlay(sfxPerfect, 0.6f, 0f, 1.5f);
                if (Viewer == u.by) Shake(0.12f, 0.25f);
            }
            else
            {
                // A turn off a wall: the flash, a square wave, a bright ping.
                Flare(a, TealPale, 3.5f, 8f, 0.3f);
                SparkBurst(a, 40, Color.white, -d, 70f, 4f, 12f, 0.5f, 0.05f);
                UltSquareWave(RagdollPawn.DriveFloor(a, 2f, 6f) ?? a, sq * 0.5f, 7f, 0.5f, Teal, 2.6f);
                UltPlay(sfxChime[Mathf.Clamp(seg * 2 + 3, 0, sfxChime.Length - 1)], 0.8f);
            }
            var cam = ViewCamera;
            if (cam != null && Viewer != u.by)
            {
                // Whoever the light passes near feels it.
                float near = DistanceToSegment(cam.transform.position, a, b);
                if (near < 12f) Shake(0.08f * (1f - near / 12f), 0.25f);
            }
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>An enemy skewered: red, and a red streak as the light drags it back.</summary>
        void GdStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null) return;
            var target = e.target;
            Vector3 at = ChestOf(target);
            Flare(at, Blood, 3f, 6f, 0.35f);
            SparkBurst(at, 30, Blood, e.dir, 60f, 3f, 9f, 0.5f, 0.05f);
            float until = Clock + 0.9f;
            anims.Add(new Streak(root, matTrail, HdrA(Blood, 2.6f), 0.5f, 0.35f, () => target != null && Clock < until ? ChestOf(target) : (Vector3?)null));
            UltPopWord(HeadOf(target) + Vector3.up * 1f, u.struckSet.Count >= 2 ? $"꿰뚫음 ×{u.struckSet.Count}" : "꿰뚫음!", HdrA(Blood, 1.5f), 0.55f, 1.1f);
            UltSound(UltSfx.Stomp, at, e.count);
            UltScreenStrike(u, e);
        }

        string GdHint(UltShow u)
        {
            if (u.by == null) return null;
            byte n = u.by.PlanNote;
            if ((n & RagdollPawn.GdCharged) != 0) return "떼면 발사!  (벽에 닿으면 직각으로 꺾여요, 두 번까지)";
            if ((n & RagdollPawn.GdCharging) != 0) return "모으는 중…  (0.5초)";
            return "좌클릭 누르고 0.5초 모았다 떼기 = 300 m 빛의 대각선";
        }

        string GdLine(UltShow u)
        {
            int shots = u.by != null ? u.by.PlanNote & RagdollPawn.GdShotsMask : 0;
            return $"남은 빛 {shots}발  ·  꿰인 적 {u.struckSet.Count}";
        }

        string GdWarning(UltShow u, RagdollPawn me) =>
            u.state is GdFx f && f.onLine.Contains(me) ? "빛의 대각선이 겨눠요 — 줄에서 엇갈려 비켜요!" : null;
    }
}
