using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R685 (승규 11-16 "이제 vfx를 한다 ... 지금 필요한 장애물에 맞는 효과", "걍 이 채팅에서 vfx 넣어", then "아니 실사로
    // 바꿔줘야지" - the effects only, the clay stays): the Royal Carriage Yard's obstacles throw up dust, earth, chips, sparks
    // and glints that try to look real. Pictures only: nothing collides with a pawn, nothing is pushed. It reads nothing of the
    // obstacles but where their parts are drawn, frame to frame, so it needs no change in their scripts and every PC sees the
    // same thing (the parts run on ObstacleClock):
    //   carriages and horses   dust rolling up behind their feet and wheels, more the faster they go; clods of earth and a
    //                          burst of dust when a charge starts (speed through ChargeFrom in under ChargeWithin seconds)
    //   stacks and pallets     set down: a skirt of dust shoved out low along the floor, then rising and spreading; grit
    //                          and wood chips skipping off the floor
    //   hoist pallets          starting off: the ratchet's sparks from the rope corners, bouncing on the floor and cooling
    //                          from white through orange to red, and chips knocked off the boards
    //   slewing cranes' loads  a streak of dust off the floor under a load sweeping low over it
    //   the outer gate         dust along the floor under each leaf's free edge while it swings
    //   the final push gate    dust scraped up along its foot while it is pushed open; gold glints that twinkle when it is
    //                          fully open
    //   the giant chest        lands: a wall of dust thrown out from its edge to the shockwave's radius in the wave's time
    //                          (CargoSlam.ShockRadius, WaveSeconds), a haze, clods and grit (its ten round puffs are gone,
    //                          its warning circle is its shadow: YardWarn); the water's spray is YardWater's (R684)
    //
    // What makes it read as real rather than as dots:
    //   - dust is a 4 x 4 sheet of different puffs drawn from domain-warped fractal noise (soft, ragged edges, denser
    //     knots, a lit top and a shaded underside), one picked at random per puff, each turning slowly, growing
    //     (ease-out) and thinning as it goes, carried by curl-like turbulence (the noise module) and slowed by the air;
    //   - its colour is the yard's dry earth tones lit by the scene: the sun's colour times its height plus the ambient,
    //     read every second (the unlit particle shader is in every build; a lit one may be stripped);
    //   - chips, grit and clods are small irregular 3D pieces (one jittered-box mesh) in URP Lit, tumbling on three axes,
    //     bouncing and settling on the floor (the particle collision module, world, static colliders only) and shrinking
    //     away at the end instead of fading;
    //   - sparks are stretched by their speed, HDR, cool white -> orange -> dull red, and skip on the floor;
    //   - glints are four-pointed stars, additive, whose size flickers (noise on size).
    // A point emits only while it is near the floor (a ray down, triggers ignored; a ray from inside a part's own collider
    // misses it) and within FarAway of the camera. Quality tiers as the yard's light and water (YardAtmosphere): 0 a third
    // of the particles and no collisions, 1 two thirds, 2 all.
    public sealed class YardFx : MonoBehaviour
    {
        enum Kind { Carriage, Stack, Pallet, Load, Gate, Chest }

        sealed class Watch
        {
            public Transform t;
            public Kind kind;
            public Vector3[] feet;              // bottom points in t's space
            public Vector3[] last;              // their world points last frame
            public Vector3 lastCentre;
            public float lastVy, speed, slow = 99f, acc;
            public bool seen, descending, resting;
            public CargoSlam slam;              // the giant chest's gantry: its shockwave's reach and time
        }

        sealed class PushWatch
        {
            public PushGate gate;
            public float progress;
            public bool open;
        }

        // The yard's dry dust and earth (the paving's sand and the yard's packed earth), the chips' wood, the grit's stone.
        static readonly Color DustLight = new Color(.80f, .73f, .62f), DustDark = new Color(.60f, .52f, .42f);
        static readonly Color Wood = new Color(.36f, .25f, .15f), Clod = new Color(.33f, .26f, .19f), Grit = new Color(.55f, .52f, .48f);
        static readonly Color SparkHdr = new Color(4.2f, 2.3f, .8f), GlintHdr = new Color(4f, 3.1f, 1.4f);
        const float FarAway = 70f, FloorReach = .9f, MaxSpeed = 25f;
        const float ChargeFrom = 3f, ChargeWithin = .6f;
        const int Sheet = 4;                    // the dust sheet is Sheet x Sheet puffs
        static Texture2D smoke, dot, star;
        static Mesh chunk;

        Transform root;
        int maxTier = 2, tier = -1;
        float scale = 1f, lightAt = -1f;
        Color lit = Color.white;
        readonly List<Watch> watches = new List<Watch>();
        readonly List<PushWatch> pushes = new List<PushWatch>();
        readonly List<Material> made = new List<Material>();
        ParticleSystem dust, haze, wall, chips, clods, grit, sparks, glints;
        Light sun;

        public void Configure(Transform yardRoot, int maxQualityTier)
        {
            root = yardRoot;
            maxTier = Mathf.Clamp(maxQualityTier, 0, 2);
        }

        void Start()
        {
            if (root == null) root = transform.parent;
            Textures();

            dust = Puffs("Yard fx dust", 900, 2.8f, .35f);
            haze = Puffs("Yard fx haze", 300, 4f, .2f);
            // The giant chest's dust wall: thrown out at the shockwave's speed, so less held back by the air.
            wall = Puffs("Yard fx dust wall", 500, 2.4f, .3f);
            var loose = wall.limitVelocityOverLifetime;
            loose.limit = 9f;
            loose.dampen = .025f;
            chips = Pieces("Yard fx chips", Wood, 300, 1.15f, .35f);
            clods = Pieces("Yard fx clods", Clod, 300, 1.2f, .2f);
            grit = Pieces("Yard fx grit", Grit, 300, 1.1f, .45f);
            sparks = Sparks();
            glints = Glints();

            foreach (var o in root.GetComponentsInChildren<RoamingCarriage>(true)) Add(o.transform, Kind.Carriage);
            foreach (var o in root.GetComponentsInChildren<StackCrate>(true)) Add(o.transform, Kind.Stack);
            foreach (var o in root.GetComponentsInChildren<HoistPallet>(true)) Add(o.transform, Kind.Pallet);
            foreach (var o in root.GetComponentsInChildren<SlewJib>(true)) Add(o.transform, Kind.Load);
            foreach (var o in root.GetComponentsInChildren<TimedGate>(true)) Add(o.transform, Kind.Gate);
            var gantries = root.GetComponentsInChildren<CargoSlam>(true);
            foreach (var o in root.GetComponentsInChildren<SlamChest>(true))
            {
                var w = Add(o.transform, Kind.Chest);
                if (w == null) continue;
                w.slam = o.GetComponentInParent<CargoSlam>(true);
                if (w.slam == null && gantries.Length > 0) w.slam = gantries[0];
            }
            foreach (var g in root.GetComponentsInChildren<PushGate>(true)) pushes.Add(new PushWatch { gate = g, progress = g.Progress, open = g.IsOpen });
            UpdateTier();
            Debug.Log("[마차뜰 효과] 품질 단계 " + tier + ": 마차 · 말 " + Count(Kind.Carriage) + ", 상자 더미 " + Count(Kind.Stack)
                      + ", 승강대 발판 " + Count(Kind.Pallet) + ", 크레인 짐 " + Count(Kind.Load) + ", 성문 문짝 " + Count(Kind.Gate)
                      + ", 미는 성문 " + pushes.Count + ", 거대 궤짝 " + Count(Kind.Chest));
        }

        void OnDestroy()
        {
            foreach (var m in made)
                if (m != null) Destroy(m);
        }

        int Count(Kind k)
        {
            int n = 0;
            foreach (var w in watches) if (w.kind == k) n++;
            return n;
        }

        // A part's bottom: the four lower corners of its drawn box and the middle of its bottom, in its own space.
        Watch Add(Transform t, Kind kind)
        {
            bool any = false;
            var box = new Bounds();
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                var w = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = t.InverseTransformPoint(w.center + Vector3.Scale(w.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { box = new Bounds(c, Vector3.zero); any = true; }
                    else box.Encapsulate(c);
                }
            }
            if (!any) return null;
            float y = box.min.y;
            var feet = new[]
            {
                new Vector3(box.min.x, y, box.min.z), new Vector3(box.max.x, y, box.min.z),
                new Vector3(box.min.x, y, box.max.z), new Vector3(box.max.x, y, box.max.z),
                new Vector3(box.center.x, y, box.center.z),
            };
            var watch = new Watch { t = t, kind = kind, feet = feet, last = new Vector3[feet.Length] };
            watches.Add(watch);
            return watch;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Time.frameCount % 60 == 0) UpdateTier();
            if (Time.time >= lightAt) { lightAt = Time.time + 1f; ReadLight(); }
            var cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            foreach (var w in watches) Look(w, dt, cam != null, eye);
            foreach (var p in pushes) LookPush(p);
        }

        void Look(Watch w, float dt, bool haveEye, Vector3 eye)
        {
            if (w.t == null || !w.t.gameObject.activeInHierarchy) { w.seen = false; return; }
            var centre = w.t.TransformPoint(w.feet[4]);
            if (!w.seen)
            {
                for (int i = 0; i < w.feet.Length; i++) w.last[i] = w.t.TransformPoint(w.feet[i]);
                w.lastCentre = centre;
                w.lastVy = 0f;
                w.seen = true;
                return;
            }
            var v = (centre - w.lastCentre) / dt;
            w.lastCentre = centre;
            if (v.magnitude > MaxSpeed)
            {
                // A cut (a new start moved it at once): no dust for the jump.
                for (int i = 0; i < w.feet.Length; i++) w.last[i] = w.t.TransformPoint(w.feet[i]);
                w.lastVy = 0f;
                return;
            }
            bool near = !haveEye || (centre - eye).sqrMagnitude < FarAway * FarAway;
            float vy = v.y, flat = new Vector2(v.x, v.z).magnitude;

            switch (w.kind)
            {
                case Kind.Carriage:
                    // Dust off the feet behind; clods and a burst when a charge starts.
                    if (flat < 1f) w.slow = 0f; else w.slow += dt;
                    if (near && w.speed < ChargeFrom && flat >= ChargeFrom && w.slow < ChargeWithin) Charge(w, v);
                    if (near) Trail(w, dt, flat, 1.2f, 6f, v);
                    break;
                case Kind.Load:
                case Kind.Gate:
                    if (near) TrailPoints(w, dt, w.kind == Kind.Load ? 2.5f : .35f, w.kind == Kind.Load ? 8f : 22f);
                    break;
                case Kind.Chest:
                    if (vy < -1f) w.descending = true;
                    if (w.descending && Mathf.Abs(vy) < .05f)
                    {
                        w.descending = false;
                        if (near) Slammed(w);
                    }
                    break;
                case Kind.Stack:
                case Kind.Pallet:
                    // The parts ease in and out (smoothstep), so a landing is the end of a real descent coming to rest,
                    // and a start is the first motion after a rest.
                    if (vy < -.5f) w.descending = true;
                    bool still = Mathf.Abs(vy) < .03f;
                    if (w.descending && still)
                    {
                        w.descending = false;
                        if (near) Landed(w);
                    }
                    if (w.kind == Kind.Pallet)
                    {
                        if (still) w.resting = true;
                        else if (Mathf.Abs(vy) > .06f)
                        {
                            if (w.resting && near) Jolt(w);
                            w.resting = false;
                        }
                    }
                    break;
            }
            w.speed = flat;
            w.lastVy = vy;
            for (int i = 0; i < w.feet.Length; i++) w.last[i] = w.t.TransformPoint(w.feet[i]);
        }

        // ------------------------------------------------------------------ the effects

        bool Floor(Vector3 at, out Vector3 floor)
        {
            if (Physics.Raycast(at + Vector3.up * .3f, Vector3.down, out var hit, .3f + FloorReach, ~0, QueryTriggerInteraction.Ignore))
            {
                floor = hit.point + Vector3.up * .05f;
                return true;
            }
            floor = at;
            return false;
        }

        int N(float count) => Mathf.Max(0, Mathf.RoundToInt(count * scale + Random.value - .5f));

        // One puff of dust: an earth tone between the light and the dark, lit by the scene, `alpha` thick.
        void Puff(ParticleSystem ps, Vector3 at, Vector3 velocity, float size, float life, float alpha)
        {
            var c = Color.Lerp(DustDark, DustLight, Random.value) * lit;
            c.a = alpha;
            var e = new ParticleSystem.EmitParams
            {
                position = at, velocity = velocity, startSize = size, startLifetime = life,
                rotation = Random.Range(0f, 360f), startColor = c,
            };
            ps.Emit(e, 1);
        }

        void Piece(ParticleSystem ps, Vector3 at, Vector3 velocity, float size, float life)
        {
            var e = new ParticleSystem.EmitParams
            {
                position = at, velocity = velocity, startSize = size, startLifetime = life,
                rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
                angularVelocity3D = Random.insideUnitSphere * 720f,
            };
            ps.Emit(e, 1);
        }

        // Dust rolling up behind a body going `flat` m/s: `perMetre` puffs for every metre above `from` m/s.
        void Trail(Watch w, float dt, float flat, float from, float perMetre, Vector3 v)
        {
            if (flat < from) return;
            w.acc += (flat - from) * perMetre * dt * scale;
            var back = new Vector3(-v.x, 0f, -v.z).normalized;
            while (w.acc >= 1f)
            {
                w.acc -= 1f;
                if (!Floor(w.t.TransformPoint(w.feet[Random.Range(0, 4)]), out var at)) continue;
                var side = Vector3.Cross(Vector3.up, back) * Random.Range(-.6f, .6f);
                Puff(dust, at + side * .3f, back * Random.Range(.3f, 1.2f) + side + Vector3.up * Random.Range(.25f, .8f),
                     Random.Range(.45f, .9f), Random.Range(1.6f, 2.6f), Random.Range(.35f, .6f));
                if (Random.value < .25f)
                    Piece(grit, at, back * Random.Range(.5f, 1.5f) + Vector3.up * Random.Range(.6f, 1.6f) + Random.insideUnitSphere * .4f,
                          Random.Range(.015f, .035f), Random.Range(.6f, 1.1f));
            }
        }

        // Dust under each bottom point moving faster than `from` m/s along the floor.
        void TrailPoints(Watch w, float dt, float from, float perMetre)
        {
            for (int i = 0; i < 4; i++)
            {
                var p = w.t.TransformPoint(w.feet[i]);
                var v = (p - w.last[i]) / dt;
                float flat = new Vector2(v.x, v.z).magnitude;
                if (flat < from || flat > MaxSpeed) continue;
                w.acc += (flat - from) * perMetre * dt * scale * .25f;
                while (w.acc >= 1f)
                {
                    w.acc -= 1f;
                    if (!Floor(p, out var at)) continue;
                    var dir = new Vector3(v.x, 0f, v.z).normalized;
                    Puff(dust, at + Random.insideUnitSphere * .15f, dir * Random.Range(.5f, 1.6f) + Vector3.up * Random.Range(.15f, .6f),
                         Random.Range(.4f, .8f), Random.Range(1.5f, 2.4f), Random.Range(.3f, .55f));
                }
            }
        }

        void Charge(Watch w, Vector3 v)
        {
            var back = new Vector3(-v.x, 0f, -v.z).normalized;
            for (int i = 0; i < 4; i++)
            {
                if (!Floor(w.t.TransformPoint(w.feet[i]), out var at)) continue;
                for (int k = N(7f); k > 0; k--)
                    Piece(clods, at + Random.insideUnitSphere * .15f, back * Random.Range(1.5f, 3.8f) + Vector3.up * Random.Range(1.5f, 3.2f) + Random.insideUnitSphere * .8f,
                          Random.Range(.03f, .08f), Random.Range(1f, 1.8f));
                for (int k = N(5f); k > 0; k--)
                    Puff(dust, at + Random.insideUnitSphere * .3f, back * Random.Range(1f, 2.6f) + Vector3.up * Random.Range(.4f, 1.2f),
                         Random.Range(.6f, 1.1f), Random.Range(1.8f, 2.8f), Random.Range(.45f, .7f));
            }
            if (Floor(w.t.TransformPoint(w.feet[4]), out var mid))
                for (int k = N(3f); k > 0; k--)
                    Puff(haze, mid + Random.insideUnitSphere * .8f, back * Random.Range(.4f, 1f) + Vector3.up * Random.Range(.2f, .5f),
                         Random.Range(1.6f, 2.6f), Random.Range(3f, 4.5f), Random.Range(.18f, .3f));
        }

        // Set down: a skirt of dust shoved out low along the floor from the foot's edge (the air pushed out from under it),
        // a slow haze over it, grit and chips skipping off.
        void Landed(Watch w)
        {
            var c = w.t.TransformPoint(w.feet[4]);
            if (!Floor(c, out var at)) return;
            float reach = 0f;
            for (int i = 0; i < 4; i++) reach = Mathf.Max(reach, Vector3.Distance(Flat(w.t.TransformPoint(w.feet[i])), Flat(c)));
            int n = N(22f);
            for (int i = 0; i < n; i++)
            {
                float a = (i + Random.value * .7f) / Mathf.Max(1, n) * Mathf.PI * 2f;
                var out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Puff(dust, at + out_ * reach * .8f, out_ * Random.Range(2f, 3.6f) + Vector3.up * Random.Range(.05f, .35f),
                     Random.Range(.4f, .75f), Random.Range(1.6f, 2.4f), Random.Range(.4f, .65f));
            }
            for (int i = N(4f); i > 0; i--)
            {
                var out_ = Random.insideUnitCircle.normalized;
                Puff(haze, at + new Vector3(out_.x, 0f, out_.y) * reach, new Vector3(out_.x, 0f, out_.y) * Random.Range(.4f, .9f) + Vector3.up * Random.Range(.2f, .45f),
                     Random.Range(1.6f, 2.4f), Random.Range(3f, 4f), Random.Range(.15f, .25f));
            }
            for (int i = N(12f); i > 0; i--)
            {
                var out_ = Random.insideUnitCircle.normalized;
                var p = at + new Vector3(out_.x, 0f, out_.y) * reach * .9f;
                var v = new Vector3(out_.x, 0f, out_.y) * Random.Range(.6f, 2f) + Vector3.up * Random.Range(1.2f, 2.6f);
                if (Random.value < .5f) Piece(chips, p, v, Random.Range(.03f, .06f), Random.Range(1.2f, 2f));
                else Piece(grit, p, v, Random.Range(.02f, .04f), Random.Range(.9f, 1.5f));
            }
        }

        // The giant chest lands: a wall of dust thrown out from its edge that reaches the shockwave's radius in the wave's
        // time (the gameplay wave: whoever it reaches on the floor is knocked down), a haze over it, clods and grit flying.
        void Slammed(Watch w)
        {
            var c = w.t.TransformPoint(w.feet[4]);
            if (!Floor(c, out var at)) return;
            float edge = 0f;
            for (int i = 0; i < 4; i++) edge = Mathf.Max(edge, Vector3.Distance(Flat(w.t.TransformPoint(w.feet[i])), Flat(c)));
            float radius = w.slam != null ? w.slam.ShockRadius : edge + 4f, seconds = w.slam != null ? Mathf.Max(.1f, w.slam.WaveSeconds) : .5f;
            float speed = Mathf.Max(1f, (radius - edge) / seconds) * 1.1f;
            int n = N(48f);
            for (int i = 0; i < n; i++)
            {
                float a = (i + Random.value * .7f) / Mathf.Max(1, n) * Mathf.PI * 2f;
                var out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Puff(wall, at + out_ * edge * .9f + Vector3.up * Random.Range(0f, .3f), out_ * speed * Random.Range(.85f, 1.05f) + Vector3.up * Random.Range(.2f, .9f),
                     Random.Range(.7f, 1.2f), Random.Range(2f, 3f), Random.Range(.5f, .75f));
            }
            for (int i = N(10f); i > 0; i--)
            {
                var out_ = Random.insideUnitCircle.normalized;
                Puff(haze, at + new Vector3(out_.x, 0f, out_.y) * edge, new Vector3(out_.x, 0f, out_.y) * Random.Range(.8f, 2f) + Vector3.up * Random.Range(.4f, 1f),
                     Random.Range(2.2f, 3.2f), Random.Range(3.5f, 5f), Random.Range(.2f, .32f));
            }
            for (int i = N(28f); i > 0; i--)
            {
                var out_ = Random.insideUnitCircle.normalized;
                var p = at + new Vector3(out_.x, 0f, out_.y) * edge;
                var v = new Vector3(out_.x, 0f, out_.y) * Random.Range(2f, 5f) + Vector3.up * Random.Range(2f, 4.5f);
                if (Random.value < .4f) Piece(clods, p, v, Random.Range(.04f, .09f), Random.Range(1.4f, 2.2f));
                else Piece(grit, p, v, Random.Range(.02f, .045f), Random.Range(1.2f, 2f));
            }
        }

        // A pallet starting off: the ratchet's sparks from its four top corners, chips knocked off the boards.
        void Jolt(Watch w)
        {
            float top = float.MinValue;
            foreach (var r in w.t.GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y);
            if (top == float.MinValue) return;
            var e = new ParticleSystem.EmitParams();
            for (int i = 0; i < 4; i++)
            {
                var corner = w.t.TransformPoint(w.feet[i]);
                corner.y = top;
                for (int k = N(9f); k > 0; k--)
                {
                    e.position = corner + Random.insideUnitSphere * .04f;
                    e.velocity = Vector3.up * Random.Range(.6f, 2.2f) + Random.insideUnitSphere * 2.2f;
                    e.startLifetime = Random.Range(.35f, .8f);
                    e.startSize = Random.Range(.02f, .035f);
                    e.startColor = Color.white;
                    sparks.Emit(e, 1);
                }
                for (int k = N(3f); k > 0; k--)
                    Piece(chips, corner + Random.insideUnitSphere * .12f, Vector3.up * Random.Range(.6f, 1.6f) + Random.insideUnitSphere * .9f,
                          Random.Range(.025f, .05f), Random.Range(1f, 1.6f));
            }
        }

        // The final gate: dust along its foot while it opens, gold glints when it is all the way open.
        void LookPush(PushWatch p)
        {
            if (p.gate == null || !p.gate.gameObject.activeInHierarchy) return;
            float now = p.gate.Progress;
            bool open = p.gate.IsOpen;
            if (now > p.progress + 1e-4f || open && !p.open)
            {
                bool any = false;
                var box = new Bounds();
                foreach (var r in p.gate.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                    if (!any) { box = r.bounds; any = true; } else box.Encapsulate(r.bounds);
                }
                if (any)
                {
                    if (now > p.progress)
                        for (int i = N(Mathf.Min(5f, (now - p.progress) * 140f)); i > 0; i--)
                        {
                            var at = new Vector3(Random.Range(box.min.x, box.max.x), box.min.y, Random.Range(box.min.z, box.max.z));
                            if (!Floor(at, out var floor)) continue;
                            Puff(dust, floor, Vector3.up * Random.Range(.15f, .5f) + Random.insideUnitSphere * .5f,
                                 Random.Range(.4f, .7f), Random.Range(1.6f, 2.4f), Random.Range(.3f, .5f));
                        }
                    if (open && !p.open)
                    {
                        var e = new ParticleSystem.EmitParams();
                        for (int i = N(60f); i > 0; i--)
                        {
                            e.position = new Vector3(Random.Range(box.min.x, box.max.x), Random.Range(box.min.y + .5f, box.max.y), Random.Range(box.min.z, box.max.z));
                            e.velocity = Vector3.up * Random.Range(.2f, .8f) + Random.insideUnitSphere * .35f;
                            e.startLifetime = Random.Range(1.2f, 2.4f);
                            e.startSize = Random.Range(.12f, .28f);
                            e.rotation = Random.Range(0f, 45f);
                            e.startColor = Color.white;
                            glints.Emit(e, 1);
                        }
                    }
                }
            }
            p.progress = now;
            p.open = open;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ------------------------------------------------------------------ light and quality

        // What lights the dust: the ambient plus the sun's colour times how high it stands (the lighting rig's directional
        // light, or the brightest one), no brighter than white so the dust never glows.
        void ReadLight()
        {
            if (sun == null || !sun.isActiveAndEnabled)
            {
                sun = RenderSettings.sun;
                if (sun == null || !sun.isActiveAndEnabled)
                {
                    sun = null;
                    foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (l.type == LightType.Directional && l.isActiveAndEnabled && (sun == null || l.intensity > sun.intensity)) sun = l;
                }
            }
            var c = RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight
                ? (RenderSettings.ambientSkyColor + RenderSettings.ambientEquatorColor) * .5f
                : RenderSettings.ambientLight;
            if (sun != null) c += sun.color * sun.intensity * .55f * Mathf.Clamp01(.25f - sun.transform.forward.y);
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak < .35f) c = Color.Lerp(c, Color.white, .35f - peak);
            if (peak > 1f) c /= peak;
            c.a = 1f;
            lit = c;
        }

        void UpdateTier()
        {
            int now = Mathf.Min(SettingsTier(), maxTier);
            if (now == tier) return;
            tier = now;
            scale = tier == 0 ? .35f : tier == 1 ? .65f : 1f;
            foreach (var ps in new[] { chips, clods, grit, sparks })
            {
                if (ps == null) continue;
                var collision = ps.collision;
                collision.enabled = tier > 0;
                collision.quality = tier == 2 ? ParticleSystemCollisionQuality.Medium : ParticleSystemCollisionQuality.Low;
            }
            foreach (var ps in new[] { dust, haze, wall })
            {
                if (ps == null) continue;
                var noise = ps.noise;
                noise.enabled = tier > 0;
            }
        }

        static int SettingsTier()
        {
            int q = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            if (q >= 0 && q < names.Length && names[q].Contains("Mobile")) return 0;
            int preset = GameSettings.Preset;
            if (preset <= 2) return Mathf.Max(0, preset);
            int shadow = GameSettings.Shadow;
            return shadow >= 3 ? 2 : shadow >= 2 ? 1 : 0;
        }

        // ------------------------------------------------------------------ the systems

        ParticleSystem NewSystem(string name, int max, out ParticleSystemRenderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        Material Unlit(string name, Texture texture, Color tint, bool additive)
        {
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null)
            {
                Debug.LogWarning("[마차뜰 효과] 파티클 셰이더가 없습니다: " + LitMaterials.ParticlesUnlitName);
                return null;
            }
            var m = new Material(shader) { mainTexture = texture, color = tint, name = name };
            LitMaterials.MakeTransparent(m, additive);
            made.Add(m);
            return m;
        }

        // Dust: a random puff of the sheet, turning slowly, growing to `grow` times its size (fast, then slower), thinning
        // out, carried by turbulence (`turbulence` m/s) and slowed by the air; a touch of lift as it warms.
        ParticleSystem Puffs(string name, int max, float grow, float turbulence)
        {
            var ps = NewSystem(name, max, out var r);
            r.sharedMaterial = Unlit(name, smoke, Color.white, false);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = 2f;
            var main = ps.main;
            main.gravityModifier = -.015f;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = Sheet;
            sheet.numTilesY = Sheet;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            // A random still frame per puff: two constants over the whole sheet (0..1), so each puff picks one.
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, .999f);
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-.5f, .5f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f, 0f, 3f * (grow - 1f)), new Keyframe(1f, grow, 0f, 0f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.9f, .9f, .9f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .07f), new GradientAlphaKey(.55f, .45f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = .35f;
            drag.dampen = .07f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = turbulence;
            noise.frequency = .45f;
            noise.scrollSpeed = .25f;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.damping = true;
            return ps;
        }

        // Chips, clods, grit: small irregular pieces, lit by the scene, tumbling, bouncing and settling on the floor, then
        // shrinking away.
        ParticleSystem Pieces(string name, Color colour, int max, float gravity, float bounce)
        {
            var ps = NewSystem(name, max, out var r);
            var m = new Material(LitMaterials.Lit) { name = name, color = colour };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .15f);
            made.Add(m);
            r.sharedMaterial = m;
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = chunk;
            r.alignment = ParticleSystemRenderSpace.World;
            var main = ps.main;
            main.gravityModifier = gravity;
            main.startRotation3D = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.8f, 1f), new Keyframe(1f, 0f)));
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.enableDynamicColliders = false;
            collision.bounce = new ParticleSystem.MinMaxCurve(bounce * .6f, bounce);
            collision.dampen = new ParticleSystem.MinMaxCurve(.35f, .6f);
            collision.radiusScale = .5f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            return ps;
        }

        // Sparks: stretched by their speed, white-hot to orange to dull red, skipping on the floor.
        ParticleSystem Sparks()
        {
            var ps = NewSystem("Yard fx sparks", 200, out var r);
            r.sharedMaterial = Unlit("Yard fx sparks", dot, SparkHdr, true);
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .045f;
            r.lengthScale = 1.2f;
            var main = ps.main;
            main.gravityModifier = 1.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 1f, .92f), 0f), new GradientColorKey(new Color(1f, .62f, .28f), .35f), new GradientColorKey(new Color(.75f, .16f, .04f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .4f));
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.enableDynamicColliders = false;
            collision.bounce = new ParticleSystem.MinMaxCurve(.3f, .5f);
            collision.dampen = new ParticleSystem.MinMaxCurve(.2f, .4f);
            collision.lifetimeLoss = .25f;
            collision.radiusScale = .3f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            return ps;
        }

        // Glints: four-pointed stars that twinkle (noise on their size) and drift up.
        ParticleSystem Glints()
        {
            var ps = NewSystem("Yard fx glints", 200, out var r);
            r.sharedMaterial = Unlit("Yard fx glints", star, GlintHdr, true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var main = ps.main;
            main.gravityModifier = -.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .15f;
            noise.frequency = 2f;
            noise.scrollSpeed = 3f;
            noise.sizeAmount = .9f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            return ps;
        }

        // ------------------------------------------------------------------ textures and the chunk mesh

        static void Textures()
        {
            if (smoke == null) smoke = SmokeSheet(128);
            if (dot == null) dot = Draw(64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 1.6f));
            if (star == null)
                star = Draw(64, (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    float rays = Mathf.Exp(-Mathf.Abs(x) * 14f) * Mathf.Exp(-y * y * 3f) + Mathf.Exp(-Mathf.Abs(y) * 14f) * Mathf.Exp(-x * x * 3f);
                    return Mathf.Clamp01(rays * .8f + Mathf.Exp(-r * r * 30f)) * Mathf.Clamp01(1f - r);
                });
            if (chunk == null) chunk = Chunk();
        }

        static float Fbm(float x, float y)
        {
            float sum = 0f, amp = .5f, norm = 0f;
            for (int i = 0; i < 4; i++)
            {
                sum += Mathf.PerlinNoise(x, y) * amp;
                norm += amp;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f + 9.7f;
                amp *= .5f;
            }
            return sum / norm;
        }

        // Sheet x Sheet puffs of dust, `n` pixels each: a soft round body whose edge is pushed about by fractal noise
        // (domain warp), denser knots inside, lit from above (a lighter top, a shaded underside) - grey, so the
        // particle's colour tints it.
        static Texture2D SmokeSheet(int n)
        {
            int size = n * Sheet;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Yard fx smoke" };
            var pixels = new Color32[size * size];
            for (int cell = 0; cell < Sheet * Sheet; cell++)
            {
                int cx = cell % Sheet, cy = cell / Sheet;
                float ox = 31.7f * cell + 5.3f, oy = 17.9f * cell + 11.1f;
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        float x = (i + .5f) / n * 2f - 1f, y = (j + .5f) / n * 2f - 1f;
                        float wx = x + .55f * (Fbm(x * 1.6f + ox, y * 1.6f + oy) - .5f);
                        float wy = y + .55f * (Fbm(x * 1.6f + oy + 40f, y * 1.6f + ox + 40f) - .5f);
                        float r = Mathf.Sqrt(wx * wx + wy * wy);
                        float body = Mathf.Clamp01((.92f - r) / .7f);
                        body = body * body * (3f - 2f * body);
                        float knots = Fbm(wx * 2.6f + ox + 80f, wy * 2.6f + oy + 80f);
                        float a = Mathf.Clamp01(body * (.35f + 1.1f * knots) - .08f);
                        a = Mathf.Pow(a, 1.25f);
                        // Lit from above: brighter where the puff faces up (its top half, its denser knots), darker under.
                        float lit = Mathf.Clamp01(.66f + .26f * y + .18f * (knots - .5f) + .1f * (1f - r));
                        byte g = (byte)(lit * 255f);
                        pixels[(cy * n + j) * size + cx * n + i] = new Color32(g, g, g, (byte)(a * 255f));
                    }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static Texture2D Draw(int n, System.Func<float, float, float> mask)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Yard fx" };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    byte b = (byte)(Mathf.Clamp01(mask((i + .5f) / n * 2f - 1f, (j + .5f) / n * 2f - 1f)) * 255f);
                    pixels[j * n + i] = new Color32(255, 255, 255, b);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // A unit chip of stone or wood: a box with its eight corners pushed about, flat-shaded (24 vertices, 12 triangles).
        static Mesh Chunk()
        {
            var rng = new System.Random(685);
            float J() => (float)(rng.NextDouble() - .5) * .45f;
            var corner = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corner[i] = new Vector3(((i & 1) == 0 ? -.5f : .5f) * 1.2f + J(), ((i & 2) == 0 ? -.5f : .5f) * .7f + J(), ((i & 4) == 0 ? -.5f : .5f) + J());
            int[][] faces =
            {
                new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 },
                new[] { 2, 6, 7, 3 }, new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 },
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var f in faces)
            {
                int b = verts.Count;
                foreach (int k in f) verts.Add(corner[k]);
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = "Yard fx chunk" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
