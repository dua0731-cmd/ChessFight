using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // R530 (user 11-09 build test 1: "참가자 PC 달리기 점프는 서 있다가 점프하거나 이동 중에 점프하면 되는데 달리기 하면서
    // 점프하면 안 돼. 즉 키가 3개 이상이 입력되면 잘 안먹히는 것 같아"): the IME (the Korean input method) is off while the game
    // reads keys, and on only while a text field has the keyboard (the chat, the name, the room code).
    //
    // Unity leaves the IME to Windows by default (IMECompositionMode.Auto), and Windows' Korean IME is often set to switch
    // 한/영 on Shift + Space. With the IME on for the game's window that switch took Shift (sprint) + Space (jump): the IME
    // kept the Space and the game never saw the jump, while a jump standing or walking (Space, W + Space) went through. The
    // keyboard was blamed ("3 keys"); a PC with 한/영 on its own key had no trouble. Off, the IME passes every key on.
    public sealed class ImeGuard : MonoBehaviour
    {
        static readonly List<TextField> fields = new List<TextField>();
        static ImeGuard instance;

        // The text fields that need the IME while they have the keyboard (Korean in the chat and the name). A field off its
        // panel (a screen hidden for now, or one left behind) simply does not count; only a long list is swept of them.
        public static void Watch(TextField field)
        {
            if (field == null || fields.Contains(field)) return;
            if (fields.Count >= 64) fields.RemoveAll(f => f == null || f.panel == null);
            fields.Add(field);
        }

        // Someone is typing in a watched field now.
        public static bool Typing
        {
            get
            {
                for (int i = 0; i < fields.Count; i++)
                    if (Focused(fields[i])) return true;
                return false;
            }
        }

        static bool Focused(TextField field)
        {
            if (field == null || field.panel == null) return false;
            var focused = field.panel.focusController?.focusedElement as VisualElement;
            return focused != null && (focused == field || field.Contains(focused));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin()
        {
            fields.Clear();
            if (instance != null) return;
            var go = new GameObject("IME guard") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ImeGuard>();
            Input.imeCompositionMode = IMECompositionMode.Off;
        }

        // Every frame, after the UI has moved the keyboard.
        void LateUpdate()
        {
            var want = Typing ? IMECompositionMode.On : IMECompositionMode.Off;
            if (Input.imeCompositionMode != want) Input.imeCompositionMode = want;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
