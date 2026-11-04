# R345 (승규 11-04, a Unity shot up the grand stair, the bishop and the knight on their plinths): "동상 1개 텍스쳐
# 입혀주고 받침대는 다른 텍스쳐로 해줘 시선을 좀더 더 돌려서 계단 쪽으로 돌려 아주 조금" -> "4개동상 전부 받침대는 다른
# 텍스쳐로", "흰 대리석 + 짙은 녹색 대리석". The four statues beside the grand stair (stairside_statues_v25.py) painted
# by their own shape, in materials Unity dresses in world space (the meshes carry no UVs):
#   Statue_Marble   the figure                      -> PR_StatueMarble (white marble, grey veins)
#   Plinth_Marble   its own pedestal and the plinth -> PR_Travertine (R347: warm cream travertine, honey veins; R345 had
#                   PR_GreenMarble, deep green - options in pedestal_colors_v37.py)
#   Palace_Gold     on the pedestal only clean lines: its flat ledges (faces looking straight up below its deck), a band
#                   round its deck and one round its foot (the carved relief stays green: picked by a Laplacian it
#                   speckled the whole pedestal); on the plinth: fillets between its tiers, a frame and a rosette on
#                   each of its eight faces
# The pedestal is everything below its deck: the level (above 0.25 m, below 3 m) with the most area looking straight
# up. Each statue turns 10 degrees further towards the stair (its 30-degree turn down the stair now 20), round the
# middle of its plinth; the plinths are built again round it with the new turn. Out of the hall FBX into their own
# (own_fbx, export_unity_v15.py leaves them out): Unity hides the hall model's HallV15_Stair_statue_* nodes.
# Run: blender -b grand_stair_hall_v34C.blend -P stair_statues_v36.py
#   -> grand_stair_hall_v36C.blend, renders_v43/*.png, Assets/Resources/PawnRushGrandStairHall/StairStatuesV36.fbx
import bpy, bmesh, math, os
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
TAG = "StairStatuesV36"
FLOOR, SIDE_X, OLD_TURN, TURN = -4.0, 15.0, 30.0, 20.0
sc = bpy.context.scene
PLACES = [("Queen", -1, 22.0), ("King", 1, 22.0), ("Bishop", -1, 44.0), ("Knight", 1, 44.0)]


def stair_z(y):
    if y <= 30:
        return (y - 14) / 16 * 4
    if y <= 36:
        return 4.0
    return 4 + (y - 36) / 16 * 4


def material(name, rgba, metal=0., rough=.5):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = rgba
        m.metallic, m.roughness = metal, rough
    return m


WHITE = material("Statue_Marble", (.95, .94, .92, 1), 0, .25)
GREEN = material("Plinth_Marble", (.78, .66, .48, 1), 0, .45)       # the preview colour (Unity: PR_Travertine)
GOLD = bpy.data.materials.get("Palace_Gold") or material("Palace_Gold", (.86, .64, .28, 1), .9, .3)
MATS = [WHITE, GREEN, GOLD]
FIG, PED, AU = range(3)

coll = bpy.data.collections["Stair_statues_v25"]


def paint(ob):
    """Figure / pedestal / gold per face; returns the deck height (mesh frame) and the pedestal's footprint centre."""
    me = ob.data
    n_v, F = len(me.vertices), len(me.polygons)
    Pv = np.empty(n_v * 3); me.vertices.foreach_get("co", Pv); Pv = Pv.reshape(-1, 3)
    C = np.empty(F * 3); me.polygons.foreach_get("center", C); C = C.reshape(-1, 3)
    Nf = np.empty(F * 3); me.polygons.foreach_get("normal", Nf); Nf = Nf.reshape(-1, 3)
    A = np.empty(F); me.polygons.foreach_get("area", A)
    # The deck: the most area looking up, in 2 cm bands between 0.25 and 3 m.
    up = (Nf[:, 2] > .92) & (C[:, 2] > .25) & (C[:, 2] < 3.)
    bins = np.arange(.25, 3.0, .02)
    hist, _ = np.histogram(C[up, 2], bins=bins, weights=A[up])
    deck = bins[int(hist.argmax())] + .02
    foot = Pv[Pv[:, 2] < .3]
    fc = foot[:, :2].mean(0)
    z = C[:, 2]
    ped = z < deck + .015
    ledge = ped & (Nf[:, 2] > .93) & (z > .06) & (z < deck - .08)
    band = ped & (z > deck - .09) & (z < deck - .005) & (np.abs(Nf[:, 2]) < .35)   # the deck's edge, all round
    foot_band = ped & (z < .09) & (np.abs(Nf[:, 2]) < .35)
    idx = np.where(ped, PED, FIG)
    idx = np.where(ledge | band | foot_band, AU, idx)
    me.materials.clear()
    for m in MATS:
        me.materials.append(m)
    me.polygons.foreach_set("material_index", idx.astype(np.int32))
    me.update()
    print("PAINT", ob.name, "deck %.2f" % deck, "faces fig %d ped %d gold %d (ledge %d band %d foot %d)" % (
        (idx == FIG).sum(), (idx == PED).sum(), (idx == AU).sum(), ledge.sum(), band.sum(), foot_band.sum()))
    return deck, Vector((fc[0], fc[1], 0))


