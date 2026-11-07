using UnityEngine;

namespace ChessFight.PawnRush
{
    // R505: a flame and its light, added in code inside a model's empty lantern cage (the user, 11-07: a model's painted
    // glow gives no light, so flames and lights are code). A small glowing teardrop and a warm point light flicker
    // together; no collider. Purely a picture, not synced (each PC flickers on its own).
    public sealed class MiniGameFlame : MonoBehaviour
    {
        Light glow;
        Transform flame;
        float baseIntensity, seed;
        Vector3 baseScale;

        public static MiniGameFlame Light(Transform parent, Vector3 localAt, Color colour, float intensity, float range)
        {
            var root = new GameObject("Lantern flame").transform;
            root.SetParent(parent, false);
            root.localPosition = localAt;
            var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(drop.GetComponent<Collider>());
            drop.name = "Flame";
            drop.transform.SetParent(root, false);
            drop.transform.localScale = new Vector3(.06f, .11f, .06f);
            drop.GetComponent<Renderer>().sharedMaterial = Arena.Lit("PR_Early_LanternFlame", colour, 4f, .9f);
            drop.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lamp = root.gameObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = colour;
            lamp.intensity = intensity;
            lamp.range = range;
            lamp.shadows = LightShadows.None;
            var f = root.gameObject.AddComponent<MiniGameFlame>();
            f.glow = lamp; f.flame = drop.transform; f.baseIntensity = intensity; f.baseScale = drop.transform.localScale;
            f.seed = Random.value * 100f;
            return f;
        }

        void Update()
        {
            float t = Time.time * 3.1f + seed;
            float k = .82f + .36f * Mathf.PerlinNoise(t, seed * .37f);
            if (glow != null) glow.intensity = baseIntensity * k;
            if (flame != null) flame.localScale = new Vector3(baseScale.x, baseScale.y * (.9f + .2f * k), baseScale.z);
        }
    }
}
