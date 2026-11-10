# Style renders of the rigged pawn (Eevee): plain lighting, hard toon, soft toon, each optionally with a thin outline,
# in the white-team cream or the black-team charcoal. Every variant renders the same camera and light.
# blender -b -P render_styles.py -- <out dir> <variant spec> ...   spec = name|blend|shading|team|outline|view
#   shading: plain | hard | soft      team: white | black      outline: 0 | 1      view: front | three
import bpy, sys, os, math
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
OUT, specs = a[0], a[1:]
os.makedirs(OUT, exist_ok=True)

SKIN, HEADC, CHEEK = (0.945, 0.808, 0.702), (0.898, 0.753, 0.649), (0.91, 0.59, 0.53)
EYE, GLINT, HAND, FOOT = (0.118, 0.098, 0.078), (0.93, 0.93, 0.93), (0.275, 0.231, 0.208), (0.157, 0.133, 0.118)
CREAM, CHARCOAL, IVORY = (0.96, 0.89, 0.77), (0.30, 0.30, 0.31), (0.95, 0.91, 0.79)
from mathutils import Euler
SUN_EULER = (math.radians(48), 0, math.radians(-55))
SUN_DIR = tuple(Euler(SUN_EULER).to_matrix() @ Vector((0, 0, 1)))

def near(c, ref, tol=0.04):
    return max(abs(c[i] - ref[i]) for i in range(3)) < tol

def recolour(o, team):
    """Body colour by team; the blush rim follows the body so its edge stays invisible; black-team eyes are ivory."""
    body = CREAM if team == "white" else CHARCOAL
    attr = o.data.color_attributes["Col"]
    for d in attr.data:
        c = d.color_srgb[:3]
        if near(c, SKIN) or near(c, HEADC):
            new = body
        elif near(c, EYE, 0.03):
            new = EYE if team == "white" else IVORY
        elif near(c, GLINT, 0.03):
            new = GLINT if team == "white" else IVORY
        elif near(c, HAND) or near(c, FOOT):
            new = (0.06, 0.055, 0.055)
        else:
            # blush gradient: how far from the head colour toward the cheek colour
            w = max(0.0, min(1.0, sum((c[i] - HEADC[i]) * (CHEEK[i] - HEADC[i]) for i in range(3)) /
                             sum((CHEEK[i] - HEADC[i]) ** 2 for i in range(3))))
            new = tuple(body[i] + (CHEEK[i] - body[i]) * w for i in range(3))
        d.color_srgb = (*new, 1.0)

def material(shading):
    m = bpy.data.materials.new("Piece_" + shading)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    col = nt.nodes.new("ShaderNodeVertexColor")
    col.layer_name = "Col"
    if shading == "plain":
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Roughness"].default_value = 0.6
        nt.links.new(col.outputs["Color"], bsdf.inputs["Base Color"])
        nt.links.new(bsdf.outputs[0], out.inputs["Surface"])
        return m
    # toon light: half-Lambert of the shading normal against the sun's direction (what a game toon shader does)
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath")
    dot.operation = "DOT_PRODUCT"
    dot.inputs[1].default_value = SUN_DIR
    half = nt.nodes.new("ShaderNodeMath")
    half.operation = "MULTIPLY_ADD"
    half.inputs[1].default_value = 0.5
    half.inputs[2].default_value = 0.5
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    cr = ramp.color_ramp
    # light falls in three tones: warm shadow, mid, lit (the factors multiply the base colour)
    tones = [(0.0, (0.66, 0.58, 0.54)), (0.30, (0.66, 0.58, 0.54)), (0.36, (0.86, 0.82, 0.78)),
             (0.62, (0.86, 0.82, 0.78)), (0.68, (1.0, 1.0, 1.0)), (1.0, (1.0, 1.0, 1.0))]
    if shading == "hard":
        cr.interpolation = "CONSTANT"
        tones = [(0.0, tones[0][1]), (0.48, tones[2][1]), (0.74, tones[4][1])]
    else:
        cr.interpolation = "EASE"
        # softer: the steps are wider
        tones = [(0.0, (0.66, 0.58, 0.54)), (0.40, (0.66, 0.58, 0.54)), (0.54, (0.86, 0.82, 0.78)),
                 (0.66, (0.86, 0.82, 0.78)), (0.80, (1.0, 1.0, 1.0)), (1.0, (1.0, 1.0, 1.0))]
    while len(cr.elements) > 1:
        cr.elements.remove(cr.elements[-1])
    cr.elements[0].position, cr.elements[0].color = tones[0][0], (*tones[0][1], 1)
    for pos, c in tones[1:]:
        e = cr.elements.new(pos)
        e.color = (*c, 1)
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(geo.outputs["Normal"], dot.inputs[0])
    nt.links.new(dot.outputs["Value"], half.inputs[0])
    nt.links.new(half.outputs[0], ramp.inputs["Fac"])
    nt.links.new(col.outputs["Color"], mul.inputs[6])
    nt.links.new(ramp.outputs["Color"], mul.inputs[7])
    nt.links.new(mul.outputs[2], emit.inputs["Color"])
    nt.links.new(emit.outputs[0], out.inputs["Surface"])
    return m

def outline_material():
    m = bpy.data.materials.new("Outline")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = (0.09, 0.07, 0.06, 1)
    nt.links.new(emit.outputs[0], out.inputs["Surface"])
    m.use_backface_culling = True
    return m

def stage(view):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 640, 820
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.20, 0.205, 0.215, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    sc.world = world
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.lens = 70
    az = math.radians(-8 if view == "front" else -32)
    el = math.radians(8)
    target = Vector((0, 0, 0.53))
    d = 3.6
    cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * d
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.data.energy = 3.2
    sun.data.angle = math.radians(6)
    sun.rotation_euler = SUN_EULER

paths = []
for spec in specs:
    name, blend, shading, team, outline, view = spec.split("|")
    bpy.ops.wm.open_mainfile(filepath=blend)
    for o in list(bpy.data.objects):
        if o.type in ("CAMERA", "LIGHT"):
            bpy.data.objects.remove(o, do_unlink=True)
    piece = bpy.data.objects["Pawn"]
    recolour(piece, team)
    piece.data.materials.clear()
    piece.data.materials.append(material(shading))
    if outline == "1":
        piece.data.materials.append(outline_material())
        sol = piece.modifiers.new("Outline", "SOLIDIFY")
        sol.thickness = 0.0045
        sol.offset = 1.0
        sol.use_flip_normals = True
        sol.use_rim = False
        sol.material_offset = 1
    stage(view)
    path = os.path.join(OUT, name + ".png")
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    paths.append(path)
    print("RENDERED", name)

# side by side
ims = []
for p in paths:
    im = bpy.data.images.load(p)
    w, h = im.size
    ims.append(np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4))
gap = np.ones((ims[0].shape[0], 8, 4), dtype=np.float32)
row = []
for x in ims:
    row += [x, gap]
sheet = np.concatenate(row[:-1], axis=1)
img = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels = sheet.ravel()
img.filepath_raw = os.path.join(OUT, "styles.png")
img.file_format = "PNG"
img.save()
print("SHEET", os.path.join(OUT, "styles.png"))
