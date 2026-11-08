"""Pawn Rush library (작전 회의실): a return stair at the lower floor's north end on each side (R457).

    blender --background --factory-startup --python north_stairs.py                              (writes the FBX)
    blender --background --factory-startup --python north_stairs.py -- --preview D:/check   (renders, writes nothing)

승규 님 (a shot of the lower floor's northmost wall bookcase under its campaign map): "제일 북쪽 에서 떨어지면 남쪽 계단
까지 가서 계단을 타야 하니 까 북쪽에서 떨어진 사람은 지금 사진에 있는 소품 치우고 빠른 계단 설치 해 양 옆으로 도착 지점은
남쪽 계단과 북쪽 계단은 똑같아".

A pawn that falls at the north end walked the whole lower floor south to the return stairs' doors (y -5 .. -2). Now, on
each side wall:
  the northmost lower bookcase (Bookshelf_+-1_B05_Z-8, y 14 .. 18) and the campaign map over it (Basement_wall_map_+-1_4)
  taken away, a door cut through the wall there (y 14.62 .. 17.38, z -8 .. -4.96, as the lower return doors) with the
  same reveals (the lower door's jambs and lintel moved 19.6 m north) and the same classical surround outside;
  outside it a landing (y 14.3 .. 17.7), then a stair of the return stairs' pitch (32 steps of rise .25, run .375, 34
  degrees) climbing south along the wall from y 14.3 to the start floor's height (z 0) at y 2.3, the treads from the
  wall's face (x 15.5 .. 19.1; the piers stand through them), the stringer wall, pilasters, blind arches, consoles and
  turned balustrade as the return stairs';
  at the top a landing (y -2 .. 2.3) and a gallery at z 0 beside the old return stair (x 19.6 .. 22.6, y -17.5 .. 2.3),
  a solid mass with a tall blind arcade on its outer face, a cornice under the deck and a balustrade round it, that
  opens onto the old stair's upper landing (y -17.5 .. -15.8) - the same arrival, the start floor's door.
The old stairs are rebuilt with that landing's outer side open (return_stairs.build_side(open_outer=True)).
One material, the environment's clay (Unity paints "Return_stair..." as floor stone). Unity's colliders:
Course01v4Builder.LibraryReplacement.cs (the north landings, steps, ramps, landing and gallery).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import return_stairs as rs  # noqa: E402
from return_stairs import BALUSTER, Build, newel, flat_balustrade  # noqa: E402

FBX, CLAY = rs.FBX, rs.CLAY
RUN, RISE, STEPS, BASE = rs.RUN, rs.RISE, rs.STEPS, rs.BASE
WALL_OUT = rs.WALL_OUT                    # 15.48
XI, X1, XO = 15.5, rs.X1, rs.XO            # treads from the wall's face, the stringer 19.1 .. 19.4
Y0 = 14.3                                  # the first step's near edge
YTOP = Y0 - RUN * STEPS                    # 2.3: the top step's far edge
LAND_LO = (Y0, 17.7)                       # the lower landing outside the door
TOP_S = -2.0                               # the top landing's south edge (over the old lower landing's north end)
GX0, GX1 = 19.6, 22.6                      # the gallery's lane
# R459 (승규 11-05, a shot of the join's newels: "사진 구간 넓혀줘"): the gallery and a strip beside the old landing
# carried 1.7 m further south, so the way from the gallery onto the landing is 3.4 m wide, not 1.7
OLD_END = rs.LAND_UP[0]                    # -17.5: the old upper landing's far end
G_END = OLD_END - 1.7                      # -19.2: the gallery's and the wider landing's far end
PIER_FACE = 16.3                           # the far end's balustrade stops short of the wall pier there (to 16.07)
JOIN = rs.YTOP                             # -15.8: the old stair's top; the gallery opens onto the landing beyond it
DOOR = (14.62, 17.38, -8.0, -4.96)
DY = (DOOR[0] + DOOR[1]) / 2 - (rs.DOOR_LO[0] + rs.DOOR_LO[1]) / 2   # 19.6: the lower return door to this one
REMOVE = ("Bookshelf_1_B05_Z-8", "Bookshelf_-1_B05_Z-8", "Basement_wall_map_1_4", "Basement_wall_map_-1_4")
NAME = "Return_stair_north_"


def tread_z(y):
    """The nosing line (as return_stairs.tread_z, from this stair's first step)."""
    return BASE + RISE * (Y0 - y) / RUN


def kerb_x(b, x, ya, yb, z):
    b.box(x - .12, x + .12, min(ya, yb), max(ya, yb), z, z + .25)


def kerb_y(b, y, xa, xb, z):
    b.box(min(xa, xb), max(xa, xb), y - .12, y + .12, z, z + .25)


def blind_arcade(b, x, ya, yb, z0, z1, bay=3.15):
    """A tall blind arcade on a face at x (facing +x before mirroring), from ya down to yb: pilasters, round arches
    with keystones, a plinth and a string course."""
    n = max(int(round((ya - yb) / bay)), 1)
    step = (ya - yb) / n
    b.box(x, x + .1, yb, ya, z0, z0 + .45)                                  # plinth
    b.box(x, x + .14, yb, ya, z0 + .45, z0 + .55)
    for j in range(n + 1):                                                  # pilasters
        y = ya - step * j
        b.box(x, x + .12, y - .26, y + .26, z0 + .55, z1 - .9)
        b.box(x, x + .18, y - .32, y + .32, z1 - 1.05, z1 - .9)             # capital
    r_out, r_in = step / 2 - .3, step / 2 - .48
    spring = z1 - 1.35 - r_out
    for j in range(n):
        yc = ya - step * (j + .5)
        b.box(x, x + .05, yc - r_out, yc - r_in, z0 + .55, spring)          # jambs
        b.box(x, x + .05, yc + r_in, yc + r_out, z0 + .55, spring)
        b.box(x, x + .08, yc - r_out - .05, yc + r_out + .05, z0 + 1.2, z0 + 1.3)   # sill
        seg = 14
        for i in range(seg):
            a0, a1 = math.pi * i / seg, math.pi * (i + 1) / seg
            pts = [(yc + r_in * math.cos(a0), spring + r_in * math.sin(a0)),
                   (yc + r_out * math.cos(a0), spring + r_out * math.sin(a0)),
                   (yc + r_out * math.cos(a1), spring + r_out * math.sin(a1)),
                   (yc + r_in * math.cos(a1), spring + r_in * math.sin(a1))]
            b.prism_yz(pts, x, x + .06)
        b.prism_yz([(yc - .12, spring + r_in - .05), (yc + .12, spring + r_in - .05), (yc + .18, spring + r_out + .1),
                    (yc - .18, spring + r_out + .1)], x, x + .1)            # keystone
    b.box(x, x + .2, yb, ya, z1 - .9, z1 - .78)                             # string course under the cornice


def build_side(side):
    b = Build(side)
    # ---- treads, nosings and the stair mass under them (as the return stairs', from the wall's face)
    for k in range(1, STEPS + 1):
        y_hi, y_lo = Y0 - RUN * (k - 1), Y0 - RUN * k
        top = BASE + RISE * k
        b.box(XI, X1, y_lo, y_hi, BASE - .3, top - .06)
        b.box(XI, X1, y_lo, y_hi + .035, top - .06, top)
        b.cylinder((XI, y_hi + .035, top - .03), (X1, y_hi + .035, top - .03), .03, 10)
    # ---- outer stringer wall: sloped top with coping, plinth, pilasters, blind arches, consoles beneath
    ya, yb = Y0 + .3, YTOP + .5
    b.prism_yz([(ya, BASE - .3), (ya, tread_z(Y0) + .18), (yb, tread_z(yb) + .18), (yb, BASE - .3)], X1, XO)
    b.prism_yz([(ya + .03, tread_z(Y0) + .16), (ya + .03, tread_z(Y0) + .25), (yb, tread_z(yb) + .25),
                (yb, tread_z(yb) + .16)], X1 - .04, XO + .05)
    b.box(XO, XO + .07, yb, ya, BASE - .3, BASE + .05)                       # plinth band
    b.box(XO, XO + .05, yb, ya, BASE - .42, BASE - .3)                       # underside moulding
    b.box(XI, XO + .05, yb, ya, BASE - .42, BASE - .3)
    for j in range(8):
        y = Y0 - .2 - 1.5 * j
        b.prism_xz([(XI + .3, BASE - .42), (XO + .02, BASE - .42), (XO + .02, BASE - .55), (XO - .25, BASE - .82),
                    (XI + .3, BASE - .82)], y - .14, y + .14)
    for j in range(1, 8):
        y = Y0 - 1.5 * j
        top = tread_z(min(y + .17, Y0)) + .12
        if top - BASE < .8:
            continue
        b.box(XO, XO + .07, y - .17, y + .17, BASE + .05, top - .1)
        b.box(XO, XO + .1, y - .21, y + .21, top - .16, top - .06)
    for j in range(7):
        ya_, yb_ = Y0 - 1.5 * j - .17, Y0 - 1.5 * (j + 1) + .17
        yc = (ya_ + yb_) / 2
        wall_top = tread_z(min(ya_, Y0)) + .12
        r_out, r_in = .52, .44
        spring = wall_top - .32 - r_out
        if spring - (BASE + .05) < .5:
            continue
        for y in (yc - (r_out + r_in) / 2, yc + (r_out + r_in) / 2):
            b.box(XO, XO + .05, y - .04, y + .04, BASE + .35, spring)
        b.box(XO, XO + .07, yc - r_out - .04, yc + r_out + .04, BASE + .3, BASE + .37)
        for i in range(10):
            a0, a1 = math.pi * i / 10, math.pi * (i + 1) / 10
            pts = [(yc + r_in * math.cos(a0), spring + r_in * math.sin(a0)),
                   (yc + r_out * math.cos(a0), spring + r_out * math.sin(a0)),
                   (yc + r_out * math.cos(a1), spring + r_out * math.sin(a1)),
                   (yc + r_in * math.cos(a1), spring + r_in * math.sin(a1))]
            b.prism_yz(pts, XO, XO + .05)
        b.box(XO, XO + .08, yc - .06, yc + .06, spring + r_in - .02, spring + r_out + .06)
    # ---- the sloped balustrade on the stringer, its last newel on the top landing
    xc = (X1 + XO) / 2 + .05
    last = YTOP - .12
    newel_ys = [Y0 + .1] + [Y0 - RUN * 8 * i for i in (1, 2, 3)] + [last]

    def rail_z(y):
        return min(tread_z(min(y, Y0)), 0.0) + .25
    for y0_, y1_ in zip(newel_ys, newel_ys[1:]):
        za, zb = rail_z(y0_), rail_z(y1_)
        ra, rb = y0_ - .2, y1_ + .2
        za_r, zb_r = za + (zb - za) * (y0_ - ra) / (y0_ - y1_), za + (zb - za) * (y0_ - rb) / (y0_ - y1_)
        b.prism_yz([(ra, za_r), (ra, za_r + .1), (rb, zb_r + .1), (rb, zb_r)], xc - .1, xc + .1)
        b.prism_yz([(ra, za_r + .82), (ra, za_r + .9), (rb, zb_r + .9), (rb, zb_r + .82)], xc - .12, xc + .12)
        b.cylinder((xc, ra, za_r + .94), (xc, rb, zb_r + .94), .085, 12)
        n = max(int((y0_ - y1_) / .27), 1)
        for i in range(1, n):
            b.turned(xc, y0_ - (y0_ - y1_) * i / n, za + (zb - za) * i / n + .1, .72, BALUSTER)
    for y in newel_ys:
        newel(b, xc, y, rail_z(y), 1.12)
    # ---- the lower landing outside the door, a kerb and balustrade round its open sides
    b.box(15.0, XO, LAND_LO[0], LAND_LO[1], BASE - .3, BASE)
    b.box(XO - .1, XO + .06, LAND_LO[0] - .06, LAND_LO[1], BASE - .42, BASE - .26)
    b.box(WALL_OUT, XO + .06, LAND_LO[1] - .06, LAND_LO[1] + .06, BASE - .42, BASE - .26)
    b.box(X1 - .04, XO + .05, Y0 + .1, LAND_LO[1] + .03, BASE, BASE + .12)
    b.box(WALL_OUT, XO + .05, LAND_LO[1] - .24, LAND_LO[1] + .03, BASE, BASE + .12)
    flat_balustrade(b, [(xc, Y0 + .1), (xc, LAND_LO[1] - .12), (15.75, LAND_LO[1] - .12)], BASE + .12)
    # ---- the top landing and the gallery: a solid mass, the deck on it, a cornice under the deck's edges
    b.box(XI, GX1, TOP_S, YTOP, BASE - .3, 0.0)                              # top landing (and its mass)
    b.box(GX0, GX1, G_END, TOP_S, BASE - .3, 0.0)                            # gallery
    b.box(XO, GX0, OLD_END, JOIN, -.3, 0.0)                                  # the deck over to the old landing
    b.box(WALL_OUT, GX0, G_END, OLD_END, BASE - .3, 0.0)                     # the landing's strip beyond its old end
    b.box(GX1, GX1 + .1, G_END - .1, YTOP + .1, BASE - .42, BASE - .3)       # foot moulding
    b.box(GX1 - .02, GX1 + .22, G_END - .22, YTOP + .22, -.3, -.12)          # cornice under the deck
    b.box(GX1 - .02, GX1 + .3, G_END - .3, YTOP + .3, -.12, 0.0)
    b.box(XO + .2, GX1 + .3, YTOP, YTOP + .3, -.3, 0.0)                     # (its north return)
    b.box(WALL_OUT, GX1 + .3, G_END - .3, G_END, -.3, 0.0)                  # (its south return)
    blind_arcade(b, GX1, YTOP, G_END, BASE - .3, 0.0)
    # the gallery's north and south ends and the top landing's south face: plinth and string course
    for x0_, x1_, y in ((XO, GX1, YTOP), (WALL_OUT, GX1, G_END), (XI, GX0, TOP_S)):
        d = 1 if y == YTOP else -1
        ya_, yb_ = sorted((y, y + .1 * d))
        b.box(x0_, x1_, ya_, yb_, BASE - .3, BASE + .15)
        ya_, yb_ = sorted((y, y + .16 * d))
        b.box(x0_, x1_, ya_, yb_, -.9, -.78)
    # ---- kerbs and balustrades round the top landing and the gallery (the old landing's far end too)
    zb = 0.0
    out = GX1 - .12
    kerb_y(b, YTOP - .12, xc + .2, out + .12, zb)                            # north edge, over the outer lane
    kerb_x(b, out, YTOP - .12, G_END + .12, zb)                              # outer edge
    kerb_y(b, G_END + .12, out + .12, PIER_FACE - .2, zb)                    # the far end, to the wall pier
    kerb_y(b, TOP_S + .12, XI + .25, GX0 + .24, zb)                          # the top landing's south edge
    kerb_x(b, GX0 + .12, TOP_S + .12, JOIN, zb)                              # inner edge, over the old stair
    outer = [(xc, last), (out, last)]
    n = int(round((last - (G_END + .12)) / 4.0))
    outer += [(out, last - (last - (G_END + .12)) * i / n) for i in range(1, n + 1)]
    outer += [(GX0, G_END + .12), (PIER_FACE, G_END + .12)]
    flat_balustrade(b, outer, zb + .25)
    inner = [(15.75, TOP_S + .12), (GX0 + .12, TOP_S + .12)]
    n = int(round((TOP_S + .12 - JOIN) / 4.0))
    inner += [(GX0 + .12, TOP_S + .12 - (TOP_S + .12 - JOIN) * i / n) for i in range(1, n + 1)]
    flat_balustrade(b, inner, zb + .25, False)
    # ---- the classical door surround on the wall's outer face (as the return doors')
    y0_, y1_, z0_, z1_ = DOOR
    for y in (y0_ - .17, y1_ + .17):
        b.box(WALL_OUT, WALL_OUT + .12, y - .15, y + .15, z0_, z1_ + .02)
        b.box(WALL_OUT, WALL_OUT + .16, y - .19, y + .19, z0_, z0_ + .28)
    b.box(WALL_OUT, WALL_OUT + .14, y0_ - .34, y1_ + .34, z1_ + .02, z1_ + .32)
    b.box(WALL_OUT, WALL_OUT + .2, y0_ - .42, y1_ + .42, z1_ + .32, z1_ + .4)
    b.box(WALL_OUT, WALL_OUT + .26, y0_ - .46, y1_ + .46, z1_ + .4, z1_ + .48)
    yc = (y0_ + y1_) / 2
    b.prism_yz([(yc - .1, z1_ - .06), (yc + .1, z1_ - .06), (yc + .15, z1_ + .3), (yc - .15, z1_ + .3)],
               WALL_OUT, WALL_OUT + .2)
    me = bpy.data.meshes.new(NAME + ("R" if side > 0 else "L"))
    b.bm.to_mesh(me)
    b.bm.free()
    return me


# ------------------------------------------------------------------ editing the environment

def bounds(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return (Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
            Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))))


