"""The royal palace ahead of Pawn Rush course 01 (승규 10-22: "진짜 압도할 만한 성으로 만들어줘", no blue roofs).

blender --background --factory-startup --python build_palace.py -- <out_dir> [preview]
One model, origin at the centre of the outer ward's paving (z = 0), the gate looking along -Y.
  rock      a floating crag ~170 m deep under the ward, in strata, with hanging spurs and three waterfalls
  outer     curtain walls 22 m with corbelled (machicolated) crenellations, 4 corner and 6 side towers
            with round corner turrets, a twin-towered gatehouse 62 m high with an arched gate and portcullis
  inner     a grand staircase to a terrace 20 m up, two great halls with buttresses, two needle towers
            joined to the keep by flying bridges
  keep      three tiers: a hall block with four corner towers, a second tier with a rose window and
            turrets, a round belvedere, and a colossal gold crown on top (~190 m above the ward)
  viaduct   an arched stone sky bridge from the gate toward the course, ending VIA_END_Y in front of it
Materials (Unity maps them by name): Palace_Stone, Palace_Trim, Palace_Dark, Palace_Rock, Palace_Window,
Gold, Iron, Banner_Red, Banner_Navy, Gem_Red, Palace_Water. Stone and rock get world-scale box UVs (one
UV unit = UV_M metres), water gets ribbon UVs (u across, v down the fall).
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

args = sys.argv[sys.argv.index("--") + 1:]
OUT = args[0]
PREVIEW = len(args) > 1 and args[1] == "preview"
os.makedirs(OUT, exist_ok=True)
rng = random.Random(1022)

UV_M = 4.0                  # metres per UV unit on stone (the ashlar texture covers 4 x 4 m)
ROCK_UV_M = 24.0
VIA_END_Y = -155.0          # the viaduct's far end (the approach islet's landing)
VIA_END_Z = -8.0            # ... and its height there (the approach islet is 8 m below the ward)

PAL = {
    "Palace_Stone": (0.84, 0.78, 0.66, 0.0, 0.62),
    "Palace_Trim": (0.92, 0.88, 0.79, 0.0, 0.5),
    "Palace_Dark": (0.6, 0.54, 0.46, 0.0, 0.7),
    "Palace_Rock": (0.56, 0.48, 0.38, 0.0, 0.85),
    "Palace_Window": (0.06, 0.06, 0.08, 0.0, 0.12),
    "Gold": (0.93, 0.7, 0.2, 1.0, 0.28),
    "Iron": (0.2, 0.2, 0.22, 0.8, 0.45),
    "Banner_Red": (0.68, 0.1, 0.12, 0.0, 0.7),
    "Banner_Navy": (0.13, 0.2, 0.45, 0.0, 0.7),
    "Gem_Red": (0.75, 0.04, 0.08, 0.0, 0.08),
    "Palace_Water": (0.75, 0.88, 0.97, 0.0, 0.05),
}
BOX_UV = {"Palace_Stone", "Palace_Trim", "Palace_Dark"}


class Geo:
    """Geometry collected straight into one bmesh per material."""

    def __init__(self):
        self.bms = {}
        self.uv_done = set()

    def bm(self, mat):
        if mat not in self.bms:
            b = bmesh.new()
            b.loops.layers.uv.new("UVMap")
            self.bms[mat] = b
        return self.bms[mat]

    def quad_mesh(self, mat, verts, faces):
        b = self.bm(mat)
        vs = [b.verts.new(v) for v in verts]
        out = []
        for f in faces:
            try:
                out.append(b.faces.new([vs[i] for i in f]))
            except ValueError:
                pass
        return out

    def obox(self, mat, c, ax, ay, az, hx, hy, hz):
        """Oriented box: centre c, unit axes ax/ay/az, half sizes."""
        c, ax, ay, az = Vector(c), Vector(ax), Vector(ay), Vector(az)
        v = [c + ax * sx * hx + ay * sy * hy + az * sz * hz for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        return self.quad_mesh(mat, v, [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)])

    def box(self, mat, x0, x1, y0, y1, z0, z1):
        if x1 < x0:
            x0, x1 = x1, x0
        if y1 < y0:
            y0, y1 = y1, y0
        return self.obox(mat, ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (1, 0, 0), (0, 1, 0), (0, 0, 1),
                         (x1 - x0) / 2, (y1 - y0) / 2, (z1 - z0) / 2)

    def frustum(self, mat, cx, cy, w0, d0, w1, d1, z0, z1):
        v = [(cx - w0 / 2, cy - d0 / 2, z0), (cx + w0 / 2, cy - d0 / 2, z0), (cx + w0 / 2, cy + d0 / 2, z0), (cx - w0 / 2, cy + d0 / 2, z0),
             (cx - w1 / 2, cy - d1 / 2, z1), (cx + w1 / 2, cy - d1 / 2, z1), (cx + w1 / 2, cy + d1 / 2, z1), (cx - w1 / 2, cy + d1 / 2, z1)]
        return self.quad_mesh(mat, v, [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)])

    def cyl(self, mat, cx, cy, r0, r1, z0, z1, segs=64, caps=True):
        v = []
        for z, r in ((z0, r0), (z1, r1)):
            for i in range(segs):
                a = 2 * math.pi * i / segs
                v.append((cx + r * math.cos(a), cy + r * math.sin(a), z))
        f = [(i, (i + 1) % segs, segs + (i + 1) % segs, segs + i) for i in range(segs)]
        if caps:
            f.append(tuple(range(segs - 1, -1, -1)))
            f.append(tuple(range(segs, 2 * segs)))
        return self.quad_mesh(mat, v, f)

    def ring(self, mat, cx, cy, r_in, r_out, z0, z1, segs=64):
        v = []
        for z in (z0, z1):
            for r in (r_in, r_out):
                for i in range(segs):
                    a = 2 * math.pi * i / segs
                    v.append((cx + r * math.cos(a), cy + r * math.sin(a), z))
        def idx(zi, ri, i):
            return zi * 2 * segs + ri * segs + i % segs
        f = []
        for i in range(segs):
            f.append((idx(0, 1, i), idx(0, 1, i + 1), idx(1, 1, i + 1), idx(1, 1, i)))   # outer
            f.append((idx(0, 0, i + 1), idx(0, 0, i), idx(1, 0, i), idx(1, 0, i + 1)))   # inner
            f.append((idx(1, 0, i), idx(1, 1, i), idx(1, 1, i + 1), idx(1, 0, i + 1)))   # top
            f.append((idx(0, 0, i + 1), idx(0, 1, i + 1), idx(0, 1, i), idx(0, 0, i)))   # bottom
        return self.quad_mesh(mat, v, f)

    def sphere(self, mat, c, r, segs=16):
        b = self.bm(mat)
        bmesh.ops.create_uvsphere(b, u_segments=segs, v_segments=max(6, segs // 2), radius=r, matrix=Matrix.Translation(Vector(c)))

    def prism(self, mat, pts, a0, a1, plane):
        """2D polygon extruded across a0..a1. plane 'XZ': pts (x, z), along y; 'YZ': (y, z) along x; 'XY': (x, y) along z."""
        def P(u, v, w):
            if plane == "XZ":
                return (u, w, v)
            if plane == "YZ":
                return (w, u, v)
            return (u, v, w)
        n = len(pts)
        v = [P(u, w, a0) for u, w in pts] + [P(u, w, a1) for u, w in pts]
        f = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
        return self.quad_mesh(mat, v, f)


G = Geo()


# ---------------------------------------------------------------- building blocks

def arch_pts(cx, spring, w, n=12):
    """The arch's semicircle, right to left."""
    r = w / 2
    return [(cx + r * math.cos(math.pi * i / n), spring + r * math.sin(math.pi * i / n)) for i in range(n + 1)]


