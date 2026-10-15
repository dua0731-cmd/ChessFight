# Usage (Blender 5.2, from the repository root; each GLB is named <Piece>.glb, any case):
#   blender -b -P Tools/PieceRig/rig_piece.py -- Assets/Art/Pawn/Pawn.fbx <folder with the GLBs> <out folder> [King Queen ...]
# Then copy <out>/<Piece>_Rigged.fbx to Assets/Art/Pieces/<Piece>/ and run
# ChessFight/Ragdoll Lab/Bake Piece Models in Unity (PieceModelBaker). A new piece also needs
# <Piece>_Albedo.png (the GLB's texture) and <Piece>.mat (Standard, smoothness 0.35) in that folder.
#
# Rig a chess-piece mesh on the pawn's own Mixamo skeleton (same bones, same rest pose), part by part.
# The Tripo models are built from separate parts (islands once the UV seams are welded): body, crown,
# eyes, two arm stubs, two cuffs/mittens, two feet. Each part goes to one place, like the pawn's own
# rig (RagdollLabBuilder weights its components the same way):
#   feet       - the whole island on its Foot (so the base above them never goes with a foot)
#   arm stub   - the whole island on its Arm
#   mitten     - the whole island (cuff included) on its Hand; a stub and mitten made as one island
#                are split where the dark mitten starts along the arm
#   the rest   - body by height along Hips > Spine > Spine1 > Spine2 > Head
# Then what the model lacks underneath: a lid inside a hollow base (the knight's), and a short leg from
# inside each foot up into the base, the foot at its bottom and the hips at its top, which stretches out
# when a foot moves away the way the pawn's skin stretches into its legs.
import bpy, sys, os, json, math
from mathutils import Vector

pawn, glbdir, outdir = sys.argv[sys.argv.index("--") + 1:][:3]
names = sys.argv[sys.argv.index("--") + 4:] or ["King", "Queen", "Rook", "Knight", "Bishop"]
DRIVEN = ["Hips", "Spine", "Spine1", "Spine2", "Head", "LeftArm", "LeftHand", "RightArm", "RightHand", "LeftFoot", "RightFoot"]
P = "mixamorig:"
DARK = 0.35  # mitten and shoes are dark grey (median luminance 0.1-0.2), the wood is 0.6-0.9

def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

