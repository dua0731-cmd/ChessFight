"""R287: v29 -> v30. Run in Blender 5.2: blender --background --python build_v30.py

User request 11-02 (two screenshots, Castle/castle_gate.glb and Castle/royal_boat.glb):
- "Set the waterfall sections on both sides behind the pink line, and remove the two circles on both sides." The
  line ran across the garden through the second tree of each terrace (y about 27). Both side terraces - tiles,
  tanks, outlets and falls, piers, beds, trees, bushes and flowers - move 10.5 m north, so each tank's wall face
  (y 16.5) stands on the line. Only their position changes (R287 review). The two
  round islands at x +-29 (island, its 72 wall pieces, its bed) go.
- "Install the assets at the entrance (exit) on the right, with a boat coming out of it and strolling round the lake
  on the right; twice: once after the start, once when the player passes the rotating section." The gate model
  stands in the moat against the east wall's arch at y 9.5 (its tunnel lines up with the arch); a strip of water
  runs on through the arch. The royal boat waits outside the wall, comes out through the arch and the gate, loops
  round the east lake (where the island was) and goes back in: one take of about 70 s, sampled at 30 Hz into
  parade_v30.json. Unity plays it (GardenBoatParade); the boat is hidden between takes.
"""
import bpy, bmesh, math, json
from mathutils import Vector, Matrix, Euler
from pathlib import Path

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v29.blend'))
scene = bpy.context.scene
scene.frame_set(1)
col = scene.collection
O, M = bpy.data.objects, bpy.data.materials
report = {}

# ---------------------------------------------------------------- round islands out
ISLAND = ('V02_장식정원_독립섬', 'V19 | V02_장식섬_벽', 'V19 | V02_장식정원_화단_받침', 'V02_장식정원_화단_식재덩어리')
gone = [o for o in O if o.name.startswith(ISLAND)]
report['island_parts_removed'] = len(gone)
for o in gone:
    O.remove(o)

# ---------------------------------------------------------------- side terraces behind the line
DY = 10.5
SHIFT = ('V19 | V02_장식_후반테라스', 'V19 | V02_장식_폭포수조', 'V22 | tank water', 'V23 | tank pier', 'V26 | spout stone tank',
         'V26 | fall curtain tank', 'V26 | fall streams tank', 'V23 | fall spray tank', 'V19 | V02_장식_테라스화단_받침',
         'V02_장식_테라스화단_식재덩어리', 'V23 | stylized tree')
moved = [o for o in O if o.name.startswith(SHIFT)]
for o in moved:
    if o.parent not in moved:
        o.matrix_world = Matrix.Translation((0, DY, 0)) @ o.matrix_world
report['terrace_objects_moved'] = len(moved)

# The terraces move as they are ("only the position changes, not the structures"): nothing is trimmed or removed.
# Every V19 block shares one bevelled-cube mesh and gets its size from its object transform, so editing a block's
# mesh would reshape all of them. The inner north tile of each terrace now tucks its corner into the finish tower.
TOWER = (18.7, 25.3, 50.7, 57.3)


def blocks(me):
    """Vertex ranges of the bushes: each starts with its 42-vertex icosphere core (v23 bush_template)."""
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seen = [False] * len(bm.verts); starts = []
    for v in bm.verts:
        if seen[v.index]:
            continue
        stack, size = [v], 0; seen[v.index] = True
        while stack:
            a = stack.pop(); size += 1
            for e in a.link_edges:
                b = e.other_vert(a)
                if not seen[b.index]:
                    seen[b.index] = True; stack.append(b)
        if size == 42:
            starts.append(v.index)
    bm.free(); starts.sort()
    return [(s, (starts[i + 1] if i + 1 < len(starts) else len(me.vertices))) for i, s in enumerate(starts)]


def coords(o):
    flat = [0.0] * (len(o.data.vertices) * 3); o.data.vertices.foreach_get('co', flat)
    return [o.matrix_world @ Vector(flat[i:i + 3]) for i in range(0, len(flat), 3)]


