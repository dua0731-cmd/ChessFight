# D capstan (R505): the user's "Royal Wooden Base" FBX fitted to the game's own capstan numbers (MgCapstan).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python d_capstan_v1.py -- [--render] [--export]
#
# Source: Source/R505_CapstanBase_RoyalWooden.fbx (1 m box, 100k triangles, one mesh of 576 loose pieces, bars on the
# diagonals). One uniform scale cannot fit the game, so the pieces are split and fitted separately:
#   - Static: drum, iron bands, lid and feet. Radius 0.24 -> 0.70 m (game drum 1.4 m across), height 0.47 -> 1.8 m
#     (top of the game's solid drum; the game's cap above it has no collider and is replaced by the model's lid).
#   - Bars: the four bars, turned onto the axes (the game's bars are at 0/90/180/270) and remapped along the bar so the
#     wood runs 0.55..2.70 m (buried in the drum at the root) and the gold tip 2.70..3.00 m (the game's last 30 cm);
#     the cross-section is remapped to 0.40..0.62 m high and 0.22 m wide (the game's bar box 0.4..0.62 x 0.2).
# Only the bars turn in the game, so the bars are their own FBX with the pivot on the axis.
# Iron in the base colour is darkened toward the phase-1 dark iron #393D43; gold tips and wood are left alone.
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R505_CapstanBase_RoyalWooden.fbx")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# Game numbers (MgCapstan.cs).
DRUM_R, DRUM_TOP = .70, 1.80
BAR_IN, BAR_OUT, TIP = .55, 3.00, .30
BAR_LO, BAR_HI, BAR_W = .40, .62, .22
# Source numbers (measured on the 1 m import).
S_DRUM_R, S_LO, S_HI = .24, -.235, .235
S_ROOT, S_TIP, S_END = .21, .50, .671      # bar root, gold tip start, bar end (radius)
S_BAR_Z, S_BAR_HALF = .084, .055           # bar centre height, half thickness
LID_UV = (0.5, 0.5)
STUB = .30                                 # source radius the old bar roots are cut at (stub ~0.12 m past the band)
TARGET_STATIC, TARGET_BAR = 22000, 1200    # triangles after decimation


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    src = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    bm = bmesh.new(); bm.from_mesh(src.data); bm.transform(src.matrix_world)
    return src, bm


def islands(bm):
    bm.verts.ensure_lookup_table()
    seen = [False] * len(bm.verts); out = []
    for v in bm.verts:
        if seen[v.index]: continue
        comp, st = [], [v]
        while st:
            x = st.pop()
            if seen[x.index]: continue
            seen[x.index] = True; comp.append(x)
            st.extend(e.other_vert(x) for e in x.link_edges)
        out.append(comp)
    return out