def cut_doors(col):
    """The north door through each side wall: a boolean on every wall piece the opening crosses."""
    view = bpy.context.view_layer
    view.update()
    y0_, y1_, z0_, z1_ = DOOR
    for side in (1, -1):
        lo = Vector((min(side * 14.2, side * 15.7), y0_, z0_ + .002))
        hi = Vector((max(side * 14.2, side * 15.7), y1_, z1_))
        walls = []
        for o in col.all_objects:
            if o.type != "MESH" or o.name.startswith((NAME, "Doorway", "Return_stair")):
                continue
            a, c = bounds(o)
            if all(a[i] < hi[i] and c[i] > lo[i] for i in range(3)):
                walls.append(o)
        bm = bmesh.new()
        r = bmesh.ops.create_cube(bm, size=1.0)
        for v in r["verts"]:
            v.co = Vector(((lo[i] + hi[i]) / 2 + v.co[i] * (hi[i] - lo[i]) for i in range(3)))
        bm.verts.ensure_lookup_table()
        for v in bm.verts:                      # down past the floor's top a little, so no skin is left in the sill
            if v.co.z < (lo.z + hi.z) / 2:
                v.co.z = z0_ - .01
        me = bpy.data.meshes.new("door cutter")
        bm.to_mesh(me)
        bm.free()
        cutter = bpy.data.objects.new("door cutter", me)
        bpy.context.scene.collection.objects.link(cutter)
        for wall in walls:
            if "floor" in wall.name.lower():
                continue
            if wall.data.users > 1:
                wall.data = wall.data.copy()
            mod = wall.modifiers.new("door", "BOOLEAN")
            mod.operation, mod.object, mod.solver = "DIFFERENCE", cutter, "EXACT"
            for o in view.objects:
                o.select_set(False)
            wall.select_set(True)
            view.objects.active = wall
            before = len(wall.data.polygons)
            bpy.ops.object.modifier_apply(modifier=mod.name)
            print("CUT", side, wall.name, before, "->", len(wall.data.polygons))
        bpy.data.objects.remove(cutter)


