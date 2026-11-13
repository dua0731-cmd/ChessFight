using System.Collections.Generic;
using System.Text;
using ChessFight.Game;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ChessFight.VFX
{
    /// <summary>
    /// The VFX workbench (Assets/VFX/Scenes/VFX_Workbench.unity, 10-27): one chess piece at a time on a dark stage, a
    /// target pawn in front of it, to build and judge effects piece by piece. A piece's effects are the prefabs in
    /// Assets/VFX/Prefabs/&lt;Piece&gt;/, plus Assets/VFX/Prefabs/Common/ for every piece: a prefab dropped in a folder
    /// is on the list the next time a key is pressed, nothing to register. Editor only (the folders are read with
    /// AssetDatabase) and the scene is not in the build. In edit mode it shows the chosen piece and the target, not
    /// saved with the scene, so effects can be placed against them; in Play mode the panel's keys play, repeat, slow and
    /// compare. The bench volume (B) holds the bloom being tried; off, the picture is the game's own look
    /// (Settings/GameLook.asset under GameLook's soft volume). The neutral volume (N, R172) swaps only the tonemapping
    /// for Neutral to compare with the game's ACES; the game itself is not changed.
    /// </summary>
    [ExecuteAlways]
    public sealed class VfxWorkbench : MonoBehaviour
    {
        public enum Piece { King, Queen, Rook, Bishop, Knight, Pawn }

        public const string PrefabRoot = "Assets/VFX/Prefabs", CommonFolder = "Common";
        static readonly string[] PieceNames = { "킹", "퀸", "룩", "비숍", "나이트", "폰" };
        static readonly float[] Speeds = { 1f, .5f, .25f, .1f };
        static readonly float[] Densities = { 1f, .6f, .35f };
        // An effect is cleared this long after its cue ends (its last particles fade out) and replayed this long after.
        const float Linger = VfxCue.Linger, ReplayGap = .6f, NoCueLength = 3f;

        [Tooltip("The piece on the stage. Its effects are the prefabs in Assets/VFX/Prefabs/<Piece>/ and .../Common/.")]
        public Piece piece = Piece.King;
        [Tooltip("Metres from the piece to the target pawn the effects aim at.")]
        public float targetDistance = 6f;
        public bool showTarget = true;
        [Tooltip("The bench's own global volume (the bloom being tried); B turns it on and off in Play mode.")]
        public GameObject benchVolume;
        [Tooltip("A global volume that swaps only the tonemapping for Neutral (the game keeps ACES); N turns it on and off in Play mode.")]
        public GameObject neutralVolume;
        public Material floorMaterial;
        public Camera view;

        struct Entry { public GameObject prefab; public string label; }
        struct Live { public GameObject go; public float until; }

        readonly List<Entry> effects = new List<Entry>();
        readonly List<Live> live = new List<Live>();
        GameObject stage, shown, dummy;
        float shownHeight, dummyHeight;
        int pick, speed, density;
        bool loop, paused, panel = true, rebuild;
        float replayAt = -1f, yaw, pitch, distance;
        Vector3 pivot;
        GUIStyle style;

        void OnEnable()
        {
            BuildStage();
            if (!Application.isPlaying) return;
            // Loading the scene also runs OnValidate; the stage is already fresh.
            rebuild = false;
            Scan();
            ResetView();
            ApplyTime();
            VfxCue.Density = Densities[density];
        }

        void OnDisable()
        {
            ClearStage();
            if (!Application.isPlaying) return;
            Time.timeScale = 1f;
            VfxCue.Density = 1f;
        }

        // OnValidate may not create objects: edit mode rebuilds just after the inspector change, Play mode next Update.
        void OnValidate()
        {
            if (Application.isPlaying)
            {
                rebuild = true;
                return;
            }
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled && !Application.isPlaying) BuildStage();
            };
#endif
        }

        void BuildStage()
        {
            ClearStage();
            stage = new GameObject("VFX Workbench Stage");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(stage.transform, false);
            floor.transform.localScale = new Vector3(6f, 1f, 6f);
            if (floorMaterial != null) floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            shown = Place(piece.ToString(), Vector3.zero, Quaternion.identity);
            dummy = Place(nameof(Piece.Pawn), Vector3.forward * targetDistance, Quaternion.Euler(0f, 180f, 0f));
            dummy.name = "Target (Pawn)";
            shownHeight = Height(shown);
            dummyHeight = Height(dummy);
            dummy.SetActive(showTarget);
            // The edit-mode stage is only a reference to work against: never saved, not editable.
            if (!Application.isPlaying)
                foreach (var t in stage.GetComponentsInChildren<Transform>(true))
                    t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        }

        GameObject Place(string kind, Vector3 at, Quaternion facing)
        {
            // The lobby's pieces (MenuPieceBaker): the match models in the bind pose, feet on y = 0, facing +Z.
            var prefab = Resources.Load<GameObject>("MenuPieces/" + kind);
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, at, facing, stage.transform);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetParent(stage.transform, false);
                go.transform.SetPositionAndRotation(at + Vector3.up, facing);
            }
            go.name = kind;
            return go;
        }

        static float Height(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 1.6f;
            var box = renderers[0].bounds;
            foreach (var r in renderers) box.Encapsulate(r.bounds);
            return Mathf.Max(.2f, box.max.y - go.transform.position.y);
        }

        void ClearStage()
        {
            live.Clear();
            if (stage == null) return;
            if (Application.isPlaying) Destroy(stage);
            else DestroyImmediate(stage);
            stage = shown = dummy = null;
        }

        void Scan()
        {
            var keep = pick < effects.Count ? effects[pick].prefab : null;
            effects.Clear();
#if UNITY_EDITOR
            Collect(PrefabRoot + "/" + piece, "");
            Collect(PrefabRoot + "/" + CommonFolder, "[공용] ");
#endif
            pick = 0;
            for (int i = 0; i < effects.Count; i++)
                if (effects[i].prefab == keep) pick = i;
        }

