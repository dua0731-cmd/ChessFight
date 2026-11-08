# R362 (승규 11-04, a Unity shot of the foyer's side wall - the architectural wall's window bay with gold bars - after
# R361's entrance: "옆쪽도 교체해줘 창문 막혀 있는 걸로"). The foyer's two side walls (x +-20 .. +-21, y -2.75 .. 14,
# -5 .. 12: foyer_sides_v20.py's architectural wall) built again in the entrance's language (hall_kit.py), the windows
# blind: arched recesses in the stone, each with a gold candelabrum (as the asset preview's niches), framed by an
# archivolt, a gold bead, a keystone and a sill. Pilasters with crown plaques, a gold dado line, the entablature round
# the outer face and the south end, a lantern-posted balustrade on top, rusticated masonry under the foyer's floor.
# The outer face's last 4 m stand inside the hall's south wall (Outer_wall_S_*), so its bays stop short of it.
# Pictures only: the blockout keeps its colliders. Foyer_side_E/W and their tracery leave the hall export (Unity
# switches their nodes off); the new walls go in their own FBX (own_fbx), their lights' places in a text file.
# Run: blender -b grand_stair_hall_v46C.blend -P foyer_sides_v47.py
#   -> grand_stair_hall_v47C.blend, renders_v47/*.png, Assets/Resources/PawnRushGrandStairHall/FoyerSidesV47.fbx + _Lights.txt
import bpy, bmesh, math, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from hall_kit import *   # noqa: F401,F403

TAG = "FoyerSidesV47"
coll = bpy.data.collections.get("Foyer_sides_v47") or bpy.data.collections.new("Foyer_sides_v47")
if coll.name not in sc.collection.children:
    sc.collection.children.link(coll)
for o in list(coll.objects):
    bpy.data.objects.remove(o)
flames.name = "FoyerSidesV47_flames"

X_IN, X_OUT, Y0, Y1 = 20.0, 21.0, -2.75, 14.0
SKIN = .22                                       # the face layers the recesses are cut in
ENT0, ENT1 = 10.1, 10.9                          # entablature; the balustrade above it to about 12
HALF, SILL, SPRING = .95, 2.3, 6.4               # the blind windows: 1.9 wide, round tops to 7.35


def face(p, s, outer, windows, pilasters):
    """One face of the wall (x = s 21 outer, s 20 inner): its skin with blind arched recesses, their surrounds and
    candelabra, the pilasters, the base moulding and the dado line."""
    xf = s * (X_OUT if outer else X_IN)
    n = X_ * s if outer else X_ * -s                 # out of the face
    into = -n
    yA, yB = Y0, Y1
    # the skin, cut round the recesses
    edges = [yA] + [e for c in windows for e in (c - HALF, c + HALF)] + [yB]
    x0, x1 = sorted((xf, xf + into.x * SKIN))
    p.add(box(P(x0, yA, 0), P(x1, yB, SILL)), STONE)
    for a, b in zip(edges[0::2], edges[1::2]):
        p.add(box(P(x0, a, SILL), P(x1, b, ENT1)), STONE)
    for c in windows:
        over = [(c - HALF, SPRING), (c - HALF, ENT1), (c + HALF, ENT1), (c + HALF, SPRING)] + arc(c, SPRING, HALF, 0, PI, 24)[1:-1]
        p.add(extrude(over, P(xf, 0, 0), Y_, Z_, into, 0, SKIN), STONE)
        # inside the recess: a gold line round its back, a candelabrum
        back = [(c - HALF + .14, SILL + .14), (c + HALF - .14, SILL + .14), (c + HALF - .14, SPRING)] \
            + arc(c, SPRING, HALF - .14, 0, PI, 20)[1:-1] + [(c - HALF + .14, SPRING)]
        p.add(outline(back, P(xf, 0, 0) + into * (SKIN - .01), Y_, Z_, n, FINE), GOLD)
        sconce(p, P(xf, c, 3.6) + into * SKIN, n)
        # the surround: archivolt with a gold bead, keystone, sill
        path = [(c - HALF, SILL), (c - HALF, SPRING)] + arc(c, SPRING, HALF, PI, 0, 24) + [(c + HALF, SILL)]
        pts = [P(xf, y, z) for y, z in path]
        ups = [-Y_, -Y_] + [Y_ * math.cos(t) + Z_ * math.sin(t) for t in [PI - PI * i / 24 for i in range(25)]] + [Y_]
        p.add(sweep(pts, [n] * len(pts), ups, scaled(BAND, 1, .85)), STONE)
        p.add(sweep(pts, [n] * len(pts), ups, circle(.055, 0, .028, 8)), GOLD)
        p.add(extrude([(c - .26, SPRING + HALF - .05), (c + .26, SPRING + HALF - .05), (c + .36, SPRING + HALF + .55),
                       (c - .36, SPRING + HALF + .55)], P(xf, 0, 0), Y_, Z_, n, 0, .2), STONE)
        sill_lo, sill_hi = sorted((xf, xf + n.x * .17))
        p.add(box(P(sill_lo, c - HALF - .22, SILL - .16), P(sill_hi, c + HALF + .22, SILL)), STONE)
        p.add(box(P(min(sill_lo, xf + n.x * .18), c - HALF - .23, SILL - .02), P(max(sill_hi, xf + n.x * .18), c + HALF + .23, SILL)), GOLD)
    U = Y_ if n.x < 0 else -Y_                       # left to right seen from outside
    for y0, y1 in pilasters:
        u0, u1 = sorted((y0 * U.y, y1 * U.y))
        pilaster(p, P(xf, 0, 0), U, n, u0, u1, 0, ENT0)
    p.add(run([(xf, yA), (xf, yB)], [n], 0, scaled(BASE, 1, 1.2)), STONE)
    p.add(run([(xf, yA), (xf, yB)], [n], 1.32, FINE), GOLD)


