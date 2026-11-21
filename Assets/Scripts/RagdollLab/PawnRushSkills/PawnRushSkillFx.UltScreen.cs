using System;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
#if CF_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The ultimates' shared cast on the screen and in the ears, every PC its own (JY-skill-lab, 2026-11-21; see
    /// PawnRushSkillFx.Ult.cs for the world). Nobody's camera is turned (the user: "3인칭 게임인데 저렇게 시점이 강제로 바뀌면 ...
    /// 조작하고 있었는데 그걸 방해하지 않을까"): the caster's own view only comes closer and lower, narrows, tilts a little and
    /// kicks on the beats, the way it looks stays the player's.
    ///
    ///   the grade    the world drains to grey over 0.8 s; on the first heartbeat the neon comes on: everything but teal and
    ///                red loses its colour (a hue curve) and those two stay; darker in the silence; a punch of contrast and
    ///                colour split at the landing; it all comes back over 1.6 s at the end
    ///   the banner   under the top bar on every screen: teal for the caster's side, red for the other (with "빨간 칸에서
    ///                벗어나요!"), the caster's face live in its window until the call
    ///   the words    the whisper ("나이트…"), the call ("패밀리 포크!!") with the move typed in the air (Nf7+!!), the
    ///                judgement ("탁월한 수!!"); an enemy named sees a red warning and its screen flashes red on the beats
    ///   the sound    the key's boom, a rising shimmer, the heartbeats, a tick for each name, silence (the music too),
    ///                the drop, chimes climbing with the tour's heads, the landing, the !! chime; made in code here, with
    ///                the knight's own clips (Resources/PawnRushSkillSfx)
    /// </summary>
    public partial class PawnRushSkillFx
    {
        enum UltSfx { Boom, Heart, Tick, Leap, Stomp, Impact, Drop, Bling, Type }

        /// <summary>Every sound the cast starts (clip, volume, pitch, delay s), for a film's sound track (PawnRushUltFilm).</summary>
        public static event Action<AudioClip, float, float, float> UltSoundPlayed;

#if CF_URP
        GameObject gradeHolder;
        Volume gradeVolume, neonVolume;
        VolumeProfile gradeProfile, neonProfile;
        ColorAdjustments ultAdjust;
        Vignette ultVignette;
        ChromaticAberration ultAberration;
#endif
        // Screen pulses, falling off: the beat's, the red warning's, the landing's punch.
        float beatKick, redKick, punchKick, whiteKick;
        Camera pipCam;
        RenderTexture pipTex;
        UltShow pipShow;
        AudioSource ultAudio, ultRise;
        AudioClip sfxBoom, sfxHeart, sfxTick, sfxDrop, sfxRise, sfxBling, sfxType, sfxImpactLow;
        readonly AudioClip[] sfxChime = new AudioClip[8];
        AudioClip clipLeap, clipStomp, clipLand, clipWall;
        GUIStyle ultTitle, ultSub, ultBig, ultSmall, ultGlyph;
        Texture2D ultGradTeal, ultGradRed;

        // ---------------------------------------------------------------- the moments

        void UltScreenStart(UltShow u)
        {
            u.title = u.Fork ? "패밀리 포크" : u.Tour ? "나이트 투어" : PawnRushSkillBook.Name(u.skill);
            u.nota = u.Fork ? "Nf7+!!" : u.Tour ? "Nf5-d6-e8!!" : "!!";
            UltAudioBuild();
            UltPlay(sfxBoom, 1f);
            if (ultRise != null && sfxRise != null)
            {
                ultRise.Stop();
                ultRise.clip = sfxRise;
                ultRise.volume = 0.55f * ChessFight.Game.GameSettings.Effects;
                ultRise.Play();
                UltSoundPlayed?.Invoke(sfxRise, ultRise.volume, 1f, 0f);
            }
            whiteKick = Mathf.Max(whiteKick, Viewer == u.by ? 0.12f : 0.05f);
        }

        void UltScreenBeat(UltShow u, int i)
        {
            beatKick = 1f;
            var me = Viewer;
            if (me != null && u.marked.Contains(me)) redKick = 1f;
            if (me == u.by) whiteKick = Mathf.Max(whiteKick, 0.05f);
        }

        void UltScreenMarked(UltShow u, RagdollPawn e)
        {
            if (Viewer == e) redKick = 1f;
        }

        void UltScreenTourAdded(UltShow u) { }

        void UltScreenGo(UltShow u)
        {
            if (ultRise != null) ultRise.Stop();
            UltPlay(sfxDrop, 1f);
            whiteKick = Mathf.Max(whiteKick, Viewer == u.by ? 0.22f : 0.08f);
            u.typed = 0;
        }

        void UltScreenStrike(UltShow u, SkillFxEvent e)
        {
            u.strikeAt = Clock;
            if (Viewer == e.target) redKick = 1f;
        }

        void UltScreenHit(UltShow u, SkillFxEvent e)
        {
            punchKick = 1f;
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, e.at) / 40f) : 0.5f;
            whiteKick = Mathf.Max(whiteKick, Viewer == u.by ? 0.3f : 0.1f + 0.15f * near);
            UltPlay(sfxBling, 0.9f, 0.18f);
        }

        void UltScreenCut(UltShow u)
        {
            if (ultRise != null) ultRise.Stop();
        }

        void UltScreenDestroy()
        {
#if CF_URP
            if (gradeHolder != null) Destroy(gradeHolder);
            if (gradeProfile != null) Destroy(gradeProfile);
            if (neonProfile != null) Destroy(neonProfile);
#endif
            if (pipCam != null) Destroy(pipCam.gameObject);
            if (pipTex != null) { pipTex.Release(); Destroy(pipTex); }
            foreach (var c in new[] { sfxBoom, sfxHeart, sfxTick, sfxDrop, sfxRise, sfxBling, sfxType, sfxImpactLow }) if (c != null) Destroy(c);
            foreach (var c in sfxChime) if (c != null) Destroy(c);
            foreach (var t in new[] { ultGradTeal, ultGradRed }) if (t != null) Destroy(t);
            ChessFight.Game.MusicDuck.Level = 1f;
        }

        // ---------------------------------------------------------------- every frame

        /// <summary>No show: everything the shows set goes back (once).</summary>
        void UltScreenIdle(float dt, Camera cam)
        {
            beatKick = redKick = punchKick = whiteKick = 0f;
#if CF_URP
            if (gradeVolume != null && gradeVolume.weight > 0f) gradeVolume.weight = 0f;
            if (neonVolume != null && neonVolume.weight > 0f) neonVolume.weight = 0f;
#endif
            if (pipCam != null && pipCam.enabled) pipCam.enabled = false;
            if (ChessFight.Game.MusicDuck.Level != 1f) ChessFight.Game.MusicDuck.Level = 1f;
        }

        void UltScreenStep(float dt, Camera cam)
        {
            beatKick = Mathf.Max(0f, beatKick - dt / 0.22f);
            redKick = Mathf.Max(0f, redKick - dt / 0.3f);
            punchKick = Mathf.Max(0f, punchKick - dt / 0.16f);
            whiteKick = Mathf.Max(0f, whiteKick - dt * 1.6f);
            float drain = 0f, hush = 0f, duck = 1f;
            bool neon = false;
            UltShow face = null;
            foreach (var u in ults)
            {
                float a = Clock - u.t0, life = u.Life(Clock);
                // The colour drains over 0.8 s and comes back over the end's 1.6 s (half a second for a cut).
                float d = Smooth01(a / 0.8f) * life;
                if (u.hitAt >= 0f) d *= 1f - Smooth01((Clock - u.hitAt - 1.3f) / 1.6f);
                drain = Mathf.Max(drain, d);
                if (Clock >= u.Beat(0) && u.endAt < 0f && (u.hitAt < 0f || Clock < u.hitAt + 1.3f)) neon = true;
                if (Clock >= u.Hush && Clock < u.Go + 0.05f && u.endAt < 0f) hush = Mathf.Max(hush, Smooth01((Clock - u.Hush) / 0.12f));
                // The music: down at the key, gone in the silence, back from the call.
                float m = Mathf.Lerp(1f, 0.3f, Smooth01(a / 0.4f));
                if (Clock >= u.Hush) m = Clock < u.Go ? 0f : Mathf.Lerp(0.15f, 1f, Smooth01((Clock - u.Go) / 2.5f));
                if (u.endAt >= 0f) m = Mathf.Max(m, 1f - life);
                duck = Mathf.Min(duck, m);
                if (u.endAt < 0f && Clock < u.Go + 0.7f && u.by != null) face = u;
                UltCameraMods(u, cam, dt);
            }
            ChessFight.Game.MusicDuck.Level = duck;
            UltPipStep(face);
#if CF_URP
            UltGradeBuild();
            if (gradeVolume == null) return;
            bool on = drain > 0.001f || beatKick > 0f || punchKick > 0f;
            gradeVolume.weight = on ? 1f : 0f;
            neonVolume.weight = neon ? 1f : 0f;
            ultAdjust.saturation.value = neon ? 0f : -72f * drain;
            ultAdjust.postExposure.value = -0.7f * drain - 0.5f * hush;
            ultAdjust.contrast.value = 16f * drain + 45f * punchKick;
            ultVignette.intensity.value = Mathf.Clamp01(0.18f + 0.22f * drain + 0.12f * beatKick + 0.15f * hush + 0.25f * redKick);
            ultVignette.color.value = Color.Lerp(Color.black, new Color(0.7f, 0f, 0.03f), Mathf.Clamp01(redKick * 1.5f));
            ultVignette.smoothness.value = 0.45f;
            ultAberration.intensity.value = Mathf.Clamp01(0.3f * beatKick + punchKick);
#endif
        }

