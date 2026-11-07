# C · 해자 도개교 v2 → Unity (R502, user 11-07 "유니티적용").
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_drawbridge_v2_export.py
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I c_install.py
#
# Builds the layout of c_drawbridge_v2.py (the user's gate, towers, winch, garden wall and kit, the stone block
# module), then for the game:
#   - each user asset is decimated once to its budget (Materials/README.md, raised by the user 11-07);
#   - what Unity makes in code is left out: the water (PR_MoatWater shader), the banners (swaying cloth), the lantern
#     glass and light (SOCKET_Lantern empties, MiniGameFixtures), the rope and the knots (the game's own);
#   - materials get the names MiniGameModels looks up in Resources/PawnRushMiniGames/Materials;
#   - textured generated parts get box UVs.
# Writes Export/C_Drawbridge_EnvV2.fbx (section frame: entrance at 0, exit at y 36) and Export/C_Drawbridge_DeckV2.fbx
# (the hinge's frame: the deck lies along -y from the hinge at 0, top at z 0), the textures into Materials/Textures,
# and C_Drawbridge_v2_export.blend.
import os, sys, math
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c_drawbridge_v2 as v2  # noqa: E402

EXPORT = os.path.join(HERE, "Export")
TEX = os.path.join(HERE, "Materials", "Textures")
os.makedirs(EXPORT, exist_ok=True)

BUDGET = {"PR_Early_RoyalGate": 80000, "tower": 50000, "winch": 20000, "winch drum": 5000, "railing": 15000,
          "KIT_Pylon": 12000, "KIT_Parapet": 12000}
# template object -> the Unity material name of its (textured) material
TEXTURED = {"PR_Early_RoyalGate": "PR_Early_C_Gate", "tower": "PR_Early_C_Tower", "winch": "PR_Early_C_Winch",
            "winch drum": "PR_Early_C_Winch",
            "railing": "PR_Early_C_Railing", "KIT_Pylon": "PR_Early_C_Kit", "KIT_Parapet": "PR_Early_C_Kit"}
# generated materials (Blender name prefix) -> Unity material
RENAME = {"iron": "PR_Early_DarkIron", "wood": "PR_Early_C_Wood", "gold (asset)": "PR_Early_C_Gold",
          "tick (functional gold)": "PR_Early_C_Tick", "PR_Early_TowerStone": "PR_Early_C_TowerStone",
          "leaf": "PR_Early_Leaf", "bloom": "PR_Early_Petal", "cypress": "PR_Early_C_Cypress",
          "rope": "PR_Early_C_Rope", "trunk": "PR_Early_C_Trunk", "buoy": "PR_Early_C_Buoy"}
DROP = ("water", "team banner", "laurel banner", "outer banner", "lantern glass", "rope", "knot", "pawn", "deck chain", "chain plate")


def tris(me):
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def decimate(template, target):
    """Collapses the template's mesh to about `target` triangles; every placement shares it."""
    me = template.data
    n = tris(me)
    if n <= target:
        return
    tmp = bpy.data.objects.new("tmp", me)
    bpy.context.scene.collection.objects.link(tmp)
    m = tmp.modifiers.new("dec", "DECIMATE")
    m.ratio = target / n
    m.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    new = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    users = [o for o in bpy.data.objects if o.data == me]
    for o in users:
        o.data = new
    new.name = me.name
    print("DECIMATE %-20s %7d -> %6d triangles (%d placements)" % (template.name, n, tris(new), len(users) - 1))


def save_maps(template, unity_name):
    """The template material's own base colour and normal maps -> Materials/Textures/<unity_name>_*.png."""
    mat = template.data.materials[0]
    out = {}
    for node in mat.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        to = [l.to_socket.name for o in node.outputs for l in o.links]
        to_nodes = [l.to_node.type for o in node.outputs for l in o.links]
        kind = "Normal" if "NORMAL_MAP" in to_nodes else "BaseColor" if "Base Color" in to else None
        if kind is None or kind in out:
            continue
        img = node.image
        path = os.path.join(TEX, "%s_%s.png" % (unity_name, kind))
        if img.size[0] == 0:
            print("MAP missing pixels", img.name)
            continue
        copy = img.copy()
        copy.filepath_raw = path
        copy.file_format = "PNG"
        copy.save()
        bpy.data.images.remove(copy)
        out[kind] = path
        print("MAP %s %s %s" % (unity_name, kind, img.size[:]))
    return out


