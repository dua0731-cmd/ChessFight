# G royal banner texture (R504): the purple velvet banners of the concept (G_01_overview: purple, cream crown, a thin
# border, a pointed hem) as one RGBA image for the code-built swaying banner (MiniGameModels.RoyalProcessionCloth,
# MiniGameBannerCloth UVs run 0..1 over the whole cloth). Drawn in numpy:
#   ground  the PR_Late_Velvet swatch (Materials/Textures), tiled at the banner's real size (1.9 x 5.2 m, 1 m swatch)
#   border  a matte brass band (#9C8354) inset along the sides and top and following the V hem
#   crown   a cream (#E5DECC) five-point crown with a band and three round jewels, in the upper third
#   hem     alpha 0 below a V from the sides' bottom up to... the point at the bottom middle (alpha-clipped material)
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python g_banner_tex_v1.py
import bpy, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "Materials", "Textures")
W_M, H_M = 1.9, 5.2
W, H = 512, int(512 * H_M / W_M) // 4 * 4            # 512 x 1400
NOTCH = .55                                            # the V hem's depth, metres


def load(name):
    img = bpy.data.images.load(os.path.join(TEX, name), check_existing=False)
    w, h = img.size
    a = np.array(img.pixels[:], np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return a


def main():
    velvet = load("PR_Late_Velvet_BaseColor.png")[..., :3]
    vh, vw = velvet.shape[:2]
    ys, xs = np.mgrid[0:H, 0:W]                        # row 0 = bottom (Blender pixel order)
    mx, my = xs / W * W_M, ys / H * H_M                # metres; my = 0 at the hem's tip level
    rgb = velvet[(my % 1. * vh).astype(int) % vh, (mx % 1. * vw).astype(int) % vw].copy()
    # the V hem: the cloth's bottom edge rises from the point (centre, y 0) to NOTCH at both sides
    hem = NOTCH * np.abs(mx - W_M / 2) / (W_M / 2)
    alpha = (my >= hem).astype(np.float32)
    # brass border: 0.09 m wide, 0.06 m in from the edges, along both sides, the top and the V
    inset, band = .06, .09
    d_side = np.minimum(mx, W_M - mx)
    d_top = H_M - my
    d_hem = (my - hem) * (W_M / 2) / np.hypot(W_M / 2, NOTCH)     # distance above the slanted hem line
    d = np.minimum(np.minimum(d_side, d_top), d_hem)
    border = (d >= inset) & (d < inset + band)
    brass = np.array([.612, .514, .329], np.float32)              # #9C8354
    rgb[border] = brass * (.9 + .1 * np.random.default_rng(1).random((int(border.sum()), 1)))
    # the crown: centre at (W/2, 3.55 m), 1.15 m wide
    cream = np.array([.898, .871, .8], np.float32)                # #E5DECC
    cx, cy, cw = W_M / 2, 3.55, 1.15
    u, v = (mx - cx) / (cw / 2), (my - cy) / (cw / 2)             # crown space, -1..1 across
    band_c = (np.abs(u) <= 1.) & (v >= -.75) & (v <= -.45)
    # five points: the top edge zigzags between peaks (u = -1, -.5, 0, .5, 1) at v .55 and valleys at v .05
    peaks = np.array([-1., -.5, 0., .5, 1.])
    top = np.full_like(u, -9.)
    for i in range(4):
        a0, a1 = peaks[i], peaks[i + 1]
        mid = (a0 + a1) / 2
        seg = (u >= a0) & (u <= a1)
        t = np.abs(u - mid) / ((a1 - a0) / 2)                     # 0 at the valley, 1 at the peaks
        top = np.where(seg, .05 + .5 * t, top)
    body = (np.abs(u) <= 1.) & (v > -.45) & (v <= top)
    balls = np.zeros_like(body)
    for p in peaks:
        balls |= (u - p) ** 2 + (v - .66) ** 2 <= .11 ** 2
    crown = band_c | body | balls
    rgb[crown] = cream
    jewels = np.zeros_like(crown)
    for p in (-.5, 0., .5):
        jewels |= (u - p) ** 2 + (v + .6) ** 2 <= .09 ** 2
    rgb[jewels] = brass
    out = bpy.data.images.new("PR_Late_G_RoyalBanner_BaseColor", W, H, alpha=True)
    px = np.concatenate([rgb, alpha[..., None]], -1).astype(np.float32)
    out.pixels.foreach_set(px.ravel())
    out.filepath_raw = os.path.join(TEX, "PR_Late_G_RoyalBanner_BaseColor.png")
    out.file_format = 'PNG'
    out.save()
    print("BANNER", W, H, "border px", int(border.sum()), "crown px", int(crown.sum()))


main()
