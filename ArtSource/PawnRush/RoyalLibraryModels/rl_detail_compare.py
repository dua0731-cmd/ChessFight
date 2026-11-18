# Close-up check of the detail maps (rl_textures.py DETAIL): each material as a 40 cm patch under a low sun, left the
# base map alone, right base + detail (albedo x2 like URP's _DETAIL_MULX2, detail height as a bump over the base normal).
#
#   blender -b --factory-startup --python rl_detail_compare.py -- [out.png]

import json, os, sys
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
LIB = os.path.join(HERE, "textures", "library")
INFO = json.load(open(os.path.join(LIB, "rl_textures.json"), encoding="utf-8"))
NAMES = [n for n in ["RL_CreamStone", "RL_Walnut", "RL_VarnishedWalnut", "RL_Brass", "RL_RedLeather", "RL_Parchment",
                     "RL_CreamCloth"] if "detail" in INFO.get(n, {})]
VIEW = .4   # metres a patch shows
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(HERE, "textures", "detail_compare.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
try:
    sc.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    sc.render.engine = "BLENDER_EEVEE"
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x, sc.render.resolution_y = 2 * 360 + 20, len(NAMES) * 360 + (len(NAMES) - 1) * 20
w = bpy.data.worlds.new("w"); sc.world = w
w.use_nodes = True
bgn = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
bgn.inputs["Color"].default_value = (1, 1, 1, 1); bgn.inputs["Strength"].default_value = .35


def img(path, data=False):
    i = bpy.data.images.load(path, check_existing=True)
    if data: i.colorspace_settings.name = "Non-Color"
    return i


def material(name, detail):
    spec = INFO[name]
    m = bpy.data.materials.new(name + ("_d" if detail else "")); m.use_nodes = True
    nt = m.node_tree; L = nt.links; bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tc = nt.nodes.new("ShaderNodeTexCoord")

    def tex(path, scale, data):
        mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (scale, scale, scale)
        L.new(tc.outputs["Generated"], mp.inputs["Vector"])
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = img(path, data); L.new(mp.outputs["Vector"], t.inputs["Vector"])
        return t

    base = tex(os.path.join(LIB, name + "_Albedo.png"), VIEW / spec["tile"], False)
    base.image.alpha_mode = "CHANNEL_PACKED"
    nrm = tex(os.path.join(LIB, name + "_Normal.png"), VIEW / spec["tile"], True)
    nm = nt.nodes.new("ShaderNodeNormalMap"); L.new(nrm.outputs["Color"], nm.inputs["Color"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1
    L.new(base.outputs["Alpha"], inv.inputs[1]); L.new(inv.outputs[0], bsdf.inputs["Roughness"])
    bsdf.inputs["Metallic"].default_value = spec["metallic"]
    if detail:
        d = spec["detail"]; k = VIEW / d["tile"]
        da = tex(os.path.join(LIB, name + "_DetailAlbedo.png"), k, True)
        x2 = nt.nodes.new("ShaderNodeMix"); x2.data_type = "RGBA"; x2.blend_type = "MULTIPLY"; x2.inputs["Factor"].default_value = 1
        two = nt.nodes.new("ShaderNodeMath"); two.operation = "MULTIPLY"; two.inputs[1].default_value = 2
        L.new(da.outputs["Color"], two.inputs[0])
        L.new(base.outputs["Color"], x2.inputs[6]); L.new(two.outputs[0], x2.inputs[7])
        L.new(x2.outputs[2], bsdf.inputs["Base Color"])
        bump = nt.nodes.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = 1
        bump.inputs["Distance"].default_value = .002 * d["normal_strength"]
        L.new(da.outputs["Color"], bump.inputs["Height"]); L.new(nm.outputs["Normal"], bump.inputs["Normal"])
        L.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    else:
        L.new(base.outputs["Color"], bsdf.inputs["Base Color"]); L.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return m


for r, name in enumerate(NAMES):
    for c in (0, 1):
        bpy.ops.mesh.primitive_plane_add(size=1, location=(c * 1.0556, -r * 1.0556, 0))
        bpy.context.object.data.materials.append(material(name, c == 1))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
cam.data.type = "ORTHO"; cam.data.ortho_scale = len(NAMES) * 1.0556 - .0556
cam.location = (.5278, -(len(NAMES) - 1) * 1.0556 / 2, 5); sc.camera = cam
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 3.2; sun.rotation_euler = (1.05, 0, 2.4)   # low, from the upper left: relief shows
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("[rl_detail_compare] wrote", OUT, NAMES)
