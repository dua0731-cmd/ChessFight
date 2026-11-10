# Writes the expression atlas of one eye style for one team: <prefix>_<team>.png (RGBA, straight alpha),
# <prefix>_<team>_height.png (relief for the bevel / normal map) and <prefix>_<team>_preview.png (on the body colour,
# cells outlined). Layout and drawings: eye_draw.py.
# blender -b --factory-startup -P make_eye_atlas.py -- <out dir> <style oval|rect|diamond> <team white|black> [prefix]
#         [--style-json '{"aspect": 1.6, ...}']   (overrides the style's values, for calibration)
import bpy, sys, os, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eye_draw as ed

a = sys.argv[sys.argv.index("--") + 1:]
over = {}
if "--style-json" in a:
    k = a.index("--style-json")
    over = json.loads(a[k + 1])
    del a[k:k + 2]
OUT, STYLE, TEAM = a[0], a[1], a[2]
PREFIX = a[3] if len(a) > 3 else "eyes_%s" % STYLE
os.makedirs(OUT, exist_ok=True)
for key, v in over.items():
    if key == "team_fit":
        ed.TEAM_FIT[(TEAM, STYLE)] = tuple(v)
        continue
    ed.STYLES[STYLE][key] = tuple(v) if isinstance(v, list) else v

rgba, hgt = ed.atlas(STYLE, TEAM)
print("STYLE", STYLE, TEAM, ed.STYLES[STYLE], "fit", ed.TEAM_FIT.get((TEAM, STYLE)))


def save(arr, name, non_color=False):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True)
    if arr.ndim == 2:
        arr = np.dstack([arr, arr, arr, np.ones_like(arr)])
    img.pixels = arr[::-1].ravel()                       # Blender rows start at the bottom
    img.filepath_raw = os.path.join(OUT, name)
    img.file_format = "PNG"
    img.save()
    return os.path.join(OUT, name)


base = "%s_%s" % (PREFIX, TEAM)
save(rgba, base + ".png")
save(hgt, base + "_height.png")
body = np.array((0.97, 0.86, 0.73) if TEAM == "white" else (0.30, 0.30, 0.31), dtype=np.float32)
prev = rgba[..., :3] * rgba[..., 3:4] + body * (1 - rgba[..., 3:4])
H, W = prev.shape[:2]
for k in range(1, 4):
    prev[k * H // 4 - 2:k * H // 4 + 2, :, :] = 0.6
    prev[:, k * W // 4 - 2:k * W // 4 + 2, :] = 0.6
for k in range(4):                                       # a faint line between the two eyes of each cell
    x = k * W // 4 + W // 8
    prev[:, x:x + 1, :] = prev[:, x:x + 1, :] * 0.8 + 0.2 * 0.6
save(np.dstack([prev, np.ones(prev.shape[:2], dtype=np.float32)]), base + "_preview.png")
json.dump(dict(style=STYLE, team=TEAM, values=ed.STYLES[STYLE], order=ed.ORDER, HU=ed.HU, HV=ed.HV),
          open(os.path.join(OUT, base + ".json"), "w"), indent=1)
print("ATLAS", os.path.join(OUT, base + ".png"))
