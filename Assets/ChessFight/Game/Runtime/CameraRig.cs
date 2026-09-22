using UnityEngine;

namespace ChessFight.Game
{
    // Test-arena camera. Sits above the board until the local pawn exists, then
    // trails it with the same exponential smoothing the prototype always used.
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Camera target;
        [SerializeField] Vector3 overviewPosition = new Vector3(0, 24, -24);
        [SerializeField] Vector3 followOffset = new Vector3(0, 12, -12);
        [SerializeField] float pitch = 45f;
        [SerializeField] float sharpness = 6f;
        [SerializeField] Color background = new Color(.07f, .10f, .17f);

        public Camera Target => target;

        public void Initialize()
        {
            if (target == null) target = Camera.main;
            if (target == null)
            {
                var go = new GameObject("ChessFight Camera") { tag = "MainCamera" };
                target = go.AddComponent<Camera>();
            }
            target.transform.SetPositionAndRotation(overviewPosition, Quaternion.Euler(pitch, 0, 0));
            target.clearFlags = CameraClearFlags.SolidColor;
            target.backgroundColor = background;
        }

        public void Follow(Transform subject, float dt)
        {
            if (target == null || subject == null) return;
            Vector3 destination = subject.position + followOffset;
            target.transform.position = Vector3.Lerp(target.transform.position, destination, 1 - Mathf.Exp(-sharpness * dt));
            target.transform.rotation = Quaternion.Euler(pitch, 0, 0);
        }
    }
}
