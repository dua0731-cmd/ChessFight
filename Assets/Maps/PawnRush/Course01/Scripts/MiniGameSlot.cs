using System;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public enum SlotExitMode
    {
        [Tooltip("A 6 m wide ramp up to the door (games A, C, E).")] Ramp,
        [Tooltip("A flat block at the exit height whose front cannot be climbed (games B, D build their own way up).")] HighStage
    }

    // A team section's mini-game island (design doc §5): 12 m wide, entry at local z 0,
    // promotion pads at z 0..4, the 18 x 12 m game area at z 4..22, then the exit part
    // and the door at the exit height (+3 m for slot 1, +6 m for slot 2) with the
    // landing behind it. Origin: the middle of the entry edge, on the island floor.
    // The game prefab goes under GameSocket; the door opens on its done signal.
    public sealed class MiniGameSlot : MonoBehaviour
    {
        [SerializeField, Range(1, 2)] int slot = 1;
        [SerializeField, Range(0, 1)] int team = Teams.White;
        [Tooltip("3 for slot 1, 6 for slot 2.")]
        [SerializeField] float exitHeight = 3f;
        [SerializeField] SlotExitMode exitMode = SlotExitMode.Ramp;
        [SerializeField] GameObject ramp;
        [SerializeField] GameObject highStage;
        [SerializeField] SlotDoor door;
        [Tooltip("Where the mini-game prefab sits: the middle of the game area.")]
        [SerializeField] Transform gameSocket;

        // Somebody walked in: show the picture card for 3 s (mini-game spec), without stopping anyone.
        public static event Action<MiniGameSlot, ICharacterDriver> Entered;

        public int Slot => slot;
        public int Team => team;
        public float ExitHeight => exitHeight;
        public SlotExitMode ExitMode => exitMode;
        public SlotDoor Door => door;
        public Transform GameSocket => gameSocket;
        public MiniGameBase Game => gameSocket != null ? gameSocket.GetComponentInChildren<MiniGameBase>() : null;

        public void Configure(int slot, int team, float exitHeight, GameObject ramp, GameObject highStage, SlotDoor door, Transform gameSocket)
        {
            this.slot = slot;
            this.team = team;
            this.exitHeight = exitHeight;
            this.ramp = ramp;
            this.highStage = highStage;
            this.door = door;
            this.gameSocket = gameSocket;
            ApplyExitMode();
        }

        public void SetTeam(int team) => this.team = team;

        public void SetExitMode(SlotExitMode mode)
        {
            exitMode = mode;
            ApplyExitMode();
        }

        // Ramp mode shows the ramp; high-stage mode fills the exit part to the door's height
        // instead. The door stays where it is either way.
        void ApplyExitMode()
        {
            if (ramp != null) ramp.SetActive(exitMode == SlotExitMode.Ramp);
            if (highStage != null) highStage.SetActive(exitMode == SlotExitMode.HighStage);
        }

        // Switching the mode in the Inspector. SetActive is not allowed inside OnValidate itself.
        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) ApplyExitMode(); };
#endif
        }

        void Start()
        {
            var game = Game;
            if (game != null && door != null) game.Finished += door.Open;
        }

        public void ResetRound()
        {
            Game?.ResetGame();
            door?.Close();
        }

        internal void Report(ICharacterDriver who) => Entered?.Invoke(this, who);
    }
}
