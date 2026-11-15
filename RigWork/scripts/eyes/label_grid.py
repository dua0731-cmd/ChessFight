# Adds a title, a header row and a left column of labels (Korean is fine: Malgun Gothic) to a grid image made of
# equal tiles. blender -b --factory-startup -P label_grid.py -- <in.png> <out.png> <spec.json>
# spec: {"tile": [w, h], "gap": 6, "cols": [...], "rows": [...], "title": "...", "note": "...",
#        "head_h": 70, "left_w": 150, "font": "C:/Windows/Fonts/malgunbd.ttf"}
import bpy, sys, json, os
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT, SPEC = a[0], a[1], json.load(open(a[2], encoding="utf-8"))
im = bpy.data.images.load(SRC); W, H = im.size
grid = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, 4)[::-1, :, :3]
bpy.data.images.remove(im)
tw, th = SPEC["tile"]; gap = SPEC.get("gap", 6)
HEAD, LEFT = SPEC.get("head_h", 70), SPEC.get("left_w", 150)
TITLE_H = 70 if SPEC.get("title") else 0
NOTE_H = 56 if SPEC.get("note") else 0
OW, OH = W + LEFT, H + HEAD + TITLE_H + NOTE_H
sheet = np.ones((OH, OW, 3), dtype=np.float32)
sheet[TITLE_H + HEAD:TITLE_H + HEAD + H, LEFT:] = grid

# ---- text: one orthographic render of the whole sheet's text, black on transparent
sc = bpy.context.scene
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
font = bpy.data.fonts.load(SPEC.get("font", "C:/Windows/Fonts/malgunbd.ttf"))
font_reg = bpy.data.fonts.load("C:/Windows/Fonts/malgun.ttf")
def text(s, x, y, size, align="CENTER", bold=True, colour=(0.08, 0.08, 0.08)):
    cu = bpy.data.curves.new("t", "FONT")
    cu.body = s
    cu.font = font if bold else font_reg
    cu.size = size
    cu.align_x = align
    cu.align_y = "CENTER"
    ob = bpy.data.objects.new("t", cu)
    sc.collection.objects.link(ob)
    ob.location = (x, OH - y, 0)                       # sheet pixels, y down -> world y up
    m = bpy.data.materials.new("m"); m.diffuse_color = (*colour, 1)
    cu.materials.append(m)
    return ob
if SPEC.get("title"):
    text(SPEC["title"], 24, TITLE_H / 2 + 4, 34, "LEFT")
for i, s in enumerate(SPEC["cols"]):
    text(s, LEFT + i * (tw + gap) + tw / 2, TITLE_H + HEAD / 2, 30)
for j, s in enumerate(SPEC["rows"]):
    for k, line in enumerate(s.split("\n")):
        n = len(s.split("\n"))
        text(line, LEFT / 2, TITLE_H + HEAD + j * (th + gap) + th / 2 + (k - (n - 1) / 2) * 34, 28)
if SPEC.get("note"):
    text(SPEC["note"], 24, OH - NOTE_H / 2, 22, "LEFT", bold=False, colour=(0.25, 0.25, 0.25))
for cell in SPEC.get("blank", []):                    # tiles with a note instead of an image
    j, i, s = cell
    x0 = LEFT + i * (tw + gap); y0 = TITLE_H + HEAD + j * (th + gap)
    sheet[y0:y0 + th, x0:x0 + tw] = 0.95
    text(s, x0 + tw / 2, y0 + th / 2, 24, bold=False, colour=(0.45, 0.45, 0.45))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
cam.data.type = "ORTHO"
cam.data.ortho_scale = max(OW, OH)
cam.location = (OW / 2, OH / 2, 10)
sc.camera = cam
sc.render.resolution_x, sc.render.resolution_y = OW, OH
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "FLAT"
sc.display.shading.color_type = "MATERIAL"
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
tmp = OUT + ".text.png"
sc.render.filepath = tmp
bpy.ops.render.render(write_still=True)
t = bpy.data.images.load(tmp); tw_, th_ = t.size
tx = np.array(t.pixels[:], dtype=np.float32).reshape(th_, tw_, 4)[::-1]
bpy.data.images.remove(t)
os.remove(tmp)
al = tx[..., 3:4]
sheet = sheet * (1 - al) + tx[..., :3] * al
img = bpy.data.images.new("out", OW, OH, alpha=True)
img.pixels = np.dstack([sheet, np.ones((OH, OW), dtype=np.float32)])[::-1].ravel()
img.filepath_raw = OUT; img.file_format = "PNG"; img.save()
print("LABELLED", OUT, OW, OH)
