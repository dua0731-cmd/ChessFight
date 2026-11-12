# Round window (R563), headless: blender -b KingdomKit.blend --factory-startup --python rose_headless.py
# Builds the stair end and renders the round window face-on, at an angle, its tracery close up and the lobby view.
import bpy, os, builtins, time

KIT = r"C:\Work\ChessFight\ArtSource\Lobby\KingdomInterior\Kit"

t0 = time.time()
K = {"__file__": os.path.join(KIT, "kit_build.py"), "__name__": "kit_build"}
exec(open(K["__file__"], encoding="utf-8").read(), K)
builtins.KIT_G = K
p = os.path.join(KIT, "stair_end.py")
G = {"__file__": p, "__name__": "stair_end"}
exec(open(p, encoding="utf-8").read(), G)

G["build_stair_end"]()
sc, root = G["se_scene"]()
with bpy.context.temp_override(scene=sc, view_layer=sc.view_layers[0]):
    dg = bpy.context.evaluated_depsgraph_get()
for o in bpy.data.objects:
    if o.name.startswith(("Rose", "Round Window")):
        e = o.evaluated_get(dg)
        m = e.to_mesh()
        print("tris %-18s %7d  loc %s" % (o.name, sum(len(f.vertices) - 2 for f in m.polygons), tuple(round(c, 3) for c in o.location)))
        e.to_mesh_clear()

cams = (("rose_front.png", "RW Front", (0.0, 4.0, 8.2), (0.0, 15.5, 8.2), 46, 1200, 1200),
        ("rose_angle.png", "RW Angle", (5.5, 6.0, 4.5), (0.0, 15.5, 8.2), 55, 1300, 1000),
        ("rose_close.png", "RW Close", (1.2, 12.0, 8.0), (0.6, 15.6, 8.8), 50, 1200, 1000))
for out, cam, at, look, fov, w, h in cams[:2]:
    G["view_cam"](cam, at, look, fov)
    G["render_view"](os.path.join(KIT, out), cam, w, h)
    print("rendered", out, round(time.time() - t0, 1))
G["render_view"](os.path.join(KIT, "rose_lobby.png"), "SE Cam Lobby A", 1600, 900)
print("done", round(time.time() - t0, 1))
