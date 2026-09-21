using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ChessFight.Network
{
    // Only the NetworkSandbox scene (or the empty template SampleScene on Network)
    // opts into this prototype. No gameplay scenes or prefabs are rewritten.
    public sealed class NetworkSandbox : MonoBehaviour
    {
        SteamSession session;
        SteamMotion motion;
        PanelSettings panel;
        readonly Dictionary<ulong, Transform> avatars = new Dictionary<ulong, Transform>();
        readonly List<Material> materials = new List<Material>();
        Camera follow;
        Label status, roster, details, error;
        TextField code;
        Button find, invite, createTest, joinParty, joinTest, start, leave, newParty;
        Toggle capture;
        float refreshAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string name = SceneManager.GetActiveScene().name;
            if ((name == "NetworkSandbox" || name == "SampleScene") && FindFirstObjectByType<NetworkSandbox>() == null)
                new GameObject("ChessFight Network Sandbox").AddComponent<NetworkSandbox>();
        }
        void Awake()
        {
            Application.runInBackground = true;
            // Network branch's template has stale URP references without a URP package.
            // Use built-in rendering for this temporary runtime arena only.
            oldPipeline = GraphicsSettings.defaultRenderPipeline; oldQualityPipeline = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
            BuildArena(); BuildUi();
            session = new SteamSession(); session.Initialize();
            if (session.Online) motion = new SteamMotion(session);
        }
        RenderPipelineAsset oldPipeline, oldQualityPipeline;
        void BuildArena()
        {
            follow = Camera.main;
            if (follow == null) { var go = new GameObject("Network camera"); follow = go.AddComponent<Camera>(); go.tag = "MainCamera"; }
            follow.transform.position = new Vector3(0, 24, -24); follow.transform.rotation = Quaternion.Euler(45, 0, 0);
            follow.clearFlags = CameraClearFlags.SolidColor; follow.backgroundColor = new Color(.07f, .10f, .17f);
            var colors = new[] { MakeMaterial(new Color(.19f, .25f, .32f)), MakeMaterial(new Color(.28f, .34f, .42f)) };
            for (int x = 0; x < 8; x++) for (int z = 0; z < 8; z++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube); tile.name = "Board tile"; tile.transform.SetParent(transform);
                tile.transform.position = new Vector3((x - 3.5f) * 5, -.25f, (z - 3.5f) * 5); tile.transform.localScale = new Vector3(5, .5f, 5);
                tile.GetComponent<Renderer>().sharedMaterial = colors[(x + z) % 2];
            }
            var light = new GameObject("Sandbox sunlight").AddComponent<Light>(); light.transform.SetParent(transform);
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(50, -25, 0);
            RenderSettings.ambientLight = new Color(.55f, .58f, .65f);
            teamMaterials = new[] { MakeMaterial(new Color(.28f, .72f, 1f)), MakeMaterial(new Color(1f, .51f, .30f)) };
        }
        Material[] teamMaterials;
        Material MakeMaterial(Color color)
        {
            // A Resources reference keeps the shader in Player builds even though
            // the test arena has no serialized Material assets.
            var material = new Material(Resources.Load<Shader>("NetworkColor")); material.color = color; materials.Add(material); return material;
        }
        void BuildUi()
        {
            var document = gameObject.AddComponent<UIDocument>();
            panel = ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("NetworkTheme");
            panel.referenceResolution = new Vector2Int(1280, 720); document.panelSettings = panel;
            var root = document.rootVisualElement;
            root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var tree = Resources.Load<VisualTreeAsset>("NetworkHud"); tree.CloneTree(root);
            status = root.Q<Label>("status"); details = root.Q<Label>("details"); roster = root.Q<Label>("roster"); error = root.Q<Label>("error"); code = root.Q<TextField>("code");
            root.focusable = true;
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                var target = evt.target as VisualElement;
                if (target != code && (target == null || !code.Contains(target))) root.Focus();
            }, TrickleDown.TrickleDown);
            capture = root.Q<Toggle>("capture"); capture.value = true;
            find = Bind("find", () => session.FindMatch()); invite = Bind("invite", () => session.Invite());
            createTest = Bind("create-test", () => session.FindMatch(true));
            joinParty = Bind("join-party", () => { if (ulong.TryParse(code.value, out ulong id)) session.JoinParty(id); });
            joinTest = Bind("join-test", () => { if (ulong.TryParse(code.value, out ulong id)) session.JoinPrivateMatch(id); });
            start = Bind("start", () => session.StartGame()); leave = Bind("leave", () => session.Cancel());
            newParty = Bind("new-party", () => session.LeaveParty());
            Bind("copy-party", () => GUIUtility.systemCopyBuffer = session.Party.ToString());
            Bind("copy-match", () => GUIUtility.systemCopyBuffer = session.Match.ToString());
            Button Bind(string name, Action action) { var b = root.Q<Button>(name); b.focusable = false; b.clicked += action; return b; }
        }
        void Update()
        {
            if (session == null) return;
            session.Tick();
            var focused = code.panel?.focusController.focusedElement as VisualElement;
            bool typing = focused != null && (focused == code || code.Contains(focused));
            bool move = capture.value && !typing && Application.isFocused;
            float x = move ? (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0) : 0;
            float z = move ? (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0) : 0;
            motion?.Update(x, z, move && Input.GetKeyDown(KeyCode.Space));
            DrawPlayers();
            if (Time.unscaledTime < refreshAt) return; refreshAt = Time.unscaledTime + .2f;
            status.text = session.Status; error.text = session.Error;
            details.text = $"Party: {session.Party}  ({session.PartyMembers.Length}/6)\nMatch: {session.Match}\n{motion?.ConnectionStatus}";
            roster.text = session.Match == 0 ? string.Join("\n", session.PartyMembers.Select(id => (id == session.Self ? "> " : "  ") + session.Name(id))) :
                string.Join("\n", session.Roster.Values.OrderBy(p => p.Team).ThenBy(p => p.Slot).Select(p => $"{(p.Team == 0 ? "BLUE" : "ORANGE")}  {session.Name(p.Id)}{(p.Id == session.Self ? " (you)" : "")}"));
            bool ready = session.Online && session.IsLeader && !session.Busy;
            find.SetEnabled(ready); createTest.SetEnabled(ready); invite.SetEnabled(session.Online && !session.Busy && session.Party != 0);
            joinParty.SetEnabled(session.Online && !session.Busy); joinTest.SetEnabled(ready);
            start.SetEnabled(session.IsHost && !session.Started); leave.SetEnabled(session.Busy); newParty.SetEnabled(session.Online);
        }
        void DrawPlayers()
        {
            if (motion == null) return;
            foreach (ulong id in avatars.Keys.ToArray()) if (!motion.States.ContainsKey(id)) { Destroy(avatars[id].gameObject); avatars.Remove(id); }
            foreach (var pair in motion.States)
            {
                var state = pair.Value; Vector3 target = new Vector3(state.X, state.Y, state.Z);
                if (!avatars.TryGetValue(pair.Key, out var avatar))
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.name = "Player " + pair.Key; go.transform.SetParent(transform);
                    go.GetComponent<Renderer>().sharedMaterial = teamMaterials[state.Team];
                    avatar = go.transform; avatar.position = target; avatars.Add(pair.Key, avatar);
                    var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere); nose.transform.SetParent(avatar, false);
                    nose.transform.localPosition = new Vector3(0, .4f, .45f); nose.transform.localScale = Vector3.one * .3f;
                    nose.GetComponent<Renderer>().sharedMaterial = teamMaterials[1 - state.Team];
                }
                Vector3 delta = target - avatar.position; delta.y = 0;
                if (delta.sqrMagnitude > .002f) avatar.rotation = Quaternion.Slerp(avatar.rotation, Quaternion.LookRotation(delta), 15 * Time.unscaledDeltaTime);
                avatar.position = Vector3.Lerp(avatar.position, target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            }
            if (avatars.TryGetValue(session.Self, out var local))
            {
                Vector3 target = local.position + new Vector3(0, 12, -12);
                follow.transform.position = Vector3.Lerp(follow.transform.position, target, 1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));
                follow.transform.rotation = Quaternion.Euler(45, 0, 0);
            }
        }
        void OnDestroy()
        {
            motion?.Dispose(); session?.Dispose();
            foreach (var material in materials) Destroy(material);
            if (panel != null) Destroy(panel);
            GraphicsSettings.defaultRenderPipeline = oldPipeline; QualitySettings.renderPipeline = oldQualityPipeline;
        }
    }
}
