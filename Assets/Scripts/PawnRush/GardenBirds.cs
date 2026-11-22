using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R769 (승규 11-22, "블렌더에서 새 모델링 하여 실제로 날아다니는 연출을 넣어줘"): birds flying over the royal water garden -
    // the white royal doves and the small royal-blue swallows of birds_sample_v1.py (GardenBirds.fbx: per kind a body and
    // two wings, each wing's origin at its shoulder). Four flocks, each on its own closed path in the garden's frame:
    //   plaza doves    6, circling over the fountain plaza
    //   lake doves     5, a figure of eight over the canal walk, the drums and the lake
    //   swallows       4, darting low over the reservoir and the lake (a Lissajous curve: quick turns)
    //   high doves     3, a wide slow loop high over the whole garden
    // Every bird is a pure function of the shared obstacle clock (HANDOFF rule 7), so every PC sees the same sky: where
    // it is (the flock's path a little behind the leader, its own place in the flock, a slow wobble), which way it
    // heads (the way it moves), how it banks (into its turn, as hard as it turns), and how its wings go (flapping in
    // bouts and gliding between them, harder while climbing; the swallows quicker). The wings turn about the bird's
    // forward axis on pivots made at their shoulders, so the FBX needs no skeleton. Nothing to touch: no colliders.
    //  sound   each flock's wing flutter (WingFlutter), a quiet loop at the flock's middle, as loud as its wings beat
    // GardenPlazaSound adds this on waking (it sits on "Plaza sounds", whose frame is the garden's).
    public sealed class GardenBirds : MonoBehaviour
    {
        const string Model = "PawnRushGardenConcept/GardenBirds";
        const string Folder = "PawnRushGardenSfx/";
        const float WingGain = .35f;

        enum Path { Circle, Eight, Lissajous, Ellipse }

        sealed class FlockDef
        {
            public string kind;
            public int count;
            public Path path;
            public Vector3 middle;         // x, height, z in the garden's frame
            public Vector2 size;           // half-extents (x, z) of the path
            public float rise;             // how far it climbs and dips over a round
            public float period;           // s a round
            public float scale;            // the model's size
            public float flapHz, flapAngle, glideAngle, bankScale, spacing;
            // R770: how far the path wanders off its shape (m, slow), how much the pace swells and eases along it (0..1)
            public float wander, pace;
        }

        // R770 (승규 11-22, "흰색 움직임을 조금 더 빨리 해주고 움직임을 자연스럽게 해줘"): the doves fly about twice as fast as
        // R769's (3.9 .. 5.3 m/s, slow for a pigeon) - near 8 .. 9 m/s, a dove's easy cruise - and their paths wander.
        static readonly FlockDef[] Flocks =
        {
            new FlockDef { kind = "Dove", count = 6, path = Path.Circle, middle = new Vector3(0f, 13f, -35f), size = new Vector2(17f, 16f),
                rise = 2.5f, period = 13.5f, scale = 2.2f, flapHz = 5.2f, flapAngle = 46f, glideAngle = 14f, bankScale = 1.15f, spacing = 1.6f,
                wander = 3.5f, pace = .18f },
            new FlockDef { kind = "Dove", count = 5, path = Path.Eight, middle = new Vector3(0f, 17f, 8f), size = new Vector2(30f, 14f),
                rise = 3.5f, period = 21f, scale = 2.2f, flapHz = 5.2f, flapAngle = 46f, glideAngle = 14f, bankScale = 1.15f, spacing = 1.7f,
                wander = 4f, pace = .2f },
            new FlockDef { kind = "Swallow", count = 4, path = Path.Lissajous, middle = new Vector3(0f, 10.5f, 40f), size = new Vector2(24f, 8f),
                rise = 3f, period = 26f, scale = 2f, flapHz = 7f, flapAngle = 40f, glideAngle = -4f, bankScale = 1.5f, spacing = 1.4f,
                wander = 0f, pace = 0f },
            new FlockDef { kind = "Dove", count = 3, path = Path.Ellipse, middle = new Vector3(0f, 24f, 1f), size = new Vector2(36f, 46f),
                rise = 4f, period = 32f, scale = 2.2f, flapHz = 4.8f, flapAngle = 44f, glideAngle = 15f, bankScale = 1.15f, spacing = 2f,
                wander = 5f, pace = .15f },
        };

        // Where each bird sits in its flock: sideways, up, back (in spacings); a loose cluster, the first leading.
        static readonly Vector3[] Places =
        {
            new Vector3(0f, 0f, 0f), new Vector3(-1f, .2f, 1f), new Vector3(1.1f, -.15f, 1.2f), new Vector3(-.4f, .35f, 2.3f),
            new Vector3(.7f, .1f, 2.6f), new Vector3(-1.5f, -.2f, 2.8f),
        };

        sealed class Bird
        {
            public Transform root, wingL, wingR;
            public FlockDef flock;
            public Vector3 place;
            public float seed, lag;
        }

        sealed class Flock { public FlockDef def; public readonly List<Bird> birds = new List<Bird>(); public Transform at; public NearFarSound wings; }

        readonly List<Flock> flocks = new List<Flock>();

        void Start()
        {
            var model = Resources.Load<GameObject>(Model);
            if (model == null)
            {
                Debug.LogWarning("[정원] 새 모델을 불러오지 못했습니다: Resources/" + Model + ".fbx");
                return;
            }
            AudioClip flutter = Resources.Load<AudioClip>(Folder + "WingFlutter"), flutterFar = Resources.Load<AudioClip>(Folder + "WingFlutterFar");
            if (flutter == null) Debug.LogWarning("[정원] 소리 파일이 없습니다: Resources/" + Folder + "WingFlutter");
            var materials = new Dictionary<string, Material>();
            var group = new GameObject("Garden birds").transform;
            group.SetParent(transform, false);
            int n = 0;
            foreach (var def in Flocks)
            {
                var flock = new Flock { def = def };
                flock.at = new GameObject(def.kind + " flock sound point").transform;
                flock.at.SetParent(group, false);
                flock.wings = new NearFarSound(flock.at, "Wing flutter sound", flutter, flutterFar, true)
                    { NearFull = 3f, NearEnd = 24f, FarStart = 6f, FarEnd = 40f, FarGain = .5f };
                for (int i = 0; i < def.count; i++, n++)
                {
                    var bird = Make(model, def, group, materials, def.kind + " " + (flocks.Count + 1) + "-" + (i + 1));
                    if (bird == null) continue;
                    bird.place = Places[i % Places.Length] * def.spacing;
                    bird.seed = (n * .618034f) % 1f;
                    bird.lag = bird.place.z / Mathf.Max(1f, Speed(def));
                    flock.birds.Add(bird);
                }
                flocks.Add(flock);
            }
        }

        // One bird: the FBX under a pivot turned 180 degrees about y (Blender x, y, z = garden x, z, y, as the garden's
        // dressing hangs), the other kind's parts dropped, each wing hung from a pivot at its shoulder.
        static Bird Make(GameObject model, FlockDef def, Transform parent, Dictionary<string, Material> materials, string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * def.scale;
            var turn = new GameObject("Turn").transform;
            turn.SetParent(root, false);
            turn.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var fbx = Instantiate(model, turn, false);
            Transform body = null, wingL = null, wingR = null;
            foreach (var t in fbx.GetComponentsInChildren<Transform>(true))
            {
                if (t == fbx.transform) continue;
                if (!t.name.StartsWith(def.kind + " "))
                {
                    if (t.GetComponent<Renderer>() != null) t.gameObject.SetActive(false);
                    continue;
                }
                if (t.name.EndsWith("body")) body = t;
                else if (t.name.EndsWith("wing L")) wingL = t;
                else if (t.name.EndsWith("wing R")) wingR = t;
            }
            if (body == null || wingL == null || wingR == null)
            {
                Debug.LogWarning("[정원] 새 모델에 " + def.kind + " body / wing L / wing R가 없습니다.");
                Destroy(root.gameObject);
                return null;
            }
            foreach (var r in fbx.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null) slots[i] = RoyalWaterGardenBuilder.ConceptMaterial(slots[i], materials);
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return new Bird { root = root, flock = def, wingL = Hinge(root, wingL), wingR = Hinge(root, wingR) };
        }

        // A pivot at the wing's shoulder in the bird's own frame (+z forward, +y up, +x right): turning it about +z
        // lifts or drops the wing.
        static Transform Hinge(Transform root, Transform wing)
        {
            var pivot = new GameObject(wing.name + " hinge").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = root.InverseTransformPoint(wing.position);
            wing.SetParent(pivot, true);
            return pivot;
        }

        // m/s along the path (its length over a round, roughly), for how far behind its leader a bird flies.
        static float Speed(FlockDef d)
        {
            float a = d.size.x, b = d.size.y;
            float ring = Mathf.PI * (3f * (a + b) - Mathf.Sqrt((3f * a + b) * (a + 3f * b)));   // Ramanujan
            float k = d.path == Path.Eight ? 1.25f : d.path == Path.Lissajous ? 1.9f : 1f;
            return ring * k / d.period;
        }

        // The flock's path at time t, in the garden's frame.
        // R770: the pace swells and eases along the way (the phase never runs back: pace < 1.6) and the whole path
        // wanders - its middle drifts, it breathes a little wider and narrower - on slow periods that never line up
        // with the round, so no two rounds trace the same line.
        static Vector3 PathAt(FlockDef d, double t)
        {
            float round = (float)(t / d.period % 1.0) * Mathf.PI * 2f;
            float slow = (float)(t % 100000.0);
            float u = round + d.pace * Mathf.Sin(slow * (2f * Mathf.PI / (d.period * 1.618f)) + .9f);
            float x, z;
            switch (d.path)
            {
                case Path.Eight:     x = Mathf.Sin(u); z = Mathf.Sin(2f * u) * .5f * 1.6f; break;   // a lemniscate of Gerono
                case Path.Lissajous: x = Mathf.Sin(2f * u + .4f); z = Mathf.Sin(3f * u); break;
                default:             x = Mathf.Cos(u); z = Mathf.Sin(u); break;
            }
            float breath = 1f + (d.wander > 0f ? .1f * Mathf.Sin(slow * .113f + 1.7f) : 0f);
            float y = Mathf.Sin(u * 2f + .7f) * d.rise * .5f + Mathf.Sin(u * 3f + 2.1f) * d.rise * .25f
                + .25f * d.wander * Mathf.Sin(slow * .131f + 2f);
            var drift = d.wander * new Vector3(Mathf.Sin(slow * .071f + 1.3f), 0f, Mathf.Sin(slow * .053f + .4f));
            return d.middle + drift + new Vector3(x * d.size.x * breath, y, z * d.size.y * breath);
        }

        // A bird at time t (garden frame): its flock's path some way behind the leader - R770: a gap that opens and
        // closes - its own place across and above the way, sliding a little, and a wobble of its own.
        static Vector3 BirdAt(Bird b, double t)
        {
            float w = (float)(t % 100000.0);
            double at = t - b.lag * (1f + .3f * Mathf.Sin(w * .19f + b.seed * 7f));
            Vector3 p = PathAt(b.flock, at), ahead = PathAt(b.flock, at + .2) - PathAt(b.flock, at - .2);
            ahead.y = 0f;
            var forward = ahead.sqrMagnitude > 1e-6f ? ahead.normalized : Vector3.forward;
            var right = new Vector3(forward.z, 0f, -forward.x);
            float s = b.flock.spacing;
            float across = b.place.x + .6f * s * Mathf.Sin(w * .27f + b.seed * 5f);
            float up = b.place.y + .35f * s * Mathf.Sin(w * .23f + b.seed * 3f);
            var wobble = new Vector3(Mathf.Sin(w * .83f + b.seed * 6.3f), Mathf.Sin(w * 1.07f + b.seed * 4.1f) * .6f, Mathf.Sin(w * .61f + b.seed * 9.7f));
            return p + right * across + Vector3.up * up + wobble * (.3f * s);
        }

        // R770: one wing beat at phase p (0..1): a quick downstroke (0..0.42) from up (1) to down (-1), then a slower
        // upstroke back, as a bird's power stroke is faster than its recovery.
        static float Stroke(float p)
        {
            const float down = .42f;
            return p < down ? Mathf.Cos(Mathf.PI * p / down) : -Mathf.Cos(Mathf.PI * (p - down) / (1f - down));
        }

        void Update()
        {
            double now = ObstacleClock.Now;
            // R770: heading, bank and climb read over half a second (was 0.1 s), so they turn smoothly
            const float dt = .25f;
            foreach (var flock in flocks)
            {
                if (flock.birds.Count == 0) continue;
                var middle = Vector3.zero;
                float beat = 0f;
                foreach (var b in flock.birds)
                {
                    var d = b.flock;
                    Vector3 p0 = BirdAt(b, now - dt), p1 = BirdAt(b, now), p2 = BirdAt(b, now + dt);
                    Vector3 v = (p2 - p0) / (2f * dt);
                    var flat0 = new Vector3(p1.x - p0.x, 0f, p1.z - p0.z);
                    var flat1 = new Vector3(p2.x - p1.x, 0f, p2.z - p1.z);
                    float yawRate = Vector3.SignedAngle(flat0, flat1, Vector3.up) * Mathf.Deg2Rad / dt;   // + = turning right
                    float speed = new Vector2(v.x, v.z).magnitude;
                    // R770: the bank a real turn takes (tan = v^2 k / g), a little more for the look (was 2.2 times)
                    float bank = Mathf.Clamp(Mathf.Atan(speed * yawRate / 9.81f) * Mathf.Rad2Deg * 1.4f * d.bankScale, -50f, 50f);
                    float pitch = Mathf.Clamp(-Mathf.Atan2(v.y, Mathf.Max(.1f, speed)) * Mathf.Rad2Deg, -25f, 25f);

                    // Wings (R770): bouts of flapping and glides between on a rhythm of its own that never repeats (three
                    // slow waves), eased in and out; climbing makes it flap, coming down lets it glide. Each beat is a
                    // quick downstroke and a slower upstroke, each bird at its own beat rate.
                    float t = (float)(now % 100000.0);
                    float n = .5f + .32f * Mathf.Sin(t * .83f + b.seed * 9f) + .22f * Mathf.Sin(t * 1.91f + b.seed * 4f)
                        + .12f * Mathf.Sin(t * 3.7f + b.seed * 2f);
                    float flapping = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.4f, .62f, n));
                    flapping = Mathf.Clamp01(Mathf.Max(flapping, v.y * .5f));
                    if (v.y < -.4f) flapping *= Mathf.Clamp01(1f + (v.y + .4f) * .6f);
                    float hz = d.flapHz * (.94f + .12f * b.seed);
                    float beatPhase = (float)((now * hz + b.seed * 3.0) % 1.0);
                    float wave = Stroke(beatPhase);
                    float glide = d.glideAngle + 3f * Mathf.Sin(t * 1.4f + b.seed * 5f);
                    float wing = Mathf.Lerp(glide, 8f + d.flapAngle * wave, flapping);
                    b.wingR.localRotation = Quaternion.AngleAxis(wing, Vector3.forward);
                    b.wingL.localRotation = Quaternion.AngleAxis(-wing, Vector3.forward);

                    var heading = v.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(new Vector3(v.x, 0f, v.z).sqrMagnitude > 1e-4f ? new Vector3(v.x, 0f, v.z) : Vector3.forward, Vector3.up) : b.root.localRotation;
                    // each downstroke lifts the body and tips the nose up a touch
                    b.root.localRotation = heading * Quaternion.Euler(pitch + 3f * wave * flapping, 0f, -bank);
                    b.root.localPosition = p1 + Vector3.up * (-.035f * d.scale * wave * flapping);
                    middle += p1;
                    beat += flapping;
                }
                flock.at.localPosition = middle / flock.birds.Count;
                flock.wings.Mix(WingGain * (.35f + .65f * beat / flock.birds.Count), flock.def.kind == "Swallow" ? 1.25f : 1f);
            }
        }
    }
}
