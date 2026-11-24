# JY-gfx: the swatch scripts' baked height and ambient occlusion (ArtSource/PawnRush/MiniGames/*/Materials/Textures,
# r502-r509: baked from real geometry in Blender, never imported into Unity) packed into one two-channel picture per
# material for the floor relief: R height (0 low .. 1 high, as baked: 0.5 and 99.5 percentiles), G ambient occlusion.
# Run: blender -b --factory-startup --python pack_height.py
import bpy, glob, os
import numpy as np

SRC = r"C:\Users\trews\OneDrive\Desktop\chess3333\ChessFight\ArtSource\PawnRush\MiniGames"
OUT = r"C:\cf-gfx\tex\height"
MAX = 1024
os.makedirs(OUT, exist_ok=True)


def load(path, size):
    im = bpy.data.images.load(path, check_existing=False)
    im.colorspace_settings.name = "Non-Color"
    w, h = im.size
    if w > size or h > size:
        im.scale(min(w, size), min(h, size))
        w, h = im.size
    a = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)


for hp in sorted(glob.glob(os.path.join(SRC, "*", "Materials", "Textures", "*_Height.png"))):
    name = os.path.basename(hp)[: -len("_Height.png")]
    h = load(hp, MAX)[:, :, 0]
    ap = hp[: -len("_Height.png")] + "_AO.png"
    a = load(ap, h.shape[0])[:, :, 0] if os.path.exists(ap) else np.ones_like(h)
    if a.shape != h.shape:
        a = np.ones_like(h)
    rows, cols = h.shape
    out = np.zeros((rows, cols, 4), np.float32)
    out[:, :, 0] = h
    out[:, :, 1] = a
    out[:, :, 3] = 1.0
    img = bpy.data.images.new(name + "_HeightAO", cols, rows, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(out.ravel())
    img.filepath_raw = os.path.join(OUT, name + "_HeightAO.png")
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print(f"packed {name}: {cols}x{rows}, height {h.min():.2f}..{h.max():.2f} mean {h.mean():.2f}, ao mean {a.mean():.2f}")
