"""Review renders: EEVEE (ray tracing on), the warm candle-lit war room, cameras, the top-down layout sheets."""
import math
import os

import bpy
from mathutils import Vector

import eg_bl_materials as MAT


def setup(engine="EEVEE", width=1600, height=900, samples=32, raytrace=True, film_transparent=False):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE" if engine == "EEVEE" else ("CYCLES" if engine == "CYCLES" else "BLENDER_WORKBENCH")
    sc.render.resolution_x, sc.render.resolution_y = width, height
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = film_transparent
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "AgX"
    try:
        sc.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    if engine == "EEVEE":
        ee = sc.eevee
        ee.taa_render_samples = samples
        if hasattr(ee, "use_raytracing"):
            ee.use_raytracing = raytrace
            if raytrace and hasattr(ee, "ray_tracing_options"):
                ee.ray_tracing_options.resolution_scale = "2"
                ee.ray_tracing_options.trace_max_roughness = 0.6
        if hasattr(ee, "use_shadows"):
            ee.use_shadows = True
        if hasattr(ee, "shadow_pool_size"):
            ee.shadow_pool_size = "1024"
        if hasattr(ee, "shadow_ray_count"):
            ee.shadow_ray_count = 1
            ee.shadow_step_count = 4
        if hasattr(ee, "fast_gi_method"):
            ee.fast_gi_method = "GLOBAL_ILLUMINATION"
        if hasattr(ee, "use_fast_gi"):
            ee.use_fast_gi = True
    elif engine == "CYCLES":
        sc.cycles.device = "CPU"
        sc.cycles.samples = samples
        sc.cycles.use_denoising = True
    else:
        sh = sc.display.shading
        sh.light = "STUDIO"
        sh.color_type = "MATERIAL"
        sh.show_shadows = True
        sh.show_cavity = False
        sc.display.render_aa = "8"
    return sc


def world(strength=0.35, color="#3A4446"):
    w = bpy.data.worlds.get("EG_World") or bpy.data.worlds.new("EG_World")
    bpy.context.scene.world = w
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes.get("Background") or nt.nodes.new("ShaderNodeBackground")
    bg.inputs["Color"].default_value = MAT.srgb(color)
    bg.inputs["Strength"].default_value = strength
    return w


def light(name, kind, loc, energy, color="#FFD9A0", rot=(0, 0, 0), size=1.0, coll=None, spot=None, shadow=False):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = MAT.srgb(color)[:3]
    if kind == "AREA":
        ld.size = size
    elif kind in ("POINT", "SPOT"):
        ld.shadow_soft_size = size
    elif kind == "SUN":
        ld.angle = math.radians(size)
    if kind == "SPOT" and spot:
        ld.spot_size = math.radians(spot)
        ld.spot_blend = 0.4
    if kind != "SUN":
        ld.use_shadow = shadow
    o = bpy.data.objects.new(name, ld)
    (coll or bpy.context.scene.collection).objects.link(o)
    o.location = loc
    o.rotation_euler = rot
    return o


def camera(name, loc, target, lens=28.0, coll=None, ortho=None):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.clip_start = 0.1
    cd.clip_end = 1000
    if ortho:
        cd.type = "ORTHO"
        cd.ortho_scale = ortho
    o = bpy.data.objects.new(name, cd)
    (coll or bpy.context.scene.collection).objects.link(o)
    o.location = loc
    d = Vector(target) - Vector(loc)
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    return o


def render(cam, path, width=None, height=None):
    sc = bpy.context.scene
    sc.camera = cam
    if width:
        sc.render.resolution_x = width
    if height:
        sc.render.resolution_y = height
    sc.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    return path


