using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.PawnRush
{
    public sealed class SampledGardenSurface : MonoBehaviour, IMovingSurface
    {
        SampledGardenMotion driver;
        Matrix4x4 previous, current;
        Quaternion previousRotation, currentRotation;
        public void Configure(SampledGardenMotion motion) { driver = motion; ResetPose(); }
        public void ResetPose() { previous = current = transform.localToWorldMatrix; previousRotation = currentRotation = transform.rotation; }
        public void BeforeStep() { previous = current; previousRotation = currentRotation; }
        public void AfterStep() { current = transform.localToWorldMatrix; currentRotation = transform.rotation; }
        public Vector3 PointVelocity(Vector3 point)
        {
            if (driver != null) driver.Step();
            Vector3 local = current.inverse.MultiplyPoint3x4(point);
            return (current.MultiplyPoint3x4(local) - previous.MultiplyPoint3x4(local)) / Mathf.Max(Time.fixedDeltaTime, .0001f);
        }
        public Quaternion DeltaRotation { get { if (driver != null) driver.Step(); return currentRotation * Quaternion.Inverse(previousRotation); } }
    }
}
