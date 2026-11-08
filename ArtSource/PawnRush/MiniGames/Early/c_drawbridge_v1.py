# C · 해자 도개교 (R501): the mini-game's Blender dressing, made from the R311 concept art over the R312 section's own
# numbers. Nothing here moves a collider, a door or a rope: the section's entrance (0,0,0), exit (0,0,36), 44 m width,
# stations at x -12/+12 z 4, the moat (game u 12..16), the hinge (u 15.95), the winch (u 11.3) and the rope lane are
# the game's (MiniGameSectionBuilder, MgDrawbridge). The art only draws over them, a centimetre clear of every face the
# game still draws (PITFALLS 39, R498).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_drawbridge_v1.py -- [--render] [--export]
#
# Writes C_Drawbridge_v1.blend and render_c_*.png here; with --export the three FBX files under
# Assets/Resources/PawnRushMiniGames/Early/ (MiniGameModels hangs them in Unity):
#   C_Drawbridge_Env   the section's static dressing, section frame
#   C_Drawbridge_Deck  one bridge deck in the hinge's frame (rides "Drawbridge hinge", both stations)
#   C_Drawbridge_Drum  one rope drum in the drum's frame (rides "Drum", both stations)
#
# Frame (as the garden kit): Blender X = section x (east), Blender Y = section z (north), Z = up, plaza floor at 0.
import os, sys, math, random

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..", "GardenConcept")))
sys.path.insert(0, HERE)
os.environ["GC_DRESS"] = "C dressing env"
from gc_kit import *  # noqa: E402,F401  (bpy, bmesh, math, Matrix, Vector, Mesh, M, textured, material, coll ...)
import gc_kit  # noqa: E402
HERE = os.path.dirname(os.path.abspath(__file__))  # gc_kit has its own HERE

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
random.seed(501)

ENV = gc_kit.DRESS
DECK = coll("C dressing deck")
DRUM = coll("C dressing drum")
PREVIEW = gc_kit.CTX

# ------------------------------------------------------------------ the game's numbers (MiniGameSectionBuilder, MgDrawbridge)
DAIS = .4                  # Arena.DaisHeight: a station's floor top over the plaza
MOAT0, MOAT1 = 12., 16.    # game u
HINGE, DECK_LEN, DECK_W = 15.95, 4.1, 4.
WINCH, DRUM_Y, DRUM_R = 11.3, 1.3, .55
FIRST_KNOT = 10.2
CLEAR = .012               # how far art stays off a face the game still draws
SIDES = (-1, 1)            # -1 white (x -12), +1 black (x +12)


def G(s, gx, gy, gz):
    """A station's game frame (x across, y up from the dais top, z = u north) -> Blender."""
    return (12. * s + gx, 4. + gz, DAIS + gy)


# ------------------------------------------------------------------ the phase-1 kit's materials (R499 standard)
STONE, STONE_W, STONE_P, BAND = M["stone"], M["stone2"], M["stone3"], M["stone_dark"]
PAVING, WOOD, GOLD = M["paving"], M["wood"], M["gold"]
SLATE = textured("GC | c slate", gc_kit.STONE_TEX, (.30, .33, .38), .88, .5)
SLATE2 = textured("GC | c slate deep", gc_kit.STONE_TEX, (.26, .29, .34), .9, .5)
SLATE_JOINT = material("GC | c slate joint", (.14, .15, .18), .95)
MOAT_STONE = textured("GC | c moat stone", gc_kit.STONE_TEX, (.62, .57, .50), .85, .5)
WALNUT = textured("GC | c walnut", gc_kit.WOOD_TEX, (.72, .62, .56), .6)
IRON = material("GC | c iron", (.20, .21, .23), .5, .65)
BRASS = material("GC | c brass", (.62, .50, .30), .45, .75)          # decoration: matte brass, never glowing
ROOF = material("GC | c roof slate", (.34, .37, .42), .8)
ROPE = material("GC | c rope", (.80, .64, .42), .9)
LANE = textured("GC | c lane", gc_kit.STONE_TEX, (.22, .24, .28), .85, .5)
TICK = material("GC | c tick gold", (1.0, .72, .26), .4, .4, .5)      # the game's functional gold: glows
LAMP = material("GC | c lantern", (1.0, .80, .48), .5, 0., 2.5)
DOOR_SHADE = material("GC | c arch shade", (.18, .15, .13), .95)
BANNER = material("GC | c banner cream", (.95, .91, .82), .8)
TEAM = {-1: material("GC | c team white", (.247, .757, .851), .7), 1: material("GC | c team black", (.941, .541, .173), .7)}
BUOY = material("GC | c buoy cream", (.93, .90, .84), .7)


class CMesh(gc_kit.Mesh):
    """The kit's Mesh with turned boxes, vertical arches and ear-clipped n-gons (concave slabs)."""

    def obox(self, center, size, rot, mat, bevel=0.):
        mtx = Matrix.Translation(Vector(center)) @ rot.to_4x4() @ Matrix.Diagonal((*size, 1)).to_4x4()
        r = bmesh.ops.create_cube(self.bm, size=1, matrix=mtx)
        edges = {e for v in r["verts"] for e in v.link_edges}
        return self.tag(r["verts"], mat, False, edges, bevel)

    def varch(self, cx, cz, y0, y1, r0, r1, a0, a1, mat, segs=None, bevel=0.):
        """A solid arch piece in the XZ plane round (cx, cz): radii r0..r1, angles a0..a1 degrees, Y from y0 to y1."""
        if segs is None:
            segs = max(2, int(abs(a1 - a0) / 6))
        rings = []
        for i in range(segs + 1):
            a = math.radians(a0 + (a1 - a0) * i / segs)
            c, s = math.cos(a), math.sin(a)
            rings.append([self.bm.verts.new((cx + r * c, y, cz + r * s)) for r, y in ((r0, y0), (r1, y0), (r1, y1), (r0, y1))])
        for a, b in zip(rings, rings[1:]):
            for j in range(4):
                k = (j + 1) % 4
                self.bm.faces.new((a[j], b[j], b[k], a[k]))
        self.bm.faces.new(list(reversed(rings[0])))
        self.bm.faces.new(rings[-1])
        made = [v for r in rings for v in r]
        pick = set()
        if bevel:
            for a, b in zip(rings, rings[1:]):
                for j in range(4):
                    e = self.bm.edges.get((a[j], b[j]))
                    if e: pick.add(e)
            for ring in (rings[0], rings[-1]):
                for j in range(4):
                    e = self.bm.edges.get((ring[j], ring[(j + 1) % 4]))
                    if e: pick.add(e)
        return self.tag(made, mat, False, pick, bevel)

    def vslab(self, outline, cx, y0, y1, mat):
        """A flat solid whose outline (x across, z up) faces south, from Y y0 (its front) to y1."""
        lo = [self.bm.verts.new((cx + x, y0, z)) for x, z in outline]
        hi = [self.bm.verts.new((cx + x, y1, z)) for x, z in outline]
        self.bm.faces.new(list(reversed(lo)))
        self.bm.faces.new(hi)
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        return self.tag(lo + hi, mat)

    def done(self, planar_uv=None):
        big = [f for f in self.bm.faces if len(f.verts) > 4]
        if big:
            bmesh.ops.triangulate(self.bm, faces=big, quad_method="BEAUTY", ngon_method="EAR_CLIP")
        return super().done(planar_uv)


