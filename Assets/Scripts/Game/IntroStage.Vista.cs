using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Game
{
    // What the great window looks out on (10-17, 승규 님: "노을 빛이랑 구름만 블랜더로"): a sunset sky with
    // soft pink clouds, rendered in Cycles in Blender (ArtSource/Hall/GreatWindow/render_sky.py, real
    // volumetric clouds and the setting sun low on the right of the view, whose light comes in through
    // the window: Lighting) to Resources/Intro/SunsetSky.png and laid on one far card
    // behind the wall. The card is sized and placed so the band of clouds fills the view through the
    // window from the camera at (0, 2.2, 9). It is drawn unlit and before the hall, with no fog.
    public sealed partial class IntroStage
    {
        Material skyMaterial;

        // The wall slab in pieces round the window openings (x centre, half width, bottom, top).
        void WallWithHoles(string name, float z, float depth, float x0, float x1, float y0, float y1, Material material, float[] windowXs,
                           float halfWidth, float bottom, float top)
        {
            var xs = new List<float>(windowXs);
            xs.Sort();
            float from = x0;
            foreach (float wx in xs)
            {
                float a = wx - halfWidth, b = wx + halfWidth;
                Piece(name, from, a, y0, y1);
                Piece(name, a, b, y0, bottom);
                Piece(name, a, b, top, y1);
                from = b;
            }
            Piece(name, from, x1, y0, y1);

            void Piece(string n, float l, float r, float lo, float hi)
            {
                if (r - l < .001f || hi - lo < .001f) return;
                MenuArt.Slab(transform, n, new Vector3((l + r) / 2f, (lo + hi) / 2f, z), new Vector3(r - l, hi - lo, depth), material);
            }
        }

        void Vista()
        {
            var image = Resources.Load<Texture2D>("Intro/SunsetSky");
            var shader = Shader.Find("Sprites/Default");
            if (image == null || shader == null) return;
            skyMaterial = new Material(shader) { name = "Intro sunset sky", mainTexture = image, renderQueue = 1980 };
            // 60 m x 20 m (the image's 3:1) at 57 m behind the wall: the clouds sit in the window's view.
            LastSceneArt.Part(transform, "Sunset Sky", LastSceneArt.Panel(60f, 20f), skyMaterial, new Vector3(-6f, 10f, -62f),
                              Quaternion.Euler(0, 180f, 0), null, false);
        }

        void DisposeVista()
        {
            if (skyMaterial != null) Destroy(skyMaterial);
        }
    }
}
