#if CHESSFIGHT_INPUTSYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChessFight.Game
{
    // Unity 6's recommended input path, reading the ChessFightControls asset.
    //
    // This lives in its own assembly gated on CHESSFIGHT_INPUTSYSTEM, which the
    // asmdef derives from the presence of com.unity.inputsystem. When the package
    // is missing the assembly is skipped whole, so neither this file nor its
    // reference to Unity.InputSystem can break the project, and the legacy source
    // in ChessFight.Game takes over.
    //
    // Do NOT gate this on Unity's own ENABLE_INPUT_SYSTEM: that define follows the
    // Active Input Handling setting, not the package, so it is set even on a
    // machine that has never installed the Input System.
    //
    // It registers itself so nothing else ever has to know the Input System exists.
    public sealed class InputSystemMoveSource : IMoveInputSource
    {
        const string AssetPath = "ChessFightControls";
        const string MapName = "Gameplay";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => MoveInputSources.Register(Create);

        static IMoveInputSource Create()
        {
#if !ENABLE_INPUT_SYSTEM
            // Deliberate for now. Active Input Handling is "Input Manager (Old)"
            // because this project has no uGUI, and with the Input System backend
            // enabled UI Toolkit's runtime panel stops receiving pointer and
            // keyboard events entirely - no clicking, no typing a lobby number.
            // Re-enabling it means adding com.unity.ugui plus an EventSystem with
            // InputSystemUIInputModule, then switching the setting to Both.
            // Until then every action here would read zero, so hand back to legacy.
            Debug.Log("[ChessFight] Input System 백엔드가 꺼져 있어 레거시 입력을 사용합니다. " +
                      "(Active Input Handling = Input Manager (Old))");
            return null;
#else
            var asset = Resources.Load<InputActionAsset>(AssetPath);
            if (asset == null)
            {
                Debug.LogWarning("[ChessFight] Resources/" + AssetPath + " not found. Using the legacy input fallback.");
                return null;
            }
            // Work on a clone: enabling actions on the imported asset leaks state
            // between play sessions in the Editor.
            var clone = Object.Instantiate(asset);
            var map = clone.FindActionMap(MapName, false);
            var move = map?.FindAction("Move", false);
            var jump = map?.FindAction("Jump", false);
            if (move == null || jump == null)
            {
                Debug.LogWarning("[ChessFight] ChessFightControls is missing the " + MapName +
                                 " map with Move and Jump. Using the legacy input fallback.");
                Object.Destroy(clone);
                return null;
            }
            // Optional so an older actions asset without them still loads.
            var shove = map.FindAction("Shove", false);
            var grab = map.FindAction("Grab", false);
            return new InputSystemMoveSource(clone, map, move, jump, shove, grab);
#endif
        }

        InputActionAsset asset;
        InputActionMap map;
        InputAction move, jump, shove, grab;

        InputSystemMoveSource(InputActionAsset asset, InputActionMap map, InputAction move, InputAction jump,
                              InputAction shove, InputAction grab)
        { this.asset = asset; this.map = map; this.move = move; this.jump = jump; this.shove = shove; this.grab = grab; }

        public string DisplayName => "Input System (ChessFightControls)";
        public void Enable() => map?.Enable();

        public void Disable()
        {
            map?.Disable();
            if (asset != null) Object.Destroy(asset);
            asset = null; map = null; move = null; jump = null; shove = null; grab = null;
        }

        public MoveIntent Read()
        {
            if (move == null) return default;
            // triggered is the press edge for a button action, matching the
            // GetKeyDown semantics the network layer expects for Jump.
            return new MoveIntent
            {
                Move = move.ReadValue<Vector2>(), Jump = jump.triggered,
                Shove = shove != null && shove.triggered, Grab = grab != null && grab.IsPressed()
            };
        }
    }
}
#endif
