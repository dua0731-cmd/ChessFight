# F Clockwork Ballroom: trim the user's two Tripo sets (R508) to game size.
#   Source/R508_F_Frames.fbx  mirror team-door frame, wall-mirror frame, hanging spotlight
#   Source/R508_F_Props.fbx   music stand, coat stand, drinks side table, potted topiary, harp
# Each set is one mesh with the assets standing in a row along x (z up, front -y); f_inspect_v1.py showed clean
# x gaps between them. Here every asset is cut out, scaled to its game size, decimated to the trimmed target,
# given its origin (see SPEC) and exported alone to Export/F_<Name>.fbx.
#
# Mirrors (user 11-08: AI 3D fills a mirror in and it stops reading as a mirror; "방만 비침"): the Tripo frames are
# open. A flat MIRROR_Surface fills each opening, cut to the opening's own outline (rays from the centre to the
# nearest frame vertex, 2 cm under the frame), in the frame's middle depth. Unity gives it a mirror material and a
# box-projected reflection probe, so the room shows in it but the moving pawns do not.
#
# Textures: the Tripo base colour and normal (4096) are saved at 2048 as PR_Mid_FramesKit_* / PR_Mid_PropsKit_*.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python f_props_v1.py -- [--render] [--export]
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# name: (set, index in the row, size rule, target triangles, origin, mirror)
#   size rule: ("xz", w, h, depth) non-uniform to the game opening; ("z", h) uniform to a height
#   origin: "floor" = floor centre; "back" = back face centre at the bottom (hangs on a wall); "top" = ceiling plate
SPEC = {
    "MirrorDoor":  ("Frames", 0, ("xz", 3.85, 4.55, .32), 6000, "floor", True),   # the game's leaf is 3.85 x 4.55 x 0.3
    "WallMirror":  ("Frames", 1, ("zd", 2.6, .16), 3000, "back", True),
    "Spotlight":   ("Frames", 2, ("z", .95), 2500, "top", False),
    "MusicStand":  ("Props", 0, ("z", 1.15), 2000, "floor", False),
    "CoatStand":   ("Props", 1, ("z", 1.8), 1500, "floor", False),
    "DrinksTable": ("Props", 2, ("z", .98), 2500, "floor", False),
    "Topiary":     ("Props", 3, ("z", 1.6), 3000, "floor", False),
    "Harp":        ("Props", 4, ("z", 1.8), 3000, "floor", False),
}
KIT = {"Frames": "PR_Mid_FramesKit", "Props": "PR_Mid_PropsKit"}


def tris(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)


def select(o):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o


def kit_material(set_name):
    """The Tripo set's material, renamed, with its maps saved at 2048 for Unity."""
    name = KIT[set_name]
    if name in bpy.data.materials: return bpy.data.materials[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    folder = os.path.join(SRC, f"R508_F_{set_name}_tex")
    for src, suffix, cs in (("basecolor.jpeg", "BaseColor", "sRGB"), ("normal.png", "Normal", "Non-Color")):
        img = bpy.data.images.load(os.path.join(folder, src))
        img.colorspace_settings.name = cs
        out = os.path.join(TEX, f"{name}_{suffix}.png")
        if "--export" in ARGS:
            cp = img.copy(); cp.scale(2048, 2048); cp.filepath_raw = out; cp.file_format = "PNG"; cp.save()
        t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = img
        if suffix == "BaseColor":
            m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
        else:
            nm = m.node_tree.nodes.new("ShaderNodeNormalMap")
            m.node_tree.links.new(t.outputs["Color"], nm.inputs["Color"]); m.node_tree.links.new(nm.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = .55
    return m


def mirror_material():
    if "PR_Mid_Mirror" in bpy.data.materials: return bpy.data.materials["PR_Mid_Mirror"]
    m = bpy.data.materials.new("PR_Mid_Mirror"); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (.82, .84, .86, 1); b.inputs["Metallic"].default_value = 1.; b.inputs["Roughness"].default_value = .04
    return m


def load_set(set_name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, f"R508_F_{set_name}.fbx"))
    ob = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
    select(ob); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.data.materials.clear(); ob.data.materials.append(kit_material(set_name))
    return ob


def split_row(ob, n):
    """Separate loose parts, then join them back per x group (the sheet's row), left to right."""
    select(ob); bpy.ops.mesh.separate(type='LOOSE')
    pieces = [o for o in bpy.context.selected_objects]
    spans = []
    for o in pieces:
        xs = [(o.matrix_world @ v.co).x for v in o.data.vertices]
        spans.append([min(xs), max(xs), [o]])
    spans.sort(key=lambda s: s[0])
    merged = []
    for s in spans:
        if merged and s[0] <= merged[-1][1] + 1e-3:
            merged[-1][1] = max(merged[-1][1], s[1]); merged[-1][2] += s[2]
        else:
            merged.append(s)
    assert len(merged) == n, f"{len(merged)} groups, expected {n}"
    out = []
    for _, _, objs in merged:
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1: bpy.ops.object.join()
        out.append(bpy.context.view_layer.objects.active)
    return out


def bounds(o):
    p = np.array([v.co[:] for v in o.data.vertices]); return p.min(0), p.max(0)


def fit(o, rule, origin):
    lo, hi = bounds(o)
    size = hi - lo
    if rule[0] == "xz":
        s = (rule[1] / size[0], rule[3] / size[1], rule[2] / size[2])
    elif rule[0] == "zd":
        k = rule[1] / size[2]; s = (k, rule[2] / size[1], k)
    else:
        k = rule[1] / size[2]; s = (k, k, k)
    c = (lo + hi) / 2
    if origin == "floor": pivot = Vector((c[0], c[1], lo[2]))
    elif origin == "back": pivot = Vector((c[0], hi[1], lo[2]))       # back = +y (front faces -y)
    else: pivot = Vector((c[0], c[1], hi[2]))
    o.data.transform(Matrix.Diagonal((*s, 1)) @ Matrix.Translation(-pivot))
    o.data.update()


def decimate(o, target):
    if tris(o) <= target: return
    select(o)
    md = o.modifiers.new("dec", 'DECIMATE'); md.ratio = target / tris(o); md.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="dec")


