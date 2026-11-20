# Royal Observatory material library and floor-motif atlas (R0, seoungtest R725, 2026-11-19; PLAN.md R0, 1-2).
#
#   blender -b --factory-startup --python ro_textures.py -- [out folder] [name,name...] [size]
#
# Into textures/library (+ ro_textures.json):
#   RO_<Name>_Albedo.png (sRGB colour, alpha = smoothness) and _Normal.png (OpenGL +Y), one seamless tile a material,
#   the slot names of ro_kit.SLOTS. Source: the Codex swatch sheet
#   Docs/PawnRush/ConceptArt/R725_RoyalObservatoryConcept/AssetRefs/RO_T3_material_swatches.png (4 x 2 swatches on
#   white, PROMPT_TEXTURES.md), each swatch cropped and made to tile the way the library's rl_textures.py does (Moisan
#   periodic split + edge blend, broad light drift flattened). Until that sheet exists, a procedural stand-in of the
#   same colour (rcy_textures' marble / metal / velvet / slate) so the pipeline runs - "source" in the json says which.
#   RO_Motif_Albedo / _Normal / _MetallicSmoothness.png: the floor-motif atlas, ATLAS_COLS x ATLAS_ROWS cells of CELL
#   px (ro_kit: cells 0-11 square walkway panels, 12-17 round star-platform tops, 18 the observing stage's sun, 19 plain
#   enamel). Source: RO_T1_floor_motifs.png (4 x 3 squares) and RO_T2_round_motifs.png (3 x 2 discs + one wide disc),
#   cut at their white gutters; until they exist (or for a cell they lack) the cell is drawn here from distance fields -
#   the same twelve + seven motifs the sheets were asked for. The gold is found by its colour: it alone is metallic and
#   smooth in the third map (URP Lit metallic map: R metallic, A smoothness), and stands a little proud in the normal.
# Tile = metres one tile covers on the models' metre UVs (Unity tiling = 1 / tile). The atlas is not tiled.

import json, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels"))
import rcy_textures as rt   # noqa: E402  (helpers only)

REFS = os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R725_RoyalObservatoryConcept", "AssetRefs")
T1 = os.path.join(REFS, "RO_T1_floor_motifs.png")
T2 = os.path.join(REFS, "RO_T2_round_motifs.png")
T3 = os.path.join(REFS, "RO_T3_material_swatches.png")
N = 1024
rt.N = N

# name: (T3 swatch index (row-major, 0..7) or None, target colour (sRGB) for the stand-in and the pull, tile m, metallic,
#        normal strength, smoothness, smoothness variation)
LIBRARY = {
    "RO_Marble": (0, "#E9DFD0", 3.0, 0.0, .8, .78, .05),
    "RO_Gold": (1, "#E2AE48", .6, 1.0, .5, .80, .06),   # 0.6 m: the filigree at the concept's scale on a 0.5 m band
    "RO_Brass": (2, "#A87A3C", 1.0, 1.0, .9, .62, .08),
    "RO_NavyEnamel": (3, "#1B2552", 1.5, 0.0, .3, .6, .03),     # smoothness .86 -> .6 (11-19: a niche's vault mirrored grey)
    "RO_VioletEnamel": (4, "#3E2768", 1.5, 0.0, .3, .6, .03),
    "RO_Pearl": (5, "#EDE6D8", 2.0, 0.0, .4, .84, .03),
    "RO_HallStone": (6, "#2A2C38", 3.0, 0.0, 1.2, .55, .08),
    "RO_Velvet": (7, "#4A2378", 1.0, 0.0, 1.0, .18, .06),
}
PULL = .6
# The navy, violet and hall-stone swatches came with a big compass star and gold lines on them (RO_T3, 11-19): tiled, that
# one star would repeat on every metre of floor - what the user asked not to have. Those three take only their ground
# colour from the sheet (the median of the swatch's non-gold pixels) and draw the body here (enamel clouds and flecks,
# slate); the motifs belong to the atlas.
MEASURED = {"RO_NavyEnamel", "RO_VioletEnamel", "RO_HallStone"}


def standin(name):
    """A procedural tile of the swatch's colour while RO_T3 is not there: (colour, smoothness, height)."""
    tgt = rt.hexc(LIBRARY[name][1])
    s = sum(map(ord, name))
    if name in ("RO_Marble", "RO_Pearl"):
        col, sm, h = rt.marble(s)
        if name == "RO_Pearl":
            col = rt.mix(col, col.mean(), .7)
    elif name in ("RO_Gold", "RO_Brass"):
        col, sm, h = rt.metal(s, [(0, "#7A5426"), (.5, "#A07234"), (1, "#C29450")], dents=40 if name == "RO_Gold" else 140,
                              scratch=.2 if name == "RO_Gold" else .6)
    elif name == "RO_Velvet":
        col, sm, h = rt.velvet(s, [(0, "#2C1048"), (.5, "#46226E"), (1, "#5C3488")])
    elif name == "RO_HallStone":
        col, sm, h = rt.slate(s)
    else:   # enamel: a deep glassy ground, soft clouds, gold flecks
        col, sm, h = enamel(s, tgt)
        return col, sm, h
    col = col * (tgt / np.clip(col.reshape(-1, 3).mean(0), 1e-3, None))[None, None, :]
    return np.clip(col, 0, 1), sm, h


