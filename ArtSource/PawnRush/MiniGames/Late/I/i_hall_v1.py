# I King's Bedchamber room shell (R511), modelled in Blender after I's own concept (user 11-08: "행차 회랑 체스 홀 침실
# 컨셉 아트에 있는 벽 구조물이 다른데 왜 구조물이 다 같아?" -> "I 벽 새로 만들기"). Until now I wore G's hall
# (MiniGameModels.RoyalProcessionHall); now the walls, windows, columns, cornice and ceiling are I's:
#   side walls   four bays per side, every bay one tall round-arched window with a fine lattice (I_01, I_02 #7), a moulded
#                reveal with keystone and sill, raised panels under the sill, plinth and chair rail, thick fluted columns
#                with matte brass rings between the bays
#   north wall   corner bays (outside the team portals) with a tall blind arch panel (a code banner hangs in it); above the
#                centre gate a panelled marble band. The centre gate and the team portal frames stay G's (same parts).
#   entry wall   blind arched panels between columns, a moulded lintel over the 12.5 x 7.5 m entry
#   cornice      a stepped cornice at 11.1..12 m round the whole room
#   ceiling      flat 0.5 m marble at 12 m with two long arched skylight lanterns over each team half (I_02 #8: marble
#                coffer frame, arched ribs and purlins, gables; open to the sky, no glass), over G's light shafts
# Curtains and banners are code cloth (11-07 rule): Unity hangs them (MiniGameModels.Bedchamber.cs BedchamberCloth).
# Fixed by the game: entry x +-6.25 up to 7.5 m, team portals x +-9.75..14.25 (frames x +-8.535..15.465), centre gate
# x +-8.535 up to 10.4 m, walls' back lines x +-22.6 / z 26.4 / z -0.75, ceiling 12 m, boards x +-3..21, z 6..24.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python i_hall_v1.py -- [--render] [--export]
#
# Frame: Blender x = section x, Blender y = section z, z = up (MiniGameModels.Hang under the env at its origin).
# Output Late/Export/I/I_HallEnv.fbx; Late/Materials/Textures/PR_Late_I_Banner_BaseColor.png (king + crescent, V hem).
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import i_props_v1 as I    # noqa: E402
P = I.P
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

HALL, BACKX, BACKN, BACKS, T = 12., 22.6, 26.4, -.75, .6
SIDECOLS = [.45, 6.64, 12.83, 19.01, BACKN - 1.2]
COL_R = .5
CORNICE = 11.1


def MAR(): return I.MAR()
def BRS(): return I.BRS()
def LAT(): return P.mat("PR_Late_I_Lattice", (.86, .82, .74), rough=.45)


def tile(o, k=.5):
    """Marble swatches are 2 m tiles: box UVs in metres x 0.5."""
    return I.uv_scale(o, k)


def mbox(lo, hi, m=None, bev=.02):
    m = m or MAR()
    o = P.box(lo, hi, m, bev=bev, grain=None)
    return tile(o) if m.name.startswith("PR_Late_Marble") else o


# ------------------------------------------------------------------ pieces in a wall-local frame
# local X along the wall (bay centre 0), local Y = depth (0 = the wall's room face, + into the wall, - into the room),
# local Z up. place() turns a list of parts into the section frame.
def place(parts, wall, c):
    if wall == "W": M = Matrix(((0, -1, 0, -(BACKX - T)), (1, 0, 0, c), (0, 0, 1, 0), (0, 0, 0, 1)))
    elif wall == "E": M = Matrix(((0, 1, 0, BACKX - T), (-1, 0, 0, c), (0, 0, 1, 0), (0, 0, 0, 1)))
    elif wall == "N": M = Matrix(((1, 0, 0, c), (0, 1, 0, BACKN - T), (0, 0, 1, 0), (0, 0, 0, 1)))
    else: M = Matrix(((-1, 0, 0, c), (0, -1, 0, BACKS + T), (0, 0, 1, 0), (0, 0, 0, 1)))
    for o in parts: o.data.transform(M)
    return parts


