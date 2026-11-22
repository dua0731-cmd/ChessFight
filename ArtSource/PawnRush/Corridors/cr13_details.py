# Detail pass (2026-11-22, 승규 "꽃 이라던가 디테일 작업 들어가") for the CR01..CR03 corridors: layered roses, buds,
# small filler flowers, folded leaves, trailing ivy, rose garlands over the niches, water lilies in the basins,
# window boxes, rope coils, wax candles. Every function adds to a cr13_common.Mesh.
import math
import random
from mathutils import Vector
from cr13_ornaments import leaf


def rose2(m, red, x, y, z, s=1.0, turn=0.):
    """A fuller rose: five open outer petals, four half-open inner ones, a curled centre."""
    outer = lambda a, hh: 1 + .28 * math.cos(5 * (a - turn))
    m.lathe([(.001, 0), (.035 * s, .004 * s), (.075 * s, .02 * s), (.095 * s, .045 * s), (.098 * s, .06 * s), (.085 * s, .07 * s)],
            (x, y, z), red, segs=12, radial=outer, cap_bottom=False, cap_top=False)
    inner = lambda a, hh: 1 + .18 * math.cos(4 * (a - turn) + .6)
    m.lathe([(.02 * s, .02 * s), (.055 * s, .045 * s), (.062 * s, .075 * s), (.05 * s, .09 * s)],
            (x, y, z), red, segs=10, radial=inner, cap_bottom=False, cap_top=False)
    m.lathe([(.001, .03 * s), (.035 * s, .055 * s), (.034 * s, .09 * s), (.018 * s, .105 * s), (.001, .1 * s)],
            (x, y, z), red, segs=8, radial=lambda a, hh: 1 + .2 * math.cos(2 * a + turn), cap_bottom=False)


def bud(m, red, green, x, y, z, s=1.0, tilt=(0, 0)):
    """A closed rosebud on green sepals and a short stem."""
    tx, ty = tilt
    m.lathe([(.001, 0), (.025 * s, .015 * s), (.032 * s, .045 * s), (.022 * s, .075 * s), (.001, .09 * s)],
            (x + tx * .05, y + ty * .05, z + .02 * s), red, segs=8)
    for k in range(3):
        a = 2 * math.pi * k / 3
        leaf(m, green, (x, y, z + .02 * s), (math.cos(a) * .6, math.sin(a) * .6, .8), length=.05 * s, width=.015 * s)
    m.tube([(x, y, z - .08), (x + tx * .02, y + ty * .02, z + .02 * s)], .007, green, segs=4, cap=False)


def daisy(m, petal, heart, x, y, z, s=1.0, normal=(0, 0, 1), n=8):
    """A small filler flower: n flat petals round a gold heart, facing `normal`."""
    nv = Vector(normal).normalized()
    side = nv.cross(Vector((0, 0, 1)) if abs(nv.z) < .9 else Vector((1, 0, 0))).normalized()
    up = nv.cross(side).normalized()
    c = Vector((x, y, z))
    for k in range(n):
        a = 2 * math.pi * k / n
        d = side * math.cos(a) + up * math.sin(a)
        leaf(m, petal, tuple(c + d * .008 * s), tuple(d + nv * .15), length=.05 * s, width=.016 * s)
    m.blob(tuple(c + nv * .006 * s), .014 * s, heart, subdiv=1)


def leaf2(m, green, at, direction, length=.12, width=.055, fold=.25):
    """A leaf folded along its midrib (two faces each side), pointed, so it catches the light."""
    a = Vector(at)
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < .1:
        side = d.cross(Vector((1, 0, 0)))
    side.normalize()
    up = side.cross(d).normalized()
    tip = a + d * length
    mid = a + d * length * .45
    rib = mid + up * width * fold
    l, r = mid + side * width, mid - side * width
    bm = m.bm
    va, vt, vr, vl, vrr = [bm.verts.new(p) for p in (a, tip, rib, l, r)]
    faces = [bm.faces.new([va, vl, vr]), bm.faces.new([vl, vt, vr]), bm.faces.new([va, vr, vrr]), bm.faces.new([vr, vt, vrr])]
    idx = m.mi(green)
    for f in faces:
        f.material_index = idx
        f.smooth = True


def ivy(m, green, stem, path, seed=1, every=.07, size=1.0, facing=(1, 0, 0)):
    """Trailing ivy along a path: a thin stem and broad leaves alternating left and right, lying flat toward `facing`
    (ivy hugs its wall, so the leaves show their faces, not their edges)."""
    nf = Vector(facing).normalized()
    rnd = random.Random(seed)
    m.tube(path, .006 * size, stem, segs=4, cap=False)
    pts = [Vector(p) for p in path]
    k = 0
    for i in range(len(pts) - 1):
        seg = pts[i + 1] - pts[i]
        L = seg.length
        if L < 1e-4:
            continue
        d = seg.normalized()
        side = d.cross(Vector((0, 0, 1)) if abs(d.z) < .9 else Vector((1, 0, 0))).normalized()
        t = 0.0
        while t < L:
            p = pts[i] + seg * (t / L)
            s = 1 if k % 2 else -1
            out = side * s + d * .3 + Vector((0, 0, rnd.uniform(-.3, .3)))
            out = (out - nf * out.dot(nf)).normalized()
            flat_leaf(m, green, p + nf * .01, out, nf, length=rnd.uniform(.07, .1) * size, width=rnd.uniform(.04, .055) * size)
            t += every * rnd.uniform(.8, 1.2)
            k += 1


