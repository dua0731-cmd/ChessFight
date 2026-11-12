# Gate end only (R558), headless: blender -b KingdomKit.blend --factory-startup --python gate_end_headless.py
# Builds the side walls, vault, floor and the new gate end of the full hall, renders the gate from the
# picture's own viewpoint (level camera 23.8 m out, 2.3 m up, 40 deg: the end wall at 2.6 cm per pixel),
# closer views and the leaves swung into the passage, then stacks the picture over the matching render.
import bpy, os, builtins, time, math
import numpy as np

KIT = os.path.dirname(os.path.abspath(__file__)) if "__file__" in dir() else r"C:\Work\ChessFight\ArtSource\Lobby\KingdomInterior\Kit"
KIT = r"C:\Work\ChessFight\ArtSource\Lobby\KingdomInterior\Kit"

t0 = time.time()
K = {"__file__": os.path.join(KIT, "kit_build.py"), "__name__": "kit_build"}
exec(open(K["__file__"], encoding="utf-8").read(), K)
builtins.KIT_G = K
p = os.path.join(KIT, "stair_end.py")
G = {"__file__": p, "__name__": "stair_end"}
exec(open(p, encoding="utf-8").read(), G)

G["full_hall"](-25.0)
G["clear_se"]()
G["LT_PLACED"].clear()
sc, root = G["se_scene"]()
shell = G["se_col"]("Shell")
gate = G["se_col"]("Gate End")
G["side_walls"](shell)
G["vault"](shell)
G["gate_end"](gate)
G["lantern_glass"](G["se_col"]("Decor"))
mb = G["MB"]()
mb.box(-G["SIDE_X"], G["SIDE_X"], G["HALL_FRONT"], G["BACK_Y"], -.2, 0)
G["finish"](mb, "Floor", shell, bevel=0)
print("gate lantern glass middles", [tuple(round(c, 3) for c in a) for a in G["GATE_LANTERN_AXIS"]])
for side in ("L", "R"):
    pv = bpy.data.objects[f"GateLeaf_{side}"]
    print("hinge", side, tuple(round(c, 3) for c in pv.location), [k.name for k in pv.children])

with bpy.context.temp_override(scene=sc, view_layer=sc.view_layers[0]):
    dg = bpy.context.evaluated_depsgraph_get()
total = 0
for c in (gate, G["se_col"]("Decor")):
    for o in c.objects:
        if o.type != "MESH":
            continue
        e = o.evaluated_get(dg)
        m = e.to_mesh()
        n = sum(len(f.vertices) - 2 for f in m.polygons)
        total += n
        print("tris %-24s %7d" % (o.name, n))
        e.to_mesh_clear()
print("gate tris", total)

GW = G["GATE_WALL"]
G["view_cam"]("GE Cam Picture", (0, GW + 23.8, 2.3), (0, GW, 2.3), 40)
G["view_cam"]("GE Cam Close", (2.6, GW + 9.0, 2.4), (0, GW, 4.2), 50)
G["view_cam"]("GE Cam Low", (-4.5, GW + 5.5, 1.6), (-.5, GW, 3.8), 60)
G["view_cam"]("GE Cam Lobby", (0.4, -12.5 + 6, 2.6), (0, GW - 1, 2.8), 36)    # near the Unity walk to the gate
G["view_cam"]("GE Cam Vault", (3.0, GW + 16.0, 1.8), (0, GW + 3.0, 12.5), 62)   # up at the roof and the gate end

shots = {}
for name, cam_name, w, h in (("gate_view.png", "GE Cam Picture", 1418, 667),
                              ("gate_close.png", "GE Cam Close", 1400, 1000),
                              ("gate_low.png", "GE Cam Low", 1400, 1000)):
    out = os.path.join(KIT, name)
    G["render_view"](out, cam_name, w, h)
    shots[name] = out
    print("rendered", name, round(time.time() - t0, 1))
# the roof (render_view hides "Vault Ribs" by name: Workbench shadows of the ribs stripe the walls)
G["view_cam"]("GE Cam Roof", (-2.0, GW + 4.0, 2.0), (1.0, GW + 22.0, 13.5), 70)    # along the hall to the stair end
rib = bpy.data.objects["Vault Ribs"]
rib.name = "Vault Ribs Shown"
for name, cam_name in (("gate_vault.png", "GE Cam Vault"), ("gate_roof.png", "GE Cam Roof")):
    out = os.path.join(KIT, name)
    G["render_view"](out, cam_name, 1400, 1000)
    shots[name] = out
rib.name = "Vault Ribs"
# the leaves swung 96 deg into the passage (LobbyStage.Castle.cs GateOpenDegrees)
bpy.data.objects["GateLeaf_L"].rotation_euler.z = math.radians(-96)
bpy.data.objects["GateLeaf_R"].rotation_euler.z = math.radians(96)
out = os.path.join(KIT, "gate_open.png")
G["render_view"](out, "GE Cam Close", 1400, 1000)
shots["gate_open.png"] = out
bpy.data.objects["GateLeaf_L"].rotation_euler.z = 0
bpy.data.objects["GateLeaf_R"].rotation_euler.z = 0


def load(path):
    im = bpy.data.images.load(path, check_existing=False)
    w, h = im.size
    a = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)


def save(a, name):
    im = bpy.data.images.new(name, a.shape[1], a.shape[0], alpha=True)
    im.pixels.foreach_set(a.ravel())
    im.filepath_raw = os.path.join(KIT, name)
    im.file_format = "PNG"
    im.save()


ref = load(os.path.join(KIT, "reference_gate_end.webp"))
view = load(shots["gate_view.png"])
bar = np.ones((10, ref.shape[1], 4), np.float32)
bar[..., :3] = .12
save(np.concatenate([view, bar, ref], 0), "gate_compare.png")          # rows start at the bottom: picture on top
mix = ref.copy()
mix[..., :3] = ref[..., :3] * .5 + view[..., :3] * .5
save(mix, "gate_overlay.png")
print("done", round(time.time() - t0, 1))
