using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R783 (the user 2026-11-23: "정원 맵이나 (배를 건너는 부분에서 배는 사라졌지만 건널 수 있는 다리가 없음) 마차뜰처럼
    // 쉬운 길 코스가 반영되지 않은 맵들이 있어. 뒤 미니게임구간이 클리어되면 발동되는 트리거가 발동되면 플레이어가 컨트롤
    // 부담 없이 바로바로 건널 수 있도록 현재 구현된 모든 맵에 길을 기획하고 만들어줘"): the easy way of a section's
    // relief (SectionRelief). With the relief given, a pawn WALKS the section from its entry to its exit: no jump, no
    // ride, no timing, nothing to push. What strikes or carries is switched off or stilled by SectionRelief; what is
    // left - water, a gap, a height only a lift or a jump reaches - gets a deck here: a walkway of boxes with a rail
    // either side, laid in the section's own frame along a line of points (a deck's top is the line).
    //
    // The three obstacle sections a mini-game follows (slots 1 and 3 of a round), measured in a copy of the project
    // (the ground under each route every half metre, R783's probe) and planned stretch by stretch:
    //
    //   the Royal Water Garden   the eight floating drums: stilled at their middle height and a causeway over them
    //                            from the west landing to the east one; the reservoir: a bridge from the boarding
    //                            quay to the gate yard (the boats go).
    //   the Royal Carriage Yard  the giant waterwheel: gone, and the two walkways it cut are planked over; the
    //                            warehouse: the conveyor, the two storage rows, the timber cranes and the loft's
    //                            hoists gone, and one ramp from the crane yard up onto the loft, over the loading
    //                            bay and the mill pond where the lifting waterwheel stood, to the cliff's landing
    //                            (0 -> 8 m, 11 to 12 degrees); the final gate stands open.
    //   the Grand Stair Hall     the floating stones: stilled and decked over; the jump pad: gone, a ramp from the
    //                            north landing up to the first dive landing; the two dive gaps: decked; the glass
    //                            stones: a deck down their middle; the clock gate's doors: gone.
    //
    // The late obstacle slot (the war room, the Royal Library, the Royal Observatory) has no mini-game after it, so
    // no relief is ever given there and none is planned.
    //
    // A slope a pawn walks is 14 degrees at most (the grand stair's own walking slope; a 0.25 m step is a wall to the
    // ragdoll), so every ramp here is under 13. Decks lie 3 cm over the floors they join. Built on every PC when it
    // gives the relief, like the rest of it; nothing is sent.
    public static class ReliefWay
    {
        public const string Garden = "Early_1-1_RoyalWaterGarden", Yard = "Early_1-2_RoyalCarriageYard", Hall = "Mid_2-1_GrandStairHall";
        const float Lift = .03f, Thick = .3f, RailHigh = 1f, RailThick = .16f, Overlap = .35f;

        // The stretches of a section's easy way: the lines its decks are laid along (section frame, on the floors
        // they join - the decks' tops are Lift over them), their widths, and whether they are railed.
        public struct Stretch
        {
            public string Name;
            public Vector3[] Line;
            public float Width;
            public bool Rails;

            public Stretch(string name, float width, bool rails, params Vector3[] line)
            {
                Name = name;
                Width = width;
                Rails = rails;
                Line = line;
            }
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public static Stretch[] Plan(string sectionId)
        {
            switch (sectionId)
            {
                case Garden:
                    return new[]
                    {
                        // The drums' tops stand at 2.35 + 0.25 a drum when stilled (2.35 .. 4.10); the causeway clears
                        // each by a hand's breadth along the line between their two rows.
                        new Stretch("Drum causeway", 3f, true, V(-27.6f, 1.8f, 4.5f), V(-24.2f, 2.45f, 4.5f), V(23.4f, 4.45f, 4.5f), V(26.8f, 3.8f, 4.5f)),
                        new Stretch("Reservoir bridge", 4f, true, V(0f, 3.8f, 32.4f), V(0f, 3.8f, 49.2f)),
                    };
                case Yard:
                    return new[]
                    {
                        new Stretch("Mill planks W", 3.2f, false, V(-4.6f, 0f, 63.6f), V(-4.6f, 0f, 68.4f)),
                        new Stretch("Mill planks E", 3.2f, false, V(4.6f, 0f, 63.6f), V(4.6f, 0f, 68.4f)),
                        // Up onto the loft (2.4 m), across it, and on over the loading bay and the pond to the
                        // cliff's landing (8 m).
                        new Stretch("Warehouse ramp", 3.2f, true, V(0f, 0f, 101.4f), V(0f, 2.4f, 113.5f), V(0f, 2.4f, 116.5f), V(0f, 8f, 141.9f)),
                    };
                case Hall:
                    return new[]
                    {
                        // Along the eight floating stones, from the curving stair's top to the north landing.
                        new Stretch("Floating stones deck", 2.6f, true,
                            V(24.97f, 9.96f, 96.69f), V(24.85f, 10f, 97.28f), V(24.2f, 10.09f, 99.06f), V(22.55f, 10.28f, 102.4f), V(20.45f, 10.47f, 105.41f),
                            V(17.96f, 10.66f, 108.03f), V(15.15f, 10.84f, 110.22f), V(12.1f, 11.03f, 111.93f), V(8.87f, 11.22f, 113.15f), V(5.55f, 11.41f, 113.86f),
                            V(1.93f, 11.5f, 114.06f), V(1.34f, 11.5f, 113.97f)),
                        // From the north landing over where the jump pad stood, up to the first dive landing.
                        new Stretch("Jump pad ramp", 2.6f, true, V(-5.2f, 11.5f, 112.47f), V(-14.3f, 14f, 104.45f), V(-15.05f, 14f, 103.79f)),
                        new Stretch("Dive gap 1", 2.6f, true, V(-17.56f, 14f, 98.55f), V(-17.79f, 14f, 97.78f), V(-18.08f, 14f, 94.19f), V(-17.96f, 14f, 93.4f)),
                        new Stretch("Dive gap 2", 2.6f, true, V(-17.49f, 14f, 90.35f), V(-17.37f, 14f, 89.56f), V(-15.94f, 14f, 86.1f), V(-15.46f, 14f, 85.46f)),
                        // Down the middle of the glass stones, from the dais to the balcony.
                        new Stretch("Glass stones deck", 2.4f, true, V(0f, 16f, 99.2f), V(0f, 16f, 121.2f)),
                    };
                default:
                    return new Stretch[0];
            }
        }

        // Lays the section's decks. Returns how many stretches (0 for a section with no plan).
        public static int Open(PawnRushSection section)
        {
            if (section == null) return 0;
            var plan = Plan(section.Id);
            if (plan.Length == 0) return 0;
            var root = new GameObject("Relief way (easy way)");
            root.transform.SetParent(section.transform, false);
            Material deck = Arena.Lit("M_ReliefDeck", new Color(.9f, .82f, .62f), 0f, .75f), rail = Arena.TrimGold;
            foreach (var stretch in plan)
            {
                var group = new GameObject(stretch.Name);
                group.transform.SetParent(root.transform, false);
                for (int i = 0; i + 1 < stretch.Line.Length; i++)
                    Span(group.transform, i + 1, stretch.Line[i] + Vector3.up * Lift, stretch.Line[i + 1] + Vector3.up * Lift, stretch.Width, stretch.Rails, deck, rail);
            }
            return plan.Length;
        }

        // One straight piece from a to b (its top's middle line), a little longer either end so that pieces meeting
        // at a bend leave no wedge open.
        static void Span(Transform parent, int number, Vector3 a, Vector3 b, float width, bool rails, Material deck, Material rail)
        {
            Vector3 along = b - a;
            float length = along.magnitude;
            if (length < .01f) return;
            var facing = Quaternion.LookRotation(along / length, Vector3.up);
            var piece = new GameObject("Span " + number);
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = (a + b) * .5f;
            piece.transform.localRotation = facing;
            bool sloped = Mathf.Abs(b.y - a.y) > .01f;
            Box(piece.transform, "Deck", new Vector3(0f, -Thick * .5f, 0f), new Vector3(width, Thick, length + Overlap * 2f), deck, true, sloped);
            if (!rails) return;
            float side = width * .5f - RailThick * .5f;
            Box(piece.transform, "Rail L", new Vector3(-side, RailHigh * .5f, 0f), new Vector3(RailThick, RailHigh, length + Overlap * 2f), rail, false, false);
            Box(piece.transform, "Rail R", new Vector3(side, RailHigh * .5f, 0f), new Vector3(RailThick, RailHigh, length + Overlap * 2f), rail, false, false);
        }

        static void Box(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool floor, bool sloped)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = at;
            box.transform.localScale = size;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
            if (floor) box.AddComponent<CourseFloor>().sloped = sloped;
            box.AddComponent<NoClimbSurface>();
        }

        // A thing that goes back and forth (ObstacleMotion) comes to rest at its middle over Seconds - a floating
        // drum, a floating stone - so that a deck can lie over it and nobody on it is thrown.
        public static bool Still(ObstacleMotion motion)
        {
            if (motion == null || motion.GetComponent<ReliefStill>() != null) return false;
            motion.gameObject.AddComponent<ReliefStill>().Begin(motion);
            return true;
        }
    }

    // A push gate swings open by itself over Seconds (set open in one step its leaves would sweep whoever stands by).
    public sealed class ReliefOpen : MonoBehaviour
    {
        const float Seconds = 2f;
        PushGate gate;

        void Awake() => gate = GetComponent<PushGate>();

        void Update()
        {
            if (gate == null || gate.IsOpen) { enabled = false; return; }
            gate.SetProgress(gate.Progress + Time.deltaTime / Seconds);
        }
    }

    public sealed class ReliefStill : MonoBehaviour
    {
        const float Seconds = .8f;
        ObstacleMotion motion;
        float from, since;

        public void Begin(ObstacleMotion of)
        {
            motion = of;
            from = of.distance;
        }

        void Update()
        {
            if (motion == null) { enabled = false; return; }
            since += Time.deltaTime;
            motion.distance = Mathf.Lerp(from, 0f, Mathf.Clamp01(since / Seconds));
            if (since >= Seconds) enabled = false;
        }
    }
}
