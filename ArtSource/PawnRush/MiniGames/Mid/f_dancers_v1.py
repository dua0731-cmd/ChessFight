# F Clockwork Ballroom dancers (R508), modelled here in Blender (user 11-08: "첫번 째는 너가 만들어주고 인형 느낌나게 해줘"),
# after the GPT sheet Docs/PawnRush/ConceptArt/R508_ClockworkBallroom/AssetRefs/F_assets_01_dancers.png:
# a ball-gown dancer, a tailcoat dancer and a big wind-up key.
#
# Doll look: porcelain body with ball joints at the elbows, rosy cheeks, pleated fabric skirt with a lace hem,
# lace collar, draped swags pinned with brass beads, a ribbon bow at the back, stitched-cloth normal on the costume.
#
# Game numbers (MgEnPassant): statue 0.9 m tall, collider radius 0.38 m; the costume stays inside r 0.38 so it never
# hides the step or the body's turn. Front = Blender -y (the statues face the entrance), floor centre = origin.
#
# Materials (slots, so Unity can recolour the costume to the other team's colour):
#   F_DancerBody (cream porcelain)  F_DancerCostume (plain white cloth, tinted in Unity)  F_DancerTrim (cream lace)
#   PR_Mid_MatteBrass (B swatch)    F_DancerBlush (cheeks)
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python f_dancers_v1.py -- [--render] [--export]
#
# Outputs (Export/): F_DancerGown.fbx, F_DancerTailcoat.fbx (origin floor centre),
#                    F_WindKey.fbx (origin = the shaft root that sits in the back socket, shaft along +y = backwards)
import bpy, bmesh, math, os, sys, importlib.util
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "Materials", "Textures")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

_spec = importlib.util.spec_from_file_location("e_props", os.path.join(HERE, "e_props_v1.py"))
E = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(E)
to_obj, cyl, join, export, tris, BR = E.to_obj, E.cyl, E.join, E.export, E.tris, E.BR

SOCKET = Vector((0., .178, .515))          # back socket: the key's shaft root (both dancers)
HEAD_C, HEAD_R = Vector((0., 0., .745)), .15


