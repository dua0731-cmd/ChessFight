using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The knight's ultimates in the world, on every PC (JY-skill-lab, 2026-11-22), drawn after the chess edit shorts the user
    /// gave - KNIGHTMARE, Most FIRE Knight FORK, For Love Sacrifices Are Made, Bro sacrificed everything - and played on the
    /// move (RagdollPawn.KnightUlts.cs: nothing here takes anyone's control or camera):
    ///
    ///   night falls      the world's lights go down and the sky turns to stars, so what glows is all that is seen; it comes
    ///                    back after the landing (PawnRushSkillFx.UltScreen.cs)
    ///   the knight       a white-blue electric aura (arcs crawling over its body, flames rising off it), a red ember for an
    ///                    eye, a comet's trail through the air
    ///   the aim          the fork: its landing square and the eight knight's squares on the floor in teal; a square with an
    ///                    enemy on it goes red and a red ring hangs over that enemy; a ring closing on the landing turns
    ///                    teal in the beat window. The tour: a red ring on the head the camera picks (for the piece played
    ///                    here, before the key too), closing and turning teal on the beat
    ///   the strike       red light falling from the sky on every forked enemy; a burst of light on every head stomped, the
    ///                    move written over it
    ///   the landing      by the judgement: for !! and ! the board catches fire round it (ember squares, flames), the
    ///                    knight's eight squares rise in light; a huge badge (!! ! ★ ?! ??) over it and the word on screen
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Color Teal = new Color(0.12f, 1f, 0.8f);
        static readonly Color TealPale = new Color(0.72f, 1f, 0.94f);
        static readonly Color Blood = new Color(1f, 0.1f, 0.14f);
        static readonly Color Electric = new Color(0.6f, 0.88f, 1f);
        static readonly Color Ember = new Color(1f, 0.42f, 0.08f);
        /// <summary>chess.com's colours for the judgements: brilliant, great, best, inaccuracy, blunder.</summary>
        static readonly Color[] GradeColors =
        {
            new Color(0.12f, 0.72f, 0.6f), new Color(0.31f, 0.53f, 0.9f), new Color(0.44f, 0.68f, 0.18f),
            new Color(0.88f, 0.7f, 0.17f), new Color(0.9f, 0.27f, 0.23f),
        };
        static readonly string[] GradeBadges = { "!!", "!", "★", "?!", "??" };
        static readonly string[] TourNotes = { "Nf5", "Nd6", "Ne8", "Nc7", "Nb5", "Nd4", "Nf3", "Ne5" };

        sealed class UltShow
        {
            public RagdollPawn by;
            public int skill;
            public float t0, air = 1.6f;
            public float hitAt = -1f, endAt = -1f, lastSeen, timingAt = -1f, stompAt = -1f, finalAt = -1f;
            public bool timingPerfect, cut, gone;
            public int grade = -1, hitCount, struck;
            public Vector3 hitPoint, finalPoint;
            public readonly HashSet<RagdollPawn> struckSet = new HashSet<RagdollPawn>();
            public string title = "", nota = "!!";
            public bool Fork => skill == PawnRushSkillIds.FamilyFork;
            public bool Tour => skill == PawnRushSkillIds.KnightTour;
            /// <summary>How much of the show is on (1 while it plays, falling after its end).</summary>
            public float Life(float clock) => gone ? 0f : endAt < 0f ? 1f : Mathf.Clamp01(1f - (clock - endAt) / (cut ? 0.5f : 1.5f));
            /// <summary>Still in the air or on the heads (before the landing).</summary>
            public bool Playing => hitAt < 0f && endAt < 0f && !gone;
        }

        readonly List<UltShow> ults = new List<UltShow>();
        Material matFrame, matSky;
        Texture2D texFrame, texStars;
        Mesh meshCube;
        bool ultBuilt;

        static Color HdrA(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static float Pop(float age, float rise = 0.06f) => age < 0f ? 0f : age < rise ? age / rise : 1f;

        void BuildUltParts()
        {
            if (ultBuilt) return;
            ultBuilt = true;
            texFrame = Tex(128, 128, TextureWrapMode.Clamp, (u, v) =>
            {
                float d = Mathf.Min(Mathf.Min(u, v), Mathf.Min(1f - u, 1f - v));
                return White(Mathf.Clamp01(Mathf.Exp(-d / 0.022f) * 1.2f + Mathf.Exp(-d / 0.09f) * 0.25f));
            });
            texStars = new Texture2D(1024, 512, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[1024 * 512];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 255);
            var rnd = new System.Random(1912);
            for (int i = 0; i < 2600; i++)
            {
                int x = rnd.Next(1024), y = 40 + rnd.Next(472);
                float b = Mathf.Pow((float)rnd.NextDouble(), 3f);
                byte c = (byte)(70 + 185 * b);
                px[y * 1024 + x] = new Color32(c, c, (byte)Mathf.Min(255, c + 20), 255);
            }
            texStars.SetPixels32(px);
            texStars.Apply(true, true);
            matFrame = Glow(texFrame);
            matSky = Glow(texStars, opacity: 1f);
            matSky.renderQueue = 2900;   // after the sky, before every effect: it shows only where nothing solid is in front
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshCube = Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            meshCube.hideFlags = HideFlags.HideAndDontSave;
            Destroy(tmp);
        }

        void DestroyUlt()
        {
            UltScreenDestroy();
            foreach (var o in new Object[] { texFrame, texStars, meshCube }) if (o != null) Destroy(o);
        }

        UltShow ShowOf(RagdollPawn by)
        {
            for (int i = ults.Count - 1; i >= 0; i--) if (ults[i].by == by && !ults[i].gone) return ults[i];
            return null;
        }

        float FamSquare => Params != null ? Params.famSquare : 2.2f;
        float FamCatch => Params != null ? Params.famCatch : 1.5f;
        float FamBeat => Params != null ? Params.famBeat : 0.22f;

        // ---------------------------------------------------------------- the moments

        void UltCast(SkillFxEvent e)
        {
            if (e.by == null) return;
            var old = ShowOf(e.by);
            if (old != null) old.gone = true;
            BuildUltParts();
            var u = new UltShow { by = e.by, skill = e.count, t0 = Clock, air = e.size > 0.1f ? e.size : 1.6f, lastSeen = Clock };
            ults.Add(u);
            UltTakeOff(u, e.at);
            UltAura(u);
            if (u.Fork) UltForkAim(u);
            if (u.Tour) UltHeadRing(u);
            UltScreenStart(u);
        }

        void UltGo(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null) return;
            if (e.size > 0.5f)
            {
                // The tour's last leap: where it means to come down.
                u.finalAt = Clock;
                u.finalPoint = e.at;
                UltLandingSquare(u, () => u.by != null && u.by.PlanPoint != Vector3.zero ? u.by.PlanPoint : u.finalPoint, () => u.Playing ? 1f : 0f);
                UltSound(UltSfx.Leap, e.at);
                return;
            }
            UltSound(UltSfx.Leap, e.at, u.struck + 1);
        }

        void UltTiming(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null) return;
            u.timingAt = Clock;
            u.timingPerfect = e.count == 1;
            Vector3 at = e.target != null ? HeadOf(e.target) + Vector3.up * 0.6f : e.at + Vector3.up * 0.4f;
            if (u.timingPerfect)
            {
                Flare(at, TealPale, 2.2f, 6f, 0.25f);
                SparkBurst(at, 26, Teal, Vector3.up, 80f, 3f, 7f, 0.5f, 0.05f);
                UltPopWord(at + Vector3.up * 0.6f, "박자!", HdrA(Teal, 1.2f), 0.55f, 0.9f);
            }
            else UltPopWord(at + Vector3.up * 0.6f, "빗나감", new Color(0.75f, 0.75f, 0.78f), 0.4f, 0.7f);
            UltSound(u.timingPerfect ? UltSfx.Perfect : UltSfx.Off, at);
            UltScreenTiming(u);
        }

        void UltStrike(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null || e.target == null) return;
            u.struck++;
            u.struckSet.Add(e.target);
            if (u.Fork) UltRedBeam(e.target);
            else UltStomp(u, e);
        }

        void UltHit(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null) return;
            u.hitAt = Clock;
            u.hitPoint = e.at;
            u.hitCount = e.count;
            u.grade = Mathf.Clamp(Mathf.RoundToInt(e.size), 0, GradeBadges.Length - 1);
            UltImpact(u, e);
            UltScreenHit(u, e);
        }

        void UltEnd(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null || u.endAt >= 0f) return;
            u.endAt = Clock;
            u.cut = e.count == 1 && u.hitAt < 0f;
        }

        // ---------------------------------------------------------------- the take-off and the knight

        void UltTakeOff(UltShow u, Vector3 feet)
        {
            var floor = Ground(feet);
            Add(new Shape(root, "Ult take-off square", meshQuad, matTile)
            {
                life = 0.8f, at = floor + Vector3.up * 0.04f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare, FamSquare, 1f), paint = 0.35f,
                animate = (s, t) => { s.color = HdrA(Teal, 1.6f); s.bright = (1f + 1.5f * Mathf.Exp(-s.Age / 0.1f)) * (1f - t); },
            });
            UltSquareWave(floor, FamSquare * 0.5f, 14f, 0.7f, Teal, 2.2f);
            Flare(floor + Vector3.up, Electric, 3f, 8f, 0.4f);
            SparkBurst(floor + Vector3.up * 0.2f, 40, Electric, Vector3.up, 40f, 4f, 10f, 0.6f, 0.06f);
            DustRing(floor, 12, 0.6f, 5f, new Color(0.6f, 0.6f, 0.62f), 0.8f, 0.8f);
            UltSound(UltSfx.Boom, floor);
        }

        static readonly BodyId[] AuraParts = { BodyId.Head, BodyId.Chest, BodyId.Hips, BodyId.HandL, BodyId.HandR, BodyId.FootL, BodyId.FootR };

        /// <summary>The knight's aura while it plays: electric arcs crawling over its body (and now and then to the floor),
        /// white-blue flames streaming off it, a light on it, a red ember for an eye; a comet's trail behind it.</summary>
        void UltAura(UltShow u)
        {
            var by = u.by;
            var light = new GameObject("Ult aura light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.color = Electric;
            light.range = 7f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            var eye = new GameObject("Ult eye").AddComponent<Light>();
            eye.transform.SetParent(root, false);
            eye.type = LightType.Point;
            eye.color = Blood;
            eye.range = 1.6f;
            eye.shadows = LightShadows.None;
            float nextArc = 0f;
            anims.Add(new Ongoing((age, dt) =>
            {
                float k = u.Life(Clock) * (u.hitAt >= 0f ? Mathf.Clamp01(1f - (Clock - u.hitAt) / 0.6f) : 1f);
                if (k <= 0f || by == null) return false;
                Vector3 chest = ChestOf(by), head = HeadOf(by), fwd = Flat(by.Facing).sqrMagnitude > 1e-4f ? Flat(by.Facing).normalized : Vector3.forward;
                light.transform.position = chest + Vector3.up * 0.5f;
                light.intensity = k * (2.2f + 0.8f * Mathf.Sin(Clock * 31f) * Mathf.Sin(Clock * 17f));
                eye.transform.position = head + fwd * 0.22f + Vector3.up * 0.04f;
                eye.intensity = k * (1.6f + 0.6f * Mathf.Sin(Clock * 9f));
                if (dt <= 0f) return true;
                nextArc -= dt;
                if (nextArc <= 0f && arc != null)
                {
                    nextArc = Random.Range(0.035f, 0.07f);
                    Vector3 a = Part(by, AuraParts[Random.Range(0, AuraParts.Length)]) + Random.insideUnitSphere * 0.05f;
                    Vector3 b = Random.value < 0.2f && by.OnTheFloor
                        ? Ground(by.Hips.position) + Random.insideUnitSphere.normalized * 0.5f
                        : Part(by, AuraParts[Random.Range(0, AuraParts.Length)]) + Random.insideUnitSphere * 0.25f;
                    if ((b - a).sqrMagnitude < 0.02f) b = a + Random.onUnitSphere * 0.35f;
                    arc.Strike(a, b, HdrA(Electric, 1.6f), 0.11f, 0.06f, 0.9f, 0.3f, 1);
                }
                // Flames off its body, streaming back the way it flies; motes like a comet's tail; red sparks from the eye.
                Vector3 vel = by.bodies[0].linearVelocity;
                if (Random.value < 0.75f)
                    smoke.Emit(chest + Random.insideUnitSphere * 0.3f, Vector3.up * Random.Range(1.2f, 2.2f) - vel * 0.25f,
                               Random.Range(0.45f, 0.8f), Random.Range(0.35f, 0.6f), Tint(Electric, Random.value * 0.5f));
                motes.Emit(chest + Random.insideUnitSphere * 0.35f, -vel * 0.15f + Random.insideUnitSphere * 0.6f,
                           Random.Range(0.06f, 0.12f), Random.Range(0.5f, 1f), Tint(Random.value < 0.5f ? Electric : Color.white, 0.2f));
                if (Random.value < 0.35f)
                    sparks.Emit(eye.transform.position, -vel * 0.2f + Random.insideUnitSphere * 0.4f, 0.035f, 0.35f, Tint(Blood, 0.1f));
                return true;
            }, () =>
            {
                if (light != null) Object.Destroy(light.gameObject);
                if (eye != null) Object.Destroy(eye.gameObject);
            }));
            anims.Add(new Streak(root, matTrail, HdrA(Electric, 3f), 0.9f, 0.55f, () => by != null && u.Playing ? by.Hips.position : (Vector3?)null));
        }

        // ---------------------------------------------------------------- the aim

        /// <summary>The fork's aim while it flies: the landing square, its eight knight's squares (red where an enemy stands,
        /// with a red ring over it), the count, and the ring that closes on the landing and turns teal on the beat.</summary>
        void UltForkAim(UltShow u)
        {
            Vector3 Landing() => u.by != null && u.by.PlanPoint != Vector3.zero ? u.by.PlanPoint : u.by != null ? FloorUnder(u.by) : Vector3.zero;
            float On() => u.Playing ? Pop(Clock - u.t0, 0.15f) : 0f;
            UltLandingSquare(u, Landing, On);
            var forked = new RagdollPawn[KnightSquares.Length];
            for (int i = 0; i < KnightSquares.Length; i++)
            {
                int k = i;
                var sq = KnightSquares[i];
                Vector3 Center() => Landing() + new Vector3(sq.x * FamSquare, 0.05f, sq.y * FamSquare);
                Add(new Shape(root, "Ult knight square", meshQuad, matTile)
                {
                    life = 60f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare * 0.94f, FamSquare * 0.94f, 1f), paint = 0.35f,
                    animate = (s, t) =>
                    {
                        if (!u.Playing) { s.life = 0f; return; }
                        Vector3 c = Center();
                        forked[k] = EnemyOnSquare(u.by, c, FamCatch);
                        s.at = c;
                        bool red = forked[k] != null;
                        s.color = red ? HdrA(Blood, 1.7f) : HdrA(Teal, 0.55f);
                        s.bright = On() * (red ? 1f + 0.3f * Mathf.Sin(Clock * 16f) : 0.8f);
                    },
                });
                Add(new Shape(root, "Ult knight square edge", meshQuad, matFrame)
                {
                    life = 60f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare, FamSquare, 1f),
                    animate = (s, t) =>
                    {
                        if (!u.Playing) { s.life = 0f; return; }
                        s.at = Center() + Vector3.up * 0.01f;
                        s.color = forked[k] != null ? HdrA(Blood, 2.8f) : HdrA(Teal, 2f);
                        s.bright = On();
                    },
                });
                UltRing(() => forked[k] != null && u.Playing ? HeadOf(forked[k]) + Vector3.up * 0.55f : (Vector3?)null, Blood, 0.55f, () => u.Playing);
                UltLight(() => forked[k] != null && u.Playing ? HeadOf(forked[k]) + Vector3.up * 0.9f : (Vector3?)null, Blood, 3.2f, 3f, () => u.Playing);
            }
            // The count of who it would fork, over the landing.
            var count = new Text3D(root, font, " ", Color.white, 0.5f);
            int shown = -1;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!u.Playing) return false;
                int n = 0;
                foreach (var f in forked) if (f != null) n++;
                if (n != shown)
                {
                    shown = n;
                    count.Destroy();
                    count = new Text3D(root, font, n > 0 ? $"포크 ×{n}" : "포크 칸에 적이 없음", n >= 2 ? HdrA(Teal, 1.3f) : n == 1 ? Color.white : new Color(0.8f, 0.8f, 0.85f), 0.5f);
                }
                var cam = ViewCamera;
                float far = cam != null ? Vector3.Distance(cam.transform.position, Landing()) : 10f;
                count.Place(Landing() + Vector3.up * 2.2f, cam, Mathf.Clamp(far / 8f, 1f, 4f), On());
                return true;
            }, () => count.Destroy()));
            // The beat: a square closing on the landing over the last 0.9 s of the fall, teal (and bright) in the window.
            Add(new Shape(root, "Ult beat ring", meshQuad, matFrame)
            {
                life = 60f, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    float left = u.t0 + u.air - Clock, w = Mathf.Clamp01(1f - left / 0.9f);
                    bool window = left <= FamBeat && left > -0.05f;
                    float size = FamSquare * Mathf.Lerp(4f, 1.05f, w);
                    s.at = Landing() + Vector3.up * 0.08f;
                    s.scale = new Vector3(size, size, 1f);
                    s.color = window ? HdrA(Teal, 3.2f) : HdrA(Color.white, 1.4f);
                    s.bright = left > 0.9f || (u.timingAt >= 0f) ? 0f : w;
                },
            });
        }

        /// <summary>An enemy the landing would fork on this square (the host's rule in RagdollPawn.FamLand: standing, not
        /// shielded, within 4 m of the landing's height).</summary>
        RagdollPawn EnemyOnSquare(RagdollPawn by, Vector3 center, float reach)
        {
            if (by == null) return null;
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == by || !Teams.AreEnemies(e.Team, by.Team) || e.State != PawnState.Active || e.SkillUntargetable || e.Floating) continue;
                Vector3 feet = e.FeetPoint;
                if (Mathf.Abs(feet.y - center.y) > 4f) continue;
                if (Flat(feet - center).magnitude <= reach) return e;
            }
            return null;
        }

        /// <summary>A landing square (teal, its edge bright) that follows <paramref name="where"/>.</summary>
        void UltLandingSquare(UltShow u, System.Func<Vector3> where, System.Func<float> on)
        {
            Add(new Shape(root, "Ult landing", meshQuad, matTile)
            {
                life = 60f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare, FamSquare, 1f), paint = 0.45f,
                animate = (s, t) =>
                {
                    float k = on();
                    if (k <= 0f && !u.Playing) { s.life = 0f; return; }
                    s.at = where() + Vector3.up * 0.05f;
                    s.color = HdrA(Teal, 1.6f);
                    s.bright = k * (1f + 0.3f * Mathf.Sin(Clock * 12f));
                },
            });
            Add(new Shape(root, "Ult landing edge", meshQuad, matFrame)
            {
                life = 60f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare * 1.06f, FamSquare * 1.06f, 1f),
                animate = (s, t) =>
                {
                    float k = on();
                    if (k <= 0f && !u.Playing) { s.life = 0f; return; }
                    s.at = where() + Vector3.up * 0.06f;
                    s.color = HdrA(TealPale, 3f);
                    s.bright = k;
                },
            });
        }

        /// <summary>A ring over something (billboard), there while <paramref name="where"/> gives a point.</summary>
        void UltRing(System.Func<Vector3?> where, Color color, float size, System.Func<bool> alive, System.Func<float> pulse = null)
        {
            Add(new Shape(root, "Ult ring", meshQuad, matRing)
            {
                life = 60f, billboard = true,
                animate = (s, t) =>
                {
                    if (!alive()) { s.life = 0f; return; }
                    var p = where();
                    s.bright = p.HasValue ? 1f + 0.25f * Mathf.Sin(Clock * 10f) : 0f;
                    if (!p.HasValue) return;
                    float k = size * (1f + (pulse != null ? pulse() : 0f));
                    s.at = p.Value;
                    s.scale = new Vector3(k, k, 1f);
                    s.color = HdrA(color, 2.4f);
                },
            });
        }

        /// <summary>A point light following <paramref name="where"/> while it gives a point (dark while it gives none), gone
        /// once <paramref name="alive"/> is false: the shorts' red glow on what is attacked, seen in the night.</summary>
        void UltLight(System.Func<Vector3?> where, Color color, float range, float intensity, System.Func<bool> alive)
        {
            var light = new GameObject("Ult target light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.shadows = LightShadows.None;
            light.intensity = 0f;
            light.enabled = false;
            float on = 0f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!alive() || light == null) return false;
                var p = where();
                on = Mathf.MoveTowards(on, p.HasValue ? 1f : 0f, dt / 0.08f);
                if (p.HasValue) light.transform.position = p.Value;
                light.intensity = intensity * on * (0.85f + 0.15f * Mathf.Sin(Clock * 14f));
                light.enabled = on > 0.001f;
                return true;
            }, () =>
            {
                if (light != null) Object.Destroy(light.gameObject);
            }));
        }

        /// <summary>The tour's next head: a red ring on it, a smaller one closing over it while the knight stands on a head,
        /// teal in the beat window.</summary>
        void UltHeadRing(UltShow u)
        {
            UltRing(() => u.Playing && u.by != null && u.by.PlanTarget != null && u.finalAt < 0f ? HeadOf(u.by.PlanTarget) + Vector3.up * 0.5f : (Vector3?)null,
                    Blood, 0.6f, () => u.Playing);
            UltLight(() => u.Playing && u.by != null && u.by.PlanTarget != null && u.finalAt < 0f ? HeadOf(u.by.PlanTarget) + Vector3.up * 0.9f : (Vector3?)null,
                     Blood, 3.2f, 3f, () => u.Playing);
            float beat0 = Params != null ? Params.ultTourBeat0 : 0.3f, beat1 = Params != null ? Params.ultTourBeat1 : 0.55f, wait = Params != null ? Params.ultTourWait : 0.8f;
            Add(new Shape(root, "Ult head beat", meshQuad, matRing)
            {
                life = 60f, billboard = true,
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    var target = u.by != null ? u.by.PlanTarget : null;
                    float stand = u.stompAt >= 0f ? Clock - u.stompAt : -1f;
                    bool standing = stand >= 0f && stand <= wait && target != null && u.finalAt < 0f && (u.timingAt < u.stompAt);
                    s.bright = standing ? 1f : 0f;
                    if (!standing) return;
                    bool window = stand >= beat0 && stand <= beat1;
                    float k = Mathf.Lerp(2.2f, 0.62f, Mathf.Clamp01(stand / beat1));
                    s.at = HeadOf(target) + Vector3.up * 0.5f;
                    s.scale = new Vector3(k, k, 1f);
                    s.color = window ? HdrA(Teal, 3.2f) : HdrA(Color.white, 1.5f);
                },
            });
        }

        /// <summary>For the piece played here, before its key: the red ring on the head the knight's tour would take.</summary>
        Shape tourPreview;
        RagdollPawn tourPreviewOn;

        void UltTourPreview(Camera cam)
        {
            var me = Viewer;
            RagdollPawn pick = null;
            if (me != null && cam != null && me.PlanSkill == 0 && me.HasSkill(PawnRushSkillIds.KnightTour) && me.UltReady && me.State == PawnState.Active)
                pick = RagdollPawn.TourPick(me, me.FeetPoint, cam.transform.forward, Params != null ? Params.ultTourStart : 22f, null);
            tourPreviewOn = pick;
            if (pick == null) return;
            BuildUltParts();
            if (tourPreview == null || !anims.Contains(tourPreview))
            {
                tourPreview = Add(new Shape(root, "Ult tour preview", meshQuad, matRing)
                {
                    life = 1e9f, billboard = true,
                    animate = (s, t) =>
                    {
                        var p = tourPreviewOn;
                        s.bright = p != null ? 0.8f + 0.2f * Mathf.Sin(Clock * 6f) : 0f;
                        if (p == null) return;
                        s.at = HeadOf(p) + Vector3.up * 0.5f;
                        s.scale = new Vector3(0.6f, 0.6f, 1f);
                        s.color = HdrA(Blood, 2f);
                    },
                });
            }
        }

        // ---------------------------------------------------------------- the strikes

        /// <summary>The tour's stomp: a burst of light on the head, its square flashing, the move written over it.</summary>
        void UltStomp(UltShow u, SkillFxEvent e)
        {
            u.stompAt = Clock;
            Vector3 head = HeadOf(e.target);
            Flare(head + Vector3.up * 0.3f, Electric, 3.5f, 6f, 0.3f);
            SparkBurst(head + Vector3.up * 0.2f, 34, Color.white, Vector3.up, 75f, 3f, 9f, 0.6f, 0.05f);
            if (arc != null) for (int i = 0; i < 5; i++) arc.Strike(head, Ground(e.target.Hips.position) + Random.insideUnitSphere.normalized * 0.9f, HdrA(Electric, 1.8f), 0.14f, 0.1f, 1f, 0.35f, 1);
            Vector3 floor = Ground(e.target.Hips.position);
            Add(new Shape(root, "Ult stomp square", meshQuad, matTile)
            {
                life = 1f, at = floor + Vector3.up * 0.05f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare, FamSquare, 1f), paint = 0.45f,
                animate = (s, t) => { s.color = HdrA(t < 0.25f ? Blood : Teal, 1.8f); s.bright = 1f - t; },
            });
            UltSquareWave(floor, FamSquare * 0.5f, 6f, 0.45f, TealPale, 2f);
            bool beat = e.size > 0.5f;
            string note = TourNotes[(Mathf.Max(1, e.count) - 1) % TourNotes.Length] + (beat ? "!" : "");
            UltPopWord(head + Vector3.up * 1.1f, note, beat ? HdrA(GradeColors[1], 1.4f) : TealPale, 0.6f, 1.2f);
            UltSound(UltSfx.Stomp, head, e.count);
            UltScreenStrike(u, e);
        }

        /// <summary>The fork's red light falling from the sky onto an enemy it throws.</summary>
        void UltRedBeam(RagdollPawn target)
        {
            Vector3 at = target.Hips.position;
            Add(new Shape(root, "Ult red beam", meshCylinder, matPillar)
            {
                life = 0.9f,
                animate = (s, t) =>
                {
                    float fall = Mathf.Clamp01(s.Age / 0.1f), w = Mathf.Lerp(0.55f, 1f, t);
                    s.at = new Vector3(at.x, Mathf.Lerp(60f, at.y - 1f, fall), at.z);
                    s.scale = new Vector3(w, 70f, w);
                    s.color = HdrA(Blood, 3f);
                    s.bright = 1f - Mathf.Clamp01((t - 0.3f) / 0.7f);
                },
            });
            Flare(at, Blood, 2.6f, 6f, 0.4f);
            SparkBurst(at, 26, Blood, Vector3.up, 60f, 3f, 9f, 0.6f, 0.05f);
        }

        // ---------------------------------------------------------------- the landing

        void UltImpact(UltShow u, SkillFxEvent e)
        {
            Vector3 at = Ground(e.at);
            int grade = u.grade;
            bool good = grade <= RagdollPawn.GradeGreat;
            Color gc = GradeColors[grade];
            Flare(at + Vector3.up * 1.5f, Color.white, good ? 5f : 3f, good ? 22f : 12f, 0.45f);
            UltSquareWave(at, FamSquare * 0.5f, good ? 26f : 14f, 1f, grade <= RagdollPawn.GradeBest ? Teal : gc, 3f);
            UltSquareWave(at, FamSquare * 0.5f, good ? 34f : 18f, 1.2f, Color.white, 1.4f, 0.08f);
            if (grade <= RagdollPawn.GradeBest)
                for (int i = 0; i < KnightSquares.Length; i++)
                {
                    var sq = KnightSquares[i];
                    Vector3 p = at + new Vector3(sq.x * FamSquare, 0f, sq.y * FamSquare);
                    float wait = i * 0.025f;
                    Add(new Shape(root, "Ult knight pillar", meshCylinder, matPillar)
                    {
                        life = 1.2f + wait, at = p,
                        animate = (s, t) =>
                        {
                            float a = s.Age - wait;
                            if (a < 0f) { s.bright = 0f; return; }
                            float h = (good ? 14f : 7f) * (1f - Mathf.Pow(1f - Mathf.Clamp01(a / 0.15f), 3f)), w = 0.5f * (1f + a * 0.4f);
                            s.scale = new Vector3(w, h, w);
                            s.color = HdrA(Teal, 2.2f);
                            s.bright = 1f - Mathf.Clamp01((a - 0.35f) / 0.85f);
                        },
                    });
                }
            DustRing(at, 18, 1.6f, 7f, new Color(0.45f, 0.45f, 0.47f), 1f, 1.1f);
            SparkBurst(at + Vector3.up * 0.4f, good ? 80 : 30, good ? Ember : Color.white, Vector3.up, 55f, 6f, 15f, 0.9f, 0.07f);
            if (good) UltFireBoard(at, grade == RagdollPawn.GradeBrilliant ? 10f : 5.5f);
            // The judgement over the landing: a huge badge.
            var words = new Text3D(root, font, GradeBadges[grade], HdrA(gc, 1.25f), 2.2f);
            Vector3 over = at + Vector3.up * 6f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age > 2f) return false;
                words.Place(over + Vector3.up * (0.4f * Smooth01(age / 2f)), ViewCamera, 1f + 0.9f * Mathf.Exp(-age / 0.09f), Mathf.Clamp01((2f - age) / 0.5f));
                return true;
            }, words.Destroy));
            UltSound(UltSfx.Impact, at, grade);
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, at) / 35f) : 0.5f;
            Shake(Viewer != null && Viewer == u.by ? 0.3f : 0.22f * near, 0.7f);
            stopBy = u.by;
            stopTarget = null;
            HitStop(good ? 0.12f : 0.06f);
            foreach (var m in u.struckSet) if (m != null) m.HoldVisual(0.12f);
        }

        /// <summary>The board catches fire round the landing (the FIRE knight fork's): its squares glow like embers from the
        /// middle out, flames and sparks rise off them, and they cool and go dark.</summary>
        void UltFireBoard(Vector3 at, float radius)
        {
            float sq = FamSquare;
            int n = Mathf.CeilToInt(radius / sq);
            for (int x = -n; x <= n; x++)
                for (int z = -n; z <= n; z++)
                {
                    Vector3 c = at + new Vector3(x * sq, 0f, z * sq);
                    float far = Flat(c - at).magnitude;
                    if (far > radius) continue;
                    var floor = Ground(c);
                    if (Mathf.Abs(floor.y - at.y) > 1.5f) continue;
                    float wait = far / 16f, life = 2.4f + Random.Range(-0.3f, 0.4f), flicker = Random.Range(0f, 6f), hot = Random.Range(0.75f, 1.25f);
                    Add(new Shape(root, "Ult ember square", meshQuad, matTile)
                    {
                        life = life + wait, at = floor + Vector3.up * 0.04f, rotation = FlatOnFloor(), scale = new Vector3(sq * 0.96f, sq * 0.96f, 1f), paint = 0.5f,
                        animate = (s, t) =>
                        {
                            float a = s.Age - wait;
                            if (a < 0f) { s.bright = 0f; return; }
                            float k = Pop(a, 0.08f) * (1f - Mathf.Clamp01((a - (life - 0.9f)) / 0.9f));
                            s.color = HdrA(Color.Lerp(Ember, new Color(1f, 0.75f, 0.3f), 0.3f + 0.3f * Mathf.Sin(Clock * 7f + flicker)), 1.8f * hot);
                            s.bright = k * (0.85f + 0.15f * Mathf.Sin(Clock * 11f + flicker));
                        },
                    });
                    float nextFlame = wait;
                    anims.Add(new Ongoing((age, dt) =>
                    {
                        if (age > wait + life - 0.6f) return false;
                        if (age < wait || dt <= 0f) return true;
                        nextFlame -= dt;
                        if (nextFlame > 0f) return true;
                        nextFlame = Random.Range(0.08f, 0.2f);
                        var p = floor + new Vector3(Random.Range(-sq, sq) * 0.45f, 0.1f, Random.Range(-sq, sq) * 0.45f);
                        smoke.Emit(p, Vector3.up * Random.Range(1.2f, 2.4f), Random.Range(0.35f, 0.7f), Random.Range(0.3f, 0.55f), Tint(Ember, Random.value * 0.4f));
                        motes.Emit(p, Vector3.up * Random.Range(0.8f, 2f) + Random.insideUnitSphere * 0.4f, Random.Range(0.05f, 0.1f), Random.Range(0.8f, 1.6f), Tint(Ember, 0.3f));
                        return true;
                    }));
                }
            Flare(at + Vector3.up * 0.8f, Ember, 3f, radius * 1.8f, 2.2f);
        }

        /// <summary>A square outline running out over the floor from <paramref name="at"/>.</summary>
        void UltSquareWave(Vector3 at, float from, float to, float life, Color color, float bright, float delay = 0f)
        {
            Add(new Shape(root, "Ult square wave", meshQuad, matFrame)
            {
                life = life + delay, at = at + Vector3.up * 0.05f, rotation = FlatOnFloor(), scale = Vector3.zero,
                animate = (s, t) =>
                {
                    float a = s.Age - delay;
                    if (a < 0f) { s.bright = 0f; return; }
                    float u = a / life, size = 2f * Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - u, 3f));
                    s.scale = new Vector3(size, size, 1f);
                    s.color = HdrA(color, bright);
                    s.bright = Mathf.Pow(1f - u, 1.3f);
                },
            });
        }

        /// <summary>A word that pops up over something and rises away.</summary>
        void UltPopWord(Vector3 at, string text, Color color, float height, float life)
        {
            var words = new Text3D(root, font, text, color, height);
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age > life) return false;
                var cam = ViewCamera;
                float far = cam != null ? Vector3.Distance(cam.transform.position, at) : 8f;
                words.Place(at + Vector3.up * (0.5f * Smooth01(age / life)), cam, (1f + 0.7f * Mathf.Exp(-age / 0.08f)) * Mathf.Clamp(far / 9f, 1f, 3f), Mathf.Clamp01((life - age) / 0.3f));
                return true;
            }, words.Destroy));
        }

        static readonly Vector2Int[] KnightSquares =
        {
            new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
            new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2),
        };

        // ---------------------------------------------------------------- every frame

        void UltStep(float dt, Camera cam)
        {
            UltTourPreview(cam);
            if (ults.Count == 0)
            {
                UltSky(cam, 0f);
                UltScreenIdle(dt, cam);
                return;
            }
            float night = 0f;
            for (int i = ults.Count - 1; i >= 0; i--)
            {
                var u = ults[i];
                // The caster still at it (any PC: its plan comes from the host); a show that lost it ends by itself.
                if (u.by != null && u.by.PlanSkill == u.skill) u.lastSeen = Clock;
                if (u.endAt < 0f && Clock - u.lastSeen > 1f)
                {
                    u.endAt = Clock;
                    u.cut = u.hitAt < 0f;
                }
                if (u.endAt >= 0f && u.Life(Clock) <= 0f) u.gone = true;
                if (u.gone)
                {
                    ults.RemoveAt(i);
                    continue;
                }
                night = Mathf.Max(night, UltNight(u));
                UltGlitter(u, dt);
            }
            UltSky(cam, night);
            UltScreenStep(dt, cam, night);
        }

        /// <summary>How dark the world is for a show: night falls in 0.35 s at the key, lifts over 1.5 s after the landing.</summary>
        float UltNight(UltShow u)
        {
            float on = Smooth01((Clock - u.t0) / 0.35f);
            float off = u.hitAt >= 0f ? 1f - Smooth01((Clock - u.hitAt - 0.9f) / 1.5f) : 1f;
            return on * off * u.Life(Clock);
        }

        /// <summary>Motes of light drifting in the air round the play while the show lasts (the shorts' glitter).</summary>
        void UltGlitter(UltShow u, float dt)
        {
            if (dt <= 0f || u.by == null) return;
            float k = u.Life(Clock);
            if (k <= 0f || Random.value > 0.5f * k) return;
            Vector3 c = u.hitAt >= 0f ? u.hitPoint : u.by.Hips.position;
            var off = Random.insideUnitSphere * 10f;
            off.y = Mathf.Abs(off.y) * 0.5f + 0.3f;
            motes.Emit(c + off, Random.insideUnitSphere * 0.25f + Vector3.up * 0.15f, Random.Range(0.04f, 0.09f), Random.Range(1.2f, 2.2f), Tint(Random.value < 0.6f ? Color.white : Electric, 0.3f));
        }

        // ---------------------------------------------------------------- the starry sky

        Transform skyBall;
        MeshRenderer skyRenderer;
        MaterialPropertyBlock skyBlock;   // (made when first used: no Unity object may be made in a MonoBehaviour's field initializers)

        /// <summary>While night falls the sky turns to stars, on every screen, behind everything solid.</summary>
        void UltSky(Camera cam, float k)
        {
            if (k <= 0.001f || cam == null)
            {
                if (skyRenderer != null) skyRenderer.enabled = false;
                return;
            }
            if (skyBall == null)
            {
                var go = new GameObject("Ult starry sky");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshSphere;
                skyRenderer = go.AddComponent<MeshRenderer>();
                skyRenderer.sharedMaterial = matSky;
                skyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skyRenderer.receiveShadows = false;
                skyBall = go.transform;
            }
            if (skyBlock == null) skyBlock = new MaterialPropertyBlock();
            float r = Mathf.Min(380f, cam.farClipPlane * 0.8f);
            skyBall.SetPositionAndRotation(cam.transform.position, Quaternion.Euler(0f, Clock * 0.6f, 0f));
            skyBall.localScale = Vector3.one * r * 2f;
            skyBlock.SetColor("_Color", new Color(1.4f, 1.45f, 1.6f, 1f));
            skyBlock.SetFloat("_Opacity", k * 0.97f);
            skyRenderer.SetPropertyBlock(skyBlock);
            skyRenderer.enabled = true;
        }
    }
}