def enamel(seed, tgt, n=N, flecks=True):
    cloud = rt.fbm(seed, 1, 8, 1.2, n=n)
    mid = rt.fbm(seed + 1, 8, 60, .9, n=n)
    col = tgt[None, None, :] * (1 + .045 * np.clip(cloud, -2.5, 2.5) + .025 * mid)[..., None]
    col = rt.mix(col, np.clip(tgt * 1.35 + .02, 0, 1), .07 * rt.smooth(rt.s01(cloud, .6, 2.2)))
    sm = np.full((n, n), .86, np.float32)
    h = .2 * mid
    if flecks:
        d1, _ = rt.worley(seed + 2, int(n * n / 2600), n, jitter=1.0)
        f = rt.smooth(1 - d1 / 1.6) * (rt.fbm(seed + 3, 4, 30, .5, n=n) > .9)
        col = rt.mix(col, rt.hexc("#E8BE5A"), np.clip(f, 0, 1))
    return np.clip(col, 0, 1).astype(np.float32), sm, h


def swatches():
    """The eight T3 swatches as arrays (None if the sheet is missing): cut at the white gutters, 6 % inset."""
    if not os.path.exists(T3):
        return None
    img = rt.load_png(T3)
    boxes = cells_on_white(img, 2, 4)
    out = []
    for k, (y0, y1, x0, x1) in enumerate(boxes):
        inset = .10 if k == 1 else .06   # the gold swatch has a framed edge: cut past it
        iy, ix = int((y1 - y0) * inset), int((x1 - x0) * inset)
        out.append(img[y0 + iy:y1 - iy, x0 + ix:x1 - ix])
    return out


def cells_on_white(img, rows, cols, white=.90):
    """Bounding boxes (y0, y1, x0, x1) of the rows x cols non-white blocks of a sheet, row-major."""
    ink = (img.min(-1) < white)
    def runs(profile, want):
        on = profile > profile.max() * .25
        r, start = [], None
        for i, v in enumerate(list(on) + [False]):
            if v and start is None:
                start = i
            if not v and start is not None:
                r.append((start, i)); start = None
        r.sort(key=lambda a: a[1] - a[0], reverse=True)
        return sorted(r[:want])
    ys = runs(ink.sum(1), rows)
    boxes = []
    for (y0, y1) in ys:
        xs = runs(ink[y0:y1].sum(0), cols)
        for (x0, x1) in xs:
            boxes.append((y0, y1, x0, x1))
    return boxes


# The concept's darker, duller look (user 11-19 on ro-shell's niche stills: "컨셉 아트 텍스쳐처럼 좀 더 어둡고 침침해야
# 해" -> "살짝만 밝게 해줘" -> "이대로 넘겨"; the numbers are ro_r6_hall_model.py's GRADE the user saw): applied to the
# albedo as Blender's Hue/Saturation/Value node then a multiply would be on the decoded (linear) colour - hue shift,
# saturation and value factors, then a colour to multiply by.
GRADE = {
    "RO_NavyEnamel": (.05, .48, .47, (.95, .9, 1.1)),
    "RO_Marble": (0.0, 1.5, .85, (1.0, .92, .8)),
    "RO_Gold": (0.0, 1.05, .8, (1.0, .92, .76)),
    "RO_VioletEnamel": (0.0, .75, .68, (1.0, 1.0, 1.0)),
    # the banners' velvet brighter and clearer (user 11-19 on ro-shell's banner stills: "더 선명하게" -> "확인")
    "RO_Velvet": (.01, 2.2, 1.3, (1.1, .85, 1.12)),
}


def to_linear(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= .0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - .055)


def grade(col, hue, sat, val, mul):
    """Blender's Hue/Saturation/Value node (Hue .5 + hue) and a multiply, on the linear colour of sRGB numbers."""
    lin = to_linear(col.astype(np.float64))
    r, g, b = lin[..., 0], lin[..., 1], lin[..., 2]
    mx, mn = lin.max(-1), lin.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    nz = d > 1e-9
    rm = nz & (mx == r)
    gm = nz & (mx == g) & ~rm
    bm = nz & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6
    h[gm] = (b - r)[gm] / d[gm] + 2
    h[bm] = (r - g)[bm] / d[bm] + 4
    h = (h / 6 + hue) % 1.0
    s_ = np.where(mx > 1e-9, d / np.maximum(mx, 1e-9), 0) * sat
    s_ = np.clip(s_, 0, 1)
    v = mx * val
    i = np.floor(h * 6).astype(int) % 6
    f = h * 6 - np.floor(h * 6)
    p_, q, t = v * (1 - s_), v * (1 - f * s_), v * (1 - (1 - f) * s_)
    out = np.zeros_like(lin)
    for k_, (a, b_, c) in enumerate(((v, t, p_), (q, v, p_), (p_, v, t), (p_, q, v), (t, p_, v), (v, p_, q))):
        m = i == k_
        out[..., 0][m], out[..., 1][m], out[..., 2][m] = a[m], b_[m], c[m]
    out *= np.array(mul)[None, None, :]
    return to_srgb(out).astype(np.float32)


def hue_of(col):
    """Hue 0..1 of sRGB numbers (as Blender's HSV)."""
    mx, mn = col.max(-1), col.min(-1)
    d = np.maximum(mx - mn, 1e-6)
    r, g, b = col[..., 0], col[..., 1], col[..., 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4))
    return (h / 6) % 1.0


