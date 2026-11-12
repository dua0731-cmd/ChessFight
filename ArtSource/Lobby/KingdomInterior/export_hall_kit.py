# Export the kingdom hall for Unity with the kit-built stair end (R549):
#   blender -b Kit/KingdomKit.blend --python export_hall_kit.py [-- out.fbx]
# Builds the stair end from the lobby kit (Kit/stair_end.py, side walls / vault / floor run on to the
# gate wall, and the gate end, R558), appends the carpets and front steps from KingdomHall_blockout.blend, thins the
# dense kit parts, and writes Assets/Resources/Lobby/KingdomHall/Hall.fbx the way export_hall.py does:
# one joined mesh per collection, apart from what the lobby code drives (GateLeaf_L / GateLeaf_R hinge
# empties with one joined child each, Gate_Passage_End). Axes and units as export_hall.py.
import bpy, os, sys, builtins

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.join(HERE, "Kit")
HALL_BLEND = os.path.join(HERE, "KingdomHall_blockout.blend")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PAINT = "--paint" in argv               # R576: paint and bake colour textures (Kit/hall_paint.py)
argv = [a for a in argv if not a.startswith("--")]
OUT = argv[0] if argv else os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "Lobby", "KingdomHall", "Hall.fbx"))
BAKE_DIR = os.path.join(os.path.dirname(OUT), "Bake")
if PAINT:
    P = {"__file__": os.path.join(KIT, "hall_paint.py"), "__name__": "hall_paint"}
    exec(open(P["__file__"], encoding="utf-8").read(), P)
GATE_WALL = -25.0                      # blockout_hall.py
# R552: no banners (승규 "깃발도 지워줘"); R558: the gate end is the kit's too (stair_end.py gate_end, from
# 승규's gate picture), the blockout's gate end and the wall patch above it are gone
FROM_BLOCKOUT = ("Carpets", "Front Steps")
# dense kit parts and how much of them to keep (Collapse decimation; flat cloth and frames take it well)
THIN = {"P BN Banner 1": .45, "P BN Banner 2": .45, "P BN Banner 3": .45, "P WN1 Window Arched": .5,
        "P CH Chandelier": .5, "P C1 Crown Pedestal": .5, "P LT Wall Lantern": .5, "P KN Knight White": .65}

# ---------------------------------------------------------------- the stair end from the kit
K = {"__file__": os.path.join(KIT, "kit_build.py"), "__name__": "kit_build"}
exec(open(K["__file__"], encoding="utf-8").read(), K)
builtins.KIT_G = K
p = os.path.join(KIT, "stair_end.py")
G = {"__file__": p, "__name__": "stair_end"}
exec(open(p, encoding="utf-8").read(), G)
G["full_hall"](GATE_WALL)
G["build_stair_end"]()
se_sc, root = G["se_scene"]()
print("GATE LANTERN GLASS", [tuple(round(c, 3) for c in a) for a in G["GATE_LANTERN_AXIS"]])

# ---------------------------------------------------------------- one scene holds everything to export
sc = bpy.context.scene
for c in list(sc.collection.children):
    sc.collection.children.unlink(c)
for o in list(sc.collection.objects):
    sc.collection.objects.unlink(o)
sc.collection.children.link(root)
for name in ("SE Stand-ins", "SE Cameras", "SE Lights"):
    c = bpy.data.collections.get(name)
    if c is not None:
        for o in list(c.objects):
            if o.name != "Floor":          # the board stand-in goes (Unity draws the stage), the floor stays
                bpy.data.objects.remove(o, do_unlink=True)
# the glass vault's ribs apart (Hall_Vault): Unity keeps them out of the sun's shadows, their stripes
# crossed the whole lobby view
ribs = bpy.data.objects.get("Vault Ribs")
if ribs is not None:
    vault = bpy.data.collections.new("Vault")
    sc.collection.children.link(vault)
    for c in list(ribs.users_collection):
        c.objects.unlink(ribs)
    vault.objects.link(ribs)
with bpy.data.libraries.load(HALL_BLEND, link=False) as (src, dst):
    dst.collections = [n for n in FROM_BLOCKOUT if n in src.collections]
for c in dst.collections:
    sc.collection.children.link(c)
banners = bpy.data.collections.get("Banners")
if banners is not None:          # only the gate end's banners: the kit side walls carry their own
    for o in list(banners.objects):
        if not o.name.startswith("Gate Banner"):
            bpy.data.objects.remove(o, do_unlink=True)
clay = bpy.data.materials["Clay"]
for m in list(bpy.data.materials):
    if m is not clay and m.name.startswith("Clay"):
        m.user_remap(clay)
