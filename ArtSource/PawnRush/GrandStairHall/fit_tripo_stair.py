# Bends the Tripo hall's own grand stair (v4) onto the blockout stair and deletes the blockout stair -> v5.
#   The Tripo stair's risers and tread levels are found on its centre line, then spread evenly between the
#   blockout's ends: lower flight first riser y 14 .. last riser y 29 (floor 0 -> middle landing 4), upper flight
#   y 36 .. 51 (landing 4 -> central landing 8). The Tripo step count is kept (it differs from the blockout's 16+16).
#   Near the treads Z follows the level map (so every tread stays flat); things well above them (balustrades, rails)
#   follow the stair's slope line instead. The walk width is widened from the Tripo's 7 m half width to our 8 m.
# Run: blender -b grand_stair_hall_v4.blend -P fit_tripo_stair.py
import bpy, os, re
import numpy as np

HERE = os.path.dirname(bpy.data.filepath)
LOWER = (14.0, 29.0, 0.0, 4.0)    # first riser y, last riser y, floor level, landing level
UPPER = (36.0, 51.0, 4.0, 8.0)

ob = bpy.data.objects["Tripo royal circular hall"]
me = ob.data
n = len(me.vertices)
co = np.zeros(n * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
M = np.array(ob.matrix_world)
w = (np.c_[co, np.ones(n)] @ M.T)[:, :3]
x, y, z = w[:, 0].copy(), w[:, 1].copy(), w[:, 2].copy()

# Centre-line profile: the top of the stair surface in 5 cm bins.
BIN = 0.05
ys = np.arange(13.0, 54.0, BIN)
c = (np.abs(x) < 5) & (z > -0.5) & (z < 8.3)
prof = np.full(len(ys), np.nan)
for i, y0 in enumerate(ys):
    s = c & (y >= y0) & (y < y0 + BIN)
    if s.sum() >= 3: prof[i] = np.percentile(z[s], 90)
# Plateaus: runs of bins that stay within 6 cm of their running median for at least 0.35 m.
plats = []
i = 0
while i < len(ys):
    if np.isnan(prof[i]): i += 1; continue
    j = i; vals = [prof[i]]
    while j + 1 < len(ys) and (np.isnan(prof[j + 1]) or abs(prof[j + 1] - np.median(vals)) < 0.06):
        j += 1
        if not np.isnan(prof[j]): vals.append(prof[j])
    if (j - i + 1) * BIN >= 0.35: plats.append([ys[i], ys[j] + BIN, float(np.median(vals))])
    i = j + 1
# Merge neighbours closer than 15 cm in height (nosings, the 12 cm plinth) and keep only rising steps.
steps = []
for p in plats:
    if steps and abs(p[2] - steps[-1][2]) < 0.15:
        steps[-1][1] = p[1]; continue
    if steps and p[2] < steps[-1][2]: continue       # a dip (a hole in the tread): skip
    steps.append(p)
for p in steps: print("PLAT y %.2f..%.2f  z %.2f" % tuple(p))

lv = np.array([p[2] for p in steps])
risers = np.array([(steps[k][1] + steps[k + 1][0]) / 2 for k in range(len(steps) - 1)])
land = int(np.argmin(np.abs(lv - 3.96) + (np.array([p[1] - p[0] for p in steps]) < 1.5) * 9))
top = len(steps) - 1
nL, nU = land, top - land
print("LOWER risers %d  UPPER risers %d  landing plateau %d" % (nL, nU, land))

Ry = np.r_[np.linspace(LOWER[0], LOWER[1], nL), np.linspace(UPPER[0], UPPER[1], nU)]
Lz = np.r_[np.linspace(LOWER[2], LOWER[3], nL + 1), np.linspace(UPPER[2], UPPER[3], nU + 1)[1:]]
print("MODEL risers", np.round(risers, 2).tolist())
print("MODEL levels", np.round(lv, 2).tolist())

# Y map: risers onto the even targets, unchanged a couple of metres outside the flights.
ykm = np.r_[10.0, risers, 56.0]; ykt = np.r_[10.0, Ry, 56.0]
assert np.all(np.diff(ykm) > 0) and np.all(np.diff(ykt) > 0)
y2 = np.interp(y, ykm, ykt)
# Level map (near the treads): each tread level onto its target, risers in between stretched.
zkm = np.r_[-50.0, lv, 60.0]; zkt = np.r_[-50.0, Lz, 60.0 + (Lz[-1] - lv[-1])]
assert np.all(np.diff(zkm) > 0)
z_near = np.interp(z, zkm, zkt)
# Slope map (well above the treads): shift by the difference of the two nosing lines.
nm_y = np.r_[risers, risers[-1] + 3]; nm_z = np.r_[lv[1:], lv[-1]]
nt_y = np.r_[Ry, Ry[-1] + 3];         nt_z = np.r_[Lz[1:], Lz[-1]]
ramp_m = np.interp(y, nm_y, nm_z, left=lv[0]); ramp_t = np.interp(y2, nt_y, nt_z, left=Lz[0])
z_far = z + ramp_t - ramp_m

def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
far = smooth(0.6, 1.4, z - ramp_m)
z2 = z_near * (1 - far) + z_far * far
# Width: the Tripo walk (|x| < 7) and balustrade (7..8.2) onto ours (|x| < 8, rail 8..8.8).
ax = np.abs(x)
x2 = np.sign(x) * np.interp(ax, [0, 7.0, 8.2, 10.5, 100], [0, 8.0, 8.8, 10.5, 100])
wx = (smooth(13.0, 14.0, y) * (1 - smooth(52.0, 54.5, y)))
x2 = x * (1 - wx) + x2 * wx

# Only the stair and what stands beside it.
region = (1 - smooth(10.5, 12.0, ax)) * smooth(11.5, 13.0, y) * (1 - smooth(54.0, 56.0, y))
out = np.c_[x + (x2 - x) * region, y + (y2 - y) * region, z + (z2 - z) * region]

Mi = np.linalg.inv(M)
me.vertices.foreach_set("co", ((np.c_[out, np.ones(n)] @ Mi.T)[:, :3]).ravel())
me.update()

# Delete the blockout stair (flights, middle landing, their rails). Drain, rolling holes and the central landing stay.
STAIR = re.compile(r"^B1_GrandStair/(Lower_flight_(step_\d+|rail_[EW])|Upper_flight_(step_\d+|rail_[EW])|Middle_landing(_rail_[EW])?)$")
gone = [o for o in bpy.data.objects if o.type == "MESH" and STAIR.match(re.sub(r"#\d+$", "", o.name))]
for o in gone: bpy.data.objects.remove(o, do_unlink=True)
print("DELETED", len(gone))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v5.blend"))
print("SAVED")