def window(face, at, along_axis, w, h, out_sign, glow=True, frame=0.32):
    """An arched window on a wall face. face: 'x' (the face's normal is +-x, plane YZ) or 'y' (normal +-y, plane XZ).
    at: (u, z) of the opening's centre bottom (u = coordinate along the face), out_sign: +1/-1, the face position along_axis.
    v2: the frame stands 0.7 m proud so the glass sits deep in its reveal, a hood mould over the arch with
    label stops, a moulded sill on corbels, and a mullion and transom in the big windows."""
    u, z0 = at
    spring = z0 + h - w / 2
    glass = [(u + w / 2, z0), (u + w / 2, spring)] + arch_pts(u, spring, w, 16)[1:-1] + [(u - w / 2, spring), (u - w / 2, z0)]
    plane = "YZ" if face == "x" else "XZ"
    p = along_axis
    o = out_sign

    def B(mat, u0, u1, d0, d1, z_0, z_1):    # u along the face, d out of it
        if face == "x":
            G.box(mat, p + o * d0, p + o * d1, u0, u1, z_0, z_1)
        else:
            G.box(mat, u0, u1, p + o * d0, p + o * d1, z_0, z_1)

    def O(mat, cu, d, cz, tang, rad, hd, ht, hr):   # an oriented block on the arch
        if face == "x":
            G.obox(mat, (p + o * d, cu, cz), (1, 0, 0), (0, tang[0], tang[1]), (0, rad[0], rad[1]), hd, ht, hr)
        else:
            G.obox(mat, (cu, p + o * d, cz), (0, 1, 0), (tang[0], 0, tang[1]), (rad[0], 0, rad[1]), hd, ht, hr)

    G.prism("Palace_Window", glass, p + o * 0.02, p + o * 0.1, plane)
    depth = 0.7
    # jambs in courses (alternately wider), sill on two corbels
    courses = max(2, int((spring - z0) / 0.9))
    for s_ in (-1, 1):
        for k in range(courses):
            za = z0 + (spring - z0) * k / courses
            zb = z0 + (spring - z0) * (k + 1) / courses
            wide = frame + (0.18 if k % 2 == 0 else 0.0)
            a, b = u + s_ * (w / 2), u + s_ * (w / 2 + wide)
            B("Palace_Trim", min(a, b), max(a, b), 0, depth, za + 0.02, zb - 0.02)
    B("Palace_Trim", u - w / 2 - frame - 0.3, u + w / 2 + frame + 0.3, 0, depth + 0.25, z0 - 0.35, z0)
    B("Palace_Trim", u - w / 2 - frame - 0.15, u + w / 2 + frame + 0.15, 0, depth + 0.1, z0 - 0.5, z0 - 0.35)
    for s_ in (-1, 1):
        cu = u + s_ * (w / 2 + frame * 0.5)
        B("Palace_Stone", cu - 0.2, cu + 0.2, 0, depth, z0 - 1.1, z0 - 0.5)
    # the arch: voussoirs, a keystone, and a hood mould over it ending in label stops
    n = 11
    r = w / 2 + frame / 2
    for i in range(n):
        a = math.pi * (i + 0.5) / n
        O("Palace_Trim", u + r * math.cos(a), depth / 2, spring + r * math.sin(a), (-math.sin(a), math.cos(a)), (math.cos(a), math.sin(a)),
          depth / 2, math.pi * r / n / 2 - 0.02, frame / 2 + (0.06 if i % 2 else 0.0))
    B("Palace_Trim", u - 0.32, u + 0.32, 0, depth + 0.15, spring + w / 2 - 0.05, spring + w / 2 + frame + 0.4)
    rh = w / 2 + frame + 0.28
    for i in range(14):
        a = math.pi * (i + 0.5) / 14
        O("Palace_Trim", u + rh * math.cos(a), depth * 0.5 + 0.08, spring + rh * math.sin(a), (-math.sin(a), math.cos(a)), (math.cos(a), math.sin(a)),
          depth * 0.5 + 0.08, math.pi * rh / 14 / 2 + 0.03, 0.13)
    for s_ in (-1, 1):
        cu = u + s_ * rh
        B("Palace_Trim", cu - 0.22, cu + 0.22, 0, depth + 0.16, spring - 0.55, spring + 0.05)
    # mullion and transom
    if w >= 2.3:
        B("Palace_Trim", u - 0.12, u + 0.12, 0, 0.32, z0, spring + w / 2 - 0.15)
        zt = z0 + (spring - z0) * 0.62
        B("Palace_Trim", u - w / 2, u + w / 2, 0, 0.3, zt - 0.11, zt + 0.11)


def quoins(cx, cy, w, z0, z1, course=1.4, proud=0.16):
    """Corner stones up the four corners of a square shaft: long and short faces alternating course by course."""
    k = 0
    z = z0
    while z + course <= z1 + 0.01:
        for sx in (-1, 1):
            for sy in (-1, 1):
                long_x = (k % 2 == 0) == (sx * sy > 0)
                lx, ly = (2.3, 1.3) if long_x else (1.3, 2.3)
                x_out, y_out = cx + sx * (w / 2 + proud), cy + sy * (w / 2 + proud)
                G.box("Palace_Trim", x_out - sx * lx, x_out, y_out - sy * ly, y_out, z + 0.04, z + course - 0.04)
        z += course
        k += 1


def crenel_edge(axis, a0, a1, line, side, z, overhang=0.8, merlon=1.9, gap=1.3, thick=1.1, corbels=True):
    """A machicolated crenellation along an axis-aligned wall top edge.
    axis 'x': the edge runs along x from a0 to a1 at y = line; side (+1/-1) is the outward direction (y).
    axis 'y': runs along y at x = line, outward in x."""
    lo, hi = min(a0, a1), max(a0, a1)

    def B(mat, u0, u1, o0, o1, z0, z1):   # u along the edge, o across (0 = wall face, + = outward)
        c0, c1 = line + side * o0, line + side * o1
        if axis == "x":
            G.box(mat, u0, u1, c0, c1, z0, z1)
        else:
            G.box(mat, c0, c1, u0, u1, z0, z1)
    # parapet base, overhanging the face by `overhang`
    B("Palace_Stone", lo, hi, -thick + overhang, overhang, z, z + 1.15)
    B("Palace_Trim", lo, hi, -thick + overhang - 0.05, overhang + 0.12, z - 0.25, z)  # drip moulding
    # merlons
    n = max(1, int((hi - lo + gap) / (merlon + gap)))
    span = n * merlon + (n - 1) * gap
    start = lo + (hi - lo - span) / 2
    for i in range(n):
        u0 = start + i * (merlon + gap)
        B("Palace_Stone", u0, u0 + merlon, -thick + overhang, overhang, z + 1.15, z + 2.6)
        B("Palace_Trim", u0 - 0.06, u0 + merlon + 0.06, -thick + overhang - 0.06, overhang + 0.06, z + 2.6, z + 2.8)
    if corbels:
        k = max(1, int((hi - lo) / 1.9))
        for i in range(k):
            u = lo + (i + 0.5) * (hi - lo) / k
            for j, (o, h) in enumerate(((0.3, 0.55), (0.55, 0.5), (overhang, 0.5))):
                B("Palace_Trim" if j == 2 else "Palace_Stone", u - 0.32, u + 0.32, -0.05, o, z - 0.25 - 0.55 * (3 - j), z - 0.25 - 0.55 * (2 - j))


