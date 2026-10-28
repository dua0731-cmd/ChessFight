using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The king's sword breaking into the platform's edge (승규 님 10-14: "스킬 기획안 내용 봤지? 바닥 벽 파괴 되게 하는 거
    /// 이거 킹 왕의 귀환 때 실제로 칼이 박히는 연출이랑 그에 따라 벽도 실제로 부서지고 그 주변에 금이 가있게 만들어줘", then
    /// "칼이 박혔고 wall이 부서졌다!라는 느낌이 더 들어야 돼 그 주위에 파편들도"; the plan's "판 부수기", done the way the Queen
    /// of the Hill bishop's strike digs the floor, R134). It drew ink cracks over the lip and the sword simply stood in the
    /// solid box.
    /// - The sword drives in (SwordFightSkills.Edge: it plunges the last 0.3 m in three frames, then its hilt quivers as his
    ///   weight comes onto it) and a crater breaks into the wall round it, reaching up through the lip so the top is bitten
    ///   out too (<c>ChessFight/Skill Breach</c>: real depth from any angle, the wall's own stone inside, the stuck blade seen
    ///   going into the stone at its bottom); cracks run out over the face and on over the lip onto the top, glowing gold a
    ///   moment, then dark.
    /// - What breaks off: a spray of grit and chips and a few heavy chunks burst out of it and fall away into the void, two
    ///   broken pieces of the lip with them, dust; broken plates of the face stick out round the crater, tilted out of the
    ///   wall, and rubble thrown up over the lip lies on the top - both stay until it heals. While he hangs, grit trickles
    ///   from the rim; when he tears the sword out, more chips and dust.
    /// - It stays a while after the sword is out (or he falls), then heals.
    /// Only what it looks like: the platform's colliders do not change (the crater is 0.4 m round, at the edge).
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>The crater's radius and depth, how far its cracks run (m); how long it all stays once the sword is out and
        /// how long it takes to heal (s).</summary>
        // 10-25 (승규 님: "왕 바깥 원 부분을 조금 줄여줘"): the crater 0.38 → 0.3 m round, its cracks 0.75 → 0.55 m.
        const float BreachRadius = 0.3f, BreachDepth = 0.2f, BreachCracks = 0.55f, BreachHold = 2.4f, BreachHeal = 0.6f;

        /// <summary>The wall's chips: the platform's pale stone.</summary>
        static readonly Kit.Palette ChipPale = new Kit.Palette("#CDD1D6", "#EEF0F3", "#9CA1A8", "#3A3A44");
        static readonly Kit.Palette ChipShade = new Kit.Palette("#B3B8BF", "#D5D9DE", "#80858C", "#3A3A44");
        /// <summary>10-25 (승규 님: "파티클이 지금 회색이야 실제 부서진 벽 색에 따라서"): this breach's chips, from the wall
        /// it broke - on the prototype board its brass trim (<see cref="chipA"/>) and the square's wood under it, else the
        /// platform's own colour. <see cref="chipShare"/> of them are <see cref="chipA"/>.</summary>
        Kit.Palette chipA = ChipPale, chipB = ChipShade;
        float chipShare = 0.6f;

        static Kit.Palette ChipsOf(Color c, float shade = 1f)
        {
            c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
            return new Kit.Palette { main = c, light = Color.Lerp(c, Color.white, 0.3f), deep = new Color(c.r * 0.62f, c.g * 0.62f, c.b * 0.62f, 1f),
                ink = new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 1f) };
        }

        /// <summary>Sparks off the blade in the stone: hot white-gold, no ink (the kit's gold drops read as coins here).</summary>
        static readonly Kit.Palette HotSpark = new Kit.Palette
        {
            main = new Color(3.0f, 2.2f, 1.0f), light = new Color(4f, 3.6f, 2.6f), deep = new Color(2.2f, 1.3f, 0.5f), ink = new Color(2.2f, 1.3f, 0.5f),
        };

        /// <summary>Paint the broken part red and the rest of its quads blue (to see where it is drawn).</summary>
        public static bool BreachDebug;

        Material matBreach;
        bool breachLooked;

        sealed class Breach
        {
            public SwordFightSkills king;
            public int uses;
            public GameObject face, top;
            public MeshRenderer rf, rt;
            public readonly MaterialPropertyBlock b = new MaterialPropertyBlock();
            public Vector3 at, outward;
            public float topY, bottomY, seed;
            /// <summary>Effect time when the sword came out (or he fell); below 0 while it is in.</summary>
            public float released = -1f;
            /// <summary>Healed and gone (what lies round it goes with it).</summary>
            public bool gone;
            public Renderer blade, guard;
            public float trickle;
        }

        readonly List<Breach> breaches = new List<Breach>();

        bool BreachReady()
        {
            if (!breachLooked)
            {
                breachLooked = true;
                var s = Shader.Find("ChessFight/Skill Breach");
                if (s == null) s = Resources.Load<Shader>("SwordFightSkillFx/SkillBreach");
                if (s != null && s.isSupported) matBreach = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            }
            return matBreach != null;
        }

        void DestroyBreaches()
        {
            foreach (var g in breaches)
            {
                g.gone = true;
                if (g.face != null) Destroy(g.face);
                if (g.top != null) Destroy(g.top);
            }
            breaches.Clear();
            if (matBreach != null) Destroy(matBreach);
        }

        /// <summary>How far a breach has healed: 0 while the sword is in and a while after, 1 gone.</summary>
        float BreachHealed(Breach g)
        {
            if (g.gone) return 1f;
            float since = g.released < 0f ? 0f : kit.Clock - g.released;
            return Mathf.Clamp01((since - BreachHold) / BreachHeal);
        }

        /// <summary>The sword goes into the wall: the crater, what breaks off and what lies round it, then the crater kept up
        /// to date each frame until it has healed.</summary>
        void KingBreach(SwordFightSkills s)
        {
            var floor = SwordFightEdgeFloor.Current;
            if (s == null || s.Pawn == null || floor == null || !floor.Found) return;
            Vector3 o = Kit.FlatDir(s.EdgeOut, Vector3.forward);
            // Where the blade crosses the face: the face is the plane through the lip, facing out.
            Vector3 hilt = s.Hilt, d = s.StuckBlade;
            float den = Vector3.Dot(d, o);
            Vector3 at = Mathf.Abs(den) > 1e-3f ? hilt + d * (Vector3.Dot(s.EdgeAt - hilt, o) / den) : s.EdgeAt + Vector3.down * 0.16f;
            at.y = Mathf.Min(at.y, floor.Top - 0.04f);
            foreach (var old in breaches) if (old.king == s && old.released < 0f) old.released = kit.Clock;
            // What the wall is: on the prototype board (SwordFightArena, the platform hidden under it) its face is the edge
            // trim, standing EdgeOut out of the platform and EdgeLip over its top (the demo's brass 3 cm / 1 cm, R191's
            // walnut frame 15 cm / 4 cm), the square's wood behind it.
            float top = floor.Top;
            var arena = floor.Hidden ? FindFirstObjectByType<SwordFightArena>() : null;
            Color wood = Color.white;
            bool brass = arena != null && arena.Brass != null && arena.WoodAt(at, out wood);
            if (brass)
            {
                at += o * arena.EdgeOut;
                top += arena.EdgeLip;
                chipA = ChipsOf(arena.Brass.color);
                chipB = ChipsOf(wood);
                chipShare = 0.5f;
            }
            else
            {
                var pr = floor.Platform != null ? floor.Platform.GetComponent<Renderer>() : null;
                Color wall = pr != null && pr.sharedMaterial != null ? pr.sharedMaterial.color : new Color(0.8f, 0.82f, 0.84f);
                chipA = ChipsOf(wall);
                chipB = ChipsOf(wall, 0.85f);
                chipShare = 0.6f;
            }
            var g = new Breach { king = s, uses = s.EdgeUses, at = at, outward = o, topY = top, bottomY = floor.Bottom, seed = Random.Range(0f, 40f) };
            if (brass) { g.b.SetColor("_Inside", new Color(wood.r, wood.g, wood.b, 1f)); g.b.SetFloat("_Skin", 0.06f); }
            else { g.b.SetColor("_Inside", new Color(1f, 1f, 1f, 0f)); g.b.SetFloat("_Skin", 0f); }
            if (s.Fighter != null && s.Fighter.SwordBody != null)
            {
                var sw = s.Fighter.SwordBody.transform;
                var b = sw.Find("Blade");
                var gd = sw.Find("Guard");
                g.blade = b != null ? b.GetComponent<Renderer>() : null;
                g.guard = gd != null ? gd.GetComponent<Renderer>() : null;
            }
            if (BreachReady())
            {
                float half = BreachRadius * 1.2f + BreachCracks + 0.08f;
                g.face = BreachQuad("King breach (face)", out g.rf);
                g.face.transform.SetPositionAndRotation(at + o * 0.002f, Quaternion.LookRotation(o, Vector3.up));
                g.face.transform.localScale = new Vector3(2f * half, 2f * half, 1f);
                g.top = BreachQuad("King breach (top)", out g.rt);
                Vector3 lipTop = new Vector3(at.x, top + 0.002f, at.z);
                g.top.transform.SetPositionAndRotation(lipTop - o * (half * 0.5f), Quaternion.LookRotation(Vector3.up, -o));
                g.top.transform.localScale = new Vector3(2f * half, half, 1f);
            }
            breaches.Add(g);

            // What breaks off: a spray of grit and chips, a few heavy chunks, the lip bursting, two pieces of it, dust.
            Vector3 lip = new Vector3(at.x, top - 0.04f, at.z);
            WallChips(at + o * 0.04f, o + Vector3.up * 0.5f, 26, 4.4f, 0.75f, 0.012f, 0.05f);
            WallChips(at + o * 0.05f, o * 0.9f + Vector3.up * 0.25f, 7, 2.4f, 0.5f, 0.055f, 0.11f);
            WallChips(lip + o * 0.03f, o * 0.5f + Vector3.up, 8, 2.8f, 0.5f, 0.02f, 0.055f);
            LipPiece(lip + o * 0.02f - Vector3.up * 0.005f, o, 0.15f, 1f);
            LipPiece(lip + o * 0.02f + Vector3.Cross(Vector3.up, o) * Random.Range(-0.12f, 0.12f), o, 0.1f, 0.7f);
            kit.PuffBurst(at + o * 0.12f, ChipShade, 9, 0.5f, 0.16f, 0.7f, 0.2f);
            kit.PuffBurst(lip + o * 0.1f + Vector3.up * 0.05f, ChipPale, 5, 0.35f, 0.12f, 0.9f, 0.35f);
            // What lies round it: plates of the face sticking out, rubble thrown up onto the top.
            RimShards(g);
            WallChips(lip + Vector3.up * 0.03f - o * 0.02f, -o * 0.8f + Vector3.up * 1.3f, 8, 1.9f, 0.4f, 0.02f, 0.055f, g);

            kit.Run(60f, (f, dt) => StepBreach(g, f.age, dt));
        }

        GameObject BreachQuad(string name, out MeshRenderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(kit.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = kit.meshQuad;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = matBreach;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return go;
        }

        /// <summary>The sword came out (he sprang up) or he fell: the crater stays a while, then heals.</summary>
        void ReleaseBreach(SwordFightSkills s, bool torn)
        {
            foreach (var g in breaches)
            {
                if (g.king != s || g.released >= 0f) continue;
                g.released = kit.Clock;
                if (!torn) continue;
                // Torn out: chips, grit and dust spat out of it.
                WallChips(g.at + g.outward * 0.05f, g.outward + Vector3.up * 0.8f, 12, 3.2f, 0.6f, 0.015f, 0.05f);
                kit.PuffBurst(g.at + g.outward * 0.1f, ChipShade, 5, 0.35f, 0.12f, 0.5f, 0.25f);
            }
        }

        bool StepBreach(Breach g, float age, float dt)
        {
            var s = g.king;
            bool alive = s != null && s.Pawn != null;
            // Still in: the same stab, still hanging (online a client's copy does not know he has sprung: his spring and
            // his fall are events, ReleaseBreach).
            if (g.released < 0f && (!alive || s.EdgeUses != g.uses || s.EdgeStage == SfEdge.None)) g.released = kit.Clock;
            bool inStone = g.released < 0f;
            float heal = BreachHealed(g);
            if (heal >= 1f || age > 59f)
            {
                g.gone = true;
                if (g.face != null) Destroy(g.face);
                if (g.top != null) Destroy(g.top);
                breaches.Remove(g);
                return false;
            }
            if (g.rf != null)
            {
                float grown = Kit.EaseOut(age / (3f * F));
                float cracks = BreachCracks * Kit.EaseOut(age / (5f * F));
                // A fresh break glows gold a moment (the stab's heat), then it is stone.
                float glow = 1f - age / 0.22f;
                // 10-25, for real: no glow - the break is stone and wood from the first frame, its cracks dark.
                if (RealEdge(ChessFight.Network.PieceKind.King)) glow = 0f;
                var gold = kit.king;
                var b = g.b;
                b.SetVector("_Origin", g.at);
                b.SetVector("_Normal", g.outward);
                b.SetVector("_Box", new Vector4(g.topY, g.bottomY, 0f, 0f));
                b.SetVector("_Crater", new Vector4(BreachRadius, BreachDepth, grown, g.seed));
                b.SetVector("_Cracks", new Vector4(11f, cracks, 0.008f, 0.8f));
                b.SetFloat("_Heal", heal);
                b.SetColor("_Glow", new Color(gold.main.r * 1.6f, gold.main.g * 1.3f, gold.main.b * 0.7f, Mathf.Clamp01(glow) * (1f - heal)));
                b.SetFloat("_Debug", BreachDebug ? 1f : 0f);
                bool blade = inStone && alive && s.Fighter != null && s.Fighter.SwordBody != null && s.SwordAt != null;
                b.SetFloat("_BladeOn", blade ? 1f : 0f);
                if (blade)
                {
                    var sw = s.Fighter.SwordBody.transform;
                    b.SetVector("_BladePos", sw.position);
                    b.SetVector("_BladeX", sw.right);
                    b.SetVector("_BladeY", sw.up);
                    b.SetVector("_BladeZ", sw.forward);
                    if (g.blade != null) b.SetColor("_BladeColor", g.blade.sharedMaterial.color);
                    if (g.guard != null) b.SetColor("_GuardColor", g.guard.sharedMaterial.color);
                }
                b.SetFloat("_Surface", 0f);
                g.rf.SetPropertyBlock(b);
                b.SetFloat("_Surface", 1f);
                g.rt.SetPropertyBlock(b);
                g.rf.enabled = g.rt.enabled = true;
            }
            // While he hangs, grit trickles out of the rim.
            if (inStone)
            {
                g.trickle -= dt;
                if (g.trickle <= 0f)
                {
                    g.trickle = Random.Range(0.05f, 0.12f);
                    Vector3 side = Vector3.Cross(Vector3.up, g.outward);
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 rim = g.at + (side * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * (BreachRadius * 0.85f) + g.outward * 0.02f;
                    rim.y = Mathf.Min(rim.y, g.topY - 0.01f);
                    WallChips(rim, g.outward * 0.3f + Vector3.down * 0.2f, Random.value < 0.3f ? 2 : 1, 0.6f, 0.4f, 0.01f, 0.026f);
                }
            }
            return true;
        }

        /// <summary>
        /// <paramref name="n"/> chips of the wall thrown from <paramref name="at"/> along <paramref name="dir"/> (spread by
        /// <paramref name="spread"/>) at about <paramref name="speed"/> m/s, sized <paramref name="min"/> to
        /// <paramref name="max"/>: tumbling, falling away past the edge. One that comes down on the top lies there a moment, or,
        /// with <paramref name="stayWith"/>, until that breach heals (rubble round it).
        /// </summary>
        Material chipMat;

        /// <summary>10-25 (Sword Fight for real): chips, lip pieces and rim shards are lit as the board is (URP Lit, their own
        /// colour, no ink, no bands).</summary>
        Material ChipMaterial()
        {
            if (chipMat != null) return chipMat;
            chipMat = ChessFight.Game.LitMaterials.NewLit(Color.white, 0.25f);
            chipMat.hideFlags = HideFlags.HideAndDontSave;
            chipMat.name = "Real chips";
            return chipMat;
        }

        void WallChips(Vector3 at, Vector3 dir, int n, float speed, float spread, float min, float max, Breach stayWith = null)
        {
            var floor = SwordFightEdgeFloor.Current;
            float top = floor != null ? floor.Top : at.y - 1f, bottom = floor != null ? floor.Bottom : at.y - 1f;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            for (int i = 0; i < n; i++)
            {
                var mesh = kit.RoundBox(new Vector3(1f, Random.Range(0.4f, 0.8f), Random.Range(0.55f, 1f)), 0.1f);
                var chip = new Kit.Prop(kit, mesh, Random.value < chipShare ? chipA : chipB, "Wall chip", null, ChipMaterial()) { inkWidth = 0f };
                Vector3 v = (dir + Random.insideUnitSphere * spread).normalized * speed * Random.Range(0.5f, 1.1f);
                float size = Random.Range(min, max);
                Vector3 p = at;
                Quaternion rot = Random.rotation;
                Vector3 spin = Random.onUnitSphere * Random.Range(300f, 900f);
                bool landed = false, bounced = false;
                float life = stayWith != null ? 60f : Random.Range(1.1f, 1.6f);
                var f = kit.Run(life, (fx, d) =>
                {
                    if (!landed)
                    {
                        v += Physics.gravity * d;
                        p += v * d;
                        rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                        // Over the platform it lands on the top (once bouncing); past the edge it falls on.
                        if (floor != null && floor.OnFloor(p) && p.y <= top + size * 0.25f && v.y < 0f && p.y > bottom)
                        {
                            p.y = top + size * 0.25f;
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
                    float fade;
                    if (stayWith != null)
                    {
                        // Rubble lies until its breach heals; what fell past the edge is gone after a while.
                        if (!landed && (fx.age > 1.8f || p.y < bottom - 3f)) return false;
                        fade = landed ? BreachHealed(stayWith) : 0f;
                        if (fade >= 1f) return false;
                    }
                    else fade = fx.age > fx.life - 0.25f ? (fx.age - (fx.life - 0.25f)) / 0.25f : 0f;
                    chip.t.SetPositionAndRotation(p - Vector3.up * (size * 0.3f * fade), rot);
                    chip.t.localScale = Vector3.one * (size * (1f - fade) * Mathf.Min(1f, fx.age / (2f * F) + 0.4f));
                    chip.Apply();
                    return true;
                });
                f.props.Add(chip);
            }
        }

        /// <summary><paramref name="n"/> sparks flying from <paramref name="at"/> along <paramref name="dir"/> (and up): thin hot
        /// streaks stretched along their flight, gone in a few frames.</summary>
        void HotSparks(Vector3 at, Vector3 dir, int n, float speed)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            for (int i = 0; i < n; i++)
            {
                var spark = new Kit.Puff(kit, HotSpark, kit.meshBall) { ink = 0f, lump = 0f };
                Vector3 v = dir * Random.Range(0.6f, 1.2f) * speed + Vector3.up * Random.Range(0.4f, 2f) + Random.insideUnitSphere * 1.3f;
                Vector3 p = at;
                var f = kit.Run(Random.Range(0.12f, 0.28f), (fx, d) =>
                {
                    v += Physics.gravity * 0.6f * d;
                    p += v * d;
                    float k = fx.age / fx.life, w = 0.014f * (1f - k) + 0.004f;
                    spark.t.SetPositionAndRotation(p, Quaternion.LookRotation(v.sqrMagnitude > 1e-6f ? v : Vector3.up));
                    spark.t.localScale = new Vector3(w, w, w + 0.09f * Mathf.Min(1f, v.magnitude / 4f) * (1f - k));
                    spark.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(spark);
            }
        }

        /// <summary>A piece of the lip the crater bites out (<paramref name="size"/> m, thrown <paramref name="power"/> as hard):
        /// it lifts as it breaks, tips out and falls away tumbling.</summary>
        void LipPiece(Vector3 at, Vector3 o, float size, float power)
        {
            var mesh = kit.RoundBox(new Vector3(1f, 0.55f, 0.7f), 0.08f);
            var piece = new Kit.Prop(kit, mesh, chipA, "Lip piece", null, ChipMaterial()) { inkWidth = 0f };
            Vector3 side = Vector3.Cross(Vector3.up, o);
            Vector3 v = (o * 1.3f + Vector3.up * 1.4f + side * Random.Range(-0.5f, 0.5f)) * power;
            Vector3 p = at;
            Quaternion rot = Quaternion.LookRotation(o, Vector3.up) * Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f);
            Vector3 spin = side * Random.Range(320f, 560f) * (Random.value < 0.5f ? 1f : -1f) + Random.insideUnitSphere * 140f;
            var f = kit.Run(1.5f, (fx, d) =>
            {
                v += Physics.gravity * d;
                p += v * d;
                rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                float fade = fx.age > fx.life - 0.3f ? (fx.age - (fx.life - 0.3f)) / 0.3f : 0f;
                piece.t.SetPositionAndRotation(p, rot);
                piece.t.localScale = Vector3.one * (size * (1f - fade));
                piece.Apply();
                return true;
            });
            f.props.Add(piece);
        }

        /// <summary>Broken plates of the wall's face round the crater: their crater-side edges pushed out of the wall (the
        /// other half still in it), popping out as it breaks, staying until it heals, then sinking back in.</summary>
        void RimShards(Breach g)
        {
            var mesh = kit.RoundBox(new Vector3(1f, 0.2f, 0.75f), 0.05f);
            Vector3 o = g.outward, side = Vector3.Cross(Vector3.up, o);
            const int n = 10;
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float ang = (turn + i * 360f / n + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
                Vector3 r = side * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang);
                Vector3 p = g.at + r * (BreachRadius * Random.Range(0.92f, 1.1f));
                // None over the lip (no wall there) or past the bottom.
                if (p.y > g.topY - 0.035f || p.y < g.bottomY + 0.04f) continue;
                var shard = new Kit.Prop(kit, mesh, i % 3 == 0 ? chipB : chipA, "Rim shard", null, ChipMaterial()) { inkWidth = 0f };
                float size = Random.Range(0.09f, 0.16f), tilt = Random.Range(18f, 38f), yaw = Random.Range(-25f, 25f), roll = Random.Range(-15f, 15f);
                float delay = Random.Range(0f, 2f) * F;
                var f = kit.Run(60f, (fx, d) =>
                {
                    float heal = BreachHealed(g);
                    if (heal >= 1f) return false;
                    float a = (fx.age - delay) / F;
                    if (a < 0f) { shard.Show(false); return true; }
                    shard.Show(true);
                    // Out in four frames, past it and back (the face breaking), then still.
                    float pop = a < 4f ? Kit.EaseOutBack(a / 4f) : 1f;
                    // Its local z away from the crater, y out of the wall; the crater side tipped out.
                    Quaternion rot = Quaternion.LookRotation(r, o) * Quaternion.Euler(tilt * pop, yaw, roll * pop);
                    shard.t.SetPositionAndRotation(p + o * (0.014f * pop - 0.05f * heal) + r * 0.012f, rot);
                    shard.t.localScale = Vector3.one * (size * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(a / 3f)) * (1f - 0.6f * heal));
                    shard.Apply();
                    return true;
                });
                f.props.Add(shard);
            }
        }
    }
}
