# The sunset seen through the title hall's great window (10-17, 승규 님: "노을 빛이랑 구름만 블랜더로"):
# a wide sunset sky (gold at the horizon, peach, rose, lavender blue above) with real volumetric clouds
# lit from behind by the low sun, rendered in Cycles to one wide image that Unity lays on a far card.
# Image 3:1, covering 60 m x 20 m on the card. Usage: blender -b --factory-startup --python render_sky.py -- <out.png> [samples]
import bpy, math, sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
OUT = args[0]; SAMPLES = int(args[1]) if len(args) > 1 else 64; PCT = int(args[2]) if len(args) > 2 else 100
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
sc = bpy.context.scene

def srgb(h):
    h = h.lstrip("#"); return tuple(((int(h[i:i + 2], 16) / 255) ** 2.2) for i in (0, 2, 4)) + (1.0,)

# ---------- the sky: a gradient on the view direction's height, with a warm glow round the sun
world = bpy.data.worlds.new("Sunset"); sc.world = world; world.use_nodes = True
nt = world.node_tree; L = nt.links.new
for n in list(nt.nodes):
    if n.type != 'OUTPUT_WORLD': nt.nodes.remove(n)
out = next(n for n in nt.nodes if n.type == 'OUTPUT_WORLD')
tc = nt.nodes.new('ShaderNodeTexCoord')
sep = nt.nodes.new('ShaderNodeSeparateXYZ'); L(tc.outputs['Generated'], sep.inputs[0])
ramp = nt.nodes.new('ShaderNodeValToRGB'); e = ramp.color_ramp.elements
e[0].position, e[0].color = 0.0, srgb("ffc070")
e[1].position, e[1].color = .62, srgb("4f5fa8")
for pos, col in ((.05, "ffb47e"), (.13, "f29a8c"), (.28, "c98bb2"), (.42, "8b84bf")):
    el = e.new(pos); el.color = srgb(col)
L(sep.outputs['Z'], ramp.inputs['Fac'])
# the sun's glow: brighter and golder towards the sun's direction near the horizon
# the sun low on the horizon, placed where the title camera sees it through the great window (right part)
sun_dir = Vector((.43, .90, .075)).normalized()
dot = nt.nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'; dot.inputs[1].default_value = sun_dir
L(tc.outputs['Generated'], dot.inputs[0])
glow = nt.nodes.new('ShaderNodeMapRange'); glow.inputs["From Min"].default_value = .6; glow.inputs['From Max'].default_value = 1.0
glow.inputs['To Min'].default_value = 0; glow.inputs['To Max'].default_value = 1; L(dot.outputs['Value'], glow.inputs['Value'])
pw = nt.nodes.new('ShaderNodeMath'); pw.operation = 'POWER'; pw.inputs[1].default_value = 2.2; L(glow.outputs['Result'], pw.inputs[0])
mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'SCREEN'
L(pw.outputs[0], mix.inputs[0]); L(ramp.outputs['Color'], mix.inputs[6]); mix.inputs[7].default_value = srgb("ffa850")
# the sun's disc and a tight bright halo round it
disc = nt.nodes.new('ShaderNodeMapRange'); disc.inputs['From Min'].default_value = .99885; disc.inputs['From Max'].default_value = .9991
L(dot.outputs['Value'], disc.inputs['Value'])
halo = nt.nodes.new('ShaderNodeMapRange'); halo.inputs['From Min'].default_value = .985; halo.inputs['From Max'].default_value = .9991
L(dot.outputs['Value'], halo.inputs['Value'])
hp = nt.nodes.new('ShaderNodeMath'); hp.operation = 'POWER'; hp.inputs[1].default_value = 3.0; L(halo.outputs['Result'], hp.inputs[0])
both = nt.nodes.new('ShaderNodeMath'); both.operation = 'MAXIMUM'; L(disc.outputs['Result'], both.inputs[0]); L(hp.outputs[0], both.inputs[1])
mix2 = nt.nodes.new('ShaderNodeMix'); mix2.data_type = 'RGBA'; L(both.outputs[0], mix2.inputs[0]); L(mix.outputs[2], mix2.inputs[6]); mix2.inputs[7].default_value = (3.0, 1.35, .35, 1)
bg = nt.nodes.new('ShaderNodeBackground'); bg.inputs['Strength'].default_value = 1.0
L(mix2.outputs[2], bg.inputs['Color']); L(bg.outputs['Background'], out.inputs['Surface'])

