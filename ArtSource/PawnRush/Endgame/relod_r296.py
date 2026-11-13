"""R296: the war room's level of detail, done again. Run in Blender 5.2:
  blender --background --factory-startup --python relod_r296.py -- [--dry]

User request 11-02: "작전 회의실 120만으로 맞추면서 level of detail 다시 해". With every placement at LOD0 the war room
drew 1.47 M triangles, half of it the 468 bookshelves. This works on the exported part FBX files Unity already uses
(Assets/Art/PawnRushMap/Endgame/Resources/PawnRushEndgame/<Part>/<Part>.fbx; the per-part folders under ArtSource are
not in this checkout) and writes them back at the same paths, so their .meta, GUID and material remaps stay:
- LOD0: the heavy repeated decor (at least LOD0_MIN triangles and REPEATED placements) is decimated by one shared
  ratio chosen so the whole room, every placement at LOD0, comes to BUDGET. Everything else keeps its LOD0.
- LOD1 / LOD2 are made again from the new LOD0 (collapse LOD_RATIOS), never below LOD_FLOOR triangles, so small parts
  are not crushed into broken shapes.
- Shading: imported custom normals are dropped and edges past SHARP degrees turn sharp, the same on every LOD.
- KEEP parts stay as they were: the hall wall panel is hundreds of loose bricks, and collapsing it lumps the bricks
  and folds faces inside out (holes in Unity, which culls back faces).
- Guard: a new LOD with FLIPPED more of its area facing inward than the old file's LOD of the same level is replaced
  by that old LOD.
Course01v4Builder.WarRoomModels sets the switch heights (and culls small decor) on each placement's LODGroup.
"""
import bpy, json, math, sys
from collections import Counter
from mathutils.bvhtree import BVHTree
from pathlib import Path

DRY = '--dry' in sys.argv
here = Path(__file__).resolve().parent
models = here.parents[2] / 'Assets/Art/PawnRushMap/Endgame/Resources/PawnRushEndgame'
BUDGET = 1_200_000
LOD0_MIN, REPEATED = 1000, 20
LOD_RATIOS = (0.6, 0.3)
LOD_FLOOR = (150, 80)
SHARP = 30.0
KEEP = ('PR_EG_HallWallPanel',)
FLIPPED = 0.02

manifest = json.loads((models / 'PR_EG_manifest.json').read_text(encoding='utf8'))
uses = Counter(p['fbx'].split('/')[0] for p in manifest)


def tris(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), bake_space_transform=True)
    lods = {}
    for o in bpy.data.objects:
        if o.type == 'MESH' and '_LOD' in o.name:
            lods[int(o.name.rsplit('_LOD', 1)[1][:1])] = o
    return lods


def bounds(objs):
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    return [min(p[i] for p in pts) for i in range(3)] + [max(p[i] for p in pts) for i in range(3)]


