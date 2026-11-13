using UnityEngine;

namespace ChessFight.PawnRush
{
    // The war room's colours (승규 님's endgame concept art, 10-30, R244): cream marble floors with gold lines, teal rails
    // and bridge beams with gold caps, walnut map tables and bookshelves, blue banners, warm lamps. Lit materials made once
    // (Arena.Lit caches them) and saved in the scene with the zone, like the mission arenas'.
    public static class WarRoomLook
    {
        public static Material Marble => Arena.Lit("M_WR_Marble", Arena.Hex("#EFE6D4"), 0f, .35f);
        public static Material Inlay => Arena.Lit("M_WR_Inlay", Arena.Hex("#C9A44E"), 0f, .3f);
        public static Material Gold => Arena.Lit("M_WR_Gold", Arena.Hex("#D2A63E"), .15f, .3f);
        public static Material Teal => Arena.Lit("M_WR_Teal", Arena.Hex("#2F6B70"), 0f, .45f);
        public static Material Wood => Arena.Lit("M_WR_Wood", Arena.Hex("#7A5432"), 0f, .6f);
        public static Material Map => Arena.Lit("M_WR_Map", Arena.Hex("#6F8E80"), 0f, .7f);
        public static Material Stone => Arena.Lit("M_WR_Stone", Arena.Hex("#E3D7C0"), 0f, .8f);
        public static Material Banner => Arena.Lit("M_WR_Banner", Arena.Hex("#2C4F86"), 0f, .8f);
        public static Material Deep => Arena.Lit("M_WR_Deep", Arena.Hex("#5A4836"), 0f, .9f);
        public static Material Lamp => Arena.Emissive(Arena.Hex("#FFC978"), 2f);

        static readonly string[] BookColours = { "#8E3B2E", "#34507A", "#4E6B45", "#B49464", "#6B3F5E" };
        public static Material Book(int i) => Arena.Lit("M_WR_Book" + i % BookColours.Length, Arena.Hex(BookColours[i % BookColours.Length]), 0f, .7f);

        // Every renderer under `go` in one material.
        public static GameObject Paint(GameObject go, Material material)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
            return go;
        }
    }
}
