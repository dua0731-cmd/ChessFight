using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class SwordFightGame
    {
        bool swordLookActive;
        float normalSensitivity;
        Vector2 lookTarget, lookVelocity;

        // A bounded lazy drag: preserve mouse distance, ease into it, then catch up on release
        // of mouse movement. Only this mode temporarily consumes the shared camera's input.
        public void StepSwordLook(bool active, Vector2 mouse, float dt)
        {
            if (CameraRig == null) return;
            if (!active || CameraRig.freeMode) { ResetSwordLook(); return; }
            if (!swordLookActive)
            {
                normalSensitivity = CameraRig.mouseSensitivity;
                lookTarget = new Vector2(CameraRig.yaw, CameraRig.pitch);
                lookVelocity = Vector2.zero; swordLookActive = true;
            }
            CameraRig.mouseSensitivity = 0;
            Vector2 shown = new Vector2(CameraRig.yaw, CameraRig.pitch);
            lookTarget += mouse * normalSensitivity;
            lookTarget.y = Mathf.Clamp(lookTarget.y, CameraRig.minPitch, CameraRig.maxPitch);
            // A stalled frame or huge mouse fling must not queue seconds of unwanted spins.
            lookTarget = shown + Vector2.ClampMagnitude(lookTarget - shown, 38f);
            float mass = Local != null ? Local.SwordBody.mass : .18f;
            float speed = Local != null && Local.Drawn ? Local.SwordBody.angularVelocity.magnitude : 0;
            float load = Local != null && Local.Drawn ? Vector3.Angle(Local.BladeTip - Local.BladeRoot, SwordFightPawn.GuardDirection(CameraRig.AimForward)) : 0;
            float response = .10f * Mathf.Sqrt(Mathf.Clamp(mass / .18f, .5f, 3f)) + .025f * Mathf.Clamp01(speed / 8f) + .025f * Mathf.Clamp01(load / 60f);
            shown = Vector2.SmoothDamp(shown, lookTarget, ref lookVelocity, response, Mathf.Infinity, Mathf.Clamp(dt, .001f, .05f));
            CameraRig.yaw = shown.x; CameraRig.pitch = Mathf.Clamp(shown.y, CameraRig.minPitch, CameraRig.maxPitch);
        }

        public void ResetSwordLook()
        {
            if (swordLookActive && CameraRig != null) CameraRig.mouseSensitivity = normalSensitivity;
            swordLookActive = false; lookVelocity = Vector2.zero;
        }
    }
}
