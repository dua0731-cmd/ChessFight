"""R296: v30 -> v31. Run in Blender 5.2: blender --background --python build_v31.py

User request 11-02: "정원 삼각형을 400만 V19 위주로 줄이고" - the exported garden was 6.47 M triangles. Nothing moves
and nothing is added or removed: only the densest shared meshes are decimated in place (collapse), so every object keeps
its name, transform, material and collider role.
- V19: the three bevelled brick meshes (21,404 triangles each) dress 92 blocks, 1.97 M triangles in all. Each goes to
  about 2,600 (PITFALLS 31: editing the shared mesh reshapes every block, which is what is wanted here). Edges past
  35 degrees turn sharp so the flat faces shade flat (smooth shading over the collapsed fans streaked the block tops).
- V16: the six zigzag C platforms (moving mesh colliders) and the royal boat mesh the two team boats share
  (186,546 / 186,474 triangles each) go to about half; the platforms' collision gets cheaper with them.
"""
import bpy, bmesh, json, math
from pathlib import Path

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v30.blend'))
scene = bpy.context.scene
scene.frame_set(1)
report = {'meshes': {}}

V19_TARGET = 2600
V16_RATIO = .5


def tris(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def decimate(me, ratio):
    """Collapse the mesh data itself, keeping its name (Unity's mesh sub-asset ids and saved colliders follow it)."""
    before = tris(me)
    probe = bpy.data.objects.new('decimate probe', me.copy())
    scene.collection.objects.link(probe)
    mod = probe.modifiers.new('Decimate', 'DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    low = bpy.data.meshes.new_from_object(probe.evaluated_get(dg))
    bm = bmesh.new()
    bm.from_mesh(low)
    bm.to_mesh(me)
    bm.free()
    me.update()
    old = probe.data
    bpy.data.objects.remove(probe)
    bpy.data.meshes.remove(old)
    bpy.data.meshes.remove(low)
    users = sum(1 for o in bpy.data.objects if o.data == me)
    report['meshes'][me.name] = {'before': before, 'after': tris(me), 'users': users}


for name in ('V19 | shared brick structural module', 'V19 | supplied brick morphed into round pillar',
             'V19 | shared brick structural module.001'):
    me = bpy.data.meshes[name]
    decimate(me, V19_TARGET / tris(me))
    me.set_sharp_from_angle(angle=math.radians(35))

seen = set()
for o in bpy.data.objects:
    if o.type != 'MESH' or not o.name.startswith('V16 |') or o.data in seen:
        continue
    if 'C platform zigzag' in o.name or 'royal boat' in o.name:
        seen.add(o.data)
        decimate(o.data, V16_RATIO)

saved = sum((m['before'] - m['after']) * m['users'] for m in report['meshes'].values())
report['triangles_removed_in_scene'] = saved
print(json.dumps(report, indent=1, ensure_ascii=False))
(folder / 'build_v31_report.json').write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v31.blend'), compress=True)
print('SAVED v31')
