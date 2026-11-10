using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R343 (승규 11-04, "회전 애니메이션 넣은 다음"): turns the royal fan's rotor (RoyalFan.fbx, royal_fan_v35.py) with its
    // vent - idling slowly, spinning up through the warning, at full speed while it blows, running down after. About the
    // vent's front axis through the rotor's origin (the hub's centre). A picture only, like the hub chandelier's sway:
    // nothing touches the blades (the vent's housing collider is the blockout's), so it reads Time.deltaTime.
    public sealed partial class RoyalFanSpin : MonoBehaviour
    {
        [SerializeField] Transform rotor;
        [SerializeField] AirVent vent;
        [Tooltip("Degrees a second: resting, warning, blowing.")]
        [SerializeField] float idle = 40f, warning = 480f, blowing = 1150f;
        [Tooltip("How quickly it reaches the speed it is going to (a second's share).")]
        [SerializeField] float spinUp = 2.2f, spinDown = .9f;

        Quaternion rest;
        Vector3 axis;
        float speed, angle;

        public void Configure(Transform turning, AirVent from)
        {
            rotor = turning;
            vent = from;
        }

        void Start()
        {
            if (rotor == null) { enabled = false; return; }
            rest = rotor.localRotation;
            // The fan's front (this model's +z) in the rotor's parent's space.
            axis = rotor.parent != null ? rotor.parent.InverseTransformDirection(transform.forward).normalized : Vector3.forward;
            speed = idle;
        }

        void Update()
        {
            var state = vent != null ? vent.State : AirVentState.Idle;
            float target = state == AirVentState.Blowing ? blowing : state == AirVentState.Warning ? warning : idle;
            float rate = target > speed ? spinUp : spinDown;
            speed = Mathf.Lerp(speed, target, 1f - Mathf.Exp(-rate * Time.deltaTime));
            angle = Mathf.Repeat(angle + speed * Time.deltaTime, 360f);
            rotor.localRotation = Quaternion.AngleAxis(angle, axis) * rest;
            Sound(state);
        }
    }
}
