using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R505 D portcullis court sounds (user, 11-07: "sfx랑 vfx도 해줘"). ElevenLabs sound effects in
    // Resources/PawnRushMiniGameSfx (flow OxNVtUEs0oTwmKL6ybXH): the capstan's creak and the chain over the pulley as
    // loops while the bars turn, louder and a little higher the faster they go. On the capstan's own bars
    // ("Capstan bars"), so every PC hears what it sees turning.
    public sealed class MiniGameCapstanSound : MonoBehaviour
    {
        // the drum's wood (take woodB 4), the chain winding onto the drum (chainB 3) and over the pulley (chainC 3),
        // all picked by the user on 11-07 (the first chain take jingled: "쩅그랑 쩅그랑")
        const string Creak = "PawnRushMiniGameSfx/sfx_capstan_turn", Chain = "PawnRushMiniGameSfx/sfx_capstan_chain";
        const string Wind = "PawnRushMiniGameSfx/sfx_capstan_chain_drum";
        const float FullSpeed = 30f;     // degrees a second that count as a full push (MgCapstan.MaxTurnRate .13 turns/s = 47)

        AudioSource creak, chain, wind;
        float lastYaw, speed;
        bool started;

        public void Setup(Transform pulleyWheel)
        {
            creak = Loop(transform.parent != null ? transform.parent : transform, "Capstan creak", Creak, 3f, 30f);
            wind = Loop(transform.parent != null ? transform.parent : transform, "Capstan chain wind", Wind, 3f, 28f);
            chain = Loop(pulleyWheel != null ? pulleyWheel : transform, "Pulley chain", Chain, 3f, 35f);
        }

        static AudioSource Loop(Transform at, string name, string clipName, float near, float far)
        {
            var clip = Resources.Load<AudioClip>(clipName);
            if (clip == null)
            {
                Debug.LogWarning("[미니게임 모델] 캡스턴 소리 파일이 없어 재생하지 않습니다: Resources/" + clipName);
                return null;
            }
            var go = new GameObject(name);
            go.transform.SetParent(at, false);
            var a = go.AddComponent<AudioSource>();
            a.clip = clip; a.loop = true; a.playOnAwake = false;
            a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = near; a.maxDistance = far;
            a.dopplerLevel = 0f; a.volume = 0f;
            SectionSoundGate.Gate(a);   // R529
            return a;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float yaw = transform.localEulerAngles.y;
            if (!started) { lastYaw = yaw; started = true; }
            float now = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / Mathf.Max(1e-4f, Time.deltaTime);
            lastYaw = yaw;
            speed = Mathf.Lerp(speed, now, 1f - Mathf.Exp(-6f * Time.deltaTime));   // smooth the network's steps
            float k = Mathf.Clamp01(speed / FullSpeed);
            Drive(creak, k, .9f);
            Drive(chain, k, .75f);
            Drive(wind, k, .7f);
        }

        static void Drive(AudioSource a, float k, float gain)
        {
            if (a == null) return;
            a.volume = gain * Mathf.SmoothStep(0f, 1f, k);
            a.pitch = Mathf.Lerp(.85f, 1.08f, k);
            if (k > .02f && !a.isPlaying)
            {
                a.time = Random.value * a.clip.length;
                a.Play();
            }
            else if (k <= .005f && a.isPlaying) a.Pause();
        }
    }

    // The D team gate's moments, in place of the shared ones (ArenaDressing, IGateFx). Pictures and sound only,
    // played by whoever sees them:
    //  Step()    every full capstan turn (MiniGamePortcullisKick): an iron clank with the chain snapping taut
    //            (sfx_portcullis_step), dust puffing from the bars' foot, the side grooves and the slot over them, grit
    //            falling from the arch.
    //  Opened()  the gate hauled up over OpenSeconds (TeamGate.Seconds, set by MiniGameModels): the chain take the
    //            user chose (ElevenLabs haul 2, 4 s, sfx_portcullis_open) plays for exactly that long (a shorter take would loop), its volume following
    //            the gate's speed (TeamGate's SmoothStep), with the dust, grit and groove sparks spread over the rise
    //            (user, 11-07: the sound and the opening did not match).
    //  Quarter() the shared 25/50/75 % sparkle (no sound), plus D's own: dust and groove sparks that grow with each
    //            quarter (no ground ring, in any mini-game).
    // Dust: E's soft puff texture (Resources/PawnRushMiniGames/Fx/T_Door_Dust) on Lit Transparent, tinted the
    // court's cream stone; sparks: the mote texture on the additive particle material in HDR orange; grit: small
    // stone chips.
    public sealed class MiniGameCourtGateFx : MonoBehaviour, IGateFx
    {
        const string Fx = "PawnRushMiniGames/Fx/";
        const string StepSound = "PawnRushMiniGameSfx/sfx_portcullis_step", OpenSound = "PawnRushMiniGameSfx/sfx_portcullis_open";
        const float HalfWidth = 2.2f, SlotUp = 4.9f;           // the opening's half width and the bars' top (m)
        public const float OpenSeconds = 4f;                   // the D gate's full opening (TeamGate.Seconds)
        static readonly Color Dust = new Color(.80f, .73f, .62f, 1f);
        static readonly Color Spark = new Color(3.2f, 1.5f, .45f, 1f);
        static readonly Color Grit = new Color(.72f, .64f, .53f, 1f);
        static Material additive, blended, chip;
        static Texture2D dustTex, moteTex;
        static AudioClip stepClip, openClip;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { additive = blended = chip = null; dustTex = moteTex = null; stepClip = openClip = null; loaded = false; }

        sealed class Bit
        {
            public Transform T;
            public Renderer R;
            public int Kind;                        // 0 dust, 1 spark, 2 grit
            public Vector3 From, Velocity, Spin;
            public float Delay, Life, Size, Phase;
        }

        readonly List<Bit> bits = new List<Bit>();
        MaterialPropertyBlock block;
        Transform root;
        AudioSource voice, haul, haul2;     // haul2: the same take in step, about twice as loud (user, 11-07: "더 크게")
        float haulAge = -1f;
        System.Random rng = new System.Random();

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public void Quarter(int quarter)
        {
            var station = GetComponent<MissionStation>();
            if (station == null) return;
            var gate = station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f));
            // no ground ring and no quarter sound, as everywhere now (ArenaDressing; user, 11-07); the sparkle stays
            ArenaFx.Burst(gate + Vector3.up * 5.6f, new[] { Arena.Gold }, 10, 2.5f, 2f, .9f, .1f);
            // D's own quarter moment (user, 11-07, via the C session: each mini-game its own, no ring): the chain
            // pulls the bars harder, so the grooves throw more dust and iron sparks, more at each quarter. The
            // full-turn clank that lands on the same turn is the portcullis's own sound, not a quarter sound.
            Play(false, quarter, false);
        }

        public void Step() => Play(false);

        public void Opened() => Play(true);

        void Play(bool open, int quarter = 0, bool sound = true)
        {
            var station = GetComponent<MissionStation>();
            if (station == null || station.Gate == null || !Application.isPlaying) return;
            Load();
            var gate = station.Gate.transform;
            Vector3 right = transform.right, forward = transform.forward;
            float floor = transform.position.y;
            // the opening's foot, in front of the bars (the court's side, -forward)
            var foot = new Vector3(gate.position.x, floor, gate.position.z) - forward * .35f;
            if (open && openClip != null)
            {
                AudioSource Haul()
                {
                    var a = gate.gameObject.AddComponent<AudioSource>();
                    a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
                    a.minDistance = 15f; a.maxDistance = 80f; a.dopplerLevel = 0f;   // full volume across the station
                    SectionSoundGate.Gate(a);   // R529
                    return a;
                }
                if (haul == null) haul = Haul();
                if (haul2 == null) haul2 = Haul();
                // a take as long as the opening plays once as it is; a short one loops (user, 11-07: haul take 2, 4 s)
                foreach (var a in new[] { haul, haul2 })
                {
                    MiniGameEar.Fit(a, station);           // the same anywhere in D (user 11-08)
                    a.clip = openClip; a.loop = openClip.length < OpenSeconds * .9f; a.volume = 0f; a.pitch = 1f; a.time = 0f; a.Play();
                }
                haulAge = 0f;
            }
            var clip = open || !sound ? null : stepClip;
            if (clip != null)
            {
                if (voice == null)
                {
                    voice = gate.gameObject.AddComponent<AudioSource>();
                    voice.playOnAwake = false; voice.spatialBlend = 1f; voice.rolloffMode = AudioRolloffMode.Linear;
                    voice.minDistance = 5f; voice.maxDistance = 50f; voice.dopplerLevel = 0f;
                    SectionSoundGate.Gate(voice);   // R529
                }
                voice.pitch = open ? 1f : R(.95f, 1.05f);
                voice.PlayOneShot(clip, open ? 1f : .85f);
            }
            if (blended == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            if (root == null) root = new GameObject("Court gate FX").transform;
            int dust = open ? 26 : quarter > 0 ? 6 + 4 * quarter : 9, grit = open ? 22 : quarter > 0 ? 4 * quarter : 8;
            int sparks = open ? 24 : 6 * quarter;
            for (int i = 0; i < dust; i++)
            {
                int where = i % 3;                                  // 0 floor, 1 grooves, 2 the slot over the bars
                Vector3 at, v;
                if (where == 0)
                {
                    at = foot + right * R(-HalfWidth, HalfWidth) + Vector3.up * R(.05f, .3f);
                    v = -forward * R(.3f, .9f) + right * R(-.4f, .4f) + Vector3.up * R(.1f, .35f);
                }
                else if (where == 1)
                {
                    float side = R(0f, 1f) < .5f ? -1f : 1f;
                    at = foot + right * side * HalfWidth + Vector3.up * R(.5f, open ? 4.6f : 3f);
                    v = -forward * R(.15f, .5f) + right * side * R(.05f, .25f) + Vector3.up * R(-.1f, .15f);
                }
                else
                {
                    at = foot + right * R(-HalfWidth, HalfWidth) + Vector3.up * SlotUp;
                    v = -forward * R(.2f, .5f) + Vector3.up * R(-.35f, -.1f);
                }
                Add(0, dustTex, blended, at, v, R(0f, open ? OpenSeconds * .8f : .12f), R(1.3f, open ? 2.8f : 1.9f), R(.7f, open ? 1.6f : 1.1f));
            }
            for (int i = 0; i < grit; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.name = "Grit";
                go.transform.SetParent(root, false);
                var r = go.GetComponent<Renderer>();
                if (chip != null) r.sharedMaterial = chip;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.enabled = false;
                bits.Add(new Bit { T = go.transform, R = r, Kind = 2, From = foot + right * R(-HalfWidth, HalfWidth) + Vector3.up * R(SlotUp, SlotUp + .6f) - forward * R(0f, .3f),
                                   Velocity = -forward * R(.1f, .6f) + right * R(-.3f, .3f), Delay = R(0f, open ? OpenSeconds * .8f : .2f), Life = 1.6f,
                                   Size = R(.04f, .09f), Spin = new Vector3(R(-400f, 400f), R(-400f, 400f), R(-400f, 400f)), Phase = 0f });
            }
            for (int i = 0; i < sparks; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var at = foot + right * side * HalfWidth + Vector3.up * R(1f, 4.4f);
                Add(1, moteTex, additive, at, -forward * R(.6f, 1.6f) + right * side * R(.2f, .8f) + Vector3.up * R(.2f, 1.2f),
                    open ? R(.3f, OpenSeconds * .85f) : R(0f, .35f), R(.35f, .7f), R(.05f, .1f));
            }
        }

        void Add(int kind, Texture2D tex, Material m, Vector3 from, Vector3 velocity, float delay, float life, float size)
        {
            if (m == null || tex == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = kind == 0 ? "Dust" : "Spark";
            go.transform.SetParent(root, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            block.Clear(); block.SetTexture("_BaseMap", tex); block.SetColor("_BaseColor", Color.clear); r.SetPropertyBlock(block);
            r.enabled = false;
            bits.Add(new Bit { T = go.transform, R = r, Kind = kind, From = from, Velocity = velocity, Delay = delay, Life = life, Size = size, Phase = Random.value * 6.3f });
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var a = Resources.Load<Material>("URPVariants/Particles Additive");
            var b = Resources.Load<Material>("URPVariants/Lit Transparent");
            dustTex = Resources.Load<Texture2D>(Fx + "T_Door_Dust");
            moteTex = Resources.Load<Texture2D>(Fx + "T_Door_Mote");
            stepClip = Resources.Load<AudioClip>(StepSound);
            openClip = Resources.Load<AudioClip>(OpenSound);
            if (a != null && b != null && dustTex != null && moteTex != null)
            {
                additive = new Material(a) { name = "Court gate FX sparks" };
                blended = new Material(b) { name = "Court gate FX dust" };
            }
            else Debug.LogWarning("[미니게임 모델] 쇠살문 효과 재질·텍스처가 없어 그리지 않습니다: Resources/" + Fx);
            var stone = Resources.Load<Material>("PawnRushMiniGames/Materials/PR_Early_DarkIron");
            chip = stone != null ? new Material(stone) { name = "Court gate FX grit" } : null;
            if (chip != null) { chip.SetColor("_BaseColor", Grit); chip.SetFloat("_Metallic", 0f); chip.SetFloat("_Smoothness", .05f); }
            if (stepClip == null) Debug.LogWarning("[미니게임 모델] 쇠살문 소리 파일이 없어 재생하지 않습니다: Resources/" + StepSound);
            if (openClip == null) Debug.LogWarning("[미니게임 모델] 쇠살문 소리 파일이 없어 재생하지 않습니다: Resources/" + OpenSound);
        }

        void Update()
        {
            if (haulAge >= 0f && haul != null)
            {
                // the gate's speed through its SmoothStep rise (peak at the middle), never quite silent while it moves
                haulAge += Time.deltaTime;
                float k = haulAge / OpenSeconds;
                if (k >= 1f) { haul.Stop(); if (haul2 != null) haul2.Stop(); haulAge = -1f; }
                else
                {
                    float speed = 6f * k * (1f - k) / 1.5f;
                    if (haul.loop)
                    {
                        haul.volume = Mathf.Lerp(.35f, 1f, speed) * Mathf.Clamp01(k / .05f) * Mathf.Clamp01((1f - k) / .08f);
                        haul.pitch = Mathf.Lerp(.9f, 1.06f, speed);
                    }
                    else haul.volume = Mathf.Lerp(.85f, 1f, speed) * Mathf.Clamp01((1f - k) / .05f);
                    if (haul2 != null) { haul2.volume = haul.volume; haul2.pitch = haul.pitch; }
                }
            }
            if (bits.Count == 0) return;
            float dt = Time.deltaTime;
            var cam = Camera.main;
            float floor = transform.position.y;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                b.Delay -= dt;
                if (b.Delay > 0f) continue;
                float t = -b.Delay, k = t / b.Life;
                if (k >= 1f) { Destroy(b.T.gameObject); bits.RemoveAt(i); continue; }
                b.R.enabled = true;
                if (b.Kind == 2)
                {
                    // fall, bounce once on the floor, lie and shrink
                    var p = b.From + b.Velocity * t + Vector3.down * (4.9f * t * t);
                    if (p.y < floor + b.Size * .5f) p.y = floor + b.Size * .5f;
                    b.T.position = p;
                    b.T.rotation = Quaternion.Euler(b.Spin * Mathf.Min(t, .6f));
                    b.T.localScale = Vector3.one * b.Size * (k > .75f ? (1f - k) / .25f : 1f);
                    continue;
                }
                float ease = 1f - (1f - k) * (1f - k);
                var at = b.From + b.Velocity * t;
                if (b.Kind == 1) at += Vector3.down * (2.5f * t * t);           // sparks arc down
                b.T.position = at;
                if (cam != null) b.T.rotation = Quaternion.LookRotation(b.T.position - cam.transform.position, cam.transform.up);
                Color c;
                if (b.Kind == 0)
                {
                    b.T.localScale = Vector3.one * b.Size * Mathf.Lerp(.5f, 1.9f, ease);
                    float a = (k < .15f ? k / .15f : (1f - k) / .85f) * .55f;
                    c = new Color(Dust.r, Dust.g, Dust.b, a);
                }
                else
                {
                    b.T.localScale = Vector3.one * b.Size;
                    float a = (1f - k) * (.75f + .25f * Mathf.Sin(b.Phase + t * 30f));
                    c = new Color(Spark.r * a, Spark.g * a, Spark.b * a, a);   // premultiplied for the additive material
                }
                block.Clear(); b.R.GetPropertyBlock(block); block.SetColor("_BaseColor", c); b.R.SetPropertyBlock(block);
            }
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }

    // The capstan's "turn this way" sign as an effect (user, 11-08: "바닥에 그려지는 t 자형 빼주고 돌리라는 화살표 표시를
    // vfx 로 해"): the game's eight flat gold T marks ("Way round", "Way round tip") stop drawing, and twelve soft gold
    // chevrons lie on the floor round the capstan (radius 2.6 m, as the marks were) and keep flowing the way it turns,
    // each pulsing a little; they fade out once the game is done. Texture: Resources/PawnRushMiniGames/Fx/T_Turn_Chevron
    // (ArtSource/PawnRush/MiniGames/Early/Materials/r505_turn_chevron.py) on the additive particle material, HDR gold so
    // the bloom takes it. Pictures only, from the station's own state, the same on every PC.
    public sealed class MiniGameTurnArrows : MonoBehaviour
    {
        const int Count = 12;
        const float Radius = 2.6f, Size = .62f, Speed = .45f;      // m, m, radians a second along the ring
        static readonly Color Gold = new Color(2.6f, 1.75f, .55f, 1f);
        static Material additive;
        static Texture2D chevron;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { additive = null; chevron = null; loaded = false; }

        MissionStation station;
        float sign = 1f, fade = 1f;
        readonly List<Renderer> quads = new List<Renderer>();
        MaterialPropertyBlock block;

        // centre: the capstan axis on the floor (this transform's parent space); turn: +1 or -1 as the marks pointed.
        public void Setup(MissionStation team, float turn)
        {
            station = team; sign = turn;
            if (!loaded)
            {
                loaded = true;
                var a = Resources.Load<Material>("URPVariants/Particles Additive");
                chevron = Resources.Load<Texture2D>("PawnRushMiniGames/Fx/T_Turn_Chevron");
                if (a != null && chevron != null) additive = new Material(a) { name = "Capstan turn arrows" };
                else Debug.LogWarning("[미니게임 모델] 캡스턴 화살표 효과 재질·텍스처가 없어 그리지 않습니다");
            }
            if (additive == null || quads.Count > 0) return;
            block = new MaterialPropertyBlock();
            for (int i = 0; i < Count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = "Turn arrow";
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * Size;
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = additive;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                block.Clear(); block.SetTexture("_BaseMap", chevron); block.SetColor("_BaseColor", Color.clear); r.SetPropertyBlock(block);
                quads.Add(r);
            }
            Place(0f);
        }

        void Update()
        {
            if (quads.Count == 0) return;
            var game = station != null ? station.Game : null;
            fade = Mathf.MoveTowards(fade, game != null && game.Completed ? 0f : 1f, Time.deltaTime);
            Place(Time.time);
        }

        void Place(float time)
        {
            for (int i = 0; i < quads.Count; i++)
            {
                float a = i * Mathf.PI * 2f / Count + sign * time * Speed;
                var radial = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var tangent = Vector3.Cross(Vector3.up, radial) * sign;
                var t = quads[i].transform;
                t.localPosition = radial * Radius;
                // flat on the floor, visible from above, the chevron's point (+V) along the way round
                t.localRotation = Quaternion.LookRotation(Vector3.down, tangent);
                float pulse = .55f + .45f * Mathf.Sin(time * 3.2f - i * .9f);
                float k = fade * pulse;
                block.Clear(); quads[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(Gold.r * k, Gold.g * k, Gold.b * k, k));   // premultiplied
                quads[i].SetPropertyBlock(block);
                quads[i].enabled = k > .01f;
            }
        }
    }
}
