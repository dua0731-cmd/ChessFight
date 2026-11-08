"""Pawn Rush library (작전 회의실): the lower floor dressed as the war room's store and map room (R427).

    blender --background --factory-startup --python basement_dressing.py                       (writes the FBX + map)
    blender --background --factory-startup --python basement_dressing.py -- --preview D:/check (renders, writes nothing)

승규 님 (11-05): "지하실 구간이 살짝 밋밋해 작전 회의실 분위기 맞게 계단 경로 방해 하지 않는 경로에 모델링 해주고 텍스쳐
입혀". The lower floor (z -8, x +-14, y -22 .. 22) is where a pawn that falls off the conveyor lands; it leaves by the two
doors in the side walls (y -5.3 .. -1.9) to the return stairs. So nothing goes in the middle under the conveyor
(x +-4.5) or in the band across the room to the doors (y -7 .. -0.5); everything stands in the side aisles between
the inner piers (x +-9.05) and the walls, in the corners, on the walls above the bookcases, or at the far end:
  wall maps    ten framed campaign maps (3.2 x 2.2 m) on the side walls above the ten lower bookcases: a painted
               parchment map (coast and sea, an 8 x 8 grid of territories, dashed borders, red and blue arrows of
               advance, castles, a compass) in a dark wood frame with a teal inner rim and brass corners
  sand table   at the far end (x +-2.8, y 18.4 .. 21): a heavy war table, the same map laid on its top under a
               raised rim, little teal and gold rooks and pawns and pinned flags standing on it
  stores       in the four corners (x +-12, y -20.3 and 20.4): closed iron-bound crates (two side by side, one on top)
               and three barrels with iron hoops
  piece crates in the side aisles (x +-11.9, y -12.9 and 12.8): a pallet with three big carved pieces waiting to go
               up - a rook, a bishop and a pawn, cream marble on the left, dark wood on the right
  sconces      six brass lanterns on iron arms on the inner piers' wall sides (y -16, 0, 16), each with a warm light
Wood, crate wood and parchment as the R402 props (war_room_props.py, the same maps); the map is new
(Assets/Resources/PawnRushLibrary/WarProps/LibraryCampaignMap + _MS, "Library campaign map"); the lanterns' glass
"Library lantern glass" (lit from inside in Unity, Course01v4Builder.LibraryLooks). Everything collides as drawn
(LibraryCollision). Running it again changes nothing (it looks for Basement_sand_table first).
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import war_room_props as wp  # noqa: E402
from war_room_props import Geo, fnoise, rgb, lerp, save_maps, wood_uv  # noqa: E402

MARK = "Basement_sand_table"
FLOOR = -8.0
MAP, GLASS = "Library campaign map", "Library lantern glass"
TEAL, CREAM = "Library teal", "Library cream"
wp.MAPS[MAP] = "LibraryCampaignMap"
# Unity's flat looks (Course01v4Builder.LibraryLooks), in case the FBX has no such material yet
wp.FLATS.setdefault(TEAL, ((.30, .44, .47), .34, .05))
wp.FLATS.setdefault(CREAM, ((.88, .84, .78), .3, 0))
MAP_W, MAP_H = 3.2, 2.2
WALL_YS = (-16.0, -9.6, 3.2, 9.6, 16.0)        # the lower bookcases' centres
WALL_X = 14.70                                  # the side walls' inner face
rng = np.random.default_rng(426)


# ------------------------------------------------------------------ the map

def paint_map(base, w=1024, h=704):
    """A campaign map on parchment, drawn with y down from the top (metres of a 3.2 x 2.2 map), flipped at the end."""
    yy, xx = np.mgrid[0:h, 0:w]
    X, Y = (xx + .5) / w * MAP_W, (yy + .5) / h * MAP_H
    paper = np.ones((h, w, 3), np.float32) * rgb(222, 198, 150)
    paper = lerp(paper, np.ones_like(paper) * rgb(184, 146, 98), np.clip(fnoise(h, w, 3.0, 61) * .35 + .2, 0, 1) * .5)
    paper *= (1 + .05 * fnoise(h, w, 1.6, 62)[..., None])
    edge = np.minimum(np.minimum(X, MAP_W - X), np.minimum(Y, MAP_H - Y))
    paper = lerp(paper, np.ones_like(paper) * rgb(120, 82, 46), np.clip(1 - edge / .14, 0, 1) ** 1.6 * .8)
    col = paper
    # land and sea: a coast from noise, the sea washed blue-grey with wave strokes
    land_n = fnoise(h, w, 3.4, 63) + 1.6 * (X / MAP_W - .35)
    sea = land_n < 0
    coast = np.abs(land_n) < .045
    col[sea] = lerp(col[sea], np.ones((int(sea.sum()), 3)) * rgb(120, 146, 150), np.full(int(sea.sum()), .55))
    waves = sea & (np.abs(((Y * 9 + .25 * np.sin(X * 14)) % 1) - .5) < .03) & (fnoise(h, w, 2, 64) > .3)
    col[waves] *= .85
    col[coast] = rgb(86, 62, 40)
    # the 8 x 8 grid of territories, faint, and dashed borders
    gx, gy = X / MAP_W * 8, Y / MAP_H * 8
    grid = (np.minimum(np.abs(gx - np.round(gx)), np.abs(gy - np.round(gy))) < .012) & ~sea
    col[grid] = col[grid] * .82
    for k in range(5):
        x0 = rng.uniform(.6, 2.8)
        bx = x0 + .25 * np.sin(Y * rng.uniform(2, 5) + k)
        dash = (np.abs(X - bx) < .008) & ((Y * 18 % 1) < .55) & ~sea
        col[dash] = rgb(150, 50, 40) if k % 2 else rgb(50, 90, 96)
    # arrows of advance: thick curved strokes with heads, red and blue
    def arrow(p0, p1, bend, colour, width=.022):
        t = np.linspace(0, 1, 200)
        mx, my = (p0[0] + p1[0]) / 2 - bend * (p1[1] - p0[1]), (p0[1] + p1[1]) / 2 + bend * (p1[0] - p0[0])
        px = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * mx + t ** 2 * p1[0]
        py = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * my + t ** 2 * p1[1]
        m = np.zeros((h, w), bool)
        for i in range(0, 200, 2):
            r = width * (1 - .4 * t[i])
            cx, cy = int(px[i] / MAP_W * w), int(py[i] / MAP_H * h)
            rr = int(r / MAP_W * w) + 2
            sl = (slice(max(cy - rr, 0), cy + rr), slice(max(cx - rr, 0), cx + rr))
            m[sl] |= (X[sl] - px[i]) ** 2 + (Y[sl] - py[i]) ** 2 < r * r
        dx, dy = px[-1] - px[-8], py[-1] - py[-8]
        n = math.hypot(dx, dy)
        dx, dy = dx / n, dy / n
        tip = (px[-1] + dx * .09, py[-1] + dy * .09)
        left = (px[-1] - dy * .07, py[-1] + dx * .07)
        right = (px[-1] + dy * .07, py[-1] - dx * .07)
        m |= wp.poly_mask(X, Y, [tip, left, right])
        col[m] = colour
    arrow((.5, 1.7), (1.6, .9), .25, rgb(150, 44, 36))
    arrow((2.9, .5), (2.0, 1.2), -.2, rgb(44, 72, 120))
    arrow((2.7, 1.9), (1.9, 1.55), .15, rgb(44, 72, 120))
    arrow((.9, .5), (1.5, .55), -.3, rgb(150, 44, 36), .016)
    # castles: little crenellated towers, and a compass in the corner
    for cx, cy, c in ((1.75, .82, rgb(70, 50, 34)), (1.85, 1.45, rgb(70, 50, 34)), (.7, 1.35, rgb(150, 44, 36)),
                      (2.55, .75, rgb(44, 72, 120))):
        tower = wp.poly_mask(X, Y, [(cx - .05, cy + .06), (cx + .05, cy + .06), (cx + .05, cy - .05), (cx - .05, cy - .05)])
        for mx in (-.05, -.017, .017):
            tower |= wp.poly_mask(X, Y, [(mx + cx, cy - .05), (mx + cx + .033, cy - .05), (mx + cx + .033, cy - .08), (mx + cx, cy - .08)])
        col[tower] = c
    ccx, ccy = MAP_W - .38, MAP_H - .38
    r = np.hypot(X - ccx, Y - ccy)
    ang = np.arctan2(Y - ccy, X - ccx)
    star = r < .2 * (.35 + .65 * np.abs(np.cos(ang * 2)) ** 6)
    ring = np.abs(r - .22) < .008
    col[star | ring] = rgb(90, 64, 40)
    col[(r < .2 * (.2 + .8 * np.abs(np.cos(ang * 2)) ** 8)) & (np.cos(ang * 2) > 0) & (r < .19)] = rgb(150, 44, 36)
    smooth = np.full((h, w), .14, np.float32)
    save_maps(base, col[::-1], np.zeros((h, w)), smooth[::-1])


# ------------------------------------------------------------------ shapes

def lathe(g, c, profile, mat, seg=24, rect=(0, 0, 1, 1), smooth=True, cap=True):
    """A solid of revolution about the vertical through c (x, y, z0): profile = [(radius, height above z0)], bottom up."""
    cx, cy, z0 = c
    rings = []
    for r, z in profile:
        if r < 1e-4:                                     # a pole: one vertex shared round the ring
            pole = g.bm.verts.new((cx, cy, z0 + z))
            rings.append([pole] * seg)
        else:
            rings.append([g.bm.verts.new((cx + r * math.cos(2 * math.pi * k / seg), cy + r * math.sin(2 * math.pi * k / seg), z0 + z))
                          for k in range(seg)])
    u0, v0, u1, v1 = rect
    total = sum(math.hypot(profile[i + 1][0] - profile[i][0], profile[i + 1][1] - profile[i][1]) for i in range(len(profile) - 1)) or 1
    acc = 0
    for i in range(len(profile) - 1):
        step = math.hypot(profile[i + 1][0] - profile[i][0], profile[i + 1][1] - profile[i][1])
        va, vb = v0 + (v1 - v0) * acc / total, v0 + (v1 - v0) * (acc + step) / total
        acc += step
        for k in range(seg):
            j = (k + 1) % seg
            ua, ub = u0 + (u1 - u0) * k / seg, u0 + (u1 - u0) * (k + 1) / seg
            a, b = rings[i], rings[i + 1]
            if profile[i][0] < 1e-4 and profile[i + 1][0] < 1e-4:
                continue
            quad = [(a[k], (ua, va)), (a[j], (ub, va)), (b[j], (ub, vb)), (b[k], (ua, vb))]
            if a[k] is a[j]:
                quad = quad[1:]                          # a triangle to the pole
            elif b[k] is b[j]:
                quad = quad[:3]
            g.face([q[0] for q in quad], [q[1] for q in quad], mat, smooth)
    if cap:
        for idx, flip in ((0, True), (-1, False)):
            r, z = profile[idx]
            if r < 1e-4:
                continue
            vs = [g.bm.verts.new((cx + r * math.cos(2 * math.pi * k / seg), cy + r * math.sin(2 * math.pi * k / seg), z0 + z)) for k in range(seg)]
            uvs = [(.5 + .5 * math.cos(2 * math.pi * k / seg), .5 + .5 * math.sin(2 * math.pi * k / seg)) for k in range(seg)]
            if flip:
                vs, uvs = vs[::-1], uvs[::-1]
            g.face(vs, uvs, mat)


def map_box(lo, hi):
    """UVs that lay the whole map on a box's top (the others take a corner of it)."""
    def uv(n, s, pts):
        if n == 2 and s > 0:
            return [((p[0] - lo[0]) / (hi[0] - lo[0]), (p[1] - lo[1]) / (hi[1] - lo[1])) for p in pts]
        return [(.02, .02)] * 4
    return uv


