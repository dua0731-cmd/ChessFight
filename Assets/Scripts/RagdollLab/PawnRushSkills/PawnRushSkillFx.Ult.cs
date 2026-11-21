using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The ultimates' shared cast in the world, on every PC (JY-skill-lab, 2026-11-21; the previz 16-공통시전 and, above all,
    /// the chess edit shorts the user gave: "내가 보여준 영상들이 진짜 멋있었으니까 그것들도 제대로 참고해줘"). What the shorts
    /// do, brought into the third-person game without taking anyone's camera:
    ///
    ///   the board drained grey, only neon squares lit   the world loses its colour, teal and red stay (the grade:
    ///                                                    PawnRushSkillFx.UltScreen.cs); the caster's square bursts teal
    ///   squares lighting on the beat                     each heartbeat lights a ring of squares round the caster, further out
    ///   the move judged: teal !!, red ??                 the targets' squares go red one by one with ?? over them; the landing
    ///                                                    gets a huge !! over it
    ///   flame and smoke round the hero                   a teal aurora (curtains of light) and teal flames on the caster
    ///   glitter in the air                               motes of light drift round it while it lasts
    ///   red light falling from the sky                   a red beam onto every enemy struck
    ///   the board rippling, then breaking into floating  the squares round the landing rise on a wave, break loose and float
    ///   stones in a starry sky                           in a sky turned to stars, then settle as the colour comes back
    ///   the fork's red lines                             red lines from the landing square to every enemy it forks
    ///
    /// Told by the moments (SkillFxKind.Ult*, RagdollPawn.KnightUlts.cs): the key, an enemy named, the call, an enemy struck, the
    /// landing, the end. The knight's fork names its crowd by its landing square and round (PlanPoint, PlanNote), which every
    /// PC has. A show that loses its caster's skill ends by itself.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Color Teal = new Color(0.12f, 1f, 0.8f);
        static readonly Color TealPale = new Color(0.72f, 1f, 0.94f);
        static readonly Color Blood = new Color(1f, 0.1f, 0.14f);
        /// <summary>A square of the board on the course (m).</summary>
        const float UltSquare = 2f;
        const float UltHush = 0.3f;
        /// <summary>The heartbeats of a 2.1 s gathering (s), quickening.</summary>
        static readonly float[] UltBeats = { 0.55f, 1.05f, 1.45f, 1.75f, 1.95f };
        /// <summary>The knight's eight squares (in squares).</summary>
        static readonly Vector2Int[] KnightSquares =
        {
            new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
            new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2),
        };

        sealed class UltShow
        {
            public RagdollPawn by;
            public int skill;
            public float t0, gather = 2.1f, cast = 2.4f;
            public float goAt = -1f, finalAt = -1f, hitAt = -1f, endAt = -1f, lastSeen;
            public bool cut, gone;
            public Vector3 square, landing, hitPoint;
            public bool landingLocked;
            public float hitRound;
            public int hitCount, struck;
            // The enemies named (in the order they were shown), when each turned red, and those still to be shown.
            public readonly List<RagdollPawn> marked = new List<RagdollPawn>();
            public readonly List<float> markedAt = new List<float>();
            public readonly List<(RagdollPawn e, int order)> told = new List<(RagdollPawn, int)>();
            public readonly HashSet<RagdollPawn> struckSet = new HashSet<RagdollPawn>();
            public int beatsShown, typed;
            public float strikeAt = -1f;
            public string title = "", nota = "!!";
            public float Beat(int i) => t0 + UltBeats[i] * gather / 2.1f;
            public float MarkStart => t0 + gather * 0.45f;
            public float Hush => t0 + gather;
            public float Go => goAt >= 0f ? goAt : t0 + cast;
            public bool Fork => skill == PawnRushSkillIds.FamilyFork;
            public bool Tour => skill == PawnRushSkillIds.KnightTour;
            /// <summary>How much it is on now (0 gone .. 1), for parts that last the whole show.</summary>
            public float Life(float clock) => gone ? 0f : endAt < 0f ? 1f : Mathf.Clamp01(1f - (clock - endAt) / (cut ? 0.5f : 1.8f));
        }

        readonly List<UltShow> ults = new List<UltShow>();
        Material matFrame, matLine, matSky, matAurora, matStone, matHole;
        Texture2D texFrame, texLine, texStars;
        bool ultBuilt;

        static Color HdrA(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static float Pop(float age, float rise = 0.06f) => age < 0f ? 0f : age < rise ? age / rise : 1f;

        void BuildUltParts()
        {
            if (ultBuilt) return;
            ultBuilt = true;
            // A square's outline (thin bright edges), a line of light across, a sky of stars.
            texFrame = Tex(128, 128, TextureWrapMode.Clamp, (u, v) =>
            {
                float d = Mathf.Min(Mathf.Min(u, v), Mathf.Min(1f - u, 1f - v));
                return White(Mathf.Clamp01(Mathf.Exp(-d / 0.022f) * 1.2f + Mathf.Exp(-d / 0.09f) * 0.25f));
            });
            texLine = Tex(64, 16, TextureWrapMode.Clamp, (u, v) =>
                White(Mathf.Exp(-Sq((v - 0.5f) / 0.17f)) * Mathf.SmoothStep(0f, 1f, u * 8f) * Mathf.SmoothStep(0f, 1f, (1f - u) * 8f)));
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
                if (b > 0.75f)
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int k = ((y + dy + 512) % 512) * 1024 + (x + dx + 1024) % 1024;
                        byte h = (byte)(c / 3);
                        px[k] = new Color32(h, h, (byte)Mathf.Min(255, h + 10), 255);
                    }
            }
            texStars.SetPixels32(px);
            texStars.Apply(true, true);
            matFrame = Glow(texFrame);
            matLine = Glow(texLine, noise: 0.25f, noiseST: new Vector4(4f, 1f, -3f, 0f));
            matSky = Glow(texStars, opacity: 1f);
            matSky.renderQueue = 2900;   // after the sky, before every effect: stars only where nothing solid is in front
            matAurora = Glow(texBeam, noise: 0.75f, noiseST: new Vector4(3f, 1.1f, 0.15f, -0.9f), rim: 0.9f, rimPower: 1.6f, soft: 0.1f);
            matHole = Glow(Texture2D.whiteTexture, opacity: 1f);
            // The floating stones: a dark stone lit by the scene, its edges lit teal from within (the outline as emission).
            matStone = ChessFight.Game.LitMaterials.NewLit(new Color(0.32f, 0.33f, 0.35f), 0.25f);
            matStone.hideFlags = HideFlags.HideAndDontSave;
            ChessFight.Game.LitMaterials.SetEmission(matStone, HdrA(Teal, 2.2f));
            if (matStone.HasProperty("_EmissionMap")) matStone.SetTexture("_EmissionMap", texFrame);
            materials.Add(matStone);
        }

        void DestroyUlt()
        {
            UltScreenDestroy();
            foreach (var t in new Object[] { texFrame, texLine, texStars }) if (t != null) Destroy(t);
        }

        UltShow ShowOf(RagdollPawn by)
        {
            for (int i = ults.Count - 1; i >= 0; i--) if (ults[i].by == by && !ults[i].gone) return ults[i];
            return null;
        }

        // ---------------------------------------------------------------- the moments

        void UltCast(SkillFxEvent e)
        {
            if (e.by == null) return;
            var old = ShowOf(e.by);
            if (old != null) old.gone = true;
            BuildUltParts();
            var u = new UltShow { by = e.by, skill = e.count, t0 = Clock, cast = e.size > 0.5f ? e.size : 2.4f, lastSeen = Clock };
            u.gather = Mathf.Max(0.5f, u.cast - UltHush);
            u.square = FloorUnder(e.by);
            u.landing = u.square;
            ults.Add(u);
            UltCastParts(u);
            UltScreenStart(u);
        }

        void UltMark(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null || e.target == null) return;
            u.told.Add((e.target, Mathf.Max(1, e.count)));
        }

        void UltGo(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null) return;
            bool final = e.size > 0.5f;
            if (final)
            {
                u.finalAt = Clock;
                UltLandingMark(u, e.at, 1.6f);
                anims.Add(new Streak(root, matTrail, HdrA(Teal, 2.6f), 0.55f, 0.45f, () => u.by != null && u.hitAt < 0f && !u.gone ? u.by.Hips.position : (Vector3?)null));
                UltSound(UltSfx.Leap, e.at);
                return;
            }
            u.goAt = Clock;
            u.landing = e.at;
            u.landingLocked = true;
            UltGoParts(u, e);
            UltScreenGo(u);
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
            u.hitRound = e.size;
            u.hitCount = e.count;
            UltImpact(u, e);
            UltScreenHit(u, e);
        }

        void UltEnd(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null || u.endAt >= 0f) return;
            u.endAt = Clock;
            u.cut = e.count == 1 && u.hitAt < 0f;
            if (u.cut) UltScreenCut(u);
        }

        // ---------------------------------------------------------------- the key: the caster's square, aurora, light

        void UltCastParts(UltShow u)
        {
            var flat = Quaternion.Euler(90f, 0f, 0f);
            Vector3 sq = u.square + Vector3.up * 0.03f;
            // The caster's square: bursts at the key, beats with the heart, holds its breath, flares at the call.
            Add(new Shape(root, "Ult square", meshQuad, matTile)
            {
                life = 60f, at = sq, rotation = flat, scale = new Vector3(UltSquare * 1.02f, UltSquare * 1.02f, 1f), paint = 0.35f,
                animate = (s, t) =>
                {
                    float k = u.Life(Clock);
                    if (k <= 0f || Clock > u.Go + 0.8f) { s.life = 0f; return; }
                    float hush = Clock >= u.Hush && Clock < u.Go ? 0.45f : 1f, after = Clock > u.Go ? 1f - (Clock - u.Go) / 0.8f : 1f;
                    s.color = HdrA(Teal, 1.1f);
                    s.bright = k * hush * after * (0.8f + 0.1f * Mathf.Sin(Clock * 10f) + UltPulse(u));
                },
            });
            Add(new Shape(root, "Ult square edge", meshQuad, matFrame)
            {
                life = 60f, at = sq + Vector3.up * 0.01f, rotation = flat, scale = new Vector3(UltSquare * 1.06f, UltSquare * 1.06f, 1f),
                animate = (s, t) =>
                {
                    float k = u.Life(Clock);
                    if (k <= 0f || Clock > u.Go + 0.8f) { s.life = 0f; return; }
                    float hush = Clock >= u.Hush && Clock < u.Go ? 0.5f : 1f, after = Clock > u.Go ? 1f - (Clock - u.Go) / 0.8f : 1f;
                    s.color = HdrA(TealPale, 2.4f);
                    s.bright = k * hush * after * (1f + UltPulse(u));
                },
            });
            // A low curtain of light standing on the square's edges.
            Add(new Shape(root, "Ult curtain", meshBox, matCurtain)
            {
                life = 60f, at = u.square, scale = new Vector3(UltSquare, 1f, UltSquare),
                animate = (s, t) =>
                {
                    float k = u.Life(Clock);
                    if (k <= 0f || Clock > u.Go + 0.35f) { s.life = 0f; return; }
                    float a = Clock - u.t0, h = Mathf.Lerp(3f, 1.3f, Smooth01((a - 0.05f) / 0.55f)) + 0.6f * UltBeatPulse(u);
                    float b = 0.55f + 0.35f * UltPulse(u);
                    if (Clock >= u.Hush && Clock < u.Go) { float q = Smooth01((Clock - u.Hush) / 0.2f); h = Mathf.Lerp(h, 0.3f, q); b *= 1f - 0.3f * q; }
                    if (Clock >= u.Go) { float g = (Clock - u.Go) / 0.35f; h = Mathf.Lerp(0.3f, 6f, Smooth01(g * 2f)); b = 1.3f * (1f - g); }
                    s.scale = new Vector3(UltSquare, h, UltSquare);
                    s.color = HdrA(Teal, 1.2f);
                    s.bright = k * b;
                },
            });
            // The aurora: two curtains of light turning round the caster, bright at their edges, clear in front of it.
            for (int i = 0; i < 2; i++)
            {
                float r = i == 0 ? 0.75f : 1.05f, h = i == 0 ? 2.6f : 3.3f, spin = i == 0 ? 70f : -45f;
                Color c = i == 0 ? Teal : TealPale;
                Add(new Shape(root, "Ult aurora", meshCylinder, matAurora)
                {
                    life = 60f,
                    animate = (s, t) =>
                    {
                        float k = u.Life(Clock);
                        if (k <= 0f || u.by == null || Clock > u.Go + 0.5f) { s.life = 0f; return; }
                        float a = Clock - u.t0, grow = Smooth01(a / 0.35f), sc = 1f, b = 0.9f + 0.5f * UltPulse(u);
                        if (Clock >= u.Hush && Clock < u.Go) { sc = Mathf.Lerp(1f, 0.72f, Smooth01((Clock - u.Hush) / 0.2f)); b *= 0.6f; }
                        if (Clock >= u.Go) { float g = (Clock - u.Go) / 0.5f; sc = Mathf.Lerp(0.72f, 1.8f, Smooth01(g * 1.6f)); b *= 1f - g; }
                        Vector3 hips = u.by.Hips.position;
                        s.at = new Vector3(hips.x, u.square.y + 0.02f, hips.z);
                        s.rotation = Quaternion.Euler(0f, a * spin, 0f);
                        s.scale = new Vector3(r * sc, h * grow * sc, r * sc);
                        s.color = HdrA(c, 1.7f);
                        s.bright = k * b;
                    },
                });
            }
            // A teal light on the caster, beating; it lights the piece and the floor round it.
            var light = new GameObject("Ult light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.color = Teal;
            light.range = 8f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            anims.Add(new Ongoing((age, dt) =>
            {
                float k = u.Life(Clock);
                if (k <= 0f || u.by == null || Clock > u.Go + 0.4f) return false;
                light.transform.position = ChestOf(u.by) + Vector3.up * 0.9f;
                float after = Clock > u.Go ? 1f - (Clock - u.Go) / 0.4f : 1f;
                light.intensity = k * after * (1.6f + 1.4f * UltPulse(u));
                // Teal flames rising off the caster and motes drifting up from its square.
                if (Clock < u.Go && dt > 0f)
                {
                    Vector3 feet = new Vector3(u.by.Hips.position.x, u.square.y, u.by.Hips.position.z);
                    if (Random.value < 0.6f)
                    {
                        var off = Random.insideUnitCircle * 0.55f;
                        smoke.Emit(feet + new Vector3(off.x, 0.2f, off.y), Vector3.up * Random.Range(1.4f, 2.4f), Random.Range(0.5f, 0.9f), Random.Range(0.6f, 0.9f), Tint(Teal, Random.value * 0.3f));
                    }
                    var m = Random.insideUnitCircle * 1.1f;
                    motes.Emit(feet + new Vector3(m.x, 0.1f, m.y), new Vector3(0f, Random.Range(1f, 2.6f), 0f), Random.Range(0.06f, 0.12f), Random.Range(0.7f, 1.3f), Tint(Random.value < 0.5f ? Teal : TealPale, 0.2f));
                }
                return true;
            }, () => { if (light != null) Object.Destroy(light.gameObject); }));
            // The key's beat: the square bursts, a wave of squares runs out over the floor and the board shows for a moment.
            UltSquareWave(u.square, 1.1f, 18f, 0.9f, Teal, 2.2f);
            UltSquareWave(u.square, 1.1f, 13f, 1f, TealPale, 1.4f, 0.1f);
            UltBoardRing(u, 1, 0f);
            UltBoardRing(u, 2, 0.07f);
            UltBoardRing(u, 3, 0.14f);
            Flare(u.square + Vector3.up * 1.2f, TealPale, 2.5f, 7f, 0.5f);
            SparkBurst(u.square + Vector3.up * 0.2f, 26, Teal, Vector3.up, 35f, 3f, 7f, 0.7f, 0.05f);
            // The fork's landing square and its eight squares, shown as the aim from the third heartbeat.
            if (u.Fork) UltAimMark(u);
        }

        /// <summary>The heart's beat now (1 at a beat, falling off), for what beats with it.</summary>
        float UltBeatPulse(UltShow u)
        {
            float k = 0f;
            for (int i = 0; i < UltBeats.Length; i++)
            {
                float a = Clock - u.Beat(i);
                if (a >= 0f) k += Mathf.Exp(-a / 0.14f);
            }
            return Mathf.Min(1.2f, k);
        }

        /// <summary>The key's burst, then the heartbeats (the caster's square and what stands on it).</summary>
        float UltPulse(UltShow u)
        {
            float a = Clock - u.t0;
            return 1.3f * Mathf.Exp(-Mathf.Max(0f, a) / 0.16f) + 0.8f * UltBeatPulse(u) + (Clock >= u.Go ? 1.6f * Mathf.Exp(-(Clock - u.Go) / 0.2f) : 0f);
        }

        /// <summary>A square outline running out over the floor from <paramref name="at"/>.</summary>
        void UltSquareWave(Vector3 at, float from, float to, float life, Color color, float bright, float delay = 0f)
        {
            var flat = Quaternion.Euler(90f, 0f, 0f);
            Add(new Shape(root, "Ult square wave", meshQuad, matFrame)
            {
                life = life + delay, at = at + Vector3.up * 0.05f, rotation = flat, scale = Vector3.zero,
                animate = (s, t) =>
                {
                    float a = s.Age - delay;
                    if (a < 0f) { s.bright = 0f; return; }
                    float u = a / life, size = Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - u, 3f));
                    s.scale = new Vector3(size, size, 1f);
                    s.color = HdrA(color, bright);
                    s.bright = Mathf.Pow(1f - u, 1.3f);
                },
            });
        }

        /// <summary>The squares of the board in ring <paramref name="ring"/> round the caster's square light for a moment (a
        /// heartbeat lights one ring further out each time; the key lights the first three).</summary>
        void UltBoardRing(UltShow u, int ring, float delay)
        {
            var flat = Quaternion.Euler(90f, 0f, 0f);
            for (int x = -ring; x <= ring; x++)
                for (int z = -ring; z <= ring; z++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
                    Vector3 at = u.square + new Vector3(x * UltSquare, 0.04f, z * UltSquare);
                    float far = new Vector2(x, z).magnitude, wait = delay + far * 0.015f;
                    Add(new Shape(root, "Ult board square", meshQuad, matFrame)
                    {
                        life = 0.9f + wait, at = at, rotation = flat, scale = new Vector3(UltSquare * 0.96f, UltSquare * 0.96f, 1f),
                        animate = (s, t) =>
                        {
                            float a = s.Age - wait;
                            s.color = HdrA(Teal, 1.9f);
                            s.bright = a < 0f ? 0f : Pop(a, 0.04f) * Mathf.Pow(1f - Mathf.Clamp01(a / 0.9f), 1.6f) / (1f + 0.25f * ring);
                        },
                    });
                }
        }

        // ---------------------------------------------------------------- the aim and the marks

        /// <summary>The knight's fork's aim: the landing square and the eight it forks from there, following the aim until the
        /// call, then held where the leap goes.</summary>
        void UltAimMark(UltShow u)
        {
            var flat = Quaternion.Euler(90f, 0f, 0f);
            Vector3 Where() => u.landingLocked ? u.landing : (u.by != null && u.by.PlanPoint != Vector3.zero ? u.by.PlanPoint : u.square);
            float On() => Clock < u.MarkStart || u.Life(Clock) <= 0f ? 0f : Pop(Clock - u.MarkStart, 0.1f) * u.Life(Clock) * (u.hitAt >= 0f ? Mathf.Clamp01(1f - (Clock - u.hitAt) / 0.5f) : 1f);
            Add(new Shape(root, "Ult landing", meshQuad, matTile)
            {
                life = 60f, rotation = flat, scale = new Vector3(UltSquare, UltSquare, 1f), paint = 0.4f,
                animate = (s, t) =>
                {
                    if (u.gone || (u.hitAt >= 0f && Clock > u.hitAt + 0.5f) || (u.endAt >= 0f && u.Life(Clock) <= 0f)) { s.life = 0f; return; }
                    s.at = Where() + Vector3.up * 0.05f;
                    s.color = HdrA(Teal, 1.4f);
                    s.bright = On() * (0.9f + 0.3f * Mathf.Sin(Clock * 12f) + 0.6f * UltBeatPulse(u));
                },
            });
            Add(new Shape(root, "Ult landing edge", meshQuad, matFrame)
            {
                life = 60f, rotation = flat, scale = new Vector3(UltSquare * 1.08f, UltSquare * 1.08f, 1f),
                animate = (s, t) =>
                {
                    if (u.gone || (u.hitAt >= 0f && Clock > u.hitAt + 0.5f) || (u.endAt >= 0f && u.Life(Clock) <= 0f)) { s.life = 0f; return; }
                    s.at = Where() + Vector3.up * 0.06f;
                    s.color = HdrA(TealPale, 2.6f);
                    s.bright = On();
                },
            });
            for (int i = 0; i < KnightSquares.Length; i++)
            {
                var k8 = KnightSquares[i];
                float wait = (i >> 1) * 0.08f;
                Add(new Shape(root, "Ult knight square", meshQuad, matFrame)
                {
                    life = 60f, rotation = flat, scale = new Vector3(UltSquare * 0.92f, UltSquare * 0.92f, 1f),
                    animate = (s, t) =>
                    {
                        if (u.gone || (u.hitAt >= 0f && Clock > u.hitAt + 0.5f) || (u.endAt >= 0f && u.Life(Clock) <= 0f)) { s.life = 0f; return; }
                        s.at = Where() + new Vector3(k8.x * UltSquare, 0.05f, k8.y * UltSquare);
                        float on = Clock < u.MarkStart + wait ? 0f : On();
                        s.color = HdrA(Teal, 1.5f);
                        s.bright = on * (0.6f + 0.4f * UltBeatPulse(u));
                    },
                });
            }
        }

        /// <summary>The landing square for a leap that is under way (the tour's last one).</summary>
        void UltLandingMark(UltShow u, Vector3 at, float seconds)
        {
            var flat = Quaternion.Euler(90f, 0f, 0f);
            Add(new Shape(root, "Ult final square", meshQuad, matTile)
            {
                life = seconds, at = at + Vector3.up * 0.05f, rotation = flat, scale = new Vector3(UltSquare, UltSquare, 1f), paint = 0.4f,
                animate = (s, t) => { s.color = HdrA(Teal, 1.5f); s.bright = Pop(s.Age, 0.05f) * (u.hitAt >= 0f ? Mathf.Clamp01(1f - (Clock - u.hitAt) / 0.4f) : 1f); },
            });
            Add(new Shape(root, "Ult final edge", meshQuad, matFrame)
            {
                life = seconds, at = at + Vector3.up * 0.06f, rotation = flat, scale = new Vector3(UltSquare * 1.08f, UltSquare * 1.08f, 1f),
                animate = (s, t) => { s.color = HdrA(TealPale, 2.6f); s.bright = Pop(s.Age, 0.05f) * (u.hitAt >= 0f ? Mathf.Clamp01(1f - (Clock - u.hitAt) / 0.4f) : 1f); },
            });
        }

        /// <summary>An enemy named: its square goes red under it and ?? rises over it, with a red flash and a tick.</summary>
        void UltRedMark(UltShow u, RagdollPawn e, int order)
        {
            u.marked.Add(e);
            u.markedAt.Add(Clock);
            var flat = Quaternion.Euler(90f, 0f, 0f);
            float Fade() => u.gone ? 0f : u.Life(Clock) * (u.hitAt >= 0f ? Mathf.Clamp01(1f - (Clock - u.hitAt - 0.9f) / 0.6f) : 1f)
                                         * (u.Fork && !u.landingLocked && !UltInRound(u, e) ? 0f : 1f);
            Vector3 floorAt = Ground(e.Hips.position - Vector3.up * e.standHeight);
            Add(new Shape(root, "Ult red square", meshQuad, matTile)
            {
                life = 60f, rotation = flat, scale = new Vector3(UltSquare, UltSquare, 1f), paint = 0.45f,
                animate = (s, t) =>
                {
                    float k = Fade();
                    if (k <= 0f && (u.gone || u.hitAt >= 0f || u.endAt >= 0f)) { s.life = 0f; return; }
                    if (e == null) { s.life = 0f; return; }
                    Vector3 hips = e.Hips.position;
                    floorAt = new Vector3(hips.x, Mathf.Lerp(floorAt.y, Ground(hips - Vector3.up * e.standHeight).y, 0.2f), hips.z);
                    s.at = floorAt + Vector3.up * 0.06f;
                    s.color = HdrA(Blood, 1.5f);
                    s.bright = k * (0.85f + 0.25f * Mathf.Sin(Clock * 14f) + 1.2f * Mathf.Exp(-s.Age / 0.12f) + (u.struckSet.Contains(e) ? 1f : 0f));
                },
            });
            Add(new Shape(root, "Ult red edge", meshQuad, matFrame)
            {
                life = 60f, rotation = flat, scale = new Vector3(UltSquare * 1.08f, UltSquare * 1.08f, 1f),
                animate = (s, t) =>
                {
                    float k = Fade();
                    if (k <= 0f && (u.gone || u.hitAt >= 0f || u.endAt >= 0f)) { s.life = 0f; return; }
                    if (e == null) { s.life = 0f; return; }
                    s.at = floorAt + Vector3.up * 0.07f;
                    s.color = HdrA(Blood, 2.6f);
                    s.bright = k;
                },
            });
            var words = new Text3D(root, font, "??", new Color(1f, 0.25f, 0.27f), 0.55f);
            anims.Add(new Ongoing((age, dt) =>
            {
                float k = Fade();
                if (e == null || (k <= 0f && (u.gone || u.hitAt >= 0f || u.endAt >= 0f))) return false;
                var cam = ViewCamera;
                float pop = 1f + 0.8f * Mathf.Exp(-age / 0.1f), far = cam != null ? Vector3.Distance(cam.transform.position, e.Hips.position) : 8f;
                words.Place(HeadOf(e) + Vector3.up * (0.75f + 0.04f * Mathf.Sin(age * 6f)), cam, pop * Mathf.Clamp(far / 9f, 1f, 3f), k);
                return true;
            }, words.Destroy));
            Flare(floorAt + Vector3.up * 0.6f, Blood, 1.6f, 4f, 0.3f);
            UltSound(UltSfx.Tick, floorAt, order);
            UltScreenMarked(u, e);
        }

        bool UltInRound(UltShow u, RagdollPawn e)
        {
            if (u.by == null || e == null) return false;
            float r = u.by.PlanNote > 0 ? u.by.PlanNote : 14f;
            Vector3 c = u.landingLocked ? u.landing : u.by.PlanPoint;
            return Flat(e.Hips.position - c).magnitude <= r;
        }

        // ---------------------------------------------------------------- the call

        void UltGoParts(UltShow u, SkillFxEvent e)
        {
            // A thin beam of teal light from the caster's square into the sky, a flash, sparks, a wide wave of squares.
            Add(new Shape(root, "Ult call beam", meshCylinder, matPillar)
            {
                life = 0.9f, at = u.square,
                animate = (s, t) =>
                {
                    float h = Mathf.Lerp(2f, 45f, 1f - Mathf.Pow(1f - Mathf.Clamp01(t * 4f), 3f)), w = Mathf.Lerp(0.55f, 0.25f, t);
                    s.scale = new Vector3(w, h, w);
                    s.color = HdrA(TealPale, 3.2f);
                    s.bright = Mathf.Pow(1f - t, 1.5f);
                },
            });
            Flare(u.square + Vector3.up * 1.5f, TealPale, 4f, 12f, 0.5f);
            SparkBurst(u.square + Vector3.up * 0.3f, 40, TealPale, Vector3.up, 25f, 6f, 13f, 0.8f, 0.06f);
            UltSquareWave(u.square, 1.5f, 30f, 0.9f, Teal, 2.6f);
            UltBoardRing(u, 4, 0f);
            UltBoardRing(u, 5, 0.06f);
            UltSound(UltSfx.Leap, u.square);
            if (u.Fork)
            {
                // The knight's own: a teal trail through the air, and red fork lines from the landing square to every enemy named.
                anims.Add(new Streak(root, matTrail, HdrA(Teal, 2.6f), 0.6f, 0.5f, () => u.by != null && u.hitAt < 0f && !u.gone ? u.by.Hips.position : (Vector3?)null));
                foreach (var m in u.marked) UltForkLine(u, m);
            }
            else if (u.Tour)
            {
                anims.Add(new Streak(root, matTrail, HdrA(Teal, 2.2f), 0.45f, 0.4f, () => u.by != null && u.hitAt < 0f && !u.gone ? u.by.Hips.position : (Vector3?)null));
                UltTourPath(u);
            }
        }

        /// <summary>A red line of light on the floor from the fork's landing to an enemy it forks (the shorts' fork lines).</summary>
        void UltForkLine(UltShow u, RagdollPawn e)
        {
            Add(new Shape(root, "Ult fork line", meshQuad, matLine)
            {
                life = 60f,
                animate = (s, t) =>
                {
                    if (u.gone || e == null || (u.hitAt >= 0f && Clock > u.hitAt + 0.7f) || (u.endAt >= 0f && u.Life(Clock) <= 0f)) { s.life = 0f; return; }
                    Vector3 a = u.landing + Vector3.up * 0.08f, b = new Vector3(e.Hips.position.x, a.y, e.Hips.position.z);
                    Vector3 d = b - a;
                    float len = Mathf.Max(0.1f, d.magnitude), grow = Smooth01(s.Age / 0.2f);
                    Vector3 dir = d / len;
                    s.at = a + dir * (len * grow * 0.5f);
                    s.rotation = Quaternion.LookRotation(Vector3.down, Vector3.Cross(Vector3.down, dir));
                    s.scale = new Vector3(len * grow, 0.32f, 1f);
                    s.color = HdrA(Blood, 3f);
                    s.bright = (u.hitAt >= 0f ? Mathf.Clamp01(1f - (Clock - u.hitAt) / 0.7f) : 1f) * (0.85f + 0.15f * Mathf.Sin(Clock * 30f));
                },
            });
        }

        /// <summary>The tour's way, from head to head in knight's moves (each step an L on the floor), lit teal.</summary>
        void UltTourPath(UltShow u)
        {
            for (int i = 0; i < 7; i++)
            {
                int leg = i;
                for (int half = 0; half < 2; half++)
                {
                    int part = half;
                    Add(new Shape(root, "Ult tour path", meshQuad, matLine)
                    {
                        life = 60f,
                        animate = (s, t) =>
                        {
                            if (u.gone || (u.hitAt >= 0f && Clock > u.hitAt + 0.4f) || (u.endAt >= 0f && u.Life(Clock) <= 0f)) { s.life = 0f; return; }
                            if (leg >= u.marked.Count || u.by == null) { s.bright = 0f; return; }
                            var to = u.marked[leg];
                            var from = leg == 0 ? null : u.marked[leg - 1];
                            if (to == null) { s.bright = 0f; return; }
                            Vector3 a = from != null ? from.Hips.position : u.square, b = to.Hips.position;
                            a.y = b.y = u.square.y + 0.09f;
                            Vector3 corner = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z) ? new Vector3(b.x, a.y, a.z) : new Vector3(a.x, a.y, b.z);
                            Vector3 p = part == 0 ? a : corner, q = part == 0 ? corner : b, d = q - p;
                            float len = d.magnitude;
                            if (len < 0.05f) { s.bright = 0f; return; }
                            Vector3 dir = d / len;
                            s.at = (p + q) * 0.5f;
                            s.rotation = Quaternion.LookRotation(Vector3.down, Vector3.Cross(Vector3.down, dir));
                            s.scale = new Vector3(len, 0.22f, 1f);
                            s.color = HdrA(Teal, 2.4f);
                            float on = Smooth01((s.Age - leg * 0.06f) / 0.15f), done = u.struckSet.Contains(to) ? 0.35f : 1f;
                            s.bright = on * done * (u.hitAt >= 0f ? Mathf.Clamp01(1f - (Clock - u.hitAt) / 0.4f) : 1f);
                        },
                    });
                }
            }
        }

        // ---------------------------------------------------------------- an enemy struck

        static readonly string[] TourSquares = { "Nf5", "Nd6", "Ne8", "Nc7", "Nb5", "Nd4", "Nf3", "Ne5" };

        /// <summary>The tour's stomp: a burst of white light on the head, the red square flashing, the move written in the air.</summary>
        void UltStomp(UltShow u, SkillFxEvent e)
        {
            Vector3 head = HeadOf(e.target);
            Flare(head + Vector3.up * 0.3f, TealPale, 3.5f, 6f, 0.3f);
            SparkBurst(head + Vector3.up * 0.2f, 30, TealPale, Vector3.up, 70f, 3f, 8f, 0.6f, 0.05f);
            MoteBurst(head, 14, Teal, 0.5f, 1.2f, 2f, 0.9f);
            UltSquareWave(Ground(e.target.Hips.position - Vector3.up * e.target.standHeight), 0.5f, 5f, 0.5f, TealPale, 2.2f);
            string note = TourSquares[(Mathf.Max(1, e.count) - 1) % TourSquares.Length] + (e.count >= 3 ? "!" : "");
            var words = new Text3D(root, font, note, TealPale, 0.6f);
            Vector3 from = head + Vector3.up * 1.1f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age > 1.3f) return false;
                var cam = ViewCamera;
                float far = cam != null ? Vector3.Distance(cam.transform.position, from) : 8f;
                words.Place(from + Vector3.up * (0.6f * Smooth01(age / 0.5f)), cam, (1f + 0.7f * Mathf.Exp(-age / 0.08f)) * Mathf.Clamp(far / 9f, 1f, 3f), Mathf.Clamp01((1.3f - age) / 0.4f));
                return true;
            }, words.Destroy));
            UltSound(UltSfx.Stomp, head, e.count);
            UltScreenStrike(u, e);
        }

        /// <summary>The fork's red light falling from the sky onto an enemy it throws.</summary>
        void UltRedBeam(RagdollPawn target)
        {
            Vector3 at = target.Hips.position;
            Add(new Shape(root, "Ult red beam", meshCylinder, matPillar)
            {
                life = 0.85f,
                animate = (s, t) =>
                {
                    float fall = Smooth01(s.Age / 0.1f), w = Mathf.Lerp(0.55f, 0.95f, t);
                    s.at = new Vector3(at.x, Mathf.Lerp(60f, at.y - 1f, fall), at.z);
                    s.scale = new Vector3(w, 70f, w);
                    s.color = HdrA(Blood, 2.8f);
                    s.bright = 1f - Mathf.Clamp01((t - 0.3f) / 0.7f);
                },
            });
            Flare(at, Blood, 2.4f, 6f, 0.4f);
            SparkBurst(at, 22, Blood, Vector3.up, 60f, 3f, 9f, 0.6f, 0.05f);
        }

        // ---------------------------------------------------------------- the landing

        void UltImpact(UltShow u, SkillFxEvent e)
        {
            Vector3 at = e.at;
            // White light, the squares' waves, the knight's eight squares rising in light, dust, sparks.
            Flare(at + Vector3.up * 1.5f, Color.white, 5f, 22f, 0.45f);
            UltSquareWave(at, 1.5f, 28f, 1f, Teal, 3f);
            UltSquareWave(at, 1.5f, 36f, 1.25f, TealPale, 1.6f, 0.06f);
            UltSquareWave(at, 1.5f, 20f, 0.8f, Color.white, 1.4f, 0.12f);
            for (int i = 0; i < KnightSquares.Length; i++)
            {
                var k8 = KnightSquares[i];
                Vector3 p = at + new Vector3(k8.x * UltSquare, 0f, k8.y * UltSquare);
                float wait = i * 0.025f;
                Add(new Shape(root, "Ult knight pillar", meshCylinder, matPillar)
                {
                    life = 1.2f + wait, at = p,
                    animate = (s, t) =>
                    {
                        float a = s.Age - wait;
                        if (a < 0f) { s.bright = 0f; return; }
                        float h = 12f * (1f - Mathf.Pow(1f - Mathf.Clamp01(a / 0.15f), 3f)), w = 0.45f * (1f + a * 0.4f);
                        s.scale = new Vector3(w, h, w);
                        s.color = HdrA(Teal, 2.2f);
                        s.bright = 1f - Mathf.Clamp01((a - 0.35f) / 0.85f);
                    },
                });
            }
            DustRing(at, 18, 1.6f, 7f, new Color(0.55f, 0.55f, 0.57f), 1f, 1.1f);
            SparkBurst(at + Vector3.up * 0.4f, 60, TealPale, Vector3.up, 55f, 6f, 15f, 0.9f, 0.07f);
            // The judgement over the landing: a huge !!.
            var words = new Text3D(root, font, "!!", TealPale, 2.2f);
            Vector3 over = at + Vector3.up * 6f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age > 1.9f) return false;
                words.Place(over + Vector3.up * (0.4f * Smooth01(age / 1.9f)), ViewCamera, 1f + 0.9f * Mathf.Exp(-age / 0.09f), Mathf.Clamp01((1.9f - age) / 0.5f));
                return true;
            }, words.Destroy));
            // The board breaks: the squares round the landing rise on a wave, break loose and float in the starry sky.
            UltShatter(u, at, u.Fork ? 9.5f : 5f);
            UltSound(UltSfx.Impact, at, e.count);
            // Shake by how near this screen's view is; the caster's own the most. The pieces struck and the caster hold still
            // a moment (online on this screen only: HitStop).
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, at) / 35f) : 0.5f;
            bool mine = Viewer != null && Viewer == u.by;
            Shake(mine ? 0.32f : 0.22f * near, 0.75f);
            stopBy = u.by;
            stopTarget = null;
            HitStop(0.12f);
            foreach (var m in u.marked) if (m != null && u.struckSet.Contains(m)) m.HoldVisual(0.12f);
        }

        /// <summary>The squares round <paramref name="at"/> (within <paramref name="radius"/>): a wave lifts them, they break
        /// loose and float up slowly turning, dark holes where they were, and later they settle back and are gone.</summary>
        void UltShatter(UltShow u, Vector3 at, float radius)
        {
            var mesh = meshCube;
            if (mesh == null) return;
            int n = Mathf.CeilToInt(radius / UltSquare);
            var flat = Quaternion.Euler(90f, 0f, 0f);
            for (int x = -n; x <= n; x++)
                for (int z = -n; z <= n; z++)
                {
                    if (x == 0 && z == 0) continue;
                    Vector3 c = at + new Vector3(x * UltSquare, 0f, z * UltSquare);
                    float far = Flat(c - at).magnitude;
                    if (far > radius || Random.value < 0.15f + far / radius * 0.35f) continue;
                    var floor = Ground(c);
                    if (Mathf.Abs(floor.y - at.y) > 1.5f) continue;
                    float wave = far / 18f, rise = Random.Range(1.2f, 4.8f) * (1f - far / (radius * 1.6f)), spin = Random.Range(-40f, 40f);
                    var axis = Random.onUnitSphere;
                    float stay = Random.Range(1.6f, 2.4f);
                    var go = new GameObject("Ult floating square");
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = matStone;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    var tf = go.transform;
                    Vector3 home = floor + Vector3.up * 0.12f;
                    tf.position = home;
                    tf.localScale = new Vector3(UltSquare * 0.94f, 0.24f, UltSquare * 0.94f);
                    anims.Add(new Ongoing((age, dt) =>
                    {
                        float a = age - wave;
                        if (a < 0f) { tf.localScale = Vector3.zero; return true; }
                        float lift, tilt, k = 1f;
                        if (a < 0.25f) { lift = Mathf.Sin(a / 0.25f * Mathf.PI) * 0.5f; tilt = 0f; }
                        else if (a < 0.25f + stay)
                        {
                            float f = (a - 0.25f) / stay;
                            lift = rise * Smooth01(f * 1.6f) + 0.15f * Mathf.Sin(a * 2.4f + far);
                            tilt = spin * Smooth01(f * 1.6f) + 6f * Mathf.Sin(a * 1.7f);
                        }
                        else
                        {
                            float f = (a - 0.25f - stay) / 0.9f;
                            if (f >= 1f) return false;
                            lift = rise * (1f - Smooth01(f)) ;
                            tilt = spin * (1f - Smooth01(f));
                            k = 1f - Smooth01((f - 0.5f) * 2f);
                        }
                        tf.SetPositionAndRotation(home + Vector3.up * lift, Quaternion.AngleAxis(tilt, axis));
                        tf.localScale = new Vector3(UltSquare * 0.94f * k, 0.24f * k, UltSquare * 0.94f * k);
                        return true;
                    }, () => { if (go != null) Object.Destroy(go); }));
                    // The hole it leaves: a dark square on the floor until it settles back.
                    Add(new Shape(root, "Ult hole", meshQuad, matHole)
                    {
                        life = wave + 0.25f + stay + 0.9f, at = floor + Vector3.up * 0.02f, rotation = flat, scale = new Vector3(UltSquare * 0.94f, UltSquare * 0.94f, 1f),
                        animate = (s, t) =>
                        {
                            float a = s.Age - wave - 0.05f;
                            s.color = new Color(0.01f, 0.02f, 0.03f, 1f);
                            s.paint = a < 0f ? 0f : 0.9f * Mathf.Clamp01(a / 0.1f) * (1f - Mathf.Clamp01((s.Age - wave - 0.25f - stay - 0.4f) / 0.5f));
                            s.bright = 1f;
                        },
                    });
                }
        }

        // ---------------------------------------------------------------- every frame

        Mesh meshCube;

        void UltStep(float dt, Camera cam)
        {
            if (ults.Count == 0)
            {
                UltScreenIdle(dt, cam);
                return;
            }
            if (meshCube == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                meshCube = Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
                meshCube.hideFlags = HideFlags.HideAndDontSave;
                Destroy(tmp);
            }
            for (int i = ults.Count - 1; i >= 0; i--)
            {
                var u = ults[i];
                // The caster still at it (on any PC: its plan comes from the host); a show that lost it ends by itself.
                if (u.by != null && u.by.PlanSkill == u.skill) u.lastSeen = Clock;
                if (u.endAt < 0f && Clock - u.lastSeen > 1f)
                {
                    u.endAt = Clock;
                    u.cut = u.hitAt < 0f;
                    if (u.cut) UltScreenCut(u);
                }
                if (u.endAt >= 0f && u.Life(Clock) <= 0f) u.gone = true;
                if (u.gone)
                {
                    ults.RemoveAt(i);
                    continue;
                }
                UltHeartbeats(u);
                UltNameTargets(u);
                UltGlitter(u, dt);
            }
            UltSky(cam);
            UltScreenStep(dt, cam);
        }

        /// <summary>The heartbeats: a ring of squares further out each time, and the beat on the screen and in the ears.</summary>
        void UltHeartbeats(UltShow u)
        {
            while (u.beatsShown < UltBeats.Length && Clock >= u.Beat(u.beatsShown) && u.endAt < 0f)
            {
                int i = u.beatsShown++;
                UltBoardRing(u, Mathf.Min(5, i + 2), 0f);
                UltSquareWave(u.square, 1.1f, 4.5f + i, 0.45f, Teal, 1.6f);
                UltSound(UltSfx.Heart, u.square, i);
                UltScreenBeat(u, i);
            }
        }

        /// <summary>The enemies it will hit go red one at a time: the fork's, everyone within its round of the landing square
        /// (re-counted as the aim moves); the tour's, the line as the host named it.</summary>
        void UltNameTargets(UltShow u)
        {
            if (Clock < u.MarkStart || u.hitAt >= 0f || u.endAt >= 0f || u.by == null) return;
            float next = u.markedAt.Count > 0 ? u.markedAt[u.markedAt.Count - 1] + 0.16f : u.MarkStart;
            if (Clock < next) return;
            if (u.Fork)
            {
                if (u.landingLocked) return;
                RagdollPawn best = null;
                float bestD = float.MaxValue;
                foreach (var e in RagdollPawn.All)
                {
                    if (e == null || e == u.by || !Teams.AreEnemies(e.Team, u.by.Team) || u.marked.Contains(e) || e.State != PawnState.Active || !UltInRound(u, e)) continue;
                    float d = Flat(e.Hips.position - u.by.PlanPoint).magnitude;
                    if (d >= bestD) continue;
                    best = e;
                    bestD = d;
                }
                if (best != null) UltRedMark(u, best, u.marked.Count + 1);
                return;
            }
            foreach (var (e, order) in u.told)
            {
                if (e == null || u.marked.Contains(e)) continue;
                UltRedMark(u, e, order);
                if (u.goAt >= 0f) UltScreenTourAdded(u);
                break;
            }
        }

        /// <summary>Motes of light drifting in the air round the caster while the show lasts (the shorts' glitter).</summary>
        void UltGlitter(UltShow u, float dt)
        {
            if (dt <= 0f || u.by == null) return;
            float k = u.Life(Clock);
            if (k <= 0f || Random.value > 0.55f * k) return;
            Vector3 c = u.hitAt >= 0f ? u.hitPoint : u.by.Hips.position;
            var off = Random.insideUnitSphere * 9f;
            off.y = Mathf.Abs(off.y) * 0.5f + 0.3f;
            motes.Emit(c + off, Random.insideUnitSphere * 0.25f + Vector3.up * 0.15f, Random.Range(0.04f, 0.09f), Random.Range(1.2f, 2.2f), Tint(Random.value < 0.6f ? Color.white : TealPale, 0.3f));
        }

        // ---------------------------------------------------------------- the starry sky

        Transform skyBall;
        MeshRenderer skyRenderer;
        readonly MaterialPropertyBlock skyBlock = new MaterialPropertyBlock();

        /// <summary>From the call to a while after the landing the sky turns to stars (the shorts' board in space), on every
        /// screen, behind everything solid.</summary>
        void UltSky(Camera cam)
        {
            float k = 0f;
            foreach (var u in ults)
            {
                if (u.goAt < 0f) continue;
                float on = Smooth01((Clock - u.goAt) / 0.35f);
                float off = u.hitAt >= 0f ? 1f - Smooth01((Clock - u.hitAt - 1.4f) / 1.2f) : 1f;
                k = Mathf.Max(k, on * off * u.Life(Clock));
            }
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
            float r = Mathf.Min(380f, cam.farClipPlane * 0.8f);
            skyBall.SetPositionAndRotation(cam.transform.position, Quaternion.Euler(0f, Clock * 0.6f, 0f));
            skyBall.localScale = Vector3.one * r * 2f;
            skyBlock.SetColor("_Color", new Color(1.4f, 1.45f, 1.6f, 1f));
            skyBlock.SetFloat("_Opacity", k);
            skyRenderer.SetPropertyBlock(skyBlock);
            skyRenderer.enabled = true;
        }
    }
}