def crenel_ring(cx, cy, r, z, n=10):
    """Merlons round a round turret top."""
    G.ring("Palace_Stone", cx, cy, r - 0.7, r + 0.15, z, z + 1.0, 64)
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        c = (cx + (r - 0.27) * math.cos(a), cy + (r - 0.27) * math.sin(a), z + 1.7)
        G.obox("Palace_Stone", c, (math.cos(a), math.sin(a), 0), (-math.sin(a), math.cos(a), 0), (0, 0, 1), 0.42, 0.62, 0.7)


def bartizan(cx, cy, r, z_top):
    """A round corner turret corbelled out of a tower's top."""
    G.cyl("Palace_Stone", cx, cy, 0.3, r, z_top - 9, z_top - 6, 64)          # the corbelled cone
    for i in range(3):
        G.cyl("Palace_Trim", cx, cy, r * (0.55 + 0.15 * i) + 0.1, r * (0.55 + 0.15 * i) + 0.1, z_top - 8 + i, z_top - 7.8 + i, 64)
    G.cyl("Palace_Stone", cx, cy, r, r, z_top - 6, z_top + 1.5, 64)
    G.cyl("Palace_Trim", cx, cy, r + 0.2, r + 0.2, z_top + 1.5, z_top + 1.8, 64)
    crenel_ring(cx, cy, r + 0.1, z_top + 1.8)
    for i in range(3):   # arrow slits
        a = 2 * math.pi * i / 3 + 0.4
        c = (cx + (r + 0.03) * math.cos(a), cy + (r + 0.03) * math.sin(a), z_top - 2)
        G.obox("Palace_Window", c, (math.cos(a), math.sin(a), 0), (-math.sin(a), math.cos(a), 0), (0, 0, 1), 0.06, 0.22, 1.3)


def tower(cx, cy, w, z0, z1, win_levels=(), win_w=1.6, win_h=4.2, turrets=True, string_every=12.0):
    """A square tower: battered plinth, shaft, string courses, windows, machicolated crenellation, corner turrets."""
    G.frustum("Palace_Dark", cx, cy, w + 5, w + 5, w + 0.6, w + 0.6, z0, z0 + 9)
    G.frustum("Palace_Trim", cx, cy, w + 0.9, w + 0.9, w + 0.9, w + 0.9, z0 + 9, z0 + 9.5)
    G.box("Palace_Stone", cx - w / 2, cx + w / 2, cy - w / 2, cy + w / 2, z0 + 9, z1)
    quoins(cx, cy, w, z0 + 9.5, z1 - 4.5)
    z = z0 + 9 + string_every
    while z < z1 - 6:
        G.box("Palace_Trim", cx - w / 2 - 0.35, cx + w / 2 + 0.35, cy - w / 2 - 0.35, cy + w / 2 + 0.35, z, z + 0.45)
        z += string_every
    for zw in win_levels:
        cols = [0.0] if w < 13 else [-w / 4, w / 4]
        for u in cols:
            window("y", (cx + u, zw), cy - w / 2, win_w, win_h, -1)
            window("y", (cx + u, zw), cy + w / 2, win_w, win_h, 1)
            window("x", (cy + u, zw), cx - w / 2, win_w, win_h, -1)
            window("x", (cy + u, zw), cx + w / 2, win_w, win_h, 1)
    inset = 2.6 if turrets else 0.0
    crenel_edge("x", cx - w / 2 + inset, cx + w / 2 - inset, cy - w / 2, -1, z1)
    crenel_edge("x", cx - w / 2 + inset, cx + w / 2 - inset, cy + w / 2, 1, z1)
    crenel_edge("y", cy - w / 2 + inset, cy + w / 2 - inset, cx - w / 2, -1, z1)
    crenel_edge("y", cy - w / 2 + inset, cy + w / 2 - inset, cx + w / 2, 1, z1)
    G.box("Palace_Dark", cx - w / 2 + 1, cx + w / 2 - 1, cy - w / 2 + 1, cy + w / 2 - 1, z1, z1 + 0.6)   # the roof walk
    if turrets:
        for sx in (-1, 1):
            for sy in (-1, 1):
                bartizan(cx + sx * (w / 2 - 0.9), cy + sy * (w / 2 - 0.9), 2.3 if w >= 14 else 1.8, z1)


def wall(axis, a0, a1, line, side, thick, z0, z1, windows=False):
    """A curtain wall along an axis; side = the outward direction; battered base, string course, crenellation."""
    lo, hi = min(a0, a1), max(a0, a1)
    o0, o1 = line - side * thick, line
    if axis == "x":
        G.box("Palace_Stone", lo, hi, o0, o1, z0 + 6, z1)
        G.prism("Palace_Dark", [(o0 if side > 0 else o1, z0), (line + side * 2.5, z0), (line, z0 + 6), (o0 if side > 0 else o1, z0 + 6)][::(1 if side > 0 else -1)], lo, hi, "YZ") if False else None
        G.box("Palace_Dark", lo, hi, o0, line + side * 1.6, z0, z0 + 6)
        G.box("Palace_Trim", lo, hi, o0, line + side * 0.3, z0 + 6, z0 + 6.4)
    else:
        G.box("Palace_Stone", o0, o1, lo, hi, z0 + 6, z1)
        G.box("Palace_Dark", o0, line + side * 1.6, lo, hi, z0, z0 + 6)
        G.box("Palace_Trim", o0, line + side * 0.3, lo, hi, z0 + 6, z0 + 6.4)
    # v2: buttresses every ~14 m, stepped back twice, with sloped weatherings
    nb = max(1, int((hi - lo) / 14))
    for i in range(nb + 1):
        u = lo + 3 + i * (hi - lo - 6) / nb
        for (d, za, zb) in ((1.4, z0 + 6, z0 + 11), (1.0, z0 + 11, z0 + 16), (0.6, z0 + 16, z1 - 3.5)):
            if axis == "x":
                G.box("Palace_Stone", u - 1.1, u + 1.1, line, line + side * d, za, zb)
                G.prism("Palace_Trim", [(line, zb), (line + side * d, zb), (line, zb + d * 0.8)][::side], u - 1.15, u + 1.15, "YZ")
            else:
                G.box("Palace_Stone", line, line + side * d, u - 1.1, u + 1.1, za, zb)
                G.prism("Palace_Trim", [(line, zb), (line + side * d, zb), (line, zb + d * 0.8)][::side], u - 1.15, u + 1.15, "XZ")
    crenel_edge(axis, lo, hi, line, side, z1)
    crenel_edge(axis, lo, hi, line - side * thick, -side, z1, overhang=0.0, corbels=False)   # inner parapet
    # arrow slits along the face
    n = int((hi - lo) / 7)
    for i in range(n):
        u = lo + (i + 0.5) * (hi - lo) / n
        for zz in (z0 + 10, z0 + 16):
            if axis == "x":
                G.box("Palace_Window", u - 0.2, u + 0.2, line, line + side * 0.08, zz, zz + 2.4)
            else:
                G.box("Palace_Window", line, line + side * 0.08, u - 0.2, u + 0.2, zz, zz + 2.4)


