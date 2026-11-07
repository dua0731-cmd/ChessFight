using UnityEngine;

namespace ChessFight.PawnRush
{
    // R502: C 해자 도개교's own team-gate moments (IGateFx), in place of the shared ones. User 11-07: every mini-game
    // makes its own 25 / 50 / 75 % effect and none of them uses a ground ring ("원은 사용 하지 않는 vfx"):
    //   quarter  the moat answers the winch - a column of water bursts up in front of the deck, bigger each quarter,
    //            and the chains shake: iron and gold sparks off both pylon eyes; no sound (user 11-07)
    //   opened   as the shared one (the beam, the burst, the ring and sfx_gate_open: the completion moment is kept as it
    //            was) with a last big splash out of the moat
    //   pulling  (user 11-07, "줄 잡아 당길 때 vfx 적용 시켜") from the metres pulled as drawn, so a client sees the same:
    //            every 0.2 m rope fibres flick off where the rope leaves the drum, friction sparks and fibres at both
    //            pulleys, fibres off the gripped knots, water drips off the deck's far edge as it lowers; every whole
    //            metre the drum clacks round - gold sparks off its gear end and wood chips
    // Pictures and sounds only, played by whoever sees the progress (ArenaDressing), no game state.
    public sealed class MiniGameDrawbridgeFx : MonoBehaviour, IGateFx
    {
        static readonly Color[] Water = { new Color(.55f, .92f, .96f), new Color(.85f, .98f, 1f), Color.white };
        static readonly Color[] Sparks = { Arena.Gold, new Color(1f, .92f, .7f), new Color(.35f, .36f, .4f) };

        MissionStation station;
        MgDrawbridge game;

        static readonly Color[] Fibre = { new Color(.86f, .74f, .55f), new Color(.72f, .6f, .42f) };
        static readonly Color[] Friction = { Arena.Gold, new Color(1f, .9f, .6f), new Color(.86f, .74f, .55f) };
        static readonly Color[] Chips = { new Color(.55f, .34f, .18f), new Color(.75f, .5f, .28f), Arena.Gold };
        static readonly Color[] Drips = { new Color(.55f, .92f, .96f), Color.white };
        const float Step = .2f;              // metres of rope between two puffs

        float lastShown = -1f, stepped, metre;

        public void Configure(MissionStation owner, MgDrawbridge bridge)
        {
            station = owner;
            game = bridge;
            lastShown = -1f;
        }

        void LateUpdate()
        {
            if (game == null || !game.PalaceFront) return;
            float shown = game.Shown;
            if (lastShown < 0f || shown < lastShown - .5f) { lastShown = shown; stepped = metre = shown; return; }   // start, a reset
            if (shown <= lastShown + 1e-4f) { lastShown = Mathf.Min(lastShown, shown); return; }
            lastShown = shown;
            if (shown - stepped >= Step)
            {
                stepped = shown;
                ArenaFx.Burst(game.DrumTopWorld, Fibre, 3, .25f, .8f, .5f, .035f);
                for (int i = 0; i < 2; i++)
                    ArenaFx.Burst(game.PulleyWorld(i) + Vector3.up * .1f, Friction, 3, .25f, .5f, .35f, .03f);
                for (int k = 0; k < 4; k++)
                    if (game.KnotHeld(k)) ArenaFx.Burst(game.KnotWorld(k) + Vector3.up * .1f, Fibre, 2, .2f, .5f, .4f, .03f);
                for (int i = 0; i < 2; i++)
                    if (game.ChainEnds(i, out _, out var end) && !game.Completed)
                        ArenaFx.Burst(end, Drips, 3, .15f, -.4f, .7f, .045f);
            }
            if (shown - metre >= 1f)
            {
                metre = Mathf.Floor(shown);
                var drum = game.DrumWorld;
                ArenaFx.Burst(drum + Vector3.up * .3f, Chips, 8, .6f, 1.2f, .6f, .05f);
                ArenaFx.Burst(drum + Vector3.up * .45f, Friction, 6, .4f, 1.4f, .45f, .04f);
            }
        }

        // the moat in front of the deck, on the water (game frame: the palace moat u 11.5..16.5, water at -0.5)
        Vector3 Moat => game != null
            ? game.transform.TransformPoint(new Vector3(0f, -.45f, .5f * (MgDrawbridge.PalaceMoat0 + MgDrawbridge.PalaceMoat1)))
            : transform.position;

        public void Quarter(int quarter)
        {
            if (game == null) return;
            float q = Mathf.Clamp(quarter, 1, 3);
            ArenaFx.Burst(Moat, Water, 24 + 10 * (int)q, 1.1f, 3.5f + 1.2f * q, 1.3f, .13f);
            ArenaFx.Burst(Moat, Water, 10, 2.2f, 1.2f, .9f, .2f);                       // the spray falling round it
            for (int i = 0; i < 2; i++)
                if (game.ChainEnds(i, out var top, out _))
                    ArenaFx.Burst(top + Vector3.up * .3f, Sparks, 10 + 3 * (int)q, .6f, 1.6f, .7f, .07f);
            // no sound at the quarters (user 11-07, "25 50 75 효과음은 하지마")
        }

        public void Opened()
        {
            if (station == null) return;
            var gate = station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f));
            var team = station.Accent(station.Team);
            ArenaFx.Beam(gate, 1.2f, 1.8f, 9f, 1.6f, team);
            ArenaFx.Burst(gate + Vector3.up, new[] { team, Arena.Gold, Color.white }, 90, 3f, 7f, 2.2f, .14f);
            ArenaFx.Ring(gate, 0f, 8f, 1f, team);
            ArenaFx.Sfx("sfx_gate_open", gate);
            if (game != null) ArenaFx.Burst(Moat, Water, 60, 1.6f, 7.5f, 1.6f, .15f);
        }
    }
}