def garland(m, green, reds, petal, heart, p0, p1, sag, out=(1, 0, 0), seed=2, roses=3):
    """A rose garland swagged between two points: a leafy rope, roses and small flowers along it, facing `out`."""
    rnd = random.Random(seed)
    a, b = Vector(p0), Vector(p1)
    o = Vector(out).normalized()
    path = []
    n = 14
    for i in range(n + 1):
        t = i / n
        p = a.lerp(b, t)
        p.z -= sag * 4 * t * (1 - t)
        p += o * .06 * 4 * t * (1 - t)
        path.append(p)
    m.tube([tuple(p) for p in path], .035, green, segs=6, radii=[.028 + .022 * math.sin(math.pi * i / n) for i in range(n + 1)])
    for i in range(1, n):
        p = path[i]
        for k in range(3):
            d = o * rnd.uniform(.2, 1.0) + Vector((0, rnd.uniform(-1, 1), rnd.uniform(-.8, .6)))
            leaf2(m, green, tuple(p), tuple(d), length=rnd.uniform(.07, .1), width=.04)
    for j in range(roses):
        t = (j + 1) / (roses + 1)
        p = path[int(round(t * n))] + o * .04
        rose2(m, reds[j % len(reds)], p.x, p.y, p.z - .03, s=1.1, turn=rnd.uniform(0, 1))
    for j in range(roses * 2):
        p = path[1 + rnd.randrange(n - 1)] + o * .05
        daisy(m, petal, heart, p.x, p.y + rnd.uniform(-.05, .05), p.z + rnd.uniform(-.04, .04), s=.8,
              normal=tuple(o + Vector((0, rnd.uniform(-.3, .3), .2))))


def lily(m, pad, flower, x, y, z, r=.14, bloom=True):
    """A water-lily pad (a notched disc) floating at z, and a pink bloom on it."""
    pts = []
    for i in range(15):
        a = .35 + (2 * math.pi - .7) * i / 14
        pts.append((x + r * math.cos(a), y + r * math.sin(a)))
    pts.append((x, y))
    m.prism(pts, "z", z, z + .012, pad, bevel=0)
    if bloom:
        m.lathe([(.001, 0), (.03, .01), (.055, .035), (.05, .05)], (x + .02, y, z + .01), flower, segs=12,
                radial=lambda a, hh: 1 + .3 * math.cos(6 * a), cap_bottom=False, cap_top=False)
        m.blob((x + .02, y, z + .03), .018, "GC_flower_heart", subdiv=1)


def planter2(m, stone, soil, box_green, box_light, reds, leaf_green, petal, heart, stem, x, y, z0, r=.42, seed=3):
    """The niche planter, fuller: the fluted bowl, a smoother boxwood ball, roses and buds over the rim, small white
    flowers between them and ivy spilling down the bowl's front (toward +x)."""
    rnd = random.Random(seed)
    k = r / .42
    flute = lambda a, hh: 1 - .045 * abs(math.cos(a * 8)) if hh > z0 + .14 else 1.0
    prof = [(.16, 0), (.18, .03), (.14, .06), (.12, .09), (.2, .13), (.32, .18), (.4, .25), (.43, .32), (.44, .34),
            (.42, .37), (.38, .37)]
    m.lathe([(rr * k, z0 + hh) for rr, hh in prof], (x, y, 0), stone, segs=20, radial=flute, cap_top=False)
    m.lathe([(.38 * k, z0 + .34)], (x, y, 0), soil, segs=20, cap_bottom=False, cap_top=True)
    m.blob((x - .12, y, z0 + .68), .31, box_green, subdiv=2, noise=.035, seed=seed)
    for i in range(6):
        a = rnd.uniform(0, 2 * math.pi)
        m.blob((x - .12 + .27 * math.cos(a), y + .27 * math.sin(a), z0 + .62 + rnd.uniform(-.12, .22)), .07, box_light,
               subdiv=1, noise=.12, seed=seed + i)
    spots = [(.14, -.3), (.27, -.14), (.29, .04), (.24, .2), (.12, .32), (.08, -.1), (.06, .1), (-.06, -.3),
             (-.06, .3), (.18, -.22), (.2, .29)]
    for i, (dx, dy) in enumerate(spots):
        rx, ry = x + dx * k, y + dy * k
        rz = z0 + .37 + rnd.uniform(.0, .12)
        if i in (9, 10):
            bud(m, reds[i % len(reds)], leaf_green, rx, ry, rz + .05, s=1.3, tilt=(rnd.uniform(-1, 1), rnd.uniform(-1, 1)))
        else:
            rose2(m, reds[i % len(reds)], rx, ry, rz, s=rnd.uniform(1.25, 1.55), turn=rnd.uniform(0, 1))
        for j in range(3):
            a = rnd.uniform(0, 2 * math.pi)
            leaf2(m, leaf_green, (rx, ry, rz + .01), (math.cos(a), math.sin(a), -.2), length=.11, width=.05)
    for i in range(7):
        a = rnd.uniform(-1.3, 1.3)
        daisy(m, petal, heart, x + .3 * math.cos(a) * k, y + .3 * math.sin(a) * k, z0 + .44 + rnd.uniform(0, .1),
              s=.9, normal=(math.cos(a), math.sin(a), .8))
    for j, dy in enumerate((-.22, .05, .25)):
        p0 = (x + .36 * k, y + dy, z0 + .36)
        path = [p0, (p0[0] + .08, p0[1] + .02, p0[2] - .08), (p0[0] + .1, p0[1] + .05, p0[2] - .2),
                (p0[0] + .1, p0[1] + .02 * (j - 1), p0[2] - .34 - .06 * j)]
        ivy(m, leaf_green, stem, path, seed=seed * 10 + j, every=.045, size=1.25)


