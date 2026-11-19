"""R6 play-time chess (seoungtest R687, PLAN.md R6 minus the giants, 2026-11-18): what GiantChessCourse.cs builds in
Play - the 8 x 8 board, its frame, the attack-warning plate and occupied-square border, the entry / exit decks, the
finish stripe, the exit arch and the six piece shapes (ivory / ebony, brass bands) - to concept D (GC_D2, GC_D4, GC_D6).

    blender -b --factory-startup --python rl_r6_chess_model.py -- <parts|all> [--preview <dir>] [--no-export] [--stills]

parts: board, marks, decks, finish, arch, pawn, knight, bishop, rook, queen, king (comma list). Every listed part is
built; the .blend (rl_r6_chess.blend) keeps what this run built. --stills renders stills/r6/<part>.png (pieces: the
ivory and ebony twins on white, as the D6 sheet; the rest: the library look via rl_lookdev). --preview <dir> writes the
FBX under <dir> instead of Assets (Unity in Play); --no-export skips FBX.

Frames (Blender x, y, z = Unity x, z, y; metres at tileSize 10 - the code scales by tileSize / 10). Every model has its
own origin, the code places it:
  RlChessBoard     course-local board frame: squares x -40 .. 40, y 0 .. 80, tops at z 0 (cream stone / walnut, the
                   square colour rule of GiantChessCourse: (s % 8 + s / 8) even = light), walnut side rails x +-40 ..
                   +-40.95 with brass corner caps (the R5 oak table x +-41 lies under it, its top z -0.8)
  RlChessWarning   one square's red plate, origin the square's centre on the board top (plate z .005 .. .06);
                   slot wax = the code's blinking red, slot brass = its brighter rim
  RlChessOccupied  one square's brass border (inside the square, z .03 .. .2)
  RlChessDeck8 / RlChessDeck20  walnut plank deck, origin the board edge's centre, running to -y (the code turns the
                   exit one round)
  RlChessFinish    the finish stripe (1 x 80 m checker inlay), origin its centre
  RlChessArch      the exit arch over the old pillars (x +-12, 2 x 2 m) and header (z 9 .. 11), origin its foot centre;
                   the sign plaque's face stays behind the code's sign text (Unity z = arch - 1.1)
  RlChessPawn ... RlChessKing   origin the centre of the base, facing -y (the knight's muzzle); body slot ivory (the
                   code draws black pieces in ebony), bands brass; the king's cross is its own object "Cross"
Nothing here changes a collider: squares, decks and the arch keep the code's boxes; pieces keep their square boxes.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rl_kit as k   # noqa: E402

ZONE = "Chess"
BLEND = os.path.join(HERE, "rl_r6_chess.blend")
STILLS = os.path.join(HERE, "stills", "r6")
SMOOTH_ANGLE = math.radians(38)
SEGS = 40                     # lathe segments of a piece
BASE_R = 3.2                  # piece base radius (6.4 m across = 0.64 of a 10 m square)
PIECE_SQUASH = .88            # traced heights x this above z 1.4 (the sheet's pieces read squatter than traced)
SQUASHED = {"RlChessPawn", "RlChessKnight", "RlChessBishop", "RlChessRook", "RlChessQueen", "RlChessKing", "Cross"}
PREVIEW = {"RL | ivory": (.80, .66, .44, 1), "RL | ebony": (.030, .020, .015, 1)}   # until R0 adds the slots


def mat(slot):
    key = slot if slot.startswith("RL | ") else "RL | " + slot
    try:
        return k.slot(key)
    except KeyError:
        m = bpy.data.materials.get(key)
        if m is None:
            m = bpy.data.materials.new(key); m.use_nodes = True
            p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
            p.inputs["Base Color"].default_value = PREVIEW[key]
            p.inputs["Roughness"].default_value = .3
            m.diffuse_color = PREVIEW[key]
        return m


# ------------------------------------------------------------------ mesh builder
class Builder:
    """Parts made in a scratch bmesh, transformed and copied into one mesh with their slot."""

    def __init__(self):
        self.bm = bmesh.new(); self.slots = []

    def add(self, src, slot, M=Matrix.Identity(4)):
        if slot not in self.slots:
            self.slots.append(slot)
        mi = self.slots.index(slot)
        vm = {v: self.bm.verts.new(M @ v.co) for v in src.verts}
        for f in src.faces:
            try:
                nf = self.bm.faces.new([vm[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = mi; nf.smooth = f.smooth
        src.free()

    def obj(self, name, col, weld=True, sharp=SMOOTH_ANGLE):
        me = bpy.data.meshes.new(name)
        if name in SQUASHED:   # pieces: everything above the plinth pressed to the D6 sheet's proportions
            for v in self.bm.verts:
                if v.co.z > 1.4:
                    v.co.z = 1.4 + (v.co.z - 1.4) * PIECE_SQUASH
        if weld:
            bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me); self.bm.free()
        for s in self.slots:
            me.materials.append(mat(s))
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=sharp)
        o = bpy.data.objects.new(name, me)
        col.objects.link(o)
        k.surface_uvs(o)
        return o


def box_bm(lo, hi, bevel=0.0, segs=1, top_only=False):
    lo, hi = Vector(lo), Vector(hi)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=hi - lo, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(lo + hi) / 2, verts=bm.verts)
    if bevel > 0:
        b = min(bevel, min(hi - lo) * .45)
        edges = list(bm.edges)
        if top_only:
            edges = [e for e in edges if all(v.co.z > hi.z - 1e-6 for v in e.verts)]
        bmesh.ops.bevel(bm, geom=edges, offset=b, segments=segs, profile=.5, affect="EDGES", clamp_overlap=True)
    return bm


def lathe_bm(profile, segs=SEGS, phase=0.0):
    """Turned solid: profile [(r, z)] bottom to top; r 0 at an end closes it with a pole."""
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        if r < 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(phase + 2 * math.pi * i / segs),
                                        r * math.sin(phase + 2 * math.pi * i / segs), z)) for i in range(segs)])
    for a, b in zip(rings, rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            if len(a) == 1 and len(b) == 1:
                continue
            if len(a) == 1:
                bm.faces.new((a[0], b[j], b[i]))
            elif len(b) == 1:
                bm.faces.new((a[i], a[j], b[0]))
            else:
                bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    return bm


def arc(cx, cz, r, a0, a1, n):
    """Profile points on a circle (degrees, 0 = +r, 90 = up)."""
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cz + r * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
            for i in range(n + 1)]


def band_bm(z0, z1, r, r_in=None, segs=SEGS):
    """A brass band: a half-round ring r out, standing z0 .. z1 (r_in: its inner face, inside the body)."""
    h = z1 - z0; c = h * .48
    ri = r_in if r_in is not None else r - .35
    return lathe_bm([(ri, z0), (r - c, z0)] + arc(r - c, z0 + c, c, -90, 0, 3)[1:] + arc(r - c, z1 - c, c, 0, 90, 3)[1:]
                    + [(ri, z1)], segs)

def evaluated_bm(o):
    bm = bmesh.new()
    bm.from_object(o, bpy.context.evaluated_depsgraph_get())
    return bm


def temp_obj(bm, name="tmp"):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def drop(*objs):
    for o in objs:
        me = o.data; bpy.data.objects.remove(o)
        if isinstance(me, bpy.types.Mesh) and me.users == 0:
            bpy.data.meshes.remove(me)


def boolean(bm, cutters, op="DIFFERENCE"):
    """bm minus (or union) the cutter bmeshes, exact solver; returns a new bmesh."""
    o = temp_obj(bm)
    cs = []
    for c in cutters:
        co = temp_obj(c, "cut"); co.hide_render = True; cs.append(co)
        m = o.modifiers.new("b", "BOOLEAN"); m.operation = op; m.object = co; m.solver = "EXACT"
    out = evaluated_bm(o)
    drop(o, *cs)
    return out


def collection(name):
    col = bpy.data.collections.get(name)
    if col:
        for o in list(col.objects):
            drop(o)
    else:
        col = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col)
    return col


# ------------------------------------------------------------------ pieces (D6 bottom rows)
# Profiles traced from the D6 sheet (each piece's base scaled to 6.4 m across): a low wide plinth, a fat half-round brass
# ring on it, a broad flared skirt, then the piece's own top. (r, z) in metres, z up from the square.
def plinth():
    R = BASE_R
    body = [(0, 0), (R - .1, 0)] + arc(R - .1, .1, .1, -90, 0, 2)[1:] + [(R, .78)] \
        + arc(R - .14, .78, .14, 0, 90, 2)[1:] + [(2.9, .96), (2.9, 1.4)]
    return body, [band_bm(.94, 1.4, 3.02, 2.75)]


def skirt(waist_r, waist_z):
    """The flared skirt from the brass ring up to the waist (D6: a soft bell)."""
    pts = []
    for i in range(1, 9):
        t = i / 8
        z = 1.4 + (waist_z - 1.4) * t
        r = waist_r + (2.95 - waist_r) * (1 - t) ** 1.7
        pts.append((r, z))
    return pts


def ball(cz, rh, rv, z_from, n=12):
    """Ellipsoid cap (horizontal radius rh, vertical rv, centre cz) from height z_from to its top."""
    a0 = math.degrees(math.asin(max(-1, min(1, (z_from - cz) / rv))))
    return [(rh * math.cos(math.radians(a0 + (90 - a0) * i / n)), cz + rv * math.sin(math.radians(a0 + (90 - a0) * i / n)))
            for i in range(n + 1)]


def finish(b, name, col):
    return b.obj(name, col)


def build_pawn(col):
    body, bands = plinth()
    body += skirt(1.3, 4.0) + [(1.82, 4.05)] + arc(1.82, 4.3, .25, -90, 90, 4)[1:] + [(1.5, 4.62), (1.45, 4.86)]
    body += ball(6.27, 1.78, 1.42, 4.92)[1:]
    b = Builder()
    b.add(lathe_bm(body), "ivory")
    for x in bands:
        b.add(x, "brass")
    b.add(band_bm(4.56, 4.88, 1.62, 1.3), "brass")      # the thin ring between collar and head
    return [b.obj("RlChessPawn", col)]


def build_rook(col):
    body, bands = plinth()
    body += [(3.05, 1.6), (3.07, 1.85), (3.0, 2.3), (2.85, 2.6), (2.55, 2.82), (2.3, 2.95), (2.12, 3.15), (2.05, 3.4),
             (1.9, 4.3), (1.93, 4.8), (2.12, 5.05), (2.32, 5.25), (2.48, 5.6), (2.55, 6.0), (2.57, 7.25)] \
        + arc(2.45, 7.25, .12, 0, 90, 2)[1:] + [(1.85, 7.37), (1.8, 6.95), (0, 6.95)]
    rook = lathe_bm(body)
    cuts = []
    for i in range(6):   # six narrow notches, 0.6 wide and 0.6 deep (two face the front, as on the sheet)
        c = box_bm((-.3, 1.2, 6.77), (.3, 3.2, 7.6))
        bmesh.ops.rotate(c, verts=c.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(2 * math.pi * i / 6, 3, "Z"))
        cuts.append(c)
    rook = boolean(rook, cuts)
    for f in rook.faces:
        f.smooth = True
    b = Builder()
    b.add(rook, "ivory")
    for x in bands:
        b.add(x, "brass")
    return [b.obj("RlChessRook", col)]


def build_bishop(col):
    body, bands = plinth()
    body += skirt(1.42, 3.86) + [(2.0, 3.9)] + arc(2.0, 4.18, .28, -90, 90, 4)[1:] + [(1.6, 4.5), (1.55, 4.72)]
    # the mitre: a broad egg (2.05 radius at 6.35) to a point at 8.35, a small knob on a neck to 9.15
    for i in range(1, 17):
        t = i / 16
        z = 4.75 + (8.35 - 4.75) * t
        u = (z - 6.35) / (8.35 - 6.35) if z > 6.35 else (z - 6.35) / (6.35 - 4.75)
        r = 2.05 * math.sqrt(max(0.0, 1 - u * u)) if z > 6.35 else 1.55 + .5 * math.sqrt(max(0.0, 1 - u * u))
        if z > 6.35:
            r = max(r * (1 - .25 * u), .28)
        body.append((r, z))
    body += [(.28, 8.45), (.22, 8.6)] + ball(8.85, .32, .3, 8.6, 5)[1:]
    mitre = lathe_bm(body)
    # the slit: a groove 0.3 wide, cut into the front half only, slanting down from upper left to the middle
    c = box_bm((.4, -.15, -.2), (3, .15, 3.2))
    bmesh.ops.rotate(c, verts=c.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-48), 3, "X"))
    bmesh.ops.translate(c, verts=c.verts, vec=(0, .05, 6.25))
    mitre = boolean(mitre, [c])
    for f in mitre.faces:
        f.smooth = True
    b = Builder()
    b.add(mitre, "ivory")
    for x in bands:
        b.add(x, "brass")
    b.add(band_bm(4.42, 4.76, 1.72, 1.4), "brass")
    return [b.obj("RlChessBishop", col)]


def _royal_body():
    """Queen / king: the skirt to a slim waist, a fat brass collar ring, then the cup flaring out."""
    body, bands = plinth()
    body += skirt(1.39, 3.84) + [(1.42, 4.7), (1.55, 4.75), (1.68, 5.3), (1.95, 5.9)]
    bands.append(band_bm(3.82, 4.7, 2.05, 1.3))
    return body, bands


def build_queen(col):
    body, bands = _royal_body()
    # the coronet: the cup to r 2.75, eight points (balls on them), the rim dipping between, a dome and a top ball
    body += [(2.3, 6.25), (2.62, 6.6), (2.75, 6.82), (2.62, 6.95), (2.2, 6.75), (1.6, 6.45), (0, 6.45)]
    cup = lathe_bm(body)
    for v in cup.verts:
        rr = math.hypot(v.co.x, v.co.y)
        if v.co.z > 6.0 and rr > 1.7:
            a = math.atan2(v.co.y, v.co.x)
            f = ((1 + math.cos(8 * a)) / 2) ** 1.4
            v.co.z -= (1 - f) * .75 * min(1.0, (v.co.z - 6.0) / .8)
    b = Builder()
    b.add(cup, "ivory")
    for x in bands:
        b.add(x, "brass")
    for i in range(8):
        a = 2 * math.pi * i / 8
        s = bmesh.new(); bmesh.ops.create_uvsphere(s, u_segments=14, v_segments=9, radius=.5)
        for f in s.faces:
            f.smooth = True
        b.add(s, "ivory", Matrix.Translation((2.62 * math.cos(a), 2.62 * math.sin(a), 7.25)))
    b.add(lathe_bm(ball(6.45, 1.35, .75, 6.3, 6) + [(.3, 7.25)] + ball(7.72, .52, .5, 7.25, 6)[1:]), "ivory")
    return [b.obj("RlChessQueen", col)]


def build_king(col):
    body, bands = _royal_body()
    # an inverted bell to r 2.38, a rounded rim, a low dome, a little plinth for the cross
    body += [(2.22, 6.3), (2.38, 6.57)] + arc(2.24, 6.62, .15, -20, 90, 3)[1:] + [(1.9, 6.85), (1.3, 7.02),
                                                                                 (.75, 7.12), (.72, 7.32), (0, 7.34)]
    b = Builder()
    b.add(lathe_bm(body), "ivory")
    for x in bands:
        b.add(x, "brass")
    king = b.obj("RlChessKing", col)
    c = Builder()   # the chunky cross (D6): 0.95 m bars, arms across y, so it faces the model's x
    c.add(box_bm((-.45, -.47, 7.3), (.45, .47, 8.95), .2, 2), "ivory")
    c.add(box_bm((-.45, -1.27, 7.75), (.45, 1.27, 8.55), .2, 2), "ivory")
    cross = c.obj("Cross", col)
    cross.parent = king
    return [king, cross]


def _smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


# The D6 white knight traced at 6x (base 6.4 m across; y = muzzle to -y, z up, metres before PIECE_SQUASH).
KNIGHT_BODY = [(-2.45, 2.4), (-2.08, 2.93), (-1.97, 3.25), (-1.57, 3.89), (-1.05, 4.45), (-.85, 4.6),   # neck front up
               (-1.4, 4.38), (-2.13, 4.13), (-2.77, 3.89), (-3.2, 3.98), (-3.45, 4.25), (-3.73, 4.85),  # jaw, chin
               (-3.85, 5.33), (-3.72, 5.6), (-3.4, 5.85), (-2.61, 6.69), (-2.13, 7.25), (-1.73, 7.49),  # muzzle, face
               (-1.0, 7.66), (-.48, 7.73), (.32, 7.6), (1.0, 7.25), (1.55, 6.69), (2.1, 5.73), (2.19, 4.53),
               (2.19, 2.93), (2.1, 2.4)]                                                                   # neck back
MANE_OUT = [(-.05, 8.13), (.99, 8.21), (1.55, 7.89), (2.19, 7.33), (2.74, 6.69), (3.06, 5.97), (3.22, 5.17),
            (3.18, 4.37), (3.06, 3.57), (2.9, 2.93)]
MANE_IN = [(-.4, 7.55), (.45, 7.5), (1.0, 7.15), (1.45, 6.65), (1.8, 6.15), (2.0, 5.65), (2.08, 5.05), (2.08, 4.35),
           (2.08, 3.55), (2.05, 2.93)]


def build_knight(col):
    body, bands = plinth()
    body += [(2.95, 1.6), (2.93, 1.95), (2.75, 2.35), (2.45, 2.62), (2.1, 2.75), (0, 2.75)]
    b = Builder()
    b.add(lathe_bm(body), "ivory")
    for x in bands:
        b.add(x, "brass")
    outline = KNIGHT_BODY
    # 1. the head and neck: the side outline extruded, narrowed toward the muzzle and the poll
    hb = bmesh.new()
    f = hb.faces.new([hb.verts.new((-1, y, z)) for y, z in outline])
    ex = bmesh.ops.extrude_face_region(hb, geom=[f])
    bmesh.ops.translate(hb, vec=(2, 0, 0), verts=[e for e in ex["geom"] if isinstance(e, bmesh.types.BMVert)])

    def half_width(y, z):
        head = _smoothstep(4.0, 4.8, z) * _smoothstep(.3, -1.0, y)
        w = 1.12 * (1 - .22 * head)                                   # neck 1.12, the skull .87
        w *= 1 - .28 * _smoothstep(-1.6, -3.85, y) * _smoothstep(4.0, 4.6, z)   # the muzzle to about .62
        w *= 1 - .22 * _smoothstep(7.1, 7.8, z)                       # the poll
        return w
    for v in hb.verts:
        v.co.x = math.copysign(half_width(v.co.y, v.co.z), v.co.x)
    bmesh.ops.recalc_face_normals(hb, faces=hb.faces)
    parts = []

    def blob(loc, size, rot=0.0):
        s = bmesh.new(); bmesh.ops.create_uvsphere(s, u_segments=16, v_segments=10, radius=1)
        bmesh.ops.scale(s, vec=size, verts=s.verts)
        bmesh.ops.rotate(s, verts=s.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(rot, 3, "X"))
        bmesh.ops.translate(s, vec=loc, verts=s.verts)
        parts.append(s)
    # ears: pointed, upright, a little forward (D6 tips at z 8.3 - 8.4)
    for sx in (-1, 1):
        blob((sx * .42, -1.2, 7.85), (.24, .32, .66), math.radians(12))
    blob((0, .0, 2.55), (1.45, 2.5, .3))                                # where the neck meets the base
    for s in parts:
        vm = {v: hb.verts.new(v.co) for v in s.verts}
        for fc in s.faces:
            hb.faces.new([vm[v] for v in fc.verts])
        s.free()
    horse = temp_obj(hb, "horse")
    m = horse.modifiers.new("r", "REMESH"); m.mode = "VOXEL"; m.voxel_size = .05
    dense = evaluated_bm(horse)
    drop(horse)
    segs2 = list(zip(outline, outline[1:] + outline[:1]))

    def inside(y, z):
        best = 9.0
        for (y0, z0), (y1, z1) in segs2:
            dy, dz = y1 - y0, z1 - z0
            t = max(0.0, min(1.0, ((y - y0) * dy + (z - z0) * dz) / (dy * dy + dz * dz)))
            best = min(best, math.hypot(y - y0 - t * dy, z - z0 - t * dz))
        return best
    # 2. rounded flanks, then the carved face: the cheek plate (a raised disc with a soft rim, D6's jaw line),
    #    the brow over the eye, the nostril and the mouth line
    for v in dense.verts:
        if abs(v.co.x) > .2 and v.co.z > 2.7:
            d = min(1.0, inside(v.co.y, v.co.z) / 1.2)
            v.co.x *= 1 + .2 * _smoothstep(0, 1, d)
    horse = temp_obj(dense, "horse")
    m = horse.modifiers.new("s", "SMOOTH"); m.factor = .7; m.iterations = 6
    dense = evaluated_bm(horse)
    drop(horse)
    dense.normal_update()
    for v in dense.verts:
        y, z, ax = v.co.y, v.co.z, abs(v.co.x)
        if ax < .3:
            continue
        sx = math.copysign(1, v.co.x)
        push = 0.0
        r = math.hypot((y + 1.35) / 1.05, (z - 5.05) / .9)          # the cheek plate
        push += .09 * (1 - _smoothstep(.7, 1.0, r))
        r = math.hypot((y + 1.3) / .55, (z - 6.55) / .22)           # the brow above the eye
        push += .08 * (1 - _smoothstep(.6, 1.0, r))
        r = math.hypot((y + 3.3) / .22, (z - 5.08) / .26)           # the nostril, a dent
        push -= .2 * (1 - _smoothstep(.5, 1.0, r))
        # the mouth line: a groove from the chin corner back along the lower jaw
        t = max(0.0, min(1.0, (y + 3.25) / 1.0))
        gy, gz = -3.25 + 1.0 * t, 4.38 + .32 * t
        dl = math.hypot(y - gy, z - gz) if -3.3 < y < -2.2 else 9
        push -= .09 * (1 - _smoothstep(.04, .12, dl))
        if push:
            v.co.x += sx * push
    # where the flank surface is at the eye (for the eye ball)
    eye_x = max((abs(v.co.x) for v in dense.verts if abs(v.co.y + 1.23) < .12 and abs(v.co.z - 6.29) < .12), default=.9)
    horse = temp_obj(dense, "horse")
    m = horse.modifiers.new("s", "SMOOTH"); m.factor = .5; m.iterations = 3
    m = horse.modifiers.new("d", "DECIMATE"); m.ratio = .036
    hm = evaluated_bm(horse)
    drop(horse)
    print("[r6] knight head", sum(len(fc.verts) - 2 for fc in hm.faces), "tris")
    for fc in hm.faces:
        fc.smooth = True
    b.add(hm, "ivory")
    # 3. the mane: nine carved plates between the neck's back (MANE_IN) and the crest (MANE_OUT), grooves between
    for i in range(len(MANE_OUT) - 1):
        def lerp(p, q, t):
            return (p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t)
        g = .06
        o0, o1 = lerp(MANE_OUT[i], MANE_OUT[i + 1], g), lerp(MANE_OUT[i], MANE_OUT[i + 1], 1 - g)
        i0, i1 = lerp(MANE_IN[i], MANE_IN[i + 1], g), lerp(MANE_IN[i], MANE_IN[i + 1], 1 - g)
        w = .62 - .1 * i / 8                                             # half thickness, thinner down the neck
        pm = bmesh.new()
        quad = [i0, o0, o1, i1]
        fc = pm.faces.new([pm.verts.new((-w, y, z)) for y, z in quad])
        ex = bmesh.ops.extrude_face_region(pm, geom=[fc])
        bmesh.ops.translate(pm, vec=(2 * w, 0, 0), verts=[e for e in ex["geom"] if isinstance(e, bmesh.types.BMVert)])
        bmesh.ops.recalc_face_normals(pm, faces=pm.faces)
        bmesh.ops.bevel(pm, geom=list(pm.edges), offset=.16, segments=2, profile=.5, affect="EDGES", clamp_overlap=True)
        for fc in pm.faces:
            fc.smooth = True
        b.add(pm, "ivory")
    # 4. the eyes: small dark glossy balls set into the flanks (D6: brown on both colours)
    for sx in (-1, 1):
        s = bmesh.new(); bmesh.ops.create_uvsphere(s, u_segments=12, v_segments=8, radius=.22)
        bmesh.ops.scale(s, vec=(.55, 1, 1), verts=s.verts)
        for fc in s.faces:
            fc.smooth = True
        b.add(s, "varnished", Matrix.Translation((sx * (eye_x - .02), -1.23, 6.29)))
    return [b.obj("RlChessKnight", col, sharp=math.radians(80))]   # the carved head: no hard creases


PIECES = {"pawn": build_pawn, "knight": build_knight, "bishop": build_bishop, "rook": build_rook,
          "queen": build_queen, "king": build_king}


# ------------------------------------------------------------------ board, marks, decks, arch (D2, D4)
T = 10.0
GAP = .04          # squares are 9.96 across: with the bevel a shallow groove between them


def build_board(col):
    b = Builder()
    h = (T - GAP) / 2
    for s in range(64):
        x = (s // 8 - 3.5) * T
        y = (7 - s % 8 + .5) * T
        light = (s % 8 + s // 8) % 2 == 0
        b.add(box_bm((x - h, y - h, -.8), (x + h, y + h, 0), .13, 2, top_only=True), "stone" if light else "walnut")
    # walnut side rails (x +-40 .. +-40.95) a hair under the squares, a brass inlay line, brass corner caps
    RX, RW, RT = 40.0, .95, -.04
    for sx in (-1, 1):
        x0, x1 = sorted((sx * RX, sx * (RX + RW)))
        b.add(box_bm((x0, 0, -.8), (x1, 80, RT), .12, 2), "varnished")
        xm = sx * (RX + RW * .5)
        b.add(box_bm((xm - .07, 4.5, RT - .02), (xm + .07, 75.5, RT + .015), .02), "brass")
        for yc, sy in ((0, 1), (80, -1)):
            y0, y1 = sorted((yc, yc + sy * 5))
            b.add(box_bm((x0 - .02, y0 - .02, -.82), (x1 + .04, y1 + .02, RT + .06), .06, 2), "brass")
            for d in (1.2, 3.8):
                s = bmesh.new(); bmesh.ops.create_uvsphere(s, u_segments=10, v_segments=6, radius=.16)
                b.add(s, "brass", Matrix.Translation((xm, yc + sy * d, RT + .07)))
                s = bmesh.new(); bmesh.ops.create_uvsphere(s, u_segments=10, v_segments=6, radius=.16)
                b.add(s, "brass", Matrix.Translation((sx * (RX + RW + .05), yc + sy * d, -.42)))
    return [b.obj("RlChessBoard", col, weld=False)]


def rounded_rect_bm(half, r, z0, z1, n=4):
    """Flat rounded rectangle (square half-size `half`, corner radius r) from z0 to z1."""
    pts = []
    for cx, cy, a0 in ((half - r, half - r, 0), (-half + r, half - r, 90), (-half + r, -half + r, 180),
                       (half - r, -half + r, 270)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    bm = bmesh.new()
    f = bm.faces.new([bm.verts.new((x, y, z0)) for x, y in pts])
    ex = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, 0, z1 - z0), verts=[e for e in ex["geom"] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def frame_bm(half_out, half_in, r_out, z0, z1, n=4):
    """A rounded square frame: the outer rounded rect minus the inner."""
    outer = rounded_rect_bm(half_out, r_out, z0, z1, n)
    inner = rounded_rect_bm(half_in, max(.05, r_out - (half_out - half_in)), z0 - .1, z1 + .1, n)
    return boolean(outer, [inner])


def build_marks(col):
    # the attack warning: the old 9.7 m red plate, its corners rounded, a brighter raised rim (slot brass)
    w = Builder()
    w.add(rounded_rect_bm(4.5, .45, .009, .045), "wax")   # bottom off the rim's .005 (no shared plane)
    w.add(frame_bm(4.85, 4.45, .6, .005, .06), "brass")
    warn = w.obj("RlChessWarning", col)
    # the occupied border: the old four gold bars (.35 wide, inset .2) as one brass frame, rounded, studs at corners
    o = Builder()
    fr = frame_bm(4.8, 4.45, .35, .03, .15)
    bmesh.ops.bevel(fr, geom=[e for e in fr.edges if all(v.co.z > .149 for v in e.verts)], offset=.06, segments=2,
                    profile=.5, affect="EDGES", clamp_overlap=True)
    o.add(fr, "brass")
    for sx in (-1, 1):
        for sy in (-1, 1):
            s = lathe_bm([(0, .03), (.32, .03), (.3, .14)] + arc(0, .14, .3, 0, 90, 3)[1:], 16)
            bmesh.ops.scale(s, vec=(1, 1, .2), verts=s.verts); bmesh.ops.translate(s, vec=(0, 0, .12), verts=s.verts)
            o.add(s, "brass", Matrix.Translation((sx * 4.62, sy * 4.62, 0)))
    occ = o.obj("RlChessOccupied", col)
    return [warn, occ]


def deck(col, depth, name):
    """Walnut planks 1.6 m wide running along the walk (y 0 .. -depth), a brass nosing at the far end, rails at the
    sides continuing the board's, tops at 0."""
    b = Builder()
    n = 50
    pw = 80.0 / n
    for i in range(n):
        x0 = -40 + i * pw
        stagger = (i % 3) * depth / 3 if depth > 12 else 0
        end = depth - .5          # planks stop where the brass nosing starts (no shared faces: Unity z-fights them)
        cuts = sorted({0, end} | ({min(stagger, end), (stagger + depth / 2) % depth} if depth > 12 else set()))
        cuts = [c for c in cuts if c <= end]
        for a, c in zip(cuts, cuts[1:]):
            if c - a < .5:
                continue
            b.add(box_bm((x0 + .02, -c + .02, -.78), (x0 + pw - .02, -a - .02, 0), .07, 1, top_only=True),
                  "walnut")
    b.add(box_bm((-40, -depth - .02, -.82), (40, -depth + .5, .0), .1, 2), "brass")
    for sx in (-1, 1):
        x0, x1 = sorted((sx * 40, sx * 40.95))
        b.add(box_bm((x0, -depth, -.8), (x1, 0, -.04), .12, 2), "varnished")
    return [b.obj(name, col, weld=False)]


