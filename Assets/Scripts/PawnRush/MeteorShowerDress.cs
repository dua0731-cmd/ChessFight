using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 (Royal Observatory modelling, R1 part 4): the meteors drawn with the Blender models. MeteorShower makes a view
    // ("Meteor") the first time it needs one and moves its parts every step - the gold warning disc ("Warning ring
    // (shove)"), the growing fill, the falling rod ("Streak") and the flash. This leaves all of that working and only
    // changes what is drawn: the disc and the rod stop drawing; the engraved gold ring (RoMeteorRing) shows while the
    // disc would, and the marble stone with its glowing tail (RoMeteor) rides the rod's lower end while it falls.
    // The fill and the flash stay the shower's own. Where and when meteors fall, and what they hit, is untouched.
    public sealed class MeteorShowerDress : MonoBehaviour
    {
        // MeteorShower's rod: 6 m long, its middle StreakFrom + half of that over the line as it starts.
        const float StreakLength = 6f;

        public static Color TailColour = new Color(1f, .82f, .55f, 1f);
        public static Color RingGlow = new Color(1f, .72f, .28f) * 1.4f;
        public static float Spin = 240f;   // degrees a second the stone turns as it falls
        // R725 ro-vfx: the ring's glow over the warning, times RingGlow - as it appears, and as the meteor lands.
        public static float RingGlowStart = 1f, RingGlowEnd = 2.6f;

        sealed class Parts
        {
            public Transform streak, rim, fill, stone, ring, rock;
            public readonly List<Material> ringGlow = new List<Material>();
        }

        MeteorShower shower;
        GameObject stoneModel, ringModel;
        Action<GameObject> paint;
        Material tail;
        bool ownTail;
        readonly Dictionary<Transform, Parts> dressed = new Dictionary<Transform, Parts>();

        public void Configure(GameObject stoneModel, GameObject ringModel, Action<GameObject> paint)
        {
            this.stoneModel = stoneModel;
            this.ringModel = ringModel;
            this.paint = paint;
        }

        void LateUpdate()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var view = transform.GetChild(i);
                if (view.name != "Meteor") continue;
                if (!dressed.TryGetValue(view, out var p))
                    dressed[view] = p = Dress(view);
                if (p == null) continue;
                bool falling = p.streak.gameObject.activeSelf;
                if (p.stone.gameObject.activeSelf != falling) p.stone.gameObject.SetActive(falling);
                if (falling)
                {
                    p.stone.localPosition = p.streak.localPosition + Vector3.down * (StreakLength * .5f);
                    if (p.rock != null) p.rock.Rotate(0f, Spin * Time.deltaTime, 0f, Space.Self);
                }
                bool warned = p.rim.gameObject.activeSelf;
                if (p.ring.gameObject.activeSelf != warned) p.ring.gameObject.SetActive(warned);
                // R725 ro-vfx: the gold glows hotter as the meteor comes - the shower's fill grows to the knock-down's
                // edge over the warning, so its width is how far the warning is.
                if (warned && p.fill != null && shower != null && p.ringGlow.Count > 0)
                {
                    float k = Mathf.Clamp01(p.fill.localScale.x / Mathf.Max(2f * shower.InnerRadius, .01f));
                    var glow = RingGlow * Mathf.Lerp(RingGlowStart, RingGlowEnd, k * k);
                    foreach (var m in p.ringGlow) m.SetColor("_EmissionColor", glow);
                }
            }
        }

        Parts Dress(Transform view)
        {
            var streak = view.Find("Streak");
            var rim = view.Find("Warning ring (shove)");
            if (streak == null || rim == null || stoneModel == null || ringModel == null) return null;
            Hide(streak);
            Hide(rim);
            if (shower == null) shower = GetComponent<MeteorShower>();
            var p = new Parts { streak = streak, rim = rim, fill = view.Find("Warning fill (knock-down)") };
            p.stone = Hang(view, stoneModel, "Meteor stone (model)");
            p.ring = Hang(view, ringModel, "Meteor ring (model)");
            p.ring.localPosition = rim.localPosition;
            p.rock = Find(p.stone, "Meteor stone");
            // the tail: an additive glow, its fade carried in the mesh's vertex colours (alpha 1 at the stone, 0 at its end)
            var tailPart = Find(p.stone, "Meteor tail");
            if (tailPart != null && tailPart.TryGetComponent<Renderer>(out var tr))
            {
                tr.sharedMaterial = TailMaterial();
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var r in p.stone.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // the ring's gold glows a little, so it reads on the gold and navy floor
            foreach (var r in p.ring.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials;
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", RingGlow);
                    p.ringGlow.Add(m);
                }
                r.materials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            p.stone.gameObject.SetActive(false);
            p.ring.gameObject.SetActive(false);
            return p;
        }

        Transform Hang(Transform view, GameObject model, string name)
        {
            // ro_kit.export's axes: a half turn about y lands Blender x, y, z on Unity x, z, y (RoyalObservatoryDressing).
            var holder = new GameObject(name).transform;
            holder.SetParent(view, false);
            var pivot = new GameObject("pivot").transform;
            pivot.SetParent(holder, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var go = Instantiate(model, pivot, false);
            go.name = model.name;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            paint?.Invoke(go);
            return holder;
        }

        static void Hide(Transform t)
        {
            if (t.TryGetComponent<Renderer>(out var r)) r.enabled = false;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var got = Find(t.GetChild(i), name);
                if (got != null) return got;
            }
            return null;
        }

        Material TailMaterial()
        {
            if (tail != null) return tail;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                tail = Arena.Glow(new Color(TailColour.r, TailColour.g, TailColour.b, .6f));
                return tail;
            }
            tail = new Material(shader) { name = "RO meteor tail (additive)" };
            ownTail = true;
            tail.SetFloat("_Surface", 1f);
            tail.SetFloat("_Blend", 2f);                                         // additive
            tail.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            tail.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            tail.SetFloat("_ZWrite", 0f);
            tail.SetFloat("_Cull", 0f);
            tail.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            tail.SetColor("_BaseColor", TailColour);
            tail.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return tail;
        }

        void OnDestroy()
        {
            if (ownTail && tail != null) Destroy(tail);
        }
    }
}
