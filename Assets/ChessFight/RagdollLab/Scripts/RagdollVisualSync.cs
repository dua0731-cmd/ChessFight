using System;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Poses the skinned Mixamo skeleton from the physics bodies after interpolation.
    /// Entries are ordered parent-first; intermediate spine bones blend between two bodies.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class RagdollVisualSync : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public Transform bone;
            public int driverA;
            public int driverB;
            public float blend;
            public bool setPosition;
            public Vector3 positionOffset;
            public Quaternion rotationOffset;
        }

        public RagdollPawn pawn;
        public Entry[] entries = Array.Empty<Entry>();

        void LateUpdate()
        {
            var bodies = pawn.bodies;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                Transform a = bodies[e.driverA].transform;
                Quaternion rotation = e.driverB >= 0
                    ? Quaternion.Slerp(a.rotation, bodies[e.driverB].transform.rotation, e.blend)
                    : a.rotation;
                if (e.setPosition) e.bone.SetPositionAndRotation(a.TransformPoint(e.positionOffset), rotation * e.rotationOffset);
                else e.bone.rotation = rotation * e.rotationOffset;
            }
        }
    }
}
