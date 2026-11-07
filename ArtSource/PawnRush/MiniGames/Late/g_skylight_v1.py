# G skylight vault (R504): the user's "Sandstone Arch Segment" FBX (Source/R504_SandstoneArchSegment.fbx: a frame of four
# round ribs on two rails, plus a stray wall block) as the barrel-vault skylight of the G hall, the concept's
# Docs/PawnRush/ConceptArt/R504_RoyalProcession/G_02_architecture_modules.png #6. No glass (user, 11-07: "유리는 안돼").
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python g_skylight_v1.py -- [--export] [--render]
#
# The frame is kept (the stray block is the other loose group), turned so its rails run along Blender y (the hall's
# length, Unity z) and its ribs span Blender x, scaled so the ribs span 5.9 m (the shell's skylight frames, x +-8),
# origin at the bottom centre. Sandstone -> cream marble, as the centre gate. A module repeated 6 times: 3,000 triangles
# (poly budget the user set via the D session, 11-07). Reuses g_center_gate_v1 / g_props_v1 helpers.
import bpy, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_center_gate_v1 as gate
import g_props_v1 as props

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SRC = "R504_SandstoneArchSegment.fbx"
SPAN = 5.9
TARGET = 6000
NAME = "G_SkylightVault"


def recolour(mat):
    nodes = mat.node_tree.nodes
    imgs = {n.image.name: n for n in nodes if n.type == 'TEX_IMAGE' and n.image}
    base, normal = imgs["base_color"].image, imgs["normal"].image
    w, h = base.size
    px = np.array(base.pixels[:], np.float32).reshape(h, w, 4)
    rgb = px[..., :3]
    hue, sat, val = gate.hsv(rgb)
    lum = (rgb * np.array([.2126, .7152, .0722], np.float32)).sum(-1, keepdims=True)
    warm = (np.clip(1 - np.abs(hue - 25) / 22, 0, 1) * np.clip((sat - .08) / .1, 0, 1))[..., None]
    ref = float(np.median(lum[warm[..., 0] > .5])) if (warm > .5).any() else .55
    px[..., :3] = rgb * (1 - warm) + np.clip(gate.CREAM * (lum / ref) ** .85, 0, 1) * warm
    name = "PR_Late_G_Skylight"
    out = bpy.data.images.new(name + "_BaseColor", w, h)
    out.pixels.foreach_set(px.ravel()); out.scale(2048, 2048)
    out.filepath_raw = os.path.join(props.TEX, name + "_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nrm = normal.copy(); nrm.name = name + "_Normal"; nrm.colorspace_settings.name = 'Non-Color'
    nrm.filepath_raw = os.path.join(props.TEX, name + "_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    imgs["base_color"].image = out; imgs["normal"].image = nrm
    mat.name = name
    print("RECOLOUR stone %.1f%%" % (100 * float((warm > .5).mean())))


def main():
    src = props.load(SRC)
    co, lab, *_ = props.labels(src.data, 1)
    groups, counts = np.unique(lab, return_counts=True)
    frame = groups[np.argmax(counts)]
    print("GROUPS", dict(zip(groups.tolist(), counts.tolist())), "frame", frame)
    ob = props.cut(src, lab == frame, NAME)
    bpy.data.objects.remove(src)
    me = ob.data
    c = np.array([v.co[:] for v in me.vertices])
    ext = c.max(0) - c.min(0)
    turn = 90. if ext[0] > ext[1] else 0.      # long axis (the rails) -> Blender y
    k = props.place(ob, turn, "w", SPAN)
    props.decimate(ob, TARGET)
    c = np.array([v.co[:] for v in ob.data.vertices])
    print("FIT %s scale %.2f tris %d size %s" % (NAME, k, props.tris(ob), np.round(c.max(0) - c.min(0), 2)))
    recolour(ob.data.materials[0])
    if "--export" in ARGS:
        props.export(ob)
    if "--render" in ARGS:
        sc = bpy.context.scene
        sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1400, 800
        w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = .8; sc.world = w
        bpy.ops.object.light_add(type='SUN', rotation=(math.radians(40), 0, math.radians(-30)))
        from mathutils import Vector
        cd = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
        cam.location = (-14, -18, 9); cam.rotation_euler = (Vector((0, 0, 1.5)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_g_skylight.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
