# Runs one CR04..CR07 corridor: builds its modules and ornaments, assembles a preview corridor as the Unity loader will
# (piers, bays stretched to the real span, the east wall turned 180 degrees, ornaments from the layout), renders it,
# and with --export writes the FBX files, CR0x_Layout.json and the .mat files.
#
# A corridor script calls run(SPEC, build) where build(col) returns {module name: (object, [empties])} and SPEC is
#   id "CR04_LowerRooms", height, z_from, z_to, portal_start, portal_end, header_start, header_end, end_wall (bool),
#   layout [ {"name": "CR04_Sconce", "per": "pier"|"bay", "index": [..], "mirror": bool, "offset": [x, y, dz]} ]
#   (offset in the corridor frame of the west wall: x from the bodies' face line (+ into the corridor), y up,
#   dz along the way from the pier or bay centre; a "mirror": false piece is placed once at x = offset.x).
import json, math, os, sys
import bpy
from mathutils import Vector
import cr_kit_04_07 as K


def _place_layout(layout, mods, ps, col):
    for item in layout:
        src = mods[item["name"].split("_", 1)[1]][0]
        zs = ps if item["per"] == "pier" else [(ps[i] + ps[i + 1]) * .5 for i in range(3)]
        x, y, dz = item["offset"]
        for i in item["index"]:
            if item.get("mirror", True):
                for side in (-1, 1):
                    K.place(src, col, (side * (6 - x), zs[i] + dz, y), 0 if side < 0 else math.pi)
            else:
                K.place(src, col, (x, zs[i] + dz, y))


def run(spec, build):
    args = K.ARGS
    K.reset()
    src = K.collection("Modules")
    mods = build(src)
    H = spec["height"]
    asm = {k: v[0] for k, v in mods.items()}
    col = K.assemble(asm, H, spec["z_from"], spec["z_to"], spec["portal_start"], spec["portal_end"],
                     spec["header_start"], spec["header_end"], asm.get("EndWall"))
    ps = K.piers(spec["z_from"], spec["z_to"])
    _place_layout(spec["layout"], mods, ps, col)
    for o in src.objects:
        o.hide_render = True
        o.location.x += 40            # the sources out of the preview
    total = 0
    print("---- %s modules" % spec["id"])
    counts = {"Bay": 6 - (2 if "BayMid" in mods else 0), "BayMid": 2, "Pier": 8, "CeilingBay": 3, "CeilingBeam": 4,
              "Portal": int(spec["portal_start"]) + int(spec["portal_end"]),
              "Header": int(spec["header_start"]) + int(spec["header_end"]), "EndWall": 1, "FloorInlay": 1}
    for item in spec["layout"]:
        n = len(item["index"]) * (2 if item.get("mirror", True) else 1)
        counts[item["name"].split("_", 1)[1]] = n
    for name, (ob, em) in mods.items():
        t = K.tris(ob)
        c = counts.get(name, 1)
        total += t * c
        print("MODULE %-14s %6d tris x %d" % (name, t, c))
    print("CORRIDOR %s total %d tris" % (spec["id"], total))
    folder_rel = "ArtSource/PawnRush/Corridors/previews"
    if "--render" in args:
        views = spec.get("views", K.STD_VIEWS)
        K.render_views(col, folder_rel, "cr47_" + spec["id"], H, views)
    if "--export" in args:
        out = K.OUT_REL + "/" + spec["id"]
        keys = set()
        for name, (ob, em) in mods.items():
            ob.location.x -= 40
            for e in em:
                e.location.x -= 40
            K.export_module(ob, out + "/%s_%s.fbx" % (spec["id"][:4], name), em)
            keys.update(m["cor_key"] for m in ob.data.materials)
        K.write_materials(sorted(keys))
        K.write(out + "/%s_Layout.json" % spec["id"][:4], json.dumps({
            "id": spec["id"], "height": H, "z_from": spec["z_from"], "z_to": spec["z_to"],
            "bay_span_authored": K.SPAN, "pier_depth": K.PIER,
            "modules": sorted(n for n in mods if n in counts and not any(n == i["name"].split("_", 1)[1] for i in spec["layout"])),
            "ornaments": spec["layout"],
            **{k: spec[k] for k in ("banner", "banner_room") if k in spec}}, indent=2) + "\n")
        rel = out + "/%s_Layout.json" % spec["id"][:4]
        if not os.path.exists(os.path.join(K.ROOT, rel + ".meta")):
            K.write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n"
                    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % K.guid(rel))
        print("LAYOUT written")
