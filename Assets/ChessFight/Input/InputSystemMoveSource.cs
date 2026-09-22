#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChessFight.Game
{
    // Unity 6's recommended input path, reading the ChessFightControls asset.
    //
    // It sits in Assembly-CSharp behind Unity's own ENABLE_INPUT_SYSTEM define
    // rather than in an assembly definition, because an .asmdef that references
    // com.unity.inputsystem stops compiling when the package is absent. This way
    // the project builds before the package is installed, and the legacy source
    // in ChessFight.Game covers that window.
    //
    // It registers itself so GameBootstrap never has to know the Input System exists.
    public sealed class InputSystemMoveSource : IMoveInputSource
    {
        const string AssetPath = "ChessFight/ChessFightControls";
        const string MapName = "Gameplay";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => MoveInputSources.Register(Create);

        static IMoveInputSource Create()
        {
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
            return new InputSystemMoveSource(clone, map, move, jump);
        }

        InputActionAsset asset;
        InputActionMap map;
        InputAction move, jump;

        InputSystemMoveSource(InputActionAsset asset, InputActionMap map, InputAction move, InputAction jump)
        { this.asset = asset; this.map = map; this.move = move; this.jump = jump; }

        public string DisplayName => "Input System (ChessFightControls/Gameplay)";
        public void Enable() => map?.Enable();

        public void Disable()
        {
            map?.Disable();
            if (asset != null) Object.Destroy(asset);
            asset = null; map = null; move = null; jump = null;
        }

        public MoveIntent Read()
        {
            if (move == null) return default;
            // triggered is the press edge for a button action, matching the
            // GetKeyDown semantics the network layer expects for Jump.
            return new MoveIntent { Move = move.ReadValue<Vector2>(), Jump = jump.triggered };
        }
    }
}
#endif