def build_material(name, out, size, sheet):
    idx, target, tile, metal, strength, smooth0, smooth_var = LIBRARY[name]
    measured = None
    if name in MEASURED and sheet is not None and idx is not None and idx < len(sheet):
        px = sheet[idx].reshape(-1, 3)
        mx, mn = px.max(-1), px.min(-1)
        goldish = ((mx - mn) / np.clip(mx, 1e-3, None) > .35) & (px[:, 0] > px[:, 2] * 1.2)
        measured = np.median(px[~goldish], 0)
        target = "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in measured)
        LIBRARY[name] = (idx, target) + LIBRARY[name][2:]
    if measured is None and sheet is not None and idx is not None and idx < len(sheet):
        src = rt.resample(sheet[idx], size)
        col = rt.edge_blend(np.stack([rt.periodic(src[..., c]) for c in range(3)], -1))
        lum = col @ np.array([.2126, .7152, .0722], np.float32)
        broad = rt.blur(lum, size / 6.0)
        col *= (lum.mean() / np.clip(broad, 1e-3, None))[..., None] ** .7
        if name == "RO_Brass":   # the brushed swatch's light falls off across the brushing: level each column and row
            lum = col @ np.array([.2126, .7152, .0722], np.float32)
            for ax in (0, 1):
                m = rt.blur_any(lum.mean(ax, keepdims=True).repeat(size, ax), 6.0) if hasattr(rt, "blur_any") else lum.mean(ax, keepdims=True)
                col *= (lum.mean() / np.clip(m, 1e-3, None))[..., None]
                lum = col @ np.array([.2126, .7152, .0722], np.float32)
        tgt = rt.hexc(target)
        col *= (1 + PULL * (tgt / np.clip(col.reshape(-1, 3).mean(0), 1e-3, None) - 1))[None, None, :]
        col = np.clip(col, 0, 1)
        lum = col @ np.array([.2126, .7152, .0722], np.float32)
        h = (lum - rt.blur(lum, 12.0)) * 8 + (rt.blur(lum, 3.0) - rt.blur(lum, 24.0)) * 6
        detail = np.clip(.5 + (lum - rt.blur(lum, 8.0)) * 4, 0, 1)
        sm = np.clip(smooth0 + smooth_var * (detail - .5) * 2, 0, 1)
        source = "RO_T3 swatch %d" % (idx + 1)
    else:
        col, sm, h = standin(name)
        sm = np.clip(smooth0 + smooth_var * rt.s01(np.asarray(sm) - np.mean(sm), -.1, .1) * 2 - smooth_var, 0, 1) \
            if np.ndim(sm) else np.full(col.shape[:2], smooth0, np.float32)
        source = ("RO_T3 swatch %d colour, procedural body" % (idx + 1)) if measured is not None else "procedural stand-in (no RO_T3 yet)"
    if name in GRADE:
        col = grade(col, *GRADE[name])
        source += ", graded (GRADE)"
    alb = np.concatenate([col, sm[..., None]], -1).astype(np.float32)
    rt.save_png(alb, os.path.join(out, name + "_Albedo.png"))
    rt.save_png(rt.normal_map(h, strength), os.path.join(out, name + "_Normal.png"))
    rgb = col.reshape(-1, 3).mean(0)
    return {"tile": tile, "metallic": metal, "smoothness": float(sm.mean()), "size": size,
            "mean": "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in rgb), "target": target, "source": source}


# ================================================================== the motif atlas

ATLAS_COLS, ATLAS_ROWS, CELL = 4, 5, 1024
NAVY, VIOLET = rt.hexc("#1A2452"), rt.hexc("#3A2468")
GOLD_LO, GOLD_HI = rt.hexc("#B07A22"), rt.hexc("#F4D27A")


class Canvas:
    """One cell drawn with signed distance fields in x, y = -1..1 (y up). gold: coverage 0..1; h: the gold's height."""

    def __init__(self, n=CELL, seed=0, ground=NAVY):
        self.n = n
        c = (np.arange(n) + .5) / n * 2 - 1
        self.x, self.y = np.meshgrid(c, -c)
        self.px = 2.0 / n
        self.gold = np.zeros((n, n), np.float32)
        self.h = np.zeros((n, n), np.float32)
        self.cut = np.zeros((n, n), np.float32)   # engraved lines in the gold (darker, lower)
        self.seed = seed
        self.ground = ground

    def cover(self, d):
        return np.clip(.5 - d / self.px, 0, 1)

    def add(self, d, raise_=1.0, bevel=.012):
        """Union a shape (signed distance d, negative inside) into the gold, raised with a rounded edge."""
        cov = self.cover(d)
        self.gold = np.maximum(self.gold, cov)
        self.h = np.maximum(self.h, raise_ * np.clip(-d / bevel, 0, 1) ** .6 * cov)

    def pyramid(self, d, scale):
        """A faceted (pyramid) raised shape: height grows to its middle (a star's ridges)."""
        cov = self.cover(d)
        self.gold = np.maximum(self.gold, cov)
        self.h = np.maximum(self.h, cov * (.6 + 1.6 * np.clip(-d / scale, 0, 1)))

    def engrave(self, d):
        self.cut = np.maximum(self.cut, self.cover(d))

    # ---- distance fields
    def circle(self, cx, cy, r):
        return np.hypot(self.x - cx, self.y - cy) - r

    def ring(self, r, w, cx=0.0, cy=0.0):
        return np.abs(np.hypot(self.x - cx, self.y - cy) - r) - w * .5

    def seg(self, a, b, w):
        ax, ay = a; bx, by = b
        px, py = self.x - ax, self.y - ay
        dx, dy = bx - ax, by - ay
        t = np.clip((px * dx + py * dy) / max(dx * dx + dy * dy, 1e-9), 0, 1)
        return np.hypot(px - t * dx, py - t * dy) - w * .5

    def poly(self, pts):
        """Signed distance to a closed polygon (any shape)."""
        x, y = self.x, self.y
        d = (x - pts[0][0]) ** 2 + (y - pts[0][1]) ** 2
        s = np.ones_like(x)
        n = len(pts)
        for i in range(n):
            (ax, ay), (bx, by) = pts[i - 1], pts[i]
            ex, ey = ax - bx, ay - by
            wx, wy = x - bx, y - by
            t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
            d = np.minimum(d, (wx - ex * t) ** 2 + (wy - ey * t) ** 2)
            c1 = y >= by
            c2 = y < ay
            c3 = ex * wy > ey * wx
            flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
            s = np.where(flip, -s, s)
        return s * np.sqrt(d)

    def star(self, cx, cy, ro, ri, points=5, turn=90.0):
        pts = []
        for i in range(points * 2):
            r = ro if i % 2 == 0 else ri
            t = math.radians(turn + 180.0 * i / points)
            pts.append((cx + r * math.cos(t), cy + r * math.sin(t)))
        return self.poly(pts)

    def sparkle(self, cx, cy, r, points=4, faceted=True):
        d = self.star(cx, cy, r, r * (.28 if points == 4 else .42), points, 90.0 if points != 4 else 90.0)
        if faceted:
            self.pyramid(d, r * .5)
        else:
            self.add(d)

    # ---- the result
    def render(self, square=True, round_=False):
        n = self.n
        s = self.seed
        col, sm, h0 = enamel(s, self.ground, n=n, flecks=True)
        gold_tone = rt.s01(rt.fbm(s + 7, 2, 24, 1.0, n=n), -2, 2)
        gold = rt.mix(GOLD_LO, GOLD_HI, .35 + .5 * gold_tone)
        # facets lit a little from the upper left so the raised gold reads even before Unity's light
        hx = (np.roll(self.h, -1, 1) - np.roll(self.h, 1, 1)) * n * .02
        hy = (np.roll(self.h, 1, 0) - np.roll(self.h, -1, 0)) * n * .02
        shade = np.clip(1 + .25 * (-hx + hy), .7, 1.25)
        gold = gold * shade[..., None]
        gold = rt.mix(gold, GOLD_LO * .55, self.cut * .85)
        g = np.clip(self.gold, 0, 1)
        col = rt.mix(col, np.clip(gold, 0, 1), g)
        # a thin dark line where the inlay meets the enamel (the cloisonne wall's shadow)
        edge = np.clip(rt.blur(g, 1.2) - g, 0, 1) * 1.5
        col = col * (1 - .5 * np.clip(edge, 0, 1))[..., None]
        height = 1.2 * self.h - .5 * self.cut * g + .15 * h0 * (1 - g)
        metal = g
        smooth = np.clip(.86 * (1 - g) + .78 * g - .15 * self.cut, 0, 1)
        return np.clip(col, 0, 1).astype(np.float32), metal.astype(np.float32), smooth.astype(np.float32), height


def frame_square(c):
    """Every square panel's border: a gold line, a finer one inside, small four-point stars in the corners."""
    d = np.maximum(np.abs(c.x), np.abs(c.y))
    c.add(np.abs(d - .955) - .018, .8)
    c.add(np.abs(d - .905) - .006, .5)
    for sx in (-1, 1):
        for sy in (-1, 1):
            c.sparkle(sx * .93, sy * .93, .075, 4)


def rng(seed):
    return np.random.default_rng(seed)


def scatter(c, seed, count, rmin, rmax, keep=lambda x, y: True, box=.86):
    r = rng(seed)
    placed = []
    tries = 0
    while len(placed) < count and tries < count * 40:
        tries += 1
        x, y = r.uniform(-box, box, 2)
        size = rmin + (rmax - rmin) * r.random() ** 2.5
        if not keep(x, y) or any(math.hypot(x - a, y - b) < (size + s) * 1.6 for a, b, s in placed):
            continue
        placed.append((x, y, size))
        pts = 4 if r.random() < .55 else 5
        if size < .018:
            c.add(c.circle(x, y, size * .7), .4)
        else:
            c.sparkle(x, y, size, pts)


def compass(c, r, cx=0.0, cy=0.0, w=1.0):
    for k in range(16):
        t = math.radians(90 + k * 22.5)
        length = r * (1.0 if k % 4 == 0 else .72 if k % 2 == 0 else .48)
        half = r * (.16 if k % 4 == 0 else .12 if k % 2 == 0 else .08) * w
        tip = (cx + length * math.cos(t), cy + length * math.sin(t))
        l = (cx + half * math.cos(t + math.pi / 2), cy + half * math.sin(t + math.pi / 2))
        rr = (cx + half * math.cos(t - math.pi / 2), cy + half * math.sin(t - math.pi / 2))
        d = c.poly([tip, l, (cx, cy), rr])
        c.pyramid(d, half * .9)
        c.engrave(c.seg((cx, cy), tip, .004))


def sun(c, r_disc, r_long, r_short, rays=12, wavy=False):
    c.pyramid(c.circle(0, 0, r_disc), r_disc * .8)
    c.engrave(c.ring(r_disc * .72, .006))
    for k in range(rays * 2):
        t = math.radians(90 + k * 180.0 / rays)
        length = r_long if k % 2 == 0 else r_short
        half = .07 if k % 2 == 0 else .045
        a = (r_disc * .95 * math.cos(t - half), r_disc * .95 * math.sin(t - half))
        b = (r_disc * .95 * math.cos(t + half), r_disc * .95 * math.sin(t + half))
        tip = (length * math.cos(t), length * math.sin(t))
        c.pyramid(c.poly([a, tip, b]), .03)


def constellation(c, pts, links, star=.055):
    for i, j in links:
        c.add(c.seg(pts[i], pts[j], .012), .5)
    for k, (x, y) in enumerate(pts):
        c.sparkle(x, y, star * (1.25 if k % 3 == 0 else 1.0), 4 if k % 2 else 5)
        c.add(c.ring(star * .55, .006, x, y), .4)


def crescent(c, cx, cy, r, phase):
    """A moon disc lit `phase` (-1 new .. 1 full) as gold: the disc minus a shifted disc."""
    d = c.circle(cx, cy, r)
    if phase < .98:
        off = (1 - abs(phase)) * 2 * r * (1 if phase >= 0 else -1)
        d = np.maximum(d, -c.circle(cx + off * .9, cy, r * 1.02))
    c.add(d, .9)
    c.add(c.ring(r, .006, cx, cy), .3)


def crown(c, cx, cy, s):
    pts = [(-1, -.55), (1, -.55), (1.05, .15), (.62, -.12), (.5, .45), (.2, -.05), (0, .62), (-.2, -.05), (-.5, .45),
           (-.62, -.12), (-1.05, .15)]
    c.pyramid(c.poly([(cx + x * s, cy + y * s) for x, y in pts]), s * .25)
    c.add(c.poly([(cx - 1.05 * s, cy - .58 * s), (cx + 1.05 * s, cy - .58 * s), (cx + 1.05 * s, cy - .8 * s),
                  (cx - 1.05 * s, cy - .8 * s)]), 1.0)
    for x, y in ((-1.05, .15), (-.5, .45), (0, .62), (.5, .45), (1.05, .15)):
        c.add(c.circle(cx + x * s, cy + (y + .1) * s, .09 * s), 1.2)
    c.engrave(c.seg((cx - s, cy - .69 * s), (cx + s, cy - .69 * s), .006))


def square_motif(i, seed):
    c = Canvas(seed=seed, ground=NAVY if i % 2 == 0 else VIOLET)
    if i == 0:   # sixteen-point compass rose
        c.add(c.ring(.80, .012), .6); c.add(c.ring(.86, .006), .4)
        for k in range(64):
            t = math.radians(k * 360 / 64)
            l = .045 if k % 4 == 0 else .025
            c.add(c.seg((.80 * math.cos(t), .80 * math.sin(t)), ((.80 - l) * math.cos(t), (.80 - l) * math.sin(t)), .007), .5)
        compass(c, .78)
        c.add(c.circle(0, 0, .06), 1.3)
        scatter(c, seed, 10, .015, .03, lambda x, y: math.hypot(x, y) > .9)
    elif i == 1:   # twelve-ray sun
        c.add(c.ring(.84, .01), .6)
        sun(c, .24, .78, .55)
        scatter(c, seed, 18, .012, .03, lambda x, y: math.hypot(x, y) > .88)
    elif i == 2:   # the Big Dipper
        pts = [(-.72, .42), (-.42, .34), (-.16, .2), (.08, .02), (.14, -.38), (.62, -.32), (.6, .1)]
        constellation(c, pts, [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 3)])
        scatter(c, seed, 26, .012, .03, lambda x, y: min(math.hypot(x - a, y - b) for a, b in pts) > .16)
    elif i == 3:   # Cassiopeia's W
        pts = [(-.7, .22), (-.36, -.24), (0, .08), (.34, -.3), (.7, .2)]
        constellation(c, pts, [(0, 1), (1, 2), (2, 3), (3, 4)], .07)
        scatter(c, seed, 26, .012, .03, lambda x, y: min(math.hypot(x - a, y - b) for a, b in pts) > .18)
    elif i == 4:   # crescent moon among small stars
        crescent(c, -.08, .02, .46, .35)
        scatter(c, seed, 30, .012, .05, lambda x, y: math.hypot(x + .08, y - .02) > .56)
    elif i == 5:   # moon phases round a ring
        c.add(c.ring(.62, .012), .6); c.add(c.ring(.5, .006), .4)
        for k in range(8):
            t = math.radians(90 + k * 45)
            crescent(c, .62 * math.cos(t), .62 * math.sin(t), .1, math.cos(math.radians(k * 45)))
        c.sparkle(0, 0, .26, 8)
        c.sparkle(0, 0, .16, 4)
    elif i == 6:   # astrolabe ring with degree ticks
        for r, w in ((.84, .016), (.70, .010), (.40, .008)):
            c.add(c.ring(r, w), .7)
        for k in range(120):
            t = math.radians(k * 3)
            l = .07 if k % 10 == 0 else .04 if k % 5 == 0 else .025
            c.add(c.seg((.84 * math.cos(t), .84 * math.sin(t)), ((.84 - l) * math.cos(t), (.84 - l) * math.sin(t)), .006), .5)
        c.add(c.ring(.32, .008, .12, .1), .5); c.add(c.ring(.5, .008, -.05, -.18), .5)
        c.pyramid(c.poly([(0, .66), (.05, 0), (0, -.66), (-.05, 0)]), .04)
        c.add(c.circle(0, 0, .05), 1.2)
    elif i == 7:   # a scatter of stars of many sizes
        scatter(c, seed, 60, .012, .11)
    elif i == 8:   # comet with a long tail
        c.pyramid(c.circle(.42, .42, .1), .08)
        c.sparkle(.42, .42, .2, 4)
        for k in range(7):
            off = (k - 3) * .035
            a = (.36 + off * .7, .36 - off * .7)
            b = (-.75 + off * 2.2, -.75 - off * 2.2)
            c.add(c.seg(a, b, .016 - abs(k - 3) * .003), .5)
        scatter(c, seed, 22, .012, .035, lambda x, y: abs(x - y) > .28)
    elif i == 9:   # concentric orbits with planets
        c.pyramid(c.circle(0, 0, .12), .1)
        sun(c, .1, .2, .15)
        r = rng(seed)
        for k, rad in enumerate((.3, .48, .66, .84)):
            c.add(c.ring(rad, .008), .5)
            t = r.uniform(0, 2 * math.pi)
            c.pyramid(c.circle(rad * math.cos(t), rad * math.sin(t), .035 + .012 * k), .03)
            if k == 2:
                c.add(c.ring(.07, .008, rad * math.cos(t), rad * math.sin(t)), .5)
    elif i == 10:  # eight-point star in a diamond lattice
        for k in range(-4, 5):
            o = k * .4
            c.add(np.abs((c.x + c.y) - o) / math.sqrt(2) - .004, .4)
            c.add(np.abs((c.x - c.y) - o) / math.sqrt(2) - .004, .4)
        for gx in np.arange(-.8, .81, .4):
            for gy in np.arange(-.8, .81, .4):
                if (round(gx / .4) + round(gy / .4)) % 2 == 0 and not (abs(gx) < .1 and abs(gy) < .1):
                    c.add(c.circle(gx, gy, .022), .8)
        c.sparkle(0, 0, .42, 8)
        c.add(c.ring(.2, .01), .6)
    elif i == 11:  # a crown in a ring of stars
        crown(c, 0, -.02, .34)
        for k in range(12):
            t = math.radians(90 + k * 30)
            c.sparkle(.66 * math.cos(t), .66 * math.sin(t), .06, 5)
        c.add(c.ring(.78, .01), .6); c.add(c.ring(.54, .006), .4)
    frame_square(c)
    return c


