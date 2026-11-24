# Architectural parts shared by the CR04..CR07 corridor modules (west-wall frame: x 0 = the bodies' face line,
# + into the corridor, the wall face 0.25 behind it; y along the way; z up).
import math
from mathutils import Vector
import cr_kit_04_07 as K


def shield_outline(w, h, n=8):
    """A heater shield (u across, v up), top edge at v h/2, point at -h/2."""
    hw, hh = w * .5, h * .5
    pts = [(-hw, hh), (hw, hh), (hw, 0)]
    for i in range(1, n):
        t = i / n
        pts.append((hw * (1 - t) ** 1.3, -hh * math.sin(t * math.pi * .5)))
    pts.append((0, -hh))
    for i in range(n - 1, 0, -1):
        t = i / n
        pts.append((-hw * (1 - t) ** 1.3, -hh * math.sin(t * math.pi * .5)))
    pts.append((-hw, 0))
    return pts


def pawn_relief(m, axis, a0, a1, cu, cv, h, key):
    """A chess pawn in low relief (a flat silhouette): base, body, collar, head. axis = the depth direction."""
    s = h / 1.0
    base = [(cu - .3 * s, cv), (cu + .3 * s, cv), (cu + .3 * s, cv + .1 * s), (cu - .3 * s, cv + .1 * s)]
    m.prism(base, axis, a0, a1, key)
    body = [(cu - .22 * s, cv + .1 * s), (cu + .22 * s, cv + .1 * s), (cu + .1 * s, cv + .55 * s), (cu - .1 * s, cv + .55 * s)]
    m.prism(body, axis, a0, a1, key)
    m.prism([(cu - .17 * s, cv + .55 * s), (cu + .17 * s, cv + .55 * s), (cu + .17 * s, cv + .62 * s),
             (cu - .17 * s, cv + .62 * s)], axis, a0, a1, key)
    head = [(cu + .17 * s * math.cos(2 * math.pi * i / 16), cv + .78 * s + .17 * s * math.sin(2 * math.pi * i / 16))
            for i in range(16)]
    m.prism(head, axis, a0, a1, key)


def curtain(m, u, top, tie, bottom, x0, x1, key, folds=4, spread=.16, rod=True, tie_key="Brass", swag=True):
    """A tied-back curtain (it never moves, so it may be a model): `folds` round folds hanging from `top`, gathered
    at `tie`, flaring to a small pool at `bottom`, centred on y = u, between x0 and x1 (x0 nearer the wall). The
    folds stand at alternating depths so the drape reads round, and a swag of two sagging folds crowns it."""
    r = (x1 - x0) * .5
    xc = (x0 + x1) * .5
    for k in range(folds):
        f = (k - (folds - 1) * .5) / max(1, (folds - 1) * .5)      # -1 .. 1
        dx = (r * .55) * (1 if k % 2 else -1)
        x = xc + dx
        up = K.bez((x, u + f * spread, top), (x, u + f * spread * .95, top - (top - tie) * .45),
                   (xc + dx * .3, u + f * spread * .35, tie + (top - tie) * .2), (xc, u + f * spread * .22, tie), 8)
        down = K.bez((xc, u + f * spread * .22, tie), (xc + dx * .4, u + f * spread * .3, tie - (tie - bottom) * .25),
                     (x, u + f * spread * .85, bottom + (tie - bottom) * .25), (x + r * .3, u + f * spread * 1.05, bottom + .02), 8)
        rr = r * (.75 - .2 * abs(f))
        m.tube(up + down[1:], rr, key, 8)
    m.torus((xc, u, tie), spread * .25 + r * .8, .024, tie_key, 12, 6)
    m.sphere((xc + r + .02, u, tie - .06), .04, tie_key, 8, 6)          # tassel knot
    m.lathe([(0, 0), (.045, .02), (.03, .12), (.0, .16)], (xc + r + .02, u, tie - .27), tie_key, 10)
    if swag:
        for k, sag in ((0, .32), (1, .2)):
            pts = K.bez((xc + r * .4, u - spread - .05, top - .02 - k * .06), (xc + r * 1.1, u - spread * .4, top - sag * 1.6),
                        (xc + r * 1.1, u + spread * .4, top - sag * 1.6), (xc + r * .4, u + spread + .05, top - .02 - k * .06), 10)
            m.tube(pts, r * .55, key, 8)


def window(m, cx, w, sill, spring, x_face, x_back, wall_key, frame_key, glass_key, grid_key, frame=.1,
           keystone=True, bars=(1, 2)):
    """A round-headed window in a wall slab: the slab with its opening, an architrave ring proud of the face, a sill,
    the glass set back near x_back, a glazing grid (bars[0] verticals, bars[1] transoms below the spring) and a
    keystone. The slab itself (K.wall_with_arch) is the caller's. Returns the apex height."""
    r = w * .5
    opening = K.lift(K.arch(cx, w, spring - sill, 12), sill)
    m.prism(opening, "x", x_back + .01, x_back + .03, glass_key)
    outer = K.grow(K.arch(cx, w, spring - sill, 12), cx, spring - sill, frame)
    m.ring(K.lift(outer, sill), opening, "x", x_face - .005, x_face + .06, frame_key)
    m.box(x_back, cx - r - frame - .05, sill - .12, x_face + .1, cx + r + frame + .05, sill, frame_key, .02)
    gx = x_back + .03
    for i in range(bars[0]):
        y = cx - r + w * (i + 1) / (bars[0] + 1)
        m.box(gx, y - .025, sill, gx + .035, y + .025, spring + r * .98, grid_key, .006)
    for j in range(bars[1]):
        z = sill + (spring - sill) * (j + 1) / (bars[1] + 1)
        m.box(gx, cx - r, z - .025, gx + .035, cx + r, z + .025, grid_key, .006)
    m.box(gx, cx - r, spring - .025, gx + .035, cx + r, spring + .025, grid_key, .006)
    if keystone:
        m.prism([(cx - .09, spring + r), (cx + .09, spring + r), (cx + .12, spring + r + frame + .14),
                 (cx - .12, spring + r + frame + .14)], "x", x_face - .005, x_face + .09, frame_key)
    return spring + r


