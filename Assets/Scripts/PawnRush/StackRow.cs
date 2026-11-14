using System;
using ChessFight.Gameplay;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // A storage row in the sorting yard (R661, zone 2 "왕실 하역 창고"; the user: "크레인으로 짐을 옮기며 정리", "플레이어들을
    // 확실히 방해할 수 있을만한 동선과 장애물 형태"): a wall of cargo stacks, two crates high (out of any jump's reach, never
    // climbed), across the whole yard, with one empty slot in each half - and a crane over the row that keeps tidying it:
    // it lifts the stack beside the empty slot, swings it over and sets it down there, so the gap moves one slot every
    // trip, out along its half and back. The only ways through the row are its two gaps: a pawn runs for a gap and goes
    // through while it is open; whoever is still in a gap as the next stack swings in is shoved out of the row - forward
    // or back, never into the next stack (StackCrate) - and knocked down. The two halves mirror each other (neither team's
    // side is luckier); the rows of a yard run at other phases, so their gaps never line up and the way zigzags.
    //
    // Warned: the gap's floor shows green while it is open and flashes red for its last WarnSeconds before a stack swings
    // in; the trolley comes over the stack it is about to move. Every pose is a pure function of ObstacleClock (the gap's
    // slot from the trip number), the same on every PC without a packet.
    //
    // This object is the row's middle on the floor, its local x along the row, local z across it. Built from code on an
    // inactive object: Configure, then switch it on.
    public sealed class StackRow : MonoBehaviour
    {
        [Tooltip("Slots in each half of the row (one of them always empty)")]
        [SerializeField] int slotsPerHalf = 9;
        [Tooltip("Distance between two slots' middles (m)")]
        [SerializeField] float pitch = 2.2f;
        [Tooltip("A stack's size: along the row, up, across it (m)")]
        [SerializeField] Vector3 stackSize = new Vector3(2f, 2.4f, 1.8f);
        [Tooltip("The row is closed with stacks that never move from its last slots out to this far from the middle (m)")]
        [SerializeField] float fillTo = 23.5f;
        [Tooltip("The crane's posts stand this far out either side (m), in the walls")]
        [SerializeField] float halfSpan = 26.45f;
        [SerializeField] float beamHeight = 6.5f;
        [Header("A trip (s): the gap stands open, the stack is lifted, carried one slot, set down")]
        [SerializeField] float openSeconds = 1.2f;
        [SerializeField] float liftSeconds = .35f;
        [SerializeField] float carrySeconds = 1f;
        [SerializeField] float lowerSeconds = .35f;
        [Tooltip("How high a stack is lifted to be moved (m)")]
        [SerializeField] float lift = .35f;
        [Tooltip("The gap's floor flashes red for this long before a stack swings in (s)")]
        [SerializeField] float warnSeconds = .8f;
        [Tooltip("Push of a stack swinging into a pawn (m/s), out of the row")]
        [SerializeField] float knockbackForce = 4.5f;

        Transform course;
        StackCrate[] crates = new StackCrate[0];
        Transform[] trolleys = new Transform[0], ropes = new Transform[0];
        Renderer[] marks = new Renderer[0];
        Material[] markMaterials = new Material[0];
        bool built;
        float phase;

        static readonly Color Open = new Color(.35f, 1f, .45f, 1f), Closing = new Color(1f, .22f, .14f, 1f);

        public int SlotsPerHalf => slotsPerHalf;
        public float Pitch => pitch;
        public Vector3 StackSize => stackSize;
        public float Trip => openSeconds + liftSeconds + carrySeconds + lowerSeconds;
        public float OpenSeconds => openSeconds;
        public float Phase => phase;
        public float KnockbackForce => knockbackForce;
        public Transform Course => course;

        public void Configure(int slotsPerHalf, float pitch, Vector3 stackSize, float fillTo, float halfSpan, float beamHeight, float openSeconds,
                              float liftSeconds, float carrySeconds, float lowerSeconds, float lift, float warnSeconds, float knockbackForce,
                              float phase, Transform course)
        {
            this.slotsPerHalf = Mathf.Max(2, slotsPerHalf);
            this.pitch = pitch;
            this.stackSize = stackSize;
            this.fillTo = fillTo;
            this.halfSpan = halfSpan;
            this.beamHeight = beamHeight;
            this.openSeconds = Mathf.Max(warnSeconds + .1f, openSeconds);
            this.liftSeconds = Mathf.Max(.1f, liftSeconds);
            this.carrySeconds = Mathf.Max(.3f, carrySeconds);
            this.lowerSeconds = Mathf.Max(.1f, lowerSeconds);
            this.lift = lift;
            this.warnSeconds = Mathf.Max(.5f, warnSeconds);
            this.knockbackForce = knockbackForce;
            this.phase = phase;
            this.course = course;
            Build();
        }

        // ------------------------------------------------------------------ the gap's slot, from the clock

        // The trip at `time` (this row's phase added) and how far into it.
        public void TripAt(double time, out long trip, out float into)
        {
            double t = time + phase, length = Math.Max(.1, Trip);
            double k = Math.Floor(t / length);
            trip = (long)k;
            into = (float)(t - k * length);
        }

        // The empty slot at the start of trip k: out along the half and back (0 is the slot by the row's middle).
        public int GapAt(long k)
        {
            int span = slotsPerHalf - 1, cycle = 2 * span;
            int at = (int)(((k % cycle) + cycle) % cycle);
            return at <= span ? at : cycle - at;
        }

        // A slot's middle along the row in half h (0 west, 1 east).
        public float SlotX(int half, int slot) => (half == 0 ? -1f : 1f) * (slot + .5f) * pitch;

        // Where stack j of a half stands while the gap is at `gap`: the stacks fill the slots in order, skipping the gap.
        public static int SlotOf(int stack, int gap) => stack < gap ? stack : stack + 1;

        // The empty slot open `ahead` seconds from now in half h (for the test bots): the gap if it is still open then,
        // else the slot the moving stack is leaving.
        public Vector3 GapWorld(int half, float ahead, out float openFor)
        {
            TripAt(ObstacleClock.Now + ahead, out long k, out float into);
            int a = GapAt(k), b = GapAt(k + 1);
            bool open = into < openSeconds;
            int slot = open ? a : b;
            openFor = open ? openSeconds - into : Trip - into + openSeconds;
            return transform.TransformPoint(new Vector3(SlotX(half, slot), 0f, 0f));
        }

        // ------------------------------------------------------------------ building

        void Build()
        {
            if (built) return;
            built = true;
            float top = beamHeight + .5f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * halfSpan;
                YardKit.Box(transform, "Row crane post", new Vector3(x, top * .5f, 0f), new Vector3(.6f, top, .6f), YardKit.WoodDark, Solid.None);
                YardKit.Box(transform, "Post band", new Vector3(x, beamHeight - .6f, 0f), new Vector3(.66f, .14f, .66f), YardKit.Iron, Solid.None);
                // The stacks that never move, from the row's last slot out to the stalls: closed.
                float inner = slotsPerHalf * pitch, outer = fillTo;
                if (outer > inner + .1f)
                {
                    var fill = YardKit.Span(transform, "Row stock (collider)", new Vector3(Mathf.Min(side * inner, side * outer), 0f, -stackSize.z * .5f),
                                            new Vector3(Mathf.Max(side * inner, side * outer), stackSize.y, stackSize.z * .5f), null, Solid.NoClimb);
                    fill.GetComponent<MeshRenderer>().enabled = false;
                    for (float s = inner + .1f; s < outer - .2f; s += pitch)
                    {
                        float w = Mathf.Min(stackSize.x, outer - s - .05f), cx = side * (s + w * .5f);
                        StackPicture(transform, new Vector3(cx, 0f, 0f), new Vector3(w, stackSize.y, stackSize.z), (int)(s * 7f));
                    }
                }
            }
            YardKit.Box(transform, "Row crane beam", new Vector3(0f, beamHeight + .25f, 0f), new Vector3(halfSpan * 2f + .9f, .5f, .6f), YardKit.WoodDark, Solid.None);
            YardKit.Box(transform, "Beam trim", new Vector3(0f, beamHeight + .25f, .31f), new Vector3(halfSpan * 2f + .9f, .08f, .02f), YardKit.Gold, Solid.None);
            YardKit.Box(transform, "Rail", new Vector3(0f, beamHeight - .04f, 0f), new Vector3(halfSpan * 2f - .8f, .08f, .3f), YardKit.Iron, Solid.None);

            int perHalf = slotsPerHalf - 1;
            crates = new StackCrate[perHalf * 2];
            for (int h = 0; h < 2; h++)
                for (int j = 0; j < perHalf; j++)
                {
                    var go = new GameObject("Stack " + (h == 0 ? "W" : "E") + (j + 1));
                    go.transform.SetParent(transform, false);
                    var crate = go.AddComponent<StackCrate>();
                    crate.Configure(this, h, j);
                    crates[h * perHalf + j] = crate;
                }

            // Per half: the trolley, its two ropes, the gap's floor mark.
            trolleys = new Transform[2];
            ropes = new Transform[4];
            marks = new Renderer[2];
            markMaterials = new Material[2];
            for (int h = 0; h < 2; h++)
            {
                trolleys[h] = YardKit.Box(transform, "Row trolley", Vector3.zero, new Vector3(1.1f, .45f, .9f), YardKit.Iron, Solid.None).transform;
                YardKit.Box(trolleys[h], "Trolley lamp", new Vector3(0f, -.3f, -.5f), new Vector3(.25f, .25f, .1f), YardKit.Lamp, Solid.None);
                for (int r = 0; r < 2; r++)
                    ropes[h * 2 + r] = YardKit.Box(transform, "Row rope", Vector3.zero, new Vector3(.06f, 1f, .06f), YardKit.WoodDark, Solid.None).transform;
                markMaterials[h] = new Material(Arena.Glow(Open)) { name = "M_YardRowGap" };
                var mark = YardKit.Box(transform, "Gap mark", Vector3.zero, new Vector3(stackSize.x - .1f, .02f, stackSize.z + .6f), markMaterials[h], Solid.None);
                marks[h] = mark.GetComponent<Renderer>();
                marks[h].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // A stack's look: two crates under a band (pictures; the collider is the caller's).
        public static void StackPicture(Transform parent, Vector3 floor, Vector3 size, int variety)
        {
            float tier = size.y * .5f;
            for (int i = 0; i < 2; i++)
            {
                float shrink = i == 1 ? .1f : 0f;
                var at = floor + Vector3.up * (tier * (i + .5f));
                var mat = ((variety + i) % 3) == 0 ? YardKit.WoodDark : YardKit.WoodLight;
                YardKit.Box(parent, "Stack crate", at, new Vector3(size.x - shrink, tier - .04f, size.z - shrink), mat, Solid.None);
                YardKit.Box(parent, "Stack band", at, new Vector3(size.x - shrink + .03f, .14f, size.z - shrink + .03f), (variety + i) % 2 == 0 ? YardKit.Banner : YardKit.WoodDark, Solid.None);
                foreach (float a in new[] { -1f, 1f })
                    YardKit.Box(parent, "Stack edge", at + new Vector3(a * (size.x - shrink) * .5f, 0f, 0f), new Vector3(.08f, tier - .02f, size.z - shrink + .02f), YardKit.WoodDark, Solid.None);
            }
        }

        // ------------------------------------------------------------------ the stacks' poses (StackCrate asks)

        // Stack j of half h at `time`, in this row's frame: its floor point and whether it is the one moving.
        public Vector3 StackLocal(int half, int j, double time, out bool moving)
        {
            TripAt(time, out long k, out float into);
            int a = GapAt(k), b = GapAt(k + 1);
            int mover = b > a ? a : b;   // the stack in slot b: stack b - 1 above the gap, stack b below it
            moving = j == mover && into >= openSeconds;
            if (!moving) return new Vector3(SlotX(half, SlotOf(j, a)), 0f, 0f);
            float t = into - openSeconds, from = SlotX(half, b), to = SlotX(half, a);
            if (t < liftSeconds) return new Vector3(from, lift * Smooth(t / liftSeconds), 0f);
            t -= liftSeconds;
            if (t < carrySeconds) return new Vector3(Mathf.Lerp(from, to, Smooth(t / carrySeconds)), lift, 0f);
            t -= carrySeconds;
            return new Vector3(to, lift * (1f - Smooth(t / lowerSeconds)), 0f);
        }

        static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        // ------------------------------------------------------------------ pictures: the trolleys, ropes and gap marks

        void LateUpdate()
        {
            if (trolleys.Length == 0) return;
            double now = ObstacleClock.Now;
            TripAt(now, out long k, out float into);
            int a = GapAt(k), b = GapAt(k + 1), before = GapAt(k - 1);
            float pulse = .5f + .5f * (float)Math.Sin(now * 14.0 % (2.0 * Math.PI));
            for (int h = 0; h < 2; h++)
            {
                // The trolley: while the gap stands open it rolls from where it set the last stack down to the next one it
                // lifts; then it carries that stack (from where the stack is drawn, so the ropes never run ahead of it).
                float x;
                bool holding = into >= openSeconds;
                float stackTop = stackSize.y;
                if (!holding)
                {
                    float u = Smooth(Mathf.Clamp01(into / Mathf.Max(.1f, openSeconds - .2f)));
                    x = Mathf.Lerp(SlotX(h, before), SlotX(h, b), u);
                }
                else
                {
                    int mover = b > a ? a : b;
                    var crate = crates[h * (slotsPerHalf - 1) + mover];
                    var drawn = transform.InverseTransformPoint(crate.transform.position);
                    x = drawn.x;
                    stackTop = drawn.y + stackSize.y;
                }
                trolleys[h].localPosition = new Vector3(x, beamHeight - .25f, 0f);
                for (int r = 0; r < 2; r++)
                {
                    float dz = (r == 0 ? -1f : 1f) * stackSize.z * .3f;
                    float bottom = holding ? stackTop : beamHeight - 1.2f;
                    float length = Mathf.Max(.05f, beamHeight - .5f - bottom);
                    ropes[h * 2 + r].localPosition = new Vector3(x, bottom + length * .5f, dz);
                    ropes[h * 2 + r].localScale = new Vector3(.06f, length, .06f);
                }
                // The gap's floor: green while it stands open, flashing red from WarnSeconds before the next stack swings
                // in until it is set down there; then the mark moves on to the new gap.
                bool closing = into > openSeconds - warnSeconds;
                marks[h].transform.localPosition = new Vector3(SlotX(h, a), .02f, 0f);
                var c = closing ? Closing : Open;
                c.a = closing ? .55f + .4f * pulse : .45f;
                markMaterials[h].color = c;
            }
        }

        void OnDestroy()
        {
            foreach (var m in markMaterials)
                if (m != null) Destroy(m);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .55f, .2f, .5f);
            Gizmos.DrawWireCube(new Vector3(0f, stackSize.y * .5f, 0f), new Vector3(fillTo * 2f, stackSize.y, stackSize.z));
        }
    }
}
