using UnityEngine;

namespace ChessFight.Game
{
    // The visual body of one pawn. Authored as a prefab so artists can replace the
    // capsule with a chess piece without touching networking code.
    //
    // This component never decides where a pawn is: it only smooths towards the
    // position the host already decided. Nothing here is authoritative.
    [DisallowMultipleComponent]
    public sealed class PawnAvatar : MonoBehaviour
    {
        [Header("Renderers tinted by team")]
        [SerializeField] Renderer body;
        [SerializeField] Renderer accent;

        [Header("Display smoothing")]
        [Tooltip("Higher converges on the networked position faster.")]
        [SerializeField] float positionSharpness = 18f;
        [SerializeField] float turnSpeed = 15f;
        [SerializeField] float turnThreshold = .002f;

        public ulong Id { get; private set; }
        public int Team { get; private set; } = -1;

        public void Bind(ulong id, int team, Material bodyMaterial, Material accentMaterial)
        {
            Id = id; Team = team;
            if (body != null && bodyMaterial != null) body.sharedMaterial = bodyMaterial;
            if (accent != null && accentMaterial != null) accent.sharedMaterial = accentMaterial;
        }

        public void Teleport(Vector3 position) => transform.position = position;

        public void Follow(Vector3 target, float dt)
        {
            Vector3 delta = target - transform.position;
            delta.y = 0;
            if (delta.sqrMagnitude > turnThreshold)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), turnSpeed * dt);
            transform.position = Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-positionSharpness * dt));
        }
    }
}
