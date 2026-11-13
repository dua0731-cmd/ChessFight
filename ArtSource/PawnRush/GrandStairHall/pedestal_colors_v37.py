# R347 (승규 11-04, after R345's deep green pedestals: "받침대 색이 마음에 안들어 시안 줘"): pedestal colour options for
# the four stair statues, rendered in EEVEE with veined marble (procedural, in object space as the Unity shader is in
# world space) so the stone reads, all from 승규's shot up the grand stair. Nothing is saved or exported: the chosen
# option goes into PR_GreenMarble's colours (or a new material) afterwards.
# Run: blender -b grand_stair_hall_v36C.blend -P pedestal_colors_v37.py [-- A C]   -> renders_v44/pedestal_<key>_<view>.png,
#      renders_v44/pedestal_options_wide.png and _close.png (the sheets)
import bpy, math, os, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "renders_v44")
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene
ONLY = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# key, label, base, darker patches, veins, vein strength, roughness
OPTIONS = [
    ("A", "A  Nero Marquina - black, white veins", (.018, .018, .02), (.008, .008, .009), (.85, .84, .82), .9, .12),
    ("B", "B  Rosso Levanto - burgundy, cream veins", (.20, .03, .035), (.09, .012, .016), (.80, .66, .55), .7, .15),
    ("C", "C  Travertine - warm cream, honey veins", (.78, .66, .48), (.62, .50, .34), (.55, .40, .24), .55, .3),
    ("D", "D  Lapis - royal blue, gold flecks", (.02, .05, .22), (.008, .02, .10), (.85, .66, .30), .75, .15),
    ("E", "E  Bardiglio - blue grey, dark veins", (.36, .40, .45), (.24, .27, .31), (.12, .13, .16), .7, .18),
    ("F", "F  Verde Alpi - current deep green", (.06, .2, .15), (.02, .08, .06), (.62, .78, .68), .8, .14),
]


def rgba(sockets, name):
    """The colour socket of a Mix node (it has a float, a vector and a colour socket under each name)."""
    return next(s for s in sockets if s.name == name and s.type == "RGBA")


def marble(m, base, shade, vein, strength, rough, scale=1.1):
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
    ramp_v.color_ramp.elements[1].position, ramp_v.color_ramp.elements[1].color = .03, (0, 0, 0, 1)
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
    L(ramp_v.outputs["Color"], mul.inputs[0])
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


PLINTH = bpy.data.materials["Plinth_Marble"]
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


CAMS = [("wide", camera("Cam37_wide", (0, 16, 13.5), (0, 46, 5.5), 22)),
        ("close", camera("Cam37_close", (8.5, 37.5, 8.2), (15.2, 44.6, 5.6), 30))]
files = {k: [] for k, _ in CAMS}
for key, label, base, shade, vein, strength, rough in OPTIONS:
    if ONLY and key not in ONLY:
        continue
    marble(PLINTH, base, shade, vein, strength, rough)
    for view, (cam, tx) in CAMS:
        tx.data.body = label
        for _, (c2, t2) in CAMS:
            t2.hide_render = t2 is not tx
        sc.camera = cam
        path = os.path.join(OUT, "pedestal_%s_%s.png" % (key, view))
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
    img = bpy.data.images.new("pedestal_options_" + view, sheet.shape[1], sheet.shape[0], alpha=True)
    img.pixels = sheet.ravel()
    img.filepath_raw = os.path.join(OUT, "pedestal_options_%s.png" % view)
    img.file_format = "PNG"
    img.save()
    print("SHEET", img.filepath_raw)
