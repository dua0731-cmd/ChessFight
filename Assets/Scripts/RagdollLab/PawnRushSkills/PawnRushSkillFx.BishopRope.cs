using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The bishop's X rope drawn (10-26, RagdollPawn.BishopLasso.cs; 승규 님: "소드파이트에서 비숍이 구원의 손 할 때 밧줄 그걸로
    /// 비숍 손에서 x 자로 상대 배에다가 걸고 … 실사에 맞게"). The Sword Fight rescue rope's look (SwordFightSkillFx.RopeHand.cs):
    /// a real three-strand hemp rope, dark and weathered so it reads on the light squares and the ivory pieces, built along
    /// its curve each frame. One from each of the bishop's hands, crossing on their way (the X), each a real rope that falls and lies on
    /// the floor (<see cref="LassoStrand"/>): in flight their ends trail and wave; round the piece's waist the two ends go
    /// into turns of the same rope wound round it; slack the spare rope sags and lies on the floor, pulled past their length
    /// they run dead straight, a little thinner, trembling; let go they fall slack and are drawn back to the hands; snapped
    /// they break in the middle, the hands' halves drawn in, the far halves falling off and lying, hemp fibres flying. While
    /// it aims, on its own screen: the two lines it would throw, faint, and a thin red ring round the enemy it would catch.
    /// No lights anywhere (10-26, 승규 님: "불빛도 지워줘").
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>The rope's thickness (m): thinner than 6 cm the builder's strand normals fold in (Sword Fight's note).</summary>
        const float LassoThick = 0.06f;
        static readonly Color LassoHemp = new Color(0.42f, 0.3f, 0.17f);
        Material lassoMat;

        Material LassoMaterial()
        {
            if (lassoMat != null) return lassoMat;
            lassoMat = LitMaterials.NewLit(LassoHemp, 0.08f);
            lassoMat.hideFlags = HideFlags.HideAndDontSave;
            lassoMat.name = "Bishop hemp rope";
            return lassoMat;
        }

        void DestroyLasso()
        {
            if (lassoMat != null) Destroy(lassoMat);
        }

        GameObject LassoObject(string name, Mesh mesh)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = LassoMaterial();
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = false;   // a thin tube speckles black with its own shadow
            return go;
        }

        /// <summary>A rope along world points, built round a pivot a metre to its side (the builder turns its strands from the
        /// mesh's own origin, which must not lie along the rope).</summary>
        static void LassoRun(GameObject go, Mesh mesh, List<Vector3> world, float thick)
        {
            int n = world.Count;
            if (n < 2) return;
            Vector3 a = world[0], b = world[n - 1];
            Vector3 flat = b - a;
            flat.y = 0f;
            Vector3 perp = flat.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            Vector3 pivot = (a + b) * 0.5f - perp;
            var local = new List<Vector3>(n);
            foreach (var p in world) local.Add(p - pivot);
            go.transform.SetPositionAndRotation(pivot, Quaternion.identity);
            RopeAlong(mesh, local, thick);
        }

        /// <summary>How far a piece's waist reaches out from its middle (m), for the turns of rope round it.</summary>
        static float WaistRadius(RagdollPawn p)
        {
            var c = p != null ? p.bodies[0].GetComponent<Collider>() : null;
            float r = c != null ? Mathf.Max(c.bounds.extents.x, c.bounds.extents.z) : 0.12f;
            return Mathf.Clamp(r, 0.08f, 0.2f);
        }

        // ---------------------------------------------------------------- the rope

        /// <summary>
        /// One strand of the bishop's rope as a real rope (10-26, 승규 님: "바닥에 통과 되면 안되는데 통과가 돼 밧줄이"; it was a
        /// curve worked out from its slack, which hung through the floor): points 8 cm apart joined by fixed lengths (Verlet),
        /// gravity, a little damping, the floor under every point - slack it lies down on the floor, taut it runs straight.
        /// Either end may be held (the hand, the waist) or free (the halves of a snapped rope).
        /// </summary>
        sealed class LassoStrand
        {
            const float Seg = 0.08f;
            readonly List<Vector3> p = new List<Vector3>(), prev = new List<Vector3>();
            float seg = Seg;
            static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);

            public List<Vector3> Points => p;
            public int Count => p.Count;
            public float Length => seg * Mathf.Max(0, p.Count - 1);

            /// <summary>Taut: straight from <paramref name="a"/> to <paramref name="b"/>, at rest (so it falls from there when it
            /// goes slack again).</summary>
            public void Straight(Vector3 a, Vector3 b)
            {
                int n = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / Seg) + 1, 3, 120);
                p.Clear();
                prev.Clear();
                for (int i = 0; i < n; i++)
                {
                    Vector3 q = Vector3.Lerp(a, b, i / (n - 1f));
                    p.Add(q);
                    prev.Add(q);
                }
                seg = Mathf.Max(0.01f, Vector3.Distance(a, b) / (n - 1));
            }

            /// <summary>Start from these points (a snapped rope's half).</summary>
            public void Set(List<Vector3> from, int start, int end)
            {
                p.Clear();
                prev.Clear();
                for (int i = start; i <= end; i++) { p.Add(from[i]); prev.Add(from[i]); }
                seg = p.Count > 1 ? Mathf.Max(0.01f, Vector3.Distance(p[0], p[p.Count - 1]) / (p.Count - 1)) : Seg;
                for (int i = 1; i < p.Count; i++) seg = Mathf.Max(seg, Vector3.Distance(p[i - 1], p[i]));
            }

            public void Step(Vector3 a, Vector3 b, float length, float dt, bool pinA, bool pinB, float radius)
            {
                int want = Mathf.Clamp(Mathf.CeilToInt(length / Seg) + 1, 3, 120);
                if (p.Count == 0)
                    for (int i = 0; i < want; i++)
                    {
                        Vector3 q = Vector3.Lerp(a, b, i / (want - 1f));
                        p.Add(q);
                        prev.Add(q);
                    }
                // Rope paid out or taken in at the held end (the hand).
                // (Halfway to the next point, not on top of the hand: a link of no length gave the mesh no normal there, the
                // shader a NaN and the bloom a white blob over the rope - 승규 님's "빛이 나는 버그".)
                while (p.Count < want)
                {
                    Vector3 q = Vector3.Lerp(p[0], p[1], 0.5f), qp = Vector3.Lerp(prev[0], prev[1], 0.5f);
                    p.Insert(1, q);
                    prev.Insert(1, qp);
                }
                while (p.Count > want) { p.RemoveAt(1); prev.RemoveAt(1); }
                seg = Mathf.Max(0.01f, length / (p.Count - 1));
                int last = p.Count - 1;
                if (dt > 0f)
                {
                    float h = Mathf.Min(dt, 1f / 30f);
                    for (int i = 0; i <= last; i++)
                    {
                        if ((i == 0 && pinA) || (i == last && pinB)) continue;
                        Vector3 v = (p[i] - prev[i]) * 0.98f;
                        prev[i] = p[i];
                        p[i] = p[i] + v + Gravity * (h * h);
                    }
                }
                if (pinA) p[0] = prev[0] = a;
                if (pinB) p[last] = prev[last] = b;
                for (int k = 0; k < 24; k++)
                {
                    for (int i = 0; i < last; i++)
                    {
                        Vector3 d = p[i + 1] - p[i];
                        float m = d.magnitude;
                        if (m < 1e-5f) continue;
                        Vector3 fix = d * ((m - seg) / m);
                        bool holdA = i == 0 && pinA, holdB = i + 1 == last && pinB;
                        if (holdA && holdB) continue;
                        if (holdA) p[i + 1] -= fix;
                        else if (holdB) p[i] += fix;
                        else { p[i] += fix * 0.5f; p[i + 1] -= fix * 0.5f; }
                    }
                    if (pinA) p[0] = a;
                    if (pinB) p[last] = b;
                }
                // The floor: the rope lies on it and drags over it (friction), never through it.
                for (int i = 0; i <= last; i++)
                {
                    if ((i == 0 && pinA) || (i == last && pinB)) continue;
                    Vector3 q = p[i];
                    if (Physics.Raycast(q + Vector3.up * 0.5f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                        && !RagdollPawn.ColliderOwner.ContainsKey(hit.collider) && q.y < hit.point.y + radius)
                    {
                        q.y = hit.point.y + radius;
                        p[i] = q;
                        Vector3 pv = prev[i];
                        prev[i] = new Vector3(Mathf.Lerp(pv.x, q.x, 0.3f), q.y, Mathf.Lerp(pv.z, q.z, 0.3f));
                    }
                }
            }
        }

        void BishopLassoFx(RagdollPawn b)
        {
            if (b.BishopLassoPhase == LassoPhase.None) return;
            kit.Keep(b, "bishop lasso", () =>
            {
                var fx = new Kit.Fx();
                var meshes = new Mesh[5];
                var objs = new GameObject[5];
                for (int i = 0; i < 5; i++)
                {
                    meshes[i] = new Mesh { name = i == 2 ? "Bishop rope round the waist" : i < 2 ? "Bishop rope" : "Bishop rope, the broken end", hideFlags = HideFlags.HideAndDontSave };
                    meshes[i].MarkDynamic();
                    objs[i] = LassoObject(meshes[i].name, meshes[i]);
                    objs[i].SetActive(false);
                }
                // The two strands; a snapped rope's far halves (3, 4).
                var strands = new[] { new LassoStrand(), new LassoStrand(), null, new LassoStrand(), new LassoStrand() };
                float builtCoil = -1f;
                var draw = new List<Vector3>();
                var clean = new List<Vector3>();
                Vector3 side = Vector3.right;
                float wave = Random.Range(0f, 10f);
                bool snapped = false;
                var snapLength = new float[2];
                fx.end = () =>
                {
                    foreach (var o in objs) if (o != null) Destroy(o);
                    foreach (var m in meshes) Destroy(m);
                };
                fx.step = (x, d) =>
                {
                    if (b == null || !kit.Held(x)) return false;
                    var phase = b.BishopLassoPhase;
                    var on = b.BishopLassoOn;
                    Vector3 handL = b.bodies[(int)BodyId.HandL].position, handR = b.bodies[(int)BodyId.HandR].position;
                    Vector3 hands = (handL + handR) * 0.5f;
                    Vector3 tip = b.BishopLassoTip;
                    var s = Params;
                    float breakAt = s != null ? s.bishopRopeBreak : 3f;
                    float t = b.BishopLassoTime;
                    float radius = LassoThick * 0.5f;
                    wave += d;
                    if (phase == LassoPhase.Fly)
                    {
                        // 10-26 (승규 님: "밧줄이 날아갈 때 위에서부터 아래로 자연스럽게 휘날리면서"): thrown from overhead, its end
                        // rises off the hands and comes down onto the piece (highest a third of the way, 0.9 m up for a
                        // 7 m throw), the rope trailing loose behind it. Drawn only: what it catches is the straight line.
                        var aimed = b.BishopLassoAim;
                        float total = aimed != null ? Vector3.Distance(hands, aimed.LassoBelly) : s != null ? s.bishopRopeRange : 7f;
                        float u = Mathf.Clamp01(Vector3.Distance(hands, tip) / Mathf.Max(0.5f, total));
                        tip += Vector3.up * (2.4f * Mathf.Sqrt(u) * (1f - u) * Mathf.Clamp01(total / 3f));
                    }

                    // Where the two ends are: either side of the tip, across the way from the bishop.
                    Vector3 toward = Flat(tip - hands);
                    if (toward.sqrMagnitude > 1e-4f) side = Vector3.Cross(Vector3.up, toward.normalized);
                    bool holding = phase == LassoPhase.Hold && on != null;
                    float apart = holding ? WaistRadius(on) : 0.05f + 0.05f * Mathf.Clamp01(t / 0.15f);
                    Vector3 endA = tip + side * apart, endB = tip - side * apart;
                    // The X: each hand to the end on the other side.
                    bool lLeft = Vector3.Dot(handL - hands, side) > 0f;
                    Vector3 toL = lLeft ? endB : endA, toR = lLeft ? endA : endB;
                    float stretch = holding ? Vector3.Distance(hands, tip) - b.BishopLassoRest : 0f;
                    float thick = LassoThick * (1f - 0.15f * Mathf.Clamp01(stretch / Mathf.Max(0.1f, breakAt)));

                    if (phase == LassoPhase.Snap)
                    {
                        // Broken in the middle: the hands' halves are drawn back in, the far halves fall off the piece and lie.
                        if (!snapped)
                        {
                            snapped = true;
                            for (int i = 0; i < 2; i++)
                            {
                                var whole = strands[i].Points;
                                if (whole.Count < 4) continue;
                                int mid = whole.Count / 2;
                                strands[3 + i].Set(whole, mid, whole.Count - 1);
                                var near = new List<Vector3>(whole);
                                strands[i].Set(near, 0, mid);
                                snapLength[i] = strands[i].Length;
                            }
                        }
                        float u = Mathf.Clamp01(t / 0.28f);
                        for (int i = 0; i < 2; i++)
                        {
                            var hand = i == 0 ? handL : handR;
                            var st = strands[i];
                            st.Step(hand, hand, Mathf.Max(0.05f, snapLength[i] * (1f - u)), d, true, false, radius);
                            Build(objs[i], meshes[i], st.Points, 0f, thick);
                            var far = strands[3 + i];
                            if (far.Count >= 2)
                            {
                                far.Step(far.Points[0], far.Points[far.Count - 1], far.Length, d, false, false, radius);
                                Build(objs[3 + i], meshes[3 + i], far.Points, 0f, thick);
                            }
                        }
                        objs[2].SetActive(false);
                        return true;
                    }

                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 hand = i == 0 ? handL : handR, end = i == 0 ? toL : toR;
                        float span = Vector3.Distance(hand, end);
                        // How much rope this strand has: flying a little more than the way (it trails), held its share of the
                        // rope's length (slack: more than the way, and it lies down; taut: just the way, straight), let go the
                        // way back with a little slack.
                        float length = phase == LassoPhase.Fly ? span * 1.22f + 0.15f
                                     : holding ? span - Mathf.Min(0f, stretch)
                                     : span + 0.2f;
                        if (holding && stretch >= -0.02f) strands[i].Straight(hand, end);
                        else strands[i].Step(hand, end, Mathf.Max(0.1f, length), d, true, true, radius);
                        float tremble = holding && stretch > 0f ? 0.004f + 0.006f * Mathf.Clamp01(stretch / Mathf.Max(0.1f, breakAt)) : 0f;
                        float flying = phase == LassoPhase.Fly ? 1f : 0f;
                        Build(objs[i], meshes[i], strands[i].Points, tremble, thick, i, flying);
                    }
                    for (int i = 3; i < 5; i++) if (objs[i].activeSelf) objs[i].SetActive(false);
                    // The turns round the waist.
                    if (holding)
                    {
                        float r = WaistRadius(on) + thick * 0.5f;
                        if (Mathf.Abs(r - builtCoil) > 0.003f)
                        {
                            RopeCoil(meshes[2], r, 0.1f, 2f, thick);
                            builtCoil = r;
                        }
                        Vector3 fwd = Flat(hands - on.LassoBelly);
                        objs[2].transform.SetPositionAndRotation(on.LassoBelly, Quaternion.LookRotation(fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward, Vector3.up));
                        if (!objs[2].activeSelf) objs[2].SetActive(true);
                    }
                    else if (objs[2].activeSelf) objs[2].SetActive(false);
                    return true;

                    void Build(GameObject go, Mesh mesh, List<Vector3> pts, float shake, float th, int k = 0, float flying = 0f)
                    {
                        if (pts.Count < 2) { go.SetActive(false); return; }
                        draw.Clear();
                        // No two points on top of each other and nothing broken (a link of no length: no normal, a NaN, a
                        // white blob in the bloom).
                        clean.Clear();
                        foreach (var q0 in pts)
                        {
                            if (float.IsNaN(q0.x) || float.IsNaN(q0.y) || float.IsNaN(q0.z) || float.IsInfinity(q0.x) || float.IsInfinity(q0.y) || float.IsInfinity(q0.z)) continue;
                            if (clean.Count == 0 || (q0 - clean[clean.Count - 1]).sqrMagnitude > 0.004f * 0.004f) clean.Add(q0);
                        }
                        float total = 0f;
                        for (int i = 1; i < clean.Count; i++) total += Vector3.Distance(clean[i - 1], clean[i]);
                        if (clean.Count < 2 || total < 0.03f) { go.SetActive(false); return; }
                        pts = clean;
                        Vector3 a = pts[0], z = pts[pts.Count - 1];
                        Vector3 across = Vector3.Cross(z - a, Vector3.up);
                        across = across.sqrMagnitude > 1e-6f ? across.normalized : Vector3.right;
                        int n = pts.Count;
                        for (int i = 0; i < n; i++)
                        {
                            float u = n > 1 ? i / (n - 1f) : 0f;
                            Vector3 q = pts[i];
                            // Flying it waves along itself (more toward its end); held taut it hums.
                            q += across * (flying * 0.04f * u * Mathf.Sin(u * 9f - wave * 22f + k * 1.7f));
                            q += Vector3.up * (flying * 0.06f * u * (1f - u) * 4f * Mathf.Sin(u * 7f - wave * 18f + k * 2.3f));
                            q += across * (shake * Mathf.Sin(wave * 90f + k * 2.1f) * Mathf.Sin(u * Mathf.PI));
                            draw.Add(q);
                        }
                        if (!go.activeSelf) go.SetActive(true);
                        LassoRun(go, mesh, draw, th);
                    }
                };
                return fx;
            });
        }

        // ---------------------------------------------------------------- its aim

        /// <summary>While the bishop aims: the rope held ready in its hands (a coil in each, a loop between), and on its own
        /// screen a thin red ring drawing tight round the enemy it would catch (no light: 승규 님 "불빛도 지워줘").</summary>
        void BishopLassoAim(RagdollPawn b)
        {
            if (b.SkillStage != SkillStage.Windup) return;
            bool own = b == Viewer;
            kit.Keep(b, "bishop lasso aim", () =>
            {
                var fx = new Kit.Fx();
                fx.strips.Add(TintStrip());   // the thin ring round the target's feet (its own screen only)
                // The rope it is about to throw, held (10-26, 승규 님: "비숍 미리보기 젓가락 같아 묶인 밧줄이 양손에 들고 있게"; it
                // was two thin lines out to the target): a coil of it in each hand and a loop of it hanging between them -
                // everyone sees it, it is the rope itself.
                var meshes = new Mesh[3];
                var objs = new GameObject[3];
                for (int i = 0; i < 3; i++)
                {
                    meshes[i] = new Mesh { name = i < 2 ? "Bishop rope coil in hand" : "Bishop rope loop between the hands", hideFlags = HideFlags.HideAndDontSave };
                    meshes[i].MarkDynamic();
                    objs[i] = LassoObject(meshes[i].name, meshes[i]);
                }
                const float CoilR = 0.075f, CoilThick = 0.045f;
                RopeCoil(meshes[0], CoilR, 0.07f, 2.5f, CoilThick);
                RopeCoil(meshes[1], CoilR, 0.07f, 2.5f, CoilThick);
                fx.end = () =>
                {
                    foreach (var o in objs) if (o != null) Destroy(o);
                    foreach (var m in meshes) Destroy(m);
                };
                var loop = new List<Vector3>();
                var circle = new List<Vector3>();
                RagdollPawn marked = null;
                float markStart = 0f;
                fx.step = (x, d) =>
                {
                    if (b == null || !kit.Held(x)) return false;
                    float fade = Mathf.Clamp01(x.age / (4f * Frame));
                    Vector3 handL = b.bodies[(int)BodyId.HandL].position, handR = b.bodies[(int)BodyId.HandR].position;
                    Vector3 fwd = Flat(b.Facing).sqrMagnitude > 1e-4f ? Flat(b.Facing).normalized : Vector3.forward;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd);
                    // The coils hang from the fists, their turns standing up across the body (the coil's axis sideways).
                    Quaternion coilTurn = Quaternion.LookRotation(fwd, right);
                    Vector3 hangL = handL + Vector3.down * (CoilR * 0.85f), hangR = handR + Vector3.down * (CoilR * 0.85f);
                    objs[0].transform.SetPositionAndRotation(hangL, coilTurn);
                    objs[1].transform.SetPositionAndRotation(hangR, coilTurn);
                    // The loop between them, hanging from the bottoms of the coils, swaying a little.
                    Vector3 a = hangL + Vector3.down * CoilR, z = hangR + Vector3.down * CoilR;
                    float sway = Mathf.Sin(kit.Clock * 3f) * 0.03f;
                    loop.Clear();
                    for (int j = 0; j <= 16; j++)
                    {
                        float u = j / 16f, bow = 4f * u * (1f - u);
                        loop.Add(Vector3.Lerp(a, z, u) + Vector3.down * (0.16f * bow) + fwd * ((0.04f + sway) * bow));
                    }
                    LassoRun(objs[2], meshes[2], loop, CoilThick);

                    var ring = x.strips[0];
                    var target = own ? b.BishopLassoAim : null;
                    if (target != marked) { marked = target; markStart = x.age; }
                    if (target != null)
                    {
                        Vector3 feet = Kit.FloorUnder(target.Hips.position, 1.5f);
                        float tight = Kit.EaseOut((x.age - markStart) / 0.22f);
                        float radius = Mathf.Lerp(1.1f, 0.55f, tight);
                        circle.Clear();
                        for (int j = 0; j <= 48; j++)
                        {
                            float ang = j * Mathf.PI * 2f / 48f;
                            circle.Add(Kit.FloorUnder(feet + new Vector3(Mathf.Cos(ang), 0.3f, Mathf.Sin(ang)) * radius, 1f) + Vector3.up * 0.016f);
                        }
                        Tint(ring, RookAimFoe);
                        ring.Build(circle, j => 0.03f, kit.Eye, true);
                        ring.taper = 0f;
                        ring.opacity = fade * 0.7f * Mathf.Lerp(0.4f, 1f, tight);
                        ring.Apply();
                    }
                    else ring.Hide();
                    return true;
                };
                return fx;
            });
        }

        // ---------------------------------------------------------------- its moments

        void BishopRopeHookFx(SkillFxEvent e)
        {
            // Hemp fibres knocked off as it wraps round, a jolt.
            RealChips(e.at, Vector3.up + e.dir * 0.3f, LassoHemp * 1.3f, 6, 1.6f);
            HitStop(3f * Frame);
            Shake(0.05f, 6f * Frame);
        }

        void BishopRopeReleaseFx(SkillFxEvent e)
        {
            // Let go taut, the piece is thrown its way: a short hold. (The burst of air there bloomed into a white blob over
            // the piece on the white floor.)
            if (e.count != 1 || e.target == null) return;
            HitStop(2f * Frame);
        }

        void BishopRopeSnapFx(SkillFxEvent e)
        {
            // Snapped: a burst of hemp fibres where it broke, a jolt (no flash: 승규 님 "불빛도 지워줘").
            RealChips(e.at, Vector3.up * 0.6f + Random.insideUnitSphere * 0.4f, LassoHemp * 1.4f, 14, 3f);
            HitStop(4f * Frame);
            Shake(0.12f, 10f * Frame);
        }
    }
}
