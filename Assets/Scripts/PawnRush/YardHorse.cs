using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // A royal horse for the Royal Carriage Yard's moving things (R626, out of R532's StableCharger): the stable horse,
    // the horses that pull the roaming carriages and the loose horses roaming the yard. Pictures only - whoever owns it
    // gives it one collider over all of it - nose to local +z, standing on y 0, Length x Width x Height. The body hangs
    // from a pivot over the hind legs, so it can rear; each leg from a pivot at its top, so it can stride; Pose moves
    // them, from numbers the owner reads off the shared clock (so every PC draws the same).
    public sealed class YardHorse
    {
        public const float Length = 2.6f, Width = .9f, Height = 1.95f;

        public enum Coat { Bay, Grey, Black }

        static Material CoatMaterial(Coat coat) =>
            coat == Coat.Grey ? Arena.Lit("M_YardHorseGrey", Arena.Hex("#D8D1C4"), 0f, .6f)
            : coat == Coat.Black ? Arena.Lit("M_YardHorseBlack", Arena.Hex("#2E2925"), 0f, .55f)
            : Arena.Lit("M_YardHorse", Arena.Hex("#7A4A2A"), 0f, .6f);
        static Material Mane(Coat coat) =>
            coat == Coat.Grey ? Arena.Lit("M_YardHorseManeGrey", Arena.Hex("#8E877C"), 0f, .8f)
            : Arena.Lit("M_YardHorseMane", Arena.Hex("#2B1B12"), 0f, .8f);
        static Material Hoof => Arena.Lit("M_YardHoof", Arena.Hex("#1E1A17"), 0f, .5f);

        public Transform Root { get; private set; }
        Transform body, neck;
        Transform[] legs;

        // Built under `parent` at `at` (local), nose to the parent's +z. `saddle`: a royal cloth on its back (a ridden
        // or stable horse); off, a harness collar (a carriage horse).
        public static YardHorse Build(Transform parent, Vector3 at, Coat coat = Coat.Bay, bool saddle = true, string name = "Horse")
        {
            var horse = new YardHorse();
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = at;
            horse.Root = root;
            const Solid none = Solid.None;
            var hide = CoatMaterial(coat);
            var mane = Mane(coat);
            var b = new GameObject("Body").transform;
            b.SetParent(root, false);
            b.localPosition = new Vector3(0f, 1.05f, -.75f);
            horse.body = b;
            // Everything below is placed from the standing horse's points, less the pivot.
            var p = new Vector3(0f, -1.05f, .75f);
            YardKit.Box(b, "Barrel", p + new Vector3(0f, 1.32f, 0f), new Vector3(.78f, .78f, 1.75f), hide, none);
            YardKit.Box(b, "Chest", p + new Vector3(0f, 1.36f, .9f), new Vector3(.74f, .72f, .4f), hide, none);
            YardKit.Box(b, "Rump", p + new Vector3(0f, 1.38f, -.85f), new Vector3(.8f, .72f, .45f), hide, none);
            if (saddle)
            {
                YardKit.Box(b, "Saddle cloth", p + new Vector3(0f, 1.62f, -.05f), new Vector3(.86f, .3f, .8f), YardKit.Banner, none);
                YardKit.Box(b, "Cloth trim", p + new Vector3(0f, 1.47f, -.05f), new Vector3(.88f, .06f, .84f), YardKit.Gold, none);
            }
            else
            {
                // A harness: a collar round the chest, a band round the barrel, gold buckles.
                YardKit.Box(b, "Collar", p + new Vector3(0f, 1.5f, .98f), new Vector3(.84f, .56f, .16f), YardKit.WoodDark, none);
                YardKit.Box(b, "Collar trim", p + new Vector3(0f, 1.5f, 1.07f), new Vector3(.86f, .1f, .04f), YardKit.Gold, none);
                YardKit.Box(b, "Girth", p + new Vector3(0f, 1.32f, .3f), new Vector3(.82f, .82f, .12f), YardKit.WoodDark, none);
                YardKit.Box(b, "Pad", p + new Vector3(0f, 1.74f, .3f), new Vector3(.4f, .1f, .34f), YardKit.Banner, none);
            }
            YardKit.Box(b, "Tail", p + new Vector3(0f, 1.25f, -1.2f), new Vector3(.16f, .6f, .16f), mane, none, Quaternion.Euler(-25f, 0f, 0f));
            var n = new GameObject("Neck").transform;
            n.SetParent(b, false);
            n.localPosition = p + new Vector3(0f, 1.5f, .95f);
            horse.neck = n;
            YardKit.Box(n, "Neck", new Vector3(0f, .32f, .15f), new Vector3(.38f, .8f, .42f), hide, none, Quaternion.Euler(28f, 0f, 0f));
            YardKit.Box(n, "Mane", new Vector3(0f, .42f, -.02f), new Vector3(.12f, .78f, .14f), mane, none, Quaternion.Euler(28f, 0f, 0f));
            YardKit.Box(n, "Head", new Vector3(0f, .72f, .52f), new Vector3(.32f, .34f, .68f), hide, none, Quaternion.Euler(18f, 0f, 0f));
            YardKit.Box(n, "Muzzle", new Vector3(0f, .6f, .84f), new Vector3(.28f, .24f, .2f), mane, none, Quaternion.Euler(18f, 0f, 0f));
            YardKit.Box(n, "Bridle", new Vector3(0f, .72f, .62f), new Vector3(.36f, .06f, .06f), YardKit.Gold, none);
            foreach (float side in new[] { -1f, 1f })
                YardKit.Box(n, "Ear", new Vector3(.09f * side, .94f, .32f), new Vector3(.07f, .16f, .07f), hide, none);
            YardKit.Box(n, "Plume", new Vector3(0f, 1.02f, .3f), new Vector3(.06f, .22f, .06f), YardKit.Banner, none);
            horse.legs = new Transform[4];
            int i = 0;
            foreach (float z in new[] { .72f, -.72f })
                foreach (float side in new[] { -1f, 1f })
                {
                    var pivot = new GameObject("Leg").transform;
                    pivot.SetParent(b, false);
                    pivot.localPosition = p + new Vector3(.24f * side, 1.0f, z);
                    YardKit.Box(pivot, "Leg", new Vector3(0f, -.45f, 0f), new Vector3(.16f, .9f, .18f), hide, none);
                    YardKit.Box(pivot, "Hoof", new Vector3(0f, -.94f, .02f), new Vector3(.2f, .12f, .22f), Hoof, none);
                    horse.legs[i++] = pivot;
                }
            return horse;
        }

        // How far into a rear (0 standing .. 1 up on its hind legs and down again) from how far into a warning (0..1),
        // as the stable horse rears: up in the first 0.3 of it, down over the last quarter.
        public static float Rear(float warned) =>
            warned < 0f ? 0f : Smooth(warned / .3f) * (1f - Smooth((warned - .75f) / .25f));

        // Rearing by `rear` (0..1), a stride with the distance `travelled` (m) at `speed` of `topSpeed` m/s, still when
        // it stands; `now` (the shared clock) shakes the head while it rears.
        public void Pose(double now, float rear, float speed, float topSpeed, float travelled)
        {
            if (body == null) return;
            body.localRotation = Quaternion.Euler(-32f * rear, 0f, 0f);
            if (neck != null) neck.localRotation = Quaternion.Euler(-12f * rear + 8f * Mathf.Sin((float)(now * 9.0 % (2.0 * System.Math.PI))) * rear, 0f, 0f);
            float share = Mathf.Clamp01(speed / Mathf.Max(.1f, topSpeed));
            // One stride every ~2.4 m at a gallop, ~1.3 m at a trot.
            float stride = Mathf.Lerp(1.3f, 2.4f, share);
            float cycle = travelled / stride * Mathf.PI * 2f;
            float swing = speed > .2f ? Mathf.Lerp(18f, 42f, share) : 0f;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                // Front legs tuck up while it rears.
                bool front = i < 2;
                float offset = (i == 0 || i == 3) ? 0f : Mathf.PI * .65f;
                float angle = swing * Mathf.Sin(cycle + offset);
                if (front) angle -= 70f * rear;
                legs[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
            var at = body.localPosition;
            at.y = 1.05f + (speed > .2f ? .07f * Mathf.Abs(Mathf.Sin(cycle)) * share : 0f);
            body.localPosition = at;
        }

        static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }
    }
}
