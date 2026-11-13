"""R279: v22 -> v23. Run in Blender 5.2 (or the bpy 5.2 module): blender --background --python build_v23.py
(tree GLBs default to source_glb/stylized_tree_A/B.glb)

User review 11-01 (screenshots):
1. Plaza marks: three pairs of round through-holes (0.5 m, raised rims) in the rotor plaza. Cut and fill them flat.
2. Trees floated 1.2 m above the terrace (placed at the old planter height 5.03, floor 3.82). Stand them on the floor.
3. Tree leaves: re-import the GLBs at a higher detail level, keep crisp leaf facets (sharp edges by angle) and
   vary the leaf greens in patches.
4. Waterfalls: the separate zig-zag ribbons and the white blocks at their feet are replaced by continuous sheets with
   UVs (curl over the lip, ballistic fall, widening) for a flowing shader, a spray band where each lands, and the
   landing lines for the water shader's foam and rings (Unity: GardenWaterInteraction). The floating terrace tanks
   get stone piers; the centre fall gets a spout.
6. Bushes looked like candy (80-face blobs with flat flower chips). Each blob becomes a leafy bush (single leaves
   with a fold and a drooping tip, three greens) and each chip on it a 3D five-petal flower.
7. Boats: their hull size and travel axis go to the motion file for the water shader's hull foam and wake.
"""
import bpy, bmesh, math, random, sys
from mathutils import Vector, Matrix, noise
from mathutils.bvhtree import BVHTree
from pathlib import Path

folder = Path(__file__).resolve().parent
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
tree_files = args[:2] or [str(folder / 'source_glb' / f'stylized_tree_{k}.glb') for k in 'AB']
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v22.blend'))
scene = bpy.context.scene
scene.frame_set(1)
col = scene.collection
O, M = bpy.data.objects, bpy.data.materials
rng = random.Random(279)
UP = Vector((0, 0, 1))
stone = O['V18 | round garden plaza | diameter36m'].data.materials[0]


def material(name, rgb):
    m = M.get(name) or M.new(name)
    m.use_nodes = True
    bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    m.diffuse_color = (*rgb, 1)
    return m


def islands(obj):
    """Connected face groups: (face index list, material names, bbox min, bbox max, area-weighted normal)."""
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.faces.ensure_lookup_table()
    names = [m.name if m else '' for m in obj.data.materials]
    mw = obj.matrix_world
    seen, out = set(), []
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
        pts = [mw @ v.co for g in part for v in g.verts]
        lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
        nrm = Vector()
        for g in part:
            nrm += (mw.to_3x3() @ g.normal) * g.calc_area()
        out.append(([g.index for g in part], {names[g.material_index] for g in part}, lo, hi,
                    nrm.normalized() if nrm.length > 1e-9 else UP.copy()))
    bm.free()
    return out


def delete_faces(obj, indices):
    if not indices:
        return
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.faces.ensure_lookup_table()
    doomed = [bm.faces[i] for i in sorted(set(indices))]
    bmesh.ops.delete(bm, geom=doomed, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data); bm.free(); obj.data.update()


def new_object(name, verts, faces, mats, face_mats, uvs=None, smooth=False):
    """uvs: optional list of per-layer lists, one (u, v) per face corner in face order."""
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    for m in mats:
        me.materials.append(m)
    me.polygons.foreach_set('material_index', face_mats)
    me.polygons.foreach_set('use_smooth', [smooth] * len(faces))
    for k, layer in enumerate(uvs or []):
        uv = me.uv_layers.new(name='UVMap' if k == 0 else f'UV{k}')
        flat = [c for pair in layer for c in pair]
        uv.data.foreach_set('uv', flat)
    me.update()
    o = O.new(name, me); col.objects.link(o)
    return o


# ---------------------------------------------------------------- 1. plaza marks
plaza = O['V18 | round garden plaza | diameter36m']
PAIRS = [((-8.95, -38.10), (-8.03, -38.25)), ((4.42, -43.39), (5.38, -43.44)), ((9.78, -30.38), (8.92, -29.98))]
bm = bmesh.new(); bm.from_mesh(plaza.data)
mw = plaza.matrix_world


def near(p, pair, r):
    return any((p.x - x) ** 2 + (p.y - y) ** 2 < r * r for x, y in pair)


