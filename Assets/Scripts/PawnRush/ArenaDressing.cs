using UnityEngine;

namespace ChessFight.PawnRush
{
    // A station's sign and gate bar at run time: the game's name, the bar filled to its progress (§2.1), the
    // 25 / 50 / 75 % and done moments (§2.4) played from the progress every PC has (online: the host's numbers).
    public sealed class ArenaDressing : MonoBehaviour
    {
        MissionStation station;
        TextMesh symbol, title, count;
        Transform fill;
        float shown, pulse;
        int quarter;
        bool done;
        int game = -2;

        public void Configure(MissionStation station, TextMesh symbol, TextMesh title, Transform fill, TextMesh count)
        {
            this.station = station;
            this.symbol = symbol;
            this.title = title;
            this.fill = fill;
            this.count = count;
        }

        // A new game in the station: new words, the bar empty, no moments owed.
        public void Installed(int game)
        {
            this.game = game;
            if (symbol != null) symbol.text = Arena.Symbol(game);
            if (title != null) title.text = MissionPicker.Name(game);
            shown = 0f;
            quarter = 0;
            done = false;
            if (count != null) count.text = "";
        }

        void Update()
        {
            if (station == null) return;
            var g = station.Game;
            float p = g != null ? g.Progress01 : 0f;
            shown = Mathf.MoveTowards(shown, p, Time.deltaTime * 1.5f);
            pulse = Mathf.Max(0f, pulse - Time.deltaTime);
            float k = 1f + .15f * Mathf.Sin(Mathf.Clamp01(pulse / .15f) * Mathf.PI);
            if (fill != null)
            {
                fill.localScale = new Vector3(Mathf.Max(.001f, 4f * shown), .22f * k, .04f);
                fill.localPosition = new Vector3(-2f + 2f * shown, 0f, -.04f);
            }
            if (g == null || station.GameIndex != game) return;
            if (count != null && g is IProgressCount c) count.text = $"{c.Done} / {c.Total}";
            int q = Mathf.Min(3, Mathf.FloorToInt(p * 4f + 1e-4f));
            if (q > quarter && !g.Completed)
            {
                quarter = q;
                pulse = .15f;
                var gate = station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f));
                ArenaFx.Ring(gate, 0f, 3f, .7f, Arena.Gold);
                ArenaFx.Burst(gate + Vector3.up * 5.6f, new[] { Arena.Gold }, 10, 2.5f, 2f, .9f, .1f);
                ArenaFx.Sfx("sfx_mg_quarter", gate, pitch: Mathf.Pow(2f, (q - 1) / 12f));
            }
            if (g.Completed && !done)
            {
                done = true;
                quarter = 4;
                var gate = station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f));
                var team = station.Accent(station.Team);
                ArenaFx.Beam(gate, 1.2f, 1.8f, 9f, 1.6f, team);
                ArenaFx.Burst(gate + Vector3.up, new[] { team, Arena.Gold, Color.white }, 90, 3f, 7f, 2.2f, .14f);
                ArenaFx.Ring(gate, 0f, 8f, 1f, team);
                bool ours = MissionStation.LocalTeam == station.Team;
                ArenaFx.Sfx("sfx_mg_complete", gate, twoD: true, volume: ours ? 1f : .35f);
                ArenaFx.Sfx("sfx_gate_open", gate);
            }
        }
    }
}
