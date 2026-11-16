using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R684 (승규 11-16 "콜라이더 적용 안된 곳 있으면 해"): the Royal Carriage Yard's colliders against what it draws. In
    // Play Mode the map (at the origin) and the section (x + 200) are built, every drawn mesh (FBX parts, the code's
    // pictures, the horses baked in their pose of the moment, LOD0 only) gets a hidden copy as a MeshCollider on layer 31
    // (colliding with nothing, only for the rays here), and rays compare what is drawn with what collides, 1.5, 9 and 15 s
    // after the build (the moving obstacles in three poses). Into Logs/YardColliderAudit, per scan:
    //   surfaces.csv  every 10 cm cell: each place a pawn could stand on a collider (up-facing, 1 m clear above, not
    //                 inside another collider) and the drawn surface found near it (from 1 m above to 1.5 m below), and
    //                 drawn up-facing surfaces with no collider within 10 cm under them (x, z, kind S/D, y, other y,
    //                 collider id, drawn id)
    //   side.csv      from every 30 cm standing place at 0.35 / 1.0 / 1.6 m, eight rays of 2.5 m: those where the drawn
    //                 and the colliding hit differ by more than 10 cm
    //   names.csv     ids to object paths ("C:" a collider, "D:" a drawn mesh); renderers.csv, colliders.csv every one
    // Tools/ColliderAudit/reach.py keeps what a pawn can reach (a flood from the start court) and groups the differences
    // by object. Batch, on a copy of the project (Tools/ColliderAudit/Run-CarriageYardColliderAudit.ps1), never in the
    // Editor the user has open: it plays the map test's prefabs in an empty scene and quits.
    [InitializeOnLoad]
    public static class CarriageYardColliderAudit
    {
        const string ActiveKey = "ChessFight.R684.ColliderAudit";
        const string Folder = "Logs/YardColliderAudit";
        const string MapPath = "Assets/Prefabs/PawnRushMaps/RoyalCarriageYard.prefab";
        const string SectionPath = "Assets/Prefabs/PawnRushSections/Early_1-2_RoyalCarriageYard.prefab";
        const int ProxyLayer = 31;
        const float SectionShift = 200f, Step = .1f, Tol = .1f;
        static readonly float[] Shots = { 1.5f, 9f, 15f };
        static int shot;
        static double requested, idle;
        static GameObject map, section;
        static float savedStep = .02f;
        static readonly StringBuilder log = new StringBuilder();

        static CarriageYardColliderAudit()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
                {
                    shot = 0;
                    EditorApplication.update -= Tick;
                    EditorApplication.update += Tick;
                }
                if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ActiveKey, false))
                {
                    SessionState.SetBool(ActiveKey, false);
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
            };
        }

        public static void RunBatch()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SectionPath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            requested = idle = EditorApplication.timeSinceStartup;
            EditorApplication.update += WaitIdle;
        }

        static void WaitIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idle = now; return; }
            if (now - idle < 2f && now - requested < 120f) return;
            EditorApplication.update -= WaitIdle;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (map == null)
                {
                    // The game's physics rate (PhysicsProfile, 120 Hz): the empty scene's 50 Hz drew every moving obstacle
                    // a 0.02 s step behind its collider (19 cm on a charging carriage), not the game's 8 mm a m/s.
                    savedStep = Time.fixedDeltaTime;
                    Time.fixedDeltaTime = 1f / 120f;
                    map = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MapPath), Vector3.zero, Quaternion.identity);
                    map.name = "Map";
                    section = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SectionPath), new Vector3(SectionShift, 0f, 0f), Quaternion.identity);
                    section.name = "Section";
                    for (int l = 0; l < 32; l++) Physics.IgnoreLayerCollision(ProxyLayer, l, true);
                    return;
                }
                if (shot < Shots.Length && Time.timeSinceLevelLoad >= Shots[shot])
                {
                    string t = "_t" + Shots[shot].ToString("0.#", CultureInfo.InvariantCulture);
                    Scan(map.transform, 0f, "map" + t);
                    if (shot == 0) Scan(section.transform, SectionShift, "section" + t);
                    shot++;
                    if (shot == Shots.Length)
                    {
                        File.WriteAllText(Path.Combine(Folder, "log.txt"), log.ToString());
                        EditorApplication.update -= Tick;
                        Time.fixedDeltaTime = savedStep;
                        EditorApplication.isPlaying = false;
                    }
                }
            }
            catch (Exception e)
            {
                Log("FAIL " + e);
                File.WriteAllText(Path.Combine(Folder, "log.txt"), log.ToString());
                EditorApplication.update -= Tick;
                Time.fixedDeltaTime = savedStep;
                EditorApplication.isPlaying = false;
            }
        }

        static void Log(string s) { log.AppendLine(s); Debug.Log("[ColliderAudit] " + s); }

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (; t != null && t != root; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => F(v.x) + " " + F(v.y) + " " + F(v.z);

        static void Scan(Transform root, float shift, string label)
        {
            var t0 = DateTime.Now;
            var proxies = new List<GameObject>();
            var ids = new Dictionary<Collider, int>();
            var names = new List<string> { "(none)" };
            int Id(Collider c)
            {
                if (c == null) return 0;
                if (!ids.TryGetValue(c, out int i))
                {
                    i = names.Count;
                    names.Add((c.gameObject.layer == ProxyLayer ? "D:" : "C:") + (proxyName.TryGetValue(c, out var s) ? s : PathOf(c.transform, root)));
                    ids[c] = i;
                }
                return i;
            }
            proxyName.Clear();
            var lod1 = new HashSet<Renderer>();
            foreach (var g in root.GetComponentsInChildren<LODGroup>())
            {
                var lods = g.GetLODs();
                for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r != null) lod1.Add(r);
            }
            var rlist = new StringBuilder("path,kind,enabled,min_x,min_y,min_z,max_x,max_y,max_z,triangles\n");
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (r.GetComponent<TextMesh>() != null) continue;
                bool on = r.enabled && r.gameObject.activeInHierarchy && !lod1.Contains(r) && r.shadowCastingMode != ShadowCastingMode.ShadowsOnly;
                Mesh mesh;
                var go = new GameObject("proxy") { layer = ProxyLayer };
                if (r is SkinnedMeshRenderer sk)
                {
                    if (sk.sharedMesh == null) { Object.Destroy(go); continue; }
                    mesh = new Mesh();
                    // Unscaled: the horse's renderer carries the FBX's 100x, its bones already put it in metres.
                    sk.BakeMesh(mesh, false);
                    go.transform.SetPositionAndRotation(sk.transform.position, sk.transform.rotation);
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) { Object.Destroy(go); continue; }
                    mesh = mf.sharedMesh;
                    go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                    go.transform.localScale = r.transform.lossyScale;
                }
                var path = PathOf(r.transform, root);
                var b = r.bounds;
                rlist.Append('"').Append(path).Append("\",").Append(r is SkinnedMeshRenderer ? "skinned" : "mesh").Append(',').Append(on ? 1 : 0).Append(',')
                     .Append(F(b.min.x - shift)).Append(',').Append(F(b.min.y)).Append(',').Append(F(b.min.z)).Append(',')
                     .Append(F(b.max.x - shift)).Append(',').Append(F(b.max.y)).Append(',').Append(F(b.max.z)).Append(',')
                     .Append(mesh.isReadable ? (mesh.triangles.Length / 3).ToString() : "?").Append('\n');
                if (!on || !mesh.isReadable) { if (on) Log("not readable: " + path); Object.Destroy(go); continue; }
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                proxyName[mc] = path;
                proxies.Add(go);
            }
            File.WriteAllText(Path.Combine(Folder, label + "_renderers.csv"), rlist.ToString());
            var clist = new StringBuilder("path,type,trigger,enabled,layer,body,renderer_on,min_x,min_y,min_z,max_x,max_y,max_z,size\n");
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (proxyName.ContainsKey(c)) continue;
                var b = c.bounds;
                var r = c.GetComponent<Renderer>();
                string size = c is BoxCollider bc ? V(Vector3.Scale(bc.size, c.transform.lossyScale)) : c is SphereCollider sc ? F(sc.radius) : c is CapsuleCollider cc ? F(cc.radius) + "x" + F(cc.height) : c is MeshCollider m ? (m.convex ? "convex" : "mesh") + ":" + (m.sharedMesh != null ? m.sharedMesh.name : "-") : "";
                clist.Append('"').Append(PathOf(c.transform, root)).Append("\",").Append(c.GetType().Name).Append(',').Append(c.isTrigger ? 1 : 0).Append(',')
                     .Append(c.enabled && c.gameObject.activeInHierarchy ? 1 : 0).Append(',').Append(c.gameObject.layer).Append(',')
                     .Append(c.attachedRigidbody != null ? (c.attachedRigidbody.isKinematic ? "kin" : "dyn") : "static").Append(',')
                     .Append(r != null && r.enabled ? 1 : 0).Append(',')
                     .Append(F(b.min.x - shift)).Append(',').Append(F(b.min.y)).Append(',').Append(F(b.min.z)).Append(',')
                     .Append(F(b.max.x - shift)).Append(',').Append(F(b.max.y)).Append(',').Append(F(b.max.z)).Append(',').Append(size).Append('\n');
            }
            File.WriteAllText(Path.Combine(Folder, label + "_colliders.csv"), clist.ToString());
            Physics.SyncTransforms();

            var all = new Bounds();
            bool any = false;
            foreach (var p in proxies)
            {
                var b = p.GetComponent<Collider>().bounds;
                if (!any) { all = b; any = true; } else all.Encapsulate(b);
            }
            float x0 = Mathf.Max(all.min.x, shift - 40f), x1 = Mathf.Min(all.max.x, shift + 40f);
            float z0 = Mathf.Max(all.min.z, -25f), z1 = Mathf.Min(all.max.z, 196f);
            int vis = 1 << ProxyLayer, phys = ~vis;
            const QueryTriggerInteraction ignore = QueryTriggerInteraction.Ignore;
            float top = Mathf.Min(all.max.y, 40f) + 2f;

            var surf = new StringBuilder("x,z,kind,y,oy,cid,did\n");
            var stands = new List<Vector3>();
            int nx = Mathf.CeilToInt((x1 - x0) / Step), nz = Mathf.CeilToInt((z1 - z0) / Step);
            var hits = new RaycastHit[64];
            for (int ix = 0; ix <= nx; ix++)
                for (int iz = 0; iz <= nz; iz++)
                {
                    float x = x0 + ix * Step, z = z0 + iz * Step;
                    var o = new Vector3(x, top, z);
                    string xs = F(x - shift), zs = F(z);
                    // Colliding places to stand.
                    int n = Physics.RaycastNonAlloc(o, Vector3.down, hits, 200f, phys, ignore);
                    for (int i = 0; i < n; i++)
                    {
                        var h = hits[i];
                        if (h.normal.y < .7f) continue;
                        var up = h.point + Vector3.up * .02f;
                        if (Physics.Raycast(up, Vector3.up, 1f, phys, ignore)) continue;
                        // Inside another collider (a ray from inside a box does not meet it): not a place to stand.
                        if (Physics.CheckSphere(h.point + Vector3.up * .3f, .1f, phys, ignore)) continue;
                        // The drawn surface near it: down from 1 m above (or less, under something drawn) to 1.5 m below.
                        float from = 1f;
                        if (Physics.Raycast(up, Vector3.up, out var roof, 1f, vis, ignore)) from = Mathf.Max(.03f, roof.distance - .01f);
                        bool hv = Physics.Raycast(h.point + Vector3.up * from, Vector3.down, out var v, from + 1.5f, vis, ignore);
                        surf.Append(xs).Append(',').Append(zs).Append(",S,").Append(F(h.point.y)).Append(',').Append(hv ? F(v.point.y) : "nan").Append(',')
                            .Append(Id(h.collider)).Append(',').Append(hv ? Id(v.collider) : 0).Append('\n');
                        if (ix % 3 == 0 && iz % 3 == 0) stands.Add(h.point);
                    }
                    // Drawn up-facing surfaces with no collider within 10 cm under them.
                    // (A drawn mesh is one collider and a ray meets it once: cast again from under each hit.)
                    var from2 = o;
                    for (int i = 0; i < 24; i++)
                    {
                        if (!Physics.Raycast(from2, Vector3.down, out var h, 200f, vis, ignore)) break;
                        from2 = h.point + Vector3.down * .01f;
                        if (h.normal.y < .7f) continue;
                        var up = h.point + Vector3.up * .02f;
                        bool below = Physics.Raycast(up, Vector3.down, out var p, 200f, phys, ignore);
                        if (below && h.point.y - p.point.y <= Tol) continue;
                        // Inside a collider: covered by it.
                        if (Physics.CheckSphere(h.point + Vector3.down * .05f, .04f, phys, ignore)) continue;
                        // Something colliding in the 10 cm above it is the S row's business.
                        if (Physics.Raycast(h.point + Vector3.down * .01f, Vector3.up, Tol + .01f, phys, ignore)) continue;
                        surf.Append(xs).Append(',').Append(zs).Append(",D,").Append(F(h.point.y)).Append(',').Append(below ? F(p.point.y) : "nan").Append(',')
                            .Append(below ? Id(p.collider) : 0).Append(',').Append(Id(h.collider)).Append('\n');
                    }
                }
            File.WriteAllText(Path.Combine(Folder, label + "_surfaces.csv"), surf.ToString());

            var side = new StringBuilder("sx,sy,sz,h,dir,dv,dp,cid,did\n");
            var dirs = Enumerable.Range(0, 8).Select(i => Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward).ToArray();
            const float reach = 2.5f;
            int samples = 0;
            foreach (var s in stands)
                foreach (float hgt in new[] { .35f, 1f, 1.6f })
                {
                    var o = s + Vector3.up * hgt;
                    if (Physics.CheckSphere(o, .06f, phys, ignore) || Physics.CheckSphere(o, .06f, vis, ignore)) continue;
                    samples++;
                    for (int k = 0; k < 8; k++)
                    {
                        bool hv = Physics.Raycast(o, dirs[k], out var v, reach, vis, ignore);
                        bool hp = Physics.Raycast(o, dirs[k], out var p, reach, phys, ignore);
                        float dv = hv ? v.distance : reach, dp = hp ? p.distance : reach;
                        if (Mathf.Abs(dv - dp) <= Tol) continue;
                        side.Append(F(s.x - shift)).Append(',').Append(F(s.y)).Append(',').Append(F(s.z)).Append(',').Append(F(hgt)).Append(',').Append(k * 45).Append(',')
                            .Append(F(dv)).Append(',').Append(F(dp)).Append(',').Append(hp ? Id(p.collider) : 0).Append(',').Append(hv ? Id(v.collider) : 0).Append('\n');
                    }
                }
            File.WriteAllText(Path.Combine(Folder, label + "_side.csv"), side.ToString());
            var nm = new StringBuilder("id,name\n");
            for (int i = 0; i < names.Count; i++) nm.Append(i).Append(",\"").Append(names[i]).Append("\"\n");
            File.WriteAllText(Path.Combine(Folder, label + "_names.csv"), nm.ToString());
            foreach (var p in proxies) Object.Destroy(p);
            Log(label + ": " + proxies.Count + " drawn meshes, grid " + nx + "x" + nz + ", " + stands.Count + " standing places, " + samples + " side samples, "
                + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        }

        static readonly Dictionary<Collider, string> proxyName = new Dictionary<Collider, string>();
    }
}
