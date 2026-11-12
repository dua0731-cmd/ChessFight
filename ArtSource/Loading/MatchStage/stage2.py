# R609 mockup: backgrounds behind the matching screen's chessboard and pawn in the game's own world (승규 님 11-12:
# the LED stage "우리 게임하고 잘 안어울려 다른 시안 줘").
#   D  성문 앞 출발선: the PAWN RUSH picture - blue sky, cream castle with rook towers, red and cream blocks, crown arch
#   E  왕국 홀: the lobby's kingdom hall at sunset - cream stone, columns, rose window light, red carpet, banners
#   F  거대 체스 말 광장: a plaza at golden hour ringed by giant ivory and ebony piece statues
# blender -b --factory-startup --python stage2.py -- <D|E|F> <out.png> <scale%>
import bpy, math, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
VAR, OUT, PCT = argv[0], argv[1], int(argv[2]) if len(argv) > 2 else 100

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
sc = bpy.context.scene
for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
    try: sc.render.engine = eng; break
    except TypeError: pass
sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 1280, 720, PCT
sc.render.filepath = OUT
sc.render.image_settings.file_format = 'PNG'
try: sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'
except Exception:
    try: sc.view_settings.view_transform = 'Filmic'
    except Exception: pass
ee = sc.eevee
for k, v in (('taa_render_samples', 48), ('volumetric_tile_size', '4'), ('volumetric_samples', 64), ('volumetric_end', 120.0),
             ('use_shadows', True), ('volumetric_light_clamp', 0.0), ('use_raytracing', True)):
    try: setattr(ee, k, v)
    except Exception: pass


def srgb(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c)


MATS = {}


def mat(name, color, rough=.45, metal=0., emit=None, estr=0., coat=0.):
    key = (name, color, rough, metal, emit, estr, coat)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*color, 1); b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if coat:
        try: b.inputs['Coat Weight'].default_value = coat; b.inputs['Coat Roughness'].default_value = .08
        except Exception: pass
    if emit is not None:
        b.inputs['Emission Color'].default_value = (*emit, 1); b.inputs['Emission Strength'].default_value = estr
    MATS[key] = m
    return m


def emit_mat(name, color, strength):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    e = nt.nodes.new('ShaderNodeEmission'); e.inputs[0].default_value = (*color, 1); e.inputs[1].default_value = strength
    o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(e.outputs[0], o.inputs[0])
    return m


def checker_mat(name, c1, c2, scale, rough=.25, coat=.6):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    tc = nt.nodes.new('ShaderNodeTexCoord'); ck = nt.nodes.new('ShaderNodeTexChecker')
    ck.inputs['Scale'].default_value = scale
    ck.inputs['Color1'].default_value = (*c1, 1); ck.inputs['Color2'].default_value = (*c2, 1)
    nt.links.new(tc.outputs['Object'], ck.inputs['Vector']); nt.links.new(ck.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = rough
    try: b.inputs['Coat Weight'].default_value = coat; b.inputs['Coat Roughness'].default_value = .05
    except Exception: pass
    return m


def box(name, loc, size, m, rot=(0, 0, 0), bevel=0.):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    o = bpy.context.object; o.name = name; o.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    o.data.materials.append(m)
    if bevel:
        bpy.ops.object.transform_apply(scale=True)
        mod = o.modifiers.new('bev', 'BEVEL'); mod.width = bevel; mod.segments = 3
        o.data.shade_smooth() if hasattr(o.data, 'shade_smooth') else None
    return o


def cyl(name, loc, r, d, m, verts=64, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d, location=loc, rotation=rot)
    o = bpy.context.object; o.name = name; o.data.materials.append(m)
    bpy.ops.object.shade_smooth(); return o


def cone(name, loc, r, d, m, verts=48):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=0, depth=d, location=loc)
    o = bpy.context.object; o.name = name; o.data.materials.append(m); bpy.ops.object.shade_smooth(); return o


def sphere(name, loc, r, m, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=32, ring_count=16)
    o = bpy.context.object; o.name = name; o.scale = scale; o.data.materials.append(m); bpy.ops.object.shade_smooth(); return o


