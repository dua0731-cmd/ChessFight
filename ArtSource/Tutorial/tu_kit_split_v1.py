# Tutorial map kit, step 1 (R834): the user's three Tripo GLBs, split into single assets, inspected for anything that
# looks broken, repaired, sized to game metres and saved as one .blend of clean parts.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python tu_kit_split_v1.py -- [--probe] [--render]
#
#   Source/TU_A1_CastleWallTower.glb  sheet 01 (AssetRefs/TU_assets_01_castle_walls.png):     wall, corner, tower, pilaster
#   Source/TU_A2_CastleProps.glb      sheet 02 (TU_assets_02_gatehouse_statues.png):          gatehouse, niche, knight, bishop, steps
#   Source/TU_A3_AssetPack.glb        sheet 03 (TU_assets_03_training_obstacles.png):         climb block, ledge, ramp, limbo, tunnel
#
# --probe   only split and print / render the raw parts (no repair, no sizing)
# --render  inspection renders (Renders/inspect_*.png): back faces show magenta, so a hole or flipped face cannot hide
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib, tu_kit_common as K
importlib.reload(K)
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PROBE = "--probe" in ARGS

SETS = [("TU_A1_CastleWallTower", "TU_Kit_CastleKit"),
        ("TU_A2_CastleProps", "TU_Kit_GateKit"),
        ("TU_A3_AssetPack", "TU_Kit_TrainingKit")]


def row(objs, gap=.06):
    x = 0.
    for o in objs:
        lo, hi = K.bounds(o)
        K.xform(o, Matrix.Translation(Vector((x - lo[0], -(lo[1] + hi[1]) / 2, -lo[2]))))
        x += hi[0] - lo[0] + gap
    return x


def probe():
    report = {}
    for src, _ in SETS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        parts = K.split_by_footprint(K.load_glb(src))
        report[src] = []
        for i, o in enumerate(parts):
            lo, hi = K.bounds(o)
            r = K.inspect(o); r["size"] = [round(float(v), 3) for v in (hi - lo)]; r["x"] = round(float((lo[0] + hi[0]) / 2), 3)
            report[src].append(r)
            print("PART", src, i, r)
        w = row(parts)
        K.mark_backfaces()
        K.setup_render(1800, 700)
        cam = K.camera()
        K.look_at(cam, (w / 2, -2.2, 1.3), (w / 2, 0, .12), ortho=w * 1.04)
        K.render(os.path.join(K.RENDERS, "probe_%s.png" % src))
    print("REPORT", json.dumps(report))


if __name__ == "__main__":
    if PROBE:
        probe()
