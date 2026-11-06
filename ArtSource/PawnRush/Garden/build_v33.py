"""R309: v32 -> v33. Run in Blender 5.2: blender --background --python build_v33.py

User 11-03: "지금 코스는 확정이야" - the v31 layout is final, so the graybox parts v32 left without a model get one. The
garden's own pieces are reused where they fit: the shared V19 brick module (a unit box the garden already stretches to
every size it needs) for floors, walls, roofs, posts and steps; the plaza entry piers and copings (V21) for the rails;
the round brick pillar for stems and the island; the zigzag drum, scaled evenly, for the shortcut posts. What the garden
has nothing for is modelled here in the same neutral clay: the boats, the sluice gate panels, the lotus pads, the spin
bars and the geyser rims. Everything sits exactly on the v31 graybox (RoyalWaterGardenBuilder numbers below; Blender
x, y, z = section x, z, y) and collides nowhere: the graybox's colliders stay and only stop drawing in Unity. Parts of
moving pieces are modelled at the piece's rest pose and name it ('attach'), so Unity hangs them on it.
"""
import bpy, bmesh, json, math, re
from pathlib import Path
from mathutils import Vector, Matrix

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v32.blend'))
scene = bpy.context.scene
scene.frame_set(1)
O = bpy.data.objects
v32 = json.loads((folder / 'v32_report.json').read_text(encoding='utf8'))
report = {'added': [], 'attach': dict(v32['attach']), 'notes': []}

UPPER, WEST, RES, DEEP_SEEN = 3.8, 1.8, 1.95, -1.8   # v31 heights; walls show from just under the moat (-1.7)
CLAY = bpy.data.materials['V16 | neutral untextured clay']
MODULE = bpy.data.meshes['V19 | shared brick structural module']   # x, y -0.5..0.5, z 0..1
PILLAR = bpy.data.meshes['V19 | supplied brick morphed into round pillar']
DRUM = O['V16 | C platform zigzag 1 left']
PIER, COPING = O['V21 | plaza entry pier NE'], O['V21 | plaza entry coping NE']
col = bpy.data.collections.new('V33 | v31 layout dressing')
scene.collection.children.link(col)


# R309 (승규 11-03 "새로 만들어진 구간 길 말고 장애물은 나중에 에셋으로 교체를 할거니까 색을 다르게 표시 해"): the
# obstacles here are stand-ins for assets to come, so they are named 'V19 | V33 obstacle | …' and drawn in one flat
# orange (object-level slots: the shortcut posts share the garden drums' mesh). Walks, floors, walls and rails stay clay.
OBSTACLE = re.compile(r'^(spin bar|shortcut post|shortcut lotus|geyser|sluice gate|boat )', re.I)
OBSTACLE_MAT = bpy.data.materials.new('V33 | obstacle placeholder')
OBSTACLE_MAT.diffuse_color = (.93, .42, .08, 1)


def add(name, mesh, matrix, attach=None):
    obstacle = OBSTACLE.match(name) is not None
    o = O.new(('V19 | V33 obstacle | ' if obstacle else 'V19 | V33 ') + name, mesh)
    o.matrix_world = matrix
    col.objects.link(o)
    if obstacle:
        if not o.material_slots:
            mesh.materials.append(CLAY)
        for slot in o.material_slots:
            slot.link = 'OBJECT'
            slot.material = OBSTACLE_MAT
    report['added'].append(o.name)
    if attach:
        report['attach'][o.name] = attach
    return o


def mesh_box(me):
    lo = Vector([min(v.co[i] for v in me.vertices) for i in range(3)])
    hi = Vector([max(v.co[i] for v in me.vertices) for i in range(3)])
    return lo, hi


def fit(me, x0, x1, y0, y1, z0, z1):
    """The matrix that stretches mesh `me`'s own box onto the world box."""
    lo, hi = mesh_box(me)
    s = Vector(((x1 - x0) / (hi.x - lo.x), (y1 - y0) / (hi.y - lo.y), (z1 - z0) / (hi.z - lo.z)))
    return Matrix.Translation((x0, y0, z0)) @ Matrix.Diagonal((*s, 1)) @ Matrix.Translation(-lo)