# ---------- clouds: volume boxes with a soft rounded falloff and billowing noise
def cloud_material(name, seed, dens=2.4):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; L = nt.links.new
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    # rounded falloff: 1 at the centre of the box, 0 at its skin, flatter at the bottom
    mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (1, 1, 1.15); mp.inputs['Location'].default_value = (0, 0, .15)
    L(tc.outputs['Object'], mp.inputs['Vector'])
    ln = nt.nodes.new('ShaderNodeVectorMath'); ln.operation = 'LENGTH'; L(mp.outputs['Vector'], ln.inputs[0])
    fall = nt.nodes.new('ShaderNodeMapRange'); fall.inputs['From Min'].default_value = 1.0; fall.inputs['From Max'].default_value = 0.0; fall.inputs['To Max'].default_value = .68
    L(ln.outputs['Value'], fall.inputs['Value'])
    nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3.2; nz.inputs['Detail'].default_value = 12; nz.inputs['Distortion'].default_value = .25
    nz.noise_dimensions = '4D'; nz.inputs['Roughness'].default_value = .62; nz.inputs['W'].default_value = seed
    L(tc.outputs['Object'], nz.inputs['Vector'])
    add = nt.nodes.new('ShaderNodeMath'); add.operation = 'ADD'; L(fall.outputs['Result'], add.inputs[0]); L(nz.outputs['Fac'], add.inputs[1])
    th = nt.nodes.new('ShaderNodeMapRange'); th.inputs['From Min'].default_value = .78; th.inputs['From Max'].default_value = .9
    L(add.outputs[0], th.inputs['Value'])
    mul = nt.nodes.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = dens; L(th.outputs['Result'], mul.inputs[0])
    vol = nt.nodes.new('ShaderNodeVolumePrincipled'); vol.inputs['Color'].default_value = srgb("fff4ee")
    vol.inputs['Anisotropy'].default_value = .35
    # sunset clouds glow a little of their own: warm rose light from the whole sky
    vol.inputs['Emission Color'].default_value = srgb("ffb8a0")
    em = nt.nodes.new('ShaderNodeMath'); em.operation = 'MULTIPLY'; em.inputs[1].default_value = .12; L(mul.outputs[0], em.inputs[0]); L(em.outputs[0], vol.inputs['Emission Strength'])
    L(mul.outputs[0], vol.inputs['Density']); L(vol.outputs['Volume'], out.inputs['Volume'])
    return m

clouds = [
    # (x, y, z, sx, sy, sz)  y = distance from the camera: big billowing banks low down, smaller ones higher
    (-70, 150, 10, 42, 20, 16), (-5, 175, 14, 48, 22, 18), (60, 160, 8, 40, 20, 15), (115, 190, 14, 44, 20, 16),
    (-130, 190, 6, 44, 20, 14), (15, 110, 34, 26, 12, 10), (-50, 120, 38, 22, 11, 9), (75, 125, 40, 24, 11, 9),
    (-25, 240, -6, 80, 26, 12), (75, 250, -8, 80, 26, 12), (-100, 100, 26, 20, 10, 8), (135, 110, 28, 22, 10, 8),
    (-160, 170, 20, 30, 14, 10), (170, 180, 16, 34, 14, 10), (35, 150, 22, 30, 12, 10), (-95, 160, 30, 26, 12, 9),
    (-30, 140, 24, 26, 12, 9), (100, 150, 34, 24, 10, 8), (-140, 130, 40, 20, 10, 7), (150, 140, 44, 20, 10, 7),
]
for i, (x, y, z, sx, sy, sz) in enumerate(clouds):
    bpy.ops.mesh.primitive_cube_add(size=2, location=(x, y, z)); o = bpy.context.active_object
    o.scale = (sx, sy, sz); o.data.materials.append(cloud_material("Cloud %d" % i, i * 3.7))

# ---------- the low sun behind the clouds, warm; a soft rose fill from the sky
sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 5.5; sun.color = srgb("ff9e5e")[:3]; sun.angle = math.radians(3)
so = bpy.data.objects.new("Sun", sun); sc.collection.objects.link(so)
so.rotation_euler = (-sun_dir).to_track_quat('-Z', 'Y').to_euler()   # from the sun: golden edges on the clouds
fill = bpy.data.lights.new("Fill", 'SUN'); fill.energy = 2.2; fill.color = srgb("e8a0c0")[:3]
fo = bpy.data.objects.new("Fill", fill); sc.collection.objects.link(fo); fo.rotation_euler = (math.radians(30), 0, math.radians(180))

# ---------- camera: a wide view a little above the horizon
cam = bpy.data.cameras.new("Cam"); cam.lens_unit = 'FOV'; cam.angle = math.radians(100); cam.sensor_fit = 'HORIZONTAL'
co = bpy.data.objects.new("Cam", cam); sc.collection.objects.link(co)
co.location = (0, 0, 0); co.rotation_euler = (math.radians(96), 0, 0)
sc.camera = co
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
sc.cycles.volume_bounces = 2; sc.cycles.max_bounces = 6
sc.render.resolution_x, sc.render.resolution_y = 3072, 1024; sc.render.resolution_percentage = PCT
sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'
sc.render.image_settings.file_format = 'PNG'; sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("SKY DONE", OUT, flush=True)
