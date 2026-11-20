using UnityEngine;

namespace ChessFight.PawnRush
{
    // Turns a planet's pivot about its own (leaning) north axis; the pivot's parent is the orbit ring, which carries it round.
    public sealed class RoyalObservatoryPlanetSpin : MonoBehaviour
    {
        [SerializeField] float tilt, speed, angle;

        public void Configure(float tiltDegrees, float degreesPerSecond, float startAngle)
        {
            tilt = tiltDegrees;
            speed = degreesPerSecond;
            angle = startAngle;
            Apply();
        }

        void Update()
        {
            angle = Mathf.Repeat(angle + speed * Time.deltaTime, 360f);
            Apply();
        }

        void Apply() => transform.localRotation = Quaternion.AngleAxis(tilt, Vector3.right) * Quaternion.AngleAxis(angle, Vector3.up);
    }
}