def kit_sheet(objs, coll, path, cols=8, gap=2.0, width=2400, height=1350, samples=24):
    """Lay the kit out on a grid (copies) and render one sheet."""
    x = y = 0.0
    row_h = 0.0
    col = 0
    placed = []
    for o in objs:
        d = o.dimensions
        if col == cols:
            col = 0
            x = 0.0
            y -= row_h + gap
            row_h = 0.0
        c = bpy.data.objects.new(o.name + "_sheet", o.data)
        coll.objects.link(c)
        bb = [Vector(v) for v in o.bound_box]
        mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
        c.location = (x - mn.x, y - mn.y - d.y, -mn.z)
        placed.append(c)
        x += d.x + gap
        row_h = max(row_h, d.y)
        col += 1
    xs = [p.location.x for p in placed]
    ys = [p.location.y for p in placed]
    cx, cy = (min(xs) + max(xs)) / 2 + 4, (min(ys) + max(ys)) / 2
    span = max(max(xs) - min(xs), (max(ys) - min(ys)) * width / height) + 16
    cam = camera("KitSheetCam", (cx, cy - span * 0.55, span * 0.62), (cx, cy, 0), lens=35, coll=coll)
    sun = light("KitSun", "SUN", (0, 0, 30), 3.2, "#FFF1DC", rot=(math.radians(50), 0, math.radians(30)), size=3, coll=coll)
    fill = light("KitFill", "SUN", (0, 0, 30), 0.8, "#BFD6E6", rot=(math.radians(60), 0, math.radians(-140)), size=10, coll=coll)
    floor = bpy.data.meshes.new("KitFloor")
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=span * 2)
    bm.to_mesh(floor)
    fo = bpy.data.objects.new("KitFloor", floor)
    fo.location = (cx, cy, -0.01)
    m = bpy.data.materials.new("KitFloorMat")
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = MAT.srgb("#30383A")
    floor.materials.append(m)
    coll.objects.link(fo)
    setup("EEVEE", width, height, samples)
    world(0.6, "#4A5456")
    return render(cam, path)


def skin_tiles(objs, outdir, size=480, samples=16, angles=(35.0,), names=None, sheet="skins_sheet.png", cols=6):
    """Each device skin alone, framed from a 3/4 view (one tile per angle), then a contact sheet with labels.

    With several angles the tiles of one skin sit side by side (a turntable sheet): the first tile carries the name,
    the others how far the camera went round."""
    from mathutils import Vector as V
    sc = bpy.context.scene
    hidden = [o for o in bpy.data.objects if not o.hide_render]
    for o in hidden:
        o.hide_render = True
    coll = bpy.data.collections.get("EG_SkinTiles") or bpy.data.collections.new("EG_SkinTiles")
    if coll.name not in sc.collection.children:
        sc.collection.children.link(coll)
    sun = light("Tile sun", "SUN", (0, 0, 50), 3.0, "#FFF0DA", rot=(math.radians(52), 0, math.radians(35)), size=3, coll=coll)
    fill = light("Tile fill", "SUN", (0, 0, 50), 0.9, "#BFD6E6", rot=(math.radians(65), 0, math.radians(-150)), size=10, coll=coll)
    sun.hide_render = fill.hide_render = False
    setup("EEVEE", size, size, samples)
    world(0.5, "#3B4547")
    paths = []
    for o in objs:
        c = bpy.data.objects.new(o.name + " tile", o.data)
        coll.objects.link(c)
        c.location = (1000.0, 0.0, 0.0)
        bpy.context.view_layer.update()
        bb = [c.matrix_world @ V(v) for v in c.bound_box]
        lo = V((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
        hi = V((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
        centre, radius = (lo + hi) / 2, max((hi - lo).length / 2, 0.5)
        for a in angles:
            d = V((math.cos(math.radians(a + 200)), math.sin(math.radians(a + 200)), 0.55)).normalized()
            cam = camera("Tile cam", centre + d * radius * 3.1, centre, lens=50, coll=coll)
            path = os.path.join(outdir, "skins", f"{o.name}_{int(a)}.png")
            render(cam, path)
            paths.append((o.name, a, path))
            bpy.data.objects.remove(cam)
        bpy.data.objects.remove(c)
    bpy.data.objects.remove(sun)
    bpy.data.objects.remove(fill)
    for o in hidden:
        o.hide_render = False
    try:
        from PIL import Image, ImageDraw, ImageFont
        rows = (len(paths) + cols - 1) // cols
        img = Image.new("RGB", (cols * size, rows * (size + 40)), (24, 30, 31))
        font = None
        for f in ("/usr/share/fonts/truetype/nanum/NanumSquareB.ttf", "C:/Windows/Fonts/malgunbd.ttf"):
            if os.path.exists(f):
                font = ImageFont.truetype(f, 22)
                break
        draw = ImageDraw.Draw(img)
        for i, (name, a, path) in enumerate(paths):
            x, y = (i % cols) * size, (i // cols) * (size + 40)
            img.paste(Image.open(path).convert("RGB"), (x, y))
            if a == angles[0]:
                draw.text((x + 10, y + size + 8), (names or {}).get(name, name), fill=(239, 230, 212), font=font)
            if len(angles) > 1:
                draw.text((x + 10, y + 8), f"{int(round(a - angles[0]))}°", fill=(239, 230, 212), font=font)
        if len(angles) > 1 and cols % len(angles) == 0:
            for c in range(len(angles), cols, len(angles)):
                draw.line([(c * size, 0), (c * size, img.height)], fill=(239, 230, 212), width=2)
        img.save(os.path.join(outdir, sheet))
    except ImportError:
        pass
    return paths
