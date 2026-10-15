using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The mini-games' shared effects and sounds (mini-games v1.0 §2.4): VFX_Burst (bits flying out and up),
    // VFX_Ring (a flat ring growing on the floor) and VFX_Beam (a column of light), and the SfxBank by the spec's
    // sound names. Everything is drawn by whoever sees the change: online a client plays a game's moments from
    // the state it gets (a square turned, a statue captured), so nothing extra is sent for effects.
    //
    // The sound files do not exist yet: a missing one is warned about once and skipped (§11.2). They go in
    // Assets/Resources/PawnRushMiniGameSfx/<name>.
    public sealed class ArenaFx : MonoBehaviour
    {
        const string SfxFolder = "PawnRushMiniGameSfx";
        const int Voices = 16;

        static ArenaFx current;

        sealed class Bit
        {
            public Transform T;
            public Vector3 Velocity;
            public float Life, Age, Size;
        }

        sealed class Grow
        {
            public Transform T;
            public LineRenderer Line;
            public Renderer R;
            public Color Color;
            public float From, To, Height, Life, Age;
            public bool Beam;
        }

        readonly List<Bit> bits = new List<Bit>();
        readonly List<Grow> grows = new List<Grow>();
        readonly Stack<Transform> spare = new Stack<Transform>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> missing = new HashSet<string>();
        AudioSource[] voices;
        int nextVoice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            missing.Clear();
        }

        static ArenaFx Get()
        {
            if (current != null) return current;
            if (!Application.isPlaying) return null;
            current = new GameObject("Mini-game effects").AddComponent<ArenaFx>();
            return current;
        }

        void Awake()
        {
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 3f;
                a.maxDistance = 30f;
                voices[i] = a;
            }
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        // ------------------------------------------------------------------ VFX

        // `count` bits of `colors`, spreading `spread` m/s sideways and `up` m/s up, living `life` s, `size` m.
        public static void Burst(Vector3 at, Color[] colors, int count, float spread, float up, float life, float size)
        {
            var fx = Get();
            if (fx == null || colors == null || colors.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                var t = fx.Take(PrimitiveType.Cube, Arena.Glow(colors[i % colors.Length]));
                t.position = at;
                t.rotation = Random.rotation;
                var dir = Random.insideUnitCircle;
                fx.bits.Add(new Bit
                {
                    T = t, Size = size * Random.Range(.7f, 1.3f), Life = life * Random.Range(.7f, 1.1f),
                    Velocity = new Vector3(dir.x * spread, up * Random.Range(.6f, 1.1f), dir.y * spread),
                });
            }
        }

        // A flat ring on the floor growing from radius `from` to `to` in `life` s, fading.
        public static void Ring(Vector3 at, float from, float to, float life, Color color)
        {
            var fx = Get();
            if (fx == null) return;
            var go = new GameObject("VFX_Ring");
            go.transform.SetParent(fx.transform, false);
            go.transform.position = at + Vector3.up * .03f;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            line.widthMultiplier = .12f;
            line.sharedMaterial = Arena.Glow(Color.white);
            line.startColor = line.endColor = color;
            fx.grows.Add(new Grow { T = go.transform, Line = line, Color = color, From = from, To = to, Life = life });
        }

        // A column of light `height` m tall, its diameter going from `from` to `to`, fading over `life` s.
        public static void Beam(Vector3 at, float from, float to, float height, float life, Color color)
        {
            var fx = Get();
            if (fx == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "VFX_Beam";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(fx.transform, false);
            go.transform.position = at + Vector3.up * height * .5f;
            var r = go.GetComponent<Renderer>();
            r.material = new Material(Arena.Glow(color));
            fx.grows.Add(new Grow { T = go.transform, R = r, Color = color, From = from, To = to, Height = height, Life = life, Beam = true });
        }

        Transform Take(PrimitiveType type, Material material)
        {
            Transform t;
            if (spare.Count > 0) { t = spare.Pop(); t.gameObject.SetActive(true); }
            else
            {
                var go = GameObject.CreatePrimitive(type);
                DestroyImmediate(go.GetComponent<Collider>());
                go.name = "VFX_Burst bit";
                t = go.transform;
                t.SetParent(transform, false);
            }
            t.GetComponent<Renderer>().sharedMaterial = material;
            return t;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                b.Age += dt;
                if (b.Age >= b.Life)
                {
                    b.T.gameObject.SetActive(false);
                    spare.Push(b.T);
                    bits.RemoveAt(i);
                    continue;
                }
                b.Velocity += Physics.gravity * .6f * dt;
                b.T.position += b.Velocity * dt;
                b.T.localScale = Vector3.one * b.Size * (1f - b.Age / b.Life);
            }
            for (int i = grows.Count - 1; i >= 0; i--)
            {
                var g = grows[i];
                g.Age += dt;
                float k = Mathf.Clamp01(g.Age / g.Life);
                if (k >= 1f)
                {
                    Destroy(g.T.gameObject);
                    grows.RemoveAt(i);
                    continue;
                }
                var c = g.Color;
                c.a = 1f - k;
                float size = Mathf.Lerp(g.From, g.To, 1f - (1f - k) * (1f - k));
                if (g.Beam)
                {
                    g.T.localScale = new Vector3(size, g.Height * .5f, size);
                    g.R.material.color = new Color(c.r, c.g, c.b, .6f * (1f - k));
                }
                else
                {
                    for (int p = 0; p < 48; p++)
                    {
                        float a = p * Mathf.PI * 2f / 48f;
                        g.Line.SetPosition(p, new Vector3(Mathf.Cos(a) * size, 0f, Mathf.Sin(a) * size));
                    }
                    g.Line.startColor = g.Line.endColor = c;
                }
            }
        }

        // ------------------------------------------------------------------ SfxBank

        // A sound by its spec name: 3D at `at` (3 m to 30 m), or 2D; pitch varies ±5 %.
        public static void Sfx(string name, Vector3 at, bool twoD = false, float volume = 1f, float pitch = 1f)
        {
            var fx = Get();
            if (fx == null || string.IsNullOrEmpty(name)) return;
            var clip = fx.Clip(name);
            if (clip == null) return;
            var voice = fx.voices[fx.nextVoice];
            fx.nextVoice = (fx.nextVoice + 1) % Voices;
            voice.transform.position = at;
            voice.spatialBlend = twoD ? 0f : 1f;
            voice.pitch = pitch * Random.Range(.95f, 1.05f);
            voice.PlayOneShot(clip, volume);
        }

        AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(SfxFolder + "/" + name);
            clips[name] = clip;
            if (clip == null && missing.Add(name))
                Debug.LogWarning($"[PawnRush] 효과음 파일이 없어 재생하지 않습니다: Resources/{SfxFolder}/{name}");
            return clip;
        }
    }
}
