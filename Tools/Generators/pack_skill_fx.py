"""Packs the effect workshop's skill effects into the sheets the skill test scene plays (R78).

The workshop (PXF) exports each effect as a strip of frames; this puts each effect's frames into one sheet
(rows of up to eight, left to right, top to bottom) under
Assets/ChessFight/RagdollLab/Resources/PawnRushSkillFx/, where PawnRushSkillFx.cs loads them. On the way it
tunes some of them for the game screen (see tune()) and makes the rook's hit sparks from the queen's.

    python Tools/Generators/pack_skill_fx.py --zip chess_vfx_spritesheets.zip --queen queen_shockwave.webp

--zip    the workshop export: one folder of numbered frames per effect (names as in SHEETS below)
--queen  the queen's shockwave, which came on its own as one strip of 20 frames 100 px wide
Needs Pillow. The .meta files already in the folder are kept, so Unity keeps its references. The output is
the same bytes for the same input.
"""
import argparse
import colorsys
import io
import os
import re
import zipfile

from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "ChessFight", "RagdollLab", "Resources", "PawnRushSkillFx")

# Must match SheetList in PawnRushSkillFx.cs (frames per sheet).
SHEETS = ["queen_shockwave_hit_sparks", "rook_stop_floor_ring", "bishop_tripwire_lightup", "bishop_tripwire_hold",
          "bishop_tripwire_preview", "bishop_trip_pop", "knight_stomp_sparks", "knight_stomp_dizzy_stars",
          "knight_land_dust_ring"]


def hexc(s):
    return tuple(int(s[i:i + 2], 16) for i in (1, 3, 5))


NAVY = hexc("#0D1430")     # the HUD's ink
WALNUT = hexc("#4A3626")   # darker than the course's walnut squares (#8C613D)


def outline(fr, color, strength, grow=1):
    """A dark rim just outside the drawing: thin streaks and pale rings stay readable on any background."""
    rim = fr.getchannel("A").filter(ImageFilter.MaxFilter(grow * 2 + 1)).point(lambda v: int(v * strength))
    layer = Image.new("RGBA", fr.size, color + (0,))
    layer.putalpha(rim)
    return Image.alpha_composite(layer, fr)


def gold(fr, sat=0.6, fill_alpha=0.8):
    """Queen: the pale cream fill and the fading outer ring (which turned khaki) pushed back to gold."""
    out = fr.copy()
    px = out.load()
    for y in range(fr.size[1]):
        for x in range(fr.size[0]):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if v > 0.45:
                h = 0.115 + (h - 0.115) * 0.3
                s = max(s, sat * min(1.0, (v - 0.45) / 0.35))
                if s < 0.45 and v > 0.9:
                    a = int(a * fill_alpha)   # the cream fill: a gold tint, not a blotch
            r2, g2, b2 = colorsys.hsv_to_rgb(h, s, v)
            px[x, y] = (int(r2 * 255), int(g2 * 255), int(b2 * 255), a)
    return out


def ramp(fr, colors, lo=0.0, hi=1.0):
    """Recolour by brightness along a ramp of colours (dark to light), alpha kept."""
    out = fr.copy()
    px = out.load()
    for y in range(fr.size[1]):
        for x in range(fr.size[0]):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            v = (0.3 * r + 0.55 * g + 0.15 * b) / 255
            v = min(1.0, max(0.0, (v - lo) / max(1e-3, hi - lo)))
            seg = v * (len(colors) - 1)
            i = min(int(seg), len(colors) - 2)
            t = seg - i
            c0, c1 = colors[i], colors[i + 1]
            px[x, y] = tuple(int(round(c0[k] + (c1[k] - c0[k]) * t)) for k in range(3)) + (a,)
    return out


def tune(name, frames):
    """For the game screen: a white test floor, the course's cream and walnut squares (#EDE0C2 / #8C613D) and a
    pale sky behind. The first film (R78) showed what got lost; shapes and timing stay the workshop's."""
    if name == "queen_shockwave":
        return [gold(f) for f in frames]
    if name in ("queen_shockwave_hit_sparks", "rook_hit_sparks"):
        return [outline(f, NAVY, 0.55) for f in frames]
    if name == "knight_stomp_sparks":
        # Pale sky blue vanished against the sky and a white floor: a deeper blue body, a white-hot core, a navy rim.
        deeper = [ramp(f, [hexc("#123E7A"), hexc("#1F7FD6"), hexc("#5CC7FF"), hexc("#E6F7FF")], 0.25, 0.95) for f in frames]
        return [outline(f, NAVY, 0.55) for f in deeper]
    if name == "knight_land_dust_ring":
        # Cream dust on cream squares could not be seen: a walnut rim around a light core reads on both squares.
        return [outline(f, WALNUT, 0.7) for f in frames]
    return frames


def pack(name, frames):
    frames = tune(name, frames)
    n = len(frames)
    w, h = frames[0].size
    cols = min(n, 8)
    rows = (n + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * w, rows * h), (0, 0, 0, 0))
    for i, fr in enumerate(frames):
        sheet.paste(fr, ((i % cols) * w, (i // cols) * h))
    sheet.save(os.path.join(OUT, name + ".png"), optimize=True)
    print(f"{name}: {n} frames {w}px, grid {cols}x{rows}, {sheet.size[0]}x{sheet.size[1]}")


def frames_in_zip(z, name):
    """The numbered frames of one effect (<name>/<name>_00.png …), in order."""
    pattern = re.compile(rf"(^|/){re.escape(name)}/{re.escape(name)}_(\d+)\.png$")
    found = sorted((int(m.group(2)), path) for path in z.namelist() if (m := pattern.search(path)))
    if not found:
        raise SystemExit(f"no frames for {name} in the zip")
    return [Image.open(io.BytesIO(z.read(path))).convert("RGBA") for _, path in found]


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--zip", required=True)
    ap.add_argument("--queen", required=True)
    ap.add_argument("--queen-frames", type=int, default=20)
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)

    strip = Image.open(args.queen).convert("RGBA")
    size = strip.size[0] // args.queen_frames
    pack("queen_shockwave", [strip.crop((i * size, 0, i * size + size, strip.size[1])) for i in range(args.queen_frames)])
    with zipfile.ZipFile(args.zip) as z:
        for name in SHEETS:
            pack(name, frames_in_zip(z, name))
        # The rook had only stars on its hits (taken out, R78): its hits get the queen's sparks in the rook's orange.
        queen_sparks = frames_in_zip(z, "queen_shockwave_hit_sparks")
    pack("rook_hit_sparks", [ramp(f, [hexc("#B5481A"), hexc("#FF8A3D"), hexc("#FFD2A8"), hexc("#FFFFFF")], 0.35, 1.0)
                             for f in queen_sparks])


if __name__ == "__main__":
    main()