def banner(x, y_face, z_top, w, h, mat, side=-1):
    """A long hanging banner on a -Y (or +Y) face: v2 folded cloth (a grid rippled in and out, both sides),
    a swallowtail, gold edging and a gold fringe, a gold rod with finials, the gold crown device."""
    y = y_face + side * 0.4
    tail = h * 0.12
    cols, rows = 28, 24
    folds = max(3, int(w / 1.4))
    b = G.bm(mat)

    def P(i, j, back):
        u = i / cols
        t = j / rows
        xx = x - w / 2 + w * u
        # the swallowtail: the hem rises to the middle
        hem = z_top - h + tail * (1 - abs(2 * u - 1))
        zz = z_top - (z_top - hem) * t
        bulge = math.sin(u * math.pi * folds * 2) * 0.22 * (0.4 + 0.6 * t) + math.sin(u * math.pi * 3 + t * 2) * 0.12
        return Vector((xx, y + side * (0.12 + bulge + (0.0 if back else 0.08)), zz))
    # a closed thin shell (front, back and the rim stitched), so its normals come out outward
    sheets = []
    for back in (False, True):
        vs = [[b.verts.new(P(i, j, back)) for i in range(cols + 1)] for j in range(rows + 1)]
        for j in range(rows):
            for i in range(cols):
                b.faces.new((vs[j][i], vs[j][i + 1], vs[j + 1][i + 1], vs[j + 1][i]))
        sheets.append(vs)
    F, K = sheets
    rim = [(0, i) for i in range(cols + 1)] + [(j, cols) for j in range(1, rows + 1)] +           [(rows, i) for i in range(cols - 1, -1, -1)] + [(j, 0) for j in range(rows - 1, 0, -1)]
    for k in range(len(rim)):
        (j0, i0), (j1, i1) = rim[k], rim[(k + 1) % len(rim)]
        b.faces.new((F[j0][i0], F[j1][i1], K[j1][i1], K[j0][i0]))
    # gold edging down both sides and a fringe along the hem
    for i in (0, cols):
        for j in range(rows):
            a, c = P(i, j, False), P(i, j + 1, False)
            m = (a + c) / 2
            G.obox("Gold", m, (1, 0, 0), (0, 1, 0), (0, 0, 1), 0.12, 0.14, (a - c).length / 2 + 0.02)
    for i in range(cols):
        a = P(i + 0.5, rows, False)
        G.box("Gold", a.x - 0.06, a.x + 0.06, a.y - 0.06, a.y + 0.06, a.z - 0.9, a.z)
    G.cyl("Gold", 0, 0, 0, 0, 0, 0, 3) if False else None
    G.box("Gold", x - w / 2 - 0.6, x + w / 2 + 0.6, y - 0.25, y + side * 0.45, z_top, z_top + 0.5)
    for s_ in (-1, 1):
        G.sphere("Gold", (x + s_ * (w / 2 + 0.75), y + side * 0.1, z_top + 0.25), 0.5, 16)
    # the crown device on the cloth (follows the fold at its height roughly)
    cz = z_top - h * 0.28
    dy = side * 0.5
    G.box("Gold", x - w * 0.22, x + w * 0.22, y + dy, y + dy + side * 0.1, cz, cz + w * 0.1)
    for s_ in (-1, 0, 1):
        G.prism("Gold", [(x + s_ * w * 0.17 - w * 0.06, cz + w * 0.1), (x + s_ * w * 0.17 + w * 0.06, cz + w * 0.1), (x + s_ * w * 0.17, cz + w * 0.26)],
                y + dy, y + dy + side * 0.1, "XZ")
        G.sphere("Gold", (x + s_ * w * 0.17, y + dy + side * 0.05, cz + w * 0.29), w * 0.03, 10)


# ---------------------------------------------------------------- the rock

def rock():
    """An inverted crag under the ward: rings from a rounded-rectangle rim (just outside the walls) down to a
    point, pushed in and out by noise, stepped into strata, with a few hanging spurs."""
    b = G.bm("Palace_Rock")
    rings, segs = 120, 360
    depth = 170.0
    rows = []
    for k in range(rings + 1):
        t = k / rings
        z = -depth * (t ** 1.15)
        n_exp = 4.5 - 2.5 * t                     # squarish at the top, round lower down
        ax, ay = 112 * (1 - t ** 1.6) + 2, 102 * (1 - t ** 1.6) + 2
        # v2: ledges: each stratum bulges out at its top and tucks in at its foot
        row = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ca, sa = math.cos(a), math.sin(a)
            # superellipse rim
            rx = ax * math.copysign(abs(ca) ** (2 / n_exp), ca)
            ry = ay * math.copysign(abs(sa) ** (2 / n_exp), sa)
            p = Vector((rx, ry, z))
            # ledges: each stratum bulges at its top, tucks in at its foot; the strata wander up and down
            # round the crag and break off here and there, so they do not read as stacked plates
            band = (t * 22 + noise.noise(Vector((ca * 1.7, sa * 1.7, t * 3.0))) * 1.4) % 1.0
            gate = 0.5 + 0.5 * noise.noise(Vector((ca * 3.1, sa * 3.1, t * 9.0 + 5.0)))
            strata = 1.0 + (0.034 * (1 - band) ** 3 - 0.014 * band) * gate
            if 0 < k < rings:
                nn = noise.noise(Vector((p.x * 0.025, p.y * 0.025, z * 0.02))) * 0.18 \
                    + noise.noise(Vector((p.x * 0.08, p.y * 0.08, z * 0.06))) * 0.06 \
                    + noise.noise(Vector((p.x * 0.22, p.y * 0.22, z * 0.2))) * 0.022 \
                    + noise.noise(Vector((p.x * 0.6, p.y * 0.6, z * 0.5))) * 0.008
                f = (1 + nn) * strata
                p = Vector((p.x * f, p.y * f, z + noise.noise(Vector((p.x * 0.05, p.y * 0.05, 3.1))) * 3.5 * t))
            row.append(b.verts.new(p))
        rows.append(row)
    for k in range(rings):
        for i in range(segs):
            j = (i + 1) % segs
            b.faces.new((rows[k][i], rows[k][j], rows[k + 1][j], rows[k + 1][i]))
    b.faces.new(rows[0])                       # the top (under the paving)
    tip = b.verts.new((0, 0, -depth - 28))
    for i in range(segs):
        b.faces.new((rows[-1][(i + 1) % segs], rows[-1][i], tip))
    # hanging spurs
    for s in range(16):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(25, 75)
        x, y = r * math.cos(a), r * math.sin(a)
        top = -depth * (1 - (r / 110) ** 1.2) * 0.75 - 10
        spur(x, y, rng.uniform(5, 12), top, rng.uniform(25, 60))


def spur(x, y, r, top, length):
    """A hanging rock spur: a cone in rings, its radius pushed in and out by noise, ledged like the crag."""
    b = G.bm("Palace_Rock")
    rings, segs = 26, 40
    rows = []
    for k in range(rings + 1):
        t = k / rings
        z = top - length * t
        rr = r * (1 - t) ** 0.8 + 0.3
        band = (t * 7) % 1.0
        rr *= 1 + 0.08 * (1 - band) ** 3
        row = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            q = Vector((x + rr * math.cos(a), y + rr * math.sin(a), z))
            f = 1 + noise.noise(Vector((q.x * 0.12, q.y * 0.12, z * 0.1))) * 0.25 + noise.noise(Vector((q.x * 0.4, q.y * 0.4, z * 0.3))) * 0.08
            row.append(b.verts.new(Vector((x + rr * f * math.cos(a), y + rr * f * math.sin(a), z))))
        rows.append(row)
    for k in range(rings):
        for i in range(segs):
            j = (i + 1) % segs
            b.faces.new((rows[k][i], rows[k][j], rows[k + 1][j], rows[k + 1][i]))
    b.faces.new(rows[0][::-1])


