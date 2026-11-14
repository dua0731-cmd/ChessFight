using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // R619 (승규, armillary-sphere picture: "회전 시계가 첫 번 째 사진 같이 3D 3차원적으로 웅장하게 교차하면서 돌아
    // 갔으면 좋겠고 안에 들어 있는 건 체스 킹으로 해줘"): a great gold armillary sphere stands on the balcony in front
    // of the round window (Armillary.fbx, ArtSource/Lobby/KingdomInterior/armillary.py, 5.2 m across). Its rings are
    // nested gimbals, each turning about its own axis inside the one before, so they keep crossing in depth; an ebony and gold
    // chess king floats and turns in the middle. The rings cast the sun's shadow, so they also pass through the light
    // shafts (RoseShafts reads the sun's shadow map). The flat tracery behind no longer turns (RoseTurnSpeed 0).
    public sealed partial class LobbyStage
    {
        // degrees a second about each gimbal's own axis (A: vertical, B: left-right, C: across B's band); the king
        const float ArmillaryA = 6f, ArmillaryB = -10f, ArmillaryC = 15f, ArmillaryKing = -8f;
        const float KingBob = .06f, KingBobSpeed = 1.4f;   // metres up and down, radians a second
        // stone: R618 mocha. R623: the king is polished ebony with gold and rubies (armillary.py king_mesh; was lapis 0x1B2E6E)
        const int ArmillaryGold = 0xE8B54E, ArmillaryEbony = 0x15100D, ArmillaryRuby = 0x9E0F22, ArmillaryStone = 0xB29C89;

        Transform[] gimbals;          // A, B, C, king
        Vector3[] gimbalAxes;         // each in its parent's local space at rest
        Vector3 kingRest;

        void HallArmillary()
        {
            gimbals = null;
            var prefab = Resources.Load<GameObject>("Lobby/KingdomHall/Armillary");
            if (prefab == null || castle == null) return;
            // Exported like Hall.fbx and in the hall's own coordinates, so it sits right under the hall untouched.
            var model = Instantiate(prefab, castle.transform, false);
            model.name = "Armillary Sphere";
            Transform Part(string n) => FindDeep(model.transform, n);
            var centre = Part("Armillary_Centre");
            var parts = new[] { Part("Armillary_A"), Part("Armillary_B"), Part("Armillary_C"), Part("Armillary_King") };
            var pins = new[] { Part("Armillary_Axis_A"), Part("Armillary_Axis_B"), Part("Armillary_Axis_C"), Part("Armillary_Axis_A") };
            if (centre == null || System.Array.Exists(parts, p => p == null) || System.Array.Exists(pins, p => p == null)) return;

            Material Mat(string n) => n.StartsWith("Ebony") ? PieceFigure.Lit(MenuArt.Hex(ArmillaryEbony), .92f, 0f)
                                    : n.StartsWith("Ruby") ? PieceFigure.Lit(MenuArt.Hex(ArmillaryRuby), .9f, 0f)
                                    : n.StartsWith("Stone") ? PieceFigure.Lit(MenuArt.Hex(ArmillaryStone), .25f, 0f)
                                    : PieceFigure.Lit(MenuArt.Hex(ArmillaryGold), .82f, .9f);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Mat(mats[i] != null ? mats[i].name : "");
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
            }

            // A pivot on the centre for each gimbal, nested A > B > C (the king on its own), the mesh moved under it
            // where it is. Axes come from the empties (centre + 1 m along the axis) in world space at rest.
            gimbals = new Transform[4];
            gimbalAxes = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                var parent = i is 0 or 3 ? model.transform : gimbals[i - 1];
                var pivot = new GameObject("Armillary Gimbal " + "ABCK"[i]).transform;
                pivot.SetParent(parent, false);
                pivot.position = centre.position;
                pivot.localRotation = Quaternion.identity;
                gimbalAxes[i] = parent.InverseTransformDirection((pins[i].position - centre.position).normalized);
                parts[i].SetParent(pivot, true);
                gimbals[i] = pivot;
            }
            kingRest = gimbals[3].localPosition;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }

        void TickArmillary(float t)
        {
            if (gimbals == null) return;
            float[] speed = { ArmillaryA, ArmillaryB, ArmillaryC, ArmillaryKing };
            for (int i = 0; i < 4; i++)
                gimbals[i].localRotation = Quaternion.AngleAxis(Mathf.Repeat(t * speed[i], 360f), gimbalAxes[i]);
            gimbals[3].localPosition = kingRest + gimbalAxes[3] * (KingBob * Mathf.Sin(t * KingBobSpeed));
        }
    }
}
