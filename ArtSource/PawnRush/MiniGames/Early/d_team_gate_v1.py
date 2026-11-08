# D team gates (R505): the user's "Stone Arch Portcullis" FBX split into the front arch and the portcullis and fitted
# to the game's team portal (MiniGamePalaceArt.Shell "Team portal front": opening 4.5 x 5.8 m at section x +-12,
# z 23.8) and team gate (TeamGate: 4 x 4.6 x 0.4 m at section z 27..27.4, lifted 0.4 m by progress, 4.6 m when done).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python d_team_gate_v1.py -- [--render] [--export]
#
# Source: Source/R505_TeamGate_StoneArchPortcullis.fbx (1.0 x 0.15 x 0.48, 500k triangles; arch on the left,
# portcullis on the right, apart).
#   - Arch: the opening is scaled to 4.5 m (x14.4, opening 5.85 m high); the piers are narrowed to PIER m each (the
#     user's choice, 11-07) so the inner pier stands less in front of the centre gate's wing. Origin = opening bottom
#     centre at the arch's mid depth.
#   - Portcullis: x WIDTH / source width (4.4 m wide, ~4.7 m with the spikes). Origin = spike tips' level, centre, mid
#     depth: it hangs from the bottom of the game's team gate and rides it.
# Colours are kept: the rivets are already dark bronze (matte-brass-like), and a colour mask for them also caught
# the oak corbels, the beam and tan stones (checked 11-07), so TONE_RIVETS is off.
TONE_RIVETS = False
import bpy, bmesh, math, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mg_holes   # closes Tripo's small face holes (R505)
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R505_TeamGate_StoneArchPortcullis.fbx")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

OPENING, PIER, WIDTH = 4.5, .9, 4.4
SPLIT_X = .09                        # source x between the two pieces
TARGET_ARCH, TARGET_GATE = 12000, 6000


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def load_split():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    src = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    bm = bmesh.new(); bm.from_mesh(src.data); bm.transform(src.matrix_world)
    bm.verts.ensure_lookup_table()
    seen = [False] * len(bm.verts); side = [0] * len(bm.verts)
    for v in bm.verts:
        if seen[v.index]: continue
        comp, st = [], [v]
        while st:
            x = st.pop()
            if seen[x.index]: continue
            seen[x.index] = True; comp.append(x)
            st.extend(e.other_vert(x) for e in x.link_edges)
        cx = sum(p.co.x for p in comp) / len(comp)
        for p in comp: side[p.index] = 1 if cx > SPLIT_X else 0
    out = []
    for s, name in ((0, "D_TeamArch"), (1, "D_Portcullis")):
        b = bm.copy(); b.verts.ensure_lookup_table()
        bmesh.ops.delete(b, geom=[v for v in b.verts if side[v.index] != s], context='VERTS')
        me = bpy.data.meshes.new(name); b.to_mesh(me); b.free()
        for m in src.data.materials: me.materials.append(m)
        ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
        out.append(ob)
    bm.free(); bpy.data.objects.remove(src)
    return out


def bounds(ob):
    vs = [v.co for v in ob.data.vertices]
    return [min(v[i] for v in vs) for i in range(3)], [max(v[i] for v in vs) for i in range(3)]


def opening(ob, lo, hi):
    """The widest empty x span among the arch's vertices at mid pier height (above the wider plinths)."""
    z0, z1 = lo[2] + .25 * (hi[2] - lo[2]), lo[2] + .45 * (hi[2] - lo[2])
    xs = sorted(v.co.x for v in ob.data.vertices if z0 < v.co.z < z1)
    gaps = [(b - a, a, b) for a, b in zip(xs, xs[1:])]
    w, a, b = max(gaps)
    # The doorway's top: lowest vertex above the opening's middle third.
    mid = [v.co.z for v in ob.data.vertices if a + w * .3 < v.co.x < b - w * .3 and v.co.z > z1]
    return a, b, min(mid)


