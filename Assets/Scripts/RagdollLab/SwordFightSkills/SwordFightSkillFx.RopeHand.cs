using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The bishop's rescue rope held in the falling ally's hand (10-25, 승규 님: "밧줄을 감는 것 보다 떨어진 플레이어 손에 감아서
    /// 플레이어도 밧줄을 잡고 연출이 들어가게 해줘"): the ally's drawn arm reaches up toward the bishop as the rope flies at it,
    /// the rope's end wraps round its mitten and the hand closes on it, the arm stays along the taut rope while it is
    /// hauled in, and lets go when it is set down. Drawing only - the carry's physics, the network and the ragdoll are as
    /// they were: the drawn arm is turned in the ally's visual sync's <see cref="RagdollVisualSync.Posed"/> (after the
    /// skeleton is drawn), from state and events every machine already has (the bishop's Carried / CarriedHeld / EdgeOut and
    /// the BishopReach / Catch / Set / Drop events), so the host and the clients draw the same.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>A pawn's drawn arm and hand: the arm's stretch helper (turned about the drawn shoulder), the hand bone
        /// (not a child of the arm in the drawn rig: turned with it) and the mitten's middle in the hand bone's space.</summary>
        sealed class DrawnHand
        {
            public RagdollVisualSync sync;
            public Transform helper, hand;
            public SkinnedMeshRenderer skin;
            Mesh builtFor;
            public Bounds mitt;

            public Vector3 Mitt => hand != null ? hand.TransformPoint(mitt.center) : Vector3.zero;

            public static DrawnHand Of(RagdollPawn p, bool left)
            {
                if (p == null) return null;
                var sync = p.GetComponentInChildren<RagdollVisualSync>();
                if (sync == null) return null;
                var h = new DrawnHand { sync = sync };
                int arm = left ? (int)BodyId.ArmL : (int)BodyId.ArmR;
                foreach (var e in sync.entries)
                {
                    if (e.bone == null) continue;
                    if (e.driverA == arm && e.bone.parent != null && e.bone.parent.name.EndsWith(" Stretch")) h.helper = e.bone.parent;
                    if (e.driverA == arm + 1) h.hand = e.bone;
                }
                if (h.hand == null) return null;
                h.skin = DrawnSkin(p);
                h.Refresh();
                return h;
            }

            /// <summary>The mitten measured again when the drawn piece changes (SetPiece swaps the mesh).</summary>
            public void Refresh()
            {
                if (skin == null || skin.sharedMesh == builtFor) return;
                builtFor = skin.sharedMesh;
                int k = System.Array.IndexOf(skin.bones, hand);
                mitt = k >= 0 ? SwordFightPawn.MittenBounds(skin, k) : new Bounds(Vector3.zero, Vector3.one * 0.1f);
            }
        }

        /// <summary>
        /// Turns a drawn arm so its mitten lies on the line from the drawn shoulder to <paramref name="target"/> (by
        /// <paramref name="w"/>, the shortest turn from the arm as drawn, so it changes smoothly frame to frame), and closes
        /// the hand on what it holds (<paramref name="grip"/>: a roll about the arm and a curl - the mittens have no fingers).
        /// A left arm is kept from swinging across the face (and a right one likewise).
        /// </summary>
        static void AimDrawnArm(DrawnHand h, RagdollPawn pawn, bool left, Vector3 target, float w, float grip)
        {
            if (h == null || h.hand == null || w <= 0f) return;
            Vector3 sh = h.helper != null ? h.helper.position : pawn.bodies[left ? (int)BodyId.ArmL : (int)BodyId.ArmR].transform.position;
            Vector3 from = h.Mitt - sh, to = target - sh;
            if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 0.04f) return;
            // Not across the face: in the chest's frame the left arm's rest is -x; it may come in no further than +0.3.
            Quaternion cr = Quaternion.Slerp(pawn.Hips.rotation, pawn.bodies[(int)BodyId.Chest].rotation, 0.5f);
            Vector3 l = Quaternion.Inverse(cr) * to.normalized;
            l.x = left ? Mathf.Min(l.x, 0.3f) : Mathf.Max(l.x, -0.3f);
            to = cr * l.normalized * to.magnitude;
            Quaternion turn = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(from, to), w);
            if (h.helper != null) h.helper.rotation = turn * h.helper.rotation;
            Vector3 hp = sh + turn * (h.hand.position - sh);
            Quaternion hr = turn * h.hand.rotation;
            Vector3 m = sh + turn * from, axis = (m - sh).normalized;
            Vector3 bend = Vector3.Cross(axis, Vector3.up);
            if (bend.sqrMagnitude < 1e-4f) bend = Vector3.Cross(axis, Vector3.forward);
            Quaternion g = Quaternion.AngleAxis(20f * grip * w, bend.normalized) * Quaternion.AngleAxis(35f * grip * w, axis);
            // The grip turns about the mitten's middle, so the mitten stays where the rope is.
            h.hand.SetPositionAndRotation(m + g * (hp - m), g * hr);
        }

        /// <summary>When a bishop's rescue moments were played on this machine (its effect time): the throw, the catch, the
        /// let-go (set down, or dropped).</summary>
        sealed class RescueBeats
        {
            public float reach = -1e9f, caught = -1e9f, letGo = -1e9f;
            public bool dropped;
        }

        readonly Dictionary<SwordFightSkills, RescueBeats> rescueBeats = new Dictionary<SwordFightSkills, RescueBeats>();
        /// <summary>Drawn arms a rope is posing now (two bishops on one ally: the first one has the arm).</summary>
        static readonly HashSet<RagdollPawn> ropeArms = new HashSet<RagdollPawn>();

        RescueBeats Beats(SwordFightSkills s)
        {
            if (!rescueBeats.TryGetValue(s, out var b)) rescueBeats[s] = b = new RescueBeats();
            return b;
        }

        void StampRescue(SfFxEvent e)
        {
            if (e.by == null) return;
            var b = Beats(e.by);
            switch (e.kind)
            {
                case SfFxKind.BishopReach: b.reach = kit.Clock; b.caught = b.letGo = -1e9f; b.dropped = false; break;
                case SfFxKind.BishopCatch: b.caught = kit.Clock; break;
                case SfFxKind.BishopSet: b.letGo = kit.Clock; b.dropped = false; break;
                case SfFxKind.BishopDrop: b.letGo = kit.Clock; b.dropped = true; break;
            }
        }

        /// <summary>
        /// The bishop's rescue: a rope thrown from his drawn left hand to the falling ally's hand (the left one: its right
        /// holds the sword). While the rope flies the ally's arm reaches up for it; caught, the rope's end wraps round the
        /// mitten (cinching tight in a few frames, a short tail hanging under the fist), the hand closes, and the arm stays
        /// along the taut rope while it is hauled in; set down (or dropped), the hand opens, the arm comes down and the rope
        /// falls away slack. A real three-strand hemp rope in the board's light.
        /// </summary>
        void RealRescue(SwordFightSkills s)
        {
            var owner = s;
            kit.Keep(s, "real rescue rope", () =>
            {
                var fx = new Kit.Fx();
                // Thinner than 6 cm the builder's strand normals fold in and it draws black in URP.
                const float Thick = 0.07f;
                var spanMesh = new Mesh { name = "Rescue rope", hideFlags = HideFlags.HideAndDontSave };
                var coilMesh = new Mesh { name = "Rescue rope round the hand", hideFlags = HideFlags.HideAndDontSave };
                var tailMesh = new Mesh { name = "Rescue rope end", hideFlags = HideFlags.HideAndDontSave };
                spanMesh.MarkDynamic();
                tailMesh.MarkDynamic();
                var span = RopeObject("Rescue rope", spanMesh);
                var coil = RopeObject("Rescue rope round the hand", coilMesh);
                var tail = RopeObject("Rescue rope end", tailMesh);
                span.SetActive(false);
                coil.SetActive(false);
                tail.SetActive(false);
                float born = kit.Clock, throwAt = float.NaN, catchAt = float.NaN, letGoAt = float.NaN, notHeld = 0f;
                float armW = 0f, grip = 0f, wAtLetGo = 0f, builtR = -1f, coilR = 0.08f, coilH = 0.1f, coilTurns = 1.5f;
                bool measured = false;
                RagdollPawn ally = null;
                DrawnHand allyHand = null, bishopHand = DrawnHand.Of(owner.Pawn, true);
                bool claimed = false;
                Vector3 lastBishop = Vector3.zero, lastEnd = Vector3.zero, lastHandAt = Vector3.zero;
                var pts = new List<Vector3>();
                System.Action armHook = null;
                armHook = () =>
                {
                    if (!claimed || ally == null || allyHand == null) return;
                    allyHand.Refresh();
                    AimDrawnArm(allyHand, ally, true, lastBishop, armW, grip);
                };
                void Unhook()
                {
                    if (allyHand != null && allyHand.sync != null) allyHand.sync.Posed -= armHook;
                    if (claimed && ally != null) ropeArms.Remove(ally);
                    claimed = false;
                }
                fx.end = () =>
                {
                    Unhook();
                    if (span != null) Destroy(span);
                    if (coil != null) Destroy(coil);
                    if (tail != null) Destroy(tail);
                    Destroy(spanMesh);
                    Destroy(coilMesh);
                    Destroy(tailMesh);
                };
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null) return false;
                    var b = Beats(owner);
                    if (!kit.Held(x)) notHeld += d;
                    if (ally == null && owner.Carried != null)
                    {
                        ally = owner.Carried;
                        allyHand = DrawnHand.Of(ally, true);
                        if (allyHand != null && !ropeArms.Contains(ally)) { ropeArms.Add(ally); claimed = true; }
                    }
                    // The moments, from the events where they were played (on a client the state can lead the drawn bodies
                    // by a tenth of a second), with fallbacks for an event that never came.
                    if (float.IsNaN(throwAt))
                    {
                        if (b.reach >= born - 0.05f) throwAt = b.reach;
                        else if (x.age > 0.2f) throwAt = born;
                    }
                    if (float.IsNaN(throwAt)) return true;
                    float a = (kit.Clock - throwAt) / F;
                    if (float.IsNaN(catchAt) && (b.caught >= born - 0.05f || (owner.CarriedHeld && a > HandFly + 15f))) catchAt = b.caught >= born - 0.05f ? b.caught : kit.Clock;
                    if (float.IsNaN(letGoAt) && (b.letGo >= born - 0.05f || notHeld > 0.25f || (ally == null && x.age > 1f)))
                    {
                        letGoAt = kit.Clock;
                        wAtLetGo = armW;
                    }
                    float r = float.IsNaN(letGoAt) ? -1f : kit.Clock - letGoAt;
                    if (r > 0.5f) return false;
                    bool caught = !float.IsNaN(catchAt) && r < 0f;
                    // The bishop's drawn left mitten (his free hand), as drawn this frame.
                    if (bishopHand == null) bishopHand = DrawnHand.Of(owner.Pawn, true);
                    Vector3 from = bishopHand != null ? bishopHand.Mitt : owner.Pawn.bodies[(int)BodyId.HandL].transform.position;
                    if (r >= 0f) from = lastHandAt + Vector3.down * (1.5f * r * r);
                    Vector3 mitt = allyHand != null ? allyHand.Mitt : ally != null ? ally.bodies[(int)BodyId.HandL].transform.position : from + owner.EdgeOut;
                    // The arm: up toward him as the rope comes, along the rope once it holds, down when it lets go.
                    if (r >= 0f) { armW = wAtLetGo * (1f - Kit.EaseOut(r / 0.15f)); grip = 0f; }
                    else if (caught) { armW = Mathf.MoveTowards(armW, 1f, d / (2f * F)); grip = Kit.Pop(kit.Clock - catchAt, 1.25f); }
                    else if (!b.dropped) armW = 0.8f * FlyEase(a);
                    Vector3 dir = (from - mitt).sqrMagnitude > 1e-6f ? (from - mitt).normalized : Vector3.up;
                    Vector3 under = Vector3.ProjectOnPlane(Vector3.down, dir);
                    if (under.sqrMagnitude < 1e-4f) under = Vector3.ProjectOnPlane(owner.EdgeOut, dir);
                    under.Normalize();
                    Quaternion coilRot = Quaternion.LookRotation(Vector3.Cross(under, dir), dir);
                    if (caught && !measured && allyHand != null && allyHand.hand != null)
                    {
                        // The mitten's size across the rope and along it, once, at the catch: the wrap's radius and its turns.
                        measured = true;
                        var c = allyHand.mitt.center;
                        var e = allyHand.mitt.extents;
                        float across = 0f, along = 0f;
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 corner = allyHand.hand.TransformPoint(c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                            Vector3 off = corner - mitt;
                            along = Mathf.Max(along, Mathf.Abs(Vector3.Dot(off, dir)));
                            across = Mathf.Max(across, Vector3.ProjectOnPlane(off, dir).magnitude);
                        }
                        coilR = Mathf.Clamp(across * 0.85f + Thick * 0.5f - 0.01f, 0.06f, 0.12f);
                        coilTurns = along * 2f >= 0.13f ? 2.5f : 1.5f;
                        coilH = Mathf.Min(coilTurns * 0.055f, Mathf.Max(0.08f, along * 2f * 0.9f));
                    }
                    float sinceCatch = caught ? (kit.Clock - catchAt) / F : 0f;
                    float cinch = sinceCatch < 4f ? Mathf.Lerp(1.6f, 0.95f, Kit.EaseOut(sinceCatch / 4f)) : sinceCatch < 7f ? Mathf.Lerp(0.95f, 1f, (sinceCatch - 4f) / 3f) : 1f;
                    float rDraw = coilR * cinch;
                    if (caught && Mathf.Abs(rDraw - builtR) > 0.003f)
                    {
                        PawnRushSkillFx.RopeCoil(coilMesh, rDraw, coilH, coilTurns, Thick);
                        builtR = rDraw;
                    }
                    if (coil.activeSelf != caught) coil.SetActive(caught);
                    if (tail.activeSelf != caught) tail.SetActive(caught);
                    Vector3 coilTop = mitt + coilRot * new Vector3(-rDraw, coilH * 0.5f, 0f);
                    Vector3 coilBottom = mitt + coilRot * new Vector3(rDraw, -coilH * 0.5f, 0f);
                    if (caught)
                    {
                        coil.transform.SetPositionAndRotation(mitt, coilRot);
                        // A short end hanging under the fist.
                        var tp = new List<Vector3> { coilBottom, coilBottom + under * 0.03f + Vector3.down * 0.05f, coilBottom + under * 0.02f + Vector3.down * 0.12f, coilBottom + Vector3.down * 0.2f };
                        BuildRopeRun(tail, tailMesh, tp, Thick);
                    }
                    // The span: from his hand to where its end is.
                    Vector3 end;
                    float sag;
                    if (r >= 0f)
                    {
                        end = lastEnd + Vector3.down * (2.5f * r * r);
                        sag = 0.25f + r;
                    }
                    else if (caught)
                    {
                        end = coilTop;
                        sag = 0.04f;
                    }
                    else
                    {
                        float t = FlyEase(a);
                        end = t < 1f ? Bezier(from, from + owner.EdgeOut * 0.6f + Vector3.up * 0.8f, mitt + Vector3.up * 0.6f, mitt, t) : mitt;
                        sag = Mathf.Lerp(0.3f, 0.12f, t);
                    }
                    if (r < 0f) { lastEnd = end; lastHandAt = from; }
                    pts.Clear();
                    for (int i = 0; i <= 16; i++)
                    {
                        float u = i / 16f;
                        pts.Add(Vector3.Lerp(from, end, u) + Vector3.down * (sag * 4f * u * (1f - u)));
                    }
                    if (!span.activeSelf) span.SetActive(true);
                    BuildRopeRun(span, spanMesh, pts, Thick);
                    // The arm is aimed at his hand as drawn this frame, by the ally's visual sync next frame (the two syncs
                    // run in no fixed order: never read his bones inside the ally's hook). Hooked afresh each frame.
                    lastBishop = from;
                    if (claimed && allyHand != null && allyHand.sync != null)
                    {
                        allyHand.sync.Posed -= armHook;
                        if (armW > 0f || r < 0f) allyHand.sync.Posed += armHook;
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>A rope along world points, built round a pivot a metre off to its side (the builder turns its strands
        /// from the mesh's own origin, which must not lie along the rope: one running straight out of it turned inside out).</summary>
        static void BuildRopeRun(GameObject go, Mesh mesh, List<Vector3> world, float thick)
        {
            int n = world.Count;
            if (n < 2) return;
            Vector3 a = world[0], b = world[n - 1];
            Vector3 flat = b - a;
            flat.y = 0f;
            Vector3 perp = flat.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            Vector3 pivot = (a + b) * 0.5f - perp;
            var local = new List<Vector3>(n);
            foreach (var p in world) local.Add(p - pivot);
            go.transform.SetPositionAndRotation(pivot, Quaternion.identity);
            PawnRushSkillFx.RopeAlong(mesh, local, thick);
        }
    }
}
