# Chandeliers from source/crystal_chandelier.glb hung on source/chunky_chain.glb, replacing v9's lathe
# chandeliers and thin chain rods, and the falling chandelier as the game plays it (ChandelierRain: sways
# 1.2 s, falls in 0.4 s, its pieces lie 1.5 s). The five over the central landing drop one after another
# (the one over the king's dais never falls, as in the game): the chain snaps, the chandelier comes down
# whole, and from the impact on its real parts (arms, cups, candles, crystals) are rigid bodies that break
# away and scatter. The simulation is baked to keyframes, so nothing needs the physics cache.
# No paint: every new piece uses v9's grey Arch_Clay.
# Run: blender -b grand_stair_hall_v9.blend -P chandelier_fall_v9.py   -> grand_stair_hall_v10.blend
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 60
SWAY, FALL, LIE = 1.2, 0.4, 2.0          # seconds: the game's warning, fall and (a little more than) debris
STARTS = [0.0, 0.9, 1.9, 2.8, 3.8]       # one drop every 0.9 ~ 1.0 s, as the game's 0.7 ~ 1.4 s gaps
CH_SCALE = 4.0                           # asset 0.71 m wide -> 2.85 m (v9's was 2.6 m), 4.0 m tall
CHAIN_SCALE = 1.3                        # a link about 0.37 m wide
FLOOR_LANDING = 8.0
random.seed(7)

CLAY = bpy.data.materials.get("Arch_Clay")
sc = bpy.context.scene
col = bpy.data.collections.new("Chandeliers_v10"); sc.collection.children.link(col)
ret = bpy.data.collections.get("_Blockout originals")


def retire(o):
    for c in list(o.users_collection):
        c.objects.unlink(o)
    ret.objects.link(o)


def clay(me):
    me.materials.clear(); me.materials.append(CLAY)


# ---------------------------------------------------------------- the two assets
def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        for c in list(o.users_collection):
            c.objects.unlink(o)
    return new


# Chandelier parts: each tripo part is one piece, baked into its own mesh with the origin at its centre.
PARTS = []                               # (mesh, local centre in asset space, metres)
for o in load(os.path.join(HERE, "source", "crystal_chandelier.glb")):
    if o.type != "MESH":
        continue
    me = o.data.copy(); me.transform(o.matrix_world)
    c = sum((v.co for v in me.vertices), Vector()) / len(me.vertices)
    me.transform(Matrix.Translation(-c))
    me.transform(Matrix.Scale(CH_SCALE, 4))
    clay(me)
    PARTS.append((me, c * CH_SCALE))
TOP = max(c.z for _, c in PARTS) + 0.06 * CH_SCALE   # the hook's top

# Chain: the main island without its ceiling plate, the long axis turned to +Z (plate end up).
src = [o for o in load(os.path.join(HERE, "source", "chunky_chain.glb")) if o.type == "MESH"][0]
bm = bmesh.new(); bm.from_mesh(src.data); bm.transform(src.matrix_world)
bm.verts.ensure_lookup_table()
seen, isl = set(), []
for v in bm.verts:
    if v.index in seen:
        continue
    st, cur = [v], []; seen.add(v.index)
    while st:
        a = st.pop(); cur.append(a)
        for e in a.link_edges:
            b = e.other_vert(a)
            if b.index not in seen:
                seen.add(b.index); st.append(b)
    isl.append(cur)
isl.sort(key=len, reverse=True)
P = np.array([tuple(v.co) for v in isl[0]]); m = P.mean(0)
ax = np.linalg.svd(P - m, full_matrices=False)[2]
axis = Vector(ax[0]); side = Vector(ax[1])
keep = set(isl[0])
t = lambda co: (co - Vector(m)).dot(axis)
PLATE = 0.40                              # the plate starts here along the axis (+ end)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v not in keep or t(v.co) > PLATE], context="VERTS")
rot = Matrix((side, axis.cross(side), axis)).to_4x4()   # rows: side -> X, axis -> Z
bm.transform(rot @ Matrix.Translation(-Vector(m)))
zs = [v.co.z for v in bm.verts]
bm.transform(Matrix.Translation((0, 0, -max(zs))))       # top at 0, hangs down
LINK_LEN = (max(zs) - min(zs)) * CHAIN_SCALE
bm.transform(Matrix.Scale(CHAIN_SCALE, 4))
chain_me = bpy.data.meshes.new("ChainSegment"); bm.to_mesh(chain_me); bm.free()
clay(chain_me)
dec = bpy.data.objects.new("_dec", chain_me); sc.collection.objects.link(dec)
mod = dec.modifiers.new("d", "DECIMATE"); mod.ratio = 0.25
bpy.context.view_layer.objects.active = dec
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.data.objects.remove(dec)
for p in chain_me.polygons:
    p.use_smooth = True
