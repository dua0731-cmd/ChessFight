"""Pawn Rush map kit v1: blocky cream-stone parts (concept art 2, 'King Rush' look).

Run headless:
  blender --background --python build_parts_v1.py -- <out_dir>
Units: 1 Blender unit = 1 m. Each part is exported as FBX (+ a .blend and a preview png).
Pivot: bottom center of each part, +Y forward in Blender (exports as Unity -Z forward via FBX axis).
"""
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.abspath(".")
os.makedirs(OUT, exist_ok=True)

PALETTE = {
    "Stone_Cream": (0.91, 0.86, 0.74, 1.0, 0.0, 0.55),
    "Stone_CreamDark": (0.80, 0.73, 0.60, 1.0, 0.0, 0.6),
    "Stone_Shadow": (0.62, 0.56, 0.48, 1.0, 0.0, 0.65),
    "Tile_White": (0.95, 0.94, 0.90, 1.0, 0.0, 0.12),
    "Tile_Black": (0.10, 0.10, 0.13, 1.0, 0.0, 0.10),
    "Trim_Red": (0.78, 0.18, 0.20, 1.0, 0.0, 0.35),
    "Trim_White": (0.95, 0.93, 0.88, 1.0, 0.0, 0.35),
    "Banner_Navy": (0.16, 0.27, 0.62, 1.0, 0.0, 0.7),
    "Gold": (0.93, 0.70, 0.20, 1.0, 1.0, 0.3),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    r, g, b, a, metal, rough = PALETTE[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (r, g, b, a)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (r, g, b, a)
    return m


def box(name, size, loc, material, bevel=0.03, jitter=0.0, rng=None):
    """Axis-aligned box. size = (x, y, z) full extents, loc = center."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if jitter and rng:
        # tiny per-block tilt so the wall does not look machine-perfect
        o.rotation_euler = (0, 0, rng.uniform(-jitter, jitter))
    if bevel > 0:
        mod = o.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier="bevel")
    o.data.materials.append(mat(material))
    return o


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    # The joined object keeps the FIRST box's origin; every part is modelled around the world origin
    # (its pivot), so move the origin back there. Without this the FBX root sits off the mesh and
    # Unity places the part shifted by that offset times its scale (10-17: floors 0.5 m too high).
    # The joined mesh also sits in the first part's rotation (a tilted plank tilted a whole bridge, 10-22):
    # bake it, so the part's own axes are the world's.
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    # Round parts read as true circles (승규 10-22 "각이 안 지게"): smooth across faces under 35°,
    # so cylinders and bevels are smooth while box corners stay crisp.
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    except Exception:
        pass
    return o


def export(part, name):
    bpy.ops.object.select_all(action="DESELECT")
    part.select_set(True)
    bpy.context.view_layer.objects.active = part
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, name + ".fbx"),
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
    )
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + ".blend"))
    preview(part, name)


def preview(part, name):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "MATERIAL"
    sc.render.resolution_x, sc.render.resolution_y = 900, 600
    sc.render.film_transparent = False
    sc.world = bpy.data.worlds.new("w") if sc.world is None else sc.world
    sc.world.color = (0.62, 0.78, 0.95)
    dims = Vector(part.dimensions)
    r = max(dims) * 1.25 + 1.0
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    sc.collection.objects.link(cam)
    center = Vector((0, 0, dims.z / 2))
    cam.location = center + Vector((r, -r, r * 0.65))
    direction = center - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.filepath = os.path.join(OUT, name + "_preview.png")
    bpy.ops.render.render(write_still=True)


# ---------------------------------------------------------------- parts


def wall_block(width=4.0, height=3.0, depth=1.0, name="PR_Wall_4x3"):
    """Running-bond cream stone wall. Block = 1.0 x 0.5 m face."""
    reset()
    rng = random.Random(7)
    objs = []
    bw, bh = 1.0, 0.5
    rows = int(round(height / bh))
    cols = int(round(width / bw))
    gap = 0.02
    for r in range(rows):
        shift = 0.5 * bw if r % 2 else 0.0
        for c in range(cols + 1):
            x0 = -width / 2 + c * bw - shift
            x1 = x0 + bw
            x0c, x1c = max(x0, -width / 2), min(x1, width / 2)
            if x1c - x0c < 0.2:
                continue
            tone = rng.choice(["Stone_Cream", "Stone_Cream", "Stone_CreamDark"])
            objs.append(
                box(
                    f"b{r}_{c}",
                    (x1c - x0c - gap, depth, bh - gap),
                    ((x0c + x1c) / 2, 0, r * bh + bh / 2),
                    tone,
                    bevel=0.035,
                )
            )
    part = join(objs, name)
    export(part, name)


def battlement(width=4.0, name="PR_Battlement_4m"):
    """Crenellated parapet: 2 merlons 1 m wide, 1 m tall, 1 m deep with 1 m gaps; base rail 0.4 m."""
    reset()
    objs = [box("rail", (width, 1.0, 0.4), (0, 0, 0.2), "Stone_CreamDark", bevel=0.04)]
    for i, x in enumerate((-1.5, 0.5)):
        objs.append(box(f"merlon{i}", (1.0, 1.0, 1.0), (x + 0.5 - 0.5 + 0.5, 0, 0.9), "Stone_Cream", bevel=0.05))
    # first merlon spans -1.5..-0.5 -> center -1.0, second 0.5..1.5 -> center 1.0
    objs[1].location.x = -1.0
    objs[2].location.x = 1.0
    part = join(objs, name)
    export(part, name)


def checker_slab(cells=2, cell=2.25, thick=0.5, name="PR_Floor_Checker_4p5"):
    """Glossy black/white chess floor slab with cream stone side skirt. cell matches the 2.25 m mission cells."""
    reset()
    objs = []
    size = cells * cell
    for i in range(cells):
        for j in range(cells):
            m = "Tile_White" if (i + j) % 2 == 0 else "Tile_Black"
            objs.append(
                box(
                    f"t{i}_{j}",
                    (cell - 0.01, cell - 0.01, thick),
                    (-size / 2 + cell * (i + 0.5), -size / 2 + cell * (j + 0.5), -thick / 2),
                    m,
                    bevel=0.02,
                )
            )
    objs.append(box("core", (size, size, thick - 0.06), (0, 0, -thick / 2 - 0.03), "Stone_Shadow", bevel=0.0))
    part = join(objs, name)
    export(part, name)


def stripe_trim(length=4.5, step=0.75, name="PR_Trim_RedWhite_4p5"):
    """Red/white striped board edge, 0.5 m wide x 0.5 m high, 4.5 m long (matches the floor slab), blocks alternate every 0.75 m."""
    reset()
    objs = []
    n = int(round(length / step))
    for i in range(n):
        m = "Trim_Red" if i % 2 == 0 else "Trim_White"
        objs.append(box(f"s{i}", (step - 0.01, 0.5, 0.5), (-length / 2 + step * (i + 0.5), 0, 0.25), m, bevel=0.03))
    part = join(objs, name)
    export(part, name)


def pillar(size=1.2, height=3.0, name="PR_Pillar_1p2x3"):
    """Square stone pillar: wide base, shaft in stacked blocks, flared cap."""
    reset()
    objs = [box("base", (size + 0.3, size + 0.3, 0.4), (0, 0, 0.2), "Stone_CreamDark", bevel=0.04)]
    shaft_h = height - 0.4 - 0.4
    n = max(1, int(round(shaft_h / 0.5)))
    bh = shaft_h / n
    for i in range(n):
        tone = "Stone_Cream" if i % 2 == 0 else "Stone_CreamDark"
        objs.append(box(f"s{i}", (size, size, bh - 0.02), (0, 0, 0.4 + bh * (i + 0.5)), tone, bevel=0.035))
    objs.append(box("cap", (size + 0.3, size + 0.3, 0.4), (0, 0, height - 0.2), "Stone_CreamDark", bevel=0.04))
    part = join(objs, name)
    export(part, name)


def tower_block(size=4.0, height=6.0, name="PR_Tower_4x6"):
    """Tower body in running-bond blocks on all four faces, crenellated top (E bell towers / plaza corners)."""
    reset()
    rng = random.Random(11)
    bh, bw, skin, gap = 0.5, 1.0, 0.5, 0.02
    objs = [box("core", (size - 2 * skin + 0.02, size - 2 * skin + 0.02, height), (0, 0, height / 2), "Stone_Shadow", bevel=0.0)]
    rows = int(round(height / bh))
    half = size / 2
    for r in range(rows):
        z = r * bh + bh / 2
        shift = 0.5 * bw if r % 2 else 0.0
        # south/north faces span the full width (own the corners), east/west faces fill between them
        for side in (-1, 1):
            n = int(round(size / bw)) + 1
            for c in range(n):
                x0 = -half + c * bw - shift
                x0c, x1c = max(x0, -half), min(x0 + bw, half)
                if x1c - x0c < 0.2:
                    continue
                tone = rng.choice(["Stone_Cream", "Stone_Cream", "Stone_CreamDark"])
                objs.append(box(f"ns{r}_{side}_{c}", (x1c - x0c - gap, skin, bh - gap), ((x0c + x1c) / 2, side * (half - skin / 2), z), tone, bevel=0.035))
            inner = half - skin
            for c in range(n):
                y0 = -inner + c * bw - (bw - shift)
                y0c, y1c = max(y0, -inner), min(y0 + bw, inner)
                if y1c - y0c < 0.2:
                    continue
                tone = rng.choice(["Stone_Cream", "Stone_Cream", "Stone_CreamDark"])
                objs.append(box(f"ew{r}_{side}_{c}", (skin, y1c - y0c - gap, bh - gap), (side * (half - skin / 2), (y0c + y1c) / 2, z), tone, bevel=0.035))
    objs.append(box("lip", (size + 0.3, size + 0.3, 0.3), (0, 0, height + 0.15), "Stone_CreamDark", bevel=0.04))
    for i, (x, y) in enumerate(((-1.5, -1.5), (1.5, -1.5), (-1.5, 1.5), (1.5, 1.5))):
        objs.append(box(f"m{i}", (1.0, 1.0, 0.8), (x, y, height + 0.7), "Stone_Cream", bevel=0.05))
    part = join(objs, name)
    export(part, name)


def kit_sheet(names, name="PR_Kit_v1_sheet"):
    """Lay the exported parts out as a small course corner and render it under a sunny sky (EEVEE)."""
    reset()

    def place(part, loc, rot_z=0.0):
        bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, part + ".fbx"))
        o = bpy.context.selected_objects[0]
        o.location = loc
        o.rotation_euler.z = math.radians(rot_z)
        return o

    # 3 x 3 checker floor (13.5 m) with striped edges on the left/right
    for i in (-1, 0, 1):
        for j in (-1, 0, 1):
            place("PR_Floor_Checker_4p5", (i * 4.5, j * 4.5, 0))
    for y in (-4.5, 0.0, 4.5):
        place("PR_Trim_RedWhite_4p5", (-7.0, y, 0), 90)
        place("PR_Trim_RedWhite_4p5", (7.0, y, 0), 90)
    # back wall with battlements, towers at the corners, pillars at the front
    for x in (-4.0, 0.0, 4.0):
        place("PR_Wall_4x3", (x, 8.5, 0))
        place("PR_Battlement_4m", (x, 8.5, 3.0))
    place("PR_Tower_4x6", (-8.5, 9.0, 0))
    place("PR_Tower_4x6", (8.5, 9.0, 0))
    place("PR_Pillar_1p2x3", (-5.0, -5.0, 0))
    place("PR_Pillar_1p2x3", (5.0, -5.0, 0))

    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    world = bpy.data.worlds.new("sky")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.45, 0.68, 0.98, 1.0)
    bg.inputs["Strength"].default_value = 0.55
    sc.world = world
    sun_data = bpy.data.lights.new("sun", "SUN")
    sun_data.energy = 2.2
    sun_data.color = (1.0, 0.96, 0.88)
    sun = bpy.data.objects.new("sun", sun_data)
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.lens = 28
    sc.collection.objects.link(cam)
    cam.location = (0, -17, 6.5)
    target = Vector((0, 3, 2))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + ".blend"))


if __name__ == "__main__":
    wall_block()
    battlement()
    checker_slab()
    stripe_trim()
    pillar()
    tower_block()
    kit_sheet(None)
    print("DONE", OUT)
