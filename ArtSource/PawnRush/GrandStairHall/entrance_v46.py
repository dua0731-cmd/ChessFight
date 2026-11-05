# R361 (승규 11-04, an asset preview - cream stone walls and a bridge arch with gold bands, pilasters with crown plaques,
# arched niches with gold candelabra, crown banners, a balustrade parapet with crown-lantern posts - beside a Unity shot
# of the entrance in the castle archway asset: "입구쪽 내가 준 에셋 지워주고 모양은 인 게임에 있는 모양으로 해주고 1번 째
# 사진처럼 모델링 해줘" + "깃발은 빨간색으로 해주고"). The entrance built again by hand in the asset preview's language,
# on the castle archway's footprint and heights as it stands in the game (measured off Entrance_archway, R312/R319):
#   arch      x +-9.75, y -12.6 .. -10, the opening x +-6.5 under a segmental arch (springing 6.6, crown 9.15),
#             entablature 9.6 .. 10.6, balustrade to 11.6 with crown-lantern posts at its corners
#   passage   walls x +-(7.6 .. 9), y -10 .. 0 along the porch, entablature 7.6 .. 8.4, balustrade above
#   wings     x +-(9 .. 20), y -2.7 .. 0 (the foyer's south wall to its side walls), three arched windows each cut
#             through as before (centres x 10.45, 13.4, 16.34, 1.92 wide, sill 2.04, round tops to 6.4)
#   base      the masonry under them, z -5 .. 0, from the floors' edges (x +-6, y 0) out to y -13.05 and x +-20,
#             in rusticated courses, its top in the floors' marble (R365)
# Stone Palace_Stone, trim Palace_Gold, banners Entrance_Red (red, as asked), plaques Entrance_Bronze, candles
# Balcony_Ivory, flames and lantern glass Balcony_Bulb. Pictures only: the blockout keeps its colliders. The old
# Entrance_archway and Entrance_base_E/W leave the hall export (to "_Blockout originals"; Unity switches their nodes
# off), the new pieces go in their own FBX (own_fbx). The warm lights' places go to EntranceV46_Lights.txt.
# Run: blender -b grand_stair_hall_v36C.blend -P entrance_v46.py
#   -> grand_stair_hall_v46C.blend, renders_v46/*.png, Assets/Resources/PawnRushGrandStairHall/EntranceV46.fbx + _Lights.txt
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from hall_kit import *   # noqa: F401,F403 (material, box, sweep, run, outline, extrude, lathe, arc, profiles, Part, crown)

TAG = "EntranceV46"

coll = bpy.data.collections.get("Entrance_v46") or bpy.data.collections.new("Entrance_v46")
if coll.name not in sc.collection.children:
    sc.collection.children.link(coll)
for o in list(coll.objects):
    bpy.data.objects.remove(o)

# ------------------------------------------------------------------ measures (Blender: x, y = Unity z, z = up)
ARCH_Y0, ARCH_Y1, PIER_X, OPEN_X = -12.6, -10.0, 9.75, 6.5
SPRING, CROWN_Z, ENT0, ENT1, ARCH_TOP = 6.6, 9.15, 9.6, 10.6, 11.6
PASS_IN, PASS_OUT, WALL_Y1 = 7.6, 9.0, 0.0
WING_Y0, WING_X1 = -2.7, 20.0
W_ENT0, W_ENT1 = 7.6, 8.4                    # the passage's and wings' entablature
WINDOWS, WIN_HALF, SILL, WIN_SPRING = (10.45, 13.4, 16.34), .96, 2.04, 5.44
FLOOR = material("Marble_Entrance", (.92, .91, .88, 1), 0, .15)
flames.name = "EntranceV46_flames"           # hall_kit's: everything that glows, one object; lights its list


