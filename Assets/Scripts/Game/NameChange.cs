using System;
using UnityEngine;

namespace ChessFight.Game
{
    // Changing the name at any time (R85, Docs/Architecture/UI.md §15), from the
    // title screen's "이름 바꾸기" or the lobby's own-name button top right: the
    // board's king hole closes on the button, the name screen comes up renaming
    // under the "이름 바꾸기" card, and "이 이름으로 바꾸기" or "돌아가기" (Esc)
    // closes on its pawn and opens again where the player came from. That scene
    // stays loaded underneath all along, so it already shows the new name when
    // the board opens.
    public static class NameChange
    {
        static NameScreen screen;

        public static bool Showing => screen != null;

        // `from` and `backTo()` are fractions of the screen (0..1 from the top
        // left); `backTitle` names the screen the card says it goes back to.
        public static void Begin(Vector2 from, Func<Vector2> backTo, string backTitle)
        {
            if (screen != null || NameScreen.Showing || SceneTransition.Busy) return;
            SceneTransition.Run("이름 바꾸기", from,
                () =>
                {
                    screen = new GameObject("Name Screen").AddComponent<NameScreen>();
                    screen.Build(PlayerProfile.Name, true);
                    var opened = screen;
                    opened.Confirmed += name => End(opened, name, backTo, backTitle);
                    opened.Cancelled += () => End(opened, null, backTo, backTitle);
                },
                // A screen dropped meanwhile (Abort) opens on the lobby at once.
                () => screen == null || screen.Ready,
                () => screen != null ? screen.PawnAt : new Vector2(.5f, .5f));
        }

        // Matching took the lobby over: drop the screen without a transition (the
        // entrance screen covers the lobby anyway) and keep the name.
        public static void Abort()
        {
            if (screen != null) UnityEngine.Object.Destroy(screen.gameObject);
            screen = null;
        }

        // `name` is null when the player went back without a change.
        static void End(NameScreen leaving, string name, Func<Vector2> backTo, string backTitle)
        {
            if (leaving == null || leaving != screen) return;
            SceneTransition.Run(backTitle, leaving.PawnAt,
                () =>
                {
                    if (name != null) PlayerProfile.Set(name);
                    if (leaving != null) UnityEngine.Object.Destroy(leaving.gameObject);
                    if (screen == leaving) screen = null;
                },
                null,
                () =>
                {
                    try { return backTo != null ? backTo() : new Vector2(.5f, .62f); }
                    catch (Exception e) { Debug.LogException(e); return new Vector2(.5f, .62f); }
                });
        }
    }
}
