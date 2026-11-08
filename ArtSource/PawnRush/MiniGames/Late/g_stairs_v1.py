# G entry stairs (R504, user "진짜 단으로"): the user's "Marble Stairs Purple Carpet" FBX
# (Source/R504_MarbleStairsPurpleCarpet.fbx: three steps rising toward +y, a top landing, a balustrade either side)
# fitted to the game's 0.4 m dais (Arena.DaisHeight) and 1.6 m ramp run (Arena.RampLength):
#   up    x1.68: the top landing (0.238 above the base in the source) at 0.4 m, so each step is ~0.15 m
#   run   front edge to back edge 1.6 m (the landing ends at the dais front)
#   width only the stair middle (|x| < 0.35 in the source) is stretched; the balustrades keep the up scale, so posts
#         and balusters are not smeared: G_StairsCenter 4.0 m wide (the raised central aisle), G_StairsRamp 4.4 m
#         (over each team's ramp)
# Origin = bottom of the front edge, centre; Blender -y = front (the low side, toward the entry). Faded pink carpet ->
# purple velvet, beige stone -> the hall's cream marble (g_props_v1.retexture "Stairs"). The walking surface is the
# game's own slope collider (MiniGameModels), so a pawn never trips on the step edges.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python g_stairs_v1.py -- [--export]
import bpy, math, os, sys
import numpy as np
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_props_v1 as props

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SRC = "R504_MarbleStairsPurpleCarpet.fbx"
TOP, INNER = .238, .35          # source: top landing above the base; half width of the stretchable middle
RISE, RUN = .4, 1.6
VARIANTS = {"G_StairsCenter": 4.0, "G_StairsRamp": 4.4}
TARGET = 8000                   # close to the camera at the door, two or three copies


def fit(ob, width):
    me = ob.data
    co = np.array([v.co[:] for v in me.vertices]); lo, hi = co.min(0), co.max(0)
    kz = RISE / TOP
    ky = RUN / (hi[1] - lo[1])
    outer = (.5 - INNER) * kz                    # one balustrade's width after the scale
    kx = (width - 2 * outer) / (2 * INNER)       # the middle's stretch
    for v in me.vertices:
        x, y, z = v.co
        if abs(x) <= INNER: nx = x * kx
        else: nx = math.copysign(INNER * kx + (abs(x) - INNER) * kz, x)
        v.co = (nx, (y - lo[1]) * ky, (z - lo[2]) * kz)
    me.update()
    print("FIT %s: middle x%.2f, sides x%.2f, run x%.2f" % (ob.name, kx, kz, ky))


def main():
    made = []
    src = props.load(SRC)            # (load resets the scene, so once)
    for name, width in VARIANTS.items():
        ob = props.cut(src, np.ones(len(src.data.vertices), bool), name)
        fit(ob, width)
        props.decimate(ob, TARGET)
        c = np.array([v.co[:] for v in ob.data.vertices])
        print("OBJ %-15s tris %5d  size %s" % (name, props.tris(ob), np.round(c.max(0) - c.min(0), 2)))
        if not made: props.retexture("Stairs", ob.data.materials[0], [])
        else: ob.data.materials[0] = made[0].data.materials[0]
        made.append(ob)
    bpy.data.objects.remove(src)
    if "--export" in ARGS:
        for ob in made: props.export(ob)


if __name__ == "__main__":
    main()
