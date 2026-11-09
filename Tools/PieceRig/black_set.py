# The black team's promoted pieces (R527, user 11-09: "흑팀 승격 기물들 모델링 넣어야 함" -> "흑팀 기물 어둡게 칠하기").
# The five piece models (Art/Pieces/<Piece>/<Piece>_Albedo.png, Tripo bakes) have one look for both teams: a cream body,
# black mitten hands, shoes and eyes with white glints, pink cheeks. This writes <Piece>_Black_Albedo.png beside each, the
# same atlas recoloured by colour (the atlas is in pieces, so not by part):
#   cream body          -> ebony, its shading kept (the body's lightness mapped onto a dark warm ramp)
#   black hands/shoes/eyes -> gold, their own shading kept (the dark parts of a white piece are gold on a black one)
#   white glints by the eyes stay white, the pink cheeks stay pink
# The two classes cross over smoothly where the bake blends them (lightness 0.16 .. 0.34).
#
#   python Tools/PieceRig/black_set.py            (from the project root; numpy + Pillow)
import os
import numpy as np
from PIL import Image

PIECES = ["Rook", "Knight", "Bishop", "Queen", "King"]
EBONY_DARK, EBONY_LIGHT = np.array([16, 12, 11], np.float32), np.array([84, 67, 58], np.float32)
# the gold keeps only a little of the bake's shading: the black parts' smudges showed as dirty spots at full range
GOLD_DARK, GOLD_LIGHT = np.array([192, 140, 52], np.float32), np.array([242, 198, 104], np.float32)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def near(mask, radius):
    """The mask grown by `radius` pixels (a square max filter, by shifts)."""
    out = mask.copy()
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            out |= np.roll(np.roll(mask, dy, 0), dx, 1)
    return out


def grow_within(seed, allowed, steps=80):
    """The seed grown pixel by pixel (8 neighbours) through `allowed` until it stops (or `steps` rounds)."""
    region = seed & allowed
    for _ in range(steps):
        grown = region.copy()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy or dx:
                    grown |= np.roll(np.roll(region, dy, 0), dx, 1)
        grown &= allowed
        if (grown == region).all():
            break
        region = grown
    return region


def box(values, radius):
    """The mean over a (2 radius + 1) square round each pixel (summed-area table, edges clamped)."""
    pad = np.pad(values, radius + 1, mode="edge").astype(np.float64)
    s = pad.cumsum(0).cumsum(1)
    n = 2 * radius + 1
    h, w = values.shape
    total = s[n:n + h, n:n + w] - s[0:h, n:n + w] - s[n:n + h, 0:w] + s[0:h, 0:w]
    return (total / (n * n)).astype(np.float32)


def recolour(rgb):
    L = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = np.where(mx > 1e-4, (mx - mn) / np.maximum(mx, 1e-4), 0.0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = (np.degrees(np.arctan2(np.sqrt(3.0) * (g - b), 2.0 * r - g - b)) + 360.0) % 360.0

    body = EBONY_DARK + (EBONY_LIGHT - EBONY_DARK) * (np.clip((L - 0.40) / 0.55, 0, 1) ** 1.1)[..., None]
    accent = GOLD_DARK + (GOLD_LIGHT - GOLD_DARK) * np.clip(L / 0.22, 0, 1)[..., None]
    # A black part is its black core (lightness under 0.12) and every darker-than-cream pixel joined to it: the mittens'
    # and shoes' worn mid-tones and baked grey highlights belong to the part (by lightness alone they came out as ebony
    # holes in the gold). The body's own dark creases are not joined to a core and stay ebony. Softened by a pixel at
    # its edge; its own lightness sets where on the gold a pixel falls.
    part = grow_within(L < 0.12, L < 0.5)
    w_dark = box(part.astype(np.float32), 1)
    out = body / 255.0 * (1.0 - w_dark[..., None]) + accent / 255.0 * w_dark[..., None]

    # the glints in the eyes: pure white next to the dark parts
    glint = (sat < 0.08) & (L > 0.88) & near(L < 0.2, 6)
    out[glint] = rgb[glint]
    # the cheeks: pink (well away from the cream's hue, 26 deg), kept as they are
    hue_w = np.clip(1.0 - np.minimum(np.abs(((hue - 350.0) + 180.0) % 360.0 - 180.0) / 25.0, 1.0), 0, 1)
    w_pink = smoothstep(0.18, 0.30, sat) * hue_w * smoothstep(0.35, 0.5, L)
    out = out * (1.0 - w_pink[..., None]) + rgb * w_pink[..., None]
    return np.clip(out, 0.0, 1.0)


def main():
    root = os.path.join("Assets", "Art", "Pieces")
    for piece in PIECES:
        src = os.path.join(root, piece, piece + "_Albedo.png")
        im = Image.open(src).convert("RGB")
        rgb = np.asarray(im).astype(np.float32) / 255.0
        dst = os.path.join(root, piece, piece + "_Black_Albedo.png")
        Image.fromarray((recolour(rgb) * 255.0 + 0.5).astype(np.uint8), "RGB").save(dst, optimize=True)
        print("wrote", dst)


if __name__ == "__main__":
    main()
