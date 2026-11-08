# J Palace Laundry room (R509). The user (11-08, relayed by the H session): reuse the other mini-games' models where
# they fit. J's section is the same as E's (MiniGameSectionBuilder), so the room is E's (Mid/e_hall_v1.py: the user's
# R507 wall and gate sets, ashlar fill, walnut ceiling) with two laundry changes:
#   - no low grey-book bookcases in front of the side walls (a library's);
#   - a white glazed tile dado (R509 PR_Mid_WhiteTile) with a walnut cap rail over the walls' walnut wainscot, as the
#     concept's washhouse (Docs/PawnRush/ConceptArt/R509_PalaceLaundry/J_01_overview.png).
# And the basin (J_BasinEnv, in the station's game frame: x = v, y = u, z up, white team's side; black mirrors it):
# white tile on the game's basin walls and floor (MgKnightPads: v +-10, u 2.1..19.3, floor -1.8, sides up to -0.4,
# south up to the dais top 0, the gate strip's face up to 0) and a tile coping along the aisle side and the north edge.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python j_hall_v1.py -- [--render] [--export]
#
# e_hall_v1.py is imported and run as it is (not edited). Its main() saves Mid/E_Hall_v1.blend: that file is backed up
# first and put back afterwards, so E's work file stays as it was.
import bpy, bmesh, math, os, sys, shutil
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
MID = os.path.dirname(HERE)
sys.path.insert(0, MID)
import e_hall_v1 as H   # noqa: E402
E = H.E
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

TILE_TOP = 1.5       # white tile dado height: under the ground windows (their sill at 1.8 m)
CAP = (.12, .10)     # walnut cap rail: height, depth


def tile_boxes(specs, mat, name, tile=1., uv_name="UVMap"):
    """Axis boxes (x0, x1, y0, y1, z0, z1), box UVs on a `tile` m repeat (the tile swatch is 1 m = 4 x 4 tiles)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new(uv_name)        # joined meshes keep UVs only on a layer of the same name
    for x0, x1, y0, y1, z0, z1 in specs:
        if x1 - x0 < .005 or y1 - y0 < .005 or z1 - z0 < .005:
            continue
        r = bmesh.ops.create_cube(bm, size=1.)
        for v in r["verts"]:
            v.co = Vector((x0 if v.co.x < 0 else x1, y0 if v.co.y < 0 else y1, z0 if v.co.z < 0 else z1))
        for f in {f for v in r["verts"] for f in v.link_faces}:
            n = f.normal
            for l in f.loops:
                p = l.vert.co
                l[uv].uv = (p.x / tile, p.y / tile) if abs(n.z) > .5 else ((p.y / tile, p.z / tile) if abs(n.x) > .5 else (p.x / tile, p.z / tile))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def tex_mat(name, stem):
    m = bpy.data.materials.get(name)
    if m:
        return m
    return H.material(name, os.path.join(E.TEX, stem + "_BaseColor.png"))


def build_hall():
    blend = os.path.join(MID, "E_Hall_v1.blend")
    saved = {}
    for f in (blend, blend + "1"):
        if os.path.exists(f):
            saved[f] = f + ".r509keep"
            shutil.copyfile(f, saved[f])
    H.ARGS = []                                     # build only: no E export, no E render
    place = E.place_copy
    E.place_copy = lambda src, name, M: None if name == "shelf" else place(src, name, M)
    join = E.join
    E.join = lambda objs, name: join([o for o in objs if o is not None], name)
    try:
        H.main()
    finally:
        E.place_copy, E.join = place, join
        for f, keep in saved.items():
            shutil.move(keep, f)
    hall = bpy.data.objects["E_HallEnv"]
    hall.name = hall.data.name = "J_HallEnv"
    return hall


def tile_dado(hall):
    """White tile in front of the walls' lower part, a walnut cap rail on top (room-side faces only)."""
    tile = tex_mat("PR_Mid_WhiteTile", "PR_Mid_WhiteTile")
    walnut = tex_mat("PR_Mid_WalnutFloor", "PR_Mid_WalnutFloor")
    f, t = H.FACE, .05
    tiles, caps = [], []
    for sd in (-1, 1):
        # side walls of the main room (z 0..25) and the rear corridor (z 30..36): in front of the module faces
        for z0, z1 in ((-.05, H.NORTH - H.MOD_D - .02), (29.95, 35.95)):
            a, b = sorted((sd * (f - t), sd * f))
            tiles.append((a, b, z0, z1, 0., TILE_TOP))
            a, b = sorted((sd * (f - CAP[1]), sd * f))
            caps.append((a, b, z0, z1, TILE_TOP, TILE_TOP + CAP[0]))
        # north wall outer span (|x| 14.22..22): in front of the plain modules (front face about z 24.38)
        zf = H.NORTH - H.MOD_D - .03
        a, b = sorted((sd * (H.TUNNEL[1] + .25), sd * f))
        tiles.append((a, b, zf - t, zf, 0., TILE_TOP))
        caps.append((a, b, zf - CAP[1], zf, TILE_TOP, TILE_TOP + CAP[0]))
        # entry wall wings (|x| 7.6..22): room face +z about 0.57
        ze = -.05 + H.MOD_D + .03
        a, b = sorted((sd * (H.OPEN_HALF + 1.4), sd * f))
        tiles.append((a, b, ze, ze + t, 0., TILE_TOP))
        caps.append((a, b, ze, ze + CAP[1], TILE_TOP, TILE_TOP + CAP[0]))
    uv_name = hall.data.uv_layers.active.name
    t_obj = tile_boxes(tiles, tile, "tile dado", 1., uv_name)
    c_obj = tile_boxes(caps, walnut, "cap rail", 2., uv_name)
    hall = E.join([hall, t_obj, c_obj] + gap_fills(uv_name), "J_HallEnv")
    # e_hall_v1 makes some materials twice ("PR_Mid_WalnutFloor.001"): Unity finds kit materials by name, so every
    # duplicate slot goes back to its first material
    mats = hall.data.materials
    for i, m in enumerate(mats):
        if m is not None and "." in m.name:
            base = bpy.data.materials.get(m.name.split(".")[0])
            if base is not None:
                mats[i] = base
    print("HALL materials", sorted({m.name for m in mats if m}))
    return hall


