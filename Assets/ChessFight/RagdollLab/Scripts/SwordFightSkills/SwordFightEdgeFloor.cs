using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>A point on the platform's edge: where, which way the void is, and how far off it was asked from.</summary>
    public struct EdgeHit
    {
        public Vector3 point;     // on the lip (the floor's top)
        public Vector3 outward;   // flat, toward the void
        public Vector3 along;     // flat, along the edge
        public float distance;    // flat, from the point asked about
        public float from, to;    // the edge piece's extent along <see cref="along"/> (metres from its start)
        public float at;          // where <see cref="point"/> is on it
        public Vector3 start;     // the edge piece's start
    }

    /// <summary>
    /// The Sword Fight test platform's edges (R103 edge skills) and the rook's 성벽 붕괴: the floor (the scene's one flat
    /// box, 14 × 14 m) is found once; its edge is its rim and, while a chunk of it is gone, that hole's sides. A collapse
    /// cuts a chunk out of the floor: the floor is rebuilt as boxes round it (the same material, every face's texture
    /// laid on as the original box had it, so nothing shows until it breaks) and the chunk is its own box: it cracks and
    /// trembles for <see cref="SwordFightEdgeParams.rookCrack"/>, then falls away (everyone over it falls with it,
    /// knocked loose) and the hole stays open for <see cref="SwordFightEdgeParams.rookRestore"/>, then the chunk rises back
    /// into place and, once no hole is left, the original floor comes back. Effects follow <see cref="Changed"/>.
    /// </summary>
    [DefaultExecutionOrder(-85)]   // before the skills (-75): a hole opened this step is a hole for them
    public class SwordFightEdgeFloor : MonoBehaviour
    {
        public enum Phase { Crack, Open, Rise, Done }

        public sealed class Collapse
        {
            public Rect rect;                // x / z on the floor
            public Vector3 outward, along;   // the edge it breaks off
            public Vector3 lip;              // the middle of its outer edge, on the lip
            public Phase phase;
            public float age, crack, open;   // seconds since it started; the crack's and the open hole's lengths
            public SwordFightSkills by;
            public GameObject chunk;         // the falling (and rising) box
            internal Transform look;         // its picture (trembles and tumbles; the collider does not)
            internal Rigidbody body;
            internal Vector3 home;
            internal Vector3 fallSpin;
            public float Rise => Mathf.Clamp01((age - crack - open) / RiseTime);
        }

        public const float RiseTime = 0.55f;

        public static SwordFightEdgeFloor Current { get; private set; }
        public static event Action<Collapse> Changed;

        public bool Found { get; private set; }
        /// <summary>The floor's top, x / z.</summary>
        public Rect Area { get; private set; }
        public float Top { get; private set; }
        public float Bottom { get; private set; }
        public readonly List<Collapse> Collapses = new List<Collapse>();

        Transform platform;
        Collider platformCollider;
        Renderer platformRenderer;
        Material floorMaterial;
        readonly List<GameObject> rest = new List<GameObject>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<(Vector3 a, Vector3 b, Vector3 outward)> edges = new List<(Vector3, Vector3, Vector3)>();
        bool edgesDirty = true;

        /// <summary>How the original box laid its texture on each face (u and v as planes in world space).</summary>
        readonly Dictionary<Vector3Int, (Vector3 u, Vector3 v)> faceMap = new Dictionary<Vector3Int, (Vector3, Vector3)>();

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        void OnDestroy()
        {
            foreach (var c in Collapses) if (c.chunk != null) Destroy(c.chunk);
            Collapses.Clear();
            Restore();
            foreach (var m in meshes) if (m != null) Destroy(m);
        }

        /// <summary>Find the floor: the collider under the middle of the scene (a box, not a piece).</summary>
        public bool Find()
        {
            if (Found && platform != null) return true;
            Found = false;
            foreach (var h in Physics.RaycastAll(new Vector3(0f, 20f, 0f), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (RagdollPawn.ColliderOwner.ContainsKey(h.collider) || h.collider.attachedRigidbody != null) continue;
                if (!(h.collider is BoxCollider) || h.normal.y < 0.9f) continue;
                platformCollider = h.collider;
                platform = h.collider.transform;
                platformRenderer = platform.GetComponent<Renderer>();
                floorMaterial = platformRenderer != null ? platformRenderer.sharedMaterial : null;
                var b = h.collider.bounds;
                Area = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                Top = b.max.y;
                Bottom = b.min.y;
                Found = true;
                break;
            }
            if (!Found) return false;
            MapFaces();
            edgesDirty = true;
            return true;
        }

        // ---------------------------------------------------------------- edges

        /// <summary>The floor under this point is there (not off the platform, not over an open hole).</summary>
        public bool OnFloor(Vector3 p)
        {
            if (!Find()) return true;
            var v = new Vector2(p.x, p.z);
            if (!Area.Contains(v)) return false;
            foreach (var c in Collapses)
                if ((c.phase == Phase.Open || c.phase == Phase.Rise && c.Rise < 0.9f) && c.rect.Contains(v)) return false;
            return true;
        }

        /// <summary>The nearest point of the edge (the rim, and the sides of any open hole) to <paramref name="p"/>.</summary>
        public bool Nearest(Vector3 p, out EdgeHit hit)
        {
            hit = default;
            if (!Find()) return false;
            if (edgesDirty) BuildEdges();
            float best = float.MaxValue;
            var q = new Vector3(p.x, 0f, p.z);
            foreach (var (a, b, n) in edges)
            {
                Vector3 ab = b - a;
                float len = ab.magnitude;
                if (len < 1e-3f) continue;
                Vector3 dir = ab / len;
                float t = Mathf.Clamp(Vector3.Dot(q - a, dir), 0f, len);
                Vector3 c = a + dir * t;
                float d = (q - c).magnitude;
                if (d >= best) continue;
                best = d;
                hit = new EdgeHit { point = new Vector3(c.x, Top, c.z), outward = n, along = dir, distance = d, from = 0f, to = len, at = t, start = new Vector3(a.x, Top, a.z) };
            }
            return best < float.MaxValue;
        }

        /// <summary>The edge where a flat ray from <paramref name="p"/> along <paramref name="dir"/> leaves the floor, within
        /// <paramref name="reach"/> (null if it stays on the floor that far).</summary>
        public bool Cast(Vector3 p, Vector3 dir, float reach, out EdgeHit hit)
        {
            hit = default;
            if (!Find()) return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return false;
            dir.Normalize();
            for (float s = 0.1f; s <= reach + 1e-3f; s += 0.1f)
            {
                Vector3 at = p + dir * s;
                if (OnFloor(at)) continue;
                return Nearest(p + dir * (s - 0.05f), out hit);
            }
            return false;
        }

        void BuildEdges()
        {
            edgesDirty = false;
            edges.Clear();
            var r = Area;
            // The rim, less the stretches an open hole has taken away.
            AddRim(new Vector3(r.xMin, 0f, r.yMin), new Vector3(r.xMax, 0f, r.yMin), Vector3.back);
            AddRim(new Vector3(r.xMax, 0f, r.yMin), new Vector3(r.xMax, 0f, r.yMax), Vector3.right);
            AddRim(new Vector3(r.xMax, 0f, r.yMax), new Vector3(r.xMin, 0f, r.yMax), Vector3.forward);
            AddRim(new Vector3(r.xMin, 0f, r.yMax), new Vector3(r.xMin, 0f, r.yMin), Vector3.left);
            foreach (var c in Collapses)
            {
                if (c.phase != Phase.Open && c.phase != Phase.Rise) continue;
                // A hole's three inner sides; the void is inside the hole.
                var h = c.rect;
                Vector3 a = new Vector3(h.xMin, 0f, h.yMin), b = new Vector3(h.xMax, 0f, h.yMin), d = new Vector3(h.xMax, 0f, h.yMax), e = new Vector3(h.xMin, 0f, h.yMax);
                var sides = new[] { (a, b, Vector3.forward), (b, d, Vector3.left), (d, e, Vector3.back), (e, a, Vector3.right) };
                foreach (var (s0, s1, n) in sides)
                {
                    // Not the side lying on the rim (that is open to the void).
                    Vector3 mid = (s0 + s1) * 0.5f - n * 0.05f;
                    if (!Area.Contains(new Vector2(mid.x, mid.z))) continue;
                    edges.Add((s0, s1, n));
                }
            }
        }

        void AddRim(Vector3 a, Vector3 b, Vector3 outward)
        {
            // Cut out every open hole's span along this side.
            var spans = new List<(float, float)> { (0f, (b - a).magnitude) };
            Vector3 dir = (b - a).normalized;
            foreach (var c in Collapses)
            {
                if (c.phase != Phase.Open && c.phase != Phase.Rise) continue;
                // Only a hole reaching this side (one by a corner may reach two).
                float reach = float.MinValue;
                foreach (var corner in new[] { new Vector3(c.rect.xMin, 0f, c.rect.yMin), new Vector3(c.rect.xMax, 0f, c.rect.yMax) })
                    reach = Mathf.Max(reach, Vector3.Dot(corner - a, outward));
                if (reach < -1e-3f) continue;
                Vector2 lo = c.rect.min, hi = c.rect.max;
                float t0 = Vector3.Dot(new Vector3(lo.x, 0f, lo.y) - a, dir), t1 = Vector3.Dot(new Vector3(hi.x, 0f, hi.y) - a, dir);
                if (t0 > t1) (t0, t1) = (t1, t0);
                var next = new List<(float, float)>();
                foreach (var (s, e) in spans)
                {
                    if (t1 <= s || t0 >= e) { next.Add((s, e)); continue; }
                    if (t0 > s) next.Add((s, t0));
                    if (t1 < e) next.Add((t1, e));
                }
                spans = next;
            }
            foreach (var (s, e) in spans)
                if (e - s > 0.05f) edges.Add((a + dir * s, a + dir * e, outward));
        }

        // ---------------------------------------------------------------- collapsing

        /// <summary>The chunk of floor a rook breaks off: <paramref name="width"/> along the edge round <paramref name="lip"/>,
        /// <paramref name="depth"/> in, kept on its side of the platform (and clear of the corners' far side).</summary>
        public Rect ChunkAt(EdgeHit edge, float width, float depth, out Vector3 lip)
        {
            var r = Area;
            Vector3 n = edge.outward, t = edge.along;
            float half = width * 0.5f;
            // Slide it along its side so it stays on it.
            float along = Mathf.Clamp(edge.at, Mathf.Min(half, edge.to * 0.5f), Mathf.Max(edge.to - half, edge.to * 0.5f));
            Vector3 c = edge.start + t * along;
            lip = new Vector3(c.x, Top, c.z);
            Vector3 p0 = c - t * half, p1 = c + t * half - n * depth;
            var rect = Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.z, p1.z), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.z, p1.z));
            return Rect.MinMaxRect(Mathf.Max(rect.xMin, r.xMin), Mathf.Max(rect.yMin, r.yMin), Mathf.Min(rect.xMax, r.xMax), Mathf.Min(rect.yMax, r.yMax));
        }

        /// <summary>Somewhere a collapse still going on already has.</summary>
        public bool Overlaps(Rect rect)
        {
            foreach (var c in Collapses) if (c.phase != Phase.Done && c.rect.Overlaps(rect)) return true;
            return false;
        }

        public Collapse Begin(Rect rect, Vector3 outward, Vector3 along, Vector3 lip, float crack, float open, SwordFightSkills by)
        {
            if (!Find() || platform == null || Quaternion.Angle(platform.rotation, Quaternion.identity) > 0.5f) return null;
            var c = new Collapse { rect = rect, outward = outward, along = along, lip = lip, crack = crack, open = open, by = by, phase = Phase.Crack };
            c.home = new Vector3(rect.center.x, (Top + Bottom) * 0.5f, rect.center.y);
            c.chunk = new GameObject("Edge collapse chunk");
            c.chunk.transform.position = c.home;
            c.body = c.chunk.AddComponent<Rigidbody>();
            c.body.isKinematic = true;
            c.body.interpolation = RigidbodyInterpolation.Interpolate;
            var box = c.chunk.AddComponent<BoxCollider>();
            box.size = new Vector3(rect.width, Top - Bottom, rect.height);
            if (platformCollider != null) box.sharedMaterial = platformCollider.sharedMaterial;
            c.look = Box(c.chunk.transform, c.home, box.size, "Chunk picture", false).transform;
            c.fallSpin = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized;
            Collapses.Add(c);
            Rebuild();
            Changed?.Invoke(c);
            return c;
        }

        void FixedUpdate()
        {
            if (Collapses.Count == 0) return;
            float dt = Time.fixedDeltaTime;
            bool rebuild = false;
            for (int i = Collapses.Count - 1; i >= 0; i--)
            {
                var c = Collapses[i];
                c.age += dt;
                switch (c.phase)
                {
                    case Phase.Crack:
                        if (c.age >= c.crack)
                        {
                            c.phase = Phase.Open;
                            Drop(c);
                            edgesDirty = true;
                            Changed?.Invoke(c);
                        }
                        break;
                    case Phase.Open:
                        if (c.age >= c.crack + c.open)
                        {
                            c.phase = Phase.Rise;
                            // Back from below: solid again, pushing up whatever is in the way.
                            c.chunk.GetComponent<Collider>().enabled = true;
                            c.look.gameObject.SetActive(true);
                            c.look.localPosition = Vector3.zero;
                            c.look.localRotation = Quaternion.identity;
                            c.chunk.transform.position = c.home + Vector3.down * 2.5f;
                            c.body.position = c.chunk.transform.position;
                            Changed?.Invoke(c);
                        }
                        break;
                    case Phase.Rise:
                    {
                        float k = c.Rise;
                        c.body.MovePosition(c.home + Vector3.down * (2.5f * (1f - Ease(k))));
                        if (k >= 1f)
                        {
                            c.phase = Phase.Done;
                            edgesDirty = true;
                            Changed?.Invoke(c);
                            Destroy(c.chunk);
                            Collapses.RemoveAt(i);
                            rebuild = true;
                        }
                        break;
                    }
                }
            }
            if (rebuild) Rebuild();
        }

        static float Ease(float k) => 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f) + 0.06f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);

        void Update()
        {
            // The pictures: the chunk trembles harder as the crack runs, and tumbles away once it goes.
            foreach (var c in Collapses)
            {
                if (c.look == null) continue;
                if (c.phase == Phase.Crack)
                {
                    float k = Mathf.Clamp01(c.age / Mathf.Max(0.05f, c.crack));
                    float amp = Mathf.Lerp(0.004f, 0.03f, k * k);
                    float t = Time.time * 70f;
                    c.look.localPosition = new Vector3(Mathf.Sin(t) * amp, Mathf.Sin(t * 1.37f + 1f) * amp * 0.6f, Mathf.Sin(t * 0.83f + 2f) * amp);
                }
                else if (c.phase == Phase.Open)
                {
                    float f = c.age - c.crack;
                    c.look.localPosition = Vector3.down * (0.5f * 14f * f * f + 0.4f * f);
                    c.look.localRotation = Quaternion.AngleAxis(f * 70f, Vector3.Cross(Vector3.up, c.fallSpin) + c.outward * 0.6f);
                    c.look.gameObject.SetActive(f < 1.4f);
                }
            }
        }

        /// <summary>The chunk lets go: its collider goes, the floor round the hole stays, and every piece standing over it
        /// is knocked loose and falls with it.</summary>
        void Drop(Collapse c)
        {
            c.chunk.GetComponent<Collider>().enabled = false;
            foreach (var p in RagdollPawn.All)
            {
                if (p == null) continue;
                var hips = p.Hips.position;
                if (!c.rect.Contains(new Vector2(hips.x, hips.z)) || hips.y < Bottom - 0.2f || hips.y > Top + 1.5f) continue;
                var f = p.GetComponent<SwordFightPawn>();
                if (f != null && !f.Alive) continue;
                p.TakeHit(Vector3.down * 1.5f + c.outward * 0.4f, 0.65f, 0f, true);
                if (f != null) f.StopCombat();
            }
        }

        /// <summary>Back to the one original floor once no collapse is left.</summary>
        void Restore()
        {
            foreach (var go in rest) if (go != null) Destroy(go);
            rest.Clear();
            if (platformCollider != null) platformCollider.enabled = true;
            if (platformRenderer != null) platformRenderer.enabled = true;
            edgesDirty = true;
        }

        /// <summary>The floor as boxes round every chunk still away (cracking, gone or coming back).</summary>
        void Rebuild()
        {
            foreach (var go in rest) if (go != null) Destroy(go);
            rest.Clear();
            edgesDirty = true;
            if (Collapses.Count == 0)
            {
                Restore();
                return;
            }
            if (platformCollider != null) platformCollider.enabled = false;
            if (platformRenderer != null) platformRenderer.enabled = false;
            var r = Area;
            var xs = new List<float> { r.xMin, r.xMax };
            var zs = new List<float> { r.yMin, r.yMax };
            foreach (var c in Collapses)
            {
                xs.Add(c.rect.xMin); xs.Add(c.rect.xMax);
                zs.Add(c.rect.yMin); zs.Add(c.rect.yMax);
            }
            xs = Sorted(xs);
            zs = Sorted(zs);
            // Cells not in any hole, merged along x into strips, then strips of the same span merged along z.
            var strips = new List<Rect>();
            for (int j = 0; j + 1 < zs.Count; j++)
            {
                float z0 = zs[j], z1 = zs[j + 1];
                float? start = null;
                for (int i = 0; i + 1 < xs.Count; i++)
                {
                    var cell = Rect.MinMaxRect(xs[i], z0, xs[i + 1], z1);
                    bool hole = false;
                    foreach (var c in Collapses) hole |= c.rect.Overlaps(Shrink(cell));
                    if (!hole && start == null) start = xs[i];
                    if (hole && start != null) { strips.Add(Rect.MinMaxRect(start.Value, z0, xs[i], z1)); start = null; }
                }
                if (start != null) strips.Add(Rect.MinMaxRect(start.Value, z0, xs[xs.Count - 1], z1));
            }
            var merged = new List<Rect>();
            foreach (var s in strips)
            {
                int k = merged.FindIndex(m => Mathf.Abs(m.xMin - s.xMin) < 1e-4f && Mathf.Abs(m.xMax - s.xMax) < 1e-4f && Mathf.Abs(m.yMax - s.yMin) < 1e-4f);
                if (k >= 0) merged[k] = Rect.MinMaxRect(merged[k].xMin, merged[k].yMin, merged[k].xMax, s.yMax);
                else merged.Add(s);
            }
            foreach (var m in merged)
            {
                Vector3 size = new Vector3(m.width, Top - Bottom, m.height);
                var go = Box(transform, new Vector3(m.center.x, (Top + Bottom) * 0.5f, m.center.y), size, "Floor round the hole", true);
                rest.Add(go);
            }
        }

        static Rect Shrink(Rect r) => Rect.MinMaxRect(r.xMin + 1e-3f, r.yMin + 1e-3f, r.xMax - 1e-3f, r.yMax - 1e-3f);

        static List<float> Sorted(List<float> v)
        {
            v.Sort();
            var o = new List<float>();
            foreach (float f in v) if (o.Count == 0 || f - o[o.Count - 1] > 1e-4f) o.Add(f);
            return o;
        }

        // ---------------------------------------------------------------- boxes that look like the floor

        /// <summary>A box of floor at <paramref name="centre"/>: the floor's material, each face's texture where the original
        /// box had it; with a collider if <paramref name="solid"/>.</summary>
        GameObject Box(Transform parent, Vector3 centre, Vector3 size, string name, bool solid)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = centre;
            go.layer = platform != null ? platform.gameObject.layer : 0;
            var mesh = BoxMesh(centre, size);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = floorMaterial;
            if (platformRenderer != null)
            {
                mr.shadowCastingMode = platformRenderer.shadowCastingMode;
                mr.receiveShadows = platformRenderer.receiveShadows;
            }
            if (solid)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = size;
                if (platformCollider != null) box.sharedMaterial = platformCollider.sharedMaterial;
            }
            return go;
        }

        static readonly Vector3Int[] Faces = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };

        Mesh BoxMesh(Vector3 centre, Vector3 size)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Vector3 h = size * 0.5f;
            foreach (var f in Faces)
            {
                Vector3 n = f;
                // Two axes across the face.
                Vector3 a = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 b = Vector3.Cross(n, a);
                Vector3 c0 = Vector3.Scale(n, h);
                var corners = new Vector3[4];
                for (int k = 0; k < 4; k++)
                {
                    float sa = k == 0 || k == 3 ? -1f : 1f, sb = k < 2 ? -1f : 1f;
                    corners[k] = c0 + Vector3.Scale(a, h) * sa + Vector3.Scale(b, h) * sb;
                }
                int i0 = verts.Count;
                foreach (var p in corners)
                {
                    verts.Add(p);
                    norms.Add(n);
                    uvs.Add(Uv(f, centre + p));
                }
                // Front faces wind so that Cross(b - a, c - a) points out.
                bool flip = Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), n) < 0f;
                if (!flip) tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                else tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            var m = new Mesh { name = "Floor box" };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        Vector2 Uv(Vector3Int face, Vector3 world)
        {
            if (!faceMap.TryGetValue(face, out var map)) return new Vector2(world.x, world.z) / 14f;
            Vector2 c = InPlane(face, world);
            return new Vector2(map.u.x * c.x + map.u.y * c.y + map.u.z, map.v.x * c.x + map.v.y * c.y + map.v.z);
        }

        static Vector2 InPlane(Vector3Int face, Vector3 p) =>
            face.x != 0 ? new Vector2(p.y, p.z) : face.y != 0 ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);

        /// <summary>Fit each face's u and v as planes over the original box's vertices, so the new boxes carry on its texture.</summary>
        void MapFaces()
        {
            faceMap.Clear();
            var mf = platform != null ? platform.GetComponent<MeshFilter>() : null;
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null || !mesh.isReadable) return;
            var v = mesh.vertices;
            var n = mesh.normals;
            var uv = mesh.uv;
            if (uv == null || uv.Length != v.Length || n.Length != v.Length) return;
            var m = platform.localToWorldMatrix;
            foreach (var face in Faces)
            {
                var pts = new List<(Vector2 c, Vector2 uv)>();
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 wn = m.MultiplyVector(n[i]).normalized;
                    if (Vector3.Dot(wn, face) < 0.99f) continue;
                    pts.Add((InPlane(face, m.MultiplyPoint3x4(v[i])), uv[i]));
                }
                if (pts.Count < 3) continue;
                // Three corners that are not on one line.
                for (int a = 0; a < pts.Count; a++)
                    for (int b = a + 1; b < pts.Count; b++)
                        for (int c = b + 1; c < pts.Count; c++)
                        {
                            if (faceMap.ContainsKey(face)) continue;
                            if (Solve(pts[a], pts[b], pts[c], out var mu, out var mv)) faceMap[face] = (mu, mv);
                        }
            }
        }

        static bool Solve((Vector2 c, Vector2 uv) p, (Vector2 c, Vector2 uv) q, (Vector2 c, Vector2 uv) r, out Vector3 mu, out Vector3 mv)
        {
            mu = mv = default;
            float det = p.c.x * (q.c.y - r.c.y) - p.c.y * (q.c.x - r.c.x) + (q.c.x * r.c.y - r.c.x * q.c.y);
            if (Mathf.Abs(det) < 1e-6f) return false;
            Vector3 Plane(float a, float b, float c)
            {
                // a = x*p.x + y*p.y + z (and so on): Cramer's rule.
                float dx = a * (q.c.y - r.c.y) - p.c.y * (b - c) + (b * r.c.y - c * q.c.y);
                float dy = p.c.x * (b - c) - a * (q.c.x - r.c.x) + (q.c.x * c - r.c.x * b);
                float dz = p.c.x * (q.c.y * c - r.c.y * b) - p.c.y * (q.c.x * c - r.c.x * b) + a * (q.c.x * r.c.y - r.c.x * q.c.y);
                return new Vector3(dx / det, dy / det, dz / det);
            }
            mu = Plane(p.uv.x, q.uv.x, r.uv.x);
            mv = Plane(p.uv.y, q.uv.y, r.uv.y);
            return true;
        }
    }
}
