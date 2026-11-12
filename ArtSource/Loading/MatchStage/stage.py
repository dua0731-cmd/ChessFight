# R606 mockup: game show stage backgrounds behind the matching screen's chessboard and pawn.
# blender -b --factory-startup --python stage.py -- <variant A|B|C> <out.png> <scale%>
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
for k, v in (('taa_render_samples', 48), ('volumetric_tile_size', '4'), ('volumetric_samples', 64), ('volumetric_end', 60.0),
             ('use_shadows', True), ('volumetric_light_clamp', 0.0)):
    try: setattr(ee, k, v)
    except Exception: pass


def mat(name, color, rough=.4, metal=0., emit=None, estr=0.):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*color, 1); b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if emit is not None:
        b.inputs['Emission Color'].default_value = (*emit, 1); b.inputs['Emission Strength'].default_value = estr
    return m


def emit_mat(name, color, strength):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    e = nt.nodes.new('ShaderNodeEmission'); e.inputs[0].default_value = (*color, 1); e.inputs[1].default_value = strength
    o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(e.outputs[0], o.inputs[0])
    return m


def srgb(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c)


def box(name, loc, size, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    o = bpy.context.object; o.name = name; o.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    o.data.materials.append(m); return o


def cyl(name, loc, r, d, m, verts=96):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d, location=loc)
    o = bpy.context.object; o.name = name; o.data.materials.append(m)
    bpy.ops.object.shade_smooth(); return o


def torus(name, loc, R, r, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=rot, major_segments=128, minor_segments=12)
    o = bpy.context.object; o.name = name; o.data.materials.append(m); bpy.ops.object.shade_smooth(); return o


def light(kind, loc, color, energy, aim=None, size=.3, spot=math.radians(30), blend=.3):
    d = bpy.data.lights.new(kind + str(len(bpy.data.lights)), kind)
    d.color = color; d.energy = energy
    if kind == 'SPOT': d.spot_size = spot; d.spot_blend = blend; d.shadow_soft_size = size
    if kind == 'AREA': d.size = size
    if kind == 'POINT': d.shadow_soft_size = size
    o = bpy.data.objects.new(d.name, d); sc.collection.objects.link(o); o.location = loc
    if aim is not None:
        o.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return o


# ---- the board and the pawn (kept from the current screen) ----
WAL, MAP, EBO, IVO, GOLD = srgb('5a3420'), srgb('d9ad74'), srgb('1b120c'), srgb('efe3c8'), srgb('d4a24c')
sq_dark, sq_light = mat('walnut', WAL, .28), mat('maple', MAP, .28)
for i in range(8):
    for j in range(8):
        box('sq', (i - 3.5, j - 3.5, .05), (1, 1, .1), sq_light if (i + j) % 2 else sq_dark)
ebony = mat('ebony', EBO, .22)
box('frame', (0, 0, -.06), (8.9, 8.9, .3), ebony)

prof = [(0, 0), (.42, 0), (.44, .05), (.42, .1), (.34, .14), (.3, .2), (.22, .42), (.17, .6), (.27, .64), (.27, .7), (.15, .74), (.14, .8)]
bm_verts = []
seg = 64
mesh = bpy.data.meshes.new('pawn'); verts = []; faces = []
for k in range(seg):
    a = 2 * math.pi * k / seg
    for (r, z) in prof: verts.append((r * math.cos(a), r * math.sin(a), z))
n = len(prof)
for k in range(seg):
    for p in range(n - 1):
        a0, a1 = k * n + p, ((k + 1) % seg) * n + p
        faces.append((a0, a1, a1 + 1, a0 + 1))
mesh.from_pydata(verts, [], faces); mesh.update()
pawn = bpy.data.objects.new('pawn', mesh); sc.collection.objects.link(pawn)
for p in mesh.polygons: p.use_smooth = True
ivory = mat('ivory', IVO, .32)
pawn.data.materials.append(ivory)
bpy.ops.mesh.primitive_uv_sphere_add(radius=.25, location=(0, 0, 1.0), segments=48, ring_count=24)
head = bpy.context.object; head.data.materials.append(ivory); bpy.ops.object.shade_smooth(); head.parent = pawn
pawn.location = (-1.0, -.6, .1); pawn.scale = (1.55, 1.55, 1.55)

