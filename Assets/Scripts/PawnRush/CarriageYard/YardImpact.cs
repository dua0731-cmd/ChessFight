using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A moving part whose hits come in turns (a roaming carriage's charges): a character is knocked down once a turn.
    public interface IHitTurns
    {
        // The turn now: a roaming carriage's is when its current leg started.
        double HitTurn { get; }
    }

    // A trigger shell round a moving part of the Royal Carriage Yard (a carriage, a waterwheel blade, a barrel) that
    // knocks a character away when the part runs into it. Only a part coming at the character hits: the face of the
    // part nearest the character (or the round of a ball or a drum) must move towards it, and mostly so (within 60
    // degrees), so a carriage driving away does not drag whoever is behind it along and one sliding past only brushes.
    // The push goes along the part's motion, or straight out from its line (alongShare), plus a lift, and grows with the
    // part's speed there up to a cap; a part coming on fast also knocks the character down for a moment, a slow one
    // only staggers it. With replaceSpeed the push sets the character's speed across the floor instead of adding to it,
    // so a hit sends it the same way whatever it was doing (a heavier piece still goes 1 / weight as far; R287: the 6v6
    // review found added pushes sliding 10-20 m and handing out speed along the charge). With a `course`, the part of a
    // push towards the race's way on is cut to forwardShare: a hit is never a free ride ahead.
    //
    // Through the existing IHitReceiver (the ragdoll's TakeHit, which also lets go of whatever it holds), only on the
    // PC that simulates the character (YardKit.Movable). The part's own solid collider still pushes physically; this
    // makes the result a clear knock-back instead of a body ground against a moving box, and a part with a `solid` box
    // lets a character it has just knocked down through that box until it is clear (at most 0.6 s), so the box does not
    // catch it and carry it on at its own speed. Once per character per `cooldown`, so a body lying against a part is
    // not hit every physics step, and one knockdown a turn for a part that has turns (IHitTurns; a stagger does not use
    // the turn up).
    [RequireComponent(typeof(Collider))]
    public sealed class YardImpact : MonoBehaviour
    {
        // The shape a hit is judged by: a box (`solid`, else the shell), a ball, or a drum lying along local x (the
        // shell a capsule along x: its ends are not a front).
        public enum Body { Box, Ball, Drum }

        [Tooltip("Push (m/s) of a hit from a part that barely moves")]
        public float knockbackForce = 6f;
        [Tooltip("Extra push per m/s of the part's speed where it touches")]
        public float speedScale = .6f;
        [Tooltip("Upward part of the push (m/s)")]
        public float lift = 2.5f;
        [Tooltip("The push never goes over this (m/s), whatever the speed")]
        public float maxPush = 13f;
        [Tooltip("Parts coming at the character slower than this (m/s) do not hit: a parked cart is a wall")]
        public float minSpeed = .4f;
        [Tooltip("Coming at the character this fast (m/s) or faster, the hit also knocks it down")]
        public float knockdownSpeed = 3f;
        [Tooltip("How long a knockdown keeps the character down (s)")]
        public float knockdownSeconds = .9f;
        [Tooltip("Seconds before the same character can be hit by this part again")]
        public float cooldown = .7f;
        [Tooltip("Share of the push along the part's motion (1); the rest goes straight out from its line, to the side the character is on (0)")]
        [Range(0f, 1f)] public float alongShare = .7f;
        [Tooltip("The push sets the character's speed across the floor instead of adding to it")]
        public bool replaceSpeed;
        [Tooltip("The part's speed at its centre, not where it touches: a rolling piece's spin does not count")]
        public bool fromCentre;
        [Tooltip("Push along this object's +z (flat) instead of the part's motion: cargo always goes down its ramp")]
        public Transform lane;
        public Body body;
        [Tooltip("Box: the part's own box the faces are judged by, when the shell reaches further than the part")]
        public BoxCollider solid;
        [Tooltip("The part stands on the floor: its underside never faces anyone (a pawn lying at its nose is in front)")]
        public bool onFloor;
        [Tooltip("The race's way on (this object's +z, flat): a push towards it is cut to forwardShare")]
        public Transform course;
        [Range(0f, 1f)] public float forwardShare = .6f;
        [Tooltip("A character dead in the part's line goes to its right (1) or its left (-1)")]
        public float centreSide = 1f;

        IMovingSurface surface;
        IHitTurns turns;
        Collider shell;
        readonly Dictionary<ICharacterDriver, Hit> lastHit = new Dictionary<ICharacterDriver, Hit>();
        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly List<Due> due = new List<Due>();

        struct Hit
        {
            public float Time;
            public double Turn;
        }

        // A hit found in a trigger callback, given in the next FixedUpdate: TakeHit lets go of what the hands hold,
        // and a grip's joint may not be destroyed inside a physics callback (HANDOFF absolute rule 14).
        struct Due
        {
            public ICharacterDriver Driver;
            public IHitReceiver Receiver;
            public Vector3 Push;
            public float Down;
        }

        // A character the solid box lets through for now, and since when.
        struct Ghost
        {
            public ICharacterDriver Who;
            public Collider[] Parts;
            public float Since;
        }

        public int Hits { get; private set; }

        void Awake()
        {
            shell = GetComponent<Collider>();
            shell.isTrigger = true;
            surface = GetComponentInParent<IMovingSurface>();
            turns = GetComponentInParent<IHitTurns>();
        }

        void OnDisable()
        {
            lastHit.Clear();
            due.Clear();
            for (int i = ghosts.Count - 1; i >= 0; i--) Collide(ghosts[i], true);
            ghosts.Clear();
        }

        void FixedUpdate()
        {
            // The hits found last step. With replaceSpeed the character's own speed comes off now, at its weight
            // (TakeHit moves a piece 1 / weight as far), so it ends at the push / weight whatever it was doing.
            for (int i = 0; i < due.Count; i++)
            {
                var d = due[i];
                if (d.Driver is Object alive && alive == null) continue;
                var push = d.Push;
                if (replaceSpeed)
                {
                    var hips = d.Driver.FollowTarget != null ? d.Driver.FollowTarget.GetComponent<Rigidbody>() : null;
                    var piece = d.Driver is Component owner ? owner.GetComponentInParent<IPromotable>() : null;
                    float weight = ChessPieces.Stats(piece != null ? piece.Piece : PieceKind.Pawn).Weight;
                    if (hips != null) push -= Flat(hips.linearVelocity) * weight;
                }
                d.Receiver.ApplyHit(push + Vector3.up * lift, d.Down, 0f, true);
            }
            due.Clear();
            // The box takes a knocked-down character back once they no longer overlap (after 0.35 s), or after 0.6 s.
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                float age = Time.time - ghosts[i].Since;
                if (age < .35f || (age < .6f && Overlaps(ghosts[i].Parts))) continue;
                Collide(ghosts[i], true);
                ghosts.RemoveAt(i);
            }
        }

        // Knocked down again while still let through: the time starts again.
        void LetThrough(ICharacterDriver driver)
        {
            if (solid == null || !(driver is Component owner)) return;
            for (int i = 0; i < ghosts.Count; i++)
                if (ghosts[i].Who == driver)
                {
                    var again = ghosts[i];
                    again.Since = Time.time;
                    ghosts[i] = again;
                    return;
                }
            var ghost = new Ghost { Who = driver, Parts = owner.GetComponentsInChildren<Collider>(), Since = Time.time };
            Collide(ghost, false);
            ghosts.Add(ghost);
        }

        // Only between live colliders: a switched-off collider has dropped its ignored pairs already (and Unity
        // complains about asking).
        void Collide(Ghost ghost, bool on)
        {
            if (solid == null || ghost.Parts == null || !solid.enabled || !solid.gameObject.activeInHierarchy) return;
            foreach (var part in ghost.Parts)
                if (part != null && !part.isTrigger && part.enabled && part.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(solid, part, !on);
        }

        bool Overlaps(Collider[] parts)
        {
            if (solid == null) return false;
            var room = solid.bounds;
            room.Expand(.1f);
            foreach (var part in parts)
                if (part != null && !part.isTrigger && room.Intersects(part.bounds)) return true;
            return false;
        }

        void OnTriggerStay(Collider other)
        {
            if (surface == null || !YardKit.Movable(other, out var driver, out var hits)) return;
            float now = Time.time;
            double turn = turns != null ? turns.HitTurn : double.NaN;
            if (lastHit.TryGetValue(driver, out var before) && (now - before.Time < cooldown || before.Turn == turn)) return;
            var target = driver.FollowTarget != null ? driver.FollowTarget.position : other.transform.position;
            var centre = shell.bounds.center;
            var velocity = surface.PointVelocity(fromCentre ? centre : target);
            float speed = velocity.magnitude;
            // Only a part coming at the character, through the face nearest it and mostly towards it: a carousel
            // carriage's side, swept round with it, only grazes.
            float coming = Vector3.Dot(velocity, Facing(target, centre));
            if (coming < Mathf.Max(minSpeed, .5f * speed)) return;

            // Along the part's motion (or its lane), and straight out from its line to the side the character is on.
            var along = Flat(lane != null ? lane.forward : velocity);
            var off = Flat(target - centre);
            Vector3 direction;
            if (along.sqrMagnitude > 1e-4f)
            {
                along.Normalize();
                var side = off - along * Vector3.Dot(off, along);
                side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.Cross(Vector3.up, along) * Mathf.Sign(centreSide);
                direction = along * alongShare + side * (1f - alongShare);
            }
            else direction = off.sqrMagnitude > 1e-4f ? off : Flat(transform.forward);
            direction = Flat(direction).normalized;

            float push = Mathf.Min(maxPush, knockbackForce + speed * speedScale);
            var change = direction * push;
            if (course != null)
            {
                var on = Flat(course.forward).normalized;
                float ahead = Vector3.Dot(change, on);
                if (ahead > 0f) change -= on * (ahead * (1f - forwardShare));
            }
            bool down = coming >= knockdownSpeed;
            due.Add(new Due { Driver = driver, Receiver = hits, Push = change, Down = down ? knockdownSeconds : 0f });
            // Letting the knocked-down body through the box only changes which pairs collide: fine in a callback.
            if (down) LetThrough(driver);
            // A stagger leaves the turn for the knockdown that may follow (a pawn caught as the charge sets off).
            lastHit[driver] = new Hit { Time = now, Turn = down ? turn : double.NaN };
            Hits++;
            if (lastHit.Count > 64) Forget(now);
        }

        // Out of the part towards the character: the face of its box the character is nearest, or the round of a ball
        // or of a drum (whose ends face along its axle, so they never come at anyone).
        Vector3 Facing(Vector3 target, Vector3 centre)
        {
            var t = shell.transform;
            if (body == Body.Ball) return (target - centre).normalized;
            if (body == Body.Drum)
            {
                var axle = t.right;
                var p = target - centre;
                float a = Vector3.Dot(p, axle);
                var radial = p - axle * a;
                float radius = .5f, half = 0f;
                if (shell is CapsuleCollider capsule)
                {
                    float scale = Mathf.Abs(t.lossyScale.x);
                    radius = capsule.radius * scale;
                    half = Mathf.Max(0f, capsule.height * .5f * scale - radius);
                }
                return Mathf.Abs(a) - half > radial.magnitude - radius ? axle * Mathf.Sign(a) : radial.normalized;
            }
            var box = solid != null ? solid : shell as BoxCollider;
            if (box == null) return (target - centre).normalized;
            // In the box's own frame: the face whose plane the point is furthest out past (or least inside of).
            var bt = box.transform;
            var size = YardKit.Abs(bt.lossyScale);
            var q = Vector3.Scale(bt.InverseTransformPoint(target) - box.center, size);
            var h = Vector3.Scale(box.size, size) * .5f;
            float ex = Mathf.Abs(q.x) - h.x, ey = Mathf.Abs(q.y) - h.y, ez = Mathf.Abs(q.z) - h.z;
            if (onFloor && q.y < 0f) ey = float.NegativeInfinity;
            // In front of that face, not off past one of its edges (no face comes at a point beyond a corner: a pawn
            // beside a charging carriage's path, reached by its long lead, is passed and not hit).
            const float beside = .3f;
            if (ex >= ey && ex >= ez) return Mathf.Max(ey, ez) > beside ? Vector3.zero : bt.right * Mathf.Sign(q.x);
            if (ey >= ez) return Mathf.Max(ex, ez) > beside ? Vector3.zero : bt.up * Mathf.Sign(q.y);
            return Mathf.Max(ex, ey) > beside ? Vector3.zero : bt.forward * Mathf.Sign(q.z);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // Old enough that neither the cooldown nor a turn (a charge is a few seconds) still holds it.
        void Forget(float now)
        {
            var old = new List<ICharacterDriver>();
            foreach (var pair in lastHit)
                if (now - pair.Value.Time > Mathf.Max(cooldown, 10f) || (pair.Key is Object o && o == null)) old.Add(pair.Key);
            foreach (var key in old) lastHit.Remove(key);
        }
    }
}
