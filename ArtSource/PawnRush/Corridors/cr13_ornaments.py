# Ornaments shared by the corridor modules, built from the GPT sheets in
# Docs/PawnRush/ConceptArt/SectionCorridors/AssetRefs (modelled here, no Tripo): fleur-de-lis, scallop spout, urn
# finial, baluster, roses, boxwood, planter bowl, mouldings. Every function adds to a cr_common.Mesh.
import math
from mathutils import Vector, Matrix


def _strip(centre, widths):
    """A 2D ribbon polygon round a centre line (points), half-width per point."""
    left, right = [], []
    n = len(centre)
    for i, (u, v) in enumerate(centre):
        a = centre[max(i - 1, 0)]
        b = centre[min(i + 1, n - 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        l = math.hypot(tx, ty) or 1
        nx, ny = -ty / l, tx / l
        w = widths[i]
        left.append((u + nx * w, v + ny * w))
        right.append((u - nx * w, v - ny * w))
    return left + list(reversed(right))


def _bezier(p0, p1, p2, p3, n):
    out = []
    for i in range(n + 1):
        t = i / n
        s = 1 - t
        out.append((s ** 3 * p0[0] + 3 * s * s * t * p1[0] + 3 * s * t * t * p2[0] + t ** 3 * p3[0],
                    s ** 3 * p0[1] + 3 * s * s * t * p1[1] + 3 * s * t * t * p2[1] + t ** 3 * p3[1]))
    return out


def fleur_polygons():
    """A fleur-de-lis as separate 2D pieces in a unit box: u -0.5..0.5 across, v 0..1 up."""
    pieces = []
    # the tall centre petal: a pointed leaf
    right = []
    for i in range(11):
        t = i / 10
        v = .36 + .64 * t
        w = .17 * math.sin(math.pi * min(t * 1.15, 1.0)) ** .75 * (1 - .25 * t)
        right.append((w, v))
    right[-1] = (0.0, 1.0)
    pieces.append(right + [(-u, v) for u, v in reversed(right[:-1])])
    # the band
    pieces.append([(-.24, .3), (.24, .3), (.24, .39), (-.24, .39)])
    # the side petals: out from the stem, over and down into a curl
    curve = _bezier((.07, .40), (.12, .62), (.42, .70), (.46, .53), 9)[:-1] + _bezier((.46, .53), (.48, .43), (.40, .38), (.35, .44), 5)
    widths = [max(.022, .085 * (1 - i / (len(curve) - 1)) ** .7) for i in range(len(curve))]
    side = _strip(curve, widths)
    pieces.append(side)
    pieces.append([(-u, v) for u, v in side])
    # below the band: a short stub and two little curls
    pieces.append([(-.07, .3), (.07, .3), (.05, .17), (0, .1), (-.05, .17)])
    low = _bezier((.05, .3), (.12, .22), (.26, .2), (.24, .1), 6)
    lw = [max(.018, .045 * (1 - i / 6)) for i in range(7)]
    ls = _strip(low, lw)
    pieces.append(ls)
    pieces.append([(-u, v) for u, v in ls])
    return pieces


def fleur(m, mat, size, plane_axis, at, depth0, depth1, flip_u=False):
    """A fleur-de-lis relief: plane perpendicular to `plane_axis` ('x' or 'y'), centred on `at` (u, v of its centre
    in that plane), standing from depth0 to depth1 along the axis."""
    cu, cv = at
    # the pieces overlap where they join (petals into the band): each stands 1.5 mm proud of the one before and starts
    # 1 mm off the back plane, so no two faces share a plane (flicker, 승규 11-22)
    step = .0015 if depth1 >= depth0 else -.0015
    for i, poly in enumerate(fleur_polygons()):
        pts = [((-u if flip_u else u) * size + cu, (v - .55) * size + cv) for u, v in poly]
        m.prism(pts, plane_axis, depth0 + step * (i + .7), depth1 + step * i, mat, bevel=0)


def baluster(m, mat, x, y, z0, h, block=.16, low=False):
    """A vase baluster with square foot and head blocks, standing h tall from z0."""
    b = block / 2
    m.box(x - b, x + b, y - b, y + b, z0, z0 + .08 * h / .55, mat, bevel=.01)
    m.box(x - b, x + b, y - b, y + b, z0 + h - .08 * h / .55, z0 + h, mat, bevel=.01)
    k = h / .55
    prof = [(.06, .08), (.072, .1), (.05, .14), (.064, .17), (.088, .25), (.084, .3), (.06, .37), (.042, .41),
            (.05, .43), (.062, .45), (.062, .47)]
    if low:
        prof = [(.06, .08), (.05, .14), (.088, .25), (.06, .37), (.042, .41), (.062, .47)]
    m.lathe([(r * k * .95, z0 + hh * k) for r, hh in prof], (x, y, 0), mat, segs=8)


def urn(m, mat, gold, x, y, z0, h=.78, flutes=12):
    """An urn finial on its own little plinth: fluted bowl, gold band, domed lid, artichoke knob."""
    k = h / .78
    m.box(x - .2 * k, x + .2 * k, y - .2 * k, y + .2 * k, z0, z0 + .07 * k, mat, bevel=.012)
    m.box(x - .17 * k, x + .17 * k, y - .17 * k, y + .17 * k, z0 + .07 * k, z0 + .1 * k, mat, bevel=.008)
    def body(a, hh):
        if .16 * k < hh - z0 < .38 * k:
            return 1 - .055 * abs(math.cos(a * flutes / 2))
        return 1.0
    prof = [(.07, .1), (.06, .12), (.045, .15), (.06, .17), (.1, .19), (.15, .24), (.17, .3), (.165, .36),
            (.15, .385)]
    m.lathe([(r * k, z0 + hh * k) for r, hh in prof], (x, y, 0), mat, segs=16, radial=body, cap_bottom=True,
            cap_top=False)
    m.lathe([(.158 * k, z0 + .385 * k), (.162 * k, z0 + .4 * k), (.162 * k, z0 + .43 * k), (.158 * k, z0 + .445 * k)],
            (x, y, 0), gold, segs=16, cap_bottom=False, cap_top=False)
    m.lathe([(.152 * k, z0 + .445 * k), (.165 * k, z0 + .47 * k), (.14 * k, z0 + .52 * k), (.08 * k, z0 + .56 * k),
             (.05 * k, z0 + .58 * k), (.045 * k, z0 + .6 * k)], (x, y, 0), mat, segs=16, cap_bottom=True, cap_top=True)
    leaf = lambda a, hh: 1 + .12 * abs(math.sin(a * 3))
    m.lathe([(.03 * k, z0 + .6 * k), (.06 * k, z0 + .64 * k), (.065 * k, z0 + .69 * k), (.045 * k, z0 + .74 * k),
             (.015 * k, z0 + .78 * k)], (x, y, 0), mat, segs=12, radial=leaf, cap_bottom=True, cap_top=True)


def shell_spout(m, stone, gold, water, x_face, y, zc, r=.3):
    """A scallop-shell plaque on a wall face at x_face (west wall: the wall at x < x_face, the plaque toward +x),
    hinge at the bottom (zc), a gold-ringed spout pipe and a stream down to z 0.24."""
    import bmesh as _bm
    bm = m.bm
    before = set(bm.faces)
    nt, nr = 20, 4
    grid = []
    for j in range(nr + 1):
        row = []
        for i in range(nt + 1):
            th = math.pi * i / nt
            R = r * (1 + .06 * math.cos(18 * th))
            rr = R * (.04 + .96 * j / nr)
            bulge = .02 + .085 * (1 - (j / nr) ** 1.6) * (.55 + .45 * abs(math.cos(9 * th)))
            row.append(bm.verts.new((x_face + bulge, y + rr * math.cos(th), zc + rr * math.sin(th))))
        grid.append(row)
    back = [bm.verts.new((x_face + .005, v.co.y, v.co.z)) for v in grid[nr]]
    hinge = bm.verts.new((x_face + .005, y, zc))
    for j in range(nr):
        for i in range(nt):
            bm.faces.new([grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]])
    for i in range(nt):
        bm.faces.new([grid[nr][i], back[i], back[i + 1], grid[nr][i + 1]])
    bm.faces.new([hinge] + back)
    # the flat underside along the hinge line, both halves, and the little fan round the hinge
    bm.faces.new([hinge] + [grid[j][0] for j in range(nr + 1)] + [back[0]])
    bm.faces.new([hinge, back[nt]] + [grid[j][nt] for j in range(nr, -1, -1)])
    for i in range(nt):
        bm.faces.new([hinge, grid[0][i + 1], grid[0][i]])
    new = [f for f in bm.faces if f not in before]
    _bm.ops.recalc_face_normals(bm, faces=new)
    idx = m.mi(stone)
    for f in new:
        f.material_index = idx
        f.smooth = True
    # the two volutes under the hinge
    for s in (-1, 1):
        path = []
        for i in range(14):
            t = i / 13
            ang = math.pi * .5 + s * 2.6 * math.pi * t
            rad = .1 * (1 - .75 * t)
            path.append((x_face + .035, y + s * (.12 + rad * math.cos(ang) * s), zc - .02 + rad * math.sin(ang) - .06))
        m.tube(path, .022, stone, segs=6, radii=[.03 - .018 * i / 13 for i in range(14)])
    # the spout: a short stone pipe from the shell's foot, a gold ring at its mouth
    sz = zc + .04
    m.lathe([(.05, 0), (.05, .14), (.042, .14)], (x_face, y, sz), stone, segs=12, axis="x", cap_top=False)
    m.lathe([(.056, .12), (.058, .17), (.05, .18), (.036, .18)], (x_face, y, sz), gold, segs=12, axis="x",
            cap_bottom=False, cap_top=False)
    mouth = x_face + .18
    path = []
    for i in range(9):
        t = i / 8
        path.append((mouth + .1 * t, y, sz - (sz - .24) * t * t))
    m.tube(path, .022, water, segs=8, radii=[.022 + .006 * i / 8 for i in range(9)])


def rose(m, red, x, y, z, s=1.0, turn=0.):
    """A stylised rose head: an open cup of five petals round a closed bud."""
    petals = lambda a, hh: 1 + .2 * math.cos(5 * a)
    m.lathe([(.001, 0), (.04 * s, .005 * s), (.07 * s, .025 * s), (.08 * s, .05 * s), (.072 * s, .065 * s)],
            (x, y, z), red, segs=10, radial=petals, cap_bottom=False, cap_top=False, phase=turn)
    m.lathe([(.001, .01 * s), (.045 * s, .03 * s), (.05 * s, .06 * s), (.035 * s, .08 * s), (.001, .085 * s)],
            (x, y, z), red, segs=8, radial=lambda a, hh: 1 + .12 * math.cos(3 * a), phase=turn + .5)


def leaf(m, green, at, direction, length=.11, width=.05):
    """A flat pointed leaf from `at` toward `direction` (3D)."""
    a = Vector(at)
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < .1:
        side = d.cross(Vector((1, 0, 0)))
    side.normalize()
    up = side.cross(d).normalized()
    pts = [a, a + d * length * .45 + side * width, a + d * length, a + d * length * .45 - side * width]
    bm = m.bm
    vs = [bm.verts.new(p) for p in pts]
    vb = [bm.verts.new(p - up * .008) for p in pts]
    f1 = bm.faces.new(vs)
    f2 = bm.faces.new(list(reversed(vb)))
    sides = []
    for i in range(4):
        j = (i + 1) % 4
        sides.append(bm.faces.new([vs[j], vs[i], vb[i], vb[j]]))
    idx = m.mi(green)
    for f in [f1, f2] + sides:
        f.material_index = idx


def planter(m, stone, soil, box_green, box_light, reds, leaf_green, x, y, z0, r=.42, seed=3):
    """A wall-niche planter: a fluted limestone bowl on a moulded foot, a clipped boxwood ball behind, roses in front.
    The bowl's back half sinks into the niche's back wall."""
    import random
    rnd = random.Random(seed)
    flute = lambda a, hh: 1 - .045 * abs(math.cos(a * 8)) if hh > z0 + .14 else 1.0
    prof = [(.16, 0), (.18, .03), (.14, .06), (.12, .09), (.2, .13), (.32, .18), (.4, .25), (.43, .32), (.42, .36),
            (.38, .37)]
    m.lathe([(rr * r / .42, z0 + hh) for rr, hh in prof], (x, y, 0), stone, segs=20, radial=flute, cap_top=False)
    m.lathe([(.38 * r / .42, z0 + .33)], (x, y, 0), soil, segs=20, cap_bottom=False, cap_top=True)
    # boxwood ball behind, toward the wall
    m.blob((x - .12, y, z0 + .66), .3, box_green, subdiv=2, noise=.06, seed=seed)
    for i in range(3):
        a = rnd.uniform(0, 2 * math.pi)
        m.blob((x - .12 + .26 * math.cos(a), y + .26 * math.sin(a), z0 + .6 + rnd.uniform(-.1, .2)), .08, box_light,
               subdiv=1, noise=.15, seed=seed + i)
    # roses along the bowl's front rim
    spots = [(.12, -.3), (.25, -.1), (.25, .12), (.12, .3), (.04, .02), (-.06, -.24), (-.06, .26)]
    for i, (dx, dy) in enumerate(spots):
        rx, ry = x + dx * r / .42, y + dy * r / .42
        rz = z0 + .36 + rnd.uniform(.02, .12)
        rose(m, reds[i % len(reds)], rx, ry, rz, s=rnd.uniform(1.45, 1.8), turn=rnd.uniform(0, 1))
        for k in range(2):
            a = rnd.uniform(0, 2 * math.pi)
            leaf(m, leaf_green, (rx, ry, rz + .01), (math.cos(a), math.sin(a), -.25))
