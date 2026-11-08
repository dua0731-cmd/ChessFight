"""Pawn Rush library (작전 회의실): new bookcases, pier banners, the scroll crate, the start / goal lantern posts gone (R402).

    blender --background --factory-startup --python war_room_props.py                       (writes the FBX + maps)
    blender --background --factory-startup --python war_room_props.py -- --preview D:/check (renders, writes nothing)

승규 님 (11-04, four pictures): "1. 책장 텍스쳐랑 모델링 다시 해 2. 깃발을 기둥 쪽에 왼쪽 기둥 오른쪽 기둥 각각 5개씩 둔다.
3. 5번 째 사진 소품을 사진 6번 사진 처럼 모델링 후 종이는 중세시대 종이 재질로 해서 넣는다. 4. 시작 구간 마지막 구간 램프 총 4개 지워라".

  bookcases   the 20 `…|GAME_Bookcase_and_bound_books|Dupli|` (one shared mesh, 3.84 x 1.12 x 3.94 m, front +y) get a
              new shared mesh in the same box, like the reference cabinet: a warm wood carcass in two bays, chunky front
              posts, a stepped cornice, a plinth and four drawers with brass knobs, dark iron corner caps with rivets, and
              four shelves of books (24 painted spines, gold bands, labels), some leaning, some lying in stacks, glazed
              jars between them. Wood tiles (Library bookcase wood / … dark), books from one atlas (Library bookcase books).
  banners     on the front of each inner gallery pier (x +-9.05, y -16 -9.6 0 9.6 16 - five a side), its face toward
              the start (-y), seen by everyone running the hall: a wool banner in the cannons' teal paint (R414; it was
              blue on the hall-side face, behind the lanterns' chains, R402), 1.1 x 3.3 m, pointed at the foot, gold
              border, a gold rook and two small stars, gently rippled, on an iron rod with brass finials and wall
              brackets, a brass tassel at the point (top z 6.4, point z 3.1).
  scroll box  the balcony chest (Balcony_chest_+-1) is now an open slatted crate in an iron frame like the reference box,
              and its scroll bundle (Balcony_scrolls_+-1) seven rolls of aged parchment standing in it, leaning one way,
              tied with string (Library crate wood, Library parchment - stains, fibres, darker rolled ends, spiral ends).
  lanterns    the four standing lantern posts (Lantern_post_+-1_-14.2 at the start, Lantern_post_+-1_19.5 at the goal)
              are deleted. They carry no light (the ten lights are the pier lanterns).

Maps in Assets/Resources/PawnRushLibrary/WarProps/ (<name>.png + <name>_MS.png: metallic RGB, smoothness A), the
materials by name in Unity's Course01v4Builder.LibraryLooks. Running it again repaints the maps and rebuilds the
banners (the same, unless changed here); the bookcases, crates and lantern posts are done once (Pier_banner_-1_0
marks them). The great door's lunette is door_teal.py.
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

LIB = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary"))
FBX = os.path.join(LIB, "PawnRushLibraryEnvironment.fbx")
OUT = os.path.join(LIB, "WarProps")
MARK = "Pier_banner_-1_0"

BOOKCASE_MESH = "GAME_Bookcase_and_bound_books"
CHEST_MESH = "GAME_KIT_Library_chest"
SCROLL_MESH = "GAME_KIT_Scroll_bundle"
LANTERN_POSTS = ("Lantern_post_-1_-14.2", "Lantern_post_1_-14.2", "Lantern_post_-1_19.5", "Lantern_post_1_19.5")
PIER_YS = (-16.0, -9.6, 0.0, 9.6, 16.0)
PIER_X = 9.05

WOOD, WOOD_DARK, BOOKS = "Library bookcase wood", "Library bookcase wood dark", "Library bookcase books"
CRATE, PAPER, BANNER = "Library crate wood", "Library parchment", "Library banner"
DARK, GOLD, STEEL = "Library dark", "Library gold", "Library steel"
# Unity's flat looks for these three (Course01v4Builder.LibraryLooks), sRGB, for the preview
FLATS = {DARK: ((.19, .19, .21), .3, .2), GOLD: ((.75, .61, .32), .42, .6), STEEL: ((.27, .28, .30), .4, .55)}
TINTS = {WOOD_DARK: (.55, .47, .42)}     # the same wood map, darker (the back boards, the plinth)


# ------------------------------------------------------------------ geometry

class Geo:
    """One mesh built from boxes, cylinders and grids, with per-loop UVs and named material slots."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.mats = []

    def slot(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def face(self, verts, uvs, mat, smooth=False):
        f = self.bm.faces.new(verts)
        f.material_index = self.slot(mat)
        f.smooth = smooth
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        return f

    def box(self, lo, hi, mat, uv=None, m=None):
        """An axis-aligned box (lo, hi) in its own frame, moved by m; uv(n, s, lo, hi) -> per-corner UVs."""
        uv = uv or wood_uv(lo, hi)
        m = m or Matrix.Identity(4)
        mirrored = m.to_3x3().determinant() < 0
        for n in range(3):
            a, b = (n + 1) % 3, (n + 2) % 3
            for s in (-1, 1):
                pts = []
                for ca, cb in ((0, 0), (1, 0), (1, 1), (0, 1)):
                    p = [0.0, 0.0, 0.0]
                    p[n] = hi[n] if s > 0 else lo[n]
                    p[a] = hi[a] if ca else lo[a]
                    p[b] = hi[b] if cb else lo[b]
                    pts.append(p)
                uvs = uv(n, s, pts)
                if (s < 0) != mirrored:
                    pts, uvs = pts[::-1], uvs[::-1]
                self.face([self.bm.verts.new(m @ Vector(p)) for p in pts], uvs, mat)

    def cyl(self, p0, p1, r, mat, seg=16, rect=(0, 0, 1, 1), cap_mat=None, cap_rect=None, r1=None, caps=True):
        """A cylinder (or cone, r1) from p0 to p1; the side's UVs fill rect (u round, v along), the caps cap_rect."""
        p0, p1 = Vector(p0), Vector(p1)
        d = (p1 - p0).normalized()
        e1 = d.orthogonal().normalized()
        e2 = d.cross(e1)
        r1 = r if r1 is None else r1
        u0, v0, u1, v1 = rect
        rings = []
        for p, rad in ((p0, r), (p1, r1)):
            rings.append([self.bm.verts.new(p + rad * (math.cos(2 * math.pi * k / seg) * e1 + math.sin(2 * math.pi * k / seg) * e2))
                          for k in range(seg)])
        for k in range(seg):
            j = (k + 1) % seg
            ua, ub = u0 + (u1 - u0) * k / seg, u0 + (u1 - u0) * (k + 1) / seg
            self.face((rings[0][k], rings[0][j], rings[1][j], rings[1][k]), ((ua, v0), (ub, v0), (ub, v1), (ua, v1)), mat, True)
        if not caps:
            return
        cm = cap_mat or mat
        cu0, cv0, cu1, cv1 = cap_rect or rect
        for p, rad, flip in ((p0, r, True), (p1, r1, False)):
            if rad < 1e-4:
                continue
            vs, uvs = [], []
            for k in range(seg):
                a = 2 * math.pi * k / seg
                vs.append(self.bm.verts.new(p + rad * (math.cos(a) * e1 + math.sin(a) * e2)))
                uvs.append((cu0 + (cu1 - cu0) * (.5 + .5 * math.cos(a)), cv0 + (cv1 - cv0) * (.5 + .5 * math.sin(a))))
            if flip:
                vs, uvs = vs[::-1], uvs[::-1]
            self.face(vs, uvs, cm)

    def ball(self, c, r, mat, squash=1.0):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=12, v_segments=8, radius=r)
        idx = self.slot(mat)
        for v in res["verts"]:
            v.co = Vector(c) + Vector((v.co.x, v.co.y, v.co.z * squash))
        for f in {f for v in res["verts"] for f in v.link_faces}:
            f.material_index = idx
            f.smooth = True
            for loop in f.loops:
                loop[self.uv].uv = (.5, .5)

    def mesh(self):
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for name in self.mats:
            me.materials.append(material(name))
        return me


