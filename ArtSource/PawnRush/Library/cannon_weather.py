"""Pawn Rush library (작전 회의실): the hand-built cannons worn like their reference — not shiny gold, teal paint (R383).

    blender --background --factory-startup --python cannon_weather.py                       (writes FBX + maps)
    blender --background --factory-startup --python cannon_weather.py -- --preview D:/check (renders, writes nothing)

승규 님 (the reference again): "대포도 마찬가지로 완전 빛나는 골드 색이면 안돼 그리고 지금 조명 때문인지 모르겠으나 저 초록색
부분이 검은색으로 나와". In Unity the cannon was flat colours: a bright, mirror-like gold (metallic .9, smoothness .72)
and a teal at metallic .45 — a metal reflects its surroundings, and the dim hall has little to reflect, so the teal read
black. Like the conveyor (R376) the cannon body (one mesh shared by both cannons) now gets a smart UV unwrap and baked
maps: its part per texel (from its material slots: gold, teal, cream, steel, dark, grout — cannon_model.PARTS order),
position, normal and ambient occlusion, then cannon_conveyor_look.weather() with the cannon's palette — warm worn
gold (half metallic, satin), teal as paint (not metal), cream stone — grime in the joints and a little dust on top.
Maps: Assets/Resources/PawnRushLibrary/Cannon/LibraryCannon(.png, _MS.png); material "Library cannon worn"
(Course01v4Builder.LibraryLooks). The fans keep their flat parts (their Unity colours are muted there too).
Running it again stops at once.
"""
import os
import sys

import bpy
import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cannon_conveyor_look as look  # noqa: E402

BODY = "BLOWER_A_left|Blower_GameReady_Arrowless|Dupli|"
OUT = os.path.join(look.LIB, "Cannon")
MATERIAL = "Library cannon worn"
# by slot: gold, teal, cream, steel, dark, grout (linear colour, metallic, smoothness)
PALETTE = [
    ((.52, .33, .085), .6, .42),     # worn warm gold: not mirror-bright, not yellow
    ((.072, .165, .185), .05, .34),  # teal paint
    ((.64, .58, .49), 0, .2),        # cream stone
    ((.06, .065, .075), .55, .4),    # steel (the duct)
    ((.03, .03, .035), .2, .3),      # dark
    ((.20, .17, .14), 0, .1),        # grout
]


def bake_slots(ob, size):
    """Which material slot each texel belongs to (an emission code per slot, baked)."""
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 1
    sc.render.bake.use_clear = False
    sc.render.bake.margin = 4
    keep = list(ob.data.materials)
    img = bpy.data.images.new("slot code", size, size, alpha=True, float_buffer=True)
    img.colorspace_settings.name = "Non-Color"
    img.generated_color = (0, 0, 0, 0)
    temp = []
    for i in range(len(keep)):
        m = bpy.data.materials.new(f"slot {i}")
        m.use_nodes = True
        nt = m.node_tree
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = ((i + .5) / 10, 0, 0, 1)
        nt.links.new(em.outputs["Emission"], nt.nodes["Material Output"].inputs["Surface"])
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.nodes.active = tex
        ob.data.materials[i] = m
        temp.append(m)
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    ob.select_set(True)
    view.objects.active = ob
    bpy.ops.object.bake(type="EMIT")
    for i, m in enumerate(keep):
        ob.data.materials[i] = m
    px = np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)
    return np.floor(px[..., 0] * 10).astype(int)


def rebuild_body():
    """Both cannons' body afresh from cannon_model (its rim no longer lies on the duct), bevelled and cleaned as
    cannon_conveyor_unity.py did (no zero-area faces: Unity reads their vertices as NaNs)."""
    import bmesh
    import cannon_model
    mesh = cannon_model.build_body()
    tmp = bpy.data.objects.new("bevel tmp", mesh)
    bpy.context.scene.collection.objects.link(tmp)
    cannon_model.bevel(tmp, .012)
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    tmp.select_set(True)
    view.objects.active = tmp
    bpy.ops.object.modifier_apply(modifier="Bevel")
    bpy.data.objects.remove(tmp)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_area() < 1e-9], context="FACES_ONLY")
    bm.to_mesh(mesh)
    bm.free()
    old = set()
    for o in list(bpy.data.objects):
        if o.type == "MESH" and o.name.startswith("BLOWER_") and not o.name.endswith("_Fan"):
            old.add(o.data)
            o.data = mesh
    for m in old:
        if m.users == 0:
            bpy.data.meshes.remove(m)
    mesh.name = "Library cannon"


def dress(out, size=2048):
    ob = bpy.data.objects[BODY]
    if ob.data.materials and ob.data.materials[0] is not None and ob.data.materials[0].name.startswith(MATERIAL):
        print("cannon: already worn")
        return False
    rebuild_body()
    ob = bpy.data.objects[BODY]
    look.smart_uv(ob)
    part = bake_slots(ob, size)
    pw, n, ao, _, valid = look.bake_set([ob], size)
    inv = np.array(ob.matrix_world.inverted())
    p = pw @ inv[:3, :3].T + inv[:3, 3]
    os.makedirs(out, exist_ok=True)
    look.weather(os.path.join(out, "LibraryCannon"), np.clip(part, 0, len(PALETTE) - 1), valid, p, n, ao,
                 PALETTE, dust_amount=.7)
    mat = look.mapped(MATERIAL, os.path.join(out, "LibraryCannon"))
    look.assign(ob.data, mat)
    return True


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=look.ENV, use_custom_normals=True, use_anim=True)
    return col


def render(out):
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_backface_culling = True
    fan_colours = {"Library gold": PALETTE[0], "Library steel": PALETTE[3], "Library dark": PALETTE[4]}
    for name, (c, mt, sm) in fan_colours.items():
        m = bpy.data.materials.get(name)
        if m is not None and m.use_nodes:
            b = m.node_tree.nodes["Principled BSDF"]
            b.inputs["Base Color"].default_value = (*c, 1)
            b.inputs["Metallic"].default_value = mt
            b.inputs["Roughness"].default_value = 1 - sm
    for o in bpy.data.objects:
        if o.type == "LIGHT" or (o.type == "MESH" and "BLOWER" not in o.name
                                 and not o.name.startswith("GEO_fitted_stone")):
            o.hide_render = True
    sun, cam = look.stage(sc)
    look.shoot(sc, sun, cam, out, (
        ("cannon_worn.png", (-6.0, -9.2, 2.6), (-10.9, -4.0, 1.25), 40, 40),
        ("cannon_worn_side.png", (-11.0, -9.9, 1.9), (-10.9, -4.0, 1.2), 35, 10),
        ("cannon_worn_top.png", (-13.5, -6.5, 4.4), (-10.6, -4.0, 1.4), 35, 60),
        ("cannon_worn_muzzle.png", (-7.0, -4.8, 1.8), (-10.2, -4.0, 1.5), 40, 80),
    ))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    col = load()
    if "--preview" in argv:
        out = argv[argv.index("--preview") + 1]
        dress(out)
        render(out)
        return
    from export_library_fbx import SETTINGS
    if not dress(OUT):
        return
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=look.ENV, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {look.ENV}")


main()
