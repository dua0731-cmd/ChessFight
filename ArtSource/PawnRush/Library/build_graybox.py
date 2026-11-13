"""Pawn Rush library conveyor map: add a graybox of the detailed map to PawnRushLibrary.blend.

    blender --background ArtSource/PawnRush/Library/PawnRushLibrary.blend --python ArtSource/PawnRush/Library/build_graybox.py

The command line saves the file. Inside Blender: Scripting tab -> open this file -> Run Script, then File -> Save.
Running it again deletes GRAYBOX and builds it anew, so do not rerun it after editing the graybox by hand.

Every piece of the detailed map that matters for play becomes one plain box that keeps the piece's position and turn
(its own bounding box, not a world-aligned one). Kit pieces (an empty with a model under it: bookshelves, desks,
banners, piers, the rook goal pieces, the blowers ...) become one box each. Decoration is left out: the vaulted
ceiling (everything above 11 m), lights, and pieces smaller than MIN_SIZE or thinner than MIN_THICK (rivets, chain
links, joints, glass, mouldings). A box that sits completely inside a bigger one is dropped.

The shuttle keeps its motion: the moving boxes hang under their own "GB CONVEYOR_CARRIAGE" empty that plays the same
16 s round trip as the detailed carriage (frames 1..385), and the belt is one deck box the size of Unity's deck
collider. The fixed guides, the start ramp and the take-off strip stay where they are.

Colours (viewport, Solid -> Material): floor light grey, wall dark grey, start green, goal red, moving or hazard
orange, prop blue. The detailed collections are hidden (eye icon) but not removed. Nothing here is exported to Unity.
"""
import bpy
import mathutils

GRAYBOX = "GRAYBOX"
ENV = "PawnRushLibraryEnvironment"
SHUTTLE = "PawnRushLibraryShuttle"
CEILING_Z = 11.0      # pieces whose lowest point is above this are the vault, beams and their trim
MIN_SIZE = 0.6        # longest side under this = decoration (rivets, bolts, small books)
MIN_THICK = 0.03      # thinnest side under this = surface detail (floor joints, panes, lips)
DECK = (3.36, 12.6, 0.28)   # Unity's moving deck collider, Blender X, Y, Z sizes
DECK_CENTRE = (0.0, 0.0, 0.24)  # relative to the carriage

CATEGORIES = [  # first match wins, matched against the lower-cased object (or kit) name
    ("START", (0.25, 0.75, 0.35, 1), ["start_spawn", "start_platform"]),
    ("GOAL", (0.85, 0.25, 0.22, 1), ["goal_capture", "rook_goal", "goal_platform"]),
    ("HAZARD", (0.95, 0.55, 0.15, 1), ["blower", "cannon", "conveyor", "belt", "carriage", "chassis", "idler",
                                        "fan", "takeoff", "trap"]),
    ("FLOOR", (0.78, 0.78, 0.76, 1), ["floor", "platform", "balcony", "landing", "stair", "step", "tread", "ramp",
                                       "deck", "tile", "nosing", "bridge"]),
    ("WALL", (0.42, 0.42, 0.44, 1), ["wall", "pier", "backing", "column", "pillar", "arch", "window", "reveal",
                                      "buttress", "extension", "guide", "rail", "parapet", "baluster", "mullion",
                                      "jamb", "shaft", "post"]),
    ("PROP", (0.45, 0.55, 0.72, 1), [""]),
]


# Trim and ornament: left out whatever their size (they do not change where a pawn can stand or run).
DECOR = ["tracery", "molding", "moulding", "lantern", "chain", "rivet", "bolt", "_cap", " cap", "foot", "bracket",
         "soffit", "plate", "lip", "joint", "nosing", "capital", "crenel", "finial", "collar", "strap", "recess_panel",
         "banner", "crossbar", "keystone", "boss", "relief", "rib", "vault", "beam"]
# Repeated blocks with the same name (wall stones, floor stones, chess tiles) that touch are merged into one box when
# the merged box is mostly solid (filled share at least MERGE_FILL).
MERGE_FILL = 0.6
MERGE_GAP = 0.12


def decor(name):
    low = name.lower()
    return any(k in low for k in DECOR)


def category(name):
    low = name.lower()
    for cat, _colour, keys in CATEGORIES:
        if any(k in low for k in keys):
            return cat
    return "PROP"


def materials():
    mats = {}
    for cat, colour, _keys in CATEGORIES:
        name = "GB_" + cat
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.diffuse_color = colour
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = colour
            bsdf.inputs["Roughness"].default_value = 0.8
        mats[cat] = mat
    return mats


