using UnityEngine;

namespace ChessFight.RagdollLab
{
    // How a giant answers what just happened on the board (GiantChessCourse.Land and the game's end).
    public enum GiantReaction { None, Pleased, Startled, Win, Lose }

    // One seated giant grandmaster of the giant chess course (seoungtest R687 R6): the rigged FBX built in Blender
    // (ArtSource/PawnRush/RoyalLibraryModels/rl_giant_model.py -> Resources/RoyalLibrary/Models/ChessRoom/RlGiant_A|B),
    // posed by code every frame from its rest pose in layers (승규 11-18 "거인 시스템", screen only, nothing networked):
    //   1. waiting (real time): rest with the hands on the armrests, think with the chin on a fist (concept D3), study
    //      the board leaning in with the hands on the knees, or look around; a new mood every 5 - 12 s, blended
    //   2. reaction (real time): pleased (rubs the hands, nods), startled (leans back, hands up), win (arms up),
    //      lose (slumps, a hand to the face)
    //   3. reach (game time, from the course): lean from the hips and turn the chest as far as the hand needs, the arm
    //      by two-bone IK with the shoulder (clavicle) taking CLAVICLE_SHARE of the swing, fingers curl to grip
    // and on top: the head turns to what it watches, the chest breathes, the eyes blink.
    // rl_giant_model.py (arm_aim, CLAVICLE_SHARE) and rl_giant_acting_stills.py preview the same poses in Blender.
    public sealed class GiantGrandmaster
    {
        const float MaxLean = 50, MaxTurn = 40, ClavicleShare = .3f, MaxHeadTurn = 65;
        enum Mood { Rest, Think, Study, LookAround }
        public readonly Transform root;
        readonly Transform hips, spine, chest, neck, head;
        readonly Transform[] shoulder = new Transform[2], upper = new Transform[2], fore = new Transform[2], hand = new Transform[2], fingers = new Transform[2], thumb = new Transform[2];
        readonly Transform[] knee = new Transform[2];
        readonly Transform[] posed;
        readonly Quaternion[] rest;
        readonly Vector3[] gripLocal = new Vector3[2];
        Vector3 headCentreLocal, headFwdLocal, headUpLocal;
        const float HeadRadius = 8.73f;      // the head sphere (rl_giant_model.py HEAD_R), its centre 8.4 m above and .9 m in front of the head bone
        readonly float[] upperLength = new float[2], lowerLength = new float[2];
        readonly Renderer[] eyes;
        readonly Vector3 restForward;      // horizontal, world, the way the giant faces
        readonly System.Random random;
        // reach (from the course, game time)
        Vector3 reachWanted, reachTarget;
        float reachWeightWanted, reachWeight, gripWanted, grip;
        int reachArm = -1;
        // watching (from the course) and the head
        Vector3 watchPoint, lookPoint, wanderPoint;
        float watchWeight, wanderUntil;
        // moods (real time)
        Mood mood = Mood.Rest;
        int chinArm;
        float clock, moodUntil, blinkAt;
        readonly float[] moodWeight = new float[4];
        // reaction (real time)
        GiantReaction reaction;
        float reactionClock, reactionLength;

        GiantGrandmaster(Transform root, int seed)
        {
            this.root = root;
            random = new System.Random(seed);
            hips = Find(root, "hips"); spine = Find(root, "spine"); chest = Find(root, "chest"); neck = Find(root, "neck"); head = Find(root, "head");
            for (int i = 0; i < 2; i++)
            {
                string t = i == 0 ? "L" : "R";
                shoulder[i] = Find(root, "shoulder." + t); upper[i] = Find(root, "upperarm." + t); fore[i] = Find(root, "forearm." + t);
                hand[i] = Find(root, "hand." + t); fingers[i] = Find(root, "fingers." + t); thumb[i] = Find(root, "thumb." + t);
                knee[i] = Find(root, "shin." + t);
            }
            posed = new[] { hips, spine, chest, neck, head, shoulder[0], upper[0], fore[0], hand[0], fingers[0], thumb[0],
                shoulder[1], upper[1], fore[1], hand[1], fingers[1], thumb[1] };
            rest = new Quaternion[posed.Length];
            var shinL = Find(root, "shin.L"); var thighL = Find(root, "thigh.L");
            restForward = shinL && thighL ? Vector3.ProjectOnPlane(shinL.position - thighL.position, Vector3.up).normalized : root.forward;
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>()) if (r.name.Contains("GiantEye")) found.Add(r);
            eyes = found.ToArray();
        }

