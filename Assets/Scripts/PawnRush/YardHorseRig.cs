using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R678: the yard's horse model (RcyHorse.fbx, skinned) following a YardHorse. Every frame, after the horse's owner
    // has posed its pivots, each bone takes its pivot's turn. The pivots stand at the bones' heads with no turn of their
    // own at rest (YardHorse), so a pivot's local turn is in the body's axes; a bone's local turn is in its parent
    // bone's, so it is carried across by the parent's rest frame in the body.
    [DefaultExecutionOrder(10000)]
    public sealed class YardHorseRig : MonoBehaviour
    {
        // Bone (horse_model.py) and pivot (YardHorse). Root holds the trunk: the body pivot moves the whole model.
        static readonly string[,] Joints =
        {
            { "Neck", "Neck" }, { "Head", "Head" }, { "Tail1", "Tail" }, { "Tail2", "Tail 2" }, { "Tail3", "Tail 3" },
            { "UpperArm.L", "Leg FL" }, { "Forearm.L", "Elbow FL" }, { "Cannon.L", "Knee FL" }, { "Pastern.L", "Fetlock FL" },
            { "UpperArm.R", "Leg FR" }, { "Forearm.R", "Elbow FR" }, { "Cannon.R", "Knee FR" }, { "Pastern.R", "Fetlock FR" },
            { "Thigh.L", "Leg HL" }, { "Gaskin.L", "Stifle HL" }, { "HindCannon.L", "Hock HL" }, { "HindPastern.L", "Fetlock HL" },
            { "Thigh.R", "Leg HR" }, { "Gaskin.R", "Stifle HR" }, { "HindCannon.R", "Hock HR" }, { "HindPastern.R", "Fetlock HR" },
        };

        struct Joint
        {
            public Transform bone, pivot;
            public Quaternion rest, frame, frameInverse;
        }

        Joint[] joints = new Joint[0];

        public int Bound => joints.Length;

        // `body`: the horse's Body pivot; `pivots`: its pivots by name (found before the model hung under it, whose
        // bones share some names); `model`: the model, at rest.
        public void Bind(Transform body, Transform model, Dictionary<string, Transform> pivots)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            var bodyInverse = Quaternion.Inverse(body.rotation);
            var list = new List<Joint>();
            for (int i = 0; i < Joints.GetLength(0); i++)
            {
                if (!bones.TryGetValue(Joints[i, 0], out var bone) || !pivots.TryGetValue(Joints[i, 1], out var pivot) || bone.parent == null)
                    continue;
                var frame = bodyInverse * bone.parent.rotation;
                list.Add(new Joint { bone = bone, pivot = pivot, rest = bone.localRotation, frame = frame, frameInverse = Quaternion.Inverse(frame) });
            }
            joints = list.ToArray();
        }

        void LateUpdate()
        {
            for (int i = 0; i < joints.Length; i++)
            {
                var j = joints[i];
                if (j.bone != null && j.pivot != null)
                    j.bone.localRotation = j.frameInverse * j.pivot.localRotation * j.frame * j.rest;
            }
        }
    }
}
