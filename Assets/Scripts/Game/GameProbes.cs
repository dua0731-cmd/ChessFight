using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24): reflections of the place instead of the sky, as the render lab's box probes did for the hall
    /// (JY-look fcb0d582), for every section: a few box-projected reflection probes laid along where the game's camera
    /// goes, each rendered once when it is laid (one face a frame, over 14 frames: laying one at once cost a 7 ms frame in
    /// the editor, a hitch every 26 m), the oldest reused. Marble, gold, glass
    /// and wet-looking floors then show the hall or the court they are in, not a blue sky indoors. The PC quality level
    /// has realtime probes off, so while the upgrade is on in the round's scenes they are turned on (and back off when
    /// it is off or Play ends). GameLook's one scene-wide probe stays under these (importance 1 against 2).
    /// </summary>
    public static class GameProbes
    {
        public static bool On = true;
        /// <summary>How many probes at most, how far apart (m), each one's box (m) and resolution.</summary>
        public static int Count = 6;
        public static float Spacing = 26f;
        public static Vector3 Box = new Vector3(64f, 36f, 64f);
        public static int Resolution = 256;

        static readonly List<ReflectionProbe> probes = new List<ReflectionProbe>();
        static bool switched, realtime0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            probes.Clear();
            switched = false;
            var go = new GameObject("Gfx probes (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watch>();
            Application.quitting -= Restore;
            Application.quitting += Restore;
        }

        // The quality level's realtime probes as they were.
        static void Restore()
        {
            if (!switched) return;
            QualitySettings.realtimeReflectionProbes = realtime0;
            switched = false;
        }

        static bool Wanted => On && GameGfx.Upgrade && GameGfx.InScope();

        /// <summary>A probe where the camera is now, rendered at once (the picture tool, before a picture).</summary>
        public static void Now(Camera camera)
        {
            if (!Wanted || camera == null) return;
            Use();
            var probe = Lay(Eye(camera));
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.RenderProbe();
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        }

        static void Use()
        {
            if (switched) return;
            realtime0 = QualitySettings.realtimeReflectionProbes;
            QualitySettings.realtimeReflectionProbes = true;
            switched = true;
        }

        // A little ahead of the camera, where what it looks at is.
        static Vector3 Eye(Camera camera)
        {
            var forward = camera.transform.forward;
            forward.y = 0f;
            return camera.transform.position + (forward.sqrMagnitude > 1e-4f ? forward.normalized * 10f : Vector3.zero);
        }

        static ReflectionProbe Lay(Vector3 at)
        {
            probes.RemoveAll(p => p == null);
            ReflectionProbe probe;
            if (probes.Count < Count)
            {
                var go = new GameObject("Gfx probe (JY-gfx)");
                probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.resolution = Resolution;
                probe.hdr = true;
                probe.boxProjection = true;
                probe.blendDistance = 6f;
                probe.importance = 2;
                probe.nearClipPlane = .3f;
                probe.farClipPlane = 400f;
                probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            }
            else
            {
                probe = probes[0];
                probes.RemoveAt(0);
            }
            probes.Add(probe);
            probe.transform.position = at;
            probe.size = Box;
            probe.center = Vector3.zero;
            probe.enabled = true;
            return probe;
        }

        sealed class Watch : MonoBehaviour
        {
            float next;

            void LateUpdate()
            {
                if (Time.realtimeSinceStartup < next) return;
                next = Time.realtimeSinceStartup + .5f;
                if (!Wanted)
                {
                    foreach (var p in probes)
                        if (p != null) p.enabled = false;
                    Restore();
                    return;
                }
                Use();
                var camera = ZoneLight.View();
                if (camera == null) return;
                var eye = Eye(camera);
                float nearest = float.MaxValue;
                foreach (var p in probes)
                {
                    if (p == null) continue;
                    p.enabled = true;
                    nearest = Mathf.Min(nearest, (p.transform.position - eye).sqrMagnitude);
                }
                if (nearest > Spacing * Spacing) Lay(eye).RenderProbe();
            }
        }
    }
}
