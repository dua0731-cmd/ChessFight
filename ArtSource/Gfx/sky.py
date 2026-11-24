# ChessFight look rebuild: the sky. Real clouds in a drawn blue: a field of volumetric cumulus (a slab of procedural
# density: flat bases, domed tops, eroded by detail noise) lit by the scene's sun and the physical sky, path traced
# once into a 360 degree panorama and laid over a storybook gradient (deep blue overhead, light at the horizon; the
# measured sky came out pale through the film look). The look-up renders use it as their world (what the camera sees of the sky, and the
# sky's light), and Unity uses the same picture as its skybox (Assets/Look/Shaders/LookSky.shader), so both show the
# same clouds in the same place. The sun's disc is not in it: the sun is a lamp in the scene.
#
#   blender -b --factory-startup --python sky.py -- --out C:/cf-gfx/tex/sky [--res 4096] [--samples 96]
#           [--cover 0.56] [--density 0.03] [--seed 3] [--marker]   (--marker: a red ball due +y, 45 degrees up, to check the mapping)
# Writes <out>.exr (linear, as rendered then graded) and <out>_preview.jpg.
import bpy, sys, os, math
import numpy as np
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit

OUT = kit.cli('--out', os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tex', 'sky')))
RES = int(kit.cli('--res', '4096'))
SAMPLES = int(kit.cli('--samples', '96'))
COVER = float(kit.cli('--cover', '0.53'))
DENSITY = float(kit.cli('--density', '0.011'))
SEED = float(kit.cli('--seed', '3'))
BASE, TOP = 700.0, 2700.0            # metres: the clouds' flat bases, and the highest a top may reach
NEAR = float(kit.cli('--near', '3800'))  # metres: no cloud nearer than this (they come in fully by 1.35 times it)

scene = kit.reset()
SUN_DIR = tuple(float(v) for v in kit.cli('--sun-dir', ','.join(str(c) for c in kit.SUN_DIR)).split(','))   # the way the light travels (Blender axes)
kit.sun_and_sky(scene, sun_dir=SUN_DIR, seen=0.0)     # the measured sky, in view as well as as light

# ---------------------------------------------------------------- the cloud slab
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, (BASE + TOP) / 2))
slab = bpy.context.active_object
slab.scale = (26000.0, 26000.0, TOP - BASE)
m = bpy.data.materials.new('Cumulus field')
m.use_nodes = True
nt = m.node_tree
for n in list(nt.nodes):
    if n.type != 'OUTPUT_MATERIAL':
        nt.nodes.remove(n)
out = [n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'][0]
import surface
b = surface.Build(nt)
geo = b.node('ShaderNodeNewGeometry')
sep = b.node('ShaderNodeSeparateXYZ')
nt.links.new(geo.outputs['Position'], sep.inputs[0])
x, y, z = sep.outputs

def noise(vector, scale, detail, rough=0.5, dims='3D'):
    n = b.node('ShaderNodeTexNoise', noise_dimensions=dims)
    nt.links.new(vector, n.inputs['Vector'])
    n.inputs['Scale'].default_value = scale
    n.inputs['Detail'].default_value = detail
    n.inputs['Roughness'].default_value = rough
    return n.outputs[0]

flat = b.xyz(b.math('ADD', x, SEED * 9173.0), b.math('ADD', y, SEED * 4127.0), 0.0)
cover = b.math('ADD', b.math('MULTIPLY', noise(flat, 1.0 / 2400.0, 3.0, 0.5, '2D'), 0.72),
               b.math('MULTIPLY', noise(flat, 1.0 / 9000.0, 1.0, 0.5, '2D'), 0.36))
rel = b.math('DIVIDE', b.math('SUBTRACT', cover, COVER), 1.0 - COVER)
# none overhead: the yard looks at the clouds' sunlit sides from afar, not up at their grey bases
away = b.node('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP')
nt.links.new(b.math('SQRT', b.math('ADD', b.math('MULTIPLY', x, x), b.math('MULTIPLY', y, y))), away.inputs['Value'])
away.inputs['From Min'].default_value, away.inputs['From Max'].default_value = NEAR, NEAR * 1.35
rel = b.math('SUBTRACT', rel, b.math('MULTIPLY', b.math('SUBTRACT', 1.0, away.outputs[0]), 3.0))
height = b.math('DIVIDE', b.math('SUBTRACT', z, BASE), TOP - BASE)
puff = b.math('ADD', b.math('MULTIPLY', b.math('SUBTRACT', noise(geo.outputs['Position'], 1.0 / 640.0, 4.0, 0.6), 0.5), 0.62),
              b.math('MULTIPLY', b.math('SUBTRACT', noise(geo.outputs['Position'], 1.0 / 170.0, 3.0, 0.6), 0.5), 0.22))
# inside a cloud where the cover's excess stands above this height, the detail eating into the edge
f = b.math('ADD', b.math('SUBTRACT', b.math('MULTIPLY', rel, 2.3), b.math('POWER', b.math('MAXIMUM', height, 0.0), 0.8)), puff)
shape = b.node('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP')
nt.links.new(f, shape.inputs['Value'])
shape.inputs['From Min'].default_value, shape.inputs['From Max'].default_value = 0.0, 0.06
foot = b.node('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP')
nt.links.new(height, foot.inputs['Value'])
foot.inputs['From Min'].default_value, foot.inputs['From Max'].default_value = 0.0, 0.035
density = b.math('MULTIPLY', b.math('MULTIPLY', shape.outputs[0], foot.outputs[0]), DENSITY)
vol = b.node('ShaderNodeVolumePrincipled')
vol.inputs['Color'].default_value = (1.0, 1.0, 1.0, 1.0)
vol.inputs['Anisotropy'].default_value = 0.45
nt.links.new(density, vol.inputs['Density'])
nt.links.new(vol.outputs[0], out.inputs['Volume'])
slab.data.materials.append(m)

# the land under the sky: its light comes back up into the clouds' bases
bpy.ops.mesh.primitive_circle_add(vertices=64, radius=60000.0, fill_type='NGON', location=(0.0, 0.0, -1.0))
land = bpy.data.materials.new('Land')
land.use_nodes = True
land.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.42, 0.40, 0.27, 1.0)
land.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 1.0
bpy.context.active_object.data.materials.append(land)
bpy.context.active_object.visible_camera = False

