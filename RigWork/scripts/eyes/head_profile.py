# Print the low-poly pawn's body-lathe rings above the collar: per ring the vertex azimuths and radii.
import bpy, math, sys
from collections import defaultdict
blend = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.open_mainfile(filepath=blend)
pawn = bpy.data.objects["Pawn"]
me = pawn.data
gidx = {g.index for g in pawn.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Foot"))}
rings = defaultdict(list)
for v in me.vertices:
    if sum(g.weight for g in v.groups if g.group in gidx) >= 0.5:
        continue
    if v.co.z > 0.45:
        rings[round(v.co.z, 4)].append(v.co)
for z in sorted(rings):
    pts = rings[z]
    rs = [math.hypot(p.x, p.y) for p in pts]
    angs = sorted((math.degrees(math.atan2(p.x, -p.y)), math.hypot(p.x, p.y)) for p in pts if math.hypot(p.x, p.y) > 1e-4)
    print("z %.4f n %2d r %.4f..%.4f  " % (z, len(pts), min(rs), max(rs)) + " ".join("%.0f:%.3f" % t for t in angs))
