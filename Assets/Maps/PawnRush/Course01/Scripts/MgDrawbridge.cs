using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // C, the drawbridge: a 4 m moat across the station in front of the gate (u 12..16, water 2 m
    // deep) and a 6 m drawbridge hinged on the far bank (u 16), standing up until it is lowered.
    // The rope runs down the middle (v 0, u 1..11) with four handles; a pawn of the team walking
    // back along it (toward u 1) lowers the bridge by 0.85% a second, at most four at once. One of
    // the other team walking it forward raises it by as much (MissionStation.ReverseSabotage),
    // never below the last 25% reached. Lowered all the way: the gate opens.
    //
    // "Holding a handle" is read as being in the rope's lane: the ragdoll's grip is not visible
    // from the course (MissionGames).
    public sealed class MgDrawbridge : MiniGameBase
    {
        public const float RatePerPawn = .0085f;
        public const float Moat0 = 12f, Moat1 = 16f, Lane = 1.6f, Lane0 = 1f, Lane1 = 11f, Walk = .6f;

        [SerializeField] MissionStation station;
        [Tooltip("The hinge on the far bank; the deck turns about its x axis.")]
        [SerializeField] Rigidbody hinge;

        float progress;

        public static MgDrawbridge Build(CourseBuilder b, MissionStation station)
        {
            var k = b.kit;
            var V = (System.Func<float, float, float, Vector3>)MissionGames.V;
            MissionGames.Floor(b, 0f, Moat0);
            MissionGames.Floor(b, Moat1, 20f);
            // The moat: walls down to its bottom, the bottom, the water. Falling in counts as a fall.
            b.Box("Moat wall S", V(-10f, -2f, Moat0 - .01f), V(10f, -1f, Moat0), k.wallNoClimb, true, false);
            b.Box("Moat wall N", V(-10f, -2f, Moat1), V(10f, -1f, Moat1 + .01f), k.wallNoClimb, true, false);
            b.Box("Moat bottom", V(-10f, -3f, Moat0), V(10f, -2f, Moat1), k.wallNoClimb, true);
            b.Box("Water", V(-10f, -.85f, Moat0), V(10f, -.8f, Moat1), k.glass, false, false);
            b.Trigger<KillVolume>("Moat (fall)", V(-10f, -2f, Moat0), V(10f, -.6f, Moat1));

            // The bridge: a hinge on the far bank, the deck reaching back over the moat when down.
            var hingeGo = new GameObject("Drawbridge hinge");
            hingeGo.transform.SetParent(b.parent, false);
            hingeGo.transform.localPosition = V(0f, 0f, Moat1);
            var body = hingeGo.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var before = b.parent;
            b.parent = hingeGo.transform;
            var deck = b.Box("Deck", V(-3f, -.3f, -(Moat1 - Moat0)), V(3f, 0f, 0f), k.boardDark);
            deck.AddComponent<CourseFloor>();
            b.Box("Deck rail W", V(-3f, 0f, -(Moat1 - Moat0)), V(-2.8f, .5f, 0f), k.hazard, true, false);
            b.Box("Deck rail E", V(2.8f, 0f, -(Moat1 - Moat0)), V(3f, .5f, 0f), k.hazard, true, false);
            b.parent = before;
            hingeGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            b.Box("Hinge post W", V(-3.6f, 0f, Moat1), V(-3f, 3f, Moat1 + .6f), k.wallNoClimb, true);
            b.Box("Hinge post E", V(3f, 0f, Moat1), V(3.6f, 3f, Moat1 + .6f), k.wallNoClimb, true);

            // The rope: from a post by the moat back along the lane, with four gold handles.
            b.Box("Rope post", V(-.3f, 0f, Lane1 + .2f), V(.3f, 2.2f, Lane1 + .8f), k.wallNoClimb, true);
            b.Box("Rope", V(-.04f, .95f, Lane0), V(.04f, 1.03f, Lane1 + .5f), k.trimBlack, false, false);
            foreach (float u in new[] { 3f, 5f, 7f, 9f })
                b.Box("Handle", V(-.12f, .85f, u - .12f), V(.12f, 1.13f, u + .12f), k.rankGold, false, false);
            b.Box("Lane edge W", V(-Lane - .05f, 0f, Lane0), V(-Lane + .05f, .02f, Lane1), k.rankGold, false, false);
            b.Box("Lane edge E", V(Lane - .05f, 0f, Lane0), V(Lane + .05f, .02f, Lane1), k.rankGold, false, false);

            var game = b.parent.gameObject.AddComponent<MgDrawbridge>();
            game.station = station;
            game.hinge = body;
            return game;
        }

        static bool InLane(Vector3 local) =>
            Mathf.Abs(local.x) <= Lane && local.z >= Lane0 && local.z <= Lane1 && local.y > -.5f && local.y < 2.5f;

        void FixedUpdate()
        {
            if (station == null) return;
            if (!Completed)
            {
                int pulling = 0, pushing = 0;
                foreach (var p in station.Pawns())
                {
                    if (!InLane(p.Local)) continue;
                    if (p.Own && p.Velocity.z < -Walk) pulling++;
                    else if (!p.Own && p.Velocity.z > Walk) pushing++;
                }
                float rate = RatePerPawn * (PawnRushMissions.Workers(pulling) - (MissionStation.ReverseSabotage ? PawnRushMissions.Workers(pushing) : 0));
                progress = (float)PawnRushMissions.Latched(progress, progress + rate * Time.fixedDeltaTime);
                Progress01 = progress;
                if (progress >= 1f) Complete();
            }
            Pose();
        }

        // StateDrivenMover: the deck follows the game's progress, not the shared obstacle clock (offline only
        // until the host sends the station's state).
        void Pose()
        {
            if (hinge == null) return;
            var parent = hinge.transform.parent;
            var local = Quaternion.Euler(90f * (1f - Progress01), 0f, 0f);
            hinge.MoveRotation(parent != null ? parent.rotation * local : local);
        }

        public override void ResetGame()
        {
            base.ResetGame();
            progress = 0f;
            if (hinge != null) hinge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
