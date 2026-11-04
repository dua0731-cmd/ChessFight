# R342 (승규 11-04, a Unity shot of beat 6 still in blockout: blue tiled side walls, grey knight alcoves, the kit clock
# gate): "대계단 홀 지오메트리 적용해서 모델링 해줘 완전 고퀄리티로". The royal balcony (Unity x -10.6 .. 10.6, z 121 ..
# 129, y 8 .. 27.8) modelled in the hall's language - cream palace stone (Palace_Stone, world space in Unity), real
# gold mouldings (Palace_Gold), crimson velvet, walnut, ivory enamel - over the blockout, which keeps its colliders:
#   Balcony_side_W/E      the side walls as the jambs of the round wall's north opening, up to its top (26): an arched
#                         niche over each knight alcove (gold bead, velvet back, a small crown), its archivolt, imposts
#                         and keystone, fluted pilasters, architrave + gold bead + dentils + cornice round to the hall
#   Knight_alcove_W/E     the guard knights' plinths: base, gold fillet and cornice, marble fields in gold beads with a
#                         velvet lozenge and a gold rosette
#   Gate_facade           the clock gate's front: two rusticated pylons, fluted gold-capitalled pilasters, the door
#                         frame, the door tracks (jambs and a mullion that keep clear of the doors), and over the opening
#                         a hollow lintel the doors rise into (they go up 4.1 m to y 23.3; the hollow is 23.6 high);
#                         two clocks in gold frames, a velvet cartouche with a gold chess king, a chessboard frieze,
#                         two cornices, a segmental pediment with a gold sunburst and the royal crown, two urn lanterns
#   Gate_transom          gilded sunburst grilles over the doors (y 19.32 .. 20.3, in front of the doors' plane)
#   Balcony_front         the balcony's face to the hall: consoles, framed panels, gold beads, a soffit moulding
#   Balcony_lantern_flame_W/E   the lanterns' glowing flames (Unity puts a warm light in each)
# and the pieces that move with the obstacles (RoyalBalconyMovers.fbx, Unity-local frames, no pivot):
#   Door                  one walnut door (3.8 x 3.2 x 0.6 like the kit's panel) with raised panels, gold beads,
#                         studs, corner straps and a boss, both faces; Unity hangs it on "Left gate" and "Right gate"
#   Hand                  a blued-steel clock hand (spear tip, moon ring, tail) with a gold cap, on each "Clock hand"
# Frames: Blender (x, y, z) = Unity section (x, z, y). The static FBX goes under the hall's 180-degree pivot as the hall's
# does; the movers are built in that frame too and turned 180 degrees about z before their export, so they need none.
# Everything stays clear of what moves: the doors' channel (x +-3.98, z 127.97 .. 128.63 with their mouldings), the
# hands (z 127.455 .. 127.512, r < 0.62) and the guard knights (Unity: FallingKnight at 1.3x, r 1.17 round x +-6.6).
# Run: blender -b grand_stair_hall_v33C.blend -P royal_balcony_v34.py
#   -> grand_stair_hall_v34C.blend, renders_v41/*.png, Assets/Resources/PawnRushGrandStairHall/RoyalBalconyV34.fbx,
#      RoyalBalconyMovers.fbx, RoyalBalconyV34_Hidden.txt
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
TAG = "RoyalBalconyV34"
sc = bpy.context.scene
PI = math.pi


# ------------------------------------------------------------------ materials (Unity dresses them by name)
def material(name, rgba, metal=0., rough=.5, emit=None):
    m = bpy.data.materials.get(name)
    if m is not None:
        return m                                # the hall's own (Palace_Stone, Palace_Gold) as they are
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metal
        bsdf.inputs["Roughness"].default_value = rough
        if emit:
            bsdf.inputs["Emission Color"].default_value = emit
            bsdf.inputs["Emission Strength"].default_value = 6.
    m.diffuse_color = rgba
    return m


STONE = material("Palace_Stone", (.90, .84, .72, 1), 0, .7)
GOLD = material("Palace_Gold", (.86, .64, .28, 1), .9, .3)
VELVET = material("Balcony_Velvet", (.42, .03, .06, 1), 0, .8)
WALNUT = material("Balcony_Walnut", (.30, .17, .10, 1), 0, .45)
IVORY = material("Balcony_Ivory", (.94, .90, .81, 1), 0, .25)
EBONY = material("Balcony_Ebony", (.07, .06, .07, 1), 0, .2)
STEEL = material("Balcony_Steel", (.07, .09, .16, 1), .9, .2)
RUBY = material("Balcony_Ruby", (.70, .04, .06, 1), 0, .3)
BULB = material("Balcony_Bulb", (1., .86, .6, 1), 0, .3, (1., .74, .45, 1))
MARBLE = material("Marble_Balcony", (.86, .85, .83, 1), 0, .25)


def P(x, y, z):
    return Vector((x, y, z))


# ------------------------------------------------------------------ primitives: each a small closed bmesh
def tidy(bm):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def box(lo, hi, bevel=0., segments=2):
    lo, hi = Vector([min(a, b) for a, b in zip(lo, hi)]), Vector([max(a, b) for a, b in zip(lo, hi)])
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    for v in bm.verts:
        v.co = Vector([lo[i] + (v.co[i] + .5) * (hi[i] - lo[i]) for i in range(3)])
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, offset_type="OFFSET", segments=segments, profile=.5,
                        affect="EDGES", clamp_overlap=True)
    return bm


def sweep(path, outs, ups, prof, closed=False):
    """A profile (a along `outs`, b along `ups`, a closed polygon) swept along a path, capped at open ends."""
    bm = bmesh.new()
    rings = [[bm.verts.new(p + o * a + u * b) for a, b in prof] for p, o, u in zip(path, outs, ups)]
    n, m = len(prof), len(rings)
    for i in range(m if closed else m - 1):
        r0, r1 = rings[i], rings[(i + 1) % m]
        for j in range(n):
            k = (j + 1) % n
            bm.faces.new((r0[j], r0[k], r1[k], r1[j]))
    if not closed:
        bm.faces.new(rings[0])
        bm.faces.new(rings[-1][::-1])
    return bm


def mitre(seg_outs, n, closed=False):
    """Per-vertex offsets from per-segment out vectors, so a constant offset keeps parallel to every segment."""
    outs = []
    for i in range(n):
        if closed:
            a, b = seg_outs[i - 1], seg_outs[i % len(seg_outs)]
        elif i == 0:
            outs.append(seg_outs[0]); continue
        elif i == n - 1:
            outs.append(seg_outs[-1]); continue
        else:
            a, b = seg_outs[i - 1], seg_outs[i]
        outs.append((a + b) / max(1 + a.dot(b), 1e-3))
    return outs


def run(pts, seg_outs, z, prof, up=Vector((0, 0, 1))):
    """A horizontal moulding along a polyline of (x, y) at height z, `seg_outs` its segments' faces' normals."""
    path = [P(x, y, z) for x, y in pts]
    outs = mitre([Vector((o[0], o[1], 0)) for o in seg_outs], len(path))
    return sweep(path, outs, [up] * len(path), prof)


