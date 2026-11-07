# Closes the small holes Tripo leaves in a model's face (R505, user 11-07: "아직 남았어" — gaps between stone blocks
# showed through to the inside). Shared by d_props_v1.py (wall), d_team_gate_v1.py (team arch), d_center_gate_v1.py.
#
#   import mg_holes; mg_holes.close_small_holes(obj, max_perimeter=4.0)
#
# Every open border loop of the mesh shorter than max_perimeter (metres, after the model is at game size) is filled
# with faces; the new corners take the UV their vertex already has on a neighbouring face, so the fill wears the
# stone around it. Loops longer than that (a model's whole open back) are left for the mirrored back copy.
import bmesh


def close_small_holes(ob, max_perimeter=4.0):
    bm = bmesh.new(); bm.from_mesh(ob.data)
    uvl = bm.loops.layers.uv.active
    boundary = [e for e in bm.edges if e.is_boundary]
    seen, loops = set(), []
    for e in boundary:
        if e in seen: continue
        group, stack = [], [e]
        while stack:
            x = stack.pop()
            if x in seen: continue
            seen.add(x); group.append(x)
            for v in x.verts:
                for y in v.link_edges:
                    if y.is_boundary and y not in seen: stack.append(y)
        loops.append(group)
    filled = 0
    for group in loops:
        if sum(e.calc_length() for e in group) > max_perimeter: continue
        uv_of = {}
        for e in group:
            for v in e.verts:
                if v not in uv_of and v.link_loops and uvl is not None:
                    uv_of[v] = v.link_loops[0][uvl].uv.copy()
        try:
            faces = bmesh.ops.holes_fill(bm, edges=group, sides=0)["faces"]
        except Exception:
            continue
        if not faces: continue
        faces = bmesh.ops.triangulate(bm, faces=faces, quad_method='BEAUTY', ngon_method='BEAUTY')["faces"]
        mat = group[0].link_faces[0].material_index if group[0].link_faces else 0
        new = set(faces)
        for f in faces:
            f.material_index = mat
            if uvl is not None:
                for l in f.loops:
                    if l.vert in uv_of: l[uvl].uv = uv_of[l.vert]
            # face the same way as the original face across the hole's rim (only the new faces are turned)
            f.normal_update()
            rim = [g for e in f.edges for g in e.link_faces if g not in new]
            if rim:
                n = sum((g.normal for g in rim), f.normal * 0)
                if n.length > 0 and f.normal.dot(n) < 0: f.normal_flip()
        filled += 1
    bm.to_mesh(ob.data); bm.free()
    print("HOLES %s: %d of %d open loops filled (shorter than %.1f m)" % (ob.name, filled, len(loops), max_perimeter))
    return filled