# ------------------------------------------------------------------ materials
def cloth(name, color, rough=.85, normal=.5, scale=5.):
    if name in E.MATS: return E.MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1); b.inputs["Roughness"].default_value = rough
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(os.path.join(TEX, "PR_Mid_Canvas_Normal.png")); t.image.colorspace_settings.name = "Non-Color"
    uvn = nt.nodes.new("ShaderNodeUVMap"); mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (scale, scale, 1)
    nt.links.new(uvn.outputs[0], mp.inputs[0]); nt.links.new(mp.outputs[0], t.inputs[0])
    nm = nt.nodes.new("ShaderNodeNormalMap"); nm.inputs["Strength"].default_value = normal
    nt.links.new(t.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    E.MATS[name] = m
    return m


def BODY(): return E.mat("F_DancerBody", (.80, .70, .56), rough=.32)
def COSTUME(): return cloth("F_DancerCostume", (.86, .86, .84))
def TRIM(): return cloth("F_DancerTrim", (.86, .80, .66), rough=.75, normal=.35, scale=8.)
def BLUSH(): return E.mat("F_DancerBlush", (.82, .36, .33), rough=.55)


# ------------------------------------------------------------------ geometry helpers
def interp(profile, z):
    """Radius of a [(r, z)] profile (z rising) at height z."""
    for (r0, z0), (r1, z1) in zip(profile, profile[1:]):
        if z0 <= z <= z1:
            return r0 + (r1 - r0) * (z - z0) / max(z1 - z0, 1e-9)
    return profile[0][0] if z < profile[0][1] else profile[-1][0]


def lathe(profile, m, segs=32, rmod=None, zmod=None, cap0=True, cap1=True, name="lathe"):
    """Turned surface about z from [(r, z)] (rising), with optional radius/height modulation f(theta, z, i).
    Ends with r > 0 are closed by a centre fan when cap0/cap1."""
    bm = bmesh.new()
    rings = []
    for i, (r, z) in enumerate(profile):
        ring = []
        for k in range(segs):
            th = 2 * math.pi * k / segs
            rr = r + (rmod(th, z, i) if rmod else 0.)
            zz = z + (zmod(th, z, i) if zmod else 0.)
            ring.append(bm.verts.new((rr * math.cos(th), rr * math.sin(th), zz)))
        rings.append(ring)
    for A, B in zip(rings, rings[1:]):
        for k in range(segs):
            bm.faces.new((A[k], A[(k + 1) % segs], B[(k + 1) % segs], B[k]))
    if cap0:
        c = bm.verts.new((0, 0, profile[0][1]))
        for k in range(segs): bm.faces.new((rings[0][(k + 1) % segs], rings[0][k], c))
    if cap1:
        c = bm.verts.new((0, 0, profile[-1][1]))
        for k in range(segs): bm.faces.new((rings[-1][k], rings[-1][(k + 1) % segs], c))
    o = to_obj(bm, m, name)
    E.uv(o, None)
    return o


def sphere(c, r, m, u=16, v=10, scale=(1, 1, 1), M=None, name="sphere"):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    bm.transform(Matrix.Diagonal((*scale, 1)))
    if M is not None: bm.transform(M)
    bm.transform(Matrix.Translation(c))
    o = to_obj(bm, m, name)
    E.uv(o, None)
    return o


def tube(points, r, m, rsegs=6, closed=False, name="tube"):
    """A round tube along a polyline (ends capped when open)."""
    bm = bmesh.new()
    P = [Vector(p) for p in points]
    n = len(P)
    rings = []
    for i in range(n):
        a = P[(i - 1) % n] if (closed or i > 0) else P[i]
        b = P[(i + 1) % n] if (closed or i < n - 1) else P[i]
        t = (b - a).normalized()
        side = t.cross(Vector((0, 0, 1)))
        if side.length < 1e-4: side = t.cross(Vector((1, 0, 0)))
        side.normalize(); up = side.cross(t)
        rings.append([bm.verts.new(P[i] + r * (math.cos(2 * math.pi * k / rsegs) * side + math.sin(2 * math.pi * k / rsegs) * up))
                      for k in range(rsegs)])
    for i in range(n if closed else n - 1):
        A, B = rings[i], rings[(i + 1) % n]
        for k in range(rsegs):
            bm.faces.new((A[k], A[(k + 1) % rsegs], B[(k + 1) % rsegs], B[k]))
    if not closed:
        bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    o = to_obj(bm, m, name)
    E.uv(o, None)
    return o


def on_head(yaw_deg, pitch_deg, out=0.):
    """A point on the head and its outward normal: yaw 0 = straight to the front (-y), + to the dancer's left."""
    y, p = math.radians(yaw_deg), math.radians(pitch_deg)
    d = Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p)))
    return HEAD_C + d * (HEAD_R + out), d


# ------------------------------------------------------------------ shared doll parts
def base():
    return [lathe([(.27, 0.), (.29, .018), (.29, .045), (.255, .062)], BODY(), 28, name="base")]


def head_and_neck(collar_trim=True):
    parts = [cyl((0, 0, .565), (0, 0, .615), .055, BODY(), segs=16, grain=False),
             sphere(HEAD_C, HEAD_R, BODY(), 20, 12)]
    for s in (-1, 1):                                   # rosy cheeks: thin discs just proud of the porcelain
        c, d = on_head(s * 38, -12, .002)
        M = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        parts.append(sphere(c, .034, BLUSH(), 12, 6, scale=(1, 1, .18), M=M))
    return parts


