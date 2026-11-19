# Royal Library material library (R0, seoungtest R687, 2026-11-17, 승규 "일단 이 사진과 똑같게 해상도로 구워" - the eight
# swatches at the bottom of GC_D6_props_materials.png, copied to textures/swatch_row_target.png).
#
#   blender -b --factory-startup --python rl_textures.py -- [out folder] [name,name...] [size]
#
# Codex albedo (textures/codex/r0, PROMPT.md there) -> one seamless tile a material: <Name>_Albedo.png (sRGB colour,
# alpha = smoothness) and <Name>_Normal.png (OpenGL +Y). Same treatment as the carriage yard library (rcy_textures.py,
# whose helpers this reuses): Moisan periodic split + edge blend so it tiles, broad light drift flattened, mean colour
# pulled to the swatch's measured colour, height from the albedo's own detail -> normal, smoothness around a base.
# Tile = metres one tile covers on the models' metre UVs (Unity tiling = 1 / tile).

import json, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels"))
import rcy_textures as rt   # noqa: E402  (helpers only; its own library is not touched)

CODEX = os.path.join(HERE, "textures", "codex", "r0")
N = 1024

# name: (codex file, swatch target colour (measured on swatch_row_target.png), tile m, metallic, normal strength,
#        smoothness, smoothness variation, isotropic)
LIBRARY = {
    "RL_CreamStone": ("rl_cream_stone.png", "#E8C397", 2.0, 0.0, 1.6, .28, .06, False),
    "RL_Walnut": ("rl_walnut.png", "#7D4323", 2.0, 0.0, 1.6, .38, .08, False),
    "RL_VarnishedWalnut": ("rl_varnished_walnut.png", "#69371C", 2.0, 0.0, 1.0, .72, .08, False),
    "RL_Brass": ("rl_polished_brass.png", "#C58B3C", 1.0, .95, .6, .74, .05, False),   # Codex's brass is even (no brushing), so no isotropic mottle (it read as dirty blotches, 11-17)
    "RL_RedLeather": ("rl_red_leather.png", "#852B20", 1.0, 0.0, 1.4, .42, .08, False),
    "RL_Parchment": ("rl_parchment.png", "#F5D19D", 2.0, 0.0, .8, .14, .04, False),
    "RL_InkGlass": ("rl_ink_black_glass.png", "#231919", 1.0, 0.0, .3, .92, .03, False),
    "RL_CreamCloth": ("rl_cream_cloth.png", "#DDB88B", 1.0, 0.0, 1.2, .12, .03, False),
    # R1 11-18: the dimensional entrance's white marble (GC_assets_04: arch ~#EADAC7 ~ #F3E5D5, floor ~#E0D4C9 with
    # pale grey veins), whiter and less yellow than the cream stone; the carriage yard's Codex marble (rcy_white_marble).
    "RL_Marble": ("rl_white_marble.png", "#EBDECF", 3.0, 0.0, .8, .55, .05, False),
}
PULL = .85   # how far the mean colour moves to the swatch's (1 = all the way)
# Mid-band pattern gain, from the swatch-vs-tile measurement (textures/swatch_vs_texture.png, 11-17): walnut's figure
# and the parchment's blotches were about twice the swatch's, the leather's pebbling about half. Brass, glass and cloth
# are left: their swatches' variation is mostly reflection and folds, which the model and Unity's light draw.
GAIN = {"RL_Walnut": .4, "RL_Parchment": .4, "RL_RedLeather": 2.6, "RL_CreamStone": .85, "RL_VarnishedWalnut": .75}


