using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The warnings of a piece's ability (M12), drawn from what the pawn reports so a network puppet shows the
    /// same: the knight's landing spot while it flies, the rook's line while it winds up, the king's ring as
    /// the sceptre goes up and a flash when the shockwave goes off, a halo under a hovering bishop. Everyone
    /// sees them - that is what gives the other side its chance (DESIGN §6.1). Added to every pawn by
    /// RagdollPawn.Awake. No colliders: purely a picture.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PieceAbilityView : MonoBehaviour
    {
        static Material warnMaterial, flashMaterial, haloMaterial;

        RagdollPawn pawn;
        Transform spot, line, ring, halo;
        PieceMove last;
        float flash, since;

        void Awake()
        {
            pawn = GetComponent<RagdollPawn>();
            MakeMaterials();
            spot = Shape("Knight Landing", PrimitiveType.Cylinder, warnMaterial);
            line = Shape("Rook Line", PrimitiveType.Cube, warnMaterial);
            ring = Shape("Check Ring", PrimitiveType.Cylinder, warnMaterial);
            halo = Shape("Hover Halo", PrimitiveType.Cylinder, haloMaterial);
        }

        void OnDestroy()
        {
            foreach (var t in new[] { spot, line, ring, halo })
                if (t != null) Destroy(t.gameObject);
        }

        static void MakeMaterials()
        {
            if (warnMaterial != null) return;
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            warnMaterial = new Material(shader) { color = new Color(0.86f, 0.22f, 0.2f) };
            flashMaterial = new Material(shader) { color = new Color(1f, 0.85f, 0.3f) };
            haloMaterial = new Material(shader) { color = new Color(0.72f, 0.93f, 1f) };
        }

        Transform Shape(string name, PrimitiveType type, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.SetActive(false);
            return go.transform;
        }

        void LateUpdate()
        {
            if (pawn == null || pawn.bodies == null || pawn.bodies[0] == null) return;
            PieceMove move = pawn.Move;
            Vector3 hips = pawn.bodies[0].position;
            Vector3 feet = hips - Vector3.up * pawn.standHeight;
            if (last == PieceMove.KingCheck && move != PieceMove.KingCheck) flash = 0.3f;
            // Timed here, not read off the pawn: a network puppet has no clock for its moves.
            since = move == last ? since + Time.deltaTime : 0f;
            last = move;
            flash -= Time.deltaTime;

            // The knight's landing spot: a red disc where it will come down.
            bool leaping = move == PieceMove.KnightLeap;
            spot.gameObject.SetActive(leaping);
            if (leaping)
            {
                spot.position = pawn.MoveTarget + Vector3.up * 0.04f;
                spot.localScale = new Vector3(1.4f, 0.02f, 1.4f);
            }

            // The rook's line: a red strip from its feet to where the charge will stop.
            bool winding = move == PieceMove.RookWindup;
            line.gameObject.SetActive(winding);
            if (winding)
            {
                Vector3 from = feet + Vector3.up * 0.05f, to = pawn.MoveTarget - Vector3.up * pawn.standHeight + Vector3.up * 0.05f;
                Vector3 d = to - from;
                if (d.sqrMagnitude < 1e-4f) d = Vector3.forward * 0.1f;
                line.position = (from + to) * 0.5f;
                line.rotation = Quaternion.LookRotation(d.normalized, Mathf.Abs(d.normalized.y) > 0.9f ? pawn.Facing : Vector3.up);
                line.localScale = new Vector3(0.5f, 0.04f, d.magnitude);
            }

            // The king's ring: grows to the shockwave's reach while the sceptre is up, then a gold flash.
            bool checking = move == PieceMove.KingCheck;
            ring.gameObject.SetActive(checking || flash > 0f);
            if (checking || flash > 0f)
            {
                float grow = checking ? Mathf.Clamp01(since / PieceAbilities.CheckWindup) : 1f;
                float d = 2f * PieceAbilities.CheckRadius * Mathf.Lerp(0.3f, 1f, grow);
                ring.position = (checking ? feet : pawn.MoveTarget - Vector3.up * pawn.standHeight) + Vector3.up * 0.03f;
                ring.localScale = new Vector3(d, 0.02f, d);
                ring.GetComponent<MeshRenderer>().sharedMaterial = checking ? warnMaterial : flashMaterial;
            }

            // A pale halo under a hovering bishop.
            bool hovering = move == PieceMove.BishopHover;
            halo.gameObject.SetActive(hovering);
            if (hovering)
            {
                halo.position = feet - Vector3.up * 0.1f;
                halo.localScale = new Vector3(1.6f, 0.02f, 1.6f);
            }
        }
    }
}
