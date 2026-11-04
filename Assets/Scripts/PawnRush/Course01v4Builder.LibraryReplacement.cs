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
            var hall = Object.Instantiate(environment, pivot, false);
            hall.name = "Library environment";
            // The start's compass rose, war map and armillary in their baked textures (R364).
            LibraryStartProps(hall);
            // The hand-built blower cannons in their flat gold, teal, cream and steel parts (R371).
            LibraryLooks(hall, "환경 모델");
            // The two blower cannons fire air across the conveyor in turn (R291); their fans are separate nodes
            // (ArtSource/PawnRush/Library/air_cannons.py).
            // Inside the hall the cannons' fan whirr is heard everywhere (R314), outside it not at all (R323): the cannon
            // measures the hall when it plays, since the zone is moved into its row after this build.
            int cannons = 0;
            foreach (var t in hall.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("BLOWER_") || t.name.EndsWith("_Fan")) continue;
                var fan = t.Find(t.name + "_Fan");
                if (fan == null) continue;
                t.gameObject.AddComponent<LibraryAirCannon>().Configure(fan, cannons++ * 2.5f, hall.transform);
            }
            if (cannons == 0) Debug.LogError("Pawn Rush library cannons have no separate fan (run air_cannons.py).");
            // The armillary turns as a gimbal (R324, ArtSource/PawnRush/Library/armillary.py); since R327 there is one,
            // Start_armillary, scaled up beside the start's war map (the goal's pair is gone).
            int armillaries = 0;
            foreach (var t in hall.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Start_armillary") continue;
                string n = t.name;
                var ring1 = t.Find(n + "_Ring1");
                var ring2 = ring1 != null ? ring1.Find(n + "_Ring2") : null;
                var ring3 = ring2 != null ? ring2.Find(n + "_Ring3") : null;
                var sphere = t.Find(n + "_Sphere");
                if (ring3 == null || sphere == null) continue;
                var marks = new[] { t.Find(n + "_Ring1_axis"), ring1.Find(n + "_Ring2_axis"), ring2.Find(n + "_Ring3_axis"), t.Find(n + "_Sphere_axis") };
                t.gameObject.AddComponent<LibraryArmillary>().Configure(ring1, ring2, ring3, sphere, marks, false);
                armillaries++;
            }
            if (armillaries == 0) Debug.LogError("Pawn Rush library armillary has no separate rings (run armillary.py).");
            var shuttle = Object.Instantiate(shuttleAsset, pivot, false);
            shuttle.name = "Moving conveyor";
            // The conveyor's painted deck tiles and chassis, its small parts in flat colours (R371).
            LibraryLooks(shuttle, "컨베이어 모델");
            // The whole map collides as drawn: walls, piers, balustrades, furniture, props (R379).
            LibraryCollision(hall, shuttle);
            // The rest of the clay in the concept art's materials, the banners off (R386); after the collision, so a
            // banner's collider goes off with it.
            LibraryDressing(hall);
            // R392 (승규 11-04, two shots - from above the hall, outside R388's box, bright and warm; from the pawn,
            // inside it, dark: "전체적인 조명이나 분위기를 1번 사진 처럼 하고 싶어"): no dimming of its own any more (R388's
            // ZoneExposure gone); what burned the piers white was the FBX lantern lights, now 6 (R389).
            // The shuttle's take is one 16 s round trip that ends where it starts. LibraryShuttleDriver samples it
            // from the shared clock every physics step, so it always loops and the deck moves in the riders' step
            // (R259; R258's Loop wrap mode on the Animation component still left 승규 님's conveyor not going back
            // and forth). The Animation component stays only as the clip's holder and is switched off.
            var anim = shuttle.GetComponent<Animation>();
            AnimationClip take = anim != null ? anim.clip : null;
            if (anim != null)
            {
                anim.playAutomatically = false;
                anim.enabled = false;
            }
            if (take == null) Debug.LogError("Pawn Rush library shuttle has no animation clip; the conveyor will not move.");
            Transform carriage = null;
            foreach (var t in shuttle.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("CONVEYOR_CARRIAGE")) continue;
                var deck = new GameObject("Conveyor collision");
                // R393 (승규 11-04 "기물이 붕 떠 보여"): its top on the belt links' top (0.20 m over the carriage, as the
                // start ramp's 0.19), not 0.38 - a pawn stood 18 cm over the belt.
                deck.transform.position = pivot.TransformPoint(new Vector3(0, .06f, 4.91593f));
                deck.transform.rotation = pivot.rotation;
                deck.transform.SetParent(t, true);
                var box = deck.AddComponent<BoxCollider>();
                box.size = new Vector3(3.36f, .28f, 12.6f);
                // A collider moved every step needs a kinematic body (R258), or PhysX treats it as static.
                var body = deck.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                deck.AddComponent<LibraryConveyorSurface>();
                // R396 ("북쪽 금색 쪽을 밟으면 ... 떨어져"): the rest of the carriage - chassis, end drums, side beads,
                // idlers, rail shoes, fittings - collides as drawn (the FBX is readable, R379) instead of only the belt's
                // box; one kinematic body carries them (the deck keeps its own), and they ride its travel.
                int frame = 0;
                foreach (var f in t.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.name.StartsWith("BELT_LINK_") || f.sharedMesh == null || f.GetComponent<Collider>() != null) continue;
                    f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
                    frame++;
                }
                if (t.GetComponent<Rigidbody>() == null)
                {
                    var frameBody = t.gameObject.AddComponent<Rigidbody>();
                    frameBody.isKinematic = true;
                    frameBody.useGravity = false;
                }
                t.gameObject.AddComponent<LibraryCarriageSurface>();
                Debug.Log("[작전 회의실] 컨베이어 캐리지 부품 " + frame + "개에 충돌(틀 · 끝 드럼 · 옆 테 등)");
                carriage = t;
                break;
            }
            if (carriage == null) Debug.LogError("Pawn Rush library shuttle has no CONVEYOR_CARRIAGE node.");
            var driver = shuttle.AddComponent<LibraryShuttleDriver>();
            driver.Configure(take, carriage);
            if (carriage != null) shuttle.AddComponent<LibraryConveyorSound>().Configure(driver, carriage);
            Box(pivot, "Start floor collision", new Vector3(0, -.12f, 17.5f), new Vector3(29.9f, .24f, 10f));
            Box(pivot, "Left balcony collision", new Vector3(-11.45f, -.12f, 2f), new Vector3(7.1f, .24f, 21f));
            Box(pivot, "Right balcony collision", new Vector3(11.45f, -.12f, 2f), new Vector3(7.1f, .24f, 21f));
            Box(pivot, "Goal floor collision", new Vector3(0, 1.08f, -20f), new Vector3(29.9f, .24f, 10f));
            // The basement is now a return route: land on its floor, then use the outside stair to reach the start.
            // The FBX reverses Blender X and Y into Unity X and Z before the enclosing pivot's 180-degree turn.
            Box(pivot, "Lower library floor collision", new Vector3(0, -8.35f, 0f), new Vector3(30f, .7f, 45f));
            // R378 (승규 11-04 "왼쪽도 설치해서 대칭으로 해"): a return stair on each side (ArtSource/PawnRush/Library/
            // return_stairs.py), the left one (x > 0 here) mirroring the right. Its landings out to the balustrade's
            // stringer (Blender x 19.4). The upper landing starts where the top step ends (z 15.8) instead of reaching
            // over the last four steps (a 0.75 m lip at the top), with a threshold strip by the door (x 13 .. 16.1).
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "" : " (left)";
                Box(pivot, "Return lower landing collision" + s, new Vector3(side * 16.2f, -8.15f, 3.6f), new Vector3(6.4f, .3f, 3f));
                Box(pivot, "Return upper landing collision" + s, new Vector3(side * 16.2f, -.15f, 16.65f), new Vector3(6.4f, .3f, 1.7f));
                Box(pivot, "Return upper threshold collision" + s, new Vector3(side * 14.55f, -.15f, 15.15f), new Vector3(3.1f, .3f, 1.3f));
                for (int i = 0; i < 32; i++)
                {
                    float height = .25f * (i + 1);
                    float z = 3.8f + (i + .5f) * .375f;
                    Box(pivot, "Return stair collision " + i + s, new Vector3(side * 17.6f, -8f + height / 2f, z), new Vector3(3f, height, .38f));
                }
            }
            for (int i = 0; i < 8; i++)
            {
                float h = .15f * (i + 1);
                float z = -8.75f - .5f * i;
                Box(pivot, "Left stairs collision " + i, new Vector3(-10.5f, h / 2, z), new Vector3(5f, h, .5f));
                Box(pivot, "Right stairs collision " + i, new Vector3(10.5f, h / 2, z), new Vector3(5f, h, .5f));
            }
            // R397 (승규 11-04 "작전 회의실 쪽 계단 자연스럽게 올라가게 해 지금 자연스럽게 못올라가"): on the step boxes a
            // pawn hopped every step (PawnRushStepOver: a lip of up to 0.25 m, then 0.3 s before the next). Each flight
            // now has a smooth ramp over its nosings, as the grand stair hall's stairs, so it is walked up; the steps
            // stay under it (every box's top lies on or under the ramp) and the drawn stairs are as they were. The
            // ramps ride a few centimetres over the nosing line, over the models' rounded nosings.
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0 ? " (left)" : " (right)";
                // The goal stairs: 8 steps of 0.15 x 0.5 m from the start floor (z -8.0) to the goal floor (1.2 at
                // z -12.0), the full width of the balcony they leave (x 7.9 .. 15.0, the outer extension's treads too).
                Ramp(pivot, "Goal stairs ramp" + s, new Vector3(side * 11.45f, .02f, -8f), new Vector3(side * 11.45f, 1.22f, -12f), 7.1f);
                // The outside return stairs: 32 steps of 0.25 x 0.375 m from the lower landing (-8 at z 3.425) to the
                // upper landing (0 at z 15.425), 34 degrees.
                Ramp(pivot, "Return stairs ramp" + s, new Vector3(side * 17.6f, -7.96f, 3.425f), new Vector3(side * 17.6f, .04f, 15.425f), 3f);
            }
        }

        static void Box(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        // A walked ramp whose top runs from `from` up to `to` (the parent's space), `width` across, 0.4 m thick under
        // its top.
        static void Ramp(Transform parent, string name, Vector3 from, Vector3 to, float width)
        {
            const float Thick = .4f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var along = to - from;
            var turn = Quaternion.LookRotation(along.normalized, Vector3.up);   // z up the ramp, y its normal
            go.transform.localRotation = turn;
            go.transform.localPosition = (from + to) * .5f - turn * Vector3.up * (Thick * .5f);
            go.AddComponent<BoxCollider>().size = new Vector3(width, Thick, along.magnitude);
            go.AddComponent<CourseFloor>().sloped = true;
        }
    }

}