# ---- camera, as the current screen frames the board ----
cam_d = bpy.data.cameras.new('cam'); cam_d.lens = 28
cam = bpy.data.objects.new('cam', cam_d); sc.collection.objects.link(cam); sc.camera = cam
cam.location = (0, -10.4, 4.3)
cam.rotation_euler = (Vector((0, 3.0, .7)) - cam.location).to_track_quat('-Z', 'Y').to_euler()

world = bpy.data.worlds.new('w'); sc.world = world; world.use_nodes = True
wn = world.node_tree; bg = next(n for n in wn.nodes if n.type == 'BACKGROUND')
bg.inputs[0].default_value = (*srgb('06070c'), 1); bg.inputs[1].default_value = 1


def haze(density):
    vol = wn.nodes.new('ShaderNodeVolumePrincipled'); vol.inputs['Density'].default_value = density
    out = next(n for n in wn.nodes if n.type == 'OUTPUT_WORLD'); wn.links.new(vol.outputs[0], out.inputs['Volume'])


BLUE, RED, WARM = srgb('3d8cff'), srgb('ff3d55'), srgb('ffd59a')
light('AREA', (0, -3, 9), WARM, 600, aim=(0, 0, 0), size=6)          # soft key from above front

if VAR == 'A':
    # TV game show studio: glossy black stage, lit platform edge, a curved back wall of light bars, team coloured beams
    floor = mat('gloss', srgb('0b0c12'), .12)
    box('floor', (0, 6, -.45), (80, 60, .2), floor)
    box('plinth', (0, 0, -.32), (10.4, 10.4, .3), mat('plinth', srgb('121218'), .18))
    edge = emit_mat('edge', GOLD, 14)
    for s in (-1, 1):
        box('e', (0, s * 5.2, -.18), (10.4, .06, .04), edge); box('e', (s * 5.2, 0, -.18), (.06, 10.4, .04), edge)
    wall = mat('wall', srgb('0d0e16'), .35)
    bar_w, bar_g = emit_mat('barw', srgb('fff1d6'), 9), emit_mat('barg', GOLD, 7)
    R = 17
    for k in range(-13, 14):
        a = math.radians(k * 6.2)
        x, y = R * math.sin(a), 4 + R * math.cos(a) - 6
        box('panel', (x, y, 5), (1.7, .3, 12), wall, rot=(0, 0, -a))
        box('bar', (x - .02 * math.cos(a), y - .18 * math.cos(a), 5), (.16, .05, 11), bar_w if k % 2 == 0 else bar_g, rot=(0, 0, -a))
    box('band', (0, -1.6, 10.6), (40, .3, .25), emit_mat('band', GOLD, 6))
    for s, col in ((-1, BLUE), (1, RED)):
        for k in range(3):
            light('SPOT', (s * (6 + 2.5 * k), 4 + 2 * k, 10.5), col, 11000, aim=(s * 1.0 * k, -1 + k, 0), spot=math.radians(18), blend=.4, size=.15)
        box('side', (s * 13, 2, 3), (.25, 14, 8), emit_mat('side', col, 2.5))
    light('SPOT', (0, 7, 12), WARM, 7000, aim=(0, 0, 0), spot=math.radians(26), size=.2)
    haze(.05)