# ------------------------------------------------------------------ the arch
def arch():
    p = Part("EntranceV46_arch")
    for s in (-1, 1):
        x0, x1 = sorted((s * OPEN_X, s * PIER_X))
        p.add(box(P(x0, ARCH_Y0, 0), P(x1, ARCH_Y1, ENT0), .02), STONE)
        p.add(box(P(x0 - .12, ARCH_Y0 - .12, 0), P(x1 + .12, ARCH_Y1 + .12, 1.5), .03), STONE)
        p.add(box(P(x0 - .14, ARCH_Y0 - .14, 1.5), P(x1 + .14, ARCH_Y1 + .14, 1.6)), GOLD)
        p.add(box(P(x0 - .1, ARCH_Y0 - .1, 1.6), P(x1 + .1, ARCH_Y1 + .1, 1.72), .02), STONE)
        p.add(box(P(x0 - .12, ARCH_Y0 - .12, SPRING - .36), P(x1 + .12, ARCH_Y1 + .12, SPRING - .06), .02), STONE)
        p.add(box(P(x0 - .14, ARCH_Y0 - .14, SPRING - .06), P(x1 + .14, ARCH_Y1 + .14, SPRING)), GOLD)
        for y, n in ((ARCH_Y0, -Y_), (ARCH_Y1, Y_)):
            U = X_ if n.y < 0 else -X_
            face = P(0, y, 0)
            rect = [(s * 7.05, 2.15), (s * 9.2, 2.15), (s * 9.2, 5.75), (s * 7.05, 5.75)]
            p.add(extrude(rect, face, X_, Z_, n, 0, .03), STONE)
            p.add(outline(rect, face + n * .03, X_, Z_, n, BEAD), GOLD)
            sconce(p, P(s * 8.12, y + n.y * .03, 3.75), n)
            plaque(p, P(s * 8.12, y, (SPRING + ENT0) / 2 + .1), U, n, .55, .55)
        # the outer side face
        rect = [(ARCH_Y0 + .45, 2.15), (ARCH_Y1 - .45, 2.15), (ARCH_Y1 - .45, 5.75), (ARCH_Y0 + .45, 5.75)]
        p.add(outline(rect, P(s * PIER_X, 0, 0), Y_, Z_, X_ * s, BEAD), GOLD)
    # the span over the opening: the spandrels round the segmental arch, through the arch's depth
    half, rise = OPEN_X, CROWN_Z - SPRING
    R = (half * half + rise * rise) / (2 * rise)
    cz = CROWN_Z - R
    t0, t1 = math.atan2(SPRING - cz, half), math.atan2(SPRING - cz, -half)
    curve = arc(0, cz, R, t0, t1, 48)                               # right to left over the top
    span = [(-half, SPRING), (-half, ENT0), (half, ENT0), (half, SPRING)] + curve[1:-1]
    p.add(extrude(span, P(0, ARCH_Y0, 0), X_, Z_, Y_, 0, ARCH_Y1 - ARCH_Y0), STONE)
    for y, n in ((ARCH_Y0, -Y_), (ARCH_Y1, Y_)):
        pts = [P(x, y, z) for x, z in reversed(curve)]
        ups = [Vector((x, 0, z - cz)).normalized() for x, z in reversed(curve)]
        p.add(sweep(pts, [n] * len(pts), ups, scaled(BAND, 1.2, 1.7)), STONE)
        p.add(sweep(pts, [n] * len(pts), ups, circle(.07, 0, .035, 8)), GOLD)
        p.add(sweep(pts, [n] * len(pts), ups, circle(.09, .46, .025, 8)), GOLD)
        # the keystone cartouche
        U = X_ if n.y < 0 else -X_
        p.add(extrude([(-.42, CROWN_Z - .2), (.42, CROWN_Z - .2), (.62, ENT1 - .1), (-.62, ENT1 - .1)], P(0, y, 0), X_, Z_, n, 0, .32), STONE)
        plaque(p, P(0, y + n.y * .32, (CROWN_Z + ENT1) / 2 - .05), U, n, .4, .5)
    # the soffit's gold edge lines where it meets the faces
    ring = [(-PIER_X, ARCH_Y0), (PIER_X, ARCH_Y0), (PIER_X, ARCH_Y1), (-PIER_X, ARCH_Y1)]
    outs = [-Y_, X_, Y_, -X_]
    p.add(box(P(-PIER_X, ARCH_Y0, ENT0), P(PIER_X, ARCH_Y1, ARCH_TOP - 1.0)), STONE)
    entablature(p, ring, outs, ENT0, closed=True)
    # the balustrade on top, crown-lantern posts at the corners, a crowned post in the middle
    yc = (ARCH_Y0 + ARCH_Y1) / 2
    z = ARCH_TOP - 1.0
    xs = [-PIER_X + .45, -3.0, 0, 3.0, PIER_X - .45]
    for a, b in zip(xs, xs[1:]):
        baluster_run(p, (a + .42, yc), (b - .42, yc), z)
    for x in xs:
        post(p, (x, yc), z, lantern=abs(x) > 8, h=1.25)
    crown(p, P(0, yc, z + 1.25), .55)
    return p