def wainscot(m, y0, y1, x_face, top, panel_key, inset_key, cap_key, skirt_key, panels=2, fillet_key=None):
    """Raised-panel wainscot on a wall face from the floor to `top`: a board, `panels` raised panels with a bolection
    rim, a cap rail and a skirting."""
    m.box(x_face, y0, .15, x_face + .04, y1, top, panel_key, 0)
    span = (y1 - y0) / panels
    for i in range(panels):
        a, b = y0 + i * span + .1, y0 + (i + 1) * span - .1
        m.ring([(a, .35), (b, .35), (b, top - .2), (a, top - .2)],
               [(a + .05, .4), (b - .05, .4), (b - .05, top - .25), (a + .05, top - .25)], "x", x_face + .04, x_face + .07,
               panel_key)
        m.box(x_face + .04, a + .05, .4, x_face + .06, b - .05, top - .25, inset_key, 0)
    m.box(x_face, y0, top, x_face + .12, y1, top + .1, cap_key, .02)
    if fillet_key:
        m.box(x_face + .1, y0, top + .02, x_face + .125, y1, top + .05, fillet_key, 0)
    m.box(x_face, y0, 0, x_face + .09, y1, .18, skirt_key, .015)


def ashlar(m, y0, y1, z0, z1, x_face, key, course=.52, unit=.92, depth=.035, gap=.022, holes=(), seed=0):
    """Raised ashlar blocks on a wall face (running bond, chamfered), courses from z0 to z1 between y0 and y1,
    standing `depth` proud of x_face. Blocks are cut short of each hole (ya, yb, za, zb) they meet."""
    import random
    rnd = random.Random(seed)
    z = z0
    row = 0
    while z < z1 - .1:
        h = min(course, z1 - z)
        shift = (unit * .5) if row % 2 else 0
        y = y0 - shift
        while y < y1 - .05:
            w = unit * rnd.uniform(.85, 1.15)
            a, b = max(y0, y) + gap * .5, min(y1, y + w) - gap * .5
            za, zb = z + gap * .5, z + h - gap * .5
            segs = [(a, b)]
            for ha, hb, hz0, hz1 in holes:
                if zb > hz0 and za < hz1:
                    nxt = []
                    for sa, sb in segs:
                        if sb <= ha or sa >= hb:
                            nxt.append((sa, sb))
                        else:
                            if sa < ha:
                                nxt.append((sa, ha - gap * .5))
                            if sb > hb:
                                nxt.append((hb + gap * .5, sb))
                    segs = nxt
            d = depth * rnd.uniform(.75, 1.2)
            for sa, sb in segs:
                if sb - sa > .1:
                    m.box(x_face, sa, za, x_face + d, sb, zb, key, min(.03, d * .8))
            y += w
        z += h
        row += 1


def stack(m, x_back, yc, half_y, x_front, levels, key):
    """A stepped moulding as stacked boxes - a pilaster's base or capital. levels: [(z0, z1, grow, key or None)]: each
    box from x_back to x_front + grow, across yc +- (half_y + grow)."""
    for z0, z1, grow, k in levels:
        m.box(x_back, yc - half_y - grow, z0, x_front + grow, yc + half_y + grow, z1, k or key, min(.025, (z1 - z0) * .3))


def cornice(m, y0, y1, x_face, z, levels, key):
    """A stepped cornice along a wall: levels [(height, projection, key or None)] from z upward."""
    for h, proj, k in levels:
        m.box(x_face, y0, z, x_face + proj, y1, z + h, k or key, min(.02, h * .3))
        z += h
    return z


def voussoirs(m, cx, w, sill, spring, ring, x0, x1, key, key_stone=None, n=9, quoins=True):
    """The dressed stone round a round-headed opening: wedge voussoirs round the head (ring wide, x0..x1 deep), a
    bigger keystone, and alternating long/short quoins down the jambs."""
    r = w * .5
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        g = .012
        a0g, a1g = a0 + g / r, a1 - g / r
        pts = [(cx + r * math.cos(a0g), spring + r * math.sin(a0g)), (cx + (r + ring) * math.cos(a0g), spring + (r + ring) * math.sin(a0g)),
               (cx + (r + ring) * math.cos(a1g), spring + (r + ring) * math.sin(a1g)), (cx + r * math.cos(a1g), spring + r * math.sin(a1g))]
        if i == n // 2:
            pts = [(cx + r * math.cos(a0g), spring + r * math.sin(a0g)), (cx + (r + ring * 1.35) * math.cos(a0g - .05), spring + (r + ring * 1.35) * math.sin(a0g - .05)),
                   (cx + (r + ring * 1.35) * math.cos(a1g + .05), spring + (r + ring * 1.35) * math.sin(a1g + .05)), (cx + r * math.cos(a1g), spring + r * math.sin(a1g))]
            m.prism(pts, "x", x0, x1 + .04, key_stone or key)
        else:
            m.prism(pts, "x", x0, x1, key)
    if quoins:
        z = sill
        k = 0
        while z < spring - .05:
            h = min(.38, spring - z)
            wq = ring * (1.0 if k % 2 == 0 else .62)
            for s in (-1, 1):
                ya, yb = sorted((cx + s * r, cx + s * (r + wq)))
                m.box(x0, ya + .006, z + .006, x1, yb - .006, z + h - .006, key, .015)
            z += h
            k += 1