def split(src, bm):
    """Tags every vertex 0 = static, 1..4 = bar (by diagonal), then builds one object per group."""
    tag = {}
    for comp in islands(bm):
        rmax = max(math.hypot(v.co.x, v.co.y) for v in comp)
        zmin = min(v.co.z for v in comp)
        if rmax > .30 and zmin > 0.0:
            c = sum((v.co for v in comp), Vector()) / len(comp)
            quad = int(((math.degrees(math.atan2(c.y, c.x)) % 360) // 90)) + 1  # 45->1, 135->2, 225->3, 315->4
            for v in comp: tag[v.index] = quad
        else:
            for v in comp: tag[v.index] = 0
    # The source's bars came out of the upper iron band; taking them away leaves holes there. Keep the root of each
    # source bar (out to radius STUB) as a short static stub, like the spare sockets of a real capstan head.
    global LID_UV
    uvl0 = bm.loops.layers.uv.active
    bm.faces.ensure_lookup_table()
    top = max((f for f in bm.faces if f.normal.z > .9), key=lambda f: f.calc_center_median().z - math.hypot(*f.calc_center_median().xy))
    LID_UV = tuple(top.loops[0][uvl0].uv)
    stub = bm.copy(); stub.verts.ensure_lookup_table()
    bmesh.ops.delete(stub, geom=[v for v in stub.verts if tag[v.index] == 0], context='VERTS')
    for q in range(1, 5):
        a = math.radians(45 + 90 * (q - 1))
        n = Vector((math.cos(a), math.sin(a), 0))
        geom = stub.verts[:] + stub.edges[:] + stub.faces[:]
        # Only cut this bar: the plane test runs on everything, so cut along the bar's own direction and keep the
        # three other bars by testing which side they lie on after the cut.
        res = bmesh.ops.bisect_plane(stub, geom=geom, plane_co=n * STUB, plane_no=n, clear_outer=False, clear_inner=False)
        cut = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        far = [v for v in stub.verts if v.co.dot(n) > STUB + 1e-5 and abs(v.co.dot(Vector((-n.y, n.x, 0)))) < .08]
        bmesh.ops.delete(stub, geom=far, context='VERTS')
        # Cap the stub's end: the cut ring belongs to several loose shell pieces, so holes_fill cannot close it (the
        # open tube showed through in Unity, user 11-07: "깨진 거 수정해주고"). A convex face over the ring's points,
        # textured with the drum lid's end grain, closes it.
        side = Vector((-n.y, n.x, 0))
        ring = [v.co.copy() for v in stub.verts if abs(v.co.dot(n) - STUB) < 1e-4 and abs(v.co.dot(side)) < .08]
        if len(ring) >= 3:
            pts = sorted({(round(c.dot(side), 5), round(c.z, 5)) for c in ring})
            def half(seq):
                h = []
                for q2 in seq:
                    while len(h) >= 2 and (h[-1][0] - h[-2][0]) * (q2[1] - h[-2][1]) - (h[-1][1] - h[-2][1]) * (q2[0] - h[-2][0]) <= 0: h.pop()
                    h.append(q2)
                return h
            hull = half(pts)[:-1] + half(pts[::-1])[:-1]
            vs = [stub.verts.new(n * (STUB + .002) + side * u + Vector((0, 0, z))) for u, z in hull]
            cap = stub.faces.new(vs)
            uvl = stub.loops.layers.uv.active
            for l in cap.loops:
                u, z = l.vert.co.dot(side), l.vert.co.z - S_BAR_Z
                l[uvl].uv = Vector(LID_UV) + Vector((u, z)) * .12
            cap.normal_update()
            if cap.normal.dot(n) < 0: cap.normal_flip()
    stub_me = bpy.data.meshes.new("stub"); stub.to_mesh(stub_me); stub.free()
    objs = {}
    for group in range(5):
        b = bm.copy()
        b.verts.ensure_lookup_table()
        bmesh.ops.delete(b, geom=[v for v in b.verts if tag[v.index] != group], context='VERTS')
        me = bpy.data.meshes.new("D_Capstan_" + ("Static" if group == 0 else "Bar%d" % group))
        b.to_mesh(me); b.free()
        for m in src.data.materials: me.materials.append(m)
        ob = bpy.data.objects.new(me.name, me)
        bpy.context.scene.collection.objects.link(ob)
        objs[group] = ob
    # Merge the stubs into the static mesh.
    sb = bmesh.new(); sb.from_mesh(objs[0].data); sb.from_mesh(stub_me); sb.to_mesh(objs[0].data); sb.free()
    bpy.data.meshes.remove(stub_me)
    bpy.data.objects.remove(src)
    return objs


def fit_static(ob):
    k = DRUM_R / S_DRUM_R
    for v in ob.data.vertices:
        v.co = Vector((v.co.x * k, v.co.y * k, (v.co.z - S_LO) / (S_HI - S_LO) * DRUM_TOP))


def fit_bar(ob, quad):
    src_angle = math.radians(45 + 90 * (quad - 1))
    dst_angle = math.radians(90 * (quad - 1))
    ca, sa = math.cos(-src_angle), math.sin(-src_angle)
    cb, sb = math.cos(dst_angle), math.sin(dst_angle)
    zc = (BAR_LO + BAR_HI) / 2; kz = (BAR_HI - BAR_LO) / 2 / S_BAR_HALF; kv = BAR_W / 2 / S_BAR_HALF
    for v in ob.data.vertices:
        u = v.co.x * ca - v.co.y * sa          # along the bar
        w = v.co.x * sa + v.co.y * ca          # across
        if u < S_TIP:
            u2 = BAR_IN + (u - S_ROOT) * (BAR_OUT - TIP - BAR_IN) / (S_TIP - S_ROOT)
        else:
            u2 = BAR_OUT - TIP + (u - S_TIP) * TIP / (S_END - S_TIP)
        w2 = w * kv
        z2 = zc + (v.co.z - S_BAR_Z) * kz
        v.co = Vector((u2 * cb - w2 * sb, u2 * sb + w2 * cb, z2))


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def decimate(ob, target):
    before = tris(ob)
    if before <= target: return before, before
    mod = ob.modifiers.new("dec", 'DECIMATE'); mod.ratio = target / before
    bpy.context.view_layer.objects.active = ob
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return before, tris(ob)


def retexture(mat):
    """Saves 2048 maps; base colour iron darkened toward #393D43 by a low-saturation mask."""
    os.makedirs(TEX, exist_ok=True)
    nodes = mat.node_tree.nodes
    imgs = {n.image.name: n for n in nodes if n.type == 'TEX_IMAGE' and n.image}
    base, normal = imgs["base_color"].image, imgs["normal"].image
    w, h = base.size
    px = np.array(base.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rgb = px[..., :3]
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-4)
    mask = np.clip((.22 - sat) / .10, 0, 1)[..., None]          # grey = iron
    lum = rgb.mean(-1, keepdims=True)
    iron = np.array([.040, .046, .056], dtype=np.float32)        # #393D43 in linear
    dark = iron * (lum / .30) ** .85                              # keep the hammered light/dark of the source
    px[..., :3] = rgb * (1 - mask) + np.clip(dark, 0, 1) * mask
    out = bpy.data.images.new("PR_Early_Capstan_BaseColor", w, h)
    out.pixels.foreach_set(px.ravel())
    out.scale(2048, 2048)
    out.filepath_raw = os.path.join(TEX, "PR_Early_Capstan_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nrm = normal.copy(); nrm.name = "PR_Early_Capstan_Normal"
    nrm.colorspace_settings.name = 'Non-Color'
    nrm.filepath_raw = os.path.join(TEX, "PR_Early_Capstan_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    imgs["base_color"].image = out
    imgs["normal"].image = nrm
    mat.name = "PR_Early_Capstan"
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Roughness"].default_value = .72
    bsdf.inputs["Metallic"].default_value = 0.0


def proxies():
    """Game stand-ins (not exported): drum and bar colliders, the push ring, a 1 m pawn."""
    col = bpy.data.collections.new("GAME_PROXY"); bpy.context.scene.collection.children.link(col)
    def mat(name, rgba):
        m = bpy.data.materials.new(name); m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = rgba
        b.inputs["Alpha"].default_value = rgba[3]; m.blend_method = 'BLEND' if rgba[3] < 1 else 'OPAQUE'
        return m
    def add(ob, m):
        for c in ob.users_collection: c.objects.unlink(ob)
        col.objects.link(ob); ob.data.materials.append(m); return ob
    bpy.ops.mesh.primitive_cylinder_add(radius=3.6, depth=.04, location=(0, 0, -.02), vertices=96)
    add(bpy.context.object, mat("proxy slate", (.18, .19, .22, 1)))
    ghost = mat("proxy collider", (1, .1, .1, 1))
    colliders = bpy.data.collections.new("GAME_COLLIDER"); bpy.context.scene.collection.children.link(colliders)
    def wire(ob):
        add(ob, ghost); col.objects.unlink(ob); colliders.objects.link(ob)
        m = ob.modifiers.new("w", 'WIREFRAME'); m.thickness = .015
    bpy.ops.mesh.primitive_cylinder_add(radius=DRUM_R, depth=DRUM_TOP, location=(0, 0, DRUM_TOP / 2), vertices=24)
    wire(bpy.context.object)
    for i in range(4):
        a = math.radians(90 * i); r = (.75 + 3.0) / 2
        bpy.ops.mesh.primitive_cube_add(location=(math.cos(a) * r, math.sin(a) * r, (BAR_LO + BAR_HI) / 2),
                                        rotation=(0, 0, a), scale=((3.0 - .75) / 2, .1, (BAR_HI - BAR_LO) / 2))
        wire(bpy.context.object)
    pawn = mat("proxy pawn", (.92, .9, .86, 1))
    for x, y, yaw in ((1.9, -.45, 0), (-.45, -2.4, 90)):
        bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(x, y, .31))
        add(bpy.context.object, pawn)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(x, y, .84))
        add(bpy.context.object, pawn)
    return col


def render(prefix):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.62, .7, .82, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .6
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(48), 0, math.radians(35)))
    bpy.context.object.data.energy = 3.2; bpy.context.object.data.angle = math.radians(8)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    target = Vector((0, 0, .7))
    colliders = bpy.data.collections["GAME_COLLIDER"]
    views = {"q34": (30, 24, 11, 40), "side": (0, 4, 10, 45), "close": (55, 18, 5.2, 40), "collider": (30, 24, 11, 40)}
    for name, (az, el, d, lens) in views.items():
        colliders.hide_render = name != "collider"
        a, e = math.radians(az), math.radians(el)
        cam.location = target + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * d
        cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
        cd.lens = lens
        sc.render.filepath = os.path.join(HERE, "render_d_capstan_%s.png" % name)
        bpy.ops.render.render(write_still=True)


