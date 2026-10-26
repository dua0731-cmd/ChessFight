using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R130, the king's counter (승규 님 10-13: "킹이 왕의 반격을 할 때 시간을 더 늘려주고 더 연출과 액션을 했으면 좋겠어 아
    /// 막고 반격한다! 라는 연출을 그리고 cc 기들을 실제로 막아내는 장면을 연출 해줘 예를 들어 비숍이 관통핀 을 킹에게 했을 때
    /// 실제로 그런 걸 잘라내는 연출이나(원거리 공격) 퀸이 상대 킹(왕의 반격을 하고 있는)에게 체크메이트를 할 때 퀸이 날라가는
    /// 날라갈 때 되는 VFX 등등 각종 기물이 공격 할 때 반격 하는 걸 상호작용 하듯 해줘"), and R132 (승규 님 10-13: "왕은 룩의 F
    /// 스킬을 막지 못해 … 한 곳만 원형이 표시가 되는데 킹 주변을 위에는 제외 하고 양 옆 앞 뒤 전체가 방어 쉴드 구축 된 거
    /// 마냥 해줘"):
    /// - the guard (0.9 s): a shield wall goes up all round him, open above (<c>ChessFight/Skill Shield</c>: chessboard
    ///   squares of gold and ivory glass, an ink edge and a bright line in each), laid in a fifth of a second from the front
    ///   round to the back, bottom row first, each square white as it lands, a gold shock and dust at his feet; it shimmers
    ///   while he holds the guard and blinks in its last quarter second;
    /// - the block: a white ripple runs over the wall from where it was struck, a white-gold clash and sparks there, his
    ///   body flashing, a hold and a short slow beat (only a hold online) — and what it took: the queen's thrust or her edge
    ///   cut and the knight's dive or kick throw their piece off him trailing gold (<see cref="KingRepelFx"/>); the bishop's
    ///   hands reach his ankles and are cut off (<see cref="CutHands"/>);
    /// - a beat later the counter: one full spin cut round him, the wall bursting outward with it (its squares flying off),
    ///   the floor shock out to its reach, afterimages through the turn;
    /// - a guard that came to nothing crumbles grey from the top; the rook's wave (which the guard cannot stop, R132)
    ///   shatters it from the side it hit and throws him as it would anyone.
    /// The piece's mark over his head pops once, with the cast (R132: "f를 누르고 스킬을 사용할 때만"), not again here.
    /// R133 (승규 님 10-14: "킹 벽을 가장 위에 라인 지워줘 3층 정도만 쌓아두면 돼 그리고 전에 있었던 작은 네모난 색이 킹 주위를
    /// 도는 vfx 좋았으니까 그것도 해줘"): the wall is three rows of squares (its top row gone, the squares as they were), and
    /// R130's gold shards circle him again through the guard, just outside the wall now (<see cref="GuardShards"/>).
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>When a king's guard last cut a bishop's hands (that bishop's own miss is not drawn then).</summary>
        readonly Dictionary<SwordFightSkills, float> handsCut = new Dictionary<SwordFightSkills, float>();

        // ---------------------------------------------------------------- the shield (R132)

        /// <summary>The wall's radius and height (m), and how long it takes to go up (s). R133: three rows of the same
        /// squares (it was four, 1.75 m).</summary>
        const float ShieldRadius = 0.85f, ShieldHeight = 1.3125f, ShieldBuild = 0.2f;
        const int ShieldRows = 3;

        sealed class Shield
        {
            public GameObject go;
            public MeshRenderer r;
            public readonly MaterialPropertyBlock b = new MaterialPropertyBlock();
            public Vector3 front;
            /// <summary>0 up, 1 bursting with the counter, 2 crumbling (nothing came), 3 shattered (struck down through it).</summary>
            public int mode;
            public float modeAge, hitAge = 9f, hitU, hitV, hitPower, flashLeft, breakU, grey;
        }

        readonly Dictionary<RagdollPawn, Shield> shields = new Dictionary<RagdollPawn, Shield>();
        Material matShield;
        Mesh shieldMesh;
        bool shieldLooked;

        Material ShieldMaterial()
        {
            if (matShield != null || shieldLooked) return matShield;
            shieldLooked = true;
            var shader = Shader.Find("ChessFight/Skill Shield");
            if (shader == null) shader = Resources.Load<Shader>("SwordFightSkillFx/SkillShield");
            if (shader == null || !shader.isSupported) return null;
            matShield = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return matShield;
        }

        /// <summary>An open cylinder of radius 1, 1 tall, normals out (uv.x round it from +x toward +z, uv.y up).</summary>
        Mesh ShieldMesh()
        {
            if (shieldMesh != null) return shieldMesh;
            const int around = 48, rows = 10;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            for (int j = 0; j <= rows; j++)
                for (int i = 0; i <= around; i++)
                {
                    float a = i / (float)around * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(radial + Vector3.up * (j / (float)rows));
                    n.Add(radial);
                    uv.Add(new Vector2(i / (float)around, j / (float)rows));
                }
            int ring = around + 1;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < around; i++)
                {
                    int a = j * ring + i, c = a + ring;
                    // Wound to face out (normals out).
                    tri.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                }
            shieldMesh = new Mesh { name = "King guard shield", hideFlags = HideFlags.HideAndDontSave };
            shieldMesh.SetVertices(v);
            shieldMesh.SetNormals(n);
            shieldMesh.SetUVs(0, uv);
            shieldMesh.SetTriangles(tri, 0);
            shieldMesh.RecalculateBounds();
            return shieldMesh;
        }

        void DestroyShieldParts()
        {
            foreach (var sh in shields.Values) if (sh.go != null) Destroy(sh.go);
            shields.Clear();
            if (matShield != null) Destroy(matShield);
            if (shieldMesh != null) Destroy(shieldMesh);
        }

        /// <summary>Where round the wall a flat direction from its middle points (its uv.x).</summary>
        static float ShieldU(Vector3 dir)
        {
            float u = Mathf.Atan2(dir.z, dir.x) / (Mathf.PI * 2f);
            return u < 0f ? u + 1f : u;
        }

        /// <summary>
        /// The shield round a king in his guard (and through the beat after a block, until the counter bursts it): kept each
        /// frame by <see cref="KingMarks"/>. Let go of without a burst it crumbles (the guard came to nothing) or shatters
        /// (he was struck down through it).
        /// </summary>
        void GuardShield(SwordFightSkills s, SwordFightSkillParams S)
        {
            var mat = ShieldMaterial();
            if (mat == null) return;
            var owner = s;
            var pawn = s.Pawn;
            var fx = kit.Keep(s, "king shield", () => new Kit.Fx());
            if (fx.step != null) return;
            var sh = new Shield { front = Kit.FlatDir(pawn.Facing, Vector3.forward) };
            sh.go = new GameObject("King guard shield");
            sh.go.transform.SetParent(kit.root, false);
            sh.go.AddComponent<MeshFilter>().sharedMesh = ShieldMesh();
            sh.r = sh.go.AddComponent<MeshRenderer>();
            sh.r.sharedMaterial = mat;
            sh.r.shadowCastingMode = ShadowCastingMode.Off;
            sh.r.receiveShadows = false;
            sh.r.lightProbeUsage = LightProbeUsage.Off;
            sh.r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            sh.r.enabled = false;
            if (shields.TryGetValue(pawn, out var old) && old.go != null) Destroy(old.go);
            shields[pawn] = sh;
            // Going up: a gold shock and dust round his feet, his view kicked a little.
            Vector3 floor = Kit.FloorUnder(pawn.Hips.position, 0.6f);
            FloorShock(floor, 0.3f, ShieldRadius + 0.45f, kit.king, 6, 12, 0.22f);
            kit.DustRing(floor + Vector3.up * 0.03f, 7, ShieldRadius + 0.1f);
            Punch(pawn, null, 3f, 10);
            fx.end = () =>
            {
                if (sh.go != null) Destroy(sh.go);
                if (shields.TryGetValue(pawn, out var now) && now == sh) shields.Remove(pawn);
            };
            fx.step = (x, d) =>
            {
                if (pawn == null) return false;
                if (sh.mode == 0 && !kit.Held(x))
                {
                    if (owner.KingCountered) ShieldBurst(pawn);
                    else if (owner.Stage == SfStage.Recovery) ShieldCrumble(pawn);
                    else ShieldShatter(pawn, -sh.front);
                }
                if (sh.mode != 0) sh.modeAge += d;
                sh.hitAge += d;
                if (sh.flashLeft > 0f) sh.flashLeft -= d;
                float scale = 1f, alpha = 1f, broken = 0f;
                bool fromTop = false;
                switch (sh.mode)
                {
                    case 1:
                    {
                        // Burst with the counter: out to well past his reach in a fifth of a second, its squares flying off.
                        float k = Mathf.Clamp01(sh.modeAge / 0.22f);
                        scale = 1f + 2.1f * Kit.EaseOut(k);
                        alpha = 1f - k * k;
                        broken = k;
                        if (sh.modeAge > 0.24f) return false;
                        break;
                    }
                    case 2:
                    {
                        float k = Mathf.Clamp01(sh.modeAge / 0.32f);
                        sh.grey = Mathf.Min(1f, sh.modeAge / 0.08f);
                        broken = k;
                        fromTop = true;
                        alpha = 1f - 0.4f * k;
                        if (sh.modeAge > 0.34f) return false;
                        break;
                    }
                    case 3:
                    {
                        float k = Mathf.Clamp01(sh.modeAge / 0.14f);
                        broken = k;
                        if (sh.modeAge > 0.16f) return false;
                        break;
                    }
                }
                // Blinking in the guard's last quarter second.
                float left = S != null ? S.kingGuard - owner.StageTime : 1f;
                bool blink = sh.mode == 0 && owner.Stage == SfStage.Windup && left < 0.25f && ((int)(kit.Clock / (2f * F)) & 1) == 0;
                Vector3 at = Kit.FloorUnder(pawn.Hips.position, 0.6f) + Vector3.up * 0.01f;
                float radius = ShieldRadius * scale;
                sh.go.transform.SetPositionAndRotation(at, Quaternion.identity);
                sh.go.transform.localScale = new Vector3(radius, ShieldHeight, radius);
                var b = sh.b;
                // The king's own gold, ivory squares between, his ink, a bright line in his light gold.
                b.SetColor("_Gold", kit.king.main);
                b.SetColor("_Ink", kit.king.ink);
                b.SetColor("_Neon", new Color(kit.king.light.r * 1.45f, kit.king.light.g * 1.4f, kit.king.light.b * 1.3f, 1f));
                b.SetFloat("_Radius", radius);
                b.SetFloat("_Height", ShieldHeight);
                b.SetFloat("_Rows", ShieldRows);
                b.SetFloat("_Front", ShieldU(sh.front));
                b.SetFloat("_Build", Mathf.Min(1.05f, x.age / ShieldBuild));
                b.SetVector("_Hit", new Vector4(sh.hitU, sh.hitV, sh.hitAge, sh.hitPower));
                b.SetFloat("_Flash", sh.flashLeft > 0f ? 1f : blink ? 0.45f : 0f);
                b.SetFloat("_Break", broken);
                b.SetFloat("_BreakU", sh.breakU);
                b.SetFloat("_BreakTop", fromTop ? 1f : 0f);
                b.SetFloat("_Grey", sh.grey);
                b.SetFloat("_Alpha", alpha);
                b.SetFloat("_Sheen", x.age * 2.6f);
                sh.r.SetPropertyBlock(b);
                sh.r.enabled = true;
                return true;
            };
        }

        /// <summary>
        /// R130's gold shards, back in R133 (승규 님: "작은 네모난 색이 킹 주위를 도는 vfx 좋았으니까 그것도 해줘"): six
        /// little gold squares circling him at chest height through the guard, just outside the wall now (inside it they
        /// hid behind its glass), bobbing, blinking with it in its last quarter second; the counter throws them out with the
        /// wall, otherwise they shrink away.
        /// </summary>
        void GuardShards(SwordFightSkills s, SwordFightSkillParams S)
        {
            var owner = s;
            var pawn = s.Pawn;
            kit.Keep(s, "king shards", () =>
            {
                var fx = new Kit.Fx();
                var shard = kit.RoundBox(new Vector3(0.13f, 0.22f, 0.035f), 0.015f);
                for (int i = 0; i < 6; i++) fx.props.Add(new Kit.Prop(kit, shard, kit.king, "Guard shard") { inkWidth = 0.006f });
                float released = -1f;
                bool burst = false;
                fx.step = (x, d) =>
                {
                    if (pawn == null) return false;
                    if (!kit.Held(x) && released < 0f)
                    {
                        released = x.age;
                        burst = owner.KingCountered;
                    }
                    float a = released >= 0f ? (x.age - released) / F : -1f;
                    if (a >= 10f) return false;
                    Vector3 c = pawn.bodies[(int)BodyId.Chest].position;
                    float left = S != null ? S.kingGuard - owner.StageTime : 1f;
                    bool blink = released < 0f && owner.Stage == SfStage.Windup && left < 0.25f && ((int)(kit.Clock / (2f * F)) & 1) == 0;
                    for (int i = 0; i < x.props.Count; i++)
                    {
                        var p = x.props[i];
                        float ang = x.age * 3.2f + i * Mathf.PI / 3f;
                        Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        float r = ShieldRadius + 0.17f + (burst && a >= 0f ? 1.6f * Kit.EaseOut(a / 10f) : 0f);
                        Vector3 at = c + radial * r + Vector3.up * (0.06f * Mathf.Sin(x.age * 5f + i));
                        p.t.SetPositionAndRotation(at, Quaternion.LookRotation(radial, Vector3.up));
                        float pop = Kit.Pop(x.age, 1.2f) * (a < 0f ? 1f : burst ? 1f - a / 10f : 1f - a / 6f);
                        p.t.localScale = Vector3.one * Mathf.Max(0.001f, pop);
                        p.flash = blink || (a >= 0f && a < 2f) ? 1f : 0f;
                        p.rim = new Color(kit.king.light.r * 0.5f, kit.king.light.g * 0.5f, kit.king.light.b * 0.5f);
                        p.Show(pop > 0.02f);
                        p.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>A blow on the wall from <paramref name="toward"/> (flat, from his middle toward it) at the height of
        /// <paramref name="at"/>: a white ripple from there, the struck squares white, the wall white for two frames.</summary>
        void ShieldHit(RagdollPawn king, Vector3 toward, Vector3 at)
        {
            if (king == null || !shields.TryGetValue(king, out var sh) || sh.mode != 0) return;
            Vector3 floor = Kit.FloorUnder(king.Hips.position, 0.6f);
            sh.hitU = ShieldU(toward);
            sh.hitV = Mathf.Clamp01((at.y - floor.y) / ShieldHeight);
            sh.hitAge = 0f;
            sh.hitPower = 1f;
            sh.flashLeft = 2f * F;
        }

        /// <summary>Where a blow from <paramref name="toward"/> meets the wall, at the height of <paramref name="at"/>.</summary>
        Vector3 OnShield(RagdollPawn king, Vector3 toward, Vector3 at)
        {
            Vector3 c = king.Hips.position;
            Vector3 p = new Vector3(c.x, 0f, c.z) + toward * ShieldRadius;
            float floor = Kit.FloorUnder(c, 0.6f).y;
            p.y = Mathf.Clamp(at.y, floor + 0.15f, floor + ShieldHeight - 0.1f);
            return p;
        }

        /// <summary>The counter bursts the wall outward: its squares fly off in gold all round.</summary>
        void ShieldBurst(RagdollPawn king)
        {
            if (king == null || !shields.TryGetValue(king, out var sh) || sh.mode != 0) return;
            sh.mode = 1;
            sh.modeAge = 0f;
            sh.flashLeft = 2f * F;
            sh.breakU = ShieldU(sh.front);
            Vector3 c = Kit.FloorUnder(king.Hips.position, 0.6f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + 0.3f;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                kit.Drops(c + radial * ShieldRadius + Vector3.up * (0.5f + 0.25f * (i % 3)), radial + Vector3.up * 0.35f, i % 2 == 0 ? kit.king : kit.queen, 3, 7.5f);
            }
        }

        /// <summary>The guard came to nothing: the wall greys and crumbles from the top, a little dust at its foot.</summary>
        void ShieldCrumble(RagdollPawn king)
        {
            if (king == null || !shields.TryGetValue(king, out var sh) || sh.mode != 0) return;
            sh.mode = 2;
            sh.modeAge = 0f;
            Vector3 c = Kit.FloorUnder(king.Hips.position, 0.6f);
            kit.PuffBurst(c + Vector3.up * 0.15f, kit.grey, 5, ShieldRadius, 0.14f, 0.4f, 0.3f);
        }

        /// <summary>
        /// He was struck down through his guard (R132: the rook's wave, which it cannot stop): the wall shatters from the side
        /// the blow came from (<paramref name="along"/>: the blow's way), white, its squares thrown on with it.
        /// </summary>
        void ShieldShatter(RagdollPawn king, Vector3 along)
        {
            if (king == null || !shields.TryGetValue(king, out var sh) || sh.mode != 0) return;
            along = Kit.FlatDir(along, -sh.front);
            sh.mode = 3;
            sh.modeAge = 0f;
            sh.flashLeft = 2f * F;
            sh.breakU = ShieldU(-along);
            Vector3 c = Kit.FloorUnder(king.Hips.position, 0.6f);
            Vector3 face = c - along * ShieldRadius + Vector3.up * 0.8f;
            kit.ImpactRing(face, -along, kit.king, 0.25f, 0.95f);
            kit.Drops(face, along + Vector3.up * 0.5f, kit.king, 6, 8.5f);
            kit.Drops(face + Vector3.up * 0.4f, along + Vector3.up * 0.2f, kit.queen, 5, 7.5f);
            kit.Drops(face, along, Kit.Stone, 4, 6f);
        }

        // ---------------------------------------------------------------- the block

        void KingBlock(SfFxEvent e)
        {
            var s = e.by;
            var king = s.Pawn;
            if (king == null) return;
            var kind = (SfBlock)e.order;
            var pal = kit.king;
            var attacker = e.target;
            Vector3 toward = Kit.FlatDir(e.dir, king.Facing);
            switch (kind)
            {
                case SfBlock.Bishop:
                {
                    // The hands are still on their way: the cut (and its ripple, hold and beat) comes when they get there.
                    var bishop = attacker != null ? attacker.GetComponent<SwordFightSkills>() : null;
                    if (bishop != null) handsCut[bishop] = kit.Clock;
                    CutHands(bishop, king);
                    return;
                }
                case SfBlock.Knight:
                case SfBlock.KnightEdge:
                    kit.ImpactRing(king.bodies[(int)BodyId.Head].position + Vector3.up * 0.35f, Vector3.up, pal, 0.3f, 1f);
                    break;
            }
            Vector3 at = OnShield(king, toward, e.at);
            ShieldHit(king, toward, at);
            Clash(at, toward, pal);
            BlockBeat(king, attacker);
        }

        /// <summary>What every block does once it lands: his body flashes, an afterimage swells off him, a hold, a short
        /// slow beat with the view pulled in (online: a hold only), a shake and a gold wash.</summary>
        void BlockBeat(RagdollPawn king, RagdollPawn attacker, bool afterimage = true)
        {
            var pal = kit.king;
            kit.FlashBody(king, pal.light);
            if (afterimage) Afterimage(king, pal, 1.55f, 0.26f, 0.65f, true);
            HitStop(6, king, attacker);
            if (Online) HitStop(OnlineBeatFrames, king, attacker);
            else SlowMo(king, attacker, 0.18f, 0.4f);
            Pull(king, attacker, 6f, 0.18f);
            Shake(king, attacker, 0.15f, 9);
            Tint(king, attacker, pal.main, 0.32f, 20);
        }

        /// <summary>The clash where an attack met his guard: a white-gold ring facing back the way it came, gold sparks
        /// flying back off it, a puff.</summary>
        void Clash(Vector3 at, Vector3 toward, Kit.Palette pal)
        {
            kit.ImpactRing(at, toward, pal, 0.22f, 0.8f);
            kit.ImpactRing(at, toward, new Kit.Palette { main = Color.white, light = Color.white, deep = pal.light, ink = pal.ink }, 0.12f, 0.45f);
            kit.Drops(at, toward + Vector3.up * 0.6f, pal, 6, 8.5f);
            kit.PuffBurst(at, kit.dust, 3, 0.35f, 0.12f, 0.35f, 0.2f);
        }

        /// <summary>
        /// The bishop's hands cut off (R130, "비숍이 관통핀 을 킹에게 했을 때 실제로 그런 걸 잘라내는 연출"): the two gold hands
        /// fly from beside the bishop for his ankles on their diagonals as they would to pin him; as they arrive two gold
        /// cuts flash across at his ankles, the hands burst into gold and violet bits and their arms snap back to the bishop.
        /// The block's ripple on the shield, its hold and its beat land with the cut.
        /// </summary>
        void CutHands(SwordFightSkills bishop, RagdollPawn king)
        {
            if (bishop == null || bishop.Pawn == null || king == null)
            {
                if (king != null && Real(PieceKind.King)) RealBlockBeat(king, bishop != null ? bishop.Pawn : null);
                else if (king != null) BlockBeat(king, bishop != null ? bishop.Pawn : null);
                return;
            }
            var hands = new[] { new GrabHand(kit, "Bishop hand L (cut)"), new GrabHand(kit, "Bishop hand R (cut)") };
            var arms = new[] { ArmStrip(), ArmStrip() };
            var starts = new Vector3[2];
            var ends = new Vector3[2];
            var ins = new Vector3[2];
            var c1 = new Vector3[2];
            var c2 = new Vector3[2];
            Vector3 dir = bishop.Dir;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                starts[i] = HandStart(bishop, side);
                ends[i] = AnkleGrip(king, dir, side, out ins[i]);
                ReachControls(starts[i], ends[i], dir, ins[i], side, out c1[i], out c2[i]);
            }
            var pts = new List<Vector3>();
            bool cut = false;
            var spin = new Vector3[2];
            var off = new Vector3[2];
            var f = kit.Run(28f * F, (fx, d) =>
            {
                float a = fx.age / F;
                // Out in nine frames; cut; the arms snap back over eight and are gone, the cut-off hands tumble away.
                float t = a < HandFly ? FlyEase(a) : 1f - Kit.EaseIn(Mathf.Clamp01((a - HandFly) / 8f));
                for (int i = 0; i < 2; i++)
                {
                    Vector3 at = Bezier(starts[i], c1[i], c2[i], ends[i], t);
                    Quaternion rot = FlyRotation(BezierTangent(starts[i], c1[i], c2[i], ends[i], Mathf.Clamp(t, 0.02f, 0.98f)), dir);
                    if (a < HandFly) hands[i].Place(at, rot, -15f, 0f, Mathf.Lerp(HandReady, HandHold, t));
                    else
                    {
                        // Severed: thrown off up and out, turning over, white for two frames, shrinking away by frame 17.
                        float b = (a - HandFly) * F;
                        Vector3 p = ends[i] + off[i] * b + Vector3.up * (2.6f * b - 4.9f * b * b);
                        Quaternion r = Quaternion.Euler(spin[i] * (a - HandFly) * 9f) * FlyRotation(ins[i], dir);
                        float s = HandHold * Mathf.Clamp01(1f - (a - HandFly) / 17f);
                        hands[i].Place(p, r, 40f, a - HandFly < 2f ? 1f : 0f, Mathf.Max(0.001f, s));
                    }
                    pts.Clear();
                    for (int j = 0; j <= 12; j++) pts.Add(Bezier(starts[i], c1[i], c2[i], ends[i], t * j / 12f));
                    if (t > 0.05f)
                    {
                        arms[i].Build(pts, j => Mathf.Lerp(0.09f, 0.16f, j / 12f), kit.Eye);
                        arms[i].Apply();
                    }
                    else arms[i].Hide();
                }
                if (!cut && a >= HandFly && Real(PieceKind.King))
                {
                    // 10-25, for real: his blade through both wrists at his ankles, steel sparks off it, the beat.
                    cut = true;
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 h = ends[i] + Vector3.up * 0.12f;
                        SteelClash(h, -ins[i], 0.6f);
                        off[i] = (-ins[i] + Vector3.Cross(Vector3.up, ins[i]) * (i == 0 ? 0.6f : -0.6f)).normalized * 2.2f;
                        spin[i] = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 60f;
                    }
                    RealBlockBeat(king, bishop.Pawn);
                }
                if (!cut && a >= HandFly)
                {
                    cut = true;
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 h = ends[i] + Vector3.up * 0.12f;
                        // An X of two cuts across the wrist (the arm is cut through).
                        Slash(h, ins[i], kit.king, 1.5f);
                        Slash(h, Vector3.Cross(Vector3.up, ins[i]), kit.king, 1.1f);
                        kit.PuffBurst(h, kit.bishop, 5, 0.4f, 0.13f, 0.45f, 0.35f);
                        kit.Drops(h, Vector3.up + ins[i] * -0.5f, kit.queen, 5, 7f);
                        kit.ImpactRing(h, Vector3.up, kit.king, 0.12f, 0.5f);
                        off[i] = (-ins[i] + Vector3.Cross(Vector3.up, ins[i]) * (i == 0 ? 0.6f : -0.6f)).normalized * 2.2f;
                        spin[i] = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 60f;
                    }
                    // One low cut right round him at ankle height: the sword that took them; the wall ripples there.
                    Vector3 feet = Kit.FloorUnder(king.Hips.position, 0.6f);
                    SpinCutAt(king, feet + Vector3.up * 0.18f, kit.king, 0.75f);
                    Vector3 fromBishop = Kit.FlatDir(bishop.Pawn.Hips.position - king.Hips.position, -dir);
                    ShieldHit(king, fromBishop, feet + Vector3.up * 0.2f);
                    // (No swelling afterimage here: it would hide the hands being cut.)
                    BlockBeat(king, bishop.Pawn, false);
                }
                return true;
            });
            f.strips.AddRange(arms);
            f.end = () => { foreach (var h in hands) h.Destroy(); };
        }

        /// <summary>A full-circle cut round <paramref name="pawn"/> at <paramref name="c"/>'s height (the ankles), around in
        /// four frames, held, eaten from its start (SpinCut's, lower and smaller).</summary>
        void SpinCutAt(RagdollPawn pawn, Vector3 c, Kit.Palette p, float radius)
        {
            if (pawn == null) return;
            float start = Mathf.Atan2(pawn.Facing.z, pawn.Facing.x);
            var pts = new List<Vector3>();
            for (int i = 0; i <= 32; i++)
            {
                float ang = start - i / 32f * Mathf.PI * 2f;
                pts.Add(c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius);
            }
            var strip = new Kit.Strip(kit, p) { core = Color.white, coreShare = 0.5f, inkShare = 0.2f };
            var f = kit.Run(14f * F, (fx, d) =>
            {
                float a = fx.age / F;
                strip.Build(pts, i => 0.08f + 0.14f * i / 32f, kit.Eye, true);
                strip.head = Kit.EaseOut(a / 4f);
                strip.tail = a < 7f ? 0f : Kit.EaseIn((a - 7f) / 7f);
                strip.core = a < 2f ? Color.white : p.light;
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
        }

        /// <summary>An attacker thrown off the king's block (R130): flashed in his gold, a gold ring and sparks where it was
        /// struck, a gold ribbon after it as it flies with afterimages (the queen's edge cut, flung hardest: a wider ribbon
        /// for longer).</summary>
        void KingRepelFx(SfFxEvent e)
        {
            var pawn = e.target;
            if (pawn == null) return;
            var kind = (SfBlock)e.order;
            bool hard = kind == SfBlock.QueenEdge;
            kit.FlashBody(pawn, kit.king.main);
            kit.ImpactRing(e.at, e.dir, kit.king, 0.2f, hard ? 0.9f : 0.65f);
            kit.Drops(e.at, e.dir + Vector3.up * 0.5f, kit.king, hard ? 6 : 4, 8f);
            float until = hard ? 0.9f : 0.55f;
            Trail(e.by, pawn, kit.king, hard ? 0.5f : 0.36f, null, 2, until + 0.35f);
        }

        // ---------------------------------------------------------------- the counter

        /// <summary>The counter a beat after the block: one full spin cut round him at sword height, a second wider one,
        /// the wall bursting outward, the floor shock out to its reach with dust, afterimages through the turn, a shake.</summary>
        void KingSpin(SfFxEvent e)
        {
            var s = e.by;
            var king = s.Pawn;
            if (king == null) return;
            float R = e.size > 0f ? e.size : 2.5f;
            ShieldBurst(king);
            SpinCut(king, kit.king, Mathf.Min(1.35f, R * 0.54f));
            kit.Run(5f * F, (fx, d) =>
            {
                if (fx.age < 4f * F || king == null) return true;
                SpinCut(king, kit.king, Mathf.Min(1.9f, R * 0.76f));
                return false;
            });
            KingWave(s);
            float next = 0f;
            kit.Run(0.36f, (fx, d) =>
            {
                if (king == null) return false;
                if (fx.age >= next)
                {
                    Afterimage(king, kit.king, 1.15f, 0.22f, 0.5f);
                    next = fx.age + 3f * F;
                }
                return true;
            });
            HitStop(3, king);
            Shake(king, null, 0.16f, 9);
            Punch(king, null, 6f, 14);
        }
    }
}
