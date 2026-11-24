# JY-gfx 11-24: the reviewed hall lighting designs (designs/<key>.design.json, the critic's corrected design) into the code:
# each map's Concept, Look and Rig in src/GfxMapLights.cs (a new Map entry where the hall had none), and its light plan as
# designs/<key>.json for gen_plans.py (GfxMapPlans.cs). Keeps every Rig field the design does not set (the adopted floors,
# the heart word, colours...). Usage: python apply_designs.py <designs folder>
import json, os, re, sys

folder = sys.argv[1]
P = "C:/cf-gfx/night/src/GfxMapLights.cs"
t = open(P, encoding="utf-8").read()
OWN = {"3-1_RoyalLibrary", "3-2_RoyalObservatory"}
NAMES = {"2-2_RoyalClockTower": "Clock tower", "3-3_RoyalVault": "Royal vault", "3-1_RoyalLibrary": "Royal library",
         "3-2_RoyalObservatory": "Royal observatory"}


def f(v):
    s = ("%.3f" % float(v)).rstrip("0").rstrip(".")
    if s.startswith("0."):
        s = s[1:]
    elif s.startswith("-0."):
        s = "-" + s[2:]
    if s in ("", "-0", "-"):
        s = "0"
    return s + "f"


def color(v):
    return "new Color(%s, %s, %s)" % (f(v[0]), f(v[1]), f(v[2]))


def text(s):
    return '"' + str(s).replace("\\", "\\\\").replace('"', '\\"').replace("\n", " ") + '"'


def look_fields(look):
    order = ["Indoor", "Bounce", "BounceTint", "Exposure", "Contrast", "Saturation", "BloomIntensity", "BloomThreshold", "Vignette",
             "ShaftIntensity", "ShaftDensity", "ShaftTint", "Temperature", "Tint", "ShadowsLift", "BloomScatter", "ShadowsTone",
             "HighlightsTone", "BloomTint"]
    out = []
    for k in order:
        if k not in look:
            continue
        v = look[k]
        out.append("%s = %s" % (k, color(v) if isinstance(v, list) else f(v)))
    return out


def split_fields(body):
    # "A = 1f, B = new Color(1f, 2f, 3f), C = \"x\"" -> ordered dict (commas inside parentheses or strings kept)
    fields, depth, cur, quote = [], 0, "", False
    for ch in body:
        if ch == '"':
            quote = not quote
        if not quote:
            if ch == "(":
                depth += 1
            elif ch == ")":
                depth -= 1
            elif ch == "," and depth == 0:
                if cur.strip():
                    fields.append(cur.strip())
                cur = ""
                continue
        cur += ch
    if cur.strip():
        fields.append(cur.strip())
    d = {}
    for item in fields:
        k, _, v = item.partition("=")
        d[k.strip()] = v.strip()
    return d


def rig_text(old, rig):
    d = split_fields(old) if old else {}
    for k, v in rig.items():
        if isinstance(v, bool):
            d[k] = "true" if v else "false"
        elif isinstance(v, list):
            d[k] = color(v)
        elif isinstance(v, str):
            d[k] = text(v)
        elif isinstance(v, int) and k in ("UplightMax",):
            d[k] = str(v)
        else:
            d[k] = f(v)
    return "new Rig { " + ", ".join("%s = %s" % kv for kv in d.items()) + " }"


done = []
for name in sorted(os.listdir(folder)):
    if not name.endswith(".design.json"):
        continue
    d = json.load(open(os.path.join(folder, name), encoding="utf-8"))
    key = name[:-len(".design.json")]
    look, rig, plan = d["look"], d["rig"], d["plan"]
    concept = d["concept_en"]
    m = re.search(r'Key = "' + re.escape(key) + r'",(?: OwnLook = true,)?(?: OwnGrade = true,)?\s*\n\s*Concept = "(?:[^"\\]|\\.)*",', t)
    if m:
        # Concept
        start = m.start()
        block_end = t.index("            },\n", t.index("Rig = new Rig", start)) + len("            },\n")
        block = t[start:block_end]
        block = re.sub(r'Concept = "(?:[^"\\]|\\.)*",', lambda _: "Concept = " + text(concept) + ",", block, count=1)
        flags = (" OwnLook = true," if key in OWN else "") + (" OwnGrade = true," if d.get("own_grade") else "")
        block = re.sub(r'Key = "' + re.escape(key) + r'",(?: OwnLook = true,)?(?: OwnGrade = true,)?', lambda _: "Key = " + text(key) + "," + flags, block, count=1)
        # Look: keep its Name
        lm = re.search(r'Look = new GameCourses\.Look\n(\s*)\{\n\s*Name = "([^"]+)",.*?\n(\s*)\},', block, re.S)
        indent = lm.group(1) + "    "
        new_look = "Look = new GameCourses.Look\n%s{\n%sName = %s, %s,\n%s}," % (lm.group(1), indent, text(lm.group(2)), ", ".join(look_fields(look)), lm.group(3))
        block = block[:lm.start()] + new_look + block[lm.end():]
        # Rig: merged
        rm = re.search(r"Rig = new Rig(?:\(\)|\s*\{(.*?)\})", block, re.S)
        block = block[:rm.start()] + "Rig = " + rig_text(rm.group(1) or "", rig) + block[rm.end():]
        t = t[:start] + block + t[block_end:]
    else:
        # A new map, before the end of the list.
        end = t.index("        };\n\n        /// <summary>The map a section is, by its name")
        entry = ("            new Map\n            {\n                Key = %s,%s\n                Concept = %s,\n"
                 "                Look = new GameCourses.Look\n                {\n                    Name = %s, %s,\n                },\n"
                 "                Rig = %s,\n            },\n") % (
            text(key), (" OwnLook = true," if key in OWN else "") + (" OwnGrade = true," if d.get("own_grade") else ""), text(concept), text(NAMES.get(key, key)), ", ".join(look_fields(look)),
            rig_text("", rig))
        t = t[:end] + entry + t[end:]
    # A part name as the anatomy prints it ("Content / Lantern glass") is the part's own name after the group.
    for line in plan:
        if line.get("each"):
            line["each"] = line["each"].split(" / ")[-1].strip()
    json.dump({"map_key": key, "plan": plan}, open(os.path.join(folder, key + ".json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    done.append((key, len(plan)))
open(P, "w", encoding="utf-8").write(t)
print("applied", done)
