using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
#if CF_URP
using UnityEngine.Rendering.Universal;
#endif
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The stage of the ultimates' "!!" presentation (JY-skill-lab, 2026-11-22; the user: "시전자랑 공격받는 범위 등에만 강한
    /// 조명 + 강렬한 네온 빛 이펙트"; the spec's §4, §5 N8-N10, N14, N15, N17 and §10's pattern pillars). With the world dimmed
    /// (PawnRushSkillFx.UltScreen.cs), real light falls only on the caster and on what it attacks, and the move is drawn in
    /// lines of light:
    ///
    ///   the key spot     L1: a square of white light straight down on the caster, which it stands in and carries with it
    ///   the rim          L2: a teal light behind the caster as each screen sees it, on rendering layer 5 with the caster's
    ///                    skin, so it edges the silhouette and spills on nothing else
    ///   the aim          L3: a spot on the square the caster aims at (its own screen)
    ///   warnings         L4: a red pool of light on the floor an attack is committed to, growing until the blow
    ///   targets          L5: a red light behind a targeted enemy's head, on rendering layer 6 with its skin
    ///   hits and blows   L6: a red flash over a struck head; L7: a flash over a blow going from white to teal
    ///   the judgement    L8: a wide spot on the to-square in place of the key spot; L9: the flying queen's teal gaze
    ///   the budget       twelve lights on a screen at most and eight within 25 m of the camera, the least needed off first;
    ///                    the Flare()s left in the ultimates capped (the spec's FlareBudget)
    ///   lines of light   light posts (N8), sky beams (N9), the comet trail (N10), the cage round the caster while it drives
    ///                    (N14), a lens streak on a far blow (N17), the eight pillars of the piece's own pattern (§10)
    ///   the giant        N15: the caster's own body, baked at the key and drawn 9-12 m tall behind it in teal rim light
    ///                    (its own screen; in ink while an impact frame wants it)
    ///
    /// The lights are pooled children of root (the world dim skips them), without shadows. Presentation only: nothing here
    /// reads more than the replicated plan, the shows' moments and the pieces' bodies, or changes the game. Not seen in
    /// Unity yet.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        // ================================================================ colours, layers, ranks

        /// <summary>The rendering layers of the rim lights: 5 the caster's ('UltCaster'), 6 a target's ('UltTarget').</summary>
        const int StgCasterBit = 5, StgTargetBit = 6;

        // The budget's order, the first switched off first (the spec's §4: L6, L3, L5, L7, L2, L4, then L8 and L1; the Flare()s
        // left in the ultimates and the queen's gaze, which only borrows a warning's slot, go before what they stand beside).
        const int StgRankFlare = 0, StgRankHit = 1, StgRankAim = 2, StgRankTarget = 3, StgRankBlow = 4, StgRankRim = 5, StgRankGaze = 6, StgRankWarn = 7, StgRankKey = 8;

        static readonly Color StgKeyColor = new Color(1f, 0.97f, 0.92f);
        static readonly Color StgJudgeColor = new Color(0.7f, 1f, 0.95f);
        static readonly Color StgJudgeBlunder = new Color(0.9f, 0.27f, 0.23f);
        static readonly Color StgMiss = new Color(0.6f, 0.6f, 0.62f);
        /// <summary>Straight down, the light's own axes on the world's (Quaternion.LookRotation(down, forward)).</summary>
        static readonly Quaternion StgDown = new Quaternion(0.70710678f, 0f, 0f, 0.70710678f);

        // The piece's own pattern round the to-square (the knight's is KnightSquares), each set from the middle out.
        static readonly Vector2Int[] StgBishopCells =
        {
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1),
            new Vector2Int(2, 2), new Vector2Int(2, -2), new Vector2Int(-2, -2), new Vector2Int(-2, 2),
        };
        static readonly Vector2Int[] StgRookCells =
        {
            new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 1),
            new Vector2Int(2, 0), new Vector2Int(0, -2), new Vector2Int(-2, 0), new Vector2Int(0, 2),
        };
        static readonly Vector2Int[] StgQueenCells =
        {
            new Vector2Int(2, 0), new Vector2Int(2, -2), new Vector2Int(0, -2), new Vector2Int(-2, -2),
            new Vector2Int(-2, 0), new Vector2Int(-2, 2), new Vector2Int(0, 2), new Vector2Int(2, 2),
        };

        // ================================================================ parts

        bool stgBuilt;
        Material stgMatCore, stgMatSheath, stgMatCoreRed, stgMatSheathRed, stgMatStreak, stgMatGiantRim, stgMatGiantInk;
        Texture2D stgTexSoftSquare, stgTexRod;
        MaterialPropertyBlock stgBlock;
        int stgColorId, stgOpacityId, stgDissolveId;
        readonly List<Vector3> stgVerts = new List<Vector3>();

        /// <summary>The stage's materials and textures, made when first wanted.</summary>
        void StgBuild()
        {
            if (stgBuilt) return;
            stgBuilt = true;
            stgColorId = Shader.PropertyToID("_Color");
            stgOpacityId = Shader.PropertyToID("_Opacity");
            stgDissolveId = Shader.PropertyToID("_Dissolve");
            stgBlock = new MaterialPropertyBlock();
            stgTexSoftSquare = StgSquareCookie();
            // A rod's length: full, softening over its last eighth at the top and a hair at the foot.
            stgTexRod = Tex(4, 64, TextureWrapMode.Clamp, (u, v) => White(Mathf.SmoothStep(0f, 1f, (1f - v) / 0.13f) * Mathf.SmoothStep(0f, 1f, v / 0.015f)));
            stgMatCore = Glow(stgTexRod, rim: -0.45f, rimPower: 1.2f);
            stgMatSheath = Glow(stgTexRod, noise: 0.25f, noiseST: new Vector4(1f, 2f, 0f, -0.8f), rim: -1f, rimPower: 2.4f, soft: 0.25f);
            // Red after teal (the caps' "warnings win": render queue +10).
            stgMatCoreRed = Glow(stgTexRod, rim: -0.45f, rimPower: 1.2f);
            stgMatSheathRed = Glow(stgTexRod, noise: 0.25f, noiseST: new Vector4(1f, 2f, 0f, -0.8f), rim: -1f, rimPower: 2.4f, soft: 0.25f);
            stgMatCoreRed.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            stgMatSheathRed.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            stgMatStreak = Glow(texSoft);
            stgMatGiantRim = Glow(Texture2D.whiteTexture, rim: 0.95f, rimPower: 2.2f);
            stgMatGiantInk = Glow(Texture2D.whiteTexture, opacity: 1f);
        }

        /// <summary>texSoftSquare, the key spot's cookie (the spec's §4): 128², 1 inside the central 62 % of each side, smoothstep
        /// to 0 at 70 %, the same in every channel (URP reads a cookie's colour, its alpha or its red by its atlas' format).</summary>
        static Texture2D StgSquareCookie()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, true)
            {
                name = "Ult key cookie", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float s = Mathf.Max(Mathf.Abs((x + 0.5f) / n * 2f - 1f), Mathf.Abs((y + 0.5f) / n * 2f - 1f));
                    byte b = (byte)(255f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.70f, s))));
                    px[y * n + x] = new Color32(b, b, b, b);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        // ================================================================ the lights' pool

        /// <summary>One light of the stage, pooled; or a Flare()'s light the budget keeps count of (external).</summary>
        sealed class StgLamp
        {
            public Light light;
            public int rank;
            public uint mask;
            public bool busy, on, cut, external, replaced;
            public float since, dist, floorY = float.NaN;
#if CF_URP
            public UniversalAdditionalLightData data;
#endif
        }

        readonly List<StgLamp> stgPool = new List<StgLamp>();
        readonly List<StgLamp> stgFlares = new List<StgLamp>();
        readonly List<StgLamp> stgFlareSpare = new List<StgLamp>();
        readonly List<StgLamp> stgLive = new List<StgLamp>();
        static readonly RaycastHit[] stgHits = new RaycastHit[16];
        int stgEpoch, stgSalt, stgCapsFrame = -1;
        bool stgLayersOn, stgCookiesOn = true;

        /// <summary>A light from the pool, set up as asked and off until its owner gives it a level.</summary>
        StgLamp StgTake(LightType type, int rank, Color color, float range)
        {
            StgLamp l = null;
            for (int i = 0; i < stgPool.Count; i++)
                if (!stgPool[i].busy && stgPool[i].light != null)
                {
                    l = stgPool[i];
                    break;
                }
            if (l == null)
            {
                var go = new GameObject("Ult stage light");
                go.transform.SetParent(root, false);
                var made = go.AddComponent<Light>();
                made.shadows = LightShadows.None;
                made.renderMode = LightRenderMode.ForcePixel;
                made.enabled = false;
                l = new StgLamp { light = made, mask = uint.MaxValue };
                stgPool.Add(l);
            }
            var light = l.light;
            light.type = type;
            light.color = color;
            light.range = range;
            light.intensity = 0f;
            light.cookie = null;
            light.enabled = false;
            l.busy = true;
            l.on = l.cut = false;
            l.rank = rank;
            l.since = RealClock;
            StgLampLayers(l, 0u);
            return l;
        }

        /// <summary>A light back in the pool.</summary>
        void StgFree(StgLamp l)
        {
            if (l == null || l.external) return;
            l.busy = l.on = l.cut = false;
            if (l.light == null) return;
            l.light.enabled = false;
            l.light.intensity = 0f;
            l.light.cookie = null;
        }

        /// <summary>The rendering layers a stage light lights; 0 the project's default, as every other light. URP 17 shades by its
        /// additional light data's layers (and copies them to Light.renderingLayerMask for the shadows).</summary>
        void StgLampLayers(StgLamp l, uint mask)
        {
            if (l == null || l.light == null) return;
            uint m = mask != 0u ? mask : RenderingLayerMask.defaultRenderingLayerMask.value;
            if (l.mask == m) return;
            l.mask = m;
#if CF_URP
            if (l.data == null) l.data = l.light.GetUniversalAdditionalLightData();
            l.data.renderingLayers = m;
#endif
            l.light.renderingLayerMask = unchecked((int)m);
        }

        /// <summary>What this screen's pipeline gives the stage, looked at once a frame: rendering layers for lights (URP's
        /// useRenderingLayers, with layers 5 and 6 defined in the project) and spot cookies (URP's supportsLightCookies; the
        /// built-in pipeline always draws them).</summary>
        void StgCaps()
        {
            if (stgCapsFrame == Time.frameCount) return;
            stgCapsFrame = Time.frameCount;
            stgLayersOn = false;
            stgCookiesOn = true;
#if CF_URP
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                stgCookiesOn = urp.supportsLightCookies;
                uint bits = (1u << StgCasterBit) | (1u << StgTargetBit);
                stgLayersOn = urp.useRenderingLayers && (RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue() & bits) == bits;
            }