def waterfall(x, y, out, width, drop=165.0):
    """A ribbon of falling water from the rim: shoots out a little, then falls; widens into spray lower down.
    v2: rounded across (9 columns bowed outward) and 80 rows; u runs across, v down (one unit = 12 m)."""
    b = G.bm("Palace_Water")
    uv = b.loops.layers.uv.active
    out = Vector((out[0], out[1], 0.0)).normalized()
    side = Vector((-out.y, out.x, 0))
    n, m = 80, 9
    rows = []
    for k in range(n + 1):
        t = k / n
        z = -0.6 - drop * t
        reach = 1.5 + 9 * math.sqrt(t)
        w = width * (1 + 1.4 * t)
        c = Vector((x, y, 0)) + out * reach + Vector((0, 0, z))
        wob = noise.noise(Vector((x * 0.1, t * 6, 1.7))) * 2.5 * t
        c += side * wob
        row = []
        for j in range(m + 1):
            a = j / m * 2 - 1
            row.append(b.verts.new(c + side * a * w / 2 + out * (1 - a * a) * w * 0.18))
        rows.append(row)
    for k in range(n):
        for j in range(m):
            f = b.faces.new((rows[k][j], rows[k][j + 1], rows[k + 1][j + 1], rows[k + 1][j]))
            for loop, (u, v) in zip(f.loops, ((j / m, k / n), ((j + 1) / m, k / n), ((j + 1) / m, (k + 1) / n), (j / m, (k + 1) / n))):
                loop[uv].uv = (u, v * drop / 12)
    G.uv_done.add("Palace_Water")


# ---------------------------------------------------------------- the palace

def paving():
    G.box("Palace_Stone", -108, 108, -98, 98, -2.5, 0)


def outer_ward():
    X, Y, T, H = 96.0, 86.0, 6.0, 22.0
    # walls between towers: back, sides, and the front either side of the gatehouse
    wall("x", -X + 10, X - 10, Y, 1, T, 0, H)
    wall("y", -Y + 10, Y - 10, -X, -1, T, 0, H)
    wall("y", -Y + 10, Y - 10, X, 1, T, 0, H)
    wall("x", -X + 10, -31, -Y, -1, T, 0, H)
    wall("x", 31, X - 10, -Y, -1, T, 0, H)
    for sx in (-1, 1):
        for sy in (-1, 1):
            tower(sx * X, sy * Y, 20, 0, 48, win_levels=(22, 34))
        tower(sx * X, -30, 14, 0, 36, win_levels=(24,))
        tower(sx * X, 30, 14, 0, 36, win_levels=(24,))
        tower(sx * 52, Y, 14, 0, 36, win_levels=(24,))
        tower(sx * 52, -Y, 14, 0, 38, win_levels=(24,))


def gatehouse():
    Y = -86.0
    for sx in (-1, 1):
        tower(sx * 20, Y - 1, 22, 0, 62, win_levels=(24, 36, 48), win_w=2.0, win_h=5.0)
        banner(sx * 20, Y - 12, 56, 7.0, 26.0, "Banner_Red")
    # the gate block between the towers, with an arched passage 14 wide, 24 high
    gw, gh, yf, yb = 14.0, 24.0, Y - 7, Y + 5
    spring = gh - gw / 2
    G.box("Palace_Stone", -9, -gw / 2, yf, yb, 0, 40)
    G.box("Palace_Stone", gw / 2, 9, yf, yb, 0, 40)
    G.box("Palace_Stone", -9, 9, yf, yb, gh, 40)
    G.prism("Palace_Stone", [(-gw / 2, spring), (-gw / 2, gh)] + [(gw / 2, gh), (gw / 2, spring)] + arch_pts(0, spring, gw)[1:-1],
            yf, yb, "XZ")
    # the passage: dark walls, a dark back, a gilded archivolt, and the portcullis half raised
    G.box("Palace_Dark", -gw / 2 + 0.01, -gw / 2 + 0.6, yf, yb, 0, spring)
    G.box("Palace_Dark", gw / 2 - 0.6, gw / 2 - 0.01, yf, yb, 0, spring)
    G.prism("Palace_Window", [(gw / 2, 0), (gw / 2, spring)] + arch_pts(0, spring, gw)[1:-1] + [(-gw / 2, spring), (-gw / 2, 0)],
            yb - 1.0, yb - 0.5, "XZ")
    n = 15
    for i in range(n):
        a = math.pi * (i + 0.5) / n
        r = gw / 2 + 0.7
        c = (r * math.cos(a), yf - 0.2, spring + r * math.sin(a))
        tang = (-math.sin(a), 0, math.cos(a))
        G.obox("Gold" if i % 2 == 0 else "Palace_Trim", c, (math.cos(a), 0, math.sin(a)), (0, 1, 0), tang, 0.7, 0.45, math.pi * r / n / 2)
    for i in range(9):
        x = -gw / 2 + (i + 0.5) * gw / 9
        G.box("Iron", x - 0.18, x + 0.18, yf + 2.0, yf + 2.4, 11, spring + 5)
    for j in range(5):
        z = 11 + j * 2.6
        G.box("Iron", -gw / 2 + 0.3, gw / 2 - 0.3, yf + 2.0, yf + 2.4, z, z + 0.3)
    for i in range(9):
        x = -gw / 2 + (i + 0.5) * gw / 9
        G.cyl("Iron", x, yf + 2.2, 0.18, 0.02, 11, 10.0, 6)
    crenel_edge("x", -9, 9, yf, -1, 40)
    # the crown device over the gate, big
    crown(0, yf - 1.4, 31.5, 4.2, face=True)
    for s in (-1, 1):
        G.sphere("Gem_Red", (s * 3.0, yf - 2.2, 33.0), 0.7, 12)


def crown(cx, cy, z, r, face=False):
    """A gold crown. face=True: a relief on a -Y face (a flat half crown)."""
    if face:
        G.box("Gold", cx - r, cx + r, cy - 0.4, cy + 0.4, z, z + r * 0.45)
        for i in range(5):
            x = cx - r + i * r / 2
            G.prism("Gold", [(x - r * 0.18, z + r * 0.45), (x + r * 0.18, z + r * 0.45), (x, z + r * 1.05)], cy - 0.4, cy + 0.4, "XZ")
            G.sphere("Gold", (x, cy - 0.3, z + r * 1.1), r * 0.1, 12)
        return
    h = r * 0.45
    G.ring("Gold", cx, cy, r - 0.9, r, z, z + h, 96)
    G.ring("Gold", cx, cy, r - 0.95, r + 0.35, z, z + h * 0.18, 96)
    G.ring("Gold", cx, cy, r - 0.95, r + 0.35, z + h * 0.82, z + h, 96)
    for i in range(16):
        a = 2 * math.pi * i / 16
        G.sphere("Gem_Red" if i % 2 == 0 else "Palace_Trim", (cx + (r + 0.15) * math.cos(a), cy + (r + 0.15) * math.sin(a), z + h * 0.5),
                 r * 0.045 if i % 2 == 0 else r * 0.03, 12)
    for i in range(8):   # fleurons
        a = 2 * math.pi * i / 8
        ux, uy = math.cos(a), math.sin(a)
        base = Vector((cx + (r - 0.45) * ux, cy + (r - 0.45) * uy, z + h))
        tall = r * (0.95 if i % 2 == 0 else 0.62)
        for k in range(6):
            t0, t1 = k / 6, (k + 1) / 6
            wdt = r * 0.2 * (1 - 0.8 * t0)
            c = base + Vector((0, 0, tall * (t0 + t1) / 2))
            G.obox("Gold", c, (-uy, ux, 0), (ux, uy, 0), (0, 0, 1), wdt, 0.45, tall / 12 + 0.05)
        G.sphere("Gold", tuple(base + Vector((0, 0, tall + r * 0.07))), r * 0.09, 16)
    # the arches over the top, the orb and the cross
    for k in range(4):
        a = math.pi * k / 4
        ux, uy = math.cos(a), math.sin(a)
        n = 14
        for i in range(n + 1):
            t = math.pi * i / n
            rr = (r - 0.6) * math.cos(t)
            zz = z + h + (r * 0.75) * math.sin(t)
            G.obox("Gold", (cx + rr * ux, cy + rr * uy, zz), (ux, uy, 0), (-uy, ux, 0), (0, 0, 1), 0.7, 0.45, 0.7)
    G.sphere("Gold", (cx, cy, z + h + r * 0.75 + r * 0.16), r * 0.17, 24)
    G.box("Gold", cx - r * 0.05, cx + r * 0.05, cy - r * 0.05, cy + r * 0.05, z + h + r * 0.98, z + h + r * 1.42)
    G.box("Gold", cx - r * 0.2, cx + r * 0.2, cy - r * 0.05, cy + r * 0.05, z + h + r * 1.2, z + h + r * 1.3)


