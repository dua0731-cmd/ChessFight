using ChessFight.Gameplay;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // One cargo stack in a StackRow (R661): it stands in its slot until the row's crane picks it to fill the gap beside it,
    // lifts it a little, carries it one slot over and sets it down - a pure function of ObstacleClock (StackRow.StackLocal).
    // Two crates high, solid, never climbed.
    //
    // Only a stack swinging in hits: a pawn in the gap it swings into is shoved out of the row - across it, forward or
    // back by which side of the row the pawn is on, never along into the next stack - and knocked down, and the stack lets
    // it through until it is clear (YardImpact), so nobody is squeezed between two stacks.
    public sealed class StackCrate : Obstacle
    {
        [SerializeField] StackRow row;
        [SerializeField] int half, index;
        bool built;

        public void Configure(StackRow row, int half, int index)
        {
            this.row = row;
            this.half = half;
            this.index = index;
        }

        protected override void Awake()
        {
            if (row == null) row = GetComponentInParent<StackRow>();
            Build();
            base.Awake();
        }

        void Build()
        {
            if (built || row == null) return;
            built = true;
            var size = row.StackSize;
            StackRow.StackPicture(transform, Vector3.zero, size, half * 31 + index * 7);
            var body = new GameObject("Stack (collider)");
            body.transform.SetParent(transform, false);
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, size.y * .5f, 0f);
            box.size = size;
            body.AddComponent<NoClimbSurface>();

            var impact = YardKit.Shell(transform, "Stack (hit)", box.center, size + new Vector3(.4f, .1f, .2f), row.KnockbackForce);
            impact.solid = box;
            impact.onFloor = true;
            impact.speedScale = 0f;
            impact.maxPush = row.KnockbackForce;
            // Out of the row (across it), hardly along: a pawn in the gap goes forward or back, not into the next stack.
            impact.alongShare = .05f;
            impact.lift = 1.2f;
            impact.replaceSpeed = true;
            impact.minSpeed = .4f;
            // Nearly any swing knocks down, and so lets the pawn through the stack until it is clear: nobody is pressed
            // between the stack coming in and the one across the gap, or swept under a lifted stack while lying.
            impact.knockdownSpeed = 1f;
            impact.knockdownSeconds = 1f;
            impact.cooldown = .6f;
            impact.ghostSeconds = 1.2f;
            impact.course = row.Course;
            impact.forwardShare = .6f;
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            if (row == null) { position = StartPosition; return; }
            var local = row.StackLocal(half, index, time, out _);
            position = StartPosition + StartRotation * local;
        }
    }
}
