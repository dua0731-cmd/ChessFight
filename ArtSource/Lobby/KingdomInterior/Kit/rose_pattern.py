# The great rose's light pattern (R563), headless: blender -b KingdomKit.blend --factory-startup --python rose_pattern.py
# Renders the window face-on (orthographic, from the hall) twice with flat colours: once with the stained glass in
# its colours and the stone black, once with the glass white. RGB = the light's colour through each pane, A = how
# much gets through. Unity's RoseShafts.shader reads it (the light in the air and on the floor). It covers
# +-ROSE_HOLE metres round the window's centre, +x right and +z up as seen from the hall.
import bpy, os, builtins
import numpy as np

KIT = r"C:\Work\ChessFight\ArtSource\Lobby\KingdomInterior\Kit"
OUT = os.path.normpath(os.path.join(KIT, "..", "..", "..", "..", "Assets", "Resources", "Lobby", "KingdomHall", "RosePattern.png"))
SIZE = 1024
GLASS = {"Rose Glass Gold": 0xFFC24A, "Rose Glass Red": 0xE0352B, "Rose Glass Blue": 0x3E6BFF, "Rose Glass Amber": 0xFF8A2A}

K = {"__file__": os.path.join(KIT, "kit_build.py"), "__name__": "kit_build"}
exec(open(K["__file__"], encoding="utf-8").read(), K)
builtins.KIT_G = K
p = os.path.join(KIT, "stair_end.py")
G = {"__file__": p, "__name__": "stair_end"}
exec(open(p, encoding="utf-8").read(), G)
G["build_stair_end"]()
sc, root = G["se_scene"]()

keep = set(GLASS) | {"Rose Tracery", "Round Window"}
for o in sc.objects:
    o.hide_render = o.name not in keep
    o.modifiers.clear() if o.name in GLASS else None

cd = bpy.data.cameras.new("RP Cam")
cd.type = "ORTHO"
cd.ortho_scale = 2 * G["ROSE_HOLE"]
cam = bpy.data.objects.new("RP Cam", cd)
sc.collection.objects.link(cam)
cam.location = (0, G["BACK_Y"] - 6, G["ROSE_Z"])
cam.rotation_euler = (1.5707963, 0, 0)          # looking +Y, up +Z
cd.clip_start, cd.clip_end = .1, 20
sc.camera = cam
sc.render.engine = "BLENDER_WORKBENCH"
sc.render.resolution_x = sc.render.resolution_y = SIZE
sc.render.resolution_percentage = 100
sc.render.film_transparent = False
sc.display.render_aa = "16"
sh = sc.display.shading
sh.light = "FLAT"
sh.color_type = "OBJECT"
sh.show_cavity = sh.show_shadows = sh.show_object_outline = sh.show_specular_highlight = False
if sc.world is None:
    sc.world = bpy.data.worlds.new("RP World")
sc.world.color = (0, 0, 0)
sc.view_settings.view_transform = "Standard"
sc.view_settings.look = "None"
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGB"


def hexc(h):
    return [((h >> s) & 255) / 255 for s in (16, 8, 0)]


def srgb_to_lin(c):
    return [x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c]


def shot(path, colours):
    for o in sc.objects:
        if o.name in keep:
            c = colours.get(o.name, (0, 0, 0))
            o.color = (*c, 1)
    sc.render.filepath = path
    with bpy.context.temp_override(scene=sc):
        bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(SIZE, SIZE, 4)[:, :, :3]
    bpy.data.images.remove(img)
    return px


# Workbench's object colour is linear; it comes out in sRGB with the Standard view
a = shot(os.path.join(KIT, "rose_pattern_colour.png"), {n: srgb_to_lin(hexc(h)) for n, h in GLASS.items()})
b = shot(os.path.join(KIT, "rose_pattern_cover.png"), {n: (1, 1, 1) for n in GLASS})
alpha = b.mean(axis=2, keepdims=True)
rgba = np.concatenate([a, alpha], axis=2)
img = bpy.data.images.new("RosePattern", SIZE, SIZE, alpha=True)
img.alpha_mode = "STRAIGHT"
img.pixels[:] = rgba.ravel()
img.filepath_raw = OUT
img.file_format = "PNG"
img.save()
print("pattern", OUT, "open share", round(float(alpha.mean()) / (np.pi / 4), 3))