def outline(poly, origin, U, V, N, prof, closed=True):
    """A moulding round a polygon (u, v) drawn on a face (origin + U u + V v, N its outward normal): the profile's a
    along N, its b outward in the face, away from the polygon's inside."""
    area = sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))
    sign = 1 if area > 0 else -1
    k = len(poly) if closed else len(poly) - 1
    segs = []
    for i in range(k):
        a, b = Vector(poly[i]), Vector(poly[(i + 1) % len(poly)])
        d = (b - a).normalized()
        segs.append(Vector((d.y, -d.x)) * sign)
    ins = mitre(segs, len(poly), closed)
    path = [origin + U * u + V * v for u, v in poly]
    ups = [U * n.x + V * n.y for n in ins]
    return sweep(path, [N] * len(path), ups, prof, closed)


def extrude(poly, origin, U, V, N, d0, d1):
    """A polygon (u, v) on a face, from d0 to d1 along N."""
    bm = bmesh.new()
    a = [bm.verts.new(origin + U * u + V * v + N * d0) for u, v in poly]
    b = [bm.verts.new(origin + U * u + V * v + N * d1) for u, v in poly]
    bm.faces.new(a)
    bm.faces.new(b[::-1])
    for i in range(len(poly)):
        j = (i + 1) % len(poly)
        bm.faces.new((a[i], a[j], b[j], b[i]))
    return bm


def basis(axis):
    axis = Vector(axis).normalized()
    e1 = Vector((1, 0, 0)) if abs(axis.x) < .9 else Vector((0, 1, 0))
    e1 = (e1 - axis * e1.dot(axis)).normalized()
    return axis, e1, axis.cross(e1)


def lathe(prof, centre, axis=(0, 0, 1), segs=24, a0=0., a1=2 * PI, closed_prof=False, e1=None):
    """A profile (r, h) turned round `axis` through `centre`; a part turn is capped at both cut planes."""
    axis, b1, b2 = basis(axis)
    if e1 is not None:
        b1 = Vector(e1).normalized(); b2 = axis.cross(b1)
    full = abs(a1 - a0 - 2 * PI) < 1e-6
    count = segs if full else segs + 1
    bm = bmesh.new()
    rings = []
    for i in range(count):
        t = a0 + (a1 - a0) * i / segs
        d = b1 * math.cos(t) + b2 * math.sin(t)
        rings.append([bm.verts.new(centre + axis * h + d * r) for r, h in prof])
    n = len(prof)
    for i in range(segs):
        r0, r1 = rings[i], rings[(i + 1) % count]
        for j in range(n if closed_prof else n - 1):
            k = (j + 1) % n
            bm.faces.new((r0[j], r0[k], r1[k], r1[j]))
    if not full:
        if not closed_prof:
            bm.faces.new(rings[0]); bm.faces.new(rings[-1][::-1])
        else:
            bm.faces.new(rings[0]); bm.faces.new(rings[-1][::-1])
    elif closed_prof is False and (prof[0][0] > 1e-6 or prof[-1][0] > 1e-6):
        pass                                    # an open tube: its rims stay open (always against something)
    return bm


def circle(ca, cb, r, n=8):
    return [(ca + r * math.cos(2 * PI * i / n), cb + r * math.sin(2 * PI * i / n)) for i in range(n)]


def arc(cx, cy, r, t0, t1, n):
    return [(cx + r * math.cos(t0 + (t1 - t0) * i / n), cy + r * math.sin(t0 + (t1 - t0) * i / n)) for i in range(n + 1)]


# ------------------------------------------------------------------ profiles (a = out from the face, b = up / outward)
def scaled(pts, sa, sb):
    return [(a * sa, b * sb) for a, b in pts]


def ogee(a0, b0, a1, b1, n=6):
    """A cyma recta from (a0, b0) to (a1, b1): concave below, convex above."""
    out = []
    for i in range(1, n):
        t = i / n
        s = .5 - .5 * math.cos(PI * t)
        out.append((a0 + (a1 - a0) * s, b0 + (b1 - b0) * t))
    return out


CORNICE = ([(-.02, 0), (.05, 0), (.05, .04), (.09, .06), (.1, .1), (.14, .12)] + ogee(.14, .12, .3, .32)
           + [(.3, .32), (.36, .34), (.36, .44), (.32, .46), (.32, .5), (-.02, .5)])
BASE = ([(-.02, 0), (.14, 0), (.14, .14)] + [(.10 + .045 * math.cos(t), .19 + .05 * math.sin(t)) for t in
        [-PI / 2 + PI * i / 6 for i in range(7)]] + [(.07, .25), (.055, .3), (.06, .34), (.04, .37), (.04, .45), (-.02, .45)])
ARCHITRAVE = [(-.02, 0), (.04, 0), (.04, .12), (.06, .13), (.06, .25), (.09, .27), (.1, .4), (-.02, .4)]
STRING = [(-.02, 0), (.06, 0), (.08, .03), (.12, .08), (.14, .16), (.14, .24), (.1, .28), (.1, .32), (-.02, .32)]
BAND = [(-.02, 0), (.05, 0), (.05, .04), (.09, .06), (.1, .1), (.1, .24), (.07, .28), (-.02, .28)]
BEAD = circle(.03, 0, .03, 8)
FINE = circle(.016, 0, .016, 6)


def rosette(centre, normal, r):
    """A gold rosette: a ring of petals round a boss, turned about the face's normal."""
    s = r / .2
    return lathe(scaled([(0, 0), (.2, 0), (.2, .012), (.17, .03), (.14, .02), (.11, .035), (.08, .03), (.06, .055),
                         (0, .065)], s, s), centre, normal, segs=20)


# ------------------------------------------------------------------ parts: one object per material set
class Part:
    def __init__(self, name):
        self.name, self.bm, self.mats = name, bmesh.new(), []

    def add(self, bm, mat):
        tidy(bm)
        if mat not in self.mats:
            self.mats.append(mat)
        for f in bm.faces:
            f.material_index = self.mats.index(mat)
        me = bpy.data.meshes.new("tmp")
        bm.to_mesh(me); bm.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def done(self, coll, tag=TAG):
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me); self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(32))
        ob = bpy.data.objects.new(self.name, me)
        coll.objects.link(ob)
        ob["own_fbx"] = tag
        print("PART", self.name, len(me.polygons), "faces")
        return ob


coll = bpy.data.collections.get("Royal_balcony_v34") or bpy.data.collections.new("Royal_balcony_v34")
if coll.name not in sc.collection.children:
    sc.collection.children.link(coll)
for o in list(coll.objects):
    bpy.data.objects.remove(o)

X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))

# ------------------------------------------------------------------ the side walls with their niches
GATE_FRONT, GATE_BACK = 127.62, 128.98
NY0, NY1, NR = 122.35, 127.25, 2.45        # the niche: 4.9 wide round y 124.8
NYC, SILL, SPRING = (122.35 + 127.25) / 2, 19.0, 20.8
APEX = SPRING + NR


