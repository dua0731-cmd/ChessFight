"""Pawn Rush library (the war room map, zone E): the ten inner gallery piers become 승규 님's stone column (R315).

    blender --background --factory-startup --python inner_piers_stone.py
    blender --background --factory-startup --python inner_piers_stone.py -- --out D:/check   (to another folder)

User request 11-03: "내부 기둥 이 에셋으로 교체해서 연결부까지 자연스럽게 해" (R310 put the column on the endgame
war room parts, which the test scene does not build; 승규 님: "벽 모델링이 안들어 갔어"). Source:
ArtSource/PawnRush/Endgame/source/stone_column.glb (Tripo, 0.24 x 0.34 x 0.98, panelled square shaft, a base and a
bolted capital, 286k triangles). Reads Assets/Resources/PawnRushLibrary/PawnRushLibraryEnvironment.fbx, gives the ten
V21/V23 Inner_gallery_pier objects one new shared mesh and writes the FBX back with export_library_fbx.SETTINGS. The
wall piers (GEO_pier) keep the coursed pier mesh they shared with the inner ones. Running it again changes nothing.

How it joins (placement unchanged, pier centres at x +-9.05):
- The deep side (source y) turned onto x and scaled evenly so the base and capital are 2.14 m deep, the old pier's
  width: the inner face stays at |x| 7.98, where the wall lanterns hang, and the shaft is narrower along the gallery.
- Base on the lower floor (z -8), capital top at 9.44 under the old upper shaft (1.5 m square from 9.4) and the
  capital mouldings, so the arches still spring from the same place.
- Not stretched: cut at two stone joints of the shaft (z 0.278 and 0.614 of the source) and that two-panel band
  stacked between the base and capital (within 6 % of its height). Each cut piece is capped before it is decimated,
  so a hairline between pieces shows stone, never a hole.
- Flat stone faces get box-axis normals (decimated panels otherwise shade like pillows); the library's one material
  (CLAY | uniform neutral gray); UVs as box projection at 1 = 4 m.
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
from export_library_fbx import SETTINGS  # noqa: E402

FBX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary",
                                    "PawnRushLibraryEnvironment.fbx"))
SRC = os.path.normpath(os.path.join(HERE, "..", "Endgame", "source", "stone_column.glb"))
PIERS = ("V21_Inner_gallery_pier", "V23_Inner_gallery_pier_center")
MESH = "R315 inner stone column"
CUT_LO, CUT_HI = 0.278, 0.614      # two shaft joints, two panels apart (source units)
DEPTH_HALF = 1.07                  # the old pier's half width
BOTTOM, TOP = -8.0, 9.44           # the lower floor and the old pier's top
TRIS = 3000
SHARE = (0.18, 0.28)               # triangles for the base piece and the capital piece; the rest is the shaft
AXES = [Vector(v) for v in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]
SNAP = math.radians(35)


def tris(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def column_mesh():
    """The stone column from BOTTOM to TOP in pier space (origin on the pier's floor-level centre, z world)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=SRC)
    src = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    assert len(src) == 1, [o.name for o in src]
    src = src[0]
    bm = bmesh.new()
    bm.from_mesh(src.data)
    bm.transform(Matrix.Rotation(math.radians(90), 4, "Z") @ src.matrix_world)
    zmin = min(v.co.z for v in bm.verts)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, -zmin))
    src_h = max(v.co.z for v in bm.verts)
    s = DEPTH_HALF / max(abs(v.co.x) for v in bm.verts if v.co.z < .1 * src_h)
    for o in set(bpy.data.objects) - before:
        bpy.data.objects.remove(o)

    def piece(lo, hi):
        p = bm.copy()
        for z, keep_above in ((lo, True), (hi, False)):
            if z is None:
                continue
            bmesh.ops.bisect_plane(p, geom=p.verts[:] + p.edges[:] + p.faces[:], plane_co=(0, 0, z),
                                   plane_no=(0, 0, 1), clear_inner=keep_above, clear_outer=not keep_above, dist=1e-6)
        bmesh.ops.holes_fill(p, edges=[e for e in p.edges if e.is_boundary], sides=0)
        bmesh.ops.triangulate(p, faces=p.faces[:])
        me = bpy.data.meshes.new("piece")
        p.to_mesh(me)
        p.free()
        return me

    def decimate(me, target):
        o = bpy.data.objects.new("dec", me)
        bpy.context.scene.collection.objects.link(o)
        m = o.modifiers.new("dec", "DECIMATE")
        m.ratio = min(1.0, target / max(tris(me), 1))
        with bpy.context.temp_override(object=o, active_object=o):
            bpy.ops.object.modifier_apply(modifier=m.name)
        bpy.data.objects.remove(o)
        return me

    height = TOP - BOTTOM
    band_h = (CUT_HI - CUT_LO) * s
    fixed_h = (CUT_LO + src_h - CUT_HI) * s
    count = max(1, round((height - fixed_h) / band_h))
    stretch = (height - fixed_h) / (count * band_h)
    print("scale %.3f, band %.3f m x %d, stretch %.4f" % (s, band_h, count, stretch))
    assert abs(stretch - 1) < .06, stretch
    t_base, t_cap = TRIS * SHARE[0], TRIS * SHARE[1]
    base = decimate(piece(None, CUT_LO), t_base)
    band = decimate(piece(CUT_LO, CUT_HI), (TRIS - t_base - t_cap) / count)
    cap = decimate(piece(CUT_HI, None), t_cap)
    bm.free()

    out = bmesh.new()

    def add(me, src_z, at, zscale):
        b = bmesh.new()
        b.from_mesh(me)
        b.transform(Matrix.Translation((0, 0, at - src_z * s * zscale)) @ Matrix.Diagonal((s, s, s * zscale, 1)))
        tmp = bpy.data.meshes.new("tmp")
        b.to_mesh(tmp)
        b.free()
        out.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)

    add(base, 0.0, BOTTOM, 1.0)
    z = BOTTOM + CUT_LO * s
    for _ in range(count):
        add(band, CUT_LO, z, stretch)
        z += band_h * stretch
    add(cap, CUT_HI, z, 1.0)
    for me in (base, band, cap):
        bpy.data.meshes.remove(me)
    return out


