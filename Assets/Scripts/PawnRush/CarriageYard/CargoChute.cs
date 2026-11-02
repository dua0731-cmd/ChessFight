using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A lane down a slope that cargo keeps rolling down (낙하 화물 경사로, Royal Carriage Yard): every spawnInterval
    // seconds a barrel or a bale drops out of the hatch over the top of the lane, rolls down it at rollingSpeed and
    // sinks through the drain grate at the bottom, then comes round again. Each pass a piece takes a new place across
    // the lane, so the gaps are never in the same place twice, but a piece is never wider than a third of the lane:
    // there is always a way up between them. A piece that hits a character knocks it back down the slope
    // (YardImpact on the piece).
    //
    // This object is the lane: its position is the top of the lane ON the slope's surface, its +z points down the
    // slope (along the surface) and its +y out of it. The pieces are RollingCargo obstacles, each a pure function of
    // ObstacleClock (the drop, the roll, the sink and the jump back to the top are all worked out from the time), so
    // every PC sees the same barrels. Built from code on an inactive object, call Configure before switching it on.
    public sealed class CargoChute : MonoBehaviour
    {
        [Tooltip("Seconds between one piece and the next")]
        [SerializeField] float spawnInterval = 1.7f;
        [Tooltip("Speed down the lane (m/s)")]
        [SerializeField] float rollingSpeed = 5f;
        [Tooltip("Piece diameters (m), taken in turn")]
        [SerializeField] float[] cargoSizes = { 1f, 1.4f, 1.8f };
        [Tooltip("Length of the lane along the slope (m)")]
        [SerializeField] float laneLength = 30f;
        [Tooltip("Width of the lane (m)")]
        [SerializeField] float laneWidth = 8f;
        [Tooltip("How far over the top of the lane a piece appears, out of sight in the hatch (m, straight up)")]
        [SerializeField] float dropHeight = 3.4f;
        [Tooltip("Seconds added to the clock: two chutes with the same settings out of step")]
        [SerializeField] float phase;
        [Tooltip("Picks where across the lane each piece comes down")]
        [SerializeField] int seed = 1;
        [Tooltip("Push of a hit (m/s); the piece's speed adds to it")]
        [SerializeField] float knockbackForce = 6f;

        bool built;
        readonly List<RollingCargo> pieces = new List<RollingCargo>();

        public float SpawnInterval => spawnInterval;
        public float RollingSpeed => rollingSpeed;
        public float LaneLength => laneLength;
        public float LaneWidth => laneWidth;
        public float DropHeight => dropHeight;
        public int Seed => seed;
        public IReadOnlyList<RollingCargo> Pieces => pieces;

        // Gravity of a dropping piece (m/s²): a little over plain gravity, like the Pawn Rush pieces fall.
        public const float DropGravity = 12f;
        public const float SinkSeconds = .7f;

        public float DropSeconds => Mathf.Sqrt(2f * Mathf.Max(.01f, dropHeight) / DropGravity);
        public float RollSeconds => laneLength / Mathf.Max(.1f, rollingSpeed);
        // One trip: drop, roll, sink, and a moment hidden before the jump back to the top.
        public int PieceCount => Mathf.Max(2, Mathf.CeilToInt((DropSeconds + RollSeconds + SinkSeconds + .3f) / Mathf.Max(.2f, spawnInterval)));
        public float LoopSeconds => PieceCount * Mathf.Max(.2f, spawnInterval);

        public void Configure(float spawnInterval, float rollingSpeed, float[] cargoSizes, float laneLength, float laneWidth,
                              float dropHeight, float phase, int seed, float knockbackForce)
        {
            this.spawnInterval = spawnInterval;
            this.rollingSpeed = rollingSpeed;
            this.cargoSizes = cargoSizes;
            this.laneLength = laneLength;
            this.laneWidth = laneWidth;
            this.dropHeight = dropHeight;
            this.phase = phase;
            this.seed = seed;
            this.knockbackForce = knockbackForce;
        }

        void Awake()
        {
            if (built) return;
            built = true;
            int count = PieceCount;
            for (int i = 0; i < count; i++)
            {
                float size = cargoSizes != null && cargoSizes.Length > 0 ? cargoSizes[i % cargoSizes.Length] : 1.2f;
                size = Mathf.Clamp(size, .5f, laneWidth / 3f);
                var go = new GameObject("Cargo " + (i + 1));
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                var piece = go.AddComponent<RollingCargo>();
                piece.Configure(this, i, size, i % 3 == 1 ? RollingCargo.Shape.Bale : RollingCargo.Shape.Barrel, knockbackForce);
                piece.SetPhase(phase);
                go.SetActive(true);
                pieces.Add(piece);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, .55f, .2f, .8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, .05f, laneLength * .5f), new Vector3(laneWidth, .1f, laneLength));
            Gizmos.DrawLine(Vector3.zero, transform.InverseTransformDirection(Vector3.up) * dropHeight);
        }
    }
}
