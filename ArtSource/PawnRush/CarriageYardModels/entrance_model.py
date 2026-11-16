# Royal Carriage Yard models (R678): zone 1, the outer gate and the yard (승규 11-15: 닫힌 마차, the closed coach).
#
#   exec(open(r".../rcy_kit.py").read()); exec(open(r".../entrance_model.py").read())
#
# Builds, all grey clay over the code's blockout (whose colliders stay):
#   "RCY Entrance"  MAP frame: the start court's paving and walls, the yard's paving and side walls, both storage
#                   sheds (posts, braces, tiled awnings, valances, the royal canopy, lanterns round the code's lamps) and
#                   every load in them, the start court's crates. The gate wall, lintel and gate towers are the castle
#                   kit's outer gatehouse (castle_model.py) since R678.
# The coach, the horses, the hitch and the gate leaves are movers_model.py's.
#
# Left to the code: the shed lamps' glow (the lanterns are built round them), the countdown and the signs.

GOLD, TILE, CLOTH, STRAW, SACK, LEATHER = "RCY | gold", "RCY | tile", "RCY | cloth", "RCY | straw", "RCY | sack", "RCY | leather"


# ------------------------------------------------------------------ shapes (shared with movers_model.py)


def lathe(col, name, prof, centre=(0, 0, 0), axis="z", seg=16, mat=STONE, smooth=False, sharp=False):
    """A solid of revolution: prof is [(radius, height)] from one end to the other (start and end at radius 0)."""
    bm = bmesh.new()
    rings = []
    for r, h in prof:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, h))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * k / seg), r * math.sin(2 * math.pi * k / seg), h))
                          for k in range(seg)])
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        for k in range(seg):
            if len(a) == 1:
                bm.faces.new((a[0], b[(k + 1) % seg], b[k]))
            elif len(b) == 1:
                bm.faces.new((a[k], a[(k + 1) % seg], b[0]))
            else:
                bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    rot = {"z": Matrix.Identity(4), "x": Matrix.Rotation(math.radians(90), 4, "Y"),
           "y": Matrix.Rotation(math.radians(-90), 4, "X")}[axis]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centre)) @ rot, verts=bm.verts)
    for f in bm.faces:
        f.smooth = smooth
    if sharp:
        sharp_creases(bm)   # R680 QA: keep a profile's hard steps (a drum's grooves) sharp in smooth shading
    return mesh_object(name, bm, col, (mat,))