for o in list(bpy.data.objects):
    if o.type in ("CAMERA", "LIGHT"):
        bpy.data.objects.remove(o, do_unlink=True)

# ---------------------------------------------------------------- thin the dense kit parts
def thin(me, ratio):
    tmp = bpy.data.objects.new("thin tmp", me)
    sc.collection.objects.link(tmp)
    d = tmp.modifiers.new("Thin", "DECIMATE")
    d.ratio = ratio
    dg = bpy.context.evaluated_depsgraph_get()
    new = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg), depsgraph=dg)
    bpy.data.objects.remove(tmp, do_unlink=True)
    before = sum(len(f.vertices) - 2 for f in me.polygons)
    after = sum(len(f.vertices) - 2 for f in new.polygons)
    me.user_remap(new)
    new.name = me.name + " Thin"
    print("THIN %-24s %6d -> %6d" % (me.name, before, after))

for name, r in THIN.items():
    me = bpy.data.meshes.get(name)
    if me is not None and me.users:
        thin(me, r)

# ---------------------------------------------------------------- join and export (as export_hall.py)
def real(objs):
    """Each object its own mesh with every modifier applied (kit parts share their mesh)."""
    dg = bpy.context.evaluated_depsgraph_get()
    out = []
    for o in objs:
        o.hide_viewport = False
        if o.type == "CURVE" or o.modifiers or o.data.users > 1:
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), depsgraph=dg)
            if o.type == "CURVE":
                n = bpy.data.objects.new(o.name, me)
                for c in o.users_collection:
                    c.objects.link(n)
                n.matrix_world = o.matrix_world.copy()
                n.parent = o.parent
                n.matrix_world = o.matrix_world.copy()
                bpy.data.objects.remove(o, do_unlink=True)
                o = n
            else:
                o.modifiers.clear()
                o.data = me
        out.append(o)
    return out


def join(objs, name):
    objs = real(objs)
    if PAINT:
        P["paint"](objs)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    j = bpy.context.view_layer.objects.active
    j.name = name
    j.data.name = name
    if not j.data.materials:
        j.data.materials.append(clay)
    return j


for side in ("L", "R"):
    pivot = bpy.data.objects[f"GateLeaf_{side}"]
    kids = [k for k in pivot.children if k.type in ("MESH", "CURVE")]
    leaf = join(kids, f"GateLeaf_{side}_Door")
    m = leaf.matrix_world.copy()
    leaf.parent = pivot
    leaf.matrix_world = m

KEEP_APART = {"Gate Passage End", "Rose Tracery", "Lantern Glass",   # Rose_Tracery: origin on the window's centre,
              "Rose Glass Gold", "Rose Glass Blue", "Rose Glass Red", "Rose Glass Amber",
              "Window Glass Light", "Window Glass Blue",   # R582: the side windows' diamond panes, Unity lights them
              "Vault Glass"}   # R588: the vault's glass, Unity makes it see-through
# Unity turns it; Lantern_Glass (R556): the kit lanterns' glass, Unity makes it glow; Rose_Glass_* (R563): the round
# window's stained glass, Unity makes it glow and turns it with the tracery
leaf_parts = {o for o in bpy.data.objects if o.parent is not None and o.parent.name.startswith("GateLeaf_")}
for c in list(sc.collection.children_recursive):
    objs =[o for o in c.objects if o.type in ("MESH", "CURVE") and o.name not in KEEP_APART and o not in leaf_parts]
    if objs:
        label = {"SE Stand-ins": "Floor"}.get(c.name, c.name.removeprefix("SE "))
        join(objs, "Hall_" + label.replace(" ", "_"))
for name in KEEP_APART:
    o = real([bpy.data.objects[name]])[0]
    if PAINT:
        P["paint"]([o])
    o.name = name.replace(" ", "_")
if PAINT:
    P["bake"]([o for o in sc.objects if o.type == "MESH"], BAKE_DIR)

sc.unit_settings.system = "METRIC"
sc.unit_settings.scale_length = 1.0
objs = [o for o in sc.objects]
tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH")
for o in sorted(objs, key=lambda o: o.name):
    t = sum(len(p.vertices) - 2 for p in o.data.polygons) if o.type == "MESH" else 0
    print("OBJ %-28s %-6s %7d %s" % (o.name, o.type, t, o.parent.name if o.parent else ""))
print("TRIS", tris)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=False, object_types={"EMPTY", "MESH"},
                         apply_scale_options="FBX_SCALE_UNITS", use_mesh_modifiers=True,
                         mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="AUTO", embed_textures=False)
print("WROTE", OUT)
