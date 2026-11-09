using UnityEngine;

namespace ChessFight.Gameplay
{
    // Development-build cheats (DECISIONS K16: the editor and development builds only).
    //
    // R529 (user 11-09: "f1키 자유카메라에서 캐릭터 비행으로 치트 변경"): F1 flies the player's own pawn (ICheatFlight),
    // where it used to fly the camera alone; the free camera moved to F4 (OrbitCamera.FlyKey, the ragdoll lab's key too).
    // Offline the playtest spawner toggles it; online the match link asks the host, which simulates the pawn.
    public static class DevCheats
    {
        public const KeyCode DefaultFlightKey = KeyCode.F1;
        // A scene that keeps F1 for itself (승규 님's test course: into the zone it looks at) sets None.
        public static KeyCode FlightKey = DefaultFlightKey;

        public static bool Allowed => Application.isEditor || Debug.isDebugBuild;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => FlightKey = DefaultFlightKey;
    }

    // A character that can fly for the cheat: W / S along the camera's view (look up to climb), A / D across it, sprint
    // faster, no gravity, hanging still with no keys down. Set where the character is simulated (offline, the host).
    public interface ICheatFlight
    {
        bool CheatFlying { get; set; }
    }
}