def build_decks(col):
    return deck(col, 8, "RlChessDeck8") + deck(col, 20, "RlChessDeck20")


def build_finish(col):
    b = Builder()
    n = 80
    for i in range(n):
        for row in range(2):
            x0 = -40 + i * 1.0
            b.add(box_bm((x0, -.5 + row * .5, -.02), (x0 + 1, row * .5, .006)), "marble" if (i + row) % 2 == 0 else "varnished")
    for y in (-.5, .5):
        b.add(box_bm((-40, y - .06, -.016), (40, y + .06, .012), .01), "brass")   # bottom off the cells' -.02
    return [b.obj("RlChessFinish", col, weld=False)]


def build_arch(col):
    """Exit arch (D4): walnut pillars on cream stone bases with stone capitals and brass balls, a walnut header with a
    cream stone arch under it and a parchment sign plaque. Origin = foot centre; pillars at x +-12 (2 x 2 m boxes
    0 .. 10), header 26 x 2 x 2 at z 9 .. 11. The front (toward the board) is -y; the code's sign text floats at
    y -1.1, the plaque's face is at y -1.04."""
    b = Builder()
    for sx in (-1, 1):
        x = sx * 12
        b.add(box_bm((x - 1.35, -1.35, 0), (x + 1.35, 1.35, 1.2), .12, 2), "marble")
        b.add(box_bm((x - 1.05, -1.05, 1.2), (x + 1.05, 1.05, 8.2), .1, 2), "varnished")
        for z in (1.5, 7.6):
            b.add(box_bm((x - 1.12, -1.12, z), (x + 1.12, 1.12, z + .3), .06), "brass")
        b.add(box_bm((x - 1.3, -1.3, 8.2), (x + 1.3, 1.3, 9.0), .12, 2), "marble")
        s = bmesh.new(); bmesh.ops.create_uvsphere(s, u_segments=20, v_segments=12, radius=.95)
        for f in s.faces:
            f.smooth = True
        b.add(s, "brass", Matrix.Translation((x, 0, 12.0)))
        b.add(lathe_bm([(0, 11.0), (.75, 11.0), (.75, 11.15), (.45, 11.3), (0, 11.3)], 20), "brass",
              Matrix.Translation((x, 0, 0)))
    # header beam and its cornice
    b.add(box_bm((-13.3, -1.04, 9.0), (13.3, 1.04, 11.0), .14, 2), "varnished")
    b.add(box_bm((-13.6, -1.25, 10.7), (13.6, 1.25, 11.05), .1, 2), "walnut")
    # the arch (D4): a cream stone ring springing from the pillars' inner faces at 5.0, its crown at 8.4, a walnut
    # spandrel filling up to the header, a stone keystone
    def band(lo, hi, y0, y1):
        bm = bmesh.new()
        vs = [[bm.verts.new((x, y, z)) for y in (y0, y1) for (x, z) in (l, h)] for l, h in zip(lo, hi)]
        for a, c in zip(vs, vs[1:]):            # a[0] lo front, a[1] hi front, a[2] lo back, a[3] hi back
            bm.faces.new((a[0], c[0], c[1], a[1])); bm.faces.new((a[2], a[3], c[3], c[2]))
            bm.faces.new((a[0], a[2], c[2], c[0])); bm.faces.new((a[1], c[1], c[3], a[3]))
        for e in (vs[0], vs[-1]):
            bm.faces.new((e[0], e[1], e[3], e[2]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return bm
    N = 24
    ring_in, ring_out, top = [], [], []
    for i in range(N + 1):
        a = math.pi * i / N
        ring_in.append((11.0 * math.cos(a), 5.0 + 3.4 * math.sin(a)))
        ring_out.append((11.85 * math.cos(a), 5.0 + 4.25 * math.sin(a)))
        top.append((11.85 * math.cos(a), 9.3))
    b.add(band(ring_in, ring_out, -1.0, 1.0), "marble")
    b.add(band(ring_out, top, -.85, .85), "varnished")
    b.add(box_bm((-.75, -1.12, 7.9), (.75, 1.12, 9.25), .1, 2), "marble")       # keystone
    # the sign plaque: parchment on a brass frame, its face 6 cm behind the code's text
    b.add(box_bm((-6.2, -1.12, 9.2), (6.2, -1.0, 10.8), .05), "brass")
    b.add(box_bm((-5.9, -1.14, 9.35), (5.9, -1.02, 10.65), .03), "parchment")
    return [b.obj("RlChessArch", col, weld=False)]


BUILD = dict(PIECES, board=build_board, marks=build_marks, decks=build_decks, finish=build_finish, arch=build_arch)
ORDER = ["board", "marks", "decks", "finish", "arch", "pawn", "knight", "bishop", "rook", "queen", "king"]


# ------------------------------------------------------------------ stills
def piece_still(objs, out):
    """The ivory piece and its ebony twin on white, front view a little from above, like the D6 sheet."""
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x, sc.render.resolution_y = 1400, 900
    sc.render.film_transparent = False
    w = bpy.data.worlds.get("r6w") or bpy.data.worlds.new("r6w"); sc.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (1, 1, 1, 1); bg.inputs["Strength"].default_value = .55
    if not bpy.data.objects.get("r6 backdrop"):   # a white card behind (the world is dimmed to light the pieces)
        bd = bmesh.new(); bmesh.ops.create_grid(bd, x_segments=1, y_segments=1, size=60)
        bo = temp_obj(bd, "r6 backdrop"); bo.rotation_euler = (math.radians(90), 0, 0); bo.location = (0, 30, 0)
        em = bpy.data.materials.new("r6 backdrop"); em.use_nodes = True
        nt = em.node_tree; nt.nodes.clear(); mo = nt.nodes.new("ShaderNodeOutputMaterial")
        e = nt.nodes.new("ShaderNodeEmission"); e.inputs["Strength"].default_value = 1.0
        nt.links.new(e.outputs[0], mo.inputs[0]); bo.data.materials.append(em)
    look = bpy.data.collections.get("r6 look")
    if look:
        for o in list(look.objects):
            if o.type == "MESH":
                drop(o)
    else:
        look = bpy.data.collections.new("r6 look"); sc.collection.children.link(look)

    def m(name, col, rough, metal=0.0):
        mm = bpy.data.materials.get(name) or bpy.data.materials.new(name); mm.use_nodes = True
        p = next(n for n in mm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        p.inputs["Base Color"].default_value = col; p.inputs["Roughness"].default_value = rough
        p.inputs["Metallic"].default_value = metal
        return mm
    ivory, ebony = m("look ivory", (.72, .55, .33, 1), .3), m("look ebony", (.018, .011, .008, 1), .25)
    brass = m("look brass", (.85, .55, .18, 1), .28, 1.0)
    eye = m("look eye", (.06, .03, .015, 1), .15)
    copies = []
    for dx, body in ((-4.5, ivory), (4.5, ebony)):
        for o in objs:
            c = o.copy(); c.data = o.data.copy(); look.objects.link(c)
            if o.parent is None:
                c.matrix_world = Matrix.Translation((dx, 0, 0)) @ Matrix.Rotation(math.radians(-90 if dx < 0 else -90), 4, "Z") @ o.matrix_world
            for s in c.material_slots:
                n = s.material.name if s.material else ""
                s.material = brass if "brass" in n else eye if "varnished" in n else body
            if o.name == "Cross" and body is ebony:
                for s in c.material_slots:
                    s.material = brass
            copies.append(c)
        # re-parent the cross copies onto their king copies
    kings = [c for c in copies if c.name.startswith("RlChessKing")]
    crosses = [c for c in copies if c.name.startswith("Cross")]
    for kc, cc in zip(kings, crosses):
        cc.parent = kc; cc.matrix_parent_inverse = Matrix.Identity(4)
    for o in bpy.data.objects:
        if o not in copies and o.type == "MESH" and o.name != "r6 backdrop":
            o.hide_render = True
    for o in copies:
        o.hide_render = False
    for name, energy, rot, size in (("key", 450, (math.radians(55), 0, math.radians(-35)), 8),
                                    ("fill", 120, (math.radians(70), 0, math.radians(60)), 12)):
        L = bpy.data.objects.get("r6 " + name)
        if L is None:
            L = bpy.data.objects.new("r6 " + name, bpy.data.lights.new("r6 " + name, "AREA")); look.objects.link(L)
        L.data.energy = energy * 10; L.data.size = size
        L.rotation_euler = rot
        L.location = Matrix.Rotation(rot[2], 3, "Z") @ Matrix.Rotation(rot[0], 3, "X") @ Vector((0, 0, 30))
    cam = bpy.data.objects.get("r6 cam")
    if cam is None:
        cam = bpy.data.objects.new("r6 cam", bpy.data.cameras.new("r6 cam")); look.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = 19
    tgt = Vector((0, 0, 4.4))
    cam.location = tgt + Vector((0, -60, 12))
    cam.rotation_euler = (tgt - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = 500
    os.makedirs(STILLS, exist_ok=True)
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    for o in copies:
        drop(o)
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = False
    print("[r6] still", out)


def lookdev(fbx, out, view, scale):
    import subprocess
    exe = bpy.app.binary_path
    subprocess.run([exe, "-b", "--factory-startup", "--python", os.path.join(HERE, "rl_lookdev.py"), "--",
                    fbx, out, view, str(scale)], check=False)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["all"]
    parts = ORDER if argv[0] == "all" else argv[0].split(",")
    export = "--no-export" not in argv
    stills = "--stills" in argv
    if "--preview" in argv:
        k.ROOT = os.path.abspath(argv[argv.index("--preview") + 1])
    if os.path.exists(BLEND):
        bpy.ops.wm.open_mainfile(filepath=BLEND)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    for p in parts:
        col = collection("R6 " + p)
        objs = BUILD[p](col)
        for o in objs:
            tris = sum(len(f.vertices) - 2 for f in o.data.polygons)
            print("[r6]", p, o.name, tris, "tris")
        if export:
            for o in objs:
                if o.parent is not None:
                    continue
                sub = bpy.data.collections.new("x " + o.name); bpy.context.scene.collection.children.link(sub)
                sub.objects.link(o)
                for c in o.children:
                    sub.objects.link(c)
                try:
                    k.export(sub.name, o.name, ZONE)
                finally:
                    for c in list(sub.objects):
                        sub.objects.unlink(c)
                    bpy.data.collections.remove(sub)
        if stills and p in PIECES:
            piece_still(objs, os.path.join(STILLS, p + ".png"))
        elif stills and export:
            fbx = os.path.join(k.ROOT, k.OUT_REL, ZONE, objs[0].name + ".fbx")
            lookdev(fbx, os.path.join(STILLS, p + ".png"), "-.6,-1,.5", 0)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("[r6] saved", BLEND)


main()
