"""R285: v27 -> v28. Run in Blender 5.2: blender --background --python build_v28.py

User review 11-02 (four screenshots):
- The six fountain posts (orange balls) in the moat beside the C crossing go (`V04_중반_분수`).
- The flowers seen from the C crossing go: the beds on the two round platforms' slabs (garden bushes at |x| 24-34,
  y -8..-2), the planter clumps over the crossing (x +-12, y -9) and any clump with water or nothing under it.
- Bushes, and the flowers on them, floated above their beds (the v23 bushes were grown around the old blobs, up to
  a metre above the v19 bed tops). Every bush is set down on what lies under its centre, sunk 8 cm into it; its
  flowers move with it.
- The boats ran into the quay walls at both ends (the hull and rail passed into the stone). Each boat now stops a
  few centimetres short of the wall: it arrives at about 0.35 m/s, knocks against it (two small bounces back,
  6 cm then 2 cm), pitches and dips a little with the knock, then waits and leaves. The bob, pitch and roll of v27
  stay on top. The motion is baked to keyframes (one per frame) instead of drivers.
"""
import bpy, bmesh, math, json
from mathutils import Vector
from pathlib import Path

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v27.blend'))
scene = bpy.context.scene
scene.frame_set(1)
O = bpy.data.objects
report = {}

# ---------------------------------------------------------------- fountains out
if 'V04_중반_분수' in O:
    O.remove(O['V04_중반_분수'])
    report['fountains_removed'] = 6

# ---------------------------------------------------------------- ground probe
SKIP = ('V23 | garden', 'V23 | planter', 'V23 | fall', 'V25 | fall', 'V26 | fall', 'V24 | lily')
visible = [o for o in O if o.type == 'MESH' and not o.hide_get() and not o.hide_render and o.visible_get()
           and not o.name.startswith(SKIP) and 'tree' not in o.name.lower() and '나무' not in o.name]
boxes = {}
for o in visible:
    w = [o.matrix_world @ Vector(c) for c in o.bound_box]
    boxes[o.name] = ([min(v[i] for v in w) for i in range(3)], [max(v[i] for v in w) for i in range(3)])


def ground(x, y, z_from):
    """Highest surface under (x, y) below z_from: (z, object name) or None."""
    best = None
    origin = Vector((x, y, z_from))
    for o in visible:
        lo, hi = boxes[o.name]
        if not (lo[0] - .01 <= x <= hi[0] + .01 and lo[1] - .01 <= y <= hi[1] + .01 and lo[2] <= z_from):
            continue
        mi = o.matrix_world.inverted()
        ok, loc, n, idx = o.ray_cast(mi @ origin, (mi.to_3x3() @ Vector((0, 0, -1))).normalized())
        if ok:
            z = (o.matrix_world @ loc).z
            if best is None or z > best[0]:
                best = (z, o.name)
    return best


def blocks(me):
    """Vertex ranges of the bushes: each starts with its 42-vertex icosphere core (v23 bush_template)."""
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seen = [False] * len(bm.verts)
    starts = []
    for v in bm.verts:
        if seen[v.index]:
            continue
        stack, size = [v], 0
        seen[v.index] = True
        while stack:
            a = stack.pop(); size += 1
            for e in a.link_edges:
                b = e.other_vert(a)
                if not seen[b.index]:
                    seen[b.index] = True; stack.append(b)
        if size == 42:
            starts.append(v.index)
    bm.free()
    starts.sort()
    return [(s, (starts[i + 1] if i + 1 < len(starts) else len(me.vertices))) for i, s in enumerate(starts)]


def coords(o):
    me = o.data
    flat = [0.0] * (len(me.vertices) * 3)
    me.vertices.foreach_get('co', flat)
    mw = o.matrix_world
    return [mw @ Vector(flat[i:i + 3]) for i in range(0, len(flat), 3)]


def write(o, pts):
    mi = o.matrix_world.inverted()
    flat = []
    for p in pts:
        flat.extend(mi @ p)
    o.data.vertices.foreach_set('co', flat)
    o.data.update()


def delete_verts(o, idx):
    if not idx:
        return
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in sorted(idx)], context='VERTS')
    bm.to_mesh(o.data); bm.free(); o.data.update()


