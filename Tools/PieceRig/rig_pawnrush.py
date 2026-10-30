# Pawn Rush skin weights (seoungtest, 10-30, 승규 님: "섬세하게 폰 러쉬에서만 리깅을 다시 해줘").
#
# Usage (Blender 5.2, from the repository root):
#   blender -b -P Tools/PieceRig/rig_pawnrush.py -- <out folder> [Pawn King Queen Rook Bishop Knight]
# Reads Assets/Art/Pawn/Pawn.fbx for the pawn and Assets/Art/Pieces/<Piece>/<Piece>_Rigged.fbx (rig_piece.py's output)
# for the others, and writes <out>/<Name>_Rigged_PawnRush.fbx on the same Mixamo skeleton. Then in Unity:
# ChessFight/Ragdoll Lab/Bake Pawn Rush Piece Models (PieceModelBaker.BakePawnRush). The shared models, used by Sword
# Fight and every other mode, are not touched.
#
# What changes against the shared weights (RagdollLabBuilder for the pawn, rig_piece.py for the pieces):
#   neck     - the head turns on a short blend at the neck's narrowest point (Spine2 -> Head over 3 cm), so the face and
#              crown stay their shape when the head turns; the shared ones blended across the whole face (pieces) or
#              cut hard at the neck (pawn).
#   parts    - every small separate part on the body (eyes, cheeks, rings, a crown's points, a mane's spikes, the
#              shoulder balls) moves as one piece with the body under its middle; split by height between two bones
#              they sheared apart.
#   body     - the spine's weights are smoothed over the surface (three passes), no kinks where two bones' spans meet.
#   pawn     - its shoes are part of the body skin: their top 4 cm (under the skirt) blend into the hips instead of a
#              hard cut.
#   arms     - the root of each arm stub leans a little on the body (30 % at the root, gone a third of the way out), so
#              the shoulder bends instead of hinging; the mitten blends from the stub over the wrist.
import bpy, sys, os, json, math
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
outdir = args[0]
names = args[1:] or ["Pawn", "King", "Queen", "Rook", "Bishop", "Knight"]
P = "mixamorig:"
CHAIN = ["Hips", "Spine", "Spine1", "Spine2", "Head"]
LIMBS = ["LeftArm", "LeftHand", "RightArm", "RightHand", "LeftFoot", "RightFoot"]
DRIVEN = CHAIN + LIMBS


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def islands_of(me):
    par = list(range(len(me.vertices)))

    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]
            a = par[a]
        return a
    for e in me.edges:
        a, b = find(e.vertices[0]), find(e.vertices[1])
        if a != b:
            par[a] = b
    out = {}
    for v in me.vertices:
        out.setdefault(find(v.index), []).append(v.index)
    return list(out.values())


def normalise(w):
    top = sorted(((k, v) for k, v in w.items() if v > 1e-4), key=lambda kv: -kv[1])[:4]
    tot = sum(v for _, v in top) or 1.0
    return {k: v / tot for k, v in top}


def mix(a, b, t):
    out = {}
    for k, v in a.items():
        out[k] = out.get(k, 0.0) + v * (1 - t)
    for k, v in b.items():
        out[k] = out.get(k, 0.0) + v * t
    return out


