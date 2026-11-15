# The bishop of the final design, modelled from measurements of the design images (bishop_measure.py,
# bishop_analyze.py): a turned low-poly body (N sides, flat faces), and the head as the game's earlier bishop had
# it - a teardrop whose back is cut away, a separate petal standing behind it, a V gap between the two that closes
# at the head's widest height and runs on below as a seam.
#
# All tables are in pixels of the front design image (1254 px, rows from the top): half widths about the axis,
# depths behind the axis (+ = back; the piece faces -y). Z() turns a row into metres: the base's underside (row 1167)
# stands at 0.12 m on the feet, the ball's top (row 44) is at 1.447 m, as the bishop now in the game.
#
# blender -b --factory-startup -P make_bishop.py -- <out dir> [--sides 16] [--name Bishop_Final]
import bpy, bmesh, sys, os, math, json
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
OUT = a[0]
def opt(name, default):
    return a[a.index(name) + 1] if name in a else default
N = int(opt("--sides", "16"))
NAME = opt("--name", "Bishop_Final")
os.makedirs(OUT, exist_ok=True)

ROW_TOP, ROW_BOT = 44.0, 1167.0
Z_TOP, Z_BOT = 1.447, 0.12
S = (Z_TOP - Z_BOT) / (ROW_BOT - ROW_TOP)          # metres per pixel
C = math.cos(math.pi / N)                          # a flat face's centre over its corners' radius

def Z(row):
    return Z_BOT + (ROW_BOT - row) * S

def ang(k):
    """Corner k of a ring: a face is centred on the front (-y), corners go round counter-clockwise from above."""
    return -math.pi / 2 + (k + 0.5) * 2 * math.pi / N

# ---------------------------------------------------------------- measurements
# The turned body, bottom to top: (half width, row). Skirt rings between its ends lie on the straight cone.
SKIRT_BOT, SKIRT_TOP = (201.5, 1104.0), (82.5, 795.0)
def skirt(row):
    t = (row - SKIRT_TOP[1]) / (SKIRT_BOT[1] - SKIRT_TOP[1])
    return (SKIRT_TOP[0] + t * (SKIRT_BOT[0] - SKIRT_TOP[0]), row)
DISC_RIM = (153.0, 664.0)
DISC_SLOPE = 1.4306                                # the disc's top cone: pixels of radius per row
def disc_cone(row):
    return DISC_RIM[0] - DISC_SLOPE * (DISC_RIM[1] - row)
BODY = [
    (205.0, 1167.0),                # underside
    (221.0, 1160.0),                # the base's chamfer
    (228.0, 1143.0),                # the base at its widest
    (222.0, 1129.0),                # under the bead
    (214.5, 1112.0),                # the bead's shoulder
    SKIRT_BOT, skirt(1027.0), skirt(950.0), skirt(872.0), SKIRT_TOP,
    (87.5, 789.0), (90.5, 781.0), (90.5, 775.0), (86.0, 767.5), (77.0, 761.0),    # the ring band
    (77.0, 720.0), (79.0, 710.0), (83.0, 702.0),                                    # the neck column and its flare
    (88.0, 697.5), (122.0, 692.0),                                                  # the disc's underside
    (153.0, 677.0), DISC_RIM,                                                       # the disc's rim
    (disc_cone(601.8), 601.8),      # the top cone runs on under the head, closed there
]

# The head (front teardrop): (row, half width) from the front view. The first and last rings are hidden (inside
# the ball, inside the disc).
HEAD = [(128.0, 17.0), (220.0, 72.5), (300.0, 114.0), (391.0, 153.5), (428.0, 165.0), (469.0, 170.5),
        (512.0, 165.5), (550.0, 152.0), (592.0, 126.5), (623.7, 94.9), (634.0, 80.0)]
APEX_ROW = 437.0                                   # where the V between head and petal closes (side view)
HEAD_BACK = [(128.0, 0.5), (140.0, 3.0), (224.0, 20.0), (304.0, 35.0), (384.0, 48.0), (416.0, 53.0), (437.0, 56.3)]
def head_back(row):
    """The head's back edge above the V, seen from the side: nearly a straight line from the tip (measured on the
    side view, half a pixel added for the flat faces)."""
    for (r0, d0), (r1, d1) in zip(HEAD_BACK, HEAD_BACK[1:]):
        if r0 <= row <= r1:
            return d0 + (d1 - d0) * (row - r0) / (r1 - r0)
    return HEAD_BACK[0][1] if row < HEAD_BACK[0][0] else HEAD_BACK[-1][1]
SEAM = [(437.0, 55.8), (469.0, 59.0), (512.0, 57.5), (550.0, 48.5), (584.0, 33.0), (592.0, 28.0), (612.0, 16.0),
        (623.7, 9.0), (634.0, 4.0)]                # the seam under the V (side view): (row, depth)
