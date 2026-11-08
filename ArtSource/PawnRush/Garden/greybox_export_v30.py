"""Greybox hand-off of the royal water garden v30 for a teammate (승규 11-02: "팀원에게 zip 파일로 왕실 수로 정원
그레이박스화 해서 뽑아줘"). Run: blender --background --python greybox_export_v30.py -- <out dir>

Same geometry and placement as the Unity export (nothing reshaped or moved); only the look changes: every surface gets
one of four flat greybox materials (structure, floor-level water, decor, moving parts), hidden helpers stay out
except the invisible play-edge walls, which go in as their own see-through layer. Writes an FBX and a summary
(previews come from render_greybox.py).
"""
import bpy, sys, json
from pathlib import Path
from mathutils import Vector

out = Path(sys.argv[sys.argv.index('--') + 1])
out.mkdir(parents=True, exist_ok=True)
folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v30.blend'))
scene = bpy.context.scene
scene.frame_set(1)
O = bpy.data.objects


def grey(name, rgb, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Roughness'].default_value = .9
    bsdf.inputs['Alpha'].default_value = alpha
    m.diffuse_color = (*rgb, alpha)
    if alpha < 1:
        m.surface_render_method = 'BLENDED'
    return m


STONE = grey('GB_Structure', (.62, .62, .62))
WATER = grey('GB_Water', (.42, .55, .66))
DECOR = grey('GB_Decor', (.45, .47, .45))
MOVING = grey('GB_Moving', (.80, .62, .30))
EDGE = grey('GB_PlayEdge', (.85, .25, .25), .25)

MOVERS = ('회전봉_PIVOT', 'V16 |', 'V20 | robot', 'V02_필수진자_PIVOT', 'V02_백팀_보트', 'V02_흑팀_보트', 'V30 | parade')


def root_of(o):
    while o.parent:
        o = o.parent
    return o


def under_mover(o):
    while o:
        if o.name.startswith(MOVERS):
            return True
        o = o.parent
    return False


def kind(o):
    n = o.name.lower()
    mats = ' '.join(m.name.lower() for m in o.data.materials if m)
    if o.get('unity_collision_proxy'):
        return EDGE if 'castle collision' in n else None
    if 'water' in n or 'calm water' in mats or 'g09_' in mats or 'fall' in n or 'spray' in n or 'waterfall' in mats:
        return WATER
    if any(w in n for w in ('tree', 'bush', 'flower', 'lily', 'meadow', '나무', '연잎', '식재', '화분')):
        return DECOR
    if under_mover(o):
        return MOVING
    return STONE


keep, counts = [], {}
picked = []
for o in list(O):
    if o.type != 'MESH':
        continue
    visible = not o.hide_render and not o.hide_get() and not o.name.startswith('V14 |')
    k = kind(o) if (visible or o.get('unity_collision_proxy')) else None
    if k is not None:
        picked.append((o, k))
# Meshes stay shared (every V19 block uses one mesh) unless their users need different greybox materials.
kinds_of = {}
for o, k in picked:
    kinds_of.setdefault(o.data.name, set()).add(k.name)
done = set()
for o, k in picked:
    o.hide_set(False); o.hide_render = False
    if len(kinds_of[o.data.name]) > 1:
        o.data = o.data.copy()
    if o.data.name not in done:
        o.data.materials.clear()
        o.data.materials.append(k)
        for p in o.data.polygons:
            p.material_index = 0
        done.add(o.data.name)
    for s in o.material_slots:
        s.link = 'DATA'
    keep.append(o)
    counts[k.name] = counts.get(k.name, 0) + 1

# Everything else (hidden helpers, cameras, lights left in the work file) goes; parents of kept meshes stay.
needed = set(keep)
for o in keep:
    p = o.parent
    while p:
        needed.add(p); p = p.parent
for o in list(O):
    if o not in needed:
        O.remove(o)

col = bpy.data.collections.new('Royal garden greybox'); scene.collection.children.link(col)
edge = bpy.data.collections.new('Play edge (invisible walls in game)'); scene.collection.children.link(edge)
for o in needed:
    for c in list(o.users_collection):
        c.objects.unlink(o)
    (edge if o.get('unity_collision_proxy') else col).objects.link(o)

lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
tris = 0
for o in keep:
    if o.get('unity_collision_proxy'):
        continue
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    tris += sum(len(p.vertices) - 2 for p in o.data.polygons)

bpy.ops.object.select_all(action='DESELECT')
for o in needed:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out / 'RoyalWaterGarden_Greybox.fbx'), use_selection=True, object_types={'MESH', 'EMPTY'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y', bake_anim=False,
                         use_mesh_modifiers=False, mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='AUTO', embed_textures=False)

info = {'meshes': len(keep), 'by_material': counts, 'triangles': tris,
        'bounds_blender_xyz_m': [round(v, 2) for v in (*lo, *hi)]}
(out / 'greybox_info.json').write_text(json.dumps(info, indent=1, ensure_ascii=False), encoding='utf8')
print('GREYBOX', json.dumps(info, ensure_ascii=False))
