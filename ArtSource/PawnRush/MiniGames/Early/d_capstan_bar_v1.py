# D capstan, one bar (R543, user 11-11: "캡스턴은 막대는 인원수 만큼으로 정해주고"): the capstan has a bar for each player
# of the team (three to six), so Unity hangs ONE bar's model on each of the game's bars (MiniGameModels.Capstan) instead
# of the four-bar model.
#
#   "C:/Program Files/Blender Foundation/Blender 3.4/blender.exe" -b --python d_capstan_bar_v1.py
#
# Source: Export/D_Capstan_Bars.fbx (d_capstan_v1.py: the four bars on the axes, pivot on the drum's axis, Blender
# x / y across, z up). This keeps the bar along +x - the game's first bar, MgCapstan builds the others turned from it -
# and writes Export/D_Capstan_Bar.fbx with the same pivot, the same UVs and the same material (PR_Early_Capstan).
import bpy, bmesh, os

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Export", "D_Capstan_Bars.fbx")
OUT = os.path.join(HERE, "Export", "D_Capstan_Bar.fbx")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    obs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    ob = obs[0]
    bpy.context.view_layer.objects.active = ob
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    bm = bmesh.new(); bm.from_mesh(ob.data)
    before = len(bm.faces)
    # the +x bar: everything further out along +x than it is across
    gone = [v for v in bm.verts if not (v.co.x > .2 and abs(v.co.y) < v.co.x)]
    bmesh.ops.delete(bm, geom=gone, context='VERTS')
    bm.to_mesh(ob.data); bm.free()
    ob.name = ob.data.name = "D_Capstan_Bar"
    co = [v.co for v in ob.data.vertices]
    print("BAR faces %d of %d, x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (
        len(ob.data.polygons), before, min(c.x for c in co), max(c.x for c in co), min(c.y for c in co), max(c.y for c in co),
        min(c.z for c in co), max(c.z for c in co)))
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', path_mode='STRIP',
                             bake_space_transform=True)


if __name__ == "__main__":
    main()
