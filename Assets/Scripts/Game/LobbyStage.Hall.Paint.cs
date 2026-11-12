using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Game
{
    // R576 (승규 "레퍼런스 사진 과 같이 컬러 텍스쳐 굽기"): the hall in the reference pictures' colours. Kit/hall_paint.py
    // (export_hall_kit.py --paint) paints it in Blender (cream stone, polished cream and walnut checker floor, red
    // velvet carpets and curtains, gold rings, lanterns, gate frame and crest, dark wood gate leaves), bakes the colour
    // with ambient occlusion and a metallic / smoothness map per mesh, and writes them to Resources/Lobby/KingdomHall/
    // Bake as <mesh>.png and <mesh>_Metal.png (R metallic, A smoothness). Each renderer that has them gets its own
    // URP Lit material with both; a mesh without them stays clay.
    public sealed partial class LobbyStage
    {
        readonly List<Material> hallPaint = new List<Material>();

        void HallPaint()
        {
            foreach (var r in castle.GetComponentsInChildren<Renderer>())
            {
                var colour = Resources.Load<Texture2D>("Lobby/KingdomHall/Bake/" + r.name);
                if (colour == null) continue;
                var metal = Resources.Load<Texture2D>("Lobby/KingdomHall/Bake/" + r.name + "_Metal");
                var m = new Material(r.sharedMaterial) { name = r.name + " baked" };
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", colour);
                if (metal != null)
                {
                    m.SetTexture("_MetallicGlossMap", metal);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetFloat("_Smoothness", 1f);    // with the map, the map's alpha is the smoothness
                }
                r.sharedMaterial = m;
                hallPaint.Add(m);
            }
        }

        // R582 (승규 "다음 텍스쳐 칠해", the reference's side windows): the diamond panes behind the leading, pale and
        // light blue in turn, lit from outside (emission) like the pictures' daylit glass. They cast no shadow, so
        // the sun still comes in through the windows.
        static readonly (string mesh, int color, float glow)[] WindowPanes =
        {
            ("Window_Glass_Light", 0xEEF2F0, .55f),
            ("Window_Glass_Blue", 0xA9C8E6, .6f),
        };

        void HallWindowGlass()
        {
            foreach (var pane in WindowPanes)
            {
                var glass = castle.transform.Find(pane.mesh);
                if (glass == null || !glass.TryGetComponent<Renderer>(out var r)) continue;
                var c = MenuArt.Hex(pane.color);
                var m = new Material(r.sharedMaterial) { name = pane.mesh + " glass" };
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", .9f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * pane.glow);
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                hallPaint.Add(m);
            }
        }

        // R588 (승규 "유리 천장", the reference's glass vault): the vault was only ribs with the sky behind them. Now real
        // glass (Vault_Glass, Kit/stair_end.py vault: a half cylinder just outside the ribs): pale sky blue, see-through
        // and glossy, so the sunset sky shows through it cooler and the glass catches the light. No shadow, so the sun
        // still comes in through it. AI Play captures: the glass faces down, away from the sun, so its colour alone
        // hardly showed (pale #BFDDF5 at .28, even #8EBEEE at .42); a faint glow of its own makes it read as the
        // picture's daylit blue glass over the sunset.
        const int VaultGlassColor = 0x8EBEEE;
        const float VaultGlassAlpha = .42f, VaultGlassSmoothness = .95f, VaultGlassGlow = .35f;

        void HallVaultGlass()
        {
            var glass = castle.transform.Find("Vault_Glass");
            if (glass == null || !glass.TryGetComponent<Renderer>(out var r)) return;
            var m = new Material(r.sharedMaterial) { name = "Vault glass" };
            var c = MenuArt.Hex(VaultGlassColor);
            c.a = VaultGlassAlpha;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", VaultGlassSmoothness);
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", MenuArt.Hex(VaultGlassColor) * VaultGlassGlow);
            LitMaterials.MakeTransparent(m);
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            hallPaint.Add(m);
        }

        // R580 (승규 "바닥에서 살짝의 광택이 나오게 해"): without a probe the polished floor (and the gold) only mirrored
        // the sunset sky outside. A box-projected probe over the whole hall, rendered once when the lobby opens, lets
        // them mirror the hall itself, faintly.
        const float HallReflectionIntensity = .7f;
        const float HallReflectionHeight = 1.8f;   // the probe's eye: about a player's view

        void HallReflection()
        {
            var bounds = new Bounds(castle.transform.position, Vector3.zero);
            foreach (var r in castle.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var probe = new GameObject("Hall Reflection").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(castle.transform, true);
            probe.transform.position = new Vector3(bounds.center.x, bounds.min.y + HallReflectionHeight, bounds.center.z);
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.boxProjection = true;
            probe.size = bounds.size;
            probe.center = bounds.center - probe.transform.position;
            probe.resolution = 256;
            probe.hdr = true;
            probe.intensity = HallReflectionIntensity;
            probe.importance = 1;
            probe.RenderProbe();
        }

        void DisposeHallPaint()
        {
            foreach (var m in hallPaint) if (m != null) Destroy(m);
            hallPaint.Clear();
        }
    }
}