def round_motif(i, seed):
    """Round star-platform tops: a large faceted five-point star in every middle, a different surround each."""
    c = Canvas(seed=seed, ground=NAVY if i % 2 == 0 else VIOLET)
    if i == 0:     # ring of small stars
        for k in range(16):
            t = math.radians(k * 22.5)
            c.sparkle(.76 * math.cos(t), .76 * math.sin(t), .05 if k % 2 else .07, 5 if k % 2 else 4)
        scatter(c, seed, 24, .01, .025, lambda x, y: .6 < math.hypot(x, y) < .68 or .82 < math.hypot(x, y) < .88, .9)
    elif i == 1:   # compass-rose ring
        c.add(c.ring(.64, .01), .6)
        for k in range(32):
            t = math.radians(k * 11.25)
            long_ = .92 if k % 4 == 0 else .82 if k % 2 == 0 else .74
            half = .05 if k % 4 == 0 else .03
            a = (.66 * math.cos(t - half), .66 * math.sin(t - half))
            b = (.66 * math.cos(t + half), .66 * math.sin(t + half))
            c.pyramid(c.poly([a, (long_ * math.cos(t), long_ * math.sin(t)), b]), .02)
    elif i == 2:   # constellation lines radiating
        r = rng(seed)
        for k in range(10):
            t = math.radians(k * 36 + r.uniform(-8, 8))
            r1 = r.uniform(.78, .9)
            p0 = (.62 * math.cos(t), .62 * math.sin(t))
            p1 = (r1 * math.cos(t + .12), r1 * math.sin(t + .12))
            c.add(c.seg(p0, p1, .01), .5)
            c.sparkle(*p1, .04, 4)
            c.add(c.circle(*p0, .02), .8)
    elif i == 3:   # band of moon phases
        c.add(c.ring(.66, .01), .6); c.add(c.ring(.9, .01), .6)
        for k in range(12):
            t = math.radians(90 + k * 30)
            crescent(c, .78 * math.cos(t), .78 * math.sin(t), .07, math.cos(math.radians(k * 30)))
    elif i == 4:   # engraved orbit rings
        for k, rad in enumerate((.64, .73, .82, .9)):
            c.add(c.ring(rad, .007), .5)
            t = (k * 1.7 + seed % 7)
            c.pyramid(c.circle(rad * math.cos(t), rad * math.sin(t), .03), .025)
    elif i == 5:   # a twelve-ray sunburst behind the star
        for k in range(24):
            t = math.radians(90 + k * 15)
            long_ = .94 if k % 2 == 0 else .78
            half = .09 if k % 2 == 0 else .06
            a = (.2 * math.cos(t - half), .2 * math.sin(t - half))
            b = (.2 * math.cos(t + half), .2 * math.sin(t + half))
            c.pyramid(c.poly([a, (long_ * math.cos(t), long_ * math.sin(t)), b]), .025)
    # the star itself and its setting
    c.add(c.ring(.6, .012), .6)
    c.pyramid(c.star(0, 0, .55, .22, 5, 90.0), .2)
    c.engrave(c.ring(.06, .004))
    # the rim: a bead ring and an outer line
    c.add(c.ring(.975, .02), .8)
    for k in range(72):
        t = math.radians(k * 5)
        c.add(c.circle(.945 * math.cos(t), .945 * math.sin(t), .011), 1.0)
    return c