def flat_uv(n, s, pts):
    return [(.5, .5)] * 4


# ------------------------------------------------------------------ the props

def wall_map(side, y):
    """A framed map on the side wall (side -1 = the left wall at x -14.7, facing +x)."""
    g = Geo("R427 wall map")
    out = -side
    zc = -2.65
    face = side * WALL_X
    hw, hh = MAP_W / 2, MAP_H / 2
    fr = .14

    def slab(y0, y1, z0, z1, d0, d1, mat, uv=None):
        x0, x1 = sorted((face + out * d0, face + out * d1))
        g.box((x0, y0, z0), (x1, y1, z1), mat, uv or wood_uv((x0, y0, z0), (x1, y1, z1)))
    # backing board, frame (four bars), teal rim, brass corners
    slab(y - hw - fr, y + hw + fr, zc - hh - fr, zc + hh + fr, 0, .03, wp.WOOD_DARK)
    for (y0, y1, z0, z1) in ((y - hw - fr, y + hw + fr, zc + hh, zc + hh + fr), (y - hw - fr, y + hw + fr, zc - hh - fr, zc - hh),
                             (y - hw - fr, y - hw, zc - hh, zc + hh), (y + hw, y + hw + fr, zc - hh, zc + hh)):
        slab(y0, y1, z0, z1, .03, .11, wp.WOOD)
    for (y0, y1, z0, z1) in ((y - hw, y + hw, zc + hh - .03, zc + hh), (y - hw, y + hw, zc - hh, zc - hh + .03),
                             (y - hw, y - hw + .03, zc - hh, zc + hh), (y + hw - .03, y + hw, zc - hh, zc + hh)):
        slab(y0, y1, z0, z1, .03, .075, TEAL, flat_uv)
    for cy in (y - hw - fr, y + hw + fr - .2):
        for cz in (zc - hh - fr, zc + hh + fr - .2):
            slab(cy, cy + .2, cz, cz + .2, .11, .125, wp.GOLD, flat_uv)
    # the map itself, just proud of the backing: u along the wall as seen from the room, v up
    x = face + out * .035
    pts = [(x, y - hw, zc - hh), (x, y + hw, zc - hh), (x, y + hw, zc + hh), (x, y - hw, zc + hh)]
    uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
    if side > 0:                       # the right wall is seen facing +x: left to right is -y
        uvs = [(1, 0), (0, 0), (0, 1), (1, 1)]
    vs = [g.bm.verts.new(p) for p in pts]
    n = (Vector(pts[1]) - Vector(pts[0])).cross(Vector(pts[3]) - Vector(pts[0]))
    if n.x * out < 0:
        vs, uvs = vs[::-1], uvs[::-1]
    g.face(vs, uvs, MAP)
    return g.mesh()


