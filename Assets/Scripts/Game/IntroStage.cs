using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The title screen's backdrop (R150, 2026-10-16): a castle hall with a stone
    // wall, rook battlements, tall checkered windows and four pennants (queen,
    // knight, rook, bishop) on an iron rod, over a walnut and maple chessboard
    // floor that runs past like a race track. The match's own piece models race
    // to the right, shoving and grabbing each other on the way. The left of the
    // frame stays calm for the title block (IntroHud.uxml).
    //
    // It answers the pointer: a pennant the mouse passes over catches a gust,
    // a click on a piece makes it stumble and the camera leans a little after
    // the mouse. The race keeps one steady pace (승규 님: no speed-up over the
    // start plate). Nothing here takes clicks away from the HUD.
    //
    // Coordinates: the camera stands on +Z looking down -Z, so screen right is
    // world -X. Sx turns a "to the right of the screen" distance into world X.
    public sealed partial class IntroStage : MonoBehaviour
    {
        static readonly Vector3 CameraAt = new Vector3(0f, 2.2f, 9f);
        const float CameraPitch = 6f, CameraFov = 30f;
        const float WallZ = -5f;
        const float BaseSpeed = 4f;          // floor speed in m/s
        const float Square = 1f;             // one square of the floor board

        static float Sx(float right) => -right;

        const int DashCount = 16;
        const float DashLength = 1.4f, DashGap = 3f;

        Camera view;
        Material floorMaterial, laneMaterial;
        readonly List<Transform> dashes = new List<Transform>();
        float travelled;
        readonly List<Pennant> pennants = new List<Pennant>();
        readonly List<Runner> runners = new List<Runner>();
        readonly List<Shove> shoves = new List<Shove>();
        readonly List<Puff> puffs = new List<Puff>();
        Material puffMaterial, streakMaterial;
        float nextPuff;

        public void Build(Camera camera)
        {
            view = camera;
            if (view == null)
            {
                var go = new GameObject("ChessFight Camera") { tag = "MainCamera" };
                view = go.AddComponent<Camera>();
            }
            view.fieldOfView = CameraFov;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = MenuArt.Hex(0x1A120C);
            view.farClipPlane = 80f;
            view.allowHDR = false;
            Lighting();
            Wall();
            Floor();
            Pillars();
            Racers();
            Effects();
            Aim(Vector2.zero);
        }

        // ---------- the hall ----------

        void Lighting()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = MenuArt.Hex(0x22160E);
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 40f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = MenuArt.Hex(0x9A7A5A) * .9f;
            RenderSettings.ambientEquatorColor = MenuArt.Hex(0x5A4030) * .75f;
            RenderSettings.ambientGroundColor = MenuArt.Hex(0x1E140C);
            RenderSettings.reflectionIntensity = .5f;
            // Scenes may carry a sun of their own; this hall is lit by its own lights.
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.transform.parent == null) light.enabled = false;

            var key = new GameObject("Key Light").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.transform.rotation = Quaternion.LookRotation(new Vector3(-.35f, -.75f, -.55f));
            key.type = LightType.Directional;
            key.color = MenuArt.Hex(0xFFE2B8);
            key.intensity = 1.3f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .6f;
            key.shadowBias = .03f;
            key.shadowNormalBias = .3f;
            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.transform.rotation = Quaternion.LookRotation(new Vector3(.5f, -.3f, -.8f));
            fill.type = LightType.Directional;
            fill.color = MenuArt.Hex(0xC9B8FF) * .8f;
            fill.intensity = .25f;
            // A soft white light from the camera so the ivory pieces read white, not grey.
            var front = new GameObject("Front Light").AddComponent<Light>();
            front.transform.SetParent(transform, false);
            front.transform.rotation = Quaternion.LookRotation(new Vector3(.15f, -.35f, -1f));
            front.type = LightType.Directional;
            front.color = MenuArt.Hex(0xFFF4E6);
            front.intensity = .45f;
        }

        // Stone wall, gold rail and dark plinth, battlements, three lancet windows
        // with sunset glass between the pennants, the iron rod and the pennants.
        void Wall()
        {
            var stone = LastSceneArt.Textured("intro stone", StoneTexture(), .12f, new Vector2(6f, 3f));
            MenuArt.Slab(transform, "Wall", new Vector3(0, 4.5f, WallZ - .25f), new Vector3(30f, 9f, .5f), stone);
            MenuArt.Slab(transform, "Plinth", new Vector3(0, .3f, WallZ + .05f), new Vector3(30f, .6f, .3f), LastSceneArt.Lit(0x1E150E, .3f));
            MenuArt.Slab(transform, "Rail", new Vector3(0, .62f, WallZ + .12f), new Vector3(30f, .07f, .12f), MenuArt.BrassMetal());
            // The battlements along the top of the wall.
            var dark = LastSceneArt.Lit(0x2A1E15, .15f);
            MenuArt.Slab(transform, "Battlement Band", new Vector3(0, 4.55f, WallZ + .1f), new Vector3(30f, .25f, .4f), dark);
            for (float x = -15f; x <= 15f; x += .9f)
                MenuArt.Slab(transform, "Merlon", new Vector3(x, 4.85f, WallZ + .1f), new Vector3(.45f, .4f, .4f), dark);
            MenuArt.Slab(transform, "Battlement Trim", new Vector3(0, 4.43f, WallZ + .32f), new Vector3(30f, .04f, .04f), MenuArt.BrassMetal());

            var glass = MenuArt.Glow("intro window", WindowTexture(), Color.white);
            glass.renderQueue = (int)RenderQueue.Geometry + 1;
            foreach (float right in new[] { .1f, 1.99f, 3.87f })
            {
                float x = Sx(right);
                LastSceneArt.Part(transform, "Window", LastSceneArt.Panel(.5f, 2.45f), glass, new Vector3(x, 2.07f, WallZ + .02f),
                                  Quaternion.Euler(0, 180f, 0), null, false);
                MenuArt.Slab(transform, "Sill", new Vector3(x, .82f, WallZ + .1f), new Vector3(.75f, .1f, .25f), dark);
                var glow = new GameObject("Window Light").AddComponent<Light>();
                glow.transform.SetParent(transform, false);
                glow.transform.localPosition = new Vector3(x, 2.1f, WallZ + 1.2f);
                glow.type = LightType.Point;
                glow.color = MenuArt.Hex(0xFFB066);
                glow.intensity = 1.1f;
                glow.range = 4.5f;
            }

            var iron = LastSceneArt.Lit(0x3A3530, .5f, .6f);
            LastSceneArt.Part(transform, "Rod", LastSceneArt.Cylinder(.035f, .035f, 7.6f, 12), iron,
                              new Vector3(Sx(1.95f), 4.02f, WallZ + .35f), Quaternion.Euler(0, 0, 90f), null, true);
            var glyphs = new[] { "♕", "♘", "♖", "♗" };
            var colors = new[] { 0x7A1F24, 0x1C1612, 0x7A1F24, 0x1C1612 };
            float[] at = { -.84f, 1.05f, 2.93f, 4.82f };
            for (int i = 0; i < 4; i++)
                pennants.Add(new Pennant(transform, new Vector3(Sx(at[i]), 3.98f, WallZ + .38f), PennantTexture(glyphs[i], colors[i]), i));
        }

        // The race track: the board floor scrolls under the runners; faint dashed
        // lanes run along it like a road's markings.
        void Floor()
        {
            var board = Instantiate(MenuArt.BoardTexture(2));
            board.wrapMode = TextureWrapMode.Repeat;
            floorMaterial = new Material(PieceFigure.Lit(Color.white, .5f, .05f)) { name = "Intro track", mainTexture = board };
            const float width = 44f, depth = 18f;
            floorMaterial.mainTextureScale = new Vector2(width / (2 * Square), depth / (2 * Square));
            var floor = LastSceneArt.Part(transform, "Track", LastSceneArt.Panel(width, depth), floorMaterial,
                                          new Vector3(0, 0, WallZ + depth / 2f), Quaternion.Euler(90f, 0, 0), null, false);
            floor.GetComponent<MeshRenderer>().receiveShadows = true;

            // Dashes rather than a scrolled texture: the blended shader ignores tiling.
            laneMaterial = MenuArt.Glow("intro lane", null, new Color(1f, .97f, .9f, .3f), false);
            foreach (float z in new[] { -3.5f, -1.9f, 1.1f })
                for (int i = 0; i < DashCount; i++)
                    dashes.Add(LastSceneArt.Part(transform, "Lane Dash", LastSceneArt.Panel(DashLength, .07f), laneMaterial,
                                                 new Vector3(0, .012f, z), Quaternion.Euler(90f, 0, 0), null, false).transform);
            // Darker towards the wall so the floor meets it softly.
            var shade = MenuArt.Glow("intro floor shade", MenuArt.VerticalFade(new Color(0, 0, 0, 0), new Color(.07f, .04f, .02f, .75f)), Color.white);
            LastSceneArt.Part(transform, "Floor Shade", LastSceneArt.Panel(width, 3f), shade,
                              new Vector3(0, .015f, WallZ + 1.5f), Quaternion.Euler(90f, 0, 0), null, false);
        }

        // Ivory columns framing the hall at both edges of the screen.
        void Pillars()
        {
            var ivory = LastSceneArt.Lit(0xEFE4CC, .35f);
            foreach (float right in new[] { -4.9f, 4.9f })
            {
                var x = Sx(right);
                LastSceneArt.Part(transform, "Pillar", LastSceneArt.Cylinder(.36f, .36f, 9f, 28), ivory, new Vector3(x, 4.5f, -.6f), null, null, true);
                MenuArt.Slab(transform, "Pillar Base", new Vector3(x, .15f, -.6f), new Vector3(.95f, .3f, .95f), ivory);
            }
        }

        // ---------- the camera ----------

        Vector2 lean;

        void Aim(Vector2 pointer)
        {
            if (view == null) return;
            lean = Vector2.Lerp(lean, pointer, .06f);
            // Small, so the frame stays the designed one wherever the mouse rests.
            view.transform.position = CameraAt + new Vector3(-lean.x * .12f, lean.y * .06f, 0);
            view.transform.rotation = Quaternion.Euler(CameraPitch - lean.y * .3f, 180f + lean.x * .35f, 0);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f), t = Time.unscaledTime;
            const float speed = BaseSpeed;
            travelled += speed * dt;

            Vector2 mouse = Pointer(out bool clicked);
            Vector2 centred = new Vector2(mouse.x / Mathf.Max(1, Screen.width) * 2f - 1f, mouse.y / Mathf.Max(1, Screen.height) * 2f - 1f);
            Aim(new Vector2(Mathf.Clamp(centred.x, -1, 1), Mathf.Clamp(centred.y, -1, 1)));

            // The floor and its lanes move towards screen left (+X) under the runners.
            if (floorMaterial != null) floorMaterial.mainTextureOffset = new Vector2(-travelled / (2 * Square), 0);
            for (int i = 0; i < dashes.Count; i++)
            {
                var d = dashes[i];
                float x = Mathf.Repeat(i % DashCount * DashGap + travelled, DashCount * DashGap) - DashCount * DashGap / 2f;
                d.localPosition = new Vector3(x, d.localPosition.y, d.localPosition.z);
            }

            foreach (var p in pennants) p.Tick(t, dt, view, mouse);
            foreach (var r in runners) r.Tick(t, dt, 1f);
            foreach (var s in shoves) s.Tick(t, dt, 1f);
            if (clicked) Poke(mouse);
            Dust(t, dt, speed);
        }

        // The mouse in screen pixels, and whether the left button went down this frame.
        static Vector2 Pointer(out bool clicked)
        {
            clicked = false;
            try
            {
                clicked = Input.GetMouseButtonDown(0);
                return Input.mousePosition;
            }
            catch (System.InvalidOperationException) { return new Vector2(Screen.width * .5f, Screen.height * .5f); }
        }

        // A click near a piece makes it stumble.
        void Poke(Vector2 mouse)
        {
            if (view == null) return;
            Figure best = null;
            float bestDistance = 70f * Screen.height / 720f;
            foreach (var f in AllFigures())
            {
                var p = view.WorldToScreenPoint(f.Body.Position + Vector3.up * .8f);
                if (p.z <= 0) continue;
                float d = Vector2.Distance(mouse, p);
                if (d < bestDistance) { bestDistance = d; best = f; }
            }
            best?.Stumble();
        }

        IEnumerable<Figure> AllFigures()
        {
            foreach (var r in runners) yield return r.Body;
            foreach (var s in shoves) { yield return s.Pusher; yield return s.Pushed; }
        }

        void OnDestroy()
        {
            foreach (var p in pennants) p.Dispose();
            if (floorMaterial != null) Destroy(floorMaterial);
            if (laneMaterial != null) Destroy(laneMaterial);
            if (puffMaterial != null) Destroy(puffMaterial);
            if (streakMaterial != null) Destroy(streakMaterial);
        }
    }
}
