"""R280: v23 -> v24. Run in Blender 5.2 (or the bpy 5.2 module): blender --background --python build_v24.py

User review 11-01 (screenshots):
1. Lily pads were flat 12-gons hovering 0.2 m over the water. Rebuild each as a pad on the surface: notch, wavy
   upturned rim with a reddish underside, raised veins, three greens.
2. The robot clay tore when the arm folded: the arm meshes were cut open at the elbow (and shoulder) and the cut
   loops reach 1.5 m from the elbow, past the 1.18 m knuckle. Cap every cut loop and grow the knuckle to 1.6 m.
3. The static white ripple rings round the C platforms go; the water shader computes the waves (R280 shader).
4. The C-platform crossing: 8 platforms in two C routes -> 6 in one zigzag that both teams cross (platform size
   unchanged, 6 m). A zigzag with 1.3 m diagonal gaps and 2.8 m same-side gaps needs 28 m, so this section grows
   4 m: the entry ledge (with its pillars, rails, banners and urns) moves 4 m toward the plaza and 1 m down, and
   the ramp up from the plaza is shortened to meet it. Platform tops rise 0.5 m a step (2.05 -> 4.55 m) and bob
   in a travelling wave (60 degrees a step). The centre waterfall moves 3 m west, clear of the last platform.
"""
import bpy, bmesh, math, random, json
from mathutils import Vector, Matrix
from pathlib import Path

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v23.blend'))
scene = bpy.context.scene
scene.frame_set(1)
col = scene.collection
O, M = bpy.data.objects, bpy.data.materials
rng = random.Random(280)


def material(name, rgb):
    m = M.get(name) or M.new(name)
    m.use_nodes = True
    bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    m.diffuse_color = (*rgb, 1)
    return m


def islands(obj):
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
        out.append(([g.index for g in part], {names[g.material_index] for g in part}, lo, hi))
    bm.free()
    return out


# ---------------------------------------------------------------- 2. robot joints
for k in '12':
    for name in (f'V20 | robot {k} upper arm', f'V20 | robot {k} forearm and weight'):
        me = O[name].data
        bm = bmesh.new(); bm.from_mesh(me)
        cut = [e for e in bm.edges if e.is_boundary]
        caps = bmesh.ops.holes_fill(bm, edges=cut, sides=0)['faces']
        bmesh.ops.triangulate(bm, faces=caps, quad_method='BEAUTY', ngon_method='BEAUTY')
        left = sum(1 for e in bm.edges if e.is_boundary)
        bm.to_mesh(me); bm.free(); me.update()
        print(name, 'capped', len(caps), 'cut loops, open edges left', left)
    knuckle = O[f'V22 | robot {k} elbow knuckle']
    for v in knuckle.data.vertices:
        v.co *= 1.6 / 1.18
    knuckle.data.update()

# ---------------------------------------------------------------- 4a. stretch the crossing: entry ledge and ramp
SHIFT_Y, DROP = -4.0, -1.0
REGION = lambda lo, hi: lo.x > -13.45 and hi.x < 13.45 and lo.y > -9.5 and hi.y < -5.4
moved = 0
for o in list(O):
    if o.type != 'MESH' or o.hide_render:
        continue
    if o.name.startswith('V19 | V03_하부단층_팀별진입') or o.name.startswith('V19 | decorative brick pillar'):
        pts = [o.matrix_world @ Vector(p) for p in o.bound_box]
        lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
        if REGION(lo, hi) or o.name.startswith('V19 | V03_하부단층_팀별진입'):
            o.location.y += SHIFT_Y; o.location.z += DROP; moved += 1
for name in ('V04_지형_갓돌과기둥', 'V04_지형_깃발과화분'):
    o = O[name]
    parts = [p for p in islands(o) if REGION(p[2], p[3])]
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    inv = o.matrix_world.inverted(); mw = o.matrix_world
    for faces, mats, lo, hi in parts:
        verts = {v for i in faces for v in bm.faces[i].verts}
        dz = 0.0 if lo.z < -1.2 else DROP          # water-level bands keep their height
        for v in verts:
            w = mw @ v.co; w.y += SHIFT_Y; w.z += dz; v.co = inv @ w
        moved += 1
    bm.to_mesh(o.data); bm.free(); o.data.update()
ramp = O['V19 | V03_광장출구_경사지형_0에서1.8m']
mw = ramp.matrix_world; inv = mw.inverted()
for v in ramp.data.vertices:                       # y -20..-9 -> -20..-13, top 0..2.89 -> 0..2.0
    w = mw @ v.co
    w.y = -20 + (w.y + 20) * 7 / 11
    if w.z > 0:
        w.z *= 2.0 / 2.89
    v.co = inv @ w