# ------------------------------------------------------------------ the passage walls and the wings
def walls(s):
    p = Part("EntranceV46_walls_" + ("W" if s < 0 else "E"))
    X = lambda v: s * v
    inner, outer = X_ * -s, X_ * s
    # passage wall: a face layer 0.3 deep with an arched niche in its first bay, the core behind
    p.add(box(P(X(PASS_IN + .3), ARCH_Y1, 0), P(X(PASS_OUT), WALL_Y1, W_ENT1), .02), STONE)
    ny0, ny1, nsill, nspring = -9.05, -7.1, 1.7, 4.6
    nr = (ny1 - ny0) / 2
    nyc = (ny0 + ny1) / 2
    face = P(X(PASS_IN), 0, 0)
    into = X_ * s
    p.add(box(P(X(PASS_IN), ARCH_Y1, 0), P(X(PASS_IN + .3), WALL_Y1, nsill)), STONE)
    p.add(box(P(X(PASS_IN), ARCH_Y1, nsill), P(X(PASS_IN + .3), ny0, W_ENT1)), STONE)
    p.add(box(P(X(PASS_IN), ny1, nsill), P(X(PASS_IN + .3), WALL_Y1, W_ENT1)), STONE)
    over = [(ny0, nspring), (ny0, W_ENT1), (ny1, W_ENT1), (ny1, nspring)] + arc(nyc, nspring, nr, 0, PI, 32)[1:-1]
    p.add(extrude(over, face, Y_, Z_, into, 0, .3), STONE)
    back = [(ny0 + .15, nsill + .15), (ny1 - .15, nsill + .15), (ny1 - .15, nspring)] + arc(nyc, nspring, nr - .15, 0, PI, 28)[1:-1] + [(ny0 + .15, nspring)]
    p.add(outline(back, P(X(PASS_IN + .29), 0, 0), Y_, Z_, inner, FINE), GOLD)
    path = [(ny0, nsill), (ny0, nspring)] + arc(nyc, nspring, nr, PI, 0, 32) + [(ny1, nsill)]
    pts = [face + Y_ * y + Z_ * z for y, z in path]
    ups = [-Y_, -Y_] + [Y_ * math.cos(t) + Z_ * math.sin(t) for t in [PI - PI * i / 32 for i in range(33)]] + [Y_]
    p.add(sweep(pts, [inner] * len(pts), ups, scaled(BAND, 1, .85)), STONE)
    p.add(sweep(pts, [inner] * len(pts), ups, circle(.055, 0, .03, 8)), GOLD)
    p.add(box(P(X(PASS_IN - .14), ny0 - .1, nsill - .14), P(X(PASS_IN), ny1 + .1, nsill)), STONE)
    p.add(box(P(X(PASS_IN - .15), ny0 - .11, nsill - .02), P(X(PASS_IN), ny1 + .11, nsill)), GOLD)
    sconce(p, P(X(PASS_IN + .3), nyc, 2.75), inner)
    p.add(extrude([(nyc - .3, nspring + nr - .05), (nyc + .3, nspring + nr - .05), (nyc + .4, nspring + nr + .55),
                   (nyc - .4, nspring + nr + .55)], face, Y_, Z_, inner, 0, .2), STONE)
    # pilasters, a red banner in the middle bay, a gold dado band, the base moulding
    U_in = Y_ * (1 if s < 0 else -1)
    for y0, y1 in ((-9.95, -9.3), (-6.85, -6.2), (-3.4, -2.75), (-.65, 0)):
        u0, u1 = sorted((y0 * U_in.y, y1 * U_in.y))
        pilaster(p, P(X(PASS_IN), 0, 0), U_in, inner, u0, u1, 0, W_ENT0)
    banner(p, P(X(PASS_IN), -4.8, 6.6), inner, .62, 3.4)
    sconce(p, P(X(PASS_IN), -1.7, 3.4), inner)
    p.add(run([(X(PASS_IN), ARCH_Y1), (X(PASS_IN), WALL_Y1)], [inner], 0, scaled(BASE, 1, 1.2)), STONE)
    p.add(run([(X(PASS_IN), ARCH_Y1), (X(PASS_IN), WALL_Y1)], [inner], 1.32, FINE), GOLD)
    # the wing: the wall with its three windows through it
    wx0 = PASS_OUT
    edges = [wx0] + [e for c in WINDOWS for e in (c - WIN_HALF, c + WIN_HALF)] + [WING_X1]
    p.add(box(P(X(wx0), WING_Y0, 0), P(X(WING_X1), WALL_Y1, SILL), .02), STONE)
    for a, b in zip(edges[0::2], edges[1::2]):
        p.add(box(P(X(a), WING_Y0, SILL), P(X(b), WALL_Y1, W_ENT1), .02), STONE)
    for c in WINDOWS:
        over = [(c - WIN_HALF, WIN_SPRING), (c - WIN_HALF, W_ENT1), (c + WIN_HALF, W_ENT1), (c + WIN_HALF, WIN_SPRING)] \
            + arc(c, WIN_SPRING, WIN_HALF, 0, PI, 24)[1:-1]
        p.add(extrude([(s * u, v) for u, v in over], P(0, WING_Y0, 0), X_, Z_, Y_, 0, WALL_Y1 - WING_Y0), STONE)
        for y, n in ((WING_Y0, -Y_), (WALL_Y1, Y_)):
            path = [(c - WIN_HALF, SILL), (c - WIN_HALF, WIN_SPRING)] + arc(c, WIN_SPRING, WIN_HALF, PI, 0, 24) + [(c + WIN_HALF, SILL)]
            pts = [P(X(u), y, v) for u, v in path]
            ups = [-X_ * s, -X_ * s] + [(X_ * s * math.cos(t) + Z_ * math.sin(t)) for t in [PI - PI * i / 24 for i in range(25)]] + [X_ * s]
            p.add(sweep(pts, [n] * len(pts), ups, scaled(BAND, 1, .8)), STONE)
            p.add(sweep(pts, [n] * len(pts), ups, circle(.055, 0, .028, 8)), GOLD)
            p.add(box(P(X(c - WIN_HALF - .2), y, SILL - .16), P(X(c + WIN_HALF + .2), y + n.y * .16, SILL)), STONE)
            p.add(box(P(X(c - WIN_HALF - .21), y, SILL - .02), P(X(c + WIN_HALF + .21), y + n.y * .17, SILL)), GOLD)
            p.add(extrude([(X(c) - .25, WIN_SPRING + WIN_HALF - .05), (X(c) + .25, WIN_SPRING + WIN_HALF - .05),
                           (X(c) + .34, WIN_SPRING + WIN_HALF + .5), (X(c) - .34, WIN_SPRING + WIN_HALF + .5)],
                          P(0, y, 0), X_, Z_, n, 0, .18), STONE)
    for y, n in ((WING_Y0, -Y_), (WALL_Y1, Y_)):
        U = X_ if n.y < 0 else -X_
        for x0, x1 in ((11.63, 12.23), (14.57, 15.17), (17.55, 18.15)):
            u0, u1 = sorted((s * x0 * U.x, s * x1 * U.x))
            pilaster(p, P(0, y, 0), U, n, u0, u1, 0, W_ENT0)
        banner(p, P(X(19.05), y, 6.6), n, .58, 3.3)
        p.add(run([(X(wx0 if n.y < 0 else PASS_IN), y), (X(WING_X1), y)], [n], 0, scaled(BASE, 1, 1.2)), STONE)
        p.add(run([(X(wx0 if n.y < 0 else PASS_IN), y), (X(WING_X1), y)], [n], 1.32, FINE), GOLD)
    # the outer face of the passage wall
    for y0, y1 in ((-9.6, -8.95), (-6.3, -5.65)):
        u0, u1 = sorted((y0 * -U_in.y, y1 * -U_in.y))
        pilaster(p, P(X(PASS_OUT), 0, 0), -U_in, outer, u0, u1, 0, W_ENT0)
    banner(p, P(X(PASS_OUT), -4.6, 6.6), outer, .55, 3.0)
    p.add(run([(X(PASS_OUT), ARCH_Y1), (X(PASS_OUT), WING_Y0)], [outer], 0, scaled(BASE, 1, 1.2)), STONE)
    # entablature round the inside (porch and foyer faces) and the outside
    entablature(p, [(X(PASS_IN), ARCH_Y1), (X(PASS_IN), WALL_Y1), (X(WING_X1), WALL_Y1)], [inner, Y_], W_ENT0)
    entablature(p, [(X(PASS_OUT), ARCH_Y1), (X(PASS_OUT), WING_Y0), (X(WING_X1), WING_Y0)], [outer, -Y_], W_ENT0)
    # the balustrade on top: along the passage, round the corner, along the wing; lantern posts at the corner and end
    xc, yc = X((PASS_IN + PASS_OUT) / 2), (WING_Y0 + WALL_Y1) / 2
    run_pts = [(xc, ARCH_Y1 + .45), (xc, -5.6), (xc, yc), (X(14.87), yc), (X(WING_X1 - .45), yc)]
    for (a, b) in zip(run_pts, run_pts[1:]):
        av, bv = Vector(a), Vector(b)
        d = (bv - av).normalized()
        baluster_run(p, tuple(av + d * .42), tuple(bv - d * .42), W_ENT1)
    for k, q in enumerate(run_pts):
        post(p, q, W_ENT1, lantern=k in (2, 4), h=1.15)
    return p


