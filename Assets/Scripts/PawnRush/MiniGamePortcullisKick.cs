using UnityEngine;

namespace ChessFight.PawnRush
{
    // R505: the D portcullis model jolts at every full turn of its team's capstan (user, 11-07: turning the capstan did not
    // seem to open the gate). The game's gate still rises only MgCapstan.PortcullisPeek (0.4 m) with progress and opens
    // after all the turns, so pawns cannot crawl under it; this is a picture on top: the bars clunk up a hand's breadth
    // and drop back with a little bounce, so each turn reads as a pull on the chain. Read from the station's game
    // progress, which every PC has (the host's on the others), so it shows the same online.
    // StateDrivenMover: driven by the capstan's progress (pawns pushing), not the obstacle clock.
    public sealed class MiniGamePortcullisKick : MonoBehaviour
    {
        const float Up = .3f, Rise = .12f, Settle = 7f, Wobble = 18f;   // metres, seconds, damping, bounce speed

        MissionStation station;
        float turns;            // MgCapstan.Turns
        int seen;               // full turns already kicked
        float kickedAt = -10f;
        Vector3 rest;
        float unitsPerMetre = 1f;

        public void Watch(MissionStation team, float turnsToOpen)
        {
            station = team;
            turns = turnsToOpen;
            rest = transform.localPosition;
            var parent = transform.parent;
            unitsPerMetre = parent != null ? 1f / Mathf.Max(.0001f, Mathf.Abs(parent.lossyScale.y)) : 1f;
        }

        // R543: the turns that open the gate go with the team's size (MgCapstan.TurnsToOpen).
        public void SetTurns(float turnsToOpen) => turns = turnsToOpen;

        void LateUpdate()
        {
            var game = station != null ? station.Game : null;
            if (game == null) return;
            int done = Mathf.FloorToInt(game.Progress01 * turns + 1e-4f);
            if (done < seen || game.Progress01 <= 0f) seen = done;           // a reset, or pushed back
            else if (done > seen && !game.Completed)
            {
                seen = done; kickedAt = Time.time;
                var fx = station.GetComponent<MiniGameCourtGateFx>();   // the clank, dust and grit (R505 SFX/VFX)
                if (fx != null) fx.Step();
            }
            float t = Time.time - kickedAt, up = 0f;
            if (t < Rise) up = Up * t / Rise;
            else if (t < Rise + 1f) up = Mathf.Max(-.04f, Up * Mathf.Exp(-Settle * (t - Rise)) * Mathf.Cos(Wobble * (t - Rise)));
            transform.localPosition = rest + Vector3.up * (up * unitsPerMetre);
        }
    }
}
