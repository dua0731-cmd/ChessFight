using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // B1 the entry passage, the servery and the waiters' parade (seoungtest R837; ArtSource/PawnRush/BanquetHall/
    // bh_b1_servery_model.py, shapes after the R836 sheets BH_A03 and BH_A11): the screens, the counter and its ramp, the
    // kitchen's doorway and the pantry's, and each waiter (the user's figurine, bh_figures.py) with his tray and plate
    // stack riding his root. Colliders, floors and the waiters' motion stay the builder's.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly Hang[] ServeryHangs =
        {
            new Hang { zone = "B1", model = "bh_screens", covers = new[] { "BH screen S", "BH screen E", "BH screen cap S", "BH passage end" } },
            new Hang { zone = "B1", model = "bh_counter", covers = new[] { "BH counter", "BH counter ramp", "BH spare plates" } },
            new Hang { zone = "B1", model = "bh_kitchen", covers = new[] { "BH kitchen wall", "BH kitchen roof", "BH kitchen lintel" } },
            new Hang { zone = "B1", model = "bh_pantry", covers = new[] { "BH pantry post", "BH pantry lintel", "BH pantry wall W", "BH pantry roof" } },
            Waiter(1), Waiter(2), Waiter(3), Waiter(4),
        };

        static Hang Waiter(int k) => new Hang
        {
            zone = "B1", model = "bh_waiter", on = "BH waiter " + k, under = "BH waiter " + k,
            covers = new[] { "BH tray", "BH plate stack", "BH waiter body", "BH waiter head", "BH waiter arm" },
        };

        // The kitchen's half doors (user 11-25: "열렸다 닫히게"): each leaf on a pivot at its hinge (bh_b1_servery_model
        // LEAF_HINGES), swung out into the hall as each tray comes through (KitchenDoors).
        static readonly Action<GrandBanquetHallDressing> KitchenLeaves = d =>
        {
            var w = d.Leaf("W", new Vector3(26.11f, 1.6f, 3.645f));
            var e = d.Leaf("E", new Vector3(31.51f, 1.6f, 3.645f));
            if (w == null || e == null) return;
            var waiters = new List<Transform>();
            foreach (var mover in d.GetComponentsInChildren<BanquetWaiter>(true)) waiters.Add(mover.transform);
            d.gameObject.AddComponent<KitchenDoors>().Configure(w, e, waiters);
        };

        // The waiters' plate stacks rattle as they walk (user 11-25: "접시가 조금씩 움직이지만 판정은 그대로"): the model's
        // one stack mesh is cut into its plates, each on a pivot of its own (PlateStackRattle). The builder's
        // "BH plate stack" collider - what is climbed and stood on - is not touched.
        static readonly Action<GrandBanquetHallDressing> RattlingStacks = d =>
        {
            foreach (var mover in d.GetComponentsInChildren<BanquetWaiter>(true))
            {
                var model = ModelOn(mover.transform, "bh_waiter");
                if (model == null) continue;
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name == "BH1 plate stack")
                    {
                        PlateStackRattle.Split(mover.transform, r);
                        break;
                    }
            }
        };

        Transform Leaf(string side, Vector3 hinge)
        {
            var fbx = Resources.Load<GameObject>(ModelFolder + "B1/bh_kitchen_leaf_" + side);
            if (fbx == null) return null;
            var pivot = new GameObject("BH kitchen leaf " + side + " hinge").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = hinge;
            HangOn(pivot, fbx);
            return pivot;
        }
    }

    // The kitchen's two half doors, a picture on the waiters' clock-driven places (nothing is sent): a leaf swings out
    // 90 degrees into the hall (west about +y the one way, east the other) as a tray's front comes within 0.35 s of the
    // doorway, stays open while the tray goes through, and swings shut 0.35 s after its back has passed. Open, a leaf
    // lies along its post's doorway face, off the trays' way (x >= 26.3) and the waiter's body (x <= 30.8).
    [DefaultExecutionOrder(200)]
    public sealed class KitchenDoors : MonoBehaviour
    {
        // A waiter root's z (the tray's middle) at which the leaves start to open, are open, start to close, are shut:
        // the doorway's plane is at z 3.645 and the tray is 1.5 m round; 2.2 m/s for 0.35 s is 0.77 m.
        public const float OpenFrom = 1.38f, OpenAt = 2.15f, CloseFrom = 5.15f, ShutAt = 5.92f, Swing = 90f;

        [SerializeField] Transform west, east;
        [SerializeField] List<Transform> waiters = new List<Transform>();

        public void Configure(Transform westPivot, Transform eastPivot, List<Transform> waiterRoots)
        {
            west = westPivot;
            east = eastPivot;
            waiters = waiterRoots;
        }

        // How open the doors are (0 shut .. 1 open) for a tray's middle at z.
        public static float Openness(float z)
        {
            float a = Mathf.Clamp01((z - OpenFrom) / (OpenAt - OpenFrom)), b = Mathf.Clamp01((z - CloseFrom) / (ShutAt - CloseFrom));
            a = a * a * (3f - 2f * a);
            b = b * b * (3f - 2f * b);
            return a * (1f - b);
        }

        void LateUpdate()
        {
            float open = 0f;
            foreach (var w in waiters)
                if (w != null) open = Mathf.Max(open, Openness(w.localPosition.z));
            if (west != null) west.localRotation = Quaternion.Euler(0f, -Swing * open, 0f);
            if (east != null) east.localRotation = Quaternion.Euler(0f, Swing * open, 0f);
        }
    }

    // A waiter's plate stack, rattling as he walks - a picture only. The stack's mesh (bh_b1_servery_model.plate_stack:
    // ten plates, each nesting 6 % into the one over it) is cut into its plates, each on a pivot at its foot in the
    // waiter root's frame. Every pose is a function of how far the waiter has walked (his root's z, itself the obstacle
    // clock's), so every PC sees the same rattle and it stops when the clock stops:
    //   - the whole stack leans a little side to side with his steps, more the higher the plate;
    //   - at each step every plate jumps a little its own way (a slide, a tilt, a lift) and settles before the next.
    // The collider is the builder's cylinder, still: the plates stay within a few centimetres of it.
    [DefaultExecutionOrder(200)]
    public sealed class PlateStackRattle : MonoBehaviour
    {
        public const int Plates = 10;                     // bh_b1_servery_model.waiter: plate_stack(..., 10)
        public const float Stride = 1.1f;                 // metres walked between two clinks
        // user 11-25 "좀 더 흔들리게 해": every size about doubled from the first pass (.035, .018; .014, 1.3, .007)
        public const float Lean = .075f, Nod = .04f;      // the top plate's sway across and along the walk, metres
        public const float Slide = .03f, Tilt = 2.8f, Lift = .015f;    // a plate's own jump: metres, degrees, metres

        [SerializeField] Transform[] plates = new Transform[0];
        [SerializeField] Vector3[] rest = new Vector3[0];

        static float Each => (GrandBanquetHallBuilder.StackTop - GrandBanquetHallBuilder.TrayY) / (Plates + .06f);

        // Cuts `stack` (a renderer of the model hung on `host`, the waiter's root) into its plates and hides it.
        public static void Split(Transform host, MeshRenderer stack)
        {
            if (!stack.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return;
            var mesh = filter.sharedMesh;
            var src = stack.transform;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            bool hasNormals = normals.Length == vertices.Length, hasUvs = uvs.Length == vertices.Length;
            // the mesh in the waiter root's frame: y up from the floor, the stack's axis at x = z = 0
            var toHost = host.worldToLocalMatrix * src.localToWorldMatrix;
            var points = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) points[i] = toHost.MultiplyPoint3x4(vertices[i]);
            float each = Each, wide = GrandBanquetHallBuilder.StackR * .85f;

            var holder = new GameObject("BH plate stack (model)").transform;   // "(model)": never covered
            holder.SetParent(host, false);
            var rattle = holder.gameObject.AddComponent<PlateStackRattle>();
            rattle.plates = new Transform[Plates];
            rattle.rest = new Vector3[Plates];

            int subs = mesh.subMeshCount;
            var triangles = new int[subs][];
            for (int s = 0; s < subs; s++) triangles[s] = mesh.GetTriangles(s);
            for (int p = 0; p < Plates; p++)
            {
                var foot = new Vector3(0f, GrandBanquetHallBuilder.TrayY + each * p, 0f);
                var map = new Dictionary<int, int>();
                var pv = new List<Vector3>();
                var pn = new List<Vector3>();
                var pu = new List<Vector2>();
                var pt = new List<int>[subs];
                for (int s = 0; s < subs; s++)
                {
                    pt[s] = new List<int>();
                    var tri = triangles[s];
                    for (int t = 0; t + 2 < tri.Length; t += 3)
                    {
                        var c = (points[tri[t]] + points[tri[t + 1]] + points[tri[t + 2]]) / 3f;
                        if (PlateOf(c, each, wide) != p) continue;
                        for (int j = 0; j < 3; j++)
                        {
                            int v = tri[t + j];
                            if (!map.TryGetValue(v, out int n))
                            {
                                map[v] = n = pv.Count;
                                pv.Add(points[v] - foot);
                                if (hasNormals) pn.Add(toHost.MultiplyVector(normals[v]).normalized);
                                if (hasUvs) pu.Add(uvs[v]);
                            }
                            pt[s].Add(n);
                        }
                    }
                }
                var plate = new GameObject("BH plate " + (p + 1) + " of the stack");
                plate.layer = stack.gameObject.layer;
                plate.transform.SetParent(holder, false);
                plate.transform.localPosition = foot;
                var m = new Mesh { name = "BH plate of the stack" };
                m.SetVertices(pv);
                if (hasNormals) m.SetNormals(pn);
                if (hasUvs) m.SetUVs(0, pu);
                m.subMeshCount = subs;
                for (int s = 0; s < subs; s++) m.SetTriangles(pt[s], s);
                // room for the rattle, so a plate at the edge of the view is not culled early
                var bounds = m.bounds;
                bounds.Expand(.2f);
                m.bounds = bounds;
                plate.AddComponent<MeshFilter>().sharedMesh = m;
                var r = plate.AddComponent<MeshRenderer>();
                r.sharedMaterials = stack.sharedMaterials;
                r.shadowCastingMode = stack.shadowCastingMode;
                r.receiveShadows = stack.receiveShadows;
                rattle.plates[p] = plate.transform;
                rattle.rest[p] = foot;
            }
            stack.enabled = false;
        }

        // Which plate (0 the lowest) a triangle whose middle is at `c` (the waiter root's frame) belongs to. A plate's
        // gold lip stands 6 % into the height of the plate over it: out at the rim there, it is the lower plate's.
        static int PlateOf(Vector3 c, float each, float wide)
        {
            float h = (c.y - GrandBanquetHallBuilder.TrayY) / each;
            int p = Mathf.FloorToInt(h);
            if (p > 0 && h - p < .08f && c.x * c.x + c.z * c.z > wide * wide) p--;
            return Mathf.Clamp(p, 0, Plates - 1);
        }

        static float Hash(int n)
        {
            float s = Mathf.Sin(n * 12.9898f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        // Plate p's place and turn (the holder's frame, from its rest) once the waiter has walked `walked` metres.
        public static void Pose(int p, float walked, out Vector3 offset, out Quaternion turn)
        {
            float u = walked / Stride;
            int step = Mathf.FloorToInt(u);
            float f = u - step, up = Mathf.Pow(p / (float)(Plates - 1), 1.6f);
            // the stack's lean: across the walk once every two steps, along it once a step
            float across = Mathf.Sin(Mathf.PI * u), along = Mathf.Sin(2f * Mathf.PI * u + 1.3f);
            offset = new Vector3(across * Lean, 0f, along * Nod) * up;
            // the plate's own jump at this step: from nothing, its own way, died away before the next step
            float angle = 2f * Mathf.PI * Hash(p * 31 + step * 17 + 5);
            float ring = Mathf.Exp(-5f * f) * Mathf.Sin(2f * Mathf.PI * (2.5f + 1.5f * Hash(p * 7 + 3)) * f);
            var way = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            offset += way * (Slide * ring) + Vector3.up * (Lift * Mathf.Abs(ring));
            turn = Quaternion.AngleAxis(Tilt * ring, new Vector3(-way.z, 0f, way.x))
                   * Quaternion.Euler(along * 1.1f * up, 0f, -across * 1.8f * up);
        }

        void LateUpdate()
        {
            var host = transform.parent;
            if (host == null) return;
            float walked = host.localPosition.z;
            for (int p = 0; p < plates.Length; p++)
            {
                if (plates[p] == null) continue;
                Pose(p, walked, out var offset, out var turn);
                plates[p].localPosition = rest[p] + offset;
                plates[p].localRotation = turn;
            }
        }
    }
}