def mini_rook(g, c, s, mat):
    lathe(g, c, [(.0, 0), (.5 * s, 0), (.5 * s, .12 * s), (.36 * s, .2 * s), (.3 * s, .75 * s), (.42 * s, .82 * s), (.42 * s, 1.0 * s), (0, 1.0 * s)], mat, 12)


def mini_pawn(g, c, s, mat):
    lathe(g, c, [(.0, 0), (.45 * s, 0), (.45 * s, .1 * s), (.25 * s, .25 * s), (.14 * s, .62 * s), (.26 * s, .7 * s), (.2 * s, .9 * s), (.08 * s, 1.0 * s), (0, 1.0 * s)], mat, 12)


def sand_table():
    g = Geo("R427 sand table")
    x0, x1, y0, y1 = -2.8, 2.8, 18.4, 21.0
    top = FLOOR + .92
    # legs, aprons, a thick top with a raised rim; the map laid in the top
    for lx in (x0 + .12, x1 - .32):
        for ly in (y0 + .12, y1 - .32):
            g.box((lx, ly, FLOOR), (lx + .2, ly + .2, top - .12), wp.WOOD_DARK)
            g.box((lx - .03, ly - .03, FLOOR), (lx + .23, ly + .23, FLOOR + .08), wp.DARK, flat_uv)
    g.box((x0 + .1, y0 + .1, top - .3), (x1 - .1, y1 - .1, top - .12), wp.WOOD)
    g.box((x0 + .3, y0 + .3, FLOOR + .18), (x1 - .3, y0 + .42, FLOOR + .28), wp.WOOD_DARK)
    g.box((x0 + .3, y1 - .42, FLOOR + .18), (x1 - .3, y1 - .3, FLOOR + .28), wp.WOOD_DARK)
    g.box((x0, y0, top - .12), (x1, y1, top - .02), wp.WOOD_DARK)
    g.box((x0 + .14, y0 + .14, top - .02), (x1 - .14, y1 - .14, top), MAP, map_box((x0 + .14, y0 + .14), (x1 - .14, y1 - .14)))
    for (a0, b0, a1, b1) in ((x0, y0, x1, y0 + .14), (x0, y1 - .14, x1, y1), (x0, y0 + .14, x0 + .14, y1 - .14), (x1 - .14, y0 + .14, x1, y1 - .14)):
        g.box((a0, b0, top - .02), (a1, b1, top + .06), wp.WOOD)
        g.box((a0, b0, top + .06), (a1, b1, top + .075), TEAL, flat_uv)
    for cx in (x0, x1 - .16):
        for cy in (y0, y1 - .16):
            g.box((cx, cy, top + .06), (cx + .16, cy + .16, top + .09), wp.GOLD, flat_uv)
    # pieces and flags on the map
    for (px, py, kind, mat) in ((-1.6, 19.2, "rook", TEAL), (-1.1, 19.9, "pawn", TEAL), (-.7, 19.0, "pawn", TEAL),
                                (-1.9, 20.3, "pawn", TEAL), (1.5, 20.1, "rook", wp.GOLD), (1.0, 19.3, "pawn", wp.GOLD),
                                (.6, 20.4, "pawn", wp.GOLD), (2.0, 19.0, "pawn", wp.GOLD)):
        (mini_rook if kind == "rook" else mini_pawn)(g, (px, py, top), .2 if kind == "rook" else .16, mat)
    for (fx, fy, mat) in ((-.2, 19.6, TEAL), (.3, 19.9, wp.GOLD), (-.4, 20.5, TEAL), (.9, 18.9, wp.GOLD)):
        g.cyl((fx, fy, top), (fx, fy, top + .28), .008, wp.DARK, 6)
        g.box((fx, fy - .002, top + .19), (fx + .12, fy + .002, top + .27), mat, flat_uv)
    return g.mesh()


