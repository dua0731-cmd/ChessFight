# I King's Bedchamber props (R511), modelled here in Blender in detail (user, 11-08: "사진 소모품들은 너가 진짜 디테일하게
# 작업해"), after the GPT sheet Docs/PawnRush/ConceptArt/R511_KingsBedchamber/AssetRefs/I_assets_02_props.png:
# nightstand with an unlit candlestick and a book, wardrobe, washstand with basin and jug, slippers, folded ermine robe,
# dressing screen. Not sent to Tripo.
# The modelling helpers are E's (Mid/e_props_v1.py: bevelled boxes, cylinders, turned parts, rings, grain UVs), imported and
# not edited, as J's j_laundry_props_v1.py does. Stylised like the concept: chunky rounded bevels, raised panels, mouldings.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python i_props_v1.py -- [--render] [--export]
#
# Materials: walnut = PR_Late_I_PropWalnut (made here like E's seamless furniture grain, warmer golden brown); velvet = R511 PR_Late_I_Velvet (the concept's plum
# violet); brass = R504 PR_Late_Brass; marble = R504 PR_Late_MarbleCream; ermine = R504 PR_Late_Ermine; the rest plain
# colours PR_Late_I_Prop* (ceramic, wax, wick, cream fur, book pages).
# No flame: the candle is a body only, its light is added in Unity (11-07 rule). SOCKET_CandleFlame marks the wick tip.
# Output (front = Blender -y, up = +z, origin floor centre):
#   Late/Export/I/I_PropsEnv.fbx  all props but the robe, placed in the section frame (Blender x = section x, y = section z)
#                                 along the entrance wall corners, off the pawns' ways (|x| > 14.8, z < 2.4)
#   Late/Export/I/I_Robe.fbx      the folded robe alone (Unity lays it on F_Bench)
#   Late/Export/I/I_Prop_<Name>.fbx  every prop alone, for other placements
import bpy, bmesh, math, os, random, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
LATE = os.path.dirname(HERE)
MID = os.path.join(os.path.dirname(LATE), "Mid")
sys.path.insert(0, MID)
import e_props_v1 as P   # noqa: E402
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
LTEX = os.path.join(LATE, "Materials", "Textures")
OUT = os.path.join(LATE, "Export", "I")

COLORS = {"PR_Late_I_PropCeramic": (.80, .70, .52), "PR_Late_I_PropWax": (.86, .78, .60),
          "PR_Late_I_PropWick": (.05, .04, .03), "PR_Late_I_PropFur": (.84, .76, .62),
          "PR_Late_I_PropPage": (.86, .78, .60), "PR_Late_I_PropSole": (.20, .11, .06)}


def walnut_texture():
    """Seamless furniture walnut like E's (u along the grain 1 m, v across 0.5 m, every term periodic), but warmer and
    lighter golden brown with a finer grain, as on the I concept sheets (E's reads too red here)."""
    name = "PR_Late_I_PropWalnut_BaseColor.png"
    H, Wd = 512, 1024
    u = np.linspace(0, 1, Wd, endpoint=False)[None, :]; v = np.linspace(0, 1, H, endpoint=False)[:, None]
    rng = np.random.default_rng(23)
    warp = sum(a * np.sin(2 * np.pi * (fu * u + fv * v) + rng.uniform(0, 6.3))
               for a, fu, fv in ((.012, 1, 1), (.007, 2, 3), (.004, 3, 1), (.003, 5, 7)))
    rings = .5 + .5 * np.sin(2 * np.pi * (37 * (v + warp)))
    fine = .5 + .5 * np.sin(2 * np.pi * (151 * (v + .4 * warp) + 2 * np.sin(2 * np.pi * u)))
    pores = sum(np.sin(2 * np.pi * (fu * u + fv * v) + rng.uniform(0, 6.3)) for fu, fv in ((61, 211), (89, 173), (127, 97))) / 3
    t = np.clip(.28 * rings ** 3 + .22 * fine + .06 * pores + .12, 0, 1)[..., None]
    light, dark = np.array([.72, .48, .29]), np.array([.47, .29, .16])       # sRGB warm golden walnut
    rgb = light * (1 - t) + dark * t
    img = bpy.data.images.new("PR_Late_I_PropWalnut_BaseColor", Wd, H, alpha=False)
    px = np.ones((H, Wd, 4), np.float32); px[..., :3] = rgb ** 2.2
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(LTEX, name); img.file_format = 'PNG'; img.save()
    return name


