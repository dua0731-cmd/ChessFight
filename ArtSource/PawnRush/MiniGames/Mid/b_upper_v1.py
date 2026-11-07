# B East Wing upper floor, ceiling and arches (R503): the user's two Tripo sets from the GPT sheets
# B_assets_06_upper / 07_ceiling_arches (Docs/PawnRush/ConceptArt/R503_RepairWing/AssetRefs), fitted to the section.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python b_upper_v1.py -- [--render] [--export]
#
# Sources (Source/, 1 m wide, z up; triangle budgets per part in README):
#   R503_StoneArchitectural: upper wall module (arched window), gallery floor on corbels, cornice, pilaster (unused)
#   R503_ArchitecturalSet:   ceiling beam, beam bracket, coffered panel, grand open arch, corridor wall module
# Output Export/B_UpperEnv.fbx in the section frame (Blender x = section x, y = section z, z = up), symmetric in x:
#   - gallery: walnut deck with a stone edge on three corbels on top of the ground-floor side modules (deck top 5.3 m,
#     x +-20.6..22.2), one per ground module (z 2, 8, 14, 20, 33); the hall balustrade stands on its front edge
#   - upper walls: the arched-window module behind the gallery (5.3..11.7 m), a warm glass pane in each window
#     (slot PR_Mid_WindowGlass) hiding the shell wall; cornice on top (11.7..12.6 m) where the ceiling beams rest
#   - ceiling: five walnut beams across the room at the shell's beam rows (z 3, 10, 17, 24, 32; 44 m of 6.3 m
#     segments, underside 12.4 m), brackets under their wall ends, a coffered panel over each chandelier
#   - arches: the open grand arch at the entry (z -0.4) and the exit (z 36.3), inner clear 12.4 m (the game's 12 m
#     openings), squashed to 12.6 m high to stay under the 13 m ceiling
#   - rear corridor: corridor wall modules on the end wall (z 36, facing the room) and on the north block's back (z 30)
# No light, flame or cloth (user rule): the glass is a flat warm material, the chandeliers' lights are code.
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import b_core_scene_v1 as cs  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

WALL_X, MODULE_W, GROUND_TOP = 22.2, 6., 4.85
MODULE_Z = [2., 8., 14., 20., 33.]
DECK_TOP, DECK_DEPTH, DECK_H = 5.3, 1.6, 1.0
UPPER_TOP, UPPER_DEPTH = 11.7, 1.2
CORNICE_H, CORNICE_D = .9, .8
BEAM_Z, BEAM_SEGMENTS, BEAM_UNDER, BEAM_SECTION = [3., 10., 17., 24., 32.], 7, 12.4, (.6, .55)
BRACKET_Z, BRACKET_H = [3., 10., 17., 32.], 2.0
PANEL_AT = [(-12., 2.), (12., 2.), (-12., 21.), (12., 21.)]
ARCH_CLEAR, ARCH_H, ARCH_DEPTH = 12.4, 12.6, 1.6
ENTRY_Z, EXIT_Z = -.4, 36.3
CORRIDOR_H = 4.4
CORRIDOR_RUNS = [  # (x0, x1, z, facing): facing -1 = toward -z (end wall), +1 = toward +z (north block back)
    (-22., -11.2, 35.95, -1), (11.2, 22., 35.95, -1),
    (-22., -14.35, 30.05, 1), (-9.65, 9.65, 30.05, 1), (14.35, 22., 30.05, 1),
    # walls run on without gaps (user, 2026-11-07): the entry wings beside the entry arch, facing the room (+z),
    # and the north facade outside the team arches (x +-16.3..22), facing the players (-z)
    (-22., -11.2, -.02, 1), (11.2, 22., -.02, 1), (-22., -16.3, 24.0, -1), (16.3, 22., 24.0, -1)]


def stone_rule(c):
    if c.x < -.21: return "upper_wall"
    if c.x < .12 and c.y > -.08: return "deck"
    if c.y < -.13 and c.x > .4: return None              # pilaster: the upper module already has end pilasters
    return "cornice"


def set_rule(c):
    if c.y > .25: return "beam"
    if c.y > .165: return "bracket"
    if c.y > -.03: return "panel"
    if c.y > -.3: return "arch"
    return "corridor"


def tone_gold(mat):
    """Bright saturated gold texels (the panel rosette) toned to matte brass, as the centre gate's toning."""
    n = next((n for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and n.image.name.endswith("BaseColor")), None)
    if n is None: return
    img = n.image; w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4); rgb = px[..., :3]
    mx, mn = rgb.max(-1), rgb.min(-1); d = np.maximum(mx - mn, 1e-5)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    sat = d / np.maximum(mx, 1e-5)
    gold = (np.clip((sat - .5) / .15, 0, 1) * np.clip((mx - .2) / .12, 0, 1) * np.clip(1 - np.abs(hue - 42) / 14, 0, 1))[..., None]
    lum = (rgb * np.array([.2126, .7152, .0722], dtype=np.float32)).sum(-1, keepdims=True)
    px[..., :3] = rgb * (1 - gold) + np.clip(np.array([.30, .22, .11], dtype=np.float32) * (lum / .35) ** .8, 0, 1) * gold
    img.pixels.foreach_set(px.ravel()); img.save()
    print("GOLD toned %.2f%%" % (100 * float((gold > .5).mean())))


