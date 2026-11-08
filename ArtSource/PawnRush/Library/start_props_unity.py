"""Pawn Rush library (작전 회의실): put the start's compass rose, war map and armillary looks into the Unity FBX (R364).

    blender --background --factory-startup --python start_props_unity.py
    blender --background --factory-startup --python start_props_unity.py -- --check D:/check   (also renders the bakes)

승규 님 approved the three Blender looks (compass_rose.py R359, war_map.py R360, armillary_look.py R361) with
"Unity에 적용해". This script edits PawnRushLibraryEnvironment.fbx in place (as armillary.py does):

    Start_compass_rose    new, on START_spawn_zone; the old flat Upper20_Compass_star / _Start_compass_ring deleted
    War_map_frame         new, the rebuilt board's posts, rails, caps, bands, studs, bolts and nodes in one mesh
    War_map_parchment     new, the two flat map faces (their UVs read each face the right way round)
                          V21_Original_treasure_map_clay (the lumpy sculpt) deleted
    Start_armillary|...   the table + stand mesh, one material; the rings get "Armillary gold", the globe its own

Unity can't run Blender's procedural shaders, so every look is baked (Cycles, through emission) into textures in
Assets/Resources/PawnRushLibrary/StartProps: <name>.png (base colour, sRGB) and <name>_MS.png (metallic in RGB,
smoothness in A, linear), the frame, stand and globe on a smart UV unwrap, the compass on a flat top projection.
The parchment keeps its painted texture. Course01v4Builder.LibraryStartProps.cs builds the materials by node name.
Running it again on the edited FBX stops at once (the compass rose is already there).
"""
import math
import os
import sys

import bpy
import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import armillary_look  # noqa: E402
import compass_rose  # noqa: E402
import war_map  # noqa: E402
from export_library_fbx import SETTINGS  # noqa: E402

FBX = war_map.FBX
OUT = os.path.normpath(os.path.join(os.path.dirname(FBX), "StartProps"))


# ------------------------------------------------------------------ helpers

def into(col, obs):
    for ob in obs:
        for c in list(ob.users_collection):
            c.objects.unlink(ob)
        col.objects.link(ob)


def activate(ob):
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    ob.select_set(True)
    view.objects.active = ob


def fresh_uv(ob):
    me = ob.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    return me.uv_layers.new(name="UVMap")


def smart_uv(ob):
    fresh_uv(ob)
    activate(ob)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.004, area_weight=.6)
    bpy.ops.object.mode_set(mode="OBJECT")


def top_uv(ob, radius):
    uv = fresh_uv(ob)
    for loop in ob.data.loops:
        co = ob.data.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (.5 + co.x / (2 * radius), .5 + co.y / (2 * radius))


def single_material(ob, name):
    me = ob.data
    m = bpy.data.materials.new(name)
    me.materials.clear()
    me.materials.append(m)
    for p in me.polygons:
        p.material_index = 0
    return m


# ------------------------------------------------------------------ baking (Cycles, every channel through emission)

def bake(ob, name, size):
    """Bakes base colour, metallic and roughness of every material on ob into <name>.png and <name>_MS.png."""
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 1
    sc.render.bake.margin = 6
    sc.render.bake.use_clear = True
    mats = [m for m in ob.data.materials if m is not None]
    results = {}
    for channel, colour in (("Base Color", "sRGB"), ("Metallic", "Non-Color"), ("Roughness", "Non-Color")):
        img = bpy.data.images.new(f"{name}_{channel}", size, size, alpha=False)
        img.colorspace_settings.name = colour
        undo = []
        for m in mats:
            nt = m.node_tree
            bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
            out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output)
            surface = out.inputs["Surface"].links[0].from_socket
            em = nt.nodes.new("ShaderNodeEmission")
            src = bsdf.inputs[channel]
            if src.is_linked:
                nt.links.new(src.links[0].from_socket, em.inputs["Color"])
            else:
                v = src.default_value
                em.inputs["Color"].default_value = tuple(v) if channel == "Base Color" else (v, v, v, 1)
            nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            nt.nodes.active = tex
            undo.append((nt, em, tex, surface, out))
        activate(ob)
        bpy.ops.object.bake(type="EMIT")
        for nt, em, tex, surface, out in undo:
            nt.nodes.remove(em)
            nt.nodes.remove(tex)
            nt.links.new(surface, out.inputs["Surface"])
        results[channel] = np.array(img.pixels[:]).reshape(size, size, 4)
        if channel == "Base Color":
            img.filepath_raw = os.path.join(OUT, name + ".png")
            img.file_format = "PNG"
            img.save()
    ms = np.ones((size, size, 4), dtype=np.float32)
    ms[..., :3] = results["Metallic"][..., :1]
    ms[..., 3] = 1 - results["Roughness"][..., 0]
    img = bpy.data.images.new(name + "_MS", size, size, alpha=True)
    img.colorspace_settings.name = "Non-Color"
    img.alpha_mode = "STRAIGHT"
    img.pixels.foreach_set(ms.ravel())
    img.filepath_raw = os.path.join(OUT, name + "_MS.png")
    img.file_format = "PNG"
    img.save()
    print(f"BAKED {name}: {size}px, metallic mean {ms[..., 0].mean():.2f}, smoothness mean {ms[..., 3].mean():.2f}")


