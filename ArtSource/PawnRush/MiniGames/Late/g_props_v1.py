# G Royal Procession props (R504): cut the user's row-of-objects FBX models into single objects, turn them to face the
# players, size them to the game, decimate, tone the colours to the phase-3 rules and export one FBX per object.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python g_props_v1.py -- [--export] [--render]
#
# Sources (Source/README.md): R504_G_A1_Pieces / A2_Decor / A3_Modules (500k triangles each) and A4_TrumpetStand (50k),
# made by the user from the asset photos in Docs/PawnRush/ConceptArt/R504_RoyalProcession/Assets. Each is one mesh with
# one 4096 colour + 2048 normal atlas; every object cut from a source keeps that source's atlas (one material per
# source: PR_Late_G_Pieces / _Decor / _Modules / _Fixes).
#
# Objects are found by grouping loose parts along the row axis (they stand apart in the photos). Origin = bottom
# centre, Blender -y = front (towards the players), sizes from the game (MgKingEscort: sled 1.6 m, blocking rook
# 1.6 m high, rook tower 2.2 m, king 1.7 m; MiniGamePalaceArt.Games: rook parking bay 3.2 x 2.8 m). Décor sizes are
# a first guess and can be scaled uniformly where they are placed.
# Colour (PAWN_RUSH_ART_PHASES phase 3): bright gold stays only where it is functional (palanquin base band, rook
# collars, garage stopper); decorative gold becomes matte brass. The faded pink velvet of A2/A4 becomes purple.
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT = os.path.join(HERE, "Export", "G")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BRASS = np.array([.30, .23, .12], np.float32)

# source key: file, row axis, turn about z (deg) so the photo's front faces -y, objects in row order:
#   (cluster index, name, size rule, size, triangles, decorative-gold-to-brass rule)
#   size rules: "h" height, "base" widest footprint in the bottom 8 % of the height, "w" width along x after the turn
#   gold rules: None (keep), "all" (whole object), ("above", f) (faces above f x height)
SOURCES = {
    "Pieces": ("R504_G_A1_Pieces.fbx", 0, 0., [
        (0, "G_PalanquinKing", "base", 1.6, 12000, ("above", .14)),
        (1, "G_Palanquin", "base", 1.6, 12000, ("above", .14)),
        (2, "G_RookEbony", "h", 1.6, 5000, None),
        (4, "G_CastlingRook", "h", 2.2, 6000, None),
        (5, "G_King", "h", 1.7, 3000, None)]),
    "Decor": ("R504_G_A2_Decor.fbx", 0, 180., [
        (0, "G_Balustrade", "h", 1.1, 2000, "all"),
        (1, "G_CrownPedestal", "h", 1.6, 3000, "all"),
        (3, "G_StanchionPair", "h", 1.0, 2500, "all")]),
    "Fixes": ("R504_G_A4_TrumpetStand.fbx", 0, 0., [
        (0, "G_RookIvory", "h", 1.6, 5000, None),
        (1, "G_TrumpetStand", "h", 1.3, 3000, None)]),
        # cluster 2, the curtain, is left out: hanging cloth that should sway is built in Unity code (user, relayed by
        # the B session 11-07: "천도 마찬가지로 흐느적 거리는게 없잖아 그것도 안돼 코드로 해야 해")
    "Modules": ("R504_G_A3_Modules.fbx", 1, -90., [
        (2, "G_DaisEdge", "h", .6, 1000, "all"),
        ("3a", "G_GarageAlcove", "w", 3.6, 5000, None),
        ("3b", "G_WallWindow", "h", 6.0, 1500, "all")]),
    # the wall system (asset photo G_W1_walls.png, user's Tripo "Classical Marble Elements", 80k): 12 m high; the niche
    # bay came out open like a window and the inside corner broke, so they are left out (corners get a column)
    "Walls": ("R504_G_W1_Walls.fbx", 0, 0., [
        (0, "G_WallBayWindow", "h", 12., 6000, "all"),
        (1, "G_WallBayPlain", "h", 12., 5000, "all"),
        (2, "G_WallColumn", "h", 12., 6000, "all")]),
}
VELVET_FIX = {"Decor", "Fixes", "Stairs"}


