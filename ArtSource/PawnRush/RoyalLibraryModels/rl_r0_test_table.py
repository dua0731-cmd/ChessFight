"""R0 test piece (seoungtest R687, 2026-11-18): one course table, end to end into Unity, to judge the pipeline and the
look before the zones run (PLAN.md R0 "시험 한 점").

    blender -b --factory-startup --python rl_r0_test_table.py

Uses the E5 tables session's parametric table (e5/rl_e5_06_07_tables.py, build_table(W, D): walnut top with planks,
brass rim, corner caps and rivets, aprons, turned legs - the course table kit) at the start table's size (blockout
"Approach floor -146": 18 x 17 m, centre z -137.5 in Unity, walkable top y 0), then:
  palette materials -> rl_kit slots (walnut, brass), curves and modifiers applied, joined into one mesh, metre UVs
  (rl_kit.surface_uvs), FBX via rl_kit.export -> Assets/Resources/RoyalLibrary/Models/R0/RlStartTable.fbx
RoyalLibraryDressing.R0.cs hangs it and stops the blockout's "Approach floor -146*" drawing (its collider stays).
Never run while Unity is in Play.

KEPT FOR THE RECORD (11-18): R1 took the start table over (rl_r1_entrance_model.py -> Models/Entrance/RlStartTable.fbx,
RoyalLibraryDressing.Entrance.cs); Models/R0 and RoyalLibraryDressing.R0.cs are gone. Do not run this again.
"""
import os, sys
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

SLOT_OF = {"E5_Walnut": "walnut", "E5_WalnutTop": "walnut", "E5_Brass": "brass", "E5_BrassDark": "brass"}
CENTRE_Y = -137.5       # Blender y = Unity z of the start table's centre
W, D = 18.0, 17.0


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    g = {"__name__": "rl_e5_tables", "__file__": os.path.join(HERE, "e5", "rl_e5_06_07_tables.py")}
    exec(open(g["__file__"], encoding="utf-8").read(), g)   # defines build_table & co, builds nothing
    root = g["build_course_table"](offset=(0, CENTRE_Y, 0), W=W, D=D)
    import rl_kit as k

    src = bpy.data.collections["E5_06_CourseTable"]
    bpy.context.view_layer.update()
    col = bpy.data.collections.new("R0 test table")
    bpy.context.scene.collection.children.link(col)
    meshes = []
    for o in list(src.objects):
        if o.type not in {"MESH", "CURVE"}:
            continue
        mw = o.matrix_world.copy()
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=False, depsgraph=dg)
        for i, m in enumerate(me.materials):
            name = m.name if m else ""
            if name not in SLOT_OF:
                raise RuntimeError("no slot for %s on %s" % (name, o.name))
            me.materials[i] = k.slot(SLOT_OF[name])
        ob = bpy.data.objects.new(o.name, me)
        ob.matrix_world = mw
        col.objects.link(ob)
        meshes.append(ob)
    bpy.data.collections.remove(src)
    for o in list(bpy.data.objects):
        if o.type == "EMPTY":
            bpy.data.objects.remove(o, do_unlink=True)
    for ob in meshes:
        k.surface_uvs(ob)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in meshes:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    table = bpy.context.view_layer.objects.active
    table.name = "RlStartTable"
    # merge the slots that now repeat (several palette materials became one slot)
    mats = table.data.materials
    first = {}
    for i, m in enumerate(mats):
        first.setdefault(m.name, i)
    for p in table.data.polygons:
        p.material_index = first[mats[p.material_index].name]
    for i in reversed(range(len(mats))):
        if first[mats[i].name] != i:
            mats.pop(index=i)
    print("[r0] table", len(table.data.polygons), "faces, slots", [m.name for m in table.data.materials])
    k.export(col.name, "RlStartTable", "R0")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "rl_r0_test_table.blend"))


main()
