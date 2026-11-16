# blender -b --factory-startup --python qa/rebuild_models.py [-- only1,only2]
# R680 QA: rebuilds every clay model from source (rcy_kit's hidden-face cull, box normals, round shading) and
# re-exports it. The castle (castle_model.py) is not touched.
import os, sys
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))   # the project, from this file
HERE = os.path.join(ROOT, "ArtSource/PawnRush/CarriageYardModels")
ONLY = set(sys.argv[sys.argv.index("--") + 1].split(",")) if "--" in sys.argv else None


def run(g, name):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        exec(compile(f.read(), name, "exec"), g)


def fresh(**flags):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    g = {"__name__": "rcy", "RCY_ROOT": ROOT}
    g.update(flags)
    run(g, "rcy_kit.py")
    run(g, "export_rcy.py")
    return g


def want(key):
    return ONLY is None or key in ONLY


if want("entrance"):
    g = fresh(RCY_BUILD=True)
    run(g, "entrance_model.py")
    g["export"]("RCY Entrance", "RcyEntrance")

if want("canal"):
    g = fresh()
    run(g, "canal_model.py")
    g["export"]("RCY Canal", "RcyCanal")
    g["export"]("RCY Wheel", "RcyGiantWaterwheel")

if want("sorting"):
    g = fresh(RCY_BUILD=False)
    run(g, "entrance_model.py")
    g["RCY_BUILD"] = True
    run(g, "sorting_model.py")
    g["export"]("RCY Sorting", "RcySorting")

if want("platform"):
    g = fresh(RCY_BUILD=True)
    run(g, "platform_model.py")
    g["platform_export"]()

if want("scenery"):
    g = fresh(RCY_BUILD=False)
    run(g, "entrance_model.py")
    run(g, "sorting_model.py")
    g["RCY_BUILD"] = True
    run(g, "scenery_model.py")
    g["scenery_export"]()

if want("movers"):
    g = fresh(RCY_BUILD=False, RCY_BUILD_MOVERS=True)
    run(g, "entrance_model.py")
    run(g, "movers_model.py")
    for cname, fbx in (("RCY Coach", "RcyCoach"), ("RCY Coach wheel", "RcyCoachWheel"), ("RCY Hitch", "RcyHitch"),
                       ("RCY Gate leaf W", "RcyGateLeafW"), ("RCY Gate leaf E", "RcyGateLeafE")):
        g["export"](cname, fbx)

if want("machines"):
    g = fresh(RCY_BUILD=False, RCY_BUILD_MOVERS=False, RCY_BUILD_MACHINES=False)
    run(g, "entrance_model.py")
    run(g, "sorting_model.py")
    run(g, "movers_model.py")
    run(g, "sorting_machines_model.py")
    g["build_sorting_machines"]()
    for cname, fbx, _ in g["SORTING_MACHINES"]:
        g["export"](cname, fbx)

if want("horse"):
    g = fresh()
    run(g, "horse_model.py")
    g["build_horse"]()
    g["export_horse"]()

print("RCY_REBUILD_DONE")