def terrace_and_stairs():
    """A terrace 20 m up behind the gate court, its front a battered retaining wall with a crenellated edge, and
    a grand staircase up its middle between balustrades, with landings."""
    x0, x1, y0, y1, top = -72.0, 72.0, -30.0, 80.0, 20.0
    G.box("Palace_Stone", x0, x1, y0, y1, 0, top)
    G.box("Palace_Dark", x0 - 1.5, x1 + 1.5, y0 - 2.5, y0, 0, 6)
    G.box("Palace_Trim", x0 - 0.4, x1 + 0.4, y0 - 0.4, y1, top - 0.6, top)
    for sx in (-1, 1):
        crenel_edge("x", sx * 17, sx * 72, y0, -1, top, overhang=0.6)
        crenel_edge("y", y0, y1, sx * 72, sx, top, overhang=0.6)
    # piers on the retaining wall
    for i in range(10):
        x = -66 + i * 14.6
        if abs(x) < 20:
            continue
        G.frustum("Palace_Dark", x, y0 - 1.0, 3.0, 3.6, 1.8, 2.0, 0, top - 1)
    # the staircase: 40 steps of 0.5 m, two landings, balustrades
    run_y0, run_y1 = -62.0, y0
    steps = 40
    for i in range(steps):
        z = (i + 1) * top / steps
        ya = run_y0 + i * (run_y1 - run_y0) / steps
        G.box("Palace_Stone", -15, 15, ya, run_y1, z - top / steps, z)
        G.box("Palace_Trim", -15, 15, ya - 0.06, ya + 0.08, z - 0.08, z + 0.02)
    for sx in (-1, 1):
        rail = [(run_y0 - 2, 0), (run_y0 - 2, 2.2), (run_y1, top + 2.2), (run_y1, 0)]
        G.prism("Palace_Stone", rail, sx * 15, sx * 17.2, "YZ")
        G.prism("Palace_Trim", [(run_y0 - 2.2, 2.2), (run_y0 - 2.2, 2.6), (run_y1, top + 2.6), (run_y1, top + 2.2)], sx * 14.9, sx * 17.4, "YZ")
        for k in range(6):   # newel posts with gold balls
            ya = run_y0 - 1 + k * (run_y1 - run_y0 + 1) / 5
            zz = max(0.0, (ya - run_y0) / (run_y1 - run_y0)) * top
            G.box("Palace_Trim", sx * 14.7, sx * 17.6, ya - 0.7, ya + 0.7, zz, zz + 3.6)
            G.sphere("Gold", (sx * 16.15, ya, zz + 4.2), 0.6, 12)


def great_hall(sx):
    """A long hall on the terrace's side: buttresses, tall windows both sides, crenellated, corner turrets."""
    xa, xb = sx * 40, sx * 68
    lo, hi = min(xa, xb), max(xa, xb)
    y0, y1, z0, z1 = -18.0, 72.0, 20.0, 54.0
    G.box("Palace_Stone", lo, hi, y0, y1, z0, z1)
    outer = hi if sx > 0 else lo
    inner = lo if sx > 0 else hi
    for k in range(10):
        y = y0 + 4.5 + k * (y1 - y0 - 9) / 9
        G.frustum("Palace_Stone", outer + sx * 1.6, y, 3.6, 2.4, 1.2, 1.6, z0, z1 - 4)
        G.box("Palace_Trim", outer + sx * 0.0, outer + sx * 2.0, y - 1.0, y + 1.0, z1 - 4, z1 - 3.5)
        if k < 9:
            ym = y + (y1 - y0 - 9) / 18
            window("x", (ym, z0 + 8), outer, 3.0, 13.0, sx)
            window("x", (ym, z0 + 8), inner, 3.0, 13.0, -sx)
            window("x", (ym, z0 + 25), outer, 1.8, 4.5, sx)
    crenel_edge("y", y0 + 3, y1 - 3, outer, sx, z1)
    crenel_edge("y", y0 + 3, y1 - 3, inner, -sx, z1)
    crenel_edge("x", lo + 3, hi - 3, y0, -1, z1)
    crenel_edge("x", lo + 3, hi - 3, y1, 1, z1)
    for y in (y0, y1):
        for x in (lo, hi):
            G.cyl("Palace_Stone", x, y, 3.0, 3.0, z0, z1 + 8, 64)
            G.cyl("Palace_Trim", x, y, 3.3, 3.3, z1 + 8, z1 + 8.4, 64)
            crenel_ring(x, y, 3.2, z1 + 8.4)
    # the gable end facing the court: a big window
    window("y", ((lo + hi) / 2, z0 + 9), y0, 7.0, 18.0, -1)


