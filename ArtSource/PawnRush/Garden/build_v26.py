"""R283: v25 -> v26. Run in Blender 5.2: blender --background --python build_v26.py

User review 11-02 (screenshots):
- The falls poured straight off flat floors (the tank's water strip, the flat slabs on the reservoir walls). Every
  fall now leaves through an outlet: an arched opening in a stone surround (a parapet with openings on the tanks, a
  framed dark mouth on the reservoir walls) and a stone trough with cheeks and a corbel under it. The water runs
  along the trough and leaves its end; each outlet has its own curtain, streams, landing line and spray.
- The boats should sway: bob 7 cm (3 s), pitch 1.7 degrees (4 s), roll 2.9 degrees (2.4 s). Measured at both ends
  of the shuttle, the deck ends (z 4.87-4.99) sit level with the boarding pier and the far courtyard (z 5.02), so
  the sway moves an end at most about 0.19 m up or down from there, well inside the 0.87 m jump.
- Side walls: the outlets stand on the battered upper face above the moulding (z 3.45), which is up to 0.18 m proud
  of x = +-19, so they are set 0.2 m out; the centre has one outlet, left of the pilaster at x -2.3; the front
  outlets sit between the pilasters (x 10/14/18).
"""
import bpy, bmesh, math, random, json
from mathutils import Vector
from pathlib import Path

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v25.blend'))
scene = bpy.context.scene
scene.frame_set(1)
col = scene.collection
O, M = bpy.data.objects, bpy.data.materials
rng = random.Random(283)
G = 9.81
ZW = -1.70   # moat surface the falls land in

# ---------------------------------------------------------------- boats sway more
for pivot, phase in (('V02_백팀_보트_PIVOT', 0.0), ('V02_흑팀_보트_PIVOT', 1.9)):
    o = O[pivot]
    for d in list(o.animation_data.drivers):
        if (d.data_path, d.array_index) in (('location', 2), ('rotation_euler', 0), ('rotation_euler', 1)):
            o.driver_remove(d.data_path, d.array_index)
    for path, index, expr in (('location', 2, '3.8+0.07*sin(2*pi*(frame-1)/90+%r)' % phase),
                              ('rotation_euler', 0, '0.03*sin(2*pi*(frame-1)/120+%r)' % (phase + .7)),
                              ('rotation_euler', 1, '0.05*sin(2*pi*(frame-1)/72+%r)' % (phase + 1.3))):
        d = o.driver_add(path, index)
        d.driver.type = 'SCRIPTED'
        d.driver.expression = expr

# ---------------------------------------------------------------- old falls, sprays and flat spouts out
for o in [o for o in O if o.name.startswith(('V25 | fall', 'V23 | fall spray'))
          or (o.name.startswith('V22 | reservoir') and 'spout' in o.name) or o.name == 'V23 | centre spout']:
    O.remove(o)

fall_mat = M['V23 | waterfall sheet']
spray_mat = M['V23 | fall spray']
stone = M['V16 | neutral untextured clay']
shadow = M.get('V26 | spout shadow') or M.new('V26 | spout shadow')
shadow.diffuse_color = (.035, .045, .05, 1)
shadow.use_nodes = True
shadow.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.035, .045, .05, 1)

# The remains of the v04 lip sheets on the side walls stood in front of the new mouths.
src = O['V04_지형_폭포']
bm = bmesh.new(); bm.from_mesh(src.data)
mw = src.matrix_world
gone = [f for f in bm.faces if (lambda c: 18.9 < abs(c.x) < 19.8 and (37.8 < c.y < 41.6 or 45.9 < c.y < 49.6)
                                 and 2.8 < c.z < 4.4)(mw @ f.calc_center_median())]
bmesh.ops.delete(bm, geom=gone, context='FACES')
bm.to_mesh(src.data); bm.free(); src.data.update()
print('old lip faces removed', len(gone))
# The old reservoir lips' foam curves (Blender only, never exported) hung on the walls beside the new outlets.
for o in O:
    if o.type == 'CURVE' and o.name.startswith(('V07 Waterfall | left reservoir lip foam',
                                                'V07 Waterfall | right reservoir lip foam')):
        o.hide_set(True); o.hide_render = True


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