def side(s):
    p = Part("FoyerSidesV47_wall_" + ("W" if s < 0 else "E"))
    xa, xb = sorted((s * (X_IN + SKIN), s * (X_OUT - SKIN)))
    p.add(box(P(xa, Y0, 0), P(xb, Y1, ENT1)), STONE)                       # the core between the two skins
    # outer face: two blind windows, three pilasters, its last 4 m inside the hall's south wall
    face(p, s, True, (.45, 6.12), ((-2.75, -2.1), (3.0, 3.65), (8.6, 9.25)))
    # inner face (the foyer's side): two blind windows, three pilasters; south of y 0 it rises over the wing
    face(p, s, False, (3.58, 10.25), ((0.0, .65), (6.5, 7.15), (13.35, 14.0)))
    # the south end, entablature round the outer face and the end, along the inner face
    xo, xi = s * X_OUT, s * X_IN
    entablature(p, [(xi, Y0), (xo, Y0), (xo, Y1)], [-Y_, X_ * s], ENT0)
    entablature(p, [(xi, Y0), (xi, Y1)], [X_ * -s], ENT0)
    # balustrade with lantern posts at both ends and a post in the middle
    xc = s * (X_IN + X_OUT) / 2
    ys = [Y0 + .45, 5.6, Y1 - .45]
    for a, b in zip(ys, ys[1:]):
        baluster_run(p, (xc, a + .42), (xc, b - .42), ENT1)
    for k, y in enumerate(ys):
        post(p, (xc, y), ENT1, lantern=k != 1, h=1.15)
    return p


def base(s):
    p = Part("FoyerSidesV47_base_" + ("W" if s < 0 else "E"))
    x0, x1 = sorted((s * X_IN, s * X_OUT))
    out = "+x" if s > 0 else "-x"
    masonry(p, x0, x1, Y0, Y1, -5, -.3, [out, "-y"])
    p.add(box(P(x0, Y0, -.3), P(x1, Y1, 0)), STONE)
    edge = [(s * X_IN, Y0), (s * X_OUT, Y0), (s * X_OUT, Y1)]
    outs = [-Y_, X_ * s]
    p.add(run(edge, outs, -.3, [(-.02, 0), (.12, 0), (.12, .12), (.16, .16), (.16, .3), (-.02, .3)]), STONE)
    p.add(run(edge, outs, -.26, scaled(FINE, 1.5, 1.5)), GOLD)
    return p


parts = []
for s in (-1, 1):
    parts += [side(s), base(s)]
objs = [p.done(coll, TAG) for p in parts]
objs.append(flames.done(coll, TAG))

retired = bpy.data.collections["_Blockout originals"]
for name in ("Foyer_side_E", "Foyer_side_W", "Foyer_side_E tracery", "Foyer_side_W tracery"):
    o = bpy.data.objects.get(name)
    if o is None:
        print("MISSING", name); continue
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
out = os.path.join(HERE, "renders_v47")
os.makedirs(out, exist_ok=True)
for name, loc, at, lens in (("outside", (40, -10, 9), (21, 4, 5), 24), ("outside_low", (32, -16, -2), (21, 3, 6), 22),
                            ("inside", (3, 2, 6), (20, 8, 5), 22), ("corner", (34, -26, 16), (14, -2, 6), 24)):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_end = 2000
    o = bpy.data.objects.new("Cam47_" + name, cd); sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = o
    sc.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera

# ------------------------------------------------------------------ export
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
for o in sc.objects:
    o.select_set(False)
for o in objs:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "FoyerSidesV47.fbx"), **FBX)
with open(os.path.join(RES, "FoyerSidesV47_Lights.txt"), "w", encoding="utf8") as f:
    f.write("# kind x y z in Blender's frame (Unity hall: x, z, y)\n")
    for k, x, y, z in lights:
        f.write("%s %.3f %.3f %.3f\n" % (k, x, y, z))
print("SIDES", [(o.name, len(o.data.polygons)) for o in objs], "lights", len(lights))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v47C.blend"))