def keep():
    """Three tiers and the crown. Centre (0, 40) on the terrace (z 20)."""
    cx, cy, z0 = 0.0, 40.0, 20.0
    # tier 1: the great block, 56 x 56, to z 100, with four corner towers to z 122
    W1, Z1 = 56.0, 100.0
    G.frustum("Palace_Dark", cx, cy, W1 + 6, W1 + 6, W1 + 0.6, W1 + 0.6, z0, z0 + 10)
    G.box("Palace_Stone", cx - W1 / 2, cx + W1 / 2, cy - W1 / 2, cy + W1 / 2, z0 + 10, Z1)
    for z in (44.0, 66.0, 84.0):
        G.box("Palace_Trim", cx - W1 / 2 - 0.5, cx + W1 / 2 + 0.5, cy - W1 / 2 - 0.5, cy + W1 / 2 + 0.5, z, z + 0.7)
    front = cy - W1 / 2
    # the portal at the head of the stairs
    pw, ph = 11.0, 19.0
    window("y", (cx, z0 + 0.5), front, pw, ph, -1, frame=0.9)
    G.prism("Gold", [(cx + pw / 2 + 0.4, z0 + ph - pw / 2), (cx + pw / 2 + 1.4, z0 + ph - pw / 2)] +
            [(cx + (pw / 2 + 1.4) * math.cos(math.pi * i / 16), z0 + ph - pw / 2 + (pw / 2 + 1.4) * math.sin(math.pi * i / 16)) for i in range(1, 16)] +
            [(cx - pw / 2 - 1.4, z0 + ph - pw / 2), (cx - pw / 2 - 0.4, z0 + ph - pw / 2)] +
            [(cx + (pw / 2 + 0.4) * math.cos(math.pi * i / 16), z0 + ph - pw / 2 + (pw / 2 + 0.4) * math.sin(math.pi * i / 16)) for i in range(15, 0, -1)],
            front - 0.95, front - 0.45, "XZ")
    # the great windows of the hall above the portal, and banners between them
    for u in (-19.0, -7.0, 7.0, 19.0):
        if abs(u) > 10:
            window("y", (cx + u, 46), front, 4.4, 17.0, -1)
        window("y", (cx + u, 70), front, 2.4, 9.0, -1)
    for u in (-13.0, 13.0):
        banner(cx + u, front, 96, 5.0, 26.0, "Banner_Red" if u < 0 else "Banner_Navy")
    for face, at, sgn in (("y", cy + W1 / 2, 1), ("x", cx - W1 / 2, -1), ("x", cx + W1 / 2, 1)):
        for u in (-18.0, -6.0, 6.0, 18.0):
            for zw in (46.0, 70.0, 86.0):
                window(face, ((cx if face == "y" else cy) + u, zw), at, 2.6 if zw == 46 else 2.0, 11.0 if zw == 46 else 6.0, sgn)
    for e in (("x", cx - W1 / 2 + 8, cx + W1 / 2 - 8, cy - W1 / 2, -1), ("x", cx - W1 / 2 + 8, cx + W1 / 2 - 8, cy + W1 / 2, 1),
              ("y", cy - W1 / 2 + 8, cy + W1 / 2 - 8, cx - W1 / 2, -1), ("y", cy - W1 / 2 + 8, cy + W1 / 2 - 8, cx + W1 / 2, 1)):
        crenel_edge(e[0], e[1], e[2], e[3], e[4], Z1)
    for sx in (-1, 1):
        for sy in (-1, 1):
            tower(cx + sx * W1 / 2, cy + sy * W1 / 2, 14, z0, 122, win_levels=(48, 72, 96), turrets=True)
    # tier 2: 40 x 40 to z 142, a rose window on the front, corner turrets
    W2, Z2 = 40.0, 142.0
    G.box("Palace_Stone", cx - W2 / 2, cx + W2 / 2, cy - W2 / 2, cy + W2 / 2, Z1, Z2)
    G.box("Palace_Trim", cx - W2 / 2 - 0.5, cx + W2 / 2 + 0.5, cy - W2 / 2 - 0.5, cy + W2 / 2 + 0.5, Z1 + 20, Z1 + 20.7)
    rz, rr = 122.0, 8.0
    f2 = cy - W2 / 2
    G.prism("Palace_Window", [(cx + rr * math.cos(2 * math.pi * i / 48), rz + rr * math.sin(2 * math.pi * i / 48)) for i in range(48)][::-1],
            f2 - 0.12, f2 - 0.02, "XZ")
    G.ring("Gold", 0, 0, 0, 0, 0, 0, 3) if False else None
    for i in range(48):   # the gold rim of the rose
        a0, a1 = 2 * math.pi * i / 48, 2 * math.pi * (i + 1) / 48
        am = (a0 + a1) / 2
        G.obox("Gold", (cx + (rr + 0.6) * math.cos(am), f2 - 0.35, rz + (rr + 0.6) * math.sin(am)),
               (math.cos(am), 0, math.sin(am)), (0, 1, 0), (-math.sin(am), 0, math.cos(am)), 0.6, 0.35, (rr + 0.6) * math.pi / 48 + 0.03)
    for i in range(12):   # tracery: spokes and an inner ring
        a = 2 * math.pi * i / 12
        G.obox("Gold", (cx + rr * 0.5 * math.cos(a), f2 - 0.25, rz + rr * 0.5 * math.sin(a)), (math.cos(a), 0, math.sin(a)), (0, 1, 0),
               (-math.sin(a), 0, math.cos(a)), rr * 0.5, 0.2, 0.18)
    G.cyl("Gold", cx, f2 - 0.3, 1.4, 1.4, rz - 0.01, rz + 0.01, 24) if False else None
    G.sphere("Gold", (cx, f2 - 0.3, rz), 1.3, 20)
    for face, at, sgn in (("y", cy + W2 / 2, 1), ("x", cx - W2 / 2, -1), ("x", cx + W2 / 2, 1)):
        for u in (-10.0, 0.0, 10.0):
            window(face, ((cx if face == "y" else cy) + u, 112), at, 2.4, 10.0, sgn)
    for u in (-12.0, 12.0):
        window("y", (cx + u, 108), f2, 2.4, 12.0, -1)
    for e in (("x", cx - W2 / 2 + 3, cx + W2 / 2 - 3, cy - W2 / 2, -1), ("x", cx - W2 / 2 + 3, cx + W2 / 2 - 3, cy + W2 / 2, 1),
              ("y", cy - W2 / 2 + 3, cy + W2 / 2 - 3, cx - W2 / 2, -1), ("y", cy - W2 / 2 + 3, cy + W2 / 2 - 3, cx + W2 / 2, 1)):
        crenel_edge(e[0], e[1], e[2], e[3], e[4], Z2)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = cx + sx * (W2 / 2 - 0.5), cy + sy * (W2 / 2 - 0.5)
            G.cyl("Palace_Stone", x, y, 3.4, 3.4, Z1, Z2 + 10, 64)
            G.cyl("Palace_Trim", x, y, 3.7, 3.7, Z2 + 10, Z2 + 10.4, 64)
            crenel_ring(x, y, 3.6, Z2 + 10.4)
    # tier 3: a round belvedere with arched openings, then the crown
    R3, Z3 = 13.0, 166.0
    G.cyl("Palace_Stone", cx, cy, R3, R3, Z2, Z3, 64)
    G.cyl("Palace_Trim", cx, cy, R3 + 0.6, R3 + 0.6, Z2, Z2 + 1.2, 64)
    G.cyl("Palace_Trim", cx, cy, R3 + 0.6, R3 + 0.6, Z3 - 1.0, Z3, 64)
    for i in range(12):
        a = 2 * math.pi * (i + 0.5) / 12
        ux, uy = math.cos(a), math.sin(a)
        c = Vector((cx + (R3 + 0.05) * ux, cy + (R3 + 0.05) * uy, Z2 + 11))
        G.obox("Palace_Window", c, (ux, uy, 0), (-uy, ux, 0), (0, 0, 1), 0.08, 1.4, 6.0)
        for s in (-1, 1):
            G.obox("Palace_Trim", c + Vector((-uy, ux, 0)) * s * 1.75 + Vector((ux, uy, 0)) * 0.25, (ux, uy, 0), (-uy, ux, 0), (0, 0, 1), 0.3, 0.35, 6.2)
        G.obox("Palace_Trim", c + Vector((0, 0, 6.6)) + Vector((ux, uy, 0)) * 0.25, (ux, uy, 0), (-uy, ux, 0), (0, 0, 1), 0.3, 2.1, 0.5)
    crown(cx, cy, Z3, 14.5)