ramp.data.update()
print('entry ledge parts moved', moved)

# ---------------------------------------------------------------- 4b. six platforms in one zigzag
S, A = 4.4, 2.9                                    # forward step, half the lateral swing
slots = [('V16 | 백팀 C platform 02', 'left'), ('V16 | 흑팀 C platform 02', 'right'),
         ('V16 | 백팀 C platform 01', 'left'), ('V16 | 흑팀 C platform 03', 'right'),
         ('V16 | 백팀 C platform 04', 'left'), ('V16 | 흑팀 C platform 04', 'right')]
bobbers = []
for k, (name, side) in enumerate(slots):
    o = O[name]
    mesh_pts = [v.co for v in o.data.vertices]
    world = [o.matrix_world @ p for p in mesh_pts]
    cx = (min(p.x for p in world) + max(p.x for p in world)) / 2
    cy = (min(p.y for p in world) + max(p.y for p in world)) / 2
    z0, z1 = min(p.z for p in mesh_pts), max(p.z for p in mesh_pts)
    top = 2.05 + 0.5 * k
    if o.data.users > 1:
        o.data = o.data.copy()
    for v in o.data.vertices:
        v.co.z = z0 + (v.co.z - z0) * (top - z0) / (z1 - z0)
    o.data.update()
    target = Vector(((-A if side == 'left' else A), -7.0 + S * k))
    o.location.x += target.x - cx; o.location.y += target.y - cy
    phase = -k * math.pi / 3                       # a wave running forward along the zigzag
    for d in o.animation_data.drivers:
        if d.data_path == 'location' and d.array_index == 2:
            d.driver.expression = '0.35*(1-cos(2*pi*(frame-1)/180+%r))' % phase
    o.name = f'V16 | C platform zigzag {k + 1} {side}'
    bobbers.append({'object': o.name, 'x': round(target.x, 3), 'y': round(target.y, 3), 'radius': 3.0})
for name in ('V16 | 백팀 C platform 03', 'V16 | 흑팀 C platform 01'):
    O[name].hide_set(True); O[name].hide_render = True
for o in O:
    if '_C발판물결_' in o.name:                      # static white rings: the shader makes the waves now
        o.hide_set(True); o.hide_render = True
for name in ('V23 | centre fall', 'V23 | centre spout', 'V23 | fall spray centre'):
    O[name].location.x -= 3.0
print('zigzag', [(b['x'], b['y']) for b in bobbers])

# ---------------------------------------------------------------- 1. lily pads
pads_src = O['V04_수면_연잎']
old = islands(pads_src)
pad_spots = [((p[2] + p[3]) / 2, max(p[3].x - p[2].x, p[3].y - p[2].y) / 2) for p in old if p[1] == {'G15_연잎'}]
flowers = [p for p in old if p[1] == {'G21_꽃_분홍'}]
centres = [Vector((b['x'], b['y'])) for b in bobbers]
inside = lambda x, y, pad: any((Vector((x, y)) - c).length < 3.0 + pad + .15 for c in centres)
gone = [i for p in old if p[1] == {'G15_연잎'} for i in p[0]]
gone += [i for p in flowers if inside((p[2].x + p[3].x) / 2, (p[2].y + p[3].y) / 2, .2) for i in p[0]]
bm = bmesh.new(); bm.from_mesh(pads_src.data); bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[bm.faces[i] for i in sorted(set(gone))], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(pads_src.data); bm.free()
pad_mats = [material('V24 | 연잎 초록', (0.27, 0.50, 0.12)), material('V24 | 연잎 연두', (0.40, 0.60, 0.16)),
            material('V24 | 연잎 짙은', (0.17, 0.38, 0.10)), material('V24 | 연잎 잎맥', (0.56, 0.72, 0.30)),
            material('V24 | 연잎 뒷면', (0.48, 0.20, 0.22))]
dg = bpy.context.evaluated_depsgraph_get(); dg.update()
water_names = ('V07 Water', 'V22 | tank water')


def surface(x, y):
    origin = Vector((x, y, 12.0))
    for _ in range(6):
        hit = scene.ray_cast(dg, origin, Vector((0, 0, -1)))
        if not hit[0]:
            return None
        if hit[4].name.startswith(water_names):
            return hit[1].z
        if hit[4].name.startswith(('V16 |', 'V19 |', 'V18 |', 'V17 |', 'V04_지형', 'V04_중반')):
            return None                            # something solid stands here
        origin = hit[1] - Vector((0, 0, 0.01))
    return None


V, F, S_ = [], [], []


