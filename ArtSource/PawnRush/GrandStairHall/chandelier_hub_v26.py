# 승규 11-03 (the reference's gilded hall chandelier): "중앙 샹들리에만 레퍼런스 사진 참고해서 텍스쳐 입혀 불빛 나오는 곳에는 실제로
# 빛이 나오게 해". Only the round hall's hub chandelier (v10's Chandelier_6, twice the size, R325) - the others share its
# meshes, so its parts get their own copies first. Its faces are sorted into four materials Unity dresses
# (Course01v4Builder.GrandStairHallModel.cs):
#   CH_Bulb     the candles' flame-shaped tips (their upper half) - emissive, and a point light in each
#   CH_Candle   the candles' lower half, cream wax
#   CH_Crystal  the hanging drops and bead strings: small parts taller than they are wide, off the middle
#   CH_Gold     everything else: the frame, arms, cups, crown and the chain
# Candles are found as spikes: a vertex 0.9 m or more off the axis with nothing higher within 0.4 m across, and only
# a narrow shaft (< 0.21 m) under it, followed down until it widens into its cup.
# Each bulb also gets a tiny marker object, "Hub_chandelier_light_NN", at its middle: exported as its own node, Unity
# hides it and puts a point light there.
# Run: blender -b grand_stair_hall_v25C.blend -P chandelier_hub_v26.py -> grand_stair_hall_v26C.blend, renders_v33/*.png
import bpy, bmesh, math, os
from mathutils import Matrix, Vector, kdtree

HERE = os.path.dirname(os.path.abspath(__file__))
HY = 92.0
sc = bpy.context.scene
RIG = "Chandelier_6"


def mat(name, rgb):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    return m


GOLD, CRYSTAL = mat("CH_Gold", (.86, .64, .27)), mat("CH_Crystal", (.84, .91, .97))
CANDLE, BULB = mat("CH_Candle", (.95, .92, .84)), mat("CH_Bulb", (1.0, .93, .62))
SLOTS = [GOLD, CRYSTAL, CANDLE, BULB]

parts = sorted([o for o in bpy.data.objects if o.name.startswith(RIG + "/Part_")], key=lambda o: o.name)
chains = [o for o in bpy.data.objects if o.name.startswith(RIG + "/Chain_")]
for o in parts:
    o.data = o.data.copy()
if chains:
    cme = chains[0].data.copy(); cme.name = "ChainSegment_hub"
    for o in chains:
        o.data = cme
    cme.materials.clear(); cme.materials.append(GOLD)

# ---- the candles' tips
pts = []
for o in parts:
    M = o.matrix_world
    pts += [M @ v.co for v in o.data.vertices]
kd = kdtree.KDTree(len(pts))
for i, p in enumerate(pts):
    kd.insert(p, i)
kd.balance()
hr = lambda a, b: math.hypot(a.x - b.x, a.y - b.y)
axis_r = lambda p: math.hypot(p.x, p.y - HY)
tips = []
for p in sorted((p for p in pts if axis_r(p) > .9), key=lambda p: -p.z):
    if any(hr(p, t) < .4 for t, _ in tips):
        continue
    near = [q for q, _, _ in kd.find_range(p, 1.2)]
    if any(q.z > p.z + 1e-4 and hr(q, p) < .4 for q in near):
        continue                                       # something higher right beside it: not a tip
    shaft = [q for q in near if p.z - .35 <= q.z <= p.z and hr(q, p) < .5]
    if not shaft or max(hr(q, p) for q in shaft) > .21:
        continue
    # down the candle until it widens into its cup
    bottom, z = p.z - .35, p.z - .35
    while z > p.z - 1.6:
        ring = [q for q in kd.find_range(Vector((p.x, p.y, z)), .45) if abs(q[0].z - z) < .03]
        if ring and max(hr(q[0], p) for q in ring) > .21:
            break
        bottom, z = z, z - .04
    tips.append((p, bottom))
print("CANDLES", len(tips), "tips at", sorted(round(t.z, 2) for t, _ in tips), "lengths",
      sorted(round(t.z - b, 2) for t, b in tips))


def zone(c):
    """3 = bulb, 2 = candle wax, None = not a candle. The widening test stops early where drops hang close by, so
    the narrow shaft on down to 1.3 m under the tip is wax too (the cup flares wider and stays gold)."""
    for t, b in tips:
        if hr(c, t) < .24 and b - .02 <= c.z <= t.z + .02:
            return 3 if c.z >= (t.z + b) / 2 else 2
        if hr(c, t) < .17 and t.z - 1.3 <= c.z < b:
            return 2
    return None


# ---- faces into materials
count = [0, 0, 0, 0]
for o in parts:
    me = o.data
    me.materials.clear()
    for m in SLOTS:
        me.materials.append(m)
    M = o.matrix_world
    zones = [zone(M @ p.center) for p in me.polygons]
    candle = sum(1 for z in zones if z) / max(1, len(zones))
    vs = [M @ v.co for v in me.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
    d, c = hi - lo, (hi + lo) / 2
    crystal = candle < .2 and max(d.x, d.y) <= .75 and d.z >= .35 and d.z >= .9 * max(d.x, d.y) and axis_r(c) > .2
    for p, z in zip(me.polygons, zones):
        p.material_index = z if z else (1 if crystal else 0)
        count[p.material_index] += 1
print("FACES gold %d crystal %d candle %d bulb %d" % tuple(count))

# ---- a marker at each bulb's middle (Unity: a point light)
col = parts[0].users_collection[0]
for i, (t, b) in enumerate(sorted(tips, key=lambda tb: (round(tb[0].z, 1), math.atan2(tb[0].y - HY, tb[0].x)))):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=.06,
                               matrix=Matrix.Translation((t.x, t.y, t.z - (t.z - b) * .25)))
    me = bpy.data.meshes.new("Hub_chandelier_light_%02d" % i); bm.to_mesh(me); bm.free()
    me.materials.append(BULB)
    col.objects.link(bpy.data.objects.new(me.name, me))

# ---- renders: the chandelier close, from the side and from under it
def camera(name, loc, at, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens
    o = bpy.data.objects.new(name, cd); col.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


out = os.path.join(HERE, "renders_v33"); os.makedirs(out, exist_ok=True)
res = sc.render.resolution_x, sc.render.resolution_y
sc.render.resolution_x = sc.render.resolution_y = 1000
for c in (camera("Cam_chandelier_side", (0, 80, 28.5), (0, 92, 28.3), 50),
          camera("Cam_chandelier_under", (6, 84, 18), (0, 92, 28), 35)):
    sc.camera = c
    sc.render.filepath = os.path.join(out, "%s.png" % c.name[4:])
    bpy.ops.render.render(write_still=True)
sc.render.resolution_x, sc.render.resolution_y = res
sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v26C.blend"))
