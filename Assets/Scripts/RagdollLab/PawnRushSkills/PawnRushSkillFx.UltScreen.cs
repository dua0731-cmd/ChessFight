using System;
using System.Collections.Generic;
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
    /// The knight's ultimates on the screen and in the ears, every PC its own (JY-skill-lab, 2026-11-22; the world's part is
    /// PawnRushSkillFx.Ult.cs). Nobody's camera is turned and nobody's keys are taken.
    ///
    ///   night        the scene's lights (sun, ambient, reflections, fog) go down while the show plays, and a grade adds
    ///                contrast, a little less colour and a vignette; colour splits on the beat and at the landing
    ///   the banner   under the top bar on every screen: teal for the caster's side, red for the other, the caster's face
    ///                live in its window for the first seconds
    ///   the words    for the caster: what its keys do now ("이동 키 = 착지 칸 · 내려올 때 Q"); for an enemy on a fork
    ///                square or under the tour's ring, a red warning; for all, the judgement at the landing (탁월한 수 !! ..
    ///                블런더 ??) in chess.com's colours
    ///   the sound    made in code: the take-off's boom, a crackle while it flies, the beat's chime (or a dull tick off
    ///                it), a chime climbing with each head, the landing and a judgement's chord; with the knight's own clips
    ///   the camera   the caster's own only: a kick of field of view at the take-off, the beat and the landing, a little
    ///                further back and higher while the fork flies (so its squares are seen); its direction stays the player's
    /// </summary>
    public partial class PawnRushSkillFx
    {
        enum UltSfx { Boom, Leap, Perfect, Off, Stomp, Impact }

        /// <summary>Every sound the show starts (clip, volume, pitch, delay s), for a film's sound track.</summary>
        public static event Action<AudioClip, float, float, float> UltSoundPlayed;

#if CF_URP
        GameObject gradeHolder;
        Volume gradeVolume;
        VolumeProfile gradeProfile;
        ColorAdjustments ultAdjust;
        Volume neonVolume;
        VolumeProfile neonProfile;
        Vignette ultVignette;
        ChromaticAberration ultAberration;
#endif
        float redKick, musicLevel = 1f;
        Camera pipCam;
        RenderTexture pipTex;
        UltShow pipShow;
        AudioSource ultAudio, ultLoop;
        AudioClip sfxBoom, sfxCrackle, sfxPerfect, sfxOff, sfxImpactLow, sfxFire, sfxRumble, sfxClack, sfxWhistle, sfxFanfare;
        readonly AudioClip[] sfxChime = new AudioClip[8];
        readonly AudioClip[] sfxGrade = new AudioClip[5];
        AudioClip clipLeap, clipStomp, clipLand, clipWall;
        GUIStyle ultTitle, ultSub, ultBig, ultGlyph;
        Texture2D ultGradTeal, ultGradRed;

        // ---------------------------------------------------------------- the moments

        void UltScreenStart(UltShow u)
        {
            u.title = u.Fork ? "패밀리 포크" : u.Tour ? "나이트 투어" : u.Driven ? DriveTitle(u.skill) : PawnRushSkillBook.Name(u.skill);
            u.nota = u.Fork ? "Nf7+!!" : u.Tour ? "Nf5-d6-e8!!" : u.Driven ? DriveNota(u.skill) : "!!";
            UltAudioBuild();
            if (ultLoop != null && sfxCrackle != null)
            {
                ultLoop.Stop();
                ultLoop.clip = sfxCrackle;
                ultLoop.loop = true;
                ultLoop.volume = 0.35f * ChessFight.Game.GameSettings.Effects;
                ultLoop.Play();
            }
        }

        void UltScreenStrike(UltShow u, SkillFxEvent e)
        {
            if (Viewer == e.target) redKick = 1f;   // the struck piece's own screen: a red vignette for 0.35 s
        }

        void UltScreenHit(UltShow u, SkillFxEvent e)
        {
            if (ultLoop != null) ultLoop.Stop();
        }

        void UltScreenDestroy()
        {
            UltWorldDim(0f);
#if CF_URP
            if (gradeHolder != null) Destroy(gradeHolder);
            if (gradeProfile != null) Destroy(gradeProfile);
            if (neonProfile != null) Destroy(neonProfile);
#endif
            if (pipCam != null) Destroy(pipCam.gameObject);
            if (pipTex != null) { pipTex.Release(); Destroy(pipTex); }
            foreach (var c in new[] { sfxBoom, sfxCrackle, sfxPerfect, sfxOff, sfxImpactLow, sfxFire, sfxRumble, sfxClack, sfxWhistle, sfxFanfare }) if (c != null) Destroy(c);
            foreach (var c in sfxChime) if (c != null) Destroy(c);
            foreach (var c in sfxGrade) if (c != null) Destroy(c);
            foreach (var t in new[] { ultGradTeal, ultGradRed }) if (t != null) Destroy(t);
            ChessFight.Game.MusicDuck.Level = 1f;
        }

        // ---------------------------------------------------------------- every frame

        float ultAudioCheck;

        bool ultSoundsMade;

        /// <summary>No show: what the shows set goes back. The sounds are made here, before the first cast (making them at
        /// the key would stall that frame), once a piece in the game has a team ultimate.</summary>
        void UltScreenIdle(float dt, Camera cam)
        {
            if (!ultSoundsMade && Clock >= ultAudioCheck)
            {
                ultAudioCheck = Clock + 1f;
                foreach (var p in RagdollPawn.All)
                    if (p != null && UltHolder(p))
                    {
                        UltAudioBuild();
                        UltSoundsBuild();   // the presentation's new clips (PawnRushSkillFx.Sounds.cs)
                        ultSoundsMade = true;
                        break;
                    }
            }
            redKick = 0f;
            musicLevel = 1f;
            UltWorldDim(0f);
#if CF_URP
            if (gradeVolume != null && gradeVolume.weight > 0f) gradeVolume.weight = 0f;
            if (neonVolume != null && neonVolume.weight > 0f) neonVolume.weight = 0f;
#endif
            if (pipCam != null && pipCam.enabled) pipCam.enabled = false;
            if (ultLoop != null && ultLoop.isPlaying) ultLoop.Stop();
            if (ChessFight.Game.MusicDuck.Level != 1f) ChessFight.Game.MusicDuck.Level = 1f;
        }

        /// <summary>Every frame of a show: the scene's light down by <paramref name="kv"/> (the strongest show's world on
        /// this screen, times how dark it makes this viewer's), the music down, the caster's camera, the grade.</summary>
        void UltScreenStep(float dt, Camera cam, float kv)
        {
            redKick = Mathf.Max(0f, redKick - realDt / 0.35f);
            UltWorldDim(kv);
            UltShow face = null;
            bool caster = false;
            foreach (var u in ults)
            {
                if (u.Playing && Clock - u.t0 < 1.6f && u.by != null) face = u;
                if (u.by != null && u.by == Viewer) caster = true;
                UltCameraMods(u, cam);
            }
            // The music: 0.35 while a show is on, 0.15 while the caster's own time is held (eased over 0.08 s real).
            float duck = Mathf.Lerp(1f, 0.35f, kv);
            if (UltSlowNow < 0.5f) duck = Mathf.Min(duck, 0.15f);
            musicLevel = Mathf.MoveTowards(musicLevel, duck, realDt / 0.08f);
            ChessFight.Game.MusicDuck.Level = musicLevel;
            if (ultLoop != null && ultLoop.isPlaying) ultLoop.volume = 0.35f * ChessFight.Game.GameSettings.Effects * kv;
            UltPipStep(face);
#if CF_URP
            UltGradeBuild();
            if (gradeVolume == null) return;
            bool on = kv > 0.001f || redKick > 0f;
            // Taken from what the scene's own volumes give at the moment it starts (this one still off), so the grade is
            // added to the scene's look instead of replacing it.
            if (on && gradeVolume.weight <= 0f) UltGradeBase();
            gradeVolume.weight = on ? 1f : 0f;
            if (!on)
            {
                neonVolume.weight = 0f;
                return;
            }
            // The grey world: a little darker (the darkness is the lights', so the neon is not pushed into the bloom) and
            // harder, a cool tint, the colour drained - past half, the neon curve keeps teal and red only. No washes: the
            // hard moments are the impact frames' (PawnRushSkillFx.Impact.cs).
            neonVolume.weight = kv > 0.5f ? 1f : 0f;
            ultAdjust.postExposure.value = baseExposure - 0.10f * kv;
            ultAdjust.contrast.value = Mathf.Clamp(baseContrast + 18f * kv, -100f, 100f);
            ultAdjust.colorFilter.value = baseColorFilter * Color.Lerp(Color.white, new Color(0.95f, 0.98f, 1f), kv);
            ultAdjust.saturation.value = Mathf.Clamp(baseSaturation - 60f * kv * (kv > 0.5f ? 0f : 1f), -100f, 100f);
            ultVignette.intensity.value = Mathf.Clamp01(Mathf.Max(baseVignette, (caster ? 0.30f : 0.20f) * kv) + 0.30f * redKick);
            ultVignette.smoothness.value = Mathf.Lerp(baseVignetteSmooth, 0.4f, kv);
            ultVignette.color.value = Color.Lerp(baseVignetteColor, new Color(0.7f, 0f, 0.03f), Mathf.Clamp01(redKick * 1.5f));
            ultAberration.intensity.value = baseAberration;
#endif
        }

#if CF_URP
        float baseExposure, baseContrast, baseSaturation, baseVignette, baseVignetteSmooth = 0.2f, baseAberration;
        Color baseVignetteColor = Color.black, baseColorFilter = Color.white;

        void UltGradeBuild()
        {
            if (gradeHolder != null) return;
            gradeHolder = new GameObject("Ult grade (volume)");
            gradeHolder.transform.SetParent(root, false);
            gradeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            gradeProfile.hideFlags = HideFlags.HideAndDontSave;
            // Only what the night changes is overridden; the scene keeps its own colour filter, hue, vignette shape.
            ultAdjust = gradeProfile.Add<ColorAdjustments>(false);
            ultAdjust.postExposure.overrideState = ultAdjust.contrast.overrideState = ultAdjust.saturation.overrideState = true;
            ultAdjust.colorFilter.overrideState = true;
            ultVignette = gradeProfile.Add<Vignette>(false);
            ultVignette.intensity.overrideState = ultVignette.color.overrideState = ultVignette.smoothness.overrideState = true;
            ultAberration = gradeProfile.Add<ChromaticAberration>(false);
            ultAberration.intensity.overrideState = true;
            gradeVolume = gradeHolder.AddComponent<Volume>();
            gradeVolume.isGlobal = true;
            gradeVolume.priority = 120f;
            gradeVolume.sharedProfile = gradeProfile;
            gradeVolume.weight = 0f;
            // The neon: every hue loses its colour but teal's (the caster's move) and red's (what it attacks), a little
            // stronger (the curve's 0.5 keeps a hue as it is, 0 greys it). The red band is narrow, so orange roofs go grey.
            neonProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            neonProfile.hideFlags = HideFlags.HideAndDontSave;
            var curves = neonProfile.Add<ColorCurves>(true);
            var keys = new[]
            {
                new Keyframe(0f, 0.62f), new Keyframe(0.015f, 0.62f), new Keyframe(0.05f, 0f), new Keyframe(0.385f, 0f),
                new Keyframe(0.425f, 0.62f), new Keyframe(0.525f, 0.62f), new Keyframe(0.565f, 0f), new Keyframe(0.93f, 0f),
                new Keyframe(0.955f, 0.62f),
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

        void UltGradeBase()
        {
            var stack = VolumeManager.instance.stack;
            var adjust = stack?.GetComponent<ColorAdjustments>();
            var vignette = stack?.GetComponent<Vignette>();
            var aberration = stack?.GetComponent<ChromaticAberration>();
            baseExposure = adjust != null ? adjust.postExposure.value : 0f;
            baseContrast = adjust != null ? adjust.contrast.value : 0f;
            baseSaturation = adjust != null ? adjust.saturation.value : 0f;
            baseVignette = vignette != null ? vignette.intensity.value : 0f;
            baseVignetteColor = vignette != null ? vignette.color.value : Color.black;
            baseVignetteSmooth = vignette != null ? vignette.smoothness.value : 0.2f;
            baseColorFilter = adjust != null ? adjust.colorFilter.value : Color.white;
            baseAberration = aberration != null ? aberration.intensity.value : 0f;
        }
#endif

        // ---------------------------------------------------------------- the world dimmed: the scene's lights down

        // What the scene had before the first show dimmed it (put back exactly when the last one is over).
        readonly List<(Light light, float intensity, bool sun)> dimLights = new List<(Light, float, bool)>();
        bool dimOn, dimFogAdded;
        float dimAmbient, dimReflection, dimFogDensity;
        FogMode dimFogMode;
        Color dimAmbientLight, dimSky, dimEquator, dimGround, dimFog;
        // The ambient probe itself as well: a change of the ambient colours at run time does not always reach it.
        UnityEngine.Rendering.SphericalHarmonicsL2 dimProbe;

        /// <summary>The scene's own light down by <paramref name="kv"/> (11-22, the user: "맵 전체 채도 낮추기 + 시전자랑
        /// 공격받는 범위 등에만 강한 조명"): the sun to 38 %, every other scene light (not the effects' own) to 30 %, ambient and
        /// reflections to 45 %, the fog toward a dark grey (a scene without fog gets a thin one while it lasts). About 38 % of
        /// the day's light at full on the caster's screen, never below 35 %; the light that is left falls where the stage
        /// lights put it. Put back exactly at 0.</summary>
        void UltWorldDim(float kv)
        {
            if (kv <= 0.001f)
            {
                if (!dimOn) return;
                foreach (var (light, intensity, _) in dimLights) if (light != null) light.intensity = intensity;
                RenderSettings.ambientIntensity = dimAmbient;
                RenderSettings.reflectionIntensity = dimReflection;
                RenderSettings.ambientLight = dimAmbientLight;
                RenderSettings.ambientSkyColor = dimSky;
                RenderSettings.ambientEquatorColor = dimEquator;
                RenderSettings.ambientGroundColor = dimGround;
                RenderSettings.fogColor = dimFog;
                if (dimFogAdded)
                {
                    RenderSettings.fog = false;
                    RenderSettings.fogMode = dimFogMode;
                    RenderSettings.fogDensity = dimFogDensity;
                }
                RenderSettings.ambientProbe = dimProbe;
                dimLights.Clear();
                dimOn = dimFogAdded = false;
                return;
            }
            if (!dimOn)
            {
                dimOn = true;
                dimLights.Clear();
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l != null && l.enabled && (root == null || !l.transform.IsChildOf(root))) dimLights.Add((l, l.intensity, l.type == LightType.Directional));
                dimAmbient = RenderSettings.ambientIntensity;
                dimReflection = RenderSettings.reflectionIntensity;
                dimAmbientLight = RenderSettings.ambientLight;
                dimSky = RenderSettings.ambientSkyColor;
                dimEquator = RenderSettings.ambientEquatorColor;
                dimGround = RenderSettings.ambientGroundColor;
                dimFog = RenderSettings.fogColor;
                dimProbe = RenderSettings.ambientProbe;
                dimFogMode = RenderSettings.fogMode;
                dimFogDensity = RenderSettings.fogDensity;
                dimFogAdded = !RenderSettings.fog;
                if (dimFogAdded)
                {
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                }
            }
            foreach (var (light, intensity, sun) in dimLights) if (light != null) light.intensity = intensity * (1f - (sun ? 0.62f : 0.70f) * kv);
            float keep = 1f - 0.55f * kv;
            RenderSettings.ambientIntensity = dimAmbient * keep;
            RenderSettings.reflectionIntensity = dimReflection * keep;
            RenderSettings.ambientLight = dimAmbientLight * keep;
            RenderSettings.ambientSkyColor = dimSky * keep;
            RenderSettings.ambientEquatorColor = dimEquator * keep;
            RenderSettings.ambientGroundColor = dimGround * keep;
            RenderSettings.fogColor = Color.Lerp(dimFog, new Color(0.07f, 0.075f, 0.085f), 0.6f * kv);
            if (dimFogAdded) RenderSettings.fogDensity = 0.006f * kv;
            RenderSettings.ambientProbe = dimProbe * keep;
        }

        // ---------------------------------------------------------------- the caster's own camera

        /// <summary>On the caster's own screen only: the slow motion's dolly zoom, roll and kicks (the key, a signature, the
        /// judgement: PawnRushSkillFx.Impact.cs), a little further back and higher while it flies or rolls, and kicks at the
        /// beat and the landing; within limits (field of view ±12°, back 2.5 m, up 1.5 m, roll 6°; a giant's pull-back on
        /// top). Its direction is never touched: the roll turns it about its own line of sight.</summary>
        void UltCameraMods(UltShow u, Camera cam)
        {
            if (cam == null || u.by == null || Viewer != u.by || u.Life(Clock) <= 0f) return;
            bool free = ViewOverride == null && game != null && game.labCamera != null && game.labCamera.freeMode;
            if (free) return;
            float motion = ChessFight.Game.GameSettings.ShakeScale, fov = 0f, back = 0f, up = 0f, roll = 0f;
            UltSlowCamera(u, ref fov, ref back, ref up, ref roll);
            // The nightmare's leaps: a little further back and higher while it flies (eased in and out), to see where it lands.
            bool wide = u.Playing && (u.skill == PawnRushSkillIds.Nightmare && (u.by.PlanNote & RagdollPawn.NmFlying) != 0 || u.skill == PawnRushSkillIds.RookRoll || u.skill == PawnRushSkillIds.QueenDescent);
            u.camK = Mathf.MoveTowards(u.camK, wide ? 1f : 0f, realDt / 0.35f);
            float wk = Smooth01(u.camK);
            back += 2.2f * wk;
            up += 1f * wk;
            fov += 5f * wk;
            if (u.Fork && u.Playing)
            {
                float air = Smooth01((Clock - u.t0) / 0.4f);
                back += 2.6f * air;
                up += 1.2f * air;
                fov += 6f * air;
            }
            if (u.timingAt >= 0f) fov -= (u.timingPerfect ? 4f : 1.5f) * motion * Mathf.Exp(-(Clock - u.timingAt) / 0.15f);
            if (u.hitAt >= 0f)
            {
                float h = Clock - u.hitAt;
                // The judgement's kick when its time is let go (a !!'s hold is 0.52 s real).
                float release = RealClock - u.hitReal - (u.grade == RagdollPawn.GradeBrilliant ? 0.52f : 0f);
                if (release >= 0f) fov += 6f * (ChessFight.Game.GameSettings.ReduceShake ? 0.5f : 1f) * Mathf.Exp(-release / 0.3f);
                float settle = 1f - Smooth01(h / 0.6f);
                back *= settle;
                up *= settle;
                if (u.Fork && h < 0.6f) { back = 2.6f * settle; up = 1.2f * settle; fov += 6f * settle; }
            }
            fov = Mathf.Clamp(fov, -12f, 12f);
            back = Mathf.Clamp(back, 0f, 2.5f);
            up = Mathf.Clamp(up, 0f, 1.5f);
            roll = Mathf.Clamp(roll, -6f, 6f);
            // A giant (the pawn's queen): back and up with its size, so the camera is not inside it (past the limits).
            float giant = GiantScaleOf(u.by);
            if (giant > 1.02f)
            {
                back += (giant - 1f) * 1.2f;
                up += (giant - 1f) * 0.55f;
                fov += Mathf.Min(8f, (giant - 1f) * 2f);
            }
            var t = cam.transform;
            Vector3 pos = t.position;
            if (back > 0f)
            {
                float room = back;
                if (Physics.SphereCast(pos, 0.25f, -t.forward, out var wall, back + 0.3f, ~0, QueryTriggerInteraction.Ignore)
                    && wall.collider.GetComponentInParent<RagdollPawn>() == null) room = Mathf.Max(0f, wall.distance - 0.3f);
                pos -= t.forward * room;
            }
            pos += Vector3.up * up;
            t.position = pos;
            // The roll, about the camera's own line of sight (a camera that does not set its turn every frame keeps no old roll).
            if (cam == ultRollCam && Quaternion.Angle(t.rotation, ultRollWritten) < 0.01f) t.rotation *= Quaternion.Euler(0f, 0f, -ultRollAdded);
            if (Mathf.Abs(roll) > 0.001f) t.rotation *= Quaternion.Euler(0f, 0f, roll);
            ultRollCam = cam;
            ultRollWritten = t.rotation;
            ultRollAdded = roll;
            // The cameras set their field of view every frame (LabCamera, OrbitCamera), so this is added on top; a frame one
            // skips must not pile one kick on the last.
            float baseFov = cam.fieldOfView;
            if (cam == ultFovCam && Mathf.Abs(baseFov - ultFovWritten) < 1e-3f) baseFov -= ultFovAdded;
            cam.fieldOfView = Mathf.Clamp(baseFov + fov, 25f, 110f);
            ultFovCam = cam;
            ultFovWritten = cam.fieldOfView;
            ultFovAdded = ultFovWritten - baseFov;
        }

        Camera ultFovCam, ultRollCam;
        float ultFovWritten = -1f, ultFovAdded, ultRollAdded;
        Quaternion ultRollWritten = Quaternion.identity;

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
                pipCam.backgroundColor = new Color(0.01f, 0.015f, 0.03f);
                pipCam.fieldOfView = 32f;
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
            pipCam.transform.position = head + fwd * 2.1f + side * 0.6f + Vector3.up * 0.15f;
            pipCam.transform.LookAt(head - Vector3.up * 0.2f);
        }

        // ---------------------------------------------------------------- the words and the banner

        void UltStyles()
        {
            if (ultTitle != null) return;
            var disp = ChessFight.Game.RuntimePanels.DisplayFont;
            var body = ChessFight.Game.RuntimePanels.KoreanFont;
            if (body == null) body = font;
            if (disp == null) disp = body;
            ultTitle = new GUIStyle { font = disp, fontSize = 36, alignment = TextAnchor.MiddleLeft };
            ultSub = new GUIStyle { font = body, fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            ultBig = new GUIStyle { font = disp, fontSize = 80, alignment = TextAnchor.MiddleCenter };
            var symbols = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Arial Unicode MS", "DejaVu Sans" }, 64);
            ultGlyph = new GUIStyle { font = symbols, fontSize = 50, alignment = TextAnchor.MiddleCenter };
            foreach (var s in new[] { ultTitle, ultSub, ultBig, ultGlyph }) s.normal.textColor = Color.white;
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

        static readonly Color UltInk = new Color(0.02f, 0.03f, 0.05f, 0.9f);

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
            float y = 74f;
            foreach (var u in ults)
            {
                if (u.by == null) continue;
                y = UltBanner(u, vw, y) + 8f;
                UltWords(u, vw);
            }
            UltHints(vw);
            UltHudGui(vw);   // the call, the top band, the eval bar and move card, the chess clock, the badges (PawnRushSkillFx.Hud.cs)
            GUI.matrix = keepMatrix;
            GUI.color = keepColor;
            UltImpactGui();   // an impact frame's speed lines and badge over everything (PawnRushSkillFx.Impact.cs)
        }

        /// <summary>Whether a piece holds a team ultimate (the knight's two or one of the driven ones): its sounds are made ahead.</summary>
        static bool UltHolder(RagdollPawn p)
        {
            if (p.HasSkill(PawnRushSkillIds.FamilyFork) || p.HasSkill(PawnRushSkillIds.KnightTour)) return true;
            for (int id = PawnRushSkillIds.PromotionRun; id <= PawnRushSkillIds.KingWalk; id++)
                if (PawnRushSkillIds.Driven(id) && p.HasSkill(id)) return true;
            return false;
        }

        /// <summary>A driven ultimate's line for the other team's banner.</summary>
        static string DriveEnemyLine(int id) => id switch
        {
            PawnRushSkillIds.Nightmare => "머리 위를 조심해요! 12 m 넘게 흩어져요",
            PawnRushSkillIds.PromotionRun => "질주 줄에서 비켜요!",
            PawnRushSkillIds.GreatDiagonal => "빛의 대각선과 나란히 서지 마요!",
            PawnRushSkillIds.RookRoll => "곧은 길에서 비켜요!",
            PawnRushSkillIds.QueenDescent => "그림자를 보면 흩어져요!",
            PawnRushSkillIds.KingWalk => "행렬을 끊어요!",
            _ => "조심해요!",
        };

        /// <summary>The caster's time bar: its time left (red and blinking in the last 2 s) and its line (presses left, its
        /// level, what it has hit).</summary>
        void UltDriveHud(float vw)
        {
            var me = Viewer;
            if (me == null) return;
            foreach (var u in ults)
            {
                if (!u.Driven || u.by != me || !u.Playing) continue;
                float total = Mathf.Max(0.5f, u.air), left = Mathf.Clamp(total - u.by.PlanTime, 0f, total), k = left / total;
                const float w = 440f, h = 12f, y = 560f;
                float x = (vw - w) * 0.5f;
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), Texture2D.whiteTexture);
                bool late = left < 2f && Mathf.Repeat(Clock * 4f, 1f) < 0.5f;
                GUI.color = late ? new Color(1f, 0.35f, 0.35f) : new Color(0.25f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(x, y, w * k, h), Texture2D.whiteTexture);
                var st = new GUIStyle(ultSub) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                GUI.color = Color.white;
                Outlined(new Rect(x - 100f, y - 32f, w + 200f, 28f), $"{u.title}  {left:0.0}초  ·  {DriveLine(u)}", st, new Color(0.9f, 1f, 0.97f), 1.5f);
                return;
            }
        }

        /// <summary>The banner under the top bar; returns its bottom.</summary>
        float UltBanner(UltShow u, float vw, float y)
        {
            float a = Clock - u.t0, k = Mathf.Clamp01(a / 0.12f) * u.Life(Clock) * (1f - Smooth01((a - 2.4f) / 0.4f));
            if (k <= 0.001f) return y;
            var me = Viewer;
            bool ally = me == null || me.Team == u.by.Team || me == u.by;
            string title = $"{Teams.Name(u.by.Team)}팀 {PieceName(u.by.Piece)}   {u.title}!!";
            string sub = ally ? $"{u.by.DisplayName} · {UltBannerNota(u)}" : u.Driven ? DriveEnemyLine(u.skill) : (u.Fork ? "포크 칸에서 벗어나요!" : "머리 위를 조심해요!");
            bool face = pipShow == u && pipTex != null && pipCam != null && pipCam.enabled;
            const float faceW = 150f;
            var tw = ultTitle.CalcSize(new GUIContent(title)).x;
            var sw = ultSub.CalcSize(new GUIContent(sub)).x;
            float w = Mathf.Max(tw, sw) + 120f + faceW, h = 84f;
            float x = (vw - w) * 0.5f + (1f - Smooth01(a / 0.3f)) * -60f;
            GUI.color = new Color(1f, 1f, 1f, k);
            GUI.DrawTexture(new Rect(x, y, w, h), ally ? ultGradTeal : ultGradRed);
            GUI.color = new Color(1f, 1f, 1f, 0.8f * k);
            GUI.DrawTexture(new Rect(x, y, w, 2.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - 2.5f, w, 2.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, 2.5f, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + w - 2.5f, y, 2.5f, h), Texture2D.whiteTexture);
            float open = face ? Smooth01((a - 0.06f) / 0.26f) * (1f - Smooth01((a - 1.3f) / 0.3f)) : 0f;
            if (open > 0.01f)
            {
                var win = new Rect(x + 8f, y + 8f + (h - 16f) * (1f - open) * 0.5f, faceW - 8f, (h - 16f) * open);
                GUI.color = new Color(1f, 1f, 1f, k);
                GUI.DrawTexture(win, pipTex, ScaleMode.ScaleAndCrop, false);
                GUI.color = new Color(1f, 1f, 1f, 0.9f * k);
                GUI.DrawTexture(new Rect(win.x - 1f, win.y - 1f, win.width + 2f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(win.x - 1f, win.yMax - 1f, win.width + 2f, 2f), Texture2D.whiteTexture);
            }
            GUI.color = new Color(1f, 1f, 1f, k);
            Outlined(new Rect(x + faceW, y, 90f, h), PieceGlyph(u.by.Piece), ultGlyph, Color.white, 1.5f);
            Outlined(new Rect(x + faceW + 88f, y + 4f, tw + 20f, 46f), title, ultTitle, Color.white, 2.5f);
            Outlined(new Rect(x + faceW + 90f, y + 46f, sw + 20f, 30f), sub, ultSub, ally ? new Color(0.9f, 1f, 0.97f) : new Color(1f, 0.9f, 0.9f), 1.2f);
            GUI.color = Color.white;
            return y + h;
        }

        /// <summary>(11-22: the big words in the middle of the screen are gone - the centre stays clear for the play; the call
        /// and the grade word are the HUD's, in the top band: PawnRushSkillFx.Hud.cs.)</summary>
        void UltWords(UltShow u, float vw)
        {
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

        /// <summary>For the piece played here: what its keys do while its own ultimate plays, or a warning when an enemy's
        /// aims at it.</summary>
        void UltHints(float vw)
        {
            var me = Viewer;
            if (me == null) return;
            var st = new GUIStyle(ultSub) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            foreach (var u in ults)
            {
                if (!u.Playing || u.by == null) continue;
                string text = null;
                Color color = Color.white;
                if (u.by == me)
                {
                    text = u.Driven ? DriveHint(u)
                         : u.Fork ? "이동 키 = 착지 칸 옮기기  ·  내려올 때 고리가 청록이면 Q"
                         : u.finalAt >= 0f ? "이동 키 = 착지 칸 옮기기"
                         : "카메라로 다음 머리 고르기  ·  고리가 청록일 때 Q";
                    color = new Color(0.85f, 1f, 0.97f);
                }
                else if (Teams.AreEnemies(me.Team, u.by.Team))
                {
                    if (u.Driven) text = DriveWarning(u, me);
                    else
                    {
                        bool aimed = u.Tour ? u.by.PlanTarget == me : u.Fork && UltOnForkSquare(u, me);
                        if (aimed) text = u.Fork ? "포크 칸이에요 — 비켜요!" : "나이트가 머리를 노려요 — 비켜요!";
                    }
                    color = new Color(1f, 0.35f, 0.35f);
                }
                if (text == null) continue;
                float blink = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Clock * 6f));
                GUI.color = new Color(0f, 0f, 0f, 0.5f * blink);
                GUI.DrawTexture(new Rect(vw * 0.5f - 330f, 588f, 660f, 40f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, blink);
                Outlined(new Rect(vw * 0.5f - 330f, 588f, 660f, 40f), text, st, color, 1.5f);
                GUI.color = Color.white;
                return;
            }
        }

        bool UltOnForkSquare(UltShow u, RagdollPawn p)
        {
            if (u.by == null || p == null) return false;
            Vector3 landing = u.by.PlanPoint;
            return RagdollPawn.OnKnightSquare(Flat(p.Hips.position - landing), FamSquare, FamCatch);
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

        /// <summary>A brass note: a bright tone with its first harmonics, a quick start and a slow fall.</summary>
        static float Brass(float f, float t)
        {
            const float Tau = Mathf.PI * 2f;
            return (Mathf.Sin(Tau * f * t) + 0.5f * Mathf.Sin(Tau * 2f * f * t) + 0.33f * Mathf.Sin(Tau * 3f * f * t) + 0.2f * Mathf.Sin(Tau * 4f * f * t))
                   * Mathf.Clamp01(t * 25f) * Mathf.Exp(-t * 1.6f);
        }

        static float Chime(float f, float t)
        {
            const float Tau = Mathf.PI * 2f;
            return (Mathf.Sin(Tau * f * t) + 0.35f * Mathf.Sin(Tau * 2f * f * t) + 0.15f * Mathf.Sin(Tau * 3.01f * f * t)) * Mathf.Exp(-t * 4.5f) * Mathf.Clamp01(t * 300f);
        }

        void UltAudioBuild()
        {
            if (ultAudio != null) return;
            ultAudio = root.gameObject.AddComponent<AudioSource>();
            ultAudio.playOnAwake = false;
            ultAudio.spatialBlend = 0f;
            ultLoop = root.gameObject.AddComponent<AudioSource>();
            ultLoop.playOnAwake = false;
            ultLoop.spatialBlend = 0f;
            const float Tau = Mathf.PI * 2f;
            sfxBoom = Synth("Ult boom", 1.1f, t => 0.95f * Mathf.Sin(Tau * (52f * t - 6f * t * t)) * Mathf.Exp(-t * 3f) + 0.35f * Noise(t) * Mathf.Exp(-t * 35f));
            // Electricity: crackles of noise in bursts over a low hum (loops).
            sfxCrackle = Synth("Ult crackle", 2f, t =>
            {
                float burst = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Tau * 7.3f * t) * Mathf.Sin(Tau * 3.1f * t + 1f)), 3f);
                return 0.35f * Noise(t) * (0.15f + burst) + 0.12f * Mathf.Sin(Tau * 100f * t) * (0.6f + 0.4f * Mathf.Sin(Tau * 2f * t));
            });
            sfxPerfect = Synth("Ult perfect", 0.9f, t => 0.5f * (Chime(1046.5f, t) + (t > 0.05f ? Chime(1568f, t - 0.05f) : 0f)));
            sfxOff = Synth("Ult off", 0.25f, t => 0.5f * Mathf.Sin(Tau * 180f * t) * Mathf.Exp(-t * 18f) + 0.15f * Noise(t) * Mathf.Exp(-t * 40f));
            sfxImpactLow = Synth("Ult impact", 1.4f, t => 0.9f * Mathf.Sin(Tau * (38f * t - 4f * t * t)) * Mathf.Exp(-t * 2.4f) + 0.45f * Noise(t) * Mathf.Exp(-t * 14f));
            sfxFire = Synth("Ult fire", 2.4f, t => 0.3f * Noise(t) * (1f - Mathf.Exp(-t * 20f)) * Mathf.Exp(-t * 1.1f) * (0.7f + 0.3f * Mathf.Sin(Tau * 11f * t)));
            // The rook's roll: a low rumble (loops: whole cycles of every wave in its 2 s) and a bowling pin's clack.
            sfxRumble = Synth("Ult rumble", 2f, t => 0.28f * Noise(t) * (0.6f + 0.4f * Mathf.Sin(Tau * 7f * t)) + 0.45f * Mathf.Sin(Tau * 48f * t) * (0.7f + 0.3f * Mathf.Sin(Tau * 3f * t)));
            // The queen's dive: a whistle falling for its second (the phase of a sweep from 1400 to 500 Hz).
            sfxWhistle = Synth("Ult whistle", 1.05f, t => 0.35f * Mathf.Sin(Tau * (1400f * t - 450f * t * t)) * Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1.05f - t) * 10f) + 0.08f * Noise(t) * t);
            // The king's walk: a brass fanfare climbing G, C, E, G.
            sfxFanfare = Synth("Ult fanfare", 1.7f, t => 0.2f * (Brass(392f, t) + (t > 0.12f ? Brass(523.25f, t - 0.12f) : 0f) + (t > 0.24f ? Brass(659.25f, t - 0.24f) : 0f) + (t > 0.36f ? Brass(784f, t - 0.36f) * 1.3f : 0f)));
            sfxClack = Synth("Ult clack", 0.3f, t => 0.55f * (Mathf.Sin(Tau * 950f * t) + 0.6f * Mathf.Sin(Tau * 1430f * t)) * Mathf.Exp(-t * 38f) + 0.4f * Noise(t) * Mathf.Exp(-t * 90f));
            int[] steps = { 0, 2, 4, 7, 9, 12, 14, 16 };
            for (int i = 0; i < sfxChime.Length; i++)
            {
                float f = 523.25f * Mathf.Pow(2f, steps[i] / 12f);
                sfxChime[i] = Synth($"Ult chime {i}", 0.9f, t => 0.5f * Chime(f, t));
            }
            // The judgements: a bright chord, a chime, a soft chime, a dull pair, a low thud.
            sfxGrade[0] = Synth("Ult brilliant", 1.6f, t => 0.32f * (Chime(659.25f, t) + Chime(830.6f, t) + Chime(987.8f, t) + (t > 0.08f ? Chime(1318.5f, t - 0.08f) : 0f)));
            sfxGrade[1] = Synth("Ult great", 1.2f, t => 0.45f * (Chime(784f, t) + (t > 0.07f ? Chime(1175f, t - 0.07f) : 0f)));
            sfxGrade[2] = Synth("Ult best", 1f, t => 0.45f * Chime(880f, t));
            sfxGrade[3] = Synth("Ult inaccuracy", 0.8f, t => 0.4f * (Chime(392f, t) + (t > 0.12f ? Chime(370f, t - 0.12f) : 0f)));
            sfxGrade[4] = Synth("Ult blunder", 1f, t => 0.7f * Mathf.Sin(Tau * (90f - 30f * t) * t) * Mathf.Exp(-t * 3f));
            clipLeap = Resources.Load<AudioClip>("PawnRushSkillSfx/KnightLeap");
            clipStomp = Resources.Load<AudioClip>("PawnRushSkillSfx/KnightStomp");
            clipLand = Resources.Load<AudioClip>("PawnRushSkillSfx/KnightLand");
            clipWall = Resources.Load<AudioClip>("PawnRushSkillSfx/RookWall");
        }

        void UltPlay(AudioClip clip, float level, float delay = 0f, float pitch = 1f)
        {
            if (clip == null || ultAudio == null) return;
            pitch *= UltSfxPitch;
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
                case UltSfx.Boom: UltPlay(sfxBoom, 1f); UltPlay(clipLeap, 0.8f); break;
                case UltSfx.Leap: UltPlay(clipLeap, 0.8f, 0f, 1f + 0.04f * Mathf.Clamp(n, 0, 8)); break;
                case UltSfx.Perfect: UltPlay(sfxPerfect, 0.85f); break;
                case UltSfx.Off: UltPlay(sfxOff, 0.6f); break;
                case UltSfx.Stomp:
                    UltPlay(clipStomp, 0.9f);
                    UltPlay(sfxChime[Mathf.Clamp(n - 1, 0, sfxChime.Length - 1)], 0.8f);
                    break;
                case UltSfx.Impact:
                    UltPlay(clipLand, 1f);
                    UltPlay(sfxImpactLow, n <= RagdollPawn.GradeGreat ? 1f : 0.6f);
                    if (n <= RagdollPawn.GradeGreat) UltPlay(clipWall, 0.7f, 0f, 0.8f);
                    if (n <= RagdollPawn.GradeGreat) UltPlay(sfxFire, 0.8f);
                    UltPlay(sfxGrade[Mathf.Clamp(n, 0, sfxGrade.Length - 1)], 0.9f, 0.15f);
                    break;
            }
        }
    }
}