        public bool Valid => hips && spine && chest && head && knee[0] && knee[1] && upper[0] && upper[1] && fore[0] && fore[1] && hand[0] && hand[1] && fingers[0] && fingers[1];

        // Loads Resources/RoyalLibrary/Models/ChessRoom/<model>, seats it (the model's origin is the chair seat's top, centre)
        // at seatLocal in parent, faces it at lookLocal and dresses its slots. Null when the model is missing.
        public static GiantGrandmaster Load(string model, Transform parent, Vector3 seatLocal, Vector3 lookLocal, Material robe, Material inside,
            Material gold, Material skin, Material ink, Material blush, Material face)
        {
            var prefab = Resources.Load<GameObject>("RoyalLibrary/Models/ChessRoom/" + model);
            if (!prefab) return null;
            var go = Object.Instantiate(prefab, parent, false); go.name = "GM " + model;
            go.transform.localPosition = seatLocal;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var slots = r.sharedMaterials;
                for (int j = 0; j < slots.Length; j++)
                {
                    string n = slots[j] ? slots[j].name : "";
                    slots[j] = n.Contains("GM_RobeInside") ? inside : n.Contains("GM_Robe") ? robe : n.Contains("GM_Gold") ? gold : n.Contains("GM_Ink") ? ink : n.Contains("GM_Blush") ? blush : n.Contains("GM_Face") ? face : skin;
                }
                r.sharedMaterials = slots; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;      // bounds follow the lean and the reach
            }
            int seed = model.GetHashCode();
            var giant = new GiantGrandmaster(go.transform, seed);
            if (!giant.Valid) { Debug.LogError("[GiantGrandmaster] " + model + ": bones missing.", go); Object.Destroy(go); return null; }
            // The head bone stands 32 m above the seat in the model; guard against a centimetre import.
            float h = giant.head.position.y - go.transform.position.y;
            if (h > .01f && Mathf.Abs(h / 32.1f - 1) > .2f) go.transform.localScale *= 32.1f / h;
            var look = Vector3.ProjectOnPlane(parent.TransformPoint(lookLocal) - go.transform.position, Vector3.up);
            var turn = Quaternion.FromToRotation(giant.restForward, look.normalized);
            go.transform.rotation = turn * go.transform.rotation;
            // LOD (rl_giant_model.py MERGE / LOD1_RATIO, the importer builds the group from the _LOD0 / _LOD1 names): the
            // half-detail meshes once the giant (about 68 m tall) is under 45 % of the screen's height, i.e. the far giant
            // seen from the other side of the board; never culled.
            var lodGroup = go.GetComponentInChildren<LODGroup>();
            if (lodGroup)
            {
                var lods = lodGroup.GetLODs();
                if (lods.Length >= 2)
                {
                    lods[0].screenRelativeTransitionHeight = .45f;
                    for (int i = 1; i < lods.Length; i++) lods[i].screenRelativeTransitionHeight = i == lods.Length - 1 ? .0005f : .45f / (i + 1);
                    lodGroup.SetLODs(lods);
                }
                lodGroup.RecalculateBounds();
            }
            return new GiantGrandmaster(go.transform, seed).Init(parent.TransformPoint(lookLocal));
        }

        GiantGrandmaster Init(Vector3 boardCentre)
        {
            for (int i = 0; i < posed.Length; i++) if (posed[i]) rest[i] = posed[i].localRotation;
            for (int i = 0; i < 2; i++)
            {
                // The grip: under the palm, a little short of the knuckles.
                Vector3 g = Vector3.Lerp(hand[i].position, fingers[i].position, .8f) + Vector3.down * 1.4f * root.lossyScale.y;
                gripLocal[i] = hand[i].InverseTransformPoint(g);
                upperLength[i] = Vector3.Distance(upper[i].position, fore[i].position);
                lowerLength[i] = Vector3.Distance(fore[i].position, g);
            }
            reachTarget = Grip(0);
            headCentreLocal = head.InverseTransformPoint(head.position + (Vector3.up * 8.4f + restForward * .9f) * Scale);
            headFwdLocal = head.InverseTransformDirection(restForward); headUpLocal = head.InverseTransformDirection(Vector3.up);
            lookPoint = wanderPoint = this.boardCentre = boardCentre;
            moodUntil = 2 + (float)random.NextDouble() * 4; blinkAt = 1 + (float)random.NextDouble() * 3;
            return this;
        }

        Vector3 boardCentre;
        float Scale => root.lossyScale.y;
        float Range(float a, float b) => a + (b - a) * (float)random.NextDouble();
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        public Vector3 Grip(int i) => hand[i].TransformPoint(gripLocal[i]);
        // The grip of the arm that reaches (or the left one at rest).
        public Vector3 Grip() => Grip(reachArm < 0 ? 0 : reachArm);
        public Vector3 RestGrip(Vector3 toward) => Grip(Nearer(toward));

        int Nearer(Vector3 p) => (p - upper[0].position).sqrMagnitude <= (p - upper[1].position).sqrMagnitude ? 0 : 1;

        // The shoulder of arm i when the body leans fully toward p (rest pose measured, no turn).
        Vector3 LeanedShoulder(int i, Vector3 p)
        {
            Vector3 dir = Vector3.ProjectOnPlane(p - hips.position, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) dir = restForward;
            var q = Quaternion.AngleAxis(MaxLean, Vector3.Cross(Vector3.up, dir.normalized));
            return hips.position + q * (upper[i].position - hips.position);
        }

        float Reach(int i) => (upperLength[i] + lowerLength[i]) * .96f;

        public bool Reachable(Vector3 p)
        {
            for (int i = 0; i < 2; i++) if (Vector3.Distance(p, LeanedShoulder(i, p)) <= Reach(i)) return true;
            return false;
        }

        // The nearest point toward p the hand can get to (for pointing and throwing at squares out of reach).
        public Vector3 Toward(Vector3 p)
        {
            int i = Nearer(p); Vector3 s = LeanedShoulder(i, p), d = p - s;
            return s + Vector3.ClampMagnitude(d, Reach(i) * .97f);
        }

        // Every frame, from the course: where the working hand goes (weight 0 = no reach) and how tight it grips.
        public void SetReach(Vector3 target, float weight, float gripAmount) { reachWanted = target; reachWeightWanted = weight; gripWanted = gripAmount; }
        // Every frame, from the course: what to look at (weight 0 = nothing in particular; the giant looks around).
        public void Watch(Vector3 point, float weight) { watchPoint = point; watchWeight = weight; }

        public void React(GiantReaction r)
        {
            if (r == GiantReaction.None) return;
            reaction = r; reactionClock = 0;
            reactionLength = r == GiantReaction.Pleased ? 1.8f : r == GiantReaction.Startled ? 1.5f : 6f;
        }

        // dtGame follows the replay (reach), dtReal the screen (moods, reactions, the head); both 0 while paused.
        public void Tick(float dtGame, float dtReal, float follow)
        {
            clock += dtReal;
            // reach, smoothed in game time
            float k = 1 - Mathf.Exp(-follow * dtGame);
            if (reachWeight < .05f && reachWeightWanted > 0) { reachArm = Nearer(reachWanted); reachTarget = Grip(reachArm); }
            if (reachArm < 0) reachArm = Nearer(reachWanted);
            reachTarget = Vector3.Lerp(reachTarget, reachWanted, k);
            reachWeight = Mathf.Lerp(reachWeight, reachWeightWanted, k);
            grip = Mathf.Lerp(grip, gripWanted, k);

            // moods: a new one now and then, blended over about a second
            if (clock >= moodUntil)
            {
                float roll = (float)random.NextDouble();
                mood = roll < .3f ? Mood.Rest : roll < .62f ? Mood.Think : roll < .82f ? Mood.Study : Mood.LookAround;
                chinArm = random.Next(2);
                moodUntil = clock + Range(5, 12);
            }
            float km = 1 - Mathf.Exp(-2.2f * dtReal);
            for (int m = 0; m < 4; m++) moodWeight[m] = Mathf.Lerp(moodWeight[m], (int)mood == m ? 1 : 0, km);
            if (clock >= wanderUntil)
            {
                // look at a different part of the board now and then
                wanderPoint = boardCentre + new Vector3(Range(-30, 30), Range(0, 8), Range(-30, 30)) * Scale;
                wanderUntil = clock + Range(1.5f, 4f) * (mood == Mood.LookAround ? .6f : 1.4f);
            }
            if (eyes.Length > 0)
            {
                bool shut = clock >= blinkAt && clock < blinkAt + .13f;
                foreach (var e in eyes) if (e) e.enabled = !shut;
                if (clock >= blinkAt + .13f) blinkAt = clock + Range(2.5f, 6f);
            }

            // reaction envelope
            float rt = 0, env = 0;
            if (reaction != GiantReaction.None)
            {
                reactionClock += dtReal; rt = reactionClock;
                env = Smooth(rt / .25f) * (1 - Smooth((rt - reactionLength + .6f) / .6f));
                if (rt >= reactionLength) reaction = GiantReaction.None;
            }
            float pleased = reaction == GiantReaction.Pleased ? env : 0, startled = reaction == GiantReaction.Startled ? env : 0;
            float win = reaction == GiantReaction.Win ? env : 0, lose = reaction == GiantReaction.Lose ? env : 0;
            float acting = Mathf.Max(Mathf.Max(pleased, startled), Mathf.Max(win, lose));

            for (int i = 0; i < posed.Length; i++) if (posed[i]) posed[i].localRotation = rest[i];

            // ---- the torso: lean, turn, breathe
            float think = moodWeight[(int)Mood.Think], study = moodWeight[(int)Mood.Study];
            float idleLean = think * 16 + study * 20;
            float moodLean = idleLean * (1 - acting) + pleased * -5 + startled * -12 + win * -9 + lose * 18;
            float breath = Mathf.Sin(clock * Mathf.PI * 2 / 4.6f) * 1.1f;
            Vector3 reachFlat = Vector3.ProjectOnPlane(reachTarget - hips.position, Vector3.up);
            Vector3 reachDir = reachFlat.sqrMagnitude > 1e-4f ? reachFlat.normalized : restForward;
            float wr = reachWeight;
            Vector3 dir = Vector3.Slerp(restForward, reachDir, wr);
            float yaw = Mathf.Clamp(Vector3.SignedAngle(restForward, reachDir, Vector3.up), -MaxTurn, MaxTurn) * wr;
            Vector3 axis = Vector3.Cross(Vector3.up, dir).normalized;
            float lean = 0;
            for (int step = 0; step <= 20; step++)
            {
                lean = Mathf.Lerp(moodLean, Mathf.Max(moodLean, MaxLean * step / 20f), wr) + breath;
                ApplyLean(axis, lean, yaw);
                if (wr < .01f || Vector3.Distance(reachTarget, upper[reachArm].position) <= Reach(reachArm)) break;
            }

            // ---- the head: watch what the course points at, else look around; reactions on top
            Vector3 wanted = Vector3.Lerp(wanderPoint, watchPoint, watchWeight);
            if (wr > .01f) wanted = Vector3.Lerp(wanted, reachTarget, wr * .7f);
            lookPoint = Vector3.Lerp(lookPoint, wanted, 1 - Mathf.Exp(-3.5f * dtReal));
            Vector3 faceDir = Quaternion.AngleAxis(lean, axis) * Quaternion.AngleAxis(yaw, Vector3.up) * restForward;
            Vector3 toLook = (lookPoint - head.position).normalized;
            float headTurn = Mathf.Min(Vector3.Angle(faceDir, toLook), MaxHeadTurn) / Mathf.Max(Vector3.Angle(faceDir, toLook), .01f);
            float follow2 = .75f * (1 - acting * .5f);
            head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(faceDir, toLook), headTurn * follow2) * head.rotation;
            Vector3 headSide = Vector3.Cross(Vector3.up, faceDir).normalized;
            float nod = pleased * Mathf.Sin(rt * 11) * 7 + think * 6 - startled * 12 - win * 14 + lose * 22;
            head.rotation = Quaternion.AngleAxis(nod, headSide) * Quaternion.AngleAxis(think * (chinArm == 0 ? 7 : -7), faceDir) * head.rotation;

            // ---- the arms: mood and reaction targets, then the reach on its arm
            Vector3 fwd = faceDir, up = Vector3.up;
            Vector3 hc = head.TransformPoint(headCentreLocal), hf = head.TransformDirection(headFwdLocal), hu = head.TransformDirection(headUpLocal);
            Vector3 chinPoint = hc + (hf * .62f - hu * .78f).normalized * HeadRadius * Scale;
            Vector3 browPoint = hc + (hf * .9f + hu * .3f).normalized * HeadRadius * Scale;
            for (int i = 0; i < 2; i++)
            {
                Vector3 outward = Vector3.ProjectOnPlane(upper[i].position - chest.position, Vector3.up).normalized;
                Vector3 t = Vector3.zero; float w = 0, c = 0;
                void Add(Vector3 p, float weight, float curlAmount) { if (weight <= 0) return; t += p * weight; w += weight; c += curlAmount * weight; }
                // think: the fist under the chin, the other hand stays on the armrest
                float chinWeight = think * (i == chinArm ? 1 : 0) * (1 - acting);
                Add(chinPoint + ChinGrip(hf, hu) * Scale, chinWeight, 1);
                // study: the hands on the knees
                Add(knee[i].position + up * KneeTop * Scale - fwd * 1.5f * Scale, study * .85f * (1 - acting), .35f);
                // reactions
                Vector3 front = chest.position + fwd * 8 * Scale;
                Add(front + Vector3.down * 3 * Scale + outward * (1.4f + Mathf.Sin(rt * 14 + i * 3) * .6f) * Scale, pleased, .3f);
                Add(upper[i].position + fwd * 6 * Scale + up * 3.5f * Scale + outward * 2 * Scale, startled * .85f, 0);
                Add(upper[i].position + up * (18 + Mathf.Sin(rt * 7) * 1.5f) * Scale + outward * 7 * Scale, win, 1);
                Add(browPoint + BrowGrip(hf, hu) * Scale, lose * (i == 1 ? 1 : 0), 0);
                Vector3 target = w > 0 ? t / w : Grip(i);
                float weight = Mathf.Clamp01(w), curlAmount = w > 0 ? c / w : 0;
                if (i == reachArm && wr > 0)
                {
                    target = w > 0 ? Vector3.Lerp(target, reachTarget, wr) : reachTarget;
                    weight = Mathf.Lerp(weight, 1, wr); curlAmount = Mathf.Lerp(curlAmount, grip, wr);
                }
                // the elbow: out and down, but forward onto the knee for the chin rest
                Vector3 pole = Vector3.Lerp(outward * .7f + Vector3.down * .7f - dir * .2f, fwd * .5f + Vector3.down * .85f + outward * .2f, w > 0 ? chinWeight / w : 0);
                if (weight > .002f) SolveArm(i, target, weight, pole);
                if (curlAmount > .002f) Curl(i, curlAmount);
            }
        }

        // Where the grip point (under the palm) sits for the chin rest and the hand on the brow, from the face point;
        // tuned in Blender (rl_giant_acting_stills.py).
        const float KneeTop = 4.9f;
        static Vector3 ChinGrip(Vector3 hf, Vector3 hu) => hf * 1.6f - hu * 2.4f;
        static Vector3 BrowGrip(Vector3 hf, Vector3 hu) => hf * 2.2f - hu * .4f;

        void SolveArm(int i, Vector3 target, float weight, Vector3 pole)
        {
            // the clavicle takes a share of the swing toward the target first (no pinch at the back of the armpit)
            Vector3 s0 = upper[i].position, restUpper = fore[i].position - s0;
            Vector3 goal0 = Vector3.Lerp(Grip(i), target, weight);
            if (shoulder[i] && (goal0 - s0).sqrMagnitude > 1e-4f)
                shoulder[i].rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(restUpper, goal0 - s0), ClavicleShare * weight) * shoulder[i].rotation;
            // two-bone IK: shoulder S, elbow E, grip G; the elbow bends out from the body and down
            Vector3 s = upper[i].position, goal = Vector3.Lerp(Grip(i), target, weight);
            float a = upperLength[i], b = lowerLength[i];
            Vector3 toGoal = goal - s;
            float d = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(a - b) + .01f, a + b - .01f);
            Vector3 n = toGoal.sqrMagnitude > 1e-6f ? toGoal.normalized : restForward;
            Vector3 perp = Vector3.ProjectOnPlane(pole, n);
            perp = perp.sqrMagnitude > 1e-6f ? perp.normalized : Vector3.down;
            float x = (a * a - b * b + d * d) / (2 * d), h = Mathf.Sqrt(Mathf.Max(a * a - x * x, 0));
            Vector3 elbow = s + n * x + perp * h;
            upper[i].rotation = Quaternion.FromToRotation(fore[i].position - s, elbow - s) * upper[i].rotation;
            Vector3 e = fore[i].position;
            fore[i].rotation = Quaternion.FromToRotation(Grip(i) - e, s + n * d - e) * fore[i].rotation;
        }

        void Curl(int i, float amount)
        {
            // fingers curl down over the piece (or into a fist), the thumb closes toward them
            Vector3 f = fingers[i].position - hand[i].position;
            Vector3 lat = Vector3.Cross(f, Vector3.up);
            if (lat.sqrMagnitude > 1e-6f) fingers[i].rotation = Quaternion.AngleAxis(-60 * amount, lat.normalized) * fingers[i].rotation;
            if (thumb[i])
            {
                Vector3 tdir = thumb[i].rotation * Vector3.up, close = Vector3.Cross(tdir, f);
                if (close.sqrMagnitude > 1e-6f) thumb[i].rotation = Quaternion.AngleAxis(30 * amount, close.normalized) * thumb[i].rotation;
            }
        }

        void ApplyLean(Vector3 axis, float lean, float yaw)
        {
            spine.localRotation = rest[1]; chest.localRotation = rest[2];
            spine.rotation = Quaternion.AngleAxis(lean * .45f, axis) * Quaternion.AngleAxis(yaw * .4f, Vector3.up) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(lean * .55f, axis) * Quaternion.AngleAxis(yaw * .6f, Vector3.up) * chest.rotation;
        }

        static string Key(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        static Transform Find(Transform t, string name)
        {
            // Blender bone names ("upperarm.L") may come through as "upperarm_L"; compare letters and digits only.
            string key = Key(name);
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (Key(c.name) == key) return c;
            return null;
        }
    }
}
