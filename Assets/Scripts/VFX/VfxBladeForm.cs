using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The bishop's second sword forming in his empty left hand as he calls his X cut (R194, 10-28, F1; reference
    /// F_01_draw.png): small silver shards fly in from the air round his hand (the children named <see cref="ShardPrefix"/>,
    /// quads with the glint material) and land on the blade from where it starts up to the tip, each where it belongs;
    /// the part of the blade they have not reached yet is still light (a LineRenderer on this object from the forming
    /// edge to the tip with the VFX_BladeChargeAdd material, its streaks the shards packed together), and a crisp glint
    /// rides the forming edge (the child <see cref="GlintName"/>), flaring at the tip as the last shard lands; then it is
    /// plain steel. The sword itself is the game's (it draws it whole as he calls the skill); this only follows it: in
    /// the game <see cref="Follow"/> hands it where the drawn blade is each frame; with nothing to follow (the workbench,
    /// the capture tool) it holds its own copy of his sword the way the game does while he aims (the child
    /// <see cref="PreviewName"/>, hidden in the game). This object sits unturned at its parent's origin, the parent's +Z
    /// the bishop's way and +X his right.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(120)]   // after the pieces are drawn (RagdollVisualSync, 100), when the game has placed its sword
    public sealed class VfxBladeForm : MonoBehaviour, IVfxSeekable
    {
        /// <summary>Where the followed blade is now, in the world: from where it starts to its tip. False while none is
        /// drawn.</summary>
        public delegate bool Source(out Vector3 guard, out Vector3 tip);

        public const string GlintName = "Glint", ShardPrefix = "Shard ", PreviewName = VfxBladeCharge.PreviewName;
        /// <summary>The game's way, for the preview (as VfxBladeCharge's): where his hand holds it low and forward as he
        /// aims (his right; the left mirrored).</summary>
        static readonly Vector3 HandLow = new Vector3(.3f, .9f, .3f);
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity"),
            FillId = Shader.PropertyToID("_Fill"), FrontId = Shader.PropertyToID("_Front"), TurnId = Shader.PropertyToID("_Turn");

        [Tooltip("0 his right sword, 1 his left (the one that forms).")]
        public int hand = 1;

        [Header("Forming")]
        [Tooltip("Seconds before the first shards land (they are on their way as the sword appears).")]
        public float lead = .1f;
        [Tooltip("Seconds the shards take to land from where the blade starts to its tip.")]
        public float form = .3f;

        [Header("The blade still to form")]
        [Tooltip("Width (m) of the light over the part of the blade not yet formed.")]
        public float width = .18f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float intensity = 2.6f;
        [Tooltip("Seconds it takes to light up as the sword appears.")]
        public float fadeIn = .05f;

        [Header("Shards")]
        [Tooltip("Width (m) of a shard at its biggest.")]
        public float shardSize = .07f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float shardIntensity = 4f;
        [Tooltip("How far (m) from the blade the shards start out.")]
        public float reach = .5f;
        [Tooltip("Seconds a shard takes to fly in.")]
        public float flight = .16f;

        [Header("Glint")]
        [Tooltip("Width (m) of the glint riding the forming edge.")]
        public float glintSize = .16f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float glintIntensity = 3f;
        [Tooltip("Width (m) of the flare at the tip as the last shard lands.")]
        public float tipSize = .3f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float tipIntensity = 5f;
        [Tooltip("Seconds the tip's flare lasts.")]
        public float tipTime = .14f;

        readonly Vector3[] points = new Vector3[2];
        LineRenderer line;
        Transform glint, preview;
        Renderer glintView;
        MeshFilter previewMesh;
        Transform[] shards;
        Renderer[] shardViews;
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
            if (source == null) Pose();
            Draw();
        }

        /// <summary>From now on follows the game's blade through <paramref name="where"/>. The preview's sword is
        /// hidden.</summary>
        public void Follow(Source where)
        {
            source = where;
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
            else Pose();
            Draw();
        }

        /// <summary>As it is <paramref name="at"/> seconds after the sword appears, on its own copy of the sword (the
        /// capture tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            Pose();
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

        /// <summary>The preview's own sword, held the way the game holds it while he aims (SwordFightSkills.BishopKey and
        /// BishopBlade: low and forward, a little out to its own side) for a bishop standing at this object's origin.</summary>
        void Pose()
        {
            if (!Ready() || preview == null || previewMesh == null)
            {
                has = false;
                return;
            }
            float sgn = Sign;
            Vector3 up = Vector3.up, f = Vector3.forward, side = Vector3.right * sgn;
            preview.localPosition = Vector3.Scale(HandLow, new Vector3(sgn, 1f, 1f));
            preview.localRotation = Quaternion.LookRotation((f * .75f - up * .35f + side * .25f).normalized, up);
            if (!preview.gameObject.activeSelf) preview.gameObject.SetActive(true);
            has = VfxBladeCharge.Blade(previewMesh, out Vector3 guard, out Vector3 point);
            guardAt = transform.InverseTransformPoint(guard);
            tipAt = transform.InverseTransformPoint(point);
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears them (Docs/Environment/PITFALLS.md 53).
            if (line == null) line = GetComponent<LineRenderer>();
            if (glint == null)
            {
                glint = transform.Find(GlintName);
                glintView = glint != null ? glint.GetComponent<Renderer>() : null;
            }
            if (preview == null)
            {
                preview = transform.Find(PreviewName);
                previewMesh = preview != null ? preview.GetComponentInChildren<MeshFilter>(true) : null;
            }
            if (shards == null || (shards.Length > 0 && shards[0] == null))
            {
                int n = 0;
                foreach (Transform child in transform) if (child.name.StartsWith(ShardPrefix)) n++;
                shards = new Transform[n];
                shardViews = new Renderer[n];
                n = 0;
                foreach (Transform child in transform)
                {
                    if (!child.name.StartsWith(ShardPrefix)) continue;
                    shards[n] = child;
                    shardViews[n++] = child.GetComponent<Renderer>();
                }
            }
            if (block == null) block = new MaterialPropertyBlock();
            return line != null;
        }

        /// <summary>A number in 0..1, the same for the same shard each time.</summary>
        static float Rand(int i, int salt)
        {
            float x = Mathf.Sin(i * 12.9898f + salt * 78.233f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        void Draw()
        {
            if (!Ready()) return;
            // How far up the blade the shards have landed.
            float front = Mathf.Clamp01((age - lead) / Mathf.Max(.01f, form));
            Vector3 edge = Vector3.Lerp(guardAt, tipAt, front);

            // The blade still to form: light from the forming edge to the tip, going as the edge gets there.
            float fade = Mathf.SmoothStep(0f, 1f, age / Mathf.Max(.01f, fadeIn)) * (1f - Mathf.SmoothStep(.85f, 1f, front));
            line.enabled = has && fade > .001f;
            if (line.enabled)
            {
                points[0] = edge;
                points[1] = tipAt;
                line.positionCount = 2;
                line.SetPositions(points);
                line.widthMultiplier = width;
                line.GetPropertyBlock(block);
                block.SetFloat(IntensityId, intensity);
                block.SetFloat(FadeId, fade);
                block.SetFloat(FillId, 1f);
                block.SetFloat(FrontId, 0f);
                line.SetPropertyBlock(block);
            }

            // The shards, each flying in from round the blade to where it belongs and gone as it lands (faster at the end:
            // they snap into place), the ones for the blade's start first.
            Vector3 along = tipAt - guardAt;
            Vector3 dir = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward;
            Vector3 a = Vector3.Cross(dir, Vector3.up);
            if (a.sqrMagnitude < 1e-4f) a = Vector3.Cross(dir, Vector3.right);
            a.Normalize();
            Vector3 b = Vector3.Cross(dir, a);
            int n = shards.Length;
            for (int i = 0; i < n; i++)
            {
                var view = shardViews[i];
                if (view == null) continue;
                float u = Mathf.Clamp((i + .5f + (Rand(i, 1) - .5f) * .8f) / Mathf.Max(1, n), .03f, .98f);
                float takes = flight * Mathf.Lerp(.75f, 1.25f, Rand(i, 2));
                float lands = lead + form * u, k = (age - (lands - takes)) / takes;
                view.enabled = has && k > 0f && k < 1f;
                if (!view.enabled) continue;
                float turn = Rand(i, 3) * Mathf.PI * 2f;
                Vector3 from = (a * Mathf.Cos(turn) + b * Mathf.Sin(turn)) * (reach * Mathf.Lerp(.55f, 1f, Rand(i, 4)))
                               + dir * (reach * (Rand(i, 5) - .3f) * .5f);
                shards[i].localPosition = Vector3.Lerp(guardAt, tipAt, u) + from * (1f - k * k);
                shards[i].localRotation = Quaternion.identity;
                shards[i].localScale = Vector3.one * (shardSize * Mathf.Lerp(.6f, 1f, Rand(i, 6)) * (1f + .6f * Mathf.SmoothStep(.75f, 1f, k)));
                view.GetPropertyBlock(block);
                block.SetFloat(IntensityId, shardIntensity);
                block.SetFloat(FadeId, Mathf.SmoothStep(0f, .35f, k));
                block.SetFloat(TurnId, Rand(i, 7) * 90f);
                view.SetPropertyBlock(block);
            }

            // The glint riding the forming edge, then flaring at the tip as the last shard lands.
            if (glintView == null) return;
            float done = age - (lead + form), size, lit, power;
            if (done < 0f)
            {
                size = glintSize;
                lit = Mathf.SmoothStep(0f, .15f, front);
                power = glintIntensity;
            }
            else
            {
                float k = done / Mathf.Max(.01f, tipTime);
                size = tipSize * (1f - .4f * Mathf.Clamp01(k));
                lit = k < 1f ? (1f - k) * (1f - k) : 0f;
                power = tipIntensity;
            }
            glintView.enabled = has && lit > .001f;
            if (!glintView.enabled) return;
            glint.localPosition = done < 0f ? edge : tipAt;
            glint.localRotation = Quaternion.identity;
            glint.localScale = Vector3.one * size;
            glintView.GetPropertyBlock(block);
            block.SetFloat(IntensityId, power);
            block.SetFloat(FadeId, lit);
            block.SetFloat(TurnId, 12f * Sign);
            glintView.SetPropertyBlock(block);
        }
    }
}
