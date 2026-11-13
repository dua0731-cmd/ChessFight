using UnityEngine;

namespace ChessFight.Game
{
    // The polished board (10-17, 승규 님: "바닥도 이런 광택나는 디자인으로"): a second camera stands mirrored
    // under the floor and renders the hall into a texture at half size; a sheet just over the board
    // (Resources/Intro/FloorReflection.shader) lays that picture on it, so the wall, the pennants and the
    // racing pieces shine back from the squares. The floor's own parts sit on FloorLayer, which the mirror
    // does not see, so it never draws the board from underneath.
    public sealed partial class IntroStage
    {
        const int FloorLayer = 4;   // "Water" in the tag manager: unused elsewhere
        Camera mirror;
        RenderTexture mirrorTexture;
        Material glossMaterial;

        void Gloss(float width, float depth, Vector3 centre)
        {
            var shader = Shader.Find("ChessFight/Floor Reflection");
            if (shader == null || view == null) return;
            glossMaterial = new Material(shader) { name = "Intro floor gloss" };
            var sheet = LastSceneArt.Part(transform, "Floor Gloss", LastSceneArt.Panel(width, depth), glossMaterial,
                                          centre + new Vector3(0, .004f, 0), Quaternion.Euler(90f, 0, 0), null, false);
            sheet.layer = FloorLayer;

            var go = new GameObject("Floor Mirror");
            go.transform.SetParent(transform, false);
            mirror = go.AddComponent<Camera>();
            mirror.clearFlags = CameraClearFlags.SolidColor;
            mirror.backgroundColor = view.backgroundColor;
            mirror.cullingMask = view.cullingMask & ~(1 << FloorLayer) & ~(1 << 5);
            mirror.depth = view.depth - 1;
            mirror.allowHDR = false;
            mirror.allowMSAA = false;
            Mirror();
        }

        // Called after Aim each frame: the mirror copies the view's lens and stands reflected in the floor.
        void Mirror()
        {
            if (mirror == null || view == null) return;
            int w = Mathf.Max(64, Screen.width / 2), h = Mathf.Max(64, Screen.height / 2);
            if (mirrorTexture == null || mirrorTexture.width != w || mirrorTexture.height != h)
            {
                if (mirrorTexture != null) { mirror.targetTexture = null; mirrorTexture.Release(); Destroy(mirrorTexture); }
                mirrorTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "Intro floor mirror" };
                mirror.targetTexture = mirrorTexture;
                glossMaterial.SetTexture("_ReflTex", mirrorTexture);
            }
            mirror.fieldOfView = view.fieldOfView;
            mirror.nearClipPlane = view.nearClipPlane;
            mirror.farClipPlane = view.farClipPlane;
            var at = view.transform.position;
            var forward = view.transform.forward;
            mirror.transform.SetPositionAndRotation(new Vector3(at.x, -at.y, at.z),
                                                    Quaternion.LookRotation(new Vector3(forward.x, -forward.y, forward.z), Vector3.up));
        }

        void DisposeGloss()
        {
            if (mirror != null) mirror.targetTexture = null;
            if (mirrorTexture != null) { mirrorTexture.Release(); Destroy(mirrorTexture); }
            if (glossMaterial != null) Destroy(glossMaterial);
        }
    }
}