def WAL():
    if "PR_Late_I_PropWalnut" not in P.MATS: walnut_texture()
    return P.mat("PR_Late_I_PropWalnut", tex=os.path.join(LTEX, "PR_Late_I_PropWalnut_BaseColor.png"), rough=.55)
def BRS(): return P.mat("PR_Late_Brass", tex=os.path.join(LTEX, "PR_Late_Brass_BaseColor.png"), rough=.45, metal=.85)
def VEL(): return P.mat("PR_Late_I_Velvet", tex=os.path.join(LTEX, "PR_Late_I_Velvet_BaseColor.png"), rough=.9)
def MAR(): return P.mat("PR_Late_MarbleCream", tex=os.path.join(LTEX, "PR_Late_MarbleCream_BaseColor.png"), rough=.3)
def ERM(): return P.mat("PR_Late_Ermine", tex=os.path.join(LTEX, "PR_Late_Ermine_BaseColor.png"), rough=.95)
def PC(name): return P.mat(name, COLORS[name], rough=.25 if "Ceramic" in name else .85)


# ------------------------------------------------------------------ extra geometry
def arch_pts(w, h, n=20, z0=0.):
    """Closed outline in the xz plane: a w-wide panel h high with a round top (radius w/2), bottom at z0."""
    r = w / 2; zc = z0 + h - r
    pts = [(-r, z0), (-r, zc)]
    pts += [(r * math.cos(math.pi - math.pi * i / n), zc + r * math.sin(math.pi - math.pi * i / n)) for i in range(1, n)]
    pts += [(r, zc), (r, z0)]
    return pts


