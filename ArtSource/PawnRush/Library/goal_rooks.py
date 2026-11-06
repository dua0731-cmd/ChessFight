"""Pawn Rush library (작전 회의실): the three goal rooks remade - a piece over a pedestal, the middle one larger (R448).

    blender --background --factory-startup --python goal_rooks.py                       (writes the FBX + marble map)
    blender --background --factory-startup --python goal_rooks.py -- --preview D:/check (renders, writes nothing)

승규 님 (11-05, a shot of the three grey rooks on the goal's star platform and a concept of white marble rooks with a
golden light under the middle one): "1. 모델링을 더 멋있게 해주고 왼쪽이랑 오른쪽은 사이즈 비슷하게 가운데는 사이즈를 살짝
크게 2. 이 기물 3명이 플레이어들을 공격을 할거야 ... vfx를 기물이랑 받침대 사이에서 빛이 들어오고 거기서 차징 하면서 쏘게 해".
The old rooks (GEO_Rook_goal_piece(.001/.002), one 1316-face clay mesh each, x -2.25 / 0 / 2.25, y 19.9) go. Each new
rook is three nodes under an empty Goal_rook_<-1|0|1> on the star platform (z 1.49):
  _pedestal  a square stepped marble pedestal - plinth, body with a brass band, top slab - and on its top an iron socket ring round a glow disc (Library rook glow), the light that shows
             when the piece rises off it
  _piece     the rook, its origin at its foot (so Unity lifts and turns it): a turned marble body (foot rings, a brass
             collar, a tapering shaft, a brass neck band, a flared crown), four crenellated merlons over a hollow top,
             an arched window with a brass frame on its front (toward the course, -y) and arrow slits on the other
             three sides
  _gap       an empty at the socket's middle: where the light gathers and the shots leave from
The sides are 2.14 m high on a 1.4 m pedestal, the middle one 1.2 times (2.57 m, 1.68 m). Marble is a new tiling map
(Assets/Resources/PawnRushLibrary/WarProps/LibraryMarble + _MS: cream white with grey and faint gold veins, polished);
the pedestal's darker parts the same map tinted ("Library marble dark"). Unity: Course01v4Builder.LibraryLooks (looks),
Course01v4Builder.LibraryRooks + LibraryRookTurret (the attack). Running it again changes nothing (Goal_rook_0 first).
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import war_room_props as wp  # noqa: E402
from war_room_props import Geo, fnoise, rgb, lerp, save_maps  # noqa: E402
from basement_dressing import lathe  # noqa: E402

MARBLE, MARBLE_DARK, GLOW = "Library marble", "Library marble dark", "Library rook glow"
wp.MAPS[MARBLE] = "LibraryMarble"
wp.MAPS[MARBLE_DARK] = "LibraryMarble"
wp.TINTS[MARBLE_DARK] = (.62, .6, .58)
OLD = ("GEO_Rook_goal_piece", "GEO_Rook_goal_piece.001", "GEO_Rook_goal_piece.002")
ROOKS = ((-1, -2.25, 1.0), (0, 0.0, 1.2), (1, 2.25, 1.0))      # (name, x, size)
Y, Z = 19.9, 1.49                                                 # the star platform's top
TILE = .8


def paint_marble(base, size=1024):
    """Tiling polished marble: cream white, soft clouds, thin grey veins and a few faint gold ones."""
    cloud = fnoise(size, size, 3.2, 81)
    warp = fnoise(size, size, 2.6, 82)
    v1 = np.abs(np.sin(2 * math.pi * (fnoise(size, size, 3.4, 83, (1.6, 1)) * .9 + warp * .35)))
    v2 = np.abs(np.sin(2 * math.pi * (fnoise(size, size, 3.0, 84, (1, 1.8)) * .7 + warp * .25)))
    col = np.ones((size, size, 3), np.float32) * rgb(236, 231, 222)
    col *= (1 + .035 * cloud[..., None])
    grey = np.clip(1 - v1 / .06, 0, 1) ** 1.5 * .55 + np.clip(1 - v1 / .02, 0, 1) * .25
    col = lerp(col, np.ones_like(col) * rgb(150, 148, 150), grey)
    gold = np.clip(1 - v2 / .025, 0, 1) * (fnoise(size, size, 3.0, 85) > .4)
    col = lerp(col, np.ones_like(col) * rgb(196, 160, 96), gold * .7)
    smooth = np.clip(.62 - .1 * grey + .03 * cloud, .3, .75)
    save_maps(base, col, np.zeros((size, size)), smooth)


def ring_uv(r, h):
    """A lathe rect that tiles the marble round and along."""
    return (0, 0, 2 * math.pi * r / TILE, h / TILE)


def merlons(g, s, z0, z1, ri, ro, count=4, width=52.0):
    """Curved battlement blocks round the crown, one centred on the front (-y)."""
    seg = 5
    for k in range(count):
        mid = -math.pi / 2 + 2 * math.pi * k / count
        a0, a1 = mid - math.radians(width) / 2, mid + math.radians(width) / 2
        ang = [a0 + (a1 - a0) * i / seg for i in range(seg + 1)]

        def pt(r, a, z):
            return Vector((r * s * math.cos(a), r * s * math.sin(a), z * s))
        rows = {}
        for (key, r, z) in (("ob", ro, z0), ("ot", ro, z1), ("ib", ri, z0), ("it", ri, z1)):
            rows[key] = [g.bm.verts.new(pt(r, a, z)) for a in ang]
        for i in range(seg):
            u0, u1 = ang[i] * ro * s / TILE, ang[i + 1] * ro * s / TILE
            g.face((rows["ob"][i], rows["ob"][i + 1], rows["ot"][i + 1], rows["ot"][i]),
                   ((u0, z0 * s / TILE), (u1, z0 * s / TILE), (u1, z1 * s / TILE), (u0, z1 * s / TILE)), MARBLE)
            g.face((rows["ib"][i + 1], rows["ib"][i], rows["it"][i], rows["it"][i + 1]),
                   ((u1, 0), (u0, 0), (u0, .2), (u1, .2)), MARBLE)
            g.face((rows["ot"][i], rows["ot"][i + 1], rows["it"][i + 1], rows["it"][i]),
                   ((u0, .3), (u1, .3), (u1, .4), (u0, .4)), MARBLE)
        for side, i in ((0, 0), (1, seg)):
            q = [rows["ib"][i], rows["ob"][i], rows["ot"][i], rows["it"][i]]
            if side == 1:
                q = q[::-1]
            g.face(q, ((0, 0), (.1, 0), (.1, .2), (0, .2)), MARBLE)


def flat_plate(g, s, a, z0, z1, r, w, t, mat):
    """A thin flat plate standing on the shaft's surface at angle a (its middle), w wide, from z0 to z1."""
    n = Vector((math.cos(a), math.sin(a), 0))
    tng = Vector((-math.sin(a), math.cos(a), 0))
    c = n * r * s
    corners = []
    for (dx, dz) in ((-w / 2, z0), (w / 2, z0), (w / 2, z1), (-w / 2, z1)):
        corners.append(c + tng * dx * s + Vector((0, 0, dz * s)))
    out = [g.bm.verts.new(p + n * t * s) for p in corners]
    inn = [g.bm.verts.new(p - n * .01 * s) for p in corners]
    uv = ((0, 0), (1, 0), (1, 1), (0, 1))
    g.face(out, uv, mat)
    for i in range(4):
        j = (i + 1) % 4
        g.face((inn[i], inn[j], out[j], out[i]), uv, mat)


