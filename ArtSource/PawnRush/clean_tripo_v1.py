"""Turn the Tripo GLBs into game parts and build the two parts Tripo got wrong (chain, checkered flag).

blender -b --factory-startup --python clean_tripo_v1.py -- <raw_dir> <out_dir>
Tripo output came back untextured, triangulated and far over budget, so each model is:
decimated to its budget, scaled to its real size, given a bottom-centre pivot (banners: top-centre,
they hang from a wall), and split into the kit's material slots by region.
"""
import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
RAW, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)

PALETTE = {
    "Stone_Cream": (0.91, 0.86, 0.74, 0.0, 0.55),
    "Stone_CreamDark": (0.80, 0.73, 0.60, 0.0, 0.6),
    "Stone_Charcoal": (0.16, 0.16, 0.18, 0.0, 0.5),
    "Tile_White": (0.95, 0.94, 0.90, 0.0, 0.35),
    "Tile_Black": (0.10, 0.10, 0.13, 0.0, 0.35),
    "Banner_Navy": (0.16, 0.27, 0.62, 0.0, 0.7),
    "Banner_Red": (0.75, 0.15, 0.17, 0.0, 0.7),
    "Iron": (0.22, 0.22, 0.24, 0.8, 0.45),
    "Wood": (0.42, 0.28, 0.16, 0.0, 0.7),
    "Gold": (0.93, 0.70, 0.20, 1.0, 0.3),
}


def mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    r, g, b, metal, rough = PALETTE[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (r, g, b, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (r, g, b, 1)
    return m


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def activate(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def load(name):
    bpy.ops.import_scene.gltf(filepath=os.path.join(RAW, name + ".glb"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    o = meshes[0]
    activate(o)
    # bake the glTF parent transform into the mesh
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for e in [x for x in bpy.context.scene.objects if x.type != "MESH"]:
        bpy.data.objects.remove(e)
    return o


def decimate(o, target_tris):
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris <= target_tris:
        return
    activate(o)
    mod = o.modifiers.new("dec", "DECIMATE")
    mod.ratio = target_tris / tris
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="dec")


def fit(o, axis, length, pivot="bottom"):
    """Scale uniformly so the size along axis = length; pivot at bottom-centre or top-centre."""
    dims = o.dimensions
    s = length / dims[axis]
    o.scale = (s, s, s)
    bpy.ops.object.transform_apply(scale=True)
    me = o.data
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    off = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs) if pivot == "bottom" else max(zs)))
    for v in me.vertices:
        v.co -= off
    o.location = (0, 0, 0)


def paint(o, slots, rule):
    """rule(face_centre, face_normal) -> slot index."""
    o.data.materials.clear()
    for s in slots:
        o.data.materials.append(mat(s))
    for p in o.data.polygons:
        p.material_index = rule(p.center, p.normal)


def smooth(o, angle=35):
    activate(o)
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
    except Exception:
        bpy.ops.object.shade_flat()


def tri_count(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def export(o, name):
    o.name = name
    activate(o)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + ".blend"))
    print(f"PART {name}: tris={tri_count(o)} size=({o.dimensions.x:.2f},{o.dimensions.y:.2f},{o.dimensions.z:.2f}) mats={[m.name for m in o.data.materials]}")


# ---------------------------------------------------------------- from Tripo


def arch():
    reset()
    o = load("arch")
    decimate(o, 10000)
    fit(o, 2, 8.0)
    H, W = o.dimensions.z, o.dimensions.x
    top = [v.co for v in o.data.vertices if v.co.z > 0.9 * H]
    cx = sum(c.x for c in top) / len(top)

    def rule(c, n):
        if c.z > 0.70 * H and abs(c.x - cx) < 0.17 * W:
            return 2  # crown
        return 0 if c.z > 0.08 * H else 1

    paint(o, ["Stone_Cream", "Stone_CreamDark", "Gold"], rule)
    smooth(o)
    export(o, "PR_CrownArch")


def knight():
    reset()
    o = load("knight")
    decimate(o, 6000)
    fit(o, 2, 3.0)
    H = o.dimensions.z
    paint(o, ["Stone_Charcoal", "Stone_Cream"], lambda c, n: 1 if c.z < 0.17 * H else 0)
    smooth(o)
    export(o, "PR_KnightStatue")