def reveals(col):
    """The lower return doors' jambs and lintel (right and mirrored left), copied 19.6 m north."""
    bpy.context.view_layer.update()
    n = 0
    for o in list(col.all_objects):
        if o.type != "MESH" or not o.name.startswith(("Doorway jamb", "Doorway lintel", "Doorway left")):
            continue
        a, c = bounds(o)
        if c.z > -4.0 or c.y < rs.DOOR_LO[0] - .6 or a.y > rs.DOOR_LO[1] + .6:
            continue
        me = o.data.copy()
        me.transform(Matrix.Translation((0, DY, 0)) @ o.matrix_world)
        dup = bpy.data.objects.new(o.name.replace("Doorway", "Doorway north"), me)
        col.objects.link(dup)
        n += 1
    print("REVEALS", n)


def rebuild(col):
    """Run again on an FBX that has them (R459), only the stairs are made again (the doors and reveals are there)."""
    again = bpy.data.objects.get(NAME + "R") is not None
    removed = 0
    for o in list(col.all_objects):
        if o.name.split("|")[0] in REMOVE or o.name.startswith(("Return_stair_R", "Return_stair_L", NAME)):
            bpy.data.objects.remove(o)
            removed += 1
    clay = bpy.data.materials.get(CLAY)
    for side in (1, -1):
        tag = "R" if side > 0 else "L"
        me = rs.build_side(side, open_outer=True)                         # the old stair, its landing open outward
        me.materials.append(clay)
        col.objects.link(bpy.data.objects.new("Return_stair_" + tag, me))
        me = build_side(side)
        me.materials.append(clay)
        ob = bpy.data.objects.new(NAME + tag, me)
        col.objects.link(ob)
        print("STAIR", ob.name, len(me.polygons), "faces")
    if not again:
        reveals(col)
        cut_doors(col)
    print("REMOVED", removed, "(stairs only, made again)" if again else "")


