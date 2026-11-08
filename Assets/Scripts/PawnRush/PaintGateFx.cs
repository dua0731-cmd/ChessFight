using UnityEngine;

namespace ChessFight.PawnRush
{
    // A · 의장 광장's team gate moments in place of the shared ones (ArenaDressing, IGateFx), R506. The user, 11-07 (via
    // the C session): every mini-game makes its own 25 / 50 / 75 % moment and none uses a ground ring.
    //  Quarter()  the shared sparkle (no chime: no quarter sounds in any mini-game, user 11-07), plus A's own: paint droplets fountaining from the arch's piers in the
    //             team's colour and its stone's (ivory / basalt), twinkles climbing the arch; more each quarter
    //             (PaintBoardFx.GateQuarter). No ring.
    //  Opened()   A's own door moment (user, 11-08: "문이 열리는 vfx랑 사운드 넣어줘"): the oak door lifting in its arch
    //             (sfx_paint_door_open, ElevenLabs, a_sounds.py), a square of light on the passage floor, stone dust,
    //             and a brushed paint path rolling out under the door into the tunnel (PaintBoardFx.GateOpened /
    //             PaintPath), then twinkles and dust along the door's foot while it rises (PaintBoardFx.DoorRising).
    //             No light column (turned down 11-07), no confetti (turned down 11-08: "폭죽 연출은 하지 말고").
    // Pictures and sound only; MiniGameModels.ParadeStation adds it to each A station.
    public sealed class PaintGateFx : MonoBehaviour, IGateFx
    {
        PaintBoardFx fx;
        float risingUntil = -1f, nextRise;
        Vector3 doorFoot, doorRight, doorCourt;

        PaintBoardFx Fx
        {
            get
            {
                if (fx == null)
                {
                    var go = new GameObject("A gate fx");
                    go.transform.SetParent(transform, false);
                    fx = go.AddComponent<PaintBoardFx>();
                }
                return fx;
            }
        }

        Vector3 Gate(MissionStation station) => station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f));

        public void Quarter(int quarter)
        {
            var station = GetComponent<MissionStation>();
            if (station == null) return;
            var gate = Gate(station);
            ArenaFx.Burst(gate + Vector3.up * 5.6f, new[] { Arena.Gold }, 10, 2.5f, 2f, .9f, .1f);
            // no sound at the quarters (user, 11-07, via the C session: "25 50 75 효과음은 하지마"); pictures only
            var stone = Arena.Hex(station.Team == Gameplay.Teams.Black ? "#2E2A26" : "#EEE3D0");
            Fx.GateQuarter(gate, station.transform.right, station.Accent(station.Team), stone, quarter);
        }

        // The closed door's floor middle, its face toward the court: the game's gate box (A moves it into the arch,
        // MiniGameModels.ParadeStation).
        Vector3 DoorFoot(MissionStation station, out Vector3 right, out Vector3 court)
        {
            right = station.transform.right;
            court = -station.transform.forward;                        // the court lies before the arch, toward the entry
            var gate = station.Gate != null ? station.Gate.transform : null;
            if (gate == null) return Gate(station) + station.transform.forward * 1.6f;
            return gate.position - Vector3.up * gate.lossyScale.y * .5f + court * (gate.lossyScale.z * .5f + .05f);
        }

        public void Opened()
        {
            var station = GetComponent<MissionStation>();
            if (station == null) return;
            doorFoot = DoorFoot(station, out doorRight, out doorCourt);
            var team = station.Accent(station.Team);
            Fx.GateOpened(doorFoot, doorRight, doorCourt, station.transform.eulerAngles.y, team, Dust, Arena.Gold, Arena.Hex("#F3EADB"));
            ArenaFx.Sfx("sfx_paint_door_open", doorFoot + Vector3.up * 2.3f);
            risingUntil = Time.time + (station.Gate != null ? station.Gate.Seconds : 1.2f) + .1f;
            nextRise = 0f;
        }

        static Color Dust => Arena.Hex("#D9CCB4");                     // cream stone dust, either team

        void Update()
        {
            if (risingUntil < 0f) return;
            if (Time.time > risingUntil) { risingUntil = -1f; return; }
            if (Time.time < nextRise) return;
            nextRise = Time.time + .06f;
            var station = GetComponent<MissionStation>();
            if (station == null || station.Gate == null) return;
            var gate = station.Gate.transform;
            float foot = gate.position.y - gate.lossyScale.y * .5f - doorFoot.y;
            Fx.DoorRising(doorFoot, doorRight, doorCourt, Mathf.Max(0f, foot), station.Accent(station.Team), Dust);
        }
    }
}
