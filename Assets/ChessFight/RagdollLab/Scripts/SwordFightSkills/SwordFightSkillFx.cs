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
    /// </summary>
    [DefaultExecutionOrder(210)]   // after the match camera (150)
    public class SwordFightSkillFx : MonoBehaviour
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
        }

        // ---------------------------------------------------------------- time, hit stop, shake

        void Update()
        {
            // Effect time: the game's own pace (slow motion slows the effects too), but a hit stop does not stop them.
            float real = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (Time.timeScale > 0f && !stopping) rate = Time.timeScale;
            dt = real * rate;
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

        /// <summary>The cast: the piece flashes its colour, an afterimage of it swells off it, a ring bursts off the floor,
        /// a puff of its colour, a two-frame freeze, and for its owner the view kicks in and its edges wash with the colour.</summary>
        void Cast(SfFxEvent e)
        {
            var pawn = e.by.Pawn;
            var pal = SwordFightSkills.Colors(e.by.Piece);
            Vector3 floor = Kit.FloorUnder(pawn.Hips.position, 0.6f);
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
                if (s.SlowLeft > 0f) SlowRing(s);
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

        /// <summary>The slam: the floor bursts round him, then a stone tower comes up out of each square as the wave
        /// reaches it.</summary>
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
            var S = this.S;
            float stamped = 0.6f, reach = s.Reach, width = S != null ? S.rookWidth : 1.5f;
            Vector3 origin = s.Origin, dir = s.Dir;
            var caster = s.Pawn;
            kit.Run(3f, (fx, d) =>
            {
                bool running = s != null && s.Stage == SfStage.Active && s.Piece == PieceKind.Rook;
                float front = running ? s.WaveFront : reach;
                while (stamped < reach - 0.3f && stamped + 0.35f <= front + 0.01f)
                {
                    float len = Mathf.Min(1.5f, reach - stamped);
                    Tower(origin + dir * (stamped + len * 0.5f), dir, caster, len, width);
                    stamped += 1.5f;
                }
                return running || stamped < reach - 0.3f;
            });
        }

        /// <summary>A stone tower with an orange band and four battlements, up out of the floor in four frames past its
        /// height, held, and back down into it; the square under it flashes, dust and stones fly.</summary>
        void Tower(Vector3 c, Vector3 dir, RagdollPawn caster, float len, float width)
        {
            if (!FloorAt(c, out Vector3 floor)) return;   // no floor there: nothing comes up
            float w = Mathf.Min(width * 0.72f, 1.1f), l = Mathf.Min(len * 0.72f, 1.1f), h = 0.8f;
            var parts = new List<(Kit.Prop p, Vector3 at)>
            {
                (new Kit.Prop(kit, kit.RoundBox(new Vector3(w, h, l), 0.07f), Kit.Stone, "Rook tower") { inkWidth = 0.02f }, new Vector3(0f, h * 0.5f, 0f)),
                (new Kit.Prop(kit, kit.RoundBox(new Vector3(w * 1.08f, 0.13f, l * 1.08f), 0.04f), Kit.Rook, "Rook tower band") { inkWidth = 0.016f }, new Vector3(0f, h - 0.07f, 0f)),
            };
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                parts.Add((new Kit.Prop(kit, kit.RoundBox(new Vector3(w * 0.3f, 0.2f, l * 0.3f), 0.04f), Kit.Rook, "Rook battlement") { inkWidth = 0.016f },
                    new Vector3(sx * w * 0.33f, h + 0.08f, sz * l * 0.33f)));
            }
            Quaternion rot = Quaternion.LookRotation(Kit.FlatDir(dir, Vector3.forward), Vector3.up);
            kit.Release(new List<Vector3> { floor + Vector3.up * 0.016f }, dir, caster, 1.45f);
            kit.DustRing(floor, 5, 0.85f, null, 0.28f, 0.5f);
            kit.Drops(floor + Vector3.up * 0.3f, Vector3.up, Kit.Stone, 3, 6f);
            float H = h + 0.2f;
            void Pose(float up, float flash)
            {
                Vector3 baseAt = floor + Vector3.up * (H * (up - 1f) - 0.02f);
                foreach (var (p, at) in parts)
                {
                    p.t.SetPositionAndRotation(baseAt + rot * at, rot);
                    p.flash = flash;
                    p.Apply();
                }
            }
            // Made inside another effect's step, it is first stepped next frame: until then it waits under the floor
            // (a prop shows from the moment it is made, at the effects' root).
            Pose(0f, 1f);
            var f = kit.Run(36f * F, (fx, d) =>
            {
                float a = fx.age / F;
                Pose(a < 4f ? Kit.EaseOut(a / 4f) * 1.12f : a < 7f ? Mathf.Lerp(1.12f, 1f, (a - 4f) / 3f) : a < 20f ? 1f : 1f - Kit.EaseIn((a - 20f) / 16f),
                    a < 2f ? 1f : 0f);
                return true;
            });
            foreach (var (p, _) in parts) f.props.Add(p);
        }

        // ---------------------------------------------------------------- bishop

        void BishopMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            if (s.Stage != SfStage.Aim && s.Stage != SfStage.Windup) return;
            bool aim = s.Stage == SfStage.Aim;
            Vector3 p = aim ? s.AimPoint : s.Point, dir = aim ? s.AimDir : s.Dir;
            bool pin = s.PinCondition;
            var w = new Warning { k = Charge(s, S.bishopWindup), alpha = alpha, quiet = aim, fill = pin ? Kit.Queen : (Kit.Palette?)null };
            Vector3 c = Kit.FloorUnder(p, 1.5f);
            w.marks.Add(new Mark { at = c + Vector3.up * 0.02f, along = Quaternion.AngleAxis(45f, Vector3.up) * dir, size = new Vector2(0.24f, 2.1f) });
            w.marks.Add(new Mark { at = c + Vector3.up * 0.021f, along = Quaternion.AngleAxis(-45f, Vector3.up) * dir, size = new Vector2(0.24f, 2.1f) });
            w.marks.Add(Ring(c, S.bishopRadius, 0.12f, dir));
            if (pin && s.PinCandidate != null)
            {
                // The floor ends behind it: a gold strip from the piece to the edge.
                Vector3 from = Kit.FloorUnder(s.PinCandidate.Hips.position, 0.6f);
                w.marks.Add(Square(from + dir * (S.bishopPinBehind * 0.5f), dir, 0.3f, S.bishopPinBehind));
            }
            Warn(s, aim ? "bishop aim" : "bishop x", w);
            if (aim) return;
            ChargeUp(s, S);
            // The two rays gathering on the point as the windup runs (thin, then the throw makes them thick).
            var owner = s;
            kit.Keep(s, "bishop rays", () =>
            {
                var fx = new Kit.Fx();
                var pal = pin ? Kit.Queen : Kit.Bishop;
                fx.strips.Add(new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.4f });
                fx.strips.Add(new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.4f });
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    float k = Kit.Charge(owner.StageTime, S.bishopWindup);
                    for (int i = 0; i < 2; i++)
                    {
                        var (a, b) = RayEnds(owner, i == 0 ? -1f : 1f);
                        var pts = new List<Vector3> { a, Vector3.Lerp(a, b, 0.5f), b };
                        x.strips[i].Build(pts, j => 0.06f + 0.06f * k, kit.Eye);
                        x.strips[i].head = Mathf.Lerp(0.25f, 1f, k);
                        x.strips[i].opacity = 0.7f;
                        x.strips[i].Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>A ray from beside the bishop's shoulder through the point and 1.6 m on into the floor.</summary>
        static (Vector3, Vector3) RayEnds(SwordFightSkills s, float side)
        {
            Vector3 perp = Vector3.Cross(Vector3.up, s.Dir).normalized;
            Vector3 a = s.Pawn.bodies[(int)BodyId.Chest].position + perp * (side * 0.9f) + Vector3.up * 0.35f;
            Vector3 p = Kit.FloorUnder(s.Point, 1.5f) + Vector3.up * 0.5f;
            Vector3 u = (p - a).normalized;
            Vector3 b = p + u * 1.6f;
            if (b.y < p.y - 0.45f) b = p + u * ((0.45f) / Mathf.Max(0.05f, -u.y));
            return (a, b);
        }

        void BishopFire(SfFxEvent e)
        {
            var s = e.by;
            var pal = s.PinCondition ? Kit.Queen : Kit.Bishop;
            for (int i = 0; i < 2; i++)
            {
                var (a, b) = RayEnds(s, i == 0 ? -1f : 1f);
                var strip = new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.4f, inkShare = 0.22f };
                var f = kit.Run(0.3f, (fx, d) =>
                {
                    var pts = new List<Vector3> { a, Vector3.Lerp(a, b, 0.5f), b };
                    float k = fx.age / fx.life;
                    strip.Build(pts, j => fx.age < 2f * F ? 0.34f : 0.26f * (1f - k * 0.5f), kit.Eye);
                    strip.core = fx.age < 2f * F ? Color.white : pal.light;
                    strip.tail = Kit.EaseIn(k);
                    strip.Apply();
                    return true;
                });
                f.strips.Add(strip);
            }
            Vector3 c = Kit.FloorUnder(e.at, 1.5f);
            kit.FlashRing(c, e.size, pal);
            kit.ImpactRing(c + Vector3.up * 0.5f, Vector3.up, pal, 0.2f, 0.7f);
            FloorShock(c, 0.2f, e.size + 0.5f, pal, 6, 14, 0.24f);
            // And a beam straight down onto the point, thick for two frames, thinning away.
            var beam = new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.45f, inkShare = 0.2f };
            var col = new List<Vector3> { c + Vector3.up * 3.2f, c + Vector3.up * 1.6f, c + Vector3.up * 0.05f };
            var fb = kit.Run(12f * F, (fx, d) =>
            {
                float a = fx.age / F;
                beam.Build(col, j => a < 2f ? 0.75f : Mathf.Lerp(0.55f, 0.05f, (a - 2f) / 10f), kit.Eye);
                beam.core = a < 2f ? Color.white : pal.light;
                beam.Apply();
                return true;
            });
            fb.strips.Add(beam);
            kit.PuffBurst(c + Vector3.up * 0.1f, pal, 5, 0.6f, 0.14f, 0.4f, 0.3f);
            HitStop(4);
            Shake(s.Pawn, null, 0.08f, 5);
            Punch(s.Pawn, null, 4f, 12);
            Tint(s.Pawn, null, pal.main, 0.3f, 18);
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
        /// Pinned (R99, 승규 님: "묶으면 적군 발목을 잡는 듯한 연출"): a violet hand comes up out of a dark hole in the floor
        /// beside each foot, open, and in the next four frames its fingers close round the ankle (a white flash, a puff,
        /// a jolt); it squeezes while the pin lasts, then lets go and sinks back. A gold ring on the floor round the piece.
        /// </summary>
        void PinMarks(SwordFightSkills s)
        {
            var pawn = s.Pawn;
            var owner = s;
            kit.Keep(s, "pinned", () =>
            {
                var fx = new Kit.Fx();
                var hands = new[] { new GrabHand(kit, "Grab hand L"), new GrabHand(kit, "Grab hand R") };
                var holes = new[] { new Kit.Tile(kit, "Grab hole L"), new Kit.Tile(kit, "Grab hole R") };
                fx.tiles.AddRange(holes);
                fx.tiles.Add(new Kit.Tile(kit, "Pin ring"));
                fx.end = () => { foreach (var h in hands) h.Destroy(); };
                float released = -1f;
                bool grabbed = false;
                var feet = new Vector3[2];
                fx.step = (x, d) =>
                {
                    if (pawn == null) return false;
                    bool on = kit.Held(x) && owner.PinLeft > 0f;
                    if (!on && released < 0f) released = x.age;
                    float a = x.age / F, r = released >= 0f ? (x.age - released) / F : -1f;
                    if (r >= 10f) return false;
                    Vector3 facing = Kit.FlatDir(pawn.Facing, Vector3.forward);
                    Vector3 right = Vector3.Cross(Vector3.up, facing);
                    Vector3 centre = Kit.FloorUnder(pawn.Hips.position, 0.6f);
                    // Open as it rises, shut on the ankle by frame 9, a small squeeze while held, open again to let go.
                    float rise = r >= 0f ? 1f - Kit.EaseIn(r / 8f) : Kit.EaseOutBack(a / 5f);
                    float curl = r >= 0f ? Mathf.Lerp(80f, -25f, Kit.EaseOut(r / 4f))
                        : a < 5f ? -25f : a < 9f ? Mathf.Lerp(-25f, 88f, Kit.EaseOut((a - 5f) / 4f)) : 80f + 6f * Mathf.Sin(kit.Clock * 38f);
                    float flash = r < 0f && a >= 9f && a < 11f ? 1f : 0f;
                    for (int i = 0; i < 2; i++)
                    {
                        // The hands hold the feet where they are; letting go, they stay where they let go.
                        if (r < 0f) feet[i] = pawn.bodies[(int)(i == 0 ? BodyId.FootL : BodyId.FootR)].position;
                        Vector3 side = Kit.FlatDir(feet[i] - pawn.Hips.position, right * (i == 0 ? -1f : 1f));
                        if (Vector3.Dot(side, right * (i == 0 ? -1f : 1f)) < 0.2f) side = right * (i == 0 ? -1f : 1f);
                        Vector3 floor = Kit.FloorUnder(feet[i], 0.4f);
                        Vector3 at = new Vector3(feet[i].x, floor.y, feet[i].z) + side * 0.075f;
                        hands[i].Pose(at, -side, rise, curl, flash, 0.85f);
                        var hole = holes[i];
                        hole.Floor(new Vector3(at.x, floor.y + 0.012f, at.z), facing, new Vector2(0.26f, 0.2f) * (r >= 0f ? 1f - Mathf.Clamp01(r / 10f) : Kit.Pop(x.age, 1.15f, 0f)));
                        hole.shape = 2f;
                        hole.fill = Kit.A(Kit.Bishop.ink, 0.95f);
                        hole.core = Color.clear;
                        hole.ink = Kit.A(Kit.Bishop.main, 1f);
                        hole.inkWidth = 0.03f;
                        hole.rim = Color.clear;
                        hole.stripe = Color.clear;
                        hole.fade = 1f;
                        hole.Apply();
                    }
                    if (!grabbed && r < 0f && a >= 9f)
                    {
                        grabbed = true;
                        for (int i = 0; i < 2; i++) kit.PuffBurst(feet[i] + Vector3.up * 0.05f, Kit.Bishop, 3, 0.3f, 0.09f, 0.3f, 0.15f);
                        kit.ImpactRing(centre + Vector3.up * 0.12f, Vector3.up, Kit.Bishop, 0.15f, 0.55f, false);
                        kit.AddSquash(pawn, Kit.SquashKind.Bump);
                        Shake(owner.Pawn, null, 0.06f, 5);
                    }
                    if (released >= 0f && Mathf.Abs(r) < 1e-4f)
                        kit.PuffBurst(centre + Vector3.up * 0.15f, Kit.Bishop, 4, 0.4f, 0.12f, 0.35f);
                    var ring = x.tiles[2];
                    ring.Floor(centre + Vector3.up * 0.022f, Vector3.forward, Vector2.one * 1.4f * Kit.Pop(x.age, 1.1f, 0.6f));
                    Kit.Horseshoe(ring, Kit.Queen, 0.95f, 0f);
                    ring.inner = 0.86f;
                    ring.gap = 0f;
                    ring.flash = a >= 9f && a < 11f ? 1f : 0f;
                    ring.fade = r >= 0f ? 1f - r / 10f : 1f;
                    ring.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>A chunky violet cartoon hand (design A props: painted, inked) with a gold cuff: a palm, four fingers
        /// of two joints and a thumb. Its root sits on the floor; the palm stands up facing <c>Pose</c>'s direction and
        /// the fingers curl toward it.</summary>
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
                var pal = Kit.Bishop;
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
                var cuff = Add(kit, kit.meshTorus, Kit.Queen, root, new Vector3(0f, 0.012f, 0f));
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

            /// <summary><paramref name="rise"/> 0 under the floor .. 1 up; <paramref name="curl"/> degrees each finger joint
            /// bends toward <paramref name="faces"/> (below 0 spread open).</summary>
            public void Pose(Vector3 at, Vector3 faces, float rise, float curl, float flash, float scale)
            {
                root.SetPositionAndRotation(at + Vector3.up * (-0.32f * scale * (1f - rise)), Quaternion.LookRotation(Kit.FlatDir(faces, Vector3.forward), Vector3.up));
                root.localScale = Vector3.one * scale;
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
                    Crown(e.by.Pawn);
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
                case SfFxKind.BishopSlow:
                    kit.FlashBody(e.target, Kit.Bishop.main);
                    kit.Drops(e.at, Vector3.up, Kit.Bishop, 3, 5f);
                    break;
                case SfFxKind.BishopPin:
                    kit.FlashRing(e.at, 0.8f, Kit.Queen);
                    Shake(e.by.Pawn, e.target, 0.08f, 5);
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

        /// <summary>A crown popping up over the king's head (his colour, inked), held a moment, hopping back down into him.</summary>
        void Crown(RagdollPawn pawn)
        {
            if (pawn == null) return;
            var root = new GameObject("King crown").transform;
            root.SetParent(kit.root, false);
            var parts = new List<Kit.Prop>();
            var band = new Kit.Prop(kit, kit.meshTorus, Kit.King, "Crown band", root) { inkWidth = 0.01f };
            band.t.localScale = new Vector3(0.15f, 0.75f, 0.15f);
            parts.Add(band);
            for (int i = 0; i < 5; i++)
            {
                float ang = i / 5f * Mathf.PI * 2f;
                var spike = new Kit.Prop(kit, kit.RoundBox(new Vector3(0.05f, 0.12f, 0.05f), 0.02f), Kit.King, "Crown point", root) { inkWidth = 0.01f };
                spike.t.localPosition = new Vector3(Mathf.Cos(ang) * 0.15f, 0.07f, Mathf.Sin(ang) * 0.15f);
                spike.t.localRotation = Quaternion.AngleAxis(-12f, Vector3.Cross(Vector3.up, spike.t.localPosition.normalized));
                parts.Add(spike);
                var gem = new Kit.Prop(kit, kit.RoundBox(Vector3.one * 0.05f, 0.024f), Kit.Queen, "Crown ball", spike.t) { inkWidth = 0.01f };
                gem.t.localPosition = new Vector3(0f, 0.07f, 0f);
                parts.Add(gem);
            }
            var f = kit.Run(0.8f, (fx, d) =>
            {
                if (pawn == null) return false;
                float a = fx.age / F;
                float hop = fx.age > 0.6f ? Mathf.Clamp01((fx.age - 0.6f) / 0.2f) : 0f;
                float s = Kit.Pop(fx.age, 1.3f, 0f) * (1f - hop);
                root.SetPositionAndRotation(pawn.bodies[(int)BodyId.Head].position + Vector3.up * (0.34f + 0.08f * Kit.EaseOutBack(Mathf.Min(1f, a / 8f)) - 0.3f * hop),
                    Quaternion.Euler(0f, kit.Clock * 120f, 0f));
                root.localScale = Vector3.one * Mathf.Max(0.001f, s);
                foreach (var p in parts)
                {
                    p.flash = a < 2f ? 1f : 0f;
                    p.Apply();
                }
                return true;
            });
            f.end = () => { if (root != null) Destroy(root.gameObject); };
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