def stage_motif(seed):
    """The observing stage: a twelve-ray sun, rays gold and navy by turns, an inner ring of stars."""
    c = Canvas(seed=seed, ground=NAVY)
    for k in range(12):
        t = math.radians(k * 30)
        a = (.42 * math.cos(t - .13), .42 * math.sin(t - .13))
        b = (.42 * math.cos(t + .13), .42 * math.sin(t + .13))
        c.pyramid(c.poly([a, (.92 * math.cos(t), .92 * math.sin(t)), b]), .04)
    for k in range(12):
        t = math.radians(15 + k * 30)
        c.add(c.seg((.45 * math.cos(t), .45 * math.sin(t)), (.86 * math.cos(t), .86 * math.sin(t)), .006), .4)
    for k in range(16):
        t = math.radians(k * 22.5)
        c.sparkle(.32 * math.cos(t), .32 * math.sin(t), .03, 5)
    sun(c, .18, .26, .22, 12)
    c.add(c.ring(.4, .01), .6)
    c.add(c.ring(.96, .02), .8)
    for k in range(96):
        t = math.radians(k * 3.75)
        c.add(c.circle(.93 * math.cos(t), .93 * math.sin(t), .009), 1.0)
    return c


def from_sheet(cell_img):
    """A Codex cell -> (colour, metallic, smoothness, height), the gold found by its colour."""
    col = rt.resample(cell_img, CELL)
    mx, mn = col.max(-1), col.min(-1)
    sat = (mx - mn) / np.clip(mx, 1e-3, None)
    hue_ok = (col[..., 0] > col[..., 2] * 1.25) & (col[..., 0] >= col[..., 1] * .9)
    gold = np.clip((sat - .3) / .2, 0, 1) * np.clip((mx - .4) / .15, 0, 1) * hue_ok
    gold = rt.blur(gold.astype(np.float32), .8)
    lum = col @ np.array([.2126, .7152, .0722], np.float32)
    height = 1.2 * rt.blur(gold, 1.5) + .4 * (lum - rt.blur(lum, 6.0)) * gold
    smooth = .86 * (1 - gold) + .78 * gold
    return col.astype(np.float32), gold.astype(np.float32), smooth.astype(np.float32), height