def torus(name, loc, R, r, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=rot, major_segments=96, minor_segments=12)
    o = bpy.context.object; o.name = name; o.data.materials.append(m); bpy.ops.object.shade_smooth(); return o


def lathe(name, prof, m, loc, s=1., seg=64):
    mesh = bpy.data.meshes.new(name); verts = []; faces = []
    for k in range(seg):
        a = 2 * math.pi * k / seg
        for (r, z) in prof: verts.append((r * math.cos(a), r * math.sin(a), z))
    n = len(prof)
    for k in range(seg):
        for p in range(n - 1):
            a0, a1 = k * n + p, ((k + 1) % seg) * n + p
            faces.append((a0, a1, a1 + 1, a0 + 1))
    mesh.from_pydata(verts, [], faces); mesh.update()
    for p in mesh.polygons: p.use_smooth = True
    o = bpy.data.objects.new(name, mesh); sc.collection.objects.link(o)
    o.data.materials.append(m); o.location = loc; o.scale = (s, s, s)
    return o


def light(kind, loc, color, energy, aim=None, size=.3, spot=math.radians(30), blend=.3, angle=None):
    d = bpy.data.lights.new(kind + str(len(bpy.data.lights)), kind)
    d.color = color; d.energy = energy
    if kind == 'SPOT': d.spot_size = spot; d.spot_blend = blend; d.shadow_soft_size = size
    if kind == 'AREA': d.size = size
    if kind == 'POINT': d.shadow_soft_size = size
    if kind == 'SUN' and angle is not None: d.angle = angle
    o = bpy.data.objects.new(d.name, d); sc.collection.objects.link(o); o.location = loc
    if aim is not None:
        o.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return o


# ---- the board and the pawn (kept from the current screen) ----
WAL, MAP, EBO, IVO, GOLD = srgb('5a3420'), srgb('d9ad74'), srgb('1b120c'), srgb('efe3c8'), srgb('d4a24c')
sq_dark, sq_light = mat('walnut', WAL, .25, coat=.5), mat('maple', MAP, .25, coat=.5)
for i in range(8):
    for j in range(8):
        box('sq', (i - 3.5, j - 3.5, .05), (1, 1, .1), sq_light if (i + j) % 2 else sq_dark)
box('frame', (0, 0, -.06), (8.9, 8.9, .3), mat('ebony', EBO, .22, coat=.5))
gold = mat('gold', GOLD, .25, metal=1.)
for s in (-1, 1):
    box('inlay', (0, s * 4.32, .1), (8.7, .07, .03), gold); box('inlay', (s * 4.32, 0, .1), (.07, 8.7, .03), gold)

PAWN = [(0, 0), (.42, 0), (.44, .05), (.42, .1), (.34, .14), (.3, .2), (.22, .42), (.17, .6), (.27, .64), (.27, .7), (.15, .74), (.14, .8)]
ivory = mat('ivory', IVO, .3, coat=.3)
pawn = lathe('pawn', PAWN, ivory, (-1.0, -.6, .1), 1.55)
sphere('head', (-1.0, -.6, .1 + 1.55 * .9), .25 * 1.55, ivory)

# ---- camera, as the current screen frames the board ----
cam_d = bpy.data.cameras.new('cam'); cam_d.lens = 28
cam = bpy.data.objects.new('cam', cam_d); sc.collection.objects.link(cam); sc.camera = cam
cam.location = (0, -10.4, 4.3)
AIM_Z = 1.25 if VAR in ('G', 'H', 'I') else .7
cam.rotation_euler = (Vector((0, 3.0, AIM_Z)) - cam.location).to_track_quat('-Z', 'Y').to_euler()

world = bpy.data.worlds.new('w'); sc.world = world; world.use_nodes = True
wn = world.node_tree; bg = next(n for n in wn.nodes if n.type == 'BACKGROUND')