def side_wall(s):
    p = Part("Balcony_side_" + ("W" if s < 0 else "E"))
    out = X_ * -s                                  # the inner face looks into the balcony
    p.add(box(P(s * 8.45, 121.0, 8.0), P(s * 10.6, 129.0, 26.0), .02), STONE)
    p.add(box(P(s * 8.0, 121.0, 8.0), P(s * 8.45, 129.0, SILL)), STONE)
    p.add(box(P(s * 8.0, 121.0, SILL), P(s * 8.45, NY0, 26.0)), STONE)
    p.add(box(P(s * 8.0, NY1, SILL), P(s * 8.45, 129.0, 26.0)), STONE)
    over = [(NY0, SPRING), (NY0, 26.0), (NY1, 26.0), (NY1, SPRING)] + arc(NYC, SPRING, NR, 0, PI, 40)[1:-1]
    p.add(extrude(over, P(s * 8.0, 0, 0), Y_, Z_, X_ * s, 0, .45), STONE)
    # The niche's back: velvet in a stone margin, a gold bead round it, a small crown high up.
    m = .22
    back = [(NY0 + m, SILL + m), (NY1 - m, SILL + m), (NY1 - m, SPRING)] + arc(NYC, SPRING, NR - m, 0, PI, 36)[1:-1] + [(NY0 + m, SPRING)]
    p.add(extrude(back, P(s * 8.45, 0, 0), Y_, Z_, out, 0, .012), VELVET)
    p.add(outline(back, P(s * 8.438, 0, 0), Y_, Z_, out, BEAD), GOLD)
    crown(p, P(s * 8.18, NYC, APEX - .78), .42)
    # The archivolt: a stone band with a gold bead along the opening, down the jambs to the sill.
    path = [(NY0, SILL), (NY0, SPRING)] + arc(NYC, SPRING, NR, PI, 0, 48) + [(NY1, SILL)]
    face = P(s * 8.0, 0, 0)
    ups = [-Y_, -Y_] + [Y_ * math.cos(t) + Z_ * math.sin(t) for t in [PI - PI * i / 48 for i in range(49)]] + [Y_]
    pts = [face + Y_ * y + Z_ * z for y, z in path]
    p.add(sweep(pts, [out] * len(pts), ups, BAND), STONE)
    p.add(sweep(pts, [out] * len(pts), ups, scaled(circle(.06, .0, .035, 8), 1, 1)), GOLD)
    # Imposts at the springing and a keystone with a rosette at the crown.
    for y0, y1 in ((NY0 - .34, NY0 + .06), (NY1 - .06, NY1 + .34)):
        p.add(box(P(s * 7.84, y0, SPRING - .2), P(s * 8.0, y1, SPRING), .015), STONE)
        p.add(box(P(s * 7.83, y0 - .01, SPRING - .24), P(s * 8.0, y1 + .01, SPRING - .2)), GOLD)
    key = [(NYC - .2, APEX - .06), (NYC + .2, APEX - .06), (NYC + .3, APEX + .6), (NYC - .3, APEX + .6)]
    p.add(extrude(key, face, Y_, Z_, out, 0, .2), STONE)
    p.add(rosette(P(s * 7.8, NYC, APEX + .28), out, .1), GOLD)
    # Gold roundels in the spandrels.
    for y in (NYC - NR * .8, NYC + NR * .8):
        p.add(lathe([(0, 0), (.22, 0), (.22, .02), (0, .02)], P(s * 8.0, y, APEX + .35), out, 24), VELVET)
        p.add(lathe(circle(.25, .02, .03, 8), P(s * 8.0, y, APEX + .35), out, 32, closed_prof=True), GOLD)
        p.add(rosette(P(s * 7.98, y, APEX + .35), out, .1), GOLD)
    # A fluted pilaster at the balcony's front corner (inner face) and a broad one on the face to the hall.
    pilaster(p, s, face_x=8.0, out=out, y0=121.05, y1=121.85, z0=16.0, z1=24.75, proj=.12)
    for z0, z1 in ((16.0, 24.75),):
        p.add(box(P(s * 8.25, 120.86, z0 + .45), P(s * 10.35, 121.0, z1 - .5), .01), STONE)
        p.add(run([(s * 8.15, 121.0), (s * 10.45, 121.0)], [-Y_], z0, BASE), STONE)
        for i in range(5):
            x = s * (8.6 + i * .35)
            p.add(box(P(x - .02, 120.85, z0 + .7), P(x + .02, 120.86, z1 - .75)), GOLD)
        p.add(box(P(s * 8.18, 120.8, z1 - .5), P(s * 10.42, 121.0, z1 - .34)), GOLD)
        p.add(box(P(s * 8.12, 120.76, z1 - .34), P(s * 10.48, 121.0, z1 - .2)), GOLD)
    p.add(run([(s * 8.0, 121.0), (s * 10.6, 121.0)], [-Y_], 15.52, STRING), STONE)
    p.add(run([(s * 8.0, 121.0), (s * 10.6, 121.0)], [-Y_], 8.4, scaled(ARCHITRAVE, 1, -1)), STONE)
    # The entablature round the inner face and the face to the hall, up to the round wall's top.
    corner = [(s * 8.0, GATE_FRONT), (s * 8.0, 121.0), (s * 10.6, 121.0)]
    outs = [out, -Y_]
    p.add(run(corner, outs, 24.75, ARCHITRAVE), STONE)
    p.add(run(corner, outs, 25.17, BEAD), GOLD)
    p.add(run(corner, outs, 25.5, CORNICE), STONE)
    for y in [GATE_FRONT - .2 - .22 * i for i in range(30)]:
        if y < 121.1:
            break
        p.add(box(P(s * 7.9, y - .07, 25.36), P(s * 8.0, y + .07, 25.5)), STONE)
    for x in [8.12 + .22 * i for i in range(12)]:
        p.add(box(P(s * (x - .07), 120.9, 25.36), P(s * (x + .07), 121.0, 25.5)), STONE)
    return p


def pilaster(p, s, face_x, out, y0, y1, z0, z1, proj):
    """A fluted pilaster on a wall face x = s face_x (out its normal), y0 .. y1 wide, with a moulded base and a gold
    capital."""
    fx = s * face_x
    p.add(box(P(fx - s * proj, y0, z0 + .45), P(fx, y1, z1 - .55), .01), STONE)
    p.add(box(P(fx - s * (proj + .1), y0 - .08, z0), P(fx, y1 + .08, z0 + .28), .02), STONE)
    p.add(box(P(fx - s * (proj + .06), y0 - .05, z0 + .28), P(fx, y1 + .05, z0 + .45), .02), STONE)
    p.add(box(P(fx - s * (proj + .07), y0 - .06, z0 + .4), P(fx, y1 + .06, z0 + .46)), GOLD)
    n = 3
    for i in range(n):
        y = y0 + (y1 - y0) * (i + 1) / (n + 1)
        p.add(box(P(fx - s * (proj + .006), y - .02, z0 + .7), P(fx - s * proj, y + .02, z1 - .8)), GOLD)
    p.add(box(P(fx - s * (proj + .03), y0 - .02, z1 - .55), P(fx, y1 + .02, z1 - .48)), GOLD)
    p.add(box(P(fx - s * (proj + .05), y0 - .04, z1 - .48), P(fx, y1 + .04, z1 - .2)), GOLD)
    for y in (y0 + .04, y1 - .04):
        p.add(lathe([(0, 0), (.07, 0), (.07, .03), (0, .03)], P(fx - s * (proj + .05), y, z1 - .26), out, 16), GOLD)
    p.add(box(P(fx - s * (proj + .1), y0 - .09, z1 - .2), P(fx, y1 + .09, z1)), GOLD)


