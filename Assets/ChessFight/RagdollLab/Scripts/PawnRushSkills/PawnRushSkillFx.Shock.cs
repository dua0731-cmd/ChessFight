using System;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R138 (승규 님 10-14, a picture of the queen's blast: "퀸이 스킬 쓸 때 저 폭발 같은 거 빼주고 퀸 스킬에 맞는 충격파 같은 게
    /// 펑~하고 연출해줘"): the queen's "팔방 밀치기" is a shock wave now, not an explosion. While she charges, the air gathers
    /// into her: a ball of bent air at her chest drawing in and a ring of it sliding in over the floor (her gold chess pieces
    /// still swirl in, polished, no longer heating up). The blast is a "펑": a dome of bent air bursting out to her reach with
    /// a thin gold rim, a ring of it running out over the floor with a hot front and a second one a beat behind, her crown
    /// ring of pearls riding it, and the eight pieces swept out over the floor by it the queen's eight ways, toppling and
    /// sliding. No fire, no fireball, no cracks or scorch on the floor.
    /// R139 (승규 님: "퀸이 팔방 밀치기 사용 할 때 먼지 vfx 지워주고 지금 충격파 그래픽 좋아 좀 더 충격파가 시각 적으로 나타났으면
    /// 좋겠어"): no dust at all (none thrown up along the front, none where a swept piece lands, none under a piece she
    /// knocks down), and the same shock wave easier to see: the ball of air at her chest pops first, the dome and the rings
    /// bend what is behind them harder and shade it a little more, tint it a light gold where the air is thickest (Skill
    /// Shock's _Tint, which cannot bloom into a white bubble), last a little longer, and a third ring follows. (The first
    /// take also split the colours at the front and tinted it far more: rainbow bands and a gold glass dome, "무지개 띠처럼
    /// ㄴㄴ".)
    /// R142 (승규 님 10-15: "퀸 팔망 밀치기 할 때 지금 공기 충격파 좋은데 전기+공기 충격파 같은 거였으면 좋겠어"): the same air,
    /// charged. Electric arcs (<see cref="SkillArc"/>: jagged, forking, a white-hot thread in a body of her gold, striking again
    /// every other frame) crackle round her chest while she charges, more and brighter as she comes to it, leap to her chest
    /// from each of her chess pieces as they swirl in, and now and then from the ring of air sliding in over the floor
    /// (round her chest alone they were lost in the pieces' gold trails); at the "펑" one shoots out along each of her eight ways
    /// from her chest down to the floor, then they crackle along the ring's front as it runs out (some jumping up off the
    /// floor), fewer and fainter as it spends itself; each piece the shock hits has them running over its body for a third of
    /// a second, the first down to the floor. Nothing round added.
    /// R143 (승규 님 10-15, a screen recording of the queen in play: "적용 된거야 난 안된거 같은데"): in play the arcs showed for
    /// about a tenth of a second twice, as faint pale threads, and the ones riding the dome and the ring's front ran out of the
    /// player's view at once. Now they are solid gold (<see cref="SkillArc"/>, R143) and they stay with her: past half her
    /// charge arcs jump from her body down to the floor round her feet; at the "펑" her eight bolts flicker on for a third of a
    /// second; arcs crawl over the floor round her for half a second; she crackles on for 0.8 s after, slower as it dies
    /// down; a piece the shock hits crackles for 0.6 s. The arcs light the floor and her round them, flickering with them.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        Material matShockShell, matShockRing;
        SkillArc arc;

        /// <summary>R142: the arcs' body, her gold (a pale gold glow alone was lost on the white floor and her white body);
        /// their core is white-hot (Skill Arc).</summary>
        static readonly Color ArcGold = new Color(1f, 0.72f, 0.24f);

        void BuildShock()
        {
            arc = new SkillArc(root);
            var shader = Shader.Find("ChessFight/Skill Shock");
            if (shader == null) shader = Resources.Load<Shader>("PawnRushSkillFx/SkillShock");
            if (shader == null) return;
            matShockShell = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            matShockShell.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            matShockRing = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            matShockRing.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            matShockRing.SetFloat("_Mode", 1f);
        }

        void DestroyShock()
        {
            if (matShockShell != null) Destroy(matShockShell);
            if (matShockRing != null) Destroy(matShockRing);
            arc?.Destroy();
            arc = null;
        }

        /// <summary>The queen's gold for the bent air's rim and the ring's front: only a tint added over what is behind (the
        /// first take's bright rim bloomed into a white bubble on the white floor).</summary>
        Color ShockGold => board ? Hdr(BQueen.light, 0.42f) : Hdr(Gold, 0.42f);

        /// <summary>R139: the gold the thick of the air colours what is behind it (a multiply, so white floor turns gold and
        /// nothing goes past white).</summary>
        static readonly Color ShockTint = new Color(1f, 0.79f, 0.4f);

        /// <summary>A shell or a floor ring of bent air (<c>Skill Shock</c>), placed and set each frame.</summary>
        class Air
        {
            readonly Transform t;
            readonly MeshRenderer r;
            readonly MaterialPropertyBlock b = new MaterialPropertyBlock();

            public Air(PawnRushSkillFx fx, bool ring, string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(fx.root, false);
                go.AddComponent<MeshFilter>().sharedMesh = ring ? fx.kit.meshQuad : fx.kit.meshPuff;
                r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = ring ? fx.matShockRing : fx.matShockShell;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                t = go.transform;
            }

            /// <summary>A ball of bent air <paramref name="radius"/> round <paramref name="at"/> (<paramref name="squash"/> of that
            /// tall), cut at <paramref name="floorY"/>; <paramref name="shade"/> how much its thick edge darkens what is behind,
            /// <paramref name="tint"/> how much it turns it gold there (R139).</summary>
            public void Shell(Vector3 at, float radius, float floorY, float bend, Color rim, float fade, float shade = 0.12f, float squash = 1f,
                              float tint = 0f)
            {
                t.SetPositionAndRotation(at, Quaternion.identity);
                float d = Mathf.Max(0.01f, 2f * radius);
                t.localScale = new Vector3(d, d * Mathf.Max(0.05f, squash), d);
                b.SetFloat("_Bend", bend);
                b.SetFloat("_Shade", shade);
                b.SetColor("_Rim", rim);
                b.SetColor("_Tint", new Color(ShockTint.r, ShockTint.g, ShockTint.b, tint));
                b.SetFloat("_FloorY", floorY);
                b.SetFloat("_Fade", Mathf.Clamp01(fade));
                r.SetPropertyBlock(b);
                r.enabled = fade > 0.001f && radius > 0.01f;
            }

            /// <summary>A ring of bent air on the floor at <paramref name="at"/>, <paramref name="radius"/> out, <paramref name="width"/> wide.</summary>
            public void Ring(Vector3 at, float radius, float width, float bend, Color front, float fade, float shade = 0.12f,
                             float tint = 0f)
            {
                float size = 2f * (radius + width * 1.6f) + 0.2f;
                t.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.up, Vector3.forward));
                t.localScale = new Vector3(size, size, 1f);
                b.SetFloat("_Size", size);
                b.SetFloat("_Radius", radius);
                b.SetFloat("_Width", Mathf.Max(0.02f, width));
                b.SetFloat("_Bend", bend);
                b.SetFloat("_Shade", shade);
                b.SetColor("_Front", front);
                b.SetColor("_Tint", new Color(ShockTint.r, ShockTint.g, ShockTint.b, tint));
                b.SetFloat("_Fade", Mathf.Clamp01(fade));
                r.SetPropertyBlock(b);
                r.enabled = fade > 0.001f;
            }

            public void Hide() => r.enabled = false;

            public void Destroy()
            {
                if (t != null) UnityEngine.Object.Destroy(t.gameObject);
            }
        }

        /// <summary>The air gathering into a charging queen (R138): a ball of bent air at her chest shrinking in as it thickens
        /// (its gold rim brighter), a ring of it sliding in to her over the floor. R142: charged: arcs crackling round her
        /// chest, more often and brighter as she comes to her charge (one every 0.1 s at first, two every 0.035 s at the
        /// end), and past a third of it one leaping in to her chest from the ring every 0.1–0.17 s. R143: past half of it one
        /// from her chest, hips or a hand down to the floor 0.35–0.8 m from her feet every 0.12 s, then every 0.06 s (over her
        /// white body and against the pieces' gold trails the chest's own were lost). Gone the moment her charge ends.</summary>
        void ShockGather(RagdollPawn q, Func<bool> charging, Func<float> charge, Func<Vector3> heart, Vector3 floor)
        {
            if (matShockShell == null || kit == null) return;
            var orb = new Air(this, false, "Queen gathering air");
            var ring = new Air(this, true, "Queen gathering ring");
            float floorY = floor.y;
            float spark = 0f, leap = 0.12f, ground = 0f;
            var f = kit.Run(10f, (fx, d) =>
            {
                if (q == null || !charging()) return false;
                float k = charge();
                orb.Shell(heart(), Mathf.Lerp(0.85f, 0.32f, Kit.EaseIn(k) * 0.6f + k * 0.4f), floorY - 10f,
                          Mathf.Lerp(0.012f, 0.05f, k), ShockGold * Mathf.Lerp(0.3f, 1f, k), Mathf.Clamp01(fx.age / (3f * Frame)), Mathf.Lerp(0.08f, 0.22f, k),
                          1f, 0.2f * k);
                Vector3 c = new Vector3(q.Hips.position.x, floorY + 0.02f, q.Hips.position.z);
                float rr = Mathf.Lerp(2.3f, 0.35f, Kit.EaseIn(Mathf.Clamp01(k * 1.05f)));
                // Faint: the first take's stronger bend set the whole floor round her wobbling.
                ring.Ring(c, rr, 0.22f, 0.01f, ShockGold * 0.4f, Mathf.Clamp01(fx.age / (3f * Frame)) * (1f - Mathf.Clamp01((k - 0.85f) / 0.15f)), 0.07f,
                          0.15f);
                if (arc == null) return true;
                spark -= d;
                if (spark <= 0f)
                {
                    spark = Mathf.Lerp(0.1f, 0.035f, k);
                    Vector3 h = heart();
                    for (int i = k > 0.5f ? 2 : 1; i > 0; i--)
                    {
                        // A short arc round her chest, reaching out past her (she is white: over her it is lost).
                        Vector3 u = Random.onUnitSphere;
                        Vector3 from = h + u * Random.Range(0.16f, 0.3f);
                        Vector3 to = h + Vector3.Slerp(u, Random.onUnitSphere, 0.7f).normalized * Random.Range(0.22f, 0.42f);
                        arc.Strike(from, to, ArcGold, 0.14f + 0.06f * k, Random.Range(0.05f, 0.09f), 0.45f + 0.55f * k, 0.3f, Random.value < 0.4f ? 1 : 0);
                    }
                }
                leap -= d;
                if (k > 0.33f && leap <= 0f)
                {
                    leap = Random.Range(0.1f, 0.17f);
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 from = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rr + Vector3.up * 0.02f;
                    arc.Strike(from, heart() + Random.insideUnitSphere * 0.08f, ArcGold, 0.19f, 0.07f, 0.6f + 0.4f * k, 0.2f, 1);
                }
                ground -= d;
                if (k > 0.45f && ground <= 0f)
                {
                    // Down from her to the floor round her feet, where the player sees it.
                    ground = Mathf.Lerp(0.12f, 0.06f, k);
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 from = Part(q, ArcParts[Random.Range(1, 5)]) + Random.insideUnitSphere * 0.05f;
                    Vector3 to = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(0.35f, 0.8f);
                    arc.Strike(from, to, ArcGold, 0.16f, Random.Range(0.08f, 0.12f), 0.65f + 0.35f * k, 0.25f, 1);
                }
                return true;
            });
            f.end = () =>
            {
                orb.Destroy();
                ring.Destroy();
            };
        }

        /// <summary>
        /// The queen's "펑" (R138, R139): the ball of air at her chest pops (five frames, bending hard); a dome of bent air
        /// bursts from her feet out past her reach in eleven frames, a light gold where it is thickest, its rim thinning, gone
        /// by frame 23; a ring of bent air with a gold front runs out over the floor to her reach in nine frames (with the crown
        /// ring of pearls, <see cref="QueenCrown"/>), a second one four frames behind and a third nine behind; the eight chess
        /// pieces swept out (<see cref="SweepChess"/>). No dust. R142: the electricity let go with it: an arc from her chest
        /// down to the floor 1.1–1.6 m out along each of her eight ways (forking twice, 0.08–0.12 s), then every other frame
        /// for 18 frames arcs along the ring's front (four while it runs out, two after; some jump up to 0.35 m off the
        /// floor), fainter as it spends itself. R143: the eight flicker on for 0.28–0.36 s; for 30 frames arcs crawl over the
        /// floor 0.25–1.9 m round her (three, then two, then one every other frame); she crackles on for 0.8 s
        /// (<see cref="ArcThrough"/>).
        /// </summary>
        void ShockBlast(Vector3 c, float outer, RagdollPawn queen)
        {
            if (kit == null) return;
            if (matShockShell != null)
            {
                Vector3 heart = queen != null ? ChestOf(queen) + Vector3.up * 0.05f : c + Vector3.up * 0.5f;
                var pop = new Air(this, false, "Queen shock pop");
                var dome = new Air(this, false, "Queen shock dome");
                var wave = new Air(this, true, "Queen shock ring");
                var echo = new Air(this, true, "Queen shock echo");
                var last = new Air(this, true, "Queen shock last echo");
                bool burst = false;
                int struck = -1;
                float turn = Random.Range(0f, 45f);
                var f = kit.Run(36f * Frame, (fx, d) =>
                {
                    float a = fx.age / Frame;
                    // The gathered ball bursting: thick and fast, out of sight by frame 8.
                    float kp = Kit.EaseOut(a / 5f);
                    pop.Shell(heart, Mathf.Lerp(0.32f, 1.25f, kp), c.y, 0.1f * (1f - kp) + 0.02f, ShockGold * 0.25f,
                              1f - Mathf.Clamp01((a - 3f) / 5f), 0.18f, 1f, 0.2f);
                    float k = Kit.EaseOut(a / 11f);
                    dome.Shell(c, Mathf.Lerp(0.4f, outer * 1.1f, k), c.y, 0.08f * (1f - Mathf.Clamp01(a / 26f)) + 0.012f,
                               ShockGold * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(a / 16f)), 1f - Mathf.Clamp01((a - 8f) / 15f),
                               Mathf.Lerp(0.36f, 0.13f, Mathf.Clamp01(a / 14f)), 0.52f, Mathf.Lerp(0.3f, 0.1f, Mathf.Clamp01(a / 16f)));
                    float kr = Kit.EaseOut(a / 9f);
                    wave.Ring(c + Vector3.up * 0.02f, Mathf.Lerp(0.3f, outer * 1.02f, kr), Mathf.Lerp(0.28f, 0.66f, kr), 0.068f,
                              ShockGold, 1f - Mathf.Clamp01((a - 9f) / 14f), 0.28f, 0.35f);
                    float ae = a - 4f;
                    float ke = Kit.EaseOut(Mathf.Max(0f, ae) / 13f);
                    echo.Ring(c + Vector3.up * 0.025f, Mathf.Lerp(0.3f, outer * 0.9f, ke), Mathf.Lerp(0.22f, 0.45f, ke), 0.048f,
                              ShockGold * 0.6f, ae <= 0f ? 0f : 1f - Mathf.Clamp01((ae - 12f) / 12f), 0.17f, 0.22f);
                    float al = a - 9f;
                    float kl = Kit.EaseOut(Mathf.Max(0f, al) / 14f);
                    last.Ring(c + Vector3.up * 0.03f, Mathf.Lerp(0.3f, outer * 0.75f, kl), Mathf.Lerp(0.18f, 0.35f, kl), 0.03f,
                              ShockGold * 0.35f, al <= 0f ? 0f : 1f - Mathf.Clamp01((al - 10f) / 12f), 0.1f, 0.12f);
                    if (arc == null) return true;
                    if (!burst)
                    {
                        // Let go along her eight ways, from her chest down to the floor, flickering on a while (R143: a
                        // little thinner now they are solid, 0.32 m read as gold roots).
                        burst = true;
                        for (int i = 0; i < 8; i++)
                        {
                            float ang = (turn + i * 45f + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                            Vector3 to = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(1.1f, 1.6f) + Vector3.up * 0.02f;
                            arc.Strike(heart, to, ArcGold, 0.27f, Random.Range(0.28f, 0.36f), 1f, 0.2f, 2);
                        }
                        if (queen != null) ArcThrough(queen, 0.8f);
                    }
                    int tick = Mathf.FloorToInt(a / 2f);
                    if (tick != struck)
                    {
                        struck = tick;
                        if (a < 18f)
                        {
                            // Crackling along the front of the ring as it runs out.
                            float front = Mathf.Lerp(0.3f, outer * 1.02f, kr), spent = 1f - a / 18f;
                            for (int i = a < 9f ? 6 : 3; i > 0; i--)
                            {
                                float t0 = Random.Range(0f, Mathf.PI * 2f);
                                float t1 = t0 + Random.Range(0.35f, 0.7f) / Mathf.Max(0.5f, front) * (Random.value < 0.5f ? -1f : 1f);
                                Vector3 p0 = c + new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0)) * front + Vector3.up * Random.Range(0.02f, 0.1f);
                                Vector3 p1 = c + new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1)) * front * Random.Range(0.96f, 1.04f) + Vector3.up * Random.Range(0.04f, 0.35f);
                                arc.Strike(p0, p1, ArcGold, 0.24f, 0.07f, 0.5f + 0.5f * spent, 0.3f, 1);
                            }
                        }
                        // And over the dome of air while it bursts out: short arcs across its face.
                        if (a < 12f)
                        {
                            float rd = Mathf.Lerp(0.4f, outer * 1.1f, k);
                            for (int i = 0; i < 2; i++)
                            {
                                Vector3 v = Random.onUnitSphere;
                                v.y = Mathf.Abs(v.y) * 0.52f;
                                Vector3 w = Vector3.Slerp(v.normalized, Random.onUnitSphere, 0.22f);
                                w.y = Mathf.Abs(w.y) * 0.52f;
                                arc.Strike(c + v * rd, c + w * rd, ArcGold, 0.2f, 0.06f, 0.85f * (1f - a / 12f), 0.3f, 1);
                            }
                        }
                        // R143: crawling over the floor round her for half a second (the ones on the dome and the ring's
                        // front leave the player's view at once).
                        if (a < 30f)
                        {
                            float left = 1f - a / 30f;
                            for (int i = a < 12f ? 3 : a < 22f ? 2 : 1; i > 0; i--)
                            {
                                float ang = Random.Range(0f, Mathf.PI * 2f);
                                var way = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                                var across = new Vector3(-way.z, 0f, way.x);
                                float from = Random.Range(0.25f, 1.2f);
                                Vector3 p0 = c + way * from + Vector3.up * 0.05f;
                                Vector3 p1 = c + way * (from + Random.Range(0.3f, 0.7f)) + across * Random.Range(-0.25f, 0.25f) + Vector3.up * 0.05f;
                                arc.Strike(p0, p1, ArcGold, 0.18f, Random.Range(0.06f, 0.1f), 0.5f + 0.5f * left, 0.22f, 1);
                            }
                        }
                    }
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
            SweepChess(c, outer);
        }

        static readonly BodyId[] ArcParts = { BodyId.Head, BodyId.Chest, BodyId.Hips, BodyId.HandL, BodyId.HandR, BodyId.FootL, BodyId.FootR };

        /// <summary>The shock's electricity running through a piece it hits (R142): an arc between two parts of its body every
        /// 0.03–0.05 s, fainter as it goes, gone in <paramref name="seconds"/>; the first one from it down to the floor.
        /// R143: slower as it dies down (every 0.1 s at the end), and now and then (three in ten) down to the floor again.</summary>
        void ArcThrough(RagdollPawn p, float seconds)
        {
            if (arc == null || kit == null || p == null) return;
            float next = 0f;
            bool grounded = false;
            kit.Run(seconds, (fx, d) =>
            {
                if (p == null) return false;
                next -= d;
                if (next > 0f) return true;
                next = Mathf.Lerp(0.04f, 0.1f, fx.age / seconds) * Random.Range(0.75f, 1.25f);
                Vector3 from = Part(p, ArcParts[Random.Range(0, ArcParts.Length)]) + Random.insideUnitSphere * 0.04f;
                Vector3 to;
                if (!grounded || Random.value < 0.3f)
                {
                    // To the floor beside it (right under it, its own body hid the arc).
                    grounded = true;
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    to = Kit.FloorUnder(p.Hips.position, 0.8f) + Vector3.up * 0.02f + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(0.2f, 0.5f);
                }
                else
                {
                    to = Part(p, ArcParts[Random.Range(0, ArcParts.Length)]) + Random.insideUnitSphere * 0.04f;
                    if ((to - from).sqrMagnitude < 0.01f) to = from + Random.onUnitSphere * 0.2f;
                }
                arc.Strike(from, to, ArcGold, 0.17f, 0.05f, 0.4f + 0.6f * (1f - fx.age / seconds), 0.3f, 0);
                return true;
            });
        }

        static Vector3 Part(RagdollPawn p, BodyId id)
        {
            var body = p.bodies[(int)id];
            return body != null ? body.position : p.Hips.position;
        }

        /// <summary>
        /// The queen's eight chess pieces swept out by her shock (R138; they burst up out of a fireball before): one on each
        /// of her eight ways, thrown out low over the floor, toppling over outward as they go, sliding to a stop, then fading
        /// away (R139: no puff of dust where each lands). Polished (gold and silver in the board look), never heated.
        /// </summary>
        void SweepChess(Vector3 c, float outer)
        {
            if (chessMeshes == null || chessMeshes.Length == 0) return;
            const int count = 8;
            float turn = Random.Range(0f, 45f);
            float floorY = c.y;
            for (int i = 0; i < count; i++)
            {
                int kind = i % chessMeshes.Length;
                float ang = (turn + i * 45f + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 axis = Vector3.Cross(Vector3.up, radial);
                float size = ChessKing * ChessHeights[kind];
                Vector3 pos = c + radial * 0.5f + Vector3.up * (size * 0.5f + 0.05f);
                Vector3 vel = radial * Random.Range(5.2f, 6.6f) * Mathf.Clamp(outer / 3f, 0.6f, 1.4f) + Vector3.up * Random.Range(1.4f, 2.2f);
                float topple = 0f, toppleRate = Random.Range(520f, 760f), yaw = Random.Range(0f, 360f), last = 0f;
                Add(new Puff(root, "Queen swept chess piece", chessMeshes[kind], ChessMaterial(i))
                {
                    life = Random.Range(1.05f, 1.2f),
                    lump = 0f,
                    flow = 2f,
                    animate = (p, t) =>
                    {
                        float dt = p.Age - last;
                        last = p.Age;
                        vel += Physics.gravity * dt;
                        pos += vel * dt;
                        topple = Mathf.Min(90f, topple + toppleRate * dt);
                        // Standing it rests on its foot; tipped over, on its side.
                        float rest = floorY + size * Mathf.Lerp(0.5f, 0.3f, topple / 90f);
                        if (pos.y < rest)
                        {
                            pos.y = rest;
                            if (vel.y < 0f) vel.y = -vel.y * 0.25f;
                            float drag = Mathf.Exp(-5f * dt);
                            vel.x *= drag;
                            vel.z *= drag;
                        }
                        p.at = pos;
                        p.rotation = Quaternion.AngleAxis(topple, axis) * Quaternion.Euler(0f, yaw, 0f);
                        p.scale = Vector3.one * size;
                        p.heat = 0f;
                        p.rim = Color.black;
                        p.dissolve = Keys(t, 0.72f, 0f, 1f, 1f);
                    },
                });
            }
        }
    }
}
