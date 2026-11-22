# Royal water garden - the birds over it, v1 (R769, 2026-11-22).
#
# 승규 11-22: "정원 전체 분위기 소리 넣어줘 또한 블렌더에서 새 모델링 하여 실제로 날아다니는 연출을 넣어줘". Two kinds, drawn in the
# garden kit's look (cream, gold, royal blue; rounded, chunky, bright):
#   Dove      the royal white dove: a round white body, a round head with a coral beak and black eyes, a fanned tail
#             with grey tips, broad rounded wings whose four primaries splay at the tip (grey ends)
#   Swallow   a small royal-blue swallow: navy above, cream below, a coral throat, a long forked tail, long narrow
#             swept wings
# Each kind is three objects, so Unity can flap the wings without a skeleton (GardenBirds): "<Kind> body" (origin at
# the body's middle) and "<Kind> wing L / R", each wing's origin at its shoulder, the wing lying flat along +-X, so
# turning it about the body's forward axis (Blender +Y = Unity +z under the garden's 180-degree pivot) flaps it.
# The kinds sit on top of one another at the origin in the exported collection; the renders use copies.
#
# Run:  blender -b --python birds_sample_v1.py
#       GC_NEW_MATERIALS_ONLY=1 blender -b --python export_concept.py -- BirdsSample_v1.blend "Birds dressing" GardenBirds
# Out:  BirdsSample_v1.blend, render_birds_v1_*.png (next to this file); Unity: Resources/PawnRushGardenConcept/GardenBirds.fbx

import math
import os
import sys

os.environ["GC_DRESS"] = "Birds dressing"
os.environ["GC_SEED"] = "769"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403

M_WHITE = material("GC | bird white", (.97, .96, .93), rough=.75)
M_GREY = material("GC | bird grey", (.66, .70, .78), rough=.75)
M_NAVY = material("GC | bird navy", (.13, .20, .52), rough=.6)
M_CREAM = material("GC | bird cream", (.98, .90, .76), rough=.75)
M_BEAK = material("GC | bird beak", (.95, .52, .40), rough=.5)
M_EYE = material("GC | bird eye", (.05, .05, .07), rough=.25)
M_GOLD = material("GC | bird gold", (.96, .72, .22), rough=.35, metal=.6)