#if UNITY_EDITOR
        void Collect(string folder, string tag)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            foreach (string path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) effects.Add(new Entry { prefab = prefab, label = tag + prefab.name });
            }
        }
#endif

        void Update()
        {
            if (!Application.isPlaying) return;
            if (rebuild)
            {
                rebuild = false;
                BuildStage();
                Scan();
            }
            Keys();
            Orbit();
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i].go != null && Time.time < live[i].until) continue;
                if (live[i].go != null) Destroy(live[i].go);
                live.RemoveAt(i);
            }
            if (loop && replayAt >= 0f && Time.time >= replayAt) PlayPick();
        }

        void Keys()
        {
            for (int i = 0; i < PieceNames.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Choose((Piece)i);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E)) Shift(1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q)) Shift(-1);
            if (Input.GetKeyDown(KeyCode.Space)) PlayPick();
            if (Input.GetKeyDown(KeyCode.L))
            {
                loop = !loop;
                if (!loop) replayAt = -1f;
            }
            if (Input.GetKeyDown(KeyCode.C)) Clear();
            if (Input.GetKeyDown(KeyCode.T))
            {
                speed = (speed + 1) % Speeds.Length;
                ApplyTime();
            }
            if (Input.GetKeyDown(KeyCode.P))
            {
                paused = !paused;
                ApplyTime();
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                density = (density + 1) % Densities.Length;
                VfxCue.Density = Densities[density];
            }
            if (Input.GetKeyDown(KeyCode.B) && benchVolume != null) benchVolume.SetActive(!benchVolume.activeSelf);
            if (Input.GetKeyDown(KeyCode.N) && neutralVolume != null) neutralVolume.SetActive(!neutralVolume.activeSelf);
            if (Input.GetKeyDown(KeyCode.V) && dummy != null)
            {
                showTarget = !showTarget;
                dummy.SetActive(showTarget);
            }
            if (Input.GetKeyDown(KeyCode.H)) panel = !panel;
            if (Input.GetKeyDown(KeyCode.F)) ResetView();
        }

        void Choose(Piece next)
        {
            piece = next;
            BuildStage();
            Scan();
        }

        void Shift(int by)
        {
            Scan();
            if (effects.Count > 0) pick = (pick + by + effects.Count) % effects.Count;
        }

        void PlayPick()
        {
            Scan();
            if (effects.Count == 0 || shown == null) return;
            var prefab = effects[pick].prefab;
            var cue = prefab.GetComponent<VfxCue>();
            var anchor = cue != null ? cue.anchor : VfxCue.Anchor.Chest;
            var parent = cue != null && cue.follow ? shown.transform : stage.transform;
            var go = Instantiate(prefab, AnchorPoint(anchor), shown.transform.rotation, parent);
            go.name = prefab.name;
            var played = go.GetComponent<VfxCue>();
            if (played != null) played.Aim(TargetPoint());
            float length = cue != null ? cue.length : NoCueLength;
            live.Add(new Live { go = go, until = Time.time + length + Linger });
            replayAt = Time.time + length + ReplayGap;
        }

        void Clear()
        {
            foreach (var l in live)
                if (l.go != null) Destroy(l.go);
            live.Clear();
            replayAt = -1f;
        }

        void ApplyTime() => Time.timeScale = paused ? 0f : Speeds[speed];

        Vector3 AnchorPoint(VfxCue.Anchor anchor)
        {
            var feet = shown.transform.position;
            switch (anchor)
            {
                case VfxCue.Anchor.Feet: return feet;
                case VfxCue.Anchor.Head: return feet + Vector3.up * shownHeight * .95f;
                case VfxCue.Anchor.Target: return TargetPoint();
                default: return feet + Vector3.up * shownHeight * .55f;
            }
        }

        // The target pawn's chest, where it stands even while hidden (V).
        Vector3 TargetPoint() => shown.transform.position + shown.transform.forward * targetDistance + Vector3.up * dummyHeight * .55f;

        void ResetView()
        {
            // From the piece's right, a little above: the piece on the left of the picture, the target on the right.
            pivot = new Vector3(0f, 1.1f, targetDistance * .5f);
            yaw = -55f;
            pitch = 12f;
            distance = 8f;
        }

        void Orbit()
        {
            var cam = view != null ? view : Camera.main;
            if (cam == null) return;
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 3f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 3f, -10f, 85f);
            }
            if (Input.GetMouseButton(2))
                pivot -= (cam.transform.right * Input.GetAxis("Mouse X") + cam.transform.up * Input.GetAxis("Mouse Y")) * distance * .02f;
            distance = Mathf.Clamp(distance * (1f - Input.mouseScrollDelta.y * .1f), 1.5f, 40f);
            var turn = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.SetPositionAndRotation(pivot - turn * Vector3.forward * distance, turn);
        }

        void OnGUI()
        {
            if (!Application.isPlaying || !panel) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true, wordWrap = true };
            var text = new StringBuilder("<b>VFX 작업대</b>   H 숨기기\n기물 1~6: ");
            for (int i = 0; i < PieceNames.Length; i++)
            {
                if (i > 0) text.Append(" · ");
                string label = (i + 1) + " " + PieceNames[i];
                text.Append(i == (int)piece ? "<color=#ffd75e><b>" + label + "</b></color>" : label);
            }
            text.Append('\n');
            if (effects.Count == 0) text.Append("이펙트: <color=#ff9a7a>없음</color>, 아래 폴더에 프리팹을 넣으세요\n");
            else text.Append("이펙트 ←/→ (").Append(pick + 1).Append('/').Append(effects.Count).Append("): <b>").Append(effects[pick].label).Append("</b>\n");
            text.Append("폴더: ").Append(PrefabRoot).Append('/').Append(piece).Append(", 공용: ").Append(CommonFolder).Append('\n');
            text.Append("Space 재생 · L 반복 ").Append(loop ? "<b>켜짐</b>" : "꺼짐").Append(" · C 지우기\n");
            text.Append("T 속도 ×").Append(Speeds[speed].ToString("0.##")).Append(" · P ").Append(paused ? "<b>멈춤</b>" : "멈추기")
                .Append(" · G 밀도 ").Append(Densities[density].ToString("0.00")).Append('\n');
            text.Append("B 블룸: ").Append(benchVolume != null && benchVolume.activeSelf ? "<b>작업대 볼륨</b>" : "게임 기준");
            if (neutralVolume != null) text.Append(" · N 톤매핑: ").Append(neutralVolume.activeSelf ? "<b>Neutral</b>" : "ACES(게임)");
            text.Append(" · V 과녁 ").Append(showTarget ? "보임" : "숨김").Append('\n');
            text.Append("오른쪽 드래그 회전 · 가운데 드래그 이동 · 휠 거리 · F 처음 시점");
            var content = new GUIContent(text.ToString());
            const float width = 470f;
            float height = style.CalcHeight(content, width - 20f) + 14f;
            var box = new Rect(12f, 12f, width, height);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10f, box.y + 7f, width - 20f, height - 10f), content, style);
        }
    }
}