def local_bounds(root, space_inv):
    """Corners of root and everything under it, in the space whose inverse matrix is space_inv."""
    lo = mathutils.Vector((1e9, 1e9, 1e9))
    hi = -lo
    found = False
    stack = [root]
    while stack:
        ob = stack.pop()
        stack.extend(ob.children)
        if ob.type != "MESH" or not ob.data.vertices:
            continue
        m = space_inv @ ob.matrix_world
        for c in ob.bound_box:
            p = m @ mathutils.Vector(c)
            lo = mathutils.Vector(map(min, lo, p))
            hi = mathutils.Vector(map(max, hi, p))
            found = True
    return (lo, hi) if found else None


def world_box(matrix, lo, hi):
    corners = [matrix @ mathutils.Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
    return (mathutils.Vector(map(min, *corners)), mathutils.Vector(map(max, *corners)))


def make_box(name, matrix, lo, hi, mat, collection, parent=None):
    size = hi - lo
    mesh = bpy.data.meshes.new(name)
    hx, hy, hz = size.x / 2, size.y / 2, size.z / 2
    verts = [(a * hx, b * hy, c * hz) for a in (-1, 1) for b in (-1, 1) for c in (-1, 1)]
    faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    ob = bpy.data.objects.new(name, mesh)
    loc, rot, scale = matrix.decompose()
    centre = (lo + hi) / 2
    ob.matrix_world = mathutils.Matrix.LocRotScale(matrix @ centre, rot, None)
    # The box keeps the piece's turn but not its scale: the size is already measured in the parent's space.
    s = mathutils.Matrix.Diagonal((scale.x, scale.y, scale.z, 1))
    mesh.transform(s)
    collection.objects.link(ob)
    if parent is not None:
        ob.parent = parent
        ob.matrix_parent_inverse = parent.matrix_world.inverted()
    return ob


def pieces(col):
    """Top-level pieces of a collection: kit empties (with what hangs under them) and loose meshes."""
    for ob in col.all_objects:
        if ob.parent is not None and ob.parent.name in col.all_objects:
            continue
        if ob.type in ("EMPTY", "MESH"):
            yield ob


def keep(lo, hi, wlo, cat, item_name):
    size = hi - lo
    if wlo.z > CEILING_Z:
        return False
    if decor(item_name):
        return False
    if max(size) < MIN_SIZE:
        return False
    if min(size) < MIN_THICK and cat not in ("START", "GOAL"):
        return False
    return True


def drop_contained(boxes):
    """boxes: list of (wlo, whi, item). Drop items whose world box sits inside a bigger box of the same category."""
    eps = 0.02
    order = sorted(boxes, key=lambda b: -(b[1] - b[0]).length)
    kept = []
    for wlo, whi, item in order:
        inside = any(item["cat"] == k[2]["cat"] and all(k[0][i] - eps <= wlo[i] and whi[i] <= k[1][i] + eps
                                                        for i in range(3)) for k in kept)
        if not inside:
            kept.append((wlo, whi, item))
    return kept


def build_environment(col, mats, out):
    boxes = []
    for ob in pieces(col):
        inv = ob.matrix_world.inverted()
        bounds = local_bounds(ob, inv)
        if bounds is None:
            continue
        lo, hi = bounds
        cat = category(ob.name)
        wlo, whi = world_box(ob.matrix_world, lo, hi)
        if keep(lo, hi, wlo, cat, ob.name):
            boxes.append((wlo, whi, {"ob": ob, "lo": lo, "hi": hi, "cat": cat}))
    kept = drop_contained(merge_repeats(boxes))
    for wlo, whi, it in kept:
        if "merged" in it:
            make_box("GB " + it["merged"], mathutils.Matrix.Identity(4), wlo, whi, mats[it["cat"]], out)
        else:
            make_box("GB " + it["ob"].name, it["ob"].matrix_world, it["lo"], it["hi"], mats[it["cat"]], out)
    return len(boxes), len(kept)


def base_name(name):
    return name.split("|")[0].rstrip("0123456789.").rstrip("_- ")


def axis_aligned(ob):
    rot = ob.matrix_world.to_euler()
    quarter = 1.5707963
    return all(abs(a / quarter - round(a / quarter)) < 0.01 for a in rot)


def merge_repeats(boxes):
    groups = {}
    for b in boxes:
        groups.setdefault((base_name(b[2]["ob"].name), b[2]["cat"]), []).append(b)
    out = []
    for (name, cat), members in groups.items():
        if len(members) < 3 or not all(axis_aligned(m[2]["ob"]) for m in members):
            out.extend(members)
            continue
        # Clusters of boxes that touch (within MERGE_GAP).
        left = list(members)
        while left:
            cluster = [left.pop()]
            grew = True
            while grew:
                grew = False
                for b in list(left):
                    if any(all(b[0][i] <= c[1][i] + MERGE_GAP and c[0][i] <= b[1][i] + MERGE_GAP for i in range(3))
                           for c in cluster):
                        cluster.append(b)
                        left.remove(b)
                        grew = True
            lo = mathutils.Vector([min(c[0][i] for c in cluster) for i in range(3)])
            hi = mathutils.Vector([max(c[1][i] for c in cluster) for i in range(3)])
            filled = sum((c[1] - c[0]).x * (c[1] - c[0]).y * (c[1] - c[0]).z for c in cluster)
            size = hi - lo
            if len(cluster) > 1 and filled >= MERGE_FILL * size.x * size.y * size.z:
                out.append((lo, hi, {"merged": f"{name} x{len(cluster)}", "cat": cat}))
            else:
                out.extend(cluster)
    return out


def build_shuttle(col, mats, out):
    carriage = next(o for o in col.all_objects if o.name.startswith("CONVEYOR_CARRIAGE"))
    scene = bpy.context.scene
    frame = scene.frame_current
    scene.frame_set(1)
    gb_carriage = bpy.data.objects.new("GB CONVEYOR_CARRIAGE", None)
    gb_carriage.empty_display_type = "ARROWS"
    out.objects.link(gb_carriage)
    gb_carriage.matrix_world = carriage.matrix_world.copy()
    src = carriage.animation_data
    gb_carriage.animation_data_create()
    gb_carriage.animation_data.action = src.action
    if hasattr(src, "action_slot") and src.action_slot is not None:
        gb_carriage.animation_data.action_slot = src.action_slot
    bpy.context.view_layer.update()
    count = 0
    # Moving parts: the chassis and the belt ends as boxes, the 64 belt links as one deck box (Unity's collider).
    for ob in carriage.children:
        name = ob.name.lower()
        if not any(k in name for k in ("chassis", "belt_end_drum", "bearing_block")):
            continue
        bounds = local_bounds(ob, ob.matrix_world.inverted())
        if bounds:
            make_box("GB " + ob.name, ob.matrix_world, *bounds, mats["HAZARD"], out, gb_carriage)
            count += 1
    deck_lo = mathutils.Vector(DECK_CENTRE) - mathutils.Vector(DECK) / 2
    deck_hi = mathutils.Vector(DECK_CENTRE) + mathutils.Vector(DECK) / 2
    make_box("GB Conveyor belt deck", carriage.matrix_world, deck_lo, deck_hi, mats["HAZARD"], out, gb_carriage)
    count += 1
    # Fixed parts.
    for ob in pieces(col):
        if ob is carriage:
            continue
        bounds = local_bounds(ob, ob.matrix_world.inverted())
        if bounds is None:
            continue
        lo, hi = bounds
        cat = category(ob.name)
        if "guide" in ob.name.lower():
            cat = "WALL"
        if keep(lo, hi, world_box(ob.matrix_world, lo, hi)[0], cat, ob.name):
            make_box("GB " + ob.name, ob.matrix_world, lo, hi, mats[cat], out)
            count += 1
    scene.frame_set(frame)
    return count


def main():
    old = bpy.data.collections.get(GRAYBOX)
    if old:
        for ob in list(old.all_objects):
            bpy.data.objects.remove(ob, do_unlink=True)
        for child in list(old.children):
            bpy.data.collections.remove(child)
        bpy.data.collections.remove(old)
    root = bpy.data.collections.new(GRAYBOX)
    bpy.context.scene.collection.children.link(root)
    root.color_tag = "COLOR_02"
    env_out = bpy.data.collections.new("GB_Environment")
    shut_out = bpy.data.collections.new("GB_Shuttle")
    root.children.link(env_out)
    root.children.link(shut_out)
    mats = materials()
    found, kept = build_environment(bpy.data.collections[ENV], mats, env_out)
    moving = build_shuttle(bpy.data.collections[SHUTTLE], mats, shut_out)
    view = bpy.context.view_layer.layer_collection
    for name in (ENV, SHUTTLE):
        view.children[name].hide_viewport = True   # eye icon: still evaluated, just not drawn
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "SOLID"
                    space.shading.color_type = "MATERIAL"
    faces = sum(len(o.data.polygons) for o in root.all_objects if o.type == "MESH")
    if bpy.app.background and bpy.data.filepath:
        bpy.ops.wm.save_mainfile(compress=True)   # command-line run: keep the result
    print(f"graybox: environment {kept} boxes (of {found} pieces kept by size), shuttle {moving} boxes, {faces} faces")


main()
