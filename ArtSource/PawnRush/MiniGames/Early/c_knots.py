# R502: the drawbridge rope's grab knots (user 11-07: of four sketches, "A랑 C 합쳐 번갈아 가면서 나오게"). Two knots
# that alternate along the pulled line (MiniGameModels.Drawbridge: knots 1 and 3 C, 2 and 4 A):
#   A  a fat overhand knot in the rope's own colour with a gold whipping band each side;
#   C  a dark leather grip sleeve round the rope with three gold rings.
# Real metres, the rope (0.14 m thick, MgDrawbridge.RopeThick) along Blender y through the origin. The gold parts are
# their own objects ("..._gold") so the game can light them in the team colour while a pawn grips the knot
# (GrabHandle.Restyle).
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_knots.py
# Writes Export/C_KnotA.fbx and Export/C_KnotC.fbx (materials PR_Early_C_Rope, PR_Early_C_Gold, PR_Early_C_Leather).
import os, math
import bpy, bmesh
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
EXPORT = os.path.join(HERE, "Export")
ALONG = Matrix.Rotation(math.radians(90), 4, "X")      # a cylinder's axis (z) onto the rope (y)


def material(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def cylinder(bm, r, length, y, segs=32):
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r, radius2=r, depth=length,
                          matrix=Matrix.Translation((0, y, 0)) @ ALONG)


def torus(bm, R, r, mat4, major=40, minor=14):
    verts = []
    for i in range(major):
        a = 2 * math.pi * i / major
        ring = []
        for j in range(minor):
            b = 2 * math.pi * j / minor
            x = (R + r * math.cos(b)) * math.cos(a)
            y = (R + r * math.cos(b)) * math.sin(a)
            z = r * math.sin(b)
            ring.append(bm.verts.new(mat4 @ __import__("mathutils").Vector((x, y, z))))
        verts.append(ring)
    for i in range(major):
        for j in range(minor):
            bm.faces.new((verts[i][j], verts[(i + 1) % major][j], verts[(i + 1) % major][(j + 1) % minor], verts[i][(j + 1) % minor]))


def obj(name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(material(mat))
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def export(objs, path):
    for o in bpy.data.objects:
        o.select_set(o in objs)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y", bake_anim=False,
                             mesh_smooth_type="OFF", add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
    print("EXPORT", os.path.basename(path), [o.name for o in objs])


bpy.ops.wm.read_factory_settings(use_empty=True)

# ---- A: the overhand knot - three loops crossing round the rope and a round core, gold whipping each side
bm = bmesh.new()
for k, ang in enumerate((0, 60, 120)):
    m = Matrix.Rotation(math.radians(ang), 4, "Y") @ Matrix.Rotation(math.radians(90), 4, "X")
    torus(bm, .085, .045, m @ Matrix.Rotation(math.radians(90), 4, "X"))
bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=.1)
a_rope = obj("KnotA_rope", bm, "PR_Early_C_Rope")
bm = bmesh.new()
for y in (-.17, .17):
    cylinder(bm, .082, .05, y)
a_gold = obj("KnotA_gold", bm, "PR_Early_C_Gold")
export([a_rope, a_gold], os.path.join(EXPORT, "C_KnotA.fbx"))

# ---- C: the leather grip with three gold rings
bm = bmesh.new()
cylinder(bm, .095, .34, 0.)
c_leather = obj("KnotC_leather", bm, "PR_Early_C_Leather")
bm = bmesh.new()
for y in (-.17, 0., .17):
    cylinder(bm, .105, .03, y)
c_gold = obj("KnotC_gold", bm, "PR_Early_C_Gold")
export([c_leather, c_gold], os.path.join(EXPORT, "C_KnotC.fbx"))
