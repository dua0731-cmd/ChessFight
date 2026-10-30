using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// One of the bishop's two swords as he lets it go (R191, 10-28, F4 of his X cut; reference F_02_throw_alt.png): a
    /// crisp, very short silver star at his hand where it lets go (the child <see cref="FlashName"/>, a quad with the
    /// glint material), and the air split by a thin bright straight line from that point to the sword as it flies off (a
    /// LineRenderer on this object with the slash material, brightest at the sword), which then closes up into the sword
    /// and is gone in a sixth of a second (F5's trail takes over). The sword itself is the game's: in the game
    /// <see cref="Follow"/> hands it where the thrown sword is each frame and where his hand was as it let go; with nothing
    /// to follow (the workbench, the capture tool) it throws its own copy of the game's way for a bishop standing at its
    /// origin, on a copy of his sword (the child <see cref="PreviewName"/>, hidden in the game). This object sits
    /// unturned at its parent's origin, the parent's +Z the bishop's way and +X his right.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(120)]   // after the pieces are drawn (RagdollVisualSync, 100), when the game has placed its sword
    public sealed class VfxSwordRelease : MonoBehaviour, IVfxSeekable
    {
        /// <summary>Where the followed sword is now, in the world: its middle (it spins about it) and the way its blade
        /// points. False while it is not flying.</summary>
        public delegate bool Source(out Vector3 middle, out Vector3 blade);

        public const string FlashName = "Flash", PreviewName = VfxBladeCharge.PreviewName;
        /// <summary>The game's way, for the preview (as VfxBladeCharge's): where his hand is as he lets go, up over his
        /// shoulder (his right; the left mirrored), half a sword's length (SwordFightSkills.BishopSwordHalf), how high over
        /// the floor the two cross (SwordFightSkillParams.bishopCrossHeight), and a throw of 4.2 m taking 0.3 s.</summary>
        static readonly Vector3 HandBack = new Vector3(.3f, 1.15f, -.1f);
        const float Half = .38f, CrossHeight = .55f, Reach = 4.2f, OutTime = .3f;
        /// <summary>The way the sword sets off from his hand (his right; the left mirrored): out to his side, forward and a
        /// little up (the start of SwordFightSkills.BishopSwordPose's curve).</summary>
        static readonly Vector3 SetOff = new Vector3(.7f, .2f, .68f).normalized;
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity"),
            RevealId = Shader.PropertyToID("_Reveal"), TurnId = Shader.PropertyToID("_Turn");

        [Tooltip("0 his right sword, 1 his left.")]
        public int hand;

        [Header("Flash at the hand")]
        [Tooltip("Seconds the flash lasts.")]
        public float flashTime = .09f;
        [Tooltip("Width (m) of the flash as he lets go; it shrinks to half as it goes.")]
        public float flashSize = .34f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float flashIntensity = 5f;
        [Tooltip("Degrees its rays are turned (the left hand's the other way).")]
        public float flashTurn = 20f;
        [Tooltip("How far (m) out along the sword's way the flash sits from his hand: at his opening fingertips, clear of the pommel and the mitten that would hide its middle from behind.")]
        public float flashAhead = .14f;

        [Header("The split in the air")]
        [Tooltip("Seconds the line holds from his hand to the sword.")]
        public float hold = .05f;
        [Tooltip("Seconds it then takes to close up into the sword, fading.")]
        public float close = .1f;
        [Tooltip("Width (m) of the line: its glow round a hair-thin core.")]
        public float width = .05f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float intensity = 2.5f;

        [Header("Preview (the workbench and the capture tool)")]
        [Tooltip("Turns a second the thrown sword spins (SwordFightSkillParams.bishopSpin).")]
        public float spin = 6f;

        readonly Vector3[] points = new Vector3[2];
        LineRenderer line;
        Transform flash, preview;
        Renderer flashView;
        MaterialPropertyBlock block;
        Source source;
        Vector3 handAt, swordAt;
        bool hasHand, hasSword;
        float age;

        float Sign => hand == 0 ? 1f : -1f;

        void OnEnable()
        {
            age = 0f;
            hasHand = hasSword = false;
            if (source == null) Pose(0f);
            Draw();
        }

        /// <summary>From now on follows the game's sword through <paramref name="where"/>, the flash and the split
        /// starting at <paramref name="from"/> (world), where his hand let go of it. The preview's sword is hidden.</summary>
        public void Follow(Source where, Vector3 from)
        {
            source = where;
            age = 0f;
            hasSword = false;
            if (Ready() && preview != null) preview.gameObject.SetActive(false);
            handAt = transform.InverseTransformPoint(from);
            hasHand = true;
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

        /// <summary>As it is <paramref name="at"/> seconds after he lets go, on its own copy of the sword (the capture
        /// tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            Pose(at);
            Draw();
        }

        /// <summary>Where the game's sword is now (kept as it was when it is not drawn for a moment).</summary>
        void Pull()
        {
            if (!source(out Vector3 middle, out _)) return;
            hasSword = true;
            swordAt = transform.InverseTransformPoint(middle);
        }

        /// <summary>
        /// The preview's own throw at <paramref name="t"/> seconds, placed on the copy of his sword: the game's throw for a
        /// bishop standing at this object's origin (SwordFightSkills.BishopSwordPose: off his hand up over his shoulder,
        /// out wide to its own side, whirling flat), from where his hand lets go.
        /// </summary>
        void Pose(float t)
        {
            float sgn = Sign;
            Vector3 up = Vector3.up, f = Vector3.forward, side = Vector3.right * sgn;
            Vector3 grip0 = Vector3.Scale(HandBack, new Vector3(sgn, 1f, 1f));
            float s = Mathf.Clamp(t, 0f, OutTime), e = 1f - Mathf.Pow(1f - s / OutTime, 1.6f), o = 1f - e;
            Vector3 s0 = f * Reach + up * CrossHeight + (side + up).normalized * .45f;
            Vector3 p0 = grip0 + f * Half;
            Vector3 flat = s0 - p0;
            flat.y = 0f;
            float d = flat.magnitude;
            Vector3 p1 = p0 + f * (.3f * d) + side * (.45f + .22f * d) + up * .35f;
            Vector3 p2 = s0 + side * (.35f + .1f * d) - f * (.15f * d) + up * .25f;
            Vector3 mid = o * o * o * p0 + 3f * o * o * e * p1 + 3f * o * e * e * p2 + e * e * e * s0;
            handAt = grip0;
            swordAt = mid;
            hasHand = hasSword = true;
            if (!Ready() || preview == null) return;
            Quaternion turn = Quaternion.AngleAxis(360f * spin * s * sgn, (up + side * .35f).normalized) * Quaternion.LookRotation(f, up);
            preview.localPosition = mid - turn * Vector3.forward * Half;
            preview.localRotation = turn;
            if (!preview.gameObject.activeSelf) preview.gameObject.SetActive(true);
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears them (Docs/Environment/PITFALLS.md 53).
            if (line == null) line = GetComponent<LineRenderer>();
            if (flash == null)
            {
                flash = transform.Find(FlashName);
                flashView = flash != null ? flash.GetComponent<Renderer>() : null;
            }
            if (preview == null) preview = transform.Find(PreviewName);
            if (block == null) block = new MaterialPropertyBlock();
            return line != null;
        }

        void Draw()
        {
            if (!Ready()) return;
            Vector3 fingertips = handAt + Vector3.Scale(SetOff, new Vector3(Sign, 1f, 1f)) * flashAhead;

            // The flash at his hand: at its brightest as he lets go, shrinking to half and gone in flashTime.
            float k = age / Mathf.Max(.01f, flashTime);
            if (flashView != null)
            {
                flashView.enabled = hasHand && k < 1f;
                if (flashView.enabled)
                {
                    flash.localPosition = fingertips;
                    flash.localRotation = Quaternion.identity;
                    flash.localScale = Vector3.one * (flashSize * (1f - .5f * k));
                    flashView.GetPropertyBlock(block);
                    block.SetFloat(IntensityId, flashIntensity);
                    block.SetFloat(FadeId, (1f - k) * (1f - k));
                    block.SetFloat(TurnId, flashTurn * Sign);
                    flashView.SetPropertyBlock(block);
                }
            }

            // The split: from his fingertips where they let go to the sword, then closing up into it as it fades.
            float c = Mathf.Clamp01((age - hold) / Mathf.Max(.01f, close));
            float fade = 1f - Mathf.SmoothStep(0f, 1f, c);
            line.enabled = hasHand && hasSword && fade > .001f;
            if (!line.enabled) return;
            points[0] = Vector3.Lerp(fingertips, swordAt, Mathf.SmoothStep(0f, 1f, c));
            points[1] = swordAt;
            line.positionCount = 2;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.GetPropertyBlock(block);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(FadeId, fade);
            // Drawn all the way to the sword, its head (the brightest) there.
            block.SetFloat(RevealId, 1f);
            line.SetPropertyBlock(block);
        }
    }
}