PITCH = LINK_LEN * 0.9


def chain(name, x, y, z_top, z_bot):
    """Segments from the ceiling down to the hook, every other one turned 90 degrees so links meet."""
    segs, z, i = [], z_top, 0
    while z - PITCH * 0.5 > z_bot:
        o = bpy.data.objects.new(f"{name}/Chain_{i:02d}", chain_me)
        o.location = (x, y, z); o.rotation_euler = (0, 0, math.pi / 2 * (i % 2))
        col.objects.link(o); segs.append(o)
        z -= PITCH; i += 1
    return segs


# ---------------------------------------------------------------- hang them where v9's were
chm = bpy.data.objects.get("Chandeliers")
if chm:
    retire(chm)
SPOTS = []
for o in sorted([o for o in bpy.data.objects if o.name.startswith("Hall/Chandelier_chain")], key=lambda o: o.name):
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
    x = sum(v.x for v in bb) / 8; y = sum(v.y for v in bb) / 8
    SPOTS.append((x, y, max(v.z for v in bb), min(v.z for v in bb)))
    retire(o)

rig = []
for k, (x, y, ceil, hook) in enumerate(SPOTS):
    name = f"Chandelier_{k + 1}"
    base = Vector((x, y, hook - TOP))                   # the asset's origin so the hook top meets the chain
    pieces = []
    for j, (me, c) in enumerate(PARTS):
        o = bpy.data.objects.new(f"{name}/Part_{j:03d}", me)
        o.location = base + c
        col.objects.link(o); pieces.append(o)
    segs = chain(name, x, y, ceil, hook)
    rig.append((name, Vector((x, y, ceil)), base, pieces, segs))
print("HUNG", len(rig), "parts each", len(PARTS), "chain link", round(LINK_LEN, 2), "m")

# ---------------------------------------------------------------- the drops (the five over the landing)
RELEASE = {}
drops = [r for r in rig if r[1].z < 31 and r[2].z + TOP < 25][:5]   # the dais one hangs from 26.7: stays
if sc.rigidbody_world is None:
    bpy.ops.rigidbody.world_add()
rbw = sc.rigidbody_world
rbw.collection = bpy.data.collections.new("RB_bodies")
rbw.substeps_per_frame = 20; rbw.solver_iterations = 20
sc.render.fps = FPS
END = int((STARTS[-1] + SWAY + FALL + LIE) * FPS)
sc.frame_start, sc.frame_end = 1, END
rbw.point_cache.frame_start, rbw.point_cache.frame_end = 1, END

