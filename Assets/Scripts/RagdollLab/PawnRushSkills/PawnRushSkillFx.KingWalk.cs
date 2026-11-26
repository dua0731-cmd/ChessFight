using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 킹 워크 on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.KingWalk.cs).
    ///
    ///   the king      drawn 1.5x his size (1.8x once the majority walks with him), a fanfare at the key
    ///   the carpet    a red carpet with pale edges unrolling 100 m behind him in 0.7 s, laid the way the host lays it,
    ///                 then following his walk; rolled away at the end
    ///   the train     a teammate on the carpet: a teal ring and a streak while it is pulled in, "합류!" and a chime up
    ///                 the scale for each; four of six: "과반!" and the king grows; one knocked out of it: a grey puff
    ///   the crown     its 0.4 s warning a fan on the floor ahead (red for the other team, teal for his); the shock wave
    ///                 an arc of light running out over the fan, a burst at the king
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class KwFx
        {
            public readonly List<Vector3> carpet = new List<Vector3>();
            public GameObject carpetObject;
            public Mesh carpetMesh;
            public float laidAt, endAt = -1f;
        }

        Material matCarpet, matCarpetEdge, matFan;
        Mesh meshFan;

        void KwCast(UltShow u)
        {
            var f = new KwFx { laidAt = Clock };
            u.state = f;
            var s = Params;
            RagdollPawn.KwLayCarpet(u.castAt, u.castDir, s != null ? s.kwCarpet : 100f, f.carpet);
            if (matCarpet == null) matCarpet = LitMaterials.NewLit(new Color(0.58f, 0.04f, 0.07f), 0.35f);
            if (matCarpetEdge == null) matCarpetEdge = LitMaterials.NewLit(new Color(0.95f, 0.9f, 0.82f), 0.5f);
            f.carpetObject = new GameObject("King's carpet");
            f.carpetObject.transform.SetParent(root, false);
            f.carpetMesh = new Mesh { name = "King's carpet", hideFlags = HideFlags.HideAndDontSave };
            f.carpetMesh.MarkDynamic();
            f.carpetObject.AddComponent<MeshFilter>().sharedMesh = f.carpetMesh;
            var mr = f.carpetObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { matCarpet, matCarpetEdge };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The fanfare: a brass chord climbing (G, C, E, G).
            UltAudioBuild();
            UltPlay(sfxFanfare, 0.9f);
        }

        void KwStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is KwFx f) || u.by == null) return;
            var s = Params;
            bool on = u.Playing && s != null;
            if (on)
            {
                RagdollPawn.KwExtendCarpet(f.carpet, FloorUnder(u.by), s.kwCarpet);
                bool grown = (u.by.PlanNote & RagdollPawn.KwGrown) != 0;
                WantGiant(u.by, grown ? s.kwGrowBig : s.kwGrow, 1f / 0.35f);
            }
            else if (f.endAt < 0f) f.endAt = Clock;
            // The carpet: unrolled in 0.7 s; rolled up again (from its far end) in 0.8 s once it is over.
            float shown = (s != null ? s.kwCarpet : 100f) * Smooth01((Clock - f.laidAt) / 0.7f);
            if (f.endAt >= 0f) shown *= 1f - Smooth01((Clock - f.endAt) / 0.8f);
            KwBuildCarpet(f, shown, s != null ? s.kwHalfWidth : 1.6f);
            if (f.endAt >= 0f && Clock - f.endAt > 0.85f && f.carpetObject != null)
            {
                Destroy(f.carpetObject);
                Destroy(f.carpetMesh);
                f.carpetObject = null;
            }
        }

        /// <summary>The carpet's ribbon (and its two pale edges) along the first <paramref name="length"/> m of it.</summary>
        void KwBuildCarpet(KwFx f, float length, float half)
        {
            if (f.carpetMesh == null) return;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var cloth = new List<int>();
            var edge = new List<int>();
            float run = 0f;
            int count = f.carpet.Count;
            for (int i = 0; i < count && run <= length; i++)
            {
                Vector3 p = f.carpet[i];
                if (i > 0) run += Vector3.Distance(f.carpet[i - 1], p);
                if (run > length && i > 0)
                {
                    float over = run - length, seg = Vector3.Distance(f.carpet[i - 1], p);
                    p = Vector3.Lerp(p, f.carpet[i - 1], seg > 1e-4f ? over / seg : 0f);
                }
                Vector3 a = i + 1 < count ? f.carpet[i + 1] : p, b = i > 0 ? f.carpet[i - 1] : p;
                Vector3 along = Flat(b - a);
                along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward;
                Vector3 side = new Vector3(along.z, 0f, -along.x), up = p + Vector3.up * 0.035f;
                // Five across: the left edge, the cloth's left, its right, the right edge's inside and out.
                v.Add(up - side * (half + 0.18f));
                v.Add(up - side * half);
                v.Add(up + side * half);
                v.Add(up + side * (half + 0.18f));
                for (int k = 0; k < 4; k++) n.Add(Vector3.up);
                float w = run / 2.2f;
                uv.Add(new Vector2(0f, w)); uv.Add(new Vector2(0.1f, w)); uv.Add(new Vector2(0.9f, w)); uv.Add(new Vector2(1f, w));
                if (i == 0) continue;
                int c = v.Count - 4, pv = c - 4;
                cloth.AddRange(new[] { pv + 1, c + 1, pv + 2, pv + 2, c + 1, c + 2 });
                edge.AddRange(new[] { pv, c, pv + 1, pv + 1, c, c + 1 });
                edge.AddRange(new[] { pv + 2, c + 2, pv + 3, pv + 3, c + 2, c + 3 });
            }
            f.carpetMesh.Clear();
            if (v.Count < 8) return;
            f.carpetMesh.SetVertices(v);
            f.carpetMesh.SetNormals(n);
            f.carpetMesh.SetUVs(0, uv);
            f.carpetMesh.subMeshCount = 2;
            f.carpetMesh.SetTriangles(cloth, 0);
            f.carpetMesh.SetTriangles(edge, 1);
            f.carpetMesh.RecalculateBounds();
        }

        void DestroyCarpets()
        {
            foreach (var u in ults)
                if (u.state is KwFx f)
                {
                    if (f.carpetObject != null) Destroy(f.carpetObject);
                    if (f.carpetMesh != null) Destroy(f.carpetMesh);
                }
            if (meshFan != null) Destroy(meshFan);
        }

        void KwStateFx(UltShow u, SkillFxEvent e)
        {
            switch (e.count)
            {
                case 1:   // a teammate on the carpet, pulled in
                    if (e.target == null) break;
                    var mate = e.target;
                    float until = Clock + 1.6f;
                    anims.Add(new Streak(root, matTrail, HdrA(Teal, 2.4f), 0.45f, 0.4f, () => mate != null && Clock < until ? ChestOf(mate) : (Vector3?)null));
                    UltRing(() => mate != null && Clock < until ? FloorUnder(mate) + Vector3.up * 0.1f : (Vector3?)null, Teal, 1.2f, () => Clock < until);
                    UltPlay(sfxChime[2], 0.6f);
                    break;
                case 2:   // in the train
                    if (e.target == null) break;
                    UltPopWord(HeadOf(e.target) + Vector3.up * 1.1f, "합류!", HdrA(Teal, 1.4f), 0.55f, 1f);
                    UltPlay(sfxChime[Mathf.Clamp(Mathf.RoundToInt(e.size) + 2, 0, sfxChime.Length - 1)], 0.8f);
                    break;
                case 3:   // the majority
                {
                    Vector3 at = u.by != null ? HeadOf(u.by) : e.at;
                    Flare(at + Vector3.up, Color.white, 5f, 14f, 0.45f);
                    UltSquareWave(Ground(e.at), FamSquare * 0.5f, 14f, 0.8f, Teal, 3f);
                    UltPopWord(at + Vector3.up * 2.5f, "과반!", HdrA(GradeColors[0], 1.6f), 1.1f, 1.6f);
                    UltPlay(sfxFanfare, 1f, 0f, 1.12f);
                    HitStop(0.08f);
                    break;
                }
                case 4:   // knocked out of the train
                    if (e.target == null) break;
                    DustRing(FloorUnder(e.target), 8, 0.4f, 2.5f, new Color(0.6f, 0.6f, 0.62f), 0.6f, 0.6f);
                    UltPopWord(HeadOf(e.target) + Vector3.up * 1f, "떨어짐", new Color(0.75f, 0.75f, 0.78f), 0.45f, 0.9f);
                    UltPlay(sfxOff, 0.6f);
                    break;
            }
        }

        /// <summary>A 30 m fan on the floor ahead for the wave's 0.4 s warning.</summary>
        void KwWarnFx(UltShow u, SkillFxEvent e)
        {
            BuildFan();
            float reach = Mathf.Max(5f, e.size), warn = Params != null ? Params.kwWarn : 0.4f;
            bool mine = Viewer == u.by || Viewer != null && u.by != null && !Teams.AreEnemies(Viewer.Team, u.by.Team);
            Vector3 dir = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Add(new Shape(root, "Kw wave fan", meshFan, matFan)
            {
                life = warn + 0.05f, at = Ground(e.at) + Vector3.up * 0.07f, rotation = Quaternion.LookRotation(dir, Vector3.up), scale = new Vector3(reach, 1f, reach),
                animate = (s, t) =>
                {
                    s.color = mine ? HdrA(Teal, 1.5f) : HdrA(Blood, 1.8f);
                    s.bright = Pop(s.Age, 0.06f) * (0.55f + 0.35f * Mathf.Sin(Clock * 28f));
                },
            });
            if (u.by != null) Flare(HeadOf(u.by) + Vector3.up * 0.6f, TealPale, 1.8f, 4f, warn);
            UltPlay(sfxPerfect, 0.6f, 0f, 0.8f);
        }

        /// <summary>The crown's shock wave: an arc of light running out over the fan, a burst at the king.</summary>
        void KwLineFx(UltShow u, SkillFxEvent e)
        {
            BuildFan();
            float reach = Mathf.Max(5f, e.size);
            Vector3 dir = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward, at = Ground(e.at) + Vector3.up * 0.09f;
            Add(new Shape(root, "Kw wave", meshFan, matFan)
            {
                life = 0.55f, at = at, rotation = Quaternion.LookRotation(dir, Vector3.up),
                animate = (s, t) =>
                {
                    float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(s.Age / 0.35f), 3f);
                    s.scale = new Vector3(reach * k, 1f, reach * k);
                    s.color = HdrA(Color.Lerp(Color.white, Teal, k), 2.6f);
                    s.bright = 1f - Mathf.Clamp01((s.Age - 0.25f) / 0.3f);
                },
            });
            Vector3 crown = u.by != null ? HeadOf(u.by) + Vector3.up * 0.8f : at + Vector3.up * 3f;
            Flare(crown, Color.white, 4.5f, 12f, 0.35f);
            SparkBurst(crown, 50, TealPale, dir + Vector3.up * 0.3f, 50f, 6f, 15f, 0.6f, 0.05f);
            DustRing(Ground(e.at), 16, 1f, 8f, new Color(0.55f, 0.55f, 0.57f), 0.9f, 0.9f);
            UltSound(UltSfx.Boom, at);
            UltPlay(sfxGrade[1], 0.7f, 0.05f);
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, at) / 35f) : 0.5f;
            Shake(Viewer == u.by ? 0.2f : 0.16f * near, 0.45f);
        }

        void KwStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null) return;
            Vector3 at = ChestOf(e.target);
            Flare(at, Blood, 3f, 7f, 0.3f);
            SparkBurst(at, 24, Blood, e.dir + Vector3.up * 0.5f, 60f, 4f, 11f, 0.6f, 0.05f);
            UltSound(UltSfx.Stomp, at, e.count);
            UltScreenStrike(u, e);
        }

        /// <summary>A flat fan of radius 1 over ±kwWaveHalf about +Z, brighter towards its rim (the wave and its warning).</summary>
        void BuildFan()
        {
            if (meshFan != null) return;
            float half = (Params != null ? Params.kwWaveHalf : 35f) * Mathf.Deg2Rad;
            const int Segs = 24, Rings = 4;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var tri = new List<int>();
            for (int r = 0; r <= Rings; r++)
            {
                float rad = r / (float)Rings;
                for (int i = 0; i <= Segs; i++)
                {
                    float a = Mathf.Lerp(-half, half, i / (float)Segs);
                    v.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * rad);
                    float edge = Mathf.Max(Mathf.Pow(rad, 3f), Mathf.Abs(i / (float)Segs - 0.5f) > 0.47f ? 0.8f : 0f);
                    c.Add(new Color(1f, 1f, 1f, 0.12f + 0.88f * edge));
                }
            }
            for (int r = 0; r < Rings; r++)
                for (int i = 0; i < Segs; i++)
                {
                    int a = r * (Segs + 1) + i, b = a + Segs + 1;
                    tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            meshFan = new Mesh { name = "Ult fan", hideFlags = HideFlags.HideAndDontSave };
            meshFan.SetVertices(v);
            meshFan.SetColors(c);
            meshFan.SetTriangles(tri, 0);
            meshFan.RecalculateNormals();
            meshFan.RecalculateBounds();
            if (matFan == null)
            {
                matFan = Glow(Texture2D.whiteTexture, opacity: 0.25f);
                matFan.SetFloat("_Cull", 0f);
            }
        }

        string KwHint(UltShow u)
        {
            if (u.by == null) return null;
            int waves = u.by.PlanNote & RagdollPawn.KwWavesMask;
            return waves > 0 ? "이동 키 = 행진 (카펫에 선 우리 편은 행렬로)  ·  좌클릭 = 왕관 충격파" : "이동 키 = 행진 (카펫에 선 우리 편은 행렬로)";
        }

        string KwLine(UltShow u)
        {
            if (u.by == null) return "";
            int train = (u.by.PlanNote >> RagdollPawn.KwTrainShift) & RagdollPawn.KwTrainMask, waves = u.by.PlanNote & RagdollPawn.KwWavesMask;
            bool grown = (u.by.PlanNote & RagdollPawn.KwGrown) != 0;
            return $"행렬 {train + 1}명{(grown ? " (과반!)" : "")}  ·  충격파 {waves}번  ·  날린 적 {u.struckSet.Count}";
        }

        string KwWarning(UltShow u, RagdollPawn me)
        {
            if (u.by == null || me == null) return null;
            Vector3 d = Flat(me.Hips.position - u.by.Hips.position);
            return d.magnitude < 6f && Vector3.Dot(d, Flat(u.by.PlanDir)) > 0f ? "커진 킹이 걸어와요 — 비켜요!" : null;
        }
    }
}
