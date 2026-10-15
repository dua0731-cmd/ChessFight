using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The bishop's flying carpet (R130, 승규 님 10-13: "비숍 양탄자를 좀 더 멋있게 해줘 밑에 그려지는 그림자도 더 그래픽
    /// 좋게 구현 해주고"). It was a dark board square tilted 45° under its feet and a hard diamond on the floor. Now:
    /// - a carpet 1.3 by 0.95 m along the bishop's facing (<c>ChessFight/Skill Carpet</c>): violet velvet with a faint
    ///   damask lattice, a chessboard medallion of ivory and ebony framed in gold, a gold border with a chain of diamonds,
    ///   gold fringe hanging off its short ends; it ripples (a wave running from front to back) and a gold tassel hangs from
    ///   each corner. R133 (승규 님 10-14: "너무 펄럭펄럭 거려 자연스럽게"): it glides: its place and heading ease after the
    ///   bishop's, it banks into turns and lifts its leading edge a little as it drifts, the ripple is one slow swell and
    ///   the tassels hang on damped pendulums (it flapped faster and bigger as it moved);
    /// - under it a soft shadow (<c>ChessFight/Skill Soft Shadow</c>): a blurred rounded shape the size of the carpet,
    ///   softer and lighter the higher it floats, with a faint violet glow round its edge;
    /// - little sparkles of gold and violet sifting down from its underside now and then.
    /// It pops in under the feet, and when the bishop comes down it flutters, blinks three times and goes up in puffs.
    /// </summary>
    public partial class QueenHillSkillFx
    {
        const int CarpetAlong = 26, CarpetAcross = 12;
        const float CarpetLength = 1.42f, CarpetWidth = 0.95f, CarpetFringe = 0.06f;

        Material matCarpet, matSoftShadow;
        bool carpetLooked;

        sealed class Carpet
        {
            public GameObject go;
            public Mesh mesh;
            public MeshRenderer r;
            public Vector3[] rest, verts;
            public readonly MaterialPropertyBlock b = new MaterialPropertyBlock();
            public Transform shadow;
            public MeshRenderer shadowR;
            public readonly MaterialPropertyBlock sb = new MaterialPropertyBlock();
            public Prop[] tassels;
            public float drift, bank, sparkle;
            // R133: its own glide: its place easing after the feet, its heading after the bishop's, the lean of its drift
            // (local x across, y along), and a pendulum on each tassel (degrees about its x and z).
            public bool placed;
            public Vector3 pos, vel, heading;
            public Vector2 lean;
            public float yawRate, pitch;
            public readonly Vector2[] swing = new Vector2[4], swingVel = new Vector2[4];

            public void Destroy()
            {
                if (go != null) Object.Destroy(go);
                if (mesh != null) Object.Destroy(mesh);
                if (shadow != null) Object.Destroy(shadow.gameObject);
            }
        }

        /// <summary>The carpet's and the shadow's materials, made once (false if the shaders are missing: the old tile).</summary>
        bool CarpetReady()
        {
            if (!carpetLooked)
            {
                carpetLooked = true;
                var c = Find("ChessFight/Skill Carpet", "QueenHillSkillFx/SkillCarpet");
                var s = Find("ChessFight/Skill Soft Shadow", "QueenHillSkillFx/SkillSoftShadow");
                if (c != null && c.isSupported) matCarpet = new Material(c) { hideFlags = HideFlags.HideAndDontSave };
                if (s != null && s.isSupported) matSoftShadow = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            }
            return matCarpet != null && matSoftShadow != null;
        }

        void DestroyCarpetMaterials()
        {
            if (matCarpet != null) Destroy(matCarpet);
            if (matSoftShadow != null) Destroy(matSoftShadow);
        }

        Carpet NewCarpet()
        {
            var c = new Carpet { go = new GameObject("Bishop carpet") };
            c.go.transform.SetParent(root, false);
            c.mesh = new Mesh { name = "Bishop carpet", hideFlags = HideFlags.HideAndDontSave };
            c.mesh.MarkDynamic();
            int nx = CarpetAlong + 1, nz = CarpetAcross + 1;
            c.rest = new Vector3[nx * nz];
            var uv = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float u = i / (float)CarpetAlong, v = j / (float)CarpetAcross;
                    c.rest[j * nx + i] = new Vector3((v - 0.5f) * CarpetWidth, 0f, (u - 0.5f) * CarpetLength);
                    uv[j * nx + i] = new Vector2(u, v);
                }
            var tris = new List<int>();
            for (int j = 0; j < CarpetAcross; j++)
                for (int i = 0; i < CarpetAlong; i++)
                {
                    int a = j * nx + i, b2 = a + 1, cc = a + nx, d = cc + 1;
                    tris.AddRange(new[] { a, b2, cc, b2, d, cc });
                }
            c.verts = (Vector3[])c.rest.Clone();
            c.mesh.vertices = c.verts;
            c.mesh.uv = uv;
            c.mesh.SetTriangles(tris, 0);
            c.mesh.RecalculateNormals();
            c.mesh.bounds = new Bounds(Vector3.zero, new Vector3(CarpetWidth + 0.4f, 0.6f, CarpetLength + 0.4f));
            c.go.AddComponent<MeshFilter>().sharedMesh = c.mesh;
            c.r = c.go.AddComponent<MeshRenderer>();
            c.r.sharedMaterial = matCarpet;
            c.r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.r.receiveShadows = false;
            var sg = new GameObject("Bishop carpet shadow");
            sg.transform.SetParent(root, false);
            sg.AddComponent<MeshFilter>().sharedMesh = meshQuad;
            c.shadowR = sg.AddComponent<MeshRenderer>();
            c.shadowR.sharedMaterial = matSoftShadow;
            c.shadowR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.shadowR.receiveShadows = false;
            c.shadow = sg.transform;
            var gold = board ? BGold : new Palette("#FFD34D", "#FFF0A8", "#C7811A", "#3A2208");
            c.tassels = new Prop[4];
            for (int i = 0; i < 4; i++) c.tassels[i] = new Prop(this, SkillMeshes.Tassel(), gold, "Carpet tassel") { inkWidth = 0.006f };
            return c;
        }

        /// <summary>
        /// Lay the carpet under <paramref name="feet"/> along <paramref name="facing"/> this frame: <paramref name="scale"/>
        /// its pop, <paramref name="flutter"/> 0..1 its fluttering as it goes, <paramref name="shown"/> false for a blink.
        /// <paramref name="floorY"/> the floor under it (for the shadow).
        /// </summary>
        void PlaceCarpet(Carpet c, Vector3 feet, Vector3 facing, float floorY, float scale, float flutter, bool shown, float d)
        {
            // R133 (승규 님 10-14: "비숍 양탄자를 타서 움직일 때 너무 펄럭펄럭 거려 자연스럽게 움직이게"): it glides. It
            // followed the body's every shake and turned with each flick of the aim, and moving made its ripple faster and
            // bigger (a flag in the wind). Now its place eases after the feet, it turns slowly round to the bishop's heading,
            // banks into a turn and a sideways drift and lifts its front a little as it goes; the ripple is one slow swell.
            Vector3 f = FlatDir(facing, Vector3.forward);
            float dt = Mathf.Clamp(d, 1e-4f, 0.05f);
            if (!c.placed)
            {
                c.placed = true;
                c.pos = feet;
                c.vel = Vector3.zero;
                c.heading = f;
            }
            Vector3 before = c.vel;
            c.pos = Vector3.SmoothDamp(c.pos, feet, ref c.vel, 0.08f, Mathf.Infinity, dt);
            Vector3 acc = (c.vel - before) / dt;
            Vector3 move = new Vector3(c.vel.x, 0f, c.vel.z);
            c.drift = Mathf.Lerp(c.drift, Mathf.Clamp01(move.magnitude / 0.7f), 1f - Mathf.Exp(-3f * dt));
            Vector3 wasHeading = c.heading;
            c.heading = Vector3.Slerp(c.heading, f, 1f - Mathf.Exp(-3.5f * dt));
            c.heading = FlatDir(c.heading, f);
            c.yawRate = Mathf.Lerp(c.yawRate, Vector3.SignedAngle(wasHeading, c.heading, Vector3.up) / dt, 1f - Mathf.Exp(-6f * dt));
            Vector3 right = Vector3.Cross(Vector3.up, c.heading);
            var drift = Vector2.ClampMagnitude(new Vector2(Vector3.Dot(move, right), Vector3.Dot(move, c.heading)) / 0.7f, 1f);
            c.lean = Vector2.Lerp(c.lean, drift, 1f - Mathf.Exp(-3f * dt));
            // Into a turn (up to 8°) and a little into a sideways drift; its front up to 3° up as it goes forward.
            c.bank = Mathf.Lerp(c.bank, Mathf.Clamp(-c.yawRate * 0.05f, -8f, 8f) - c.lean.x * 4f, 1f - Mathf.Exp(-2.5f * dt));
            c.pitch = Mathf.Lerp(c.pitch, -c.lean.y * 3f, 1f - Mathf.Exp(-2.5f * dt));
            Quaternion rot = Quaternion.LookRotation(c.heading, Vector3.up) * Quaternion.Euler(c.pitch, 0f, c.bank);
            c.go.transform.SetPositionAndRotation(c.pos, rot);
            c.go.transform.localScale = Vector3.one * scale;

            float amp = 0.012f + 0.006f * c.drift + 0.03f * flutter, speed = 2.4f + 1.1f * c.drift + 10f * flutter;
            float bodyHalf = CarpetLength * (0.5f - CarpetFringe);
            // One slow swell running back along it (longer than the carpet, so it rolls rather than flaps), a gentle roll
            // across; drifting, the edge it goes toward curls up a little and the one behind trails lower; the fringe hangs.
            float Lift(float across, float along)
            {
                float back = Mathf.InverseLerp(bodyHalf, -bodyHalf, along);
                float y = amp * Mathf.Sin(along * 3.4f + clock * speed) * (0.45f + 0.55f * back);
                y += amp * 0.3f * Mathf.Sin(across * 3.2f - clock * speed * 0.7f + 1.3f);
                float toward = across / (CarpetWidth * 0.5f) * c.lean.x + along / bodyHalf * c.lean.y;
                float lead = Mathf.Clamp01((toward - 0.35f) / 0.65f), trail = Mathf.Clamp01((-toward - 0.35f) / 0.65f);
                return y + 0.022f * (lead * lead - 0.5f * trail * trail);
            }
            for (int k = 0; k < c.rest.Length; k++)
            {
                Vector3 p = c.rest[k];
                float along = p.z, across = p.x;
                float y = Lift(across, along);
                float past = Mathf.Abs(along) - bodyHalf;
                if (past > 0f) y -= past * 0.8f;
                c.verts[k] = new Vector3(across, y, along);
            }
            c.mesh.vertices = c.verts;
            c.mesh.RecalculateNormals();
            c.b.SetColor("_Color", Color.white);
            c.b.SetFloat("_Flash", flutter > 0.95f ? 1f : 0f);
            c.b.SetVector("_Size", new Vector4(CarpetLength * (1f - 2f * CarpetFringe), CarpetWidth, 0f, 0f));
            c.b.SetFloat("_Fringe", CarpetFringe);
            if (!board)
            {
                c.b.SetColor("_Field", look.Bishop.deep * 0.55f);
                c.b.SetColor("_FieldLit", look.Bishop.main);
            }
            c.r.SetPropertyBlock(c.b);
            c.r.enabled = shown;

            // Tassels at the body's four corners, hanging on a damped pendulum each (R133; they swung on their own, faster
            // and wider as it moved): left behind as it speeds up, carried on as it slows, out a little in a turn, with a
            // slow sway of their own.
            Vector3 accLocal = Quaternion.Inverse(rot) * acc;
            const float pendulum = 2f * Mathf.PI * 1.2f, damping = 0.3f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                Vector3 local = new Vector3(sx * CarpetWidth * 0.5f, 0f, sz * bodyHalf);
                local.y = Lift(local.x, local.z) - 0.01f;
                float turn = Mathf.Abs(c.yawRate) * 0.03f;
                var want = new Vector2(
                    Mathf.Clamp(accLocal.z * 5f - turn * sz + 3f * Mathf.Sin(clock * 1.7f + i * 1.3f) + 12f * flutter * Mathf.Sin(clock * 9f + i), -22f, 22f),
                    Mathf.Clamp(-accLocal.x * 5f + turn * sx + 3f * Mathf.Sin(clock * 1.4f + i * 2.1f), -22f, 22f));
                c.swingVel[i] += (pendulum * pendulum * (want - c.swing[i]) - 2f * damping * pendulum * c.swingVel[i]) * dt;
                c.swing[i] += c.swingVel[i] * dt;
                var t = c.tassels[i];
                Vector3 at = c.go.transform.TransformPoint(local);
                t.t.SetPositionAndRotation(at, rot * Quaternion.Euler(c.swing[i].x, 0f, c.swing[i].y));
                t.t.localScale = Vector3.one * scale;
                t.Show(shown);
                t.Apply();
            }

            // The shadow on the floor: the carpet's shape, softer and lighter the higher it floats; a faint glow round it.
            float height = Mathf.Max(0f, c.pos.y - floorY);
            c.shadow.SetPositionAndRotation(new Vector3(c.pos.x, floorY + 0.012f, c.pos.z), Quaternion.LookRotation(Vector3.up, c.heading));
            float soft = 0.08f + 0.12f * height;
            Vector2 size = new Vector2(CarpetWidth, CarpetLength * (1f - 2f * CarpetFringe)) * scale * (1f + 0.06f * height);
            Vector2 quad = size + Vector2.one * (4f * soft + 0.2f);
            c.shadow.localScale = new Vector3(quad.x, quad.y, 1f);
            c.sb.SetVector("_Size", new Vector4(size.x, size.y, 0f, 0f));
            c.sb.SetVector("_Quad", new Vector4(quad.x, quad.y, 0f, 0f));
            c.sb.SetFloat("_Round", 0.1f);
            c.sb.SetFloat("_Soft", soft);
            var shade = board ? new Color(0.09f, 0.07f, 0.16f, 0.62f / (1f + 0.55f * height)) : new Color(0.12f, 0.06f, 0.22f, 0.55f / (1f + 0.55f * height));
            c.sb.SetColor("_Color", shade);
            var glow = look.Bishop.light;
            c.sb.SetColor("_Glow", new Color(glow.r * 0.9f, glow.g * 0.75f, glow.b * 1.1f, 0.22f * scale));
            c.shadowR.SetPropertyBlock(c.sb);
            c.shadowR.enabled = scale > 0.01f;

            // Sparkles sifting down from under it now and then (gold and violet).
            c.sparkle += d * (6f + 6f * c.drift);
            while (c.sparkle >= 1f && shown)
            {
                c.sparkle -= 1f;
                Vector3 from = c.go.transform.TransformPoint(new Vector3(Random.Range(-0.42f, 0.42f) * CarpetWidth, -0.02f, Random.Range(-0.45f, 0.45f) * CarpetLength));
                Sparkle(from, Random.value < 0.5f ? look.Bishop : (board ? BGold : look.Queen));
            }
        }

        /// <summary>A tiny glinting ball falling slowly from the carpet and fading (R130).</summary>
        void Sparkle(Vector3 from, Palette p)
        {
            var puff = new Puff(this, p, meshBall) { ink = 0.25f, lump = 0f };
            float size = Random.Range(0.025f, 0.045f), drift = Random.Range(-0.15f, 0.15f);
            var f = Run(Random.Range(0.55f, 0.85f), (fx, d) =>
            {
                float k = fx.age / fx.life;
                puff.t.position = from + new Vector3(drift * k, -0.45f * k, drift * 0.5f * k);
                float tw = 0.75f + 0.25f * Mathf.Sin(fx.age * 40f);
                puff.t.localScale = Vector3.one * (size * (1f - k * 0.6f) * tw);
                puff.dissolve = Mathf.Clamp01((k - 0.55f) / 0.45f);
                puff.Apply(fx.age);
                return true;
            });
            f.puffs.Add(puff);
        }
    }
}
