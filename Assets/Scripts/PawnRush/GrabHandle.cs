using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A handle a pawn grips with the grab button (mini-games v1.0 §2.2): C's rope knots, G's sled ropes and rook
    // rings, I's carts, knight ring and king ring. Put on a small collider whose body is kinematic (a hand's joint
    // then hangs on that body and is dragged with it). The thing it belongs to wears NoClimbSurface; the handle
    // itself must not, or no hand could hold it.
    //
    // Who counts is not read from forces: a pawn pulls when one hand or both grip the handle, its hips are within
    // Reach of it, its move input points along PullAxis (dot >= 0.5), and it is standing. The same with the input
    // the other way (dot <= -0.5) holds back. Judged where the characters are simulated (offline, the host).
    public sealed class GrabHandle : MonoBehaviour
    {
        public enum HandleKind : byte { Pull, Drag, Hold }

        public const float Reach = 2f, Along = .5f;

        public byte Id;
        public MiniGameBase Owner;
        // World direction that moves the game forward when pulled toward; the game updates it every step.
        public Vector3 PullAxis = Vector3.back;
        public HandleKind Kind = HandleKind.Pull;

        Renderer look;
        Material gold, lit;
        static readonly Collider[] none = new Collider[0];

        public static readonly Color Gold = new Color(.886f, .698f, .235f);

        void Awake()
        {
            look = GetComponent<Renderer>();
            if (look != null)
            {
                gold = Arena.Emissive(Gold, .4f);
                look.sharedMaterial = gold;
            }
        }

        // The team colour lights up while one of `team` grips it (§2.2.7); the plain gold otherwise.
        public void ShowHeld(int team)
        {
            if (look == null) return;
            if (team < 0) { look.sharedMaterial = gold; return; }
            if (lit == null) lit = Arena.Emissive(Arena.TeamColor(team), .8f);
            lit.color = Arena.TeamColor(team);
            look.sharedMaterial = lit;
        }

        // The handle a pawn's hand holds, if it is one (either hand).
        public static GrabHandle HeldBy(IMiniGamePawn pawn)
        {
            if (pawn == null) return null;
            for (int hand = 0; hand < 2; hand++)
            {
                var c = pawn.Held(hand);
                if (c == null) continue;
                var h = c.GetComponentInParent<GrabHandle>();
                if (h != null) return h;
            }
            return null;
        }

        public static bool Holds(IMiniGamePawn pawn, GrabHandle handle)
        {
            if (pawn == null || handle == null) return false;
            for (int hand = 0; hand < 2; hand++)
            {
                var c = pawn.Held(hand);
                if (c != null && c.GetComponentInParent<GrabHandle>() == handle) return true;
            }
            return false;
        }

        // +1 pulling along PullAxis, -1 holding against it, 0 neither (§2.2.3-4).
        public int Effort(ICharacterDriver driver)
        {
            if (!(driver is IMiniGamePawn pawn) || pawn.Down || !Holds(pawn, this) || driver.FollowTarget == null) return 0;
            if (Vector3.Distance(driver.FollowTarget.position, transform.position) > Reach) return 0;
            var move = pawn.MoveInput;
            move.y = 0f;
            if (move.sqrMagnitude < .01f) return 0;
            var axis = PullAxis;
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-4f) return 0;
            float dot = Vector3.Dot(move.normalized, axis.normalized);
            return dot >= Along ? 1 : dot <= -Along ? -1 : 0;
        }
    }
}
