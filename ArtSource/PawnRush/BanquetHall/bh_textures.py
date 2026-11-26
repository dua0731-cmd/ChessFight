# Grand banquet hall material library (B0, seoungtest R837, 2026-11-25; PLAN.md B0, 2 - slot names).
#
#   blender -b --factory-startup --python bh_textures.py -- [out folder] [names] [size]
#
# Into textures/library (+ bh_textures.json): BH_<Name>_Albedo.png (sRGB colour, alpha = smoothness) and _Normal.png
# (OpenGL +Y), one seamless tile a material - the slots of bh_kit.SLOTS. Source: the Codex swatch sheet
# Docs/PawnRush/ConceptArt/R836_BanquetHallAssets/AssetRefs/BH_T1_material_swatches.png (3 x 4 swatches on white,
# row-major: cream stone, marble, walnut, gold damask / silver, linen, crown runner, olive velvet / red velvet,
# chessboard, chocolate, ivory), each cropped and made to tile the way the observatory's ro_textures.py does (Moisan
# periodic split + edge blend, broad light drift flattened, pulled toward the concept's colour).
# Tile = metres one tile covers on the models' metre UVs (Unity tiling = 1 / tile).
# Materials not on the sheet: BH_FloorLight / BH_FloorDark (the dance floor's squares, from the chessboard swatch's own
# light and dark squares, measured, bodies from the marble and walnut swatches), BH_Ebony (black pieces: measured from
# nothing - a near-black lacquer with the walnut's grain faintly), BH_Crystal (no texture; bh_unity_materials writes
# it clear).

import json, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels"))
sys.path.insert(0, os.path.join(HERE, "..", "RoyalObservatory"))
import rcy_textures as rt   # noqa: E402  (helpers only)

T1 = os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R836_BanquetHallAssets", "AssetRefs",
                  "BH_T1_material_swatches.png")
N = 1024
rt.N = N

# name: (T1 swatch (row-major 0..11) or None, target colour (sRGB, the concept's), tile m, metallic, normal strength,
#        smoothness, smoothness variation, inset (0..0.5 of the swatch cut off each side before tiling))
LIBRARY = {
    "BH_Stone": (0, "#E2CDA4", 2.4, 0.0, 1.4, .30, .06, .04),        # cream ashlar, a block about 0.6 m tall
    "BH_Marble": (1, "#EFE3CF", 2.5, 0.0, .5, .78, .05, .05),        # cream marble, honey veins (the dais's faces)
    "BH_Walnut": (2, "#5A3220", 1.6, 0.0, .7, .55, .08, .05),        # walnut posts, beams, chairs
    "BH_Gold": (3, "#E0A83C", .8, 1.0, .12, .86, .03, .03),          # polished gilt (ware, trims): the swatch's colour, its damask flattened away
    "BH_GoldDamask": (3, "#D6A94A", .8, 1.0, .9, .78, .06, .03),     # gilded with a damask chased in it (panels, bands)
    "BH_Silver": (4, "#C4C6C8", 1.2, 1.0, .35, .80, .05, .05),       # brushed silver (cloches, platters)
    "BH_Linen": (5, "#F0EBDF", .8, 0.0, .6, .18, .04, .05),          # white damask linen (tablecloths)
    "BH_Runner": (6, "#55642A", 1.5, 0.0, .8, .22, .05, .17),        # olive runner, gold crowns (its gold edges cut off: the model's trims)
    "BH_Olive": (7, "#4E5E22", 1.0, 0.0, 1.0, .20, .06, .05),        # olive velvet (chair seats, banners' ground)
    "BH_Velvet": (8, "#7A1018", 1.0, 0.0, 1.0, .20, .06, .05),       # red velvet (the stage curtain, carpets)
    "BH_FloorLight": (1, "#EDE2CC", 2.5, 0.0, .5, .82, .04, .05),    # the dance floor's light squares (marble body)
    "BH_FloorDark": (2, "#5B3422", 2.0, 0.0, .6, .70, .05, .05),     # its dark squares (walnut body, polished)
    "BH_Chocolate": (10, "#4A2418", 1.5, 0.0, .3, .86, .03, .05),    # glossy chocolate
    "BH_Wax": (11, "#F2E6C8", 1.0, 0.0, .3, .45, .04, .05),          # candle wax, cream
    "BH_Ivory": (11, "#EDE3CC", 1.0, 0.0, .3, .62, .04, .05),        # white pieces' ivory
    "BH_Ebony": (None, "#17110E", 1.0, 0.0, .4, .70, .05, .05),      # black pieces' ebony lacquer
}
PULL = .6
CHESSBOARD = 9   # the swatch whose squares give the floor's two colours


def swatches():
    img = rt.load_png(T1)
    boxes = cells_on_white(img, 3, 4)
    return img, boxes


