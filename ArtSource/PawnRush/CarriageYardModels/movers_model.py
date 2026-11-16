# Royal Carriage Yard models (R678): zone 1's moving things, each in the frame of the pivot the code turns it on.
#
#   exec(rcy_kit.py); RCY_BUILD = False; exec(entrance_model.py)   # its shapes (lathe, arc_ring, crown, prism ...)
#   exec(open(r".../movers_model.py").read())
#
# 승규 11-15: the closed coach (닫힌 마차). Collections, each one FBX (export_rcy.py), all grey clay:
#   "RCY Coach"        YardKit.Carriage's frame (nose +z, ground y 0 = Blender nose +y, up +z), without its wheels: the
#                      undercarriage and springs, the curved body with doors and shuttered windows (no glass), the crest on
#                      each door, roof rail, the gilt crown finial, the driver's
#                      box under its hammercloth, the footman's board, lamp housings round the code's lamps (kept: they go
#                      red through a charge).
#   "RCY Coach wheel"  one wheel in a Wheel pivot's frame (it turns about x): nave, twelve spokes, felloe, tyre.
#   "RCY Hitch"        a team's RoamingCarriage frame: the pole and its tip, the swingle-tree, four traces and the pole
#                      straps (the traces and straps in the slot "RCY | leather", painted like the horses' tack).
#   "RCY Gate leaf W" / "RCY Gate leaf E"   a start gate's TimedGate frame (the hinge at the origin; W runs +x, E -x):
#                      framed boards, iron bands with studs, knuckles, the crown plate; ledges and braces behind.
# The horse itself is one skinned model now, horse_model.py (RcyHorse); its five pinned parts (RcyHorseBody, ...Neck,
# ...ForeLeg, ...HindLeg, ...Harness) are gone.

PAINT, HIDE = "RCY | coach", "RCY | hide"


# ------------------------------------------------------------------ shapes


def loft(col, name, stations, seg=16, mat=HIDE, smooth=True, subsurf=1, side=Vector((1, 0, 0))):
    """A tube through `stations` [(centre, half-width, half-height)]: elliptical sections square to the path, the width
    along `side`, each end closed on a point just past its last section."""
    pts = [Vector(c) for c, _, _ in stations]
    bm = bmesh.new()
    rings = []
    for i, (c, rw, rh) in enumerate(stations):
        c = Vector(c)
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        u = side - t * side.dot(t)
        if u.length < 1e-4:
            u = Vector((0, 1, 0)) - t * t.y
        u.normalize()
        v = t.cross(u).normalized()
        if v.z < -1e-3 or (abs(v.z) <= 1e-3 and v.y < 0):
            v = -v
        rings.append([bm.verts.new(c + u * (rw * math.cos(2 * math.pi * k / seg)) + v * (rh * math.sin(2 * math.pi * k / seg)))
                      for k in range(seg)])
    for a, b in zip(rings, rings[1:]):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    t0 = (pts[0] - pts[1]).normalized()
    t1 = (pts[-1] - pts[-2]).normalized()
    tip0 = bm.verts.new(pts[0] + t0 * min(stations[0][1], stations[0][2]) * .6)
    tip1 = bm.verts.new(pts[-1] + t1 * min(stations[-1][1], stations[-1][2]) * .6)
    for k in range(seg):
        bm.faces.new((rings[0][(k + 1) % seg], rings[0][k], tip0))
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % seg], tip1))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = smooth
    ob = mesh_object(name, bm, col, (mat,))
    if subsurf:
        m = ob.modifiers.new("Sub", "SUBSURF")
        m.levels = subsurf
    return ob


def ring_x(col, name, cx, r0, r1, x0, x1, mat, seg=36, bevel=.01):
    """A washer round the x axis (a wheel's felloe, its tyre): radii r0..r1, from cx + x0 to cx + x1."""
    ob = arc_ring(col, name, 0, 0, r0, r1, x0, x1, mat, seg=seg, bevel=bevel)
    ob.rotation_euler = (0, math.radians(90), 0)
    ob.location = (cx, 0, 0)
    return ob


