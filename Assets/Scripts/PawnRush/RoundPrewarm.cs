using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R732 (the user, 11-21, after the giant chess course stuttered as the pawns went into the library and into its
    // chess room: "다른 코스 미니 게임도 방을 한번 미리 그려 두는 작업을 해 그게 로딩 역할이잖아" ... "코스가 추가 될 건데
    // 그 코스들도 로딩 뒤에서 그려야 해"): behind the match's loading screen the round's own camera tours every section the
    // round built - its entry, the middle of each of its beats, its exit - looking on and back with a wide view, one
    // frame at each spot, so every material, mesh and light state on the way is drawn once while nobody is watching.
    // A Direct3D 12 player makes a pipeline the first time a shader variant is drawn in a state; drawn here, it is not
    // made in the middle of the run. The sections' own lights (the carriage yard, the library, the observatory) come in
    // as the camera passes, as in play, so their looks are drawn too. Each section's switched-off things (the giant chess
    // board's warning squares, ...) are drawn once in front of the camera by mesh and material.
    //
    // Nothing here is per course: a section prefab added to the section set is toured the same way, from its entry, its
    // beats and its exit (PawnRushSection). The match loader waits for it (LoadWarmup); the section sounds stay as heard
    // from where the camera was (SectionSoundGate.HoldEar). Offline (no loading screen) it does nothing.
    [DisallowMultipleComponent]
    public sealed class RoundPrewarm : MonoBehaviour
    {
        const float Eye = 2.5f;              // metres over a beat's floor
        const float Inset = 3f;              // metres in from a section's entry and exit
        const float MergeMetres = 10f;       // spots closer than this to the last one kept are left out
        const float WideFov = 100f;          // the tour's vertical field of view (about 130 degrees across at 16:9)
        const float SettleSeconds = .6f;     // back where it started, for the sections' lights to ease back
        const float GiveUpSeconds = 30f;     // the whole tour at most
        const int MaxViews = 140;
        const int MaxSwitchedOffPerSection = 400;

        struct View
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public PawnRushSection Section;
            public bool First;
        }

        PawnRushRound round;
        Coroutine tour;
        Camera taken;
        OrbitCamera orbit;
        bool orbitWas, holdingEar;
        Pose before;
        float fovBefore;
        readonly HashSet<long> drawnPairs = new HashSet<long>();
        readonly Dictionary<string, float> longest = new Dictionary<string, float>();
        readonly List<string> order = new List<string>();
        int switchedOff;

        // From PawnRushRound.Build: a round built behind a match's loading screen gets toured (again, if it was rebuilt).
        public static void Begin(PawnRushRound round)
        {
            if (!Application.isPlaying || round == null || !LoadingScreenView.Showing) return;
            var prewarm = round.GetComponent<RoundPrewarm>();
            if (prewarm == null) prewarm = round.gameObject.AddComponent<RoundPrewarm>();
            prewarm.round = round;
            prewarm.Restart();
        }

        void Restart()
        {
            if (tour != null) StopCoroutine(tour);
            GiveBack();
            LoadWarmup.Hold(this, 0f);
            tour = StartCoroutine(Tour());
        }

        void OnDisable()
        {
            if (tour != null) StopCoroutine(tour);
            tour = null;
            GiveBack();
            LoadWarmup.Release(this);
        }

        IEnumerator Tour()
        {
            float began = Time.realtimeSinceStartup;
            // The sections' Start first: their lights hang their rigs a frame or two in (RoyalLibraryLighting).
            yield return null;
            yield return null;
            var cam = Camera.main;
            var views = Views();
            if (cam == null || views.Count == 0)
            {
                LoadWarmup.Release(this);
                tour = null;
                yield break;
            }
            Take(cam);
            drawnPairs.Clear();
            longest.Clear();
            order.Clear();
            switchedOff = 0;
            int shown = 0;
            string last = null;
            for (int i = 0; i < views.Count; i++)
            {
                if (!LoadingScreenView.Showing || Time.realtimeSinceStartup - began > GiveUpSeconds || cam == null) break;
                var view = views[i];
                if (view.Section == null) continue;
                cam.transform.SetPositionAndRotation(view.Position, view.Rotation);
                cam.fieldOfView = WideFov;
                if (view.First) DrawSwitchedOff(view.Section, cam);
                LoadWarmup.Hold(this, .95f * (i + 1) / views.Count);
                last = view.Section.DisplayName;
                if (!longest.ContainsKey(last))
                {
                    longest[last] = 0f;
                    order.Add(last);
                }
                yield return null;
                // The frame just drawn at that spot is this one's delta.
                longest[last] = Mathf.Max(longest[last], Time.unscaledDeltaTime);
                shown++;
            }
            // Back where the camera was; the sections' lights ease back to what it sees there.
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(before.position, before.rotation);
                cam.fieldOfView = fovBefore;
            }
            float until = Time.realtimeSinceStartup + SettleSeconds;
            while (Time.realtimeSinceStartup < until && LoadingScreenView.Showing) yield return null;
            GiveBack();
            Report(shown, views.Count, Time.realtimeSinceStartup - began);
            LoadWarmup.Release(this);
            tour = null;
        }

        // The spots: per section its entry, the middle of each beat a little over its floor, its exit - a spot within
        // MergeMetres of the one before left out - each looking on to the next spot and back.
        List<View> Views()
        {
            var views = new List<View>();
            if (round == null) return views;
            var points = new List<Vector3>();
            foreach (var section in round.Sections)
            {
                if (section == null || !section.isActiveAndEnabled) continue;
                points.Clear();
                var entry = section.EntryWorld;
                var exit = section.ExitWorld;
                Add(points, entry.position + entry.rotation * new Vector3(0f, Eye, Inset));
                foreach (var beat in section.Beats)
                {
                    var middle = (beat.min + beat.max) * .5f;
                    middle.y = Mathf.Clamp(beat.min.y + Eye, beat.min.y, beat.max.y);
                    Add(points, section.transform.TransformPoint(middle));
                }
                Add(points, exit.position + exit.rotation * new Vector3(0f, Eye, -Inset));
                for (int i = 0; i < points.Count; i++)
                {
                    var on = i + 1 < points.Count ? points[i + 1] - points[i] : exit.rotation * Vector3.forward;
                    on.y = 0f;
                    if (on.sqrMagnitude < .01f) on = section.transform.forward;
                    var look = Quaternion.LookRotation(on.normalized, Vector3.up);
                    views.Add(new View { Position = points[i], Rotation = look, Section = section, First = i == 0 });
                    views.Add(new View { Position = points[i], Rotation = look * Quaternion.Euler(0f, 180f, 0f), Section = section });
                }
            }
            // A very long round: every other spot's look back goes first, each section's first spot always stays.
            for (int i = views.Count - 1; i >= 0 && views.Count > MaxViews; i--)
                if (!views[i].First && i % 2 == 1) views.RemoveAt(i);
            return views;
        }

        static void Add(List<Vector3> points, Vector3 p)
        {
            if (points.Count > 0 && (points[points.Count - 1] - p).sqrMagnitude < MergeMetres * MergeMetres) return;
            points.Add(p);
        }

        // Each renderer switched off with its object (SetActive false: warning squares, marks, things that come and go)
        // drawn once, this frame, a few metres in front of the camera - each mesh and material pair once a tour. A
        // renderer that is merely disabled on a live object is left out: those are hidden for good (blockouts).
        void DrawSwitchedOff(PawnRushSection section, Camera cam)
        {
            int n = 0;
            var rotation = cam.transform.rotation;
            var at = cam.transform.position + cam.transform.forward * 8f;
            foreach (var r in section.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
                else if (r.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var bounds = mesh.bounds;
                float scale = Mathf.Min(1f, 3f / Mathf.Max(bounds.extents.magnitude, .01f));
                var matrix = Matrix4x4.TRS(at - rotation * (bounds.center * scale), rotation, Vector3.one * scale);
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length && i < mesh.subMeshCount; i++)
                {
                    var material = materials[i];
                    if (material == null) continue;
                    long pair = ((long)material.GetInstanceID() << 32) ^ (uint)mesh.GetInstanceID();
                    if (!drawnPairs.Add(pair)) continue;
                    if (n++ >= MaxSwitchedOffPerSection) return;
                    var settings = new RenderParams(material)
                    {
                        camera = cam,
                        layer = r.gameObject.layer,
                        shadowCastingMode = ShadowCastingMode.On,
                        receiveShadows = true,
                    };
                    Graphics.RenderMesh(settings, mesh, i, matrix);
                    switchedOff++;
                }
            }
        }

        // The round's camera for the tour: its follow camera stopped, its place and view noted, the section sounds held
        // to where it is.
        void Take(Camera cam)
        {
            taken = cam;
            before = new Pose(cam.transform.position, cam.transform.rotation);
            fovBefore = cam.fieldOfView;
            orbit = cam.GetComponent<OrbitCamera>();
            orbitWas = orbit != null && orbit.enabled;
            if (orbit != null) orbit.enabled = false;
            SectionSoundGate.HoldEar(before.position);
            holdingEar = true;
        }

        void GiveBack()
        {
            if (taken != null)
            {
                taken.transform.SetPositionAndRotation(before.position, before.rotation);
                taken.fieldOfView = fovBefore;
            }
            if (orbit != null && orbitWas)
            {
                orbit.enabled = true;
                orbit.Cut();
            }
            if (holdingEar) SectionSoundGate.ReleaseEar();
            holdingEar = false;
            taken = null;
            orbit = null;
            orbitWas = false;
        }

        void Report(int shown, int planned, float seconds)
        {
            var text = new StringBuilder();
            text.AppendFormat(CultureInfo.InvariantCulture, "[ChessFight] 코스 미리 그리기: 구간 {0} · 시점 {1}/{2} · 꺼진 물체 그림 {3} · {4:0.0}초",
                order.Count, shown, planned, switchedOff, seconds);
            if (order.Count > 0)
            {
                text.Append(" | 구간마다 가장 긴 프레임:");
                foreach (var name in order)
                    text.AppendFormat(CultureInfo.InvariantCulture, " {0} {1:0.00}초 ·", name, longest[name]);
                text.Length -= 2;
            }
            Debug.Log(text.ToString());
        }
    }
}
