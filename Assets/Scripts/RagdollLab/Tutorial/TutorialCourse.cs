using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The first-run tutorial (R829 grey box, Docs/Player/TUTORIAL_DESIGN_v0.1.md): one offline pawn on the ragdoll lab's
    /// map, played exactly as in a Pawn Rush match (PlaytestSpawner, the Pawn Rush movement, skills and key card -
    /// CourseSkills.PawnRush), led through the lessons one at a time. A lesson ends when the player has done the move
    /// (passed under the bar, stood on the wall...), never on a "next" button; then a gold square shows the next one.
    ///
    /// Grey box 1: lessons 1-6, 7 and 10. Lesson 7 (user 11-25) is three parts against three practice bots that walk to
    /// and fro (TutorialBots): tackle one (7-1), hold one (7-2), get free when one holds you (7-3). Lessons 8-9 (the pawn's
    /// skill, promotion) come later. Skipping opens once lesson 7 is done (the user, 11-24): before that the Esc menu's
    /// leave only says so.
    ///
    /// Every lesson has its own checkpoint at its start: a fall comes back there (KillVolume under the whole map).
    /// The grappling hook is off as in a match (SectionPlaytest's hook zone far below).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed partial class TutorialCourse : MonoBehaviour
    {
        /// <summary>GameSettings flag: the tutorial was finished or skipped at this PC (the intro reads it, grey box 3).</summary>
        public const string DoneKey = SceneNames.TutorialDone;
        /// <summary>The lesson number from which leaving (skipping) is allowed: after lesson 7 (user 11-24).</summary>
        // Was: skipping opened after lesson 7 (user 11-24). Now a first run is played to the end (user 11-27).
        const string FirstRunLocked = "첫 튜토리얼은 끝까지 마쳐야 로비로 갈 수 있어요";
        const float HintAfter = 20f;
        const float PraiseFor = 1.3f;
        const float GoalWait = 3f;

        [SerializeField] PlaytestSpawner spawner;
        [Tooltip("The gold square and pillar that show where to go.")]
        [SerializeField] Material gold;
        [Tooltip("The practice bots' skin: the black set (the lab's P2), so they are not mistaken for the player.")]
        [SerializeField] Material botSkin;

        enum Goal { Reach, Hurdles, UnderBar, AirDive, Sprint, StandOnTop, Tackle, Hold, Escape, Step, StepTwice, Goal }

        sealed class Lesson
        {
            public int number;
            public string label;         // "7-1" for a lesson's parts, else the number
            public string title;
            public Goal goal;
            public Bounds zone;          // Reach / Hurdles / UnderBar / Sprint / Goal: where the hips must be
            public Rect top;             // StandOnTop: the wall top on the ground plan
            public float topY;
            public Vector3 start, mark;
            public float startYaw;
            public Func<string> line, hint;
            public Checkpoint checkpoint;
        }

        readonly List<Lesson> lessons = new List<Lesson>();
        int current = -1, lastDone;
        float lessonStart, sprintFor, onTopFor, praiseUntil = -1f, fellUntil = -1f, noSkipUntil = -1f, goalAt = -1f, overBarUntil = -1f;
        float holdFor, heldFor, lastDive = -99f;
        bool underBar, sprinted, skipOpen;
        bool airDived, wasDiving;
        float airFor;
        TutorialBots bots;
        readonly bool[] hurdles = new bool[TutorialLayout.HurdleX.Length];
        Vector3 lastHips;
        bool hasLast;
        Transform marker;
        GUIStyle title, body, big, small;
        string doneTitle = "";
        float cardLift;
        float ui = 1f, fade = 1f;   // this frame's screen scale (900 px tall = 1) and the alpha of what is being drawn

        public static TutorialCourse Current { get; private set; }
        public int LessonNumber => current >= 0 && current < lessons.Count ? lessons[current].number : 0;
        public string LessonLabel => current >= 0 && current < lessons.Count ? lessons[current].label : "";
        public TutorialBots Bots => bots;
        public bool Finished => goalAt >= 0f;
        public int LessonCount => lessons.Count;

        /// <summary>The automatic check's way past a lesson it cannot do without a held key (lesson 4's sprint).</summary>
        public void SkipLessonForTest()
        {
            if (current >= 0 && current < lessons.Count - 1) Done();
        }

        RagdollPawn Pawn => spawner != null && spawner.Driver is RagdollDriver driver ? driver.Pawn : null;

        void Awake()
        {
            Current = this;
            // The Pawn Rush movement, piece skills and key card, as in a match (PawnRushCourseSkills attaches itself).
            CourseSkills.PawnRush = true;
            // No grappling hook in Pawn Rush (SectionPlaytest): one hook zone far below the map.
            var noHook = new GameObject("No hook here");
            noHook.transform.SetParent(transform, false);
            noHook.transform.position = new Vector3(0f, -500f, 0f);
            noHook.AddComponent<BoxCollider>().isTrigger = true;
            noHook.AddComponent<HookStartZone>();
            BuildLessons();
            BuildKillFloor();
            BuildMoat();
            BuildMarker();
            // Its own Esc menu, not the match's (user 11-27): paused, the lesson on show. A first-time player cannot
            // leave before the end (user 11-27 "처음 다운 받은 플레이어는 튜토리얼 나가기 못하게 막아"): skipping stays locked.
            GameMenu.UseTutorial(gameObject, Leave, () => skipOpen, () => Finished, MenuLine, () => Loc.T(FirstRunLocked));
            // R842: played again from the settings window (done before): leaving is open from the start.
            if (GameSettings.Bool(DoneKey, false)) skipOpen = true;
        }

        void OnEnable()
        {
            KillVolume.Entered += OnFell;
            WaterZone.Entered += OnSplash;
        }

        void OnDisable()
        {
            KillVolume.Entered -= OnFell;
            WaterZone.Entered -= OnSplash;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            CourseSkills.PawnRush = false;
            foreach (var look in markLooks) Destroy(look);
        }

        // ------------------------------------------------------------------ lessons

        // A key as bound now, drawn as a gold key cap in the line: [W]
        static string Key(GameKey key) => "<color=#FFD152><b>[" + GameSettings.KeyCap(key) + "]</b></color>";

        // The lines are written as a game's tutorial prompts (11-25 "너무 AI 자막 같아 실제 인게임 튜토리얼 식"): the key
        // first, one short order, no asides; the hint (after 20 s) is one plain fact. No wall heights in metres
        // (11-25 "몇 m 오르기 자막 빼").
        void BuildLessons()
        {
            Add(1, "이동", Goal.Reach, TutorialLayout.Start, TutorialLayout.StartYaw, TutorialLayout.WalkMark,
                () => Loc.F("{0} {1} {2} {3} 로 이동해 빛기둥에 도착하세요",
                            Key(GameKey.Forward), Key(GameKey.Left), Key(GameKey.Back), Key(GameKey.Right)),
                () => Loc.T("화면을 클릭하면 마우스로 시점을 조작할 수 있습니다"));
            Add(2, "점프", Goal.Hurdles, TutorialLayout.JumpStart, 90f, TutorialLayout.JumpMark,
                () => Loc.F("{0} 로 장애물 2개를 뛰어넘으세요", Key(GameKey.Jump)),
                () => Loc.F("장애물 바로 앞에서 {0} 를 누르세요", Key(GameKey.Jump)));
            Add(3, "다이빙", Goal.UnderBar, TutorialLayout.DiveStart, 180f, TutorialLayout.DiveMark,
                () => Loc.F("달리는 중 {0} 로 봉 아래를 통과하세요", Key(GameKey.Shove)),
                () => Loc.T("걸어서 지나가거나 위로 넘으면 인정되지 않습니다"), -1, "3-1");
            // 3-2 (user 11-25 "점프 다이빙도 넣어줘"): a dive begun in the air, back west along the open floor.
            Add(3, "점프 다이빙", Goal.AirDive, TutorialLayout.DiveMark, 270f, TutorialLayout.AirDiveMark,
                () => Loc.F("{0} 점프 중 {1} 로 몸을 날려 빛기둥에 도착하세요", Key(GameKey.Jump), Key(GameKey.Shove)),
                () => Loc.F("발이 땅에서 떨어진 뒤에 {0} 를 누르세요", Key(GameKey.Shove)), -1, "3-2");
            Add(4, "질주", Goal.Sprint, TutorialLayout.SprintStart, 90f, TutorialLayout.SprintMark,
                () => Loc.F("{0} 를 누른 채 벽 앞까지 질주하세요", Key(GameKey.Sprint)),
                () => Loc.T("질주 중에는 스테미나가 소모됩니다"));
            Add(5, "매달리기", Goal.StandOnTop, TutorialLayout.LedgeStart, 90f, TutorialLayout.WallTopMark(0),
                () => Loc.F("{0} 점프 후 {1} 를 누른 채 벽 위로 올라가세요", Key(GameKey.Jump), Key(GameKey.Grab)),
                () => Loc.F("모서리에 매달린 상태에서 {0} 를 누르면 올라갑니다", Key(GameKey.Jump)),
                0);
            Add(6, "벽 타기", Goal.StandOnTop, TutorialLayout.ClimbStart, 90f, TutorialLayout.WallTopMark(1),
                () => Loc.F("벽에 붙어 {0} + {1} 로 꼭대기까지 오르세요", Key(GameKey.Grab), Key(GameKey.Forward)),
                () => Loc.T("멈춰 있으면 스테미나가 소모되지 않습니다"),
                1);
            // 7: rough and tumble against the practice bots (user 11-25). 8-9 (the pawn's skill, promotion) come later.
            Add(7, "밀치기", Goal.Tackle, TutorialLayout.FightStart, TutorialLayout.FightStartYaw, TutorialLayout.FightMark,
                () => Loc.F("{0} 로 연습 상대를 넘어뜨리세요", Key(GameKey.Shove)),
                () => Loc.T("정면 공격은 피합니다. 뒤나 옆에서 접근하세요"), -1, "7-1");
            Add(7, "잡기", Goal.Hold, TutorialLayout.FightStart, TutorialLayout.FightStartYaw, TutorialLayout.FightMark,
                () => Loc.F("{0} 를 누른 채 연습 상대를 붙잡으세요", Key(GameKey.Grab)),
                () => Loc.F("{0} 로 추격하세요. 잡은 상태에서 {1} 로 던질 수 있습니다", Key(GameKey.Sprint), Key(GameKey.Shove)), -1, "7-2");
            Add(7, "탈출", Goal.Escape, TutorialLayout.FightStart, TutorialLayout.FightStartYaw, TutorialLayout.FightMark,
                () => Loc.F("잡히면 {0} 연타로 탈출하세요", Key(GameKey.Shove)),
                () => Loc.T("연타하면서 반대 방향으로 이동하면 더 빨리 풀립니다"), -1, "7-3");
            // 8 (user 11-25 "폰 f 스킬 … 넣어줘"): the pawn's step, then the second step into a practice bot (the bots
            // are the other side, so the second step has a target when one stands in front within reach).
            Add(8, "폰 스킬", Goal.Step, TutorialLayout.FightStart, TutorialLayout.FightStartYaw, TutorialLayout.FightMark,
                () => Loc.F("{0} 로 스텝을 사용하세요", Key(GameKey.Use)),
                () => Loc.T("이동 중인 방향으로 짧게 돌진합니다"), -1, "8-1");
            Add(8, "연속 스텝", Goal.StepTwice, TutorialLayout.FightStart, TutorialLayout.FightStartYaw, TutorialLayout.FightMark,
                () => Loc.F("연습 상대 앞에서 {0} {0} 를 연속으로 눌러 공격하세요", Key(GameKey.Use)),
                () => Loc.T("두 번째 스텝은 정면 가까이에 상대가 있을 때만 나갑니다"), -1, "8-2");
            Add(10, "외나무다리", Goal.Goal, TutorialLayout.BeamStart, TutorialLayout.BeamStartYaw, TutorialLayout.GoalMark,
                () => Loc.T("다리를 건너 금빛 문에 도착하세요"),
                () => Loc.T("떨어지면 다리 앞에서 다시 시작합니다"));
        }

        void Add(int number, string name, Goal goal, Vector3 start, float yaw, Vector3 mark, Func<string> line, Func<string> help,
                 int wall = -1, string label = null)
        {
            var lesson = new Lesson
            {
                number = number, label = label ?? number.ToString(), title = name, goal = goal, start = start, startYaw = yaw, mark = mark, line = line, hint = help,
                zone = new Bounds(mark + Vector3.up * 1.5f, new Vector3(2.6f, 4f, 2.6f)),
            };
            switch (goal)
            {
                case Goal.Hurdles: lesson.zone = new Bounds(mark + Vector3.up * 1.5f, new Vector3(2.4f, 4f, TutorialLayout.HurdleLength)); break;
                case Goal.UnderBar: lesson.zone = new Bounds(mark + Vector3.up * 1.5f, new Vector3(4f, 4f, 2f)); break;
                case Goal.Sprint: lesson.zone = new Bounds(mark + Vector3.up * 1.5f, new Vector3(2.6f, 4f, LabLayout.WallWidth)); break;
                case Goal.Goal: lesson.zone = new Bounds(mark + Vector3.up * 1.5f, new Vector3(6f, 4f, 6f)); break;
                case Goal.StandOnTop:
                    lesson.top = TutorialLayout.WallTop(wall);
                    lesson.topY = LabLayout.WallHeights[wall];
                    break;
            }
            // Each lesson's checkpoint at its start (order = its place): R and a fall come back here.
            var cp = new GameObject($"Lesson {number} checkpoint");
            cp.transform.SetParent(transform, false);
            cp.transform.SetPositionAndRotation(start + Vector3.up * 0.5f, Quaternion.Euler(0f, yaw, 0f));
            cp.AddComponent<BoxCollider>().size = Vector3.one;
            lesson.checkpoint = cp.AddComponent<Checkpoint>();
            lesson.checkpoint.Configure(lessons.Count);
            lessons.Add(lesson);
        }

        void BuildKillFloor()
        {
            var kill = new GameObject("Fall (KillVolume)");
            kill.transform.SetParent(transform, false);
            kill.transform.position = TutorialLayout.KillCenter;
            kill.AddComponent<BoxCollider>().size = TutorialLayout.KillSize;
            kill.AddComponent<KillVolume>();
        }

        // R857 (user 11-26: "물에 떨어지면 충돌 판정 없이 떨어지니까 수정해"): the moat's water holds what falls into it - the
        // pawn splashes in, floats and thrashes, and is put back after a moment (PlaytestSpawner). The picture is
        // TutorialMoatWater's, at the same height; the kill box lies under it for anything that gets past.
        void BuildMoat()
        {
            var moat = new GameObject("Moat (WaterZone)");
            moat.transform.SetParent(transform, false);
            moat.transform.position = TutorialLayout.MoatCenter;
            moat.AddComponent<BoxCollider>().size = TutorialLayout.MoatSize;
            JsonUtility.FromJsonOverwrite("{\"respawnDelay\":" + TutorialLayout.MoatRespawnDelay.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}",
                                          moat.AddComponent<WaterZone>());
        }

        // Where to go, seen from across the map: a waypoint as in a game's tutorial (11-25 "이 막대도 튜토리얼처럼 효과",
        // then "그래픽을 더 좋게 하고 밑에 발판 지워" - no gold slab any more). A column of light in three layers (a wide soft
        // one with streaks rising through it, a brighter inner one, a pale core), a glow and a thin ring on the floor
        // where to stand, rings spreading out of it, motes of light drifting up, and a gold diamond turning and bobbing
        // above. Shader Resources/TutorialFx/TutorialBeacon ("ChessFight/Tutorial Beacon"). Drawn only - no colliders.
        Transform beam, gem;
        Transform[] rings;
        Renderer[] ringDraws;
        Renderer glowDraw;
        Material columnLook;
        readonly List<Material> markLooks = new List<Material>();
        MaterialPropertyBlock markBlock;
        float markAt;
        const float BeamHeight = 8f, GemHeight = 3.3f;
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity");

        void BuildMarker()
        {
            marker = new GameObject("Tutorial mark").transform;
            marker.SetParent(transform, false);
            markBlock = new MaterialPropertyBlock();

            // Without the beacon shader (not imported yet): Sprites/Default, plain vertex colours.
            var shader = Shader.Find("ChessFight/Tutorial Beacon");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material Look(float shape, float intensity, float cover)
            {
                if (shader == null) return gold;
                var look = new Material(shader) { enableInstancing = false };
                look.SetFloat("_Shape", shape);
                look.SetFloat(IntensityId, intensity);
                look.SetFloat("_Opacity", cover);
                markLooks.Add(look);
                return look;
            }
            columnLook = Look(0f, 1.7f, .55f);
            var floorLook = Look(1f, 1.8f, .6f);
            var moteLook = Look(2f, 2.2f, .35f);
            var white = new Color(1f, 1f, 1f, 1f);
            Color Faded(float a) => new Color(1f, 1f, 1f, a);

            beam = MarkPart("Beam", Tube(.8f, BeamHeight, 48, Faded(.6f)), columnLook);
            MarkPart("Beam inner", Tube(.4f, BeamHeight * .8f, 32, Faded(.75f)), columnLook).SetParent(beam, false);
            MarkPart("Beam core", Tube(.07f, BeamHeight * .9f, 12, white), columnLook).SetParent(beam, false);

            var glow = MarkPart("Floor glow", Ring(0f, 1.35f, 48, Faded(.75f), Faded(0f)), floorLook);
            glow.localPosition = new Vector3(0f, .03f, 0f);
            glowDraw = glow.GetComponent<Renderer>();
            // Where to stand: a thin bright ring, soft on both sides.
            MarkPart("Floor ring in", Ring(.72f, .8f, 64, Faded(0f), white), floorLook).localPosition = new Vector3(0f, .035f, 0f);
            MarkPart("Floor ring out", Ring(.8f, .86f, 64, white, Faded(0f)), floorLook).localPosition = new Vector3(0f, .035f, 0f);

            rings = new Transform[3];
            ringDraws = new Renderer[rings.Length];
            var ring = Ring(.84f, 1f, 64, Faded(0f), Faded(.9f));
            for (int i = 0; i < rings.Length; i++)
            {
                rings[i] = MarkPart("Ring " + i, ring, floorLook);
                rings[i].localPosition = new Vector3(0f, .04f + .004f * i, 0f);
                ringDraws[i] = rings[i].GetComponent<Renderer>();
            }

            BuildMotes(moteLook);
            gem = MarkPart("Diamond", Diamond(.26f, .42f), gold);
            gem.localPosition = new Vector3(0f, GemHeight, 0f);
            marker.gameObject.SetActive(false);
        }

        // Motes of light drifting up through the column.
        void BuildMotes(Material look)
        {
            var motes = new GameObject("Motes");
            motes.transform.SetParent(marker, false);
            motes.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // the cone's axis (+Z) straight up
            var system = motes.AddComponent<ParticleSystem>();
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.7f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.07f, .18f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 80;
            var emission = system.emission;
            emission.rateOverTime = 18f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = .75f;
            var color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var draw = motes.GetComponent<ParticleSystemRenderer>();
            draw.sharedMaterial = look;
            draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            draw.receiveShadows = false;
        }

        Transform MarkPart(string name, Mesh mesh, Material look)
        {
            var part = new GameObject(name);
            part.transform.SetParent(marker, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = look;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
        }

        // An open tube standing on the floor: uv x round it, uv y up it, normals pointing out.
        static Mesh Tube(float radius, float height, int sides, Color color)
        {
            var points = new Vector3[(sides + 1) * 2];
            var normals = new Vector3[points.Length];
            var uvs = new Vector2[points.Length];
            var colors = new Color[points.Length];
            var tris = new int[sides * 6];
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int k = 0; k < 2; k++)
                {
                    points[i * 2 + k] = dir * radius + Vector3.up * (height * k);
                    normals[i * 2 + k] = dir;
                    uvs[i * 2 + k] = new Vector2(i / (float)sides, k);
                    colors[i * 2 + k] = color;
                }
            }
            for (int i = 0; i < sides; i++)
            {
                int v = i * 2, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v + 2; tris[t + 4] = v + 1; tris[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Tutorial mark tube", vertices = points, normals = normals, uv = uvs, colors = colors, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        // A flat ring on the floor, one colour at its inner edge and another at its outer edge.
        static Mesh Ring(float inner, float outer, int sides, Color inside, Color outside)
        {
            var points = new Vector3[(sides + 1) * 2];
            var colors = new Color[points.Length];
            var tris = new int[sides * 6];
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                points[i * 2] = dir * inner; colors[i * 2] = inside;
                points[i * 2 + 1] = dir * outer; colors[i * 2 + 1] = outside;
            }
            for (int i = 0; i < sides; i++)
            {
                int v = i * 2, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 2; tris[t + 4] = v + 3; tris[t + 5] = v + 1;
            }
            var mesh = new Mesh { name = "Tutorial mark ring", vertices = points, colors = colors, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        // A four-sided diamond (two pyramids, flat faces), its middle at the origin.
        static Mesh Diamond(float radius, float half)
        {
            var points = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f, b = (i + 1) * Mathf.PI * .5f;
                var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                var q = new Vector3(Mathf.Cos(b) * radius, 0f, Mathf.Sin(b) * radius);
                foreach (var face in new[] { new[] { Vector3.up * half, q, p }, new[] { Vector3.down * half, p, q } })
                    foreach (var corner in face) { tris.Add(points.Count); points.Add(corner); }
            }
            var mesh = new Mesh { name = "Tutorial mark diamond" };
            mesh.SetVertices(points);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // The mark's life: it grows out of the floor when it moves to a new place, the light breathes, the rings spread
        // and fade one after the other, the diamond turns and bobs.
        void LateUpdate()
        {
            if (marker == null || beam == null || !marker.gameObject.activeSelf) return;
            float t = Time.time, born = Mathf.Clamp01((t - markAt) / .45f);
            float pop = 1f - (1f - born) * (1f - born);
            float breath = .5f + .5f * Mathf.Sin(t * 2.4f);
            beam.localScale = new Vector3(Mathf.Lerp(.6f, 1f, pop), pop, Mathf.Lerp(.6f, 1f, pop));
            if (columnLook != null && columnLook != gold) columnLook.SetFloat(IntensityId, 1.5f + .45f * breath);
            Fade(glowDraw, pop * (.7f + .3f * breath));
            for (int i = 0; i < rings.Length; i++)
            {
                float phase = Mathf.Repeat(t * .5f + i / (float)rings.Length, 1f);
                float size = Mathf.Lerp(.75f, 1.9f, phase);
                rings[i].localScale = new Vector3(size, 1f, size);
                Fade(ringDraws[i], pop * Mathf.Sin(phase * Mathf.PI) * (1f - phase));
            }
            gem.localPosition = new Vector3(0f, GemHeight + .18f * Mathf.Sin(t * 3f), 0f);
            gem.localRotation = Quaternion.Euler(0f, t * 120f, 0f);
            gem.localScale = Vector3.one * pop;
        }

        void Fade(Renderer draw, float amount)
        {
            markBlock.SetFloat(FadeId, Mathf.Clamp01(amount));
            draw.SetPropertyBlock(markBlock);
        }

        void Start()
        {
            gameObject.AddComponent<TutorialAmbience>();   // the breeze and the birds, from the first frame
            gameObject.AddComponent<TutorialMusic>();      // the opening's flight, then the lessons' theme
            gameObject.AddComponent<TutorialSunset>();     // R855: the intro's sunset light (sun, ambient, haze)
            MarkBridge();   // wooden footsteps on the plank bridge (TutorialCourse.Sound)
            MarkRails();    // the hurdles and the bar are jumped over, not stood off (no launching off them)
            // The practice bots walk their lines from the start (seen from lesson 1 on, met in lesson 7).
            if (spawner != null && spawner.CharacterPrefab != null)
            {
                bots = new GameObject("Practice bots").AddComponent<TutorialBots>();
                bots.transform.SetParent(transform, false);
                bots.Spawn(spawner.CharacterPrefab, botSkin);
            }
            Begin(0);
        }

        // The low things to jump over (Gameplay.LowRail): the hurdles, the limbo bar and its posts. Found where the layout
        // has them, by their colliders standing 0.3 to 1 m high (the floor's top is at 0).
        void MarkRails()
        {
            void Mark(Vector3 middle, Vector3 half)
            {
                foreach (var c in Physics.OverlapBox(middle, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    if (c.attachedRigidbody == null && c.bounds.max.y > 0.3f && c.bounds.max.y < 1f && c.GetComponent<LowRail>() == null)
                        c.gameObject.AddComponent<LowRail>();
            }
            foreach (float x in TutorialLayout.HurdleX)
                Mark(new Vector3(x, TutorialLayout.HurdleHeight * 0.5f, TutorialLayout.HurdleZ),
                     new Vector3(0.1f, 0.1f, TutorialLayout.HurdleLength * 0.45f));
            Mark(LabLayout.LimboCenter + Vector3.up * 0.65f, new Vector3(2.2f, 0.05f, 0.2f));
        }

        void Begin(int index)
        {
            current = index;
            CueBegan();
            lessonStart = Time.time;
            sprintFor = onTopFor = holdFor = heldFor = 0f;
            underBar = sprinted = airDived = false;
            Array.Clear(hurdles, 0, hurdles.Length);
            hasLast = false;
            var lesson = lessons[index];
            if (spawner != null && spawner.Driver != null) Checkpoint.Report(spawner.Driver, lesson.checkpoint);
            marker.position = lesson.mark;
            marker.gameObject.SetActive(true);
            markAt = Time.time;
            // 7-3: the first bot comes for the player and holds on until the player struggles free.
            // The bots' minds follow the lesson (TutorialBots): 7-1 dodge dives, 7-2 run and struggle, 7-3 hunt.
            if (bots != null)
            {
                bots.Target = Pawn;
                bots.Now = lesson.goal == Goal.Tackle ? TutorialBots.Mode.Dodge
                         : lesson.goal == Goal.Hold ? TutorialBots.Mode.Evade
                         : lesson.goal == Goal.Escape ? TutorialBots.Mode.Hunt
                         : TutorialBots.Mode.Wander;
            }
        }

        void Done()
        {
            var finished = lessons[current];
            lastDone = finished.number;
            doneTitle = finished.title;
            praiseUntil = Time.time + PraiseFor;
            CueDone();
            if (current + 1 < lessons.Count) Begin(current + 1);
        }

        void Update()
        {
            if (Finished)
            {
                if (Time.time >= goalAt + GoalWait) { goalAt = float.MaxValue; LeaveToLobby(); }
                return;
            }
            var pawn = Pawn;
            if (pawn == null || current < 0) return;
            // The opening (TutorialIntro: the eagle's ride and the crash landing): the first lesson's clock starts after it.
            if (TutorialIntro.Playing) { lessonStart = Time.time; hasLast = false; return; }
            CueUpdate();
            var hips = pawn.Hips.position;
            var lesson = lessons[current];
            if (pawn.Diving) lastDive = Time.time;
            // A dive begun in the air: off the floor for a moment (a jump, not the dive's own lift) when the dive starts.
            if (pawn.Diving && !wasDiving && airFor >= 0.08f) airDived = true;
            wasDiving = pawn.Diving;
            airFor = pawn.Grounded ? 0f : pawn.Diving ? airFor : airFor + Time.deltaTime;
            switch (lesson.goal)
            {
                case Goal.Reach:
                    if (lesson.zone.Contains(hips)) Done();
                    break;
                case Goal.Hurdles:
                    for (int i = 0; i < hurdles.Length; i++)
                    {
                        float x = TutorialLayout.HurdleX[i];
                        if (hasLast && lastHips.x < x && hips.x >= x && Mathf.Abs(hips.z - TutorialLayout.HurdleZ) < TutorialLayout.HurdleLength * 0.5f)
                            hurdles[i] = true;
                    }
                    if (Array.TrueForAll(hurdles, h => h) && lesson.zone.Contains(hips)) Done();
                    break;
                case Goal.UnderBar:
                    var bar = LabLayout.LimboCenter;
                    // R856 (user 11-26 "넘어가도 클리어가 된다"): only a dive that takes the hips under the bar counts -
                    // not a jump over it, nor walking under it (the body squeezes through). Either way says so.
                    if (hasLast && lastHips.z > bar.z && hips.z <= bar.z && Mathf.Abs(hips.x - bar.x) < TutorialLayout.LimboHalfSpan)
                    {
                        if (hips.y < LabLayout.LimboClearance && Time.time - lastDive < TutorialLayout.UnderBarDiveWindow) underBar = true;
                        else { overBarUntil = Time.time + 3f; CueRefused(); }
                    }
                    if (underBar && lesson.zone.Contains(hips)) Done();
                    break;
                case Goal.AirDive:
                    if (airDived && lesson.zone.Contains(hips)) Done();
                    break;
                case Goal.Step:
                    if (pawn.PawnStepNumber >= 1) Done();
                    break;
                case Goal.StepTwice:
                    if (pawn.PawnStepNumber >= 2) Done();
                    break;
                case Goal.Sprint:
                    sprintFor = pawn.Sprinting && pawn.Grounded ? sprintFor + Time.deltaTime : 0f;
                    if (sprintFor >= TutorialLayout.SprintNeeded) sprinted = true;
                    if (sprinted && lesson.zone.Contains(hips)) Done();
                    break;
                case Goal.StandOnTop:
                    bool onTop = lesson.top.Contains(new Vector2(hips.x, hips.z)) && hips.y > lesson.topY + 0.1f   // standing hips are about 0.25 m above the feet
                                 && pawn.Grounded && !pawn.Climbing && pawn.State == PawnState.Active;
                    onTopFor = onTop ? onTopFor + Time.deltaTime : 0f;
                    if (onTopFor >= 0.3f) Done();
                    break;
                case Goal.Tackle:
                    // A bot knocked over near the player just after the player's dive.
                    if (bots != null && Time.time - lastDive < TutorialLayout.TackleWindow)
                        for (int i = 0; i < bots.Count; i++)
                        {
                            var bot = bots[i];
                            if (bot != null && bot.State != PawnState.Active
                                && Vector3.Distance(bot.Hips.position, hips) < TutorialLayout.TackleRange) { Done(); break; }
                        }
                    break;
                case Goal.Hold:
                    holdFor = pawn.HoldingPawn(out _) ? holdFor + Time.deltaTime : 0f;
                    if (holdFor >= TutorialLayout.GrabHold) Done();
                    break;
                case Goal.Escape:
                    // Held for a moment, then free again by the player's own struggle: only a struggle that fills the
                    // gauge gives the second of grab immunity (RagdollPawn struggleFreeTime) - R, a fall or the bot
                    // letting go do not, and start the count again.
                    if (pawn.BeingHeld) heldFor += Time.deltaTime;
                    else if (heldFor >= TutorialLayout.HeldBeforeEscape && pawn.GrabImmune) Done();
                    else heldFor = 0f;
                    break;
                case Goal.Goal:
                    if (lesson.zone.Contains(hips) && pawn.Grounded)
                    {
                        lastDone = lesson.number;
                        goalAt = Time.time;
                        CueGoal();
                        marker.gameObject.SetActive(false);
                        GameSettings.SetBool(DoneKey, true);
                    }
                    break;
            }
            lastHips = hips;
            hasLast = true;
        }

        void OnFell(ICharacterDriver who, KillVolume volume) => Fell(who, volume.RespawnDelay);

        void OnSplash(ICharacterDriver who, WaterZone water) => Fell(who, water.RespawnDelay);

        // The notice and the sounds of a fall: the pawn comes back to the lesson's start after the delay.
        void Fell(ICharacterDriver who, float delay)
        {
            if (spawner == null || who != spawner.Driver) return;
            fellUntil = Time.time + delay + 1.2f;
            CueFell(delay);
            hasLast = false;
        }

        // The Esc menu's line under its title: the lesson the player is on ("3-1단계 · 허들 넘기").
        string MenuLine()
        {
            if (Finished) return Loc.T("모든 단계를 마쳤어요");
            if (current < 0 || current >= lessons.Count) return "";
            var lesson = lessons[current];
            return Loc.F("{0}단계 · {1}", lesson.label, Loc.T(lesson.title));
        }

        // The Esc menu's leave: open only when the tutorial was done before (a replay) or is finished now.
        void Leave()
        {
            if (!skipOpen && !Finished)
            {
                noSkipUntil = Time.time + 3f;
                CueRefused();
                return;
            }
            GameSettings.SetBool(DoneKey, true);
            LeaveToLobby();
        }

        // R842: behind the scene transition's board, as the intro goes to the lobby (a bare LoadScene froze on the
        // tutorial's last frame until the lobby was up). The Esc menu closes first, so it is not left on the screen.
        static void LeaveToLobby()
        {
            GameMenu.Close();
            SceneTransition.ToLobby(new Vector2(.5f, .5f));
        }

        // ------------------------------------------------------------------ screen

        void Styles()
        {
            if (title != null) return;
            var font = RuntimePanels.KoreanFont;
            var none = new RectOffset(0, 0, 0, 0);
            title = new GUIStyle(GUI.skin.label) { font = font, fontStyle = FontStyle.Bold, richText = true, padding = none, margin = none, clipping = TextClipping.Overflow };
            body = new GUIStyle(title) { fontStyle = FontStyle.Normal, wordWrap = true };
            big = new GUIStyle(title);
            small = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, richText = true };
        }

        // The screen is drawn like the match HUD (StaminaBarGui): navy boxes, a faint cream edge, cream corner brackets,
        // gold for what matters. Sizes are for a 900 px tall screen and scale with it (ui).
        static readonly Color Navy = new Color(29 / 255f, 43 / 255f, 94 / 255f, .92f);
        static readonly Color Cream = new Color(246 / 255f, 233 / 255f, 204 / 255f);
        static readonly Color Gold = new Color(1f, .82f, .32f);
        static readonly Color Track = new Color(246 / 255f, 233 / 255f, 204 / 255f, .16f);

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            c.a *= fade;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Text(Rect r, string text, GUIStyle style, float size, Color color, TextAnchor anchor)
        {
            style.fontSize = Mathf.RoundToInt(size * ui);
            style.alignment = anchor;
            style.normal.textColor = color;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        float TextHeight(string text, float size, float width)
        {
            body.fontSize = Mathf.RoundToInt(size * ui);
            return body.CalcHeight(new GUIContent(text), width);
        }

        void Panel(Rect r)
        {
            Fill(r, Navy);
            float f = Mathf.Max(1f, ui);
            var edge = new Color(Cream.r, Cream.g, Cream.b, .25f);
            Fill(new Rect(r.x, r.y, r.width, f), edge);
            Fill(new Rect(r.x, r.yMax - f, r.width, f), edge);
            Fill(new Rect(r.x, r.y + f, f, r.height - 2f * f), edge);
            Fill(new Rect(r.xMax - f, r.y + f, f, r.height - 2f * f), edge);
            float o = 2f * ui, t = Mathf.Max(2f, 3f * ui), len = 11f * ui;
            var b = new Rect(r.x - o, r.y - o, r.width + 2f * o, r.height + 2f * o);
            Fill(new Rect(b.x, b.y, len, t), Cream); Fill(new Rect(b.x, b.y, t, len), Cream);
            Fill(new Rect(b.xMax - len, b.y, len, t), Cream); Fill(new Rect(b.xMax - t, b.y, t, len), Cream);
            Fill(new Rect(b.xMax - len, b.yMax - t, len, t), Cream); Fill(new Rect(b.xMax - t, b.yMax - len, t, len), Cream);
            Fill(new Rect(b.x, b.yMax - t, len, t), Cream); Fill(new Rect(b.x, b.yMax - len, t, len), Cream);
        }

        // How far this lesson's own action is done, 0..1; -1 where there is nothing to count (just get there).
        float Progress(Lesson lesson)
        {
            switch (lesson.goal)
            {
                case Goal.Hurdles:
                    int over = 0;
                    foreach (bool h in hurdles) if (h) over++;
                    return over / (float)hurdles.Length;
                case Goal.UnderBar: return underBar ? 1f : 0f;
                case Goal.AirDive: return airDived ? 1f : 0f;
                case Goal.Sprint: return sprinted ? 1f : Mathf.Clamp01(sprintFor / TutorialLayout.SprintNeeded);
                case Goal.Hold: return Mathf.Clamp01(holdFor / TutorialLayout.GrabHold);
                default: return -1f;
            }
        }

        // Top middle: the course's name, "n / N" and one segment for each lesson (gold = done, the current one fills
        // with its own progress and blinks).
        void DrawProgress(float w)
        {
            float s = ui, pw = 560f * s, pad = 16f * s;
            var box = new Rect((w - pw) * .5f, 16f * s, pw, 58f * s);
            Panel(box);
            int n = lessons.Count, done = Finished ? n : current;
            var head = new Rect(box.x + pad, box.y + 8f * s, pw - 2f * pad, 22f * s);
            Text(head, Loc.T("튜토리얼"), title, 15f, Cream, TextAnchor.MiddleLeft);
            Text(head, done + " / " + n, title, 15f, Gold, TextAnchor.MiddleRight);
            float gap = 4f * s, sw = (pw - 2f * pad - gap * (n - 1)) / n, y = box.y + 37f * s, sh = 9f * s;
            for (int i = 0; i < n; i++)
            {
                var seg = new Rect(box.x + pad + i * (sw + gap), y, sw, sh);
                if (i < done) { Fill(seg, Gold); continue; }
                Fill(seg, Track);
                if (i != current) continue;
                float p = Mathf.Max(0f, Progress(lessons[i]));
                Fill(new Rect(seg.x, seg.y, seg.width * p, seg.height), Gold);
                float blink = .35f + .65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                var rim = new Color(Cream.r, Cream.g, Cream.b, blink);
                float f = Mathf.Max(1f, 1.5f * s);
                Fill(new Rect(seg.x, seg.y - f, seg.width, f), rim);
                Fill(new Rect(seg.x, seg.yMax, seg.width, f), rim);
            }
        }

        // A banner across the middle: a headline between two gold lines, a smaller line under it.
        void Banner(float y, string headline, string sub, float alpha, float pop = 1f)
        {
            float s = ui, bw = 520f * s, bh = (sub != null ? 112f : 80f) * s;
            fade = Mathf.Clamp01(alpha);
            var box = new Rect((Screen.width - bw) * .5f, y, bw, bh);
            Panel(box);
            Fill(new Rect(box.x + 24f * s, box.y + 10f * s, bw - 48f * s, 2f * s), Gold);
            Fill(new Rect(box.x + 24f * s, box.yMax - 12f * s, bw - 48f * s, 2f * s), Gold);
            Text(new Rect(box.x, box.y + 14f * s, bw, 54f * s), headline, big, 38f * pop, Gold, TextAnchor.MiddleCenter);
            if (sub != null) Text(new Rect(box.x, box.y + 66f * s, bw, 30f * s), sub, title, 17f, Cream, TextAnchor.MiddleCenter);
            fade = 1f;
        }

        // A one-line notice in its own small box.
        void Toast(float y, string text, Color color)
        {
            float s = ui;
            title.fontSize = Mathf.RoundToInt(18f * s);
            float tw = title.CalcSize(new GUIContent(text)).x + 44f * s;
            var box = new Rect((Screen.width - tw) * .5f, y, tw, 44f * s);
            Panel(box);
            Text(box, text, title, 18f, color, TextAnchor.MiddleCenter);
        }

        void OnGUI()
        {
            if (SettingsWindow.IsOpen || GameMenu.IsOpen || current < 0 || TutorialIntro.Playing) return;
            Styles();
            float w = Screen.width, h = Screen.height;
            // The same scale as the stamina bar (StaminaBarGui), so the two keep their distance at every screen size.
            float s = ui = Mathf.Clamp(h / 900f, .8f, 2.2f);
            fade = 1f;

            // Top: how far along (11-25 "튜토리얼 막대를 실제 튜토리얼처럼": a segmented progress bar, a lesson card).
            DrawProgress(w);

            if (Finished)
            {
                Banner(h * .36f, Loc.T("튜토리얼 완료"), Loc.T("로비로 이동합니다"), (Time.time - goalAt) / .25f);
                return;
            }

            var lesson = lessons[current];
            float since = Time.time - lessonStart;
            if (current == 0 && since < 3.5f)
                Banner(h * .2f, Loc.T("튜토리얼"), null, Mathf.Min(since / .3f, (3.5f - since) / .5f));

            // Bottom middle: this lesson - its number in a gold square, its name, what to do in the keys as bound now, a
            // bar for the lesson's own action, and after 20 s the hint. It slides up when the lesson starts.
            // It ends above the stamina bar (44 + 30 px from the bottom at 900 px tall) with a gap, so the card never
            // covers it (11-25 "UI 안겹치게").
            bool stuck = since >= HintAfter;
            float cw = 760f * s, pad = 16f * s, stripe = 5f * s, badge = 64f * s;
            float textX = stripe + pad + badge + 16f * s, textW = cw - textX - pad;
            string line = lesson.line(), help = stuck ? lesson.hint() : null;
            float lineH = TextHeight(line, 21f, textW);
            float tag = 62f * s, helpW = textW - tag - 10f * s;
            float helpH = stuck ? Mathf.Max(24f * s, TextHeight(help, 17f, helpW)) : 0f;
            float prog = Progress(lesson);
            float ch = 12f * s + 24f * s + lineH + (prog >= 0f ? 16f * s : 0f) + (stuck ? helpH + 20f * s : 0f) + 12f * s;
            ch = Mathf.Max(ch, badge + 24f * s);
            float slide = 1f - Mathf.Clamp01(since / .3f);
            slide *= slide;
            fade = 1f - slide;
            // While the pawn is held or holds a piece, the grip plate (GripHudGui: "잡혔다! 좌클릭 연타" 64 px tall, "잡는 중"
            // 32 px, both right above the stamina bar) is where the card's foot is: the card moves up clear of it
            // (11-25 "좌클릭 연타 할 때 자막 때문에 안보이는 문제").
            var me = Pawn;
            float clear = me != null && me.BeingHeld ? 88f : me != null && me.HoldingPawn(out _) ? 46f : 0f;
            cardLift = Mathf.MoveTowards(cardLift, clear, Time.unscaledDeltaTime * 600f * (Event.current.type == EventType.Repaint ? 1f : 0f));
            var card = new Rect((w - cw) * .5f, h - (92f + cardLift) * s - ch + slide * 20f * s, cw, ch);
            Panel(card);
            Fill(new Rect(card.x, card.y, stripe, ch), Gold);
            var mark = new Rect(card.x + stripe + pad, card.y + 12f * s, badge, badge);
            Fill(mark, new Color(Gold.r, Gold.g, Gold.b, .14f));
            float rim = Mathf.Max(1f, 2f * s);
            Fill(new Rect(mark.x, mark.y, badge, rim), Gold); Fill(new Rect(mark.x, mark.yMax - rim, badge, rim), Gold);
            Fill(new Rect(mark.x, mark.y, rim, badge), Gold); Fill(new Rect(mark.xMax - rim, mark.y, rim, badge), Gold);
            Text(mark, lesson.label, big, lesson.label.Length > 2 ? 24f : 34f, Gold, TextAnchor.MiddleCenter);

            float x = card.x + textX, y = card.y + 12f * s;
            Text(new Rect(x, y, textW, 22f * s), Loc.T(lesson.title), title, 16f, Gold, TextAnchor.MiddleLeft);
            y += 24f * s;
            Text(new Rect(x, y, textW, lineH), line, body, 21f, Color.white, TextAnchor.UpperLeft);
            y += lineH;
            if (prog >= 0f)
            {
                var bar = new Rect(x, y + 6f * s, textW, 6f * s);
                Fill(bar, Track);
                Fill(new Rect(bar.x, bar.y, bar.width * prog, bar.height), prog >= 1f ? Gold : Cream);
                y += 16f * s;
            }
            if (stuck)
            {
                // The hint fades in under a thin line, behind a gold "도움말" tag.
                fade *= Mathf.Clamp01((since - HintAfter) / .4f);
                Fill(new Rect(x, y + 8f * s, textW, Mathf.Max(1f, s)), new Color(Cream.r, Cream.g, Cream.b, .25f));
                y += 18f * s;
                var tagRect = new Rect(x, y, tag, 24f * s);
                Fill(tagRect, Gold);
                Text(tagRect, Loc.T("도움말"), title, 14f, new Color(.11f, .17f, .37f), TextAnchor.MiddleCenter);
                Text(new Rect(x + tag + 10f * s, y, helpW, helpH), help, body, 17f, Cream, helpH > 26f * s ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            }
            fade = 1f;

            // A lesson done: "좋아요!" pops in with the lesson's name, then fades.
            if (Time.time < praiseUntil)
            {
                float left = praiseUntil - Time.time, gone = PraiseFor - left;
                Banner(h * .28f, Loc.T("성공!"), Loc.F("{0} 완료", Loc.T(doneTitle)), left / .3f, 1f + .25f * (1f - Mathf.Clamp01(gone / .15f)));
            }
            if (Time.time < fellUntil) Toast(h * .44f, Loc.T("체크포인트에서 다시 시작합니다"), Color.white);
            if (Time.time < noSkipUntil) Toast(h * .44f + 54f * s, Loc.T(FirstRunLocked), Gold);
            if (Time.time < overBarUntil && lessons[current].goal == Goal.UnderBar) Toast(h * .44f, Loc.T("다이빙으로 봉 아래를 통과해야 합니다"), Gold);

            // The stamina bar (the spawner draws it only with its help box, which is off here).
            if (spawner != null && spawner.Driver is IStaminaReadout readout) StaminaBarGui.Draw(readout.Stamina01, small);
        }
    }
}