# ------------------------------------------------------------------ the masonry under it all
def base(s):
    """The masonry under the walls, from the porch floor's edge (x +-6) and the foyer floor's (y 0) outwards, up to the
    walked top (0). R365 (승규 11-04, a recording of the floor flickering beside the wing: "움직이면 깨져 … 축 부분
    안깨지게 다시 제대로 맞춰"): R358 raised the floors' boxes to 0, where this masonry's top lay too and overlapped them
    (x 5.75 .. 6 along the porch, y 0 .. 0.25 along the foyer, x 20 .. 20.75 under the side wall) - two surfaces in one
    plane, fighting. It now stops at those edges, and its top is the floors' marble (Marble_Entrance, Unity's
    HallMarble) so the strip between the porch floor and the passage walls (x 6 .. 7.6) is floor, not stone."""
    p = Part("EntranceV46_base_" + ("W" if s < 0 else "E"))
    X = lambda v: s * v
    lo = lambda a, b: (min(X(a), X(b)), max(X(a), X(b)))
    out = "+x" if s > 0 else "-x"
    x0, x1 = lo(6.0, 10.1);  masonry(p, x0, x1, -13.05, -9.75, -5, -.3, ["-y", out])
    x0, x1 = lo(6.0, 9.25);  masonry(p, x0, x1, -9.75, -2.95, -5, -.3, [out])
    x0, x1 = lo(6.0, 20.0);  masonry(p, x0, x1, -2.95, 0., -5, -.3, ["-y"])
    edge = [(X(6.0), -13.05), (X(10.1), -13.05), (X(10.1), -9.75), (X(9.25), -9.75), (X(9.25), -2.95), (X(20.0), -2.95)]
    segs = []
    for a, b in zip(edge, edge[1:]):
        d = Vector((b[0] - a[0], b[1] - a[1], 0)).normalized()
        n = Vector((d.y, -d.x, 0)) * (1 if s > 0 else -1)
        segs.append(n)
    p.add(run(edge, segs, -.3, [(-.02, 0), (.12, 0), (.12, .12), (.16, .16), (.16, .3), (-.02, .3)]), STONE)
    p.add(run(edge, segs, -.26, scaled(FINE, 1.5, 1.5)), GOLD)
    for (a, b), (y0, y1) in (((6.0, 10.1), (-13.05, -9.75)), ((6.0, 9.25), (-9.75, -2.95)), ((6.0, 20.0), (-2.95, 0.))):
        x0, x1 = lo(a, b)
        p.add(box(P(x0, y0, -.3), P(x1, y1, 0)), FLOOR)          # the top course: floor where it shows
    return p


