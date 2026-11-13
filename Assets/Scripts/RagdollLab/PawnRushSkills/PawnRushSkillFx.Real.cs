using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 10-25 (승규 님: "소드파이트에서 실사 그래픽 봤지? 그 느낌으로 폰 러쉬 수정 할건데 퀸부터"): Pawn Rush's skills drawn for real,
    /// as the Sword Fight skills were the same day - no ink, no gold, no rings or pearls, no chess pieces flying in, no
    /// electricity, no squash: what the move would really show, with a little of a film camera's look (a flash and a lens
    /// streak at the one bright moment). The pieces drawn this way so far are in <see cref="Real"/>; the others keep
    /// design A until their turn.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>The pieces whose skill is drawn for real (10-25: the queen first, then the rook's aim and, 10-26, the bishop's aim - only their aims so far).</summary>
        public static bool Real(PieceKind kind) => kind == PieceKind.Queen || kind == PieceKind.Rook || kind == PieceKind.Bishop;

        /// <summary>The faint light a preview lies on the floor in (Sword Fight's light lines): warm white.</summary>
        static readonly Color LightLine = new Color(1f, 0.96f, 0.88f);

        Material realFlashMat, realChipMat;
        Texture2D realGlowTex;

        void DestroyReal()
        {
            if (realFlashMat != null) Destroy(realFlashMat);
            if (realChipMat != null) Destroy(realChipMat);
            if (realGlowTex != null) Destroy(realGlowTex);
            if (ghostRealMat != null) Destroy(ghostRealMat);
        }

        // The queen: PawnRushSkillFx.RealQueen.cs.

        // ---------------------------------------------------------------- shared real parts

        /// <summary>The colour of a piece's body (its model's material), for the chips knocked off it.</summary>
        static Color BodyColor(RagdollPawn pawn)
        {
            var skin = pawn != null ? pawn.skin : null;
            if (skin == null && pawn != null) skin = pawn.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null && skin.sharedMaterial != null && skin.sharedMaterial.HasProperty(LitMaterials.BaseColorId))
                return skin.sharedMaterial.GetColor(LitMaterials.BaseColorId);
            return pawn != null && pawn.Team == ChessFight.Gameplay.Teams.Black ? new Color(0.17f, 0.15f, 0.13f) : new Color(0.93f, 0.9f, 0.84f);
        }

        static readonly Vector3[] ChipShapes = { new Vector3(1f, 0.45f, 0.7f), new Vector3(1f, 0.7f, 0.9f), new Vector3(1f, 0.55f, 0.55f) };

        Material RealChipMaterial()
        {
            if (realChipMat != null) return realChipMat;
            realChipMat = LitMaterials.NewLit(Color.white, 0.25f);
            realChipMat.hideFlags = HideFlags.HideAndDontSave;
            realChipMat.name = "Real chips";
            return realChipMat;
        }

        /// <summary><paramref name="n"/> chips of <paramref name="color"/> knocked off at <paramref name="at"/> along
        /// <paramref name="dir"/>: small rounded bits tumbling, bouncing once on the floor, lying a moment and shrinking away.</summary>
        void RealChips(Vector3 at, Vector3 dir, Color color, int n, float speed)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            var pal = new Kit.Palette { main = color, light = Color.Lerp(color, Color.white, 0.3f), deep = color * 0.62f, ink = color * 0.3f };
            float floorY = Kit.FloorUnder(at, 2f).y;
            for (int i = 0; i < n; i++)
            {
                // Three shapes (the kit keeps each round box it makes).
                var mesh = kit.RoundBox(ChipShapes[Random.Range(0, ChipShapes.Length)], 0.1f);
                var chip = new Kit.Prop(kit, mesh, pal, "Real chip", null, RealChipMaterial()) { inkWidth = 0f };
                Vector3 v = (dir + Random.insideUnitSphere * 0.6f).normalized * speed * Random.Range(0.5f, 1.1f);
                float size = Random.Range(0.012f, 0.035f);
                Vector3 p = at;
                Quaternion rot = Random.rotation;
                Vector3 spin = Random.onUnitSphere * Random.Range(300f, 900f);
                bool landed = false, bounced = false;
                var f = kit.Run(Random.Range(1.1f, 1.6f), (fx, d) =>
                {
                    if (!landed)
                    {
                        v += Physics.gravity * d;
                        p += v * d;
                        rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                        if (p.y <= floorY + size * 0.25f && v.y < 0f)
                        {
                            p.y = floorY + size * 0.25f;
                            if (!bounced && v.y < -1.5f)
                            {
                                bounced = true;
                                v = new Vector3(v.x * 0.5f, -v.y * 0.3f, v.z * 0.5f);
                                spin *= 0.4f;
                            }
                            else
                            {
                                landed = true;
                                rot = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
                            }
                        }
                    }
                    float fade = fx.age > fx.life - 0.25f ? (fx.age - (fx.life - 0.25f)) / 0.25f : 0f;
                    chip.t.SetPositionAndRotation(p, rot);
                    chip.t.localScale = Vector3.one * (size * (1f - fade));
                    chip.Apply();
                    return true;
                });
                f.props.Add(chip);
            }
        }

        /// <summary>A round soft glow: white in the middle, falling off as a gaussian to nothing at the edge.</summary>
        Texture2D RealGlowTexture()
        {
            if (realGlowTex != null) return realGlowTex;
            const int N = 64;
            realGlowTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, name = "Real glow" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r2 = dx * dx + dy * dy;
                    px[y * N + x] = new Color(1f, 1f, 1f, r2 >= 1f ? 0f : Mathf.Exp(-r2 * 5f) * (1f - r2));
                }
            realGlowTex.SetPixels(px);
            realGlowTex.Apply();
            return realGlowTex;
        }

        Material RealFlashMaterial()
        {
            if (realFlashMat != null) return realFlashMat;
            realFlashMat = new Material(Shader.Find(LitMaterials.ParticlesUnlitName)) { hideFlags = HideFlags.HideAndDontSave, name = "Real flash" };
            LitMaterials.MakeTransparent(realFlashMat, true);
            realFlashMat.SetTexture("_BaseMap", RealGlowTexture());
            realFlashMat.SetFloat("_Cull", 0f);
            return realFlashMat;
        }

        /// <summary>The flash and the lens streak of one bright moment at <paramref name="at"/> (Sword Fight's ClashFlash): both
        /// face the eye; the flash is gone in four frames, the two streaks (level on the screen) in seven, thinning.</summary>
        void RealFlash(Vector3 at, float power = 1f)
        {
            var parts = new List<(Transform t, MeshRenderer r, MaterialPropertyBlock b)>();
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject(i == 0 ? "Real flash" : "Real streak", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = kit.meshQuad;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = RealFlashMaterial();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                parts.Add((go.transform, r, new MaterialPropertyBlock()));
            }
            var f = kit.Run(8f * Frame, (fx, d) =>
            {
                float a = fx.age / Frame;
                var cam = ViewCamera;
                if (cam == null) return true;
                Quaternion face = cam.transform.rotation;
                Vector3 toEye = (cam.transform.position - at).normalized;
                for (int i = 0; i < parts.Count; i++)
                {
                    var (t, r, b) = parts[i];
                    t.SetPositionAndRotation(at + toEye * 0.15f, face);
                    float k, alpha;
                    Color c;
                    if (i == 0)
                    {
                        k = Mathf.Clamp01(a / 4f);
                        t.localScale = Vector3.one * (0.8f + 0.35f * k) * power;
                        alpha = (1f - k) * (1f - k);
                        c = new Color(1f, 0.97f, 0.92f) * 3.2f;
                    }
                    else
                    {
                        k = Mathf.Clamp01(a / 7f);
                        float len = (i == 1 ? 2.8f : 1.4f) * power * (0.8f + 0.2f * k), thick = (i == 1 ? 0.05f : 0.022f) * (1f - 0.7f * k);
                        t.localScale = new Vector3(len, thick, 1f);
                        alpha = (1f - k) * (i == 1 ? 0.55f : 1f);
                        c = new Color(0.88f, 0.94f, 1f) * (i == 1 ? 2.2f : 3.6f);
                    }
                    c.a = alpha;
                    b.SetColor(LitMaterials.BaseColorId, c);
                    r.SetPropertyBlock(b);
                }
                return true;
            });
            f.end = () => { foreach (var p in parts) if (p.t != null) Destroy(p.t.gameObject); };
        }

        /// <summary>A light at <paramref name="at"/> for a moment: bright at once and out in <paramref name="seconds"/>.</summary>
        void RealLight(Vector3 at, float intensity, float seconds, float range, Color color)
        {
            var go = new GameObject("Real light");
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.shadows = LightShadows.None;
            l.intensity = intensity;
            var f = kit.Run(seconds, (fx, d) =>
            {
                if (l == null) return false;
                float k = fx.age / fx.life;
                l.intensity = intensity * (1f - k) * (1f - k);
                return true;
            });
            f.end = () => { if (go != null) Destroy(go); };
        }
    }
}
