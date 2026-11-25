# Shared helpers for the first-run tutorial map (R834, palace drill yard concept,
# Docs/Player/ConceptArt/R834_Tutorial). Imported by tu_kit_split_v1.py and tu_map_v1.py.
#
# Frame: Blender (x, y, z) = Unity (x, z, y). Unity's tutorial numbers (LabLayout / TutorialLayout) are used as they are,
# with Unity z written as Blender y.
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
OUT = os.path.join(HERE, "Export")
RENDERS = os.path.join(HERE, "Renders")
TEX = os.path.join(HERE, "Materials", "Textures")


# ------------------------------------------------------------------ mesh basics
def coords(o):
    a = np.empty(len(o.data.vertices) * 3, np.float32)
    o.data.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def xform(o, M):
    o.data.transform(M)
    o.data.update()


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def link(o, coll=None):
    (coll or bpy.context.scene.collection).objects.link(o)
    return o


def apply_mod(o, m):
    with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o], selected_editable_objects=[o]):
        bpy.ops.object.modifier_apply(modifier=m.name)


def join(objs, name):
    objs = [o for o in objs if o]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    with bpy.context.temp_override(active_object=objs[0], selected_editable_objects=objs, selected_objects=objs):
        bpy.ops.object.join()
    o = objs[0]
    o.name = o.data.name = name
    return o


def bounds(o):
    p = coords(o)
    return p.min(0), p.max(0)


# ------------------------------------------------------------------ Tripo import and split
def load_glb(name):
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, name + ".glb"))
    ob = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world)
    ob.parent = None
    ob.matrix_world.identity()
    for o in list(bpy.context.selected_objects):
        if o is not ob and o.type != 'MESH':
            bpy.data.objects.remove(o)
    ob.name = name
    return ob


def islands(bm):
    bm.faces.ensure_lookup_table()
    seen = np.zeros(len(bm.faces), bool)
    out = []
    for f in bm.faces:
        if seen[f.index]:
            continue
        st = [f]; seen[f.index] = True; cur = []
        while st:
            g = st.pop(); cur.append(g.index)
            for e in g.edges:
                for h in e.link_faces:
                    if not seen[h.index]:
                        seen[h.index] = True; st.append(h)
        out.append(cur)
    return out


def split_by_footprint(ob, margin=.004, dims=2, rule=None):
    """Tripo sheets arrive as one mesh of many open shells. Islands whose ground footprints (x/y boxes) touch are one
    asset; returns a list of objects ordered left to right (sheet order)."""
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
    isl = islands(bm)
    box = []
    for idx in isl:
        vs = np.array([v.co[:] for i in idx for v in bm.faces[i].verts])
        box.append((vs.min(0), vs.max(0)))
    parent = list(range(len(isl)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]; a = parent[a]
        return a
    for i in range(len(isl)):
        for j in range(i + 1, len(isl)):
            a, b = box[i], box[j]
            if all(a[0][k] - margin <= b[1][k] and b[0][k] - margin <= a[1][k] for k in range(dims)):
                parent[find(i)] = find(j)
    groups = {}
    for i, idx in enumerate(isl):
        groups.setdefault(find(i), []).extend(idx)
    out = []
    for key, idx in groups.items():
        keep = set(idx)
        b2 = bm.copy(); b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f.index not in keep], context='FACES')
        bmesh.ops.delete(b2, geom=[v for v in b2.verts if not v.link_faces], context='VERTS')
        me = bpy.data.meshes.new("part"); b2.to_mesh(me); b2.free()
        for m in ob.data.materials:
            me.materials.append(m)
        out.append(link(bpy.data.objects.new("part", me)))
    bm.free()
    bpy.data.objects.remove(ob)
    out.sort(key=lambda o: coords(o)[:, 0].mean())
    return out


# ------------------------------------------------------------------ inspection
def inspect(o):
    """Counts that show a broken-looking asset. Open edges on the very bottom (resting on the ground) are fine."""
    bm = bmesh.new(); bm.from_mesh(o.data)
    z0 = min(v.co.z for v in bm.verts); z1 = max(v.co.z for v in bm.verts)
    h = max(z1 - z0, 1e-6)
    bnd = [e for e in bm.edges if e.is_boundary]
    vis = [e for e in bnd if min(v.co.z for v in e.verts) > z0 + .02 * h]
    nonman = sum(1 for e in bm.edges if not e.is_manifold and not e.is_boundary)
    degen = sum(1 for f in bm.faces if f.calc_area() < 1e-10)
    # spikes: vertices far from all neighbours compared to the mesh's typical edge length
    lens = np.array([e.calc_length() for e in bm.edges]) if bm.edges else np.zeros(1)
    med = float(np.median(lens)) if len(lens) else 0.
    spikes = sum(1 for e in bm.edges if e.calc_length() > 40 * med) if med > 0 else 0
    r = dict(faces=len(bm.faces), boundary=len(bnd), visible_open=len(vis), nonmanifold=nonman, degenerate=degen, spikes=spikes)
    bm.free()
    return r


