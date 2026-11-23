# Royal Carriage Yard: worn copies of library tiles (11-23, 승규 on the final gate's leaves: "너무 흠집이 없는 문이야
# 약간의 스크래치 낡은 문 컨셉으로 해줘").
#
#   blender -b --factory-startup --python textures/rcy_worn.py
#   then: <Blender python> -I textures/rcy_unity_materials.py RCY_Oldwalnut,RCY_Oldiron
#
# Reads textures/library/RCY_Walnut and RCY_Iron (rcy_textures.py's tiles) and writes RCY_Oldwalnut and RCY_Oldiron
# next to them (and their rcy_textures.json entries): the same wood and iron, aged. Wood: sun-bleached grey patches,
# grime, light scratches across the grain (fresh wood showing, a groove in the normal map), a few dark gouges, small
# dents. Iron: rust blooms (orange-brown, rough), bright scratches, pits. Every mark is drawn wrapped, so the tiles stay
# seamless. The slots "RCY | oldwalnut" / "RCY | oldiron" (movers_model.py's final gate leaves) find them by name.

import json, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
LIB = os.path.join(HERE, "library")
ns = {"__name__": "rcy_textures", "__file__": os.path.join(os.path.dirname(HERE), "rcy_textures.py")}
exec(open(ns["__file__"], encoding="utf-8").read(), ns)
fbm, blur, smooth, s01, hexc, mix, save_png, worley = (ns[k] for k in ("fbm", "blur", "smooth", "s01", "hexc", "mix",
                                                                         "save_png", "worley"))


def load4(path):
    import bpy
    img = bpy.data.images.load(path, check_existing=False)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)[::-1].copy()


def lines(n, seed, count, length, width, angle, spread):
    """A mask of `count` thin wrapped strokes: lengths in `length` px, angles round `angle` (radians) +- spread, each
    fading in and out along its length; width in px (drawn as stamps, then softened)."""
    r = np.random.default_rng(seed)
    m = np.zeros((n, n), np.float32)
    for _ in range(count):
        x, y = r.uniform(0, n, 2)
        L = r.uniform(*length)
        a = angle + r.uniform(-spread, spread)
        bend = r.uniform(-.15, .15)
        k = max(2, int(L * 1.5))
        t = np.linspace(0, 1, k)
        px = x + np.cos(a + bend * t) * L * t
        py = y + np.sin(a + bend * t) * L * t
        v = (np.sin(np.pi * t) ** .5 * r.uniform(.5, 1.0)).astype(np.float32)
        wd = r.uniform(*width)
        for ox in np.arange(-(wd - 1) / 2, (wd - 1) / 2 + .01, 1.0) if wd > 1 else (0,):
            ix = np.round(px - np.sin(a) * ox).astype(int) % n
            iy = np.round(py + np.cos(a) * ox).astype(int) % n
            np.maximum.at(m, (iy, ix), v)
    return np.clip(blur(m, .6) * 1.6, 0, 1)


