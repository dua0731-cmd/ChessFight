# Tutorial sky (R834, 승규 11-25 "구름 그래픽 좋게 해줘"): a 360 panorama of real volumetric cumulus over a clear sky,
# path traced in Cycles, for the tutorial's own skybox (Skybox/Panoramic, RagdollLabBuilder.TutorialDress). Not JY-gfx's
# sky: the tutorial does not use that package (11-25 "패키지 파일 적용 하지 말자").
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tu_sky_v1.py -- [--w 4096] [--samples 64] [--test]
#
# The sun is the tutorial scene's Directional Light: its light travels (Unity) -0.548, -0.743, 0.384, so the sun stands
# 48 degrees up toward (0.548, 0.743, -0.384). The clouds are lit from there (a Sun lamp, plus the sky's own light), the
# sky texture draws its disc. The panorama is turned afterwards (a horizontal roll) so the sun lands where Unity's
# Skybox/Panoramic looks for that direction (u = 0.5 - atan2(z, x) / 2 pi, v = 1 - acos(y) / pi), with _Rotation 0.
#
# Writes Materials/Textures/TU_SkyClouds.exr (half float, linear) and Renders/sky_preview.png.
import bpy, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default):
    return type(default)(ARGS[ARGS.index(name) + 1]) if name in ARGS else default


