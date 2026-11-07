# B East Wing hall dressing (R503): the user's two Tripo sets from the GPT sheets B_assets_04_teamgate / 05_hall
# (Docs/PawnRush/ConceptArt/R503_RepairWing/AssetRefs), fitted to the section.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python b_hall_v1.py -- [--render] [--export]
#
# Sources (Source/, 1 m wide, z up):
#   R503_RusticProps:          team arch frame (stone round arch, timber posts and cross beam), canvas panel (unused:
#                              hanging cloth is made in code, it must sway), rolled dust sheet on a batten, step stool
#   R503_FantasyArchitectural: iron chandelier (eight EMPTY cups), wall sconce (EMPTY lantern cage), walkway tile with
#                              brass strips, marble balustrade, marble steps (unused: the entry has no height change)
# Light and flame are never in a model (user rule, 2026-11-07): empties named SOCKET_Flame / SOCKET_Light mark where
# MiniGameModels.LightSockets adds the flames and the lights.
#
# Outputs (Export/):
#   B_TeamArch.fbx  one team arch, origin = floor, centre of the opening, front face; Blender -y faces the players.
#                   Uniform scale: the timber posts stand 4.0 m apart (the game's team gate is 4 m wide), so the arch
#                   is 7.9 m wide; hung at x +-12, z 23.8 (the shell's portal front). The centre gate was narrowed to
#                   16 m for it (b_center_gate_v1.py).
#   B_HallEnv.fbx   section frame (Blender x = section x, y = section z, z = up), symmetric in x:
#                   five walkway tiles on the central aisle (x -2..2, z 4..24), balustrades on the side-wall modules
#                   (a gallery at 4.85 m), four chandeliers, eight sconces, a step stool and a rolled sheet by each arch.
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import b_core_scene_v1 as cs  # noqa: E402  (split, decimate, fit helpers, sockets, export)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

GATE_CLEAR = 4.0                     # timber posts' inner clear width = the team gate
ARCH_AT = 23.8                       # portal front (MiniGamePalaceArt.Shell)
CHANDELIERS = [(-12., 2.), (12., 2.), (-12., 21.), (12., 21.)]
CHANDELIER_TOP, CHANDELIER_H = 12.8, 2.6
SCONCES = [(-16.6, 24.05, 0.), (16.6, 24.05, 0.)] + \
          [(s * 20.9, z, s) for s in (-1., 1.) for z in (5., 11., 17.)]   # (x, z, wall side; 0 = north facade)
SCONCE_Y, SCONCE_H = 3.6, 1.0
WALL_X, MODULE_TOP = 22.2, 5.3        # R503 upper floor: on the gallery deck (b_upper_v1.py), not the module tops
BALUSTRADE_X, BALUSTRADE_H = 20.75, 1.0   # the gallery deck front edge (x +-20.6)
GALLERY_Z = [(-.95, 23.05), (30.05, 35.95)]   # the gallery decks (b_upper_v1.py: z -1..23 and 30..36)


def rustic_rule(c):
    if c.y > .19: return "arch"
    if c.y > -.02: return None                       # the canvas panel: cloth is code
    if c.y > -.295: return "roll"
    return "stool"


def fantasy_rule(c):
    if c.x < -.27: return "chandelier"
    if c.x < -.11: return "sconce"
    # the tile (x -0.09..0.10, y -0.03..0.19) and the balustrade (x 0.04..0.27, y -0.19..-0.05) overlap in x: split by
    # y too, or the tile's right brass strip ends up in the balustrade (a gold bar under every railing section)
    if c.x < .11 and c.y > -.04: return "tile"
    if c.x < .273: return "balustrade"
    return None                                      # steps


def min_rect_align(o):
    """Turn about z to the angle with the smallest footprint box (squares: PCA has no long axis)."""
    p = cs.coords(o)[:, :2]; best = None
    for a in np.radians(np.arange(0, 90, .5)):
        c, s = math.cos(a), math.sin(a)
        q = p @ np.array([[c, -s], [s, c]])
        area = np.ptp(q[:, 0]) * np.ptp(q[:, 1])
        if best is None or area < best[0]: best = (area, a)
    cs.xform(o, Matrix.Rotation(best[1], 4, 'Z'))
    p = cs.coords(o); lo, hi = p.min(0), p.max(0)
    cs.xform(o, Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))))