def closed_crate(g, c, rot=0.0, w=1.2, d=.9, h=.8):
    """An iron-bound crate with its lid on, standing at c (x, y, z) turned rot about z."""
    m = Matrix.Translation(c) @ Matrix.Rotation(rot, 4, "Z")
    X, Y = w / 2, d / 2

    def wuv(lo, hi):
        return wood_uv(lo, hi, .9)
    for k in range(4):                                   # four slats a side, the long sides and the ends
        z0, z1 = .02 + k * (h - .04) / 4 + .004, .02 + (k + 1) * (h - .04) / 4 - .004
        for sy in (-1, 1):
            lo, hi = (-X + .04, min(sy * Y, sy * (Y - .04)), z0), (X - .04, max(sy * Y, sy * (Y - .04)), z1)
            g.box(lo, hi, wp.CRATE, wuv(lo, hi), m)
        for sx in (-1, 1):
            lo, hi = (min(sx * X, sx * (X - .04)), -Y + .04, z0), (max(sx * X, sx * (X - .04)), Y - .04, z1)
            g.box(lo, hi, wp.CRATE, wuv(lo, hi), m)
    for k in range(5):                                   # lid boards
        y0 = -Y + k * d / 5 + .004
        lo, hi = (-X, y0, h - .04), (X, y0 + d / 5 - .008, h)
        g.box(lo, hi, wp.CRATE, wuv(lo, hi), m)
    g.box((-X + .04, -Y + .04, .02), (X - .04, Y - .04, h - .04), wp.WOOD_DARK, flat_uv, m)   # the inside, unseen
    for sx in (-1, 1):                                   # iron corners and bands
        for sy in (-1, 1):
            g.box((min(sx * (X + .005), sx * (X - .07)), min(sy * (Y + .005), sy * (Y - .07)), 0),
                  (max(sx * (X + .005), sx * (X - .07)), max(sy * (Y + .005), sy * (Y - .07)), h + .005), wp.DARK, flat_uv, m)
    for z0 in (.1, h - .14):
        g.box((-X - .006, -Y - .006, z0), (X + .006, Y + .006, z0 + .05), wp.DARK, flat_uv, m)
    for sy in (-1, 1):
        for x in (-X + .2, 0, X - .2):
            for z in (.125, h - .115):
                g.box((x - .015, min(sy * (Y + .006), sy * (Y + .016)), z - .015), (x + .015, max(sy * (Y + .006), sy * (Y + .016)), z + .015), wp.STEEL, flat_uv, m)


