using System;
using UnityEngine;

namespace ChessFight.Game
{
    // What the local player is asking for this frame, before the network layer
    // turns it into an authoritative MoveInput packet.
    public struct MoveIntent
    {
        public Vector2 Move;
        public bool Jump;   // Edge triggered: true on the frame the key went down.
    }

    public interface IMoveInputSource
    {
        string DisplayName { get; }
        void Enable();
        void Disable();
        MoveIntent Read();
    }

    // The Input System package is optional, so its source registers itself here
    // instead of being referenced directly. Without the package the legacy
    // fallback keeps WASD working and the project still compiles.
    public static class MoveInputSources
    {
        static Func<IMoveInputSource> factory;
        public static void Register(Func<IMoveInputSource> create) => factory = create;
        public static IMoveInputSource Create()
        {
            if (factory != null)
            {
                var source = factory();
                if (source != null) return source;
            }
            return new LegacyMoveInputSource();
        }
    }

    // Currently the active path, not just a fallback: Active Input Handling is
    // "Input Manager (Old)" because enabling the Input System backend leaves
    // UI Toolkit's runtime panel without pointer or keyboard events in a project
    // that has no uGUI. See InputSystemMoveSource for the way back.
    public sealed class LegacyMoveInputSource : IMoveInputSource
    {
        bool unavailable;
        public string DisplayName => "레거시 입력 (Input Manager)";
        public void Enable() { }
        public void Disable() { }
        public MoveIntent Read()
        {
            if (unavailable) return default;
            try
            {
                return new MoveIntent
                {
                    Move = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                                       (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)),
                    Jump = Input.GetKeyDown(KeyCode.Space)
                };
            }
            catch (InvalidOperationException)
            {
                // Active Input Handling is set to "Input System Package (New)" while
                // the package is missing. Say so once instead of throwing per frame.
                unavailable = true;
                Debug.LogError("[ChessFight] No input backend. Install com.unity.inputsystem " +
                               "(ChessFight > Setup > Install dependencies) or set Active Input Handling to Both.");
                return default;
            }
        }
    }
}
