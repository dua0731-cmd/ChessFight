using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        static void LibraryReplacement(CourseBuilder b)
        {
            var environment = Resources.Load<GameObject>("PawnRushLibrary/PawnRushLibraryEnvironment");
            var shuttleAsset = Resources.Load<GameObject>("PawnRushLibrary/PawnRushLibraryShuttle");
            if (environment == null || shuttleAsset == null)
            {
                Debug.LogError("Pawn Rush library models are missing from Resources/PawnRushLibrary.");
                return;
            }
            // The model's start is at +Z; face it toward the usual +Z course direction.
            var pivot = new GameObject("Library replacement").transform;
            pivot.SetParent(b.parent, false);
            pivot.localRotation = Quaternion.Euler(0, 180, 0);
            Object.Instantiate(environment, pivot, false).name = "Library environment";
            var shuttle = Object.Instantiate(shuttleAsset, pivot, false);
            shuttle.name = "Moving conveyor";
            foreach (var t in shuttle.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("CONVEYOR_CARRIAGE")) continue;
                var deck = new GameObject("Conveyor collision");
                deck.transform.position = pivot.TransformPoint(new Vector3(0, .24f, 4.91593f));
                deck.transform.rotation = pivot.rotation;
                deck.transform.SetParent(t, true);
                var box = deck.AddComponent<BoxCollider>();
                box.size = new Vector3(3.36f, .28f, 12.6f);
                deck.AddComponent<LibraryConveyorSurface>();
                break;
            }
            Box(pivot, "Start floor collision", new Vector3(0, -.12f, 17.5f), new Vector3(29.9f, .24f, 10f));
            Box(pivot, "Left balcony collision", new Vector3(-11.45f, -.12f, 2f), new Vector3(7.1f, .24f, 21f));
            Box(pivot, "Right balcony collision", new Vector3(11.45f, -.12f, 2f), new Vector3(7.1f, .24f, 21f));
            Box(pivot, "Goal floor collision", new Vector3(0, 1.08f, -20f), new Vector3(29.9f, .24f, 10f));
            for (int i = 0; i < 8; i++)
            {
                float h = .15f * (i + 1);
                float z = -8.75f - .5f * i;
                Box(pivot, "Left stairs collision " + i, new Vector3(-10.5f, h / 2, z), new Vector3(5f, h, .5f));
                Box(pivot, "Right stairs collision " + i, new Vector3(10.5f, h / 2, z), new Vector3(5f, h, .5f));
            }
        }

        static void Box(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }
    }

}