def mirror_surface(o, name, cell=.01, under=.02, step=.04):
    """A flat mirror filling the frame's opening: the frame is rasterised seen from the front (x-z, 1 cm cells),
    the empty region around the centre is flood-filled (that is the see-through hole), grown 2 cm so its edge hides
    under the frame, and meshed as one strip of quads (one span per 4 cm row). It sits at the median depth of the
    frame vertices round the hole, so the frame's front faces cover its edge."""
    me0 = o.data
    p = np.array([v.co[:] for v in me0.vertices])
    lo, hi = p.min(0), p.max(0)
    nx, nz = int((hi[0] - lo[0]) / cell) + 3, int((hi[2] - lo[2]) / cell) + 3
    solid = np.zeros((nz, nx), bool)
    gx = lambda x: (x - lo[0]) / cell + 1
    gz = lambda z: (z - lo[2]) / cell + 1
    for poly in me0.polygons:
        vs = [p[i] for i in poly.vertices]
        for k in range(1, len(vs) - 1):
            a, b, c = (np.array([gx(v[0]), gz(v[2])]) for v in (vs[0], vs[k], vs[k + 1]))
            x0, x1 = int(max(min(a[0], b[0], c[0]), 0)), int(min(max(a[0], b[0], c[0]) + 1, nx - 1))
            z0, z1 = int(max(min(a[1], b[1], c[1]), 0)), int(min(max(a[1], b[1], c[1]) + 1, nz - 1))
            X, Z = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(z0, z1 + 1) + .5)
            den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
            if abs(den) < 1e-12: continue
            l1 = ((b[1] - c[1]) * (X - c[0]) + (c[0] - b[0]) * (Z - c[1])) / den
            l2 = ((c[1] - a[1]) * (X - c[0]) + (a[0] - c[0]) * (Z - c[1])) / den
            inside = (l1 >= -.05) & (l2 >= -.05) & (1 - l1 - l2 >= -.05)
            solid[z0:z1 + 1, x0:x1 + 1] |= inside
    # flood fill the hole from the centre of the frame's bounds
    seed = (nz // 2, nx // 2)
    assert not solid[seed], "frame centre is not in the opening"
    hole = np.zeros_like(solid); hole[seed] = True
    while True:
        grow = hole.copy()
        grow[1:] |= hole[:-1]; grow[:-1] |= hole[1:]; grow[:, 1:] |= hole[:, :-1]; grow[:, :-1] |= hole[:, 1:]
        grow &= ~solid
        if (grow == hole).all(): break
        hole = grow
    assert not hole[0].any() and not hole[-1].any() and not hole[:, 0].any() and not hole[:, -1].any(), "opening leaks out"
    rim = hole.copy()
    for _ in range(int(round(under / cell))):
        g = rim.copy(); g[1:] |= rim[:-1]; g[:-1] |= rim[1:]; g[:, 1:] |= rim[:, :-1]; g[:, :-1] |= rim[:, 1:]; rim = g
    # depth: frame vertices near the hole's edge
    edge = rim & ~hole
    ex = (p[:, 0] - lo[0]) / cell + 1; ez = (p[:, 2] - lo[2]) / cell + 1
    ix, iz = np.clip(ex.astype(int), 0, nx - 1), np.clip(ez.astype(int), 0, nz - 1)
    ymid = float(np.median(p[edge[iz, ix], 1])) if edge[iz, ix].any() else float((lo[1] + hi[1]) / 2)
    # rows -> spans -> quads
    rows = []
    zstep = int(round(step / cell))
    for r in range(0, nz, zstep):
        band = rim[r:r + zstep].any(0)
        cols = np.nonzero(band)[0]
        if len(cols): rows.append(((r - 1) * cell + lo[2], (cols[0] - 1) * cell + lo[0], (cols[-1] - 1 + 1) * cell + lo[0]))
    zs = [z for z, _, _ in rows] + [rows[-1][0] + zstep * cell]
    bm = bmesh.new()
    prev = None
    for k, (z, xa, xb) in enumerate(rows):
        top = zs[k + 1]
        a = (bm.verts.new((xa, ymid, z)), bm.verts.new((xb, ymid, z))) if prev is None else prev
        b = (bm.verts.new((xa, ymid, top)), bm.verts.new((xb, ymid, top)))
        if k + 1 < len(rows):            # next row's span starts where this one ends, so the strip stays joined
            nxa, nxb = rows[k + 1][1], rows[k + 1][2]
            b[0].co.x, b[1].co.x = min(xa, nxa), max(xb, nxb)
        bm.faces.new((a[0], a[1], b[1], b[0]))
        prev = b
    for f in bm.faces:                      # face the room (-y)
        if f.normal.y > 0: f.normal_flip()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    print(f"  mirror: hole {hole.sum() * cell * cell:.2f} m2, depth y {ymid:.3f}, {len(rows)} rows")
    me.materials.append(mirror_material())
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            v = me.vertices[me.loops[li].vertex_index].co
            uvl.data[li].uv = ((v.x - lo[0]) / (hi[0] - lo[0]), (v.z - lo[2]) / (hi[2] - lo[2]))
    m = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(m)
    return m


def export(objs, name):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='OFF', path_mode='STRIP', bake_space_transform=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts = {}
    for set_name, n in (("Frames", 3), ("Props", 5)):
        row = split_row(load_set(set_name), n)
        for name, (sn, i, rule, target, origin, mirror) in SPEC.items():
            if sn != set_name: continue
            o = row[i]; o.name = o.data.name = "F_" + name
            raw = tris(o)
            fit(o, rule, origin)
            decimate(o, target)
            for p in o.data.polygons: p.use_smooth = True
            select(o); bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
            lo, hi = bounds(o)
            print(f"PART F_{name}: {raw} -> {tris(o)} tris, size {tuple(round(float(x), 2) for x in hi - lo)}")
            objs = [o]
            if mirror:
                ms = mirror_surface(o, "MIRROR_Surface"); ms.parent = o; objs.append(ms)
            parts[name] = objs
    if "--export" in ARGS:
        for name, objs in parts.items(): export(objs, "F_" + name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "F_Props_v1.blend"))
    if "--render" in ARGS: render(parts)