def sky(top, horizon, strength=1.):
    """A vertical gradient sky: `horizon` at the horizon, `top` overhead."""
    tc = wn.nodes.new('ShaderNodeTexCoord'); sep = wn.nodes.new('ShaderNodeSeparateXYZ')
    ramp = wn.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.0; ramp.color_ramp.elements[0].color = (*horizon, 1)
    ramp.color_ramp.elements[1].position = .45; ramp.color_ramp.elements[1].color = (*top, 1)
    wn.links.new(tc.outputs['Generated'], sep.inputs[0])
    mp = wn.nodes.new('ShaderNodeMapRange'); mp.inputs['From Min'].default_value = .5; mp.inputs['From Max'].default_value = 1.
    wn.links.new(sep.outputs['Z'], mp.inputs['Value']); wn.links.new(mp.outputs['Result'], ramp.inputs['Fac'])
    wn.links.new(ramp.outputs['Color'], bg.inputs[0]); bg.inputs[1].default_value = strength


def haze(density, color=(1, 1, 1), anis=0.):
    vol = wn.nodes.new('ShaderNodeVolumePrincipled'); vol.inputs['Density'].default_value = density
    vol.inputs['Color'].default_value = (*color, 1)
    try: vol.inputs['Anisotropy'].default_value = anis
    except Exception: pass
    out = next(n for n in wn.nodes if n.type == 'OUTPUT_WORLD'); wn.links.new(vol.outputs[0], out.inputs['Volume'])


def crown(loc, s, m, gem=None):
    """A gold crown: a band, five points with balls, a cushion."""
    x, y, z = loc
    cyl('crown band', (x, y, z), 1.0 * s, .55 * s, m, verts=48)
    for k in range(5):
        a = math.radians(-90 + (k - 2) * 34)
        px, py = x + math.cos(a) * .95 * s, y + math.sin(a) * .95 * s
        cone('crown point', (px, py, z + .6 * s), .28 * s, .9 * s, m, verts=16)
        sphere('crown ball', (px, py, z + 1.08 * s), .14 * s, m)
    if gem is not None:
        sphere('gem', (x, y - 1.0 * s, z), .17 * s, gem)


CREAM, STONE, RED, BLUE, PURPLE = srgb('f1e4c6'), srgb('e3d2ad'), srgb('d8443c'), srgb('2f6fd6'), srgb('6a3fa8')

