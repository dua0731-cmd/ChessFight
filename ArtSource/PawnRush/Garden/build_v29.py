"""R286: v28 -> v29. Run in Blender 5.2: blender --background --python build_v29.py

User request 11-02 (two Tripo models, Castle/castle_bridge.glb and Castle/castle_tower.glb): "these are the map's
castle walls; the map is the rim of a rectangle - fit them to it, tall and burly".
- The old perimeter goes: the low V17 side walls (7 M triangles), their crenels, the eight round towers, their
  crenels and roofs.
- castle_bridge is a horseshoe of wall with eight towers and a gate. It is unrolled along its centre line into a
  straight strip (towers kept round), the towers' centres are put on one line, and the strip is cut at the tower
  centres into seven spans (six arched walls and the gate). The spans run round the rectangle tower to tower, so
  two half towers meet at every joint; the gate stands behind the start. Scale 150: wall top about 16 m above the
  base, tower tips about 29 m.
- castle_tower stands at the four corners (xy 62, z 50: its wall stubs match the wall's thickness), covering the
  spans' end towers.
- A meadow outside the walls, so the arches do not open onto nothing.
- The walls do not collide; four hidden slabs on the play edge do (x +-46, forecourt end 60, start pen end -60).
Both models are decimated (wall to 30 %, tower to 25 %); every span instance shares its mesh.
"""
import bpy, bmesh, math, json, statistics as st
from mathutils import Vector, Matrix, kdtree
from pathlib import Path

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v28.blend'))
scene = bpy.context.scene
scene.frame_set(1)
col = scene.collection
O, M = bpy.data.objects, bpy.data.materials
report = {}

# ---------------------------------------------------------------- old perimeter out
roof_mat, gold_mat = M['G19_탑지붕_파랑'], M['G06_금장식']     # the old towers' blue roofs and gold finials
OLD = ('V17 | castle wall', '외벽_흉벽', 'V19 | 성벽_원형탑', 'V19 | 성벽_흉벽', 'V04_성벽_탑지붕')
gone = [o for o in O if o.name.startswith(OLD)]
for o in list(gone):
    gone += [c for c in o.children_recursive if c not in gone]
report['old_removed'] = len(gone)
for o in gone:
    O.remove(o)

stone = M.new('V29 | castle stone')
stone.diffuse_color = (.54, .54, .54, 1); stone.roughness = .78
stone.use_nodes = True
bsdf = stone.node_tree.nodes['Principled BSDF']
bsdf.inputs['Base Color'].default_value = (.54, .54, .54, 1); bsdf.inputs['Roughness'].default_value = .78
meadow_mat = M.new('V29 | outer meadow')
meadow_mat.diffuse_color = (.33, .47, .27, 1); meadow_mat.roughness = .9
meadow_mat.use_nodes = True
meadow_mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.33, .47, .27, 1)


def load(name, ratio):
    """Import a Tripo GLB, decimate it and return (world vertices, faces); the imported objects are removed."""
    before = set(O)
    bpy.ops.import_scene.gltf(filepath=str(folder / 'Castle' / name))
    new = [o for o in O if o not in before]
    src = next(o for o in new if o.type == 'MESH')
    mod = src.modifiers.new('decimate', 'DECIMATE'); mod.ratio = ratio
    ev = src.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = bpy.data.meshes.new_from_object(ev)
    verts = [src.matrix_world @ v.co for v in me.vertices]
    faces = [tuple(p.vertices) for p in me.polygons]
    bpy.data.meshes.remove(me)
    for o in new:
        data = o.data if o.type == 'MESH' else None
        O.remove(o)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    return verts, faces


# ---------------------------------------------------------------- wall: unroll the horseshoe
P, F = load('castle_bridge.glb', .30)
yc = -.12                       # below this the two legs run straight; above it the wall bends round
body = [p for p in P if .02 < p.z < .09]
nb = 90
arc = [[] for _ in range(nb)]; legL, legR = {}, {}
for p in body:
    if p.y >= yc:
        k = min(nb - 1, int(math.atan2(p.y - yc, p.x) / math.pi * nb)); arc[k].append(math.hypot(p.x, p.y - yc))
    else:
        (legL if p.x < 0 else legR).setdefault(int((p.y - yc) / .01), []).append(p.x)
line = [Vector((st.median(legL[k]), yc + (k - .5) * .01, 0)) for k in sorted(legL)]
line += [Vector((math.cos((k + .5) / nb * math.pi), math.sin((k + .5) / nb * math.pi), 0)) * st.median(arc[k]) + Vector((0, yc, 0))
         for k in range(nb - 1, -1, -1) if arc[k]]
line += [Vector((st.median(legR[k]), yc + (k - .5) * .01, 0)) for k in sorted(legR, reverse=True)]
for _ in range(8):
    line = [line[0]] + [(line[i - 1] + 2 * line[i] + line[i + 1]) / 4 for i in range(1, len(line) - 1)] + [line[-1]]