elif VAR == 'B':
    # Big LED screen stage: a giant chequer LED wall with a glowing crown ring, a truss over the board
    floor = mat('gloss', srgb('0a0b10'), .16)
    box('floor', (0, 6, -.45), (80, 60, .2), floor)
    m = bpy.data.materials.new('led'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    tc = nt.nodes.new('ShaderNodeTexCoord'); ck = nt.nodes.new('ShaderNodeTexChecker'); ck.inputs['Scale'].default_value = 1
    mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (18, 8, 1)
    ck.inputs['Color1'].default_value = (*srgb('2a1d55'), 1); ck.inputs['Color2'].default_value = (*srgb('0d1440'), 1)
    grad = nt.nodes.new('ShaderNodeTexGradient'); grad.gradient_type = 'SPHERICAL'
    mp2 = nt.nodes.new('ShaderNodeMapping'); mp2.inputs['Location'].default_value = (-.5, -.5, 0); mp2.inputs['Scale'].default_value = (1.6, 3.2, 1)
    mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'ADD'
    mix.inputs['B'].default_value = (*srgb('6a3cff'), 1)
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs[1].default_value = 2.2
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(tc.outputs['UV'], mp.inputs[0]); nt.links.new(mp.outputs[0], ck.inputs['Vector'])
    nt.links.new(tc.outputs['UV'], mp2.inputs[0]); nt.links.new(mp2.outputs[0], grad.inputs['Vector'])
    nt.links.new(grad.outputs['Fac'], mix.inputs['Factor']); nt.links.new(ck.outputs['Color'], mix.inputs['A'])
    nt.links.new(mix.outputs['Result'], em.inputs[0]); nt.links.new(em.outputs[0], out.inputs[0])
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 11, 4.5), rotation=(math.radians(90), 0, 0))
    led = bpy.context.object; led.scale = (30, 13, 1); led.data.materials.append(m)
    torus('ring', (0, 10.8, 5.6), 3.0, .12, emit_mat('ring', GOLD, 12), rot=(math.radians(90), 0, 0))
    torus('ring2', (0, 10.8, 5.6), 3.6, .04, emit_mat('ring2', srgb('fff1d6'), 10), rot=(math.radians(90), 0, 0))
    steel = mat('truss', srgb('2a2a30'), .4, metal=.8)
    for x in (-8, 8):
        box('leg', (x, 4, 4), (.35, .35, 9), steel)
    box('top', (0, 4, 8.6), (16.5, .35, .35), steel); box('top2', (0, 4.8, 8.6), (16.5, .35, .35), steel)
    for k in range(-3, 4):
        lx = k * 2.2
        box('can', (lx, 4.4, 8.2), (.4, .4, .5), mat('can', srgb('111111'), .5))
        col = BLUE if k < 0 else (RED if k > 0 else WARM)
        light('SPOT', (lx, 4.2, 8.0), col, 7000, aim=(lx * .35, -.5, 0), spot=math.radians(16), blend=.35, size=.1)
    for s, col in ((-1, BLUE), (1, RED)):
        box('strip', (s * 15, 10.8, 4.5), (.3, .2, 13), emit_mat('strip', col, 6))
    haze(.04)

else:
    # Round arena stage: a round dais under the board, light rings on the floor, a ring of lights overhead
    floor = mat('gloss', srgb('08090e'), .14)
    box('floor', (0, 6, -.85), (90, 70, .2), floor)
    cyl('dais', (0, 0, -.45), 7.0, .7, mat('dais', srgb('16141a'), .2))
    cyl('dais2', (0, 0, -.72), 8.2, .25, mat('dais2', srgb('101015'), .2))
    torus('lip', (0, 0, -.1), 7.02, .05, emit_mat('lip', GOLD, 14))
    for R, col, s in ((9.6, BLUE, 6), (11.4, srgb('fff1d6'), 4), (13.4, RED, 5)):
        torus('fr', (0, 0, -.74), R, .06, emit_mat('fr', col, s))
    torus('truss', (0, 1, 9.5), 8.5, .22, mat('truss', srgb('26262c'), .4, metal=.8))
    torus('trussglow', (0, 1, 9.2), 8.5, .05, emit_mat('tg', GOLD, 8))
    for k in range(12):
        a = 2 * math.pi * k / 12 + .26
        p = (8.5 * math.cos(a), 1 + 8.5 * math.sin(a), 9.3)
        col = BLUE if math.cos(a) < -.2 else (RED if math.cos(a) > .2 else WARM)
        light('SPOT', p, col, 6500, aim=(p[0] * .1, p[1] * .1 - .5, 0), spot=math.radians(14), blend=.35, size=.1)
    for s, col in ((-1, BLUE), (1, RED)):
        for k in range(4):
            box('pillar', (s * (14 + k * 2.5), 12 + k * 3, 6), (.4, .4, 14), emit_mat('pl', col, 3 + k))
    haze(.055)

bpy.ops.render.render(write_still=True)
print('RENDERED', OUT)