def barrel(g, c, h=1.0, r=.36):
    R = r * 1.14
    prof = [(0, 0)] + [(r + (R - r) * math.sin(math.pi * i / 10), h * i / 10) for i in range(11)] + [(0, h)]
    circ = 2 * math.pi * R
    lathe(g, c, prof, wp.CRATE, 24, (0, 0, circ / .9, h / .9))
    for t in (.08, .3, .7, .92):
        rr = r + (R - r) * math.sin(math.pi * t) + .008
        lathe(g, (c[0], c[1], c[2] + h * t - .025), [(rr, 0), (rr, .05)], wp.DARK, 24, cap=False)
    lathe(g, (c[0], c[1], c[2] + h - .01), [(r * .93, 0), (r * .93, .012)], wp.WOOD_DARK, 24)


def stores(side, y):
    g = Geo("R427 stores")
    x = side * 12.1
    closed_crate(g, (x - side * .1, y - .6, FLOOR), math.radians(4))
    closed_crate(g, (x + side * .05, y + .45, FLOOR), math.radians(-3))
    closed_crate(g, (x, y - .1, FLOOR + .8), math.radians(10), 1.0, .8, .65)
    for (bx, by) in ((x - side * 1.45, y - .9), (x - side * 1.35, y + .1), (x - side * 1.9, y - .35)):
        barrel(g, (bx, by, FLOOR))
    return g.mesh()


