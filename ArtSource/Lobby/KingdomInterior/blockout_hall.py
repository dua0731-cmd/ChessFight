# Grey clay blockout of the combined kingdom hall (승규 2026-11-11, two reference images):
#   lobby view (camera A, while picking a mode) looks at the GRAND STAIR end (image 1);
#   "게임 시작" -> the camera swings round and the party runs to the GREAT GATE at the
#   opposite end (image 2), behind camera A.
# Run: blender -b --python blockout_hall.py -- [out_dir]
# Blender axes: +Y away from camera A (= Unity +Z), +Z up. Stage and pawns are stand-ins.
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from kit import *          # noqa: F401,F403  (bpy, sc, clay, box, cyl, torus, sphere, arcs, banner, column, ...)
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT_DIR = argv[0] if argv else os.path.join(HERE, "hall")
os.makedirs(OUT_DIR, exist_ok=True)

HALL_W = 19.0
SIDE_X = HALL_W / 2
# R546 (승규 "the reference has a lower camera, the stairs' bottoms joined, and bigger, higher stairs"):
# walls 5.4 -> 7.2 m, gallery 2.5 -> 4.5 m, wider and longer stairs on one joined base.
WALL_H = 7.2          # side walls; the glass vault starts here
STAIR_WALL = 15.5     # far wall of the stair end (+Y)
STAIR_WALL_H = 10.6   # the stair end's wall (rose window, banners); glass above it
GATE_WALL = -25.0     # face of the gate wall (-Y); ~24 m run from the stage at 10 m/s
GALLERY_Z = 4.5
GALLERY_Y = 12.0      # front edge of the gallery
BASE_Y, BASE_H = 6.6, .44   # the joined two-step base under both stairs and the dais (front edge, top)
GATE_W, GATE_LEG, GATE_T = 3.8, 2.8, .5

# The stage as Unity draws it in the hall (LobbyStage.cs, R545: smaller and low, 승규 "the board
# reads big and thick next to the hall"): 0.75 of the courtyard stage, 0.4 m high.
STAGE_SCALE = .75
STAGE_HALF = 10.8 * STAGE_SCALE / 2           # 4.05
STAGE_BASE, STAGE_FRAME = .26, .14
STAGE_TOP = STAGE_BASE + STAGE_FRAME + .005   # 0.405
STEP_D, STEPS_FRONT = .42, 2                  # two low steps in front of it
STEPS_END = STAGE_HALF + STEP_D * STEPS_FRONT + .02

# ---------------- floor and carpets ----------------
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, (STAIR_WALL + GATE_WALL) / 2, 0))
fl = bpy.context.object
fl.name = "Floor"
fl.scale = (HALL_W + 2, STAIR_WALL - GATE_WALL + 2, 1)
put(fl, "Floor")
box("Runner To Stairs", "Carpets", 0, (STAGE_HALF + BASE_Y) / 2, 0, 3.0, BASE_Y - STAGE_HALF, .03)
box("Runner To Gate", "Carpets", 0, (GATE_WALL + 1.2 - STEPS_END) / 2, 0, 3.0, -STEPS_END - (GATE_WALL + 1.2), .03)

# ---------------- stage stand-in (Unity draws it) ----------------
S = STAGE_SCALE
box("Stage", "Stand-in Stage", 0, 0, 0, 10.8 * S, 10.8 * S, STAGE_BASE)
box("Frame", "Stand-in Stage", 0, 0, STAGE_BASE, 10.4 * S, 10.4 * S, STAGE_FRAME)
box("Board", "Stand-in Stage", 0, 0, STAGE_TOP - .01, 9.6 * S, 9.6 * S, .01)
PAWNS = [(0, -.5, 1.3), (-2.4, -.5, 1.15), (2.4, -.5, 1.15), (-1.2, 1.5, 1.15), (1.2, 1.5, 1.15), (-3.6, 1.5, 1.15)]
for i, (px, py, s) in enumerate(PAWNS):
    px, py, s, z = px * S, py * S, s * S, STAGE_TOP
    cyl(f"Pawn {i} Foot", "Stand-in Pawns", px, py, z, .36 * s, .14 * s)
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=.28 * s, radius2=.13 * s, depth=.75 * s, location=(px, py, z + .14 * s + .375 * s))
    put(bpy.context.object, "Stand-in Pawns").name = f"Pawn {i} Body"
    cyl(f"Pawn {i} Collar", "Stand-in Pawns", px, py, z + .86 * s, .22 * s, .07 * s)
    sphere(f"Pawn {i} Head", "Stand-in Pawns", px, py, z + 1.1 * s, .23 * s)
