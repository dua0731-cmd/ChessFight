# The rooks' rampart walk (룩 성벽길, level design v0.1) stacked up in Blender from rampart_layout.py's numbers and
# dressed in the concept picture's look (cream stone, gold, white crown banners, blue roofs, cliffs, the river), every
# moving thing posed where the shared clock has it at T. Three stills: the whole place from the south-east, the outer
# wall and the lower ward from the south-west, the cranes and the inner gatehouse from the north-west.
# A picture only: nothing here goes into Unity. Section frame: x east, y up, z north; in Blender (X, Y, Z) = (x, z, y).
#
#   blender --background --factory-startup --python rampart_blockout.py -- <out.png> [samples] [width] [height]
#     (writes <out>.png, <out>_walls.png, <out>_cranes.png; RAMPART_SHOTS="overview,walls,cranes" picks some)
#
# The box / cylinder / bar helpers are ../RoyalObservatory/observatory_dress_preview.py's (its head, up to its first
# geometry, is run here: it reads the arguments and empties the scene). The chess pieces are the game's own
# (Assets/Art/PawnRushMap/Resources/Pieces/PR_Piece_*.fbx).
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler

_here = os.path.dirname(os.path.abspath(__file__))
_head = open(os.path.join(_here, "..", "RoyalObservatory", "observatory_dress_preview.py"), encoding="utf-8").read()
exec(compile(_head.split("# entry, ring court")[0], "observatory_dress_preview.py (head)", "exec"), globals())
L = {"__name__": "rampart_layout"}
exec(compile(open(os.path.join(_here, "rampart_layout.py"), encoding="utf-8").read(), "rampart_layout.py", "exec"), L)
ROOT = os.path.normpath(os.path.join(_here, "..", "..", ".."))
random.seed(5)
T = 13.75        # the clock's moment: ladders down and up, light trapdoors open, one siege tower at each wall, two rook lines lit

# ---------------------------------------------------------------- the place's colours (the concept picture)
M_STONE2 = marble("MAT_RampartStone", (0.80, 0.73, 0.60), (0.72, 0.65, 0.52), 0.55)
M_WALK = principled("MAT_WalkPaving", (0.68, 0.61, 0.50), 0.65)
M_ROCK = principled("MAT_Rock", (0.40, 0.35, 0.29), 0.85)
M_GRASS = principled("MAT_Grass", (0.20, 0.34, 0.09), 0.9)
M_ROOF = principled("MAT_BlueRoof", (0.06, 0.12, 0.34), 0.45)
M_WOOD = principled("MAT_Wood", (0.34, 0.20, 0.09), 0.7)
M_WOOD2 = principled("MAT_WoodLight", (0.56, 0.39, 0.21), 0.7)
M_HIDE = principled("MAT_WetHide", (0.24, 0.15, 0.09), 0.55)
M_IVORY = principled("MAT_Ivory", (0.88, 0.84, 0.74), 0.35)
M_KNIGHTW = principled("MAT_KnightWood", (0.48, 0.29, 0.13), 0.45)
M_BANNERC = principled("MAT_BannerCloth", (0.94, 0.92, 0.86), 0.8)
M_RIVER = principled("MAT_River", (0.07, 0.24, 0.38), 0.08)
M_FALLS = principled("MAT_Falls", (0.92, 0.96, 1.0), 0.3, 0.0, (0.8, 0.9, 1.0), 0.25)
M_GLOW = principled("MAT_RookLine", (1.0, 0.75, 0.25), 0.4, 0.0, (1.0, 0.45, 0.08), 5.0)
M_HOLE = principled("MAT_Hole", (0.015, 0.012, 0.01), 1.0)
M_SQL = principled("MAT_SquareLight", (0.88, 0.82, 0.70), 0.4)
M_SQD = principled("MAT_SquareDark", (0.30, 0.21, 0.13), 0.4)
M_CIRCLE = principled("MAT_LandingCircle", (0.85, 0.12, 0.08), 0.5, 0.0, (0.9, 0.12, 0.05), 0.8)
M_IRON = principled("MAT_Iron", (0.06, 0.06, 0.065), 0.45, 0.8)
M_ROPE = principled("MAT_Rope", (0.62, 0.52, 0.36), 0.85)
M_BLACKP = principled("MAT_PawnBlack", (0.07, 0.065, 0.07), 0.45)
M_LEAF = principled("MAT_Leaf", (0.10, 0.30, 0.08), 0.8)
M_SKYDOOR = principled("MAT_KeepDoor", (1, 0.92, 0.75), 0.5, 0.0, (1.0, 0.85, 0.6), 5.0)


# ---------------------------------------------------------------- helpers
def heading_rot(dx, dz):
    return (0.0, 0.0, math.atan2(dz, dx))


def seg_box(name, a, b, off0, off1, y0, y1, mat, ext=0.0):
    # A box along a -> b (section x, z), from off0 to off1 along its inward (left-hand, north) normal, y0 .. y1.
    dx, dz = b[0] - a[0], b[1] - a[1]
    n = math.hypot(dx, dz)
    ux, uz = dx / n, dz / n
    nx, nz = -uz, ux
    mx, mz = (a[0] + b[0]) / 2 + nx * (off0 + off1) / 2, (a[1] + b[1]) / 2 + nz * (off0 + off1) / 2
    return inst(ME_CUBE, name, mat, U(mx, (y0 + y1) / 2, mz), (n + 2 * ext, abs(off1 - off0), abs(y1 - y0)), heading_rot(dx, dz))


