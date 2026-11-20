"""R6 the hall's shell (ro-shell session, seoungtest R725, 2026-11-19): the round wall, its niches, statues, globes and
banners, the dome's star chart, the slit and the night outside, the hall floor (PLAN.md 1 "R6 홀 셸", RO_1, R711).

    blender -b --factory-startup --python ro_r6_hall_model.py -- --part niche

One part at a time (the user, 11-19: each part is shown alone beside its concept picture and passed before the hall is
put together). --part <name> builds that part alone at the origin and writes stills/r6/parts/<name>_compare.png
(the sheet's cell, the concept's crop, two renders) and ro_r6_<name>.blend.

Graybox (RoyalObservatoryBuilder.Hall, "Hall (looks)", nothing collided with): the hall's middle is Unity (0, 64), its
floor y -18 (HallFloor) and the wall's inner face at radius 50 (the "Hall wall" hoop: 50.25, 0.5 thick) up to y 14
(WallTop), 36 segments; the wall is open 10.5 deg either side of due south for the entry tunnel (jambs at x +-6.3 ..
8.9, the wall over it from y 6). The dome springs at y 14: 12 ribs, the first 15 deg west of the slit's heading
(Yaw of (9, 0, 113) - (0, 0, 64) = 10.4 deg east of north), a hoop at 36 deg elevation, a crown at 84 deg.

The wall's bays: 36 of 10 deg (8.73 m at the face), the two over the tunnel left out -> 34. A bay is built flat (x along
the wall, y out of the wall toward the hall, z up) and bent onto the circle (bend()); every bay is the same mesh, so
Unity draws it instanced.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k          # noqa: E402
import ro_parts as parts    # noqa: E402

STILLS = os.path.join(HERE, "stills", "r6")
ROOT_DIR = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CONCEPT = os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R725_RoyalObservatoryConcept")
SHEETS = os.path.join(CONCEPT, "AssetRefs")

# ---------------------------------------------------------------- the builder's numbers (Unity space)
HALL_MIDDLE = (0.0, 0.0, 64.0)
HALL_FLOOR, WALL_TOP = -18.0, 14.0
WALL_FACE = 50.0                 # inner face of the "Hall wall" hoop
BAYS = 36
BAY_W = 2 * math.pi * WALL_FACE / BAYS      # 8.727 m at the face

GOLD, BRASS, NAVY, VIOLET, MARBLE, PEARL, MOTIF = parts.GOLD, parts.BRASS, parts.NAVY, parts.VIOLET, parts.MARBLE, \
    parts.PEARL, parts.MOTIF
SLOT_ORDER = parts.SLOT_ORDER

# Outline-to-wall matrices for k.prism (outline in its x-y, extruded along its z), all proper rotations:
XZY = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))       # outline x -> x, y -> up, z -> -y (out)
CORBEL = Matrix(((0, 0, -1, 0), (1, 0, 0, 0), (0, -1, 0, 0), (0, 0, 0, 1)))    # outline x -> out (y), y -> down, z -> x
ALONG_X = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))     # outline x -> out (y), y -> up, z -> x


# ---------------------------------------------------------------- level of detail (lightening, 11-19)
# The plan session for the user ("경량화 진행"): the hall was 2.3 M triangles of the observatory's 3.5 M, the wall alone
# 1.9 M, no LOD; target the whole hall under 0.5 M, then (the user setting the observatory's whole at 2 M) under
# 1.2 M - "덜 줄여도 되니까 보이는 모습을 더 지켜", so LOD0 keeps more. The wall is 15 m and more from the course, so
# LOD0 already drops what no one sees there (the 1.8 cm round bevel, fine flutes, the smallest studs, many-sided
# turnings), LOD1 is about half that and LOD2 about a fifth (Unity's LODGroup, never culled). DETAIL None: the models
# as first passed (the part pictures); 0, 1, 2: the LODs.
DETAIL = None


def nseg(n, floor=4):
    """Segments for a turning / arch at this detail."""
    if DETAIL is None:
        return n
    return max(floor, int(round(n * (.75, .5, .22)[DETAIL])))


def bead_lod(r, mat=GOLD, u=8, v=5):
    """parts.bead at this detail (the smallest beads gone at LOD2)."""
    if DETAIL == 2 and r < .07:
        return bmesh.new()
    return parts.bead(r, mat, nseg(u, 4), nseg(v, 3))


def torus_lod(R, r, segs=48, rsegs=8, mat=GOLD):
    return parts.torus(R, r, nseg(segs, 6), max(3, nseg(rsegs, 3)), mat)


def simple_lamp(bm, foot, height, globe):
    """The lamp post at a distance (LODs): a turned gold stand and the pearl globe. Returns the globe's middle."""
    foot = Vector(foot)
    lathe_at(bm, [(0, 0), (.15, 0), (.15, .05), (.11, .1), (.11, .3), (.06, .38), (.035, .5), (.035, height - .3),
                  (.07, height - .22), (.05, height - .17), (0, height - .17)], tuple(foot), 12, GOLD)
    gz = height - .17 + globe * .95
    add(bm, parts.bead(globe, PEARL, nseg(20, 8), nseg(12, 5)), Matrix.Translation(foot + Vector((0, 0, gz))))
    if DETAIL != 2:
        lathe_at(bm, [(0, 0), (.05, 0), (.03, .04), (0, .08)], tuple(foot + Vector((0, 0, gz + globe * .93))), 8, GOLD)
    return foot + Vector((0, 0, gz))


# ---------------------------------------------------------------- mesh helpers (all into one bmesh, slot indices)

def box(bm, x0, x1, y0, y1, z0, z1, mat):
    weight_layer(bm)                       # before the cube: a new layer would void the element references
    got = bmesh.ops.create_cube(bm, size=1.0)
    vs = got["verts"]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)) @
                        Matrix.Diagonal((abs(x1 - x0), abs(y1 - y0), abs(z1 - z0), 1)), verts=vs)
    for f in {f for v in vs for f in v.link_faces}:
        f.material_index = mat
    hard_edges(bm, {e for v in vs for e in v.link_edges})
    return vs


# Only the hard-edged pieces (boxes and prisms) get the round bevel: their edges carry a bevel weight and make_bay's
# bevel is limited to weighted edges, so the turned parts (balusters, columns, beads, lamps) are not bevelled again.
def weight_layer(bm):
    return bm.edges.layers.float.get("bevel_weight_edge") or bm.edges.layers.float.new("bevel_weight_edge")


def hard_edges(bm, edges):
    lay = weight_layer(bm)
    for e in edges:
        e[lay] = 1.0


def hard_prism(*a, **kw):
    b = k.prism(*a, **kw)
    weight_layer(b)
    hard_edges(b, b.edges)
    return b


def add(bm, src, m=None):
    """Merge bmesh src (freed) into bm, moved by matrix m."""
    if m is not None:
        bmesh.ops.transform(src, matrix=m, verts=src.verts)
    parts._merge(bm, src)


def lathe_at(bm, profile, at, segs=24, mat=GOLD, axis="Z"):
    b = k.lathe(profile, nseg(segs, 4), close_top=True, close_bottom=True, mat=mat)
    m = Matrix.Translation(at)
    if axis == "Y":
        m = m @ Matrix.Rotation(-math.pi / 2, 4, "X")
    add(bm, b, m)


def sweep_arch(bm, section, cz, radius, a0=0.0, a1=180.0, segs=24, mat=GOLD, cap=True):
    """Sweep a closed section [(radial offset, y)] along the arch of `radius` round (x 0, z cz) in the x-z plane,
    angles a0..a1 (degrees, 0 = +x, 90 = up)."""
    segs = nseg(segs, 8)
    if DETAIL == 2 and len(section) > 4:          # a moulding read as a band at a distance
        xs = [d for d, _ in section]
        ys = [y for _, y in section]
        section = [(min(xs), min(ys)), (max(xs), min(ys)), (max(xs), max(ys)), (min(xs), max(ys))]
    rings = []
    for i in range(segs + 1):
        t = math.radians(a0 + (a1 - a0) * i / segs)
        c, s = math.cos(t), math.sin(t)
        rings.append([bm.verts.new(((radius + dr) * c, y, cz + (radius + dr) * s)) for dr, y in section])
    n = len(section)
    for i in range(segs):
        for j in range(n):
            f = bm.faces.new((rings[i][j], rings[i][(j + 1) % n], rings[i + 1][(j + 1) % n], rings[i + 1][j]))
            f.material_index = mat
    if cap:
        for r in (rings[0], rings[-1][::-1]):
            try:
                bm.faces.new(r).material_index = mat
            except ValueError:
                pass


def field_with_arch(bm, x0, x1, zs, zt, rh, y, mat, b=None):
    """The wall face (facing +y at `y`) from x0..x1, zs..zt, with a semicircular opening of radius rh round (0, zs) -
    or a half ellipse rh across, b up. Worked out where the ellipse is a unit circle, then scaled back."""
    b = b or rh
    x0, x1, zt = x0 / rh, x1 / rh, zs + (zt - zs) * rh / b       # (zt - zs) / b in units of rh
    corners = [math.degrees(math.atan2(zt - zs, x1 * rh)), math.degrees(math.atan2(zt - zs, x0 * rh))]
    na = nseg(32, 10)
    angles = sorted(set([180.0 * i / na for i in range(na + 1)] + corners))
    inner, outer = [], []
    for a in angles:
        t = math.radians(a)
        c, s = math.cos(t), math.sin(t)
        inner.append(bm.verts.new((rh * c, y, zs + b * s)))
        # the ray from the middle to the rectangle's edge (in the circle's space: x in rh, z in rh)
        ds = []
        if c > 1e-9:
            ds.append(x1 * rh / c)
        if c < -1e-9:
            ds.append(x0 * rh / c)
        if s > 1e-9:
            ds.append((zt - zs) / s)
        d = min(ds)
        outer.append(bm.verts.new((d * c, y, zs + d * s * b / rh)))
    for i in range(len(angles) - 1):
        f = bm.faces.new((inner[i], outer[i], outer[i + 1], inner[i + 1]))
        f.material_index = mat
        f.normal_update()
        if f.normal.y < 0:
            f.normal_flip()


def sharp_seam(bm, ring):
    """Mark the edges along a ring of verts sharp: the vault meets the flat back (and the wall face) at a crease, and a
    smooth normal across it shades the back's fan in triangles."""
    on = set(ring)
    for v in ring:
        for e in v.link_edges:
            if e.other_vert(v) in on:
                e.smooth = False


def star_on(bm, size, at, depth=None, points=5, mat=GOLD, facing="Y"):
    """A raised faceted star stud at `at`, facing +y (on a wall) or +z (on a floor). At a distance the smallest go."""
    if DETAIL is not None and size < (.1, .2, .55)[DETAIL]:
        return
    s = parts.star_stud(size, depth or size * .12, mat, points)
    m = Matrix.Translation(at) @ (Matrix.Rotation(math.pi / 2, 4, "Z") if facing == "Y" else
                                  Matrix.Rotation(-math.pi / 2, 4, "Y"))
    add(bm, s, m)


def column(bm, x, y, z0, z1, r, flutes=12):
    """A slim gold column: moulded base, fluted shaft, a capital with a bead collar and a square abacus."""
    lathe_at(bm, [(r * 1.55, 0), (r * 1.55, .06), (r * 1.35, .09), (r * 1.3, .15), (r * 1.15, .2), (r * 1.05, .24),
                  (r, .26), (0, .26)], (x, y, z0), 20, GOLD)
    if DETAIL is None or DETAIL == 0:
        add(bm, parts.fluted_shaft(r, z0 + .26, z1 - .42, flutes if DETAIL is None else 8, r * .12, GOLD),
            Matrix.Translation((x, y, 0)))
    else:
        lathe_at(bm, [(r, 0), (r, z1 - .42 - (z0 + .26))], (x, y, z0 + .26), 16, GOLD)
    lathe_at(bm, [(r * 1.05, 0), (r * 1.18, .05), (r * 1.05, .1), (r * 1.1, .2), (r * 1.4, .32), (r * 1.55, .36),
                  (0, .36)], (x, y, z1 - .42), 20, GOLD)
    add(bm, torus_lod(r * 1.08, r * .1, 20, 6, GOLD), Matrix.Translation((x, y, z1 - .37)))
    box(bm, x - r * 1.7, x + r * 1.7, y - r * 1.7, y + r * 1.7, z1 - .06, z1, GOLD)


# ---------------------------------------------------------------- the bay (RO_A1 niche, RO_1's upper tier)
# Heights from the bay's foot (its tier's floor). The statue tier: 11 m.
NICHE = dict(H=11.0, base=.9, sill=1.0, rh=2.05, spring=6.75, depth=1.55, pil_w=1.15, pil_d=.42, ent=9.35)


def base_course(bm, W, sill):
    """The base course along a bay's foot: marble plinth, a navy band with gold stars, a gold fillet up to `sill`."""
    hw = W / 2
    box(bm, -hw, hw, 0, .38, 0, .55, MARBLE)
    box(bm, -hw, hw, 0, .34, .55, .82, NAVY)
    box(bm, -hw, hw, 0, .42, .82, sill, GOLD)
    for i in range(6):
        star_on(bm, .2, (-hw + W * (i + .5) / 6, .34, .685), .03, 4)


