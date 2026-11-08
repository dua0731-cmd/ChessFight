using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Saved test-scene fixture only: keep the recovery floors outside the bridge's sweep.
    [ExecuteAlways]
    public sealed class WeightedBridgePracticeArea : MonoBehaviour
    {
        [SerializeField] Transform bridgeRoot;
        [SerializeField] Transform approach, landing, leftWalk, rightWalk;
        [SerializeField] Transform recovery, recoveryMarker, landingMarker, leftEdge, rightEdge;
        Vector3 lastSize, lastPosition;
        bool dirty = true;
        void OnEnable() { dirty = true; }
        void OnValidate() { dirty = true; }
        void Update()
        {
            if (bridgeRoot == null) return;
            var bridge = bridgeRoot.GetComponent<WeightedBridge>();
            if (bridge == null) return;
            var size = Vector3.Scale(bridge.Size, bridgeRoot.localScale);
            if (!dirty && size == lastSize && bridgeRoot.localPosition == lastPosition) return;
            dirty = false; lastSize = size; lastPosition = bridgeRoot.localPosition;
            float gapX = lastSize.x * .5f + 1f, gapZ = lastSize.z * .5f + .6f;
            float width = Mathf.Max(24f, 2f * gapX + 8f);
            float floorY = bridgeRoot.localPosition.y;
            SetFloor(approach, new Vector3(0f, floorY - .5f, -gapZ - 5f), new Vector3(width, 1f, 10f));
            SetFloor(landing, new Vector3(0f, floorY - .5f, gapZ + 6f), new Vector3(width, 1f, 12f));
            float sideWidth = (width - 2f * gapX) * .5f;
            SetFloor(leftWalk, new Vector3(-gapX - sideWidth * .5f, floorY - .5f, 0f), new Vector3(sideWidth, 1f, 2f * gapZ));
            SetFloor(rightWalk, new Vector3(gapX + sideWidth * .5f, floorY - .5f, 0f), new Vector3(sideWidth, 1f, 2f * gapZ));
            if (recovery != null) recovery.localPosition = new Vector3(0f, floorY + .03f, -gapZ - 5f);
            if (recoveryMarker != null) recoveryMarker.localPosition = new Vector3(0f, floorY + .006f, -gapZ - 5f);
            if (landingMarker != null) landingMarker.localPosition = new Vector3(0f, floorY + .006f, gapZ + 6f);
            SetFloor(leftEdge, new Vector3(-width * .5f + .2f, floorY + .008f, .5f), new Vector3(.12f, .016f, 2f * gapZ + 22f));
            SetFloor(rightEdge, new Vector3(width * .5f - .2f, floorY + .008f, .5f), new Vector3(.12f, .016f, 2f * gapZ + 22f));
        }
        static void SetFloor(Transform part, Vector3 position, Vector3 scale)
        {
            if (part == null) return;
            part.localPosition = position; part.localScale = scale;
        }
    }
}