def gap_at(p, z0, z1, axis=0):
    """Widest empty interval along `axis` around 0 among vertices with z in [z0, z1]."""
    q = np.sort(p[(p[:, 2] > z0) & (p[:, 2] < z1)][:, axis])
    left = q[q < 0].max() if (q < 0).any() else -1.
    right = q[q > 0].min() if (q > 0).any() else 1.
    return left, right


def build_arch(o):
    cs.decimate(o, 9000)
    # faces along +-x in the source: turn so the face looks down -y
    cs.xform(o, Matrix.Rotation(math.radians(90), 4, 'Z'))
    p = cs.coords(o); lo, hi = p.min(0), p.max(0)
    cs.xform(o, Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -lo[1], -lo[2]))))
    p = cs.coords(o); h = p[:, 2].max()
    l, r = gap_at(p, .05 * h, .15 * h)
    s = GATE_CLEAR / (r - l)
    cs.xform(o, Matrix.Translation(Vector((-(l + r) / 2, 0, 0))))
    cs.xform(o, Matrix.Scale(s, 4))
    p = cs.coords(o)
    mid = p[np.abs(p[:, 0]) < .3]
    beam = mid[mid[:, 2] > 3.][:, 2].min() if (mid[:, 2] > 3.).any() else 0.
    print("ARCH scale %.1f size %s cross beam underside %.2f m" % (s, (p.max(0) - p.min(0)).round(2), beam))
    return o


def build_chandelier(o):
    cs.decimate(o, 7000); cs.ground_align(o)
    p = cs.coords(o); s = CHANDELIER_H / np.ptp(p[:, 2]); cs.xform(o, Matrix.Scale(s, 4))
    p = cs.coords(o)
    cx, cy = (p[:, 0].max() + p[:, 0].min()) / 2, (p[:, 1].max() + p[:, 1].min()) / 2
    cs.xform(o, Matrix.Translation(Vector((-cx, -cy, -CHANDELIER_H))))       # origin = the top of the chain
    p = cs.coords(o); r = np.hypot(p[:, 0], p[:, 1]); R = r.max()
    ring = p[r > .7 * R]
    zr = np.percentile(ring[:, 2], 60)
    cups = ring[ring[:, 2] > zr]
    ang = np.degrees(np.arctan2(cups[:, 1], cups[:, 0])) % 45.
    hist = np.histogram(ang, bins=45, range=(0, 45))[0]
    off = math.radians(np.argmax(hist) + .5)
    rc = float(np.median(np.hypot(cups[:, 0], cups[:, 1])))
    zc = float(np.percentile(cups[:, 2], 90))
    flames = [Vector((rc * math.cos(off + k * math.pi / 4), rc * math.sin(off + k * math.pi / 4), zc + .03)) for k in range(8)]
    print("CHANDELIER R %.2f cups r %.2f z %.2f" % (R, rc, zc))
    return o, flames, Vector((0, 0, zc - .1))


def build_sconce(o):
    cs.decimate(o, 2500)
    p = cs.coords(o); lo, hi = p.min(0), p.max(0)
    s = SCONCE_H / (hi[2] - lo[2]); cs.xform(o, Matrix.Scale(s, 4))
    p = cs.coords(o); lo, hi = p.min(0), p.max(0)
    # backplate on +y (against the wall): origin at the backplate's back face, lantern toward -y
    cs.xform(o, Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -hi[1], -(lo[2] + hi[2]) / 2))))
    p = cs.coords(o); y0 = p[:, 1].min()
    cage = p[p[:, 1] < y0 + .45 * np.ptp(p[:, 1])]
    c = (cage.min(0) + cage.max(0)) / 2
    z = cage[:, 2].min() + .38 * np.ptp(cage[:, 2])
    print("SCONCE depth %.2f flame at %s" % (np.ptp(p[:, 1]), np.round([c[0], c[1], z], 2)))
    return o, Vector((float(c[0]), float(c[1]), float(z)))