def wood_uv(lo, hi, tile=1.0, seed=None):
    """Tiled wood: 1 map = `tile` m, the grain (the map's v) along the box's longest side, offset per box."""
    size = [hi[i] - lo[i] for i in range(3)]
    g = max(range(3), key=lambda i: size[i])
    rnd = np.random.default_rng(seed if seed is not None else int(abs(lo[0] * 731 + lo[1] * 977 + lo[2] * 389) * 1000) % 2 ** 31)
    off = rnd.uniform(0, 1, 2)

    def uv(n, s, pts):
        a, b = (n + 1) % 3, (n + 2) % 3
        if g == a:
            ua, va = b, a
        elif g == b:
            ua, va = a, b
        else:
            ua, va = a, b
        return [(p[ua] / tile + off[0], p[va] / tile + off[1]) for p in pts]
    return uv


# ------------------------------------------------------------------ maps

S_PX = 2048


def fnoise(h, w, beta=2.0, seed=0, stretch=(1.0, 1.0)):
    """Seamless 1/f noise (zero mean, unit spread); stretch > 1 on an axis makes its features that much longer."""
    rng = np.random.default_rng(seed)
    wn = rng.standard_normal((h, w))
    fy = np.fft.fftfreq(h)[:, None] * stretch[0]
    fx = np.fft.fftfreq(w)[None, :] * stretch[1]
    f = np.sqrt(fx ** 2 + fy ** 2)
    f[0, 0] = 1
    spec = np.fft.fft2(wn) / f ** (beta / 2)
    spec[0, 0] = 0
    n = np.real(np.fft.ifft2(spec))
    return (n - n.mean()) / (n.std() + 1e-9)


def rgb(*c):
    return np.array(c, dtype=np.float32) / 255.0


def lerp(a, b, t):
    return a + (b - a) * t[..., None]


