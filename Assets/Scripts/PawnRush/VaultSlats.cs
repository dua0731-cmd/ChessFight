using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The ink bars that show a royal vault belt running (R725, build spec §2.1 and §2.7): a look only, no collider. Slat k sits
    // at z0 + Wrap(BeltTravel(belt, now) + k pitch, length) along its parent's z, so the bars move with the belt's carry on
    // the obstacle clock - the same on every PC, standing still while the east belt stands - and the one leaving the belt's
    // end comes back in at its start. The slats are children of Content (the section's frame), not of the scaled belt box;
    // only their z is set, their x and y stay where the builder put them.
    public sealed class VaultSlats : MonoBehaviour
    {
        int belt;
        Transform[] slats;
        float z0, length, pitch;

        public int Belt => belt;

        // belt: 0 west, 1 east. z0 and length: the run the slats loop over, in their parent's frame; pitch: the spacing.
        public void Configure(int belt, Transform[] slats, float z0, float length, float pitch)
        {
            this.belt = belt;
            this.slats = slats;
            this.z0 = z0;
            this.length = length;
            this.pitch = pitch;
            Apply(ObstacleClock.Now);
        }

        void Update() => Apply(ObstacleClock.Now);

        // Puts every slat where the belt has carried it at a clock time (in double: the travel is clock-sized).
        public void Apply(double time)
        {
            if (slats == null || length <= 0f) return;
            double travel = RoyalVaultRules.BeltTravel(belt, time);
            for (int k = 0; k < slats.Length; k++)
            {
                var slat = slats[k];
                if (slat == null) continue;
                var p = slat.localPosition;
                p.z = z0 + (float)RoyalVaultRules.Wrap(travel + k * (double)pitch, length);
                slat.localPosition = p;
            }
        }
    }
}