def wall_with_hole(W, inner, top=CORNICE, y0=0., y1=T):
    """A wall panel W wide, 0..top high, with an arched hole `inner` (arch_pts loop): outer points are the inner ones
    pushed out to the panel's edge from the arch centre (same count, so ring_solid can bridge them)."""
    zc = inner[1][1]
    outer = []
    for x, z in inner:
        if z <= inner[0][1] + 1e-6:                     # the two sill corners -> the panel's bottom corners
            outer.append((math.copysign(W / 2, x), 0.)); continue
        if abs(z - zc) < 1e-6:                          # the spring points -> straight out to the sides
            outer.append((math.copysign(W / 2, x), zc)); continue
        dx, dz = x, z - zc
        t = min((W / 2) / abs(dx) if abs(dx) > 1e-6 else 1e9, (top - zc) / dz if dz > 1e-6 else 1e9)
        outer.append((dx * t, zc + dz * t))
    return I.ring_solid(outer, inner, y0, y1, MAR(), bev=.0)


def column(height_top=CORNICE):
    """Thick fluted column, half in the wall: square plinth + torus base, 20 flutes, matte brass rings, capital."""
    p = []
    p.append(mbox((-.68, -.68 - .25, 0), (.68, .68 - .25, .32), bev=.03))
    p.append(tile(P.lathe([(0, .32), (.62, .32), (.66, .38), (.62, .46), (.56, .5), (.54, .56), (0, .56)], MAR(), segs=32, at=(0, -.25, 0))))
    z0, z1 = .56, height_top - .85
    shaft = P.lathe([(0, z0), (COL_R, z0), (COL_R * .97, (z0 + z1) * .5), (COL_R * .92, z1), (0, z1)], MAR(), segs=60, at=(0, -.25, 0))
    for v in shaft.data.vertices:                         # flutes: radial cos ripple (20 flutes)
        d = Vector((v.co.x, v.co.y + .25, 0))
        if d.length > .2:
            a = math.atan2(d.y, d.x)
            k = 1 - .035 * max(0., math.cos(20 * a)) ** .6
            v.co.x = d.x * k; v.co.y = d.y * k - .25
    I.subdivide(shaft, 0) if False else None
    p.append(tile(shaft))
    for zz in (z0 + .25, z1 - .2):                        # matte brass rings
        r = P.torus(COL_R + .02, .045, BRS(), segs=40, rsegs=8)
        r.data.transform(Matrix.Translation((0, -.25, zz)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
        p.append(r)
    p.append(tile(P.lathe([(0, z1), (COL_R * .95, z1), (.62, z1 + .18), (.7, z1 + .32), (0, z1 + .32)], MAR(), segs=32, at=(0, -.25, 0))))
    p.append(mbox((-.78, -.78 - .25, z1 + .32), (.78, .78 - .25, z1 + .52), bev=.03))
    p.append(mbox((-.72, -.72 - .25, z1 + .52), (.72, .72 - .25, height_top), bev=.02))
    return p


def run_mouldings(L, top=CORNICE):
    """Plinth, chair rail and cornice along a wall length L (local X -L/2..L/2)."""
    p = []
    p.append(mbox((-L / 2, -.1, 0), (L / 2, .02, .32), bev=.02))
    p.append(mbox((-L / 2, -.14, .32), (L / 2, .02, .38), bev=.015))
    p.append(mbox((-L / 2, -.08, 1.9), (L / 2, .02, 2.0), bev=.02))         # chair rail
    z = top
    for out, h in ((.05, .12), (.12, .1), (.22, .18), (.32, .12), (.4, .1), (.45, HALL - top - .62)):
        p.append(mbox((-L / 2, -out, z), (L / 2, .02, z + h), bev=.02))
        z += h
    return p


def window_bay(W):
    """One side-wall bay W wide: the wall with a tall arched window, its reveal, keystone, sill, lattice and the raised
    panel under it."""
    w, h, z0 = 3.5, 7.9, 2.3
    inner = I.arch_pts(w, h, n=28, z0=z0)
    p = [wall_with_hole(W, inner)]
    # reveal: a proud moulded arch round the opening and its inner jamb
    rev_o = I.arch_pts(w + .56, h + .28, n=28, z0=z0 - .1)
    rev_i = I.arch_pts(w, h, n=28, z0=z0)
    p.append(tile(I.ring_solid(rev_o, rev_i, -.16, .02, MAR(), bev=.015)))
    rev_o2 = I.arch_pts(w + .3, h + .15, n=28, z0=z0 - .05)
    p.append(tile(I.ring_solid(rev_o2, rev_i, -.22, -.15, MAR(), bev=.01)))
    zc = z0 + h - w / 2
    p.append(mbox((-.26, -.3, z0 + h - .1), (.26, .02, z0 + h + .55), bev=.03))           # keystone
    p.append(mbox((-w / 2 - .45, -.34, z0 - .22), (w / 2 + .45, .02, z0 - .05), bev=.03))  # sill
    for s in (-1, 1):                                                                       # sill brackets
        p.append(mbox((s * (w / 2 + .2) - .12, -.26, z0 - .5), (s * (w / 2 + .2) + .12, .02, z0 - .22), bev=.02))
    # lattice in the opening (wall middle): 4 lights across, transoms every 0.62 m below the spring, a concentric arch
    # and radial bars in the head
    lat, b, y = LAT(), .055, T * .45
    r = w / 2
    for k in (1, 2, 3):
        x = -r + w * k / 4
        top_z = zc + math.sqrt(max(0., r * r - x * x)) - .02
        p.append(P.box((x - b / 2, y - b / 2, z0), (x + b / 2, y + b / 2, top_z), lat, bev=.008))
    z = z0 + .62
    while z < zc - .05:
        p.append(P.box((-r, y - b / 2, z - b / 2), (r, y + b / 2, z + b / 2), lat, bev=.008)); z += .62
    p.append(P.box((-r, y - .045, zc - .045), (r, y + .045, zc + .045), lat, bev=.01))     # spring transom, heavier
    arc = [(x_, z_) for x_, z_ in I.arch_pts(w * .6, w * .3 + .01, n=20, z0=zc)][2:-2]
    p.append(I.sweep([Vector((x_, y, z_)) for x_, z_ in arc], b * .6, lat, rsegs=6))
    for k in range(1, 6):
        a = math.pi * k / 6
        p.append(P.cyl((r * .3 * math.cos(a), y, zc + r * .3 * math.sin(a)), ((r - .03) * math.cos(a), y, zc + (r - .03) * math.sin(a)),
                       b * .45, lat, segs=6, bev=0, grain=False))
    p.append(I.sweep([Vector((x_, y, z_)) for x_, z_ in I.arch_pts(w - .04, h - .02, n=28, z0=z0)[1:-1]], b * .55, lat, rsegs=6))
    # raised panel under the sill (between plinth and chair rail)
    p.append(mbox((-w / 2 - .2, -.05, .55), (w / 2 + .2, .02, 1.75), bev=.02))
    p.append(mbox((-w / 2 + .05, -.09, .7), (w / 2 - .05, -.04, 1.6), bev=.03))
    return p


def blind_bay(W, top=CORNICE, panel_w=3.4, panel_h=7.2, z0=2.4):
    """A plain bay W wide with a tall blind arch: moulded arch frame, sunk field (a code banner hangs in it)."""
    p = [mbox((-W / 2, 0, 0), (W / 2, T, top), bev=0.)]
    o = I.arch_pts(panel_w + .5, panel_h + .25, n=24, z0=z0 - .12)
    i = I.arch_pts(panel_w, panel_h, n=24, z0=z0)
    p.append(tile(I.ring_solid(o, i, -.14, .02, MAR(), bev=.015)))
    p.append(tile(I.solid(i, -.02, .01, MAR(), bev=.0, grain=None)))
    p.append(mbox((-.24, -.24, z0 + panel_h - .08), (.24, .02, z0 + panel_h + .45), bev=.03))   # keystone
    p.append(mbox((-panel_w / 2 - .35, -.2, z0 - .2), (panel_w / 2 + .35, .02, z0 - .06), bev=.03))
    p.append(mbox((-panel_w / 2 - .2, -.05, .55), (panel_w / 2 + .2, .02, 1.75), bev=.02))
    p.append(mbox((-panel_w / 2 + .05, -.09, .7), (panel_w / 2 - .05, -.04, 1.6), bev=.03))
    return p


# ------------------------------------------------------------------ walls
def side_walls():
    parts = []
    for wall in ("W", "E"):
        cols = SIDECOLS
        L = BACKN - BACKS
        parts += place(run_mouldings(L), wall, (BACKN + BACKS) / 2 if wall == "W" else (BACKN + BACKS) / 2)
        for i in range(4):
            a, b = cols[i], cols[i + 1]
            c = (a + b) / 2 if wall == "W" else (a + b) / 2
            parts += place(window_bay(b - a), wall, c if wall == "W" else c)
        # the strips from the corners to the end columns
        for a, b in ((BACKS, cols[0]), (cols[-1], BACKN)):
            parts += place([mbox((-(b - a) / 2, 0, 0), ((b - a) / 2, T, CORNICE), bev=0.)], wall, (a + b) / 2)
        for z in cols:
            parts += place(column(), wall, z)
    # the East mapping runs local X the other way: its bays are symmetric, so the same centres work
    return parts


def north_wall():
    parts = []
    for s in (-1, 1):
        a, b = 15.465, BACKX - T                          # corner bay: from the portal frame to the side wall face
        W = b - a
        c = s * (a + b) / 2
        parts += place(blind_bay(W, panel_w=3.2) + run_mouldings(W), "N", c)
        parts += place(column(), "N", s * 15.465)
    # the gaps either side of each team portal frame (G's frame only fills x +-9.75..14.25; in G its 2.43 m columns
    # covered the rest; user 11-08 screenshot "북쪽 출구쪽 빈틈 채워"): solid pilasters as deep as the frame (1.6 m,
    # z 24.8..26.4) from the floor to the ceiling, x +-8.535..9.75 (to the centre gate) and +-14.25..15.465 (to the
    # corner column), dressed with a plinth, panel, capital band and the cornice
    for s in (-1, 1):
        # the inner one starts at the centre gate's side (G_CenterGate is 16 m wide: x +-8.0), so it butts the gate
        # (user 11-08 screenshot: "빈 공간을 자연스럽게 연결해")
        for a, b in ((8.0, 9.75), (14.25, 15.465)):
            x0, x1 = min(s * a, s * b), max(s * a, s * b)
            y0 = BACKN - 1.62
            parts.append(mbox((x0, y0, 0), (x1, BACKN, HALL), bev=0.))
            parts.append(mbox((x0 - .06, y0 - .12, 0), (x1 + .06, y0 + .02, .45), bev=.03))           # plinth
            parts.append(mbox((x0 + .14, y0 - .05, .8), (x1 - .14, y0 + .02, 9.6), bev=.03))          # sunk-edged panel
            parts.append(mbox((x0 + .24, y0 - .09, 1.0), (x1 - .24, y0 - .04, 9.4), bev=.04))
            parts.append(mbox((x0 - .04, y0 - .14, 10.3), (x1 + .04, y0 + .02, 10.55), bev=.03))       # capital band
            r = P.box((x0 - .02, y0 - .1, 10.55), (x1 + .02, y0 + .0, 10.62), BRS(), bev=.01)        # brass fillet
            parts.append(r)
            z = CORNICE
            for out, h in ((.05, .12), (.12, .1), (.22, .18), (.32, .12), (.4, .1), (.45, HALL - CORNICE - .62)):
                parts.append(mbox((x0, y0 - out, z), (x1, y0 + .02, z + h), bev=.02)); z += h
    # behind the north wall, between its back line (z 26.4) and the rear corridor (z 30..36, the teams' way to the exit):
    # outside the team tunnels (linings at x +-9.78 / 14.22) there was neither floor nor wall (in G the shell's thick
    # north wall mass filled it; user 11-08 "출구 쪽 바닥 빈틈 매워"). Solid masonry from the floor to the ceiling, its
    # face on the corridor at z 29.95 (the team portals' rear arches stand at 29.9), the tunnels left open
    for s in (-1, 1):
        for a, b in ((8.0, 9.72), (14.28, BACKX)):
            x0, x1 = min(s * a, s * b), max(s * a, s * b)
            parts.append(mbox((x0, BACKN, 0.), (x1, 29.95, HALL + .5), bev=0.))
    # over the centre gate: G_CenterGate is 16 x 10.39 m and 7 m deep (z 24.02..31.02, door face 26), so it runs on
    # behind the north wall's line; under the band above it the sky showed over its roof (user 11-08 screenshot "빈
    # 공간을 자연스럽게 연결해"). A marble cap closes the room over the gate's roof, from the wall's back line to the
    # gate's back, 3 cm above the roof (no wall below it at the gate's back: the rear corridor runs behind there)
    parts.append(mbox((-8.0, BACKN, 10.42), (8.0, 31.1, HALL + .5), bev=0.))
    # above the centre gate (10.4 m) to the cornice: a panelled band, and the cornice across the gate
    band = [mbox((-8.535, 0, 10.4), (8.535, T, CORNICE), bev=0.)]
    for k in range(-3, 4):
        band.append(mbox((k * 2.3 - .95, -.06, 10.52), (k * 2.3 + .95, .02, 11.0), bev=.02))
    z = CORNICE
    for out, h in ((.05, .12), (.12, .1), (.22, .18), (.32, .12), (.4, .1), (.45, HALL - CORNICE - .62)):
        band.append(mbox((-8.535, -out, z), (8.535, .02, z + h), bev=.02)); z += h
    parts += place(band, "N", 0.)
    return parts


def entry_wall():
    parts = []
    for s in (-1, 1):
        cols = [6.95, 14.6, BACKX - T]
        segs = [(6.25, 6.95), (6.95, 14.6), (14.6, BACKX - T)]
        for a, b in segs:
            W = b - a
            body = blind_bay(W, panel_w=min(3.2, W - 1.6)) if W > 3. else [mbox((-W / 2, 0, 0), (W / 2, T, CORNICE), bev=0.)]
            parts += place(body + run_mouldings(W), "S", s * (a + b) / 2)
        for x in cols[:2]:
            parts += place(column(), "S", s * x)
    # the lintel over the entry (12.5 x 7.5 m opening): moulded architrave, frieze panel, cornice runs on
    lin = [mbox((-6.25, 0, 7.5), (6.25, T, CORNICE), bev=0.),
           mbox((-6.45, -.18, 7.3), (6.45, .02, 7.62), bev=.03),
           mbox((-6.25, -.1, 7.62), (6.25, .02, 7.8), bev=.02),
           mbox((-4.5, -.08, 8.2), (4.5, .02, 10.6), bev=.03),
           mbox((-4.2, -.13, 8.45), (4.2, -.06, 10.35), bev=.04)]
    for s in (-1, 1):
        lin.append(mbox((s * 6.25 - .25, -.18, 0), (s * 6.25 + .25 * 0, .02, 7.3), bev=.03))   # jamb mouldings
    z = CORNICE
    for out, h in ((.05, .12), (.12, .1), (.22, .18), (.32, .12), (.4, .1), (.45, HALL - CORNICE - .62)):
        lin.append(mbox((-6.25, -out, z), (6.25, .02, z + h), bev=.02)); z += h
    parts += place(lin, "S", 0.)
    return parts


# ------------------------------------------------------------------ ceiling and skylight lanterns
SKY_X0, SKY_X1 = 5., 11.                                  # per half, |x|
SKY_Z = [(1.2, 11.2), (13.2, 23.2)]


def ceiling():
    parts = []
    slab = (HALL, HALL + .5)
    xs = [-BACKX, -SKY_X1, -SKY_X0, SKY_X0, SKY_X1, BACKX]
    for i in range(5):
        x0, x1 = xs[i], xs[i + 1]
        if i in (1, 3):                                   # the lantern strips: slab only between and around the openings
            z = BACKS
            for a, b in SKY_Z:
                parts.append(mbox((x0, z, slab[0]), (x1, a, slab[1]), bev=0.)); z = b
            parts.append(mbox((x0, z, slab[0]), (x1, BACKN, slab[1]), bev=0.))
        else:
            parts.append(mbox((x0, BACKS, slab[0]), (x1, BACKN, slab[1]), bev=0.))
    lat = LAT()
    for s in (-1, 1):
        x0, x1 = s * SKY_X0, s * SKY_X1
        xa, xb = min(x0, x1), max(x0, x1)
        cx, half = (xa + xb) / 2, (xb - xa) / 2
        for za, zb in SKY_Z:
            # stepped coffer frame under the ceiling round the opening
            for k, (d, dz) in enumerate(((.0, .0), (.25, .18), (.5, .34))):
                zz = HALL - .12 - dz
                parts += [mbox((xa - .35 - d, za - .35 - d, zz), (xb + .35 + d, za - d, zz + .14), bev=.02),
                          mbox((xa - .35 - d, zb + d, zz), (xb + .35 + d, zb + .35 + d, zz + .14), bev=.02),
                          mbox((xa - .35 - d, za - d, zz), (xa - d, zb + d, zz + .14), bev=.02),
                          mbox((xb + d, za - d, zz), (xb + .35 + d, zb + d, zz + .14), bev=.02)]
            r = P.box((xa - .1, za - .1, HALL - .1), (xb + .1, zb + .1, HALL - .02), BRS(), bev=.01)     # brass band
            bm = bmesh.new(); bm.from_mesh(r.data)
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if abs(f.normal.z) > .9], context='FACES')
            bm.to_mesh(r.data); bm.free()
            parts.append(r)
            # the lantern: a low curb on the slab, arched ribs every 1.25 m, purlins along, marble gables at both ends
            top = HALL + .5
            parts += [mbox((xa - .3, za - .3, top), (xb + .3, za, top + .45), bev=.03), mbox((xa - .3, zb, top), (xb + .3, zb + .3, top + .45), bev=.03),
                      mbox((xa - .3, za, top), (xa, zb, top + .45), bev=.03), mbox((xb, za, top), (xb + .3, zb, top + .45), bev=.03)]
            rise = 1.9
            def arc(t): return Vector((cx - half * math.cos(math.pi * t), 0, top + .45 + rise * math.sin(math.pi * t)))
            n = int((zb - za) / 1.25)
            for k in range(n + 1):
                y = za + (zb - za) * k / n
                pts = [arc(t) + Vector((0, y, 0)) for t in np.linspace(0, 1, 17)]
                parts.append(I.sweep(pts, .07 if k in (0, n) else .045, lat, rsegs=6))
            for t in (.12, .3, .5, .7, .88):
                a = arc(t)
                parts.append(P.cyl((a.x, za, a.z), (a.x, zb, a.z), .035, lat, segs=6, bev=0, grain=False))
            for y, d in ((za, -1), (zb, 1)):              # gables: half-disc marble walls with a moulded rim
                loop = [(cx - half * math.cos(math.pi * t), top + .45 + rise * math.sin(math.pi * t)) for t in np.linspace(0, 1, 17)]
                g = I.solid(loop, 0, .2, MAR(), bev=.0, grain=None)
                g.data.transform(Matrix.Translation((0, y + (.0 if d > 0 else -.2), 0)) @ Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1))) if False else Matrix.Identity(4))
                # solid() extrudes along y in an xz outline: put it at the gable line
                g.data.transform(Matrix.Translation((0, y - .1 + d * .1, 0)))
                parts.append(tile(g))
            parts.append(P.lathe([(0, 0), (.14, 0), (.16, .08), (.08, .16), (.11, .26), (0, .36)], BRS(), segs=16, at=(cx, (za + zb) / 2, top + .45 + rise)))
    return parts


