"""R281: v24 -> v25. Run in Blender 5.2 (or the bpy 5.2 module): blender --background --python build_v25.py

User review 11-01 (screenshots):
- The waterfalls read as flat cards, not falling water. Each fall is now a thin see-through curtain plus 3-4 round
  streams per metre of lip (radius 4-13 cm, 6 sides), each on its own ballistic path, with its data in vertex
  colours (r: across 0..1, g: lip -> landing 0..1, b: per-stream seed, a: 1 stream / 0 curtain) so the Unity shader
  does not depend on a second UV channel. UV0 = (metres across or round, metres along the flow).
- The boats floated on the reservoir (hull bottom 3.30 m, water 3.26 m). Lowering them would leave the deck 0.96 m
  under the far quay (jump 0.87 m), so the reservoir water rises 0.24 m instead and the hulls sit 0.2 m deep.
  The boats bob (3.5 cm, 3 s), pitch (0.7 degrees, 4 s) and roll (1 degree, 2.4 s) on their shuttle.
- The zigzag platforms lifted their feet out of the moat at the top of their bob: the bottom band reaches 0.8 m
  deeper so they stay in the water.
"""
import bpy, bmesh, math, random, json
from mathutils import Vector
from pathlib import Path

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v24.blend'))
scene = bpy.context.scene
scene.frame_set(1)
col = scene.collection
O, M = bpy.data.objects, bpy.data.materials
rng = random.Random(281)
G = 9.81

# ---------------------------------------------------------------- reservoir water up 0.24 m (with its pads)
RISE = 0.24
res = O['V07 Water | upper reservoir | gentle wave']
res.location.z += RISE
for name in ('V24 | lily pads', 'V04_수면_연잎'):
    o = O[name]
    for v in o.data.vertices:
        if (o.matrix_world @ v.co).z > 2.0:
            v.co.z += RISE
    o.data.update()

# ---------------------------------------------------------------- boats ride the water
for pivot, phase in (('V02_백팀_보트_PIVOT', 0.0), ('V02_흑팀_보트_PIVOT', 1.9)):
    o = O[pivot]
    for path, index, expr in (('location', 2, '3.8+0.035*sin(2*pi*(frame-1)/90+%r)' % phase),
                              ('rotation_euler', 0, '0.012*sin(2*pi*(frame-1)/120+%r)' % (phase + .7)),
                              ('rotation_euler', 1, '0.018*sin(2*pi*(frame-1)/72+%r)' % (phase + 1.3))):
        d = o.driver_add(path, index)
        d.driver.type = 'SCRIPTED'
        d.driver.expression = expr

# ---------------------------------------------------------------- zigzag platforms keep their feet wet
for o in O:
    if o.name.startswith('V16 | C platform zigzag'):
        zs = [v.co.z for v in o.data.vertices]
        z0 = min(zs)
        for v in o.data.vertices:
            if v.co.z < z0 + 0.5:
                v.co.z -= 0.8 * (1 - (v.co.z - z0) / 0.5)
        o.data.update()

# ---------------------------------------------------------------- waterfalls: curtain + streams
fall_mat = M['V23 | waterfall sheet']
old = [o for o in O if o.name.startswith(('V23 | tank fall', 'V23 | reservoir side fall', 'V23 | reservoir front fall', 'V23 | centre fall'))]
for o in old:
    O.remove(o)


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
    o = O.new(name, me); col.objects.link(o)
    return o


def ballistic(z0, inset, v0, zw, rows):
    T = math.sqrt(2 * (z0 - (zw - .1)) / G)
    path = [(-inset + inset * k / 3, z0) for k in range(3)]
    for j in range(rows + 1):
        tau = T * (j / rows) ** 1.25
        path.append((v0 * tau, z0 - .5 * G * tau * tau))
    return path, v0 * T