# ------------------------------------------------------------------ loading and grouping
def load(fname):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, fname))
    ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    return ob


def labels(me, axis):
    """Per-vertex object label: loose parts merged while their intervals along the row axis overlap."""
    n = len(me.vertices)
    co = np.empty(n * 3, np.float32); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    ed = np.empty(len(me.edges) * 2, np.int64); me.edges.foreach_get("vertices", ed); ed = ed.reshape(-1, 2)
    parent = np.arange(n)

    def find(i):
        root = i
        while parent[root] != root: root = parent[root]
        while parent[i] != root: parent[i], i = root, parent[i]
        return root
    for u, v in ed:
        ru, rv = find(u), find(v)
        if ru != rv: parent[ru] = rv
    roots = np.array([find(i) for i in range(n)])
    uniq, inv = np.unique(roots, return_inverse=True)
    lo = np.full(len(uniq), 9.); hi = np.full(len(uniq), -9.)
    np.minimum.at(lo, inv, co[:, axis]); np.maximum.at(hi, inv, co[:, axis])
    order = np.argsort(lo)
    span = co[:, axis].max() - co[:, axis].min()
    group = np.empty(len(uniq), int); g, end = -1, -9.
    for k in order:
        if lo[k] > end + span * .004: g += 1; end = hi[k]
        else: end = max(end, hi[k])
        group[k] = g
    return co, group[inv], inv, lo, hi, group


def split_two(co, lab, g, axis, part_of_vertex, part_lo, part_hi):
    """Split group g into two objects at the emptiest slice of its middle (garage | wall touch in A3)."""
    m = lab == g
    x = co[m, axis]
    a, b = x.min(), x.max()
    hist, edges = np.histogram(x, 200, (a, b))
    mid = slice(60, 140)
    k = 60 + int(np.argmin(hist[mid]))
    cut = (edges[k] + edges[k + 1]) / 2
    centre = (part_lo + part_hi) / 2
    side = centre[part_of_vertex] < cut
    out = lab.astype(object)
    out[m & side] = "%sa" % g
    out[m & ~side] = "%sb" % g
    print("SPLIT group %s at %.3f" % (g, cut))
    return out


def cut(src, keep_mask, name):
    """New object holding only the faces whose vertices are all in keep_mask."""
    bm = bmesh.new(); bm.from_mesh(src.data)
    bm.verts.ensure_lookup_table()
    gone = [v for v in bm.verts if not keep_mask[v.index]]
    bmesh.ops.delete(bm, geom=gone, context='VERTS')
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(src.data.materials[0])
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def place(ob, turn, rule, size):
    me = ob.data
    me.transform(Matrix.Rotation(math.radians(turn), 4, 'Z'))
    co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    lo, hi = co.min(0), co.max(0)
    h = hi[2] - lo[2]
    if rule == "h": k = size / h
    elif rule == "w": k = size / (hi[0] - lo[0])
    else:
        foot = co[co[:, 2] < lo[2] + .08 * h]
        k = size / max(foot[:, 0].max() - foot[:, 0].min(), foot[:, 1].max() - foot[:, 1].min())
    c = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]))
    me.transform(Matrix.Scale(k, 4) @ Matrix.Translation(-c))
    me.update()
    return k