# ------------------------------------------------------------------ banner texture (code cloth wears it)
def banner_texture():
    """512 x 1024 RGBA: plum velvet field, matte brass border, cream king above a cream crescent, V-shaped hem cut by
    alpha. No text."""
    Wd, H = 512, 1024
    y, x = np.mgrid[0:H, 0:Wd].astype(np.float32)
    u, v = (x + .5) / Wd, 1 - (y + .5) / H                  # v = 1 at the top
    rng = np.random.default_rng(3)
    plum = np.array([.33, .12, .34])
    noise = sum(np.sin(2 * np.pi * (fx * u + fy * v) + rng.uniform(0, 6.3)) for fx, fy in ((3, 5), (7, 2), (11, 13))) / 3
    rgb = plum[None, None, :] * (1 + .08 * noise[..., None])
    hem = .12 + .12 * (1 - np.abs(u - .5) * 2)              # V hem: bottom edge rises to the sides
    alpha = (v > .12 - .12 * np.abs(u - .5) * 2 + .0).astype(np.float32)
    alpha = (v > (0.0 + .14 * np.abs(u - .5) * 2 * 0 + (.24 - .24 * np.abs(u - .5) * 2) * 0 + .02 + .16 * (1 - np.abs(u - .5) * 2) * 0)).astype(np.float32)
    point = .02 + .2 * np.abs(u - .5) * 2                   # the hem's edge: lowest in the middle
    alpha = (v > point).astype(np.float32)
    brass = np.array([.72, .56, .26])
    border = (u < .06) | (u > .94) | (v > .955) | ((v > point) & (v < point + .045))
    inner = ((u > .085) & (u < .915) & (v > point + .07) & (v < .93))
    rgb = np.where(border[..., None], brass[None, None, :] * (1 + .1 * noise[..., None]), rgb)
    line = (~inner) & (~border) & (((np.abs(u - .085) < .006) | (np.abs(u - .915) < .006)) & (v < .93) & (v > point + .07))
    rgb = np.where(line[..., None], brass, rgb)
    cream = np.array([.93, .88, .76])
    # king: base, body (trapezoid), collar, crown band, cross
    cx = .5
    def box(x0, x1, y0, y1): return (u > x0) & (u < x1) & (v > y0) & (v < y1)
    body = (v > .5) & (v < .74) & (np.abs(u - cx) < .055 + (.74 - v) * .32)
    base = box(cx - .16, cx + .16, .46, .5) | box(cx - .13, cx + .13, .5, .53)
    collar = box(cx - .1, cx + .1, .74, .765)
    head = ((u - cx) / .085) ** 2 + ((v - .8) / .045) ** 2 < 1
    crownb = box(cx - .09, cx + .09, .835, .855)
    cross = box(cx - .012, cx + .012, .855, .91) | box(cx - .035, cx + .035, .875, .893)
    king = body | base | collar | head | crownb | cross
    # crescent under the king
    c1 = ((u - .5) / .16) ** 2 + ((v - .33) / .08) ** 2 < 1
    c2 = ((u - .56) / .14) ** 2 + ((v - .345) / .07) ** 2 < 1
    moon = c1 & ~c2
    rgb = np.where((king | moon)[..., None], cream[None, None, :] * (1 + .04 * noise[..., None]), rgb)
    img = bpy.data.images.new("PR_Late_I_Banner_BaseColor", Wd, H, alpha=True)
    px = np.ones((H, Wd, 4), np.float32)
    px[..., :3] = np.clip(rgb, 0, 1) ** 2.2
    px[..., 3] = alpha
    img.pixels.foreach_set(px[::-1].ravel())
    img.filepath_raw = os.path.join(I.LTEX, "PR_Late_I_Banner_BaseColor.png"); img.file_format = 'PNG'; img.save()


