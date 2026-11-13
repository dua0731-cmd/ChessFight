# Renders one canal fountain's water through a whole cycle from jet_math.py (the same model as GardenJet.cs, R426),
# so the motion can be judged before Unity.
#
#   blender -b --python jet_preview.py -- sheet   ten moments side by side      -> render_jet_v2_sheet.png
#   blender -b --python jet_preview.py -- video   every frame (30 fps) and MP4  -> jet_frames/####.png, render_jet_v2.mp4
# (after canal_sample_v2.py has written CanalSample_v2.blend)
#
# The material stands in for GC_jet (the garden falls' shader): clear blue where the water is whole, white where it
# holds air (vertex data 1), streaks riding on the water (data 2 = the parcel's flow coordinate), and the umbrella
# (data 4 = 0) a thin curtain that breaks into strands as it falls.

import os
import sys
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import jet_math as jm  # noqa: E402

MODE = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "sheet"
bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "CanalSample_v2.blend"))
sc = bpy.context.scene
FPS = 30
GX, GZ, ORDER = -8.0, -14.0, 6          # the second fountain along the walk (flat, with a basin)
FRAMES = int(round(jm.END * FPS)) + 1
OUT = os.path.join(HERE, "jet_frames")
os.makedirs(OUT, exist_ok=True)
MOMENTS = [.5, .85, .95, 1.1, 1.35, 1.65, 2.0, 2.45, 3.0, 3.6]   # s, seconds since the geyser began to warn


def srgb(c):
    return tuple((v / 12.92) if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


def node(kind, **inputs):
    n = nt.nodes.new(kind)
    for k, v in inputs.items():
        n.inputs[k].default_value = v
    return n


mat = bpy.data.materials.new("Jet water (preview)")
mat.use_nodes = True
nt = mat.node_tree
p = nt.nodes["Principled BSDF"]
attr = nt.nodes.new("ShaderNodeAttribute"); attr.attribute_name = "jet"
sep = nt.nodes.new("ShaderNodeSeparateColor")
nt.links.new(attr.outputs["Color"], sep.inputs["Color"])
# Noise riding on the water: (round, along the flow).
mu = node("ShaderNodeMath"); mu.operation = "MULTIPLY"; mu.inputs[1].default_value = 14.0
mf = node("ShaderNodeMath"); mf.operation = "MULTIPLY"; mf.inputs[1].default_value = 1.6
nt.links.new(sep.outputs["Blue"], mu.inputs[0]); nt.links.new(sep.outputs["Green"], mf.inputs[0])
uvw = nt.nodes.new("ShaderNodeCombineXYZ")
nt.links.new(mu.outputs[0], uvw.inputs["X"]); nt.links.new(mf.outputs[0], uvw.inputs["Y"])
noise = node("ShaderNodeTexNoise", Scale=1.0, Detail=4.0)
nt.links.new(uvw.outputs[0], noise.inputs["Vector"])
# White where aerated.
white = node("ShaderNodeMath"); white.operation = "MULTIPLY_ADD"; white.inputs[1].default_value = .55
nt.links.new(noise.outputs["Fac"], white.inputs[0]); nt.links.new(sep.outputs["Red"], white.inputs[2])
ramp = nt.nodes.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].position = .55; ramp.color_ramp.elements[0].color = (*srgb((.68, .88, 1.0)), 1)
ramp.color_ramp.elements[1].position = 1.05; ramp.color_ramp.elements[1].color = (*srgb((.97, .99, 1.0)), 1)
nt.links.new(white.outputs[0], ramp.inputs["Fac"])
nt.links.new(ramp.outputs["Color"], p.inputs["Base Color"])
# Alpha: whole water 0.92, the curtain 0.38 times its strands; both fade out once the air passes 0.94.
fade = node("ShaderNodeMapRange")
fade.inputs["From Min"].default_value = .94; fade.inputs["From Max"].default_value = 1.0
fade.inputs["To Min"].default_value = 1.0; fade.inputs["To Max"].default_value = 0.0
nt.links.new(sep.outputs["Red"], fade.inputs["Value"])
strands = node("ShaderNodeMapRange")
strands.inputs["From Min"].default_value = .45; strands.inputs["From Max"].default_value = .6
nt.links.new(noise.outputs["Fac"], strands.inputs["Value"])
curtain = node("ShaderNodeMath"); curtain.operation = "MULTIPLY"; curtain.inputs[1].default_value = .38
nt.links.new(strands.outputs["Result"], curtain.inputs[0])
body = node("ShaderNodeMix"); body.data_type = "FLOAT"
nt.links.new(attr.outputs["Alpha"], body.inputs["Factor"])
nt.links.new(curtain.outputs[0], body.inputs["A"])
body.inputs["B"].default_value = .92
alpha = node("ShaderNodeMath"); alpha.operation = "MULTIPLY"
nt.links.new(body.outputs["Result"], alpha.inputs[0]); nt.links.new(fade.outputs["Result"], alpha.inputs[1])
nt.links.new(alpha.outputs[0], p.inputs["Alpha"])
p.inputs["Roughness"].default_value = .12
p.inputs["Emission Color"].default_value = (*srgb((.9, .96, 1.0)), 1)
p.inputs["Emission Strength"].default_value = .08
for a, v in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
    try:
        setattr(mat, a, v)
    except Exception:
        pass