def round_fill(col, gold, ground):
    """A round cell from a sheet: outside the disc becomes the disc's own edge colour (the corners are never seen
    on a round top, but mipmaps reach them)."""
    n = col.shape[0]
    c = (np.arange(n) + .5) / n * 2 - 1
    x, y = np.meshgrid(c, -c)
    out = np.hypot(x, y) > .985
    col = col.copy()
    col[out] = ground
    gold = gold * ~out
    return col, gold


def build_atlas(out):
    cells = []
    sources = []
    s1 = s2 = None
    if os.path.exists(T1):
        img = rt.load_png(T1)
        s1 = [img[y0:y1, x0:x1] for (y0, y1, x0, x1) in cells_on_white(img, 3, 4)]
    if os.path.exists(T2):
        img = rt.load_png(T2)
        boxes = cells_on_white(img, 3, 3)   # two rows of three, the wide disc alone below
        s2 = [img[y0:y1, x0:x1] for (y0, y1, x0, x1) in boxes]
    for i in range(ATLAS_COLS * ATLAS_ROWS):
        seed = 7000 + i * 13
        if i < 12 and s1 and len(s1) > i:
            got = from_sheet(s1[i]); sources.append("RO_T1 %d" % (i + 1))
        elif 12 <= i < 18 and s2 and len(s2) > i - 12:
            col, g, sm, h = from_sheet(s2[i - 12])
            col, g = round_fill(col, g, NAVY if i % 2 == 0 else VIOLET)
            got = (col, g, sm, h); sources.append("RO_T2 %d" % (i - 11))
        elif i == 18 and s2 and len(s2) > 6:
            col, g, sm, h = from_sheet(s2[6])
            got = round_fill(col, g, NAVY) + (sm, h); sources.append("RO_T2 7")
        else:
            c = square_motif(i, seed) if i < 12 else round_motif(i - 12, seed) if i < 18 else stage_motif(seed) \
                if i == 18 else Canvas(seed=seed, ground=NAVY)
            got = c.render(); sources.append("drawn")
        cells.append(got)
    H, W = ATLAS_ROWS * CELL, ATLAS_COLS * CELL
    alb = np.zeros((H, W, 4), np.float32)
    ms = np.zeros((H, W, 4), np.float32)
    hgt = np.zeros((H, W), np.float32)
    for i, (col, metal, smooth, height) in enumerate(cells):
        r, c = divmod(i, ATLAS_COLS)
        ys, xs = slice(r * CELL, (r + 1) * CELL), slice(c * CELL, (c + 1) * CELL)
        # the user (11-19, "1 2 번 적용"): the atlas darkened as the library was - its enamel by the navy or the violet
        # grade as the pixel's hue is bluer or purpler, its gold by the gold grade, its enamel as dull as the library's
        h = hue_of(col)
        w_violet = np.clip((h - .69) / .07, 0, 1)[..., None]
        enamel = grade(col, *GRADE["RO_NavyEnamel"]) * (1 - w_violet) + grade(col, *GRADE["RO_VioletEnamel"]) * w_violet
        g = metal[..., None]
        col = enamel * (1 - g) + grade(col, *GRADE["RO_Gold"]) * g
        smooth = .6 * (1 - metal) + .78 * metal
        alb[ys, xs, :3] = col
        alb[ys, xs, 3] = smooth
        ms[ys, xs, 0] = metal
        ms[ys, xs, 1] = metal
        ms[ys, xs, 2] = metal
        ms[ys, xs, 3] = smooth
        hgt[ys, xs] = height
    rt.save_png(alb, os.path.join(out, "RO_Motif_Albedo.png"))
    rt.save_png(ms, os.path.join(out, "RO_Motif_MetallicSmoothness.png"))
    rt.save_png(rt.normal_map(rt.blur_any(hgt, 1.2), 1.1), os.path.join(out, "RO_Motif_Normal.png"))
    # a contact sheet for checking by eye, quarter size
    small = alb[::4, ::4, :3]
    rt.save_png(small, os.path.join(out, "RO_Motif_preview.png"))
    return {"atlas": [ATLAS_COLS, ATLAS_ROWS], "cell": CELL, "metallic": "map", "sources": sources, "tile": 1.0}