def repair(o, weld=None):
    """Weld coincident shells, drop degenerate bits and dust, make normals consistent, close small visible holes."""
    lo, hi = bounds(o); size = float(np.max(hi - lo))
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=weld if weld is not None else size * 4e-4)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=size * 1e-5)
    # dust: tiny loose islands (a few faces floating apart) read as broken specks
    bm.faces.ensure_lookup_table()
    for idx in islands(bm):
        if len(idx) < 12:
            bmesh.ops.delete(bm, geom=[bm.faces[i] for i in idx], context='FACES')
        bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    # small holes away from the bottom: fill (up to 24 sides)
    z0 = min(v.co.z for v in bm.verts)
    holes = [e for e in bm.edges if e.is_boundary and min(v.co.z for v in e.verts) > z0 + size * .01]
    if holes:
        bmesh.ops.holes_fill(bm, edges=holes, sides=24)
    bm.to_mesh(o.data); bm.free()
    o.data.update()


# ------------------------------------------------------------------ fitting
def ground_center(o):
    lo, hi = bounds(o)
    xform(o, Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))))


def straighten(o):
    """Turn about z so the footprint's long axis is x (PCA), keeping the object's front facing -y."""
    p = coords(o)
    xy = p[:, :2] - p[:, :2].mean(0)
    w, v = np.linalg.eigh(np.cov(xy.T))
    ax = v[:, np.argmax(w)]
    ang = math.atan2(ax[1], ax[0])
    if ang > math.pi / 2: ang -= math.pi
    if ang < -math.pi / 2: ang += math.pi
    xform(o, Matrix.Rotation(-ang, 4, 'Z'))
    ground_center(o)


def fit_box(o, size):
    """Non-uniform scale to an exact (x, y, z) box, centred in x/y, ground at z 0."""
    ground_center(o)
    lo, hi = bounds(o); d = hi - lo
    xform(o, Matrix.Diagonal((size[0] / d[0], size[1] / d[1], size[2] / d[2], 1.)))


def fit_height(o, h):
    ground_center(o)
    lo, hi = bounds(o)
    xform(o, Matrix.Scale(h / (hi[2] - lo[2]), 4))


def decimate(o, target):
    if tris(o) > target:
        m = o.modifiers.new("dec", 'DECIMATE'); m.ratio = target / tris(o); m.use_collapse_triangulate = True
        apply_mod(o, m)
    return tris(o)


# ------------------------------------------------------------------ materials for new geometry
def principled(name, rgba, rough=.5, metal=0.):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs["Base Color"].default_value = rgba
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    m.diffuse_color = rgba
    return m


def image_material(name, base_png, normal_png=None, rough=.6, metal=0.):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(base_png, check_existing=True)
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    if normal_png and os.path.exists(normal_png):
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(normal_png, check_existing=True)
        tn.image.colorspace_settings.name = 'Non-Color'
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    return m


def mark_backfaces(on=True):
    """Inspection look: every back face (a hole seen from outside, a flipped normal) renders bright magenta."""
    for m in bpy.data.materials:
        if not m.use_nodes: continue
        nt = m.node_tree
        out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None)
        if out is None: continue
        tag = nt.nodes.get("_bf_mix")
        if on and tag is None:
            src = out.inputs["Surface"].links[0].from_socket if out.inputs["Surface"].links else None
            if src is None: continue
            geo = nt.nodes.new("ShaderNodeNewGeometry")
            em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (1, 0, 1, 1); em.inputs["Strength"].default_value = 1.
            mix = nt.nodes.new("ShaderNodeMixShader"); mix.name = "_bf_mix"
            nt.links.new(geo.outputs["Backfacing"], mix.inputs[0])
            nt.links.new(src, mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
            nt.links.new(mix.outputs[0], out.inputs["Surface"])
            m.use_backface_culling = False


# ------------------------------------------------------------------ rendering
def setup_render(w=1600, h=900, engine='EEVEE'):
    sc = bpy.context.scene
    try:
        sc.render.engine = 'BLENDER_EEVEE_NEXT' if engine == 'EEVEE' else engine
    except TypeError:
        sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x = w; sc.render.resolution_y = h
    sc.render.film_transparent = False
    if sc.world is None:
        sc.world = bpy.data.worlds.new("world")
    sc.world.use_nodes = True
    bg = next(n for n in sc.world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs["Color"].default_value = (.62, .72, .85, 1); bg.inputs["Strength"].default_value = .55
    sc.view_settings.view_transform = 'Standard' if 'Standard' in [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items] else sc.view_settings.view_transform
    if "sun" not in bpy.data.objects:
        sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN'))
        sun.data.energy = 4.5; sun.data.angle = math.radians(8)
        sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
        link(sun)
    return sc


def use_cycles(samples=48):
    """Map views: Cycles, so every object casts a shadow (EEVEE runs out of shadow pages on the whole map)."""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 4
    try:
        sc.cycles.device = 'GPU'
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for t in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI'):
            try:
                prefs.compute_device_type = t; prefs.get_devices()
                if any(d.type == t for d in prefs.devices):
                    for d in prefs.devices: d.use = True
                    break
            except Exception:
                continue
        else:
            sc.cycles.device = 'CPU'
    except Exception:
        sc.cycles.device = 'CPU'
    bg = next(n for n in sc.world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs["Color"].default_value = (.42, .62, .95, 1); bg.inputs["Strength"].default_value = .9
    sun = bpy.data.objects["sun"]; sun.data.energy = 4.
    return sc


def camera(name="cam"):
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = link(bpy.data.objects.new(name, bpy.data.cameras.new(name)))
    bpy.context.scene.camera = cam
    return cam


def look_at(cam, loc, target, lens=35, ortho=None):
    cam.location = loc
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_end = 2000; cam.data.clip_start = .05
    if ortho:
        cam.data.type = 'ORTHO'; cam.data.ortho_scale = ortho
    else:
        cam.data.type = 'PERSP'; cam.data.lens = lens


def render(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