if VAR == 'D':
    # the PAWN RUSH picture: the start line in front of the castle on a sunny day
    sky(srgb('3f8ae6'), srgb('cfe6fb'), 1.2)
    light('SUN', (0, 0, 20), srgb('fff1d8'), 4.2, aim=(4, 10, 0), angle=math.radians(2.5))
    haze(.006, srgb('d6ecff'))
    # the checker runway running back to the arch, cream and slate blue
    run = checker_mat('runway', srgb('efe6d2'), srgb('7d93b8'), 1.0, rough=.18, coat=.7)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 22, -.28)); p = bpy.context.object
    p.scale = (10, 52, 1); p.data.materials.append(run)
    box('grass', (0, 30, -.4), (200, 160, .2), mat('lawn', srgb('7fbf5a'), .8))
    # chunky red and cream blocks along both sides
    for s in (-1, 1):
        for k in range(14):
            y = -3 + k * 2.1
            c = RED if k % 2 == 0 else CREAM
            box('block', (s * 6.4, y, .55), (1.5, 1.9, 1.7), mat('block', c, .35, coat=.4), bevel=.12)
    # the coral finish arch with a big gold crown and purple banners
    arch = mat('arch', srgb('e45a4a'), .3, coat=.5)
    for s in (-1, 1):
        box('pillar', (s * 4.6, 17, 3.8), (1.3, 1.3, 8.4), arch, bevel=.15)
        box('banner', (s * 4.6, 16.25, 5.5), (1.0, .06, 2.6), mat('purple', PURPLE, .6))
        crown((s * 4.6, 16.2, 5.9), .32, gold)
        for f in range(2):
            box('flagpole', (s * (6.2 + f * 1.4), 16.6, 2.2), (.08, .08, 4.8), mat('pole', srgb('ffffff'), .4))
            box('flag', (s * (6.2 + f * 1.4) + s * .45, 16.6, 4.2), (.9, .03, .6), checker_mat('cflag', srgb('ffffff'), srgb('111111'), 6))
    box('beam', (0, 17, 8.4), (10.6, 1.4, 1.3), arch, bevel=.15)
    crown((0, 17, 9.6), 1.15, gold, mat('ruby', srgb('d02040'), .15))
    # the cream stone castle behind: wall, rook towers, roofs, banners
    stone = mat('stone', STONE, .7)
    box('wall', (0, 34, 4.5), (70, 3, 9), stone)
    for k in range(-17, 18):
        box('merlon', (k * 2.0, 34, 9.4), (1.1, 3, .9), stone)
    roofs = [BLUE, RED, BLUE, RED, BLUE, RED]
    for i, x in enumerate((-26, -15, -6.5, 6.5, 15, 26)):
        h = 13 if abs(x) < 10 else 11
        cyl('tower', (x, 33, h / 2), 2.6, h, stone)
        for k in range(10):
            a = 2 * math.pi * k / 10
            box('tm', (x + 2.5 * math.cos(a), 33 + 2.5 * math.sin(a), h + .45), (.9, .9, .9), stone, rot=(0, 0, a))
        if abs(x) < 10 or i in (0, 5):
            cone('roof', (x, 33, h + 3.0), 2.9, 4.6, mat('roof', roofs[i], .4))
        box('tban', (x, 30.3, h - 3.5), (1.4, .06, 3.2), mat('tban', BLUE if x < 0 else RED, .6))
        crown((x, 30.2, h - 3.0), .28, gold)
    # far towers in the haze, and fluffy clouds
    for x in (-40, -33, 33, 42):
        cyl('far tower', (x, 60, 9), 3, 18, stone); cone('far roof', (x, 60, 20.5), 3.3, 5, mat('roof', BLUE, .4))
    cloud = mat('cloud', srgb('ffffff'), 1.0, emit=srgb('ffffff'), estr=.35)
    for (cx, cz, r) in ((-28, 26, 5), (-20, 29, 4), (22, 27, 5.5), (31, 30, 4), (2, 33, 3.5), (-6, 31, 3)):
        for k in range(5):
            sphere('cloud', (cx + (k - 2) * r * .55, 90, cz + math.sin(k * 1.7) * r * .25), r * (.7 + .1 * (k % 3)), cloud, scale=(1.2, .6, .8))