def doll_arm(s, sleeve_upper, sleeve_lower=None, cuff=True):
    """Arm held a little out and forward. Shoulder and elbow are ball joints; hand a round mitten."""
    sh, el, wr = Vector((s * .175, 0., .525)), Vector((s * .245, -.035, .445)), Vector((s * .282, -.085, .395))
    parts = [sphere(sh + Vector((s * .012, 0, 0)), .052, sleeve_upper, 12, 7),     # puff / shoulder ball
             cyl(sh, el, .033, sleeve_upper, segs=10, bev=0., grain=False),
             sphere(el, .037, BODY(), 10, 6),                                          # elbow ball joint
             cyl(el, wr, .031, sleeve_lower or BODY(), segs=10, bev=0., grain=False),
             sphere(wr + (wr - el).normalized() * .03, .043, BODY(), 12, 7)]
    if cuff:
        parts.append(tube(ring_points(wr, (wr - el).normalized(), .036, 10), .011, TRIM(), 4, closed=True, name="cuff"))
    return parts


def ring_points(c, axis, R, n):
    axis = axis.normalized()
    u = axis.cross(Vector((0, 0, 1)))
    if u.length < 1e-4: u = axis.cross(Vector((1, 0, 0)))
    u.normalize(); v = axis.cross(u)
    return [c + R * (math.cos(2 * math.pi * k / n) * u + math.sin(2 * math.pi * k / n) * v) for k in range(n)]


def socket():
    return [cyl(SOCKET - Vector((0, .03, 0)), SOCKET, .026, BR(), segs=14, grain=False),
            cyl(SOCKET - Vector((0, .005, 0)), SOCKET + Vector((0, .004, 0)), .034, BR(), segs=14, grain=False)]


# ------------------------------------------------------------------ gown dancer
SKIRT = [(.33, .075), (.345, .11), (.337, .17), (.31, .23), (.27, .29), (.215, .35), (.165, .4), (.145, .425)]
PLEATS = 12


def skirt_r(th, z):
    a = .018 * max(0., (.425 - z) / .35)              # pleats deepen toward the hem
    return interp(SKIRT, z) + a * math.cos(PLEATS * th)


def gown():
    parts = base()
    parts.append(lathe(SKIRT, COSTUME(), 36, rmod=lambda th, z, i: skirt_r(th, z) - interp(SKIRT, z), name="skirt"))
    # lace hem: a scalloped cord following the pleats at the bottom edge
    hem = [Vector((math.cos(th) * (skirt_r(th, .085) + .008), math.sin(th) * (skirt_r(th, .085) + .008),
                   .082 + .012 * abs(math.sin(PLEATS * th)))) for th in [2 * math.pi * k / 72 for k in range(72)]]
    parts.append(tube(hem, .011, TRIM(), 4, closed=True, name="hem lace"))
    # bodice and waist sash
    parts.append(lathe([(.148, .415), (.16, .45), (.165, .49), (.15, .535), (.11, .57), (.06, .585)], COSTUME(), 24, cap0=False, name="bodice"))
    parts.append(tube(ring_points(Vector((0, 0, .425)), Vector((0, 0, 1)), .152, 28), .014, TRIM(), 6, closed=True, name="sash"))
    # lace collar ruffle: a pleated ring that waves up and down
    parts.append(lathe([(.055, .588), (.1, .582), (.138, .57), (.14, .56), (.1, .568), (.055, .574)], TRIM(), 42,
                       zmod=lambda th, z, i: .008 * math.cos(14 * th) * (1 if i in (2, 3) else .3), cap0=False, cap1=False, name="ruffle"))
    # swags: six double drapes pinned with brass beads (one pin at the very front)
    for k in range(6):
        t0 = -math.pi / 2 + k * math.pi / 3
        for drop, rr in ((.055, .013), (.085, .009)):
            pts = []
            for j in range(9):
                t = j / 8
                th = t0 + t * math.pi / 3
                z = .29 - drop * math.sin(math.pi * t)
                pts.append(Vector((math.cos(th) * (skirt_r(th, z) + .012), math.sin(th) * (skirt_r(th, z) + .012), z)))
            parts.append(tube(pts, rr, TRIM(), 5, name="swag"))
        parts.append(sphere(Vector((math.cos(t0) * (skirt_r(t0, .29) + .02), math.sin(t0) * (skirt_r(t0, .29) + .02), .29)), .021, BR(), 10, 6))
    # bodice buttons
    for z in (.465, .515):
        parts.append(sphere(Vector((0, -interp([(.148, .415), (.16, .45), (.165, .49), (.15, .535)], z) - .006, z)), .013, BR(), 8, 6))
    # ribbon bow at the back of the waist (below the key socket)
    for s in (-1, 1):
        parts.append(sphere(Vector((s * .045, .158, .43)), .04, TRIM(), 12, 8, scale=(1, .45, .62),
                            M=Matrix.Rotation(math.radians(-s * 18), 4, 'Y')))
        parts.append(tube([Vector((s * .01, .165, .42)), Vector((s * .03, .19, .36)), Vector((s * .05, .2, .3))], .012, TRIM(), 5, name="bow tail"))
    parts.append(sphere(Vector((0, .162, .43)), .02, TRIM(), 10, 6))
    for s in (-1, 1):
        parts += doll_arm(s, COSTUME())
    parts += head_and_neck() + socket()
    return parts


