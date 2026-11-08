using UnityEngine;

namespace ChessFight.PawnRush
{
    // R502: C 해자 도개교's own team-gate moments (IGateFx), in place of the shared ones. User 11-07: every mini-game
    // makes its own 25 / 50 / 75 % effect and none of them uses a ground ring ("원은 사용 하지 않는 vfx"):
    //   quarter  the moat answers the winch - a column of water bursts up in front of the deck, bigger each quarter,
    //            and the chains shake: iron and gold sparks off both pylon eyes; no sound (user 11-07)
    //   opened   the team gate hauled up: dust and grit off it, the team's motes through the passage, its own sound
    //            (no shared beam, burst or ring: user 11-08)
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

        // User 11-08 ("문이 열릴 때 vfx랑 사운드도 해줘 기존 문이 열리는 vfx는 지워주고"): no shared beam, burst or ring
        // and no moat splash any more. The team gate (the game's 4 x 4.6 m door, rising 4.6 m in 1.2 s, TeamGate) is
        // hauled up: a dust puff where its foot leaves the floor, stone dust and grit shaken off its top edge all the
        // way up, then at the top a puff under the arch and the team's motes drifting up through the open passage;
        // sfx_drawbridge_gate (Early/Sfx/c_sounds.py: a wooden gate hauled up, a thunk at the top).
        static readonly Color[] Dust = { new Color(.86f, .78f, .64f), new Color(.74f, .66f, .52f) };
        static readonly Color[] Grit = { new Color(.62f, .55f, .44f), new Color(.86f, .78f, .64f), new Color(.45f, .36f, .25f) };
        const float GateSeconds = 1.2f;

        public void Opened()
        {
            if (station == null) return;
            var door = station.transform.Find("Team gate");
            var at = door != null ? door.position : station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight, 18.8f));
            ArenaFx.Sfx("sfx_drawbridge_gate", at + Vector3.up * 2f);
            StopAllCoroutines();
            StartCoroutine(Hauled(door, at));
        }

        System.Collections.IEnumerator Hauled(Transform door, Vector3 at)
        {
            var team = station.Accent(station.Team);
            var look = door != null ? door.GetComponent<Renderer>() : null;
            var right = station.transform.right;
            float foot = look != null ? look.bounds.min.y : at.y;
            // the foot leaves the floor
            for (int e = -1; e <= 1; e++)
                ArenaFx.Burst(new Vector3(at.x, foot + .05f, at.z) + right * (e * 1.4f), Dust, 8, 1.2f, .5f, .9f, .12f);
            float t0 = Time.time;
            while (Time.time - t0 < GateSeconds)
            {
                // the top edge as it rises: dust off it, grit falling
                float top = look != null ? look.bounds.max.y : at.y + 4.6f;
                var edge = new Vector3(at.x, top, at.z) + right * Random.Range(-1.8f, 1.8f);
                ArenaFx.Burst(edge, Dust, 3, .5f, .2f, .7f, .1f);
                ArenaFx.Burst(edge, Grit, 3, .3f, -1.5f, .8f, .05f);
                yield return new WaitForSeconds(.1f);
            }
            // at the top: a puff under the arch, the team's motes drifting up through the opened passage
            float high = look != null ? look.bounds.min.y : foot + 4.6f;
            for (int e = -1; e <= 1; e++)
                ArenaFx.Burst(new Vector3(at.x, high, at.z) + right * (e * 1.3f), Dust, 10, 1f, .3f, 1.1f, .13f);
            for (int i = 0; i < 4; i++)
            {
                ArenaFx.Burst(new Vector3(at.x, foot + .3f, at.z) + right * Random.Range(-1.5f, 1.5f),
                    new[] { team, Color.Lerp(team, Color.white, .5f) }, 6, .35f, 1.2f, 1.6f, .07f);
                yield return new WaitForSeconds(.25f);
            }
        }
    }
}
