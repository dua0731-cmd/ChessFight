using System;
using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    // The graybox vocabulary the course is written in: boxes given by their min and max
    // corners in metres (the course root sits at the world origin, so these are the design
    // doc's world coordinates), and a line of its tables ("x -15~15, z 0~18, y 0") becomes
    // one call. Floors are 1 m thick boxes whose top is the table's y. Every collider is a
    // BoxCollider except the round parts. Walkable tops are marked CourseFloor (the validator
    // measures a path's width on them).
    public sealed class CourseBuilder
    {
        // How obstacle prefabs are placed. The editor swaps in PrefabUtility.InstantiatePrefab so
        // the course built in the scene keeps the link to the imported obstacle prefabs.
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
        public GameObject Floor(string name, float x0, float x1, float z0, float z1, float top = 0f)
        {
            var go = Box(name, new Vector3(x0, top - 1f, z0), new Vector3(x1, top, z1), kit.floorChecker);
            go.AddComponent<CourseFloor>();
            return go;
        }

        // A wall or a block: climbable stone, or marble that cannot be climbed. `floor`: its top is walked on.
        public GameObject Wall(string name, float x0, float x1, float y0, float y1, float z0, float z1, bool climbable, bool floor = false)
        {
            var go = Box(name, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), climbable ? kit.wallClimbable : kit.wallNoClimb, !climbable);
            if (floor) go.AddComponent<CourseFloor>();
            return go;
        }

        // A tilted box whose top centre line runs from `from` to `to`, `width` across. The slab reaches
        // `below` under that line and `above` over it (a railing). Slopes in the course stay at 19.5
        // degrees or less (design doc §3).
        public GameObject SlopeBetween(string name, Vector3 from, Vector3 to, float width, float below, float above,
                                       Material material, bool noClimb = false)
        {
            var d = to - from;
            var rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            var normal = rotation * Vector3.up;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localRotation = rotation;
            go.transform.localPosition = (from + to) * .5f + normal * ((above - below) * .5f);
            go.transform.localScale = new Vector3(Mathf.Abs(width), above + below, d.magnitude);
            Finish(go, material, noClimb, true);
            return go;
        }

        // A slope along z (x0..x1 wide) from (z0, y0) to (z1, y1); either direction.
        public GameObject Slope(string name, float x0, float x1, float z0, float z1, float y0, float y1,
                                float below, float above, Material material, bool noClimb = false)
        {
            float x = (x0 + x1) * .5f;
            return SlopeBetween(name, new Vector3(x, y0, z0), new Vector3(x, y1, z1), x1 - x0, below, above, material, noClimb);
        }

        // A slope along x (z0..z1 wide) from (x0, y0) to (x1, y1); either direction.
        public GameObject SlopeX(string name, float z0, float z1, float x0, float x1, float y0, float y1,
                                 float below, float above, Material material, bool noClimb = false)
        {
            float z = (z0 + z1) * .5f;
            return SlopeBetween(name, new Vector3(x0, y0, z), new Vector3(x1, y1, z), z1 - z0, below, above, material, noClimb);
        }

        // A walkable ramp along z, `below` thick under its surface.
        public GameObject Ramp(string name, float x0, float x1, float z0, float z1, float y0, float y1, float below = 3.5f)
        {
            var go = Slope(name, x0, x1, z0, z1, y0, y1, below, 0f, kit.floorChecker);
            go.AddComponent<CourseFloor>().sloped = true;
            return go;
        }

        // A walkable ramp along x.
        public GameObject RampX(string name, float z0, float z1, float x0, float x1, float y0, float y1, float below = 1f)
        {
            var go = SlopeX(name, z0, z1, x0, x1, y0, y1, below, 0f, kit.floorChecker);
            go.AddComponent<CourseFloor>().sloped = true;
            return go;
        }

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

        // A checkpoint at a ground point, facing `yaw` (the way to go; a respawn faces it too): the
        // existing Checkpoint (order = number) plus six respawn spots 2 m apart across its width.
        public CourseCheckpoint Checkpoint(int number, Vector3 at, float yaw, float width = 10f, int team = Teams.None)
        {
            var go = new GameObject("CP" + number);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at + Vector3.up * 1.5f;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, 3f, 2f);
            box.isTrigger = true;
            var cp = go.AddComponent<CourseCheckpoint>();
            cp.GetComponent<Checkpoint>().Configure(number);
            cp.Configure(number, team, Mathf.Min(2f, (width - 1f) / 5f));
            return cp;
        }

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