def solid(loop, y0, y1, m, bev=.006, grain=2):
    """Extrude a closed convex xz outline between y0 and y1."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in loop]
    b = [bm.verts.new((x, y1, z)) for x, z in loop]
    bm.faces.new(a[::-1]); bm.faces.new(b)
    n = len(loop)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    P.bevel_edges(bm, [e for e in bm.edges if e.calc_face_angle(0) > .5], bev)
    o = P.to_obj(bm, m, "solid")
    P.uv(o, grain)
    return o


def ring_solid(outer, inner, y0, y1, m, bev=.005, grain=2):
    """A frame between two closed xz outlines with the same point count, extruded from y0 to y1."""
    bm = bmesh.new()
    n = len(outer)
    L = {k: [bm.verts.new((x, y, z)) for x, z in pts] for k, pts, y in
         (("o0", outer, y0), ("o1", outer, y1), ("i0", inner, y0), ("i1", inner, y1))}
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((L["o0"][j], L["o0"][i], L["i0"][i], L["i0"][j]))      # back face
        bm.faces.new((L["o1"][i], L["o1"][j], L["i1"][j], L["i1"][i]))      # front face
        bm.faces.new((L["o0"][i], L["o0"][j], L["o1"][j], L["o1"][i]))      # outer side
        bm.faces.new((L["i0"][j], L["i0"][i], L["i1"][i], L["i1"][j]))      # inner side
    P.bevel_edges(bm, [e for e in bm.edges if e.calc_face_angle(0) > .5], bev)
    o = P.to_obj(bm, m, "frame")
    P.uv(o, grain)
    return o


def moulding(x0, x1, y0, y1, z, steps, m, grain=0):
    """Stacked bevelled slabs (bottom to top) making a profiled edge: steps = [(outset, height)]."""
    p = []
    for out, h in steps:
        p.append(P.box((x0 - out, y0 - out, z), (x1 + out, y1 + out, z + h), m, bev=min(.012, h * .45), grain=grain))
        z += h
    return p, z


def knob(at, r, m):
    x, y, z = at
    k = P.lathe([(0, 0), (r * .45, 0), (r * .35, r * .5), (r * .9, r * .9), (r, r * 1.35), (r * .7, r * 1.8), (0, r * 1.9)], m, segs=18)
    k.data.transform(Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
    return k


def bun(x, y, r, h, m):
    return P.lathe([(0, 0), (r * .7, 0), (r, h * .35), (r * .95, h * .7), (r * .6, h * .95), (r * .55, h), (0, h)], m, segs=20, at=(x, y, 0))


def rough_up(o, amt, seed, scale=1.):
    """Soft random bulge on every vertex (cloth, fur)."""
    rng = np.random.default_rng(seed)
    ph = rng.uniform(0, 6.3, 6)
    for v in o.data.vertices:
        c = v.co * scale
        d = (math.sin(c.x * 9 + ph[0]) * math.sin(c.y * 11 + ph[1]) * math.sin(c.z * 13 + ph[2]) +
             .5 * math.sin(c.x * 23 + ph[3]) * math.sin(c.y * 19 + ph[4] + c.z * 17))
        v.co += v.normal * amt * d
    return o


def sweep(path, r, m, rsegs=12, closed=False):
    """A smooth tube of radius r along a list of points (rolled fur, collars); open ends get round caps."""
    path = [Vector(q) for q in path]
    n = len(path)
    bm = bmesh.new()
    rings, prev = [], Vector((1, 0, 0))
    for i, c in enumerate(path):
        a = path[(i - 1) % n] if (closed or i > 0) else c
        b = path[(i + 1) % n] if (closed or i < n - 1) else c
        t = (b - a).normalized()
        side = t.cross(Vector((0, 0, 1)))
        side = prev if side.length < 1e-4 else side.normalized()
        prev = side
        up = side.cross(t).normalized()
        rings.append([bm.verts.new(c + r * (math.cos(2 * math.pi * k / rsegs) * side + math.sin(2 * math.pi * k / rsegs) * up))
                      for k in range(rsegs)])
    for i in range(n if closed else n - 1):
        A, B = rings[i], rings[(i + 1) % n]
        for k in range(rsegs):
            bm.faces.new((A[k], A[(k + 1) % rsegs], B[(k + 1) % rsegs], B[k]))
    if not closed:
        for ring, end, d in ((rings[0], path[0], path[0] - path[1]), (rings[-1], path[-1], path[-1] - path[-2])):
            tip = bm.verts.new(end + d.normalized() * r * .8)
            for k in range(rsegs):
                bm.faces.new((ring[(k + 1) % rsegs], ring[k], tip))
    o = P.to_obj(bm, m, "tube")
    P.uv(o, None)
    return o


def uv_scale(o, k):
    for d in o.data.uv_layers[P.UVN].data:
        d.uv = (d.uv[0] * k, d.uv[1] * k)
    return o


def subdivide(o, cuts=1, smooth=1.):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True, smooth=smooth)
    bm.to_mesh(o.data); bm.free()
    return o


# ------------------------------------------------------------------ props (origin floor centre, front -y)
def nightstand():
    W_, D, H = .56, .46, .60
    w, br = WAL(), BRS()
    p = [bun(x, y, .045, .07, w) for x in (-.22, .22) for y in (-.17, .17)]
    m, z = moulding(-W_ / 2 + .02, W_ / 2 - .02, -D / 2 + .02, D / 2 - .02, .07, [(.012, .03), (.0, .012)], w)
    p += m
    p.append(P.box((-W_ / 2 + .03, -D / 2 + .03, z), (W_ / 2 - .03, D / 2 - .03, H - .05), w, bev=.012, grain="long"))
    for s in (-1, 1):                                     # side panels: frame + raised field
        x = s * (W_ / 2 - .03)
        p.append(P.box((x - .008, -D / 2 + .08, z + .06), (x + .008, D / 2 - .08, H - .11), w, bev=.006, grain=1))
        p.append(P.box((x - .016 * (s < 0) - .0, -D / 2 + .11, z + .09), (x + .016 * (s > 0), D / 2 - .11, H - .14), w, bev=.008, grain=1))
    yf = -D / 2 + .03
    p.append(P.box((-W_ / 2 + .05, yf - .012, z + .05), (W_ / 2 - .05, yf, H - .1), w, bev=.008, grain=0))     # face frame
    p.append(P.box((-W_ / 2 + .075, yf - .03, z + .08), (W_ / 2 - .075, yf - .012, H - .13), w, bev=.01, grain=0))   # drawer
    p.append(P.box((-W_ / 2 + .11, yf - .042, z + .11), (W_ / 2 - .11, yf - .03, H - .16), w, bev=.012, grain=0))   # raised field
    p.append(knob((0, yf - .042, (z + H - .1) / 2 + .005), .022, br))
    p.append(P.box((-.018, yf - .046, (z + H - .1) / 2 - .05), (.018, yf - .04, (z + H - .1) / 2 - .03), br, bev=.003))  # keyhole plate
    m, z = moulding(-W_ / 2, W_ / 2, -D / 2, D / 2, H - .05, [(.0, .015), (.018, .02), (.01, .015)], w, grain="long")
    p += m
    top = z
    # candlestick (turned brass, drip pan) and an unlit candle, back left
    cx, cy = -.13, .06
    p.append(P.lathe([(0, 0), (.075, 0), (.08, .008), (.07, .02), (.03, .03), (.022, .06), (.034, .075), (.02, .09),
                      (.016, .17), (.03, .185), (.016, .2), (.016, .23), (.05, .235), (.055, .245), (.022, .25), (0, .25)],
                     br, segs=24, at=(cx, cy, top)))
    p.append(P.lathe([(0, 0), (.019, 0), (.019, .16), (.016, .168), (.008, .171), (0, .171)], PC("PR_Late_I_PropWax"),
                     segs=18, at=(cx, cy, top + .245)))
    for a, h in ((0.3, .05), (2.4, .035), (4.4, .065)):    # wax drips
        p.append(P.cyl((cx + .019 * math.cos(a), cy + .019 * math.sin(a), top + .41 - h), (cx + .019 * math.cos(a), cy + .019 * math.sin(a), top + .413),
                       .006, PC("PR_Late_I_PropWax"), segs=8, bev=.002, r2=.004, grain=False))
    p.append(P.cyl((cx, cy, top + .414), (cx, cy, top + .432), .0025, PC("PR_Late_I_PropWick"), segs=6, bev=0, grain=False))
    sock = bpy.data.objects.new("SOCKET_CandleFlame", None); sock.location = (cx, cy, top + .45)
    bpy.context.scene.collection.objects.link(sock)
    # a closed velvet-bound book lying front right, pages showing
    bx, by = .1, -.04
    book = [P.box((-.11, -.075, 0), (.11, .075, .012), VEL(), bev=.005),
            P.box((-.105, -.072, .012), (.1, .072, .045), PC("PR_Late_I_PropPage"), bev=.003),
            P.box((-.11, -.075, .045), (.11, .075, .057), VEL(), bev=.005),
            P.cyl((-.112, -.075, .0285), (-.112, .075, .0285), .029, VEL(), segs=12, bev=.003, grain=False)]
    for t in (-.04, .04):                                 # spine bands
        book.append(P.cyl((-.114, t - .006, .0285), (-.114, t + .006, .0285), .031, BRS(), segs=12, bev=.002, grain=False))
    P.xf(book, Matrix.Translation((bx, by, top)) @ Matrix.Rotation(math.radians(-12), 4, 'Z'))
    p += book
    return p


def wardrobe():
    W_, D, H = 1.30, .62, 2.30
    w, br = WAL(), BRS()
    p = [bun(x, y, .07, .1, w) for x in (-.56, .56) for y in (-.22, .22)]
    m, z = moulding(-W_ / 2 + .03, W_ / 2 - .03, -D / 2 + .03, D / 2 - .03, .1, [(.02, .05), (.008, .025), (0, .02)], w, grain="long")
    p += m
    zb = z
    zt = H - .42
    p.append(P.box((-W_ / 2 + .04, -D / 2 + .04, zb), (W_ / 2 - .04, D / 2 - .04, zt), w, bev=.015, grain=2))
    for s in (-1, 1):                                     # fluted corner pilasters
        x = s * (W_ / 2 - .07)
        p.append(P.box((x - .045, -D / 2 + .02, zb), (x + .045, -D / 2 + .05, zt), w, bev=.01, grain=2))
        for k in (-1, 0, 1):
            p.append(P.cyl((x + k * .022, -D / 2 + .018, zb + .1), (x + k * .022, -D / 2 + .018, zt - .1), .007, w, segs=8, bev=.003))
        for zz in (zb + .06, zt - .06):
            p.append(P.box((x - .055, -D / 2 + .012, zz - .02), (x + .055, -D / 2 + .05, zz + .02), w, bev=.008, grain=0))
    # two doors with arched raised panels
    yf = -D / 2 + .04
    dw, gap = .5, .01
    for s in (-1, 1):
        cx = s * (dw / 2 + gap / 2)
        p.append(P.box((cx - dw / 2, yf - .03, zb + .04), (cx + dw / 2, yf, zt - .04), w, bev=.012, grain=2))
        ph = zt - zb - .3
        outer = [(cx + x, z_) for x, z_ in arch_pts(dw - .12, ph, z0=zb + .16)]
        inner = [(cx + x, z_) for x, z_ in arch_pts(dw - .2, ph - .08, z0=zb + .2)]
        p.append(ring_solid(outer, inner, yf - .058, yf - .03, w, bev=.008))             # proud bolection frame
        p.append(solid(inner, yf - .036, yf - .03, w, bev=.004))                           # sunk ground
        p.append(solid([(cx + x, z_) for x, z_ in arch_pts(dw - .26, ph - .16, z0=zb + .24)], yf - .05, yf - .036, w, bev=.012))  # raised field
        p.append(solid([(cx + x, z_) for x, z_ in arch_pts(dw - .34, ph - .24, z0=zb + .28)], yf - .058, yf - .05, w, bev=.008))
        p.append(knob((s * .05, yf - .03, zb + (zt - zb) * .5), .032, br))
        p.append(P.box((s * .05 - .02, yf - .036, zb + (zt - zb) * .5 - .11), (s * .05 + .02, yf - .03, zb + (zt - zb) * .5 - .06), br, bev=.004))
        for zz in (zb + .25, zt - .25):                    # hinges on the outer edge
            p.append(P.cyl((cx + s * (dw / 2 + .004), yf - .02, zz - .06), (cx + s * (dw / 2 + .004), yf - .02, zz + .06), .012, br, segs=10, bev=.003, grain=False))
    # bonnet top: stepped cornice and an arched pediment with a small turned finial
    m, z = moulding(-W_ / 2 + .03, W_ / 2 - .03, -D / 2 + .03, D / 2 - .03, zt, [(0, .03), (.02, .03), (.04, .04), (.055, .03)], w, grain=0)
    p += m
    ped = [(x, z_) for x, z_ in arch_pts(W_ - .1, .38, n=28, z0=z - .2)]
    ped = [(x, max(z_, z)) for x, z_ in ped]
    p.append(solid(ped, -D / 2 - .01, D / 2 - .04, w, bev=.012))
    edge_o = [(x, z_) for x, z_ in arch_pts(W_ - .02, .45, n=28, z0=z - .27)]
    edge_i = [(x, z_) for x, z_ in arch_pts(W_ - .1, .38, n=28, z0=z - .2)]
    keep = [i for i, (x, z_) in enumerate(edge_i) if z_ > z + .02]
    arc_o = [edge_o[i] for i in keep]; arc_i = [edge_i[i] for i in keep]
    for i in range(len(keep) - 1):                         # arched moulding over the pediment, segment by segment
        a0, a1, b0, b1 = arc_o[i], arc_o[i + 1], arc_i[i + 1], arc_i[i]
        p.append(solid([a0, a1, b0, b1][::-1] if (a1[0] - a0[0]) < 0 else [a0, b1, b0, a1], -D / 2 - .035, -D / 2 + .02, w, bev=.004))
    p.append(P.lathe([(0, 0), (.04, 0), (.045, .02), (.025, .04), (.035, .07), (.012, .1), (0, .11)], br, segs=16, at=(0, -D / 2 + .01, z + .17)))
    p.append(P.box((-.11, -D / 2 - .012, z + .02), (.11, -D / 2 - .002, z + .1), w, bev=.008, grain=0))   # carved cartouche block
    return p


def washstand():
    W_, D, H = .82, .50, .76
    w = WAL()
    p = []
    for x in (-.34, .34):
        for y in (-.18, .18):                             # turned legs with beads
            p.append(P.lathe([(0, 0), (.028, 0), (.032, .03), (.024, .06), (.03, .1), (.022, .14), (.026, .3), (.02, .42),
                              (.03, .46), (.022, .5), (.03, .58), (.034, .62), (.034, H - .1), (0, H - .1)], w, segs=16, at=(x, y, 0)))
    p.append(P.box((-.32, -.16, .16), (.32, .16, .19), w, bev=.01, grain="long"))     # lower shelf
    p.append(P.box((-.33, -.17, .19), (-.30, .17, .2), w, bev=.004, grain=1))
    for y in (-.19, .19):                                 # aprons with a scalloped lower edge
        p.append(P.box((-.34, y - .012, H - .2), (.34, y + .012, H - .1), w, bev=.008, grain=0))
        p.append(P.cyl((-.33, y - .016 * (1 if y < 0 else -1), H - .2), (.33, y - .016 * (1 if y < 0 else -1), H - .2), .012, w, segs=10, bev=.003))  # bead
        p.append(P.box((-.05, y - .016, H - .235), (.05, y + .016, H - .199), w, bev=.015, grain=0))        # carved centre drop
    for x in (-.35, .35):
        p.append(P.box((x - .012, -.18, H - .2), (x + .012, .18, H - .1), w, bev=.008, grain=1))
    p.append(P.box((-.12, -.205, H - .17), (.12, -.19, H - .12), w, bev=.006, grain=0))   # small drawer front
    p.append(knob((0, -.205, H - .145), .014, BRS()))
    m, z = moulding(-W_ / 2, W_ / 2, -D / 2, D / 2, H - .1, [(0, .025), (.02, .04), (.01, .015)], MAR(), grain=None)
    p += m
    top = z
    # basin and jug, cream ceramic
    p.append(P.lathe([(0, 0), (.08, 0), (.1, .012), (.16, .05), (.2, .095), (.215, .11), (.205, .118), (.19, .108),
                      (.15, .07), (.09, .03), (0, .025)], PC("PR_Late_I_PropCeramic"), segs=36, at=(-.14, .02, top)))
    jx, jy = .2, .05
    prof = [(0, 0), (.06, 0), (.07, .01), (.085, .05), (.09, .1), (.08, .16), (.06, .2), (.05, .225), (.06, .26), (.07, .285),
            (.062, .29), (.042, .27), (.04, .23), (.05, .2), (0, .2)]
    jug = P.lathe(prof, PC("PR_Late_I_PropCeramic"), segs=36)
    for v in jug.data.vertices:                           # pull the lip forward into a spout
        if v.co.z > .25 and v.co.x < 0:
            t = (v.co.z - .25) / .04
            v.co.x -= .035 * t * max(0., -v.co.x / .07) ** 2
            v.co.z += .012 * t * max(0., -v.co.x / .07) ** 3
    h = P.torus(.075, .013, PC("PR_Late_I_PropCeramic"), segs=24, rsegs=10, arc=math.pi * 1.15)
    h.data.transform(Matrix.Translation((.075, 0, .17)) @ Matrix.Rotation(math.pi * .4, 4, 'Y'))
    for o in (jug, h): o.data.transform(Matrix.Translation((jx, jy, top)) @ Matrix.Rotation(math.radians(160), 4, 'Z'))
    p += [jug, h]
    return p


def prism(lp, z0, z1, m, bev):
    """Extrude a closed xy outline from z0 to z1."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y, z0)) for x, y in lp]; b = [bm.verts.new((x, y, z1)) for x, y in lp]
    bm.faces.new(a[::-1]); bm.faces.new(b)
    for i in range(len(lp)):
        j = (i + 1) % len(lp); bm.faces.new((a[i], a[j], b[j], b[i]))
    P.bevel_edges(bm, [e for e in bm.edges if e.calc_face_angle(0) > .5], bev)
    o = P.to_obj(bm, m, "prism"); P.uv(o, None)
    return o


