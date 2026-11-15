// Batch check of the baked piece models (bishop, king, queen; rook and knight for reference): each menu prefab
// (the pawn's skeleton wearing the piece, baked by MenuPieceBaker) is put into an empty scene and rendered from four
// sides in the white and the black team's material, then once in a test pose (spine bent, head turned, arms raised)
// to show the skin weights. Lives only in the batch copy of the project (Assets/Editor), not in the repository.
//   Unity -batchmode -projectPath <copy> -executeMethod PieceCheckRender.Run -quit   (no -nographics: it renders)
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PieceCheckRender
{
    const int W = 520, H = 860;

    public static void Run()
    {
        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "CheckRender");
        Directory.CreateDirectory(outDir);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.60f, 0.58f, 0.56f);
        var light = new GameObject("Key").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.93f, 0.93f, 0.95f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 60f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.aspect = (float)W / H;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

        foreach (string piece in new[] { "Bishop", "King", "Queen", "Rook", "Knight" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MenuPieces/" + piece + ".prefab");
            if (prefab == null) { Debug.Log("[Check] no menu prefab for " + piece); continue; }
            foreach (string team in new[] { "white", "black" })
            {
                var go = (GameObject)Object.Instantiate(prefab);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skin == null) { Debug.Log("[Check] no skin in " + piece); Object.DestroyImmediate(go); continue; }
                skin.updateWhenOffscreen = true;
                if (team == "black")
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/PieceModels/Black/" + piece + ".mat");
                    if (mat != null) skin.sharedMaterial = mat;
                }
                Bounds b = Measure(skin);
                Debug.Log(string.Format("[Check] {0} {1}: {2} vertices, shader {3}, bounds min {4} max {5}", piece, team,
                    skin.sharedMesh.vertexCount, skin.sharedMaterial != null ? skin.sharedMaterial.shader.name : "none",
                    b.min.ToString("F3"), b.max.ToString("F3")));
                foreach (int yaw in new[] { 0, 90, 180, 270 })
                    Shot(cam, light, rt, tex, b, yaw, Path.Combine(outDir, piece + "_" + team + "_" + yaw + ".png"));
                if (team == "white")
                {
                    Turn(go.transform, "mixamorig:Spine", new Vector3(18f, 0f, 0f));
                    Turn(go.transform, "mixamorig:Spine2", new Vector3(14f, 0f, 8f));
                    Turn(go.transform, "mixamorig:Head", new Vector3(-10f, 30f, 0f));
                    Turn(go.transform, "mixamorig:LeftArm", new Vector3(0f, 0f, -45f));
                    Turn(go.transform, "mixamorig:RightArm", new Vector3(0f, 30f, 35f));
                    Turn(go.transform, "mixamorig:LeftUpLeg", new Vector3(-25f, 0f, 0f));
                    Bounds pb = Measure(skin);
                    pb.Encapsulate(b);
                    foreach (int yaw in new[] { 0, 90, 180, 270 })
                        Shot(cam, light, rt, tex, pb, yaw, Path.Combine(outDir, piece + "_pose_" + yaw + ".png"));
                }
                Object.DestroyImmediate(go);
            }
        }
        Debug.Log("[Check] done: " + outDir);
    }

    static Bounds Measure(SkinnedMeshRenderer skin)
    {
        var baked = new Mesh();
        skin.BakeMesh(baked, true);
        var vs = baked.vertices;
        var b = new Bounds(skin.transform.TransformPoint(vs[0]), Vector3.zero);
        foreach (var v in vs) b.Encapsulate(skin.transform.TransformPoint(v));
        Object.DestroyImmediate(baked);
        return b;
    }

    static void Turn(Transform root, string bone, Vector3 euler)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>())
            if (t.name == bone) { t.localRotation = t.localRotation * Quaternion.Euler(euler); return; }
    }

    static void Shot(Camera cam, Light light, RenderTexture rt, Texture2D tex, Bounds b, int yaw, string path)
    {
        Vector3 target = b.center;
        Quaternion rot = Quaternion.Euler(4f, yaw, 0f);
        cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * 20f, rot);
        cam.orthographicSize = Mathf.Max(b.size.y * 0.54f, Mathf.Max(b.size.x, b.size.z) * 0.54f * H / W);
        light.transform.rotation = Quaternion.Euler(38f, yaw + 28f, 0f);
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
    }
}
