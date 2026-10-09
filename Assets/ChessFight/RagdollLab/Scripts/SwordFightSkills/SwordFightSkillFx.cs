using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight skill effects (R91) in design A "잉크 테두리 장난감 체스" (picked 2026-10-08 for all three modes; the
    /// shared parts are <see cref="SkillInkKit"/>, R90). Warnings are real board squares and rings on the floor along each
    /// skill's own way, in the piece's colour with the viewer's rim (cream mine, teal my side's, red with sliding stripes
    /// the other side's): they fill as the windup runs, snap full in its last six frames, flash white for two and are gone
    /// in six. The aim before it (held right click) is only shown to its owner, faint. Hits: the body white for two frames
    /// then the hitter's colour for two, round drops off the way it flies, a C half ring, a short hit stop and a shake;
    /// a piece that goes down gets the captured square in the hitter's colour. The king's guard is an emerald arc in front
    /// of him and the reach of his answer on the floor; the queen's thrust a gold ribbon down her line; the rook's wave an
    /// orange wall running along the floor lighting each square it crosses; the bishop's two violet rays meeting in an X
    /// (gold when the floor ends behind the piece: it will be pinned, four stakes and a ring); the knight's horseshoe and
    /// its two sky rings ahead. No glow, no words, no stars, no sound (승규 님, 10-09).
    /// Test-bed stand-ins: the hit stop holds the whole game, the shake moves the view camera.
    ///
    /// R99 (승규 님 10-10: "좀 더 아! 스킬이다 라는 느낌", the queen and the rook felt like one skill in two colours, the
    /// pinned bishop target should look held by the ankles): every skill now starts with a cast (the piece flashes its
    /// colour, a swelling afterimage of its body, a ring bursting off the floor, a two-frame freeze, the view kicks in
    /// and its edges wash with the colour for the caster) and charges (balls of its colour flying into its chest, two
    /// broken rings turning at its feet, the blade in its colour); the queen dashes down her line herself (gold
    /// afterimages, her cut drawn on the floor and lit white when she stops, a slash across each piece she passes); the
    /// rook's line cracks as he raises his sword and stone towers burst up out of the floor square by square, throwing
    /// pieces up; two violet hands come up out of the floor and close round a pinned piece's ankles; the king answers
    /// with a full-circle cut and a crown; the knight's landing forks two lines to its spots.
    ///
    /// R102 (승규 님 10-10): every cast pops the piece's mark up over its head (the five icons 승규 님 sent, in the piece's
    /// colour: <c>SwordFightSkillFx.Icons.cs</c>; the king's pops again when his guard takes a cut, instead of R99's
    /// crown); the rook's towers are the team's piece characters without their feet, in a new random order every slam
    /// (<c>SwordFightSkillFx.Statues.cs</c>); the bishop's two hands wait by its shoulders through the windup, then
    /// shoot out on the diagonals with violet arms stretching behind them and close round the ankles of every piece on
    /// the point (they miss onto the floor and come back if nobody is there), the arms coming away once they hold.
    /// </summary>
    [DefaultExecutionOrder(210)]   // after the match camera (150)
    public partial class SwordFightSkillFx : MonoBehaviour
    {
        public bool effects = true;
        [Tooltip("맞는 순간 게임 전체를 잠깐 멈춤 (시험용: 실제로는 때린 쪽·맞은 쪽만)")]
        public bool hitStop = true;
        public bool shake = true;

        /// <summary>The film's camera while it records; the match camera otherwise.</summary>
        public static Camera ViewOverride;

        const float F = Kit.F;
        Kit kit;
        SwordFightGame game;
        float rate = 1f, stopLeft, stopResume = 1f, shakeAmp, shakeLeft, shakeTotal, shakeClock, dt;
        bool stopping;

        RagdollPawn Viewer => game != null && game.Local != null ? game.Local.Pawn : null;
        public Camera ViewCamera => ViewOverride != null ? ViewOverride : game != null && game.CameraRig != null ? game.CameraRig.Cam : Camera.main;
        Vector3 Eye => ViewCamera != null ? ViewCamera.transform.position : Vector3.up * 5f;
        SwordFightSkillParams S => GetComponent<SwordFightSkillBed>() is SwordFightSkillBed b ? b.skills : null;

        void Awake()
        {
            game = GetComponent<SwordFightGame>();
            if (game == null) game = FindFirstObjectByType<SwordFightGame>();
            kit = new Kit(new GameObject("Sword Fight skill effects").transform, () => Viewer, () => Eye);
            SwordFightSkills.Fx += OnFx;
        }

        void OnDestroy()
        {
            SwordFightSkills.Fx -= OnFx;
            if (stopping) Time.timeScale = stopResume;
            if (punchCam != null && Mathf.Abs(punchCam.fieldOfView - punchSetFov) < 1e-3f) punchCam.fieldOfView += punchApplied;
            if (tintQuad != null) Destroy(tintQuad);
            if (tintMat != null) Destroy(tintMat);
            if (tintTex != null) Destroy(tintTex);
            if (ghostMat != null) Destroy(ghostMat);
            kit?.Destroy();
            DestroyIcons();
            DestroyStatues();
        }

        // ---------------------------------------------------------------- time, hit stop, shake

        void Update()
        {
            // Effect time: the game's own pace (slow motion slows the effects too), but a hit stop does not stop them.
            float real = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (Time.timeScale > 0f && !stopping) rate = Time.timeScale;
            dt = real * rate;
            BuildIconsSlowly();
            if (stopping)
            {
                stopLeft -= dt;
                if (stopLeft <= 0f)
                {
                    stopping = false;
                    if (Time.timeScale == 0f) Time.timeScale = stopResume;
                }
            }
        }

        void HitStop(int frames)
        {
            if (!hitStop || frames <= 0) return;
            if (!stopping)
            {
                stopResume = Time.timeScale > 0f ? Time.timeScale : stopResume;
                stopping = true;
            }
            stopLeft = Mathf.Max(stopLeft, frames * F);
            Time.timeScale = 0f;
        }

        /// <summary>A shake of the view (A: only the hitter's and the hit piece's camera).</summary>
        void Shake(RagdollPawn a, RagdollPawn b, float metres, int frames)
        {
            if (!shake) return;
            var v = Viewer;
            if (ViewOverride == null && v != null && a != v && b != v) return;
            metres *= ChessFight.Game.GameSettings.ShakeScale;
            float now = shakeTotal > 0f ? shakeAmp * Mathf.Clamp01(shakeLeft / shakeTotal) : 0f;
            if (metres < now) return;
            shakeAmp = metres;
            shakeLeft = shakeTotal = frames * F;
        }

        void LateUpdate()
        {
            if (kit == null) return;
            if (effects) Watch();
            kit.Step(dt);
            var cam = ViewCamera;
            if (shakeLeft > 0f && cam != null)
            {
                shakeLeft -= dt;
                shakeClock += dt;
                float k = Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeTotal));
                float a = shakeAmp * k * k, t = shakeClock;
                float x = Mathf.Sin(t * 61f) * 0.6f + Mathf.Sin(t * 97f + 1.3f) * 0.4f;
                float y = Mathf.Sin(t * 71f + 2.1f) * 0.6f + Mathf.Sin(t * 113f + 0.4f) * 0.4f;
                var view = cam.transform;
                view.position += (view.right * x + view.up * y * 0.7f) * a;
                view.rotation *= Quaternion.Euler(y * a * 10f, x * a * 10f, Mathf.Sin(t * 53f + 4.2f) * a * 6f);
            }
            StepView(cam);
        }

        // ---------------------------------------------------------------- the view: a zoom kick and a colour edge

        float punchAmp, punchLeft, punchTotal, punchApplied, punchSetFov = float.NaN;
        Camera punchCam;
        Color tintColor;
        float tintAmp, tintLeft, tintTotal;
        GameObject tintQuad;
        Material tintMat;
        Texture2D tintTex;

        /// <summary>A's view effects are the hitter's and the hit piece's own (the film sees them all).</summary>
        bool Sees(RagdollPawn a, RagdollPawn b)
        {
            var v = Viewer;
            return ViewOverride != null || v == null || a == v || b == v;
        }

        /// <summary>The view zooms in by <paramref name="degrees"/> at once and eases back over <paramref name="frames"/>.</summary>
        void Punch(RagdollPawn a, RagdollPawn b, float degrees, int frames)
        {
            if (!shake || !Sees(a, b)) return;
            float now = punchTotal > 0f ? punchAmp * Mathf.Clamp01(punchLeft / punchTotal) : 0f;
            if (degrees < now) return;
            punchAmp = degrees;
            punchLeft = punchTotal = frames * F;
        }

        /// <summary>The edges of the view wash with a skill's colour and fade ("that was my skill").</summary>
        void Tint(RagdollPawn a, RagdollPawn b, Color c, float alpha, int frames)
        {
            if (!Sees(a, b)) return;
            tintColor = c;
            tintAmp = alpha;
            tintLeft = tintTotal = frames * F;
        }

        void StepView(Camera cam)
        {
            if (cam == null) return;
            // Last frame's kick comes off first, unless something has set the field of view since (the match camera
            // does every frame; the film once a shot).
            if (cam == punchCam && Mathf.Abs(cam.fieldOfView - punchSetFov) < 1e-3f) cam.fieldOfView += punchApplied;
            punchApplied = 0f;
            if (punchLeft > 0f)
            {
                punchLeft -= dt;
                float k = Mathf.Clamp01(punchLeft / Mathf.Max(0.01f, punchTotal));
                punchApplied = punchAmp * k * k;
                cam.fieldOfView -= punchApplied;
            }
            punchCam = cam;
            punchSetFov = cam.fieldOfView;

            if (tintLeft > 0f) tintLeft -= dt;
            float ta = tintLeft > 0f ? tintAmp * Mathf.Clamp01(tintLeft / Mathf.Max(0.01f, tintTotal)) : 0f;
            if (ta <= 0.002f)
            {
                if (tintQuad != null) tintQuad.SetActive(false);
                return;
            }
            EnsureTint();
            tintQuad.SetActive(true);
            float d = cam.nearClipPlane + 0.02f;
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.04f;
            tintQuad.transform.SetPositionAndRotation(cam.transform.position + cam.transform.forward * d, cam.transform.rotation);
            tintQuad.transform.localScale = new Vector3(h * Mathf.Max(cam.aspect, 16f / 9f) * 1.06f, h, 1f);
            tintMat.color = Kit.A(tintColor, ta);
        }

        void EnsureTint()
        {
            if (tintQuad != null) return;
            const int n = 64;
            tintTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    // A rounded frame: clear in the middle, a painted band at the edges.
                    float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 1f, r)));
                }
            tintTex.SetPixels(px);
            tintTex.Apply();
            tintMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tintTex, hideFlags = HideFlags.HideAndDontSave, renderQueue = 4000 };
            tintQuad = new GameObject("Skill view tint");
            tintQuad.AddComponent<MeshFilter>().sharedMesh = kit.meshQuad;
            var mr = tintQuad.AddComponent<MeshRenderer>();
            mr.sharedMaterial = tintMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ---------------------------------------------------------------- shared skill parts (R99)

        Material ghostMat;

        Material GhostMaterial()
        {
            if (ghostMat != null) return ghostMat;
            var shader = Shader.Find("ChessFight/Skill Ghost");
            if (shader == null) shader = Resources.Load<Shader>("SwordFightSkillFx/SkillGhost");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            ghostMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return ghostMat;
        }

        /// <summary>A copy of the piece's body as it stands now: a see-through silhouette of a colour with an ink edge
        /// (Skill Ghost), swelling round its chest by <paramref name="grow"/> and fading over <paramref name="life"/>;
        /// white for its first two frames if <paramref name="flash"/>.</summary>
        void Afterimage(RagdollPawn pawn, Kit.Palette p, float grow, float life, float alpha = 0.6f, bool flash = false)
        {
            if (pawn == null || pawn.skin == null) return;
            var mesh = new Mesh { name = "Skill afterimage" };
            pawn.skin.BakeMesh(mesh, true);
            var go = new GameObject("Afterimage");
            go.transform.SetParent(kit.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = GhostMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            var block = new MaterialPropertyBlock();
            Transform st = pawn.skin.transform;
            Vector3 pos = st.position, centre = pawn.bodies[(int)BodyId.Chest].position;
            Quaternion rot = st.rotation;
            var f = kit.Run(life, (fx, d) =>
            {
                float k = fx.age / fx.life;
                float s = Mathf.Lerp(1f, grow, Kit.EaseOut(k));
                go.transform.SetPositionAndRotation(centre + (pos - centre) * s, rot);
                go.transform.localScale = Vector3.one * s;
                block.SetColor("_Color", flash && fx.age < 2f * F ? Color.white : p.main);
                block.SetColor("_InkColor", p.ink);
                block.SetFloat("_Ink", 0.3f);
                block.SetFloat("_Alpha", alpha * (1f - k * k));
                r.SetPropertyBlock(block);
                r.enabled = true;
                return true;
            });
            f.end = () =>
            {
                if (go != null) Destroy(go);
                if (mesh != null) Destroy(mesh);
            };
        }

        /// <summary>A thick ring of a colour bursting out along the floor (white core, ink), eaten round once it is out.</summary>
        void FloorShock(Vector3 c, float r0, float r1, Kit.Palette p, int grow = 8, int life = 16, float band = 0.3f)
        {
            var ring = new Kit.Tile(kit, "Shock ring");
            var f = kit.Run(life * F, (fx, d) =>
            {
                float a = fx.age / F;
                float r = Mathf.Lerp(r0, r1, Kit.EaseOut(a / grow));
                ring.Floor(c + Vector3.up * 0.03f, Vector3.forward, Vector2.one * (2f * r));
                ring.shape = 1f;
                ring.inner = Mathf.Clamp01(1f - band / Mathf.Max(0.05f, r));
                ring.fill = Kit.A(p.main, 1f);
                ring.core = Kit.A(Color.white, 1f);
                ring.coreWidth = 0.04f;
                ring.ink = Kit.A(p.ink, 1f);
                ring.inkWidth = 0.035f;
                ring.rim = Color.clear;
                ring.stripe = Color.clear;
                ring.flash = a < 2f ? 1f : 0f;
                ring.arc = a < grow ? 1f : 1f - (a - grow) / Mathf.Max(1f, life - grow);
                ring.fade = 1f;
                ring.Apply();
                return true;
            });
            f.tiles.Add(ring);
        }

        /// <summary>The cast: the piece's mark pops up over its head, the piece flashes its colour, an afterimage of it
        /// swells off it, a ring bursts off the floor, a puff of its colour, a two-frame freeze, and for its owner the view
        /// kicks in and its edges wash with the colour.</summary>
        void Cast(SfFxEvent e)
        {
            var pawn = e.by.Pawn;
            var pal = SwordFightSkills.Colors(e.by.Piece);
            Vector3 floor = Kit.FloorUnder(pawn.Hips.position, 0.6f);
            HeadMark(pawn, e.by.Piece);
            Afterimage(pawn, pal, 1.6f, 0.3f, 0.7f, true);
            kit.FlashBody(pawn, pal.main);
            FloorShock(floor, 0.3f, 1.5f, pal, 7, 14, 0.2f);
            kit.PuffBurst(floor + Vector3.up * 0.15f, pal, 5, 0.55f, 0.14f, 0.4f, 0.5f);
            kit.AddSquash(pawn, Kit.SquashKind.Bump);
            HitStop(2);
            Punch(pawn, null, 5f, 14);
            Tint(pawn, null, pal.main, 0.34f, 22);
        }

        float WindupOf(SwordFightSkills s, SwordFightSkillParams S) => s.Piece switch
        {
            PieceKind.Queen => S.queenWindup,
            PieceKind.Rook => S.rookWindup,
            PieceKind.Bishop => S.bishopWindup,
            PieceKind.King => S.kingGuard,
            _ => 0.4f,
        };

        readonly Dictionary<SwordFightSkills, float> moteNext = new Dictionary<SwordFightSkills, float>();

        /// <summary>A windup drawing power in: balls of the piece's colour flying into its chest, and two broken rings
        /// turning opposite ways at its feet, closing in as it fills and blinking white at the very end.</summary>
        void ChargeUp(SwordFightSkills s, SwordFightSkillParams S)
        {
            var pal = SwordFightSkills.Colors(s.Piece);
            if (!moteNext.TryGetValue(s, out float next) || kit.Clock >= next || next - kit.Clock > 1f)
            {
                moteNext[s] = kit.Clock + 2.5f * F;
                Mote(s.Pawn, pal);
            }
            var owner = s;
            kit.Keep(s, "charge rings", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Charge ring"));
                fx.tiles.Add(new Kit.Tile(kit, "Charge ring inner"));
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    float k = Mathf.Clamp01(owner.StageTime / Mathf.Max(0.05f, WindupOf(owner, S)));
                    Vector3 at = Kit.FloorUnder(owner.Pawn.Hips.position, 0.6f) + Vector3.up * 0.026f;
                    bool blink = k > 0.85f && ((int)(kit.Clock / F) & 2) == 0;
                    for (int i = 0; i < 2; i++)
                    {
                        var t = x.tiles[i];
                        float spin = kit.Clock * (i == 0 ? 540f : -400f);
                        float r = Mathf.Lerp(i == 0 ? 1.8f : 1.3f, i == 0 ? 0.95f : 0.7f, Kit.EaseOut(k));
                        t.Floor(at + Vector3.up * 0.002f * i, Quaternion.AngleAxis(spin, Vector3.up) * Vector3.forward, Vector2.one * r * Kit.Pop(x.age, 1.1f, 0.6f));
                        Kit.Horseshoe(t, pal, 0.95f, i == 0 ? 140f : 220f);
                        t.inner = i == 0 ? 0.86f : 0.8f;
                        t.flash = blink ? 1f : 0f;
                        t.fade = 1f;
                        t.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        void Mote(RagdollPawn pawn, Kit.Palette pal)
        {
            if (pawn == null) return;
            Vector3 dir = UnityEngine.Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f - 0.1f;
            dir.Normalize();
            float far = UnityEngine.Random.Range(0.8f, 1.2f);
            var ball = new Kit.Puff(kit, new Kit.Palette { main = pal.main, light = Color.white, deep = pal.deep, ink = pal.ink }, kit.meshBall) { ink = 0.35f, lump = 0f };
            var f = kit.Run(9f * F, (fx, d) =>
            {
                if (pawn == null) return false;
                float k = Kit.EaseIn(fx.age / fx.life);
                ball.t.SetPositionAndRotation(pawn.bodies[(int)BodyId.Chest].position + dir * far * (1f - k), Quaternion.LookRotation(-dir));
                float s = Mathf.Lerp(0.12f, 0.045f, k);
                ball.t.localScale = new Vector3(s, s, s * (1f + 2.5f * k));
                ball.Apply(fx.age);
                return true;
            });
            f.puffs.Add(ball);
        }

        /// <summary>A long diagonal cut across a piece: drawn in three frames, held, eaten from its start.</summary>
        void Slash(Vector3 at, Vector3 dir, Kit.Palette p, float len = 1.5f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, Kit.FlatDir(dir, Vector3.forward));
            Vector3 a = at - side * (len * 0.5f) + Vector3.up * (len * 0.3f), b = at + side * (len * 0.5f) - Vector3.up * (len * 0.22f);
            var strip = new Kit.Strip(kit, p) { core = Color.white, coreShare = 0.5f, inkShare = 0.18f };
            var pts = new List<Vector3>();
            for (int i = 0; i <= 6; i++) pts.Add(Vector3.Lerp(a, b, i / 6f));
            var f = kit.Run(14f * F, (fx, d) =>
            {
                float t = fx.age / F;
                strip.Build(pts, i => 0.03f + 0.17f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 0.92f, i / 6f)), kit.Eye);
                strip.head = Kit.EaseOut(t / 3f);
                strip.tail = t < 7f ? 0f : Kit.EaseIn((t - 7f) / 7f);
                strip.core = t < 2f ? Color.white : p.light;
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
        }

        /// <summary>The floor under a point if there is any (not a piece, not a moving body).</summary>
        static bool FloorAt(Vector3 p, out Vector3 floor)
        {
            floor = p;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.down, 7.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (RagdollPawn.ColliderOwner.ContainsKey(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.normal.y < 0.5f || h.distance >= best) continue;
                best = h.distance;
                floor = h.point;
            }
            return best < float.MaxValue;
        }

        // ---------------------------------------------------------------- warnings (held while the state lasts)

        struct Mark
        {
            public Vector3 at, along;
            public Vector2 size;
            public float shape, inner, gap;
        }

        class Warning
        {
            public readonly List<Mark> marks = new List<Mark>();
            public float k, alpha = 1f;
            /// <summary>An aim: when it ends (the skill goes, or is called off) it only fades, the white is the warning's.</summary>
            public bool quiet;
            public Kit.Palette? fill;
        }

        readonly Dictionary<(object, string), Warning> warnings = new Dictionary<(object, string), Warning>();

        /// <summary>Show these marks this frame as <paramref name="caster"/>'s warning (strength <paramref name="k"/>); when it
        /// stops asking they flash white for two frames and are gone in six.</summary>
        void Warn(SwordFightSkills owner, string key, Warning w)
        {
            string id = key + "#" + w.marks.Count;
            warnings[(owner, id)] = w;
            var caster = owner.Pawn;
            kit.Keep(owner, id, () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < w.marks.Count; i++) fx.tiles.Add(new Kit.Tile(kit, key));
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (caster == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float a = released >= 0f ? (x.age - released) / F : -1f;
                    if (a >= 6f) return false;
                    var cur = warnings[(owner, id)];
                    float flash = a >= 0f && a < 2f && !cur.quiet ? 1f : 0f;
                    float fade = released < 0f ? Mathf.Clamp01(x.age / (4f * F)) : cur.quiet ? 1f - Mathf.Clamp01(a / 4f) : 1f - Mathf.Clamp01((a - 2f) / 4f);
                    for (int i = 0; i < x.tiles.Count && i < cur.marks.Count; i++)
                    {
                        var t = x.tiles[i];
                        var m = cur.marks[i];
                        t.Floor(m.at, m.along, m.size);
                        t.shape = m.shape;
                        t.inner = m.inner;
                        t.gap = m.gap;
                        t.arc = 1f;
                        t.round = 0.07f;
                        t.fade = fade * cur.alpha;
                        kit.Warn(t, caster, released >= 0f ? 1f : cur.k, m.shape == 2f ? 0f : flash, cur.fill);   // a big disc going white floods the view
                    }
                    return true;
                };
                return fx;
            });
        }

        static Mark Square(Vector3 at, Vector3 along, float width, float length) =>
            new Mark { at = at + Vector3.up * 0.016f, along = along, size = new Vector2(width, length) };

        static Mark Ring(Vector3 at, float r, float band, Vector3 along = default) =>
            new Mark { at = at + Vector3.up * 0.018f, along = along.sqrMagnitude > 0f ? along : Vector3.forward, size = Vector2.one * (2f * r), shape = 1f, inner = Mathf.Clamp01(1f - band / r) };

        static Mark Disc(Vector3 at, float r) =>
            new Mark { at = at + Vector3.up * 0.014f, along = Vector3.forward, size = Vector2.one * (2f * r), shape = 2f };

        /// <summary>Board squares along a line on the floor, <paramref name="width"/> across, about 1.5 m long each.</summary>
        static List<Mark> Lane(Vector3 from, Vector3 dir, float start, float length, float width)
        {
            var list = new List<Mark>();
            int n = Mathf.Max(1, Mathf.CeilToInt((length - start) / 1.5f - 0.15f));
            float each = (length - start) / n;
            for (int i = 0; i < n; i++)
            {
                Vector3 c = from + dir * (start + each * (i + 0.5f));
                list.Add(Square(Kit.FloorUnder(c, 1.5f), dir, width, each - 0.06f));
            }
            return list;
        }

        // ---------------------------------------------------------------- watching the pieces each frame

        void Watch()
        {
            var S = this.S;
            if (S == null) return;
            foreach (var s in SwordFightSkills.All)
            {
                if (s == null || s.Pawn == null || s.Fighter == null || !s.Fighter.Alive) continue;
                bool mine = s.Pawn == Viewer || ViewOverride != null;
                if (s.Stage == SfStage.Aim && !mine) continue;   // the aim is the owner's own
                float aimAlpha = s.Stage == SfStage.Aim ? 0.55f : 1f;
                switch (s.Piece)
                {
                    case PieceKind.King: KingMarks(s, S); break;
                    case PieceKind.Queen: QueenMarks(s, S, aimAlpha); break;
                    case PieceKind.Rook: RookMarks(s, S, aimAlpha); break;
                    case PieceKind.Bishop: BishopMarks(s, S, aimAlpha); break;
                    case PieceKind.Knight: KnightMarks(s, S, aimAlpha); break;
                }
                if (s.SlowLeft > 0f && s.PinLeft <= 0f) SlowRing(s);   // pinned, the hands say it
                if (s.PinLeft > 0f) PinMarks(s);
            }
        }

        float Charge(SwordFightSkills s, float windup) => s.Stage == SfStage.Aim ? 0.3f : Kit.Charge(s.StageTime, windup);

        // ---------------------------------------------------------------- king

        void KingMarks(SwordFightSkills s, SwordFightSkillParams S)
        {
            if (s.Stage == SfStage.Recovery && !s.KingCountered)
            {
                // The guard came to nothing: a grey ring with sliding stripes at his feet while he is open (빈틈).
                var open = s;
                kit.Keep(s, "king open", () =>
                {
                    var fx = new Kit.Fx();
                    fx.tiles.Add(new Kit.Tile(kit, "King open"));
                    fx.step = (x, d) =>
                    {
                        if (open.Pawn == null || !kit.Held(x)) return false;
                        var t = x.tiles[0];
                        t.Floor(Kit.FloorUnder(open.Pawn.Hips.position, 0.6f) + Vector3.up * 0.02f, Vector3.forward, Vector2.one * 1.3f * Kit.Pop(x.age, 1.15f, 0.5f));
                        Kit.Horseshoe(t, Kit.Grey, 0.95f, 0f);
                        t.gap = 0f;
                        t.inner = 0.6f;
                        t.stripe = Kit.A(Kit.TeamEnemy.deep, 0.6f);
                        t.stripePhase = kit.Clock * 0.8f;
                        t.fade = 1f;
                        t.Apply();
                        return true;
                    };
                    return fx;
                });
                return;
            }
            if (s.Stage != SfStage.Windup) return;
            Vector3 floor = Kit.FloorUnder(s.Pawn.Hips.position, 0.6f);
            var w = new Warning { k = Kit.Charge(s.StageTime, S.kingGuard), alpha = 0.9f };
            w.marks.Add(Ring(floor, S.kingCounterRadius, 0.16f));
            Warn(s, "king reach", w);
            kit.Coil(s.Pawn, Mathf.Clamp01(s.StageTime / 0.12f) * 0.6f);
            // The guard: an emerald arc standing in front of him, thick, pulsing a little.
            var pawn = s.Pawn;
            kit.Keep(s, "king guard", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "King guard"));
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (pawn == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float a = released >= 0f ? (x.age - released) / F : -1f;
                    if (a >= 6f) return false;
                    Vector3 f = Kit.FlatDir(pawn.Facing, Vector3.forward);
                    var t = x.tiles[0];
                    float pop = Kit.Pop(x.age, 1.15f, 0.4f) * (1f + 0.03f * Mathf.Sin(x.age * 40f));
                    // Local +x up (the horseshoe opens to −x): an arch over the top, open at his feet.
                    t.Facing(pawn.bodies[(int)BodyId.Chest].position + f * 0.42f + Vector3.up * 0.05f, f, Vector2.one * (1.5f * pop), Vector3.Cross(f, Vector3.up));
                    Kit.Horseshoe(t, Kit.King, 0.95f, 150f);
                    t.inner = 0.72f;
                    t.flash = a >= 0f && a < 2f ? 1f : 0f;
                    t.fade = released < 0f ? 1f : 1f - Mathf.Clamp01((a - 2f) / 4f);
                    t.Apply();
                    return true;
                };
                return fx;
            });
        }

        // ---------------------------------------------------------------- queen

        void QueenMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            if (s.Stage != SfStage.Aim && s.Stage != SfStage.Windup) return;
            bool aim = s.Stage == SfStage.Aim;
            Vector3 dir = aim ? s.AimDir : s.Dir;
            float reach = aim ? s.AimReach : s.Reach;
            var w = new Warning { k = Charge(s, S.queenWindup), alpha = alpha, quiet = aim };
            // Her way: the squares she dashes over and her sword's reach past them, and a ring where she stops.
            w.marks.AddRange(Lane(s.Origin, dir, 0.3f, Mathf.Max(1f, reach + S.queenHitAhead), S.queenWidth));
            w.marks.Add(Ring(Kit.FloorUnder(s.Origin + dir * Mathf.Max(0.4f, reach), 1.5f) + Vector3.up * 0.004f, 0.45f, 0.13f, dir));
            Warn(s, aim ? "queen aim" : "queen line", w);
            if (aim) return;
            kit.Coil(s.Pawn, Mathf.Clamp01(s.StageTime / S.queenWindup));
            ChargeUp(s, S);
        }

        /// <summary>She goes: a ring and dust where she pushes off, then while she dashes a gold afterimage every other
        /// frame, a gold ribbon off her chest and her cut drawn on the floor behind her (eaten from its start once she
        /// has stopped).</summary>
        void QueenDash(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            var pal = Kit.Queen;
            Vector3 start = Kit.FloorUnder(e.at, 1.5f), dir = e.dir;
            kit.FlashRing(start, 0.75f, pal);
            kit.PuffBurst(start - dir * 0.3f + Vector3.up * 0.15f, Kit.Dust, 5, 0.6f, 0.2f, 0.4f, 0.2f);
            kit.AddSquash(pawn, Kit.SquashKind.Spring);
            Afterimage(pawn, pal, 1.35f, 0.22f, 0.7f, true);
            HitStop(2);
            Shake(pawn, null, 0.08f, 6);
            Punch(pawn, null, 7f, 16);
            Tint(pawn, null, pal.main, 0.34f, 20);
            var trail = new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.4f, inkShare = 0.2f };
            var cut = new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.45f, inkShare = 0.18f };
            var pts = new List<Vector3>();
            var line = new List<Vector3>();
            float nextGhost = 0f, stopped = -1f, run = 0f;
            var f = kit.Run(2.5f, (fx, d) =>
            {
                bool dashing = s != null && pawn != null && s.Stage == SfStage.Active && s.Piece == PieceKind.Queen;
                if (dashing)
                {
                    pts.Add(pawn.bodies[(int)BodyId.Chest].position);
                    run = Mathf.Max(run, s.DashProgress + 0.4f);
                    if (fx.age >= nextGhost)
                    {
                        Afterimage(pawn, pal, 1f, 0.2f, 0.45f);
                        nextGhost = fx.age + 2f * F;
                    }
                }
                else if (stopped < 0f) stopped = fx.age;
                while (pts.Count > 12) pts.RemoveAt(0);
                if (!dashing && pts.Count > 0) pts.RemoveAt(0);
                if (pts.Count >= 2)
                {
                    trail.Build(pts, i => 0.4f * i / Mathf.Max(1f, pts.Count - 1f), kit.Eye);
                    trail.Apply();
                }
                else trail.Hide();
                if (run > 0.5f)
                {
                    line.Clear();
                    for (int i = 0; i <= 10; i++) line.Add(Kit.FloorUnder(start + dir * (0.3f + (run - 0.3f) * i / 10f), 1.5f) + Vector3.up * 0.034f);
                    cut.Build(line, i => 0.04f + 0.3f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.06f, 0.94f, i / 10f)), kit.Eye, true);
                    cut.tail = stopped < 0f ? 0f : Kit.EaseIn(Mathf.Clamp01((fx.age - stopped - 0.4f) / 0.25f));
                    cut.Apply();
                }
                return stopped < 0f || fx.age - stopped < 0.7f;
            });
            f.strips.Add(trail);
            f.strips.Add(cut);
        }

        /// <summary>She plants: dust thrown on ahead, a ring, and every square of her way flashing white at once.</summary>
        void QueenStop(SfFxEvent e)
        {
            var s = e.by;
            Vector3 at = Kit.FloorUnder(e.at, 1.5f);
            kit.PuffBurst(at + e.dir * 0.35f + Vector3.up * 0.12f, Kit.Dust, 5, 0.55f, 0.2f, 0.45f, 0.2f);
            kit.FlashRing(at, 0.6f, Kit.Queen);
            kit.AddSquash(s.Pawn, Kit.SquashKind.Down);
            if (e.size > 0.8f) kit.Release(Kit.SquaresAlong(s.Origin + e.dir * 0.3f, e.dir, e.size, 1.2f), e.dir, s.Pawn, 1.15f, Kit.Queen);
            Shake(s.Pawn, null, 0.07f, 5);
        }

        // ---------------------------------------------------------------- rook

        void RookMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            if (s.Stage != SfStage.Aim && s.Stage != SfStage.Windup) return;
            bool aim = s.Stage == SfStage.Aim;
            Vector3 dir = aim ? s.AimDir : s.Dir;
            float reach = aim ? s.AimReach : s.Reach;
            var w = new Warning { k = Charge(s, S.rookWindup), alpha = alpha, quiet = aim };
            w.marks.AddRange(Lane(s.Origin, dir, 0.6f, Mathf.Max(1.2f, reach), S.rookWidth));
            Warn(s, aim ? "rook aim" : "rook lane", w);
            if (aim) return;
            kit.Coil(s.Pawn, Mathf.Clamp01(s.StageTime / S.rookWindup) * 0.7f);
            ChargeUp(s, S);
            Cracks(s, S);
        }

        /// <summary>The rook's line cracks open down its middle while he raises his sword (and a moment after).</summary>
        void Cracks(SwordFightSkills s, SwordFightSkillParams S)
        {
            var owner = s;
            kit.Keep(s, "rook crack", () =>
            {
                var fx = new Kit.Fx();
                var crack = new Kit.Strip(kit, new Kit.Palette { main = Kit.Rook.ink, light = Kit.Rook.deep, deep = Kit.Rook.ink, ink = Kit.Rook.ink }) { core = Kit.Rook.main, coreShare = 0.34f, inkShare = 0.3f };
                fx.strips.Add(crack);
                var pts = new List<Vector3>();
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    if (released >= 0f && x.age - released > 0.6f) return false;
                    if (pts.Count == 0)
                    {
                        Vector3 perp = Vector3.Cross(Vector3.up, owner.Dir);
                        int n = Mathf.Max(3, Mathf.CeilToInt(owner.Reach / 0.45f));
                        for (int i = 0; i <= n; i++)
                        {
                            float along = 0.5f + (owner.Reach - 0.5f) * i / n;
                            float off = i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f) * UnityEngine.Random.Range(0.06f, 0.2f);
                            pts.Add(Kit.FloorUnder(owner.Origin + owner.Dir * along + perp * off, 1.5f) + Vector3.up * 0.036f);
                        }
                    }
                    crack.Build(pts, i => 0.11f, kit.Eye, true);
                    crack.head = released >= 0f ? 1f : Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(owner.StageTime / S.rookWindup));
                    crack.fade = released >= 0f ? 1f - (x.age - released) / 0.6f : 1f;
                    crack.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The slam: the floor bursts round him, then a piece of the photo comes up out of each square as the
        /// wave reaches it, in a new random order every slam (<see cref="Statue"/>).</summary>
        void RookSlam(SfFxEvent e)
        {
            var s = e.by;
            Vector3 at = Kit.FloorUnder(e.at, 1.5f);
            FloorShock(at, 0.3f, 1.7f, Kit.Rook, 7, 15, 0.3f);
            kit.PuffBurst(at + Vector3.up * 0.2f, Kit.Stone, 6, 0.7f, 0.26f, 0.5f);
            kit.Drops(at + Vector3.up * 0.2f, Vector3.up, Kit.Stone, 5, 6f);
            kit.AddSquash(s.Pawn, Kit.SquashKind.Down);
            HitStop(5);
            Shake(s.Pawn, null, 0.17f, 9);
            Punch(s.Pawn, null, 6f, 16);
            Tint(s.Pawn, null, Kit.Rook.main, 0.34f, 20);
            float stamped = 0.6f, reach = s.Reach;
            Vector3 origin = s.Origin, dir = s.Dir;
            var caster = s.Pawn;
            var order = StatueOrder(Mathf.Max(1, Mathf.CeilToInt((reach - 0.6f) / 1.5f)));
            int next = 0;
            kit.Run(3f, (fx, d) =>
            {
                bool running = s != null && s.Stage == SfStage.Active && s.Piece == PieceKind.Rook;
                float front = running ? s.WaveFront : reach;
                while (stamped < reach - 0.3f && stamped + 0.35f <= front + 0.01f)
                {
                    float len = Mathf.Min(1.5f, reach - stamped);
                    Statue(order[Mathf.Min(next++, order.Count - 1)], origin + dir * (stamped + len * 0.5f), dir, caster);
                    stamped += 1.5f;
                }
                return running || stamped < reach - 0.3f;
            });
        }

        // ---------------------------------------------------------------- bishop

        /// <summary>A hand's size (about 0.29 m long at 1): waiting beside the bishop, and holding an ankle.</summary>
        const float HandReady = 1.15f, HandHold = 1.5f;
        /// <summary>Frames the hands take to get to the point.</summary>
        const float HandFly = 9f;

        /// <summary>Who pinned whom, from the pin's event to the frame its effect starts (and how the bishop faced).</summary>
        readonly Dictionary<RagdollPawn, (SwordFightSkills by, Vector3 dir, float at)> pinnedBy = new Dictionary<RagdollPawn, (SwordFightSkills, Vector3, float)>();

        void BishopMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            if (s.Stage != SfStage.Aim && s.Stage != SfStage.Windup) return;
            bool aim = s.Stage == SfStage.Aim;
            Vector3 p = aim ? s.AimPoint : s.Point, dir = aim ? s.AimDir : s.Dir;
            // Gold when there is an enemy on the point to take; the X is the bishop's diagonals.
            var w = new Warning { k = Charge(s, S.bishopWindup), alpha = alpha, quiet = aim, fill = s.PinCandidate != null ? Kit.Queen : (Kit.Palette?)null };
            Vector3 c = Kit.FloorUnder(p, 1.5f);
            w.marks.Add(new Mark { at = c + Vector3.up * 0.02f, along = Quaternion.AngleAxis(45f, Vector3.up) * dir, size = new Vector2(0.24f, 2.1f) });
            w.marks.Add(new Mark { at = c + Vector3.up * 0.021f, along = Quaternion.AngleAxis(-45f, Vector3.up) * dir, size = new Vector2(0.24f, 2.1f) });
            w.marks.Add(Ring(c, S.bishopRadius, 0.12f, dir));
            Warn(s, aim ? "bishop aim" : "bishop x", w);
            if (aim) return;
            ChargeUp(s, S);
            // The two hands wait by its shoulders, open and wriggling, the way they will go drawn faint on ahead of them;
            // white in the windup's last frames. When it goes they are gone here and fly from the same spots.
            var owner = s;
            kit.Keep(s, "bishop hands", () =>
            {
                var fx = new Kit.Fx();
                var hands = new[] { new GrabHand(kit, "Bishop hand L"), new GrabHand(kit, "Bishop hand R") };
                fx.strips.Add(new Kit.Strip(kit, Kit.Bishop) { core = Color.white, coreShare = 0.3f });
                fx.strips.Add(new Kit.Strip(kit, Kit.Bishop) { core = Color.white, coreShare = 0.3f });
                fx.end = () => { foreach (var h in hands) h.Destroy(); };
                var pts = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    float k = Kit.Charge(owner.StageTime, S.bishopWindup);
                    bool blink = k >= 1f && ((int)(kit.Clock / F) & 2) == 0;
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1f : 1f;
                        Vector3 start = HandStart(owner, side);
                        Vector3 end = owner.PinCandidate != null ? AnkleGrip(owner.PinCandidate, owner.Dir, side, out Vector3 inDir) : PointGrip(owner, side, out inDir);
                        Quaternion rot = Quaternion.LookRotation(owner.Dir, Vector3.up) * Quaternion.Euler(0f, 0f, -side * 18f);
                        hands[i].Place(start, rot, -25f + 10f * Mathf.Sin(kit.Clock * 28f + i * 1.7f), blink ? 1f : 0f, HandReady * Kit.Pop(x.age, 1.25f, 0f));
                        pts.Clear();
                        ReachControls(start, end, owner.Dir, inDir, side, out Vector3 p1, out Vector3 p2);
                        for (int j = 0; j <= 14; j++) pts.Add(Bezier(start, p1, p2, end, j / 14f));
                        var path = x.strips[i];
                        path.Build(pts, j => 0.045f, kit.Eye);
                        path.head = Mathf.Lerp(0.15f, 1f, k);
                        path.opacity = 0.55f;
                        path.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>Where a hand waits and flies from: beside the bishop's shoulder, a little ahead.</summary>
        static Vector3 HandStart(SwordFightSkills s, float side)
        {
            Vector3 perp = Vector3.Cross(Vector3.up, s.Dir).normalized;
            return s.Pawn.bodies[(int)BodyId.Chest].position + perp * (side * 0.42f) - Vector3.up * 0.06f + s.Dir * 0.08f;
        }

        /// <summary>Where a hand holds an ankle: the foot on its side of the piece (as the bishop sees it), the wrist just
        /// outside the piece's base on the diagonal it comes in on (<paramref name="inDir"/>: toward the line), a little
        /// into the floor.</summary>
        static Vector3 AnkleGrip(RagdollPawn pawn, Vector3 dir, float side, out Vector3 inDir)
        {
            Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;
            inDir = (dir - perp * side).normalized;
            Vector3 a = pawn.bodies[(int)BodyId.FootL].position, b = pawn.bodies[(int)BodyId.FootR].position;
            bool aLeft = Vector3.Dot(a - b, perp) < 0f;
            Vector3 foot = side < 0f ? (aLeft ? a : b) : (aLeft ? b : a);
            Vector3 floor = Kit.FloorUnder(foot, 0.4f);
            return new Vector3(foot.x, floor.y - 0.05f, foot.z) - inDir * 0.17f;
        }

        /// <summary>Where a hand lands on an empty point: flat on the floor beside it.</summary>
        static Vector3 PointGrip(SwordFightSkills s, float side, out Vector3 inDir)
        {
            Vector3 perp = Vector3.Cross(Vector3.up, s.Dir).normalized;
            inDir = (s.Dir - perp * side).normalized;
            return Kit.FloorUnder(s.Point + perp * (side * 0.22f), 1.5f) + Vector3.up * 0.04f - inDir * 0.2f;
        }

        /// <summary>A hand's way from <paramref name="start"/> to <paramref name="end"/> (a cubic curve): out on one diagonal,
        /// in on the other (the bishop's moves), arching over the floor.</summary>
        static void ReachControls(Vector3 start, Vector3 end, Vector3 dir, Vector3 inDir, float side, out Vector3 p1, out Vector3 p2)
        {
            Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 outDir = (dir + perp * side).normalized, flat = end - start;
            flat.y = 0f;
            float k = Mathf.Clamp(flat.magnitude * 0.38f, 0.5f, 2.6f);
            p1 = start + outDir * k + Vector3.up * 0.25f;
            p2 = end - inDir * k + Vector3.up * 0.45f;
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
        }

        /// <summary>A flying hand: its fingers on along its way, its palm down.</summary>
        static Quaternion FlyRotation(Vector3 tangent, Vector3 fallback)
        {
            if (tangent.sqrMagnitude < 1e-6f) tangent = fallback;
            tangent.Normalize();
            Vector3 palm = Vector3.ProjectOnPlane(Vector3.down, tangent);
            if (palm.sqrMagnitude < 1e-4f) palm = Vector3.ProjectOnPlane(fallback, tangent);
            return Quaternion.LookRotation(palm.normalized, tangent);
        }

        /// <summary>The ease of a hand's flight: quick off, slowing onto the ankle (0..1 over <see cref="HandFly"/> frames).</summary>
        static float FlyEase(float a) => a >= HandFly ? 1f : 1f - (1f - a / HandFly) * (1f - a / HandFly);

        /// <summary>A violet arm stretching behind a hand (the bishop's sleeve: light inside, inked).</summary>
        Kit.Strip ArmStrip() => new Kit.Strip(kit, Kit.Bishop) { core = Kit.Bishop.light, coreShare = 0.36f, inkShare = 0.24f };

        /// <summary>The hands go (the pinned pieces' own effects fly them: <see cref="PinMarks"/>); here the push off beside
        /// the bishop, and when they get there the hit on the point: a ring bursting along the floor, an impact ring, a
        /// puff, a hit stop, the view kicking in (for the bishop) and washing violet. Nobody on the point: the hands slap
        /// the floor there and come back (<see cref="MissedHands"/>).</summary>
        void BishopFire(SfFxEvent e)
        {
            var s = e.by;
            var caster = s.Pawn;
            Vector3 c = Kit.FloorUnder(e.at, 1.5f);
            bool got = e.order > 0;
            for (int i = 0; i < 2; i++) kit.PuffBurst(HandStart(s, i == 0 ? -1f : 1f), Kit.Bishop, 3, 0.3f, 0.1f, 0.3f, 0.2f);
            Punch(caster, null, 3f, 10);
            if (!got) MissedHands(s);
            var pal = got ? Kit.Queen : Kit.Bishop;
            float r = e.size;
            kit.Run(1f, (fx, d) =>
            {
                if (fx.age < HandFly * F) return true;
                kit.FlashRing(c, r, pal);
                kit.ImpactRing(c + Vector3.up * 0.4f, Vector3.up, pal, 0.2f, 0.7f);
                FloorShock(c, 0.2f, r + 0.5f, Kit.Bishop, 6, 14, 0.24f);
                kit.PuffBurst(c + Vector3.up * 0.1f, Kit.Bishop, 5, 0.6f, 0.14f, 0.4f, 0.3f);
                HitStop(got ? 4 : 2);
                Shake(caster, null, got ? 0.08f : 0.05f, 5);
                Punch(caster, null, 4f, 12);
                Tint(caster, null, Kit.Bishop.main, 0.3f, 18);
                return false;
            });
        }

        /// <summary>Nobody on the point: the two hands fly there all the same, slap the floor and make fists, and are
        /// pulled back to the bishop by their arms, popping away as they get there.</summary>
        void MissedHands(SwordFightSkills s)
        {
            var hands = new[] { new GrabHand(kit, "Bishop hand L"), new GrabHand(kit, "Bishop hand R") };
            var arms = new[] { ArmStrip(), ArmStrip() };
            var starts = new Vector3[2];
            var ends = new Vector3[2];
            var ins = new Vector3[2];
            var c1 = new Vector3[2];
            var c2 = new Vector3[2];
            Vector3 dir = s.Dir;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                starts[i] = HandStart(s, side);
                ends[i] = PointGrip(s, side, out ins[i]);
                ReachControls(starts[i], ends[i], dir, ins[i], side, out c1[i], out c2[i]);
            }
            var pts = new List<Vector3>();
            bool slapped = false;
            var f = kit.Run(34f * F, (fx, d) =>
            {
                float a = fx.age / F;
                // Out in nine frames, fists by thirteen, back from sixteen to thirty, popping away in the last four.
                float t = a < 16f ? FlyEase(a) : 1f - Kit.EaseIn((a - 16f) / 14f);
                for (int i = 0; i < 2; i++)
                {
                    Vector3 at = Bezier(starts[i], c1[i], c2[i], ends[i], t);
                    Quaternion fly = FlyRotation(BezierTangent(starts[i], c1[i], c2[i], ends[i], Mathf.Clamp(t, 0.02f, 0.98f)), dir);
                    Quaternion slap = Quaternion.LookRotation(Vector3.down, ins[i]);
                    Quaternion rot = a < HandFly - 3f ? fly : a < 16f ? Quaternion.Slerp(fly, slap, Mathf.Clamp01((a - (HandFly - 3f)) / 3f)) : Quaternion.Slerp(slap, fly, Mathf.Clamp01((a - 16f) / 4f));
                    float curl = a < HandFly ? -15f : Mathf.Lerp(-15f, 95f, Kit.EaseOut((a - HandFly) / 4f));
                    float scale = Mathf.Lerp(HandReady, HandHold, t) * (a > 30f ? 1f - Kit.EaseIn((a - 30f) / 4f) : 1f);
                    hands[i].Place(at, rot, curl, a >= HandFly && a < HandFly + 2f ? 1f : 0f, scale);
                    pts.Clear();
                    for (int j = 0; j <= 12; j++) pts.Add(Bezier(starts[i], c1[i], c2[i], ends[i], t * j / 12f));
                    if (t > 0.05f)
                    {
                        arms[i].Build(pts, j => Mathf.Lerp(0.06f, 0.1f, j / 12f), kit.Eye);
                        arms[i].Apply();
                    }
                    else arms[i].Hide();
                }
                if (!slapped && a >= HandFly)
                {
                    slapped = true;
                    for (int i = 0; i < 2; i++) kit.PuffBurst(ends[i] + Vector3.up * 0.05f, Kit.Dust, 3, 0.3f, 0.1f, 0.3f, 0.15f);
                }
                return true;
            });
            f.strips.AddRange(arms);
            f.end = () => { foreach (var h in hands) h.Destroy(); };
        }

        void SlowRing(SwordFightSkills s)
        {
            var pawn = s.Pawn;
            var owner = s;
            kit.Keep(s, "slowed", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Slow ring"));
                fx.tiles.Add(new Kit.Tile(kit, "Slow ripple"));
                fx.step = (x, d) =>
                {
                    if (pawn == null || !kit.Held(x) || owner.SlowLeft <= 0f) return false;
                    Vector3 at = Kit.FloorUnder(pawn.Hips.position, 0.6f) + Vector3.up * 0.02f;
                    var r = x.tiles[0];
                    r.Floor(at, Vector3.forward, Vector2.one * 1.1f * Kit.Pop(x.age, 1.15f, 0.4f));
                    Kit.Horseshoe(r, Kit.Bishop, 0.9f, 0f);
                    r.gap = 0f;
                    r.fade = Mathf.Clamp01(owner.SlowLeft / 0.2f);
                    r.Apply();
                    float w = (x.age * 1.6f) % 1f;
                    var rip = x.tiles[1];
                    rip.Floor(at - Vector3.up * 0.002f, Vector3.forward, Vector2.one * (1.1f + 0.9f * w));
                    rip.shape = 1f;
                    rip.inner = 0.88f;
                    rip.fill = Kit.A(Kit.Bishop.main, 0.6f * (1f - w));
                    rip.ink = Kit.A(Kit.Bishop.ink, 0.6f * (1f - w));
                    rip.inkWidth = 0.02f;
                    rip.core = Color.clear;
                    rip.rim = Color.clear;
                    rip.stripe = Color.clear;
                    rip.fade = r.fade;
                    rip.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>
        /// Pinned (R99, 승규 님: "묶으면 적군 발목을 잡는 듯한 연출"; R102: "처음에 스킬을 쓸 때 손 모양이 대각선으로 뻗으면서
        /// 상대 플레이어 발목을 잡는 연출", and anywhere on the floor): the bishop's two gold hands shoot out from beside its
        /// shoulders, out on one diagonal and in on the other, violet arms stretching behind them; in nine frames each is at
        /// an ankle, turning to it, and its fingers close round it in three more (white, puffs, a jolt, the piece flashes
        /// violet and is pulled down into a crouch). The arms come away from the bishop's end, the hands squeeze while the
        /// pin lasts (or until the piece is knocked down), then open and pop away. A gold ring on the floor round the piece
        /// while it is held. Pinned by something that is not a bishop's hands, they come straight up out of the floor.
        /// </summary>
        void PinMarks(SwordFightSkills s)
        {
            var pawn = s.Pawn;
            var owner = s;
            kit.Keep(s, "pinned", () =>
            {
                var fx = new Kit.Fx();
                var hands = new[] { new GrabHand(kit, "Grab hand L"), new GrabHand(kit, "Grab hand R") };
                var arms = new[] { ArmStrip(), ArmStrip() };
                fx.strips.AddRange(arms);
                fx.tiles.Add(new Kit.Tile(kit, "Pin ring"));
                fx.end = () => { foreach (var h in hands) h.Destroy(); };
                SwordFightSkills by = null;
                Vector3 dir = Kit.FlatDir(pawn.Facing, Vector3.forward);
                if (pinnedBy.TryGetValue(pawn, out var src))
                {
                    pinnedBy.Remove(pawn);
                    if (src.by != null && src.by.Pawn != null && kit.Clock - src.at < 0.3f)
                    {
                        by = src.by;
                        dir = Kit.FlatDir(src.dir, dir);
                    }
                }
                var starts = new Vector3[2];
                var ends = new Vector3[2];
                var ins = new Vector3[2];
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    ends[i] = AnkleGrip(pawn, dir, side, out ins[i]);
                    starts[i] = by != null ? HandStart(by, side) : ends[i] - Vector3.up * 0.5f;
                }
                float released = -1f;
                bool grabbed = false, puffed = false;
                var pts = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    if (pawn == null) return false;
                    bool on = kit.Held(x) && owner.PinLeft > 0f && pawn.State == PawnState.Active;
                    if (!on && released < 0f) released = x.age;
                    float a = x.age / F, r = released >= 0f ? (x.age - released) / F : -1f;
                    // Let go before they got there: they only pop away where they are.
                    bool early = released >= 0f && released < HandFly * F;
                    if (r >= (early ? 6f : 10f)) return false;
                    float t = early ? FlyEase(released / F) : FlyEase(a);
                    Vector3 centre = Kit.FloorUnder(pawn.Hips.position, 0.6f);
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1f : 1f;
                        // The ankle on its side; held, the hands stay where they took hold.
                        if (r < 0f) ends[i] = AnkleGrip(pawn, dir, side, out ins[i]);
                        Vector3 c1 = starts[i], c2 = ends[i];
                        if (by != null) ReachControls(starts[i], ends[i], dir, ins[i], side, out c1, out c2);
                        Vector3 at = Bezier(starts[i], c1, c2, ends[i], t);
                        Quaternion fly = FlyRotation(BezierTangent(starts[i], c1, c2, ends[i], Mathf.Clamp(t, 0.02f, 0.98f)), dir);
                        Quaternion grip = Quaternion.LookRotation(ins[i], Vector3.up);
                        Quaternion rot = by == null ? grip : Quaternion.Slerp(fly, grip, Mathf.Clamp01((a - (HandFly - 3f)) / 3f));
                        // Open as it flies, shut on the ankle in three frames, a small squeeze while held, open to let go.
                        float curl = early ? -15f
                            : r >= 0f ? Mathf.Lerp(80f, -25f, Kit.EaseOut(r / 4f))
                            : a < HandFly ? -15f : a < HandFly + 3f ? Mathf.Lerp(-15f, 88f, Kit.EaseOut((a - HandFly) / 3f)) : 80f + 6f * Mathf.Sin(kit.Clock * 38f);
                        float scale = Mathf.Lerp(by != null ? HandReady : HandHold, HandHold, t);
                        if (early) scale *= 1f - Kit.EaseIn(r / 6f);
                        else if (r >= 4f) scale *= 1f - Kit.EaseIn((r - 4f) / 6f);
                        hands[i].Place(at, rot, curl, r < 0f && a >= HandFly && a < HandFly + 2f ? 1f : 0f, scale);
                        // The arm, from beside the bishop to the wrist; once the hand holds, it comes away from the bishop's end.
                        var arm = arms[i];
                        float gone = early ? 1f : a < HandFly + 2f ? 0f : Kit.EaseIn((a - HandFly - 2f) / 8f);
                        if (by != null && t > 0.05f && gone < 0.999f)
                        {
                            pts.Clear();
                            for (int j = 0; j <= 12; j++) pts.Add(Bezier(starts[i], c1, c2, ends[i], t * j / 12f));
                            arm.Build(pts, j => Mathf.Lerp(0.06f, 0.1f, j / 12f), kit.Eye);
                            arm.tail = gone;
                            arm.Apply();
                        }
                        else arm.Hide();
                    }
                    if (early) return true;
                    // Held, the piece is pulled down into a crouch.
                    if (r < 0f && a >= HandFly) kit.Coil(pawn, 0.75f);
                    if (!grabbed && r < 0f && a >= HandFly)
                    {
                        grabbed = true;
                        for (int i = 0; i < 2; i++) kit.PuffBurst(ends[i] + ins[i] * 0.17f + Vector3.up * 0.08f, Kit.Bishop, 3, 0.3f, 0.09f, 0.3f, 0.15f);
                        kit.ImpactRing(centre + Vector3.up * 0.12f, Vector3.up, Kit.Bishop, 0.15f, 0.55f, false);
                        kit.FlashBody(pawn, Kit.Bishop.main);
                        kit.AddSquash(pawn, Kit.SquashKind.Bump);
                        Shake(by != null ? by.Pawn : null, pawn, 0.06f, 5);
                    }
                    if (!puffed && r >= 4f)
                    {
                        puffed = true;
                        for (int i = 0; i < 2; i++) kit.PuffBurst(ends[i] + Vector3.up * 0.15f, Kit.Bishop, 3, 0.3f, 0.1f, 0.3f);
                    }
                    var ring = x.tiles[0];
                    if (a >= HandFly)
                    {
                        ring.Floor(centre + Vector3.up * 0.022f, Vector3.forward, Vector2.one * 1.4f * Kit.Pop(x.age - HandFly * F, 1.1f, 0.6f));
                        Kit.Horseshoe(ring, Kit.Queen, 0.95f, 0f);
                        ring.inner = 0.86f;
                        ring.gap = 0f;
                        ring.flash = a < HandFly + 2f ? 1f : 0f;
                        ring.fade = r >= 0f ? 1f - r / 10f : 1f;
                        ring.Apply();
                    }
                    else ring.Hide();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>A chunky gold cartoon hand (design A props: painted, inked) with a violet cuff: a palm, four fingers of
        /// two joints and a thumb. Its root is the wrist; the fingers point along its up, the palm faces its forward and the
        /// fingers curl toward it.</summary>
        sealed class GrabHand
        {
            readonly Transform root;
            readonly List<Kit.Prop> props = new List<Kit.Prop>();
            readonly Transform[] pivots = new Transform[4], tips = new Transform[4];
            readonly Transform thumb;

            public GrabHand(Kit kit, string name)
            {
                root = new GameObject(name).transform;
                root.SetParent(kit.root, false);
                root.position = Vector3.down * 1000f;   // out of sight until it is first placed
                var pal = Kit.Queen;   // gold: the pin's colour, and it reads on the violet arms and rings
                Add(kit, kit.RoundBox(new Vector3(0.13f, 0.15f, 0.055f), 0.024f), pal, root, new Vector3(0f, 0.075f, 0f));
                for (int i = 0; i < 4; i++)
                {
                    pivots[i] = Joint(root, new Vector3(-0.048f + 0.032f * i, 0.145f, 0f));
                    Add(kit, kit.RoundBox(new Vector3(0.03f, 0.07f, 0.034f), 0.014f), pal, pivots[i], new Vector3(0f, 0.032f, 0f));
                    tips[i] = Joint(pivots[i], new Vector3(0f, 0.064f, 0f));
                    Add(kit, kit.RoundBox(new Vector3(0.028f, 0.058f, 0.032f), 0.013f), pal, tips[i], new Vector3(0f, 0.025f, 0f));
                }
                thumb = Joint(root, new Vector3(0.07f, 0.055f, 0.005f));
                Add(kit, kit.RoundBox(new Vector3(0.034f, 0.075f, 0.036f), 0.015f), pal, thumb, new Vector3(0f, 0.034f, 0f));
                var cuff = Add(kit, kit.meshTorus, Kit.Bishop, root, new Vector3(0f, 0.012f, 0f));
                cuff.t.localScale = new Vector3(0.085f, 0.3f, 0.045f);
                cuff.inkWidth = 0.008f;
            }

            static Transform Joint(Transform parent, Vector3 at)
            {
                var t = new GameObject("Joint").transform;
                t.SetParent(parent, false);
                t.localPosition = at;
                return t;
            }

            Kit.Prop Add(Kit kit, Mesh mesh, Kit.Palette pal, Transform parent, Vector3 at)
            {
                var p = new Kit.Prop(kit, mesh, pal, "Hand part", parent) { inkWidth = 0.01f };
                p.t.localPosition = at;
                props.Add(p);
                return p;
            }

            /// <summary>The wrist at <paramref name="wrist"/>, turned by <paramref name="rot"/>; <paramref name="curl"/> degrees
            /// each finger joint bends toward the palm's side (below 0 spread open).</summary>
            public void Place(Vector3 wrist, Quaternion rot, float curl, float flash, float scale)
            {
                root.SetPositionAndRotation(wrist, rot);
                root.localScale = Vector3.one * Mathf.Max(0.001f, scale);
                float open = Mathf.Clamp01(-curl / 25f);
                for (int i = 0; i < 4; i++)
                {
                    float c = curl * (1f + 0.07f * (i - 1.5f));
                    pivots[i].localRotation = Quaternion.Euler(c, 0f, (i - 1.5f) * -9f * open);
                    tips[i].localRotation = Quaternion.Euler(Mathf.Max(curl, 0f) * 0.9f, 0f, 0f);
                }
                thumb.localRotation = Quaternion.Euler(curl * 0.75f, 0f, -40f + 15f * Mathf.Clamp01(curl / 80f));
                foreach (var p in props)
                {
                    p.flash = flash;
                    p.Apply();
                }
            }

            public void Destroy()
            {
                if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        // ---------------------------------------------------------------- knight

        void KnightMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            bool aim = s.Stage == SfStage.Aim, air = s.Stage == SfStage.Active;
            if (!aim && !air) return;
            Vector3 p = aim ? s.AimPoint : s.Point, dir = aim ? s.AimDir : s.Dir;
            var w = new Warning { k = aim ? 0.3f : Kit.Charge(s.StageTime, S.knightAir), alpha = alpha, quiet = aim };
            foreach (var spot in s.KnightSpots)
            {
                w.marks.Add(Disc(spot, S.knightSpotRadius));
                w.marks.Add(Ring(spot, S.knightSpotRadius, 0.13f));
            }
            Warn(s, aim ? "knight aim" : "knight spots", w);
            // The landing: a horseshoe open behind, shrinking to the touchdown.
            var owner = s;
            kit.Keep(s, aim ? "knight shoe aim" : "knight shoe", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Knight landing"));
                fx.step = (x, d) =>
                {
                    if (owner == null || !kit.Held(x)) return false;
                    bool a2 = owner.Stage == SfStage.Aim;
                    Vector3 at = Kit.FloorUnder(a2 ? owner.AimPoint : owner.Point, 1.5f) + Vector3.up * 0.024f;
                    float k = a2 ? 0f : Mathf.Clamp01(owner.StageTime / S.knightAir);
                    var t = x.tiles[0];
                    t.Floor(at, -(a2 ? owner.AimDir : owner.Dir), Vector2.one * Mathf.Lerp(1.3f, 0.75f, k) * Kit.Pop(x.age, 1.1f, 0.5f));
                    Kit.Horseshoe(t, Kit.Knight, a2 ? 0.6f : 0.95f);
                    t.fade = a2 ? 0.7f : 1f;
                    t.Apply();
                    return true;
                };
                return fx;
            });
            if (aim)
            {
                // The arc it will fly, a thin dotted line for its owner.
                kit.Keep(s, "knight arc", () =>
                {
                    var fx = new Kit.Fx();
                    fx.strips.Add(new Kit.Strip(kit, Kit.Knight) { core = Color.white, coreShare = 0.3f });
                    fx.step = (x, d) =>
                    {
                        if (owner == null || !kit.Held(x) || owner.Stage != SfStage.Aim) return false;
                        var pts = new List<Vector3>();
                        Vector3 a = owner.Origin + Vector3.up * 0.6f, b = owner.AimPoint + Vector3.up * 0.25f;
                        for (int i = 0; i <= 16; i++)
                        {
                            float u = i / 16f;
                            pts.Add(Vector3.Lerp(a, b, u) + Vector3.up * (4f * 1.4f * u * (1f - u)));
                        }
                        x.strips[0].Build(pts, j => 0.07f, kit.Eye);
                        x.strips[0].opacity = 0.75f;
                        x.strips[0].Apply();
                        return true;
                    };
                    return fx;
                });
            }
        }

        void KnightLeap(SfFxEvent e)
        {
            kit.FlashRing(e.at, 0.6f, Kit.Knight);
            kit.DustRing(e.at, 4, 0.7f);
            var pawn = e.by.Pawn;
            var owner = e.by;
            // The horseshoe it kicks off from, stamped on the floor behind it.
            var shoe = new Kit.Tile(kit, "Knight take-off");
            Vector3 from = e.at + Vector3.up * 0.03f, back = -e.dir;
            var fs = kit.Run(24f * F, (fx, d) =>
            {
                float a = fx.age / F;
                shoe.Floor(from, back, Vector2.one * 1.2f * Kit.Pop(fx.age, 1.2f, 0.4f));
                Kit.Horseshoe(shoe, Kit.Knight, 1f);
                shoe.flash = a < 2f ? 1f : 0f;
                shoe.fade = a < 12f ? 1f : 1f - (a - 12f) / 12f;
                shoe.Apply();
                return true;
            });
            fs.tiles.Add(shoe);
            Punch(pawn, null, 5f, 14);
            var strip = new Kit.Strip(kit, Kit.Knight) { core = Color.white, coreShare = 0.3f, inkShare = 0.22f };
            var pts = new List<Vector3>();
            float nextGhost = 2f * F;
            var f = kit.Run(1.2f, (fx, d) =>
            {
                bool flying = owner != null && owner.Stage == SfStage.Active && pawn != null;
                if (flying && fx.age >= nextGhost)
                {
                    Afterimage(pawn, Kit.Knight, 1f, 0.2f, 0.45f);
                    nextGhost = fx.age + 3f * F;
                }
                if (flying) pts.Add(pawn.Hips.position);
                else if (pts.Count > 0) pts.RemoveAt(0);
                while (pts.Count > 14) pts.RemoveAt(0);
                if (pts.Count < 2) { strip.Hide(); return flying || fx.age < 0.1f; }
                strip.Build(pts, i => 0.32f * i / Mathf.Max(1f, pts.Count - 1f), kit.Eye);
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
        }

        void KnightLand(SfFxEvent e)
        {
            var s = e.by;
            kit.FlashRing(e.at, 0.8f, Kit.Knight);
            kit.DustRing(e.at, 5, 0.9f);
            FloorShock(e.at, 0.3f, 1.4f, Kit.Knight, 6, 14, 0.24f);
            Afterimage(s.Pawn, Kit.Knight, 1.45f, 0.26f, 0.7f, true);
            Punch(s.Pawn, null, 6f, 16);
            Tint(s.Pawn, null, Kit.Knight.main, 0.3f, 18);
            foreach (var spot in s.KnightSpots)
            {
                // The fork: a line shooting out along the floor from where it lands to each spot.
                var prong = new Kit.Strip(kit, Kit.Knight) { core = Color.white, coreShare = 0.45f, inkShare = 0.2f };
                Vector3 a0 = e.at + Vector3.up * 0.036f, b0 = spot + Vector3.up * 0.036f;
                var line = new List<Vector3> { a0, Vector3.Lerp(a0, b0, 0.5f), b0 };
                var fp = kit.Run(16f * F, (fx, d) =>
                {
                    float a = fx.age / F;
                    prong.Build(line, j => 0.22f, kit.Eye, true);
                    prong.head = Kit.EaseOut(a / 3f);
                    prong.tail = a < 8f ? 0f : Kit.EaseIn((a - 8f) / 8f);
                    prong.core = a < 2f ? Color.white : Kit.Knight.light;
                    prong.Apply();
                    return true;
                });
                fp.strips.Add(prong);
                kit.ImpactRing(spot + Vector3.up * 0.35f, Vector3.up, Kit.Knight, 0.2f, 0.6f, false);
                var ring = new Kit.Tile(kit, "Knight impact");
                Vector3 c = spot + Vector3.up * 0.03f;
                float r1 = e.size;
                var f = kit.Run(14f * F, (fx, d) =>
                {
                    float a = fx.age / F;
                    float r = r1 * Kit.Pop(fx.age, 1.12f, 0.3f);
                    ring.Floor(c, s.Dir, Vector2.one * (2f * r));
                    ring.shape = 1f;
                    ring.inner = Mathf.Clamp01(1f - 0.28f / r);
                    ring.fill = Kit.A(Kit.Knight.main, 1f);
                    ring.core = Kit.A(Color.white, 1f);
                    ring.coreWidth = 0.04f;
                    ring.ink = Kit.A(Kit.Knight.ink, 1f);
                    ring.inkWidth = 0.035f;
                    ring.rim = Color.clear;
                    ring.stripe = Color.clear;
                    ring.flash = a < 2f ? 1f : 0f;
                    ring.arc = a < 6f ? 1f : 1f - (a - 6f) / 8f;
                    ring.fade = 1f;
                    ring.Apply();
                    return true;
                });
                f.tiles.Add(ring);
                kit.DustRing(spot, 4, 0.9f, null, 0.3f, 0.5f);
            }
            kit.AddSquash(s.Pawn, Kit.SquashKind.Down);
            HitStop(5);
            Shake(s.Pawn, null, 0.14f, 7);
        }

        // ---------------------------------------------------------------- events

        void OnFx(SfFxEvent e)
        {
            if (!effects || kit == null || e.by == null) return;
            switch (e.kind)
            {
                case SfFxKind.Cast: Cast(e); break;
                case SfFxKind.KingParry:
                    kit.ImpactRing(e.at, e.dir, Kit.King, 0.15f, 0.5f);
                    kit.FlashBody(e.by.Pawn, Kit.King.main);
                    KingWave(e.by);
                    SpinCut(e.by.Pawn, Kit.King, S != null ? Mathf.Min(1.3f, S.kingCounterRadius * 0.5f) : 1.2f);
                    HeadMark(e.by.Pawn, PieceKind.King);   // his mark pops again (R99's crown is this now)
                    Afterimage(e.by.Pawn, Kit.King, 1.7f, 0.32f, 0.7f, true);
                    HitStop(7);
                    Shake(e.by.Pawn, null, 0.18f, 9);
                    Punch(e.by.Pawn, null, 7f, 18);
                    Tint(e.by.Pawn, null, Kit.King.main, 0.36f, 22);
                    break;
                case SfFxKind.KingCounterHit: Hit(e, Kit.King, 0, 0.16f); break;
                case SfFxKind.KingWhiff:
                    kit.PuffBurst(e.by.Pawn.bodies[(int)BodyId.Chest].position + e.dir * 0.4f, Kit.Grey, 4, 0.4f, 0.16f, 0.4f);
                    break;
                case SfFxKind.QueenThrust: QueenDash(e); break;
                case SfFxKind.QueenHit:
                    if (e.target != null) Slash(e.target.bodies[(int)BodyId.Chest].position, e.by.Dir, Kit.Queen);
                    Hit(e, Kit.Queen, e.order == 0 ? 5 : 3, e.order == 0 ? 0.16f : 0.1f);
                    break;
                case SfFxKind.QueenStop: QueenStop(e); break;
                case SfFxKind.RookSlam: RookSlam(e); break;
                case SfFxKind.RookHit:
                    Hit(e, Kit.Rook, 4, 0.14f);
                    if (e.target != null) kit.Drops(Kit.FloorUnder(e.target.Hips.position, 0.6f) + Vector3.up * 0.2f, Vector3.up, Kit.Stone, 4, 7f);
                    break;
                case SfFxKind.RookBlocked:
                {
                    Vector3 at = Kit.FloorUnder(e.at, 1.5f) + Vector3.up * 0.5f;
                    kit.ImpactRing(at, -e.dir, Kit.Rook, 0.3f, 0.85f);
                    kit.Drops(at, -e.dir + Vector3.up * 0.5f, Kit.Stone, 4);
                    kit.DustRing(at - Vector3.up * 0.5f, 5, 0.8f);
                    HitStop(4);
                    break;
                }
                case SfFxKind.BishopFire: BishopFire(e); break;
                case SfFxKind.BishopSlow: break;   // the hands say it when they get there (PinMarks)
                case SfFxKind.BishopPin:
                    if (e.target != null) pinnedBy[e.target] = (e.by, e.dir, kit.Clock);
                    break;
                case SfFxKind.KnightLeap: KnightLeap(e); break;
                case SfFxKind.KnightLand: KnightLand(e); break;
                case SfFxKind.KnightHit: Hit(e, Kit.Knight, 0, 0.12f); break;
                case SfFxKind.Interrupted:
                    kit.PuffBurst(e.at, Kit.Grey, 4, 0.4f, 0.16f, 0.4f);
                    break;
            }
        }

        void KingWave(SwordFightSkills s)
        {
            var S = this.S;
            float R = S != null ? S.kingCounterRadius : 2.5f;
            Vector3 c = Kit.FloorUnder(s.Pawn.Hips.position, 0.6f);
            FloorShock(c, 0.4f, R, Kit.King, 8, 16, 0.32f);
            kit.DustRing(c + Vector3.up * 0.03f, 6, R * 0.7f);
            kit.AddSquash(s.Pawn, Kit.SquashKind.Spring);
        }

        /// <summary>The king's answer drawn as one full-circle cut round him at sword height: around in five frames,
        /// held, eaten from its start.</summary>
        void SpinCut(RagdollPawn pawn, Kit.Palette p, float radius)
        {
            if (pawn == null) return;
            Vector3 c = pawn.bodies[(int)BodyId.HandR].position;
            c.x = pawn.Hips.position.x;
            c.z = pawn.Hips.position.z;
            float start = Mathf.Atan2(pawn.Facing.z, pawn.Facing.x);
            var pts = new List<Vector3>();
            for (int i = 0; i <= 32; i++)
            {
                float ang = start - i / 32f * Mathf.PI * 2f;
                pts.Add(c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius);
            }
            var strip = new Kit.Strip(kit, p) { core = Color.white, coreShare = 0.45f, inkShare = 0.2f };
            var f = kit.Run(16f * F, (fx, d) =>
            {
                float a = fx.age / F;
                strip.Build(pts, i => 0.12f + 0.2f * i / 32f, kit.Eye, true);
                strip.head = Kit.EaseOut(a / 5f);
                strip.tail = a < 8f ? 0f : Kit.EaseIn((a - 8f) / 8f);
                strip.core = a < 2f ? Color.white : p.light;
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
        }

        /// <summary>A skill hit in design A: the body white then the hitter's colour, an impact ring, drops and a C half
        /// ring the way it flies, a hit stop, a shake and a kick of the view, and the captured square if it goes down.</summary>
        void Hit(SfFxEvent e, Kit.Palette p, int stopFrames, float shakeMetres)
        {
            if (e.target == null) return;
            kit.FlashBody(e.target, p.main);
            kit.ImpactRing(e.at, e.dir, p, 0.18f, 0.55f);
            kit.Drops(e.at, e.dir + Vector3.up * 0.4f, p, 5);
            kit.HalfRing(e.at, e.dir, p, 0.8f, 0.2f);
            kit.Down(e.target, p, e.at);
            HitStop(Mathf.Max(stopFrames, 3));
            Shake(e.by.Pawn, e.target, shakeMetres, 7);
            Punch(e.by.Pawn, e.target, 3f, 10);
        }
    }
}