def render(o):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1800, 1100
    sc.view_settings.view_transform = 'AgX'
    wd = bpy.data.worlds.new("w"); wd.use_nodes = True
    wd.node_tree.nodes["Background"].inputs[0].default_value = (.75, .72, .85, 1); wd.node_tree.nodes["Background"].inputs[1].default_value = 1.
    sc.world = wd
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(35), 0, math.radians(-25))); bpy.context.object.data.energy = 3.
    gm = bpy.data.materials.new("floor"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.12, .14, .22, 1)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, (BACKN + BACKS) / 2, 0)); f = bpy.context.object
    f.scale = (2 * BACKX, BACKN - BACKS, 1); f.data.materials.append(gm)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    cd.lens = 24
    for name, loc, tgt in (("overview", (0, -14, 13), (0, 14, 4)), ("side", (-6, 4, 2.2), (-22, 12, 5)),
                           ("north", (0, 6, 3), (14, 26, 6)), ("ceiling", (8, 2, 1.5), (8, 12, 13))):
        cam.location = loc
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_i_hall_%s.png" % name); bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    banner_texture()
    parts = side_walls() + north_wall() + entry_wall() + ceiling()
    env = P.join(parts, "I_HallEnv")
    bm = bmesh.new(); bm.from_mesh(env.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.to_mesh(env.data); bm.free()
    pts = np.array([v.co[:] for v in env.data.vertices])
    print("HALL", P.tris(env), "tris", "min", pts.min(0).round(2), "max", pts.max(0).round(2), sorted({m.name for m in env.data.materials}))
    os.makedirs(I.OUT, exist_ok=True)
    if "--export" in ARGS:
        P.OUT_FBX = I.OUT
        P.export(env, "I_HallEnv")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "I_Hall_v1.blend"))
    if "--render" in ARGS:
        render(env)


if __name__ == "__main__":
    main()
