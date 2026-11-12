# Grey clay blockout of the "kingdom interior" lobby (reference: 승규 2026-11-11 image).
# Run: blender -b --python blockout.py -- [out.png]
# Blender axes: +Y away from the lobby camera (= Unity +Z), +Z up. Stage and pawns are
# stand-ins (Unity draws them). Every new part uses one grey clay material.
import bpy, math, sys, os
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(HERE, "blockout_v1.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

clay = bpy.data.materials.new("Clay")
clay.use_nodes = True
bsdf = clay.node_tree.nodes["Principled BSDF"]
bsdf.inputs["Base Color"].default_value = (0.62, 0.62, 0.62, 1)
bsdf.inputs["Roughness"].default_value = 0.6

col_cache = {}
def coll(name):
    if name not in col_cache:
        c = bpy.data.collections.new(name)
        sc.collection.children.link(c)
        col_cache[name] = c
    return col_cache[name]

def put(obj, group):
    for c in obj.users_collection:
        c.objects.unlink(obj)
    coll(group).objects.link(obj)
    if obj.type in ("MESH", "CURVE"):
        obj.data.materials.clear()
        obj.data.materials.append(clay)
    return obj

def box(name, group, x, y, z, sx, sy, sz, rz=0.0):
    """Box with its BOTTOM centre at (x, y, z) and full size (sx, sy, sz)."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z + sz / 2), rotation=(0, 0, rz))
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(scale=True)
    bev = o.modifiers.new("Bevel", "BEVEL")
    bev.width = min(.04, sx / 4, sy / 4, sz / 4)
    bev.segments = 2
    return put(o, group)

def cyl(name, group, x, y, z, r, h, verts=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=(x, y, z + h / 2))
    o = bpy.context.object
    o.name = name
    return put(o, group)

def torus(name, group, x, y, z, R, r):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=(x, y, z))
    o = bpy.context.object
    o.name = name
    return put(o, group)

def sphere(name, group, x, y, z, r):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=(x, y, z), segments=24, ring_count=12)
    o = bpy.context.object
    o.name = name
    bpy.ops.object.shade_smooth()
    return put(o, group)

def arc_curve(name, group, centre, radius, a0, a1, plane, thick, steps=24):
    """Bevelled curve along an arc. plane 'xz' (facing the camera) or 'yz' (side wall)."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = thick
    cu.bevel_resolution = 2
    sp = cu.splines.new("POLY")
    sp.points.add(steps)
    cx, cy, cz = centre
    for i in range(steps + 1):
        a = a0 + (a1 - a0) * i / steps
        if plane == "xz":
            p = (cx + radius * math.cos(a), cy, cz + radius * math.sin(a))
        else:
            p = (cx, cy + radius * math.cos(a), cz + radius * math.sin(a))
        sp.points[i].co = (*p, 1)
    o = bpy.data.objects.new(name, cu)
    sc.collection.objects.link(o)
    return put(o, group)

def line_curve(name, group, a, b, thick):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = thick
    sp = cu.splines.new("POLY")
    sp.points.add(1)
    sp.points[0].co = (*a, 1)
    sp.points[1].co = (*b, 1)
    o = bpy.data.objects.new(name, cu)
    sc.collection.objects.link(o)
    return put(o, group)

def arch_contour(width, leg_h, n=24):
    """Round-top opening outline (u across, v up), bottom left -> top -> bottom right."""
    hw = width / 2
    pts = [(-hw, 0.0), (-hw, leg_h)]
    for i in range(1, n):
        a = math.pi - math.pi * i / n
        pts.append((hw * math.cos(a), leg_h + hw * math.sin(a)))
    pts += [(hw, leg_h), (hw, 0.0)]
    return pts

def to3d(u, v, x, y, z, plane):
    return (x + u, y, z + v) if plane == "xz" else (x, y + u, z + v)

def arch_frame(name, group, x, y, z, width, leg_h, thick, depth, plane="xz"):
    """One-piece round arch frame around an opening of `width` (legs + semicircle)."""
    inner = arch_contour(width, leg_h)
    outer = arch_contour(width + 2 * thick, leg_h)
    verts = [to3d(u, v, x, y, z, plane) for u, v in inner] + [to3d(u, v, x, y, z, plane) for u, v in outer]
    n = len(inner)
    faces = [(i, i + 1, n + i + 1, n + i) for i in range(n - 1)]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    o = bpy.data.objects.new(name, me)
    sc.collection.objects.link(o)
    sol = o.modifiers.new("Solid", "SOLIDIFY")
    sol.thickness = depth
    sol.offset = 0
    return put(o, group)

def arch_slab(name, group, x, y, z, width, leg_h, depth, plane="xz", half=0):
    """Filled round-top slab (door leaf, glass). half=-1/+1 keeps only the left/right half."""
    pts = arch_contour(width, leg_h)
    if half:
        # this leaf's own side (left u <= 0, right u >= 0); the arc's top point is on both
        pts = [(u, v) for u, v in pts if u * half >= -1e-6]
        pts = pts + [(0.0, 0.0)] if half < 0 else [(0.0, 0.0)] + pts
    verts = [to3d(u, v, x, y, z, plane) for u, v in pts]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], [list(range(len(verts)))])
    o = bpy.data.objects.new(name, me)
    sc.collection.objects.link(o)
    sol = o.modifiers.new("Solid", "SOLIDIFY")
    sol.thickness = depth
    sol.offset = 0
    return put(o, group)