def prism(col, name, outline, y0, y1, mat=STONE, bevel=0.0, axis="y"):
    """A flat outline [(a, b)] extruded: axis 'y' -> outline in x-z, extruded y0..y1; 'x' -> outline in y-z, along x."""
    bm = bmesh.new()
    if axis == "y":
        lo = [bm.verts.new((a, y0, b)) for a, b in outline]
        hi = [bm.verts.new((a, y1, b)) for a, b in outline]
    else:
        lo = [bm.verts.new((y0, a, b)) for a, b in outline]
        hi = [bm.verts.new((y1, a, b)) for a, b in outline]
    n = len(outline)
    bm.faces.new(lo)
    bm.faces.new(list(reversed(hi)))
    for k in range(n):
        bm.faces.new((lo[k], hi[k], hi[(k + 1) % n], lo[(k + 1) % n]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = mesh_object(name, bm, col, (mat,))
    if bevel > 0:
        bevel_mod(ob, bevel, 1)
    return ob


def arc_block(col, name, cx, cy, r0, r1, a0, a1, z0, z1, mat=STONE, bevel=.03):
    """A curved block of a round wall: radii r0..r1, angles a0..a1 (radians), heights z0..z1."""
    steps = max(2, int(abs(a1 - a0) / math.radians(15)) + 1)
    bm = bmesh.new()
    rows = []
    for z in (z0, z1):
        row = []
        for i in range(steps + 1):
            a = a0 + (a1 - a0) * i / steps
            row.append(bm.verts.new((cx + r1 * math.cos(a), cy + r1 * math.sin(a), z)))
        for i in range(steps, -1, -1):
            a = a0 + (a1 - a0) * i / steps
            row.append(bm.verts.new((cx + r0 * math.cos(a), cy + r0 * math.sin(a), z)))
        rows.append(row)
    n = len(rows[0])
    bm.faces.new(rows[0])
    bm.faces.new(list(reversed(rows[1])))
    for k in range(n):
        bm.faces.new((rows[0][k], rows[0][(k + 1) % n], rows[1][(k + 1) % n], rows[1][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = mesh_object(name, bm, col, (mat,))
    if bevel > 0:
        bevel_mod(ob, bevel, 1)
    return ob


def round_wall(col, name, cx, cy, r, z0, z1, course=.5, blocks=9, depth=.35, mat=STONE, phase=0.0):
    parts, z, row = [], z0, 0
    while z < z1 - 1e-3:
        h = min(course, z1 - z)
        off = phase + (math.pi / blocks if row % 2 else 0)
        for k in range(blocks):
            a0 = off + 2 * math.pi * k / blocks
            parts.append(arc_block(col, name, cx, cy, r - depth, r, a0, a0 + 2 * math.pi / blocks, z, z + h, mat))
        z += h
        row += 1
    return parts


def arc_ring(col, name, cx, cy, r0, r1, z0, z1, mat, seg=40, bevel=.02):
    """A flat ring (a washer) round a vertical axis at (cx, cy): radii r0..r1, heights z0..z1."""
    bm = bmesh.new()
    rings = []
    for r, z in ((r0, z0), (r1, z0), (r1, z1), (r0, z1)):
        rings.append([bm.verts.new((cx + r * math.cos(2 * math.pi * k / seg), cy + r * math.sin(2 * math.pi * k / seg), z))
                      for k in range(seg)])
    for i in range(4):
        a, b = rings[i], rings[(i + 1) % 4]
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True   # R680 QA: round about the axis, its 90-degree rims kept sharp
    sharp_creases(bm)
    ob = mesh_object(name, bm, col, (mat,))
    if bevel > 0:
        bevel_mod(ob, bevel, 1)
    return ob


def crenels(col, name, x0, x1, y0, y1, z, height=.85, merlon=1.0, gap=.7, mat=STONE):
    """Merlons along the longer of x / y from z up."""
    parts = []
    along = "x" if x1 - x0 >= y1 - y0 else "y"
    a0, a1 = (x0, x1) if along == "x" else (y0, y1)
    n = max(1, int((a1 - a0 + gap) / (merlon + gap)))
    pad = (a1 - a0 - (n * merlon + (n - 1) * gap)) / 2
    for k in range(n):
        b0 = a0 + pad + k * (merlon + gap)
        lo = (b0, y0, z) if along == "x" else (x0, b0, z)
        hi = (b0 + merlon, y1, z + height) if along == "x" else (x1, b0 + merlon, z + height)
        parts.append(box(col, name, lo, hi, mat, bevel=.04, segments=1))
    return parts


def crown(col, base, size, mat=GOLD):
    """A royal crown standing on `base` (Blender), `size` across."""
    x, y, z = base
    r = size * .5
    parts = [arc_ring(col, "Crown band", x, y, r * .8, r, z, z + size * .22, mat, seg=32, bevel=.01)]
    for k in range(8):
        a = 2 * math.pi * k / 8
        tall = size * (.55 if k % 2 == 0 else .38)
        cx, cy = x + r * .9 * math.cos(a), y + r * .9 * math.sin(a)
        parts.append(lathe(col, "Crown point", [(0, 0), (size * .07, 0), (size * .05, tall * .7), (size * .06, tall * .78),
                                                (size * .03, tall), (0, tall + size * .02)], (cx, cy, z + size * .2), seg=8, mat=mat, smooth=True))
    parts.append(lathe(col, "Crown cap", [(0, 0), (r * .8, 0), (r * .7, size * .18), (r * .4, size * .32), (0, size * .36)],
                       (x, y, z + size * .2), seg=24, mat=mat, smooth=True))
    parts.append(lathe(col, "Crown orb", [(0, 0), (size * .05, 0), (size * .1, size * .08), (size * .1, size * .14),
                                          (size * .05, size * .2), (0, size * .21)], (x, y, z + size * .55), seg=12, mat=mat, smooth=True))
    parts.append(box(col, "Crown cross", (x - size * .02, y - size * .02, z + size * .74), (x + size * .02, y + size * .02, z + size * .95), mat))
    parts.append(box(col, "Crown cross", (x - size * .08, y - size * .02, z + size * .83), (x + size * .08, y + size * .02, z + size * .87), mat))
    return parts


def crate(col, foot, size, dark=False):
    """A slatted crate standing on `foot` (Blender, the middle of its base): a board core, edge battens and rails, a
    diagonal brace on each face."""
    x, y, z = foot
    sx, sy, sz = size
    mat = WALNUT if dark else OAK
    parts = [box(col, "Crate", (x - sx / 2 + .03, y - sy / 2 + .03, z), (x + sx / 2 - .03, y + sy / 2 - .03, z + sz - .02), mat, bevel=.01, segments=1)]
    t = .09
    for ex in (-1, 1):
        for ey in (-1, 1):
            parts.append(box(col, "Crate batten", (x + ex * sx / 2 - (t if ex > 0 else 0), y + ey * sy / 2 - (t if ey > 0 else 0), z),
                             (x + ex * sx / 2 + (0 if ex > 0 else t), y + ey * sy / 2 + (0 if ey > 0 else t), z + sz), WALNUT, bevel=.015, segments=1))
    for zz in (z, z + sz - t):
        for ey in (-1, 1):
            parts.append(box(col, "Crate rail", (x - sx / 2, y + ey * sy / 2 - (t if ey > 0 else 0), zz),
                             (x + sx / 2, y + ey * sy / 2 + (0 if ey > 0 else t), zz + t), WALNUT, bevel=.015, segments=1))
        for ex in (-1, 1):
            parts.append(box(col, "Crate rail", (x + ex * sx / 2 - (t if ex > 0 else 0), y - sy / 2, zz),
                             (x + ex * sx / 2 + (0 if ex > 0 else t), y + sy / 2, zz + t), WALNUT, bevel=.015, segments=1))
    for ey in (-1, 1):
        yy = y + ey * (sy / 2 + .005)
        parts.append(beam(col, "Crate brace", (x - sx / 2 + t, yy, z + t), (x + sx / 2 - t, yy, z + sz - t), w=.08, h=.03, mat=WALNUT, segments=1))
    for ex in (-1, 1):
        xx = x + ex * (sx / 2 + .005)
        parts.append(beam(col, "Crate brace", (xx, y - sy / 2 + t, z + t), (xx, y + sy / 2 - t, z + sz - t), w=.03, h=.08, mat=WALNUT, segments=1))
    return parts


def tile_course(col, p0, p1, y0, y1, up, pitch=.3, amp=.03, mat=TILE):
    """One course of round tiles from p0 down to p1 (Blender x-z points: the course's upper and lower edges) along
    y0..y1: one strip whose top waves across the tiles, flat beneath, triangulated ends (the lowest course's end is the
    eave's row of tile mouths)."""
    n = max(1, int(round((y1 - y0) / pitch)))
    pitch = (y1 - y0) / n
    top = []
    for k in range(n):
        for f in (0, .25, .5, .75):
            top.append((y0 + (k + f) * pitch, amp * math.sin(2 * math.pi * f) + amp))
    top.append((y1, amp))
    outline = top + [(y1, -.02), (y0, -.02)]
    bm = bmesh.new()
    r0 = [bm.verts.new(Vector((p0.x, y, p0.z)) + up * h) for y, h in outline]
    r1 = [bm.verts.new(Vector((p1.x, y, p1.z)) + up * h) for y, h in outline]
    m = len(outline)
    caps = [bm.faces.new(r0), bm.faces.new(list(reversed(r1)))]
    for k in range(m):
        f = bm.faces.new((r0[k], r1[k], r1[(k + 1) % m], r0[(k + 1) % m]))
        f.smooth = k < len(top) - 1
    bmesh.ops.triangulate(bm, faces=caps)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_object("Tiles", bm, col, (mat,))


# ------------------------------------------------------------------ the court and the yard


def court_and_yard(col):
    parts = []
    lo, hi = col_box("1 Entrance/Start court")
    parts.append(flagstones(col, "Court paving", lo.x, hi.x, lo.y, hi.y, hi.z, size=1.1))
    for name, face in (("Court wall W", "+x"), ("Court wall E", "-x"), ("Court wall S", "+y")):
        lo, hi = col_box("1 Entrance/" + name)
        parts.append(ashlar(col, name, Vector((lo.x, lo.y, lo.z + .45)), Vector((hi.x, hi.y, hi.z - .22)), face))
        g = Vector((.04, 0, 0)) if face in ("+x", "-x") else Vector((0, .04, 0))
        parts.append(box(col, name + " plinth", Vector((lo.x, lo.y, 0)) - g, Vector((hi.x, hi.y, .45)) + g, STONE, bevel=.04))
        parts.append(box(col, name + " coping", Vector((lo.x, lo.y, hi.z - .22)) - g * 2, hi + g * 2, STONE, bevel=.05))
        parts += crenels(col, "Merlon", lo.x, hi.x, lo.y, hi.y, hi.z, height=.75)
    # The yard: big flags where the carriages run, from shed front to shed front.
    lo, hi = col_box("1 Entrance/Yard")
    parts.append(box(col, "Yard base", (lo.x, lo.y, lo.z), (hi.x, hi.y, hi.z - .06), STONE))
    parts.append(flagstones(col, "Yard paving", -19.0, 19.0, lo.y, hi.y, hi.z, size=2.0))
    for name, face in (("Yard wall W", "+x"), ("Yard wall E", "-x")):
        lo, hi = col_box("1 Entrance/" + name)
        parts.append(ashlar(col, name, Vector((lo.x, lo.y, lo.z)), Vector((hi.x, hi.y, hi.z - .2)), face))
        parts.append(box(col, name + " coping", (lo.x, lo.y, hi.z - .2), (hi.x, hi.y, hi.z), STONE, bevel=.04))
    # The start court's crates (the code's Crates at (9.8, -14.6): two along z, one more on the first).
    for i in range(2):
        cy = -14.6 + i * 1.6
        parts += crate(col, (9.8, cy, 0), (1.4, 1.4, 1.4))
        if i % 2 == 0:
            parts += crate(col, (9.9, cy, 1.4), (1.1, 1.1, 1.1), dark=True)
    return parts


# ------------------------------------------------------------------ the sheds and their loads


def lumpy(col, name, bottom, radii, mat, seed):
    """A soft lump standing on `bottom` (Blender): a ball with noise, its underside flattened."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=9, radius=1.0)
    r = random.Random(seed)
    for v in bm.verts:
        n = v.co.normalized()
        k = 1 + .07 * math.sin(3.1 * n.x + seed) * math.cos(2.3 * n.y) + r.uniform(-.025, .025)
        p = Vector((n.x * radii[0] * k, n.y * radii[1] * k, n.z * radii[2] * k))
        if p.z < -radii[2] * .6:
            p.z = -radii[2] * .6 + (p.z + radii[2] * .6) * .1
        v.co = p
    for f in bm.faces:
        f.smooth = True
    bmesh.ops.translate(bm, vec=Vector(bottom) + Vector((0, 0, radii[2] * .6)), verts=bm.verts)
    return mesh_object(name, bm, col, (mat,))


def sack(col, centre, scale, seed):
    """The code's 1 m ball scaled (Unity x, y, z) with its middle at `centre` (Blender): a tied sack lying down."""
    sx, sy, sz = scale
    c = Vector(centre)
    radii = (sx * .5, sz * .5, sy * .5)
    bottom = c - Vector((0, 0, radii[2] * .6))
    parts = [lumpy(col, "Sack", bottom, radii, SACK, seed)]
    end = c + Vector((0, radii[1] * .9, 0))
    parts.append(lathe(col, "Sack neck", [(0, 0), (.11, 0), (.07, .06), (.06, .1), (.1, .16), (.06, .2), (0, .21)],
                       end, axis="y", seg=8, mat=SACK, smooth=True))
    return parts


def barrel(col, foot, lying=False, d=1.0, h=1.3):
    """A staved barrel: 20 staves (the facets), four hoops, a sunk head."""
    r = d * .5
    prof = [(0, .04), (r * .8, .04), (r * .82, 0), (r * .87, 0), (r * .94, h * .25), (r, h * .5), (r * .94, h * .75),
            (r * .87, h), (r * .82, h), (r * .8, h - .04), (0, h - .04)]
    parts = [lathe(col, "Barrel", prof, (0, 0, 0), seg=20, mat=OAK)]
    for t in (.1, .26, .74, .9):
        rr = r * (.87 + .13 * math.sin(math.pi * t)) + .012
        parts.append(arc_ring(col, "Barrel hoop", 0, 0, rr - .03, rr, h * t - .035, h * t + .035, IRON, seg=20, bevel=0))
    ob = join(col, "Barrel", parts)
    if lying:
        ob.rotation_euler = (math.radians(-90), 0, 0)
        ob.location = Vector(foot) + Vector((0, -h / 2, r))
    else:
        ob.location = Vector(foot)
    return [ob]


def hay_bale(col, lo, hi, seed):
    lo, hi = Vector(lo), Vector(hi)
    ob = box(col, "Hay bale", lo + Vector((.02, .02, 0)), hi - Vector((.02, .02, .02)), STRAW)
    m = ob.modifiers.new("Bevel", "BEVEL")
    m.width, m.segments = .1, 2
    tex = bpy.data.textures.get("RCY straw") or bpy.data.textures.new("RCY straw", "CLOUDS")
    tex.noise_scale = .1
    rough = ob.modifiers.new("Rough", "DISPLACE")
    rough.texture = tex
    rough.strength = .025
    rough.mid_level = .5
    sub = ob.modifiers.new("Sub", "SUBSURF")
    sub.levels = 1
    bpy.context.view_layer.objects.active = ob
    ob.modifiers.move(2, 1)   # bevel, subdivide, then roughen
    parts = [ob]
    long_x = (hi.x - lo.x) >= (hi.y - lo.y)
    for t in (.3, .7):
        if long_x:
            x = lo.x + (hi.x - lo.x) * t
            parts.append(box(col, "Twine", (x - .02, lo.y - .01, lo.z + .03), (x + .02, hi.y + .01, hi.z + .005), LEATHER))
        else:
            y = lo.y + (hi.y - lo.y) * t
            parts.append(box(col, "Twine", (lo.x - .01, y - .02, lo.z + .03), (hi.x + .01, y + .02, hi.z + .005), LEATHER))
    return parts


def covered_load(col, lo, hi, seed):
    """Goods under a royal cloth: a draped lump with folds round its foot, the gold stripe, a rope over it."""
    lo, hi = Vector(lo), Vector(hi)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=7, use_grid_fill=True)
    c, s = (lo + hi) / 2, hi - lo
    for v in bm.verts:
        p = v.co.copy()
        q = Vector((p.x * s.x, p.y * s.y, (p.z + .5) * s.z))
        # The top sags between the goods' corners; the sides fall in folds near the floor.
        if p.z > .45:
            q.z -= .12 * (1 - (2 * abs(p.x)) ** 2) * (1 - (2 * abs(p.y)) ** 2) * (.6 + .4 * math.sin(p.x * 11 + seed))
        low = max(0.0, .1 - p.z) * 1.6
        fold = .05 * math.sin((p.x * s.x + p.y * s.y) * 6 + seed) * low
        q.x += fold * (1 if p.x > 0 else -1)
        q.y += fold * (1 if p.y > 0 else -1)
        q.z = max(0.0, q.z)
        v.co = Vector((c.x, c.y, lo.z)) + q
    for f in bm.faces:
        f.smooth = True
    parts = [mesh_object("Covered load", bm, col, (CLOTH,))]
    long_x = s.x >= s.y
    if long_x:
        parts.append(box(col, "Cloth stripe", (lo.x - .03, c.y - .11, lo.z + .05), (hi.x + .03, c.y + .11, hi.z + .02), GOLD))
    else:
        parts.append(box(col, "Cloth stripe", (c.x - .11, lo.y - .03, lo.z + .05), (c.x + .11, hi.y + .03, hi.z + .02), GOLD))
    for t in (.22, .78):
        if long_x:
            x = lo.x + s.x * t
            parts.append(box(col, "Rope", (x - .03, lo.y - .04, lo.z + .1), (x + .03, hi.y + .04, hi.z + .015), LEATHER))
        else:
            y = lo.y + s.y * t
            parts.append(box(col, "Rope", (lo.x - .04, y - .03, lo.z + .1), (hi.x + .04, y + .03, hi.z + .015), LEATHER))
    return parts


SHED_Z0, SHED_Z1, SHED_BAY, SHED_DEPTH, YARD_HALF = 2.6, 47.6, 4.5, 3.0, 22.0


def valance(col, x, y0, y1, top, depth, width=.9):
    """A cloth hanging under an eave along y, its foot in scallops, a gold hem along its top."""
    parts = []
    n = max(1, int(round((y1 - y0) / width)))
    w = (y1 - y0) / n
    for k in range(n):
        a = y0 + k * w
        pts = [(a, top), (a + w, top)]
        for j in range(9):
            t = 1 - j / 8
            pts.append((a + w * t, top - depth * (.7 + .3 * math.sin(math.pi * t))))
        parts.append(prism(col, "Valance", pts, x - .025, x + .025, CLOTH, axis="x"))
    parts.append(box(col, "Valance hem", (x - .035, y0, top - .06), (x + .035, y1, top), GOLD))
    return parts


def load(col, side, front, z, bay, kind, seed):
    """One bay's load inside the solid goods block (the code's Load), Blender x across the shed, y along it."""
    x = front + side * 1.5
    w = bay - .5
    parts = []
    if kind == 0:
        for dz in (-w * .25, w * .25):
            parts += crate(col, (x, z + dz, 0), (2.6, w * .45, 1.2))
        parts += crate(col, (x + side * .3, z, 1.2), (1.6, 1.4, 1.2), dark=True)
    elif kind == 1:
        parts.append(box(col, "Pallet", (x - 1.35, z - w * .45, 0), (x + 1.35, z + w * .45, .16), WALNUT, bevel=.02))
        for i in range(6):
            dz, dx = (i % 3 - 1) * w * .28, (i // 3 - .5) * 1.2
            parts += sack(col, (x + dx, z + dz, .55), (1.1, .8, 1.2), seed * 10 + i)
        parts += sack(col, (x + side * .2, z, 1.25), (1.1, .75, 1.3), seed * 10 + 7)
    elif kind == 2:
        for i in range(4):
            dz, dx = (i % 2 - .5) * w * .45, (i // 2 - .5) * 1.2
            parts += barrel(col, (x + dx, z + dz, 0))
        parts += barrel(col, (x + side * .3, z, 1.3), lying=True)
    elif kind == 3:
        parts += covered_load(col, (x - 1.3, z - w * .425, 0), (x + 1.3, z + w * .425, 1.8), seed)
    else:
        for i in range(3):
            cz = .45 + (.9 if i == 2 else 0)
            cy = z + (0 if i == 2 else (i - .5) * w * .48)
            parts += hay_bale(col, (x - 1.2, cy - w * .22, cz - .45), (x + 1.2, cy + w * .22, cz + .45), seed * 3 + i)
    return parts


def shed(col, side):
    """One side's storage shed (the code's Granaries)."""
    parts = []
    front, back = side * (YARD_HALF - SHED_DEPTH), side * YARD_HALF
    x0, x1 = min(front, back), max(front, back)
    bays = max(1, round((SHED_Z1 - SHED_Z0) / SHED_BAY))
    bay = (SHED_Z1 - SHED_Z0) / bays
    # Floor boards along the shed, under the goods.
    y = SHED_Z0
    while y < SHED_Z1 - 1e-3:
        parts.append(box(col, "Shed board", (x0 + .02, y + .01, 0), (x1 - .02, min(y + .3, SHED_Z1) - .01, .03), WALNUT))
        y += .3
    eave_x = front - side * .35
    high, low = 4.6, 3.4
    # Posts on stone footings, knee braces up to the eave plate.
    for i in range(bays + 1):
        py = SHED_Z0 + i * bay
        px = front + side * .14
        parts += square_post(col, "Shed post", px, py, .25, 3.34, w=.24, mat=WALNUT)
        parts.append(box(col, "Post footing", (px - .19, py - .19, 0), (px + .19, py + .19, .25), STONE, bevel=.03))
        for d in (-1, 1):
            if (i == 0 and d < 0) or (i == bays and d > 0):
                continue
            parts.append(beam(col, "Knee brace", (px, py + d * .1, 2.7), (px, py + d * .78, 3.33), w=.11, h=.11, mat=WALNUT))
    parts.append(box(col, "Eave plate", (front + side * .14 - .14, SHED_Z0 - .2, 3.32), (front + side * .14 + .14, SHED_Z1 + .2, 3.56), WALNUT, bevel=.025))
    lx = back - side * .12
    parts.append(box(col, "Wall ledger", (lx - .12, SHED_Z0 - .2, 4.36), (lx + .12, SHED_Z1 + .2, 4.6), WALNUT, bevel=.02))
    # Rafters from the wall over the eave plate to the eave; tile courses on them.
    dx, dz = eave_x - back, low - high
    run = math.hypot(dx, dz)
    up = Vector((-dz, 0, dx)) / run
    if up.z < 0:
        up = -up
    y = SHED_Z0 - .2
    while y <= SHED_Z1 + .2 + 1e-3:
        parts.append(beam(col, "Rafter", Vector((back, y, high)) - up * .09, Vector((eave_x, y, low)) - up * .09, w=.1, h=.14, mat=WALNUT))
        y += .9
    courses = 7
    for k in range(courses):
        # Each course lies over the top of the one below it (a lap of 4 % of the run).
        t0, t1 = k / courses, min(1.0, (k + 1) / courses + .04)
        lift = up * (.02 + .035 * (courses - 1 - k) / (courses - 1))
        p0 = Vector((back + dx * t0, 0, high + dz * t0)) + lift
        p1 = Vector((back + dx * t1, 0, high + dz * t1)) + lift
        parts.append(tile_course(col, p0, p1, SHED_Z0 - .2, SHED_Z1 + .2, up))
    parts.append(box(col, "Fascia", (eave_x - .05, SHED_Z0 - .2, low - .14), (eave_x + .05, SHED_Z1 + .2, low + .04), GOLD, bevel=.015))
    parts += valance(col, eave_x - side * .03, SHED_Z0 - .2, SHED_Z1 + .2, low - .12, .32)
    # Lanterns round the code's glowing lamps (on the odd posts), hung from the eave plate.
    for i in range(1, bays + 1, 2):
        py = SHED_Z0 + i * bay
        lp = front - side * .1
        parts.append(box(col, "Lantern cap", (lp - .17, py - .17, 3.22), (lp + .17, py + .17, 3.27), IRON, bevel=.01, segments=1))
        parts.append(lathe(col, "Lantern roof", [(0, 3.27), (.16, 3.27), (.05, 3.4), (.03, 3.44), (.05, 3.47), (0, 3.5)], (lp, py, 0), seg=4, mat=IRON))
        parts.append(box(col, "Lantern base", (lp - .16, py - .16, 2.82), (lp + .16, py + .16, 2.88), IRON, bevel=.01, segments=1))
        for ex in (-1, 1):
            for ey in (-1, 1):
                parts.append(box(col, "Lantern bar", (lp + ex * .145 - .015, py + ey * .145 - .015, 2.88), (lp + ex * .145 + .015, py + ey * .145 + .015, 3.22), IRON))
    # The royal canopy over the middle bay, its valance and crown.
    mid = (SHED_Z0 + SHED_Z1) * .5
    cx0, cx1 = min(front - side * .45, front + side * 1.2), max(front - side * .45, front + side * 1.2)
    parts.append(box(col, "Royal canopy", (cx0, mid - bay * .55, 3.55), (cx1, mid + bay * .55, 3.74), CLOTH, bevel=.04))
    parts += valance(col, front - side * .47, mid - bay * .55, mid + bay * .55, 3.6, .5, width=.75)
    parts += crown(col, (front - side * .5, mid, 3.74), .6)
    for i in range(bays):
        parts += load(col, side, front, SHED_Z0 + (i + .5) * bay, bay, (i * 7 + (0 if side < 0 else 3)) % 5, i + (0 if side < 0 else 20))
    return parts


def build_entrance():
    col = collection("RCY Entrance")
    clear(col)
    # The gate wall, its lintel and the two gate towers are the AI castle kit's outer gatehouse since R678
    # (castle_model.py, RcyCastleOuterGate).
    pieces = court_and_yard(col)
    for side in (-1, 1):
        pieces += shed(col, side)
    # R680 QA (collision-proxy): the yard collider runs to x +-22 but the paving to +-19 and the sheds only from
    # SHED_Z0 to SHED_Z1, so two strips each side showed the base 6 cm under the floor; paved here (own random so
    # nothing above changes).
    strip_rng = random.Random(680)
    lo, hi = col_box("1 Entrance/Yard")
    for s in (-1, 1):
        xa, xb = sorted((s * 19.0, s * YARD_HALF))
        for ya, yb in ((lo.y, SHED_Z0), (SHED_Z1, hi.y)):
            pieces.append(flagstones(col, "Yard paving", xa, xb, ya, yb, hi.z, size=2.0, rng=strip_rng))
    return join(col, "Entrance", pieces)


if globals().get("RCY_BUILD", True):
    entrance = build_entrance()
    print("entrance tris", tris(bpy.data.collections["RCY Entrance"]))