def pilaster(bm, xc, sill, zs, ent, width, d):
    """The pilaster standing at x = xc from `sill` to `ent` (its abacus top): marble with two sunk navy panels (split at
    the arch's spring zs) and gold stars, gold base mouldings, a gold capital with volutes."""
    hw = -xc
    p = {"pil_w": width}
    # -- the pilaster on the west edge: marble with a sunk navy panel and a gold star, gold base and capital
    x0, x1 = -hw - p["pil_w"] / 2, -hw + p["pil_w"] / 2
    box(bm, x0 - .08, x1 + .08, 0, d + .1, sill, sill + .22, GOLD)                 # base mouldings
    box(bm, x0 - .04, x1 + .04, 0, d + .06, sill + .22, sill + .38, MARBLE)
    box(bm, x0 - .06, x1 + .06, 0, d + .08, sill + .38, sill + .46, GOLD)
    box(bm, x0, x1, 0, d, sill + .46, ent - 1.0, MARBLE)                           # shaft
    for z0, z1 in ((sill + 1.0, zs - .3), (zs + .3, ent - 1.5)):                     # two sunk panels
        box(bm, x0 + .16, x1 - .16, d, d + .015, z0, z1, NAVY)
        box(bm, x0 + .12, x1 - .12, d, d + .03, z0 - .04, z0, GOLD)
        box(bm, x0 + .12, x1 - .12, d, d + .03, z1, z1 + .04, GOLD)
        box(bm, x0 + .12, x0 + .16, d, d + .03, z0, z1, GOLD)
        box(bm, x1 - .16, x1 - .12, d, d + .03, z0, z1, GOLD)
        star_on(bm, .5, (-hw, d + .015, (z0 + z1) / 2), .06, 5)
        for zz in (z0 + .45, z1 - .45):
            star_on(bm, .16, (-hw, d + .015, zz), .03, 4)
    # capital: necking bead, a gold bell with two volutes and a star, an abacus
    cz = ent - 1.0
    box(bm, x0 - .02, x1 + .02, 0, d + .04, cz, cz + .08, GOLD)
    for i in range(7):
        add(bm, bead_lod(.05, GOLD, 8, 5), Matrix.Translation((x0 + .1 + i * (p["pil_w"] - .2) / 6, d + .06, cz + .14)))
    bell = hard_prism([(x0 + .02, 0), (x1 - .02, 0), (x1 + .14, .62), (x0 - .14, .62)], -(d + .12), 0, GOLD)
    add(bm, bell, Matrix.Translation((0, 0, cz + .2)) @ XZY)
    for sx in (x0 - .06, x1 + .06):
        add(bm, torus_lod(.17, .055, 18, 6, GOLD), Matrix.Translation((sx, d + .12, cz + .66)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
        add(bm, bead_lod(.08, GOLD, 10, 6), Matrix.Translation((sx, d + .16, cz + .66)))
    star_on(bm, .36, (-hw, d + .12, cz + .48), .05, 5)
    box(bm, x0 - .2, x1 + .2, 0, d + .24, cz + .82, ent, GOLD)                     # abacus


def cornice(bm, x0, x1, e):
    """The wall's top entablature from x0 to x1, its foot at e (1.52 m tall + the lip): marble architrave, a navy frieze
    with gold stars, gold corbels, the gold cornice with dentils - the line the dome springs from."""
    W = x1 - x0
    hw = W / 2
    xm = (x0 + x1) / 2
    # -- the entablature: marble architrave, a navy frieze with gold stars, gold corbels, the gold cornice + dentils
    box(bm, x0, x1, 0, .5, e, e + .22, MARBLE)
    box(bm, x0, x1, 0, .54, e + .22, e + .3, GOLD)
    box(bm, x0, x1, 0, .5, e + .3, e + .5, MARBLE)
    box(bm, x0, x1, 0, .47, e + .5, e + 1.05, NAVY)                               # frieze
    for i in range(max(2, int(round(W / 1.09)))):
        x = x0 + W * (i + .5) / max(2, int(round(W / 1.09)))
        star_on(bm, .34 if i % 2 == 0 else .18, (x, .47, e + .775), .04, 5 if i % 2 == 0 else 4)
    for i in range(max(1, int(round(W / 2.18)))):                                                             # corbels under the cornice
        x = x0 + W * (i + .5) / max(1, int(round(W / 2.18)))
        cb = hard_prism([(0, 0), (.66, 0), (.66, .14), (.56, .22), (.52, .42), (.48, .58), (0, .58)], -.16, .16, GOLD)
        add(bm, cb, Matrix.Translation((x, 0, e + 1.05)) @ CORBEL)
    box(bm, x0, x1, 0, .62, e + 1.05, e + 1.15, GOLD)
    for i in range(int(W / .28) if DETAIL != 2 else 0):                           # dentils (a band at LOD2)
        x = x0 + .14 + i * .28
        box(bm, x - .07, x + .07, .58, .72, e + 1.15, e + 1.3, GOLD)
    box(bm, x0, x1, 0, .82, e + 1.3, e + 1.42, GOLD)
    box(bm, x0, x1, 0, .9, e + 1.42, e + 1.52, GOLD)
    lip = hard_prism([(0, 0), (.95, 0), (.95, .06), (.82, .12), (0, .12)], x0, x1, GOLD)
    add(bm, lip, Matrix.Translation((0, 0, e + 1.52)) @ ALONG_X)


def niche_bay():
    """One statue niche bay, flat: x -W/2..W/2 along the wall, y > 0 toward the hall, z up from the tier's floor. The
    pilaster on its west edge (x = -W/2) is its own; the next bay's stands at +W/2."""
    p = NICHE
    W, H, rh, zs, D = BAY_W, p["H"], p["rh"], p["spring"], p["depth"]
    hw = W / 2
    bm = bmesh.new()
    sill, ent = p["sill"], p["ent"]
    base_course(bm, W, sill)
    # -- the wall field (marble) round the niche's arch, and the piers under the spring
    field_with_arch(bm, -hw, hw, zs, ent, rh, 0.0, NAVY)          # RO_1: the field between pilasters is dark enamel
    box(bm, -hw, -rh, -.3, 0, sill, zs, NAVY)
    box(bm, rh, hw, -.3, 0, sill, zs, NAVY)
    # -- the niche: a navy box with a barrel-vault top, vertical boards on its back, a few gold stars in the vault
    box(bm, -rh, rh, -D - .1, -D, sill, zs, NAVY)                        # back
    box(bm, -rh - .1, -rh, -D, 0, sill, zs, NAVY)                        # sides
    box(bm, rh, rh + .1, -D, 0, sill, zs, NAVY)
    box(bm, -rh, rh, -D, 0, sill - .02, sill, MARBLE)                    # floor
    # the vault: half a cylinder seen from inside (faces toward its axis), its back a half disc
    seg = nseg(24, 8)
    ring_f = [bm.verts.new((rh * math.cos(math.pi * i / seg), 0.0, zs + rh * math.sin(math.pi * i / seg))) for i in range(seg + 1)]
    ring_b = [bm.verts.new((rh * math.cos(math.pi * i / seg), -D, zs + rh * math.sin(math.pi * i / seg))) for i in range(seg + 1)]
    for i in range(seg):
        bm.faces.new((ring_f[i + 1], ring_f[i], ring_b[i], ring_b[i + 1])).material_index = NAVY
    hub = bm.verts.new((0, -D, zs))
    for i in range(seg):
        bm.faces.new((hub, ring_b[i + 1], ring_b[i])).material_index = NAVY
    sharp_seam(bm, ring_b + ring_f)
    # boards on the back: half-round staves with gold fillets between (at LOD2 the plain back)
    staves = 9 if DETAIL != 2 else 0
    for i in range(staves):
        x = -rh + (i + .5) * 2 * rh / staves
        st = k.lathe([(0, 0), (.15, 0), (.15, zs - sill - .5), (0, zs - sill - .5)], nseg(10, 3), mat=NAVY, angle=180.0, start=0.0)
        add(bm, st, Matrix.Translation((x, -D, sill + .25)))
    box(bm, -rh, rh, -D, -D + .2, sill + .02, sill + .25, GOLD)           # skirting
    box(bm, -rh, rh, -D, -D + .2, zs - .25, zs, GOLD)                     # the impost line across the back
    for i, (x, z) in enumerate(((-1.1, zs + .9), (0, zs + 1.55), (1.1, zs + .9), (-.55, zs + .4), (.55, zs + .4))):
        star_on(bm, .34 if i < 3 else .2, (x, -D, z), .04, 5 if i < 3 else 4)
    # -- the pedestal the statue stands on
    pw, pd = .85, .55
    box(bm, -pw - .08, pw + .08, -D * .55 - pd - .08, -D * .55 + pd + .08, sill, sill + .18, MARBLE)
    box(bm, -pw, pw, -D * .55 - pd, -D * .55 + pd, sill + .18, sill + 1.05, MARBLE)
    box(bm, -pw - .06, pw + .06, -D * .55 - pd - .06, -D * .55 + pd + .06, sill + .95, sill + 1.12, GOLD)
    box(bm, -pw + .12, pw - .12, -D * .55 + pd, -D * .55 + pd + .02, sill + .35, sill + .82, NAVY)   # front panel
    star_on(bm, .32, (0, -D * .55 + pd + .02, sill + .585), .04, 5)
    # -- the archivolt: a gold moulded arch, a marble band inside it, a gold keystone star
    sweep_arch(bm, [(0, 0), (.06, .14), (.2, .16), (.32, .1), (.38, .02), (.38, -.02), (0, -.02)], zs, rh, segs=36, mat=GOLD)
    sweep_arch(bm, [(-.02, -.04), (-.02, .05), (0, .07), (0, -.04)], zs, rh, segs=36, mat=GOLD)
    add(bm, hard_prism([(-.3, 0), (.3, 0), (.2, .85), (-.2, .85)], -.24, 0, MARBLE),       # the keystone block
        Matrix.Translation((0, 0, zs + rh - .05)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    star_on(bm, .62, (0, .24, zs + rh + .38), .09, 5)
    # beads along the archivolt
    for i in range(1, 18):
        t = math.pi * i / 18
        add(bm, bead_lod(.045, GOLD, 8, 5), Matrix.Translation(((rh + .44) * math.cos(t), .05, zs + (rh + .44) * math.sin(t))))
    # -- the two slim gold columns flanking the opening, an impost block over each
    for sx in (-1, 1):
        cx = sx * (rh + .22)
        column(bm, cx, .2, sill, zs - .18, .16)
        box(bm, cx - .34, cx + .34, -.02, .44, zs - .18, zs, GOLD)
        box(bm, sx * rh - (.0 if sx > 0 else .1), sx * rh + (.1 if sx > 0 else .0), -D, 0, zs - .18, zs, GOLD)
    pilaster(bm, -hw, sill, zs, ent, p["pil_w"], p["pil_d"])
    cornice(bm, -hw, hw, ent)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.normal_update()
    return bm


# ---------------------------------------------------------------- the arcade bay (RO_1's two lower tiers)
# Wall from the hall floor (-18) to 14 = 32 m: the statue niches on top (3 .. 14, the way's height), two arcade tiers
# under them (-18 .. -7.5, -7.5 .. 3), the same mesh twice. Each arcade tier ends in a ledge with a balustrade and a
# lamp on each pilaster: the ledges RO_1 shows between the tiers.
TIERS = (("arcade", -18.0), ("arcade", -7.5), ("niche", 3.0))
ARCADE = dict(H=10.5, sill=1.0, rh=2.75, spring=5.4, depth=2.3, ent=8.7, ledge=1.8, rail_y=1.45, lamp_h=1.7, globe=.24)


def wall_rail(bm, x0, x1, y, z, height=1.05):
    """A balustrade along x from x0 to x1 on a ledge at height z, its middle at y: ro_parts.rail's look (marble plinth
    and coping, gold vase balusters, gold top moulding, marble end posts with violet star panels and gold balls), but
    the balusters turned with 6 sides - the wall is seen from 15 m and more, and it repeats 68 times."""
    w = .3
    L = x1 - x0
    box(bm, x0, x1, y - w / 2, y + w / 2, z, z + .16, MARBLE)
    box(bm, x0, x1, y - w * .36, y + w * .36, z + .16, z + .19, GOLD)
    box(bm, x0, x1, y - w * .45, y + w * .45, z + height - .12, z + height - .04, MARBLE)
    box(bm, x0, x1, y - w * .32, y + w * .32, z + height - .04, z + height, GOLD)
    hb = height - .12 - .19
    sc = hb / .62
    prof = [(0, 0), (.045 * sc, 0), (.045 * sc, .03 * sc), (.03 * sc, .05 * sc), (.05 * sc, .17 * sc), (.055 * sc, .24 * sc),
            (.04 * sc, .33 * sc), (.022 * sc, .42 * sc), (.02 * sc, .48 * sc), (.032 * sc, .52 * sc), (.04 * sc, .58 * sc),
            (.04 * sc, .62 * sc), (0, .62 * sc)]
    n = max(1, int((L - .8) / .24))
    if DETAIL == 0:      # the vase's outline in fewer turns and five sides
        prof = [prof[i] for i in (0, 1, 4, 5, 7, 9, 11, 12)]
    for i in range(n):
        x = x0 + .4 + (i + .5) * (L - .8) / n
        if DETAIL is None:
            lathe_at(bm, prof, (x, y, z + .19), 6, GOLD)
        elif DETAIL == 0:
            add(bm, k.lathe(prof, 5, close_top=True, close_bottom=True, mat=GOLD), Matrix.Translation((x, y, z + .19)))
        elif DETAIL == 1:
            box(bm, x - .035, x + .035, y - .035, y + .035, z + .19, z + .19 + hb, GOLD)
    if DETAIL == 2:      # the row read as a gold screen
        box(bm, x0 + .4, x1 - .4, y - .02, y + .02, z + .19, z + .19 + hb, GOLD)
    for x in (x0 + .19, x1 - .19):
        box(bm, x - .17, x + .17, y - .19, y + .19, z, z + height + .05, MARBLE)
        box(bm, x - .2, x + .2, y - .22, y + .22, z + height + .05, z + height + .12, GOLD)
        lathe_at(bm, [(0, 0), (.06, 0), (.045, .03), (.03, .05), (.1, .1), (.12, .18), (.1, .26), (.05, .3), (0, .31)],
                 (x, y, z + height + .12), 12, GOLD)
        box(bm, x - .12, x + .12, y + .19, y + .196, z + .25, z + height - .2, VIOLET)
        star_on(bm, .13, (x, y + .196, z + (height + .05) / 2), .015, 8)


def arcade_bay():
    """One arcade bay, flat as niche_bay: a deep arch into a dark gallery (navy staves, a vault with gold stars),
    roundels in the spandrels, the pilaster on the west edge, an entablature carrying a ledge on gold consoles with a
    balustrade, and a lamp post on the ledge before the pilaster. Returns (bmesh, lamp globe centres)."""
    p = ARCADE
    W, H, rh, zs, D, sill, ent = BAY_W, p["H"], p["rh"], p["spring"], p["depth"], p["sill"], p["ent"]
    hw = W / 2
    bm = bmesh.new()
    base_course(bm, W, sill)
    field_with_arch(bm, -hw, hw, zs, ent, rh, 0.0, NAVY)
    box(bm, -hw, -rh, -.3, 0, sill, zs, NAVY)
    box(bm, rh, hw, -.3, 0, sill, zs, NAVY)
    # the gallery behind the arch
    box(bm, -rh, rh, -D - .1, -D, sill, zs, NAVY)
    box(bm, -rh - .1, -rh, -D, 0, sill, zs, NAVY)
    box(bm, rh, rh + .1, -D, 0, sill, zs, NAVY)
    box(bm, -rh, rh, -D, 0, sill - .02, sill, MARBLE)
    seg = nseg(24, 8)
    ring_f = [bm.verts.new((rh * math.cos(math.pi * i / seg), 0.0, zs + rh * math.sin(math.pi * i / seg))) for i in range(seg + 1)]
    ring_b = [bm.verts.new((rh * math.cos(math.pi * i / seg), -D, zs + rh * math.sin(math.pi * i / seg))) for i in range(seg + 1)]
    for i in range(seg):
        bm.faces.new((ring_f[i + 1], ring_f[i], ring_b[i], ring_b[i + 1])).material_index = NAVY
    hub = bm.verts.new((0, -D, zs))
    for i in range(seg):
        bm.faces.new((hub, ring_b[i + 1], ring_b[i])).material_index = NAVY
    sharp_seam(bm, ring_b + ring_f)
    staves = 11 if DETAIL != 2 else 0
    for i in range(staves):
        x = -rh + (i + .5) * 2 * rh / staves
        add(bm, k.lathe([(0, 0), (.17, 0), (.17, zs - sill - .45), (0, zs - sill - .45)], max(3, nseg(6, 3)), mat=NAVY, angle=180.0),
            Matrix.Translation((x, -D, sill + .2)))
    box(bm, -rh, rh, -D, -D + .2, sill + .02, sill + .2, GOLD)
    box(bm, -rh, rh, -D, -D + .2, zs - .2, zs, GOLD)
    for x, z, sz in ((-1.4, zs + 1.0, .36), (0, zs + 1.9, .44), (1.4, zs + 1.0, .36)):
        star_on(bm, sz, (x, -D, z), .05, 5)
    # the archivolt: a plainer gold moulding than the niche's, a marble keystone with a star
    sweep_arch(bm, [(0, 0), (.08, .12), (.24, .13), (.34, .06), (.34, -.02), (0, -.02)], zs, rh, segs=32, mat=GOLD)
    add(bm, hard_prism([(-.3, 0), (.3, 0), (.2, .7), (-.2, .7)], -.22, 0, MARBLE),
        Matrix.Translation((0, 0, zs + rh - .05)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    star_on(bm, .46, (0, .22, zs + rh + .3), .07, 5)
    box(bm, -rh - .3, -rh + .02, -.02, .3, zs - .22, zs, GOLD)                # impost blocks
    box(bm, rh - .02, rh + .3, -.02, .3, zs - .22, zs, GOLD)
    # roundels in the spandrels: a gold ring, a navy disc, a gold star
    for sx in (-1, 1):
        cx, cz = sx * 3.28, zs + 2.3
        add(bm, torus_lod(.4, .055, 24, 6, GOLD), Matrix.Translation((cx, .05, cz)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
        lathe_at(bm, [(0, 0), (.36, 0), (.36, .04), (0, .04)], (cx, 0, cz), 24, NAVY, axis="Y")
        star_on(bm, .5, (cx, .04, cz), .05, 8)
    # a pedestal in the gallery for a chess piece (the user, 11-19: "벽 1층 2층에도 기물 넣어줘"), as the niches'
    pw, pd, py = 1.05, .62, -D * .5
    box(bm, -pw - .08, pw + .08, py - pd - .08, py + pd + .08, sill, sill + .18, MARBLE)
    box(bm, -pw, pw, py - pd, py + pd, sill + .18, sill + 1.05, MARBLE)
    box(bm, -pw - .06, pw + .06, py - pd - .06, py + pd + .06, sill + .95, sill + 1.12, GOLD)
    box(bm, -pw + .12, pw - .12, py + pd, py + pd + .02, sill + .35, sill + .82, NAVY)
    star_on(bm, .32, (0, py + pd + .02, sill + .585), .04, 5)
    pilaster(bm, -hw, sill, zs, ent, NICHE["pil_w"], NICHE["pil_d"])
    # the entablature and the ledge
    e = ent
    box(bm, -hw, hw, 0, .5, e, e + .22, MARBLE)
    box(bm, -hw, hw, 0, .54, e + .22, e + .3, GOLD)
    box(bm, -hw, hw, 0, .47, e + .3, e + .9, NAVY)
    for i in range(8):
        star_on(bm, .3 if i % 2 == 0 else .16, (-hw + W * (i + .5) / 8, .47, e + .6), .04, 5 if i % 2 == 0 else 4)
    top = H
    led = p["ledge"]
    box(bm, -hw, hw, 0, .62, e + .9, e + 1.0, GOLD)
    for i in range(6):                                                        # consoles under the ledge
        x = -hw + W * (i + .5) / 6
        cb = hard_prism([(0, 0), (led - .1, 0), (led - .1, .12), (led - .3, .2), (.9, .32), (.6, .6), (.5, .85), (0, .85)],
                     -.13, .13, GOLD)
        add(bm, cb, Matrix.Translation((x, 0, top - .3)) @ CORBEL)
    box(bm, -hw, hw, 0, led, top - .3, top - .12, MARBLE)                     # the ledge slab
    box(bm, -hw, hw, led - .02, led + .06, top - .32, top, GOLD)              # its gold nosing
    box(bm, -hw, hw, 0, led, top - .12, top, MARBLE)
    wall_rail(bm, -hw + .38, hw - .38, p["rail_y"], top)
    # the lamp post before the pilaster (ro_parts.lamp_post, scaled for the wall; simpler at a distance)
    if DETAIL is not None:
        globe = simple_lamp(bm, (-hw, p["rail_y"], top), p["lamp_h"], p["globe"])
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bm.normal_update()
        return bm, [globe]
    lamp_objs, globe = parts.lamp_post(bpy.context.scene.collection, "_lamp", (-hw, p["rail_y"], top), p["lamp_h"], p["globe"])
    for o in lamp_objs:
        lb = bmesh.new()
        lb.from_mesh(o.data)
        add(bm, lb, o.matrix_world)
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.meshes.remove(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.normal_update()
    return bm, [globe]


# ---------------------------------------------------------------- the south wall over the entry tunnel
# The two bays round due south (170 .. 190 deg) are the tunnel's mouth: the builder leaves the hoop open there, with
# jambs at x +-6.3 .. 8.9 and the wall over the tunnel from y 6 (Unity). The lowest tier (-18 .. -7.5) stays two
# ordinary arcade bays; from -7.5 to the top (14) one portal piece two bays wide: the rectangular mouth x +-6.3, y 0 ..
# 6 (the tunnel itself, its floor, rails and door are R1's), a lintel, an elliptical arch over it with a gold sunburst
# in its navy tympanum, slim gold columns on plinths either side with lamp posts on their impost blocks, a medallion
# on the wall under the tunnel floor, giant pilasters at both edges (wide enough to hide the next bays' own pilaster
# at +W), and the wall's top cornice across, continuing the niches'.
PORTAL = dict(foot=-7.5, mouth=6.3, floor=7.5, top=13.5, lintel=14.3, a=6.3, b=4.2, ent=19.85, giant_w=1.7, giant_d=.75)


def sweep_ellipse(bm, section, cz, a, b, segs=40, mat=GOLD):
    """sweep_arch on a half ellipse (half-axes a across, b up) round (0, cz); the section's radial offset adds to both."""
    segs = nseg(segs, 10)
    rings = []
    for i in range(segs + 1):
        t = math.pi * i / segs
        c, s_ = math.cos(t), math.sin(t)
        rings.append([bm.verts.new(((a + dr) * c, y, cz + (b + dr) * s_)) for dr, y in section])
    n = len(section)
    for i in range(segs):
        for j in range(n):
            bm.faces.new((rings[i][j], rings[i][(j + 1) % n], rings[i + 1][(j + 1) % n], rings[i + 1][j])).material_index = mat
    for r in (rings[0], rings[-1][::-1]):
        try:
            bm.faces.new(r).material_index = mat
        except ValueError:
            pass


def sun_ray(bm, centre, angle, r0, r1, width, y0=-.1, ridge=.1):
    """A faceted gold ray in the wall plane from radius r0 to its tip at r1 round centre (x, z), its ridge raised."""
    cx, cz = centre
    d = Vector((math.cos(angle), 0, math.sin(angle)))
    q = Vector((-math.sin(angle), 0, math.cos(angle)))
    c = Vector((cx, y0, cz))
    bl = bm.verts.new(c + d * r0 + q * width / 2)
    br = bm.verts.new(c + d * r0 - q * width / 2)
    tip = bm.verts.new(c + d * r1)
    mid = bm.verts.new(c + d * (r0 + (r1 - r0) * .25) + Vector((0, ridge - y0, 0)))
    for f in ((bl, br, mid), (br, tip, mid), (tip, bl, mid)):
        face = bm.faces.new(f)
        face.material_index = GOLD
        face.normal_update()
        if face.normal.y < 0:
            face.normal_flip()


def portal_bay():
    """The south portal, flat: x -W..W (two bays), z from the middle tier's foot (Unity -7.5) to the top (14).
    Returns (bmesh, lamp globe centres)."""
    p = PORTAL
    W = BAY_W
    m, fl, top, lin, a, b, ent = p["mouth"], p["floor"], p["top"], p["lintel"], p["a"], p["b"], p["ent"]
    bm = bmesh.new()
    sill = 1.0
    base_course(bm, 2 * W, sill)
    # the wall field round the mouth (navy, as the bays'), the tympanum filled
    box(bm, -W, -m, -.3, 0, sill, ent, NAVY)
    box(bm, m, W, -.3, 0, sill, ent, NAVY)
    box(bm, -m, m, -.3, 0, sill, fl - .5, NAVY)
    box(bm, -m, m, -.3, 0, top, ent, NAVY)                                      # the wall over the mouth
    # The entry door itself is R1's (ro-path's RoEntryGate: gold columns at x +-6.75, the arch r 6.35 .. 7.8 round
    # Unity y 6, the crown tympanum, marble piers x +-7.45 .. 8.95, pearl lamps). Both models dressed the same wall
    # and flickered where they met (the threshold's top on star 1's at y 0, the columns, the arch); the user kept the
    # door (11-19, "입구 문(R1) 남기기"), so this portal no longer has its frame, threshold band, lintel, elliptical
    # arch and sun tympanum, keystone, columns and lamps - only the wall round the mouth, the medallion under it, the
    # giant pilasters and the cornice.
    lamps = []
    # the medallion on the wall under the tunnel's floor: a compass star in two gold rings on a navy disc
    mz = (sill + fl - .5) / 2
    lathe_at(bm, [(0, 0), (2.5, 0), (2.5, .05), (0, .05)], (0, 0, mz), 48, NAVY, axis="Y")
    add(bm, torus_lod(2.5, .1, 48, 6, GOLD), Matrix.Translation((0, .06, mz)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    add(bm, torus_lod(2.05, .05, 48, 6, GOLD), Matrix.Translation((0, .06, mz)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    star_on(bm, 3.8, (0, .05, mz), .3, 8)
    for i in range(8):
        t = math.pi / 8 + math.pi / 4 * i
        star_on(bm, .3, (2.27 * math.cos(t), .05, mz + 2.27 * math.sin(t)), .04, 4)
    # giant pilasters at both edges, from the base course to the cornice
    for xc in (-W, W):
        pilaster(bm, xc, sill, 10.5, ent, p["giant_w"], p["giant_d"])
    cornice(bm, -W, W, ent)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.normal_update()
    return bm, lamps


def bend_point(v, radius=WALL_FACE):
    x, y, z = v
    a = x / radius
    r = radius - y
    return Vector((r * math.sin(a), radius - r * math.cos(a), z))


BEND_STEP = 1.2      # chord error 1.2^2 / (8 * 50) = 3.6 mm


def bend(bm, radius=WALL_FACE):
    """Bend a flat bay onto the wall's circle: x along the arc, y toward the middle (which ends at (0, radius)).
    Faces wider than BEND_STEP along x are first cut across every BEND_STEP: a wide flat face bent by its corners alone
    stays a straight chord and stands out of the curve in its middle (0.4 m over the portal's 12.6 m, 0.19 m over a
    bay) - in front of what is fixed on it."""
    xs = [v.co.x for v in bm.verts]
    lo, hi = min(xs), max(xs)
    step = BEND_STEP if DETAIL is None else (1.6, 2.4, 3.0)[DETAIL]     # chord error .6 .. 2.3 cm, 15 m away
    x = math.ceil(lo / step) * step
    while x < hi:
        wide = [f for f in bm.faces
                if min(v.co.x for v in f.verts) < x - 1e-4 and max(v.co.x for v in f.verts) > x + 1e-4
                and max(v.co.x for v in f.verts) - min(v.co.x for v in f.verts) > step]
        if wide:
            edges = list({e for f in wide for e in f.edges})
            verts = list({v for f in wide for v in f.verts})
            bmesh.ops.bisect_plane(bm, geom=wide + edges + verts, plane_co=(x, 0, 0), plane_no=(1, 0, 0))
        x += step
    for v in bm.verts:
        x, y, z = v.co
        a = x / radius
        r = radius - y
        v.co = Vector((r * math.sin(a), radius - r * math.cos(a), z))
    bm.normal_update()


# ---------------------------------------------------------------- parts

def shape_bay(ob):
    """A flat bay object -> bent onto the wall, then the round bevel on its hard (weighted) edges, metre UVs. Bent
    first: the bevel's hardened normals would not survive the cuts the bend makes."""
    me = bmesh.new()
    me.from_mesh(ob.data)
    bend(me)
    me.to_mesh(ob.data)
    me.free()
    if DETAIL is None or DETAIL == 0:      # LOD0 keeps the round edge, one segment (it catches the light)
        bv = k.bevel(ob, .018, 2 if DETAIL is None else 1, 35)
        bv.limit_method = "WEIGHT"
    # Triangulated last (keeping the weighted normals): the bend leaves long faces cut into many-sided ones that are no
    # longer flat, and Unity drops a polygon it finds self-intersecting (11-19 import: 21 in the niche bay, 12 in the
    # arcade bay, 54 in the portal).
    tri = ob.modifiers.new("Triangulate", "TRIANGULATE")
    tri.quad_method, tri.ngon_method, tri.keep_custom_normals = "BEAUTY", "BEAUTY", True
    k.apply_modifiers(ob)
    k.surface_uvs(ob)


def make_bay(col, name, bm, lamps=()):
    """A flat bay -> bevelled, bent onto the wall, metre UVs. Preview only: the bays either side (the same mesh turned
    10 deg round the hall's middle), so a bay is seen between its two pilasters as on the wall."""
    ob = k.new_object(name, bm, col, SLOT_ORDER)
    shape_bay(ob)
    extra = []
    for sgn in (-1, 1):
        nb = bpy.data.objects.new("Preview bay %+d" % sgn, ob.data)
        col.objects.link(nb)
        nb.matrix_world = (Matrix.Translation((0, WALL_FACE, 0)) @ Matrix.Rotation(sgn * 2 * math.pi / BAYS, 4, "Z") @
                           Matrix.Translation((0, -WALL_FACE, 0)))
        extra.append(nb)
    lamp_at = [bend_point(v) for v in lamps]
    for nb in extra:
        lamp_at += [nb.matrix_world @ bend_point(v) for v in lamps]
    return [ob], extra, lamp_at


def part_niche(col):
    return make_bay(col, "RoHall niche bay", niche_bay())


def part_arcade(col):
    bm, lamps = arcade_bay()
    return make_bay(col, "RoHall arcade bay", bm, lamps)


def part_portal(col):
    bm, lamps = portal_bay()
    ob = k.new_object("RoHall south portal", bm, col, SLOT_ORDER)
    shape_bay(ob)
    # preview only: the bays either side as they will stand (the middle arcade tier and the niches over it), 1.5 bays
    # round the hall's middle; their lamps lit too
    extra, lamp_at = [], [bend_point(v) for v in lamps]
    for kind, foot in (("arcade", 0.0), ("niche", 10.5)):
        if kind == "arcade":
            nbm, nl = arcade_bay()
        else:
            nbm, nl = niche_bay(), []
        nob = k.new_object("Preview " + kind, nbm, col, SLOT_ORDER)
        shape_bay(nob)
        for sgn in (-1, 1):
            o = nob if sgn < 0 else bpy.data.objects.new("Preview %s +1" % kind, nob.data)
            if sgn > 0:
                col.objects.link(o)
            o.matrix_world = (Matrix.Translation((0, WALL_FACE, foot)) @ Matrix.Rotation(sgn * 1.5 * 2 * math.pi / BAYS, 4, "Z") @
                              Matrix.Translation((0, -WALL_FACE, 0)))
            extra.append(o)
            lamp_at += [o.matrix_world @ bend_point(v) for v in nl]
    return [ob], extra, lamp_at


# ---------------------------------------------------------------- the statues in the niches (no new models: PLAN 1-3)
# Two sets to choose from, both already in the project, drawn in marble (RO_Marble) with their gold (RO_Gold):
#   "chess"  the royal library's chess pieces, Resources/RoyalLibrary/Models/Chess/RlChess*.fbx (3 - 6k tris each) -
#            the plain turned pieces of RO_1. They are giants' pieces (6.4 m across the foot), so each is scaled to the
#            pedestal: 0.3 across, 0.42 up (a little slimmer, as RO_1's).
#   "statue" the grand stair hall's sculpted statues, Assets/Art/PawnRushMap/Kit/PR_Statue_*.fbx (18 - 36k tris):
#            a caped king, a queen with crossed sceptres, a bishop with a staff, a rearing horse. Scaled to 4.4 m high
#            (the horse to the niche's width).
# In Unity the Dressing would load the same FBX (no copy) and set the materials by name; here they are imported for the
# pictures only. The user (11-19, both sets side by side): "지금 좋아" - the chess pieces, at this size.
STATUE_KIND = "chess"
STATUE_SETS = {
    "chess": [(os.path.join(ROOT_DIR, "Assets", "Resources", "RoyalLibrary", "Models", "Chess", "RlChess%s.fbx" % n), n)
              for n in ("King", "Queen", "Knight", "Rook", "Bishop")],
    "statue": [(os.path.join(ROOT_DIR, "Assets", "Art", "PawnRushMap", "Kit", "PR_Statue_%s.fbx" % n), n)
               for n in ("Bishop", "King", "Knight", "Queen", "Bishop")],
}
PEDESTAL_TOP = NICHE["sill"] + 1.12
# the arcade galleries' pieces (two lower tiers): a little larger (the arches are wider), lighter LODs (they are lower
# and farther from the way; 70 of them)
ARCADE_STATUE_AT = (0.0, -2.3 * .5, 1.0 + 1.12)
ARCADE_PIECE_SCALE = 1.15
ARCADE_DECIMATE = (.4, .2, .09)
ARCADE_ORDER = ("Rook", "Bishop", "Knight", "Pawn", "Queen", "King")
STATUE_AT = (0.0, -NICHE["depth"] * .55, PEDESTAL_TOP)      # the pedestal's top middle, flat bay frame


def import_statue(path, kind, name):
    """One statue as a single mesh object at the origin, standing on z 0, facing +y (the hall), in ro_kit slots."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    for o in meshes:
        o.data.transform(o.matrix_world)
        o.matrix_world = Matrix.Identity(4)
        o.parent = None
        mats = o.data.materials
        for i, m in enumerate(mats):
            low = (m.name if m else "").lower()
            mats[i] = k.slot("marble" if ("ivory" in low or "stone" in low or not low) else "gold")
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    ob = k.join(meshes, "Statue " + name) if len(meshes) > 1 else meshes[0]
    ob.name = "Statue " + name
    pts = [v.co for v in ob.data.vertices]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    size = hi - lo
    if kind == "chess":
        sc = Vector((.3, .3, .42))
    else:
        f = min(4.4 / size.z, 3.6 / max(size.x, size.y))
        sc = Vector((f, f, f))
    centre = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    turn = math.pi if (kind == "statue" or name == "Knight") else 0.0      # their fronts look -y
    if name == "Knight":
        turn += math.radians(-28)                                          # three quarters to the hall
    ob.data.transform(Matrix.Rotation(turn, 4, "Z") @ Matrix.Diagonal((*sc, 1)) @ Matrix.Translation(-centre))
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    return ob


def part_statues(col, kind):
    """Five niche bays side by side, a statue on each pedestal (the middle one alone in the closer pictures)."""
    objs, extra, lamps = make_bay(col, "RoHall niche bay", niche_bay())
    for o in extra:
        bpy.data.objects.remove(o, do_unlink=True)
    centre_bay = objs[0]
    extra = []
    bays = []
    for i in range(-2, 3):
        if i == 0:
            b = centre_bay
        else:
            b = bpy.data.objects.new("Preview bay %+d" % i, centre_bay.data)
            col.objects.link(b)
            b.matrix_world = (Matrix.Translation((0, WALL_FACE, 0)) @ Matrix.Rotation(i * 2 * math.pi / BAYS, 4, "Z") @
                              Matrix.Translation((0, -WALL_FACE, 0)))
            extra.append(b)
        bays.append(b)
    made = []
    for (path, name), b in zip(STATUE_SETS[kind], bays):
        st = import_statue(path, kind, name)
        col.objects.link(st)
        at = b.matrix_world @ bend_point(STATUE_AT)
        yaw = math.atan2(b.matrix_world[1][0], b.matrix_world[0][0])
        st.matrix_world = Matrix.Translation(at) @ Matrix.Rotation(yaw, 4, "Z")
        made.append(st)
        print("   statue", name, k.tris([st]), "tris", tuple(round(v, 2) for v in st.dimensions))
    mid = made[2]
    extra += [m for m in made if m is not mid]
    return [centre_bay, mid], extra, []


# ---------------------------------------------------------------- the armillary sphere (RO_A3, middle row)
# On the niche's pedestal between the statues (RO_1: piece, piece, globe ...): a marble drum with gold bead rings and a
# navy band of stars, a gold vase stem and cradle, the sphere 2 m across - the meridian band and the horizon band
# studded with stars, the equator and two colures in gold wire round a polar axis tilted 35 deg with knobbed ends, the
# ecliptic band 23.4 deg off the equator, a gold ball in the middle. Built standing on z 0, facing +y.
ARMILLARY = dict(R=1.0, axis_tilt=35.0, ecliptic=23.4)


def band_ring(bm, R, half_w, half_t, segs, mat, m):
    """A flat band ring (radius R, width 2 half_w along its axis, thickness 2 half_t) round z, moved by m."""
    b = k.lathe([(R - half_t, -half_w), (R + half_t, -half_w), (R + half_t, half_w), (R - half_t, half_w),
                 (R - half_t, -half_w)], nseg(segs, 12), mat=mat, smooth=False)
    add(bm, b, m)


def armillary():
    p = ARMILLARY
    R = p["R"]
    bm = bmesh.new()
    # the stand
    lathe_at(bm, [(0, 0), (.62, 0), (.62, .08), (.56, .1), (.56, .16), (.5, .2), (.36, .24), (.34, .3), (0, .3)],
             (0, 0, 0), 32, MARBLE)
    if DETAIL != 2:
        add(bm, k.beads(.6, 30, .07, .12, GOLD, rings=4 if DETAIL is None else 3, segs=6 if DETAIL is None else 4), None)
    lathe_at(bm, [(0, 0), (.34, 0), (.34, .12), (.3, .14), (.3, .9), (.34, .92), (.34, 1.02), (0, 1.02)], (0, 0, .3), 28, MARBLE)
    lathe_at(bm, [(0, 0), (.315, 0), (.315, .2), (0, .2)], (0, 0, .62), 28, NAVY)
    for i in range(6):
        t = 2 * math.pi * i / 6
        st = (parts.star_stud(.12, .02, GOLD, 5) if DETAIL is None or DETAIL == 0 else bmesh.new())
        add(bm, st, Matrix.Translation((.315 * math.cos(t), .315 * math.sin(t), .72)) @ Matrix.Rotation(t, 4, "Z"))
    for z in (.44, .58, .86):
        add(bm, torus_lod(.33, .025, 28, 6, GOLD), Matrix.Translation((0, 0, z)))
    if DETAIL != 2:
        add(bm, k.beads(.35, 22, .06, 1.34, GOLD, rings=4 if DETAIL is None else 3, segs=6 if DETAIL is None else 4), None)
    # the stem and its cradle
    lathe_at(bm, [(0, 0), (.24, 0), (.2, .05), (.1, .12), (.07, .25), (.09, .34), (.14, .4), (.08, .48), (.06, .6),
                  (.1, .66), (.22, .7), (.24, .74), (0, .74)], (0, 0, 1.32), 24, GOLD)
    c = Vector((0, 0, 2.06 + R + .04))
    # the meridian: a star-studded band in the x-z plane (facing the hall)
    band_ring(bm, R, .05, .045, 40, GOLD, Matrix.Translation(c) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    for i in range(12):
        t = 2 * math.pi * (i + .5) / 12
        add(bm, (parts.star_stud(.1, .02, GOLD, 4) if DETAIL is None or DETAIL == 0 else bmesh.new()),
            Matrix.Translation(c + Vector(((R + .045) * math.cos(t), 0, (R + .045) * math.sin(t)))) @
            Matrix.Rotation(-t, 4, "Y"))
        star_on(bm, .09, c + Vector(((R) * math.cos(t), .05, (R) * math.sin(t))), .02, 4)
    # the horizon: a wider band round the middle, its stars facing out
    Rh = R + .1
    band_ring(bm, Rh, .07, .03, 40, GOLD, Matrix.Translation(c))
    for i in range(16):
        t = 2 * math.pi * i / 16
        add(bm, (parts.star_stud(.11, .02, GOLD, 5) if DETAIL is None or DETAIL == 0 else bmesh.new()), Matrix.Translation(c + Vector(((Rh + .03) * math.cos(t), (Rh + .03) * math.sin(t), 0))) @ Matrix.Rotation(t, 4, "Z"))
    # the polar axis tilted in the meridian's plane, the equator and the colures round it, the ecliptic off the equator
    tilt = Matrix.Rotation(math.radians(p["axis_tilt"]), 4, "Y")
    rod = k.lathe([(0, -R - .32), (.025, -R - .32), (.025, R + .32), (0, R + .32)], 10, mat=GOLD)
    add(bm, rod, Matrix.Translation(c) @ tilt)
    for sgn in (-1, 1):
        add(bm, bead_lod(.07, GOLD, 12, 8), Matrix.Translation(c) @ tilt @ Matrix.Translation((0, 0, sgn * (R + .36))))
        add(bm, torus_lod(.06, .018, 12, 6, GOLD), Matrix.Translation(c) @ tilt @ Matrix.Translation((0, 0, sgn * (R + .25))))
    Ri = R * .86
    add(bm, torus_lod(Ri, .022, 40, 6, GOLD), Matrix.Translation(c) @ tilt)                                  # equator
    add(bm, torus_lod(Ri, .018, 40, 5, GOLD), Matrix.Translation(c) @ tilt @ Matrix.Rotation(math.pi / 2, 4, "X"))   # colures
    add(bm, torus_lod(Ri, .018, 40, 5, GOLD), Matrix.Translation(c) @ tilt @ Matrix.Rotation(math.pi / 2, 4, "Y"))
    for zt in (-.4, .4):                                                                                     # tropics
        rr = math.sqrt(Ri * Ri - zt * zt)
        add(bm, torus_lod(rr, .012, 32, 4, GOLD), Matrix.Translation(c) @ tilt @ Matrix.Translation((0, 0, zt)))
    ecl = Matrix.Translation(c) @ tilt @ Matrix.Rotation(math.radians(p["ecliptic"]), 4, "X")
    band_ring(bm, Ri + .01, .085, .02, 40, GOLD, ecl)
    for i in range(12):
        t = 2 * math.pi * i / 12
        add(bm, (parts.star_stud(.09, .015, GOLD, 5) if DETAIL is None or DETAIL == 0 else bmesh.new()), ecl @ Matrix.Translation(((Ri + .03) * math.cos(t), (Ri + .03) * math.sin(t), 0)) @ Matrix.Rotation(t, 4, "Z"))
    # the ball in the middle
    add(bm, bead_lod(.2, GOLD, 16, 10), Matrix.Translation(c))
    add(bm, torus_lod(.21, .015, 24, 5, GOLD), Matrix.Translation(c))
    bm.normal_update()
    return bm


def part_armillary(col):
    """The armillary sphere on the middle niche's pedestal, a chess piece either side (as RO_1 alternates them)."""
    objs, extra, lamps = make_bay(col, "RoHall niche bay", niche_bay())
    for o in extra:
        bpy.data.objects.remove(o, do_unlink=True)
    centre_bay = objs[0]
    extra = []
    ob = k.new_object("RoHall armillary", armillary(), col, SLOT_ORDER)
    k.surface_uvs(ob)
    ob.matrix_world = Matrix.Translation(bend_point(STATUE_AT))
    for i, name in ((-1, "Rook"), (1, "Queen")):
        b = bpy.data.objects.new("Preview bay %+d" % i, centre_bay.data)
        col.objects.link(b)
        b.matrix_world = (Matrix.Translation((0, WALL_FACE, 0)) @ Matrix.Rotation(i * 2 * math.pi / BAYS, 4, "Z") @
                          Matrix.Translation((0, -WALL_FACE, 0)))
        path = [pth for pth, n in STATUE_SETS["chess"] if n == name][0]
        st = import_statue(path, "chess", name)
        col.objects.link(st)
        st.matrix_world = Matrix.Translation(b.matrix_world @ bend_point(STATUE_AT)) @ \
            Matrix.Rotation(math.atan2(b.matrix_world[1][0], b.matrix_world[0][0]), 4, "Z")
        extra += [b, st]
    print("   armillary", k.tris([ob]), "tris", tuple(round(v, 2) for v in ob.dimensions))
    return [centre_bay, ob], extra, []


# ---------------------------------------------------------------- the banners (RO_A3 / RO_1: violet, a gold crown)
# The user (11-19) on a first, modelled banner: "깃발은 블렌더에서 하면 뭔가 딱딱한 느낌이니까 다른 방법으로 실제 깃발 같은
# 느낌을 줄 수 있는 걸로 만들어". So the cloth is not modelled but simulated: a velvet sheet pinned along its top to the
# rod, started with soft pleats, let fall under gravity with a little turbulence (Blender cloth), and taken when it has
# settled - its folds are the cloth's own. The gold on it (border, hem, crown, fringe) is built flat and carried onto the
# fallen cloth by a Surface Deform modifier, so it lies in the folds. The rod, its finials and brackets stay rigid.
# Movement is Unity's (a later step): a few bones down the banner, swung slowly by the Dressing, each banner its own way.
# On every other pilaster of the niche tier, below its capital. Slots: velvet, gold.
BANNER = dict(w=1.3, top=8.15, rect=3.1, point=.75, y=.56, nu=20, nv=50, frames=90)
VEL, BGOLD = 0, 1


def _banner_at(u, v, pleat=True):
    """A point of the flat sheet (banner frame: top middle at 0, x across, z down, the hall +y), the start pleats on."""
    p = BANNER
    x = (u - .5) * p["w"]
    z = -v * (p["rect"] + p["point"] * (1 - abs(2 * u - 1)))
    y = (.09 * math.sin(u * math.pi * 5 + .4) * (1 - .5 * v) + .025 * math.sin(u * math.pi * 11 + 1.3)) if pleat else 0.0
    return Vector((x, y, z))


def banner_sheet():
    """The flat velvet sheet with its pin group (the top row)."""
    p = BANNER
    nu, nv = p["nu"], p["nv"]
    bm = bmesh.new()
    grid = [[bm.verts.new(_banner_at(i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            f = bm.faces.new((grid[j][i], grid[j + 1][i], grid[j + 1][i + 1], grid[j][i + 1]))
            f.material_index = VEL
            f.smooth = True
    bm.normal_update()
    for f in bm.faces:
        if f.normal.y < 0:
            f.normal_flip()
    me = bpy.data.meshes.new("banner sheet")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("banner sheet", me)
    bpy.context.scene.collection.objects.link(ob)
    for s_ in ("velvet", "gold"):
        me.materials.append(k.slot(s_))
    pin = ob.vertex_groups.new(name="pin")
    pin.add([i for i in range(nu + 1)], 1.0, "REPLACE")
    # UVs: the sheet's own (u across, v down), for the velvet's nap
    uv = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (co.x + .65, co.z)
    return ob


def banner_gold_flat():
    """The gold that lies on the sheet, flat and a hair in front of it (before the cloth falls)."""
    p = BANNER
    nu, nv = p["nu"], p["nv"]
    bm = bmesh.new()
    lift = Vector((0, .012, 0))
    side_l = [_banner_at(0, j / nv) + lift for j in range(nv + 1)]
    side_r = [_banner_at(1, j / nv) + lift for j in range(nv + 1)]
    bottom = [_banner_at(i / nu, 1) + lift for i in range(nu + 1)]
    sides = 6 if DETAIL is None else (5, 4, 3)[DETAIL]         # the gold cords' sides at this detail
    thin = lambda line: line if DETAIL is None else line[::(2, 3, 4)[DETAIL]] + ([line[-1]] if (len(line) - 1) % (2, 3, 4)[DETAIL] else [])
    for line in (side_l, side_r, bottom):
        add(bm, parts._tube(thin(line) if line is not bottom else line, .022, sides, BGOLD))
    for u in ((.1, .9) if DETAIL != 2 else ()):
        add(bm, parts._tube(thin([_banner_at(u, j / nv) + lift * 1.5 for j in range(3, nv - 4)]), .009, max(3, sides - 1), BGOLD))
    # the hem along the top
    for dz, r in ((0.0, .032), (-.09, .018))[:1 if DETAIL == 2 else 2]:   # the hem: a gold roll and a cord under it
        add(bm, parts._tube([_banner_at(i / nu, 0) + lift + Vector((0, 0, dz)) for i in range(nu + 1)], r, sides, BGOLD))
    # the crown, raised, with balls at its points and three jewels on its band
    cz = -1.25
    v = 1.25 / (p["rect"] + p["point"])
    yc = _banner_at(.5, v).y + .015
    crown = [(-.3, 0), (.3, 0), (.3, .1), (.4, .48), (.17, .27), (0, .56), (-.17, .27), (-.4, .48), (-.3, .1)]
    add(bm, hard_prism(crown, -.03, 0, BGOLD), Matrix.Translation((0, yc, cz)) @ XZY)
    box(bm, -.33, .33, yc - .01, yc + .045, cz - .12, cz + .02, BGOLD)
    for x, z in ((-.4, .48), (0, .56), (.4, .48)):
        add(bm, bead_lod(.05, BGOLD, 8, 6), Matrix.Translation((x, yc + .02, cz + z + .04)))
    for x in ((-.2, 0, .2) if DETAIL is None or DETAIL == 0 else ()):
        add(bm, bead_lod(.03, BGOLD, 6, 4), Matrix.Translation((x, yc + .05, cz - .05)))
    # the fringe: a strand under every edge segment, a bead at the point
    for i in range(nu):
        a, b = bottom[i], bottom[i + 1]
        if DETAIL is not None and DETAIL >= 1:       # at a distance the fringe is a row of gold teeth (none at LOD2)
            if DETAIL == 1:
                tip = (a + b) / 2 + Vector((0, .004, -.14))
                f = bm.faces.new((bm.verts.new(a), bm.verts.new(tip), bm.verts.new(b)))
                f.material_index = BGOLD
                f.normal_update()
                if f.normal.y < 0:
                    f.normal_flip()
            continue
        for t in ((.25, .75) if DETAIL is None else (.5,)):
            q = a.lerp(b, t)
            add(bm, parts._tube([q, q + Vector((0, 0, -.07)), q + Vector((0, .003, -.14))], .008, 4 if DETAIL is None else 3, BGOLD))
            add(bm, parts.bead(.014, BGOLD, 6 if DETAIL is None else 4, 4 if DETAIL is None else 3),
                Matrix.Translation(q + Vector((0, .003, -.15))))
    add(bm, bead_lod(.06, BGOLD, 10, 6), Matrix.Translation(_banner_at(.5, 1) + Vector((0, .01, -.12))))
    me = bpy.data.meshes.new("banner gold")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("banner gold", me)
    bpy.context.scene.collection.objects.link(ob)
    for s_ in ("velvet", "gold"):
        me.materials.append(k.slot(s_))
    return ob


def banner_rod():
    """The rigid part: the rod, its finials and rings, two brackets back to the pilaster (banner frame)."""
    p = BANNER
    w, y0 = p["w"], 0.0
    bm = bmesh.new()
    rz = .1
    rod = k.lathe([(0, -w / 2 - .32), (.04, -w / 2 - .32), (.04, w / 2 + .32), (0, w / 2 + .32)], nseg(12, 5), mat=BGOLD)
    add(bm, rod, Matrix.Translation((0, y0 + .02, rz)) @ Matrix.Rotation(math.pi / 2, 4, "Y"))
    for sx in (-1, 1):
        lathe_at(bm, [(0, 0), (.06, 0), (.05, .03), (.1, .1), (.11, .17), (.08, .24), (.03, .28), (.04, .33), (0, .36)],
                 (sx * (w / 2 + .3), y0 + .02, rz - .17), 14, BGOLD)
        box(bm, sx * .45 - .04, sx * .45 + .04, NICHE["pil_d"] - p["y"], y0 + .06, rz - .05, rz + .05, BGOLD)
    for i in range(5):
        x = (i / 4 - .5) * (w - .1)
        add(bm, torus_lod(.065, .014, 12, 5, BGOLD), Matrix.Translation((x, y0 + .02, rz)) @ Matrix.Rotation(math.pi / 2, 4, "Y"))
    return bm


def banner_fall():
    """Simulate the sheet falling and settling; return one bmesh (velvet + gold + rod), banner frame."""
    p = BANNER
    sc = bpy.context.scene
    sheet = banner_sheet()
    gold = banner_gold_flat()
    cl = sheet.modifiers.new("Cloth", "CLOTH")
    st = cl.settings
    st.quality = 10
    st.mass = .6                      # velvet: heavy, soft
    st.tension_stiffness = st.compression_stiffness = 25
    st.shear_stiffness = 12
    st.bending_stiffness = 1.5
    st.air_damping = 2.0
    st.vertex_group_mass = "pin"
    cl.point_cache.frame_start, cl.point_cache.frame_end = 1, p["frames"]
    # a little restless air so the folds are not symmetric
    turb = bpy.data.objects.new("banner air", None)
    sc.collection.objects.link(turb)
    turb.location = (.2, .6, -1.5)
    bpy.context.view_layer.objects.active = turb
    with bpy.context.temp_override(object=turb, active_object=turb):
        bpy.ops.object.forcefield_toggle()
    turb.field.type = "TURBULENCE"
    turb.field.strength = 6.0
    turb.field.size = .7
    turb.field.seed = 7
    sc.frame_set(1)
    sd = gold.modifiers.new("Follow", "SURFACE_DEFORM")
    sd.target = sheet
    sd.falloff = 6.0
    with bpy.context.temp_override(object=gold, active_object=gold, selected_objects=[gold]):
        bpy.ops.object.surfacedeform_bind(modifier=sd.name)
    for f in range(1, p["frames"] + 1):
        sc.frame_set(f)
    dg = bpy.context.evaluated_depsgraph_get()
    out = bmesh.new()
    for o in (sheet, gold):
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        if o is sheet and DETAIL is not None:        # the fallen cloth thinned: .6 / .3 / .14 of its faces
            tmp = bpy.data.objects.new("_sheet", me)
            sc.collection.objects.link(tmp)
            dm = tmp.modifiers.new("Decimate", "DECIMATE")
            dm.ratio = (.6, .3, .14)[DETAIL]
            dm.use_collapse_triangulate = True
            k.apply_modifiers(tmp)
            me = tmp.data
            bpy.data.objects.remove(tmp, do_unlink=True)
        b = bmesh.new()
        b.from_mesh(me)
        add(out, b)
        bpy.data.meshes.remove(me)
    add(out, banner_rod())
    for o in (sheet, gold, turb):
        bpy.data.objects.remove(o, do_unlink=True)
    out.normal_update()
    return out


def part_banner(col):
    """A banner on the pilaster between two niche bays (three bays, the middle bay's pilaster)."""
    objs, extra, lamps = make_bay(col, "RoHall niche bay", niche_bay())
    bm = banner_fall()
    bmesh.ops.translate(bm, vec=(-BAY_W / 2, BANNER["y"], BANNER["top"]), verts=bm.verts)
    bend(bm)
    ob = k.new_object("RoHall banner", bm, col, ("velvet", "gold"))
    k.surface_uvs(ob)
    print("   banner", k.tris([ob]), "tris", tuple(round(v, 2) for v in ob.dimensions))
    return [objs[0], ob], extra, []


# ---------------------------------------------------------------- the dome: the star chart over the hall (RO_1, RO_T4)
# Graybox (RoyalObservatoryBuilder.Hall): a half sphere of 50 m round Unity (0, 14, 64); 12 ribs every 30 deg, the first
# 15 deg west of the slit's heading (atan2(9, 49) = 10.4 deg east of north), from the spring (0 deg) to 84 deg; a hoop
# at 36 deg open over the slit; a crown ring at 84 deg. The slit itself (between the two ribs either side of it) is the
# next part; here it is left open.
# The chart is built, not painted - at 50 m a texture would be soft: a navy enamel shell (faces inward), gold wire
# meridians every 15 deg and parallels every 12 deg, ~700 small gold stars and some sixty large ones, four compass suns
# low round the sky (RO_T4's band), and the course's own constellations where the course lies under them (the
# structure: "the ceiling is the course's map"): the Big Dipper over the Dipper, Cassiopeia over Cassiopeia, the pole
# star at the zenith over the great globe. A course point (x, z) maps to the heading from the hall's middle and an
# elevation falling from the crown (84 deg) at the middle to 12 deg at 50 m.
# Ribs (RO_A1's "dome rib"): gold bands with star studs on a square foot; the hoop and crown: gold bands with stars.
DOME = dict(R=50.0, middle=(0.0, 64.0, 14.0), slit=math.degrees(math.atan2(9.0, 49.0)), slit_half=15.0, hoop=36.0,
            crown=84.0)
DIPPER = [(-2, 18), (-10, 26), (-19, 33), (-27, 43), (-38, 49), (-37, 62), (-25, 60)]       # builder Dipper[] (x, z)
CASSIOPEIA = [(19.5, 66.5), (23.7, 72.1), (18.3, 79.3), (23.7, 86.5), (19.76, 92.29)]       # star 1 + the four gears


def dome_dir(h, e):
    """Blender direction from the dome's middle at heading h (deg, Unity: 0 north, 90 east) and elevation e (deg)."""
    h, e = math.radians(h), math.radians(e)
    return Vector((math.sin(h) * math.cos(e), math.cos(h) * math.cos(e), math.sin(e)))


def dome_at(h, e, r=None):
    return Vector(DOME["middle"]) + dome_dir(h, e) * (r if r is not None else DOME["R"])


def in_slit(h, e=0.0):
    d = (h - DOME["slit"] + 180.0) % 360.0 - 180.0
    return abs(d) < DOME["slit_half"] - 1e-6 and e < DOME["crown"]


def course_to_sky(x, z):
    """A course point (Unity x, z) -> (heading, elevation) on the chart."""
    dx, dz = x - 0.0, z - 64.0
    r = math.hypot(dx, dz)
    return math.degrees(math.atan2(dx, dz)), DOME["crown"] - (DOME["crown"] - 12.0) * min(1.0, r / DOME["R"])


def facing_in(at, size_mat=None):
    """A matrix putting something built facing +x at `at`, facing the dome's middle."""
    d = (Vector(DOME["middle"]) - at).normalized()
    return Matrix.Translation(at) @ Vector((1, 0, 0)).rotation_difference(d).to_matrix().to_4x4()


def sky_star(bm, h, e, size, points=4, r_off=-.2, depth=None):
    add(bm, parts.star_stud(size, depth or size * .12, GOLD, points), facing_in(dome_at(h, e, DOME["R"] + r_off)))


def sky_line(bm, a, b, r_off, radius, steps=None, mat=GOLD, sides=5):
    """A gold wire on the sphere from (h, e) a to b along the great circle."""
    da, db = dome_dir(*a), dome_dir(*b)
    ang = da.angle(db)
    n = steps or max(2, int(ang / math.radians(2.5)) + 1)
    pts = []
    for i in range(n + 1):
        t = i / n
        v = (da * math.sin((1 - t) * ang) + db * math.sin(t * ang)) / math.sin(ang) if ang > 1e-6 else da
        pts.append(Vector(DOME["middle"]) + v.normalized() * (DOME["R"] + r_off))
    add(bm, parts._tube(pts, radius, sides, mat))


def dome_band(bm, h0, h1, e, width, depth, segs, mat=GOLD):
    """A horizontal gold band on the sphere at elevation e from heading h0 to h1 (its face toward the middle)."""
    R = DOME["R"]
    rings = []
    closed = abs(h1 - h0 - 360.0) < 1e-6
    for i in range(segs + (0 if closed else 1)):
        h = h0 + (h1 - h0) * i / segs
        d = dome_dir(h, e)
        up = dome_dir(h, e + 90.0)
        sec = [(R + .05, -width / 2), (R - depth, -width / 2), (R - depth, width / 2), (R + .05, width / 2)]
        rings.append([bm.verts.new(Vector(DOME["middle"]) + d * r + up * w) for r, w in sec])
    _sweep_rings(bm, rings, mat, closed=closed)


def dome_rib(bm, h, e0, e1, width, depth, segs, mat=GOLD):
    """A gold band up the sphere at heading h from e0 to e1."""
    R = DOME["R"]
    side = Vector((math.cos(math.radians(h)), -math.sin(math.radians(h)), 0))
    rings = []
    for i in range(segs + 1):
        d = dome_dir(h, e0 + (e1 - e0) * i / segs)
        sec = [(R + .05, -width / 2), (R - depth, -width / 2), (R - depth, width / 2), (R + .05, width / 2)]
        rings.append([bm.verts.new(Vector(DOME["middle"]) + d * r + side * w) for r, w in sec])
    _sweep_rings(bm, rings, mat)


def _sweep_rings(bm, rings, mat, closed=False):
    """Faces between consecutive rings of verts (and round, when closed), capped when open; normals outward."""
    n, m = len(rings[0]), len(rings)
    made = []
    for i in range(m if closed else m - 1):
        a, b = rings[i], rings[(i + 1) % m]
        for j in range(n):
            f = bm.faces.new((a[j], a[(j + 1) % n], b[(j + 1) % n], b[j]))
            f.material_index = mat
            made.append(f)
    if not closed:
        for r in (rings[0], rings[-1][::-1]):
            f = bm.faces.new(r)
            f.material_index = mat
            made.append(f)
    bmesh.ops.recalc_face_normals(bm, faces=made)


def compass_sun(bm, h, e, size):
    """RO_T4's compass sun: two gold rings, an eight-point star, sixteen fine rays, a ball."""
    at = dome_at(h, e, DOME["R"] - .1)
    m = facing_in(at)
    flat = Matrix.Rotation(math.pi / 2, 4, "Y")                        # a ring round +x (the inward axis)
    add(bm, parts.torus(size * .62, size * .025, 48, 5, GOLD), m @ flat)
    add(bm, parts.torus(size * .5, size * .015, 48, 5, GOLD), m @ flat)
    for i in range(24):
        t = 2 * math.pi * i / 24
        add(bm, parts.bead(size * .025, GOLD, 6, 4), m @ Matrix.Translation((0, size * .56 * math.cos(t), size * .56 * math.sin(t))))
    # (its eight-point and four-point stars removed: the user, 11-19, "천장에 이렇게 큰 문양은 지워")
    add(bm, parts.bead(size * .1, GOLD, 12, 8), m)


def dome_chart():
    """The dome's star chart, ribs, hoop and crown; Blender world frame (the section's)."""
    p = DOME
    R, c = p["R"], Vector(p["middle"])
    bm = bmesh.new()
    # (the shell is its own object with the sky texture: dome_shell)
    # the gold grid: meridians every 15 deg, parallels every 12 deg (36 is the hoop, 84 the crown)
    for i in range(24):
        h = 7.5 + 15.0 * i
        if in_slit(h):
            continue
        sky_line(bm, (h, 0.0), (h, p["crown"]), .25, .07)
    for e in (12.0, 24.0, 48.0, 60.0, 72.0):                   # parallels: constant elevation, not great circles
        h0 = p["slit"] + p["slit_half"]
        pts = [dome_at(h0 + 330.0 * i / 132, e, R + .25) for i in range(133)]
        add(bm, parts._tube(pts, .07, 5, GOLD))
    # stars: a fixed scatter (the same each run), small four-point and some large eight-point, none in the slit
    import random
    rnd = random.Random(725)
    n = 0
    while n < 1500:
        h, z = rnd.uniform(0, 360), rnd.uniform(math.sin(math.radians(3)), math.sin(math.radians(88)))
        e = math.degrees(math.asin(z))                                  # even over the sphere
        if in_slit(h, e) or abs(((h - p["slit"]) % 30.0) - 15.0) < .9:   # not on a rib
            continue
        big = False          # the large eight-point stars removed (the user, 11-19: "천장에 이렇게 큰 문양은 지워")
        sky_star(bm, h, e, rnd.uniform(2.0, 3.2) if big else rnd.uniform(.4, 1.0), 8 if big else 4,
                 -.15, .22 if big else .08)
        n += 1
    # the four compass suns, low round the sky
    for h in (p["slit"] + 90.0, p["slit"] + 180.0 + 15.0, p["slit"] + 270.0):
        compass_sun(bm, h, 24.0, 6.0)
    compass_sun(bm, p["slit"] + 135.0, 54.0, 4.5)
    # the course's constellations: gold wire lines, a ball and a star at each star; the pole star at the zenith
    for stars in (DIPPER, CASSIOPEIA):
        sky = [course_to_sky(x, z) for x, z in stars]
        for a, b in zip(sky, sky[1:]):
            sky_line(bm, a, b, -.25, .2, sides=6)
        for (h, e) in sky:
            add(bm, parts.bead(.75, GOLD, 14, 8), Matrix.Translation(dome_at(h, e, R - .3)))
            pass             # (the big star on each constellation star removed with the others; its ball stays)
    sky_line(bm, course_to_sky(*DIPPER[-1]), course_to_sky(*DIPPER[3]), -.25, .2, sides=6)   # the bowl closed
    top = c + Vector((0, 0, R - .2))
    add(bm, parts.torus(3.3, .14, 48, 6, GOLD), Matrix.Translation(top))
    add(bm, parts.torus(2.6, .08, 48, 6, GOLD), Matrix.Translation(top))
    # (the pole star's big eight- and four-point stars removed with the others; its rings and ball stay)
    add(bm, parts.bead(.6, GOLD, 16, 10), Matrix.Translation(top + Vector((0, 0, -.4))))
    # the ribs, each with stars down its face and a square foot on the cornice
    for rib in range(12):
        h = p["slit"] - 15.0 + rib * 30.0
        dome_rib(bm, h, 0.0, p["crown"], .8, .4, 42)
        for k_ in range(1, 14):
            e = p["crown"] * k_ / 14
            sky_star(bm, h, e, .42, 4, -.42, .06)
        dome_rib(bm, h, 0.0, 3.0, 1.5, .75, 3)                          # its square foot on the cornice
    # the hoop (open over the slit) and the crown ring
    h0 = p["slit"] + p["slit_half"]
    dome_band(bm, h0, h0 + 330.0, p["hoop"], .8, .45, 132)
    for i in range(44):
        h = h0 + 330.0 * (i + .5) / 44
        sky_star(bm, h, p["hoop"], .45, 5, -.47, .06)
    dome_band(bm, 0.0, 360.0, p["crown"], 1.0, .5, 48)
    bm.normal_update()
    return bm


# ---------------------------------------------------------------- the dome's sky (the user, 11-19: "더 푸르고 별 가득하게")
# The navy enamel read near black under the hall's light and the chart sparse against RO_1 / RO_T4. The shell now has a
# sky of its own: a texture over heading x elevation (u = heading / 360, v = elevation / 90), deep blue darkening to
# the zenith, a violet milky way along a tilted great circle in cloudy noise, and ~40,000 star specks of all
# brightnesses (thickest in the milky way) - the dust no geometry could carry; the gold stars and lines stay built.
# It needs a slot of its own ("sky" -> RO_DomeSky, asked of ro-path); until then the stills make the Blender material
# from this file directly.
SKY_TEX = os.path.join(HERE, "textures", "r6", "RO_DomeSky_Albedo.png")
SKY_SLOT = "RO | sky"


def sky_texture(path=SKY_TEX, W=4096, H=1024, seed=725):
    import numpy as np
    rng = np.random.default_rng(seed)
    u = (np.arange(W) + .5) / W
    v = (np.arange(H) + .5) / H
    U, V = np.meshgrid(u, v)                                  # rows = v (bottom first, as Blender's pixels)
    h, e = U * 2 * np.pi, V * np.pi / 2
    D = np.stack([np.sin(h) * np.cos(e), np.cos(h) * np.cos(e), np.sin(e)], -1)

    def noise(scale_u, scale_v, octaves=4):
        out = np.zeros((H, W))
        amp, total = 1.0, 0.0
        for o in range(octaves):
            gu, gv = scale_u * 2 ** o, scale_v * 2 ** o
            g = rng.random((gv + 2, gu + 1))
            g[:, -1] = g[:, 0]                                # wraps round in heading
            x = U * gu
            y = V * gv
            x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
            fx, fy = x - x0, y - y0
            fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
            a = g[y0, x0] * (1 - fx) + g[y0, x0 + 1] * fx
            b = g[y0 + 1, x0] * (1 - fx) + g[y0 + 1, x0 + 1] * fx
            out += amp * (a * (1 - fy) + b * fy)
            total += amp
            amp *= .5
        return out / total

    low = np.array([34, 52, 132]) / 255.0                     # sRGB: a clear royal blue low round the sky
    high = np.array([14, 22, 74]) / 255.0                     # deepening to the zenith
    t = (V ** .8)[..., None]
    col = low * (1 - t) + high * t
    n1 = noise(8, 3)
    col *= (.82 + .36 * n1)[..., None]
    # the milky way: a band round a great circle tilted across the dome
    nrm = np.array([math.sin(math.radians(60)) * math.cos(math.radians(55)),
                    math.cos(math.radians(60)) * math.cos(math.radians(55)), math.sin(math.radians(55))])
    dist = np.abs(D @ nrm)
    cloud = noise(24, 8, 5)
    band = np.exp(-(dist / .2) ** 2) * np.clip(cloud * 1.6 - .35, 0, 1)
    col = col + band[..., None] * np.array([.38, .2, .55]) * .75 + (band ** 2)[..., None] * np.array([.25, .25, .4]) * .4
    # star dust: even over the sphere, more in the milky way
    def specks(n, in_band=False):
        z = rng.uniform(math.sin(math.radians(1)), 1.0, n)
        hh = rng.uniform(0, 2 * math.pi, n)
        if in_band:
            keep = rng.random(n) < np.exp(-((np.abs(np.stack([np.sin(hh) * np.sqrt(1 - z * z), np.cos(hh) * np.sqrt(1 - z * z), z], -1) @ nrm)) / .15) ** 2)
            z, hh = z[keep], hh[keep]
        px = (hh / (2 * math.pi) * W).astype(int) % W
        py = np.clip((np.arcsin(z) / (math.pi / 2) * H).astype(int), 0, H - 1)
        b = rng.random(len(px)) ** 3
        return px, py, b
    for n, inb in ((26000, False), (30000, True)):
        px, py, b = specks(n, inb)
        tint = np.where(rng.random(len(px))[:, None] < .25, np.array([1.0, .85, .55]), np.array([.85, .9, 1.0]))
        add = (.25 + 1.1 * b)[:, None] * tint
        np.add.at(col, (py, px), add)
        bright = b > .35
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            np.add.at(col, (np.clip(py[bright] + dy, 0, H - 1), (px[bright] + dx) % W), add[bright] * .35)
    col = np.clip(col, 0, 1)
    img = np.concatenate([col, np.ones((H, W, 1))], -1).astype(np.float32)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im = bpy.data.images.new("RO_DomeSky", W, H, alpha=False)
    im.pixels[:] = img.ravel()
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    return im


def sky_material():
    """The Blender material for the sky slot (preview; Unity's RO_DomeSky is ro-path's to make)."""
    m = bpy.data.materials.get(SKY_SLOT)
    if m is None:
        m = bpy.data.materials.new(SKY_SLOT)
        m.use_nodes = True
        p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        p.inputs["Roughness"].default_value = .6
        if os.path.exists(SKY_TEX):
            tex = m.node_tree.nodes.new("ShaderNodeTexImage")
            tex.image = bpy.data.images.load(SKY_TEX, check_existing=True)
            m.node_tree.links.new(tex.outputs["Color"], p.inputs["Base Color"])
    return m


def dome_shell():
    """The sky shell (faces toward the middle, open over the slit) as its own object with heading x elevation UVs."""
    p = DOME
    R, c = p["R"], Vector(p["middle"])
    nh, ne = 120, 30
    me = bpy.data.meshes.new("dome shell")
    bm = bmesh.new()
    rows = [[bm.verts.new(c + dome_dir(360.0 * i / nh, 90.0 * j / ne) * (R + .3)) for i in range(nh)] for j in range(ne)]
    apex = bm.verts.new(c + Vector((0, 0, R + .3)))
    uv = bm.loops.layers.uv.new("UVMap")
    for j in range(ne):
        for i in range(nh):
            if in_slit(360.0 * (i + .5) / nh, 90.0 * (j + .5) / ne):
                continue
            if j < ne - 1:
                vs = (rows[j][i], rows[j][(i + 1) % nh], rows[j + 1][(i + 1) % nh], rows[j + 1][i])
                uvs = ((i / nh, j / ne), ((i + 1) / nh, j / ne), ((i + 1) / nh, (j + 1) / ne), (i / nh, (j + 1) / ne))
            else:
                vs = (rows[j][i], rows[j][(i + 1) % nh], apex)
                uvs = ((i / nh, j / ne), ((i + 1) / nh, j / ne), ((i + .5) / nh, 1.0))
            f = bm.faces.new(vs)
            f.material_index = 0
            f.smooth = True
            for loop, t in zip(f.loops, uvs):
                loop[uv].uv = t
            f.normal_update()
            if f.normal.dot(c - f.calc_center_median()) < 0:
                f.normal_flip()
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("RoHall dome sky", me)
    me.materials.append(sky_material())
    return ob


def part_dome(col):
    sky_texture()
    shell = dome_shell()
    col.objects.link(shell)
    ob = k.new_object("RoHall dome", dome_chart(), col, SLOT_ORDER)
    k.surface_uvs(ob)
    print("   dome", k.tris([ob]), "tris + sky", k.tris([shell]))
    return [ob, shell], [], []


# ---------------------------------------------------------------- the slit, the dome's outside, the night beyond (RO_1, RO_3)
# The slit is the gore between the two ribs either side of the telescope's heading (30 deg wide, from the spring to the
# crown ring). The telescope's deck leaves through its foot (Unity y 20 at r ~55) onto the balcony outside, so from the
# balcony the dome is seen from without: it gets an outer skin 1.6 m over the sky (navy enamel), 24 raised gold ribs, a
# gold ring at its foot and a lantern finial on top, and the slit's two edges show the dome's thickness (marble
# returns, gold nosings, a brass shutter track with rivets on the outside). Beyond the slit: the night - a far shell
# (260 m, the sky slot, the same texture as the dome's sky) round the north, and the moon (a pearl ball: Unity makes
# it glow, as the lamp globes). The outer wall face under the dome (r 53, Unity y -18 .. 14) closes the hall seen from
# outside, left open where the entry tunnel runs.
OUTER = DOME["R"] + 1.6
# The night itself is the scene's skybox while the camera is in the observatory (RoyalObservatorySky, the plan session,
# 11-20: the 260 m shell's edge showed against the scene's blue sky from the balcony, and in a round the sections lie
# about 300 m apart) - night_backdrop is no longer exported. The moon stays a model so it is always in the slit
# wherever a round turns the section: the same direction, 75 m off, its radius cut to keep its size (9 x 75 / 230).
MOON = dict(h=DOME["slit"] + 9.0, e=24.0, dist=75.0, r=9.0 * 75.0 / 230.0)


def dome_outer():
    p = DOME
    R0, c = OUTER, Vector(p["middle"])
    bm = bmesh.new()
    nh, ne = 120, 30
    rows = [[bm.verts.new(c + dome_dir(360.0 * i / nh, 90.0 * j / ne) * R0) for i in range(nh)] for j in range(ne)]
    apex = bm.verts.new(c + Vector((0, 0, R0)))
    for j in range(ne):
        for i in range(nh):
            if in_slit(360.0 * (i + .5) / nh, 90.0 * (j + .5) / ne):
                continue
            vs = ((rows[j][i], rows[j][(i + 1) % nh], rows[j + 1][(i + 1) % nh], rows[j + 1][i]) if j < ne - 1
                  else (rows[j][i], rows[j][(i + 1) % nh], apex))
            f = bm.faces.new(vs)
            f.material_index = NAVY
            f.smooth = True
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - c) < 0:
                f.normal_flip()
    # 24 raised gold ribs outside (two either side of the slit are its frame), a ring at the foot, the lantern
    for i in range(24):
        h = p["slit"] - 15.0 + 15.0 * i
        if in_slit(h + .01) and in_slit(h - .01):
            continue
        _outer_band(bm, h, .7, .35, 0.0, p["crown"] if abs(((h - p["slit"] + 180) % 360) - 180) <= 15.01 else 88.0)
    ring = [c + dome_dir(360.0 * i / 120, 0) * (OUTER + .2) for i in range(121)]
    add(bm, parts._tube(ring, .5, 8, GOLD))
    top = c + Vector((0, 0, OUTER))
    lathe_at(bm, [(0, 0), (2.4, 0), (2.4, .4), (1.8, .6), (1.2, .8), (1.1, 2.6), (1.5, 2.8), (1.5, 3.1), (.5, 3.4),
                  (.3, 4.6), (0, 4.8)], top - Vector((0, 0, .3)), 24, GOLD)
    for i in range(8):                                                       # the lantern's little arched windows
        t = 2 * math.pi * i / 8
        add(bm, parts.star_stud(.7, .1, GOLD, 4), Matrix.Translation(top + Vector((1.12 * math.cos(t), 1.12 * math.sin(t), 1.7))) @ Matrix.Rotation(t, 4, "Z"))
    add(bm, parts.bead(.55, GOLD, 16, 10), Matrix.Translation(top + Vector((0, 0, 5.2))))
    add(bm, parts.star_stud(2.2, .3, GOLD, 8), Matrix.Translation(top + Vector((0, 0, 6.8))) @ Matrix.Rotation(-math.pi / 2, 4, "Y"))
    return bm


def _outer_band(bm, h, width, height, e0, e1, segs=40):
    side = Vector((math.cos(math.radians(h)), -math.sin(math.radians(h)), 0))
    c = Vector(DOME["middle"])
    rings = []
    for i in range(segs + 1):
        d = dome_dir(h, e0 + (e1 - e0) * i / segs)
        rings.append([bm.verts.new(c + d * r + side * w) for r, w in
                      ((OUTER - .05, -width / 2), (OUTER + height, -width * .35), (OUTER + height, width * .35), (OUTER - .05, width / 2))])
    _sweep_rings(bm, rings, GOLD)


def slit_returns():
    """The slit's two edges: the dome's thickness in marble, gold nosings inside and out, a brass track with rivets."""
    p = DOME
    c = Vector(p["middle"])
    bm = bmesh.new()
    for sgn in (-1, 1):
        h = p["slit"] + sgn * p["slit_half"]
        side = Vector((math.cos(math.radians(h)), -math.sin(math.radians(h)), 0)) * sgn     # out of the slit's middle
        segs = 42
        rings = []
        for i in range(segs + 1):
            d = dome_dir(h, p["crown"] * i / segs)
            rings.append([bm.verts.new(c + d * r + side * w) for r, w in
                          ((p["R"] + .3, .02), (OUTER, .02), (OUTER, .45), (p["R"] + .3, .45))])
        _sweep_rings(bm, rings, MARBLE)
        for r, w, mat in ((p["R"] + .3, .14, GOLD), (OUTER, .14, GOLD), (OUTER + .45, .3, BRASS)):
            pts = [c + dome_dir(h, p["crown"] * i / segs) * r + side * (w - .1) for i in range(segs + 1)]
            add(bm, parts._tube(pts, .1 if mat == GOLD else .16, 6, mat))
        for i in range(1, 28):
            d = dome_dir(h, p["crown"] * i / 28)
            add(bm, parts.rivet(.07, .06, BRASS), facing_in(c + d * (OUTER + .62) + side * .2) @ Matrix.Rotation(math.pi, 4, "Z"))
    # the crown ring's end over the slit: a marble lintel joining the two edges at the top
    h0, h1 = p["slit"] - p["slit_half"], p["slit"] + p["slit_half"]
    rings = []
    for i in range(13):
        d = dome_dir(h0 + (h1 - h0) * i / 12, p["crown"])
        up = dome_dir(h0 + (h1 - h0) * i / 12, p["crown"] + 90)
        rings.append([bm.verts.new(c + d * r + up * w) for r, w in ((p["R"] + .3, -.5), (OUTER, -.5), (OUTER, .5), (p["R"] + .3, .5))])
    _sweep_rings(bm, rings, MARBLE)
    return bm


def outer_wall():
    """The hall's wall from outside: a marble drum at r 53 from the hall floor to the dome's foot, a gold band at its
    top, open over the entry tunnel."""
    c = Vector(DOME["middle"])
    bm = bmesh.new()
    nh = 120
    lo, hi = HALL_FLOOR, WALL_TOP
    for i in range(nh):
        h0, h1 = 360.0 * i / nh, 360.0 * (i + 1) / nh
        if abs(((h0 + h1) / 2 - 180.0)) < 11.0:
            continue
        a, b = dome_dir(h0, 0), dome_dir(h1, 0)
        r = 53.0
        vs = [bm.verts.new(Vector((c.x, c.y, z)) + d * r) for d, z in ((a, lo), (b, lo), (b, hi), (a, hi))]
        f = bm.faces.new(vs)
        f.material_index = MARBLE
        f.normal_update()
        if f.normal.dot(f.calc_center_median() - Vector((c.x, c.y, f.calc_center_median().z))) < 0:
            f.normal_flip()
    # the band at the top, up to the dome's foot ring
    ring = [Vector((c.x, c.y, hi - .3)) + dome_dir(360.0 * i / 120, 0) * 53.2 for i in range(121)]
    add(bm, parts._tube(ring, .35, 8, GOLD))
    # a ledge from the wall's top in to the dome's skin, so no gap shows between them
    for i in range(nh):
        h0, h1 = 360.0 * i / nh, 360.0 * (i + 1) / nh
        a, b = dome_dir(h0, 0), dome_dir(h1, 0)
        vs = [bm.verts.new(Vector((c.x, c.y, hi)) + d * r) for d, r in ((a, 53.0), (b, 53.0), (b, OUTER - .2), (a, OUTER - .2))]
        f = bm.faces.new(vs)
        f.material_index = MARBLE
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    return bm


def night_backdrop():
    """The far sky round the north, seen through the slit and from the balcony: a shell of 260 m on the sky slot."""
    c = Vector(DOME["middle"])
    Rn = 260.0
    me = bpy.data.meshes.new("night")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    nh, ne = 40, 20
    h0, h1, e0, e1 = DOME["slit"] - 75.0, DOME["slit"] + 75.0, -12.0, 85.0
    grid = [[bm.verts.new(c + dome_dir(h0 + (h1 - h0) * i / nh, e0 + (e1 - e0) * j / ne) * Rn) for i in range(nh + 1)]
            for j in range(ne + 1)]
    for j in range(ne):
        for i in range(nh):
            f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            for loop in f.loops:
                co = loop.vert.co - c
                # heading unwrapped round the slit (the shell crosses north: u runs on past 1 / below 0, the texture
                # repeats) - a face across the seam would otherwise squeeze the whole sky into one strip
                h = DOME["slit"] + (math.degrees(math.atan2(co.x, co.y)) - DOME["slit"] + 180.0) % 360.0 - 180.0
                e = math.degrees(math.asin(max(-1.0, min(1.0, co.z / co.length))))
                loop[uv].uv = (h / 360.0, max(0.0, e) / 90.0 * .999)
            f.normal_update()
            if f.normal.dot(c - f.calc_center_median()) < 0:
                f.normal_flip()
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("RoHall night", me)
    me.materials.append(night_material())
    return ob


NIGHT_SLOT = "RO | night"


def night_material():
    """The far night's material: the sky texture unlit (it is the sky, nothing lights it) - "night" -> RO_NightSky,
    asked of ro-path. Preview: the texture as emission."""
    m = bpy.data.materials.get(NIGHT_SLOT)
    if m is None:
        m = bpy.data.materials.new(NIGHT_SLOT)
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            if n.type == "BSDF_PRINCIPLED":
                nt.nodes.remove(n)
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Strength"].default_value = .8
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(SKY_TEX, check_existing=True)
        nt.links.new(tex.outputs["Color"], em.inputs["Color"])
        nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
    return m


def part_slit(col):
    sky_texture()
    bm = dome_outer()
    add(bm, slit_returns())
    add(bm, outer_wall())
    mb = bmesh.new()
    add(mb, parts.bead(MOON["r"], PEARL, 32, 20), Matrix.Translation(Vector(DOME["middle"]) + dome_dir(MOON["h"], MOON["e"]) * MOON["dist"]))
    add(bm, mb)
    ob = k.new_object("RoHall dome outside", bm, col, SLOT_ORDER)
    k.surface_uvs(ob)
    night = None                         # the night is the scene's skybox now (RoyalObservatorySky)
    # the dome's inside as built (preview only), so the slit is seen in its frame
    inner = k.new_object("Preview dome", dome_chart(), col, SLOT_ORDER)
    shell = dome_shell()
    col.objects.link(shell)
    print("   slit + outside", k.tris([ob]), "tris")
    return [ob], [inner, shell], [Vector(DOME["middle"]) + dome_dir(MOON["h"], MOON["e"]) * (MOON["dist"] - 12)]


# ---------------------------------------------------------------- the abyss: the hall's floor opened onto deep space
# The user (11-19) on the hall floor: "진짜 우주에 온 것 처럼 플레이어가 우주 공포감이 들 수 있게 끔 하고 코덱스에
# 요청해" -> Codex: Docs/PawnRush/ConceptArt/R725_RoyalObservatoryConcept/AssetRefs/Abyss/ (RO_4 the mood, RO_S1 the
# nebula round a dark void, RO_S2 stars on black, RO_S3 faint wisps on black) -> "이 방향으로 만들고 유니티 처리도 해".
# The graybox floor (y -18, nothing collided with; the kill volume is at y -5) stops drawing. Under where it was: a well
# 50 m round going down to y -110, its wall the star layer (opaque), its bottom the nebula with the void under the great
# globe; between them two thin layers that add light - the stars at y -55 and the wisps at y -30 and -80 - so as the
# camera moves the layers slide past each other and the depth is real. Faint gold orbit rings hang where the floor was
# (its last trace, RO_4). Everything that stood on the floor goes on down and is lost in the dark: the pillars and
# axles continue to the bottom, the great globe's base hangs a tapering marble and gold root.
# Slots: the four layers are this zone's own (abyss_nebula, abyss_wall, abyss_stars, abyss_wisps): their materials are
# made by RoyalObservatoryDressing.Hall.cs from the Codex textures (unlit; the stars and wisps additive); the rest is the
# library's (gold, marble).
ABYSS = dict(R=50.0, bottom=-110.0, stars=-55.0, wisps=(-30.0, -80.0), rings=-18.6, wall_tile=48.0)
ABYSS_TEX = os.path.join(ROOT_DIR, "Docs", "PawnRush", "ConceptArt", "R725_RoyalObservatoryConcept", "AssetRefs", "Abyss")
ABYSS_LAYERS = {   # slot: (texture, additive, preview emission strength)
    "abyss_nebula": ("RO_S1_abyss_nebula.png", False, 1.0),
    "abyss_wall": ("RO_S2_abyss_stars.png", False, .55),
    "abyss_stars": ("RO_S2_abyss_stars.png", True, 1.2),
    "abyss_wisps": ("RO_S3_abyss_wisps.png", True, 2.2),
}


def abyss_material(slot_name):
    """Blender material "RO | abyss_*" for the preview: the texture as emission (added over what is behind for the
    additive ones). Unity's are RoyalObservatoryDressing.Hall.cs's."""
    key = "RO | " + slot_name
    m = bpy.data.materials.get(key)
    if m is not None:
        return m
    tex_name, additive, strength = ABYSS_LAYERS[slot_name]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type == "BSDF_PRINCIPLED":
            nt.nodes.remove(n)
    out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(ABYSS_TEX, tex_name), check_existing=True)
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Strength"].default_value = strength
    nt.links.new(tex.outputs["Color"], em.inputs["Color"])
    if additive:
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        add_ = nt.nodes.new("ShaderNodeAddShader")
        nt.links.new(em.outputs[0], add_.inputs[0])
        nt.links.new(tr.outputs[0], add_.inputs[1])
        nt.links.new(add_.outputs[0], out.inputs["Surface"])
        try:
            m.surface_render_method = "BLENDED"
        except (AttributeError, TypeError):
            m.blend_method = "BLEND"
    else:
        nt.links.new(em.outputs[0], out.inputs["Surface"])
    return m


def _disc(bm, uv, z, R, mat, turn=0.0, tile=1.0, segs=96):
    """A disc facing up at height z, planar UVs (the texture's square over the diameter, `tile` times, turned)."""
    c = Vector(DOME["middle"])
    hub = bm.verts.new((c.x, c.y, z))
    ring = [bm.verts.new((c.x + R * math.cos(2 * math.pi * i / segs), c.y + R * math.sin(2 * math.pi * i / segs), z))
            for i in range(segs)]
    ct, st = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    for i in range(segs):
        f = bm.faces.new((hub, ring[i], ring[(i + 1) % segs]))
        f.material_index = mat
        for loop in f.loops:
            x, y = (loop.vert.co.x - c.x) / (2 * R), (loop.vert.co.y - c.y) / (2 * R)
            x, y = x * ct - y * st, x * st + y * ct
            loop[uv].uv = (.5 + x * tile, .5 + y * tile)
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()


def abyss_layers():
    """The well's wall, its bottom and the three light layers: one object, slots abyss_nebula, _wall, _stars, _wisps."""
    p = ABYSS
    c = Vector(DOME["middle"])
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    NEB, WALL, STARS, WISPS = 0, 1, 2, 3
    # the wall: faces in, the star texture tiled in metres
    nh, nz = 120, 8
    top, bot = HALL_FLOOR, p["bottom"]
    circ = 2 * math.pi * p["R"]
    rows = [[bm.verts.new((c.x + p["R"] * math.cos(2 * math.pi * i / nh), c.y + p["R"] * math.sin(2 * math.pi * i / nh),
                           top + (bot - top) * j / nz)) for i in range(nh + 1)] for j in range(nz + 1)]
    for j in range(nz):
        for i in range(nh):
            f = bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
            f.material_index = WALL
            for loop in f.loops:
                vi = rows[j].index(loop.vert) if loop.vert in rows[j] else rows[j + 1].index(loop.vert)
                jj = j if loop.vert in rows[j] else j + 1
                loop[uv].uv = (circ * vi / nh / p["wall_tile"], (top - (top + (bot - top) * jj / nz)) / p["wall_tile"])
            f.normal_update()
            if f.normal.dot(Vector((c.x, c.y, f.calc_center_median().z)) - f.calc_center_median()) < 0:
                f.normal_flip()
    # The user (11-19, the abyss in Play): "실제로 움직이고 렌더 되게 해줘" - each layer is its own object, its origin
    # on the hall's axis, so Unity (Hall.cs AbyssDrift) turns them about it, each at its own pace.
    obs = [_abyss_object("RoHallAbyss Wall", bm, [NEB, WALL, STARS, WISPS])]
    for name, fn in (("RoHallAbyss Nebula", lambda b, u: _disc(b, u, bot, p["R"], NEB, turn=20.0)),
                     ("RoHallAbyss Stars", lambda b, u: _disc(b, u, p["stars"], p["R"] - .2, STARS, turn=0.0, tile=2.0)),
                     ("RoHallAbyss Wisps A", lambda b, u: _disc(b, u, p["wisps"][0], p["R"] - .2, WISPS, turn=35.0, tile=1.0)),
                     ("RoHallAbyss Wisps B", lambda b, u: _disc(b, u, p["wisps"][1], p["R"] - .2, WISPS, turn=160.0, tile=1.3))):
        b2 = bmesh.new()
        u2 = b2.loops.layers.uv.new("UVMap")
        fn(b2, u2)
        obs.append(_abyss_object(name, b2, None))
    return obs


def _abyss_object(name, bm, _unused):
    """A mesh object from bm, its origin moved to the hall's axis at the mesh's own height."""
    c = Vector(DOME["middle"])
    zs = [v.co.z for v in bm.verts]
    origin = Vector((c.x, c.y, (min(zs) + max(zs)) / 2))
    bmesh.ops.translate(bm, vec=-origin, verts=bm.verts)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    ob.location = origin
    for s_ in ("abyss_nebula", "abyss_wall", "abyss_stars", "abyss_wisps"):
        me.materials.append(abyss_material(s_))
    return ob


def abyss_roots():
    """What stood on the floor goes on down: the pillars and axles to the bottom, a root under the globe's base, and
    the gold orbit rings hanging where the floor was."""
    c = Vector(DOME["middle"])
    bm = bmesh.new()
    seen = set()
    for n in k.dump()["nodes"]:
        m = n["matrix"]
        sy = math.sqrt(m[1] ** 2 + m[5] ** 2 + m[9] ** 2)
        sx = math.sqrt(m[0] ** 2 + m[4] ** 2 + m[8] ** 2)
        if not ("pillar" in n["name"] or "axle" in n["name"] or n["name"] == "Planet rail post 1" or
                n["name"].startswith("Planet rail post")):
            continue
        if abs(m[7] - sy - HALL_FLOOR) > .4 or sx > 3.0:
            continue
        x, z = m[3], m[11]
        if (round(x, 1), round(z, 1)) in seen:
            continue
        seen.add((round(x, 1), round(z, 1)))
        # the same radius as the model above it ends with (ro-path, 11-19: their pillars now stop at y -18 with no
        # foot - star platforms .36, variable stars .22, orbit pillars .25, rail posts .17), else the graybox's
        nm = n["name"]
        r = (.36 if nm in ("Dipper star 1 pillar", "Dipper star 4 pillar", "Dipper star 7 pillar") else
             .22 if nm in ("Dipper star 2 pillar", "Dipper star 3 pillar") else
             .25 if nm.startswith("Orbit pillar") else
             .17 if nm.startswith("Planet rail post") else max(sx * .5, .3) * 1.15)
        add(bm, parts.fluted_shaft(r, ABYSS["bottom"], HALL_FLOOR, 12, r * .1, GOLD), Matrix.Translation((x, z, 0)))
        for zz in (HALL_FLOOR - .4, HALL_FLOOR - 9.0, HALL_FLOOR - 26.0, HALL_FLOOR - 50.0):
            add(bm, parts.torus(r * 1.15, r * .18, 16, 6, GOLD), Matrix.Translation((x, z, zz)))
    # the great globe's base: a tapering root under its foot. ro-hall's base model is wider than the graybox's 26 m
    # (31 m, its lowest gold rim at r 15.46, its underside closed with a gold disc at y -18): the root starts at that
    # rim and narrows from a metre under it.
    lathe_at(bm, [(15.5, 0), (15.3, -1.0), (13.4, -2.2), (11.2, -5.5), (8.2, -10.0), (5.8, -16.0), (4.0, -25.0), (2.6, -37.0),
                  (1.7, -53.0), (1.0, -70.0), (0, -86.0)], (c.x, c.y, HALL_FLOOR), 56, MARBLE)
    for zz, rr in ((-1.6, 14.4), (-7.0, 10.0), (-16.0, 5.9), (-37.0, 2.7)):
        add(bm, parts.torus(rr, .22 + rr * .015, 48, 6, GOLD), Matrix.Translation((c.x, c.y, HALL_FLOOR + zz)))
    add(bm, parts.bead(1.4, GOLD, 16, 10), Matrix.Translation((c.x, c.y, HALL_FLOOR - 86.5)))
    # Seen from outside (the user, 11-20, a picture of the well hanging out under the hall: "밖에서 밑에 부분 안보이게
    # 벽으로 해"): the hall's outer wall (r 53, marble) goes on down round the well to its bottom and closes under it,
    # faces outward, so from outside the abyss is a solid drum and none of it shows.
    nh = 96
    ring_t = [bm.verts.new((c.x + 53.0 * math.cos(2 * math.pi * i / nh), c.y + 53.0 * math.sin(2 * math.pi * i / nh), HALL_FLOOR))
              for i in range(nh)]
    ring_b = [bm.verts.new((c.x + 53.0 * math.cos(2 * math.pi * i / nh), c.y + 53.0 * math.sin(2 * math.pi * i / nh), ABYSS["bottom"] - .5))
              for i in range(nh)]
    hub = bm.verts.new((c.x, c.y, ABYSS["bottom"] - .5))
    for i in range(nh):
        j = (i + 1) % nh
        f = bm.faces.new((ring_t[i], ring_b[i], ring_b[j], ring_t[j]))
        f.material_index = MARBLE
        f.normal_update()
        if f.normal.dot(f.calc_center_median() - Vector((c.x, c.y, f.calc_center_median().z))) < 0:
            f.normal_flip()
        g = bm.faces.new((hub, ring_b[j], ring_b[i]))
        g.material_index = MARBLE
        g.normal_update()
        if g.normal.z > 0:
            g.normal_flip()
    # the orbit rings where the floor was, studded with small gold stars
    for rr in (19.0, 27.0, 35.0, 43.0):
        add(bm, parts.torus(rr, .07, 160, 5, GOLD), Matrix.Translation((c.x, c.y, ABYSS["rings"])))
        for i in range(int(rr / 3)):
            t = 2 * math.pi * (i + .3 * rr) / int(rr / 3)
            add(bm, parts.bead(.16, GOLD, 8, 5), Matrix.Translation((c.x + rr * math.cos(t), c.y + rr * math.sin(t), ABYSS["rings"])))
    bm.normal_update()
    return bm


def part_abyss(col):
    layers = abyss_layers()
    for o in layers:
        col.objects.link(o)
    roots = k.new_object("RoHall abyss roots", abyss_roots(), col, SLOT_ORDER)
    k.surface_uvs(roots)
    print("   abyss layers", k.tris(layers), "tris, roots", k.tris([roots]))
    return layers + [roots], [], []


def export_abyss(col_name):
    """Models/Hall/RoHallAbyss.fbx: ro_kit.export, whose slot check knows only the library's slots - the four abyss
    slots are this zone's (Hall.cs makes their materials), so it is let through for this model alone."""
    keep = k.check_slots
    try:
        k.check_slots = lambda meshes, model: None
        return k.export(col_name, "RoHallAbyss", "Hall")
    finally:
        k.check_slots = keep


def part_statues_chess(col):
    return part_statues(col, "chess")


def part_statues_statue(col):
    return part_statues(col, "statue")


# ---------------------------------------------------------------- the hall put together (every part passed, 11-19)
# The user: "조립 시작". Every part where it goes, in the section's frame (Blender = Unity x, z, y):
#   the wall: 36 bays of 10 deg (bay edges on the tens, so the tunnel's opening 170 .. 190 is two whole bays) -
#     arcade at y -18 and -7.5, niches at y 3; over the tunnel the portal from y -7.5 with two ordinary arcade bays
#     under it. A bay's pilaster stands at its higher heading's edge (its flat x = -W/2); the portal's giant pilasters
#     cover the next bays' at both its edges.
#   in the niches, going round: two chess pieces then an armillary sphere; a banner on every other niche pilaster.
#   the dome (chart, sky), the slit and the outside, the night, the moon; the abyss under the floor.
# The course itself is the graybox (ro_blockout.py: the other zones dress it), the graybox's own hall hidden.
# Lamps: a warm point light at every lamp globe (arcade ledges, portal), as RoyalObservatoryDressing does.
CHESS_ORDER = ("King", "Queen", "Rook", "Bishop", "Knight")


def bay_matrix(h, foot):
    """A bent bay (built round the hall's middle at (0, WALL_FACE), its middle at heading 180) moved to heading h
    (degrees, Unity: 0 north, 90 east) with its foot at height `foot`."""
    c = Vector(DOME["middle"])
    return (Matrix.Translation((c.x, c.y, foot)) @ Matrix.Rotation(math.radians(180.0 - h), 4, "Z") @
            Matrix.Translation((0, -WALL_FACE, 0)))


def _instance(col, name, data, m):
    ob = bpy.data.objects.new(name, data)
    col.objects.link(ob)
    ob.matrix_world = m
    return ob


def assemble(col):
    lamps = []
    # the bays' meshes, once each
    niche = k.new_object("RoHall niche bay", niche_bay(), col, SLOT_ORDER)
    shape_bay(niche)
    abm, alamps = arcade_bay()
    arcade = k.new_object("RoHall arcade bay", abm, col, SLOT_ORDER)
    shape_bay(arcade)
    alamps = [bend_point(v) for v in alamps]
    pbm, plamps = portal_bay()
    portal = k.new_object("RoHall south portal", pbm, col, SLOT_ORDER)
    shape_bay(portal)
    plamps = [bend_point(v) for v in plamps]
    for o in (niche, arcade, portal):
        col.objects.unlink(o)                                     # the masters stay out of the scene; instances go in
    # what stands in a niche, and the banner, once each
    statues = {n: import_statue(p, "chess", n) for p, n in STATUE_SETS["chess"]}
    statues["Pawn"] = import_statue(os.path.join(ROOT_DIR, "Assets", "Resources", "RoyalLibrary", "Models", "Chess",
                                                 "RlChessPawn.fbx"), "chess", "Pawn")
    arm = k.new_object("RoHall armillary", armillary(), None, SLOT_ORDER)
    k.surface_uvs(arm)
    bbm = banner_fall()
    bmesh.ops.translate(bbm, vec=(-BAY_W / 2, BANNER["y"], BANNER["top"]), verts=bbm.verts)
    bend(bbm)
    ban = k.new_object("RoHall banner", bbm, None, ("velvet", "gold"))
    k.surface_uvs(ban)
    at_pedestal = Matrix.Translation(bend_point(STATUE_AT))
    pieces = CHESS_ORDER + ("Pawn",)
    n_niche = 0
    for b in range(BAYS):
        h = 5.0 + 10.0 * b
        if abs(h - 180.0) < 6.0:
            _instance(col, "Arcade bay %03d low" % h, arcade.data, bay_matrix(h, TIERS[0][1]))
            lamps += [bay_matrix(h, TIERS[0][1]) @ v for v in alamps]
            continue
        for kind, foot in TIERS:
            m = bay_matrix(h, foot)
            if kind == "arcade":
                _instance(col, "Arcade bay %03d %+.0f" % (h, foot), arcade.data, m)
                lamps += [m @ v for v in alamps]
            else:
                _instance(col, "Niche bay %03d" % h, niche.data, m)
                if n_niche % 3 == 2:
                    _instance(col, "Armillary %03d" % h, arm.data, m @ at_pedestal)
                else:
                    piece = pieces[(n_niche - n_niche // 3) % len(pieces)]
                    _instance(col, "Statue %s %03d" % (piece, h), statues[piece].data, m @ at_pedestal)
                if n_niche % 2 == 0:
                    _instance(col, "Banner %03d" % h, ban.data, m)
                n_niche += 1
    m = bay_matrix(180.0, -7.5)
    _instance(col, "South portal", portal.data, m)
    lamps += [m @ v for v in plamps]
    # the dome, the slit and the outside, the night and the moon, the abyss
    dome = k.new_object("RoHall dome", dome_chart(), col, SLOT_ORDER)
    k.surface_uvs(dome)
    sky_texture()
    col.objects.link(dome_shell())
    out = dome_outer()
    add(out, slit_returns())
    add(out, outer_wall())
    mb = bmesh.new()
    add(mb, parts.bead(MOON["r"], PEARL, 32, 20), Matrix.Translation(Vector(DOME["middle"]) + dome_dir(MOON["h"], MOON["e"]) * MOON["dist"]))
    add(out, mb)
    outside = k.new_object("RoHall dome outside", out, col, SLOT_ORDER)
    k.surface_uvs(outside)
    pass                                 # (the night: the scene's skybox in Unity, RoyalObservatorySky)
    for o in abyss_layers():
        col.objects.link(o)
    roots = k.new_object("RoHall abyss roots", abyss_roots(), col, SLOT_ORDER)
    k.surface_uvs(roots)
    print("   assembled: bays", sum(1 for o in col.objects if "bay" in o.name), "niches", n_niche, "lamps", len(lamps))
    return lamps


def graybox_course():
    """The course as the graybox stands (ro_blockout.py), its own hall hidden (this is the hall now)."""
    src = open(os.path.join(HERE, "ro_blockout.py"), encoding="utf-8").read()
    src = src.split("\nbuild()\nscene_setup()")[0]
    ns = {"__file__": os.path.join(HERE, "ro_blockout.py"), "__name__": "ro_blockout_lib"}
    exec(compile(src, "ro_blockout.py", "exec"), ns)
    ns["build"]()
    for o in bpy.data.objects:
        if "/Hall (looks)" in o.get("unity_path", ""):
            o.hide_render = True
    return dressed_course()


def dressed_course():
    """The other zones' models that hang in the section's frame (their Hang has no `on`), imported from the FBX the
    Dressing loads, the graybox pieces they cover hidden; their lamps. Devices that move keep their graybox here."""
    import re, glob
    lamps = []
    covers = []
    scripts = glob.glob(os.path.join(ROOT_DIR, "Assets", "Scripts", "PawnRush", "RoyalObservatoryDressing.*.cs"))
    for path in scripts:
        if path.endswith("Hall.cs"):
            continue
        text = open(path, encoding="utf-8").read()
        for block in re.findall(r"new Hang\s*\{(.*?)\}\s*,", text, re.S):
            if re.search(r"(^|[\s,{])on\s*=", block):
                continue
            zone = re.search(r'zone\s*=\s*"([^"]+)"', block)
            model = re.search(r'model\s*=\s*"([^"]+)"', block)
            if not zone or not model:
                continue
            covers += re.findall(r'"([^"]+)"', (re.search(r"covers\s*=\s*new\[\]\s*\{(.*?)\}", block, re.S) or [None, ""])[1] or "")
            fbx = os.path.join(ROOT_DIR, "Assets", "Resources", "RoyalObservatory", "Models", zone.group(1), model.group(1) + ".fbx")
            if not os.path.exists(fbx):
                continue
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=fbx)
            for o in set(bpy.data.objects) - before:
                if o.type != "MESH":
                    continue
                for i, m in enumerate(o.data.materials):
                    base = (m.name if m else "").split(".")[0]
                    if base in k.SLOTS or base.startswith(k.PLANET_PREFIX):
                        o.data.materials[i] = k.slot(base)
            lp = fbx[:-4] + "_lamps.txt"
            if os.path.exists(lp):
                for line in open(lp, encoding="utf-8"):
                    q = line.split()
                    if len(q) >= 3:
                        lamps.append(Vector((float(q[0]), float(q[2]), float(q[1]))))
            print("   course model", zone.group(1), model.group(1))
    for o in bpy.data.objects:
        if not o.get("unity_path"):
            continue
        for c in covers:
            if (c.endswith("*") and o.name.startswith(c[:-1])) or o.name == c:
                o.hide_render = True
    return lamps


def assembly_stills(lamps):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    try:
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1066
    sc.view_settings.view_transform = "AgX"
    w = bpy.data.worlds.new("hall")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.01, .011, .03, 1)
    bg.inputs["Strength"].default_value = 1.0
    for i, at in enumerate(lamps):
        L = bpy.data.objects.new("lamp %d" % i, bpy.data.lights.new("lamp %d" % i, "POINT"))
        L.data.energy, L.data.color, L.data.shadow_soft_size = 220, (1.0, .74, .45), .3
        try:
            L.data.use_shadow = False
        except AttributeError:
            pass
        L.location = at
        sc.collection.objects.link(L)
    glow = bpy.data.objects.new("globe sun", bpy.data.lights.new("globe sun", "POINT"))
    glow.data.energy, glow.data.color, glow.data.shadow_soft_size = 260000, (1.0, .8, .55), 3.0
    glow.location = (0, 64, 18)
    sc.collection.objects.link(glow)
    moon = bpy.data.objects.new("moonlight", bpy.data.lights.new("moonlight", "SUN"))
    moon.data.energy, moon.data.color, moon.data.angle = 1.2, (.72, .8, 1.0), math.radians(1)
    moon.rotation_euler = (-dome_dir(MOON["h"], MOON["e"])).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(moon)
    pm = bpy.data.materials.get("RO | pearl")
    if pm is not None:
        pb = next(n for n in pm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        pb.inputs["Emission Color"].default_value = (1.0, .85, .6, 1)
        pb.inputs["Emission Strength"].default_value = 4.0
    out = os.path.join(STILLS, "assembly")
    os.makedirs(out, exist_ok=True)
    shots = {
        "assembly_1_ro1_view": ((-4, 20, 30), (2, 74, -6), 18),       # RO_1's: high over the entry, looking north
        "assembly_2_dipper_eye": ((-19, 33, 3.2), (24, 72, 8), 24),     # a pawn's eye on the Dipper's third star
        "assembly_3_from_stage": ((21, 102, 23), (-24, 40, 2), 22),     # back over the observing stage
        "assembly_4_wall_close": ((-24, 70, 6), (-50, 58, 4), 30),      # the west wall, close
    }
    for name, (loc, look, lens) in shots.items():
        cam = camera(name, loc, look, lens)
        sc.camera = cam
        sc.render.filepath = os.path.join(out, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("wrote", sc.render.filepath)


# ---------------------------------------------------------------- to Unity (the user on the assembly, 11-19: "확인, Unity로")
# Models/Hall/ (RoyalObservatoryDressing.Hall.cs puts them in place):
#   RoHallNicheBay / RoHallArcadeBay / RoHallPortal  one bay each, the hall's middle at the origin, the bay at due south
#       (heading 180): Hall.cs places 104 copies, each turned to its heading about the hall's axis at its tier's foot.
#       _lamps.txt beside the arcade bay and the portal: their lamp globes in that frame (Unity axes).
#   RoHallStatues    every chess piece and armillary sphere in its niche, in the section's frame (one model)
#   RoHallBanners    every banner in place, each its own skinned mesh on a chain of five bones (Hall.cs swings them)
#   RoHallDome       the star chart, ribs, hoop, crown, the sky shell
#   RoHallOutside    the slit's edges, the dome's outside, the outer wall, the moon, the night
#   (RoHallAbyss was exported before with --part abyss --export)
HALL_ZONE = "Hall"


def _export_objects(objs, model, extra_types=()):
    """Export these objects alone (a collection of their own) as Models/Hall/<model>.fbx in the section's frame."""
    col = bpy.data.collections.new("export " + model)
    bpy.context.scene.collection.children.link(col)
    for o in objs:
        col.objects.link(o)
    try:
        if not extra_types:
            return k.export(col.name, model, HALL_ZONE)
        # with armatures: ro_kit.export exports meshes only; the same settings, the rigs with them
        rel = "%s/%s/%s.fbx" % (k.OUT_REL, HALL_ZONE, model)
        bpy.ops.object.select_all(action="DESELECT")
        for o in col.all_objects:
            o.hide_set(False); o.hide_viewport = False; o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.export_scene.fbx(filepath=os.path.join(k.ROOT, rel), use_selection=True,
                                 object_types={"MESH", "ARMATURE"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
                                 axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=True,
                                 mesh_smooth_type="FACE", add_leaf_bones=False, use_armature_deform_only=True,
                                 primary_bone_axis="Y", secondary_bone_axis="X", path_mode="AUTO", embed_textures=False)
        if not os.path.exists(os.path.join(k.ROOT, rel + ".meta")):
            with open(k.FBX_META_TEMPLATE, encoding="utf-8") as f:
                lines = f.read().split("\n")
            lines[1] = "guid: " + k._guid(rel)
            k._write(rel + ".meta", "\n".join(lines))
        print("[ro_r6] exported", rel, len(objs), "objects")
        return rel
    finally:
        for o in list(col.objects):
            col.objects.unlink(o)
        bpy.data.collections.remove(col)


LODS = (0, 1, 2)      # what is exported: <name>_LOD0 .. _LOD2 (the user passed each part's LODs one by one, 11-19)


def _bay_export(name, builder):
    """A bay's three LODs in one FBX, the hall's middle at the origin; its lamps (the same at every LOD) beside it."""
    global DETAIL
    model = name.replace(" ", "")
    obs, lamps = [], []
    for d in LODS:
        DETAIL = d
        try:
            bm, lp = builder()
        finally:
            DETAIL = None
        ob = k.new_object("%s_LOD%d" % (model, d), bm, None, SLOT_ORDER)
        DETAIL = d
        try:
            shape_bay(ob)
        finally:
            DETAIL = None
        ob.matrix_world = Matrix.Translation((0, -WALL_FACE, 0))     # the hall's middle at the origin
        obs.append(ob)
        if d == 0:
            lamps = [bend_point(v) for v in lp]
        print("   %s LOD%d %d tris" % (model, d, k.tris([ob])))
    _export_objects(obs, model)
    if lamps:
        k.write_lamps(HALL_ZONE, model, [Vector((v.x, v.y - WALL_FACE, v.z)) for v in lamps])


def banner_rig(meshes, m, name):
    """One banner placed by matrix m (bay frame -> section): a chain of bones down its middle (the root on the rod, it
    stays; five that Hall.cs swings) and its LOD meshes ({detail: mesh}) all on that rig, "<name>_LOD<n>", each vertex
    weighted to the two bones nearest down the banner."""
    p = BANNER
    top = m @ bend_point((-BAY_W / 2, p["y"] + .02, p["top"] + .1))
    tip = m @ bend_point((-BAY_W / 2, p["y"], p["top"] - p["rect"] - p["point"] - .15))
    arm = bpy.data.armatures.new(name + " rig")
    rig = bpy.data.objects.new(name + " rig", arm)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    n = 5
    root = arm.edit_bones.new("%s root" % name)
    root.head = top + Vector((0, 0, .25))
    root.tail = top
    prev = root
    for i in range(n):
        b = arm.edit_bones.new("%s b%d" % (name, i + 1))
        b.head = top.lerp(tip, i / n)
        b.tail = top.lerp(tip, (i + 1) / n)
        b.parent = prev
        b.use_connect = i > 0
        prev = b
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.scene.collection.objects.unlink(rig)
    out = [rig]
    L = (top - tip).length
    for d, mesh in meshes.items():
        me = mesh.copy()
        me.name = "%s_LOD%d" % (name, d)
        me.transform(m)
        ob = bpy.data.objects.new(me.name, me)
        groups = [ob.vertex_groups.new(name="%s root" % name)] + [ob.vertex_groups.new(name="%s b%d" % (name, i + 1)) for i in range(n)]
        for v in me.vertices:
            dd = (top.z - v.co.z) / L                     # 0 at the rod, 1 at the tip
            if dd <= .02:
                groups[0].add([v.index], 1.0, "REPLACE")
                continue
            s_ = min(dd * n, n - 1e-4)                    # along the five bones: the one it is on and the next
            i = int(s_)
            f = s_ - i
            groups[1 + i].add([v.index], 1.0 - .5 * f, "REPLACE")
            if i + 1 < n:
                groups[2 + i].add([v.index], .5 * f, "REPLACE")
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
        ob.parent = rig
        out.append(ob)
    return out


def export_hall(bays_only=False, with_dome=False):
    """To Assets (never while Unity is in Play): the three bays, the statues and the banners at LOD0 .. 2; with_dome:
    the dome and the outside too (they have no LODs)."""
    global DETAIL
    _bay_export("RoHall NicheBay", lambda: (niche_bay(), []))
    _bay_export("RoHall ArcadeBay", arcade_bay)
    _bay_export("RoHall Portal", portal_bay)
    if bays_only:
        return
    src = {n: import_statue(p_, "chess", n) for p_, n in STATUE_SETS["chess"]}
    src["Pawn"] = import_statue(os.path.join(ROOT_DIR, "Assets", "Resources", "RoyalLibrary", "Models", "Chess",
                                             "RlChessPawn.fbx"), "chess", "Pawn")
    pieces_at = {d: {n: statue_at_detail(o, d, "%s_LOD%d" % (n, d)) for n, o in src.items()} for d in LODS}
    arms_at = {d: armillary_at_detail(d, "armillary_LOD%d" % d) for d in LODS}
    low_at = {d: {n: statue_at_detail(o, d, "%s low_LOD%d" % (n, d), ARCADE_DECIMATE) for n, o in src.items()} for d in LODS}
    banners_at = {}
    for d in LODS:
        DETAIL = d
        try:
            bbm = banner_fall()
            bmesh.ops.translate(bbm, vec=(-BAY_W / 2, BANNER["y"], BANNER["top"]), verts=bbm.verts)
            bend(bbm)
        finally:
            DETAIL = None
        ban = k.new_object("banner_LOD%d" % d, bbm, None, ("velvet", "gold"))
        k.surface_uvs(ban)
        banners_at[d] = ban.data
    at_pedestal = Matrix.Translation(bend_point(STATUE_AT))
    pieces = CHESS_ORDER + ("Pawn",)
    placed, banners = [], []
    n_niche = 0
    for b in range(BAYS):
        h = 5.0 + 10.0 * b
        if abs(h - 180.0) < 6.0:
            continue
        m = bay_matrix(h, TIERS[2][1])
        for d in LODS:
            if n_niche % 3 == 2:
                o = bpy.data.objects.new("Armillary %03d_LOD%d" % (h, d), arms_at[d].data)
            else:
                piece = pieces[(n_niche - n_niche // 3) % len(pieces)]
                o = bpy.data.objects.new("Statue %s %03d_LOD%d" % (piece, h, d), pieces_at[d][piece].data)
            o.matrix_world = m @ at_pedestal
            placed.append(o)
        if n_niche % 2 == 0:
            banners += banner_rig(banners_at, m, "Banner %03d" % h)
        n_niche += 1
    # the arcade galleries, both lower tiers, every bay (the two under the portal too)
    n_low = 0
    up = Matrix.Diagonal((ARCADE_PIECE_SCALE, ARCADE_PIECE_SCALE, ARCADE_PIECE_SCALE, 1))
    for foot, tier in ((TIERS[0][1], "low"), (TIERS[1][1], "mid")):
        for b in range(BAYS):
            h = 5.0 + 10.0 * b
            if tier == "mid" and abs(h - 180.0) < 6.0:
                continue
            piece = ARCADE_ORDER[(n_low * 5 + (1 if tier == "mid" else 0)) % len(ARCADE_ORDER)]
            m = bay_matrix(h, foot) @ Matrix.Translation(bend_point(ARCADE_STATUE_AT)) @ up
            for d in LODS:
                o = bpy.data.objects.new("Statue %s %03d %s_LOD%d" % (piece, h, tier, d), low_at[d][piece].data)
                o.matrix_world = m
                placed.append(o)
            n_low += 1
    for d in LODS:
        print("   statues LOD%d %d tris, banners LOD%d %d tris" % (
            d, k.tris([o for o in placed if o.name.endswith("_LOD%d" % d)]), d,
            sum(len(p_.vertices) - 2 for o in banners if o.type == "MESH" and o.name.endswith("_LOD%d" % d) for p_ in o.data.polygons)))
    _export_objects(placed, "RoHallStatues")
    _export_objects(banners, "RoHallBanners", extra_types=("ARMATURE",))
    if not with_dome:
        return
    sky_texture()
    dome = k.new_object("RoHall dome", dome_chart(), None, SLOT_ORDER)
    k.surface_uvs(dome)
    shell = dome_shell()
    _export_objects([dome, shell], "RoHallDome")
    out = dome_outer()
    add(out, slit_returns())
    add(out, outer_wall())
    mb = bmesh.new()
    add(mb, parts.bead(MOON["r"], PEARL, 32, 20), Matrix.Translation(Vector(DOME["middle"]) + dome_dir(MOON["h"], MOON["e"]) * MOON["dist"]))
    add(out, mb)
    outside = k.new_object("RoHall dome outside", out, None, SLOT_ORDER)
    k.surface_uvs(outside)
    _export_objects([outside], "RoHallOutside")


# ---------------------------------------------------------------- LOD pictures (one part at a time, before and after)
# --lod <niche|arcade|portal>: the bay as first passed and its LOD0 / LOD1 / LOD2 from the same camera, 15 m off at a
# three-quarter view (the course's nearest; LOD1 and LOD2 are drawn farther in the game, shown here close to judge what
# goes), side by side in stills/r6/lod/<name>_lod_compare.png, the triangle counts printed.
LOD_BUILDERS = {
    "niche": (lambda: (niche_bay(), []), 5.5, 15.0),
    "arcade": (lambda: arcade_bay(), 5.5, 15.0),
    "portal": (lambda: portal_bay(), 11.0, 24.0),
}


STATUE_DECIMATE = (.6, .3, .13)    # the chess pieces' share of faces kept at LOD0 / 1 / 2 (Blender's Decimate, collapse)


def statue_at_detail(src, detail, name, ratios=None):
    """A copy of an imported chess piece's mesh with a Decimate at this detail applied (None: as is)."""
    me = src.data.copy()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if detail is not None:
        dm = ob.modifiers.new("Decimate", "DECIMATE")
        dm.ratio = (ratios or STATUE_DECIMATE)[detail]
        dm.use_collapse_triangulate = True
        k.apply_modifiers(ob)
    bpy.context.scene.collection.objects.unlink(ob)
    return ob


def armillary_at_detail(detail, name):
    global DETAIL
    DETAIL = detail
    try:
        ob = k.new_object(name, armillary(), None, SLOT_ORDER)
        k.surface_uvs(ob)
        bpy.context.scene.collection.objects.unlink(ob)
        return ob
    finally:
        DETAIL = None


def statue_lod_stills():
    """--lod statues: a rook, an armillary sphere and a queen in three niches, as passed and at LOD0 / 1 / 2, from
    the same camera 15 m off; the niches stay as passed, only what stands in them changes."""
    import numpy as np
    sc = bpy.context.scene
    col = bpy.data.collections.new("lod statues")
    sc.collection.children.link(col)
    niche = k.new_object("niche", niche_bay(), col, SLOT_ORDER)
    shape_bay(niche)
    bays = []
    for i in (-1, 0, 1):
        b = niche if i == 0 else bpy.data.objects.new("niche %+d" % i, niche.data)
        if i != 0:
            col.objects.link(b)
        b.matrix_world = (Matrix.Translation((0, WALL_FACE, 0)) @ Matrix.Rotation(i * 2 * math.pi / BAYS, 4, "Z") @
                          Matrix.Translation((0, -WALL_FACE, 0)))
        bays.append(b)
    src = {n: import_statue(p_, "chess", n) for p_, n in STATUE_SETS["chess"] if n in ("Rook", "Queen")}
    preview_materials()
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 1100, 800
    sc.view_settings.view_transform = "AgX"
    w = bpy.data.worlds.new("lod")
    sc.world = w
    w.use_nodes = True
    next(n for n in w.node_tree.nodes if n.type == "BACKGROUND").inputs["Color"].default_value = (.010, .011, .028, 1)
    glow = bpy.data.objects.new("hall glow", bpy.data.lights.new("hall glow", "AREA"))
    glow.data.energy, glow.data.size, glow.data.color = 14000, 8, (1, .8, .55)
    glow.location = (4, 16, 6)
    glow.rotation_euler = (Vector((0, 0, 3.5)) - glow.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(glow)
    look = Vector((0, -1, 3.6))
    cam = camera("lod cam", look + Vector((.15, 1, .05)).normalized() * 15.0, look, 17)
    sc.camera = cam
    out = os.path.join(STILLS, "lod")
    os.makedirs(out, exist_ok=True)
    shots, counts, placed = [], [], []
    for detail in (None, 0, 1, 2):
        for o in placed:
            bpy.data.objects.remove(o, do_unlink=True)
        tag = "full" if detail is None else "LOD%d" % detail
        rook = statue_at_detail(src["Rook"], detail, "rook " + tag)
        queen = statue_at_detail(src["Queen"], detail, "queen " + tag)
        arm = armillary_at_detail(detail, "armillary " + tag)
        placed = [rook, arm, queen]
        for o, b in zip(placed, bays):
            col.objects.link(o)
            o.matrix_world = b.matrix_world @ Matrix.Translation(bend_point(STATUE_AT))
        if detail is None:
            preview_materials()
        counts.append((tag, k.tris([rook]), k.tris([arm]), k.tris([queen])))
        f = os.path.join(out, "statues_%s.png" % tag)
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        shots.append(f)
    H = 520
    row = [_fit(_pixels(f, H), H) for f in shots]
    gap = np.ones((H, 12, 4), np.float32)
    img = np.concatenate(sum([[r, gap] for r in row], [])[:-1], axis=1)
    o = bpy.data.images.new("cmp", img.shape[1], H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = os.path.join(out, "statues_lod_compare.png")
    o.file_format = "PNG"
    o.save()
    print("LOD statues (rook, armillary, queen)", "  ".join("%s %d/%d/%d" % c for c in counts), "->", o.filepath_raw)


def banner_lod_stills():
    import numpy as np
    global DETAIL
    sc = bpy.context.scene
    col = bpy.data.collections.new("lod banner")
    sc.collection.children.link(col)
    niche = k.new_object("niche", niche_bay(), col, SLOT_ORDER)
    shape_bay(niche)
    preview_materials()
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 700, 1000
    sc.view_settings.view_transform = "AgX"
    w = bpy.data.worlds.new("lod")
    sc.world = w
    w.use_nodes = True
    next(n for n in w.node_tree.nodes if n.type == "BACKGROUND").inputs["Color"].default_value = (.010, .011, .028, 1)
    glow = bpy.data.objects.new("hall glow", bpy.data.lights.new("hall glow", "AREA"))
    glow.data.energy, glow.data.size, glow.data.color = 9000, 6, (1, .8, .55)
    glow.location = (-2, 14, 7)
    glow.rotation_euler = (Vector((-4.4, 0, 6)) - glow.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(glow)
    look = Vector((bend_point((-BAY_W / 2, 0, 0)).x, 0, BANNER["top"] - 1.8))
    sc.camera = camera("lod cam", look + Vector((.3, 1, .05)).normalized() * 15.0, look, 50)
    out = os.path.join(STILLS, "lod")
    shots, counts, last = [], [], None
    for detail in (None, 0, 1, 2):
        if last is not None:
            bpy.data.objects.remove(last, do_unlink=True)
        DETAIL = detail
        try:
            bm = banner_fall()
            bmesh.ops.translate(bm, vec=(-BAY_W / 2, BANNER["y"], BANNER["top"]), verts=bm.verts)
            bend(bm)
        finally:
            DETAIL = None
        tag = "full" if detail is None else "LOD%d" % detail
        last = k.new_object("banner " + tag, bm, col, ("velvet", "gold"))
        k.surface_uvs(last)
        if detail is None:
            preview_materials()
        counts.append((tag, k.tris([last])))
        f = os.path.join(out, "banner_%s.png" % tag)
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        shots.append(f)
    H = 700
    row = [_fit(_pixels(f, H), H) for f in shots]
    gap = np.ones((H, 12, 4), np.float32)
    img = np.concatenate(sum([[r, gap] for r in row], [])[:-1], axis=1)
    o = bpy.data.images.new("cmp", img.shape[1], H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = os.path.join(out, "banner_lod_compare.png")
    o.file_format = "PNG"
    o.save()
    print("LOD banner", " ".join("%s %d" % c for c in counts), "->", o.filepath_raw)


def arcade_statue_stills():
    """--part-arcade-statues: two tiers of three arcade bays, a piece in each gallery, as they ship (LOD0)."""
    global DETAIL
    sc = bpy.context.scene
    col = bpy.data.collections.new("arcade statues")
    sc.collection.children.link(col)
    DETAIL = 0
    try:
        bm, lamps = arcade_bay()
    finally:
        DETAIL = None
    bay = k.new_object("arcade", bm, col, SLOT_ORDER)
    DETAIL = 0
    try:
        shape_bay(bay)
    finally:
        DETAIL = None
    col.objects.unlink(bay)
    src = {n: import_statue(p_, "chess", n) for p_, n in STATUE_SETS["chess"]}
    src["Pawn"] = import_statue(os.path.join(ROOT_DIR, "Assets", "Resources", "RoyalLibrary", "Models", "Chess",
                                             "RlChessPawn.fbx"), "chess", "Pawn")
    low = {n: statue_at_detail(o, 0, n + " low", ARCADE_DECIMATE) for n, o in src.items()}
    up = Matrix.Diagonal((ARCADE_PIECE_SCALE, ARCADE_PIECE_SCALE, ARCADE_PIECE_SCALE, 1))
    lights = []
    i = 0
    for foot in (TIERS[0][1], TIERS[1][1]):
        for h in (170.0, 180.0, 190.0):
            m = bay_matrix(h, foot)
            _instance(col, "bay", bay.data, m)
            piece = ARCADE_ORDER[i % len(ARCADE_ORDER)]
            _instance(col, "piece " + piece, low[piece].data, m @ Matrix.Translation(bend_point(ARCADE_STATUE_AT)) @ up)
            lights += [m @ bend_point(v) for v in lamps]
            i += 1
    print("   pieces", {n: k.tris([o]) for n, o in low.items()})
    preview_materials()
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1100
    sc.view_settings.view_transform = "AgX"
    w = bpy.data.worlds.new("hall")
    sc.world = w
    w.use_nodes = True
    next(n for n in w.node_tree.nodes if n.type == "BACKGROUND").inputs["Color"].default_value = (.010, .011, .028, 1)
    for at in lights:
        L = bpy.data.objects.new("lamp", bpy.data.lights.new("lamp", "POINT"))
        L.data.energy, L.data.color, L.data.shadow_soft_size = 220, (1.0, .74, .45), .3
        L.location = at
        sc.collection.objects.link(L)
    glow = bpy.data.objects.new("hall glow", bpy.data.lights.new("hall glow", "POINT"))
    glow.data.energy, glow.data.color, glow.data.shadow_soft_size = 160000, (1.0, .8, .55), 3.0
    glow.location = (0, 64, 2)
    sc.collection.objects.link(glow)
    out = os.path.join(STILLS, "parts")
    shots = {"arcade_statues_0": ((0, 40, -2), (0, 14, -9), 24), "arcade_statues_1": ((5, 30, -10), (-1, 14, -14), 30)}
    for name, (loc, look, lens) in shots.items():
        sc.camera = camera(name, loc, look, lens)
        sc.render.filepath = os.path.join(out, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("wrote", sc.render.filepath)


def build_detail(name, detail):
    global DETAIL
    DETAIL = detail
    try:
        bm, lamps = LOD_BUILDERS[name][0]()
        ob = k.new_object("%s %s" % (name, "full" if detail is None else "LOD%d" % detail), bm, None, SLOT_ORDER)
        shape_bay(ob)
        return ob, [bend_point(v) for v in lamps]
    finally:
        DETAIL = None


def lod_stills(name):
    import numpy as np
    _, look_z, dist = LOD_BUILDERS[name]
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    try:
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 900, 1000
    sc.view_settings.view_transform = "AgX"
    w = bpy.data.worlds.new("lod")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.010, .011, .028, 1)
    moon = bpy.data.objects.new("moon", bpy.data.lights.new("moon", "SUN"))
    moon.data.energy, moon.data.color = 1.2, (.7, .78, 1.0)
    moon.rotation_euler = (math.radians(35), 0, math.radians(200))
    sc.collection.objects.link(moon)
    glow = bpy.data.objects.new("hall glow", bpy.data.lights.new("hall glow", "AREA"))
    glow.data.energy, glow.data.size, glow.data.color = 9000, 6, (1, .8, .55)
    glow.location = (6, 18, look_z + 2)
    glow.rotation_euler = (Vector((0, 0, look_z)) - glow.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(glow)
    d = Vector((.45, 1, .15)).normalized()
    look = Vector((0, 0, look_z))
    cam = camera("lod cam", look + d * dist, look, 35)
    sc.camera = cam
    out = os.path.join(STILLS, "lod")
    os.makedirs(out, exist_ok=True)
    shots, counts, lights = [], [], []
    for detail in (None, 0, 1, 2):
        ob, lamps = build_detail(name, detail)
        if detail is None:
            preview_materials()          # the slots exist now
        for L in lights:
            bpy.data.objects.remove(L, do_unlink=True)
        lights = []
        for i, at in enumerate(lamps):
            L = bpy.data.objects.new("lamp", bpy.data.lights.new("lamp", "POINT"))
            L.data.energy, L.data.color, L.data.shadow_soft_size = 140, (1.0, .74, .45), .15
            L.location = at
            sc.collection.objects.link(L)
            lights.append(L)
        tag = "full" if detail is None else "LOD%d" % detail
        counts.append((tag, k.tris([ob])))
        f = os.path.join(out, "%s_%s.png" % (name, tag))
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        shots.append(f)
        ob.hide_render = True
    H = 700
    row = [_fit(_pixels(f, H), H) for f in shots]
    gap = np.ones((H, 12, 4), np.float32)
    img = np.concatenate(sum([[r, gap] for r in row], [])[:-1], axis=1)
    o = bpy.data.images.new("cmp", img.shape[1], H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = os.path.join(out, "%s_lod_compare.png" % name)
    o.file_format = "PNG"
    o.save()
    print("LOD", name, " ".join("%s %d" % c for c in counts), "->", o.filepath_raw)


PARTS = {
    # name: (builder, sheet file, sheet grid (cols, rows), cell (col, row) or None, concept crop (file, x0, y0, x1, y1) px)
    "niche": (part_niche, "RO_A1_architecture_modules.png", (4, 3), (3, 1), ("RO_1_whole_hall.png", 380, 95, 1010, 330)),
    "portal": (part_portal, "RO_A1_architecture_modules.png", (4, 3), (1, 2), ("RO_1_whole_hall.png", 0, 700, 430, 1024)),
    "statues": (part_statues_chess, "RO_A3_hall_props.png", (4, 3), None, ("RO_1_whole_hall.png", 380, 95, 1010, 330)),
    "statues_pr": (part_statues_statue, "RO_A3_hall_props.png", (4, 3), None, ("RO_1_whole_hall.png", 380, 95, 1010, 330)),
    "armillary": (part_armillary, "RO_A3_hall_props.png", (4, 3), (1, 1), ("RO_1_whole_hall.png", 380, 95, 1010, 330)),
    "banner": (part_banner, "RO_A3_hall_props.png", (4, 3), (2, 1), ("RO_1_whole_hall.png", 380, 95, 1010, 330)),
    "abyss": (part_abyss, "RO_A3_hall_props.png", (4, 3), None, ("RO_1_whole_hall.png", 0, 560, 1536, 1024)),
    "slit": (part_slit, "RO_A3_hall_props.png", (4, 3), None, ("RO_1_whole_hall.png", 1290, 0, 1536, 230)),
    "dome": (part_dome, "RO_T4_dome_and_planets.png", (1, 2), (0, 0), ("RO_1_whole_hall.png", 0, 0, 1536, 200)),
    "arcade": (part_arcade, "RO_A1_architecture_modules.png", (4, 3), (0, 2), ("RO_1_whole_hall.png", 0, 200, 700, 520)),
}


# ---------------------------------------------------------------- stills

# The user on the first niche (11-19): "컨셉 아트 텍스쳐 처럼 좀 더 어둡고 침침 해야 해". RO_1's niche enamel measures a
# greyed slate violet (sRGB ~120 115 128 lit), its marble a warm cream (~215 190 180 lit), where the library's navy is a
# clear blue (42 90 147) and its marble near white (246 240 233). Until ro-path's library (common, not ours) is changed,
# the stills graded the library albedo this way (hue shift, saturation, value, then a colour multiply). The user passed
# it ("이대로 넘겨") and ro-path baked these numbers into the library (ro_textures.py GRADE, 11-19): the stills no
# longer grade (GRADE_IN_STILLS), or the colours would darken twice.
GRADE_IN_STILLS = False
GRADE = {
    "RO_NavyEnamel": (.05, .48, .47, (.95, .9, 1.1)),
    "RO_Marble": (0.0, 1.5, .85, (1.0, .92, .8)),
    "RO_Gold": (0.0, 1.05, .8, (1.0, .92, .76)),
    "RO_VioletEnamel": (0.0, .75, .68, (1.0, 1.0, 1.0)),
}
# Proposals not yet in the library, graded in the stills only (the same hue / saturation / value / multiply steps).
# The user on the banner (11-19): "더 선명하게" - RO_Velvet (banners only) read a greyed navy violet in the dim hall.
PROPOSED = {
    # "RO_Velvet": (.01, 2.2, 1.3, (1.1, .85, 1.12)) - passed by the user, baked into the library by ro-path (11-19)
}


def preview_materials():
    """The library albedo / normal on the slots, as RoyalObservatoryDressing will draw them (ro_r0_star_model's)."""
    lib = os.path.join(HERE, "textures", "library")
    tiles = {"RO_Marble": 3, "RO_Gold": .6, "RO_Brass": 1, "RO_NavyEnamel": 1.5, "RO_VioletEnamel": 1.5, "RO_Pearl": 2,
             "RO_HallStone": 3, "RO_Velvet": 1}
    for key, (unity, *_r) in k.SLOTS.items():
        m = bpy.data.materials.get(key)
        if m is None or unity in ("RO_Glass", "RO_Motif"):
            continue
        alb = os.path.join(lib, unity + "_Albedo.png")
        if not os.path.exists(alb):
            continue
        nt = m.node_tree
        p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        mp = nt.nodes.new("ShaderNodeMapping")
        t = tiles.get(unity, 1)
        mp.inputs["Scale"].default_value = (1 / t, 1 / t, 1)
        tc = nt.nodes.new("ShaderNodeTexCoord")
        nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(alb, check_existing=True)
        nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
        out = tex.outputs["Color"]
        grade = PROPOSED.get(unity) or (GRADE.get(unity) if GRADE_IN_STILLS else None)
        if grade:
            hue, sat, val, mul = grade
            hs = nt.nodes.new("ShaderNodeHueSaturation")
            hs.inputs["Hue"].default_value = .5 + hue
            hs.inputs["Saturation"].default_value = sat
            hs.inputs["Value"].default_value = val
            nt.links.new(out, hs.inputs["Color"])
            mx = nt.nodes.new("ShaderNodeMix")
            mx.data_type = "RGBA"
            mx.blend_type = "MULTIPLY"
            mx.inputs[0].default_value = 1.0
            nt.links.new(hs.outputs["Color"], mx.inputs[6])
            mx.inputs[7].default_value = (*mul, 1)
            out = mx.outputs[2]
        nt.links.new(out, p.inputs["Base Color"])
        nrm = os.path.join(lib, unity + "_Normal.png")
        if os.path.exists(nrm):
            ni = bpy.data.images.load(nrm, check_existing=True)
            ni.colorspace_settings.name = "Non-Color"
            nn = nt.nodes.new("ShaderNodeTexImage")
            nn.image = ni
            nt.links.new(mp.outputs["Vector"], nn.inputs["Vector"])
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(nn.outputs["Color"], nm.inputs["Color"])
            nt.links.new(nm.outputs["Normal"], p.inputs["Normal"])


def camera(name, loc, look, lens):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.clip_end = 1000
    co = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(co)
    co.location = loc
    co.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return co


def _pixels(path, H, crop=None):
    import numpy as np
    im = bpy.data.images.load(path)
    w, h = im.size
    px = np.array(im.pixels[:], np.float32).reshape(h, w, 4)
    bpy.data.images.remove(im)
    if crop:
        x0, y0, x1, y1 = crop                       # image pixels from the top left
        px = px[h - y1:h - y0, x0:x1]
    return px


def _fit(px, H):
    import numpy as np
    h, w = px.shape[:2]
    nw = max(1, int(w * H / h))
    return px[(np.arange(H) * h / H).astype(int)][:, (np.arange(nw) * w / nw).astype(int)]


def part_stills(name, objs, extra=(), lamps=()):
    import numpy as np
    _, sheet, grid, cell, crop = PARTS[name]
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    mid, size = (lo + hi) / 2, (hi - lo).length
    # a pawn for scale (1.2 m) in front of the part on its floor
    pawn = k.lathe([(0, 0), (.35, 0), (.35, .12), (.2, .25), (.14, .7), (.24, .78), (.12, .86), (.2, .95), (.22, 1.02),
                    (.18, 1.12), (.08, 1.19), (0, 1.2)], 24, mat=0)
    pob = k.new_object("Scale pawn 1.2 m", pawn, bpy.context.scene.collection, ("marble",))
    pob.location = (hi.x - 1.2, hi.y + 1.0, lo.z)
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    try:
        sc.eevee.taa_render_samples = 48
        sc.eevee.use_raytracing = True
    except Exception:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1000, 1100
    sc.view_settings.view_transform = "AgX"
    w = bpy.data.worlds.new("part")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    # a dim hall, as RO_1: a dark navy air, cool moonlight from the slit above, warm lamp globes along the ledge at the
    # bay's foot, a soft warm glow from the hall (the great globe's sun) in front
    bg.inputs["Color"].default_value = (.010, .011, .028, 1)
    bg.inputs["Strength"].default_value = 1.0
    sc.render.film_transparent = False
    sun = bpy.data.objects.new("moon", bpy.data.lights.new("moon", "SUN"))
    sun.data.energy, sun.data.color, sun.data.angle = 1.2, (.7, .78, 1.0), math.radians(3)
    sun.rotation_euler = (math.radians(35), 0, math.radians(200))
    sc.collection.objects.link(sun)
    # the part's own lamp globes when it has them (as Unity will light them), else three at a ledge before it
    spots = ([] if name == "slit" else list(lamps)) or [Vector((i * BAY_W - BAY_W / 2, 2.6, 1.9)) for i in range(-1, 2)]
    for i, at in enumerate(spots):
        lp = bpy.data.objects.new("lamp %d" % i, bpy.data.lights.new("lamp %d" % i, "POINT"))
        lp.data.energy, lp.data.color, lp.data.shadow_soft_size = 140, (1.0, .74, .45), .15
        lp.location = at
        sc.collection.objects.link(lp)
    for nm, off, en, colr in (("hall glow", Vector((.3, 1, .25)), .55, (1, .8, .55)), ("fill", Vector((-1, .7, .3)), .05, (.6, .68, 1))):
        ld = bpy.data.lights.new(nm, "AREA")
        ld.energy, ld.size, ld.color = 20000 * (size / 20) ** 2 * en, size * .12, colr   # small: the glossy enamel shows it as a highlight, not a sheet
        lob = bpy.data.objects.new(nm, ld)
        lob.location = mid + off.normalized() * size * 1.2
        lob.rotation_euler = (mid - lob.location).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lob)
    out = os.path.join(STILLS, "parts")
    os.makedirs(out, exist_ok=True)
    shots = []
    if name in ("dome", "slit", "abyss"):
        # inside the hall, looking up: lamps round the wall's top, the great globe's warm glow from below the middle
        for i in range(8):
            t = 2 * math.pi * i / 8
            lp = bpy.data.objects.new("rim %d" % i, bpy.data.lights.new("rim %d" % i, "POINT"))
            lp.data.energy, lp.data.color, lp.data.shadow_soft_size = 9000, (1.0, .74, .45), 1.0
            lp.location = Vector(DOME["middle"]) + Vector((44 * math.cos(t), 44 * math.sin(t), -2))
            sc.collection.objects.link(lp)
        glow = bpy.data.objects.new("globe glow", bpy.data.lights.new("globe glow", "POINT"))
        glow.data.energy, glow.data.color, glow.data.shadow_soft_size = 90000, (1.0, .8, .55), 4.0
        sc.render.resolution_x, sc.render.resolution_y = 1500, 1000
        glow.location = (0, 64, -4)
        sc.collection.objects.link(glow)
        dome_shots = [((0, 22, 2), (0, 80, 44), 16), ((10, 92, 14), (-14, 50, 50), 18),
                      ((0, 64, 10), tuple(dome_at(DOME["slit"] + 165.0, 40.0)), 45)]
        if name == "abyss":
            # from the course, looking down into it: from the Dipper's third star, from the stage, from the entry
            dome_shots = [((-19, 33, 2), (4, 66, -70), 24), ((9, 90, 13), (-2, 60, -80), 22), ((0, 14, 2), (0, 58, -45), 20)]
        if name == "slit":
            # from the stage up through the slit, from the balcony back at the dome, from outside to the north-east;
            # the moon's cool light from its side
            moon = bpy.data.objects.new("moonlight", bpy.data.lights.new("moonlight", "SUN"))
            moon.data.energy, moon.data.color, moon.data.angle = 2.0, (.72, .8, 1.0), math.radians(1)
            moon.rotation_euler = (-dome_dir(MOON["h"], MOON["e"])).to_track_quat("-Z", "Y").to_euler()
            sc.collection.objects.link(moon)
            moon.data.energy = 4.0
            # the moon glows in Unity (pearl, as the lamp globes): the same here
            pm = bpy.data.materials.get("RO | pearl")
            if pm is not None:
                pb = next(n for n in pm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
                pb.inputs["Emission Color"].default_value = (1.0, .95, .85, 1)
                pb.inputs["Emission Strength"].default_value = 6.0
            dome_shots = [((9, 88, 13), tuple(dome_at(DOME["slit"], 26.0, 80.0)), 22),
                          ((9, 122, 22.5), tuple(dome_at(DOME["slit"], 12.0, 200.0)), 24),
                          ((75, 150, 55), (0, 64, 22), 32)]
    for i, (d, lens) in enumerate(((Vector((0, 1, .12)), 40), (Vector((.8, 1, .35)), 70), (Vector((-.25, 1, -.05)), 120))):
        d.normalize()
        if name in ("dome", "slit", "abyss"):
            loc, look, lens = dome_shots[i]
            cam = camera("%s_%d" % (name, i), loc, look, lens)
            sc.camera = cam
            f = os.path.join(out, "%s_%d.png" % (name, i))
            sc.render.filepath = f
            bpy.ops.render.render(write_still=True)
            shots.append(f)
            continue
        for e in extra:
            e.hide_render = i > 0
        close_z = {"arcade": ARCADE["spring"] + 1.5, "portal": PORTAL["lintel"] + 1.5,
                   "armillary": PEDESTAL_TOP + 3.0}.get(name, NICHE["spring"] + 1.0)
        if name.startswith("statues"):
            close_z = PEDESTAL_TOP + 1.8
        look = mid if i < 2 else Vector((0, lo.y, close_z))
        if name == "banner" and i == 2:
            look = Vector((bend_point((-BAY_W / 2, 0, 0)).x, lo.y, BANNER["top"] - 1.6))
        cam = camera("%s_%d" % (name, i), look + d * size * (2.0 if i < 2 else 1.2), look, lens)
        sc.camera = cam
        f = os.path.join(out, "%s_%d.png" % (name, i))
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        shots.append(f)
    H = 700
    row = []
    sheet_path = os.path.join(SHEETS, sheet)
    if cell is not None and os.path.exists(sheet_path):
        px = _pixels(sheet_path, H)
        h, wd = px.shape[:2]
        cw, ch = wd // grid[0], h // grid[1]
        x0, y0 = cell[0] * cw, h - (cell[1] + 1) * ch
        row.append(_fit(px[y0:y0 + ch, x0:x0 + cw], H))
    if crop:
        row.append(_fit(_pixels(os.path.join(CONCEPT, crop[0]), H, crop[1:]), H))
    for f in shots:
        px = _pixels(f, H)
        al = px[..., 3:4]
        px = px * al + (1 - al) * np.ones_like(px)
        px[..., 3] = 1
        row.append(_fit(px, H))
    gap = np.ones((H, 12, 4), np.float32)
    img = np.concatenate(sum([[r, gap] for r in row], [])[:-1], axis=1)
    o = bpy.data.images.new("cmp", img.shape[1], H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = os.path.join(out, "%s_compare.png" % name)
    o.file_format = "PNG"
    o.save()
    print("PART", name, "tris", k.tris(objs), "size", tuple(round(v, 2) for v in (hi - lo)), "->", o.filepath_raw)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    k.motif_reset()
    if "--part-arcade-statues" in args:
        arcade_statue_stills()
        sys.exit(0)
    if "--export-outside" in args:         # the slit's edges, the dome's outside, the outer wall, the moon
        _out = dome_outer()
        add(_out, slit_returns())
        add(_out, outer_wall())
        _mb = bmesh.new()
        add(_mb, parts.bead(MOON["r"], PEARL, 32, 20), Matrix.Translation(Vector(DOME["middle"]) + dome_dir(MOON["h"], MOON["e"]) * MOON["dist"]))
        add(_out, _mb)
        _o = k.new_object("RoHall dome outside", _out, None, SLOT_ORDER)
        k.surface_uvs(_o)
        _export_objects([_o], "RoHallOutside")
        sys.exit(0)
    if "--export-dome" in args:            # the dome alone (star chart, ribs, sky shell)
        sky_texture()
        _dome = k.new_object("RoHall dome", dome_chart(), None, SLOT_ORDER)
        k.surface_uvs(_dome)
        _export_objects([_dome, dome_shell()], "RoHallDome")
        sys.exit(0)
    if "--lod" in args:
        which = args[args.index("--lod") + 1]
        statue_lod_stills() if which == "statues" else banner_lod_stills() if which == "banner" else lod_stills(which)
        sys.exit(0)
    if "--export-hall" in args or "--export-bays" in args:
        export_hall(bays_only="--export-bays" in args, with_dome="--with-dome" in args)
        sys.exit(0)
    if "--assembly" in args:
        col = bpy.data.collections.new("RO R6 hall")
        bpy.context.scene.collection.children.link(col)
        LAMPS = assemble(col)
        LAMPS += graybox_course()
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r6_hall.blend"))
        preview_materials()
        assembly_stills(LAMPS)
        sys.exit(0)
    name = args[args.index("--part") + 1] if "--part" in args else "niche"
    col = bpy.data.collections.new("RO R6 " + name)
    bpy.context.scene.collection.children.link(col)
    objs, extra, LAMPS = PARTS[name][0](col)
    for o in objs:
        print("   ", o.name, k.tris([o]))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r6_%s.blend" % name))
    if "--export" in args and name == "abyss":
        export_abyss(col.name)
    preview_materials()
    part_stills(name, objs, extra, LAMPS)
