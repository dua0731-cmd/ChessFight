"""The Royal Observatory's graybox in Blender (R0, seoungtest R725, 2026-11-19).

    blender -b --python ArtSource/PawnRush/RoyalObservatory/ro_blockout.py      -> ro_blockout.blend
    (or in an open Blender: exec(open(r"...\\ro_blockout.py").read()) - only the "RO ..." collections are rebuilt)

Reads ro_blockout.json, which Unity writes (Assets/Scripts/PawnRushEditor/RoyalObservatoryBlockoutDump.cs: after a
compile when the builder is newer, or ChessFight > Pawn Rush > Royal Observatory > Dump Blockout For Blender), and
stands every built object up where Unity has it: one collection a builder group ("0 Entry tunnel", "1 Big Dipper" ...),
object names = Unity names (custom property unity_path holds the whole path), Unity's cube / cylinder / sphere as shared
unit meshes and the builder's own meshes (petals, gear rims) as they are. Groups and devices (Device_*) are empties so
a device's frame can be seen; drawn objects are not parented (their matrix is the world one). Colour = the Unity
material's colour, material named after it. Triggers and kill volumes are hidden, inactive objects too.
Axes: Blender (x, y, z) = Unity (x, z, y), metres - as ro_kit.
"""
import json, os, sys
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else \
    r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\RoyalObservatory"
DUMP = os.path.join(HERE, "ro_blockout.json")
OUT = os.path.join(HERE, "ro_blockout.blend")
C = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def unit_meshes():
    import bmesh
    got = {}
    for kind in ("cube", "cylinder", "sphere"):
        name = "RO_unit_" + kind
        me = bpy.data.meshes.get(name)
        if me is None:
            me = bpy.data.meshes.new(name)
            bm = bmesh.new()
            if kind == "cube":
                bmesh.ops.create_cube(bm, size=1.0)
            elif kind == "cylinder":   # Unity's: radius 0.5, height 2 along its y (Blender z)
                bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=.5, radius2=.5, depth=2.0)
            else:
                bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=.5)
            bm.to_mesh(me)
            bm.free()
        got[kind] = me
    return got


def built_mesh(i, spec):
    name = "RO_built_%d_%s" % (i, spec["name"])
    me = bpy.data.meshes.get(name)
    if me is not None:
        return me
    v = spec["vertices"]
    verts = [(v[k], v[k + 2], v[k + 1]) for k in range(0, len(v), 3)]          # Unity (x, y, z) -> (x, z, y)
    t = spec["triangles"]
    faces = [(t[k], t[k + 2], t[k + 1]) for k in range(0, len(t), 3)]          # the swap mirrors: reverse the winding
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.validate()
    return me


def material(name, colour):
    key = "RO_blk_" + (name or "none")
    m = bpy.data.materials.get(key)
    if m is None:
        m = bpy.data.materials.new(key)
        m.use_nodes = True
        p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        c = tuple(colour[:3]) + (1.0,) if colour else (.6, .6, .6, 1)
        p.inputs["Base Color"].default_value = c
        if colour and len(colour) > 3 and colour[3] < .99:
            p.inputs["Alpha"].default_value = max(.15, colour[3])
            try:
                m.surface_render_method = "BLENDED"
            except (AttributeError, TypeError):
                pass
        m.diffuse_color = (c[0], c[1], c[2], colour[3] if colour and len(colour) > 3 else 1)
    return m


def collection(name, parent):
    col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if col.name not in [c.name for c in parent.children]:
        parent.children.link(col)
    return col


def build():
    with open(DUMP, encoding="utf-8") as f:
        dump = json.load(f)
    old = bpy.data.collections.get("RO Blockout")
    if old:
        for o in list(old.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
        for c in list(old.children_recursive):
            bpy.data.collections.remove(c)
        bpy.data.collections.remove(old)
    top = collection("RO Blockout", bpy.context.scene.collection)
    units = unit_meshes()
    meshes = {}
    nodes = dump["nodes"]
    hidden_chain = {}
    count = 0
    for i, n in enumerate(nodes):
        group = n["path"].split("/")[0]
        col = collection("RO " + group, top)
        m = C @ Matrix([n["matrix"][0:4], n["matrix"][4:8], n["matrix"][8:12], n["matrix"][12:16]]) @ C
        draws = n.get("draws")
        if draws:
            if draws.startswith("mesh:"):
                k = int(draws[5:])
                if k not in meshes:
                    meshes[k] = built_mesh(k, dump["meshes"][k])
                me = meshes[k]
            else:
                me = units[draws]
            if len(me.materials) == 0:
                me.materials.append(None)
            ob = bpy.data.objects.new(n["name"], me)
            ob.matrix_world = m
            mat = material(n.get("material"), n.get("colour"))
            ob["material"] = n.get("material", "")
            ob.color = mat.diffuse_color
            # one material per object without touching the shared mesh: the slot is linked to the object
            ob.material_slots[0].link = "OBJECT"
            ob.material_slots[0].material = mat
        else:
            ob = bpy.data.objects.new(n["name"], None)
            ob.empty_display_type = "ARROWS" if n["name"].startswith("Device_") else "PLAIN_AXES"
            ob.empty_display_size = 1.5 if n["name"].startswith("Device_") else .3
            ob.matrix_world = m
        ob["unity_path"] = n["path"]
        if n.get("collider"):
            ob["collider"] = n["collider"]
        if n.get("components"):
            ob["components"] = ", ".join(n["components"])
        col.objects.link(ob)
        inactive = (not n["active"]) or hidden_chain.get(n["parent"], False)
        hidden_chain[i] = inactive
        trigger = "trigger" in (n.get("collider") or "") or n["path"].startswith("KillVolumes")
        if inactive or trigger:
            ob.hide_set(True)
            ob.hide_render = True
        count += 1
    print("[ro_blockout]", count, "objects,", len(meshes), "built meshes, from", DUMP)


def scene_setup():
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    for area in (bpy.context.screen.areas if bpy.context.screen else []):
        if area.type == "VIEW_3D":
            for sp in area.spaces:
                if sp.type == "VIEW_3D":
                    sp.shading.color_type = "OBJECT"
                    sp.clip_end = 2000


build()
scene_setup()
if bpy.app.background:
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