if kit.flag('--marker'):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=40.0, location=(0.0, 400.0, 400.0))
    mk = bpy.data.materials.new('Marker')
    mk.use_nodes = True
    mk.node_tree.nodes['Principled BSDF'].inputs['Emission Color'].default_value = (1.0, 0.0, 0.0, 1.0)
    mk.node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value = 20.0
    bpy.context.active_object.data.materials.append(mk)

# ---------------------------------------------------------------- the panorama
data = bpy.data.cameras.new('Sky camera')
data.type = 'PANO'
try:
    data.panorama_type = 'EQUIRECTANGULAR'
except Exception:
    data.cycles.panorama_type = 'EQUIRECTANGULAR'
cam = bpy.data.objects.new('Sky camera', data)
cam.location = (0.0, 0.0, 2.0)
# The frame an Environment Texture reads back: u = 0.5 - atan2(y, x) / 2 pi, v = 0.5 + height / pi, so +y lies a quarter
# from the left (checked with --marker: the red ball due +y shows at u = 0.25).
cam.rotation_euler = (math.radians(90.0), 0.0, math.radians(-90.0))
data.clip_start, data.clip_end = 0.5, 200000.0                          # Cycles cuts what lies past the camera's far end
scene.collection.objects.link(cam)
scene.camera = cam

scene.render.engine = 'CYCLES'
prefs = bpy.context.preferences.addons['cycles'].preferences
try:
    prefs.compute_device_type = 'OPTIX'
    prefs.get_devices()
    for d in prefs.devices:
        d.use = d.type == 'OPTIX'
    scene.cycles.device = 'GPU'
except Exception as e:
    print('GPU off', e)
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.cycles.max_bounces = 48
scene.cycles.volume_bounces = 48
for key, value in (('volume_step_rate', 1.0), ('volume_max_steps', 1024)):
    try:
        setattr(scene.cycles, key, value)
    except Exception:
        pass
scene.render.resolution_x, scene.render.resolution_y = RES, RES // 2
scene.render.resolution_percentage = 100
scene.render.film_transparent = True                 # the clouds alone, with how much of the sky each covers
scene.render.image_settings.color_mode = 'RGBA'
scene.render.image_settings.file_format = 'OPEN_EXR'
scene.render.image_settings.color_depth = '32'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.exposure = 0.0
raw = OUT + '_raw.exr'   # kept: --compose-only builds the picture from it again
scene.render.filepath = raw
if not (kit.flag('--compose-only') and os.path.exists(raw)):
    bpy.ops.render.render(write_still=True)
print('RENDERED sky %.1f s' % (kit.time.time() - kit.T0))