def seam(row):
    for (r0, d0), (r1, d1) in zip(SEAM, SEAM[1:]):
        if r0 <= row <= r1:
            return d0 + (d1 - d0) * (row - r0) / (r1 - r0)
    return SEAM[0][1] if row < SEAM[0][0] else SEAM[-1][1]
def head_shift(row):
    """The teardrop leans forward a little: its centre is 10 px in front of the axis at the tip, on it at the V."""
    return max(-10.5, min(3.0, -10.0 + 0.03233 * (row - 140.0)))
CONVEX = 0.04                                      # the cut back bulges by this much of the half width

# The petal: (row, half width from the back view, front face depth, back depth), under its tip.
PETAL_TIP = (229.5, 93.0)                          # row, depth
PETAL = [(234.0, 5.0, 86.5, 99.5), (250.0, 19.0, 82.8, 108.5), (300.0, 59.0, 75.6, 127.0),
         (391.0, 131.0, 62.5, 150.0), (428.0, 153.0, 57.1, 152.5), (469.0, 161.5, 57.5, 150.5),
         (512.0, 161.0, 56.0, 139.0), (550.0, 150.5, 47.0, 123.0), (584.0, 131.0, 31.5, 97.0),
         (612.0, 107.5, 14.5, 66.0)]
BEVEL = 4.0                                        # the petal's rim is chamfered by this much

BALL = (92.5, 48.5)                                # centre row, radius

# ---------------------------------------------------------------- building
bm = bmesh.new()
part_layer = bm.faces.layers.int.new("part")       # 0 body, 1 head, 2 petal, 3 ball
P = lambda x, y, row: Vector((x * S, y * S, Z(row)))

def ring(pts):
    return [bm.verts.new(p) for p in pts]

def bridge(lo, hi, part):
    n = len(lo)
    for k in range(n):
        k1 = (k + 1) % n
        f = bm.faces.new((lo[k], lo[k1], hi[k1], hi[k]))
        f[part_layer] = part

def fan(r, centre, top, part):
    c = bm.verts.new(centre)
    n = len(r)
    for k in range(n):
        k1 = (k + 1) % n
        f = bm.faces.new((r[k], r[k1], c) if top else (r[k1], r[k], c))
        f[part_layer] = part

def lathe_ring(hw, row):
    rv = hw / C
    return ring([P(rv * math.cos(ang(k)), rv * math.sin(ang(k)), row) for k in range(N)])

# the body
rings = [lathe_ring(hw, row) for hw, row in BODY]
for lo, hi in zip(rings, rings[1:]):
    bridge(lo, hi, 0)
fan(rings[0], P(0, 0, BODY[0][1]), False, 0)
fan(rings[-1], P(0, 0, BODY[-1][1]), True, 0)

# the head: the front half of each ring is the lathe's circle about the leaning centre; the back half is cut:
# the circle's corner just behind the side, a corner where the circle meets the cut, the rest across the cut face
M = N // 4
TH = [(j + 0.5) * 2 * math.pi / N for j in range(M)]         # the back half's lathe angles on one side
def head_ring(row, hw):
    av = hw / C
    sh = head_shift(row)
    conv = CONVEX * hw
    if row <= APEX_ROW:
        centre = head_back(row)                    # the side view's back edge
        conv = min(conv, 0.5 * (centre - sh))
        corner = centre - conv
    else:
        corner = seam(row)                         # the corner lies on the seam; the bulge hides in the petal
        centre = corner + conv
    cl = max(corner - sh, 0.05 * av)               # the cut's depth behind the ring's own centre
    yf = min(av * math.sin(TH[0]), 0.62 * cl)
    side = [(math.sqrt(av * av - yf * yf), sh + yf)]                                 # just behind the side
    xc = math.sqrt(max(av * av - cl * cl, (0.3 * av) ** 2))
    side.append((xc, sh + cl))                                                        # where the cut starts
    for j in range(2, M):
        x = xc * math.cos(TH[j]) / math.cos(TH[1])
        side.append((x, sh + cl + conv * (1 - (x / xc) ** 2)))
    pts = []
    for k in range(N):
        if M <= k < 2 * M:                         # right side of the back half
            x, y = side[k - M]
        elif 2 * M <= k < 3 * M:                   # left side, mirrored
            x, y = side[3 * M - 1 - k]
            x = -x
        else:
            x, y = av * math.cos(ang(k)), sh + av * math.sin(ang(k))
        pts.append(P(x, y, row))
    return ring(pts), dict(row=row, shift=sh, corner=sh + cl, centre=sh + cl + conv, xc=xc)