def big_piece(g, c, kind, mat):
    s = {"rook": 1.45, "bishop": 1.7, "pawn": 1.1}[kind]
    base = [(0, 0), (.36, 0), (.36, .08), (.32, .12), (.33, .16), (.26, .22)]
    if kind == "rook":
        body = [(.2, .3), (.17, .85), (.24, .9), (.24, 1.0), (.0, 1.0)]
    elif kind == "bishop":
        body = [(.15, .3), (.1, .7), (.18, .74), (.1, .78), (.13, .88), (.12, .96), (.06, 1.0), (.03, 1.03), (.0, 1.04)]
    else:
        body = [(.16, .3), (.1, .55), (.17, .6), (.12, .64), (.17, .72), (.17, .86), (.1, .94), (.0, .96)]
    prof = [(r * s * .75, z * s) for r, z in base + body]     # slimmer than a real set: three fit on the pallet
    lathe(g, c, prof, mat, 28)
    if kind == "rook":                                   # crenels on the top
        top = c[2] + s * 1.0
        for k in range(4):
            a = math.pi / 2 * k + math.pi / 4
            cx, cy = c[0] + math.cos(a) * .12 * s, c[1] + math.sin(a) * .12 * s
            g.box((cx - .045 * s, cy - .045 * s, top), (cx + .045 * s, cy + .045 * s, top + .1 * s), mat, flat_uv)


