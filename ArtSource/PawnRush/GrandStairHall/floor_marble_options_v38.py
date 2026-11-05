# R358 (승규 11-04, after R357 put the grand stair's marble texture on the hall's floors: "일단 돌 대리석 같은 느낌이 있어야
# 돼 문양도 없고 시안으로 여러가지 뽑아서 줘"): floor marble options for the hall's marble floors (every material named
# Marble*: the central landing, the dais, the landings, the balcony), rendered in EEVEE with veined stone (object space,
# bold veins plus fine ones, polished) from 승규's two shots - the central landing with the king, the royal dais.
# Nothing is saved or exported: the chosen one goes into the floors' Unity material afterwards.
# Run: blender -b grand_stair_hall_v36C.blend -P floor_marble_options_v38.py [-- A C]
#      -> renders_v45/floor_<key>_<view>.png, renders_v45/floor_options_landing.png and _dais.png (the sheets)
import bpy, math, os, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "renders_v45")
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene
ONLY = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# key, label, base, darker clouds, bold veins, vein strength, roughness, vein width, fine veins, scale
OPTIONS = [
    ("A", "A  Calacatta Oro - white, bold grey-gold veins", (.90, .88, .84), (.80, .78, .74), (.42, .36, .27), .85, .08, .045, .35, .8),
    ("B", "B  Carrara - soft white, feathery grey veins", (.82, .82, .82), (.70, .71, .72), (.40, .42, .46), .6, .14, .025, .7, 1.4),
    ("C", "C  Statuario - bright white, dark dramatic veins", (.93, .93, .92), (.86, .86, .86), (.16, .16, .18), .9, .07, .035, .25, .65),
    ("D", "D  Botticino - warm cream, beige veins", (.80, .72, .58), (.68, .59, .45), (.52, .42, .30), .7, .16, .03, .55, 1.1),
    ("E", "E  Crema Marfil - ivory, fine honey cracks", (.85, .78, .64), (.76, .68, .54), (.60, .48, .32), .75, .12, .015, .9, 2.0),
    ("F", "F  Grigio Perla - pearl grey, white veins", (.52, .53, .55), (.42, .43, .45), (.90, .90, .88), .75, .1, .03, .5, 1.0),
]


def rgba(sockets, name):
    """The colour socket of a Mix node (it has a float, a vector and a colour socket under each name)."""
    return next(s for s in sockets if s.name == name and s.type == "RGBA")