report = {}
for name in names:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=pawn)
    arm = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"][0]
    for o in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(glbdir, name + ".glb"))
    mesh = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
    mesh.name = name
    bpy.context.view_layer.objects.active = mesh
    for o in bpy.context.selected_objects: o.select_set(False)
    mesh.select_set(True)
    if mesh.parent: mesh.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # Split at every UV seam in the GLB: weld, or two vertices on one spot could follow different bones.
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0005); bpy.ops.object.mode_set(mode="OBJECT")
    me = mesh.data

    W = arm.matrix_world
    bone = {n: W @ arm.data.bones[P + n].head_local for n in DRIVEN}

    img = [n.image for n in mesh.active_material.node_tree.nodes if n.type == "TEX_IMAGE" and n.image][0]
    w, h = img.size; px = img.pixels[:]
    uv = me.uv_layers.active.data
    acc = [0.0] * len(me.vertices); cnt = [0] * len(me.vertices)
    for loop in me.loops:
        u, v = uv[loop.index].uv
        x = min(w - 1, max(0, int((u % 1.0) * w))); y = min(h - 1, max(0, int((v % 1.0) * h)))
        i = (y * w + x) * 4
        acc[loop.vertex_index] += 0.2126 * px[i] + 0.7152 * px[i + 1] + 0.0722 * px[i + 2]; cnt[loop.vertex_index] += 1
    lum = [acc[i] / cnt[i] if cnt[i] else 1.0 for i in range(len(acc))]
    verts = [v.co.copy() for v in me.vertices]

    # islands
    par = list(range(len(verts)))
    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]; a = par[a]
        return a
    for e in me.edges:
        a, b = find(e.vertices[0]), find(e.vertices[1])
        if a != b: par[a] = b
    islands = {}
    for i in range(len(verts)): islands.setdefault(find(i), []).append(i)

    label = ["Body"] * len(verts)
    zArm = bone["LeftHand"].z
    parts = []
    for ids in islands.values():
        xs = [verts[i].x for i in ids]; ys = [verts[i].y for i in ids]; zs = [verts[i].z for i in ids]
        one_side = min(xs) * max(xs) > 0
        side = "Left" if sum(xs) > 0 else "Right"
        cy = sum(ys) / len(ys); cz = sum(zs) / len(zs)
        reach = max(abs(x) for x in xs)
        dark = sum(1 for i in ids if lum[i] < DARK) / len(ids)
        if one_side and min(zs) < 0.03 and max(zs) < 0.3:
            kind = "Foot"
            for i in ids: label[i] = side + "Foot"
        elif one_side and abs(cy) < 0.06 and abs(cz - zArm) < 0.2 and reach > 0.1:
            x0 = min(abs(x) for x in xs); x1 = reach
            if dark < 0.15:
                kind = "Arm"
                for i in ids: label[i] = side + "Arm"
            else:
                # one island holding both the stub and the mitten: the mitten starts at the first
                # 1 cm slice (from the body out) from which every slice is dark
                kind = "Hand"
                wrist = x0
                if x1 - x0 > 0.08:
                    bins = []
                    b = x0
                    while b < x1:
                        sel = sorted(lum[i] for i in ids if b <= abs(verts[i].x) < b + 0.01)
                        bins.append((b, sel[len(sel) // 2] if sel else None))
                        b += 0.01
                    for k in range(len(bins)):
                        if all(m is None or m < DARK for _, m in bins[k:]):
                            wrist = bins[k][0]; break
                    if wrist > x0 + 0.02: kind = "Arm+Hand"
                    else: wrist = x0  # only the cuff's light rim before the dark: all of it is the hand
                for i in ids: label[i] = side + ("Hand" if abs(verts[i].x) >= wrist else "Arm")
        else:
            kind = "Body"
        parts.append((kind, side, len(ids), round(min(xs), 3), round(max(xs), 3), round(min(zs), 3), round(max(zs), 3), round(dark, 2)))

    groups = {n: mesh.vertex_groups.new(name=P + n) for n in DRIVEN}
    chain = ["Hips", "Spine", "Spine1", "Spine2", "Head"]
    zs = [bone["Hips"].z, bone["Spine"].z, bone["Spine1"].z, bone["Spine2"].z, bone["Head"].z]
    # The rook has no neck: its face sits on the tower, so the whole tower above the waist turns with the head.
    if name == "Rook": zs = [bone["Hips"].z, 0.30, 0.34, 0.38, 0.42]
    W8 = []
    for i, p in enumerate(verts):
        if label[i] != "Body":
            W8.append({label[i]: 1.0}); continue
        z = p.z
        if z <= zs[0]: W8.append({"Hips": 1.0}); continue
        if z >= zs[-1]: W8.append({"Head": 1.0}); continue
        for k in range(len(zs) - 1):
            if z <= zs[k + 1]:
                t = smooth((z - zs[k]) / (zs[k + 1] - zs[k]))
                W8.append({chain[k]: 1 - t, chain[k + 1]: t}); break
    # soften where a stub turns into its mitten inside one island (edges stay inside an island)
    nbr = [[] for _ in verts]
    for e in me.edges:
        a, b = e.vertices; nbr[a].append(b); nbr[b].append(a)
    for _ in range(2):
        new = []
        for i, ns in enumerate(nbr):
            if not ns or label[i] == "Body" or all(label[j] == label[i] for j in ns):
                new.append(W8[i]); continue
            acc = dict((k, v * 0.5) for k, v in W8[i].items())
            for j in ns:
                for k, v in W8[j].items(): acc[k] = acc.get(k, 0) + 0.5 * v / len(ns)
            new.append(acc)
        W8 = new
    for i, wt in enumerate(W8):
        top = sorted(wt.items(), key=lambda kv: -kv[1])[:4]
        tot = sum(v for _, v in top)
        for k, v in top:
            if v / tot > 0.01: groups[k].add([i], v / tot, "REPLACE")
    count = {}
    for l in label: count[l] = count.get(l, 0) + 1
    print("PARTS", name, json.dumps(sorted(parts, key=lambda p: (p[0], p[1]))))
    print("LABELS", name, json.dumps(count, sort_keys=True))

    # ---- What the model lacks underneath, that the pawn has: a closed base and legs.
    uv_of = {}
    for loop in me.loops: uv_of.setdefault(loop.vertex_index, tuple(uv[loop.index].uv))
    island_of = [find(i) for i in range(len(verts))]
    spanning = [ids for ids in islands.values() if min(verts[i].x for i in ids) < -0.05
                and max(verts[i].x for i in ids) > 0.05 and min(verts[i].z for i in ids) < 0.3]
    base = min(spanning, key=lambda ids: min(verts[i].z for i in ids))
    zbase = min(verts[i].z for i in base)
    bottom = [i for i in base if verts[i].z < zbase + 0.012]
    def base_uv(p):
        return uv_of[min(bottom, key=lambda i: (verts[i] - p).length)]
    extra = []  # (vertices, faces, uv, weights)
    # A hollow base (the knight's is a cup, open underneath): a lid just inside its rim.
    inward = 0
    for poly in me.polygons:
        if island_of[poly.vertices[0]] != island_of[base[0]] or poly.center.z > zbase + 0.08: continue
        radial = Vector((poly.center.x, poly.center.y, 0))
        if radial.length > 0.02 and Vector((poly.normal.x, poly.normal.y, 0)).dot(radial.normalized()) < -0.5: inward += 1
    if inward > 10:
        c = sum((verts[i] for i in bottom), Vector()) / len(bottom)
        c.z = zbase + 0.003
        n = 64
        radius = [0.0] * n
        for i in bottom:
            p = verts[i]
            k = int((math.atan2(p.y - c.y, p.x - c.x) + math.pi) / (2 * math.pi) * n) % n
            radius[k] = max(radius[k], math.hypot(p.x - c.x, p.y - c.y))
        known = sorted(r for r in radius if r > 0)
        radius = [r if r > 0 else known[len(known) // 2] for r in radius]
        vs = [c.copy()]
        for k in range(n):
            a = -math.pi + (k + 0.5) * 2 * math.pi / n
            vs.append(Vector((c.x + 0.97 * radius[k] * math.cos(a), c.y + 0.97 * radius[k] * math.sin(a), c.z)))
        fs = [(0, 1 + (k + 1) % n, 1 + k) for k in range(n)]  # facing down
        extra.append((vs, fs, [base_uv(c)] * len(vs), [{"Hips": 1.0}] * len(vs)))
    # Legs, as the pawn's skin stretches into them: a short post from inside each foot up into the
    # base, the bottom on the foot and the top on the hips. Hidden at rest; drawn out when a foot moves.
    for side in ("Left", "Right"):
        foot = [i for i in range(len(verts)) if label[i] == side + "Foot"]
        if not foot: continue
        xs = [verts[i].x for i in foot]; ys = [verts[i].y for i in foot]; fz = [verts[i].z for i in foot]
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        r = 0.35 * min(max(xs) - min(xs), max(ys) - min(ys))
        z0 = max(min(fz) + 0.02, (min(fz) + max(fz)) / 2)
        z1 = max(zbase + 0.03, z0 + 0.03)
        rings, n = 6, 16
        vs, ws = [], []
        for k in range(rings):
            t = k / (rings - 1)
            for j in range(n):
                a = 2 * math.pi * j / n
                vs.append(Vector((cx + r * math.cos(a), cy + r * math.sin(a), z0 + (z1 - z0) * t)))
                ws.append({side + "Foot": 1.0 - t, "Hips": t})
        fs = [(k * n + j, k * n + (j + 1) % n, (k + 1) * n + (j + 1) % n, (k + 1) * n + j)
              for k in range(rings - 1) for j in range(n)]
        extra.append((vs, fs, [base_uv(Vector((cx, cy, zbase)))] * len(vs), ws))
    added = []
    for idx, (vs, fs, uvs, ws) in enumerate(extra):
        me2 = bpy.data.meshes.new("%s_extra%d" % (name, idx))
        me2.from_pydata([tuple(v) for v in vs], [], fs)
        me2.update()
        uvl = me2.uv_layers.new(name=me.uv_layers.active.name)
        for loop in me2.loops: uvl.data[loop.index].uv = uvs[loop.vertex_index]
        me2.materials.append(mesh.active_material)
        ob2 = bpy.data.objects.new(me2.name, me2)
        bpy.context.scene.collection.objects.link(ob2)
        for i, wt in enumerate(ws):
            for g, val in wt.items():
                if val <= 0: continue
                vg = ob2.vertex_groups.get(P + g) or ob2.vertex_groups.new(name=P + g)
                vg.add([i], val, "REPLACE")
        added.append(ob2)
    if added:
        for o in bpy.context.selected_objects: o.select_set(False)
        for o in added: o.select_set(True)
        mesh.select_set(True)
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.join()
    print("UNDER", name, "base bottom %.3f" % zbase, "lid" if inward > 10 else "no lid", "(%d inward faces)" % inward, "legs", len(extra) - (1 if inward > 10 else 0))

    mod = mesh.modifiers.new("Armature", "ARMATURE"); mod.object = arm
    mesh.parent = arm
    mesh.matrix_parent_inverse = arm.matrix_world.inverted()
    for o in bpy.context.selected_objects: o.select_set(False)
    arm.select_set(True); mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    os.makedirs(outdir, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, name + "_Rigged.fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, path_mode="COPY",
                             embed_textures=True, use_armature_deform_only=False)
    print("EXPORTED", name)