def trim_stopper(ob):
    """The photo's gold bar lies across the bottom of the alcove's mouth, where the rook drives in. The game has its own
    functional gold stop at the back of the garage, so the bar is cut away: faces in the low, front, middle box."""
    me = ob.data
    co = np.array([v.co[:] for v in me.vertices])
    lo, hi = co.min(0), co.max(0)
    W, D, H = hi - lo
    # only faces painted gold (the atlas colour under the face's UV centre), so the stone jambs beside the bar stay
    img = next(n.image for n in me.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE' and n.image.name.startswith("base"))
    small = img.copy(); small.scale(1024, 1024)
    px = np.array(small.pixels[:], np.float32).reshape(1024, 1024, 4)[..., :3]
    bpy.data.images.remove(small)
    hue, sat, val = hsv(px)
    gold = (np.abs(hue - 42) < 16) & (sat > .4) & (val > .35)
    bm = bmesh.new(); bm.from_mesh(me)
    uvl = bm.loops.layers.uv.active
    gone = []
    for f in bm.faces:
        c = f.calc_center_median()
        if abs(c.x) < .34 * W and c.z < .16 * H and c.y < lo[1] + .5 * D:
            u = sum(l[uvl].uv.x for l in f.loops) / len(f.loops); v = sum(l[uvl].uv.y for l in f.loops) / len(f.loops)
            if gold[min(1023, max(0, int(v * 1024))), min(1023, max(0, int(u * 1024)))]:
                gone.append(f)
    # the bar's shaded underside is not gold in the atlas: take every face inside the gold faces' box (a little grown)
    if gone:
        cs = np.array([f.calc_center_median()[:] for f in gone])
        blo, bhi = cs.min(0) - [.0, .03 * D, .02 * H], cs.max(0) + [.0, .03 * D, .02 * H]
        blo[0], bhi[0] = max(blo[0], -.23 * W), min(bhi[0], .23 * W)      # stay inside the jambs
        gone = [f for f in bm.faces if np.all((np.array(f.calc_center_median()[:]) >= blo) & (np.array(f.calc_center_median()[:]) <= bhi))]
    bmesh.ops.delete(bm, geom=gone, context='FACES')
    bm.to_mesh(me); bm.free(); me.update()
    print("TRIM alcove stopper: %d faces" % len(gone))


def decimate(ob, target):
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.scene.objects: o.select_set(o == ob)
    mod = ob.modifiers.new("weld", 'WELD'); mod.merge_threshold = .002
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if tris(ob) > target:
        mod = ob.modifiers.new("dec", 'DECIMATE'); mod.ratio = target / tris(ob); mod.use_collapse_triangulate = True
        with bpy.context.temp_override(object=ob, active_object=ob):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    smooth_by_angle(ob.data)


def smooth_by_angle(me, degrees=35.):
    """Tripo meshes come flat-shaded: every small face catches the light on its own and flat panels look crumpled.
    Smooth shading with sharp edges only above `degrees` keeps mouldings crisp and flat faces flat."""
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.set_sharp_from_angle(angle=math.radians(degrees))
    me.update()


# ------------------------------------------------------------------ colour
def hsv(rgb):
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = np.maximum(mx - mn, 1e-5)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return hue, d / np.maximum(mx, 1e-5), mx


def from_hsv(h, s, v):
    c = v * s; hp = (h % 360) / 60; x = c * (1 - np.abs(hp % 2 - 1)); m = v - c
    z = np.zeros_like(h)
    conds = [(hp < 1), (hp < 2), (hp < 3), (hp < 4), (hp < 5), (hp >= 5)]
    rs = [c, x, z, z, x, c]; gs = [x, c, c, x, z, z]; bs = [z, z, x, c, c, x]
    r = np.select(conds, rs); g = np.select(conds, gs); b = np.select(conds, bs)
    return np.stack([r + m, g + m, b + m], -1)


def raster_mask(obs_faces, size):
    """Texels covered by the UV triangles of the given (object, face filter) list, dilated 2 px."""
    mask = np.zeros((size, size), bool)
    for ob, keep in obs_faces:
        me = ob.data
        uv = me.uv_layers.active.data
        me.calc_loop_triangles()
        zs = np.array([v.co.z for v in me.vertices]); H = zs.max() - zs.min()
        for t in me.loop_triangles:
            if keep is not None and np.mean([me.vertices[i].co.z for i in t.vertices]) < keep * H: continue
            p = np.array([uv[l].uv[:] for l in t.loops]) * size
            x0, y0 = np.floor(p.min(0)).astype(int); x1, y1 = np.ceil(p.max(0)).astype(int)
            x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, size - 1), min(y1, size - 1)
            if x1 < x0 or y1 < y0: continue
            xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
            (ax, ay), (bx, by), (cx, cy) = p
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-9:
                mask[y0:y1 + 1, x0:x1 + 1] = True; continue
            l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den
            l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
            inside = (l1 >= -.05) & (l2 >= -.05) & (1 - l1 - l2 >= -.05)
            mask[y0:y1 + 1, x0:x1 + 1] |= inside
    for _ in range(2):
        mask = mask | np.roll(mask, 1, 0) | np.roll(mask, -1, 0) | np.roll(mask, 1, 1) | np.roll(mask, -1, 1)
    return mask


