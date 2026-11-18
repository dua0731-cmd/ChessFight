# Renders the baked library (textures/library, rl_textures.py) as the concept's swatch row - eight rounded tiles lit from
# the upper left on white - and stacks it under the target row (textures/swatch_row_target.png) for a side-by-side check.
#
#   blender -b --factory-startup --python rl_swatch_compare.py -- [out.png]
#
# Look check only (EEVEE, Filmic-free Standard view so the colours compare 1:1 with the painted swatches).

import os, sys
import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
LIB = os.path.join(HERE, "textures", "library")
TARGET = os.path.join(HERE, "textures", "swatch_row_target.png")
ORDER = ["RL_CreamStone", "RL_Walnut", "RL_VarnishedWalnut", "RL_Brass", "RL_RedLeather", "RL_Parchment", "RL_InkGlass",
         "RL_CreamCloth"]
META = {"RL_Brass": .95}
VIEW = .35   # metres of material a tile shows (the swatches are close-ups)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(HERE, "textures", "swatch_compare.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
try:
    sc.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    sc.render.engine = "BLENDER_EEVEE"
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x, sc.render.resolution_y = 1644, 190
sc.render.film_transparent = False
world = bpy.data.worlds.new("w"); sc.world = world
world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (1, 1, 1, 1); bg.inputs["Strength"].default_value = .8


def material(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    alb = nt.nodes.new("ShaderNodeTexImage")
    alb.image = bpy.data.images.load(os.path.join(LIB, name + "_Albedo.png"))
    alb.image.alpha_mode = "CHANNEL_PACKED"
    nrm = nt.nodes.new("ShaderNodeTexImage")
    nrm.image = bpy.data.images.load(os.path.join(LIB, name + "_Normal.png")); nrm.image.colorspace_settings.name = "Non-Color"
    tc = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (VIEW, VIEW, VIEW)
    nt.links.new(tc.outputs["Generated"], mp.inputs["Vector"])
    for t in (alb, nrm): nt.links.new(mp.outputs["Vector"], t.inputs["Vector"])
    nt.links.new(alb.outputs["Color"], bsdf.inputs["Base Color"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1
    nt.links.new(alb.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(nrm.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Metallic"].default_value = META.get(name, 0.0)
    return m


for i, name in enumerate(ORDER):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(i * 1.24, 0, 0))
    o = bpy.context.object; o.scale = (1, 1, .16)
    bev = o.modifiers.new("b", "BEVEL"); bev.width = .07; bev.segments = 6; bev.limit_method = "NONE"
    bpy.ops.object.shade_smooth()
    o.data.materials.append(material(name))

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
cam.data.type = "ORTHO"; cam.data.ortho_scale = 1.24 * 8 + .02
cam.location = (1.24 * 3.5, 0, 10); sc.camera = cam
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA")); sc.collection.objects.link(key)
key.data.energy = 1100; key.data.size = 3; key.location = (-2.5, 3, 5)
key.rotation_euler = (-0.5, -0.35, 0)
sc.render.filepath = OUT + ".render.png"
bpy.ops.render.render(write_still=True)


def read(path):
    img = bpy.data.images.load(path); w, h = img.size
    a = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


ren = read(OUT + ".render.png")
tgt = read(TARGET)
W = ren.shape[1]
ys = (np.arange(int(tgt.shape[0] * W / tgt.shape[1])) * tgt.shape[0] / int(tgt.shape[0] * W / tgt.shape[1])).astype(int)
xs = (np.arange(W) * tgt.shape[1] / W).astype(int)
tgt2 = tgt[ys][:, xs]
tgt2[..., 3] = 1
gap = np.ones((12, W, 4), np.float32)
both = np.concatenate([ren, gap, tgt2], 0)   # rows bottom first: render below, target above
out = bpy.data.images.new("cmp", W, both.shape[0], alpha=True)
out.pixels.foreach_set(both.ravel()); out.filepath_raw = OUT; out.file_format = "PNG"; out.save()
os.remove(OUT + ".render.png")
print("[rl_swatch_compare] wrote", OUT)
