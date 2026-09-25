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

        // Cartoon arms. On a wall the hands are placed on holds up to five arm-lengths away
        // (RagdollPawn.ApplyClimbPose), so the arm stubs are drawn out to meet them. Each arm bone
        // hangs off a helper that copies its arm body's pose and is scaled along the body's X axis,
        // which is the axis the stub lies on; scale 1 is exactly the old rigid arm.
        Transform[] stretch;
        float[] restLength;

        void Awake()
        {
            stretch = new Transform[entries.Length];
            restLength = new float[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                bool arm = e.driverA == (int)BodyId.ArmL || e.driverA == (int)BodyId.ArmR;
                if (!arm || !e.setPosition || e.bone == null) continue;
                Transform body = pawn.bodies[e.driverA].transform;
                Transform hand = pawn.bodies[e.driverA + 1].transform;
                restLength[i] = Vector3.Distance(body.position, hand.position);
                if (restLength[i] < 1e-4f) continue;
                // The hand bone is a child of the arm bone in the Mixamo rig. It is posed in world space
                // every frame anyway, but under a stretched parent the ball would stretch too.
                foreach (Entry other in entries)
                    if (other.bone != null && other.bone != e.bone && other.bone.IsChildOf(e.bone))
                        other.bone.SetParent(transform, true);
                var helper = new GameObject(e.bone.name + " Stretch").transform;
                helper.SetParent(transform, false);
                helper.SetPositionAndRotation(body.position, body.rotation);
                e.bone.SetParent(helper, false);
                e.bone.localPosition = e.positionOffset;
                e.bone.localRotation = e.rotationOffset;
                stretch[i] = helper;
            }
        }

        void LateUpdate()
        {
            var bodies = pawn.bodies;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                Transform a = bodies[e.driverA].transform;
                if (stretch != null && stretch[i] != null)
                {
                    float k = 1f;
                    if (pawn.ArmsStretched)
                        k = Mathf.Max(1f, Vector3.Distance(a.position, bodies[e.driverA + 1].transform.position) / restLength[i]);
                    stretch[i].SetPositionAndRotation(a.position, a.rotation);
                    stretch[i].localScale = new Vector3(k, 1f, 1f);
                    continue;
                }
                Quaternion rotation = e.driverB >= 0
                    ? Quaternion.Slerp(a.rotation, bodies[e.driverB].transform.rotation, e.blend)
                    : a.rotation;
                if (e.setPosition) e.bone.SetPositionAndRotation(a.TransformPoint(e.positionOffset), rotation * e.rotationOffset);
                else e.bone.rotation = rotation * e.rotationOffset;
            }
        }
    }
}