class Frame:
    """Outlet space: u across the lip, o out of the wall, z up; origin on the wall face at the trough floor."""
    def __init__(self, origin, across, out):
        self.c, self.u, self.o = origin, across, out

    def at(self, u, o, z):
        return self.c + self.u * u + self.o * o + Vector((0, 0, z))


def solid(bm, f, u0, u1, o0, o1, z0, z1, o_top=None):
    """A block in outlet space; o_top tapers the top face's outer edge (a corbel)."""
    ot = o1 if o_top is None else o_top
    v = [bm.verts.new(f.at(u, o, z)) for (u, o, z) in
         ((u0, o0, z0), (u1, o0, z0), (u1, o1, z0), (u0, o1, z0),
          (u0, o0, z1), (u1, o0, z1), (u1, ot, z1), (u0, ot, z1))]
    for q in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([v[i] for i in q])


def arch(bm, f, u_mid, half, z_spring, o0, o1, thick, segments=9):
    """A ring of voussoirs over an opening of half width `half`."""
    for k in range(segments):
        a0, a1 = math.pi * k / segments, math.pi * (k + 1) / segments
        ring = []
        for a in (a0, a1):
            for r in (half, half + thick):
                ring.append((u_mid + math.cos(a) * r, z_spring + math.sin(a) * r))
        quad = [ring[0], ring[1], ring[3], ring[2]]
        v = [bm.verts.new(f.at(u, o, z)) for o in (o0, o1) for (u, z) in quad]
        for q in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
            bm.faces.new([v[i] for i in q])


def mesh_from(name, bm, material, bevel=.025):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, profile=.6, affect='EDGES', clamp_overlap=True)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    me.materials.append(material)
    return link(name, me)


def outlet(label, f, width, rise, reach, wall_depth=0.0):
    """Stone surround, dark mouth and trough at outlet space origin. `width` is the water width; the trough runs
    from the wall face out `reach` metres. With wall_depth > 0 a parapet wall of that thickness is built too."""
    half = width / 2
    cheek = .15
    bm = bmesh.new()
    # Trough: floor, cheeks, and a corbel tapering back into the wall under it.
    solid(bm, f, -half - cheek, half + cheek, -.02, reach, -.16, 0)
    for s in (-1, 1):
        u0, u1 = (s * half, s * (half + cheek)) if s > 0 else (s * (half + cheek), s * half)
        solid(bm, f, u0, u1, -.02, reach, 0, .24)
    solid(bm, f, -half - cheek * .6, half + cheek * .6, -.02, reach * .25, -.62, -.16, o_top=reach * .92)
    # Surround: jambs, an arch and a keystone, standing a little proud of the wall.
    spring = rise - half
    for s in (-1, 1):
        u0, u1 = (half, half + .22) if s > 0 else (-half - .22, -half)
        solid(bm, f, u0, u1, -.3, .12, -.16, spring)
    arch(bm, f, 0, half, spring, -.3, .12, .22)
    solid(bm, f, -.12, .12, -.3, .16, rise - .04, rise + .3)
    mesh_from(f'V26 | spout stone {label}', bm, stone)
    if wall_depth:
        return      # a parapet's opening shows the tank water behind it
    # The dark mouth just in front of the wall behind the opening.
    bm = bmesh.new()
    pts = [(-half, 0)] + [(math.cos(math.pi - math.pi * k / 10) * half, spring + math.sin(math.pi * k / 10) * half)
                          for k in range(11)] + [(half, 0)]
    bm.faces.new([bm.verts.new(f.at(u, .005, z)) for (u, z) in pts])
    mesh_from(f'V26 | spout shadow {label}', bm, shadow, bevel=0)


def parapet(label, f, length, outlets, depth, height, opening_rise):
    """A wall along a tank's lip with openings at the outlets (u centre, half width incl. surround)."""
    bm = bmesh.new()
    edges = sorted((u - h, u + h) for (u, h) in outlets)
    cur = -length / 2
    for (a, b) in edges + [(length / 2, length / 2)]:
        if a > cur:
            solid(bm, f, cur, a, -depth, 0, -.02, height)
        cur = b
    for (a, b) in edges:     # over each opening: the arch ring in front covers the corners
        solid(bm, f, a, b, -depth, 0, opening_rise, height)
    solid(bm, f, -length / 2 - .06, length / 2 + .06, -depth - .06, .08, height, height + .14)   # coping
    mesh_from(f'V26 | spout stone {label} parapet', bm, stone, bevel=.03)


