"""R284: v26 -> v27. Run in Blender 5.2: blender --background --python build_v27.py

User review 11-02 (photo and a screen recording):
- Some outlets looked blocked: each dark mouth is a single face and bmesh picked its normal at random, so in Unity
  the back face was culled on front E2/W1 and side E1/E2 and the wall showed through. Every mouth now faces out of
  its wall.
- Seen from the tank side, the streams started 0.23 m behind the parapet, above the tank water (3 cm deep, z 3.91),
  so they stuck out like stubs. They now start just inside the opening and sink under the water behind the wall
  face, so the water seems to run out of the tank into the opening.
- Boats sway a little more for the stronger water interaction: bob 9 cm (3 s), pitch 1.7 degrees (4 s), roll 3.4
  degrees (2.4 s). The deck ends move at most about 0.24 m from the pier height, still well inside the 0.87 m jump.
"""
import bpy, bmesh, math, random, json
from mathutils import Vector
from pathlib import Path

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v26.blend'))
scene = bpy.context.scene
scene.frame_set(1)
col = scene.collection
O, M = bpy.data.objects, bpy.data.materials
rng = random.Random(284)
G = 9.81
ZW = -1.70

# ---------------------------------------------------------------- boats
for pivot, phase in (('V02_백팀_보트_PIVOT', 0.0), ('V02_흑팀_보트_PIVOT', 1.9)):
    o = O[pivot]
    for d in list(o.animation_data.drivers):
        if (d.data_path, d.array_index) in (('location', 2), ('rotation_euler', 1)):
            o.driver_remove(d.data_path, d.array_index)
    for path, index, expr in (('location', 2, '3.8+0.09*sin(2*pi*(frame-1)/90+%r)' % phase),
                              ('rotation_euler', 1, '0.06*sin(2*pi*(frame-1)/72+%r)' % (phase + 1.3))):
        d = o.driver_add(path, index)
        d.driver.type = 'SCRIPTED'
        d.driver.expression = expr

# ---------------------------------------------------------------- mouths face out of their walls
facing = {'centre': Vector((0, -1, 0)), 'front': Vector((0, -1, 0)), 'side E': Vector((1, 0, 0)),
          'side W': Vector((-1, 0, 0))}
flipped = 0
for o in O:
    if not o.name.startswith('V26 | spout shadow'):
        continue
    label = o.name[len('V26 | spout shadow '):]
    want = next(v for k, v in facing.items() if label.startswith(k))
    bm = bmesh.new(); bm.from_mesh(o.data)
    rot = o.matrix_world.to_3x3()
    rev = [f for f in bm.faces if (rot @ f.normal).dot(want) < 0]
    if rev:
        bmesh.ops.reverse_faces(bm, faces=rev); flipped += 1
    bm.to_mesh(o.data); bm.free(); o.data.update()
print('mouths flipped', flipped)

# ---------------------------------------------------------------- tank falls start inside the openings
for o in [o for o in O if o.name.startswith(('V26 | fall curtain tank', 'V26 | fall streams tank', 'V23 | fall spray tank'))]:
    O.remove(o)
fall_mat = M['V23 | waterfall sheet']
spray_mat = M['V23 | fall spray']


def link(name, me):
    o = O.new(name, me); col.objects.link(o)
    return o


def build(name, verts, faces, uvs, colours):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.materials.append(fall_mat)
    me.polygons.foreach_set('use_smooth', [True] * len(faces))
    uv = me.uv_layers.new(name='UVMap')
    uv.data.foreach_set('uv', [c for pair in uvs for c in pair])
    ca = me.color_attributes.new('FallData', 'FLOAT_COLOR', 'CORNER')
    ca.data.foreach_set('color', [c for q in colours for c in q])
    me.color_attributes.active_color = ca
    me.color_attributes.render_color_index = me.color_attributes.find('FallData')
    me.update()
    return link(name, me)


def ballistic(z0, inset, v0, zw, rows):
    T = math.sqrt(2 * (z0 - (zw - .1)) / G)
    path = [(-inset + inset * k / 4, z0) for k in range(4)]
    for j in range(1, rows + 1):
        tau = T * (j / rows) ** 1.25
        path.append((v0 * tau, z0 - .5 * G * tau * tau))
    return path, v0 * T