line = [line[0] + (line[0] - line[1]).normalized() * .05] + line + [line[-1] + (line[-1] - line[-2]).normalized() * .05]
dense, us, u = [], [], 0.0
for a, b in zip(line, line[1:]):
    n = max(1, int((b - a).length / .0005))
    for j in range(n):
        dense.append(a + (b - a) * (j / n)); us.append(u + (b - a).length * j / n)
    u += (b - a).length
tang = [(dense[min(i + 1, len(dense) - 1)] - dense[max(i - 1, 0)]).normalized() for i in range(len(dense))]
kd = kdtree.KDTree(len(dense))
for i, p in enumerate(dense):
    kd.insert(p, i)
kd.balance()
S = []                           # strip coordinates: u along the wall, n outward, z up
for p in P:
    q = Vector((p.x, p.y, 0)); c, i, _ = kd.find(q)
    t = tang[i]
    S.append(Vector((us[i] + (q - c).dot(t), (q - c).dot(Vector((-t.y, t.x, 0))), p.z)))

# Tower centres from their cone tips; then shear n so every tower centre lies on n = 0.
tips = sorted((s.x, s.y) for s in S if s.z > .175)
groups = [[tips[0]]]
for e in tips[1:]:
    (groups.append([e]) if e[0] - groups[-1][-1][0] > .006 else groups[-1].append(e))
towers = [(st.median(e[0] for e in g), st.median(e[1] for e in g)) for g in groups if len(g) > 100]
assert len(towers) == 8, towers


def centre_n(x):
    if x <= towers[0][0]:
        return towers[0][1]
    for (u0, n0), (u1, n1) in zip(towers, towers[1:]):
        if x <= u1:
            return n0 + (n1 - n0) * (x - u0) / (u1 - u0)
    return towers[-1][1]


for s in S:
    s.y -= centre_n(s.x)
wall_n = sorted(s.y for s in S if .03 < s.z < .07)
inner = wall_n[int(.03 * len(wall_n))]     # the wall's inner face (3rd percentile; bushes stand a little proud)
report['wall'] = {'towers_u': [round(t[0], 4) for t in towers], 'inner_face_n': round(inner, 4), 'triangles': sum(len(f) - 2 for f in F)}

SCALE = 150.0
EPS = .004     # each span runs this far past its towers' centres, so the two halves of a joint tower overlap
SPANS = {'S1': (0, 1), 'S2': (1, 2), 'S3': (2, 3), 'G': (3, 4), 'S5': (4, 5), 'S6': (5, 6), 'S7': (6, 7)}


def span_mesh(key):
    """The strip between two tower centres, cut just past both towers' centres, in span space (u from 0 at the first centre)."""
    a, b = SPANS[key]
    u0, u1 = towers[a][0], towers[b][0]
    bm = bmesh.new()
    vs = [bm.verts.new((s.x - u0, s.y, s.z)) for s in S]
    for f in F:
        try:
            bm.faces.new([vs[i] for i in f])
        except ValueError:
            pass
    for co, no in (((-EPS, 0, 0), (-1, 0, 0)), ((u1 - u0 + EPS, 0, 0), (1, 0, 0))):
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, clear_outer=True)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    me = bpy.data.meshes.new(f'V29 | castle wall span {key}')
    bm.to_mesh(me); bm.free()
    me.materials.append(stone); me.materials.append(roof_mat); me.materials.append(gold_mat)
    # Tower roofs above the towers' window ring, gold finials and vanes at the tips (within a tower's radius of either end).
    idx = []
    for p in me.polygons:
        c = p.center
        tower = min(abs(c.x), abs(c.x - (u1 - u0))) < .045
        idx.append(0 if not tower or c.z <= .14 else 2 if c.z > .181 else 1)
    me.polygons.foreach_set('material_index', idx)
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.update()
    return me, u1 - u0


meshes = {k: span_mesh(k) for k in SPANS}

# ---------------------------------------------------------------- lay the spans round the rectangle
# The walls' inner faces: x +-46, north 61, south -62 (the gate's carvings stand about 2.6 m proud of the face, so
# they stop short of the start pen, which ends at -60).
off = -inner * SCALE                     # tower-centre line this far outside the inner face
XE, YN, YS = 46.0 + off, 61.0 + off, -(62.0 + off)
corners = {'SW': Vector((-XE, YS, 0)), 'NW': Vector((-XE, YN, 0)), 'NE': Vector((XE, YN, 0)), 'SE': Vector((XE, YS, 0))}
BASE = -2.0
# Clockwise, so the garden is on each run's right (as inside the horseshoe) and the outward normal is on its left.
SIDES = [('W', 'SW', 'NW', ['S1', 'S5', 'S2', 'S6'], None), ('N', 'NW', 'NE', ['S3', 'S1', 'S5'], None),
         ('E', 'NE', 'SE', ['S7', 'S2', 'S6', 'S3'], None), ('S', 'SE', 'SW', ['S7', 'G', 'S1'], 'G')]