def ballistic(z0, inset, v0, zw, rows):
    T = math.sqrt(2 * (z0 - (zw - .1)) / G)
    path = [(-inset + inset * k / 4, z0) for k in range(4)]
    for j in range(1, rows + 1):
        tau = T * (j / rows) ** 1.25
        path.append((v0 * tau, z0 - .5 * G * tau * tau))
    return path, v0 * T


def fall(label, a, b, out, z0, inset, v0, zw=ZW):
    """Curtain and streams leaving the trough end between a and b (as in v25, narrower and from inside)."""
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
            p.z = z
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
            p.z = z + r * .6
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
    return e0, e1


def spray(label, e0, e1, out, zw=ZW, height=1.1):
    """Two crossed mist bands over the landing line (UV0 = along 0..1, up 0..1), lower than v23's."""
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


splashes = []


def outlets(label, wall_centre, along, out, wall_face, floor_z, total, count, water, reach, v0, parapet_depth=0.0):
    """`count` outlets spread over `total` metres of a wall face; each pours a fall of `water` metres width."""
    along, out = Vector((*along, 0)).normalized(), Vector((*out, 0)).normalized()
    centre = Vector((*wall_centre, floor_z))
    rise = water / 2 + .42
    spacing = total / count
    holes = []
    for k in range(count):
        u = -total / 2 + spacing * (k + .5)
        f = Frame(centre + along * u + out * wall_face, along, out)
        name = f'{label}{k + 1}' if count > 1 else label
        outlet(name, f, water, rise, reach, parapet_depth)
        holes.append((u, water / 2 + .22))
        a = f.at(-water / 2, reach + .02, 0); b = f.at(water / 2, reach + .02, 0)
        a.z = b.z = 0
        e0, e1 = fall(name, a, b, out, floor_z + .02, reach + .25 + parapet_depth, v0)
        splashes.append({'x0': round(e0.x, 3), 'y0': round(e0.y, 3), 'x1': round(e1.x, 3), 'y1': round(e1.y, 3), 'z': ZW})
    if parapet_depth:
        parapet(label, Frame(centre + out * wall_face, along, out), total + 2.4, holes, parapet_depth, rise + .62, rise)


for sx, side in ((-1, 'W'), (1, 'E')):
    # Tank: a parapet with four openings along its front edge (y 16.5), over its water.
    outlets(f'tank {side}', (sx * 32.0, 16.5), (1, 0), (0, -1), 0.0, 3.9, 10.0, 4, 1.0, .6, 1.5, parapet_depth=.45)
    # Reservoir side wall (face x = +-19) and front wall (face y = 30).
    for k, yc in enumerate((39.725, 47.75)):
        # Above the wall's moulding (z 3.25-3.45) and 0.2 m out, on the battered upper face (0.05-0.18 m proud of
        # x = +-19), near the reservoir's water (3.5).
        outlets(f'side {side}{k + 1}', (sx * 19.0, yc), (0, 1), (sx, 0), 0.2, 3.45, 2.65, 1, 1.1, .75, 1.6)
    # Between the wall's pilasters (x = +-10, +-14, +-18).
    outlets(f'front {side}', (sx * 14.0, 30.0), (1, 0), (0, -1), 0.0, 3.14, 8.0, 2, 1.1, .6, 1.5)
# One outlet left of the pilaster at x -2.3, low enough that its arch stays under the cornice (z 4.6).
outlets('centre', (-3.6, 18.0), (1, 0), (0, -1), 0.0, 3.3, 1.0, 1, 0.9, .7, 1.4)

extra = json.loads((folder / 'water_interaction_v25.json').read_text(encoding='utf8'))
extra['splashes'] = splashes
(folder / 'water_interaction_v26.json').write_text(json.dumps(extra, indent=1, ensure_ascii=False), encoding='utf8')
print('outlets', len(splashes))
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v26.blend'), compress=True)
print('SAVED v26')
