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
        const float RodHeight = 2.62f, PennantScale = .74f;
        const float BaseSpeed = 2.5f;        // floor speed in m/s (4 until 승규 님 asked for slower)
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
            RenderSettings.ambientSkyColor = MenuArt.Hex(0x9A7A5A) * .75f;
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
            // 10-17: the sunset behind the wall, low and warm, the main (shadow-casting) light: the wall
            // keeps it out except through the great window, whose mullions lay bars of light on the board.
            var sunset = new GameObject("Sunset Light").AddComponent<Light>();
            sunset.transform.SetParent(transform, false);
            // From the sun low on the right of the window (the sky image), steep enough to reach the board.
            sunset.transform.rotation = Quaternion.LookRotation(new Vector3(.34f, -.5f, .8f));
            sunset.type = LightType.Directional;
            sunset.color = MenuArt.Hex(0xFF9C58);
            sunset.intensity = 3.6f;
            sunset.shadows = LightShadows.Soft;
            sunset.shadowStrength = 1f;
            sunset.shadowBias = .03f;
            sunset.shadowNormalBias = .3f;
            RenderSettings.sun = sunset;
            key.intensity = .62f;
            key.shadows = LightShadows.None;
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
            front.intensity = .38f;
        }

        // Stone wall, gold rail and dark plinth, battlements, three lancet windows
        // with sunset glass between the pennants, the iron rod and the pennants.
        void Wall()
        {
            var stone = StoneMaterial(new Vector2(6f, 3f));
            // 10-17: the wall in pieces round the great window (IntroStage.Window.cs), the sky world
            // beyond it (IntroStage.Vista.cs); real blocks over the part the camera sees
            // (IntroStage.Pier.cs) on a mortar-coloured backing.
            var opening = new[] { GreatCentre };
            WallWithHoles("Wall", WallZ - .25f, .5f, -15f, 15f, 0f, 9f, stone, opening, GreatHalfWidth, GreatBottom, GreatTop);
            WallWithHoles("Wall Mortar", WallZ + .006f, .01f, -8.5f, 8.5f, .65f, 4.45f, LastSceneArt.Lit(0x6E5A42, .05f), opening,
                          GreatHalfWidth, GreatBottom, GreatTop);
            WallBlocks(WallZ);
            GreatWindow();
            Vista();
            // 10-17: the lobby castle's cream stone (was dark brown): a heavier plinth course.
            MenuArt.Slab(transform, "Plinth", new Vector3(0, .3f, WallZ + .05f), new Vector3(30f, .6f, .3f), LastSceneArt.Lit(0xC9B48C, .25f));
            MenuArt.Slab(transform, "Rail", new Vector3(0, .62f, WallZ + .12f), new Vector3(30f, .07f, .12f), MenuArt.BrassMetal());

            var iron = LastSceneArt.Lit(0x3A3530, .5f, .6f);
            LastSceneArt.Part(transform, "Rod", LastSceneArt.Cylinder(.035f, .035f, 7.6f, 12), iron,
                              new Vector3(Sx(1.95f), RodHeight + .04f, WallZ + .35f), Quaternion.Euler(0, 0, 90f), null, true);
            var glyphs = new[] { "♕", "♘", "♖", "♗" };
            var colors = new[] { 0x7A1F24, 0x1C1612, 0x7A1F24, 0x1C1612 };
            float[] at = { -.84f, 1.05f, 2.93f, 4.82f };
            for (int i = 0; i < 4; i++)
            {
                // 10-17: hung lower and a little smaller, under the great window's sill.
                var pennant = new Pennant(transform, new Vector3(Sx(at[i]), RodHeight, WallZ + .38f), PennantTexture(glyphs[i], colors[i]), i);
                pennant.Shape.localScale *= PennantScale;
                pennants.Add(pennant);
            }
        }

        // The race track: the board floor scrolls under the runners; faint dashed
        // lanes run along it like a road's markings.
        void Floor()
        {
            var board = Instantiate(MenuArt.BoardTexture(2));
            board.wrapMode = TextureWrapMode.Repeat;
            floorMaterial = new Material(PieceFigure.Lit(Color.white, .82f, .05f)) { name = "Intro track", mainTexture = board };
            const float width = 44f, depth = 18f;
            floorMaterial.mainTextureScale = new Vector2(width / (2 * Square), depth / (2 * Square));
            var floor = LastSceneArt.Part(transform, "Track", LastSceneArt.Panel(width, depth), floorMaterial,
                                          new Vector3(0, 0, WallZ + depth / 2f), Quaternion.Euler(90f, 0, 0), null, false);
            floor.GetComponent<MeshRenderer>().receiveShadows = true;
            floor.layer = FloorLayer;
            Gloss(width, depth, new Vector3(0, 0, WallZ + depth / 2f));

            // Dashes rather than a scrolled texture: the blended shader ignores tiling.
            laneMaterial = MenuArt.Glow("intro lane", null, new Color(1f, .97f, .9f, .3f), false);
            foreach (float z in new[] { -3.5f, -1.9f, 1.1f })
                for (int i = 0; i < DashCount; i++)
                {
                    var dash = LastSceneArt.Part(transform, "Lane Dash", LastSceneArt.Panel(DashLength, .07f), laneMaterial,
                                                 new Vector3(0, .012f, z), Quaternion.Euler(90f, 0, 0), null, false);
                    dash.layer = FloorLayer;
                    dashes.Add(dash.transform);
                }
            // Darker towards the wall so the floor meets it softly.
            var shade = MenuArt.Glow("intro floor shade", MenuArt.VerticalFade(new Color(0, 0, 0, 0), new Color(.07f, .04f, .02f, .75f)), Color.white);
            LastSceneArt.Part(transform, "Floor Shade", LastSceneArt.Panel(width, 3f), shade,
                              new Vector3(0, .015f, WallZ + 1.5f), Quaternion.Euler(90f, 0, 0), null, false).layer = FloorLayer;
        }

        // The piers framing the hall: IntroStage.Pier.cs.

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
            Mirror();

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
            DisposePiers();
            if (floorMaterial != null) Destroy(floorMaterial);
            if (laneMaterial != null) Destroy(laneMaterial);
            if (puffMaterial != null) Destroy(puffMaterial);
            if (streakMaterial != null) Destroy(streakMaterial);
            DisposeGloss();
            DisposeVista();
        }
    }
}