mat.use_backface_culling = False

jet = bpy.data.objects.new("Jet", bpy.data.meshes.new("Jet"))
jet.location = (GX, GZ, .02)  # the geyser group stands 2 cm over the floor
sc.collection.objects.link(jet)


def update(scene, *_):
    s = (scene.frame_current - 1) / FPS
    b = jm.build(s, ORDER, 0, True)
    me = bpy.data.meshes.new("Jet")
    if b.verts:
        me.from_pydata(b.verts, [], b.faces)
        col = me.color_attributes.new("jet", "FLOAT_COLOR", "POINT")
        for i, d in enumerate(b.data):
            col.data[i].color = d
        for poly in me.polygons:
            poly.use_smooth = True
        me.materials.append(mat)
    old = jet.data
    jet.data = me
    bpy.data.meshes.remove(old)


bpy.app.handlers.frame_change_pre.clear()
bpy.app.handlers.frame_change_pre.append(update)

cam = sc.camera
cam.location = Vector((.6, -20.0, 2.0))
cam.rotation_euler = (Vector((GX, GZ, 2.3)) - cam.location).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 22
sc.render.resolution_x, sc.render.resolution_y = 768, 432
try:
    sc.eevee.taa_render_samples = 8
except Exception:
    pass
sc.render.fps = FPS
sc.render.image_settings.file_format = "PNG"


def frame_of(s):
    return int(round(s * FPS)) + 1


if MODE == "sheet":
    tiles = []
    for s in MOMENTS:
        sc.frame_set(frame_of(s))
        path = os.path.join(OUT, "moment_%04d.png" % frame_of(s))
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path)
        w, h = img.size
        tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::2, ::2])
    rows = [np.concatenate(tiles[i:i + 5], axis=1) for i in (0, 5)]
    sheet = np.concatenate([rows[1], rows[0]], axis=0)  # pixel rows run bottom-up: the first five end on top
    out = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0], alpha=False)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = os.path.join(HERE, "render_jet_v2_sheet.png")
    out.file_format = "PNG"
    out.save()
    print("SHEET DONE")
else:
    sc.frame_start, sc.frame_end = 1, FRAMES
    sc.render.filepath = os.path.join(OUT, "")
    bpy.ops.render.render(animation=True)
    print("FRAMES DONE")
    try:
        vs = bpy.data.scenes.new("Video")
        vs.render.resolution_x, vs.render.resolution_y = 768, 432
        vs.render.fps = FPS
        ed = vs.sequence_editor_create()
        files = sorted(f for f in os.listdir(OUT) if f[0].isdigit() and f.endswith(".png"))
        coll = ed.strips if hasattr(ed, "strips") else ed.sequences
        strip = coll.new_image("frames", os.path.join(OUT, files[0]), 1, 1)
        for f in files[1:]:
            strip.elements.append(f)
        vs.frame_start, vs.frame_end = 1, len(files)
        try:
            vs.render.image_settings.media_type = "VIDEO"
        except Exception:
            pass
        vs.render.image_settings.file_format = "FFMPEG"
        vs.render.ffmpeg.format = "MPEG4"
        vs.render.ffmpeg.codec = "H264"
        vs.render.filepath = os.path.join(HERE, "render_jet_v2.mp4")
        with bpy.context.temp_override(scene=vs):
            bpy.ops.render.render(animation=True, scene=vs.name)
        print("VIDEO DONE")
    except Exception as e:  # noqa: BLE001
        print("VIDEO SKIPPED", e)
