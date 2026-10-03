using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // The title screen's backdrop (design C revised, 2026-10-03): the white king on
    // a walnut pedestal with a brass rim in the middle of the dark wooden hall,
    // five pieces a side behind it, white lit cyan from the left and black red
    // from the right, warm beams sweeping the haze. The title sits above the king
    // (IntroHud.uxml). Numbers are the design sample's (MenuArt.Web).
    //
    // Display only. The camera drifts a little and the pieces breathe; nothing
    // takes input.
    public sealed class IntroStage : MonoBehaviour
    {
        static readonly (PieceKind kind, float x, float z)[] Sides =
        {
            (PieceKind.Queen, -4.2f, -1.6f), (PieceKind.Rook, -5.9f, -3.3f), (PieceKind.Bishop, -3.1f, -3.9f),
            (PieceKind.Knight, -7.3f, -1.1f), (PieceKind.Pawn, -5.1f, -5.3f)
        };

        Camera view;
        LastSceneFigure king;
        readonly List<LastSceneFigure> pieces = new List<LastSceneFigure>();
        readonly Transform[] sweeps = new Transform[4];

        public void Build(Camera camera)
        {
            view = camera;
            if (view == null)
            {
                var go = new GameObject("ChessFight Camera") { tag = "MainCamera" };
                view = go.AddComponent<Camera>();
            }
            view.fieldOfView = 36f;
            var root = transform;
            MenuArt.Hall(root, view, 22f, 72f);

            // The pedestal: walnut, a brass rim on top and an LED ring on the floor.
            LastSceneArt.Part(root, "Pedestal", LastSceneArt.Cylinder(1.8f, 1.6f, .5f, 64), MenuArt.Wood(MenuArt.Walnut, .65f, new Vector2(4f, 1f)),
                              new Vector3(0, .25f, 0));
            LastSceneArt.Part(root, "Pedestal Rim", MenuArt.Torus(1.62f, .05f), MenuArt.BrassMetal(), new Vector3(0, .5f, 0));
            LastSceneArt.Part(root, "Pedestal Light", MenuArt.Torus(1.86f, .03f), MenuArt.Lamp(MenuArt.Led), new Vector3(0, .04f, 0), null, null, false);

            king = MenuArt.Figure(root, PieceKind.King, 0, MenuArt.Web(0, .5f, 0), MenuArt.Web(1.4f, .5f, 10f));
            foreach (var (kind, x, z) in Sides)
            {
                pieces.Add(MenuArt.Figure(root, kind, 0, MenuArt.Web(x, 0, z), MenuArt.Web(0, 0, 9f)));
                pieces.Add(MenuArt.Figure(root, kind, 1, MenuArt.Web(-x, 0, z), MenuArt.Web(0, 0, 9f)));
            }

            MenuArt.Spot(root, MenuArt.Web(2.5f, 9, 7), MenuArt.Web(0, 1.6f, 0), 0xFFE6C2, 1.6f, .3f, .5f, true);
            MenuArt.Spot(root, MenuArt.Web(0, 9, -6), MenuArt.Web(0, 1.6f, 0), 0xFFF0DC, 2.6f, .3f, .5f);
            MenuArt.Spot(root, MenuArt.Web(-9, 6, -5), MenuArt.Web(-5, 1, -3), MenuArt.Cyan, 2.4f, .6f, .6f);
            MenuArt.Spot(root, MenuArt.Web(9, 6, -5), MenuArt.Web(5, 1, -3), MenuArt.Red, 2.4f, .6f, .6f);
            MenuArt.Spot(root, MenuArt.Web(0, 7, 9), MenuArt.Web(0, 1, -3), 0xFFD9A8, 1f, .6f, .7f);

            MenuArt.Beam(root, MenuArt.Web(0, 13, -1), MenuArt.Web(0, 0, 0), 0xFFE8C8, 2.4f, .085f);
            MenuArt.Halo(root, MenuArt.Web(0, 13, -1), 0xFFF0D8, 3.4f, .8f, view);
            sweeps[0] = MenuArt.Beam(root, MenuArt.Web(-11, 0, -18), MenuArt.Web(-2, 14, -4), MenuArt.Warm, 2.6f, .06f);
            sweeps[1] = MenuArt.Beam(root, MenuArt.Web(11, 0, -18), MenuArt.Web(2, 14, -4), MenuArt.Warm, 2.6f, .06f);
            sweeps[2] = MenuArt.Beam(root, MenuArt.Web(-5, 0, -22), MenuArt.Web(-1, 15, -8), MenuArt.Cyan, 2.2f, .035f);
            sweeps[3] = MenuArt.Beam(root, MenuArt.Web(5, 0, -22), MenuArt.Web(1, 15, -8), MenuArt.Red, 2.2f, .035f);
            MenuArt.Halo(root, MenuArt.Web(-11, .3f, -18), MenuArt.Warm, 3.2f, .8f, view);
            MenuArt.Halo(root, MenuArt.Web(11, .3f, -18), MenuArt.Warm, 3.2f, .8f, view);
            Animate(0f);
        }

        void Animate(float t)
        {
            float b = Mathf.Sin(t * 1.6f);
            king?.Pose(new FigurePose { Jump = .02f + .02f * b, ArmLeft = .35f + .08f * b, ArmRight = .35f + .08f * b, Squash = -.012f * b });
            for (int i = 0; i < pieces.Count; i++) MenuArt.Idle(pieces[i], t, i, .5f);

            MenuArt.Aim(sweeps[0], MenuArt.Web(-11, 0, -18), MenuArt.Web(-2 + Mathf.Sin(t * .5f) * 5, 14, -4));
            MenuArt.Aim(sweeps[1], MenuArt.Web(11, 0, -18), MenuArt.Web(2 - Mathf.Sin(t * .5f + 1) * 5, 14, -4));
            MenuArt.Aim(sweeps[2], MenuArt.Web(-5, 0, -22), MenuArt.Web(-1 + Mathf.Sin(t * .4f + 2) * 4, 15, -8));
            MenuArt.Aim(sweeps[3], MenuArt.Web(5, 0, -22), MenuArt.Web(1 - Mathf.Sin(t * .4f + 3) * 4, 15, -8));

            if (view == null) return;
            view.transform.position = MenuArt.Web(Mathf.Sin(t * .12f) * .6f, 2.5f, 12.4f + Mathf.Sin(t * .22f) * .5f);
            view.transform.LookAt(MenuArt.Web(0, 3f, 0));
        }

        void Update() => Animate(Time.unscaledTime);
    }
}
