# Reference images for the Royal Carriage Yard concept-art prompt (R676):
#  - top-down layout maps of the course drawn from the game's coordinates (match / section version, map z, far end at
#    the top), the whole course and four parts, so an image model copies counts and positions;
#  - the two pawn crops of the current player look (RigWork renders).
# Coordinates come from RoyalCarriageYard*.cs after R662 (map z = own z - 24 for the sorting yard and after) and match
# Docs/PawnRush/ConceptArt/R676_RoyalCarriageYard/PROMPT.md "Layout, south to north". Static layout only: one moment
# (both cranes at rest pointing west, all four hoist pallets drawn, the coffer over the second berth from the left).
# Run (Windows PowerShell 5.1): powershell -ExecutionPolicy Bypass -File rcy_concept_refs.ps1 [-Out <folder>]
param([string]$Out = (Join-Path $PSScriptRoot '..\..\..\Docs\PawnRush\ConceptArt\R676_RoyalCarriageYard'))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class RcyMap
{
    static Graphics g;

    static Color C(string hex, int a) { Color c = ColorTranslator.FromHtml(hex); return Color.FromArgb(a, c.R, c.G, c.B); }
    static Color C(string hex) { return C(hex, 255); }

    static readonly Color Parchment = C("#F4ECDC");
    static readonly Color Floor = C("#D9CBAE");
    static readonly Color Wall = C("#B7AA92");
    static readonly Color Edge = C("#7C7262");
    static readonly Color Marble = C("#FBF8F2");
    static readonly Color Water = C("#3D9EBD");
    static readonly Color Oak = C("#BC9665");
    static readonly Color Walnut = C("#65432D");
    static readonly Color Goods = C("#94704C");
    static readonly Color Crate = C("#B88B5A");
    static readonly Color CrateTop = C("#8A5F3A");
    static readonly Color Roof = C("#6E7075");
    static readonly Color Iron = C("#393D43");
    static readonly Color Gold = C("#E2B23C");
    static readonly Color Red = C("#FF3829", 95);
    static readonly Color Green = C("#59FF73", 170);
    static readonly Color Cream = C("#E6D9BC");
    static readonly Color Light = C("#E4D9C2");
    static readonly Color Shade = C("#A99C84");
    static readonly Color CoachGreen = C("#2E5D4B");
    static readonly Color CoachBrown = C("#4F3424");
    static readonly Color Bay = C("#7A4A2A");
    static readonly Color Grey = C("#D8D1C4");
    static readonly Color Black = C("#2E2925");
    static readonly Color Fall = C("#DBF0FF");
    static readonly Color Dark = C("#3A3530");
    static readonly Color Lawn = C("#B9C79A");

    static void Poly(Color c, PointF[] p) { using (var b = new SolidBrush(c)) g.FillPolygon(b, p); }
    static void Outline(Color c, float w, PointF[] p) { using (var pen = new Pen(c, w)) g.DrawPolygon(pen, p); }
    static PointF[] Quad(float x0, float z0, float x1, float z1)
    {
        return new[] { new PointF(x0, z0), new PointF(x1, z0), new PointF(x1, z1), new PointF(x0, z1) };
    }
    static void Rect(float x0, float z0, float x1, float z1, Color c) { Poly(c, Quad(x0, z0, x1, z1)); }
    static void Boxed(float x0, float z0, float x1, float z1, Color c, Color edge, float w)
    {
        Rect(x0, z0, x1, z1, c);
        Outline(edge, w, Quad(x0, z0, x1, z1));
    }
    // A rectangle and its mirror image across x = 0 (x0 < x1, both >= 0).
    static void Mirror(float x0, float z0, float x1, float z1, Color c) { Rect(x0, z0, x1, z1, c); Rect(-x1, z0, -x0, z1, c); }
    static void Disc(float x, float z, float r, Color c) { using (var b = new SolidBrush(c)) g.FillEllipse(b, x - r, z - r, 2 * r, 2 * r); }
    static void Round(float x, float z, float r, Color c, Color edge)
    {
        Disc(x, z, r, c);
        using (var pen = new Pen(edge, .12f)) g.DrawEllipse(pen, x - r, z - r, 2 * r, 2 * r);
    }
    // Part of a ring round (x, z), from angle a0 to a1 in degrees (0 = east, 90 = north).
    static void Annulus(float x, float z, float r0, float r1, float a0, float a1, Color c)
    {
        var pts = new List<PointF>();
        const int n = 64;
        for (int i = 0; i <= n; i++) { double a = (a0 + (a1 - a0) * i / n) * Math.PI / 180; pts.Add(new PointF(x + (float)(r1 * Math.Cos(a)), z + (float)(r1 * Math.Sin(a)))); }
        for (int i = n; i >= 0; i--) { double a = (a0 + (a1 - a0) * i / n) * Math.PI / 180; pts.Add(new PointF(x + (float)(r0 * Math.Cos(a)), z + (float)(r0 * Math.Sin(a)))); }
        Poly(c, pts.ToArray());
    }
    static void Line(float x0, float z0, float x1, float z1, float w, Color c) { using (var p = new Pen(c, w)) g.DrawLine(p, x0, z0, x1, z1); }
    // A rectangle in a body's frame: `along` forward, `side` to the right; heading 0 = north, 90 = east.
    static void Body(float x, float z, float heading, float a0, float a1, float s0, float s1, Color c)
    {
        double h = heading * Math.PI / 180;
        float fx = (float)Math.Sin(h), fz = (float)Math.Cos(h), rx = fz, rz = -fx;
        Func<float, float, PointF> P = (a, s) => new PointF(x + fx * a + rx * s, z + fz * a + rz * s);
        Poly(c, new[] { P(a0, s0), P(a1, s0), P(a1, s1), P(a0, s1) });
    }
    // A rank band: one row of 1 m squares, alternating light and shade stone.
    static void Band(float x0, float x1, float z0, float z1)
    {
        int i = 0;
        for (float x = x0; x < x1 - .01f; x += 1f, i++)
            Rect(x, z0, Math.Min(x + 1f, x1), z1, i % 2 == 0 ? Light : Shade);
    }
    static void Lamp(float x, float z) { Disc(x, z, .3f, Iron); }

    // One coach rig, 6.95 x 2.3 m round its centre: the coach (4.2 m) behind, the pair of horses (2.6 m) in front.
    static void Rig(float x, float z, float heading, Color body, Color coat)
    {
        Body(x, z, heading, -3.475f, .725f, -1.15f, 1.15f, Walnut);
        Body(x, z, heading, -3.2f, .5f, -1.05f, 1.05f, body);
        Body(x, z, heading, -1.85f, -.95f, -.35f, .35f, C("#9C8354"));     // crown finial on the roof
        Body(x, z, heading, .725f, 2.9f, -.07f, .07f, Walnut);              // pole
        foreach (float s in new[] { -.55f, .55f })
        {
            Body(x, z, heading, .875f, 3.1f, s - .3f, s + .3f, coat);       // horse body
            Body(x, z, heading, 3.1f, 3.475f, s - .17f, s + .17f, coat);    // neck and head
        }
    }

    // A storage row across the yard at z: nine slots a half on a 2.2 m pitch, one gap a half (the same slot, mirrored).
    static void Row(float z, int gap)
    {
        for (int i = 0; i < 9; i++)
        {
            float cx = 1.1f + 2.2f * i;
            if (i == gap) { Mirror(cx - .95f, z - 1.2f, cx + .95f, z + 1.2f, Green); continue; }
            foreach (float s in new[] { -1f, 1f })
                Boxed(Math.Min(s * (cx - 1f), s * (cx + 1f)), z - .9f, Math.Max(s * (cx - 1f), s * (cx + 1f)), z + .9f, Crate, CrateTop, .1f);
        }
        Mirror(19.8f, z - .9f, 23.5f, z + .9f, Goods);                       // fixed stock to the stalls
        Line(-26f, z, 26f, z, .3f, Walnut);                                    // the row crane's beam, overhead
    }

    static void Draw()
    {
        // Lawns round the castle, the course on them.
        Rect(-40f, -30f, 40f, 200f, Lawn);

        // ---------------- start court and outer gate (z -17 .. 2)
        Rect(-12f, -16f, 12f, 2f, Floor);
        Rect(-13f, -17f, -12f, 0f, Wall); Rect(12f, -17f, 13f, 0f, Wall); Rect(-13f, -17f, 13f, -16f, Wall);
        Boxed(-10.95f, -14.6f, -8.65f, -10.4f, CoachGreen, Walnut, .15f);     // parked coach, nose north, no horses
        Boxed(9.1f, -15.3f, 10.5f, -13.9f, Crate, CrateTop, .1f);
        Boxed(9.1f, -13.7f, 10.5f, -12.3f, Crate, CrateTop, .1f);
        Rect(-23f, 0f, -3f, 2f, Wall); Rect(3f, 0f, 23f, 2f, Wall);
        Band(-3f, 3f, .5f, 1.5f);
        Rect(-3f, 1f, -2.65f, 4f, Walnut); Rect(2.65f, 1f, 3f, 4f, Walnut);  // the leaves, swung open into the yard
        Round(-4.8f, 1f, 1.5f, Wall, Edge); Round(4.8f, 1f, 1.5f, Wall, Edge);
        Round(-24f, 1f, 2f, Wall, Edge); Round(24f, 1f, 2f, Wall, Edge);

        // ---------------- wagon yard (z 2 .. 50)
        Rect(-22f, 2f, 22f, 50f, Floor);
        Rect(-23f, 2f, -22f, 50f, Wall); Rect(22f, 2f, 23f, 50f, Wall);
        Mirror(18.65f, 2.6f, 22f, 47.6f, Roof);                                // granary lean-tos over the solid goods
        for (int k = 0; k <= 10; k++)
        {
            float z = 2.6f + 4.5f * k;
            Line(-22f, z, -18.65f, z, .12f, Dark); Line(18.65f, z, 22f, z, .12f, Dark);
            Disc(-18.9f, z, .2f, Walnut); Disc(18.9f, z, .2f, Walnut);
        }
        Mirror(18.65f, 22.6f, 20.4f, 27.6f, Cream);                            // the raised royal canopies, mid-row
        Rig(-7.3f, 15.1f, 90f, CoachGreen, Bay);
        Rig(7.3f, 15.1f, -90f, CoachBrown, Grey);
        Rig(-7.3f, 27.3f, 90f, CoachGreen, Black);
        Rig(7.3f, 27.3f, -90f, CoachBrown, Bay);
        Rig(0f, 39.4f, 0f, CoachGreen, Grey);

        // ---------------- canal and the giant waterwheel (z 50 .. 82)
        Rect(-22f, 50f, 22f, 82f, Water);
        Rect(-23f, 50f, -22f, 82f, Wall); Rect(22f, 50f, 23f, 82f, Wall);
        Mirror(10f, 50f, 22f, 50.4f, Wall);                                    // parapets beside the entry landing
        Mirror(10f, 81.6f, 22f, 82f, Wall);                                    // and beside the far landing
        Rect(-10f, 50f, 10f, 58f, Floor);                                      // entry landing
        Mirror(9.6f, 50f, 10f, 58f, Marble);
        Rect(-9.6f, 57.6f, -6.2f, 58f, Marble); Rect(-3f, 57.6f, 3f, 58f, Marble); Rect(6.2f, 57.6f, 9.6f, 58f, Marble);
        Lamp(-9f, 51f); Lamp(9f, 51f);
        Band(-10f, 10f, 52.5f, 53.5f);                                         // checkpoint 1
        Mirror(3f, 58f, 6.2f, 64.3f, Oak); Mirror(3f, 67.7f, 6.2f, 74f, Oak);  // the walkways, broken at the wheel
        for (float z = 58.5f; z < 74f; z += 1f)
            if (z < 64.1f || z > 67.9f) { Line(-6.17f, z, -3.03f, z, .05f, Walnut); Line(3.03f, z, 6.17f, z, .05f, Walnut); }
        Mirror(3f, 64.12f, 6.2f, 64.3f, Gold); Mirror(3f, 67.7f, 6.2f, 67.88f, Gold);
        Rect(-9f, 65.5f, 9f, 66.5f, Oak);                                      // the wheel, edge on from above
        Rect(-9f, 65.5f, 9f, 65.62f, Walnut); Rect(-9f, 66.38f, 9f, 66.5f, Walnut);
        Rect(-.35f, 64.5f, .35f, 65.1f, Walnut); Rect(-.35f, 66.9f, .35f, 67.5f, Walnut);
        Line(0f, 64.6f, 0f, 67.4f, .45f, Iron);
        Rect(-.8f, 65.7f, .8f, 73f, Walnut); Rect(-.7f, 65.7f, .7f, 73f, Water);  // flume, high over the wheel
        Boxed(-1.2f, 72.4f, 1.2f, 73.8f, Walnut, Dark, .1f);                    // cistern
        Rect(8.1f, 64.9f, 9.1f, 65.5f, Fall);                                  // the spill on the wheel's falling side
        Rect(-10f, 74f, 10f, 82f, Floor);                                      // far landing
        Mirror(9.6f, 74f, 10f, 82f, Marble);
        Rect(-9.6f, 74f, -6.2f, 74.4f, Marble); Rect(-3f, 74f, 3f, 74.4f, Marble); Rect(6.2f, 74f, 9.6f, 74.4f, Marble);
        Lamp(-9f, 81f); Lamp(9f, 81f);
        Band(-10f, 10f, 78f, 79f);                                             // checkpoint 2
        Round(-28f, 82f, 2f, Wall, Edge); Round(28f, 82f, 2f, Wall, Edge);

        // ---------------- royal warehouse (z 82 .. 132.85)
        Rect(-26f, 82f, 26f, 132.85f, Floor);
        Mirror(22f, 81.6f, 27f, 82.4f, Wall);                                  // where the bailey widens
        Mirror(26f, 82f, 27f, 157f, Wall);                                     // the tall side walls, on to the final gate
        Mirror(23.15f, 82f, 26f, 129.5f, Roof);                                // sorting stalls under their lean-tos
        for (int k = 0; k <= 10; k++)
        {
            float z = 82f + 4.75f * k;
            Line(-26f, z, -23.15f, z, .12f, Dark); Line(23.15f, z, 26f, z, .12f, Dark);
        }
        Rect(-24.3f, 86.85f, -23.15f, 89.15f, Dark); Rect(23.15f, 86.85f, 24.3f, 89.15f, Dark);  // the two doorways
        Lamp(-11.2f, 82.8f); Lamp(11.2f, 82.8f);
        // The cargo line, west to east, nine loads on it (two of them two-crate stacks).
        Rect(-23.5f, 87.2f, 23.5f, 88.8f, Walnut);
        Rect(-23.5f, 87.35f, 23.5f, 88.65f, Oak);
        for (float x = -23.2f; x < 23.5f; x += .6f) Line(x, 87.35f, x, 88.65f, .05f, Walnut);
        Rect(-23.5f, 87.2f, 23.5f, 87.35f, Gold); Rect(-23.5f, 88.65f, 23.5f, 88.8f, Gold);
        float[] loads = { -19.5f, -14.8f, -10.2f, -5.8f, -1.3f, 3.2f, 7.8f, 12.4f, 17f };
        for (int i = 0; i < loads.Length; i++)
            Boxed(loads[i] - .65f, 87.35f, loads[i] + .65f, 88.65f, i == 3 || i == 6 ? CrateTop : Crate, Dark, .08f);
        Row(93.5f, 3);                                                         // stack row 1
        // Timber crane yard: the stores, the rest boards, the north half-ring each load sweeps, the drums, the loads.
        Mirror(20.11f, 97.8f, 23.5f, 113.4f, Goods);
        for (float z = 98.2f; z < 113.4f; z += .8f) { Line(-23.5f, z, -20.11f, z, .05f, Dark); Line(20.11f, z, 23.5f, z, .05f, Dark); }
        Rect(-19.6f, 98.55f, -11.4f, 99.45f, Dark); Rect(-7.6f, 98.55f, 7.6f, 99.45f, Dark); Rect(11.4f, 98.55f, 19.6f, 99.45f, Dark);
        Annulus(-9.5f, 99f, 2f, 10.4f, 0f, 180f, Red); Annulus(9.5f, 99f, 2f, 10.4f, 0f, 180f, Red);
        Round(-9.5f, 99f, 1.2f, Wall, Edge); Round(9.5f, 99f, 1.2f, Wall, Edge);
        Boxed(-19.5f, 98.75f, -11.5f, 99.25f, Oak, Walnut, .08f); Boxed(-.5f, 98.75f, 7.5f, 99.25f, Oak, Walnut, .08f);  // both loads at rest, west
        // Loft hoists: four wells with their pallets, gold boarding edges, the posts and arms.
        Mirror(.9f, 110.97f, 3.27f, 113.37f, Oak); Mirror(3.73f, 110.97f, 6.1f, 113.37f, Oak);
        Mirror(.9f, 110.97f, 3.27f, 111.15f, Gold); Mirror(3.73f, 110.97f, 6.1f, 111.15f, Gold);
        Mirror(3.3f, 111.97f, 3.7f, 112.37f, Walnut);
        Line(-6.25f, 112.17f, -.75f, 112.17f, .25f, Walnut); Line(.75f, 112.17f, 6.25f, 112.17f, .25f, Walnut);
        // The loft: the deck in the middle with its gold south lip and north edge, the high ends with crate stacks.
        Rect(-12f, 113.4f, 12f, 116.6f, Oak);
        for (float x = -11.5f; x < 12f; x += 1f) Line(x, 113.4f, x, 116.6f, .05f, Walnut);
        Rect(-12f, 113.4f, 12f, 113.58f, Gold); Rect(-12f, 116.42f, 12f, 116.6f, Gold);
        Mirror(12f, 113.4f, 23.5f, 116.6f, Goods);
        for (int k = 0; k < 5; k++) Mirror(12.4f + 2.2f * k, 114.1f, 14.2f + 2.2f * k, 115.9f, Crate);
        Row(122f, 6);                                                          // stack row 2
        // The heavy berth: four marks, the warning circle round the target, the coffer over it, the gantry overhead.
        foreach (float x in new[] { -13.5f, -4.5f, 4.5f, 13.5f })
            Boxed(x - 1.95f, 123.85f, x + 1.95f, 126.95f, Dark, Iron, .12f);
        Disc(-4.5f, 125.4f, 6f, Red);
        Boxed(-6.1f, 124.2f, -2.9f, 126.6f, Oak, Walnut, .15f);
        Line(-27f, 124.2f, 27f, 124.2f, .4f, Walnut); Line(-27f, 126.6f, 27f, 126.6f, .4f, Walnut);
        Line(-27f, 125.4f, 27f, 125.4f, .15f, Iron);
        // The apron: lamps, checkpoint 3, the gold pond edge.
        foreach (float x in new[] { -21.5f, -8.5f, 8.5f, 21.5f }) Lamp(x, 131.95f);
        Band(-26f, 26f, 131f, 132f);
        Rect(-26f, 132.6f, 26f, 132.85f, Gold);

        // ---------------- mill pond, paddle wheel and marble cliff (z 132.85 .. 143.6)
        Rect(-26f, 132.85f, 26f, 143f, Water);
        foreach (float s in new[] { -1f, 1f })
        {
            Rect(Math.Min(s * 10.9f, s * 20.1f), 138.2f, Math.Max(s * 10.9f, s * 20.1f), 141.4f, Walnut);   // barge
            Rect(Math.Min(s * 11.5f, s * 19.5f), 138.6f, Math.Max(s * 11.5f, s * 19.5f), 141f, Oak);
            Boxed(Math.Min(s * 13f, s * 16.5f), 139f, Math.Max(s * 13f, s * 16.5f), 140.6f, Goods, Dark, .08f);
            Rect(Math.Min(s * 13.5f, s * 17.5f), 142.2f, Math.Max(s * 13.5f, s * 17.5f), 143f, Fall);       // sluice pour
        }
        Mirror(2f, 136.5f, 3f, 137.5f, Wall);                                   // the paddle wheel's piers
        Rect(-3.5f, 141.4f, 3.5f, 143f, Oak);                                   // the landing at the cliff top
        Rect(-3.5f, 141.4f, 3.5f, 141.58f, Gold);
        Rect(-1.5f, 131.8f, 1.5f, 142.2f, Oak);                                 // the paddle wheel, from above
        Rect(-1.5f, 131.8f, -1.3f, 142.2f, Walnut); Rect(1.3f, 131.8f, 1.5f, 142.2f, Walnut);
        foreach (float z in new[] { 131.95f, 133.32f, 137f, 140.68f, 142.05f }) Line(-1.3f, z, 1.3f, z, .18f, Gold);
        Rect(-5f, 142.6f, 5f, 143f, Fall);                                     // the waterfall behind the wheel
        Rect(-26f, 143f, 26f, 143.6f, Marble);                                 // the cliff's top
        Mirror(12.5f, 143f, 18.5f, 143.6f, Dark);                              // sluice portals in it
        Outline(Edge, .08f, Quad(-26f, 143f, 26f, 143.6f));

        // ---------------- platform, final gate and the section's end (z 143.6 .. 181)
        Rect(-26f, 143.6f, 26f, 159f, Floor);
        Rect(-26f, 143.6f, -3.5f, 144f, Marble); Rect(3.5f, 143.6f, 26f, 144f, Marble);   // the cliff-top railing
        Band(-26f, 26f, 145.5f, 146.5f);                                        // checkpoint 4
        Lamp(-8f, 144.5f); Lamp(8f, 144.5f);
        foreach (float s in new[] { -1f, 1f })
        {
            Disc(s * 17.7f, 144.4f, .35f, Walnut);                              // dock crane post and its arm over the barge
            Line(s * 17.7f, 144.4f, s * 16.4f, 139.6f, .3f, Walnut);
        }
        Rect(-27f, 155f, -4f, 157f, Wall); Rect(4f, 155f, 27f, 157f, Wall);
        Rect(-4f, 155.25f, 0f, 155.75f, Walnut); Rect(0f, 155.25f, 4f, 155.75f, Walnut);  // the push gate, closed
        Rect(-.7f, 155.05f, -.2f, 155.25f, Gold); Rect(.2f, 155.05f, .7f, 155.25f, Gold); // its ring handles
        Round(-6.6f, 156f, 1.8f, Wall, Edge); Round(6.6f, 156f, 1.8f, Wall, Edge);
        Round(-28f, 159f, 2f, Wall, Edge); Round(28f, 159f, 2f, Wall, Edge);
        Rect(-6f, 159f, 6f, 181f, Floor);                                      // causeway, terrace, steps, entrance band
        Mirror(6f, 157f, 6.3f, 174.6f, Marble); Mirror(6f, 175.4f, 6.3f, 181f, Marble);
        Band(-6f, 6f, 171.5f, 172.5f);                                          // checkpoint 5
        Mirror(6f, 174.6f, 7f, 175.4f, Wall);                                   // the open portal's piers
        Line(-7f, 175f, 7f, 175f, .5f, Edge);                                   // its lintel, overhead
        for (int k = 1; k <= 4; k++) Line(-6f, 175f + .5f * k, 6f, 175f + .5f * k, .06f, Dark);
    }

    public static void Render(string path, float xmin, float xmax, float zmin, float zmax, float scale)
    {
        const int margin = 12;
        int w = (int)Math.Round((xmax - xmin) * scale) + 2 * margin, h = (int)Math.Round((zmax - zmin) * scale) + 2 * margin;
        using (var bmp = new Bitmap(w, h))
        {
            g = Graphics.FromImage(bmp);
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Parchment);
                g.SetClip(new Rectangle(margin, margin, w - 2 * margin, h - 2 * margin));
                g.Transform = new Matrix(scale, 0f, 0f, -scale, margin - xmin * scale, margin + zmax * scale);
                Draw();
            }
            finally { g.Dispose(); }
            bmp.Save(path, ImageFormat.Png);
        }
    }

    // Cuts rectangles (x, y, w, h in source pixels) out of `source` and lays them side by side on its own background.
    public static void Crop(string source, string path, int[][] parts)
    {
        using (var src = new Bitmap(source))
        {
            int gap = 24, w = gap, h = 0;
            foreach (var p in parts) { w += p[2] + gap; h = Math.Max(h, p[3]); }
            using (var bmp = new Bitmap(w, h + 2 * gap))
            using (var gr = Graphics.FromImage(bmp))
            {
                gr.Clear(src.GetPixel(4, 4));
                int x = gap;
                foreach (var p in parts)
                {
                    gr.DrawImage(src, new Rectangle(x, gap, p[2], p[3]), new Rectangle(p[0], p[1], p[2], p[3]), GraphicsUnit.Pixel);
                    x += p[2] + gap;
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
'@

$Out = [System.IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force $Out | Out-Null
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))

# Layout maps: the whole course (portrait) and four parts, x -30..30, far end at the top.
[RcyMap]::Render((Join-Path $Out 'RCY_map_course.png'),     -30, 30, -18, 183, 7.5)
[RcyMap]::Render((Join-Path $Out 'RCY_map_wagon_yard.png'), -30, 30, -18, 60, 14)
[RcyMap]::Render((Join-Path $Out 'RCY_map_canal.png'),      -30, 30, 46, 86, 16)
[RcyMap]::Render((Join-Path $Out 'RCY_map_warehouse.png'),  -30, 30, 80, 135, 14)
[RcyMap]::Render((Join-Path $Out 'RCY_map_pond_gate.png'),  -30, 30, 126, 183, 14)

# The pawn as it looks in the game (charcoal and cream, unity_look.png) and its proportions (lineup_arms.png).
$look = Join-Path $root 'RigWork\pieces\tripo_rigged\unity_look.png'
$lineup = Join-Path $root 'RigWork\pieces\tripo_rigged\lineup_arms.png'
[RcyMap]::Crop($look, (Join-Path $Out 'RCY_ref_pawns_in_game.png'), [int[][]]@(@(950, 60, 300, 410), @(2380, 60, 220, 410)))
[RcyMap]::Crop($lineup, (Join-Path $Out 'RCY_ref_pawn_front.png'), [int[][]]@(,@(30, 170, 355, 486)))

Get-ChildItem $Out -Filter 'RCY_*.png' | ForEach-Object { '{0}  {1} bytes' -f $_.Name, $_.Length }
