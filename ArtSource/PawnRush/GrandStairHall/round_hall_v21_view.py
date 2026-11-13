# Opens a round hall concept blend (v21B / v21C) looking through its overview camera, in the renders' clay look.
# Run: blender grand_stair_hall_v21B.blend --python round_hall_v21_view.py
import bpy


def setup():
    for w in bpy.context.window_manager.windows:
        for a in w.screen.areas:
            if a.type == 'VIEW_3D':
                sp = a.spaces.active; sh = sp.shading
                sh.type = 'SOLID'; sh.color_type = 'MATERIAL'; sh.light = 'STUDIO'
                sh.show_cavity = True; sh.cavity_type = 'BOTH'; sh.show_backface_culling = True
                sp.clip_end = 2000; sp.overlay.show_floor = False
                sp.region_3d.view_perspective = 'CAMERA'
    return None


bpy.app.timers.register(setup, first_interval=1.0)
