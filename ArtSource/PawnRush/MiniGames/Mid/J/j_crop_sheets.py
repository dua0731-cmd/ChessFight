# Crop the J asset sheets down to the parts no other mini-game already has (user 11-08, relayed by the H
# session: reuse other chats' models, only ask Tripo for what is new).
#   J_assets_01_tub_basin.png -> J_tripo_01_tub_nozzle.png   (tub + nozzle; basin walls/platform are Blender boxes)
#   J_assets_03_gates.png     -> J_tripo_03_sluice.png       (sluice leaf + wheel; facade/frame reuse B/E)
# Run: "<Blender>/blender.exe" -b --python j_crop_sheets.py
import bpy, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REFS = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R509_PalaceLaundry", "AssetRefs"))
JOBS = [("J_assets_01_tub_basin.png", "J_tripo_01_tub_nozzle.png", 0., .285),
        ("J_assets_03_gates.png", "J_tripo_03_sluice.png", .635, 1.)]

for src, dst, x0, x1 in JOBS:
    img = bpy.data.images.load(os.path.join(REFS, src), check_existing=False)
    w, h = img.size
    px = np.array(img.pixels[:], np.float32).reshape(h, w, 4)
    a, b = int(x0 * w), int(x1 * w)
    crop = px[:, a:b].copy()
    pad = int(.04 * w)                       # white margin on both sides
    white = np.ones((h, pad, 4), np.float32)
    out_px = np.concatenate([white, crop, white], 1)
    out = bpy.data.images.new(dst, out_px.shape[1], h, alpha=True)
    out.pixels.foreach_set(out_px.ravel())
    out.filepath_raw = os.path.join(REFS, dst)
    out.file_format = "PNG"
    out.save()
    print("wrote", out.filepath_raw, out_px.shape[1], h)

# User 11-08 "저 사진 하나로 합쳐": the two crops side by side as one Tripo sheet.
parts = []
for name in ("J_tripo_01_tub_nozzle.png", "J_tripo_03_sluice.png"):
    img = bpy.data.images.load(os.path.join(REFS, name), check_existing=False)
    w, h = img.size
    parts.append(np.array(img.pixels[:], np.float32).reshape(h, w, 4))
out_px = np.concatenate(parts, 1)
out = bpy.data.images.new("merged", out_px.shape[1], out_px.shape[0], alpha=True)
out.pixels.foreach_set(out_px.ravel())
out.filepath_raw = os.path.join(REFS, "J_tripo_01_tub_sluice.png")
out.file_format = "PNG"
out.save()
print("wrote", out.filepath_raw, out_px.shape[1], out_px.shape[0])
