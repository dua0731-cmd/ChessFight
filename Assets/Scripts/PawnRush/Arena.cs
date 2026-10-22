using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The mini-game arena's look (mini-games v1.0 §2.1): every station is a dais 0.4 m over the course floor in a
    // dark slate with a gold rim, reached by a ramp at its entrance between two flags in the team's colour, with
    // the game's sign over the entrance and a progress bar over the team gate. Game parts are warm wood or
    // slate squares; gold marks what to grab, stand on or fit; the team colours mark each team's things.
    //
    // Materials are made here at run time (and in the Editor when the course is built), named as the spec names
    // them (M_ArenaDais, M_ArenaTrim_Gold ...). The spec's prefabs (PF_ArenaDais, PF_ArenaSign,
    // PF_GateProgressBar) are built from code like the rest of the course.
    public static class Arena
    {
        public const float DaisHeight = .4f, TrimWidth = .22f, TrimHeight = .1f, RampWidth = 4.4f, RampLength = 1.6f;

        public static readonly Color Dais = Hex("#2C303B"), Trim = Hex("#D9A83A"), Gold = Hex("#E2B23C"), Red = Hex("#E0483B");
        public static readonly Color Maple = Hex("#E3C391"), Walnut = Hex("#6B4428"), Slate = Hex("#4A5060"), Groove = Hex("#1C1F26");
        // Team accents. Were the spec's sky blue / orange placeholders (§2.1 "[확인]"); 10-17 the map kit takes
        // concept art 2's team colours: royal blue for white, crimson for black (deeper than the warning Red above,
        // which flashes, so the two stay apart).
        public static readonly Color White = Hex("#3D7BF0"), Black = Hex("#B8233A");

        public static Color TeamColor(int team) => team == Teams.Black ? Black : White;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        // Map-kit look (concept art 2, 10-17): the stations' structural surfaces take the course kit's palette
        // (cream block stone, glossy black/white tiles, navy signs). Only surfaces: team colours, warnings,
        // paint, dots and anything a game reads by colour keep the spec's colours above.
        static readonly Dictionary<string, (Color color, float roughness)> KitLook = new Dictionary<string, (Color, float)>
        {
            { "M_ArenaDais", (new Color(.80f, .73f, .60f), .85f) },
            { "M_PillarStone", (new Color(.80f, .73f, .60f), .8f) },
            { "M_LightStone", (new Color(.84f, .78f, .66f), .75f) },
            { "M_MoatStone", (new Color(.64f, .58f, .48f), .85f) },
            { "M_TowerBase", (new Color(.84f, .78f, .66f), .75f) },
            { "M_Curb", (new Color(.84f, .78f, .66f), .75f) },
            { "M_KnightPad", (new Color(.84f, .78f, .66f), .6f) },
            { "M_CheckBoardLight", (new Color(.80f, .79f, .76f), .3f) },
            { "M_CheckBoardDark", (new Color(.10f, .10f, .13f), .2f) },
            { "M_SignBack", (new Color(.13f, .22f, .52f), .7f) },
            { "M_BarGroove", (new Color(.10f, .16f, .38f), .7f) },
            // the pieces as a real tournament set (10-22): lacquered boxwood and ebony
            { "M_PieceWhite", (new Color(.9f, .81f, .63f), .22f) },
            { "M_PieceBlack", (new Color(.1f, .075f, .06f), .2f) },
        };

        // Real surfaces for the same materials (승규 10-22 "사실적으로"): sand with ripples and grains, wood with
        // grain and rings, block stone with grit. Texture set (MapLook, Resources), tiles per face, bump.
        static readonly Dictionary<string, (string set, float tiling, float bump)> KitSurface = new Dictionary<string, (string, float, float)>
        {
            { "M_Sand", ("PR_Sand", 3f, 1f) }, { "M_SandRipple", ("PR_Sand", 2f, 1f) },
            { "M_Maple", ("PR_Wood", 1f, .6f) }, { "M_Walnut", ("PR_Wood", 1f, .6f) }, { "M_Runner", ("PR_Wood", 1f, .4f) },
            { "M_ArenaDais", ("PR_StoneGrain", 2f, .8f) }, { "M_PillarStone", ("PR_StoneGrain", 1f, .8f) },
            { "M_LightStone", ("PR_StoneGrain", 1f, .8f) }, { "M_MoatStone", ("PR_StoneGrain", 1f, .8f) },
            { "M_TowerBase", ("PR_StoneGrain", 1f, .8f) }, { "M_Curb", ("PR_StoneGrain", 1f, .8f) },
            { "M_KnightPad", ("PR_StoneGrain", 1f, .6f) },
            { "M_PieceWhite", ("PR_Boxwood", 1f, .25f) }, { "M_PieceBlack", ("PR_Ebony", 1f, .25f) },
        };

        // A lit material: colour, emission strength (0 = none) and roughness.
        public static Material Lit(string name, Color color, float emission = 0f, float roughness = .6f)
        {
            if (emission <= 0f && KitLook.TryGetValue(name, out var look)) (color, roughness) = look;
            string key = name + ColorUtility.ToHtmlStringRGBA(color) + emission + roughness;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = LitMaterials.NewLit(color, 1f - roughness);
            m.name = name;
            if (emission > 0f) LitMaterials.SetEmission(m, color * emission, realtimeGI: true);
            if (KitSurface.TryGetValue(name, out var surface)) MapLook.Surface(m, surface.set, surface.tiling, surface.bump);
            cache[key] = m;
            return m;
        }

        public static Material Emissive(Color color, float emission) => Lit("M_Gold", color, emission, .35f);
        public static Material DaisMaterial => Lit("M_ArenaDais", Dais, 0f, .95f);
        public static Material TrimGold => Lit("M_ArenaTrim_Gold", Trim, .35f, .4f);
        public static Material TrimTeam(int team) => team == Teams.Black ? Lit("M_ArenaTrim_Black", Hex("#15110F"), 0f, .5f) : Lit("M_ArenaTrim_White", Hex("#F3EADB"), 0f, .5f);
        public static Material Wood(bool maple) => maple ? Lit("M_Maple", Maple, 0f, .7f) : Lit("M_Walnut", Walnut, 0f, .7f);

        // An unlit, see-through colour for effects (rings, beams, bits).
        public static Material Glow(Color color)
        {
            string key = "glow" + ColorUtility.ToHtmlStringRGBA(color);
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            var shader = Shader.Find("Sprites/Default");
            m = new Material(shader != null ? shader : LitMaterials.Unlit) { name = "M_Glow", color = color };
            cache[key] = m;
            return m;
        }

        // A box with no collider (a picture), in `parent`'s frame.
        public static GameObject Picture(Transform parent, string name, Vector3 min, Vector3 max, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * .5f;
            go.transform.localScale = max - min;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        // The gold rim along a floor segment's outer edges (§2.1): west and east always, south at the station's
        // entrance, north at its gate. In the game's frame (floor top at 0).
        public static void Rim(CourseBuilder b, float u0, float u1)
        {
            float h = MissionStation.Half, w = TrimWidth;
            var gold = TrimGold;
            Picture(b.parent, "Rim W", new Vector3(-h, 0f, u0), new Vector3(-h + w, TrimHeight, u1), gold);
            Picture(b.parent, "Rim E", new Vector3(h - w, 0f, u0), new Vector3(h, TrimHeight, u1), gold);
            if (u0 <= .01f)
            {
                // Not across the ramp: the way in stays clear.
                float r = RampWidth * .5f;
                Picture(b.parent, "Rim S W", new Vector3(-h, 0f, 0f), new Vector3(-r, TrimHeight, w), gold);
                Picture(b.parent, "Rim S E", new Vector3(r, 0f, 0f), new Vector3(h, TrimHeight, w), gold);
            }
            if (u1 >= MissionStation.Size - .01f)
            {
                Picture(b.parent, "Rim N W", new Vector3(-h, 0f, MissionStation.Size - w), new Vector3(-2.2f, TrimHeight, MissionStation.Size), gold);
                Picture(b.parent, "Rim N E", new Vector3(2.2f, 0f, MissionStation.Size - w), new Vector3(h, TrimHeight, MissionStation.Size), gold);
            }
        }

        // What every station has whatever its game (in the station's frame, plaza floor at 0): the ramp and its
        // flags, the sign over the entrance, the progress bar over the gate. Built once; the sign's words change
        // with the game.
        public static ArenaDressing Dress(MissionStation station)
        {
            var root = new GameObject("Arena dressing");
            root.transform.SetParent(station.transform, false);
            var b = new CourseBuilder(station.Kit, root.transform);
            float r = RampWidth * .5f;
            // The ramp: plaza floor up to the dais, just outside the entrance.
            var ramp = b.Ramp("Arena ramp", -r, r, -RampLength, 0f, 0f, DaisHeight, .5f);
            ramp.GetComponent<Renderer>().sharedMaterial = DaisMaterial;
            Picture(root.transform, "Ramp rim W", new Vector3(-r - TrimWidth, 0f, -RampLength), new Vector3(-r, DaisHeight + TrimHeight, 0f), TrimGold);
            Picture(root.transform, "Ramp rim E", new Vector3(r, 0f, -RampLength), new Vector3(r + TrimWidth, DaisHeight + TrimHeight, 0f), TrimGold);
            var team = Lit("M_TeamFlag", TeamColor(station.Team), .5f, .5f);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (r + .6f);
                Picture(root.transform, "Flag pole", new Vector3(x - .05f, 0f, -.4f), new Vector3(x + .05f, 3.2f, -.3f), TrimGold);
                Picture(root.transform, "Flag", new Vector3(x - (side < 0 ? .9f : 0f), 2.3f, -.36f), new Vector3(x + (side > 0 ? .9f : 0f), 3.1f, -.34f), team);
            }

            // The sign: 5.6 x 1.4 m at 3.9 m over the entrance, on two posts, facing the way in.
            var sign = new GameObject("Arena sign").transform;
            sign.SetParent(root.transform, false);
            sign.localPosition = new Vector3(0f, 3.9f, -.1f);
            foreach (float side in new[] { -1f, 1f })
                Picture(root.transform, "Sign post", new Vector3(side * 2.9f - .07f, 0f, -.17f), new Vector3(side * 2.9f + .07f, 3.9f, -.03f), Wood(false));
            Picture(sign, "Sign board", new Vector3(-2.8f, -.7f, -.05f), new Vector3(2.8f, .7f, .05f), Lit("M_SignBack", Groove, 0f, .8f));
            Picture(sign, "Sign rim top", new Vector3(-2.85f, .65f, -.08f), new Vector3(2.85f, .75f, .06f), TrimGold);
            Picture(sign, "Sign rim bottom", new Vector3(-2.85f, -.75f, -.08f), new Vector3(2.85f, -.65f, .06f), TrimGold);
            var symbol = Text(sign, "Sign symbol", new Vector3(-2.25f, 0f, -.07f), 1.1f, Gold);
            var title = Text(sign, "Sign title", new Vector3(.35f, 0f, -.07f), .55f, Hex("#F3EADB"));

            // The gate bar: 4.0 x 0.22 m over the gate, on the station's side of the north block.
            var bar = new GameObject("Gate progress bar").transform;
            bar.SetParent(root.transform, false);
            bar.localPosition = new Vector3(0f, 5.6f, MissionStation.Size - .12f);
            Picture(bar, "Bar groove", new Vector3(-2.05f, -.15f, -.03f), new Vector3(2.05f, .15f, .03f), Lit("M_BarGroove", Groove, 0f, .8f));
            var fill = Picture(bar, "Bar fill", new Vector3(-2f, -.11f, -.06f), new Vector3(2f, .11f, -.02f), Lit("M_BarFill", TeamColor(station.Team), .9f, .4f));
            var count = Text(bar, "Bar count", new Vector3(0f, .55f, -.05f), .45f, TeamColor(station.Team));

            var dressing = root.AddComponent<ArenaDressing>();
            dressing.Configure(station, symbol, title, fill.transform, count);
            return dressing;
        }

        // A text facing -z (the way in), Korean-capable.
        public static TextMesh Text(Transform parent, string name, Vector3 at, float height, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var t = go.AddComponent<TextMesh>();
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.fontSize = 64;
            t.characterSize = height / 6.4f;
            t.color = color;
            var font = RuntimePanels.KoreanFont;
            if (font != null)
            {
                t.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return t;
        }

        // The sign's symbol per game.
        public static string Symbol(int game)
        {
            switch (game)
            {
                case Network.PawnRushMissions.Paint: return "♝";
                case Network.PawnRushMissions.Planks: return "♜";
                case Network.PawnRushMissions.Drawbridge: return "♜";
                case Network.PawnRushMissions.Capstan: return "♜";
                case Network.PawnRushMissions.Bells: return "♛";
                case Network.PawnRushMissions.EnPassant: return "♟";
                case Network.PawnRushMissions.KingEscort: return "♚";
                case Network.PawnRushMissions.Checkmate: return "♚";
                case Network.PawnRushMissions.CheckBlock: return "♔";
                case Network.PawnRushMissions.KnightPads: return "♞";
                default: return "♟";
            }
        }
    }

    // A station's sign and gate bar at run time: the game's name, the bar filled to its progress (§2.1), the
    // 25 / 50 / 75 % and done moments (§2.4) played from the progress every PC has (online: the host's numbers).
    public sealed class ArenaDressing : MonoBehaviour
    {
        MissionStation station;
        TextMesh symbol, title, count;
        Transform fill;
        float shown, pulse;
        int quarter;
        bool done;
        int game = -2;

        public void Configure(MissionStation station, TextMesh symbol, TextMesh title, Transform fill, TextMesh count)
        {
            this.station = station;
            this.symbol = symbol;
            this.title = title;
            this.fill = fill;
            this.count = count;
        }

        // A new game in the station: new words, the bar empty, no moments owed.
        public void Installed(int game)
        {
            this.game = game;
            if (symbol != null) symbol.text = Arena.Symbol(game);
            if (title != null) title.text = MissionPicker.Name(game);
            shown = 0f;
            quarter = 0;
            done = false;
            if (count != null) count.text = "";
        }

        void Update()
        {
            if (station == null) return;
            var g = station.Game;
            float p = g != null ? g.Progress01 : 0f;
            shown = Mathf.MoveTowards(shown, p, Time.deltaTime * 1.5f);
            pulse = Mathf.Max(0f, pulse - Time.deltaTime);
            float k = 1f + .15f * Mathf.Sin(Mathf.Clamp01(pulse / .15f) * Mathf.PI);
            if (fill != null)
            {
                fill.localScale = new Vector3(Mathf.Max(.001f, 4f * shown), .22f * k, .04f);
                fill.localPosition = new Vector3(-2f + 2f * shown, 0f, -.04f);
            }
            if (g == null || station.GameIndex != game) return;
            if (count != null && g is IProgressCount c) count.text = $"{c.Done} / {c.Total}";
            int q = Mathf.Min(3, Mathf.FloorToInt(p * 4f + 1e-4f));
            if (q > quarter && !g.Completed)
            {
                quarter = q;
                pulse = .15f;
                var gate = station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f));
                ArenaFx.Ring(gate, 0f, 3f, .7f, Arena.Gold);
                ArenaFx.Burst(gate + Vector3.up * 5.6f, new[] { Arena.Gold }, 10, 2.5f, 2f, .9f, .1f);
                ArenaFx.Sfx("sfx_mg_quarter", gate, pitch: Mathf.Pow(2f, (q - 1) / 12f));
            }
            if (g.Completed && !done)
            {
                done = true;
                quarter = 4;
                var gate = station.transform.TransformPoint(new Vector3(0f, Arena.DaisHeight + .05f, 18.8f));
                var team = Arena.TeamColor(station.Team);
                ArenaFx.Beam(gate, 1.2f, 1.8f, 9f, 1.6f, team);
                ArenaFx.Burst(gate + Vector3.up, new[] { team, Arena.Gold, Color.white }, 90, 3f, 7f, 2.2f, .14f);
                ArenaFx.Ring(gate, 0f, 8f, 1f, team);
                bool ours = MissionStation.LocalTeam == station.Team;
                ArenaFx.Sfx("sfx_mg_complete", gate, twoD: true, volume: ours ? 1f : .35f);
                ArenaFx.Sfx("sfx_gate_open", gate);
            }
        }
    }

    // A game that counts things (squares, statues, kings, flags): the gate's "18 / 32".
    public interface IProgressCount
    {
        int Done { get; }
        int Total { get; }
    }
}