# low carpet steps on the gate side of the stage (the way the party runs)
for i in range(STEPS_FRONT):
    box(f"Front Step {i}", "Front Steps", 0, -STAGE_HALF - STEP_D / 2 - i * STEP_D, 0, 3.4, STEP_D,
        STAGE_TOP * (STEPS_FRONT - i) / (STEPS_FRONT + 1))

# ================= STAIR END (image 1) =================
G = "Stair End"
box("Stair Wall", G, 0, STAIR_WALL + .3, 0, HALL_W, .6, STAIR_WALL_H)
# gallery across the back and its under-wall with the curtained alcove
UNDER_H = GALLERY_Z - .4
box("Gallery Floor", G, 0, (GALLERY_Y + STAIR_WALL) / 2, UNDER_H, HALL_W, STAIR_WALL - GALLERY_Y, .4)
box("Gallery Lip", G, 0, GALLERY_Y + .05, UNDER_H - .2, HALL_W, .3, .25)
AL = 2.3   # half width of the alcove opening
box("Under Wall L", G, -(AL + (SIDE_X - AL) / 2), GALLERY_Y + .3, 0, SIDE_X - AL, .4, UNDER_H)
box("Under Wall R", G, (AL + (SIDE_X - AL) / 2), GALLERY_Y + .3, 0, SIDE_X - AL, .4, UNDER_H)
box("Alcove Back", G, 0, GALLERY_Y + 1.6, 0, AL * 2, .3, UNDER_H)
for s in (-1, 1):
    box(f"Alcove Side {s}", G, s * AL, GALLERY_Y + .95, 0, .3, 1.6, UNDER_H)
    box(f"Curtain {s}", G, s * (AL - .55), GALLERY_Y + .5, BASE_H, .9, .2, UNDER_H - BASE_H - .3)
    cyl(f"Curtain Tassel {s}", G, s * (AL - .12), GALLERY_Y + .25, UNDER_H * .42, .11, .65)
box("Curtain Valance", G, 0, GALLERY_Y + .45, UNDER_H - .7, AL * 2, .25, .6)
o = box("Alcove Diamond", G, 0, GALLERY_Y - .05, UNDER_H - .8, .6, .2, .6)
o.rotation_euler = (0, math.pi / 4, 0)
# the joined base (the reference's continuous bottom): two broad low steps under both stair feet and the dais
BASE_BACK = GALLERY_Y + .1
box("Stair Base Low", G, 0, (BASE_Y + BASE_BACK) / 2, 0, 14.0, BASE_BACK - BASE_Y, BASE_H / 2)
box("Stair Base High", G, 0, (BASE_Y + .45 + BASE_BACK) / 2, BASE_H / 2, 13.0, BASE_BACK - BASE_Y - .45, BASE_H / 2)
# two-step dais in front of the alcove, on the base, between the stair feet
box("Platform Low", G, 0, (9.0 + BASE_BACK) / 2, BASE_H, 2 * AL + 1.4, BASE_BACK - 9.0, .22)
box("Platform High", G, 0, (9.6 + BASE_BACK) / 2, BASE_H + .22, 2 * AL + .4, BASE_BACK - 9.6, .22)
# arched niches with lanterns under the side galleries, outside the stairs
for s in (-1, 1):
    arch_frame(f"Niche {s}", G, s * 8.4, GALLERY_Y + .05, 0, 1.5, 1.7, .25, .3)
    arch_slab(f"Niche Back {s}", G, s * 8.4, GALLERY_Y + .45, 0, 1.5, 1.7, .05)
    lantern(f"Niche Lantern {s}", G, s * 8.4, GALLERY_Y + .1, 1.9)
# gallery balustrade over the alcove and between the stair tops, and beyond the stairs at the sides
def balustrade(name, x0, x1, y, z, step=.4):
    n = max(2, int(abs(x1 - x0) / step))
    for k in range(n + 1):
        x = x0 + (x1 - x0) * k / n
        cyl(f"{name} Post {k}", "Balusters", x, y, z, .08, .9, verts=10)
    box(f"{name} Rail", "Balusters", (x0 + x1) / 2, y, z + .9, abs(x1 - x0) + .2, .24, .12)
