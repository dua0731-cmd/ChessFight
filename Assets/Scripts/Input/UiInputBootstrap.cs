#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ChessFight.Game
{
    // With the Input System package active, runtime UI is expected to be driven by
    // an EventSystem whose input module comes from that package. Without one,
    // UI Toolkit falls back to its own default event system, which is the path
    // that stops delivering clicks once Active Input Handling leaves "Input
    // Manager (Old)".
    //
    // Creating it here keeps the requirement next to the Input System code rather
    // than asking every scene to remember it.
    public static class UiInputBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            var system = Object.FindFirstObjectByType<EventSystem>();
            if (system == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
                return;
            }
            if (system.GetComponent<InputSystemUIInputModule>() != null) return;
            // A legacy StandaloneInputModule cannot talk to the Input System, so a
            // scene carrying one would silently swallow every click.
            var legacy = system.GetComponent<BaseInputModule>();
            if (legacy != null) Object.Destroy(legacy);
            system.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
#endif
