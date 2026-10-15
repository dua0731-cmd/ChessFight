using System.Collections.Generic;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R138 (승규 님 10-14, with a picture of a piece tied in the middle of an X of ropes between four posts: "비숍이 스킬을
    /// 사용 할 때 밧줄에 걸려서 넘어 지는 게 아니라 밧줄에 묶이게 해줘 (실제 밧줄에 묶이는게 잘 보여야 돼)"): a piece the bishop's
    /// wire catches is tied up where it stands (RagdollPawn.SkillBind, SkillTripwire). What shows it: the wire's lines run
    /// from all four pegs up to its waist (SkillTripwire.LinePoints), and a rope winds round it, two and a half turns round
    /// its chest over its arms and two round its skirt, cinched tight in five frames with a white flash; it holds while the
    /// piece wriggles in it, then loosens, slips down and is gone when the piece is free. 4 cm thick (2.4 cm was a hairline
    /// at play distance; the first take's silver rope did not show on a grey piece over a white floor).
    /// R139 (승규 님: "비숍의 x자 줄도 실제 밧줄 모양으로 바꿔줘 (상대 묶는 밧줄이랑 동일)"): the X's two lines are that same rope
    /// (<see cref="RopeSpan"/>, the wire's own thin lines hidden): each half from its peg to the middle (or to where it is
    /// pulled, or to a tied piece's waist), sagging a little when slack and pulled straight when taut, the second line lying
    /// over the first where they cross, and a turn and a half of it knotted round each peg's top.
    /// R140 (승규 님: "밧줄 만 더 그래픽 적으로 밧줄이다! 라고 할 수 있게 만들어줘 색도 실제 밧줄 처럼 되게 해주고"): a laid rope
    /// you can tell at a glance: three strands twisted round each other (the tube's section is three round lobes turning
    /// along it, deep grooves between, <see cref="BuildRope"/>), its ends closed, in matte natural hemp (Skill Rope: each
    /// strand shaded round, yarn streaks, uneven fibres and a fuzz at the edge; a pale straw tan with brown grooves and a dark
    /// brown ink edge) instead of a glossy caramel tube with faint ridges.
    /// R142 (승규 님 10-15: "밧줄 지탱하는 곳을 좀 더 사실 적으로"): tied to a wooden stake (PawnRushSkillFx.Stake.cs) with a clove
    /// hitch (<see cref="RopeKnotMesh"/>) instead of a coil round a peg's top, each line leaving the hitch from the stake's
    /// side; the rope casts a shadow.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>R140: natural hemp, the same in either look (main, pale fibre, groove, ink).</summary>
        static readonly Kit.Palette HempRope = new Kit.Palette("#C9A46C", "#EED9AE", "#6A4A2A", "#3A2B1A");
        /// <summary>The rope's thickness (m), round a tied piece and along the X alike.</summary>
        const float RopeThick = 0.04f;
        /// <summary>How far along the rope one strand goes once round it (m): three crowns pass every third of this.</summary>
        const float RopeLay = 0.11f;
        Mesh meshRopeKnot;
        Material matRope;

        void BuildRope()
        {
            var shader = Shader.Find("ChessFight/Skill Rope");
            if (shader == null) shader = Resources.Load<Shader>("PawnRushSkillFx/SkillRope");
            if (shader != null) matRope = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void DestroyRope()
        {
            if (matRope != null) Destroy(matRope);
            if (meshRopeKnot != null) Destroy(meshRopeKnot);
        }

        /// <summary>A rope prop: the rope's own material (the kit's if Skill Rope is missing), its dark brown ink edge; R142: it
        /// casts a shadow.</summary>
        Kit.Prop RopeProp(Mesh mesh, string name, float inkWidth = 0.008f)
        {
            var prop = new Kit.Prop(kit, mesh, HempRope, name, null, matRope) { inkWidth = inkWidth };
            CastShadow(prop);
            return prop;
        }

        /// <summary>How far the hitch's two turns lie apart (m: a rope's thickness and a little).</summary>
        const float HitchPitch = RopeThick * 1.05f;
        /// <summary>How far the rope's middle lies from the stake's axis across a flat of its notch (pressed in a little).</summary>
        const float HitchReach = StakeWide * 0.5f - StakeNotch + RopeThick * 0.45f;
        /// <summary>Where the line leaves the hitch, from the tie point (the hitch's own frame: up the stake, +z to the line).</summary>
        static Vector3 HitchExit => new Vector3(HitchReach, 0f, 0f);

        /// <summary>
        /// The clove hitch round a stake (R142; a turn and a half round a peg's top before), built once: the tie point at its
        /// origin, the stake up its y, the line out along +z. Two turns round the notch, lying round the squared stake as a
        /// rounded square, the lower one first; across the stake's +x face the rope climbs from the lower turn to the upper one,
        /// riding over the end where it went in (the hitch's X); the upper turn leaves the stake at <see cref="HitchExit"/>
        /// along +z into the line; the end comes out from under the crossing the other way and hangs down 8 cm, 4–5 cm off
        /// to the side. (Where rope still presses into rope, Skill Rope shows its inside dark.)
        /// </summary>
        Mesh RopeKnotMesh()
        {
            if (meshRopeKnot != null) return meshRopeKnot;
            meshRopeKnot = new Mesh { name = "Bishop rope hitch" };
            const float a = HitchReach;
            var pts = new List<Vector3>();
            // The end, from its tip hanging below up into the lower turn (which starts a little low, under the crossing).
            Vector3 tip = new Vector3(a + 0.014f, -HitchPitch - 0.085f, -0.055f);
            Vector3 bend = new Vector3(a + 0.004f, -HitchPitch - 0.02f, -0.052f);
            Vector3 into = new Vector3(a, -HitchPitch - RopeThick * 0.35f, 0f);
            for (int i = 0; i < 16; i++)
            {
                float u = i / 16f;
                pts.Add((1f - u) * (1f - u) * tip + 2f * (1f - u) * u * bend + u * u * into);
            }
            // Two turns: the lower one, the climb across the +x face over the end, the upper one out to the line.
            const int steps = 160;
            for (int i = 0; i <= steps; i++)
            {
                float phi = i / (float)steps * Mathf.PI * 4f;
                float climb = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f * Mathf.PI - 0.7f, 2f * Mathf.PI + 0.7f, phi));
                // Riding over the end, gently (a rope cannot bend tighter than itself: a sharper rise turned the tube
                // inside out in pale patches).
                float over = Mathf.Exp(-Mathf.Pow((phi - 2f * Mathf.PI) / 0.75f, 2f));
                float low = RopeThick * 0.35f * (1f - Mathf.SmoothStep(0f, 1f, phi / 1.2f));
                float r = Squircle(phi) * a + RopeThick * 0.8f * over;
                pts.Add(new Vector3(Mathf.Cos(phi) * r, -HitchPitch * (1f - climb) - low, Mathf.Sin(phi) * r));
            }
            RopeAlong(meshRopeKnot, pts, RopeThick, false);
            return meshRopeKnot;
        }

        /// <summary>How far out a rounded square reaches at <paramref name="phi"/> (1 across its flats): rope round a squared
        /// stake, its corners rounded no tighter than the rope itself bends (a squarer path folded the tube over at them).</summary>
        static float Squircle(float phi) =>
            1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(Mathf.Cos(phi)), 3f) + Mathf.Pow(Mathf.Abs(Mathf.Sin(phi)), 3f), 1f / 3f);

        /// <summary>A rope along a run of points (R142), measured along them; its strands turned from the stake's axis (the
        /// mesh's y) outward; <paramref name="capEnd"/>: its far end closed.</summary>
        static void RopeAlong(Mesh mesh, List<Vector3> pts, float thick, bool capEnd = true)
        {
            int n = pts.Count;
            var run = new float[n];
            for (int i = 1; i < n; i++) run[i] = run[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            float length = run[n - 1];
            Vector3 Tangent(int i) => (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            BuildRope(mesh, length, (float s, out Vector3 at, out Vector3 along, out Vector3 side) =>
            {
                int i = 0;
                while (i < n - 2 && run[i + 1] < s) i++;
                float u = Mathf.Clamp01((s - run[i]) / Mathf.Max(1e-6f, run[i + 1] - run[i]));
                at = Vector3.Lerp(pts[i], pts[i + 1], u);
                along = Vector3.Lerp(Tangent(i), Tangent(i + 1), u).normalized;
                var outward = new Vector3(at.x, 0f, at.z);
                side = outward - along * Vector3.Dot(outward, along);
                if (side.sqrMagnitude < 1e-8f) side = Vector3.up - along * Vector3.Dot(Vector3.up, along);
                side.Normalize();
            }, thick, true, capEnd);
        }

        /// <summary>
        /// One length of the bishop's rope (R139): from one end to the other, hanging <c>sag</c> lower at its far end
        /// (where the two halves of a line meet, the bottom of the curve), made again only when an end moves.
        /// </summary>
        class RopeSpan
        {
            readonly Kit.Prop prop;
            readonly Mesh mesh;
            Vector3 builtFrom, builtTo;
            float builtSag = -1f;

            public RopeSpan(PawnRushSkillFx fx, string name)
            {
                mesh = new Mesh { name = name };
                mesh.MarkDynamic();
                prop = fx.RopeProp(mesh, name);
                prop.Show(false);
            }

            public void Place(Vector3 from, Vector3 to, float sag, float fade)
            {
                if (fade < 0.02f)
                {
                    prop.Show(false);
                    return;
                }
                if (builtSag < 0f || (from - builtFrom).sqrMagnitude > 4e-6f || (to - builtTo).sqrMagnitude > 4e-6f || Mathf.Abs(sag - builtSag) > 0.002f)
                {
                    RopeLine(mesh, from, to, sag, RopeThick, false);
                    builtFrom = from;
                    builtTo = to;
                    builtSag = sag;
                }
                prop.t.SetPositionAndRotation(from, Quaternion.identity);
                prop.t.localScale = Vector3.one;
                prop.fade = fade;
                prop.Show(true);
                prop.Apply();
            }

            public void Destroy()
            {
                prop.Destroy();
                if (mesh != null) Object.Destroy(mesh);
            }
        }

        /// <summary>The rope round a tied piece (R138), for <paramref name="seconds"/> or until it is free.</summary>
        void TieUp(SkillFxEvent e, float seconds)
        {
            var target = e.target;
            if (target == null || kit == null) return;
            // A client's copy of the wire hears of the tie from this moment (the host's lines close on their own).
            if (e.source is SkillTripwire wire && wire != null && wire.Remote) wire.NetBind(target, seconds);
            var upperMesh = new Mesh { name = "Tied rope (body)" };
            var lowerMesh = new Mesh { name = "Tied rope (legs)" };
            upperMesh.MarkDynamic();
            lowerMesh.MarkDynamic();
            var upper = RopeProp(upperMesh, "Tied rope (body)");
            var lower = RopeProp(lowerMesh, "Tied rope (skirt)");
            // A pawn (measured 10-14): hips 0.26 m up with its skirt 0.29 m round, chest 0.33 m (0.17 round), shoulders 0.52 m.
            float chestR = Mathf.Clamp(Girth(target.bodies[(int)BodyId.Chest], 0.17f), 0.12f, 0.3f);
            float skirtR = Mathf.Clamp(Girth(target.Hips, 0.29f), 0.15f, 0.4f);
            float released = -1f, builtUpper = -1f, builtLower = -1f;
            var f = kit.Run(seconds + 1.5f, (fx, d) =>
            {
                if (target == null) return false;
                bool tied = target.NetworkPuppet ? fx.age < seconds && target.State == PawnState.Active : target.SkillBound;
                if (!tied && released < 0f) released = fx.age;
                float loose = released >= 0f ? Mathf.Clamp01((fx.age - released) / 0.3f) : 0f;
                if (loose >= 1f) return false;
                float a = fx.age / Frame;
                // Cinched in five frames from loose round it (a little past tight, then settling), loosened when it is free.
                float cinch = a < 5f ? Mathf.Lerp(1.9f, 0.95f, Kit.EaseOut(a / 5f)) : a < 8f ? Mathf.Lerp(0.95f, 1f, (a - 5f) / 3f) : 1f;
                float grow = cinch * (1f + 0.9f * loose);
                float drop = 0.3f * loose * loose;

                Vector3 hips = target.Hips.position, chest = target.bodies[(int)BodyId.Chest].position;
                Vector3 axis = chest - hips;
                axis = axis.sqrMagnitude > 1e-4f ? axis.normalized : Vector3.up;
                // Round its chest and its arms (bound to its sides): three turns from just over the hips to under the
                // shoulders.
                float arms = Mathf.Max(OffAxis(target, BodyId.ArmL, hips, axis), OffAxis(target, BodyId.ArmR, hips, axis));
                float ru = (Mathf.Max(chestR + 0.03f, Mathf.Min(arms, chestR + 0.12f)) + 0.02f) * grow;
                if (Mathf.Abs(ru - builtUpper) > 0.004f)
                {
                    RopeCoil(upperMesh, ru, 0.17f, 3f, RopeThick);
                    builtUpper = ru;
                }
                upper.t.SetPositionAndRotation(hips + axis * 0.16f - Vector3.up * drop, Quaternion.FromToRotation(Vector3.up, axis));
                // Round its skirt (its legs are under it): two turns low down.
                float rl = (skirtR + 0.03f) * grow;
                if (Mathf.Abs(rl - builtLower) > 0.004f)
                {
                    RopeCoil(lowerMesh, rl, 0.07f, 2f, RopeThick);
                    builtLower = rl;
                }
                lower.t.SetPositionAndRotation(hips - axis * 0.07f - Vector3.up * drop, Quaternion.FromToRotation(Vector3.up, axis));
                float flash = a < 2f ? 1f : 0f;
                upper.flash = lower.flash = flash;
                upper.fade = lower.fade = 1f - loose;
                upper.Apply();
                lower.Apply();
                return true;
            });
            f.props.Add(upper);
            f.props.Add(lower);
            f.end = () =>
            {
                Destroy(upperMesh);
                Destroy(lowerMesh);
            };
            kit.PuffBurst(Kit.FloorUnder(target.Hips.position, 0.6f) + Vector3.up * 0.08f, kit.dust, 2, 0.35f, 0.14f, 0.4f, 0.1f);
        }

        /// <summary>How far round a body's collider reaches out from its middle, flat (m).</summary>
        static float Girth(Rigidbody body, float fallback)
        {
            if (body == null) return fallback;
            var col = body.GetComponent<Collider>();
            if (col == null) return fallback;
            var e = col.bounds.extents;
            return Mathf.Max(e.x, e.z);
        }

        /// <summary>How far a limb sticks out from the line up through the body (m), its own girth included.</summary>
        static float OffAxis(RagdollPawn pawn, BodyId id, Vector3 origin, Vector3 axis)
        {
            var body = pawn.bodies[(int)id];
            if (body == null) return 0f;
            Vector3 v = body.position - origin;
            Vector3 off = v - axis * Vector3.Dot(v, axis);
            return off.magnitude + Mathf.Min(0.08f, Girth(body, 0.04f) * 0.6f);
        }

        /// <summary>Where a rope's middle runs (R140): at <paramref name="s"/> metres along it, the point, the way on (unit)
        /// and a way square to that to measure round it from (unit).</summary>
        delegate void RopeSpine(float s, out Vector3 at, out Vector3 along, out Vector3 side);

        static readonly List<Vector3> ropeVerts = new List<Vector3>();
        static readonly List<Vector3> ropeNormals = new List<Vector3>();
        static readonly List<Vector2> ropeUvs = new List<Vector2>();
        static readonly List<int> ropeTris = new List<int>();

        /// <summary>
        /// A rope wound round the local y axis (R138): <paramref name="turns"/> turns of it <paramref name="thick"/> across at
        /// <paramref name="radius"/>, rising <paramref name="height"/> from end to end, centred on the origin.
        /// </summary>
        static void RopeCoil(Mesh mesh, float radius, float height, float turns, float thick)
        {
            float sweep = turns * Mathf.PI * 2f;
            float length = Mathf.Sqrt(radius * sweep * radius * sweep + height * height);
            BuildRope(mesh, length, (float s, out Vector3 at, out Vector3 along, out Vector3 side) =>
            {
                float u = s / length, ang = u * sweep;
                at = new Vector3(Mathf.Cos(ang) * radius, (u - 0.5f) * height, Mathf.Sin(ang) * radius);
                along = new Vector3(-Mathf.Sin(ang) * radius * sweep, height, Mathf.Cos(ang) * radius * sweep).normalized;
                var outward = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                side = (outward - along * Vector3.Dot(outward, along)).normalized;
            }, thick);
        }

        /// <summary>
        /// A length of rope (R139), in metres from <paramref name="from"/> (the mesh's origin) to <paramref name="to"/>,
        /// hanging in a half parabola <paramref name="sag"/> lower at <paramref name="to"/> (level there: the bottom of the
        /// curve a whole line makes between its two pegs).
        /// </summary>
        static void RopeLine(Mesh mesh, Vector3 from, Vector3 to, float sag, float thick, bool capFrom = true)
        {
            Vector3 span = to - from;
            float length = Mathf.Max(0.01f, span.magnitude);
            BuildRope(mesh, length, (float s, out Vector3 at, out Vector3 along, out Vector3 side) =>
            {
                float u = s / length;
                at = span * u + Vector3.down * (sag * (1f - (1f - u) * (1f - u)));
                along = span + Vector3.down * (sag * 2f * (1f - u));
                along = along.sqrMagnitude > 1e-8f ? along.normalized : Vector3.forward;
                side = Vector3.up - along * Vector3.Dot(Vector3.up, along);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.forward - along * Vector3.Dot(Vector3.forward, along);
                side.Normalize();
            }, thick, capFrom);
        }

        /// <summary>The rope's surface (R140): three strands, so its section is three round lobes (deep, sharp grooves
        /// between them) that turn once round every <see cref="RopeLay"/> along it.</summary>
        static Vector3 RopePoint(Vector3 at, Vector3 side, Vector3 across, float s, float a, float thick)
        {
            float lobe = Mathf.Sqrt(Mathf.Abs(Mathf.Cos(1.5f * (a - s / RopeLay * Mathf.PI * 2f))));
            float r = thick * 0.5f * (0.7f + 0.3f * lobe);
            return at + (side * Mathf.Cos(a) + across * Mathf.Sin(a)) * r;
        }

        /// <summary>
        /// The rope along <paramref name="spine"/> for <paramref name="length"/> metres (R140): rings every 1.2 cm, twelve
        /// round, each point's normal taken off the surface itself (so the grooves shade), uv x along it in lays and y once
        /// round it (for Skill Rope's strands and yarns), both ends closed (R142: unless it goes on into another length of
        /// rope there: a hitch into its line; a closed end showed as a pale patch in the join).
        /// </summary>
        static void BuildRope(Mesh mesh, float length, RopeSpine spine, float thick, bool capStart = true, bool capEnd = true)
        {
            const int around = 12;
            const float step = 0.012f, ds = 0.003f, da = 0.03f;
            int n = Mathf.Clamp(Mathf.CeilToInt(length / step), 2, 700) + 1;
            ropeVerts.Clear();
            ropeNormals.Clear();
            ropeUvs.Clear();
            ropeTris.Clear();
            Vector3 firstAt = Vector3.zero, firstAlong = Vector3.forward, lastAt = Vector3.zero, lastAlong = Vector3.forward;
            for (int i = 0; i < n; i++)
            {
                float s = length * i / (n - 1);
                spine(s, out var at, out var along, out var side);
                var across = Vector3.Cross(along, side);
                float s0 = Mathf.Max(0f, s - ds), s1 = Mathf.Min(length, s + ds);
                spine(s0, out var at0, out var along0, out var side0);
                spine(s1, out var at1, out var along1, out var side1);
                var across0 = Vector3.Cross(along0, side0);
                var across1 = Vector3.Cross(along1, side1);
                if (i == 0) { firstAt = at; firstAlong = along; }
                if (i == n - 1) { lastAt = at; lastAlong = along; }
                for (int j = 0; j <= around; j++)
                {
                    float a = j / (float)around * Mathf.PI * 2f;
                    var p = RopePoint(at, side, across, s, a, thick);
                    var pa = RopePoint(at, side, across, s, a + da, thick) - RopePoint(at, side, across, s, a - da, thick);
                    var ps = RopePoint(at1, side1, across1, s1, a, thick) - RopePoint(at0, side0, across0, s0, a, thick);
                    var normal = Vector3.Cross(ps, pa);
                    if (Vector3.Dot(normal, p - at) < 0f) normal = -normal;
                    ropeVerts.Add(p);
                    ropeNormals.Add(normal.sqrMagnitude > 1e-12f ? normal.normalized : (p - at).normalized);
                    ropeUvs.Add(new Vector2(s / RopeLay, j / (float)around));
                }
            }
            int ring = around + 1;
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < around; j++)
                {
                    int a0 = i * ring + j, b0 = a0 + ring;
                    ropeTris.Add(a0); ropeTris.Add(a0 + 1); ropeTris.Add(b0);
                    ropeTris.Add(a0 + 1); ropeTris.Add(b0 + 1); ropeTris.Add(b0);
                }
            // Close both ends: a fan from the middle, facing out along the rope.
            if (capStart) Cap(0, firstAt, -firstAlong, ring);
            if (capEnd) Cap((n - 1) * ring, lastAt, lastAlong, ring);
            mesh.Clear();
            mesh.SetVertices(ropeVerts);
            mesh.SetNormals(ropeNormals);
            mesh.SetUVs(0, ropeUvs);
            mesh.SetTriangles(ropeTris, 0);
            mesh.RecalculateBounds();
        }

        static void Cap(int first, Vector3 middle, Vector3 facing, int ring)
        {
            int c = ropeVerts.Count;
            ropeVerts.Add(middle);
            ropeNormals.Add(facing);
            ropeUvs.Add(new Vector2(0f, 0.5f));
            for (int j = 0; j < ring - 1; j++)
            {
                int a = first + j, b = first + j + 1;
                // The way round the ring is not known here: turn the triangle to face out.
                if (Vector3.Dot(Vector3.Cross(ropeVerts[a] - middle, ropeVerts[b] - middle), facing) > 0f)
                {
                    ropeTris.Add(c); ropeTris.Add(a); ropeTris.Add(b);
                }
                else
                {
                    ropeTris.Add(c); ropeTris.Add(b); ropeTris.Add(a);
                }
            }
        }
    }
}