elif VAR == 'E':
    # the lobby's kingdom hall at sunset: cream stone, columns, a rose window pouring orange light, red carpet
    sky(srgb('2a1a14'), srgb('3a2418'), .4)
    marble = checker_mat('marble', srgb('efe4cf'), srgb('b9a68a'), 1.0, rough=.12, coat=.8)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 20, -.3)); p = bpy.context.object
    p.scale = (60, 70, 1); p.data.materials.append(marble)
    box('carpet', (0, 15, -.27), (5.2, 26, .04), mat('carpet', srgb('9b1f24'), .85))
    for s in (-1, 1):
        box('carpet gold', (s * 2.7, 15, -.26), (.18, 26, .05), gold)
    stone = mat('hall stone', srgb('e9dcc0'), .6)
    # the back wall with grand stairs and the rose window
    box('back wall', (0, 30, 12), (60, 2, 26), stone)
    for k in range(8):
        box('step', (0, 23 + k * .8, -.1 + k * .42), (16 - k * .4, .9, .45), stone)
    torus('rose frame', (0, 28.9, 12.5), 5.2, .45, stone, rot=(math.radians(90), 0, 0))
    glass = emit_mat('rose glass', srgb('ffb066'), 9)
    cyl('rose', (0, 29.05, 12.5), 5.0, .2, glass, rot=(math.radians(90), 0, 0))
    for k in range(12):
        a = math.pi * k / 12
        box('spoke', (0, 28.9, 12.5), (10, .3, .22), stone, rot=(0, a, 0))
    torus('rose inner', (0, 28.85, 12.5), 1.6, .25, stone, rot=(math.radians(90), 0, 0))
    # sun through the window: a strong warm spot from behind, haze to show the shafts
    light('SPOT', (0, 40, 22), srgb('ff9a4a'), 90000, aim=(0, 6, 0), spot=math.radians(32), blend=.5, size=1.2)
    light('AREA', (0, -6, 12), srgb('ffe2b8'), 2200, aim=(0, 4, 0), size=14)
    haze(.022, srgb('ffd9b0'), .5)
    # columns and arches along both sides, banners between them
    for s in (-1, 1):
        for k in range(5):
            y = 2 + k * 5.5
            x = s * 10.5
            cyl('column', (x, y, 8), .85, 16, stone, verts=32)
            box('base', (x, y, .2), (2.2, 2.2, 1), stone); box('capital', (x, y, 16.2), (2.2, 2.2, .7), stone)
            if k < 4:
                box('banner', (x - s * .1, y + 2.75, 9), (.06, 1.8, 6.5), mat('hban', BLUE if s < 0 else RED, .7), )
                crown((x - s * .2, y + 2.75, 10.8), .3, gold)
                light('POINT', (x - s * 1.4, y + 2.75, 6), srgb('ffb35c'), 260, size=.2)
                sphere('lamp', (x - s * 1.4, y + 2.75, 6), .18, emit_mat('lamp', srgb('ffc27a'), 25))
        box('side wall', (s * 13, 14, 10), (1, 40, 22), stone)
        box('arch beam', (s * 10.5, 14, 17), (2.2, 30, 1.2), stone)
    box('ceiling', (0, 14, 21), (30, 40, 1), mat('ceiling', srgb('5a3a24'), .7))
    # a throne on the landing with a gold crown above
    box('throne', (0, 27.2, 4.4), (2.2, 1.6, 4), mat('throne', srgb('7a1a20'), .5))
    crown((0, 27.0, 7.4), .55, gold, mat('ruby', srgb('d02040'), .15))

elif VAR == 'P':
    # R609: the board and the pawn alone on a transparent picture, to lay over the 2D backgrounds in the reveal's look
    sc.render.film_transparent = True
    sky(srgb('15110d'), srgb('15110d'), .6)
    light('AREA', (0, -3, 9), srgb('ffe6c0'), 700, aim=(0, 0, 0), size=6)
    light('AREA', (-6, 6, 5), srgb('fff6e2'), 220, aim=(0, 0, 0), size=4)