def piece(size):
    g = Geo("R448 goal rook piece")
    s = size
    prof = [(0, 0), (.54, 0), (.56, .03), (.56, .09), (.52, .11), (.53, .14), (.48, .17), (.43, .20),
            (.39, .25), (.34, .34), (.31, .50), (.285, .75), (.28, .95), (.29, 1.02), (.32, 1.05), (.30, 1.08),
            (.34, 1.18), (.41, 1.28), (.46, 1.33), (.47, 1.36), (.47, 1.44), (.32, 1.44), (.32, 1.40), (0, 1.40)]
    prof = [(r * s, z * s) for r, z in prof]
    length = sum(math.hypot(prof[i + 1][0] - prof[i][0], prof[i + 1][1] - prof[i][1]) for i in range(len(prof) - 1))
    lathe(g, (0, 0, 0), prof, MARBLE, 40, ring_uv(.5 * s, length))
    # brass collar at the foot and band at the neck
    lathe(g, (0, 0, .135 * s), [(.535 * s, 0), (.545 * s, .012 * s), (.545 * s, .04 * s), (.535 * s, .05 * s)], wp.GOLD, 40, cap=False)
    lathe(g, (0, 0, 1.0 * s), [(.285 * s, 0), (.30 * s, .012 * s), (.30 * s, .045 * s), (.29 * s, .055 * s)], wp.GOLD, 40, cap=False)
    merlons(g, s, 1.44, 1.66, .32, .47)
    # an arched window with a brass frame on the front (-y), arrow slits on the other three sides
    front = -math.pi / 2
    flat_plate(g, s, front, .52, .82, .302, .14, .012, wp.DARK)
    flat_plate(g, s, front, .50, .52, .31, .19, .02, wp.GOLD)
    flat_plate(g, s, front, .82, .86, .297, .19, .02, wp.GOLD)
    for k in (1, 2, 3):
        a = front + math.pi / 2 * k
        flat_plate(g, s, a, .58, .80, .298, .045, .01, wp.DARK)
    return g.mesh()