def baked_look(m, name):
    """The material as Unity will draw it, for the check renders."""
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    col = nt.nodes.new("ShaderNodeTexImage")
    col.image = bpy.data.images.load(os.path.join(OUT, name + ".png"))
    nt.links.new(col.outputs["Color"], b.inputs["Base Color"])
    ms = nt.nodes.new("ShaderNodeTexImage")
    ms.image = bpy.data.images.load(os.path.join(OUT, name + "_MS.png"))
    ms.image.colorspace_settings.name = "Non-Color"
    nt.links.new(ms.outputs["Color"], b.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1
    nt.links.new(ms.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs["Value"], b.inputs["Roughness"])


# ------------------------------------------------------------------ the three props

def compass(col):
    mats = (compass_rose.patina("Compass verdigris"),
            compass_rose.metal("Compass bronze band", (.70, .46, .18), (.56, .35, .12), .34),
            compass_rose.metal("Compass gold", (1.0, .72, .28), (.86, .56, .18), .24),
            compass_rose.metal("Compass bronze", (.52, .29, .09), (.38, .20, .06), .32))
    for name in compass_rose.HIDE:
        bpy.data.objects.remove(bpy.data.objects[name])
    rose = compass_rose.build_rose(mats)
    into(col, [rose])
    top_uv(rose, compass_rose.DISC_R)
    bake(rose, "StartCompass", 2048)
    baked_look(single_material(rose, "Start compass rose"), "StartCompass")


def board(col):
    bpy.data.objects.remove(bpy.data.objects[war_map.OLD])
    os.makedirs(OUT, exist_ok=True)
    img = war_map.paint_map(os.path.join(OUT, "WarMapParchment.png"))
    obs = war_map.build_board(img)
    into(col, obs)
    parch = next(o for o in obs if o.name == "War_map_parchment")
    frame = [o for o in obs if o is not parch]
    for o in frame:
        activate(o)
        for mod in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    activate(frame[0])
    for o in frame:
        o.select_set(True)
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = joined.data.name = "War_map_frame"
    smart_uv(joined)
    bake(joined, "WarMapFrame", 2048)
    baked_look(single_material(joined, "War map frame"), "WarMapFrame")
    print("WAR MAP frame faces", len(joined.data.polygons), "parchment faces", len(parch.data.polygons))


def armillary():
    gold_c = (1.0, .62, .20)
    gold = war_map.metal("Armillary gold", gold_c, (.80, .46, .12), .26, 6)
    wood = war_map.wood("Armillary table wood", "Z")
    ramp = next(n for n in wood.node_tree.nodes if n.type == "VALTORGB")
    ramp.color_ramp.elements[0].color = (.16, .07, .025, 1)
    ramp.color_ramp.elements[1].color = (.42, .20, .07, 1)
    next(n for n in wood.node_tree.nodes if n.type == "TEX_WAVE").inputs["Scale"].default_value = 5
    next(n for n in wood.node_tree.nodes if n.type == "BUMP").inputs["Strength"].default_value = .1
    stand = bpy.data.objects[armillary_look.STAND]
    armillary_look.split_stand(stand, wood, gold)
    smart_uv(stand)
    bake(stand, "ArmillaryStand", 2048)
    baked_look(single_material(stand, "Armillary stand"), "ArmillaryStand")
    for name in armillary_look.RINGS:   # plain gold in Unity (Course01v4Builder.LibraryStartProps)
        me = bpy.data.objects[name].data
        me.materials.clear()
        me.materials.append(gold)
    sphere = bpy.data.objects[armillary_look.SPHERE]
    stars = armillary_look.paint_stars(os.path.join(bpy.app.tempdir, "armillary_star_map.png"))
    sphere.data.materials.clear()
    sphere.data.materials.append(armillary_look.globe(stars, gold_c))
    smart_uv(sphere)
    bake(sphere, "ArmillaryGlobe", 2048)
    baked_look(single_material(sphere, "Armillary globe"), "ArmillaryGlobe")


# ------------------------------------------------------------------ main

def check(out):
    """Renders the edited start with the baked textures (what Unity gets)."""
    os.makedirs(out, exist_ok=True)
    sc = bpy.context.scene
    stone = bpy.data.materials.get("CLAY | uniform neutral gray")
    b = stone.node_tree.nodes["Principled BSDF"]
    keep = tuple(b.inputs["Base Color"].default_value)
    b.inputs["Base Color"].default_value = (.70, .58, .47, 1)
    from mathutils import Vector
    for o in bpy.data.objects:   # the hall's lights, roof and upper walls only shadow a still render
        if o.type == "LIGHT" or (o.type == "MESH" and min((o.matrix_world @ Vector(c)).z for c in o.bound_box) > 4.2):
            o.hide_render = True
    sun =bpy.data.objects.new("Check sun", bpy.data.lights.new("Check sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.data.energy, sun.data.color = 3.5, (1.0, .86, .70)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-25))
    world = bpy.data.worlds.new("Check world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.45, .48, .55, 1)
    sc.world = world
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.taa_render_samples = 32
    sc.view_settings.view_transform = "AgX"
    cam = bpy.data.objects.new("Check cam", bpy.data.cameras.new("Check cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    for name, at, target, lens in (("baked_start.png", (-4.2, -11.0, 3.4), (-5.2, -18.6, 1.2), 30),
                                   ("baked_compass.png", (1.6, -14.0, 2.1), (0, -16.5, .2), 45),
                                   ("baked_armillary.png", (-6.9, -15.2, 2.6), (-8.98, -19.7, 2.0), 38)):
        cam.location = Vector(at)
        cam.data.lens = lens
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out, name)
        bpy.ops.render.render(write_still=True)
        print("WROTE", sc.render.filepath)
    b.inputs["Base Color"].default_value = keep


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    if bpy.data.objects.get("Start_compass_rose") is not None:
        print("Start props: already in the FBX")
        return
    os.makedirs(OUT, exist_ok=True)
    before = len(col.all_objects)
    compass(col)
    board(col)
    armillary()
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {before} -> {len(col.all_objects)} objects -> {FBX}")
    if "--check" in argv:
        check(argv[argv.index("--check") + 1])


main()
