using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class ChessTimerMinigame
    {
        readonly GameObject[] detailedPieceAssets = new GameObject[5];
        readonly bool[] pieceAssetChecked = new bool[5];
        Material ivoryPiece, ebonyPiece, brassPiece, carvedDetail;
        bool BuildDetailedPiece(Vector3 position, ChessWirePuzzle.Kind kind, string coordinate, bool black)
        {
            int index = (int)kind;
            if (!pieceAssetChecked[index])
            {
                detailedPieceAssets[index] = Resources.Load<GameObject>("ChessTimerPieces/" + kind);
                pieceAssetChecked[index] = true;
                if (detailedPieceAssets[index] == null)
                    Debug.LogWarning("Missing detailed chess model: " + kind + ". Using prototype geometry.", this);
            }
            if (detailedPieceAssets[index] == null) return false;
            if (ivoryPiece == null)
            {
                ivoryPiece = Mat(new Color(.89f, .83f, .70f)); ivoryPiece.SetFloat("_Glossiness", .42f);
                ebonyPiece = Mat(new Color(.095f, .12f, .15f)); ebonyPiece.SetFloat("_Glossiness", .48f);
                ebonyPiece.SetFloat("_Metallic", .12f);
                brassPiece = Mat(new Color(.65f, .43f, .17f)); brassPiece.SetFloat("_Metallic", .65f);
                brassPiece.SetFloat("_Glossiness", .43f);
                carvedDetail = Mat(new Color(.018f, .025f, .035f)); carvedDetail.SetFloat("_Glossiness", .25f);
            }
            var root = new GameObject("Puzzle " + kind + " detailed"); root.transform.SetParent(transform, false);
            root.transform.position = position;
            var model = Instantiate(detailedPieceAssets[index], root.transform, false);
            model.name = kind + " sculpted model";
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(root); return false; }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var slots = renderer.sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    string slotName = slots[slot] == null ? "" : slots[slot].name;
                    slots[slot] = slotName.Contains("Trim") ? brassPiece : slotName.Contains("Dark") ? carvedDetail :
                        black ? ebonyPiece : ivoryPiece;
                }
                renderer.sharedMaterials = slots;
            }
            // Normalize FBX units without changing the established 2.8 m base footprint or board spacing.
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            if (width < .001f) { Destroy(root); return false; }
            model.transform.localScale = Vector3.one * (2.8f / width);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            float height = bounds.size.y;
            // Decorative detail never adds small snagging colliders to the helper routes.
            var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up * (height * .5f);
            collider.size = new Vector3(2.8f, height, 2.8f);
            float scale = Mathf.Clamp(rookScale, .2f, 1f); root.transform.localScale = Vector3.one * scale;
            string title = kind == ChessWirePuzzle.Kind.Rook ? "룩" : kind == ChessWirePuzzle.Kind.Bishop ? "비숍" :
                kind == ChessWirePuzzle.Kind.Knight ? "나이트" : kind == ChessWirePuzzle.Kind.Queen ? "퀸" : "폰";
            Label("Piece name and square", position + Vector3.up * (height * scale + .35f), title + " · " + coordinate, .26f, 2.8f, .4f);
            if (kind == ChessWirePuzzle.Kind.Pawn)
            {
                var marker = Box("Pawn forward marker", position + new Vector3(0, .08f, 1.02f * scale),
                    new Vector3(.18f, .06f, .45f), toolGrip, false);
                marker.transform.SetParent(root.transform, true);
            }
            return true;
        }
    }
}