balustrade("Gallery Rail C", -4.0, 4.0, GALLERY_Y + .1, GALLERY_Z)
for s in (-1, 1):
    balustrade(f"Gallery Rail Side {s}", s * 7.1, s * 9.3, GALLERY_Y + .1, GALLERY_Z)

# big curved stairs, mirrored: their feet on the joined base by the dais, climbing back and outwards
# onto the gallery
def bez(p0, p1, p2, t):
    a = (1 - t) ** 2
    b = 2 * (1 - t) * t
    c = t * t
    return Vector((a * p0[0] + b * p1[0] + c * p2[0], a * p0[1] + b * p1[1] + c * p2[1]))
STEPS, STAIR_W = 26, 2.8
for s in (-1, 1):
    P0, P1, P2 = (s * 3.75, BASE_Y + 1.0), (s * 4.0, 11.6), (s * 7.2, 13.4)
    rails = {-1: [], 1: []}
    for k in range(STEPS):
        t0, t1 = k / STEPS, (k + 1) / STEPS
        a, b = bez(P0, P1, P2, t0), bez(P0, P1, P2, t1)
        mid, tan = (a + b) / 2, (b - a)
        rz = math.atan2(-tan.x, tan.y)
        h = BASE_H + (GALLERY_Z - BASE_H) * (k + 1) / STEPS
        box(f"Stair {s} Step {k}", "Stairs", mid.x, mid.y, 0, STAIR_W, tan.length * 1.08, h, rz=rz)
        side = Vector((math.cos(rz), math.sin(rz)))
        for e in (-1, 1):
            p = mid + side * e * (STAIR_W / 2 - .1)
            rails[e].append((p.x, p.y, h + .95))
            if k % 2 == 0:
                cyl(f"Stair {s} Baluster {e} {k}", "Balusters", p.x, p.y, h, .08, .95, verts=10)
    for e in (-1, 1):
        cu = bpy.data.curves.new(f"Stair {s} Rail {e}", "CURVE")
        cu.dimensions = "3D"
        cu.bevel_depth = .1
        sp = cu.splines.new("POLY")
        sp.points.add(len(rails[e]) - 1)
        for k, p in enumerate(rails[e]):
            sp.points[k].co = (*p, 1)
        ob = bpy.data.objects.new(cu.name, cu)
        sc.collection.objects.link(ob)
        put(ob, "Balusters")
    # crown pedestal beside the stair foot, on the base
    PX, PY = s * 5.9, BASE_Y + 1.25
    box(f"Stair Pedestal {s}", G, PX, PY, BASE_H, 1.5, 1.5, .3)
    box(f"Stair Pedestal Body {s}", G, PX, PY, BASE_H + .3, 1.25, 1.25, 1.5)
    box(f"Stair Pedestal Top {s}", G, PX, PY, BASE_H + 1.8, 1.45, 1.45, .25)
    crown(f"Stair Pedestal Crown {s}", G, PX, PY, BASE_H + 2.05, .55)

# knight rose window above the gallery, pilasters, banners
ROSE_Z, ROSE_R = GALLERY_Z + 3.0, 1.9
torus("Rose Window Ring", G, 0, STAIR_WALL - .05, ROSE_Z, ROSE_R, .25).rotation_euler = (math.pi / 2, 0, 0)
cyl("Rose Window Glass", G, 0, STAIR_WALL + .05, ROSE_Z, ROSE_R - .1, .05).rotation_euler = (math.pi / 2, 0, 0)
box("Rose Knight Placeholder", G, 0, STAIR_WALL - .15, ROSE_Z - 1.25, 1.0, .2, 2.2)
for a in range(4):
    ang = a * math.pi / 2
    o = box(f"Rose Diamond {a}", G, ROSE_R * math.cos(ang), STAIR_WALL - .2, ROSE_Z + ROSE_R * math.sin(ang) - .25, .5, .15, .5)
    o.rotation_euler = (0, math.pi / 4, 0)
for s in (-1, 1):
    for i, px in enumerate((2.9, 7.4)):
        box(f"Stair Wall Pilaster {s} {i}", G, s * px, STAIR_WALL - .2, GALLERY_Z, .9, .4, STAIR_WALL_H - GALLERY_Z)
    banner(f"Stair Banner Red {s}", "Banners", s * 4.3, STAIR_WALL - .1, GALLERY_Z + 4.3, 1.15, 3.1)
    banner(f"Stair Banner Black {s}", "Banners", s * 6.0, STAIR_WALL - .1, GALLERY_Z + 4.3, 1.0, 2.6)
