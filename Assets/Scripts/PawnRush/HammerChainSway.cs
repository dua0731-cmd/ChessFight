using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R403 (승규 11-04 "사슬에 애니메이션을 넣어서 스윙하는 듯한 궤적을 만들어라"): the grand stair hall's hammer chains
    // bend as they swing. The pendulum's Pivot still turns the whole hammer rigidly (PendulumSwing, the shared obstacle
    // clock) and the ball stays where its collider is; between the pin and the ball's eye the thirteen links
    // (hammer_v51.py's Hammer_Link_01 .. 13, each in its own holder on the Pivot) bow out of the straight line like a
    // heavy chain: the middle lags behind the swing as it speeds up and runs on past as it slows into the ends, then
    // springs back, and each link turns along the curve with a small twist. A warm ribbon behind the ball draws the arc
    // it sweeps. Pictures only (nothing collides with the links or the ribbon), so it reads Time.deltaTime, like
    // RoyalFanSpin and the hub chandelier's sway.
    public sealed partial class HammerChainSway : MonoBehaviour
    {
        [SerializeField] Transform pivot;
        [SerializeField] Transform[] links = new Transform[0];
        [SerializeField] Vector3[] rest = new Vector3[0];   // each link's middle in the Pivot's space, hanging still
        [SerializeField] Quaternion restPivot = Quaternion.identity;
        [SerializeField] Transform ball;
        [SerializeField] float ballRadius = 1.1f;

        [Header("Bow (m, at the chain's middle)")]
        [Tooltip("How far the swing's angular acceleration throws the chain's middle (x the pseudo force on it).")]
        [SerializeField] float fromAcceleration = 2.2f;
        [Tooltip("How far the chain's middle trails behind the swing per (rad/s x m).")]
        [SerializeField] float fromSpeed = .06f;
        [Tooltip("The chain's own sway: frequency (Hz) and damping ratio.")]
        [SerializeField] float frequency = 1.1f, damping = .28f;
        [SerializeField] float maxBow = .5f;
        [Tooltip("Degrees each link twists about the chain at most, from how fast it bows.")]
        [SerializeField] float twist = 9f;

        [Header("Swing trail")]
        [SerializeField] bool trail = true;
        [SerializeField] Color trailColor = new Color(1f, .78f, .45f, .32f);
        [SerializeField] float trailTime = .32f;

        Quaternion[] restRotation;
        Vector3 axisDown;   // the chain from its first link's middle to its last's, a unit in the Pivot's space
        float length, lastAngle, lastRate, bow, bowRate, shownAccel;
        bool primed;
        TrailRenderer ribbon;

        public void Configure(Transform swingPivot, Transform[] chainLinks, Transform swingBall, float radius)
        {
            pivot = swingPivot;
            links = chainLinks;
            ball = swingBall;
            ballRadius = radius;
            restPivot = swingPivot.localRotation;
            rest = new Vector3[links.Length];
            for (int i = 0; i < links.Length; i++)
            {
                var view = links[i] != null ? links[i].GetComponentInChildren<Renderer>(true) : null;
                rest[i] = view != null ? swingPivot.InverseTransformPoint(view.bounds.center) : Vector3.zero;
            }
        }

        void Start()
        {
            if (pivot == null || links.Length == 0 || rest.Length != links.Length) { enabled = false; return; }
            restRotation = new Quaternion[links.Length];
            for (int i = 0; i < links.Length; i++) restRotation[i] = links[i] != null ? links[i].localRotation : Quaternion.identity;
            var span = rest[rest.Length - 1] - rest[0];
            axisDown = span.sqrMagnitude > 1e-6f ? span.normalized : Vector3.down;
            // The chain runs from the pin (the Pivot's origin) to half a pitch under its last link's middle (the eye).
            length = Mathf.Max(.1f, Vector3.Dot(rest[rest.Length - 1], axisDown) + .5f * span.magnitude / Mathf.Max(1, links.Length - 1));
            if (trail && ball != null) ribbon = MakeTrail();
        }

        // The swing angle (radians) about the Pivot's own z, from its turn away from where it hangs still.
        float SwingAngle()
        {
            var q = Quaternion.Inverse(restPivot) * pivot.localRotation;
            if (q.w < 0f) { q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w; }
            return 2f * Mathf.Atan2(q.z, q.w);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float angle = SwingAngle();
            if (!primed)
            {
                lastAngle = angle;
                lastRate = 0f;
                primed = true;
                return;
            }
            float rate = (angle - lastAngle) / dt;
            float accel = (rate - lastRate) / dt;
            lastAngle = angle;
            lastRate = rate;
            // A frame's acceleration from two differences is noisy: smooth it over about 50 ms.
            shownAccel = Mathf.Lerp(shownAccel, Mathf.Clamp(accel, -30f, 30f), 1f - Mathf.Exp(-dt / .05f));

            // The chain's middle as a damped spring in the swinging frame, pushed by the pseudo force of the frame's
            // turn (-α r: it lags as the swing speeds up, runs on as it slows) and trailing behind the motion.
            float mid = length * .5f;
            float w = 2f * Mathf.PI * Mathf.Max(.1f, frequency);
            float target = -rate * mid * fromSpeed;
            int steps = Mathf.CeilToInt(dt / (1f / 120f));
            float h = dt / steps;
            float push = -shownAccel * mid * fromAcceleration;   // m/s²: the turn's pseudo force on the middle, scaled
            for (int s = 0; s < steps; s++)
            {
                float a = push - w * w * (bow - target) - 2f * damping * w * bowRate;
                bowRate += a * h;
                bow += bowRate * h;
            }
            if (Mathf.Abs(bow) > maxBow)
            {
                bow = Mathf.Sign(bow) * maxBow;
                bowRate *= .5f;
            }
            Pose();
            Sound();
        }

        // Each link on the bowed line pin -> eye (sin(π s): the ends stay on the pin and the eye), turned along it.
        void Pose()
        {
            // Square to the chain in the swing's plane: the Pivot turns about its z, so the swing moves along z x down.
            var across = Vector3.Cross(Vector3.forward, axisDown).normalized;   // +x for a chain hanging down -y
            var along = axisDown;
            float spin = Mathf.Clamp(bowRate * 25f, -twist, twist);
            for (int i = 0; i < links.Length; i++)
            {
                var link = links[i];
                if (link == null) continue;
                var c = rest[i];
                float s = Mathf.Clamp01(Vector3.Dot(c, along) / length);
                float offset = bow * Mathf.Sin(Mathf.PI * s);
                float slope = bow * Mathf.PI * Mathf.Cos(Mathf.PI * s) / length;
                // AngleAxis about Cross(a, b) turns a towards b: the link's length leans towards the bow.
                var turn = Quaternion.AngleAxis(Mathf.Atan(slope) * Mathf.Rad2Deg, Vector3.Cross(along, across).normalized)
                         * Quaternion.AngleAxis(spin * Mathf.Sin(Mathf.PI * s) * ((i & 1) == 0 ? 1f : -1f), along);
                // The holder sits on the Pivot with the link's mesh about the Pivot's origin: turn it about the link's
                // middle, then carry that middle out by the bow.
                link.localRotation = turn * restRotation[i];
                link.localPosition = c + across * offset - turn * c;
            }
        }

        TrailRenderer MakeTrail()
        {
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null) return null;
            var tex = new Texture2D(4, 32, TextureFormat.RGBA32, false) { name = "Hammer trail", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[4 * 32];
            for (int y = 0; y < 32; y++)
            {
                float v = Mathf.Sin(Mathf.PI * (y + .5f) / 32f);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1f, 1f, 1f, v * v);
            }
            tex.SetPixels(px);
            tex.Apply();
            var material = LitMaterials.NewAdditiveParticles(tex, Color.white);
            if (material == null) return null;
            material.hideFlags = HideFlags.DontSave;
            material.name = "Hammer swing trail";

            var go = new GameObject("Hammer swing trail") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(ball, false);
            var r = go.AddComponent<TrailRenderer>();
            r.sharedMaterial = material;
            r.time = trailTime;
            r.minVertexDistance = .08f;
            r.widthMultiplier = ballRadius * 1.6f;
            r.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .2f));
            r.numCapVertices = 0;
            r.textureMode = LineTextureMode.Stretch;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(trailColor.a, .12f), new GradientAlphaKey(0f, 1f) });
            r.colorGradient = g;
            return r;
        }

        void OnDestroy()
        {
            if (ribbon == null) return;
            if (ribbon.sharedMaterial != null)
            {
                Destroy(ribbon.sharedMaterial.mainTexture);
                Destroy(ribbon.sharedMaterial);
            }
            Destroy(ribbon.gameObject);
        }
    }
}