def ellipse_ring(col, name, centre, axis, rw, rh, band, depth, mat, seg=28, side=Vector((1, 0, 0))):
    """A flat band round an axis, elliptical (rw, rh to the band's middle), `band` thick across, `depth` along the axis:
    a girth round a barrel, a collar round a neck, a spring."""
    c, t = Vector(centre), Vector(axis).normalized()
    u = (side - t * side.dot(t)).normalized()
    v = t.cross(u).normalized()
    if v.z < -1e-3:
        v = -v
    bm = bmesh.new()
    rings = []
    for dr, dt in ((-band / 2, -depth / 2), (band / 2, -depth / 2), (band / 2, depth / 2), (-band / 2, depth / 2)):
        rings.append([bm.verts.new(c + t * dt + u * ((rw + dr) * math.cos(2 * math.pi * k / seg)) + v * ((rh + dr) * math.sin(2 * math.pi * k / seg)))
                      for k in range(seg)])
    for i in range(4):
        a, b = rings[i], rings[(i + 1) % 4]
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    return mesh_object(name, bm, col, (mat,))


def strap(col, name, a, b, w=.05, t=.015, mat=LEATHER, up=Vector((0, 0, 1))):
    """A flat strap from a to b, `w` wide (across: square to the strap and to `up`), `t` thick."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    across = d.cross(Vector(up))
    if across.length < 1e-4:
        across = d.cross(Vector((1, 0, 0)))
    across.normalize()
    n = across.cross(d).normalized()
    bm = bmesh.new()
    vs = []
    for p in (a, b):
        for sa, sn in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs.append(bm.verts.new(p + across * (sa * w / 2) + n * (sn * t / 2)))
    for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
        bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_object(name, bm, col, (mat,))


def dome(col, name, at, axis, r, sgn=1, mat=IRON, seg=8):
    """A little dome (a nail head, an eye, a rosette) on a face: `axis` 'x' / 'y' / 'z', bulging to `sgn`."""
    prof = [(0, 0), (r, 0), (r * .9, r * .45 * sgn), (r * .6, r * .8 * sgn), (0, r * .95 * sgn)]
    return lathe(col, name, prof, at, axis=axis, seg=seg, mat=mat, smooth=True)


def shift(ob, pivot):
    """Moves a joined part into its pivot's frame."""
    ob.data.transform(Matrix.Translation(-Vector(pivot)))
    return ob


# ------------------------------------------------------------------ the closed coach


CAB_Y0, CAB_Y1, WAIST, CAB_TOP, HALF = -1.75, 1.15, 1.56, 2.48, 1.0


def tub_section(y, n=12):
    """The body's lower half (the tub) at y: a rounded U from the waist on one side to the waist on the other."""
    u = (y - (CAB_Y0 + CAB_Y1) / 2) / ((CAB_Y1 - CAB_Y0) / 2)
    bottom = .88 + .2 * u ** 4          # lowest in the middle of its length, rising to its ends
    width = HALF * (1 - .1 * u ** 6)
    pts = []
    for k in range(n + 1):
        th = -math.pi / 2 + math.pi * k / n
        s, c = math.sin(th), math.cos(th)
        x = math.copysign(abs(s) ** (2 / 3.2), s) * width
        z = WAIST - (WAIST - bottom) * abs(c) ** (2 / 2.6)
        pts.append(Vector((x * (1 - .28 * abs(c) ** 3), y, z)))
    return pts


def coach_tub(col):
    """The lower body lofted along y, its ends and its top closed."""
    ys = [CAB_Y0 + (CAB_Y1 - CAB_Y0) * (.5 - .5 * math.cos(math.pi * i / 14)) for i in range(15)]
    bm = bmesh.new()
    rows = [[bm.verts.new(p) for p in tub_section(y)] for y in ys]
    m = len(rows[0])
    for a, b in zip(rows, rows[1:]):
        for k in range(m - 1):
            bm.faces.new((a[k], a[k + 1], b[k + 1], b[k]))
    for row in (rows[0], rows[-1]):
        bm.faces.new(row)
    bm.faces.new([r[0] for r in rows] + [r[-1] for r in reversed(rows)])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = abs(f.normal.y) < .9 and f.normal.z < .99
    return mesh_object("Coach tub", bm, col, (PAINT,))