def block(name, x0, x1, y0, y1, z0, z1, attach=None):
    return add(name, MODULE, fit(MODULE, x0, x1, y0, y1, z0, z1), attach)


def floor(name, x0, x1, y0, y1, top, thick=1.2, cell=5.6):
    """Brick slabs about `cell` metres square paving a rectangle, top at `top` (the garden's own slab size)."""
    nx, ny = max(1, round((x1 - x0) / cell)), max(1, round((y1 - y0) / cell))
    for i in range(nx):
        for j in range(ny):
            block('%s %d.%d' % (name, i, j), x0 + (x1 - x0) * i / nx, x0 + (x1 - x0) * (i + 1) / nx,
                  y0 + (y1 - y0) * j / ny, y0 + (y1 - y0) * (j + 1) / ny, top - thick, top)


def wall(name, x0, x1, y0, y1, z0, z1, cell=5.2):
    """A wall of stacked bricks blocks along its long side."""
    along_x = (x1 - x0) >= (y1 - y0)
    length = (x1 - x0) if along_x else (y1 - y0)
    n, rows = max(1, round(length / cell)), max(1, round((z1 - z0) / 3.6))
    for i in range(n):
        for r in range(rows):
            a, c = i / n, (i + 1) / n
            za, zc = z0 + (z1 - z0) * r / rows, z0 + (z1 - z0) * (r + 1) / rows
            if along_x:
                block('%s %d.%d' % (name, i, r), x0 + (x1 - x0) * a, x0 + (x1 - x0) * c, y0, y1, za, zc)
            else:
                block('%s %d.%d' % (name, i, r), x0, x1, y0 + (y1 - y0) * a, y0 + (y1 - y0) * c, za, zc)


def rail(name, x0, x1, y0, y1, floor_z, inward, pitch=0.0):
    """The plaza's entry parapet (pier + coping) along a v31 rail: its walk-side face on the rail's walk-side face,
    the rest outside (`inward` = from the rail towards the walk: 'x+', 'x-', 'y+', 'y-'), as high over the floor as at
    the plaza; `pitch` rises it to the north (the ramp's rail)."""
    plo, phi = mesh_box(PIER.data)
    # The NE pier's walk side is its -x face; local frame: that face at x = 0, outward +x, its middle at y = 0.
    to_local = Matrix.Translation((-plo.x, -(plo.y + phi.y) / 2, 0))
    piece = phi.y - plo.y
    along_y = inward[0] == 'x'
    length = (y1 - y0) if along_y else (x1 - x0)
    n = max(1, round(length / piece))
    seg = length / n
    turn = {'x-': 0, 'x+': math.pi, 'y+': -math.pi / 2, 'y-': math.pi / 2}[inward]
    for i in range(n):
        if along_y:
            edge = x0 if inward == 'x-' else x1
            yc = y0 + seg * (i + .5)
            at = Vector((edge, yc, floor_z + math.tan(pitch) * (yc - y0)))
        else:
            edge = y0 if inward == 'y-' else y1
            at = Vector((x0 + seg * (i + .5), edge, floor_z))
        stretch = seg / math.cos(pitch) / piece
        m = (Matrix.Translation(at) @ Matrix.Rotation(pitch, 4, 'X') @ Matrix.Rotation(turn, 4, 'Z')
             @ Matrix.Diagonal((1, stretch, 1, 1)) @ to_local)
        add('%s pier %d' % (name, i), PIER.data, m)
        add('%s coping %d' % (name, i), COPING.data, m)


def pillar(name, x, y, z0, z1, d, attach=None):
    return add(name, PILLAR, fit(PILLAR, x - d / 2, x + d / 2, y - d / 2, y + d / 2, z0, z1), attach)


