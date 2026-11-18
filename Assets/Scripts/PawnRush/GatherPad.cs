using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R704 (the flow rules' option 3 - the user 2026-11-17: "미니게임을 클리어해도 특정인원 이상 같은팀이 모여서 발판을
    // 밟아야 미니게임 이후의 길이 열림"; the R699 proposal's pick, taken as it stood: the team's majority, the win's
    // own sum): the gold pad before a mission station's team gate. Under PawnRushRules.AfterGather a cleared game
    // does not open the gate; the gate opens when most of the team's players stand on this pad together, so the ones
    // ahead wait for the ones behind and the team goes on as one.
    //
    // The pad lies in the gate passage before the door (MiniGameSectionBuilder.Station: the station's x -2..2,
    // z 20..23), on whatever floor is there. Pictures only: a plate, a rim, one pip a player needed and the count
    // over it. Dim until the game is cleared, gold and pulsing while the gate waits, gone a moment after it opens.
    //
    // Who counts is the finish's own list (CrownFinish.Runners that count: the players in the match now, R538), so
    // the number needed follows who is playing as the win's does - 4 of 6, 3 of 5 or 4, 2 of 3 or 2, 1 of 1 - and a
    // team with no player at all opens on its clear. Every PC counts for its own pictures; the PC that judges the
    // games (offline, the host) opens the gate, and the others hear it with the course state (the gate flag).
    public sealed class GatherPad : MonoBehaviour
    {
        public const float HalfWidth = 1.85f, Depth = 2.6f;
        // A pawn stands on it: its hips this far outside the plate at most, and this far under and over its floor.
        const float Reach = .3f, Below = .6f, Above = 3.2f;
        const float GoneAfter = 2.5f, RingEvery = 1.6f;

        MissionStation station;
        GameObject body;
        Renderer plate;
        TextMesh count;
        readonly List<Renderer> pips = new List<Renderer>();
        Material dim, lit, pipOff, pipOn;
        int shownNeed = -1, shownLit = -1;
        bool shownWaiting;
        float openedAt = -1f, nextRing, placedAt;
        bool settled;

        // As last shown: how many of the team's players are needed, and whether the gate waits for them.
        public int Need { get; private set; }
        public bool Waiting { get; private set; }

        public static GatherPad Make(MissionStation station)
        {
            if (station == null || station.Gate == null) return null;
            var root = new GameObject("Gather pad (" + Teams.Name(station.Team) + ")");
            root.transform.SetParent(station.transform, false);
            var pad = root.AddComponent<GatherPad>();
            pad.station = station;
            pad.Place();
            pad.placedAt = Time.fixedTime;
            pad.Build();
            return pad;
        }

        // Before the door, on the court's side: the door's own place across and along (A stands its door in its
        // arch), the first solid thing under the doorway for the floor (a pawn or a loose thing there is skipped).
        void Place()
        {
            var s = station.transform;
            var door = station.Gate.transform;
            Vector3 front = door.position - s.forward * (Mathf.Abs(door.lossyScale.z) * .5f + .1f + Depth * .5f);
            float floor = s.position.y, best = float.MaxValue;
            var from = new Vector3(front.x, s.position.y + 2.5f, front.z);
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                var held = hit.collider.attachedRigidbody;
                if (held != null && !held.isKinematic) continue;
                if (hit.collider.GetComponentInParent<TeamGate>() != null) continue;
                if (hit.collider.GetComponentInParent<ICharacterDriver>() != null) continue;
                if (hit.distance < best) { best = hit.distance; floor = hit.point.y; }
            }
            transform.SetPositionAndRotation(new Vector3(front.x, floor, front.z), s.rotation);
        }

        void Build()
        {
            body = new GameObject("Pictures");
            body.transform.SetParent(transform, false);
            dim = Arena.Lit("M_GatherPad_Dim", new Color(.34f, .27f, .12f), 0f, .55f);
            lit = Arena.Emissive(new Color(.93f, .6f, .1f), .5f);
            pipOff = Arena.Lit("M_GatherPip_Off", new Color(.15f, .12f, .08f), 0f, .6f);
            pipOn = Arena.Emissive(new Color(1f, .92f, .55f), 2.2f);
            plate = Picture("Plate", new Vector3(0f, .03f, 0f), new Vector3(HalfWidth * 2f, .05f, Depth), dim);
            var trim = Arena.TrimGold;
            Picture("Rim S", new Vector3(0f, .05f, -Depth * .5f), new Vector3(HalfWidth * 2f + .14f, .09f, .14f), trim);
            Picture("Rim N", new Vector3(0f, .05f, Depth * .5f), new Vector3(HalfWidth * 2f + .14f, .09f, .14f), trim);
            Picture("Rim W", new Vector3(-HalfWidth, .05f, 0f), new Vector3(.14f, .09f, Depth), trim);
            Picture("Rim E", new Vector3(HalfWidth, .05f, 0f), new Vector3(.14f, .09f, Depth), trim);
            // Over the pad's court edge, facing the court (Arena.Text faces -z: the way in).
            count = Arena.Text(body.transform, "Count", new Vector3(0f, 3.1f, -Depth * .5f), .9f, new Color(1f, .9f, .55f));
            count.text = "";
        }

        Renderer Picture(string name, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        // One pip a player needed, in a row along the court edge.
        void Pips(int need)
        {
            foreach (var pip in pips) if (pip != null) Destroy(pip.gameObject);
            pips.Clear();
            float step = Mathf.Min(.62f, (HalfWidth * 2f - .5f) / Mathf.Max(1, need));
            for (int i = 0; i < need; i++)
                pips.Add(Picture("Pip " + (i + 1), new Vector3((i - (need - 1) * .5f) * step, .09f, -Depth * .5f + .42f),
                                 new Vector3(step * .62f, .08f, step * .62f), pipOff));
        }

        // The team's players (who counts toward a majority) and how many of them stand on the pad.
        public void Count(out int on, out int players)
        {
            on = players = 0;
            var finish = CrownFinish.Current;
            if (finish == null) return;
            var runners = finish.Runners;
            for (int i = 0; i < runners.Count; i++)
            {
                var r = runners[i];
                if (!r.Counts || r.Team != station.Team) continue;
                players++;
                var hips = r.Driver != null ? r.Driver.FollowTarget : null;
                if (hips != null && Holds(hips.position)) on++;
            }
        }

        public bool Holds(Vector3 world)
        {
            var p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= HalfWidth + Reach && Mathf.Abs(p.z) <= Depth * .5f + Reach && p.y >= -Below && p.y <= Above;
        }

        // Every frame, from the room (MiniGameSection.Rules.cs): the game cleared, the gate open, who is here.
        public void Show(bool cleared, bool open, int on, int players)
        {
            // Its place once more after a physics step: the room may have been stood in the round the frame the pad
            // was made, before the physics knew where its floor is.
            if (!settled && Time.fixedTime > placedAt + .05f)
            {
                settled = true;
                Place();
            }
            Need = players > 0 ? PawnRushFinish.Needed(players) : 0;
            Waiting = cleared && !open;
            if (Need != shownNeed)
            {
                shownNeed = Need;
                shownLit = -1;
                Pips(Need);
            }
            int lights = open ? Need : Waiting ? Mathf.Min(on, Need) : 0;
            if (lights != shownLit)
            {
                shownLit = lights;
                for (int i = 0; i < pips.Count; i++) if (pips[i] != null) pips[i].sharedMaterial = i < lights ? pipOn : pipOff;
            }
            if (Waiting != shownWaiting)
            {
                shownWaiting = Waiting;
                plate.sharedMaterial = Waiting ? lit : dim;
                nextRing = 0f;
            }
            string text = Waiting ? on + " / " + Need : "";
            if (count.text != text) count.text = text;
            if (Waiting && Time.time >= nextRing)
            {
                nextRing = Time.time + RingEvery;
                ArenaFx.Ring(transform.position + Vector3.up * .12f, .4f, HalfWidth + .7f, .9f, Arena.Gold);
            }
            // Gone a moment after the gate opened; back with a new round's shut gate.
            if (!open) openedAt = -1f;
            else if (openedAt < 0f) openedAt = Time.time;
            bool seen = !open || Time.time - openedAt < GoneAfter;
            if (body.activeSelf != seen) body.SetActive(seen);
        }

        // The rule is not this round's.
        public void Hide()
        {
            Waiting = false;
            if (body != null && body.activeSelf) body.SetActive(false);
        }
    }
}