def crown(p, base, k, jewels=True):
    """The royal crown (gold band and points, velvet cap, two arches, an orb and cross), base at its bottom middle."""
    def L(prof, h=0., segs=40, closed=False, mat=GOLD):
        p.add(lathe(scaled(prof, k, k), base + Z_ * h * k, Z_, segs, closed_prof=closed), mat)
    L([(.42, 0), (.47, 0), (.48, .04), (.47, .08), (.47, .24), (.49, .27), (.48, .3), (.42, .3)], closed=True)
    L([(0, .18), (.41, .18), (.40, .36), (.33, .56), (.2, .68), (0, .72)], mat=VELVET)
    L(circle(.47, .02, .025, 6), 0, 40, True)
    L(circle(.48, .29, .02, 6), 0, 40, True)
    for i in range(8):
        t = 2 * PI * i / 8
        d = Vector((math.cos(t), math.sin(t), 0))
        e = Vector((-math.sin(t), math.cos(t), 0))
        point = [(-.1, 0), (.1, 0), (.07, .2), (.035, .28), (0, .4), (-.035, .28), (-.07, .2)]
        p.add(extrude(scaled(point, k, k), base + d * .45 * k + Z_ * .27 * k, e, Z_, d, -.022 * k, .022 * k), GOLD)
        p.add(lathe(scaled([(0, -.045), (.03, -.035), (.045, 0), (.03, .035), (0, .045)], k, k),
                    base + d * .45 * k + Z_ * .71 * k, Z_, 12), MARBLE)
        if jewels:
            tj = t + PI / 8
            dj = Vector((math.cos(tj), math.sin(tj), 0))
            p.add(lathe(scaled([(0, 0), (.045, 0), (.04, .02), (.02, .035), (0, .04)], k, k),
                        base + dj * .47 * k + Z_ * .15 * k, dj, 12), RUBY if i % 2 else EBONY)
    for t0 in (0, PI / 2):
        d = Vector((math.cos(t0), math.sin(t0), 0))
        pts = [base + (d * .44 * math.cos(t) + Z_ * (.3 + .52 * math.sin(t))) * k for t in [PI * i / 24 for i in range(25)]]
        ups = [(d * math.cos(t) + Z_ * math.sin(t)).normalized() for t in [PI * i / 24 for i in range(25)]]
        side = d.cross(Z_)
        p.add(sweep(pts, [side] * 25, ups, scaled([(-.03, -.02), (.03, -.02), (.03, .02), (-.03, .02)], k, k)), GOLD)
    L([(0, -.1), (.06, -.085), (.095, -.04), (.1, 0), (.085, .05), (.05, .085), (0, .1)], .92, 20)
    p.add(box(base + P(-.02, -.02, 1.0) * k, base + P(.02, .02, 1.24) * k), GOLD)
    p.add(box(base + P(-.075, -.02, 1.1) * k, base + P(.075, .02, 1.145) * k), GOLD)


# ------------------------------------------------------------------ the knight alcoves
def alcove(s):
    p = Part("Knight_alcove_" + ("W" if s < 0 else "E"))
    p.add(box(P(s * 5.2, 121.9, 16.0), P(s * 8.0, 127.7, 19.0), .015), STONE)
    edge = [(s * 8.0, 121.9), (s * 5.2, 121.9), (s * 5.2, GATE_FRONT)]
    outs = [-Y_, X_ * -s]
    p.add(run(edge, outs, 16.0, BASE), STONE)
    p.add(run(edge, outs, 18.42, FINE), GOLD)
    p.add(run(edge, outs, 18.5, scaled(CORNICE, .55, 1.0)), STONE)
    # The inner face's long panel and the south face's short one: marble in a gold bead, a velvet lozenge, a rosette.
    for origin, U, out, u0, u1 in ((P(s * 5.2, 0, 0), Y_, X_ * -s, 122.45, 127.15),
                                   (P(0, 121.9, 0), X_, -Y_, min(s * 5.6, s * 7.6), max(s * 5.6, s * 7.6))):
        rect = [(u0, 16.78), (u1, 16.78), (u1, 18.12), (u0, 18.12)]
        p.add(extrude(rect, origin, U, Z_, out, 0, .025), MARBLE)
        p.add(outline(rect, origin + out * .01, U, Z_, out, BEAD), GOLD)
        um, vm = (u0 + u1) / 2, 17.45
        w = min(.6, (u1 - u0) * .3)
        loz = [(um - w, vm), (um, vm - .5), (um + w, vm), (um, vm + .5)]
        p.add(extrude(loz, origin, U, Z_, out, .02, .045), VELVET)
        p.add(outline(loz, origin + out * .045, U, Z_, out, FINE), GOLD)
        p.add(rosette(origin + U * um + Z_ * vm + out * .045, out, .16), GOLD)
    return p


# ------------------------------------------------------------------ the clock gate
DOOR_X, OPEN_TOP = 4.02, 20.3                     # the opening's sides and top; the kit's doors are x +-3.98
CLOCK_Z, CLOCKS = 21.4, (-2.08, 2.08)


