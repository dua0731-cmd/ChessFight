"""Blender (R153 4th): the Tripo horseshoe, stake and cross stake into game parts.
make2.py -- <out_dir> <horseshoe.glb> <stake.glb> <cross.glb>
Horseshoe -> KnightHorseshoe.fbx (Shoe): lying flat, 1 m across, the toe toward Unity +Z, centred, its underside at y 0.
Stake -> BishopStake.fbx (Wood, Band): point at the origin, 1 tall, up; the iron band split off by where it stands out.
Cross -> BishopCross.fbx (Wood, Rope): centred, 1 at its longest; the rope knot split off round the crossing."""
import bpy, bmesh, sys, os, math, mathutils

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir, shoe_path, stake_path, cross_path = argv


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load_one(path):
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.data.materials.clear()
    return o


def decimate(o, faces):
    n = len(o.data.polygons)
    if n > faces:
        m = o.modifiers.new("dec", "DECIMATE")
        m.ratio = faces / n
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier="dec")
    for p in o.data.polygons:
        p.use_smooth = True


def bounds(o):
    vs = [v.co for v in o.data.vertices]
    lo = mathutils.Vector((min(v[k] for v in vs) for k in range(3)))
    hi = mathutils.Vector((max(v[k] for v in vs) for k in range(3)))
    return lo, hi


def xform(o, m):
    o.data.transform(m)
    o.data.update()


def split(o, pick, name_in, name_out):
    """Faces for which pick(center) is true go to a new object name_in; the rest stay as name_out."""
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(o.data)
    for f in bm.faces:
        f.select = pick(f.calc_center_median())
    bmesh.update_edit_mesh(o.data)
    n = sum(1 for f in bm.faces if f.select)
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    parts = [x for x in bpy.context.scene.objects if x.type == "MESH" and x != o]
    new = parts[-1] if parts else None
    o.name = name_out
    o.data.name = name_out
    if new is not None:
        new.name = name_in
        new.data.name = name_in
    print("SPLIT", name_in, n, "faces of", n + len(o.data.polygons))
    return new


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        if o is not None:
            o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    print("EXPORT", os.path.basename(path), [(o.name, len(o.data.polygons)) for o in objs if o is not None])


# ---- horseshoe: front view XZ (toe up +Z), thickness along Y
reset()
o = load_one(shoe_path)
decimate(o, 3000)
lo, hi = bounds(o)
c = (lo + hi) / 2
xform(o, mathutils.Matrix.Translation(-c))
xform(o, mathutils.Matrix.Scale(1.0 / (hi.x - lo.x), 4))
# toe (+Z) -> -Y (Unity +Z), thickness (Y) -> +Z (Unity up)
xform(o, mathutils.Matrix.Rotation(math.radians(90), 4, "X"))
lo, hi = bounds(o)
xform(o, mathutils.Matrix.Translation((0, 0, -lo.z)))
o.name = o.data.name = "Shoe"
lo, hi = bounds(o)
print("SHOE size", [round(x, 3) for x in hi - lo])
export([o], os.path.join(out_dir, "KnightHorseshoe.fbx"))

# ---- stake: point down (-Z), band near the top
reset()
o = load_one(stake_path)
decimate(o, 3000)
lo, hi = bounds(o)
cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
xform(o, mathutils.Matrix.Translation((-cx, -cy, -lo.z)))
xform(o, mathutils.Matrix.Scale(1.0 / (hi.z - lo.z), 4))
# the widest slices (the band) by height
slices = [0.0] * 50
for v in o.data.vertices:
    k = min(49, int(v.co.z * 50))
    slices[k] = max(slices[k], max(abs(v.co.x), abs(v.co.y)))
top_half = sorted(slices[25:45])
median = top_half[len(top_half) // 2]
band = [k for k in range(25, 50) if slices[k] > median * 1.07]
blo, bhi = (min(band) / 50, (max(band) + 1) / 50) if band else (0.62, 0.72)
print("STAKE band", round(blo, 3), round(bhi, 3), "median half-width", round(median, 3), "slices", [round(s, 3) for s in slices[20:50]])
inner = median * 1.03
band_obj = split(o, lambda p: 0.635 <= p.z <= 0.742 and max(abs(p.x), abs(p.y)) > 0.135, "Band", "Wood")
export([o, band_obj], os.path.join(out_dir, "BishopStake.fbx"))
# the half-width at a few heights (to size the knot round it)
for f in (0.8, 0.85, 0.9, 0.95):
    print("STAKE width at", f, round(2 * slices[min(49, int(f * 50))], 3))

# ---- cross stake: a + of sticks, rope knot at the crossing
reset()
o = load_one(cross_path)
decimate(o, 4000)
lo, hi = bounds(o)
c = (lo + hi) / 2
xform(o, mathutils.Matrix.Translation(-c))
size = max(hi - lo)
xform(o, mathutils.Matrix.Scale(1.0 / size, 4))
lo, hi = bounds(o)
arms = [v.co.z for v in o.data.vertices if abs(v.co.x) > 0.3]
zbar = sum(arms) / max(1, len(arms))
knot = mathutils.Vector((0, 0, zbar))
print("CROSS bar z", round(zbar, 3), "size", [round(x, 3) for x in hi - lo])
def stick(p):
    # the horizontal bar along x at zbar, and the two upright sticks side by side near x = +-0.03
    return abs(p.y) < 0.042 and (abs(p.z - zbar) < 0.042 or abs(abs(p.x) - 0.03) < 0.04)
rope = split(o, lambda p: (p - knot).length < 0.19 and not stick(p), "Rope", "Wood")
export([o, rope], os.path.join(out_dir, "BishopCross.fbx"))
