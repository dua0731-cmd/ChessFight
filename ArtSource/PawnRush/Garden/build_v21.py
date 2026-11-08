"""R277: v20 -> v21. Run in Blender 5.2 (or the bpy 5.2 module):
blender --background --python build_v21.py   (tree GLBs default to source_glb/stylized_tree_A/B.glb)

1. Torn plaza walls (user video, 11-01):
   a. The rotor separated in R275 still carried fragments of the static inner curb ring and hub floor ring, so
      curb pieces swept round with the arms and left gaps. They return to a static object at their rest pose.
   b. The north/south openings cut in R273 left open, hollow wall and flower-bed sections. Remove everything inside
      the two entry channels, extend the floor infills over the cut seam and close both sides with stone piers,
      coping and end posts.
   c. A static stone collar covers the ragged seam between the plaza floor and the turning hub disc.
2. Trees: the twelve merged prototype trees become the two supplied stylized tree GLBs (decimated, shared meshes).
3. Plaza collision: a hidden smooth heightfield (pavement flattened, rim/beds kept) replaces the 496k-face
   decorative plaza mesh as the collider; repaired pavement patches are lowered so they do not form ledges.
"""
import bpy, bmesh, math, random, sys
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from pathlib import Path

folder = Path(__file__).resolve().parent
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
tree_files = args[:2] or [str(folder / 'source_glb' / f'stylized_tree_{k}.glb') for k in 'AB']
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v20.blend'))
scene = bpy.context.scene
scene.frame_set(1)
col = scene.collection
plaza = bpy.data.objects['V18 | round garden plaza | diameter36m']
stone = plaza.data.materials[0]

# Entry channels in world space: (y_outer_rim, inward direction). Width matches the 11.6 m infills.
HALF = 5.8
ENTRIES = {'S': (-53.0, 1.0), 'N': (-17.0, -1.0)}
DEPTH = 5.2  # the old channel and its cut planter slabs end about 4.5-5.0 m inside the rim
REACH = 6.3  # raised curb remnants of the cut flower beds reach about 5.9 m inside the rim


def in_channel(p):
    # Everything in the channel, plus raised remnants (curbs, bed slabs) a little further in.
    if abs(p.x) >= HALF:
        return False
    for y0, s in ENTRIES.values():
        d = (p.y - y0) * s
        if -1.0 < d < DEPTH or (DEPTH <= d < REACH and p.z > 0.06):
            return True
    return False


# 1a. Delete the hollow, cut-open plaza geometry inside both channels.
bm = bmesh.new(); bm.from_mesh(plaza.data)
mw = plaza.matrix_world
doomed = [f for f in bm.faces if in_channel(mw @ f.calc_center_median())]
print('plaza faces removed', len(doomed))
bmesh.ops.delete(bm, geom=doomed, context='FACES')
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(plaza.data); bm.free(); plaza.data.update()

# 1a2. The rotor separated from the plaza in R275 kept fragments of the static plaza: inner flower-bed curb pieces
# (r 13.2-13.8 m) and hub floor-ring pieces (r 2.5-3.1 m). They spun with the arms, tearing gaps in the curb.
# Move them back into a static object at their rest pose (frame 1 = the source pose).
rotor = bpy.data.objects['V20 | clockwise plaza rotor']
hub = rotor.parent.matrix_world.translation
rm = rotor.matrix_world
bm = bmesh.new(); bm.from_mesh(rotor.data); bm.faces.index_update()
seen, static_faces = set(), []
for f in bm.faces:
    if f.index in seen:
        continue
    stack, part = [f], []
    seen.add(f.index)
    while stack:
        g = stack.pop(); part.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen:
                    seen.add(h.index); stack.append(h)
    pts = [rm @ v.co for g in part for v in g.verts]
    r = [math.hypot(p.x - hub.x, p.y - hub.y) for p in pts]
    top = max(p.z for p in pts)
    if (min(r) > 12.5 and top < 0.8) or (min(r) > 2.4 and max(r) < 3.2 and top < 0.5):
        static_faces += part
