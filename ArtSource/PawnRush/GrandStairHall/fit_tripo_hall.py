# Bends the placed Tripo hall (v3) onto the blockout's layout and deletes the blockout's walls and floors -> v4.
#   front (porch, foyer, the grand stair, the central landing): a piecewise map along Y and a piecewise map in Z
#     model Y 12.8 porch front / 20 porch-foyer / 34 hall south wall / 36 stair foot / 50-52 middle landing /
#     64.5 stair top / 78 landing north  ->  our -12 / 0 / 12 / 14 / 29.5-35.5 / 52 / 66
#     model Z 1.1 foyer / 7.0 middle landing / 10.2 central landing -> our 0 / 4 / 8 (above: +1 per 1, below: shifted)
#   round hall: its own centre moved onto our hub (0, 92) and scaled about it so the outer wall's inner face sits on
#     our round wall's inner radius 31 (same Z map); the two maps are blended where the ring meets the landing.
# Run: blender -b grand_stair_hall_v3.blend -P fit_tripo_hall.py
import bpy, os, re
import numpy as np

HERE = os.path.dirname(bpy.data.filepath)
HUB = np.array([0.0, 92.0])
R_OURS = 31.0

ob = bpy.data.objects["Tripo royal circular hall"]
me = ob.data
n = len(me.vertices)
co = np.zeros(n * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
M = np.array(ob.matrix_world)
w = (np.c_[co, np.ones(n)] @ M.T)[:, :3]          # world positions (placed and scaled in v3)
x, y, z = w[:, 0].copy(), w[:, 1].copy(), w[:, 2].copy()

# The round hall's outer wall: fit a circle to its tall vertices (north half, radius 29.5..34.5 from a first guess).
guess = np.array([0.0, 93.0])
for _ in range(3):
    d = np.hypot(x - guess[0], y - guess[1])
    s = (z > 12) & (z < 22) & (y > guess[1] - 10) & (d > 29.5) & (d < 34.5)
    A = np.c_[2 * x[s], 2 * y[s], np.ones(s.sum())]
    b = x[s] ** 2 + y[s] ** 2
    cx, cy, c = np.linalg.lstsq(A, b, rcond=None)[0]
    guess = np.array([cx, cy])
d = np.hypot(x - guess[0], y - guess[1])
s = (z > 12) & (z < 22) & (y > guess[1]) & (d > 28) & (d < 35)
inner = np.percentile(d[s], 8)                     # the wall's inner face
k = R_OURS / inner
print("FIT centre (%.2f, %.2f) inner %.2f -> scale %.3f" % (guess[0], guess[1], inner, k))

def zmap(v):
    return np.interp(v, [-50, 1.1, 7.0, 10.2, 100], [-51.1, 0.0, 4.0, 8.0, 97.8])

# front map
fy = np.interp(y, [-50, 12.8, 20.0, 34.0, 36.0, 50.0, 52.0, 64.5, 78.0, 200],
                  [-74.8, -12.0, 0.0, 12.0, 14.0, 29.5, 35.5, 52.0, 66.0, 188.0])
front = np.c_[x, fy, zmap(z)]
# round map
rx = HUB[0] + k * (x - guess[0])
ry = HUB[1] + k * (y - guess[1])
round_ = np.c_[rx, ry, zmap(z)]

# blend: the ring (radius > 20 from the model centre, north of the landing's foot) and everything north of the
# landing take the round map; the landing and everything south of it the front map.
def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
# The long side walls and the carpet beside the stair (|x| > 20) keep straight: from the hall's south wall (model
# 34 -> 12) to where the round hall starts (model centre -> our hub 92), then on with the round hall's scale.
sy = np.interp(y, [-50, 12.8, 20.0, 34.0, guess[1], 200], [-74.8, -12.0, 0.0, 12.0, 92.0, 92.0 + k * (200 - guess[1])])
side = np.c_[x, sy, zmap(z)]
ws = smooth(20.0, 30.0, np.abs(x))[:, None]
out = front * (1 - ws) + side * ws
outside = smooth(33.5, 35.5, d)                    # beyond the round wall: the side walls' corners
ring = smooth(19.0, 22.0, d) * smooth(58.0, 64.0, y) * (1 - outside)
north = smooth(78.0, 81.0, y) * (1 - outside)
wt = np.maximum(ring, north)[:, None]
out = out * (1 - wt) + round_ * wt

# back to the object's local space (the object keeps its v3 transform)
Mi = np.linalg.inv(M)
loc = (np.c_[out, np.ones(n)] @ Mi.T)[:, :3]
me.vertices.foreach_set("co", loc.ravel())
me.update()
print("RESULT x %.1f..%.1f  y %.1f..%.1f  z %.1f..%.1f" % (out[:, 0].min(), out[:, 0].max(), out[:, 1].min(), out[:, 1].max(), out[:, 2].min(), out[:, 2].max()))

# Delete the blockout's walls and floors (the stair, landings, spiral, obstacles and markers stay).
WALLS_FLOORS = re.compile(r"^(Hall/Hall_(wall|floor)|B1_GrandStair/(Porch_(floor|wall)|Foyer_(floor|wall|pillar|rail)|Entrance_arch))")
gone = [o for o in bpy.data.objects if o.type == "MESH" and WALLS_FLOORS.match(re.sub(r"#\d+$", "", o.name))]
orig = bpy.data.collections.get("_Blockout originals")
if orig: gone += [o for o in orig.objects if o not in gone]
names = sorted(set(re.sub(r"#\d+$", "", o.name) for o in gone))
for o in gone: bpy.data.objects.remove(o, do_unlink=True)
if orig: bpy.data.collections.remove(orig)
print("DELETED", len(gone), names)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v4.blend"))
print("SAVED")
