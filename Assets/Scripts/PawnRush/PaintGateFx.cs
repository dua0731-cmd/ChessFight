using UnityEngine;

namespace ChessFight.PawnRush
{
    // A · 의장 광장's team gate moments in place of the shared ones (ArenaDressing, IGateFx), R506. The user, 11-07 (via
    // the C session): every mini-game makes its own 25 / 50 / 75 % moment and none uses a ground ring.
    //  Quarter()  the shared sparkle (no chime: no quarter sounds in any mini-game, user 11-07), plus A's own: paint droplets fountaining from the arch's piers in the
    //             team's colour and its stone's (ivory / basalt), twinkles climbing the arch; more each quarter
    //             (PaintBoardFx.GateQuarter). No ring.
    //  Opened()   unchanged from the shared moment (light column, burst, ring, sfx_gate_open): only the quarters were
    //             asked for.
    // Pictures and sound only; MiniGameModels.ParadeStation adds it to each A station.
    public sealed class PaintGateFx : MonoBehaviour, IGateFx
    {
        PaintBoardFx fx;

        Vector3 Gate(MissionStation station) => station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f));

        public void Quarter(int quarter)
        {
            var station = GetComponent<MissionStation>();
            if (station == null) return;
            var gate = Gate(station);
            ArenaFx.Burst(gate + Vector3.up * 5.6f, new[] { Arena.Gold }, 10, 2.5f, 2f, .9f, .1f);
            // no sound at the quarters (user, 11-07, via the C session: "25 50 75 효과음은 하지마"); pictures only
            if (fx == null)
            {
                var go = new GameObject("A gate fx");
                go.transform.SetParent(transform, false);
                fx = go.AddComponent<PaintBoardFx>();
            }
            var stone = Arena.Hex(station.Team == Gameplay.Teams.Black ? "#2E2A26" : "#EEE3D0");
            fx.GateQuarter(gate, station.transform.right, station.Accent(station.Team), stone, quarter);
        }

        public void Opened()
        {
            var station = GetComponent<MissionStation>();
            if (station == null) return;
            var gate = Gate(station);
            var team = station.Accent(station.Team);
            ArenaFx.Beam(gate, 1.2f, 1.8f, 9f, 1.6f, team);
            ArenaFx.Burst(gate + Vector3.up, new[] { team, Arena.Gold, Color.white }, 90, 3f, 7f, 2.2f, .14f);
            ArenaFx.Ring(gate, 0f, 8f, 1f, team);
            // no sound (user, 11-07: "사운드는 다 없애고")
        }
    }
}
