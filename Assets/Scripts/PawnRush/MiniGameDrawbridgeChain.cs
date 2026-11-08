using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R502 drawbridge chain of thick forged links (Resources/PawnRushMiniGames/Early/C/C_ChainLink.fbx,
    // ArtSource/PawnRush/MiniGames/Early/c_chain_link.py). As the R311 concept the chain hangs from an eye on the pylon
    // crown's floor to an eye on the deck's far corner (MgDrawbridge.ChainEnds), each eye a bigger link standing on a
    // bolted iron plate (user 11-07, "쇠사슬 접합부"); the links between follow the deck as it lowers, every other one
    // turned 90 degrees about the run. Picture only: no colliders, not synced (what it follows already is).
    public sealed class MiniGameDrawbridgeChain : MonoBehaviour
    {
        const float LinkLength = .45f;           // metres: a chunky link
        const float Pitch = LinkLength * .7f;    // the next link starts inside this one's end

        MgDrawbridge game;
        int index;
        Mesh mesh;
        Material material;
        Quaternion meshToZ = Quaternion.identity;   // turns the mesh's long axis onto +z
        float meshScale = 1f;
        readonly List<Transform> links = new List<Transform>();
        // R502 (user 11-07, "쇠사슬 접합부 부분 수정해"): the chain's ends hook into an eye - a bigger link standing on a
        // bolted iron plate - on the pylon crown's floor and on the deck's far corner.
        const float EyeScale = 1.35f;
        Transform topEye, endEye, topPlate, endPlate;
        readonly List<Vector3> path = new List<Vector3>();
        readonly List<float> along = new List<float>();

        public static MiniGameDrawbridgeChain Add(MgDrawbridge game, int index, Mesh mesh, Material material)
        {
            if (game == null || mesh == null) return null;
            var go = new GameObject("Drawbridge chain " + (index == 0 ? "W" : "E"));
            go.transform.SetParent(game.transform, false);
            var c = go.AddComponent<MiniGameDrawbridgeChain>();
            c.game = game;
            c.index = index;
            c.mesh = mesh;
            c.material = material;
            var e = mesh.bounds.size;
            Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
            c.meshToZ = Quaternion.FromToRotation(axis, Vector3.forward);
            c.meshScale = LinkLength / Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            c.topEye = c.NewLink();
            c.endEye = c.NewLink();
            c.topEye.name = c.endEye.name = "Chain eye";
            c.topEye.localScale = c.endEye.localScale = Vector3.one * c.meshScale * EyeScale;
            c.topPlate = c.NewPlate(new Vector3(.55f, .07f, .55f));
            c.endPlate = c.NewPlate(new Vector3(.5f, .06f, .4f));
            c.LateUpdate();
            return c;
        }

        Transform NewPlate(Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Chain eye plate";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        // An eye on a plate at a mount point on a face (normal n), turned so the chain (going `along`) runs through it;
        // returns the eye's middle, where the chain starts.
        Vector3 Mount(Transform eye, Transform plate, Vector3 at, Vector3 n, Vector3 along)
        {
            var flat = Vector3.ProjectOnPlane(along, n);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.Cross(n, Vector3.right);
            flat.Normalize();
            float h = plate.localScale.y;
            plate.position = at + n * (h * .5f);
            plate.rotation = Quaternion.LookRotation(flat, n);
            float r = LinkLength * EyeScale * .5f;
            var mid = at + n * (h + r * .8f);
            eye.position = mid;
            eye.rotation = Quaternion.LookRotation(n, flat) * Quaternion.Euler(0f, 0f, 90f) * meshToZ;   // stands up, across the first link
            return mid;
        }

        Transform NewLink()
        {
            var go = new GameObject("Chain link", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.transform.localScale = Vector3.one * meshScale;
            return go.transform;
        }

        void LateUpdate()
        {
            if (game == null || mesh == null || !game.ChainEnds(index, out var top, out var end)) return;
            var run = (end - top).normalized;
            var ea = Mount(topEye, topPlate, top, Vector3.up, run);
            var eb = Mount(endEye, endPlate, end, game.DeckUp, -run);
            path.Clear();
            path.Add(ea);
            path.Add(eb);
            along.Clear();
            along.Add(0f);
            for (int i = 1; i < path.Count; i++) along.Add(along[i - 1] + Vector3.Distance(path[i - 1], path[i]));
            float total = along[along.Count - 1];
            if (total < .05f) return;
            // the links run between the eyes (the concept: the chain hangs from the pylon's crown to the deck's corner)
            float edge = LinkLength * EyeScale * .35f;
            float shift = edge;
            int parity = 0;
            int n = Mathf.Max(1, Mathf.FloorToInt((total - 2f * edge) / Pitch) + 1);
            while (links.Count < n) links.Add(NewLink());
            int seg = 0;
            for (int k = 0; k < links.Count; k++)
            {
                var t = links[k];
                bool on = k < n;
                if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
                if (!on) continue;
                float s = Mathf.Min(shift + k * Pitch, total - edge);
                while (seg < path.Count - 2 && along[seg + 1] < s) seg++;
                var a = path[seg];
                var b = path[seg + 1];
                float l = Mathf.Max(along[seg + 1] - along[seg], 1e-4f);
                var dir = (b - a) / l;
                t.position = Vector3.Lerp(a, b, (s - along[seg]) / l);
                var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > .98f ? Vector3.forward : Vector3.up;
                t.rotation = Quaternion.LookRotation(dir, up) * Quaternion.Euler(0f, 0f, ((k + parity) & 1) == 0 ? 0f : 90f) * meshToZ;
            }
        }
    }

    // R502 drawbridge rope (user 11-07, "로프를 다시 만들어", then "당길때 끊어져 이런 부분 하나하나 신경 써"): one mesh,
    // rebuilt whenever the metres pulled change, along MgDrawbridge.RopeLine - wound on the drum, off its top, round both
    // pulleys and past the lead knot - so it never parts at a joint. Three strands twist round the line, 120 degrees
    // every 0.4 m (as c_rope_segment.py); the twist is measured from where the rope leaves the drum less the metres
    // pulled, so as it is pulled the strands flow off the drum, along the line and out past the knots. Picture only.
    public sealed class MiniGameDrawbridgeRope : MonoBehaviour
    {
        const float Thick = MgDrawbridge.RopeThick;
        const float Step = .06f;                                   // metres between rings
        const float Twist = 2f * Mathf.PI / 3f / .4f;              // radians a metre
        const int Strands = 3, Sides = 6;
        const float StrandR = Thick * .29f, StrandOff = Thick * .22f;

        MgDrawbridge game;
        Mesh mesh;
        float lastShown = float.NaN;
        readonly List<Vector3> line = new List<Vector3>();
        readonly List<float> len = new List<float>();
        readonly List<Vector3> pts = new List<Vector3>();
        readonly List<float> sAt = new List<float>();
        readonly List<Vector3> tan = new List<Vector3>();
        readonly List<Vector3> nrm = new List<Vector3>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();
        readonly float[] snap = new float[4];                // each knot's strain moment now, 0..1
        readonly float[] snapAt = new float[4];              // where along the rope (m) each knot is
        bool snapping;
        float tension, fibreClock, lastLift = -1f;

        // User 11-08 ("줄이 팽창 하는 거 말고 줄이 끊어질랑 말랑 하는 거 여려명이서 하면 그 강도가 쎄지고"): not a swell -
        // the rope looks ready to part, the harder the more pawns haul (MgDrawbridge.Tension01, from the synced net pull):
        // its three strands work loose and thin (up to 60 % apart, 15 % thinner) and loose fibres spit off it; the line
        // itself does not move (no trembling: "줄이 출렁 이면 안돼"). At each sprinting tug's strain sound (MgDrawbridge.StrainAge) the rope
        // round that knot frays hard for half a second (0.7 m either side) and sprays fibres. Off the drum only: the
        // wound coil stays tight.
        const float FrayMax = .6f, ThinMax = .15f, SnapReach = .7f;
        static readonly Color[] Fibres = { new Color(.86f, .74f, .55f), new Color(.72f, .6f, .42f), new Color(.95f, .88f, .72f) };

        static float SnapAt(float age) => age < 0f || age > .6f ? 0f : age < .06f ? age / .06f : Mathf.Exp(-(age - .06f) / .16f);

        // `rope` and `pieceMesh` are from the pieced rope this replaces: only rope 0 is made, the piece is not used.
        public static MiniGameDrawbridgeRope Add(MgDrawbridge game, int rope, Mesh pieceMesh, Material material)
        {
            if (game == null || rope != 0) return null;
            var go = new GameObject("Drawbridge rope", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(game.transform, false);           // the game frame: RopeLine's points are local here
            var r = go.AddComponent<MiniGameDrawbridgeRope>();
            r.game = game;
            r.mesh = new Mesh { name = "Drawbridge rope" };
            r.mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = r.mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            r.LateUpdate();
            return r;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        void LateUpdate()
        {
            if (game == null || mesh == null) return;
            float shown = game.Shown;
            bool wasLive = snapping || tension > .01f;
            snapping = false;
            for (int k = 0; k < 4; k++)
            {
                float age = game.StrainAge(k);
                float was = snap[k];
                snap[k] = SnapAt(age);
                if (snap[k] > 0f) snapping = true;
                if (age >= 0f && age < .1f && was <= 0f)        // the tug (first frame it shows): a spray of fibres at the knot
                    ArenaFx.Burst(game.KnotWorld(k) + Vector3.up * .1f, Fibres, 10, .9f, .8f, .6f, .03f);
            }
            tension = game.Tension01;
            bool live = snapping || tension > .01f;
            float liftSum = game.LiftSum;
            bool settling = Mathf.Abs(liftSum - lastLift) > 1e-4f;
            lastLift = liftSum;
            if (Mathf.Abs(shown - lastShown) < 1e-4f && !live && !wasLive && !settling) return;
            if (!game.RopeLine(line, out int depart) || line.Count < 2) { mesh.Clear(); return; }
            lastShown = shown;

            // even rings along the line
            len.Clear();
            len.Add(0f);
            for (int i = 1; i < line.Count; i++) len.Add(len[i - 1] + Vector3.Distance(line[i - 1], line[i]));
            float total = len[len.Count - 1];
            float sDep = len[depart];
            int n = Mathf.Max(2, Mathf.CeilToInt(total / Step) + 1);
            pts.Clear();
            sAt.Clear();
            int seg = 0;
            for (int i = 0; i < n; i++)
            {
                float sv = total * i / (n - 1);
                while (seg < line.Count - 2 && len[seg + 1] < sv) seg++;
                float l = Mathf.Max(len[seg + 1] - len[seg], 1e-5f);
                pts.Add(Vector3.Lerp(line[seg], line[seg + 1], Mathf.Clamp01((sv - len[seg]) / l)));
                sAt.Add(sv);
            }
            if (snapping)
                for (int k = 0; k < 4; k++)
                {
                    if (snap[k] <= 0f) continue;
                    var at = transform.InverseTransformPoint(game.KnotWorld(k));
                    float best = float.MaxValue;
                    for (int i = 0; i < n; i++)
                    {
                        float d = (pts[i] - at).sqrMagnitude;
                        if (d < best) { best = d; snapAt[k] = sAt[i]; }
                    }
                }
            tan.Clear();
            for (int i = 0; i < n; i++)
            {
                var d = pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)];
                tan.Add(d.sqrMagnitude > 1e-10f ? d.normalized : (i > 0 ? tan[i - 1] : Vector3.forward));
            }
            // frames carried along the line from where it leaves the drum (so the strands there never jump as the
            // coil shortens), both ways
            int k0 = Mathf.Clamp(Mathf.RoundToInt(sDep / total * (n - 1)), 0, n - 1);
            nrm.Clear();
            for (int i = 0; i < n; i++) nrm.Add(Vector3.zero);
            var n0 = Vector3.Cross(tan[k0], Vector3.right);
            if (n0.sqrMagnitude < 1e-6f) n0 = Vector3.Cross(tan[k0], Vector3.up);
            nrm[k0] = n0.normalized;
            for (int i = k0 + 1; i < n; i++) nrm[i] = Carry(nrm[i - 1], tan[i - 1], tan[i]);
            for (int i = k0 - 1; i >= 0; i--) nrm[i] = Carry(nrm[i + 1], tan[i + 1], tan[i]);

            verts.Clear();
            normals.Clear();
            uvs.Clear();
            tris.Clear();
            for (int i = 0; i < n; i++)
            {
                var t = tan[i];
                var nn = nrm[i];
                var bb = Vector3.Cross(t, nn);
                float psi = (sAt[i] - sDep - shown) * Twist;
                // the strain here: the haul along the whole run off the drum, plus a tug's moment round its knot
                float strain = 0f;
                if (sAt[i] > sDep)
                {
                    strain = tension * Mathf.Clamp01((sAt[i] - sDep) / .5f);
                    if (snapping)
                        for (int k = 0; k < 4; k++)
                            if (snap[k] > 0f)
                            {
                                float d = (sAt[i] - snapAt[k]) / SnapReach;
                                strain += snap[k] * Mathf.Exp(-d * d);
                            }
                    strain = Mathf.Min(strain, 1.4f);
                }
                float apart = 1f + FrayMax * strain, thin = 1f - ThinMax * Mathf.Min(1f, strain);
                var centre = pts[i];                       // the rope's line stays taut and still (user 11-08: "줄이 출렁 이면 안돼")
                for (int j = 0; j < Strands; j++)
                {
                    float th = psi + j * 2f * Mathf.PI / Strands;
                    var off = (nn * Mathf.Cos(th) + bb * Mathf.Sin(th)) * StrandOff * apart;
                    for (int k = 0; k < Sides; k++)
                    {
                        float be = k * 2f * Mathf.PI / Sides;
                        var dir = nn * Mathf.Cos(be) + bb * Mathf.Sin(be);
                        verts.Add(centre + off + dir * (StrandR * thin));
                        normals.Add(dir);
                        uvs.Add(new Vector2(k / (float)Sides, sAt[i] - shown));
                    }
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < Strands; j++)
                    for (int k = 0; k < Sides; k++)
                    {
                        int a = (i * Strands + j) * Sides + k, b = (i * Strands + j) * Sides + (k + 1) % Sides;
                        int c = a + Strands * Sides, d = b + Strands * Sides;
                        tris.Add(a); tris.Add(b); tris.Add(c);
                        tris.Add(b); tris.Add(d); tris.Add(c);
                    }
            // the strands' two ends closed
            for (int e = 0; e < 2; e++)
            {
                int i = e == 0 ? 0 : n - 1;
                var t = e == 0 ? -tan[i] : tan[i];
                for (int j = 0; j < Strands; j++)
                {
                    int ring = (i * Strands + j) * Sides;
                    var mid = Vector3.zero;
                    for (int k = 0; k < Sides; k++) mid += verts[ring + k];
                    int ci = verts.Count;
                    verts.Add(mid / Sides);
                    normals.Add(t);
                    uvs.Add(Vector2.zero);
                    for (int k = 0; k < Sides; k++)
                    {
                        int v0 = ring + k, v1 = ring + (k + 1) % Sides;
                        if (e == 0) { tris.Add(ci); tris.Add(v1); tris.Add(v0); }
                        else { tris.Add(ci); tris.Add(v0); tris.Add(v1); }
                    }
                }
            }
            if (tension > .01f)
            {
                fibreClock += Time.deltaTime * tension * 12f;
                while (fibreClock >= 1f)
                {
                    fibreClock -= 1f;
                    float s0 = Random.Range(sDep + .5f, total);
                    int i = Mathf.Clamp(Mathf.RoundToInt(s0 / total * (n - 1)), 0, n - 1);
                    ArenaFx.Burst(transform.TransformPoint(pts[i]), Fibres, 2, .4f, .35f, .5f, .025f);
                }
            }
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        // A frame's normal carried from one tangent to the next (turned by the bend, kept square to the new tangent).
        static Vector3 Carry(Vector3 n, Vector3 from, Vector3 to)
        {
            var m = Quaternion.FromToRotation(from, to) * n;
            m -= to * Vector3.Dot(m, to);
            return m.sqrMagnitude > 1e-10f ? m.normalized : n;
        }
    }
}
