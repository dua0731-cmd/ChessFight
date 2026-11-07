# A ceremonial court banners (R506): the concept's cream banners with a brass crown and the team banners with a pawn
# shield (Docs/PawnRush/ConceptArt/R506_CeremonialCourt/A_01_overview.png), as RGBA images for the code-built swaying
# cloth (MiniGameBannerCloth: UVs 0..1 over the whole cloth). Same way as G's g_banner_tex_v1.py, drawn in numpy:
#   ground  woven cloth (fine warp/weft ripple + noise) in cream #E5DECC or the team colour
#   border  matte brass (#9C8354) inset along the sides and top and following the V hem
#   emblem  a brass crown (cream banners) or a cream heater shield with a brass rim and a team-colour pawn (team banners)
#   hem     alpha 0 below a V (alpha-clipped material)
# Banners (metres, the size the game hangs them):
#   PR_A_CrownBanner       1.05 x 1.7   the parade flagpoles and the rear corridor
#   PR_A_CrownBannerLong   0.9 x 2.6    the gatehouse wall either side of the arch
#   PR_A_TeamBannerWhite   2.35 x 2.3   over the white (sky-blue) team's portal
#   PR_A_TeamBannerBlack   2.35 x 2.3   over the black (orange) team's portal
# Team colours only on team things (R499): sky blue #3FC1D9, orange #F08A2C.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python a_banner_tex_v1.py
import bpy, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "Materials", "Textures")
CREAM = np.array([.898, .871, .8], np.float32)        # #E5DECC
BRASS = np.array([.612, .514, .329], np.float32)      # #9C8354
TEAM = {"White": np.array([.247, .757, .851], np.float32), "Black": np.array([.941, .541, .173], np.float32)}
PX_PER_M = 400


def cloth(colour, mx, my, seed):
    rng = np.random.default_rng(seed)
    weave = .04 * np.sin(mx * 2 * np.pi * 60) * np.sin(my * 2 * np.pi * 60)        # warp x weft, ~1.7 cm
    streak = .03 * np.sin(my * 2 * np.pi * 2.3 + np.sin(mx * 7.) * .8)              # long folds of tone
    grain = rng.normal(0, .02, mx.shape)
    return np.clip(colour[None, None, :] * (1 + weave + streak + grain)[..., None], 0, 1)


def crown(u, v):
    """Five-point crown in -1..1 space (G's shape): band, zigzag body, balls on the peaks; jewels on the band."""
    band = (np.abs(u) <= 1.) & (v >= -.75) & (v <= -.45)
    peaks = np.array([-1., -.5, 0., .5, 1.])
    top = np.full_like(u, -9.)
    for i in range(4):
        a0, a1 = peaks[i], peaks[i + 1]
        seg = (u >= a0) & (u <= a1)
        t = np.abs(u - (a0 + a1) / 2) / ((a1 - a0) / 2)
        top = np.where(seg, .05 + .5 * t, top)
    body = (np.abs(u) <= 1.) & (v > -.45) & (v <= top)
    balls = np.zeros_like(body)
    for p in peaks:
        balls |= (u - p) ** 2 + (v - .66) ** 2 <= .11 ** 2
    jewels = np.zeros_like(body)
    for p in (-.5, 0., .5):
        jewels |= (u - p) ** 2 + (v + .6) ** 2 <= .09 ** 2
    return band | body | balls, jewels


def shield(u, v):
    """Heater shield in -1..1 across: flat top at v .85, straight sides down to v 0, then curving to a point at v -1.2."""
    half = np.where(v >= 0, 1., np.clip(1 + v / 1.2, 0, 1) ** .6)
    return (np.abs(u) <= half) & (v <= .85) & (v >= -1.2), half


def pawn(u, v):
    head = u ** 2 + (v - .38) ** 2 <= .2 ** 2
    collar = (np.abs(u) <= .26) & (v >= .1) & (v <= .17)
    t = np.clip((.1 - v) / .55, 0, 1)                      # 0 under the collar, 1 at the foot
    body = (v <= .1) & (v >= -.45) & (np.abs(u) <= .12 + .2 * t)
    foot = (np.abs(u) <= .46) & (v >= -.6) & (v < -.45)
    return head | collar | body | foot


def banner(name, w_m, h_m, ground, emblem, seed):
    W, H = int(w_m * PX_PER_M) // 4 * 4, int(h_m * PX_PER_M) // 4 * 4
    ys, xs = np.mgrid[0:H, 0:W]                           # row 0 = bottom (Blender pixel order)
    mx, my = (xs + .5) / W * w_m, (ys + .5) / H * h_m
    rgb = cloth(ground, mx, my, seed)
    notch = .1 * h_m
    hem = notch * np.abs(mx - w_m / 2) / (w_m / 2)
    alpha = (my >= hem).astype(np.float32)
    inset, band = .035 * w_m, .05 * w_m
    d_side = np.minimum(mx, w_m - mx)
    d_top = h_m - my
    d_hem = (my - hem) * (w_m / 2) / np.hypot(w_m / 2, notch)
    d = np.minimum(np.minimum(d_side, d_top), d_hem)
    rgb[(d >= inset) & (d < inset + band)] = BRASS
    if emblem == "crown":
        cx, cy, cw = w_m / 2, .62 * h_m, .62 * w_m
        u, v = (mx - cx) / (cw / 2), (my - cy) / (cw / 2)
        body, jewels = crown(u, v)
        rgb[body] = BRASS * 1.08
        rgb[jewels] = CREAM
    else:
        cx, cy, cw = w_m / 2, .58 * h_m, .5 * w_m
        u, v = (mx - cx) / (cw / 2), (my - cy) / (cw / 2)
        inside, half = shield(u, v)
        rim = inside & ~((np.abs(u) <= half - .1) & (v <= .75) & (v >= -1.08))
        rgb[inside] = CREAM
        rgb[rim] = BRASS
        rgb[inside & ~rim & pawn(u, v)] = ground
    out = bpy.data.images.new(name + "_BaseColor", W, H, alpha=True)
    out.pixels.foreach_set(np.concatenate([rgb, alpha[..., None]], -1).astype(np.float32).ravel())
    out.filepath_raw = os.path.join(TEX, name + "_BaseColor.png")
    out.file_format = 'PNG'
    out.save()
    print("BANNER", name, W, H)


banner("PR_A_CrownBanner", 1.05, 1.7, CREAM, "crown", 61)
banner("PR_A_CrownBannerLong", .9, 2.6, CREAM, "crown", 62)
banner("PR_A_TeamBannerWhite", 2.35, 2.3, TEAM["White"], "shield", 63)
banner("PR_A_TeamBannerBlack", 2.35, 2.3, TEAM["Black"], "shield", 64)
