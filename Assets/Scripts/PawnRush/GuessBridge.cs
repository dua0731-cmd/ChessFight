using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand stair hall's glass stepping stones (R303, the user: "옳바른 길은 정해져있는데 플레이어는 그 길을 직접
    // 체크해서 확인해야만 알 수 있는 방식"): rows of two glass squares from the royal dais to the balcony. In every row
    // one square holds and the other gives way under a pawn, and the two look the same. Which is which comes from
    // the section's seed: the same way for the whole round and on every PC. A square that gave way stays open for
    // `reform` seconds - those behind can learn from whoever stepped on it - then closes again. Under the bridge a
    // kill volume sends the faller back to the dais's checkpoint.
    //
    // StateDrivenMover: a square breaks because someone stands on it, not on the shared clock. The authority
    // (offline, or the host) decides; online (R508) the host sends each square's state in the course state
    // (ISectionNetPart: a byte a square, 0 whole, else how long it has been open in tenths of a second).
    public sealed class GuessBridge : MonoBehaviour, ISectionPart, ISectionNetPart
    {
        [Tooltip("Row by row from the dais: the left (west) and right (east) squares.")]
        [SerializeField] GameObject[] left = new GameObject[0];
        [SerializeField] GameObject[] right = new GameObject[0];
        [Tooltip("Seconds a pawn stands on a false square before it gives way.")]
        [SerializeField] float giveWay = .12f;
        [Tooltip("Seconds a broken square stays open.")]
        [SerializeField] float reform = 15f;
        [Tooltip("Mixed into the section's seed: this bridge's own stream.")]
        [SerializeField] int salt = 4;

        bool[] leftHolds;
        float[] standing, brokenAt;          // per square: left rows first, then right rows
        Transform[][] shards;
        AudioClip breakClip;
        bool receivedState;
        readonly Collider[] overlaps = new Collider[32];

        public int Rows => Mathf.Min(left.Length, right.Length);
        public bool LeftHolds(int row) => leftHolds != null && row < leftHolds.Length && leftHolds[row];

        // For the builder: on an inactive object, before Awake.
        public void Configure(GameObject[] leftSquares, GameObject[] rightSquares)
        {
            left = leftSquares;
            right = rightSquares;
        }

        public void OnSection(PawnRushSection section)
        {
            int rows = Rows;
            var rng = new SeededRandom(SeededSchedule.Hash(section.Seed, salt));
            leftHolds = new bool[rows];
            for (int i = 0; i < rows; i++) leftHolds[i] = (rng.NextUInt() & 1u) == 0u;
            standing = new float[rows * 2];
            brokenAt = new float[rows * 2];
            receivedState = false;
            if (breakClip == null) breakClip = Resources.Load<AudioClip>("PawnRushHallSfx/BridgeGlassBreak");
            for (int i = 0; i < rows * 2; i++) Mend(i);
        }

        GameObject Square(int i) => i < Rows ? left[i] : right[i - Rows];

        // ------------------------------------------------------------------ online (R508)

        public int NetBytes => Rows * 2;

        public void WriteNet(ref NetBlock block)
        {
            for (int i = 0; i < Rows * 2; i++)
            {
                float open = brokenAt != null && !float.IsNegativeInfinity(brokenAt[i]) ? Time.time - brokenAt[i] : -1f;
                block.U8(open < 0f ? 0 : Mathf.Clamp(1 + Mathf.FloorToInt(open * 10f), 1, 254));
            }
        }

        // Not the host: the host's squares. One the host has open opens here (counted from when it did there); one the host
        // has whole again closes here.
        public void ReadNet(ref NetBlock block)
        {
            for (int i = 0; i < Rows * 2; i++)
            {
                int v = block.ReadU8();
                if (brokenAt == null || Square(i) == null) continue;
                if (v == 0)
                {
                    if (!float.IsNegativeInfinity(brokenAt[i])) Mend(i);
                    continue;
                }
                float open = (v - 1) / 10f, at = Time.time - open;
                if (float.IsNegativeInfinity(brokenAt[i]))
                    Break(i, at, receivedState && open <= .4f);
                else if (Mathf.Abs(brokenAt[i] - at) > .5f) brokenAt[i] = at;
            }
            receivedState = true;
        }

        void Break(int i, float at, bool sound)
        {
            var square = Square(i);
            brokenAt[i] = at;
            Show(square, false);
            if (!sound || breakClip == null) return;
            var voice = square.GetComponent<AudioSource>();
            if (voice == null)
            {
                voice = square.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 4f;
                voice.maxDistance = 24f;
                voice.dopplerLevel = 0f;
                SectionSoundGate.Gate(voice);
            }
            voice.PlayOneShot(breakClip, 1.4f);   // R654: -24 dB after the hall's 0.55 (was -29, under the music)
        }

        void Mend(int i)
        {
            // A late join can place a real break before this client's scene time zero.
            brokenAt[i] = float.NegativeInfinity;
            Show(Square(i), true);
            if (shards != null && shards[i] != null)
                foreach (var piece in shards[i]) if (piece != null) piece.gameObject.SetActive(false);
        }
        bool Holds(int i) => i < Rows ? leftHolds[i] : !leftHolds[i - Rows];

        void FixedUpdate()
        {
            if (leftHolds == null) return;
            float now = Time.time, dt = Time.fixedDeltaTime;
            for (int i = 0; i < Rows * 2; i++)
            {
                var square = Square(i);
                if (square == null) continue;
                if (!float.IsNegativeInfinity(brokenAt[i]))
                {
                    Fall(i, now - brokenAt[i]);
                    if (ObstacleClock.Authority && now - brokenAt[i] >= reform) Mend(i);
                    continue;
                }
                if (Holds(i) || !ObstacleClock.Authority) continue;
                standing[i] = SomeoneOn(square) ? standing[i] + dt : 0f;
                if (standing[i] < giveWay) continue;
                standing[i] = 0f;
                Break(i, now, true);
            }
        }

        // A pawn's body just over the square.
        bool SomeoneOn(GameObject square)
        {
            var box = square.GetComponent<Collider>().bounds;
            int n = Physics.OverlapBoxNonAlloc(box.center + Vector3.up * (box.extents.y + .45f), new Vector3(box.extents.x, .45f, box.extents.z),
                                               overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < n; k++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[k]);
                if (actor != null && actor.CanReceiveObstacle) return true;
            }
            return false;
        }

        void Show(GameObject square, bool whole)
        {
            square.GetComponent<Collider>().enabled = whole;
            square.GetComponent<Renderer>().enabled = whole;
        }

        // The broken glass: four pieces of the square drop and tumble for a second (pictures, nothing to hit).
        void Fall(int i, float age)
        {
            if (shards == null) shards = new Transform[Rows * 2][];
            if (shards[i] == null)
            {
                var square = Square(i);
                shards[i] = new Transform[4];
                for (int k = 0; k < 4; k++)
                {
                    var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(piece.GetComponent<Collider>());
                    piece.name = "Glass shard";
                    piece.transform.SetParent(transform, true);
                    piece.GetComponent<Renderer>().sharedMaterial = square.GetComponent<Renderer>().sharedMaterial;
                    shards[i][k] = piece.transform;
                }
            }
            var home = Square(i).transform;
            bool falling = age < 1.2f;
            for (int k = 0; k < 4; k++)
            {
                var piece = shards[i][k];
                piece.gameObject.SetActive(falling);
                if (!falling) continue;
                var corner = new Vector3((k & 1) == 0 ? -.45f : .45f, 0f, (k & 2) == 0 ? -.45f : .45f);
                piece.position = home.position + Vector3.Scale(corner, home.lossyScale) + Vector3.down * (4.9f * age * age) + corner * age;
                piece.rotation = home.rotation * Quaternion.Euler(age * (200f + 60f * k), age * 90f * k, age * 150f);
                piece.localScale = Vector3.Scale(home.lossyScale, new Vector3(.45f, 1f, .45f));
            }
        }
    }
}
