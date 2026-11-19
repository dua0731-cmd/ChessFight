using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 (Royal Observatory modelling): a device's warning drawn on its Blender model instead of the graybox's flat
    // overlay. The device's own code still decides when it warns by turning its warning object on and off (StarPetal's
    // "Petal warning"); RoyalObservatoryDressing stops that object drawing and adds this, which watches it and makes the
    // model's gold glow a pulsing ember red while it is on. Only how it looks: nothing here moves or decides anything.
    public sealed class DeviceWarningGlow : MonoBehaviour
    {
        public static Color Glow = new Color(1f, .32f, .06f);
        public static float Intensity = 2.6f, PulseHz = 5f, Fade = 12f;

        GameObject warning;
        readonly List<Material> glowing = new List<Material>();
        float level;

        // Which of the model's materials glow: those drawn with a library material whose name starts with `slot`
        // (e.g. "RO_Gold": the rim, the bands' mouldings, the studs and the hinge). Each renderer gets its own copies.
        public void Configure(GameObject warning, IEnumerable<Renderer> model, string slot = "RO_Gold")
        {
            this.warning = warning;
            foreach (var r in model)
            {
                var mats = r.materials;   // per-renderer copies: one petal glows, its neighbours do not
                foreach (var m in mats)
                {
                    if (m == null || !m.name.StartsWith(slot)) continue;
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    m.SetColor("_EmissionColor", Color.black);
                    glowing.Add(m);
                }
                r.materials = mats;
            }
        }

        void Update()
        {
            if (glowing.Count == 0) return;
            bool on = warning != null && warning.activeSelf;
            level = Mathf.MoveTowards(level, on ? 1f : 0f, Time.deltaTime * Fade);
            float pulse = .55f + .45f * Mathf.Sin(Time.time * PulseHz * 2f * Mathf.PI);
            var c = Glow * (Intensity * level * pulse);
            foreach (var m in glowing) m.SetColor("_EmissionColor", c);
        }

        void OnDestroy()
        {
            foreach (var m in glowing) if (m != null) Destroy(m);
        }
    }
}
