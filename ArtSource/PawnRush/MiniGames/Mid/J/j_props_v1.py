# J Palace Laundry (R509): the user's Tripo FBX of the wash tub, the bubble nozzle, the sluice gate leaf and the sluice
# wheel (Mid/Source/R509_J_TubSluice.fbx, made from AssetRefs/J_tripo_01_tub_sluice.png) split into single assets,
# scaled to their in-game sizes and lowered to a per-asset triangle budget (verdicts in J/README.md).
#   - Tub:    2.2 m across (the game's square floor is 2.05 m, so the picture hardly passes its edge). Origin at the
#             middle of the LID TOP (= the game's floor top, MgKnightPads square y 0); the body runs down past the basin
#             floor (-1.8 m) and hides under it.
#   - Nozzle: Tripo tore the perforated dome apart, so it is built here: a brass flange with rivets and a low dome with
#             dark holes, 0.75 m across, origin at its bottom centre (it sits on the lid).
#   - Leaf:   stretched to the game's team door leaf 3.85 x 0.3 x 4.55 m (as A/E), the lifting bar on top extra;
#             origin at the centre of the leaf (like the game's door box).
#   - Wheel:  1.2 m wheel on its iron bracket, turned so the bracket's wall side is +Y (back) and the wheel faces -Y;
#             origin at the bottom of the back face (it hangs on a wall).
# Front = Blender -Y, up = +Z; exported for Unity like the other mini-game FBX.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python j_props_v1.py -- [--render] [--export]
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
MID = os.path.dirname(HERE)
SRC = os.path.join(MID, "Source", "R509_J_TubSluice.fbx")
TEX = os.path.join(MID, "Materials", "Textures")
OUT = os.path.join(MID, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# name: (verdict, target triangles)
PLAN = {
    "Tub":    ("lower", 7000),
    "Nozzle": ("new (Tripo torn)", 0),
    "Leaf":   ("lower + stretched", 4500),
    "Wheel":  ("lower + turned", 3500),
}
TUB_D = 2.2
LEAF = (3.85, .30, 4.55)
BAR = .10            # the lifting bar takes about this fraction of the Tripo leaf's height
WHEEL_D = 1.2


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def verts(o):
    a = np.empty(len(o.data.vertices) * 3, np.float32)
    o.data.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def bounds(o):
    v = verts(o)
    return Vector(v.min(0)), Vector(v.max(0))


def modifier(o, kind, **kw):
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new(kind, kind)
    for a, b in kw.items():
        setattr(m, a, b)
    with bpy.context.temp_override(object=o, active_object=o):
        bpy.ops.object.modifier_apply(modifier=m.name)


def lower_to(o, target):
    modifier(o, 'WELD', merge_threshold=.0005)
    if tris(o) > target:
        modifier(o, 'DECIMATE', ratio=target / tris(o), use_collapse_triangulate=True)


def which(c):
    """Asset of a face centre in the Tripo FBX (x along the row, z up; see J/README.md)."""
    if c.x < -.28:
        return "Wheel"
    if c.x < .03:
        return "Leaf"
    if c.x < .232 and c.z < 0.:
        return "NozzleTorn"
    return "Tub"


def split(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    # whole loose islands go one way (a torn nozzle shard touching the tub stays with the nozzle)
    seen = np.zeros(len(bm.faces), bool)
    groups = {}
    for f in bm.faces:
        if seen[f.index]:
            continue
        stack, isl = [f], []
        seen[f.index] = True
        while stack:
            g = stack.pop()
            isl.append(g.index)
            for e in g.edges:
                for h in e.link_faces:
                    if not seen[h.index]:
                        seen[h.index] = True
                        stack.append(h)
        c = sum((bm.faces[i].calc_center_median() for i in isl), Vector()) / len(isl)
        groups.setdefault(which(c), set()).update(isl)
    out = {}
    for key, keep in groups.items():
        b2 = bm.copy()
        b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f.index not in keep], context='FACES')
        bmesh.ops.delete(b2, geom=[v for v in b2.verts if not v.link_faces], context='VERTS')
        me = bpy.data.meshes.new("J_" + key)
        b2.to_mesh(me)
        b2.free()
        for m in ob.data.materials:
            me.materials.append(m)
        o = bpy.data.objects.new("J_" + key, me)
        bpy.context.scene.collection.objects.link(o)
        out[key] = o
    bm.free()
    bpy.data.objects.remove(ob)
    return out


