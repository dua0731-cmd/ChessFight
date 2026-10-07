using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Fairness by mirror (design doc v0.2 §6): a team section is built for white only (x < 0)
    // and black is made from it by reflecting x -> -x about the world's x = 0 plane (the
    // course root sits at the origin, so its groups' x = 0 is the world's).
    //
    // A reflection is applied to every local transform below the copy: position x
    // negated, rotation (x, y, z, w) -> (x, -y, -z, w), scale kept. Doing it at every
    // level reflects the whole tree exactly, because cubes, cylinders and spheres are
    // their own mirror images. An imported obstacle prefab is NOT symmetric inside, so
    // the reflection stops at its root (anything with an ObstacleContext): it is placed
    // and turned as the mirror image, its parts are left as they are.
    //
    // What a transform cannot carry is fixed by hand: a sliding axis has its x
    // negated, a turning axis becomes (x, -y, -z) (a turn seen in a mirror runs the
    // other way), the wind's direction has its x negated, a spinning disc turns the
    // other way. Phases stay the same: both teams see the same thing at the same time.
    public static class TeamMirror
    {
        public const string WhiteName = "Team_White", BlackName = "Team_Black";

        // The black half's name for a white one: W1_White -> W1_Black, Team_White -> Team_Black.
        public static string BlackOf(string white) => white.EndsWith("_White") ? white.Substring(0, white.Length - 6) + "_Black" : BlackName;

        // Builds (or rebuilds) the black half next to the white one.
        public static Transform MirrorTeam(Transform white)
        {
            var black = MirrorGroup(white, BlackOf(white.name));
            SetTeam(black.gameObject, Teams.Black);
            return black;
        }

        // A mirror image of a whole group next to it, replacing an earlier one of that name (the
        // tower's east ramp from its west ramp). Teams are left as they are.
        public static Transform MirrorGroup(Transform source, string name)
        {
            var parent = source.parent;
            var old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var copy = Object.Instantiate(source.gameObject, parent).transform;
            copy.name = name;
            copy.SetSiblingIndex(source.GetSiblingIndex() + 1);
            Reflect(copy, false);
            return copy;
        }

        // A mirrored copy of one object about its parent's x = 0 (the "mirror selected" menu for a
        // shared module's left/right pairs). Back-and-forth obstacles are moved half a cycle so the
        // pair is a mirror image at every moment (design doc §3); a turning pair turns opposite ways.
        public static GameObject MirrorCopy(GameObject source, bool halfCycle)
        {
            var copy = Object.Instantiate(source, source.transform.parent);
            copy.name = source.name.EndsWith(" L") ? source.name.Substring(0, source.name.Length - 2) + " R" : source.name + " (mirror)";
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            Reflect(copy.transform, true);
            if (halfCycle) ObstaclePhase.Shift(copy, ObstaclePhase.HalfPeriod(copy));
            return copy;
        }

        // Reflects `t`'s own local transform (when `self`) and everything under it.
        static void Reflect(Transform t, bool self)
        {
            if (self)
            {
                var p = t.localPosition;
                t.localPosition = new Vector3(-p.x, p.y, p.z);
                var q = t.localRotation;
                t.localRotation = new Quaternion(q.x, -q.y, -q.z, q.w);
            }
            bool unit = t.GetComponent<ObstacleContext>() != null;
            if (unit) FixValues(t.gameObject, true);
            else
            {
                FixValues(t.gameObject, false);
                foreach (Transform child in t) Reflect(child, true);
            }
        }

        // Directions a transform does not carry. `subtree` covers a whole obstacle prefab at once.
        static void FixValues(GameObject go, bool subtree)
        {
            var motions = subtree ? go.GetComponentsInChildren<ObstacleMotion>(true) : go.GetComponents<ObstacleMotion>();
            foreach (var m in motions)
            {
                var a = m.axis;
                m.axis = m.kind == MotionKind.Rotate ? new Vector3(a.x, -a.y, -a.z) : new Vector3(-a.x, a.y, a.z);
            }
            var vents = subtree ? go.GetComponentsInChildren<AirVent>(true) : go.GetComponents<AirVent>();
            foreach (var v in vents) v.WindDirection = new Vector3(-v.WindDirection.x, v.WindDirection.y, v.WindDirection.z);
            var surfaces = subtree ? go.GetComponentsInChildren<ObstacleSurface>(true) : go.GetComponents<ObstacleSurface>();
            foreach (var s in surfaces)
            {
                s.counterClockwise = !s.counterClockwise;
                s.direction = new Vector3(-s.direction.x, s.direction.y, s.direction.z);
                s.launchVelocity = new Vector3(-s.launchVelocity.x, s.launchVelocity.y, s.launchVelocity.z);
            }
        }

        // Everything under `root` that belongs to a team now belongs to `team`.
        public static void SetTeam(GameObject root, int team)
        {
            foreach (var x in root.GetComponentsInChildren<SpawnPoint>(true)) x.Configure(x.Index, team);
            foreach (var x in root.GetComponentsInChildren<CourseCheckpoint>(true)) x.SetTeam(team);
            foreach (var x in root.GetComponentsInChildren<TeamBarrier>(true)) x.SetTeam(team);
            foreach (var x in root.GetComponentsInChildren<MiniGameSlot>(true)) x.SetTeam(team);
            foreach (var x in root.GetComponentsInChildren<PromotionZone>(true)) x.SetTeam(team);
            foreach (var x in root.GetComponentsInChildren<TeamTint>(true)) x.Apply(team);
            foreach (var x in root.GetComponentsInChildren<TeamZone>(true)) x.SetTeam(team);
        }
    }
}