def lathe_x(m, profile, at, mat, segs=24, loop=False, uv_len=None):
    """The kit's lathe turned to run along X (a drum, an axle): profile (radius, x offset)."""
    return m.lathe(profile, Matrix.Translation(Vector(at)) @ Matrix.Rotation(math.radians(90), 4, "Y"), mat, segs,
                   True, loop, uv_len)


def torus_x(m, at, R, r, mat, segs=20, tube=8):
    prof = [(R + r * math.cos(2 * math.pi * k / tube), r * math.sin(2 * math.pi * k / tube)) for k in range(tube)]
    return lathe_x(m, prof, at, mat, segs, loop=True)


def torus_z(m, at, R, r, mat, segs=24, tube=8):
    prof = [(R + r * math.cos(2 * math.pi * k / tube), r * math.sin(2 * math.pi * k / tube)) for k in range(tube)]
    return m.lathe(prof, Matrix.Translation(Vector(at)), mat, segs, True, True)


def rot_z(deg):
    return Matrix.Rotation(math.radians(deg), 3, "Z")


# ================================================================== 1. floors: cream paving and the slate dais
def paving():
    m = CMesh("C paving", ENV)
    top, bottom = .02, .004
    rects = [(-22, 22, 0, 2.4), (-22, -14.2, 2.4, 4), (-9.8, 9.8, 2.4, 4), (14.2, 22, 2.4, 4),  # entry band, not the ramps
             (-2, 2, 4, 16), (-2, 2, 20, 24),                                                       # central aisle
             (-14, -10, 24, 30), (10, 14, 24, 30),                                                  # team gate passages
             (-22, 22, 30, 36)]                                                                     # rear corridor
    for x0, x1, y0, y1 in rects:
        m.box((x0, y0, bottom), (x1, y1, top), PAVING)
    # A darker cut-stone kerb along the aisle's sides, flush with the paving.
    for x in (-2, 1.82):
        m.box((x, 4, top - .001), (x + .18, 16, top + .012), BAND, .01)
        m.box((x, 20, top - .001), (x + .18, 24, top + .012), BAND, .01)
    return m.done(lambda co: (co.x / 2.4, co.y / 2.4))


def dais():
    """Slate flagstones a centimetre over each station's floor, cream coping along the moat."""
    m = CMesh("C dais", ENV)
    for s in SIDES:
        for z0, z1 in ((0., MOAT0 - .6), (MOAT1 + .6, 20.)):
            a, b = G(s, -10, 0, z0), G(s, 10, 0, z1)
            m.box((a[0], a[1], DAIS + .002), (b[0], b[1], DAIS + .005), SLATE_JOINT)
            rows = int(round(z1 - z0))
            h = (z1 - z0) / rows
            for r in range(rows):
                cuts = [-10.] + [k + (.5 if r % 2 else 0.) for k in range(-10, 10)] + [10.]
                cuts = sorted({min(10., max(-10., c)) for c in cuts})
                for xa, xb in zip(cuts, cuts[1:]):
                    if xb - xa < .1:
                        continue
                    lo, hi = G(s, xa + .012, 0, z0 + r * h + .012), G(s, xb - .012, 0, z0 + (r + 1) * h - .012)
                    m.box((lo[0], lo[1], DAIS + .005), (hi[0], hi[1], DAIS + .011), random.choice((SLATE, SLATE, SLATE2)))
        # The coping: cut cream blocks 0.6 m deep at the moat, overhanging it by 3 cm.
        for z0, z1 in ((MOAT0 - .6, MOAT0 + .03), (MOAT1 - .03, MOAT1 + .6)):
            x = -10.
            while x < 9.99:
                w = min(1.25, 10 - x)
                lo, hi = G(s, x + .01, 0, z0), G(s, x + w - .01, 0, z1)
                m.box((lo[0], lo[1], DAIS + .004), (hi[0], hi[1], DAIS + .018), random.choice((STONE, STONE_P)), .012)
                x += w
    return m.done()


# ================================================================== 2. the moats' stone and the central railing
def ashlar_face(m, axis, at, u0, u1, z0, z1, depth, facing, mats, course=.5, block=1.0, gap=.022, bevel=0.):
    """Blocks in a vertical plane. axis 'y': the face lies at Y = at (x from u0 to u1), else at X = at (Y from u0 to u1);
    `facing` +1/-1 is the side the blocks show on; they reach `depth` back into the wall."""
    z, row = z0, 0
    while z < z1 - .05:
        h = min(course, z1 - z)
        u = u0 - (block * .5 if row % 2 else 0.)
        while u < u1 - .02:
            a, b = max(u, u0), min(u + block, u1)
            if b - a > .08:
                front, back = at, at - facing * depth
                lo_f, hi_f = min(front, back), max(front, back)
                if axis == "y":
                    m.box((a + gap * .5, lo_f, z + gap * .5), (b - gap * .5, hi_f, z + h - gap * .5), random.choice(mats), bevel)
                else:
                    m.box((lo_f, a + gap * .5, z + gap * .5), (hi_f, b - gap * .5, z + h - gap * .5), random.choice(mats), bevel)
            u += block
        z += course
        row += 1


