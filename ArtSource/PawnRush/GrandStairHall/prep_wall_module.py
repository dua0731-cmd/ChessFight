# Outer wall module from source/castle_arch_wall.glb (0.748 wide x 0.26 deep x 0.981 tall, one fused mesh, no UV):
# cut the hanging banner off the front face and close the hole flat on the niche back, then rebuild the back
# face as the front's mirror (the asset is the same design on both faces; its back banner was harder to cut),
# decimate and save wall_module.blend. Front face = -Y, the wall's inner face when placed.
# Run: blender -b -P prep_wall_module.py
import bpy, bmesh, math, os
HERE = os.path.dirname(os.path.abspath(__file__))
NICHE = 0.004                 # front niche back (y); the back niche is at 0.023
MID = (0.004 + 0.023) / 2     # the thin niche slab's middle: mirror plane

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", "castle_arch_wall.glb"))
src = [o for o in bpy.data.objects if o.type == "MESH"][0]
bm = bmesh.new(); bm.from_mesh(src.data); bm.transform(src.matrix_world)


def banner(c):
    proud = NICHE - c.y
    if -0.095 <= c.x <= 0.097 and 0.31 <= c.z <= 0.74 and proud > 0.008:
        return True
    return -0.072 <= c.x <= 0.072 and 0.735 <= c.z <= 0.785 and proud > 0.012   # rod and hanger


dead = [f for f in bm.faces if banner(f.calc_center_median()) or f.calc_center_median().y > MID]
bmesh.ops.delete(bm, geom=dead, context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
hole = [e for e in bm.edges if e.is_boundary and all(-0.12 < v.co.x < 0.12 and 0.28 < v.co.z < 0.8 and v.co.y < MID - 0.0015
                                                     for v in e.verts)]
res = bmesh.ops.holes_fill(bm, edges=hole, sides=0)
for f in res["faces"]:
    if all(abs(v.co.y - NICHE) < 0.02 for v in f.verts):
        for v in f.verts:
            v.co.y = NICHE          # on the niche back: flat (a cut in the arch soffit keeps its shape)
bmesh.ops.triangulate(bm, faces=res["faces"])
# The hanger went through the slab: a flat patch just in front of the niche back closes it.
patch = [bm.verts.new((x, NICHE - 0.001, z)) for x, z in ((-0.04, 0.725), (0.04, 0.725), (0.04, 0.77), (-0.04, 0.77))]
bm.faces.new(patch)
print("BANNER faces removed", len(dead), "holes filled", len(res["faces"]))

# Back = mirror of the front through the slab's middle, welded on the cut.
back = bmesh.ops.duplicate(bm, geom=bm.faces[:] + bm.edges[:] + bm.verts[:])
bv = [g for g in back["geom"] if isinstance(g, bmesh.types.BMVert)]
for v in bv:
    v.co.y = 2 * MID - v.co.y
bmesh.ops.reverse_faces(bm, faces=[g for g in back["geom"] if isinstance(g, bmesh.types.BMFace)])
bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if abs(v.co.y - MID) < 0.003], dist=0.002)

me = bpy.data.meshes.new("WallModule")
bm.to_mesh(me); bm.free()
ob = bpy.data.objects.new("WallModule", me)
bpy.context.scene.collection.objects.link(ob)
bpy.data.objects.remove(src)
bpy.context.view_layer.objects.active = ob
mod = ob.modifiers.new("Decimate", "DECIMATE")
mod.ratio = 0.1
bpy.ops.object.modifier_apply(modifier=mod.name)
ob.select_set(True)
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
vs = [v.co for v in me.vertices]
print("TRIS", sum(len(p.vertices) - 2 for p in me.polygons), "BOUNDS",
      [round(min(v[i] for v in vs), 3) for i in range(3)], [round(max(v[i] for v in vs), 3) for i in range(3)])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "wall_module.blend"))
