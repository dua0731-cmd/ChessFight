using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// One of the bishop's two swords as he draws them back (R188, 10-28, F3 of his X cut; reference F_02_throw.png):
    /// silver light runs up the blade from where it starts (over the guard and its collar) to the tip in the 0.2 s he takes to draw it back over his
    /// shoulder (a LineRenderer on this object along the blade with the VFX_BladeChargeAdd material: lit as far as the
    /// light has run, brighter toward the tip, a brighter band running up at the light's edge), and as it gets there the
    /// tip lights up (the child <see cref="TipName"/>, a quad with the glint material). He lets go with the blades lit and
    /// the tips brightest; the light then flares a little and dies away on the flying swords in <see cref="after"/>.
    /// The sword itself is the game's; this only follows it: in the game <see cref="Follow"/> hands it where the drawn
    /// blade is each frame (the one in his hand, then the thrown copy); with nothing to follow (the workbench, the
    /// capture tool) it draws its own copy of the game's draw-back for a bishop standing at its origin, on a copy of his
    /// sword (the child <see cref="PreviewName"/>, hidden in the game). This object sits unturned at its parent's origin,
    /// the parent's +Z the bishop's way and +X his right.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(120)]   // after the pieces are drawn (RagdollVisualSync, 100), when the game has placed its sword
    public sealed class VfxBladeCharge : MonoBehaviour, IVfxSeekable
    {
        /// <summary>Where the followed blade is now, in the world: from where it starts to its tip. False while none is
        /// drawn.</summary>
        public delegate bool Source(out Vector3 guard, out Vector3 tip);

        public const string TipName = "Tip", PreviewName = "Preview sword";
        /// <summary>The game's sword models stand along their +Y; the bishop's blade itself starts this high, over its
        /// guard (0.06 to 0.13) and the openwork collar above it (as SwordFightPawn.DrawnBlade reads them).</summary>
        public const float BladeStart = .36f;
        /// <summary>The game's way, for the preview: where his hands are held low and forward as he aims and up over his
        /// shoulders when he lets go (his right; the left mirrored), half a sword's length (SwordFightSkills.BishopSwordHalf),
        /// how high over the floor the two cross (SwordFightSkillParams.bishopCrossHeight), and a throw of 4.2 m taking 0.3 s.</summary>
        static readonly Vector3 HandLow = new Vector3(.3f, .9f, .3f), HandBack = new Vector3(.3f, 1.15f, -.1f);
        const float Half = .38f, CrossHeight = .55f, Reach = 4.2f, OutTime = .3f;
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity"),
            FillId = Shader.PropertyToID("_Fill"), FrontId = Shader.PropertyToID("_Front"), TurnId = Shader.PropertyToID("_Turn");

        [Tooltip("0 his right sword, 1 his left.")]
        public int hand;
        [Min(.05f), Tooltip("Seconds he takes to draw them back (SwordFightSkillParams.bishopThrowWindup); he lets go at the end. The game hands its own.")]
        public float windup = .2f;
        [Range(.2f, 1f), Tooltip("Share of the draw-back by which the light has run up to the tip.")]
        public float fillBy = .8f;
        [Tooltip("Seconds the light takes to die away on the flying sword once he lets go.")]
        public float after = .15f;

        [Header("Blade")]
        [Tooltip("Width (m) of the light along the blade: its glow, wider than the blade's own 9 cm.")]
        public float width = .17f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float intensity = 2f;
        [Tooltip("How much brighter it flares as he lets go, dying away with it.")]
        public float release = 1.4f;

        [Header("Tip")]
        [Tooltip("Width (m) of the tip's glint at its brightest.")]
        public float tipSize = .18f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float tipIntensity = 4f;

        [Header("Preview (the workbench and the capture tool)")]
        [Tooltip("Turns a second the thrown sword spins once he lets go (SwordFightSkillParams.bishopSpin).")]
        public float spin = 6f;

        readonly Vector3[] points = new Vector3[2];
        LineRenderer line;
        Transform tip, preview;
        Renderer tipView;
        MeshFilter previewMesh;
        MaterialPropertyBlock block;
        Source source;
        Vector3 guardAt, tipAt;
        bool has;
        float age;

        float Sign => hand == 0 ? 1f : -1f;

        void OnEnable()
        {
            age = 0f;
            has = false;
            if (source == null) Pose(0f);
            Draw();
        }

        /// <summary>From now on follows the game's blade through <paramref name="where"/>, the draw-back taking
        /// <paramref name="seconds"/> (the game: SwordFightSkillParams.bishopThrowWindup). The preview's sword is hidden.</summary>
        public void Follow(Source where, float seconds)
        {
            source = where;
            windup = Mathf.Max(.05f, seconds);
            age = 0f;
            has = false;
            if (Ready() && preview != null) preview.gameObject.SetActive(false);
            Pull();
            Draw();
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (source != null) Pull();
            else Pose(age);
            Draw();
        }

        /// <summary>As it is <paramref name="at"/> seconds after he starts drawing back, on its own copy of the sword (the
        /// capture tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            Pose(at);
            Draw();
        }

        /// <summary>Where the game's blade is now (kept as it was when none is drawn for a moment).</summary>
        void Pull()
        {
            if (!source(out Vector3 guard, out Vector3 point)) return;
            has = true;
            guardAt = transform.InverseTransformPoint(guard);
            tipAt = transform.InverseTransformPoint(point);
        }

        /// <summary>
        /// The preview's own sword at <paramref name="t"/> seconds, placed on the copy of his sword and its blade kept:
        /// the game's draw-back for a bishop standing at this object's origin (SwordFightSkills.BishopKey and BishopBlade:
        /// from held low and forward, a little apart, up and back over his shoulder, the blade turning from forward and
        /// down to back and up), then thrown (BishopSwordPose: off his hand out wide to its own side, whirling flat).
        /// </summary>
        void Pose(float t)
        {
            if (!Ready() || preview == null || previewMesh == null)
            {
                has = false;
                return;
            }
            float sgn = Sign;
            Vector3 up = Vector3.up, f = Vector3.forward, side = Vector3.right * sgn;
            Vector3 mirror = new Vector3(sgn, 1f, 1f), grip;
            Quaternion turn;
            if (t <= windup)
            {
                float k = Out2(t / windup);
                grip = Vector3.Scale(Vector3.Lerp(HandLow, HandBack, k), mirror) + up * (.12f * Mathf.Sin(Mathf.PI * k));
                Vector3 low = (f * .75f - up * .35f + side * .25f).normalized, back = (-f * .6f + up * .55f + side * .3f).normalized;
                turn = Quaternion.LookRotation(Vector3.Slerp(low, back, k), up);
            }
            else
            {
                float s = Mathf.Min(t - windup, OutTime), e = 1f - Mathf.Pow(1f - s / OutTime, 1.6f), o = 1f - e;
                Vector3 s0 = f * Reach + up * CrossHeight + (side + up).normalized * .45f;
                Vector3 p0 = Vector3.Scale(HandBack, mirror) + f * Half;
                Vector3 flat = s0 - p0;
                flat.y = 0f;
                float d = flat.magnitude;
                Vector3 p1 = p0 + f * (.3f * d) + side * (.45f + .22f * d) + up * .35f;
                Vector3 p2 = s0 + side * (.35f + .1f * d) - f * (.15f * d) + up * .25f;
                Vector3 mid = o * o * o * p0 + 3f * o * o * e * p1 + 3f * o * e * e * p2 + e * e * e * s0;
                turn = Quaternion.AngleAxis(360f * spin * s * sgn, (up + side * .35f).normalized) * Quaternion.LookRotation(f, up);
                grip = mid - turn * Vector3.forward * Half;
            }
            preview.localPosition = grip;
            preview.localRotation = turn;
            if (!preview.gameObject.activeSelf) preview.gameObject.SetActive(true);
            has = Blade(previewMesh, out Vector3 guard, out Vector3 point);
            guardAt = transform.InverseTransformPoint(guard);
            tipAt = transform.InverseTransformPoint(point);
        }

        /// <summary>A sword model's blade in the world, from where it starts to its tip: the game's sword models stand
        /// along their +Y (SwordFightPawn.UpdateSwordModel), the blade starting <see cref="BladeStart"/> up, the tip at
        /// the top of the mesh.</summary>
        public static bool Blade(MeshFilter model, out Vector3 guard, out Vector3 tip)
        {
            guard = tip = Vector3.zero;
            if (model == null || model.sharedMesh == null) return false;
            Bounds b = model.sharedMesh.bounds;
            guard = model.transform.TransformPoint(new Vector3(b.center.x, BladeStart, b.center.z));
            tip = model.transform.TransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            return true;
        }

        static float Out2(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x);
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears them (Docs/Environment/PITFALLS.md 53).
            if (line == null) line = GetComponent<LineRenderer>();
            if (tip == null)
            {
                tip = transform.Find(TipName);
                tipView = tip != null ? tip.GetComponent<Renderer>() : null;
            }
            if (preview == null)
            {
                preview = transform.Find(PreviewName);
                previewMesh = preview != null ? preview.GetComponentInChildren<MeshFilter>(true) : null;
            }
            if (block == null) block = new MaterialPropertyBlock();
            return line != null;
        }

        void Draw()
        {
            if (!Ready()) return;
            float fill = Mathf.SmoothStep(0f, 1f, age / (windup * fillBy));
            float gone = age > windup ? Mathf.Clamp01((age - windup) / Mathf.Max(.01f, after)) : 0f;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, gone);
            // A flare as he lets go, dying away with the light.
            float flare = age > windup ? Mathf.Lerp(release, 1f, gone) : 1f;

            // The blade, lit as far as the light has run; the band at its edge goes as it gets to the tip.
            line.enabled = has && fade > .001f && fill > .001f;
            if (line.enabled)
            {
                points[0] = guardAt;
                points[1] = tipAt;
                line.positionCount = 2;
                line.SetPositions(points);
                line.widthMultiplier = width;
                line.GetPropertyBlock(block);
                block.SetFloat(IntensityId, intensity * flare);
                block.SetFloat(FadeId, fade);
                block.SetFloat(FillId, fill);
                block.SetFloat(FrontId, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.8f, 1f, fill)));
                line.SetPropertyBlock(block);
            }

            // The tip, lighting up as the light gets there, brightest as he lets go.
            float lit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.75f, 1f, fill)) * fade;
            if (tipView == null) return;
            tipView.enabled = has && lit > .001f;
            if (!tipView.enabled) return;
            tip.localPosition = tipAt;
            tip.localRotation = Quaternion.identity;
            tip.localScale = Vector3.one * (tipSize * (.5f + .5f * lit));
            tipView.GetPropertyBlock(block);
            block.SetFloat(IntensityId, tipIntensity * flare);
            block.SetFloat(FadeId, lit);
            block.SetFloat(TurnId, 12f * Sign);
            tipView.SetPropertyBlock(block);
        }
    }
}