def export(objs):
    os.makedirs(OUT_FBX, exist_ok=True)
    for name, group in (("D_Capstan_Static", [objs[0]]), ("D_Capstan_Bars", [objs[i] for i in range(1, 5)])):
        bpy.ops.object.select_all(action='DESELECT')
        for o in group: o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                 mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)


def plug_sockets(ob):
    """The four old-bar stubs are open tubes and the iron band has holes behind them: seen in Unity as see-through
    breaks (user, 11-07: "깨진 거 수정해주고"). Each gets a closed oak plug from inside the drum to just past the stub's
    end, a little larger than the stub, with a flat end-grain face. Flat oak colour taken from the drum's own texture."""
    import numpy as np
    mat = ob.data.materials[0]
    img = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "Capstan_BaseColor" in n.image.name)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)[::97, :3]
    s = np.clip(px, 0, 1) ** (1 / 2.2); mx, mn = s.max(1), s.min(1); sat = (mx - mn) / np.maximum(mx, 1e-4)
    oak = px[(sat > .35) & (mx > .35) & (s[:, 0] > s[:, 2])].mean(0)
    wood = bpy.data.materials.new("PR_Early_CapstanOak"); wood.use_nodes = True
    b = wood.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (*oak, 1); b.inputs["Roughness"].default_value = .8
    ob.data.materials.append(wood); slot = len(ob.data.materials) - 1
    k = DRUM_R / S_DRUM_R
    r0, r1 = S_ROOT * k - .1, STUB * k + .03                  # from inside the drum to past the stub's end
    zc = (S_BAR_Z - S_LO) / (S_HI - S_LO) * DRUM_TOP
    hz = S_BAR_HALF / (S_HI - S_LO) * DRUM_TOP + .04            # the stub's half height (+4 cm)
    hw = S_BAR_HALF * k + .04                                   # its half width (+4 cm)
    bm = bmesh.new()
    for q in range(4):
        a = math.radians(45 + 90 * q)
        geom = bmesh.ops.create_cube(bm, size=1.0)
        vs = geom["verts"]
        for v in vs:
            u = r0 + (v.co.x + .5) * (r1 - r0); w2 = v.co.y * 2 * hw; z = zc + v.co.z * 2 * hz
            v.co = Vector((u * math.cos(a) - w2 * math.sin(a), u * math.sin(a) + w2 * math.cos(a), z))
        bmesh.ops.bevel(bm, geom=[e for e in bm.edges if all(v in vs for v in e.verts)], offset=.025, segments=2, affect='EDGES')
    for f in bm.faces: f.material_index = slot
    me = bpy.data.meshes.new("plugs"); bm.to_mesh(me); bm.free()
    b2 = bmesh.new(); b2.from_mesh(ob.data); b2.from_mesh(me); b2.to_mesh(ob.data); b2.free()
    bpy.data.meshes.remove(me)
    print("PLUGS r %.2f..%.2f, z %.2f +-%.2f, half width %.2f, oak %s" % (r0, r1, zc, hz, hw, tuple(round(float(x), 3) for x in oak)))


