using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // The loading screen's two pictures, taken once when it opens: the menu's
    // chess board with six pawns a side on ranks 2 and 7 (dimmed behind the
    // screen), and two pawns squaring up for its middle. Built from the same
    // StageKit and PieceFigure as the title and lobby, far below the plaza where
    // no scene camera looks, rendered by a throwaway camera and removed at once,
    // the way PiecePortraits does. The current scene's sky, haze and sun light
    // them, so take them from the lobby.
    public static class LoadingBackdrop
    {
        const float Tile = 1.5f;
        static readonly Vector3 Studio = new Vector3(0f, -600f, 0f);

        public static RenderTexture Stage(int width, int height)
        {
            var root = new GameObject("Loading Backdrop");
            root.transform.position = Studio;
            StageKit.Floor(root.transform);
            float top = StageKit.Board(root.transform, "Board", 8, 8, Tile, Vector3.zero, .5f, .8f);
            for (int i = 1; i <= 6; i++)
            {
                float x = (i - 3.5f) * Tile;
                Place(root.transform, PieceSkin.White, new Vector3(x, top, -2.5f * Tile), 0f);
                Place(root.transform, PieceSkin.Black, new Vector3(x, top, 2.5f * Tile), 180f);
            }
            // Low over White's right shoulder, like the title screen, a little
            // lower so a band of sky shows above the plaza.
            return Shoot(root, width, height, 32f, new Vector3(15.5f, 4.8f, -9.5f), new Vector3(-1.4f, 1f, -.4f), null);
        }

        // Transparent around the board; `matte` is the colour its soft edges
        // blend towards, best the colour it is drawn over.
        public static RenderTexture Duel(int width, int height, Color matte)
        {
            var root = new GameObject("Loading Duel");
            root.transform.position = Studio;
            float top = StageKit.Board(root.transform, "Board", 4, 2, Tile, Vector3.zero, .4f, .6f);
            // Facing each other, each turned 30 degrees towards the camera.
            Place(root.transform, PieceSkin.White, new Vector3(-1.35f, top, 0f), 120f);
            Place(root.transform, PieceSkin.Black, new Vector3(1.35f, top, 0f), -120f);
            return Shoot(root, width, height, 30f, new Vector3(0f, 2.6f, -10.5f), new Vector3(0f, 1.15f, 0f), matte);
        }

        static void Place(Transform parent, PieceSkin skin, Vector3 at, float yaw)
        {
            var figure = PieceFigure.Build(PieceKind.Pawn, skin, parent);
            figure.transform.localPosition = at;
            figure.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // No matte: the scene's sky behind. With one: a transparent background.
        static RenderTexture Shoot(GameObject root, int width, int height, float fov, Vector3 at, Vector3 look, Color? matte)
        {
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = root.name };
            texture.Create();

            var rig = new GameObject(root.name + " Camera");
            var camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            if (matte.HasValue)
            {
                var clear = matte.Value;
                clear.a = 0f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = clear;
            }
            else if (RenderSettings.skybox != null) camera.clearFlags = CameraClearFlags.Skybox;
            else
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = PieceFigure.Hex(0xDCEBF9);
            }
            camera.fieldOfView = fov;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 400f;
            camera.allowHDR = false;
            camera.targetTexture = texture;
            rig.transform.position = Studio + at;
            rig.transform.LookAt(Studio + look);
            camera.Render();
            camera.targetTexture = null;

            // Destroy waits for the end of the frame; hide the set now so nothing
            // else rendered this frame has it in shot.
            root.SetActive(false);
            Object.Destroy(rig);
            Object.Destroy(root);
            return texture;
        }
    }
}
