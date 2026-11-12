# Shared grey-clay helpers for the kingdom interior lobby blockouts (blockout.py, blockout_hall.py).
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
        # one simple loop: left = outer foot -> up -> top -> centre foot, right = centre foot -> top -> down
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