def finish(bm, me, uv_names):
    """bm (pier space, already in the mesh's local space) into `me`: box UVs, axis-snapped normals."""
    layers = [bm.loops.layers.uv.new(n) for n in uv_names] or [bm.loops.layers.uv.new("UVMap")]
    for f in bm.faces:
        f.material_index = 0
        ax = max(range(3), key=lambda i: abs(f.normal[i]))
        a, c = [(1, 2), (0, 2), (0, 1)][ax]
        for l in f.loops:
            for uv in layers:
                l[uv].uv = (l.vert.co[a] / 4.0, l.vert.co[c] / 4.0)
    bm.to_mesh(me)
    bm.free()
    normals = []
    for p in me.polygons:
        n = p.normal
        best = max(AXES, key=lambda a: a.dot(n))
        normals.extend([tuple(best if best.angle(n) < SNAP else n)] * p.loop_total)
    me.normals_split_custom_set(normals)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = argv[argv.index("--out") + 1] if "--out" in argv else os.path.dirname(FBX)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)

    piers = [o for o in col.all_objects if o.type == "EMPTY" and o.name.startswith(PIERS)]
    meshes = [c for p in piers for c in p.children if c.type == "MESH"]
    if len(meshes) != 10:
        raise SystemExit(f"expected 10 inner piers, found {len(meshes)}; stopped")
    if all(m.data.name == MESH for m in meshes):
        print("inner piers already stone columns")
        return
    old = meshes[0].data
    # Pier space -> the mesh's local space: every inner pier has the same transform apart from where it stands.
    to_local = meshes[0].matrix_world.inverted() @ Matrix.Translation(
        (piers[0].matrix_world.translation.x, piers[0].matrix_world.translation.y, 0))
    for m in meshes[1:]:
        other = m.matrix_world.inverted() @ Matrix.Translation(
            (m.parent.matrix_world.translation.x, m.parent.matrix_world.translation.y, 0))
        assert all(abs(a - b) < 1e-4 for r0, r1 in zip(to_local, other) for a, b in zip(r0, r1)), m.name
    bm = column_mesh()
    bm.transform(to_local)
    me = bpy.data.meshes.new(MESH)
    for mat in old.materials:
        me.materials.append(mat)
    finish(bm, me, [uv.name for uv in old.uv_layers])
    for m in meshes:
        m.data = me
        for slot in m.material_slots:   # the imported piers carry the clay on the object; the new mesh carries it
            slot.link = "DATA"
    print(f"{len(meshes)} inner piers -> {MESH}, {tris(me)} triangles each (was {tris(old)})")

    view.active_layer_collection = view.layer_collection.children[col.name]
    path = os.path.join(out_dir, "PawnRushLibraryEnvironment.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {path}")


main()
