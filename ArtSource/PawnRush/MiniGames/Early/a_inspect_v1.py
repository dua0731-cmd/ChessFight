# R506 A Ceremonial Court: inspect the user's Tripo FBX files before trimming.
#
# For each source FBX: size, triangles, loose pieces, embedded textures; then split it into assets by top-view
# position: these sheets come back standing in one row along X (front -Y, Z up), so faces are split at the empty
# gaps of the X occupancy. Prints one row per asset and renders
#   render_a_inspect_<key>_top.png   top view, assets numbered left to right
#   render_a_inspect_<key>_q34.png   3/4 view with textures
#   render_a_inspect_<key>_back.png  the same from behind
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python a_inspect_v1.py
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
FILES = {"facade": "R506_A_Facade_StoneCastleGate.fbx",
         "structure": "R506_A_Structure_StoneCastleAssets.fbx",
         "props": "R506_A_Props_StoneFlowerPlanter.fbx"}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def load(path):
    bpy.ops.import_scene.fbx(filepath=path)
    obs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    if len(obs) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob, len(obs)


def islands(me):
    """Loose pieces (vertex connectivity) by union-find."""
    n = len(me.vertices)
    parent = np.arange(n)

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    ev = np.empty(len(me.edges) * 2, np.int64)
    me.edges.foreach_get("vertices", ev)
    for a, b in ev.reshape(-1, 2):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    return len({find(i) for i in range(n)})


def blobs(me, bins=2000):
    """Assets stand in one row along X (front view): split faces at empty gaps of the X occupancy of all
    vertices. Returns (label per face, asset ids left to right, face centres)."""
    nf = len(me.polygons)
    cen = np.empty(nf * 3, np.float32)
    me.polygons.foreach_get("center", cen)
    cen = cen.reshape(-1, 3)
    co = np.empty(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", co)
    x = co.reshape(-1, 3)[:, 0]
    lo, hi = x.min(), x.max()
    occ = np.zeros(bins, bool)
    # an edge covers every bin between its two ends, so a long face never leaves a false gap
    ev = np.empty(len(me.edges) * 2, np.int64)
    me.edges.foreach_get("vertices", ev)
    ev = ev.reshape(-1, 2)
    a = ((x[ev[:, 0]] - lo) / (hi - lo) * (bins - 1)).astype(int)
    b = ((x[ev[:, 1]] - lo) / (hi - lo) * (bins - 1)).astype(int)
    for i, j in zip(np.minimum(a, b), np.maximum(a, b)):
        occ[i:j + 1] = True
    lab_bin = np.cumsum(np.concatenate([[1], (occ[1:] & ~occ[:-1]).astype(int)]))
    fb = ((cen[:, 0] - lo) / (hi - lo) * (bins - 1)).astype(int)
    face_lab = lab_bin[fb]
    ids = sorted(set(face_lab.tolist()))
    return face_lab, ids, cen


def tri_count(me, mask=None):
    lt = np.empty(len(me.polygons), np.int32)
    me.polygons.foreach_get("loop_total", lt)
    t = lt - 2
    return int(t.sum() if mask is None else t[mask].sum())


def bbox_of(me, mask):
    vi = set()
    for p in np.nonzero(mask)[0]:
        vi.update(me.polygons[int(p)].vertices)
    co = np.array([me.vertices[i].co[:] for i in vi])
    return co.min(0), co.max(0)


def textures():
    out = []
    for im in bpy.data.images:
        if im.size[0]:
            out.append(f"{im.name} {im.size[0]}x{im.size[1]}")
    return out


def render(ob, key, labels):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.display.shading.show_cavity = True
    sc.render.film_transparent = False
    w = bpy.data.worlds.new("w")
    sc.world = w
    sc.display.shading.background_type = "VIEWPORT"
    sc.render.resolution_x, sc.render.resolution_y = 1800, 1000
    lo = Vector(np.array([v.co[:] for v in ob.data.vertices]).min(0))
    hi = Vector(np.array([v.co[:] for v in ob.data.vertices]).max(0))
    mid, size = (lo + hi) / 2, hi - lo
    cam = bpy.data.cameras.new("cam")
    co = bpy.data.objects.new("cam", cam)
    sc.collection.objects.link(co)
    sc.camera = co
    for name, x, y, t in labels:
        cu = bpy.data.curves.new("t", "FONT")
        cu.body = name
        cu.size = max(size.x, size.y) * .04
        cu.align_x = "CENTER"
        to = bpy.data.objects.new("t", cu)
        to.location = (x, y, hi.z + .01)
        sc.collection.objects.link(to)
    # top
    cam.type = "ORTHO"
    cam.ortho_scale = max(size.x * 1.08, size.y * 1.08 * 1.8)
    co.location = (mid.x, mid.y, hi.z + 5)
    co.rotation_euler = (0, 0, 0)
    sc.render.filepath = os.path.join(HERE, f"render_a_inspect_{key}_top.png")
    bpy.ops.render.render(write_still=True)
    for o in [o for o in sc.objects if o.type == "FONT"]:
        bpy.data.objects.remove(o)
    # 3/4 from the front (-Y), slightly above
    cam.type = "PERSP"
    cam.lens = 50
    d = max(size.x, size.z * 1.8) * 1.15
    co.location = (mid.x + d * .35, mid.y - d * 1.6, mid.z + d * .55)
    co.rotation_euler = (mid - co.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(HERE, f"render_a_inspect_{key}_q34.png")
    bpy.ops.render.render(write_still=True)
    # the same from behind (+Y): tells which side Tripo made the front
    co.location = (mid.x - d * .35, mid.y + d * 1.6, mid.z + d * .55)
    co.rotation_euler = (mid - co.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(HERE, f"render_a_inspect_{key}_back.png")
    bpy.ops.render.render(write_still=True)


for key, fn in FILES.items():
    reset()
    ob, nobj = load(os.path.join(SRC, fn))
    me = ob.data
    lo = np.array([v.co[:] for v in me.vertices]).min(0)
    hi = np.array([v.co[:] for v in me.vertices]).max(0)
    print(f"\n=== {key}: {fn}")
    print(f"objects {nobj}  size {hi[0]-lo[0]:.3f} x {hi[1]-lo[1]:.3f} x {hi[2]-lo[2]:.3f}  "
          f"(x {lo[0]:.3f}..{hi[0]:.3f} y {lo[1]:.3f}..{hi[1]:.3f} z {lo[2]:.3f}..{hi[2]:.3f})")
    print(f"triangles {tri_count(me)}  loose pieces {islands(me)}  materials {[m.name for m in me.materials if m]}")
    print("textures", textures())
    face_lab, ids, cen = blobs(me)
    labels = []
    for n, i in enumerate(ids, 1):
        mask = face_lab == i
        a, b = bbox_of(me, mask)
        s = b - a
        print(f"  asset {n}: tris {tri_count(me, mask):7d}  size {s[0]:.3f} x {s[1]:.3f} x {s[2]:.3f}  "
              f"centre x {(a[0]+b[0])/2:.3f} y {(a[1]+b[1])/2:.3f}  z {a[2]:.3f}..{b[2]:.3f}")
        labels.append((str(n), (a[0] + b[0]) / 2, a[1] - (hi[1] - lo[1]) * .06, 0))
    render(ob, key, labels)
