# Tutorial map textures (R834): tileable maps for the geometry made in Blender (floors, training walls, moat walls).
# Plain numpy, no external images, so they rebuild the same every time (fixed seeds).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python tu_textures_v1.py
#
# Materials/Textures/
#   TU_CreamBlock_BaseColor.png   2 x 2 m: cream dressed stone, 1.0 x 0.5 m blocks in running bond, soft veins
#   TU_MarbleCream_BaseColor.png  one 2.5 m chess square, warm cream marble with beige veins
#   TU_MarbleWalnut_BaseColor.png one 2.5 m chess square, dark walnut-brown marble with lighter veins
#   TU_Paving_BaseColor.png       2 x 2 m: cream paving slabs 1 x 1 m (floors that are not the chessboard)
#   TU_Walnut_BaseColor.png       1 x 4 m: polished dark walnut planks along the long side (the balance beam)
#   TU_MoatFloor_BaseColor.png    4 x 4 m: worn grey-beige flagstones far below (the dry moat)
import bpy, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "Materials", "Textures")


def value_noise(n, cells, rng):
    """Tileable value noise, n x n, `cells` lattice cells per side, smooth (cosine) interpolation."""
    g = rng.random((cells, cells))
    t = np.arange(n) * cells / n
    i0 = np.floor(t).astype(int); f = t - i0; i1 = (i0 + 1) % cells
    f = (1 - np.cos(f * np.pi)) / 2
    a = g[i0][:, i0]; b = g[i0][:, i1]; c = g[i1][:, i0]; d = g[i1][:, i1]
    fx = f[None, :]; fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(n, rng, base=4, octaves=5):
    out = np.zeros((n, n)); amp = 1.; tot = 0.
    for o in range(octaves):
        out += value_noise(n, base * 2 ** o, rng) * amp; tot += amp; amp *= .5
    return out / tot


def veins(n, rng, scale=6., sharp=18.):
    """Marble veins: thin bright/dark lines from a warped sine."""
    y, x = np.mgrid[0:n, 0:n] / n
    w = fbm(n, rng, 3, 5)
    v = np.abs(np.sin((x + y * .6 + w * 1.6) * np.pi * 2 * scale / 4))
    return np.clip(1 - v * sharp / 4, 0, 1) ** 2


def save(name, rgb):
    rgb = np.clip(rgb, 0, 1)
    h, w, _ = rgb.shape
    img = bpy.data.images.new(name, w, h, alpha=False)
    px = np.concatenate([rgb, np.ones((h, w, 1))], axis=2)[::-1].astype(np.float32)   # Blender rows go bottom-up
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(TEX, name + ".png"); img.file_format = 'PNG'
    img.save()
    print("TEX", name, w, h)


def blocks(n, metres, bw, bh, mortar, base, var, rng, bevel=.025):
    """Running-bond blocks. Returns rgb and a 0..1 'edge' map (1 at the mortar)."""
    px = n / metres
    y, x = np.mgrid[0:n, 0:n] / px
    row = np.floor(y / bh).astype(int)
    xo = x + (row % 2) * bw / 2
    col = np.floor(xo / bw).astype(int)
    fx = xo / bw - col; fy = y / bh - row
    dx = np.minimum(fx, 1 - fx) * bw; dy = np.minimum(fy, 1 - fy) * bh
    d = np.minimum(dx, dy)
    edge = np.clip(1 - (d - mortar / 2) / bevel, 0, 1)
    edge[d < mortar / 2] = 1
    ids = (row * 7919 + (col % int(round(metres / bw * 2 + 1))) * 104729) % 9973
    tint = (rng.random(9973) - .5)[ids] * var
    rgb = np.ones((n, n, 3)) * base[None, None, :]
    rgb *= (1 + tint)[..., None]
    return rgb, edge


