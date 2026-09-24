using UnityEngine;

namespace ChessFight.Gameplay
{
    // Physics rate for scenes that host ragdolls, applied while the scene is
    // loaded and handed back when it unloads.
    //
    // The project default is 50 Hz with 6 solver iterations, which suits the
    // lobby. The ragdoll lab measured that below roughly 90 Hz the leg chain
    // cannot carry the body and the hips sink about 8 cm, and settled on 120 Hz
    // with 24 iterations. Without this, dropping the ragdoll prefab into a course
    // scene would make every pawn crouch.
    //
    // Runs first so that bodies spawned during Start get the new defaults. Bodies
    // already saved in the scene keep the project default; that is harmless for
    // kinematic obstacles and props.
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class PhysicsProfile : MonoBehaviour
    {
        [SerializeField] int physicsRate = 120;
        [SerializeField] int solverIterations = 24;

        float savedStep;
        int savedIterations;
        bool applied;

        void Awake()
        {
            savedStep = Time.fixedDeltaTime;
            savedIterations = Physics.defaultSolverIterations;
            Time.fixedDeltaTime = 1f / Mathf.Max(1, physicsRate);
            Physics.defaultSolverIterations = Mathf.Max(1, solverIterations);
            applied = true;
        }

        void OnDestroy()
        {
            if (!applied) return;
            Time.fixedDeltaTime = savedStep;
            Physics.defaultSolverIterations = savedIterations;
        }
    }
}