def decimated(src, name, ratio):
    o = bpy.data.objects.new(name, src.data.copy())
    o.matrix_world = src.matrix_world.copy()
    bpy.context.scene.collection.objects.link(o)
    if ratio < 1.0:
        mod = o.modifiers.new('Decimate', 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
            bpy.ops.object.modifier_apply(modifier='Decimate')
    return o


def shade(o):
    me = o.data
    if me.has_custom_normals:
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
    me.set_sharp_from_angle(angle=math.radians(SHARP))


def flipped(o):
    """Share of `o`'s area that faces inward: a ray from just in front of a face, along its normal, first meets the
    mesh's own surface from behind. Measured on the mesh itself (against the old LOD0 a surface that merely sank a few
    millimetres looked flipped, and nearest-surface tests failed on the thin closed slabs of floors and books)."""
    tree = BVHTree.FromObject(o, bpy.context.evaluated_depsgraph_get())
    bad = area = 0.0
    for p in o.data.polygons:
        n = p.normal
        area += p.area
        hit, normal, _, _ = tree.ray_cast(p.center + n * 0.002, n, 0.5)
        if hit is not None and normal.dot(n) > 0.2:
            bad += p.area
    return bad / area if area else 0.0


# Pass 1: what each part costs at LOD0 now, and the shared ratio for the heavy repeated decor.
parts = sorted(p for p in (d.name for d in models.iterdir() if d.is_dir()) if (models / p / f'{p}.fbx').exists())
lod0 = {}
for part in parts:
    lod0[part] = tris(load(models / part / f'{part}.fbx')[0].data)
heavy = [p for p in parts if lod0[p] >= LOD0_MIN and uses[p] >= REPEATED and p not in KEEP]
fixed = sum(lod0[p] * uses[p] for p in parts if p not in heavy)
ratio = (BUDGET - fixed) / sum(lod0[p] * uses[p] for p in heavy)
assert 0.4 < ratio < 1.0, ratio
report = {'budget': BUDGET, 'ratio_heavy_lod0': round(ratio, 4), 'heavy': heavy, 'parts': {}}

# Pass 2: new LODs, written over the same files.
total = [0, 0, 0]
for part in parts:
    path = models / part / f'{part}.fbx'
    old = load(path)
    before = bounds([old[0]])
    if part in KEEP:
        counts = [tris(old[i].data) for i in range(3)]
        for i in range(3):
            total[i] += counts[i] * uses[part]
        report['parts'][part] = {'uses': uses[part], 'old_lod0': lod0[part], 'lods': counts, 'kept': True}
        continue
    r0 = ratio if part in heavy else 1.0
    new = [decimated(old[0], f'{part}_LOD0_new', r0)]
    base = tris(new[0].data)
    for i, (r, floor) in enumerate(zip(LOD_RATIOS, LOD_FLOOR), 1):
        new.append(decimated(new[0], f'{part}_LOD{i}_new', min(1.0, max(r, floor / max(base, 1)))))
    fell_back = []
    for i, o in enumerate(new):
        share = flipped(o) - flipped(old[i])
        if share > FLIPPED and i > 0:
            bpy.data.objects.remove(o)
            new[i] = decimated(old[i], f'{part}_LOD{i}_old', 1.0)
            fell_back.append((i, round(share, 3)))
        elif share > FLIPPED:
            raise AssertionError((part, 'LOD0 flipped', share))
    for o in old.values():
        bpy.data.objects.remove(o)
    for i, o in enumerate(new):
        o.name = f'{part}_LOD{i}'
        shade(o)
    counts = [tris(o.data) for o in new]
    for i in range(3):
        total[i] += counts[i] * uses[part]
    report['parts'][part] = {'uses': uses[part], 'old_lod0': lod0[part], 'lods': counts}
    if fell_back:
        report['parts'][part]['old_lod_kept'] = fell_back
    if DRY:
        continue
    bpy.ops.object.select_all(action='DESELECT')
    for o in new:
        o.select_set(True)
    bpy.context.view_layer.objects.active = new[0]
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False,
                             use_tspace=True, use_mesh_modifiers=True, mesh_smooth_type='FACE', path_mode='STRIP',
                             embed_textures=False, object_types={'MESH'})
    # Round trip: the new file must import where the old one did, with the same materials.
    back = load(path)
    after = bounds([back[0]])
    drift = max(abs(a - b) for a, b in zip(before, after))
    report['parts'][part]['bounds_drift_m'] = round(drift, 4)
    assert drift < 0.02 * max(1.0, max(b - a for a, b in zip(before[:3], before[3:]))), (part, before, after)
    assert sorted(back) == [0, 1, 2], (part, sorted(back))

report['placed_triangles'] = {'LOD0': total[0], 'LOD1': total[1], 'LOD2': total[2]}
print(json.dumps({k: v for k, v in report.items() if k != 'parts'}, indent=1))
(here / ('relod_r296_dry.json' if DRY else 'relod_r296_report.json')).write_text(
    json.dumps(report, indent=1, ensure_ascii=False), encoding='utf8')
