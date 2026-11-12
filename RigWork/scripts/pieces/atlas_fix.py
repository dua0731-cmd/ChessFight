# Write the stripe atlases again from the eye file's materials (the black team's ivory read from its emission), for the
# pieces and the pawn: <P>_Black_Albedo.png, Assets/Art/Pawn/PawnAlbedo_Black.png, and PawnAlbedo.jpg with the plain
# sRGB values (the first jpg went through Blender's view transform and came out grey).
# blender -b -P atlas_fix.py -- <eye .blend> <repo root> Rook Knight Bishop
import bpy, sys, os
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
EYE, REPO, NAMES = a[0], a[1], a[2:]
bpy.ops.wm.open_mainfile(filepath=EYE)
CREAM, CHARCOAL, DARK = (0.96, 0.88, 0.76), (0.22, 0.22, 0.24), (0.05, 0.045, 0.045)
STRIPES = 8
def srgb(c):
    return tuple(12.92 * x if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055 for x in c[:3])
def base(mat_name, fallback):
    m = bpy.data.materials.get(mat_name)
    if m is None:
        return fallback
    for n in m.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            if n.inputs["Emission Strength"].default_value >= 0.8:
                return srgb(n.inputs["Emission Color"].default_value)
            return srgb(n.inputs["Base Color"].default_value)
    return fallback
white = [CREAM, DARK, base("Eye_WhiteTeam_Black", DARK), base("Glint_WhiteTeam", (1, 1, 1)),
         base("Blush_WhiteTeam", (0.96, 0.6, 0.54)), base("Eye_WhiteTeam_Black", DARK)]
ivory = base("Eye_BlackTeam_Ivory", (0.93, 0.9, 0.82))
black = [CHARCOAL, DARK, ivory, ivory, base("Blush_BlackTeam", (0.7, 0.4, 0.4)), ivory]
print("COLOURS white", [tuple(round(x * 255) for x in c) for c in white], "black", [tuple(round(x * 255) for x in c) for c in black])
sc = bpy.context.scene
sc.view_settings.view_transform = "Standard"
sc.view_settings.look = "None"
sc.view_settings.exposure = 0
sc.view_settings.gamma = 1
def atlas(path, cols, fmt="PNG"):
    n, w = 256, 256 // STRIPES
    px = np.ones((n, n, 4), dtype=np.float32)
    for i in range(STRIPES):
        px[:, i * w:(i + 1) * w, :3] = cols[i] if i < len(cols) else cols[0]
    img = bpy.data.images.new(os.path.basename(path), n, n, alpha=False)
    img.pixels = px.ravel()
    if fmt == "PNG":
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
    else:
        sc.render.image_settings.file_format = "JPEG"
        sc.render.image_settings.quality = 95
        img.save_render(path, scene=sc)
    print("ATLAS", path)
for name in NAMES:
    atlas(os.path.join(REPO, "Assets/Art/Pieces", name, name + "_Black_Albedo.png"), black)
atlas(os.path.join(REPO, "Assets/Art/Pawn/PawnAlbedo_Black.png"), black)
atlas(os.path.join(REPO, "Assets/Art/Pawn/PawnAlbedo.jpg"), white, "JPEG")
