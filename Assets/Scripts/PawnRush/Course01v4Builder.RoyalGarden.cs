using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 승규 님's royal water garden (R263, 11-01): the Blender map (v06, made in a cloud chat, source outside the repo) as
    // one FBX, zone G of the test scene, beside the library (zone E). It is the Blender layout as it is: colliders from the
    // model's own meshes, the spinner, the two pendulums and the two team ferries moved by the shared clock with the
    // numbers of the Blender drivers, and water that sends a pawn back. Not yet: team-only boarding, checkpoints, a finish.
    //
    // Axes: Blender's FBX (forward -Z, up Y) comes into Unity as (x, y, z) -> (-x, z, -y) of Blender's coordinates
    // (PITFALLS §24). Under the same 180-degree pivot as the library it is (x, z, y): Blender's +Y, the way from the start
    // to the gate, is the zone's +Z; Blender's height is the zone's y.
    public static partial class Course01v4Builder
    {
        const string GardenModel = "PawnRushGarden/PawnRushRoyalWaterGarden";
        // Meshes in the model that nothing stands on: the water surfaces. The decoration (names V04_*) has no collider either.
        static readonly string[] GardenWater = { "해자_시각영역_물판", "V03_상단_수면_높이2.1m" };

        static void RoyalGarden(CourseBuilder b)
        {
            var model = Resources.Load<GameObject>(GardenModel);
            if (model == null)
            {
                Debug.LogError("Pawn Rush royal garden model is missing: Resources/" + GardenModel + ".fbx");
                return;
            }
            var pivot = new GameObject("Royal garden").transform;
            pivot.SetParent(b.parent, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var garden = Object.Instantiate(model, pivot, false);
            garden.name = "Royal garden model";

            foreach (var filter in garden.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.name.StartsWith("V04_") ||
                    System.Array.IndexOf(GardenWater, filter.name) >= 0) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }

            // The Blender drivers, with frame 1 = clock 0 at 30 fps:
            //   spinner   (frame - 1) * 2pi / 240 about Blender Z            one turn in 8 s
            //   pendulums 0.65 rad * sin(2pi t / 8 + 0 or pi) about Blender Y  the second half a swing later
            //   ferries   y = 41.5 - 3.5 cos(2pi t / 12)                    the two boats on the same timetable
            // (x, y, z) -> (x, z, y) is a mirror, so a turn about a Blender axis is the opposite turn about the zone's axis.
            Vector3 up = b.parent.up, ahead = b.parent.forward;
            var hub = GardenPart(garden.transform, "회전봉_PIVOT_Z_8초1회전");
            if (hub != null) hub.gameObject.AddComponent<Spinner>().Configure(GardenAxis(hub, up), -45f);
            string[] pendulums = { "V02_필수진자_PIVOT", "V02_필수진자_PIVOT.001" };
            for (int i = 0; i < pendulums.Length; i++)
            {
                var top = GardenPart(garden.transform, pendulums[i]);
                if (top == null) continue;
                var swing = top.gameObject.AddComponent<Pendulum>();
                swing.Configure(GardenAxis(top, ahead), -.65f * Mathf.Rad2Deg, 8f);
                swing.SetPhase(i * 4f);
            }
            foreach (var name in new[] { "V02_백팀_보트_PIVOT", "V02_흑팀_보트_PIVOT" })
            {
                var boat = GardenPart(garden.transform, name);
                if (boat == null) continue;
                // Oscillator swings either side of where it stands: stand it in the middle of the run (Blender y 41.5);
                // the phase puts it at the boarding dock (y 38) when the clock reads 0, as in Blender, and at the
                // landing (y 45) 6 s later.
                Vector3 half = ahead * 3.5f;
                boat.position += half;
                var ferry = boat.gameObject.AddComponent<Oscillator>();
                ferry.Configure(Quaternion.Inverse(boat.rotation) * half, 12f);
                ferry.SetPhase(9f);
            }

            // Water: a pawn that falls in goes back (KillVolume, 1.5 s). The moat round everything (surface y -1.7, its
            // floor y -2) and the upper reservoir under the ferries (surface y 2.1, its floor y 1.5). Zone axes.
            b.Trigger<KillVolume>("Moat (fall)", V(-46.5f, -3f, -58.5f), V(46.5f, -1.45f, 58.5f));
            b.Trigger<KillVolume>("Reservoir (fall)", V(-18f, 1f, 31f), V(18f, 1.95f, 59f));
        }

        static Transform GardenPart(Transform model, string name)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            Debug.LogError("Pawn Rush royal garden model has no " + name + "; it will not move.");
            return null;
        }

        // A zone direction as the part's own axis: Spinner, Pendulum and Oscillator take local axes.
        static Vector3 GardenAxis(Transform part, Vector3 direction) => Quaternion.Inverse(part.rotation) * direction;
    }
}
