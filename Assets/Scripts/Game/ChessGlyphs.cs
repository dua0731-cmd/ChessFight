using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The voice chat's small drawings (R131), with Painter2D like MenuMarks: crisp at any menu size and
    // independent of fonts (the body font has no chess symbols). Only MoveTo, LineTo and Arc, the calls
    // the Linux compile check's stand-in knows.
    public static class ChessGlyphs
    {
        static readonly Color Ivory = new Color(244 / 255f, 236 / 255f, 220 / 255f);
        static readonly Color Ebony = new Color(27 / 255f, 21 / 255f, 18 / 255f);
        static readonly Color Rim = new Color(233 / 255f, 220 / 255f, 192 / 255f);
        static readonly Color Ink = new Color(26 / 255f, 18 / 255f, 11 / 255f);

        static Vector2[] Outline(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.King: return ChessOutlines.King;
                case PieceKind.Queen: return ChessOutlines.Queen;
                case PieceKind.Rook: return ChessOutlines.Rook;
                case PieceKind.Bishop: return ChessOutlines.Bishop;
                case PieceKind.Knight: return ChessOutlines.Knight;
                default: return ChessOutlines.Pawn;
            }
        }

        // A chess piece standing in its box, white (ivory with a dark edge) or black (ebony with a light
        // rim). `cut` is what shows through the bishop's mitre: the colour under the piece.
        public sealed class PieceMark : VisualElement
        {
            PieceKind kind;
            bool white;
            Color cut;

            public PieceMark(PieceKind piece, bool whitePiece, Color under)
            {
                kind = piece;
                white = whitePiece;
                cut = under;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void Set(PieceKind piece, bool whitePiece)
            {
                if (kind == piece && white == whitePiece) return;
                kind = piece;
                white = whitePiece;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                // The 100 × 200 box (pieces stand on y = 196), scaled to the height, centred.
                float s = Mathf.Min(rect.height / 200f, rect.width / 100f);
                var origin = new Vector2(rect.center.x - 50f * s, rect.yMax - 198f * s);
                Vector2 P(Vector2 q) => origin + q * s;
                var p = context.painter2D;
                Color fill = white ? Ivory : Ebony, edge = white ? Ink : Rim;
                p.fillColor = fill;
                p.strokeColor = edge;
                p.lineWidth = Mathf.Max(1f, 9f * s);
                p.lineJoin = LineJoin.Round;
                var outline = Outline(kind);
                p.BeginPath();
                p.MoveTo(P(outline[0]));
                for (int i = 1; i < outline.Length; i++) p.LineTo(P(outline[i]));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                if (kind == PieceKind.Queen)
                    foreach (var ball in ChessOutlines.QueenBalls) Disc(p, P(new Vector2(ball.x, ball.y)), ball.z * s);
                else if (kind == PieceKind.Pawn)
                    Disc(p, P(new Vector2(ChessOutlines.PawnHead.x, ChessOutlines.PawnHead.y)), ChessOutlines.PawnHead.z * s);
                else if (kind == PieceKind.Bishop)
                {
                    p.strokeColor = cut;
                    p.lineWidth = Mathf.Max(1f, 7f * s);
                    p.lineCap = LineCap.Round;
                    p.BeginPath();
                    p.MoveTo(P(ChessOutlines.BishopCut[0]));
                    p.LineTo(P(ChessOutlines.BishopCut[1]));
                    p.Stroke();
                }
            }

            static void Disc(Painter2D p, Vector2 centre, float radius)
            {
                p.BeginPath();
                p.MoveTo(centre + new Vector2(radius, 0f));
                p.Arc(centre, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }
        }

        // A loudspeaker in a 24-unit box: quiet (no waves), talking (two waves) or muted (a cross).
        public sealed class SpeakerMark : VisualElement
        {
            public enum Look { Quiet, Talking, Muted }
            Look look;
            Color color;
            float width;

            public SpeakerMark(Color tint, float lineWidth = 2.2f)
            {
                color = tint;
                width = lineWidth;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void Set(Look state, Color tint)
            {
                if (look == state && color == tint) return;
                look = state;
                color = tint;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                float s = Mathf.Min(rect.width, rect.height) / 24f;
                var origin = rect.center - new Vector2(12f, 12f) * s;
                Vector2 P(float x, float y) => origin + new Vector2(x, y) * s;
                var p = context.painter2D;
                p.fillColor = color;
                p.strokeColor = color;
                p.lineWidth = width * s;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(P(3f, 9f)); p.LineTo(P(7f, 9f)); p.LineTo(P(12f, 4.5f));
                p.LineTo(P(12f, 19.5f)); p.LineTo(P(7f, 15f)); p.LineTo(P(3f, 15f));
                p.ClosePath();
                p.Fill();
                p.BeginPath();
                if (look == Look.Muted)
                {
                    p.MoveTo(P(15.5f, 9f)); p.LineTo(P(21f, 15f));
                    p.MoveTo(P(21f, 9f)); p.LineTo(P(15.5f, 15f));
                }
                else
                {
                    Wave(p, P(12f, 12f), 4.6f * s);
                    if (look == Look.Talking) Wave(p, P(12f, 12f), 8.4f * s);
                }
                p.Stroke();
            }

            static void Wave(Painter2D p, Vector2 centre, float radius)
            {
                p.MoveTo(centre + new Vector2(Mathf.Cos(-50f * Mathf.Deg2Rad), Mathf.Sin(-50f * Mathf.Deg2Rad)) * radius);
                p.Arc(centre, radius, new Angle(-50f, AngleUnit.Degree), new Angle(50f, AngleUnit.Degree));
            }
        }

        // Three little level bars that bob while someone talks.
        public sealed class TalkBars : VisualElement
        {
            Color color;
            readonly float phase;

            public TalkBars(Color tint, float offset = 0f)
            {
                color = tint;
                phase = offset;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                schedule.Execute(() => { if (resolvedStyle.display != DisplayStyle.None) MarkDirtyRepaint(); }).Every(40);
            }

            public void SetColor(Color tint)
            {
                if (color == tint) return;
                color = tint;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                float w = rect.width / 5f, t = Time.unscaledTime + phase;
                var p = context.painter2D;
                p.fillColor = color;
                for (int i = 0; i < 3; i++)
                {
                    // The sample's .5 s bob, each bar .15 s after the last.
                    float k = .35f + .65f * (.5f + .5f * Mathf.Sin((t - i * .15f) / .5f * Mathf.PI * 2f));
                    float h = rect.height * k, x = rect.x + i * w * 2f;
                    p.BeginPath();
                    p.MoveTo(new Vector2(x, rect.yMax));
                    p.LineTo(new Vector2(x + w, rect.yMax));
                    p.LineTo(new Vector2(x + w, rect.yMax - h));
                    p.LineTo(new Vector2(x, rect.yMax - h));
                    p.ClosePath();
                    p.Fill();
                }
            }
        }
    }
}