class Part:
    """One object of many pieces, its material slots in order of first use."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def slot(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def faces(self, verts_faces, mat, smooth):
        verts, faces = verts_faces
        vs = [self.bm.verts.new(v) for v in verts]
        idx = self.slot(mat)
        made = []
        for f in faces:
            try:
                face = self.bm.faces.new([vs[i] for i in f])
            except ValueError:
                continue
            face.material_index = idx
            face.smooth = smooth
            made.append(face)
        return made

    def build(self, collection, location=(0, 0, 0)):
        mesh = bpy.data.meshes.new(self.name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for m in self.mats:
            mesh.materials.append(m)
        ob = bpy.data.objects.new(self.name, mesh)
        ob.location = location
        collection.objects.link(ob)
        return ob


def ellipsoid(center, radii, segs=14, rings=9, shape=None):
    """A UV ellipsoid; shape(x, y, z) -> (x, y, z) bends it (unit-sphere coordinates in, offsets out)."""
    cx, cy, cz = center
    rx, ry, rz = radii
    verts, faces = [], []
    verts.append((0, 0, -1))
    for r in range(1, rings):
        phi = math.pi * r / rings - math.pi / 2
        for s in range(segs):
            th = 2 * math.pi * s / segs
            verts.append((math.cos(phi) * math.cos(th), math.cos(phi) * math.sin(th), math.sin(phi)))
    verts.append((0, 0, 1))
    out = []
    for (x, y, z) in verts:
        px, py, pz = x * rx, y * ry, z * rz
        if shape:
            px, py, pz = shape(x, y, z, px, py, pz)
        out.append((cx + px, cy + py, cz + pz))
    top = len(verts) - 1
    for s in range(segs):
        faces.append((0, 1 + (s + 1) % segs, 1 + s))
    for r in range(rings - 2):
        a, b = 1 + r * segs, 1 + (r + 1) * segs
        for s in range(segs):
            faces.append((a + s, a + (s + 1) % segs, b + (s + 1) % segs, b + s))
    last = 1 + (rings - 2) * segs
    for s in range(segs):
        faces.append((last + s, last + (s + 1) % segs, top))
    return out, faces


def cone(base, tip, r, sides=6):
    b, t = Vector(base), Vector(tip)
    axis = (t - b).normalized()
    side = axis.orthogonal().normalized()
    up = axis.cross(side)
    verts = [tuple(b + (side * math.cos(2 * math.pi * i / sides) + up * math.sin(2 * math.pi * i / sides)) * r)
             for i in range(sides)]
    verts.append(tuple(t))
    verts.append(tuple(b))
    faces = [(i, (i + 1) % sides, sides) for i in range(sides)] + [((i + 1) % sides, i, sides + 1) for i in range(sides)]
    return verts, faces


def slab(outline, z_of, thick_of):
    """A thin plate: outline (x, y) points round its edge (counter-clockwise seen from above), its middle surface at
    z_of(x, y), thickness thick_of(x, y). Top and bottom ngons plus a band round the edge."""
    n = len(outline)
    top = [(x, y, z_of(x, y) + thick_of(x, y) / 2) for x, y in outline]
    bot = [(x, y, z_of(x, y) - thick_of(x, y) / 2) for x, y in outline]
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, n + i, n + j, j))
    return top + bot, faces


# ------------------------------------------------------------------ the dove (0.42 m beak to tail, 0.80 m span)

def dove_body(name):
    p = Part(name)

    def plump(x, y, z, px, py, pz):
        # a round breast forward and low, the back end narrowing into the tail
        if y < 0:
            k = 1 + .45 * y
            px, pz = px * k, pz * k + .02 * (-y)
        else:
            pz -= .02 * y * (1 - z)
        return px, py, pz

    p.faces(ellipsoid((0, 0, 0), (.115, .2, .11), shape=plump), M_WHITE, True)
    p.faces(ellipsoid((0, .19, .085), (.072, .078, .072)), M_WHITE, True)
    p.faces(cone((0, .255, .078), (0, .315, .07), .02), M_BEAK, True)
    for s in (-1, 1):
        p.faces(ellipsoid((s * .052, .232, .1), (.013, .013, .015), segs=8, rings=5), M_EYE, True)
    # the tail fan: white with grey tips, a little up from the body line
    fan = [(-.035, -.14), (.035, -.14), (.085, -.33), (.045, -.36), (0, -.345), (-.045, -.36), (-.085, -.33)]
    z = lambda x, y: .03 + .1 * (y + .14)  # noqa: E731
    p.faces(slab(fan[:2] + [(.07, -.27), (-.07, -.27)], z, lambda x, y: .03), M_WHITE, False)
    p.faces(slab([(-.07, -.27), (.07, -.27)] + fan[2:], z, lambda x, y: .022), M_GREY, False)
    return p


def dove_wing(name, side):
    """Right wing (side 1) along +X from its shoulder at the origin; the left one mirrored."""
    p = Part(name)
    # the inner wing: broad and round at the front, its trailing edge the secondaries
    inner = [(0, .07), (.16, .075), (.27, .03), (.3, -.06), (.2, -.13), (.08, -.15), (0, -.1)]
    z = lambda x, y: .05 * (x / .4) ** 2  # noqa: E731
    th = lambda x, y: .045 * (1 - .7 * x / .3)  # noqa: E731
    p.faces(slab([(side * x, y) for x, y in (inner if side > 0 else inner[::-1])], z, th), M_WHITE, True)
    # four primaries splayed at the tip, white with grey ends
    for i, (a, length) in enumerate(((-.12, .15), (-.42, .16), (-.72, .15), (-1.0, .13))):
        root = Vector((.25 - .02 * i, .02 - .05 * i, 0))
        d = Vector((math.cos(a), math.sin(a), 0))
        n = Vector((-d.y, d.x, 0))
        w = .022
        tip = root + d * length
        mid = root + d * length * .62
        pts = [root + n * w, mid + n * w * 1.05, mid - n * w * 1.05, root - n * w]
        tip_pts = [mid + n * w * 1.05, tip + n * w * .55, tip + d * .015, tip - n * w * .55, mid - n * w * 1.05]
        for poly, mat in ((pts, M_WHITE), (tip_pts, M_GREY)):
            outline = [(side * q.x, q.y) for q in (poly if side > 0 else poly[::-1])]
            p.faces(slab(outline, lambda x, y: .05 * (abs(x) / .4) ** 2 + .004 * i, lambda x, y: .012), mat, False)
    return p


# ------------------------------------------------------------------ the swallow (0.34 m with its tail, 0.62 m span)

def swallow_body(name):
    p = Part(name)

    def sleek(x, y, z, px, py, pz):
        if y < 0:
            k = 1 + .5 * y
            px, pz = px * k, pz * k
        return px, py, pz

    verts, faces = ellipsoid((0, 0, 0), (.07, .15, .065), shape=sleek)
    # navy above the middle line, cream below
    top = [f for f in faces if sum(verts[i][2] for i in f) / len(f) > -.01]
    under = [f for f in faces if f not in top]
    p.faces((verts, top), M_NAVY, True)
    p.faces((verts, under), M_CREAM, True)
    p.faces(ellipsoid((0, .14, .045), (.05, .055, .05)), M_NAVY, True)
    p.faces(ellipsoid((0, .165, .02), (.035, .03, .028), segs=10, rings=5), M_BEAK, True)   # the coral throat
    p.faces(cone((0, .19, .045), (0, .225, .04), .012, sides=5), M_EYE, True)
    for s in (-1, 1):
        p.faces(ellipsoid((s * .036, .168, .06), (.009, .009, .011), segs=8, rings=5), M_EYE, True)
        # one prong of the forked tail
        prong = [(s * .012, -.1), (s * .04, -.1), (s * .085, -.3), (s * .07, -.31)]
        p.faces(slab(prong if s > 0 else prong[::-1], lambda x, y: .01 + .03 * (y + .1), lambda x, y: .014), M_NAVY, False)
    return p


def swallow_wing(name, side):
    p = Part(name)
    # long, narrow and swept back to a point
    shape = [(0, .05), (.1, .06), (.2, .02), (.31, -.06), (.33, -.1), (.22, -.06), (.1, -.05), (0, -.06)]
    outline = [(side * x, y) for x, y in (shape if side > 0 else shape[::-1])]
    p.faces(slab(outline, lambda x, y: .02 * (abs(x) / .3) ** 2, lambda x, y: .03 * (1 - .75 * abs(x) / .33)), M_NAVY, True)
    return p


KINDS = {
    "Dove": (dove_body, dove_wing, (.09, .04, .06)),
    "Swallow": (swallow_body, swallow_wing, (.055, .03, .035)),
}


def bird(kind, collection, at=(0, 0, 0), flap=0.0, turn=0.0, prefix=""):
    body_f, wing_f, shoulder = KINDS[kind]
    root = Vector(at)
    body = body_f(prefix + kind + " body").build(collection, root)
    wings = []
    for side, tag in ((1, "R"), (-1, "L")):
        w = wing_f(prefix + kind + " wing " + tag, side).build(collection, root + Vector((side * shoulder[0], shoulder[1], shoulder[2])))
        w.rotation_euler = (0, -side * math.radians(flap), 0)
        wings.append(w)
    for ob in [body] + wings:
        if turn:
            ob.rotation_euler.z += math.radians(turn)
            off = ob.location - root
            off.rotate(Matrix.Rotation(math.radians(turn), 3, "Z"))
            ob.location = root + off
    return [body] + wings


# The exported birds: at the origin, wings flat.
for k in KINDS:
    bird(k, DRESS)

# ------------------------------------------------------------------ renders (copies in a collection not exported)
SHOW = coll("Render birds (not exported)")
DRESS.hide_render = True
render_setup()
for x, flap in ((-.55, 35), (0, -20), (.55, 5)):
    bird("Dove", SHOW, (x, 0, 0), flap, turn=25, prefix="R%.2f " % x)
for x, flap in ((1.15, 30), (1.6, -15)):
    bird("Swallow", SHOW, (x, .05, .05), flap, turn=25, prefix="R%.2f " % x)
shoot("birds_v1", "front", (1.3, 2.3, .7), (.55, 0, 0), 38)
shoot("birds_v1", "side", (2.9, -.2, .35), (.55, 0, 0), 38)
shoot("birds_v1", "below", (.9, 1.3, -1.4), (.55, 0, 0), 38)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "BirdsSample_v1.blend"))
print("BIRDS DONE")
