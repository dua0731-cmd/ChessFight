# -*- coding: utf-8 -*-
"""B 보수 중인 동관: the white team's planks in pale maple (R528).

The user (11-09, list 2): "나무 판자 색 구분 가능하도록 양 팀이 다르게". The plank model (B_Plank.fbx) wears the walnut
atlas PR_Mid_WoodKit; black's planks darken it in code (MgPlanks: the material's colour times dark ebony), but a colour
multiply cannot lighten a dark texture, so white's planks wear this pale copy of the atlas instead:

  - every wood pixel keeps its light and shade (the grain, the seams, the worn edges) on a maple ramp from a honey
    brown in the darkest grain to a cream in the lightest, after a percentile stretch of the wood's brightness;
  - the gold nail heads and brass (saturated yellow, bright) stay as they are.

Output (1024 x 1024, the plank never fills more of the screen than that):
  Assets/Art/PawnRushMiniGames/Mid/Textures/PR_Mid_WoodKit_Maple_BaseColor.png
The material Resources/PawnRushMiniGames/Materials/PR_Mid_WoodKit_Maple.mat is PR_Mid_WoodKit with this base map
(written by hand once; MgPlanks loads it by name and falls back to a lighter tint without it).

Run from the project root:  python ArtSource/PawnRush/MiniGames/Mid/b_plank_maple.py  (numpy, Pillow)
"""
import os
import sys

import numpy as np
from PIL import Image

SOURCE = "Assets/Art/PawnRushMiniGames/Mid/Textures/PR_Mid_WoodKit_BaseColor.png"
TARGET = "Assets/Art/PawnRushMiniGames/Mid/Textures/PR_Mid_WoodKit_Maple_BaseColor.png"
SIZE = 1024

# The maple ramp, sRGB 0..255: darkest grain and seams, the middle, the lightest wood.
MAPLE_DARK = np.array([124, 84, 44], float)
MAPLE_MID = np.array([216, 170, 106], float)
MAPLE_LIGHT = np.array([250, 229, 184], float)


def main():
    root = os.getcwd()
    src = Image.open(os.path.join(root, SOURCE)).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
    a = np.asarray(src).astype(float) / 255.0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx, mn = a.max(axis=2), a.min(axis=2)
    sat = np.where(mx > 1e-4, (mx - mn) / np.maximum(mx, 1e-4), 0.0)
    lum = .2126 * r + .7152 * g + .0722 * b
    # hue in degrees (0..360) for the gold test
    d = np.maximum(mx - mn, 1e-6)
    hue = np.where(mx == r, (g - b) / d % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60.0
    gold = (sat > .45) & (mx > .55) & (hue > 30) & (hue < 62) & (lum > .38)
    wood = ~gold
    lo, hi = np.percentile(lum[wood], 2), np.percentile(lum[wood], 98)
    s = np.clip((lum - lo) / max(hi - lo, 1e-4), 0.0, 1.0)
    # two-part ramp: dark -> mid over the lower 55 %, mid -> light above
    k1 = np.clip(s / .55, 0, 1)[..., None]
    k2 = np.clip((s - .55) / .45, 0, 1)[..., None]
    maple = MAPLE_DARK + (MAPLE_MID - MAPLE_DARK) * k1
    maple = maple + (MAPLE_LIGHT - MAPLE_MID) * k2 * (s[..., None] > .55)
    out = np.where(gold[..., None], a * 255.0, maple)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(os.path.join(root, TARGET), optimize=True)
    print("gold pixels %.2f %%, wood brightness %.3f .. %.3f -> %s" % (gold.mean() * 100, lo, hi, TARGET))


if __name__ == "__main__":
    sys.exit(main())