def render(parts):
    """All eight in a row at game size with a 0.9 m pawn for scale, 3/4 front; the door alone up close."""
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1800, 800
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (.95, .9, .82, 1); bg.inputs[1].default_value = .8
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30))); bpy.context.object.data.energy = 3.
    # a warm "room" behind and around, so the mirrors have something to reflect
    for loc, rot, col in (((6, 4, 3), (math.radians(90), 0, 0), (.75, .55, .35)), ((6, -25, 3), (math.radians(90), 0, 0), (.45, .3, .2)),
                          ((6, 0, 0), (0, 0, 0), (.7, .62, .5))):
        bpy.ops.mesh.primitive_plane_add(size=30, location=loc, rotation=rot)
        m = bpy.data.materials.new("bg"); m.use_nodes = True; m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*col, 1)
        bpy.context.object.data.materials.append(m)
    x = 0.
    order = ["MirrorDoor", "WallMirror", "Spotlight", "MusicStand", "CoatStand", "DrinksTable", "Topiary", "Harp"]
    for name in order:
        o = parts[name][0]
        lo, hi = bounds(o)
        x -= lo[0]
        o.location = (x, 0, 2.6 if name == "Spotlight" else (.6 if name == "WallMirror" else 0))
        x += hi[0] + .5
    pawn = bpy.data.materials.new("pawn"); pawn.use_nodes = True
    pawn.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.95, .93, .9, 1)
    bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.6, location=(x + .2, 0, .3)); bpy.context.object.data.materials.append(pawn)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.2, location=(x + .2, 0, .78)); bpy.context.object.data.materials.append(pawn)
    cd = bpy.data.cameras.new("cam"); cd.lens = 35; cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    tgt = Vector((x / 2, 0, 1.6))
    cam.location = tgt + Vector((-2.5, -11.5, 2.2)); cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(HERE, "render_f_props_row.png"); bpy.ops.render.render(write_still=True)
    o = parts["MirrorDoor"][0]; lo, hi = bounds(o)
    tgt = Vector((o.location.x, 0, 2.3)); cam.location = tgt + Vector((-2.6, -6.5, .6)); cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x, sc.render.resolution_y = 1000, 1000
    sc.render.filepath = os.path.join(HERE, "render_f_props_door.png"); bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
