using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 10-27 (승규 님: "F 누르면 칼이 양 손에 두개가 되고", then "비숍은 가만히 있고 칼이 멋있게 윙윙 날아가면서 … 칼이 돌아 올때도
    /// 무작위로 휭~ 돌아다니면서 내 손에 와야돼"): the bishop's two swords through his X cut, drawn on every machine (from the
    /// skill's replicated state) once the skeleton is drawn each frame. A second sword in the left hand while the skill
    /// holds one there (<see cref="SwordFightSkills.OffBlade"/>), and both swords in the air while they are thrown
    /// (<see cref="SwordFightSkills.BishopSwordPose"/>; the held sword is hidden meanwhile, <see cref="SwordAway"/>). Copies
    /// of this piece's own sword model, only drawn (no collider, no physics).
    /// </summary>
    public partial class SwordFightPawn
    {
        Transform offSword, thrownSword;
        int offSwordPiece = -1, thrownSwordPiece = -1;
        Transform visualHandL;
        Vector3 handMiddleL;
        int handLPiece = -1;

        void PoseOffHand()
        {
            var skills = Skills;
            bool can = skills != null && Alive && swordModel != null;
            // The right sword, thrown.
            if (can && skills.BishopSwordPose(0, ClickGrip(), out Vector3 gripR, out Quaternion turnR))
                Place(ref thrownSword, ref thrownSwordPiece, "Thrown sword (right hand)", gripR, turnR);
            else Hide(thrownSword);
            // The left: thrown, or held.
            if (can && skills.BishopSwordPose(1, LeftGrip(), out Vector3 gripL, out Quaternion turnL))
                Place(ref offSword, ref offSwordPiece, "Off-hand sword", gripL, turnL);
            else if (can && skills.OffBlade is Vector3 blade)
            {
                Vector3 grip = LeftGrip();
                Place(ref offSword, ref offSwordPiece, "Off-hand sword", grip, Quaternion.LookRotation(AboveFloor(grip, blade)));
            }
            else Hide(offSword);
        }

        /// <summary>His thrown sword's drawn copy (<paramref name="hand"/> 0 his right, 1 his left) while it is on its way
        /// out to the X: its <paramref name="middle"/> (it spins about it) and the way its <paramref name="blade"/> points;
        /// false while it is not (R181: the flight's effect follows it, SwordFightSkillFx.BishopFlight).</summary>
        public bool ThrownSwordOut(int hand, out Vector3 middle, out Vector3 blade)
        {
            var skills = Skills;
            return ThrownSword(hand, 0f, skills != null ? skills.BishopOutTime : 0f, out middle, out blade);
        }

        /// <summary>The same while it is on its way back from the X to his hand (R186: the way back's effect follows it,
        /// SwordFightSkillFx.BishopReturn).</summary>
        public bool ThrownSwordBack(int hand, out Vector3 middle, out Vector3 blade)
        {
            var skills = Skills;
            float from = skills != null ? skills.BishopOutTime + (skills.S != null ? skills.S.bishopCrossTime : 0.14f) : 0f;
            return ThrownSword(hand, from, skills != null ? skills.BishopThrowTime : 0f, out middle, out blade);
        }

        /// <summary>R191: where his hand (<paramref name="hand"/> 0 his right, 1 his left) holds its sword now, the drawn
        /// mitten's middle: where a thrown sword leaves from (the throw's effect starts there,
        /// SwordFightSkillFx.BishopRelease).</summary>
        public Vector3 SwordHand(int hand) => hand == 0 ? ClickGrip() : LeftGrip();

        /// <summary>How high up his sword model its blade starts, over the guard and its collar (the models stand along
        /// their +Y; VfxBladeCharge.BladeStart).</summary>
        const float BladeStart = 0.36f;

        /// <summary>R188: his drawn sword's blade now (<paramref name="hand"/> 0 his right, 1 his left), from where it
        /// starts to its tip: the one in his hand, or its thrown copy while it flies; false while neither is drawn (the
        /// draw-back's effect follows it, SwordFightSkillFx.BishopWindup).</summary>
        public bool DrawnBlade(int hand, out Vector3 guard, out Vector3 tip)
        {
            guard = tip = Vector3.zero;
            Transform model = null;
            var copy = hand == 0 ? thrownSword : offSword;
            if (copy != null && copy.gameObject.activeSelf && copy.childCount > 0) model = copy.GetChild(0);
            else if (hand == 0 && swordModel != null && swordModel.gameObject.activeInHierarchy) model = swordModel;
            var mesh = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
            if (mesh == null || mesh.sharedMesh == null) return false;
            Bounds b = mesh.sharedMesh.bounds;
            guard = mesh.transform.TransformPoint(new Vector3(b.center.x, BladeStart, b.center.z));
            tip = mesh.transform.TransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            return true;
        }

        /// <summary>His thrown sword's drawn copy between <paramref name="from"/> and <paramref name="to"/> seconds into
        /// the throw.</summary>
        bool ThrownSword(int hand, float from, float to, out Vector3 middle, out Vector3 blade)
        {
            middle = blade = Vector3.zero;
            var copy = hand == 0 ? thrownSword : offSword;
            var skills = Skills;
            if (copy == null || !copy.gameObject.activeSelf || skills == null || !skills.BishopSwordsOut
                || skills.StageTime < from || skills.StageTime >= to) return false;
            blade = copy.forward;
            middle = copy.position + blade * SwordFightSkills.BishopSwordHalf;
            return true;
        }

        void Place(ref Transform copy, ref int piece, string name, Vector3 grip, Quaternion turn)
        {
            if (copy == null || piece != modelPiece)
            {
                if (copy != null) Destroy(copy.gameObject);
                copy = BuildSwordCopy(name);
                piece = modelPiece;
            }
            if (copy == null) return;
            copy.SetPositionAndRotation(grip, turn);
            if (!copy.gameObject.activeSelf) copy.gameObject.SetActive(true);
        }

        static void Hide(Transform copy)
        {
            if (copy != null && copy.gameObject.activeSelf) copy.gameObject.SetActive(false);
        }

        /// <summary>A copy of the sword: a root like the sword's own (the grip at its origin, the blade along +Z) with the
        /// model under it placed as the sword's is.</summary>
        Transform BuildSwordCopy(string name)
        {
            if (swordModel == null) return null;
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            var copy = Instantiate(swordModel.gameObject, root, false);
            copy.name = "Sword model (copy)";
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
            copy.SetActive(true);
            root.gameObject.SetActive(false);
            return root;
        }

        /// <summary>The middle of the drawn left mitten (the piece model's left hand bone), else the physical left hand.</summary>
        Vector3 LeftGrip()
        {
            if (handLPiece != (int)Pawn.Piece || visualHandL == null)
            {
                handLPiece = (int)Pawn.Piece;
                visualHandL = null;
                var visual = transform.Find("Visual");
                if (visual != null)
                    foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        for (int k = 0; k < skin.bones.Length; k++)
                            if (skin.bones[k] != null && skin.bones[k].name.EndsWith("LeftHand"))
                            {
                                visualHandL = skin.bones[k];
                                handMiddleL = HandMiddle(skin, k);
                                break;
                            }
                        if (visualHandL != null) break;
                    }
            }
            return visualHandL != null ? visualHandL.TransformPoint(handMiddleL) : Pawn.bodies[(int)BodyId.HandL].position;
        }

        void DestroyOffSword()
        {
            if (offSword != null) Destroy(offSword.gameObject);
            if (thrownSword != null) Destroy(thrownSword.gameObject);
            offSword = thrownSword = null;
        }
    }
}
