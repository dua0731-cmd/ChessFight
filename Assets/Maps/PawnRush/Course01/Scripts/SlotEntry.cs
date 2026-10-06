using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The island's 3 m opening: reports a character walking in to its slot (the picture card).
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SlotEntry : MonoBehaviour
    {
        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            var slot = GetComponentInParent<MiniGameSlot>();
            if (driver != null && slot != null) slot.Report(driver);
        }
    }
}
