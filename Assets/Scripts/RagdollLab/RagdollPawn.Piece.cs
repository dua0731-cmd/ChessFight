using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Promotion and what each piece is made of (Queen of the Hill M11; RagdollDriver forwards IPromotable
    /// here). The ragdoll stays the pawn's - the same size and the same physics - and a piece only scales it:
    /// pushes, hits, tackles and bumps by 1 / weight, the run and the sprint by its move, climbing walls and
    /// ropes by its climb; the king may not sprint and shrugs off squashes and staggers. A piece wears its own
    /// model where there is one (ShowPieceModel); otherwise a small gold marker on the head shows which it is.
    /// </summary>
    public partial class RagdollPawn
    {
        PieceKind piece = PieceKind.Pawn;
        PieceStats pieceStats = ChessPieces.Stats(PieceKind.Pawn);
        Transform pieceMarker;
        static Material markerMaterial;
        // While a piece's mesh is worn: the pawn's own, to put back.
        Mesh pawnMesh;
        Material pawnSkinMaterial;

        public PieceKind Piece => piece;
        public PieceStats PieceStats => pieceStats;
        public float Weight => pieceStats.Weight;
        public int Promotions { get; private set; }

        /// <summary>How far a push moves this piece against a pawn: 1 / weight.</summary>
        internal float PushScale => 1f / Mathf.Max(0.1f, pieceStats.Weight);

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

        /// <summary>The piece's own model where there is one (Resources/PieceModels), else a small gold marker on
        /// the head: battlements, a mitre, a horse's head, a cross, a crown.</summary>
        void ShowPiece()
        {
            if (pieceMarker != null) Destroy(pieceMarker.gameObject);
            pieceMarker = null;
            if (piece == PieceKind.Pawn || !ShowPieceModel()) ShowPawnMesh();
            if (piece == PieceKind.Pawn || skin != null && pawnMesh != null) return;
            Transform head = bodies[(int)BodyId.Head].transform;
            var headCollider = bodies[(int)BodyId.Head].GetComponent<Collider>();
            float top = headCollider != null ? headCollider.bounds.extents.y : 0.12f;
            pieceMarker = new GameObject($"Piece ({piece})").transform;
            pieceMarker.SetParent(head, false);
            pieceMarker.localPosition = head.InverseTransformDirection(Vector3.up) * top;
            pieceMarker.localRotation = Quaternion.Inverse(head.rotation) * Quaternion.LookRotation(Flat(facing).normalized, Vector3.up);
            if (markerMaterial == null)
            {
                markerMaterial = new Material(LitMaterials.Lit) { color = new Color(0.95f, 0.78f, 0.25f) };
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

        /// <summary>
        /// The artist's model for this piece, worn on the pawn's own skeleton: Resources/PieceModels/&lt;piece&gt;
        /// is a prefab whose SkinnedMeshRenderer holds the piece mesh weighted onto the pawn's Mixamo bones,
        /// part by part (body by height, each stub on its arm, each mitten on its hand, each foot on its
        /// foot, a short leg between foot and base), in the pawn skin's space and with the pawn's bind poses
        /// (Art/Pieces/*_Rigged.fbx, baked in the editor). RagdollVisualSync keeps driving the same bones,
        /// as a stiffer wooden piece with its arms on its own shoulders and its feet under its base, from
        /// the prefab's marks (RagdollVisualSync.PoseAsPiece). A piece without a model keeps the marker.
        /// The pawn's own mesh, material and pose come back when it is a pawn again.
        /// </summary>
        bool ShowPieceModel()
        {
            if (skin == null) return false;
            var prefab = Resources.Load<GameObject>("PieceModels/" + piece);
            var look = prefab != null ? prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (look == null || look.sharedMesh == null) return false;
            if (look.sharedMesh.bindposes.Length != skin.sharedMesh.bindposes.Length && pawnMesh == null) return false;
            if (pawnMesh == null)
            {
                pawnMesh = skin.sharedMesh;
                pawnSkinMaterial = skin.sharedMaterial;
            }
            SwapSkinMesh(look.sharedMesh);
            skin.sharedMaterial = look.sharedMaterial;
            if (VisualSync != null) VisualSync.SetPieceLook(look.transform);
            return true;
        }

        void ShowPawnMesh()
        {
            if (skin == null || pawnMesh == null) return;
            SwapSkinMesh(pawnMesh);
            skin.sharedMaterial = pawnSkinMaterial;
            pawnMesh = null;
            pawnSkinMaterial = null;
            if (VisualSync != null) VisualSync.ClearPieceLook();
        }

        // The skin is switched off for the swap so its GPU skinning buffer is made again for the new mesh: swapped
        // while on, a render in the same frame (a capture or a render texture) still has the old mesh's vertex count
        // and Unity stops drawing the skin ("does not match the expected mesh data size and vertex stride").
        void SwapSkinMesh(Mesh mesh)
        {
            bool on = skin.enabled;
            skin.enabled = false;
            skin.sharedMesh = mesh;
            skin.enabled = on;
        }

        RagdollVisualSync visualSync;
        RagdollVisualSync VisualSync => visualSync != null ? visualSync : visualSync = GetComponent<RagdollVisualSync>();

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