def needle_towers_and_flying_bridges():
    for sx in (-1, 1):
        x, y = sx * 60, -22.0
        tower(x, y, 10, 20, 112, win_levels=(40, 58, 76, 94), win_w=1.4, win_h=3.8, string_every=14.0)
        # a flying bridge from the needle tower to the keep's front corner tower, high up
        a = Vector((x - sx * 5.2, y + 2, 90))
        b = Vector((sx * 28 + sx * 7.2, 40 - 28 - 0.0, 90))
        d = b - a
        L = d.length
        u = d.normalized()
        side = Vector((-u.y, u.x, 0))
        mid = (a + b) / 2
        G.obox("Palace_Stone", mid, u, side, (0, 0, 1), L / 2, 2.2, 0.8)
        for s in (-1, 1):
            G.obox("Palace_Stone", mid + side * s * 2.0 + Vector((0, 0, 1.4)), u, side, (0, 0, 1), L / 2, 0.25, 0.6)
            G.obox("Palace_Trim", mid + side * s * 2.0 + Vector((0, 0, 2.1)), u, side, (0, 0, 1), L / 2, 0.32, 0.12)
        n = 16
        for i in range(n):   # the arch under it
            t = (i + 0.5) / n
            p = a + d * t + Vector((0, 0, -0.8 - 7.0 * math.sin(math.pi * t) * -1 - 7.0))
            p = a + d * t + Vector((0, 0, -0.8 - 7.0 * (1 - math.sin(math.pi * t))))
            G.obox("Palace_Stone", p, u, side, (0, 0, 1), L / n / 2 + 0.05, 1.6, 0.9)


def viaduct():
    """From the gate's front toward the course: deck 9 m wide, arches on piers that fall away into spurs,
    parapets, lamp posts. It descends to VIA_END_Z at VIA_END_Y."""
    y0, y1 = -93.0, VIA_END_Y
    z0, z1 = 0.0, VIA_END_Z
    W = 4.5

    def zz(y):
        return z0 + (z1 - z0) * (y - y0) / (y1 - y0)
    L = abs(y1 - y0)
    n = 4
    seg = L / n
    for i in range(n):
        ya, yb = y0 - i * seg, y0 - (i + 1) * seg
        za, zb = zz(ya), zz(yb)
        G.prism("Palace_Stone", [(ya, za), (yb, zb), (yb, zb - 2.2), (ya, za - 2.2)], -W, W, "YZ")
        G.prism("Palace_Trim", [(ya, za - 2.2), (yb, zb - 2.2), (yb, zb - 2.6), (ya, za - 2.6)], -W - 0.3, W + 0.3, "YZ")
        # spandrel with the arch cut out
        sp = (za + zb) / 2 - 2.6
        r = seg / 2 - 2.0
        yc = (ya + yb) / 2
        pts = [(ya - 0.0, sp + 0.2), (ya, sp - r - 2), ] + [(yc + r * math.cos(math.pi * k / 16), sp - r - 2 + r * math.sin(math.pi * k / 16)) for k in range(0, 17)][::-1] if False else None
        pts = [(ya, za - 2.6), (yb, zb - 2.6), (yb, sp - r - 1)]
        pts += [(yc - r * math.cos(math.pi * k / 18), sp - r - 1 + r * math.sin(math.pi * k / 18)) for k in range(18, -1, -1)][::-1][::-1]
        pts += [(ya, sp - r - 1)]
        G.prism("Palace_Stone", pts, -W + 0.4, W - 0.4, "YZ")
        # parapets with merlons
        for s in (-1, 1):
            G.prism("Palace_Stone", [(ya, za), (yb, zb), (yb, zb + 1.2), (ya, za + 1.2)], s * W, s * (W - 0.7), "YZ")
            for k in range(6):
                ym = ya - (k + 0.5) * seg / 6
                zm = zz(ym)
                G.box("Palace_Stone", s * W, s * (W - 0.7), ym - 0.9, ym + 0.9, zm + 1.2, zm + 2.4)
    # piers between the arches, plunging into spurs
    for i in range(n + 1):
        y = y0 - i * seg
        z = zz(y) - 2.6
        if i == 0:
            continue
        G.frustum("Palace_Stone", 0, y, 2 * W + 0.6, 4.2, 2 * W + 0.6, 4.2, z - 30, z)
        G.frustum("Palace_Dark", 0, y, 2 * W + 0.6, 4.2, 2.0, 1.2, z - 52, z - 30)
        G.box("Palace_Trim", -W - 0.6, W + 0.6, y - 2.4, y + 2.4, z - 0.5, z)
    # lamp posts with lanterns
    for i in range(n * 2 + 1):
        y = y0 - i * L / (n * 2)
        z = zz(y)
        for s in (-1, 1):
            x = s * (W - 0.35)
            G.box("Iron", x - 0.12, x + 0.12, y - 0.12, y + 0.12, z + 2.4, z + 6.8)
            G.box("Gold", x - 0.45, x + 0.45, y - 0.45, y + 0.45, z + 6.8, z + 7.0)
            G.box("Palace_Window", x - 0.38, x + 0.38, y - 0.38, y + 0.38, z + 7.0, z + 8.0)
            G.box("Gold", x - 0.5, x + 0.5, y - 0.5, y + 0.5, z + 8.0, z + 8.2)
            G.sphere("Gold", (x, y, z + 8.5), 0.25, 10)


# ---------------------------------------------------------------- output

def box_uv(bm, metres):
    uv = bm.loops.layers.uv.active
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            co = loop.vert.co
            if ax == 0:
                u, v = co.y, co.z
            elif ax == 1:
                u, v = co.x, co.z
            else:
                u, v = co.x, co.y
            loop[uv].uv = (u / metres, v / metres)


def material(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    r, g, b, metal, rough = PAL[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bs = m.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = (r, g, b, 1)
    bs.inputs["Metallic"].default_value = metal
    bs.inputs["Roughness"].default_value = rough
    if name == "Palace_Window":
        bs.inputs["Emission Color"].default_value = (1.0, 0.7, 0.4, 1)
        bs.inputs["Emission Strength"].default_value = 0.6
    if name == "Palace_Water":
        bs.inputs["Alpha"].default_value = 0.7
    return m


def build_object():
    objs = []
    for name, bm in G.bms.items():
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.normal_update()
        if name in BOX_UV:
            box_uv(bm, UV_M)
        elif name == "Palace_Rock":
            box_uv(bm, ROCK_UV_M)
        elif name not in G.uv_done:
            box_uv(bm, 2.0)
        bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="EAR_CLIP")
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        o = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(o)
        o.data.materials.append(material(name))
        objs.append(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    pal = bpy.context.active_object
    pal.name = "PR_Palace"
    pal.data.name = "PR_Palace"
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(30))
    return pal


def preview(pal):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    w = bpy.data.worlds.new("sky")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.45, 0.66, 0.95, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
    sc.world = w
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(48), 0, math.radians(-125))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.clip_end = 3000
    sc.collection.objects.link(cam)
    sc.camera = cam
    for name, loc, target, lens in (("palace_from_course", (0, -250, 5), (0, 30, 70), 28),
                                    ("palace_close", (-85, -165, 40), (0, 0, 60), 30),
                                    ("palace_gate", (12, -150, 10), (0, -86, 25), 32)):
        cam.location = loc
        cam.data.lens = lens
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(OUT, name + ".png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rock()
    waterfall(-110, 12, (-1, 0.15), 12)
    waterfall(108, -24, (1, -0.1), 10)
    waterfall(-58, -100, (-0.35, -1), 9)
    paving()
    outer_ward()
    gatehouse()
    terrace_and_stairs()
    great_hall(-1)
    great_hall(1)
    keep()
    needle_towers_and_flying_bridges()
    viaduct()
    pal = build_object()
    tris = sum(len(p.vertices) - 2 for p in pal.data.polygons)
    print(f"PALACE tris={tris} materials={[m.name for m in pal.data.materials]}")
    bpy.ops.object.select_all(action="DESELECT")
    pal.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "PR_Palace.fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "PR_Palace.blend"))
    if PREVIEW:
        preview(pal)
    print("DONE")