def banner(src, cloth, name):
    reset()
    o = load(src)
    decimate(o, 600)
    fit(o, 2, 4.0, pivot="top")
    H, W = o.dimensions.z, o.dimensions.x
    # pivot is at the top: z runs from -H to 0
    band = [p for p in o.data.polygons if -0.28 * H < p.center.z < -0.08 * H and abs(p.center.x) < 0.22 * W]
    ys = sorted(p.center.y for p in band) or [0.0]
    med = ys[len(ys) // 2]

    def rule(c, n):
        if c.z > -0.05 * H:
            return 1  # rod
        if -0.28 * H < c.z < -0.08 * H and abs(c.x) < 0.22 * W and abs(c.y - med) > 0.012 * H:
            return 1  # raised crown emblem
        return 0

    paint(o, [cloth, "Gold"], rule)
    smooth(o, 45)
    export(o, name)


# ---------------------------------------------------------------- rebuilt in Blender


def chain():
    """6 chunky iron links (each ~0.6 m), alternating 90 degrees, hanging from a gold wall bracket."""
    reset()
    parts = []
    link_len, link_w, wire = 0.6, 0.38, 0.09
    step = link_len - 2 * wire - 0.02
    for i in range(6):
        bpy.ops.mesh.primitive_torus_add(major_radius=link_w / 2 - wire / 2, minor_radius=wire / 2,
                                         major_segments=12, minor_segments=6)
        t = bpy.context.active_object
        # stretch the ring into an oval along z
        t.rotation_euler = (math.radians(90), 0, math.radians(90 * (i % 2)))
        t.scale = (1, (link_len - wire) / (link_w - wire), 1)
        bpy.ops.object.transform_apply(rotation=True, scale=True)
        t.location = (0, 0, -0.25 - i * step - link_len / 2)
        bpy.ops.object.transform_apply(location=True)
        t.data.materials.append(mat("Iron"))
        parts.append(t)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0.05, -0.12))
    plate = bpy.context.active_object
    plate.scale = (0.4, 0.1, 0.4)
    bpy.ops.object.transform_apply(scale=True)
    plate.data.materials.append(mat("Gold"))
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.05, depth=0.25, location=(0, -0.05, -0.18),
                                        rotation=(math.radians(90), 0, 0))
    pin = bpy.context.active_object
    pin.data.materials.append(mat("Gold"))
    parts += [plate, pin]
    activate(parts[0])
    for p in parts:
        p.select_set(True)
    bpy.ops.object.join()
    o = bpy.context.active_object
    for p in o.data.polygons:
        p.use_smooth = True
    fit(o, 2, o.dimensions.z, pivot="top")
    export(o, "PR_ChainHanging")


def checker_flag():
    """Pole with gold ball + waving 6x4 black/white cloth (per-face materials, no texture)."""
    reset()
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.035, depth=2.0, location=(0, 0, 1.0))
    pole = bpy.context.active_object
    pole.data.materials.append(mat("Wood"))
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=0.07, location=(0, 0, 2.04))
    ball = bpy.context.active_object
    ball.data.materials.append(mat("Gold"))

    me = bpy.data.meshes.new("cloth")
    bm = bmesh.new()
    cols, rows, w, h = 6, 4, 1.0, 0.66
    grid = [[bm.verts.new((0.04 + w * i / cols, 0.06 * math.sin(i / cols * math.pi * 1.5), 1.95 - h * j / rows))
             for j in range(rows + 1)] for i in range(cols + 1)]
    faces = []
    for i in range(cols):
        for j in range(rows):
            f = bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
            f.material_index = (i + j) % 2
            faces.append(f)
    bmesh.ops.solidify(bm, geom=faces, thickness=0.02) if hasattr(bmesh.ops, "solidify") else None
    bm.to_mesh(me)
    bm.free()
    cloth = bpy.data.objects.new("cloth", me)
    bpy.context.scene.collection.objects.link(cloth)
    me.materials.append(mat("Tile_White"))
    me.materials.append(mat("Tile_Black"))
    activate(cloth)
    mod = cloth.modifiers.new("solid", "SOLIDIFY")
    mod.thickness = 0.02
    bpy.ops.object.modifier_apply(modifier="solid")
    # put the pole/ball materials onto the joined object's slot list
    activate(pole)
    for p in (ball, cloth):
        p.select_set(True)
    bpy.ops.object.join()
    o = bpy.context.active_object
    fit(o, 2, o.dimensions.z)
    export(o, "PR_FlagChecker")


def sheet():
    """All six side by side, sunny sky, EEVEE."""
    reset()
    lay = [("PR_CrownArch", (0, 6, 0)), ("PR_KnightStatue", (-8, 2, 0)), ("PR_ChainHanging", (-4.5, 2, 3.2)),
           ("PR_BannerNavy", (5.5, 2, 4.3)), ("PR_BannerRed", (8, 2, 4.3)), ("PR_FlagChecker", (-11, 2, 0))]
    for n, loc in lay:
        bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, n + ".fbx"))
        bpy.context.selected_objects[0].location = loc
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    bpy.context.active_object.data.materials.append(mat("Stone_CreamDark"))
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1600, 800
    w = bpy.data.worlds.new("sky")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.45, 0.68, 0.98, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    sc.world = w
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 2.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(25))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.lens = 30
    sc.collection.objects.link(cam)
    cam.location = (0, -19, 5.5)
    cam.rotation_euler = (Vector((0, 3, 3)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.filepath = os.path.join(OUT, "PR_Tripo_v1_sheet.png")
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    arch()
    knight()
    banner("banner_navy", "Banner_Navy", "PR_BannerNavy")
    banner("banner_red", "Banner_Red", "PR_BannerRed")
    chain()
    checker_flag()
    sheet()
    print("DONE")