# ================================================================== planets (ro_kit slot "planet_<name>" -> RO_Planet<Name>)
# Codex's equirectangular surfaces (AssetRefs/Planets/RO_P1..P7, 1536 x 1024, asked to join left to right) -> 2048 x 1024
# with the left and right edges blended into each other (a sphere's UV seam), a mild normal from the pattern, alpha =
# smoothness. Not tiles: a sphere's UVs run 0..1 once round (Unity tiling 1). Shared by the bowl's orbit planets and the
# rolling planet (R2) and the great globe's planets (R3).
PLANETS = {
    "RO_PlanetJupiter": ("RO_P1_jupiter.png", .45),
    "RO_PlanetSaturn": ("RO_P2_saturn.png", .45),
    "RO_PlanetNeptune": ("RO_P3_neptune.png", .5),
    "RO_PlanetEarth": ("RO_P4_earth.png", .5),
    "RO_PlanetMars": ("RO_P5_mars.png", .3),
    "RO_PlanetVenus": ("RO_P6_venus.png", .45),
    "RO_PlanetMercury": ("RO_P7_mercury.png", .25),
}
PLANET_DIR = os.path.join(REFS, "Planets")


def resize(a, h, w):
    """Bilinear resample of an (H, W, C) array to (h, w)."""
    H, W = a.shape[:2]
    ys = (np.arange(h) + .5) * H / h - .5
    xs = (np.arange(w) + .5) * W / w - .5
    y0 = np.clip(np.floor(ys).astype(int), 0, H - 1); y1 = np.clip(y0 + 1, 0, H - 1); fy = np.clip(ys - y0, 0, 1)[:, None, None]
    x0 = np.clip(np.floor(xs).astype(int), 0, W - 1); x1 = np.clip(x0 + 1, 0, W - 1); fx = np.clip(xs - x0, 0, 1)[None, :, None]
    top = a[y0][:, x0] * (1 - fx) + a[y0][:, x1] * fx
    bot = a[y1][:, x0] * (1 - fx) + a[y1][:, x1] * fx
    return (top * (1 - fy) + bot * fy).astype(np.float32)


