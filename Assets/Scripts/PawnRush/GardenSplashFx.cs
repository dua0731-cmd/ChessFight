using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R442 (승규 11-05, "기물이 바닥에 떨어졌을 때 물에 풍덩 한 것처럼 해"); R444 ("물 튀기는 걸 좀 더 무작위로 자연스럽게 계산해"):
    // a piece plunging into the garden's water, worked out from its velocity into the water - V down (2 .. 12 m/s), U
    // across along heading psi - with the randomness a real splash has, drawn from where it comes from, so no two
    // are alike:
    //  - the crown: the water pushed aside rises round the piece as a thin flaring sheet, its rim leaving at 0.35 V
    //    out and 0.55 V up (at most 5 m/s); more on the side the piece heads for (asymmetry e = 1.2 U / (U + V), at
    //    most 0.7); never round - a smooth random undulation (modes 2 .. 6) - and each side collapses in its own time;
    //  - the fingers: the rim, a liquid cylinder, breaks up (Rayleigh-Plateau) into N = 8 + 1.1 V (+- 1.5) fingers,
    //    unevenly spaced, of lognormal strength; each sheds drops as a Poisson process (rate (45 + 6 V) / N times its
    //    strength) while the crown rises, of lognormal size (median 2 cm x strength^0.4), small ones a little faster;
    //  - the spray: Poisson(8 + 1.4 V) drops thrown out low at the first touch, headings von Mises round the piece's
    //    (kappa 3e), 15 .. 55 degrees up, speed V x lognormal;
    //  - the mist: Poisson(14 + 2 V) droplets of 3 .. 7 mm, which the air slows - they hang, then fall;
    //  - the jet: when the cavity behind the piece closes (t_c = (0.18 + 0.025 V) x lognormal) a column shoots up at
    //    w = min(0.5 V, 5.5) x lognormal, leaning back from the piece's heading a little, and breaks up near its peak
    //    into 2 .. 5 drops (Rayleigh-Plateau again);
    //  - bubbles: the air dragged under comes up as a raft of clear bubbles round the impact, each lasting an
    //    exponential while;
    //  - every drop is a projectile with quadratic air drag, dv/dt = g - k |v| v, k = 3 Cd rho_air / (8 rho_water r)
    //    (Cd 0.47): small drops slow and hang, big ones fly; a drop that falls back in leaves a small ring;
    //  - rings and churn on the water: the water shader (_GardenSplashes in PR_GardenClearWater); the sound: one of
    //    three takes (ArtSource/PawnRush/GardenConcept/splash_sound.py), lower and louder for a harder landing.
    // Drawn by every PC for itself (GardenSplashPool), nothing sent; one shared object, made on the first splash.
    public sealed class GardenSplashFx : MonoBehaviour
    {
        const int MaxSplashes = 6, MaxMainRings = 4, MaxPlips = 12, MaxBits = 640, Voices = 6;
        const float Gravity = 9.81f, PieceRadius = .28f;
        const float DragTimesRadius = 3f * .47f * 1.2f / (8f * 1000f);   // k r (m): 3 Cd rho_air / (8 rho_water)
        static readonly int RingId = Shader.PropertyToID("_GardenSplashes");
        static readonly int RingInfoId = Shader.PropertyToID("_GardenSplashesInfo");
        static readonly int RingCountId = Shader.PropertyToID("_GardenSplashesCount");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly string[] Takes = { "GardenSplash", "GardenSplash2", "GardenSplash3" };

        static GardenSplashFx current;
        static bool warned;

        enum Kind { Drop, Mist, Bubble }

        sealed class Bit
        {
            public Transform T;
            public Renderer R;
            public Kind Kind;
            public Vector3 Position, Velocity;
            public float Radius, Shown, Floor, Age, Life, Delay;
        }

        sealed class Ring
        {
            public float X, Z, Y, Start, Strength, Scale, Churn, Life;
        }

        sealed class Splash
        {
            public Vector3 At, JetAxis;
            public float V, E, Psi, Age, Start, Ur, Uy, Tc, W, JetRadius, CrownLife;
            public int Pinched;
            public float[] FingerAngle, FingerStrength, FingerNext, PinchAt;
            public readonly float[] NoiseAmp = new float[5], NoisePhase = new float[5];
            public Mesh Crown;
            public GameObject CrownObject, Jet, JetTip;
            public Renderer CrownRenderer, JetRenderer, TipRenderer;

            // The rim's random undulation at angle a (modes 2 .. 6, spread 1).
            public float Noise(float a)
            {
                float n = 0f;
                for (int m = 0; m < 5; m++) n += NoiseAmp[m] * Mathf.Sin((m + 2) * a + NoisePhase[m]);
                return n;
            }

            // How much higher and further out the crown goes at angle a (the piece's heading, the undulation).
            public float Up(float a) => Mathf.Max(.2f, 1f + .8f * E * Mathf.Cos(a - Psi) + .15f * Noise(a));
            public float Out(float a) => Mathf.Max(.3f, 1f + .35f * E * Mathf.Cos(a - Psi) + .06f * Noise(a));
        }

        readonly List<Bit> bits = new List<Bit>();
        readonly Stack<Bit> spare = new Stack<Bit>();
        readonly List<Splash> splashes = new List<Splash>();
        readonly List<Ring> mainRings = new List<Ring>(), plips = new List<Ring>();
        readonly Vector4[] ringData = new Vector4[MaxMainRings + MaxPlips], ringInfo = new Vector4[MaxMainRings + MaxPlips];
        readonly List<AudioClip> clips = new List<AudioClip>();
        Material water;
        Mesh sphere;
        MaterialPropertyBlock block;
        AudioSource[] voices;
        int nextVoice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            warned = false;
        }

        // A splash where a piece went in (`at` on the surface) with `velocity`.
        public static void Play(Vector3 at, Vector3 velocity)
        {
            if (!Application.isPlaying) return;
            if (current == null) current = new GameObject("Garden splashes").AddComponent<GardenSplashFx>();
            current.Begin(at, velocity);
        }

        public static void Play(Vector3 at, float speed) => Play(at, Vector3.down * speed);

        void Awake()
        {
            water = Resources.Load<Material>("PR_GardenSplash");
            if (water == null && !warned)
            {
                warned = true;
                Debug.LogWarning("[정원] 물보라 재질이 없습니다: Resources/PR_GardenSplash");
            }
            sphere = MakeSphere(10, 7);
            block = new MaterialPropertyBlock();
            foreach (var take in Takes)
            {
                var clip = Resources.Load<AudioClip>("PawnRushGardenSfx/" + take);
                if (clip != null) clips.Add(clip);
            }
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Splash voice");
                go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 4f;
                a.maxDistance = 45f;
                SectionSoundGate.Gate(a);   // R529
                voices[i] = a;
            }
        }

        void OnDestroy()
        {
            if (current == this) current = null;
            Shader.SetGlobalFloat(RingCountId, 0f);
        }

        // ------------------------------------------------------------------ a new splash

        void Begin(Vector3 at, Vector3 velocity)
        {
            if (splashes.Count >= MaxSplashes) Finish(splashes[0], 0);
            float v = Mathf.Clamp(-velocity.y, 2f, 12f);
            var flat = new Vector2(velocity.x, velocity.z);
            float u = flat.magnitude;
            var s = new Splash
            {
                At = at, V = v, Start = Time.timeSinceLevelLoad,
                E = Mathf.Min(.7f, 1.2f * u / (u + v)),
                Psi = u > .05f ? Mathf.Atan2(flat.y, flat.x) : Random.value * Mathf.PI * 2f,
                Ur = .35f * v, Uy = Mathf.Min(.55f * v, 5f),
            };
            // The rim's undulation: random amplitudes (falling with the mode) and phases, scaled to a spread of 1.
            float sum = 0f;
            for (int m = 0; m < 5; m++)
            {
                s.NoiseAmp[m] = Gauss() / (m + 2);
                s.NoisePhase[m] = Random.value * Mathf.PI * 2f;
                sum += s.NoiseAmp[m] * s.NoiseAmp[m] * .5f;
            }
            for (int m = 0; m < 5; m++) s.NoiseAmp[m] /= Mathf.Sqrt(Mathf.Max(sum, 1e-4f));
            s.CrownLife = 2f * s.Uy * (1f + .8f * s.E + .3f) / Gravity;
            // The fingers: uneven spacing, lognormal strength, each its own Poisson clock.
            int n = Mathf.Clamp(Mathf.RoundToInt(8f + 1.1f * v + 1.5f * Gauss()), 8, 24);
            s.FingerAngle = new float[n];
            s.FingerStrength = new float[n];
            s.FingerNext = new float[n];
            float turn = Random.value * Mathf.PI * 2f;
            for (int k = 0; k < n; k++)
            {
                s.FingerAngle[k] = turn + Mathf.PI * 2f * (k + Random.Range(-.3f, .3f)) / n;
                s.FingerStrength[k] = LogNormal(0f, .3f);
                s.FingerNext[k] = .04f + Exponential(1f / FingerRate(s, k));
            }
            // The jet: when the cavity closes, how fast, which way it leans, where it breaks.
            s.Tc = (.18f + .025f * v) * LogNormal(0f, .08f);
            s.W = Mathf.Min(.5f * v, 5.5f) * LogNormal(0f, .12f);
            s.JetRadius = .07f + .006f * v;
            float lean = (6f * s.E / .7f + 4f * Gauss()) * Mathf.Deg2Rad;
            var back = new Vector3(-Mathf.Cos(s.Psi), 0f, -Mathf.Sin(s.Psi));
            s.JetAxis = (Vector3.up * Mathf.Cos(lean) + back * Mathf.Sin(lean)).normalized;
            int pinches = Mathf.Clamp(2 + Poisson(1.2f), 2, 5);
            float rise = s.W / Gravity;
            s.PinchAt = new float[pinches];
            for (int j = 0; j < pinches; j++) s.PinchAt[j] = rise * (.7f + .12f * j + Random.Range(0f, .08f));
            if (water != null)
            {
                s.Crown = new Mesh { name = "Splash crown" };
                s.Crown.MarkDynamic();
                s.CrownObject = Part("Splash crown", s.Crown, out s.CrownRenderer);
                s.Jet = Part("Splash jet", sphere, out s.JetRenderer);
                s.JetTip = Part("Splash jet tip", sphere, out s.TipRenderer);
                s.CrownObject.SetActive(false);
                s.Jet.SetActive(false);
                s.JetTip.SetActive(false);
                FirstTouch(s);
            }
            splashes.Add(s);
            if (mainRings.Count >= MaxMainRings) mainRings.RemoveAt(0);
            mainRings.Add(new Ring { X = at.x, Z = at.z, Y = at.y, Start = s.Start, Strength = Mathf.Clamp(v / 8f, .35f, 1.3f), Scale = 1f, Churn = 1f, Life = 5f });
            if (clips.Count > 0)
            {
                var voice = voices[nextVoice];
                nextVoice = (nextVoice + 1) % Voices;
                voice.transform.position = at;
                float hard = Mathf.InverseLerp(4f, 12f, v);
                voice.pitch = Mathf.Lerp(1.12f, .9f, hard) * LogNormal(0f, .04f);
                voice.PlayOneShot(clips[Random.Range(0, clips.Count)], Mathf.Lerp(.25f, .56f, Mathf.InverseLerp(2f, 9f, v))   /* R654: at most -21 dB (was -16) */);
            }
        }

        static float FingerRate(Splash s, int k) => (45f + 6f * s.V) / s.FingerAngle.Length * s.FingerStrength[k];

        // The spray, the mist and the bubbles of the first touch.
        void FirstTouch(Splash s)
        {
            var at = s.At;
            int spray = Poisson(8f + 1.4f * s.V);
            for (int i = 0; i < spray; i++)
            {
                float a = VonMises(s.Psi, 3f * s.E);
                float el = (15f + 40f * (Random.value + Random.value) * .5f) * Mathf.Deg2Rad;
                float speed = s.V * LogNormal(-.15f, .25f) * (1f + .5f * s.E * Mathf.Cos(a - s.Psi));
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = Mathf.Clamp(LogNormal(Mathf.Log(.015f), .4f), .006f, .04f);
                Emit(Kind.Drop, at + dir * (PieceRadius * .8f) + Vector3.up * .02f,
                     dir * (speed * Mathf.Cos(el)) + Vector3.up * (speed * Mathf.Sin(el)), r, r, at.y, 99f, 0f);
            }
            int mist = Poisson(14f + 2f * s.V);
            for (int i = 0; i < mist; i++)
            {
                float a = VonMises(s.Psi, 2f * s.E);
                float el = Random.Range(25f, 70f) * Mathf.Deg2Rad;
                float speed = s.V * LogNormal(-.6f, .35f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = Random.Range(.003f, .007f);
                Emit(Kind.Mist, at + dir * (PieceRadius * Random.Range(.6f, 1.1f)) + Vector3.up * .03f,
                     dir * (speed * Mathf.Cos(el)) + Vector3.up * (speed * Mathf.Sin(el)), r, Mathf.Max(2.2f * r, .009f), at.y,
                     Random.Range(.9f, 1.6f), 0f);
            }
            int bubbles = Poisson(5f + .8f * s.V);
            for (int i = 0; i < bubbles; i++)
            {
                var off = new Vector3(Gauss(), 0f, Gauss()) * .35f;
                off = Vector3.ClampMagnitude(off, 1.2f);
                float r = Mathf.Clamp(LogNormal(Mathf.Log(.025f), .35f), .012f, .07f);
                var drift = off.sqrMagnitude > 1e-4f ? off.normalized * Random.Range(.05f, .2f) : Vector3.zero;
                Emit(Kind.Bubble, at + off, drift, r, r, at.y, Mathf.Clamp(Exponential(.9f), .25f, 2.5f), Random.Range(0f, .45f));
            }
        }

        GameObject Part(string name, Mesh mesh, out Renderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = water;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            renderer = r;
            return go;
        }

        void Emit(Kind kind, Vector3 position, Vector3 velocity, float radius, float shown, float floor, float life, float delay)
        {
            if (water == null) return;
            Bit b;
            if (spare.Count > 0) b = spare.Pop();
            else if (bits.Count + spare.Count < MaxBits)
            {
                var go = Part("Splash drop", sphere, out var r);
                b = new Bit { T = go.transform, R = r };
            }
            else return;
            b.T.gameObject.SetActive(delay <= 0f);
            b.Kind = kind;
            b.Position = position;
            b.Velocity = velocity;
            b.Radius = radius;
            b.Shown = shown;
            b.Floor = floor;
            b.Age = 0f;
            b.Life = life;
            b.Delay = delay;
            Fade(b.R, kind == Kind.Mist ? .35f : kind == Kind.Bubble ? .5f : .72f);
            bits.Add(b);
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = splashes.Count - 1; i >= 0; i--) Step(splashes[i], i, dt);
            for (int i = bits.Count - 1; i >= 0; i--)
                if (!Move(bits[i], dt))
                {
                    bits[i].T.gameObject.SetActive(false);
                    spare.Push(bits[i]);
                    bits.RemoveAt(i);
                }
            Publish();
        }

        // A drop, a mist droplet or a bubble; false when it is gone.
        bool Move(Bit b, float dt)
        {
            if (b.Delay > 0f)
            {
                b.Delay -= dt;
                if (b.Delay > 0f) return true;
                b.T.gameObject.SetActive(true);
            }
            b.Age += dt;
            if (b.Age >= b.Life) return false;
            if (b.Kind == Kind.Bubble)
            {
                // Floating, drifting out, shrinking before it bursts.
                b.Position += b.Velocity * dt;
                b.Velocity *= Mathf.Exp(-1.5f * dt);
                float left = 1f - b.Age / b.Life;
                float r = b.Shown * Mathf.Min(1f, left * 4f);
                b.T.SetPositionAndRotation(new Vector3(b.Position.x, b.Floor + r * .3f, b.Position.z), Quaternion.identity);
                b.T.localScale = new Vector3(2f * r, 1.2f * r, 2f * r);
                return true;
            }
            // Gravity and quadratic air drag (implicit in the drag, so a tiny droplet does not overshoot).
            b.Velocity += Vector3.down * (Gravity * dt);
            float k = DragTimesRadius / b.Radius;
            b.Velocity /= 1f + k * b.Velocity.magnitude * dt;
            b.Position += b.Velocity * dt;
            if (b.Position.y < b.Floor && b.Velocity.y < 0f)
            {
                if (b.Kind == Kind.Drop && b.Radius >= .012f) Plip(new Vector3(b.Position.x, b.Floor, b.Position.z), b.Radius);
                return false;
            }
            if (b.Kind == Kind.Mist) Fade(b.R, .35f * (1f - b.Age / b.Life));
            float speed = b.Velocity.magnitude;
            b.T.SetPositionAndRotation(b.Position, speed > .01f ? Quaternion.LookRotation(b.Velocity) : Quaternion.identity);
            b.T.localScale = new Vector3(2f * b.Shown, 2f * b.Shown, 2f * b.Shown * (1f + .07f * speed));
            return true;
        }

        void Step(Splash s, int index, float dt)
        {
            s.Age += dt;
            float t = s.Age;
            if (water != null)
            {
                Crown(s, t);
                Fingers(s, t);
                JetStep(s, t);
            }
            // Its rings stay in the water shader for 5 s.
            if (t > 5f) Finish(s, index);
        }

        // The crown sheet: each side rises and falls in its own time; drawn while any side stands.
        void Crown(Splash s, float t)
        {
            float top = s.Uy * 1.6f * t - .5f * Gravity * t * t;
            if (t >= s.CrownLife || top <= .01f)
            {
                if (s.CrownObject.activeSelf) s.CrownObject.SetActive(false);
                return;
            }
            BuildCrown(s, t);
            s.CrownObject.SetActive(true);
            Fade(s.CrownRenderer, .5f * (1f - Mathf.Pow(Mathf.Clamp01(t / s.CrownLife), 1.5f)));
        }

        // Each finger sheds drops as a Poisson process while its side of the crown rises.
        void Fingers(Splash s, float t)
        {
            for (int k = 0; k < s.FingerAngle.Length; k++)
            {
                float a = s.FingerAngle[k];
                float up = s.Uy * s.Up(a);
                if (t > .55f * 2f * up / Gravity) continue;
                float strength = s.FingerStrength[k];
                while (s.FingerNext[k] <= t)
                {
                    s.FingerNext[k] += Exponential(1f / FingerRate(s, k));
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var side = new Vector3(-dir.z, 0f, dir.x);
                    float h = Mathf.Max(0f, up * t - .5f * Gravity * t * t);
                    float rim = PieceRadius + s.Ur * s.Out(a) * t;
                    float r = Mathf.Clamp(LogNormal(Mathf.Log(.02f * Mathf.Pow(strength, .4f)), .35f), .008f, .05f);
                    float quick = Mathf.Pow(.02f / r, .1f);
                    var vel = dir * (s.Ur * s.Out(a) * (1f + .1f * Gauss())) + side * (.25f * Gauss())
                              + Vector3.up * ((up - Gravity * t) * (1f + .1f * Gauss()) + Random.Range(0f, .5f));
                    Emit(Kind.Drop, s.At + dir * (rim + .01f) + Vector3.up * (h + Cusp(s, t) * strength), vel * quick, r, r, s.At.y, 99f, 0f);
                }
            }
        }

        static float Cusp(Splash s, float t) => (.05f + .03f * s.V / 8f) * Mathf.Min(1f, t / (.3f * s.CrownLife));

        // The jet once the cavity has closed: up its leaning axis at w, back down; drops break off its top.
        void JetStep(Splash s, float t)
        {
            float tj = t - s.Tc, life = 2f * s.W / Gravity;
            float jh = s.W * tj - .5f * Gravity * tj * tj;
            if (tj <= 0f || tj >= life || jh <= .02f)
            {
                if (s.Jet.activeSelf) s.Jet.SetActive(false);
                if (s.JetTip.activeSelf) s.JetTip.SetActive(false);
                return;
            }
            float r = s.JetRadius * Mathf.Sqrt(1f - tj / life);
            var tip = s.At + s.JetAxis * jh;
            s.Jet.SetActive(true);
            s.JetTip.SetActive(true);
            s.Jet.transform.SetPositionAndRotation(s.At + s.JetAxis * (jh * .5f), Quaternion.FromToRotation(Vector3.up, s.JetAxis));
            s.Jet.transform.localScale = new Vector3(2f * r, jh, 2f * r);
            s.JetTip.transform.SetPositionAndRotation(tip, Quaternion.identity);
            s.JetTip.transform.localScale = Vector3.one * (2.6f * r);
            Fade(s.JetRenderer, .7f);
            Fade(s.TipRenderer, .75f);
            while (s.Pinched < s.PinchAt.Length && tj >= s.PinchAt[s.Pinched])
            {
                float drop = r * (1.35f - .2f * s.Pinched) * LogNormal(0f, .1f);
                var side = new Vector3(Gauss(), 0f, Gauss()) * .25f;
                Emit(Kind.Drop, tip + s.JetAxis * (.04f * s.Pinched), s.JetAxis * (s.W - Gravity * tj) + side, drop, drop, s.At.y, 99f, 0f);
                s.Pinched++;
            }
        }

        void Finish(Splash s, int index)
        {
            if (s.CrownObject != null) Destroy(s.CrownObject);
            if (s.Jet != null) Destroy(s.Jet);
            if (s.JetTip != null) Destroy(s.JetTip);
            if (s.Crown != null) Destroy(s.Crown);
            splashes.RemoveAt(index);
        }

        void Fade(Renderer r, float alpha)
        {
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(.86f, .95f, 1f, Mathf.Clamp01(alpha)));
            r.SetPropertyBlock(block);
        }

        // The crown as a flaring band round the piece, from its foot in the water to its rim: each side at its own
        // height (heading, undulation; collapsed where it has fallen back), bowed outward, the fingers standing up
        // out of the rim as cusps.
        static void BuildCrown(Splash s, float t)
        {
            const int around = 72, rows = 6;
            var v = new Vector3[around * rows];
            var tri = new int[around * (rows - 1) * 6];
            float cusp = Cusp(s, t);
            float foot = PieceRadius + .45f * s.Ur * t;
            for (int i = 0; i < around; i++)
            {
                float a = i * Mathf.PI * 2f / around;
                float h = Mathf.Max(0f, s.Uy * s.Up(a) * t - .5f * Gravity * t * t);
                float rim = PieceRadius + s.Ur * s.Out(a) * t;
                // The fingers near this angle (their width a third of their spacing).
                float bump = 0f;
                float width = .35f * Mathf.PI * 2f / s.FingerAngle.Length;
                for (int k = 0; k < s.FingerAngle.Length; k++)
                {
                    float d = Mathf.DeltaAngle(a * Mathf.Rad2Deg, s.FingerAngle[k] * Mathf.Rad2Deg) * Mathf.Deg2Rad / width;
                    if (d * d < 9f) bump += s.FingerStrength[k] * Mathf.Exp(-d * d);
                }
                for (int j = 0; j < rows; j++)
                {
                    float u = j / (rows - 1f);
                    float r = Mathf.Lerp(foot, rim, u * u) + .08f * Mathf.Sin(u * Mathf.PI) * Mathf.Clamp01(1f - t / s.CrownLife);
                    float y = h * u - .03f + (j == rows - 1 && h > .01f ? cusp * bump : 0f);
                    v[j * around + i] = s.At + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                }
            }
            int n = 0;
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < around; i++)
                {
                    int a = j * around + i, b = j * around + (i + 1) % around, c = a + around, d = b + around;
                    tri[n++] = a; tri[n++] = c; tri[n++] = b;
                    tri[n++] = b; tri[n++] = c; tri[n++] = d;
                }
            s.Crown.Clear();
            s.Crown.vertices = v;
            s.Crown.triangles = tri;
            s.Crown.RecalculateNormals();
            s.Crown.RecalculateBounds();
        }

        // ------------------------------------------------------------------ rings for the water shader

        // A drop falling back in: a small ring, stronger for a bigger drop.
        void Plip(Vector3 at, float radius)
        {
            if (plips.Count >= MaxPlips) plips.RemoveAt(0);
            plips.Add(new Ring
            {
                X = at.x, Z = at.z, Y = at.y, Start = Time.timeSinceLevelLoad, Strength = Mathf.Clamp(radius / .03f * .3f, .05f, .35f),
                Scale = .35f, Churn = 0f, Life = 2.5f,
            });
        }

        // World xz, start (the shader's _Time.y), strength; height, wavelength scale, churn. Arrays always 16 long
        // (Unity keeps a global array's first size; R444 renamed them from R442's 8-long _GardenSplash*).
        void Publish()
        {
            float now = Time.timeSinceLevelLoad;
            int n = Gather(mainRings, now, 0);
            n = Gather(plips, now, n);
            for (int i = n; i < ringData.Length; i++)
            {
                ringData[i] = new Vector4(0f, 0f, now - 100f, 0f);
                ringInfo[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(RingId, ringData);
            Shader.SetGlobalVectorArray(RingInfoId, ringInfo);
            Shader.SetGlobalFloat(RingCountId, n);
        }

        // Drops the rings that have died out and copies the rest into the arrays from `n` on; returns the new count.
        int Gather(List<Ring> list, float now, int n)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (now - list[i].Start > list[i].Life) list.RemoveAt(i);
            foreach (var r in list)
            {
                if (n == ringData.Length) break;
                ringData[n] = new Vector4(r.X, r.Z, r.Start, r.Strength);
                ringInfo[n] = new Vector4(r.Y, r.Scale, r.Churn, 0f);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ random numbers with the right shapes

        static float Gauss()
        {
            float u1 = Mathf.Max(1e-6f, 1f - Random.value), u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        static float LogNormal(float mu, float sigma) => Mathf.Exp(mu + sigma * Gauss());

        static float Exponential(float mean) => -mean * Mathf.Log(Mathf.Max(1e-6f, 1f - Random.value));

        static int Poisson(float lambda)
        {
            if (lambda > 30f) return Mathf.Max(0, Mathf.RoundToInt(lambda + Mathf.Sqrt(lambda) * Gauss()));
            float limit = Mathf.Exp(-lambda), p = 1f;
            int k = 0;
            do
            {
                k++;
                p *= Random.value;
            } while (p > limit);
            return k - 1;
        }

        // An angle round `mean`, more often near it the larger kappa (von Mises, by rejection).
        static float VonMises(float mean, float kappa)
        {
            if (kappa < .01f) return Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 64; i++)
            {
                float a = (Random.value * 2f - 1f) * Mathf.PI;
                if (Random.value < Mathf.Exp(kappa * (Mathf.Cos(a) - 1f))) return mean + a;
            }
            return mean;
        }

        // A unit sphere (diameter 1) for the drops, the jet and the bubbles.
        static Mesh MakeSphere(int around, int rings)
        {
            var v = new List<Vector3>();
            var nrm = new List<Vector3>();
            for (int j = 0; j <= rings; j++)
            {
                float p = Mathf.PI * j / rings;
                for (int i = 0; i <= around; i++)
                {
                    float a = 2f * Mathf.PI * i / around;
                    var d = new Vector3(Mathf.Sin(p) * Mathf.Cos(a), Mathf.Cos(p), Mathf.Sin(p) * Mathf.Sin(a));
                    v.Add(d * .5f);
                    nrm.Add(d);
                }
            }
            var t = new List<int>();
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < around; i++)
                {
                    int a = j * (around + 1) + i, b = a + 1, c = a + around + 1, d = c + 1;
                    t.Add(a); t.Add(b); t.Add(c);
                    t.Add(b); t.Add(d); t.Add(c);
                }
            var mesh = new Mesh { name = "Splash sphere" };
            mesh.SetVertices(v);
            mesh.SetNormals(nrm);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