# crown chandelier over the stairs
CH_Y, CH_Z = 9.0, 7.8
torus("Chandelier Ring", G, 0, CH_Y, CH_Z, 2.0, .22)
for k in range(8):
    ang = 2 * math.pi * k / 8
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=.34, radius2=0, depth=.9,
                                    location=(2.0 * math.cos(ang), CH_Y + 2.0 * math.sin(ang), CH_Z + .55))
    put(bpy.context.object, G).name = f"Chandelier Point {k}"
for k in range(4):
    ang = 2 * math.pi * k / 4 + math.pi / 4
    line_curve(f"Chandelier Chain {k}", G, (1.8 * math.cos(ang), CH_Y + 1.8 * math.sin(ang), CH_Z + .1), (0, CH_Y, WALL_H + 8.0), .035)

# ================= GATE END (image 2) =================
# Faces +Y (towards the stage); "front" offsets go +Y from the wall.
W = GATE_WALL
F = lambda off: W + off
GT = "Gate End"

def wall_with_arch(name, group, y, wall_w, wall_h, open_w, leg, depth):
    """Wall across the hall (xz plane) with a round-top opening cut out of it: a strip between
    the opening's outline and the wall's outline, each opening point pushed out to the edge."""
    inner = arch_contour(open_w, leg, n=32)
    hx = wall_w / 2
    outer = []
    for u, v in inner:
        if v <= leg + 1e-6:                      # the legs: straight out sideways
            outer.append((math.copysign(hx, u), v))
        else:                                    # the round top: out along the ray from its centre
            du, dv = u, v - leg
            t = min(hx / abs(du) if abs(du) > 1e-6 else 1e9, (wall_h - leg) / dv)
            outer.append((du * t, leg + dv * t))
    # the strip misses the two top corners of the wall: add them as their own quads
    verts = [(u, y, v) for u, v in inner] + [(u, y, v) for u, v in outer]
    n = len(inner)
    faces = [(i, i + 1, n + i + 1, n + i) for i in range(n - 1)]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    o = bpy.data.objects.new(name, me)
    sc.collection.objects.link(o)
    sol = o.modifiers.new("Solid", "SOLIDIFY")
    sol.thickness = depth
    sol.offset = 0
    put(o, group)
    for s in (-1, 1):   # top corners
        box(f"{name} Corner {s}", group, s * (hx - 1.0), y, wall_h - 1.0, 2.0, depth, 1.0)
    return o

wall_with_arch("Gate Wall", GT, W - .3, HALL_W, WALL_H + .4, GATE_W, GATE_LEG, .6)
# the short passage beyond the gate: side walls and a ceiling, so the open gate shows a lit room, not the sky
PASSAGE_H = GATE_LEG + GATE_W / 2 + .7
for s in (-1, 1):
    box(f"Gate Passage Side {s}", GT, s * (GATE_W / 2 + .3), W - 3.1, 0, .3, 5.6, PASSAGE_H)
box("Gate Passage Ceiling", GT, 0, W - 3.1, PASSAGE_H, GATE_W + .9, 5.6, .3)
box("Gate Wall Plinth", GT, 0, F(.05), 0, HALL_W, .3, .6)
box("Gate Wall Cornice", GT, 0, F(.05), WALL_H - .3, HALL_W, .4, .3)
GY = F(.25)
arch_frame("Gate Arch", GT, 0, F(.45), 0, GATE_W, GATE_LEG, GATE_T, .7)
for s in (-1, 1):
    box(f"Gate Pilaster {s}", GT, s * (GATE_W / 2 + GATE_T + .3), F(.5), 0, .6, .8, GATE_LEG + GATE_W / 2 + GATE_T)