print('rotor faces returned to the plaza', len(static_faces))
moved = {f.index for f in static_faces}
keep = bm.copy()
bmesh.ops.delete(bm, geom=static_faces, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(rotor.data); bm.free()
bmesh.ops.delete(keep, geom=[f for f in keep.faces if f.index not in moved], context='FACES')
bmesh.ops.delete(keep, geom=[v for v in keep.verts if not v.link_faces], context='VERTS')
keep.transform(rm)
# The returned curb also crossed the south entry channel: cut it like the plaza.
bmesh.ops.delete(keep, geom=[f for f in keep.faces if in_channel(f.calc_center_median())], context='FACES')
bmesh.ops.delete(keep, geom=[v for v in keep.verts if not v.link_faces], context='VERTS')
curb_me = bpy.data.meshes.new('V21 | plaza curb returned from rotor')
keep.to_mesh(curb_me); keep.free()
for m in rotor.data.materials:
    curb_me.materials.append(m)
curb = bpy.data.objects.new('V21 | plaza curb returned from rotor', curb_me); col.objects.link(curb)

# 1a3. The plaza hole under the rotor hub is ragged (r 2.8-3.0 m) and the turning base disc ends near r 2.7 m,
# so dark gaps showed round the hub as it turned. A static stone collar covers the seam, flush with the disc top.
def collar(name, x, y, r_in, r_out, z0, z1, n=72):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    profile = [(r_in, z0), (r_in, z1), (r_out - .1, z1), (r_out, z1 - .07), (r_out, z0)]
    rings = [[bm.verts.new((x + r * math.cos(a), y + r * math.sin(a), z)) for r, z in profile]
             for a in (math.tau * i / n for i in range(n))]
    for i in range(n):
        a, b = rings[i], rings[(i + 1) % n]
        for k in range(len(profile)):
            bm.faces.new((a[k], b[k], b[(k + 1) % len(profile)], a[(k + 1) % len(profile)]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    me.materials.append(stone)
    return o


hub_collar = collar('V21 | plaza hub collar', hub.x, hub.y, 2.55, 3.25, -0.3, 0.47)

# 1b. Extend each infill to REACH + 0.05 m inside the rim so the cut seam is fully covered.
for name, (y0, s) in (('V18 | plaza entry floor infill', ENTRIES['S']), ('V18 | plaza entry floor infill.001', ENTRIES['N'])):
    o = bpy.data.objects[name]
    inner = max(((o.matrix_world @ v.co - Vector((0, y0, 0))).y * s for v in o.data.vertices))
    for v in o.data.vertices:
        w = o.matrix_world @ v.co
        if abs((w.y - y0) * s - inner) < 1e-3:
            w.y = y0 + s * (REACH + 0.05)
            v.co = o.matrix_world.inverted() @ w
    o.data.update()


def block(name, x0, x1, y0, y1, z0, z1, bevel):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((x0 + (v.co.x + .5) * (x1 - x0), y0 + (v.co.y + .5) * (y1 - y0), z0 + (v.co.z + .5) * (z1 - z0)))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, affect='EDGES', profile=.5)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    me.materials.append(stone)
    return o


def post(name, x, y, z0):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    for r, a, b in ((.52, z0, 1.30), (.64, 1.30, 1.44), (.40, 1.44, 1.52)):
        g = bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=r, radius2=r, depth=b - a)
        bmesh.ops.translate(bm, vec=Vector((x, y, (a + b) / 2)), verts=g['verts'])
    g = bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=.26)
    bmesh.ops.translate(bm, vec=Vector((x, y, 1.70)), verts=g['verts'])
    bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = True
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    me.materials.append(stone)
    return o


# 1c. Close each cut side: pier wall (covers the cut faces), coping, and a round post at the outer end.
added = []
for key, (y0, s) in ENTRIES.items():
    for side, sx in (('W', -1.0), ('E', 1.0)):
        xa, xb = sorted((sx * 5.72, sx * 6.62))
        ya, yb = sorted((y0 + s * 0.25, y0 + s * (REACH + 0.05)))
        added.append(block(f'V21 | plaza entry pier {key}{side}', xa, xb, ya, yb, -1.5, 1.18, .05))
        xa, xb = sorted((sx * 5.62, sx * 6.72))
        ya, yb = sorted((y0 + s * 0.15, y0 + s * (REACH + 0.15)))
        added.append(block(f'V21 | plaza entry coping {key}{side}', xa, xb, ya, yb, 1.18, 1.30, .04))
        added.append(post(f'V21 | plaza entry post {key}{side}', sx * 6.17, y0 + s * 0.55, -1.5))

