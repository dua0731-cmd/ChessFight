using UnityEngine;
using UnityEngine.InputSystem;

namespace ChessFight.ProtectKing
{
    // One controlled player at a time. The other eleven slots are collision dummies, not network peers.
    public sealed class LocalPlaySession : MonoBehaviour
    {
        public StageMap map;
        public InputActionAsset inputActions;
        public Camera viewCamera;
        public int selectedPlayer;
        public float cameraDistance = 11;
        InputActionAsset instance;
        InputAction move, look, jump, interact;
        float yaw;
        float pitch = 30;
        bool cameraSnap = true;
        public PlayerMotor Active => map.players[selectedPlayer].GetComponent<PlayerMotor>();

        void Awake()
        {
            instance = Instantiate(inputActions);
            var actions = instance.FindActionMap("Player", true);
            move = actions.FindAction("Move", true);
            look = actions.FindAction("Look", true);
            jump = actions.FindAction("Jump", true);
            interact = actions.FindAction("Interact", true);
            actions.Enable();
            SelectPlayer(selectedPlayer);
        }

        void OnDestroy()
        {
            if (instance != null) { instance.Disable(); Destroy(instance); }
        }

        public void SelectPlayer(int index)
        {
            selectedPlayer = Mathf.Clamp(index, 0, map.players.Length - 1);
            for (int i = 0; i < map.players.Length; i++)
            {
                var motor = map.players[i].GetComponent<PlayerMotor>();
                motor.IsLocal = i == selectedPlayer;
                motor.SetInput(Vector2.zero, false);
            }
            yaw = 0;
            cameraSnap = true;
            map.match.throne.ResetProgress();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.enterKey.wasPressedThisFrame && !map.match.IsRunning) map.match.StartMatch();
                if (keyboard.tabKey.wasPressedThisFrame) SelectPlayer((selectedPlayer + 1) % map.players.Length);
                if (keyboard.rKey.wasPressedThisFrame && map.match.IsRunning) map.Respawn(Active);
            }
            bool mouseLook = Mouse.current != null && Mouse.current.rightButton.isPressed;
            if (mouseLook)
            {
                var delta = look.ReadValue<Vector2>();
                yaw += delta.x * .13f;
                pitch = Mathf.Clamp(pitch - delta.y * .1f, 15, 65);
            }
            else if (Gamepad.current != null)
            {
                var stick = Gamepad.current.rightStick.ReadValue();
                yaw += stick.x * 110 * Time.deltaTime;
                pitch = Mathf.Clamp(pitch - stick.y * 80 * Time.deltaTime, 15, 65);
            }
            Active.ViewYaw = yaw;
            Active.SetInput(move.ReadValue<Vector2>(), jump.WasPressedThisFrame());
            // IsPressed reads physical hold state; the existing Input Action's Hold delay is not added twice.
            map.match.throne.TickInteraction(Active.Identity, interact.IsPressed(), Time.deltaTime);
        }

        void LateUpdate()
        {
            var focus = Active.transform.position + Vector3.up * 1.25f;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var direction = rotation * Vector3.back;
            float distance = cameraDistance;
            if (Physics.SphereCast(focus, .25f, direction, out var hit, distance, 1 << 0, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(.7f, hit.distance - .2f);
            var desired = focus + direction * distance;
            viewCamera.transform.position = cameraSnap ? desired :
                Vector3.Lerp(viewCamera.transform.position, desired, 1 - Mathf.Exp(-12 * Time.deltaTime));
            viewCamera.transform.rotation = Quaternion.LookRotation(focus - viewCamera.transform.position);
            cameraSnap = false;
        }
    }
}
