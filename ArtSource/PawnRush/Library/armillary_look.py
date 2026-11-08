"""Pawn Rush library (작전 회의실): the start armillary textured like its concept image — preview only (R361).

    blender --background --factory-startup --python armillary_look.py -- --preview D:/check   (renders, writes nothing)

승규 님 sent the concept image (worn gold rings and stand, a navy celestial globe with gold constellations and
stars, a warm brown wooden table) with "텍스쳐 입혀". The FBX's Start_armillary (R324 / R327, armillary.py) keeps
its shapes: the table and the gold stand are one welded mesh, split here by height (faces above the table top
are the stand), the three rings are gold, the globe gets a star map painted with numpy (equirectangular,
mapped by sphere projection about its own pivot, the sphere centre) and its two raised seam bands gold.
The war map beside it is drawn as war_map.py builds it. Writes only the star map and the renders.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import war_map  # noqa: E402
from war_map import FBX, Canvas, PPM, fbm, metal, principled, wood  # noqa: E402

ROOT = "Start_armillary"
STAND = "Start_armillary|Original_mesh_0|Dupli|"
RINGS = ("Start_armillary_Ring1", "Start_armillary_Ring2", "Start_armillary_Ring3")
SPHERE = "Start_armillary_Sphere"
SPHERE_R, BAND_R = .333, .339     # globe radius and where its raised seam bands begin (armillary space)
NAVY, NAVY_DK = np.array((.22, .31, .46)), np.array((.10, .16, .27))
STAR_GOLD, LINE_GOLD = (.98, .76, .38), (.86, .62, .28)


# ------------------------------------------------------------------ the star map

def paint_stars(path, w=2048, h=1024):
    rng = np.random.default_rng(11)
    cv = Canvas(w / PPM, h / PPM)
    cv.aa = 1.4 / PPM
    t = np.clip(fbm(cv.h, cv.w, 5, 6, rng) * 1.3 - .2, 0, 1)
    cv.c[:] = NAVY_DK * (1 - t[..., None]) + NAVY * t[..., None]
    speck = fbm(cv.h, cv.w, 120, 2, rng)
    cv.c[:] *= (.9 + .2 * speck)[..., None]
    # faint gold graticule: meridians every 30 degrees, parallels every 30 degrees and the equator brighter
    for i in range(12):
        u = i / 12
        cv.polyline([(u, .02), (u, .98)], .003, LINE_GOLD, alpha=.25)
    for lat in (-60, -30, 30, 60):
        v = .5 - lat / 180
        cv.polyline([(0, v), (1, v)], .003, LINE_GOLD, alpha=.25)
    cv.polyline([(0, .5), (1, .5)], .005, LINE_GOLD, alpha=.45)

    def star(p, r, alpha=1.0):
        """A four-point sparkle, widened by 1 / cos(latitude) so it stays round on the globe."""
        x, y = cv.m(p)
        sx = 1 / max(math.cos((.5 - p[1]) * math.pi), .25)
        for dx, dy in ((1, 0), (0, 1)):
            long, thin = r * 2.4, r * .35
            cv.convex([(x - dx * long * sx, y - dy * long), (x + dy * thin * sx, y + dx * thin),
                       (x + dx * long * sx, y + dy * long), (x - dy * thin * sx, y - dx * thin)], STAR_GOLD, alpha)
        cv.convex([(x - r * .7 * sx, y), (x, y + r * .7), (x + r * .7 * sx, y), (x, y - r * .7)], (1, .9, .6), alpha)

    # constellations: a few stars joined by gold lines, kept off the poles where the map squeezes
    for k in range(26):
        cu, cvv = (k + rng.random() * .6) / 26, .25 + rng.random() * .5
        n = rng.integers(4, 8)
        pts = [(cu, cvv)]
        for _ in range(n - 1):
            a = rng.random() * 2 * math.pi
            d = .04 + rng.random() * .05
            pu, pv = pts[-1]
            pts.append((pu + math.cos(a) * d * .5, min(max(pv + math.sin(a) * d, .2), .8)))
        cv.polyline(pts, .008, LINE_GOLD, alpha=.9)
        if rng.random() < .5:
            j = rng.integers(0, len(pts) - 1)
            cv.polyline([pts[j], (pts[j][0] + .03, pts[j][1] + .06)], .008, LINE_GOLD, alpha=.9)
        for p in pts:
            star(p, .011 + rng.random() * .008)
    # scattered stars of all sizes
    for _ in range(320):
        p = (rng.random(), .08 + rng.random() * .84)
        r = .003 + rng.random() ** 3 * .01
        if r > .006:
            star(p, r, .9)
        else:
            cv.disc(cv.m(p), r, STAR_GOLD, alpha=.5 + rng.random() * .5)

    img = bpy.data.images.new("armillary_star_map", cv.w, cv.h, alpha=False)
    rgba = np.ones((cv.h, cv.w, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(cv.c[::-1], 0, 1)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


# ------------------------------------------------------------------ materials

def globe(img, gold):
    """The star map by sphere projection about the pivot; past BAND_R (the seam bands) the gold's colour."""
    m, nt, b = principled("Armillary globe", (1, 1, 1), .15, .55)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.projection = "SPHERE"
    tex.interpolation = "Cubic"
    centred = nt.nodes.new("ShaderNodeVectorMath")   # sphere projection turns about (.5, .5, .5)
    centred.operation = "ADD"
    centred.inputs[1].default_value = (.5, .5, .5)
    nt.links.new(co, centred.inputs[0])
    nt.links.new(centred.outputs["Vector"], tex.inputs["Vector"])
    ln = nt.nodes.new("ShaderNodeVectorMath")
    ln.operation = "LENGTH"
    nt.links.new(co, ln.inputs[0])
    band = nt.nodes.new("ShaderNodeMath")
    band.operation = "GREATER_THAN"
    band.inputs[1].default_value = BAND_R
    nt.links.new(ln.outputs["Value"], band.inputs[0])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    nt.links.new(band.outputs["Value"], mix.inputs["Factor"])
    nt.links.new(tex.outputs["Color"], mix.inputs["A"])
    mix.inputs["B"].default_value = (*gold, 1)
    nt.links.new(mix.outputs["Result"], b.inputs["Base Color"])
    met = nt.nodes.new("ShaderNodeMapRange")
    met.inputs["To Min"].default_value, met.inputs["To Max"].default_value = .15, .9
    nt.links.new(band.outputs["Value"], met.inputs["Value"])
    nt.links.new(met.outputs["Result"], b.inputs["Metallic"])
    return m