def cells_on_white(img, rows, cols, white=.90):
    """Bounding boxes (y0, y1, x0, x1) of the rows x cols non-white blocks of a sheet, row-major."""
    ink = (img.min(-1) < white)

    def runs(profile, want):
        on = profile > profile.max() * .25
        r, start = [], None
        for i, v in enumerate(list(on) + [False]):
            if v and start is None:
                start = i
            if not v and start is not None:
                r.append((start, i)); start = None
        r.sort(key=lambda a: a[1] - a[0], reverse=True)
        return sorted(r[:want])
    ys = runs(ink.sum(1), rows)
    boxes = []
    for (y0, y1) in ys:
        xs = runs(ink[y0:y1].sum(0), cols)
        for (x0, x1) in xs:
            boxes.append((y0, y1, x0, x1))
    return boxes


def crop(img, box, inset):
    y0, y1, x0, x1 = box
    iy, ix = int((y1 - y0) * inset), int((x1 - x0) * inset)
    return img[y0 + iy:y1 - iy, x0 + ix:x1 - ix]


def chess_colours(img, box):
    """The chessboard swatch's light and dark squares' median colours (4 x 4 squares)."""
    c = crop(img, box, .02)
    h, w = c.shape[:2]
    light, dark = [], []
    for r in range(4):
        for k in range(4):
            sq = c[int((r + .25) * h / 4):int((r + .75) * h / 4), int((k + .25) * w / 4):int((k + .75) * w / 4)]
            (light if (r + k) % 2 == 0 else dark).append(np.median(sq.reshape(-1, 3), 0))
    return np.median(light, 0), np.median(dark, 0)


def tile_from(src, size):
    src = rt.resample(src, size)
    col = rt.edge_blend(np.stack([rt.periodic(src[..., c]) for c in range(3)], -1))
    lum = col @ np.array([.2126, .7152, .0722], np.float32)
    broad = rt.blur(lum, size / 6.0)
    col *= (lum.mean() / np.clip(broad, 1e-3, None))[..., None] ** .7
    return np.clip(col, 0, 1)


def build_material(name, out, size, img, boxes):
    idx, target, tile, metal, strength, smooth0, smooth_var, inset = LIBRARY[name]
    source = "BH_T1 swatch %d" % (idx + 1) if idx is not None else "procedural"
    if name in ("BH_FloorLight", "BH_FloorDark"):
        light, dark = chess_colours(img, boxes[CHESSBOARD])
        target = "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in (light if name == "BH_FloorLight" else dark))
        source += " body, colour of the chessboard swatch's %s squares" % ("light" if name == "BH_FloorLight" else "dark")
    if idx is not None:
        col = tile_from(crop(img, boxes[idx], inset), size)
    else:   # ebony: the walnut's grain, near black
        col = tile_from(crop(img, boxes[2], .05), size)
        lum = col @ np.array([.2126, .7152, .0722], np.float32)
        col = np.repeat(((lum - lum.mean()) * .35 + lum.mean())[..., None], 3, -1)
    if name == "BH_Gold":   # plain polished: keep only a whisper of the damask
        mean = col.reshape(-1, 3).mean(0)
        col = mean + (col - mean) * .1
    tgt = rt.hexc(target)
    pull = PULL if idx is not None else 1.0
    if name in ("BH_FloorLight", "BH_FloorDark", "BH_Ivory"):
        pull = .9
    col *= (1 + pull * (tgt / np.clip(col.reshape(-1, 3).mean(0), 1e-3, None) - 1))[None, None, :]
    col = np.clip(col, 0, 1).astype(np.float32)
    lum = col @ np.array([.2126, .7152, .0722], np.float32)
    h = (lum - rt.blur(lum, 12.0)) * 8 + (rt.blur(lum, 3.0) - rt.blur(lum, 24.0)) * 6
    if name == "BH_Stone":   # the joints sink: darker than the block faces
        h = h + (lum - rt.blur(lum, 40.0)) * 10
    detail = np.clip(.5 + (lum - rt.blur(lum, 8.0)) * 4, 0, 1)
    sm = np.clip(smooth0 + smooth_var * (detail - .5) * 2, 0, 1)
    if name == "BH_GoldDamask":   # the damask's raised scrolls shine, the ground between them is matter
        sm = np.clip(smooth0 + .18 * (detail - .5) * 2, 0, 1)
    alb = np.concatenate([col, sm[..., None]], -1).astype(np.float32)
    rt.save_png(alb, os.path.join(out, name + "_Albedo.png"))
    rt.save_png(rt.normal_map(h, strength), os.path.join(out, name + "_Normal.png"))
    rgb = col.reshape(-1, 3).mean(0)
    return {"tile": tile, "metallic": metal, "smoothness": float(sm.mean()), "size": size,
            "mean": "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in rgb), "target": target, "source": source}


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv and argv[0] else os.path.join(HERE, "textures", "library")
    names = argv[1].split(",") if len(argv) > 1 and argv[1] else list(LIBRARY)
    size = int(argv[2]) if len(argv) > 2 else N
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, "bh_textures.json")
    info = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}
    img, boxes = swatches()
    assert len(boxes) == 12, boxes
    for nm in names:
        got = build_material(nm, out, size, img, boxes)
        print("BH_TEX", nm, got)
        info[nm] = got
    json.dump(info, open(path, "w", encoding="utf-8"), indent=1)
