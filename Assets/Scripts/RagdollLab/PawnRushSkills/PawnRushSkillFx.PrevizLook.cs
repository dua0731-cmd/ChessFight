using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The ultimates' look as the previz has it (JY-skill-lab, 2026-11-22; the user, after the night-and-lightning version:
    /// "스킬들은 다 괜찮은데, 연출이 별로야. 그냥 이 프리비즈 만들때 연출 컨셉이랑 이펙트로 가자" - the previz 16, common cast with
    /// the knight and the queen). Daylight, the world's colour drained to grey but teal (the caster's move) and red (what it
    /// attacks); the caster's square bursting teal into a light curtain, the board's squares lighting in rings; a teal
    /// aurora - two curtains of light turning round the caster - for as long as it runs; the move and the skill's name in
    /// big teal letters; red light from the sky on whoever it hits; at a good judgement the floor breaks into floating
    /// tiles round it, eight teal pillars and "탁월한 수!!"; then the colour comes back slowly. (The grade itself is in
    /// PawnRushSkillFx.UltScreen.cs; the call's letters in its OnGUI.)
    /// </summary>
    public partial class PawnRushSkillFx
    {
        Material matAurora, matStone, matHole;

        void BuildPrevizParts()
        {
            if (matAurora != null) return;
            matAurora = Glow(texBeam, noise: 0.75f, noiseST: new Vector4(3f, 1.1f, 0.15f, -0.9f), rim: 0.9f, rimPower: 1.6f, soft: 0.1f);
            matHole = Glow(Texture2D.whiteTexture, opacity: 1f);
            // The floating tiles: a dark stone lit by the scene, its edges lit teal from within.
            matStone = ChessFight.Game.LitMaterials.NewLit(new Color(0.32f, 0.33f, 0.35f), 0.25f);
            matStone.hideFlags = HideFlags.HideAndDontSave;
            ChessFight.Game.LitMaterials.SetEmission(matStone, HdrA(Teal, 2.2f));
            if (matStone.HasProperty("_EmissionMap")) matStone.SetTexture("_EmissionMap", texFrame);
            materials.Add(matStone);
        }

        /// <summary>The cast's burst on the caster's square: a curtain of light on its edges rising and falling, the board's
        /// squares lighting in three rings round it.</summary>
        void UltPrevizCast(UltShow u, Vector3 feet)
        {
            BuildPrevizParts();
            Vector3 sq = Ground(feet);
            float size = FamSquare;
            Add(new Shape(root, "Ult curtain", meshBox, matCurtain)
            {
                life = 1.3f, at = sq, scale = new Vector3(size, 1f, size),
                animate = (s, t) =>
                {
                    float a = s.Age;
                    float h = a < 0.25f ? Mathf.Lerp(0.2f, 4.5f, Smooth01(a / 0.25f)) : Mathf.Lerp(4.5f, 1f, Smooth01((a - 0.25f) / 1f));
                    s.scale = new Vector3(size, h, size);
                    s.color = HdrA(Teal, 1.3f);
                    s.bright = 1f - Mathf.Clamp01((a - 0.5f) / 0.8f);
                },
            });
            for (int ring = 1; ring <= 3; ring++) UltBoardRing(sq, ring, (ring - 1) * 0.07f);
            Flare(sq + Vector3.up * 1.2f, TealPale, 2.5f, 7f, 0.5f);
        }

        /// <summary>The squares of ring <paramref name="ring"/> round a square light for a moment.</summary>
        void UltBoardRing(Vector3 at, int ring, float delay)
        {
            float size = FamSquare;
            for (int x = -ring; x <= ring; x++)
                for (int z = -ring; z <= ring; z++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
                    float far = new Vector2(x, z).magnitude, wait = delay + far * 0.015f;
                    Add(new Shape(root, "Ult board square", meshQuad, matFrame)
                    {
                        life = 0.9f + wait, at = at + new Vector3(x * size, 0.04f, z * size), rotation = FlatOnFloor(), scale = new Vector3(size * 0.96f, size * 0.96f, 1f),
                        animate = (s, t) =>
                        {
                            float a = s.Age - wait;
                            s.color = HdrA(Teal, 1.9f);
                            s.bright = a < 0f ? 0f : Pop(a, 0.04f) * Mathf.Pow(1f - Mathf.Clamp01(a / 0.9f), 1.6f) / (1f + 0.25f * ring);
                        },
                    });
                }
        }

        /// <summary>The aurora: two curtains of light turning round the caster (bright at their edges, clear in front of it), a
        /// teal light on it, teal flames and motes rising off its feet; strong at the key, softer as it drives, gone after the
        /// judgement.</summary>
        void UltAurora(UltShow u)
        {
            BuildPrevizParts();
            var by = u.by;
            for (int i = 0; i < 2; i++)
            {
                float r = i == 0 ? 0.75f : 1.05f, h = i == 0 ? 2.6f : 3.3f, spin = i == 0 ? 70f : -45f;
                Color c = i == 0 ? Teal : TealPale;
                Add(new Shape(root, "Ult aurora", meshCylinder, matAurora)
                {
                    life = 1e9f,
                    animate = (s, t) =>
                    {
                        float k = AuroraK(u);
                        if (k <= 0f || by == null) { s.life = 0f; return; }
                        float a = Clock - u.t0, grow = Smooth01(a / 0.35f), burst = 1f + 0.8f * Mathf.Exp(-a / 0.4f);
                        float sc = GiantScaleOf(by);
                        s.at = FloorUnder(by) + Vector3.up * 0.02f;
                        s.rotation = Quaternion.Euler(0f, a * spin, 0f);
                        s.scale = new Vector3(r * sc, h * grow * sc, r * sc);
                        s.color = HdrA(c, 1.7f);
                        s.bright = k * burst * (0.85f + 0.15f * Mathf.Sin(Clock * 6f + i));
                    },
                });
            }
            var light = new GameObject("Ult light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.color = Teal;
            light.range = 8f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            anims.Add(new Ongoing((age, dt) =>
            {
                float k = AuroraK(u);
                if (k <= 0f || by == null) return false;
                light.transform.position = ChestOf(by) + Vector3.up * 0.9f;
                light.intensity = k * (1.6f + 1.4f * Mathf.Exp(-(Clock - u.t0) / 0.3f));
                if (dt <= 0f) return true;
                Vector3 feet = FloorUnder(by);
                if (Random.value < 0.5f * k)
                {
                    var off = Random.insideUnitCircle * 0.55f;
                    smoke.Emit(feet + new Vector3(off.x, 0.2f, off.y), Vector3.up * Random.Range(1.4f, 2.4f), Random.Range(0.5f, 0.9f), Random.Range(0.6f, 0.9f), Tint(Teal, Random.value * 0.3f));
                }
                var m = Random.insideUnitCircle * 1.1f;
                motes.Emit(feet + new Vector3(m.x, 0.1f, m.y), new Vector3(0f, Random.Range(1f, 2.6f), 0f), Random.Range(0.06f, 0.12f), Random.Range(0.7f, 1.3f), Tint(Random.value < 0.5f ? Teal : TealPale, 0.2f));
                return true;
            }, () => { if (light != null) Object.Destroy(light.gameObject); }));
            // A teal trail behind it.
            anims.Add(new Streak(root, matTrail, HdrA(Teal, 2.2f), 0.6f, 0.45f, () => by != null && u.Playing ? by.Hips.position : (Vector3?)null));
        }

        /// <summary>How strong the aurora is: full at the key, 0.7 while it drives, gone 0.6 s after the judgement or the end.</summary>
        float AuroraK(UltShow u)
        {
            float k = u.Life(Clock) * (u.hitAt >= 0f ? Mathf.Clamp01(1f - (Clock - u.hitAt) / 0.6f) : 1f);
            if (u.Driven) k *= Mathf.Lerp(1f, 0.7f, Smooth01((Clock - u.t0 - 1f) / 1f));
            return k;
        }

        /// <summary>The floor round a blow breaks into tiles that rise and float, edges lit teal, the holes dark under them,
        /// and settle back.</summary>
        void UltShatter(Vector3 at, float radius)
        {
            BuildPrevizParts();
            var mesh = meshCube;
            if (mesh == null) return;
            float size = FamSquare;
            int n = Mathf.CeilToInt(radius / size);
            for (int x = -n; x <= n; x++)
                for (int z = -n; z <= n; z++)
                {
                    if (x == 0 && z == 0) continue;
                    Vector3 c = at + new Vector3(x * size, 0f, z * size);
                    float far = Flat(c - at).magnitude;
                    if (far > radius || Random.value < 0.15f + far / radius * 0.35f) continue;
                    var floor = Ground(c);
                    if (Mathf.Abs(floor.y - at.y) > 1.5f) continue;
                    float wave = far / 18f, rise = Random.Range(1.2f, 4.8f) * (1f - far / (radius * 1.6f)), spin = Random.Range(-40f, 40f);
                    var axis = Random.onUnitSphere;
                    float stay = Random.Range(1.6f, 2.4f);
                    var go = new GameObject("Ult floating tile");
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = matStone;
                    var tf = go.transform;
                    Vector3 home = floor + Vector3.up * 0.12f;
                    tf.position = home;
                    tf.localScale = Vector3.zero;
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
                            lift = rise * (1f - Smooth01(f));
                            tilt = spin * (1f - Smooth01(f));
                            k = 1f - Smooth01((f - 0.5f) * 2f);
                        }
                        tf.SetPositionAndRotation(home + Vector3.up * lift, Quaternion.AngleAxis(tilt, axis));
                        tf.localScale = new Vector3(size * 0.94f * k, 0.24f * k, size * 0.94f * k);
                        return true;
                    }, () => { if (go != null) Object.Destroy(go); }));
                    Add(new Shape(root, "Ult hole", meshQuad, matHole)
                    {
                        life = wave + 0.25f + stay + 0.9f, at = floor + Vector3.up * 0.02f, rotation = FlatOnFloor(), scale = new Vector3(size * 0.94f, size * 0.94f, 1f),
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
    }
}
