"""Grand banquet hall stills (B0, seoungtest R837): the white studio the sheets were drawn in, framing, a render of
some objects only, and the comparison picture (the R836 sheet's cell beside the render) every part is checked with.

    import bh_stills as st; cam = st.setup_render(scene); st.frame(cam, objs, yaw, pitch, lens); st.render_only(...)
    st.compare(sheet_png, (x0, y0, x1, y1), render_png, out_png)
"""
import math, os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SHEETS = os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R836_BanquetHallAssets", "AssetRefs")


def setup_render(scene, res=900):
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 96
    scene.cycles.use_denoising = True
    try:
        scene.cycles.device = "GPU"
    except TypeError:
        pass
    scene.render.resolution_x = scene.render.resolution_y = res
    scene.render.film_transparent = False
    w = scene.world or bpy.data.worlds.new("W")
    scene.world = w
    w.use_nodes = True
    # a studio: the camera sees white (as the sheet's paper), reflections see a soft light top and a darker floor
    nt = w.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_WORLD":
            nt.nodes.remove(n)
    out = next(n for n in nt.nodes if n.type == "OUTPUT_WORLD")
    lp, mix, white = nt.nodes.new("ShaderNodeLightPath"), nt.nodes.new("ShaderNodeMixShader"), nt.nodes.new("ShaderNodeBackground")
    white.inputs[0].default_value, white.inputs[1].default_value = (1, 1, 1, 1), 1.6
    studio, tc, sep, ramp = (nt.nodes.new("ShaderNodeBackground"), nt.nodes.new("ShaderNodeTexCoord"),
                             nt.nodes.new("ShaderNodeSeparateXYZ"), nt.nodes.new("ShaderNodeValToRGB"))
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs["Z"], ramp.inputs[0])
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = .42, (.30, .26, .22, 1)
    ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = .62, (1.0, .97, .92, 1)
    nt.links.new(ramp.outputs["Color"], studio.inputs[0])
    studio.inputs[1].default_value = 2.0
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(studio.outputs[0], mix.inputs[1])
    nt.links.new(white.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    scene.view_settings.exposure = -.55   # the white china and linen keep their shading; the paper stays white (below)
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError as e:
        print("[bh_stills] view transform:", e)
    for o in [o for o in scene.objects if o.type in ("LIGHT", "CAMERA")]:
        bpy.data.objects.remove(o)
    for name, rot, energy, size in (("key", (math.radians(50), 0, math.radians(-35)), 4.0, 8.0),
                                    ("fill", (math.radians(60), 0, math.radians(120)), 1.5, 15.0)):
        ld = bpy.data.lights.new(name, "SUN")
        ld.energy = energy
        ld.angle = math.radians(size)
        ld.color = (1.0, .95, .88) if name == "key" else (.9, .94, 1.0)
        lo = bpy.data.objects.new(name, ld)
        lo.rotation_euler = rot
        scene.collection.objects.link(lo)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def frame(cam, objs, yaw, pitch, lens=50, pad=1.12):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    centre = (lo + hi) / 2
    radius = (hi - lo).length / 2 * pad
    cam.data.lens = lens
    fov = 2 * math.atan(18 / lens)
    dist = radius / math.sin(fov / 2)
    d = Vector((math.cos(pitch) * math.sin(yaw), -math.cos(pitch) * math.cos(yaw), math.sin(pitch)))
    cam.location = centre + d * dist
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = dist * 4


def render_only(scene, objs, path):
    keep = set(objs)
    for o in scene.objects:
        if o.type == "MESH":
            o.hide_render = o not in keep
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def compare(sheet_path, box, render_path, out):
    """The sheet's cell (box = x0, y0, x1, y1 in its pixels) beside the render, same height, white between."""
    import numpy as np
    sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels"))
    import rcy_textures as rt
    sheet = rt.load_png(sheet_path)[..., :3]
    x0, y0, x1, y1 = box
    a = sheet[y0:y1, x0:x1]
    b = rt.load_png(render_path)[..., :3]
    hgt = 700
    def fit(img):
        h, w = img.shape[:2]
        return rt.resample(img, 1) if False else _resize(img, hgt, int(w * hgt / h))
    a, b = fit(a), fit(b)
    gap = np.ones((hgt, 30, 3), np.float32)
    img = np.concatenate([a, gap, b], 1)
    img = np.concatenate([img, np.ones(img.shape[:2] + (1,), np.float32)], -1)
    rt.save_png(img.astype(np.float32), out)


def _resize(a, h, w):
    import numpy as np
    H, W = a.shape[:2]
    ys = np.clip(((np.arange(h) + .5) * H / h).astype(int), 0, H - 1)
    xs = np.clip(((np.arange(w) + .5) * W / w).astype(int), 0, W - 1)
    return a[ys][:, xs].astype(np.float32)