def main():
    os.makedirs(TEX, exist_ok=True)
    rng = np.random.default_rng(834)
    n = 1024

    # cream dressed stone blocks
    base = np.array([.90, .76, .64])
    rgb, edge = blocks(n, 2., 1., .5, .03, base, .07, rng)
    rgb *= (0.94 + .12 * fbm(n, rng, 8, 5))[..., None]
    rgb -= veins(n, rng, 5)[..., None] * np.array([.05, .07, .09])
    rgb = rgb * (1 - .35 * edge[..., None]) + np.array([.66, .52, .42]) * .35 * edge[..., None]
    save("TU_CreamBlock_BaseColor", rgb)

    # chess marbles
    m = 512
    for name, c, vc in (("TU_MarbleCream_BaseColor", [.95, .86, .76], [-.10, -.13, -.16]),
                        ("TU_MarbleWalnut_BaseColor", [.40, .25, .17], [.12, .09, .06])):
        rgb = np.ones((m, m, 3)) * np.array(c)
        rgb *= (0.93 + .12 * fbm(m, rng, 3, 5))[..., None]
        rgb += veins(m, rng, 3, 14)[..., None] * np.array(vc)
        rgb += veins(m, rng, 7, 22)[..., None] * np.array(vc) * .5
        save(name, rgb)

    # paving slabs 1 x 1 m
    rgb, edge = blocks(n, 2., 1., 1., .03, np.array([.92, .80, .68]), .05, rng)
    rgb *= (0.95 + .1 * fbm(n, rng, 6, 5))[..., None]
    rgb -= veins(n, rng, 4)[..., None] * np.array([.04, .05, .07])
    rgb = rgb * (1 - .3 * edge[..., None]) + np.array([.70, .56, .46]) * .3 * edge[..., None]
    save("TU_Paving_BaseColor", rgb)

    # walnut planks: three boards across 1 m (wait: 0.6 m beam uses the left 60 %), grain along v (the 4 m side)
    w, h = 256, 1024
    y, x = np.mgrid[0:h, 0:w] / np.array([h, w])[:, None, None]
    board = np.floor(x * 3)
    grain = fbm(1024, rng, 4, 4)[:h, :w]
    g = np.sin((x * 3 * 9 + grain * 6 + board * 1.7) * np.pi * 2) * .5 + .5
    col = np.array([.36, .21, .12]) * (0.85 + .25 * g)[..., None]
    col *= (1 + (board / 3 - .33) * .12)[..., None]
    seam = np.abs(x * 3 - np.round(x * 3)) < .012
    col[seam] *= .55
    save("TU_Walnut_BaseColor", col)

    # dry moat flagstones
    rgb, edge = blocks(n, 4., 1.3, .9, .05, np.array([.70, .62, .52]), .10, rng)
    rgb *= (0.85 + .25 * fbm(n, rng, 6, 5))[..., None]
    rgb = rgb * (1 - .45 * edge[..., None]) + np.array([.35, .33, .30]) * .45 * edge[..., None]
    save("TU_MoatFloor_BaseColor", rgb)

    # R834 aerial pass (own seed, so the maps above stay as they were): the cream blocks in shade (inside the moat
    # walls' blind arches), wet stone at the water line, and the meadow outside the walls (4 x 4 m)
    rng2 = np.random.default_rng(8341)
    rgb, edge = blocks(n, 2., 1., .5, .03, np.array([.90, .76, .64]), .07, rng2)
    rgb *= (0.94 + .12 * fbm(n, rng2, 8, 5))[..., None]
    rgb = rgb * (1 - .35 * edge[..., None]) + np.array([.66, .52, .42]) * .35 * edge[..., None]
    save("TU_CreamBlockShade_BaseColor", rgb * np.array([.70, .68, .72]))
    wet = rgb * np.array([.46, .46, .38]) + np.array([.02, .05, .02])
    wet *= (0.8 + .35 * fbm(n, rng2, 5, 5))[..., None]
    save("TU_WetStone_BaseColor", wet)
    base = np.array([.30, .52, .18])
    g1 = fbm(n, rng2, 3, 6); g2 = fbm(n, rng2, 24, 3)
    rgb = base[None, None, :] * (0.78 + .35 * g1)[..., None]
    rgb += (g2 - .5)[..., None] * np.array([.10, .12, .04])
    rgb += np.clip(g1 - .62, 0, 1)[..., None] * np.array([.35, .25, .05])     # sunlit dry patches
    save("TU_Grass_BaseColor", rgb)



if __name__ == "__main__":
    main()