def new_mesh(name, build):
    bm = bmesh.new()
    build(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new('V33 | ' + name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(CLAY)
    for p in me.polygons:
        p.use_smooth = False
    return me


def bevel_box(bm, x0, x1, y0, y1, z0, z1, off=.03):
    r = bmesh.ops.create_cube(bm, size=1)
    vs = r['verts']
    for v in vs:
        v.co = Vector((x0 if v.co.x < 0 else x1, y0 if v.co.y < 0 else y1, z0 if v.co.z < 0 else z1))
    edges = list({e for v in vs for e in v.link_edges})
    bmesh.ops.bevel(bm, geom=edges, offset=off, segments=2, profile=.5, affect='EDGES')


def cylinder(bm, x, y, z0, z1, r, seg=24, bevel=.03):
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r, depth=z1 - z0,
                                matrix=Matrix.Translation((x, y, (z0 + z1) / 2)))
    if bevel:
        rims = [e for e in {e for v in res['verts'] for e in v.link_edges} if len(e.link_faces) == 2 and
                abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-4]
        bmesh.ops.bevel(bm, geom=rims, offset=bevel, segments=2, profile=.5, affect='EDGES')


def platform_top(i):
    return Vector((-21.0 + 6.0 * i, 2.5 if i % 2 == 0 else 6.5, 2.0 + .25 * i + .35))


# ------------------------------------------------------------------ beat 1: the start yard's rails and the spin bars

rail('start yard rail W', -6.3, -6, -61, -51.2, 0, 'x+')
rail('start yard rail E', 6, 6.3, -61, -51.2, 0, 'x-')

BARS = [(-6.5, -43.5, 4.5, False), (6.5, -43.5, 4.5, False), (0, -35, 5, True), (-8, -27.5, 4.5, False), (8, -27.5, 4.5, False)]


def spin_bar(target, x, y, z, reach, tall):
    """Hub and arms at rest (the mover unturned; arm k turned k * 360 / arms about up, as Unity's Euler(0, a, 0))."""
    arms = 3 if tall else 2
    y0, y1, half = (.1, 1.2, .25) if tall else (.25, .55, .175)
    hub_d = 1.4 if tall else 1.0
    hub = new_mesh('spin bar hub %s' % target, lambda bm: (cylinder(bm, 0, 0, 0, y1 + .2, hub_d / 2, 32, .05),
                                                              cylinder(bm, 0, 0, y1 + .2, y1 + .35, hub_d / 2 + .08, 32, .04)))
    add(target + ' hub', hub, Matrix.Translation((x, y, z)), target)

    def arm(bm):
        if tall:
            bevel_box(bm, .3, reach, -half, half, y0, y1, .06)
        else:
            res = bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=half, radius2=half, depth=reach - .3,
                                        matrix=Matrix.Translation(((reach + .3) / 2, 0, (y0 + y1) / 2)) @ Matrix.Rotation(math.pi / 2, 4, 'Y'))
            bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=half * 1.25,
                                      matrix=Matrix.Translation((reach, 0, (y0 + y1) / 2)))
    me = new_mesh('spin bar arm %s' % ('tall' if tall else 'low %.2f' % reach), arm)
    for k in range(arms):
        a = math.radians(k * 360 / arms)
        # Unity's Euler(0, a, 0) turns +x to (cos a, -sin a) on the ground (Blender x, y).
        add('%s arm %d' % (target, k + 1), me, Matrix.Translation((x, y, z)) @ Matrix.Rotation(-a, 4, 'Z'), '%s/Arm %d' % (target, k + 1))


for n, (x, y, reach, tall) in enumerate(BARS, 1):
    spin_bar('Spin bar %d' % n, x, y, 0.0, reach, tall)

# ------------------------------------------------------------------ beat 2: the geyser canal and the ramp

floor('canal walk', -35, -6.6, -18.5, -12.5, 0)
floor('canal walk middle', -6.6, 6.6, -17.45, -12.5, 0)     # the plaza's north infill (top 0) reaches -17.5
floor('canal walk east', 6.6, 7, -18.5, -12.5, 0)
rail('canal walk rail S', -35, -7, -18.8, -18.5, 0, 'y+')
rail('canal walk rail N', -27, 7, -12.5, -12.2, 0, 'y-')
rail('canal walk rail E', 7, 7.3, -18.5, -12.2, 0, 'x-')
wall('canal west wall', -36.2, -35, -18.8, 8.3, DEEP_SEEN, WEST + 1.4)