def moats():
    m = CMesh("C moat", ENV)
    mats = (MOAT_STONE, MOAT_STONE, STONE_W)
    top = DAIS - CLEAR             # just under the station floor's top
    for s in SIDES:
        x0, x1 = 12 * s - 9.97, 12 * s + 9.97
        lo_y, hi_y = DAIS - 1.6, top
        # South face: in front of the floor's north face (game u 12), facing north; north face, facing south.
        m.box((x0, 4 + MOAT0 - .17, lo_y), (x1, 4 + MOAT0 + CLEAR, hi_y), SLATE_JOINT)
        ashlar_face(m, "y", 4 + MOAT0 + .03, x0, x1, lo_y, hi_y, .05, 1, mats)
        m.box((x0, 4 + MOAT1 - CLEAR, lo_y), (x1, 4 + MOAT1 + .17, hi_y), SLATE_JOINT)
        ashlar_face(m, "y", 4 + MOAT1 - .03, x0, x1, lo_y, hi_y, .05, -1, mats)
        # The two ends (the game's walls W/E, top at the plaza floor).
        for e in (-1, 1):
            face = 12 * s + e * 9.78
            m.box((min(face, face + e * .17), 4 + MOAT0, lo_y), (max(face, face + e * .17), 4 + MOAT1, -CLEAR), SLATE_JOINT)
            ashlar_face(m, "x", face - e * .03, 4 + MOAT0 + .03, 4 + MOAT1 - .03, lo_y, -CLEAR, .05, -e, mats)
        # The bottom, a centimetre over the game's.
        m.box((x0, 4 + MOAT0, DAIS - 1.6), (x1, 4 + MOAT1, DAIS - 1.59), BAND)
    # The central moat (the aisle's, between the stations): lined the same way, its bottom 1.2 m down.
    lo_y = -1.2
    m.box((-1.97, 16 - .17, lo_y), (1.97, 16 + CLEAR, -CLEAR), SLATE_JOINT)
    ashlar_face(m, "y", 16.03, -1.97, 1.97, lo_y, -CLEAR, .05, 1, mats)
    m.box((-1.97, 20 - CLEAR, lo_y), (1.97, 20.17, -CLEAR), SLATE_JOINT)
    ashlar_face(m, "y", 19.97, -1.97, 1.97, lo_y, -CLEAR, .05, -1, mats)
    for e in (-1, 1):
        ashlar_face(m, "x", e * 1.97, 16.03, 19.97, lo_y, -CLEAR, .05, -e, mats)
    m.box((-1.97, 16, lo_y - .1), (1.97, 20, lo_y), BAND)
    # The railing where the aisle meets the moat (the game's collision box: x -2..2, u 15.7..16, 1.1 m).
    m.box((-1.98, 15.72, .02), (1.98, 15.98, .26), STONE_W, .02)
    for x in (-1.75, 1.75):
        m.box((x - .2, 15.72, .02), (x + .2, 15.98, 1.08), STONE, .03)
        m.box((x - .24, 15.70, 1.0), (x + .24, 15.99, 1.1), STONE_P, .02)
    for k in range(5):
        x = -1.1 + k * .55
        m.lathe([(0, 0), (.09, 0), (.09, .05), (.06, .12), (.1, .34), (.06, .56), (.09, .62), (.09, .66), (0, .66)],
                Matrix.Translation((x, 15.85, .26)), STONE_P, 16)
    m.box((-1.56, 15.73, .92), (1.56, 15.97, 1.05), STONE, .02)
    # The crown plaque on the aisle side.
    m.box((-.5, 15.69, .38), (.5, 15.72, .74), BRASS, .006)
    crown(m, (0, 15.67, .5), .32, "y")
    # Lily pads on the water (the game's water top is 0.5 m under the plaza floor), a centimetre over it.
    for s in SIDES:
        for k in range(7):
            x = 12 * s + random.uniform(-9, 9)
            if abs(x - 12 * s) < 2.6:
                continue
            y = random.uniform(4 + MOAT0 + .5, 4 + MOAT1 - .5)
            m.lathe([(0, 0), (.32, 0), (.32, .012), (0, .012)], Matrix.Translation((x, y, -.49)) @ Matrix.Rotation(random.uniform(0, 6.3), 4, "Z"), M["lily"], 14)
            if k % 3 == 0:
                m.flower((x + .1, y, -.47), .1, M["white"])
    return m.done()


def crown(m, at, size, facing="y"):
    """A small brass crown standing in the XZ plane (seen from the south)."""
    x, y, z = at
    m.box((x - .5 * size, y - .03, z - .1 * size), (x + .5 * size, y + .03, z + .08 * size), BRASS, .005)
    for k in range(-2, 3):
        h = (.55 if k == 0 else .32 if abs(k) == 1 else .44) * size
        m.box((x + k * .22 * size - .05 * size, y - .025, z), (x + k * .22 * size + .05 * size, y + .025, z + h), BRASS, .004)
        m.sphere((x + k * .22 * size, y, z + h + .05 * size), .06 * size, BRASS, subdiv=1)


# ================================================================== 3. the stations' machines (static parts)
def bridge_towers():
    m = CMesh("C bridge towers", ENV)
    for s in SIDES:
        for e in (-1, 1):
            cx, cy, base = G(s, e * 2.4, 0, 16.2)
            # Shaft: the game's 0.7 m tower, as courses of cream block; the chain enters at game y 5.4.
            z = base + .01
            k = 0
            while z < base + 5.15:
                h = min(.45, base + 5.15 - z)
                w = .37 if k % 2 else .35
                m.box((cx - w, cy - w, z), (cx + w, cy + w, z + h - .015), random.choice((STONE, STONE_W, STONE_P)), .02)
                z += h
                k += 1
            m.box((cx - .5, cy - .5, base + 5.15), (cx + .5, cy + .5, base + 5.42), BAND, .03)
            for a in range(4):
                for b in (-1, 1):
                    # Merlons on the four sides, the chain's gap in the middle of each.
                    if a % 2 == 0:
                        lo, hi = (cx + b * .18, cy - .5 if a == 0 else cy + .3), (cx + b * .5, cy - .3 if a == 0 else cy + .5)
                    else:
                        lo, hi = (cx - .5 if a == 1 else cx + .3, cy + b * .18), (cx - .3 if a == 1 else cx + .5, cy + b * .5)
                    m.box((min(lo[0], hi[0]), min(lo[1], hi[1]), base + 5.42), (max(lo[0], hi[0]), max(lo[1], hi[1]), base + 5.95), STONE, .025)
            # The lantern on the south face, at 3.8 m: an iron bracket and a brass cage round a warm light.
            ly = cy - .37
            m.box((cx - .04, ly - .32, base + 3.95), (cx + .04, ly, base + 4.02), IRON)
            m.box((cx - .13, ly - .45, base + 3.42), (cx + .13, ly - .19, base + 3.44), BRASS)
            m.box((cx - .14, ly - .46, base + 3.82), (cx + .14, ly - .18, base + 3.88), BRASS, .01)
            for dx in (-.12, .12):
                for dy in (-.44, -.2):
                    m.box((cx + dx - .015, ly + dy - .015, base + 3.44), (cx + dx + .015, ly + dy + .015, base + 3.82), BRASS)
            m.box((cx - .1, ly - .42, base + 3.46), (cx + .1, ly - .22, base + 3.8), LAMP)
            m.lathe([(0, 0), (.17, 0), (0, .16)], Matrix.Translation((cx, ly - .32, base + 3.88)), IRON, 4)
    return m.done()


