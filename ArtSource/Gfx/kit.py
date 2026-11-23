# ChessFight look-up kit (Blender 5.3, run headless). Shared by every asset module and scene script.
#
# The rule of the kit: an asset is real geometry with a colour per face corner (the 'Col' attribute) and one of a few
# PBR materials. The forms are stylized (chunky, bevelled, toy-like); the surfaces are real: each material wears a
# surface set (surface.py, textures.py: stone grain, wood grain, hammered iron ...) laid over the mesh's colour, with
# worn edges and grime where surfaces meet. Nothing only Blender has: Unity URP reads the same meshes, colours and sets.
#
#   k = Kit()                      one per scene; k.rnd is its random source
#   k.stone.box(center, size, rot=(0,0,0), color=(r,g,b))          colours are display (sRGB) values, 0..1
#   k.wood.lathe(profile, matrix, segments, color, stave=0.1)      profile: (height, radius) pairs turned round +z
#   k.gold.ball(...), k.leaf.ico(...), k.cloth.poly(points, color)
#   k.finish()                     turns every batch into one object with its modifiers
# Batches (name: material, bevel): see Kit.SPEC. Add a batch of your own with k.batch(name, material_key, ...).
import bpy, bmesh, math, os, random, sys, time, colorsys
from mathutils import Vector, Matrix, Euler

T0 = time.time()

def cli(name, fallback=None):
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return argv[argv.index(name) + 1] if name in argv else fallback

def flag(name):
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return name in argv

# ---------------------------------------------------------------- palette (display values, as picked on screen)
STONE = (0.93, 0.86, 0.72)         # warm cream limestone, trims and arches
STONE_WALL = (0.88, 0.83, 0.74)    # wall blocks, a little cooler and greyer
PAVE = (0.90, 0.76, 0.52)          # the yard's paving, sandy
SAND = (0.92, 0.72, 0.40)
WOOD = (0.62, 0.40, 0.22)
WOOD_LIGHT = (0.74, 0.52, 0.28)
WOOD_DARK = (0.40, 0.25, 0.15)
RED = (0.78, 0.15, 0.13)
BLUE = (0.15, 0.24, 0.62)
ROOF_BLUE = (0.20, 0.30, 0.66)
CREAM = (0.96, 0.90, 0.76)
GOLD = (1.0, 0.76, 0.28)
GOLD_CLOTH = (0.98, 0.78, 0.30)
IRON = (0.16, 0.15, 0.17)
LEAF = (0.18, 0.40, 0.15)
LEAF_LIGHT = (0.36, 0.58, 0.20)
PIECE_WHITE = (0.96, 0.93, 0.86)
PIECE_BLACK = (0.17, 0.15, 0.17)

def hsv(h, s, v):
    return colorsys.hsv_to_rgb(h % 1.0, max(0.0, min(1.0, s)), max(0.0, min(1.0, v)))