removed = filled = 0
for pair in PAIRS:
    ref = [f for f in bm.faces if (lambda c: near(c, pair, 0.9) and not near(c, pair, 0.6) and abs(c.z) < .08)(mw @ f.calc_center_median())]
    mat = ref[0].material_index if ref else 0
    # Grow the cut until the rim leaves one closed floor-level loop, then fill it flat.
    for r in (0.42, 0.5, 0.55, 0.62, 0.7):
        doomed = [f for f in bm.faces if (lambda c: c.z > -1.3 and near(c, pair, r))(mw @ f.calc_center_median())]
        removed += len(doomed)
        bmesh.ops.delete(bm, geom=doomed, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        rims = [e for e in bm.edges if e.is_boundary and all(near(mw @ v.co, pair, 1.0) and (mw @ v.co).z > -0.4 for v in e.verts)]
        new_faces = bmesh.ops.holes_fill(bm, edges=rims, sides=0)['faces']
        for f in new_faces:
            f.material_index = mat; f.smooth = False      # a flat patch, facing up
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
        left = [e for e in bm.edges if e.is_boundary and all(near(mw @ v.co, pair, 1.0) and (mw @ v.co).z > -0.4 for v in e.verts)]
        if not left:
            filled += 1
            bmesh.ops.triangulate(bm, faces=[f for f in new_faces if f.is_valid], quad_method='BEAUTY', ngon_method='BEAUTY')
            break
    else:
        raise RuntimeError('plaza mark not closed: %s' % (pair,))
    # Steep slivers of the old rim bend the smooth vertex normals round the patch: drop them, refill, and shade the
    # flat pavement round the patch flat.
    slivers = [f for f in bm.faces if (lambda c: near(c, pair, 1.2) and c.z > -0.5)(mw @ f.calc_center_median())
               and f.normal.z < 0.6]
    bmesh.ops.delete(bm, geom=slivers, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    rims = [e for e in bm.edges if e.is_boundary and all(near(mw @ v.co, pair, 1.4) and (mw @ v.co).z > -0.4 for v in e.verts)]
    for f in bmesh.ops.holes_fill(bm, edges=rims, sides=0)['faces']:
        f.material_index = mat; f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    for f in bm.faces:
        if near(mw @ f.calc_center_median(), pair, 1.3) and f.normal.z > .9:
            f.smooth = False
bm.to_mesh(plaza.data); bm.free(); plaza.data.update()
# The supplied plaza carries custom normals: the rim's tilted ones stayed on the corners round each patch and shaded
# a dark pinch. Point every up-facing corner there straight up.
corner = [tuple(c.vector) for c in plaza.data.corner_normals]
for poly in plaza.data.polygons:
    if poly.normal.z > .9 and any(near(mw @ poly.center, pair, 1.3) for pair in PAIRS):
        for li in poly.loop_indices:
            corner[li] = (0.0, 0.0, 1.0)
plaza.data.normals_split_custom_set(corner)
print('plaza marks: removed', removed, 'faces, filled', filled, 'of', len(PAIRS), 'pairs')

# ---------------------------------------------------------------- 2+3. trees on the floor, more leaf detail
leaf_mid, leaf_dark = M['G12_잎'], M['G13_짙은잎']
leaf_light = material('V23 | 잎 연두', (0.34, 0.56, 0.15))
trunk = M['G26_나무줄기']
old_trees = [o for o in O if o.name.startswith('V21 | stylized tree')]
spots = [(o.location.x, o.location.y) for o in old_trees]
old_meshes = {o.data for o in old_trees}
for o in old_trees:
    O.remove(o)
for me in old_meshes:
    if me.users == 0:
        bpy.data.meshes.remove(me)
tree_meshes = []
for i, path in enumerate(tree_files):
    before = set(O)
    bpy.ops.import_scene.gltf(filepath=path)
    imported = [o for o in O if o not in before]
    src = [o for o in imported if o.type == 'MESH'][0]
    for o in imported:
        o.select_set(o == src)
    bpy.context.view_layer.objects.active = src
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    dec = src.modifiers.new('decimate', 'DECIMATE'); dec.ratio = 0.14
    bpy.ops.object.modifier_apply(modifier=dec.name)
    me = src.data
    me.name = f'V23 | stylized tree {"AB"[i]} mesh'
    zs = [v.co.z for v in me.vertices]; h = max(zs) - min(zs); zmin = min(zs)
    me.materials.clear()
    for m in (trunk, leaf_light, leaf_mid, leaf_dark):
        me.materials.append(m)
    trunk_top, trunk_r = (.31, .22) if i == 0 else (.27, .17)
    bm = bmesh.new(); bm.from_mesh(me)
    for f in bm.faces:
        c = f.calc_center_median()
        if (c.z - zmin) / h < trunk_top and math.hypot(c.x, c.y) / h < trunk_r:
            f.material_index = 0
        else:
            # Patches of light, mid and dark leaves; tree B leans darker.
            n = noise.noise(c * (7.0 / h) + Vector((i * 13.1, 0, 0))) + (0.25 if i == 1 else 0)
            n += (c.z - zmin) / h * -0.5 + 0.2          # sunlit crown a little lighter
            f.material_index = 1 if n < -0.2 else (2 if n < 0.25 else 3)
        f.smooth = True
    for e in bm.edges:                                # crisp leaf facets, soft within a leaf
        if len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(38):
            e.smooth = False
    bm.to_mesh(me); bm.free()
    for o in imported:
        O.remove(o)
    tree_meshes.append((me, h))
    print('tree', 'AB'[i], len(me.polygons), 'faces')
dg = bpy.context.evaluated_depsgraph_get()
trees = []
for n, (x, y) in enumerate(spots):
    hit = scene.ray_cast(dg, Vector((x, y, 12)), Vector((0, 0, -1)))
    floor = hit[1].z if hit[0] else 3.82
    me, h = tree_meshes[n % 2]
    o = O.new(f'V23 | stylized tree {n + 1:02d}', me); col.objects.link(o)
    size = rng.uniform(3.9, 4.4) / h
    o.location = (x, y, floor - 0.04); o.rotation_euler = (0, 0, rng.uniform(0, math.tau)); o.scale = (size,) * 3
    trees.append(o)
print('trees stand on floor heights', sorted({round(t.location.z + .04, 2) for t in trees}))

# ---------------------------------------------------------------- 6. leafy bushes and 3D flowers
LEAF = {  # leaf frame: x across, y along the blade, z out of the leaf
    'M0': (0, 0, 0), 'E1L': (-.30, .25, .06), 'E1R': (.30, .25, .06), 'M1': (0, .28, -.03),
    'E2L': (-.42, .55, .05), 'E2R': (.42, .55, .05), 'M2': (0, .56, -.04),
    'E3L': (-.24, .82, 0), 'E3R': (.24, .82, 0), 'M3': (0, .83, -.02), 'T': (0, 1, -.05)}
LEAF_FACES = [('M0', 'M1', 'E1L'), ('M1', 'M2', 'E2L', 'E1L'), ('M2', 'M3', 'E3L', 'E2L'), ('M3', 'T', 'E3L'),
              ('M0', 'E1R', 'M1'), ('M1', 'E1R', 'E2R', 'M2'), ('M2', 'E2R', 'E3R', 'M3'), ('M3', 'E3R', 'T')]


def bush_template(seed, leaves=96):
    """A bush around the unit sphere: (verts, faces, face material slot 0-2)."""
    r = random.Random(seed)
    V, F, S = [], [], []
    ico = bmesh.new(); bmesh.ops.create_icosphere(ico, subdivisions=2, radius=1.0)
    base = len(V)
    for v in ico.verts:
        V.append(Vector((v.co.x * .8, v.co.y * .8, v.co.z * .86 - .12)))
    for f in ico.faces:
        F.append([base + v.index for v in f.verts]); S.append(2)
    ico.free()
    golden = math.pi * (3 - math.sqrt(5))
    count = 0
    k = 0
    while count < leaves:
        k += 1
        zz = 1 - 2 * (k + .5) / (leaves * 1.35)
        if zz < -.75:
            break
        rad = math.sqrt(max(0, 1 - zz * zz))
        a = golden * k + r.uniform(-.3, .3)
        n = Vector((math.cos(a) * rad, math.sin(a) * rad, zz)).normalized()
        tangent = n.cross(UP)
        if tangent.length < 1e-3:
            tangent = Vector((1, 0, 0))
        tangent.normalize()
        tangent.rotate(Matrix.Rotation(r.uniform(0, math.tau), 3, n))
        Y = (n + tangent * r.uniform(.3, .7) + UP * .35).normalized()
        Z = n - Y * n.dot(Y)
        Z = Z.normalized() if Z.length > 1e-3 else UP.copy()
        X = Y.cross(Z)
        L = r.uniform(.36, .46); W = L * r.uniform(.5, .62)
        origin = n * r.uniform(.66, .78)
        idx = {}
        for key, (lx, ly, lz) in LEAF.items():
            lz -= .25 * ly * ly                       # drooping tip
            idx[key] = len(V)
            V.append(origin + X * (lx * W) + Y * (ly * L) + Z * (lz * L))
        slot = r.choices((0, 1, 2), (.3, .45, .25))[0]
        for face in LEAF_FACES:
            F.append([idx[q] for q in face]); S.append(slot)
        count += 1
    return V, F, S


def flower_geo(center, axis, radius, petal_slot, centre_slot, V, F, S, r):
    """Five cupped petals round a domed centre, facing `axis`."""
    axis = axis.normalized()
    t = axis.cross(UP) if abs(axis.z) < .95 else axis.cross(Vector((1, 0, 0)))
    t.normalize(); b = axis.cross(t)
    spin = r.uniform(0, math.tau)
    for k in range(5):
        a = spin + k * math.tau / 5
        d = t * math.cos(a) + b * math.sin(a)
        s = d.cross(axis)
        pts = [center + axis * (.05 * radius),
               center + d * (.45 * radius) + s * (.32 * radius) + axis * (.14 * radius),
               center + d * (.45 * radius) - s * (.32 * radius) + axis * (.14 * radius),
               center + d * (.95 * radius) + s * (.18 * radius) + axis * (.30 * radius),
               center + d * (.95 * radius) - s * (.18 * radius) + axis * (.30 * radius),
               center + d * (1.12 * radius) + axis * (.36 * radius)]
        i0 = len(V); V.extend(pts)
        for face in ((0, 1, 2), (1, 3, 4, 2), (3, 5, 4)):     # counter-clockwise about `axis`
            F.append([i0 + q for q in face]); S.append(petal_slot)
    i0 = len(V)
    ring = []
    for k in range(6):
        a = k * math.tau / 6
        ring.append(len(V)); V.append(center + (t * math.cos(a) + b * math.sin(a)) * (.26 * radius) + axis * (.16 * radius))
    top = len(V); V.append(center + axis * (.34 * radius))
    for k in range(6):
        F.append([ring[k], ring[(k + 1) % 6], top]); S.append(centre_slot)


FLOWER_MATS = ['G21_꽃_분홍', 'G22_꽃_노랑', 'G23_꽃_흰색', 'G24_꽃_산호', 'G25_꽃_보라']
for src_name, out_name in (('V04_장식정원_꽃과폭포', 'V23 | garden bushes'), ('V04_지형_깃발과화분', 'V23 | planter bushes')):
    src = O[src_name]
    parts = islands(src)
    bushes = [p for p in parts if p[1] <= {'G12_잎', 'G13_짙은잎'} and p[1]]
    chips = [p for p in parts if len(p[1]) == 1 and next(iter(p[1])) in FLOWER_MATS]
    templates = [bush_template(s) for s in (11, 12, 13)]
    V, F, S = [], [], []
    FV, FF, FS = [], [], []
    used_chips, taken = [], set()
    for bi, (faces, mats, lo, hi, nrm) in enumerate(bushes):
        c = (lo + hi) / 2
        rad = (hi - lo) / 2
        rot = Matrix.Rotation(rng.uniform(0, math.tau), 3, 'Z')
        tv, tf, ts = templates[bi % 3]
        base = len(V)
        for v in tv:
            V.append(c + rot @ Vector((v.x * rad.x, v.y * rad.y, v.z * rad.z)))
        for f, s in zip(tf, ts):
            F.append([base + q for q in f]); S.append(s)
        for ci, chip in enumerate(chips):
            cc = (chip[2] + chip[3]) / 2
            if ci not in taken and all(lo[i] - .3 <= cc[i] <= hi[i] + .3 for i in range(3)):
                taken.add(ci)
                rel = cc - c
                out = Vector((rel.x / max(rad.x, .05), rel.y / max(rad.y, .05), rel.z / max(rad.z, .05)))
                out = out.normalized() if out.length > 1e-3 else UP.copy()
                pos = c + Vector((out.x * rad.x, out.y * rad.y, out.z * rad.z)) * 1.05
                size = max((chip[3] - chip[2]).length * .48, .05)
                mat = next(iter(chip[1]))
                flower_geo(pos, out * .6 + UP * .4, size, FLOWER_MATS.index(mat),
                           5 if mat != 'G22_꽃_노랑' else 6, FV, FF, FS, rng)
                used_chips.append(chip)
    delete_faces(src, [i for p in bushes for i in p[0]] + [i for p in used_chips for i in p[0]])
    new_object(out_name, [tuple(v) for v in V], F, [leaf_light, leaf_mid, leaf_dark], S, smooth=True)
    new_object(out_name.replace('bushes', 'flowers'), [tuple(v) for v in FV], FF,
               [M[n] for n in FLOWER_MATS] + [M['G22_꽃_노랑'], M['G26_나무줄기']], FS, smooth=True)
    print(src_name, 'bushes', len(bushes), 'flowers', len(used_chips), 'leaf tris ~', len(F) * 2)
# Bed tops under the bushes read as soil, not a flat green slab.
soil = M['G04_화단흙']
for o in O:
    if '식재덩어리' in o.name and o.type == 'MESH':
        o.data.materials.clear(); o.data.materials.append(soil)

# ---------------------------------------------------------------- 4. waterfalls
G = 9.81


def sheet(name, a, b, out, z0, inset, v0, zw=-1.70, rows=28, seg=.25, spread=.05):
    """Continuous fall from lip a-b (Blender xy at height z0), flowing along `out`; starts `inset` behind the lip."""
    a, b, out = Vector((*a, 0)), Vector((*b, 0)), Vector((*out, 0)).normalized()
    across = b - a; width = across.length; across.normalize()
    n = max(4, math.ceil(width / seg))
    T = math.sqrt(2 * (z0 - (zw - .1)) / G)
    path = [(-inset + inset * k / 3, z0) for k in range(3)]       # on the source surface, then over the lip
    for j in range(rows + 1):
        tau = T * (j / rows) ** 1.25
        path.append((v0 * tau, z0 - .5 * G * tau * tau))
    arc = [0.0]
    for k in range(1, len(path)):
        arc.append(arc[-1] + math.dist(path[k], path[k - 1]))
    V, F, UV0, UV1 = [], [], [], []
    grid = []
    for k, (h, z) in enumerate(path):
        fall = max(0.0, h) / max(v0 * T, 1e-3)
        row = []
        for i in range(n + 1):
            s = i / n
            p = a + across * (width * s + (s - .5) * 2 * spread * width * fall) + out * h
            p.z = z
            row.append(len(V)); V.append(tuple(p))
        grid.append(row)
    for k in range(len(path) - 1):
        for i in range(n):
            q = (grid[k][i], grid[k][i + 1], grid[k + 1][i + 1], grid[k + 1][i])
            F.append(q)
            for ii, kk in ((i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1)):
                UV0.append((width * ii / n, arc[kk]))
                UV1.append((ii / n, arc[kk] / arc[-1]))
    o = new_object(name, V, F, [fall_mat], [0] * len(F), uvs=[UV0, UV1], smooth=True)
    land = path[-1][0]
    e0 = a + out * land; e1 = b + out * land
    return o, (e0, e1, out, width)


def spray(name, e0, e1, out, zw=-1.70, height=1.7):
    """Two crossed mist bands over the landing line."""
    across = (e1 - e0).normalized()
    V, F, UV0 = [], [], []
    for off in (-.28, .28):
        a = e0 - across * .35 + out * off; b = e1 + across * .35 + out * off
        n = max(2, math.ceil((b - a).length / .5))
        base = len(V)
        for j in range(2):
            for i in range(n + 1):
                p = a + (b - a) * (i / n); V.append((p.x, p.y, zw - .08 + j * height))
        for i in range(n):
            F.append((base + i, base + i + 1, base + n + 2 + i, base + n + 1 + i))
            UV0 += [(i / n, 0), ((i + 1) / n, 0), ((i + 1) / n, 1), (i / n, 1)]
    return new_object(name, V, F, [spray_mat], [0] * len(F), uvs=[UV0])


fall_mat = material('V23 | waterfall sheet', (.78, .93, 1.0))
spray_mat = material('V23 | fall spray', (.95, .98, 1.0))
# Old ribbons and white foam blocks out; the reservoir lip sheets hidden.
for src_name in ('V04_장식정원_꽃과폭포', 'V04_지형_폭포', 'V04_중반_분수'):
    src = O[src_name]
    gone = [i for p in islands(src) for i in p[0]
            if (p[1] == {'G11_물거품'}) or (p[1] == {'G10_폭포물'} and src_name != 'V04_중반_분수')]
    delete_faces(src, gone)
for n in ('V07 Waterfall | left reservoir lip', 'V07 Waterfall | right reservoir lip'):
    O[n].hide_set(True); O[n].hide_render = True


def lip(name, x0, x1, y0, y1, z0, z1):
    me = bpy.data.meshes.new(name); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((x0 + (v.co.x + .5) * (x1 - x0), y0 + (v.co.y + .5) * (y1 - y0), z0 + (v.co.z + .5) * (z1 - z0)))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=.05, segments=2, affect='EDGES', profile=.5)
    bm.to_mesh(me); bm.free(); me.materials.append(stone)
    o = O.new(name, me); col.objects.link(o)
    return o


lip('V23 | centre spout', -1.75, 1.75, 17.25, 17.95, 4.32, 4.62)
for sx, side in ((-1, 'W'), (1, 'E')):
    for k, px in enumerate((26.6, 32.0, 37.4)):          # the terrace tanks stood on nothing over the moat
        xa, xb = sorted((sx * (px - .65), sx * (px + .65)))
        lip(f'V23 | tank pier {side}{k + 1}', xa, xb, 17.05, 19.05, -2.0, 1.95)

landings = []
for sx, side in ((-1, 'W'), (1, 'E')):
    _, L = sheet(f'V23 | tank fall {side}', (sx * 27.0, 16.5), (sx * 37.0, 16.5), (0, -1), 3.925, .30, 1.25)
    landings.append((f'tank {side}', L))
    for k, (ya, yb) in enumerate(((38.4, 41.05), (46.4, 49.1))):
        _, L = sheet(f'V23 | reservoir side fall {side}{k + 1}', (sx * 19.85, ya), (sx * 19.85, yb), (sx, 0), 3.0, .25, 1.4)
        landings.append((f'side {side}{k + 1}', L))
    _, L = sheet(f'V23 | reservoir front fall {side}', (sx * 10.5, 29.45), (sx * 15.5, 29.45), (0, -1), 3.14, .30, 1.3)
    landings.append((f'front {side}', L))
_, L = sheet('V23 | centre fall', (-1.6, 17.25), (1.6, 17.25), (0, -1), 4.28, .25, 1.2)
landings.append(('centre', L))
splashes = []
for label, (e0, e1, out, width) in landings:
    spray(f'V23 | fall spray {label}', e0, e1, out)
    splashes.append({'x0': round(e0.x, 3), 'y0': round(e0.y, 3), 'x1': round(e1.x, 3), 'y1': round(e1.y, 3), 'z': -1.70})

# ---------------------------------------------------------------- 7. boats for the water shader
boats = []
for pivot_name, hull_name in (('V02_백팀_보트_PIVOT', 'V16 | 백팀 royal boat'), ('V02_흑팀_보트_PIVOT', 'V16 | 흑팀 royal boat')):
    hull = O[hull_name]
    pts = [hull.matrix_world @ Vector(p) for p in hull.bound_box]
    hl = (max(p.y for p in pts) - min(p.y for p in pts)) / 2
    hw = (max(p.x for p in pts) - min(p.x for p in pts)) / 2
    boats.append({'pivot': pivot_name, 'halfLength': round(hl * .92, 3), 'halfWidth': round(hw * .85, 3), 'forward': [0, 1, 0]})
import json
(folder / 'water_interaction_v23.json').write_text(json.dumps({'splashes': splashes, 'boats': boats}, indent=1), encoding='utf8')

bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v23.blend'), compress=True)
print('SAVED v23', len(splashes), 'landings', boats)