def add_height(nrm, h, strength):
    """The library normal map with a height field's slopes added (OpenGL +Y, as rcy_textures.normal_map)."""
    n = nrm[..., :3] * 2 - 1
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * .5 * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * .5 * strength
    n = n / np.clip(n[..., 2:3], .2, None)
    n = np.stack([n[..., 0] - dx, n[..., 1] + dy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * .5 + .5


def lum(c):
    return c @ np.array([.2126, .7152, .0722], np.float32)


def old_walnut(alb, nrm):
    n = alb.shape[0]
    col, sm = alb[..., :3].copy(), alb[..., 3].copy()
    # Sun and rain: broad patches bleached towards a dry grey-brown, and grime in others.
    bleach = smooth(s01(fbm(9101, 1.5, 6, 1.2), -.2, 1.6))
    col = mix(col, lum(col)[..., None] * .55 + hexc("#8C7560") * .45, .3 * bleach[..., None])
    grime = smooth(s01(fbm(9102, 1, 4, 1.0), .4, 2.2))
    col *= (1 - .16 * grime)[..., None]
    # Weathered grain: the soft wood between the lines worn down a little (stretched along the grain, U).
    worn = fbm(9103, 6, 60, .8, stretch=4.0)
    col *= (1 + .06 * worn)[..., None]
    # Scratches across the grain: pale fresh wood, a shallow groove.
    sc = lines(n, 9104, 110, (25, 170), (1, 2), math.pi / 2, .9)
    fresh = col * 1.25 + hexc("#B88A5E") * .25
    col = mix(col, fresh, .75 * sc[..., None])
    # Gouges: few, wider and dark (dirt in them).
    go = lines(n, 9105, 14, (40, 120), (3, 5), math.pi / 2, .6)
    col = mix(col, col * .45, .8 * go[..., None])
    # Dents: small dark dimples.
    f1, _ = worley(9106, 70)
    dent = np.clip(1 - f1 / 5.0, 0, 1) ** 2
    col *= (1 - .3 * dent)[..., None]
    h = -1.2 * sc - 3.0 * go - 1.5 * dent + .4 * worn
    sm = np.clip(sm * .75 - .08 * bleach + .05 * sc, 0, 1)
    return np.clip(col, 0, 1), sm, add_height(nrm, h, 1.6)


def old_iron(alb, nrm):
    n = alb.shape[0]
    col, sm = alb[..., :3].copy(), alb[..., 3].copy()
    # Rust: blooms with ragged edges, deep orange-brown to dark crust.
    bloom = fbm(9201, 3, 24, 1.1) + .45 * fbm(9202, 24, 120, .7)
    rust = smooth(s01(bloom, 1.0, 2.2))
    shade = smooth(s01(fbm(9203, 8, 60, .9), -1.5, 1.5))
    rc = mix(hexc("#5A2A14"), hexc("#9A5128"), shade[..., None])
    col = mix(col, rc, .85 * rust[..., None])
    # Bright scratches through the paint and rust.
    sc = lines(n, 9204, 120, (15, 110), (1, 1.5), 0.0, math.pi)
    col = mix(col, hexc("#8E8C88"), .7 * sc[..., None] * (1 - .6 * rust[..., None]))
    # Pits in the rust.
    f1, _ = worley(9205, 260)
    pit = np.clip(1 - f1 / 2.5, 0, 1) * rust
    col *= (1 - .35 * pit)[..., None]
    h = 1.5 * rust * (.5 + .5 * shade) - 1.0 * sc - 2.0 * pit
    sm = np.clip(sm * (1 - .75 * rust) + .25 * sc, 0, 1)
    return np.clip(col, 0, 1), sm, add_height(nrm, h, 1.4)


def main():
    with open(os.path.join(LIB, "rcy_textures.json"), encoding="utf-8") as f:
        info = json.load(f)
    for src, dst, slot, fn in (("RCY_Walnut", "RCY_Oldwalnut", "RCY | oldwalnut", old_walnut),
                               ("RCY_Iron", "RCY_Oldiron", "RCY | oldiron", old_iron)):
        alb = load4(os.path.join(LIB, src + "_Albedo.png"))
        nrm = load4(os.path.join(LIB, src + "_Normal.png"))
        col, sm, nm = fn(alb, nrm)
        save_png(np.concatenate([col, sm[..., None]], -1).astype(np.float32), os.path.join(LIB, dst + "_Albedo.png"))
        save_png(nm.astype(np.float32), os.path.join(LIB, dst + "_Normal.png"))
        spec = dict(info[src])
        spec.update(slot=slot, smoothness=float(sm.mean()),
                    mean="#%02X%02X%02X" % tuple(int(round(c * 255)) for c in col.reshape(-1, 3).mean(0)),
                    source="textures/library/%s (aged by textures/rcy_worn.py)" % src)
        info[dst] = spec
        print("RCY_WORN", dst, spec)
    with open(os.path.join(LIB, "rcy_textures.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, indent=1)


main()
