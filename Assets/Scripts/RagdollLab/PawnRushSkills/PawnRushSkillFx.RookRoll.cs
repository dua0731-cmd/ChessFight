using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 룩앤롤 on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.RookRoll.cs; the "!!" restyle after the
    /// spec's per-ultimate file): the rook on the open file, sweeping the seventh rank.
    ///
    ///   the column   the rook's model as it was at the key, baked once and drawn on its side, 3 m across and 4.5 m long,
    ///                turning with its speed (its own skin hidden meanwhile), teal neon rims on its round ends and a white line
    ///                flickering on its leading edge when it is fast; dust and grit off the ground behind it, the ground
    ///                shaking under whoever it passes, a rumble growing with its speed
    ///   the topple   its file and rank lit faintly for 1.5 s (the rook's lines), grey dust
    ///   the file     THE OPEN FILE ahead of it on the board, 2 squares wide and as long as 0.6 s of its run, bending with it:
    ///                teal on its side's screens, red with a red floor on the other side's, a red pool of light 6 m ahead;
    ///                on its own screen the edge speed lines
    ///   the pins     each capture a move of the rook on the seventh rank (R×a7, R×b7 … R×h7); four in a moment is the
    ///                STRIKE, its signature: the blow, the rook's own slow motion after it, "스트라이크!!", the pins' clatter
    ///   the rest     the rook lift (its jump: "룩 리프트!", a white square wave, a teal one where it lands), a crash into a
    ///                wall (grey: grit, a grey square wave, a boom, a punch on its own screen), a teammate launched ahead is
    ///                castling ("O-O!", a teal trail on it), standing up on a teal square with a tock
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class RrFx
        {
            public Mesh baked;
            public Material[] mats;
            public Matrix4x4 canon = Matrix4x4.identity;
            public float angle, width = 1f, length = 1f, nextRumble, nextDust, rimFlareAt = -10f;
            public bool rolling, stood, wasAir, pooled;
            public AudioSource loop;
            public CellMesh file, fileFill;
        }

        static readonly Vector2Int[] RrLines = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        string RrNota(UltShow u, string nota) => u.black ? Mirror(nota) : nota;

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
            DustRing(floor, 12, 1.2f, 6f, new Color(0.58f, 0.58f, 0.6f), 1f, 1f);
            UltSquareWave(floor, FamSquare * 0.5f, 9f, 0.6f, Teal, 3f);
            BoardLines(u, Cell(floor), RrLines, 10, Teal, 1.2f, 1.5f);   // its file and rank: the rook's lines
            UltSound(UltSfx.Boom, floor);
            RrRims(u, f);
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
                RrFileOff(f);
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
                    renderingLayerMask = skin != null ? skin.renderingLayerMask : 1u,   // (the stage's rim light on the column)
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
                if (fast > 0.5f) sparks.Emit(at, -dir * speed * 0.3f + Vector3.up * 2f + Random.insideUnitSphere, 0.04f, 0.4f, Tint(Random.value < 0.5f ? Color.white : TealPale, 0.2f));
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
                f.loop.pitch = (0.8f + 0.6f * fast) * UltSfxPitch;
            }
            // The rook lift's touchdown: a teal square wave, dust and a tock.
            if (f.wasAir && !air)
            {
                UltSquareWave(ground, FamSquare * 0.5f, 7f, 0.5f, Teal, 3f);
                DustRing(ground, 12, 1f, 5f, new Color(0.58f, 0.58f, 0.6f), 0.9f, 0.9f);
                UltClip(UltClipId.Tock, 0.6f * UltAudience(u, ground));
            }
            f.wasAir = air;
            RrDrawFile(u, f, s, center, dir, speed, cam);
            if (Viewer == u.by) UltEdgeLines(0.10f + 0.25f * Mathf.Clamp01((speed - 15f) / 25f));
        }

        /// <summary>The column's neon: a teal ring (1.5 m, ×3) on each round end turning with it, flaring brighter for 0.2 s at
        /// the rook lift; a white line flickering on its leading edge above 30 m/s.</summary>
        void RrRims(UltShow u, RrFx f)
        {
            var s = Params;
            if (s == null) return;
            BrdBuild();
            bool Rolling() => u.Playing && f.rolling && !f.stood && u.by != null && f.baked != null;
            Vector3 Dir() => Flat(u.by.PlanDir).sqrMagnitude > 1e-4f ? Flat(u.by.PlanDir).normalized : Vector3.forward;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Add(new Shape(root, "Rr rim", meshQuad, matRing)
                {
                    life = 1e9f,
                    animate = (sh, t) =>
                    {
                        if (!u.Playing || f.stood) { sh.life = 0f; return; }
                        if (!Rolling()) { sh.bright = 0f; return; }
                        Vector3 dir = Dir(), right = new Vector3(dir.z, 0f, -dir.x);
                        sh.at = u.by.Hips.position + right * (side * s.rrLength * 0.5f);
                        sh.rotation = Quaternion.LookRotation(right, Vector3.up) * Quaternion.Euler(0f, 0f, f.angle);
                        float r = s.rrRadius / 0.86f * 2f;
                        sh.scale = new Vector3(r, r, 1f);
                        sh.color = HdrA(Teal, Clock - f.rimFlareAt < 0.2f ? 4f : 3f);
                        sh.bright = 1f;
                    },
                });
            }
            Add(new Shape(root, "Rr leading edge", meshCylinder, matBrdLine)
            {
                life = 1e9f,
                animate = (sh, t) =>
                {
                    if (!u.Playing || f.stood) { sh.life = 0f; return; }
                    if (!Rolling() || RrSpeedOf(u.by, s) < 30f) { sh.bright = 0f; return; }
                    Vector3 dir = Dir(), right = new Vector3(dir.z, 0f, -dir.x);
                    sh.at = u.by.Hips.position + dir * (s.rrRadius * 0.98f) - right * (s.rrLength * 0.5f);
                    sh.rotation = Quaternion.FromToRotation(Vector3.up, right);
                    sh.scale = new Vector3(0.03f, s.rrLength, 0.03f);
                    sh.color = HdrA(Color.white, 3f);
                    sh.bright = 0.6f + 0.4f * Mathf.Sin(Clock * 47f);
                },
            });
        }

        /// <summary>THE OPEN FILE ahead (every screen): the board squares 2 wide (4.4 m) along its heading for 0.6 s of its run
        /// (9-24 m), snapped to the board and bending with it - teal ×1.6 on its side's screens, red ×2 with a red floor (0.3)
        /// on the other side's - and a red pool of light 6 m ahead (half on its side's screens).</summary>
        void RrDrawFile(UltShow u, RrFx f, PawnRushSkillParams s, Vector3 center, Vector3 dir, float speed, Camera cam)
        {
            var me = Viewer;
            bool ours = me == null || !Teams.AreEnemies(me.Team, u.by.Team);
            if (f.file == null) f.file = BrdMesh("Rr open file", ours ? matFrame : matBrdFrameRed);
            if (f.fileFill == null && !ours) f.fileFill = BrdMesh("Rr open file floor", matBrdFillRed);
            if (!f.pooled)
            {
                f.pooled = true;
                UltWarnPool(u, () => u.by != null ? u.by.Hips.position + Flat(u.by.PlanDir).normalized * 6f - Vector3.up * s.rrRadius : center, 3.3f, 0.3f, 60f,
                            () => u.Playing && f.rolling && !f.stood, 8f, 0.5f);
            }
            float floorY = center.y - s.rrRadius, len = Mathf.Clamp(Mathf.Max(speed, 15f) * 0.6f, 9f, 24f);
            Vector3 right = new Vector3(dir.z, 0f, -dir.x);
            f.file.Begin(cam);
            f.fileFill?.Begin(cam);
            rrSeen.Clear();
            for (float x = s.rrRadius; x <= len + s.rrRadius; x += 1f)
                for (float across = -FamSquare; across <= FamSquare + 0.01f; across += FamSquare)
                {
                    var c = Cell(center + dir * x + right * across);
                    if (!rrSeen.Add(c)) continue;
                    float fy = BoardFloor(u, c, floorY);
                    if (float.IsNaN(fy)) continue;
                    Vector3 p = CellFlat(c);
                    p.y = fy;
                    float k = 1f - Smooth01((x - len * 0.7f) / (len * 0.3f + 1f));
                    f.file.Quad(p + Vector3.up * 0.05f, FamSquare * 0.96f, BrdTimes(ours ? Teal : Blood, ours ? 1.6f : 2f), k);
                    if (f.fileFill != null)
                    {
                        BrdFillColour(Blood, 0.9f, 0.3f, k, out var rgb, out var alpha);
                        f.fileFill.Quad(p + Vector3.up * 0.035f, FamSquare * 0.96f, rgb, alpha);
                    }
                }
            f.file.End();
            f.fileFill?.End();
        }

        readonly HashSet<Vector2Int> rrSeen = new HashSet<Vector2Int>();

        void RrFileOff(RrFx f)
        {
            BrdKill(f.file);
            BrdKill(f.fileFill);
            f.file = f.fileFill = null;
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
                    // The rook lift: its rims flare, a white square wave where it takes off.
                    f.rimFlareAt = Clock;
                    DustRing(floor, 12, 1f, 5f, new Color(0.58f, 0.58f, 0.6f), 0.9f, 0.9f);
                    UltSquareWave(floor, FamSquare * 0.5f, 7f, 0.5f, Color.white, 1.2f);
                    UltPopWord(e.at + Vector3.up * 3f, "룩 리프트!", HdrA(Teal, 1.3f), 0.7f, 0.9f);
                    UltSound(UltSfx.Leap, e.at, 4);
                    break;
                case RagdollPawn.RrCrash:
                    // Grey, never red: grit, a grey square wave, the boom; a punch on its own screen.
                    DustRing(floor, 20, 1.4f, 7f, new Color(0.58f, 0.58f, 0.6f), 1f, 1.1f);
                    SparkBurst(e.at + Vector3.up, 20, Color.white, -e.dir, 70f, 4f, 12f, 0.7f, 0.04f);
                    SparkBurst(e.at + Vector3.up, 20, new Color(0.7f, 0.7f, 0.72f), -e.dir, 70f, 4f, 12f, 0.7f, 0.04f);
                    UltSquareWave(floor, FamSquare * 0.5f, 8f, 0.5f, new Color(0.6f, 0.6f, 0.62f), 1f);
                    UltPlay(sfxImpactLow, 0.9f, 0f, 1.2f);
                    UltPlay(clipWall, 0.8f);
                    UltFrames(ImpactSeq.Best, e.at, u);
                    {
                        var cam = ViewCamera;
                        float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, e.at) / 30f) : 0.5f;
                        Shake(Viewer == u.by ? 0.25f : 0.2f * near, 0.5f);
                    }
                    break;
                case RagdollPawn.RrLift:
                    // A teammate launched ahead: castling.
                    if (e.target != null)
                    {
                        var mate = e.target;
                        UltPopWord(HeadOf(mate) + Vector3.up * 1.2f, "O-O!", HdrA(Teal, 1.4f), 0.8f, 0.9f);
                        float until = Clock + 0.6f;
                        anims.Add(new Streak(root, matTrail, HdrA(Teal, 2.2f), 0.45f, 0.6f, () => mate != null && Clock < until ? mate.Hips.position : (Vector3?)null));
                    }
                    UltMove(u, "O-O!", e.at);
                    UltClip(UltClipId.Castle, 0.8f * UltAudience(u, e.at));
                    break;
                case RagdollPawn.RrStrike:
                    // THE STRIKE, the signature: four pins in a moment.
                    UltBlow(u, ImpactSeq.Blow, e.at + Vector3.up * 1.5f);
                    UltSlow(u, UltSlowKind.SigAftermath);
                    UltTopWord("스트라이크!!", HdrA(Teal, 1.2f), 56);
                    UltBlowLight(e.at + Vector3.up * 3f);
                    for (int i = 0; i < 6; i++) UltPlay(sfxClack, 0.8f, i * 0.045f, 0.85f + i * 0.07f);
                    UltSquareWave(floor, FamSquare * 0.5f, 16f, 0.9f, Teal, 3f);
                    break;
                case RagdollPawn.RrStand:
                    f.stood = true;
                    RrFileOff(f);
                    DustRing(floor, 12, 1f, 5f, new Color(0.58f, 0.58f, 0.6f), 0.9f, 0.9f);
                    UltSquareWave(floor, FamSquare * 0.5f, 6f, 0.5f, Teal, 3f);
                    SquareMark(u, Cell(floor), Teal, 1.15f, 0.42f, TealPale, 3.2f, null, 0.9f, null, floor.y);
                    UltClip(UltClipId.Tock, 0.6f * UltAudience(u, floor));
                    break;
            }
        }

        /// <summary>A pin, on every screen: the rook's capture on the seventh rank (R×a7, R×b7 … by how many it has bowled),
        /// a burst of white sparks, the pin's clack. The capture itself is the common one.</summary>
        void RrStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null) return;
            Vector3 at = ChestOf(e.target);
            SparkBurst(at, 16, Color.white, e.dir, 70f, 4f, 12f, 0.5f, 0.04f);
            int pin = Mathf.Max(1, e.count);
            UltMove(u, RrNota(u, $"R×{(char)('a' + (pin - 1) % 8)}7"), at);
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