def save_png(path, col, alpha=None, colour="sRGB"):
    """col (H, W, 3) with row 0 at the bottom (Blender's pixel order, = UV v 0)."""
    h, w = col.shape[:2]
    a = np.ones((h, w)) if alpha is None else alpha
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    img.colorspace_settings.name = colour
    img.pixels.foreach_set(np.concatenate([np.clip(col, 0, 1), np.clip(a, 0, 1)[..., None]], -1).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("WROTE", path, w, "x", h)
    return img


def save_maps(base, col, metal, smooth):
    save_png(base + ".png", col)
    save_png(base + "_MS.png", np.repeat(np.asarray(metal, np.float32)[..., None], 3, -1) * np.ones_like(col), smooth, "Non-Color")


def paint_wood(base, size=1024, light=(198, 122, 64), mid=(158, 88, 42), dark=(104, 54, 26), seed=3, grey=0.0):
    """Tiling wood, grain along v (rows = v)."""
    big = fnoise(size, size, 3.2, seed, (14, 1))
    fine = fnoise(size, size, 1.3, seed + 1, (40, 1))
    blotch = fnoise(size, size, 3.5, seed + 2)
    ring = .5 + .5 * np.sin(2 * math.pi * (big * 1.6 + fine * .08))
    t = np.clip(ring ** 1.8 * .8 + .1 * fine + .08, 0, 1)
    col = lerp(rgb(*light), rgb(*mid), np.clip(t * 1.4, 0, 1))
    col = lerp(col, rgb(*dark), np.clip((t - .62) * 2.6, 0, 1))
    col *= (1 + .07 * blotch[..., None])
    pores = (fnoise(size, size, .6, seed + 4, (60, 1)) > 2.2)
    col[pores] *= .82
    if grey:
        g = col.mean(-1, keepdims=True)
        weather = np.clip(.5 + .5 * fnoise(size, size, 3.0, seed + 5), 0, 1)[..., None] * grey
        col = col * (1 - weather) + (g * .9 + rgb(120, 112, 100) * .1) * weather
    smooth = np.clip(.28 - .1 * t + .03 * blotch, .08, .5)
    save_maps(base, col, np.zeros(col.shape[:2]), smooth)


# The book spines: 24 cells of 256 x 512 px in an 8 x 3 grid above a 512 px band of pages and jar glazes.
SPINES = [(132, 34, 30), (92, 26, 28), (36, 52, 98), (46, 78, 142), (40, 84, 52), (34, 98, 96), (112, 66, 34),
          (166, 120, 70), (40, 34, 32), (78, 42, 88), (178, 132, 44), (206, 186, 146), (170, 86, 36), (102, 96, 46),
          (72, 82, 94), (120, 30, 50), (58, 110, 70), (150, 50, 30), (30, 70, 80), (190, 160, 110), (90, 60, 40),
          (60, 40, 110), (140, 100, 60), (25, 45, 60)]
GLAZES = [(52, 96, 128), (150, 62, 40), (196, 180, 140), (60, 110, 84), (120, 70, 40), (180, 140, 60), (90, 60, 100),
          (40, 60, 70)]


def cell_rect(i, spine=True):
    c, r = i % 8, i // 8
    u = (c * 256 + (8 if spine else 182)) / S_PX, (c * 256 + (168 if spine else 248)) / S_PX
    v = (512 + r * 512 + 8) / S_PX, (512 + (r + 1) * 512 - 8) / S_PX
    return u[0], v[0], u[1], v[1]


PAGES_RECT = (8 / S_PX, 8 / S_PX, 1016 / S_PX, 248 / S_PX)


def glaze_rect(i):
    return ((1024 + i * 128 + 6) / S_PX, 6 / S_PX, (1024 + (i + 1) * 128 - 6) / S_PX, 250 / S_PX)


def paint_books(base):
    S = S_PX
    rng = np.random.default_rng(11)
    col = np.zeros((S, S, 3), np.float32)
    metal = np.zeros((S, S), np.float32)
    smooth = np.full((S, S), .3, np.float32)
    grain = fnoise(S, S, 1.6, 21)
    gold, gold_dark = rgb(206, 164, 82), rgb(122, 88, 40)
    for i, c in enumerate(SPINES):
        cx, cy = (i % 8) * 256, 512 + (i // 8) * 512
        leather = rgb(*c)
        blk = np.ones((512, 256, 3), np.float32) * leather
        n = grain[cy:cy + 512, cx:cx + 256]
        blk *= (1 + .06 * n[..., None])
        xs = np.arange(256)[None, :]
        ys = np.arange(512)[:, None]
        # the spine's round: darker towards its edges (only the spine part, x 8..168)
        sx = np.clip((xs - 8) / 160, 0, 1)
        shade = .72 + .28 * np.sin(np.pi * sx) ** .6
        spine = (xs < 172)
        blk = np.where(spine[..., None], blk * shade[..., None], blk)
        m_blk = np.zeros((512, 256), np.float32)
        s_blk = np.full((512, 256), .32 + .05 * rng.random(), np.float32)
        # gold bands (raised bands of an old binding), from the top and the bottom (row 0 = the foot)
        style = rng.integers(0, 4)
        bands = {0: (40, 70, 440, 470), 1: (34, 58, 82, 430, 454, 478), 2: (60, 452), 3: (46, 96, 416, 466)}[style]
        for y in bands:
            band = (np.abs(ys - y) < 5) & spine
            edge = (np.abs(np.abs(ys - y) - 5.5) < 1.5) & spine
            blk[band] = gold * (.85 + .15 * rng.random())
            blk[edge] = gold_dark
            m_blk[band] = .7
            s_blk[band] = .5
        # a label or a gold title block in the upper half
        if rng.random() < .55:
            lab = rgb(*(rng.choice([(214, 196, 150), (40, 30, 26), (140, 34, 30), (30, 40, 70)])))
            y0, y1 = 300, 380
            inside = (ys > y0) & (ys < y1) & (xs > 28) & (xs < 148)
            blk[inside] = lab
            ticks = inside & (np.abs(ys - (y0 + y1) / 2) < 4) & ((xs // 9) % 2 == 0) & (xs > 40) & (xs < 136)
            blk[ticks] = gold if lab.mean() < .4 else rgb(60, 40, 30)
            frame = ((np.abs(ys - y0) < 2) | (np.abs(ys - y1) < 2)) & (xs > 26) & (xs < 150)
            blk[frame] = gold
            m_blk[frame] = .7
        else:
            for k in range(3):
                y = 330 + k * 22
                t = (np.abs(ys - y) < 3) & (xs > 50 + rng.integers(0, 20)) & (xs < 110 + rng.integers(0, 20)) & ((xs // 6) % 3 != 0)
                blk[t] = gold
                m_blk[t] = .6
        # a small ornament low on the spine
        if rng.random() < .5:
            d = (np.abs(xs - 88) + np.abs(ys - 160)) < 14
            blk[d] = gold
            m_blk[d] = .7
        # wear on the corners and edges
        wear = (np.minimum(ys, 511 - ys) < 10) | ((xs > 160) & (xs < 172))
        blk[wear] *= .75
        col[cy:cy + 512, cx:cx + 256] = blk
        metal[cy:cy + 512, cx:cx + 256] = m_blk
        smooth[cy:cy + 512, cx:cx + 256] = s_blk
    # pages: cream with fine lines along u
    pg = np.ones((256, 1024, 3), np.float32) * rgb(226, 212, 176)
    yy = np.arange(256)[:, None]
    lines = .9 + .1 * np.sin(yy * 2.1 + fnoise(256, 1024, 2, 31)[..., :] * 2)
    pg *= lines[..., None]
    pg *= (1 + .05 * fnoise(256, 1024, 2.5, 32)[..., None])
    col[0:256, 0:1024] = pg
    smooth[0:256, 0:1024] = .12
    # jar glazes: a soft vertical gradient and a highlight stripe
    for i, c in enumerate(GLAZES):
        x0 = 1024 + i * 128
        g = np.ones((256, 128, 3), np.float32) * rgb(*c)
        yv = np.arange(256)[:, None] / 255
        xv = np.arange(128)[None, :] / 127
        g *= (.8 + .25 * yv)[..., None]
        hl = np.exp(-((xv - .3) / .05) ** 2) * .35
        g = g + hl[..., None] * (1 - g)
        col[0:256, x0:x0 + 128] = g
        smooth[0:256, x0:x0 + 128] = .72
    # the rest of the bottom band (unused) a neutral leather
    col[256:512] = rgb(110, 70, 40)
    save_maps(base, col, metal, smooth)


def paint_parchment(base, size=1024):
    """Aged parchment (u 0 .. .75, its v ends darker: a roll's ends), a rolled end (u .75 .., v .75 ..), string."""
    col = np.zeros((size, size, 3), np.float32)
    smooth = np.full((size, size), .12, np.float32)
    w = 768
    stain = fnoise(size, w, 3.0, 41)
    mottle = fnoise(size, w, 1.8, 42)
    fib = fnoise(size, w, 1.0, 43, (1, 12))
    p = np.ones((size, w, 3), np.float32) * rgb(224, 199, 152)
    p = lerp(p, np.ones_like(p) * rgb(186, 146, 96), np.clip(stain * .35 + .2, 0, 1) * .55)
    p *= (1 + .05 * mottle[..., None] + .025 * fib[..., None])
    spots = fnoise(size, w, 2.2, 44) > 2.3
    p[spots] *= .82
    v = np.arange(size)[:, None] / (size - 1)
    end = np.clip(1 - np.minimum(v, 1 - v) / .1, 0, 1) ** 1.5           # the rolled ends: browned and worn
    p = lerp(p, np.ones_like(p) * rgb(140, 98, 58), end[:, 0] [:, None] * np.ones(w)[None, :] * .7)
    # faint ink lines seen through the outer turn
    ink = (np.abs(((v * 64) % 1) - .5) < .02) & (np.arange(w)[None, :] % 97 < 70) & (v > .15) & (v < .85)
    p[ink] = p[ink] * .88
    col[:, :w] = p
    # the rolled end: a spiral of paper edges
    n = size - w
    yy, xx = np.mgrid[0:n, 0:n]
    x, y = (xx + .5) / n * 2 - 1, (yy + .5) / n * 2 - 1
    r, th = np.sqrt(x * x + y * y), np.arctan2(y, x)
    sp = np.ones((n, n, 3), np.float32) * rgb(214, 186, 138)
    turn = ((r * 9 - th / (2 * math.pi)) % 1)
    sp[turn < .16] = rgb(120, 86, 52)
    sp *= (1 - .25 * np.clip(r - .7, 0, 1)[..., None] / .3)
    sp[r < .14] = rgb(70, 50, 34)
    col[size - n:, w:] = sp
    # string (red-brown, twisted)
    s = np.ones((n, n, 3), np.float32) * rgb(128, 52, 34)
    s *= (.8 + .2 * np.sin((xx + yy) * .35))[..., None]
    col[size - 2 * n:size - n, w:] = s
    col[:size - 2 * n, w:] = rgb(214, 186, 138)
    save_maps(base, col, np.zeros((size, size)), smooth)


PAPER_RECT = (0.0, 0.0, .75, 1.0)
PAPER_END = (.755, .755, .995, .995)
STRING_RECT = (.76, .51, .99, .74)

# The banner, in metres: 1.1 wide, 3.3 long, square for 80 % of it then a point.
BW, BL, BRECT = 1.1, 3.3, .8


def banner_outline():
    return [(0, 0), (BW, 0), (BW, BL * BRECT), (BW / 2, BL), (0, BL * BRECT)]


def seg_dist(px, py, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = np.clip(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy), 0, 1)
    return np.hypot(px - ax - t * dx, py - ay - t * dy)


def poly_mask(px, py, pts):
    """Inside a polygon (even-odd)."""
    inside = np.zeros(px.shape, bool)
    n = len(pts)
    for i in range(n):
        (x1, y1), (x2, y2) = pts[i], pts[(i + 1) % n]
        cond = (y1 > py) != (y2 > py)
        xint = (x2 - x1) * (py - y1) / (y2 - y1 + 1e-12) + x1
        inside ^= cond & (px < xint)
    return inside


def paint_banner(base, w=1024, h=2048):
    """Drawn with Y down from the top (metres), flipped to Blender's rows at the end."""
    yy, xx = np.mgrid[0:h, 0:w]
    X, Y = (xx + .5) / w * BW, (yy + .5) / h * BL
    out = banner_outline()
    d = np.min([seg_dist(X, Y, out[i], out[(i + 1) % len(out)]) for i in range(len(out))], axis=0)
    blue, blue_dark = rgb(77, 112, 120), rgb(36, 58, 64)   # R414: the cannons' teal paint (was blue)
    gold, gold_lo, gold_hi = rgb(214, 170, 82), rgb(140, 98, 40), rgb(240, 208, 128)
    weave = .5 * fnoise(h, w, .4, 51) + .5 * np.sin(xx * 1.9) * np.sin(yy * 1.9)
    col = np.ones((h, w, 3), np.float32) * blue
    col *= (1 + .045 * weave[..., None] + .05 * fnoise(h, w, 2.5, 52)[..., None])
    folds = .5 + .5 * np.sin(X / BW * 2 * math.pi * 3 + .6)
    col *= (.9 + .12 * folds)[..., None]
    metal = np.zeros((h, w), np.float32)
    smooth = np.full((h, w), .1, np.float32)

    def paint(mask, c, m=.55, s=.45):
        col[mask] = c
        metal[mask] = m
        smooth[mask] = s

    # top sleeve over the rod, a gold line under it
    sleeve = Y < .13
    col[sleeve] = col[sleeve] * .78
    paint((Y > .13) & (Y < .16), gold)
    # gold border following the outline, a thin inner line
    paint((d > .05) & (d < .088) & (Y > .2), gold)
    paint((d > .088) & (d < .096) & (Y > .2), gold_lo)
    paint((d > .118) & (d < .127) & (Y > .23), gold)
    # the rook (centre x .55, crown at y .9 .. base 2.15)
    cx = BW / 2
    parts = [
        [(cx - .30, 2.15), (cx + .30, 2.15), (cx + .30, 2.04), (cx - .30, 2.04)],
        [(cx - .25, 2.04), (cx + .25, 2.04), (cx + .25, 1.96), (cx - .25, 1.96)],
        [(cx - .20, 1.96), (cx + .20, 1.96), (cx + .155, 1.26), (cx - .155, 1.26)],
        [(cx - .23, 1.26), (cx + .23, 1.26), (cx + .23, 1.17), (cx - .23, 1.17)],
        [(cx - .26, 1.17), (cx + .26, 1.17), (cx + .26, 1.03), (cx - .26, 1.03)],
    ]
    for mx in (-.205, 0, .205):
        parts.append([(cx + mx - .055, 1.03), (cx + mx + .055, 1.03), (cx + mx + .055, .9), (cx + mx - .055, .9)])
    rook = np.zeros((h, w), bool)
    for pts in parts:
        rook |= poly_mask(X, Y, pts)
    # dark outline around the rook (grown by ~1.5 cm)
    grown = rook.copy()
    for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1)):
        k = 14
        grown |= np.roll(np.roll(rook, dy * k, 0), dx * k, 1)
    col[grown & ~rook] = blue_dark
    shade = np.clip(.5 + (cx - X) * 1.4 + .15 * fnoise(h, w, 2.0, 53), 0, 1)
    goldmix = lerp(np.ones((h, w, 3)) * gold_lo, np.ones((h, w, 3)) * gold_hi, shade)
    col[rook] = goldmix[rook]
    metal[rook], smooth[rook] = .55, .45
    # grooves: the stone courses on the body, the bands' edges, an arrow slit
    for y in (1.42, 1.62, 1.82):
        g = rook & (np.abs(Y - y) < .008)
        col[g] = gold_lo
    for y in (1.26, 1.17, 1.03, 2.04, 1.96):
        g = rook & (np.abs(Y - y) < .007)
        col[g] = gold_lo
    slit = (np.abs(X - cx) < .022) & (Y > 1.45) & (Y < 1.6)
    col[slit] = blue_dark
    metal[slit] = 0
    # two small four-point stars low on the cloth, a diamond above the point
    for sx in (.27, BW - .27):
        sy = 2.42
        a = np.abs(X - sx) / .07 + np.abs(Y - sy) / .07
        star = (np.minimum(np.abs(X - sx) / .018, np.abs(Y - sy) / .018) < 1) & (a < 1) | (a < .35)
        paint(star, gold)
    dm = np.abs(X - cx) / .06 + np.abs(Y - 2.86) / .09 < 1
    paint(dm, gold)
    # outside the outline: the cloth's colour (the mesh stops at the outline)
    col, metal, smooth = col[::-1], metal[::-1], smooth[::-1]
    save_maps(base, col, metal, smooth)


# ------------------------------------------------------------------ the models

def book(g, lo, hi, cell, m=None):
    """A book: spine on +y, its back on -y; the thinnest side shows the covers, the rest page edges."""
    size = [hi[i] - lo[i] for i in range(3)]
    thin = min((0, 2), key=lambda i: size[i])          # x (standing) or z (lying)
    long_ = 2 if thin == 0 else 0

    def uv(n, s, pts):
        if n == 1:
            u0, v0, u1, v1 = cell_rect(cell, spine=(s > 0))
            ua, va = thin, long_
        elif n == thin:
            u0, v0, u1, v1 = cell_rect(cell, spine=False)
            ua, va = 1, long_
        else:
            u0, v0, u1, v1 = PAGES_RECT
            ua, va = 1, thin
        return [(u0 + (u1 - u0) * (p[ua] - lo[ua]) / (hi[ua] - lo[ua]), v0 + (v1 - v0) * (p[va] - lo[va]) / (hi[va] - lo[va]))
                for p in pts]
    g.box(lo, hi, BOOKS, uv, m)


def fill_shelf(g, rng, x0, x1, zb, clear, front=.33):
    x = x0 + rng.uniform(0, .03)
    while x < x1 - .04:
        r = rng.random()
        if r < .09 and x1 - x > .26:
            rad, ht = rng.uniform(.06, .09), rng.uniform(.15, .25)
            c = (x + rad + .01, front - .14, zb)
            k = int(rng.integers(0, len(GLAZES)))
            g.cyl(c, (c[0], c[1], zb + ht), rad, BOOKS, 16, glaze_rect(k))
            g.cyl((c[0], c[1], zb + ht), (c[0], c[1], zb + ht + .025), rad * .82, GOLD, 16)
            g.cyl((c[0], c[1], zb + ht + .025), (c[0], c[1], zb + ht + .05), rad * .25, GOLD, 10)
            x += 2 * rad + rng.uniform(.03, .08)
        elif r < .17 and x1 - x > .5:
            length = rng.uniform(.3, .44)
            z = zb
            xs = x + .02
            for k in range(int(rng.integers(3, 6))):
                t, d = rng.uniform(.035, .07), rng.uniform(.22, .3)
                l2 = length - rng.uniform(0, .05)
                dx = rng.uniform(-.015, .015)
                book(g, (xs + dx, front - d, z), (xs + dx + l2, front - rng.uniform(0, .02), z + t), int(rng.integers(0, 24)))
                z += t
                if z > zb + clear - .12:
                    break
            x += length + rng.uniform(.04, .1)
        elif r < .21:
            x += rng.uniform(.05, .14)
        else:
            for k in range(int(rng.integers(4, 13))):
                t, h, d = rng.uniform(.035, .085), min(rng.uniform(.3, .58), clear - .05), rng.uniform(.22, .32)
                if x + t > x1:
                    break
                yf = front - rng.uniform(0, .025)
                book(g, (x, yf - d, zb), (x + t, yf, zb + h), int(rng.integers(0, 24)))
                x += t + .002
            if x1 - x > .3 and rng.random() < .4:
                a = math.radians(rng.uniform(8, 18))
                t, h, d = rng.uniform(.04, .08), min(rng.uniform(.32, .55), clear - .06), rng.uniform(.22, .3)
                px = x + h * math.sin(a)
                m = Matrix.Translation((px, 0, zb)) @ Matrix.Rotation(-a, 4, "Y")
                yf = front - .01
                book(g, (0, yf - d, 0), (t, yf, h), int(rng.integers(0, 24)), m)
                x = px + t * math.cos(a) + rng.uniform(.04, .1)


def build_bookcase():
    """Local: x across (+-1.92), y depth (front +y, +-0.56), z up (0 .. 3.94), as the old mesh."""
    g = Geo("R402 bookcase")
    rng = np.random.default_rng(400)
    W = 1.92
    B, F = -.52, .52
    g.box((-W + .02, B, 0), (W - .02, .50, .14), WOOD_DARK)                                       # plinth
    for x in (-W + .02, W - .12):                                                                  # sides
        g.box((x, B, .14), (x + .10, .44, 3.62), WOOD)
    for k in range(8):                                                                             # back boards
        x0 = -W + .12 + k * (2 * W - .24) / 8
        g.box((x0 + .002, B, .14), (x0 + (2 * W - .24) / 8 - .002, B + .05, 3.62), WOOD_DARK)
    g.box((-.05, B + .05, .14), (.05, .44, 3.62), WOOD)                                           # middle divider
    for x in (-W, -.08, W - .16):                                                                  # front posts
        g.box((x, .36, .14), (x + .16, F, 3.62), WOOD)
    # drawers (two a bay) under the first shelf
    g.box((-W + .12, .36, .66), (W - .12, .50, .74), WOOD)
    for bx0, bx1 in ((-W + .16, -.08), (.08, W - .16)):
        mid = (bx0 + bx1) / 2
        for d0, d1 in ((bx0 + .02, mid - .02), (mid + .02, bx1 - .02)):
            g.box((d0, .38, .17), (d1, .47, .63), WOOD)
            g.box((d0 + .06, .47, .23), (d1 - .06, .495, .57), WOOD)
            c = (d0 + d1) / 2
            g.box((c - .045, .495, .355), (c + .045, .505, .445), GOLD)
            g.cyl((c, .505, .40), (c, .55, .40), .028, GOLD, 12)
        g.box((mid - .02, .38, .14), (mid + .02, .48, .66), WOOD_DARK)
    # shelves, each with a front lip
    levels = (.74, 1.47, 2.17, 2.87)
    for z in levels[1:] + (3.62,):
        for bx0, bx1 in ((-W + .12, -.05), (.05, W - .12)):
            g.box((bx0, B + .05, z - .05), (bx1, .40, z), WOOD)
            g.box((bx0, .36, z - .07), (bx1, .44, z + .005), WOOD)
    for bx0, bx1 in ((-W + .12, -.05), (.05, W - .12)):
        g.box((bx0, B + .05, .70), (bx1, .36, .74), WOOD)
    # cornice: three steps
    g.box((-W - .03, B, 3.62), (W + .03, .54, 3.70), WOOD)
    g.box((-W - .01, B, 3.70), (W + .01, .52, 3.84), WOOD_DARK)
    g.box((-W - .06, B, 3.84), (W + .06, .56, 3.94), WOOD)
    # iron corner caps with rivets, top and bottom of each post, and on the cornice's front corners
    for x in (-W, -.08, W - .16):
        for z0, z1 in ((.14, .36), (3.40, 3.62)):
            g.box((x - .012, F, z0), (x + .172, F + .014, z1), DARK)
            for zz in (z0 + .05, z1 - .05):
                for xx in (x + .035, x + .125):
                    g.box((xx - .013, F + .014, zz - .013), (xx + .013, F + .024, zz + .013), STEEL)
    for s in (-1, 1):
        x_out = s * W
        for z0, z1 in ((.14, .36), (3.40, 3.62)):
            lo, hi = (min(x_out, x_out + s * .014), .30, z0), (max(x_out, x_out + s * .014), F + .014, z1)
            g.box(lo, hi, DARK)
        cx = s * (W + .06)
        g.box((min(cx, cx - s * .2), .55, 3.83), (max(cx, cx - s * .2), .575, 3.95), DARK)
        g.box((min(cx, cx + s * .016), .36, 3.83), (max(cx, cx + s * .016), .575, 3.95), DARK)
    # contents
    clear = .62
    for bx0, bx1 in ((-W + .14, -.07), (.07, W - .14)):
        for z in levels:
            fill_shelf(g, rng, bx0, bx1, z, clear)
    return g.mesh()


def build_crate():
    """Local (as the old chest): x across (+-0.65), y depth (+-0.47), z up; open on top, slats in an iron frame."""
    g = Geo("R402 scroll crate")
    X, Y, H = .65, .46, .80

    def wuv(lo, hi):
        return wood_uv(lo, hi, .9)
    g.box((-X + .03, -Y + .03, .05), (X - .03, Y - .03, .09), CRATE, wuv((-X, -Y, 0), (X, Y, .1)))
    for y0 in (-Y + .03, Y - .13):                                                               # skids
        g.box((-X + .02, y0, 0), (X - .02, y0 + .10, .05), CRATE, wuv((-X, y0, 0), (X, y0 + .1, .05)))
    slats = ((.11, .27), (.30, .46), (.49, .65))
    for sy in (-1, 1):                                                                            # long sides
        yo = sy * (Y - .02)
        for z0, z1 in slats + ((.68, .75),):
            lo, hi = (-X + .05, min(yo, yo - sy * .035), z0), (X - .05, max(yo, yo - sy * .035), z1)
            g.box(lo, hi, CRATE, wuv(lo, hi))
    for sx in (-1, 1):                                                                            # ends
        xo = sx * (X - .02)
        for z0, z1 in slats + ((.68, .75),):
            lo, hi = (min(xo, xo - sx * .035), -Y + .05, z0), (max(xo, xo - sx * .035), Y - .05, z1)
            g.box(lo, hi, CRATE, wuv(lo, hi))
    # iron frame: corner posts, bottom and top rails, a middle strap on the long sides, rivets
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * X, sy * Y
            g.box((min(x, x - sx * .075), min(y, y - sy * .075), .04), (max(x, x - sx * .075), max(y, y - sy * .075), H), DARK)
    for sy in (-1, 1):
        y = sy * (Y + .005)
        for z0, z1 in ((.05, .11), (H - .06, H)):
            g.box((-X, min(y, y - sy * .04), z0), (X, max(y, y - sy * .04), z1), DARK)
        g.box((-.04, min(y, y - sy * .03), .05), (.04, max(y, y - sy * .03), H), DARK)
        for x in (-X + .04, -.0, X - .04):
            for z in (.08, H - .03, .38):
                if abs(x) > .01 and abs(z - .38) < .01:
                    continue
                g.box((x - .013, min(y, y + sy * .01), z - .013), (x + .013, max(y, y + sy * .01), z + .013), STEEL)
    for sx in (-1, 1):
        x = sx * (X + .005)
        for z0, z1 in ((.05, .11), (H - .06, H)):
            g.box((min(x, x - sx * .04), -Y, z0), (max(x, x - sx * .04), Y, z1), DARK)
    # the inside: a darker floor of straw-coloured packing under the rolls
    g.box((-X + .06, -Y + .06, .09), (X - .06, Y - .06, .14), CRATE, wuv((-X, -Y, .09), (X, Y, .14)))
    return g.mesh()


def build_scrolls(z_off):
    """Seven parchment rolls standing in the crate, leaning towards +x; local to the scroll bundle (crate z - z_off)."""
    g = Geo("R402 parchment rolls")
    rng = np.random.default_rng(401)
    bases = [(-.46, -.22), (-.40, .14), (-.24, -.05), (-.16, .26), (-.06, -.28), (.02, .08), (-.32, -.32)]
    for k, (bx, by) in enumerate(bases):
        r = rng.uniform(.055, .085)
        length = rng.uniform(.85, 1.1)
        lean = math.radians(rng.uniform(22, 32))
        turn = math.radians(rng.uniform(-14, 14))
        d = Vector((math.sin(lean) * math.cos(turn), math.sin(lean) * math.sin(turn), math.cos(lean)))
        p0 = Vector((bx, by, .14 + r * .5 - z_off))
        p1 = p0 + d * length
        u0 = rng.uniform(0, .3)
        g.cyl(p0, p1, r, PAPER, 20, (u0, .02, u0 + .45, .98), PAPER, PAPER_END)
        # two string ties round the roll
        for t in (rng.uniform(.25, .4), rng.uniform(.6, .75)):
            c = p0 + d * (length * t)
            g.cyl(c - d * .02, c + d * .02, r + .006, PAPER, 20, STRING_RECT, PAPER, STRING_RECT)
    return g.mesh()


def build_banner(face, normal, top=6.4):
    """One banner on a pier face: face = the face's point (x, y) under the banner's middle, normal = the face's
    outward horizontal direction. Built in a local frame (x across as seen from the front, y out of the face, z up)."""
    g = Geo("R414 pier banner")
    n = Vector((normal[0], normal[1], 0)).normalized()
    t = (-n).cross(Vector((0, 0, 1)))          # the viewer's right, facing the face
    frame = Matrix(((t.x, n.x, 0, face[0]), (t.y, n.y, 0, face[1]), (0, 0, 1, 0), (0, 0, 0, 1)))

    def at(x, y, z):
        return frame @ Vector((x, y, z))
    nx, ny = 12, 30
    off = .10
    grid = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            u = i / nx
            maxd = BL * BRECT + BL * (1 - BRECT) * (1 - abs(2 * u - 1))
            depth = j / ny * maxd
            amp = .012 + .03 * depth / BL
            ripple = amp * math.sin(2 * math.pi * u * 3 + .6) + .006 * math.sin(2 * math.pi * depth / 1.3 + u * 4)
            row.append((((u - .5) * BW, off + ripple, top - depth), (u, 1 - depth / BL)))
        grid.append(row)
    front = [[g.bm.verts.new(at(*p)) for p, _ in row] for row in grid]
    back = [[g.bm.verts.new(at(p[0], p[1] - .006, p[2])) for p, _ in row] for row in grid]
    for j in range(ny):
        for i in range(nx):
            q = [(j, i), (j + 1, i), (j + 1, i + 1), (j, i + 1)]
            uvs = [grid[a][b][1] for a, b in q]
            vf = [front[a][b] for a, b in q]
            vb = [back[a][b] for a, b in q]
            # the front's winding decided in the world (the frame is mirrored: y points at the viewer)
            if (vf[1].co - vf[0].co).cross(vf[3].co - vf[0].co).dot(n) < 0:
                vf, vb, uvs = vf[::-1], vb[::-1], uvs[::-1]
            g.face(vf, uvs, BANNER, True)
            g.face(vb[::-1], uvs[::-1], BANNER, True)
    # rod, finials, wall brackets, rings, tassel
    rz = top + .03
    half = BW / 2 + .14
    g.cyl(at(-half, off, rz), at(half, off, rz), .026, DARK, 12)
    for s in (-1, 1):
        g.ball(at(s * (half + .035), off, rz), .045, GOLD)
        xb = s * (BW / 2 + .06)
        g.cyl(at(xb, -.02, rz), at(xb, off, rz), .018, DARK, 8)
        g.box((xb - .05, -.02, rz - .07), (xb + .05, .012, rz + .07), DARK, m=frame)
    for k in range(5):
        xr = (k / 4 - .5) * (BW - .1)
        g.cyl(at(xr - .012, off, rz), at(xr + .012, off, rz), .036, GOLD, 12)
    ty = off + .012 + .03 + .006 * math.sin(2 * math.pi * BL / 1.3 + 2)
    g.ball(at(0, ty, top - BL), .03, GOLD)
    g.cyl(at(0, ty, top - BL - .02), at(0, ty, top - BL - .2), .035, GOLD, 10, r1=.012)
    return g.mesh()


# ------------------------------------------------------------------ materials (as Unity will draw them)

def material(name):
    m = bpy.data.materials.get(name)
    if m is not None:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    if name in FLATS:
        c, sm, mt = FLATS[name]
        b.inputs["Base Color"].default_value = (*[x ** 2.2 for x in c], 1)
        b.inputs["Metallic"].default_value = mt
        b.inputs["Roughness"].default_value = 1 - sm
        return m
    base = os.path.join(OUT, MAPS[name])
    col = nt.nodes.new("ShaderNodeTexImage")
    col.image = bpy.data.images.load(base + ".png", check_existing=True)
    out = col.outputs["Color"]
    if name in TINTS:
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1
        nt.links.new(out, mix.inputs[6])
        mix.inputs[7].default_value = (*[x ** 2.2 for x in TINTS[name]], 1)
        out = mix.outputs[2]
    nt.links.new(out, b.inputs["Base Color"])
    ms = nt.nodes.new("ShaderNodeTexImage")
    ms.image = bpy.data.images.load(base + "_MS.png", check_existing=True)
    ms.image.colorspace_settings.name = "Non-Color"
    nt.links.new(ms.outputs["Color"], b.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1
    nt.links.new(ms.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs["Value"], b.inputs["Roughness"])
    return m


MAPS = {WOOD: "LibraryBookcaseWood", WOOD_DARK: "LibraryBookcaseWood", BOOKS: "LibraryBookcaseBooks",
        CRATE: "LibraryCrateWood", PAPER: "LibraryParchment", BANNER: "LibraryBanner"}


def paint_all(out):
    os.makedirs(out, exist_ok=True)
    paint_wood(os.path.join(out, "LibraryBookcaseWood"))
    paint_wood(os.path.join(out, "LibraryCrateWood"), light=(206, 156, 98), mid=(172, 120, 70), dark=(120, 78, 42), seed=9,
               grey=.35)
    paint_books(os.path.join(out, "LibraryBookcaseBooks"))
    paint_parchment(os.path.join(out, "LibraryParchment"))
    paint_banner(os.path.join(out, "LibraryBanner"))


# ------------------------------------------------------------------ applying to the environment

def pier_front(y_c, side):
    """R414: the pier's front - its face toward the start (-y), seen by everyone running the hall - as the point
    under the banner's middle (the pier's x, the face's y over the banner's span)."""
    best, xc = None, None
    for o in bpy.data.objects:
        if o.type != "MESH" or "Inner_gallery_pier" not in o.name:
            continue
        c = o.matrix_world.translation
        if abs(c.y - y_c) > .5 or c.x * side < 0:
            continue
        xc = c.x
        mw = o.matrix_world
        for v in o.data.vertices:
            w = mw @ v.co
            if 2.9 < w.z < 6.6 and abs(w.x - c.x) < BW / 2 + .3:
                best = w.y if best is None else min(best, w.y)
    if best is None:
        raise RuntimeError(f"no pier at y {y_c} side {side}")
    return xc, best


def apply_props(col):
    """Bookcases, crates and lantern posts once (Pier_banner_-1_0 marks them done); the banners every time."""
    if bpy.data.objects.get(MARK) is None:
        swap_props()
    place_banners(col)


def swap_props():
    # 4. the four lantern posts
    gone = 0
    for name in LANTERN_POSTS:
        o = bpy.data.objects.get(name)
        if o is None:
            raise RuntimeError("missing " + name)
        for c in list(o.children_recursive) + [o]:
            bpy.data.objects.remove(c, do_unlink=True)
            gone += 1
    print("lantern posts removed:", gone, "objects")
    # 1. bookcases
    case = build_bookcase()
    n = 0
    old = set()
    for o in bpy.data.objects:
        if o.type == "MESH" and o.data.name.startswith(BOOKCASE_MESH):
            old.add(o.data)
            o.data = case
            n += 1
    print("bookcases:", n, "->", len(case.polygons), "faces")
    # 3. the chest and its scrolls
    crate = build_crate()
    scrolls = build_scrolls(.96)
    nc = ns = 0
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        if o.name.startswith("Balcony_chest_") and o.data.name.startswith(CHEST_MESH):
            old.add(o.data)
            o.data = crate
            nc += 1
        elif o.name.startswith("Balcony_scrolls_") and o.data.name.startswith(SCROLL_MESH):
            old.add(o.data)
            o.data = scrolls
            ns += 1
    print("crates:", nc, len(crate.polygons), "faces; scroll bundles:", ns, len(scrolls.polygons), "faces")
    if n != 20 or nc != 2 or ns != 2:
        raise RuntimeError("unexpected counts")
    for me in old:
        if me.users == 0:
            bpy.data.meshes.remove(me)


def place_banners(col):
    """2. banners, five a side, on each inner pier's front (R414; R402 hung them on the hall-side face, behind the
    lanterns' chains). Any banners already there are replaced."""
    for o in [o for o in bpy.data.objects if o.name.startswith("Pier_banner_")]:
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if me.users == 0:
            bpy.data.meshes.remove(me)
    for side in (-1, 1):
        for i, y in enumerate(PIER_YS):
            x, fy = pier_front(y, side)
            me = build_banner((x, fy), (0, -1))
            ob = bpy.data.objects.new(f"Pier_banner_{side}_{i}", me)
            col.objects.link(ob)
            print("banner", ob.name, "front at", round(x, 3), round(fy, 3), len(me.polygons), "faces")


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    return col


def preview(out):
    import cannon_conveyor_look as look
    global OUT
    os.makedirs(out, exist_ok=True)
    OUT = out
    col = load()
    paint_all(out)
    apply_props(col)
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_backface_culling = True
        if m.name.startswith("CLAY"):
            m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.62, .55, .46, 1)
        if m.name in FLATS and m.use_nodes:
            c, sm, mt = FLATS[m.name]
            b = m.node_tree.nodes["Principled BSDF"]
            b.inputs["Base Color"].default_value = (*[x ** 2.2 for x in c], 1)
            b.inputs["Metallic"].default_value = mt
            b.inputs["Roughness"].default_value = 1 - sm

    def bounds(o):
        cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
        return Vector([min(c[i] for c in cs) for i in range(3)]), Vector([max(c[i] for c in cs) for i in range(3)])
    sun, cam = look.stage(sc)
    shots = {
        "bookcase.png": ((-9.5, 2.0, 2.3), (-14.0, 3.2, 1.9), 30, 110, lambda lo, hi: hi.z < 9 and lo.x < -11),
        "bookcase_close.png": ((-11.6, 3.9, 2.4), (-13.9, 2.6, 2.1), 30, 110, lambda lo, hi: hi.z < 9 and lo.x < -11),
        "banners_left.png": ((-2.0, -21.0, 2.2), (-9.0, -3.0, 4.6), 26, 200, lambda lo, hi: hi.z < 9.6 and hi.x < -7 and lo.z > -8.5),
        "banner_close.png": ((-7.4, -14.2, 3.0), (-9.05, -10.6, 4.9), 30, 200, lambda lo, hi: hi.z < 9.6 and hi.x < -7 and lo.z > -8.5),
        "banners_right.png": ((2.0, -21.0, 2.2), (9.0, -3.0, 4.6), 26, 160, lambda lo, hi: hi.z < 9.6 and lo.x > 7 and lo.z > -8.5),
        "crate.png": ((-9.2, -7.4, 1.9), (-11.5, -9.0, .7), 32, 120, lambda lo, hi: hi.z < 4 and lo.x < -9.5 and lo.z > -1),
        "crate_side.png": ((-11.5, -6.4, 1.5), (-11.5, -9.0, .6), 32, 160, lambda lo, hi: hi.z < 4 and lo.x < -9.5 and lo.z > -1),
        "start_no_posts.png": ((0, -4.0, 6.0), (0, -15.0, 0), 24, 200, lambda lo, hi: hi.z < 5 and lo.z > -1),
        "goal_no_posts.png": ((0, 8.0, 7.0), (0, 19.0, 1.2), 24, 20, lambda lo, hi: hi.z < 6 and lo.z > 0),
    }
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    box = {o.name: bounds(o) for o in meshes}
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
    for name, (at, target, lens, turn, keep) in shots.items():
        for o in meshes:
            o.hide_render = not keep(*box[o.name])
        look.shoot(sc, sun, cam, out, ((name, at, target, lens, turn),))


def apply():
    from export_library_fbx import SETTINGS
    col = load()
    paint_all(OUT)
    apply_props(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {FBX}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])
    else:
        apply()