def face_front(o, normal_axis):
    """Turn so the face that looked along -normal_axis ('x' or 'y') looks down -y, centred, ground at 0."""
    if normal_axis == 'x':
        cs.xform(o, Matrix.Rotation(math.radians(90), 4, 'Z'))
    p = cs.coords(o); lo, hi = p.min(0), p.max(0)
    cs.xform(o, Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))))


def window_rect(o):
    """The open window in a wall module (front -y): widest vertex-free x gap at mid height, then z extent there."""
    p = cs.coords(o); h = p[:, 2].max()
    band = np.sort(p[(p[:, 2] > .4 * h) & (p[:, 2] < .55 * h)][:, 0])
    gaps = np.diff(band); i = int(np.argmax(gaps)); x0, x1 = band[i], band[i + 1]
    xc = (x0 + x1) / 2
    col = np.sort(p[np.abs(p[:, 0] - xc) < .1 * (x1 - x0)][:, 2])
    gz = np.diff(col); j = int(np.argmax(gz)); z0, z1 = col[j], col[j + 1]
    return x0, x1, z0, z1


def glass(name, x0, x1, z0, z1, mat_slot_name):
    bm = bmesh.new(); uv = bm.loops.layers.uv.new("map1")
    for dy, order in ((-.002, 1), (.002, -1)):
        vs = [bm.verts.new((x, dy, z)) for x, z in ((x0, z0), (x1, z0), (x1, z1), (x0, z1))]
        f = bm.faces.new(vs[::order])
        for l, (u, v) in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))): l[uv].uv = (u, v)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    m = bpy.data.materials.get(mat_slot_name) or bpy.data.materials.new(mat_slot_name)
    me.materials.append(m)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def side_matrix(side, z, y=0.):
    """Ground-module convention: module face (-y) turned to face the room on the side wall at x = side * WALL_X."""
    yaw = 90. if side < 0 else -90.
    return Matrix.Translation((side * WALL_X, z, y)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z')


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    st = cs.split(cs.load("R503_StoneArchitectural"), stone_rule)
    se = cs.split(cs.load("R503_ArchitecturalSet"), set_rule)
    for o in st.values(): cs.kit_material(o, "PR_Mid_UpperKit")
    for o in se.values(): cs.kit_material(o, "PR_Mid_CeilingKit")
    tone_gold(se["panel"].data.materials[0])

    # --- fit (budgets: README table)
    upper = st["upper_wall"]; cs.decimate(upper, 1800); face_front(upper, 'y')
    cs.fit_box(upper, (MODULE_W, UPPER_DEPTH, UPPER_TOP - DECK_TOP))
    x0, x1, z0, z1 = window_rect(upper)
    # No glass (user, 2026-11-07: "창문 밖이 보여야지"): open window; B_ShellEnv leaves a hole behind it.
    print("UPPER window x %.2f..%.2f z %.2f..%.2f" % (x0, x1, z0, z1))
    deck = st["deck"]; cs.decimate(deck, 1200); face_front(deck, 'y'); cs.fit_box(deck, (MODULE_W, DECK_DEPTH, DECK_H))
    cornice = st["cornice"]; cs.decimate(cornice, 700); face_front(cornice, 'y'); cs.fit_box(cornice, (MODULE_W, CORNICE_D, CORNICE_H))
    beam = se["beam"]; cs.decimate(beam, 600); cs.ground_align(beam)
    cs.fit_box(beam, (44.4 / BEAM_SEGMENTS, BEAM_SECTION[0], BEAM_SECTION[1]))
    bracket = se["bracket"]; cs.decimate(bracket, 900); face_front(bracket, 'x')
    p = cs.coords(bracket); s = BRACKET_H / np.ptp(p[:, 2]); cs.xform(bracket, Matrix.Scale(s, 4))
    panel = se["panel"]; cs.decimate(panel, 2000)
    p = cs.coords(panel); p0 = p - p.mean(0); w, v = np.linalg.eigh(np.cov(p0.T)); nrm = Vector(v[:, 0].tolist())
    if nrm.z > 0: nrm = -nrm
    cs.xform(panel, nrm.rotation_difference(Vector((0, 0, -1))).to_matrix().to_4x4())   # face (rosette) down
    import b_hall_v1 as hall
    hall.min_rect_align(panel)
    cs.fit_box(panel, (3.2, 3.2, .35))
    arch = se["arch"]; cs.decimate(arch, 6000); face_front(arch, 'x')
    p = cs.coords(arch); h = p[:, 2].max()
    l, r = hall.gap_at(p, .05 * h, .2 * h)
    s = ARCH_CLEAR / (r - l)
    cs.xform(arch, Matrix.Translation(Vector((-(l + r) / 2, 0, 0)))); cs.xform(arch, Matrix.Scale(s, 4))
    p = cs.coords(arch); cs.xform(arch, Matrix.Diagonal((1., ARCH_DEPTH / np.ptp(p[:, 1]), ARCH_H / p[:, 2].max(), 1.)))
    p = cs.coords(arch); print("ARCH size %s (uniform x%.1f, then z to %.1f m)" % ((p.max(0) - p.min(0)).round(2), s, ARCH_H))
    corridor = se["corridor"]; cs.decimate(corridor, 1200); face_front(corridor, 'x')

    # --- place
    env = []
    for side in (-1., 1.):
        for i, z in enumerate(MODULE_Z):
            # Continuous walls (user, 2026-11-07): each piece 3 % longer than the 6 m pitch so neighbours overlap,
            # every other one 1 cm back so the overlaps never share a plane (no flicker).
            S = Matrix.Diagonal((1.03, 1., 1., 1.)); back = .01 * (i % 2)
            # deck: back edge in the wall (x = side*WALL_X), front edge 1.6 m into the room, top at DECK_TOP
            env.append(cs.place_copy(deck, "deck", side_matrix(side, z, DECK_TOP - DECK_H) @ Matrix.Translation((0, -DECK_DEPTH / 2 + .02 + back, 0)) @ S))
            env.append(cs.place_copy(upper, "upper", side_matrix(side, z, DECK_TOP) @ Matrix.Translation((0, .1 + back, 0)) @ S))
            env.append(cs.place_copy(cornice, "cornice", side_matrix(side, z, UPPER_TOP) @ Matrix.Translation((0, -.25 + back, 0)) @ S))
        for z in BRACKET_Z:
            env.append(cs.place_copy(bracket, "bracket", side_matrix(side, z, BEAM_UNDER - BRACKET_H) @ Matrix.Translation((0, -UPPER_DEPTH / 2 + .1, 0))))
    for z in BEAM_Z:
        for k in range(BEAM_SEGMENTS):
            x = -22.2 + (k + .5) * 44.4 / BEAM_SEGMENTS
            env.append(cs.place_copy(beam, "beam", Matrix.Translation((x, z, BEAM_UNDER))))
    for x, z in PANEL_AT:
        env.append(cs.place_copy(panel, "panel", Matrix.Translation((x, z, 13. - .35))))
    env.append(cs.place_copy(arch, "entry arch", Matrix.Translation((0., ENTRY_Z, 0.))))
    env.append(cs.place_copy(arch, "exit arch", Matrix.Translation((0., EXIT_Z, 0.)) @ Matrix.Rotation(math.pi, 4, 'Z')))
    p = cs.coords(corridor); cw = np.ptp(p[:, 0]); ch = np.ptp(p[:, 2])
    for x0, x1, z, facing in CORRIDOR_RUNS:
        n = max(1, int(round((x1 - x0) / 5.)))
        w = (x1 - x0) / n
        for i in range(n):
            xc = x0 + (i + .5) * w
            # 15 % overlap: the cornice overhangs the wall body, so bodies placed edge to edge leave a slit.
            # Every other module sits 1 cm back so the overlapping cornices never share a plane (no flicker).
            S = Matrix.Diagonal((1.15 * w / cw, 1., CORRIDOR_H / ch, 1.))
            R = Matrix.Rotation(math.pi if facing > 0 else 0., 4, 'Z')   # face -y = toward -z; turned = toward +z
            back = .01 * (i % 2)
            env.append(cs.place_copy(corridor, "corridor", Matrix.Translation((xc, z, 0.)) @ R @ Matrix.Translation((0, back, 0)) @ S))
    for o in (upper, deck, cornice, beam, bracket, panel, arch, corridor):
        bpy.data.objects.remove(o)
    up = cs.join(env, "B_UpperEnv")
    print("UPPER ENV tris", cs.tris(up), "materials", [m.name for m in up.data.materials])
    if "--export" in ARGS:
        cs.export(up, "B_UpperEnv")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "B_Upper_v1.blend"))
    if "--render" in ARGS:
        render()


def render():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.9, .8, .68, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .6; sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(35), 0, math.radians(-30)))
    bpy.context.object.data.energy = 2.5
    # ground modules and floor as stand-ins
    for side in (-1, 1):
        bpy.ops.mesh.primitive_cube_add(location=(side * 21.6, 17.5, GROUND_TOP / 2), scale=(.6, 18.5, GROUND_TOP / 2))
    bpy.ops.mesh.primitive_cube_add(location=(0, 17.5, -.5), scale=(22, 18.5, .5))
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    views = {"hall": ((0, -6, 4), (0, 20, 7), 20), "gallery": ((-6, 6, 6.5), (20, 14, 8), 24),
             "rear": ((0, 26, 3), (0, 36, 4), 22)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_b_upper_%s.png" % name)
        bpy.ops.render.render(write_still=True)


main()
