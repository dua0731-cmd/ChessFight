using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of the royal vault (왕실 금고실, R725; Assets/Prefabs/PawnRushSections/Late_3-3_RoyalVault.prefab and its map-test
    // twin Assets/Prefabs/PawnRushMaps/RoyalVault.prefab): a draft for the late obstacle slot (slot 5's drafts, after the
    // observatory), grey-box. The prefab holds only this root and its PawnRushSection; Awake builds Content from code
    // (RoyalVaultBuilder), as the war room does (WarRoomSection). No kit field and no ISectionPart: every machine's phase is a
    // fixed constant on the shared obstacle clock (RoyalVaultRules), so the section's seed is not used.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class RoyalVaultSection : MonoBehaviour
    {
        public const string Id = "Late_3-3_RoyalVault", DisplayName = "왕실 금고실", ContentName = "Content";

        // Entry: the middle of the 12 m threshold, on the floor. Exit: the end of the treasure room's level runout.
        public static readonly Vector3 Entry = Vector3.zero,
            Exit = new Vector3(0f, (float)RoyalVaultRules.ExitY, (float)RoyalVaultRules.ExitZ);

        [Tooltip("Build the vault from code when it wakes (the prefab holds only its root).")]
        [SerializeField] bool buildIfEmpty = true;

        public bool HasContent => transform.Find(ContentName) != null;

        void Awake()
        {
            if (buildIfEmpty && !HasContent) RoyalVaultBuilder.Build(this);
        }
    }
}