def winches():
    """Each station's winch frame, lamp post and rope lane (the drum itself rides the game's Drum)."""
    m = CMesh("C winches", ENV)
    for s in SIDES:
        ax = 4. * s                 # the winch assembly's offset across (ropeOffset): outside the bridge's approach
        wx, wy, b = G(s, ax, 0, WINCH)
        top = b + .011
        # Sills and cheeks: walnut, brass bolt heads; the drum's axle sits in the cheeks at 1.3 m.
        for dy in (-.45, .3):
            m.box((wx - 1.58, wy + dy, top), (wx + 1.58, wy + dy + .15, top + .2), WALNUT, .02)
        for e in (-1, 1):
            cx = wx + e * 1.35
            m.box((cx - .1, wy - .42, top + .2), (cx + .1, wy + .42, b + DRUM_Y - .12), WALNUT, .02)
            m.vslab([(-.1, 0), (.1, 0), (.1, .32), (0, .42), (-.1, .32)], cx, wy - .38, wy + .38, WALNUT)
            m.box((cx - .12, wy - .3, b + DRUM_Y - .12), (cx + .12, wy + .3, b + DRUM_Y + .22), WALNUT, .025)
            torus_x(m, (cx + e * .13, wy, b + DRUM_Y), .13, .035, IRON, 16, 6)
            for dy in (-.3, .3):
                m.box((cx - .14, wy + dy - .03, top + .1), (cx + .14, wy + dy + .03, top + .16), BRASS, .005)
            # A brace from the sill to the cheek.
            m.obox((cx, wy - .62, top + .5), (.12, .1, 1.0), Matrix.Rotation(math.radians(-38), 3, "X"), WALNUT, .015)
        # The lamp post (the game's three latch lamps hang on its south face at game x 2*s from the winch).
        px = wx + 2. * s
        m.box((px - .11, wy - .09, top), (px + .11, wy + .13, b + 2.55), WALNUT, .02)
        m.box((px - .16, wy - .14, b + 2.55), (px + .16, wy + .18, b + 2.68), BAND, .02)
        for i in range(3):
            ly = b + 1. + i * .6
            m.box((px - .1, wy - .17, ly - .14), (px + .1, wy - .09, ly - .1), BRASS)
            torus_z(m, (px, wy - .16, ly - .1), .1, .012, BRASS, 16, 5)
        # The rope lane: dark slate with brass edges, a glowing gold tick every metre, chevrons pointing south.
        lane_y0, lane_y1 = G(s, ax, 0, .2)[1], G(s, ax, 0, 10.9)[1]
        m.box((wx - 1.2, lane_y0, b + .011), (wx + 1.2, lane_y1, b + .02), LANE)
        for e in (-1, 1):
            m.box((wx + e * 1.2 - (.06 if e > 0 else 0), lane_y0, b + .011), (wx + e * 1.2 + (0 if e > 0 else .06), lane_y1, b + .026), BRASS)
            y = lane_y0 + .5
            while y < lane_y1 - .3:
                m.sphere((wx + e * 1.13, y, b + .022), .035, BRASS, (1, 1, .5), 1)
                y += 1.
        for i in range(8):
            ty = G(s, ax, 0, FIRST_KNOT - i)[1]
            m.box((wx - 1.12, ty - .03, b + .02), (wx + 1.12, ty + .03, b + .028), TICK)
        for i in range(4):
            cy = G(s, ax, 0, 9. - i * 2.2 + .14)[1]
            for side in (-1, 1):
                m.obox((wx - 1.05 + side * .14, cy, b + .024), (.06, .44, .008), rot_z(side * 40), TICK)
    return m.done()


# ================================================================== 4. the north facade, gates, towers, banners
WALL_TOP = 10.0


def body(m, x0, x1, z0, z1, y0=24.0, y1=30.0, mat=None):
    m.box((x0, y0, z0), (x1, y1, z1), mat or STONE_W)