box("Gate Lintel", GT, 0, F(.5), GATE_LEG + GATE_W / 2 + GATE_T, GATE_W + 2 * GATE_T + 1.2, .8, .35)
leaves = []
for s, side in ((-1, "L"), (1, "R")):
    hinge_x = s * GATE_W / 2
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(hinge_x, GY, 0))
    pivot = bpy.context.object
    pivot.name = f"GateLeaf_{side}"
    put(pivot, GT)
    leaf = arch_slab(f"GateLeaf_{side} Door", GT, 0, GY, 0, GATE_W - .04, GATE_LEG, .16, half=s)
    shield = box(f"GateLeaf_{side} Shield", GT, s * GATE_W / 4, GY + .1, .8, .95, .06, 1.45)
    for o in (leaf, shield):
        o.parent = pivot
        o.matrix_parent_inverse = pivot.matrix_world.inverted()
    leaves.append((pivot, s))
# the light beyond the gate (a short bright passage, Unity glows it)
box("Gate Passage Floor", GT, 0, W - 3.0, 0, GATE_W + .4, 5.4, .05)
box("Gate Passage End", GT, 0, W - 5.8, 0, GATE_W + .4, .3, PASSAGE_H + .2)
box("Gate Step", GT, 0, F(.9), 0, GATE_W + 2.6, 1.0, .14)
CREST_Z = GATE_LEG + GATE_W / 2 + GATE_T + .2
box("Gate Crest", GT, 0, F(.7), CREST_Z, 1.1, .2, 1.2)
crown("Gate Crest Crown", GT, 0, F(.85), CREST_Z + .45, .26)
for s in (-1, 1):
    GPX = s * (GATE_W / 2 + 1.9)
    box(f"Gate Pedestal {s}", GT, GPX, F(1.2), 0, 1.1, 1.1, .3)
    box(f"Gate Pedestal Body {s}", GT, GPX, F(1.2), .3, .9, .9, 1.25)
    box(f"Gate Pedestal Top {s}", GT, GPX, F(1.2), 1.55, 1.05, 1.05, .2)
    crown(f"Gate Pedestal Crown {s}", GT, GPX, F(1.2), 1.75, .38)
    for i, px in enumerate((5.6, 8.7)):
        box(f"Gate Wall Pilaster {s} {i}", GT, s * px, F(.2), 0, .9, .5, WALL_H - .35)
    box(f"Gate Lantern Bracket {s}", GT, s * 7.15, F(.25), 3.45, .08, .5, .08)
    box(f"Gate Lantern {s}", GT, s * 7.15, F(.55), 2.9, .32, .32, .55)
    banner(f"Gate Banner {s}", "Banners", s * 4.6, F(.1), 6.3, .95, 2.7)

# ================= SIDE WALLS, COLUMNS, GLASS VAULT =================
LEN_Y = STAIR_WALL - GATE_WALL
MID_Y = (STAIR_WALL + GATE_WALL) / 2
for s in (-1, 1):
    box(f"Side Wall {s}", "Side Walls", s * (SIDE_X + .3), MID_Y, 0, .6, LEN_Y, WALL_H)
    box(f"Side Plinth {s}", "Side Walls", s * (SIDE_X - .1), MID_Y, 0, .3, LEN_Y, .6)
    box(f"Side Cornice {s}", "Side Walls", s * (SIDE_X - .1), MID_Y, WALL_H - .35, .4, LEN_Y, .35)
    for j, wy in enumerate((5.6, -1.0, -7.6, -14.2, -20.8)):
        arch_frame(f"Window {s} {j}", "Side Windows", s * (SIDE_X - .1), wy, 1.2, 2.0, 2.9, .3, .4, plane="yz")
        arch_slab(f"Window Glass {s} {j}", "Side Windows", s * (SIDE_X + .02), wy, 1.2, 2.0, 2.9, .04, plane="yz")
        box(f"Window Sill {s} {j}", "Side Windows", s * (SIDE_X - .2), wy, 1.05, .5, 2.8, .15)
        banner(f"Side Banner {s} {j}", "Banners", s * (SIDE_X - .15), wy - 3.3, 6.5, 1.0, 2.8, facing="x")
    for j, cy in enumerate((9.8, 2.3, -4.3, -10.9, -17.5, -23.4)):
        column(f"Column {s} {j}", "Columns", s * (SIDE_X - 1.15), cy, .5, WALL_H)
VR = SIDE_X
y = GATE_WALL
k = 0
while y <= STAIR_WALL + .01:
    arc_curve(f"Vault Rib {k}", "Glass Vault", (0, y, WALL_H), VR, 0, math.pi, "xz", .16, steps=40)
    y += LEN_Y / 9
    k += 1
