# blender -b --factory-startup --python qa/castle_check.py -- <before dir> <after dir> <out dir>
# Section end before/after: open edges, then textured views (the castle's own textures) and grey hole views (back faces
# culled, magenta sky) from the same cameras; counts changed and newly see-through pixels per view.
import sys, os
import bpy, bmesh
from mathutils import Vector

before, after, out = sys.argv[sys.argv.index("--") + 1:]
TEX = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", "Assets", "Resources", "PawnRushCarriageYard"))
os.makedirs(out, exist_ok=True)
VIEWS = {
    "steps_down": ((0.0, 172.0, 9.4), (0.0, 178.5, 7.0)),
    "steps_up_back": ((1.0, 180.5, 8.5), (0.0, 175.0, 7.6)),
    "rail_east_eye": ((3.0, 163.0, 9.6), (6.0, 169.0, 8.6)),
    "rail_end_exit": ((3.0, 184.0, 8.2), (6.2, 180.6, 7.6)),
    "rail_west_low": ((-3.0, 177.5, 7.9), (-6.2, 178.8, 7.2)),
}


def load(src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for n in ("RcyCastleSectionEnd", "RcySectionFloors"):
        bpy.ops.import_scene.fbx(filepath=os.path.join(src, n + ".fbx"))
    # the castle's slot names are its texture names
    for m in bpy.data.materials:
        png = os.path.join(TEX, m.name + ".png")
        if os.path.exists(png):
            m.use_nodes = True
            nt = m.node_tree
            bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = bpy.data.images.load(png, check_existing=True)
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    sc.view_settings.view_transform = "Standard"
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = 24
    return sc, cam


def open_edges():
    n = 0
    for ob in bpy.data.objects:
        if ob.type == "MESH" and ob.name.startswith("Castle"):
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            n += sum(1 for e in bm.edges if len(e.link_faces) == 1)
            bm.free()
    return n


def shoot(src, tag):
    sc, cam = load(src)
    print("RCY_CASTLE_CHECK", tag, "section end open edges", open_edges())
    sh = sc.display.shading
    sh.light = "STUDIO"
    for mode in ("tex", "holes"):
        if mode == "tex":
            sh.color_type, sh.show_backface_culling = "TEXTURE", False
            sh.background_type, sh.background_color = "VIEWPORT", (.75, .8, .85)
        else:
            sh.color_type, sh.single_color, sh.show_backface_culling = "SINGLE", (.62, .62, .62), True
            sh.background_type, sh.background_color = "VIEWPORT", (1.0, 0.0, 1.0)
        for name, (eye, at) in VIEWS.items():
            cam.location = Vector(eye)
            cam.rotation_euler = (Vector(at) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()
            sc.render.filepath = os.path.join(out, "%s_%s_%s.png" % (name, mode, tag))
            bpy.ops.render.render(write_still=True)


shoot(before, "before")
shoot(after, "after")
for name in VIEWS:
    for mode in ("tex", "holes"):
        a = bpy.data.images.load(os.path.join(out, "%s_%s_before.png" % (name, mode)))
        b = bpy.data.images.load(os.path.join(out, "%s_%s_after.png" % (name, mode)))
        pa, pb = list(a.pixels), list(b.pixels)
        ch = sum(1 for i in range(0, len(pa), 4) if max(abs(pa[i + k] - pb[i + k]) for k in range(3)) > 8 / 255)
        new_sky = 0
        if mode == "holes":
            new_sky = sum(1 for i in range(0, len(pa), 4) if pb[i] > .9 and pb[i + 1] < .1 and pb[i + 2] > .9
                          and not (pa[i] > .9 and pa[i + 1] < .1 and pa[i + 2] > .9))
        print("RCY_CASTLE_DIFF %-16s %-5s changed %6d px  new see-through %d" % (name, mode, ch, new_sky))