def save_atlas(o, stem):
    """Tripo's embedded maps go to Materials/Textures under a stable name (resolution kept)."""
    mat = o.data.materials[0]
    for n in mat.node_tree.nodes:
        if n.type != 'TEX_IMAGE' or not n.image:
            continue
        kind = "BaseColor" if "base" in n.image.name.lower() else "Normal"
        img = n.image.copy()
        img.name = "%s_%s" % (stem, kind)
        if kind == "Normal":
            img.colorspace_settings.name = 'Non-Color'
        img.filepath_raw = os.path.join(TEX, "%s_%s.png" % (stem, kind))
        img.file_format = 'PNG'
        img.save()
        n.image = img
    mat.name = stem
    return mat


def tex_material(name, stem, rough=.5, metal=0.):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(os.path.join(TEX, stem + "_BaseColor.png"), check_existing=True)
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    n = nt.nodes.new("ShaderNodeTexImage")
    n.image = bpy.data.images.load(os.path.join(TEX, stem + "_Normal.png"), check_existing=True)
    n.image.colorspace_settings.name = 'Non-Color'
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(n.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    return m


def flat_material(name, hexcol, rough=.7, metal=0.):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    c = [int(hexcol[i:i + 2], 16) / 255. for i in (1, 3, 5)]
    c = [x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c]
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*c, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    return m


# ------------------------------------------------------------------ fitting
def fit_tub(o):
    lo, hi = bounds(o)
    e = hi - lo
    s = TUB_D / max(e.x, e.y)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, hi.z))       # lid top centre -> origin
    o.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(-c))
    return s


def fit_leaf(o):
    lo, hi = bounds(o)
    e = hi - lo
    kz = LEAF[2] / (e.z * (1. - BAR))
    k = (LEAF[0] / e.x, LEAF[1] / e.y, kz)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    o.data.transform(Matrix.Diagonal((*k, 1.)) @ Matrix.Translation(-c))
    o.data.transform(Matrix.Translation((0., 0., -LEAF[2] / 2)))     # centre of the leaf (bar above it)
    return k


def fit_wheel(o):
    # Tripo stood the wheel at a slant. The bracket's foot (lowest part) sits against the wall, the wheel ring out in
    # front of it: turn about Z so the line from the ring to the foot points +Y (wall behind, wheel facing -Y).
    v = verts(o)
    lo_z, hi_z = v[:, 2].min(), v[:, 2].max()
    ring = v[v[:, 2] > lo_z + .6 * (hi_z - lo_z)][:, :2].mean(0)
    foot = v[v[:, 2] < lo_z + .15 * (hi_z - lo_z)][:, :2].mean(0)
    d = foot - ring
    turn = math.atan2(d[0], d[1])
    o.data.transform(Matrix.Rotation(turn, 4, 'Z'))
    print("WHEEL turned %d deg" % math.degrees(turn))
    lo, hi = bounds(o)
    s = WHEEL_D / (hi.x - lo.x)            # facing -Y, the ring is the widest part in X
    c = Vector(((lo.x + hi.x) / 2, hi.y, lo.z))                     # bottom of the back face
    o.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(-c))
    return s


