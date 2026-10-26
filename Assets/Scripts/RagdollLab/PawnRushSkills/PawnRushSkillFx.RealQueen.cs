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
    /// The queen's "팔방 밀치기" drawn for real (10-25). The first take was only clear air and faint lines and on the white
    /// floor showed almost nothing (승규 님: "지금 너무 밑밑해 실사적인데 스킬 다웠으면 좋겠어"); this second take keeps it real
    /// - light, bent air, grit off the floor, chips off a piece, a long exposure's trail - but loud enough to read as a
    /// skill: the charge lights her up and draws the floor's grit in, the "펑" flares light over the floor, its shock front
    /// catches the light, grit flies out along it, and a piece it hits is struck with a flash and leaves a trail.
    /// Then (승규 님: "됐어 이정도만 하면 됐고 퀸 스킬을 좀 더 강하게 표현 해도 될 거 같아"): the same, stronger - more light, the air
    /// bent harder and longer, sixteen gusts, more and faster grit, a longer hold and a bigger shake, harder hits.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>The queen's light: a warm white, a little gold, as a lamp gives - not her cartoon gold.</summary>
        static readonly Color QueenLight = new Color(1f, 0.86f, 0.62f);

        Material ghostRealMat;

        /// <summary>
        /// Her warning while she charges, for real: two circles of light lying on the floor - a firmer one at her inner
        /// reach (where she knocks down) and a fainter one at her reach (where she pushes) - growing brighter as the push
        /// comes, the inner one pulsing faster near the end; out in four frames once she lets go. She does not coil down.
        /// </summary>
        void RealQueenWarning(RagdollPawn q)
        {
            if (q.SkillStage != SkillStage.Windup) return;
            var s = Params;
            float windup = s != null ? s.queenWindup : 0.35f, inner = s != null ? s.queenInner : 1.5f, outer = s != null ? s.queenRadius : 3f;
            kit.Keep(q, "queen real warning", () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < 2; i++) fx.strips.Add(LightStrip());
                var line = new List<Vector3>();
                float released = -1f, k = 0f;
                fx.step = (x, d) =>
                {
                    if (q == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float a = released >= 0f ? (x.age - released) / Frame : -1f;
                    if (a >= 4f) return false;
                    if (released < 0f) k = Kit.Charge(q.SkillStageTime, windup);
                    float fade = released < 0f ? Mathf.Clamp01(x.age / (4f * Frame)) : 1f - a / 4f;
                    float pulse = 0.85f + 0.15f * Mathf.Sin(kit.Clock * Mathf.Lerp(12f, 40f, k));
                    Vector3 c = Flat(q.Hips.position) + Vector3.up * q.Hips.position.y;
                    for (int i = 0; i < 2; i++)
                    {
                        float r = i == 0 ? inner : outer;
                        line.Clear();
                        const int N = 56;
                        for (int j = 0; j <= N; j++)
                        {
                            float ang = j * Mathf.PI * 2f / N;
                            line.Add(Kit.FloorUnder(c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r, 1.5f) + Vector3.up * 0.014f);
                        }
                        var st = x.strips[i];
                        st.Build(line, j => i == 0 ? 0.05f : 0.032f, kit.Eye, true);
                        st.taper = 0f;
                        st.opacity = fade * (i == 0 ? 0.75f * pulse : 0.42f) * (0.4f + 0.6f * k);
                        st.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        Kit.Strip LightStrip()
        {
            var pal = new Kit.Palette { main = LightLine, light = LightLine, deep = LightLine, ink = LightLine };
            return new Kit.Strip(kit, pal) { core = LightLine, coreShare = 1f, inkShare = 0f };
        }

        /// <summary>
        /// The charge: she draws the air in and you see it - a ball of bent air at her chest with a pale bright edge drawing
        /// in, a ring of it sliding in over the floor; grit lifts off the floor round her and is drawn in, swirling up toward
        /// her; a warm light rises in her and lights the floor round her, brighter as she comes to it; at the end a glint
        /// flares on her crown. No gold paint, no electricity, no chess pieces. Gone the moment her charge ends.
        /// </summary>
        void RealQueenCharge(RagdollPawn q)
        {
            if (kit == null) return;
            float windup = Params != null ? Params.queenWindup : 0.35f;
            Vector3 floor = FloorUnder(q);
            Air orb = matShockShell != null ? new Air(this, false, "Queen real gathering air") : null;
            Air ring = matShockShell != null ? new Air(this, true, "Queen real gathering ring") : null;
            var glow = new GameObject("Queen real glow").AddComponent<Light>();
            glow.transform.SetParent(root, false);
            glow.type = LightType.Point;
            glow.color = QueenLight;
            glow.shadows = LightShadows.None;
            glow.intensity = 0f;
            bool glinted = false;
            float nextGrit = 0f;
            Color grit = FloorColor(floor);
            var f = kit.Run(10f, (fx, d) =>
            {
                if (q == null || !QueenCharging(q)) return false;
                float k = Mathf.Clamp01(q.SkillStageTime / windup);
                float on = Mathf.Clamp01(fx.age / (3f * Frame));
                Vector3 heart = ChestOf(q) + Vector3.up * 0.05f;
                Vector3 c = new Vector3(q.Hips.position.x, floor.y + 0.02f, q.Hips.position.z);
                Color edge = Kit.A(QueenLight * Mathf.Lerp(0.25f, 0.6f, k), 1f);
                orb?.Shell(heart, Mathf.Lerp(0.85f, 0.3f, Kit.EaseIn(k) * 0.6f + k * 0.4f), floor.y - 10f,
                           Mathf.Lerp(0.03f, 0.1f, k), edge, on, Mathf.Lerp(0.08f, 0.18f, k));
                float rr = Mathf.Lerp(2.6f, 0.35f, Kit.EaseIn(Mathf.Clamp01(k * 1.05f)));
                ring?.Ring(c, rr, 0.26f, 0.03f, edge, on * (1f - Mathf.Clamp01((k - 0.85f) / 0.15f)), 0.08f);
                glow.transform.position = heart + Vector3.up * 0.1f;
                glow.range = Mathf.Lerp(2.2f, 4.2f, k);
                glow.intensity = Mathf.Lerp(0.4f, 3.6f, k * k) * (0.9f + 0.1f * Mathf.Sin(kit.Clock * 50f));
                // Grit lifted off the floor round her and drawn in.
                if (fx.age >= nextGrit && k < 0.9f)
                {
                    nextGrit = fx.age + 0.012f;
                    float ang = Random.Range(0f, Mathf.PI * 2f), r0 = Random.Range(1.2f, 2.6f);
                    Vector3 from = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r0;
                    GritIn(from, q, grit, windup * (1f - k) + 0.05f);
                }
                if (!glinted && k > 0.8f)
                {
                    glinted = true;
                    RealFlash(HeadOf(q) + Vector3.up * 0.18f, 0.6f);
                }
                return true;
            });
            f.end = () =>
            {
                orb?.Destroy();
                ring?.Destroy();
                if (glow != null) Destroy(glow.gameObject);
            };
        }

        /// <summary>A grain of the floor lifted at <paramref name="from"/> and drawn in to <paramref name="q"/>'s chest in
        /// <paramref name="seconds"/>, rising, turning round her as it comes, shrinking into her.</summary>
        void GritIn(Vector3 from, RagdollPawn q, Color color, float seconds)
        {
            var pal = new Kit.Palette { main = color, light = Color.Lerp(color, Color.white, 0.3f), deep = color * 0.7f, ink = color * 0.4f };
            var chip = new Kit.Prop(kit, kit.RoundBox(ChipShapes[Random.Range(0, ChipShapes.Length)], 0.1f), pal, "Queen grit", null, RealChipMaterial()) { inkWidth = 0f };
            float size = Random.Range(0.02f, 0.05f), turn = Random.Range(0.6f, 1.2f) * (Random.value < 0.5f ? -1f : 1f);
            Quaternion rot = Random.rotation;
            Vector3 spin = Random.onUnitSphere * Random.Range(400f, 900f);
            seconds = Mathf.Max(0.12f, seconds);
            var f = kit.Run(seconds, (fx, d) =>
            {
                if (q == null) return false;
                float k = fx.age / fx.life, e = k * k;
                Vector3 to = ChestOf(q);
                Vector3 axis = new Vector3(to.x, from.y, to.z);
                Vector3 off = Quaternion.Euler(0f, turn * 90f * k, 0f) * (from - axis);
                Vector3 p = Vector3.Lerp(axis + off, to, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.4f;
                rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                chip.t.SetPositionAndRotation(p, rot);
                chip.t.localScale = Vector3.one * size * Mathf.Clamp01(fx.age / 0.04f) * (1f - 0.7f * e);
                chip.Apply();
                return true;
            });
            f.props.Add(chip);
        }

        /// <summary>
        /// The "펑": the air she gathered bursts out of her as a shock wave you can see - a white flash and lens streaks at
        /// her chest, a strong warm light flaring over the floor round her; a dome of bent air with a pale bright edge
        /// running out past her reach; a ring of it racing over the floor with a bright front where the light catches it,
        /// two more behind; grit and chips of the floor thrown out along the front; gusts of air thrown low along her eight
        /// ways. A hold, a shake. No gold paint, no pearls, no chess pieces, no electricity, no dust.
        /// </summary>
        void RealQueenBlast(SkillFxEvent e)
        {
            Vector3 c = Ground(e.at);
            float outer = e.size > 0f ? e.size : 3f;
            var queen = e.by;
            Vector3 heart = queen != null ? ChestOf(queen) + Vector3.up * 0.05f : c + Vector3.up * 0.5f;
            if (matShockShell != null)
            {
                var pop = new Air(this, false, "Queen real pop");
                var dome = new Air(this, false, "Queen real dome");
                var wave = new Air(this, true, "Queen real ring");
                var echo = new Air(this, true, "Queen real echo");
                var last = new Air(this, true, "Queen real last echo");
                var f = kit.Run(40f * Frame, (fx, d) =>
                {
                    float a = fx.age / Frame;
                    float kp = Kit.EaseOut(a / 5f);
                    pop.Shell(heart, Mathf.Lerp(0.3f, 1.6f, kp), c.y, 0.2f * (1f - kp) + 0.04f, Kit.A(QueenLight * 0.9f, 1f),
                              1f - Mathf.Clamp01((a - 3f) / 5f), 0.12f);
                    float k = Kit.EaseOut(a / 12f);
                    float edge = Mathf.Lerp(1.1f, 0.25f, Mathf.Clamp01(a / 20f));
                    dome.Shell(c, Mathf.Lerp(0.4f, outer * 1.15f, k), c.y, 0.14f * (1f - Mathf.Clamp01(a / 30f)) + 0.02f,
                               Kit.A(QueenLight * edge, 1f), 1f - Mathf.Clamp01((a - 12f) / 18f), Mathf.Lerp(0.2f, 0.06f, Mathf.Clamp01(a / 16f)), 0.55f);
                    float kr = Kit.EaseOut(a / 10f);
                    wave.Ring(c + Vector3.up * 0.02f, Mathf.Lerp(0.3f, outer * 1.06f, kr), Mathf.Lerp(0.35f, 0.9f, kr), 0.12f,
                              Kit.A(QueenLight * Mathf.Lerp(1.9f, 0.6f, Mathf.Clamp01(a / 18f)), 1f), 1f - Mathf.Clamp01((a - 12f) / 16f), 0.24f);
                    float ae = a - 4f, ke = Kit.EaseOut(Mathf.Max(0f, ae) / 13f);
                    echo.Ring(c + Vector3.up * 0.025f, Mathf.Lerp(0.3f, outer * 0.94f, ke), Mathf.Lerp(0.25f, 0.6f, ke), 0.085f,
                              Kit.A(QueenLight * 0.9f, 1f), ae <= 0f ? 0f : 1f - Mathf.Clamp01((ae - 12f) / 12f), 0.12f);
                    float al = a - 9f, kl = Kit.EaseOut(Mathf.Max(0f, al) / 14f);
                    last.Ring(c + Vector3.up * 0.03f, Mathf.Lerp(0.3f, outer * 0.76f, kl), Mathf.Lerp(0.18f, 0.38f, kl), 0.04f,
                              Kit.A(QueenLight * 0.35f, 1f), al <= 0f ? 0f : 1f - Mathf.Clamp01((al - 10f) / 12f), 0.08f);
                    return true;
                });
                f.end = () =>
                {
                    pop.Destroy();
                    dome.Destroy();
                    wave.Destroy();
                    echo.Destroy();
                    last.Destroy();
                };
            }
            // The air thrown out low along her eight ways, and the floor's grit thrown out along the front.
            float turn = Random.Range(0f, 45f);
            for (int i = 0; i < 16; i++)
            {
                var way = Quaternion.Euler(0f, turn + i * 22.5f, 0f) * Vector3.forward;
                air?.Kick(c + way * 0.45f + Vector3.up * 0.25f, way + Vector3.down * 0.15f, i % 2 == 0 ? 1.6f : 1.1f);
            }
            Color grit = FloorColor(c);
            for (int i = 0; i < 44; i++)
            {
                var way = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                RealChips(c + way * Random.Range(0.4f, 0.9f) + Vector3.up * 0.03f, way + Vector3.up * Random.Range(0.25f, 0.7f), grit, 1, Random.Range(5f, 9f));
            }
            RealFlash(heart, 1.9f);
            RealLight(heart + Vector3.up * 0.2f, 9f, 0.4f, 8f, QueenLight);
            HitStop(8f * Frame);
            Shake(0.28f, 16f * Frame);
        }

        /// <summary>A piece her shock hits: a flash where it is struck, the air knocked on past it, chips of its own body
        /// knocked off, and a see-through copy of it left behind every other frame for a third of a second as it is thrown
        /// (a long exposure's trail). No colour paint, no drops, no captured square, no electricity.</summary>
        void RealQueenHit(SkillFxEvent e)
        {
            if (e.target == null) return;
            var target = e.target;
            Vector3 at = ChestOf(target);
            Vector3 away = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            bool down = e.count == 1;
            RealFlash(at - away * 0.15f, down ? 1f : 0.7f);
            air?.Kick(at - away * 0.1f, away, down ? 1.6f : 1.1f);
            RealChips(at, away + Vector3.up * 0.6f, BodyColor(target), down ? 16 : 9, 4.5f);
            float next = 0f;
            kit.Run(0.45f, (fx, d) =>
            {
                if (target == null) return false;
                if (fx.age >= next)
                {
                    RealGhost(target, 0.45f, 0.26f, -away);
                    next = fx.age + 2f * Frame;
                }
                return true;
            });
            HitStop((down ? 5f : 3f) * Frame);
        }

        /// <summary>An afterimage as a long exposure leaves one (Sword Fight's RealGhost): the piece itself, its own colour,
        /// held where it is this frame and see-through, fading in <paramref name="life"/> seconds, drawn a little back.</summary>
        void RealGhost(RagdollPawn pawn, float alpha, float life, Vector3 back)
        {
            if (pawn == null) return;
            var skin = pawn.skin;
            if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy)
            {
                skin = null;
                foreach (var s in pawn.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (s.enabled && s.gameObject.activeInHierarchy) { skin = s; break; }
            }
            if (skin == null) return;
            if (ghostRealMat == null)
            {
                ghostRealMat = LitMaterials.NewLit(Color.white, 0.25f);
                ghostRealMat.hideFlags = HideFlags.HideAndDontSave;
                ghostRealMat.name = "Real afterimage";
                LitMaterials.MakeTransparent(ghostRealMat);
            }
            var mesh = new Mesh { name = "Real afterimage", hideFlags = HideFlags.HideAndDontSave };
            skin.BakeMesh(mesh, true);
            var go = new GameObject("Real afterimage", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            Vector3 pos = skin.transform.position;
            go.transform.SetPositionAndRotation(pos, skin.transform.rotation);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = ghostRealMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            Color body = BodyColor(pawn);
            var f = kit.Run(life, (fx, d) =>
            {
                if (go == null) return false;
                float k = fx.age / fx.life;
                go.transform.position = pos + back * (0.12f * k);
                Color c = Color.Lerp(body, new Color(0.9f, 0.92f, 0.96f), 0.15f);
                c.a = alpha * (1f - k) * (1f - k);
                block.SetColor(LitMaterials.BaseColorId, c);
                r.SetPropertyBlock(block);
                return true;
            });
            f.end = () => { if (go != null) Destroy(go); Destroy(mesh); };
        }

        /// <summary>The colour of the floor under <paramref name="at"/> (its material), for the grit thrown off it.</summary>
        static Color FloorColor(Vector3 at)
        {
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var r = hit.collider.GetComponent<Renderer>();
                var m = r != null ? r.sharedMaterial : null;
                if (m != null && m.HasProperty(LitMaterials.BaseColorId))
                {
                    Color c = m.GetColor(LitMaterials.BaseColorId) * 0.85f;
                    c.a = 1f;
                    return c;
                }
            }
            return new Color(0.78f, 0.74f, 0.66f);
        }
    }
}
