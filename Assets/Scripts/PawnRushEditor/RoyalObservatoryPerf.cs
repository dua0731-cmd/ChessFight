using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R725: how heavy the Royal Observatory is once the three modelling sessions' models are on it - called by
    // RoyalObservatoryValidation after its pictures, in the same Play Mode batch on a copy of the project.
    //   - totals under the section: renderers drawn, triangles, meshes, materials, lights (and which cast shadows), LOD groups;
    //   - for a few views along the way: what the camera's frustum holds (renderers, triangles) and how long one frame
    //     takes to draw into 1920x1080 with the game camera's settings (post-processing, shadows): the median of 20
    //     Camera.Render calls, CPU only and until the GPU is done.
    // The Editor's batch mode is not a player: the times compare views and later changes, they are not a frame rate.
    // Unity's own Batches / SetPass / Draw Calls counters stay 0 in batch mode (R719), so draw calls are not here.
    // Report: Logs/RoyalObservatory/perf.txt and perf_views.csv.
    public static class RoyalObservatoryPerf
    {
        struct View { public string name; public Vector3 eye, target; public float fov; }

        static readonly View[] Views =
        {
            new View { name = "tunnel mouth", eye = new Vector3(1f, 2.6f, 11f), target = new Vector3(-12f, 4f, 52f), fov = 60f },
            new View { name = "dipper handle (player height)", eye = new Vector3(-6f, 3.2f, 22f), target = new Vector3(-19f, 2f, 35f), fov = 60f },
            new View { name = "dipper bowl", eye = new Vector3(-27f, 5.5f, 41f), target = new Vector3(-25f, 3f, 58f), fov = 60f },
            new View { name = "inside the globe", eye = new Vector3(-12f, 6.4f, 62f), target = new Vector3(6f, 10.5f, 65f), fov = 60f },
            new View { name = "cassiopeia", eye = new Vector3(19.5f, 15f, 64f), target = new Vector3(22f, 12f, 82f), fov = 60f },
            new View { name = "up the tube", eye = new Vector3(9f, 14.4f, 94f), target = new Vector3(9f, 19.4f, 114f), fov = 60f },
            new View { name = "whole hall (worst case)", eye = new Vector3(30f, 40f, 30f), target = new Vector3(-4f, 5f, 68f), fov = 64f },
        };

        public static void Measure(Transform section, string folder)
        {
            var text = new StringBuilder("Royal Observatory - what is drawn and how long a frame takes (R725). Editor batch mode on a project copy, not a player.\n\n");
            var renderers = section.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
            // R725 after the LODs went in: a renderer inside a LOD group counts only at its level. The totals are the
            // worst case (every group at LOD0); a view counts the level the camera would pick.
            var lodOf = new Dictionary<Renderer, (LODGroup group, int level)>();
            foreach (var g in section.GetComponentsInChildren<LODGroup>(false))
            {
                var levels = g.GetLODs();
                for (int i = 0; i < levels.Length; i++)
                    foreach (var r in levels[i].renderers)
                        if (r != null) lodOf[r] = (g, i);
            }
            var lodCounts = new long[4];
            foreach (var kv in lodOf) lodCounts[Mathf.Min(kv.Value.level, 3)] += Triangles(kv.Key);
            renderers.RemoveAll(r => lodOf.TryGetValue(r, out var at) && at.level > 0);
            var allLevels = section.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
            long tris = 0;
            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();
            var byGroup = new Dictionary<string, long>();
            foreach (var r in renderers)
            {
                long t = Triangles(r);
                tris += t;
                var mesh = MeshOf(r);
                if (mesh != null) meshes.Add(mesh);
                foreach (var m in r.sharedMaterials) if (m != null) materials.Add(m);
                string group = Group(section, r.transform);
                byGroup.TryGetValue(group, out long g);
                byGroup[group] = g + t;
            }
            var lights = section.GetComponentsInChildren<Light>(false).Where(l => l.enabled).ToList();
            var sceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled && l.gameObject.activeInHierarchy).ToList();
            text.Append($"Under the section: {renderers.Count} renderers drawn, {tris:N0} triangles with every LOD group at LOD0, {meshes.Count} distinct meshes, {materials.Count} distinct materials, ")
                .Append($"{section.GetComponentsInChildren<LODGroup>(false).Length} LOD groups (their triangles by level: {lodCounts[0]:N0} / {lodCounts[1]:N0} / {lodCounts[2]:N0} / {lodCounts[3]:N0}).\n")
                .Append($"Lights under the section: {lights.Count} ({lights.Count(l => l.type == LightType.Point)} point, {lights.Count(l => l.type == LightType.Spot)} spot, ")
                .Append($"{lights.Count(l => l.shadows != LightShadows.None)} casting shadows). Whole scene: {sceneLights.Count} lights, ")
                .Append($"{sceneLights.Count(l => l.shadows != LightShadows.None)} casting shadows.\n\nTriangles by group (the builder's groups):\n");
            foreach (var kv in byGroup.OrderByDescending(kv => kv.Value)) text.Append($"  {kv.Key,-28} {kv.Value,12:N0}\n");

            var main = Camera.main != null ? Camera.main : Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.enabled);
            // A copy of the game camera with its pipeline settings (post-processing, shadows: URP's additional data comes
            // along), every other script on it switched off so it does not follow the pawn.
            Camera probe;
            if (main != null)
            {
                var copy = Object.Instantiate(main.gameObject);
                copy.name = "Observatory perf camera";
                foreach (var behaviour in copy.GetComponentsInChildren<Behaviour>(true))
                    if (!(behaviour is Camera) && behaviour.GetType().Name != "UniversalAdditionalCameraData") behaviour.enabled = false;
                probe = copy.GetComponent<Camera>();
            }
            else probe = new GameObject("Observatory perf camera").AddComponent<Camera>();
            probe.enabled = false;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf) { name = "Observatory perf 1080p" };
            var sync = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true);
            probe.targetTexture = target;
            var csv = new StringBuilder("view,renderers_in_view,triangles_in_view,cpu_ms_median,gpu_done_ms_median,gpu_done_ms_worst\n");
            text.Append("\nViews (1920x1080, the game camera's settings").Append(main != null ? " from '" + main.name + "'" : ", no game camera found").Append("):\n");
            text.Append("  view                              renderers   triangles   CPU ms   + GPU ms   worst\n");
            foreach (var v in Views)
            {
                var eye = section.TransformPoint(v.eye);
                probe.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(section.TransformPoint(v.target) - eye, Vector3.up));
                probe.fieldOfView = v.fov;
                var planes = GeometryUtility.CalculateFrustumPlanes(probe);
                int inView = 0;
                long trisInView = 0;
                var picked = new Dictionary<LODGroup, int>();
                foreach (var r in allLevels)
                {
                    if (lodOf.TryGetValue(r, out var at))
                    {
                        if (!picked.TryGetValue(at.group, out int level)) picked[at.group] = level = Level(at.group, probe);
                        if (level != at.level) continue;
                    }
                    if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) { inView++; trisInView += Triangles(r); }
                }
                for (int i = 0; i < 5; i++) { probe.Render(); Wait(target, sync); }
                var cpu = new List<double>();
                var all = new List<double>();
                for (int i = 0; i < 20; i++)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    probe.Render();
                    long t1 = Stopwatch.GetTimestamp();
                    Wait(target, sync);
                    long t2 = Stopwatch.GetTimestamp();
                    cpu.Add(Ms(t1 - t0));
                    all.Add(Ms(t2 - t0));
                }
                double c = Median(cpu), a = Median(all), w = all.Max();
                text.Append($"  {v.name,-32} {inView,9} {trisInView,11:N0} {c,8:0.0} {a,10:0.0} {w,7:0.0}\n");
                csv.Append('"').Append(v.name).Append("\",").Append(inView).Append(',').Append(trisInView).Append(',')
                   .Append(F(c)).Append(',').Append(F(a)).Append(',').Append(F(w)).Append('\n');
            }
            probe.targetTexture = null;
            Object.Destroy(probe.gameObject);
            Object.Destroy(target);
            Object.Destroy(sync);
            text.Append("\nCompare: the Royal Library's baseline on this PC (R719, Docs/Environment/GPU_RESIDENT_DRAWER.md), measured the same way.\n");
            File.WriteAllText(Path.Combine(folder, "perf.txt"), text.ToString());
            File.WriteAllText(Path.Combine(folder, "perf_views.csv"), csv.ToString());
        }

        // The level a LOD group shows to this camera, as Unity picks it: the group's size over the view's height at its
        // distance, times the LOD bias, against each level's transition height. -1: past the last level (culled).
        static int Level(LODGroup g, Camera camera)
        {
            var t = g.transform;
            float size = g.size * Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z));
            float distance = Vector3.Distance(camera.transform.position, t.TransformPoint(g.localReferencePoint));
            float height = size / (2f * Mathf.Max(distance, .01f) * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad)) * QualitySettings.lodBias;
            var levels = g.GetLODs();
            for (int i = 0; i < levels.Length; i++)
                if (height >= levels[i].screenRelativeTransitionHeight) return i;
            return -1;
        }

        static Mesh MeshOf(Renderer r) =>
            r is SkinnedMeshRenderer s ? s.sharedMesh : r.TryGetComponent<MeshFilter>(out var f) ? f.sharedMesh : null;

        static long Triangles(Renderer r)
        {
            var mesh = MeshOf(r);
            if (mesh == null) return 0;
            long n = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles) n += mesh.GetIndexCount(i) / 3;
            return n;
        }

        // The builder's top group a renderer is in ("1 Big Dipper", "Hall (looks)" ...), or where a dressing hung it.
        static string Group(Transform section, Transform t)
        {
            var top = t;
            while (top.parent != null && top.parent != section && top.parent.parent != section) top = top.parent;
            return top == section ? section.name : top.name;
        }

        static void Wait(RenderTexture rt, Texture2D sync)
        {
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            sync.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            RenderTexture.active = was;
        }

        static double Median(List<double> list)
        {
            var a = list.OrderBy(x => x).ToList();
            return a.Count == 0 ? 0 : a[a.Count / 2];
        }

        static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        static string F(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
