# JY-gfx 11-24: the indoor maps' lighting designs (JSON, one file per map in a folder) as C#: GfxMapPlans.cs.
# Usage: python gen_plans.py <designs folder> <out GfxMapPlans.cs>
# A design file: {"map_key": "...", "plan": [ {layer, note, type, at, to, count, each, anchor, offset, inward, max, spacing,
#                 aim, intensity, range, angle, inner, color, shadows}, ... ]}
import json, os, sys

src, out = sys.argv[1], sys.argv[2]


def f(v):
    s = ("%.3f" % float(v)).rstrip("0").rstrip(".")
    if s in ("-0", ""):
        s = "0"
    return s + "f"


def vec(v):
    return "new Vector3(%s, %s, %s)" % (f(v[0]), f(v[1]), f(v[2]))


def col(v):
    return "new Color(%s, %s, %s)" % (f(v[0]), f(v[1]), f(v[2]))


def text(s):
    return '"' + str(s).replace("\\", "\\\\").replace('"', '\\"').replace("\n", " ") + '"'


LAYERS = {"daylight", "general", "wash", "uplight", "decorative", "accent", "bounce"}
maps = []
for name in sorted(os.listdir(src)):
    if not name.endswith(".json") or name.endswith(".design.json"):
        continue
    d = json.load(open(os.path.join(src, name), encoding="utf-8"))
    key = d["map_key"]
    lines = []
    for i, p in enumerate(d["plan"]):
        layer = p.get("layer", "general")
        assert layer in LAYERS, (key, i, layer)
        t = p.get("type", "Point")
        assert t in ("Point", "Spot"), (key, i, t)
        parts = ["Layer = " + text(layer), "Note = " + text(p.get("note", "")), "Type = LightType." + t]
        if p.get("each"):
            parts.append("Each = " + text(p["each"]))
            if p.get("anchor", "centre") != "centre":
                parts.append("Anchor = " + text(p["anchor"]))
            if p.get("offset"):
                parts.append("Offset = " + vec(p["offset"]))
            if p.get("inward"):
                parts.append("Inward = true")
            if p.get("radial"):
                parts.append("Radial = true")
            if p.get("max"):
                parts.append("Max = %d" % int(p["max"]))
            if p.get("spacing"):
                parts.append("Spacing = " + f(p["spacing"]))
        else:
            parts.append("At = " + vec(p["at"]))
            if int(p.get("count", 1) or 1) > 1:
                parts.append("To = " + vec(p["to"]))
                parts.append("Count = %d" % int(p["count"]))
        if t == "Spot":
            parts.append("Aim = " + vec(p.get("aim", [0, -1, 0])))
            parts.append("Angle = " + f(p.get("angle", 60)))
            parts.append("Inner = " + f(p.get("inner", min(40, p.get("angle", 60)))))
        parts.append("Intensity = " + f(p["intensity"]))
        parts.append("Range = " + f(p["range"]))
        parts.append("Color = " + col(p.get("color", [1, 1, 1])))
        if p.get("beam"):
            parts.append("Beam = " + f(p["beam"]))
        if p.get("bounce") is not None and float(p["bounce"]) != 1.0:
            parts.append("Bounce = " + f(p["bounce"]))
        if p.get("shadows"):
            parts.append("Shadows = true")
        lines.append("                new GfxMapLights.Fixture { " + ", ".join(parts) + " },")
    maps.append((key, lines))

with open(out, "w", encoding="utf-8", newline="\n") as o:
    o.write("using System.Collections.Generic;\nusing UnityEngine;\n\nnamespace ChessFight.PawnRush\n{\n")
    o.write("    /// <summary>\n")
    o.write("    /// JY-gfx (2026-11-24): each indoor map's lighting design, light by light (JY: \"조명 설계부터 다시 하고 라이팅\n")
    o.write("    /// 구현해\"). Made by ArtSource/Gfx/Lighting/gen_plans.py from the designs beside it (one JSON per map); edit those\n")
    o.write("    /// and generate again rather than editing this file. GfxMapLights.Fixture says what each field means; the rig\n")
    o.write("    /// builds the lights with the section and scales each layer by its numbers (GfxMapLights.Rig).\n")
    o.write("    /// </summary>\n")
    o.write("    public static class GfxMapPlans\n    {\n")
    o.write("        public static List<GfxMapLights.Fixture> For(string key) => key != null && Plans.TryGetValue(key, out var plan) ? plan : null;\n\n")
    o.write("        static readonly Dictionary<string, List<GfxMapLights.Fixture>> Plans = new Dictionary<string, List<GfxMapLights.Fixture>>\n        {\n")
    for key, lines in maps:
        o.write("            [" + text(key) + "] = new List<GfxMapLights.Fixture>\n            {\n")
        o.write("\n".join(lines) + "\n")
        o.write("            },\n")
    o.write("        };\n    }\n}\n")
print("maps", len(maps), "lights lines", sum(len(l) for _, l in maps))
