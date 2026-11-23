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
    //
    // 11-23 (승규 님: "인트로 화면에 구름 실제로 움직이게"): the clouds came out of that image. The card now
    // shows SunsetSkyClear.png, the sky and the sun alone (render_sky.py mode sky; SunsetSky.png stays as
    // it was for the lobby hall's skybox), and the clouds are three transparent
    // layers in front of it (modes far, mid, near), each rendered so it repeats seamlessly left to right.
    // Each layer's card scrolls its picture round at its own pace, far ones slower, all towards screen
    // left like the floor, so the clouds drift past the window in depth. The scroll is a pure function
    // of the clock (no accumulation), so it never drifts out of step or loses precision.
    public sealed partial class IntroStage
    {
        Material skyMaterial;

        // One layer: its image, its pace in card metres a second, its card's distance and its start.
        static readonly (string image, float speed, float z, float phase)[] CloudLayers =
        {
            ("Intro/SunsetCloudsFar", .12f, -61.6f, .11f),
            ("Intro/SunsetCloudsMid", .26f, -61.2f, .47f),
            ("Intro/SunsetCloudsNear", .42f, -60.8f, .83f),
        };
        // A layer's image covers 80 m of card before it repeats; the 60 m card shows three quarters of it.
        const float CloudPeriod = 80f, SkyWidth = 60f, SkyHeight = 20f;

        sealed class CloudLayer
        {
            public Mesh Mesh;
            public Material Material;
            public float Speed, Phase;
            public readonly Vector2[] Uv = new Vector2[4];
        }

        readonly List<CloudLayer> cloudLayers = new List<CloudLayer>();

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
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            // The clear sky under the drifting layers; without it, the old picture with its clouds painted
            // in (still the lobby hall's skybox, LobbyStage.Hall.Sunset) and no layers on top.
            var image = Resources.Load<Texture2D>("Intro/SunsetSkyClear");
            bool drifting = image != null;
            if (!drifting) image = Resources.Load<Texture2D>("Intro/SunsetSky");
            if (image == null) return;
            skyMaterial = new Material(shader) { name = "Intro sunset sky", mainTexture = image, renderQueue = 1980 };
            // 60 m x 20 m (the image's 3:1) at 57 m behind the wall: the clouds sit in the window's view.
            LastSceneArt.Part(transform, "Sunset Sky", LastSceneArt.Panel(SkyWidth, SkyHeight), skyMaterial, new Vector3(-6f, 10f, -62f),
                              Quaternion.Euler(0, 180f, 0), null, false);
            if (drifting) Clouds(shader);
        }

        // The cloud layers, drawn far to near just after the sky and still before the hall.
        void Clouds(Shader shader)
        {
            for (int i = 0; i < CloudLayers.Length; i++)
            {
                var (file, speed, z, phase) = CloudLayers[i];
                var image = Resources.Load<Texture2D>(file);
                if (image == null) continue;
                var layer = new CloudLayer
                {
                    Material = new Material(shader) { name = "Intro clouds " + i, mainTexture = image, renderQueue = 1981 + i },
                    Mesh = new Mesh { name = "Intro clouds " + i },
                    Speed = speed,
                    Phase = phase,
                };
                // One quad the size of the sky card; Sprites/Default draws both faces. Seen from the camera
                // after the half turn, +X is screen right, as on the sky card.
                float w = SkyWidth / 2f, h = SkyHeight / 2f;
                layer.Mesh.vertices = new[] { new Vector3(-w, -h), new Vector3(w, -h), new Vector3(w, h), new Vector3(-w, h) };
                layer.Mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                layer.Mesh.MarkDynamic();
                ScrollCloud(layer, 0f);
                layer.Mesh.RecalculateBounds();
                LastSceneArt.Part(transform, "Sunset Clouds " + i, layer.Mesh, layer.Material, new Vector3(-6f, 10f, z),
                                  Quaternion.Euler(0, 180f, 0), null, false);
                cloudLayers.Add(layer);
            }
        }

        // Slides each layer's picture under its card (the image repeats, so it wraps round unseen).
        // The window moving right over the picture carries the clouds towards screen left.
        void DriftClouds(float t)
        {
            foreach (var layer in cloudLayers) ScrollCloud(layer, t);
        }

        static void ScrollCloud(CloudLayer layer, float t)
        {
            // Repeat on the clock first so a long-running title screen keeps full float precision.
            float cycle = CloudPeriod / layer.Speed;
            float u = layer.Phase + Mathf.Repeat(t, cycle) / cycle, span = SkyWidth / CloudPeriod;
            u -= Mathf.Floor(u);
            layer.Uv[0] = new Vector2(u, 0f);
            layer.Uv[1] = new Vector2(u + span, 0f);
            layer.Uv[2] = new Vector2(u + span, 1f);
            layer.Uv[3] = new Vector2(u, 1f);
            layer.Mesh.uv = layer.Uv;
        }

        void DisposeVista()
        {
            if (skyMaterial != null) Destroy(skyMaterial);
            foreach (var layer in cloudLayers)
            {
                if (layer.Material != null) Destroy(layer.Material);
                if (layer.Mesh != null) Destroy(layer.Mesh);
            }
            cloudLayers.Clear();
        }
    }
}