def pad(cx, cy, cz, R, rot, body):
    """A lily pad of radius R at (cx, cy) floating at cz: notch, wavy upturned rim, veins, reddish rim side."""
    half = rng.uniform(.17, .24)                    # notch half-angle (rad)
    seg = 30
    rings = [(.14, .010), (.46, .006), (.80, .004), (1.0, .05)]
    wob = rng.uniform(0, math.tau)

    def at(r, a, dz):
        edge = 1 + .035 * math.sin(5 * a + wob) * (r ** 2)
        x, y = math.cos(a + rot) * r * R * edge, math.sin(a + rot) * r * R * edge
        return Vector((cx + x, cy + y, cz + dz * R))
    angles = [half + (math.tau - 2 * half) * i / seg for i in range(seg + 1)]
    centre = len(V); V.append(Vector((cx, cy, cz + .016 * R)))
    grid = []
    for r, z in rings:
        row = []
        for a in angles:
            row.append(len(V)); V.append(at(r, a, z))
        grid.append(row)
    for i in range(seg):
        F.append([centre, grid[0][i], grid[0][i + 1]]); S_.append(body)
        for j in range(len(rings) - 1):
            F.append([grid[j][i], grid[j + 1][i], grid[j + 1][i + 1], grid[j][i + 1]]); S_.append(body)
    # Thickness: the rim and both notch edges turn down to just under the water.
    low = [len(V) + i for i in range(seg + 1)]
    for a in angles:
        V.append(at(1.0, a, -.03))
    for i in range(seg):
        F.append([grid[-1][i], low[i], low[i + 1], grid[-1][i + 1]]); S_.append(4)
    for a, edge in ((angles[0], 0), (angles[-1], seg)):
        prev_top, prev_low = centre, None
        tops = [centre] + [grid[j][edge] for j in range(len(rings))]
        lows = []
        for t in tops:
            lows.append(len(V)); V.append(V[t] - Vector((0, 0, .045 * R)))
        for j in range(len(tops) - 1):
            q = [tops[j], tops[j + 1], lows[j + 1], lows[j]]
            F.append(q[::-1] if edge == 0 else q); S_.append(4)    # facing into the notch
    # Veins: low ridges from near the centre to near the rim.
    for vi in range(11):
        a = half + (math.tau - 2 * half) * (vi + .5) / 11
        idx = []
        for r in (.1, .4, .7, .93):
            z = .014 + .006 * (1 - r)
            c = at(r, a, z); side = Vector((-math.sin(a + rot), math.cos(a + rot), 0)) * (.022 * R * (1.2 - r))
            idx.append((len(V), len(V) + 1, len(V) + 2))
            V.extend([c - side - Vector((0, 0, .004 * R)), c + Vector((0, 0, .006 * R)), c + side - Vector((0, 0, .004 * R))])
        for (l0, m0, r0), (l1, m1, r1) in zip(idx, idx[1:]):
            F.append([l0, l1, m1, m0]); S_.append(3)
            F.append([m0, m1, r1, r0]); S_.append(3)


kept = 0
for (c, r) in pad_spots:
    for n in range(1 if r < .45 else 2):           # bigger old pads become a pair
        x = c.x + (rng.uniform(-.25, .25) if n else 0); y = c.y + (rng.uniform(-.25, .25) if n else 0)
        R = r * rng.uniform(.85, 1.25) * (.7 if n else 1)
        if inside(x, y, R):
            continue
        z = surface(x, y)
        if z is None:
            continue
        pad(x, y, z + .012, R, rng.uniform(0, math.tau), rng.choices((0, 1, 2), (.5, .25, .25))[0])
        kept += 1
me = bpy.data.meshes.new('V24 | lily pads')
me.from_pydata([tuple(v) for v in V], [], F)
for m in pad_mats:
    me.materials.append(m)
me.polygons.foreach_set('material_index', S_)
me.update()
col.objects.link(O.new('V24 | lily pads', me))
print('lily pads', kept, 'of', len(pad_spots), 'faces', len(F))

# ---------------------------------------------------------------- water interaction data
extra = json.loads((folder / 'water_interaction_v23.json').read_text(encoding='utf8'))
for s in extra['splashes']:
    if abs(s['x0'] + 1.6) < .01 and abs(s['x1'] - 1.6) < .01:   # the centre fall moved 3 m west
        s['x0'] -= 3.0; s['x1'] -= 3.0
extra['bobbers'] = bobbers
(folder / 'water_interaction_v24.json').write_text(json.dumps(extra, indent=1, ensure_ascii=False), encoding='utf8')

bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v24.blend'), compress=True)
print('SAVED v24')