#if CF_URP
        void UltGradeBuild()
        {
            if (gradeHolder != null) return;
            gradeHolder = new GameObject("Ult grade (volume)");
            gradeHolder.transform.SetParent(root, false);
            gradeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            gradeProfile.hideFlags = HideFlags.HideAndDontSave;
            ultAdjust = gradeProfile.Add<ColorAdjustments>(true);
            ultVignette = gradeProfile.Add<Vignette>(true);
            ultAberration = gradeProfile.Add<ChromaticAberration>(true);
            gradeVolume = gradeHolder.AddComponent<Volume>();
            gradeVolume.isGlobal = true;
            gradeVolume.priority = 120f;
            gradeVolume.sharedProfile = gradeProfile;
            gradeVolume.weight = 0f;
            // The neon: every hue loses its colour but teal's (the caster's move) and red's (what it attacks), a little
            // stronger (the curve's 0.5 keeps a hue as it is, 0 greys it).
            neonProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            neonProfile.hideFlags = HideFlags.HideAndDontSave;
            var curves = neonProfile.Add<ColorCurves>(true);
            var keys = new[]
            {
                new Keyframe(0f, 0.62f), new Keyframe(0.035f, 0.62f), new Keyframe(0.085f, 0f), new Keyframe(0.385f, 0f),
                new Keyframe(0.425f, 0.62f), new Keyframe(0.525f, 0.62f), new Keyframe(0.565f, 0f), new Keyframe(0.915f, 0f),
                new Keyframe(0.965f, 0.62f),
            };
            curves.hueVsSat.Override(new TextureCurve(keys, 0.5f, true, new Vector2(0f, 1f)));
            var holder = new GameObject("Ult neon (volume)");
            holder.transform.SetParent(gradeHolder.transform, false);
            neonVolume = holder.AddComponent<Volume>();
            neonVolume.isGlobal = true;
            neonVolume.priority = 121f;
            neonVolume.sharedProfile = neonProfile;
            neonVolume.weight = 0f;
        }
