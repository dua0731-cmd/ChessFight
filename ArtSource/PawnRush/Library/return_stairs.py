"""Pawn Rush library (작전 회의실): the outside return stair rebuilt in detail, and a mirrored one on the left — preview (R377).

    blender --background --factory-startup --python return_stairs.py                              (writes the FBX, R378)
    blender --background --factory-startup --python return_stairs.py -- --preview D:/check   (renders, writes nothing)

승규 님 (a shot of the outside stair from its upper landing): "작전 회의실 계단 모델링 디테일 하게 해주고 오른쪽만 있는데
왼쪽도 설치해서 대칭으로 해".

The stair (Blender +x side, outside the side wall at x 15.48) climbs from the basement door (lower landing, z -8) to
the start floor's door (upper landing, z 0): 32 steps of rise .25 and run .375 from y -3.8 toward -y, x 16.1 .. 19.1.
It was plain boxes (steps from the ground, a slab per landing, square posts and one bar). Rebuilt with the same
walking surfaces (each tread's top and its y span as before, so Unity's step colliders still fit):
  treads with a bullnose nosing over solid risers, the stair mass under them down to a moulded underside with
  consoles; an outer stringer wall with a plinth, pilasters and blind arches, a sloped coping; a balustrade of turned
  balusters between a sloped base rail and a moulded handrail, newels with caps and ball finials every 8 steps;
  landings with moulded edges and balustrades; classical door surrounds (pilasters, architrave, cornice, keystone).
The upper landing used to reach over the last four steps (a 0.75 m lip under it at the top); it now starts where the
top step ends (y -15.8) and keeps a threshold strip by the door (x 13 .. 16.1, y -15.8 .. -14.5).
The left side (x -> -x) gets the same stair, its two door openings cut through the left wall (limestone, backing,
moulding) where the right ones are, the reveals (jambs, lintel) mirrored, and the two bookcases in those openings
taken away. One material, the environment's clay.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

FBX = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets",
                                    "Resources", "PawnRushLibrary", "PawnRushLibraryEnvironment.fbx"))
CLAY = "CLAY | uniform neutral gray"
X0, X1, XO = 16.1, 19.1, 19.4          # stair inner edge, outer edge of the treads, outer face of the stringer wall
Y0, RUN, RISE, STEPS, BASE = -3.8, .375, .25, 32, -8.0
YTOP = Y0 - RUN * STEPS                # -15.8: the top step's far edge
LAND_UP = (-17.5, YTOP)                # upper landing y span (beyond the stair)
LAND_LO = (-5.1, -2.1)                 # lower landing y span
WALL_OUT = 15.48                       # outer face of the side wall
DOOR_UP = (-17.38, -14.62, 0.0, 3.1)   # opening y0, y1, z0, z1 (between the jambs)
DOOR_LO = (-4.98, -2.22, -8.0, -4.96)
OLD = ("Return stair step", "Return stair guard post", "Return stair handrail", "Upper return landing",
       "Lower return landing")
REVEAL = ("Doorway jamb", "Doorway lintel")
LEFT_WALL = ("Side_wall_staggered_limestone_-1", "Side_wall_solid_backing_-1", "Continuous_side_wall_molding")
LEFT_BOOKCASES = ("Bookshelf_-1_B02_Z-8", "Bookshelf_-1_B00_Z0")


def tread_z(y):
    """The nosing line: the top of the step whose near edge is at y (y from Y0 down to YTOP)."""
    return BASE + RISE * (Y0 - y) / RUN


# ------------------------------------------------------------------ one-material bmesh helpers, mirrored by side

class Build:
    def __init__(self, side):
        self.s = side                   # +1 right (Blender +x), -1 left
        self.bm = bmesh.new()

    def X(self, x):
        return self.s * x

    def box(self, x0, x1, y0, y1, z0, z1):
        xa, xb = sorted((self.X(x0), self.X(x1)))
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        lo, hi = (xa, min(y0, y1), min(z0, z1)), (xb, max(y0, y1), max(z0, z1))
        for v in r["verts"]:
            v.co = Vector(((lo[i] + hi[i]) / 2 + v.co[i] * (hi[i] - lo[i]) for i in range(3)))

    def prism_yz(self, outline, x0, x1):
        """A polygon in the y-z plane extruded across x from x0 to x1 (mirrored with the side)."""
        a, b = self.X(x0), self.X(x1)
        lo = [self.bm.verts.new((a, y, z)) for y, z in outline]
        hi = [self.bm.verts.new((b, y, z)) for y, z in outline]
        faces = [self.bm.faces.new(lo), self.bm.faces.new(hi[::-1])]
        for i in range(len(outline)):
            j = (i + 1) % len(outline)
            faces.append(self.bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)

    def prism_xz(self, outline, y0, y1):
        """A polygon in the x-z plane (x before mirroring) extruded along y."""
        lo = [self.bm.verts.new((self.X(x), y0, z)) for x, z in outline]
        hi = [self.bm.verts.new((self.X(x), y1, z)) for x, z in outline]
        faces = [self.bm.faces.new(lo), self.bm.faces.new(hi[::-1])]
        for i in range(len(outline)):
            j = (i + 1) % len(outline)
            faces.append(self.bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)

    def cylinder(self, a, b, r, segments=12):
        a = Vector((self.X(a[0]), a[1], a[2]))
        b = Vector((self.X(b[0]), b[1], b[2]))
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segments, radius1=r, radius2=r,
                                    depth=(b - a).length)
        rot = Vector((0, 0, 1)).rotation_difference(b - a).to_matrix().to_4x4()
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=res["verts"])
        for f in {f for v in res["verts"] for f in v.link_faces}:
            f.smooth = len(f.verts) == 4

    def turned(self, x, y, z0, height, profile, segments=12):
        """A vertical turned piece (baluster, finial): profile = (radius, fraction of height) from the bottom."""
        cx = self.X(x)
        rings = []
        for r, f in profile:
            z = z0 + f * height
            if r < 1e-6:
                rings.append([self.bm.verts.new((cx, y, z))] * segments)
            else:
                rings.append([self.bm.verts.new((cx + r * math.cos(2 * math.pi * i / segments),
                                                 y + r * math.sin(2 * math.pi * i / segments), z))
                              for i in range(segments)])
        faces = []
        for ra, rb in zip(rings, rings[1:]):
            for i in range(segments):
                j = (i + 1) % segments
                quad = [ra[i], ra[j], rb[j], rb[i]]
                corners = [v for k, v in enumerate(quad) if v not in quad[:k]]
                if len(corners) >= 3:
                    faces.append(self.bm.faces.new(corners))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        for f in faces:
            f.smooth = True

    def sphere(self, x, y, z, r):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=14, v_segments=8, radius=r)
        bmesh.ops.translate(self.bm, vec=Vector((self.X(x), y, z)), verts=res["verts"])
        for f in {f for v in res["verts"] for f in v.link_faces}:
            f.smooth = True


BALUSTER = ((.055, 0), (.055, .07), (.04, .1), (.06, .3), (.072, .42), (.05, .6), (.034, .74), (.034, .8),
            (.052, .84), (.052, .9), (.04, .93), (.04, 1.0))
FINIAL = ((0, 0), (.05, 0), (.05, .15), (.035, .25), (.09, .45), (.10, .6), (.07, .85), (0, 1.0))


def newel(b, x, y, z, height=1.0):
    """A square newel: base, shaft, cap and a ball finial on a neck."""
    b.box(x - .2, x + .2, y - .2, y + .2, z, z + .16)
    b.box(x - .16, x + .16, y - .16, y + .16, z + .16, z + height - .12)
    b.box(x - .2, x + .2, y - .2, y + .2, z + height - .12, z + height - .04)
    b.box(x - .17, x + .17, y - .17, y + .17, z + height - .04, z + height)
    b.turned(x, y, z + height, .26, FINIAL)


def flat_balustrade(b, pts, z, skip_first=True):
    """A level balustrade along the polyline pts [(x, y)] standing on z: base rail, balusters, handrail."""
    for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
        if abs(xa - xb) < 1e-6:
            b.box(xa - .11, xa + .11, ya, yb, z, z + .1)
            b.box(xa - .13, xa + .13, ya, yb, z + .82, z + .92)
            b.cylinder((xa, min(ya, yb), z + .95), (xa, max(ya, yb), z + .95), .085, 12)
            n = max(int(abs(yb - ya) / .28), 1)
            for i in range(1, n):
                b.turned(xa, ya + (yb - ya) * i / n, z + .1, .72, BALUSTER)
        else:
            b.box(xa, xb, ya - .11, ya + .11, z, z + .1)
            b.box(xa, xb, ya - .13, ya + .13, z + .82, z + .92)
            b.cylinder((min(xa, xb), ya, z + .95), (max(xa, xb), ya, z + .95), .085, 12)
            n = max(int(abs(xb - xa) / .28), 1)
            for i in range(1, n):
                b.turned(xa + (xb - xa) * i / n, ya, z + .1, .72, BALUSTER)
    for x, y in pts[1:] if skip_first else pts:   # the first post is the stair's own newel
        newel(b, x, y, z, 1.12)


def build_side(side, open_outer=False):
    """open_outer (R457, north_stairs.py): the upper landing's outer side left open onto the north stairs' gallery -
    the stringer's level part under the landing's slab, no level coping, no balustrade there (the gallery's own runs
    along the landing's far end)."""
    b = Build(side)
    # ---- treads, nosings and the stair mass under them (walking tops and spans as the old steps)
    for k in range(1, STEPS + 1):
        y_hi, y_lo = Y0 - RUN * (k - 1), Y0 - RUN * k
        top = BASE + RISE * k
        b.box(X0, X1, y_lo, y_hi, BASE - .3, top - .06)                      # riser and mass
        b.box(X0, X1, y_lo, y_hi + .035, top - .06, top)                     # tread with overhang
        b.cylinder((X0, y_hi + .035, top - .03), (X1, y_hi + .035, top - .03), .03, 10)   # bullnose
    b.box(X0, X1, LAND_UP[0], LAND_UP[1], BASE - .3, -.3)                    # mass under the upper landing
    # ---- outer stringer wall: sloped top with coping, plinth, pilasters, blind arches, consoles beneath
    ya = Y0 + .3
    if open_outer:
        b.prism_yz([(ya, BASE - .3), (ya, tread_z(Y0) + .18), (YTOP, .18), (YTOP, -.3), (LAND_UP[0], -.3),
                    (LAND_UP[0], BASE - .3)], X1, XO)
    else:
        b.prism_yz([(ya, BASE - .3), (ya, tread_z(Y0) + .18), (YTOP, .18), (LAND_UP[0], .18), (LAND_UP[0], BASE - .3)],
                   X1, XO)
    b.prism_yz([(ya + .03, tread_z(Y0) + .16), (ya + .03, tread_z(Y0) + .25), (YTOP, .25), (YTOP, .16)],
               X1 - .04, XO + .05)                                           # sloped coping
    if not open_outer:
        b.box(X1 - .04, XO + .05, LAND_UP[0] - .03, YTOP, .16, .25)          # its level part by the landing
    b.box(XO, XO + .07, LAND_UP[0], ya, BASE - .3, BASE + .05)               # plinth band
    b.box(XO, XO + .05, LAND_UP[0], ya, BASE - .42, BASE - .3)               # underside moulding
    b.box(X0 + .02, XO + .05, LAND_UP[0], ya, BASE - .42, BASE - .3)
    for j in range(9):                                                        # consoles under the mass
        y = Y0 - .2 - 1.5 * j
        b.prism_xz([(X0 + .3, BASE - .42), (XO + .02, BASE - .42), (XO + .02, BASE - .55), (XO - .25, BASE - .82),
                    (X0 + .3, BASE - .82)], y - .14, y + .14)
    for j in range(1, 9):                                                     # pilasters every 1.5 m
        y = Y0 - 1.5 * j
        top = tread_z(min(y + .17, Y0)) + .12
        if top - BASE < .8:
            continue
        b.box(XO, XO + .07, y - .17, y + .17, BASE + .05, top - .1)
        b.box(XO, XO + .1, y - .21, y + .21, top - .16, top - .06)           # capital
    for j in range(8):                                                        # blind arches between them
        ya_, yb_ = Y0 - 1.5 * j - .17, Y0 - 1.5 * (j + 1) + .17
        yc = (ya_ + yb_) / 2
        wall_top = tread_z(min(ya_, Y0)) + .12
        r_out, r_in = .52, .44
        spring = wall_top - .32 - r_out
        if spring - (BASE + .05) < .5:
            continue
        for y in (yc - (r_out + r_in) / 2,  yc + (r_out + r_in) / 2):         # jambs
            b.box(XO, XO + .05, y - .04, y + .04, BASE + .35, spring)
        b.box(XO, XO + .07, yc - r_out - .04, yc + r_out + .04, BASE + .3, BASE + .37)   # sill
        seg = 10
        for i in range(seg):                                                  # the arch ring
            a0, a1 = math.pi * i / seg, math.pi * (i + 1) / seg
            pts = [(yc + r_in * math.cos(a0), spring + r_in * math.sin(a0)),
                   (yc + r_out * math.cos(a0), spring + r_out * math.sin(a0)),
                   (yc + r_out * math.cos(a1), spring + r_out * math.sin(a1)),
                   (yc + r_in * math.cos(a1), spring + r_in * math.sin(a1))]
            b.prism_yz(pts, XO, XO + .05)
        b.box(XO, XO + .08, yc - .06, yc + .06, spring + r_in - .02, spring + r_out + .06)   # keystone
    # ---- the sloped balustrade on the stringer
    xc = (X1 + XO) / 2 + .05
    newel_ys = [Y0 + .1] + [Y0 - RUN * 8 * i for i in (1, 2, 3)] + [YTOP]
    for y0_, y1_ in zip(newel_ys, newel_ys[1:]):
        za, zb = tread_z(min(y0_, Y0)) + .25, tread_z(y1_) + .25
        ra, rb = y0_ - .2, y1_ + .2
        za_r, zb_r = za + (zb - za) * (y0_ - ra) / (y0_ - y1_), za + (zb - za) * (y0_ - rb) / (y0_ - y1_)
        b.prism_yz([(ra, za_r), (ra, za_r + .1), (rb, zb_r + .1), (rb, zb_r)], xc - .1, xc + .1)           # base rail
        b.prism_yz([(ra, za_r + .82), (ra, za_r + .9), (rb, zb_r + .9), (rb, zb_r + .82)], xc - .12, xc + .12)
        b.cylinder((xc, ra, za_r + .94), (xc, rb, zb_r + .94), .085, 12)                                   # handrail
        n = max(int((y0_ - y1_) / .27), 1)
        for i in range(1, n):
            y = y0_ - (y0_ - y1_) * i / n
            z = za + (zb - za) * i / n
            b.turned(xc, y, z + .1, .72, BALUSTER)
    for y in newel_ys:
        newel(b, xc, y, tread_z(min(y, Y0)) + .25, 1.12)
    # ---- landings: slabs with a moulded edge, balustrades round their open sides
    b.box(13.0, XO, LAND_UP[0], LAND_UP[1], -.3, 0.0)
    b.box(13.0, X0, LAND_UP[1], -14.5, -.3, 0.0)                              # the threshold strip by the door
    b.box(XO - .1, XO + .06, LAND_UP[0] - .06, LAND_UP[1], -.42, -.26)
    b.box(15.48, XO + .06, LAND_UP[0] - .06, LAND_UP[0] + .1, -.42, -.26)
    if not open_outer:                                                        # (R459: the gallery's wider landing goes on)
        b.box(15.48, XO + .05, LAND_UP[0] - .03, LAND_UP[0] + .24, 0.0, .25)  # kerb along the landing's far end
    if not open_outer:
        flat_balustrade(b, [(xc, YTOP), (xc, LAND_UP[0] + .12), (15.75, LAND_UP[0] + .12)], .25)
    b.box(13.0, XO, LAND_LO[0], LAND_LO[1], BASE - .3, BASE)
    b.box(XO - .1, XO + .06, LAND_LO[0], LAND_LO[1] + .06, BASE - .42, BASE - .26)
    b.box(15.48, XO + .06, LAND_LO[1] - .1, LAND_LO[1] + .06, BASE - .42, BASE - .26)
    b.box(X1 - .04, XO + .05, Y0 + .1, LAND_LO[1] + .05, BASE, BASE + .12)   # a kerb for the lower balustrade
    b.box(15.48, XO + .05, LAND_LO[1] - .24, LAND_LO[1] + .03, BASE, BASE + .12)
    flat_balustrade(b, [(xc, Y0 + .1), (xc, LAND_LO[1] - .12), (15.75, LAND_LO[1] - .12)], BASE + .12, False)
    # ---- classical door surrounds on the wall's outer face
    for y0_, y1_, z0_, z1_ in (DOOR_UP, DOOR_LO):
        for y in (y0_ - .17, y1_ + .17):                                       # pilasters with bases
            b.box(WALL_OUT, WALL_OUT + .12, y - .15, y + .15, z0_, z1_ + .02)
            b.box(WALL_OUT, WALL_OUT + .16, y - .19, y + .19, z0_, z0_ + .28)
        b.box(WALL_OUT, WALL_OUT + .14, y0_ - .34, y1_ + .34, z1_ + .02, z1_ + .32)        # architrave
        b.box(WALL_OUT, WALL_OUT + .2, y0_ - .42, y1_ + .42, z1_ + .32, z1_ + .4)          # cornice
        b.box(WALL_OUT, WALL_OUT + .26, y0_ - .46, y1_ + .46, z1_ + .4, z1_ + .48)
        yc = (y0_ + y1_) / 2
        b.prism_yz([(yc - .1, z1_ - .06), (yc + .1, z1_ - .06), (yc + .15, z1_ + .3), (yc - .15, z1_ + .3)],
                   WALL_OUT, WALL_OUT + .2)                                    # keystone
    me = bpy.data.meshes.new("Return stair " + ("R" if side > 0 else "L"))
    b.bm.to_mesh(me)
    b.bm.free()
    return me


# ------------------------------------------------------------------ editing the environment

def cut_left_doors(col):
    """Cuts the right side's two door openings through the left wall (boolean on each wall piece)."""
    cutters = []
    for y0_, y1_, z0_, z1_ in (DOOR_UP, DOOR_LO):
        bm = bmesh.new()
        r = bmesh.ops.create_cube(bm, size=1.0)
        lo, hi = (-15.7, y0_, z0_ - .01), (-14.2, y1_, z1_)
        for v in r["verts"]:
            v.co = Vector(((lo[i] + hi[i]) / 2 + v.co[i] * (hi[i] - lo[i]) for i in range(3)))
        me = bpy.data.meshes.new("door cutter")
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new("door cutter", me)
        bpy.context.scene.collection.objects.link(ob)
        cutters.append(ob)
    view = bpy.context.view_layer
    for name in LEFT_WALL:
        wall = bpy.data.objects[name]
        if wall.data.users > 1:
            wall.data = wall.data.copy()
        for c in cutters:
            mod = wall.modifiers.new("door", "BOOLEAN")
            mod.operation, mod.object, mod.solver = "DIFFERENCE", c, "EXACT"
        for o in view.objects:
            o.select_set(False)
        wall.select_set(True)
        view.objects.active = wall
        before = len(wall.data.polygons)
        for mod in list(wall.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        print("CUT", name, before, "->", len(wall.data.polygons))
    for c in cutters:
        bpy.data.objects.remove(c)


def rebuild(col):
    removed = 0
    for o in list(col.all_objects):
        # a bookcase is an empty with its mesh as a child named "<empty>|GAME_Bookcase...|Dupli|": both go
        if o.name.startswith(OLD) or o.name.split("|")[0] in LEFT_BOOKCASES:
            bpy.data.objects.remove(o)
            removed += 1
    clay = bpy.data.materials.get(CLAY)
    for side in (1, -1):
        me = build_side(side)
        me.materials.append(clay)
        ob = bpy.data.objects.new("Return_stair_" + ("R" if side > 0 else "L"), me)
        col.objects.link(ob)
        print("STAIR", ob.name, len(me.polygons), "faces")
    mirror = Matrix.Scale(-1, 4, (1, 0, 0))
    for o in list(col.all_objects):                                             # the door reveals, mirrored left
        if o.type == "MESH" and o.name.startswith(REVEAL):
            me = o.data.copy()
            me.transform(mirror @ o.matrix_world)                               # world space, mirrored, baked in
            bm = bmesh.new()
            bm.from_mesh(me)
            bmesh.ops.reverse_faces(bm, faces=bm.faces)                         # a mirror turns faces inside out
            bm.to_mesh(me)
            bm.free()
            dup = bpy.data.objects.new(o.name.replace("Doorway", "Doorway left"), me)
            col.objects.link(dup)
    cut_left_doors(col)
    print("REMOVED", removed)


# ------------------------------------------------------------------ preview

def preview(out):
    from cannon_conveyor_look import stage, shoot
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    bpy.context.scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True)
    rebuild(col)
    sc = bpy.context.scene
    clay = bpy.data.materials.get(CLAY)
    b = clay.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (.46, .36, .27, 1)
    b.inputs["Roughness"].default_value = .7
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
    sun, cam = stage(sc)
    shoot(sc, sun, cam, out, (
        ("stair_right_like_shot.png", (16.0, -21.0, 4.2), (18.0, -9.0, -4.5), 24, 210),
        ("stair_right_side.png", (31.0, -9.5, -2.0), (17.5, -9.5, -4.0), 30, 240),
        ("stair_left_side.png", (-31.0, -9.5, -2.0), (-17.5, -9.5, -4.0), 30, 120),
        ("stair_left_like_shot.png", (-16.0, -21.0, 4.2), (-18.0, -9.0, -4.5), 24, 150),
        ("stair_detail.png", (22.5, -7.0, -4.2), (18.6, -10.0, -5.6), 30, 240),
        ("both_from_front.png", (0.0, -48.0, 6.0), (0.0, -10.0, -4.0), 26, 200),
    ))


def apply():
    """R378 (승규 11-04 "Unity에 적용해"): writes both stairs, the left doors and reveals into the environment FBX.
    Unity's colliders for the two return routes: Course01v4Builder.LibraryReplacement.cs. Stops if already done."""
    from export_library_fbx import SETTINGS
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    if bpy.data.objects.get("Return_stair_R") is not None:
        print("return stairs: already in the FBX")
        return
    before = len(col.all_objects)
    rebuild(col)
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"return stairs: {before} -> {len(col.all_objects)} objects -> {FBX}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])
    else:
        apply()
