using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushRallyGate : MonoBehaviour
    {
        public int wave;
        public Transform wall;
        public TextMesh label;
        public Bounds arrival;
        public KingRushRallyRules Rules { get; private set; }
        Vector3 closed;
        double openedAt = -1;
        Collider barrier;
        void Awake() { closed = wall.position; barrier = wall.GetComponent<Collider>(); ResetGate(); }
        public void ResetGate()
        { Rules = new KingRushRallyRules(wave); openedAt = -1; wall.position = closed; barrier.enabled = true; }
        public void Sample(KingRushRules claims, double now)
        {
            Rules.Advance(claims, now);
            if (label != null) label.text = Rules.Released ? "전원 집결 완료 · 출발!" : Rules.StartedAt < 0 ?
                $"승격 {Rules.Claimed} / {Rules.Required}\n모두 정해지면 10초 뒤 출발" :
                $"{System.Math.Ceiling(Rules.Remaining(now)):0}초 뒤 출발\n미도착 인원도 여기로 합류";
            if (Rules.Released) wall.position = closed + Vector3.up * (10 * Mathf.Clamp01((float)(now - openedAt) / .8f));
        }
        public void OpenAfterGather(double now, bool fixture = false)
        {
            if (fixture) Rules.ReleaseForTest();
            else if (!Rules.Release(now)) return;
            openedAt = now; barrier.enabled = false;
        }
        public Vector3 Slot(int index)
        {
            // Separate team rows and columns, away from the promotion pads.
            return new Vector3(arrival.center.x + (index < 6 ? -1 : 1) * (1.8f + index % 3 * 2.1f),
                arrival.min.y, arrival.center.z + 3 + index % 6 / 3 * 2);
        }
    }
}