def fall(label, a, b, out, z0, inset, v0, behind, zw=ZW):
    """As v26's fall(), but the part more than `behind` metres before the trough end (behind the wall face) sinks
    under the tank water, so nothing sticks out on the tank side."""
    def sink(h):
        return min(1.0, max(0.0, (-h - behind) / max(inset - behind, 1e-3)))
    across = b - a; width = across.length; across = across.normalized()
    path, reach = ballistic(z0, inset, v0, zw, 26)
    arc = [0.0]
    for k in range(1, len(path)):
        arc.append(arc[-1] + math.dist(path[k], path[k - 1]))
    n = max(4, math.ceil(width / .2))
    V, F, UV, C, grid = [], [], [], [], []
    for (h, z) in path:
        spread = max(0.0, h) / max(reach, 1e-3)
        row = []
        for i in range(n + 1):
            s = i / n
            p = a + across * (width * s + (s - .5) * .14 * width * spread) + out * h
            p.z = z - .04 * sink(h)
            row.append(len(V)); V.append(tuple(p))
        grid.append(row)
    for k in range(len(path) - 1):
        for i in range(n):
            F.append((grid[k][i], grid[k][i + 1], grid[k + 1][i + 1], grid[k + 1][i]))
            for ii, kk in ((i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1)):
                UV.append((width * ii / n, arc[kk]))
                C.append((ii / n, arc[kk] / arc[-1], .5, 0.0))
    build(f'V26 | fall curtain {label}', V, F, UV, C)
    V, F, UV, C = [], [], [], []
    count = max(2, round(width * 3.0))
    for m in range(count):
        s = (m + rng.uniform(.2, .8)) / count
        r = rng.uniform(.04, .1)
        seed = rng.random()
        pv0 = v0 * rng.uniform(.9, 1.1)
        drift = rng.uniform(-.06, .06)
        spath, sreach = ballistic(z0 + r * .2, inset, pv0, zw, 22)
        sarc = [0.0]
        for k in range(1, len(spath)):
            sarc.append(sarc[-1] + math.dist(spath[k], spath[k - 1]))
        centres = []
        for (h, z) in spath:
            t = max(0.0, h) / max(sreach, 1e-3)
            p = a + across * (width * s + drift * t * width * .1) + out * h
            p.z = z + r * .6 - r * 1.9 * sink(h)
            centres.append(p)
        base = len(V)
        for k, c in enumerate(centres):
            nxt = centres[min(k + 1, len(centres) - 1)] - centres[max(k - 1, 0)]
            t = nxt.normalized() if nxt.length > 1e-6 else Vector((0, 0, -1))
            normal = t.cross(across).normalized()
            rr = r * (1 + .3 * (sarc[k] / sarc[-1]))
            for q in range(6):
                ang = q * math.tau / 6
                V.append(tuple(c + (across * math.cos(ang) + normal * math.sin(ang)) * rr))
        for k in range(len(centres) - 1):
            for q in range(6):
                i0 = base + k * 6 + q; i1 = base + k * 6 + (q + 1) % 6
                F.append((i0, i1, i1 + 6, i0 + 6))
                for qq, kk in ((q, k), (q + 1, k), (q + 1, k + 1), (q, k + 1)):
                    UV.append((s * width + qq * (math.tau * r / 6), sarc[kk]))
                    C.append((s, sarc[kk] / sarc[-1], seed, 1.0))
    build(f'V26 | fall streams {label}', V, F, UV, C)
    e0, e1 = a + out * reach, b + out * reach
    spray(label, e0, e1, out)


def spray(label, e0, e1, out, zw=ZW, height=1.1):
    across = (e1 - e0).normalized()
    V, F, UV = [], [], []
    for off in (-.22, .22):
        a = e0 - across * .25 + out * off; b = e1 + across * .25 + out * off
        n = max(2, math.ceil((b - a).length / .5))
        base = len(V)
        for j in range(2):
            for i in range(n + 1):
                p = a + (b - a) * (i / n); V.append((p.x, p.y, zw - .08 + j * height))
        for i in range(n):
            F.append((base + i, base + i + 1, base + n + 2 + i, base + n + 1 + i))
            UV += [(i / n, 0), ((i + 1) / n, 0), ((i + 1) / n, 1), (i / n, 1)]
    me = bpy.data.meshes.new(f'V23 | fall spray {label}')
    me.from_pydata(V, [], F)
    me.materials.append(spray_mat)
    uv = me.uv_layers.new(name='UVMap')
    uv.data.foreach_set('uv', [c for pair in UV for c in pair])
    me.update()
    link(f'V23 | fall spray {label}', me)


# Same tank outlets as v26 (wall face y 16.5, floor 3.9, 4 x 1 m of water over 10 m, trough 0.6 m, parapet 0.45 m).
reach, depth, water, floor_z = .6, .45, 1.0, 3.9
for sx, side in ((-1, 'W'), (1, 'E')):
    for k in range(4):
        u = -5.0 + 2.5 * (k + .5)
        x = sx * 32.0 + u
        a, b = Vector((x - water / 2, 16.5 - (reach + .02), 0)), Vector((x + water / 2, 16.5 - (reach + .02), 0))
        # Start 4 cm inside the parapet's back face; sink behind the wall face (more than reach + 0.02 m back).
        fall(f'tank {side}{k + 1}', a, b, Vector((0, -1, 0)), floor_z + .02, reach + .02 + depth - .04, 1.5,
             behind=reach + .02)

extra = json.loads((folder / 'water_interaction_v26.json').read_text(encoding='utf8'))
(folder / 'water_interaction_v27.json').write_text(json.dumps(extra, indent=1, ensure_ascii=False), encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v27.blend'), compress=True)
print('SAVED v27')