def crown(name, group, x, y, z, r):
    cyl(name + " Band", group, x, y, z, r, r * .55)
    for i in range(5):
        a = 2 * math.pi * i / 5 + math.pi / 2
        bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=r * .32, radius2=0, depth=r * .9,
                                        location=(x + r * .8 * math.cos(a), y + r * .8 * math.sin(a), z + r * .55 + r * .45))
        put(bpy.context.object, group).name = f"{name} Point {i}"
        sphere(f"{name} Pearl {i}", group, x + r * .8 * math.cos(a), y + r * .8 * math.sin(a), z + r * 1.5, r * .12)

def banner(name, group, x, y, top, w, h, notch=.45, facing="y"):
    """Hanging pennant with a notched tail and a rod, facing the camera (-Y) or a side wall."""
    hw = w / 2
    pts = [(-hw, 0), (hw, 0), (hw, -h), (0, -h + notch), (-hw, -h)]
    if facing == "y":
        verts = [(x + px, y, top + pz) for px, pz in pts]
    else:  # on a side wall, facing +/-X
        verts = [(x, y + px, top + pz) for px, pz in pts]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], [list(range(5))])
    o = bpy.data.objects.new(name, me)
    sc.collection.objects.link(o)
    sol = o.modifiers.new("Solid", "SOLIDIFY")
    sol.thickness = .05
    put(o, group)
    if facing == "y":
        line_curve(name + " Rod", group, (x - hw - .2, y - .05, top + .05), (x + hw + .2, y - .05, top + .05), .05)
        sphere(name + " Rod End L", group, x - hw - .22, y - .05, top + .05, .09)
        sphere(name + " Rod End R", group, x + hw + .22, y - .05, top + .05, .09)
    else:
        line_curve(name + " Rod", group, (x, y - hw - .2, top + .05), (x, y + hw + .2, top + .05), .05)

def column(name, group, x, y, r, h):
    box(name + " Plinth", group, x, y, 0, r * 2.6, r * 2.6, .55)
    cyl(name + " Base", group, x, y, .55, r * 1.12, .35)
    torus(name + " Ring Low", group, x, y, .95, r * 1.02, .06)
    cyl(name + " Shaft", group, x, y, .9, r, h - 1.6)
    torus(name + " Ring Mid", group, x, y, h * .48, r * 1.02, .06)
    torus(name + " Ring High", group, x, y, h - .75, r * 1.02, .06)
    box(name + " Capital", group, x, y, h - .7, r * 2.4, r * 2.4, .7)

