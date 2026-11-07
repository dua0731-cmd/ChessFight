"""B East Wing: punch the two relief windows of the Tripo wall through and add iron lattice frames.

Usage:
  blender -b --python b_window_wall_v1.py -- --src "<wall.glb>" [--render] [--export]

Reference: Docs/PawnRush/ConceptArt/R503_RepairWing/B_02_architecture_modules.png, item 4
(arched window, dark iron lattice, warm glowing glass, cream stone surround).
The wall keeps its own scale (about 0.98 m wide); scale it in Unity.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SRC = argv[argv.index("--src") + 1] if "--src" in argv else os.path.expanduser(
    r"~\Downloads\architectural wall 3d model.glb")
DO_RENDER = "--render" in argv
DO_EXPORT = "--export" in argv

# Window opening measured from the Tripo wall (metres, wall space).
WIN_X = (-0.281, 0.281)   # centres of the two relief windows
HALF_W = 0.044            # half width of the opening, inside the relief casing
Z_BOTTOM = 0.178
Z_SPRING = 0.395          # where the pointed arch starts
APEX_H = 0.057            # arch height above the spring line
Y_CUT = (-0.04, 0.13)     # cutter depth, fully through the wall (front ~-0.01, back 0.081)
Y_FRAME = 0.020           # centre of the iron frame inside the reveal
BAR_W = 0.0045            # iron bar face width
BAR_D = 0.008             # iron bar depth
RIM_W = 0.0065            # outer iron rim width


def arch_outline(half_w, z_bottom, z_spring, apex_h, n=24):
    """Closed outline of a pointed arch window in XZ, counter-clockwise, starting bottom-left."""
    c = (apex_h * apex_h - half_w * half_w) / (2 * half_w)  # arc centre offset from the axis
    r = half_w + c
    pts = [(-half_w, z_bottom), (half_w, z_bottom)]
    # right arc: centre (-c, z_spring), from (half_w, spring) up to apex (0, spring + apex_h)
    a0 = 0.0
    a1 = math.atan2(apex_h, c)
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        pts.append((-c + r * math.cos(a), z_spring + r * math.sin(a)))
    # left arc mirrored, from apex down to (-half_w, spring)
    for i in range(1, n + 1):
        a = a1 + (a0 - a1) * i / n
        pts.append((c - r * math.cos(a), z_spring + r * math.sin(a)))
    return pts


def x_at(z, half_w, z_spring, apex_h):
    """Half width of the opening at height z."""
    if z <= z_spring:
        return half_w
    c = (apex_h * apex_h - half_w * half_w) / (2 * half_w)
    r = half_w + c
    dz = z - z_spring
    return max(0.0, math.sqrt(max(r * r - dz * dz, 0.0)) - c)


def new_obj(name, bm, mat=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if mat:
        me.materials.append(mat)
    return ob


def prism(bm, outline, y0, y1):
    """Extrude a closed XZ outline between y0 and y1 (y0 < y1 = front to back)."""
    front = [bm.verts.new((x, y0, z)) for x, z in outline]
    back = [bm.verts.new((x, y1, z)) for x, z in outline]
    bm.faces.new(list(reversed(front)))
    bm.faces.new(back)
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], front[j], back[j], back[i]))


def bar(bm, a, b, w, y, d):
    """Straight rectangular bar from a to b (XZ points) with face width w and depth d."""
    ax, az = a; bx, bz = b
    dx, dz = bx - ax, bz - az
    L = math.hypot(dx, dz)
    nx, nz = -dz / L * w / 2, dx / L * w / 2
    quad = [(ax + nx, az + nz), (bx + nx, bz + nz), (bx - nx, bz - nz), (ax - nx, az - nz)]
    prism(bm, quad, y - d / 2, y + d / 2)


def polyline_bars(bm, pts, w, y, d, closed=False):
    segs = list(zip(pts, pts[1:] + (pts[:1] if closed else [])))
    for a, b in segs:
        bar(bm, a, b, w, y, d)


def make_materials():
    iron = bpy.data.materials.new("B_Win_Iron")
    iron.use_nodes = True
    p = iron.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (0.045, 0.042, 0.040, 1)
    p.inputs["Metallic"].default_value = 0.85
    p.inputs["Roughness"].default_value = 0.55

    glass = bpy.data.materials.new("B_Win_Glass")
    glass.use_nodes = True
    p = glass.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (1.0, 0.82, 0.52, 1)
    p.inputs["Roughness"].default_value = 0.15
    p.inputs["Emission Color"].default_value = (1.0, 0.72, 0.38, 1)
    p.inputs["Emission Strength"].default_value = 1.6
    p.inputs["Alpha"].default_value = 0.72
    glass.surface_render_method = 'BLENDED'
    return iron, glass


def build_window(cx, iron, glass):
    outline = [(cx + x, z) for x, z in arch_outline(HALF_W, Z_BOTTOM, Z_SPRING, APEX_H)]

    # Outer iron rim following the opening edge.
    bm = bmesh.new()
    inner = [(cx + x, z) for x, z in arch_outline(HALF_W - RIM_W, Z_BOTTOM + RIM_W, Z_SPRING, APEX_H - RIM_W * 0.6)]
    front_o = [bm.verts.new((x, Y_FRAME - BAR_D / 2, z)) for x, z in outline]
    back_o = [bm.verts.new((x, Y_FRAME + BAR_D / 2, z)) for x, z in outline]
    front_i = [bm.verts.new((x, Y_FRAME - BAR_D / 2, z)) for x, z in inner]
    back_i = [bm.verts.new((x, Y_FRAME + BAR_D / 2, z)) for x, z in inner]
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front_o[j], front_o[i], front_i[i], front_i[j]))
        bm.faces.new((back_o[i], back_o[j], back_i[j], back_i[i]))
        bm.faces.new((front_o[i], front_o[j], back_o[j], back_o[i]))
        bm.faces.new((front_i[j], front_i[i], back_i[i], back_i[j]))

    # Lattice: three lights (two mullions), transoms below the spring line,
    # and two small pointed sub-arches under the main arch (concept item 4).
    hw = HALF_W - RIM_W
    mx = hw / 3
    zb = Z_BOTTOM + RIM_W
    for sx in (-mx, mx):
        top = Z_SPRING + (APEX_H - RIM_W) * 0.45
        bar(bm, (cx + sx, zb), (cx + sx, top), BAR_W, Y_FRAME, BAR_D * 0.85)
    rows = 5
    for k in range(1, rows):
        z = zb + (Z_SPRING - zb) * k / rows
        bar(bm, (cx - hw, z), (cx + hw, z), BAR_W * 0.8, Y_FRAME, BAR_D * 0.75)
    bar(bm, (cx - hw, Z_SPRING), (cx + hw, Z_SPRING), BAR_W, Y_FRAME, BAR_D * 0.85)
    # sub-arches: each spans from a side to the centre, springing at Z_SPRING
    sub_w = hw / 2
    for sc in (-sub_w, sub_w):
        pts = [(cx + sc + x, z) for x, z in arch_outline(sub_w, Z_SPRING, Z_SPRING, APEX_H * 0.55, n=10)][2:]
        polyline_bars(bm, pts, BAR_W * 0.8, Y_FRAME, BAR_D * 0.75)
    # centre post up to the apex
    bar(bm, (cx, Z_SPRING), (cx, Z_SPRING + APEX_H - RIM_W * 0.6), BAR_W * 0.8, Y_FRAME, BAR_D * 0.75)
    frame = new_obj("B_Window_Frame_%s" % ("L" if cx < 0 else "R"), bm, iron)

    # Glass: one thin pane in the middle of the frame.
    bm = bmesh.new()
    prism(bm, outline, Y_FRAME - 0.0006, Y_FRAME + 0.0006)
    pane = new_obj("B_Window_Glass_%s" % ("L" if cx < 0 else "R"), bm, glass)

    # Cutter
    bm = bmesh.new()
    prism(bm, outline, Y_CUT[0], Y_CUT[1])
    cutter = new_obj("cutter", bm)
    return frame, pane, cutter


def inside(pt, poly):
    x, z = pt
    hit = False
    for (x1, z1), (x2, z2) in zip(poly, poly[1:] + poly[:1]):
        if (z1 > z) != (z2 > z) and x < x1 + (z - z1) * (x2 - x1) / (z2 - z1):
            hit = not hit
    return hit


def clear_inside(wall, cx):
    """Delete faces left inside the opening. Parts of the Tripo relief window have
    flipped normals, so the boolean keeps them; the new reveal faces sit on the
    outline and survive because the test outline is shrunk by 0.6 mm."""
    shrink = 0.0006
    poly = [(cx + x, z) for x, z in arch_outline(HALF_W - shrink, Z_BOTTOM + shrink, Z_SPRING, APEX_H - shrink)]
    bm = bmesh.new()
    bm.from_mesh(wall.data)
    mw = wall.matrix_world
    doomed = []
    for f in bm.faces:
        c = mw @ f.calc_center_median()
        if inside((c.x, c.z), poly):
            doomed.append(f)
    bmesh.ops.delete(bm, geom=doomed, context='FACES')
    bm.to_mesh(wall.data); bm.free()
    print("cleared", len(doomed), "faces inside window at x", cx)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SRC)
    wall = [o for o in bpy.data.objects if o.type == 'MESH'][0]
    wall.name = "B_Wall_Arcade"
    iron, glass = make_materials()

    parts = []
    for cx in WIN_X:
        frame, pane, cutter = build_window(cx, iron, glass)
        mod = wall.modifiers.new("cut", 'BOOLEAN')
        mod.operation = 'DIFFERENCE'
        mod.solver = 'EXACT'
        # The Tripo mesh has overlapping shells (the relief lattice sits on the wall),
        # so self-intersection and hole tolerance are needed to cut everything.
        mod.use_self = True
        mod.use_hole_tolerant = True
        mod.object = cutter
        bpy.context.view_layer.objects.active = wall
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cutter)
        clear_inside(wall, cx)
        parts += [frame, pane]
    for p in parts:
        p.parent = wall
    print("WALL faces after cut:", len(wall.data.polygons))

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "B_WindowWall_v1.blend"))
    if DO_EXPORT:
        bpy.ops.export_scene.gltf(filepath=os.path.join(HERE, "B_WindowWall_v1.glb"), export_format='GLB')
    if DO_RENDER:
        render(wall)


def render(wall):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1600; scene.render.resolution_y = 1200
    scene.view_settings.view_transform = 'AgX'
    world = bpy.data.worlds.new("w"); scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.86, 0.80, 0.72, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    # preview only: cream stone like the concept (the exported wall keeps its own material)
    for m in wall.data.materials:
        if m and m.use_nodes:
            m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.80, 0.72, 0.58, 1)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN'))
    sun.data.energy = 3.5; sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
    scene.collection.objects.link(sun)
    back = bpy.data.objects.new("back", bpy.data.lights.new("back", 'AREA'))
    back.data.energy = 8; back.data.size = 1.0; back.data.color = (1.0, 0.85, 0.6)
    back.location = (0, 0.5, 0.35); back.rotation_euler = (math.radians(90), 0, math.radians(180))
    scene.collection.objects.link(back)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam); scene.camera = cam
    cam.data.lens = 50
    shots = {
        "front": ((0, -1.6, 0.30), (0, 0, 0.30)),
        "q34": ((-0.9, -1.2, 0.55), (-0.05, 0, 0.28)),
        "close": ((-0.36, -0.42, 0.36), (-0.281, 0.02, 0.32)),
        "back": ((0.4, 1.3, 0.4), (0, 0, 0.30)),
    }
    for name, (loc, tgt) in shots.items():
        cam.location = loc
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(HERE, "render_b_window_%s.png" % name)
        bpy.ops.render.render(write_still=True)


main()