def shutter(col, s, y0, y1, z0, z1):
    """A window closed by a pair of louvred wooden shutters (승규 11-15: a closed royal coach, shutters, no glass): a
    back board set into the opening, a frame of stiles and rails, and slats tilted to shed the rain outward."""
    parts = []
    g = s * (HALF - .06)
    parts.append(box(col, "Shutter back", (min(g, g - s * .012), y0, z0), (max(g, g - s * .012), y1, z1), WALNUT))
    xo, xi = s * (HALF - .012), s * (HALF - .06)
    xa, xb = min(xo, xi), max(xo, xi)
    mid = (y0 + y1) / 2
    for ya, yb in ((y0, y0 + .045), (mid - .02, mid + .02), (y1 - .045, y1)):
        parts.append(box(col, "Shutter stile", (xa, ya, z0), (xb, yb, z1), WALNUT, bevel=.008, segments=1))
    for za, zb in ((z0, z0 + .05), (z1 - .05, z1)):
        parts.append(box(col, "Shutter rail", (xa, y0, za), (xb, y1, zb), WALNUT, bevel=.008, segments=1))
    n = 7
    tilt = Quaternion((0, 1, 0), s * math.radians(38))
    for k in range(n):
        z = z0 + .08 + (z1 - z0 - .16) * k / (n - 1)
        for ya, yb in ((y0 + .045, mid - .02), (mid + .02, y1 - .045)):
            bm = _box_bm(Vector((s * (HALF - .036), (ya + yb) / 2, z)), Vector((.055, yb - ya, .012)), tilt)
            parts.append(mesh_object("Shutter slat", bm, col, (WALNUT,)))
    parts.append(cylinder(col, "Shutter knob", (s * (HALF + .005), mid, (z0 + z1) / 2), (1, 0, 0), .018, .03, GOLD, segments=8))
    return parts


