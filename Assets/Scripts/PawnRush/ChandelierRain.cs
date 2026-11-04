using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using ChessFight.VFX;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The central landing's falling chandeliers (grand stair hall level design v0.3 §5 "샹들리에 낙하",
    // after 10 falling chess piece): every 0.7 ~ 1.4 s a chandelier falls somewhere random on the landing.
    // Its shadow on the floor darkens for 1.2 s (R340: the warning first, the chandelier only after it shows - it
    // comes down to hang shaking over the shadow), then it falls in 0.4 s, bounces and breaks on the floor. Where and
    // when come from the section's seed (ChandelierSchedule: never on the king's base, never within 6 m of a
    // drop still warning or just fallen, so a way between the shadows is always open).
    //
    // Drawn from the event and ObstacleClock only - the same on every PC, nothing sent. Hits are the
    // authority's (ObstacleClock.Authority): inside 2.5 m a pawn is knocked flat (12 + up 4), out to 4.5 m it
    // staggers (7 + up 2). The king statue shields what is behind it, like the falling piece's cover. The
    // broken pieces lie 1.5 s and touch nothing.
    public sealed class ChandelierRain : MonoBehaviour, ISectionPart
    {
        [Header("Where (section frame)")]
        [SerializeField] Vector2 areaMin = new Vector2(-13f, 54f);
        [SerializeField] Vector2 areaMax = new Vector2(13f, 65f);
        [Tooltip("Left out: the king statue's base.")]
        [SerializeField] Vector2 keepOutMin = new Vector2(-2f, 61f);
        [SerializeField] Vector2 keepOutMax = new Vector2(2f, 65f);
        [SerializeField] float floorY = 8f;

        [Header("When")]
        [SerializeField] float minGap = .7f;
        [SerializeField] float maxGap = 1.4f;
        [SerializeField] float minDistance = 6f;
        [SerializeField] float warning = 1.2f;
        [SerializeField] float fall = .4f;
        [SerializeField] float debris = 1.5f;
        [Tooltip("How high over the floor it comes down from when it shows.")]
        [SerializeField] float dropFrom = 16f;
        [Tooltip("How high over the floor it hangs, shaking, between showing and falling.")]
        [SerializeField] float hangOver = 8f;
        [Tooltip("How far into the warning (0..1) it shows: the shadow alone before that.")]
        [SerializeField, Range(0f, 1f)] float showAt = .35f;
        [Tooltip("Mixed into the section's seed: this rain's own stream.")]
        [SerializeField] int salt = 2;

        [Header("Hits")]
        [SerializeField] float innerRadius = 2.5f;
        [SerializeField] float outerRadius = 4.5f;
        [SerializeField] float innerPush = 12f, innerLift = 4f, innerKnockdown = 1f;
        [SerializeField] float outerPush = 7f, outerLift = 2f;
        [SerializeField] float heightTolerance = 2f;

        [Header("Look")]
        [SerializeField] Material chandelierMaterial;

        // Someone was hit (who, "샹들리에" or "샹들리에 고리"): the playtest counts it.
        public static event Action<GameObject, string> Hit;

        sealed class View
        {
            public GameObject root, chandelier, shadow, ring, dust;
            public Transform[] pieces;
            public Material shadowMaterial, dustMaterial;
            public VfxGroundWarn warn;   // R340: the floor warning decal, null without its shader
            public long serial;
            // R308: the Blender chandelier (its pivot at the top, where the chain held it) and its parts' rest pose.
            public bool model, scattered;
            public float top;
            public Vector3[] restPos;
            public Quaternion[] restRot;
            // R340: the frame's big parts (they bounce once together before they break) and how high each part's
            // centre lies over the floor once it is down.
            public bool[] body;
            public float[] lift;
        }

        // R340: the bounce on the floor, snappier than real gravity so it reads from the play camera.
        const float Gravity = 30f, Restitution = .42f;
        // The frame's one hop before it breaks: a squash, then up 0.9 m, tipping over and back.
        const float Squash = .08f, BodyHop = .9f, BodyTilt = 18f;
        static float BodyAir => Squash + 2f * Mathf.Sqrt(2f * Gravity * BodyHop) / Gravity;

        ChandelierSchedule schedule;
        double lastClock = double.NaN;
        readonly Dictionary<long, View> live = new Dictionary<long, View>();
        readonly List<View> free = new List<View>();
        readonly HashSet<long> seen = new HashSet<long>();
        readonly List<long> gone = new List<long>();
        readonly Collider[] overlaps = new Collider[64];
        readonly RaycastHit[] rays = new RaycastHit[32];

        public ChandelierSchedule Schedule => schedule;
        float Busy => warning + fall + .6f;

        public void Configure(Material chandelier) => chandelierMaterial = chandelier;

        // The landing it falls on and how often (the builder: a wider landing falls as often per square metre).
        public void Configure(Material chandelier, Vector2 min, Vector2 max, float gapMin, float gapMax)
        {
            chandelierMaterial = chandelier;
            areaMin = min;
            areaMax = max;
            minGap = gapMin;
            maxGap = gapMax;
        }

        public void OnSection(PawnRushSection section)
        {
            var rules = new ChandelierSchedule.Rules
            {
                MinGap = minGap, MaxGap = maxGap,
                XMin = areaMin.x, XMax = areaMax.x, ZMin = areaMin.y, ZMax = areaMax.y,
                KeepOutX0 = keepOutMin.x, KeepOutX1 = keepOutMax.x, KeepOutZ0 = keepOutMin.y, KeepOutZ1 = keepOutMax.y,
                MinDistance = minDistance, Busy = Busy,
            };
            schedule = new ChandelierSchedule(SeededSchedule.Hash(section.Seed, salt), section.StartClock, rules);
            lastClock = double.NaN;
            foreach (var view in live.Values) Release(view);
            live.Clear();
        }

        void FixedUpdate()
        {
            if (schedule == null) return;
            double now = ObstacleClock.Now;
            float life = warning + fall + debris;
            schedule.Advance(now + .1);
            schedule.Prune(now - life - 4.0);

            seen.Clear();
            for (int i = 0; i < schedule.Count; i++)
            {
                var d = schedule[i];
                double age = now - d.Time;
                if (age < 0) break;
                if (age > life) continue;
                seen.Add(d.Serial);
                if (!live.TryGetValue(d.Serial, out var view))
                {
                    view = Take();
                    view.serial = d.Serial;
                    live[d.Serial] = view;
                }
                Draw(view, d, (float)age);
                // The hit, once, the step the impact time is crossed (never for impacts before this PC started).
                double impact = d.Time + warning + fall;
                if (ObstacleClock.Authority && !double.IsNaN(lastClock) && impact > lastClock && impact <= now) Strike(d);
            }
            lastClock = now;

            gone.Clear();
            foreach (var kv in live) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (long serial in gone)
            {
                Release(live[serial]);
                live.Remove(serial);
            }
        }

        Vector3 Center(ChandelierSchedule.Drop d) => new Vector3((float)d.X, floorY, (float)d.Z);

        void Draw(View view, ChandelierSchedule.Drop d, float age)
        {
            var center = Center(d);
            view.root.transform.localPosition = center;
            bool falling = age >= warning && age < warning + fall;
            bool broken = age >= warning + fall;
            // Shadow and the outer ring: darkening through the warning, gone once it has fallen.
            float dark = broken ? 0f : Mathf.Clamp01(age / warning);
            // R340: the floor decal when there is one (filling to its rim as it lands), else the dark discs.
            if (view.warn != null) view.warn.Seek(age);
            view.shadow.SetActive(!broken && view.warn == null);
            view.ring.SetActive(!broken && view.warn == null);
            view.shadowMaterial.color = new Color(.05f, .03f, .08f, .15f + .6f * dark);
            // R340: a puff of dust out from the impact.
            float since = age - warning - fall;
            bool puff = since >= 0f && since < .45f;
            view.dust.SetActive(puff);
            if (puff)
            {
                float k = since / .45f, size = Mathf.Lerp(1.5f, outerRadius * 2.4f, 1f - (1f - k) * (1f - k));
                view.dust.transform.localScale = new Vector3(size, .004f, size);
                view.dustMaterial.color = new Color(.96f, .92f, .82f, .55f * (1f - k) * (1f - k));
            }
            // R340: nothing overhead when the warning starts - the shadow alone. Then (showAt into the warning) it slides
            // down from high up to hang over the shadow, shaking harder and harder, and at the warning's end it falls,
            // speeding up, turning a little.
            float show = warning * showAt;
            bool hanging = age >= show && age < warning;
            view.chandelier.SetActive(hanging || falling || (view.model && broken));
            float u = broken ? 1f : falling ? Mathf.Clamp01((age - warning) / fall) : 0f;
            var c = view.chandelier.transform;
            if (hanging)
            {
                float k = Mathf.Clamp01((age - show) / .25f), e = 1f - (1f - k) * (1f - k);
                float shake = Mathf.Sin(age * 23f) * (2f + 9f * Mathf.Clamp01((age - show) / (warning - show)));
                c.localPosition = Vector3.up * (Mathf.Lerp(dropFrom, hangOver, e) + .02f + view.top);
                c.localRotation = Quaternion.Euler(shake, age * 20f, shake * .6f);
            }
            else
            {
                c.localPosition = Vector3.up * (hangOver * (1f - u * u) + .02f + view.top);
                c.localRotation = Quaternion.Euler(0f, (warning + fall * u) * 20f + 40f * u, 0f);
            }
            if (view.model)
            {
                DrawModel(view, d, since, broken);
                return;
            }
            // The primitive chandelier's pieces: out from the impact, bouncing, then still.
            for (int i = 0; i < view.pieces.Length; i++)
            {
                var piece = view.pieces[i];
                piece.gameObject.SetActive(broken);
                if (!broken) continue;
                float h1 = Hash01(d.Serial, i, 1), h2 = Hash01(d.Serial, i, 2), h3 = Hash01(d.Serial, i, 3);
                float a = (i * 137.5f + d.Serial * 31f) * Mathf.Deg2Rad, slide = Slide(since);
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                piece.localPosition = outward * ((.6f + 2.2f * h1) * slide) + Vector3.up * (.06f + Hop(since, .5f, 3f + 4f * h2));
                piece.localRotation = Quaternion.Euler(i * 40f + 540f * h3 * slide, i * 70f, 360f * h1 * slide);
            }
        }

        // R308: the Blender chandelier's own parts (crystals, arms, candles, rings) break off where it lands. R340 (승규
        // 11-04 "떨어지고 바닥에 튀기는 애니메이션이 없어 너무 정적이야"): it squashes on the floor, the small parts spray out
        // bouncing, the frame hops once, tipping over and back, and breaks when it lands again; every part slides to a
        // stop. From the drop's serial only, so every PC sees the same pieces in the same places.
        void DrawModel(View view, ChandelierSchedule.Drop d, float since, bool broken)
        {
            var c = view.chandelier.transform;
            if (!broken)
            {
                if (view.scattered)
                {
                    for (int i = 0; i < view.pieces.Length; i++)
                        view.pieces[i].SetPositionAndRotation(c.TransformPoint(view.restPos[i]), c.rotation * view.restRot[i]);
                    view.scattered = false;
                }
                return;
            }
            view.scattered = true;
            float floor = -view.top - .02f, t = since;
            // The frame's squash, then its hop (its centre's height over the rest pose) and tilt about a level axis.
            float squash = t < Squash ? 1f - .22f * Mathf.Sin(Mathf.PI * t / Squash) : 1f;
            float air = BodyAir, hop = t > Squash && t < air ? Hop(t - Squash, 0f, Mathf.Sqrt(2f * Gravity * BodyHop)) : 0f;
            float a = Hash01(d.Serial, 0, 4) * 6.283f;
            var tilt = Quaternion.AngleAxis(t < air ? BodyTilt * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - Squash) / (air - Squash))) : 0f,
                                            new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            var middle = new Vector3(0f, floor + view.top * .5f, 0f);
            for (int i = 0; i < view.pieces.Length; i++)
            {
                float h1 = Hash01(d.Serial, i, 1), h2 = Hash01(d.Serial, i, 2), h3 = Hash01(d.Serial, i, 3);
                var rest = view.restPos[i];
                // The small parts fly from the impact; the frame's parts from where the frame lands again (its rest pose).
                float flight = t;
                if (view.body[i])
                {
                    if (t < air)
                    {
                        var squashed = new Vector3(rest.x * (2f - squash), floor + (rest.y - floor) * squash, rest.z * (2f - squash));
                        var held = middle + tilt * (squashed - middle) + Vector3.up * hop;
                        view.pieces[i].SetPositionAndRotation(c.TransformPoint(held), c.rotation * tilt * view.restRot[i]);
                        continue;
                    }
                    flight = t - air;
                }
                var from = rest;
                var flat = new Vector3(rest.x, 0f, rest.z);
                var dir = flat.sqrMagnitude > .0025f ? flat.normalized
                        : new Vector3(Mathf.Sin(h1 * 6.283f), 0f, Mathf.Cos(h1 * 6.283f));
                float reach = view.body[i] ? .3f + 1.2f * h1 : .8f + 2.6f * h1;
                float up = view.body[i] ? 1.5f + 2.5f * h2 : 3f + 5f * h2;
                float slide = Slide(flight), lift = view.lift[i];
                var at = new Vector3(from.x, 0f, from.z) + dir * (reach * slide)
                       + Vector3.up * (floor + lift + Hop(flight, Mathf.Max(0f, from.y - floor - lift), up));
                var tumble = Quaternion.Euler((h1 * 2f - 1f) * 540f * slide, (h2 * 2f - 1f) * 360f * slide, (h3 * 2f - 1f) * 300f * slide);
                view.pieces[i].SetPositionAndRotation(c.TransformPoint(at), c.rotation * tumble * view.restRot[i]);
            }
        }

        // R340: the height at t of something leaving h0 over the floor at v0 upwards, bouncing (Restitution) until a
        // hop would be a few centimetres.
        static float Hop(float t, float h0, float v0)
        {
            float h = h0, v = v0;
            for (int n = 0; n < 5; n++)
            {
                float land = Mathf.Sqrt(v * v + 2f * Gravity * h), air = (v + land) / Gravity;
                if (t < air) return Mathf.Max(0f, h + v * t - .5f * Gravity * t * t);
                t -= air;
                h = 0f;
                v = Restitution * land;
                if (v < .7f) break;
            }
            return 0f;
        }

        // How far along its slide a flying part is, 0 -> 1, nearly stopped in 0.8 s.
        static float Slide(float t) => t <= 0f ? 0f : 1f - Mathf.Exp(-5f * t);

        static float Hash01(long serial, int i, int salt)
        {
            uint h = (uint)(serial * 73856093L) ^ (uint)(i * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        void Strike(ChandelierSchedule.Drop d)
        {
            var center = transform.TransformPoint(Center(d));
            int n = Physics.OverlapSphereNonAlloc(center, outerRadius + 1f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            var struck = new HashSet<ImportedObstacleActor>();
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !struck.Add(actor)) continue;
                var delta = actor.Position - center;
                if (Mathf.Abs(delta.y) > heightTolerance) continue;
                var flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                float distance = flat.magnitude;
                if (distance > outerRadius || Shielded(center, actor)) continue;
                var outward = distance > .05f ? flat / distance : transform.forward;
                bool inner = distance <= innerRadius;
                actor.AddObstacleHit(outward * (inner ? innerPush : outerPush) + Vector3.up * (inner ? innerLift : outerLift),
                                     inner ? innerKnockdown : 0f);
                Hit?.Invoke(actor.gameObject, inner ? "샹들리에" : "샹들리에 고리");
            }
        }

        // Something solid between the impact and the pawn (the king statue) that is neither a pawn nor a moving obstacle.
        bool Shielded(Vector3 center, ImportedObstacleActor actor)
        {
            var from = center + Vector3.up;
            var to = actor.Position + Vector3.up * .5f;
            var dir = to - from;
            float length = dir.magnitude;
            if (length < .05f) return false;
            int n = Physics.RaycastNonAlloc(from, dir / length, rays, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = rays[i].collider;
                if (c.GetComponentInParent<ICharacterDriver>() != null || c.attachedRigidbody != null) continue;
                if (c.GetComponent<CourseFloor>() != null) continue;
                return true;
            }
            return false;
        }

        View Take()
        {
            View view;
            if (free.Count > 0)
            {
                view = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
            }
            else view = Make();
            view.root.SetActive(true);
            return view;
        }

        void Release(View view)
        {
            if (view == null || view.root == null) return;
            view.root.SetActive(false);
            free.Add(view);
        }

        // ------------------------------------------------------------------ the chandelier, its shadow and its pieces

        View Make()
        {
            var root = new GameObject("Falling chandelier");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var made = MakeModel(root);
            if (made != null) return made;
            var gold =chandelierMaterial != null ? chandelierMaterial : Arena.TrimGold;
            var candle = Arena.Lit("M_Candle", new Color(.98f, .95f, .86f), .2f, .5f);
            var flame = Arena.Glow(new Color(1f, .78f, .3f, .95f));

            var chandelier = new GameObject("Chandelier");
            chandelier.transform.SetParent(root.transform, false);
            var c = chandelier.transform;
            Shape(c, PrimitiveType.Cylinder, Vector3.zero, new Vector3(2.4f, .07f, 2.4f), gold);          // ring
            Shape(c, PrimitiveType.Cylinder, Vector3.up * .45f, new Vector3(.22f, .5f, .22f), gold);       // column
            Shape(c, PrimitiveType.Sphere, Vector3.up * -.25f, new Vector3(.45f, .45f, .45f), gold);       // finial
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var at = new Vector3(Mathf.Sin(a) * 1.1f, 0f, Mathf.Cos(a) * 1.1f);
                Shape(c, PrimitiveType.Cylinder, at + Vector3.up * .2f, new Vector3(.1f, .16f, .1f), candle);
                Shape(c, PrimitiveType.Sphere, at + Vector3.up * .42f, new Vector3(.1f, .14f, .1f), flame);
            }

            var view = Marks(root);
            var pieces = new Transform[7];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = Shape(root.transform, i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Cylinder, Vector3.zero,
                                  new Vector3(.35f + .1f * (i % 3), .08f, .18f), i % 3 == 0 ? candle : gold);
                piece.SetActive(false);
                pieces[i] = piece.transform;
            }
            view.chandelier = chandelier;
            view.pieces = pieces;
            view.top = .6f;   // the finial hangs below the ring
            return view;
        }

        // The shadow (darkening), the outer ring's edge (a stagger out to 4.5 m) and the impact's dust (R340), each with
        // its own colour.
        View Marks(GameObject root)
        {
            var shadowMaterial = new Material(Arena.Glow(new Color(.05f, .03f, .08f, .2f)));
            var shadow = Shape(root.transform, PrimitiveType.Cylinder, Vector3.up * .03f,
                               new Vector3(innerRadius * 2f, .01f, innerRadius * 2f), shadowMaterial);
            var ring = Shape(root.transform, PrimitiveType.Cylinder, Vector3.up * .02f,
                             new Vector3(outerRadius * 2f, .005f, outerRadius * 2f), Arena.Glow(new Color(.05f, .03f, .08f, .16f)));
            var dustMaterial = new Material(Arena.Glow(new Color(.96f, .92f, .82f, .5f)));
            var dust = Shape(root.transform, PrimitiveType.Cylinder, Vector3.up * .04f, Vector3.one, dustMaterial);
            dust.name = "Dust";
            dust.SetActive(false);
            return new View { root = root, shadow = shadow, ring = ring, dust = dust, shadowMaterial = shadowMaterial, dustMaterial = dustMaterial, warn = MakeWarn(root) };
        }

        // R340 (승규 11-04 "샹들리에 예고 vfx 만들어"): the floor warning, the falling knights' projected decal (VfxGroundWarn,
        // R341) in the chandelier's own material - its rim at the stagger's reach, a brighter inner ring at the knock-down's,
        // filling from the middle to the rim over the warning and the fall, full as it lands. From the hazards' set
        // (menu ChessFight/VFX/Hazard/Build W1 chandelier) or, before that menu has been run, from the shader with the same
        // values. Drawn on the drop's age like the rest, so every PC sees the same; the dark discs stay as a fallback.
        const string WarnSet = "Hazards", WarnKey = "Chandelier_Warn", WarnShader = "ChessFight/VFX/Ground Warn Decal";
        static Material warnMaterial;

        VfxGroundWarn MakeWarn(GameObject root)
        {
            VfxGroundWarn warn = null;
            var set = VfxSet.Load(WarnSet);
            var prefab = set != null ? set.Find(WarnKey) : null;
            if (prefab != null)
            {
                var fx = Instantiate(prefab, root.transform, false);
                fx.name = prefab.name;
                if (fx.TryGetComponent<VfxCue>(out var cue)) cue.enabled = false;
                warn = fx.GetComponentInChildren<VfxGroundWarn>(true);
            }
            else
            {
                if (warnMaterial == null)
                {
                    var shader = Shader.Find(WarnShader);
                    if (shader == null)
                    {
                        Debug.LogWarning("[대계단 홀] 샹들리에 예고 셰이더가 없습니다(" + WarnShader + "): 어두운 원판으로 예고합니다");
                        return null;
                    }
                    // ChandelierVfxBuilder's look: the inner ring at 2.5 of 4.5 m, 24 ticks, a 1.3 m landing mark.
                    warnMaterial = new Material(shader) { name = "Chandelier warn decal (no prefab)" };
                    warnMaterial.SetFloat("_InnerRing", 2.5f / 4.5f);
                    warnMaterial.SetFloat("_InnerLight", 1.8f);
                    warnMaterial.SetFloat("_Ticks", 24f);
                    warnMaterial.SetFloat("_Mark", 1.3f);
                }
                var decal = new GameObject("W1 Decal (no prefab)");
                decal.transform.SetParent(root.transform, false);
                decal.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                var view = decal.AddComponent<MeshRenderer>();
                view.sharedMaterial = warnMaterial;
                view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                view.receiveShadows = false;
                view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                warn = decal.AddComponent<VfxGroundWarn>();
            }
            if (warn == null) return null;
            // The drop's age drives it (Draw), not its own clock.
            warn.enabled = false;
            warn.radius = outerRadius;
            warn.lead = warning + fall;
            return warn;
        }

        // R360 (승규 11-04 "계단 쪽 샹들리에도 텍스쳐 입혀줘 대형 샹들리에랑 똑같으면 안돼"): the falling chandelier
        // dressed as the hub chandelier's darker sibling (falling_chandelier_v48.py sorted its faces): an antique silver
        // frame, deep ruby drops with a faint glow, ivory candles, a dim flame - no point lights (they fall often).
        static Material fcSilver, fcRuby, fcWax, fcFlame;

        static Material FallingMaterial(string name)
        {
            if (!name.StartsWith("FC_")) return null;
            if (fcSilver == null)
            {
                fcSilver = LitMaterials.NewLit(new Color(.68f, .68f, .72f), .72f, .9f);
                fcSilver.name = "Falling chandelier silver";
                fcRuby = LitMaterials.NewLit(new Color(.42f, .02f, .06f), .92f, .1f);
                fcRuby.name = "Falling chandelier ruby";
                LitMaterials.SetEmission(fcRuby, new Color(.5f, .03f, .07f) * .5f);
                fcWax = LitMaterials.NewLit(new Color(.95f, .92f, .85f), .35f);
                fcWax.name = "Falling chandelier wax";
                fcFlame = LitMaterials.NewLit(new Color(1f, .78f, .5f), .5f);
                fcFlame.name = "Falling chandelier flame";
                LitMaterials.SetEmission(fcFlame, new Color(1f, .6f, .28f) * 3f);
            }
            return name.StartsWith("FC_Ruby") ? fcRuby : name.StartsWith("FC_Wax") ? fcWax
                 : name.StartsWith("FC_Flame") ? fcFlame : fcSilver;
        }

        // R308: the Blender hall's chandelier (export_unity_v15.py: 129 parts, origin at its foot, and its chain), clay
        // like the hall until R360. Null when the model is missing: the primitive chandelier above draws instead.
        View MakeModel(GameObject root)
        {
            var model = Resources.Load<GameObject>("PawnRushGrandStairHall/FallingChandelier");
            if (model == null) return null;
            var clay = Course01v4Builder.HallClay;
            var chandelier = new GameObject("Chandelier");
            chandelier.transform.SetParent(root.transform, false);
            var turn = new GameObject("Model pivot").transform;
            turn.SetParent(chandelier.transform, false);
            turn.localRotation = Quaternion.Euler(0, 180, 0);
            var view = Instantiate(model, turn, false);
            view.name = "Falling chandelier model";
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = FallingMaterial(slots[i] != null ? slots[i].name : "") ?? clay;
                r.sharedMaterials = slots;
            }
            // Its height from its parts; the pivot goes to the top, where the chain held it. R340: no chain - nothing
            // hangs there before it falls.
            var parts = new List<Transform>();
            var sizes = new List<Vector3>();
            float top = 0f;
            foreach (Transform t in view.transform)
            {
                if (t.name == "Chain") { t.gameObject.SetActive(false); continue; }
                if (!t.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                parts.Add(t);
                // Mesh corners, not Renderer.bounds: the root is still inactive here.
                var b = filter.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    top = Mathf.Max(top, t.TransformPoint(corner).y - chandelier.transform.position.y);
                }
                sizes.Add(Vector3.Scale(b.size, t.lossyScale));
            }
            if (parts.Count == 0) { Destroy(view); return null; }
            turn.localPosition = Vector3.down * top;
            var c = chandelier.transform;
            var restPos = new Vector3[parts.Count];
            var restRot = new Quaternion[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                restPos[i] = c.InverseTransformPoint(parts[i].position);
                restRot[i] = Quaternion.Inverse(c.rotation) * parts[i].rotation;
            }
            // R340: the frame = the biggest quarter of the parts (rings, arms, the column); a part lies on the floor on
            // its thinnest side, a little sunk so a tilted one does not float.
            var longest = new float[parts.Count];
            var lift = new float[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                var s = sizes[i];
                longest[i] = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                lift[i] = .4f * Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            }
            var sorted = (float[])longest.Clone();
            System.Array.Sort(sorted);
            float frame = sorted[Mathf.Clamp(sorted.Length * 3 / 4, 0, sorted.Length - 1)];
            var body = new bool[parts.Count];
            for (int i = 0; i < parts.Count; i++) body[i] = longest[i] >= frame;

            var made = Marks(root);
            made.chandelier = chandelier;
            made.pieces = parts.ToArray();
            made.model = true;
            made.top = top;
            made.restPos = restPos;
            made.restRot = restRot;
            made.body = body;
            made.lift = lift;
            return made;
        }

        static GameObject Shape(Transform parent, PrimitiveType type, Vector3 at, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        void OnDisable()
        {
            foreach (var view in live.Values) Release(view);
            live.Clear();
            lastClock = double.NaN;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .8f, .2f, .8f);
            var mid = new Vector3((areaMin.x + areaMax.x) * .5f, floorY + .1f, (areaMin.y + areaMax.y) * .5f);
            Gizmos.DrawWireCube(mid, new Vector3(areaMax.x - areaMin.x, .2f, areaMax.y - areaMin.y));
        }
    }
}
