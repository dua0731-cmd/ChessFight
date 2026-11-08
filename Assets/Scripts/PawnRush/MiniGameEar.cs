using UnityEngine;

namespace ChessFight.PawnRush
{
    // Team gate sounds the same everywhere inside a mini-game (user, 11-08: "플레이어가 그 미니게임에 있다면 어디든 똑같은
    // 소리가 날 수 있게 해 ... 1~10번 미니게임도 마찬가지"). A listener standing in the station's mini-game section hears
    // a gate's opening flat (2D, no distance fall-off, no left/right); one outside it hears it placed in the world as
    // before, so the next section's gate is still far away.
    //  - ArenaDressing sets Current around the gate-open moment, so every ArenaFx.Sfx played in it (the shared
    //    sfx_gate_open and each mini-game's own IGateFx.Opened sounds) goes flat for a listener inside.
    //  - A mini-game playing its gate sound on its own AudioSource calls Fit(source, station).
    public static class MiniGameEar
    {
        // The section's floor plan in its content's space (MiniGameSectionBuilder: x -23..23, z -1..36) with a margin.
        const float HalfX = 24.5f, Z0 = -3f, Z1 = 39f, Y0 = -10f, Y1 = 40f;
        const float LooseRange = 30f;                 // a station outside a mini-game section: within this of it

        public static MissionStation Current;
        static AudioListener ear;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; ear = null; }

        static Vector3? Listener()
        {
            if (ear == null || !ear.isActiveAndEnabled)
                ear = Object.FindFirstObjectByType<AudioListener>();
            return ear != null ? ear.transform.position : (Vector3?)null;
        }

        public static bool Inside(MissionStation station)
        {
            if (station == null) return false;
            var at = Listener();
            if (at == null) return false;
            var section = station.GetComponentInParent<MiniGameSection>();
            if (section == null) return Vector3.Distance(at.Value, station.transform.position) < LooseRange;
            var space = section.transform.Find(MiniGameSection.ContentName);
            var p = (space != null ? space : section.transform).InverseTransformPoint(at.Value);
            return Mathf.Abs(p.x) <= HalfX && p.z >= Z0 && p.z <= Z1 && p.y >= Y0 && p.y <= Y1;
        }

        // Flat for a listener in the station's mini-game, placed in the world otherwise.
        public static void Fit(AudioSource source, MissionStation station)
        {
            if (source != null) source.spatialBlend = Inside(station) ? 0f : 1f;
        }
    }
}