def coach(col):
    parts = []
    # --- undercarriage: perch, axle beds, the fore carriage's turntable, four elliptic springs
    parts.append(beam(col, "Perch", (0, -1.75, .66), (0, 1.75, .66), w=.14, h=.13, mat=WALNUT))
    for y in (-1.3, 1.3):
        parts.append(box(col, "Axle bed", (-.95, y - .07, .5), (.95, y + .07, .62), WALNUT, bevel=.02))
        parts.append(cylinder(col, "Axle", (0, y, .55), (1, 0, 0), .035, 2.1, IRON, segments=10))
        for s in (-1, 1):
            parts.append(ellipse_ring(col, "Spring", (s * .62, y, .74), (1, 0, 0), .34, .09, .035, .07, IRON, seg=24, side=Vector((0, 1, 0))))
            parts.append(box(col, "Spring block", (s * .62 - .06, y - .06, .62), (s * .62 + .06, y + .06, .66), IRON))
    parts.append(arc_ring(col, "Turntable", 0, 1.3, .32, .42, .62, .66, IRON, seg=28, bevel=0))
    for s in (-1, 1):
        parts.append(beam(col, "Futchell", (s * .45, .9, .66), (s * .2, 1.85, .66), w=.08, h=.08, mat=WALNUT, segments=1))
    # --- the body: the tub, the upper body's frame round its windows, louvred shutters in them, the doors, the crest
    parts.append(coach_tub(col))
    parts.append(box(col, "Waist rail", (-HALF - .025, CAB_Y0 - .02, WAIST - .03), (HALF + .025, CAB_Y1 + .02, WAIST + .07), GOLD, bevel=.02))
    parts.append(box(col, "Upper body", (-HALF + .07, CAB_Y0 + .07, WAIST), (HALF - .07, CAB_Y1 - .07, CAB_TOP), PAINT))
    frame = .07
    windows = ((-1.6, -.95), (-.66, .06), (.32, 1.0))    # rear quarter, door, front quarter (y)
    for s in (-1, 1):
        x0, x1 = (HALF - frame, HALF) if s > 0 else (-HALF, -HALF + frame)
        posts = [(CAB_Y0, windows[0][0]), (windows[0][1], windows[1][0]), (windows[1][1], windows[2][0]), (windows[2][1], CAB_Y1)]
        for a, b in posts:
            parts.append(box(col, "Pillar", (x0, a, WAIST + .07), (x1, b, CAB_TOP - .16), PAINT, bevel=.015, segments=1))
        parts.append(box(col, "Belt", (x0, CAB_Y0, WAIST + .07), (x1, CAB_Y1, 1.7), PAINT, bevel=.015, segments=1))
        parts.append(box(col, "Header", (x0, CAB_Y0, CAB_TOP - .2), (x1, CAB_Y1, CAB_TOP), PAINT, bevel=.015, segments=1))
        for a, b in windows:
            parts += shutter(col, s, a, b, 1.7, CAB_TOP - .2)
            for ya, yb, za, zb in ((a, b, 1.68, 1.72), (a, b, CAB_TOP - .22, CAB_TOP - .18), (a - .02, a + .02, 1.7, CAB_TOP - .2), (b - .02, b + .02, 1.7, CAB_TOP - .2)):
                parts.append(box(col, "Window bead", (x0 if s > 0 else x0 - .012, ya, za), (x1 + .012 if s > 0 else x1, yb, zb), GOLD))
        dx = s * (HALF + .012)
        for ya, yb in ((-.74, -.7), (.1, .14)):
            parts.append(box(col, "Door line", (min(dx, dx - s * .03), ya, 1.0), (max(dx, dx - s * .03), yb, CAB_TOP - .12), GOLD))
        parts.append(cylinder(col, "Door handle", (s * (HALF + .04), -.62, 1.62), (0, 1, 0), .025, .16, GOLD, segments=8))
        cz = 1.22
        tub_x = max(abs(p.x) for p in tub_section(-.3) if abs(p.z - cz) < .12)
        medal = lathe(col, "Crest", [(0, 0), (.2, 0), (.22, .02 * s), (.2, .04 * s), (.15, .05 * s), (0, .055 * s)],
                      (s * (tub_x - .01), -.3, cz), axis="x", seg=24, mat=GOLD, smooth=True)
        parts.append(medal)
        cc = prism(col, "Crest crown", [(-.11, -.07), (.11, -.07), (.13, .09), (.06, .02), (0, .11), (-.06, .02), (-.13, .09)],
                   0, .03, GOLD, axis="y")
        cc.rotation_euler = (0, 0, math.radians(90) * s)
        cc.location = (s * (tub_x + .075), -.3, cz)
        parts.append(cc)
    # Front and back: raised beads on the end panels.
    for y, sgn in ((CAB_Y1, 1), (CAB_Y0, -1)):
        yy = y - sgn * .07
        parts.append(box(col, "End panel", (-HALF + .07, min(yy, y), WAIST), (HALF - .07, max(yy, y), CAB_TOP), PAINT))
        for xa, xb, za, zb in ((-.7, .7, 1.75, 1.79), (-.7, .7, 2.22, 2.26), (-.72, -.68, 1.75, 2.26), (.68, .72, 1.75, 2.26)):
            parts.append(box(col, "End bead", (xa, min(y, y + sgn * .012), za), (xb, max(y, y + sgn * .012), zb), GOLD))
    for s in (-1, 1):
        for y in (CAB_Y0 + .07, CAB_Y1 - .07):
            parts.append(cylinder(col, "Corner post", (s * (HALF - .07), y, (WAIST + CAB_TOP) / 2), (0, 0, 1), .07, CAB_TOP - WAIST, PAINT, segments=12))
    # --- roof: a moulded eave, a domed top, a gilt rail round it, the crown in the middle
    parts.append(box(col, "Roof eave", (-HALF - .08, CAB_Y0 - .08, CAB_TOP), (HALF + .08, CAB_Y1 + .08, CAB_TOP + .08), WALNUT, bevel=.035))
    parts.append(loft(col, "Roof dome", [((0, CAB_Y0 + .02, CAB_TOP + .08), .3, .02), ((0, CAB_Y0 + .1, CAB_TOP + .08), HALF + .02, .08),
                                          ((0, -.3, CAB_TOP + .08), HALF + .02, .14), ((0, CAB_Y1 - .1, CAB_TOP + .08), HALF + .02, .08),
                                          ((0, CAB_Y1 - .02, CAB_TOP + .08), .3, .02)], seg=24, mat=WALNUT, subsurf=1))
    for s in (-1, 1):
        parts.append(box(col, "Rail", (s * (HALF - .02) - .02, CAB_Y0 + .05, 2.68), (s * (HALF - .02) + .02, CAB_Y1 - .05, 2.71), GOLD))
        for k in range(9):
            y = CAB_Y0 + .05 + (CAB_Y1 - CAB_Y0 - .1) * k / 8
            parts.append(lathe(col, "Rail post", [(0, 2.56), (.025, 2.56), (.018, 2.62), (.025, 2.68), (0, 2.7)], (s * (HALF - .02), y, 0), seg=6, mat=GOLD))
    for y in (CAB_Y0 + .05, CAB_Y1 - .05):
        parts.append(box(col, "Rail", (-HALF, y - .02, 2.68), (HALF, y + .02, 2.71), GOLD))
    parts.append(lathe(col, "Crown seat", [(0, 2.6), (.3, 2.6), (.3, 2.66), (.24, 2.7), (0, 2.7)], (0, -.3, 0), seg=20, mat=GOLD))
    parts += crown(col, (0, -.3, 2.7), .42)
    # --- the driver's box under its hammercloth, the cushion, the footboard and its irons
    parts.append(box(col, "Box seat", (-.62, 1.2, .98), (.62, 1.78, 1.3), WALNUT, bevel=.02))
    hem = [(.75, 1.33)]
    for k in range(25):
        t = 1 - k / 24
        hem.append((-.75 + 1.5 * t, .95 + .07 * abs(math.sin(math.pi * 6 * t))))
    parts.append(prism(col, "Hammercloth front", hem + [(-.75, 1.33)], 1.84, 1.87, CLOTH))
    for s in (-1, 1):
        side_hem = [(1.17, 1.33), (1.87, 1.33)]
        for k in range(15):
            t = 1 - k / 14
            side_hem.append((1.17 + .7 * t, .95 + .07 * abs(math.sin(math.pi * 4 * t))))
        parts.append(prism(col, "Hammercloth side", side_hem, s * .75 - .015, s * .75 + .015, CLOTH, axis="x"))
    parts.append(box(col, "Hammercloth top", (-.77, 1.15, 1.3), (.77, 1.88, 1.34), CLOTH, bevel=.02))
    parts.append(box(col, "Hammercloth fringe", (-.77, 1.86, 1.27), (.77, 1.89, 1.34), GOLD))
    cushion = box(col, "Cushion", (-.6, 1.25, 1.34), (.6, 1.78, 1.44), LEATHER)
    bevel_mod(cushion, .045, 3)
    parts.append(cushion)
    parts.append(box(col, "Backrest", (-.6, 1.16, 1.44), (.6, 1.24, 1.72), LEATHER, bevel=.03))
    parts.append(beam(col, "Footboard", (0, 1.76, .99), (0, 2.08, .9), w=1.45, h=.05, mat=WALNUT))
    for s in (-1, 1):
        parts.append(beam(col, "Box iron", (s * .6, 1.6, .66), (s * .62, 2.04, .9), w=.04, h=.04, mat=IRON, segments=1))
        parts.append(beam(col, "Seat rail", (s * .66, 1.24, 1.45), (s * .66, 1.76, 1.45), w=.03, h=.03, mat=GOLD, segments=1))
    # --- the footman's board behind, its two hand straps
    parts.append(box(col, "Footman's board", (-.55, -2.08, .84), (.55, -1.8, .9), WALNUT, bevel=.015))
    for s in (-1, 1):
        parts.append(beam(col, "Board iron", (s * .45, -1.75, .66), (s * .48, -2.05, .84), w=.04, h=.04, mat=IRON, segments=1))
        parts.append(strap(col, "Hand strap", (s * .5, -1.78, 2.3), (s * .5, -1.78, 1.75), w=.06, t=.02, up=Vector((0, 1, 0))))
    # --- the steps under the doors
    for s in (-1, 1):
        parts.append(beam(col, "Step iron", (s * .82, -.3, .9), (s * .98, -.3, .58), w=.03, h=.03, mat=IRON, segments=1))
        parts.append(box(col, "Step", (s * .98 - .09, -.48, .55), (s * .98 + .07, -.12, .58), IRON))
    # --- lamp housings round the code's lamps (x 1.08, y 1.2, z 2.02; the code scales them to 0.32 x 0.54)
    for s in (-1, 1):
        cx, cy = s * 1.08, 1.2
        parts.append(lathe(col, "Lamp cap", [(0, 2.29), (.2, 2.29), (.2, 2.31), (.08, 2.42), (.04, 2.42), (.04, 2.5), (.06, 2.5), (0, 2.53)],
                           (cx, cy, 0), seg=8, mat=GOLD))
        parts.append(lathe(col, "Lamp cup", [(0, 1.62), (.03, 1.62), (.06, 1.66), (.19, 1.73), (.19, 1.75), (0, 1.75)], (cx, cy, 0), seg=8, mat=GOLD))
        parts.append(beam(col, "Lamp bracket", (s * .98, 1.08, 1.75), (cx, cy, 1.66), w=.03, h=.03, mat=IRON, segments=1))
    return parts


