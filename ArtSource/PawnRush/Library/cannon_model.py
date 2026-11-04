"""Pawn Rush library (작전 회의실): the blower cannon modelled by hand to its concept image — preview (R368, v2 R370).

    blender --background --factory-startup --python cannon_model.py -- --preview D:/check   (renders, writes nothing)

승규 님: "대포에셋은 지워주고 너가 직접 모델링 텍스쳐 입혀줘", then with the concept image again "대포 모델링을 사진처럼
더 멋있게 해줘야지". What the image shows, and what v2 builds:
  barrel   a tapering drum of cream stone staves (lengthwise seams, a cross joint) held by flat gold hoops, a gold strap
           along the top with a lug at the back, a gold back cap; a teal band near the front with a big gold rook
  muzzle   a teal flared collar studded with gold rivets, a thick gold rim with clamps, a dark steel duct, a swirl fan
           of thin blades round a gold hub
  mount    A-frame cheeks (teal, gold edging, a cut-out between the legs, gold feet) on a stepped turntable (teal drum
           with rivets, a dark step, a gold ring), stacked trunnion caps (teal, gold, a gold dome, a dark bolt)
  plinth   cream stone blocks with dark joints and a slab top, chamfered gold corner caps with rivets, teal panels in
           gold frames on all four sides, a gold-framed rook plaque on the long sides
Built in the cannon's own space (the BLOWER_<x> empty's: nozzle toward +x, centre line y 0, floor z 0), inside the
old sculpt's footprint, the barrel's axis level at the fan's hub (x .938, z 1.501): LibraryAirCannon spins the fan
about the level line from the cannon's origin to the fan, so it turns true in the duct. The fan is its own mesh,
origin on the hub, blades in its y-z plane, as the old BLOWER_<x>_Fan. Materials are flat parts (Library gold, teal,
cream, steel, dark, grout); every closed piece faces outward and the duct inward (Unity culls back faces).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

AXIS_Z, FAN_X = 1.501, .938
PARTS = {   # base colour (linear), metallic, smoothness
    "gold": ((1.0, .60, .19), .9, .72),
    "teal": ((.040, .105, .13), .45, .55),
    "cream": ((.74, .67, .57), 0, .3),
    "steel": ((.07, .08, .095), .85, .62),
    "dark": ((.025, .025, .03), .4, .45),
    "grout": ((.25, .21, .17), 0, .2),
}
SLOT = {k: i for i, k in enumerate(PARTS)}
TX = -.55          # turntable, yoke and trunnion centre (x)


# ------------------------------------------------------------------ bmesh helpers (each face gets its part's slot)

class Mesh:
    def __init__(self):
        self.bm = bmesh.new()

    def _tag(self, faces, part, smooth=False):
        for f in faces:
            f.material_index = SLOT[part]
            f.smooth = smooth

    def _closed(self, faces, part, smooth=False):
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)   # a closed piece: outward
        self._tag(faces, part, smooth)

    def box(self, part, lo, hi, rot_x=0.0, about=(0.0, AXIS_Z)):
        """An axis box, optionally turned rot_x about the barrel's axis line."""
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        turn = Matrix.Translation((0, about[0], about[1])) @ Matrix.Rotation(rot_x, 4, "X") @ \
            Matrix.Translation((0, -about[0], -about[1]))
        for v in r["verts"]:
            v.co = turn @ Vector(((lo[i] + hi[i]) / 2 + v.co[i] * (hi[i] - lo[i]) for i in range(3)))
        self._tag({f for v in r["verts"] for f in v.link_faces}, part)

    def ring(self, part, profile, segments=64, smooth=True, about=(0.0, AXIS_Z), closed=True, a0=0.0, a1=None):
        """Revolves (x, r) points round the line y = about[0], z = about[1] parallel to x. Closed profiles make a
        solid ring; an open profile is a shell facing out while x grows, in while it falls. a0 .. a1: an arc only."""
        full = a1 is None
        n = segments if full else segments + 1
        span = (2 * math.pi) if full else (a1 - a0)
        rings = []
        for x, r in profile:
            if r < 1e-6:   # a point on the axis: one vertex, met by triangles (no zero-area faces: Unity reads NaNs)
                v = self.bm.verts.new((x, about[0], about[1]))
                rings.append([v] * n)
                continue
            rings.append([self.bm.verts.new((x, about[0] + r * math.cos(a0 + span * i / segments),
                                             about[1] + r * math.sin(a0 + span * i / segments))) for i in range(n)])
        faces = []
        pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
        for ra, rb in pairs:
            for i in range(segments):
                j = (i + 1) % n
                quad = [ra[i], ra[j], rb[j], rb[i]]
                corners = [v for k, v in enumerate(quad) if v not in quad[:k]]
                if len(corners) >= 3:
                    faces.append(self.bm.faces.new(corners))
        if closed and not full:
            faces.append(self.bm.faces.new([r[0] for r in rings]))
            faces.append(self.bm.faces.new([r[-1] for r in rings][::-1]))
        if closed:
            self._closed(faces, part, smooth)
        else:
            self._tag(faces, part, smooth)
        if not full:   # flat end caps stay flat
            for f in faces[-2:]:
                f.smooth = False

    def cylinder(self, part, a, b, r, segments=32):
        a, b = Vector(a), Vector(b)
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segments, radius1=r, radius2=r,
                                    depth=(b - a).length)
        rot = Vector((0, 0, 1)).rotation_difference(b - a).to_matrix().to_4x4()
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=res["verts"])
        faces = {f for v in res["verts"] for f in v.link_faces}
        self._tag(faces, part)
        for f in faces:
            f.smooth = len(f.verts) == 4

    def dome(self, part, at, r, normal, flat=.55, segments=16):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segments, v_segments=segments // 2, radius=r)
        # only the upper half: its open rim sits on the surface (no squashed, zero-area lower half)
        below = [v for v in res["verts"] if v.co.z < -1e-6]
        keep = [v for v in res["verts"] if v.co.z >= -1e-6]
        bmesh.ops.delete(self.bm, geom=below, context="VERTS")
        rot = Vector((0, 0, 1)).rotation_difference(Vector(normal)).to_matrix()
        for v in keep:
            co = v.co.copy()
            co.z *= flat
            v.co = Vector(at) + rot @ co
        self._tag({f for v in keep for f in v.link_faces}, part, True)

    def prism(self, part, outline, origin, u, v, w, depth):
        """A flat polygon (outline in (s, t) on the plane origin + s u + t v) extruded depth along w."""
        o, u, v, w = Vector(origin), Vector(u), Vector(v), Vector(w)
        lo = [self.bm.verts.new(o + s * u + t * v) for s, t in outline]
        hi = [self.bm.verts.new(o + s * u + t * v + w * depth) for s, t in outline]
        faces = [self.bm.faces.new(lo[::-1]), self.bm.faces.new(hi)]
        for i in range(len(outline)):
            j = (i + 1) % len(outline)
            faces.append(self.bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
        self._closed(faces, part)

    def finish(self, name):
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for part in PARTS:
            me.materials.append(material(part))
        return me


def material(part):
    name = "Library " + part
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        c, mt, sm = PARTS[part]
        b.inputs["Base Color"].default_value = (*c, 1)
        b.inputs["Metallic"].default_value = mt
        b.inputs["Roughness"].default_value = 1 - sm
    return m


def on_axis(x, r, a):
    """The point at x, radius r, angle a (0 = +y, pi/2 = up) round the barrel's axis, and its outward normal."""
    n = Vector((0, math.cos(a), math.sin(a)))
    return Vector((x, 0, AXIS_Z)) + n * r, n


# The rook emblem as convex pieces in a unit square (s right, t up).
ROOK = (
    ((.26, .04), (.74, .04), (.74, .13), (.26, .13)),      # foot
    ((.31, .13), (.69, .13), (.69, .21), (.31, .21)),
    ((.38, .21), (.62, .21), (.59, .62), (.41, .62)),      # body
    ((.31, .62), (.69, .62), (.69, .72), (.31, .72)),      # collar
    ((.31, .72), (.40, .72), (.40, .88), (.31, .88)),      # merlons
    ((.455, .72), (.545, .72), (.545, .88), (.455, .88)),
    ((.60, .72), (.69, .72), (.69, .88), (.60, .88)),
)


def rook(m, centre, u, v, w, size, frame=False, depth=.014):
    c, u, v, w = Vector(centre), Vector(u), Vector(v), Vector(w)
    o = c - u * size / 2 - v * size / 2
    for piece in ROOK:
        m.prism("gold", [(s * size, t * size) for s, t in piece], o, u, v, w, depth)
    if frame:
        e = .05
        for a, b in (((0, 0), (1, e)), ((0, 1 - e), (1, 1)), ((0, 0), (e, 1)), ((1 - e, 0), (1, 1))):
            m.prism("gold", [(a[0] * size, a[1] * size), (b[0] * size, a[1] * size), (b[0] * size, b[1] * size),
                             (a[0] * size, b[1] * size)], o, u, v, w, depth * .8)


# ------------------------------------------------------------------ the barrel and the muzzle

def barrel_radius(x):
    """The stone drum's outer radius: .47 at the back cap, widening to .60 at the teal band."""
    return .47 + (x + 1.84) / (1.84 - .32) * .13


def build_barrel(m):
    # back cap: a gold dish with a rim
    m.ring("gold", [(-1.97, 0.0), (-1.96, .30), (-1.92, .44), (-1.86, .51), (-1.80, .53), (-1.80, .40), (-1.97, 0.0)],
           closed=True)
    m.ring("grout", [(-1.80, .44), (-.32, .57)], closed=False)                 # the core under the staves
    # stone staves: 14 round, two courses with a cross joint, a small gap between each
    staves, gap = 14, math.radians(1.6)
    for course, (x0, x1) in enumerate(((-1.80, -1.13), (-1.11, -.32))):
        for i in range(staves):
            a0 = 2 * math.pi * i / staves + gap / 2 + (course * math.pi / staves)
            a1 = a0 + 2 * math.pi / staves - gap
            r0, r1 = barrel_radius(x0), barrel_radius(x1)
            m.ring("cream", [(x0, r0 - .05), (x0, r0), (x1, r1), (x1, r1 - .05)], segments=4, a0=a0, a1=a1,
                   smooth=False)
    # flat gold hoops with rivets
    for x0, x1 in ((-1.78, -1.70), (-1.16, -1.08), (-.40, -.30)):
        rr = barrel_radius(x1) + .035
        m.ring("gold", [(x0, rr - .06), (x0, rr), (x1, rr), (x1, rr - .06)])
        for i in range(10):
            p, n = on_axis((x0 + x1) / 2, rr, 2 * math.pi * (i + .5) / 10)
            m.dome("gold", p, .022, n)
    # the gold strap along the top with rivets, and a lug at the back
    a = math.pi / 2
    m.ring("gold", [(-1.70, barrel_radius(-1.70) - .02), (-1.70, barrel_radius(-1.70) + .03),
                    (-.40, barrel_radius(-.40) + .03), (-.40, barrel_radius(-.40) - .02)],
           segments=2, a0=a - .09, a1=a + .09, smooth=False)
    for x in (-1.45, -1.25, -.95, -.75, -.55):
        p, n = on_axis(x, barrel_radius(x) + .03, a)
        m.dome("gold", p, .02, n)
    m.box("gold", (-1.62, -.05, AXIS_Z + .48), (-1.40, .05, AXIS_Z + .62))
    m.ring("gold", [(-1.55, .07), (-1.55, .11), (-1.47, .11), (-1.47, .07)], segments=24, about=(0.0, AXIS_Z + .70))
    # the teal band near the front with the big rook on each side, gold edges
    m.ring("teal", [(-.30, .54), (-.30, .605), (.36, .63), (.36, .54)])
    for x0, x1 in ((-.30, -.24), (.30, .36)):
        rr = .63 + (x1 - .36) * .04
        m.ring("gold", [(x0, rr - .05), (x0, rr + .025), (x1, rr + .025), (x1, rr - .05)])
    for s in (-1, 1):
        ang = math.radians(28)
        n = Vector((0, s * math.cos(ang), math.sin(ang)))
        up = Vector((0, -s * math.sin(ang), math.cos(ang)))
        right = Vector((1, 0, 0)) if s < 0 else Vector((-1, 0, 0))
        rook(m, Vector((.03, 0, AXIS_Z)) + n * .618, right, up, n, .46)


def build_muzzle(m):
    # the teal flared collar with rivets
    m.ring("teal", [(.36, .56), (.36, .65), (.72, .97), (.72, .84), (.40, .58)])
    for i in range(14):
        p, n = on_axis(.58, .83, 2 * math.pi * (i + .5) / 14)
        m.dome("gold", p, .028, (n + Vector((-.65, 0, 0))).normalized())
    # the thick gold rim, its lip and clamps
    # R383: its inner face stays just outside the steel duct (it lay on it, and textured the duct read gold)
    m.ring("gold", [(.66, .905), (.66, 1.02), (.72, 1.09), (1.12, 1.11), (1.21, 1.08), (1.27, 1.01), (1.27, .95),
                    (1.12, .945)])
    for x0, x1 in ((.84, .90), (1.02, 1.08)):                              # raised bands round the rim
        m.ring("gold", [(x0, 1.06), (x0, 1.135), (x1, 1.135), (x1, 1.06)])
    for i in range(12):                                                    # clamps from the collar onto the rim
        ang = 2 * math.pi * (i + .5) / 12
        m.box("gold", (.60, -.06, AXIS_Z + .93), (.80, .06, AXIS_Z + 1.10), rot_x=ang - math.pi / 2)
        p, n = on_axis(.72, 1.10, ang)
        m.dome("gold", p, .026, n)
    for i in range(18):
        p, n = on_axis(1.27, .98, 2 * math.pi * (i + .5) / 18)
        m.dome("gold", p, .022, (1, 0, 0))
    # the duct: dark steel lining facing in, a dark back wall behind the fan
    m.ring("steel", [(1.12, .93), (.80, .91), (.62, .87), (.42, .62), (.30, .44)], closed=False)
    m.ring("dark", [(.30, .44), (.30, 0.0)], closed=False)


def build_fan(blades=20):
    """Hub at the origin, blades in the y-z plane with a swirl; the fan looks toward +x."""
    m = Mesh()
    m.ring("gold", [(-.08, 0.0), (-.08, .17), (.02, .20), (.12, .17), (.19, .10), (.23, .03), (.24, 0.0)],
           segments=32, about=(0.0, 0.0), closed=False)
    m.ring("gold", [(-.10, .19), (-.10, .245), (.03, .245), (.03, .19)], segments=32, about=(0.0, 0.0))
    m.ring("dark", [(-.12, .14), (-.12, .27), (-.09, .27), (-.09, .14)], segments=32, about=(0.0, 0.0))
    bm = m.bm
    for i in range(blades):
        a0 = 2 * math.pi * i / blades
        rows = []
        steps = 8
        for k in range(steps + 1):
            t = k / steps
            rad = .24 + t * (.86 - .24)
            sweep = a0 + t * .85                       # the swirl
            chord = .19 - .06 * t
            pitch = math.radians(42 - 18 * t)
            centre = Vector((-.02 * t, rad * math.cos(sweep), rad * math.sin(sweep)))
            tangent = Vector((0, -math.sin(sweep), math.cos(sweep)))
            along = (tangent * math.cos(pitch) + Vector((1, 0, 0)) * math.sin(pitch)) * chord / 2
            thick = (Vector((1, 0, 0)) * math.cos(pitch) - tangent * math.sin(pitch)) * (.009 - .004 * t)
            rows.append([bm.verts.new(centre - along - thick), bm.verts.new(centre + along - thick),
                         bm.verts.new(centre + along + thick), bm.verts.new(centre - along + thick)])
        faces = []
        for ra, rb in zip(rows, rows[1:]):
            for j in range(4):
                k = (j + 1) % 4
                faces.append(bm.faces.new((ra[j], ra[k], rb[k], rb[j])))
        faces.append(bm.faces.new(rows[0][::-1]))
        faces.append(bm.faces.new(rows[-1]))
        m._closed(faces, "steel", True)
    return m.finish("Library cannon fan")


# ------------------------------------------------------------------ the mount and the plinth

def edge_strip(p0, p1, width, outward):
    """A quad (in the cheek's s-t plane) along p0 -> p1, width toward `outward` (a 2D point the strip leans away from)."""
    d = Vector((p1[0] - p0[0], p1[1] - p0[1]))
    nrm = Vector((-d.y, d.x)).normalized()
    mid = Vector(((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2))
    if (mid - Vector(outward)).dot(nrm) < 0:
        nrm = -nrm
    a, b = Vector(p0), Vector(p1)
    return [tuple(a), tuple(b), tuple(b + nrm * width), tuple(a + nrm * width)]


def build_mount(m):
    # the stepped turntable
    m.cylinder("teal", (TX, 0, .62), (TX, 0, .71), .97, 64)
    for i in range(20):
        a = 2 * math.pi * i / 20
        m.dome("gold", (TX + .97 * math.cos(a), .97 * math.sin(a), .665), .022, (math.cos(a), math.sin(a), 0))
    m.cylinder("gold", (TX, 0, .71), (TX, 0, .75), .92, 64)
    m.cylinder("dark", (TX, 0, .75), (TX, 0, .80), .86, 64)
    m.cylinder("gold", (TX, 0, .80), (TX, 0, .83), .80, 64)
    # A-frame cheeks: teal legs and head with a cut-out, gold edging and feet
    legs = (((-.66, .83), (-.40, .83), (-.10, 1.30), (-.30, 1.30)),
            ((.40, .83), (.66, .83), (.30, 1.30), (.10, 1.30)))
    head = ((-.34, 1.24), (.34, 1.24), (.28, 1.52), (-.28, 1.52))
    centre = (0.0, 1.0)
    for s in (-1, 1):
        y = s * .74
        org = (TX, y - s * .08, 0)
        for piece in legs + (head,):
            m.prism("teal", list(piece), org, (1, 0, 0), (0, 0, 1), (0, s, 0), .16)
        org2 = (TX, y - s * .10, 0)
        for leg in legs:
            outer = (leg[0], leg[3]) if leg[0][0] < 0 else (leg[1], leg[2])
            inner = (leg[1], leg[2]) if leg[0][0] < 0 else (leg[0], leg[3])
            m.prism("gold", edge_strip(outer[0], outer[1], .045, centre), org2, (1, 0, 0), (0, 0, 1), (0, s, 0), .2)
            m.prism("gold", edge_strip(inner[0], inner[1], .03, (outer[0][0], 1.0)), org2, (1, 0, 0), (0, 0, 1),
                    (0, s, 0), .2)
        m.prism("gold", [(-.30, 1.52), (.30, 1.52), (.30, 1.56), (-.30, 1.56)], org2, (1, 0, 0), (0, 0, 1), (0, s, 0), .2)
        for fx in (-.53, .53):                                        # gold feet
            m.box("gold", (TX + fx - .17, y - .14, .83), (TX + fx + .17, y + .14, .93))
            m.dome("gold", (TX + fx, y + s * .14, .88), .025, (0, s, 0))
        # stacked trunnion caps: pin, teal disc, gold disc, gold dome, dark bolt
        m.cylinder("gold", (TX, s * .50, AXIS_Z), (TX, s * .84, AXIS_Z), .17, 32)
        m.cylinder("teal", (TX, s * .82, AXIS_Z), (TX, s * .90, AXIS_Z), .31, 48)
        m.cylinder("gold", (TX, s * .90, AXIS_Z), (TX, s * .97, AXIS_Z), .26, 48)
        m.dome("gold", (TX, s * .97, AXIS_Z), .19, (0, s, 0), .55, 24)
        m.dome("dark", (TX, s * 1.07, AXIS_Z), .06, (0, s, 0), .7)
        for i in range(8):
            a = 2 * math.pi * i / 8
            m.dome("gold", (TX + .28 * math.cos(a), s * .90, AXIS_Z + .28 * math.sin(a)), .018, (0, s, 0))


def build_plinth(m):
    X0, X1, Y, TOP = -1.52, .62, 1.24, .58
    m.box("grout", (X0 + .03, -Y + .03, 0), (X1 - .03, Y - .03, TOP - .01))      # joints show through the gaps
    gap, deep = .014, .12
    # two courses of blocks round the sides, staggered, their faces on the plinth's outline
    for course, (z0, z1) in enumerate(((.02, .29), (.29, .50))):
        for along, ends in (("x", (X0, X1)), ("y", (-Y, Y))):
            length = ends[1] - ends[0]
            step = length / 4
            cuts = [ends[0] + (k + (.5 if course else 0)) * step for k in range(5)]
            edges = [ends[0]] + [c for c in cuts if ends[0] < c < ends[1]] + [ends[1]]
            for a, b in zip(edges, edges[1:]):
                for s in (-1, 1):
                    if along == "x":
                        y = s * Y
                        m.box("cream", (a + gap / 2, min(y, y - s * deep), z0 + gap / 2),
                              (b - gap / 2, max(y, y - s * deep), z1 - gap / 2))
                    else:
                        x = X0 if s < 0 else X1
                        m.box("cream", (min(x, x - s * deep), a + gap / 2, z0 + gap / 2),
                              (max(x, x - s * deep), b - gap / 2, z1 - gap / 2))
    # the slab top in a 4 x 3 grid
    for i in range(4):
        for j in range(3):
            xa = X0 + (X1 - X0) * i / 4
            xb = X0 + (X1 - X0) * (i + 1) / 4
            ya = -Y + 2 * Y * j / 3
            yb = -Y + 2 * Y * (j + 1) / 3
            m.box("cream", (xa + gap / 2, ya + gap / 2, .50), (xb - gap / 2, yb - gap / 2, TOP))
    # teal panels in gold frames on all four sides; a gold-framed rook plaque on the long sides
    for s in (-1, 1):
        y = s * Y
        m.box("teal", (X0 + .40, min(y, y + s * .02), .10), (X1 - .40, max(y, y + s * .02), .46))
        for lo, hi in (((X0 + .37, .07), (X1 - .37, .11)), ((X0 + .37, .45), (X1 - .37, .49)),
                       ((X0 + .37, .07), (X0 + .41, .49)), ((X1 - .41, .07), (X1 - .37, .49))):
            m.box("gold", (lo[0], min(y, y + s * .035), lo[1]), (hi[0], max(y, y + s * .035), hi[1]))
        cx = (X0 + X1) / 2
        m.box("teal", (cx - .19, min(y, y + s * .045), .12), (cx + .19, max(y, y + s * .045), .44))
        right = (1, 0, 0) if s < 0 else (-1, 0, 0)
        rook(m, (cx, y + s * .045, .28), right, (0, 0, 1), (0, s, 0), .32, frame=True)
        for x in (X0 + .55, X1 - .55):
            m.dome("gold", (x, y + s * .02, .28), .025, (0, s, 0))
    for s in (-1, 1):
        x = X0 if s < 0 else X1
        m.box("teal", (min(x, x + s * .02), -Y + .40, .10), (max(x, x + s * .02), Y - .40, .46))
        for lo, hi in (((-Y + .37, .07), (Y - .37, .11)), ((-Y + .37, .45), (Y - .37, .49)),
                       ((-Y + .37, .07), (-Y + .41, .49)), ((Y - .41, .07), (Y - .37, .49))):
            m.box("gold", (min(x, x + s * .035), lo[0], lo[1]), (max(x, x + s * .035), hi[0], hi[1]))
    # chamfered gold corner caps with a top plate and rivets
    for cx, sx in ((X0, -1), (X1, 1)):
        for cy, sy in ((-Y, -1), (Y, 1)):
            m.box("gold", (cx - .19, cy - .19, 0), (cx + .19, cy + .19, .64))
            m.box("gold", (cx - .16, cy - .16, .64), (cx + .16, cy + .16, .69))
            for nx, ny in ((sx, 0), (0, sy)):
                for z in (.16, .46):
                    for k in (-.08, .08):
                        p = Vector((cx + nx * .19 + (k if nx == 0 else 0), cy + ny * .19 + (k if ny == 0 else 0), z))
                        m.dome("gold", p, .026, (nx, ny, 0))
            m.dome("gold", (cx, cy, .69), .05, (0, 0, 1))


def build_body():
    m = Mesh()
    build_plinth(m)
    build_mount(m)
    build_barrel(m)
    build_muzzle(m)
    return m.finish("Library cannon")


def bevel(ob, width=.008):
    mod = ob.modifiers.new("Bevel", "BEVEL")
    mod.width, mod.segments, mod.limit_method, mod.angle_limit = width, 2, "ANGLE", math.radians(40)
    mod.harden_normals = True


# ------------------------------------------------------------------ preview

def preview(out):
    from cannon_conveyor_look import ENV, shoot, stage
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=ENV)
    sc = bpy.context.scene
    body, fan = build_body(), build_fan()
    print("CANNON faces", len(body.polygons), "fan faces", len(fan.polygons))
    for o in list(bpy.data.objects):
        if o.name.startswith("BLOWER_") and o.type == "MESH":
            o.data = fan if o.name.endswith("_Fan") else body
            bevel(o, .003 if o.name.endswith("_Fan") else .012)
    for o in bpy.data.objects:   # the cannon on the stone floor round it
        if o.type == "LIGHT" or (o.type == "MESH" and "BLOWER" not in o.name
                                 and not o.name.startswith("GEO_fitted_stone")):
            o.hide_render = True
    stone = bpy.data.materials.get("CLAY | uniform neutral gray")
    stone.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.70, .58, .47, 1)
    sun, cam = stage(sc)
    shoot(sc, sun, cam, out, (
        ("cannon_hero.png", (-6.0, -9.2, 2.6), (-10.9, -4.0, 1.25), 40, 40),
        ("cannon_side.png", (-11.0, -9.9, 1.9), (-10.9, -4.0, 1.2), 35, 10),
        ("cannon_back.png", (-15.6, -1.0, 2.8), (-11.0, -4.0, 1.2), 38, 150),
        ("cannon_muzzle.png", (-7.0, -4.8, 1.8), (-10.2, -4.0, 1.5), 40, 80),
    ))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" not in argv:
        sys.exit("Preview only for now (not applied to Unity): -- --preview <folder>")
    preview(argv[argv.index("--preview") + 1])
