using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // How a figure stands this frame, relative to its spot. Angles in radians.
    // Dx is toward screen right and Dz toward the camera, as on the design samples.
    public struct FigurePose
    {
        public float Jump, Yaw, Tip, Pitch, Flip, Lean, Sit, Squash, Arm, Leg, Dx, Dz;
        public float? ArmLeft, ArmRight;
    }

    // A chess piece in the PAWN RUSH look (the reference picture of 2026-10-01): short
    // dark legs and shoes, round mittens, a smooth body - cream for white, charcoal for
    // black - with the team crown on the chest, and the team pawn's big glossy eyes.
    // Display only: no colliders, no physics. The pivot is on the ground between the
    // feet and the figure faces +Z until it is placed.
    public sealed class LastSceneFigure
    {
        public readonly GameObject Root;
        public readonly PieceKind Kind;
        public readonly int Team;
        // The king's crown, a part of its own so it can fly off on defeat.
        public readonly GameObject Crown;
        readonly Transform rig;
        readonly Transform[] arms, legs;
        Vector3 spot;
        float spotYaw;

        static readonly int[] BodyColor = { 0xF3E9D8, 0x3A3843 }, TrimColor = { 0xE2D4BC, 0x2B2932 };
        static readonly int[] EmblemColor = { 0x2F6FE0, 0xF2C14E }, BlushColor = { 0xF2A3A3, 0x8C5B66 };
        const int Shoe = 0x2A2730, Gold = 0xF2C14E, Eye = 0x17141C;

        static readonly Vector2[] Skirt = { V(0, .38f), V(.5f, .38f), V(.55f, .45f), V(.52f, .55f), V(.45f, .63f), V(.4f, .74f) };
        static readonly Vector2[] RookBody = { V(.41f, .82f), V(.42f, 1.24f), V(.47f, 1.28f), V(.53f, 1.36f), V(.53f, 1.5f), V(.4f, 1.5f), V(.38f, 1.43f), V(0, 1.43f) };
        static readonly Vector2[] BishopBody = { V(.35f, .84f), V(.3f, .98f), V(0, .98f) };
        static readonly Vector2[] PawnBody = { V(.35f, .84f), V(.3f, .98f), V(.27f, 1.06f), V(0, 1.06f) };
        static readonly Vector2[] Mitre = { V(0, 1.04f), V(.3f, 1.06f), V(.37f, 1.22f), V(.38f, 1.4f), V(.33f, 1.58f), V(.22f, 1.75f), V(.08f, 1.86f), V(0, 1.88f) };
        static readonly Vector2[] Horse = { V(-.28f, 0), V(-.31f, .34f), V(-.27f, .6f), V(-.17f, .8f), V(-.11f, .96f), V(-.05f, 1.08f), V(.02f, .92f), V(.14f, .86f), V(.4f, .6f), V(.5f, .47f), V(.47f, .34f), V(.34f, .31f), V(.21f, .35f), V(.12f, .22f), V(.2f, 0) };

        static Vector2 V(float x, float y) => new Vector2(x, y);

        LastSceneFigure(GameObject root, PieceKind kind, int team, Transform rig, Transform[] arms, Transform[] legs, GameObject crown)
        {
            Root = root; Kind = kind; Team = team; this.rig = rig; this.arms = arms; this.legs = legs; Crown = crown;
        }

        public Vector3 Position => Root.transform.position;

        public static LastSceneFigure Build(PieceKind kind, int team, Transform parent)
        {
            int t = team == 1 ? 1 : 0;
            Material body = LastSceneArt.Lit(BodyColor[t], .5f), trim = LastSceneArt.Lit(TrimColor[t], .45f);
            Material shoe = LastSceneArt.Lit(Shoe, .55f), gold = LastSceneArt.Lit(Gold, .7f, .7f);
            var root = new GameObject(kind + (t == 0 ? " (white)" : " (black)"));
            root.transform.SetParent(parent, false);
            var rig = LastSceneArt.Group(root.transform, "Body", Vector3.zero).transform;

            var legs = new Transform[2];
            for (int s = -1, i = 0; s <= 1; s += 2, i++)
            {
                legs[i] = LastSceneArt.Group(rig, "Leg", new Vector3(s * .17f, .44f, 0)).transform;
                LastSceneArt.Part(legs[i], "Shin", LastSceneArt.Cylinder(.085f, .095f, .3f, 14), shoe, new Vector3(0, -.16f, 0));
                LastSceneArt.Part(legs[i], "Shoe", LastSceneArt.Sphere(), shoe, new Vector3(0, -.36f, .05f), null, new Vector3(.3f, .18f, .42f));
            }

            var profile = kind == PieceKind.Rook ? RookBody : kind == PieceKind.Bishop ? BishopBody : PawnBody;
            string bodyKey = kind == PieceKind.Rook ? "rook" : kind == PieceKind.Bishop ? "bishop" : "pawn";
            LastSceneArt.Part(rig, "Body", LastSceneArt.Lathe("figure " + bodyKey, Concat(Skirt, profile), 28), body, Vector3.zero);

            float shoulderY = .9f, shoulderX = .34f, emblemY = .74f, emblemZ = .42f;
            GameObject crown = null;
            switch (kind)
            {
                case PieceKind.Rook:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2 + Mathf.PI / 6;
                        LastSceneArt.Part(rig, "Merlon", LastSceneArt.RoundedBox(new Vector3(.24f, .22f, .17f), .05f, 3), body,
                                          new Vector3(Mathf.Sin(a) * .45f, 1.6f, Mathf.Cos(a) * .45f), Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                    }
                    LastSceneArt.Part(rig, "Band", LastSceneArt.Cylinder(.445f, .445f, .05f, 28), trim, new Vector3(0, 1.02f, 0), null, null, false);
                    Face(rig, new Vector3(0, 1.16f, 0), .425f, true, t, 1f);
                    shoulderY = .98f; shoulderX = .42f; emblemY = .86f; emblemZ = .44f;
                    break;
                case PieceKind.Bishop:
                    LastSceneArt.Part(rig, "Collar", LastSceneArt.Cylinder(.34f, .34f, .08f), trim, new Vector3(0, 1.02f, 0));
                    LastSceneArt.Part(rig, "Mitre", LastSceneArt.Lathe("figure mitre", Mitre, 28), body, Vector3.zero);
                    LastSceneArt.Part(rig, "Knob", LastSceneArt.Sphere(.085f, 14, 10), body, new Vector3(0, 1.95f, 0));
                    LastSceneArt.Part(rig, "Slit", PieceFigure.Cube(), LastSceneArt.Lit(t == 0 ? 0x6B5446 : 0x1E1C24, .2f),
                                      new Vector3(.12f, 1.68f, .2f), Quaternion.Euler(0, 28f, 35f), new Vector3(.05f, .3f, .14f), false);
                    Face(rig, new Vector3(0, 1.32f, 0), .375f, false, t, .95f);
                    break;
                case PieceKind.Knight:
                    LastSceneArt.Part(rig, "Collar", LastSceneArt.Cylinder(.34f, .34f, .08f), trim, new Vector3(0, 1.1f, 0));
                    var head = LastSceneArt.Group(rig, "Head", new Vector3(0, .06f, 0), Quaternion.Euler(0, -90f, 0)).transform;
                    LastSceneArt.Part(head, "Horse", LastSceneArt.Prism("horse", Horse, .38f), body, new Vector3(.04f, 1.1f, 0));
                    LastSceneArt.Part(head, "Mane", LastSceneArt.RoundedBox(new Vector3(.12f, .62f, .2f), .04f, 3), LastSceneArt.Lit(t == 0 ? 0xD2A884 : 0x5E4638, .25f),
                                      new Vector3(-.28f, 1.62f, 0), Quaternion.Euler(0, 0, -16f));
                    Material eye = LastSceneArt.Lit(Eye, .85f), shine = LastSceneArt.Blended("shine", null, Color.white);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Decal(head, "Eye", eye, new Vector3(.19f, 1.84f, s * .25f), Quaternion.LookRotation(new Vector3(.35f, 0, s)), new Vector3(.15f, .21f, .07f));
                        Decal(head, "Shine", shine, new Vector3(.22f, 1.89f, s * .285f), Quaternion.identity, Vector3.one * .05f);
                        Decal(head, "Nostril", eye, new Vector3(.55f, 1.57f, s * .1f), Quaternion.identity, Vector3.one * .06f);
                    }
                    break;
                default:
                    LastSceneArt.Part(rig, "Collar", LastSceneArt.Cylinder(.36f, .36f, .09f), trim, new Vector3(0, 1.1f, 0));
                    float r = kind == PieceKind.Pawn ? .38f : .37f, headY = kind == PieceKind.Pawn ? 1.47f : 1.45f;
                    LastSceneArt.Part(rig, "Head", LastSceneArt.Sphere(r, 28, 20), body, new Vector3(0, headY, 0));
                    Face(rig, new Vector3(0, headY, 0), r, false, t, 1f);
                    if (kind == PieceKind.Queen)
                    {
                        LastSceneArt.Part(rig, "Coronet", LastSceneArt.Cylinder(.31f, .27f, .17f), gold, new Vector3(0, headY + .38f, 0));
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i / 8f * Mathf.PI * 2;
                            LastSceneArt.Part(rig, "Pearl", LastSceneArt.Sphere(.05f, 10, 8), gold, new Vector3(Mathf.Sin(a) * .29f, headY + .49f, Mathf.Cos(a) * .29f));
                        }
                        LastSceneArt.Part(rig, "Top Pearl", LastSceneArt.Sphere(.085f, 14, 10), gold, new Vector3(0, headY + .52f, 0));
                    }
                    else if (kind == PieceKind.King)
                    {
                        crown = LastSceneArt.Group(rig, "Crown", new Vector3(0, headY + .38f, 0));
                        KingCrown(crown.transform);
                    }
                    break;
            }

            LastSceneArt.Part(rig, "Emblem", LastSceneArt.Prism("crown", LastSceneArt.CrownOutline, .2f),
                              LastSceneArt.Lit(EmblemColor[t], .6f, t == 1 ? .6f : .1f), new Vector3(0, emblemY, emblemZ), Quaternion.Euler(-6f, 0, 0), Vector3.one * .26f, false);

            var arms = new Transform[2];
            for (int s = -1, i = 0; s <= 1; s += 2, i++)
            {
                arms[i] = LastSceneArt.Group(rig, "Arm", new Vector3(s * shoulderX, shoulderY, 0)).transform;
                LastSceneArt.Part(arms[i], "Sleeve", LastSceneArt.Cylinder(.08f, .085f, .28f, 14), body, new Vector3(s * .14f, 0, 0), Quaternion.Euler(0, 0, 90f));
                LastSceneArt.Part(arms[i], "Mitten", LastSceneArt.Sphere(.145f, 16, 12), shoe, new Vector3(s * .32f, 0, .02f));
            }
            return new LastSceneFigure(root, kind, t, rig, arms, legs, crown);
        }

        // A band and a cross in gold: the king's crown (also the one that flies off).
        public static void KingCrown(Transform parent)
        {
            var gold = LastSceneArt.Lit(Gold, .7f, .7f);
            LastSceneArt.Part(parent, "Band", LastSceneArt.Cylinder(.27f, .3f, .14f), gold, Vector3.zero);
            LastSceneArt.Part(parent, "Cross", PieceFigure.Cube(), gold, new Vector3(0, .22f, 0), null, new Vector3(.1f, .36f, .1f));
            LastSceneArt.Part(parent, "Cross Bar", PieceFigure.Cube(), gold, new Vector3(0, .27f, 0), null, new Vector3(.28f, .1f, .1f));
        }

        // Stand here, facing `lookAt` (usually the camera).
        public void Place(Vector3 position, Vector3 lookAt)
        {
            spot = position;
            spotYaw = Mathf.Atan2(lookAt.x - position.x, lookAt.z - position.z);
            Pose(default);
        }

        public void Pose(in FigurePose p)
        {
            float yaw = spotYaw + p.Yaw;
            float x = spot.x + p.Dx, y = spot.y + p.Jump, z = spot.z - p.Dz;
            // Lying on its side or back, the body's half width keeps it on the floor.
            y += .5f * Mathf.Sin(Mathf.Min(Mathf.PI / 2, Mathf.Max(Mathf.Abs(p.Tip), Mathf.Abs(p.Pitch))));
            if (p.Flip != 0)
            {
                // Somersault round the middle of the body, not the feet.
                const float c = .95f;
                float oz = -c * Mathf.Sin(p.Flip);
                y += c - c * Mathf.Cos(p.Flip);
                x += oz * Mathf.Sin(yaw);
                z += oz * Mathf.Cos(yaw);
            }
            Root.transform.localPosition = new Vector3(x, y, z);
            // Facing the camera a figure's left is screen right, so a tip is mirrored
            // to keep the samples' "positive tips to screen left".
            Root.transform.localRotation = Quaternion.Euler((p.Pitch + p.Flip) * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, -p.Tip * Mathf.Rad2Deg);
            float squash = p.Squash + p.Sit * .2f;
            rig.localPosition = new Vector3(0, -p.Sit * .12f, 0);
            rig.localRotation = Quaternion.Euler((p.Lean - p.Sit * .35f) * Mathf.Rad2Deg, 0, 0);
            rig.localScale = new Vector3(1 + squash * .5f, 1 - squash, 1 + squash * .5f);
            float left = p.ArmLeft ?? p.Arm, right = p.ArmRight ?? p.Arm;
            arms[0].localRotation = Quaternion.Euler(0, 0, -left * Mathf.Rad2Deg);
            arms[1].localRotation = Quaternion.Euler(0, 0, right * Mathf.Rad2Deg);
            legs[0].localRotation = Quaternion.Euler(p.Leg * Mathf.Rad2Deg, 0, 0);
            legs[1].localRotation = Quaternion.Euler(-p.Leg * Mathf.Rad2Deg, 0, 0);
        }

        // Two eyes with a shine and two blush discs on a round (or cylindrical) head.
        static void Face(Transform rig, Vector3 center, float r, bool cylinder, int team, float size)
        {
            Material eye = LastSceneArt.Lit(Eye, .85f), shine = LastSceneArt.Blended("shine", null, Color.white), blush = LastSceneArt.Lit(BlushColor[team], .25f);
            for (int s = -1; s <= 1; s += 2)
            {
                float ex = .135f * size;
                Vector3 n, p;
                if (cylinder)
                {
                    float z = Mathf.Sqrt(Mathf.Max(0, r * r - ex * ex));
                    p = center + new Vector3(s * ex, 0, z);
                    n = new Vector3(s * ex, 0, z).normalized;
                }
                else
                {
                    n = new Vector3(s * ex / r, 0, 1).normalized;
                    p = center + n * r * .985f;
                }
                Decal(rig, "Eye", eye, p, Quaternion.LookRotation(n), new Vector3(.12f, .17f, .06f) * size);
                Decal(rig, "Shine", shine, p + new Vector3(s * .008f + .018f * size, .04f * size, .03f), Quaternion.identity, Vector3.one * .042f * size);
                float bx = .24f * size, by = -.1f * size;
                Vector3 bn, bp;
                if (cylinder)
                {
                    float z = Mathf.Sqrt(Mathf.Max(0, r * r - bx * bx));
                    bp = center + new Vector3(s * bx, by, z + .003f);
                    bn = new Vector3(s * bx, 0, z).normalized;
                }
                else
                {
                    bn = new Vector3(s * bx / r, by / r, 1).normalized;
                    bp = center + bn * r * 1.003f;
                }
                Decal(rig, "Blush", blush, bp, Quaternion.LookRotation(bn), new Vector3(.16f, .1f, .02f) * size);
            }
        }

        static void Decal(Transform parent, string name, Material material, Vector3 position, Quaternion rotation, Vector3 scale) =>
            LastSceneArt.Part(parent, name, LastSceneArt.Sphere(.5f, 18, 12), material, position, rotation, scale, false);

        static Vector2[] Concat(Vector2[] a, Vector2[] b)
        {
            var all = new Vector2[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }
    }
}