head_info = []
rings = []
for row, hw in HEAD:
    r, info = head_ring(row, hw)
    rings.append(r)
    head_info.append(info)
for hi, lo in zip(rings, rings[1:]):               # the table runs from the top down
    bridge(lo, hi, 1)
fan(rings[0], P(0, head_shift(HEAD[0][0]), HEAD[0][0]), True, 1)
fan(rings[-1], P(0, head_shift(HEAD[-1][0]), HEAD[-1][0]), False, 1)

# the petal: a half ellipse behind its flat front face, a chamfered rim, corners at the lathe's angles
PTH = [(j + 0.5) * 2 * math.pi / N for j in range(N // 2)]
def petal_ring(row, hw, yf, yb):
    t = (yb - yf) / math.sin(PTH[N // 4 - 1])      # so that the face on the back's middle reaches yb
    c = min(BEVEL, 0.3 * hw, 0.6 * t * math.sin(PTH[0]))
    pts = [P(hw - c, yf, row), P(hw, yf + c, row)]
    pts += [P(hw * math.cos(th), yf + t * math.sin(th), row) for th in PTH]
    pts += [P(-hw, yf + c, row), P(-(hw - c), yf, row)]
    return ring(pts)
rings = [petal_ring(*p) for p in PETAL]
for hi, lo in zip(rings, rings[1:]):
    bridge(lo, hi, 2)
tip = bm.verts.new(P(0, PETAL_TIP[1], PETAL_TIP[0]))
n = len(rings[0])
for k in range(n):
    f = bm.faces.new((rings[0][k], rings[0][(k + 1) % n], tip))
    f[part_layer] = 2
last = PETAL[-1]
fan(rings[-1], P(0, (last[2] + last[3]) / 2, last[0]), False, 2)

# the ball: a sphere of N sides and N/2 bands
rb = BALL[1] / C
lat = N // 2
rings = []
for i in range(1, lat):
    ph = -math.pi / 2 + i * math.pi / lat
    rings.append(ring([Vector((rb * S * math.cos(ph) * math.cos(ang(k)), rb * S * math.cos(ph) * math.sin(ang(k)),
                               Z(BALL[0]) + rb * S * math.sin(ph))) for k in range(N)]))
for lo, hi in zip(rings, rings[1:]):
    bridge(lo, hi, 3)
fan(rings[0], Vector((0, 0, Z(BALL[0]) - BALL[1] * S)), False, 3)
fan(rings[-1], Vector((0, 0, Z_TOP)), True, 3)

bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
bpy.ops.wm.read_factory_settings(use_empty=True)
me = bpy.data.meshes.new(NAME)
bm.to_mesh(me)
bm.free()
for p in me.polygons:
    p.use_smooth = False
ob = bpy.data.objects.new("Bishop", me)
bpy.context.scene.collection.objects.link(ob)

part = me.attributes["part"].data
counts = [0, 0, 0, 0]
for d in part:
    counts[d.value] += 1
zs = [v.co.z for v in me.vertices]
report = dict(
    sides=N, vertices=len(me.vertices), faces=len(me.polygons), triangles=sum(len(p.vertices) - 2 for p in me.polygons),
    faces_by_part=dict(body=counts[0], head=counts[1], petal=counts[2], ball=counts[3]),
    bounds=[[round(min(v.co[i] for v in me.vertices), 4) for i in range(3)],
            [round(max(v.co[i] for v in me.vertices), 4) for i in range(3)]],
    metres_per_pixel=S,
    neck_column=dict(z_bottom=round(Z(761.0), 4), z_top=round(Z(702.0), 4), z_middle=round(Z(731.5), 4),
                     radius=round(77.0 * S, 4)),
    ring_band=[round(Z(795.0), 4), round(Z(761.0), 4)], disc_rim=[round(Z(677.0), 4), round(Z(664.0), 4)],
    head=dict(z_bottom=round(Z(623.7), 4), z_widest=round(Z(469.0), 4), radius=round(170.5 * S, 4),
              v_apex_z=round(Z(APEX_ROW), 4), tip_z=round(Z(136.0), 4)),
    petal=dict(tip_z=round(Z(PETAL_TIP[0]), 4), tip_y=round(PETAL_TIP[1] * S, 4)),
    ball=dict(z=round(Z(BALL[0]), 4), radius=round(BALL[1] * S, 4)),
    head_rings=[{k: round(v, 2) for k, v in h.items()} for h in head_info],
)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + ".blend"), check_existing=False)
with open(os.path.join(OUT, NAME + "_report.json"), "w", encoding="utf-8") as f:
    json.dump(report, f, indent=1)
print("BISHOP", json.dumps({k: report[k] for k in ("sides", "vertices", "faces", "triangles", "faces_by_part", "bounds",
                                                    "neck_column", "head", "petal", "ball")}))