# ------------------------------------------------------------------ tailcoat dancer
LOWER = [(.255, .062), (.24, .1), (.21, .18), (.18, .26), (.16, .33), (.15, .37)]
COAT = [(.158, .345), (.168, .4), (.172, .47), (.16, .53), (.118, .568), (.06, .582)]


def tail(s):
    """One coat tail hanging at the back: a curved strip just off the lower body, narrowing to a rounded tip."""
    bm = bmesh.new()
    nu, nv = 7, 4
    grid = []
    for i in range(nu + 1):
        u = i / nu
        z = .43 - u * .3
        half = math.radians(26) * (1 - .55 * u ** 2)
        mid = math.radians(90 + s * 24 + s * 6 * u)
        row = []
        for j in range(nv + 1):
            th = mid - half + 2 * half * j / nv
            r = interp(LOWER + [(.158, .345), (.168, .4), (.172, .47)], z) + .022 + .025 * u
            row.append(bm.verts.new((r * math.cos(th), r * math.sin(th), z)))
        grid.append(row)
    for i in range(nu):
        for j in range(nv):
            bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=.016)
    o = to_obj(bm, COSTUME(), "coat tail")
    E.uv(o, None)
    return o


def tailcoat():
    parts = base()
    parts.append(lathe(LOWER, BODY(), 28, cap0=False, name="lower body"))
    parts.append(lathe(COAT, COSTUME(), 28, name="coat"))
    parts.append(tube(ring_points(Vector((0, 0, .348)), Vector((0, 0, 1)), .16, 28), .012, COSTUME(), 6, closed=True, name="coat hem"))
    parts += [tail(-1), tail(1)]
    # shirt front, lapels, bow tie, buttons, collar
    parts.append(E.box((-.042, -.19, .43), (.042, -.15, .565), TRIM(), bev=.012))
    for s in (-1, 1):
        lap = E.box((-.022, -.02, -.07), (.022, .02, .07), COSTUME(), bev=.008)
        lap.data.transform(Matrix.Translation((s * .07, -.165, .5)) @ Matrix.Rotation(math.radians(s * -22), 4, 'Y')
                           @ Matrix.Rotation(math.radians(s * 25), 4, 'Z'))
        parts.append(lap)
        parts.append(sphere(Vector((s * .033, -.175, .567)), .027, COSTUME(), 12, 8, scale=(1, .5, .7)))
    parts.append(sphere(Vector((0, -.18, .567)), .013, COSTUME(), 8, 6))
    for z in (.455, .49, .525):
        parts.append(sphere(Vector((0, -.193, z)), .011, BR(), 8, 6))
    parts.append(tube(ring_points(Vector((0, 0, .578)), Vector((0, 0, 1)), .064, 20), .012, TRIM(), 5, closed=True, name="shirt collar"))
    for s in (-1, 1):
        parts += doll_arm(s, COSTUME(), COSTUME())
    parts += head_and_neck() + socket()
    return parts