def retexture(key, mat, brass_faces):
    nodes = mat.node_tree.nodes
    imgs = {n.image.name: n for n in nodes if n.type == 'TEX_IMAGE' and n.image}
    base, normal = imgs["base_color"].image, imgs["normal"].image
    w, h = base.size
    px = np.array(base.pixels[:], np.float32).reshape(h, w, 4)
    rgb = px[..., :3]
    hue, sat, val = hsv(rgb)
    lum = (rgb * np.array([.2126, .7152, .0722], np.float32)).sum(-1, keepdims=True)
    if brass_faces:
        gold = np.clip((sat - .45) / .15, 0, 1) * np.clip((val - .22) / .12, 0, 1) * np.clip(1 - np.abs(hue - 40) / 16, 0, 1)
        m = raster_mask(brass_faces, 1024)
        m = np.repeat(np.repeat(m, h // 1024, 0), w // 1024, 1).astype(np.float32)
        gold = (gold * m)[..., None]
        rgb = rgb * (1 - gold) + np.clip(BRASS * (lum / .35) ** .8, 0, 1) * gold
        print("GOLD %s: %.2f%% of texels to matte brass" % (key, 100 * float((gold > .5).mean())))
    if key in ("Walls", "Gate", "Stairs"):
        # the same cream marble as the centre gate (#DDD7C9, g_center_gate_v1): the Tripo beige stone, luminance kept
        hue, sat, val = hsv(rgb)
        gold_left = (np.clip((sat - .45) / .15, 0, 1) * np.clip(1 - np.abs(hue - 40) / 16, 0, 1))[..., None]
        warm = (np.clip(1 - np.abs(hue - 30) / 25, 0, 1) * np.clip((sat - .05) / .1, 0, 1))[..., None] * (1 - gold_left)
        ref = float(np.median(lum[warm[..., 0] > .5])) if (warm > .5).any() else .6
        cream = np.array([.867, .843, .788], np.float32)
        rgb = rgb * (1 - warm) + np.clip(cream * (lum / ref) ** .85, 0, 1) * warm
        print("CREAM %s: %.1f%% of texels to cream marble" % (key, 100 * float((warm > .5).mean())))
    if key in VELVET_FIX:
        hue, sat, val = hsv(rgb)
        pink = (np.clip(1 - np.abs(((hue - 300 + 180) % 360) - 180) / 45, 0, 1) * np.clip((sat - .12) / .1, 0, 1))[..., None]
        purple = from_hsv(np.full_like(hue, 274.), np.clip(sat * 1.25, .35, .7), val * .82)
        rgb = rgb * (1 - pink) + purple * pink
        print("VELVET %s: %.2f%% of texels to purple" % (key, 100 * float((pink > .5).mean())))
    px[..., :3] = rgb
    name = "PR_Late_G_" + key
    out = bpy.data.images.new(name + "_BaseColor", w, h)
    out.pixels.foreach_set(px.ravel()); out.scale(2048, 2048)
    out.filepath_raw = os.path.join(TEX, name + "_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nrm = normal.copy(); nrm.name = name + "_Normal"; nrm.colorspace_settings.name = 'Non-Color'
    nrm.filepath_raw = os.path.join(TEX, name + "_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    imgs["base_color"].image = out; imgs["normal"].image = nrm
    mat.name = name
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Roughness"].default_value = .55; bsdf.inputs["Metallic"].default_value = 0.


# ------------------------------------------------------------------ main
def export(ob):
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.context.scene.objects: o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)


def build(key):
    fname, axis, turn, picks = SOURCES[key]
    src = load(fname)
    co, lab, part_of_vertex, part_lo, part_hi, _ = labels(src.data, axis)
    if any(isinstance(p[0], str) for p in picks):
        g = int(next(p[0] for p in picks if isinstance(p[0], str))[:-1])
        lab = split_two(co, lab, g, axis, part_of_vertex, part_lo, part_hi)
    print("SOURCE %s: %d objects" % (key, len(set(lab.tolist()))))
    made, brass = [], []
    for idx, name, rule, size, target, gold in picks:
        keep = np.array([l == idx or str(l) == str(idx) for l in lab])
        ob = cut(src, keep, name)
        k = place(ob, turn, rule, size)
        if name == "G_GarageAlcove": trim_stopper(ob)
        decimate(ob, target)
        lo = [min(v.co[i] for v in ob.data.vertices) for i in range(3)]
        hi = [max(v.co[i] for v in ob.data.vertices) for i in range(3)]
        print("OBJ %-16s scale %6.2f  tris %6d  size %s" % (name, k, tris(ob), [round(b - a, 2) for a, b in zip(lo, hi)]))
        if gold == "all": brass.append((ob, None))
        elif gold: brass.append((ob, gold[1]))
        made.append(ob)
    bpy.data.objects.remove(src)
    retexture(key, made[0].data.materials[0], brass)
    if "--export" in ARGS:
        for ob in made: export(ob)
    return made


def contact_sheet(all_objs):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    x = 0.
    for name in all_objs:
        bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"))
        ob = bpy.context.selected_objects[0]
        # the FBX carries no texture paths (path_mode STRIP, Unity assigns its own .mat); rewire them for the preview
        mname = ob.data.materials[0].name.split(".")[0]
        if mname not in bpy.data.materials or not bpy.data.materials[mname].get("wired"):
            m = bpy.data.materials.new(mname); m["wired"] = True; m.use_nodes = True
            nt = m.node_tree; bsdf = nt.nodes["Principled BSDF"]; bsdf.inputs["Roughness"].default_value = .55
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(TEX, mname + "_BaseColor.png"))
            nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
            tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(os.path.join(TEX, mname + "_Normal.png"))
            tn.image.colorspace_settings.name = 'Non-Color'
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
        ob.data.materials[0] = next(m for m in bpy.data.materials if m.get("wired") and m.name.split(".")[0] == mname)
        bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
        w = max(v.x for v in bb) - min(v.x for v in bb)
        ob.location.x += x + w / 2 - min(v.x for v in bb)
        x += w + .8
    # 1.7 m pawn for scale
    bpy.ops.mesh.primitive_cylinder_add(radius=.3, depth=1.2, location=(x + .3, 0, .6))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.3, location=(x + .3, 0, 1.45))
    bpy.ops.mesh.primitive_plane_add(size=200, location=(x / 2, 0, 0))
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.8, .8, .85, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .7
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(40), 0, math.radians(-25))); bpy.context.object.data.energy = 3
    cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = x + 2.5
    cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = (x / 2 + .4, -60, 4.6 + 60 * math.tan(math.radians(4))); cam.rotation_euler = (math.radians(86), 0, 0)
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 2400, 800
    sc.view_settings.view_transform = 'AgX'
    sc.render.filepath = os.path.join(HERE, "render_g_props.png")
    bpy.ops.render.render(write_still=True)


def main():
    names = []
    if "--sheet-only" not in ARGS:
        for key in SOURCES:
            made = build(key)
            names += [o.name for o in made]
    else:
        names = [p[1] for _, _, _, picks in SOURCES.values() for p in picks]
    if "--render" in ARGS and ("--export" in ARGS or "--sheet-only" in ARGS):
        contact_sheet(names)


if __name__ == "__main__":
    main()
