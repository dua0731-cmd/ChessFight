"""Pawn Rush library (작전 회의실): wall anchors on the tie beams' ends, so the roof reads as a tied vault (R381).

    blender --background --factory-startup --python wall_anchors.py                       (writes the FBX)
    blender --background --factory-startup --python wall_anchors.py -- --preview D:/check (renders, writes nothing)

승규 님 asked whether the roof could stand (a 30 m, flat stone vault on 0.8 m walls with no buttresses would push
them out); of the fixes offered, "1번 하고 유니티에 넣어": show that the steel beams at the springing are ties pulling
the walls together, as in 19th-century tied vaults, by their anchors on the outside.

  side walls  each transverse beam (7, y -19.2 .. 19.2 every 6.4 m, z 11.77 .. 12.23, ending in the piers' upper shafts
              at x +-15) gets an X-shaped cast-iron pattress plate on the shaft's outer face (x +-15.75), just under the
              capital (centre z 11.45): tapered arms with bolt heads, a round boss, a hex nut and the rod's end.
  rear wall   the two longitudinal beams (x +-9.05) stopped 1.8 m short of it (y 22.5); they are carried on to the rear
              wall (an extension to its inner face, y 24.31) and anchored on its outer face (y 25.07, centre z 11.8).
The front ends stay as they are (the hall's front is open). Anchors in "Library steel" (Unity's flat dark iron,
Course01v4Builder.LibraryLooks), the beam extensions in the environment's clay like the beams. Running it again
changes nothing (it looks for Wall_tie_anchors first).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

FBX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary",
                                    "PawnRushLibraryEnvironment.fbx"))
CLAY = "CLAY | uniform neutral gray"
BEAM_YS = (-19.2, -12.8, -6.4, 0.0, 6.4, 12.8, 19.2)
SHAFT_FACE, SIDE_Z = 15.75, 11.45
LONG_XS, REAR_IN, REAR_OUT, LONG_END = (-9.05, 9.05), 24.31, 25.07, 22.5
REAR_Z = 11.8                          # the plate stays under the rear wall's top (12.25)
BEAM_Z = (11.77, 12.23)
ARM, ROOT_W, TIP_W, PLATE_T = .40, .13, .06, .035


def anchor(bm, centre, normal, up):
    """One pattress plate on the face through centre looking along normal (outward), arms at 45 degrees."""
    n = Vector(normal).normalized()
    u = Vector(up).normalized()
    r = u.cross(n).normalized()
    c = Vector(centre)
    frame = Matrix((r, u, n)).transposed()           # local (x right, y up, z out) -> world

    def at(x, y, z):
        return c + frame @ Vector((x, y, z))

    def slab(outline, z0, z1):
        lo = [bm.verts.new(at(x, y, z0)) for x, y in outline]
        hi = [bm.verts.new(at(x, y, z1)) for x, y in outline]
        faces = [bm.faces.new(lo[::-1]), bm.faces.new(hi)]
        for i in range(len(outline)):
            j = (i + 1) % len(outline)
            faces.append(bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
        bmesh.ops.recalc_face_normals(bm, faces=faces)

    def disc(x, y, z0, z1, rad, seg=20):
        slab([(x + rad * math.cos(2 * math.pi * k / seg), y + rad * math.sin(2 * math.pi * k / seg))
              for k in range(seg)], z0, z1)

    def dome(x, y, z, rad):
        res = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=6, radius=rad)
        below = [v for v in res["verts"] if v.co.z < -1e-6]
        keep = [v for v in res["verts"] if v.co.z >= -1e-6]
        bmesh.ops.delete(bm, geom=below, context="VERTS")
        for v in keep:
            v.co = at(x + v.co.x, y + v.co.y, z + v.co.z * .6)
        for f in {f for v in keep for f in v.link_faces}:
            f.smooth = True

    for k in range(4):                                # the X: four tapered arms with rounded tips and a bolt each
        a = math.radians(45 + 90 * k)
        d, s = Vector((math.cos(a), math.sin(a))), Vector((-math.sin(a), math.cos(a)))
        pts = [(s * ROOT_W / 2), (d * ARM + s * TIP_W / 2), (d * ARM - s * TIP_W / 2), (-s * ROOT_W / 2)]
        slab([(p.x, p.y) for p in pts], 0, PLATE_T)
        tip = d * ARM
        disc(tip.x, tip.y, 0, PLATE_T, TIP_W * .75, 12)
        bolt = d * (ARM - .06)
        dome(bolt.x, bolt.y, PLATE_T, .028)
    disc(0, 0, 0, PLATE_T + .03, .13, 24)             # boss
    slab([(.075 * math.cos(math.pi / 3 * k), .075 * math.sin(math.pi / 3 * k)) for k in range(6)],
         PLATE_T + .03, PLATE_T + .085)                # hex nut
    disc(0, 0, PLATE_T + .085, PLATE_T + .13, .036, 14)    # the rod's end
    dome(0, 0, PLATE_T + .13, .036)


def build():
    bm = bmesh.new()
    for s in (-1, 1):
        for y in BEAM_YS:
            anchor(bm, (s * SHAFT_FACE, y, SIDE_Z), (s, 0, 0), (0, 0, 1))
    for x in LONG_XS:
        anchor(bm, (x, REAR_OUT, REAR_Z), (0, 1, 0), (0, 0, 1))
    me = bpy.data.meshes.new("Wall tie anchors")
    bm.to_mesh(me)
    bm.free()
    ext = bmesh.new()
    for x in LONG_XS:
        r = bmesh.ops.create_cube(ext, size=1.0)
        lo, hi = (x - .15, LONG_END, BEAM_Z[0]), (x + .15, REAR_IN + .02, BEAM_Z[1])
        for v in r["verts"]:
            v.co = Vector(((lo[i] + hi[i]) / 2 + v.co[i] * (hi[i] - lo[i]) for i in range(3)))
    em = bpy.data.meshes.new("Longitudinal beam to rear wall")
    ext.to_mesh(em)
    ext.free()
    return me, em


def steel():
    m = bpy.data.materials.get("Library steel")
    if m is None:
        m = bpy.data.materials.new("Library steel")
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (.07, .08, .095, 1)
        b.inputs["Metallic"].default_value = .85
        b.inputs["Roughness"].default_value = .38
    return m


def add(col):
    me, em = build()
    me.materials.append(steel())
    em.materials.append(bpy.data.materials.get(CLAY))
    for name, mesh in (("Wall_tie_anchors", me), ("Longitudinal_beam_to_rear_wall", em)):
        ob = bpy.data.objects.new(name, mesh)
        col.objects.link(ob)
        print("ADD", name, len(mesh.polygons), "faces")


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    return col


def preview(out):
    from cannon_conveyor_look import shoot, stage
    os.makedirs(out, exist_ok=True)
    col = load()
    if bpy.data.objects.get("Wall_tie_anchors") is None:
        add(col)
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_backface_culling = True
    clay = bpy.data.materials.get(CLAY)
    clay.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.46, .36, .27, 1)
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
    sun, cam = stage(sc)
    shoot(sc, sun, cam, out, (
        ("anchors_side.png", (28.0, -2.0, 13.5), (15.75, 3.0, 11.0), 24, 230),
        ("anchor_close.png", (17.8, -.9, 11.9), (15.75, 0.0, 11.45), 40, 230),
        ("anchors_rear.png", (12.0, 31.0, 12.6), (9.05, 25.07, 11.8), 30, 160),
        ("anchors_left.png", (-28.0, -2.0, 13.5), (-15.75, 3.0, 11.0), 24, 130),
    ))


def apply():
    from export_library_fbx import SETTINGS
    col = load()
    if bpy.data.objects.get("Wall_tie_anchors") is not None:
        print("wall anchors: already in the FBX")
        return
    add(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {FBX}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])
    else:
        apply()
