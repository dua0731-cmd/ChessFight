"""R310: the war room hall pillars become the user's stone column asset. Run in Blender 5.2:
  blender --background --factory-startup --python pillar_r310.py -- [--lod0 2500]

User request 11-03: "내부 기둥 이 에셋으로 교체해서 연결부까지 자연스럽게 해" (the war room pillars). Source:
ArtSource/PawnRush/Endgame/source/stone_column.glb (Tripo, 0.24 x 0.34 x 0.98, square shaft with stone panels, a
base and a bolted capital, 286k triangles, no UV). Writes PR_EG_HallPillar.fbx over the file Unity already uses (same
.meta, GUID and material remap), LOD0/1/2 like relod_r296.py.

How it joins the hall (placement unchanged: manifest base centre at x +-26, floor height -30, every 12 m):
- Scaled evenly so the base and capital are 4.06 m deep: their back faces touch the bookshelf wall's inner face
  (|x| 28, 3 cm into it) and their front edge stops on the edge of the chessboard floor tiles (|x| 24); the shaft
  stands 0.5 m off the wall, like a pilaster.
- 51.72 m tall instead of 48: the capital now carries the ceiling beam (beam underside at 23 - 1.28 = 21.72) instead of
  stopping 3.7 m short of it.
- The asset is short (height 2.9 x its depth), so it is not stretched: it is cut at two stone joints of the shaft
  (z 0.278 and 0.614 of the source, two panels apart) and that two-panel band is stacked 11 times between the original
  base and capital. The cuts lie in the joints, so the seams read as ordinary joints. Each piece is capped before it
  is decimated, so a hairline between pieces shows stone, never a hole.
- The hall floor tiles stop at |x| 24, so each pillar also carries a stone floor strip from the tiles to the wall, one
  bay (12 m) long, so the pillar foot stands on floor instead of a black gap.
- Flat stone faces get box-axis normals (decimated panels otherwise shade like pillows); Unity imports the normals.
- One material, EG_Stone (the asset is all stone); UV0 box projection at 1 = 4 m like every war room part, UV1 for
  lightmaps.
"""
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix
from pathlib import Path

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
LOD0 = int(args[args.index('--lod0') + 1]) if '--lod0' in args else 2500
here = Path(__file__).resolve().parent
SRC = here / 'source' / 'stone_column.glb'
OUT = here.parents[2] / 'Assets/Art/PawnRushMap/Endgame/Resources/PawnRushEndgame/PR_EG_HallPillar/PR_EG_HallPillar.fbx'

CUT_LO, CUT_HI = 0.278, 0.614          # two shaft joints, two panels apart (source units)
DEPTH_HALF = 2.03                      # base / capital half depth after scaling (wall face at 28, pillar at 26)
HEIGHT = 23.0 - 1.28 - (-30.0)         # floor (-30) to the ceiling beam's underside
SHARE = (0.18, 0.28)                   # LOD0 triangles for the base piece and the capital piece; the rest is the shaft
LOD_RATIOS, LOD_FLOOR = (0.6, 0.3), (150, 80)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SRC))
src = [o for o in bpy.data.objects if o.type == 'MESH']
assert len(src) == 1, [o.name for o in src]
src = src[0]
# Into one clean mesh: source transform applied, deep side (source y) turned onto x so it reaches the wall, base on 0.
bm = bmesh.new()
bm.from_mesh(src.data)
bm.transform(Matrix.Rotation(math.radians(90), 4, 'Z') @ src.matrix_world)
zmin = min(v.co.z for v in bm.verts)
bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, -zmin))
src_h = max(v.co.z for v in bm.verts)
depth_half = max(abs(v.co.x) for v in bm.verts if v.co.z < 0.1 * src_h)
s = DEPTH_HALF / depth_half
print('source height %.3f, depth half %.3f, scale %.3f' % (src_h, depth_half, s))


def piece(lo, hi):
    """The source between heights lo and hi (None = open end), holes from the cuts filled."""
    p = bm.copy()
    for z, keep_above in ((lo, True), (hi, False)):
        if z is None:
            continue
        geom = p.verts[:] + p.edges[:] + p.faces[:]
        bmesh.ops.bisect_plane(p, geom=geom, plane_co=(0, 0, z), plane_no=(0, 0, 1),
                               clear_inner=keep_above, clear_outer=not keep_above, dist=1e-6)
    bmesh.ops.holes_fill(p, edges=[e for e in p.edges if e.is_boundary], sides=0)
    bmesh.ops.triangulate(p, faces=p.faces[:])
    return p


def obj(name, b):
    me = bpy.data.meshes.new(name)
    b.to_mesh(me)
    b.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def decimate(o, target):
    m = o.modifiers.new('dec', 'DECIMATE')
    m.ratio = min(1.0, target / max(tris(o), 1))
    with bpy.context.temp_override(object=o, active_object=o):
        bpy.ops.object.modifier_apply(modifier=m.name)
    return o


parts = {'base': obj('base', piece(None, CUT_LO)), 'band': obj('band', piece(CUT_LO, CUT_HI)),
         'cap': obj('cap', piece(CUT_HI, None))}
