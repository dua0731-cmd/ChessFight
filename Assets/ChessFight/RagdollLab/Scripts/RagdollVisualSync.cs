using System;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Poses the skinned Mixamo skeleton from the physics bodies after interpolation.
    /// Entries are ordered parent-first; intermediate spine bones blend between two bodies.
    /// While the pawn wears a chess piece's model (RagdollPawn.Piece), PoseAsPiece poses it instead.
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

        // A chess piece is drawn from the same bodies as the pawn, but as a stiffer wooden thing with its
        // parts where its model has them (PoseAsPiece):
        // - its chest turns half as far from its hips as the chest body does, its head half as far again
        //   from the chest: the bodies' full 30 + 40 degrees folded the tall pieces in two in a dive;
        // - each arm turns about the piece's own shoulder (the rook's far out, the king's low and deep in),
        //   and the mitten keeps to the arm: centred off the hand ball, it swung off the stub;
        // - each foot turns half as far, about its own middle, and never goes up into the base or in under
        //   the other foot, which is where the pawn's foot bodies take wide or small-footed pieces' feet.
        const float PieceWaistBend = 0.5f;
        const float PieceHeadBend = 0.5f;
        const float PieceFootTurn = 0.5f;
        const float PieceFootLift = 0.02f;
        // R145 (승규 님 10-15, a screen recording of the rook: "뛸 때 팔이 너무 왔다갔다 거려 좀 줄여줘 … 팔 연결 부분 쪽이
        // 찢어져서 비어보이는 현상 수정해줘", "이거 끌고 다닐 때 빈 공간이 발생해", for every piece): the arm bodies swing
        // far as it runs, and a stub turned that far about its shoulder left the body (a gap there) and hid behind its mitten,
        // so the mitten seemed to float off on its own. Unless the arms reach for something (grabbing, climbing), each arm is
        // drawn turned only PieceArmSwing of the way from its rest pose on the chest, at most PieceArmMost degrees, and its
        // mitten sits at the stub's end. Dragging something, the stub is drawn out to the mitten (up to PieceArmDraw) instead
        // of the mitten leaving its end.
        const float PieceArmSwing = 0.35f;
        const float PieceArmMost = 30f;
        const float PieceArmDraw = 1.5f;
        static readonly string[] PieceMarks = { "LeftShoulder", "LeftWrist", "RightShoulder", "RightWrist", "LeftFoot", "RightFoot" };
        bool pieceLook;
        Vector3[] restAt;          // per arm and foot entry: its body at rest, in this transform's space
        Vector3[] restInParent;    // per arm and hand entry: the arm body at rest in the chest's space;
                                   // per foot entry: the foot body at rest in the hips' space
        Vector3 restHeadInChest;
        Vector3[] pieceShift;      // per arm, hand and foot entry, rest space: the piece's own joint - the pawn's
        float[] pieceReach;        // per arm entry: the pawn arm's rest length / the piece stub's
        Quaternion[] handInArm;    // per hand entry: the hand body's rest rotation in its arm's
        Quaternion[] armRest;      // per arm entry: the arm body's rest rotation in the chest body's
        Vector3[] handRest;        // per hand entry: the hand body at rest in its arm body's space
        readonly Quaternion[] armDrawn = new Quaternion[2];   // this frame's drawn arm, left and right

        void Awake() => Setup();

        void Setup()
        {
            if (stretch != null) return;
            stretch = new Transform[entries.Length];
            restLength = new float[entries.Length];
            restAt = new Vector3[entries.Length];
            restInParent = new Vector3[entries.Length];
            pieceShift = new Vector3[entries.Length];
            pieceReach = new float[entries.Length];
            handInArm = new Quaternion[entries.Length];
            armRest = new Quaternion[entries.Length];
            handRest = new Vector3[entries.Length];
            Transform hips = pawn.bodies[(int)BodyId.Hips].transform, chest = pawn.bodies[(int)BodyId.Chest].transform;
            restHeadInChest = chest.InverseTransformPoint(pawn.bodies[(int)BodyId.Head].position);
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                if (IsHand(e))
                {
                    Transform armBody = pawn.bodies[e.driverA - 1].transform;
                    handInArm[i] = Quaternion.Inverse(armBody.rotation) * pawn.bodies[e.driverA].rotation;
                    handRest[i] = armBody.InverseTransformPoint(pawn.bodies[e.driverA].position);
                    restInParent[i] = chest.InverseTransformPoint(armBody.position);
                }
                if (IsFoot(e))
                {
                    restAt[i] = transform.InverseTransformPoint(pawn.bodies[e.driverA].position);
                    restInParent[i] = hips.InverseTransformPoint(pawn.bodies[e.driverA].position);
                }
                bool arm = e.driverA == (int)BodyId.ArmL || e.driverA == (int)BodyId.ArmR;
                if (!arm || !e.setPosition || e.bone == null) continue;
                Transform body = pawn.bodies[e.driverA].transform;
                Transform hand = pawn.bodies[e.driverA + 1].transform;
                restAt[i] = transform.InverseTransformPoint(body.position);
                restInParent[i] = chest.InverseTransformPoint(body.position);
                armRest[i] = Quaternion.Inverse(chest.rotation) * body.rotation;
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

        static bool IsHand(Entry e) => e.setPosition && e.bone != null && (e.driverA == (int)BodyId.HandL || e.driverA == (int)BodyId.HandR);
        static bool IsFoot(Entry e) => e.setPosition && e.bone != null && (e.driverA == (int)BodyId.FootL || e.driverA == (int)BodyId.FootR);

        /// <summary>
        /// Wear a piece's model: <paramref name="look"/> is its prefab root (Resources/PieceModels), whose
        /// children mark, in the pawn's rest space, where each arm stub leaves the body (LeftShoulder,
        /// RightShoulder), where its mitten begins (LeftWrist, RightWrist) and the middle of each foot
        /// (LeftFoot, RightFoot). Without all six the pawn's own pose stays.
        /// </summary>
        public void SetPieceLook(Transform look)
        {
            Setup();
            ClearPieceLook();
            if (look == null) return;
            foreach (string mark in PieceMarks)
                if (look.Find(mark) == null) return;
            pieceLook = true;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                if (IsFoot(e))
                {
                    pieceShift[i] = look.Find((e.driverA == (int)BodyId.FootL ? "Left" : "Right") + "Foot").localPosition - restAt[i];
                    continue;
                }
                if (stretch[i] == null) continue;
                string side = e.driverA == (int)BodyId.ArmL ? "Left" : "Right";
                Vector3 shoulder = look.Find(side + "Shoulder").localPosition, wrist = look.Find(side + "Wrist").localPosition;
                pieceShift[i] = shoulder - restAt[i];
                pieceReach[i] = restLength[i] / Mathf.Max(0.02f, Mathf.Abs(wrist.x - shoulder.x));
                for (int j = 0; j < entries.Length; j++)
                    if (IsHand(entries[j]) && entries[j].driverA == e.driverA + 1) pieceShift[j] = pieceShift[i];
                // Under its stretch helper, which now sits on the piece's shoulder.
                e.bone.localPosition = e.positionOffset - pieceShift[i];
            }
        }

        /// <summary>Back to the pawn's own pose.</summary>
        public void ClearPieceLook()
        {
            if (!pieceLook) return;
            pieceLook = false;
            for (int i = 0; i < entries.Length; i++)
            {
                pieceShift[i] = Vector3.zero;
                if (stretch[i] != null) entries[i].bone.localPosition = entries[i].positionOffset;
            }
        }

        void LateUpdate()
        {
            // A per-screen hit stop (R111, online): the picture holds still while the bodies go on.
            if (pawn.VisualHeld) return;
            if (pieceLook)
            {
                PoseAsPiece();
                return;
            }
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

        /// <summary>The worn piece, from the same bodies (see the fields above).</summary>
        void PoseAsPiece()
        {
            var bodies = pawn.bodies;
            Transform hips = bodies[(int)BodyId.Hips].transform, chestBody = bodies[(int)BodyId.Chest].transform;
            Quaternion hipsTurn = hips.rotation;
            Quaternion chest = Quaternion.Slerp(hipsTurn, chestBody.rotation, PieceWaistBend);
            Vector3 waist = chestBody.position;
            // Reaching for something the arms go where their bodies are; otherwise they are calmed (R145).
            bool reaching = pawn.ArmsStretched || pawn.Grabbing;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                Transform a = bodies[e.driverA].transform;
                switch ((BodyId)e.driverA)
                {
                    case BodyId.Hips when e.driverB == (int)BodyId.Chest:
                        // Spine, Spine1: part of the way to the drawn chest.
                        e.bone.rotation = Quaternion.Slerp(hipsTurn, chest, e.blend) * e.rotationOffset;
                        break;
                    case BodyId.Chest:
                        e.bone.SetPositionAndRotation(waist + chest * e.positionOffset, chest * e.rotationOffset);
                        break;
                    case BodyId.Head:
                    {
                        Quaternion head = chest * Quaternion.Slerp(Quaternion.identity,
                            Quaternion.Inverse(chestBody.rotation) * a.rotation, PieceHeadBend);
                        e.bone.SetPositionAndRotation(waist + chest * restHeadInChest + head * e.positionOffset, head * e.rotationOffset);
                        break;
                    }
                    case BodyId.ArmL:
                    case BodyId.ArmR:
                        if (stretch[i] == null) goto default;
                        {
                            float k = 1f;
                            float reach = Vector3.Distance(a.position, bodies[e.driverA + 1].position) / restLength[i];
                            if (pawn.ArmsStretched) k = Mathf.Max(1f, reach);
                            else if (pawn.Grabbing) k = Mathf.Clamp(reach, 1f, PieceArmDraw);
                            Quaternion inChest = Quaternion.Inverse(chestBody.rotation) * a.rotation;
                            if (!reaching)
                            {
                                Quaternion swing = Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(armRest[i]) * inChest, PieceArmSwing);
                                inChest = armRest[i] * Quaternion.RotateTowards(Quaternion.identity, swing, PieceArmMost);
                            }
                            Quaternion arm = chest * inChest;
                            armDrawn[e.driverA == (int)BodyId.ArmL ? 0 : 1] = arm;
                            // About the piece's shoulder; drawn out so the stub still meets the mitten at the hand.
                            stretch[i].SetPositionAndRotation(waist + chest * (restInParent[i] + pieceShift[i]), arm);
                            stretch[i].localScale = new Vector3(1f + (k - 1f) * pieceReach[i], 1f, 1f);
                        }
                        break;
                    case BodyId.HandL:
                    case BodyId.HandR:
                        if (!IsHand(e)) goto default;
                        {
                            // Held to the arm, as far along it as the hand ball is (out on a hold while climbing).
                            Transform armBody = bodies[e.driverA - 1].transform;
                            Quaternion arm = armDrawn[e.driverA == (int)BodyId.HandL ? 0 : 1];
                            Vector3 along = Quaternion.Inverse(armBody.rotation) * (a.position - armBody.position);
                            // R145: at the stub's end unless the arm reaches (then the stub is drawn out to it, up to
                            // PieceArmDraw while dragging).
                            if (!reaching) along = handRest[i];
                            else if (!pawn.ArmsStretched && along.magnitude > handRest[i].magnitude * PieceArmDraw)
                                along = along.normalized * handRest[i].magnitude * PieceArmDraw;
                            e.bone.SetPositionAndRotation(
                                waist + chest * (restInParent[i] + pieceShift[i]) + arm * (along + handInArm[i] * e.positionOffset - pieceShift[i]),
                                arm * handInArm[i] * e.rotationOffset);
                        }
                        break;
                    case BodyId.FootL:
                    case BodyId.FootR:
                        if (!IsFoot(e)) goto default;
                        {
                            Vector3 moved = Quaternion.Inverse(hipsTurn) * (a.position - hips.position) - restInParent[i];
                            moved.y = Mathf.Min(moved.y, PieceFootLift);
                            moved.x = restInParent[i].x < 0f ? Mathf.Min(moved.x, 0f) : Mathf.Max(moved.x, 0f);
                            Quaternion foot = hipsTurn * Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(hipsTurn) * a.rotation, PieceFootTurn);
                            e.bone.SetPositionAndRotation(
                                hips.position + hipsTurn * (restInParent[i] + moved + pieceShift[i]) + foot * (e.positionOffset - pieceShift[i]),
                                foot * e.rotationOffset);
                        }
                        break;
                    default:
                    {
                        Quaternion rotation = e.driverB >= 0
                            ? Quaternion.Slerp(a.rotation, bodies[e.driverB].transform.rotation, e.blend)
                            : a.rotation;
                        if (e.setPosition) e.bone.SetPositionAndRotation(a.TransformPoint(e.positionOffset), rotation * e.rotationOffset);
                        else e.bone.rotation = rotation * e.rotationOffset;
                        break;
                    }
                }
            }
        }
    }
}
