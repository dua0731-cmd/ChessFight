# I King's Bedchamber moving devices (R511), modelled here in Blender in detail (user, 11-08: "그것도 블렌더로 디테일 하게
# 만들어"), after the GPT sheet Docs/PawnRush/ConceptArt/R511_KingsBedchamber/AssetRefs/I_assets_01_bed_cart.png:
# the canopy bed on its round base (our king's base, dragged at the double check) and the shield cart with its folding
# screen. Not sent to Tripo. Helpers from i_props_v1.py (and through it E's e_props_v1.py).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python i_devices_v1.py -- [--render] [--export]
#
# Game sizes it must keep (MgCheckBlock.cs):
#   King base  capsule collider radius 0.9 m, 2.25 m high, origin on the floor at the base's centre. The bed is drawn round
#              it: round walnut base 1.8 m across x 0.35 m with ONE bright gold grip band (functional: "grab and drag"),
#              bed frame 1.9 x 2.3 m on it. Attack beams come at 1.1 m from the south, west, east and the two southern
#              diagonals, never from the north, so the headboard (the only tall part) stands at the NORTH end (Blender +y)
#              and everything between 0.5 and 2.5 m stays open: footboard top 0.92 m, side rails 0.62 m, canopy valance from
#              3.05 m up, curtains tied tight to the posts above that. SOCKET_KingPillow = where the sleeping king's head goes.
#   Shield cart  box collider 1.25 x 0.8 x 1.25 centred 0.4 up, origin on the floor at the centre; it rolls along local
#              +z in Unity (Blender -y here is its front, the side with the gold grip stripe). Our piece stands on the front
#              half of the deck (SOCKET_CartPiece, 0.8 m up); the three-panel screen stands on the back edge (Blender +y),
#              up to 2.1 m, so a beam at 1.1 m hits velvet.
# Materials: walnut PR_Late_I_PropWalnut, velvet PR_Late_I_Velvet, linen PR_Mid_DollLinen (R509 J), brass PR_Late_Brass,
# bright functional gold PR_Late_I_GripGold (plain, the game's Arena.Gold), the rest as i_props_v1.
# Output Late/Export/I/I_CanopyBed.fbx and I_ShieldCart.fbx (+ renders render_i_dev_*.png, save I_Devices_v1.blend).
import bpy, bmesh, math, os, random, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import i_props_v1 as I    # noqa: E402
P = I.P
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MTEX = os.path.join(os.path.dirname(I.LATE), "Mid", "Materials", "Textures")


def GOLD(): return P.mat("PR_Late_I_GripGold", (.80, .52, .10), rough=.28, metal=1.)
def LIN(): return P.mat("PR_Late_I_Linen", tex=os.path.join(MTEX, "PR_Mid_DollLinen_BaseColor.png"), rough=.9)


def socket(name, at):
    s = bpy.data.objects.new(name, None); s.location = at
    bpy.context.scene.collection.objects.link(s)
    return s


def soft_box(lo, hi, m, bev, cuts=1, bulge=0., seed=0):
    """A cushion-like rounded box: bevelled, subdivided, and puffed on top."""
    o = P.box(lo, hi, m, bev=bev, seg=3)
    I.subdivide(o, cuts, 1.)
    if bulge:
        cx, cy = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
        hx, hy = (hi[0] - lo[0]) / 2, (hi[1] - lo[1]) / 2
        for v in o.data.vertices:
            if v.co.z > (lo[2] + hi[2]) / 2:
                f = max(0., 1 - ((v.co.x - cx) / hx) ** 2) * max(0., 1 - ((v.co.y - cy) / hy) ** 2)
                v.co.z += bulge * f
    if seed: I.rough_up(o, .003, seed, 3.)
    return o


