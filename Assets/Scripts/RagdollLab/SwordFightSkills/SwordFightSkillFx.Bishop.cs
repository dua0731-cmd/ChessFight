using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight bishop's mark.
    /// R130 (승규 님 10-13: "비숍 미리 보기 검은 색 부분을 그래픽 좋은 광택 나는 검은 색으로 바꿔줘"): its X and its ring are
    /// solid polished obsidian (Skill Gloss, a black chrome: a dark mirror with a cool sky in it, a crisp highlight, an edge
    /// light in the energy's colour that strengthens as the windup fills), lying a little proud of the floor; the X turns
    /// gold when there is an enemy on the point to take.
    /// R132 (승규 님 10-13: "실제 … 타오르고 있고 파티클이랑 이런 vfx가 적용이 되야 해 그리고 일정한 간격 지금 크고 작고 이렇게
    /// 있어 그리고 상대에게 f 를 맞추고 적용이 되면 그 파티클 vfx가 함께 빨려 들어가는 연출 해줘 연기도 다시 연출해줘", then
    /// with a picture of a glowing violet orb trailing wavy strands of light: "비숍 내가 원한건 이런 디자인이야"): the fire is
    /// gone; round the ring float balls of energy (<see cref="EnergyRing"/>, <c>ChessFight/Skill Energy</c>) — eight, all
    /// the same size, at even steps — each a glowing ball full of twinkling sparkles round a white-hot core, trailing wavy
    /// strands of light behind it as they circle the ring (the strands are the smoke now), sparks twinkling up round them, a
    /// violet light on the floor (the other side's red). They circle slowly while it aims, faster, bigger and brighter as
    /// the windup fills. When the hands take someone every ball flies into it on a curve, its strands streaming after it,
    /// as the hands get there, and they gather into one big ball at its ankles that bursts into sparks and shrinks into
    /// them, while three small balls circle its ankles trailing strands for as long as it is pinned; the ring and the X
    /// collapse into it too. When they take no one the balls gather in the ring's middle and burst.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        static readonly Kit.Palette Obsidian = new Kit.Palette("#121218", "#7D86A8", "#040406", "#24242F", 1f);

        /// <summary>How many balls of energy circle the bishop's ring (all the same size, at even steps, 승규 님: "일정한 간격").</summary>
        const int EnergyBalls = 8;

        Material matEnergy;
        bool energyLooked;

        /// <summary>The energy's material (null if its shader is missing: no energy then).</summary>
        Material EnergyMaterial()
        {
            if (matEnergy != null || energyLooked) return matEnergy;
            energyLooked = true;
            var shader = Shader.Find("ChessFight/Skill Energy");
            if (shader == null) shader = Resources.Load<Shader>("SwordFightSkillFx/SkillEnergy");
            if (shader == null || !shader.isSupported) return null;
            matEnergy = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return matEnergy;
        }

        void DestroyBishopParts()
        {
            foreach (var c in bishopEnergy.Values) c?.Destroy();
            bishopEnergy.Clear();
            if (matEnergy != null) Destroy(matEnergy);
        }

        /// <summary>The energy's colour for whoever watches: the other side's red, else violet.</summary>
        Color EnergyColour(SwordFightSkills s) =>
            kit.SideOf(s.Pawn) == Kit.Side.Enemy ? new Color(kit.enemy.main.r * 1.5f, kit.enemy.main.g * 1.2f, kit.enemy.main.b * 1.2f) : new Color(0.78f, 0.32f, 1.3f);

        static Color EnergyHot(Color energy) => new Color(energy.r * 1.4f + 0.3f, energy.g * 1.4f + 0.3f, energy.b * 1.4f + 0.3f);

        // ---------------------------------------------------------------- the obsidian X and ring (R130)

        readonly Dictionary<SwordFightSkills, (Vector3 c, Vector3 dir, float r, float k, float alpha, bool aim, bool taking)> bishopAt =
            new Dictionary<SwordFightSkills, (Vector3, Vector3, float, float, float, bool, bool)>();

        /// <summary>Where a bishop's energy was last drawn into someone it took (R132): its ring and X collapse there.</summary>
        readonly Dictionary<SwordFightSkills, (Vector3 at, float when)> energyInto = new Dictionary<SwordFightSkills, (Vector3, float)>();

        /// <summary>The obsidian X and ring this frame (false if the board look's glossy material is missing: the old
        /// warning squares are drawn instead).</summary>
        bool BishopObsidian(SwordFightSkills s, Vector3 c, Vector3 dir, float radius, float k, float alpha, bool aim, bool taking)
        {
            if (!board) return false;
            bishopAt[s] = (c, dir, radius, k, alpha, aim, taking);
            var owner = s;
            var fx = kit.Keep(s, "bishop obsidian", () =>
            {
                var f = new Kit.Fx();
                f.props.Add(new Kit.Prop(kit, kit.meshTorus, Obsidian, "Bishop ring (obsidian)") { inkWidth = 0.012f });
                var bar = kit.RoundBox(new Vector3(0.17f, 0.05f, 2.1f), 0.025f);
                f.props.Add(new Kit.Prop(kit, bar, Obsidian, "Bishop X (obsidian)") { inkWidth = 0.01f });
                f.props.Add(new Kit.Prop(kit, bar, Obsidian, "Bishop X (obsidian)") { inkWidth = 0.01f });
                f.end = () => bishopAt.Remove(owner);
                return f;
            });
            if (fx.step != null) return true;
            float released = -1f;
            fx.step = (x, d) =>
            {
                if (owner == null || !bishopAt.TryGetValue(owner, out var v)) return false;
                if (!kit.Held(x) && released < 0f) released = x.age;
                float a = released >= 0f ? (x.age - released) / F : -1f;
                // R132: when the energy is drawn into someone it took, the ring and the X go with it — shrinking into its
                // ankles over the hands' flight, white as they go.
                bool into = a >= 0f && !v.aim && energyInto.TryGetValue(owner, out var to) && kit.Clock - to.when < 0.5f;
                if (a >= (into ? HandFly + 1f : 8f)) return false;
                // Released: white for two frames (not an aim's end: that only fades), sinking and fading in six.
                float flash = a >= 0f && a < 2f && !v.aim ? 1f : 0f;
                float gone = a < 0f ? 0f : v.aim ? Mathf.Clamp01(a / 5f) : Mathf.Clamp01((a - 2f) / 6f);
                float pop = Kit.Pop(x.age, 1.12f);
                float sink = -0.04f * gone;
                Vector3 c0 = v.c;
                if (into)
                {
                    float t = Kit.EaseIn(Mathf.Clamp01(a / HandFly));
                    Vector3 target = energyInto[owner].at;
                    c0 = Vector3.Lerp(v.c, new Vector3(target.x, v.c.y, target.z), t);
                    pop *= Mathf.Max(0.02f, 1f - t);
                    gone = 0f;
                    sink = 0f;
                    flash = a < 3f ? 1f : 0.35f;
                }
                Color glow = EnergyColour(owner);
                float glowK = (v.aim ? 0.25f : 0.35f + 0.65f * v.k) * (1f - gone);
                var ring = x.props[0];
                ring.t.SetPositionAndRotation(c0 + Vector3.up * (0.03f + sink), Quaternion.LookRotation(Kit.FlatDir(v.dir, Vector3.forward), Vector3.up));
                ring.t.localScale = new Vector3(v.r, v.r * 0.32f, v.r) * pop;
                for (int i = 0; i < 3; i++)
                {
                    var p = x.props[i];
                    if (i > 0)
                    {
                        Vector3 arm = Quaternion.AngleAxis(i == 1 ? 45f : -45f, Vector3.up) * Kit.FlatDir(v.dir, Vector3.forward);
                        p.t.SetPositionAndRotation(c0 + Vector3.up * (0.035f + 0.004f * i + sink), Quaternion.LookRotation(arm, Vector3.up));
                        p.t.localScale = Vector3.one * pop;
                        // Gold when an enemy stands on the point (the hands will take it).
                        p.colors = v.taking ? kit.queen : Obsidian;
                    }
                    p.flash = flash;
                    p.fade = v.alpha * (1f - gone);
                    p.rim = new Color(glow.r * glowK, glow.g * glowK, glow.b * glowK);
                    p.rimPower = 2.2f;
                    p.Show(p.fade > 0.02f && pop > 0.03f);
                    p.Apply();
                }
                return true;
            };
            return true;
        }

        // ---------------------------------------------------------------- the energy (R132)

        readonly Dictionary<SwordFightSkills, (Vector3 c, float r, float k, float alpha, bool aim)> bishopRing =
            new Dictionary<SwordFightSkills, (Vector3, float, float, float, bool)>();
        readonly Dictionary<SwordFightSkills, EnergyRing> bishopEnergy = new Dictionary<SwordFightSkills, EnergyRing>();

        /// <summary>
        /// The balls of energy round the ring this frame; <paramref name="k"/> the windup's fill. Aiming: small, circling
        /// slowly, a few sparks. Winding up: bigger, brighter, circling faster, their strands longer, more sparks. When the
        /// windup ends they fly into whoever the hands took (<see cref="EnergyTaken"/>) or, if they took no one, gather in
        /// the middle and burst; an aim let go of just fades.
        /// </summary>
        void BishopEnergy(SwordFightSkills s, Vector3 c, float radius, float k, float alpha, bool aim)
        {
            var mat = EnergyMaterial();
            if (mat == null) return;
            bishopRing[s] = (c, radius, k, alpha, aim);
            var owner = s;
            var fx = kit.Keep(s, "bishop energy", () => new Kit.Fx());
            if (fx.step != null) return;
            Color energy = EnergyColour(s);
            var ring = new EnergyRing(kit.root, mat, c, radius, EnergyBalls, energy, EnergyHot(energy), "Bishop energy");
            if (bishopEnergy.TryGetValue(s, out var old) && old != null) old.Quench(0.15f);
            bishopEnergy[s] = ring;
            fx.end = () =>
            {
                ring.Destroy();
                if (bishopEnergy.TryGetValue(owner, out var now) && now == ring) bishopEnergy.Remove(owner);
                bishopRing.Remove(owner);
            };
            bool released = false;
            fx.step = (x, d) =>
            {
                if (owner == null) return false;
                if (!released && bishopRing.TryGetValue(owner, out var v))
                {
                    ring.center = v.c;
                    ring.radius = v.r;
                    float fill = v.aim ? 0f : Mathf.Clamp01(v.k);
                    ring.ballSize = v.aim ? 0.3f : Mathf.Lerp(0.34f, 0.48f, fill);
                    ring.spin = v.aim ? 0.9f : Mathf.Lerp(1.5f, 3.4f, fill);
                    ring.height = v.aim ? 0.3f : Mathf.Lerp(0.33f, 0.45f, fill);
                    ring.sparks = v.aim ? 8f : Mathf.Lerp(16f, 46f, fill);
                    ring.glow = v.aim ? 0.26f : Mathf.Lerp(0.32f, 0.62f, fill);
                    ring.opacity = Mathf.Clamp01(v.alpha * 1.35f) * Mathf.Clamp01(x.age / 0.12f);
                    if (!kit.Held(x))
                    {
                        released = true;
                        // Not drawn into anyone: an aim fades, a cast gathers in the middle and bursts.
                        if (ring.state == EnergyRing.State.Ring)
                        {
                            if (v.aim) ring.Quench(0.2f);
                            else ring.Gather();
                        }
                    }
                }
                else if (!released) released = true;
                bool alive = ring.Step(d, ViewCamera);
                return alive || !released;
            };
        }

        /// <summary>
        /// The hands took <paramref name="target"/> (R132, "상대에게 f 를 맞추고 적용이 되면 그 파티클 vfx가 함께 빨려 들어가는
        /// 연출"): the bishop's balls of energy fly into its ankles on curves, their strands streaming after them, arriving
        /// as the hands do; there they gather into one big ball that bursts into sparks and shrinks into its ankles, with a ring
        /// of the energy's light on the floor, and three small balls circle its ankles trailing strands while it is pinned.
        /// </summary>
        void EnergyTaken(SwordFightSkills by, RagdollPawn target, float pinTime)
        {
            if (by == null || target == null || by.Pawn == null) return;
            Vector3 last = Kit.FloorUnder(target.Hips.position, 0.6f) + Vector3.up * 0.2f;
            Vector3 Ankles()
            {
                if (target != null) last = Kit.FloorUnder(target.Hips.position, 0.6f) + Vector3.up * 0.2f;
                return last;
            }
            energyInto[by] = (last, kit.Clock);
            if (!bishopEnergy.TryGetValue(by, out var ring) || ring == null) return;
            Color energy = EnergyColour(by);
            var pal = new Kit.Palette { main = new Color(energy.r * 0.7f, energy.g * 0.7f, energy.b * 0.7f), light = EnergyHot(energy), deep = new Color(0.05f, 0.02f, 0.08f), ink = Obsidian.ink };
            ring.Into(Ankles, pinTime, at =>
            {
                Vector3 floor = Kit.FloorUnder(at, 1f);
                kit.ImpactRing(floor + Vector3.up * 0.1f, Vector3.up, pal, 0.25f, 0.95f);
                kit.FlashRing(floor, 0.7f, pal);
            });
        }

        // ---------------------------------------------------------------- balls of energy, strands, sparks

        /// <summary>
        /// The bishop's energy (R132): balls circling a ring, each trailing strands of light, sparks and glows round them, all
        /// drawn as one mesh by <c>ChessFight/Skill Energy</c> (every part light, added: no sorting). A ball keeps its last
        /// <see cref="TrailLength"/> places; its strands are laid along that path, each waving its own way and fanning out
        /// toward the tail, thinning and fading, so they stream behind it whichever way it goes (round the ring, or on its
        /// curve into whoever it was drawn into).
        /// </summary>
        sealed class EnergyRing
        {
            public enum State { Ring, Into, Gather, Out }

            const int TrailLength = 34, Strands = 6, Most = 3000;

            sealed class Ball
            {
                public Vector3 pos;
                public float angle, seed, alpha = 1f, scale = 1f;
                public readonly Vector3[] trail = new Vector3[TrailLength];
                public int head = -1, count;
                public readonly float[] phase = new float[Strands], amp = new float[Strands];
                // Flying in (drawn into someone, or gathering).
                public bool flying, landed;
                public Vector3 from, bend;
                public float flyClock, flyTime;
                public int target = -1;
                // Circling a pinned piece's ankles.
                public bool binder;
                public float radius, spin, height, until;

                public void Push(Vector3 p)
                {
                    head = (head + 1) % TrailLength;
                    trail[head] = p;
                    count = Mathf.Min(TrailLength, count + 1);
                }

                /// <summary>Where it was <paramref name="k"/> frames ago.</summary>
                public Vector3 Back(int k) => trail[(head - k + TrailLength * 4) % TrailLength];
            }

            struct Mote
            {
                public Vector3 pos, vel;
                public float age, life, w, seed;
                public byte kind;   // 1 glow, 2 spark, 5 a ball bursting
                public int follow;  // the target a burst sits on (-1: none)
            }

            sealed class Target
            {
                public Func<Vector3> at;
                public float pin;
                public bool landed;
            }

            readonly List<Ball> balls = new List<Ball>();
            readonly List<Mote> motes = new List<Mote>();
            readonly List<Target> targets = new List<Target>();
            readonly GameObject go;
            readonly Mesh mesh;
            readonly Light light;
            readonly List<Vector3> vs = new List<Vector3>();
            readonly List<Color> cs = new List<Color>();
            readonly List<Vector4> u0 = new List<Vector4>(), u1 = new List<Vector4>(), u2 = new List<Vector4>(), u3 = new List<Vector4>();
            readonly List<int> tri = new List<int>();
            Action<Vector3> landed;
            float clock, sparkAcc, glowAcc, quench = -1f, quenchLeft, lightNow, stateClock;
            bool assigned;

            public Vector3 center;
            public float radius;
            /// <summary>A ball's size (m across), how fast they circle (rad/s), how high they float (m).</summary>
            public float ballSize = 0.3f, spin = 1.5f, height = 0.33f;
            /// <summary>Sparks a second, the glows' strength, the whole thing's opacity.</summary>
            public float sparks = 12f, glow = 0.3f, opacity = 1f;
            public State state = State.Ring;

            public EnergyRing(Transform parent, Material mat, Vector3 center, float radius, int count, Color energy, Color hot, string name)
            {
                this.center = center;
                this.radius = radius;
                for (int i = 0; i < count; i++)
                {
                    var b = new Ball { angle = i * Mathf.PI * 2f / count, seed = Random.Range(0f, 50f) };
                    for (int s = 0; s < Strands; s++)
                    {
                        b.phase[s] = Random.Range(0f, Mathf.PI * 2f);
                        b.amp[s] = 0.05f + 0.026f * s + Random.Range(-0.012f, 0.012f);
                    }
                    balls.Add(b);
                }
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var block = new MaterialPropertyBlock();
                block.SetColor("_Edge", energy);
                block.SetColor("_Lip", hot);
                block.SetColor("_Deep", new Color(energy.r * 0.3f, energy.g * 0.22f, energy.b * 0.32f));
                mr.SetPropertyBlock(block);
                float most = Mathf.Max(energy.r, Mathf.Max(energy.g, energy.b));
                var lg = new GameObject(name + " (light)");
                lg.transform.SetParent(go.transform, false);
                light = lg.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = most > 1e-3f ? new Color(energy.r / most, energy.g / most, energy.b / most) : Color.white;
                light.shadows = LightShadows.None;
                PipelineSettings.LightPiecesOnly(light);   // R157: the orbs colour the pieces, not the floor
                light.intensity = 0f;
                light.range = radius + 1.8f;
            }

            public void Destroy()
            {
                if (go != null) Object.Destroy(go);
                if (mesh != null) Object.Destroy(mesh);
            }

            /// <summary>Everything fades away within <paramref name="seconds"/>.</summary>
            public void Quench(float seconds)
            {
                state = State.Out;
                quench = quenchLeft = Mathf.Max(0.01f, seconds);
            }

            /// <summary>Nobody was taken: the balls gather in the ring's middle and burst there.</summary>
            public void Gather()
            {
                if (state != State.Ring) return;
                state = State.Gather;
                stateClock = 0f;
                Vector3 mid = center + Vector3.up * (height + 0.05f);
                targets.Add(new Target { at = () => mid, pin = 0f });
                assigned = false;
            }

            /// <summary>Every ball flies into the nearest place given (a taken piece's ankles); <paramref name="onLand"/>
            /// when the first gets to each.</summary>
            public void Into(Func<Vector3> at, float pin, Action<Vector3> onLand)
            {
                if (state == State.Out) return;
                if (state != State.Into)
                {
                    targets.Clear();
                    state = State.Into;
                    stateClock = 0f;
                }
                targets.Add(new Target { at = at, pin = pin });
                landed = onLand;
                assigned = false;
            }

            /// <summary>Sends every ball still on the ring off to its nearest target on a curve (on round its way, rising),
            /// one after another, all there in about the hands' flight.</summary>
            void Assign()
            {
                assigned = true;
                int n = 0;
                foreach (var b in balls)
                {
                    if (b.binder || b.landed) continue;
                    int best = 0;
                    float bd = float.MaxValue;
                    for (int t = 0; t < targets.Count; t++)
                    {
                        float dd = (targets[t].at() - b.pos).sqrMagnitude;
                        if (dd < bd) { bd = dd; best = t; }
                    }
                    Vector3 radial = new Vector3(Mathf.Cos(b.angle), 0f, Mathf.Sin(b.angle));
                    Vector3 along = new Vector3(-radial.z, 0f, radial.x) * Mathf.Sign(spin);
                    b.flying = true;
                    b.target = best;
                    b.from = b.pos;
                    b.bend = b.pos + along * 0.55f + Vector3.up * 0.35f;
                    b.flyClock = 0f;
                    b.flyTime = 0.12f + 0.008f * n++;
                }
            }

            /// <summary>One frame (<paramref name="dt"/> in effect time), drawn for <paramref name="cam"/>; false once it is
            /// done and every part is gone.</summary>
            public bool Step(float dt, Camera cam)
            {
                clock += dt;
                stateClock += dt;
                if ((state == State.Into || state == State.Gather) && !assigned) Assign();
                if (quench > 0f)
                {
                    quenchLeft -= dt;
                    if (quenchLeft <= 0f) { balls.Clear(); motes.Clear(); }
                }

                for (int i = balls.Count - 1; i >= 0; i--)
                {
                    var b = balls[i];
                    if (b.binder)
                    {
                        var t = targets[b.target];
                        b.angle += b.spin * dt;
                        Vector3 c = t.at();
                        b.pos = c + new Vector3(Mathf.Cos(b.angle), 0f, Mathf.Sin(b.angle)) * b.radius
                                  + Vector3.up * (b.height - 0.2f + 0.05f * Mathf.Sin(clock * 6f + b.seed));
                        if (clock > b.until) b.alpha -= dt / 0.25f;
                        if (b.alpha <= 0f) { balls.RemoveAt(i); continue; }
                    }
                    else if (b.landed)
                    {
                        // Gone in; its strands follow it in over a few frames.
                        b.alpha -= dt / 0.18f;
                        if (b.alpha <= 0f) { balls.RemoveAt(i); continue; }
                        b.pos = targets[b.target].at();
                    }
                    else if (b.flying)
                    {
                        b.flyClock += dt;
                        float k = Mathf.Clamp01(b.flyClock / b.flyTime);
                        float e = k * k;
                        Vector3 to = targets[b.target].at();
                        b.pos = (1f - e) * (1f - e) * b.from + 2f * (1f - e) * e * b.bend + e * e * to;
                        b.scale = Mathf.Lerp(1f, 0.6f, e);
                        if (k >= 1f)
                        {
                            b.landed = true;
                            Land(b.target);
                        }
                    }
                    else
                    {
                        b.angle += spin * dt;
                        b.pos = center + new Vector3(Mathf.Cos(b.angle), 0f, Mathf.Sin(b.angle)) * radius
                                + Vector3.up * (height + 0.035f * Mathf.Sin(clock * 3.1f + b.seed));
                    }
                    b.Push(b.pos);
                }

                // Sparks twinkling up round the ring, glows under the balls (while it is up).
                if (state == State.Ring && opacity > 0.01f)
                {
                    sparkAcc += dt * sparks;
                    while (sparkAcc >= 1f)
                    {
                        sparkAcc -= 1f;
                        float a = Random.value * Mathf.PI * 2f;
                        Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        AddMote(2, center + radial * (radius + Random.Range(-0.12f, 0.12f)) + Vector3.up * Random.Range(0.05f, height + 0.2f),
                            Vector3.up * Random.Range(0.25f, 0.7f) + radial * Random.Range(-0.15f, 0.2f), Random.Range(0.45f, 0.9f), Random.Range(0.035f, 0.06f));
                    }
                    glowAcc += dt * balls.Count * 3f;
                    while (glowAcc >= 1f && balls.Count > 0)
                    {
                        glowAcc -= 1f;
                        var b = balls[Random.Range(0, balls.Count)];
                        AddMote(1, new Vector3(b.pos.x, center.y + 0.08f, b.pos.z), Vector3.zero, Random.Range(0.4f, 0.6f), ballSize * 2.2f);
                    }
                }

                for (int i = motes.Count - 1; i >= 0; i--)
                {
                    var m = motes[i];
                    m.age += dt;
                    if (m.age >= m.life)
                    {
                        motes[i] = motes[motes.Count - 1];
                        motes.RemoveAt(motes.Count - 1);
                        continue;
                    }
                    if (m.follow >= 0 && m.follow < targets.Count) m.pos = targets[m.follow].at();
                    else
                    {
                        if (m.kind == 2)
                        {
                            m.vel += new Vector3(Mathf.Sin(clock * 8.3f + m.seed * 3f), 0.1f, Mathf.Cos(clock * 7.1f + m.seed * 5f)) * (1.2f * dt);
                            m.vel *= Mathf.Exp(-1.2f * dt);
                        }
                        m.pos += m.vel * dt;
                    }
                    motes[i] = m;
                }

                Draw(cam, dt);
                bool lingering = balls.Count > 0 || motes.Count > 0;
                return state == State.Ring || lingering;
            }

            void AddMote(byte kind, Vector3 pos, Vector3 vel, float life, float w, int follow = -1)
            {
                if (motes.Count >= Most) return;
                motes.Add(new Mote { kind = kind, pos = pos, vel = vel, life = life, w = w, seed = Random.Range(0f, 50f), follow = follow });
            }

            /// <summary>The first ball got to a target: a big ball bursting there and shrinking in, a spray of sparks, and
            /// (a taken piece) three small balls circling its ankles for as long as it is pinned.</summary>
            void Land(int t)
            {
                var target = targets[t];
                if (target.landed) return;
                target.landed = true;
                Vector3 at = target.at();
                bool gather = state == State.Gather;
                AddMote(5, at, Vector3.zero, gather ? 0.3f : 0.38f, ballSize * (gather ? 2.0f : 2.2f), gather ? -1 : t);
                AddMote(1, at, Vector3.zero, 0.3f, ballSize * 6f, gather ? -1 : t);
                for (int i = 0; i < 22; i++)
                {
                    Vector3 dir = Random.onUnitSphere;
                    dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                    AddMote(2, at + dir * 0.1f, dir * Random.Range(1.2f, 3f), Random.Range(0.35f, 0.7f), Random.Range(0.04f, 0.065f));
                }
                lightNow = Mathf.Max(lightNow, 3f);
                if (gather) return;
                landed?.Invoke(at);
                for (int i = 0; i < 3; i++)
                {
                    var b = new Ball
                    {
                        binder = true,
                        target = t,
                        angle = i * Mathf.PI * 2f / 3f,
                        radius = 0.34f,
                        spin = 5.5f,
                        height = 0.12f + 0.12f * i,
                        until = clock + target.pin,
                        seed = Random.Range(0f, 50f),
                        scale = 0.55f,
                    };
                    for (int s = 0; s < Strands; s++)
                    {
                        b.phase[s] = Random.Range(0f, Mathf.PI * 2f);
                        b.amp[s] = 0.025f + 0.011f * s;
                    }
                    balls.Add(b);
                }
            }

            void Draw(Camera cam, float dt)
            {
                vs.Clear(); cs.Clear(); u0.Clear(); u1.Clear(); u2.Clear(); u3.Clear(); tri.Clear();
                float fade = opacity * (quench > 0f ? Mathf.Clamp01(quenchLeft / quench) : 1f);
                foreach (var b in balls)
                {
                    float size = ballSize * b.scale;
                    float alpha = Mathf.Clamp01(b.alpha) * fade;
                    if (alpha <= 0.004f) continue;
                    // The strands, laid along its path: waving, fanning out and fading toward the tail.
                    if (b.count >= 2)
                    {
                        float width = Mathf.Max(0.01f, size * 0.1f);
                        for (int s = 0; s < Strands; s++)
                        {
                            Vector3 prev = Vector3.zero;
                            for (int k = 0; k < b.count; k++)
                            {
                                Vector3 p = b.Back(k);
                                Vector3 q = b.Back(Mathf.Min(b.count - 1, k + 1));
                                Vector3 dir = p - q;
                                Vector3 side = Vector3.Cross(Vector3.up, dir);
                                side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.right;
                                float u = k / (float)(TrailLength - 1);
                                float spread = Mathf.Pow(u, 0.85f);
                                Vector3 wave = Vector3.up * (Mathf.Sin(b.phase[s] + u * 8f - clock * 7f) * b.amp[s] * spread * 1.4f)
                                             + side * (Mathf.Cos(b.phase[s] * 1.7f + u * 6f - clock * 5f) * b.amp[s] * spread * 1.6f);
                                Vector3 here = p + wave;
                                if (k > 0)
                                {
                                    Vector3 seg = prev - here;
                                    float len = seg.magnitude;
                                    if (len > 1e-4f)
                                    {
                                        float a = Mathf.Pow(1f - u, 1.2f) * alpha;
                                        Quad((prev + here) * 0.5f, 6f, u, width * (1f - 0.55f * u), len + width, 0f, b.seed + s, new Vector4(seg.x, seg.y, seg.z, 1f), new Color(1f, 1f, 1f, a), 0f);
                                    }
                                }
                                prev = here;
                            }
                        }
                    }
                    Quad(b.pos, 5f, 0f, size * 1.7f, size * 1.7f, 0f, b.seed, Vector4.zero, new Color(1f, 1f, 1f, alpha), clock + b.seed);
                }
                foreach (var m in motes)
                {
                    float t = Mathf.Clamp01(m.age / m.life);
                    switch (m.kind)
                    {
                        case 1:
                            Quad(m.pos, 1f, t, m.w, m.w, 0f, m.seed, Vector4.zero, new Color(1f, 1f, 1f, Mathf.Sin(Mathf.PI * t) * glow * fade * (m.w > ballSize * 4f ? 3f : 1f)), 0f);
                            break;
                        case 2:
                        {
                            float speed = m.vel.magnitude;
                            float twinkle = 0.6f + 0.4f * Mathf.Sin(clock * 22f + m.seed * 9f);
                            float a = Mathf.Min(1f, m.age / 0.05f) * Mathf.Pow(1f - t, 1.2f) * twinkle * fade;
                            Vector4 axis = speed > 0.6f ? new Vector4(m.vel.x, m.vel.y, m.vel.z, 1f) : Vector4.zero;
                            Quad(m.pos, 2f, t, m.w, m.w + (speed > 0.6f ? speed * 0.03f : 0f), m.seed, m.seed, axis, new Color(1f, 1f, 1f, a), 0f);
                            break;
                        }
                        default:
                        {
                            // A ball bursting: big and white-hot in two frames, shrinking in.
                            float grow = t < 0.12f ? Kit.EaseOut(t / 0.12f) * 1.15f : Mathf.Lerp(1.15f, 0f, Kit.EaseIn((t - 0.12f) / 0.88f));
                            Quad(m.pos, 5f, t, m.w * grow, m.w * grow, 0f, m.seed, Vector4.zero, new Color(1.4f, 1.4f, 1.4f, fade), clock * 1.5f + m.seed);
                            break;
                        }
                    }
                }
                mesh.Clear();
                mesh.SetVertices(vs);
                mesh.SetColors(cs);
                mesh.SetUVs(0, u0);
                mesh.SetUVs(1, u1);
                mesh.SetUVs(2, u2);
                mesh.SetUVs(3, u3);
                mesh.SetTriangles(tri, 0, false);
                mesh.bounds = new Bounds(center + Vector3.up * 1.2f, new Vector3(radius * 2f + 8f, 6f, radius * 2f + 8f));

                // The light: on the floor round the ring while it is up, after the balls as they go, a flash where they land.
                float want = state == State.Ring ? glow * 2.4f : 0f;
                lightNow = Mathf.MoveTowards(lightNow, want, dt * 10f);
                light.intensity = PipelineSettings.PointIntensity(lightNow * (0.9f + 0.1f * Mathf.Sin(clock * 9f)) * fade, radius + 1.8f);
                Vector3 lightAt = center;
                if (state != State.Ring && targets.Count > 0) lightAt = Vector3.Lerp(center, targets[0].at(), Mathf.Clamp01(stateClock / 0.15f));
                light.transform.position = lightAt + Vector3.up * (height + 0.15f);
                light.range = radius + 1.8f;
            }

            void Quad(Vector3 mid, float kind, float t, float w, float h, float turn, float seed, Vector4 axis, Color tint, float own)
            {
                int v0 = vs.Count;
                for (int k = 0; k < 4; k++)
                {
                    float cx = k == 0 || k == 3 ? -1f : 1f, cy = k < 2 ? -1f : 1f;
                    vs.Add(mid);
                    cs.Add(tint);
                    u0.Add(new Vector4(cx, cy, kind, t));
                    u1.Add(new Vector4(w, h, turn, seed));
                    u2.Add(axis);
                    u3.Add(new Vector4(own, 0f, 0f, 0f));
                }
                tri.Add(v0); tri.Add(v0 + 1); tri.Add(v0 + 2);
                tri.Add(v0); tri.Add(v0 + 2); tri.Add(v0 + 3);
            }
        }
    }
}
