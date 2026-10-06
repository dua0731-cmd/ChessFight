using System;
using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    // The graybox vocabulary the modules are written in: boxes given by their min and
    // max corners in module-local metres, so a line of the design doc's tables
    // ("x -15~15, z 0~14, y 0") becomes one call. Floors are 1 m thick boxes whose
    // top is the table's y (design doc §6). Every collider is a BoxCollider except the
    // round parts.
    public sealed class CourseBuilder
    {
        // How obstacle prefabs are placed. The editor swaps in PrefabUtility.InstantiatePrefab so
        // a baked module keeps the link to the imported obstacle prefab.
        public static Func<GameObject, Transform, GameObject> Spawn = (prefab, parent) => Object.Instantiate(prefab, parent);

        public readonly Course01Kit kit;
        public Transform parent;

        public CourseBuilder(Course01Kit kit, Transform parent)
        {
            this.kit = kit;
            this.parent = parent;
        }

        public Transform Group(string name, Transform under = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(under != null ? under : parent, false);
            return go.transform;
        }

        // A group made current for the calls inside `body`.
        public Transform In(string name, Action body)
        {
            var group = Group(name);
            var before = parent;
            parent = group;
            try { body(); }
            finally { parent = before; }
            return group;
        }

        public GameObject Box(string name, Vector3 min, Vector3 max, Material material, bool noClimb = false, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * .5f;
            go.transform.localScale = Abs(max - min);
            Finish(go, material, noClimb, solid);
            return go;
        }

        // Floor slab, 1 m thick, top at `top`.
        public GameObject Floor(string name, float x0, float x1, float z0, float z1, float top = 0f) =>
            Box(name, new Vector3(x0, top - 1f, z0), new Vector3(x1, top, z1), kit.floorChecker);

        // A wall or a block: climbable stone, or marble that cannot be climbed.
        public GameObject Wall(string name, float x0, float x1, float y0, float y1, float z0, float z1, bool climbable) =>
            Box(name, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), climbable ? kit.wallClimbable : kit.wallNoClimb, !climbable);

        // A box tilted about x whose top runs from (z0, y0) to (z1, y1). The slab reaches `below` under
        // that line (solid, so nothing walks under a ramp) and `above` over it (a railing). Slopes in
        // the course stay at 19.5 degrees or less (design doc §2).
        public GameObject Slope(string name, float x0, float x1, float z0, float z1, float y0, float y1,
                                float below, float above, Material material, bool noClimb = false)
        {
            float dz = z1 - z0, dy = y1 - y0;
            float length = Mathf.Sqrt(dz * dz + dy * dy);
            float angle = Mathf.Atan2(dy, dz) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(-angle, 0f, 0f);
            var normal = rotation * Vector3.up;
            var line = new Vector3((x0 + x1) * .5f, (y0 + y1) * .5f, (z0 + z1) * .5f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localRotation = rotation;
            go.transform.localPosition = line + normal * ((above - below) * .5f);
            go.transform.localScale = new Vector3(Mathf.Abs(x1 - x0), above + below, length);
            Finish(go, material, noClimb, true);
            return go;
        }

        public GameObject Ramp(string name, float x0, float x1, float z0, float z1, float y0, float y1) =>
            Slope(name, x0, x1, z0, z1, y0, y1, 3.5f, 0f, kit.floorChecker);

        // A 1.2 m (or `height`) unclimbable railing on a straight edge.
        public GameObject Rail(string name, float x0, float x1, float z0, float z1, float floor, float height = 1.2f) =>
            Wall(name, x0, x1, floor, floor + height, z0, z1, false);

        // An upright cylinder standing on `bottom`.
        public GameObject Cylinder(string name, Vector3 baseCenter, float diameter, float height, Material material,
                                   bool solid = true, bool noClimb = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = baseCenter + Vector3.up * (height * .5f);
            go.transform.localScale = new Vector3(diameter, height * .5f, diameter);
            Finish(go, material, noClimb, solid);
            return go;
        }

        public GameObject Sphere(string name, Vector3 center, float diameter, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = Vector3.one * diameter;
            Finish(go, material, false, solid);
            return go;
        }

        // An imported obstacle prefab at a module-local point, turned `yaw` degrees about y.
        public GameObject Obstacle(GameObject prefab, string name, Vector3 at, float yaw = 0f)
        {
            if (prefab == null)
            {
                Debug.LogError("[PawnRush] Course01Kit is missing the obstacle for " + name);
                return Group(name + " (missing obstacle)").gameObject;
            }
            var go = Spawn(prefab, parent);
            go.name = name;
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        // An invisible trigger box carrying T.
        public T Trigger<T>(string name, Vector3 min, Vector3 max) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * .5f;
            var box = go.AddComponent<BoxCollider>();
            box.size = Abs(max - min);
            box.isTrigger = true;
            return go.AddComponent<T>();
        }

        // A checkpoint across the course at local z: the existing Checkpoint (order = number) plus its
        // six respawn spots, 2 m apart across x -5..5 around `centerX`.
        public CourseCheckpoint Checkpoint(int number, float z, float centerX = 0f, float width = 30f, int team = Teams.None, float floor = 0f)
        {
            var cp = Trigger<CourseCheckpoint>("CP" + number, new Vector3(centerX - width * .5f, floor, z - 1f),
                                               new Vector3(centerX + width * .5f, floor + 3f, z + 1f));
            cp.GetComponent<Checkpoint>().Configure(number);
            cp.Configure(number, team);
            return cp;
        }

        // A kill volume under a whole module: 8 m under the entry floor, wider than anything above.
        public KillVolume KillFloor(float length, float halfWidth = 60f) =>
            Trigger<KillVolume>("Kill volume", new Vector3(-halfWidth, -9f, -1f), new Vector3(halfWidth, -7f, length + 1f));

        static void Finish(GameObject go, Material material, bool noClimb, bool solid)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            // Immediate even in Play: the module is still inactive while it is built, and a collider
            // left for the end of the frame would be live for one step after it is switched on.
            if (!solid) Object.DestroyImmediate(collider);
            else if (collider is CapsuleCollider capsule)
            {
                // Cylinders get a box-free round collider: a convex mesh of the cylinder.
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(capsule);
                var round = go.AddComponent<MeshCollider>();
                round.sharedMesh = mesh;
                round.convex = true;
            }
            if (noClimb && solid) go.AddComponent<NoClimbSurface>();
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