def gap_fills(uv_name):
    """User 11-08 (two photos at the doors), "출구 쪽 문 빈틈 없이 채워": the sky showed through two holes in the floor.
      - the team-gate tunnels: the game's "Gate passage" floor is 4 m wide (x +-10..14, z 24..30) but the tunnel walls
        stand at |x| 9.78 and 14.22, so a 0.22 m slot ran along both walls the whole tunnel -> slate strips there;
      - the centre gate: its door is set back to z ~24.2 while the central aisle floor stops at z 24, and its bottom is
        a few cm off the floor, ragged -> the aisle's chequer carried on under it to the wall (z 25), and a 0.25 m stone
        threshold under the door (x +-2.6) that the door's ragged foot stands into.
    Tops 2 mm under the game floors they meet (no shared plane)."""
    slate = tex_mat("PR_Mid_SlateFloor", "PR_Mid_SlateFloor")
    chequer = tex_mat("PR_Mid_CheckerMarble", "PR_Mid_CheckerMarble")
    t0, t1 = H.TUNNEL[0], H.TUNNEL[1]
    strips = []
    for sd in (-1, 1):
        for a, b in ((t0 - .03, 10.03), (13.97, t1 + .03)):
            x0, x1 = sorted((sd * a, sd * b))
            strips.append((x0, x1, 23.97, 30.03, -.6, -.002))
    out = [tile_boxes(strips, slate, "tunnel floor edges", 2., uv_name)]
    out.append(tile_boxes([(-2.02, 2.02, 23.97, H.NORTH + .02, -.6, -.002)], chequer, "centre gate floor", 2., uv_name))
    out.append(tile_boxes([(-2.6, 2.6, 24.0, H.NORTH - .02, 0., .25)], slate, "centre gate threshold", 1., uv_name))
    # the door leaves' Tripo surface is torn low on the right (the wall behind showed through): a walnut board just
    # behind the doors (their face is at z ~24.2) so any tear shows dark wood
    walnut = tex_mat("PR_Mid_WalnutFloor", "PR_Mid_WalnutFloor")
    out.append(tile_boxes([(-2.6, 2.6, 24.42, 24.52, .2, 3.2)], walnut, "centre door backing", 2., uv_name))
    return out