def lantern(name, group, x, y, z, side=0):
    # bracket from the wall, then the lamp box with a cap
    if side == 0:
        box(name + " Bracket", group, x, y - .25, z + .55, .08, .5, .08)
        box(name + " Lamp", group, x, y - .55, z, .32, .32, .55)
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=.28, radius2=.04, depth=.25, location=(x, y - .55, z + .68), rotation=(0, 0, math.pi / 4))
    else:
        box(name + " Bracket", group, x - side * .25, y, z + .55, .5, .08, .08)
        box(name + " Lamp", group, x - side * .55, y, z, .32, .32, .55)
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=.28, radius2=.04, depth=.25, location=(x - side * .55, y, z + .68), rotation=(0, 0, math.pi / 4))
    put(bpy.context.object, group).name = name + " Cap"

# ---------------- dimensions ----------------
HALL_W = 19.0          # inner width, side walls at x = +/-9.5
BACK_Y = 8.6           # back wall face
WALL_H = 5.4           # where the glass vault starts (camera A sees ~6.6 m at the back wall)
FRONT_Y = -22.0

# ---------------- floor ----------------
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, (BACK_Y + FRONT_Y) / 2, 0))
fl = bpy.context.object
fl.name = "Floor"
fl.scale = (HALL_W + 2, BACK_Y - FRONT_Y + 2, 1)
put(fl, "Floor")

# ---------------- stage stand-in (Unity draws it) ----------------
box("Stage", "Stand-in Stage", 0, 0, 0, 10.8, 10.8, .9)
box("Frame", "Stand-in Stage", 0, 0, .9, 10.4, 10.4, .34)
box("Board", "Stand-in Stage", 0, 0, 1.24, 9.6, 9.6, .01)
pawns = [(0, -.5, 1.3), (-2.4, -.5, 1.15), (2.4, -.5, 1.15), (-1.2, 1.5, 1.15), (1.2, 1.5, 1.15), (-3.6, 1.5, 1.15)]
for i, (px, py, s) in enumerate(pawns):
    z = 1.25
    cyl(f"Pawn {i} Foot", "Stand-in Pawns", px, py, z, .36 * s, .14 * s)
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=.28 * s, radius2=.13 * s, depth=.75 * s, location=(px, py, z + .14 * s + .375 * s))
    put(bpy.context.object, "Stand-in Pawns").name = f"Pawn {i} Body"
    cyl(f"Pawn {i} Collar", "Stand-in Pawns", px, py, z + .86 * s, .22 * s, .07 * s)
    sphere(f"Pawn {i} Head", "Stand-in Pawns", px, py, z + 1.1 * s, .23 * s)

# carpet steps in front of the stage (new: the reference's red runner with a crown)
for i in range(4):
    h = 1.24 - i * .31
    box(f"Front Step {i}", "Front Steps", 0, -5.4 - .2 - i * .42, 0, 3.4, .42, h)
box("Front Runner", "Front Steps", 0, -5.4 - 1.68 - 6, 0, 3.0, 12, .03)
crown("Stage Crown", "Front Steps", 0, -5.42, .7, .32)

# ---------------- back wall with the great gate ----------------
GATE_W, GATE_LEG, GATE_T = 3.4, 2.4, .5       # arch top = 2.4 + 1.7 + .5 = 4.6 m
# back wall: a full slab behind; the gate's dark opening is a recess in front of it
box("Back Wall", "Back Wall", 0, BACK_Y + .3, 0, HALL_W, .6, WALL_H)
box("Back Wall Plinth", "Back Wall", 0, BACK_Y - .05, 0, HALL_W, .3, .6)
box("Back Wall Cornice", "Back Wall", 0, BACK_Y - .05, WALL_H - .3, HALL_W, .4, .3)