# ------------------------------------------------------------------ build
parts = [arch()]
for s in (-1, 1):
    parts.append(walls(s))
    parts.append(base(s))
objs = [p.done(coll, TAG) for p in parts]
objs.append(flames.done(coll, TAG))

# The old entrance out of the hall export (and out of these renders).
retired = bpy.data.collections["_Blockout originals"]
for name in ("Entrance_archway", "Entrance_base_E", "Entrance_base_W"):
    o = bpy.data.objects.get(name)
    if o is None:
        continue
    for c in list(o.users_collection):
        c.objects.unlink(o)
    retired.objects.link(o)
    o.hide_render = o.hide_viewport = True

# ------------------------------------------------------------------ renders
sc.render.engine = "BLENDER_WORKBENCH"
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
sh = sc.display.shading
sh.light, sh.color_type = "STUDIO", "MATERIAL"
sh.show_cavity, sh.show_shadows = True, True
for c in ("Royal_balcony_v34 previews",):
    if c in bpy.data.collections:
        for o in bpy.data.collections[c].objects:
            o.hide_render = True
out = os.path.join(HERE, "renders_v46")
os.makedirs(out, exist_ok=True)
for name, loc, at, lens in (("out", (16, -36, 14), (0, -6, 6), 24), ("in", (-3, 12, 7.5), (0, -8, 6), 20),
                            ("side", (-32, -22, 17), (-6, -4, 5), 24), ("porch", (3, -20, 4.5), (0, -2, 6), 18),
                            ("wing_in", (8, 10, 6), (-14, -1, 5), 22)):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_end = 2000
    o = bpy.data.objects.new("Cam46_" + name, cd); sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = o
    sc.render.filepath = os.path.join(out, "new_" + name + ".png")
    bpy.ops.render.render(write_still=True)
sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera

# ------------------------------------------------------------------ export (world frame, as the hall's)
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
for o in sc.objects:
    o.select_set(False)
for o in objs:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "EntranceV46.fbx"), **FBX)
with open(os.path.join(RES, "EntranceV46_Lights.txt"), "w", encoding="utf8") as f:
    f.write("# kind x y z in Blender's frame (Unity hall: x, z, y)\n")
    for k, x, y, z in lights:
        f.write("%s %.3f %.3f %.3f\n" % (k, x, y, z))
print("ENTRANCE", [(o.name, len(o.data.polygons)) for o in objs], "lights", len(lights))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v46C.blend"))
