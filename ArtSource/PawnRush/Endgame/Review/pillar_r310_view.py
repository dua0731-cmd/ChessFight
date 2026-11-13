import bpy
def setup():
    for w in bpy.context.window_manager.windows:
        for a in w.screen.areas:
            if a.type=='VIEW_3D':
                sp=a.spaces.active; sh=sp.shading
                sh.type='SOLID'; sh.color_type='MATERIAL'; sh.show_cavity=True; sh.show_shadows=True; sh.light='STUDIO'
                sp.clip_end=2000; sp.overlay.show_floor=False
                sp.region_3d.view_perspective='CAMERA'
    return None
bpy.app.timers.register(setup, first_interval=1.0)
