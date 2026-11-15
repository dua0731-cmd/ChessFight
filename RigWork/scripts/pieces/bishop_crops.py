# Labelled grid crops of an image, to read pixel coordinates off the design by eye.
# blender -b --factory-startup -P bishop_crops.py -- <out dir> <name>=<image>:<x0>,<y0>,<x1>,<y1>[:zoom[:step]] ...
#   <image> is front|left|right|back (the design originals in Downloads) or a file path
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
OUT = a[0]
os.makedirs(OUT, exist_ok=True)
DL = "C:/Users/trews/Downloads/ChatGPT 이미지 2026년 11월 9일 오후 "
VIEWS = {"front": DL + "10_39_12.png", "left": DL + "10_31_49.png", "right": DL + "10_31_51.png",
         "back": DL + "10_38_40.png"}
cache = {}
for spec in a[1:]:
    name, rest = spec.split("=", 1)
    parts = rest.rsplit(":", 3) if rest.count(":") >= 3 and not rest[1:3] == ":/" else rest.split(":")
    # a Windows path has its own colon: split from the right instead
    bits = rest.split(":")
    if len(bits) > 1 and len(bits[0]) == 1:          # drive letter
        bits = [bits[0] + ":" + bits[1]] + bits[2:]
    img, box = bits[0], [int(v) for v in bits[1].split(",")]
    zoom = int(bits[2]) if len(bits) > 2 else 1
    step = int(bits[3]) if len(bits) > 3 else 50
    path = VIEWS.get(img, img)
    if path not in cache:
        cache[path] = bi.load(path)
    c = bi.grid_crop(cache[path], box[0], box[1], box[2], box[3], zoom, step)
    bi.save(os.path.join(OUT, name + ".png"), c)
    print("CROP", name, c.shape[1], c.shape[0])
