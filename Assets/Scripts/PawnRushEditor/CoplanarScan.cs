using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ChessFight.Gameplay;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R904 (user 11-28: "대연회장 깜빡거리는 부분 외곽에서 보이고 내부에서도 몇개 있어 고쳐주고 왕의 서재도 깜빡거리는 부분
    // 지워주고"): finds the faces that flicker (z-fighting, PITFALLS #39) in the grand banquet hall and the royal library
    // as drawn in Play - their Blender models hang over the graybox only in Play. One Play session: each section prefab
    // alone, its entry on the origin, a few frames for the dressing, then every drawn triangle (enabled MeshRenderers, not
    // transparent; of a LODGroup its first level, then its last - what is drawn from far away) compared with the others
    // (CoplanarFaces: same way, within 3 mm of one plane, sharing more than 1 cm2). Skinned meshes are not read.
    //
    // Most faces inside one FBX are lifted on import (CoplanarLiftImporter). With -coplanarBake the scan runs with
    // CoplanarNudges off and, round by round (each level of detail), picks of every pair the face to lift - a hung model's
    // before the hall's shell and before a graybox piece, else the smaller part's; a face lifted before stays on top - lifts
    // it Lift in front of the other in its renderer's own copy of the mesh, and looks again; the faces go to
    // Assets/Resources/<folder>/coplanar_nudges.txt (Gameplay CoplanarNudges) and everything is scanned once more.
    // The dais is narrowed by hand (GrandBanquetHallDressing.DaisOffWalls) and never lifted here.
    // Results: Logs/CoplanarScan/<prefab>.txt (what is left, by kind). Tools/Run-CoplanarScan.ps1 [-Bake] on the test copy.
    [InitializeOnLoad]
    public static class CoplanarScan
    {
        const string Folder = "Logs/CoplanarScan";
        const string ActiveKey = "ChessFight.R904.Coplanar.Play", ExitKey = "ChessFight.R904.Coplanar.Exit";
        const float Tolerance = .003f, Lift = .005f, MinOverlap = 1e-4f, MinArea = 2e-5f;
        const int SettleFrames = 30, Listed = 60;

        sealed class Target
        {
            public string prefab, folder, dressing;
            public string[] shell;   // hung models that are the room itself: the others come forward of them
            public string[] fixedModels = Array.Empty<string>();   // hung models placed by hand (never nudged)
        }

        static readonly Target[] Targets =
        {
            new Target
            {
                prefab = "Assets/Prefabs/PawnRushSections/Mid_2-4_GrandBanquetHall.prefab", folder = "BanquetHall",
                dressing = "GrandBanquetHallDressing",
                fixedModels = new[] { "bh_dais" },   // GrandBanquetHallDressing.DaisOffWalls
                shell = new[] { "bh_hall_walls", "bh_hall_pilasters", "bh_hall_ceiling", "bh_hall_floor", "bh_hall_entry", "bh_hall_fireplaces", "bh_dance_floor" },
            },
            new Target
            {
                prefab = "Assets/Prefabs/PawnRushSections/Late_3-1_RoyalLibrary.prefab", folder = "RoyalLibrary",
                dressing = "RoyalLibraryDressing",
                shell = new[] { "RlRoomWalls", "RlGalleryWalls", "RlGalleryCeiling" },
            },
        };

        static int targetIndex, frames;
        static readonly HashSet<string> roundHeld = new HashSet<string>(), roundMoving = new HashSet<string>();
        static GameObject holder;
        static bool done;

        static CoplanarScan()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += Exit;
        }

        static bool Bake => Array.IndexOf(Environment.GetCommandLineArgs(), "-coplanarBake") >= 0 || SessionState.GetBool("ChessFight.R904.Coplanar.Bake", false);

        [MenuItem("ChessFight/Pawn Rush/Validate/Coplanar faces (banquet hall + library)", false, 230)]
        public static void RunMenu() { SessionState.SetBool("ChessFight.R904.Coplanar.Bake", false); RunBatch(); }

        [MenuItem("ChessFight/Pawn Rush/Validate/Coplanar faces - bake nudges", false, 231)]
        public static void BakeMenu() { SessionState.SetBool("ChessFight.R904.Coplanar.Bake", true); RunBatch(); }

        public static void RunBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.Delete(Path.Combine(Folder, "error.txt"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                targetIndex = 0;
                frames = -1;
                done = false;
                CoplanarNudges.Enabled = !Bake;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                CoplanarNudges.Enabled = true;
                if (SessionState.GetBool(ExitKey, false)) Exit();
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || done) return;
            try
            {
                if (frames < 0)
                {
                    if (targetIndex >= Targets.Length) { Stop(); return; }
                    Place(Targets[targetIndex].prefab);
                    frames = 0;
                    return;
                }
                if (++frames < SettleFrames) return;
                Run(Targets[targetIndex]);
                Object.DestroyImmediate(holder);
                targetIndex++;
                frames = -1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.AppendAllText(Path.Combine(Folder, "error.txt"), e + "\n");
                Stop();
            }
        }

        static void Stop()
        {
            done = true;
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, true);
            EditorApplication.isPlaying = false;
        }

        static void Exit()
        {
            SessionState.SetBool(ExitKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Place(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            holder = new GameObject("Coplanar scan");
            holder.SetActive(false);
            var go = Object.Instantiate(prefab, holder.transform);
            go.name = prefab.name;
            var section = go.GetComponent<PawnRushSection>();
            if (section != null)
            {
                section.PlaceEntryOn(new Pose(Vector3.zero, Quaternion.identity));
                section.Configure(2, 900, 6, ObstacleClock.Now);
            }
            holder.SetActive(true);
        }

        // ------------------------------------------------------------------ one section

        sealed class Owner
        {
            public Renderer renderer;
            public string path, material, model;   // path under the dressing (null: outside it); model: the hung FBX (null: none)
            public bool shell, fixedHere;
            public int sub;
            public float area;
        }

        sealed class Hit
        {
            public float area;
            public int count;
            public Vector3 at;
            public float best;
        }

        static void Run(Target target)
        {
            var section = holder.transform.GetChild(0);
            Transform dressing = null;
            foreach (var c in section.GetComponentsInChildren<MonoBehaviour>(true))
                if (c != null && c.GetType().Name == target.dressing) { dressing = c.transform; break; }
            if (dressing == null) throw new InvalidOperationException(target.prefab + ": no " + target.dressing);

            var report = new StringBuilder();
            string name = Path.GetFileNameWithoutExtension(target.prefab);
            report.AppendLine("R904 coplanar faces in " + name + " (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                              "), section frame (entry on the origin). Two faces facing the same way within " + Tolerance * 1000 +
                              " mm of one plane, sharing more than " + MinOverlap * 1e4f + " cm2; area = summed overlap.");
            if (Bake)
            {
                // rounds: a face lifted can meet another; each round lifts, applies and looks again
                lifts.Clear();
                liftedEver.Clear();
                int rounds = 0, moved = 0;
                for (; rounds < 12; rounds++)
                {
                    var step = new Dictionary<string, FaceMove>();
                    roundHeld.Clear();
                    roundMoving.Clear();
                    int levels = LodLevels(section);
                    for (int level = 0; level < levels; level++) Collect(section, dressing, target, level, step, null);
                    if (step.Count == 0) break;
                    var lines = new StringBuilder();
                    foreach (var kv in step)
                    {
                        var m = kv.Value;
                        lines.Append(Line(m));
                        if (lifts.TryGetValue(kv.Key, out var had)) had.move += m.move;
                        else lifts[kv.Key] = new FaceMove { path = m.path, sub = m.sub, k = m.k, move = m.move };
                        liftedEver.Add(kv.Key);
                    }
                    moved += CoplanarNudges.ApplyText(dressing, lines.ToString());
                }
                var text = new StringBuilder();
                text.Append("# R904 CoplanarScan bake (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ", " + name +
                            "): <renderer path under " + target.dressing + ">\\t<submesh>\\t<the face's first index>\\t<x y z, the mesh's frame>. " +
                            "Written by Tools/Run-CoplanarScan.ps1 -Bake; read by Gameplay CoplanarNudges.\n");
                var keys = new List<string>(lifts.Keys);
                keys.Sort(StringComparer.Ordinal);
                var pieces = new HashSet<string>();
                foreach (var key in keys)
                {
                    pieces.Add(lifts[key].path);
                    text.Append(Line(lifts[key]));
                }
                string file = "Assets/Resources/" + target.folder + "/coplanar_nudges.txt";
                File.WriteAllText(file, text.ToString());
                report.AppendLine("Bake: " + keys.Count + " faces of " + pieces.Count + " renderers lifted, written to " + file + " (" + rounds +
                                  " rounds, " + moved + " face lifts applied for the check below).");
            }
            foreach (int level in new[] { 0, -1 })
                Collect(section, dressing, target, level, null, report);
            File.WriteAllText(Path.Combine(Folder, name + ".txt"), report.ToString());
            Debug.Log("[겹친 면 점검] " + name + " -> " + Path.Combine(Folder, name + ".txt"));
        }

        sealed class FaceMove
        {
            public string path;
            public int sub, k;
            public Vector3 move;   // the mesh's frame
        }

        static readonly Dictionary<string, FaceMove> lifts = new Dictionary<string, FaceMove>();
        static readonly HashSet<string> liftedEver = new HashSet<string>();

        static string Key(FaceMove m) => m.path + "\t" + m.sub + "\t" + m.k;
        static string Line(FaceMove m) => Key(m) + "\t" + G(m.move.x) + " " + G(m.move.y) + " " + G(m.move.z) + "\n";

        static int LodLevels(Transform section)
        {
            int most = 1;
            foreach (var g in section.GetComponentsInChildren<LODGroup>(false)) most = Mathf.Max(most, g.lodCount);
            return most;
        }

        // One pass at one level of detail (-1: each group's last): the faces to lift (bake), or the report of what is left.
        static void Collect(Transform section, Transform dressing, Target target, int level, Dictionary<string, FaceMove> step, StringBuilder report)
        {
            GC.Collect();   // millions of faces a pass: the last pass's first
            var skip = new HashSet<Renderer>();
            foreach (var g in section.GetComponentsInChildren<LODGroup>(false))
            {
                var lods = g.GetLODs();
                int keep = level < 0 ? lods.Length - 1 : Mathf.Min(level, lods.Length - 1);
                for (int i = 0; i < lods.Length; i++)
                    if (i != keep)
                        foreach (var r in lods[i].renderers) if (r != null) skip.Add(r);
            }
            var owners = new List<Owner>();
            var faces = new List<CoplanarFaces.Face>();
            int skinned = 0, see = 0;
            foreach (var r in section.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || skip.Contains(r)) continue;
                if (r is SkinnedMeshRenderer) { skinned++; continue; }
                if (!(r is MeshRenderer) || !r.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) continue;
                Read(r, mf.sharedMesh, section, dressing, target, owners, faces, ref see);
            }
            var hits = new Dictionary<long, Hit>();
            var wanted = new List<(FaceMove a, FaceMove b, float area)>();
            CoplanarFaces.Pairs(faces, Tolerance, MinOverlap, (i, j, area) =>
            {
                var A = faces[i];
                var B = faces[j];
                Owner a = owners[A.group], b = owners[B.group];
                if (step != null)
                {
                    int ra = Rank(a), rb = Rank(b);
                    if (ra == 0 && rb == 0) return;   // neither the dressing's to move
                    bool moveA = ra != rb ? ra > rb : (Mathf.Approximately(a.area, b.area) ? A.group > B.group : a.area < b.area);
                    var m = moveA ? A : B;
                    var o = moveA ? B : A;
                    Owner mo = moveA ? a : b, oo = moveA ? b : a;
                    float ahead = m.d - o.d;
                    if (ahead >= Lift) return;
                    var mine = new FaceMove { path = mo.path, sub = mo.sub, k = m.tri,
                                              move = mo.renderer.transform.InverseTransformVector(section.TransformVector(m.n * (Lift - ahead))) };
                    // the partner, should it be the one to rise (it was lifted before): Lift in front of this face
                    var theirs = Rank(oo) == 0 ? null : new FaceMove { path = oo.path, sub = oo.sub, k = o.tri,
                                              move = oo.renderer.transform.InverseTransformVector(section.TransformVector(o.n * (Lift + ahead))) };
                    wanted.Add((mine, theirs, area));
                    return;
                }
                if (report == null) return;
                int lo = Mathf.Min(A.group, B.group), hi = Mathf.Max(A.group, B.group);
                long key = ((long)lo << 32) | (uint)hi;
                if (!hits.TryGetValue(key, out var h)) hits[key] = h = new Hit();
                h.area += area;
                h.count++;
                if (area > h.best) { h.best = area; h.at = (A.a + A.b + A.c) / 3f; }
            });
            if (step != null)
            {
                // biggest overlaps first. A face lifted before stays the one on top (it rises further, never its partner
                // past it), and a face whose partner rises this round waits: no two faces chase each other up.
                wanted.Sort((x, y) => y.area.CompareTo(x.area));
                foreach (var w in wanted)
                {
                    var mover = w.a;
                    string mk = Key(w.a), ok = w.b == null ? null : Key(w.b);
                    if (ok != null && liftedEver.Contains(ok) && !liftedEver.Contains(mk)) { mover = w.b; var t = mk; mk = ok; ok = t; }
                    if (roundHeld.Contains(mk) || (ok != null && roundMoving.Contains(ok))) continue;
                    roundMoving.Add(mk);
                    if (ok != null) roundHeld.Add(ok);
                    if (step.TryGetValue(mk, out var have)) have.move = CoplanarNudges.Combine(have.move, mover.move);
                    else step[mk] = new FaceMove { path = mover.path, sub = mover.sub, k = mover.k, move = mover.move };
                }
            }
            if (report == null) return;

            report.AppendLine();
            report.AppendLine("== " + (level < 0 ? "far (last LOD level)" : "near (first LOD level)") + ": " + faces.Count + " triangles, " + owners.Count +
                              " renderer materials, skinned skipped " + skinned + ", transparent skipped " + see);
            var list = new List<KeyValuePair<long, Hit>>(hits);
            list.Sort((x, y) => y.Value.area.CompareTo(x.Value.area));
            foreach (var kind in new[] { "objects", "one FBX, two materials", "one FBX, one material" })
            {
                var rows = list.FindAll(kv => Kind(owners[(int)(kv.Key >> 32)], owners[(int)(kv.Key & 0xffffffff)]) == kind);
                float sum = 0;
                foreach (var kv in rows) sum += kv.Value.area;
                report.AppendLine("-- " + kind + ": " + rows.Count + " pairs, " + F(sum) + " m2");
                for (int n = 0; n < rows.Count && n < Listed; n++)
                {
                    var kv = rows[n];
                    Owner a = owners[(int)(kv.Key >> 32)], b = owners[(int)(kv.Key & 0xffffffff)];
                    var h = kv.Value;
                    report.AppendLine("   " + F(h.area * 1e4f) + " cm2 x" + h.count + " at " + V(h.at) + "  |  " + Label(a) + "  ~  " + Label(b));
                }
            }
        }

        // "objects": two renderers not of one hung FBX - CoplanarNudges' (or two graybox pieces); else the importer's.
        static string Kind(Owner a, Owner b)
        {
            if (a.renderer != b.renderer && (a.model == null || b.model == null || a.model != b.model)) return "objects";
            return a.material == b.material ? "one FBX, one material" : "one FBX, two materials";
        }

        // 2: a hung model (comes forward), 1: the room's shell, 0: a graybox or other piece (never moved).
        static int Rank(Owner o) => o.model == null || o.path == null || o.fixedHere ? 0 : o.shell ? 1 : 2;

        static string Label(Owner o) => (o.path ?? "(outside) " + o.renderer.name) + " [" + o.material + "]";

        static void Read(Renderer r, Mesh mesh, Transform section, Transform dressing, Target target, List<Owner> owners,
                         List<CoplanarFaces.Face> faces, ref int see)
        {
            string path = PathUnder(r.transform, dressing);
            string model = ModelOf(r.transform, dressing);
            bool shell = model != null && Array.IndexOf(target.shell, model) >= 0;
            var mats = r.sharedMaterials;
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var md = data[0];
                if (md.vertexCount == 0) return;
                var verts = new NativeArray<Vector3>(md.vertexCount, Allocator.Persistent);
                try
                {
                    md.GetVertices(verts);
                    var m = section.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    var world = new Vector3[verts.Length];
                    for (int i = 0; i < verts.Length; i++) world[i] = m.MultiplyPoint3x4(verts[i]);
                    for (int s = 0; s < md.subMeshCount && s < mats.Length; s++)
                    {
                        var sub = md.GetSubMesh(s);
                        if (sub.topology != MeshTopology.Triangles) continue;
                        if (mats[s] != null && mats[s].renderQueue >= 3000) { see++; continue; }
                        var idx = new NativeArray<int>(sub.indexCount, Allocator.Persistent);
                        try
                        {
                            md.GetIndices(idx, s);
                            int group = owners.Count;
                            var owner = new Owner { renderer = r, sub = s, path = path, model = model, shell = shell, fixedHere = model != null && Array.IndexOf(target.fixedModels, model) >= 0, material = mats[s] != null ? mats[s].name : "-" };
                            owners.Add(owner);
                            for (int k = 0; k + 2 < idx.Length; k += 3)
                                if (CoplanarFaces.Make(world[idx[k]], world[idx[k + 1]], world[idx[k + 2]], group, -1, MinArea, out var f))
                                {
                                    f.tri = k;
                                    owner.area += f.area;
                                    faces.Add(f);
                                }
                        }
                        finally { idx.Dispose(); }
                    }
                }
                finally { verts.Dispose(); }
            }
        }

        static string PathUnder(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (; t != null && t != root; t = t.parent) parts.Add(t.name + Twin(t));
            if (t == null) return null;
            parts.Reverse();
            return string.Join("/", parts);
        }

        // "#k" when siblings share the name (the chess board's two decks): the k-th of them, from 1 (CoplanarNudges)
        static string Twin(Transform t)
        {
            if (t.parent == null) return "";
            int same = 0, mine = 0;
            for (int i = 0; i < t.parent.childCount; i++)
            {
                var c = t.parent.GetChild(i);
                if (c.name != t.name) continue;
                same++;
                if (c == t) mine = same;
            }
            return same > 1 ? "#" + mine : "";
        }

        // The hung FBX a renderer belongs to: the name before " (model)" of its nearest such ancestor.
        static string ModelOf(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (t.name.EndsWith(" (model)", StringComparison.Ordinal)) return t.name.Substring(0, t.name.Length - " (model)".Length);
            return null;
        }

        static string G(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "(" + v.x.ToString("0.00", CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.00", CultureInfo.InvariantCulture) +
                                       ", " + v.z.ToString("0.00", CultureInfo.InvariantCulture) + ")";
    }
}