def gate_facade():
    p = Part("Gate_facade")
    for s in (-1, 1):
        p.add(box(P(s * 4.3, GATE_FRONT, 16.0), P(s * 8.0, GATE_BACK, 24.8), .02), STONE)
        # The door tracks: jambs before and behind the doors' channel.
        p.add(box(P(s * DOOR_X, GATE_FRONT, 16.0), P(s * 4.3, 127.93, OPEN_TOP)), STONE)
        p.add(box(P(s * DOOR_X, 128.67, 16.0), P(s * 4.3, GATE_BACK, OPEN_TOP)), STONE)
        # The lintel's ends beside the hollow.
        p.add(box(P(s * 4.1, 127.92, OPEN_TOP), P(s * 4.3, 128.68, 23.6)), STONE)
        # Base, rustication and the fluted pilaster that frames the opening.
        p.add(run([(s * 8.0, GATE_FRONT), (s * DOOR_X, GATE_FRONT), (s * DOOR_X, 127.93)], [-Y_, X_ * -s], 16.0, BASE), STONE)
        p.add(run([(s * DOOR_X, 128.67), (s * DOOR_X, GATE_BACK), (s * 8.0, GATE_BACK)], [X_ * -s, Y_], 16.0, BASE), STONE)
        for row in range(7):
            z0 = 16.45 + row * .55
            cuts = [4.95, 6.45, 8.0] if row % 2 == 0 else [4.95, 5.7, 7.2, 8.0]
            for a, b in zip(cuts, cuts[1:]):
                p.add(box(P(s * (a + .015), 127.52, z0 + .015), P(s * (b - .015), GATE_FRONT, min(z0 + .535, OPEN_TOP)), .035, 1), STONE)
        x0, x1 = 4.3, 4.95
        p.add(box(P(s * x0, 127.48, 16.45), P(s * x1, GATE_FRONT, 21.85), .01), STONE)
        p.add(box(P(s * (x0 - .05), 127.40, 16.0), P(s * (x1 + .05), GATE_FRONT, 16.3), .02), STONE)
        p.add(box(P(s * (x0 - .02), 127.43, 16.3), P(s * (x1 + .02), GATE_FRONT, 16.45), .02), STONE)
        p.add(box(P(s * (x0 - .03), 127.425, 16.42), P(s * (x1 + .03), GATE_FRONT, 16.48)), GOLD)
        for i in range(4):
            x = x0 + .13 + i * .13
            p.add(box(P(s * (x - .018), 127.474, 16.7), P(s * (x + .018), 127.48, 21.6)), GOLD)
        p.add(box(P(s * (x0 - .01), 127.46, 21.85), P(s * (x1 + .01), GATE_FRONT, 21.92)), GOLD)
        p.add(box(P(s * (x0 - .03), 127.44, 21.92), P(s * (x1 + .03), GATE_FRONT, 22.22)), GOLD)
        for x in (x0 + .03, x1 - .03):
            p.add(lathe([(0, 0), (.075, 0), (.075, .03), (0, .03)], P(s * x, 127.44, 22.16), -Y_, 16), GOLD)
        p.add(box(P(s * (x0 - .08), 127.38, 22.22), P(s * (x1 + .08), GATE_FRONT, 22.4)), GOLD)
        # The pylon's frieze and attic panels.
        for (z0, z1), rose in (((20.62, 22.3), .18), ((23.15, 24.2), .13)):
            rect = [(s * 5.15, z0), (s * 7.8, z0), (s * 7.8, z1), (s * 5.15, z1)]
            if z0 < 21:
                p.add(extrude(rect, P(0, GATE_FRONT, 0), X_, Z_, -Y_, 0, .02), VELVET)
            p.add(outline(rect, P(0, GATE_FRONT - .01, 0), X_, Z_, -Y_, BEAD), GOLD)
            p.add(rosette(P(s * 6.47, GATE_FRONT - .02, (z0 + z1) / 2), -Y_, rose), GOLD)
    # The hollow lintel the doors rise into: front and back slabs, the top.
    p.add(box(P(-4.3, GATE_FRONT, OPEN_TOP), P(4.3, 127.92, 23.6)), STONE)
    p.add(box(P(-4.3, 128.68, OPEN_TOP), P(4.3, GATE_BACK, 23.6)), STONE)
    p.add(box(P(-4.3, GATE_FRONT, 23.6), P(4.3, GATE_BACK, 24.8)), STONE)
    for y0, y1 in ((127.9, 127.94), (128.66, 128.7)):
        p.add(box(P(-4.1, y0, OPEN_TOP - .03), P(4.1, y1, OPEN_TOP + .01)), GOLD)
    # The mullion between the doors: no wider than 0.3 in their channel, a pilaster before and behind it.
    p.add(box(P(-.15, GATE_FRONT, 16.0), P(.15, GATE_BACK, OPEN_TOP)), STONE)
    for y0, y1, sgn in ((127.5, 127.93, -1), (128.67, 129.1, 1)):
        p.add(box(P(-.28, y0, 16.3), P(.28, y1, 19.9), .01), STONE)
        p.add(box(P(-.33, y0 - (.04 if sgn < 0 else 0), 16.0), P(.33, y1 + (.04 if sgn > 0 else 0), 16.3), .02), GOLD)
        p.add(box(P(-.33, y0 - (.04 if sgn < 0 else 0), 19.9), P(.33, y1 + (.04 if sgn > 0 else 0), OPEN_TOP), .015), GOLD)
        face = y0 if sgn < 0 else y1
        for x in (-.12, 0, .12):
            p.add(box(P(x - .015, face + sgn * .006, 16.5), P(x + .015, face, 19.7)), GOLD)
    # The door frame round the opening, both faces.
    frame = [(-DOOR_X, 16.0), (-DOOR_X, OPEN_TOP), (DOOR_X, OPEN_TOP), (DOOR_X, 16.0)]
    for y, n in ((GATE_FRONT, -Y_), (GATE_BACK, Y_)):
        pts = [P(x, y, z) for x, z in frame]
        ups = mitre([-X_, Z_, X_], 4)
        p.add(sweep(pts, [n] * 4, ups, scaled(BAND, 1, .8)), STONE)
        p.add(sweep(pts, [n] * 4, ups, circle(.06, .0, .028, 8)), GOLD)
    # Frieze: the clocks' gold frames with corner rosettes, the cartouche with the chess king.
    for c in CLOCKS:
        rect = [(c - 1.0, 20.6), (c + 1.0, 20.6), (c + 1.0, 22.33), (c - 1.0, 22.33)]
        p.add(outline(rect, P(0, GATE_FRONT, 0), X_, Z_, -Y_, BEAD), GOLD)
        for dx, z in ((-.88, 20.72), (.88, 20.72), (-.88, 22.21), (.88, 22.21)):
            p.add(rosette(P(c + dx, GATE_FRONT, z), -Y_, .075), GOLD)
    cart = []
    for i in range(64):
        t = 2 * PI * i / 64
        ct, st = math.cos(t), math.sin(t)
        x = .8 * math.copysign(abs(ct) ** (2 / 2.6), ct)
        z = .82 * math.copysign(abs(st) ** (2 / 2.6), st)
        cart.append((x, 21.46 + z))
    back = [(x * 1.1, 21.46 + (z - 21.46) * 1.06) for x, z in cart]
    p.add(extrude(back, P(0, GATE_FRONT, 0), X_, Z_, -Y_, 0, .025), STONE)
    p.add(extrude(cart, P(0, GATE_FRONT, 0), X_, Z_, -Y_, 0, .05), VELVET)
    p.add(outline(cart, P(0, GATE_FRONT - .05, 0), X_, Z_, -Y_, circle(0, 0, .035, 8)), GOLD)
    king = [(0, 0), (.36, 0), (.36, .05), (.32, .07), (.3, .1), (.26, .12), (.2, .16), (.15, .25), (.12, .45),
            (.11, .58), (.16, .6), (.18, .63), (.13, .66), (.15, .7), (.2, .78), (.22, .86), (.2, .88), (0, .88)]
    kb = P(0, GATE_FRONT - .05, 20.8)
    p.add(lathe(scaled(king, 1.0, 1.15), kb, Z_, 20, PI, 2 * PI, e1=X_), GOLD)   # the half toward the balcony
    top = 20.8 + .88 * 1.15
    p.add(box(P(-.035, kb.y - .06, top), P(.035, kb.y, top + .22)), GOLD)
    p.add(box(P(-.11, kb.y - .06, top + .09), P(.11, kb.y, top + .15)), GOLD)
    # Cornice, the chessboard attic, the top cornice - front and back.
    for y, n in ((GATE_FRONT, -Y_), (GATE_BACK, Y_)):
        p.add(run([(-8.0, y), (8.0, y)], [n], 22.4, scaled(CORNICE, .9, 1.1)), STONE)
        p.add(run([(-8.0, y), (8.0, y)], [n], 22.4, BEAD), GOLD)
        p.add(run([(-8.0, y), (8.0, y)], [n], 24.45, scaled(CORNICE, .7, .7)), STONE)
        rect = [(-3.35, 23.25), (3.35, 23.25), (3.35, 24.1), (-3.35, 24.1)]
        p.add(outline(rect, P(0, y, 0), X_, Z_, n, BEAD), GOLD)
        for i in range(16):
            for j in range(2):
                x0, z0 = -3.2 + i * .4, 23.275 + j * .4
                p.add(box(P(x0, y, z0), P(x0 + .4, y + n.y * .02, z0 + .4)), MARBLE if (i + j) % 2 == 0 else EBONY)
    return p


