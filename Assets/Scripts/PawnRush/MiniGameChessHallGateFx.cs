using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R643, H · 국왕 체스 홀 (Checkmate): the board opens the way out. A path of board squares (4 across, 4 deep, in
    // front of the doorway) turns over row by row from the court toward the door, "탁탁탁": each square pops up a
    // little, flips over and slams down, ebony coming up where ivory was and the other way round, so the path ends as
    // a chequer. A brass edge then slams round the path. They sink back into the floor from the door outward.
    // 3.2 s. See MiniGameGateMoment.
    public sealed class MiniGameChessHallGateFx : MiniGameGateMoment
    {
        const int Across = 4, Deep = 4;
        const float Square = .86f, Pitch = .9f, Lift = .32f, Flip = .17f, RowGap = .1f, FirstRow = .08f, Layer = .028f;

        readonly List<(Part pivot, int row, int col)> squares = new List<(Part, int, int)>();
        readonly List<Part> edge = new List<Part>();

        protected override float Length => 3.2f;

        protected override void Build()
        {
            squares.Clear();
            edge.Clear();
            for (int row = 0; row < Deep; row++)
                for (int col = 0; col < Across; col++)
                {
                    float x = (col - (Across - 1) * .5f) * Pitch, z = .55f + row * Pitch;
                    var pivot = Pivot("H square", new Vector3(x, .03f, z));
                    // ivory or ebony on top when it lands (a chequer), the other colour underneath before the flip
                    bool ivoryUp = (row + col) % 2 == 0;
                    Box("H square top", new Vector3(0f, -Layer * .5f, 0f), new Vector3(Square, Layer, Square), ivoryUp ? Ivory : Ebony, pivot.T);
                    Box("H square under", new Vector3(0f, Layer * .5f, 0f), new Vector3(Square, Layer, Square), ivoryUp ? Ebony : Ivory, pivot.T);
                    squares.Add((pivot, row, col));
                }
            // the brass edge round the path
            float w = Across * Pitch + .1f, d = Deep * Pitch + .1f, cz = .55f + (Deep - 1) * Pitch * .5f;
            edge.Add(Box("H edge", new Vector3(0f, .05f, cz - d * .5f), new Vector3(w, .05f, .07f), Brass));
            edge.Add(Box("H edge", new Vector3(0f, .05f, cz + d * .5f), new Vector3(w, .05f, .07f), Brass));
            edge.Add(Box("H edge", new Vector3(-w * .5f, .05f, cz), new Vector3(.07f, .05f, d), Brass));
            edge.Add(Box("H edge", new Vector3(w * .5f, .05f, cz), new Vector3(.07f, .05f, d), Brass));
        }

        protected override void Pose(float t)
        {
            foreach (var (pivot, row, col) in squares)
            {
                // from the court's end (the last row) toward the door, a row every 0.1 s, the middle two first
                float start = FirstRow + (Deep - 1 - row) * RowGap + (col == 0 || col == Across - 1 ? .025f : 0f);
                float since = t - start;
                // out of sight until its turn, then up, over and down; it sinks from the door outward at the end
                float sink = Fade(t, 2.45f + row * .08f, .3f);
                if (since < 0f) { pivot.T.localScale = Vector3.one * .0001f; continue; }
                float u = Mathf.Clamp01(since / Flip);
                float up = Mathf.Sin(u * Mathf.PI) * Lift;
                // the landing squash: flat for an instant, back by 0.08 s
                float squash = since > Flip && since < Flip + .08f ? Mathf.Lerp(.55f, 1f, (since - Flip) / .08f) : 1f;
                float grow = Mathf.Min(1f, since / .04f);
                pivot.T.localScale = new Vector3(grow * sink, Mathf.Max(.0001f, grow * squash * sink), grow * sink);
                pivot.T.localPosition = new Vector3(pivot.Home.x, pivot.Home.y + up - (1f - sink) * .05f, pivot.Home.z);
                // over toward the door (about the square's own across axis)
                pivot.T.localRotation = Quaternion.Euler(-180f * (1f - (1f - u) * (1f - u)), 0f, 0f);
            }
            // the brass edge lands once the last row (the one by the door) is down
            float edgeAt = FirstRow + (Deep - 1) * RowGap + Flip + .04f;
            float k = t < edgeAt ? 0f : Slam(t - edgeAt, 1.12f) * Fade(t, 2.45f, .3f);
            k = Mathf.Max(.0001f, k);
            foreach (var bar in edge) bar.T.localScale = bar.Size * k;
        }
    }
}
