# E Royal Library lights (R507): B's chandelier (eight EMPTY candle cups) and wall sconce (EMPTY lantern cage), from
# the same Tripo set R503_FantasyArchitectural and b_hall_v1.py's own builders, hung round the E room. Removing the
# R312 shell took its code chandeliers with it. Flames and lights are never in a model (user rule, 2026-11-07):
# SOCKET_Flame / SOCKET_Light / SOCKET_Lantern empties mark where MiniGameFixtures.LightSockets adds them in Unity.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python e_lights_v1.py -- [--render] [--export]
#
# Output Export/E_LightsEnv.fbx in the section frame (Blender x = section x, y = section z, z = up), symmetric in x.
# Placement, clear of the bookcase towers (x +-7 and +-17 at z 9, 15, 21, up to 10.3 m with the bell) and of the
# ceiling beams (z 5, 10, 15, 20, 12.5..13 m):
#   chandeliers  over the central aisle (x 0, z 7.5, 12.5, 17.5) and between each team's tower columns (x +-12,
#                z 7.5, 17.5), the chain's top at 12.9 m, 2.6 m tall (bottom 10.3 m)
#   sconces      on the side walls' pilasters between the ground windows (x +-21.98, z 5, 10, 15, 20, 3.4 m up) and
#                on the north wall between its two plain modules (x +-18.4, z 24.38)
import bpy, math, os, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import b_core_scene_v1 as cs  # noqa: E402
import b_hall_v1 as bh        # noqa: E402  (fantasy_rule, build_chandelier, build_sconce)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
CHANDELIERS = [(0., 7.5), (0., 12.5), (0., 17.5), (-12., 7.5), (12., 7.5), (-12., 17.5), (12., 17.5)]
CHANDELIER_TOP = 12.9
SCONCES = [(s * 21.98, z, s) for s in (-1., 1.) for z in (5., 10., 15., 20.)] + [(-18.4, 24.38, 0.), (18.4, 24.38, 0.)]
SCONCE_Y = 3.4


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    fa = cs.split(cs.load("R503_FantasyArchitectural"), bh.fantasy_rule)
    for k in list(fa):
        if k not in ("chandelier", "sconce"): bpy.data.objects.remove(fa.pop(k))
    for o in fa.values(): cs.kit_material(o, "PR_Mid_HallKit")
    chand, flames, chand_light = bh.build_chandelier(fa["chandelier"])
    sconce, sconce_flame = bh.build_sconce(fa["sconce"])
    env, sockets = [], []
    for x, z in CHANDELIERS:
        at = Vector((x, z, CHANDELIER_TOP))
        env.append(cs.place_copy(chand, "chandelier", Matrix.Translation(at)))
        sockets += [("SOCKET_Flame", at + f) for f in flames] + [("SOCKET_Light", at + chand_light)]
    for x, z, side in SCONCES:
        yaw = 0. if side == 0 else (90. if side < 0 else -90.)       # lantern (-y) to the south, or into the room
        R = Matrix.Rotation(math.radians(yaw), 4, 'Z')
        at = Vector((x, z, SCONCE_Y))
        env.append(cs.place_copy(sconce, "sconce", Matrix.Translation(at) @ R))
        sockets.append(("SOCKET_Lantern", at + R.to_3x3() @ sconce_flame))
    lights = cs.join(env, "E_LightsEnv")
    for name, at in sockets:
        cs.socket(lights, name, at)
    print("LIGHTS tris", cs.tris(lights), "sockets", len(sockets))
    for o in (chand, sconce):
        bpy.data.objects.remove(o)
    if "--export" in ARGS:
        cs.export(lights, "E_LightsEnv")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "E_Lights_v1.blend"))
    if "--render" in ARGS:
        render(lights, sockets)


def render(lights, sockets):
    """The lights in the E hall and towers, with a Blender point light at each socket for the preview."""
    for name in ("E_HallEnv", "E_StationEnv"):
        bpy.ops.import_scene.fbx(filepath=os.path.join(cs.OUT_FBX, name + ".fbx"))
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name not in ("E_LightsEnv",)]
    for o in objs:
        o.data.transform(o.matrix_world); o.matrix_world.identity()
        if o.name.startswith("E_StationEnv"):
            for x in (-12., 12.): cs.place_copy(o, "station", Matrix.Translation((x, 4., .4)))
            o.hide_render = True
    for name, at in sockets:
        if name == "SOCKET_Flame": continue
        bpy.ops.object.light_add(type='POINT', location=at)
        bpy.context.object.data.energy = 900 if name == "SOCKET_Light" else 250
        bpy.context.object.data.color = (1, .74, .45)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.6, .7, .85, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .35; sc.world = w
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    for name, (loc, tgt, lens) in {"entry": ((0, 1.5, 4.5), (0, 20, 7), 16), "side": ((-6, 6, 3), (-22, 13, 5), 18)}.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_e_lights_%s.png" % name)
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