def load(col_name="PawnRushLibraryEnvironment", anim=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    col = bpy.data.collections.new(col_name)
    bpy.context.scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=anim)
    return col


def preview(out):
    from cannon_conveyor_look import stage, shoot
    os.makedirs(out, exist_ok=True)
    col = load()
    rebuild(col)
    sc = bpy.context.scene
    b = bpy.data.materials.get(CLAY).node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (.46, .36, .27, 1)
    b.inputs["Roughness"].default_value = .7
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
    sun, cam = stage(sc)
    shoot(sc, sun, cam, out, (
        ("north_right_outside.png", (38.0, 4.0, 2.0), (19.0, 0.0, -4.0), 24, 240),
        ("north_left_outside.png", (-38.0, 4.0, 2.0), (-19.0, 0.0, -4.0), 24, 120),
        ("north_right_from_north.png", (24.0, 34.0, 4.0), (18.0, 8.0, -4.0), 26, 230),
        ("north_basement_door.png", (0.0, 8.0, -6.2), (14.0, 16.0, -6.5), 26, 240),
        ("north_top_landing.png", (17.5, 8.0, 3.0), (20.0, -6.0, -1.0), 22, 240),
        ("gallery_join.png", (21.0, -6.0, 2.2), (18.0, -17.0, -.5), 24, 240),
        ("join_top.png", (18.5, -17.0, 15.0), (18.5, -17.4, 0.0), 26, 240),
        ("both_from_front.png", (0.0, -58.0, 8.0), (0.0, -4.0, -4.0), 30, 200),
    ))


def apply():
    """Writes the north stairs into the environment FBX (again: the stairs only, R459)."""
    from export_library_fbx import SETTINGS
    col = load(anim=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    before = len(col.all_objects)
    rebuild(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"north stairs: {before} -> {len(col.all_objects)} objects -> {FBX}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])
    else:
        apply()