W = arg("--w", 4096)
SAMPLES = arg("--samples", 64)
SUN_UNITY = np.array([.548, .743, -.384])           # toward the sun, Unity axes
SUN_B = np.array([SUN_UNITY[0], SUN_UNITY[2], SUN_UNITY[1]])   # Blender axes (x, z, y)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = SAMPLES
    sc.cycles.use_denoising = True
    sc.cycles.use_auto_tile = False          # one tile: no seams where tiles were denoised apart
    sc.cycles.use_adaptive_sampling = False
    sc.cycles.volume_step_rate = .008      # converged: finer steps (.003) look the same
    sc.cycles.volume_max_steps = 4096
    sc.cycles.volume_biased = True          # ray-marched: the unbiased tracker's majorant octree under-guessed the procedural
                                            # density and cut the clouds into straight-edged blocks
    sc.cycles.max_bounces = 6; sc.cycles.volume_bounces = 2
    sc.render.resolution_x = W; sc.render.resolution_y = W // 2
    sc.view_settings.view_transform = 'Standard'

    elev = math.asin(SUN_B[2]); azim = math.atan2(SUN_B[1], SUN_B[0])
    # world: a clear sky (the sky texture's own model, its kinds read, not guessed)
    w = bpy.data.worlds.new("sky"); sc.world = w; w.use_nodes = True
    nt = w.node_tree; bg = next(n for n in nt.nodes if n.type == 'BACKGROUND')
    sky = nt.nodes.new("ShaderNodeTexSky")
    kinds = [i.identifier for i in sky.bl_rna.properties["sky_type"].enum_items]
    for k in ("MULTIPLE_SCATTERING", "NISHITA", "SINGLE_SCATTERING", "HOSEK_WILKIE"):
        if k in kinds: sky.sky_type = k; break
    for prop, val in (("sun_elevation", elev), ("sun_rotation", azim - math.pi / 2 if False else (math.pi / 2 - azim)),
                      ("altitude", 300.), ("air_density", .9), ("dust_density", .08), ("aerosol_density", .08),
                      ("ozone_density", 1.), ("sun_disc", True), ("sun_size", math.radians(.9)), ("sun_intensity", .6)):
        if hasattr(sky, prop):
            try: setattr(sky, prop, val)
            except Exception: pass
    nt.links.new(sky.outputs["Color"], bg.inputs["Color"]); bg.inputs["Strength"].default_value = .22

    # the sun lamp lights the clouds crisply
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN'))
    sun.data.energy = 2.6; sun.data.angle = math.radians(.6); sun.data.color = (1., .97, .92)
    d = -SUN_B
    import mathutils
    sun.rotation_euler = mathutils.Vector(tuple(d)).to_track_quat('-Z', 'Y').to_euler()
    sc.collection.objects.link(sun)

    # cumulus: a slab 1.2 - 2.4 km up, 60 km across, density from warped noise, flat bases, cauliflower tops
    bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0, 18))
    slab = bpy.context.active_object; slab.name = "clouds"; slab.scale = (300, 300, 6)   # the whole sky at 1:100 (60 km, 1.2-2.4 km up): float precision kept triangle seams out
    m = bpy.data.materials.new("cloud"); m.use_nodes = True; mt = m.node_tree
    for n in list(mt.nodes): mt.nodes.remove(n)
    out = mt.nodes.new("ShaderNodeOutputMaterial")
    vol = mt.nodes.new("ShaderNodeVolumePrincipled")
    vol.inputs["Color"].default_value = (1, 1, 1, 1)
    vol.inputs["Anisotropy"].default_value = .55
    mt.links.new(vol.outputs[0], out.inputs["Volume"])
    tc = mt.nodes.new("ShaderNodeTexCoord")
    sep = mt.nodes.new("ShaderNodeSeparateXYZ"); mt.links.new(tc.outputs["Object"], sep.inputs[0])
    # the noise reads coordinates moved well off the origin: Blender's noise showed straight seams along the x = 0 and
    # y = 0 planes (cut clouds in the panorama)
    moved = mt.nodes.new("ShaderNodeVectorMath"); moved.operation = 'ADD'
    mt.links.new(tc.outputs["Object"], moved.inputs[0]); moved.inputs[1].default_value = (17.31, 23.77, 0.)
    # warp
    warp = mt.nodes.new("ShaderNodeTexNoise"); warp.inputs["Scale"].default_value = 6.; warp.inputs["Detail"].default_value = 3.
    mt.links.new(moved.outputs[0], warp.inputs["Vector"])
    mix = mt.nodes.new("ShaderNodeVectorMath"); mix.operation = 'MULTIPLY_ADD'
    mt.links.new(warp.outputs["Color"], mix.inputs[0]); mix.inputs[1].default_value = (.08, .08, .0)
    mt.links.new(moved.outputs[0], mix.inputs[2])
    # big cells (where clouds are) and detail (their billows)
    big = mt.nodes.new("ShaderNodeTexNoise"); big.inputs["Scale"].default_value = 30.; big.inputs["Detail"].default_value = 2.
    big.inputs["Roughness"].default_value = .45
    mt.links.new(mix.outputs[0], big.inputs["Vector"])
    fine = mt.nodes.new("ShaderNodeTexNoise"); fine.inputs["Scale"].default_value = 220.; fine.inputs["Detail"].default_value = 6.
    fine.inputs["Roughness"].default_value = .6
    mt.links.new(mix.outputs[0], fine.inputs["Vector"])
    # height profile: 0 at the bottom (z -1 in object space), a flat base at -0.75, rounded top
    h = mt.nodes.new("ShaderNodeMapRange"); mt.links.new(sep.outputs["Z"], h.inputs[0])
    h.inputs[1].default_value = -1.; h.inputs[2].default_value = 1.; h.inputs[3].default_value = 0.; h.inputs[4].default_value = 1.
    base = mt.nodes.new("ShaderNodeMapRange"); mt.links.new(h.outputs[0], base.inputs[0])
    base.inputs[1].default_value = .06; base.inputs[2].default_value = .12; base.inputs[3].default_value = 0.; base.inputs[4].default_value = 1.
    top = mt.nodes.new("ShaderNodeMapRange"); mt.links.new(h.outputs[0], top.inputs[0])
    top.inputs[1].default_value = 1.; top.inputs[2].default_value = .25; top.inputs[3].default_value = 0.; top.inputs[4].default_value = 1.
    prof = mt.nodes.new("ShaderNodeMath"); prof.operation = 'MULTIPLY'
    mt.links.new(base.outputs[0], prof.inputs[0]); mt.links.new(top.outputs[0], prof.inputs[1])
    # coverage: big noise minus a threshold that rises with height (tops narrower), plus billows
    shape = mt.nodes.new("ShaderNodeMath"); shape.operation = 'MULTIPLY_ADD'
    mt.links.new(fine.outputs["Fac"], shape.inputs[0]); shape.inputs[1].default_value = .3
    mt.links.new(big.outputs["Fac"], shape.inputs[2])
    lift = mt.nodes.new("ShaderNodeMath"); lift.operation = 'MULTIPLY_ADD'
    mt.links.new(h.outputs[0], lift.inputs[0]); lift.inputs[1].default_value = .16; lift.inputs[2].default_value = .84
    cov = mt.nodes.new("ShaderNodeMath"); cov.operation = 'SUBTRACT'
    mt.links.new(shape.outputs[0], cov.inputs[0]); mt.links.new(lift.outputs[0], cov.inputs[1])
    dens = mt.nodes.new("ShaderNodeMath"); dens.operation = 'MULTIPLY'; dens.use_clamp = False
    mt.links.new(cov.outputs[0], dens.inputs[0]); dens.inputs[1].default_value = 70.
    clamp = mt.nodes.new("ShaderNodeMath"); clamp.operation = 'MAXIMUM'
    mt.links.new(dens.outputs[0], clamp.inputs[0]); clamp.inputs[1].default_value = 0.
    fin = mt.nodes.new("ShaderNodeMath"); fin.operation = 'MULTIPLY'
    mt.links.new(clamp.outputs[0], fin.inputs[0]); mt.links.new(prof.outputs[0], fin.inputs[1])
    # thin out toward the slab's far edge, so the horizon is not one white wall of stacked clouds
    rad = mt.nodes.new("ShaderNodeVectorMath"); rad.operation = 'LENGTH'
    flat = mt.nodes.new("ShaderNodeCombineXYZ"); mt.links.new(sep.outputs["X"], flat.inputs["X"]); mt.links.new(sep.outputs["Y"], flat.inputs["Y"])
    mt.links.new(flat.outputs[0], rad.inputs[0])
    fade = mt.nodes.new("ShaderNodeMapRange"); mt.links.new(rad.outputs["Value"], fade.inputs[0])
    fade.inputs[1].default_value = .3; fade.inputs[2].default_value = .7; fade.inputs[3].default_value = 1.; fade.inputs[4].default_value = 0.
    faded = mt.nodes.new("ShaderNodeMath"); faded.operation = 'MULTIPLY'
    mt.links.new(fin.outputs[0], faded.inputs[0]); mt.links.new(fade.outputs[0], faded.inputs[1])
    scale_d = mt.nodes.new("ShaderNodeMath"); scale_d.operation = 'MULTIPLY'
    mt.links.new(faded.outputs[0], scale_d.inputs[0]); scale_d.inputs[1].default_value = 45.
    mt.links.new(scale_d.outputs[0], vol.inputs["Density"])
    slab.data.materials.append(m)

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = (0, 0, .02); cam.rotation_euler = (math.pi / 2, 0, -math.pi / 2)
    cam.data.type = 'PANO'
    try: cam.data.panorama_type = 'EQUIRECTANGULAR'
    except Exception: cam.data.cycles.panorama_type = 'EQUIRECTANGULAR'
    cam.data.clip_end = 2000

    tmp = os.path.join(HERE, "Renders", "sky_raw.exr")
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    sc.render.image_settings.file_format = 'OPEN_EXR'
    sc.render.image_settings.color_depth = '16'
    sc.render.filepath = tmp
    bpy.ops.render.render(write_still=True)

    # roll the panorama so the sun sits where Unity looks for it
    img = bpy.data.images.load(tmp)
    px = np.array(img.pixels[:], np.float32).reshape(img.size[1], img.size[0], 4)   # rows bottom-up
    hgt, wid = px.shape[:2]
    lum = px[..., :3].sum(-1)
    upper = lum[hgt // 2:, :]
    r, c = np.unravel_index(np.argmax(upper), upper.shape)
    sun_u_now = (c + .5) / wid
    target_u = .5 - math.atan2(SUN_UNITY[2], SUN_UNITY[0]) / (2 * math.pi)
    shift = int(round((target_u - sun_u_now) * wid)) % wid
    px = np.roll(px, shift, axis=1)
    print("SKY sun found at u %.3f v %.3f, wants u %.3f v %.3f: rolled %d px" %
          (sun_u_now, (r + hgt // 2 + .5) / hgt, target_u, 1 - math.acos(SUN_UNITY[1]) / math.pi, shift))
    outp = os.path.join(HERE, "Materials", "Textures", "TU_SkyClouds.exr")
    o = bpy.data.images.new("TU_SkyClouds", wid, hgt, alpha=False, float_buffer=True)
    o.pixels.foreach_set(px.ravel())
    o.filepath_raw = outp; o.file_format = 'OPEN_EXR'
    sc.render.image_settings.file_format = 'OPEN_EXR'; sc.render.image_settings.color_depth = '16'
    o.save_render(outp, scene=sc)
    prev = bpy.data.images.new("prev", wid, hgt, alpha=False)
    pp = px.copy(); pp[..., :3] = np.clip(pp[..., :3] * .8, 0, 1) ** (1 / 2.2)
    prev.pixels.foreach_set(pp.ravel()); prev.filepath_raw = os.path.join(HERE, "Renders", "sky_preview.png"); prev.file_format = 'PNG'; prev.save()
    print("SKY wrote", outp)


main()