for kind in ('garden', 'planter'):
    bush, flower = O[f'V23 | {kind} bushes'], O[f'V23 | {kind} flowers']
    bp, fp = coords(bush), coords(flower)
    ranges = blocks(bush.data)
    centres = [sum(bp[s:s + 42], Vector()) / 42 for (s, e) in ranges]
    # Flowers: 37 vertices each (five petals and a centre, v23 flower_geo), each on its nearest bush.
    owner = []
    for k in range(0, len(fp), 37):
        c = sum(fp[k:k + 37], Vector()) / 37
        owner.append(min(range(len(centres)), key=lambda i: (centres[i] - c).length))
    gone_b, gone_f, moved, drops = [], [], 0, []
    for i, (s, e) in enumerate(ranges):
        c = centres[i]
        bottom = min(p.z for p in bp[s:e])
        cut = (kind == 'planter' and abs(abs(c.x) - 12) < 3 and abs(c.y + 9) < 3        # clumps over the crossing
               or kind == 'garden' and 24 < abs(c.x) < 34 and -8 < c.y < -2)            # the round platforms' beds
        hit = None if cut else ground(c.x, c.y, bottom + .05)
        over_water = hit is not None and 'water' in hit[1].lower()
        if cut or hit is None or over_water or bottom - hit[0] > 3.0:
            gone_b.extend(range(s, e))
            gone_f.extend(v for k, b in enumerate(owner) if b == i for v in range(k * 37, k * 37 + 37))
            continue
        dz = hit[0] - .08 - bottom
        if abs(dz) < .02 or dz > .3:       # already resting, or would rise into something above
            continue
        for v in range(s, e):
            bp[v].z += dz
        for k, b in enumerate(owner):
            if b == i:
                for v in range(k * 37, k * 37 + 37):
                    fp[v].z += dz
        moved += 1; drops.append(round(-dz, 2))
    write(bush, bp); write(flower, fp)
    delete_verts(bush, gone_b); delete_verts(flower, gone_f)
    report[kind] = {'bushes': len(ranges), 'moved': moved, 'removed': len(gone_b) and len({i for i, (s, e) in enumerate(ranges) if s in set(gone_b)}),
                    'flowers_removed': len(gone_f) // 37, 'largest_drop_m': max(drops) if drops else 0}
    print(kind, report[kind])

# ---------------------------------------------------------------- boats stop at the walls with a knock
FPS, LOOP = 30, 720
boats = {'V02_백팀_보트_PIVOT': (0.0, -1), 'V02_흑팀_보트_PIVOT': (1.9, 1)}   # bob phase, starting end (-1: low y)
CENTRE, HALF = 41.5, 3.5
GAP = .04


def descendants(o):
    out = []
    for c in o.children:
        out.append(c); out += descendants(c)
    return out


def clearance(pivot, direction):
    """How far the boat (at its current pose) can move along +-y before any vertex meets a non-boat surface."""
    deps = bpy.context.evaluated_depsgraph_get()
    mine = {pivot.name} | {c.name for c in descendants(pivot)}
    pts = []
    for c in descendants(pivot):
        if c.type != 'MESH' or c.hide_render:
            continue
        ev = c.evaluated_get(deps)
        pts += [ev.matrix_world @ v.co for v in ev.data.vertices]
    lead = max(p.y * direction for p in pts)
    front = [p for p in pts if p.y * direction > lead - 1.2]
    d = Vector((0, direction, 0))
    best = 9.0
    for o in visible:
        if o.name in mine or 'water' in o.name.lower():
            continue
        lo, hi = boxes[o.name]
        mi = o.matrix_world.inverted(); dl = (mi.to_3x3() @ d).normalized()
        for p in front[::3]:
            if not (lo[0] <= p.x <= hi[0] and lo[2] <= p.z <= hi[2]):
                continue
            if direction > 0 and hi[1] < p.y or direction < 0 and lo[1] > p.y:
                continue
            ok, loc, n, idx = o.ray_cast(mi @ p, dl)
            if ok:
                best = min(best, ((o.matrix_world @ loc) - p).length)
    return best


ends = {}
for name in boats:
    o = O[name]
    for d in list(o.animation_data.drivers):
        o.driver_remove(d.data_path, d.array_index)
    o.rotation_mode = 'XYZ'
    res = {}
    for end in (-1, 1):
        # Measured from 1.5 m short of the old end: a ray that starts inside the wall would report its far side.
        o.location.y = CENTRE + end * (HALF - 1.5)
        o.location.z = 3.8
        o.rotation_euler = (0, 0, 0)
        bpy.context.view_layer.update()
        free = clearance(o, end)
        res[end] = CENTRE + end * (HALF - 1.5 + free - GAP)
    ends[name] = res
    report[name] = {'stop_low_y': round(res[-1], 3), 'stop_high_y': round(res[1], 3)}
    print(name, report[name])


def herm(u, m):
    return (-2 * u ** 3 + 3 * u * u) + m * (u ** 3 - u * u)


T_TRIP, T_HIT, V_HIT = 6.0, 4.6, .35
for name, (phase, start) in boats.items():
    o = O[name]
    lo, hi = ends[name][-1], ends[name][1]
    D = hi - lo
    m = V_HIT * T_HIT / D
    for f in range(1, LOOP + 2):
        t = (f - 1) / FPS
        trip = int(t // T_TRIP)
        tt = t - trip * T_TRIP
        a, b = (lo, hi) if (trip % 2 == 0) == (start == -1) else (hi, lo)
        toward = 1 if b > a else -1
        knock_pitch = knock_dip = 0.0
        if tt < T_HIT:
            y = a + (b - a) * herm(tt / T_HIT, m)
        else:
            k = tt - T_HIT
            y = b - toward * (.06 * math.exp(-k / .35) * abs(math.sin(math.pi * k / .42)))
            knock_pitch = toward * .035 * math.exp(-k / .45) * math.sin(2 * math.pi * k / .55)
            knock_dip = -.03 * math.exp(-k / .45) * math.sin(2 * math.pi * k / .6)
        w = 2 * math.pi * (f - 1)
        o.location = (o.location.x, y, 3.8 + .09 * math.sin(w / 90 + phase) + knock_dip)
        o.rotation_euler = (.03 * math.sin(w / 120 + phase + .7) + knock_pitch,
                            .06 * math.sin(w / 72 + phase + 1.3) + .4 * knock_pitch * toward, o.rotation_euler.z)
        o.keyframe_insert('location', frame=f)
        o.keyframe_insert('rotation_euler', frame=f)
    act = o.animation_data.action
    curves = act.fcurves if hasattr(act, 'fcurves') else [c for l in act.layers for s in l.strips for b in s.channelbags for c in b.fcurves]
    for c in curves:
        for kp in c.keyframe_points:
            kp.interpolation = 'LINEAR'

scene.frame_set(1)
(folder / 'build_v28_report.json').write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding='utf8')
extra = json.loads((folder / 'water_interaction_v27.json').read_text(encoding='utf8'))
(folder / 'water_interaction_v28.json').write_text(json.dumps(extra, indent=1, ensure_ascii=False), encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v28.blend'), compress=True)
print('SAVED v28')
