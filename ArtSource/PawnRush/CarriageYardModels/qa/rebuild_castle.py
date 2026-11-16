# blender -b --factory-startup --python qa/rebuild_castle.py
# R680 QA castle batch: rebuilds the castle from the AI kit and re-exports its three FBXs (no textures); castle_export
# also rewrites CastleBoxData in RoyalCarriageYard.Castle.cs (diffed afterwards).
import os
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))   # the project, from this file
HERE = os.path.join(ROOT, "ArtSource/PawnRush/CarriageYardModels")


def run(g, name):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        exec(compile(f.read(), name, "exec"), g)


bpy.ops.wm.read_factory_settings(use_empty=True)
g = {"__name__": "rcy", "RCY_ROOT": ROOT, "RCY_BUILD": False}
run(g, "rcy_kit.py")
g["RCY_BUILD"] = True
run(g, "castle_model.py")
run(g, "export_rcy.py")
g["castle_export"](textures=False)
rail = bpy.data.objects.get("Balustrade part")
print("RCY_CASTLE rail part tris", sum(len(p.vertices) - 2 for p in rail.data.polygons) if rail else None)
print("RCY_CASTLE_DONE")