pitch = math.atan2(WEST, 11.5)
for i in range(2):
    for j in range(3):
        xa, xb = -35 + 4 * i, -31 + 4 * i
        ya, yb = -12.5 + 11.5 * j / 3, -12.5 + 11.5 * (j + 1) / 3
        top = Vector(((xa + xb) / 2, (ya + yb) / 2, WEST * ((ya + yb) / 2 + 12.5) / 11.5))
        add('west ramp %d.%d' % (i, j), MODULE, Matrix.Translation(top) @ Matrix.Rotation(pitch, 4, 'X')
            @ Matrix.Translation((0, 0, -1.2)) @ Matrix.Diagonal((xb - xa, (yb - ya) / math.cos(pitch) + .02, 1.2, 1)))
rail('west ramp rail E', -27.3, -27, -12.5, -1, 0, 'x-', pitch)

GEYSERS = [(-3, -17), (-8, -14), (-13, -17), (-18, -14), (-23, -17), (-28, -14), (-33, -9), (-29, -4.5)]
rim = new_mesh('geyser rim', lambda bm: (
    [bmesh.ops.create_cone(bm, cap_ends=False, segments=32, radius1=r, radius2=r, depth=.08) for r in (1.2,)],
    cylinder(bm, 0, 0, -.06, .04, 1.2, 32, .02),
    [bevel_box(bm, -1.0, 1.0, k * .3 - .04, k * .3 + .04, -.02, .045, .01) for k in range(-3, 4)]))
for n, (x, y) in enumerate(GEYSERS, 1):
    z = WEST * (y + 12.5) / 11.5 if y > -12.5 else 0.0
    add('geyser %d rim' % n, rim, Matrix.Translation((x, y, z + .02)))

# ------------------------------------------------------------------ beat 3: the west landing, the shortcut

floor('west landing', -35, -25.2, -1, 8, WEST)
rail('west landing rail N', -35, -25.2, 8, 8.3, WEST, 'y-')
rail('west landing rail S', -27, -25.2, -1.3, -1, WEST, 'y+')

P4 = platform_top(3)
LANDING = Vector((19.4, 24.0, UPPER))
along = (Vector((LANDING.x - P4.x, LANDING.y - P4.y, 0))).normalized()


def shortcut_at(s):
    return Vector((P4.x, P4.y, 0)) + along * s


drum_lo, drum_hi = mesh_box(DRUM.data)
drum_top = 2.05   # the drum mesh's walking top (local z), measured in v32


def small_drum(name, at, top, d, attach=None):
    s = d / (drum_hi.x - drum_lo.x)
    return add(name, DRUM.data, Matrix.Translation((at.x, at.y, top - drum_top * s)) @ Matrix.Diagonal((s, s, s, 1)), attach)


for i, (s, base) in enumerate(zip((5.8, 9.3, 12.8), (3.25, 3.35, 3.45))):
    at = shortcut_at(s)
    small_drum('shortcut post K%d' % (i + 1), at, base + .4, 1.2, 'Shortcut post K%d' % (i + 1))
    pillar('shortcut post K%d stem' % (i + 1), at.x, at.y, DEEP_SEEN, base - .7, .5)
k4 = shortcut_at(16.9)
small_drum('shortcut post K4', k4, 3.4, 2.6)
pillar('shortcut post K4 stem', k4.x, k4.y, DEEP_SEEN, 3.4 - 1.9, 2.2)
spin_bar('Shortcut post K4 spin bar', k4.x, k4.y, 3.4, 1.25, False)


def lotus(bm):
    # A pad 1.6 m across, 0.12 thick, rim curled up 4 cm, a notch to the centre, a knot in the middle.
    seg, notch = 48, math.radians(24)
    top, bot, ring = [], [], []
    for k in range(seg + 1):
        a = notch / 2 + (2 * math.pi - notch) * k / seg
        for r, z, lst in ((.8, .04, top), (.8, -.08, bot)):
            lst.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
    c_top, c_bot = bm.verts.new((0, 0, 0)), bm.verts.new((0, 0, -.12))
    for k in range(seg):
        bm.faces.new((c_top, top[k], top[k + 1]))
        bm.faces.new((c_bot, bot[k + 1], bot[k]))
        bm.faces.new((top[k], bot[k], bot[k + 1], top[k + 1]))
    bm.faces.new((c_top, c_bot, bot[0], top[0]))
    bm.faces.new((c_top, top[seg], bot[seg], c_bot))
    cylinder(bm, 0, 0, -.02, .05, .09, 12, .02)