for s in (-1, 1):
    for h in (1.2, 2.6, 4.8):
        a = math.asin(min(1, h / VR))
        x = s * VR * math.cos(a)
        line_curve(f"Vault Purlin {s} {h}", "Glass Vault", (x, GATE_WALL, WALL_H + h), (x, STAIR_WALL, WALL_H + h), .06)
line_curve("Vault Ridge", "Glass Vault", (0, GATE_WALL, WALL_H + VR), (0, STAIR_WALL, WALL_H + VR), .1)
for end, wy in (("Stair", STAIR_WALL - .1), ("Gate", GATE_WALL + .1)):
    base = STAIR_WALL_H if end == "Stair" else WALL_H + .4
    for k in range(1, 8):
        a = math.pi * k / 8
        line_curve(f"Lunette {end} Spoke {k}", "Glass Vault", (0, wy, base), (VR * math.cos(a), wy, WALL_H + VR * math.sin(a)), .07)

# ================= light, world, cameras =================
sun_d = bpy.data.lights.new("Sun", "SUN")
sun_d.energy = 2.6
sun_d.angle = math.radians(6)
sun = bpy.data.objects.new("Sun", sun_d)
sc.collection.objects.link(sun)
sun.rotation_euler = (math.radians(38), 0, math.radians(20))    # high, through the glass, onto the stair end
fill_d = bpy.data.lights.new("Fill", "SUN")
fill_d.energy = 1.1
fill = bpy.data.objects.new("Fill", fill_d)
sc.collection.objects.link(fill)
fill.rotation_euler = (math.radians(45), 0, math.radians(200))   # weaker, onto the gate end
world = bpy.data.worlds.new("World")
sc.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.75, 0.78, 0.82, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.4
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 1920, 1080
sc.render.resolution_percentage = 50
sc.view_settings.view_transform = "AgX"
sc.view_settings.look = "AgX - Medium High Contrast"

def camera(name, at, look, fov=30.0, ortho=0.0):
    d = bpy.data.cameras.new(name)
    d.sensor_fit = "VERTICAL"
    if ortho:
        d.type = "ORTHO"
        d.ortho_scale = ortho
    else:
        d.angle = math.radians(fov)
    d.clip_end = 300
    c = bpy.data.objects.new(name, d)
    sc.collection.objects.link(c)
    c.location = at
    c.rotation_euler = (Vector(look) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    put(c, "Cameras")
    return c

# The hall's lobby camera (R546, LobbyStage.Hall.cs HallLobbyAt/Look): low, just behind the stage,
# looking nearly level at the stairs, vertical FOV 36.
shots = [
    ("1_lobby_A", camera("Cam 1 Lobby A", (0.4, -12.5, 3.2), (0.4, 10.0, 3.0), fov=36)),
    ("2_swing", camera("Cam 2 Swing", (7.0, -2.5, 4.4), (0.0, -1.0, 1.2), fov=40)),
    ("3_gate_B", camera("Cam 3 Gate B", (0.3, 3.5, 5.0), (0.3, GATE_WALL, 2.8))),
    ("4_follow", camera("Cam 4 Follow", (0.0, -11.0, 3.4), (0.0, GATE_WALL, 2.3), fov=40)),
]
plan = camera("Cam Plan", (0, MID_Y, 60), (0, MID_Y, 0), ortho=LEN_Y + 4)

def render(name, cam):
    sc.camera = cam
    sc.render.filepath = os.path.join(OUT_DIR, name + ".png")
    bpy.ops.render.render(write_still=True)

for name, cam in shots[:3]:
    render(name, cam)
# the run: pawns off the stage and on the carpet, gate open
for o in coll("Stand-in Pawns").objects:
    o.location.y -= 16.5
    o.location.z -= STAGE_TOP
for pivot, s in leaves:
    pivot.rotation_euler = (0, 0, s * math.radians(100))
render(*shots[3])
sc.render.resolution_x, sc.render.resolution_y = 1100, 1920
render("5_plan", plan)
for o in coll("Stand-in Pawns").objects:
    o.location.y += 16.5
    o.location.z += STAGE_TOP
for pivot, s in leaves:
    pivot.rotation_euler = (0, 0, 0)
sc.camera = shots[0][1]
sc.render.resolution_x, sc.render.resolution_y = 1920, 1080
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "KingdomHall_blockout.blend"))
print("DONE", OUT_DIR)