elif VAR in ('G', 'H', 'I'):
    # 승규 님 11-12 "이게 아니야 매칭 씬 느낌이 안나잖아": the game's own places, staged as a matching screen - night, the
    # white team's side lit blue and the black team's red, beams through haze, gold in the middle where they meet.
    # Only about the lowest 8 m of the back is in frame, so everything that matters sits low.
    TB, TR, WARM = srgb('3d8cff'), srgb('ff3d55'), srgb('ffcf8a')
    sky(srgb('05060c'), srgb('0d0f1c'), .5)
    light('AREA', (0, -3, 9), WARM, 500, aim=(0, 0, 0), size=6)   # soft key over the board, as on the screen now

    def team_lights(y0, z, energy, spread=18):
        for s, col in ((-1, TB), (1, TR)):
            for k in range(3):
                x = s * (3.5 + 3.2 * k)
                light('SPOT', (x, y0 + 3 * k, z), col, energy, aim=(s * (1.2 + 1.8 * k), -1 + 2.5 * k, 0),
                      spot=math.radians(spread), blend=.45, size=.15)
        light('SPOT', (0, y0 + 4, z + 1), WARM, energy * .8, aim=(0, 0, 0), spot=math.radians(24), blend=.5, size=.2)

    def team_banner(x, y, z, s, col, h=4.2):
        box('banner', (x, y, z), (1.5, .08, h), mat('tb', col, .6))
        box('banner rod', (x, y - .05, z + h / 2 + .1), (1.9, .12, .12), gold)
        crown((x, y - .12, z + h * .15), .32, gold)

    if VAR == 'G':
        # 왕국 결투장: a castle courtyard at night, each team's gate on its side, the crown gate in the middle
        floor = mat('wet stone', srgb('1a1714'), .14, coat=.6)
        box('floor', (0, 20, -.45), (90, 80, .2), floor)
        stone = mat('night stone', srgb('b9a98a'), .7)
        Y = 15.5
        box('back wall', (0, Y + 1, 2.8), (60, 2, 5.6), stone)
        for k in range(-14, 15):
            box('merlon', (k * 2.0, Y + 1, 5.95), (1.1, 2, .7), stone)
        # the crown gate: an arch glowing gold, a big crown over it
        for s in (-1, 1):
            box('gate post', (s * 2.6, Y - .4, 2.3), (1.2, 1.6, 4.6), stone)
        box('gate top', (0, Y - .4, 4.9), (6.4, 1.6, 1.0), stone)
        box('gate glow', (0, Y + .1, 2.3), (4.0, .2, 4.6), emit_mat('gate glow', srgb('ffc66a'), 7))
        crown((0, Y - .6, 5.75), .55, gold, mat('ruby', srgb('d02040'), .15))
        # each team's portcullis gate, lit from inside in its colour
        bars = mat('iron', srgb('1a1a1e'), .4, metal=.8)
        for s, col in ((-1, TB), (1, TR)):
            gx = s * 9.5
            box('team gate wall', (gx, Y, 2.6), (5.6, 2.4, 5.2), stone)
            box('team gate light', (gx, Y - 1.25, 1.9), (3.0, .2, 3.8), emit_mat('tg', col, 6))
            for b in range(-3, 4):
                box('bar', (gx + b * .4, Y - 1.4, 1.9), (.08, .08, 3.8), bars)
            for b in range(3):
                box('bar h', (gx, Y - 1.4, .7 + b * 1.2), (3.0, .08, .08), bars)
            team_banner(s * 5.4, Y - .1, 3.0, s, col, 3.2)
            team_banner(s * 13.8, Y - .1, 3.0, s, col, 3.2)
            for t in range(2):
                tx = s * (6.8 + t * 5.6)
                light('POINT', (tx, Y - 1.2, 4.2), srgb('ff9a40'), 260, size=.15)
                sphere('torch', (tx, Y - 1.2, 4.2), .18, emit_mat('torch', srgb('ffb050'), 30))
        team_lights(6, 11, 12000)
        haze(.02, srgb('ffffff'), .3)

    elif VAR == 'H':
        # 출발 게이트: Pawn Rush's start line at dusk, the start arch with its countdown lamps, the castle behind
        sky(srgb('101632'), srgb('5a3a5c'), .9)
        run = checker_mat('runway', srgb('d9d0bc'), srgb('4a5a7a'), 1.0, rough=.16, coat=.7)
        bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 24, -.28)); p = bpy.context.object
        p.scale = (10, 50, 1); p.data.materials.append(run)
        box('ground', (0, 30, -.42), (200, 160, .2), mat('dusk ground', srgb('20201e'), .6))
        for s in (-1, 1):
            for k in range(12):
                c = RED if k % 2 == 0 else CREAM
                box('block', (s * 6.4, -3 + k * 2.1, .55), (1.5, 1.9, 1.7), mat('block', c, .35, coat=.4), bevel=.12)
        arch = mat('arch', srgb('e45a4a'), .3, coat=.5)
        for s in (-1, 1):
            box('pillar', (s * 4.8, 13, 2.8), (1.3, 1.3, 6.4), arch, bevel=.15)
        box('beam', (0, 13, 6.4), (11, 1.4, 1.4), arch, bevel=.15)
        # five countdown lamps on the beam: lit gold, the last dark, waiting
        for k in range(5):
            x = (k - 2) * 1.5
            box('lamp cup', (x, 12.25, 6.4), (1.1, .2, 1.1), mat('cup', srgb('15110d'), .4))
            on = k < 3
            sphere('lamp', (x, 12.15, 6.4), .42, emit_mat('lamp', srgb('ffc04a') if on else srgb('3a2a12'), 22 if on else .5), scale=(1, .5, 1))
        crown((0, 13, 7.6), .8, gold, mat('ruby', srgb('d02040'), .15))
        # each team's flag on its side of the arch
        for s, col in ((-1, TB), (1, TR)):
            team_banner(s * 4.8, 12.3, 3.2, s, col, 3.0)
            box('pole', (s * 8.5, 13, 3.2), (.12, .12, 7), gold)
            box('flag', (s * 9.3, 13, 5.7), (1.5, .05, 1.0), mat('pflag', col, .6))
        # the castle in silhouette, windows lit
        stone = mat('dusk stone', srgb('6a5f55'), .8)
        box('castle wall', (0, 42, 4), (80, 3, 8), stone)
        for x in (-22, -12, 12, 22):
            cyl('tower', (x, 41, 5.5), 2.6, 11, stone)
            cone('roof', (x, 41, 13.3), 2.9, 4.6, mat('roof', BLUE if x < 0 else RED, .5))
            for w in range(2):
                box('window', (x, 38.3, 3 + w * 3), (.6, .1, 1.1), emit_mat('window', srgb('ffb860'), 6))
        team_lights(4, 11, 10000)
        haze(.03, srgb('ffffff'), .3)

    else:
        # 왕국 홀 입장식: the lobby's kingdom hall at night, team light from each side, the rose window glowing low
        marble = checker_mat('marble', srgb('d8ccb4'), srgb('2a2420'), 1.0, rough=.1, coat=.9)
        bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 20, -.3)); p = bpy.context.object
        p.scale = (60, 70, 1); p.data.materials.append(marble)
        box('carpet', (0, 15, -.27), (5.2, 26, .04), mat('carpet', srgb('7a1a1e'), .85))
        for s in (-1, 1):
            box('carpet gold', (s * 2.7, 15, -.26), (.18, 26, .05), gold)
        stone = mat('hall stone', srgb('cfc1a3'), .6)
        box('back wall', (0, 31, 10), (60, 2, 22), stone)
        for k in range(8):
            box('step', (0, 23 + k * .8, -.1 + k * .42), (16 - k * .4, .9, .45), stone)
        cx, cz = 0, 7.4
        torus('rose frame', (cx, 29.9, cz), 3.6, .35, stone, rot=(math.radians(90), 0, 0))
        cyl('rose', (cx, 30.05, cz), 3.45, .2, emit_mat('rose glass', srgb('ffb066'), 7), rot=(math.radians(90), 0, 0))
        for k in range(12):
            box('spoke', (cx, 29.9, cz), (7, .3, .16), stone, rot=(0, math.pi * k / 12, 0))
        torus('rose inner', (cx, 29.85, cz), 1.1, .2, stone, rot=(math.radians(90), 0, 0))
        crown((0, 27.0, 4.4), .5, gold, mat('ruby', srgb('d02040'), .15))
        for s, col in ((-1, TB), (1, TR)):
            for k in range(4):
                y = 6 + k * 5.5
                x = s * 9.5
                cyl('column', (x, y, 8), .8, 16, stone, verts=32)
                box('base', (x, y, .2), (2, 2, 1), stone)
                team_banner(x - s * .9, y + 2.75, 4.2, s, col, 5)
                light('POINT', (x - s * 1.2, y + 2.75, 1.0), col, 180, size=.2)
            box('side wall', (s * 12.5, 14, 10), (1, 40, 22), stone)
        light('SPOT', (0, 40, 14), srgb('ff9a4a'), 40000, aim=(0, 4, 0), spot=math.radians(26), blend=.5, size=1.0)
        team_lights(6, 12, 11000)
        haze(.035, srgb('ffffff'), .35)