pad = new_mesh('lotus pad', lotus)
for i, s in enumerate((21.2, 25.1)):
    at = shortcut_at(s)
    add('shortcut lotus L%d' % (i + 1), pad, Matrix.Translation((at.x, at.y, 3.3)), 'Shortcut lotus L%d' % (i + 1))
    pillar('shortcut lotus L%d stem' % (i + 1), at.x, at.y, DEEP_SEEN, 3.3 - .6, .35)

# ------------------------------------------------------------------ beat 4: the east lock

floor('east landing', 25.2, 35, 0, 10, UPPER)
rail('east landing rail S', 25.2, 35, -.3, 0, UPPER, 'y+')
rail('east landing rail N', 25.2, 26.4, 9.7, 10, UPPER, 'y-')
floor('lock floor', 27, 35, 10, 24, UPPER)
wall('lock wall W', 25.8, 27, 10, 24, DEEP_SEEN, UPPER + 1.8)
wall('lock wall E', 35, 36.2, -.3, 32.6, DEEP_SEEN, UPPER + 1.8)


def gate_panel(bm):
    # Ten oak planks (2 cm apart) between two iron straps, 8 x 2.6 x 0.6 m, standing on its bottom edge.
    for k in range(10):
        x0 = -4 + k * .8
        bevel_box(bm, x0 + .01, x0 + .79, -.22, .22, 0, 2.6, .04)
    for z in (.45, 2.0):
        bevel_box(bm, -4, 4, -.3, .3, z, z + .18, .02)


panel = new_mesh('sluice gate panel', gate_panel)
for i, z in enumerate((13, 17.5, 22), 1):
    add('sluice gate %d' % i, panel, Matrix.Translation((31, z, UPPER)), 'Sluice gate %d' % i)
    block('lock gate post W %d' % i, 25.8, 27, z - .45, z + .45, UPPER + 1.8, 10.2)
    block('lock gate post E %d' % i, 35, 36.2, z - .45, z + .45, UPPER + 1.8, 10.2)
    block('lock gate beam %d' % i, 25.8, 36.2, z - .45, z + .45, 9.6, 10.4)

# ------------------------------------------------------------------ beat 5: the robot dyke (the quay's blocks start at y 26)

floor('robot dyke', 10, 35, 24, 30, UPPER)
floor('robot dyke west', 8, 10, 24, 26, UPPER)
floor('robot dyke north', 10, 23.5, 30, 32.6, UPPER)   # east of 23.5 the terrace's own blocks are this floor
rail('dyke wall N', 10, 35, 30, 30.5, UPPER, 'y-')

# ------------------------------------------------------------------ beat 6: the quay's rails, the reservoir, the tunnels, the boats

rail('boarding quay rail W', -10.3, -10, 27, 33, UPPER, 'x+')
rail('boarding quay rail S', -10, 8, 26.7, 27, UPPER, 'y+')
wall('reservoir wall S', -26, 26, 32.6, 33.2, DEEP_SEEN, 2.6)
LANES = (35.4, 39, 42.6, 46.2)
TUNNEL_WALLS = ((33, 34.2), (36.6, 37.8), (40.2, 41.4), (43.8, 45), (47.4, 48.6))
for s in (-1, 1):
    xa, xb = (18, 26) if s > 0 else (-26, -18)
    side = 'E' if s > 0 else 'W'
    floor('tunnel roof ' + side, xa, xb, 33, 48.6, 6.2)
    block('tunnel eave ' + side, *((18, 18.6) if s > 0 else (-18.6, -18)), 33, 48.6, 4.4, 5)
    for k, (a, b) in enumerate(TUNNEL_WALLS):
        block('tunnel wall %s %d' % (side, k), xa, xb, a, b, DEEP_SEEN, 5)


