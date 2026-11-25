using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The ragdoll's side of <see cref="ISummonCarry"/> (the throne contest's summons onto the chessboard, JY 2026-11-22):
    /// the outer flight (<see cref="RagdollPawn.Fly"/>, the king's call's way of carrying a teammate) takes the hips along
    /// the way, its bodies touching nothing, eased - off slowly, fast, slowing onto the spot. There it is stood facing the
    /// way it was told and held by the same flight (touching things again) until let go, so no key moves it before the
    /// contest starts. It counts as carried (<see cref="RagdollDriver.Carried"/>), so the runaway respawn leaves it alone.
    /// Added to a pawn the first time it is carried; runs before the pawn's own step, so the flight is asked for every
    /// step it lasts. On the host only (a puppet's flight does nothing).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class RagdollSummonCarry : MonoBehaviour, ISummonCarry
    {
        RagdollPawn pawn;
        Rigidbody[] bodies;
        Vector3[] way;      // hips points
        float[] along;      // the way's length up to each point
        float seconds, t, length;
        Vector3 face;
        bool carrying, holding;

        public bool Carrying => carrying;
        public bool Holding => holding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            SummonCarry.For = driver =>
            {
                if (!(driver is RagdollDriver rd) || rd.Pawn == null) return null;
                var c = rd.GetComponent<RagdollSummonCarry>();
                return c != null ? c : rd.gameObject.AddComponent<RagdollSummonCarry>();
            };
        }

        public void Carry(Vector3[] ground, float secs, Vector3 faceDirection)
        {
            pawn = GetComponent<RagdollPawn>();
            if (pawn == null || pawn.NetworkPuppet || ground == null || ground.Length < 2) return;
            bodies = pawn.GetComponentsInChildren<Rigidbody>();
            way = new Vector3[ground.Length];
            along = new float[ground.Length];
            way[0] = pawn.Hips.position;
            for (int i = 1; i < ground.Length; i++) way[i] = ground[i] + Vector3.up * pawn.standHeight;
            length = 0f;
            for (int i = 1; i < way.Length; i++)
            {
                length += Vector3.Distance(way[i - 1], way[i]);
                along[i] = length;
            }
            seconds = Mathf.Max(.2f, secs);
            t = 0f;
            face = faceDirection.sqrMagnitude > .01f ? faceDirection : transform.forward;
            carrying = true;
            holding = false;
            Solid(false);
        }

        public void Release()
        {
            if (carrying) Solid(true);
            if ((carrying || holding) && pawn != null) pawn.EndFlight(Vector3.zero);
            carrying = holding = false;
        }

        void OnDisable() => Release();

        void FixedUpdate()
        {
            if (pawn == null || (!carrying && !holding)) return;
            float dt = Time.fixedDeltaTime;
            Vector3 end = way[way.Length - 1];
            if (carrying)
            {
                t += dt;
                if (t < seconds)
                {
                    Vector3 p = At(Ease(t / seconds)), q = At(Ease(Mathf.Min(1f, (t + dt) / seconds)));
                    pawn.Fly(p, (q - p) / dt, 0f, default, 18f);
                    return;
                }
                // There: touching things again, stood on the spot facing the board, then held.
                carrying = false;
                holding = true;
                Solid(true);
                pawn.EndFlight(Vector3.zero);
                pawn.Teleport(end, face);
            }
            pawn.Fly(end, Vector3.zero, 0f, default, 18f);
        }

        void Solid(bool solid)
        {
            if (bodies == null) return;
            foreach (var rb in bodies)
                if (rb != null) rb.detectCollisions = solid;
        }

        // Off slowly, fast midway, slowing onto the spot.
        static float Ease(float u) => u < .5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) * .5f;

        Vector3 At(float u)
        {
            float d = u * length;
            for (int i = 1; i < way.Length; i++)
                if (d <= along[i] || i == way.Length - 1)
                {
                    float seg = along[i] - along[i - 1];
                    return Vector3.Lerp(way[i - 1], way[i], seg > 1e-4f ? Mathf.Clamp01((d - along[i - 1]) / seg) : 1f);
                }
            return way[way.Length - 1];
        }
    }
}
