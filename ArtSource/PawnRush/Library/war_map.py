"""Pawn Rush library (작전 회의실): the start's war map rebuilt clean and textured — preview only (R360).

    blender --background --factory-startup --python war_map.py -- --preview D:/check   (renders, writes nothing)

승규 님: "작전 지도도 울퉁불퉁한 부분 매끄럽게 해주고 사진처럼 텍스쳐 입혀" with the concept image the model was made
from: a parchment campaign map (compass, pines, mountains, a river, dashed roads, a network of iron-ringed brass nodes,
a slate castle) in a wooden frame with iron bands, gold corner caps with iron diamond studs and gold bolts.

The FBX's V21_Original_treasure_map_clay is one welded 65k-triangle sculpt, lumpy all over and with no UVs, so its
parts can't take separate materials. This script hides it and builds the same board at the same size from clean
bevelled boxes (posts, rails, caps, backing, bands, clamps, brackets, studs, bolts), lays a flat parchment on both
faces with a painted texture (drawn here with numpy: paper grain, burnt torn edge, river, roads, trees, mountains,
compass, castle, network lines and node shadows) and stands 3D nodes (iron ring + brass dome) on it.
Each face reads the right way round from its own side. The pedestal and feet are the FBX's, untouched.
Writes only the texture and the renders into the preview folder.
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

FBX = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets",
                                    "Resources", "PawnRushLibrary", "PawnRushLibraryEnvironment.fbx"))
OLD = "V21_Original_treasure_map_clay"
CX, CY = -5.0, -19.7            # board centre (x, y); y is its depth axis
X0, X1, Z0, Z1 = -6.85, -3.15, .65, 3.95
PX0, PX1, PZ0, PZ1 = -6.30, -3.70, 1.15, 3.36   # parchment
PARCH_D = .185                  # parchment faces at y = CY +- this
PPM = 800                       # texture pixels a metre

# The concept image's layout, in parchment fractions (0, 0 top left): nodes (u, v, big) and the lines between them.
NODES = ((.315, .208, 1), (.867, .384, 1), (.392, .581, 1), (.685, .197, 0), (.546, .305, 0), (.179, .370, 0),
         (.631, .424, 0), (.090, .516, 0), (.597, .578, 0), (.256, .743, 0), (.659, .700, 0), (.569, .773, 0),
         (.203, .905, 0))
LINKS = ((0, 4), (0, 5), (0, 2), (4, 3), (3, 1), (4, 6), (6, 1), (3, 6), (5, 7), (7, 2), (5, 2), (2, 6), (2, 8),
         (8, 1), (8, 10), (2, 9), (9, 12), (10, 11), (2, 11), (10, 1), (11, 9))
RIVER = ((.97, .24), (.86, .30), (.80, .43), (.72, .50), (.63, .63), (.50, .69), (.42, .77), (.37, .88), (.34, 1.02))
ROADS = (((.02, .33), (.12, .30), (.24, .36), (.30, .45)),
         ((.50, .06), (.58, .14), (.76, .10), (.95, .06)),
         ((.03, .62), (.15, .67), (.30, .64), (.36, .70)),
         ((.50, .86), (.60, .93), (.76, .90), (.97, .82)),
         ((.80, .49), (.90, .54), (.99, .52)))
TREES = ((.43, .14, 1), (.47, .17, .9), (.51, .12, 1.1), (.55, .15, .9), (.58, .11, 1), (.56, .20, .85),
         (.42, .38, 1), (.46, .40, .9), (.50, .37, 1.05),
         (.07, .38, 1), (.10, .44, .9), (.21, .58, 1), (.25, .61, .9), (.17, .62, .85),
         (.15, .82, 1), (.62, .82, 1), (.66, .80, .9), (.88, .58, .85), (.04, .70, .9))
MOUNTAINS = ((.82, .07, 1.2), (.90, .10, .9), (.12, .74, 1), (.18, .76, .75), (.46, .87, 1), (.52, .89, .7))
COMPASS = (.14, .19, .085)
CASTLE = (.83, .66)

# Colours (sRGB) from the concept image.
PAPER, PAPER_DK, BURNT = np.array((.91, .78, .59)), np.array((.80, .63, .43)), np.array((.45, .27, .13))
BACKING = np.array((.17, .10, .06))
INK, ROAD_C, RIVER_C, RIVER_EDGE = (.33, .22, .15), (.62, .46, .32), (.64, .69, .71), (.52, .57, .60)
TREE_C, TREE_DK, MOUNT_C, MOUNT_DK = (.42, .42, .24), (.30, .31, .17), (.66, .50, .35), (.47, .33, .21)
SEPIA, SLATE = (.60, .44, .30), (.19, .21, .26)


# ------------------------------------------------------------------ the parchment texture (numpy)

class Canvas:
    def __init__(self, w_m, h_m):
        self.w, self.h = int(w_m * PPM), int(h_m * PPM)
        self.wm, self.hm = w_m, h_m
        self.c = np.zeros((self.h, self.w, 3))
        self.aa = 1.2 / PPM

    def window(self, lo, hi):
        """Pixel slices and metre grids over the box lo..hi (metres)."""
        x0, y0 = max(int(lo[0] * PPM) - 2, 0), max(int(lo[1] * PPM) - 2, 0)
        x1, y1 = min(int(hi[0] * PPM) + 3, self.w), min(int(hi[1] * PPM) + 3, self.h)
        if x1 <= x0 or y1 <= y0:
            return None
        ys, xs = np.mgrid[y0:y1, x0:x1]
        return (slice(y0, y1), slice(x0, x1)), (xs + .5) / PPM, (ys + .5) / PPM

    def paint(self, sl, mask, colour, alpha=1.0):
        m = (np.clip(mask, 0, 1) * alpha)[..., None]
        self.c[sl] = self.c[sl] * (1 - m) + np.asarray(colour) * m

    def m(self, p):
        return p[0] * self.wm, p[1] * self.hm

    def polyline(self, pts, width, colour, dash=None, alpha=1.0):
        pts = [self.m(p) for p in pts]
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        win = self.window((min(xs) - width, min(ys) - width), (max(xs) + width, max(ys) + width))
        if win is None:
            return
        sl, X, Y = win
        best = np.full(X.shape, 1e9)
        along = np.zeros(X.shape)
        run = 0.0
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            dx, dy = bx - ax, by - ay
            ln = math.hypot(dx, dy) or 1e-9
            t = np.clip(((X - ax) * dx + (Y - ay) * dy) / (ln * ln), 0, 1)
            d = np.hypot(X - (ax + t * dx), Y - (ay + t * dy))
            closer = d < best
            best = np.where(closer, d, best)
            along = np.where(closer, run + t * ln, along)
            run += ln
        mask = np.clip((width / 2 - best) / self.aa + .5, 0, 1)
        if dash:
            mask *= (np.mod(along, dash[0] + dash[1]) < dash[0])
        self.paint(sl, mask, colour, alpha)

    def convex(self, pts, colour, alpha=1.0, grow=0.0):
        """Fills a convex polygon given counter-clockwise in screen space (y down)."""
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        win = self.window((min(xs) - grow, min(ys) - grow), (max(xs) + grow, max(ys) + grow))
        if win is None:
            return
        sl, X, Y = win
        sd = np.full(X.shape, -1e9)
        n = len(pts)
        area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
        s = 1 if area > 0 else -1
        for i in range(n):
            (ax, ay), (bx, by) = pts[i], pts[(i + 1) % n]
            ex, ey = bx - ax, by - ay
            ln = math.hypot(ex, ey) or 1e-9
            nx, ny = s * ey / ln, -s * ex / ln          # outward normal
            sd = np.maximum(sd, (X - ax) * nx + (Y - ay) * ny)
        self.paint(sl, np.clip((grow - sd) / self.aa + .5, 0, 1), colour, alpha)

    def disc(self, c, r, colour, alpha=1.0, soft=0.0):
        win = self.window((c[0] - r - soft, c[1] - r - soft), (c[0] + r + soft, c[1] + r + soft))
        if win is None:
            return
        sl, X, Y = win
        d = np.hypot(X - c[0], Y - c[1])
        mask = np.clip((r - d) / max(self.aa, soft) + .5, 0, 1)
        self.paint(sl, mask, colour, alpha)

    def ring(self, c, r, width, colour, alpha=1.0):
        win = self.window((c[0] - r - width, c[1] - r - width), (c[0] + r + width, c[1] + r + width))
        if win is None:
            return
        sl, X, Y = win
        d = np.abs(np.hypot(X - c[0], Y - c[1]) - r)
        self.paint(sl, np.clip((width / 2 - d) / self.aa + .5, 0, 1), colour, alpha)


def value_noise(h, w, cells, rng):
    gy, gx = max(int(cells * h / w), 1), cells
    g = rng.random((gy + 2, gx + 2))
    ys = np.linspace(0, gy, h, endpoint=False)
    xs = np.linspace(0, gx, w, endpoint=False)
    y0, x0 = ys.astype(int), xs.astype(int)
    fy, fx = ys - y0, xs - x0
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    a = g[y0][:, x0] * (1 - fx) + g[y0][:, x0 + 1] * fx
    b = g[y0 + 1][:, x0] * (1 - fx) + g[y0 + 1][:, x0 + 1] * fx
    return a * (1 - fy[:, None]) + b * fy[:, None]


def fbm(h, w, cells, octaves, rng):
    out, amp, tot = np.zeros((h, w)), 1.0, 0.0
    for _ in range(octaves):
        out += value_noise(h, w, cells, rng) * amp
        tot += amp
        amp *= .5
        cells *= 2
    return out / tot


def catmull(pts, n=12):
    pts = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = (np.array(p) for p in pts[i - 1:i + 3])
        for k in range(n):
            t = k / n
            out.append(tuple(.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                   + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)))
    out.append(tuple(pts[-2]))
    return out


def paint_map(path):
    rng = np.random.default_rng(7)
    cv = Canvas(PX1 - PX0, PZ1 - PZ0)
    h, w = cv.h, cv.w
    ys, xs = np.mgrid[0:h, 0:w]
    X, Y = (xs + .5) / PPM, (ys + .5) / PPM
    # paper: two-tone grain, blotches, faint fibres
    grain = fbm(h, w, 6, 6, rng)
    fibre = fbm(h, w, 90, 2, rng)
    stain = fbm(h, w, 3, 4, rng)
    t = np.clip(grain * 1.1 - .25 + (fibre - .5) * .25, 0, 1)
    cv.c[:] = PAPER * (1 - t[..., None]) + PAPER_DK * t[..., None]
    blot = np.clip((stain - .58) * 4, 0, 1) * .35
    cv.c[:] = cv.c * (1 - blot[..., None]) + PAPER_DK * .85 * blot[..., None]
    # torn, burnt edge: distance in from the rectangle, threshold wobbling along the edge
    edge = np.minimum(np.minimum(X, cv.wm - X), np.minimum(Y, cv.hm - Y))
    tear = .028 + .03 * fbm(h, w, 40, 3, rng)
    burn = np.clip(1 - (edge - tear) / .14, 0, 1) ** 1.6
    cv.c[:] = cv.c * (1 - burn[..., None] * .75) + BURNT * (burn[..., None] * .75)
    torn = np.clip((tear - edge) / cv.aa + .5, 0, 1)[..., None]   # the dark backing past the torn edge
    cv.c[:] = cv.c * (1 - torn) + BACKING * torn

    # river: a soft darker bank under a lighter stream
    river = catmull([cv.m(p) for p in RIVER], 16)
    river = [(x / cv.wm, y / cv.hm) for x, y in river]
    cv.polyline(river, .060, RIVER_EDGE, alpha=.9)
    cv.polyline(river, .044, RIVER_C)
    # dashed roads
    for road in ROADS:
        sm = [(x / cv.wm, y / cv.hm) for x, y in catmull([cv.m(p) for p in road], 14)]
        cv.polyline(sm, .010, ROAD_C, dash=(.045, .03), alpha=.9)
    # compass (sepia, faint): two rings, eight points
    c = cv.m(COMPASS[:2])
    r = COMPASS[2] * cv.wm
    cv.ring(c, r * .62, .006, SEPIA, .8)
    cv.ring(c, r * .70, .004, SEPIA, .6)
    for i in range(8):
        a = i * math.pi / 4
        ln = r * (1.25 if i % 2 == 0 else .75)
        wd = r * (.16 if i % 2 == 0 else .12)
        tip = (c[0] + ln * math.sin(a), c[1] - ln * math.cos(a))
        side = (c[0] + wd * math.cos(a), c[1] + wd * math.sin(a))
        side2 = (c[0] - wd * math.cos(a), c[1] - wd * math.sin(a))
        cv.convex([c, tip, side], SEPIA, .9)
        cv.convex([c, side2, tip], SEPIA, .6)
    # mountains: two-tone peaks with a dark outline
    for u, v, s in MOUNTAINS:
        bx, by = cv.m((u, v))
        hw, ht = .085 * s, .11 * s
        left = [(bx - hw, by + ht * .5), (bx, by - ht * .5), (bx, by + ht * .5)]
        right = [(bx, by - ht * .5), (bx + hw, by + ht * .5), (bx, by + ht * .5)]
        cv.convex([(bx - hw, by + ht * .5), (bx, by - ht * .5), (bx + hw, by + ht * .5)], MOUNT_DK, grow=.006)
        cv.convex(left, MOUNT_C)
        cv.convex(right, (MOUNT_C[0] * .85, MOUNT_C[1] * .85, MOUNT_C[2] * .85))
    # pines: three stacked triangles and a trunk
    for u, v, s in TREES:
        bx, by = cv.m((u, v))
        sz = .085 * s
        cv.convex([(bx - sz * .08, by + sz * .2), (bx + sz * .08, by + sz * .2), (bx + sz * .08, by + sz * .55),
                   (bx - sz * .08, by + sz * .55)], TREE_DK)
        for k, (yy, ww) in enumerate(((.25, .42), (-.1, .34), (-.42, .25))):
            cv.convex([(bx - sz * ww, by + sz * yy), (bx, by + sz * (yy - .55)), (bx + sz * ww, by + sz * yy)],
                      TREE_C if k != 1 else TREE_DK)
    # castle: a slate keep with crenels, a flag and an arched door
    kx, ky = cv.m(CASTLE)
    W2, Hh = .17, .40

    def rect(x0, y0, x1, y1, col):
        cv.convex([(x0, y0), (x0, y1), (x1, y1), (x1, y0)], col)
    rect(kx - W2 * 1.25, ky + Hh * .30, kx + W2 * 1.25, ky + Hh * .50, SLATE)      # base
    rect(kx - W2, ky - Hh * .22, kx + W2, ky + Hh * .32, SLATE)                     # body
    rect(kx - W2 * 1.15, ky - Hh * .30, kx + W2 * 1.15, ky - Hh * .18, SLATE)       # corbel
    for i in range(4):
        x = kx - W2 * 1.15 + i * (W2 * 2.3 - W2 * .4) / 3
        rect(x, ky - Hh * .42, x + W2 * .4, ky - Hh * .28, SLATE)                  # merlons
    rect(kx - .006, ky - Hh * .75, kx + .006, ky - Hh * .40, SLATE)                 # pole
    cv.convex([(kx + .006, ky - Hh * .75), (kx + .006, ky - Hh * .58), (kx + .11, ky - Hh * .66)], SLATE)
    rect(kx - W2 * .38, ky + Hh * .08, kx + W2 * .38, ky + Hh * .50, PAPER * .9)    # door
    cv.disc((kx, ky + Hh * .08), W2 * .38, PAPER * .9)
    for sx in (-1, 1):
        rect(kx + sx * W2 * .55 - .012, ky - Hh * .08, kx + sx * W2 * .55 + .012, ky + Hh * .02, PAPER * .85)
    # network lines, then the shadows the 3D nodes will throw
    for a, b in LINKS:
        cv.polyline([NODES[a][:2], NODES[b][:2]], .011, INK, alpha=.9)
    for u, v, big in NODES:
        r = .085 if big else .05
        p = cv.m((u, v))
        cv.disc((p[0] + .012, p[1] + .016), r * 1.05, (.25, .16, .09), alpha=.55, soft=.02)

    img = bpy.data.images.new("war_map_parchment", w, h, alpha=False)
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(cv.c[::-1], 0, 1)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


# ------------------------------------------------------------------ materials

def principled(name, colour, metallic=0.0, rough=.5):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*colour, 1)
    b.inputs["Metallic"].default_value = metallic
    b.inputs["Roughness"].default_value = rough
    return m, m.node_tree, b


def metal(name, light, dark, rough, scale=14.0):
    m, nt, b = principled(name, light, .9, rough)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = 5
    nt.links.new(co, n.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = .3, (*dark, 1)
    ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = .72, (*light, 1)
    nt.links.new(n.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    return m


def wood(name, along):
    """Brown wood with grain running along the axis `along` ('X' or 'Z')."""
    m, nt, b = principled(name, (.2, .1, .04), 0, .62)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    sep = nt.nodes.new("ShaderNodeMapping")   # stretch the noise along the grain
    sep.inputs["Scale"].default_value = (1, 1, 14) if along == "X" else (14, 1, 1)
    sep.inputs["Scale"].default_value = (.08, 1, 1) if along == "X" else (1, 1, .08)
    nt.links.new(co, sep.inputs["Vector"])
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.wave_type = "BANDS"
    wave.bands_direction = "Z" if along == "X" else "X"
    wave.inputs["Scale"].default_value = 14
    wave.inputs["Distortion"].default_value = 3
    wave.inputs["Detail"].default_value = 4
    wave.inputs["Detail Scale"].default_value = 1.5
    nt.links.new(co, wave.inputs["Vector"])
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.inputs["Scale"].default_value = 30
    nt.links.new(sep.outputs["Vector"], n.inputs["Vector"])
    mix = nt.nodes.new("ShaderNodeMath")
    mix.operation = "MULTIPLY_ADD"
    mix.inputs[1].default_value = .55
    nt.links.new(wave.outputs["Fac"], mix.inputs[0])
    nt.links.new(n.outputs["Fac"], mix.inputs[2])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = .25, (.10, .045, .018, 1)
    ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = .85, (.28, .14, .06, 1)
    nt.links.new(mix.outputs["Value"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .25
    nt.links.new(mix.outputs["Value"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])
    return m


def parchment(img):
    m, nt, b = principled("War map parchment", (1, 1, 1), 0, .78)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Cubic"
    nt.links.new(tex.outputs["Color"], b.inputs["Base Color"])
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.inputs["Scale"].default_value = 60
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .06
    nt.links.new(n.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])
    return m


# ------------------------------------------------------------------ the board

class Builder:
    def __init__(self, mats):
        self.mats = mats
        self.parts = {k: bmesh.new() for k in mats}

    def box(self, mat, lo, hi):
        bm = self.parts[mat]
        r = bmesh.ops.create_cube(bm, size=1.0)
        for v in r["verts"]:
            v.co = Vector(((lo[i] + hi[i]) / 2 + v.co[i] * (hi[i] - lo[i]) for i in range(3)))

    def both(self, mat, x0, x1, z0, z1, d0, d1):
        """A pair of boxes, one on each face, from depth d0 to d1."""
        self.box(mat, (x0, CY + d0, z0), (x1, CY + d1, z1))
        self.box(mat, (x0, CY - d1, z0), (x1, CY - d0, z1))

    def dome(self, mat, at, r, normal):
        bm = self.parts[mat]
        res = bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=10, radius=r)
        n = Vector(normal)
        rot = Vector((0, 0, 1)).rotation_difference(n).to_matrix()
        for v in res["verts"]:
            co = v.co.copy()
            co.z = max(co.z, 0) * .7
            v.co = Vector(at) + rot @ co

    def diamond(self, mat, at, s, h, normal):
        bm = self.parts[mat]
        n = Vector(normal)
        rot = Vector((0, 0, 1)).rotation_difference(n).to_matrix()
        base = [bm.verts.new(Vector(at) + rot @ Vector(p)) for p in ((s, 0, 0), (0, s, 0), (-s, 0, 0), (0, -s, 0))]
        tip = bm.verts.new(Vector(at) + rot @ Vector((0, 0, h)))
        bm.faces.new(base[::-1])
        for i in range(4):
            bm.faces.new((base[i], base[(i + 1) % 4], tip))

    def objects(self, bevel):
        out = []
        for k, bm in self.parts.items():
            if not bm.verts:
                continue
            me = bpy.data.meshes.new("War map " + k)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            bm.to_mesh(me)
            me.materials.append(self.mats[k])
            ob = bpy.data.objects.new("War_map_" + k, me)
            bpy.context.scene.collection.objects.link(ob)
            if bevel.get(k):
                mod = ob.modifiers.new("Bevel", "BEVEL")
                mod.width, mod.segments, mod.limit_method = bevel[k], 3, "ANGLE"
                mod.harden_normals = True
            out.append(ob)
        return out


def build_board(img):
    mats = {
        "wood_v": wood("War map wood (posts)", "Z"),
        "wood_h": wood("War map wood (rails)", "X"),
        "backing": principled("War map backing", (.06, .035, .02), 0, .7)[0],
        "iron": metal("War map iron", (.11, .115, .12), (.05, .05, .055), .45, 9),
        "gold": metal("War map gold", (1.0, .64, .20), (.86, .50, .12), .27, 5),
        "parch": parchment(img),
    }
    B = Builder(mats)
    # posts and corner caps
    for xa, xb in ((X0 + .06, X0 + .44), (X1 - .44, X1 - .06)):
        B.box("wood_v", (xa, CY - .27, 1.12), (xb, CY + .27, 3.48))
        B.both("iron", (xa + xb) / 2 - .055, (xa + xb) / 2 + .055, 1.15, 3.45, .26, .29)
        for z in (1.45, 1.95, 2.45, 2.95):
            for s in (1, -1):
                B.dome("gold", ((xa + xb) / 2, CY + s * .29, z), .032, (0, s, 0))
    for xa, xb in ((X0, X0 + .50), (X1 - .50, X1)):
        for za, zb in ((Z0, Z0 + .50), (Z1 - .50, Z1)):
            B.box("gold", (xa, CY - .35, za), (xb, CY + .35, zb))
            mx, mz = (xa + xb) / 2, (za + zb) / 2
            for s in (1, -1):
                B.diamond("iron", (mx, CY + s * .35, mz), .14, .08, (0, s, 0))
            out = -1 if xa == X0 else 1
            B.diamond("iron", (xb if out > 0 else xa, CY, mz), .14, .08, (out, 0, 0))
            B.diamond("iron", (mx, CY, zb if zb == Z1 else za), .14, .08, (0, 0, 1 if zb == Z1 else -1))
    # rails, their iron clamps and end brackets
    for za, zb in ((Z1 - .48, Z1 - .22), (Z0 + .15, Z0 + .43)):
        B.box("wood_h", (X0 + .44, CY - .22, za), (X1 - .44, CY + .22, zb))
        B.box("iron", (CX - .16, CY - .26, za - .03), (CX + .16, CY + .26, zb + .03))
        for xa in (X0 + .44, X1 - .64):
            B.box("iron", (xa, CY - .25, za - .02), (xa + .2, CY + .25, zb + .02))
        for s in (1, -1):
            B.dome("gold", (CX, CY + s * .26, (za + zb) / 2), .045, (0, s, 0))
            for xa in (X0 + .54, X1 - .54):
                B.dome("gold", (xa, CY + s * .25, (za + zb) / 2), .03, (0, s, 0))
    # backing board, parchment, gold corner brackets with bolts
    B.box("backing", (X0 + .44, CY - .13, Z0 + .43), (X1 - .44, CY + .13, Z1 - .48))
    B.box("backing", (PX0 - .02, CY - .17, PZ0 - .02), (PX1 + .02, CY + .17, PZ1 + .02))
    for cx, sx in ((PX0, 1), (PX1, -1)):
        for cz, sz in ((PZ0, 1), (PZ1, -1)):
            for s in (1, -1):
                d0, d1 = (PARCH_D, PARCH_D + .02) if s > 0 else (-PARCH_D - .02, -PARCH_D)
                B.box("gold", (min(cx, cx + sx * .26), CY + d0, min(cz, cz + sz * .07)),
                      (max(cx, cx + sx * .26), CY + d1, max(cz, cz + sz * .07)))
                B.box("gold", (min(cx, cx + sx * .07), CY + d0, min(cz, cz + sz * .26)),
                      (max(cx, cx + sx * .07), CY + d1, max(cz, cz + sz * .26)))
                B.dome("gold", (cx + sx * .1, CY + s * (PARCH_D + .02), cz + sz * .1), .035, (0, s, 0))
    # 3D nodes: iron ring disc and a brass dome, on both faces
    for side in (1, -1):
        for u, v, big in NODES:
            r = .085 if big else .05
            x = (PX1 - u * (PX1 - PX0)) if side > 0 else (PX0 + u * (PX1 - PX0))
            z = PZ1 - v * (PZ1 - PZ0)
            bm = B.parts["iron"]
            res = bmesh.ops.create_cone(bm, cap_ends=True, segments=28, radius1=r, radius2=r * .92, depth=.022)
            for vv in res["verts"]:
                vv.co = Vector((x + vv.co.x, CY + side * (PARCH_D + .011 + vv.co.z * side), z + vv.co.y))
            B.dome("gold", (x, CY + side * (PARCH_D + .02), z), r * .55, (0, side, 0))
    parts = B.objects({"wood_v": .02, "wood_h": .02, "iron": .006, "gold": .012, "backing": .01})

    # the parchment faces, each read the right way from its own side
    me = bpy.data.meshes.new("War map parchment")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for side in (1, -1):
        y = CY + side * PARCH_D
        corners = [(PX0, PZ0), (PX1, PZ0), (PX1, PZ1), (PX0, PZ1)]
        vs = [bm.verts.new((x, y, z)) for x, z in corners]
        f = bm.faces.new(vs if side < 0 else vs[::-1])
        for loop in f.loops:
            x, _, z = loop.vert.co
            u = (PX1 - x) / (PX1 - PX0) if side > 0 else (x - PX0) / (PX1 - PX0)
            loop[uv].uv = (u, (z - PZ0) / (PZ1 - PZ0))
    bm.to_mesh(me)
    me.materials.append(mats["parch"])
    ob = bpy.data.objects.new("War_map_parchment", me)
    bpy.context.scene.collection.objects.link(ob)
    return parts + [ob]


# ------------------------------------------------------------------ preview

def look_at(cam, target):
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()


def preview(out):
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=FBX)
    sc = bpy.context.scene
    bpy.data.objects[OLD].hide_render = True
    stone = bpy.data.materials.get("CLAY | uniform neutral gray")
    b = stone.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (.70, .58, .47, 1)
    b.inputs["Roughness"].default_value = .62
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
        elif o.type == "MESH":   # the hall's roof and upper walls only shadow the map in a still render
            zs = [(o.matrix_world @ Vector(c)).z for c in o.bound_box]
            if min(zs) > 4.2:
                o.hide_render = True
    img = paint_map(os.path.join(out, "war_map_parchment.png"))
    build_board(img)

    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.data.energy, sun.data.color, sun.data.angle = 3.5, (1.0, .86, .70), math.radians(2)
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.45, .48, .55, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .8
    sc.world = world
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.use_raytracing = True
    sc.eevee.taa_render_samples = 64
    sc.view_settings.view_transform = "AgX"
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    shots = (   # name, camera, target, lens, sun (x tilt, z turn) toward the seen face
        ("war_map_back.png", (-4.2, -26.0, 2.6), (-5.0, -19.7, 2.25), 42, (55, 160)),
        ("war_map_front.png", (-5.8, -13.6, 2.6), (-5.0, -19.7, 2.25), 42, (55, -20)),
        ("war_map_close.png", (-4.4, -22.4, 2.7), (-4.6, -19.7, 2.5), 35, (55, 160)),
    )
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1200
    for name, at, target, lens, (tilt, turn) in shots:
        cam.location = Vector(at)
        cam.data.lens = lens
        look_at(cam, target)
        sun.rotation_euler = (math.radians(tilt), 0, math.radians(turn))
        sc.render.filepath = os.path.join(out, name)
        bpy.ops.render.render(write_still=True)
        print("WROTE", sc.render.filepath)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" not in argv:
        sys.exit("Preview only for now (not applied to Unity): -- --preview <folder>")
    preview(argv[argv.index("--preview") + 1])