def boat(bm):
    # A flat-bottomed garden punt 6 x 2.4 m, its deck at 0 and keel at -1.8: a straight middle, rounded ends, the
    # bottom narrower than the deck, a 10 cm gunwale and five deck planks.
    nx, nr = 28, 10
    rings = []
    for i in range(nx + 1):
        t = -1 + 2 * i / nx
        end = max(0.0, min(1.0, (1 - abs(t)) / .28))
        w = 1.2 * (end ** .5 if end < 1 else 1) + .001
        ring = []
        for k in range(nr + 1):
            u = k / nr                     # 0 = port deck edge, 1 = starboard deck edge, round the bottom
            a = math.pi * u
            y = -w * math.cos(a)
            z = -1.8 * (math.sin(a) ** .35) * (.35 + .65 * end ** .4)
            ring.append(bm.verts.new((3 * t, y * (1 - .3 * math.sin(a) ** 2), z)))
        rings.append(ring)
    for i in range(nx):
        for k in range(nr):
            bm.faces.new((rings[i][k], rings[i + 1][k], rings[i + 1][k + 1], rings[i][k + 1]))
    deck = [r[0] for r in rings] + [r[-1] for r in reversed(rings)]
    bm.faces.new(deck)
    # The gunwale: one band round the deck edge, 10 cm high, pulled 12 cm in towards the middle.
    edge = [v.co.copy() for v in deck]
    mid = Vector((0, 0, 0))
    outer = [bm.verts.new((p.x, p.y, .1)) for p in edge]
    inner = [bm.verts.new((p.x * (1 - .12 / 3), p.y - math.copysign(min(.12, abs(p.y)), p.y), .1)) for p in edge]
    foot = [bm.verts.new((v.co.x, v.co.y, 0)) for v in inner]
    n = len(edge)
    for i in range(n):
        j = (i + 1) % n
        if (edge[i] - edge[j]).length < 1e-4:
            continue
        bm.faces.new((deck[i], deck[j], outer[j], outer[i]))
        bm.faces.new((outer[i], outer[j], inner[j], inner[i]))
        bm.faces.new((inner[i], inner[j], foot[j], foot[i]))
    for k in range(5):
        y = -.8 + .4 * k
        bevel_box(bm, -2.5, 2.5, y - .17, y + .17, 0, .015, .005)


hull = new_mesh('garden boat', boat)
for lane, z in enumerate(LANES, 1):
    for n in (1, 2):
        x = -9 + 18 * (n - 1)
        add('boat %d-%d' % (lane, n), hull, Matrix.Translation((x, z, UPPER)), 'Boat %d-%d' % (lane, n))

# ------------------------------------------------------------------ beat 7: the gate yard's rails, the way out

floor('gate yard south strip', -14, 14, 48.6, 49.5, UPPER, cell=7)
rail('gate yard rail W', -14.3, -14, 48.6, 60.5, UPPER, 'x+')
rail('gate yard rail E', 14, 14.3, 48.6, 60.5, UPPER, 'x-')
floor('exit terrace', -6, 6, 60, 66.5, UPPER)
rail('exit terrace rail W', -6.3, -6, 62, 66.1, UPPER, 'x+')
rail('exit terrace rail E', 6, 6.3, 62, 66.1, UPPER, 'x-')
block('exit arch pillar W', -7.5, -6, 66.1, 66.9, UPPER - 1, UPPER + 6)
block('exit arch pillar E', 6, 7.5, 66.1, 66.9, UPPER - 1, UPPER + 6)
block('exit arch lintel', -7.5, 7.5, 65.9, 67.1, UPPER + 5, UPPER + 6.4)
for i in range(1, 5):
    block('step down %d' % i, -6, 6, 66.5 + (i - 1) * .5, 66.5 + i * .5, UPPER - .25 * i - 1.2, UPPER - .25 * i)
floor('mini-game entrance band', -6, 6, 68.5, 72.5, UPPER - 1)
rail('band rail W', -6.3, -6, 66.9, 72.5, UPPER - 1, 'x+')
rail('band rail E', 6, 6.3, 66.9, 72.5, UPPER - 1, 'x-')

# ------------------------------------------------------------------ the island (scenery)

pillar('garden island', -18, 16, DEEP_SEEN, .6, 10)

bpy.context.view_layer.update()
report['notes'].append('added %d objects' % len(report['added']))
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v33.blend'))
(folder / 'v33_report.json').write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding='utf8')
print('V33', len(report['added']), 'added', len(report['attach']), 'attached', flush=True)
