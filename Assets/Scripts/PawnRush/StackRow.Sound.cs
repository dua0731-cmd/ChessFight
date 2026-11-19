using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "8번 짐더미 줄" - the set-down thud, the lift and carry - made with the ElevenLabs MCP): a storage
    // row's crane sounds, baked by ArtSource/PawnRush/Sfx/bake_carriage_yard.py into Resources/PawnRushCarriageYardSfx
    // (prompts: Docs/PawnRush/PAWN_RUSH_AUDIO.md section 12). Per half, at the trolley's place 1.2 m over the floor:
    //   move  the ropes pulling taut and the trolley rolling, from the lift's start (the gap's open time over) through
    //         the lift, the carry and the lowering; a PC that first sees it part way plays it from there (up to Late in)
    //   set   the stack set down in the gap (the trip's end, the next trip's start), as YardFx throws its dust
    // The two halves move together (mirrored) and each is heard from its own side. The takes alternate trip by trip,
    // the same on every PC. Every source goes through SectionSoundGate (heard only in this section, times the effects).
    public sealed partial class StackRow
    {
        const string SfxFolder = "PawnRushCarriageYardSfx/";
        const float Late = .3f;
        static AudioClip[] moves, sets;
        Transform[] ears;
        AudioSource[] movers, setters;
        long movedTrip = long.MinValue, setTrip = long.MinValue;

        static AudioClip[] Clips(string stem)
        {
            var found = new System.Collections.Generic.List<AudioClip>();
            for (int i = 0; i < 2; i++)
            {
                var clip = Resources.Load<AudioClip>(SfxFolder + stem + i);
                if (clip != null) found.Add(clip);
            }
            return found.ToArray();
        }

        void RowSounds(long trip, float into)
        {
            if (ears == null)
            {
                if (moves == null)
                {
                    moves = Clips("StackMove");
                    sets = Clips("StackSet");
                }
                ears = new Transform[2];
                movers = new AudioSource[2];
                setters = new AudioSource[2];
                for (int h = 0; h < 2; h++)
                {
                    var go = new GameObject(h == 0 ? "Row sound W" : "Row sound E");
                    go.transform.SetParent(transform, false);
                    ears[h] = go.transform;
                    if (moves.Length > 0) movers[h] = Source(go, 3f, 30f);
                    if (sets.Length > 0) setters[h] = Source(go, 4f, 35f);
                }
                // First seen: nothing set down "just now".
                setTrip = trip;
            }
            for (int h = 0; h < 2; h++)
                if (h < trolleys.Length && trolleys[h] != null)
                    ears[h].localPosition = new Vector3(trolleys[h].localPosition.x, 1.2f, 0f);
            // The last trip's stack set down: this trip just began.
            if (trip != setTrip)
            {
                setTrip = trip;
                var clip = Pick(sets, trip);
                if (clip != null && into < Late)
                    foreach (var s in setters)
                        if (s != null && s.isActiveAndEnabled) s.PlayOneShot(clip, 1f);
            }
            // This trip's stack lifted and carried.
            if (into >= openSeconds && trip != movedTrip)
            {
                movedTrip = trip;
                var clip = Pick(moves, trip);
                float late = into - openSeconds;
                if (clip != null && late <= Late && late < clip.length)
                    foreach (var m in movers)
                    {
                        if (m == null || !m.isActiveAndEnabled) continue;
                        m.Stop();
                        m.clip = clip;
                        m.time = late;
                        m.Play();
                    }
            }
        }

        static AudioClip Pick(AudioClip[] set, long trip) =>
            set == null || set.Length == 0 ? null : set[(int)(System.Math.Abs(trip) % set.Length)];

        static AudioSource Source(GameObject on, float near, float far)
        {
            var a = on.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            SectionSoundGate.Gate(a);
            return a;
        }
    }
}