def gate_clocks():
    p = Part("Gate_clocks")
    for c in CLOCKS:
        at = P(c, GATE_FRONT, CLOCK_Z)
        p.add(lathe([(.72, 0), (.72, .1), (.745, .16), (.79, .19), (.82, .15), (.82, 0)], at, -Y_, 64, closed_prof=True), GOLD)
        p.add(lathe([(0, 0), (.725, 0), (.725, .06), (0, .06)], at, -Y_, 64), IVORY)
        p.add(lathe([(.6, .06), (.68, .06), (.68, .066), (.6, .066)], at, -Y_, 64, closed_prof=True), EBONY)
        for h in range(12):
            t = PI / 2 - 2 * PI * h / 12
            d = X_ * math.cos(t) + Z_ * math.sin(t)
            e = X_ * -math.sin(t) + Z_ * math.cos(t)
            w = .03 if h % 3 else .05
            quad = [(-w, .5), (w, .5), (w, .665), (-w, .665)]
            p.add(extrude(quad, at, e, d, -Y_, .06, .074), GOLD)
        t = PI / 2 - 2 * PI * 1.3 / 12
        d, e = X_ * math.cos(t) + Z_ * math.sin(t), X_ * -math.sin(t) + Z_ * math.cos(t)
        flag = [(0, .3), (.13, .38), (0, .44)]
        p.add(extrude(flag, at, e, d, -Y_, .06, .07), RUBY)
        p.add(extrude([(-.008, .3), (.008, .3), (.008, .46), (-.008, .46)], at, e, d, -Y_, .06, .072), GOLD)
    return p


def gate_transom():
    p = Part("Gate_transom")
    y = 127.79
    for xa, xb in ((-DOOR_X + .03, -.36), (.36, DOOR_X - .03)):
        xm, w, rise, z0 = (xa + xb) / 2, (xb - xa) / 2, .93, 19.34
        R = (w * w + rise * rise) / (2 * rise)
        cz = z0 + rise - R
        t0 = math.atan2(z0 - cz, xa - xm)
        t1 = math.atan2(z0 - cz, xb - xm)
        top = arc(xm, cz, R, t0, t1, 40)
        def tube(pts2, r):
            pts = [P(x, y, z) for x, z in pts2]
            ups = []
            for i in range(len(pts)):
                a, b = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
                d = (b - a).normalized()
                ups.append(d.cross(Y_).normalized())
            p.add(sweep(pts, [Y_] * len(pts), ups, circle(0, 0, r, 8)), GOLD)
        tube(top, .035)
        tube([(xa, z0), (xb, z0)], .035)
        tube(arc(xm, z0, .5, PI, 0, 24), .022)
        for k in range(1, 9):
            t = PI * k / 9
            d = Vector((math.cos(t), math.sin(t)))
            lo = (xm + d.x * .5, z0 + d.y * .5)
            # Out to the top arc.
            r = .5
            while True:
                q = (xm + d.x * r, z0 + d.y * r)
                if (q[0] - xm) ** 2 + (q[1] - cz) ** 2 > (R - .02) ** 2:
                    break
                r += .01
            tube([lo, q], .018)
            p.add(lathe([(0, 0), (.04, 0), (.04, .02), (0, .03)], P(lo[0], y, lo[1]), -Y_, 10), GOLD)
        p.add(extrude(arc(xm, z0, .26, 0, PI, 20), P(0, y, 0), X_, Z_, Y_, -.03, .03), GOLD)
        p.add(rosette(P(xm, y - .03, z0 + .08), -Y_, .12), GOLD)
    return p


def gate_pediment():
    p = Part("Gate_pediment")
    half, rise, z0 = 4.6, 1.25, 24.8
    R = (half * half + rise * rise) / (2 * rise)
    cz = z0 + rise - R
    t0, t1 = math.atan2(z0 - cz, -half), math.atan2(z0 - cz, half)
    seg = arc(0, cz, R, t0, t1, 48)
    p.add(extrude(seg, P(0, 127.74, 0), X_, Z_, Y_, 0, 1.12), STONE)
    for y, n in ((127.74, -Y_), (128.86, Y_)):
        pts = [P(x, y, z) for x, z in seg]
        ups = [Vector((x, 0, z - cz)).normalized() for x, z in seg]
        p.add(sweep(pts, [n] * len(pts), ups, scaled(CORNICE, .8, .7)), STONE)
        p.add(sweep(pts, [n] * len(pts), ups, [(a, b - .02) for a, b in BEAD]), GOLD)
    # The sunburst behind the crown, on the front of the tympanum.
    hub = (0, z0 + .05)
    for k in range(11):
        t = PI * (k + 1) / 12
        d = Vector((math.cos(t), math.sin(t)))
        r = .5
        while (hub[0] + d.x * r) ** 2 + (hub[1] + d.y * r - cz) ** 2 < (R - .32) ** 2:
            r += .02
        e = Vector((-d.y, d.x))
        w = .07 if k % 2 == 0 else .045
        ray = [(hub[0] + d.x * .45 + e.x * w, hub[1] + d.y * .45 + e.y * w), (hub[0] + d.x * r, hub[1] + d.y * r),
               (hub[0] + d.x * .45 - e.x * w, hub[1] + d.y * .45 - e.y * w)]
        p.add(extrude(ray, P(0, 127.74, 0), X_, Z_, -Y_, 0, .025), GOLD)
    p.add(extrude(arc(hub[0], hub[1] - .05, .5, 0, PI, 24), P(0, 127.74, 0), X_, Z_, -Y_, 0, .04), GOLD)
    p.add(lathe(circle(.5, 0, .03, 6), P(0, 127.70, hub[1] - .05), -Y_, 40, 0, PI, closed_prof=True, e1=X_), GOLD)
    crown(p, P(0, 128.3, z0 + rise - .08), 1.9)
    # The pylons' urn lanterns.
    for s in (-1, 1):
        c = P(s * 6.15, 128.3, 24.8)
        p.add(box(c + P(-.45, -.45, 0), c + P(.45, .45, .35), .02), STONE)
        p.add(box(c + P(-.49, -.49, .35), c + P(.49, .49, .41), .01), GOLD)
        urn = [(0, .41), (.3, .41), (.3, .47), (.18, .53), (.16, .61), (.28, .76), (.36, .96), (.33, 1.11), (.24, 1.21),
               (.24, 1.26), (0, 1.26)]
        p.add(lathe(urn, c, Z_, 32), STONE)
        p.add(lathe(circle(.27, 1.22, .035, 6), c, Z_, 32, closed_prof=True), GOLD)
        p.add(lathe(circle(.355, .96, .03, 6), c, Z_, 32, closed_prof=True), GOLD)
        for i in range(6):
            t = 2 * PI * i / 6
            d = Vector((math.cos(t), math.sin(t), 0))
            arm = [c + d * .22 * math.cos(u) * 1.0 + Z_ * (1.26 + .55 * math.sin(u)) for u in [PI / 2 * j / 8 for j in range(9)]]
            ups = [(d * math.cos(u) + Z_ * math.sin(u)).normalized() for u in [PI / 2 * j / 8 for j in range(9)]]
            p.add(sweep(arm, [d.cross(Z_)] * 9, ups, circle(0, 0, .018, 6)), GOLD)
        p.add(lathe([(0, 1.78), (.08, 1.79), (.05, 1.84), (0, 1.86)], c, Z_, 16), GOLD)
    return p


