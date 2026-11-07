# B side wall module (R503): the user's "Stone Wall Archway" FBX (replaces the earlier Tripo arcade wall,
# B_WindowWall_v1) as the repeating side wall of the B East Wing: cream ashlar, an open round-arched window with a
# stone sill, walnut wainscot below. Concept: Docs/PawnRush/ConceptArt/R503_RepairWing/B_01_overview.png side walls,
# B_02_architecture_modules.png item 4.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python b_wall_arch_v1.py -- [--render] [--export]
#
# Source: Source/R503_StoneWallArchway.fbx (1.0 x 0.32 x 0.81, 500k triangles, double-sided: the back is a second
# facade). Uniform scale x6: a 6 x 4.85 m module, wainscot 1.6 m (a pawn is about 1 m).
# Origin = the wall slab's middle plane (source y 0.06), floor level, centre; Blender -y faces the room.
# MiniGameModels hangs it along both side walls (x +-22, slab centre in the 1 m collision wall), centres z 2, 8, 14,
# 20 and 33, so no pilaster stands where the plank piles are (z 6..8, x +-18.75..21.25).
# Fixes: the bottom plinth band of the source is broken (the front rail floats over a gap, holes in the feet and in
# the back). Everything below source z -0.355 is removed and replaced by a clean walnut plinth: a centre band and a
# deeper block under each end pilaster, slot "PR_Mid_WalnutFloor" (one wide board, as the centre gate's skirting).
# A warm glowing pane (slot "PR_Mid_WindowGlass") fills the window at the middle plane and hides the shell wall behind.
import bpy, bmesh, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import b_center_gate_v1 as cg  # noqa: E402  (helpers: decimate, tris, retexture-style toning, floor_material)

SRC = os.path.join(HERE, "Source", "R503_StoneWallArchway.fbx")
TEX = cg.TEX
OUT_FBX = cg.OUT_FBX
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

SCALE = 6.0
S_MID_Y = .06                 # middle plane of the wall slab (front face +.019, back +.100)
S_CUT_Z = -.355               # broken plinth band below this
TARGET = 16000
# clean plinth in source units: centre band and the two end pilaster feet
PLINTH = [((-.36, .36), (-.03, .15)), ((-.495, -.36), (-.142, .142)), ((.36, .495), (-.142, .142))]
GLASS = ((-.17, .17), (-.20, .335))   # x, z of the pane (edges hide inside the stone)


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    lo = min(v.co.z for v in ob.data.vertices)
    # drop the broken plinth band
    bm = bmesh.new(); bm.from_mesh(ob.data)
    dead = [f for f in bm.faces if all(v.co.z < S_CUT_Z for v in f.verts)]
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(ob.data); bm.free()
    print("CUT plinth faces", len(dead))
    for v in ob.data.vertices:
        v.co = Vector((v.co.x * SCALE, (v.co.y - S_MID_Y) * SCALE, (v.co.z - lo) * SCALE))
    ob.name = ob.data.name = "B_WallArch"
    return ob, lo


def retexture(mat):
    """Same toning as the centre gate (no bright gold expected); writes PR_Mid_WallArch maps."""
    import numpy as np
    nodes = mat.node_tree.nodes
    imgs = {n.image.name: n for n in nodes if n.type == 'TEX_IMAGE' and n.image}
    base, normal = imgs["base_color"].image, imgs["normal"].image
    out = base.copy(); out.name = "PR_Mid_WallArch_BaseColor"
    out.scale(2048, 2048)
    out.filepath_raw = os.path.join(TEX, "PR_Mid_WallArch_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nrm = normal.copy(); nrm.name = "PR_Mid_WallArch_Normal"; nrm.colorspace_settings.name = 'Non-Color'
    nrm.filepath_raw = os.path.join(TEX, "PR_Mid_WallArch_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    imgs["base_color"].image = out; imgs["normal"].image = nrm
    mat.name = "PR_Mid_WallArch"
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Roughness"].default_value = .8
    bsdf.inputs["Metallic"].default_value = 0.0


def glass_material():
    m = bpy.data.materials.new("PR_Mid_WindowGlass"); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (1., .86, .62, 1)
    b.inputs["Emission Color"].default_value = (1., .80, .52, 1)
    b.inputs["Emission Strength"].default_value = 1.4
    b.inputs["Roughness"].default_value = .3
    return m


def add_parts(ob, s_lo):
    """Clean plinth (walnut board slot) and the window pane (glass slot), joined into the module."""
    floor = cg.floor_material(); ob.data.materials.append(floor); f_slot = len(ob.data.materials) - 1
    bm = bmesh.new(); bm.from_mesh(ob.data)
    uv = bm.loops.layers.uv.verify()
    z1 = (S_CUT_Z + .02 - s_lo) * SCALE      # a little over the cut so the slit under the centre panels is covered
    kv = .2 / z1
    for (xa, xb), (ya, yb) in PLINTH:
        res = bmesh.ops.create_cube(bm, size=1.)
        for v in res["verts"]:
            v.co = Vector(((xa if v.co.x < 0 else xb) * SCALE, ((ya if v.co.y < 0 else yb) - S_MID_Y) * SCALE,
                           0. if v.co.z < 0 else z1))
        for f in {f for v in res["verts"] for f in v.link_faces}:
            f.material_index = f_slot
            n = f.normal
            for l in f.loops:
                p = l.vert.co
                if abs(n.z) > .5: l[uv].uv = (p.x / 2, .4 + p.y * kv)
                elif abs(n.x) > .5: l[uv].uv = (p.y / 2, .4 + p.z * kv)
                else: l[uv].uv = (p.x / 2, .4 + p.z * kv)
    # No glass (user, 2026-11-07: "창문 밖이 보여야지"): the window stays open and B_ShellEnv leaves a hole behind it.
    bm.to_mesh(ob.data); bm.free()


def render():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.9, .8, .68, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .55
    sc.world = w
    import math
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(55), 0, math.radians(-25)))
    bpy.context.object.data.energy = 3.2; bpy.context.object.data.color = (1, .9, .78)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    views = {"front": ((0, -14, 2.4), (0, 0, 2.4), 35), "q34": ((-7, -9, 3.5), (0, 0, 2.2), 32),
             "base": ((-3.5, -4.2, .8), (-2.2, 0, .5), 28), "back": ((3, 9, 2.5), (0, 0, 2.2), 32)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_b_wall_%s.png" % name)
        bpy.ops.render.render(write_still=True)


def main():
    ob, s_lo = load()
    cg.TARGET = TARGET
    print("DECIMATE", cg.decimate(ob))
    retexture(ob.data.materials[0])
    add_parts(ob, s_lo)
    lo = [min(v.co[i] for v in ob.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in ob.data.vertices) for i in range(3)]
    print("FIT tris", cg.tris(ob), "lo", [round(x, 2) for x in lo], "hi", [round(x, 2) for x in hi])
    if "--export" in ARGS:
        os.makedirs(OUT_FBX, exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, "B_WallArch.fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                 mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "B_WallArch_v1.blend"))
    if "--render" in ARGS: render()


main()
