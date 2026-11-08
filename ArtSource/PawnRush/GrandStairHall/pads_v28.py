# 승규 11-03 (the two pad assets' own previews: cream tiles, dark panels, gold frames, glowing orange seams and arrow):
# "발판도 텍스쳐 입혀". The source GLBs carry no UVs and no colours, so R311's FloatingPad / JumpPadModel get both here:
#   top (faces up)    UVs straight down over the pad's square (u = x, v = y), material <Pad>_Top
#   side (the rest)   u round the square's perimeter (one side = 0 .. 1), v up the pad's height, material <Pad>_Side
# The textures are drawn from the pads' own relief (pad_textures_v28.py), so the seams, tiles and the arrow land on
# the shapes: "raster" writes each top's height map (textures/<Pad>_height.npy, 1024 square, +v up); "build" adds the
# UVs and materials (their images, for the preview renders) and exports both FBX into the repository, turned as v18's.
# Run: blender -b grand_stair_hall_v27C.blend -P pads_v28.py -- raster
#      python pad_textures_v28.py
#      blender -b grand_stair_hall_v27C.blend -P pads_v28.py -- build   -> grand_stair_hall_v28C.blend, renders_v35/*.png
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
MODE = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "build"
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
TEX = os.path.join(HERE, "textures")
PADS = [("Floating_pad", "FloatingPad", "FloatingPad.fbx"), ("Jump_pad_model", "JumpPad", "JumpPadModel.fbx")]
N = 1024
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
sc = bpy.context.scene


def bounds(me):
    vs = np.array([v.co[:] for v in me.vertices])
    return vs.min(0), vs.max(0)


def raster(me):
    """The highest up-facing surface over each texel of the pad's square (NaN where none)."""
    lo, hi = bounds(me)
    H = np.full((N, N), np.nan)
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:])
    for f in bm.faces:
        if f.normal.z < .3:
            continue
        p = np.array([v.co[:] for v in f.verts])
        uv = (p[:, :2] - lo[:2]) / (hi[:2] - lo[:2]) * (N - 1)
        a, b, c = uv
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12:
            continue
        x0, y0 = np.floor(uv.min(0)).astype(int); x1, y1 = np.ceil(uv.max(0)).astype(int)
        xs = np.arange(max(x0, 0), min(x1, N - 1) + 1)
        for y in range(max(y0, 0), min(y1, N - 1) + 1):
            l1 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (y - c[1])) / den
            l2 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (y - c[1])) / den
            l3 = 1 - l1 - l2
            ok = (l1 >= -.02) & (l2 >= -.02) & (l3 >= -.02)
            z = l1 * p[0, 2] + l2 * p[1, 2] + l3 * p[2, 2]
            H[y, xs[ok]] = np.fmax(H[y, xs[ok]], z[ok])
    bm.free()
    return H, lo, hi


def material(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    path = os.path.join(RES, name + ".png")
    if os.path.exists(path):
        nt = m.node_tree
        img = nt.nodes.get("Image") or nt.nodes.new("ShaderNodeTexImage")
        img.name = "Image"
        img.image = bpy.data.images.load(path, check_existing=True)
        nt.links.new(img.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
        nt.nodes.active = img
    return m


def unwrap(me, key):
    lo, hi = bounds(me)
    side = (hi[0] - lo[0] + hi[1] - lo[1]) / 2
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="UVMap")
    me.materials.clear()
    me.materials.append(material(key + "_Top")); me.materials.append(material(key + "_Side"))
    top = 0
    for p in me.polygons:
        up = p.normal.z > .5
        p.material_index = 0 if up else 1
        top += up
        n = p.normal
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            if up:
                uv.data[li].uv = ((co.x - lo[0]) / (hi[0] - lo[0]), (co.y - lo[1]) / (hi[1] - lo[1]))
            else:
                # round the square: along the side the face looks out of, one side = 0 .. 1
                s = (co.y - lo[1]) / side if abs(n.x) >= abs(n.y) else (co.x - lo[0]) / side
                uv.data[li].uv = (s, (co.z - lo[2]) / (hi[2] - lo[2]))
    print("UV", key, len(me.polygons), "faces,", top, "top")


def export(name, me, fbx):
    """As v18's export: Blender (X, Y, Z) arrives as Unity (-x, y, -z) without the hall's pivot, so the mesh turns half
    round to keep its +Y (the arrow) on the pad's +z. The UVs stay with the faces."""
    m = me.copy(); m.transform(Matrix.Rotation(math.pi, 4, "Z"))
    o = bpy.data.objects.new(name, m); sc.collection.objects.link(o)
    for s in sc.objects:
        s.select_set(False)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(30))
    bpy.ops.export_scene.fbx(filepath=os.path.join(RES, fbx), **FBX)
    print("EXPORT", fbx, len(m.polygons), "faces", [s.name for s in m.materials])
    bpy.data.objects.remove(o)


os.makedirs(TEX, exist_ok=True)
for mesh_name, key, fbx in PADS:
    me = bpy.data.meshes[mesh_name]
    if MODE == "raster":
        H, lo, hi = raster(me)
        np.save(os.path.join(TEX, key + "_height.npy"), H)
        np.save(os.path.join(TEX, key + "_bounds.npy"), np.array([lo, hi]))
        z = H[~np.isnan(H)]
        print("RASTER", key, "z %.3f .. %.3f" % (z.min(), z.max()), "cover %.2f" % (len(z) / H.size))
    else:
        unwrap(me, key)
        export(mesh_name, me, fbx)

if MODE == "build":
    sh = sc.display.shading
    sh.color_type = "TEXTURE"
    out = os.path.join(HERE, "renders_v35"); os.makedirs(out, exist_ok=True)
    for name, loc, at in (("pads_stones", (16, 112, 20), (-12, 104, 10)), ("pads_jump", (-4, 116, 16.5), (-9, 110.6, 11.6))):
        cd = bpy.data.cameras.new(name); cd.lens = 35
        cam = bpy.data.objects.new(name, cd); sc.collection.objects.link(cam)
        cam.location = loc
        cam.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.camera = cam
        sc.render.filepath = os.path.join(out, name + ".png")
        bpy.ops.render.render(write_still=True)
    sh.color_type = "MATERIAL"
    sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v28C.blend"))