# Passive floor under the drops: the central landing's top as one thick box.
xs = [r[1].x for r in drops]; ys = [r[1].y for r in drops]
fl_me = bpy.data.meshes.new("_landing_collider")
bmf = bmesh.new(); bmesh.ops.create_cube(bmf, size=1); bmf.to_mesh(fl_me); bmf.free()
floor = bpy.data.objects.new("_Landing_collider", fl_me)
floor.scale = (max(xs) - min(xs) + 30, max(ys) - min(ys) + 30, 2)
floor.location = ((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, FLOOR_LANDING - 1)
sc.collection.objects.link(floor)
floor.select_set(True)
bpy.context.view_layer.objects.active = floor
bpy.ops.rigidbody.object_add(type="PASSIVE"); floor.rigid_body.friction = 0.8
floor.rigid_body.collision_shape = "BOX"; floor.rigid_body.collision_collections = [True] * 20


def pose(t_rel, pivot, axis_angle, depth):
    """The chandelier's whole transform at t_rel seconds after its warning began (the game's Draw)."""
    if t_rel < SWAY:
        dark = t_rel / SWAY
        ang = math.radians(math.sin(t_rel * 9) * (4 + 10 * dark))
        return Matrix.Translation(pivot) @ Matrix.Rotation(ang, 4, axis_angle) @ Matrix.Translation(-pivot)
    u = min((t_rel - SWAY) / FALL, 1.0)
    ang = math.radians(math.sin(SWAY * 9) * 14) * (1 - u)
    return (Matrix.Translation((0, 0, -depth * u * u)) @ Matrix.Translation(pivot)
            @ Matrix.Rotation(ang, 4, axis_angle) @ Matrix.Translation(-pivot))


for n, (name, pivot, base, pieces, segs) in enumerate(drops):
    start = STARTS[n]
    lowest = base.z + min(c.z - 0.05 for _, c in PARTS)
    depth = lowest - FLOOR_LANDING - 0.05                   # straight down until its bottom meets the floor
    swing = Vector((1, 0, 0)) if n % 2 == 0 else Vector((0, 1, 0))
    snap = len(segs) // 2                                   # the chain breaks at its middle: the lower half falls
    falling = pieces + segs[snap:]
    f_snap = int((start + SWAY) * FPS) + 1
    f_hit = int((start + SWAY + FALL) * FPS)
    # Collision groups: the asset's parts were modelled overlapping, and two overlapping hulls set loose
    # blow each other apart. Parts whose boxes touch get different groups (greedy colouring over 19), so
    # a part only hits parts it was clear of, and every part hits the floor (all groups).
    group = {}
    box = lambda o: [o.matrix_world @ Vector(c) for c in o.bound_box]
    span = {o: ([min(v[i] for v in box(o)) - .03 for i in range(3)], [max(v[i] for v in box(o)) + .03 for i in range(3)])
            for o in pieces + segs}
    touch = lambda a, b: all(span[a][0][i] <= span[b][1][i] and span[b][0][i] <= span[a][1][i] for i in range(3))
    for o in sorted(pieces + segs, key=lambda o: -o.dimensions.length):
        used = [0] * 19
        for p, g in group.items():
            if touch(o, p):
                used[g] += 1
        group[o] = min(range(19), key=lambda g: (used[g], random.random()))
    for o in pieces + segs:
        bpy.context.view_layer.objects.active = o; bpy.ops.rigidbody.object_add(type="ACTIVE")
        rb = o.rigid_body
        rb.collision_shape = "CONVEX_HULL"; rb.friction = 0.8; rb.restitution = 0.1
        rb.linear_damping = 0.3; rb.angular_damping = 0.4
        rb.mass = max(0.2, o.dimensions.x * o.dimensions.y * o.dimensions.z * 300)
        bits = [False] * 20; bits[1 + group[o]] = True
        rb.collision_collections = bits
        rb.kinematic = True
    rest = {o: o.matrix_world.copy() for o in pieces + segs}
    for f in range(1, f_hit + 1):
        tr = (f - 1) / FPS - start
        if tr < 0:
            continue
        # The last frames come down 0.25 m each: the physics takes its speed from the frames before the
        # release - about 15 m/s, a real 10 m drop's - not the game's 0.4 s fall's 48 m/s, which bounced
        # the parts tens of metres.
        if f_hit - 3 <= f <= f_hit:
            above = .25 * (f_hit - f)
            tr = SWAY + FALL * math.sqrt((depth - above) / depth)
        M = pose(tr, pivot, swing, depth)
        for o in pieces + segs:
            if o in segs[:snap] and tr >= SWAY:
                # The upper chain stays where the snap left it.
                M2 = pose(min(tr, SWAY - 1e-3), pivot, swing, depth)
                o.matrix_world = M2 @ rest[o]
            else:
                o.matrix_world = M @ rest[o]
            o.keyframe_insert("location", frame=f); o.keyframe_insert("rotation_euler", frame=f)
    # Released at the impact (the falling half of the chain a frame later: it lands on the pile).
    for o in falling:
        fr = f_hit + (2 if o in segs else 0)
        o.rigid_body.kinematic = True; o.rigid_body.keyframe_insert("kinematic", frame=fr - 1)
        o.rigid_body.kinematic = False; o.rigid_body.keyframe_insert("kinematic", frame=fr)
    for o in segs[:snap]:
        o.rigid_body.kinematic = True; o.rigid_body.keyframe_insert("kinematic", frame=END)
    print("DROP", name, "start", start, "snap frame", f_snap, "impact frame", f_hit, "fall", round(depth, 2), "m")

    for o in falling:
        RELEASE[o] = f_hit + (2 if o in segs else 0)

# The dais one stays still: no rigid body.
# Bake: run the simulation frame by frame and record where each released body is (Blender's own
# "bake to keyframes" needs a window), then drop the simulation and key what was recorded.
rec = {o: [] for o in RELEASE}
for f in range(1, END + 1):
    sc.frame_set(f)
    for o, fr in RELEASE.items():
        if f >= fr:
            rec[o].append((f, o.matrix_world.copy()))
bodies = [o for o in rbw.collection.objects if o is not floor]
bpy.ops.object.select_all(action="DESELECT")
for o in bodies:
    o.select_set(True)
bpy.context.view_layer.objects.active = bodies[0]
bpy.ops.rigidbody.objects_remove()
bpy.ops.rigidbody.world_remove()
bpy.data.objects.remove(floor)
for o, frames in rec.items():
    prev = o.rotation_euler.copy()
    for f, M in frames:
        loc, q, _ = M.decompose()
        o.location = loc
        o.rotation_euler = q.to_euler("XYZ", prev); prev = o.rotation_euler.copy()
        o.keyframe_insert("location", frame=f); o.keyframe_insert("rotation_euler", frame=f)
sc.frame_set(1)
print("BAKED", len(rec), "released bodies over", END, "frames at", FPS, "fps")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v10.blend"))
