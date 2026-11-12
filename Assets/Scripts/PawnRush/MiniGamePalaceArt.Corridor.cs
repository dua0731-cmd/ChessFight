using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class MiniGamePalaceArt
    {
        // R517: the pictures of a corridor between two sections of a round (SectionCorridor owns its bodies and calls this).
        // Corridor frame: the way runs +z from 0 to `length`, |x| < 6 is walked, the walls' bodies stand at |x| 6..6.6 from
        // `from` to `to` (short of a mini-game's frames), `height` is the walls' height and a ceiling's underside.
        // `style` 0 early (open to the sky), 1 mid, 2 late (both ceilinged). `portalStart` / `portalEnd` put a gate frame
        // round an end that opens onto an obstacle section (a mini-game frames its own ends with its arches); `endHalf` > 0
        // closes the exit's line out to |x| endHalf (the war room's start floor is wider than the corridor).
        //
        // Nothing reaches into the walked width: the piers' faces stand on the bodies' faces (|x| 6) and the wall between
        // them 0.25 m further back; only the lamps (over 3 m up) and the plinths' 4 cm lips come forward. The floor's
        // picture is the mini-games' paving, 2.4 cm proud of the floor body (R547, CorridorFloor).
        public static void Corridor(Transform root, int style, float length, float from, float to, float height, float endHalf,
                                    bool portalStart, bool portalEnd)
        {
            bool early = style == 0, late = style == 2;
            var art = Group(root, "ENV_Corridor");
            CorridorFloor(art, length, from, to, endHalf > 0f, early, late);
            foreach (float side in new[] { -1f, 1f }) CorridorWall(art, side, from, to, height, early, late);
            if (!early) CorridorCeiling(art, length, from, to, height, late);
            if (portalStart) CorridorPortal(art, 0f, 1f, height, early, late);
            if (portalEnd) CorridorPortal(art, length, -1f, height, early, late);
            if (endHalf > 0f) CorridorEndWall(art, length, endHalf, height, early);
        }

        // Three bays a wall between four piers (a lamp, a banner, a lamp); the ceiling's cross beams rest on the piers.
        const int CorridorBays = 3;
        const float PierWidth = .9f;
        static float CorridorPier(float from, float to, int i) => Mathf.Lerp(from + PierWidth * .5f, to - PierWidth * .5f, i / (float)CorridorBays);

        // R547 (승규 11-11 "미니게임 바닥에 맞추기"): the paving is the mini-games' own (MiniGamePalaceArt.Paving, baked into
        // their prefabs): the same 1.97 m slabs 2.4 cm proud of the floor body, the same materials (early all limestone; mid
        // and late a chequer of marble and PavingCool), and the chequer carries on across the join instead of starting
        // afresh. A corridor touches at most one mini-game (the slots alternate obstacle, mini-game), so its phase is that
        // mini-game's: leaving one (`from` > 0) the first row continues its exit row, else the last row leads into its
        // entry row. The late runner is the mini-games' too and runs on to the arch; the mid mini-games have none, so
        // neither has a mid corridor. Over the ballroom's arch floor (top -0.004) the slabs stand 2.8 cm clear.
        static void CorridorFloor(Transform art, float length, float from, float to, bool closedEnd, bool early, bool late)
        {
            var floor = Group(art, "Corridor floor");
            // The bed under the paving, out to the walls' outer faces: the joints show it, not the sky.
            Box(floor, "Corridor bed", new Vector3(0f, -.2f, length * .5f), new Vector3(13.2f, .35f, length), Stone, false);
            var light = early ? Cream : Marble;
            // Called as MiniGamePalaceArt.Paving calls it, so it is the one material the baked mini-games hold.
            var dark = M("PavingCool", late ? "#A7AAA9" : "#A0A5A6", 0f, .82f);
            int rows = Mathf.CeilToInt(length / 2f - .01f);
            // The mini-game's row index of corridor row 0: its exit row 17 is followed by 18; its entry row 0 is led into
            // by rows -rows .. -1. Its columns are x*2+1 for x -11..10, so corridor column c (centre c*2-5) is its x c-3.
            int rowShift = from > 0f ? 18 : -rows;
            for (int x = 0; x < 6; x++)
                for (int z = 0; z < rows; z++)
                {
                    float z0 = z * 2f, z1 = Mathf.Min(length, z0 + 2f);
                    bool other = !early && (((x - 3) + (z + rowShift)) & 1) != 0;
                    Box(floor, "Paving module", new Vector3(x * 2f - 5f, .012f, (z0 + z1) * .5f), new Vector3(1.97f, .024f, z1 - z0 - .03f),
                        other ? dark : light, false);
                }
            if (!late) return;
            // Stopped only at the war room's line; at a mini-game it meets that room's own runner.
            float a = 0f, b = closedEnd ? length - .6f : length;
            float mid = (a + b) * .5f, run = b - a;
            Box(floor, "Central velvet runner", new Vector3(0f, .022f, mid), new Vector3(2.4f, .035f, run), Violet, false);
            foreach (float x in new[] { -1.24f, 1.24f })
                Box(floor, "Runner selvedge", new Vector3(x, .045f, mid), new Vector3(.07f, .018f, run), Brass, false);
        }

        static void CorridorWall(Transform art, float side, float from, float to, float height, bool early, bool late)
        {
            float run = to - from, mid = (from + to) * .5f;
            if (run <= 2f) return;
            var wall = Group(art, side < 0f ? "Corridor wall W" : "Corridor wall E", new Vector3(side * 6f, 0f, 0f));
            // d: how far behind the bodies' face (|x| 6) a part stands.
            Vector3 At(float d, float y, float z) => new Vector3(side * d, y, z);
            var face = Quaternion.Euler(0f, side * 90f, 0f);   // a banner's or a lamp's back (+z) to the wall
            Box(wall, "Wall mass", At(.425f, height * .5f, mid), new Vector3(.35f, height, run), early ? Stone : late ? Marble : Cream);
            Box(wall, "Wall skirting", At(.125f, .075f, mid), new Vector3(.25f, .15f, run), Stone);
            for (int bay = 0; bay < CorridorBays; bay++)
            {
                float z0 = CorridorPier(from, to, bay) + PierWidth * .5f, z1 = CorridorPier(from, to, bay + 1) - PierWidth * .5f;
                float zc = (z0 + z1) * .5f, span = z1 - z0;
                if (early) CorridorAshlar(wall, side, z0, z1, height);
                else
                {
                    Box(wall, "Wainscot", At(.21f, 1.2f, zc), new Vector3(.08f, 2.4f, span), late ? M("LowMarble", "#9E9E99") : Wood);
                    float inset = (span - .45f) * .5f;
                    foreach (float k in new[] { -1f, 1f })
                        Box(wall, "Wainscot inset", At(.16f, 1.25f, zc + k * (inset * .5f + .075f)), new Vector3(.02f, 1.75f, inset),
                            late ? Marble : M("WalnutInset", "#76523A", 0f, .9f, "PR_Wood"));
                    Box(wall, "Wainscot cap", At(.195f, 2.46f, zc), new Vector3(.11f, .12f, span), Brass);
                    Box(wall, "Wall cornice", At(.15f, height - .3f, zc), new Vector3(.2f, .3f, span), Stone);
                }
                if (bay == 1)
                {
                    // Hung on the face the pawns see: the ashlar's (0.13 back) on the early wall, the plaster's (0.25) else.
                    var banner = Group(wall, "Corridor banner", At(early ? .065f : .18f, early ? height - .5f : height - 1f, zc));
                    banner.localRotation = face;
                    // Darker than the wall behind it: crimson on the limestone, navy on the plaster, purple on the marble.
                    Banner(banner, Vector3.zero, 1.3f, early ? 2.2f : 2.8f,
                           early ? M("BannerCrimson", "#8A2E2E", 0f, .95f) : late ? Violet : M("EmblemNavy", "#344350"), true);
                    continue;
                }
                var lamp = Group(wall, "Corridor lamp", At(.07f, early ? 3.1f : 3.4f, zc));
                lamp.localRotation = face;
                Sconce(lamp, Vector3.zero);
                if (late) CorridorWindow(wall, side, zc, height);
                else if (!early) CorridorPortrait(wall, side, zc, height);
            }
            for (int i = 0; i <= CorridorBays; i++)
            {
                float z = CorridorPier(from, to, i);
                Box(wall, "Pier plinth", At(.28f, .3f, z), new Vector3(.64f, .6f, 1.1f), Stone);
                if (early)
                {
                    Box(wall, "Pier shaft", At(.3f, height * .5f + .2f, z), new Vector3(.6f, height + .4f, PierWidth), Cream);
                    Box(wall, "Pier cap", At(.3f, height + .5f, z), new Vector3(.7f, .2f, 1.05f), Stone);
                }
                else
                {
                    Box(wall, "Pier shaft", At(.3f, height * .5f, z), new Vector3(.6f, height, .8f), Marble);
                    Box(wall, "Pier capital", At(.27f, height - .75f, z), new Vector3(.66f, .4f, 1.05f), Stone);
                    Box(wall, "Pier brass fillet", At(.29f, height - 1f, z), new Vector3(.62f, .08f, .9f), Brass);
                }
            }
            CorridorOuterFace(wall, side, from, to, height, early, late);
            if (!early) return;
            // The open-air wall's walk: a coping and battlements along the top.
            Box(wall, "Wall coping", At(.3f, height - .1f, mid), new Vector3(.66f, .2f, run), Stone);
            for (float z = from + .35f; z < to - .3f; z += 1.4f)
                Box(wall, "Battlement", At(.35f, height + .35f, Mathf.Min(z + .35f, to - .35f)), new Vector3(.5f, .7f, .7f), Cream);
        }

        // R547 (승규 11-11 "벽 부분 자연스럽게"): the wall's outer face, which a neighbour section shows across its open
        // ground or hall (the garden's yard, the stair hall's terrace, the temporary floor, a mini-game's courtyard).
        // It was the bare wall mass and, mid and late, the ceiling slab's raw edge on top: a box set down between the
        // sections. Now it is masonry like theirs: running-bond ashlar (limestone early and mid, marble late) between piers
        // standing on the inner piers' lines, a stone plinth, and on a ceilinged corridor a cornice over the slab's edge and
        // a parapet along the roof. All of it stands outside |x| 6.6 (the bodies) and between `from` and `to`, so nothing
        // reaches into a section's walls at the ends (a mini-game's end walls, an obstacle end's gate frame).
        static void CorridorOuterFace(Transform wall, float side, float from, float to, float height, bool early, bool late)
        {
            float run = to - from, mid = (from + to) * .5f;
            Vector3 At(float d, float y, float z) => new Vector3(side * d, y, z);
            var stone = late ? Marble : Cream;
            float top = early ? height - .2f : height;
            Box(wall, "Outer plinth", At(.68f, .3f, mid), new Vector3(.16f, .6f, run), Stone);
            for (int bay = 0; bay < CorridorBays; bay++)
            {
                float z0 = CorridorPier(from, to, bay) + PierWidth * .5f, z1 = CorridorPier(from, to, bay + 1) - PierWidth * .5f;
                CorridorAshlar(wall, side, z0, z1, top, .66f, stone, .6f);
            }
            for (int i = 0; i <= CorridorBays; i++)
            {
                float z = CorridorPier(from, to, i);
                Box(wall, "Outer pier", At(.74f, top * .5f, z), new Vector3(.28f, top, PierWidth), stone);
                Box(wall, "Outer pier plinth", At(.76f, .35f, z), new Vector3(.34f, .7f, 1.05f), Stone);
                if (!early) Box(wall, "Outer pier capital", At(.75f, height - .55f, z), new Vector3(.32f, .4f, 1.05f), Stone);
            }
            if (early) return;
            // Over the ceiling slab's edge (height .. height + 0.3) and along the roof.
            Box(wall, "Outer cornice", At(.72f, height + .15f, mid), new Vector3(.3f, .42f, run), Stone);
            Box(wall, "Roof parapet", At(.42f, height + .6f, mid), new Vector3(.36f, .6f, run), stone);
            Box(wall, "Roof parapet coping", At(.42f, height + .95f, mid), new Vector3(.46f, .1f, run), Stone);
        }

        // Running-bond ashlar on a wall face between two piers, 0.76 m courses up to under the coping: the early wall's
        // inner face by default (0.19 behind the bodies' face), or the outer face (R547) from `bottom` up.
        static void CorridorAshlar(Transform wall, float side, float z0, float z1, float top, float d = .19f, Material stone = null, float bottom = .15f)
        {
            if (stone == null) stone = Cream;
            float span = z1 - z0;
            int n = Mathf.Max(1, Mathf.RoundToInt(span / 1.1f));
            float unit = span / n;
            int course = 0;
            for (float y = bottom; y < top - .45f; y += .76f, course++)
            {
                float h = Mathf.Min(.72f, top - .24f - y);
                float shift = (course & 1) == 1 ? unit * .5f : 0f;
                for (float z = z0 - shift; z < z1 - .05f; z += unit)
                {
                    float a = Mathf.Max(z0, z) + .02f, b = Mathf.Min(z1, z + unit) - .02f;
                    if (b - a > .12f) Box(wall, "Ashlar face", new Vector3(side * d, y + h * .5f, (a + b) * .5f), new Vector3(.12f, h, b - a), stone);
                }
            }
        }

        // A royal pawn's portrait in a brass frame over a mid hallway's lamp.
        static void CorridorPortrait(Transform wall, float side, float z, float height)
        {
            float y = Mathf.Min(4.9f, height - 1.6f);
            Box(wall, "Portrait frame", new Vector3(side * .22f, y, z), new Vector3(.06f, 1.7f, 1.35f), Brass);
            Box(wall, "Portrait canvas", new Vector3(side * .18f, y, z), new Vector3(.04f, 1.45f, 1.1f), M("PortraitCanvas", "#4E3F35", 0f, .95f));
            Ball(wall, "Portrait pawn head", new Vector3(side * .155f, y + .3f, z), .16f, Cloth);
            Box(wall, "Portrait pawn body", new Vector3(side * .155f, y - .25f, z), new Vector3(.02f, .55f, .42f), Cloth);
        }

        // A tall window with tied curtains in a late gallery's bay, over the lamp.
        static void CorridorWindow(Transform wall, float side, float z, float height)
        {
            float y0 = 4f, y1 = height - 1.15f, y = (y0 + y1) * .5f, h = y1 - y0;
            var window = Group(wall, "Corridor window", new Vector3(side * .2f, y, z));
            Box(window, "Window glass", new Vector3(side * .025f, 0f, 0f), new Vector3(.05f, h, 1.2f), Glass);
            Box(window, "Window mullion", new Vector3(-side * .01f, 0f, 0f), new Vector3(.06f, h, .08f), Marble);
            Box(window, "Window transom", new Vector3(-side * .01f, h * .2f, 0f), new Vector3(.06f, .08f, 1.2f), Marble);
            foreach (float k in new[] { -1f, 1f })
                Box(window, "Window jamb", new Vector3(-side * .01f, 0f, k * .64f), new Vector3(.06f, h + .1f, .1f), Marble);
            Box(window, "Window head", new Vector3(-side * .01f, h * .5f + .08f, 0f), new Vector3(.06f, .16f, 1.38f), Marble);
            foreach (float k in new[] { -1f, 1f })
            {
                Box(window, "Tied royal curtain", new Vector3(-side * .1f, .1f, k * .82f), new Vector3(.1f, h + .2f, .32f), Violet);
                Box(window, "Curtain tie", new Vector3(-side * .11f, -h * .2f, k * .82f), new Vector3(.12f, .12f, .36f), Brass);
            }
        }

        static void CorridorCeiling(Transform art, float length, float from, float to, float height, bool late)
        {
            var ceiling = Group(art, "Corridor ceiling");
            float run = to - from, mid = (from + to) * .5f;
            var stone = late ? Marble : Cream;
            Box(ceiling, "Ceiling slab", new Vector3(0f, height + .15f, mid), new Vector3(13.2f, .3f, run), stone);
            for (int i = 0; i <= CorridorBays; i++)
                Box(ceiling, "Ceiling beam", new Vector3(0f, height - .25f, CorridorPier(from, to, i)), new Vector3(12.2f, .5f, .5f), late ? Marble : Wood);
            for (int bay = 0; bay < CorridorBays; bay++)
            {
                float z0 = CorridorPier(from, to, bay) + .25f, z1 = CorridorPier(from, to, bay + 1) - .25f, zc = (z0 + z1) * .5f, span = z1 - z0;
                if (late)
                {
                    // A skylight between the beams in brass frames (a picture: the slab above stays the lid).
                    Box(ceiling, "Skylight glazing", new Vector3(0f, height - .03f, zc), new Vector3(3.4f, .06f, span - .3f), Glass);
                    Box(ceiling, "Skylight frame", new Vector3(0f, height - .08f, zc), new Vector3(.1f, .1f, span - .3f), Brass);
                    foreach (float x in new[] { -1.76f, 1.76f })
                        Box(ceiling, "Skylight frame", new Vector3(x, height - .08f, zc), new Vector3(.12f, .14f, span), Brass);
                }
                else
                {
                    foreach (float x in new[] { -3f, 3f })
                        Box(ceiling, "Ceiling length beam", new Vector3(x, height - .15f, zc), new Vector3(.3f, .3f, span), Wood);
                    if (bay != 1) Chandelier(ceiling, new Vector3(0f, height - 2.7f, zc));
                }
            }
            // A header over an end on a mini-game's arch: the arch rises higher than this ceiling, so through its top the
            // room looks onto wall, not sky.
            const float HeaderTop = 10.5f;
            float headerHeight = HeaderTop - height - .3f, headerY = height + .3f + headerHeight * .5f;
            if (from > 0f) Box(ceiling, "Corridor end header", new Vector3(0f, headerY, from + .15f), new Vector3(13.2f, headerHeight, .3f), stone);
            if (to < length) Box(ceiling, "Corridor end header", new Vector3(0f, headerY, to - .15f), new Vector3(13.2f, headerHeight, .3f), stone);
            // Warm light in the lamps' bays (the ceiling keeps the sun out).
            foreach (int bay in new[] { 0, 2 })
            {
                var lamp = new GameObject("Corridor light");
                lamp.transform.SetParent(ceiling, false);
                lamp.transform.localPosition = new Vector3(0f, height - 2.4f, (CorridorPier(from, to, bay) + CorridorPier(from, to, bay + 1)) * .5f);
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 10f;
                light.intensity = 1.5f;
                light.color = new Color(1f, .82f, .6f);
                light.shadows = LightShadows.None;
            }
        }

        // A gate frame round an end that opens onto an obstacle section: piers either side out past the walls, a lintel
        // over the way, battlements on it (early) or a crown (mid, late). `inward` +1 at the entry (the frame stands on
        // 0..0.8), -1 at the exit.
        static void CorridorPortal(Transform art, float z, float inward, float height, bool early, bool late)
        {
            var portal = Group(art, "Corridor portal", new Vector3(0f, 0f, z + inward * .4f));
            var stone = early ? Cream : Marble;
            foreach (float side in new[] { -1f, 1f })
            {
                Box(portal, "Portal pier", new Vector3(side * 7f, (height + .9f) * .5f, 0f), new Vector3(2f, height + .9f, .8f), stone);
                Box(portal, "Portal pier plinth", new Vector3(side * 7.05f, .35f, 0f), new Vector3(2.2f, .7f, .95f), Stone);
            }
            Box(portal, "Portal lintel", new Vector3(0f, height + .45f, 0f), new Vector3(16f, .9f, .8f), stone);
            Box(portal, "Portal cornice", new Vector3(0f, height + .97f, 0f), new Vector3(16.4f, .15f, 1f), Stone);
            // R547: the frame was three plain blocks beside the section's own dressed stone; now coursed like the walls,
            // on both faces: the piers in running bond with quoins at their edges, the lintel in voussoir-like blocks
            // round a keystone.
            foreach (float face in new[] { -1f, 1f })
            {
                float zf = face * .43f;
                foreach (float side in new[] { -1f, 1f })
                    PortalCourses(portal, side * 6.05f, side * 7.95f, .75f, height + .05f, zf, stone);
                const int Blocks = 11;
                for (int i = 0; i < Blocks; i++)
                {
                    float x0 = -7.95f + i * 15.9f / Blocks + .025f, x1 = -7.95f + (i + 1) * 15.9f / Blocks - .025f;
                    Box(portal, "Portal lintel block", new Vector3((x0 + x1) * .5f, height + .45f, zf), new Vector3(x1 - x0, .8f, .06f), stone);
                }
                Box(portal, "Portal keystone", new Vector3(0f, height + .4f, face * .47f), new Vector3(.8f, 1f, .1f), Stone);
            }
            if (early)
            {
                for (int i = -5; i <= 5; i++)
                    Box(portal, "Battlement", new Vector3(i * 1.5f, height + 1.4f, 0f), new Vector3(.75f, .75f, .7f), Cream);
                return;
            }
            Box(portal, "Portal emblem back", new Vector3(0f, height + .45f, -inward * .42f), new Vector3(2.6f, .72f, .05f), late ? Violet : M("EmblemNavy", "#344350"));
            Crown(portal, new Vector3(0f, height + 1.17f, 0f), .7f);
        }

        // Running-bond courses (0.6 m) on one face (z `zf`) of a portal pier between x `x0` and `x1` (either order), from
        // `y0` to `y1`; a course's end blocks alternate long and short at both edges, as quoins do.
        static void PortalCourses(Transform portal, float x0, float x1, float y0, float y1, float zf, Material stone)
        {
            float a = Mathf.Min(x0, x1), b = Mathf.Max(x0, x1);
            int course = 0;
            for (float y = y0; y < y1 - .3f; y += .6f, course++)
            {
                float h = Mathf.Min(.56f, y1 - y - .04f);
                float cut = (course & 1) == 0 ? .75f : 1.15f;   // one joint a course, alternating: quoins at both edges
                Box(portal, "Portal ashlar", new Vector3((a + a + cut) * .5f, y + h * .5f, zf), new Vector3(cut - .04f, h, .06f), stone);
                Box(portal, "Portal ashlar", new Vector3((a + cut + b) * .5f, y + h * .5f, zf), new Vector3(b - a - cut - .04f, h, .06f), stone);
            }
        }

        // Across the exit's line out to |x| endHalf, either side of the way, and over it: the war room's start floor
        // opens wider than the corridor, and its south edge stood over the mini-game's end wall before R517. Seen from the
        // room: piers on the line the bodies stop at (z = length), the wall 0.2 m behind them, banners, a crown over the way.
        static void CorridorEndWall(Transform art, float length, float endHalf, float height, bool early)
        {
            float top = height + 4f;
            var stone = early ? Cream : Marble;
            var fabric = early ? M("BannerCrimson", "#8A2E2E", 0f, .95f) : Violet;
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side * 6.6f, x1 = side * endHalf, w = Mathf.Abs(x1 - x0);
                var end = Group(art, side < 0f ? "Corridor end wall W" : "Corridor end wall E", new Vector3((x0 + x1) * .5f, 0f, length));
                Box(end, "End wall mass", new Vector3(0f, (top - 3f) * .5f, -.4f), new Vector3(w, top + 3f, .4f), stone);
                Box(end, "End wall cornice", new Vector3(0f, top - .2f, -.3f), new Vector3(w, .3f, .7f), Stone);
                Box(end, "End wall skirting", new Vector3(0f, .1f, -.15f), new Vector3(w, .2f, .1f), Stone);
                foreach (float k in new[] { 0f, .5f, 1f })
                {
                    float x = Mathf.Lerp(-w * .5f + .45f, w * .5f - .45f, k);
                    Box(end, "End wall pier", new Vector3(x, top * .5f, -.3f), new Vector3(.9f, top, .6f), stone);
                    Box(end, "End wall pier plinth", new Vector3(x, .3f, -.28f), new Vector3(1.1f, .6f, .64f), Stone);
                    Box(end, "End wall pier capital", new Vector3(x, top - .75f, -.27f), new Vector3(1.05f, .4f, .66f), Stone);
                }
                foreach (float k in new[] { .25f, .75f })
                {
                    var banner = Group(end, "End wall banner", new Vector3(Mathf.Lerp(-w * .5f + .45f, w * .5f - .45f, k), height + 1f, -.14f));
                    banner.localRotation = Quaternion.Euler(0f, 180f, 0f);   // its front to the room (+z)
                    Banner(banner, Vector3.zero, 1.6f, 3.4f, fabric, true);
                }
            }
            Box(art, "Corridor end lintel", new Vector3(0f, (height + .3f + top) * .5f, length - .4f), new Vector3(13.2f, top - height - .3f, .4f), stone);
            Box(art, "Corridor end emblem back", new Vector3(0f, height + 1.75f, length - .17f), new Vector3(3f, 1.7f, .05f), fabric);
            Crown(art, new Vector3(0f, height + 1.55f, length - .12f), .9f);
        }
    }
}