def coach_wheel(col):
    """One wheel round the x axis, 1.1 m across, 0.14 m wide, in its pivot's frame."""
    parts = [ring_x(col, "Tyre", 0, .515, .55, -.07, .07, IRON, seg=32, bevel=.006)]
    parts.append(ring_x(col, "Felloe", 0, .44, .52, -.05, .05, WALNUT, seg=32, bevel=.01))
    parts.append(lathe(col, "Nave", [(0, -.14), (.06, -.14), (.08, -.12), (.1, -.11), (.12, -.07), (.13, -.03), (.13, .03), (.12, .07),
                                     (.1, .11), (.08, .12), (.06, .14), (0, .14)], (0, 0, 0), axis="x", seg=12, mat=WALNUT, smooth=True))
    for x in (-.11, .11):
        parts.append(ring_x(col, "Nave band", x, .085, .105, -.012, .012, GOLD, seg=16, bevel=0))
    for sgn in (-1, 1):
        parts.append(lathe(col, "Hub cap", [(0, .14 * sgn), (.055, .14 * sgn), (.05, .17 * sgn), (.03, .19 * sgn), (0, .195 * sgn)],
                           (0, 0, 0), axis="x", seg=12, mat=GOLD, smooth=True))
    for k in range(12):
        sp = lathe(col, "Spoke", [(0, .1), (.028, .1), (.026, .2), (.021, .35), (.018, .46), (0, .46)], (0, 0, 0), axis="z", seg=6, mat=OAK, smooth=True)
        sp.rotation_euler = (2 * math.pi * k / 12, 0, 0)
        parts.append(sp)
    return parts