# ---------------------------------------------------------------- the clouds over the drawn sky
HORIZON, MIDDLE, OVERHEAD = (0.55, 0.80, 1.0), (0.16, 0.48, 1.0), (0.03, 0.20, 0.85)     # as kit's drawn sky
MIDDLE_AT, OVERHEAD_AT = 0.14, 0.60                                                      # sine of the height
CLOUD_GAIN = float(kit.cli('--cloud-gain', '0.62'))
img = bpy.data.images.load(raw)
w, h = img.size
px = np.empty(w * h * 4, np.float32)
img.pixels.foreach_get(px)
px = px.reshape(h, w, 4)
cloud, alpha = px[:, :, :3], np.clip(px[:, :, 3:4], 0.0, 1.0)
elev = ((np.arange(h, dtype=np.float32) + 0.5) / h - 0.5) * np.pi                        # row 0 is straight down
up = np.sin(elev)[:, None, None]
azim = (0.5 - (np.arange(w, dtype=np.float32) + 0.5) / w) * 2.0 * np.pi                  # u = 0.5 - atan2(y, x) / 2 pi
def lerp(a, b, t):
    return np.array(a, np.float32) * (1.0 - t) + np.array(b, np.float32) * t
low = lerp(HORIZON, MIDDLE, np.clip(up / MIDDLE_AT, 0.0, 1.0))
sky = low * (1.0 - np.clip((up - MIDDLE_AT) / (OVERHEAD_AT - MIDDLE_AT), 0.0, 1.0)) + np.array(OVERHEAD, np.float32) * np.clip((up - MIDDLE_AT) / (OVERHEAD_AT - MIDDLE_AT), 0.0, 1.0)
sky = np.broadcast_to(sky, (h, w, 3)).copy()
# a warm brightening round the sun
to_sun = -Vector(SUN_DIR).normalized()
dx = np.cos(elev)[:, None] * np.cos(azim)[None, :]
dy = np.cos(elev)[:, None] * np.sin(azim)[None, :]
dz = np.sin(elev)[:, None] * np.ones((1, w), np.float32)
near_sun = np.clip(dx * to_sun.x + dy * to_sun.y + dz * to_sun.z, -1.0, 1.0)
sky += (np.exp(-((np.arccos(near_sun) / math.radians(16.0)) ** 2))[:, :, None] * np.array([0.9, 0.75, 0.5], np.float32) * 0.8)
# the far clouds sink into the horizon's air
depth = np.clip(elev / math.radians(7.0), 0.0, 1.0)[:, None, None]
depth = depth * depth * (3.0 - 2.0 * depth)
alpha = alpha * (0.45 + 0.55 * depth)
rgb = sky * (1.0 - alpha) + cloud * CLOUD_GAIN * (0.45 + 0.55 * depth)
# Under the horizon: the far land. Just under it the horizon's haze, then over about ten degrees a warm, darker
# ground (sunlit fields and stone far below). In view it is only seen past a map's edge; as light it is what comes
# up from below, so it must be ground-coloured: a light haze there lifted every shade and turned it blue.
mid = h // 2
band = np.array(HORIZON, np.float32)
land = np.array([float(v) for v in kit.cli('--land', '0.30,0.27,0.20').split(',')], np.float32)
for r in range(mid):
    below = (mid - r) / float(mid) * 90.0              # degrees under the horizon
    k = np.clip((below - 2.5) / 10.0, 0.0, 1.0)
    k = k * k * (3.0 - 2.0 * k)
    rgb[r] = band * 0.92 * (1.0 - k) + land * k
out = np.concatenate([np.maximum(rgb, 0.0), np.ones((h, w, 1), np.float32)], -1).astype(np.float32)
final = bpy.data.images.new('sky', w, h, alpha=False, float_buffer=True)
final.pixels.foreach_set(out.ravel())
settings = scene.render.image_settings
settings.file_format, settings.color_mode, settings.color_depth, settings.exr_codec = 'OPEN_EXR', 'RGB', '16', 'ZIP'
scene.view_settings.view_transform = 'Raw'
final.save_render(OUT + '.exr', scene=scene)
view = np.clip(1.0 - np.exp(-rgb * 1.3), 0.0, 1.0) ** (1.0 / 2.2)
pv = np.concatenate([view, np.ones((h, w, 1), np.float32)], -1)
prev = bpy.data.images.new('sky preview', w, h, alpha=False)
prev.pixels.foreach_set(pv.astype(np.float32).ravel())
prev.scale(min(w, 2048), min(h, 1024))
prev.filepath_raw = OUT + '_preview.jpg'
prev.file_format = 'JPEG'
prev.save()
print('WROTE', OUT + '.exr')