def window_box(m, wood, iron, soil, green, flowers, x_face, y, z, w=.9, seed=4):
    """A timber window box on two iron brackets under a sill (toward +x), overflowing with flowers and trailing leaves."""
    rnd = random.Random(seed)
    m.box(x_face, x_face + .22, y - w / 2, y + w / 2, z, z + .2, wood, bevel=.015)
    for s in (-1, 1):
        m.box(x_face + .005, x_face + .225, y + s * (w / 2 - .12) - .02, y + s * (w / 2 - .12) + .02, z - .01, z + .21, iron, bevel=.004)
        m.prism([(x_face, z - .22), (x_face, z - .17), (x_face + .17, z), (x_face + .12, z)], "y",
                y + s * (w / 2 - .12) - .015, y + s * (w / 2 - .12) + .015, iron, bevel=0)
    m.box(x_face + .02, x_face + .2, y - w / 2 + .02, y + w / 2 - .02, z + .15, z + .18, soil, bevel=0)
    for i in range(9):
        fy = y - w / 2 + .08 + (w - .16) * i / 8
        fz = z + .2 + rnd.uniform(.02, .08)
        fx = x_face + rnd.uniform(.06, .16)
        for j in range(3):
            a = rnd.uniform(0, 2 * math.pi)
            leaf2(m, green, (fx, fy, fz - .03), (math.cos(a), math.sin(a), .3), length=.08, width=.035)
        daisy(m, flowers[i % len(flowers)], "GC_flower_heart", fx, fy, fz + .02, s=1.0, normal=(.5, 0, 1), n=6)
    for j in range(3):
        fy = y - w / 3 + w / 3 * j
        p0 = (x_face + .22, fy, z + .17)
        ivy(m, green, green, [p0, (p0[0] + .04, fy, z + .05), (p0[0] + .05, fy + .02, z - .15)], seed=seed + j, every=.05, size=.8)


def rope_coil(m, rope, x, y, z, r=.16, turns=4):
    """A coiled rope lying on a ledge."""
    for t in range(turns):
        rr = r - t * .025
        path = [(x + rr * math.cos(2 * math.pi * i / 16), y + rr * math.sin(2 * math.pi * i / 16), z + .02 + t * .012) for i in range(17)]
        m.tube(path, .02, rope, segs=6, cap=False)


def candle(m, wax, x, y, z, h=.16, r=.022):
    """An unlit wax candle standing in a cup (code puts the flame on the FLAME_ marker at its wick)."""
    m.lathe([(r, 0), (r, h), (r * .7, h + .01), (.002, h + .012)], (x, y, z), wax, segs=8, cap_bottom=True, cap_top=False)
    return (x, y, z + h + .03)


def flat_leaf(m, green, at, d, n, length=.09, width=.05):
    """A broad leaf lying in the plane across `n` (its face toward n), pointing along d, with a raised midrib."""
    a = Vector(at)
    d = Vector(d).normalized()
    n = Vector(n).normalized()
    side = n.cross(d).normalized()
    pts = [a, a + d * length * .35 + side * width, a + d * length * .7 + side * width * .6, a + d * length,
           a + d * length * .7 - side * width * .6, a + d * length * .35 - side * width]
    rib = a + d * length * .5 + n * width * .25
    bm = m.bm
    vs = [bm.verts.new(p) for p in pts]
    vr = bm.verts.new(rib)
    idx = m.mi(green)
    for i in range(len(vs)):
        f = bm.faces.new([vs[i], vs[(i + 1) % len(vs)], vr])
        f.material_index = idx
        f.smooth = True