def box_uv(ob, tile=2.):
    """Planar UVs per face from world position (tile metres a repeat), for generated parts with a textured material."""
    me = ob.data
    if me.uv_layers:
        return
    uv = me.uv_layers.new(name="UVMap")
    mw = ob.matrix_world
    n3 = mw.to_3x3()
    for p in me.polygons:
        n = (n3 @ p.normal).normalized()
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in p.loop_indices:
            c = mw @ me.vertices[me.loops[li].vertex_index].co
            u, v = [(c.y, c.z), (c.x, c.z), (c.x, c.y)][ax]
            uv.data[li].uv = (u / tile, v / tile)


def drum_seams(ride):
    """The drum was cut out of the user's winch face by face (c_winch_split.py), so its two ends are ragged and one
    could see through between the drum and the cheeks' iron rings (user 11-07, "깨진 부분 매워"). In the drum's frame
    (it turns with it): a dark core just inside the drum's surface the whole length and a little into the cheeks,
    and a dark iron collar at each end over the seam."""
    t = bpy.data.objects["winch drum"]
    R = t["radius"] * v2.WINCH_K
    L = t["length"] * v2.WINCH_K
    bm = bmesh.new()
    # the core: closed, axis along x
    bmesh.ops.create_cone(bm, cap_ends=True, segments=48, radius1=R * .96, radius2=R * .96, depth=L + .14,
                          matrix=Matrix.Rotation(math.radians(90), 4, "Y"))
    # the collars: a short thick tube at each end, from inside the drum to past the cheeks' inner rim
    for sx in (-1, 1):
        x = sx * (L / 2 - .01)
        for r_out, r_in in ((R * 1.2, R * .9),):
            outer = bmesh.ops.create_cone(bm, cap_ends=False, segments=48, radius1=r_out, radius2=r_out, depth=.09,
                                          matrix=Matrix.Translation((x, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Y"))["verts"]
            inner = bmesh.ops.create_cone(bm, cap_ends=False, segments=48, radius1=r_in, radius2=r_in, depth=.09,
                                          matrix=Matrix.Translation((x, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Y"))["verts"]
            bmesh.ops.reverse_faces(bm, faces=list({f for v in inner for f in v.link_faces}))
            for ring_o, ring_i in ((sorted([v for v in outer if v.co.x > x], key=lambda v: math.atan2(v.co.z, v.co.y)),
                                    sorted([v for v in inner if v.co.x > x], key=lambda v: math.atan2(v.co.z, v.co.y))),
                                   (sorted([v for v in outer if v.co.x < x], key=lambda v: math.atan2(v.co.z, v.co.y)),
                                    sorted([v for v in inner if v.co.x < x], key=lambda v: math.atan2(v.co.z, v.co.y)))):
                n = len(ring_o)
                for k in range(n):
                    try:
                        bm.faces.new((ring_o[k], ring_o[(k + 1) % n], ring_i[(k + 1) % n], ring_i[k]))
                    except ValueError:
                        pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("drum seams")
    bm.to_mesh(me)
    bm.free()
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    mat = bpy.data.materials.get("PR_Early_DarkIron") or bpy.data.materials.new("PR_Early_DarkIron")
    me.materials.append(mat)
    ob = bpy.data.objects.new("drum seams", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def rope_coil_on_drum():
    """Rope wound on the winch's drum (user 11-07, "로프를 다시 만들어"): a helix of rope round the drum's axis (Blender x),
    seven turns over the drum's length, in the drum's frame so it turns with the drum."""
    t = bpy.data.objects["winch drum"]
    R = t["radius"] * v2.WINCH_K + .06
    L = t["length"] * v2.WINCH_K * .86
    turns, rope_r, steps, tube = 7, .06, 7 * 24, 8
    bm = bmesh.new()
    rows = []
    for i in range(steps + 1):
        u = i / steps
        a = 2 * math.pi * turns * u
        c = Vector((-L / 2 + L * u, R * math.cos(a), R * math.sin(a)))
        radial = Vector((0, math.cos(a), math.sin(a)))
        tang = Vector((L / (2 * math.pi * turns), -R * math.sin(a), R * math.cos(a))).normalized()
        side = tang.cross(radial).normalized()
        rows.append([bm.verts.new(c + (radial * math.cos(2 * math.pi * j / tube) + side * math.sin(2 * math.pi * j / tube)) * rope_r)
                     for j in range(tube)])
    for i in range(steps):
        for j in range(tube):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % tube], rows[i + 1][(j + 1) % tube], rows[i + 1][j]))
    me = bpy.data.meshes.new("drum rope")
    bm.to_mesh(me)
    bm.free()
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    mat = bpy.data.materials.get("PR_Early_C_Rope") or bpy.data.materials.new("PR_Early_C_Rope")
    me.materials.append(mat)
    ob = bpy.data.objects.new("drum rope", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def main():
    sc = v2.build()
    # the templates (hidden sources every placement shares)
    templates = {o.name: o for o in bpy.data.objects if o.name in BUDGET}
    for name, target in BUDGET.items():
        if name in templates:
            decimate(templates[name], target)
    maps = {}
    for name, unity in TEXTURED.items():
        t = templates.get(name)
        if t is None:
            continue
        if unity not in maps:
            maps[unity] = save_maps(t, unity)
        t.data.materials[0].name = unity
    # generated materials -> Unity names; copies made per part ("trunk.003") all become the one material
    canon = {}
    for o in bpy.data.objects:
        if o.type not in ("MESH", "CURVE"):
            continue
        for i, m in enumerate(o.data.materials):
            if m is None:
                continue
            base = m.name.split(".")[0]
            if base not in RENAME:
                continue
            if base not in canon:
                canon[base] = m
                m.name = RENAME[base]
            o.data.materials[i] = canon[base]
    # lantern glass -> sockets; drop what Unity draws itself (water, banners, glass, lights, rope, knots)
    for o in list(bpy.data.objects):
        if o.name.startswith("lantern glass"):
            s = bpy.data.objects.new("SOCKET_Lantern", None)
            s.location = o.matrix_world.translation
            sc.collection.objects.link(s)
    for o in list(bpy.data.objects):
        base = o.name.split(".")[0]
        if o.type == "LIGHT" or base in DROP or any(base.startswith(d) for d in ("team banner", "laurel banner", "outer banner", "lantern glass")):
            bpy.data.objects.remove(o)
    # every placement takes its mesh's own material slots (an object-level slot breaks the FBX's material index)
    for o in bpy.data.objects:
        if o.type == "MESH":
            for slot in o.material_slots:
                slot.link = "DATA"
    # textured generated parts need UVs
    for o in bpy.data.objects:
        if o.type == "MESH" and o.data.materials and o.data.materials[0] and o.data.materials[0].name == "PR_Early_C_Wood":
            box_uv(o)

    # the decks: white's (lowered) is the one model; both are taken out of the section
    decks = [o for o in bpy.data.objects if o.name.startswith("deck ")]
    deck_parts = {d.name: [c for c in d.children] for d in decks}
    for o in bpy.data.objects:
        o.select_set(False)

    def export(objs, path):
        for o in bpy.data.objects:
            o.select_set(False)
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
                                 apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z",
                                 axis_up="Y", bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="OFF",
                                 add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
        n = sum(tris(o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()) for o in objs if o.type == "MESH")
        print("EXPORT %s: %d objects, %d triangles" % (os.path.basename(path), len(objs), n))

    white = [d for d in decks if d.name.endswith("white")][0]
    white.location = (0, 0, 0)
    white.rotation_euler = (0, 0, 0)
    for o in deck_parts[white.name]:
        box_uv(o)
    bpy.context.view_layer.update()
    export([white] + deck_parts[white.name], os.path.join(EXPORT, "C_Drawbridge_DeckV2.fbx"))
    gone = [c.name for d in decks for c in deck_parts[d.name]] + [d.name for d in decks]
    for name in gone:
        o = bpy.data.objects.get(name)
        if o is not None:
            bpy.data.objects.remove(o)

    # the winch's drum: one model in the game Drum's frame (origin on the axis, axis along x), turned by the game
    rides = [o for o in bpy.data.objects if o.name.startswith("winch drum ride")]
    if rides:
        r = rides[0]
        r.location = (0, 0, 0)
        coil = rope_coil_on_drum()
        patch = drum_seams(r)
        bpy.context.view_layer.update()
        export([r, coil, patch], os.path.join(EXPORT, "C_Drawbridge_DrumV2.fbx"))
        bpy.data.objects.remove(coil)
        bpy.data.objects.remove(patch)
        for o in rides:
            bpy.data.objects.remove(o)
    env = [o for o in bpy.data.objects if o.type in ("MESH", "EMPTY", "CURVE") and not o.hide_get() and not o.hide_render]
    in_scene = set(bpy.context.view_layer.objects)
    env = [o for o in env if o.type != "CURVE" and o in in_scene]
    export(env, os.path.join(EXPORT, "C_Drawbridge_EnvV2.fbx"))
    used = sorted({m.name for o in env if o.type == "MESH" for m in o.data.materials if m})
    print("MATERIALS", used)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "C_Drawbridge_v2_export.blend"))


main()