report = {}
for name in names:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src = "Assets/Art/Pawn/Pawn.fbx" if name == "Pawn" else "Assets/Art/Pieces/%s/%s_Rigged.fbx" % (name, name)
    bpy.ops.import_scene.fbx(filepath=src)
    arm = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"][0]
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    mesh = max(meshes, key=lambda o: len(o.data.vertices))
    for o in meshes:
        if o != mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    me = mesh.data
    W = mesh.matrix_world
    verts = [W @ v.co for v in me.vertices]
    n = len(verts)
    bone = {b: arm.matrix_world @ arm.data.bones[P + b].head_local for b in DRIVEN + ["Neck"]}
    left_sign = 1.0 if bone["LeftHand"].x > 0 else -1.0
    height = max(p.z for p in verts) - min(p.z for p in verts)
    isl = islands_of(me)
    island_of = [0] * n
    for k, ids in enumerate(isl):
        for i in ids:
            island_of[i] = k
    old = [{} for _ in range(n)]
    gname = {g.index: g.name.replace(P, "") for g in mesh.vertex_groups}
    for v in me.vertices:
        for g in v.groups:
            if g.weight > 0:
                old[v.index][gname[g.group]] = g.weight

    # ---- which part each vertex belongs to
    label = ["Body"] * n           # Body, or one of LIMBS (whole-island limb parts keep their weights)
    keep = [False] * n             # a limb island (stub, mitten, foot, leg post) keeps rig_piece's weights
    main = max(range(len(isl)), key=lambda k: len(isl[k]))
    if name == "Pawn":
        # As RagdollLabBuilder: the biggest island is the body (feet included), hands far out, stubs at shoulder
        # height, everything else face detail on the head.
        for k, ids in enumerate(isl):
            if k == main:
                continue
            c = sum((verts[i] for i in ids), Vector()) / len(ids)
            ax = abs(c.x)
            side = "Left" if (c.x > 0) == (left_sign > 0) else "Right"
            if ax > 0.27 * height:
                kind = side + "Hand"
            elif ax > 0.13 * height and 0.4 * height < c.z < 0.65 * height:
                kind = side + "Arm"
            else:
                kind = "Body"
            for i in ids:
                label[i] = kind
    else:
        for k, ids in enumerate(isl):
            if any(old[i].get(l, 0) > 0.01 for i in ids for l in LIMBS):
                for i in ids:
                    keep[i] = True
                    best = max(old[i].items(), key=lambda kv: kv[1])[0]
                    label[i] = best if best in LIMBS else "Body"

    # ---- the spine and the neck
    zs = [bone[b].z for b in CHAIN]
    if name == "Pawn":
        # RagdollLabBuilder's spans (each bone 3 cm up), the neck where its builder put the head.
        zs = [bone["Hips"].z + 0.03, bone["Spine"].z + 0.03, bone["Spine1"].z + 0.03, bone["Spine2"].z + 0.03]
        neck = bone["Neck"].z - 0.01
    elif name == "Rook":
        # No neck: the face sits on the tower, the whole tower above the waist turns with the head (as rig_piece).
        zs = [bone["Hips"].z, 0.30, 0.34, 0.38]
        neck = 0.40
    else:
        zs = zs[:4]
        # The narrowest slice of the body between the shoulders and the head bone (1 cm slices).
        body = [verts[i] for i in range(n) if label[i] == "Body" and not keep[i]]
        lo, hi = bone["Spine2"].z - 0.04, bone["Head"].z + 0.02
        best, neck = 1e9, (bone["Spine2"].z + bone["Head"].z) / 2
        z = lo
        while z < hi:
            sl = [p for p in body if z <= p.z < z + 0.01]
            if len(sl) >= 8:
                r = max(math.hypot(p.x, p.y - bone["Hips"].y) for p in sl)
                if r < best:
                    best, neck = r, z + 0.005
            z += 0.01
    BLEND_DOWN, BLEND_UP = 0.02, 0.01

    def chain(z):
        if z >= neck + BLEND_UP:
            return {"Head": 1.0}
        if z <= zs[0]:
            w = {"Hips": 1.0}
        elif z >= zs[-1]:
            w = {"Spine2": 1.0}
        else:
            w = None
            for k in range(len(zs) - 1):
                if z <= zs[k + 1]:
                    t = smooth((z - zs[k]) / (zs[k + 1] - zs[k]))
                    w = {CHAIN[k]: 1 - t, CHAIN[k + 1]: t}
                    break
        if z > neck - BLEND_DOWN:
            w = mix(w, {"Head": 1.0}, smooth((z - (neck - BLEND_DOWN)) / (BLEND_DOWN + BLEND_UP)))
        return w

    new = [None] * n
    foot_top = 0.12 * height
    for i, p in enumerate(verts):
        if keep[i]:
            new[i] = dict(old[i])
        elif label[i] in LIMBS:
            new[i] = {label[i]: 1.0}
        elif name == "Pawn" and island_of[i] == main and p.z < foot_top and abs(p.x) > 0.035 * height:
            # The pawn's shoe is part of its skin (below 0.12 of its height; the skirt's hem starts there): all foot
            # at the sole, fading into the hips over its top 4 cm, all of it under the skirt. Reaching past the hem,
            # the first try dragged the skirt's edge out with each step.
            side = "Left" if (p.x > 0) == (left_sign > 0) else "Right"
            t = smooth((p.z - (foot_top - 0.04)) / 0.04)
            new[i] = mix({side + "Foot": 1.0}, chain(p.z), t)
        elif name == "Pawn" and island_of[i] != main:
            new[i] = {"Head": 1.0}   # face detail (RagdollLabBuilder), made rigid below
        else:
            new[i] = chain(p.z)

    # ---- arm stubs: the root leans a little on the body; the mitten blends in over the wrist
    for side in ("Left", "Right"):
        stub = [i for i in range(n) if label[i] == side + "Arm"]
        if not stub:
            continue
        x0 = min(abs(verts[i].x) for i in stub)
        x1 = max(abs(verts[i].x) for i in stub)
        span = max(0.01, x1 - x0)
        for i in stub:
            t = (abs(verts[i].x) - x0) / span
            share = 0.3 * (1 - smooth(t / 0.33))
            if share > 0.001:
                new[i] = mix({side + "Arm": 1.0} if not keep[i] else new[i], chain(verts[i].z), share)

    # ---- neighbours (within an island) for smoothing
    nbr = [[] for _ in range(n)]
    for e in me.edges:
        a, b = e.vertices
        nbr[a].append(b)
        nbr[b].append(a)

    # the body's spine weights smoothed over the surface; heads and limbs left alone
    # (and never spreads a foot into the skirt: a body vertex only takes on bones it already has or the spine's)
    smoothable = [not keep[i] and label[i] == "Body" and "Head" not in new[i] for i in range(n)]
    for _ in range(3):
        nxt = list(new)
        for i in range(n):
            if not smoothable[i] or not nbr[i]:
                continue
            acc = {k: v * 0.5 for k, v in new[i].items()}
            for j in nbr[i]:
                for k, v in new[j].items():
                    if k in LIMBS and k not in new[i]:
                        k = "Hips"
                    acc[k] = acc.get(k, 0.0) + 0.5 * v / len(nbr[i])
            nxt[i] = acc
        new = nxt
    # the pawn's mitten-to-stub join and stub-to-body: two soft passes where labels meet (as rig_piece)
    for _ in range(2):
        nxt = list(new)
        for i in range(n):
            if keep[i] or label[i] == "Body" or not nbr[i] or all(label[j] == label[i] for j in nbr[i]):
                continue
            acc = {k: v * 0.5 for k, v in new[i].items()}
            for j in nbr[i]:
                for k, v in new[j].items():
                    acc[k] = acc.get(k, 0.0) + 0.5 * v / len(nbr[i])
            nxt[i] = acc
        new = nxt

    # ---- small separate body parts move as one piece, with the body at their middle
    rigid = 0
    for k, ids in enumerate(isl):
        if k == main or keep[ids[0]] or label[ids[0]] in LIMBS:
            continue
        zs_ = [verts[i].z for i in ids]
        if max(zs_) - min(zs_) > 0.12:
            continue   # a tall part of the body (a column, a base): it bends along its length
        c = sum((verts[i] for i in ids), Vector()) / len(ids)
        w = normalise(chain(c.z))
        for i in ids:
            new[i] = dict(w)
        rigid += 1

    # ---- write the weights
    for g in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(g)
    groups = {b: mesh.vertex_groups.new(name=P + b) for b in DRIVEN}
    for i, w in enumerate(new):
        for k, v in normalise(w).items():
            if v > 0.01:
                groups[k].add([i], v, "REPLACE")
    if not any(m.type == "ARMATURE" for m in mesh.modifiers):
        mod = mesh.modifiers.new("Armature", "ARMATURE")
        mod.object = arm
    report[name] = {"vertices": n, "islands": len(isl), "rigid parts": rigid, "neck": round(neck, 3)}
    print("PAWNRUSH", name, json.dumps(report[name]))

    for o in bpy.context.selected_objects:
        o.select_set(False)
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    os.makedirs(outdir, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, name + "_Rigged_PawnRush.fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, path_mode="COPY",
                             embed_textures=True, use_armature_deform_only=False)
    print("EXPORTED", name)
