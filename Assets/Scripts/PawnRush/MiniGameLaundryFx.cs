using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A station's own water-game moments in place of the shared confetti bursts and rings (ArenaFx) that the knight pads
    // and the water return play: MgKnightPads and MgWaterReturn ask the station for one and fall back to ArenaFx when it
    // has none. Pictures only; the sounds stay the games'.
    public interface IWaterGameFx
    {
        void PadLaunch(Vector3 at);                  // a pad's high jump, at the pad's middle
        void PieceTaken(Vector3 at, Vector3 away);   // a piece knocked off, at its base, flying `away` (flat, unit)
        void Splash(Vector3 at, float strength);     // something falls into the water, at the surface (pawn 1, piece ~0.7)
    }

    // J · 왕궁 세탁장 effects (R509), on each station, pictures only (no sound yet, no gameplay, nothing sent: every PC
    // plays them from what it already sees). User 11-08: "vfx적용 시켜줘", then "점프 뛸 때랑 물에 떨어질 때 인형 넘어
    // 뜨릴 때 똑같은 vfx 가 나오잖아 좀 더 실사로 해" - so each moment has its own, realistic-looking picture:
    //   - jump (IWaterGameFx.PadLaunch): a jet of soapy water shoots up from the brass nozzle (streaked droplets that
    //     arc and fall), a puff of spray mist, suds spreading on the lid, a few bubbles;
    //   - fall into the water (Splash): a crown of streaked droplets flung out and up, a centre column, spray mist,
    //     ripples running out over the surface, foam spreading and thinning on the water, bubbles surfacing and popping;
    //   - doll knocked off (PieceTaken): wisps of cotton stuffing and lint fibres burst from the cloth and drift down,
    //     a spray of soapy water thrown the way it flies; its fall into the water is a smaller Splash;
    //   - every nozzle keeps sending up a trickle of soap bubbles (with the gold pad ring gone, user 11-08: "이런 원형으로
    //     된 노란색 하지 말고 다른 거 해줘", the bubbling nozzle marks where to jump);
    //   - the team gate (IGateFx, in place of the shared team beam and burst): at 25 / 50 / 75 % a puff of bubbles under
    //     the sluice gate; when it opens, soapy water gushes from under the rising sluice for 2.5 s.
    // All pictures are drawn into small textures here (no files). Billboards face the camera; droplets stretch along
    // their flight; ripples and foam on the water lie flat.
    public sealed class MiniGameLaundryFx : MonoBehaviour, IGateFx, IWaterGameFx
    {
        const int MaxBits = 520;
        static Material litMat;
        static Texture2D bubbleTex, foamTex, dropTex, mistTex, rippleTex, patchTex, lintTex;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { litMat = null; bubbleTex = foamTex = dropTex = mistTex = rippleTex = patchTex = lintTex = null; loaded = false; }

        enum Kind { Bubble, Foam, Drop, Mist, Ripple, Patch, Lint, SurfaceBubble }

        sealed class Bit
        {
            public Transform T;
            public Renderer R;
            public Kind Kind;
            public Vector3 Pos, Vel;
            public float Age, Delay, Life, Size, Grow, Phase, Spin, Floor;
            public Color Tint;
            public bool Live;
        }

        readonly List<Bit> bits = new List<Bit>();
        MaterialPropertyBlock block;
        Transform root;
        MissionStation station;
        Transform[] pads;
        float[] padNext;
        float gushUntil = -1f, gushNext;
        static readonly Color Soap = new Color(.86f, .97f, 1f, 1f), Water = new Color(.82f, .95f, .97f, 1f), Cotton = new Color(.97f, .96f, .93f, 1f);

        void Start()
        {
            station = GetComponent<MissionStation>();
            Load();
            block = new MaterialPropertyBlock();
            // world-space bits: kept off the station's transform (black's station is mirrored, which would flip the quads)
            root = new GameObject("Laundry FX " + name).transform;
        }

        bool Ready() => Application.isPlaying && litMat != null && root != null;
        static float R(float a, float b) => Random.Range(a, b);
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static Vector3 Around() { float a = R(0f, 6.2832f); return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }

        // ------------------------------------------------------------------ the moments

        public void PadLaunch(Vector3 at)
        {
            if (!Ready()) return;
            var top = at + Vector3.up * .14f;
            for (int i = 0; i < 30; i++)                    // the jet: fast streaks up, a narrow cone, falling back
            {
                var side = Around() * R(0f, .9f);
                Spawn(Kind.Drop, top + side * .12f, Vector3.up * R(4f, 7.5f) + side * R(.4f, 1.3f), R(.9f, 1.4f), R(.035f, .065f), Water, delay: R(0f, .12f), floor: at.y);
            }
            for (int i = 0; i < 4; i++)                     // spray mist at the nozzle, drifting up
                Spawn(Kind.Mist, top + Vector3.up * R(0f, .4f), Vector3.up * R(.6f, 1.6f) + Around() * R(0f, .3f), R(.7f, 1.1f), R(.3f, .5f), Water, grow: 2.4f, delay: R(0f, .1f));
            Spawn(Kind.Patch, new Vector3(at.x, at.y + .02f, at.z), Vector3.zero, 1.4f, .7f, Soap, grow: 1.9f);   // suds on the lid
            for (int i = 0; i < 6; i++)
                Spawn(Kind.Bubble, top + Around() * R(.1f, .3f), Vector3.up * R(.8f, 1.6f) + Around() * R(.1f, .4f), R(1.2f, 2f), R(.06f, .14f), Soap);
        }

        public void Splash(Vector3 at, float strength)
        {
            if (!Ready()) return;
            float s = Mathf.Clamp(strength, .3f, 1.5f);
            int crown = Mathf.RoundToInt(40 * s);
            for (int i = 0; i < crown; i++)                 // the crown: flung out and up round the hole, falling back
            {
                var d = Around();
                Spawn(Kind.Drop, at + d * R(.15f, .4f) * s, d * R(1.1f, 2.6f) * s + Vector3.up * R(2.2f, 4.4f) * s, R(.8f, 1.2f), R(.04f, .08f), Water, delay: R(0f, .06f), floor: at.y);
            }
            for (int i = 0; i < Mathf.RoundToInt(9 * s); i++)   // the centre column, a beat later
                Spawn(Kind.Drop, at + Around() * R(0f, .1f), Vector3.up * R(3.6f, 5.6f) * s + Around() * R(0f, .4f), R(.9f, 1.3f), R(.05f, .09f), Water, delay: R(.12f, .22f), floor: at.y);
            for (int i = 0; i < 4; i++)
                Spawn(Kind.Mist, at + Vector3.up * .2f + Around() * R(0f, .3f), Vector3.up * R(.4f, 1.1f) + Around() * R(.2f, .6f), R(.8f, 1.2f), R(.5f, .8f) * s, Water, grow: 2.2f, delay: R(0f, .1f));
            var flat = new Vector3(at.x, at.y + .015f, at.z);
            for (int i = 0; i < 3; i++)                     // ripples running out over the surface
                Spawn(Kind.Ripple, flat + Vector3.up * (.002f * i), Vector3.zero, R(1.4f, 1.9f), .35f * s, Water, grow: 7f + 2f * i, delay: .12f * i);
            for (int i = 0; i < 3; i++)                     // foam spreading and thinning on the water
                Spawn(Kind.Patch, flat + Vector3.up * (.006f + .002f * i) + Around() * R(0f, .3f), Vector3.zero, R(2.2f, 3.2f), R(.6f, 1f) * s, Soap, grow: 2.6f, delay: R(.05f, .25f));
            for (int i = 0; i < Mathf.RoundToInt(12 * s); i++)   // bubbles surfacing and popping
                Spawn(Kind.SurfaceBubble, flat + Vector3.up * .02f + Around() * R(.1f, 1f) * s, Vector3.up * .03f, R(.6f, 1.6f), R(.05f, .12f), Soap, delay: R(.2f, 1.2f));
        }

        public void PieceTaken(Vector3 at, Vector3 away)
        {
            if (!Ready()) return;
            var c = at + Vector3.up * .65f;
            away = away.sqrMagnitude > 1e-4f ? Flat(away).normalized : Around();
            for (int i = 0; i < 9; i++)                     // wisps of cotton stuffing puffing out of the cloth
                Spawn(Kind.Foam, c + Random.insideUnitSphere * .3f, (away * R(.4f, 1.4f) + Random.insideUnitSphere * .8f + Vector3.up * .4f),
                      R(.7f, 1.1f), R(.18f, .32f), Cotton, grow: 2.2f, delay: R(0f, .08f));
            for (int i = 0; i < 18; i++)                    // lint fibres fluttering down
                Spawn(Kind.Lint, c + Random.insideUnitSphere * .35f, away * R(.5f, 2f) + Random.insideUnitSphere * 1.4f + Vector3.up * R(.6f, 1.6f),
                      R(1.4f, 2.4f), R(.06f, .13f), Cotton, floor: at.y);
            for (int i = 0; i < 14; i++)                    // soapy water shaken off the wet doll
                Spawn(Kind.Drop, c + Random.insideUnitSphere * .25f, away * R(1.2f, 3f) + Random.insideUnitSphere * 1.2f + Vector3.up * R(1f, 2.6f),
                      R(.7f, 1.1f), R(.03f, .06f), Water, floor: at.y - 1f);
        }

        public void Quarter(int quarter)
        {
            if (!Ready() || station == null || station.Gate == null) return;
            var (bottom, across, room) = GateFrame();
            for (int i = 0; i < 14; i++)
                Spawn(Kind.Bubble, bottom + across * R(-1.8f, 1.8f) + room * .2f, Vector3.up * R(.5f, 1.1f) + room * R(.1f, .5f), R(1.6f, 2.6f), R(.08f, .2f), Soap);
            for (int i = 0; i < 6; i++)
                Spawn(Kind.Foam, bottom + across * R(-1.6f, 1.6f) + room * .3f, room * R(.2f, .5f), R(1f, 1.6f), R(.35f, .6f), Soap, grow: 1.6f);
        }

        public void Opened()
        {
            if (!Ready() || station == null || station.Gate == null) return;
            gushUntil = Time.time + 2.5f;
            gushNext = Time.time;
        }

        // The gate's bottom middle (it rises, so the bottom is read each frame), its width direction and the room side.
        (Vector3 bottom, Vector3 across, Vector3 room) GateFrame()
        {
            var g = station.Gate.transform;
            var room = -station.transform.forward;
            var across = station.transform.right;
            float floor = station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight, 0f)).y;
            var bottom = new Vector3(g.position.x, floor + .05f, g.position.z) + room * .25f;
            return (bottom, across, room);
        }

        bool Watch()
        {
            if (station == null || station.Game == null) return false;
            if (pads != null && pads.Length > 0 && pads[0] != null) return true;
            var p = new List<Transform>();
            foreach (var t in station.Game.transform.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Jump pad ")) p.Add(t);
            pads = p.ToArray();
            padNext = new float[pads.Length];
            for (int i = 0; i < pads.Length; i++) padNext[i] = Time.time + Random.value * 1.2f;
            return pads.Length > 0;
        }

        void Update()
        {
            if (!Ready()) return;
            float now = Time.time;
            if (Watch())
                // every nozzle bubbles: a small bubble or two now and then, straight up over the nozzle
                for (int i = 0; i < pads.Length; i++)
                {
                    var pad = pads[i];
                    if (pad == null || now < padNext[i] || !pad.gameObject.activeInHierarchy) continue;
                    padNext[i] = now + R(.45f, 1.1f);
                    int n = Random.value < .3f ? 2 : 1;
                    for (int k = 0; k < n; k++)
                        Spawn(Kind.Bubble, pad.position + Vector3.up * .16f + new Vector3(R(-.12f, .12f), 0f, R(-.12f, .12f)),
                              Vector3.up * R(.35f, .65f) + new Vector3(R(-.05f, .05f), 0f, R(-.05f, .05f)), R(1.8f, 3.2f), R(.06f, .14f), Soap);
                }
            // the gate's gush
            if (now < gushUntil && station != null && station.Gate != null && now >= gushNext)
            {
                gushNext = now + .05f;
                var (bottom, across, room) = GateFrame();
                float strength = Mathf.Lerp(1f, .35f, 1f - (gushUntil - now) / 2.5f);   // strong at first, easing off
                var at = bottom + across * R(-1.85f, 1.85f);
                Spawn(Kind.Foam, at, room * R(1.2f, 2.4f) * strength + Vector3.up * .05f, R(1.2f, 2f), R(.45f, .8f), Soap, grow: 1.8f);
                for (int d = 0; d < 2; d++)
                    Spawn(Kind.Drop, at + Vector3.up * R(.05f, .3f), room * R(1.5f, 3.2f) * strength + Vector3.up * R(1f, 2.4f), R(.6f, 1f), R(.04f, .07f), Water, floor: bottom.y - .05f);
                if (Random.value < .6f)
                    Spawn(Kind.Bubble, at + room * .3f, Vector3.up * R(.6f, 1.3f) + room * R(.3f, .9f), R(1.8f, 3f), R(.08f, .22f), Soap);
            }
            Step(Time.deltaTime);
        }

        void Spawn(Kind kind, Vector3 pos, Vector3 vel, float life, float size, Color tint, float grow = 1f, float delay = 0f, float floor = -1e6f)
        {
            Bit b = null;
            foreach (var x in bits)
                if (!x.Live) { b = x; break; }
            if (b == null)
            {
                if (bits.Count >= MaxBits) return;
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(go.GetComponent<Collider>());
                go.name = "Laundry bit";
                go.transform.SetParent(root, false);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = litMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                b = new Bit { T = go.transform, R = r };
                bits.Add(b);
            }
            b.Kind = kind; b.Pos = pos; b.Vel = vel; b.Age = 0f; b.Delay = delay; b.Life = life; b.Size = size; b.Grow = grow;
            b.Phase = Random.value * 6.3f; b.Spin = R(-200f, 200f); b.Floor = floor; b.Tint = tint; b.Live = true;
            b.R.enabled = false;
        }

        void Step(float dt)
        {
            var cam = Camera.main;
            foreach (var b in bits)
            {
                if (!b.Live) continue;
                if (b.Delay > 0f) { b.Delay -= dt; continue; }
                b.Age += dt;
                float k = b.Age / b.Life;
                if (k >= 1f) { b.Live = false; b.R.enabled = false; continue; }
                b.R.enabled = true;
                float ease = 1f - (1f - k) * (1f - k);
                Vector3 scale;
                float alpha;
                Texture2D tex;
                Quaternion rot = cam != null ? Quaternion.LookRotation(b.Pos - cam.transform.position, cam.transform.up) : Quaternion.identity;
                switch (b.Kind)
                {
                    case Kind.Bubble:
                        // floats up with a wobble, drags to a stop, pops (a quick swell and fade)
                        b.Vel *= 1f - .9f * dt;
                        b.Vel += Vector3.up * .15f * dt;
                        b.Pos += (b.Vel + new Vector3(Mathf.Sin(b.Phase + b.Age * 2.3f), 0f, Mathf.Cos(b.Phase + b.Age * 1.9f)) * .12f) * dt;
                        float pop = k > .9f ? (k - .9f) / .1f : 0f;
                        scale = Vector3.one * b.Size * Mathf.Min(1f, k * 8f) * (1f + .6f * pop);
                        alpha = .85f * (1f - pop);
                        tex = bubbleTex;
                        break;
                    case Kind.SurfaceBubble:
                        // sits on the water, swells a little and pops
                        b.Pos += b.Vel * dt;
                        float p2 = k > .85f ? (k - .85f) / .15f : 0f;
                        scale = Vector3.one * b.Size * Mathf.Min(1f, k * 6f) * (1f + .8f * p2);
                        alpha = .9f * (1f - p2);
                        tex = bubbleTex;
                        break;
                    case Kind.Foam:
                        // soft suds or cotton wisps: thrown out, slowed by the air, swelling and thinning
                        b.Vel *= 1f - 2.2f * dt;
                        b.Vel += Vector3.down * .25f * dt;
                        b.Pos += b.Vel * dt;
                        scale = Vector3.one * b.Size * Mathf.Lerp(.6f, b.Grow, ease);
                        alpha = (k < .12f ? k / .12f : (1f - k) / .88f) * .85f;
                        rot *= Quaternion.Euler(0f, 0f, b.Phase * 57f + b.Spin * .2f * b.Age);
                        tex = foamTex;
                        break;
                    case Kind.Mist:
                        b.Vel *= 1f - 1.5f * dt;
                        b.Pos += b.Vel * dt;
                        scale = Vector3.one * b.Size * Mathf.Lerp(.5f, b.Grow, ease);
                        alpha = (k < .15f ? k / .15f : (1f - k) / .85f) * .35f;
                        rot *= Quaternion.Euler(0f, 0f, b.Phase * 57f);
                        tex = mistTex;
                        break;
                    case Kind.Ripple:
                    case Kind.Patch:
                        // flat on the water (or the lid): a ripple runs out fast and thins; foam spreads slowly and breaks up
                        scale = Vector3.one * b.Size * Mathf.Lerp(1f, b.Grow, b.Kind == Kind.Ripple ? ease : Mathf.Sqrt(k));
                        alpha = b.Kind == Kind.Ripple ? (1f - k) * .55f : (k < .1f ? k / .1f : 1f - (k - .1f) / .9f) * .8f;
                        rot = Quaternion.Euler(90f, b.Phase * 57f, 0f);
                        tex = b.Kind == Kind.Ripple ? rippleTex : patchTex;
                        break;
                    case Kind.Lint:
                        // a fibre: thrown out, then floats and flutters down, turning
                        b.Vel *= 1f - 2.6f * dt;
                        b.Vel += Vector3.down * .9f * dt;
                        b.Pos += (b.Vel + new Vector3(Mathf.Sin(b.Phase + b.Age * 5f), 0f, Mathf.Cos(b.Phase + b.Age * 4f)) * .25f) * dt;
                        if (b.Pos.y < b.Floor + .02f) { b.Pos.y = b.Floor + .02f; b.Vel = Vector3.zero; }
                        scale = new Vector3(b.Size * .35f, b.Size, 1f);
                        alpha = k > .75f ? (1f - k) / .25f : 1f;
                        rot *= Quaternion.Euler(0f, 0f, b.Phase * 57f + b.Spin * b.Age);
                        tex = lintTex;
                        break;
                    default:
                        // a droplet: flies, falls, stretched along its flight; gone where it reaches the water or floor
                        b.Vel += Physics.gravity * dt;
                        b.Pos += b.Vel * dt;
                        if (b.Pos.y < b.Floor) { b.Live = false; b.R.enabled = false; continue; }
                        float speed = b.Vel.magnitude;
                        scale = new Vector3(b.Size, b.Size * (1f + speed * .35f), 1f);
                        alpha = (1f - k * k) * .9f;
                        if (cam != null && speed > .01f)
                            rot = Quaternion.LookRotation(b.Pos - cam.transform.position, b.Vel);
                        tex = dropTex;
                        break;
                }
                b.T.position = b.Pos;
                b.T.rotation = rot;
                b.T.localScale = scale;
                block.Clear();
                block.SetTexture("_BaseMap", tex);
                block.SetColor("_BaseColor", new Color(b.Tint.r, b.Tint.g, b.Tint.b, alpha));
                b.R.SetPropertyBlock(block);
            }
        }

        // ------------------------------------------------------------------ pictures, drawn once

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var lit = Resources.Load<Material>("URPVariants/Lit Transparent");
            if (lit == null)
            {
                Debug.LogWarning("[미니게임 모델] 세탁장 효과: URPVariants/Lit Transparent 재질이 없어 그리지 않습니다");
                return;
            }
            litMat = new Material(lit) { name = "Laundry FX" };
            bubbleTex = Draw("Laundry bubble", 64, (x, y) =>
            {
                // a thin bright rim, a faint film inside, a window highlight up-left
                float r = Mathf.Sqrt(x * x + y * y);
                float rim = Mathf.Clamp01(1f - Mathf.Abs(r - .86f) / .12f);
                float film = r < .86f ? .12f + .1f * r : 0f;
                float hx = x + .38f, hy = y - .38f;
                float spot = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / .2f);
                return new Color(1f, 1f, 1f, r > 1f ? 0f : Mathf.Clamp01(rim * .8f + film + spot));
            });
            foamTex = Draw("Laundry wisp", 64, (x, y) =>
            {
                // a soft lumpy cloud (suds, cotton stuffing): overlapping soft blobs, brighter tops
                float a = 0f;
                for (int i = 0; i < 7; i++)
                {
                    float ang = i * 2.39f, rr = .1f + .42f * ((i * 37 % 7) / 7f);
                    float cx = Mathf.Cos(ang) * rr, cy = Mathf.Sin(ang) * rr, s = .3f + .1f * (i % 3);
                    float d = ((x - cx) * (x - cx) + (y - cy) * (y - cy)) / (s * s);
                    a = Mathf.Max(a, Mathf.Exp(-d * 1.6f));
                }
                float v = .88f + .12f * Mathf.Clamp01(y * .5f + .5f);
                return new Color(v, v, v, a * Mathf.Clamp01(1.2f - Mathf.Sqrt(x * x + y * y)));
            });
            dropTex = Draw("Laundry drop", 32, (x, y) =>
            {
                // a clear drop: a bright edge, a clear middle, a glint
                float r = Mathf.Sqrt(x * x + y * y);
                float edge = Mathf.Clamp01(1f - Mathf.Abs(r - .75f) / .3f);
                float hx = x + .25f, hy = y - .3f;
                float glint = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / .25f);
                return new Color(1f, 1f, 1f, r > 1f ? 0f : Mathf.Clamp01(edge * .75f + .2f + glint));
            });
            mistTex = Draw("Laundry mist", 64, (x, y) =>
            {
                float r2 = x * x + y * y;
                float n = .75f + .25f * Mathf.Sin(x * 7f + Mathf.Sin(y * 5f) * 2f) * Mathf.Cos(y * 6f);
                return new Color(1f, 1f, 1f, Mathf.Exp(-r2 * 3.2f) * n);
            });
            rippleTex = Draw("Laundry ripple", 128, (x, y) =>
            {
                // a soft wave crest ring with a faint trough inside and a slightly broken edge
                float r = Mathf.Sqrt(x * x + y * y);
                float wob = .02f * Mathf.Sin(Mathf.Atan2(y, x) * 9f);
                float crest = Mathf.Exp(-Mathf.Pow((r - .88f - wob) / .05f, 2f));
                float trough = Mathf.Exp(-Mathf.Pow((r - .72f) / .08f, 2f)) * .25f;
                return new Color(1f, 1f, 1f, r > 1f ? 0f : Mathf.Clamp01(crest + trough));
            });
            patchTex = Draw("Laundry foam patch", 128, (x, y) =>
            {
                // floating foam: a lacework of little bubble cells, denser in the middle, broken at the edge
                float r = Mathf.Sqrt(x * x + y * y);
                float cells = 0f;
                for (int i = 0; i < 3; i++)
                {
                    float f = 9f + i * 7f;
                    float u = x * f + i * 1.7f, v = y * f + i * 3.1f;
                    float c = Mathf.Abs(Mathf.Sin(u) * Mathf.Sin(v + Mathf.Sin(u * .5f)));
                    cells = Mathf.Max(cells, Mathf.Clamp01((c - .55f) * 3f));
                }
                float body = Mathf.Clamp01(1f - r) * (1f + .35f * Mathf.Sin(Mathf.Atan2(y, x) * 5f + r * 6f));
                return new Color(1f, 1f, 1f, Mathf.Clamp01(body * 1.4f) * (.35f + .65f * cells));
            });
            lintTex = Draw("Laundry lint", 32, (x, y) =>
            {
                // a curled fibre along y
                float cx = .25f * Mathf.Sin(y * 2.6f);
                float d = Mathf.Abs(x - cx);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d / .12f) * Mathf.Clamp01((1f - Mathf.Abs(y)) * 3f));
            });
        }

        static Texture2D Draw(string name, int n, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    px[j * n + i] = f((i + .5f) / n * 2f - 1f, (j + .5f) / n * 2f - 1f);
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