def piece_pallet(side, y):
    g = Geo("R427 piece pallet")
    x = side * 11.9
    mat = CREAM if side < 0 else wp.WOOD_DARK
    for k in range(3):                                   # pallet: three runners and seven boards
        lo, hi = (x - .95, y - .9 + k * .8, FLOOR), (x + .95, y - .78 + k * .8, FLOOR + .12)
        g.box(lo, hi, wp.CRATE, wood_uv(lo, hi, .9))
    for k in range(7):
        lo, hi = (x - .95 + k * .27, y - .9, FLOOR + .12), (x - .95 + k * .27 + .23, y + .9, FLOOR + .16)
        g.box(lo, hi, wp.CRATE, wood_uv(lo, hi, .9))
    z = FLOOR + .16
    big_piece(g, (x - side * .45, y - .4, z), "rook", mat)
    big_piece(g, (x + side * .45, y + .25, z), "bishop", mat)
    big_piece(g, (x - side * .45, y + .5, z), "pawn", mat)
    return g.mesh()


def sconce(side, y):
    """A brass lantern on an iron arm on the inner pier's wall side (side -1: the left pier, its face toward -x)."""
    g = Geo("R427 sconce")
    face = pier_back(side, y)
    out = side                                           # away from the hall's middle, toward the wall
    z = FLOOR + 3.6
    x0 = face
    g.box((min(x0, x0 + out * .03), y - .12, z + .1), (max(x0, x0 + out * .03), y + .12, z + .5), wp.DARK, flat_uv)
    g.cyl((x0, y, z + .38), (x0 + out * .42, y, z + .38), .025, wp.DARK, 8)
    g.cyl((x0 + out * .1, y, z + .14), (x0 + out * .3, y, z + .37), .018, wp.DARK, 8)
    lx = x0 + out * .42
    g.cyl((lx, y, z + .38), (lx, y, z + .3), .01, wp.DARK, 6)
    lathe(g, (lx, y, z - .25), [(.0, 0), (.12, 0), (.14, .04), (.14, .07), (.0, .07)], wp.GOLD, 8)
    lathe(g, (lx, y, z - .18), [(.1, 0), (.1, .34)], GLASS, 8, smooth=False)
    for k in range(8):
        a = 2 * math.pi * k / 8
        g.cyl((lx + .105 * math.cos(a), y + .105 * math.sin(a), z - .18), (lx + .105 * math.cos(a), y + .105 * math.sin(a), z + .16), .008, wp.GOLD, 5)
    lathe(g, (lx, y, z + .16), [(.15, 0), (.12, .06), (.04, .14), (.0, .16)], wp.GOLD, 8)
    return g.mesh(), (lx, y, z)


def pier_back(side, y_c):
    """The inner pier's face toward the side wall over the lantern's height."""
    best = None
    for o in bpy.data.objects:
        if o.type != "MESH" or "Inner_gallery_pier" not in o.name:
            continue
        c = o.matrix_world.translation
        if abs(c.y - y_c) > .5 or c.x * side < 0:
            continue
        mw = o.matrix_world
        for v in o.data.vertices:
            w = mw @ v.co
            if -5 < w.z < -3.3 and abs(w.y - y_c) < .3:
                val = w.x * side
                best = val if best is None else max(best, val)
    if best is None:
        raise RuntimeError(f"no pier at y {y_c}")
    return best * side


def glass_material():
    m = bpy.data.materials.get(GLASS)
    if m is None:
        m = bpy.data.materials.new(GLASS)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (1, .7, .35, 1)
        b.inputs["Emission Color"].default_value = (1, .62, .28, 1)
        b.inputs["Emission Strength"].default_value = 6
    return m