def fit_arch(ob):
    lo, hi = bounds(ob)
    a, b, top = opening(ob, lo, hi)
    xc, half = (a + b) / 2, (b - a) / 2
    s = OPENING / (b - a)
    yc = (lo[1] + hi[1]) / 2
    left, right = xc - lo[0] - half, hi[0] - xc - half       # source pier widths
    for v in ob.data.vertices:
        dx = v.co.x - xc
        ax = abs(dx)
        if ax <= half:
            x2 = dx * s
        else:
            pier = left if dx < 0 else right
            x2 = math.copysign(OPENING / 2 + (ax - half) * PIER / pier, dx)
        v.co = Vector((x2, (v.co.y - yc) * s, (v.co.z - lo[2]) * s))
    print("ARCH opening %.3f..%.3f top %.3f  scale %.2f  piers %.3f/%.3f -> %.2f m" % (a, b, top, s, left, right, PIER))
    print("ARCH opening height %.2f m" % ((top - lo[2]) * s))


def fit_gate(ob):
    lo, hi = bounds(ob)
    s = WIDTH / (hi[0] - lo[0])
    xc, yc = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
    for v in ob.data.vertices:
        v.co = Vector(((v.co.x - xc) * s, (v.co.y - yc) * s, (v.co.z - lo[2]) * s))
    print("GATE scale %.2f" % s)


def decimate(ob, target):
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new("weld", 'WELD'); mod.merge_threshold = .003
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    before = tris(ob)
    mod = ob.modifiers.new("dec", 'DECIMATE'); mod.ratio = min(1, target / before); mod.use_collapse_triangulate = True
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return before, tris(ob)