#endif
        }

        // ================================================================ the budget (every frame)

        /// <summary>
        /// Every frame, after the stage's lights have their levels (UltStep): at most twelve lights on this screen and eight
        /// within 25 m of the camera (the caps' §6, one budget for two ultimates at once). Over it, lights are switched off for
        /// the frame in the order the spec's §4 gives - the hit flashes (L6), the aim (L3), the target rims (L5), the blow (L7),
        /// the caster's rim (L2), the warnings (L4), the key and judgement spots (L1, L8) last - with the tracked Flare()s before
        /// all of them and the farthest first among equals. A tracked Flare() within 15 m of the camera is also kept 1.5 m over
        /// its floor.
        /// </summary>
        void UltLightBudget(Camera cam)
        {
            stgLive.Clear();
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            for (int i = stgPool.Count - 1; i >= 0; i--)
            {
                var l = stgPool[i];
                if (l.light == null)
                {
                    stgPool.RemoveAt(i);
                    continue;
                }
                if (!l.busy) continue;
                l.cut = false;
                if (!l.on)
                {
                    l.light.enabled = false;
                    continue;
                }
                l.dist = cam != null ? Vector3.Distance(eye, l.light.transform.position) : 0f;
                stgLive.Add(l);
            }
            for (int i = stgFlares.Count - 1; i >= 0; i--)
            {
                var f = stgFlares[i];
                if (f.light == null)
                {
                    stgFlares.RemoveAt(i);
                    f.light = null;
                    stgFlareSpare.Add(f);
                    continue;
                }
                if (f.replaced)
                {
                    f.light.enabled = false;
                    continue;
                }
                Vector3 p = f.light.transform.position;
                f.dist = cam != null ? Vector3.Distance(eye, p) : 0f;
                if (f.dist <= 15f && !float.IsNaN(f.floorY) && p.y < f.floorY + 1.5f)
                {
                    p.y = f.floorY + 1.5f;
                    f.light.transform.position = p;
                }
                f.cut = false;
                if (f.light.intensity > 1e-4f) stgLive.Add(f);
            }
            // The least needed first (an insertion sort: a handful of lights, nothing made).
            for (int i = 1; i < stgLive.Count; i++)
            {
                var x = stgLive[i];
                int j = i - 1;
                while (j >= 0 && StgCutsBefore(x, stgLive[j]))
                {
                    stgLive[j + 1] = stgLive[j];
                    j--;
                }
                stgLive[j + 1] = x;
            }
            int over = stgLive.Count - 12, near = 0;
            for (int i = 0; i < stgLive.Count; i++)
            {
                var l = stgLive[i];
                if (over > 0)
                {
                    l.cut = true;
                    over--;
                }
                else if (l.dist <= 25f) near++;
            }
            for (int i = 0; i < stgLive.Count && near > 8; i++)
            {
                var l = stgLive[i];
                if (l.cut || l.dist > 25f) continue;
                l.cut = true;
                near--;
            }
            for (int i = 0; i < stgLive.Count; i++) stgLive[i].light.enabled = !stgLive[i].cut;
        }

        static bool StgCutsBefore(StgLamp a, StgLamp b) => a.rank < b.rank || (a.rank == b.rank && a.dist > b.dist);

        /// <summary>
        /// The spec's FlareBudget for a Flare() about to be made at <paramref name="at"/>: its peak at most 2.5·h² (h its height
        /// over the floor, so it never lights the floor under it past E 2.5), its life 0.35 s at most, and within 15 m of the
        /// camera its range 9 m at most and h counted as 1.5 m at least (UltFlareTrack lifts it there). One raycast.
        /// </summary>
        void UltFlareClamp(Vector3 at, ref float peak, ref float range, ref float life)
        {
            float floor = StgFloorY(at + Vector3.up * 0.2f, 40f);
            float h = float.IsNaN(floor) ? 40f : Mathf.Max(0.05f, at.y - floor);
            var cam = ViewCamera;
            if (cam != null && (cam.transform.position - at).sqrMagnitude <= 15f * 15f)
            {
                h = Mathf.Max(h, 1.5f);
                range = Mathf.Min(range, 9f);
            }
            peak = Mathf.Min(peak, 2.5f * h * h);
            life = Mathf.Min(life, 0.35f);
        }

        /// <summary>A Flare()'s light (or any other effect light) counted in the budget from now until it is destroyed: three
        /// at most alive, a fourth switching the oldest off for good; within 15 m of the camera it is kept 1.5 m over the floor
        /// it was made over. One raycast.</summary>
        void UltFlareTrack(Light light)
        {
            if (light == null) return;
            int alive = 0;
            StgLamp oldest = null;
            for (int i = stgFlares.Count - 1; i >= 0; i--)
            {
                var f = stgFlares[i];
                if (f.light == null)
                {
                    stgFlares.RemoveAt(i);
                    stgFlareSpare.Add(f);
                    continue;
                }
                if (f.light == light) return;
                if (f.replaced) continue;
                alive++;
                if (oldest == null || f.since <= oldest.since) oldest = f;
            }
            if (alive >= 3 && oldest != null)
            {
                oldest.replaced = true;
                oldest.light.enabled = false;
                oldest.light.intensity = 0f;
            }
            StgLamp n;
            if (stgFlareSpare.Count > 0)
            {
                n = stgFlareSpare[stgFlareSpare.Count - 1];
                stgFlareSpare.RemoveAt(stgFlareSpare.Count - 1);
            }
            else n = new StgLamp();
            n.light = light;
            n.external = n.busy = true;
            n.replaced = n.cut = n.on = false;
            n.rank = StgRankFlare;
            n.since = RealClock;
            n.floorY = StgFloorY(light.transform.position + Vector3.up * 0.2f, 40f);
            stgFlares.Add(n);
        }

        // ================================================================ rendering layers on the pieces' skins

        /// <summary>The layer bits this file has put on one piece's skin: how many ask for each, and which the skin had of its own.</summary>
        sealed class StgMark
        {
            public SkinnedMeshRenderer skin;
            public readonly int[] count = new int[32];
            public uint had, set;
        }

        readonly Dictionary<RagdollPawn, StgMark> stgMarks = new Dictionary<RagdollPawn, StgMark>();

        /// <summary>Rendering layer <paramref name="bit"/> on <paramref name="p"/>'s skin while anyone asks for it (counted), and put
        /// back exactly as the skin had it when the last one lets go. Bit 5 is the caster's rim (L2), bit 6 a target's (L5).</summary>
        void UltLayer(RagdollPawn p, int bit, bool on)
        {
            if (ReferenceEquals(p, null) || bit < 0 || bit > 31) return;
            uint flag = 1u << bit;
            if (!stgMarks.TryGetValue(p, out var m))
            {
                if (!on || p == null) return;
                m = new StgMark { skin = p.skin };
                stgMarks[p] = m;
            }
            if (p != null && p.skin != m.skin) StgMarkMove(m, p.skin);
            if (on)
            {
                if (m.count[bit]++ == 0) StgMarkSet(m, flag);
            }
            else if (m.count[bit] > 0 && --m.count[bit] == 0) StgMarkUnset(m, flag);
            if (m.set == 0u) stgMarks.Remove(p);
        }

        /// <summary>A piece whose skin was swapped: the bits go over to the new one (cheap when nothing changed).</summary>
        void StgMarkFollow(RagdollPawn p)
        {
            if (p != null && stgMarks.TryGetValue(p, out var m) && p.skin != m.skin) StgMarkMove(m, p.skin);
        }

        static void StgMarkSet(StgMark m, uint flag)
        {
            m.set |= flag;
            if (m.skin == null) return;
            if ((m.skin.renderingLayerMask & flag) != 0u) m.had |= flag;
            else m.had &= ~flag;
            m.skin.renderingLayerMask |= flag;
        }

        static void StgMarkUnset(StgMark m, uint flag)
        {
            m.set &= ~flag;
            if (m.skin != null && (m.had & flag) == 0u) m.skin.renderingLayerMask &= ~flag;
            m.had &= ~flag;
        }

        static void StgMarkMove(StgMark m, SkinnedMeshRenderer skin)
        {
            uint bits = m.set;
            for (int b = 0; b < 32; b++) if ((bits & (1u << b)) != 0u) StgMarkUnset(m, 1u << b);
            m.skin = skin;
            for (int b = 0; b < 32; b++) if ((bits & (1u << b)) != 0u) StgMarkSet(m, 1u << b);
        }

        static void StgMarkClear(StgMark m)
        {
            for (int b = 0; b < 32; b++) if ((m.set & (1u << b)) != 0u) StgMarkUnset(m, 1u << b);
            Array.Clear(m.count, 0, m.count.Length);
        }

        // ================================================================ shared helpers

        /// <summary>The floor's height under <paramref name="from"/> within <paramref name="down"/> m, the pieces skipped; NaN when
        /// there is none (one raycast, nothing made).</summary>
        static float StgFloorY(Vector3 from, float down)
        {
            int n = Physics.RaycastNonAlloc(from, Vector3.down, stgHits, down, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, y = float.NaN;
            for (int i = 0; i < n; i++)
            {
                var hit = stgHits[i];
                if (hit.distance >= best || hit.collider == null || hit.collider.GetComponentInParent<RagdollPawn>() != null) continue;
                best = hit.distance;
                y = hit.point.y;
            }
            return y;
        }

        // The bodies where they are drawn (the transforms: interpolated, and what every screen sees).
        static Vector3 StgHips(RagdollPawn p) => p.bodies[0].transform.position;
        static Vector3 StgHead(RagdollPawn p) => p.bodies[(int)BodyId.Head].transform.position;

        /// <summary>This camera's way forward over the floor (looking straight down: the way up its picture).</summary>
        static Vector3 StgFlatForward(Camera cam)
        {
            if (cam == null) return Vector3.forward;
            var t = cam.transform;
            Vector3 f = t.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.01f)
            {
                f = t.up;
                f.y = 0f;
            }
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        /// <summary>The caps' near fade for a line from <paramref name="a"/> to <paramref name="b"/>: × clamp01((d − 1.2) / 2.5), or
        /// × clamp01((d − 3) / 4) for a wide shape, d the camera's distance from it.</summary>
        static float StgNearK(Camera cam, Vector3 a, Vector3 b, bool wide)
        {
            if (cam == null) return 1f;
            Vector3 e = cam.transform.position, ab = b - a;
            float len = ab.sqrMagnitude, t = len > 1e-6f ? Mathf.Clamp01(Vector3.Dot(e - a, ab) / len) : 0f;
            float d = Vector3.Distance(e, a + ab * t);
            return wide ? Mathf.Clamp01((d - 3f) / 4f) : Mathf.Clamp01((d - 1.2f) / 2.5f);
        }

        /// <summary>A light colour within 0..1, what was over it moved into the intensity.</summary>
        static void StgSetColor(Light light, Color c, float intensity)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m > 1f)
            {
                c.r /= m;
                c.g /= m;
                c.b /= m;
                intensity *= m;
            }
            c.a = 1f;
            light.color = c;
            light.intensity = intensity;
        }

        static bool StgTealish(Color c) => c.g > c.r * 1.5f && c.g > 0.05f;

        bool StgLive(UltShow u) => u != null && !u.gone && ults.Contains(u);

        /// <summary>How many warning pools (L4) are lit now.</summary>
        int StgLivePools()
        {
            int n = 0;
            for (int i = 0; i < stgPools.Count; i++) if (stgPools[i].Live) n++;
            return n;
        }

        /// <summary>Teal within 2 m of a live warning drops to half (the caps' §8).</summary>
        float StgWarnK(Vector3 p)
        {
            for (int i = 0; i < stgPools.Count; i++)
            {
                var w = stgPools[i];
                if (!w.Live) continue;
                float r = w.radius + 2f;
                float dx = p.x - w.centreNow.x, dz = p.z - w.centreNow.z;
                if (dx * dx + dz * dz < r * r) return 0.5f;
            }
            return 1f;
        }

        /// <summary>How much of a show's teal shows at <paramref name="p"/> (the caps' §8): half near a live warning, and on an
        /// enemy's screen 0.6 within 8 m of the piece played here.</summary>
        float StgTealK(UltShow u, Vector3 p)
        {
            float k = StgWarnK(p);
            var me = Viewer;
            if (u != null && u.by != null && me != null && me != u.by && Teams.AreEnemies(me.Team, u.by.Team) && (StgHips(me) - p).sqrMagnitude < 64f) k *= 0.6f;
            return k;
        }

        // ================================================================ a show's own stage

        /// <summary>A show's stage (its UltShow.stage): its lights and lines, and the floor under its caster.</summary>
        sealed class StgShow
        {
            public StgKeyAnim key;
            public StgRimAnim rim;
            public StgCageAnim cage;
            public StgTrailAnim trail;
            public StgAvatarAnim avatar;
            public StgJudgeAnim judge;
            public StgGazeAnim gaze;
            public float handedReal = -1f, floorY = float.NaN;
            public int floorFrame = -100, salt;
        }

        StgShow StgOf(UltShow u)
        {
            if (!(u.stage is StgShow s))
            {
                s = new StgShow { salt = stgSalt++ & 3 };
                u.stage = s;
            }
            return s;
        }

        /// <summary>The floor under a show's caster, looked for on every fourth frame (each show on a frame of its own).</summary>
        float StgFloorOf(UltShow u, StgShow s)
        {
            var by = u.by;
            if (by == null) return float.IsNaN(s.floorY) ? 0f : s.floorY;
            if (float.IsNaN(s.floorY) || (((Time.frameCount + s.salt) & 3) == 0 && s.floorFrame != Time.frameCount))
            {
                s.floorFrame = Time.frameCount;
                Vector3 hips = StgHips(by);
                float y = StgFloorY(hips + Vector3.up * 0.3f, 45f);
                s.floorY = float.IsNaN(y) ? hips.y - by.standHeight : y;
            }
            return s.floorY;
        }

        /// <summary>Where a show's caster has its feet: on its floor, or in the air during a leap or the queen's flight; the rolled
        /// rook on its floor.</summary>
        float StgFeetY(UltShow u, StgShow s)
        {
            float floor = StgFloorOf(u, s);
            if (u.by == null || (u.skill == PawnRushSkillIds.RookRoll && u.Playing)) return floor;
            return Mathf.Max(floor, StgHips(u.by).y - u.by.standHeight - 0.35f);
        }

        /// <summary>The flying queen (her drive on: she is in the air from its first frame to her landing).</summary>
        static bool StgQueenFlying(UltShow u) => u.skill == PawnRushSkillIds.QueenDescent && u.Playing && u.by != null && u.by.PlanSkill == u.skill;

        /// <summary>The key spot's level (the spec's §4): 0 → 70 in 0.06 s real at the key, 40 by 0.5 s; at the judgement held to
        /// +0.6 s and out over 1.0 s (?! down to 20 first, ?? cut in 0.15 s); handed over to the judgement spot in 0.05 s; a show
        /// over without a judgement folds in 0.5 s (cut) or 1.0 s. Below 0 once it is out for good.</summary>
        float StgKeyLevel(UltShow u, StgShow s)
        {
            float level = StgKeyCurve(RealClock - u.t0Real);
            if (u.hitReal >= 0f)
            {
                float j = RealClock - u.hitReal, from = StgKeyCurve(u.hitReal - u.t0Real);
                if (u.grade == RagdollPawn.GradeBlunder)
                {
                    if (j >= 0.15f) return -1f;
                    level = from * (1f - j / 0.15f);
                }
                else
                {
                    if (j >= 1.6f) return -1f;
                    if (u.grade == RagdollPawn.GradeInaccuracy) from = Mathf.Lerp(from, 20f, Smooth01(j / 0.15f));
                    level = from * (1f - Smooth01((j - 0.6f) / 1f));
                }
            }
            else if (u.endReal >= 0f)
            {
                float e = RealClock - u.endReal, over = u.cut ? 0.5f : 1f;
                if (e >= over) return -1f;
                level *= 1f - e / over;
            }
            if (s.handedReal >= 0f)
            {
                float k = 1f - Mathf.Clamp01((RealClock - s.handedReal) / 0.05f);
                if (k <= 0f) return -1f;
                level *= k;
            }
            return level;
        }

        static float StgKeyCurve(float a) => a <= 0f ? 0f : a < 0.06f ? 70f * a / 0.06f : Mathf.Lerp(70f, 40f, Smooth01((a - 0.06f) / 0.44f));

        /// <summary>The rim's level: in with the key, after the judgement held to +0.6 s and out over 1.0 s (?? cut in 0.15 s), a
        /// show over without one folding like the key spot. Below 0 once it is out for good.</summary>
        float StgRimLevel(UltShow u)
        {
            float level = Mathf.Clamp01((RealClock - u.t0Real) / 0.06f);
            if (u.hitReal >= 0f)
            {
                float j = RealClock - u.hitReal;
                if (u.grade == RagdollPawn.GradeBlunder) return j >= 0.15f ? -1f : level * (1f - j / 0.15f);
                return j >= 1.6f ? -1f : level * (1f - Smooth01((j - 0.6f) / 1f));
            }
            if (u.endReal >= 0f)
            {
                float e = RealClock - u.endReal, over = u.cut ? 0.5f : 1f;
                return e >= over ? -1f : level * (1f - e / over);
            }
            return level;
        }

        // ================================================================ the stage's animations

        /// <summary>Every animation of the stage: stepped in the effects' list, ended by DestroyStage, given back once.</summary>
        abstract class StgAnim : Anim
        {
            protected readonly PawnRushSkillFx fx;
            readonly int epoch;
            bool stopped, released;

            protected StgAnim(PawnRushSkillFx fx)
            {
                this.fx = fx;
                epoch = fx.stgEpoch;
            }

            /// <summary>Done (ended, stopped or given back).</summary>
            public bool Over => stopped || released;

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (stopped || released || epoch != fx.stgEpoch) return false;
                if (Run(dt, cam)) return true;
                stopped = true;
                return false;
            }

            /// <summary>Ends it now: its lights go back to the pool at once, the list drops it at its next step.</summary>
            public void Stop()
            {
                stopped = true;
                Destroy();
            }

            public override void Destroy()
            {
                if (released) return;
                released = true;
                Release();
            }

            protected abstract bool Run(float dt, Camera cam);
            protected abstract void Release();
        }

        // ---------------------------------------------------------------- L1 the key spot

        /// <summary>
        /// L1, the key spot (the spec's §4), on every screen, from the key to the judgement: a spot (1, 0.97, 0.92) straight down
        /// through texSoftSquare, so the caster stands in a square of real light about 2.7 m wide (soft to 3.06 m), following its
        /// x and z. 6 m over the floor (never under the piece in a leap); a giant 4 m over its drawn height, the rook 7 m over its
        /// column, both brighter by (h / 6)² so the floor gets the same light; the flying queen 6 m over her body. 0 → 70 in
        /// 0.06 s real, 40 by 0.5 s; at the judgement held to +0.6 s then out over 1.0 s (?! to 20, ?? cut in 0.15 s); handed over
        /// to L8 when UltJudgeSpot runs. Without cookies a round spot (30°, inner 22°). Call at the cast, once the show is in ults.
        /// </summary>
        void UltKeySpot(UltShow u)
        {
            if (u == null || u.by == null) return;
            StgBuild();
            var s = StgOf(u);
            if (s.key != null && !s.key.Over) return;
            s.key = new StgKeyAnim(this, u) { tick = UltTick.Real };
            anims.Add(s.key);
        }

        sealed class StgKeyAnim : StgAnim
        {
            readonly UltShow u;
            StgLamp lamp;
            float y = float.NaN;

            public StgKeyAnim(PawnRushSkillFx fx, UltShow u) : base(fx) => this.u = u;

            protected override bool Run(float dt, Camera cam)
            {
                if (!fx.StgLive(u) || u.by == null) return false;
                var s = fx.StgOf(u);
                float level = fx.StgKeyLevel(u, s);
                if (level < 0f) return false;
                var by = u.by;
                Vector3 hips = StgHips(by);
                float giant = fx.GiantScaleOf(by), h, want;
                if (StgQueenFlying(u))
                {
                    h = 6f;
                    want = hips.y + 6f;
                }
                else
                {
                    if (giant > 1.01f) h = Mathf.Max(6f, fx.GiantBaseHeight(by) * giant + 4f);
                    else h = u.skill == PawnRushSkillIds.RookRoll && u.Playing ? 7f : 6f;
                    want = fx.StgFloorOf(u, s) + h;
                    if (giant <= 1.01f) want = Mathf.Max(want, hips.y + 3f);
                }
                y = float.IsNaN(y) ? want : Mathf.Lerp(y, want, 1f - Mathf.Exp(-12f * Mathf.Max(0f, dt)));
                if (lamp == null) lamp = fx.StgTake(LightType.Spot, StgRankKey, StgKeyColor, h + 3f);
                var light = lamp.light;
                if (light == null) return false;
                fx.StgCaps();
                bool square = fx.stgCookiesOn && fx.stgTexSoftSquare != null;
                Texture cookie = square ? fx.stgTexSoftSquare : null;
                if (light.cookie != cookie) light.cookie = cookie;
                light.spotAngle = square ? 40f : 30f;
                light.innerSpotAngle = square ? 38f : 22f;
                light.range = h + 3f;
                light.transform.SetPositionAndRotation(new Vector3(hips.x, y, hips.z), StgDown);
                StgSetColor(light, StgKeyColor, level * (h / 6f) * (h / 6f));
                lamp.on = light.intensity > 1e-4f;
                light.enabled = lamp.on;
                return true;
            }

            protected override void Release()
            {
                fx.StgFree(lamp);
                lamp = null;
            }
        }

        // ---------------------------------------------------------------- L2 the caster's rim

        /// <summary>
        /// L2, the caster's rim (the spec's §4), placed on each screen: a Teal point light, 2.0 (±10 % at 2 Hz), 0.8 m over the
        /// head and 0.9 m along this camera's flat forward - behind the caster as this screen sees it - range 2.5 m. On rendering
        /// layer 5 with the caster's skin while it lasts (the skin's mask put back exactly after), so it edges the silhouette and
        /// spills on nothing else; without rendering layers (checked at run time) range 1.4 m and no layer. A giant's at its
        /// drawn head, range and offsets × its scale (and the light × scale² to keep its strength); the rolled rook's over its
        /// column. Out after the judgement like the key spot (?? cut in 0.15 s). Call at the cast, once the show is in ults.
        /// </summary>
        void UltRim(UltShow u)
        {
            if (u == null || u.by == null) return;
            StgBuild();
            var s = StgOf(u);
            if (s.rim != null && !s.rim.Over) return;
            s.rim = new StgRimAnim(this, u) { tick = UltTick.Real };
            anims.Add(s.rim);
        }

        sealed class StgRimAnim : StgAnim
        {
            readonly UltShow u;
            readonly RagdollPawn by;
            StgLamp lamp;
            bool layered;

            public StgRimAnim(PawnRushSkillFx fx, UltShow u) : base(fx)
            {
                this.u = u;
                by = u.by;
            }

            protected override bool Run(float dt, Camera cam)
            {
                if (!fx.StgLive(u) || by == null || u.by != by) return false;
                float level = fx.StgRimLevel(u);
                if (level < 0f) return false;
                if (lamp == null) lamp = fx.StgTake(LightType.Point, StgRankRim, Teal, 2.5f);
                var light = lamp.light;
                if (light == null) return false;
                fx.StgCaps();
                bool layers = fx.stgLayersOn;
                if (layers != layered)
                {
                    layered = layers;
                    fx.UltLayer(by, StgCasterBit, layers);
                }
                else if (layered) fx.StgMarkFollow(by);
                fx.StgLampLayers(lamp, layers ? 1u << StgCasterBit : 0u);
                float giant = Mathf.Max(1f, fx.GiantScaleOf(by)), g = giant;
                Vector3 head = StgHead(by), hips = StgHips(by);
                if (u.skill == PawnRushSkillIds.RookRoll && u.Playing)
                {
                    head = hips + Vector3.up * (fx.Params != null ? fx.Params.rrRadius : 1.5f);   // the toppled tower's top
                    g = 1.6f;
                }
                else if (giant > 1.01f)
                {
                    var feet = new Vector3(hips.x, fx.StgFloorOf(u, fx.StgOf(u)), hips.z);
                    head = feet + (head - feet) * giant;
                }
                light.transform.position = head + Vector3.up * (0.8f * g) + StgFlatForward(cam) * (0.9f * g);
                light.range = (layers ? 2.5f : 1.4f) * g;
                StgSetColor(light, Teal, 2f * g * g * (1f + 0.1f * Mathf.Sin(fx.RealClock * 4f * Mathf.PI)) * level);
                lamp.on = light.intensity > 1e-4f;
                light.enabled = lamp.on;
                return true;
            }

            protected override void Release()
            {
                fx.StgFree(lamp);
                lamp = null;
                if (!layered) return;
                layered = false;
                fx.UltLayer(by, StgCasterBit, false);
            }
        }

        // ---------------------------------------------------------------- L3 the aim

        StgAimAnim stgAim;

        /// <summary>
        /// L3, the aim spot (the spec's §4), on the caster's screen only and one at a time (a new one replaces it): a spot 7 m over
        /// <paramref name="at"/>() (null: dark for now), 30°, range 10 m, 30 (E 0.6), in <paramref name="colour"/>() - Teal on an
        /// empty square, Blood fastened on an enemy, grey 0.5 blocked (teal at half near a live warning). For the knight's aim
        /// and the queen's dive aim; it ends with the show's play.
        /// </summary>
        void UltAimSpot(UltShow u, Func<Vector3?> at, Func<Color> colour)
        {
            if (u == null || u.by == null || at == null || Viewer != u.by) return;
            StgBuild();
            stgAim?.Stop();
            stgAim = new StgAimAnim(this, u, at, colour) { tick = UltTick.Real };
            anims.Add(stgAim);
        }

        sealed class StgAimAnim : StgAnim
        {
            readonly UltShow u;
            readonly Func<Vector3?> at;
            readonly Func<Color> colour;
            StgLamp lamp;

            public StgAimAnim(PawnRushSkillFx fx, UltShow u, Func<Vector3?> at, Func<Color> colour) : base(fx)
            {
                this.u = u;
                this.at = at;
                this.colour = colour;
            }

            protected override bool Run(float dt, Camera cam)
            {
                if (!fx.StgLive(u) || !u.Playing) return false;
                Vector3? p = at();
                if (!p.HasValue)
                {
                    fx.StgFree(lamp);
                    lamp = null;
                    return true;
                }
                if (lamp == null) lamp = fx.StgTake(LightType.Spot, StgRankAim, Teal, 10f);
                var light = lamp.light;
                if (light == null) return false;
                Color c = colour != null ? colour() : Teal;
                light.spotAngle = 30f;
                light.innerSpotAngle = 22f;
                light.range = 10f;
                light.transform.SetPositionAndRotation(p.Value + Vector3.up * 7f, StgDown);
                StgSetColor(light, c, 30f * (StgTealish(c) ? fx.StgTealK(u, p.Value) : 1f));
                lamp.on = light.intensity > 1e-4f;
                light.enabled = lamp.on;
                return true;
            }

            protected override void Release()
            {
                fx.StgFree(lamp);
                lamp = null;
                if (fx.stgAim == this) fx.stgAim = null;
            }
        }

        // ---------------------------------------------------------------- L4 the warning pools

        readonly List<StgPoolAnim> stgPools = new List<StgPoolAnim>();

        /// <summary>
        /// L4, a warning pool (the spec's §4), on every screen, two at most (a third switches the oldest off; the queen's gaze L9
        /// yields its slot): a Blood spot <paramref name="height"/> m over <paramref name="centre"/>() (the sacrifice: 10), its
        /// angle 2·atan(r/h) up to 120°, range h + 5, rising 0 → <paramref name="peak"/> (70: E 1.4; the sacrifice 140) over
        /// <paramref name="seconds"/> on the game's clock, off 0.1 s after <paramref name="alive"/>() turns false (null: while the
        /// show plays); × <paramref name="ownTeam"/> on the caster's own team's screens (the rook's lane: 0.5). Never dimmed.
        /// </summary>
        void UltWarnPool(UltShow u, Func<Vector3> centre, float radius, float seconds, float peak, Func<bool> alive, float height = 7f, float ownTeam = 1f)
        {
            if (u == null || centre == null) return;
            StgBuild();
            for (int i = stgPools.Count - 1; i >= 0; i--) if (stgPools[i].Over) stgPools.RemoveAt(i);
            while (stgPools.Count >= 2)
            {
                stgPools[0].Stop();
                stgPools.RemoveAt(0);
            }
            var me = Viewer;
            float team = me != null && u.by != null && !Teams.AreEnemies(me.Team, u.by.Team) ? ownTeam : 1f;
            var pool = new StgPoolAnim(this, u, centre, alive, Mathf.Max(0.1f, radius), Mathf.Max(0f, seconds), Mathf.Max(0f, peak * team), Mathf.Max(1f, height)) { tick = UltTick.Game };
            stgPools.Add(pool);
            anims.Add(pool);
        }

        sealed class StgPoolAnim : StgAnim
        {
            readonly UltShow u;
            readonly Func<Vector3> centre;
            readonly Func<bool> alive;
            readonly float seconds, peak, height, t0;
            public readonly float radius;
            public Vector3 centreNow;
            float offAt = -1f;
            StgLamp lamp;

            public StgPoolAnim(PawnRushSkillFx fx, UltShow u, Func<Vector3> centre, Func<bool> alive, float radius, float seconds, float peak, float height) : base(fx)
            {
                this.u = u;
                this.centre = centre;
                this.alive = alive;
                this.radius = radius;
                this.seconds = seconds;
                this.peak = peak;
                this.height = height;
                t0 = fx.Clock;
                centreNow = centre();
            }

            /// <summary>Still warning (not in its last 0.1 s after the blow).</summary>
            public bool Live => !Over && offAt < 0f;

            protected override bool Run(float dt, Camera cam)
            {
                if (!fx.StgLive(u)) return false;
                if (offAt < 0f && (alive != null ? !alive() : !u.Playing)) offAt = fx.Clock;
                if (offAt >= 0f && fx.Clock - offAt >= 0.1f) return false;
                centreNow = centre();
                if (lamp == null) lamp = fx.StgTake(LightType.Spot, StgRankWarn, Blood, height + 5f);
                var light = lamp.light;
                if (light == null) return false;
                float angle = Mathf.Min(120f, 2f * Mathf.Atan(radius / height) * Mathf.Rad2Deg);
                light.spotAngle = angle;
                light.innerSpotAngle = Mathf.Max(1f, angle - 6f);
                light.range = height + 5f;
                light.transform.SetPositionAndRotation(centreNow + Vector3.up * height, StgDown);
                StgSetColor(light, Blood, peak * (seconds > 0f ? Mathf.Clamp01((fx.Clock - t0) / seconds) : 1f));
                lamp.on = light.intensity > 1e-4f;
                light.enabled = lamp.on;
                return true;
            }

            protected override void Release()
            {
                fx.StgFree(lamp);
                lamp = null;
            }
        }

        // ---------------------------------------------------------------- L5 the target rims

        /// <summary>One ask for a target rim: which show, which enemy, for how long.</summary>
        sealed class StgTarget
        {
            public UltShow u;
            public RagdollPawn e;
            public Func<bool> alive;
            public StgLamp lamp;
            public bool layered;
        }

        readonly List<StgTarget> stgTargets = new List<StgTarget>();
        StgTargetsAnim stgTargetsAnim;

        /// <summary>
        /// L5, a target rim (the spec's §4) for <paramref name="e"/> while <paramref name="alive"/>() holds (null: while the show
        /// plays): a Blood point light, 2.0, range 2.0 m, 0.6 m over and 0.6 m behind its head as this camera sees it, on rendering
        /// layer 6 with its skin. One light per enemy, three on a screen at most (the first asked keep theirs; the rest wait).
        /// Who gets one is the caller's: the caster's screen the nearest three threatened (DriveThreat), an enemy's own screen
        /// itself, allies' screens fastened or locked targets only. The same show asking again for the same enemy replaces its rule.
        /// </summary>
        void UltTargetRim(UltShow u, RagdollPawn e, Func<bool> alive)
        {
            if (u == null || e == null) return;
            StgBuild();
            for (int i = 0; i < stgTargets.Count; i++)
                if (stgTargets[i].u == u && stgTargets[i].e == e)
                {
                    stgTargets[i].alive = alive;
                    return;
                }
            stgTargets.Add(new StgTarget { u = u, e = e, alive = alive });
            if (stgTargetsAnim == null || stgTargetsAnim.Over)
            {
                stgTargetsAnim = new StgTargetsAnim(this) { tick = UltTick.Real };
                anims.Add(stgTargetsAnim);
            }
        }

        void StgTargetLight(StgTarget t, Vector3 forward)
        {
            if (t.lamp == null) t.lamp = StgTake(LightType.Point, StgRankTarget, Blood, 2f);
            var light = t.lamp.light;
            if (light == null) return;
            bool layers = stgLayersOn;
            if (layers != t.layered)
            {
                t.layered = layers;
                UltLayer(t.e, StgTargetBit, layers);
            }
            else if (t.layered) StgMarkFollow(t.e);
            StgLampLayers(t.lamp, layers ? 1u << StgTargetBit : 0u);
            light.transform.position = StgHead(t.e) + Vector3.up * 0.6f + forward * 0.6f;
            light.range = 2f;
            StgSetColor(light, Blood, 2f);
            t.lamp.on = true;
            light.enabled = true;
        }

        void StgTargetDark(StgTarget t)
        {
            StgFree(t.lamp);
            t.lamp = null;
            if (!t.layered) return;
            t.layered = false;
            UltLayer(t.e, StgTargetBit, false);
        }

        /// <summary>The target rims on this screen, all of them: the ended out, one light an enemy, three at most.</summary>
        sealed class StgTargetsAnim : StgAnim
        {
            public StgTargetsAnim(PawnRushSkillFx fx) : base(fx) { }

            protected override bool Run(float dt, Camera cam)
            {
                var list = fx.stgTargets;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var t = list[i];
                    if (fx.StgLive(t.u) && t.e != null && (t.alive != null ? t.alive() : t.u.Playing)) continue;
                    fx.StgTargetDark(t);
                    list.RemoveAt(i);
                }
                fx.StgCaps();
                Vector3 forward = StgFlatForward(cam);
                int lit = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    bool first = true;
                    for (int j = 0; j < i && first; j++) if (list[j].e == t.e) first = false;
                    if (!first || lit >= 3)
                    {
                        fx.StgTargetDark(t);
                        continue;
                    }
                    lit++;
                    fx.StgTargetLight(t, forward);
                }
                return list.Count > 0;
            }

            protected override void Release()
            {
                var list = fx.stgTargets;
                for (int i = 0; i < list.Count; i++) fx.StgTargetDark(list[i]);
                if (fx.stgTargetsAnim == this) fx.stgTargetsAnim = null;
            }
        }

        // ---------------------------------------------------------------- L6 hits, L7 the blow

        readonly List<StgFlashAnim> stgHitList = new List<StgFlashAnim>();
        StgFlashAnim stgHitLast, stgBlow;
        int stgHitFrame = -1, stgHitN;
        Vector3 stgHitSum;

        /// <summary>
        /// L6, a hit light (the spec's §4) over a struck piece's head <paramref name="at"/>: a Blood point light 1.5 m over it,
        /// peak 8, range 6 m, rise 0.03 s, life 0.30 s (show time). Hits in one frame are one light at their centroid; three at
        /// most (the oldest goes). Call once a struck piece, from its strike.
        /// </summary>
        void UltHitLight(Vector3 at)
        {
            StgBuild();
            Vector3 p = at + Vector3.up * 1.5f;
            if (stgHitFrame == Time.frameCount && stgHitLast != null && !stgHitLast.Over)
            {
                stgHitSum += p;
                stgHitN++;
                stgHitLast.at = stgHitSum / stgHitN;
                return;
            }
            for (int i = stgHitList.Count - 1; i >= 0; i--) if (stgHitList[i].Over) stgHitList.RemoveAt(i);
            while (stgHitList.Count >= 3)
            {
                stgHitList[0].Stop();
                stgHitList.RemoveAt(0);
            }
            var flash = new StgFlashAnim(this, p, Blood, Blood, 8f, 6f, 0.30f, StgRankHit) { tick = UltTick.Show };
            stgHitList.Add(flash);
            anims.Add(flash);
            stgHitFrame = Time.frameCount;
            stgHitSum = p;
            stgHitN = 1;
            stgHitLast = flash;
        }

        /// <summary>
        /// L7, the blow light (the spec's §4; it replaces the big Flare()s of the remove list): a point light 3 m over the blow at
        /// <paramref name="at"/>, from white to Teal over its life, peak 14 (E 1.55), range 12 m, life 0.35 s (show time). One at
        /// most: a new blow takes its place.
        /// </summary>
        void UltBlowLight(Vector3 at)
        {
            StgBuild();
            stgBlow?.Stop();
            stgBlow = new StgFlashAnim(this, at + Vector3.up * 3f, Color.white, Teal, 14f, 12f, 0.35f, StgRankBlow) { tick = UltTick.Show };
            anims.Add(stgBlow);
        }

        sealed class StgFlashAnim : StgAnim
        {
            public Vector3 at;
            readonly Color from, to;
            readonly float peak, range, life;
            readonly int rank;
            StgLamp lamp;

            public StgFlashAnim(PawnRushSkillFx fx, Vector3 at, Color from, Color to, float peak, float range, float life, int rank) : base(fx)
            {
                this.at = at;
                this.from = from;
                this.to = to;
                this.peak = peak;
                this.range = range;
                this.life = life;
                this.rank = rank;
            }

            protected override bool Run(float dt, Camera cam)
            {
                if (age >= life) return false;
                if (lamp == null) lamp = fx.StgTake(LightType.Point, rank, from, range);
                var light = lamp.light;
                if (light == null) return false;
                const float rise = 0.03f;
                float k = age < rise ? age / rise : 1f - (age - rise) / Mathf.Max(0.01f, life - rise);
                light.transform.position = at;
                light.range = range;
                StgSetColor(light, Color.Lerp(from, to, age / life), peak * k * k);
                lamp.on = light.intensity > 1e-4f;
                light.enabled = lamp.on;
                return true;
            }

            protected override void Release()
            {
                fx.StgFree(lamp);
                lamp = null;
                if (fx.stgBlow == this) fx.stgBlow = null;
                if (fx.stgHitLast == this) fx.stgHitLast = null;
            }
        }

        // ---------------------------------------------------------------- L8 the judgement spot

        /// <summary>
        /// L8, the judgement spot (the spec's §4, §10), on every screen: it replaces the show's key spot (L1 out in 0.05 s). A spot
        /// 9 m over the to-square <paramref name="at"/> (its floor looked for under it), 60°, range 14 m, 110 (E 1.36), in over
        /// 0.05 s real, held 0.6 s, out over 1.0 s; (0.7, 1, 0.95), or (0.9, 0.27, 0.23) for ??; for ?! at half (the key spot's
        /// 40 → 20) draining to the miss grey over 0.4 s. One a show (again: it starts over). Call at the judgement (UltHit).
        /// </summary>
        void UltJudgeSpot(UltShow u, Vector3 at, int grade)
        {
            if (u == null) return;
            StgBuild();
            var s = StgOf(u);
            s.judge?.Stop();
            if (s.handedReal < 0f) s.handedReal = RealClock;
            float floor = StgFloorY(at + Vector3.up * 1.5f, 6f);
            if (!float.IsNaN(floor)) at.y = floor;
            s.judge = new StgJudgeAnim(this, u, at, grade) { tick = UltTick.Real };
            anims.Add(s.judge);
        }

        sealed class StgJudgeAnim : StgAnim
        {
            readonly UltShow u;
            readonly Vector3 at;
            readonly int grade;
            readonly float t0;
            StgLamp lamp;

            public StgJudgeAnim(PawnRushSkillFx fx, UltShow u, Vector3 at, int grade) : base(fx)
            {
                this.u = u;
                this.at = at;
                this.grade = grade;
                t0 = fx.RealClock;
            }

            protected override bool Run(float dt, Camera cam)
            {
                if (!fx.StgLive(u)) return false;
                float a = fx.RealClock - t0;
                if (a >= 1.65f) return false;
                float k = a < 0.05f ? a / 0.05f : 1f - Smooth01((a - 0.65f) / 1f);
                Color c = StgJudgeColor;
                float level = 110f;
                if (grade == RagdollPawn.GradeBlunder) c = StgJudgeBlunder;
                else if (grade == RagdollPawn.GradeInaccuracy)
                {
                    c = Color.Lerp(StgJudgeColor, StgMiss, Smooth01(a / 0.4f));
                    level = 55f;
                }
                if (lamp == null) lamp = fx.StgTake(LightType.Spot, StgRankKey, c, 14f);
                var light = lamp.light;
                if (light == null) return false;
                light.spotAngle = 60f;
                light.innerSpotAngle = 48f;
                light.range = 14f;
                light.transform.SetPositionAndRotation(at + Vector3.up * 9f, StgDown);
                StgSetColor(light, c, level * k);
                lamp.on = light.intensity > 1e-4f;
                light.enabled = lamp.on;
                return true;
            }

            protected override void Release()
            {
                fx.StgFree(lamp);
                lamp = null;
            }
        }

        // ---------------------------------------------------------------- L9 the queen's gaze

        /// <summary>
        /// L9, the queen's gaze (the spec's §4), on every screen while <paramref name="flying"/>() holds (null: while the show
        /// plays): a Teal spot from under her body straight down, 30°, range h + 8, h² (E 1.0 on the floor h below) - a teal pool
        /// where she hovers. It borrows a warning's slot: dark while two warning pools are lit. Call at her cast.
        /// </summary>
        void UltGaze(UltShow u, Func<bool> flying)
        {
            if (u == null || u.by == null) return;
            StgBuild();
            var s = StgOf(u);
            s.gaze?.Stop();
            s.gaze = new StgGazeAnim(this, u, flying) { tick = UltTick.Real };
            anims.Add(s.gaze);
        }

        sealed class StgGazeAnim : StgAnim
        {
            readonly UltShow u;
            readonly Func<bool> flying;
            StgLamp lamp;
            float k, floorY = float.NaN;
            int probed = -100;

            public StgGazeAnim(PawnRushSkillFx fx, UltShow u, Func<bool> flying) : base(fx)
            {
                this.u = u;
                this.flying = flying;
            }

            protected override bool Run(float dt, Camera cam)
            {
                if (!fx.StgLive(u) || u.by == null) return false;
                bool fly = u.Playing && (flying == null || flying());
                k = Mathf.MoveTowards(k, fly && fx.StgLivePools() < 2 ? 1f : 0f, Mathf.Max(0f, dt) / 0.2f);
                if (k <= 0f)
                {
                    fx.StgFree(lamp);
                    lamp = null;
                    return u.Playing;
                }
                var by = u.by;
                Vector3 hips = StgHips(by);
                float y0 = hips.y - by.standHeight - 0.3f;
                if (float.IsNaN(floorY) || Time.frameCount - probed >= 4)
                {
                    probed = Time.frameCount;
                    floorY = StgFloorY(new Vector3(hips.x, y0, hips.z), 60f);
                }
                float h = float.IsNaN(floorY) ? 20f : Mathf.Max(1f, y0 - floorY);
                if (lamp == null) lamp = fx.StgTake(LightType.Spot, StgRankGaze, Teal, h + 8f);
                var light = lamp.light;
                if (light == null) return false;
                light.spotAngle = 30f;
                light.innerSpotAngle = 22f;
                light.range = h + 8f;
                light.transform.SetPositionAndRotation(new Vector3(hips.x, y0, hips.z), StgDown);
                StgSetColor(light, Teal, h * h * k * fx.StgTealK(u, new Vector3(hips.x, y0 - h, hips.z)));
                lamp.on = light.intensity > 1e-4f;
                light.enabled = lamp.on;
                return true;
            }

            protected override void Release()
            {
                fx.StgFree(lamp);
                lamp = null;
            }
        }

        // ================================================================ lines of light

        /// <summary>Two open cylinders of light round one axis: a thin core and, if it has one, a soft sheath.</summary>
        sealed class StgRod
        {
            readonly Transform coreT, sheathT;
            readonly MeshRenderer coreR, sheathR;

            public StgRod(Transform parent, string name, Mesh mesh, Material core, Material sheath)
            {
                coreT = Part(parent, name, mesh, core, out coreR);
                if (sheath != null) sheathT = Part(parent, name + " sheath", mesh, sheath, out sheathR);
            }

            static Transform Part(Transform parent, string name, Mesh mesh, Material material, out MeshRenderer r)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                r.enabled = false;
                return go.transform;
            }

            /// <summary>Stands it from <paramref name="foot"/> up <paramref name="height"/> m, each part at its radius and colour (HDR,
            /// light added: the colour is its brightness).</summary>
            public void Show(MaterialPropertyBlock block, int colorId, Vector3 foot, float height, float coreRadius, Color core, float sheathRadius, Color sheath)
            {
                Set(coreT, coreR, block, colorId, foot, height, coreRadius, core);
                if (sheathT != null) Set(sheathT, sheathR, block, colorId, foot, height, sheathRadius, sheath);
            }

            static void Set(Transform t, MeshRenderer r, MaterialPropertyBlock block, int colorId, Vector3 foot, float height, float radius, Color c)
            {
                bool on = height > 0.01f && radius > 0f && c.r + c.g + c.b > 0.003f;
                r.enabled = on;
                if (!on) return;
                t.SetPositionAndRotation(foot, Quaternion.identity);
                t.localScale = new Vector3(radius, height, radius);
                block.SetColor(colorId, c);
                r.SetPropertyBlock(block);
            }

            public void Hide()
            {
                if (coreR != null) coreR.enabled = false;
                if (sheathR != null) sheathR.enabled = false;
            }

            public void Destroy()
            {
                if (coreT != null) Object.Destroy(coreT.gameObject);
                if (sheathT != null) Object.Destroy(sheathT.gameObject);
            }
        }

        // ---------------------------------------------------------------- N8 light posts, §10's pattern pillars

        /// <summary>
        /// N8, a light post (the spec's §5): a core of radius 0.03 m, TealPale ×3.2, and (with <paramref name="sheath"/>) a sheath of
        /// radius 0.12 m, Teal ×1.0, both × <paramref name="hdr"/> within the caps' ladder (lines ×3.5, wide shapes ×1.4). It grows
        /// from <paramref name="foot"/> (as given: on the floor) to <paramref name="height"/> m over <paramref name="rise"/> s, holds
        /// <paramref name="hold"/> s and fades over <paramref name="fade"/> s (show time); half near a live warning, faded near
        /// the camera. Its own life: no show owns it.
        /// </summary>
        void LightPost(Vector3 foot, float height, float rise, float hold, float fade, bool sheath = true, float hdr = 1f)
        {
            StgBuild();
            anims.Add(new StgPostAnim(this, null, foot, false, height, rise, hold, fade, 0f, 0.03f, sheath ? 0.12f : 0f,
                HdrA(TealPale, Mathf.Min(3.5f, 3.2f * hdr)), HdrA(Teal, Mathf.Min(1.4f, 1f * hdr))) { tick = UltTick.Show });
        }

        /// <summary>
        /// §10's pattern pillars: 8 posts on the piece's own pattern on the board round Cell(<paramref name="centre"/>) - the
        /// knight's eight L-squares, the bishop's X (2 cells along each diagonal), the rook's + (2 cells along each line), the
        /// queen's one cell 2 out in each of the 8 directions (the pawn's queen, and any other piece, the queen's). Core radius
        /// 0.06 m TealPale ×3.2, sheath 0.35 m Teal ×1.1 (× <paramref name="hdr"/>, within the ladder), <paramref name="height"/> m
        /// tall (!!: 14; !: 8 at 0.7); each rises in 0.15 s, 0.03 s after the one before, and fades over 1 s (show time). Each
        /// post finds its floor when it starts (cells more than 1.5 m off the centre's keep its height). They go with the show.
        /// </summary>
        void PatternPillars(UltShow u, Vector3 centre, PieceKind kind, float height = 14f, float hdr = 1f)
        {
            StgBuild();
            var cells = kind == PieceKind.Knight ? KnightSquares : kind == PieceKind.Bishop ? StgBishopCells : kind == PieceKind.Rook ? StgRookCells : StgQueenCells;
            Vector2Int home = Cell(centre);
            Color core = HdrA(TealPale, Mathf.Min(3.5f, 3.2f * hdr)), sheath = HdrA(Teal, Mathf.Min(1.4f, 1.1f * hdr));
            for (int i = 0; i < cells.Length; i++)
            {
                Vector3 foot = CellFlat(home + cells[i]);
                foot.y = centre.y;
                anims.Add(new StgPostAnim(this, u, foot, true, height, 0.15f, 0f, 1f, i * 0.03f, 0.06f, 0.35f, core, sheath) { tick = UltTick.Show });
            }
        }

        sealed class StgPostAnim : StgAnim
        {
            readonly UltShow u;
            readonly StgRod rod;
            readonly float height, rise, hold, fade, delay, coreRadius, sheathRadius;
            readonly Color core, sheath;
            readonly bool owned;
            Vector3 foot;
            bool probe;

            public StgPostAnim(PawnRushSkillFx fx, UltShow u, Vector3 foot, bool probe, float height, float rise, float hold, float fade, float delay,
                float coreRadius, float sheathRadius, Color core, Color sheath) : base(fx)
            {
                this.u = u;
                owned = u != null;
                this.foot = foot;
                this.probe = probe;
                this.height = height;
                this.rise = Mathf.Max(0f, rise);
                this.hold = Mathf.Max(0f, hold);
                this.fade = Mathf.Max(0.01f, fade);
                this.delay = delay;
                this.coreRadius = coreRadius;
                this.sheathRadius = sheathRadius;
                this.core = core;
                this.sheath = sheath;
                rod = new StgRod(fx.root, "Ult light post", fx.meshCylinder, fx.stgMatCore, sheathRadius > 0f ? fx.stgMatSheath : null);
            }

            protected override bool Run(float dt, Camera cam)
            {
                if (owned && !fx.StgLive(u)) return false;
                float a = age - delay;
                if (a < 0f)
                {
                    rod.Hide();
                    return true;
                }
                if (probe)
                {
                    probe = false;
                    float y = StgFloorY(foot + Vector3.up * 1.5f, 3f);
                    if (!float.IsNaN(y)) foot.y = y;
                }
                if (a >= rise + hold + fade) return false;
                float grow = rise > 0f ? 1f - Mathf.Pow(1f - Mathf.Clamp01(a / rise), 3f) : 1f;
                float k = a <= rise + hold ? 1f : 1f - Smooth01((a - rise - hold) / fade);
                float tall = height * grow;
                Vector3 top = foot + Vector3.up * tall;
                k *= fx.StgWarnK(foot);
                rod.Show(fx.stgBlock, fx.stgColorId, foot, tall, coreRadius, core * (k * StgNearK(cam, foot, top, false)), sheathRadius, sheath * (k * StgNearK(cam, foot, top, true)));
                return true;
            }

            protected override void Release() => rod.Destroy();
        }

        // ---------------------------------------------------------------- N9 sky beams

        readonly List<StgBeamAnim> stgBeams = new List<StgBeamAnim>();

        /// <summary>
        /// N9, a sky beam (the spec's §5; it replaces UltRedBeam) on <paramref name="at"/> (a struck piece; its floor looked for
        /// under it): a core of radius 0.10 m, <paramref name="colour"/> halfway to white ×3.0 (Blood's is (1, 0.55, 0.6)), in a
        /// sheath of radius 0.45 m, <paramref name="colour"/> ×1.1; it falls from 60 m in 0.08 s, holds 0.2 s and fades over 0.5 s
        /// (<paramref name="life"/> in all, show time). In <paramref name="warning"/> mode it stands from the start and fades in
        /// to α 0.6 over <paramref name="warnSeconds"/>, the core at ×2.0, held until <paramref name="life"/> (if longer) and gone
        /// in 0.15 s (game time: a gameplay mark). Red ones draw after teal; eight at most (the oldest goes).
        /// </summary>
        void SkyBeam(Vector3 at, Color colour, float life = 0.78f, bool warning = false, float warnSeconds = 0f)
        {
            StgBuild();
            for (int i = stgBeams.Count - 1; i >= 0; i--) if (stgBeams[i].Over) stgBeams.RemoveAt(i);
            while (stgBeams.Count >= 8)
            {
                stgBeams[0].Stop();
                stgBeams.RemoveAt(0);
            }
            float floor = StgFloorY(at + Vector3.up * 0.5f, 6f);
            var foot = new Vector3(at.x, float.IsNaN(floor) ? at.y - 1f : floor, at.z);
            var beam = new StgBeamAnim(this, foot, colour, Mathf.Max(0.1f, life), warning, Mathf.Max(0f, warnSeconds)) { tick = warning ? UltTick.Game : UltTick.Show };
            stgBeams.Add(beam);
            anims.Add(beam);
        }

        sealed class StgBeamAnim : StgAnim
        {
            const float Sky = 60f;
            readonly StgRod rod;
            readonly Vector3 foot;
            readonly Color core, sheath;
            readonly float life, warnSeconds;
            readonly bool warning;

            public StgBeamAnim(PawnRushSkillFx fx, Vector3 foot, Color colour, float life, bool warning, float warnSeconds) : base(fx)
            {
                this.foot = foot;
                this.life = life;
                this.warning = warning;
                this.warnSeconds = warnSeconds;
                colour.a = 1f;
                core = Color.Lerp(colour, Color.white, 0.5f);
                sheath = colour;
                bool red = colour.r > colour.g;
                rod = new StgRod(fx.root, "Ult sky beam", fx.meshCylinder, red ? fx.stgMatCoreRed : fx.stgMatCore, red ? fx.stgMatSheathRed : fx.stgMatSheath);
            }

            protected override bool Run(float dt, Camera cam)
            {
                float a = age, bottom = foot.y, k, coreK, widen = 1f;
                if (warning)
                {
                    float hold = Mathf.Max(life, warnSeconds);
                    if (a >= hold + 0.15f) return false;
                    k = 0.6f * (warnSeconds > 0f ? Smooth01(a / warnSeconds) : 1f) * (a > hold ? 1f - (a - hold) / 0.15f : 1f);
                    coreK = 2f;
                }
                else
                {
                    if (a >= life) return false;
                    float fadeFrom = Mathf.Min(0.28f, life * 0.36f), f = Smooth01((a - fadeFrom) / Mathf.Max(0.05f, life - fadeFrom));
                    bottom = Mathf.Lerp(foot.y + Sky, foot.y, Mathf.Clamp01(a / 0.08f));
                    k = 1f - f;
                    coreK = 3f;
                    widen = 1f + 0.35f * f;
                }
                var from = new Vector3(foot.x, bottom, foot.z);
                var top = new Vector3(foot.x, foot.y + Sky, foot.z);
                rod.Show(fx.stgBlock, fx.stgColorId, from, top.y - bottom, 0.10f, HdrA(core, coreK) * (k * StgNearK(cam, from, top, false)),
                    0.45f * widen, HdrA(sheath, 1.1f) * (k * StgNearK(cam, from, top, true)));
                return true;
            }

            protected override void Release() => rod.Destroy();
        }

        // ---------------------------------------------------------------- N10 the comet trail

        /// <summary>
        /// N10, the comet trail (the spec's §5; it replaces the single 0.6 m and 0.9 m trails): two ribbons from 1 m behind the
        /// caster's hips (× a giant's scale) the way it moves - a core 0.10 m wide, TealPale ×3.0, 0.35 s long, and a halo 0.45 m
        /// wide, Teal ×1.0, 0.30 s - while the show plays (teal rules of the caps' §8), fading out after. Call at the cast or at
        /// its first move.
        /// </summary>
        void UltTrail(UltShow u)
        {
            if (u == null || u.by == null) return;
            StgBuild();
            var s = StgOf(u);
            if (s.trail != null && !s.trail.Over) return;
            s.trail = new StgTrailAnim(this, u) { tick = UltTick.Real };
            anims.Add(s.trail);
        }

        sealed class StgTrailAnim : StgAnim
        {
            readonly UltShow u;
            readonly TrailRenderer core, halo;
            Vector3 last, back = Vector3.back;
            bool started;
            float endedAt = -1f;

            public StgTrailAnim(PawnRushSkillFx fx, UltShow u) : base(fx)
            {
                this.u = u;
                core = Make(fx, "Ult trail", 0.10f, 0.35f);
                halo = Make(fx, "Ult trail halo", 0.45f, 0.30f);
            }

            static TrailRenderer Make(PawnRushSkillFx fx, string name, float width, float time)
            {
                var go = new GameObject(name);
                go.transform.SetParent(fx.root, false);
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = fx.matTrail;
                tr.time = time;
                tr.minVertexDistance = 0.04f;
                tr.widthMultiplier = width;
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                tr.numCapVertices = 4;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
                tr.textureMode = LineTextureMode.Stretch;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
                tr.colorGradient = g;
                tr.emitting = false;
                return tr;
            }

            protected override bool Run(float dt, Camera cam)
            {
                bool live = fx.StgLive(u) && u.by != null;
                if (!live) return false;
                if (u.Playing && endedAt < 0f)
                {
                    Vector3 hips = StgHips(u.by);
                    if (!started)
                    {
                        last = hips;
                        Vector3 face = Flat(u.by.Facing);
                        if (face.sqrMagnitude > 1e-4f) back = -face.normalized;
                    }
                    Vector3 move = hips - last;
                    last = hips;
                    if (dt > 0f && move.sqrMagnitude > 1.5f * 1.5f * dt * dt) back = Vector3.Slerp(back, -move.normalized, 1f - Mathf.Exp(-10f * dt)).normalized;
                    Vector3 p = hips + back * Mathf.Max(1f, fx.GiantScaleOf(u.by));
                    core.transform.position = p;
                    halo.transform.position = p;
                    if (!started)
                    {
                        core.Clear();
                        halo.Clear();
                        started = true;
                    }
                    core.emitting = halo.emitting = true;
                    float tk = fx.StgTealK(u, hips);
                    var block = fx.stgBlock;
                    block.SetColor(fx.stgColorId, HdrA(TealPale, 3f * tk));
                    core.SetPropertyBlock(block);
                    block.SetColor(fx.stgColorId, HdrA(Teal, tk));
                    halo.SetPropertyBlock(block);
                    return true;
                }
                if (endedAt < 0f)
                {
                    endedAt = fx.RealClock;
                    core.emitting = halo.emitting = false;
                }
                return fx.RealClock - endedAt < 0.45f;
            }

            protected override void Release()
            {
                if (core != null) Object.Destroy(core.gameObject);
                if (halo != null) Object.Destroy(halo.gameObject);
            }
        }

        // ---------------------------------------------------------------- N14 the cage

        /// <summary>
        /// N14, the cage (the spec's §5: the aura while driving, in the aurora's place after the key), on every screen from the
        /// end of the key (1.10 s real after it) while the show plays: four light posts - lines only, radius 0.03 m - at the
        /// corners of a 2.2 m square round the caster on the world's axes, following it, 2.4 m tall, and two orbit ribbons
        /// (0.10 m bands in matAurora, rim 0.9: clear in front of the caster), radius 0.95 m at the hips and 1.25 m at the head
        /// + 0.2 m, tilted ±12°, turning +90°/s and −60°/s on the show clock, Teal ×2.6; all × a giant's scale. At the judgement
        /// the posts sink over 0.3 s; a show over without one folds in 0.5 s. Call at the cast, once the show is in ults.
        /// </summary>
        void UltCage(UltShow u)
        {
            if (u == null || u.by == null) return;
            StgBuild();
            BuildPrevizParts();
            var s = StgOf(u);
            if (s.cage != null && !s.cage.Over) return;
            s.cage = new StgCageAnim(this, u) { tick = UltTick.Real };
            anims.Add(s.cage);
        }

        sealed class StgCageAnim : StgAnim
        {
            readonly UltShow u;
            readonly StgRod[] posts = new StgRod[4];
            readonly Transform[] bands = new Transform[2];
            readonly MeshRenderer[] bandR = new MeshRenderer[2];

            public StgCageAnim(PawnRushSkillFx fx, UltShow u) : base(fx)
            {
                this.u = u;
                for (int i = 0; i < posts.Length; i++) posts[i] = new StgRod(fx.root, "Ult cage post", fx.meshCylinder, fx.stgMatCore, null);
                for (int i = 0; i < bands.Length; i++)
                {
                    var go = new GameObject("Ult cage ribbon");
                    go.transform.SetParent(fx.root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = fx.meshCylinder;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = fx.matAurora;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.enabled = false;
                    bands[i] = go.transform;
                    bandR[i] = r;
                }
            }

            protected override bool Run(float dt, Camera cam)
            {
                if (!fx.StgLive(u) || u.by == null) return false;
                float on = Smooth01((fx.RealClock - u.t0Real - 1.10f) / 0.25f), sink = 1f;
                if (u.hitReal >= 0f)
                {
                    float j = fx.RealClock - u.hitReal;
                    if (j >= 0.3f) return false;
                    sink = 1f - Smooth01(j / 0.3f);
                }
                else if (u.endReal >= 0f)
                {
                    float e = fx.RealClock - u.endReal;
                    if (e >= 0.5f) return false;
                    on *= 1f - e / 0.5f;
                }
                if (on <= 0.001f)
                {
                    Hide();
                    return true;
                }
                var by = u.by;
                var s = fx.StgOf(u);
                float g = Mathf.Max(1f, fx.GiantScaleOf(by));
                Vector3 hips = StgHips(by), head = StgHead(by);
                var feet = new Vector3(hips.x, fx.StgFeetY(u, s), hips.z);
                if (g > 1.01f)
                {
                    hips = feet + (hips - feet) * g;
                    head = feet + (head - feet) * g;
                }
                float half = 1.1f * g, tall = 2.4f * g * sink, k = on * fx.StgTealK(u, feet);
                Color line = HdrA(TealPale, 3.2f);
                for (int i = 0; i < posts.Length; i++)
                {
                    Vector3 foot = feet + new Vector3(i % 2 == 0 ? -half : half, 0f, i < 2 ? -half : half);
                    posts[i].Show(fx.stgBlock, fx.stgColorId, foot, tall, 0.03f, line * (k * StgNearK(cam, foot, foot + Vector3.up * tall, false)), 0f, Color.clear);
                }
                float turn = fx.ShowClock;
                Band(0, hips, 0.95f * g, 12f, 90f * turn, k * sink, cam);
                Band(1, head + Vector3.up * (0.2f * g), 1.25f * g, -12f, -60f * turn, k * sink, cam);
                return true;
            }

            /// <summary>One orbit ribbon (near the camera faded by the caps' rule, its nearest edge counted).</summary>
            void Band(int i, Vector3 centre, float radius, float tilt, float turn, float k, Camera cam)
            {
                if (cam != null) k *= Mathf.Clamp01((Mathf.Max(0f, Vector3.Distance(cam.transform.position, centre) - radius) - 1.2f) / 2.5f);
                var r = bandR[i];
                r.enabled = k > 0.001f;
                if (!r.enabled) return;
                Quaternion rot = Quaternion.Euler(0f, turn, 0f) * Quaternion.Euler(tilt, 0f, 0f);
                bands[i].SetPositionAndRotation(centre + rot * new Vector3(0f, -0.05f, 0f), rot);
                bands[i].localScale = new Vector3(radius, 0.10f, radius);
                fx.stgBlock.SetColor(fx.stgColorId, HdrA(Teal, 2.6f) * k);
                r.SetPropertyBlock(fx.stgBlock);
            }

            void Hide()
            {
                for (int i = 0; i < posts.Length; i++) posts[i].Hide();
                for (int i = 0; i < bandR.Length; i++) if (bandR[i] != null) bandR[i].enabled = false;
            }

            protected override void Release()
            {
                for (int i = 0; i < posts.Length; i++) posts[i].Destroy();
                for (int i = 0; i < bands.Length; i++) if (bands[i] != null) Object.Destroy(bands[i].gameObject);
            }
        }

        // ---------------------------------------------------------------- N15 the giant avatar

        /// <summary>
        /// N15, the giant avatar (the spec's §5), on the caster's screen only: the caster's skin baked once (BakeMesh) and drawn
        /// each frame with Graphics.RenderMesh for this camera, <paramref name="height"/> m tall (9; the queen 12), 4.5 m beyond
        /// the caster along this camera's flat forward (so never between the camera and it), as it stood at the bake - in
        /// matGiantRim (rim only, the middle clear), Teal ×1.4, or in black ink while ImpactGiantInk holds. Scale 0.92 → 1.0,
        /// rising 0.8 m, in over 0.15 s, dissolving upward over its last 0.4 s (0.65-1.05 s real); <paramref name="life"/> 1.05 s
        /// at most. Rendering layer 5 on it, no shadows. Again within the show: shown again (the bake kept). Call at the key.
        /// </summary>
        void UltGiantAvatar(UltShow u, float height, float life = 1.05f)
        {
            var a = StgAvatarOf(u);
            if (a == null || (!a.Baked && !a.Bake())) return;
            a.Show(height, life);
        }

        /// <summary>The caster bakes again for its avatar (the pawn's queen: once u.by.Piece is Queen after PrLeap); one showing
        /// goes on with the new body. The caster's screen only.</summary>
        void UltGiantRebake(UltShow u) => StgAvatarOf(u)?.Bake();

        StgAvatarAnim StgAvatarOf(UltShow u)
        {
            if (u == null || u.by == null || Viewer != u.by || !StgLive(u)) return null;
            StgBuild();
            var s = StgOf(u);
            if (s.avatar == null || s.avatar.Over)
            {
                s.avatar = new StgAvatarAnim(this, u) { tick = UltTick.Real };
                anims.Add(s.avatar);
            }
            return s.avatar;
        }

        sealed class StgAvatarAnim : StgAnim
        {
            readonly UltShow u;
            readonly RagdollPawn by;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            Mesh mesh;
            Color32[] colors;
            float[] heights;
            Matrix4x4 canon = Matrix4x4.identity;
            float tall = 1f, showAt = -1f, life = 1.05f, height = 9f, front = float.NaN;

            public StgAvatarAnim(PawnRushSkillFx fx, UltShow u) : base(fx)
            {
                this.u = u;
                by = u.by;
            }

            public bool Baked => mesh != null && heights != null;

            /// <summary>The caster's skin as it is now, baked into the avatar's own mesh: its pose kept as it stood, its foot at the
            /// origin, each vertex's height (0 foot .. 1 top) kept for the dissolve.</summary>
            public bool Bake()
            {
                var skin = by != null ? by.skin : null;
                if (skin == null) return false;
                if (mesh == null)
                {
                    mesh = new Mesh { name = "Ult giant avatar", hideFlags = HideFlags.HideAndDontSave };
                    mesh.MarkDynamic();
                }
                skin.BakeMesh(mesh, true);
                var verts = fx.stgVerts;
                mesh.GetVertices(verts);
                int n = verts.Count;
                if (n == 0) return false;
                Quaternion rot = skin.transform.rotation;
                if (heights == null || heights.Length != n) heights = new float[n];
                if (colors == null || colors.Length != n) colors = new Color32[n];
                float lo = float.MaxValue, hi = float.MinValue, x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                for (int i = 0; i < n; i++)
                {
                    Vector3 w = rot * verts[i];
                    heights[i] = w.y;
                    lo = Mathf.Min(lo, w.y);
                    hi = Mathf.Max(hi, w.y);
                    x0 = Mathf.Min(x0, w.x);
                    x1 = Mathf.Max(x1, w.x);
                    z0 = Mathf.Min(z0, w.z);
                    z1 = Mathf.Max(z1, w.z);
                }
                verts.Clear();
                tall = Mathf.Max(0.2f, hi - lo);
                for (int i = 0; i < n; i++)
                {
                    heights[i] = (heights[i] - lo) / tall;
                    colors[i] = new Color32(255, 255, 255, 255);
                }
                mesh.SetColors(colors);
                mesh.RecalculateBounds();
                front = float.NaN;
                canon = Matrix4x4.Translate(-new Vector3((x0 + x1) * 0.5f, lo, (z0 + z1) * 0.5f)) * Matrix4x4.Rotate(rot);
                return true;
            }

            public void Show(float height, float life)
            {
                this.height = Mathf.Max(1f, height);
                this.life = Mathf.Clamp(life, 0.2f, 1.05f);
                showAt = fx.RealClock;
                Front(-1f);
            }

            /// <summary>The dissolve's front (0 foot .. 1 top): what is under it is gone, a 0.15 band soft over it.</summary>
            void Front(float to)
            {
                if (colors == null || heights == null || mesh == null) return;
                if (!float.IsNaN(front) && Mathf.Abs(to - front) < 0.004f) return;
                front = to;
                for (int i = 0; i < colors.Length; i++) colors[i].a = (byte)(255f * Mathf.Clamp01((heights[i] - to) / 0.15f));
                mesh.SetColors(colors);
            }

            protected override bool Run(float dt, Camera cam)
            {
                if (!fx.StgLive(u) || by == null || u.by != by) return false;
                if (showAt < 0f || mesh == null || cam == null) return true;
                float t = fx.RealClock - showAt;
                if (t >= life)
                {
                    showAt = -1f;
                    return true;
                }
                float k = Mathf.Clamp01(t / 0.15f), grow = Smooth01(t / life);
                float from = Mathf.Max(0.15f, life - 0.4f), f = Mathf.Clamp01((t - from) / Mathf.Max(0.05f, life - from));
                if (f > 0f) Front(Mathf.Lerp(-0.15f, 1f, f));
                Vector3 hips = StgHips(by);
                Vector3 feet = new Vector3(hips.x, fx.StgFeetY(u, fx.StgOf(u)), hips.z) + StgFlatForward(cam) * 4.5f + Vector3.up * (0.8f * grow);
                float scale = height / tall * Mathf.Lerp(0.92f, 1f, grow);
                Matrix4x4 m = Matrix4x4.TRS(feet, Quaternion.identity, Vector3.one * scale) * canon;
                bool ink = fx.ImpactGiantInk;
                block.SetColor(fx.stgColorId, ink ? new Color(0f, 0f, 0f, 1f) : HdrA(Teal, 1.4f) * k);
                block.SetFloat(fx.stgOpacityId, ink ? k : 0f);
                block.SetFloat(fx.stgDissolveId, f * 0.5f);
                var skin = by.skin;
                var rp = new RenderParams(ink ? fx.stgMatGiantInk : fx.stgMatGiantRim)
                {
                    layer = skin != null ? skin.gameObject.layer : 0,
                    renderingLayerMask = (skin != null ? skin.renderingLayerMask : 1u) | (1u << StgCasterBit),
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(feet + Vector3.up * (height * 0.5f), new Vector3(height, height * 1.2f, height)),
                    matProps = block,
                    camera = cam,
                };
                for (int i = 0; i < mesh.subMeshCount; i++) Graphics.RenderMesh(rp, mesh, i, m);
                return true;
            }

            protected override void Release()
            {
                if (mesh != null) Object.Destroy(mesh);
                mesh = null;
                heights = null;
                colors = null;
            }
        }

        // ---------------------------------------------------------------- N17 the lens streak

        /// <summary>N17, a lens streak (the spec's §5) on a blow or a judgement at <paramref name="at"/>: a flat line of light 6 m by
        /// 0.06 m facing the camera, TealPale ×3, 0.25 s (show time) - only when it is at least 12 m from this camera.</summary>
        void LensStreak(Vector3 at)
        {
            var cam = ViewCamera;
            if (cam == null || (cam.transform.position - at).sqrMagnitude < 12f * 12f) return;
            StgBuild();
            anims.Add(new StgStreakAnim(this, at) { tick = UltTick.Show });
        }

        sealed class StgStreakAnim : StgAnim
        {
            readonly Vector3 at;
            readonly Transform tf;
            readonly MeshRenderer mr;

            public StgStreakAnim(PawnRushSkillFx fx, Vector3 at) : base(fx)
            {
                this.at = at;
                var go = new GameObject("Ult lens streak");
                go.transform.SetParent(fx.root, false);
                go.AddComponent<MeshFilter>().sharedMesh = fx.meshQuad;
                mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = fx.stgMatStreak;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                tf = go.transform;
            }

            protected override bool Run(float dt, Camera cam)
            {
                const float life = 0.25f;
                if (age >= life || cam == null) return false;
                float t = age / life, k = age < 0.03f ? age / 0.03f : (1f - t) * (1f - t);
                tf.SetPositionAndRotation(at, cam.transform.rotation);
                tf.localScale = new Vector3(6f * (0.75f + 0.25f * t), 0.06f, 1f);
                fx.stgBlock.SetColor(fx.stgColorId, HdrA(TealPale, 3f) * k);
                mr.SetPropertyBlock(fx.stgBlock);
                mr.enabled = k > 0.001f;
                return true;
            }

            protected override void Release()
            {
                if (tf != null) Object.Destroy(tf.gameObject);
            }
        }

        // ================================================================ gone

        /// <summary>Everything of the stage gone at once: its animations and their lines, every pooled light, the tracked Flare()s
        /// let go, the skins' rendering layers put back, its materials and textures destroyed.</summary>
        void DestroyStage()
        {
            stgEpoch++;
            for (int i = anims.Count - 1; i >= 0; i--)
                if (anims[i] is StgAnim a)
                {
                    a.Destroy();
                    anims.RemoveAt(i);
                }
            for (int i = 0; i < stgTargets.Count; i++) StgTargetDark(stgTargets[i]);
            stgTargets.Clear();
            stgTargetsAnim = null;
            stgAim = null;
            stgBlow = null;
            stgHitLast = null;
            stgHitList.Clear();
            stgPools.Clear();
            stgBeams.Clear();
            foreach (var l in stgPool) if (l.light != null) Destroy(l.light.gameObject);
            stgPool.Clear();
            foreach (var f in stgFlares) f.light = null;
            stgFlares.Clear();
            stgFlareSpare.Clear();
            stgLive.Clear();
            foreach (var m in stgMarks.Values) StgMarkClear(m);
            stgMarks.Clear();
            foreach (var u in ults) if (u.stage is StgShow) u.stage = null;
            if (!stgBuilt) return;
            stgBuilt = false;
            foreach (var m in new[] { stgMatCore, stgMatSheath, stgMatCoreRed, stgMatSheathRed, stgMatStreak, stgMatGiantRim, stgMatGiantInk })
            {
                if (m == null) continue;
                materials.Remove(m);
                Destroy(m);
            }
            if (stgTexSoftSquare != null) Destroy(stgTexSoftSquare);
            if (stgTexRod != null) Destroy(stgTexRod);
            stgTexSoftSquare = stgTexRod = null;
        }
    }
}
