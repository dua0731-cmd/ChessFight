# A look at hammer_asset.py on its own (R432): one robot at x 0, the dyke's floor, a pawn for scale.
#   blender -b --python hammer_test.py
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
os.environ["GC_DRESS"] = "Hammer test"
from gc_kit import *
import hammer_asset

M["moss"] = textured("GC | moss band", STONE_TEX, (.42, .62, .30), .75, .5)


def pillar_banner(m, x_face, side, yc, top, width=1.3, length=2.4):
    """A royal blue swallowtail banner on a pillar's east (side +1) or west (-1) face, its gold crown outward."""
    z0 = top - length

    def piece(y0, y1, d0, d1, za, zb, mat, bev=0.):
        xa, xb = sorted((x_face + side * d0, x_face + side * d1))
        m.box((xa, yc + y0, za), (xb, yc + y1, zb), mat, bev)

    w = width / 2
    tail = length * .2
    piece(-w, w, 0, .04, z0, top, M["blue"])
    piece(-w, -.06, 0, .04, z0 - tail, z0, M["blue"])
    piece(.06, w, 0, .04, z0 - tail, z0, M["blue"])
    piece(-w - .08, w + .08, -.02, .12, top - .12, top + .04, M["gold"], .02)
    piece(-w, w, .04, .07, top - .38, top - .3, M["gold"])
    piece(-w, w, .04, .07, z0 + .06, z0 + .14, M["gold"])
    # The crown: a band and three points with balls.
    cz = z0 + length * .42
    piece(-.38, .38, .04, .1, cz, cz + .16, M["gold"], .01)
    for k in (-1, 0, 1):
        tip = .12 if k == 0 else 0
        piece(k * .28 - .08, k * .28 + .08, .04, .1, cz + .16, cz + .5 + tip, M["gold"], .01)
        m.sphere((x_face + side * .07, yc + k * .28, cz + .58 + tip), .08, M["gold"], subdiv=1)


mats = {k: M[k] for k in ("stone", "stone2", "stone3", "moss", "gold", "wood", "blue")}
made = hammer_asset.make_hammers([("1", 0.0)], mats, DRESS)
pillar = [o for o in made if o.name.startswith("Robot pillar")][0]
co = np.array([v.co[:] for v in pillar.data.vertices])
body = co[(co[:, 2] > 5) & (co[:, 2] < 8.5) & (co[:, 1] > 29.5)]
x_lo, x_hi = body[:, 0].min(), body[:, 0].max()
yc = (body[:, 1].min() + body[:, 1].max()) / 2
print("PILLAR body x %.2f..%.2f, y %.2f..%.2f" % (x_lo, x_hi, body[:, 1].min(), body[:, 1].max()))
print("PILLAR all", co.min(0).round(2), co.max(0).round(2))
arm = [o for o in made if o.name.startswith("Robot arm")][0]
ca = np.array([v.co[:] for v in arm.data.vertices])
print("ARM", ca.min(0).round(2), ca.max(0).round(2), "tris", len(arm.data.polygons), "pillar tris", len(pillar.data.polygons))
m = Mesh("Robot banners 1")
for side, xf in ((1, x_hi), (-1, x_lo)):
    pillar_banner(m, xf, side, yc, 8.75)
m.done()

# The dyke's floor (blockout clay) and a pawn for scale.
ctx = Mesh("Context floor", CTX)
ctx.box((-8, 22, 3.6), (8, 33, 3.8), M["clay"])
ctx.box((-8, 30, 3.8), (8, 30.5, 4.9), M["clay"])
pawn(ctx, 2.0, 27, 3.8)
ctx.done()

render_setup()
shoot("hammer_v3", "side", (22, 27.5, 7.5), (0, 28.5, 6.5), 40)
shoot("hammer_v3", "three_quarter", (13, 13, 9), (0, 28.5, 6.2), 35)
shoot("hammer_v3", "hub_east", (7, 26, 10.2), (0, 27.3, 9.5), 35)
shoot("hammer_v3", "hub_south", (2.5, 19, 10.5), (0, 27.5, 9.4), 30)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "HammerTest_v3.blend"))