# 2. Trees.
old = bpy.data.objects['V04_장식정원_나무']
mats = {m.name: m for m in old.data.materials}
old.hide_set(True); old.hide_render = True
trunk_mat, leaf_mat, dark_mat = mats['G26_나무줄기'], mats['G12_잎'], mats['G13_짙은잎']
spots = [(sx * 38.6, y) for sx in (-1, 1) for y in (23.0, 27.2, 31.4, 35.6, 39.8, 44.0)]
tree_meshes = []
for i, path in enumerate(tree_files):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    imported = [o for o in bpy.data.objects if o not in before]
    src = [o for o in imported if o.type == 'MESH'][0]
    for o in imported:
        o.select_set(o == src)
    bpy.context.view_layer.objects.active = src
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    dec = src.modifiers.new('decimate', 'DECIMATE'); dec.ratio = 0.035
    bpy.ops.object.modifier_apply(modifier=dec.name)
    me = src.data
    me.name = f'V21 | stylized tree {"AB"[i]} mesh'
    # Untextured asset: colour by part. Trunk = low and near the axis; everything else is foliage.
    zs = [v.co.z for v in me.vertices]; h = max(zs) - min(zs); zmin = min(zs)
    me.materials.clear(); me.materials.append(trunk_mat); me.materials.append(leaf_mat if i == 0 else dark_mat)
    trunk_top, trunk_r = (.31, .22) if i == 0 else (.27, .17)
    for p in me.polygons:
        c = p.center
        p.material_index = 0 if (c.z - zmin) / h < trunk_top and math.hypot(c.x, c.y) / h < trunk_r else 1
        p.use_smooth = True
    for o in imported:
        bpy.data.objects.remove(o)
    tree_meshes.append((me, h))
    print('tree', i, len(me.polygons), 'faces')
rng = random.Random(277)
for n, (x, y) in enumerate(spots):
    me, h = tree_meshes[n % len(tree_meshes)]
    o = bpy.data.objects.new(f'V21 | stylized tree {n + 1:02d}', me); col.objects.link(o)
    size = rng.uniform(3.9, 4.4) / h
    o.location = (x, y, 5.03); o.rotation_euler = (0, 0, rng.uniform(0, math.tau)); o.scale = (size, size, size)
    added.append(o)

# 3. Lower repaired pavement patches to just above the pavement (was a 0.08 m ledge).
for o in bpy.data.objects:
    if o.name.startswith('V20 | repaired pavement'):
        o.location.z -= 0.05

# 3b. Smooth plaza collision heightfield: 0.2 m grid, pavement flattened to one level, rim/beds kept.
dg = bpy.context.evaluated_depsgraph_get()
sources = [plaza, curb, hub_collar] + [o for o in bpy.data.objects if o.name.startswith('V20 | repaired pavement')]
trees = []
for o in sources:
    m = o.evaluated_get(dg).to_mesh()
    trees.append(BVHTree.FromPolygons([o.matrix_world @ v.co for v in m.vertices], [p.vertices[:] for p in m.polygons]))
    o.evaluated_get(dg).to_mesh_clear()
STEP, FLOOR = 0.2, 0.02
x0, y0g = -18.2, -53.2
nx, ny = int(36.4 / STEP) + 1, int(36.4 / STEP) + 1
grid = {}
for i in range(nx):
    for j in range(ny):
        x, y = x0 + i * STEP, y0g + j * STEP
        best = None
        for t in trees:
            hit = t.ray_cast(Vector((x, y, 6)), Vector((0, 0, -1)))
            if hit[0] is not None and hit[0].z > -0.4 and (best is None or hit[0].z > best):
                best = hit[0].z
        if best is not None:
            grid[i, j] = FLOOR if best < 0.12 else best
me = bpy.data.meshes.new('V21 | plaza collision proxy')
bm = bmesh.new(); verts = {}
for (i, j), z in grid.items():
    verts[i, j] = bm.verts.new((x0 + i * STEP, y0g + j * STEP, z))
for i in range(nx - 1):
    for j in range(ny - 1):
        q = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
        if all(k in verts for k in q):
            bm.faces.new([verts[k] for k in q])
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.0), verts=bm.verts, edges=bm.edges)
bmesh.ops.triangulate(bm, faces=bm.faces)
bm.to_mesh(me); bm.free()
proxy = bpy.data.objects.new('V21 | plaza collision proxy', me); col.objects.link(proxy)
proxy['unity_collision_proxy'] = True
proxy.display_type = 'WIRE'; proxy.hide_render = True
print('proxy triangles', len(me.polygons))

bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v21.blend'), compress=True)
print('SAVED v21', len(added), 'new objects')
