"""R278: v21 -> v22. Run in Blender 5.2 (or the bpy 5.2 module): blender --background --python build_v22.py

1. Robot arms (user video 11-01: the weights swayed about 1-1.5 m above the walkway and the clay tore at the elbow).
   a. Both arms grow 1.25x about the shoulder so the weight can reach the walkway (floor z 5.0) at its middle.
   b. New 6 s loop solved by two-joint IK on a target path: wind up high on its own side, a hard sweep that skims
      0.25 m above the walkway, a hold on the far side, a slower swing back. Robot 2 mirrors robot 1, half a loop later.
   c. A knuckle sphere at each elbow covers the hinge so the meshes do not open when the elbow bends.
2. Waterfalls (user video: falls in mid air).
   a. The terrace-tank curtains started 1 m above the tanks (4.9 vs 3.88 m): fit them to the tank lip and give each
      tank a water surface they pour from.
   b. The reservoir side and front outlets came out of bare wall: add stone spout lips over their tops.
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix
from pathlib import Path

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v21.blend'))
scene = bpy.context.scene
col = scene.collection
O = bpy.data.objects
FPS, FRAMES, CYCLE = 30, 721, 6.0
K = 1.25            # arm scale
FLOOR = 5.0         # walkway top under the robots
CLEAR = 0.25        # weight clearance over the walkway at the bottom of the sweep

# ---------------------------------------------------------------- 1. robots
robots = []
for k, pivot_name in (('1', 'V02_필수진자_PIVOT'), ('2', 'V02_필수진자_PIVOT.001')):
    pivot, elbow = O[pivot_name], O['V20 | robot elbow ' + k]
    upper, fore = O[f'V20 | robot {k} upper arm'], O[f'V20 | robot {k} forearm and weight']
    for o in (pivot, elbow):
        if o.animation_data:
            o.animation_data_clear()
        o.rotation_euler = (0, 0, 0)
    robots.append((k, pivot, elbow, upper, fore))
bpy.context.view_layer.update()

clay = O['V20 | robot 1 upper arm'].data.materials[0]
rest = {}
for k, pivot, elbow, upper, fore in robots:
    P = pivot.matrix_world.translation.copy()
    E = elbow.matrix_world.translation.copy()
    for o in (upper, fore):
        if o.data.users > 1:
            o.data = o.data.copy()
    uw = [upper.matrix_world @ v.co for v in upper.data.vertices]
    fw = [fore.matrix_world @ v.co for v in fore.data.vertices]
    weight = [w for w in fw if (w - E).length > 3.0]
    lo = Vector([min(w[i] for w in weight) for i in range(3)]); hi = Vector([max(w[i] for w in weight) for i in range(3)])
    C = (lo + hi) / 2
    radius = (hi.x - lo.x) / 2 * K
    E2 = P + K * (E - P)
    elbow.location = pivot.matrix_world.inverted() @ E2
    bpy.context.view_layer.update()
    inv_u, inv_f = upper.matrix_world.inverted(), fore.matrix_world.inverted()
    for v, w in zip(upper.data.vertices, uw):
        v.co = inv_u @ (P + K * (w - P))
    for v, w in zip(fore.data.vertices, fw):
        v.co = inv_f @ (P + K * (w - P))
    upper.data.update(); fore.data.update()
    C2 = P + K * (C - P)
    # Shoulder: a fixed hinge drum fills the dark slit between the support's socket and the turning hub.
    me = bpy.data.meshes.new(f'V22 | robot {k} shoulder hinge')
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=1.45, radius2=1.45, depth=1.4,
                          matrix=Matrix.Translation(P) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
    bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(clay)
    col.objects.link(O.new(me.name, me))
    # Elbow knuckle: a sphere is the same at every hinge angle, so the bend never shows an open seam.
    me = bpy.data.meshes.new(f'V22 | robot {k} elbow knuckle')
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=16, radius=1.18); bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(clay)
    knuckle = O.new(f'V22 | robot {k} elbow knuckle', me); col.objects.link(knuckle)
    knuckle.parent = elbow
    knuckle.matrix_world = Matrix.Translation(E2)
    rest[k] = dict(P=P, u0=E2 - P, v0=C2 - E2, L1=(E2 - P).length, L2=(C2 - E2).length, r=radius)
    print('robot', k, 'L1 %.2f L2 %.2f weight r %.2f' % (rest[k]['L1'], rest[k]['L2'], radius))


def ang(v):
    return math.atan2(v.z, v.x)


def bezier(a, c, b, s):
    return (1 - s) ** 2 * a + 2 * s * (1 - s) * c + s * s * b


def solve(R, target, sign):
    P, L1, L2 = R['P'], R['L1'], R['L2']
    d = target - P; d.y = 0
    dist = min(max(d.length, abs(L1 - L2) + 0.05), L1 + L2 - 0.05)
    alpha = math.acos(max(-1, min(1, (L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist))))
    base = ang(d)
    for a in (base + alpha, base - alpha):
        u = Vector((math.cos(a), 0, math.sin(a))) * L1
        v = d.normalized() * dist - u
        if (u.x * v.z - u.z * v.x) * sign > 0:
            return u, v
    raise RuntimeError('no IK branch')


def phase(t):
    """0..1 along the path (0 = wound up, 1 = far side) for loop time t in seconds."""
    t %= CYCLE
    if t < 1.0:
        return 0.0
    if t < 1.85:                       # hard sweep: fastest where the weight skims the walkway
        u = (t - 1.0) / 0.85
        return 0.5 - 0.5 * math.cos(math.pi * u)
    if t < 2.6:
        return 1.0
    if t < 4.4:                        # slower swing back
        u = (t - 2.6) / 1.8
        return 1 - u * u * (3 - 2 * u)
    return 0.0


for k, pivot, elbow, upper, fore in robots:
    R = rest[k]
    m = -1.0 if k == '1' else 1.0      # robot 1 stands on -x and sweeps toward +x; robot 2 mirrors it
    y = R['P'].y
    low_z = FLOOR + R['r'] + CLEAR
    W = Vector((m * 2.0, y, 11.6)); L = Vector((0.0, y, low_z)); F = Vector((-m * 2.6, y, 8.6))
    Cc = 2 * L - (W + F) / 2
    sign = -1.0 if k == '1' else 1.0   # elbow raised above the shoulder-weight line ("excavator" bend)
    offset = 0.0 if k == '1' else CYCLE / 2
    prev = None
    lowest = 99
    for f in range(1, FRAMES + 1):
        t = (f - 1) / FPS + offset
        s = phase(t)
        target = bezier(W, Cc, F, s)
        target.z = max(target.z, low_z)
        u, v = solve(R, target, sign)
        ts = ang(R['u0']) - ang(u)
        te = ang(R['v0']) - ts - ang(v)
        if prev:
            ts += round((prev[0] - ts) / math.tau) * math.tau
            te += round((prev[1] - te) / math.tau) * math.tau
        prev = (ts, te)
        pivot.rotation_euler = (0, ts, 0); pivot.keyframe_insert('rotation_euler', index=1, frame=f)
        elbow.rotation_euler = (0, te, 0); elbow.keyframe_insert('rotation_euler', index=1, frame=f)
        lowest = min(lowest, (R['P'] + u + v).z - R['r'])
    print('robot', k, 'lowest weight bottom %.2f (floor %.1f)' % (lowest, FLOOR))

# ---------------------------------------------------------------- 2a. terrace tank falls
tank_top = 3.88
deco = O['V04_장식정원_꽃과폭포']
fall_slots = {i for i, m in enumerate(deco.data.materials) if m and m.name.startswith('G10')}
bm = bmesh.new(); bm.from_mesh(deco.data)
mw = deco.matrix_world; inv = mw.inverted()
moved = set()
for f in bm.faces:
    if f.material_index not in fall_slots:
        continue
    c = mw @ f.calc_center_median()
    if 27.0 < abs(c.x) < 37.2 and 15.0 < c.y < 16.7:
        moved.update(f.verts)
z0, z1 = -1.52, 4.9
for v in moved:
    w = mw @ v.co
    w.z = z0 + (w.z - z0) * (tank_top - 0.02 - z0) / (z1 - z0)
    v.co = inv @ w
bm.to_mesh(deco.data); bm.free(); deco.data.update()
print('tank fall vertices refit', len(moved))

calm = bpy.data.materials['V07 | calm water']
for name in ('V19 | V02_장식_폭포수조', 'V19 | V02_장식_폭포수조.001'):
    tank = O[name]
    pts = [tank.matrix_world @ Vector(p) for p in tank.bound_box]
    x0, x1 = min(p.x for p in pts) + .35, max(p.x for p in pts) - .35
    y0, y1 = min(p.y for p in pts) + .12, max(p.y for p in pts) - .35
    me = bpy.data.meshes.new(name.replace('V19 | V02_장식_폭포수조', 'V22 | tank water'))
    me.from_pydata([(x0, y0, tank_top + .03), (x1, y0, tank_top + .03), (x1, y1, tank_top + .03), (x0, y1, tank_top + .03)], [], [(0, 1, 2, 3)])
    me.materials.append(calm)
    col.objects.link(O.new(me.name, me))

# ---------------------------------------------------------------- 2b. spout lips over the reservoir outlets
stone = O['V18 | round garden plaza | diameter36m'].data.materials[0]


def lip(name, x0, x1, y0, y1, z0, z1):
    me = bpy.data.meshes.new(name); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((x0 + (v.co.x + .5) * (x1 - x0), y0 + (v.co.y + .5) * (y1 - y0), z0 + (v.co.z + .5) * (z1 - z0)))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=.04, segments=2, affect='EDGES', profile=.5)
    bm.to_mesh(me); bm.free(); me.materials.append(stone)
    o = O.new(name, me); col.objects.link(o)


for sx, side in ((1, 'E'), (-1, 'W')):
    for n, (ya, yb) in enumerate(((38.3, 41.15), (46.3, 49.2))):
        xa, xb = sorted((sx * 18.9, sx * 19.85))
        lip(f'V22 | reservoir side spout {side}{n + 1}', xa, xb, ya, yb, 3.05, 3.38)
    xa, xb = sorted((sx * 10.3, sx * 15.7))
    lip(f'V22 | reservoir front spout {side}', xa, xb, 29.45, 30.15, 3.18, 3.48)

bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v22.blend'), compress=True)
print('SAVED v22')
