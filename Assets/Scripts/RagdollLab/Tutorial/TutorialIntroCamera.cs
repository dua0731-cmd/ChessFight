using ChessFight.Game;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// On the player's camera for the opening's ride (added by TutorialIntro): after the orbit camera has placed itself
    /// (its order is 150), this puts the camera where TutorialIntro.ViewFromHead says - between the yard and the eagle's
    /// head, looking at the head and the rider. The mouse swings it a little round them (user 11-25: "마우스로 약간만
    /// 움직일 수 있게"): 26 degrees to either side, 12 down and 16 up. When the ride ends it eases over 0.7 s from that last
    /// view to the orbit camera's own, then removes itself.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class TutorialIntroCamera : MonoBehaviour
    {
        const float Sideways = 26f, Down = 12f, Up = 16f, MouseDegrees = 2f;

        public TutorialIntro intro;
        Vector3 lastPosition;
        Quaternion lastRotation = Quaternion.identity;
        bool held;
        float released = -1f, yaw, pitch;

        void LateUpdate()
        {
            if (intro == null) { Destroy(this); return; }
            if (TutorialIntro.Riding)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    yaw = Mathf.Clamp(yaw + GameSettings.LookX * MouseDegrees, -Sideways, Sideways);
                    pitch = Mathf.Clamp(pitch - GameSettings.LookY * MouseDegrees, -Down, Up);
                }
                intro.ViewFromHead(yaw, pitch, out lastPosition, out lastRotation);
                held = true;
                transform.SetPositionAndRotation(lastPosition, lastRotation);
                return;
            }
            if (!held) { Destroy(this); return; }
            if (released < 0f) released = Time.unscaledTime;
            float x = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - released) / 0.7f);
            if (x >= 1f) { Destroy(this); return; }
            transform.SetPositionAndRotation(Vector3.Lerp(lastPosition, transform.position, x), Quaternion.Slerp(lastRotation, transform.rotation, x));
        }
    }
}