def facade():
    m = CMesh("C facade", ENV)
    face = 23.94
    # Wall body (the game's north blocks: |x| < 10 and 14 < |x| < 22, u 24..30, 11 m), solid back to the corridor.
    for x0, x1 in ((-22, -14.25), (-9.75, 9.75), (14.25, 22)):
        body(m, x0, x1, 0, WALL_TOP)
    for s in SIDES:
        body(m, 12 * s - 2.25, 12 * s + 2.25, 6.05, WALL_TOP)
    # Ashlar over the front, skipping the gates and the towers in front of it.
    holes = [(-4.2, 4.2, 9.3)] + [(12 * s - 3.0, 12 * s + 3.0, 6.6) for s in SIDES] + \
            [(s * 4.35 if s < 0 else 4.35, None, None) for s in ()]
    towers = [(-7.05, -4.35), (4.35, 7.05), (-21.5, -16.5), (16.5, 21.5)]

    def skip(a, b, z):
        for x0, x1, top in holes:
            if b > x0 and a < x1 and z < top:
                return True
        for x0, x1 in towers:
            if b > x0 + .05 and a < x1 - .05:
                return True
        return False

    z, row = .62, 0
    while z < WALL_TOP - .1:
        h = min(.62, WALL_TOP - z)
        u = -22. - (.65 if row % 2 else 0.)
        while u < 22:
            a, b = max(u, -22.), min(u + 1.3, 22.)
            if b - a > .1 and not skip(a, b, z):
                m.box((a + .012, face, z + .012), (b - .012, 24.02, z + h - .012), random.choice((STONE, STONE, STONE_P, STONE_W)), .02)
            u += 1.3
        z += .62
        row += 1
    # Plinth and cornice; merlons along the top.
    for x0, x1 in ((-22, -21.5), (-16.5, -14.95), (-9.05, -7.05), (-4.35, -4.15), (4.15, 4.35), (7.05, 9.05), (14.95, 16.5), (21.5, 22)):
        m.box((x0, 23.86, 0.), (x1, 24.03, .62), BAND, .03)
    m.box((-22, 23.72, WALL_TOP), (22, 24.4, WALL_TOP + .34), BAND, .04)
    x = -21.6
    while x < 21.7:
        m.box((x - .38, 23.9, WALL_TOP + .34), (x + .38, 24.7, WALL_TOP + 1.0), STONE, .03)
        x += 1.3

    # ---- the central gate (closed): arched oak door, iron straps, voussoir ring, keystone, brass crown
    spring, r_in, r_out = 5.0, 3.4, 4.15
    door = [(-3.38, 0), (3.38, 0), (3.38, spring)] + \
           [(3.38 * math.cos(math.radians(a)), spring + 3.38 * math.sin(math.radians(a))) for a in range(10, 171, 10)] + [(-3.38, spring)]
    door[0], door[1] = (-3.38, .02), (3.38, .02)
    m.vslab(door, 0, 23.97, 24.05, DOOR_SHADE)
    for i in range(7):
        x0 = -3.36 + i * .96
        top = spring + math.sqrt(max(0., 3.36 ** 2 - (x0 + .48) ** 2)) - .08
        m.box((x0 + .02, 23.88, .03), (x0 + .94, 23.97, top), WALNUT, .02)
    for zz in (1.0, 3.0, 5.0):
        m.box((-3.3, 23.84, zz), (3.3, 23.88, zz + .16), IRON, .01)
        for xx in (-3.0, -1.9, -.7, .7, 1.9, 3.0):
            m.sphere((xx, 23.83, zz + .08), .05, BRASS, (1, .6, 1), 1)
    for xx in (-.4, .4):
        m.obox((xx, 23.82, 3.0), (.08, .06, .08), Matrix.Identity(3), IRON)
        m.lathe([(.16, -.025), (.2, 0), (.16, .025), (.12, 0)], Matrix.Translation((xx, 23.8, 2.78)) @ Matrix.Rotation(math.radians(90), 4, "X"), BRASS, 18, True, True)
    for e in (-1, 1):
        zz = 0.
        k = 0
        while zz < spring - .05:
            h = min(.62, spring - zz)
            w = .75 if k % 2 else .62
            m.box((e * r_in if e > 0 else -r_in - w, 23.8, zz + .012), (e * r_in + w if e > 0 else -r_in, 24.06, zz + h - .012), random.choice((STONE, STONE_P)), .025)
            zz += h
            k += 1
    n = 15
    for i in range(n):
        a0, a1 = 180 * i / n + .6, 180 * (i + 1) / n - .6
        m.varch(0, spring, 23.78, 24.06, r_in, r_out + (.15 if i == n // 2 else 0), a0, a1, STONE_P if i % 2 else STONE, 3, .02)
    for e in (-1, 1):
        pts = [(e * 4.2, spring), (e * 4.2, 9.3), (0, 9.3), (0, spring + r_out + .02)] + \
              [(e * r_out * math.cos(math.radians(a)), spring + r_out * math.sin(math.radians(a))) for a in range(80, -1, -10)]
        if e > 0:
            m.vslab(list(reversed(pts)), 0, 23.94, 24.02, STONE_W)
        else:
            m.vslab([(x_, z_) for x_, z_ in pts], 0, 23.94, 24.02, STONE_W)
    crown(m, (0, 23.72, 9.45), 1.6)

    # ---- the team portals: open arches, a vaulted passage to the corridor, a team banner, two lanterns
    p_spring, p_in, p_out = 3.55, 2.25, 2.95
    for s in SIDES:
        cx = 12. * s
        for yfront, yback in ((23.78, 24.08), (29.92, 30.22)):
            for e in (-1, 1):
                zz, k = 0., 0
                while zz < p_spring - .05:
                    h = min(.6, p_spring - zz)
                    w = .7 if k % 2 else .58
                    x0 = cx + e * p_in if e > 0 else cx - p_in - w
                    m.box((x0, yfront, zz + .012), (x0 + w, yback, zz + h - .012), random.choice((STONE, STONE_P)), .025)
                    zz += h
                    k += 1
            for i in range(11):
                a0, a1 = 180 * i / 11 + .8, 180 * (i + 1) / 11 - .8
                m.varch(cx, p_spring, yfront, yback, p_in, p_out + (.12 if i == 5 else 0), a0, a1, STONE if i % 2 else STONE_P, 3, .02)
        for e in (-1, 1):
            pts = [(e * 3.0, p_spring), (e * 3.0, 6.6), (0, 6.6), (0, p_spring + p_out + .02)] + \
                  [(e * p_out * math.cos(math.radians(a)), p_spring + p_out * math.sin(math.radians(a))) for a in range(80, -1, -10)]
            m.vslab(list(reversed(pts)) if e > 0 else pts, cx, 23.94, 24.02, STONE_W)
        # The passage's vault and walls (the game's gate passage, x +-2 of the portal, u 24..30).
        m.varch(cx, p_spring, 24.0, 30.0, p_in, p_in + .15, 0, 180, STONE_P, 18)
        for e in (-1, 1):
            m.box((cx + e * 2.1 - .075, 24.0, .02), (cx + e * 2.1 + .075, 30.0, p_spring), STONE_P)
        # Team banner over the portal (team colour is the gate's owner: the R499 standard's one use of it).
        top = 9.5
        notch = [(-1.0, top), (1.0, top), (1.0, top - 2.6), (0, top - 2.2), (-1.0, top - 2.6)]
        m.vslab(list(reversed(notch)), cx, 23.82, 23.9, TEAM[s])
        m.box((cx - 1.25, 23.78, top - .02), (cx + 1.25, 23.9, top + .08), BRASS, .02)
        for e in (-1, 1):
            m.sphere((cx + e * 1.3, 23.84, top + .03), .07, BRASS, subdiv=1)
        # A cream rook on the banner.
        rook = [(-.3, 7.55), (.3, 7.55), (.3, 7.68), (.2, 7.72), (.16, 8.25), (.28, 8.3), (.28, 8.62), (.17, 8.62), (.17, 8.5),
                (.06, 8.5), (.06, 8.62), (-.06, 8.62), (-.06, 8.5), (-.17, 8.5), (-.17, 8.62), (-.28, 8.62), (-.28, 8.3),
                (-.16, 8.25), (-.2, 7.72), (-.3, 7.68)]
        m.vslab(list(reversed(rook)), cx, 23.79, 23.82, BANNER)
        for e in (-1, 1):
            sconce(m, cx + e * 3.3, 3.6, 23.86)

    # ---- towers: two rook towers flanking the gate, two slate-roofed corner gatehouses
    for x0, x1, height, roof in ((-7.05, -4.35, 12.4, False), (4.35, 7.05, 12.4, False), (-21.5, -16.5, 11.8, True), (16.5, 21.5, 11.8, True)):
        tower(m, x0, x1, 23.84, 30.0, height, roof)
    # Cream banners with a gold laurel between the gate towers and the portals.
    for x in (-8.05, 8.05):
        laurel_banner(m, x, 8.9, 23.86)
    for x in (-15.72, 15.72):
        laurel_banner(m, x, 8.4, 23.86, .8)
    return m.done()


def sconce(m, x, z, y):
    m.box((x - .12, y, z - .3), (x + .12, y + .08, z + .3), IRON, .01)
    m.box((x - .03, y - .3, z + .2), (x + .03, y, z + .26), IRON)
    m.box((x - .16, y - .52, z - .28), (x + .16, y - .2, z - .25), BRASS)
    m.box((x - .17, y - .53, z + .14), (x + .17, y - .19, z + .2), BRASS, .01)
    for dx in (-.15, .15):
        for dy in (-.5, -.22):
            m.box((x + dx - .015, y + dy - .015, z - .25), (x + dx + .015, y + dy + .015, z + .14), BRASS)
    m.box((x - .12, y - .48, z - .24), (x + .12, y - .24, z + .12), LAMP)
    m.lathe([(0, 0), (.22, 0), (0, .2)], Matrix.Translation((x, y - .36, z + .2)), IRON, 4)


def laurel_banner(m, x, top, y, w=1.0):
    pts = [(-.5 * w, top), (.5 * w, top), (.5 * w, top - 2.6), (0, top - 2.25), (-.5 * w, top - 2.6)]
    m.vslab(list(reversed(pts)), x, y, y + .06, BANNER)
    m.box((x - .62 * w, y - .04, top - .02), (x + .62 * w, y + .06, top + .07), BRASS, .015)
    # A brass laurel: two arcs of leaves round the middle.
    cz = top - 1.25
    for e in (-1, 1):
        for k in range(7):
            a = math.radians(-70 + k * 22)
            px, pz = x + e * .3 * w * math.cos(a), cz + .3 * w * math.sin(a)
            m.obox((px, y - .012, pz), (.07 * w, .015, .14 * w), Matrix.Rotation(-e * (a + math.radians(20)), 3, "Y"), BRASS)
    m.box((x - .03 * w, y - .015, cz - .36 * w), (x + .03 * w, y, cz - .26 * w), BRASS)


def tower(m, x0, x1, y0, y1, height, roof):
    w = x1 - x0
    m.box((x0 + .04, y0 + .05, 0), (x1 - .04, y1, height - .5), STONE_W)
    z, k = 0., 0
    while z < height - .55:
        h = min(.62, height - .5 - z)
        # Quoins: long and short blocks alternating at both corners; plain blocks between.
        u = x0
        while u < x1 - .02:
            bw = min(1.0 if (k % 2 == 0) == (u == x0) else .7, x1 - u)
            m.box((u + .012, y0, z + .012), (u + bw - .012, y0 + .1, z + h - .012), random.choice((STONE, STONE_P)), .02)
            u += bw
        z += h
        k += 1
    m.box((x0 - .14, y0 - .14, height - .5), (x1 + .14, y1, height - .14), BAND, .04)
    if roof:
        # A short slate pyramid (grey, never blue: a blue roof reads as the white team's).
        cx, cy = (x0 + x1) * .5, (y0 + y1) * .5
        d = (y1 - y0) * .5 + .2
        hw = w * .5 + .2
        r = max(hw, d) * 1.414
        m.lathe([(0, 0), (r, 0), (0, 3.0)], Matrix.Translation((cx, cy, height - .14)) @ Matrix.Rotation(math.radians(45), 4, "Z")
                @ Matrix.Diagonal((hw / max(hw, d), d / max(hw, d), 1, 1)), ROOF, 4, False)
        m.lathe([(0, 0), (.1, 0), (.1, .3), (.04, .35), (.04, .75), (0, .8)], Matrix.Translation((cx, cy, height + 2.95)), BRASS, 10)
    else:
        u = x0 - .1
        while u < x1:
            bw = min(.62, x1 + .1 - u)
            m.box((u + .02, y0 - .1, height - .14), (u + bw - .14, y0 + .45, height + .6), STONE, .03)
            u += .8


# ================================================================== 5. side, entry and rear walls; the garden corners
def low_wall(m, x0, x1, y0, y1, height, merlons=True):
    m.box((x0, y0, 0), (x1, y1, height), STONE_W)
    along_y = (y1 - y0) > (x1 - x0)
    if along_y:
        m.box((x0 - .06, y0, height), (x1 + .06, y1, height + .22), BAND, .03)
    else:
        m.box((x0, y0 - .06, height), (x1, y1 + .06, height + .22), BAND, .03)
    if not merlons:
        return
    u0, u1 = (y0, y1) if along_y else (x0, x1)
    u = u0 + .25
    while u < u1 - .5:
        if along_y:
            m.box((x0, u, height + .22), (x1, u + .7, height + .8), STONE, .03)
        else:
            m.box((u, y0, height + .22), (u + .7, y1, height + .8), STONE, .03)
        u += 1.2


def walls():
    m = CMesh("C walls", ENV)
    H = 3.2
    for e in (-1, 1):
        # Sides: the game's walls at |x| 22..23 (11 m of collision; drawn low, as the garden's).
        x0, x1 = (22.02, 22.8) if e > 0 else (-22.8, -22.02)
        low_wall(m, x0, x1, -.75, 30.0, H)
        ashlar_face(m, "x", e * 22.0, -.7, 23.84, .0, H, .03, -e, (STONE, STONE_P, STONE_W), .55, 1.2, .022, .01)
        for k in range(7):
            y = 1.5 + k * 3.7
            m.box((min(e * 22.8, e * 23.3), y - .35, 0), (max(e * 22.8, e * 23.3), y + .35, H - .3), STONE, .03)
        low_wall(m, x0, x1, 30.0, 36.6, H)
        # Entry wings (|x| 6.85..22, u -0.75..-0.05) and the rear end walls (u 36..36.6), 7 m at the rear.
        low_wall(m, min(e * 6.85, e * 22.8), max(e * 6.85, e * 22.8), -.75, -.05, H)
        low_wall(m, min(e * 6.85, e * 22.8), max(e * 6.85, e * 22.8), 36.02, 36.6, 7.0, False)
        ashlar_face(m, "y", 36.0, min(e * 6.85, e * 22.0), max(e * 6.85, e * 22.0), 0, 7.0, .03, -1, (STONE, STONE_P, STONE_W), .62, 1.3)
    # The entry and exit arches: 12.5 m wide openings (the game's |x| < 6 stays clear), springing at 1.25 m.
    for yfront, yback, top in ((-.78, -.02, 7.5), (35.96, 36.62, 7.5)):
        for e in (-1, 1):
            zz, k = 0., 0
            while zz < 1.24:
                h = min(.62, 1.25 - zz)
                x0 = 6.25 if e > 0 else -6.85
                m.box((x0, yfront, zz + .012), (x0 + .6, yback, zz + h - .012), STONE_P if k % 2 else STONE, .025)
                zz += h
                k += 1
        for i in range(21):
            a0, a1 = 180 * i / 21 + .4, 180 * (i + 1) / 21 - .4
            m.varch(0, 1.25, yfront, yback, 6.25, 6.9 + (.15 if i == 10 else 0), a0, a1, STONE if i % 2 else STONE_P, 3, .025)
        crown(m, (0, yfront - .03, 8.5), 1.0)
    # Fill between the exit arch and the 7 m rear wall.
    for e in (-1, 1):
        amax, amin = math.degrees(math.asin((7.0 - 1.25) / 6.9)), math.degrees(math.acos(6.85 / 6.9))
        arc = [amax - (amax - amin) * k / 10 for k in range(11)]
        pts = [(e * 6.85, 1.25 + 6.9 * math.sin(math.radians(amin))), (e * 6.85, 7.0)] + [
            (e * 6.9 * math.cos(math.radians(a)), 1.25 + 6.9 * math.sin(math.radians(a))) for a in arc]
        m.vslab(list(reversed(pts)) if e > 0 else pts, 0, 36.04, 36.58, STONE_W)
    return m.done()


def corners():
    """Garden corners (cypress over the side walls, stone planters, benches) and the threshold props."""
    m = CMesh("C garden corners", ENV)
    for e in (-1, 1):
        for k in range(7):
            y = 3.3 + k * 3.7
            x = e * 24.2
            m.lathe([(.15, 0), (.15, .6), (0, .6)], Matrix.Translation((x, y, 0)), WALNUT, 8)
            for t in range(5):
                m.sphere((x, y, 1.2 + t * 1.0), .95 - t * .15, gc_kit.LEAVES[t % 3], (1, 1, 1.25), 2)
        for y in (1.7, 34.3):
            planter(m, e * 20.6, y)
        m.box((e * 18.2 - .95, 34.0 - .3, .4), (e * 18.2 + .95, 34.0 + .3, .52), WALNUT, .02)
        for dx in (-.75, .75):
            m.box((e * 18.2 + dx - .12, 34.0 - .26, .02), (e * 18.2 + dx + .12, 34.0 + .26, .4), STONE, .02)
        # Threshold set (the R312 kit's): coiled spare rope, a spare cog, a cream buoy, a crate.
        tx, ty = e * 16.4, 1.6
        for i in range(4):
            torus_z(m, (tx - e * .9, ty, .08 + i * .075), .42 - i * .03, .05, ROPE, 24, 6)
        cog(m, (tx + e * .3, ty + .2, .62), .55)
        torus_x_vertical(m, (tx + e * 1.25, ty + .35, .62), .45, .11, BUOY)
        m.box((tx + e * 1.9 - .4, ty - .35, .02), (tx + e * 1.9 + .4, ty + .35, .7), WALNUT, .03)
        m.box((tx + e * 1.9 - .42, ty - .37, .3), (tx + e * 1.9 + .42, ty + .37, .38), IRON, .01)
    return m.done()


def planter(m, x, y):
    m.lathe([(0, 0), (.62, 0), (.62, .3), (.5, .34), (.66, .95), (.76, 1.0), (.76, 1.08), (0, 1.08)], Matrix.Translation((x, y, 0)), STONE, 20)
    m.sphere((x, y, 1.25), .55, M["leaf"], (1, 1, .7), 2)
    for k in range(7):
        a = k * 2 * math.pi / 7
        m.flower((x + .38 * math.cos(a), y + .38 * math.sin(a), 1.48), .13, random.choice(gc_kit.FLOWERS))


def cog(m, at, r):
    """A spare brass cog standing on its edge, facing south."""
    x, y, z = at
    m.lathe([(r * .25, -.05), (r * .82, -.05), (r * .82, .05), (r * .25, .05)], Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(90), 4, "X"), BRASS, 24, True, True)
    for k in range(12):
        a = k * 30
        rr = Matrix.Rotation(math.radians(a), 3, "Y")
        m.obox(Vector((x, y, z)) + rr @ Vector((0, 0, r * .9)), (r * .2, .1, r * .22), rr, BRASS)


def torus_x_vertical(m, at, R, r, mat):
    """A ring standing up, facing south (a life buoy), with four dark bands."""
    prof = [(R + r * math.cos(2 * math.pi * k / 8), r * math.sin(2 * math.pi * k / 8)) for k in range(8)]
    m.lathe(prof, Matrix.Translation(Vector(at)) @ Matrix.Rotation(math.radians(90), 4, "X"), mat, 24, True, True)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        rr = Matrix.Rotation(a, 3, "Y")
        m.obox(Vector(at) + rr @ Vector((0, 0, R)), (.16, r * 2.3, r * 2.3), rr, IRON)


# ================================================================== 6. the central raised bridge (decor, never lowered)
def central_bridge():
    m = CMesh("C central bridge", ENV)
    # Standing at the central moat's north bank, in front of the main gate: planks across, iron straps.
    for i in range(8):
        z0 = .3 + i * .5
        m.box((-1.75, 20.02, z0 + .01), (1.75, 20.24, z0 + .49), WALNUT if i % 2 else WOOD, .02)
    for x in (-1.35, 1.35):
        m.box((x - .07, 19.98, .3), (x + .07, 20.02, 4.3), IRON)
        for k in range(7):
            m.sphere((x, 19.97, .55 + k * .55), .045, BRASS, (1, .6, 1), 1)
    for e in (-1, 1):
        x = e * 2.25
        z = .02
        while z < 3.0:
            m.box((x - .33, 20.0, z), (x + .33, 20.66, z + .43), random.choice((STONE, STONE_P)), .02)
            z += .45
        m.box((x - .45, 19.88, 3.0), (x + .45, 20.78, 3.25), BAND, .03)
        for a in range(4):
            dx, dy = [(-.3, -.3), (.3, -.3), (.3, .3), (-.3, .3)][a]
            m.box((x + dx - .14, 20.33 + dy - .14, 3.25), (x + dx + .14, 20.33 + dy + .14, 3.65), STONE, .02)
        m.box((x - e * .33 - .04, 20.1, 3.6), (x - e * .33 + .04, 20.2, 4.2), IRON)
    return m.done()


# ================================================================== 7. the moving parts: deck and drum (their own frames)
def deck():
    """The game's deck in the hinge frame: X across (-2..2), Y = hinge-local z (-4.1..0), Z = hinge-local y (top 0)."""
    m = CMesh("C deck", DECK)
    n = 8
    step = DECK_LEN / n
    # Wood all through: while the bridge stands, its underside is what the players see.
    m.box((-1.9, -DECK_LEN + .05, -.2), (1.9, -.05, -.06), SLATE_JOINT)
    for i in range(n):
        y0 = -DECK_LEN + i * step
        m.box((-2.0, y0 + .012, -.24), (2.0, y0 + step - .012, -.004), WALNUT if i % 2 else WOOD, .015)
    for y in (-DECK_LEN + .5, -DECK_LEN * .5, -.5):
        m.box((-1.9, y - .12, -.3), (1.9, y + .12, -.24), WALNUT, .015)
        m.box((-1.92, y - .04, -.33), (1.92, y + .04, -.3), IRON, .005)
        for x in (-1.6, -.8, 0, .8, 1.6):
            m.sphere((x, y, -.335), .03, BRASS, (1, 1, .5), 1)
    for x in (-1.5, 1.5):
        m.box((x - .08, -DECK_LEN + .02, -.004), (x + .08, -.02, .018), IRON, .005)
        for k in range(8):
            m.sphere((x, -DECK_LEN + .3 + k * .5, .02), .035, BRASS, (1, 1, .55), 1)
        m.box((x * .8 - .1, -DECK_LEN + .05, -.4), (x * .8 + .1, -.05, -.33), WALNUT, .02)
    m.box((-2.0, -DECK_LEN, -.22), (2.0, -DECK_LEN + .1, .012), IRON, .01)
    for x in (-1.85, 1.85):
        lathe_x(m, [(.0, -.06), (.07, -.06), (.07, .06), (.0, .06)], (x, -DECK_LEN + .14, .1), IRON, 12)
        torus_x(m, (x, -DECK_LEN + .14, .1), .07, .02, IRON, 12, 5)
    for x in (-1.6, -.55, .55, 1.6):
        lathe_x(m, [(0, -.22), (.1, -.22), (.1, .22), (0, .22)], (x, -.06, -.12), IRON, 14)
    return m.done(lambda co: (co.y, co.x))


def drum():
    """The game's drum in its own frame: X = the drum's axis (it turns about x), radius 0.55, 2.2 m long."""
    m = CMesh("C drum", DRUM)
    lathe_x(m, [(0, -1.0), (.47, -1.0), (.47, 1.0), (0, 1.0)], (0, 0, 0), WOOD, 28, uv_len=1.5)
    x = -.86
    while x < .87:
        torus_x(m, (x, 0, 0), .53, .06, ROPE, 24, 7)
        x += .124
    for e in (-1, 1):
        lathe_x(m, [(0, -.05), (.72, -.05), (.72, .05), (0, .05)], (e * 1.04, 0, 0), WALNUT, 28)
        torus_x(m, (e * 1.04, 0, 0), .72, .03, IRON, 28, 5)
        lathe_x(m, [(.4, -.03), (.62, -.03), (.62, .03), (.4, .03)], (e * 1.14, 0, 0), BRASS, 28)
        for k in range(14):
            a = 360 * k / 14
            rr = Matrix.Rotation(math.radians(a), 3, "X")
            m.obox(Vector((e * 1.14, 0, 0)) + rr @ Vector((0, 0, .66)), (.06, .07, .1), rr, BRASS)
        lathe_x(m, [(0, -.25), (.08, -.25), (.08, .25), (0, .25)], (e * 1.42, 0, 0), IRON, 12)
        for k in range(6):
            a = 360 * k / 6 + 30
            rr = Matrix.Rotation(math.radians(a), 3, "X")
            m.obox(Vector((e * 1.105, 0, 0)) + rr @ Vector((0, 0, .3)), (.03, .06, .5), rr, BRASS)
    return m.done()


# ================================================================== 8. preview stand-ins for the game's own parts (not exported)
def preview(deck_ob, drum_ob):
    p = CMesh("Preview | game parts", PREVIEW)
    water = material("Context | water", (.20, .72, .72), .1)
    for s in SIDES:
        p.box((12 * s - 9.8, 4 + MOAT0, DAIS - .95), (12 * s + 9.8, 4 + MOAT1, DAIS - .9), water)
        # Gold rims on the dais' outer edges (Arena.Rim), the dais' sides, the ramps, the team doors.
        for x0, x1 in ((-10, -9.78), (9.78, 10)):
            for z0, z1 in ((0, 12), (16, 20)):
                a, b = G(s, x0, 0, z0), G(s, x1, .1, z1)
                p.box(a, b, GOLD)
        a, b = G(s, -10, -1, 0), G(s, 10, 0, 12)
        p.box(a, (b[0], b[1], b[2] - .001), material("Context | dais", (.19, .21, .25), .9))
        a, b = G(s, -10, -1, 16), G(s, 10, 0, 20)
        p.box(a, (b[0], b[1], b[2] - .001), material("Context | dais", (.19, .21, .25), .9))
        p.box((12 * s - 2.2, 2.4, 0), (12 * s + 2.2, 4, .4), material("Context | dais", (.19, .21, .25), .9))
        p.box((12 * s - 2, 27, 0), (12 * s + 2, 27.4, 4.6), WALNUT)
        # The rope from the drum to its tail with four gold knots.
        wx = 16 * s
        p.box((wx - .045, 4 + .2, DAIS + .8 - .045), (wx + .045, 4 + WINCH, DAIS + .8 + .045), ROPE)
        for k in range(4):
            y = 4 + FIRST_KNOT - k
            p.box((wx - .15, y - .07, DAIS + .65), (wx + .15, y + .07, DAIS + .95), GOLD)
        for i in range(3):
            p.sphere((18 * s, 4 + WINCH - .16, DAIS + 1 + i * .6), .08, LAMP if i == 0 else IRON, subdiv=1)
        gc_kit.pawn(p, wx, 4 + 7.5, DAIS)
    p.box((-1.98, 16, -.6), (1.98, 20, -.5), water)
    p.done()
    # One deck lowered (white), one standing (black); a drum on each winch.
    for s, lowered in ((-1, True), (1, False)):
        d = deck_ob.copy()
        PREVIEW.objects.link(d)
        d.location = G(s, 0, 0, HINGE)
        d.rotation_euler = (0 if lowered else math.radians(-90), 0, 0)
        r = drum_ob.copy()
        PREVIEW.objects.link(r)
        r.location = G(s, 4 * s, DRUM_Y, WINCH)
        r.rotation_euler = (math.radians(random.uniform(0, 90)), 0, 0)


# ================================================================== build, save, render, export
env_parts = [paving(), dais(), moats(), bridge_towers(), winches(), facade(), walls(), corners(), central_bridge()]
deck_ob, drum_ob = deck(), drum()
preview(deck_ob, drum_ob)
deck_ob.hide_render = drum_ob.hide_render = True  # their copies in the preview stand in for them
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "C_Drawbridge_v1.blend"))
print("SAVED", os.path.join(HERE, "C_Drawbridge_v1.blend"))
for ob in list(ENV.objects) + list(DECK.objects) + list(DRUM.objects):
    print("OBJ %-22s %7d tris" % (ob.name, sum(len(p.vertices) - 2 for p in ob.data.polygons)))