def build_tile(o):
    cs.decimate(o, 1500); min_rect_align(o)
    p = cs.coords(o); lo, hi = p.min(0), p.max(0)
    # brass strips are the raised edges: keep them along y (the aisle runs along section z = Blender y)
    edge_x = p[np.abs(p[:, 0]) > .42 * np.ptp(p[:, 0])][:, 2].mean()
    edge_y = p[np.abs(p[:, 1]) > .42 * np.ptp(p[:, 1])][:, 2].mean()
    if edge_y > edge_x: cs.xform(o, Matrix.Rotation(math.radians(90), 4, 'Z'))
    cs.fit_box(o, (4., 4., .2))
    cs.xform(o, Matrix.Translation(Vector((0, 0, -.2 + .012))))             # top 1.2 cm over the game's floor
    return o


def build_balustrade(o):
    # Square to the rail: the smallest footprint box (PCA leaned a few degrees, so the sections zig-zagged), long side x.
    cs.decimate(o, 1200); min_rect_align(o)
    p = cs.coords(o)
    if np.ptp(p[:, 1]) > np.ptp(p[:, 0]): cs.xform(o, Matrix.Rotation(math.radians(90), 4, 'Z'))
    p = cs.coords(o); s = BALUSTRADE_H / np.ptp(p[:, 2]); cs.xform(o, Matrix.Scale(s, 4))
    return o, float(np.ptp(cs.coords(o)[:, 0]))


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rp = cs.split(cs.load("R503_RusticProps"), rustic_rule)
    fa = cs.split(cs.load("R503_FantasyArchitectural"), fantasy_rule)
    for o in rp.values(): cs.kit_material(o, "PR_Mid_GateKit")
    for o in fa.values(): cs.kit_material(o, "PR_Mid_HallKit")

    arch = build_arch(rp["arch"])
    chand, flames, chand_light = build_chandelier(fa["chandelier"])
    sconce, sconce_flame = build_sconce(fa["sconce"])
    tile = build_tile(fa["tile"])
    bal, bal_len = build_balustrade(fa["balustrade"])
    stool = rp["stool"]; cs.decimate(stool, 2500); cs.ground_align(stool); cs.uniform(stool, 1.1)
    roll = rp["roll"]; cs.decimate(roll, 2500); cs.ground_align(roll); cs.uniform(roll, 1.8)

    env, sockets = [], []
    # Walkway tiles dropped (2026-11-07): the Tripo tile never squared up (its brass strips lay askew across the aisle).
    # The aisle now takes the slate floor material and two straight brass edge strips (b_shell_v1.py).
    for k in range(0):
        env.append(cs.place_copy(tile, "tile", Matrix.Translation((0., 4. + 4. * k + 2., 0.))))
    n = 0
    for side in (-1., 1.):
        for z0, z1 in GALLERY_Z:
            count = max(1, int(round((z1 - z0) / bal_len)))
            pitch = (z1 - z0) / count
            for i in range(count):
                z = z0 + (i + .5) * pitch
                # one unbroken railing (user, 2026-11-07): each section stretched 4 % past its pitch so the end posts
                # overlap, every other one 1 cm back (no shared planes, no flicker)
                S = Matrix.Diagonal((1.04 * pitch / bal_len, 1., 1., 1.))
                M = (Matrix.Translation((side * (BALUSTRADE_X + .01 * (i % 2) * side), z, MODULE_TOP))
                     @ Matrix.Rotation(math.radians(90), 4, 'Z') @ S)
                env.append(cs.place_copy(bal, "balustrade", M)); n += 1
    print("BALUSTRADE sections", n, "each %.2f m" % bal_len)
    for x, z in CHANDELIERS:
        at = Vector((x, z, CHANDELIER_TOP))
        env.append(cs.place_copy(chand, "chandelier", Matrix.Translation(at)))
        sockets += [("SOCKET_Flame", at + f) for f in flames] + [("SOCKET_Light", at + chand_light)]
    for x, z, side in SCONCES:
        yaw = 0. if side == 0 else (90. if side < 0 else -90.)       # lantern (-y) to the south, or into the room
        R = Matrix.Rotation(math.radians(yaw), 4, 'Z')
        at = Vector((x, z, SCONCE_Y))
        env.append(cs.place_copy(sconce, "sconce", Matrix.Translation(at) @ R))
        sockets.append(("SOCKET_Lantern", at + R.to_3x3() @ sconce_flame))
    for side in (-1., 1.):
        env.append(cs.place_copy(stool, "stool", Matrix.Translation((side * 16.4, 22.3, 0.)) @ Matrix.Rotation(math.radians(side * 20), 4, 'Z')))
        env.append(cs.place_copy(roll, "roll", Matrix.Translation((side * 17.6, 21.2, 0.)) @ Matrix.Rotation(math.radians(side * 70), 4, 'Z')))
    hall = cs.join(env, "B_HallEnv")
    for name, at in sockets:
        cs.socket(hall, name, at)
    print("HALL tris", cs.tris(hall), "sockets", len(sockets), "materials", [m.name for m in hall.data.materials])
    for o in (chand, sconce, tile, bal, stool, roll):
        bpy.data.objects.remove(o)
    print("ARCH tris", cs.tris(arch))
    if "--export" in ARGS:
        cs.export(arch, "B_TeamArch")
        cs.export(hall, "B_HallEnv")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "B_Hall_v1.blend"))
    if "--render" in ARGS:
        render(arch, hall)