band_h = (CUT_HI - CUT_LO) * s
fixed_h = (CUT_LO + (src_h - CUT_HI)) * s
count = round((HEIGHT - fixed_h) / band_h)
stretch = (HEIGHT - fixed_h) / (count * band_h)
print('band %.3f m x %d, stretch %.4f' % (band_h, count, stretch))
assert abs(stretch - 1) < 0.03, stretch

t_base, t_cap = LOD0 * SHARE[0], LOD0 * SHARE[1]
decimate(parts['base'], t_base)
decimate(parts['cap'], t_cap)
decimate(parts['band'], (LOD0 - t_base - t_cap) / count)

# Stack: base, count bands, capital, each scaled by s (bands also by `stretch` in height).
out = bmesh.new()


def add(o, src_z, at, zscale):
    """Piece `o` scaled by s (height also by zscale), its source height src_z placed at height `at`."""
    b = bmesh.new()
    b.from_mesh(o.data)
    b.transform(Matrix.Translation((0, 0, at - src_z * s * zscale)) @ Matrix.Diagonal((s, s, s * zscale, 1)))
    me = bpy.data.meshes.new('tmp')
    b.to_mesh(me)
    b.free()
    out.from_mesh(me)
    bpy.data.meshes.remove(me)


add(parts['base'], 0.0, 0.0, 1.0)
z = CUT_LO * s
for i in range(count):
    add(parts['band'], CUT_LO, z, stretch)
    z += band_h * stretch
add(parts['cap'], CUT_HI, z, 1.0)
for o in list(parts.values()) + [src]:
    bpy.data.objects.remove(o)

lod0 = obj('PR_EG_HallPillar_LOD0', out)
lod0.data.materials.append(bpy.data.materials.get('EG_Stone') or bpy.data.materials.new('EG_Stone'))
top = max(v.co.z for v in lod0.data.vertices)
print('LOD0 %d triangles, height %.3f (want %.3f)' % (tris(lod0), top, HEIGHT))
assert abs(top - HEIGHT) < 0.01, top

lods = [lod0]
for i, (r, floor) in enumerate(zip(LOD_RATIOS, LOD_FLOOR), 1):
    o = bpy.data.objects.new(f'PR_EG_HallPillar_LOD{i}', lod0.data.copy())
    bpy.context.scene.collection.objects.link(o)
    lods.append(decimate(o, max(r * tris(lod0), floor)))


def floor_strip(b):
    """The stone strip between the chessboard floor's edge (|x| 24) and the wall (|x| 28): the hall floor tiles stop at
    24, which left a 4 m black gap under every pillar foot. One bay long (local y 0..12, like the wall panel and floor
    tiles the hall places at z + 6), top at the floor (0), 3 m slabs with 2 cm joints like the tiles."""
    for k in range(4):
        y0, y1 = k * 3.0 + 0.01, k * 3.0 + 2.99
        r = bmesh.ops.create_cube(b, size=1.0)
        bmesh.ops.scale(b, vec=(4.0, y1 - y0, 0.3), verts=r['verts'])
        bmesh.ops.translate(b, vec=(0.0, (y0 + y1) / 2, -0.15), verts=r['verts'])


AXES = [Vector(v) for v in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]
SNAP = math.radians(35)
for o in lods:
    me = o.data
    b = bmesh.new()
    b.from_mesh(me)
    floor_strip(b)
    # UV0: box projection, 1 UV = 4 m (war room convention).
    uv = b.loops.layers.uv.new('UVMap')
    for f in b.faces:
        f.material_index = 0
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, c = [(1, 2), (0, 2), (0, 1)][ax]
        for l in f.loops:
            l[uv].uv = (l.vert.co[a] / 4.0, l.vert.co[c] / 4.0)
    b.to_mesh(me)
    b.free()
    # Shading: the decimated stone panels are a few big, slightly tilted triangles; with their own normals they read as
    # pillows. Faces within SNAP of a box axis get that axis as their normal (flat, crisp panels); grooves, chamfers and
    # bolts keep their face normal, so every edge between them stays hard.
    normals = []
    for p in me.polygons:
        n = p.normal
        best = max(AXES, key=lambda a: a.dot(n))
        nn = best if best.angle(n) < SNAP else n
        p.use_smooth = True
        normals.extend([tuple(nn)] * p.loop_total)
    me.normals_split_custom_set(normals)
    # UV1 for lightmaps
    me.uv_layers.new(name='UV1_Lightmap')
    me.uv_layers.active = me.uv_layers['UV1_Lightmap']
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=0.01)
    bpy.ops.object.mode_set(mode='OBJECT')
    me.uv_layers.active = me.uv_layers['UVMap']
    print(o.name, tris(o))

if '--dry' not in args:
    bpy.ops.object.select_all(action='DESELECT')
    for o in lods:
        o.select_set(True)
    bpy.context.view_layer.objects.active = lod0
    bpy.ops.export_scene.fbx(filepath=str(OUT), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False,
                             use_tspace=True, use_mesh_modifiers=True, mesh_smooth_type='FACE', path_mode='STRIP',
                             embed_textures=False, object_types={'MESH'})
    print('wrote', OUT)
