# 승규 11-04 (a picture of a white marble pawn: faceted, polished, a double gold ring at its neck, a gold band at its
# base) for the grand stair's big roll (RollingSpawner's statue: a capsule 1.6 m across and 2.6 m long, lying across its
# lane, turning about its length; "step aside"). The pawn lies along x, its base at +x (Unity -x, as the primitive's
# base end), its finial at -x, 2.6 m from end to end, never wider than 1.6 m:
#   RollingStatue_Stone  white marble (RoyalStair_Marble.png, the stair's), turned in 14 facets with flat faces - the
#                        picture's cut-stone look: base tiers, a flared foot, the tapering body, a stone ring between
#                        the collar's two gold rings, the neck, the faceted head, the finial
#   RollingStatue_Gold   the gold: a band round the base, the two collar rings, a thin ring under the finial
# UVs: u round (1.5 m a tile), v along (1.5 m a tile), so the marble turns with the roll (no world-space shader).
# Run: blender -b -P pawn_roll_v47.py -- [render]
#   -> Assets/Resources/PawnRushGrandStairHall/RollingStatue.fbx, pawn_roll_v47.blend, renders_roll_v47/
import bpy, bmesh, math, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
RENDER = "render" in sys.argv
PI = math.pi
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
L = 2.6                       # base bottom (h 0) at x +1.3, finial top (h 2.6) at x -1.3
FACETS = 14                   # v2: fewer, plainer facets, as the picture


def X(h):
    return L / 2 - h


def material(name, base, metal, rough, image=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*base, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if image:
        t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(RES, image))
        m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    return m


M_STONE = material("Roll_Marble", (.95, .94, .91), 0, .18, "RoyalStair_Marble.png")
M_GOLD = material("Stair_Gold", (.86, .64, .28), 1, .25, "RoyalFan_Gold.png")