GY = BACK_Y - .25
arch_frame("Gate Arch", "Great Gate", 0, GY - .2, 0, GATE_W, GATE_LEG, GATE_T, .7)
# square frame (pilasters + lintel) around the arch, like the reference
for s in (-1, 1):
    box(f"Gate Pilaster {s}", "Great Gate", s * (GATE_W / 2 + GATE_T + .3), GY - .25, 0, .6, .8, GATE_LEG + GATE_W / 2 + GATE_T)
box("Gate Lintel", "Great Gate", 0, GY - .25, GATE_LEG + GATE_W / 2 + GATE_T, GATE_W + 2 * GATE_T + 1.2, .8, .35)
# the two leaves, pivoted on their hinges so they can swing open (like the courtyard gate)
for s, side in ((-1, "L"), (1, "R")):
    hinge_x = s * GATE_W / 2
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(hinge_x, GY, 0))
    pivot = bpy.context.object
    pivot.name = f"GateLeaf_{side}"
    put(pivot, "Great Gate")
    leaf = arch_slab(f"GateLeaf_{side} Door", "Great Gate", 0, GY, 0, GATE_W - .04, GATE_LEG, .16, half=s)
    shield = box(f"GateLeaf_{side} Shield", "Great Gate", s * GATE_W / 4, GY - .1, .8, .95, .06, 1.45)
    for o in (leaf, shield):
        o.parent = pivot
        o.matrix_parent_inverse = pivot.matrix_world.inverted()
box("Gate Step", "Great Gate", 0, GY - .9, 0, GATE_W + 2.6, 1.0, .14)
# crest shield with a crown above the lintel
CREST_Z = GATE_LEG + GATE_W / 2 + GATE_T + .2
box("Gate Crest", "Great Gate", 0, GY - .7, CREST_Z, 1.1, .2, 1.2)
crown("Gate Crest Crown", "Great Gate", 0, GY - .85, CREST_Z + .45, .26)

# crown pedestals either side of the gate
for s in (-1, 1):
    box(f"Pedestal {s}", "Pedestals", s * 3.7, BACK_Y - 1.2, 0, 1.1, 1.1, .3)
    box(f"Pedestal Body {s}", "Pedestals", s * 3.7, BACK_Y - 1.2, .3, .9, .9, 1.25)
    box(f"Pedestal Top {s}", "Pedestals", s * 3.7, BACK_Y - 1.2, 1.55, 1.05, 1.05, .2)
    crown(f"Pedestal Crown {s}", "Pedestals", s * 3.7, BACK_Y - 1.2, 1.75, .38)

# back wall pilasters, lanterns and banners
for s in (-1, 1):
    for i, px in enumerate((5.6, 8.7)):
        box(f"Back Pilaster {s} {i}", "Back Wall", s * px, BACK_Y - .2, 0, .9, .5, WALL_H - .35)
    lantern(f"Back Lantern {s}", "Lanterns", s * 7.15, BACK_Y, 2.9)
    banner(f"Back Banner {s}", "Banners", s * 4.6, BACK_Y - .1, 4.6, .95, 2.3)