if "--render" in ARGS:
    render_setup()
    sc.render.resolution_x, sc.render.resolution_y = 1536, 1024

    def shot(name, at, look, lens):
        cam = sc.camera
        cam.location = Vector(at)
        cam.rotation_euler = (Vector(look) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = lens
        cam.data.clip_end = 400
        sc.render.filepath = os.path.join(HERE, "render_c_%s.png" % name)
        bpy.ops.render.render(write_still=True)

    shot("entry", (0, -1.0, 2.4), (0, 24, 3.2), 24)
    shot("overview", (-26, -14, 24), (0, 18, 0), 30)
    shot("winch", (-11, 6.5, 3.2), (-16, 16, 1.2), 30)
    shot("bridge", (-6, 10.5, 2.6), (-12, 22, 2.4), 28)

if "--export" in ARGS:
    deck_ob.hide_render = drum_ob.hide_render = False
    import mg_export
    mg_export.export(ENV, "Assets/Resources/PawnRushMiniGames/Early/C_Drawbridge_Env.fbx")
    mg_export.export(DECK, "Assets/Resources/PawnRushMiniGames/Early/C_Drawbridge_Deck.fbx")
    mg_export.export(DRUM, "Assets/Resources/PawnRushMiniGames/Early/C_Drawbridge_Drum.fbx")