# ------------------------------------------------------------------ the team's hitch and the start gates' leaves


def hitch(col):
    """In the team's frame: the carriage's nose at y .725, the horses' roots at x +-.55, y 2.175 (RcyHorse's middle)."""
    parts = [lathe(col, "Pole", [(0, .7), (.07, .7), (.065, 1.0), (.055, 2.5), (.045, 3.2), (0, 3.2)], (0, 0, .95), axis="y", seg=10, mat=WALNUT, smooth=True)]
    parts.append(lathe(col, "Pole tip", [(0, 3.15), (.06, 3.15), (.08, 3.22), (.06, 3.3), (.03, 3.34), (0, 3.35)], (0, 0, .95), axis="y", seg=12, mat=GOLD, smooth=True))
    parts.append(lathe(col, "Swingle tree", [(0, -.88), (.04, -.88), (.06, -.6), (.07, 0), (.06, .6), (.04, .88), (0, .88)], (0, .84, .95), axis="x", seg=10, mat=WALNUT, smooth=True))
    for x in (-.86, .86):
        parts.append(lathe(col, "Swingle ferrule", [(0, -.03), (.05, -.03), (.05, .03), (0, .03)], (x, .84, .95), axis="x", seg=10, mat=IRON))
    # Traces from the swingle-tree's hooks forward to where the horse's own traces (RcyHorse's harness, horse_model.py:
    # side straps at its height 1.06 back to y -.52 of its frame, here 2.175 - .52) end, overlapping them by 10 cm so
    # the horse's bob opens no gap, .385 out from its middle (its skin there is .35-.365, its strap .37-.39). Pole
    # straps from the pole's tip to the breast collar's inner front (the chest at 105 degrees, y .84 of its frame).
    for hx in (-.55, .55):
        for s in (-1, 1):
            a = Vector((max(-.86, min(.86, hx + s * .36)), .86, .96))
            b = Vector((hx + s * .385, 2.175 - .42, 1.06))
            parts.append(strap(col, "Trace", a, b, w=.055, t=.018, up=Vector((1, 0, 0))))
        parts.append(strap(col, "Pole strap", (math.copysign(.04, hx), 3.12, .97), (hx - math.copysign(.15, hx), 2.175 + .86, 1.1), w=.05, t=.018))
    return parts


CROWN_OUTLINE = [(-.3, -.22), (.3, -.22), (.34, .2), (.18, .02), (.09, .26), (0, .06), (-.09, .26), (-.18, .02), (-.34, .2)]
FLEUR = [(0, -.14), (.12, -.08), (.2, 0), (.12, .08), (0, .14), (.06, 0)]