def slipper(side):
    """One slipper: walnut sole, low velvet wall round the foot, velvet toe cap, cream lining in the opening and a
    rolled cream fur cuff round the opening (across the instep, round the heel)."""
    L, Wd, N = .30, .115, 40

    def edge(t):                    # footprint outline, t 0..1 round (toe at -y): wide at the ball, narrow heel
        a = 2 * math.pi * t
        y = -math.cos(a) * L / 2
        u = (y + L / 2) / L
        half = Wd / 2 * (.8 + .25 * math.sin(math.pi * min(1., u * 1.7)) - .18 * max(0., u - .65) / .35)
        return (math.sin(a) * half, y)
    loop = [edge(i / N) for i in range(N)]
    p = [prism(loop, 0, .02, PC("PR_Late_I_PropSole"), .007)]
    p.append(prism([(x * .965, y * .975) for x, y in loop], .018, .056, VEL(), .012))           # velvet wall block
    yi = -.035                                                                                     # instep line
    open_loop = [(x * .8, y * .86 + .012) for x, y in loop if y > yi]
    p.append(prism(open_loop, .05, .059, PC("PR_Late_I_PropFur"), .003))                        # cream lining
    cap = P.lathe([(0, 0), (1., 0), (.97, .3), (.85, .62), (.6, .86), (.3, .97), (0, 1.)], VEL(), segs=28)
    for v in cap.data.vertices:                           # toe cap: front half of a squashed dome, back cut flat at the instep
        v.co = Vector((v.co.x * Wd * .47, min(v.co.y, 0.) * L * .43 + yi, v.co.z * .042 + .052))
    p.append(cap)
    path = []
    for i in range(N):                                    # cuff: round the heel part of the outline, closed across the instep
        x, y = edge(i / N)
        if y > yi: path.append((math.atan2(x, -(y - .05)), Vector((x * .9, y * .93 + .006, .064))))
    path = [q for _, q in sorted(path, key=lambda t: t[0])]
    cuff = sweep(path, .021, PC("PR_Late_I_PropFur"), rsegs=10, closed=True)
    rough_up(cuff, .003, 7 + side, 4.)
    p.append(cuff)
    return p


