# The king and the queen, modelled from their front design images (the only views there are): turned low-poly
# bodies of 16 flat sides measured row by row (piece_profile.py), and the crowns built by hand - the king's four
# ribs over a cap with a cross, the queen's scalloped cup with eight balls round a dome.
# Tables are in pixels of each design image about the axis (rows from the top). What the front view cannot show
# (depths) follows the turned shape: everything is round except the cross (a flat slab) and the ribs.
# Heights continue the lineup's steps (rook 1.15, knight 1.33, bishop 1.51 pawn bodies): queen 1.69, king 1.87.
# blender -b --factory-startup -P make_royals.py -- <out dir> [king] [queen]
import bpy, bmesh, sys, os, math, json
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lp

a = sys.argv[sys.argv.index("--") + 1:]
OUT = a[0]
WHICH = a[1:] or ["king", "queen"]
os.makedirs(OUT, exist_ok=True)
PAWN_BODY, Z_BOT = 0.88, 0.12

def save(ob, name, extra):
    me = ob.data
    counts = {}
    for d in me.attributes["part"].data:
        counts[d.value] = counts.get(d.value, 0) + 1
    rep = dict(vertices=len(me.vertices), faces=len(me.polygons),
               triangles=sum(len(p.vertices) - 2 for p in me.polygons), faces_by_part=counts,
               bounds=[[round(min(v.co[i] for v in me.vertices), 4) for i in range(3)],
                       [round(max(v.co[i] for v in me.vertices), 4) for i in range(3)]])
    rep.update(extra)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + ".blend"), check_existing=False)
    with open(os.path.join(OUT, name + "_report.json"), "w", encoding="utf-8") as f:
        json.dump(rep, f, indent=1)
    print(name.upper(), json.dumps(rep))

# ---------------------------------------------------------------------------------------------------- king
def king():
    ROW_TOP, ROW_BOT = 24.0, 1052.0
    b = lp.Builder(ROW_TOP, ROW_BOT, Z_BOT + 1.87 * PAWN_BODY, Z_BOT)
    # parts: 0 body, 1 head, 2 crown band, 3 crown cap, 4 ribs, 5 cross
    body = [(200, 1052), (209, 1044), (209, 990), (150, 924), (147, 921), (147, 885), (144, 881), (117, 848),
            (103, 808), (90, 760), (82, 700), (93, 636), (123, 624), (134, 615), (134, 590), (127, 584),
            (93, 582), (93, 542), (85, 539)]
    b.lathe(body, 0)
    # the head: a ball under the crown; its top and bottom are hidden
    b.sphere(0, 0, 432, 137, 1, 16, 6, -60.0, 30.0)
    # the crown's band: a ring round the head, closed on the hidden inside
    band = [(120, 400), (148, 400), (153, 396), (153, 350), (150, 346), (100, 346)]
    rings = [b.circle(hw, row) for hw, row in band]
    for lo, hi in zip(rings, rings[1:]):
        b.bridge(lo, hi, 2)
    b.bridge(rings[-1], rings[0], 2)
    # the cap inside the ribs: a ball standing in the band
    CAP_R, CAP_ROW = 127.0, 275.0
    b.sphere(0, 0, CAP_ROW, CAP_R, 3, 16, 7, -36.2, 90.0)
    # four ribs from the band up to the top plate: the outline of the front view, 60 px wide, their inner side in
    # the cap
    rib = [(151, 352), (158, 330), (167, 308), (174, 286), (176, 266), (173, 244), (166, 226), (156, 208),
           (142, 192), (122, 176), (100, 165), (80, 159), (36, 156)]
    W = 30.0
    def cap_r(row):
        d = row - CAP_ROW
        return math.sqrt(max(CAP_R * CAP_R - d * d, 0.0))
    for q in range(4):
        al = q * math.pi / 2                       # 0 front, then round
        er = Vector((math.sin(al), -math.cos(al)))
        et = Vector((math.cos(al), math.sin(al)))
        rr = []
        for i, (r, row) in enumerate(rib):
            inner = max(min(cap_r(row) - 8.0, r - 10.0), 8.0)
            drop = 10.0 if i == len(rib) - 1 else 0.0                   # the last ring dips under the plate
            pts = []
            for rad, sgn in ((r, -1), (r, 1), (inner, 1), (inner, -1)):
                v = er * rad + et * (W * sgn)
                pts.append(b.P(v.x, v.y, row + (drop if rad == inner else 0.0)))
            rr.append(b.ring(pts))
        for lo, hi in zip(rr, rr[1:]):
            b.bridge(lo, hi, 4)
        for r_, flip in ((rr[0], True), (rr[-1], False)):
            f = b.bm.faces.new(r_[::-1] if flip else r_)
            f[b.part] = 4
    # the top plate and the cross: a slab cut to a cross with flaring arms
    b.lathe([(41, 160), (41, 151)], 4)
    T = 14.0                                       # a thin slab: the multi-view sheet shows it edge-on from the side
    outline = [(-33, 25), (33, 25), (25, 70), (75, 64), (75, 129), (25, 115), (33, 158), (-33, 158), (-25, 115),
               (-75, 129), (-75, 64), (-25, 70)]
    front = [b.bm.verts.new(b.P(x, -T, row)) for x, row in outline]
    back = [b.bm.verts.new(b.P(x, T, row)) for x, row in outline]
    quads = [(0, 1, 2, 11), (2, 3, 4, 5), (5, 6, 7, 8), (11, 8, 9, 10), (11, 2, 5, 8)]
    for q in quads:
        f = b.bm.faces.new([front[i] for i in q]); f[b.part] = 5
        f = b.bm.faces.new([back[i] for i in q][::-1]); f[b.part] = 5
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        f = b.bm.faces.new((front[i], back[i], back[j], front[j])); f[b.part] = 5
    ob = b.finish("King_Final", "King")
    save(ob, "King_Final", dict(metres_per_pixel=b.S, arm_z=round(b.Z(672), 4), arm_body_radius=round(84 * b.S, 4),
                                head=dict(z=round(b.Z(432), 4), radius=round(137 * b.S, 4)),
                                eye=dict(z=round(b.Z(437), 4), x=round(47.5 * b.S, 4), w=round(40 * b.S, 4),
                                         h=round(62 * b.S, 4)),
                                image=dict(axis=625.0, top=ROW_TOP, bottom=ROW_BOT, size=1254)))

