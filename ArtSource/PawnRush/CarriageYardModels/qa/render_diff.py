# blender -b --factory-startup --python qa/render_diff.py -- <before dir> <after dir> <out dir>
# Renders the static map-frame models from the same cameras twice (before / after the R680 QA rebuild) in grey clay
# with back faces culled (so a dropped face that could be seen shows the sky through it), then counts the pixels that
# changed per view and writes a diff image (red = changed by more than 8/255).
import sys, os
import bpy
from mathutils import Vector

before, after, out = sys.argv[sys.argv.index("--") + 1:]
os.makedirs(out, exist_ok=True)
STATIC = ["RcyEntrance", "RcyCanal", "RcySorting", "RcyPlatform", "RcyFinishCourt", "RcySectionFloors", "RcyScenery",
          "RcySceneryOwn"]
CASTLE = ["RcyCastleOuterGate", "RcyCastleFinalGate"]
VIEWS = {   # Blender map frame: (eye, look-at). Pawn eye 1.6 m above the floor, a few high 3/4 views.
    "court_eye": ((0, -12, 1.6), (0, 6, 0.5)),
    "yard_eye_n": ((0, 6, 1.6), (0, 30, 0.3)),
    "yard_west_strip": ((-12, 1.0, 1.6), (-21, 4.0, 0)),
    "yard_east_strip_n": ((12, 50.5, 1.6), (21, 47.0, 0)),
    "yard_shed_w": ((-14, 20, 1.6), (-21, 26, 1.2)),
    "yard_wall_e_low": ((16, 30, 0.6), (22, 36, 2.0)),
    "canal_landing": ((0, 55, 3.2), (0, 70, 2.0)),
    "canal_side": ((-10, 60, 6.5), (0, 66, 2.0)),
    "sorting_mouth": ((0, 84, 1.6), (0, 100, 0.5)),
    "sorting_stalls_w": ((-16, 95, 1.6), (-24, 104, 1.5)),
    "sorting_loft": ((6, 120, 1.6), (-4, 128, 2.5)),
    "sorting_wall_low": ((18, 110, 0.5), (26, 116, 3.0)),
    "platform_eye": ((0, 146, 9.6), (0, 156, 8.0)),
    "finish_court": ((0, 160, 9.6), (0, 172, 8.0)),
    "overview_se": ((46, -22, 44), (0, 62, 0)),
    "overview_nw": ((-46, 196, 40), (0, 110, 0)),
}


def load(src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for n in STATIC + CASTLE:
        bpy.ops.import_scene.fbx(filepath=os.path.join(src, n + ".fbx"))
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "SINGLE"
    sh.single_color = (0.62, 0.62, 0.62)
    sh.show_backface_culling = True
    sh.background_type = "VIEWPORT"
    sh.background_color = (1.0, 0.0, 1.0)
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    sc.view_settings.view_transform = "Standard"
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = 24
    return sc, cam


def shoot(src, tag):
    sc, cam = load(src)
    for name, (eye, at) in VIEWS.items():
        cam.location = Vector(eye)
        cam.rotation_euler = (Vector(at) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out, "%s_%s.png" % (name, tag))
        bpy.ops.render.render(write_still=True)


shoot(before, "before")
shoot(after, "after")
for name in VIEWS:
    a = bpy.data.images.load(os.path.join(out, name + "_before.png"))
    b = bpy.data.images.load(os.path.join(out, name + "_after.png"))
    pa, pb = list(a.pixels), list(b.pixels)
    w, h = a.size
    diff = [0.0] * len(pa)
    changed = magenta_new = 0
    for i in range(0, len(pa), 4):
        d = max(abs(pa[i] - pb[i]), abs(pa[i + 1] - pb[i + 1]), abs(pa[i + 2] - pb[i + 2]))
        is_mag_b = pb[i] > .9 and pb[i + 1] < .1 and pb[i + 2] > .9
        is_mag_a = pa[i] > .9 and pa[i + 1] < .1 and pa[i + 2] > .9
        if d > 8 / 255:
            changed += 1
            diff[i:i + 4] = (1.0, 0.0, 0.0, 1.0)
        else:
            g = pb[i] * .4
            diff[i:i + 4] = (g, g, g, 1.0)
        if is_mag_b and not is_mag_a:
            magenta_new += 1
            diff[i:i + 4] = (1.0, 1.0, 0.0, 1.0)
    img = bpy.data.images.new(name + "_diff", w, h)
    img.pixels = diff
    img.filepath_raw = os.path.join(out, name + "_diff.png")
    img.file_format = "PNG"
    img.save()
    print("RCY_DIFF %-20s changed %6d px (%.3f%%)  new see-through %5d px" % (name, changed, 100.0 * changed / (w * h), magenta_new))