def marble(m, base, shade, vein, strength, rough, scale=1.1, width=.03, fine=0.):
    """A veined marble in object space: broad patches, then thin veins where warped noise crosses one level."""
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    coord = nt.nodes.new("ShaderNodeTexCoord")
    patch = nt.nodes.new("ShaderNodeTexNoise"); patch.inputs["Scale"].default_value = .35 * scale; patch.inputs["Detail"].default_value = 4
    warp = nt.nodes.new("ShaderNodeTexNoise"); warp.inputs["Scale"].default_value = .6 * scale; warp.inputs["Detail"].default_value = 6
    warp.inputs["Distortion"].default_value = 2.5
    wave = nt.nodes.new("ShaderNodeTexWave"); wave.inputs["Scale"].default_value = .16 * scale
    wave.inputs["Distortion"].default_value = 4.; wave.inputs["Detail"].default_value = 6; wave.inputs["Detail Scale"].default_value = 1.5
    ramp_v = nt.nodes.new("ShaderNodeValToRGB")
    ramp_v.color_ramp.elements[0].position, ramp_v.color_ramp.elements[0].color = .0, (1, 1, 1, 1)
    ramp_v.color_ramp.elements[1].position, ramp_v.color_ramp.elements[1].color = width, (0, 0, 0, 1)
    mix_p = nt.nodes.new("ShaderNodeMix"); mix_p.data_type = "RGBA"
    mix_v = nt.nodes.new("ShaderNodeMix"); mix_v.data_type = "RGBA"
    mul = nt.nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"; mul.inputs[1].default_value = strength
    L = nt.links.new
    L(coord.outputs["Object"], patch.inputs["Vector"])
    L(coord.outputs["Object"], warp.inputs["Vector"])
    bend = nt.nodes.new("ShaderNodeVectorMath"); bend.operation = "MULTIPLY_ADD"     # coords + (warp - 0.5) * 2
    bend.inputs[1].default_value = (2, 2, 2); bend.inputs[2].default_value = (-1, -1, -1)
    add = nt.nodes.new("ShaderNodeVectorMath"); add.operation = "ADD"
    L(warp.outputs["Color"], bend.inputs[0])
    L(coord.outputs["Object"], add.inputs[0]); L(bend.outputs["Vector"], add.inputs[1])
    L(add.outputs["Vector"], wave.inputs["Vector"])
    L(wave.outputs["Fac"], ramp_v.inputs["Fac"])
    L(patch.outputs["Fac"], mix_p.inputs[0])
    rgba(mix_p.inputs, "A").default_value = (*base, 1)
    rgba(mix_p.inputs, "B").default_value = (*shade, 1)
    veins = ramp_v.outputs["Color"]
    if fine > 0:                             # a second, finer set of veins at a third of the strength
        wave2 = nt.nodes.new("ShaderNodeTexWave"); wave2.inputs["Scale"].default_value = .55 * scale
        wave2.inputs["Distortion"].default_value = 7.; wave2.inputs["Detail"].default_value = 8
        ramp2 = nt.nodes.new("ShaderNodeValToRGB")
        ramp2.color_ramp.elements[0].position, ramp2.color_ramp.elements[0].color = .0, (fine * .45, fine * .45, fine * .45, 1)
        ramp2.color_ramp.elements[1].position, ramp2.color_ramp.elements[1].color = width * .45, (0, 0, 0, 1)
        both = nt.nodes.new("ShaderNodeMath"); both.operation = "MAXIMUM"
        L(add.outputs["Vector"], wave2.inputs["Vector"]); L(wave2.outputs["Fac"], ramp2.inputs["Fac"])
        L(ramp_v.outputs["Color"], both.inputs[0]); L(ramp2.outputs["Color"], both.inputs[1])
        veins = both.outputs["Value"]
    L(veins, mul.inputs[0])
    L(mul.outputs["Value"], mix_v.inputs[0])
    L(rgba(mix_p.outputs, "Result"), rgba(mix_v.inputs, "A"))
    rgba(mix_v.inputs, "B").default_value = (*vein, 1)
    L(rgba(mix_v.outputs, "Result"), bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = rough
    L(bsdf.outputs["BSDF"], out.inputs["Surface"])
    m.diffuse_color = (*base, 1)


def gold(m):
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    if b:
        b.inputs["Base Color"].default_value = (.86, .62, .25, 1)
        b.inputs["Metallic"].default_value = 1.
        b.inputs["Roughness"].default_value = .28


FLOORS = [m for m in bpy.data.materials if m.name.startswith("Marble")]
print("FLOOR MATERIALS", [m.name for m in FLOORS])
marble(bpy.data.materials["Plinth_Marble"], (.78, .66, .48), (.62, .50, .34), (.55, .40, .24), .55, .3)   # R347 travertine
marble(bpy.data.materials["Statue_Marble"], (.93, .92, .9), (.84, .84, .85), (.5, .52, .57), .45, .2, 1.4)
gold(bpy.data.materials["Palace_Gold"])
# The walls' trim-map texture does not load in a background EEVEE render (it showed magenta): plain cream stone.
for m in bpy.data.materials:
    if m.name.startswith(("Palace_Wall", "Palace_Stone")) and m.use_nodes:
        b = m.node_tree.nodes.get("Principled BSDF")
        if b:
            for link in list(b.inputs["Base Color"].links):
                m.node_tree.links.remove(link)
            b.inputs["Base Color"].default_value = (.80, .72, .58, 1)
            b.inputs["Roughness"].default_value = .7
for m in bpy.data.materials:                 # the pads' textures do not load here either (magenta): plain
    if m.name.startswith(("FloatingPad", "JumpPad")) and m.use_nodes:
        b = m.node_tree.nodes.get("Principled BSDF")
        if b:
            for link in list(b.inputs["Base Color"].links):
                m.node_tree.links.remove(link)
            b.inputs["Base Color"].default_value = (.55, .5, .42, 1)
carpet = bpy.data.materials.get("Section_Respawn")
if carpet is not None:                       # the red floor as Unity's carpet reads
    carpet.use_nodes = True
    b = carpet.node_tree.nodes.get("Principled BSDF")
    if b:
        b.inputs["Base Color"].default_value = (.32, .015, .02, 1)
        b.inputs["Roughness"].default_value = .9

# Light: a bright sky, a sun through the vault, a big soft fill from behind the camera.
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 960, 540
sc.render.film_transparent = False
try:
    sc.eevee.use_raytracing = True          # the polished floors mirror what stands on them
except Exception:
    pass
w = sc.world or bpy.data.worlds.new("World")
sc.world = w
w.use_nodes = True
bg = w.node_tree.nodes.get("Background")
bg.inputs["Color"].default_value = (.75, .8, .88, 1)
bg.inputs["Strength"].default_value = .55
for name, kind, loc, rot, energy, size in (("Prev_sun", "SUN", (0, 40, 60), (math.radians(35), 0, math.radians(200)), 3., 0),
                                           ("Prev_fill", "AREA", (0, 4, 16), (math.radians(70), 0, 0), 12000., 30)):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    if kind == "AREA":
        ld.size = size
    lo = bpy.data.objects.new(name, ld); sc.collection.objects.link(lo)
    lo.location, lo.rotation_euler = loc, rot
for c in ("Royal_balcony_v34 previews",):
    if c in bpy.data.collections:
        for o in bpy.data.collections[c].objects:
            o.hide_render = True
try:
    sc.view_settings.view_transform = "AgX"
except Exception:
    pass

sc.view_settings.exposure = -1.1
for m in bpy.data.materials:                 # the cream walls a little deeper, as in Unity
    if m.name.startswith(("Palace_Wall", "Palace_Stone")) and m.use_nodes:
        b = m.node_tree.nodes.get("Principled BSDF")
        if b:
            b.inputs["Base Color"].default_value = (.62, .54, .42, 1)

label_mat = bpy.data.materials.new("Prev_label")
label_mat.use_nodes = True
lb = label_mat.node_tree.nodes.get("Principled BSDF")
lb.inputs["Base Color"].default_value = (1, 1, 1, 1)
lb.inputs["Emission Color"].default_value = (1, 1, 1, 1)
lb.inputs["Emission Strength"].default_value = 6.


def camera(name, loc, at, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_end = 2000
    cam = bpy.data.objects.new(name, cd); sc.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    # A label in the top left corner, 2 m in front of the lens.
    tc = bpy.data.curves.new(name + "_label", "FONT")
    tc.size = .11 * 22 / lens
    tx = bpy.data.objects.new(name + "_label", tc); sc.collection.objects.link(tx)
    tx.data.materials.append(label_mat)
    tx.parent = cam
    half_w = 2 * 18 / lens
    tx.location = (-half_w * .95, half_w * 9 / 16 * .78, -2)
    return cam, tx


CAMS = [("landing", camera("Cam38_landing", (7, 49, 17), (0, 60, 8), 24)),
        ("dais", camera("Cam38_dais", (6, 80, 24), (0, 93, 16), 24))]
files = {k: [] for k, _ in CAMS}
for key, label, base, shade, vein, strength, rough, width, fine, scale in OPTIONS:
    if ONLY and key not in ONLY:
        continue
    for m in FLOORS:
        marble(m, base, shade, vein, strength, rough, scale, width, fine)
    for view, (cam, tx) in CAMS:
        tx.data.body = label
        for _, (c2, t2) in CAMS:
            t2.hide_render = t2 is not tx
        sc.camera = cam
        path = os.path.join(OUT, "floor_%s_%s.png" % (key, view))
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        files[view].append((key, path))
        print("RENDER", key, view, path)

# The sheets: one per view, the options 3 by 2 (A B C over D E F).
for view, got in files.items():
    if len(got) < 2:
        continue
    tiles = []
    for key, path in got:
        img = bpy.data.images.load(path)
        tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4))
        bpy.data.images.remove(img)
    h, wdt = tiles[0].shape[:2]
    cols, gap = 3, 12
    rows = (len(tiles) + cols - 1) // cols
    sheet = np.ones((rows * h + (rows + 1) * gap, cols * wdt + (cols + 1) * gap, 4), dtype=np.float32)
    sheet[..., :3] = .08
    for i, tile in enumerate(tiles):
        r, c = divmod(i, cols)
        y0 = sheet.shape[0] - (r + 1) * (h + gap)          # Blender images start at the bottom row
        x0 = gap + c * (wdt + gap)
        sheet[y0:y0 + h, x0:x0 + wdt] = tile
    img = bpy.data.images.new("floor_options_" + view, sheet.shape[1], sheet.shape[0], alpha=True)
    img.pixels = sheet.ravel()
    img.filepath_raw = os.path.join(OUT, "floor_options_%s.png" % view)
    img.file_format = "PNG"
    img.save()
    print("SHEET", img.filepath_raw)