# ---------------------------------------------------------------------------------------------------- queen
def queen():
    ROW_TOP, ROW_BOT = 41.0, 1147.0
    b = lp.Builder(ROW_TOP, ROW_BOT, Z_BOT + 1.69 * PAWN_BODY, Z_BOT)
    # parts: 0 body, 1 head, 2 crown (band and cup), 3 dome, 4 balls
    body = [(246, 1147), (256, 1139), (256, 1110), (240, 1090), (240, 1048), (190, 983), (184, 978), (184, 943),
            (176, 936), (168, 927), (154, 903), (138, 863), (120, 815), (102, 767), (89, 719), (80, 671),
            (80, 652), (86, 647), (88, 636), (86, 626), (82, 621),                       # the waist ring
            (84, 600), (88, 583), (95, 567), (103, 551), (117, 537), (126, 530), (126, 504), (122, 501),
            (82, 499), (82, 458), (70, 458)]
    b.lathe(body, 0)
    # the head: a bowl under the crown
    head = [(68, 460), (81, 447), (98, 431), (114, 407), (125, 383), (133, 351), (139, 319), (142, 300)]
    b.lathe(head, 1)
    # the crown: band and flaring cup in 32 sides, corners to the front, so that the eight points stand at the
    # front, the sides and between; the rim dips between the points; an inner wall goes down into the dome
    NC = 32
    low = [(130, 314), (146, 312), (155, 304), (157, 295), (155, 287), (149, 280), (166, 243)]
    rings = [b.circle(hw, row, NC, True) for hw, row in low]
    for lo, hi in zip(rings, rings[1:]):
        b.bridge(lo, hi, 2)
    b.fan(rings[0], b.P(0, 0, low[0][1]), False, 2)
    def rim(k, inset):
        role = k % 4
        r, row = ((205.0, 176.0), (201.0, 188.0), (197.0, 196.0), (201.0, 188.0))[role]
        th = -math.pi / 2 + k * 2 * math.pi / NC
        return b.P((r - inset) * math.cos(th), (r - inset) * math.sin(th), row)
    outer = b.ring([rim(k, 0.0) for k in range(NC)])
    inner = b.ring([rim(k, 9.0) for k in range(NC)])
    b.bridge(rings[-1], outer, 2)
    b.bridge(outer, inner, 2)
    wall = b.circle(116, 236, NC, True)
    b.bridge(inner, wall, 2)
    b.fan(wall, b.P(0, 0, 236), False, 2)
    # the dome in the cup and the ball on it
    DOME_R, DOME_ROW = 125.4, 220.0
    b.sphere(0, 0, DOME_ROW, DOME_R, 3, 16, 7, -9.0, 90.0)
    b.sphere(0, 0, 71, 30.5, 4, 16, 8)
    # eight balls on the points
    for k in range(8):
        th = -math.pi / 2 + k * math.pi / 4
        b.sphere(188.0 * math.cos(th), 188.0 * math.sin(th), 165, 25.0, 4, 8, 6)
    ob = b.finish("Queen_Final", "Queen")
    save(ob, "Queen_Final", dict(metres_per_pixel=b.S, arm_z=round(b.Z(639), 4), arm_body_radius=round(88 * b.S, 4),
                                 eye=dict(z=round(b.Z(369), 4), x=round(56 * b.S, 4), w=round(56 * b.S, 4),
                                          h=round(83 * b.S, 4)),
                                 image=dict(axis=618.5, top=ROW_TOP, bottom=ROW_BOT, size=[1236, 1272])))

for w in WHICH:
    dict(king=king, queen=queen)[w]()