def pedestal(size):
    g = Geo("R448 goal rook pedestal")
    s = size

    def slab(half, z0, z1, mat, inset=0.0):
        h = (half - inset) * s
        g.box((-h, -h, z0 * s), (h, h, z1 * s), mat, wp.wood_uv((-h, -h, z0 * s), (h, h, z1 * s), TILE))
    slab(.70, 0, .10, MARBLE_DARK)
    slab(.66, .10, .13, MARBLE)
    slab(.62, .13, .36, MARBLE)
    slab(.625, .29, .33, wp.GOLD)
    slab(.67, .36, .45, MARBLE)
    # the socket: an iron ring round a glow disc where the piece stands
    lathe(g, (0, 0, .45 * s), [(.50 * s, 0), (.58 * s, 0), (.58 * s, .03 * s), (.50 * s, .03 * s), (.50 * s, 0)],
          wp.DARK, 40, cap=False)
    lathe(g, (0, 0, .451 * s), [(.50 * s, 0), (0, 0)], GLOW, 40, cap=False)      # facing up
    return g.mesh()


def glow_material():
    m = bpy.data.materials.get(GLOW)
    if m is None:
        m = bpy.data.materials.new(GLOW)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (1, .7, .3, 1)
        b.inputs["Emission Color"].default_value = (1, .6, .2, 1)
        b.inputs["Emission Strength"].default_value = 3
    return m


def build(col):
    if bpy.data.objects.get("Goal_rook_0") is not None:
        print("goal rooks: already remade")
        return False
    gone = 0
    for name in OLD:
        o = bpy.data.objects.get(name)
        if o is None:
            raise RuntimeError("missing " + name)
        for c in list(o.children_recursive) + [o]:
            bpy.data.objects.remove(c, do_unlink=True)
            gone += 1
    glow_material()
    meshes = {}
    for key, x, size in ROOKS:
        if size not in meshes:
            meshes[size] = (pedestal(size), piece(size))
        ped, pc = meshes[size]
        root = bpy.data.objects.new(f"Goal_rook_{key}", None)
        root.location = (x, Y, Z)
        col.objects.link(root)
        a = bpy.data.objects.new(f"Goal_rook_{key}_pedestal", ped)
        b = bpy.data.objects.new(f"Goal_rook_{key}_piece", pc)
        c = bpy.data.objects.new(f"Goal_rook_{key}_gap", None)
        b.location = (0, 0, .48 * size)
        c.location = (0, 0, .48 * size)
        for o in (a, b, c):
            col.objects.link(o)
            o.parent = root
        print("rook", key, "size", size, "pedestal", len(ped.polygons), "piece", len(pc.polygons), "faces")
    print("old rook objects removed:", gone)
    return True


def preview(out):
    import cannon_conveyor_look as look
    os.makedirs(out, exist_ok=True)
    wp.OUT = out
    paint_marble(os.path.join(out, "LibraryMarble"))
    col = wp.load()
    build(col)
    # the R402 materials come back from the FBX without their maps: rebuilt (as basement_dressing.py's preview)
    for name in list(wp.MAPS):
        old = bpy.data.materials.get(name)
        if old is None or name in (MARBLE, MARBLE_DARK):
            continue
        old.name = name + " (fbx)"
        keep = wp.OUT
        wp.OUT = os.path.join(wp.LIB, "WarProps")
        old.user_remap(wp.material(name))
        wp.OUT = keep
    bpy.context.view_layer.update()                     # the new nodes' world matrices, before reading bounds
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_backface_culling = True
        if m.name in wp.FLATS and m.use_nodes:
            c, sm, mt = wp.FLATS[m.name]
            bsdf = m.node_tree.nodes["Principled BSDF"]
            bsdf.inputs["Base Color"].default_value = (*[x ** 2.2 for x in c], 1)
            bsdf.inputs["Metallic"].default_value = mt
            bsdf.inputs["Roughness"].default_value = 1 - sm
        if m.name.startswith("CLAY"):
            m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.62, .55, .46, 1)
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
        elif o.type == "MESH":
            cs = [o.matrix_world @ Vector(cc) for cc in o.bound_box]
            o.hide_render = min(c.z for c in cs) > 5 or max(c.y for c in cs) < 12
    # the middle piece lifted as when it charges, to see the light between
    lifted = bpy.data.objects["Goal_rook_0_piece"]
    lifted.location.z += .3
    sun, cam = look.stage(sc)
    look.shoot(sc, sun, cam, out, (
        ("rooks_front.png", (0, 12.5, 3.0), (0, 19.9, 2.4), 32, 200),
        ("rooks_close.png", (1.6, 16.6, 2.6), (2.25, 19.9, 2.6), 34, 220),
        ("rook_center_lifted.png", (-1.4, 16.9, 2.2), (0, 19.9, 2.3), 34, 200),
    ))


def apply():
    from export_library_fbx import SETTINGS
    col = wp.load()
    if bpy.data.objects.get("Goal_rook_0") is not None:
        print("goal rooks: already remade")
        return
    paint_marble(os.path.join(wp.OUT, "LibraryMarble"))
    build(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=wp.FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {wp.FBX}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])
    else:
        apply()
