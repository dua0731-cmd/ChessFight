using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.ChessBoxingPrototype
{
    // This component is placed ONLY in the opt-in test scene. No runtime auto-bootstrap.
    public sealed class CBMatch : MonoBehaviour
    {
        public GameObject pawnModel;
        public CBSettings settings = new CBSettings();
        CBFighter player, opponent;
        CBBrain brain;
        Camera view;
        CBOrbitCamera orbit;
        readonly CBControl control = new CBControl();
        bool practiceMode;
        public CBFighter Player => player;
        public CBFighter Opponent => opponent;
        public Camera Camera => view;
        public CBControl Control => control;
        public bool ExternalControl { get; set; }
        public bool PracticeMode => practiceMode;
        [SerializeField, HideInInspector] List<Material> ownedMaterials = new List<Material>();
        GUIStyle title, text, small, button;
        Font font;
        AudioSource audioSource;
        AudioClip hitSound, blockSound;
        float countdown = 2f, elapsed;
        bool paused, ended;
        string result = "", problem = "";
        CursorLockMode previousCursor;
        bool previousCursorVisible;

        void OnEnable()
        {
            // Unity restores live bodies on script reload, but not plain C# controllers/events.
            if (player == null || opponent == null || view == null) return;
            player.Defeated -= Finish; opponent.Defeated -= Finish;
            player.Defeated += Finish; opponent.Defeated += Finish;
            player.Hit -= OnHit; opponent.Hit -= OnHit;
            player.Hit += OnHit; opponent.Hit += OnHit;
            brain = new CBBrain(opponent, player, settings);
            brain.Reset();
            orbit = new CBOrbitCamera(view, player, opponent);
            control.Set(player.Intent);
            if (hitSound == null) hitSound = MakeSound("CB hit", 125f, 0.085f);
            if (blockSound == null) blockSound = MakeSound("CB block", 330f, 0.05f);
        }

        void Start()
        {
            previousCursor = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            try
            {
                if (pawnModel == null) throw new InvalidOperationException("테스트 씬 생성 메뉴로 Pawn 모델을 연결해 주세요.");
                BuildArena();
                player = CreateFighter("Player", new Vector3(0f, 0f, -1.05f), Vector3.forward, new Color(0.26f, 0.62f, 0.92f));
                opponent = CreateFighter("AI", new Vector3(0f, 0f, 1.05f), Vector3.back, new Color(0.92f, 0.35f, 0.27f));
                player.Opponent = opponent;
                opponent.Opponent = player;
                player.Defeated += Finish;
                opponent.Defeated += Finish;
                player.Hit += OnHit;
                opponent.Hit += OnHit;
                brain = new CBBrain(opponent, player, settings);
                brain.Reset();
                var cameraObject = new GameObject("CB Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(transform, false);
                view = cameraObject.GetComponent<Camera>();
                view.tag = "MainCamera";
                view.fieldOfView = 55f;
                view.nearClipPlane = 0.05f;
                view.farClipPlane = 80f;
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = new Color(0.12f, 0.17f, 0.23f);
                orbit = new CBOrbitCamera(view, player, opponent);
                orbit.Tick(control.Position, 0f, true);
                audioSource = cameraObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;
                audioSource.volume = 0.28f;
                hitSound = MakeSound("CB hit", 125f, 0.085f);
                blockSound = MakeSound("CB block", 330f, 0.05f);
                SetCursor(false);
            }
            catch (Exception e)
            {
                problem = e.Message;
                Debug.LogException(e, this);
                SetCursor(true);
                if (player != null) player.SetPaused(true);
                if (opponent != null) opponent.SetPaused(true);
            }
        }

        CBFighter CreateFighter(string label, Vector3 floor, Vector3 forward, Color accent)
        {
            var go = new GameObject("CB " + label);
            go.transform.SetParent(transform, false);
            var fighter = go.AddComponent<CBFighter>();
            try { fighter.Initialize(pawnModel, settings, floor, forward, accent); }
            catch { go.SetActive(false); Destroy(go); throw; }
            fighter.InputEnabled = false;
            return fighter;
        }

        void Update()
        {
            if (problem.Length > 0 || player == null || opponent == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (Input.GetKeyDown(KeyCode.Tab)) SetPracticeMode(!practiceMode);
            if (paused || ended) return;
            float dt = Time.deltaTime;
            countdown = Mathf.Max(0f, countdown - dt);
            bool active = countdown <= 0f;
            player.InputEnabled = opponent.InputEnabled = active;
            if (!active) return;
            elapsed += dt;
            if (ExternalControl) return;
            // Position, not gesture velocity: the chosen hand remains selected when the mouse stops.
            Vector2 mouse = Input.mousePosition;
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 intent = (mouse - center) / Mathf.Max(100f, Mathf.Min(Screen.width, Screen.height) * 0.32f);
            float x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float z = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            Drive(intent, new Vector2(x, z), Input.GetMouseButton(1) || Input.GetKey(KeyCode.L),
                Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J), Input.GetKeyDown(KeyCode.Space));
            if (!practiceMode) brain.Tick(dt);
            else opponent.Command(Vector3.zero, false);
        }

        // Shared by normal input and the opt-in Editor PlayerLoop checks.
        public void Drive(Vector2 intent, Vector2 movement, bool guard, bool punch, bool dodge)
        {
            if (player == null || opponent == null || paused || ended || countdown > 0f) return;
            control.Set(intent);
            player.SetIntent(control.Position);
            Vector3 forward = opponent.Position - player.Position;
            forward.y = 0f;
            if (forward.sqrMagnitude < .001f) forward = player.Facing;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            movement = Vector2.ClampMagnitude(movement, 1f);
            Vector3 move = right * (movement.x * .80f) + forward * (movement.y * (movement.y < 0f ? .70f : 1f));
            player.Command(move, guard);
            if (punch) player.PunchSelected(control.LeftHand, control.BodyPunch);
            if (dodge) player.DodgeDirection(control.Direction);
        }

        public void SetPracticeMode(bool enabled)
        {
            practiceMode = enabled;
            if (opponent != null) opponent.Command(Vector3.zero, false);
            if (brain != null) brain.Reset();
        }

        void LateUpdate()
        {
            if (orbit != null) orbit.Tick(control.Position, paused ? 0f : Time.deltaTime);
        }

        void Finish(CBFighter loser)
        {
            if (ended) return;
            ended = true;
            player.InputEnabled = opponent.InputEnabled = false;
            player.Command(Vector3.zero, false);
            opponent.Command(Vector3.zero, false);
            player.Charging = opponent.Charging = false;
            result = loser == opponent ? "승리!  ·  KO" : "패배  ·  KO";
            SetCursor(true);
        }

        void OnHit(CBFighter victim, bool blocked, bool strong)
        {
            // Readability comes first while establishing the camera; no procedural camera shake.
            AudioClip clip = blocked ? blockSound : hitSound;
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        void TogglePause()
        {
            if (ended) return;
            paused = !paused;
            player.SetPaused(paused);
            opponent.SetPaused(paused);
            SetCursor(paused);
        }

        public void Restart()
        {
            if (player == null || opponent == null || problem.Length > 0) return;
            player.ResetFighter();
            opponent.ResetFighter();
            player.InputEnabled = opponent.InputEnabled = false;
            brain.Reset();
            countdown = 2f;
            elapsed = 0f;
            paused = ended = false;
            result = "";
            control.Reset();
            if (orbit != null) orbit.Tick(control.Position, 0f, true);
            SetCursor(false);
        }

        void OnApplicationFocus(bool focus)
        { if (!focus && !ExternalControl && !paused && !ended && player != null && opponent != null) TogglePause(); }

        static void SetCursor(bool show)
        { Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Confined; Cursor.visible = show; }

        void BuildArena()
        {
            Material ivory = Material(new Color(0.82f, 0.79f, 0.68f));
            Material blue = Material(new Color(0.24f, 0.34f, 0.43f));
            Material dark = Material(new Color(0.09f, 0.14f, 0.20f));
            Material rope = Material(new Color(0.74f, 0.13f, 0.12f));
            Box("Hall floor", new Vector3(0f, -0.45f, 0f), new Vector3(24f, 0.3f, 24f), dark, true);
            Box("Ring base", new Vector3(0f, -0.14f, 0f), new Vector3(7f, 0.28f, 7f), blue, true);
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                    Box("Mat", new Vector3((x - 3.5f) * 0.8f, 0.004f, (z - 3.5f) * 0.8f), new Vector3(0.798f, 0.008f, 0.798f), (x + z) % 2 == 0 ? ivory : blue, false);
            // Stage 1 boundaries are rigid. Elastic rope behaviour belongs to stage 2.
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float edge = side % 2 == 0 ? -3.25f : 3.25f;
                Vector3 wallAt = alongX ? new Vector3(0f, 0.6f, edge) : new Vector3(edge, 0.6f, 0f);
                var wall = new GameObject("CB solid test boundary");
                wall.transform.SetParent(transform, false);
                wall.transform.position = wallAt;
                var collider = wall.AddComponent<BoxCollider>();
                collider.size = alongX ? new Vector3(6.8f, 1.6f, 0.1f) : new Vector3(0.1f, 1.6f, 6.8f);
                for (int row = 0; row < 3; row++)
                {
                    Vector3 at = wallAt; at.y = 0.33f + row * 0.25f;
                    Box("Fixed rail", at, alongX ? new Vector3(6.5f, 0.035f, 0.035f) : new Vector3(0.035f, 0.035f, 6.5f), rope, false);
                }
            }
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = new Vector3(x * 3.25f, 0.55f, z * 3.25f);
                    Box("Rook post", corner, new Vector3(0.3f, 1.1f, 0.3f), ivory, true);
                    Box("Corner pad", corner + new Vector3(-x * 0.14f, 0.08f, -z * 0.14f), new Vector3(0.28f, 0.6f, 0.28f), blue, false);
                    for (int i = 0; i < 4; i++)
                        Box("Battlement", corner + new Vector3((i % 2 == 0 ? -1 : 1) * 0.11f, 0.62f, (i < 2 ? -1 : 1) * 0.11f), new Vector3(0.1f, 0.17f, 0.1f), ivory, false);
                }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Box("Hall pillar", new Vector3(Mathf.Cos(a) * 10f, 2f, Mathf.Sin(a) * 10f), new Vector3(0.8f, 5f, 0.8f), ivory, false);
            }
            var sun = new GameObject("CB Key light", typeof(Light));
            sun.transform.SetParent(transform, false);
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.5f; light.shadows = LightShadows.Soft;
            var fill = new GameObject("CB Fill light", typeof(Light));
            fill.transform.SetParent(transform, false);
            fill.transform.rotation = Quaternion.Euler(30f, 150f, 0f);
            fill.GetComponent<Light>().type = LightType.Directional;
            fill.GetComponent<Light>().intensity = 0.55f;
        }

        Material Material(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Lit 셰이더를 찾을 수 없습니다.");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            ownedMaterials.Add(material);
            return material;
        }

        GameObject Box(string label, Vector3 position, Vector3 scale, Material material, bool solid)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label;
            box.transform.SetParent(transform, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                var collider = box.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
            }
            return box;
        }

        static AudioClip MakeSound(string label, float frequency, float duration)
        {
            const int rate = 22050;
            float[] samples = new float[Mathf.CeilToInt(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 45f) * 0.7f;
            }
            var clip = AudioClip.Create(label, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void Styles()
        {
            if (title != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 20);
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = Color.white;
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, alignment = TextAnchor.MiddleLeft };
            text.normal.textColor = Color.white;
            small = new GUIStyle(text) { fontSize = 14 };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20 };
        }

        void OnGUI()
        {
            Styles();
            Matrix4x4 previous = GUI.matrix;
            Color previousColor = GUI.color;
            try
            {
                float scale = Mathf.Max(0.4f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
                GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
                float w = Screen.width / scale, h = Screen.height / scale;
                Panel(new Rect(16f, 16f, w - 32f, 90f));
                GUI.Label(new Rect(w * 0.5f - 180f, 24f, 360f, 36f), "CHESS FIGHT · BOXING", text);
                GUI.Label(new Rect(w * 0.5f - 90f, 66f, 180f, 30f), $"{Mathf.FloorToInt(elapsed / 60f):00}:{Mathf.FloorToInt(elapsed % 60f):00}", title);
                if (player != null && opponent != null)
                {
                    FighterHud(player, new Rect(32f, 22f, 280f, 80f), "PLAYER", new Color(0.25f, 0.66f, 0.98f));
                    FighterHud(opponent, new Rect(w - 312f, 22f, 280f, 80f), "AI", new Color(0.98f, 0.36f, 0.25f));
                }
                Panel(new Rect(16f, h - 76f, w - 32f, 60f));
                GUI.Label(new Rect(30f, h - 70f, w - 60f, 25f), "WASD 이동  ·  마우스 좌우로 손 선택 / 아래로 바디  ·  왼쪽 클릭 펀치  ·  오른쪽 클릭 가드", small);
                GUI.Label(new Rect(30f, h - 46f, w - 60f, 25f), "Space 방향 회피  ·  Tab 연습/대전  ·  R 재시작  ·  Esc 일시정지", small);
                if (!paused && !ended && problem.Length == 0)
                {
                    float radius = Mathf.Min(w, h) * .32f;
                    Vector2 selected = new Vector2(w * .5f, h * .5f) + new Vector2(control.Position.x, -control.Position.y) * radius;
                    GUI.color = new Color(.85f, .92f, 1f, .80f);
                    GUI.DrawTexture(new Rect(selected.x - 3f, selected.y - 3f, 6f, 6f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    string hand = control.LeftHand ? "왼손" : "오른손";
                    string height = control.BodyPunch ? "바디" : "머리";
                    Panel(new Rect(w * .5f - 185f, h - 114f, 370f, 32f));
                    GUI.Label(new Rect(w * .5f - 180f, h - 110f, 360f, 25f), hand + " · " + height + (practiceMode ? "   |   연습 상대" : "   |   AI 대전"), text);
                }
                if (problem.Length > 0) CenterPanel(w, h, "실행 준비 확인", problem, false);
                else if (ended) CenterPanel(w, h, result, $"유효 타격 {player.Landed}회 · 가드 {player.Blocks}회", true);
                else if (paused)
                {
                    CenterPanel(w, h, "일시정지", "Esc를 누르면 계속합니다.", true);
                    if (GUI.Button(new Rect(w * 0.5f - 110f, h * 0.5f + 100f, 220f, 42f), "계속하기", button)) TogglePause();
                }
                else if (countdown > 0f)
                    GUI.Label(new Rect(w * 0.5f - 100f, h * 0.5f - 70f, 200f, 90f), Mathf.CeilToInt(countdown).ToString(), title);
            }
            finally { GUI.matrix = previous; GUI.color = previousColor; }
        }

        void FighterHud(CBFighter fighter, Rect r, string label, Color color)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 25f), $"{label}  ·  폰", text);
            Bar(new Rect(r.x, r.y + 24f, r.width, 14f), fighter.Health01, color);
            Bar(new Rect(r.x, r.y + 43f, r.width, 6f), fighter.Stamina01, new Color(0.3f, 0.85f, 0.63f));
            GUI.Label(new Rect(r.x, r.y + 52f, r.width, 22f), $"HP {Mathf.CeilToInt(fighter.Health)}  ·  {fighter.LastEvent}", small);
        }

        void CenterPanel(float w, float h, string heading, string detail, bool restart)
        {
            Panel(new Rect(w * 0.5f - 290f, h * 0.5f - 125f, 580f, paused ? 290f : 245f));
            GUI.Label(new Rect(w * 0.5f - 270f, h * 0.5f - 100f, 540f, 60f), heading, title);
            GUI.Label(new Rect(w * 0.5f - 260f, h * 0.5f - 25f, 520f, 55f), detail, text);
            if (restart && GUI.Button(new Rect(w * 0.5f - 110f, h * 0.5f + 45f, 220f, 42f), "다시 하기  [R]", button)) Restart();
        }

        static void Panel(Rect rect)
        { GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.88f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }
        static void Bar(Rect rect, float fraction, Color color)
        {
            GUI.color = new Color(0.13f, 0.17f, 0.22f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color; GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void OnDestroy()
        {
            Cursor.lockState = previousCursor;
            Cursor.visible = previousCursorVisible;
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            if (hitSound != null) Destroy(hitSound);
            if (blockSound != null) Destroy(blockSound);
            if (font != null) Destroy(font);
        }
    }
}