def lathe(bm, uv, prof, segs, smooth, tile=1.5):
    """Profile (r, h) turned about the pawn's axis; caps where r > 0 at the ends."""
    rings = []
    for i in range(segs + 1):                                   # a seam ring for the UVs
        a = 2 * PI * i / segs
        rings.append([bm.verts.new((X(h), r * math.cos(a), r * math.sin(a))) for r, h in prof])
    for i in range(segs):
        for j in range(len(prof) - 1):
            f = bm.faces.new((rings[i][j], rings[i + 1][j], rings[i + 1][j + 1], rings[i][j + 1]))
            f.smooth = smooth
            for l, (ii, jj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                r, h = prof[jj]
                l[uv].uv = (2 * PI * ii / segs * max(r, .3) / tile, h / tile)
    for end in (0, len(prof) - 1):
        if prof[end][0] > 1e-5:
            f = bm.faces.new([rings[i][end] for i in range(segs)])
            f.smooth = False
            for l in f.loops:
                l[uv].uv = (l.vert.co.y / tile + .5, l.vert.co.z / tile + .5)


def sphere_profile(c, r, n, h0=None, h1=None):
    """A sphere's (r, h) profile round centre height c, from h0 up to h1 (defaults: pole to pole)."""
    t0 = -PI / 2 if h0 is None else math.asin(max(-1, min(1, (h0 - c) / r)))
    t1 = PI / 2 if h1 is None else math.asin(max(-1, min(1, (h1 - c) / r)))
    return [(max(0.0, r * math.cos(t0 + (t1 - t0) * k / n)), c + r * math.sin(t0 + (t1 - t0) * k / n)) for k in range(n + 1)]


# ---------------------------------------------------------------- the stone: one turned profile, base to finial
# v2 (first render: a tall trumpet body and a tiny finial): the picture's proportions in the same 2.6 m - a short wide
# bell body, a big faceted head on a thick gold collar, a double gold ring, a large ball on top.
stone = [(0, 0), (.77, 0), (.8, .025), (.8, .1),             # the base's lower tier (the gold band round it)
         (.79, .18), (.78, .2), (.72, .25), (.66, .28),       # the upper tier, rounding in
         (.62, .31), (.56, .38), (.5, .47), (.45, .58),       # the bell body
         (.42, .69), (.4, .8), (.38, .86), (.3, .9)]          # up under the collar
stone += sphere_profile(1.4, .5, 8, h0=.92, h1=1.86)          # the faceted head
stone += [(.18, 1.9), (.2, 1.96), (.18, 2.02), (.1, 2.07)]    # between the two gold rings
stone += sphere_profile(2.33, .27, 6, h0=2.08)                # the ball on top (its top at 2.6)
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
lathe(bm, uv, stone, FACETS, smooth=False)
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
me = bpy.data.meshes.new("RollingStatue_Stone"); bm.to_mesh(me); bm.free()
me.materials.append(M_STONE)
stone_ob = bpy.data.objects.new("RollingStatue_Stone", me); sc.collection.objects.link(stone_ob)

# ---------------------------------------------------------------- the gold rings (smooth, a little proud of the stone)
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
for prof in ([(.79, .1), (.815, .11), (.815, .17), (.79, .18)],                      # the base band
             [(.38, .8), (.5, .81), (.55, .85), (.55, .89), (.5, .93), (.3, .94)],   # the thick collar under the head
             [(.18, 1.85), (.25, 1.855), (.28, 1.88), (.28, 1.92), (.25, 1.95), (.18, 1.955)],   # the double ring
             [(.18, 1.965), (.23, 1.97), (.26, 1.99), (.26, 2.03), (.23, 2.055), (.1, 2.07)]):
    lathe(bm, uv, prof, 48, smooth=True, tile=.6)
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
me = bpy.data.meshes.new("RollingStatue_Gold"); bm.to_mesh(me); bm.free()
me.materials.append(M_GOLD)
gold_ob = bpy.data.objects.new("RollingStatue_Gold", me); sc.collection.objects.link(gold_ob)

vs = [v.co for o in (stone_ob, gold_ob) for v in o.data.vertices]
print("PAWN", len(stone_ob.data.polygons) + len(gold_ob.data.polygons), "faces, x %.3f .. %.3f, widest %.3f" %
      (min(v.x for v in vs), max(v.x for v in vs), 2 * max(math.hypot(v.y, v.z) for v in vs)))
for o in sc.objects:
    o.select_set(o in (stone_ob, gold_ob))
bpy.context.view_layer.objects.active = stone_ob
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "RollingStatue.fbx"), **FBX)
print("EXPORT RollingStatue.fbx")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "pawn_roll_v47.blend"))

if RENDER:
    out = os.path.join(HERE, "renders_roll_v47"); os.makedirs(out, exist_ok=True)
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 48; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 900, 1100
    w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (.05, .045, .04, 1)
    for name, loc, e in (("Key", (-2.5, -3.5, 3), 700), ("Fill", (3, -2, 1), 200), ("Rim", (1, 3, 3), 500)):
        ld = bpy.data.lights.new(name, "AREA"); ld.energy = e; ld.size = 2; ld.color = (1, .92, .82)
        lo = bpy.data.objects.new(name, ld); sc.collection.objects.link(lo); lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    # stood up for the portrait (base down), then lying as it rolls
    for o in (stone_ob, gold_ob):
        o.rotation_euler = (0, PI / 2, 0)              # base (+x) down
        o.location = (0, 0, 1.3)
    for name, loc, at, frame in (("standing", (0, -5.4, 1.6), (0, 0, 1.3), 0), ("lying", (2.6, -4.2, 2.4), (0, 0, .8), 1)):
        if frame:
            for o in (stone_ob, gold_ob):
                o.rotation_euler = (.7, 0, 0)
                o.location = (0, 0, .8)
        cd = bpy.data.cameras.new(name); cd.lens = 50
        cam = bpy.data.objects.new(name, cd); sc.collection.objects.link(cam)
        cam.location = loc
        cam.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.camera = cam
        sc.render.filepath = os.path.join(out, "pawn_" + name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDER", sc.render.filepath)
