using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Promotion and what each piece is made of (Queen of the Hill M11; RagdollDriver forwards IPromotable
    /// here). The ragdoll stays the pawn's - the same size and the same physics - and a piece only scales it:
    /// pushes, hits, tackles and bumps by 1 / weight, the run and the sprint by its move, climbing walls and
    /// ropes by its climb; the king may not sprint and shrugs off squashes and staggers. Until the pieces have
    /// their own art, a small gold marker on the head shows which piece it is.
    /// </summary>
    public partial class RagdollPawn
    {
        PieceKind piece = PieceKind.Pawn;
        PieceStats pieceStats = ChessPieces.Stats(PieceKind.Pawn);
        Transform pieceMarker;
        static Material markerMaterial;

        public PieceKind Piece => piece;
        public PieceStats PieceStats => pieceStats;
        public float Weight => pieceStats.Weight;
        public int Promotions { get; private set; }

        /// <summary>How far a push moves this piece against a pawn: 1 / weight.</summary>
        float PushScale => 1f / Mathf.Max(0.1f, pieceStats.Weight);

        float ClimbScale => pieceStats.Climb;

        /// <summary>This pawn's share of a bump with <paramref name="other"/>: the lighter one bounces further.</summary>
        float BumpShare(RagdollPawn other) => 2f * other.Weight / Mathf.Max(0.1f, Weight + other.Weight);

        public void SetPiece(PieceKind kind)
        {
            if (!ChessPieces.IsValid((int)kind)) return;
            if (kind != piece) Promotions++;
            piece = kind;
            pieceStats = ChessPieces.Stats(kind);
            StatusImmune = pieceStats.StatusImmune;
            if (!pieceStats.Sprint) Sprinting = false;
            // The hook, en passant and the bells are the pawn's.
            if (kind != PieceKind.Pawn && hookPhase != HookPhase.None && !NetworkPuppet)
            {
                EndClimbPose();
                ClearHook();
            }
            ShowPiece();
        }

        /// <summary>A small gold marker on the head: battlements, a mitre, a horse's head, a cross, a crown.</summary>
        void ShowPiece()
        {
            if (pieceMarker != null) Destroy(pieceMarker.gameObject);
            pieceMarker = null;
            if (piece == PieceKind.Pawn) return;
            Transform head = bodies[(int)BodyId.Head].transform;
            var headCollider = bodies[(int)BodyId.Head].GetComponent<Collider>();
            float top = headCollider != null ? headCollider.bounds.extents.y : 0.12f;
            pieceMarker = new GameObject($"Piece ({piece})").transform;
            pieceMarker.SetParent(head, false);
            pieceMarker.localPosition = head.InverseTransformDirection(Vector3.up) * top;
            pieceMarker.localRotation = Quaternion.Inverse(head.rotation) * Quaternion.LookRotation(Flat(facing).normalized, Vector3.up);
            if (markerMaterial == null)
            {
                var shader = Shader.Find("Standard");
                markerMaterial = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Diffuse")) { color = new Color(0.95f, 0.78f, 0.25f) };
            }
            switch (piece)
            {
                case PieceKind.Rook:
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.2f, 0.03f, 0.2f));
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f;
                        Part(PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.08f, 0.08f, Mathf.Sin(a) * 0.08f), Vector3.one * 0.05f);
                    }
                    break;
                case PieceKind.Bishop:
                    Part(PrimitiveType.Capsule, new Vector3(0f, 0.09f, 0f), new Vector3(0.1f, 0.09f, 0.1f));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), Vector3.one * 0.045f);
                    break;
                case PieceKind.Knight:
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.07f, 0.05f), new Vector3(0.08f, 0.1f, 0.18f));
                    Part(PrimitiveType.Cube, new Vector3(0.03f, 0.14f, -0.02f), new Vector3(0.03f, 0.06f, 0.03f));
                    Part(PrimitiveType.Cube, new Vector3(-0.03f, 0.14f, -0.02f), new Vector3(0.03f, 0.06f, 0.03f));
                    break;
                case PieceKind.King:
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.04f, 0.2f, 0.04f));
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.14f, 0f), new Vector3(0.14f, 0.04f, 0.04f));
                    break;
                case PieceKind.Queen:
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 0.4f;
                        Part(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.08f, 0.06f, Mathf.Sin(a) * 0.08f), Vector3.one * 0.05f);
                    }
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.11f, 0f), Vector3.one * 0.06f);
                    break;
            }
        }

        void Part(PrimitiveType shape, Vector3 at, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(shape);
            // Gone before it is parented: under the head's body even for one step it would be part of the head.
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
            go.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;
            go.transform.SetParent(pieceMarker, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
        }
    }
}
