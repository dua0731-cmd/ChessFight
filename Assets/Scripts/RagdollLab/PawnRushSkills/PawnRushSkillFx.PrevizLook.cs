using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The key's burst, as the previz has it (JY-skill-lab, 2026-11-22; the user: "스킬들은 다 괜찮은데, 연출이 별로야. 그냥 이
    /// 프리비즈 만들때 연출 컨셉이랑 이펙트로 가자", then: "내가 원하는 연출 컨셉은 이번에 가까운데, 더 강력해보이는건 이전이였어.
    /// 근데 그건 너무 이펙트가 강해서 화면이 안보이는 경우도 있었지"). Now the burst of the key only, thin and short, so the screen
    /// stays readable: a curtain of teal light rising on the from-square's edges and sinking; the aurora's two rim-only
    /// curtains turning round the caster for a moment with lightning crawling over it; then the cage (the stage file) carries
    /// the aura while it drives. At a good judgement the floor round it breaks into floating tiles, their edges flickering
    /// teal. Decor: it runs on the show clock (the online picture slow slows it, nothing it times is gameplay).
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

        /// <summary>The key's curtain on the from-square (the board cell the caster stands on): teal walls on its edges rising
        /// from 0.2 to 3.2 m in 0.25 s and sinking to 1 m over 0.8 s, a bright line along their top, fading from 0.5 s (life
        /// 1.1 s on the show clock). The board itself unfolds round it (PawnRushSkillFx.Brilliant.cs).</summary>
        void UltPrevizCast(UltShow u, Vector3 feet)
        {
            BuildPrevizParts();
            Vector2Int cell = Cell(feet);
            Vector3 sq = CellFlat(cell);
            sq.y = Ground(feet).y;
            float size = FamSquare;
            float Height(float a) => a < 0.25f ? Mathf.Lerp(0.2f, 3.2f, Smooth01(a / 0.25f)) : Mathf.Lerp(3.2f, 1f, Smooth01((a - 0.25f) / 0.8f));
            float Fade(float a) => 1f - Mathf.Clamp01((a - 0.5f) / 0.6f);
            anims.Add(new Shape(root, "Ult curtain", meshBox, matCurtain)
            {
                tick = UltTick.Show, life = 1.1f, at = sq, scale = new Vector3(size, 1f, size),
                animate = (s, t) =>
                {
                    s.scale = new Vector3(size, Height(s.Age), size);
                    s.color = HdrA(Teal, 0.9f);
                    s.bright = Fade(s.Age);
                },
            });
            anims.Add(new Shape(root, "Ult curtain top", meshQuad, matFrame)
            {
                tick = UltTick.Show, life = 1.1f, rotation = FlatOnFloor(), scale = new Vector3(size, size, 1f),
                animate = (s, t) =>
                {
                    s.at = sq + Vector3.up * Height(s.Age);
                    s.color = HdrA(TealPale, 3.5f);
                    s.bright = Fade(s.Age);
                },
            });
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

        /// <summary>The key's aurora burst: the previz's two rim-only curtains (0.75 and 1.05 m) turning round the caster at
        /// x1.8 until 0.6 s, gone by 1.2 s; lightning crawling over it for the first 0.6 s (one arc every 0.03 s, at most two
        /// alive); teal motes off its feet. All on the show clock. No light of its own (the stage's key spot and rim light it),
        /// no smoke, no trail (the stage's two-layer trail follows it).</summary>
        void UltAurora(UltShow u)
        {
            BuildPrevizParts();
            var by = u.by;
            for (int i = 0; i < 2; i++)
            {
                float r = i == 0 ? 0.75f : 1.05f, h = i == 0 ? 2.6f : 3.3f, spin = i == 0 ? 70f : -45f;
                Color c = i == 0 ? Teal : TealPale;
                int ring = i;
                anims.Add(new Shape(root, "Ult aurora", meshCylinder, matAurora)
                {
                    tick = UltTick.Show, life = 1.2f,
                    animate = (s, t) =>
                    {
                        if (by == null) { s.bright = 0f; return; }
                        float a = s.Age, grow = Smooth01(a / 0.2f), sc = GiantScaleOf(by);
                        s.at = FloorUnder(by) + Vector3.up * 0.02f;
                        s.rotation = Quaternion.Euler(0f, a * spin, 0f);
                        s.scale = new Vector3(r * sc, h * grow * sc, r * sc);
                        s.color = HdrA(c, 1.8f);
                        s.bright = (1f - Smooth01((a - 0.6f) / 0.6f)) * (0.85f + 0.15f * Mathf.Sin(a * 6f + ring));
                    },
                });
            }
            float nextArc = 0f;
            var arcsAt = new float[2];
            anims.Add(new Ongoing((age, dt) =>
            {
                if (by == null || age > 0.6f) return false;
                if (dt <= 0f) return true;
                nextArc -= dt;
                if (nextArc <= 0f && arc != null)
                {
                    nextArc = 0.03f;
                    // At most two arcs alive on it (each lives 0.06 s).
                    int slot = arcsAt[0] <= arcsAt[1] ? 0 : 1;
                    if (age - arcsAt[slot] >= 0.06f || arcsAt[slot] == 0f)
                    {
                        arcsAt[slot] = Mathf.Max(age, 1e-4f);
                        Vector3 a = Part(by, AuraParts[Random.Range(0, AuraParts.Length)]) + Random.insideUnitSphere * 0.05f;
                        Vector3 b = Part(by, AuraParts[Random.Range(0, AuraParts.Length)]) + Random.insideUnitSphere * 0.25f;
                        if ((b - a).sqrMagnitude < 0.02f) b = a + Random.onUnitSphere * 0.35f;
                        arc.Strike(a, b, HdrA(Electric, 2.2f), Random.Range(0.05f, 0.08f), 0.06f, 1f, 0.3f, 1);
                    }
                }
                if (Random.value < 0.6f)
                {
                    var m = Random.insideUnitCircle * 1.1f;
                    motes.Emit(FloorUnder(by) + new Vector3(m.x, 0.1f, m.y), new Vector3(0f, Random.Range(1f, 2.6f), 0f), 0.05f, Random.Range(0.7f, 1.3f), Tint(Random.value < 0.5f ? Teal : TealPale, 0.2f));
                }
                return true;
            }) { tick = UltTick.Show });
        }

        /// <summary>The floor round a blow breaks into tiles that rise and float, edges lit teal, the holes dark under them,
        /// and settle back (on the show clock). With <paramref name="ember"/> each tile's edges flicker x1.4-2.0 at 7-11 Hz
        /// (the burning board, in the brilliant colour).</summary>
        void UltShatter(Vector3 at, float radius, bool ember = false)
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
                    MaterialPropertyBlock glow = ember ? new MaterialPropertyBlock() : null;
                    float hz = Random.Range(7f, 11f), phase = Random.Range(0f, Mathf.PI * 2f);
                    anims.Add(new Ongoing((age, dt) =>
                    {
                        float a = age - wave;
                        if (a < 0f) { tf.localScale = Vector3.zero; return true; }
                        if (glow != null)
                        {
                            glow.SetColor("_EmissionColor", HdrA(Teal, 2.2f * Mathf.Lerp(1.4f, 2f, 0.5f + 0.5f * Mathf.Sin(age * hz * Mathf.PI * 2f + phase)) / 1.7f));
                            mr.SetPropertyBlock(glow);
                        }
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
                    }, () => { if (go != null) Object.Destroy(go); }) { tick = UltTick.Show });
                    anims.Add(new Shape(root, "Ult hole", meshQuad, matHole)
                    {
                        tick = UltTick.Show, life = wave + 0.25f + stay + 0.9f, at = floor + Vector3.up * 0.02f, rotation = FlatOnFloor(), scale = new Vector3(size * 0.94f, size * 0.94f, 1f),
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
