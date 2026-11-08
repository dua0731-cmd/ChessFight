using UnityEngine;

namespace ChessFight.Game
{
    // The great window above the banners (10-17, 승규 님: the light rays looked wrong and cut across the
    // banners and the wall; "깃발 위에 큰 창을 하나 따로 넣어줘 노을 하늘이 좌악 들어 올 수 있게"). It
    // replaces the three narrow lancets and their light cards: one wide opening across the upper wall,
    // divided by stone mullions and a transom, on a deep sill, open to the sunset sky world beyond
    // (IntroStage.Vista.cs). The light that comes in is real: the low sunset sun stands behind the wall
    // (Lighting) and shines only through this opening, so its mullions lay warm bars of light across the
    // board and the runners pass in and out of them. No shafts or glow cards.
    public sealed partial class IntroStage
    {
        // The opening: centred over the banners, from just above the rod to above the top of the frame.
        const float GreatLeft = 1.5f, GreatRight = -5.4f, GreatBottom = 3.0f, GreatTop = 5.8f;
        static float GreatCentre => (GreatLeft + GreatRight) / 2f;
        static float GreatHalfWidth => (GreatLeft - GreatRight) / 2f;

        void GreatWindow()
        {
            // The window modelled in Blender (ArtSource/Hall/GreatWindow, built by build_window.py and baked):
            // a moulded surround, a sill on a corbel table with a gold edge, octagonal mullions on moulded
            // bases, a row of short lights, a transom with gold rosettes and a row of pointed gothic lights
            // with round openings between them. Its origin is the bottom centre of the opening on the wall
            // face; half a turn puts Blender's +Y (towards the camera) on Unity's +Z.
            var model = Resources.Load<GameObject>("Hall/GreatWindow/GreatWindow");
            if (model != null)
            {
                var window = Instantiate(model, transform);
                window.name = "Great Window";
                window.transform.localPosition = new Vector3(GreatCentre, GreatBottom, WallZ);
                window.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                foreach (var r in window.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
                return;
            }
            // Without the model: plain stone jambs, sill, mullions and a transom.
            var stone = LastSceneArt.Lit(0xB8A27C, .2f);
            var dark = LastSceneArt.Lit(0x5B4632, .12f);
            float cx = GreatCentre, half = GreatHalfWidth, height = GreatTop - GreatBottom;
            const float depth = .5f;
            float z = WallZ - .25f;
            // Jambs either side, a deep sill with a brass edge, and the reveal under the head in shadow.
            foreach (float s in new[] { -1f, 1f })
                MenuArt.Slab(transform, "Great Window Jamb", new Vector3(cx + s * (half + .14f), GreatBottom + height / 2f, z + .05f),
                             new Vector3(.32f, height, depth + .2f), stone);
            MenuArt.Slab(transform, "Great Window Sill", new Vector3(cx, GreatBottom - .09f, z + .16f), new Vector3(half * 2f + .7f, .18f, depth + .42f), stone);
            MenuArt.Slab(transform, "Great Window Sill Trim", new Vector3(cx, GreatBottom - .19f, z + .38f), new Vector3(half * 2f + .7f, .035f, .04f), MenuArt.BrassMetal());
            MenuArt.Slab(transform, "Great Window Reveal", new Vector3(cx, GreatBottom + .01f, z - .05f), new Vector3(half * 2f, .02f, depth - .1f), dark);
            // Mullions every 1.15 m and a transom: the bars of light on the floor come from these.
            int bays = Mathf.RoundToInt(half * 2f / 1.15f);
            for (int i = 1; i < bays; i++)
            {
                float x = cx - half + i * (half * 2f / bays);
                MenuArt.Slab(transform, "Great Window Mullion", new Vector3(x, GreatBottom + height / 2f, z), new Vector3(.13f, height, .34f), stone);
            }
            MenuArt.Slab(transform, "Great Window Transom", new Vector3(cx, 4.35f, z), new Vector3(half * 2f, .12f, .3f), stone);
        }
    }
}