def lantern_flames():
    out = []
    for s, side in ((-1, "W"), (1, "E")):
        p = Part("Balcony_lantern_flame_" + side)
        c = P(s * 6.15, 128.3, 24.8)
        p.add(lathe([(0, 1.26), (.12, 1.3), (.16, 1.4), (.12, 1.55), (.05, 1.68), (0, 1.74)], c, Z_, 24), BULB)
        out.append(p)
    return out


def balcony_front():
    p = Part("Balcony_front")
    face = P(0, 121.0, 0)
    # A stone facing and soffit over the blockout box under the marble (its floor material showed through).
    p.add(box(P(-8.0, 120.97, 8.0), P(8.0, 121.0, 15.52)), STONE)
    p.add(box(P(-8.0, 121.0, 7.97), P(8.0, 128.3, 8.0)), STONE)
    p.add(run([(-8.0, 121.0), (8.0, 121.0)], [-Y_], 8.4, scaled(ARCHITRAVE, 1, -1)), STONE)
    p.add(run([(-8.0, 121.0), (8.0, 121.0)], [-Y_], 14.62, STRING), STONE)
    p.add(run([(-8.0, 121.0), (8.0, 121.0)], [-Y_], 15.0, BEAD), GOLD)
    cons = (-6.4, -3.6, 3.6, 6.4)
    for xc in cons:
        prof = [(0, 15.5), (.72, 15.5), (.72, 15.32)]
        for i in range(1, 15):
            t = i / 15
            prof.append((.12 + .58 * (.5 + .5 * math.cos(PI * t)), 15.3 - 2.4 * t))
        prof += [(.1, 12.78), (0, 12.74)]
        p.add(extrude([(-d, z) for d, z in prof], face, Y_, Z_, X_, xc - .22, xc + .22), STONE)
        for sx in (-1, 1):
            n = X_ * sx
            p.add(lathe(scaled([(0, 0), (.16, 0), (.16, .02), (.1, .03), (0, .035)], 1, 1), P(xc + sx * .22, 121.0 - .5, 15.02), n, 20), GOLD)
            p.add(lathe([(0, 0), (.1, 0), (.1, .02), (0, .03)], P(xc + sx * .22, 121.0 - .16, 13.05), n, 16), GOLD)
        p.add(box(P(xc - .12, 120.27, 15.0), P(xc + .12, 120.29, 15.28)), GOLD)
    # R346 (승규 11-04, a Unity shot of these panels: "금색 원이 깨져 보여 벽 축이 안맞아서 깨지는 것 같아"): each layer stands
    # clear of the one behind it. Before, the raised panel (121 -> 120.97) lay exactly on the facing (front 120.97), the
    # velvet disc's face was on that plane too (it flickered red and cream), the gold ring's tube was centred on it and
    # the narrow spans' rosettes sat behind it (121), so the rings showed broken. Now, from the facing (FACE_Y) out:
    # panel 3 cm, velvet 2.5 cm on it (from just inside it), the ring round the velvet's edge, the rosette on the velvet.
    FACE_Y = 120.97
    PANEL_Y = FACE_Y - .03
    VELVET_Y = PANEL_Y - .025
    spans = [(-7.75, -6.85), (-5.95, -4.05), (-3.15, 3.15), (4.05, 5.95), (6.85, 7.75)]
    for x0, x1 in spans:
        rect = [(x0, 9.2), (x1, 9.2), (x1, 14.25), (x0, 14.25)]
        p.add(outline(rect, face, X_, Z_, -Y_, BEAD), GOLD)
        cx = (x0 + x1) / 2
        if x1 - x0 > 1.5:
            p.add(extrude([(x0 + .25, 9.45), (x1 - .25, 9.45), (x1 - .25, 14.0), (x0 + .25, 14.0)], face, X_, Z_, -Y_,
                          121.0 - FACE_Y, 121.0 - PANEL_Y), STONE)
            p.add(lathe([(0, -.005), (.5, -.005), (.5, .025), (0, .025)], P(cx, PANEL_Y, 11.72), -Y_, 48), VELVET)
            p.add(lathe(circle(.52, .025, .036, 10), P(cx, PANEL_Y, 11.72), -Y_, 64, closed_prof=True), GOLD)
            p.add(rosette(P(cx, VELVET_Y, 11.72), -Y_, .22), GOLD)
        else:
            p.add(rosette(P(cx, FACE_Y, 11.72), -Y_, .14), GOLD)
    return p


# ------------------------------------------------------------------ the movers, built in the hall's frame at the origin
def door():
    p = Part("Door")
    p.add(box(P(-1.9, -.25, -1.6), P(1.9, .25, 1.6), .012), WALNUT)
    for f in (-1, 1):
        y0, y1 = f * .25, f * .29
        for lo, hi in (((-1.9, -1.6), (-1.62, 1.6)), ((1.62, -1.6), (1.9, 1.6)), ((-1.62, -1.6), (1.62, -1.32)),
                       ((-1.62, 1.32), (1.62, 1.6)), ((-.1, -1.32), (.1, 1.32)), ((-1.62, -.1), (1.62, .1))):
            p.add(box(P(lo[0], y0, lo[1]), P(hi[0], y1, hi[1]), .01), WALNUT)
        for (xa, xb), (za, zb) in [((xa, xb), (za, zb)) for xa, xb in ((-1.62, -.1), (.1, 1.62)) for za, zb in ((-1.32, -.1), (.1, 1.32))]:
            bm = bmesh.new()
            outer = [P(xa + .02, y0, za + .02), P(xb - .02, y0, za + .02), P(xb - .02, y0, zb - .02), P(xa + .02, y0, zb - .02)]
            inner = [P(xa + .16, f * .282, za + .16), P(xb - .16, f * .282, za + .16), P(xb - .16, f * .282, zb - .16), P(xa + .16, f * .282, zb - .16)]
            vo = [bm.verts.new(v) for v in outer]
            vi = [bm.verts.new(v) for v in inner]
            bm.faces.new(vo[::-1])
            bm.faces.new(vi)
            for i in range(4):
                j = (i + 1) % 4
                bm.faces.new((vo[i], vo[j], vi[j], vi[i]))
            p.add(bm, WALNUT)
            rect = [(xa, za), (xb, za), (xb, zb), (xa, zb)]
            p.add(outline(rect, P(0, y1 - f * .016, 0), X_, Z_, Y_ * f, [(a, -b) for a, b in FINE]), GOLD)
        p.add(lathe([(0, 0), (.2, 0), (.2, .012), (.15, .022), (.11, .018), (.08, .03), (0, .036)], P(0, y1, 0), Y_ * f, 24), GOLD)
        studs = [(x, z) for x in (-1.76, 1.76) for z in [-1.45 + .29 * i for i in range(11)]]
        studs += [(x, z) for z in (-1.46, 1.46) for x in [-1.45 + .29 * i for i in range(11)]]
        for x, z in studs:
            p.add(lathe([(0, 0), (.03, 0), (.025, .014), (0, .022)], P(x, y1, z), Y_ * f, 8), GOLD)
        for sx in (-1, 1):
            for sz in (-1, 1):
                p.add(box(P(sx * 1.86, y1, sz * 1.56), P(sx * 1.3, y1 + f * .008, sz * 1.5)), GOLD)
                p.add(box(P(sx * 1.86, y1, sz * 1.56), P(sx * 1.8, y1 + f * .008, sz * 1.0)), GOLD)
        p.add(box(P(-1.86, y1, -1.6), P(1.86, y1 + f * .008, -1.5)), GOLD)
    return p


