using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// One of the bishop's two thrown swords on its way out (R181, 10-28, F5 of his X cut; reference F_03_flight.png):
    /// round the sword, where it spins flat, a blur disc (the child <see cref="DiscName"/>, a quad with the VFX_SpinBlurAdd
    /// material laid in the plane it spins in, its streaks brightest right behind the blade), and behind it a trail along
    /// the way it came (a LineRenderer on this object with the VFX_TrailAdd material, <see cref="length"/> long, bright at
    /// the sword, leaving the disc at its rim on the outer side of the curve, <see cref="attach"/>). The sword itself is
    /// the game's (SwordFightPawn's drawn copy); this only follows it: in the game
    /// <see cref="Follow"/> hands it where the copy is each frame until it turns into the X at <see cref="outTime"/>;
    /// with nothing to follow (the workbench, the capture tool) it flies the game's way out over <see cref="reach"/>
    /// itself. The disc goes as the sword turns into its cut, the trail dies away into the sword as the X begins. This
    /// object sits unturned at its parent's origin, the parent's +Z the bishop's way and +X his right.
    /// With <see cref="back"/> (R186, F8 of the X cut; reference F_06_return.png) the same parts follow the sword on its
    /// way back to his hand instead: <see cref="outTime"/> is then the way back, the preview flies the game's way back,
    /// and the trail (no disc) is thinner and fainter and sways (<see cref="waver"/>); it dies away into the sword as it
    /// drops into his hand.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DefaultExecutionOrder(120)]   // after the pieces are drawn (RagdollVisualSync, 100), when the game has placed its sword
    public sealed class VfxSwordFlight : MonoBehaviour, IVfxSeekable
    {
        /// <summary>Where the followed sword is now, in the world: its middle (it spins about it) and the way its blade
        /// points. False while it is not on its way out.</summary>
        public delegate bool Source(out Vector3 middle, out Vector3 blade);

        public const string DiscName = "Disc";
        const int Points = 24, Kept = 96;
        /// <summary>The preview's step along its own way (s).</summary>
        const float Step = 1f / 120f;
        /// <summary>The game's sword, for the preview's way: half its length (SwordFightSkills.BishopSwordHalf) and the height
        /// over the floor the two cross at (SwordFightSkillParams.bishopCrossHeight).</summary>
        const float Half = .38f, CrossHeight = .55f;
        /// <summary>How fast the trail's sway runs back along it (radians a second).</summary>
        const float Sway = 9f;
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity"),
            TurnId = Shader.PropertyToID("_Turn"), DirId = Shader.PropertyToID("_Dir");

        [Tooltip("0 his right sword (out to +X), 1 his left.")]
        public int hand;
        [Min(.05f), Tooltip("Seconds from his hand to the X. The game hands its own (the distance / 14 m/s, 0.28 to 0.6).")]
        public float outTime = .3f;
        [Tooltip("Metres ahead the two cross, for the preview's own way (the game: 0.8 m short of whoever he aims at).")]
        public float reach = 4.2f;
        [Tooltip("Turns a second the preview's sword spins (SwordFightSkillParams.bishopSpin; on the way back the game spins 0.8 of it, slowing).")]
        public float spin = 6f;
        [Tooltip("On its way back to his hand (F8) instead of out to the X: outTime is then the way back (SwordFightSkillParams.bishopBackTime) and the preview flies the game's way back.")]
        public bool back;

        [Header("Blur disc")]
        [Tooltip("Its width (m): the sword's sweep, a little over its length.")]
        public float discSize = .95f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float discIntensity = 1.8f;
        [Tooltip("Seconds it takes to come up after the throw.")]
        public float discIn = .04f;
        [Tooltip("Share of the way out over which it goes, as the sword turns into its cut.")]
        public Vector2 discOut = new Vector2(.72f, .95f);

        [Header("Trail")]
        [Tooltip("Metres back along the way it reaches.")]
        public float length = 2.4f;
        [Tooltip("Width (m) at the sword.")]
        public float width = .32f;
        [Range(0f, 1f), Tooltip("Where it leaves the disc: out to the outer side of its curve, in the plane the disc spins in, as a share of the disc's radius (0 its middle).")]
        public float attach = .66f;
        [Range(0f, 1f), Tooltip("Its width at the far end, as a share of the width at the sword.")]
        public float endWidth = .15f;
        [Tooltip("HDR multiplier (Docs/VFX/README.md §6).")]
        public float intensity = 2.2f;
        [Tooltip("Seconds it takes to die away, drawing in to the sword, once the sword turns into the X (or drops into his hand).")]
        public float fadeOut = .1f;
        [Tooltip("How far (m) it sways from side to side at its far end, none at the sword (0: it does not sway).")]
        public float waver;
        [Tooltip("Metres along it from one sway to the next.")]
        public float waverLength = .8f;

        readonly Vector3[] kept = new Vector3[Kept];
        readonly float[] keptAt = new float[Kept];
        readonly Vector3[] points = new Vector3[Points];
        readonly Vector3[] sides = new Vector3[Points];
        int count;
        LineRenderer line;
        Transform disc;
        Renderer discView;
        MaterialPropertyBlock block;
        Source source;
        Vector3 blade = Vector3.forward;
        float age;

        float Sign => hand == 0 ? 1f : -1f;

        /// <summary>The way it spins about, in this object's frame: up, leaning out to its own side (the game's whirl).</summary>
        Vector3 Axis => (Vector3.up + Vector3.right * (.35f * Sign)).normalized;

        void OnEnable()
        {
            age = 0f;
            count = 0;
            Shape();
            Draw();
        }

        /// <summary>From now on follows the game's sword through <paramref name="where"/>, out over
        /// <paramref name="seconds"/> (the game: SwordFightSkills.BishopOutTime; with <see cref="back"/>, until it is back in
        /// his hand).</summary>
        public void Follow(Source where, float seconds)
        {
            source = where;
            outTime = Mathf.Max(.05f, seconds);
            age = 0f;
            count = 0;
            Pull();
            Draw();
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (source != null) Pull();
            else Fly(age);
            Draw();
        }

        /// <summary>As it is <paramref name="at"/> seconds after the throw, on its own way (the capture tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            count = 0;
            float end = Mathf.Min(at, outTime);
            for (float t = 0f; t < end; t += Step) Fly(t);
            if (end >= 0f) Fly(end);
            Shape();
            Draw();
        }

        /// <summary>Where the game's sword is now, kept while it is on its way out.</summary>
        void Pull()
        {
            if (age > outTime) return;
            if (!source(out Vector3 middle, out Vector3 toward)) return;
            Keep(transform.InverseTransformPoint(middle), age);
            blade = transform.InverseTransformDirection(toward);
        }

        /// <summary>
        /// The preview's own way at <paramref name="t"/> seconds, kept: the game's way out (SwordFightSkills.BishopSwordPose)
        /// for a throw of <see cref="reach"/> from hands held back over his shoulders, out wide to its own side on a curve
        /// and in to just over where the two cross, whirling flat <see cref="spin"/> turns a second.
        /// </summary>
        void Fly(float t)
        {
            if (t > outTime && count > 0) return;
            t = Mathf.Clamp(t, 0f, outTime);
            if (back)
            {
                FlyBack(t);
                return;
            }
            float sgn = Sign;
            Vector3 up = Vector3.up, f = Vector3.forward, side = Vector3.right;
            Vector3 s0 = f * reach + up * CrossHeight + (side * sgn + up).normalized * .45f;
            Vector3 p0 = new Vector3(.3f * sgn, 1.15f, -.1f) + f * Half;
            Vector3 flat = s0 - p0;
            flat.y = 0f;
            float d = flat.magnitude;
            Vector3 p1 = p0 + f * (.3f * d) + side * (sgn * (.45f + .22f * d)) + up * .35f;
            Vector3 p2 = s0 + side * (sgn * (.35f + .1f * d)) - f * (.15f * d) + up * .25f;
            float e = 1f - Mathf.Pow(1f - t / outTime, 1.6f), o = 1f - e;
            Keep(o * o * o * p0 + 3f * o * o * e * p1 + 3f * o * e * e * p2 + e * e * e * s0, t);
            blade = Quaternion.AngleAxis(360f * spin * t * sgn, Axis) * f;
        }

        /// <summary>
        /// The preview's own way back at <paramref name="t"/> seconds, kept: the game's way back (SwordFightSkills.BishopSwordPose)
        /// from the end of its cut, low on the other side of the X, on a loose curve of its own (the game's random curve for a
        /// bishop standing at the world's middle) to his hand held out in front of him, wobbling, whirling and slowing.
        /// </summary>
        void FlyBack(float t)
        {
            float sgn = Sign, u = t / outTime, e = Mathf.SmoothStep(0f, 1f, u), o = 1f - e;
            Vector3 up = Vector3.up, f = Vector3.forward, side = Vector3.right;
            Vector3 path = (side * sgn + up).normalized;
            Vector3 s1 = f * reach + up * CrossHeight - path * .45f;
            Vector3 catchBlade = (f * .6f + up * .55f + side * (.35f * sgn)).normalized;
            Vector3 handMid = new Vector3(.28f * sgn, 1f, .25f) + catchBlade * Half;
            int seed = (hand + 1) * 83492791;
            Vector3 q1 = s1 - path * .9f - f * .2f + HashDir(seed, 0) * 1.1f;
            q1.y = Mathf.Max(q1.y, .4f);
            Vector3 behind = -f * (.4f + .8f * Hash01(seed, 3)) + side * (sgn * (.3f + .9f * Hash01(seed, 4)));
            Vector3 q2 = handMid + behind + up * (.5f + .7f * Hash01(seed, 5)) + HashDir(seed, 6) * .5f;
            q2.y = Mathf.Max(q2.y, .4f);
            Vector3 mid = o * o * o * s1 + 3f * o * o * e * q1 + 3f * o * e * e * q2 + e * e * e * handMid;
            Vector3 wob = Vector3.Cross(up, (handMid - s1).normalized);
            mid += wob * (.18f * Mathf.Sin(u * Mathf.PI * (2f + Hash01(seed, 9)) + Hash01(seed, 10) * 6.28f) * Mathf.Sin(Mathf.PI * u));
            Keep(mid, t);
            blade = Quaternion.AngleAxis(360f * spin * t * sgn * (1f - .45f * u), (up + HashDir(seed, 11) * .5f).normalized) * f;
        }

        /// <summary>The game's number in 0..1 from a seed (SwordFightSkills.Hash01), for the preview's way back.</summary>
        static float Hash01(int seed, int k)
        {
            float x = Mathf.Sin(seed * .0001237f + k * 78.233f + seed % 9973 * .731f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        static Vector3 HashDir(int seed, int k) =>
            new Vector3(Hash01(seed, k) * 2f - 1f, Hash01(seed, k + 1) * 2f - 1f, Hash01(seed, k + 2) * 2f - 1f);

        /// <summary>Keeps a place on the way at <paramref name="t"/> (one at the same time is replaced; the oldest goes
        /// when they are all used).</summary>
        void Keep(Vector3 at, float t)
        {
            if (count > 0 && t <= keptAt[count - 1])
            {
                kept[count - 1] = at;
                return;
            }
            if (count == Kept)
            {
                System.Array.Copy(kept, 1, kept, 0, Kept - 1);
                System.Array.Copy(keptAt, 1, keptAt, 0, Kept - 1);
                count--;
            }
            kept[count] = at;
            keptAt[count] = t;
            count++;
        }

        /// <summary>The trail narrows from the sword to <see cref="endWidth"/> at its far end.</summary>
        void Shape()
        {
            if (!Ready()) return;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, endWidth), new Keyframe(1f, 1f));
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears them (Docs/Environment/PITFALLS.md 53).
            if (line == null) line = GetComponent<LineRenderer>();
            if (disc == null)
            {
                disc = transform.Find(DiscName);
                discView = disc != null ? disc.GetComponent<Renderer>() : null;
            }
            if (block == null) block = new MaterialPropertyBlock();
            return line != null;
        }

        void Draw()
        {
            if (!Ready()) return;
            float u = age / outTime;
            float gone = age > outTime ? Mathf.Clamp01((age - outTime) / Mathf.Max(.01f, fadeOut)) : 0f;
            bool any = count > 0 && gone < 1f;

            // The disc, round the sword in the plane it spins in (its Z the axis, its Y his way), turned to the blade.
            float spinning = (discIn > 0f ? Mathf.Clamp01(age / discIn) : 1f)
                             * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(discOut.x, discOut.y, u)));
            if (discView != null)
            {
                discView.enabled = any && spinning > .001f;
                if (discView.enabled)
                {
                    var plane = Quaternion.LookRotation(Axis, Vector3.forward);
                    disc.localPosition = kept[count - 1];
                    disc.localRotation = plane;
                    disc.localScale = Vector3.one * discSize;
                    Vector3 b = Quaternion.Inverse(plane) * blade;
                    discView.GetPropertyBlock(block);
                    block.SetFloat(IntensityId, discIntensity);
                    block.SetFloat(FadeId, spinning);
                    block.SetFloat(TurnId, Mathf.Atan2(b.y, b.x) * Mathf.Rad2Deg);
                    block.SetFloat(DirId, Sign);
                    discView.SetPropertyBlock(block);
                }
            }

            // The trail, back along the way from the sword; it draws in to the sword as it dies away.
            line.enabled = any && Lay(length * (1f - gone));
            if (!line.enabled) return;
            line.positionCount = Points;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.GetPropertyBlock(block);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(FadeId, 1f - gone);
            line.SetPropertyBlock(block);
        }

        /// <summary>The trail's points, evenly along the way kept from <paramref name="back"/> metres behind the sword (or as
        /// far as it has come) to the sword, far end first, on a smooth curve through the places kept, moved out to the
        /// outer side of the curve by <see cref="attach"/> of the disc's radius. False with too little way to draw.</summary>
        bool Lay(float back)
        {
            if (count < 2 || back <= .01f) return false;
            float have = 0f;
            for (int i = count - 1; i > 0 && have < back; i--) have += Vector3.Distance(kept[i], kept[i - 1]);
            float drawn = Mathf.Min(have, back);
            if (drawn <= .01f) return false;
            // Walking back from the sword: the stretch from kept[seg] back to kept[seg - 1] starts done metres back.
            int seg = count - 1;
            float done = 0f, stretch = Vector3.Distance(kept[seg], kept[seg - 1]);
            for (int i = Points - 1; i >= 0; i--)
            {
                float s = drawn * (Points - 1 - i) / (Points - 1);
                while (s > done + stretch && seg > 1)
                {
                    done += stretch;
                    seg--;
                    stretch = Vector3.Distance(kept[seg], kept[seg - 1]);
                }
                float t = stretch > 1e-5f ? Mathf.Clamp01((s - done) / stretch) : 0f;
                points[i] = CatmullRom(At(seg + 1), kept[seg], kept[seg - 1], At(seg - 2), t);
            }

            // Out to the outer side of the curve (his right for his right sword), in the plane the disc spins in, so the
            // trail runs off the disc's rim the way the reference's does instead of out of its middle.
            float off = attach * discSize * .5f * Sign;
            if (Mathf.Abs(off) >= 1e-4f)
            {
                for (int i = 0; i < Points; i++)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, points[Mathf.Min(i + 1, Points - 1)] - points[Mathf.Max(i - 1, 0)]);
                    side -= Axis * Vector3.Dot(side, Axis);
                    sides[i] = side.sqrMagnitude > 1e-10f ? side.normalized * off : i > 0 ? sides[i - 1] : Vector3.zero;
                }
                for (int i = 0; i < Points; i++) points[i] += sides[i];
            }

            // Swaying from side to side, more toward the far end, the sway running back along it (the way back's trail).
            if (waver > 1e-4f)
            {
                float wave = 2f * Mathf.PI / Mathf.Max(.05f, waverLength);
                for (int i = 0; i < Points; i++)
                {
                    float s = drawn * (Points - 1 - i) / (Points - 1);
                    Vector3 across = Vector3.Cross(Vector3.up, points[Mathf.Min(i + 1, Points - 1)] - points[Mathf.Max(i - 1, 0)]);
                    sides[i] = across.sqrMagnitude > 1e-10f
                        ? across.normalized * (waver * (s / drawn) * Mathf.Sin(wave * s - age * Sway))
                        : Vector3.zero;
                }
                for (int i = 0; i < Points; i++) points[i] += sides[i];
            }
            return true;
        }

        Vector3 At(int i) => kept[Mathf.Clamp(i, 0, count - 1)];

        static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return .5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (3f * b - a - 3f * c + d) * t3);
        }
    }
}