def plug_gaps(ob, material, step=.1, gap=.05, deep=.15, depth=.12):
    """Tripo builds a facade's stones as separate closed pieces with deep gaps between them; looking into a gap shows
    the pieces behind (user, 11-07: "아직 남았어"). From both sides (Blender -y and +y) rays are cast on a `step` grid
    to find where the face really is. A grid square whose hit lies more than `deep` behind its neighbours' (or that
    sees straight through, inside a solid neighbourhood) is a gap: a small plug goes there, `gap` behind the
    deepest neighbouring face, so it never stands out of the face. Flat faces and the model's own
    recesses get nothing. Plugs wear `material` (a tileable stone) box-mapped in metres. (A first try laid slabs from the
    mesh's vertices; on a decimated mesh broad faces have no vertices inside, and the slabs stood out of them.)"""
    import numpy as np
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    bm0 = bmesh.new(); bm0.from_mesh(ob.data)
    tree = BVHTree.FromBMesh(bm0); bm0.free()
    vs = np.array([v.co[:] for v in ob.data.vertices])
    lo, hi = vs.min(0), vs.max(0)
    xs = np.arange(lo[0] + step / 2, hi[0], step); zs = np.arange(lo[2] + step / 2, hi[2], step)
    ob.data.materials.append(material); slot = len(ob.data.materials) - 1
    bm = bmesh.new(); bm.from_mesh(ob.data)
    uvl = bm.loops.layers.uv.active
    made = 0
    for sign in (1, -1):                                   # +1: looking from -y (front), -1: from +y (back)
        start = (lo[1] - 1) if sign > 0 else (hi[1] + 1)
        hit = np.full((len(xs), len(zs)), np.nan)
        for i, x in enumerate(xs):
            for k, z in enumerate(zs):
                loc, _, _, _ = tree.ray_cast(Vector((x, start, z)), Vector((0, sign, 0)))
                if loc is not None: hit[i, k] = loc.y * sign      # depth measured into the model
        for i in range(1, len(xs) - 1):
            for k in range(1, len(zs) - 1):
                around = np.concatenate([hit[i - 1:i + 2, k - 1], hit[i - 1:i + 2, k + 1], hit[[i - 1, i + 1], k]])
                around = around[~np.isnan(around)]
                if len(around) < 6: continue                   # an edge or an opening, not a gap
                own = hit[i, k]
                if not np.isnan(own) and own < around.min() + deep: continue
                face = around.max() + gap                      # just behind the deepest face around it
                if not np.isnan(own) and face >= own - .01: continue
                y0, y1 = sorted((face * sign, (face + depth) * sign))
                x0, x1, z0, z1 = xs[i] - step * .6, xs[i] + step * .6, zs[k] - step * .6, zs[k] + step * .6
                v8 = [bm.verts.new((x, y, z)) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
                for q in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
                    f = bm.faces.new([v8[j] for j in q]); f.material_index = slot; f.normal_update()
                    a, b, c = (abs(t) for t in f.normal)
                    for l in f.loops:
                        co = l.vert.co
                        u, w = (co.x, co.y) if c >= max(a, b) else ((co.y, co.z) if a >= b else (co.x, co.z))
                        if uvl is not None: l[uvl].uv = (u / 2.0, w / 2.0)
                made += 1
    new = [f for f in bm.faces if f.material_index == slot]
    if new: bmesh.ops.recalc_face_normals(bm, faces=new)
    bm.to_mesh(ob.data); bm.free()
    print("PLUGS %s: %d gap plugs (%.2f m grid)" % (ob.name, made, step))
    return made


def stone_brick(name, tex_dir, tint):
    """The tileable R502 stone brick, tinted (linear), as a Blender material (Unity: PawnRushMiniGames/Materials/<name>)."""
    import bpy, os
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(os.path.join(tex_dir, "PR_Early_StoneBrick_BaseColor.png"))
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 1
    mix.inputs[7].default_value = (*tint, 1)
    nt.links.new(tex.outputs[0], mix.inputs[6]); nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = .9
    return m


def mirror_back(ob, depth_scale=.97, width_scale=.998):
    """A facade model's real face on one side only (Tripo), a broken half-made back on the other. From behind, those
    broken remnants stood a little outside the mirrored copy and showed holes with the copy's stones behind them
    (user, 11-07: "아직 남았어", the centre gate's back). So: find the complete side (rays from both sides: the side
    whose hits lie closer to its bounding face), delete the far half's faces that look the other way, then add the
    remaining face mirrored about the middle depth, a hair thinner and narrower (no shared planes, no flicker)."""
    import numpy as np
    from mathutils import Vector, Matrix
    from mathutils.bvhtree import BVHTree
    bm = bmesh.new(); bm.from_mesh(ob.data)
    tree = BVHTree.FromBMesh(bm)
    vs = np.array([v.co[:] for v in bm.verts]); lo, hi = vs.min(0), vs.max(0)
    mid = float((lo[1] + hi[1]) / 2)
    depth = {}
    for sign in (1, -1):
        start = lo[1] - 1 if sign > 0 else hi[1] + 1
        d = []
        for x in np.linspace(lo[0], hi[0], 40)[1:-1]:
            for z in np.linspace(lo[2], hi[2], 30)[1:-1]:
                loc, _, _, _ = tree.ray_cast(Vector((x, start, z)), Vector((0, sign, 0)))
                if loc is not None: d.append(abs(loc.y - (lo[1] if sign > 0 else hi[1])))
        depth[sign] = np.mean(d) if d else 9
    front = 1 if depth[1] <= depth[-1] else -1          # +1: the face looks toward -y
    bside = front                                        # +1: the broken side is +y (the face looks -y)
    # Cut exactly at the middle depth and drop the whole far half (removing only its back-looking faces left side
    # slivers, eave undersides and shards outside the copy, still broken from behind), then add the near half
    # mirrored: both halves are the real face and meet exactly in the cut plane, which holds no faces of its own.
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=Vector((0, mid, 0)), plane_no=Vector((0, bside, 0)), clear_outer=True)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    drop = []
    bm.to_mesh(ob.data); bm.free()
    me = ob.data.copy()
    me.transform(Matrix.Translation((0, mid, 0)) @ Matrix.Diagonal((1.0, -1.0, 1.0, 1.0)) @ Matrix.Translation((0, -mid, 0)))
    me.flip_normals()
    b2 = bmesh.new(); b2.from_mesh(ob.data); b2.from_mesh(me); b2.to_mesh(ob.data); b2.free()
    import bpy
    bpy.data.meshes.remove(me)
    print("MIRROR %s: face looks %s (mean depth %.2f vs %.2f), far half cut off at y %.2f and the near half mirrored"
          % (ob.name, "-y" if front == 1 else "+y", depth[1], depth[-1], mid))
