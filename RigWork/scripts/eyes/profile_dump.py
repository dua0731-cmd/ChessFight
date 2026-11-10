import bpy, sys, math
import numpy as np
for p in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=p)
    o = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    me = o.data
    zs = np.array([v.co.z for v in me.vertices]); rs = np.array([math.hypot(v.co.x, v.co.y) for v in me.vertices])
    # how many distinct azimuth columns (facets around) near the face, and the profile around it
    sel = (zs > 0.45) & (zs < 0.7)
    az = np.degrees(np.arctan2(np.array([v.co.x for v in me.vertices])[sel], -np.array([v.co.y for v in me.vertices])[sel]))
    print(p.split("/")[-1], "verts in 0.45..0.7:", sel.sum(), "distinct z levels:", len(np.unique(np.round(zs[sel], 3))))
    for z in np.arange(0.44, 0.72, 0.02):
        m = (zs > z - 0.01) & (zs < z + 0.01)
        print("  z %.2f  n %4d  r max %.4f  r median %.4f" % (z, m.sum(), rs[m].max() if m.any() else 0, np.median(rs[m]) if m.any() else 0))
    # face sizes near the eyes
    areas = [f.area for f in me.polygons if 0.45 < f.center.z < 0.7 and f.center.y < 0]
    print("  front faces 0.45..0.7:", len(areas), "median edge ~ %.4f m, max ~ %.4f m" % (math.sqrt(np.median(areas) * 2), math.sqrt(max(areas) * 2)))
