using UnityEngine;

namespace ChessFight.PawnRush
{
    // R459 (승규 11-05 "사운드가 지하실이나 계단 쪽에 있으면 약간의 방음이 되야 해"): the war room's own sounds (the
    // cannons, the conveyor, its jolt) heard through stone while the listener is down in the lower floor or out on the
    // return stairs and their gallery - a little quieter and without their highs, as through a floor or a wall.
    // One per library, on its pivot (Course01v4Builder.LibraryReplacement); the sounds find it among their parents
    // (NearFarSound, LibraryAirCannon) and read Amount every frame.
    public sealed class LibraryMuffle : MonoBehaviour
    {
        const float Quieter = .55f;      // the level fully muffled
        const float Cutoff = 1200f;      // the low-pass cutoff fully muffled (22 kHz is open)
        const float Fade = 4f;           // how fast it follows the listener (1/s)

        float amount;
        static AudioListener listener;

        // 0 in the hall, 1 in the lower floor or on the outside stairs, smoothed.
        public float Amount => amount;

        public static float Gain(float m) => Mathf.Lerp(1f, Quieter, m);

        // Down from 22 kHz as a ratio, so half muffled is halfway by ear.
        public static float CutoffAt(float m) => 22000f * Mathf.Pow(Cutoff / 22000f, Mathf.Clamp01(m));

        void Update()
        {
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            float target = listener != null ? Below(transform.InverseTransformPoint(listener.transform.position)) : 0f;
            amount = Mathf.Lerp(amount, target, 1f - Mathf.Exp(-Fade * Time.deltaTime));
        }

        // In the pivot's space (as LibraryReplacement's colliders): the lower floor's top is at -8 and the start
        // floor's at 0 inside the side walls (|x| 15.48); outside them the return stairs climb from -8 to 0 and the
        // north stairs' gallery runs at 0. The camera rides a few metres over its pawn, so the outside counts up to 4.
        static float Below(Vector3 p)
        {
            if (Mathf.Abs(p.z) > 27f) return 0f;
            float ax = Mathf.Abs(p.x);
            if (ax < 15.2f) return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-3f, -1f, p.y));
            if (ax < 24f) return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 6f, p.y));
            return 0f;
        }
    }
}