# ------------------------------------------------------------------ canopy bed
def canopy_bed():
    w, br, vel, lin = I.WAL(), I.BRS(), I.VEL(), LIN()
    p = []
    # round base: plinth, gold grip band, top disc with a moulded lip and plank seams
    p.append(P.lathe([(0, 0), (.86, 0), (.9, .02), (.9, .08), (.88, .1), (0, .1)], w, segs=64))
    p.append(P.lathe([(.885, .1), (.905, .11), (.905, .22), (.885, .23), (0, .23), (0, .1)], GOLD(), segs=64))
    p.append(P.lathe([(0, .23), (.88, .23), (.9, .25), (.9, .31), (.875, .35), (0, .35)], w, segs=64))
    for k in range(-3, 4):                                # shallow plank seams across the top disc
        x = k * .24
        half = math.sqrt(max(0., .86 ** 2 - x ** 2)) - .02
        p.append(P.box((x - .004, -half, .349), (x + .004, half, .352), P.mat("PR_Late_I_Seam", (.12, .07, .04), rough=.9), bev=0))
    zb = .35
    BW, BL = 1.9, 2.3
    # bed frame: corner blocks, side rails with sunk panels, footboard (low, south) and headboard (tall, north)
    for x in (-BW / 2 + .07, BW / 2 - .07):
        for y in (-BL / 2 + .07, BL / 2 - .07):
            p.append(P.box((x - .07, y - .07, zb), (x + .07, y + .07, zb + .5), w, bev=.02, grain=2))
            p.append(P.lathe([(0, 0), (.06, 0), (.07, .02), (.05, .04), (0, .045)], w, segs=16, at=(x, y, zb + .5)))
    for s in (-1, 1):
        x = s * (BW / 2 - .05)
        p.append(P.box((x - .05, -BL / 2 + .14, zb + .06), (x + .05, BL / 2 - .14, zb + .27), w, bev=.015, grain=1))
        for k in (-1, 0, 1):                              # three sunk panels per rail
            y = k * .66
            p.append(P.box((x + s * .045, y - .28, zb + .1), (x + s * .056, y + .28, zb + .23), w, bev=.008, grain=1))
        p.append(P.box((x - .06, -BL / 2 + .14, zb + .26), (x + .06, BL / 2 - .14, zb + .29), w, bev=.01, grain=1))   # top moulding
    yf = -BL / 2 + .05                                    # footboard: two rails and arched top, top at 0.92 m
    p.append(P.box((-BW / 2 + .14, yf - .05, zb + .06), (BW / 2 - .14, yf + .05, zb + .45), w, bev=.015, grain=0))
    foot = [(x * (BW / 2 - .14) / ((BW - .28) / 2), z_) for x, z_ in I.arch_pts(BW - .28, .7, n=24, z0=0)]
    foot = [(x, zb + .45 + min(.12, max(0., z_ - .6)) ) for x, z_ in foot]
    p.append(I.solid([(x, z_) for x, z_ in foot] + [], yf - .045, yf + .045, w, bev=.012))
    p.append(P.box((-BW / 2 + .2, yf - .062, zb + .12), (BW / 2 - .2, yf - .05, zb + .4), w, bev=.008, grain=0))      # raised panel
    p.append(P.box((-BW / 2 + .26, yf - .07, zb + .17), (BW / 2 - .26, yf - .062, zb + .35), w, bev=.008, grain=0))
    yh = BL / 2 - .05                                     # headboard: carved double-curve top up to 1.32 m
    prof = []
    for i in range(41):
        t = i / 40; x = -BW / 2 + .14 + (BW - .28) * t
        z_ = zb + .7 + .2 * math.sin(math.pi * t) ** .8 + .07 * math.sin(math.pi * t * 3) ** 2 * (1 - abs(2 * t - 1))
        prof.append((x, z_))
    head = [(-BW / 2 + .14, zb + .06)] + prof + [(BW / 2 - .14, zb + .06)]
    p.append(I.solid(head, yh - .05, yh + .05, w, bev=.014))
    for k in (-1, 0, 1):                                  # three arched panels on the headboard's face
        cx = k * .52
        outer = [(cx + x, z_) for x, z_ in I.arch_pts(.44, .55, n=16, z0=zb + .18)]
        inner = [(cx + x, z_) for x, z_ in I.arch_pts(.36, .49, n=16, z0=zb + .22)]
        p.append(I.ring_solid(outer, inner, yh - .068, yh - .05, w, bev=.004))
        p.append(I.solid(inner, yh - .06, yh - .05, w, bev=.008))
    p.append(P.lathe([(0, 0), (.05, 0), (.06, .025), (.03, .05), (.04, .08), (0, .1)], br, segs=16, at=(0, yh, zb + .97)))
    # mattress, pillow, folded blanket
    zm = zb + .27
    p.append(soft_box((-BW / 2 + .1, -BL / 2 + .1, zm), (BW / 2 - .1, BL / 2 - .12, zm + .2), lin, .07, 1, .015))
    p.append(soft_box((-.45, BL / 2 - .62, zm + .2), (.45, BL / 2 - .18, zm + .34), lin, .06, 2, .05, seed=5))
    bl = soft_box((-BW / 2 + .07, -BL / 2 + .07, zm + .17), (BW / 2 - .07, .05, zm + .27), vel, .05, 1, .02, seed=8)
    p.append(bl)
    p.append(P.cyl((-BW / 2 + .08, .05, zm + .24), (BW / 2 - .08, .05, zm + .24), .055, vel, segs=16, bev=.02, grain=False))  # folded-back roll
    for s in (-1, 1):                                     # blanket hanging over the sides (stops above the rail)
        o = P.box((s * (BW / 2 - .1) - .02 * (s > 0), -BL / 2 + .1, zm + .02), (s * (BW / 2 - .1) + .02 * (s < 0), .03, zm + .24), vel, bev=.01)
        p.append(o)
    socket("SOCKET_KingPillow", (0, BL / 2 - .4, zm + .38))
    # canopy: four matte brass posts with turned rings, valance with scallops and a brass trim, domed velvet roof,
    # curtains gathered tight at the post tops, brass finials
    PX, PY, ZT = BW / 2 - .07, BL / 2 - .07, 3.25
    for x in (-PX, PX):
        for y in (-PY, PY):
            p.append(P.lathe([(0, 0), (.06, 0), (.065, .03), (.045, .07), (.04, .12), (.032, .2), (.032, ZT - zb - .75),
                              (.045, ZT - zb - .7), (.032, ZT - zb - .62), (.032, ZT - zb - .5), (0, ZT - zb - .5)],
                             br, segs=16, at=(x, y, zb + .5)))
            for zz in (1.45, 2.3):                        # turned brass rings on the shaft
                r = P.torus(.04, .012, br, segs=16, rsegs=6); r.data.transform(Matrix.Translation((x, y, zz)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
                p.append(r)
            p.append(P.lathe([(0, 0), (.06, 0), (.07, .04), (.04, .09), (.06, .14), (0, .2)], br, segs=16, at=(x, y, ZT + .1)))
            # gathered curtain tied at the post top: a bulging velvet sheaf with a brass tie
            sheaf = P.lathe([(0, -.02), (.1, 0), (.15, .12), (.13, .26), (.07, .36), (.1, .44), (.16, .5), (0, .52)], vel, segs=14)
            I.rough_up(sheaf, .012, int(10 + x * 3 + y), 6.)
            sheaf.data.transform(Matrix.Translation((x + (.03 if x < 0 else -.03), y + (.03 if y < 0 else -.03), ZT - .5)))
            p.append(sheaf)
            tie = P.torus(.075, .016, br, segs=16, rsegs=6); tie.data.transform(Matrix.Translation((x, y, ZT - .17)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
            p.append(tie)
    # valance: a velvet band round the top with a scalloped hem (5 scallops on the long sides, 4 on the short)
    def valance_side(a, b, n):
        a, b = Vector(a), Vector(b)
        bm = bmesh.new(); top, bot = [], []
        steps = n * 8
        for i in range(steps + 1):
            t = i / steps; c = a.lerp(b, t)
            hem = ZT - .18 - .14 * abs(math.sin(math.pi * n * t))
            top.append(bm.verts.new((c.x, c.y, ZT + .02))); bot.append(bm.verts.new((c.x, c.y, hem)))
        for i in range(steps):
            bm.faces.new((bot[i], bot[i + 1], top[i + 1], top[i]))
        hem_pts = [Vector(v.co) for v in bot]
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=.025)
        o = P.to_obj(bm, vel, "valance"); P.uv(o, None)
        trim = I.sweep(hem_pts, .016, br, rsegs=6)
        return [o, trim]
    for a, b, n in (((-PX, -PY), (PX, -PY), 4), ((PX, -PY), (PX, PY), 5), ((PX, PY), (-PX, PY), 4), ((-PX, PY), (-PX, -PY), 5)):
        p += valance_side(a, b, n)
    p.append(P.box((-PX - .06, -PY - .06, ZT), (PX + .06, PY + .06, ZT + .07), w, bev=.02, grain=0))       # cornice frame
    for s in (-1, 1):
        p.append(P.box((-PX - .07, s * (PY + .055) - .02, ZT + .07), (PX + .07, s * (PY + .055) + .02, ZT + .1), br, bev=.008))
        p.append(P.box((s * (PX + .055) - .02, -PY - .07, ZT + .07), (s * (PX + .055) + .02, PY + .07, ZT + .1), br, bev=.008))
    # domed roof: four curved velvet gores rising to a brass crown finial, ribs in brass
    bm = bmesh.new()
    R = 24
    rings = []
    for j in range(R + 1):
        t = j / R; h = ZT + .1 + .62 * math.sin(math.pi / 2 * t) ** 1.3
        k = (1 - t) ** 1.15
        ring = []
        for i in range(64):
            a = 2 * math.pi * i / 64
            # rounded rectangle footprint shrinking to the peak
            cx, cy = math.cos(a), math.sin(a)
            m = max(abs(cx), abs(cy)) or 1
            sq = Vector((cx / m * (PX + .05), cy / m * (PY + .05)))
            circ = Vector((cx * (PX + .05), cy * (PY + .05)))
            q = sq.lerp(circ, .35) * k
            sag = .04 * (1 - t) * t * 4 * (1 - abs(math.cos(4 * a)) ** 4)      # gores sag between the ribs
            ring.append(bm.verts.new((q.x, q.y, h - sag)))
        rings.append(ring)
    for j in range(R):
        for i in range(64):
            bm.faces.new((rings[j][i], rings[j][(i + 1) % 64], rings[j + 1][(i + 1) % 64], rings[j + 1][i]))
    rib_pts = {i: [Vector(rings[j][i].co) + Vector((0, 0, .012)) for j in range(0, R + 1, 2)] for i in (8, 24, 40, 56)}
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    roof = P.to_obj(bm, vel, "roof"); P.uv(roof, None)
    p.append(roof)
    for i in (8, 24, 40, 56):                             # ribs down the four corners
        p.append(I.sweep(rib_pts[i], .014, br, rsegs=6))
    p.append(P.lathe([(0, 0), (.12, 0), (.13, .05), (.07, .1), (.09, .16), (.05, .22), (.08, .26), (.03, .32), (0, .34)],
                     br, segs=20, at=(0, 0, ZT + .66)))
    return p


# ------------------------------------------------------------------ shield cart
def shield_cart():
    w, br, vel = I.WAL(), I.BRS(), I.VEL()
    C, H0, H1 = 1.25, .22, .8                             # footprint, body bottom, deck top
    p = []
    # body: planked box (planks run across, small gaps), top deck planks, under-frame, brass corner straps with rivets
    p.append(P.box((-C / 2 + .03, -C / 2 + .03, H0), (C / 2 - .03, C / 2 - .03, H1 - .06), w, bev=.02, grain=0))
    for side in range(4):                                 # side planks: three per face, proud by 1 cm
        rot = Matrix.Rotation(math.pi / 2 * side, 4, 'Z')
        for k in range(3):
            z0 = H0 + .02 + k * (H1 - .08 - H0) / 3
            o = P.box((-C / 2 + .07, -C / 2 + .015, z0), (C / 2 - .07, -C / 2 + .035, z0 + (H1 - .08 - H0) / 3 - .012), w, bev=.008, grain=0)
            o.data.transform(rot); p.append(o)
    for k in range(6):                                    # deck planks
        x = -C / 2 + .02 + k * (C - .04) / 6
        p.append(P.box((x + .004, -C / 2, H1 - .065), (x + (C - .04) / 6 - .004, C / 2, H1), w, bev=.01, grain=1))
    for x in (-1, 1):                                     # deck edge rails
        p.append(P.box((x * C / 2 - .03, -C / 2, H1 - .07), (x * C / 2 + .0 * x, C / 2, H1 - .045), w, bev=.008, grain=1))
    rivet = lambda at: P.lathe([(0, 0), (.012, 0), (.01, .006), (0, .009)], br, segs=8)
    for cx in (-1, 1):
        for cy in (-1, 1):                                # L-shaped corner straps
            for (ax, ay) in ((1, 0), (0, 1)):
                o = P.box((-.0, -.0, H0 + .01), (.16 if ax else .03, .16 if ay else .03, H1 - .07), br, bev=.006)
                o.data.transform(Matrix.Translation((cx * (C / 2 + .005), cy * (C / 2 + .005), 0)) @ Matrix.Diagonal((-cx, -cy, 1, 1)))
                p.append(o)
            for zz in (H0 + .08, (H0 + H1) / 2 - .03, H1 - .13):
                for d in (.1,):
                    for ax, ay in ((1, 0), (0, 1)):
                        r = rivet(None)
                        nrm = Vector((0, cy, 0)) if ax else Vector((cx, 0, 0))
                        pos = Vector((cx * (C / 2 + .012) - cx * d * ax, cy * (C / 2 + .012) - cy * d * ay, zz))
                        r.data.transform(Matrix.Translation(pos) @ nrm.to_track_quat('Z', 'Y').to_matrix().to_4x4())
                        p.append(r)
    # the bright gold grip stripe across the front (Blender -y) with brass end caps
    p.append(P.box((-C / 2 + .1, -C / 2 - .012, H0 + .2), (C / 2 - .1, -C / 2 + .02, H0 + .3), GOLD(), bev=.012))
    # axles and four spoked wheels (two per side, under the body's edge)
    WR = .2
    for y in (-C / 2 + .26, C / 2 - .26):
        p.append(P.cyl((-C / 2 - .02, y, WR), (C / 2 + .02, y, WR), .025, br, segs=10, bev=.004, grain=False))
        for s in (-1, 1):
            x = s * (C / 2 + .02)
            wheel = []
            rim = P.torus(WR - .025, .028, w, segs=32, rsegs=8)
            rim.data.transform(Matrix.Diagonal((1, 1.6, 1, 1)))
            wheel.append(rim)
            tyre = P.torus(WR - .005, .012, P.mat("PR_Late_Iron", (.06, .06, .07), rough=.5, metal=.6), segs=32, rsegs=6)
            wheel.append(tyre)
            wheel.append(P.cyl((0, -.05, 0), (0, .05, 0), .05, w, segs=14, bev=.012, grain=False))           # hub
            wheel.append(P.lathe([(0, 0), (.035, 0), (.03, .02), (0, .03)], br, segs=12))                        # brass hub cap
            wheel[-1].data.transform(Matrix.Translation((0, -.05, 0)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
            for k in range(8):                            # spokes, thicker at the hub
                a = 2 * math.pi * k / 8
                wheel.append(P.cyl((.045 * math.cos(a), 0, .045 * math.sin(a)), ((WR - .04) * math.cos(a), 0, (WR - .04) * math.sin(a)),
                                   .013, w, segs=8, bev=.003, r2=.009, grain=False))
            P.xf(wheel, Matrix.Translation((x, y, WR)) @ Matrix.Rotation(-s * math.pi / 2, 4, 'Z'))
            p += wheel
    # screen: three arched walnut-framed velvet panels on the back edge (Blender +y), the side ones angled forward
    PW, T, FR = .4, .05, .06
    def panel(hgt):
        q = []
        outer = I.arch_pts(PW, hgt, n=20, z0=0)
        inner = I.arch_pts(PW - 2 * FR, hgt - FR - .03, n=20, z0=FR)
        q.append(I.ring_solid(outer, inner, -T / 2, T / 2, w, bev=.01))
        q.append(I.solid(inner, -.012, .012, vel, bev=.005, grain=None))
        bead = I.arch_pts(PW - 2 * FR + .025, hgt - FR - .03 + .012, n=20, z0=FR - .012)
        q.append(I.ring_solid(bead, inner, -T / 2 - .009, -T / 2 + .002, w, bev=.004))
        return q
    ys = C / 2 - .1
    mid = panel(1.3)
    P.xf(mid, Matrix.Translation((0, ys, H1)))
    p += mid
    G_ = .01
    for sd in (-1, 1):
        q = panel(1.12)
        P.xf(q, Matrix.Translation((sd * (PW / 2 + G_ / 2), ys, H1)) @ Matrix.Rotation(math.radians(-sd * 22), 4, 'Z')
             @ Matrix.Translation((sd * (PW / 2 + G_ / 2), 0, 0)))
        p += q
        hx = sd * (PW / 2 + G_ / 2)
        for zz in (H1 + .25, H1 + .85):
            p.append(P.cyl((hx, ys - T / 2 - .004, zz - .06), (hx, ys - T / 2 - .004, zz + .06), .012, br, segs=10, bev=.003, grain=False))
            p.append(P.box((hx - .032, ys - T / 2 - .006, zz - .045), (hx + .032, ys - T / 2 + .001, zz + .045), br, bev=.003))
    p.append(P.box((-.5, ys - .06, H1), (.5, ys + .06, H1 + .05), w, bev=.012, grain=0))                   # screen sill
    socket("SOCKET_CartPiece", (0, -.15, H1))
    return p


DEVICES = {"CanopyBed": canopy_bed, "ShieldCart": shield_cart}


def render(built):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1200
    sc.view_settings.view_transform = 'AgX'
    wd = bpy.data.worlds.new("w"); wd.use_nodes = True
    wd.node_tree.nodes["Background"].inputs[0].default_value = (1, .98, .95, 1); wd.node_tree.nodes["Background"].inputs[1].default_value = .8
    sc.world = wd
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30))); bpy.context.object.data.energy = 3.
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(60), 0, math.radians(150))); bpy.context.object.data.energy = .8
    gm = bpy.data.materials.new("ground"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.92, .9, .86, 1)
    bpy.ops.mesh.primitive_plane_add(size=40); bpy.context.object.data.materials.append(gm)
    pm = bpy.data.materials.new("pawn"); pm.use_nodes = True
    pm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.95, .93, .9, 1)
    bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(0, 0, .31)); pb = bpy.context.object; pb.data.materials.append(pm)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(0, 0, .84)); ph = bpy.context.object; ph.data.materials.append(pm)
    beam_m = bpy.data.materials.new("beam"); beam_m.use_nodes = True
    bsdf = beam_m.node_tree.nodes["Principled BSDF"]; bsdf.inputs["Emission Color"].default_value = (1, .45, .05, 1); bsdf.inputs["Emission Strength"].default_value = 4.
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    for o in built.values(): o.hide_render = True
    views = {"CanopyBed": [("q34", Vector((-1., -1.5, .9))), ("side", Vector((-1.8, -.25, .35)))],
             "ShieldCart": [("q34", Vector((-1., -1.4, .8))), ("back", Vector((1.1, 1.4, .7)))]}
    for name, o in built.items():
        o.hide_render = False
        pts = np.array([v.co[:] for v in o.data.vertices]); lo, hi = pts.min(0), pts.max(0); size = float(max(hi - lo))
        pb.location.x = ph.location.x = hi[0] + .45
        # an orange attack beam at 1.1 m from the west, ending at the bed's king / on the cart's screen
        bpy.ops.mesh.primitive_cylinder_add(radius=.04, depth=4., location=(lo[0] - 1.8, 0 if name == "CanopyBed" else hi[1] - .2, 1.1),
                                            rotation=(0, math.pi / 2, 0))
        beam = bpy.context.object; beam.data.materials.append(beam_m)
        for vname, d in views[name]:
            tgt = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (hi[2]) * .45))
            cam.location = tgt + d.normalized() * (size * 2.1 + 1.5); cd.lens = 50
            cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = os.path.join(HERE, "render_i_dev_%s_%s.png" % (name.lower(), vname)); bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(beam)
        o.hide_render = True


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    built = {}
    for name, make in DEVICES.items():
        before = set(bpy.context.scene.objects)
        parts = [o for o in make() if o.type == 'MESH']
        built[name] = P.join(parts, "I_" + name)
        socks = [o for o in bpy.context.scene.objects if o.type == 'EMPTY' and o not in before]
        for s in socks: s.parent = built[name]
        pts = np.array([v.co[:] for v in built[name].data.vertices])
        print("DEV", name, P.tris(built[name]), "tris", "size %.2f x %.2f x %.2f" % tuple(pts.max(0) - pts.min(0)),
              "min z %.3f" % pts.min(0)[2], sorted({m.name for m in built[name].data.materials}), [s.name for s in socks])
    os.makedirs(I.OUT, exist_ok=True)
    P.OUT_FBX = I.OUT
    if "--export" in ARGS:
        for name, o in built.items():
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
            for c in o.children: c.select_set(True)
            bpy.ops.export_scene.fbx(filepath=os.path.join(I.OUT, "I_" + name + ".fbx"), use_selection=True,
                                     axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                     mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "I_Devices_v1.blend"))
    if "--render" in ARGS:
        render(built)


if __name__ == "__main__":
    main()