#endif

        // ---------------------------------------------------------------- the caster's own camera

        /// <summary>On the caster's own screen only: closer, lower, narrower and a little tilted while it gathers, still in the
        /// silence, wide at the call and back, pulled up and back while the knight is in the air, kicked on the beats and at the
        /// landing. Its direction is never touched.</summary>
        void UltCameraMods(UltShow u, Camera cam, float dt)
        {
            if (cam == null || u.by == null || Viewer != u.by || u.Life(Clock) <= 0f) return;
            bool free = ViewOverride == null && game != null && game.labCamera != null && game.labCamera.freeMode;
            if (free) return;
            float motion = ChessFight.Game.GameSettings.ShakeScale;
            float a = Clock - u.t0, g = Smooth01(a / u.gather), k = u.Life(Clock);
            float closer = 1.3f * g, lower = 0.4f * g, fov = -7f * g, roll = 2.5f * g * motion, up = 0f, back = 0f;
            if (Clock >= u.Hush)
            {
                float h = Smooth01((Clock - u.Hush) / Mathf.Max(0.05f, u.Go - u.Hush));
                closer += 0.25f * h;
                fov -= 1.5f * h;
            }
            if (Clock >= u.Go)
            {
                float r = Smooth01((Clock - u.Go) / 0.45f);
                closer *= 1f - r;
                lower *= 1f - r;
                fov *= 1f - r;
                roll *= 1f - r;
                fov += 10f * motion * Mathf.Exp(-(Clock - u.Go) / 0.25f);
                if (u.hitAt < 0f && (u.Fork || u.finalAt >= 0f))
                {
                    float air = Smooth01((Clock - Mathf.Max(u.Go, u.finalAt)) / 0.4f);
                    fov += 9f * air;
                    up += 1.6f * air;
                    back += 2.4f * air;
                }
            }
            fov -= 2.2f * motion * beatKick;
            if (u.strikeAt >= 0f) fov -= 3f * motion * Mathf.Exp(-(Clock - u.strikeAt) / 0.12f);
            if (u.hitAt >= 0f)
            {
                float h = Clock - u.hitAt;
                fov += 8f * motion * Mathf.Exp(-h / 0.3f);
                roll += 3f * motion * Mathf.Exp(-h / 0.35f);
                float settle = 1f - Smooth01((h - 0.2f) / 0.8f);
                up *= settle;
                back *= settle;
            }
            var t = cam.transform;
            Vector3 head = HeadOf(u.by);
            closer = Mathf.Min(closer, Mathf.Max(0f, Vector3.Distance(t.position, head) - 1.6f));
            Vector3 pos = t.position + t.forward * (closer * k);
            if (back > 0f)
            {
                float room = back;
                if (Physics.SphereCast(pos, 0.25f, -t.forward, out var wall, back + 0.3f, ~0, QueryTriggerInteraction.Ignore)
                    && wall.collider.GetComponentInParent<RagdollPawn>() == null) room = Mathf.Max(0f, wall.distance - 0.3f);
                pos -= t.forward * (room * k);
            }
            pos += Vector3.up * ((up - lower) * k);
            if (Physics.Raycast(pos + Vector3.up * 0.6f, Vector3.down, out var floor, 1.3f, ~0, QueryTriggerInteraction.Ignore)
                && floor.collider.GetComponentInParent<RagdollPawn>() == null)
                pos.y = Mathf.Max(pos.y, floor.point.y + 0.55f);
            t.position = pos;
            if (Mathf.Abs(roll) > 1e-3f) t.rotation *= Quaternion.Euler(0f, 0f, roll * k);
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView + fov * k, 25f, 110f);
        }

        // ---------------------------------------------------------------- the face in the banner's window

        void UltPipStep(UltShow u)
        {
            pipShow = u;
            if (u == null || u.by == null)
            {
                if (pipCam != null && pipCam.enabled) pipCam.enabled = false;
                return;
            }
            if (pipCam == null)
            {
                pipTex = new RenderTexture(320, 216, 24) { name = "Ult face", hideFlags = HideFlags.HideAndDontSave };
                var go = new GameObject("Ult face camera");
                go.transform.SetParent(root, false);
                pipCam = go.AddComponent<Camera>();
                pipCam.targetTexture = pipTex;
                pipCam.clearFlags = CameraClearFlags.SolidColor;
                pipCam.backgroundColor = new Color(0.015f, 0.06f, 0.055f);
                pipCam.fieldOfView = 30f;
                pipCam.nearClipPlane = 0.05f;
                pipCam.farClipPlane = 80f;
                pipCam.depth = -20f;
                pipCam.allowHDR = true;
#if CF_URP
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.renderShadows = false;
#endif
            }
            pipCam.enabled = true;
            Vector3 head = HeadOf(u.by), fwd = Flat(u.by.Facing);
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            float push = Smooth01((Clock - u.t0) / Mathf.Max(0.5f, u.cast));
            pipCam.transform.position = head + fwd * Mathf.Lerp(2.6f, 1.9f, push) + side * 0.5f + Vector3.up * 0.05f;
            pipCam.transform.LookAt(head - Vector3.up * 0.25f);
        }

        // ---------------------------------------------------------------- the words and the banner

        void UltStyles()
        {
            if (ultTitle != null) return;
            var disp = ChessFight.Game.RuntimePanels.DisplayFont;
            var body = ChessFight.Game.RuntimePanels.KoreanFont;
            if (disp == null) disp = body != null ? body : font;
            if (body == null) body = font;
            ultTitle = new GUIStyle { font = disp, fontSize = 38, alignment = TextAnchor.MiddleLeft };
            ultSub = new GUIStyle { font = body, fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            ultBig = new GUIStyle { font = disp, fontSize = 80, alignment = TextAnchor.MiddleCenter };
            ultSmall = new GUIStyle { font = body, fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var symbols = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Arial Unicode MS", "DejaVu Sans" }, 64);
            ultGlyph = new GUIStyle { font = symbols, fontSize = 50, alignment = TextAnchor.MiddleCenter };
            foreach (var s in new[] { ultTitle, ultSub, ultBig, ultSmall, ultGlyph }) s.normal.textColor = Color.white;
            ultGradTeal = UltGradientTex(new Color(0.07f, 0.55f, 0.47f, 0.93f), new Color(0.04f, 0.3f, 0.35f, 0.93f));
            ultGradRed = UltGradientTex(new Color(0.67f, 0.11f, 0.13f, 0.93f), new Color(0.43f, 0.04f, 0.06f, 0.93f));
        }

        static Texture2D UltGradientTex(Color a, Color b)
        {
            var t = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < 64; i++) t.SetPixel(i, 0, Color.Lerp(a, b, i / 63f));
            t.Apply();
            return t;
        }

        static readonly Color UltInk = new Color(0.03f, 0.05f, 0.06f, 0.9f);

        static void Outlined(Rect r, string text, GUIStyle st, Color main, float width)
        {
            var keep = GUI.color;
            GUI.color = new Color(UltInk.r, UltInk.g, UltInk.b, UltInk.a * keep.a);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * width, r.y + Mathf.Sin(a) * width, r.width, r.height), text, st);
            }
            GUI.color = new Color(main.r, main.g, main.b, main.a * keep.a);
            GUI.Label(r, text, st);
            GUI.color = keep;
        }

        static string PieceName(PieceKind k) => k switch
        {
            PieceKind.Pawn => "폰", PieceKind.Knight => "나이트", PieceKind.Bishop => "비숍",
            PieceKind.Rook => "룩", PieceKind.Queen => "퀸", PieceKind.King => "킹", _ => "기물",
        };

        static string PieceGlyph(PieceKind k) => k switch
        {
            PieceKind.Pawn => "♟", PieceKind.Knight => "♞", PieceKind.Bishop => "♝",
            PieceKind.Rook => "♜", PieceKind.Queen => "♛", _ => "♚",
        };

        void OnGUI()
        {
            if (!effects || ults.Count == 0 || Event.current.type != EventType.Repaint) return;
            UltStyles();
            float ui = Screen.height / 720f, vw = Screen.width / ui;
            var keepMatrix = GUI.matrix;
            var keepColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(ui, ui, 1f));
            // The screen's flashes: white on the beats and the landing, red on the named one's beats.
            if (whiteKick > 0.001f)
            {
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(whiteKick));
                GUI.DrawTexture(new Rect(0f, 0f, vw, 720f), Texture2D.whiteTexture);
            }
            if (redKick > 0.001f)
            {
                GUI.color = new Color(1f, 0.05f, 0.08f, 0.16f * redKick);
                GUI.DrawTexture(new Rect(0f, 0f, vw, 720f), Texture2D.whiteTexture);
            }
            GUI.color = keepColor;
            float y = 74f;
            foreach (var u in ults)
            {
                if (u.by == null) continue;
                y = UltBanner(u, vw, y) + 8f;
                UltWords(u, vw);
            }
            UltWarning(vw);
            GUI.matrix = keepMatrix;
            GUI.color = keepColor;
        }

        /// <summary>The banner under the top bar; returns its bottom.</summary>
        float UltBanner(UltShow u, float vw, float y)
        {
            float a = Clock - u.t0, k = Mathf.Clamp01(a / 0.12f) * u.Life(Clock);
            if (u.hitAt >= 0f) k *= 1f - Smooth01((Clock - u.hitAt - 1.4f) / 0.4f);
            if (k <= 0.001f) return y;
            var me = Viewer;
            bool ally = me == null || me.Team == u.by.Team || me == u.by;
            string title = $"{Teams.Name(u.by.Team)}팀 {PieceName(u.by.Piece)}   {u.title}!!";   // (the display font has no middle dot)
            string sub = u.cut ? "끊겼어요 — 팀 게이지 절반이 남아요"
                : ally ? (u.Fork ? $"{u.by.DisplayName} · Nf7+ · 착지 둘레 {(u.by.PlanNote > 0 ? u.by.PlanNote : 14)} m 적 전원이 하늘로, 20 m 뒤로"
                                 : u.Tour ? $"{u.by.DisplayName} · 머리에서 머리로 · 밟힌 적은 3초 납작 · 적 선두 앞으로" : u.by.DisplayName)
                : "빨간 칸에서 벗어나요!";
            bool face = pipShow == u && pipTex != null && pipCam != null && pipCam.enabled;
            float faceW = 150f;
            var tw = ultTitle.CalcSize(new GUIContent(title)).x;
            var sw = ultSub.CalcSize(new GUIContent(sub)).x;
            float w = Mathf.Max(tw, sw) + 120f + faceW, h = 108f;
            float slide = (1f - Smooth01(a / 0.3f)) * -60f, x = (vw - w) * 0.5f + slide;
            GUI.color = new Color(1f, 1f, 1f, k);
            GUI.DrawTexture(new Rect(x, y, w, h), ally ? ultGradTeal : ultGradRed);
            GUI.color = new Color(1f, 1f, 1f, 0.8f * k);
            GUI.DrawTexture(new Rect(x, y, w, 2.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - 2.5f, w, 2.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, 2.5f, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + w - 2.5f, y, 2.5f, h), Texture2D.whiteTexture);
            // The face, live (it shuts like a shutter at the call).
            float open = face ? Smooth01((a - 0.06f) / 0.26f) * (1f - Smooth01((Clock - u.Go - 0.4f) / 0.3f)) : 0f;
            var win = new Rect(x + 8f, y + 8f + (h - 16f) * (1f - open) * 0.5f, faceW - 8f, (h - 16f) * open);
            if (open > 0.01f)
            {
                GUI.color = new Color(1f, 1f, 1f, k);
                GUI.DrawTexture(win, pipTex, ScaleMode.ScaleAndCrop, false);
                GUI.color = new Color(1f, 1f, 1f, 0.9f * k);
                GUI.DrawTexture(new Rect(win.x - 1f, win.y - 1f, win.width + 2f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(win.x - 1f, win.yMax - 1f, win.width + 2f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(win.x - 1f, win.y - 1f, 2f, win.height + 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(win.xMax - 1f, win.y - 1f, 2f, win.height + 2f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, k);
                var tag = new GUIStyle(ultSub) { fontSize = 12, alignment = TextAnchor.LowerLeft };
                Outlined(new Rect(win.x + 5f, win.y, win.width, win.height - 3f), "시전자 실시간", tag, Color.white, 1f);
            }
            GUI.color = new Color(1f, 1f, 1f, k);
            Outlined(new Rect(x + faceW, y, 90f, h), PieceGlyph(u.by.Piece), ultGlyph, Color.white, 1.5f);
            Outlined(new Rect(x + faceW + 88f, y + 10f, tw + 20f, 52f), title, ultTitle, Color.white, 2.5f);
            Outlined(new Rect(x + faceW + 90f, y + 60f, sw + 20f, 34f), sub, ultSub, ally ? new Color(0.9f, 1f, 0.97f) : new Color(1f, 0.9f, 0.9f), 1.2f);
            GUI.color = Color.white;
            return y + h;
        }

        /// <summary>The words everyone hears, as big words in the middle: the whisper, the call with the move typed in the
        /// air, the judgement.</summary>
        void UltWords(UltShow u, float vw)
        {
            var center = new Vector2(vw * 0.5f, 430f);
            string piece = PieceName(u.by.Piece);
            if (u.endAt < 0f && Clock < u.Hush)
            {
                float a = Clock - u.t0 - 0.15f;
                if (a > 0f) Word($"{piece}...", center, 50, new Color(0.78f, 1f, 0.95f), a, u.Hush - u.t0 - 0.15f);
            }
            if (u.goAt >= 0f && Clock < u.goAt + 0.85f) Word($"{u.title}!!", center, 84, new Color(0.25f, 0.88f, 0.75f), Clock - u.goAt, 0.85f);
            if (u.goAt >= 0f && Clock < u.goAt + 1.5f)
            {
                // The move, typed letter by letter on the beat, tilted, held, gone.
                float a = Clock - u.goAt;
                int n = Mathf.Clamp(Mathf.FloorToInt(a / 0.055f) + 1, 0, u.nota.Length);
                if (n > u.typed)
                {
                    u.typed = n;
                    UltPlay(sfxType, 0.5f, 0f, 1f + 0.03f * n);
                }
                float k = 1f - Smooth01((a - 1.15f) / 0.35f);
                var keep = GUI.matrix;
                var at = new Vector2(vw * 0.5f, 250f);
                GUIUtility.RotateAroundPivot(-6f, at * (Screen.height / 720f));
                var st = new GUIStyle(ultBig) { fontSize = 100 };
                GUI.color = new Color(1f, 1f, 1f, k);
                Outlined(new Rect(at.x - 500f, at.y - 70f, 1000f, 140f), u.nota.Substring(0, n), st, new Color(0.25f, 0.88f, 0.75f), 5f);
                GUI.matrix = keep;
                GUI.color = Color.white;
            }
            if (u.hitAt >= 0f && Clock > u.hitAt + 0.12f && Clock < u.hitAt + 1.4f)
                Word("탁월한 수!!", new Vector2(vw * 0.5f, 410f), 74, new Color(0.25f, 0.88f, 0.75f), Clock - u.hitAt - 0.12f, 1.28f);
        }

        void Word(string text, Vector2 center, int size, Color color, float age, float life)
        {
            float k = Mathf.Clamp01(age / 0.06f) * (1f - Smooth01((age - (life - 0.25f)) / 0.25f)), pop = 1f + 0.25f * (1f - Smooth01(age / 0.15f));
            if (k <= 0.001f) return;
            var keep = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), center * (Screen.height / 720f));
            var st = new GUIStyle(ultBig) { fontSize = size };
            GUI.color = new Color(1f, 1f, 1f, k);
            Outlined(new Rect(center.x - 600f, center.y - size, 1200f, size * 2f), text, st, color, Mathf.Max(3f, size * 0.07f));
            GUI.matrix = keep;
            GUI.color = Color.white;
        }

        /// <summary>The piece played here named by an enemy's ultimate: a red warning near the bottom while it can still step away.</summary>
        void UltWarning(float vw)
        {
            var me = Viewer;
            if (me == null) return;
            foreach (var u in ults)
            {
                if (u.hitAt >= 0f || u.endAt >= 0f || !u.marked.Contains(me) || u.struckSet.Contains(me)) continue;
                float blink = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Clock * 9f));
                var st = new GUIStyle(ultBig) { fontSize = 34 };
                GUI.color = new Color(1f, 1f, 1f, blink);
                Outlined(new Rect(vw * 0.5f - 400f, 560f, 800f, 60f), u.Tour ? "나이트가 머리 위로 온다! 비켜요!" : "빨간 칸에서 벗어나요!", st, new Color(1f, 0.3f, 0.3f), 3f);
                GUI.color = Color.white;
                return;
            }
        }

        // ---------------------------------------------------------------- the sound

        const int Rate = 44100;

        static AudioClip Synth(string name, float seconds, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        static float Noise(float t)
        {
            uint h = (uint)(t * Rate) * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xffff) / 32767.5f - 1f;
        }

        void UltAudioBuild()
        {
            if (ultAudio != null) return;
            ultAudio = root.gameObject.AddComponent<AudioSource>();
            ultAudio.playOnAwake = false;
            ultAudio.spatialBlend = 0f;
            ultRise = root.gameObject.AddComponent<AudioSource>();
            ultRise.playOnAwake = false;
            ultRise.spatialBlend = 0f;
            const float Tau = Mathf.PI * 2f;
            // The key's "둠": a deep thud falling in pitch.
            sfxBoom = Synth("Ult boom", 1.1f, t => 0.95f * Mathf.Sin(Tau * (52f * t - 6f * t * t)) * Mathf.Exp(-t * 3f) + 0.35f * Noise(t) * Mathf.Exp(-t * 35f));
            // A heartbeat: lub, then a softer dub.
            sfxHeart = Synth("Ult heart", 0.45f, t =>
            {
                float lub = Mathf.Sin(Tau * 62f * t) * Mathf.Exp(-t * 26f) * Mathf.Clamp01(t * 400f);
                float d = t - 0.17f, dub = d > 0f ? 0.7f * Mathf.Sin(Tau * 54f * d) * Mathf.Exp(-d * 30f) * Mathf.Clamp01(d * 400f) : 0f;
                return 1.3f * (lub + dub);
            });
            // A name: a hard little tick.
            sfxTick = Synth("Ult tick", 0.07f, t => 0.5f * (Mathf.Sin(Tau * 1500f * t) + 0.6f * Mathf.Sin(Tau * 2240f * t)) * Mathf.Exp(-t * 75f));
            // The gathering: a shimmer rising to the silence.
            sfxRise = Synth("Ult rise", 2.2f, t =>
            {
                float g = Mathf.Pow(Mathf.Clamp01(t / 2.1f), 2.2f);
                return g * (0.22f * Noise(t) * (0.5f + 0.5f * Mathf.Sin(Tau * 9f * t)) + 0.18f * Mathf.Sin(Tau * (180f * t + 160f * t * t)));
            });
            // The call: the drop, a sub falling away under a rush of air.
            sfxDrop = Synth("Ult drop", 1.6f, t =>
            {
                float sub = Mathf.Sin(Tau * (46f * t - 5f * t * t)) * Mathf.Exp(-t * 1.6f) * Mathf.Clamp01(t * 200f);
                float air = Noise(t) * Mathf.Exp(-Mathf.Pow((t - 0.12f) / 0.16f, 2f)) * 0.5f;
                return 0.95f * sub + air;
            });
            // The judgement: two bright notes.
            sfxBling = Synth("Ult bling", 1.2f, t =>
            {
                float a = Chime(1318.5f, t), b = t > 0.07f ? Chime(1975.5f, t - 0.07f) : 0f;
                return 0.45f * (a + b);
            });
            // A letter typed in the air.
            sfxType = Synth("Ult type", 0.05f, t => 0.6f * Noise(t) * Mathf.Exp(-t * 160f));
            // Under the landing: the low end of an explosion.
            sfxImpactLow = Synth("Ult impact", 1.4f, t => 0.9f * Mathf.Sin(Tau * (38f * t - 4f * t * t)) * Mathf.Exp(-t * 2.4f) + 0.45f * Noise(t) * Mathf.Exp(-t * 14f));
            // The tour's heads: a chime climbing the scale with each.
            int[] steps = { 0, 2, 4, 7, 9, 12, 14, 16 };
            for (int i = 0; i < sfxChime.Length; i++)
            {
                float f = 523.25f * Mathf.Pow(2f, steps[i] / 12f);
                sfxChime[i] = Synth($"Ult chime {i}", 0.9f, t => 0.5f * Chime(f, t));
            }
            clipLeap = Resources.Load<AudioClip>("PawnRushSkillSfx/KnightLeap");
            clipStomp = Resources.Load<AudioClip>("PawnRushSkillSfx/KnightStomp");
            clipLand = Resources.Load<AudioClip>("PawnRushSkillSfx/KnightLand");
            clipWall = Resources.Load<AudioClip>("PawnRushSkillSfx/RookWall");
        }

        static float Chime(float f, float t)
        {
            const float Tau = Mathf.PI * 2f;
            return (Mathf.Sin(Tau * f * t) + 0.35f * Mathf.Sin(Tau * 2f * f * t) + 0.15f * Mathf.Sin(Tau * 3.01f * f * t)) * Mathf.Exp(-t * 4.5f) * Mathf.Clamp01(t * 300f);
        }

        void UltPlay(AudioClip clip, float level, float delay = 0f, float pitch = 1f)
        {
            if (clip == null || ultAudio == null) return;
            float v = Mathf.Clamp01(level * ChessFight.Game.GameSettings.Effects);
            UltSoundPlayed?.Invoke(clip, v, pitch, delay);
            if (delay <= 0f && Mathf.Abs(pitch - 1f) < 0.01f)
            {
                ultAudio.PlayOneShot(clip, v);
                return;
            }
            var src = root.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.clip = clip;
            src.volume = v;
            src.pitch = pitch;
            src.PlayDelayed(Mathf.Max(0f, delay));
            Destroy(src, delay + clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        void UltSound(UltSfx kind, Vector3 at, int n = 0)
        {
            UltAudioBuild();
            switch (kind)
            {
                case UltSfx.Heart: UltPlay(sfxHeart, 0.9f); break;
                case UltSfx.Tick: UltPlay(sfxTick, 0.6f, 0f, 1f + 0.05f * Mathf.Clamp(n - 1, 0, 8)); break;
                case UltSfx.Leap: UltPlay(clipLeap, 0.9f); break;
                case UltSfx.Stomp:
                    UltPlay(clipStomp, 0.9f);
                    UltPlay(sfxChime[Mathf.Clamp(n - 1, 0, sfxChime.Length - 1)], 0.8f);
                    break;
                case UltSfx.Impact:
                    UltPlay(clipLand, 1f);
                    UltPlay(clipWall, 0.8f, 0f, 0.8f);
                    UltPlay(sfxImpactLow, 1f);
                    break;
            }
        }
    }
}
