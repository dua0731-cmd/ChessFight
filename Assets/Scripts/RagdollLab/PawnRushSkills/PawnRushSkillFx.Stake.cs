using System.Collections.Generic;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R142 (승규 님 10-15, with a picture of a peg with the rope knotted round its top: "비숍 교차 밧줄 할 때 밧줄 지탱하는 곳을 좀
    /// 더 사실 적으로 묘사해줘"): what holds the bishop's rope is a real stake now, not a grey cylinder. A squared wooden stake
    /// with its corners taken off (8.5 cm across its flats, the faces a little uneven), its head mushroomed and split by the
    /// mallet, a notch cut round it where the rope is tied, driven into the floor leaning a little out away from the X (as a
    /// stake is set to take a pull), each turned a little its own way; in walnut-brown wood (Skill Wood: the growth rings as
    /// long wavy stripes down the faces and arcs on the head, fibres, checks, darker where it goes into the floor). It casts a
    /// shadow, and so does the rope. The rope is tied on with a clove hitch in the notch and leaves the stake from its side
    /// toward the X's middle (<see cref="RopeKnotMesh"/>). Driven in as the X lands (down 16 cm in three frames); sinking into
    /// the floor when the wire is gone. SkillTripwire's own pegs stay hidden while these show (<c>PegsDrawnElsewhere</c>).
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>Walnut-brown wood (main, pale wood, the grain's dark, ink).</summary>
        static readonly Kit.Palette StakeWood = new Kit.Palette("#8A6544", "#B48A5E", "#4E3622", "#2A1C12");
        /// <summary>The stake's width across its flats and how much is taken off each corner (m).</summary>
        const float StakeWide = 0.085f, StakeChamfer = 0.014f;
        /// <summary>How far the stake stands above where the rope is tied, and how far it goes into the floor (m).</summary>
        const float StakeHead = 0.07f, StakeDeep = 0.08f;
        /// <summary>How deep the notch the rope is tied in is cut into each face (m).</summary>
        const float StakeNotch = 0.005f;
        /// <summary>How far each stake leans out away from the X's middle (degrees).</summary>
        const float StakeLean = 7f;
        /// <summary>How far the stake drops as it is driven in when the X lands (m), and in how many frames.</summary>
        const float StakeDrive = 0.16f, StakeDriveFrames = 3f;

        Mesh meshStake;
        float meshStakeTie = -1f;
        Material matWood;

        void BuildStake()
        {
            var shader = Shader.Find("ChessFight/Skill Wood");
            if (shader == null) shader = Resources.Load<Shader>("PawnRushSkillFx/SkillWood");
            if (shader != null) matWood = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void DestroyStake()
        {
            if (matWood != null) Destroy(matWood);
            if (meshStake != null) Destroy(meshStake);
        }

        /// <summary>A stake standing with its rope tied on at <paramref name="tie"/> metres up (its own wood, its dark ink edge,
        /// a shadow).</summary>
        Kit.Prop StakeProp(float tie, string name)
        {
            var prop = new Kit.Prop(kit, StakeMesh(tie), StakeWood, name, null, matWood) { inkWidth = 0.007f };
            CastShadow(prop);
            return prop;
        }

        /// <summary>The prop's fill casts a shadow (its ink edge does not).</summary>
        static void CastShadow(Kit.Prop prop)
        {
            var r = prop.t.GetComponent<MeshRenderer>();
            if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>Each stake's own turn about its length (degrees), the same while it stands and as it sinks.</summary>
        static float StakeYaw(int i) => i == 0 ? 12f : i == 1 ? -17f : i == 2 ? 31f : -6f;

        /// <summary>How a stake at <paramref name="foot"/> stands: leaning out from <paramref name="middle"/> (the X's) by
        /// <see cref="StakeLean"/>, then as <paramref name="pull"/> leans it in (SkillTripwire.PegTie).</summary>
        static Quaternion StakeStand(Vector3 foot, Vector3 middle, Quaternion pull)
        {
            Vector3 outward = foot - middle;
            outward.y = 0f;
            if (outward.sqrMagnitude < 1e-6f) return pull;
            return pull * Quaternion.AngleAxis(StakeLean, Vector3.Cross(Vector3.up, outward.normalized));
        }

        /// <summary>
        /// The stake (built once for a tie height): its foot on the floor at the origin, up its y, <see cref="StakeDeep"/>
        /// into the floor. Eight flat faces (four flats, four chamfers), a little uneven; the notch from just under the
        /// hitch's lower turn to just over its upper one; the head flared out where the mallet mushroomed it, its top a little
        /// dished and uneven. Its normals are smooth (the kit's ink edge is pushed out along them: split per facet, it broke
        /// apart at the corners); Skill Wood shades each facet flat from its slope on screen.
        /// </summary>
        Mesh StakeMesh(float tie)
        {
            if (meshStake != null && Mathf.Abs(meshStakeTie - tie) < 1e-4f) return meshStake;
            if (meshStake == null) meshStake = new Mesh { name = "Bishop stake" };
            meshStakeTie = tie;
            float top = tie + StakeHead;
            if (matWood != null) matWood.SetFloat("_Top", top);
            float notch = 1f - StakeNotch / (StakeWide * 0.5f);
            float lo = tie - HitchPitch - RopeThick * 0.5f - 0.006f, hi = tie + RopeThick * 0.5f + 0.006f;
            // Rings up the stake: height, width (1: its flats StakeWide apart) and how uneven (m).
            float[] ys = { -StakeDeep, 0f, lo - 0.004f, lo, hi, hi + 0.004f, top - 0.022f, top - 0.009f, top };
            float[] ws = { 0.9f, 1f, 1f, notch, notch, 1f, 1f, 1.075f, 1.04f };
            float[] rough = { 0.001f, 0.0012f, 0.0012f, 0.001f, 0.001f, 0.0012f, 0.0015f, 0.0032f, 0.0024f };
            float s = StakeWide * 0.5f, c = StakeChamfer;
            var corners = new[]
            {
                new Vector2(s, -s + c), new Vector2(s, s - c), new Vector2(s - c, s), new Vector2(-s + c, s),
                new Vector2(-s, s - c), new Vector2(-s, -s + c), new Vector2(-s + c, -s), new Vector2(s - c, -s),
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int ring = 0; ring < ys.Length; ring++)
                for (int j = 0; j < 8; j++)
                {
                    Vector2 q = corners[j] * ws[ring] * (1f + StakeHash(ring, j) * rough[ring] / s);
                    float y = ys[ring] + (ring == ys.Length - 1 ? StakeHash(17, j) * 0.0015f : 0f);
                    verts.Add(new Vector3(q.x, y, q.y));
                }
            for (int ring = 0; ring < ys.Length - 1; ring++)
                for (int j = 0; j < 8; j++)
                {
                    int a = ring * 8 + j, b = ring * 8 + (j + 1) % 8;
                    Vector3 outward = verts[a] + verts[b];
                    outward.y = 0f;
                    FaceTo(verts, tris, a, a + 8, b, outward);
                    FaceTo(verts, tris, b, a + 8, b + 8, outward);
                }
            // The head: a little dished, uneven.
            int dish = verts.Count, rim = (ys.Length - 1) * 8;
            verts.Add(new Vector3(0f, top - 0.003f, 0f));
            for (int j = 0; j < 8; j++) FaceTo(verts, tris, dish, rim + j, rim + (j + 1) % 8, Vector3.up);
            meshStake.Clear();
            meshStake.SetVertices(verts);
            meshStake.SetTriangles(tris, 0);
            // Smooth normals, so its ink edge does not break apart at the corners; Skill Wood shades each facet flat.
            meshStake.RecalculateNormals();
            meshStake.RecalculateBounds();
            return meshStake;
        }

        /// <summary>A fixed -1..1 for a ring and a corner (the stake's unevenness, the same every time it is built).</summary>
        static float StakeHash(int a, int b)
        {
            float h = Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.547f;
            return (h - Mathf.Floor(h)) * 2f - 1f;
        }

        /// <summary>A triangle wound to face <paramref name="n"/> (Unity's front faces turn clockwise to the eye).</summary>
        static void FaceTo(List<Vector3> verts, List<int> tris, int a, int b, int c, Vector3 n)
        {
            if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), n) < 0f) (b, c) = (c, b);
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }
    }
}