# ------------------------------------------------------------------ the nozzle, built here
def new_nozzle(brass, hole):
    bm = bmesh.new()
    R, H, F = .3, .12, .375
    # flange: a flat ring disc with a rounded edge
    bmesh.ops.create_cone(bm, cap_ends=True, segments=48, radius1=F, radius2=F - .01, depth=.03,
                          matrix=Matrix.Translation((0, 0, .015)))
    # dome: a squashed half sphere
    geom = bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=12, radius=1.)
    dv = geom["verts"]
    for v in list(dv):
        if v.co.z < -1e-4:
            bm.verts.remove(v)
    dome = [v for v in bm.verts if v.is_valid and v.co.length > .99 and v.co.z >= -1e-4]
    for v in dome:
        v.co = Vector((v.co.x * R, v.co.y * R, .03 + v.co.z * H))
    me = bpy.data.meshes.new("J_Nozzle")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(brass)
    me.materials.append(hole)
    o = bpy.data.objects.new("J_Nozzle", me)
    bpy.context.scene.collection.objects.link(o)
    # holes: small dark discs set into the dome in three rings + one on top; rivets round the flange
    parts = []
    def add(prim, loc, rot, scale, mat_index):
        b = bmesh.new()
        if prim == "disc":
            bmesh.ops.create_cone(b, cap_ends=True, segments=10, radius1=1., radius2=1., depth=1.)
        else:
            bmesh.ops.create_uvsphere(b, u_segments=8, v_segments=5, radius=1.)
        bmesh.ops.transform(b, matrix=Matrix.Translation(loc) @ rot @ Matrix.Diagonal((*scale, 1.)), verts=b.verts)
        m = bpy.data.meshes.new("p")
        b.to_mesh(m)
        b.free()
        for p in m.polygons:
            p.material_index = mat_index
        parts.append(m)
    for ring, n in ((.25, 8), (.55, 12), (.82, 16)):
        el = math.acos(1. - ring) if ring < 1. else math.pi / 2       # elevation band from the top
        for i in range(n):
            a = 2 * math.pi * (i + .5 * (n % 2 == 0 and ring == .55)) / n
            d = Vector((math.sin(el) * math.cos(a), math.sin(el) * math.sin(a), math.cos(el)))
            p = Vector((d.x * R, d.y * R, .03 + d.z * H))
            nrm = Vector((d.x / R, d.y / R, d.z / H)).normalized()
            rot = nrm.to_track_quat('Z', 'Y').to_matrix().to_4x4()
            add("disc", p + nrm * .0006, rot, (.022, .022, .0008), 1)     # a flat dark hole on the surface
    add("disc", Vector((0, 0, .03 + H + .0006)), Matrix.Identity(4), (.024, .024, .0008), 1)
    for i in range(16):
        a = 2 * math.pi * i / 16
        add("sphere", Vector((math.cos(a) * .34, math.sin(a) * .34, .03)), Matrix.Identity(4), (.016, .016, .012), 0)
    bm = bmesh.new()
    bm.from_mesh(me)
    for m in parts:
        bm.from_mesh(m)
        bpy.data.meshes.remove(m)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    return o


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    src = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
    src.data.transform(src.matrix_world)
    src.matrix_world.identity()
    parts = split(src)
    save_atlas(parts["Tub"], "PR_Mid_J_TubSluice")
    torn = parts.pop("NozzleTorn")
    torn_tris = tris(torn)
    bpy.data.objects.remove(torn)
    brass = flat_material("PR_Mid_J_NozzleBrass", "#A8833C", .45, 1.)   # no UVs on the built nozzle: flat matte brass
    hole = flat_material("PR_Mid_J_NozzleHole", "#1C1712", .9)
    parts["Nozzle"] = new_nozzle(brass, hole)
    report = []
    for name, (verdict, target) in PLAN.items():
        o = parts[name]
        before = torn_tris if name == "Nozzle" else tris(o)
        k = {"Tub": fit_tub, "Leaf": fit_leaf, "Wheel": fit_wheel}.get(name, lambda o: 1.)(o)
        if target:
            lower_to(o, target)
        report.append((name, before, k, verdict))
    for name, before, k, verdict in report:
        o = parts[name]
        lo, hi = bounds(o)
        e = hi - lo
        print("ASSET %-7s %-20s tris %6d -> %6d  size %.2f x %.2f x %.2f  z %.2f..%.2f" % (name, verdict, before, tris(o), e.x, e.y, e.z, lo.z, hi.z))
    if "--export" in ARGS:
        os.makedirs(OUT, exist_ok=True)
        for name, o in parts.items():
            bpy.ops.object.select_all(action='DESELECT')
            o.select_set(True)
            bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "J_%s.fbx" % name), use_selection=True,
                                     axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                     mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    x = 0.
    for name in PLAN:
        o = parts[name]
        lo, hi = bounds(o)
        o.location = (x - lo.x, 0, -lo.z)
        x += (hi.x - lo.x) + 1.2
    pawn = flat_material("pawn", "#F2EEE6", .5)
    bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(-1.2, 0, .31))
    bpy.context.object.data.materials.append(pawn)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(-1.2, 0, .84))
    bpy.context.object.data.materials.append(pawn)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(MID, "J_Props_v1.blend"))
    if "--render" in ARGS:
        render(parts)


def render(parts):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1000, 800
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.6, .66, .74, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .8
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-25)))
    bpy.context.object.data.energy = 3.4
    cd = bpy.data.cameras.new("c")
    cam = bpy.data.objects.new("c", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    cd.lens = 50
    cd.clip_end = 500
    pawn = [o for o in sc.objects if o.type == 'MESH' and o.name.startswith(("Cylinder", "Sphere"))]
    for name in PLAN:
        o = parts[name]
        lo, hi = bounds(o)
        for p in pawn:
            p.location.x = o.location.x + lo.x - .6        # a 1 m pawn next to each asset
        c = o.location + (lo + hi) / 2
        d = max(max(hi - lo) * 2.0, 1.6) + .8
        cam.location = c + Vector((d * .5, -d * .8, d * .45))
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_j_prop_%s.png" % name)
        bpy.ops.render.render(write_still=True)


main()
