# 승규 11-03, on walls_v30.py's trim map (white lines on black): "반대로 된거 같아 검은색이 원래 돌 텍스쳐 흰색이 골드색이야".
# The map itself is the design: every face whose centre lies on a white line is gold, every other face stone - no
# longer only the mouldings' sides (v30 left the flat faces on a line in stone, so the gold broke up). The map keeps
# its outer frame (the module's own edges are gold too, as the picture). Back faces (looking away from the decorated
# side) stay stone. From grand_stair_hall_v31C.blend (R335's walls, no banners).
# Run: blender -b grand_stair_hall_v31C.blend -P walls_v32.py -> grand_stair_hall_v32C.blend, renders_v38/*.png
import bpy, math, os
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
HY, R0, FLOOR, TOP, W, H = 92.0, 31.0, -4.0, 26.0, 0.98, 0.59
ARCS = ((108.0, 234.0), (306.0, 432.0))
FIT = 0.567                                  # round_hall_v22.py moved the wall this far in (its FIT print)
sc = bpy.context.scene
STONE, GOLD = bpy.data.materials["Palace_Stone"], bpy.data.materials["Palace_Gold"]

# ---- the trim map
Dm = np.load(os.path.join(HERE, "textures", "WallModule_front.npy"))
lo_m, hi_m = np.load(os.path.join(HERE, "textures", "WallModule_bounds.npy"))
NZ, NX = Dm.shape
F = np.where(np.isnan(Dm), np.nanmax(Dm) + .02, Dm)


def box(a, r):
    k = 2 * r + 1
    c = np.pad(np.pad(a, r, mode="edge").cumsum(0).cumsum(1), ((1, 0), (1, 0)))
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


gy, gx = np.gradient(box(F, 1))
TRIM = box((np.hypot(gx, gy) > .0015).astype(float), 2) > 0
print("TRIM map %d x %d, %.1f %% lines" % (NX, NZ, 100 * TRIM.mean()))


END = .40                                    # the end pilasters (|x| > 0.40): where a wall stops or meets the next


def on_trim(ax, az):
    i = int(round((ax - lo_m[0]) / (hi_m[0] - lo_m[0]) * (NX - 1)))
    j = int(round((az - lo_m[2]) / (hi_m[2] - lo_m[2]) * (NZ - 1)))
    return 0 <= i < NX and 0 <= j < NZ and TRIM[j, i]


def paint(me, where):
    """where(face) -> True for gold. Slot 0 stone, 1 gold."""
    me.materials.clear(); me.materials.append(STONE); me.materials.append(GOLD)
    g = 0
    for p in me.polygons:
        gold = where(p)
        p.material_index = 1 if gold else 0
        g += gold
    return g, len(me.polygons)


# ---- module walls, in the asset's frame (front -y, the decorated side y < 0.03)
done = set()
for name in ("Outer_wall_E_0", "Outer_wall_S_east", "Outer_wall_S_west", "Foyer_side_E", "Foyer_side_W"):
    me = bpy.data.objects[name].data
    if me.name in done:
        continue
    done.add(me.name)
    g, n = paint(me, lambda p: p.normal.y < .5 and on_trim(p.center.x, p.center.z))
    print("WALL", name, "(%s, %d users)" % (me.name, me.users), "gold %d / %d" % (g, n))

# ---- the round wall, through the bend back to the asset's frame
for ai, (a0, a1) in enumerate(ARCS):
    Lm = R0 * math.radians(a1 - a0) / 2
    sx = Lm / W
    for k in range(2):
        me = bpy.data.objects["Round_wall_%d_%d" % (ai, k)].data

        def gold(p, a1=a1, k=k, Lm=Lm, sx=sx):
            c = p.center
            r = math.hypot(c.x, c.y - HY)
            phi = math.degrees(math.atan2(c.y - HY, c.x)) % 360
            while phi < a1 - 180:
                phi += 360
            s = math.radians(a1 - phi) * R0
            ax = lo_m[0] + (s - k * Lm) / sx
            az = lo_m[2] + (c.z - FLOOR) * H / (TOP - FLOOR)
            front = -Vector((c.x, c.y - HY, 0)).normalized()
            return p.normal.dot(front) > -.5 and on_trim(ax, az)

        g, n = paint(me, gold)
        print("WALL Round_wall_%d_%d gold %d / %d" % (ai, k, g, n))

for name in ("Entrance_archway", "Entrance_base_E", "Entrance_base_W", "Corner_column_NE", "Corner_column_NW"):
    g, n = paint(bpy.data.objects[name].data, lambda p: False)
    print("STONE", name, n)

# ---- v29's banners go
bc = bpy.data.collections.get("Palace_banners_v29")
if bc:
    gone = 0
    for o in list(bc.objects):
        if o.type == "MESH":
            bpy.data.objects.remove(o); gone += 1
    print("BANNERS removed", gone)

# ---- previews
cams = []
for name, loc, at, lens in (("wall_east", (8, 58, 12), (34, 74, 9), 26), ("wall_base", (24, 66, -1), (34, 78, 2), 24),
                            ("round_wall", (0, 100, 17), (-12, 120, 16), 22), ("foyer_out", (34, 6, 2), (21, 6, 3), 24)):
    cd = bpy.data.cameras.new(name); cd.lens = lens
    o = bpy.data.objects.new("Cam32_" + name, cd); sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cams.append(o)
out = os.path.join(HERE, "renders_v38"); os.makedirs(out, exist_ok=True)
for c in cams:
    sc.camera = c
    sc.render.filepath = os.path.join(out, c.name[6:] + ".png")
    bpy.ops.render.render(write_still=True)
sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v32C.blend"))
