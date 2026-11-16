using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R678 (승규 11-15, through the peer session: "마차뜰 블록아웃 채팅에서 그 구간 모델링 작업을 할 때 이거 쓰라고 하면 돼",
    // "일단 에셋을 늘려서 써도 되고 이상하면 추후에 바꿈"; then "색이 없는 에셋은 에셋들과 색을 맞춘 다음 적용 시켜"): the AI-3D
    // castle kit over the gates, drawn in its own textures (not the clay), built by castle_model.py
    // (ArtSource/PawnRush/CarriageYardModels) from the five sources in source_ai3d/:
    //
    //   RcyCastleOuterGate   the outer gatehouse on the 6 m opening, the gate wall's wings out to x +-23, the slim rook
    //                        tower where the scenery's four round corner towers of the yard and the canal stand
    //   RcyCastleFinalGate   the final gatehouse on the 8 m opening, its wall's wings out to x +-26, the two corner
    //                        towers behind it
    //   RcyCastleSectionEnd  as a section: the boundary's gateway (two piers on the 12 m arch, the beam, the brass crown),
    //                        the marble balustrade along its railings, and on each of the code's four steps one of the
    //                        asset's (R679: its flight could not be stretched to them; the code's blocks still collide)
    //
    // Each FBX's slots are named after its source (RcyCastle_OuterGate, _FinalGate, _Walls, _Towers, _SectionEnd), the
    // texture's name in Resources/PawnRushCarriageYard (the base colour, and "_N" its normal map).
    //
    // Collisions stay boxes (승규 "전해": box colliders only, never the models' meshes). Where a pawn touches, the model was
    // fitted to the blockout's colliders: the passages' jambs (the final gatehouse's, which splayed 4.7 .. 4.0, were
    // straightened onto x +-4 so its door meets the stone), the gatehouses' faces, the piers' and the balustrade's inner
    // faces. Where it could not be, boxes go where the model is (castle_model.py, guides/rcy_castle_boxes.json, written
    // into CastleBoxData below) and the blockout's pieces there stop colliding: the gatehouses are deeper than the 2 m
    // gate walls and their round towers stand out of them (each tower 16 edge boxes); the gate walls' segments have their
    // face set back 0.4 m between piers; the outer passage's mouths step back 0.5 m on both sides (left open); the
    // piers rise past the railings. Measured in Play mode against the meshes (R678): what a pawn touches is within ~10 cm
    // but for the towers' bronze studs and bands, which stand out of the stone above them. No opening, path or gap
    // changed: the passages are 6 and 8 m as before, only deeper (the outer one 0.5 .. 3.05 at 6 m, the final one 155 ..
    // 159.5 from the platform's face).
    //
    // R679 (승규 "미뤄 둔 성문 문제 먼저 고치기"), measured again in Play mode with rays from each box's faces: the towers'
    // tiers, bands and studs fitted onto their boxes (studs off the stone moved onto it, the band boxes per tier); the gate
    // walls' piers stood on plinth boxes where their foot is wider; the walls' seams at the piers closed; the final
    // passage's far face boxed in two parts where it bends into the tower; the final gatehouse's squashed top raised to
    // 25.6 m; the banners moved out of the piers and towers
    // onto the wall between them; every castle group's labels drawn depth-tested (CastleLabels, after the map is switched
    // on, so the push gate's own label too) so none shows through stone or mirrored from behind; the section end's
    // gateway moved 0.133 m and narrowed 1.6 % (its opening was off the asset's middle) so both piers' inner faces stand
    // on the 6 m boxes, its piers' courses and the balustrade cut straight (they were torn where faces were picked by
    // their centres) and the asset's steps laid on the code's four. Left: where the stone steps back at a tier's top
    // (10 .. 15 cm) and at bevelled corners, and faces no pawn reaches.
    //
    // R680 (승규 "내가 즌 에셋 사이사이 깨진 부분 있어 매워"): the asset's meshes come split along their UV seams, and the
    // decimation pulled those seams apart into hairline cracks the sky showed through; castle_model.py's part() now welds
    // them shut (1e-5) before it decimates. A render of the three models against a flat colour found ~3000 crack pixels
    // before and 1 after; the towers measured from the platform side again within 5 cm of their boxes. Then (승규 "진행")
    // the seam down the pier where two gate wall segments meet (x +-16 on the outer gate, +-18 on the final one): the
    // wall part's two ends are not alike, so one half of that pier was lit and the other dark, their blocks at other
    // heights. Each side's outer segment is now the part mirrored and the ends' normals straightened, so a joint is one
    // end against its own mirror image; the piers' boxes moved with them (by 2 to 6 cm).
    public partial class RoyalCarriageYard
    {
        // Blockout pieces a castle model draws for: direct children `name` of the group at `group` under the built root,
        // with absX / z only those whose position in the group's frame has |x| and z in those ranges.
        sealed class Cover
        {
            public string group, name;
            public Vector2? absX, z;
        }

        // A piece put elsewhere for a castle model (R679): to absX on its own side, x, y, z (the map frame; what is null
        // stays), and for a label a new character size (0 keeps it).
        sealed class Move
        {
            public Cover what;
            public float? absX, x, y, z;
            public float textSize;
        }

        sealed class CastleModel
        {
            public string model;
            public bool sectionOnly;
            public Cover[] covers = new Cover[0];     // stop drawing (their colliders stay)
            public Cover[] uncollide = new Cover[0];  // stop colliding (the model stands where they stood out)
            public Move[] moves = new Move[0];        // put where the model leaves room for them
            public string[] labels = new string[0];   // groups whose labels the model now hides from behind (WorldText)
            public float[] boxes = new float[0];      // added: centre (map frame), size, yaw about y, tilt; 8 numbers a box
        }

        const string FinalGroup = "3-4 (own frame)/4 Final gate", OwnScenery = "3-4 (own frame)/Scenery (own frame)";

        static Cover C(string group, string name) => new Cover { group = group, name = name };

        static Cover[] Towers(string group) =>
            new[] { C(group, "Tower"), C(group, "Tower band"), C(group, "Tower roof"), C(group, "Tower finial") };

        static Cover[] Join(params Cover[][] lists)
        {
            var all = new List<Cover>();
            foreach (var l in lists) all.AddRange(l);
            return all.ToArray();
        }

        static readonly CastleModel[] CastleModels =
        {
            new CastleModel
            {
                // The gate itself is in zone 1, which RcyEntrance's dressing stops drawing whole.
                model = "RcyCastleOuterGate",
                covers = Towers("Scenery"),
                // The blockout's towers stood 0.5 m out of the model's wall, its gate walls' faces in front of the model's.
                uncollide = new[] { C("1 Entrance", "Tower"), C("1 Entrance", "Gate wall W"), C("1 Entrance", "Gate wall E") },
                moves = new[]
                {
                    // The banners on the gate wall's yard face hung inside the towers (x +-10) and the piers (+-17): to
                    // the face between them (1.96 .. 1.99 there), under its cornice (from y 5.45).
                    new Move { what = new Cover { group = "Scenery", name = "Banner", absX = new Vector2(8f, 12f), z = new Vector2(0f, 4f) }, absX = 13.2f, y = 4.9f },
                    new Move { what = new Cover { group = "Scenery", name = "Banner", absX = new Vector2(16f, 18f), z = new Vector2(0f, 4f) }, absX = 19.5f, y = 4.9f },
                    // The title lay on the crest's shield: to the lintel under it (y 5.6 .. 6.55, its face at z .33; the
                    // shield's point, z .15, falls between the words), and the start countdown that was there to the
                    // closed gate's leaves, under the zone's sign (4.3).
                    new Move { what = C("1 Entrance", "Label 왕실 마차뜰"), x = 0f, y = 6.05f, z = .1f, textSize = .11f },
                    new Move { what = C("1 Entrance", "Label "), x = 0f, y = 2.9f, z = -.3f },
                },
                labels = new[] { "1 Entrance" },
                boxes = CastleBoxData.Outer,
            },
            new CastleModel
            {
                model = "RcyCastleFinalGate",
                covers = Join(new[] { C(FinalGroup, "Gate wall W"), C(FinalGroup, "Gate wall E"), C(FinalGroup, "Gate lintel"),
                                      C(FinalGroup, "Gate arch trim") },
                              Towers(FinalGroup), Towers(OwnScenery)),
                // The gate walls' faces stood in front of the model's, the blockout's towers out of its corners.
                uncollide = new[] { C(FinalGroup, "Gate wall W"), C(FinalGroup, "Gate wall E"), C(FinalGroup, "Tower") },
                moves = new[]
                {
                    // The banners on the wall's platform face hung inside the towers (x +-12) and the piers (+-18): to
                    // the face between them (z 155.4 there), under its cornice (from y 16.65).
                    new Move { what = new Cover { group = OwnScenery, name = "Banner", absX = new Vector2(10f, 14f), z = new Vector2(GateZ - 3f, GateZ) }, absX = 14.7f, y = 16.4f, z = 155.38f },
                    new Move { what = new Cover { group = OwnScenery, name = "Banner", absX = new Vector2(16f, 20f), z = new Vector2(GateZ - 3f, GateZ) }, absX = 22f, y = 16.4f, z = 155.38f },
                },
                labels = new[] { FinalGroup },
                boxes = CastleBoxData.Final,
            },
            new CastleModel
            {
                model = "RcyCastleSectionEnd",
                sectionOnly = true,
                covers = new[] { C(FinalGroup, "Arch pillar"), C(FinalGroup, "Arch lintel"), C(FinalGroup, "Runout railing"),
                                 C(FinalGroup, "Band railing"), C(FinalGroup, "Step down 1"), C(FinalGroup, "Step down 2"),
                                 C(FinalGroup, "Step down 3"), C(FinalGroup, "Step down 4") },
                boxes = CastleBoxData.Section,
            },
        };

        // The boxes castle_model.py measured (guides/rcy_castle_boxes.json; its castle_sync_cs writes them here, not by
        // hand): centre (map frame), size, yaw about y, tilt about the box's own x (the towers' leaning tiers lean in
        // with the stone), 8 numbers a box.
        // <castle boxes>
        static class CastleBoxData
        {
            public static readonly float[] Outer =
            {
                // Outer gate tower
                -4.976f, 0.748f, 3.308f, 0.27f, 1.452f, 1f, -73.79f, -2.65f,
                -5.255f, 2.153f, 3.213f, 0.197f, 1.566f, 1f, -83.82f, 8.22f,
                -5.388f, 4.97f, 3.395f, 0.134f, 4.007f, 1f, -62.8f, 3.41f,
                -5.124f, 0.725f, 3.355f, 0.808f, 1.45f, 1f, -121.3f, 0f,
                -5.355f, 2.151f, 3.196f, 0.776f, 1.567f, 1f, -123.49f, 8.52f,
                -5.575f, 4.954f, 3.035f, 0.74f, 4.017f, 1f, -126.07f, 5.29f,
                -5.523f, 0.725f, 3.904f, 0.801f, 1.45f, 1f, -130.78f, 0f,
                -5.731f, 2.154f, 3.658f, 0.763f, 1.566f, 1f, -135.74f, 8.17f,
                -5.943f, 4.956f, 3.467f, 0.721f, 4.016f, 1f, -136.24f, 5.09f,
                -5.973f, 0.725f, 4.282f, 0.796f, 1.45f, 1f, -149.31f, 0f,
                -6.214f, 2.15f, 4.06f, 0.748f, 1.568f, 1f, -143.78f, 8.66f,
                -6.363f, 4.958f, 3.811f, 0.705f, 4.014f, 1f, -147.06f, 4.84f,
                -6.591f, 0.725f, 4.614f, 0.774f, 1.45f, 1f, -154.26f, 0f,
                -6.714f, 2.144f, 4.344f, 0.74f, 1.571f, 1f, -155.46f, 9.3f,
                -6.874f, 4.957f, 4.105f, 0.692f, 4.015f, 1f, -152.89f, 4.9f,
                -7.139f, 0.725f, 4.783f, 0.768f, 1.45f, 1f, -171.39f, 0f,
                -7.242f, 2.146f, 4.522f, 0.731f, 1.57f, 1f, -167.87f, 9.1f,
                -7.309f, 4.958f, 4.261f, 0.688f, 4.014f, 1f, -168.53f, 4.78f,
                -7.807f, 0.725f, 4.854f, 0.748f, 1.45f, 1f, -176.55f, 0f,
                -7.81f, 2.15f, 4.599f, 0.72f, 1.568f, 1f, -178.11f, 8.59f,
                -7.824f, 4.961f, 4.331f, 0.676f, 4.012f, 1f, -178.97f, 4.42f,
                -8.365f, 0.725f, 4.811f, 0.741f, 1.45f, 1f, 167.67f, 0f,
                -8.36f, 2.154f, 4.567f, 0.711f, 1.566f, 1f, 170.41f, 8.19f,
                -8.344f, 4.96f, 4.292f, 0.667f, 4.013f, 1f, 171.63f, 4.58f,
                -8.977f, 0.725f, 4.641f, 0.726f, 1.45f, 1f, 161.02f, 0f,
                -8.857f, 2.155f, 4.423f, 0.702f, 1.565f, 1f, 156.7f, 8.05f,
                -8.765f, 4.959f, 4.168f, 0.66f, 4.013f, 1f, 156.06f, 4.66f,
                -9.528f, 0.725f, 4.4f, 0.723f, 1.45f, 1f, 151.76f, 0f,
                -9.395f, 2.155f, 4.162f, 0.69f, 1.565f, 1f, 151.29f, 7.99f,
                -9.227f, 4.962f, 3.933f, 0.645f, 4.012f, 1f, 147.98f, 4.41f,
                -10.045f, 0.725f, 4.072f, 0.727f, 1.45f, 1f, 143.55f, 0f,
                -9.838f, 2.155f, 3.861f, 0.689f, 1.565f, 1f, 140.73f, 8.05f,
                -9.678f, 4.961f, 3.621f, 0.643f, 4.012f, 1f, 143.11f, 4.48f,
                -10.431f, 0.725f, 3.693f, 0.728f, 1.45f, 1f, 127.52f, 0f,
                -10.217f, 2.155f, 3.487f, 0.69f, 1.565f, 1f, 129.9f, 8.05f,
                -10.04f, 4.956f, 3.276f, 0.652f, 4.015f, 1f, 134.03f, 5.01f,
                -10.753f, 0.725f, 3.189f, 0.725f, 1.45f, 1f, 117.58f, 0f,
                -10.549f, 2.143f, 3.009f, 0.699f, 1.571f, 1f, 123.17f, 9.43f,
                -10.246f, 4.954f, 2.958f, 0.652f, 4.017f, 1f, 114.91f, 5.3f,
                -10.994f, 0.725f, 2.63f, 0.727f, 1.45f, 1f, 109f, 0f,
                -10.733f, 2.136f, 2.581f, 0.701f, 1.575f, 1f, 106.05f, 10.31f,
                -10.421f, 4.955f, 2.499f, 0.646f, 4.016f, 1f, 105.11f, 5.11f,
                -11.106f, 0.725f, 2.087f, 0.728f, 1.45f, 1f, 94.35f, 0f,
                -10.837f, 2.142f, 2.061f, 0.699f, 1.572f, 1f, 94.53f, 9.56f,
                -10.52f, 4.956f, 1.999f, 0.646f, 4.016f, 1f, 97.05f, 5.06f,
                -11.11f, 0.725f, 1.47f, 0.729f, 1.45f, 1f, 86.34f, 0f,
                -10.833f, 2.144f, 1.52f, 0.697f, 1.571f, 1f, 83.98f, 9.32f,
                -10.522f, 4.955f, 1.549f, 0.647f, 4.016f, 1f, 84.17f, 5.17f,
                -11.002f, 0.725f, 0.929f, 0.729f, 1.45f, 1f, 71.2f, 0f,
                -10.729f, 2.142f, 0.969f, 0.7f, 1.572f, 1f, 75.18f, 9.5f,
                -10.428f, 4.954f, 1.071f, 0.648f, 4.017f, 1f, 74.19f, 5.23f,
                -10.762f, 0.725f, 0.367f, 0.727f, 1.45f, 1f, 62.56f, 0f,
                -10.55f, 2.148f, 0.519f, 0.699f, 1.569f, 1f, 59.47f, 8.88f,
                -10.25f, 4.953f, 0.618f, 0.652f, 4.018f, 1f, 63.98f, 5.36f,
                -10.442f, 0.725f, -0.131f, 0.73f, 1.45f, 1f, 51.86f, 0f,
                -10.245f, 2.155f, 0.089f, 0.693f, 1.565f, 1f, 47.61f, 8.04f,
                -10.037f, 4.956f, 0.297f, 0.651f, 4.016f, 1f, 45.53f, 5.04f,
                -10.051f, 0.725f, -0.503f, 0.728f, 1.45f, 1f, 35.52f, 0f,
                -9.823f, 2.153f, -0.326f, 0.693f, 1.566f, 1f, 41.93f, 8.29f,
                -9.665f, 4.961f, -0.057f, 0.641f, 4.013f, 1f, 37.71f, 4.53f,
                -9.522f, 0.725f, -0.838f, 0.723f, 1.45f, 1f, 28.91f, 0f,
                -9.424f, 2.154f, -0.597f, 0.693f, 1.566f, 1f, 26.3f, 8.19f,
                -9.224f, 4.961f, -0.368f, 0.645f, 4.012f, 1f, 32.18f, 4.47f,
                -8.978f, 0.725f, -1.077f, 0.726f, 1.45f, 1f, 18.78f, 0f,
                -8.852f, 2.153f, -0.863f, 0.703f, 1.566f, 1f, 23.72f, 8.26f,
                -8.788f, 4.96f, -0.583f, 0.655f, 4.013f, 1f, 21.6f, 4.62f,
                -8.364f, 0.725f, -1.249f, 0.741f, 1.45f, 1f, 12.52f, 0f,
                -8.364f, 2.149f, -1.001f, 0.712f, 1.568f, 1f, 9.13f, 8.7f,
                -8.331f, 4.961f, -0.712f, 0.663f, 4.012f, 1f, 9.31f, 4.52f,
                -7.809f, 0.725f, -1.292f, 0.747f, 1.45f, 1f, -3.69f, 0f,
                -7.819f, 2.146f, -1.027f, 0.719f, 1.569f, 1f, -2.64f, 9.04f,
                -7.825f, 4.962f, -0.752f, 0.673f, 4.012f, 1f, -1.02f, 4.42f,
                -7.144f, 0.725f, -1.217f, 0.766f, 1.45f, 1f, -9.03f, 0f,
                -7.25f, 2.145f, -0.949f, 0.729f, 1.57f, 1f, -12.51f, 9.2f,
                -7.32f, 4.959f, -0.682f, 0.684f, 4.014f, 1f, -11.92f, 4.75f,
                -6.596f, 0.725f, -1.044f, 0.772f, 1.45f, 1f, -25.94f, 0f,
                -6.712f, 2.143f, -0.769f, 0.739f, 1.571f, 1f, -23.95f, 9.4f,
                -6.862f, 4.958f, -0.523f, 0.69f, 4.014f, 1f, -25.51f, 4.86f,
                -5.975f, 0.725f, -0.711f, 0.796f, 1.45f, 1f, -30.32f, 0f,
                -6.211f, 2.146f, -0.486f, 0.748f, 1.57f, 1f, -35.47f, 9.1f,
                -6.365f, 4.958f, -0.246f, 0.704f, 4.014f, 1f, -32.99f, 4.83f,
                -5.53f, 0.725f, -0.339f, 0.799f, 1.45f, 1f, -49.57f, 0f,
                -5.715f, 2.15f, -0.088f, 0.767f, 1.568f, 1f, -42.97f, 8.63f,
                -5.949f, 4.955f, 0.101f, 0.72f, 4.016f, 1f, -43.8f, 5.19f,
                -5.129f, 0.725f, 0.214f, 0.806f, 1.45f, 1f, -58.46f, 0f,
                -5.374f, 2.146f, 0.343f, 0.775f, 1.569f, 1f, -59.19f, 9.04f,
                -5.584f, 4.952f, 0.535f, 0.739f, 4.018f, 1f, -53.84f, 5.49f,
                -4.974f, 0.751f, 0.22f, 0.262f, 1.452f, 1f, -110.59f, -2.96f,
                -5.263f, 2.145f, 0.482f, 0.183f, 1.57f, 1f, -83.67f, 9.2f,
                -5.391f, 4.969f, 0.154f, 0.132f, 4.008f, 1f, -118.11f, 3.57f,
                // Outer gate passage
                -4.1f, 3.5f, 1.775f, 2.2f, 7f, 2.55f, 0f, 0f,
                // Outer gate wall
                -16.7f, 3.5f, 1.2f, 12.6f, 7f, 1.6f, 0f, 0f,
                // Outer gate wall pier plinth
                -16f, 0.475f, 2.285f, 3.06f, 0.95f, 0.57f, 0f, 0f,
                // Outer gate wall pier
                -16f, 3.975f, 2.2f, 2.64f, 6.05f, 0.4f, 0f, 0f,
                // Outer gate wall pier plinth
                -22.245f, 0.475f, 2.285f, 1.51f, 0.95f, 0.57f, 0f, 0f,
                // Outer gate wall pier
                -22.35f, 3.975f, 2.2f, 1.3f, 6.05f, 0.4f, 0f, 0f,
                // Outer gate tower
                11.094f, 0.725f, 2.099f, 0.727f, 1.45f, 1f, -94.16f, 0f,
                10.829f, 2.14f, 2.044f, 0.699f, 1.573f, 1f, -96.95f, 9.74f,
                10.501f, 4.953f, 2.022f, 0.646f, 4.018f, 1f, -96.05f, 5.36f,
                10.985f, 0.725f, 2.646f, 0.727f, 1.45f, 1f, -108.39f, 0f,
                10.717f, 2.14f, 2.584f, 0.698f, 1.573f, 1f, -106.52f, 9.75f,
                10.408f, 4.953f, 2.501f, 0.646f, 4.018f, 1f, -105.77f, 5.36f,
                10.752f, 0.725f, 3.227f, 0.729f, 1.45f, 1f, -115.39f, 0f,
                10.526f, 2.146f, 3.031f, 0.695f, 1.569f, 1f, -121.77f, 9.04f,
                10.236f, 4.953f, 2.943f, 0.649f, 4.018f, 1f, -116.72f, 5.36f,
                10.45f, 0.725f, 3.701f, 0.731f, 1.45f, 1f, -129.53f, 0f,
                10.207f, 2.155f, 3.488f, 0.689f, 1.566f, 1f, -130.43f, 8.1f,
                10.021f, 4.957f, 3.268f, 0.648f, 4.015f, 1f, -134.6f, 4.94f,
                10.044f, 0.725f, 4.082f, 0.728f, 1.45f, 1f, -144.19f, 0f,
                9.822f, 2.153f, 3.865f, 0.688f, 1.566f, 1f, -140.51f, 8.23f,
                9.647f, 4.962f, 3.627f, 0.638f, 4.012f, 1f, -141.89f, 4.39f,
                9.519f, 0.725f, 4.412f, 0.723f, 1.45f, 1f, -151.49f, 0f,
                9.382f, 2.152f, 4.159f, 0.688f, 1.567f, 1f, -151.45f, 8.45f,
                9.211f, 4.962f, 3.939f, 0.643f, 4.011f, 1f, -147.49f, 4.31f,
                8.961f, 0.725f, 4.665f, 0.729f, 1.45f, 1f, -159.71f, 0f,
                8.836f, 2.149f, 4.425f, 0.702f, 1.568f, 1f, -155.92f, 8.69f,
                8.78f, 4.961f, 4.154f, 0.653f, 4.012f, 1f, -158.41f, 4.43f,
                8.376f, 0.725f, 4.825f, 0.74f, 1.45f, 1f, -169.61f, 0f,
                8.367f, 2.149f, 4.558f, 0.708f, 1.568f, 1f, -171.96f, 8.79f,
                8.317f, 4.962f, 4.287f, 0.662f, 4.012f, 1f, -170.12f, 4.35f,
                7.803f, 0.725f, 4.858f, 0.746f, 1.45f, 1f, 176.21f, 0f,
                7.799f, 2.148f, 4.59f, 0.717f, 1.569f, 1f, 178.62f, 8.85f,
                7.805f, 4.963f, 4.334f, 0.674f, 4.011f, 1f, 179.98f, 4.25f,
                7.141f, 0.725f, 4.782f, 0.764f, 1.45f, 1f, 170.74f, 0f,
                7.238f, 2.146f, 4.52f, 0.728f, 1.57f, 1f, 167.93f, 9.12f,
                7.305f, 4.96f, 4.271f, 0.686f, 4.013f, 1f, 168.33f, 4.56f,
                6.592f, 0.725f, 4.608f, 0.77f, 1.45f, 1f, 154.25f, 0f,
                6.676f, 2.146f, 4.348f, 0.743f, 1.57f, 1f, 158.13f, 9.12f,
                6.854f, 4.959f, 4.113f, 0.692f, 4.013f, 1f, 153.97f, 4.68f,
                5.974f, 0.725f, 4.278f, 0.793f, 1.45f, 1f, 149.59f, 0f,
                6.219f, 2.151f, 4.092f, 0.748f, 1.567f, 1f, 141.73f, 8.51f,
                6.353f, 4.958f, 3.825f, 0.705f, 4.014f, 1f, 147.05f, 4.78f,
                5.524f, 0.725f, 3.904f, 0.798f, 1.45f, 1f, 131.14f, 0f,
                5.713f, 2.154f, 3.662f, 0.765f, 1.566f, 1f, 136.87f, 8.15f,
                5.939f, 4.955f, 3.478f, 0.72f, 4.016f, 1f, 136.02f, 5.17f,
                5.134f, 0.725f, 3.364f, 0.804f, 1.45f, 1f, 120.55f, 0f,
                5.349f, 2.147f, 3.211f, 0.777f, 1.569f, 1f, 123.09f, 8.99f,
                5.565f, 4.953f, 3.04f, 0.741f, 4.018f, 1f, 126.54f, 5.38f,
                4.994f, 0.739f, 3.203f, 0.27f, 1.451f, 1f, 87.17f, -1.58f,
                5.247f, 2.15f, 3.185f, 0.209f, 1.568f, 1f, 87.13f, 8.67f,
                5.422f, 4.966f, 3.334f, 0.143f, 4.01f, 1f, 70.19f, 3.95f,
                4.974f, 0.739f, 0.359f, 0.249f, 1.451f, 1f, 93.22f, -1.58f,
                5.239f, 2.147f, 0.452f, 0.18f, 1.569f, 1f, 86.61f, 8.95f,
                5.301f, 4.973f, 0.085f, 0.133f, 4.006f, 1f, 126.56f, 3.12f,
                5.116f, 0.725f, 0.223f, 0.808f, 1.45f, 1f, 58.36f, 0f,
                5.345f, 2.148f, 0.37f, 0.778f, 1.569f, 1f, 57.09f, 8.9f,
                5.544f, 4.953f, 0.554f, 0.747f, 4.018f, 1f, 52.05f, 5.42f,
                5.519f, 0.725f, -0.33f, 0.8f, 1.45f, 1f, 49.5f, 0f,
                5.714f, 2.153f, -0.094f, 0.766f, 1.566f, 1f, 44.44f, 8.31f,
                5.934f, 4.956f, 0.101f, 0.721f, 4.016f, 1f, 44.09f, 5.08f,
                5.966f, 0.725f, -0.708f, 0.796f, 1.45f, 1f, 30.81f, 0f,
                6.19f, 2.145f, -0.472f, 0.75f, 1.57f, 1f, 34.34f, 9.16f,
                6.353f, 4.958f, -0.247f, 0.706f, 4.014f, 1f, 33.34f, 4.83f,
                6.583f, 0.725f, -1.041f, 0.774f, 1.45f, 1f, 25.77f, 0f,
                6.716f, 2.14f, -0.765f, 0.739f, 1.573f, 1f, 25.13f, 9.74f,
                6.842f, 4.958f, -0.52f, 0.693f, 4.014f, 1f, 24.95f, 4.84f,
                7.134f, 0.725f, -1.215f, 0.768f, 1.45f, 1f, 9.11f, 0f,
                7.228f, 2.146f, -0.942f, 0.731f, 1.57f, 1f, 11.4f, 9.12f,
                7.316f, 4.959f, -0.686f, 0.685f, 4.013f, 1f, 12.61f, 4.7f,
                7.799f, 0.725f, -1.289f, 0.749f, 1.45f, 1f, 3.56f, 0f,
                7.813f, 2.146f, -1.016f, 0.719f, 1.57f, 1f, 2.8f, 9.12f,
                7.804f, 4.963f, -0.755f, 0.676f, 4.011f, 1f, -0.08f, 4.27f,
                8.353f, 0.725f, -1.244f, 0.743f, 1.45f, 1f, -12.94f, 0f,
                8.332f, 2.148f, -0.981f, 0.712f, 1.569f, 1f, -11.2f, 8.83f,
                8.331f, 4.961f, -0.709f, 0.664f, 4.012f, 1f, -8.8f, 4.42f,
                8.973f, 0.725f, -1.069f, 0.727f, 1.45f, 1f, -18.62f, 0f,
                8.854f, 2.154f, -0.833f, 0.699f, 1.566f, 1f, -22.47f, 8.17f,
                8.767f, 4.961f, -0.582f, 0.657f, 4.012f, 1f, -22.96f, 4.52f,
                9.518f, 0.725f, -0.83f, 0.724f, 1.45f, 1f, -28.69f, 0f,
                9.37f, 2.157f, -0.585f, 0.688f, 1.564f, 1f, -29.94f, 7.78f,
                9.219f, 4.963f, -0.356f, 0.644f, 4.011f, 1f, -31.98f, 4.29f,
                10.05f, 0.725f, -0.497f, 0.73f, 1.45f, 1f, -35.35f, 0f,
                9.825f, 2.158f, -0.275f, 0.686f, 1.564f, 1f, -38.97f, 7.66f,
                9.661f, 4.962f, -0.046f, 0.641f, 4.012f, 1f, -37.4f, 4.4f,
                10.443f, 0.725f, -0.124f, 0.732f, 1.45f, 1f, -51.68f, 0f,
                10.205f, 2.158f, 0.098f, 0.687f, 1.564f, 1f, -49.61f, 7.71f,
                10.026f, 4.957f, 0.305f, 0.65f, 4.015f, 1f, -45.89f, 4.98f,
                10.76f, 0.725f, 0.371f, 0.729f, 1.45f, 1f, -63.01f, 0f,
                10.54f, 2.15f, 0.564f, 0.696f, 1.568f, 1f, -56.9f, 8.58f,
                10.241f, 4.955f, 0.627f, 0.651f, 4.017f, 1f, -64.25f, 5.21f,
                11f, 0.725f, 0.938f, 0.73f, 1.45f, 1f, -71.25f, 0f,
                10.722f, 2.142f, 0.993f, 0.699f, 1.572f, 1f, -74.09f, 9.5f,
                10.419f, 4.955f, 1.086f, 0.647f, 4.016f, 1f, -73.9f, 5.16f,
                11.102f, 0.725f, 1.473f, 0.729f, 1.45f, 1f, -87.2f, 0f,
                10.831f, 2.141f, 1.54f, 0.699f, 1.572f, 1f, -83.18f, 9.62f,
                10.505f, 4.954f, 1.558f, 0.646f, 4.017f, 1f, -84.37f, 5.29f,
                // Outer gate passage
                4.1f, 3.5f, 1.775f, 2.2f, 7f, 2.55f, 0f, 0f,
                // Outer gate wall
                16.7f, 3.5f, 1.2f, 12.6f, 7f, 1.6f, 0f, 0f,
                // Outer gate wall pier plinth
                16f, 0.475f, 2.285f, 3.06f, 0.95f, 0.57f, 0f, 0f,
                // Outer gate wall pier
                16f, 3.975f, 2.2f, 2.64f, 6.05f, 0.4f, 0f, 0f,
                // Outer gate wall pier plinth
                22.245f, 0.475f, 2.285f, 1.51f, 0.95f, 0.57f, 0f, 0f,
                // Outer gate wall pier
                22.35f, 3.975f, 2.2f, 1.3f, 6.05f, 0.4f, 0f, 0f,
            };

            public static readonly float[] Final =
            {
                // Final gate tower
                -6.918f, 8.661f, 158.79f, 0.452f, 1.351f, 1f, -132.48f, 1.63f,
                -6.797f, 9.608f, 158.508f, 0.499f, 0.602f, 1f, -166.35f, 4.84f,
                -7.46f, 9.939f, 158.652f, 0.313f, 0.767f, 1f, -113.63f, 38.49f,
                -7.659f, 13.964f, 158.241f, 0.35f, 6.852f, 1f, -133.96f, 1.21f,
                -7.285f, 8.675f, 159.062f, 0.878f, 1.35f, 1f, -150.65f, 0f,
                -7.499f, 9.65f, 158.878f, 0.814f, 0.6f, 1f, -145.47f, 0f,
                -7.623f, 9.907f, 158.722f, 0.803f, 0.824f, 1f, -144.68f, 43.26f,
                -7.91f, 13.956f, 158.33f, 0.706f, 6.855f, 1f, -147.08f, 2.22f,
                -7.96f, 8.675f, 159.379f, 0.847f, 1.35f, 1f, -159.15f, 0f,
                -8.092f, 9.65f, 159.221f, 0.802f, 0.6f, 1f, -154.66f, 0f,
                -8.149f, 9.91f, 159.012f, 0.792f, 0.818f, 1f, -158.13f, 42.84f,
                -8.389f, 13.958f, 158.602f, 0.692f, 6.854f, 1f, -156.84f, 1.97f,
                -8.664f, 8.675f, 159.604f, 0.828f, 1.35f, 1f, -165.6f, 0f,
                -8.669f, 9.65f, 159.396f, 0.795f, 0.6f, 1f, -171.54f, 0f,
                -8.736f, 9.911f, 159.184f, 0.776f, 0.815f, 1f, -169.61f, 42.61f,
                -8.884f, 13.958f, 158.763f, 0.68f, 6.854f, 1f, -168.21f, 1.91f,
                -9.321f, 8.675f, 159.693f, 0.82f, 1.35f, 1f, -178.91f, 0f,
                -9.331f, 9.65f, 159.444f, 0.772f, 0.6f, 1f, 179.56f, 0f,
                -9.352f, 9.909f, 159.244f, 0.76f, 0.82f, 1f, -179.06f, 42.95f,
                -9.375f, 13.958f, 158.81f, 0.669f, 6.854f, 1f, 178.95f, 1.91f,
                -9.976f, 8.675f, 159.633f, 0.808f, 1.35f, 1f, 168.33f, 0f,
                -9.978f, 9.65f, 159.391f, 0.758f, 0.6f, 1f, 171.01f, 0f,
                -9.931f, 9.909f, 159.195f, 0.748f, 0.821f, 1f, 169.45f, 43.07f,
                -9.867f, 13.958f, 158.749f, 0.655f, 6.854f, 1f, 167.37f, 1.96f,
                -10.646f, 8.675f, 159.45f, 0.793f, 1.35f, 1f, 160.98f, 0f,
                -10.573f, 9.65f, 159.238f, 0.749f, 0.6f, 1f, 160.06f, 0f,
                -10.528f, 9.903f, 159.04f, 0.738f, 0.833f, 1f, 161.94f, 43.92f,
                -10.343f, 13.956f, 158.589f, 0.64f, 6.855f, 1f, 157.81f, 2.13f,
                -11.304f, 8.675f, 159.188f, 0.796f, 1.35f, 1f, 155.64f, 0f,
                -11.136f, 9.65f, 158.978f, 0.743f, 0.6f, 1f, 150.4f, 0f,
                -11.027f, 9.895f, 158.809f, 0.734f, 0.852f, 1f, 149.39f, 45.26f,
                -10.765f, 13.954f, 158.358f, 0.63f, 6.856f, 1f, 147.74f, 2.35f,
                -11.8f, 8.675f, 158.863f, 0.797f, 1.35f, 1f, 137.93f, 0f,
                -11.633f, 9.65f, 158.632f, 0.74f, 0.6f, 1f, 139.77f, 0f,
                -11.519f, 9.889f, 158.464f, 0.732f, 0.867f, 1f, 141.49f, 46.19f,
                -11.145f, 13.953f, 158.059f, 0.622f, 6.857f, 1f, 138.84f, 2.57f,
                -12.281f, 8.675f, 158.369f, 0.794f, 1.35f, 1f, 130.49f, 0f,
                -12.055f, 9.65f, 158.201f, 0.739f, 0.6f, 1f, 129.09f, 0f,
                -11.922f, 9.883f, 158.069f, 0.734f, 0.884f, 1f, 130.56f, 47.26f,
                -11.413f, 13.951f, 157.743f, 0.618f, 6.858f, 1f, 124.72f, 2.75f,
                -12.688f, 8.675f, 157.817f, 0.803f, 1.35f, 1f, 122.38f, 0f,
                -12.412f, 9.65f, 157.688f, 0.742f, 0.6f, 1f, 120.58f, 0f,
                -12.25f, 9.874f, 157.595f, 0.739f, 0.911f, 1f, 120.44f, 48.79f,
                -11.648f, 13.95f, 157.332f, 0.61f, 6.859f, 1f, 116.27f, 2.87f,
                -12.923f, 8.675f, 157.27f, 0.804f, 1.35f, 1f, 104.07f, 0f,
                -12.636f, 9.65f, 157.165f, 0.743f, 0.6f, 1f, 105.92f, 0f,
                -12.46f, 9.87f, 157.104f, 0.741f, 0.922f, 1f, 106.73f, 49.42f,
                -11.806f, 13.949f, 156.913f, 0.607f, 6.859f, 1f, 106.22f, 2.93f,
                -13.038f, 8.675f, 156.597f, 0.799f, 1.35f, 1f, 95.38f, 0f,
                -12.766f, 9.65f, 156.547f, 0.745f, 0.6f, 1f, 97.79f, 0f,
                -12.585f, 9.869f, 156.524f, 0.744f, 0.925f, 1f, 97.7f, 49.56f,
                -11.892f, 13.949f, 156.476f, 0.607f, 6.859f, 1f, 96.05f, 2.95f,
                -13.04f, 8.675f, 155.932f, 0.799f, 1.35f, 1f, 84.87f, 0f,
                -12.767f, 9.65f, 155.981f, 0.745f, 0.6f, 1f, 82.5f, 0f,
                -12.585f, 9.868f, 156.002f, 0.744f, 0.93f, 1f, 82.74f, 49.84f,
                -11.89f, 13.949f, 156.06f, 0.606f, 6.859f, 1f, 83.53f, 2.94f,
                -12.924f, 8.675f, 155.265f, 0.804f, 1.35f, 1f, 75.49f, 0f,
                -12.634f, 9.65f, 155.386f, 0.742f, 0.6f, 1f, 72.14f, 0f,
                -12.463f, 9.87f, 155.438f, 0.741f, 0.924f, 1f, 72.34f, 49.51f,
                -11.802f, 13.949f, 155.611f, 0.607f, 6.859f, 1f, 74.46f, 2.96f,
                -12.687f, 8.675f, 154.704f, 0.803f, 1.35f, 1f, 58.61f, 0f,
                -12.396f, 9.65f, 154.825f, 0.74f, 0.6f, 1f, 61.92f, 0f,
                -12.243f, 9.873f, 154.931f, 0.738f, 0.912f, 1f, 60.15f, 48.86f,
                -11.65f, 13.95f, 155.214f, 0.609f, 6.859f, 1f, 62.59f, 2.89f,
                -12.289f, 8.675f, 154.156f, 0.796f, 1.35f, 1f, 49.42f, 0f,
                -12.066f, 9.65f, 154.347f, 0.739f, 0.6f, 1f, 48.79f, 0f,
                -11.927f, 9.882f, 154.479f, 0.734f, 0.886f, 1f, 48.08f, 47.36f,
                -11.409f, 13.951f, 154.798f, 0.615f, 6.858f, 1f, 54.87f, 2.72f,
                -11.808f, 8.675f, 153.668f, 0.798f, 1.35f, 1f, 41.47f, 0f,
                -11.622f, 9.65f, 153.909f, 0.737f, 0.6f, 1f, 40.49f, 0f,
                -11.509f, 9.89f, 154.076f, 0.729f, 0.865f, 1f, 38.67f, 46.07f,
                -11.133f, 13.953f, 154.47f, 0.621f, 6.857f, 1f, 42.07f, 2.53f,
                -11.301f, 8.675f, 153.335f, 0.797f, 1.35f, 1f, 25f, 0f,
                -11.127f, 9.65f, 153.559f, 0.741f, 0.6f, 1f, 30.08f, 0f,
                -11.026f, 9.894f, 153.743f, 0.731f, 0.855f, 1f, 29.85f, 45.42f,
                -10.769f, 13.955f, 154.184f, 0.628f, 6.856f, 1f, 31.51f, 2.33f,
                -10.655f, 8.675f, 153.079f, 0.794f, 1.35f, 1f, 18.2f, 0f,
                -10.548f, 9.65f, 153.269f, 0.756f, 0.6f, 1f, 23.24f, 0f,
                -10.492f, 9.896f, 153.489f, 0.739f, 0.849f, 1f, 20.97f, 45.03f,
                -10.339f, 13.956f, 153.952f, 0.638f, 6.855f, 1f, 22.28f, 2.13f,
                -9.974f, 8.675f, 152.896f, 0.808f, 1.35f, 1f, 11.95f, 0f,
                -9.979f, 9.65f, 153.104f, 0.765f, 0.6f, 1f, 9.17f, 0f,
                -9.948f, 9.9f, 153.341f, 0.747f, 0.841f, 1f, 9.05f, 44.47f,
                -9.871f, 13.958f, 153.799f, 0.652f, 6.854f, 1f, 12.07f, 1.97f,
                -9.332f, 8.675f, 152.842f, 0.818f, 1.35f, 1f, -2.19f, 0f,
                -9.363f, 9.65f, 153.072f, 0.772f, 0.6f, 1f, -3.1f, 0f,
                -9.331f, 9.906f, 153.284f, 0.763f, 0.828f, 1f, 0.79f, 43.55f,
                -9.366f, 13.958f, 153.735f, 0.668f, 6.854f, 1f, 2.02f, 1.94f,
                -8.65f, 8.675f, 152.932f, 0.829f, 1.35f, 1f, -12.83f, 0f,
                -8.708f, 9.65f, 153.157f, 0.784f, 0.6f, 1f, -11.74f, 0f,
                -8.764f, 9.909f, 153.343f, 0.773f, 0.821f, 1f, -12.81f, 43.02f,
                -8.892f, 13.958f, 153.779f, 0.678f, 6.854f, 1f, -12.21f, 1.96f,
                -7.967f, 8.675f, 153.146f, 0.846f, 1.35f, 1f, -21.8f, 0f,
                -8.087f, 9.65f, 153.351f, 0.798f, 0.6f, 1f, -22.8f, 0f,
                -8.161f, 9.91f, 153.533f, 0.788f, 0.819f, 1f, -22.18f, 42.9f,
                -8.396f, 13.958f, 153.939f, 0.689f, 6.854f, 1f, -23.34f, 1.97f,
                -7.294f, 8.675f, 153.473f, 0.875f, 1.35f, 1f, -29.89f, 0f,
                -7.506f, 9.65f, 153.667f, 0.811f, 0.6f, 1f, -34.2f, 0f,
                -7.617f, 9.905f, 153.827f, 0.802f, 0.829f, 1f, -33.96f, 43.62f,
                -7.959f, 13.957f, 154.197f, 0.696f, 6.854f, 1f, -36.35f, 2.06f,
                -6.777f, 8.675f, 153.894f, 0.881f, 1.35f, 1f, -48.3f, 0f,
                -7.003f, 9.65f, 154.098f, 0.822f, 0.6f, 1f, -46.78f, 0f,
                -7.136f, 9.901f, 154.234f, 0.815f, 0.839f, 1f, -45.89f, 44.32f,
                -7.545f, 13.956f, 154.569f, 0.707f, 6.855f, 1f, -45.74f, 2.2f,
                -6.292f, 8.675f, 154.512f, 0.898f, 1.35f, 1f, -55.41f, 0f,
                -6.565f, 9.65f, 154.65f, 0.837f, 0.6f, 1f, -56.2f, 0f,
                -6.723f, 9.896f, 154.751f, 0.83f, 0.849f, 1f, -56.21f, 45.06f,
                -7.248f, 13.953f, 154.987f, 0.713f, 6.857f, 1f, -59.49f, 2.5f,
                -5.868f, 8.672f, 155.108f, 0.592f, 1.35f, 1f, -52.48f, 0.34f,
                -6.207f, 9.63f, 155.176f, 0.443f, 0.6f, 1f, -52.05f, 2.26f,
                -6.529f, 9.899f, 155.071f, 0.36f, 0.843f, 1f, -71.87f, 44.62f,
                -7.182f, 13.951f, 154.969f, 0.121f, 6.858f, 1f, -91.26f, 2.74f,
                13.056f, 8.675f, 156.608f, 0.801f, 1.35f, 1f, -94.7f, 0f,
                12.771f, 9.65f, 156.548f, 0.744f, 0.6f, 1f, -98.11f, 0f,
                12.586f, 9.87f, 156.537f, 0.742f, 0.923f, 1f, -96.89f, 49.43f,
                11.889f, 13.949f, 156.486f, 0.605f, 6.86f, 1f, -95.47f, 3.04f,
                12.937f, 8.675f, 157.256f, 0.803f, 1.35f, 1f, -106.07f, 0f,
                12.632f, 9.65f, 157.143f, 0.739f, 0.6f, 1f, -108.12f, 0f,
                12.465f, 9.872f, 157.085f, 0.738f, 0.918f, 1f, -108.38f, 49.18f,
                11.805f, 13.948f, 156.907f, 0.605f, 6.86f, 1f, -106.72f, 3.05f,
                12.686f, 8.675f, 157.808f, 0.8f, 1.35f, 1f, -122.98f, 0f,
                12.393f, 9.65f, 157.698f, 0.737f, 0.6f, 1f, -118.58f, 0f,
                12.238f, 9.874f, 157.609f, 0.735f, 0.909f, 1f, -118.91f, 48.69f,
                11.646f, 13.949f, 157.321f, 0.607f, 6.859f, 1f, -116.85f, 2.96f,
                12.275f, 8.675f, 158.363f, 0.79f, 1.35f, 1f, -130.16f, 0f,
                12.057f, 9.65f, 158.199f, 0.737f, 0.6f, 1f, -128.95f, 0f,
                11.926f, 9.88f, 158.07f, 0.733f, 0.892f, 1f, -130.41f, 47.72f,
                11.41f, 13.951f, 157.734f, 0.614f, 6.858f, 1f, -124.86f, 2.79f,
                11.799f, 8.675f, 158.857f, 0.794f, 1.35f, 1f, -137.59f, 0f,
                11.641f, 9.65f, 158.619f, 0.737f, 0.6f, 1f, -140.62f, 0f,
                11.526f, 9.889f, 158.469f, 0.731f, 0.868f, 1f, -141.2f, 46.28f,
                11.146f, 13.953f, 158.049f, 0.619f, 6.857f, 1f, -138.88f, 2.57f,
                11.303f, 8.675f, 159.19f, 0.794f, 1.35f, 1f, -154.64f, 0f,
                11.134f, 9.65f, 158.971f, 0.74f, 0.6f, 1f, -149.81f, 0f,
                11.047f, 9.896f, 158.8f, 0.732f, 0.849f, 1f, -150.56f, 45.02f,
                10.766f, 13.955f, 158.354f, 0.627f, 6.856f, 1f, -147.24f, 2.31f,
                10.659f, 8.675f, 159.452f, 0.793f, 1.35f, 1f, -161.13f, 0f,
                10.564f, 9.65f, 159.251f, 0.752f, 0.6f, 1f, -157.71f, 0f,
                10.519f, 9.902f, 159.046f, 0.738f, 0.836f, 1f, -160.23f, 44.16f,
                10.346f, 13.957f, 158.589f, 0.639f, 6.854f, 1f, -157.25f, 2.07f,
                9.994f, 8.675f, 159.628f, 0.805f, 1.35f, 1f, -169.15f, 0f,
                9.982f, 9.65f, 159.419f, 0.763f, 0.6f, 1f, -169.99f, 0f,
                9.953f, 9.908f, 159.198f, 0.747f, 0.822f, 1f, -170.29f, 43.11f,
                9.882f, 13.958f, 158.745f, 0.653f, 6.854f, 1f, -167.75f, 1.92f,
                9.329f, 8.675f, 159.687f, 0.818f, 1.35f, 1f, 179.38f, 0f,
                9.354f, 9.65f, 159.469f, 0.775f, 0.6f, 1f, 179.08f, 0f,
                9.368f, 9.914f, 159.245f, 0.758f, 0.81f, 1f, 178.78f, 42.24f,
                9.379f, 13.959f, 158.81f, 0.669f, 6.854f, 1f, -178.17f, 1.85f,
                8.668f, 8.675f, 159.605f, 0.828f, 1.35f, 1f, 166.54f, 0f,
                8.701f, 9.65f, 159.403f, 0.791f, 0.6f, 1f, 169.47f, 0f,
                8.75f, 9.915f, 159.182f, 0.774f, 0.809f, 1f, 169.59f, 42.11f,
                8.896f, 13.959f, 158.769f, 0.68f, 6.853f, 1f, 168.14f, 1.82f,
                7.962f, 8.675f, 159.39f, 0.85f, 1.35f, 1f, 159.72f, 0f,
                8.1f, 9.65f, 159.213f, 0.801f, 0.6f, 1f, 155.63f, 0f,
                8.165f, 9.909f, 159.008f, 0.79f, 0.82f, 1f, 158.1f, 42.98f,
                8.396f, 13.959f, 158.608f, 0.691f, 6.854f, 1f, 157.16f, 1.88f,
                7.288f, 8.675f, 159.077f, 0.88f, 1.35f, 1f, 150.65f, 0f,
                7.495f, 9.65f, 158.88f, 0.817f, 0.6f, 1f, 146.83f, 0f,
                7.619f, 9.901f, 158.714f, 0.805f, 0.837f, 1f, 146.34f, 44.19f,
                7.923f, 13.956f, 158.339f, 0.706f, 6.855f, 1f, 146.85f, 2.13f,
                6.922f, 8.664f, 158.808f, 0.464f, 1.35f, 1f, 131.37f, 1.23f,
                6.834f, 9.627f, 158.53f, 0.489f, 0.601f, 1f, 162.78f, 2.6f,
                7.441f, 9.928f, 158.627f, 0.326f, 0.785f, 1f, 116.99f, 40.13f,
                7.636f, 13.965f, 158.216f, 0.355f, 6.851f, 1f, 137.89f, 1.17f,
                5.867f, 8.672f, 155.122f, 0.597f, 1.35f, 1f, 50.84f, 0.33f,
                6.224f, 9.637f, 155.168f, 0.433f, 0.6f, 1f, 53.09f, 1.53f,
                6.53f, 9.9f, 155.04f, 0.362f, 0.84f, 1f, 74.41f, 44.41f,
                7.177f, 13.951f, 154.938f, 0.125f, 6.858f, 1f, 93.89f, 2.7f,
                6.306f, 8.675f, 154.514f, 0.898f, 1.35f, 1f, 55.82f, 0f,
                6.573f, 9.65f, 154.65f, 0.838f, 0.6f, 1f, 56.42f, 0f,
                6.71f, 9.896f, 154.766f, 0.836f, 0.849f, 1f, 54.48f, 45.06f,
                7.243f, 13.953f, 154.996f, 0.716f, 6.856f, 1f, 58.48f, 2.47f,
                6.787f, 8.675f, 153.889f, 0.882f, 1.35f, 1f, 48.98f, 0f,
                7.01f, 9.65f, 154.093f, 0.823f, 0.6f, 1f, 47.22f, 0f,
                7.149f, 9.899f, 154.228f, 0.816f, 0.843f, 1f, 46.61f, 44.65f,
                7.557f, 13.956f, 154.553f, 0.708f, 6.855f, 1f, 47.15f, 2.18f,
                7.298f, 8.675f, 153.469f, 0.877f, 1.35f, 1f, 29.86f, 0f,
                7.508f, 9.65f, 153.668f, 0.814f, 0.6f, 1f, 33.67f, 0f,
                7.62f, 9.901f, 153.827f, 0.805f, 0.837f, 1f, 33.71f, 44.21f,
                7.951f, 13.957f, 154.194f, 0.7f, 6.854f, 1f, 35.44f, 2.02f,
                7.969f, 8.675f, 153.148f, 0.848f, 1.35f, 1f, 21.05f, 0f,
                8.095f, 9.65f, 153.354f, 0.798f, 0.6f, 1f, 22.52f, 0f,
                8.159f, 9.91f, 153.542f, 0.79f, 0.819f, 1f, 21f, 42.92f,
                8.402f, 13.958f, 153.931f, 0.691f, 6.854f, 1f, 23.38f, 1.9f,
                8.681f, 8.675f, 152.912f, 0.831f, 1.35f, 1f, 15.56f, 0f,
                8.713f, 9.65f, 153.168f, 0.784f, 0.6f, 1f, 10.89f, 0f,
                8.768f, 9.914f, 153.359f, 0.772f, 0.811f, 1f, 11.86f, 42.28f,
                8.896f, 13.959f, 153.773f, 0.68f, 6.854f, 1f, 11.77f, 1.88f,
                9.32f, 8.675f, 152.824f, 0.825f, 1.35f, 1f, -0.01f, 0f,
                9.347f, 9.65f, 153.108f, 0.769f, 0.6f, 1f, -0.22f, 0f,
                9.347f, 9.914f, 153.301f, 0.759f, 0.811f, 1f, -0.59f, 42.25f,
                9.375f, 13.959f, 153.731f, 0.669f, 6.854f, 1f, -2.18f, 1.87f,
                9.98f, 8.675f, 152.896f, 0.81f, 1.35f, 1f, -12.53f, 0f,
                9.985f, 9.65f, 153.16f, 0.755f, 0.6f, 1f, -9.3f, 0f,
                9.955f, 9.907f, 153.358f, 0.744f, 0.824f, 1f, -9.37f, 43.24f,
                9.879f, 13.958f, 153.797f, 0.652f, 6.854f, 1f, -12.55f, 1.9f,
                10.664f, 8.675f, 153.085f, 0.794f, 1.35f, 1f, -18.4f, 0f,
                10.572f, 9.65f, 153.317f, 0.746f, 0.6f, 1f, -20.63f, 0f,
                10.5f, 9.9f, 153.509f, 0.735f, 0.839f, 1f, -20.89f, 44.36f,
                10.347f, 13.957f, 153.955f, 0.638f, 6.854f, 1f, -22.63f, 2.05f,
                11.314f, 8.675f, 153.342f, 0.797f, 1.35f, 1f, -24.78f, 0f,
                11.128f, 9.65f, 153.581f, 0.737f, 0.6f, 1f, -30.27f, 0f,
                11.036f, 9.895f, 153.764f, 0.727f, 0.851f, 1f, -29.33f, 45.16f,
                10.767f, 13.955f, 154.189f, 0.626f, 6.855f, 1f, -32.57f, 2.28f,
                11.818f, 8.675f, 153.672f, 0.798f, 1.35f, 1f, -41.61f, 0f,
                11.615f, 9.65f, 153.93f, 0.733f, 0.6f, 1f, -40.91f, 0f,
                11.51f, 9.889f, 154.09f, 0.726f, 0.866f, 1f, -38.86f, 46.18f,
                11.131f, 13.954f, 154.481f, 0.618f, 6.856f, 1f, -42.68f, 2.44f,
                12.301f, 8.675f, 154.163f, 0.796f, 1.35f, 1f, -49.23f, 0f,
                12.038f, 9.65f, 154.363f, 0.731f, 0.6f, 1f, -50.48f, 0f,
                11.935f, 9.879f, 154.5f, 0.732f, 0.896f, 1f, -47.24f, 47.98f,
                11.42f, 13.952f, 154.82f, 0.611f, 6.857f, 1f, -53.77f, 2.61f,
                12.698f, 8.675f, 154.708f, 0.803f, 1.35f, 1f, -58.66f, 0f,
                12.379f, 9.65f, 154.859f, 0.733f, 0.6f, 1f, -60.59f, 0f,
                12.232f, 9.871f, 154.927f, 0.735f, 0.921f, 1f, -61.64f, 49.36f,
                11.652f, 13.95f, 155.222f, 0.606f, 6.859f, 1f, -62.68f, 2.87f,
                12.937f, 8.675f, 155.27f, 0.804f, 1.35f, 1f, -75.35f, 0f,
                12.627f, 9.65f, 155.409f, 0.738f, 0.6f, 1f, -70.75f, 0f,
                12.459f, 9.87f, 155.455f, 0.737f, 0.923f, 1f, -71.74f, 49.45f,
                11.806f, 13.949f, 155.626f, 0.605f, 6.859f, 1f, -73.8f, 2.99f,
                13.054f, 8.675f, 155.937f, 0.8f, 1.35f, 1f, -84.81f, 0f,
                12.77f, 9.65f, 155.994f, 0.744f, 0.6f, 1f, -81.74f, 0f,
                12.583f, 9.87f, 156.008f, 0.741f, 0.924f, 1f, -82.71f, 49.53f,
                11.889f, 13.949f, 156.059f, 0.604f, 6.859f, 1f, -84.1f, 3f,
                // Final gate passage
                -5f, 13.5f, 157.245f, 2f, 11f, 3.95f, 0f, 0f,
                // Final gate passage (face)
                -5.65f, 13.5f, 155.315f, 2.7f, 11f, 0.57f, 0f, 0f,
                // Final gate passage (chamfer)
                -4.302f, 13.5f, 155.353f, 0.378f, 11f, 0.5f, -37.48f, 0f,
                -4.308f, 13.5f, 159.17f, 0.375f, 11f, 0.5f, -136.08f, 0f,
                // Final gate passage (far face)
                -5.083f, 13.5f, 158.844f, 1.679f, 11f, 1f, 169.36f, 0f,
                -6.392f, 13.5f, 158.435f, 1.355f, 11f, 1f, 154.19f, 0f,
                // Final gate wall
                -19f, 13.5f, 156.2f, 14f, 11f, 1.6f, 0f, 0f,
                // Final gate wall pier plinth
                -18f, 8.75f, 155.205f, 3.52f, 1.5f, 0.39f, 0f, 0f,
                // Final gate wall pier
                -18f, 14.25f, 155.26f, 3.04f, 9.5f, 0.28f, 0f, 0f,
                // Final gate wall pier plinth
                -25.15f, 8.75f, 155.205f, 1.7f, 1.5f, 0.39f, 0f, 0f,
                // Final gate wall pier
                -25.27f, 14.25f, 155.26f, 1.46f, 9.5f, 0.28f, 0f, 0f,
                // Final gate passage
                5f, 13.5f, 157.245f, 2f, 11f, 3.95f, 0f, 0f,
                // Final gate passage (face)
                5.65f, 13.5f, 155.315f, 2.7f, 11f, 0.57f, 0f, 0f,
                // Final gate passage (chamfer)
                4.302f, 13.5f, 155.353f, 0.378f, 11f, 0.5f, 37.48f, 0f,
                4.308f, 13.5f, 159.17f, 0.375f, 11f, 0.5f, 136.08f, 0f,
                // Final gate passage (far face)
                5.083f, 13.5f, 158.844f, 1.679f, 11f, 1f, -169.36f, 0f,
                6.392f, 13.5f, 158.435f, 1.355f, 11f, 1f, -154.19f, 0f,
                // Final gate wall
                19f, 13.5f, 156.2f, 14f, 11f, 1.6f, 0f, 0f,
                // Final gate wall pier plinth
                18f, 8.75f, 155.205f, 3.52f, 1.5f, 0.39f, 0f, 0f,
                // Final gate wall pier
                18f, 14.25f, 155.26f, 3.04f, 9.5f, 0.28f, 0f, 0f,
                // Final gate wall pier plinth
                25.15f, 8.75f, 155.205f, 1.7f, 1.5f, 0.39f, 0f, 0f,
                // Final gate wall pier
                25.27f, 14.25f, 155.26f, 1.46f, 9.5f, 0.28f, 0f, 0f,
            };

            public static readonly float[] Section =
            {
                // Section end pier
                -9.216f, 13.5f, 173.35f, 6.431f, 13f, 4.1f, 0f, 0f,
                9.216f, 13.5f, 173.35f, 6.431f, 13f, 4.1f, 0f, 0f,
            };
        }
        // </castle boxes>

        static readonly Dictionary<string, Material> castlePaints = new Dictionary<string, Material>();

        // In Dress, after the zones: each castle model in its textures, the pieces it draws for dark, the boxes added.
        void DressCastle()
        {
            Transform boxes = null;
            int hidden = 0, added = 0, shown = 0;
            foreach (var castle in CastleModels)
            {
                if (castle.sectionOnly && !asSection) continue;
                var model = Model(castle.model);
                if (model == null) continue;
                foreach (var cover in castle.covers)
                    foreach (var piece in Covered(cover))
                        foreach (var r in piece.GetComponentsInChildren<Renderer>(true))
                            if (r.enabled && r.GetComponent<TextMesh>() == null)
                            {
                                r.enabled = false;
                                hidden++;
                            }
                foreach (var cover in castle.uncollide)
                    foreach (var piece in Covered(cover))
                        foreach (var c in piece.GetComponents<Collider>())
                            c.enabled = false;
                foreach (var move in castle.moves)
                    foreach (var piece in new List<Transform>(Covered(move.what)))
                    {
                        var p = root.InverseTransformPoint(piece.position);
                        if (move.absX.HasValue) p.x = Mathf.Sign(p.x) * move.absX.Value;
                        if (move.x.HasValue) p.x = move.x.Value;
                        if (move.y.HasValue) p.y = move.y.Value;
                        if (move.z.HasValue) p.z = move.z.Value;
                        piece.position = root.TransformPoint(p);
                        if (move.textSize > 0f && piece.TryGetComponent<TextMesh>(out var text)) text.characterSize = move.textSize;
                    }
                castleLabelGroups.AddRange(castle.labels);
                if (boxes == null && castle.boxes.Length > 0) boxes = Group("Castle colliders");
                var b = castle.boxes;
                for (int i = 0; i + 7 < b.Length; i += 8)
                {
                    var box = YardKit.Box(boxes, castle.model + " (collider)", new Vector3(b[i], b[i + 1], b[i + 2]),
                                          new Vector3(b[i + 3], b[i + 4], b[i + 5]), null, YardKit.Solid.NoClimb, Quaternion.Euler(b[i + 7], b[i + 6], 0f));
                    box.GetComponent<MeshRenderer>().enabled = false;
                    added++;
                }
                var pivot = Hang(model, root, castle.model);
                var source = model.GetComponentsInChildren<Renderer>(true);
                var views = pivot.GetComponentsInChildren<Renderer>(true);
                for (int k = 0; k < views.Length && k < source.Length; k++)
                {
                    var from = source[k].sharedMaterials;
                    var slots = views[k].sharedMaterials;
                    for (int i = 0; i < slots.Length && i < from.Length; i++)
                        slots[i] = CastlePaint(from[i] != null ? from[i].name : "");
                    views[k].sharedMaterials = slots;
                }
                shown++;
            }
            if (shown > 0) Debug.Log("[RoyalCarriageYard] 성 모델 " + shown + "개 · 블록아웃 " + hidden + "개 그림 끔 · 충돌 상자 " + added + "개 더함");
        }

        // The groups whose labels stand by a castle model (CastleModel.labels), for CastleLabels.
        readonly List<string> castleLabelGroups = new List<string>();

        // In Awake once the map is switched on: every label in those groups in the depth-tested text material, the ones
        // the obstacles made in their own Awake just then among them (the push gate's "우클릭을 누른 채로 밀기" over the
        // final gate, which read mirrored from the finish court through the gatehouse).
        void CastleLabels()
        {
            foreach (var group in castleLabelGroups)
            {
                var g = root.Find(group);
                if (g == null) continue;
                foreach (var text in g.GetComponentsInChildren<TextMesh>(true))
                {
                    var m = WorldText(text.font);
                    if (m != null) text.GetComponent<MeshRenderer>().sharedMaterial = m;
                }
            }
        }

        IEnumerable<Transform> Covered(Cover cover)
        {
            var group = root.Find(cover.group);
            if (group == null) yield break;
            foreach (Transform child in group)
            {
                if (child.name != cover.name) continue;
                var p = child.localPosition;
                if (cover.absX.HasValue && (Mathf.Abs(p.x) < cover.absX.Value.x || Mathf.Abs(p.x) > cover.absX.Value.y)) continue;
                if (cover.z.HasValue && (p.z < cover.z.Value.x || p.z > cover.z.Value.y)) continue;
                yield return child;
            }
        }

        // A slot's textured material: Resources/PawnRushCarriageYard/<slot> (base colour) and <slot>_N (normal map); the
        // clay when the texture is missing.
        Material CastlePaint(string slot)
        {
            if (castlePaints.TryGetValue(slot, out var m) && m != null) return m;
            var albedo = Resources.Load<Texture2D>(ModelFolder + slot);
            if (albedo == null)
            {
                Debug.LogWarning("[RoyalCarriageYard] 성 텍스처가 없습니다: Resources/" + ModelFolder + slot + ".png");
                return clay;
            }
            m = ChessFight.Game.LitMaterials.NewLit(Color.white, .18f);
            m.name = "M_" + slot;
            m.mainTexture = albedo;
            var normal = Resources.Load<Texture2D>(ModelFolder + slot + "_N");
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            return castlePaints[slot] = m;
        }

        static readonly Dictionary<Font, Material> worldTexts = new Dictionary<Font, Material>();
        static bool worldTextHooked;

        // The labels' font material (GUI/Text Shader) draws over everything and from both sides: through a gatehouse
        // the outer gate's title read mirrored from the yard (R679). The course-text shader the King Rush opening made
        // for this (Resources/KingRushCourseText: the font's alpha in the label's colour, depth tested) hides them
        // behind the model like any stone. Its atlas follows the font's when the font grows it; null (the labels keep
        // the font's material) if the shader is missing.
        static Material WorldText(Font font)
        {
            if (font == null) return null;
            if (worldTexts.TryGetValue(font, out var m) && m != null) return m;
            var shader = Resources.Load<Shader>("KingRushCourseText");
            if (shader == null) return null;
            m = new Material(shader) { name = "M_WorldText " + font.name, mainTexture = font.material.mainTexture };
            if (!worldTextHooked)
            {
                Font.textureRebuilt += WorldTextRebuilt;
                worldTextHooked = true;
            }
            return worldTexts[font] = m;
        }

        static void WorldTextRebuilt(Font font)
        {
            if (font != null && worldTexts.TryGetValue(font, out var m) && m != null) m.mainTexture = font.material.mainTexture;
        }
    }
}
