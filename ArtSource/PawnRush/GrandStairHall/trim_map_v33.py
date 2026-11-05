# 승규 11-03, on the wall module's trim map: "검은색이 원래 돌 텍스쳐 흰색이 골드색이야". The map becomes a texture the wall
# shader reads (PR_PalaceWall.shader) - white gold, black stone - so the gold edge is the map's, not a triangle's.
# Built as walls_v30.py built it (the module's front depth map, its steps over 0.0015 per texel widened 2 texels) at
# the depth map's 980 x 590 over the asset's x -0.49 .. 0.49, z 0 .. 0.59, then smoothed a texel for a soft edge and
# doubled (1960 x 1180; the UVs span it whole). Row 0 of the depth map is
# the asset's bottom, so it is flipped on saving (a texture's top row is v = 1).
# Run: python trim_map_v33.py -> Assets/Resources/PawnRushGrandStairHall/PalaceTrim.png, renders preview PalaceTrimPreview.png
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
D = np.load(os.path.join(HERE, "textures", "WallModule_front.npy"))
F = np.where(np.isnan(D), np.nanmax(D) + .02, D)


def box(a, r):
    k = 2 * r + 1
    c = np.pad(np.pad(a, r, mode="edge").cumsum(0).cumsum(1), ((1, 0), (1, 0)))
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


gy, gx = np.gradient(box(F, 1))
trim = (box((np.hypot(gx, gy) > .0015).astype(float), 2) > 0).astype(float)
soft = box(trim, 1)
# R339 (승규 "벽테두리 금색이 좀 굵어 살짝만 얇게 해"): the shader's gold edge is this map's 0.5; moving it to the soft
# edge's 0.7 takes about 0.6 texel off each side - lines about 3.8 texels wide instead of 5 (about 0.16 m on a 41 m wall).
THIN = .2
soft = np.clip((soft - THIN * 2) / (1 - THIN * 2), 0, 1)
big = np.asarray(Image.fromarray((soft * 255).astype(np.uint8)).resize((soft.shape[1] * 2, soft.shape[0] * 2), Image.BICUBIC))
Image.fromarray(np.flipud(big)).save(os.path.join(RES, "PalaceTrim.png"))
# a coloured preview for Blender's renders: cream where black, gold where white
m = np.flipud(big)[..., None] / 255.0
cream, gold = np.array([236, 224, 200.]), np.array([219, 163, 69.])
Image.fromarray((cream * (1 - m) + gold * m).astype(np.uint8)).save(os.path.join(HERE, "textures", "PalaceTrimPreview.png"))
print("TRIM", big.shape, "%.1f %% gold" % (100 * (big > 127).mean()))