def hand():
    p = Part("Hand")
    def flat(poly):
        p.add(extrude(poly, P(0, 0, 0), X_, Z_, Y_, -.012, .012), STEEL)
    flat([(-.022, -.05), (.022, -.05), (.012, .36), (-.012, .36)])
    flat([(-.018, .48), (.018, .48), (.032, .53), (0, .615), (-.032, .53)])
    flat([(-.012, -.13), (.012, -.13), (.022, -.05), (-.022, -.05)])
    p.add(lathe([(.045, -.012), (.07, -.012), (.07, .012), (.045, .012)], P(0, 0, .42), Y_, 32, closed_prof=True), STEEL)
    p.add(lathe([(0, -.012), (.05, -.012), (.05, .012), (0, .012)], P(0, 0, -.15), Y_, 24), STEEL)
    p.add(lathe([(0, .012), (.055, .012), (.055, .025), (.03, .04), (0, .045)], P(0, 0, 0), -Y_, 24), GOLD)
    return p


def box_uvs(ob, size=1.2):
    me = ob.data
    uv = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        u_i, v_i = [(1, 2), (0, 2), (0, 1)][ax]
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (co[u_i] / size, co[v_i] / size)


# ------------------------------------------------------------------ build
static = []
for s in (-1, 1):
    static.append(side_wall(s).done(coll))
    static.append(alcove(s).done(coll))
static.append(gate_facade().done(coll))
static.append(gate_clocks().done(coll))
static.append(gate_transom().done(coll))
static.append(gate_pediment().done(coll))
static.append(balcony_front().done(coll))
static += [f.done(coll) for f in lantern_flames()]
door_ob = door().done(coll, TAG + "_mover")
hand_ob = hand().done(coll, TAG + "_mover")
box_uvs(door_ob)

# The blockout pieces these replace: Unity stops drawing them (they keep their colliders); hidden here too.
retired = bpy.data.collections["_Blockout originals"]
gone = ["Balcony_wall_W", "Balcony_wall_E", "Knight_alcove_W", "Knight_alcove_E", "Gate_wall_W", "Gate_wall_E"]
gate = "B6_RoyalBalcony/09_Clock_gate_(the_palace_door)/"
hidden = []
for o in list(sc.objects):
    up = o.get("unity_path", "").split("#")[0]
    if up in ["B6_RoyalBalcony/" + g for g in gone] or up in [gate + "Gate_pillar", gate + "Clock_arch", gate + "Clock_face"]:
        if up not in hidden:
            hidden.append(up)
        for c in list(o.users_collection):
            c.objects.unlink(o)
        retired.objects.link(o)
        o.hide_render = o.hide_viewport = True
    elif up.startswith(gate) or up.startswith("B6_RoyalBalcony/17_Guard_knight_"):
        o.hide_render = True                    # the kit doors, hands and knights: Unity hangs the new ones on them

# Previews of the movers where the kit's stand (the right door raised as when open), and the knights.
prev = bpy.data.collections.get("Royal_balcony_v34 previews") or bpy.data.collections.new("Royal_balcony_v34 previews")
if prev.name not in sc.collection.children:
    sc.collection.children.link(prev)
for o in list(prev.objects):
    bpy.data.objects.remove(o)
for x, lift in ((-2.08, 0), (2.08, 4.1)):
    d = door_ob.copy(); d["own_fbx"] = "preview"; prev.objects.link(d)
    d.location = (x, 128.3, 17.6 + lift)
for x, ang in ((-2.08, -40), (2.08, 115)):
    h = hand_ob.copy(); h["own_fbx"] = "preview"; prev.objects.link(h)
    h.location = (x, 127.5, CLOCK_Z)
    h.rotation_euler = (0, math.radians(ang), 0)
door_ob.hide_render = hand_ob.hide_render = True
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=os.path.join(RES, "FallingKnight.fbx"), axis_forward="-Z", axis_up="Y")
knight = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
for o in knight:
    for c in list(o.users_collection):
        c.objects.unlink(o)
for x, y, face in ((-6.6, 123.3, 90), (6.6, 126.3, -90)):
    for o in knight:
        k = o.copy(); k["own_fbx"] = "preview"; prev.objects.link(k)
        k.matrix_world = Matrix.Translation((x, y, 19.0)) @ Matrix.Rotation(math.radians(face), 4, "Z") @ Matrix.Scale(1.3, 4) @ o.matrix_world
for o in knight:
    bpy.data.objects.remove(o)

# ------------------------------------------------------------------ renders
sc.render.engine = "BLENDER_WORKBENCH"
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
sh = sc.display.shading
sh.light, sh.color_type = "STUDIO", "MATERIAL"
sh.show_cavity, sh.show_shadows = True, True
sh.cavity_type = "BOTH"
out = os.path.join(HERE, "renders_v41"); os.makedirs(out, exist_ok=True)
for name, loc, at, lens in (("approach", (0, 111.5, 18.4), (0, 128.5, 21.2), 24),
                            ("gate", (0, 121.8, 17.7), (0, 128.3, 21.8), 20),
                            ("niche_W", (2.5, 122.2, 18.3), (-8.4, 125.3, 21.3), 22),
                            ("crown", (-3.5, 120.5, 23.5), (0, 128.3, 25.5), 30),
                            ("from_dais", (4, 95.5, 17.8), (0, 124, 18.5), 30),
                            ("front_low", (0, 108, 9), (0, 121, 12.5), 24)):
    cd = bpy.data.cameras.new(name); cd.lens = lens
    o = bpy.data.objects.new("Cam34_" + name, cd); sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = o
    sc.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera

# ------------------------------------------------------------------ export
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
for o in sc.objects:
    o.select_set(False)
for o in static:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "RoyalBalconyV34.fbx"), **FBX)
with open(os.path.join(RES, "RoyalBalconyV34_Hidden.txt"), "w", encoding="utf8") as f:
    f.write("\n".join(hidden) + "\n")
print("STATIC", len(static), "nodes,", sum(len(o.data.polygons) for o in static), "faces,", len(hidden), "hidden:", hidden)

# The movers turned 180 degrees about z: imported without a pivot they stand as their kit pieces do.
movers = []
for src in (door_ob, hand_ob):
    me = src.data.copy()
    me.transform(Matrix.Rotation(PI, 4, "Z"))
    ob = bpy.data.objects.new(src.name, me)
    sc.collection.objects.link(ob)
    movers.append(ob)
src_names = [door_ob.name, hand_ob.name]
door_ob.name, hand_ob.name = "Door_hall_frame", "Hand_hall_frame"
for o, n in zip(movers, src_names):
    o.name = n
for o in sc.objects:
    o.select_set(False)
for o in movers:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "RoyalBalconyMovers.fbx"), **FBX)
print("MOVERS", [(o.name, len(o.data.polygons)) for o in movers])
for o in movers:
    bpy.data.objects.remove(o)
door_ob.name, hand_ob.name = src_names

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v34C.blend"))
