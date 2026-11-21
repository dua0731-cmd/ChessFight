using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The six driven ultimates on every screen, their common part (JY-skill-lab, 2026-11-22; the rules in
    /// RagdollPawn.DriveUlts.cs). The cast is the knight's (PawnRushSkillFx.Ult.cs: the take-off square, the electric aura,
    /// night falling, the banner, the sound); then, for the 10-12 s it runs:
    ///
    ///   dusk        after the first second the night eases to a dusk (the race must still be seen), and deepens again for
    ///               the judgement at the end
    ///   outlines    on the caster's screen every enemy is drawn again in red light, through walls too (UltOutline.shader)
    ///   preview     before the key, on the screen of the piece that holds it, what it would aim at
    ///   the HUD     the caster's time left, its presses left, what its keys do now; a warning for an enemy it aims at
    ///   each piece  its own marks and moments (PawnRushSkillFx.Drive*.cs), read from the moments the host tells and the
    ///               caster's replicated plan (point, target, note)
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>The way the camera of the piece played here looks (the lab camera's own, without its shake: the aim the
        /// host is sent).</summary>
        Vector3 ViewAim(Camera cam) =>
            game != null && game.labCamera != null && !game.labCamera.freeMode ? game.labCamera.AimForward : cam != null ? cam.transform.forward : Vector3.forward;

        static string DriveTitle(int id) => id switch
        {
            PawnRushSkillIds.PromotionRun => "승격",
            PawnRushSkillIds.Nightmare => "나이트메어",
            PawnRushSkillIds.GreatDiagonal => "롱 다이애거널",
            PawnRushSkillIds.RookRoll => "룩앤롤",
            PawnRushSkillIds.QueenDescent => "여왕 강림",
            PawnRushSkillIds.KingWalk => "킹 워크",
            _ => PawnRushSkillBook.Name(id),
        };

        static string DriveNota(int id) => id switch
        {
            PawnRushSkillIds.PromotionRun => "e8=Q!!",
            PawnRushSkillIds.Nightmare => "KNIGHTMARE",
            PawnRushSkillIds.GreatDiagonal => "a1–h8!!",
            PawnRushSkillIds.RookRoll => "Rook'n'Roll",
            PawnRushSkillIds.QueenDescent => "Qh7+!!",
            PawnRushSkillIds.KingWalk => "Kg3–f4–g5–h6!!",
            _ => "!!",
        };

        // ---------------------------------------------------------------- the moments, sent on to each one's own

        void UltDriveCast(UltShow u)
        {
            switch (u.skill)
            {
                case PawnRushSkillIds.Nightmare: NmCast(u); break;
                case PawnRushSkillIds.GreatDiagonal: GdCast(u); break;
            }
        }

        void UltDriveStep(UltShow u, float dt, Camera cam)
        {
            switch (u.skill)
            {
                case PawnRushSkillIds.Nightmare: NmStep(u, dt, cam); break;
                case PawnRushSkillIds.GreatDiagonal: GdStep(u, dt, cam); break;
            }
        }

        void DriveGo(UltShow u, SkillFxEvent e)
        {
            switch (u.skill)
            {
                case PawnRushSkillIds.Nightmare: NmGoFx(u, e); break;
            }
        }

        void DriveStrike(UltShow u, SkillFxEvent e)
        {
            switch (u.skill)
            {
                case PawnRushSkillIds.Nightmare: NmStrikeFx(u, e); break;
                case PawnRushSkillIds.GreatDiagonal: GdStrikeFx(u, e); break;
            }
        }

        void DriveLandFx(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null || !u.Driven) return;
            switch (u.skill)
            {
                case PawnRushSkillIds.Nightmare: NmLandFx(u, e); break;
            }
        }

        void DriveLineFx(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null || !u.Driven) return;
            switch (u.skill)
            {
                case PawnRushSkillIds.GreatDiagonal: GdLineFx(u, e); break;
            }
        }

        void DriveWarnFx(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null || !u.Driven) return;
        }

        void DriveStateFx(SkillFxEvent e)
        {
            var u = ShowOf(e.by);
            if (u == null || !u.Driven) return;
        }

        /// <summary>What the caster's keys do now (its own screen).</summary>
        string DriveHint(UltShow u) => u.skill switch
        {
            PawnRushSkillIds.Nightmare => NmHint(u),
            PawnRushSkillIds.GreatDiagonal => GdHint(u),
            _ => null,
        };

        /// <summary>The line under the caster's time bar: its presses left, its level, what it has hit.</summary>
        string DriveLine(UltShow u) => u.skill switch
        {
            PawnRushSkillIds.Nightmare => NmLine(u),
            PawnRushSkillIds.GreatDiagonal => GdLine(u),
            _ => $"맞힌 적 {u.struckSet.Count}",
        };

        /// <summary>A warning for the piece played here when the ultimate aims at it now, or null.</summary>
        string DriveWarning(UltShow u, RagdollPawn me) => u.skill switch
        {
            PawnRushSkillIds.Nightmare => NmWarning(u, me),
            PawnRushSkillIds.GreatDiagonal => GdWarning(u, me),
            _ => null,
        };

        // ---------------------------------------------------------------- before the key: what it would aim at

        // The rings over the enemies a held ultimate would aim at now, on the screen of the piece that holds it.
        readonly Dictionary<RagdollPawn, Shape> previewMarks = new Dictionary<RagdollPawn, Shape>();
        readonly HashSet<RagdollPawn> previewWanted = new HashSet<RagdollPawn>();

        void UltDrivePreview(Camera cam)
        {
            previewWanted.Clear();
            var me = Viewer;
            var s = Params;
            if (me != null && cam != null && s != null && me.PlanSkill == 0 && me.UltReady && me.State == PawnState.Active)
            {
                if (me.HasSkill(PawnRushSkillIds.Nightmare)) NmPreview(me, s, previewWanted);
                if (me.HasSkill(PawnRushSkillIds.GreatDiagonal)) GdPreview(me, s, cam, previewWanted);
            }
            foreach (var p in previewWanted)
            {
                if (previewMarks.TryGetValue(p, out var have) && have != null && anims.Contains(have)) continue;
                BuildUltParts();
                var target = p;
                previewMarks[p] = Add(new Shape(root, "Ult preview mark", meshQuad, matRing)
                {
                    life = 1e9f, billboard = true,
                    animate = (sh, t) =>
                    {
                        bool on = target != null && previewWanted.Contains(target);
                        sh.bright = on ? 0.75f + 0.25f * Mathf.Sin(Clock * 6f) : 0f;
                        if (!on) return;
                        sh.at = HeadOf(target) + Vector3.up * 0.55f;
                        sh.scale = new Vector3(0.55f, 0.55f, 1f);
                        sh.color = HdrA(Blood, 2f);
                    },
                });
            }
        }

        // ---------------------------------------------------------------- the red outlines

        sealed class EnemyOutline
        {
            public RagdollPawn p;
            public SkinnedMeshRenderer r;
            public float k, want;
        }

        readonly Dictionary<RagdollPawn, EnemyOutline> outlines = new Dictionary<RagdollPawn, EnemyOutline>();
        readonly List<RagdollPawn> outlinesGone = new List<RagdollPawn>();
        Material matOutline;
        MaterialPropertyBlock outlineBlock;   // (made when first used)
        bool outlineShaderMissing;

        /// <summary>Every frame: while a driven ultimate of the piece played here runs, every enemy of it in red light.</summary>
        void UltOutlines(float dt)
        {
            RagdollPawn caster = null;
            float want = 0f;
            foreach (var u in ults)
            {
                if (!u.Driven || u.by == null || u.by != Viewer) continue;
                float k = u.Life(Clock) * (u.hitAt >= 0f ? 1f - Smooth01((Clock - u.hitAt) / 0.8f) : 1f);
                if (k <= want) continue;
                want = k;
                caster = u.by;
            }
            foreach (var o in outlines.Values) o.want = 0f;
            if (caster != null && !outlineShaderMissing)
                foreach (var p in RagdollPawn.All)
                {
                    if (p == null || p == caster || p.skin == null || !Teams.AreEnemies(p.Team, caster.Team)) continue;
                    if (!outlines.TryGetValue(p, out var o))
                    {
                        o = MakeOutline(p);
                        if (o == null) continue;
                        outlines[p] = o;
                    }
                    o.want = want;
                }
            outlinesGone.Clear();
            foreach (var kv in outlines)
            {
                var o = kv.Value;
                var skin = o.p != null ? o.p.skin : null;
                if (o.r == null || skin == null)
                {
                    outlinesGone.Add(kv.Key);
                    continue;
                }
                o.k = Mathf.MoveTowards(o.k, o.want, dt / 0.25f);
                if (o.k <= 0f && o.want <= 0f)
                {
                    Destroy(o.r.gameObject);
                    outlinesGone.Add(kv.Key);
                    continue;
                }
                if (o.r.sharedMesh != skin.sharedMesh)
                {
                    o.r.sharedMesh = skin.sharedMesh;
                    o.r.bones = skin.bones;
                    o.r.rootBone = skin.rootBone;
                }
                o.r.forceRenderingOff = !skin.enabled || skin.forceRenderingOff || !skin.gameObject.activeInHierarchy;
                if (outlineBlock == null) outlineBlock = new MaterialPropertyBlock();
                outlineBlock.SetFloat("_Alpha", o.k * (0.85f + 0.15f * Mathf.Sin(Clock * 5f + o.p.GetInstanceID())));
                o.r.SetPropertyBlock(outlineBlock);
            }
            foreach (var p in outlinesGone) outlines.Remove(p);
        }

        EnemyOutline MakeOutline(RagdollPawn p)
        {
            var skin = p.skin;
            if (skin == null || skin.sharedMesh == null) return null;
            if (matOutline == null)
            {
                var shader = Shader.Find("ChessFight/Ult Outline");
                if (shader == null) shader = Resources.Load<Shader>("PawnRushSkillFx/UltOutline");
                if (shader == null)
                {
                    outlineShaderMissing = true;
                    Debug.LogWarning("[PawnRushSkillFx] Ult outline shader missing (Resources/PawnRushSkillFx/UltOutline)");
                    return null;
                }
                matOutline = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            var go = new GameObject("Ult outline");
            go.transform.SetParent(skin.transform, false);
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = skin.sharedMesh;
            r.bones = skin.bones;
            r.rootBone = skin.rootBone;
            r.localBounds = skin.localBounds;
            r.updateWhenOffscreen = true;
            r.sharedMaterial = matOutline;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.skinnedMotionVectors = false;
            r.allowOcclusionWhenDynamic = false;   // behind a wall is the point
            return new EnemyOutline { p = p, r = r };
        }

        void DestroyOutlines()
        {
            foreach (var o in outlines.Values) if (o.r != null) Destroy(o.r.gameObject);
            outlines.Clear();
            if (matOutline != null) Destroy(matOutline);
        }
    }
}
