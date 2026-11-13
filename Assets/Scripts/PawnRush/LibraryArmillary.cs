using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // The library's armillary turns (R324, 승규 님: "외곽 쪽은 교차하면서 움직이고 내부 구는 사선 방향으로"); since R327 it
    // is one, Start_armillary, 1.6 times its size beside the start's war map (the goal's two are gone).
    // ArtSource/PawnRush/Library/armillary.py rebuilt it as a gimbal: Ring1 (outer meridian) turns about the
    // post, Ring2 (equator) is pinned to Ring1's side blocks and rolls about that pin, Ring3 (inner meridian) is
    // pinned to Ring2 and turns the other way, so the rings keep crossing; the sphere spins about a diagonal axis.
    // Each part turns about the line from its pivot to its "<part>_axis" empty, which sits in the part's parent.
    // Angles come from the shared obstacle clock, so every PC sees the same pose (mirrored turns the other way).
    // Ring2 and Ring3 speed up and ease off a little (never stopping) so the motion does not look like a motor.
    public sealed class LibraryArmillary : MonoBehaviour
    {
        // degrees per second; Ring2 / Ring3 swing their pace by Ease degrees over EasePeriod seconds
        const float Ring1Speed = 9f, Ring2Speed = -15f, Ring3Speed = 22f, SphereSpeed = 30f;
        const float Ring2Ease = 12f, Ring2EasePeriod = 23f, Ring3Ease = 16f, Ring3EasePeriod = 17f;

        [SerializeField] Transform[] parts = new Transform[4];   // Ring1, Ring2, Ring3, Sphere
        [SerializeField] Transform[] axes = new Transform[4];    // their axis marks
        [SerializeField] float mirror = 1f;

        Quaternion[] rest;
        Vector3[] axisLocal;

        public void Configure(Transform ring1, Transform ring2, Transform ring3, Transform sphere, Transform[] marks, bool mirrored)
        {
            parts = new[] { ring1, ring2, ring3, sphere };
            axes = marks;
            mirror = mirrored ? -1f : 1f;
        }

        void Awake()
        {
            rest = new Quaternion[parts.Length];
            axisLocal = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || axes[i] == null) { enabled = false; return; }
                rest[i] = parts[i].localRotation;
                // The mark and the part share a parent, so this is the axis in the parent's space.
                axisLocal[i] = (axes[i].localPosition - parts[i].localPosition).normalized;
            }
        }

        static float Eased(float speed, float ease, float period, float t) =>
            speed * t + ease * Mathf.Sin(t * 2f * Mathf.PI / period);

        void Update()
        {
            // Wrapped to a day so the float angles keep their precision (whole turns there, the ease nudges once a day).
            float t = (float)(ObstacleClock.Now % 86400.0);
            Turn(0, Ring1Speed * t);
            Turn(1, Eased(Ring2Speed, Ring2Ease, Ring2EasePeriod, t));
            Turn(2, Eased(Ring3Speed, Ring3Ease, Ring3EasePeriod, t));
            Turn(3, SphereSpeed * t);
        }

        void Turn(int i, float degrees) =>
            parts[i].localRotation = Quaternion.AngleAxis(Mathf.Repeat(degrees * mirror, 360f), axisLocal[i]) * rest[i];
    }
}