placed = []
for side, a, b, seq, fixed in SIDES:
    A, B = corners[a], corners[b]
    d = (B - A).normalized(); out = Vector((-d.y, d.x, 0)); L = (B - A).length
    lengths = [meshes[k][1] * SCALE for k in seq]
    free = sum(l for k, l in zip(seq, lengths) if k != fixed)
    k_free = (L - sum(l for k, l in zip(seq, lengths) if k == fixed)) / free
    pos = 0.0
    for n, (key, length) in enumerate(zip(seq, lengths)):
        stretch = 1.0 if key == fixed else k_free
        o = O.new(f'V29 | castle wall {side}{n + 1} {key}', meshes[key][0]); col.objects.link(o)
        rot = Matrix((d, out, Vector((0, 0, 1)))).transposed()
        o.matrix_world = Matrix.Translation(A + d * pos + Vector((0, 0, BASE))) @ rot.to_4x4() @ Matrix.Diagonal((SCALE * stretch, SCALE, SCALE, 1))
        pos += length * stretch
        placed.append((o.name, round(stretch, 3)))
    report[f'side {side}'] = {'length_m': round(L, 2), 'stretch': round(k_free, 3)}

# ---------------------------------------------------------------- corner towers
TP, TF = load('castle_tower.glb', .25)
tme = bpy.data.meshes.new('V29 | castle corner tower')
tme.from_pydata([tuple(p) for p in TP], [], TF)
tme.materials.append(stone); tme.materials.append(roof_mat); tme.materials.append(gold_mat)
tme.polygons.foreach_set('material_index', [2 if p.center.z > .9 else 1 if p.center.z > .6 else 0 for p in tme.polygons])
tme.polygons.foreach_set('use_smooth', [True] * len(tme.polygons))
tme.update()
# The body is centred near (0, .02); its wall stubs run along y. The tall stub (-y) points out past the corner.
for name, c in corners.items():
    o = O.new(f'V29 | castle corner tower {name}', tme); col.objects.link(o)
    turn = 0 if name.startswith('S') else math.pi
    o.matrix_world = (Matrix.Translation(c + Vector((0, 0, BASE))) @ Matrix.Rotation(turn, 4, 'Z')
                      @ Matrix.Translation((0, -.02 * 62, 0)) @ Matrix.Diagonal((62, 62, 50, 1)))
report['tower'] = {'triangles': sum(len(f) - 2 for f in TF)}

# ---------------------------------------------------------------- meadow outside the walls
R = 60.0
v = [(-XE - R, YS - R, BASE), (XE + R, YS - R, BASE), (XE + R, YN + R, BASE), (-XE - R, YN + R, BASE),
     (-47.9, -60, BASE), (47.9, -60, BASE), (47.9, 60, BASE), (-47.9, 60, BASE)]     # inner edge = the base slab, so it runs under the walls
f = [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
mme = bpy.data.meshes.new('V29 | outer meadow'); mme.from_pydata(v, [], f); mme.materials.append(meadow_mat); mme.update()
col.objects.link(O.new('V29 | outer meadow', mme))

# ---------------------------------------------------------------- collision: four hidden slabs on the play edge
# The walls themselves do not collide (a box round a span would reach 2.6 m into the start pen and the finish
# forecourt). Slabs stand on the edge of the play area instead - x +-46, the forecourt's end (60) and the start pen's
# end (-60) - 10 m thick and 32 m tall, so nobody walks out through an arch or the gate.
for name, lo, hi in (('W', (-56, -70), (-46, 70)), ('E', (46, -70), (56, 70)),
                     ('N', (-56, 60), (56, 70)), ('S', (-56, -70), (56, -60))):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for vert in bm.verts:
        vert.co = Vector((lo[0] if vert.co.x < 0 else hi[0], lo[1] if vert.co.y < 0 else hi[1], -4 if vert.co.z < 0 else 30))
    pme = bpy.data.meshes.new(f'V29 | castle collision {name}'); bm.to_mesh(pme); bm.free()
    pme.materials.append(stone)
    o = O.new(f'V29 | castle collision {name}', pme); col.objects.link(o)
    o['unity_collision_proxy'] = True
    o.hide_render = True; o.hide_set(True)

report['placed'] = placed
print(json.dumps(report, indent=1, ensure_ascii=False))
(folder / 'build_v29_report.json').write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding='utf8')
extra = json.loads((folder / 'water_interaction_v28.json').read_text(encoding='utf8'))
(folder / 'water_interaction_v29.json').write_text(json.dumps(extra, indent=1, ensure_ascii=False), encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v29.blend'), compress=True)
print('SAVED v29')
