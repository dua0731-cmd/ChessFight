using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The support slot on every screen (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.Support.cs; KITS 6.1-6.5 "지원" and
    /// 6.10). Drawn from the moments and the pieces' bodies, in the presentation's language: teal for what helps, red where it
    /// is cut; made of the leader's own piece (baked from its skin) and light, no props.
    ///
    ///   knight   the empty horse - the leader's knight drawn in teal light, bigger - running to the one called, then under
    ///            it as it rides, a teal arc as it leaps
    ///   rook     the drawbridge: stone planks laid from the leader's side to the one called (1 s), teal chevrons running
    ///            along it toward the leader, two chains from the leader's hands while it holds them
    ///   queen    the rope of light shot up from the leader's hand to 15 m over the one called, then the whole rope down to
    ///            the leader; the crown pulley over the one sliding
    ///   bishop   the mitre drone - the leader's bishop, small, in teal light - flying over the one called, then over it as
    ///            it hangs, two lines down to its hands
    ///   pawn     the stone pawns - the leader's pawn in stone - rising one after another along the chain, toppling where an
    ///            enemy struck one, a tock at every throw
    ///   words    for the one called "선두가 부른다!", what comes and how to take it, the time left; for the one carried and
    ///            the leader what their keys do now; "구조 !!" on every screen when it arrives, the two tied by a teal light
    ///            while their reward lasts
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class SupFx
        {
            public RagdollPawn by, mate;
            public PieceKind kind;
            public float callReal, goReal = -1f, endReal = -1f, answer = 5f, jumpReal = -10f;
            public Vector3 at, far, dir, horse, drone;
            public bool going, cut, saved;
            public Mesh baked;
            public Matrix4x4 canon = Matrix4x4.identity;
            public float tall = 1f;
            public readonly List<Vector3> chain = new List<Vector3>();
            public readonly List<float> down = new List<float>();
        }

        readonly List<SupFx> sups = new List<SupFx>();
        readonly List<(RagdollPawn a, RagdollPawn b, float until)> supTethers = new List<(RagdollPawn, RagdollPawn, float)>();
        // (Made on first use: Unity allows no MaterialPropertyBlock in a component's field initializers - one here once
        // cut the whole effects component's construction short, and every ultimate's effects with it.)
        MaterialPropertyBlock supBlock;
        Material matSupRim;
        float supSavedReal = -10f;
        string supSavedLine = "";

        /// <summary>A support of this team under way (as this PC has seen it: the G card does not light meanwhile).</summary>
        public bool SupportBusy(int team)
        {
            foreach (var f in sups)
                if (f.endReal < 0f && f.by != null && f.by.Team == team) return true;
            return false;
        }

        SupFx SupOf(RagdollPawn by)
        {
            for (int i = sups.Count - 1; i >= 0; i--) if (sups[i].by == by && sups[i].endReal < 0f) return sups[i];
            return null;
        }

        // ---------------------------------------------------------------- the moments

        void SupportFx(SkillFxEvent e)
        {
            switch (e.kind)
            {
                case SkillFxKind.SupCall: SupCall(e); break;
                case SkillFxKind.SupGo: SupGo(e); break;
                case SkillFxKind.SupStep: SupStep(e); break;
                case SkillFxKind.SupCut: SupEnd(e, true, false); break;
                case SkillFxKind.SupCancel: SupEnd(e, false, false); break;
                case SkillFxKind.SupSaved: SupEnd(e, false, true); break;
            }
        }

        void SupCall(SkillFxEvent e)
        {
            if (e.by == null) return;
            var old = SupOf(e.by);
            if (old != null) old.endReal = RealClock;
            BuildUltParts();
            BrdBuild();
            BuildPrevizParts();
            if (matSupRim == null) matSupRim = Glow(Texture2D.whiteTexture, rim: 0.95f, rimPower: 2.2f);
            var f = new SupFx
            {
                by = e.by, mate = e.target, kind = (PieceKind)e.count, callReal = RealClock, answer = Mathf.Max(0.5f, e.size),
                at = e.at, far = e.normal, dir = e.dir, horse = e.at, drone = e.at,
            };
            if (f.kind == PieceKind.Knight || f.kind == PieceKind.Bishop || f.kind == PieceKind.Pawn) SupBake(f);
            if (f.kind == PieceKind.Pawn)
            {
                RagdollPawn.SupportChainPoints(e.at, e.normal, Params, f.chain);
                for (int i = 0; i < f.chain.Count; i++) f.down.Add(-1f);
            }
            sups.Add(f);
            UltAudioBuild();
            float lvl = SupHeard(f, e.at);
            switch (f.kind)
            {
                case PieceKind.Knight: UltPlay(sfxWhistle, 0.7f * lvl, 0f, 1.7f); break;
                case PieceKind.Rook: UltPlay(clipWall, 0.8f * lvl, 0f, 0.7f); UltClip(UltClipId.Castle, 0.7f * lvl, 0.8f); break;
                case PieceKind.Queen: UltPlay(sfxPerfect, 0.7f * lvl, 0f, 1.3f); break;
                case PieceKind.Bishop: UltPlay(sfxPerfect, 0.6f * lvl, 0f, 0.8f); break;
                default: UltClip(UltClipId.Tock, 0.6f * lvl, 0.9f); UltClip(UltClipId.Tock, 0.6f * lvl, 1.1f, 0.12f); break;
            }
            UltClip(UltClipId.ClockPress, 0.5f * lvl);
        }

        float SupHeard(SupFx f, Vector3 at)
        {
            var me = Viewer;
            if (me != null && (me == f.by || me == f.mate)) return 1f;
            return 0.6f * UltHeard(at);
        }

        /// <summary>The leader's own piece baked once (the horse, the mitre, the stone pawn are made of it), its foot at the
        /// origin.</summary>
        void SupBake(SupFx f)
        {
            var skin = f.by != null ? f.by.skin : null;
            if (skin == null) return;
            f.baked = new Mesh { name = "Support piece", hideFlags = HideFlags.HideAndDontSave };
            skin.BakeMesh(f.baked, true);
            var verts = new List<Vector3>();
            f.baked.GetVertices(verts);
            if (verts.Count == 0) return;
            var white = new Color32[verts.Count];
            for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
            f.baked.SetColors(white);   // (the glow shader multiplies the vertex colour in)
            // Turned as in the world (the skin's rotation), then back by the piece's facing: its front along +z.
            Vector3 face = Flat(f.by.Facing);
            Quaternion inv = Quaternion.Inverse(Quaternion.LookRotation(face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward, Vector3.up))
                             * skin.transform.rotation;
            float lo = float.MaxValue, hi = float.MinValue, x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var v in verts)
            {
                Vector3 w = inv * v;
                lo = Mathf.Min(lo, w.y);
                hi = Mathf.Max(hi, w.y);
                x0 = Mathf.Min(x0, w.x);
                x1 = Mathf.Max(x1, w.x);
                z0 = Mathf.Min(z0, w.z);
                z1 = Mathf.Max(z1, w.z);
            }
            f.tall = Mathf.Max(0.2f, hi - lo);
            f.canon = Matrix4x4.Translate(-new Vector3((x0 + x1) * 0.5f, lo, (z0 + z1) * 0.5f)) * Matrix4x4.Rotate(inv);
        }

        void SupGo(SkillFxEvent e)
        {
            var f = e.by != null ? SupOf(e.by) : null;
            if (e.by != null && e.count >= 0) RagdollPawn.NoteSupportUsed(e.by.Team, e.count);
            if (f == null || e.count < 0) return;   // (a rider along a bridge rides the bridge as drawn)
            f.going = true;
            f.goReal = RealClock;
            float lvl = SupHeard(f, e.at);
            UltClip(UltClipId.Tock, 0.7f * lvl);
            UltPlay(sfxChime[3], 0.6f * lvl);
            if (f.kind == PieceKind.Queen) UltClip(UltClipId.Whoosh, 0.6f * lvl);
        }

        void SupStep(SkillFxEvent e)
        {
            var f = e.by != null ? SupOf(e.by) : null;
            if (f == null) return;
            float lvl = SupHeard(f, e.at);
            if (f.kind == PieceKind.Pawn)
            {
                if (e.size < 0f)
                {
                    // A stone pawn toppled: the gap.
                    if (e.count >= 0 && e.count < f.down.Count) f.down[e.count] = RealClock;
                    DustRing(Ground(e.at), 10, 0.6f, 4f, new Color(0.58f, 0.58f, 0.6f), 0.7f, 0.7f);
                    UltClip(UltClipId.Crack, 0.6f * lvl);
                    return;
                }
                DustRing(Ground(e.at), 6, 0.4f, 3f, new Color(0.58f, 0.58f, 0.6f), 0.5f, 0.6f);
                UltClip(UltClipId.Tock, 0.6f * lvl, 0.9f + 0.05f * e.count);
                return;
            }
            if (e.count == 2)
            {
                // The drone set the one hanging down short of the leader: that support is over.
                f.endReal = RealClock;
                UltSquareWave(Ground(e.at), FamSquare * 0.5f, 4f, 0.4f, Teal, 2f);
                UltClip(UltClipId.Tock, 0.5f * lvl, 0.8f);
                return;
            }
            if (e.count == 1)
            {
                // The horse's leap.
                f.jumpReal = RealClock;
                UltSound(UltSfx.Leap, e.at);
                UltSquareWave(Ground(e.at), FamSquare * 0.5f, 5f, 0.4f, Teal, 3f);
                if (f.mate != null)
                {
                    var rider = f.mate;
                    float until = Clock + 0.6f;
                    anims.Add(new Streak(root, matTrail, HdrA(Teal, 2.2f), 0.45f, 0.5f, () => rider != null && Clock < until ? rider.Hips.position : (Vector3?)null));
                }
            }
        }

        void SupEnd(SkillFxEvent e, bool cut, bool saved)
        {
            var f = e.by != null ? SupOf(e.by) : null;
            if (f != null && f.mate != null && e.target != null && e.target != f.mate) f = null;   // (a rider along the bridge)
            if (f != null)
            {
                f.endReal = RealClock;
                f.cut = cut;
                f.saved = saved;
            }
            if (cut)
            {
                Flare(e.at + Vector3.up, Blood, 2f, 6f, 0.3f);
                SparkBurst(e.at + Vector3.up, 16, Blood, Vector3.up, 70f, 3f, 8f, 0.5f, 0.04f);
                UltPopWord(e.at + Vector3.up * 2f, "끊김!", HdrA(Blood, 1.6f), 0.55f, 0.9f);
                UltClip(UltClipId.Crack, 0.7f * (f != null ? SupHeard(f, e.at) : UltHeard(e.at)));
            }
            if (!saved || e.by == null || e.target == null) return;
            // Arrived: "구조 !!" on every screen, the two tied by a teal light while their reward lasts.
            supSavedReal = RealClock;
            supSavedLine = $"{e.by.DisplayName} → {e.target.DisplayName}  ·  팀 궁극 게이지 +{e.size * 100f:0}%  ·  구조 {e.count}";
            supTethers.Add((e.by, e.target, RealClock + (Params != null ? Params.supBoost : 4f)));
            UltSquareWave(Ground(e.at), FamSquare * 0.5f, 10f, 0.7f, Teal, 3f);
            SparkBurst(e.at + Vector3.up, 20, TealPale, Vector3.up, 60f, 4f, 10f, 0.6f, 0.04f);
            float lvl = f != null ? SupHeard(f, e.at) : UltHeard(e.at);
            UltPlay(sfxGrade[1], 0.8f * lvl);
            UltClip(UltClipId.Shimmer, 0.5f * lvl);
        }

        // ---------------------------------------------------------------- every frame (UltStep)

        void SupportStepFx(Camera cam)
        {
            for (int i = sups.Count - 1; i >= 0; i--)
            {
                var f = sups[i];
                float gone = f.endReal >= 0f ? RealClock - f.endReal : -1f;
                if (f.by == null || gone > 0.8f || RealClock - f.callReal > 40f)
                {
                    if (f.baked != null) Destroy(f.baked);
                    sups.RemoveAt(i);
                    continue;
                }
                // An answer that never came, as this PC sees it: the call fades after its time (the host's moment ends it too).
                if (!f.going && f.endReal < 0f && RealClock - f.callReal > f.answer + 0.5f) f.endReal = RealClock;
                float k = gone < 0f ? 1f : 1f - Mathf.Clamp01(gone / 0.6f);
                switch (f.kind)
                {
                    case PieceKind.Knight: SupHorse(f, k, cam); break;
                    case PieceKind.Rook: SupBridge(f, k, cam); break;
                    case PieceKind.Queen: SupRope(f, k, cam); break;
                    case PieceKind.Bishop: SupDrone(f, k, cam); break;
                    default: SupChain(f, k, cam); break;
                }
            }
            for (int i = supTethers.Count - 1; i >= 0; i--)
            {
                var (a, b, until) = supTethers[i];
                if (a == null || b == null || RealClock > until)
                {
                    supTethers.RemoveAt(i);
                    continue;
                }
                float k = Mathf.Clamp01((until - RealClock) / 0.4f);
                SupLine(ChestOf(a), ChestOf(b), HdrA(Teal, 2.4f), 0.035f, k, cam);
            }
        }

        /// <summary>The leader's baked piece drawn at <paramref name="feet"/>, <paramref name="height"/> tall, facing
        /// <paramref name="face"/>: in teal rim light, or in stone.</summary>
        void SupDrawPiece(SupFx f, Vector3 feet, Vector3 face, float height, bool stone, float k, Camera cam, Quaternion tilt = default)
        {
            if (f.baked == null || k <= 0.01f || cam == null) return;
            face = Flat(face).sqrMagnitude > 1e-4f ? Flat(face).normalized : Vector3.forward;
            Quaternion turn = Quaternion.LookRotation(face, Vector3.up) * (tilt == default ? Quaternion.identity : tilt);
            float scale = height / Mathf.Max(0.2f, f.tall);
            Matrix4x4 m = Matrix4x4.TRS(feet, turn, Vector3.one * scale) * f.canon;
            var mat = stone ? matStone : matSupRim;
            if (mat == null) return;
            if (supBlock == null) supBlock = new MaterialPropertyBlock();
            supBlock.Clear();
            if (!stone) supBlock.SetColor("_Color", HdrA(Teal, 1.6f) * k);
            var rp = new RenderParams(mat)
            {
                layer = 0,
                shadowCastingMode = stone ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = stone,
                worldBounds = new Bounds(feet + Vector3.up * height * 0.5f, Vector3.one * (height * 1.6f + 1f)),
                matProps = stone ? null : supBlock,
            };
            for (int i = 0; i < f.baked.subMeshCount; i++) Graphics.RenderMesh(rp, f.baked, i, m);
        }

        /// <summary>A thin line of light between two points this frame (a chain, a rope, a tether).</summary>
        void SupLine(Vector3 a, Vector3 b, Color c, float radius, float k, Camera cam)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f || k <= 0.01f) return;
            var shape = Add(new Shape(root, "Support line", meshCylinder, matBrdLine)
            {
                life = Time.deltaTime * 1.5f + 0.001f, at = a, rotation = Quaternion.FromToRotation(Vector3.up, d / len), scale = new Vector3(radius, len, radius),
                color = c, bright = k,
            });
            shape.tick = UltTick.Real;
        }

        void SupHorse(SupFx f, float k, Camera cam)
        {
            var mate = f.mate;
            if (mate == null) return;
            var s = Params;
            float rideUp = 0.6f;
            if (!f.going)
            {
                // The empty horse running up to the one called, then waiting by it.
                Vector3 goal = mate.FeetPoint + FlatDir(f.at - mate.FeetPoint) * 1.4f;
                Vector3 before = f.horse;
                f.horse = Vector3.MoveTowards(f.horse, goal, (s != null ? s.supHorseGo : 60f) * realDt);
                var g = BrdGroundBelow(f.horse + Vector3.up * 2f, 6f);
                if (!float.IsNaN(g)) f.horse.y = g;
                SupDrawPiece(f, f.horse, Flat(goal - before).sqrMagnitude > 1e-4f ? goal - before : mate.FeetPoint - f.horse, 2.3f, false, k, cam);
                return;
            }
            // Under the rider.
            Vector3 hips = mate.Hips.position;
            Vector3 feet = hips - Vector3.up * (mate.standHeight + rideUp);
            SupDrawPiece(f, feet - mate.Facing.normalized * 0.2f, mate.Facing, 2.3f, false, k, cam);
        }

        static Vector3 FlatDir(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        void SupBridge(SupFx f, float k, Camera cam)
        {
            var s = Params;
            Vector3 farEnd = f.at, near = f.far, d = near - farEnd;
            float len = d.magnitude;
            if (len < 0.5f) return;
            Vector3 dir = d / len;
            float open = Mathf.Clamp01((RealClock - f.callReal) / Mathf.Max(0.1f, s != null ? s.supBridgeOpen : 1f));
            float width = s != null ? s.supBridgeWidth : 3f;
            int planks = Mathf.Clamp(Mathf.CeilToInt(len / 3f), 1, 60);
            // Laid from the leader's side out to the one called (each plank drops in as the bridge reaches it).
            for (int i = 0; i < planks; i++)
            {
                float x = len - (i + 0.5f) * len / planks;          // from the near end outward
                float reach = (i + 1f) / planks;
                float drop = Mathf.Clamp01((open - reach * 0.8f) / 0.2f);
                if (drop <= 0f) continue;
                Vector3 p = farEnd + dir * x + Vector3.up * ((1f - drop) * 2.5f);
                SupPlank(p, dir, width, len / planks * 0.98f, k * drop);
            }
            // The floor's chevrons running toward the leader.
            if (open >= 1f && k > 0.01f)
            {
                float speed = s != null ? s.supBridgeSpeed : 35f, step = 3f, shift = Mathf.Repeat(Clock * speed, step);
                for (float x = len - shift; x > 0f; x -= step)
                {
                    Vector3 p = farEnd + dir * (len - x) + Vector3.up * 0.06f;
                    Add(new Shape(root, "Support chevron", meshQuad, matFrame)
                    {
                        life = Time.deltaTime * 1.5f + 0.001f, at = p, rotation = Quaternion.LookRotation(Vector3.down, dir) * Quaternion.Euler(0f, 0f, 45f),
                        scale = new Vector3(0.7f, 0.7f, 1f), color = HdrA(Teal, 2.4f), bright = k,
                    }).tick = UltTick.Real;
                }
            }
            // The chains from the leader's hands to the bridge's near end while it holds them.
            if (f.by != null && !f.cut)
            {
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                Vector3 hand = ChestOf(f.by);
                SupLine(hand + right * 0.35f, near + right * (width * 0.45f), new Color(0.7f, 0.72f, 0.75f) * 1.4f, 0.03f, k * open, cam);
                SupLine(hand - right * 0.35f, near - right * (width * 0.45f), new Color(0.7f, 0.72f, 0.75f) * 1.4f, 0.03f, k * open, cam);
            }
        }

        /// <summary>One stone plank of the drawbridge (lit stone, its top on the bridge's line).</summary>
        void SupPlank(Vector3 at, Vector3 dir, float width, float length, float k)
        {
            if (k <= 0.01f || matStone == null || meshCube == null) return;
            Matrix4x4 m = Matrix4x4.TRS(at - Vector3.up * 0.14f, Quaternion.LookRotation(dir, Vector3.up), new Vector3(width, 0.25f, length) * Mathf.Lerp(0.6f, 1f, k));
            var rp = new RenderParams(matStone) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On, receiveShadows = true, layer = 0 };
            Graphics.RenderMesh(rp, meshCube, 0, m);
        }

        void SupRope(SupFx f, float k, Camera cam)
        {
            var s = Params;
            Vector3 top = f.at, end = f.far;
            float shoot = Mathf.Clamp01((RealClock - f.callReal) / Mathf.Max(0.05f, s != null ? s.supRopeShoot : 0.35f));
            if (shoot < 1f && f.by != null)
            {
                // Shot up from the leader's hand to the top.
                Vector3 hand = ChestOf(f.by) + Vector3.up * 0.4f;
                Vector3 tip = Vector3.Lerp(hand, top, shoot);
                SupLine(hand, tip, HdrA(TealPale, 3f), 0.04f, k, cam);
                return;
            }
            SupLine(top, end, HdrA(TealPale, 3f), 0.04f, k, cam);
            SupLine(top, end, HdrA(Teal, 1.1f), 0.15f, k * 0.5f, cam);
            // The crown pulley: on the top until it is taken, then over the one sliding.
            Vector3 pulley = f.going && f.mate != null ? f.mate.Hips.position + Vector3.up * 1.1f : top;
            Add(new Shape(root, "Support pulley", meshQuad, matRing)
            {
                life = Time.deltaTime * 1.5f + 0.001f, at = pulley, billboard = true, scale = new Vector3(0.9f, 0.9f, 1f),
                color = HdrA(Teal, 3f), bright = k,
            }).tick = UltTick.Real;
        }

        void SupDrone(SupFx f, float k, Camera cam)
        {
            var mate = f.mate;
            if (mate == null) return;
            var s = Params;
            float hang = s != null ? s.supDroneHang : 2.2f;
            if (!f.going)
            {
                Vector3 over = mate.bodies[(int)BodyId.Head].position + Vector3.up * 3f;
                f.drone = Vector3.MoveTowards(f.drone, over, (s != null ? s.supDroneGo : 60f) * realDt);
            }
            else f.drone = Vector3.Lerp(f.drone, mate.Hips.position + Vector3.up * hang, 1f - Mathf.Exp(-realDt * 30f));
            float spin = RealClock * 120f;
            SupDrawPiece(f, f.drone - Vector3.up * 0.4f, Quaternion.Euler(0f, spin, 0f) * Vector3.forward, 1.2f, false, k, cam);
            if (f.going)
            {
                Vector3 l = mate.bodies[(int)BodyId.HandL].position, r = mate.bodies[(int)BodyId.HandR].position;
                SupLine(f.drone - Vector3.up * 0.4f, l, HdrA(TealPale, 2.2f), 0.02f, k, cam);
                SupLine(f.drone - Vector3.up * 0.4f, r, HdrA(TealPale, 2.2f), 0.02f, k, cam);
            }
        }

        void SupChain(SupFx f, float k, Camera cam)
        {
            var s = Params;
            float rise = s != null ? s.supChainRise : 0.6f, age = RealClock - f.callReal;
            for (int i = 0; i < f.chain.Count; i++)
            {
                float up = Mathf.Clamp01((age - i * 0.03f) / Mathf.Max(0.05f, rise));
                if (up <= 0f) continue;
                float sink = f.endReal >= 0f ? Mathf.Clamp01((RealClock - f.endReal) / 0.6f) : 0f;
                Quaternion tilt = default;
                if (f.down[i] >= 0f)
                {
                    float t = Mathf.Clamp01((RealClock - f.down[i]) / 0.3f);
                    tilt = Quaternion.Euler(85f * t, 0f, 0f);
                }
                Vector3 feet = f.chain[i] - Vector3.up * ((1f - Smooth01(up)) * 1.6f + sink * 1.6f);
                SupDrawPiece(f, feet, f.dir, 1.6f, true, 1f, cam, tilt);
            }
        }

        // ---------------------------------------------------------------- the words (OnGUI, 720-px reference)

        /// <summary>OnGUI (UltScreen.cs), before the ultimates' part and whether or not one is on: the support's words in the
        /// 720-px reference.</summary>
        void SupportGuiFrame()
        {
            if (sups.Count == 0 && RealClock - supSavedReal >= 1.8f) return;
            float ui = Screen.height / 720f, vw = Screen.width / ui;
            var keepMatrix = GUI.matrix;
            var keepColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(ui, ui, 1f));
            SupportGui(vw);
            GUI.matrix = keepMatrix;
            GUI.color = keepColor;
        }

        /// <summary>OnGUI (UltScreen.cs, before the ultimates' part): the call on the one called, what the keys do for the one
        /// carried and the leader, "구조 !!" on every screen.</summary>
        void SupportGui(float vw)
        {
            UltStyles();
            var me = Viewer;
            var st = new GUIStyle(ultSub) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            foreach (var f in sups)
            {
                if (f.endReal >= 0f || me == null) continue;
                if (me == f.mate && !f.going)
                {
                    float left = Mathf.Max(0f, f.answer - (RealClock - f.callReal));
                    float pulse = 0.85f + 0.15f * Mathf.Sin(RealClock * 8f);
                    var big = new GUIStyle(ultBig) { fontSize = 46 };
                    GUI.color = new Color(1f, 1f, 1f, pulse);
                    Outlined(new Rect(vw * 0.5f - 400f, 150f, 800f, 60f), "선두가 부른다!", big, HdrA(Teal, 1.1f), 3f);
                    GUI.color = Color.white;
                    Outlined(new Rect(vw * 0.5f - 400f, 208f, 800f, 34f), $"{SupCallLine(f)}  ·  {left:0.0}초", st, new Color(0.9f, 1f, 0.97f), 1.5f);
                }
                else if (me == f.mate && f.going)
                    Outlined(new Rect(vw * 0.5f - 400f, 150f, 800f, 34f), SupRideLine(f), st, new Color(0.9f, 1f, 0.97f), 1.5f);
                else if (me == f.by)
                    Outlined(new Rect(vw * 0.5f - 400f, 150f, 800f, 34f), SupLeadLine(f), st, new Color(0.9f, 1f, 0.97f), 1.5f);
            }
            float a = RealClock - supSavedReal;
            if (a >= 0f && a < 1.8f)
            {
                float kk = Mathf.Clamp01(a / 0.06f) * (1f - Smooth01((a - 1.4f) / 0.4f)), pop = 1f + 0.3f * (1f - Smooth01(a / 0.15f));
                var keep = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(vw * 0.5f, 200f) * (Screen.height / 720f));
                var big = new GUIStyle(ultBig) { fontSize = 76 };
                GUI.color = new Color(1f, 1f, 1f, kk);
                Outlined(new Rect(vw * 0.5f - 400f, 160f, 800f, 80f), "구조 !!", big, HdrA(Teal, 1.2f), 4f);
                GUI.matrix = keep;
                Outlined(new Rect(vw * 0.5f - 400f, 240f, 800f, 30f), supSavedLine, st, Color.white, 1.5f);
                GUI.color = Color.white;
            }
        }

        static string SupCallLine(SupFx f) => f.kind switch
        {
            PieceKind.Knight => "빈 말이 달려와요 — 오면 F로 올라타요",
            PieceKind.Rook => "도개교가 내려와요 — F로 올라서요",
            PieceKind.Queen => "퀸의 줄 — F로 왕관 도르래를 잡아요",
            PieceKind.Bishop => "주교관 드론 — 오면 F로 매달려요",
            _ => "폰 사슬 — F로 첫 폰에 올라서요",
        };

        static string SupRideLine(SupFx f) => f.kind switch
        {
            PieceKind.Knight => "F를 누르고 있어요 (놓으면 떨어져요)  ·  이동 키 = 몰기  ·  점프 = 뛰어넘기",
            PieceKind.Bishop => "F를 누르고 있어요 (놓으면 떨어져요)  ·  선두가 몰아요",
            PieceKind.Rook => "도개교에 실려 가는 중",
            PieceKind.Queen => "줄을 타고 미끄러지는 중",
            _ => "폰 사슬이 던져 주는 중",
        };

        string SupLeadLine(SupFx f)
        {
            switch (f.kind)
            {
                case PieceKind.Knight: return RealClock - f.callReal < (Params != null ? Params.supWhistle : 1f) ? "휘파람!" : f.going ? "말이 데려와요 — 다시 달려요" : "말이 가는 중";
                case PieceKind.Bishop:
                    if (!f.going) return "드론이 가는 중 — 꼴찌가 매달리면 몰아요";
                    float left = Mathf.Max(0f, (Params != null ? Params.supDroneTime : 6f) - (RealClock - f.goReal));
                    return $"이동 키 · 카메라 = 드론 몰기  ·  G = 내려놓기  ·  {left:0.0}초";
                default: return "G를 누르고 있어요 — 놓으면 끊겨요";
            }
        }

        void DestroySupport()
        {
            foreach (var f in sups) if (f.baked != null) Destroy(f.baked);
            sups.Clear();
            supTethers.Clear();
        }
    }
}