# ---------------- side walls with arched windows ----------------
SIDE_X = HALL_W / 2
for s in (-1, 1):
    box(f"Side Wall {s}", "Side Walls", s * (SIDE_X + .3), (BACK_Y + FRONT_Y) / 2, 0, .6, BACK_Y - FRONT_Y, WALL_H)
    box(f"Side Plinth {s}", "Side Walls", s * (SIDE_X - .1), (BACK_Y + FRONT_Y) / 2, 0, .3, BACK_Y - FRONT_Y, .6)
    box(f"Side Cornice {s}", "Side Walls", s * (SIDE_X - .1), (BACK_Y + FRONT_Y) / 2, WALL_H - .35, .4, BACK_Y - FRONT_Y, .35)
    for j, wy in enumerate((4.6, -2.0, -8.6)):
        arch_frame(f"Window {s} {j}", "Side Windows", s * (SIDE_X - .1), wy, 1.1, 1.6, 2.0, .3, .4, plane="yz")
        arch_slab(f"Window Glass {s} {j}", "Side Windows", s * (SIDE_X + .02), wy, 1.1, 1.6, 2.0, .04, plane="yz")
        box(f"Window Sill {s} {j}", "Side Windows", s * (SIDE_X - .2), wy, .95, .5, 2.4, .15)
        banner(f"Side Banner {s} {j}", "Banners", s * (SIDE_X - .15), wy - 3.3, 4.7, 1.0, 2.4, facing="x")
        lantern(f"Side Lantern {s} {j}", "Lanterns", s * SIDE_X, wy + 1.9 if j else wy - 1.9, 2.9, side=s)
    # big round columns standing in front of the side walls
    for j, cy in enumerate((6.4, 1.3, -4.6)):
        column(f"Column {s} {j}", "Columns", s * (SIDE_X - 1.15), cy, .5, WALL_H)

# ---------------- glass vault ----------------
# Ribs across the hall (barrel arches) and a half-dome lunette over the back wall,
# with thin glazing bars. The glass itself is left open in the blockout.
VR = SIDE_X
for k, ry in enumerate((BACK_Y - .3, 4.0, -.6, -5.2, -9.8, -14.4)):
    arc_curve(f"Vault Rib {k}", "Glass Vault", (0, ry, WALL_H), VR, 0, math.pi, "xz", .16, steps=40)
for s in (-1, 1):
    for h in (1.2, 2.6):   # purlins along the hall
        a = math.asin(h / VR) if h < VR else math.pi / 2
        x = s * VR * math.cos(a)
        line_curve(f"Vault Purlin {s} {h}", "Glass Vault", (x, BACK_Y - .3, WALL_H + h), (x, FRONT_Y, WALL_H + h), .06)
line_curve("Vault Ridge", "Glass Vault", (0, BACK_Y - .3, WALL_H + VR), (0, FRONT_Y, WALL_H + VR), .1)
# lunette spokes over the back wall
for k in range(1, 8):
    a = math.pi * k / 8
    line_curve(f"Lunette Spoke {k}", "Glass Vault", (0, BACK_Y - .3, WALL_H), (VR * math.cos(a), BACK_Y - .3, WALL_H + VR * math.sin(a)), .07)
arc_curve("Lunette Inner Ring", "Glass Vault", (0, BACK_Y - .3, WALL_H), VR * .45, 0, math.pi, "xz", .08)

# ---------------- camera, light, world ----------------
cam_data = bpy.data.cameras.new("Cam A (lobby)")
cam_data.sensor_fit = "VERTICAL"
cam_data.angle = math.radians(30)
cam = bpy.data.objects.new("Cam A (lobby)", cam_data)
sc.collection.objects.link(cam)
cam.location = (0.6, -18.6, 7.0)
cam.rotation_euler = (1.296, 0, 0)
sc.camera = cam

sun_d = bpy.data.lights.new("Sun", "SUN")
sun_d.energy = 3.0
sun_d.angle = math.radians(6)
sun = bpy.data.objects.new("Sun", sun_d)
sc.collection.objects.link(sun)
sun.rotation_euler = (math.radians(42), 0, math.radians(18))

world = bpy.data.worlds.new("World")
sc.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.75, 0.78, 0.82, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35

sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 1920, 1080
sc.render.resolution_percentage = 50
sc.view_settings.view_transform = "AgX"
sc.view_settings.look = "AgX - Medium High Contrast"
try:
    sc.eevee.use_shadows = True
except Exception:
    pass

sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "KingdomInterior_blockout.blend"))
print("DONE", OUT)
