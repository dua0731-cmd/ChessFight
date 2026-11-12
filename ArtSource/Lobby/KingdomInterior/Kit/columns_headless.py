# Pilasters and round columns (R560), headless: blender -b KingdomKit.blend --factory-startup --python columns_headless.py
# Builds the whole hall (stair end, side walls, gate end), prints the pilaster lanterns' glass middles for
# Unity's lights, and renders a side pilaster face-on and at an angle, the big front column (whole, base,
# capital), the balcony columns, the last pilaster by the gate lantern and the lobby camera's view.
import bpy, os, builtins, time

KIT = r"C:\Work\ChessFight\ArtSource\Lobby\KingdomInterior\Kit"

t0 = time.time()
K = {"__file__": os.path.join(KIT, "kit_build.py"), "__name__": "kit_build"}
exec(open(K["__file__"], encoding="utf-8").read(), K)
builtins.KIT_G = K
p = os.path.join(KIT, "stair_end.py")
G = {"__file__": p, "__name__": "stair_end"}
exec(open(p, encoding="utf-8").read(), G)

G["full_hall"](-25.0)
G["build_stair_end"]()
sc, root = G["se_scene"]()
print("pilaster lantern glass middles", [tuple(round(c, 3) for c in a) for a in G["LANTERN_AXIS"]])
with bpy.context.temp_override(scene=sc, view_layer=sc.view_layers[0]):
    dg = bpy.context.evaluated_depsgraph_get()
for name in ("Columns", "Column Blocks", "Side Bays"):
    e = bpy.data.objects[name].evaluated_get(dg)
    m = e.to_mesh()
    print("tris %-14s %7d" % (name, sum(len(f.vertices) - 2 for f in m.polygons)))
    e.to_mesh_clear()

cams = (("col_pilaster_front.png", "CL Pilaster Front", (2.5, .27, 4.6), (9.5, .27, 4.7), 70, 900, 1300),
        ("col_pilaster_angle.png", "CL Pilaster Angle", (4.0, -5.5, 2.0), (9.2, .27, 4.5), 55, 1300, 1000),
        ("col_front_column.png", "CL Front Column", (4.0, 2.0, 1.8), (9.15, 8.45, 4.8), 72, 900, 1300),
        ("col_column_top.png", "CL Column Top", (6.3, 4.6, 7.4), (9.15, 8.45, 9.6), 45, 1200, 1000),
        ("col_column_base.png", "CL Column Base", (6.6, 5.4, 1.4), (9.15, 8.45, .7), 45, 1200, 1000),
        ("col_back_columns.png", "CL Back Columns", (0.0, 5.5, 6.0), (0.0, 15.4, 7.0), 62, 1300, 1000),
        ("col_gate_corner.png", "CL Gate Corner", (5.0, -19.0, 4.0), (8.8, -24.6, 5.6), 55, 1200, 1000))
for out, cam, at, look, fov, w, h in cams:
    G["view_cam"](cam, at, look, fov)
    G["render_view"](os.path.join(KIT, out), cam, w, h)
    print("rendered", out, round(time.time() - t0, 1))
G["render_view"](os.path.join(KIT, "col_lobby.png"), "SE Cam Lobby A", 1600, 900)
print("done", round(time.time() - t0, 1))