else:
    # a plaza at golden hour ringed by giant ivory and ebony chess statues
    sky(srgb('3b5fa8'), srgb('ffb070'), 1.1)
    light('SUN', (0, 0, 20), srgb('ffbf80'), 3.6, aim=(-6, -8, -4.5), angle=math.radians(1.5))
    light('AREA', (0, -8, 10), srgb('ffe8cc'), 900, aim=(0, 4, 0), size=12)
    haze(.012, srgb('ffd2a0'), .4)
    tiles = checker_mat('plaza', srgb('eadbb9'), srgb('cbb48b'), .5, rough=.35, coat=.4)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 30, -.3)); p = bpy.context.object
    p.scale = (160, 120, 1); p.data.materials.append(tiles)
    big_ivory, big_ebony = mat('big ivory', srgb('efe3c8'), .28, coat=.5), mat('big ebony', srgb('1d1612'), .22, coat=.6)
    ROOK = [(0, 0), (1.0, 0), (1.0, .14), (.86, .2), (.66, .32), (.58, 1.3), (.74, 1.38), (.74, 1.72), (0, 1.72)]
    BISHOP = [(0, 0), (1.0, 0), (1.0, .14), (.84, .2), (.6, .34), (.42, 1.2), (.62, 1.28), (.36, 1.36), (.5, 1.62), (.44, 1.86), (.22, 2.04), (.12, 2.12), (0, 2.16)]
    KING = [(0, 0), (1.0, 0), (1.0, .14), (.84, .2), (.6, .36), (.44, 1.5), (.7, 1.6), (.5, 1.7), (.66, 2.1), (.4, 2.22), (0, 2.24)]
    QUEEN = [(0, 0), (1.0, 0), (1.0, .14), (.84, .2), (.6, .36), (.4, 1.5), (.66, 1.58), (.46, 1.68), (.7, 2.08), (.5, 2.14), (.2, 2.22), (0, 2.24)]
    statues = [(-17, 22, ROOK, big_ebony, 5.5), (-10.5, 26, BISHOP, big_ivory, 5.2), (-3.6, 30, QUEEN, big_ebony, 6.2),
               (3.6, 30, KING, big_ivory, 6.4), (10.5, 26, BISHOP, big_ebony, 5.2), (17, 22, ROOK, big_ivory, 5.5)]
    for (x, y, prof, m, s) in statues:
        cyl('plinth', (x, y, .9), 1.35 * s / 1.0 * .78, 2.2, mat('plinth', srgb('d9c7a0'), .6))
        o = lathe('statue', prof, m, (x, y, 2.0), s)
        top = 2.0 + prof[-1][1] * s
        if prof is ROOK:
            for k in range(6):
                a = 2 * math.pi * k / 6
                box('rook merlon', (x + .62 * s * math.cos(a), y + .62 * s * math.sin(a), top + .3 * s), (.34 * s, .34 * s, .5 * s), m, rot=(0, 0, a))
        if prof is KING:
            box('cross v', (x, y, top + .5 * s), (.16 * s, .16 * s, .8 * s), m); box('cross h', (x, y, top + .62 * s), (.5 * s, .16 * s, .16 * s), m)
        if prof is QUEEN:
            for k in range(8):
                a = 2 * math.pi * k / 8
                sphere('queen ball', (x + .5 * s * math.cos(a), y + .5 * s * math.sin(a), top - .04 * s), .1 * s, m)
            sphere('queen top', (x, y, top + .12 * s), .14 * s, m)
        if prof is BISHOP:
            sphere('bishop top', (x, y, top + .08 * s), .1 * s, m)
    # banner poles along the plaza with blue and red crown flags
    for s in (-1, 1):
        for k in range(3):
            x, y = s * (8 + k * 1.5), 6 + k * 5
            box('pole', (x, y, 4), (.14, .14, 8.4), gold)
            box('flag', (x, y, 6.8), (.05, 1.6, 2.4), mat('pflag', BLUE if s < 0 else RED, .6))
    # distant hills and a city line in the haze
    for k in range(-6, 7):
        sphere('hill', (k * 16, 110, -8), 18 + 4 * (k % 3), mat('hill', srgb('8a7a6a'), .9), scale=(1.4, 1, .6))

bpy.ops.render.render(write_still=True)
print('RENDERED', OUT)
