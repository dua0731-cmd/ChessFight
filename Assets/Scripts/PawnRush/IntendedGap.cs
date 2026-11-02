using UnityEngine;

namespace ChessFight.PawnRush
{
    // A gap on the way that is meant to be wider than the validator's required 1.5 m (R152): a jump pad's flight,
    // a gap only a dive crosses. SectionValidator leaves points inside it out of the gap check. A box in this
    // object's frame, centred on it.
    public sealed class IntendedGap : MonoBehaviour
    {
        [SerializeField] Vector3 size = Vector3.one;
        [SerializeField] string why = "";

        public string Why => why;

        public void Configure(Vector3 boxSize, string reason)
        {
            size = boxSize;
            why = reason;
        }

        public bool Contains(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            return Mathf.Abs(local.x) <= size.x * .5f && Mathf.Abs(local.y) <= size.y * .5f && Mathf.Abs(local.z) <= size.z * .5f;
        }
    }
}
