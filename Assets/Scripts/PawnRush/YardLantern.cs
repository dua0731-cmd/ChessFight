using ChessFight.Game;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // R685 (승규 11-16 "여기 신호등도 그래픽 더 좋게 해"): the yard's signal lamps (the loft hoists' and the slewing cranes')
    // are lanterns instead of flat glowing boxes: an iron frame (base plate, four corner posts, a hipped cap and a ring
    // to hang it by) round a pane of coloured glass that glows (URP Lit with emission, HDR, so the bloom takes it), and a
    // soft halo facing the camera. The glass keeps the name and material its obstacle already colours every frame (green
    // / amber / red with a flame's flicker, YardWarn), so the obstacle's timing and the lighting rig's point light that
    // follows the glass (RoyalCarriageYard.Lighting, "Hoist lamp") are unchanged; this only reads that colour and turns
    // it into the glow and the halo. Pictures only, no colliders.
    public sealed class YardLantern : MonoBehaviour
    {
        // A glow strong enough for the bloom but not so strong the tonemapper bleaches the hue to white, and a halo thick
        // enough to carry the colour (승규 11-17: the colours were too faint to tell apart).
        const float Glow = 1.7f, HaloAlpha = .5f, HaloSize = 3.4f;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static Texture2D haloTexture;

        Material glass, haloMaterial;
        Transform halo;
        Color last = new Color(-1f, 0f, 0f, 0f);

        // The glass a lantern glows through: the obstacle sets its colour (`color`), the lantern its glow from it.
        public static Material GlassMaterial(string name, Color start)
        {
            var m = new Material(LitMaterials.Lit) { name = name, color = start };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .85f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor(EmissionId, start * Glow);
            return m;
        }

        // A lantern `size` across centred at `at` in `parent`'s frame; its glass named `glassName` in `glass`.
        public static YardLantern Build(Transform parent, string glassName, Vector3 at, float size, Material glass)
        {
            var go = new GameObject(glassName + " (lantern)");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var t = go.transform;
            float h = size, half = size * .5f, post = Mathf.Max(.025f, size * .09f);
            // The iron: a base plate, four corner posts, a cap in two steps and a ring on top.
            Iron(t, "Lantern base", new Vector3(0f, -half - .02f, 0f), new Vector3(size * 1.12f, .045f, size * 1.12f));
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -half : half, z = (i & 2) == 0 ? -half : half;
                Iron(t, "Lantern post", new Vector3(x, 0f, z), new Vector3(post, h, post));
            }
            Iron(t, "Lantern cap", new Vector3(0f, half + .03f, 0f), new Vector3(size * 1.2f, .06f, size * 1.2f));
            Iron(t, "Lantern cap", new Vector3(0f, half + .1f, 0f), new Vector3(size * .7f, .08f, size * .7f));
            Iron(t, "Lantern cap", new Vector3(0f, half + .17f, 0f), new Vector3(size * .3f, .06f, size * .3f));
            Iron(t, "Lantern ring", new Vector3(0f, half + .27f, 0f), new Vector3(size * .28f, .12f, .025f));
            // The glass, a hair inside the posts.
            var pane = YardKit.Box(t, glassName, Vector3.zero, new Vector3(size * .92f, h * .94f, size * .92f), glass, Solid.None);
            pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lantern = go.AddComponent<YardLantern>();
            lantern.glass = glass;
            lantern.MakeHalo(size);
            return lantern;
        }

        static void Iron(Transform t, string name, Vector3 at, Vector3 size) =>
            YardKit.Box(t, name, at, size, YardKit.Iron, Solid.None).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        void MakeHalo(float size)
        {
            if (haloTexture == null)
            {
                const int n = 64;
                haloTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Lantern halo" };
                var pixels = new Color32[n * n];
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        float x = (i + .5f) / n * 2f - 1f, y = (j + .5f) / n * 2f - 1f, d = Mathf.Sqrt(x * x + y * y);
                        // A bright core falling off as light scatters in the air (1 / (1 + k d^2)), to nothing at the edge.
                        float a = Mathf.Clamp01(1f / (1f + 18f * d * d) - .05f) * Mathf.Clamp01((1f - d) * 3f);
                        pixels[j * n + i] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                haloTexture.SetPixels32(pixels);
                haloTexture.Apply(false, true);
            }
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null) return;
            haloMaterial = new Material(shader) { mainTexture = haloTexture, name = "Lantern halo" };
            LitMaterials.MakeTransparent(haloMaterial, true);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Lantern halo";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localScale = Vector3.one * size * HaloSize;
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = haloMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            halo = quad.transform;
        }

        void LateUpdate()
        {
            if (glass == null) return;
            var c = glass.color;
            if (c != last)
            {
                last = c;
                var lit = new Color(c.r, c.g, c.b, 1f);
                glass.SetColor(EmissionId, lit * Glow);
                if (haloMaterial != null) haloMaterial.color = new Color(c.r, c.g, c.b, HaloAlpha);
            }
            if (halo != null)
            {
                var cam = Camera.main;
                if (cam != null) halo.rotation = cam.transform.rotation;
            }
        }

        void OnDestroy()
        {
            if (haloMaterial != null) Destroy(haloMaterial);
        }
    }
}