def from_codex(name, size):
    fname, target, tile, metal, strength, smooth0, smooth_var, iso = LIBRARY[name]
    path = os.path.join(CODEX, fname)
    if not os.path.exists(path):
        return None
    src = rt.load_png(path)
    src = rt.resample(src, size)
    col = rt.edge_blend(np.stack([rt.periodic(src[..., c]) for c in range(3)], -1))
    if iso:   # a brushing direction would read as grain on a part whose U runs across
        col = np.stack([rt.blur(col[..., c], 4.0) for c in range(3)], -1)
        seed = sum(map(ord, name))
        mottle = .6 * rt.fbm(seed, 6, 40, 1.0, n=size) + .4 * rt.fbm(seed + 1, 40, 160, .6, n=size)
        col *= (1 + .06 * mottle)[..., None]
    lum = col @ np.array([.2126, .7152, .0722], np.float32)
    broad = rt.blur(lum, size / 6.0)
    col *= (lum.mean() / np.clip(broad, 1e-3, None))[..., None] ** .7
    # The swatch colours are sRGB display values; load_png reads the file's numbers as they are (no conversion), so
    # both sides are in the same (sRGB) numbers.
    tgt = rt.hexc(target)
    col *= (1 + PULL * (tgt / np.clip(col.reshape(-1, 3).mean(0), 1e-3, None) - 1))[None, None, :]
    # Pattern contrast to the swatch's ("더 다듬어", 11-17): the band between ~3 and ~40 px (a swatch's visible figure at
    # the view a swatch shows, about a third of a 2 m tile) scaled by GAIN, measured against the swatch crops.
    g = GAIN.get(name, 1.0)
    if g != 1.0:
        lo = np.stack([rt.blur(col[..., c], 40.0) for c in range(3)], -1)
        mid = np.stack([rt.blur(col[..., c], 3.0) for c in range(3)], -1) - lo
        col = col + (g - 1.0) * mid
    col = np.clip(col, 0, 1)
    lum = col @ np.array([.2126, .7152, .0722], np.float32)
    h = (lum - rt.blur(lum, 12.0)) * 8 + (rt.blur(lum, 3.0) - rt.blur(lum, 24.0)) * 6
    detail = np.clip(.5 + (lum - rt.blur(lum, 8.0)) * 4, 0, 1)
    sm = np.clip(smooth0 + smooth_var * (detail - .5) * 2, 0, 1)
    return col, sm, h, (tile, metal, strength, path)


def build(name, out, size):
    got = from_codex(name, size)
    if got is None:
        return None
    col, sm, h, (tile, metal, strength, source) = got
    alb = np.concatenate([col, sm[..., None]], -1).astype(np.float32)
    nrm = rt.normal_map(h, strength)
    rt.save_png(alb, os.path.join(out, name + "_Albedo.png"))
    rt.save_png(nrm, os.path.join(out, name + "_Normal.png"))
    rgb = alb[..., :3].reshape(-1, 3).mean(0)
    got = {"tile": tile, "metallic": metal, "smoothness": float(alb[..., 3].mean()), "size": size,
           "mean": "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in rgb), "target": LIBRARY[name][1],
           "source": os.path.relpath(source, HERE).replace("\\", "/")}
    if name in DETAIL:
        got["detail"] = build_detail(name, out)
    return got

# ------------------------------------------------------------------ detail maps (승규 "디테일 맵 추가 해", 11-18)
# URP Lit's detail slot: a small tile repeated many times over the base tile, so the surface stays crisp where the
# camera comes close (tiny pawns on giant furniture). Albedo = linear grey, 0.5 = no change (URP multiplies by 2x the
# sample); normal blended over the base normal. Procedural (periodic numpy noise from rcy_textures), 512 px.
#   name: (detail tile m, pattern, albedo amplitude, normal strength)
# The ink glass gets none: it should stay a clean lacquer.
DN = 512
DETAIL = {
    "RL_CreamStone": (.5, "grain", .07, 2.2),
    "RL_Walnut": (.5, "fibre", .11, 2.4),
    "RL_VarnishedWalnut": (.5, "fibre", .06, 1.2),
    "RL_Brass": (.25, "hairline", .03, .9),
    "RL_RedLeather": (.2, "pebble", .07, 2.4),
    "RL_Parchment": (.4, "paper", .04, .9),
    "RL_CreamCloth": (.1, "weave", .05, 1.5),
    "RL_Marble": (.5, "grain", .025, .9),
}