def render(arch, hall):
    for x in (-12., 12.):
        cs.place_copy(arch, "arch", Matrix.Translation((x, ARCH_AT, 0.)))
    bpy.data.objects.remove(arch)
    col = bpy.data.collections.new("GAME_PROXY"); bpy.context.scene.collection.children.link(col)
    def mat(name, rgba):
        m = bpy.data.materials.new(name); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba; return m
    def box(lo, hi, m):
        c = [(a + b) / 2 for a, b in zip(lo, hi)]; s = [(b - a) / 2 for a, b in zip(lo, hi)]
        bpy.ops.mesh.primitive_cube_add(location=c, scale=s); o = bpy.context.object; o.data.materials.append(m)
        [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o); return o
    slate, wall, cloth = mat("slate", (.13, .17, .22, 1)), mat("wall", (.8, .74, .64, 1)), mat("cloth", (.82, .76, .64, 1))
    box((-22, -1, -1), (22, 36, 0), slate)
    for side in (-1, 1):
        box(sorted((side * 22.2, side * 23)), (-1, 36), wall) if False else box((min(side * 22.2, side * 23), -1, 0), (max(side * 22.2, side * 23), 36, 13), wall)
        box((min(side * 10, side * 14), 27, 0), (max(side * 10, side * 14), 27.4, 4.6), cloth)       # team gate (cloth in code)
        box((min(side * 14.35, side * 22), 24.05, 0), (max(side * 14.35, side * 22), 26, 13), wall)
        box((min(side * 8, side * 9.65), 24.05, 0), (max(side * 8, side * 9.65), 26, 13), wall)
    box((-8, 24.05, 9.9), (8, 26, 13), wall)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.9, .8, .68, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .5; sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(40), 0, math.radians(-30)))
    bpy.context.object.data.energy = 2.5
    for e in [o for o in bpy.data.objects if o.name.startswith("SOCKET_Light") or o.name.startswith("SOCKET_Lantern")]:
        bpy.ops.object.light_add(type='POINT', location=e.matrix_world.translation)
        bpy.context.object.data.energy = 400; bpy.context.object.data.color = (1, .75, .45)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    views = {"overview": ((0, -14, 14), (0, 16, 3), 24), "arch": ((-6, 14, 3.5), (-12, 24, 3.5), 30),
             "gallery": ((0, 8, 2), (20, 14, 5), 26)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_b_hall_%s.png" % name)
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