def core(ob):
    """The drum's staves are one thin shell with narrow gaps between them; light showed through (user, 11-07: "여기
    살짝 깨져 보이는 부분"). A closed core goes just inside the shell, following its radius height by height (rays from
    outside), 3 cm under it so no faces share a plane. Every corner takes the UV of an even, dark patch of the drum's
    own oak, so a gap now shows dark wood instead of the sky."""
    from mathutils.bvhtree import BVHTree
    b0 = bmesh.new(); b0.from_mesh(ob.data); tree = BVHTree.FromBMesh(b0); b0.free()
    hit, *_ = tree.ray_cast(Vector((0, 0, DRUM_TOP + 1)), Vector((0, 0, -1)))
    top = (hit.z if hit else DRUM_TOP) - .012
    rings = []
    for z in np.arange(.03, top, .05):
        rs = []
        for a in np.linspace(0, 2 * math.pi, 180, endpoint=False):
            d = Vector((math.cos(a), math.sin(a), 0))
            loc, *_ = tree.ray_cast(d * 2.0 + Vector((0, 0, z)), -d)
            if loc is not None and .42 < loc.xy.length < .95: rs.append(loc.xy.length)
        if len(rs) > 30: rings.append((float(z), float(np.percentile(rs, 3)) - .015))
    rings.append((top, rings[-1][1]))
    # dark even oak: the 32 px block with a wood hue, little variation and a low brightness
    img = next(n.image for n in ob.data.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE' and n.image
               and "Capstan_BaseColor" in n.image.name)
    w, h = img.size; B = 32
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[..., :3]
    blk = px[:h // B * B, :w // B * B].reshape(h // B, B, w // B, B, 3)
    mean, std = blk.mean((1, 3)), blk.std((1, 3)).mean(-1)
    mx, mn = mean.max(-1), mean.min(-1); sat = (mx - mn) / np.maximum(mx, 1e-4)
    wood = (mean[..., 0] > mean[..., 1]) & (mean[..., 1] > mean[..., 2]) & (sat > .4) & (std < .05)
    lum = mean.mean(-1); lim = np.percentile(lum[wood], 50)
    score = np.where(wood, np.abs(lum - lim) + std, 9)          # a mid-tone, even oak (dark read as cracks, user 11-07)
    by, bx = np.unravel_index(np.argmin(score), score.shape)
    uv = ((bx + .5) * B / w, (by + .5) * B / h)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new(ob.data.uv_layers.active.name)   # same layer as the model
    seg = 48; vs = []
    for z, r in rings:
        vs.append([bm.verts.new((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
    faces = []
    for a, b in zip(vs, vs[1:]):
        for i in range(seg):
            j = (i + 1) % seg; faces.append(bm.faces.new((a[i], a[j], b[j], b[i])))
    faces.append(bm.faces.new(vs[0][::-1])); faces.append(bm.faces.new(vs[-1]))
    for f in faces:
        for l in f.loops: l[uvl].uv = uv
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    me = bpy.data.meshes.new("core"); bm.to_mesh(me); bm.free()
    b2 = bmesh.new(); b2.from_mesh(ob.data); b2.from_mesh(me); b2.to_mesh(ob.data); b2.free()
    bpy.data.meshes.remove(me)
    print("CORE %d rings z %.2f..%.2f, r %.3f..%.3f, oak uv (%.3f, %.3f) colour %s" % (len(rings), rings[0][0], top,
          min(r for _, r in rings), max(r for _, r in rings), uv[0], uv[1], tuple(round(float(x), 3) for x in mean[by, bx])))


def main():
    src, bm = load()
    objs = split(src, bm); bm.free()
    fit_static(objs[0])
    for q in range(1, 5): fit_bar(objs[q], q)
    for q, ob in objs.items():
        print("DECIMATE", ob.name, decimate(ob, TARGET_STATIC if q == 0 else TARGET_BAR))
    retexture(objs[0].data.materials[0])
    core(objs[0])
    # Bars into one object (they turn together); static stays one object.
    bpy.ops.object.select_all(action='DESELECT')
    for q in range(1, 5): objs[q].select_set(True)
    bpy.context.view_layer.objects.active = objs[1]
    bpy.ops.object.join(); objs[1].name = objs[1].data.name = "D_Capstan_Bars"
    for q in range(2, 5): objs[q] = objs[1]
    for o in (objs[0], objs[1]):
        lo = [min(v.co[i] for v in o.data.vertices) for i in range(3)]
        hi = [max(v.co[i] for v in o.data.vertices) for i in range(3)]
        print("FIT", o.name, "tris", tris(o), "lo", [round(x, 3) for x in lo], "hi", [round(x, 3) for x in hi])
    if "--export" in ARGS: export(objs)
    proxies()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "D_Capstan_v1.blend"))
    if "--render" in ARGS: render("d_capstan")


main()
