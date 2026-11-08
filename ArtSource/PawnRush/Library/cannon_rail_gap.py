"""Pawn Rush library (작전 회의실): no balustrade in front of the two air cannons (R421).

    blender --background --factory-startup --python cannon_rail_gap.py                       (writes the FBX)
    blender --background --factory-startup --python cannon_rail_gap.py -- --preview D:/check (renders, writes nothing)

승규 님 (11-05, a shot of the left cannon's muzzle behind the teal railing): "대포앞에 난간 지워주고". The balcony edge
railings (GAME_Railing_sector_-1_2 at x -8, GAME_Railing_sector_1_3 at x +8) are posts every 2.33 m with rails and
balusters between them, all loose pieces of one mesh. In front of each cannon (A at y -4, B at y +5; the cannon body
is 2.9 m wide) the railing opens 3.1 m, centred on the cannon:
  - the post nearest each end of the opening moves to that end, so the railing still finishes on a post
    (left: the post at -3.17, right in front of the muzzle, goes to -2.45; right: 3.83 -> 3.45 and 6.17 -> 6.55),
  - every rail, baluster and post between the two is deleted,
  - the rails of the bay beyond a moved post are shortened to meet it (scaled along y from their far post) and a
    baluster left too close to it goes.
The model collides as drawn (Course01v4Builder.LibraryCollision), so the opening is open to walk through too.
Running it again changes nothing (the end posts are already there and nothing is left between them).
"""
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
FBX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary",
                                    "PawnRushLibraryEnvironment.fbx"))
# (railing object, cannon's y): the cannons BLOWER_A_left (x -10.84, y -4) and BLOWER_B_right (x 10.84, y 5)
GAPS = (("GAME_Railing_sector_-1_2", -4.0), ("GAME_Railing_sector_1_3", 5.0))
HALF = 1.55            # half the opening
POST_TOL = .2          # a piece within this of a post's y belongs to the post
KEEP = .15             # a post this close to an end already stands there


def islands(bm):
    seen, out = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        stack, isl = [v], []
        seen.add(v)
        while stack:
            a = stack.pop()
            isl.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b not in seen:
                    seen.add(b)
                    stack.append(b)
        ys = [p.co.y for p in isl]
        zs = [p.co.z for p in isl]
        out.append(dict(verts=isl, y0=min(ys), y1=max(ys), yc=(min(ys) + max(ys)) / 2, z0=min(zs), z1=max(zs)))
    return out


def open_gap(ob, yc):
    """Works in world space (Blender's z up, the hall's y along the course), back to the object's own at the end."""
    mw = ob.matrix_world.copy()
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.transform(mw)
    isl = islands(bm)
    # posts: the tall shafts (from near the floor to the cap)
    posts = sorted({round(i["yc"], 3) for i in isl if i["y1"] - i["y0"] < .4 and i["z0"] < .2 and i["z1"] > 1.1})
    g = (yc - HALF, yc + HALF)
    moved, cut, scaled = 0, 0, 0
    kill = set()
    for side, edge in ((-1, g[0]), (1, g[1])):
        p = min(posts, key=lambda q: abs(q - edge))
        if abs(p - edge) < KEEP:
            continue
        shift = edge - p
        group = [i for i in isl if i["y1"] - i["y0"] < .4 and abs(i["yc"] - p) < POST_TOL]
        for i in group:
            for v in i["verts"]:
                v.co.y += shift
            i["moved"] = True
            moved += 1
        # the bay beyond: rails from p to the next post outward, scaled to start at the edge
        far = [q for q in posts if (q - p) * side > .5]
        if not far:
            continue
        q = min(far, key=lambda q: abs(q - p))
        for i in isl:
            if i.get("moved"):
                continue
            lo, hi = min(p, q), max(p, q)
            if i["y1"] - i["y0"] > 1 and i["y0"] > lo - .2 and i["y1"] < hi + .2:
                k = (q - edge) / (q - p)
                for v in i["verts"]:
                    v.co.y = q + (v.co.y - q) * k
                i["moved"] = True
                scaled += 1
            elif i["y1"] - i["y0"] < .4 and 0 <= (i["yc"] - edge) * side <= .25 and abs(i["yc"] - q) > POST_TOL:
                kill.add(id(i))          # a baluster too close to the moved post
    for i in isl:
        if i.get("moved"):
            continue
        if g[0] + KEEP < i["yc"] < g[1] - KEEP or id(i) in kill:
            bmesh.ops.delete(bm, geom=i["verts"], context="VERTS")
            cut += 1
    bm.transform(mw.inverted())
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    print(f"{ob.name}: opening y {g[0]:.2f} .. {g[1]:.2f}, posts {posts}, moved {moved} post pieces, "
          f"shortened {scaled} rails, deleted {cut} pieces, {len(ob.data.polygons)} faces left")
    return moved + cut + scaled


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    return col


def apply_all():
    changed = 0
    for name, yc in GAPS:
        changed += open_gap(bpy.data.objects[name], yc)
    return changed


def preview(out):
    import cannon_conveyor_look as look
    os.makedirs(out, exist_ok=True)
    load()
    apply_all()
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_backface_culling = True
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
        elif o.type == "MESH":
            cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
            o.hide_render = max(c.z for c in cs) > 4 or min(c.z for c in cs) < -.5
    sun, cam = look.stage(sc)
    look.shoot(sc, sun, cam, out, (
        ("rail_gap_left.png", (-3.5, -4.0, 1.6), (-9.5, -4.0, 1.2), 26, 90),
        ("rail_gap_right.png", (3.5, 5.0, 1.6), (9.5, 5.0, 1.2), 26, 270),
        ("rail_gap_left_wide.png", (-2.0, -11.0, 3.0), (-8.5, -3.5, .8), 26, 60),
    ))


def apply():
    from export_library_fbx import SETTINGS
    col = load()
    if not apply_all():
        print("railings: already open in front of the cannons")
        return
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {FBX}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])
    else:
        apply()