def split_stand(ob, wood_mat, gold_mat):
    """Faces above the table top are the gold stand; the table is wood (armillary space, pivot on the axis)."""
    me = ob.data
    me.materials.clear()
    me.materials.append(wood_mat)
    me.materials.append(gold_mat)
    edge = [v.co for v in me.vertices if math.hypot(v.co.x, v.co.y) > .45]
    top = max(c.z for c in edge)
    for p in me.polygons:
        c = p.center
        p.material_index = 1 if c.z > top + .004 and math.hypot(c.x, c.y) < .45 else 0
    print("STAND table top", round(top, 3), "gold faces", sum(p.material_index for p in me.polygons))


# ------------------------------------------------------------------ preview

def preview(out):
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=FBX)
    sc = bpy.context.scene
    stone = bpy.data.materials.get("CLAY | uniform neutral gray")
    b = stone.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (.70, .58, .47, 1)
    b.inputs["Roughness"].default_value = .62
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
        elif o.type == "MESH":
            zs = [(o.matrix_world @ Vector(c)).z for c in o.bound_box]
            if min(zs) > 4.2:
                o.hide_render = True

    gold_c = (1.0, .62, .20)
    gold = metal("Armillary gold", gold_c, (.80, .46, .12), .26, 6)
    warm_wood = wood("Armillary table wood", "Z")
    ramp = next(n for n in warm_wood.node_tree.nodes if n.type == "VALTORGB")
    ramp.color_ramp.elements[0].color = (.16, .07, .025, 1)
    ramp.color_ramp.elements[1].color = (.42, .20, .07, 1)
    wave = next(n for n in warm_wood.node_tree.nodes if n.type == "TEX_WAVE")
    wave.inputs["Scale"].default_value = 5     # broader, calmer grain than the war map's posts
    next(n for n in warm_wood.node_tree.nodes if n.type == "BUMP").inputs["Strength"].default_value = .1
    split_stand(bpy.data.objects[STAND], warm_wood, gold)
    for name in RINGS:
        me = bpy.data.objects[name].data
        me.materials.clear()
        me.materials.append(gold)
    sph = bpy.data.objects[SPHERE].data
    sph.materials.clear()
    sph.materials.append(globe(paint_stars(os.path.join(out, "armillary_star_map.png")), gold_c))
    # the war map beside it, as war_map.py draws it
    bpy.data.objects[war_map.OLD].hide_render = True
    war_map.build_board(war_map.paint_map(os.path.join(out, "war_map_parchment.png")))

    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.data.energy, sun.data.color, sun.data.angle = 3.5, (1.0, .86, .70), math.radians(2)
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.45, .48, .55, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .8
    sc.world = world
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.use_raytracing = True
    sc.eevee.taa_render_samples = 64
    sc.view_settings.view_transform = "AgX"
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    shots = (   # name, camera, target, lens, (sun tilt, turn), resolution
        ("armillary_front.png", (-6.9, -15.2, 2.6), (-8.98, -19.7, 2.0), 38, (50, -25), (1000, 1300)),
        ("armillary_close.png", (-8.1, -17.2, 3.1), (-8.98, -19.7, 2.75), 40, (50, -25), (1300, 1000)),
        ("armillary_with_map.png", (-5.6, -12.0, 2.9), (-6.6, -19.7, 2.1), 34, (50, -25), (1600, 1000)),
    )
    for name, at, target, lens, (tilt, turn), (rx, ry) in shots:
        cam.location = Vector(at)
        cam.data.lens = lens
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        sun.rotation_euler = (math.radians(tilt), 0, math.radians(turn))
        sc.render.resolution_x, sc.render.resolution_y = rx, ry
        sc.render.filepath = os.path.join(out, name)
        bpy.ops.render.render(write_still=True)
        print("WROTE", sc.render.filepath)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" not in argv:
        sys.exit("Preview only for now (not applied to Unity): -- --preview <folder>")
    preview(argv[argv.index("--preview") + 1])