def detail_height(name, kind):
    s = sum(map(ord, name)) + 101
    n = DN
    if kind == "grain":     # sandstone grains + scattered pores
        d1, _ = rt.worley(s, 900, n)
        pores = -np.clip(1 - d1 / 2.2, 0, 1) * 2.5
        return .7 * rt.fbm(s, 60, 256, .4, n=n) + .3 * rt.fbm(s + 1, 12, 60, .8, n=n) + pores
    if kind == "fibre":     # wood fibres along the grain (U = the part's long axis, as the base's grain)
        return .75 * rt.fbm(s, 30, 256, .5, stretch=10.0, n=n) + .25 * rt.fbm(s + 1, 8, 40, .9, stretch=5.0, n=n)
    if kind == "hairline":  # satin brushing
        return rt.fbm(s, 40, 256, .2, stretch=40.0, n=n) + .15 * rt.fbm(s + 1, 4, 20, 1.0, n=n)
    if kind == "pebble":    # leather pebbling: rounded cells with creases between, small pores
        d1, d2 = rt.worley(s, 260, n)
        cells = np.sqrt(np.clip(d2 - d1, 0, None))
        cells = (cells - cells.mean()) / (cells.std() + 1e-6)
        return cells + .25 * rt.fbm(s + 1, 80, 256, .3, n=n)
    if kind == "paper":     # paper tooth: soft isotropic grain at two scales (crossed fibres read as cloth, 11-18)
        return .6 * rt.fbm(s, 50, 256, .5, n=n) + .4 * rt.fbm(s + 2, 10, 50, .9, n=n)
    if kind == "weave":     # 2/2 twill
        c = n // 64   # 64 threads a 10 cm tile: a fine wool, under the base's twill
        y, x = np.mgrid[0:n, 0:n]
        over = ((x // c + y // c) // 2) % 2 == 0   # twill: floats step one thread a row
        hx = np.sin(np.pi * (y % c) / c)            # weft thread across
        hy = np.sin(np.pi * (x % c) / c)            # warp thread along
        h = np.where(over, hy, hx).astype(np.float32)
        return (h - h.mean()) / (h.std() + 1e-6) + .2 * rt.fbm(s, 60, 256, .3, n=n)
    raise ValueError(kind)


def build_detail(name, out):
    tile, kind, amp, strength = DETAIL[name]
    h = detail_height(name, kind).astype(np.float32)
    h = (h - h.mean()) / (h.std() + 1e-6)
    g = np.clip(.5 + amp * np.clip(h, -2.5, 2.5), 0, 1)
    alb = np.stack([g, g, g, np.ones_like(g)], -1)
    rt.save_png(alb, os.path.join(out, name + "_DetailAlbedo.png"))
    rt.save_png(rt.normal_map(h, strength), os.path.join(out, name + "_DetailNormal.png"))
    return {"tile": tile, "pattern": kind, "albedo_amp": amp, "normal_strength": strength, "size": DN}


# ------------------------------------------------------------------ procedural-only materials (no Codex source)
# R1 (11-18) asked for red sealing wax (the stamp's rubber face, the seal marks): a deep red with a soft poured mottle,
# glossy. 256 px is plenty for a near-flat colour.
#   name: (colour, tile m, metallic, smoothness, mottle amount, size)
PROCEDURAL = {
    "RL_RedWax": ("#8E1A16", .5, 0.0, .65, .06, 256),
    # R6 11-18: the white chess pieces' polished ivory (D6's piece row): creamy, nearly grainless, glossy.
    "RL_Ivory": ("#E6D3B1", 2.0, 0.0, .72, .035, 512),
    # 11-18 R7's teacup: black tea, a glossy deep amber surface (the varnished walnut showed grain)
    "RL_Tea": ("#6B300F", .5, 0.0, .90, .02, 256),
    # the plants session (11-18, GC_assets_02_plants): leaves get their veins from the shape, so these are near-flat colours
    "RL_Leaf": ("#35652C", 0.5, 0.0, 0.7, 0.07, 256),
    "RL_LeafLight": ("#619930", 0.5, 0.0, 0.55, 0.07, 256),
    "RL_Stem": ("#4B7630", 0.5, 0.0, 0.35, 0.06, 256),
    "RL_Soil": ("#35271D", 1, 0.0, 0.1, 0.18, 256),
    "RL_PetalCream": ("#EDE5CB", 0.5, 0.0, 0.45, 0.04, 256),
    "RL_PetalPink": ("#F3ADAD", 0.5, 0.0, 0.45, 0.05, 256),
    "RL_Ceramic": ("#E7D4AD", 2, 0.0, 0.8, 0.03, 256),
    "RL_Pollen": ("#EDCB59", 0.3, 0.0, 0.35, 0.08, 256),
}


def build_procedural(name, out):
    colour, tile, metal, smooth, amount, n = PROCEDURAL[name]
    s = sum(map(ord, name))
    m = .65 * rt.fbm(s, 2, 12, 1.0, n=n) + .35 * rt.fbm(s + 1, 12, 60, .7, n=n)
    col = rt.hexc(colour)[None, None, :] * (1 + amount * np.clip(m, -2.5, 2.5))[..., None]
    sm = np.clip(smooth + .04 * rt.fbm(s + 2, 20, 90, .5, n=n), 0, 1)
    alb = np.concatenate([np.clip(col, 0, 1), sm[..., None]], -1).astype(np.float32)
    rt.save_png(alb, os.path.join(out, name + "_Albedo.png"))
    rt.save_png(rt.normal_map(.6 * rt.fbm(s + 3, 4, 30, .9, n=n), .6), os.path.join(out, name + "_Normal.png"))
    rgb = alb[..., :3].reshape(-1, 3).mean(0)
    return {"tile": tile, "metallic": metal, "smoothness": float(sm.mean()), "size": n,
            "mean": "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in rgb), "target": colour, "source": "procedural"}


# ------------------------------------------------------------------ colour variants (R4 11-18: book spines)
# The red leather's albedo recoloured to another mean colour, its grain and smoothness kept; Unity shares the red
# leather's normal and detail maps (rl_unity_materials "share"), so a variant costs one albedo. The concept's shelves
# (GC_D1, GC_E1) mix brown, dark oxblood, tan and a cool dark grey among the red. No blue (team colour rule): "slate"
# is a near-neutral dark grey.
TINTS = {
    "RL_LeatherBrown": ("RL_RedLeather", "#6A3B20"),
    "RL_LeatherDark": ("RL_RedLeather", "#4A1B14"),
    "RL_LeatherTan": ("RL_RedLeather", "#A86E3C"),
    "RL_LeatherSlate": ("RL_RedLeather", "#4B4A4C"),
    # R6 11-18: the black pieces' polished ebony - the walnut darkened, its grain softened (contrast .45), glossy.
    #   (source, colour[, smoothness, grain contrast])
    "RL_Ebony": ("RL_Walnut", "#2B1D17", .70, .45),
    "RL_Bark": ("RL_Walnut", "#61452C", .25, 1.0),   # plants 11-18: the fiddle-leaf fig's trunk
}


def build_tint(name, out):
    import bpy
    src, colour = TINTS[name][:2]
    smooth = TINTS[name][2] if len(TINTS[name]) > 2 else None
    contrast = TINTS[name][3] if len(TINTS[name]) > 3 else 1.0
    img = bpy.data.images.load(os.path.join(out, src + "_Albedo.png"), check_existing=False)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    a = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    a = a.reshape(h, w, 4)[::-1].copy()
    rgb = a[..., :3]
    lum = rgb @ np.array([.2126, .7152, .0722], np.float32)
    tgt = rt.hexc(colour)
    # keep the grain as a brightness ratio round the mean, take the hue from the target
    rel = (lum / max(float(lum.mean()), 1e-4)) ** contrast
    a[..., :3] = np.clip(tgt[None, None, :] * rel[..., None], 0, 1)
    if smooth is not None:   # move the smoothness (alpha) to the new mean, its variation kept
        a[..., 3] = np.clip(a[..., 3] - a[..., 3].mean() + smooth, 0, 1)
    rt.save_png(a, os.path.join(out, name + "_Albedo.png"))
    info = json.load(open(os.path.join(out, "rl_textures.json"), encoding="utf-8"))[src]
    got = dict(info)
    if smooth is not None:
        got["smoothness"] = float(a[..., 3].mean())
    got.update({"mean": "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in a[..., :3].reshape(-1, 3).mean(0)),
                "target": colour, "source": "tint of " + src, "share": src})
    return got


# ------------------------------------------------------------------ paintings (11-18 승규 "초상화 그림 텍스쳐 작업")
# A whole picture, not a tile: the canvas face is UV-mapped 0..1 (not metre UVs), the material's tiling 1.
#   name: (source image, canvas aspect w / h, smoothness, brushstroke relief)
PAINTINGS = {
    "RL_Portrait": (os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R687_GiantChessLibrary",
                                 "AssetRefs", "GC_portrait_painting.png"), 24.0 / 30.0, .32, .8),
}


def build_painting(name, out):
    import bpy
    path, aspect, smooth, relief = PAINTINGS[name]
    img = bpy.data.images.load(os.path.abspath(path), check_existing=False)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    a = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    a = a.reshape(h, w, 4)[::-1, :, :3]
    ch = int(round(w / aspect))                  # crop the height to the canvas's aspect, centred
    y0 = max(0, (h - ch) // 2)
    col = a[y0:y0 + ch]
    lum = col @ np.array([.2126, .7152, .0722], np.float32)
    k = 4.0
    blur = lum.copy()
    for ax in (0, 1):                             # a cheap box blur for the brushstroke height
        blur = (np.roll(blur, 2, ax) + np.roll(blur, 1, ax) + blur + np.roll(blur, -1, ax) + np.roll(blur, -2, ax)) / 5
    h_ = (lum - blur) * k
    sm = np.full(lum.shape, smooth, np.float32)
    alb = np.concatenate([col, sm[..., None]], -1).astype(np.float32)
    rt.save_png(alb, os.path.join(out, name + "_Albedo.png"))
    rt.save_png(rt.normal_map(h_, relief), os.path.join(out, name + "_Normal.png"))
    rgb = col.reshape(-1, 3).mean(0)
    return {"tile": 1.0, "metallic": 0.0, "smoothness": smooth, "size": [int(col.shape[1]), int(col.shape[0])],
            "mean": "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in rgb), "target": "", "source": "painting",
            "uv": "0..1 over the canvas"}


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv and argv[0] else os.path.join(HERE, "textures", "library")
    names = argv[1].split(",") if len(argv) > 1 and argv[1] else list(LIBRARY) + list(PROCEDURAL) + list(TINTS) + list(PAINTINGS)
    size = int(argv[2]) if len(argv) > 2 else N
    os.makedirs(out, exist_ok=True)
    path = os.path.join(out, "rl_textures.json")
    info = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}
    for nm in names:
        got = (build_procedural(nm, out) if nm in PROCEDURAL else build_tint(nm, out) if nm in TINTS
               else build_painting(nm, out) if nm in PAINTINGS else build(nm, out, size))
        print("RL_TEX", nm, got)
        if got:
            info[nm] = got
    json.dump(info, open(path, "w", encoding="utf-8"), indent=1)
