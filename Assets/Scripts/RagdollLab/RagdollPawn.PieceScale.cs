using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R668 (승규 님 11-15, after the JY-piece-rig models came in: "비율을 폰 기준으로 맞춰주고 팔이 안보여" → option B of three
    /// still drawings): a piece's model is drawn to a height set against the pawn's own model, not to its sheet height.
    /// The new models stood 1.13 (rook) to 1.76 (king) times the pawn on a pawn-sized ragdoll, so their shoulders, arms and
    /// heads sat far above the bodies that move them. Now rook 1.04, knight 1.08, bishop 1.12, queen 1.16, king 1.2: the
    /// chess order stays, the gap to the body shrinks. The whole model is scaled (its shape kept) about the ground under the
    /// pawn, and so are its marks (shoulders, wrists, feet: RagdollVisualSync.SetPieceLook), so the arm stubs still leave
    /// the body where the model has them. The physics stays the pawn's.
    /// The nameplate (MatchNameplate) floats the same 0.5 m over the drawn top that it floats over the pawn's head.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>Drawn height of each piece over the pawn model's (1 = the pawn's).</summary>
        public static float PieceHeightOverPawn(PieceKind kind) => kind switch
        {
            PieceKind.Rook => 1.04f,
            PieceKind.Knight => 1.08f,
            PieceKind.Bishop => 1.12f,
            PieceKind.Queen => 1.16f,
            PieceKind.King => 1.2f,
            _ => 1f,
        };

        static readonly Dictionary<(Mesh, int), Mesh> scaledPieceMeshes = new Dictionary<(Mesh, int), Mesh>();

        // How much higher than the pawn the worn model's top is (m); 0 as a pawn.
        float drawnRise;

        /// <summary>How far above the head body the nameplate floats (m): the pawn's 0.5, plus what a piece stands taller.</summary>
        public float NameplateRise => 0.5f + drawnRise;

        // The mesh's height in this pawn's space (m), with the ground point under the pawn in the mesh's own space.
        float MeshHeight(Mesh mesh, out Vector3 groundInMesh)
        {
            Matrix4x4 toPawn = transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            groundInMesh = toPawn.inverse.MultiplyPoint3x4(Vector3.zero);
            Bounds b = mesh.bounds;
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
                float y = toPawn.MultiplyPoint3x4(corner).y;
                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
            }
            return Mathf.Max(0f, high - low);
        }

        /// <summary>
        /// The piece's mesh drawn to its height over <paramref name="pawn"/> (shared between pawns; the asset itself is never
        /// changed) and the factor it was scaled by. Without a pawn mesh to measure against, the mesh as it is.
        /// </summary>
        Mesh PieceMeshAtHeight(Mesh look, Mesh pawn, out float scale)
        {
            scale = 1f;
            if (look == null || pawn == null || skin == null) return look;
            float pawnHeight = MeshHeight(pawn, out _);
            float pieceHeight = MeshHeight(look, out Vector3 ground);
            if (pawnHeight < 0.1f || pieceHeight < 0.1f) return look;
            scale = PieceHeightOverPawn(piece) * pawnHeight / pieceHeight;
            drawnRise = Mathf.Max(0f, pieceHeight * scale - pawnHeight);
            if (Mathf.Abs(scale - 1f) < 0.005f) { scale = 1f; return look; }
            var key = (look, Mathf.RoundToInt(scale * 1000f));
            if (scaledPieceMeshes.TryGetValue(key, out var made) && made != null) return made;
            made = Instantiate(look);
            made.name = look.name + " (R668 x" + scale.ToString("0.00") + ")";
            var vertices = made.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = ground + (vertices[i] - ground) * scale;
            made.vertices = vertices;
            made.RecalculateBounds();
            scaledPieceMeshes[key] = made;
            return made;
        }
    }
}