def build_planet(name, out):
    fname, smooth = PLANETS[name]
    src = rt.load_png(os.path.join(PLANET_DIR, fname))
    col = resize(src, 1024, 2048)
    # the seam: the outer 8 % of each side cross-faded with the other side rolled half round
    w = col.shape[1]
    band = int(w * .08)
    rolled = np.roll(col, w // 2, 1)
    d = np.minimum(np.arange(w), w - 1 - np.arange(w)).astype(np.float32)
    m = np.clip(d / band, 0, 1)
    m = (m * m * (3 - 2 * m))[None, :, None]
    col = col * m + rolled * (1 - m)
    lum = col @ np.array([.2126, .7152, .0722], np.float32)
    h = (lum - rt.blur_any(lum, 6.0)) * 4
    sm = np.full(lum.shape, smooth, np.float32)
    rt.save_png(np.concatenate([col, sm[..., None]], -1).astype(np.float32), os.path.join(out, name + "_Albedo.png"))
    rt.save_png(rt.normal_map(h, .8), os.path.join(out, name + "_Normal.png"))
    rgb = col.reshape(-1, 3).mean(0)
    return {"tile": 1.0, "metallic": 0.0, "smoothness": smooth, "size": [2048, 1024],
            "mean": "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in rgb), "target": "", "source": "AssetRefs/Planets/" + fname,
            "uv": "0..1 round the sphere (equirectangular)"}


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv and argv[0] else os.path.join(HERE, "textures", "library")
    names = argv[1].split(",") if len(argv) > 1 and argv[1] else list(LIBRARY) + ["RO_Motif"]
    size = int(argv[2]) if len(argv) > 2 else N
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, "ro_textures.json")
    info = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}
    sheet = swatches() if any(n in LIBRARY for n in names) else None
    for nm in names:
        got = (build_atlas(out) if nm == "RO_Motif" else build_planet(nm, out) if nm in PLANETS
               else build_material(nm, out, size, sheet))
        print("RO_TEX", nm, {k: v for k, v in got.items() if k != "sources"})
        info[nm] = got
    json.dump(info, open(path, "w", encoding="utf-8"), indent=1)