# ---------------------------------------------------------------- materials: the colour comes from the mesh ('Col')
def material(name, rough=0.7, metal=0.0, sheen=0.0, bump=0.0, bump_scale=6.0, transl=0.0, emit=0.0, coat=0.0, surface_key=None):
    """surface_key: the kit material key whose surface set it wears (surface.WEAR); None = a plain material."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes['Principled BSDF']
    attr = nt.nodes.new('ShaderNodeAttribute')
    attr.attribute_name = 'Col'
    nt.links.new(attr.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    for key, value in (('Sheen Weight', sheen), ('Subsurface Weight', transl), ('Coat Weight', coat)):
        if key in bsdf.inputs and value > 0:
            bsdf.inputs[key].default_value = value
    if transl > 0 and 'Subsurface Radius' in bsdf.inputs:
        bsdf.inputs['Subsurface Radius'].default_value = (0.3, 0.5, 0.2)
    if emit > 0:
        nt.links.new(attr.outputs['Color'], bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = emit
    if bump > 0:
        noise = nt.nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = bump_scale
        noise.inputs['Detail'].default_value = 3.0
        b = nt.nodes.new('ShaderNodeBump')
        b.inputs['Strength'].default_value = bump
        b.inputs['Distance'].default_value = 0.05
        nt.links.new(noise.outputs['Fac'], b.inputs['Height'])
        nt.links.new(b.outputs['Normal'], bsdf.inputs['Normal'])
    if surface_key and not flag('--plain'):
        try:
            import surface
            surface.dress(m, surface_key)
        except Exception as e:
            print('SURFACE off for', name, repr(e))
    return m

MATERIALS = {
    'stone': dict(rough=0.82, bump=0.10, bump_scale=3.0),
    'sand': dict(rough=0.95, bump=0.5, bump_scale=14.0),
    'wood': dict(rough=0.62, bump=0.18, bump_scale=9.0),
    'paint': dict(rough=0.42),
    'cloth': dict(rough=0.9, sheen=0.4),
    'gold': dict(rough=0.28, metal=1.0),
    'iron': dict(rough=0.45, metal=1.0),
    'leaf': dict(rough=0.6, transl=0.25),
    'piece': dict(rough=0.5),
    'glow': dict(rough=0.4, emit=6.0),
    'cloud': dict(rough=1.0, emit=0.35),
}

# ---------------------------------------------------------------- mesh batches
class Batch:
    """Many simple solids in one mesh, each with its own colour; bevelled together by one modifier."""
    def __init__(self, name, mat, rnd, bevel=0.0, segments=2, smooth=False, displace=0.0, displace_scale=1.1, subdiv=0):
        self.name, self.mat, self.rnd, self.bevel, self.segments, self.smooth = name, mat, rnd, bevel, segments, smooth
        self.displace, self.displace_scale, self.subdiv = displace, displace_scale, subdiv
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new('Col')
        self.obj = None

    def _paint(self, faces, color, smooth=None):
        c = (color[0], color[1], color[2], 1.0)
        for f in faces:
            f.smooth = self.smooth if smooth is None else smooth
            for l in f.loops:
                l[self.col] = c

    def box(self, center, size, rot=(0, 0, 0), color=(1, 1, 1)):
        m = Matrix.Translation(center) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((size[0], size[1], size[2], 1))
        g = bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)
        self._paint({f for v in g['verts'] for f in v.link_faces}, color, False)

    def lathe(self, profile, matrix=None, segments=24, color=(1, 1, 1), colors=None, stave=0.0, smooth=True):
        """profile: (height, radius) pairs from the foot up, turned round +z. colors: one per span of the profile.
        stave: each of the segments gets its own lightness, this much either way (barrel staves, planks)."""
        matrix = matrix or Matrix.Identity(4)
        rings = [[self.bm.verts.new(matrix @ Vector((math.cos(2 * math.pi * k / segments) * r, math.sin(2 * math.pi * k / segments) * r, h)))
                  for k in range(segments)] for h, r in profile]
        strips = [self.rnd.uniform(-stave, stave) for _ in range(segments)]
        for i in range(len(rings) - 1):
            base = colors[i] if colors else color
            for k in range(segments):
                f = self.bm.faces.new((rings[i][k], rings[i][(k + 1) % segments], rings[i + 1][(k + 1) % segments], rings[i + 1][k]))
                self._paint([f], tuple(max(0.0, min(1.0, v * (1 + strips[k]))) for v in base), smooth)
        for ring, foot, c in ((rings[0], True, colors[0] if colors else color), (rings[-1], False, colors[-1] if colors else color)):
            if (profile[0][1] if foot else profile[-1][1]) > 1e-4:
                self._paint([self.bm.faces.new(ring[::-1] if foot else ring)], c, False)

    def ball(self, center, radius, color=(1, 1, 1), scale=(1, 1, 1), u=12, v=8, smooth=False):
        m = Matrix.Translation(center) @ Matrix.Diagonal((radius * scale[0], radius * scale[1], radius * scale[2], 1))
        g = bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=1.0, matrix=m)
        self._paint({f for vert in g['verts'] for f in vert.link_faces}, color, smooth)

    def ico(self, center, radius, color=(1, 1, 1), scale=(1, 1, 1), sub=2, smooth=True):
        m = Matrix.Translation(center) @ Matrix.Diagonal((radius * scale[0], radius * scale[1], radius * scale[2], 1))
        g = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=1.0, matrix=m)
        self._paint({f for vert in g['verts'] for f in vert.link_faces}, color, smooth)

    def poly(self, points, color=(1, 1, 1), both=False):
        self._paint([self.bm.faces.new([self.bm.verts.new(p) for p in points])], color, False)
        if both:
            self._paint([self.bm.faces.new([self.bm.verts.new(p) for p in points][::-1])], color, False)

    def count(self):
        return len(self.bm.faces)

    def finish(self, scene):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        scene.collection.objects.link(obj)
        mesh.materials.append(self.mat)
        if self.subdiv:
            s = obj.modifiers.new('Subdiv', 'SUBSURF')
            s.levels = s.render_levels = self.subdiv
        if self.displace:
            tex = bpy.data.textures.new(self.name + ' lumps', 'CLOUDS')
            tex.noise_scale = self.displace_scale
            d = obj.modifiers.new('Displace', 'DISPLACE')
            d.texture, d.strength, d.texture_coords = tex, self.displace, 'GLOBAL'
        if self.bevel:
            b = obj.modifiers.new('Bevel', 'BEVEL')
            b.width, b.segments, b.limit_method, b.angle_limit = self.bevel, self.segments, 'ANGLE', math.radians(35)
        self.obj = obj
        return obj


class Kit:
    # name: (material, options). The same names in every module, so the scene has one object per kind of surface.
    SPEC = {
        'stone': ('stone', dict(bevel=0.045, segments=3)),         # blocks: fat bevels
        'core': ('stone', dict()),                                 # hidden fillers behind block faces
        'trim': ('stone', dict(bevel=0.03, segments=3, smooth=True)),   # turned stone: columns, finials, round steps
        'roof': ('paint', dict(bevel=0.03)),
        'gold': ('gold', dict(smooth=True)),
        'iron': ('iron', dict(bevel=0.012)),
        'iron_round': ('iron', dict(bevel=0.015, smooth=True)),
        'wood': ('wood', dict(bevel=0.03, segments=2)),            # sawn timber: posts, planks, beams
        'wood_round': ('wood', dict(bevel=0.03, smooth=True)),     # turned timber: barrels, shafts
        'paint': ('paint', dict(bevel=0.02)),
        'paint_round': ('paint', dict(bevel=0.035, smooth=True)),
        'cloth': ('cloth', dict()),                                # thin sheets (give both=True to poly)
        'awning': ('cloth', dict(bevel=0.01)),                     # cloth with thickness
        'glow': ('glow', dict()),
        'sand': ('sand', dict()),
        'piece': ('piece', dict()),
        'leaf': ('leaf', dict(displace=0.55, subdiv=1)),
        'trunk': ('wood', dict(smooth=True)),
        'cloud': ('cloud', dict(displace=6.0, displace_scale=22.0, subdiv=2)),
    }

    def __init__(self, seed=11):
        self.rnd = random.Random(seed)
        self.scene = bpy.context.scene
        self.mats = {key: material(key.capitalize(), surface_key=key, **opts) for key, opts in MATERIALS.items()}
        self.batches = {}

    def batch(self, name, material_key, **options):
        if name not in self.batches:
            self.batches[name] = Batch(name, self.mats[material_key], self.rnd, **options)
        return self.batches[name]

    def __getattr__(self, name):
        spec = Kit.SPEC.get(name)
        if spec is None:
            raise AttributeError(name)
        return self.batch(name, spec[0], **spec[1])

    def jitter(self, base, dh=0.012, ds=0.05, dv=0.05):
        h, s, v = colorsys.rgb_to_hsv(*base)
        return hsv(h + self.rnd.uniform(-dh, dh), s + self.rnd.uniform(-ds, ds), v + self.rnd.uniform(-dv, dv))

    def finish(self):
        if panorama_sky() and not flag('--geo-clouds'):
            for name in ('cloud', 'cloud_puff'):          # the sky picture has its own clouds
                self.batches.pop(name, None)
        faces = {name: b.count() for name, b in self.batches.items()}
        for b in self.batches.values():
            b.finish(self.scene)
        try:
            import surface
            surface.grain(self)
        except Exception as e:
            print('GRAIN off', repr(e))
        print('FACES before bevel', sum(faces.values()), faces)
        return faces


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene

# ---------------------------------------------------------------- the standard light: one warm sun and the sky
SUN_DIR = (0.57, 0.40, -0.72)      # the way the light travels: the sun stands in the south-west, 46 degrees up (the lead's look)

SKY_PANORAMA = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tex', 'sky.exr'))

def panorama_sky():
    """The path-traced sky with its volumetric clouds (sky.py), when it has been made."""
    return os.path.exists(SKY_PANORAMA) and not flag('--drawn-sky')

def sun_and_sky(scene, sun_dir=SUN_DIR, sun=5.6, sky=0.30, soft=2.4, sun_color=(1.0, 0.93, 0.80), seen=2.2, pano_light=2.0, pano_seen=1.7):
    """One warm sun and the sky. With the panorama (tex/sky.exr, made by sky.py with this same sun) the sky is that
    picture: its light (pano_light times as strong), and what the camera sees (pano_seen times as bright). Without it: the sky model's light and a
    drawn gradient in view (seen; 0 = the model in view too)."""
    d = Vector(sun_dir).normalized()
    data = bpy.data.lights.new('Sun', 'SUN')
    data.energy, data.color, data.angle = sun, sun_color, math.radians(soft)
    obj = bpy.data.objects.new('Sun', data)
    obj.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    scene.collection.objects.link(obj)
    world = bpy.data.worlds.new('Sky')
    world.use_nodes = True
    scene.world = world
    wn = world.node_tree
    bg = wn.nodes['Background']
    tex = wn.nodes.new('ShaderNodeTexSky')
    for kind in ('MULTIPLE_SCATTERING', 'NISHITA', 'HOSEK_WILKIE'):
        try:
            tex.sky_type = kind
            break
        except Exception:
            continue
    for key, value in (('sun_elevation', math.asin(-d.z)), ('sun_rotation', math.atan2(-d.x, -d.y)), ('sun_disc', False), ('air_density', 1.0),
                       ('dust_density', 0.4), ('aerosol_density', 0.4), ('ozone_density', 1.6), ('altitude', 0.0)):
        try:
            setattr(tex, key, value)
        except Exception:
            pass
    wn.links.new(tex.outputs['Color'], bg.inputs['Color'])
    bg.inputs['Strength'].default_value = sky
    if seen > 0 and panorama_sky():
        env = wn.nodes.new('ShaderNodeTexEnvironment')
        env.image = bpy.data.images.load(SKY_PANORAMA, check_existing=True)
        wn.links.new(env.outputs['Color'], bg.inputs['Color'])
        bg.inputs['Strength'].default_value = pano_light
        if pano_seen != pano_light:
            out = [n for n in wn.nodes if n.type == 'OUTPUT_WORLD'][0]
            shown, path, mix = wn.nodes.new('ShaderNodeBackground'), wn.nodes.new('ShaderNodeLightPath'), wn.nodes.new('ShaderNodeMixShader')
            shown.inputs['Strength'].default_value = pano_seen
            wn.links.new(env.outputs['Color'], shown.inputs['Color'])
            wn.links.new(path.outputs['Is Camera Ray'], mix.inputs['Fac'])
            wn.links.new(bg.outputs['Background'], mix.inputs[1])
            wn.links.new(shown.outputs['Background'], mix.inputs[2])
            wn.links.new(mix.outputs['Shader'], out.inputs['Surface'])
        return obj
    if seen > 0:
        # What the camera sees of the sky is drawn, not measured: a deep storybook blue overhead running to a light
        # horizon. The light still comes from the sky model, so shadows keep their real blue fill.
        out = [n for n in wn.nodes if n.type == 'OUTPUT_WORLD'][0]
        coord, sep, ramp = wn.nodes.new('ShaderNodeTexCoord'), wn.nodes.new('ShaderNodeSeparateXYZ'), wn.nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position = 0.0
        ramp.color_ramp.elements[0].color = (0.55, 0.80, 1.0, 1.0)
        ramp.color_ramp.elements[1].position = 0.6
        ramp.color_ramp.elements[1].color = (0.03, 0.20, 0.85, 1.0)
        ramp.color_ramp.elements.new(0.14).color = (0.16, 0.48, 1.0, 1.0)
        shown, path, mix = wn.nodes.new('ShaderNodeBackground'), wn.nodes.new('ShaderNodeLightPath'), wn.nodes.new('ShaderNodeMixShader')
        shown.inputs['Strength'].default_value = seen
        wn.links.new(coord.outputs['Generated'], sep.inputs['Vector'])
        wn.links.new(sep.outputs['Z'], ramp.inputs['Fac'])
        wn.links.new(ramp.outputs['Color'], shown.inputs['Color'])
        wn.links.new(path.outputs['Is Camera Ray'], mix.inputs['Fac'])
        wn.links.new(bg.outputs['Background'], mix.inputs[1])
        wn.links.new(shown.outputs['Background'], mix.inputs[2])
        wn.links.new(mix.outputs['Shader'], out.inputs['Surface'])
    return obj

def camera(scene, location, look_at, lens=30.0, fstop=None, focus=None):
    data = bpy.data.cameras.new('Camera')
    data.lens, data.sensor_width = lens, 36
    data.clip_start, data.clip_end = 0.1, 20000.0
    cam = bpy.data.objects.new('Camera', data)
    cam.location = location
    cam.rotation_euler = (Vector(look_at) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()
    scene.collection.objects.link(cam)
    scene.camera = cam
    if fstop:
        data.dof.use_dof = True
        data.dof.focus_distance = (Vector(focus if focus else look_at) - Vector(location)).length
        data.dof.aperture_fstop = fstop
    return cam

def render(scene, out, samples=96, res=(1536, 1024), exposure=-0.3, look='AgX - Punchy', blend=''):
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
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    try:
        scene.cycles.denoiser = 'OPTIX'
    except Exception:
        pass
    scene.cycles.max_bounces = 8
    scene.cycles.diffuse_bounces = 4
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = out
    try:
        scene.view_settings.view_transform = 'AgX'
        scene.view_settings.look = look
    except Exception as e:
        print('look', e)
    scene.view_settings.exposure = exposure
    if blend:
        bpy.ops.wm.save_as_mainfile(filepath=blend)
    bpy.ops.render.render(write_still=True)
    print('RENDERED %s  %.1f s' % (out, time.time() - T0))