def gate_leaf(col, side):
    """A leaf in its hinge's frame, running `side` (+1 the W gate, -1 the E gate) along x: 3 x 5.4 m, 0.35 thick (y);
    the court's face is -y."""
    W, H, T = 3.0, 5.4, .35
    parts = []

    def X(a, b):
        return min(side * a, side * b), max(side * a, side * b)

    for a, b in ((0, .26), (W - .26, W)):
        x0, x1 = X(a, b)
        parts.append(box(col, "Stile", (x0, -T / 2, 0), (x1, T / 2, H), WALNUT, bevel=.02))
    for z0, z1 in ((0, .3), (2.58, 2.86), (H - .3, H)):
        x0, x1 = X(.26, W - .26)
        parts.append(box(col, "Rail", (x0, -T / 2, z0), (x1, T / 2, z1), WALNUT, bevel=.02))
    n = 10
    for k in range(n):
        a = .26 + (W - .52) * k / n
        b = .26 + (W - .52) * (k + 1) / n
        x0, x1 = X(a + .008, b - .008)
        for z0, z1 in ((.3, 2.58), (2.86, H - .3)):
            parts.append(box(col, "Board", (x0, -T / 2 + .04, z0), (x1, T / 2 - .04, z1), OAK, bevel=.012, segments=1))
    # The court's face: three iron bands (the code's, at 0.97 / 2.70 / 4.43 m) with studs and a fleur at the free end,
    # the knuckles round the wall's pintles.
    for zc in (.97, 2.70, 4.43):
        x0, x1 = X(.05, W - .05)
        parts.append(box(col, "Iron band", (x0, -T / 2 - .06, zc - .08), (x1, -T / 2, zc + .08), IRON, bevel=.01, segments=1))
        for k in range(10):
            parts.append(dome(col, "Stud", (side * (.2 + (W - .4) * k / 9), -T / 2 - .06, zc), "y", .03, -1, IRON))
        parts.append(prism(col, "Band fleur", [(side * (W - .26 + a), zc + b) for a, b in FLEUR], -T / 2 - .06, -T / 2, IRON))
        parts.append(cylinder(col, "Knuckle", (side * .02, 0, zc), (0, 0, 1), .06, .2, IRON, segments=10))
    # The crown plate (the code's, 0.9 m at x 1.65, z 3.35): a round gold boss with a crown in relief.
    cx = side * 1.65
    parts.append(lathe(col, "Crown plate", [(0, 0), (.46, 0), (.48, -.02), (.46, -.04), (.4, -.05), (0, -.05)], (cx, -T / 2, 3.35),
                       axis="y", seg=32, mat=GOLD, smooth=True))
    parts.append(prism(col, "Plate crown", [(cx + a, 3.35 + b) for a, b in CROWN_OUTLINE], -T / 2 - .09, -T / 2 - .04, GOLD, bevel=.008))
    # The yard's face: ledges behind the bands and a brace across each half.
    x0, x1 = X(.26, W - .26)
    for za, zb in ((.3, 2.58), (2.86, H - .3)):
        parts.append(beam(col, "Brace", (side * .3, T / 2 + .03, za + .15), (side * (W - .3), T / 2 + .03, zb - .15), w=.18, h=.06, mat=WALNUT))
    for zc in (.97, 2.70, 4.43):
        parts.append(box(col, "Ledge", (x0, T / 2, zc - .12), (x1, T / 2 + .06, zc + .12), WALNUT, bevel=.015))
    return parts


# ------------------------------------------------------------------ build


def build_movers():
    made = {}

    def make(name, builder, pivot=None):
        col = collection(name)
        clear(col)
        ob = join(col, name.replace("RCY ", ""), builder(col))
        if pivot is not None:
            shift(ob, pivot)
        made[name] = tris(col)
        return ob

    make("RCY Coach", coach)
    make("RCY Coach wheel", coach_wheel)
    make("RCY Hitch", hitch)
    make("RCY Gate leaf W", lambda c: gate_leaf(c, 1))
    make("RCY Gate leaf E", lambda c: gate_leaf(c, -1))
    return made


if globals().get("RCY_BUILD_MOVERS", True):
    print(build_movers())