class Part:
    def __init__(self, name):
        self.name, self.bm, self.mats = name, bmesh.new(), []

    def add(self, bm, mat):
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        if mat not in self.mats:
            self.mats.append(mat)
        for f in bm.faces:
            f.material_index = self.mats.index(mat)
        me = bpy.data.meshes.new("tmp"); bm.to_mesh(me); bm.free()
        self.bm.from_mesh(me); bpy.data.meshes.remove(me)

    def done(self):
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:])
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me); self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(30))
        ob = bpy.data.objects.new(self.name, me)
        coll.objects.link(ob)
        ob["own_fbx"] = TAG
        return ob


def octa(c, r, z0, z1, yaw):
    """An octagonal prism, its flats r from c."""
    bm = bmesh.new()
    rr = r / math.cos(math.pi / 8)
    lo = [bm.verts.new((c.x + rr * math.cos(yaw + math.pi / 8 + k * math.pi / 4), c.y + rr * math.sin(yaw + math.pi / 8 + k * math.pi / 4), z0)) for k in range(8)]
    hi = [bm.verts.new((v.co.x, v.co.y, z1)) for v in lo]
    bm.faces.new(lo[::-1]); bm.faces.new(hi)
    for k in range(8):
        bm.faces.new((lo[k], lo[(k + 1) % 8], hi[(k + 1) % 8], hi[k]))
    return bm


def slab(centre, U, V, N, u0, u1, v0, v1, d0, d1):
    bm = bmesh.new()
    pts = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
    a = [bm.verts.new(centre + U * u + V * v + N * d0) for u, v in pts]
    b = [bm.verts.new(centre + U * u + V * v + N * d1) for u, v in pts]
    bm.faces.new(a); bm.faces.new(b[::-1])
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((a[i], a[j], b[j], b[i]))
    return bm


def lathe(prof, centre, axis, segs=24):
    axis = Vector(axis).normalized()
    e1 = Vector((0, 0, 1)) if abs(axis.z) < .9 else Vector((1, 0, 0))
    e1 = (e1 - axis * e1.dot(axis)).normalized(); e2 = axis.cross(e1)
    bm = bmesh.new()
    rings = [[bm.verts.new(centre + axis * h + (e1 * math.cos(t) + e2 * math.sin(t)) * r) for r, h in prof]
             for t in [2 * math.pi * i / segs for i in range(segs)]]
    for i in range(segs):
        r0, r1 = rings[i], rings[(i + 1) % segs]
        for j in range(len(prof) - 1):
            bm.faces.new((r0[j], r0[j + 1], r1[j + 1], r1[j]))
    return bm