def fall(label, a, b, out, z0, inset, v0, zw=-1.70):
    a, b, out = Vector((*a, 0)), Vector((*b, 0)), Vector((*out, 0)).normalized()
    across = b - a; width = across.length; across.normalize()
    # Curtain: thin and see-through, frayed by the shader.
    path, reach = ballistic(z0, inset, v0, zw, 26)
    arc = [0.0]
    for k in range(1, len(path)):
        arc.append(arc[-1] + math.dist(path[k], path[k - 1]))
    n = max(4, math.ceil(width / .25))
    V, F, UV, C = [], [], [], []
    grid = []
    for k, (h, z) in enumerate(path):
        spread = max(0.0, h) / max(reach, 1e-3)
        row = []
        for i in range(n + 1):
            s = i / n
            p = a + across * (width * s + (s - .5) * .1 * width * spread) + out * h
            p.z = z
            row.append(len(V)); V.append(tuple(p))
        grid.append(row)
    for k in range(len(path) - 1):
        for i in range(n):
            F.append((grid[k][i], grid[k][i + 1], grid[k + 1][i + 1], grid[k + 1][i]))
            for ii, kk in ((i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1)):
                UV.append((width * ii / n, arc[kk]))
                C.append((ii / n, arc[kk] / arc[-1], .5, 0.0))
    build(f'V25 | fall curtain {label}', V, F, UV, C)
    # Streams: round jets of different weights on their own arcs.
    V, F, UV, C = [], [], [], []
    count = max(3, round(width * 3.4))
    for m in range(count):
        s = (m + rng.uniform(.15, .85)) / count
        r = rng.uniform(.04, .13)
        seed = rng.random()
        pv0 = v0 * rng.uniform(.88, 1.12)
        drift = rng.uniform(-.06, .06)
        spath, sreach = ballistic(z0 - rng.uniform(0, .04), inset, pv0, zw, 22)
        ring_count = 6
        base = len(V)
        sarc = [0.0]
        for k in range(1, len(spath)):
            sarc.append(sarc[-1] + math.dist(spath[k], spath[k - 1]))
        centres = []
        for k, (h, z) in enumerate(spath):
            fall_t = max(0.0, h) / max(sreach, 1e-3)
            p = a + across * (width * s + drift * fall_t * width * .1) + out * h
            p.z = z + r * .6
            centres.append(p)
        for k, c in enumerate(centres):
            nxt = centres[min(k + 1, len(centres) - 1)] - centres[max(k - 1, 0)]
            t = nxt.normalized() if nxt.length > 1e-6 else Vector((0, 0, -1))
            side = across.copy()
            normal = t.cross(side).normalized()
            rr = r * (1 + .25 * (sarc[k] / sarc[-1]))          # jets spread a little as they fall
            for q in range(ring_count):
                ang = q * math.tau / ring_count
                V.append(tuple(c + (side * math.cos(ang) + normal * math.sin(ang)) * rr))
        for k in range(len(centres) - 1):
            for q in range(ring_count):
                i0 = base + k * ring_count + q; i1 = base + k * ring_count + (q + 1) % ring_count
                j0 = i0 + ring_count; j1 = i1 + ring_count
                F.append((i0, i1, j1, j0))
                for qq, kk in ((q, k), (q + 1, k), (q + 1, k + 1), (q, k + 1)):
                    UV.append((s * width + qq * (math.tau * r / ring_count), sarc[kk]))
                    C.append((s, sarc[kk] / sarc[-1], seed, 1.0))
    build(f'V25 | fall streams {label}', V, F, UV, C)
    print(label, 'streams', count)


for sx, side in ((-1, 'W'), (1, 'E')):
    fall(f'tank {side}', (sx * 27.0, 16.5), (sx * 37.0, 16.5), (0, -1), 3.925, .30, 1.25)
    for k, (ya, yb) in enumerate(((38.4, 41.05), (46.4, 49.1))):
        fall(f'side {side}{k + 1}', (sx * 19.85, ya), (sx * 19.85, yb), (sx, 0), 3.0, .25, 1.4)
    fall(f'front {side}', (sx * 10.5, 29.45), (sx * 15.5, 29.45), (0, -1), 3.14, .30, 1.3)
fall('centre', (-4.6, 17.25), (-1.4, 17.25), (0, -1), 4.28, .25, 1.2)

extra = json.loads((folder / 'water_interaction_v24.json').read_text(encoding='utf8'))
(folder / 'water_interaction_v25.json').write_text(json.dumps(extra, indent=1, ensure_ascii=False), encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v25.blend'), compress=True)
print('SAVED v25')
