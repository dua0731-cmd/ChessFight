"""Look check of an exported Royal Library FBX in the baked library materials (R0, 11-18).

    blender -b --factory-startup --python rl_lookdev.py -- <fbx> <out.png> [view x,y,z] [ortho scale] [neutral]

("neutral": white sky and sun, Standard view - to compare colours with a concept sheet; default the warm afternoon)

Imports the FBX (as Unity would read it), draws every "RL | <slot>" in its library material the way URP Lit does
(albedo, smoothness from alpha, metallic, normal map, the detail map x2 and its normal; tiling = 1 / tile in metres on
the model's metre UVs) and renders a warm afternoon key light. A preview of the look, not Unity's renderer.
"""
import json, math, os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
LIB = os.path.join(HERE, "textures", "library")
INFO = json.load(open(os.path.join(LIB, "rl_textures.json"), encoding="utf-8"))
SLOT = {"stone": "RL_CreamStone", "walnut": "RL_Walnut", "varnished": "RL_VarnishedWalnut", "brass": "RL_Brass",
        "leather": "RL_RedLeather", "parchment": "RL_Parchment", "ink": "RL_InkGlass", "cloth": "RL_CreamCloth",
        "wax": "RL_RedWax", "marble": "RL_Marble", "ivory": "RL_Ivory", "ebony": "RL_Ebony", "glass": "RL_Glass", "painting": "RL_Portrait", "tea": "RL_Tea", "veil": "RL_Veil",
        "leaf": "RL_Leaf", "leaf_light": "RL_LeafLight", "stem": "RL_Stem", "soil": "RL_Soil", "petal_cream": "RL_PetalCream", "petal_pink": "RL_PetalPink", "ceramic": "RL_Ceramic", "pollen": "RL_Pollen", "bark": "RL_Bark", "leather_brown": "RL_LeatherBrown", "leather_dark": "RL_LeatherDark",
        "leather_tan": "RL_LeatherTan", "leather_slate": "RL_LeatherSlate"}


def glass_material(name):
    """RL_Glass is transparent with no textures in Unity (rl_unity_materials GLASS): clear, a little warm, glossy."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (.62, .78, .82, 1) if name == "RL_Veil" else (1.0, .97, .90, 1)
    p.inputs["Roughness"].default_value = .1 if name == "RL_Veil" else .03
    for s in ("Transmission Weight", "Transmission"):
        if s in p.inputs:
            p.inputs[s].default_value = 1.0
    return m


def library_material(name):
    if name in ("RL_Glass", "RL_Veil"):
        return glass_material(name)
    spec = INFO[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt, L = m.node_tree, m.node_tree.links
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    uv = nt.nodes.new("ShaderNodeUVMap")

    def tex(file, scale, data):
        mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (scale, scale, 1)
        L.new(uv.outputs["UV"], mp.inputs["Vector"])
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(os.path.join(LIB, file), check_existing=True)
        if data:
            t.image.colorspace_settings.name = "Non-Color"
        L.new(mp.outputs["Vector"], t.inputs["Vector"])
        return t
    base = tex(name + "_Albedo.png", 1 / spec["tile"], False); base.image.alpha_mode = "CHANNEL_PACKED"
    src = spec.get("share", name)   # a colour variant (rl_textures TINTS) uses its source's normal and detail maps
    nrm = tex(src + "_Normal.png", 1 / spec["tile"], True)
    nm = nt.nodes.new("ShaderNodeNormalMap"); L.new(nrm.outputs["Color"], nm.inputs["Color"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1
    L.new(base.outputs["Alpha"], inv.inputs[1]); L.new(inv.outputs[0], bsdf.inputs["Roughness"])
    bsdf.inputs["Metallic"].default_value = spec["metallic"]
    col, normal = base.outputs["Color"], nm.outputs["Normal"]
    if "detail" in spec:
        d = spec["detail"]
        da = tex(src + "_DetailAlbedo.png", 1 / d["tile"], True)
        two = nt.nodes.new("ShaderNodeMath"); two.operation = "MULTIPLY"; two.inputs[1].default_value = 2
        L.new(da.outputs["Color"], two.inputs[0])
        mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1
        L.new(col, mul.inputs[6]); L.new(two.outputs[0], mul.inputs[7]); col = mul.outputs[2]
        bump = nt.nodes.new("ShaderNodeBump"); bump.inputs["Distance"].default_value = .002 * d["normal_strength"]
        L.new(da.outputs["Color"], bump.inputs["Height"]); L.new(normal, bump.inputs["Normal"]); normal = bump.outputs["Normal"]
    L.new(col, bsdf.inputs["Base Color"]); L.new(normal, bsdf.inputs["Normal"])
    return m


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    fbx, out = argv[0], argv[1]
    view = Vector(tuple(float(v) for v in argv[2].split(","))) if len(argv) > 2 else Vector((-.7, -1, .55))
    scale = float(argv[3]) if len(argv) > 3 else 0
    neutral = "neutral" in argv[4:]   # R1 11-18: white light and Standard view, to compare colours with a sheet
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    cache = {}
    objs = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in objs:
        for s in o.material_slots:
            n = s.material.name if s.material else ""
            key = n[2:].lstrip(" _|").strip().lower() if n.startswith("RL") else ""
            if key in SLOT:
                cache.setdefault(key, library_material(SLOT[key]))
                s.material = cache[key]
    lo = Vector((min(min((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in objs) for i in range(3)))
    hi = Vector((max(max((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in objs) for i in range(3)))
    centre, size = (lo + hi) / 2, (hi - lo).length
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "Standard" if neutral else "AgX"
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1000
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (1, 1, 1, 1) if neutral else (.95, .82, .66, 1)
    bg.inputs["Strength"].default_value = .8 if neutral else .7
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.5 if neutral else 4.5
    sun.data.color = (1, 1, 1) if neutral else (1.0, .86, .68); sun.data.angle = math.radians(3)
    sun.rotation_euler = (math.radians(52), 0, math.radians(-40))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = scale or size * 1.05
    cam.location = centre + view.normalized() * size * 3
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = size * 10
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print("[rl_lookdev] wrote", out)


main()
