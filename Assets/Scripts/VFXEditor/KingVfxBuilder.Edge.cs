using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// The king's edge skill (E, 왕의 귀환) from E2 on (R199 ~ R203, 10-28; E1 is BuildStab in KingVfxBuilder.cs): the
    /// crown foretold on his landing spot while he hangs (E2), the spring out of the wall (E3), the landing (E4), whoever
    /// it throws (E5) and the fall when his sword is knocked loose (E6). References Assets/VFX/Reference/king/E_02_mark ..
    /// E_06_drop (사용자 10-28 "킹 E 레퍼런스 스킬 사진인데 처음부터 끝까지 너가 테스트 하고 작업 해"). Timing: the
    /// stab about 0.05 s after E, hanging from 0.12 s, the spring at 0.52 s (0.5 s up and over), the landing at 1.02 s
    /// (SwordFightEdgeParams kingCatch, kingHang, kingVault).
    /// </summary>
    public static partial class KingVfxBuilder
    {
        const string MarkPrefab = VfxAssets.Root + "/Prefabs/King/32_King_E_Mark.prefab";
        const string MarkMaterial = VfxAssets.Root + "/Materials/M_King_Mark.mat";
        const string HiltGlowMaterial = VfxAssets.Root + "/Materials/M_King_HiltGlow.mat";
        const string VaultPrefab = VfxAssets.Root + "/Prefabs/King/33_King_E_Vault.prefab";
        const string TrailMaterial = VfxAssets.Root + "/Materials/M_King_Trail.mat";
        // The game finds these by name (SwordFightSkillFx.KingVaultVfx): the burst and the spit go to the crater, the trail
        // follows him.
        const string VaultBurstPart = "E3 Burst", VaultSpitPart = "E3 Spit", VaultAirPart = "E3 Air", VaultTrailPart = "E3 Trail";
        const string LandPrefab = VfxAssets.Root + "/Prefabs/King/34_King_E_Land.prefab";
        const string SealMaterial = VfxAssets.Root + "/Materials/M_King_Seal.mat";
        const string SealTexture = VfxAssets.Root + "/Textures/T_King_LandSeal.png";
        const string LandStreakMaterial = VfxAssets.Root + "/Materials/M_King_LandStreak.mat";
        const string RibbonMaterial = VfxAssets.Root + "/Materials/M_King_Ribbon.mat";
        const string SealPart = "E4 Seal", LandFlashPart = "E4 Flash", LandStreakPart = "E4 Streak", RibbonPart = "E4 Ribbon";
        const string LandHitPrefab = VfxAssets.Root + "/Prefabs/King/35_King_E_Hit.prefab";
        const string LandHitStarPart = "E5 Star", LandHitCutPart = "E5 Streak", LandHitSliverPart = "E5 Slivers";
        const string DropPrefab = VfxAssets.Root + "/Prefabs/King/36_King_E_Drop.prefab";
        const string DropMotesPart = "E6 Motes", DropHiltPart = "E6 Hilt";
        // E1's sparks (R198), off the star where the blade goes in.
        const string StabSparksPart = "E1 Sparks", StabEmbersPart = "E1 Embers";

        // E2: the crown on his landing spot. Its band is a circle on the board; the outline stands in the plane the
        // camera looks across (VfxCrownMark turns it), its base on the band.
        const float BandRadius = .62f, BandLift = .035f;
        // E4: the seal's size as it bursts open and as it settles, when first built (m across; 3.7 and 4 until 10-28,
        // 사용자 "그려지는 원 범위를 4분의 3만큼 줄여줘"); a rebuild keeps what the prefab has.
        const float SealStart = 2.775f, SealEnd = 3f;
        // The seal's crown (its -1..1 units, KingVfxBuilder.Seal.cs) stood up in metres, its foot on the band.
        const float CrownSize = 1.45f, CrownFoot = -.32f;

        static Vector3 Upright(Vector2 p) => new Vector3(p.x * CrownSize, (p.y - CrownFoot) * CrownSize, 0f);

        /// <summary>The right half of the seal's crown's outline, in its units: from the middle of the band's foot out
        /// along it, up its end, in to the body, up the body's side, then over the tops of the lobes to the middle.</summary>
        static List<Vector2> CrownOutline()
        {
            var pts = new List<Vector2> { new Vector2(0f, -.32f), new Vector2(.3f, -.32f), new Vector2(.3f, -.2f), new Vector2(.28f, -.2f), new Vector2(.357f, .06f) };
            float[] at = { -.3f, -.15f, 0f, .15f, .3f }, top = { .06f, .13f, .17f, .13f, .06f };
            const float Lobe = .082f;
            for (float x = .3f + Lobe; x > -.001f; x -= .01f)
            {
                float y = float.MinValue;
                for (int k = 0; k < 5; k++)
                {
                    float dx = x - at[k];
                    if (Mathf.Abs(dx) <= Lobe) y = Mathf.Max(y, top[k] + Mathf.Sqrt(Lobe * Lobe - dx * dx));
                }
                if (y > float.MinValue) pts.Add(new Vector2(Mathf.Max(0f, x), y));
            }
            return pts;
        }
        // The spring, from his hips as it starts (where the game plays E3, +Z the way he faces, onto the board): his hips
        // as he hangs are 0.42 m under the lip and 0.22 m out; the landing spot 1.5 m in (SwordFightEdgeParams.kingLandIn),
        // his hips then about 0.78 m over it; the top 1.25 m over the higher end (SwordFightSkills.VaultPoint).
        static readonly Vector3 VaultEnd = new Vector3(0f, 1.2f, 1.72f);
        // The crater, from his hips as he hangs: on the wall's face, a little over them.
        static readonly Vector3 VaultCrater = new Vector3(0f, .26f, .1f);

        [MenuItem("ChessFight/VFX/King/Build E2 (mark only)")]
        public static void BuildE2() => BuildOne(BuildMark, "E2 the crown foretold (E_Gather)");

        [MenuItem("ChessFight/VFX/King/Build E2 (mark only)", true)]
        static bool CanBuildE2() => !EditorApplication.isPlaying;

        [MenuItem("ChessFight/VFX/King/Build E3 (vault only)")]
        public static void BuildE3() => BuildOne(BuildVault, "E3 the spring (E_Vault)");

        [MenuItem("ChessFight/VFX/King/Build E3 (vault only)", true)]
        static bool CanBuildE3() => !EditorApplication.isPlaying;

        [MenuItem("ChessFight/VFX/King/Build E4 (land only)")]
        public static void BuildE4() => BuildOne(BuildLand, "E4 the landing (E_Land)");

        [MenuItem("ChessFight/VFX/King/Build E4 (land only)", true)]
        static bool CanBuildE4() => !EditorApplication.isPlaying;

        [MenuItem("ChessFight/VFX/King/Build E5 (land hit only)")]
        public static void BuildE5() => BuildOne(BuildLandHit, "E5 whoever the landing throws (E_Hit)");

        [MenuItem("ChessFight/VFX/King/Build E5 (land hit only)", true)]
        static bool CanBuildE5() => !EditorApplication.isPlaying;

        [MenuItem("ChessFight/VFX/King/Build E6 (drop only)")]
        public static void BuildE6() => BuildOne(BuildDrop, "E6 the fall (E_Drop)");

        [MenuItem("ChessFight/VFX/King/Build E6 (drop only)", true)]
        static bool CanBuildE6() => !EditorApplication.isPlaying;

        // ---------------------------------------------------------------- E1 (R198): sparks off the stab

        /// <summary>R198 (E1, against E_01_stab.png): a spray of short gold sparks off the star, out of the wall and down,
        /// and a few slower glowing bits falling away below it.</summary>
        static void StabSparks(GameObject root, GameObject old)
        {
            var sparks = new GameObject(StabSparksPart);
            sparks.transform.SetParent(root.transform, false);
            sparks.transform.localPosition = StabCrater + Vector3.forward * .05f;
            var ps = Burst(sparks, 40, new Vector2(.12f, .35f), new Vector2(2.5f, 6.5f), new Vector2(.012f, .024f), 1.2f);
            var shape = ps.shape;
            // Round +Z (out of the wall), wide, tipped a little down.
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 62f;
            shape.radius = .03f;
            shape.rotation = new Vector3(12f, 0f, 0f);
            Streaks(ps, SliverMat(), .03f);
            KeepParticles(old, StabSparksPart, sparks);

            var embers = new GameObject(StabEmbersPart);
            embers.transform.SetParent(root.transform, false);
            embers.transform.localPosition = StabCrater + Vector3.forward * .05f;
            var drop = Burst(embers, 7, new Vector2(.5f, .9f), new Vector2(.6f, 1.8f), new Vector2(.03f, .05f), .9f);
            var dropShape = drop.shape;
            dropShape.shapeType = ParticleSystemShapeType.Cone;
            dropShape.angle = 40f;
            dropShape.radius = .08f;
            dropShape.rotation = new Vector3(35f, 0f, 0f);
            Embers(drop, MoteMat());
            KeepParticles(old, StabEmbersPart, embers);
        }

        // ---------------------------------------------------------------- E2 (R199): the crown foretold

        /// <summary>
        /// E2, the landing foretold (SfFxKind.KingStab, as the blade goes home; the game plays it on his landing spot, +Z
        /// onto the board away from the edge, and ends it when he lands or falls): a crown of thin gold light draws itself
        /// in on the spot - its band, a circle 1.24 m across on the board, from the edge side round both ways; then the
        /// outline of the seal's king's crown (10-28), up from the band over its five lobes to the middle one from both
        /// sides; then the orb and the cross on that - and breathes until
        /// he lands, when it flares and is gone as the seal bursts (E4). A soft gold glow breathes where his sword is
        /// buried (<see cref="VfxCrownMark"/>). No ring round it, nothing that grows or ripples. Takes over E_Gather (the
        /// old way's 07_King_E_Gather swirl, which played from the spring).
        /// </summary>
        static GameObject BuildMark()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(MarkPrefab);
            var root = new GameObject("32_King_E_Mark");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 2.5f;
            // The band going round, the points going up, the cross, held and breathing, the landing (0.97 s), gone.
            cue.captureTimes = new[] { .12f, .22f, .32f, .42f, .52f, .62f, .8f, .99f, 1.04f };
            var mark = root.AddComponent<VfxCrownMark>();
            var tunedMark = old != null ? old.GetComponent<VfxCrownMark>() : null;
            if (tunedMark != null) EditorUtility.CopySerialized(tunedMark, mark);
            var mat = Material(MarkMaterial, "ChessFight/VFX/Path Ribbon Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) },
                ("_DepthFade", .02f), ("_CoreBoost", 1.3f), ("_CoreWidth", .22f), ("_GlowFalloff", 1.4f), ("_HeadGlow", 3f), ("_HeadTime", .05f),
                ("_FadePower", 1f));

            // The band: two halves from the edge side (-Z) round to the back.
            for (int side = -1; side <= 1; side += 2)
            {
                var pts = new List<Vector3>();
                var times = new List<float>();
                for (int i = 0; i <= 12; i++)
                {
                    float a = Mathf.PI * i / 12f;
                    pts.Add(new Vector3(side * Mathf.Sin(a) * BandRadius, BandLift, -Mathf.Cos(a) * BandRadius));
                    times.Add(Mathf.Lerp(.08f, .3f, i / 12f));
                }
                Line(root, old, side < 0 ? "E2 Band L" : "E2 Band R", mat, pts, times, 4, .04f);
            }

            // The outline of the seal's king's crown (E4, CrownPieces; 10-28 사용자 "올라갈 때 그려지는 문양도 교체해줘"),
            // turned to the camera by VfxCrownMark: from the middle of the band's foot out and up both sides, over the
            // lobes to the top of the middle one; then the band's top, the orb and the cross.
            var crown = new GameObject(VfxCrownMark.CrownName);
            crown.transform.SetParent(root.transform, false);
            var half = CrownOutline();
            for (int side = -1; side <= 1; side += 2)
            {
                var pts = new List<Vector3>();
                float run = 0f, total = 0f;
                for (int i = 1; i < half.Count; i++) total += (half[i] - half[i - 1]).magnitude;
                var times = new List<float>();
                for (int i = 0; i < half.Count; i++)
                {
                    if (i > 0) run += (half[i] - half[i - 1]).magnitude;
                    pts.Add(Upright(new Vector2(side * half[i].x, half[i].y)));
                    times.Add(Mathf.Lerp(.27f, .5f, run / total));
                }
                Line(crown, old, side < 0 ? "E2 Crown L" : "E2 Crown R", mat, pts, times, 1, .045f);
            }
            Line(crown, old, "E2 Crown Band", mat, new List<Vector3> { Upright(new Vector2(-.3f, -.2f)), Upright(new Vector2(.3f, -.2f)) },
                new List<float> { .32f, .44f }, 1, .04f);
            var orb = new List<Vector3>();
            var orbTimes = new List<float>();
            for (int i = 0; i <= 16; i++)
            {
                float a = -Mathf.PI * .5f + 2f * Mathf.PI * i / 16f;
                orb.Add(Upright(new Vector2(Mathf.Cos(a) * .045f, .27f + Mathf.Sin(a) * .045f)));
                orbTimes.Add(Mathf.Lerp(.5f, .56f, i / 16f));
            }
            Line(crown, old, "E2 Orb", mat, orb, orbTimes, 1, .04f);
            Line(crown, old, "E2 Cross", mat, new List<Vector3> { Upright(new Vector2(0f, .315f)), Upright(new Vector2(0f, .48f)) },
                new List<float> { .55f, .6f }, 1, .045f);
            Line(crown, old, "E2 Cross Bar", mat, new List<Vector3> { Upright(new Vector2(-.08f, .41f)), Upright(new Vector2(.08f, .41f)) },
                new List<float> { .58f, .63f }, 1, .045f);

            // Where the sword is buried: the edge is 1.5 m behind the spot; the hilt 0.18 m out and 0.12 m under the lip.
            var glow = new GameObject(VfxCrownMark.GlowName);
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, -.12f, -1.68f);
            glow.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BillboardMesh);
            var view = glow.AddComponent<MeshRenderer>();
            view.sharedMaterial = Material(HiltGlowMaterial, "ChessFight/VFX/Soft Glow Additive",
                new[] { ("_Color", Color.Lerp(Gold, WarmWhite, .25f)) }, ("_Falloff", 3f));
            Quiet(view);

            var prefab = VfxAssets.Prefab(MarkPrefab, root);
            // Takes over the old way's 07_King_E_Gather, which stays until the user says to remove it.
            Register("E_Gather", prefab);
            return prefab;
        }

        /// <summary>One line of light along <paramref name="pts"/> (this parent's space), each point drawn at its time:
        /// a <see cref="VfxPathRibbon"/> that keeps its light (a long life, barely narrowing).</summary>
        static void Line(GameObject parent, GameObject old, string name, Material mat, List<Vector3> pts, List<float> times, int smooth, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>();
            var view = go.AddComponent<MeshRenderer>();
            view.sharedMaterial = mat;
            Quiet(view);
            var line = go.AddComponent<VfxPathRibbon>();
            line.points = pts;
            line.times = times;
            line.smooth = smooth;
            line.width = width;
            line.taper = .1f;
            line.life = 2.5f;
            line.intensity = 1.3f;
            var tuned = old != null ? FindDeep(old.transform, name) : null;
            if (tuned != null && tuned.GetComponent<VfxPathRibbon>() != null)
            {
                // The tuned look, but this build's path (the shape lives here).
                var t = tuned.GetComponent<VfxPathRibbon>();
                line.width = t.width;
                line.taper = t.taper;
                line.life = t.life;
                line.intensity = t.intensity;
            }
        }

        static Transform FindDeep(Transform under, string name)
        {
            foreach (var t in under.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ---------------------------------------------------------------- E3 (R200): the spring

        /// <summary>
        /// E3, the spring out of the wall (SfFxKind.KingVault; the game plays it 0.45 m under his hips, +Z onto the board):
        /// as the sword tears free a short gold burst at the crater (<see cref="VfxStarFlash"/>, smaller than E1's) with a
        /// spit of gold sparks out of it, a few thin streaks of air pressed down under him, and a bright gold trail that
        /// curves up from the crater along his way (<see cref="VfxPathRibbon"/>: the game lays it down where his body
        /// goes), hot white in its core, crisp, and dying away a third of a second behind him. No dust, no ring.
        /// </summary>
        static GameObject BuildVault()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(VaultPrefab);
            var root = new GameObject("33_King_E_Vault");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1.2f;
            // The tear, the burst, rising (top at 0.21 s), coming down, landing (0.5 s), the trail gone.
            cue.captureTimes = new[] { 0f, .03f, .07f, .12f, .2f, .28f, .38f, .5f, .7f };
            // His hips under the lip; the capture floor is under the root.
            cue.captureHeight = .1f;

            var star = new GameObject(VaultBurstPart);
            star.transform.SetParent(root.transform, false);
            star.transform.localPosition = VaultCrater;
            star.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(StarMesh);
            var starView = star.AddComponent<MeshRenderer>();
            starView.sharedMaterial = Material(StarMaterial, "ChessFight/VFX/Star Flash Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) });
            Quiet(starView);
            var burst = star.AddComponent<VfxStarFlash>();
            burst.duration = .12f;
            burst.streakTime = .12f;
            burst.intensity = 2.4f;
            burst.startSize = .7f;
            burst.endSize = .3f;
            burst.turnRange = 25f;
            Keep(old, VaultBurstPart, burst);
            star.transform.localScale = Vector3.one * burst.startSize;

            var spit = new GameObject(VaultSpitPart);
            spit.transform.SetParent(root.transform, false);
            // Out of the wall: toward -Z here (he faces the board), tipped up.
            spit.transform.localPosition = VaultCrater;
            spit.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            var ps = Burst(spit, 24, new Vector2(.12f, .32f), new Vector2(2f, 5f), new Vector2(.011f, .022f), 1.1f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = .04f;
            shape.rotation = new Vector3(-25f, 0f, 0f);
            Streaks(ps, SliverMat(), .03f);
            KeepParticles(old, VaultSpitPart, spit);

            var air = new GameObject(VaultAirPart);
            air.transform.SetParent(root.transform, false);
            air.transform.localPosition = new Vector3(0f, .1f, 0f);
            Air(air, SliverMat());
            var airPs = air.GetComponent<ParticleSystem>();
            var airMain = airPs.main;
            airMain.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
            airMain.startLifetime = new ParticleSystem.MinMaxCurve(.1f, .2f);
            var airShape = airPs.shape;
            // A flat box under him sends them straight down (+Z turned onto -Y).
            airShape.scale = new Vector3(.55f, .45f, .05f);
            airShape.rotation = new Vector3(90f, 0f, 0f);
            airPs.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
            KeepParticles(old, VaultAirPart, air);

            var trail = new GameObject(VaultTrailPart);
            trail.transform.SetParent(root.transform, false);
            trail.AddComponent<MeshFilter>();
            var trailView = trail.AddComponent<MeshRenderer>();
            trailView.sharedMaterial = Material(TrailMaterial, "ChessFight/VFX/Path Ribbon Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) },
                ("_DepthFade", .05f), ("_CoreBoost", 1.5f), ("_CoreWidth", .2f), ("_HeadGlow", 1.2f), ("_FadePower", 1.3f));
            Quiet(trailView);
            var ribbon = trail.AddComponent<VfxPathRibbon>();
            ribbon.width = .2f;
            ribbon.taper = .9f;
            ribbon.life = .3f;
            ribbon.intensity = 1.8f;
            Keep(old, VaultTrailPart, ribbon);
            // A typical way for the workbench and the captures (the game lays down its own): from the crater, then his
            // hips 0.2 m low along the spring.
            ribbon.points = new List<Vector3> { VaultCrater };
            ribbon.times = new List<float> { 0f };
            Vector3 a = new Vector3(0f, .45f, 0f), b = VaultEnd + Vector3.up * .45f;
            for (int i = 1; i <= 20; i++)
            {
                float u = i / 20f;
                ribbon.points.Add(Spring(a, b, u) + Vector3.down * .2f);
                ribbon.times.Add(u * .5f);
            }

            var prefab = VfxAssets.Prefab(VaultPrefab, root);
            // Takes over the old way's 05_King_E_Vault, which stays until the user says to remove it.
            Register("E_Vault", prefab);
            return prefab;
        }

        /// <summary>SwordFightSkills.VaultPoint: up fast and slowing to the top (1.25 m over the higher end at 42% of the
        /// way), down gathering speed; across steadily.</summary>
        static Vector3 Spring(Vector3 a, Vector3 b, float u)
        {
            const float Rise = 1.25f, Top = .42f;
            float peak = Mathf.Max(a.y, b.y) + Rise;
            float y = u < Top ? Mathf.Lerp(a.y, peak, 1f - Mathf.Pow(1f - u / Top, 3f)) : Mathf.Lerp(peak, b.y, Mathf.Pow((u - Top) / (1f - Top), 2f));
            float s = u * u * (3f - 2f * u);
            return new Vector3(Mathf.Lerp(a.x, b.x, s), y, Mathf.Lerp(a.z, b.z, s));
        }

        // ---------------------------------------------------------------- E4 (R201): the landing

        /// <summary>
        /// E4, the landing (SfFxKind.KingLand, as he comes down in his chop; the game plays it on the board under him, +Z the
        /// way he faces): the king's seal bursts open under him, 3 m across, the crown and its cross at its middle
        /// and its rims (T_King_CrownSeal, after the user's picture, 10-28), flashing as it opens and settling, then dying away
        /// (<see cref="VfxSigil"/>, not turning); a white-hot flash at his feet; a long thin level streak of light across
        /// him at chest height; and four thin ribbons of gold light winding up off the seal's rim (<see cref="VfxPathRibbon"/>),
        /// gone from their tails in a third of a second. No smoke, no dust, no ring that grows out. Takes over E_Land (the
        /// old way's 10_King_E_Land_New).
        /// </summary>
        static GameObject BuildLand()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(LandPrefab);
            var root = new GameObject("34_King_E_Land");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1.6f;
            // The landing, the seal opening and flashing, the ribbons winding up, settled, dying away.
            cue.captureTimes = new[] { 0f, .03f, .07f, .12f, .2f, .3f, .45f, .7f, 1.1f };

            var seal = new GameObject(SealPart);
            seal.transform.SetParent(root.transform, false);
            seal.transform.localPosition = new Vector3(0f, .025f, 0f);
            seal.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FloorMesh);
            var sealView = seal.AddComponent<MeshRenderer>();
            var sealMat = Material(SealMaterial, "ChessFight/VFX/Sigil Additive", new[] { ("_Color", Gold) }, ("_Intensity", 1.6f), ("_EdgeGlow", 2.5f));
            // 10-28: the seal of the user's picture (KingVfxBuilder.Seal.cs) in place of the old way's T_King_LandSeal.
            sealMat.SetTexture("_MainTex", VfxAssets.Mask(CrownSealTexture, 512, CrownSeal(512)));
            sealView.sharedMaterial = sealMat;
            Quiet(sealView);
            var sigil = seal.AddComponent<VfxSigil>();
            sigil.duration = 1.4f;
            sigil.reveal = .1f;
            sigil.flashIntensity = 4f;
            sigil.intensity = 1.5f;
            sigil.flashTime = .22f;
            sigil.startScale = SealStart;
            sigil.endScale = SealEnd;
            sigil.spin = 0f;
            sigil.fadeOut = .55f;
            Keep(old, SealPart, sigil);

            var flash = new GameObject(LandFlashPart);
            flash.transform.SetParent(root.transform, false);
            flash.transform.localPosition = new Vector3(0f, .25f, 0f);
            flash.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(StarMesh);
            var flashView = flash.AddComponent<MeshRenderer>();
            flashView.sharedMaterial = Material(StarMaterial, "ChessFight/VFX/Star Flash Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) });
            Quiet(flashView);
            var hot = flash.AddComponent<VfxStarFlash>();
            hot.duration = .13f;
            hot.streakTime = .03f;
            hot.intensity = 3.5f;
            hot.startSize = 1.5f;
            hot.endSize = .6f;
            hot.turnRange = 15f;
            Keep(old, LandFlashPart, hot);
            flash.transform.localScale = Vector3.one * hot.startSize;

            var streak = new GameObject(LandStreakPart);
            streak.transform.SetParent(root.transform, false);
            streak.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            streak.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(StarMesh);
            var streakView = streak.AddComponent<MeshRenderer>();
            // All streak: as wide as the shader draws, its rays and cross faint.
            streakView.sharedMaterial = Material(LandStreakMaterial, "ChessFight/VFX/Star Flash Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) },
                ("_Aspect", 8f), ("_RayStrength", .15f), ("_Cross", .1f), ("_Streak", 1.6f), ("_StreakThin", 90f), ("_CoreBoost", 2f), ("_Glow", .4f));
            Quiet(streakView);
            var line = streak.AddComponent<VfxStarFlash>();
            line.duration = .08f;
            line.streakTime = .26f;
            line.intensity = 2.6f;
            line.startSize = 1.1f;
            line.endSize = .8f;
            line.turnRange = 0f;
            line.spin = 0f;
            Keep(old, LandStreakPart, line);
            streak.transform.localScale = Vector3.one * line.startSize;

            var ribbonMat = Material(RibbonMaterial, "ChessFight/VFX/Path Ribbon Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) },
                ("_DepthFade", .03f), ("_CoreBoost", 1.4f), ("_CoreWidth", .22f), ("_HeadGlow", 2f), ("_FadePower", 1.2f));
            for (int k = 0; k < 4; k++)
            {
                var go = new GameObject(RibbonPart + " " + (k + 1));
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>();
                var view = go.AddComponent<MeshRenderer>();
                view.sharedMaterial = ribbonMat;
                Quiet(view);
                var ribbon = go.AddComponent<VfxPathRibbon>();
                ribbon.width = .06f;
                ribbon.taper = .8f;
                ribbon.life = .32f;
                ribbon.intensity = 1.5f;
                Keep(old, go.name, ribbon);
                // Up off the rim, winding a little over half way round, drawing in toward him as it rises.
                ribbon.points = new List<Vector3>();
                ribbon.times = new List<float>();
                float a0 = k * 90f + 30f, delay = .03f + .03f * (k % 2);
                for (int i = 0; i <= 16; i++)
                {
                    float u = i / 16f, a = (a0 + 200f * u) * Mathf.Deg2Rad, r = Mathf.Lerp(1.75f, 1.1f, u) * SealEnd / 4f;
                    ribbon.points.Add(new Vector3(Mathf.Sin(a) * r, Mathf.Lerp(.05f, 1.7f, u), Mathf.Cos(a) * r));
                    ribbon.times.Add(delay + .42f * (1f - (1f - u) * (1f - u)));
                }
            }

            var prefab = VfxAssets.Prefab(LandPrefab, root);
            // Takes over the old way's 10_King_E_Land_New, which stays until the user says to remove it.
            Register("E_Land", prefab);
            return prefab;
        }

        // ---------------------------------------------------------------- E5 (R202): whoever the landing throws

        /// <summary>
        /// E5, on each piece the landing throws (SfFxKind.KingLandHit; the game plays it at his chest, +Z the way he is
        /// pushed, and hangs it on his chest): a white-gold star bursting on him (<see cref="VfxStarFlash"/>), a short crisp
        /// streak of gold slanting down across him (<see cref="VfxSlash"/>, F4's material), and fine gold slivers thrown the
        /// way he goes. No ring.
        /// </summary>
        static GameObject BuildLandHit()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(LandHitPrefab);
            var root = new GameObject("35_King_E_Hit");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Target;
            cue.length = .6f;
            cue.captureTimes = new[] { 0f, .02f, .05f, .09f, .15f, .25f, .4f };
            cue.captureHeight = .9f;

            var star = new GameObject(LandHitStarPart);
            star.transform.SetParent(root.transform, false);
            star.transform.localPosition = new Vector3(0f, 0f, -.25f);
            star.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(StarMesh);
            var starView = star.AddComponent<MeshRenderer>();
            starView.sharedMaterial = Material(StarMaterial, "ChessFight/VFX/Star Flash Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) });
            Quiet(starView);
            var burst = star.AddComponent<VfxStarFlash>();
            burst.duration = .12f;
            burst.streakTime = .16f;
            burst.intensity = 3f;
            burst.startSize = 1f;
            burst.endSize = .45f;
            burst.turnRange = 20f;
            Keep(old, LandHitStarPart, burst);
            star.transform.localScale = Vector3.one * burst.startSize;

            var cut = new GameObject(LandHitCutPart);
            cut.transform.SetParent(root.transform, false);
            cut.transform.localPosition = new Vector3(0f, 0f, -.28f);
            var line = cut.AddComponent<LineRenderer>();
            line.sharedMaterial = Material(CutMaterial, "ChessFight/VFX/Slash Additive",
                new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) }, ("_DepthFade", .03f), ("_CoreBoost", 2f), ("_CoreWidth", .15f), ("_GlowFalloff", 1.8f));
            Quiet(line);
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            var stroke = cut.AddComponent<VfxSlash>();
            stroke.length = .85f;
            stroke.lean = 58f;
            stroke.leftward = true;
            stroke.bend = .03f;
            stroke.depth = 0f;
            stroke.width = .08f;
            stroke.taper = 1.1f;
            stroke.segments = 14;
            stroke.sweep = .035f;
            stroke.preroll = 0f;
            stroke.duration = .32f;
            stroke.fadeOut = .7f;
            stroke.endWidth = .3f;
            stroke.flashIntensity = 3.2f;
            stroke.intensity = 1.5f;
            stroke.flashTime = .07f;
            Keep(old, LandHitCutPart, stroke);
            stroke.Shape();

            var slivers = new GameObject(LandHitSliverPart);
            slivers.transform.SetParent(root.transform, false);
            slivers.transform.localPosition = new Vector3(0f, 0f, -.2f);
            var ps = Burst(slivers, 45, new Vector2(.18f, .45f), new Vector2(3f, 8f), new Vector2(.01f, .022f), 1.3f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = .12f;
            shape.rotation = new Vector3(-15f, 0f, 0f);
            Streaks(ps, SliverMat(), .015f);
            KeepParticles(old, LandHitSliverPart, slivers);

            var prefab = VfxAssets.Prefab(LandHitPrefab, root);
            Register("E_Hit", prefab);
            return prefab;
        }

        // ---------------------------------------------------------------- E6 (R203): the fall

        /// <summary>
        /// E6, the fall (SfFxKind.KingDrop, when a blow knocks him or his sword loose; the game plays it at his hilt, +Z out
        /// of the wall): the gold goes out - the cracks' glow is quenched (VfxCrackGlow.Quench, the game), E2's crown dies
        /// away (VfxCrownMark.Stop) - and a few gold motes break off the hilt and the crater and fall with him, cooling and
        /// gone within a second. No sparks, no ring.
        /// </summary>
        static GameObject BuildDrop()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(DropPrefab);
            var root = new GameObject("36_King_E_Drop");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Chest;
            cue.length = 1.4f;
            cue.captureTimes = new[] { 0f, .05f, .12f, .2f, .32f, .5f, .7f, .95f };
            cue.captureHeight = 1.2f;
            var mat = MoteMat();

            var motes = new GameObject(DropMotesPart);
            motes.transform.SetParent(root.transform, false);
            // The crater, 0.15 m into the wall from the hilt.
            motes.transform.localPosition = StabCrater;
            var ps = Motes(motes, .2f, new Vector2(.55f, .95f), new Vector2(.2f, .7f), new Vector2(.022f, .042f), .55f);
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8), new ParticleSystem.Burst(.06f, 4), new ParticleSystem.Burst(.14f, 3) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .12f;
            Embers(ps, mat);
            KeepParticles(old, DropMotesPart, motes);

            var hilt = new GameObject(DropHiltPart);
            hilt.transform.SetParent(root.transform, false);
            var hps = Motes(hilt, .1f, new Vector2(.45f, .8f), new Vector2(.1f, .4f), new Vector2(.02f, .036f), .7f);
            hps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
            var hshape = hps.shape;
            hshape.shapeType = ParticleSystemShapeType.Sphere;
            hshape.radius = .06f;
            Embers(hps, mat);
            KeepParticles(old, DropHiltPart, hilt);

            var prefab = VfxAssets.Prefab(DropPrefab, root);
            Register("E_Drop", prefab);
            return prefab;
        }

        // ---------------------------------------------------------------- shared (E)

        /// <summary>A one-off burst of <paramref name="count"/> into the world.</summary>
        static ParticleSystem Burst(GameObject go, int count, Vector2 life, Vector2 speed, Vector2 size, float gravity)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count + 10;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            return ps;
        }

        /// <summary>Stretched along their way, white-hot cooling to a deeper gold (F2's slivers).</summary>
        static void Streaks(ParticleSystem ps, Material mat, float stretch)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .35f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Cooling();
            var view = ps.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = mat;
            Quiet(view);
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = stretch;
            view.lengthScale = 1f;
        }
    }
}
