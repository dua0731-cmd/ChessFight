# Joins the Tripo stair to the central landing and turns the Tripo's own landing into the blockout's central landing
# box (v5) -> v6.
#   1. The Tripo dais at the top of the stair (y 51..63, 8.4..9.3 high) is flattened onto our landing height 8, so the
#      top tread runs straight onto the landing (the balustrade and its end posts are left standing).
#   2. North of the top riser (y 51) the Tripo landing is widened from its 8 m half width to the blockout's 14 m: the
#      tread, the balustrade, the end posts and the side wall under them move out together, so the balustrade turns
#      out at the top riser and the box's wings are made of the asset itself. The hall floor beside them is pressed
#      into the strip that is left; from y 63 the Tripo's wide platform (already 14 m) is left as it is.
#   3. The blockout's central landing and its rails are deleted (the king statue stays).
# Run: blender -b grand_stair_hall_v5.blend -P fit_tripo_landing.py
import bpy, os, re
import numpy as np

HERE = os.path.dirname(bpy.data.filepath)
TOP = 8.0

ob = bpy.data.objects["Tripo royal circular hall"]
me = ob.data
M = np.array(ob.matrix_world); Mi = np.linalg.inv(M)
n = len(me.vertices)
co = np.zeros(n * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
w = (np.c_[co, np.ones(n)] @ M.T)[:, :3]
x, y, z = w.T.copy()

def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

# 1. Flatten the dais. The balustrade stands at |x| 8..9.6 and its end posts at |x| 7..9 from y 58, so stay inside.
xin = np.where(y < 57.5, 1 - smooth(7.5, 7.9, np.abs(x)), 1 - smooth(6.5, 6.9, np.abs(x)))
wt = xin * smooth(51.15, 51.6, y) * (1 - smooth(63.0, 64.0, y)) * smooth(6.6, 7.2, z) * (1 - smooth(9.8, 10.4, z))
z = z + ((TOP + (z - TOP) * 0.06) - z) * wt
print("FLATTENED", int((wt > 0.5).sum()))

# 2. Widen the landing: tread |x| 0..8 -> 0..12.7, balustrade 8..9.7 -> 12.7..14.3, floor 9.7..14.6 pressed to the edge.
ax = np.abs(x)
wide = np.sign(x) * np.interp(ax, [0, 8.0, 9.7, 14.6, 20.0, 100], [0, 12.7, 14.3, 14.6, 20.0, 100])
wy = smooth(50.95, 51.6, y) * (1 - smooth(63.0, 64.2, y))
x = x + (wide - x) * wy
print("WIDENED", int((wy * (ax < 9.7) > 0.5).sum()))

# 2b. The landing's north edge (where the blockout had its north rail, y 66): the Tripo platform's crumpled drop to the
#     round hall floor (8 -> 4) becomes a clean top up to y 66 and a straight face at 66.3. East of x 4 the spiral
#     foot sits on it, so only x -11.5..4.
EDGE = 66.0
wx = np.where(x < 0, 1 - smooth(11.0, 12.0, -x), 1 - smooth(3.5, 4.5, x))
band = wx * smooth(63.0, 63.8, y) * (1 - smooth(70.0, 71.0, y)) * smooth(4.0, 4.4, z) * (1 - smooth(8.8, 9.4, z))
north = y > EDGE
z_e = np.where(north, np.clip(z, 4.0, TOP), TOP + (z - TOP) * 0.06)
y_e = np.where(north, EDGE + 0.3, y)
y = y + (y_e - y) * band
z = z + (z_e - z) * band
# Under the spiral foot (x 4..12, on to y 71.5) the platform only needs to be flat at the landing height.
foot = smooth(3.5, 4.5, x) * (1 - smooth(12.0, 13.0, x)) * smooth(63.0, 63.8, y) * (1 - smooth(71.5, 72.5, y)) \
     * smooth(4.0, 4.4, z) * (1 - smooth(9.4, 10.0, z))
z = z + ((TOP + (z - TOP) * 0.06) - z) * foot
print("EDGE", int((band > 0.5).sum()), "FOOT", int((foot > 0.5).sum()))

out = np.c_[x, y, z]
me.vertices.foreach_set("co", ((np.c_[out, np.ones(n)] @ Mi.T)[:, :3]).ravel())
me.update()

# 3. Delete the blockout box and its rails.
BOX = re.compile(r"^B2_CentralLanding/Central_landing(_rail_.*)?$")
gone = [o for o in bpy.data.objects if o.type == "MESH" and BOX.match(re.sub(r"#\d+$", "", o.name))]
print("DELETED", [o.name for o in gone])
for o in gone: bpy.data.objects.remove(o, do_unlink=True)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v6.blend"))
print("SAVED")