def retexture(mat):
    os.makedirs(TEX, exist_ok=True)
    nodes = mat.node_tree.nodes
    imgs = {n.image.name: n for n in nodes if n.type == 'TEX_IMAGE' and n.image}
    base, normal = imgs["base_color"].image, imgs["normal"].image
    w, h = base.size
    px = np.array(base.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rgb = px[..., :3]
    # Judge colours in display (sRGB) values: in linear values the cream stone reads as saturated as the rivets.
    srgb = np.clip(rgb, 0, 1) ** (1 / 2.2)
    mx, mn = srgb.max(-1), srgb.min(-1)
    d = np.maximum(mx - mn, 1e-5)
    r, g, b = srgb[..., 0], srgb[..., 1], srgb[..., 2]
    hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    sat = d / np.maximum(mx, 1e-5)
    # Rivets: copper-gold (hue ~25-50), saturated (sRGB > .55), mid-bright. Cream stone is pale (sRGB sat ~.2-.3),
    # the beam wood is redder and darker.
    gold = np.clip((sat - .55) / .12, 0, 1) * np.clip((mx - .45) / .12, 0, 1) * np.clip(1 - np.abs(hue - 36) / 14, 0, 1)
    gold = gold[..., None]
    lum = (rgb * np.array([.2126, .7152, .0722], dtype=np.float32)).sum(-1, keepdims=True)
    brass = np.array([.30, .22, .11], dtype=np.float32) * (lum / .35) ** .8
    if TONE_RIVETS:
        px[..., :3] = rgb * (1 - gold) + np.clip(brass, 0, 1) * gold
        print("GOLD texels toned down: %.2f%%" % (100 * float((gold > .5).mean())))
    out = bpy.data.images.new("PR_Early_TeamGate_BaseColor", w, h)
    out.pixels.foreach_set(px.ravel()); out.scale(2048, 2048)
    out.filepath_raw = os.path.join(TEX, "PR_Early_TeamGate_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nrm = normal.copy(); nrm.name = "PR_Early_TeamGate_Normal"; nrm.colorspace_settings.name = 'Non-Color'
    nrm.filepath_raw = os.path.join(TEX, "PR_Early_TeamGate_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    imgs["base_color"].image = out; imgs["normal"].image = nrm
    mat.name = "PR_Early_TeamGate"
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Roughness"].default_value = .8
    bsdf.inputs["Metallic"].default_value = 0.0


def proxies(arch, gate):
    """Blue team side in section coordinates (x -12): gate at z 27.2, arch at z 23.8, centre gate wing, walls."""
    col = bpy.data.collections.new("GAME_PROXY"); bpy.context.scene.collection.children.link(col)
    def mat(name, rgba):
        m = bpy.data.materials.new(name); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba
        return m
    def box(lo, hi, m):
        c = [(a + b) / 2 for a, b in zip(lo, hi)]; s = [(b - a) / 2 for a, b in zip(lo, hi)]
        bpy.ops.mesh.primitive_cube_add(location=c, scale=s); ob = bpy.context.object; ob.data.materials.append(m)
        for u in ob.users_collection: u.objects.unlink(ob)
        col.objects.link(ob)
    # Blender (x, y, z) here = section (x, z, y) mirrored in y so the players' side is -y: y = -(z - 23.8).
    Y = lambda z: -(z - 23.8)
    box((-22, Y(36), -.05), (0, Y(0), 0), mat("paving", (.78, .72, .62, 1)))
    stone = mat("wall", (.62, .6, .56, 1))
    box((-22, Y(26), 0), (-14.35, Y(24.05), 11), stone)               # side wall mass
    box((-14.25, Y(30), 0), (-14.13, Y(24), 5.6), stone)               # tunnel lining
    box((-9.87, Y(30), 0), (-9.75, Y(24), 5.6), stone)
    box((-9.93, Y(30.1), 0), (-6.35, Y(24.34), 8.7), mat("centre gate wing", (.85, .8, .7, 1)))
    box((-14.35, Y(26), 6), (-9.65, Y(24.05), 11), stone)              # portal upper wall
    arch.location = (-12, 0, 0)
    gate.location = (-12, Y(27.2), 0)
    gate.rotation_euler = (0, 0, math.pi)                              # game face toward the entrance
    pawn = mat("pawn", (.95, .93, .9, 1))
    for x, z in ((-12.6, 21.5), (-10.8, 25.5)):
        bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(x, Y(z), .31)); o = bpy.context.object
        o.data.materials.append(pawn); [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(x, Y(z), .84)); o = bpy.context.object
        o.data.materials.append(pawn); [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o)


def render():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.55, .7, .9, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .7
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30)))
    bpy.context.object.data.energy = 3.5; bpy.context.object.data.angle = math.radians(6)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    views = {"front": ((-12, 14, 3.5), (-12, 0, 3.2), 32), "q34": ((-1, 13, 7), (-12, -1, 3), 30),
             "close": ((-9.5, 6, 1.6), (-12, -2, 2.4), 32)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_d_teamgate_%s.png" % name)
        bpy.ops.render.render(write_still=True)


def back_face(arch):
    """Tripo made only the arch's face; from the tunnel behind it the back showed holes (user, 11-07: "성벽 뒤
    깨졌어"). A copy turned front to back (mirrored in y) goes inside it, a hair thinner (y x0.97) and narrower
    (x x0.998) so none of its faces share a plane with the original's (no flicker, R498): from behind, its face shows."""
    from mathutils import Matrix
    me = arch.data.copy()
    me.transform(Matrix.Diagonal((.998, -.97, 1.0, 1.0)))
    me.flip_normals()                                   # the mirror turned every face inside out
    bm = bmesh.new(); bm.from_mesh(arch.data); bm.from_mesh(me); bm.to_mesh(arch.data); bm.free()
    bpy.data.meshes.remove(me)
    print("ARCH back: mirrored copy added, %d triangles" % tris(arch))


def main():
    arch, gate = load_split()
    fit_arch(arch); fit_gate(gate)
    print("DECIMATE arch", decimate(arch, TARGET_ARCH), "gate", decimate(gate, TARGET_GATE))
    retexture(arch.data.materials[0])
    mg_holes.close_small_holes(arch, 4.0)
    mg_holes.plug_gaps(arch, mg_holes.stone_brick("PR_Early_WallCore", TEX, (.81, .78, .73)))
    mg_holes.mirror_back(arch)       # replaces back_face (kept below for the record)
    for o in (arch, gate):
        lo, hi = bounds(o)
        print("FIT", o.name, tris(o), [round(x, 2) for x in lo], [round(x, 2) for x in hi])
    if "--export" in ARGS:
        os.makedirs(OUT_FBX, exist_ok=True)
        for o in (arch, gate):
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
            bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, o.name + ".fbx"), use_selection=True,
                                     axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                     mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    proxies(arch, gate)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "D_TeamGate_v1.blend"))
    if "--render" in ARGS: render()


main()
