using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // The title screen's backdrop: a full chess set lined up on a raised board in
    // the plaza, seen from low over White's right shoulder. The left of the frame
    // stays calm for the title block (IntroHud.uxml).
    //
    // Display only. The camera drifts a little and the pieces breathe; nothing
    // takes input.
    public sealed class IntroStage : MonoBehaviour
    {
        const float Tile = 1.5f;
        static readonly PieceKind[] BackRank =
        {
            PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop, PieceKind.Queen,
            PieceKind.King, PieceKind.Bishop, PieceKind.Knight, PieceKind.Rook
        };
        static readonly Vector3 CameraAt = new Vector3(15.5f, 8.2f, -7.5f);
        static readonly Vector3 LookAt = new Vector3(-1.4f, .4f, -.6f);

        Camera view;
        readonly List<Transform> pieces = new List<Transform>();
        readonly List<float> rest = new List<float>();

        public void Build(Camera camera)
        {
            view = camera;
            if (view == null)
            {
                var go = new GameObject("ChessFight Camera") { tag = "MainCamera" };
                view = go.AddComponent<Camera>();
            }
            view.fieldOfView = 32f;
            StageKit.Environment(transform, view, 40f, 160f);
            StageKit.Floor(transform);
            float top = StageKit.Board(transform, "Board", 8, 8, Tile, Vector3.zero, .5f, .8f);

            // White (the camera's side) faces +Z towards Black; knights turn out a
            // little so their profile reads.
            for (int i = 0; i < 8; i++)
            {
                float x = (i - 3.5f) * Tile;
                bool knight = BackRank[i] == PieceKind.Knight;
                Place(BackRank[i], PieceSkin.White, new Vector3(x, top, -3.5f * Tile), knight ? -34f : 0f);
                Place(BackRank[i], PieceSkin.Black, new Vector3(x, top, 3.5f * Tile), knight ? 214f : 180f);
                Place(PieceKind.Pawn, PieceSkin.White, new Vector3(x, top, -2.5f * Tile), 0f);
                Place(PieceKind.Pawn, PieceSkin.Black, new Vector3(x, top, 2.5f * Tile), 180f);
            }
            Aim(0f);
        }

        void Place(PieceKind kind, PieceSkin skin, Vector3 at, float yaw)
        {
            var figure = PieceFigure.Build(kind, skin, transform);
            figure.transform.localPosition = at;
            figure.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            pieces.Add(figure.transform);
            rest.Add(at.y);
        }

        // A slow sway round the board, a couple of degrees either way.
        void Aim(float t)
        {
            if (view == null) return;
            float angle = Mathf.Sin(t * .12f) * 2.5f;
            view.transform.position = Quaternion.Euler(0, angle, 0) * CameraAt;
            view.transform.LookAt(LookAt);
        }

        void Update()
        {
            float t = Time.unscaledTime;
            Aim(t);
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i].localPosition;
                p.y = rest[i] + Mathf.Abs(Mathf.Sin(t * 1.6f + i * .7f)) * .03f;
                pieces[i].localPosition = p;
            }
        }
    }
}