def basin():
    """J_BasinEnv in the white station's game frame (x = v, y = u, z up; y 0 = the dais top)."""
    tile = tex_mat("PR_Mid_WhiteTile", "PR_Mid_WhiteTile")
    S, N, B, T = 2.1, 19.3, -1.8, .03          # MgKnightPads South, North, Bottom; tile thickness
    side, strip = -.4, (-3.2, 4.4)            # the side walls' top (-DaisHeight); the gate strip's v span (white)
    specs = [
        (-9.9, 9.9, S, S + T, B, -.004),                       # south wall face, up under the dais edge
        (-9.9 + T, 9.9 - T, S + T, N - T, B, B + .012),       # floor
        (-9.9, -9.9 + T, S + T, N - T, B, side),               # west (the room's side wall)
        (9.9 - T, 9.9, S + T, N - T, B, side),                 # east (the central aisle)
    ]
    # north wall: up to -0.4, and up to the strip's top 0 where the gate strip stands behind it
    for a, b, top in ((-9.9, strip[0], side), (strip[0], strip[1], -.004), (strip[1], 9.9, side)):
        specs.append((a, b, N - T, N, B, top))
    # coping: a rounded-looking lip along the aisle side and the north edge, a few cm over the room floor
    lip = [(9.82, 10.04, S, N + .02, side, side + .05)]
    for a, b in ((-9.9, strip[0]), (strip[1], 9.9)):
        lip.append((a, b, N - .08, N + .14, side, side + .05))
    o = tile_boxes(specs + lip, tile, "J_BasinEnv")
    return o


def render(hall, bas):
    """The room with both basins (white at x -12, black mirrored at x +12; game frame at section y 4, z 0.4)."""
    for x, m in ((-12., 1.), (12., -1.)):
        c = E.place_copy(bas, "basin", Matrix.Translation((x, 4., .4)) @ Matrix.Diagonal((m, 1, 1, 1)))
        if m < 0:
            c.data.flip_normals()
    bas.hide_render = True
    tub = os.path.join(MID, "Export", "J_Tub.fbx")
    if os.path.exists(tub):
        bpy.ops.import_scene.fbx(filepath=tub)
        t = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
        t.data.transform(t.matrix_world)
        t.matrix_world.identity()
        t.data.materials.clear()
        t.data.materials.append(tex_mat("PR_Mid_J_TubSluice", "PR_Mid_J_TubSluice"))
        sq = [(-1.125, 3.125), (-5.625, 5.375), (-7.875, 9.875), (-3.375, 12.125), (1.125, 9.875), (5.625, 12.125), (7.875, 16.625), (3.375, 18.875)]
        for x, m in ((-12., 1.), (12., -1.)):
            for v, u in sq:
                E.place_copy(t, "tub", Matrix.Translation((x + m * v, 4. + u, .4)))
        t.hide_render = True
    mw = bpy.data.materials.new("water")
    mw.use_nodes = True
    b = mw.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (.55, .8, .8, 1)
    b.inputs["Alpha"].default_value = .55
    for x in (-12., 12.):
        bpy.ops.mesh.primitive_plane_add(size=1, location=(x, 4. + 10.7, .4 - .6), scale=(19.8, 17.2, 1))
        bpy.context.object.data.materials.append(mw)
    mf = bpy.data.materials.new("floor")
    mf.use_nodes = True
    mf.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.22, .25, .3, 1)
    for x0, x1, y0, y1, z in ((-2, 2, 0, 24, 0.), (-22, 22, 0, 4, 0.), (-22, -2, 4, 6.1, .4), (2, 22, 4, 6.1, .4),
                              (-22, 22, 23.3, 25, .4), (-22, 22, 30, 36, 0.)):
        bpy.ops.mesh.primitive_cube_add(location=((x0 + x1) / 2, (y0 + y1) / 2, z - .25), scale=((x1 - x0) / 2, (y1 - y0) / 2, .25))
        bpy.context.object.data.materials.append(mf)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.75, .8, .88, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .9
    sc.world = w
    for name, loc, aim in (("overview", (0, 1.5, 11), (0, 20, 0)), ("basin", (-4, 1, 6), (-14, 14, -1)),
                           ("north", (8, 8, 4), (14, 25, 2)), ("side", (-8, 6, 2.5), (-22, 14, 1.5))):
        bpy.ops.object.light_add(type='SUN', rotation=(math.radians(55), 0, math.radians(-20)))
        bpy.context.object.data.energy = 2.5
        cd = bpy.data.cameras.new(name)
        cd.lens = 22
        cam = bpy.data.objects.new(name, cd)
        sc.collection.objects.link(cam)
        cam.location = loc
        cam.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.camera = cam
        sc.render.filepath = os.path.join(HERE, "render_j_hall_%s.png" % name)
        bpy.ops.render.render(write_still=True)


def main():
    hall = tile_dado(build_hall())
    print("HALL tris", E.tris(hall))
    bas = basin()
    print("BASIN tris", E.tris(bas))
    if "--export" in ARGS:
        E.export(hall, "J_HallEnv")
        E.export(bas, "J_BasinEnv")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(MID, "J_Hall_v1.blend"))
    if "--render" in ARGS:
        render(hall, bas)


main()