def prism(name, pts, y0, y1, mat):
    bm = bmesh.new()
    lo = [bm.verts.new(U(x, y0, z)) for x, z in pts]
    hi = [bm.verts.new(U(x, y1, z)) for x, z in pts]
    bm.faces.new(lo[::-1])
    bm.faces.new(hi)
    n = len(pts)
    for i in range(n):
        bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    return o


def lathe(name, profile, at, mat, sides=40):
    # (radius, height) pairs from the bottom up, turned round the vertical at `at` (section point).
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        if r < 1e-4:
            rings.append([bm.verts.new(U(at[0], at[1] + h, at[2]))])
        else:
            rings.append([bm.verts.new(U(at[0] + r * math.cos(2 * math.pi * j / sides), at[1] + h, at[2] + r * math.sin(2 * math.pi * j / sides)))
                          for j in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for j in range(sides):
            if len(a) == 1:
                bm.faces.new((a[0], b[(j + 1) % sides], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[j], a[(j + 1) % sides], b[0]))
            else:
                bm.faces.new((a[j], a[(j + 1) % sides], b[(j + 1) % sides], b[j]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    return o


def piece_mesh(name, head_toward=None):
    # The game's piece: its transform baked, its foot on 0, centred, 1 high.
    path = os.path.join(ROOT, "Assets", "Art", "PawnRushMap", "Resources", "Pieces", "PR_Piece_%s.fbx" % name)
    before = set(bpy.data.objects)
    bpy.ops.wm.fbx_import(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    src = [o for o in new if o.type == "MESH"][0]
    me = src.data.copy()
    me.transform(src.matrix_world)
    xs, ys, zs = [v.co.x for v in me.vertices], [v.co.y for v in me.vertices], [v.co.z for v in me.vertices]
    h = max(zs) - min(zs)
    me.transform(Matrix.Translation(Vector((-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs)))))
    me.transform(Matrix.Scale(1.0 / h, 4))
    for o in new:
        bpy.data.objects.remove(o)
    me.materials.clear()
    me.materials.append(None)
    for p in me.polygons:
        p.use_smooth = True
    if head_toward is not None:
        # Turn it so its head (the top third's centroid) points along +X (Blender), i.e. section east.
        top = [v.co for v in me.vertices if v.co.z > 0.62]
        cx, cy = sum(v.x for v in top) / len(top), sum(v.y for v in top) / len(top)
        me.transform(Matrix.Rotation(-math.atan2(cy, cx), 4, "Z"))
    return me


ME_ROOKP = piece_mesh("Rook")
ME_KNIGHTP = piece_mesh("Knight", head_toward=True)


def piece(me, name, mat, x, y, z, height, yaw_deg=0.0):
    # Section point of its foot; yaw turns it from facing east (+x) toward north (+z).
    return inst(me, name, mat, U(x, y, z), (height, height, height), (0.0, 0.0, math.radians(yaw_deg)))


def player(x, y, z, team):
    pawn(x, y, z, team)


def banner(name, x, y0, y1, z0, z1, face_x, mat=None):
    # A white crown banner flat on a face (x = face_x, hanging toward -x or +x by the sign of x - face_x).
    t = 0.06
    o = box(name, min(x, face_x), max(x, face_x) if abs(x - face_x) > t else face_x + (t if x > face_x else -t), y0, y1, z0, z1, mat or M_BANNERC)
    box(name + " hem", min(x, face_x) - 0.02, max(x, face_x) + 0.02, y0 - 0.25, y0 + 0.1, z0 - 0.05, z1 + 0.05, M_GOLD)
    ball(name + " crown", x + (0.06 if x > face_x else -0.06), (y0 + y1) / 2 + 0.3, (z0 + z1) / 2, min(1.2, (z1 - z0) * 0.45), M_GOLD)
    return o


def banner_z(name, z, y0, y1, x0, x1, face_z):
    # The same on a face at z = face_z, toward -z (south) or +z.
    s = -1 if z < face_z else 1
    box(name, x0, x1, y0, y1, face_z + s * 0.06, face_z, M_BANNERC)
    box(name + " hem", x0 - 0.05, x1 + 0.05, y0 - 0.25, y0 + 0.1, face_z + s * 0.08, face_z, M_GOLD)
    ball(name + " crown", (x0 + x1) / 2, (y0 + y1) / 2 + 0.3, face_z + s * 0.1, min(1.2, (x1 - x0) * 0.45), M_GOLD)


# ---------------------------------------------------------------- the ground: the spur, the ward, the ditch, the bailey, the gorges
SPUR = L["SPUR_X"]
box("Spur rock", -SPUR, SPUR, -34.0, L["DITCH_Y"], -12.0, 110.0, M_ROCK)
w2f, w3f = L["wall2_face_z"], L["wall3_face_z"]
GT = L["GREAT_TOWER"]
ward = [(-SPUR, -12.0), (SPUR, -12.0), (SPUR, w2f(SPUR)), (GT[1], w2f(GT[1] + 0.01)), (GT[1], GT[2]), (GT[0], GT[2]), (GT[0], w2f(GT[0] - 0.01)), (-SPUR, w2f(-SPUR))]
prism("Glacis and lower ward", ward, L["DITCH_Y"], L["WARD"], M_ROCK)
prism("Glacis and lower ward grass", ward, L["WARD"] - 0.05, L["WARD"] + 0.02, M_GRASS)
GH = L["GATEHOUSE"]
bailey = [(-SPUR, w3f(-SPUR)), (GH[0], GH[2]), (GH[1], GH[2]), (SPUR, w3f(SPUR)), (SPUR, 110.0), (-SPUR, 110.0)]
prism("Inner bailey", bailey, L["DITCH_Y"], L["BAILEY_Y"], M_ROCK)
prism("Inner bailey grass", bailey, L["BAILEY_Y"] - 0.05, L["BAILEY_Y"] + 0.02, M_GRASS)
for s in (-1, 1):
    box("Gorge river", s * SPUR, s * 96.0, L["RIVER_Y"] - 1.0, L["RIVER_Y"], -40.0, 140.0, M_RIVER)
    box("Far cliff", s * 70.0, s * 110.0, -34.0, 6.0, -40.0, 140.0, M_ROCK)
    box("Far cliff grass", s * 70.0, s * 110.0, 6.0, 6.3, -40.0, 140.0, M_GRASS)
    for k in range(5):
        z = -6.0 + 26.0 * k + random.uniform(-4, 4)
        box("Waterfall", s * 70.05, s * 70.25, -31.5, 5.0, z, z + random.uniform(3.0, 6.0), M_FALLS)
    for k in range(40):
        x, z = s * random.uniform(72.0, 105.0), random.uniform(-36.0, 136.0)
        hgt = random.uniform(5.0, 9.0)
        inst(ME_CONE, "Pine", M_LEAF, U(x, 6.3 + hgt / 2, z), (2.6, 2.6, hgt))
    for k in range(10):
        z = random.uniform(-8.0, 104.0)
        hgt = random.uniform(3.5, 6.0)
        inst(ME_CONE, "Cliff pine", M_LEAF, U(s * (SPUR - 0.5), L["DITCH_Y"] + 2.0 + hgt / 2, z), (1.8, 1.8, hgt))
for z0, z1 in ((-40.0, -14.0), (114.0, 140.0)):
    box("Low bridge pier", -SPUR - 8, -SPUR - 3, L["RIVER_Y"], -12.0, z0 + 10, z0 + 13, M_STONE2)
box("Arched bridge (gorge)", -SPUR - 26, -SPUR, -13.0, -11.5, -24.0, -20.0, M_STONE2)
box("Arched bridge (gorge) east", SPUR, SPUR + 26, -13.0, -11.5, 118.0, 122.0, M_STONE2)


# ---------------------------------------------------------------- the walls
def wall(name, points, y0, walk, gaps=(), body=True):
    # Body, walk paving, the parapet's low part and merlons every 3 m (1.8 merlon, 1.2 crenel); gaps: x ranges
    # with neither merlon nor parapet (a hoist door), or (x, half) crenels kept clear (a ladder, a siege bridge).
    for a, b in zip(points, points[1:]):
        if body:
            seg_box(name + " body", a, b, 0.0, L["WALL_T"], y0, walk, M_STONE2, 0.15)
            seg_box(name + " walk", a, b, L["PARAPET"], L["WALL_T"], walk - 0.05, walk + 0.02, M_WALK, 0.1)
        dx, dz = b[0] - a[0], b[1] - a[1]
        n = math.hypot(dx, dz)
        k = 0
        while True:
            s = 1.0 + 3.0 * k
            if s > n - 0.4:
                break
            k += 1
            mx = a[0] + dx * s / n
            mz = a[1] + dz * s / n
            if any(g0 <= mx <= g1 for g0, g1 in gaps):
                continue
            p0 = (a[0] + dx * (s - 1.5) / n, a[1] + dz * (s - 1.5) / n)
            p1 = (a[0] + dx * (s + 1.5) / n, a[1] + dz * (s + 1.5) / n)
            seg_box(name + " parapet", p0, p1, 0.0, L["PARAPET"], walk, walk + L["SILL_H"], M_STONE2)
            m0 = (a[0] + dx * (s - 0.9) / n, a[1] + dz * (s - 0.9) / n)
            m1 = (a[0] + dx * (s + 0.9) / n, a[1] + dz * (s + 0.9) / n)
            seg_box(name + " merlon", m0, m1, 0.0, L["PARAPET"], walk + L["SILL_H"], walk + L["MERLON_H"], M_STONE2)


wall("Outer wall", L["WALL1"], -1.0, L["W1_Y"])
sx = L["SIEGE_X"]
wall("Middle wall east", L["WALL2_E"], L["DITCH_Y"], L["W2_Y"], [(x - 1.7, x + 1.7) for x in sx])
wall("Middle wall west", L["WALL2_W"], L["DITCH_Y"], L["W2_Y"])
wall("Inner wall west", L["WALL3_W"], L["DITCH_Y"], L["W3_Y"], [(-33.5, -21.0)])
wall("Inner wall east", L["WALL3_E"], L["DITCH_Y"], L["W3_Y"])


def tower(x, z, r, top, bottom, roof=True):
    cyl("Tower", x, bottom, top, z, 2 * r, M_STONE2)
    torus("Tower string", x, top - 0.1, z, r + 0.05, 0.12, M_GOLD)
    for j in range(8):
        a = 2 * math.pi * (j + 0.5) / 8
        cx, cz = x + (r - 0.45) * math.cos(a), z + (r - 0.45) * math.sin(a)
        inst(ME_CUBE, "Tower merlon", M_STONE2, U(cx, top + 0.6, cz), (1.6, 0.9, 1.2), (0, 0, a + math.pi / 2))
    if roof:
        inst(ME_CONE, "Tower roof", M_ROOF, U(x, top + 0.2 + r * 0.9, z), (2 * r * 0.92, 2 * r * 0.92, r * 1.8))
        cyl("Roof spire", x, top + 0.2 + r * 1.8, top + 0.2 + r * 2.4, z, 0.12, M_GOLD, ME_CYL8)
        ball("Roof finial", x, top + 0.25 + r * 2.4, z, 0.4, M_GOLD)


for i, (x, z, r, top) in enumerate(L["TOWERS"]):
    tower(x, z, r, top, L["DITCH_Y"] if i >= 4 else -1.0, roof=i not in (1, 2))

# ---------------------------------------------------------------- ② the outer gate (barbican) and its murder holes
G0, G1 = L["GATE_X"]
box("Outer gate", G0, G1, -1.0, L["W1_Y"], 3.0, 12.0, M_STONE2)
box("Outer gate passage", G0 + 2.5, G1 - 2.5, 0.0, 2.9, 2.95, 18.5, M_HOLE)
for x in [G0 + 2.6 + 0.45 * k for k in range(11)]:
    box("Portcullis bar", x, x + 0.12, 0.0, 2.9, 2.85, 2.97, M_IRON)
for y in (0.7, 1.5, 2.3):
    box("Portcullis rail", G0 + 2.5, G1 - 2.5, y, y + 0.12, 2.85, 2.97, M_IRON)
wall("Outer gate parapet", [(G0, 3.0), (G1, 3.0)], L["W1_Y"] - 0.3, L["W1_Y"], body=False)
for xg in (G0, G1):
    cyl("Gate turret", xg, -1.0, 7.0, 3.0, 4.2, M_STONE2)
    inst(ME_CONE, "Gate turret roof", M_ROOF, U(xg, 7.0 + 1.9, 3.0), (4.0, 4.0, 3.8))
banner_z("Outer gate banner", 2.9, 0.3, 2.6, G0 + 0.6, G0 + 2.2, 3.0)
for i in range(L["HOLES_X"]):
    for j in range(L["HOLES_Z"]):
        x0, z0 = G0 + i * L["HOLE"], 12.0 + j * L["HOLE"]
        state = L["trapdoor"](i, j, T)
        if state == 2:
            box("Murder hole", x0 + 0.08, x0 + 1.92, L["W1_Y"] - 0.6, L["W1_Y"] + 0.01, z0 + 0.08, z0 + 1.92, M_HOLE)
            lid = inst(ME_CUBE, "Trapdoor (open)", M_WOOD, U(x0 + 0.1, L["W1_Y"] - 0.95, z0 + 1.0), (0.1, 1.84, 1.84), (0.0, 0.0, 0.0))
        else:
            lift = 0.08 if state == 1 else 0.0
            box("Trapdoor", x0 + 0.06, x0 + 1.94, L["W1_Y"] - 0.04 + lift, L["W1_Y"] + 0.04 + lift, z0 + 0.06, z0 + 1.94, M_SQL if (i + j) % 2 == 0 else M_SQD)

# ---------------------------------------------------------------- ① the siege ladders, the defenders, the crews
LL, climb = L["LADDER_L"], L["LADDER_CLIMB"]
for i, x in enumerate(L["LADDERS"]):
    a = math.radians(L["ladder_angle"](i, T))
    zf = 11.0 - LL * math.cos(math.radians(L["LEAN"])) + 0.1   # the foot, out from the face
    d = (math.sin(a), math.cos(a))                               # (up, north)
    top = (zf + d[1] * LL, d[0] * LL)
    for s in (-0.6, 0.6):
        bar("Ladder rail", (x + s, 0.0, zf), (x + s, top[1], top[0]), 0.12, M_WOOD2)
    for k in range(1, int(LL / 0.4)):
        p = (zf + d[1] * 0.4 * k, d[0] * 0.4 * k)
        bar("Ladder rung", (x - 0.6, p[1], p[0]), (x + 0.6, p[1], p[0]), 0.07, M_WOOD2)
    for s in (-1, 1):                                            # the crew's ropes and the crew
        bar("Ladder rope", (x + s * 0.6, top[1], top[0]), (x + s * 1.6, 0.9, zf - 3.4), 0.04, M_ROPE)
    player(x - 1.2, 0.0, zf - 3.6, 1)
for dd in range(2):
    dxp = L["defender_x"](dd, T)
    piece(ME_ROOKP, "Rook defender", M_IVORY, dxp, L["W1_Y"], 13.4, 2.6)
    bar("Forked pole", (dxp, L["W1_Y"] + 1.9, 13.0), (dxp + 0.2, L["W1_Y"] + 1.2, 10.4), 0.12, M_WOOD)

# ---------------------------------------------------------------- ③ the siege towers
SW, FL = L["SIEGE_W"], L["SIEGE_FLOORS"]
for k, x in enumerate(sx):
    z, bridge = L["siege"](k, T)
    h = FL[2] + 1.3
    box("Siege tower side", x - SW / 2, x - SW / 2 + 0.25, 0.9, h, z - SW / 2, z + SW / 2, M_HIDE)
    box("Siege tower side", x + SW / 2 - 0.25, x + SW / 2, 0.9, h, z - SW / 2, z + SW / 2, M_HIDE)
    box("Siege tower front", x - SW / 2, x + SW / 2, 0.9, FL[2], z + SW / 2 - 0.25, z + SW / 2, M_HIDE)
    for y in FL:
        box("Siege tower floor", x - SW / 2, x + SW / 2, y - 0.15, y, z - SW / 2, z + SW / 2 - 0.25, M_WOOD2)
    for y0, y1, xs in ((FL[0], FL[1], x - 1.2), (FL[1], FL[2], x + 1.2)):
        for s in (-0.4, 0.4):
            bar("Siege ladder rail", (xs + s, y0, z + 0.9), (xs + s, y1 + 0.6, z + 1.3), 0.08, M_WOOD)
    for s in (-1, 1):
        for zz in (z - SW / 2 + 0.6, z + SW / 2 - 0.6):
            inst(ME_CYL, "Siege wheel", M_WOOD, U(x + s * (SW / 2 + 0.15), 0.85, zz), (1.7, 1.7, 0.25), (0.0, math.pi / 2, 0.0))
    for s in (-1, 1):
        box("Siege tower merlon", x + s * SW / 2 - (0.25 if s > 0 else 0), x + s * SW / 2 + (0 if s > 0 else 0.25), h, h + 0.7, z - SW / 2, z, M_HIDE)
    ang = math.radians(bridge)
    hinge = (FL[2], z + SW / 2)
    tip = (hinge[0] + 2.4 * math.sin(ang), hinge[1] + 2.4 * math.cos(ang))
    for s in (-0.8, 0.8):
        bar("Drop-bridge", (x + s, hinge[0], hinge[1]), (x + s, tip[0], tip[1]), 0.18, M_WOOD2)
    for kk in range(7):
        f = (kk + 0.5) / 7
        p = (hinge[0] + (tip[0] - hinge[0]) * f, hinge[1] + (tip[1] - hinge[1]) * f)
        bar("Drop-bridge plank", (x - 0.85, p[0], p[1]), (x + 0.85, p[0], p[1]), 0.16, M_WOOD2)
    for s in (-1.2, 0.0, 1.2):
        player(x + s, 0.0, z - SW / 2 - 0.8, 0)                  # the crew pushing
    box("Siege rut", x - 1.8, x - 1.4, -0.02, 0.03, L["SIEGE_Z0"] - 2, L["SIEGE_Z1"][k] + 2, M_WOOD)
    box("Siege rut", x + 1.4, x + 1.8, -0.02, 0.03, L["SIEGE_Z0"] - 2, L["SIEGE_Z1"][k] + 2, M_WOOD)

# ---------------------------------------------------------------- ④ the great tower and the rooks' drill square
BY = L["BOARD_Y"]
box("Great tower", GT[0], GT[1], L["DITCH_Y"], BY, GT[2], GT[3], M_STONE2)
for c in range(L["BOARD_N"]):
    for r in range(L["BOARD_N"]):
        x0, z0 = L["BOARD_X0"] + c * L["SQUARE"], L["BOARD_Z0"] + r * L["SQUARE"]
        box("Board square", x0, x0 + L["SQUARE"], BY - 0.03, BY + 0.02, z0, z0 + L["SQUARE"], M_SQL if (c + r) % 2 == 0 else M_SQD)
ent, ext_ = L["BOARD_ENTRY_Z"], L["BOARD_EXIT_Z"]
wall("Great tower parapet S", [(GT[0], GT[2]), (GT[1], GT[2])], BY - 0.2, BY, body=False)
wall("Great tower parapet N", [(GT[1], GT[3]), (GT[0], GT[3])], BY - 0.2, BY, body=False)
wall("Great tower parapet E", [(GT[1], GT[2]), (GT[1], ent - 1.3)], BY - 0.2, BY, body=False)
wall("Great tower parapet E", [(GT[1], ent + 1.3), (GT[1], GT[3])], BY - 0.2, BY, body=False)
wall("Great tower parapet W", [(GT[0], GT[3]), (GT[0], ext_ + 1.3)], BY - 0.2, BY, body=False)
wall("Great tower parapet W", [(GT[0], ext_ - 1.3), (GT[0], GT[2])], BY - 0.2, BY, body=False)
for xs, zs, sgn in ((GT[1], ent, 1), (GT[0], ext_, -1)):
    for k in range(4):
        box("Board steps", xs + sgn * (0.6 * k), xs + sgn * (0.6 * (k + 1)), L["W2_Y"], BY - 0.5 * k, zs - 1.2, zs + 1.2, M_WALK)
for xb in (-6.0, 6.0):
    banner_z("Great tower banner", GT[2] - 1, BY - 6.5, BY - 1.0, xb - 1.3, xb + 1.3, GT[2])
for n_ in range(len(L["DRILL_ROOKS"])):
    x, z, glow = L["drill_rook"](n_, T)
    piece(ME_ROOKP, "Drill rook", M_IVORY, x, BY, z, 3.0)
    kind, idx, _ = L["DRILL_ROOKS"][n_]
    if glow:
        a = L["square_mid"](idx, 0) if kind == "file" else L["square_mid"](0, idx)
        b = L["square_mid"](idx, L["BOARD_N"] - 1) if kind == "file" else L["square_mid"](L["BOARD_N"] - 1, idx)
        if kind == "file":
            box("Rook line", a[0] - 1.1, a[0] + 1.1, BY + 0.02, BY + 0.05, L["BOARD_Z0"], L["BOARD_Z0"] + L["BOARD_N"] * L["SQUARE"], M_GLOW)
        else:
            box("Rook line", L["BOARD_X0"], -L["BOARD_X0"], BY + 0.02, BY + 0.05, a[1] - 1.1, a[1] + 1.1, M_GLOW)

# ---------------------------------------------------------------- ⑤ the cranes: the giant rook and the knight
w2i = L["wall2_inner_z"]
BEAM_Y = 25.5
for cx in (L["ROOK_CRANE_X"], L["KNIGHT_CRANE_X"]):
    zs_, zn_ = w2i(cx) - 1.6, w3f(cx) + 5.0
    for s in (-2.1, 2.1):
        bar("Gantry post", (cx + s, L["W2_Y"], zs_), (cx + s * 0.6, BEAM_Y, zs_), 0.45, M_WOOD)
        bar("Gantry post", (cx + s, L["W3_Y"], zn_), (cx + s * 0.6, BEAM_Y, zn_), 0.45, M_WOOD)
    for s in (-0.75, 0.75):
        box("Gantry beam", cx + s - 0.25, cx + s + 0.25, BEAM_Y, BEAM_Y + 0.7, zs_ - 0.5, zn_ + 0.5, M_WOOD)
    box("Gantry beam gold", cx - 1.05, cx + 1.05, BEAM_Y + 0.7, BEAM_Y + 0.8, zs_ - 0.6, zs_ + 0.2, M_GOLD)
    box("Gantry beam gold", cx - 1.05, cx + 1.05, BEAM_Y + 0.7, BEAM_Y + 0.8, zn_ - 0.2, zn_ + 0.6, M_GOLD)
# the treadwheels on the inner wall, walked by the castle's workers
for tx in (-38.0, -19.5):
    tz = w3f(tx) + 4.0
    torus("Treadwheel", tx, L["W3_Y"] + 2.6, tz, 2.4, 0.16, M_WOOD, (0.0, math.pi / 2, 0.0))
    torus("Treadwheel", tx + 1.1, L["W3_Y"] + 2.6, tz, 2.4, 0.16, M_WOOD, (0.0, math.pi / 2, 0.0))
    for j in range(8):
        a = 2 * math.pi * j / 8
        bar("Treadwheel spoke", (tx + 0.55, L["W3_Y"] + 2.6, tz), (tx + 0.55, L["W3_Y"] + 2.6 + 2.4 * math.sin(a), tz + 2.4 * math.cos(a)), 0.1, M_WOOD)
    player(tx + 0.55, L["W3_Y"] + 0.3, tz, 0)
# the giant rook: its crown a floor 5 m across, its body hanging under it
cy_, cz_ = L["rook_crane"](T)
RB = 4.5
lathe("Giant rook", [(0.0, 0.0), (2.1, 0.0), (2.2, 0.35), (1.8, 0.7), (1.55, 2.6), (1.9, 3.4), (2.5, 3.9), (2.5, RB), (0.0, RB)],
      (L["ROOK_CRANE_X"], cy_ - RB, cz_), M_IVORY, 48)
torus("Giant rook band", L["ROOK_CRANE_X"], cy_ - 0.6, cz_, 2.5, 0.08, M_GOLD)
for j in range(6):
    a = 2 * math.pi * (j + 0.5) / 6
    inst(ME_CUBE, "Giant rook merlon", M_IVORY, U(L["ROOK_CRANE_X"] + 2.15 * math.cos(a), cy_ + 0.3, cz_ + 2.15 * math.sin(a)), (1.4, 0.6, 0.6),
         (0.0, 0.0, a + math.pi / 2))
for s in ((-1.6, -1.6), (1.6, -1.6), (-1.6, 1.6), (1.6, 1.6)):
    bar("Rook chain", (L["ROOK_CRANE_X"] + s[0], cy_ + 0.6, cz_ + s[1]), (L["ROOK_CRANE_X"] + s[0] * 0.3, BEAM_Y, cz_ + s[1] * 0.3), 0.08, M_IRON)
box("Rook trolley", L["ROOK_CRANE_X"] - 1.4, L["ROOK_CRANE_X"] + 1.4, BEAM_Y - 0.5, BEAM_Y, cz_ - 1.0, cz_ + 1.0, M_IRON)
# the knight, mid-leap
ky, kz = L["knight_crane"](T)
KH = L["KNIGHT_H"]
piece(ME_KNIGHTP, "Giant knight", M_KNIGHTW, L["KNIGHT_CRANE_X"], ky, kz, KH, 180.0)
for s in (-0.5, 0.5):
    bar("Knight chain", (L["KNIGHT_CRANE_X"] + s, ky + KH * 0.92, kz), (L["KNIGHT_CRANE_X"] + s * 0.4, BEAM_Y, kz), 0.08, M_IRON)
box("Knight trolley", L["KNIGHT_CRANE_X"] - 1.2, L["KNIGHT_CRANE_X"] + 1.2, BEAM_Y - 0.5, BEAM_Y, kz - 0.9, kz + 0.9, M_IRON)
# where they set down on the inner wall: keep clear
cyl("Landing circle", L["KNIGHT_CRANE_X"], L["W3_Y"] + 0.01, L["W3_Y"] + 0.04, L["KN_HIGH_Z"], 2 * L["KNIGHT_R"] + 0.8, M_CIRCLE)
cyl("Landing circle", L["ROOK_CRANE_X"], L["W2_Y"] + 0.01, L["W2_Y"] + 0.04, w2i(L["ROOK_CRANE_X"]) - 1.2, 1.6, M_CIRCLE)

# ---------------------------------------------------------------- ⑥ the inner gatehouse and the king's banner
GY = L["GATE_Y"]
box("Inner gatehouse", GH[0], GH[1], L["DITCH_Y"], GY, GH[2], GH[3], M_STONE2)
BZ = L["BANNER_Z"]
wall("Gatehouse parapet S", [(GH[0], GH[2]), (GH[1], GH[2])], GY - 0.2, GY, body=False)
wall("Gatehouse parapet E", [(GH[1], GH[2]), (GH[1], GH[3])], GY - 0.2, GY, body=False)
wall("Gatehouse parapet N", [(GH[1], GH[3]), (2.3, GH[3])], GY - 0.2, GY, body=False)
wall("Gatehouse parapet N", [(-2.3, GH[3]), (GH[0], GH[3])], GY - 0.2, GY, body=False)
wall("Gatehouse parapet W", [(GH[0], GH[3]), (GH[0], BZ[1] + 0.3)], GY - 0.2, GY, body=False)
wall("Gatehouse parapet W", [(GH[0], BZ[0] - 0.3), (GH[0], GH[2])], GY - 0.2, GY, body=False)
for x, z, r, top in L["GATE_TOWERS"]:
    tower(x, z, r, top, L["DITCH_Y"], roof=False)
box("Inner gate", -2.6, 2.6, L["BAILEY_Y"], L["BAILEY_Y"] + 6.0, GH[2] - 0.3, GH[2] + 0.1, M_HOLE)
box("Raised drawbridge", -2.8, 2.8, L["BAILEY_Y"] - 0.2, L["BAILEY_Y"] + 6.6, GH[2] - 1.0, GH[2] - 0.6, M_WOOD)
box("Banner ledge", GH[0] - 0.6, GH[0], L["LEDGE_Y"] - 0.2, L["LEDGE_Y"], GH[2] + 0.5, GH[3] - 0.5, M_STONE2)
ba = L["banner_angle"](T)
for y0, y1 in ((L["W3_Y"], L["LEDGE_Y"] - 0.05), (L["LEDGE_Y"] + 0.05, GY)):
    hgt = y1 - y0
    o = inst(ME_CUBE, "King's banner", M_BANNERC, U(GH[0] - 0.08, y1, (BZ[0] + BZ[1]) / 2), (0.06, BZ[1] - BZ[0], hgt))
    o.data = o.data.copy()
    o.data.transform(Matrix.Translation(Vector((0.0, 0.0, -0.5))))
    o.rotation_euler = (0.0, math.radians(ba), 0.0)
    ball("King's banner crown", GH[0] - 0.16 - hgt / 2 * math.sin(math.radians(ba)), y1 - hgt / 2 * math.cos(math.radians(ba)) + 0.2, (BZ[0] + BZ[1]) / 2, 1.3, M_GOLD)
    box("King's banner rod", GH[0] - 0.3, GH[0], y1 - 0.12, y1 + 0.05, BZ[0] - 0.2, BZ[1] + 0.2, M_GOLD)
for px in (-8.0, 0.0, 8.0):
    cyl("Pennant pole", px, GY, GY + 4.0, GH[3] - 1.0, 0.12, M_GOLD, ME_CYL8)
    o = inst(ME_CONE, "Pennant", M_GOLD, U(px + 1.2, GY + 3.5, GH[3] - 1.0), (0.5, 0.15, 2.4), (0.0, math.pi / 2, 0.0))
for xb in (-4.0, 4.0):
    banner_z("Gatehouse banner", GH[2] - 1, GY - 7.0, GY - 1.5, xb - 1.0, xb + 1.0, GH[2])

# ---------------------------------------------------------------- the keep bridge and the keep
BR = L["BRIDGE"]
box("Keep bridge", BR[0], BR[1], GY - 0.6, GY, BR[2], BR[3], M_WALK)
for xr in (BR[0], BR[1]):
    box("Keep bridge rail", xr - 0.12, xr + 0.12, GY + 0.95, GY + 1.1, BR[2], BR[3], M_GOLD)
    for z in [BR[2] + 1.0 + 1.0 * k for k in range(int(BR[3] - BR[2] - 1))]:
        cyl("Keep bridge baluster", xr, GY, GY + 0.95, z, 0.18, M_BANNERC, ME_CYL8)
for zp in (BR[2] + 4.5, BR[2] + 10.5):
    box("Keep bridge pier", BR[0] + 0.4, BR[1] - 0.4, L["BAILEY_Y"], GY - 0.6, zp, zp + 2.0, M_STONE2)
EZ = L["EXIT"][2]
box("Keep", -16.0, 16.0, L["BAILEY_Y"], 34.0, EZ, EZ + 3.0, M_STONE2)
box("Keep door", -2.2, 2.2, GY, GY + 5.0, EZ - 0.05, EZ + 0.1, M_SKYDOOR)
box("Keep door arch", -2.6, 2.6, GY + 5.0, GY + 5.6, EZ - 0.2, EZ, M_GOLD)
for s in (-1, 1):
    cyl("Keep tower", s * 13.0, L["BAILEY_Y"], 38.0, EZ + 1.5, 7.0, M_STONE2)
    inst(ME_CONE, "Keep tower roof", M_ROOF, U(s * 13.0, 38.0 + 3.6, EZ + 1.5), (7.4, 7.4, 7.2))
    banner_z("Keep banner", EZ - 1, 22.0, 31.0, s * 7.0 - 1.4, s * 7.0 + 1.4, EZ)

# ---------------------------------------------------------------- banners on the walls (the concept picture's crowns)
for x in (-36.0, -20.0, 30.0, 38.0):
    zf_ = w2f(x)
    banner_z("Wall banner", zf_ - 1, L["W2_Y"] - 6.0, L["W2_Y"] - 0.8, x - 1.1, x + 1.1, zf_)
for x in (-38.0, -14.0, 16.0, 32.0):
    zf_ = w3f(x)
    banner_z("Wall banner", zf_ - 1, L["W3_Y"] - 6.0, L["W3_Y"] - 0.8, x - 1.1, x + 1.1, zf_)
for x in (-30.0, 30.0):
    banner_z("Wall banner", 10, 0.3, 3.0, x - 1.0, x + 1.0, L["on_line"](L["WALL1"][0], L["WALL1"][1], x) if x < 0 else L["on_line"](L["WALL1"][2], L["WALL1"][3], x))

# ---------------------------------------------------------------- the storming party (pawns where the way has them at T)
lx = L["LADDERS"]
zf = 11.0 - LL * math.cos(math.radians(L["LEAN"])) + 0.1
for i, along in ((0, 2.6), (5, 4.2), (2, 1.2)):
    if L["ladder_climbable"](i, T):
        a = math.radians(L["ladder_angle"](i, T))
        player(lx[i], along * math.sin(a), zf + along * math.cos(a) - 0.45, i % 2)
for x, y, z, team in ((-9.0, 0.0, 6.5, 0), (-15.0, 0.0, 6.0, 1), (-8.0, L["W1_Y"], 15.0, 1), (-1.6, L["W1_Y"], 14.4, 0),
                      (G0 + 3.0, L["W1_Y"], 13.0, 1), (G0 + 7.0, L["W1_Y"], 17.0, 0),
                      (sx[1], L["W1_Y"], L["siege"](1, T)[0] - 0.6, 1), (sx[1] - 0.8, L["W1_Y"], L["SIEGE_Z0"] - 2.8, 0),
                      (sx[0], FL[2], L["siege"](0, T)[0] + 1.0, 0), (sx[0], FL[2] + 0.1, L["siege"](0, T)[0] + SW / 2 + 1.3, 1),
                      (16.0, L["W2_Y"], 42.5, 0), (8.4, BY, ent, 1), (2.4, BY, 47.0, 0), (-7.2, BY, 49.4, 1),
                      (-16.0, L["W2_Y"], 51.5, 0), (L["ROOK_CRANE_X"] - 0.8, cy_, cz_ - 0.4, 1), (L["ROOK_CRANE_X"] + 0.9, cy_, cz_ + 0.6, 0),
                      (L["KNIGHT_CRANE_X"] - 0.9, ky + 1.6, kz - 0.4, 1),
                      (-20.0, L["W3_Y"], w3f(-20.0) + 3.0, 0), (-14.0, L["W3_Y"], w3f(-14.0) + 3.5, 1),
                      (GH[0] - 0.55, L["W3_Y"] + 1.3, (BZ[0] + BZ[1]) / 2 - 0.6, 0),
                      (-4.0, GY, 74.0, 1), (2.0, GY, 79.0, 0), (0.0, GY, 90.0, 1)):
    player(x, y, z, team)


# ---------------------------------------------------------------- light, camera, render
def light(name, kind, loc, energy, color, **kw):
    d = bpy.data.lights.new(name, kind)
    d.energy = energy
    d.color = color
    for k, v in kw.items():
        setattr(d, k, v)
    o = bpy.data.objects.new(name, d)
    col.objects.link(o)
    o.location = loc
    return o


def aim(o, target):
    o.rotation_euler = (Vector(target) - o.location).to_track_quat("-Z", "Y").to_euler()


world = bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.46, 0.62, 0.92, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
sun = light("LGT_Sun", "SUN", U(60, 100, -40), 3.6, (1.0, 0.93, 0.80), angle=math.radians(2.0))
aim(sun, U(0, 0, 50))
cam_d = bpy.data.cameras.new("CAM_Rampart")
cam_d.clip_end = 1200
cam = bpy.data.objects.new("CAM_Rampart", cam_d)
col.objects.link(cam)
scene.camera = cam
scene.render.resolution_x = WIDTH
scene.render.resolution_y = HEIGHT
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "AgX"
for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
    try:
        scene.view_settings.look = look
        break
    except Exception:
        pass
scene.render.engine = "CYCLES"
prefs = bpy.context.preferences.addons["cycles"].preferences
prefs.compute_device_type = "OPTIX"
prefs.get_devices()
for d in prefs.devices:
    d.use = d.type in ("OPTIX",)
scene.cycles.device = "GPU"
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.cycles.max_bounces = 6
scene.view_settings.exposure = -0.1
scene.render.image_settings.file_format = "PNG"
print("OBJECTS", len(col.objects))

shots = (("", "overview", 28, U(76, 66, -38), U(-4, 6, 46)),
         ("_walls", "walls", 24, U(-42, 26, -8), U(6, 6, 32)),
         ("_cranes", "cranes", 30, U(-38, 42, 104), U(-23, 12, 62)))
ONLY = os.environ.get("RAMPART_SHOTS")
if ONLY:
    shots = tuple(s for s in shots if s[1] in ONLY.split(","))
for suffix, key, lens, eye, target in shots:
    cam_d.lens = lens
    cam.location = eye
    aim(cam, target)
    root, ext = os.path.splitext(OUT)
    scene.render.filepath = root + suffix + (ext or ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)
