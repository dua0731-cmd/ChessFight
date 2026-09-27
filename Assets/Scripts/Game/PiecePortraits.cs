using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // Full-length pictures of the piece figures for the HUD (the game-mode
    // cards), rendered once from the same PieceFigure the stages use, so the art
    // always matches the 3D and needs no image assets.
    //
    // The figure is posed far below the plaza, where no scene camera looks,
    // rendered by a throwaway camera onto a transparent texture, then removed.
    // The current scene's lights and ambient colour light it, so call this after
    // the stage is built.
    public static class PiecePortraits
    {
        const int Size = 384;
        static readonly Vector3 Studio = new Vector3(0f, -300f, 0f);
        static readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();

        // `yaw` turns the figure from facing the viewer (positive: towards the
        // viewer's left); `matte` is the colour its
        // soft edges blend towards, best the colour of what it is drawn over.
        public static RenderTexture Get(PieceKind kind, PieceSkin skin, float yaw, Color matte)
        {
            string key = kind + "/" + skin + "/" + yaw;
            if (cache.TryGetValue(key, out var texture) && texture != null && texture.IsCreated()) return texture;

            texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Portrait " + key };
            texture.Create();

            // Faces the camera (-Z), front-lit by the scene's sun like the stage.
            var figure = PieceFigure.Build(kind, skin, null);
            figure.transform.SetPositionAndRotation(Studio, Quaternion.Euler(0f, 180f + yaw, 0f));
            // No shadows: at this scale they only add acne stripes.
            foreach (var part in figure.GetComponentsInChildren<Renderer>())
            {
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                part.receiveShadows = false;
            }

            var rig = new GameObject("Portrait Camera");
            var camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            matte.a = 0f;
            camera.backgroundColor = matte;
            camera.fieldOfView = 22f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 30f;
            camera.allowHDR = false;
            camera.targetTexture = texture;
            rig.transform.position = Studio + new Vector3(0f, 1.3f, -7.4f);
            rig.transform.LookAt(Studio + new Vector3(0f, 1.08f, 0f));
            camera.Render();
            camera.targetTexture = null;

            // Destroy waits for the end of the frame; hide the figure now so the
            // next portrait rendered this frame does not have it in shot.
            figure.SetActive(false);
            Object.Destroy(rig);
            Object.Destroy(figure);
            cache[key] = texture;
            return texture;
        }
    }
}