def write(o, pts):
    mi = o.matrix_world.inverted(); flat = []
    for p in pts:
        flat.extend(mi @ p)
    o.data.vertices.foreach_set('co', flat); o.data.update()


def delete_verts(o, idx):
    if idx:
        bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.verts[i] for i in sorted(idx)], context='VERTS')
        bm.to_mesh(o.data); bm.free(); o.data.update()


# ---------------------------------------------------------------- the start pad in one piece
# R287 review ("폰 시작 점 바닥"): the 12 x 9 m start pad was four bevelled blocks whose rounded corners met under the
# pawn's start (0, -55.5 .. -57), leaving a dark pinhole and grooves there. One block of the same shared mesh, scaled
# to the whole pad, takes their place (the mesh itself is not edited: every V19 block uses it, PITFALLS 31).
PAD = 'V19 | 출발_12명대기_12x9m'
tiles = [o for o in O if o.name.startswith(PAD + ' ')]
lo = Vector((min(min((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in tiles) for i in range(3)))
hi = Vector((max(max((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in tiles) for i in range(3)))
first = tiles[0]
flo = Vector((min((first.matrix_world @ Vector(c))[i] for c in first.bound_box) for i in range(3)))
fhi = Vector((max((first.matrix_world @ Vector(c))[i] for c in first.bound_box) for i in range(3)))
ratio = Vector(((hi - lo)[i] / (fhi - flo)[i] for i in range(3)))
pad = O.new(PAD, first.data); col.objects.link(pad)
for slot_from, slot_to in zip(first.material_slots, pad.material_slots):
    slot_to.link = slot_from.link; slot_to.material = slot_from.material
pad.matrix_world = (Matrix.Translation((lo + hi) / 2) @ Matrix.Diagonal((*ratio, 1)) @ Matrix.Translation(-(flo + fhi) / 2)
                    @ first.matrix_world)
report['start_pad'] = {'replaced': [o.name for o in tiles], 'box': [round(v, 3) for v in (*lo, *hi)]}
for o in tiles:
    O.remove(o)


# Terrace bushes and their flowers (all at |x| > 19 in the garden mesh) move too.
bush, flower = O['V23 | garden bushes'], O['V23 | garden flowers']
bp, fp = coords(bush), coords(flower)
ranges = blocks(bush.data)
centres = [sum(bp[s:s + 42], Vector()) / 42 for (s, e) in ranges]
owner = [min(range(len(centres)), key=lambda i: (centres[i] - sum(fp[k:k + 37], Vector()) / 37).length) for k in range(0, len(fp), 37)]
moved_y = []
for i, (s, e) in enumerate(ranges):
    if abs(centres[i].x) <= 19 or not 14 < centres[i].y < 47:
        continue
    moved_y.append(round(centres[i].y, 1))
    for v in range(s, e):
        bp[v].y += DY
    for k, b in enumerate(owner):
        if b == i:
            for v in range(k * 37, k * 37 + 37):
                fp[v].y += DY
write(bush, bp); write(flower, fp)
report['bushes_moved'] = {'count': len(moved_y), 'y': [min(moved_y), max(moved_y)] if moved_y else None}
# Anything moved that still stands in the finish towers.
report['in_towers'] = []
for o in moved:
    w = [o.matrix_world @ Vector(c) for c in o.bound_box]
    if min(abs(v.x) for v in w) < TOWER[1] and max(abs(v.x) for v in w) > TOWER[0] and max(v.y for v in w) > TOWER[2] and min(v.y for v in w) < TOWER[3]:
        report['in_towers'].append(o.name)


def load(name, ratio):
    """Import a Tripo GLB, decimate it and return (world vertices, faces); the imported objects are removed."""
    before = set(O)
    bpy.ops.import_scene.gltf(filepath=str(folder / 'Castle' / name))
    new = [o for o in O if o not in before]
    src = next(o for o in new if o.type == 'MESH')
    mod = src.modifiers.new('decimate', 'DECIMATE'); mod.ratio = ratio
    me = bpy.data.meshes.new_from_object(src.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    verts = [src.matrix_world @ v.co for v in me.vertices]
    faces = [tuple(p.vertices) for p in me.polygons]
    bpy.data.meshes.remove(me)
    for o in new:
        data = o.data if o.type == 'MESH' else None
        O.remove(o)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    return verts, faces


def mesh(name, verts, faces, mats, pick):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    for m in mats:
        me.materials.append(m)
    me.polygons.foreach_set('material_index', [pick(p.center) for p in me.polygons])
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.update()
    return me


stone, roof, gold, royal = M['V29 | castle stone'], M['G19_탑지붕_파랑'], M['G06_금장식'], M['G08_왕실파랑']

# ---------------------------------------------------------------- the water gate at the east arch
ARCH_Y, WALL_X = 9.5, 46.0
GX, GY, GZ = 26.0, 14.0, 30.0          # along the wall, tunnel depth, height (tunnel 5.7 m wide, 3.9 m high)
GP, GF = load('castle_gate.glb', .15)
gate_me = mesh('V30 | castle water gate', GP, GF, [stone, roof], lambda c: 1 if c.z > .27 and c.x < -.3 else 0)
gate = O.new('V30 | castle water gate', gate_me); col.objects.link(gate)
# The model's tunnel runs along its y, centred at x .06; turned 90 degrees its back (y -.41) meets the wall's face.
gate.matrix_world = (Matrix.Translation((WALL_X - .41 * GY, ARCH_Y - .06 * GX, -2.0)) @ Matrix.Rotation(math.pi / 2, 4, 'Z')
                     @ Matrix.Diagonal((GX, GY, GZ, 1)))
report['gate'] = {'triangles': len(GF), 'x': [round(WALL_X - .82 * GY, 2), WALL_X], 'y': [round(ARCH_Y - .55 * GX, 2), round(ARCH_Y + .43 * GX, 2)]}

# Water through the wall's arch, so the boat floats until it is out of sight.
cme = bpy.data.meshes.new('V30 | gate channel water')
cme.from_pydata([(44.0, ARCH_Y - 4, -1.7), (56.0, ARCH_Y - 4, -1.7), (56.0, ARCH_Y + 4, -1.7), (44.0, ARCH_Y + 4, -1.7)], [], [(0, 1, 2, 3)])
cme.materials.append(M['V07 | calm water']); cme.update()
col.objects.link(O.new('V30 | gate channel water', cme))

# ---------------------------------------------------------------- the royal boat and its take
BOAT_SCALE, DRAFT, WATER = 7.75, .6, -1.7
BP, BF = load('royal_boat.glb', .20)
ivory = M.get('V30 | boat ivory') or M.new('V30 | boat ivory')
ivory.use_nodes = True
ivory.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.86, .82, .74, 1)
ivory.diffuse_color = (.86, .82, .74, 1)


def boat_part(c):
    if c.z > .385 and abs(c.x) < .12:
        return 2                                   # crown
    if c.z > .30:
        return 1                                   # canopy
    if abs(c.x) > .40 and c.z > .2:
        return 2                                   # lanterns
    return 0


boat_me = mesh('V30 | parade royal boat', BP, BF, [ivory, royal, gold], boat_part)
pivot = O.new('V30 | parade boat PIVOT', None); col.objects.link(pivot)
pivot.empty_display_size = 2
boat = O.new('V30 | parade royal boat', boat_me); col.objects.link(boat)
boat.parent = pivot
boat.matrix_parent_inverse = Matrix.Identity(4)
boat.matrix_basis = Matrix.Translation((0, 0, -DRAFT)) @ Matrix.Diagonal((BOAT_SCALE, BOAT_SCALE, BOAT_SCALE, 1))

# From deep in the gate's tunnel (the wall's arch sill is above the hull, so the take starts and ends behind the
# gate), south down the west side of the east lake (x 24), round the south end, north along x 41, west below the
# gate and up x 29.5, east back into the gate.
CTRL = [(50, ARCH_Y), (44, ARCH_Y), (38, ARCH_Y), (31, 9.2), (26.5, 6.5), (24.3, 1), (23.8, -10),
        (24, -26), (26.5, -38), (32, -45), (38.5, -44), (41.3, -36), (41.5, -20), (40.3, -12), (36.5, -8.4),
        (31.5, -6.6), (29.3, -2), (29.6, 4), (31.5, 7.6), (35, 9.3), (40, ARCH_Y), (45, ARCH_Y), (50, ARCH_Y)]


def catmull(points, steps=40):
    pts = [Vector((p[0], p[1], 0)) for p in points]
    pts = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1:i + 3]
        for k in range(steps):
            t = k / steps
            out.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(pts[-2])
    return out


line = catmull(CTRL)
arc = [0.0]
for a, b in zip(line, line[1:]):
    arc.append(arc[-1] + (b - a).length)
L = arc[-1]


def at(s):
    s = min(max(s, 0), L)
    lo, hi = 0, len(arc) - 1
    while hi - lo > 1:
        mid = (lo + hi) // 2
        (lo, hi) = (mid, hi) if arc[mid] <= s else (lo, mid)
    f = (s - arc[lo]) / max(arc[hi] - arc[lo], 1e-9)
    return line[lo].lerp(line[hi], f), (line[hi] - line[lo]).normalized()


CRUISE, EASE = 2.6, 12.0                       # m/s; metres to speed up from (and slow down to) a crawl


def speed(s):
    return max(.5, CRUISE * min(1.0, s / EASE, (L - s) / EASE) ** .5)


def heading(s):
    """Direction of travel over the next and last 1.5 m (the polyline's own steps are too short to steer by)."""
    d = at(s + 1.5)[0] - at(s - 1.5)[0]
    return math.atan2(d.y, d.x)


FPS = 30
samples, s, t = [], 0.0, 0.0
while s < L:
    p = at(s)[0]
    yaw = heading(s)
    v = speed(s)
    turn = ((heading(s + 2) - heading(s - 2) + math.pi) % (2 * math.pi) - math.pi) / 4     # rad per metre
    bank = max(-.07, min(.07, -turn * v * v * .12))            # leans into the turn, up to 4 degrees
    bob = .05 * math.sin(2 * math.pi * t / 3.1)
    pitch = .012 * math.sin(2 * math.pi * t / 4.3 + .8)
    m = Matrix.Translation((p.x, p.y, WATER + bob)) @ Euler((bank, pitch, yaw), 'XYZ').to_matrix().to_4x4()
    q = m.to_quaternion()
    samples.extend(round(float(c), 6) for c in (m.translation.x, m.translation.y, m.translation.z, q.x, q.y, q.z, q.w))
    s += v / FPS; t += 1 / FPS
frames = len(samples) // 7
pivot.rotation_mode = 'QUATERNION'
pivot.location = samples[0:3]
pivot.rotation_quaternion = (samples[6], samples[3], samples[4], samples[5])
report['boat_triangles'] = len(BF)
report['parade'] = {'length_m': round(L, 1), 'duration_s': round(frames / FPS, 1), 'frames': frames}
parade = {'pivot': pivot.name, 'fps': FPS, 'frames': frames, 'duration': frames / FPS, 'samples': samples,
          'halfLength': round(.49 * BOAT_SCALE, 2), 'halfWidth': round(.25 * BOAT_SCALE, 2), 'forward': [1, 0, 0]}
# forward: the boat's own +x, in its local frame (the take turns it)
(folder / 'parade_v30.json').write_text(json.dumps(parade, separators=(',', ':'), ensure_ascii=False), encoding='utf8')

# ---------------------------------------------------------------- splash lines of the moved tank falls
extra = json.loads((folder / 'water_interaction_v29.json').read_text(encoding='utf8'))
for sp in extra['splashes']:
    if abs(sp['y0'] - 14.26) < .5 and 25 < abs(sp['x0']) < 39:
        sp['y0'] = round(sp['y0'] + DY, 3); sp['y1'] = round(sp['y1'] + DY, 3)
(folder / 'water_interaction_v30.json').write_text(json.dumps(extra, indent=1, ensure_ascii=False), encoding='utf8')

print(json.dumps(report, indent=1, ensure_ascii=False))
(folder / 'build_v30_report.json').write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v30.blend'), compress=True)
print('SAVED v30')