def slippers():
    p = []
    for side, x, yaw in ((-1, -.085, 6), (1, .085, -9)):
        s = slipper(side)
        P.xf(s, Matrix.Translation((x, -.01 * side, 0)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
        p += s
    return p


def robe():
    """A thick folded velvet robe (three soft layers) with an ermine collar lying in a V and an ermine front band."""
    rng = random.Random(4)
    p, z = [], 0.
    for i, (w, d, t) in enumerate(((.62, .46, .08), (.6, .45, .075), (.58, .44, .07))):
        o = P.box((-w / 2, -d / 2, z), (w / 2, d / 2, z + t), VEL(), bev=.03, seg=4)
        subdivide(o, 1, 1.)
        rough_up(o, .005, 20 + i, 2.)
        dx = rng.uniform(-.012, .012)
        o.data.transform(Matrix.Translation((dx, 0, 0)))
        p.append(o)
        if i < 2:                                         # the fold roll on the front edge of each layer
            r = P.cyl((-w / 2 + .02 + dx, -d / 2 + .01, z + t / 2), (w / 2 - .02 + dx, -d / 2 + .01, z + t / 2), t / 2 + .004, VEL(), segs=14, bev=.01, grain=False)
            rough_up(r, .003, 30 + i, 3.)
            p.append(r)
        z += t - .008
    for i in range(10):                                   # quilting stitches on the top
        y = -.18 + i * .04
        p.append(P.box((-.25, y - .002, z - .002), (.25, y + .002, z + .002), PC("PR_Late_I_PropSole"), bev=.001))
    # ermine collar: one smooth roll lying in a V (point at the front), round the back, plus a band down the left edge
    pts = []
    for i, t in enumerate(np.linspace(0, 1, 31)):
        if t < .35:
            u = t / .35; x, y = -.015 - .2 * u, -.16 + .3 * u
        elif t < .65:
            u = (t - .35) / .3; x, y = -.215 + .43 * u, .14 + .045 * math.sin(math.pi * u)
        else:
            u = (t - .65) / .35; x, y = .215 - .2 * u, .14 - .3 * u
        pts.append(Vector((x, y, z + .032 + .006 * math.sin(i * .9))))
    collar = sweep(pts, .043, ERM(), rsegs=14)
    rough_up(collar, .004, 41, 5.)
    p.append(uv_scale(collar, 3.))
    band = P.box((-.31, -.235, 0), (-.23, .235, z - .005), ERM(), bev=.025, seg=3)
    rough_up(band, .003, 43, 5.)
    p.append(uv_scale(band, 3.))
    return p


def screen():
    """Three arched panels hinged edge to edge in a zigzag (the side panels swing 25 deg toward the front): chunky
    walnut frames with a bead moulding, plum velvet fields, a mid rail, brass hinges, turned feet and finials."""
    PW, PH, T, FR, G = .56, 1.82, .055, .075, .012
    w, br = WAL(), BRS()

    def panel():
        q = []
        outer = arch_pts(PW, PH - .07, n=24, z0=.07)
        inner = arch_pts(PW - 2 * FR, PH - .07 - FR, n=24, z0=.07 + FR)
        q.append(ring_solid(outer, inner, -T / 2, T / 2, w, bev=.012))
        q.append(solid(inner, -.012, .012, VEL(), bev=.006, grain=None))
        bead = arch_pts(PW - 2 * FR + .03, PH - .07 - FR + .015, n=24, z0=.07 + FR - .015)
        q.append(ring_solid(bead, inner, -T / 2 - .01, -T / 2 + .002, w, bev=.005))
        q.append(P.box((-PW / 2 + FR, -.02, .64), (PW / 2 - FR, .02, .69), w, bev=.01, grain=0))            # mid rail
        for x in (-PW / 2 + .08, PW / 2 - .08):
            q.append(P.lathe([(0, 0), (.03, 0), (.036, .025), (.024, .05), (.034, .065), (.03, .072), (0, .075)], w, segs=14, at=(x, 0, 0)))
        q.append(P.lathe([(0, 0), (.026, 0), (.03, .02), (.014, .04), (.02, .06), (0, .07)], br, segs=14, at=(0, 0, PH)))
        return q
    out = panel()
    for sd in (-1, 1):
        hx = sd * (PW / 2 + G / 2)                        # hinge axis between the middle and this side panel
        q = panel()
        P.xf(q, Matrix.Translation((hx, 0, 0)) @ Matrix.Rotation(math.radians(-sd * 25), 4, 'Z') @ Matrix.Translation((sd * (PW / 2 + G / 2), 0, 0)))
        out += q
        for zz in (.4, 1.3):
            out.append(P.cyl((hx, -T / 2 - .004, zz - .075), (hx, -T / 2 - .004, zz + .075), .014, br, segs=12, bev=.004, grain=False))
            out.append(P.box((hx - .04, -T / 2 - .006, zz - .055), (hx + .04, -T / 2 + .001, zz + .055), br, bev=.003))
    return out


PROPS = {"Nightstand": nightstand, "Wardrobe": wardrobe, "Washstand": washstand, "Slippers": slippers,
         "Robe": robe, "Screen": screen}
# section frame placement (prop, x, z, yaw): along the entrance wall (z 0) facing into the room, beyond the station ramps
PLACES = []
for s in (-1, 1):
    PLACES += [("Wardrobe", s * 20.9, .45, 180), ("Washstand", s * 19.1, .4, 180), ("Nightstand", s * 17.7, .35, 180),
               ("Slippers", s * 17.6, .85, 180 - s * 15), ("Screen", s * 15.9, .9, 180 + s * 20)]


def lineup(built):
    """All six props in one row, 3/4 from the front on a light ground, with a 1 m pawn for scale (like the GPT sheet)."""
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 2400, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w2"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (1, .98, .95, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .8
    sc.world = w
    for o in list(sc.objects):
        if o.type in ('LIGHT', 'CAMERA') or o.name.startswith(("Cylinder", "Sphere")): bpy.data.objects.remove(o)
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30))); bpy.context.object.data.energy = 3.
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(60), 0, math.radians(150))); bpy.context.object.data.energy = .8
    gm = bpy.data.materials.new("ground"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.92, .9, .86, 1)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(4, 0, 0)); bpy.context.object.data.materials.append(gm)
    x = 0.
    for name in ("Nightstand", "Wardrobe", "Washstand", "Slippers", "Robe", "Screen"):
        o = built[name]
        pts = np.array([v.co[:] for v in o.data.vertices]); lo, hi = pts.min(0), pts.max(0)
        c = bpy.data.objects.new("show_" + name, o.data); sc.collection.objects.link(c)
        c.location = (x - lo[0], 0, 0); x += (hi[0] - lo[0]) + .3
    pm = bpy.data.materials.new("pawn2"); pm.use_nodes = True
    pm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.95, .93, .9, 1)
    bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(x + .25, .25, .31)); bpy.context.object.data.materials.append(pm)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(x + .25, .25, .84)); bpy.context.object.data.materials.append(pm)
    for o in built.values(): o.hide_render = True
    cd = bpy.data.cameras.new("cam2"); cam = bpy.data.objects.new("cam2", cd); sc.collection.objects.link(cam); sc.camera = cam
    cd.type = 'ORTHO'; cd.ortho_scale = x + .9
    tgt = Vector(((x + .4) / 2, 0, .9))
    cam.location = tgt + Vector((-.3, -1.6, .7)).normalized() * 20
    cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(HERE, "render_i_props_lineup.png"); bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    built = {}
    for name, make in PROPS.items():
        built[name] = P.join(make(), "I_Prop_" + name)
        p = np.array([v.co[:] for v in built[name].data.vertices])
        print("PROP", name, P.tris(built[name]), "tris", "size %.2f x %.2f x %.2f" % tuple(p.max(0) - p.min(0)),
              sorted({m.name for m in built[name].data.materials}))
    os.makedirs(OUT, exist_ok=True)
    P.OUT_FBX = OUT
    if "--export" in ARGS:
        for name, o in built.items():
            P.export(o, "I_Prop_" + name)
        P.export(built["Robe"], "I_Robe")
    env = []
    for name, x, z, yaw in PLACES:
        me = built[name].data.copy()
        me.transform(Matrix.Translation((x, z, 0.)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
        o = bpy.data.objects.new("placed", me); bpy.context.scene.collection.objects.link(o); env.append(o)
    props_env = P.join(env, "I_PropsEnv")
    print("ENV", P.tris(props_env), "tris")
    if "--export" in ARGS:
        P.export(props_env, "I_PropsEnv")
    props_env.hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "I_Props_v1.blend"))
    if "--render" in ARGS:
        P.HERE = HERE
        P.render(built)
        for name in PROPS:
            src = os.path.join(HERE, "render_e_prop_%s.png" % name.lower())
            if os.path.exists(src):
                os.replace(src, os.path.join(HERE, "render_i_prop_%s.png" % name.lower()))
        lineup(built)


if __name__ == "__main__":
    main()