# ------------------------------------------------------------------ wind-up key (origin = shaft root, shaft toward +y)
def key():
    parts = [cyl((0, 0, 0), (0, .12, 0), .022, BR(), segs=12, bev=0., grain=False),
             cyl((0, .045, 0), (0, .072, 0), .036, BR(), segs=12, bev=.004, grain=False)]
    for z in (.052, -.052):                       # figure-eight bow, standing in the y-z plane, loops above and below
        t = E.torus(.044, .019, BR(), 16, 6)
        t.data.transform(Matrix.Translation((0, .17, z)) @ Matrix.Rotation(math.radians(90), 4, 'Z'))
        parts.append(t)
    parts.append(E.box((-.014, .118, -.03), (.014, .2, .03), BR(), bev=.008))
    return parts


def smooth(o, angle=50):
    """Porcelain and cloth read soft: smooth normals, with sharp edges only where the surface folds sharply."""
    for p in o.data.polygons: p.use_smooth = True
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
    return o


def export(o, name):
    os.makedirs(E.OUT_FBX, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(E.OUT_FBX, name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='OFF', path_mode='STRIP', bake_space_transform=True)


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    E.MATS.clear()
    built = {"F_DancerGown": join(gown(), "F_DancerGown"), "F_DancerTailcoat": join(tailcoat(), "F_DancerTailcoat"),
             "F_WindKey": join(key(), "F_WindKey")}
    # 32 dancers stand in the room: decimate to the trimmed targets (3,000~4,000) after modelling
    target = {"F_DancerGown": 4000, "F_DancerTailcoat": 3500}
    for n, o in built.items():
        if n in target and tris(o) > target[n]:
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
            md = o.modifiers.new("dec", 'DECIMATE'); md.ratio = target[n] / tris(o); md.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier="dec")
        smooth(o)
        p = np.array([v.co[:] for v in o.data.vertices])
        r = float(np.max(np.hypot(p[:, 0], p[:, 1])))
        print("PART", n, tris(o), "tris", "height %.3f" % p[:, 2].max(), "max r %.3f" % r, [m.name for m in o.data.materials])
    return built


def render(built):
    """3/4 front and 3/4 back of both dancers (costumes tinted orange / blue as in the game), a key in each socket."""
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.95, .9, .82, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .7
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30))); bpy.context.object.data.energy = 3.2
    bpy.ops.object.light_add(type='AREA', location=(1.5, 1.5, 1.6)); bpy.context.object.data.energy = 120
    ground = bpy.data.materials.new("ground"); ground.use_nodes = True
    ground.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.75, .66, .52, 1)
    bpy.ops.mesh.primitive_plane_add(size=8); bpy.context.object.data.materials.append(ground)
    tint = {"F_DancerGown": (.85, .32, .06), "F_DancerTailcoat": (.08, .2, .5)}
    for n, x in (("F_DancerGown", -.45), ("F_DancerTailcoat", .45)):
        o = built[n]; o.location.x = x
        m = E.MATS["F_DancerCostume"].copy(); m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*tint[n], 1)
        o.data = o.data.copy()
        for i, s in enumerate(o.data.materials):
            if s.name == "F_DancerCostume": o.data.materials[i] = m
        k = bpy.data.objects.new("key", built["F_WindKey"].data); sc.collection.objects.link(k)
        k.location = SOCKET + Vector((x, 0, 0)); k.rotation_euler = (0, math.radians(20), 0)
    built["F_WindKey"].location = (0, -.7, .03); built["F_WindKey"].rotation_euler = (math.radians(90), 0, math.radians(-50))
    cd = bpy.data.cameras.new("cam"); cd.lens = 70; cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    tgt = Vector((0, -.1, .42))
    for view, off in (("front", Vector((-1.2, -3.2, 1.05))), ("back", Vector((1.3, 3.1, 1.1)))):
        cam.location = tgt + off
        cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_f_dancers_%s.png" % view); bpy.ops.render.render(write_still=True)


def main():
    built = build()
    if "--export" in ARGS:
        for n, o in built.items(): export(o, n)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "F_Dancers_v1.blend"))
    if "--render" in ARGS: render(built)


if __name__ == "__main__":
    main()
