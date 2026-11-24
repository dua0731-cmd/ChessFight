using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// The one mixer of every place's light (ZoneLight, R734): late in the frame, after every place has set its weight,
    /// the scene's light at weight 1 - (the places' weights) and each place's at its weight; past a total of 1 the places
    /// share it and the scene's is gone. The scene's own is noted when the first place comes in and put back exactly
    /// when the last one is gone. Made the first time a place is added, kept across scenes (a scene's noted light is
    /// dropped, never put back, once that scene is no longer the active one).
    /// </summary>
    [DefaultExecutionOrder(10000)]
    sealed class ZoneLightMixer : MonoBehaviour
    {
        static ZoneLightMixer runner;

        public static void Ensure()
        {
            if (runner != null || !Application.isPlaying) return;
            var go = new GameObject("Zone light mixer") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            runner = go.AddComponent<ZoneLightMixer>();
        }

        public static Light Sun => runner != null && runner.captured ? runner.sun : null;

        static List<ZoneLight> Zones => ZoneLight.zones;
        const float Off = ZoneLight.Off;

        // The scene's own, noted the moment the first place comes in.
        bool captured;
        Scene scene;
        Light sun, sunSetting0;
        bool madeSun;
        Color sunColor0;
        float sunIntensity0, shadowStrength0;
        Quaternion sunRotation0;
        LightShadows shadows0;
        AmbientMode ambientMode0;
        Color sky0, equator0, ground0, flat0;
        SphericalHarmonicsL2 probe0, ambient0;
        bool fog0;
        FogMode fogMode0;
        Color fogColor0;
        float fogStart0, fogEnd0, fogDensity0;
        readonly List<float> lastWeights = new List<float>();
        // JY-gfx: the ambient light Apply wrote last. Anything else in the probe while places are in means the scene's own
        // changed under the mix (the sky's light made again: DynamicGI.UpdateEnvironment finishes a frame or more after
        // it is asked, e.g. right after the upgrade's switch while the camera stands in a place): noted as the scene's
        // then, so a place scales the settled sky's light, not a half-made one.
        SphericalHarmonicsL2 written;
        bool wrote;

        void LateUpdate()
        {
            float total = 0f;
            foreach (var z in Zones) if (z.weight > Off) total += z.weight;
            if (captured && SceneManager.GetActiveScene() != scene) captured = false;   // that scene's light went with it
            if (total <= Off)
            {
                if (captured) Restore();
                return;
            }
            if (!captured) Capture();
            else if (wrote && !Same(RenderSettings.ambientProbe, written))
            {
                probe0 = ambient0 = RenderSettings.ambientProbe;
                ZoneLight.Renotes++;
                lastWeights.Clear();
            }
            if (Unchanged()) return;
            Apply(total);
        }

        static bool Same(SphericalHarmonicsL2 a, SphericalHarmonicsL2 b)
        {
            for (int rgb = 0; rgb < 3; rgb++)
                for (int c = 0; c < 9; c++)
                    if (Mathf.Abs(a[rgb, c] - b[rgb, c]) > 1e-5f + 1e-4f * Mathf.Abs(b[rgb, c])) return false;
            return true;
        }

        // Standing weights are not applied again in a player (in the Editor they are: the looks may be tuned while
        // playing). The first frame after a change always applies.
        bool Unchanged()
        {
            bool same = lastWeights.Count == Zones.Count;
            for (int i = 0; same && i < Zones.Count; i++) same = Mathf.Abs(lastWeights[i] - Zones[i].weight) < 1e-4f;
            if (same && !Application.isEditor) return true;
            lastWeights.Clear();
            foreach (var z in Zones) lastWeights.Add(z.weight);
            return false;
        }

        static bool Owned(Light l) => ZoneLight.owned.Contains(l);

        Light FindSun()
        {
            var s = RenderSettings.sun;
            if (s != null && s.type == LightType.Directional && s.isActiveAndEnabled && !Owned(s)) return s;
            Light best = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional || !l.isActiveAndEnabled || Owned(l)) continue;
                if (best == null || l.intensity > best.intensity) best = l;
            }
            return best;
        }

        void Capture()
        {
            captured = true;
            scene = SceneManager.GetActiveScene();
            lastWeights.Clear();
            ZoneLight.owned.RemoveWhere(l => l == null);
            sunSetting0 = RenderSettings.sun;
            sun = FindSun();
            madeSun = false;
            if (sun == null)
            {
                ZoneLight bringer = null;
                foreach (var z in Zones) if (z.BringSun && z.weight > Off) { bringer = z; break; }
                if (bringer != null)
                {
                    // A scene with no sun and a place that brings its own: strength 0 outside it, its way always.
                    sun = new GameObject("Zone sun").AddComponent<Light>();
                    sun.type = LightType.Directional;
                    sun.shadows = LightShadows.Soft;
                    sun.intensity = 0f;
                    sun.color = bringer.SunColor;
                    sun.transform.rotation = bringer.SunRotation;
                    madeSun = true;
                }
            }
            if (sun != null)
            {
                sunColor0 = sun.color;
                sunIntensity0 = madeSun ? 0f : sun.intensity;
                shadowStrength0 = sun.shadowStrength;
                sunRotation0 = sun.transform.rotation;
                shadows0 = sun.shadows;
            }

            ambientMode0 = RenderSettings.ambientMode;
            sky0 = RenderSettings.ambientSkyColor;
            equator0 = RenderSettings.ambientEquatorColor;
            ground0 = RenderSettings.ambientGroundColor;
            flat0 = RenderSettings.ambientLight;
            // Unity keeps the harmonics of whatever mode is on (the sky's, a gradient's, a flat colour's), so the scene's
            // ambient is that exactly; the mix writes harmonics in the sky mode (the only mode that takes them written).
            probe0 = RenderSettings.ambientProbe;
            ambient0 = probe0;

            fog0 = RenderSettings.fog;
            fogMode0 = RenderSettings.fogMode;
            fogColor0 = RenderSettings.fogColor;
            fogStart0 = RenderSettings.fogStartDistance;
            fogEnd0 = RenderSettings.fogEndDistance;
            fogDensity0 = RenderSettings.fogDensity;
        }

        void Apply(float total)
        {
            float rest = Mathf.Max(0f, 1f - total), share = total > 1f ? 1f / total : 1f;

            if (sun != null)
            {
                var colour = sunColor0 * rest;
                float intensity = sunIntensity0 * rest, shadow = shadowStrength0 * rest, keepWay = rest;
                var way = Vector4.zero;
                bool turn = madeSun, soft = false;
                foreach (var z in Zones)
                {
                    if (z.weight <= Off) continue;
                    float w = z.weight * share;
                    soft |= z.SoftShadows;
                    if (z.KeepSun)
                    {
                        // JY-gfx: the scene's own sun for this share, exactly (one time of day everywhere).
                        colour += sunColor0 * w;
                        intensity += sunIntensity0 * w;
                        shadow += shadowStrength0 * w;
                        keepWay += w;
                        continue;
                    }
                    colour += z.SunColor * w;
                    intensity += z.SunIntensity * w;
                    shadow += z.ShadowStrength * w;
                    if (z.TurnSun) { way += Aligned(z.SunRotation, sunRotation0) * w; turn = true; }
                    else keepWay += w;
                }
                colour.a = 1f;
                sun.color = colour;
                sun.intensity = intensity;
                sun.shadowStrength = Mathf.Clamp01(shadow);
                if (turn)
                {
                    // A weighted mean of the ways (the scene's own for its share and for the places that keep it).
                    way += Aligned(sunRotation0, sunRotation0) * keepWay;
                    if (way.sqrMagnitude > 1e-8f)
                    {
                        way.Normalize();
                        sun.transform.rotation = new Quaternion(way.x, way.y, way.z, way.w);
                    }
                }
                if (soft && sun.shadows == LightShadows.None) sun.shadows = LightShadows.Soft;
                if (RenderSettings.sun != sun) RenderSettings.sun = sun;
            }

            // Ambient: blended as harmonics, so a little of a place is a little of its light and none is the scene's.
            var sh = ambient0 * rest;
            foreach (var z in Zones)
                if (z.weight > Off) sh += (z.AmbientScale >= 0f ? Neutral(ambient0, z.AmbientNeutral, z.AmbientTint) * z.AmbientScale : z.AmbientSH()) * (z.weight * share);
            if (RenderSettings.ambientMode != AmbientMode.Skybox) RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientProbe = sh;
            written = RenderSettings.ambientProbe;
            wrote = true;

            // Haze: from the scene's linear fog, or from none (pushed past everything) when it had none.
            float fogged = 0f;
            var hazeColour = Color.black;
            foreach (var z in Zones)
                if (z.Fog && z.weight > Off) { fogged += z.weight; hazeColour += z.FogColor * z.weight; }
            if (fogged > 0f)
            {
                bool fromLinear = fog0 && fogMode0 == FogMode.Linear;
                var noneColour = fog0 ? fogColor0 : hazeColour * (1f / fogged);
                float start0 = fromLinear ? fogStart0 : ZoneLight.NoFogStart, end0 = fromLinear ? fogEnd0 : ZoneLight.NoFogEnd;
                // The distances are mixed as 1 / distance: mixed straight, 3000 m -> 80 m would bring the haze into view
                // only in the last tenth of the fade (ZoneLightWalk caught it); this way what is seen fogs evenly.
                var colour = noneColour * rest;
                float nearStart = rest / Mathf.Max(.1f, start0), nearEnd = rest / Mathf.Max(.1f, end0);
                foreach (var z in Zones)
                {
                    if (z.weight <= Off) continue;
                    float w = z.weight * share;
                    colour += (z.Fog ? z.FogColor : noneColour) * w;
                    nearStart += w / Mathf.Max(.1f, z.Fog ? z.FogStart : start0);
                    nearEnd += w / Mathf.Max(.1f, z.Fog ? z.FogEnd : end0);
                }
                colour.a = 1f;
                float start = 1f / Mathf.Max(1e-7f, nearStart), end = Mathf.Max(start + .1f, 1f / Mathf.Max(1e-7f, nearEnd));
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = colour;
                RenderSettings.fogStartDistance = start;
                RenderSettings.fogEndDistance = end;
            }
            else RestoreFog();
        }

        // JY-gfx: ambient harmonics `t` of the way to their own grey times `tint` (each coefficient's colour toward its
        // luminance; harmonics are linear in colour, so this tints the light from every direction alike).
        static SphericalHarmonicsL2 Neutral(SphericalHarmonicsL2 sh, float t, Color tint)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f) return sh;
            for (int c = 0; c < 9; c++)
            {
                float l = .2126f * sh[0, c] + .7152f * sh[1, c] + .0722f * sh[2, c];
                for (int rgb = 0; rgb < 3; rgb++) sh[rgb, c] = Mathf.Lerp(sh[rgb, c], l * tint[rgb], t);
            }
            return sh;
        }

        // A rotation as a 4-vector on the same side as `near` (q and -q are the same turn).
        static Vector4 Aligned(Quaternion q, Quaternion near)
        {
            var v = new Vector4(q.x, q.y, q.z, q.w);
            if (Quaternion.Dot(q, near) < 0f) v = -v;
            return v;
        }

        void RestoreFog()
        {
            RenderSettings.fog = fog0;
            RenderSettings.fogMode = fogMode0;
            RenderSettings.fogColor = fogColor0;
            RenderSettings.fogStartDistance = fogStart0;
            RenderSettings.fogEndDistance = fogEnd0;
            RenderSettings.fogDensity = fogDensity0;
        }

        void Restore()
        {
            captured = false;
            wrote = false;
            lastWeights.Clear();
            if (SceneManager.GetActiveScene() != scene) return;
            if (sun != null)
            {
                if (madeSun) Destroy(sun.gameObject);
                else
                {
                    sun.color = sunColor0;
                    sun.intensity = sunIntensity0;
                    sun.shadowStrength = shadowStrength0;
                    sun.transform.rotation = sunRotation0;
                    sun.shadows = shadows0;
                }
            }
            sun = null;
            madeSun = false;
            RenderSettings.sun = sunSetting0;
            RenderSettings.ambientMode = ambientMode0;
            RenderSettings.ambientSkyColor = sky0;
            RenderSettings.ambientEquatorColor = equator0;
            RenderSettings.ambientGroundColor = ground0;
            RenderSettings.ambientLight = flat0;
            RenderSettings.ambientProbe = probe0;
            RestoreFog();
        }

        void OnDestroy()
        {
            if (captured) Restore();
            if (runner == this) runner = null;
        }
    }
}
