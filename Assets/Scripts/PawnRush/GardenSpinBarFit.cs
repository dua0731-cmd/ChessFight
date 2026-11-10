using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R532 (승규 11-09, "큰 회전 막대에 기물이랑 회전이랑 충돌 위치가 안맞아 공간이 생겨", "끝에 깜빡깜빡 거려"): a garden spin
    // bar's dressing put back on its graybox and made to collide as it is drawn.
    //
    // The handmade pass (R478, handmade_pass.py step 2) turned, leaned and shifted the dressing's parts, "a third as much"
    // on pieces that ride movers - still enough on a 5 m bar: the tall bar's hub sat 0.15 m off the axis it turns about
    // (so it wobbled round it), two of its arms were turned 2.5 and 4 degrees (0.3 m off at their tips) and the low bars
    // leaned, their tips up to 5 cm high or low. The graybox's colliders stayed where the bars should be, so a pawn
    // stopped short of one side of a log and sank into the other.
    //
    // On waking (in Play, so nothing made here is ever saved into a scene or a prefab), from the dressing's own mesh:
    //   seat    each arm (its logs and everything on them: bands, rivets, the end caps, the spacers between a pair) is moved
    //           as one rigid piece so its lowest log lies on the graybox's log (Bar log capsules, LogColliders), level,
    //           pointing down its arm, its tip TipGap inside the arm's reach; the hub's turned parts are centred on the axis
    //   tuck    each log's end disc is pulled back under its gold cap: the two were drawn in one plane (plaza_sample_v4.py
    //           log(): the wood ends at `end` and the cap's rim face is at `end` too), so they flickered as the bar turned
    //   collide the graybox's Bar log capsules and Hub give way to a convex hull of every part big enough to touch - each
    //           log, band, collar and cap (a part that spans both logs of a pair split between them), the hub's drum, band,
    //           foot and dome - so the bar collides exactly as it is drawn; rivets, sprigs and the knob are too small to
    //           matter. The hub's hulls cannot be climbed, as the graybox's hub.
    // Nothing is done (the graybox stays) when the dressing is missing or does not have a log for each graybox log.
    [DisallowMultipleComponent]
    public sealed class GardenSpinBarFit : MonoBehaviour
    {
        const float TipGap = .02f;    // plaza_sample_v4.py: the drawn end stays this far inside the collider's reach
        const float Tuck = .03f;      // behind the cap's recessed face (0.018 in from its rim)
        const float MinHull = .25f;   // parts smaller than this collide as nothing (rivets, sprigs, the knob, a lone spacer)
        const float HubRadius = .3f;  // a part whose middle is this close to the axis belongs to the hub
        const int MaxHullPoints = 120;   // at most 2n - 4 = 236 faces
        const float Weld = 2000f;    // vertices within 0.5 mm are one (the FBX splits them where normals or UVs part)

        [SerializeField] MeshFilter dressing;

        Mesh seated;
        readonly List<Mesh> hulls = new List<Mesh>();

        sealed class Part
        {
            public int sub;
            public List<int> verts = new List<int>();
            public Bounds box;
            public Log log;    // this part is a log
            public Arm arm;    // the arm it rides (null: the hub)
        }

        sealed class Log
        {
            public Part part;
            public Vector3 start, end, axis;   // where the drawing has it (the bar's frame)
            public float length, radius;
            public CapsuleCollider graybox;
        }

        sealed class Arm
        {
            public Vector3 dir;
            public readonly List<CapsuleCollider> grayboxLogs = new List<CapsuleCollider>();
            public readonly List<Log> logs = new List<Log>();
            public Matrix4x4 seat = Matrix4x4.identity;
        }

        // For the builder: the dressing hung on this mover (RoyalWaterGardenBuilder.Hang).
        public void Configure(MeshFilter dressingMesh) => dressing = dressingMesh;

        void Awake()
        {
            if (!Fit()) Debug.LogWarning("[정원] 회전 막대 장식을 맞추지 못해 그레이박스 충돌을 씁니다: " + name);
        }

        void OnDestroy()
        {
            if (seated != null) Destroy(seated);
            foreach (var hull in hulls) if (hull != null) Destroy(hull);
        }

        bool Fit()
        {
            if (dressing == null) return false;
            var mesh = dressing.sharedMesh;
            var view = dressing.GetComponent<MeshRenderer>();
            if (mesh == null || !mesh.isReadable || view == null) return false;
            var slots = view.sharedMaterials;

            // Everything is worked out in the bar's own frame (y up through the hub, at rest or not: only local transforms).
            var toBar = transform.worldToLocalMatrix * dressing.transform.localToWorldMatrix;
            var source = mesh.vertices;
            var p = new Vector3[source.Length];
            for (int i = 0; i < p.Length; i++) p[i] = toBar.MultiplyPoint3x4(source[i]);

            var arms = GrayboxArms();
            if (arms.Count == 0) return false;
            var parts = Parts(mesh, p, slots);

            // The logs, each matched to the graybox arm it points along and, within the arm, to its graybox log by height.
            foreach (var part in parts)
            {
                if (!IsWood(slots, part.sub) || Mathf.Max(part.box.size.x, part.box.size.y, part.box.size.z) < .5f) continue;
                var log = FitLog(part, p);
                if (log == null) continue;
                Arm best = null;
                float bestDot = .8f;
                foreach (var arm in arms)
                {
                    float d = Vector3.Dot(Flat(log.axis), arm.dir);
                    if (d > bestDot) { bestDot = d; best = arm; }
                }
                if (best == null) continue;   // wood that is no log of an arm
                part.log = log;
                best.logs.Add(log);
                part.arm = best;
            }
            foreach (var arm in arms)
            {
                if (arm.logs.Count != arm.grayboxLogs.Count || arm.logs.Count == 0) return false;
                arm.logs.Sort((a, b) => a.start.y.CompareTo(b.start.y));
                arm.grayboxLogs.Sort((a, b) => a.center.y.CompareTo(b.center.y));
                for (int k = 0; k < arm.logs.Count; k++) arm.logs[k].graybox = arm.grayboxLogs[k];
                arm.seat = Seat(arm);
            }

            // Everything else rides the log it sits on (bands, rivets, caps, spacers) or is the hub's.
            var logs = new List<Log>();
            foreach (var arm in arms) logs.AddRange(arm.logs);
            Vector3 hubShift = Vector3.zero;
            float hubSize = 0f;
            foreach (var part in parts)
            {
                if (part.log != null) continue;
                var middle = part.box.center;
                if (Flat(middle).magnitude < HubRadius)
                {
                    float size = Mathf.Max(part.box.size.x, part.box.size.z);
                    if (size > hubSize) { hubSize = size; hubShift = -Flat(middle); }
                    continue;
                }
                Log near = null;
                float nearest = float.MaxValue;
                foreach (var log in logs)
                {
                    float d = DistanceToAxis(middle, log);
                    if (d < nearest) { nearest = d; near = log; }
                }
                if (near != null && nearest <= near.radius + .25f) part.arm = ArmOf(arms, near);
            }

            // Seat every part: an arm's parts as their arm, the hub's turned lathes each centred on the axis, the rest of the
            // hub (the sprigs on its faces) as its biggest piece.
            var moved = (Vector3[])p.Clone();
            var done = new bool[p.Length];
            var shifts = new Dictionary<Part, Matrix4x4>();
            foreach (var part in parts)
            {
                Matrix4x4 m;
                if (part.arm != null) m = part.arm.seat;
                else if (Flat(part.box.center).magnitude < HubRadius && Mathf.Abs(part.box.size.x - part.box.size.z) < .03f * Mathf.Max(part.box.size.x, .01f))
                    m = Matrix4x4.Translate(-Flat(part.box.center));
                else m = Matrix4x4.Translate(hubShift);
                shifts[part] = m;
                foreach (int i in part.verts)
                {
                    if (done[i]) continue;
                    done[i] = true;
                    moved[i] = m.MultiplyPoint3x4(p[i]);
                }
            }

            foreach (var log in logs)
            {
                var m = shifts[log.part];
                log.start = m.MultiplyPoint3x4(log.start);
                log.axis = m.MultiplyVector(log.axis).normalized;
            }

            // The handmade pass turned each group of gold pieces on its own, so a collar, band or cap can sit off its log
            // (the tall bar's first collar 0.18 m to one side): each ring round a log is centred back on it, and the rivets
            // and spacers on it go with it.
            var rings = new List<KeyValuePair<Part, Vector3>>();
            var offsets = new Dictionary<Part, Vector3>();
            foreach (var part in parts)
            {
                if (part.arm == null || part.log != null) continue;
                var centre = Box(part.verts, moved).center;
                var near = NearestLog(part.arm, centre);
                var rel = centre - near.start;
                var lateral = rel - near.axis * Vector3.Dot(rel, near.axis);
                // A band pair and the spacer between them are one piece across both logs: it stays where it is.
                bool oneLog = true;
                foreach (int i in part.verts)
                    if (NearestLog(part.arm, moved[i]) != near) { oneLog = false; break; }
                if (!oneLog) continue;
                float spread = 0f;
                foreach (int i in part.verts)
                {
                    var r = moved[i] - near.start;
                    r -= near.axis * Vector3.Dot(r, near.axis);
                    spread = Mathf.Max(spread, (r - lateral).magnitude);
                }
                if (spread < near.radius * .9f) continue;   // not round the log
                offsets[part] = -lateral;
                rings.Add(new KeyValuePair<Part, Vector3>(part, centre));
            }
            foreach (var part in parts)
            {
                if (part.arm == null || part.log != null || offsets.ContainsKey(part)) continue;
                var centre = Box(part.verts, moved).center;
                float best = .35f;
                foreach (var ring in rings)
                {
                    if (ring.Key.arm != part.arm) continue;
                    float d = Vector3.Distance(ring.Value, centre);
                    if (d < best) { best = d; offsets[part] = offsets[ring.Key]; }
                }
            }
            foreach (var pair in offsets)
            {
                foreach (int i in pair.Key.verts) moved[i] += pair.Value;
                shifts[pair.Key] = Matrix4x4.Translate(pair.Value) * shifts[pair.Key];
            }

            // Tuck each log's end disc under its cap.
            foreach (var log in logs)
            {
                foreach (int i in log.part.verts)
                    if (Vector3.Dot(moved[i] - log.start, log.axis) > log.length - .005f) moved[i] -= log.axis * Tuck;
                log.end = log.start + log.axis * (log.length - Tuck);
            }

            // The drawing, seated (normals and tangents turned with their parts).
            var toMesh = toBar.inverse;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var vertsOut = new Vector3[source.Length];
            for (int i = 0; i < source.Length; i++) vertsOut[i] = toMesh.MultiplyPoint3x4(moved[i]);
            foreach (var part in parts)
            {
                var turn = toMesh * shifts[part] * toBar;
                foreach (int i in part.verts)
                {
                    if (normals.Length == source.Length) normals[i] = turn.MultiplyVector(normals[i]).normalized;
                    if (tangents.Length == source.Length)
                    {
                        var t = turn.MultiplyVector(tangents[i]).normalized;
                        tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                    }
                }
            }
            seated = Instantiate(mesh);
            seated.name = mesh.name + " (seated)";
            seated.vertices = vertsOut;
            if (normals.Length == source.Length) seated.normals = normals;
            if (tangents.Length == source.Length) seated.tangents = tangents;
            seated.RecalculateBounds();
            dressing.sharedMesh = seated;

            // The drawing's own shape collides; then the graybox's colliders give way to it.
            int made = 0;
            foreach (var part in parts)
            {
                var box = Box(part.verts, moved);
                if (Mathf.Max(box.size.x, box.size.y, box.size.z) < MinHull) continue;
                if (part.arm == null)
                {
                    Hull(part, part.verts, moved, true, ref made);
                    continue;
                }
                // A part on an arm hulls with its own log only: a band and spacer that join a pair split between them.
                var byLog = new Dictionary<Log, List<int>>();
                foreach (int i in part.verts)
                {
                    Log near = null;
                    float nearest = float.MaxValue;
                    foreach (var log in part.arm.logs)
                    {
                        float d = DistanceToAxis(moved[i], log);
                        if (d < nearest) { nearest = d; near = log; }
                    }
                    if (!byLog.TryGetValue(near, out var list)) byLog[near] = list = new List<int>();
                    list.Add(i);
                }
                foreach (var list in byLog.Values) Hull(part, list, moved, false, ref made);
            }
            if (made == 0) return false;
            foreach (var arm in arms)
                foreach (var capsule in arm.grayboxLogs) Destroy(capsule);
            var hub = transform.Find("Hub");
            if (hub != null && hub.TryGetComponent<Collider>(out var hubCollider)) Destroy(hubCollider);
            return true;
        }

        // ------------------------------------------------------------------ the graybox

        List<Arm> GrayboxArms()
        {
            var arms = new List<Arm>();
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Arm ")) continue;
                var arm = new Arm { dir = Flat(child.localRotation * Vector3.right).normalized };
                foreach (var capsule in child.GetComponentsInChildren<CapsuleCollider>(true))
                    if (capsule.name.StartsWith("Bar log")) arm.grayboxLogs.Add(capsule);
                if (arm.grayboxLogs.Count > 0) arms.Add(arm);
            }
            return arms;
        }

        // The rigid move that lays the arm's lowest log on its graybox log: level, down the arm, its tip TipGap inside the
        // reach; the arm's other logs keep their places on it.
        static Matrix4x4 Seat(Arm arm)
        {
            var low = arm.logs[0];
            var forward = Vector3.zero;
            foreach (var log in arm.logs) forward += log.axis;
            forward.Normalize();
            var up = arm.logs.Count > 1 ? arm.logs[arm.logs.Count - 1].start - low.start : Vector3.up;
            up = Vector3.ProjectOnPlane(up, forward).normalized;
            var capsule = low.graybox;
            float reach = capsule.center.x + capsule.height * .5f;
            var tip = arm.dir * (reach - TipGap) + Vector3.up * capsule.center.y;
            var from = Matrix4x4.TRS(low.start, Quaternion.LookRotation(forward, up), Vector3.one);
            var to = Matrix4x4.TRS(tip - arm.dir * low.length, Quaternion.LookRotation(arm.dir, Vector3.up), Vector3.one);
            return to * from.inverse;
        }

        static Log NearestLog(Arm arm, Vector3 point)
        {
            Log near = null;
            float nearest = float.MaxValue;
            foreach (var log in arm.logs)
            {
                float d = DistanceToAxis(point, log);
                if (d < nearest) { nearest = d; near = log; }
            }
            return near;
        }

        static Arm ArmOf(List<Arm> arms, Log log)
        {
            foreach (var arm in arms) if (arm.logs.Contains(log)) return arm;
            return null;
        }

        // ------------------------------------------------------------------ the drawing

        static bool IsWood(Material[] slots, int sub) =>
            sub < slots.Length && slots[sub] != null && slots[sub].name.ToLowerInvariant().Contains("wood");

        // A log is a closed lathe (plaza_sample_v4.py log()): rings at its two ends, a disc on each. Its axis runs from the
        // middle of the hub end's ring to the middle of the tip's.
        static Log FitLog(Part part, Vector3[] p)
        {
            var out_ = Flat(part.box.center);
            if (out_.sqrMagnitude < 1e-4f) return null;
            out_.Normalize();
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (int i in part.verts)
            {
                float a = Vector3.Dot(p[i], out_);
                lo = Mathf.Min(lo, a);
                hi = Mathf.Max(hi, a);
            }
            // An end ring turned a few degrees off `out_` spreads along it by up to r sin(turn): take a generous band.
            // Each place once: the seam's doubled vertices would pull the middle a centimetre toward them.
            Vector3 s = Vector3.zero, e = Vector3.zero;
            int ns = 0, ne = 0;
            var seen = new HashSet<Vector3Int>();
            foreach (int i in part.verts)
            {
                if (!seen.Add(Vector3Int.RoundToInt(p[i] * 500f))) continue;
                float a = Vector3.Dot(p[i], out_);
                if (a < lo + .06f) { s += p[i]; ns++; }
                else if (a > hi - .06f) { e += p[i]; ne++; }
            }
            if (ns == 0 || ne == 0) return null;
            s /= ns;
            e /= ne;
            var log = new Log { part = part, start = s, end = e, axis = (e - s).normalized, length = (e - s).magnitude };
            foreach (int i in part.verts) log.radius = Mathf.Max(log.radius, Vector3.Cross(p[i] - s, log.axis).magnitude);
            return log;
        }

        static float DistanceToAxis(Vector3 point, Log log)
        {
            float t = Mathf.Clamp(Vector3.Dot(point - log.start, log.axis), 0f, log.length);
            return Vector3.Distance(point, log.start + log.axis * t);
        }

        // The connected pieces of each submesh; vertices at the same place join.
        static List<Part> Parts(Mesh mesh, Vector3[] p, Material[] slots)
        {
            var parts = new List<Part>();
            var parent = new int[p.Length];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                var used = new HashSet<int>(tris);
                foreach (int i in used) parent[i] = i;
                var at = new Dictionary<Vector3Int, int>();
                foreach (int i in used)
                {
                    var key = Vector3Int.RoundToInt(p[i] * Weld);
                    if (at.TryGetValue(key, out int other)) Join(parent, i, other);
                    else at[key] = i;
                }
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Join(parent, tris[t], tris[t + 1]);
                    Join(parent, tris[t], tris[t + 2]);
                }
                var byRoot = new Dictionary<int, Part>();
                foreach (int i in used)
                {
                    int root = Find(parent, i);
                    if (!byRoot.TryGetValue(root, out var part))
                    {
                        byRoot[root] = part = new Part { sub = s, box = new Bounds(p[i], Vector3.zero) };
                        parts.Add(part);
                    }
                    part.verts.Add(i);
                    part.box.Encapsulate(p[i]);
                }
            }
            return parts;
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        static void Join(int[] parent, int a, int b)
        {
            a = Find(parent, a);
            b = Find(parent, b);
            if (a != b) parent[a] = b;
        }

        static Bounds Box(List<int> verts, Vector3[] p)
        {
            var box = new Bounds(p[verts[0]], Vector3.zero);
            foreach (int i in verts) box.Encapsulate(p[i]);
            return box;
        }

        // A convex collider round the given vertices (the bar's frame), on a child of the bar.
        void Hull(Part part, List<int> verts, Vector3[] p, bool hub, ref int made)
        {
            var box = Box(verts, p);
            if (Mathf.Max(box.size.x, box.size.y, box.size.z) < MinHull) return;
            var points = new List<Vector3>();
            var seen = new HashSet<Vector3Int>();
            foreach (int i in verts)
                if (seen.Add(Vector3Int.RoundToInt(p[i] * 500f))) points.Add(p[i]);   // 2 mm is close enough to touch
            // PhysX keeps a hull under 256 faces and cuts a bigger one short (the hub's dome, 80 sides by 4 rings): thin the
            // points on a coarser grid, keeping the outermost in each cell, until there are few enough.
            for (float cell = .01f; points.Count > MaxHullPoints; cell *= 1.4f)
            {
                var outer = new Dictionary<Vector3Int, Vector3>();
                foreach (int i in verts)
                {
                    var key = Vector3Int.FloorToInt(p[i] / cell);
                    if (!outer.TryGetValue(key, out var kept) || (p[i] - box.center).sqrMagnitude > (kept - box.center).sqrMagnitude)
                        outer[key] = p[i];
                }
                points = new List<Vector3>(outer.Values);
            }
            if (points.Count < 4) return;
            // The cooker builds the hull from the points; a fan of triangles only makes the mesh valid.
            var tris = new List<int>();
            for (int k = 1; k + 1 < points.Count; k++) { tris.Add(0); tris.Add(k); tris.Add(k + 1); }
            var shape = new Mesh { name = name + " hull " + made };
            shape.SetVertices(points);
            shape.SetTriangles(tris, 0);
            hulls.Add(shape);
            var go = new GameObject((hub ? "Hub hull " : "Arm hull ") + made);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = shape;
            collider.convex = true;
            if (hub) go.AddComponent<NoClimbSurface>();
            made++;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