# ------------------------------------------------------------------ applying

def dress(col):
    if bpy.data.objects.get(MARK) is not None:
        print("basement: already dressed")
        return False
    glass_material()
    made = []

    def put(name, mesh):
        ob = bpy.data.objects.new(name, mesh)
        col.objects.link(ob)
        made.append(ob)
        return ob
    for side in (-1, 1):
        for i, y in enumerate(WALL_YS):
            put(f"Basement_wall_map_{side}_{i}", wall_map(side, y))
    put(MARK, sand_table())
    for side in (-1, 1):
        for j, y in enumerate((-20.3, 20.4)):
            put(f"Basement_stores_{side}_{j}", stores(side, y))
        for j, y in enumerate((-12.9, 12.8)):
            put(f"Basement_piece_pallet_{side}_{j}", piece_pallet(side, y))
        for j, y in enumerate((-16.0, 0.0, 16.0)):
            me, at = sconce(side, y)
            put(f"Basement_sconce_{side}_{j}", me)
            light = bpy.data.lights.new(f"Basement lantern 2700K {side} {j}", "POINT")
            light.energy, light.color = 90, (1.0, .78, .5)
            lo = bpy.data.objects.new(f"Basement lantern 2700K {side} {j}", light)
            lo.location = at
            col.objects.link(lo)
    print("basement:", len(made), "props,", sum(len(o.data.polygons) for o in made), "faces")
    return True


def preview(out):
    import cannon_conveyor_look as look
    os.makedirs(out, exist_ok=True)
    wp.OUT = out
    paint_map(os.path.join(out, "LibraryCampaignMap"))
    col = wp.load()
    dress(col)
    # The R402 materials come back from the FBX without their tint and maps: rebuilt as war_room_props makes them
    # (the project's maps), the new map from the preview folder.
    for name in list(wp.MAPS):
        old = bpy.data.materials.get(name)
        if old is None or name == MAP:
            continue
        old.name = name + " (fbx)"
        wp.OUT = os.path.join(wp.LIB, "WarProps")
        new = wp.material(name)
        old.user_remap(new)
    wp.OUT = out
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_backface_culling = True
        if m.name in wp.FLATS and m.use_nodes:
            c, sm, mt = wp.FLATS[m.name]
            b = m.node_tree.nodes["Principled BSDF"]
            b.inputs["Base Color"].default_value = (*[x ** 2.2 for x in c], 1)
            b.inputs["Metallic"].default_value = mt
            b.inputs["Roughness"].default_value = 1 - sm
        if m.name.startswith("CLAY"):
            m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.62, .55, .46, 1)
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
        elif o.type == "MESH":
            cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
            o.hide_render = min(c.z for c in cs) > -4.2 and not o.name.startswith("Basement_")
    sun, cam = look.stage(sc)
    look.shoot(sc, sun, cam, out, (
        ("basement_left_aisle.png", (-10.0, -23.5, -5.6), (-12.5, -8.0, -6.5), 24, 60),
        ("basement_wall_maps.png", (-9.8, 4.0, -5.4), (-14.7, 9.6, -3.0), 26, 100),
        ("basement_sand_table.png", (0, 14.0, -5.3), (0, 19.7, -7.1), 30, 160),
        ("basement_stores.png", (-8.6, -16.4, -6.0), (-12.1, -20.3, -7.4), 30, 30),
        ("basement_pieces.png", (-10.9, 16.2, -5.9), (-11.9, 12.8, -7.1), 30, 110),
        ("basement_overview.png", (0, -21.0, -3.0), (0, 0, -7.6), 22, 200),
    ))


def apply():
    from export_library_fbx import SETTINGS
    col = wp.load()
    if bpy.data.objects.get(MARK) is not None:
        print("basement: already dressed")
        return
    paint_map(os.path.join(wp.OUT, "LibraryCampaignMap"))
    dress(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=wp.FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {wp.FBX}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])
    else:
        apply()