def plinth(name, c, r, top, yaw):
    """The octagonal plinth from the hall floor to `top` in green marble with gold: a stepped base, a gold fillet, the
    shaft with a gold frame and rosette on each face, a gold band, a moulded cap with a gold fillet under the pedestal."""
    p = Part(name)
    tiers = [(FLOOR, FLOOR + .35, r + .45, GREEN), (FLOOR + .35, FLOOR + .42, r + .36, GOLD),
             (FLOOR + .42, FLOOR + .62, r + .3, GREEN), (FLOOR + .62, top - .62, r, GREEN),
             (top - .62, top - .55, r + .06, GOLD), (top - .55, top - .35, r + .18, GREEN),
             (top - .35, top - .29, r + .26, GOLD), (top - .29, top, r + .32, GREEN)]
    for z0, z1, rr, m in tiers:
        p.add(octa(c, rr, z0, z1, yaw), m)
    side = 2 * r * math.tan(math.pi / 8)
    z0, z1 = FLOOR + .62, top - .62
    for k in range(8):
        a = yaw + k * math.pi / 4                       # the flat's normal (the octagon's verts are at +pi/8)
        n = Vector((math.cos(a), math.sin(a), 0))
        u = Vector((-math.sin(a), math.cos(a), 0))
        face = c + n * r
        mu, mv, w = .32, .45, .06
        u0, u1, v0, v1 = -side / 2 + mu, side / 2 - mu, z0 + mv, z1 - mv
        if u1 - u0 < .6 or v1 - v0 < .8:
            continue
        for (a0, a1, b0, b1) in ((u0, u1, v0, v0 + w), (u0, u1, v1 - w, v1), (u0, u0 + w, v0, v1), (u1 - w, u1, v0, v1)):
            p.add(slab(Vector((face.x, face.y, 0)), u, Vector((0, 0, 1)), n, a0, a1, b0, b1, 0, .025), GOLD)
        rz = (v0 + v1) / 2 + (v1 - v0) * .12
        p.add(lathe([(0, 0), (.24, 0), (.24, .015), (.2, .03), (.16, .022), (.12, .04), (.08, .035), (.05, .065), (0, .075)],
                    Vector((face.x, face.y, rz)), n, 24), GOLD)
    return p.done()


# ------------------------------------------------------------------ paint, turn, new plinths
for name, side, y in PLACES:
    ob = bpy.data.objects["Stair_statue_" + name]
    deck, fc = paint(ob)
    top = stair_z(y)
    target = Vector((side * SIDE_X, y, top))
    delta = math.radians(TURN - OLD_TURN) * (-1 if side < 0 else 1)    # west turned -30 -> -20, east +30 -> +20
    ob.matrix_world = Matrix.Translation(target) @ Matrix.Rotation(delta, 4, "Z") @ Matrix.Translation(-target) @ ob.matrix_world
    ob["own_fbx"] = TAG
    for p in ob.data.polygons:
        p.use_smooth = True
    old = bpy.data.objects.get("Stair_statue_%s plinth" % name)
    foot = [v.co for v in ob.data.vertices if v.co.z < .3]
    rad = max(math.hypot(v.x - fc.x, v.y - fc.y) for v in foot)
    yaw = math.radians(-TURN if side < 0 else TURN)
    if old is not None:
        bpy.data.objects.remove(old)
    pl = plinth("Stair_statue_%s plinth" % name, target, rad * .92, top, yaw)
    print("STATUE", name, "deck %.2f" % deck, "pedestal r %.2f" % rad, "turn %+.0f" % math.degrees(yaw), "plinth", len(pl.data.polygons), "faces")

# ------------------------------------------------------------------ renders (as 승규's shot up the stair, and closer)
sc.render.engine = "BLENDER_WORKBENCH"
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
sh = sc.display.shading
sh.light, sh.color_type = "STUDIO", "MATERIAL"
sh.show_cavity, sh.show_shadows = True, True
for o in bpy.data.collections["Royal_balcony_v34 previews"].objects:
    o.hide_render = True
out = os.path.join(HERE, "renders_v43"); os.makedirs(out, exist_ok=True)
for name, loc, at, lens in (("stair_up", (0, 16, 13.5), (0, 46, 5.5), 22), ("knight", (5, 34, 11), (15, 46, 9), 26),
                            ("bishop", (-4, 34, 11), (-15, 46, 9), 26), ("lower_pair", (0, 6, 7), (0, 24, 4), 22),
                            ("queen", (-6, 14, 6), (-15, 23, 4), 26)):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_end = 2000
    o = bpy.data.objects.new("Cam36_" + name, cd); sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = o
    sc.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera

# ------------------------------------------------------------------ export (world frame, as the hall's)
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
dg = bpy.context.evaluated_depsgraph_get()
outs = []
for o in [o for o in coll.objects if o.get("own_fbx") == TAG]:
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), depsgraph=dg)
    me.transform(o.matrix_world)
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    e = bpy.data.objects.new(o.name.replace("Stair_statue_", "StairStatue_").replace(" ", "_"), me)
    sc.collection.objects.link(e)
    outs.append(e)
for o in sc.objects:
    o.select_set(False)
for o in outs:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "StairStatuesV36.fbx"), **FBX)
print("EXPORT", [(o.name, len(o.data.polygons)) for o in outs])
for o in outs:
    bpy.data.objects.remove(o)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v36C.blend"))
