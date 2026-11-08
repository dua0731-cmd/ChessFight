"""Pawn Rush library: make the two goal armillaries movable (R324).

    blender --background --factory-startup --python armillary.py
    blender --background --factory-startup --python armillary.py -- --preview D:/check   (renders, writes nothing)

The armillaries (Goal_armillary_-1, Goal_armillary_1) came as one welded mesh each: stand, rings and sphere fused,
and the rings all about the same size, so no ring could turn without cutting through another. This script keeps
the stand (cut at the post's neck, z NECK) and rebuilds the moving parts as a gimbal in the same chunky style:

    Ring1  outer meridian, radius .585, turns about the vertical axis (finial on top, cup on the post below)
    Ring2  equator ring, radius .48, pinned to Ring1's side blocks, rolls about that pin axis
    Ring3  inner meridian, radius .39, pinned to Ring2's blocks, turns about that pin axis (the other way)
    Sphere radius .333, two raised seam bands so its spin shows, turns about a diagonal axis

R327 (승규 님: "마지막 구간에 넣지 말고 ... 작전 지도만큼 키운다음 1개만 옆에 배치"): only one armillary is kept,
renamed Start_armillary, moved beside the start's war map (V21_Original_treasure_map_clay, top 3.95 m) and scaled
START_SCALE so its finial reaches the map's top; the goal's pair is gone.

At rest the rings stand where the old ones were (two meridians and the equator). Each part's pivot is the sphere
centre; Ring2 is Ring1's child and Ring3 is Ring2's, so each turns inside the one that holds it. Every part has an
empty <part>_axis beside it (in the parent's space, so it stays put) that marks its turning axis for Unity.
Radii are chosen so that no part touches another in any pose (clearances in CLEAR below).
Unity: LibraryArmillary.cs turns the parts on the shared obstacle clock.
Running it again on the edited file changes nothing.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from export_library_fbx import SETTINGS  # noqa: E402

FBX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary",
                                    "PawnRushLibraryEnvironment.fbx"))
ARMILLARIES = ("Goal_armillary_-1", "Goal_armillary_1")
# R327: the one kept (ARMILLARIES[1]) as START_NAME, standing on the start floor (z 0) west of the war map
# (pedestal x -7.28 .. -2.72, map centre y -19.7), 0.6 m clear of the pedestal; the armillary is 2.468 m tall.
START_NAME = "Start_armillary"
START_SCALE = 3.95 / 2.468
START_AT = Vector((-7.28 - .6 - .69 * START_SCALE, -19.7, 0.0))
CENTRE_Z = 1.6942   # sphere centre height in the armillary's space (least-squares fit of the old sphere)
NECK = 1.07         # the post's narrowest point: the stand is kept below it
SPHERE_R = .333
TILT = 35.0         # the sphere's axis leans this far from vertical, toward +x +y

# Rings: radius of the tube's middle line, tube size across (radial) and along the axis.
R1, R1_RAD, R1_AX = .585, .08, .09
R2, R2_RAD, R2_AX = .48, .065, .075
R3, R3_RAD, R3_AX = .39, .06, .07
# Closest approach in any pose (m): Ring1's top/bottom block inner face .545 vs Ring2's outermost corner .525;
# Ring2's inner collars .4375 vs Ring3's outermost corner .427; Ring3's collars .355 vs the sphere's bands .3453.
# Pinned neighbours only turn about their shared pin, and the bosses round it keep 1 cm from the next part.
CLEAR = (.545 - .525, .4375 - .427, .355 - .3453)


# ------------------------------------------------------------------ shapes (all in the part's own space, pivot 0)

def append(dst, src, matrix=Matrix.Identity(4)):
    """Copies bmesh src into dst, moved by matrix."""
    vmap = {}
    for v in src.verts:
        vmap[v] = dst.verts.new(matrix @ v.co)
    for f in src.faces:
        dst.faces.new([vmap[v] for v in f.verts])


def ring(dst, radius, rad, ax, u, v, n, segments=80, corner=None):
    """A closed tube of rounded-rectangle section around a circle in the plane (u, v), axis n."""
    corner = corner if corner is not None else min(rad, ax) * .3
    profile = []
    hr, ha = rad / 2 - corner, ax / 2 - corner
    for cx, cy, a0 in ((hr, ha, 0), (-hr, ha, 90), (-hr, -ha, 180), (hr, -ha, 270)):
        for k in range(4):
            a = math.radians(a0 + k * 30)
            profile.append((cx + corner * math.cos(a), cy + corner * math.sin(a)))
    rows = []
    for i in range(segments):
        t = 2 * math.pi * i / segments
        out = u * math.cos(t) + v * math.sin(t)
        rows.append([dst.verts.new(out * (radius + pr) + n * pa) for pr, pa in profile])
    m = len(profile)
    for i in range(segments):
        a, b = rows[i], rows[(i + 1) % segments]
        for k in range(m):
            dst.faces.new((a[k], b[k], b[(k + 1) % m], a[(k + 1) % m]))


def bevelled(make, offset, segments=3):
    bm = bmesh.new()
    make(bm)
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=offset, offset_type="OFFSET", profile_type="SUPERELLIPSE",
                    segments=segments, profile=.5, affect="EDGES", clamp_overlap=True)
    return bm


def block(dst, centre, size, frame, bevel=.014):
    """A bevelled box: size along frame's three axes (x, y, z columns of frame)."""
    bm = bevelled(lambda b: bmesh.ops.create_cube(b, size=1.0, matrix=Matrix.Diagonal((*size, 1.0))), bevel)
    append(dst, bm, Matrix.Translation(centre) @ frame.to_4x4())
    bm.free()


def cylinder(dst, a, b, radius, bevel=.006, segments=24):
    """A capped, edge-rounded cylinder from point a to point b."""
    d = b - a
    bm = bevelled(lambda x: bmesh.ops.create_cone(x, cap_ends=True, cap_tris=False, segments=segments,
                                                  radius1=radius, radius2=radius, depth=d.length), bevel, 2)
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    append(dst, bm, Matrix.Translation((a + b) / 2) @ rot)
    bm.free()


def frame_of(tangent, radial, axial):
    return Matrix((tangent, radial, axial)).transposed()


def ring_block(dst, radius, angle, u, v, n, tangent, r_in, r_out, axial, bevel=.014):
    """A block on a ring at angle (degrees) spanning radius r_in..r_out (measured from the pivot)."""
    t = math.radians(angle)
    out = u * math.cos(t) + v * math.sin(t)
    tan = -u * math.sin(t) + v * math.cos(t)
    block(dst, out * ((r_in + r_out) / 2), (tangent, r_out - r_in, axial), frame_of(tan, out, n), bevel)


X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


def build_ring1():
    bm = bmesh.new()
    ring(bm, R1, R1_RAD, R1_AX, X, Z, Y)
    for a in (0, 180):          # side blocks holding Ring2's pins, with a round boss outside
        ring_block(bm, R1, a, X, Z, Y, .12, R1 - .06, R1 + .065, .13)
        s = 1 if a == 0 else -1
        cylinder(bm, X * s * (R1 + .06), X * s * (R1 + .09), .045)
        cylinder(bm, X * s * (R1 + .085), X * s * (R1 + .105), .024)
    ring_block(bm, R1, 90, X, Z, Y, .12, R1 - .04, R1 + .06, .13)     # top
    ring_block(bm, R1, 270, X, Z, Y, .12, R1 - .04, R1 + .02, .13)    # bottom, sits on the cup
    top = R1 + .06
    cylinder(bm, Z * (top - .01), Z * (top + .035), .03)               # finial stem, collar, knob
    cylinder(bm, Z * (top + .03), Z * (top + .05), .055)
    knob = bmesh.new()
    bmesh.ops.create_uvsphere(knob, u_segments=20, v_segments=12, radius=.055)
    append(bm, knob, Matrix.Translation(Z * (top + .085)) @ Matrix.Diagonal((1, 1, .8, 1)))
    knob.free()
    low = -(R1 + .02)
    cylinder(bm, Z * (NECK - CENTRE_Z - .005), Z * (low + .005), .085)  # cup from the post's neck to the block
    return bm


def build_ring2():
    bm = bmesh.new()
    ring(bm, R2, R2_RAD, R2_AX, X, Y, Z)
    for s in (1, -1):
        cylinder(bm, X * s * (R2 - .02), X * s * (R2 + .035), .05)         # bearing boss at the pin
        cylinder(bm, X * s * (R2 + .03), X * s * (R1 - .02), .022)          # pin into Ring1's side block
        ring_block(bm, R2, 90 if s > 0 else 270, X, Y, Z, .1, R2 - .04, R2 + .04, .1)   # holds Ring3's pin
    for a in (45, 135, 225, 315):
        ring_block(bm, R2, a, X, Y, Z, .05, R2 - .0425, R2 + .0425, .095, .01)
    return bm


def build_ring3():
    bm = bmesh.new()
    ring(bm, R3, R3_RAD, R3_AX, Y, Z, X)
    for s in (1, -1):
        cylinder(bm, Y * s * (R3 - .02), Y * s * (R3 + .04), .045)          # bearing boss
        cylinder(bm, Y * s * (R3 + .035), Y * s * (R2 - .01), .02)          # pin into Ring2's block
    for a in (45, 135, 225, 315):
        ring_block(bm, R3, a, Y, Z, X, .045, R3 - .035, R3 + .035, .085, .01)
    return bm


def sphere_axis():
    t = math.radians(TILT)
    return Vector((math.sin(t) * math.cos(math.pi / 4), math.sin(t) * math.sin(math.pi / 4), math.cos(t)))


def build_sphere():
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=SPHERE_R)
    a = sphere_axis()
    side = a.cross(Z).normalized()
    third = a.cross(side).normalized()
    # One band around the spin axis (shows the lean) and one through the poles (shows the turn).
    ring(bm, SPHERE_R + .002, .02, .03, side, third, a, 96, .006)
    ring(bm, SPHERE_R + .002, .02, .03, a, side, third, 96, .006)
    return bm


# ------------------------------------------------------------------ file

def to_mesh(name, bm, material):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(material)
    return me


def cut_stand(body):
    """Keeps the stand below NECK in the shared body mesh and closes the cut with a flat cap."""
    bm = bmesh.new()
    bm.from_mesh(body.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, NECK), plane_no=(0, 0, 1), clear_outer=True)
    open_edges = [e for e in bm.edges if e.is_boundary and all(abs(v.co.z - NECK) < 1e-4 for v in e.verts)]
    bmesh.ops.holes_fill(bm, edges=open_edges, sides=0)
    neck = [v.co for v in bm.verts if abs(v.co.z - NECK) < 1e-4]
    centre = sum(neck, Vector()) / len(neck)
    bm.to_mesh(body.data)
    bm.free()
    return Vector((centre.x, centre.y, CENTRE_Z))


def add(col, name, data, parent, world):
    ob = bpy.data.objects.new(name, data)
    col.objects.link(ob)
    ob.parent = parent
    ob.matrix_world = world
    if data is None:
        ob.empty_display_size = .05
    return ob


def make_movable(col, name, shared):
    root = bpy.data.objects.get(name)
    if root is None:
        raise SystemExit(f"{name} not found")
    if any(c.name == name + "_Ring1" for c in root.children):
        print(f"{name}: already movable")
        return None
    body = next(c for c in root.children if c.type == "MESH")
    if "parts" not in shared:
        local = root.matrix_world.inverted() @ body.matrix_world
        if any(abs(local[i][j] - (i == j)) > 1e-4 for i in range(4) for j in range(4)):
            raise SystemExit(f"{name}: the body is offset from its empty; stopped")
        material = body.data.materials[0]
        pivot = cut_stand(body)
        shared["pivot"] = pivot
        shared["parts"] = {p: to_mesh("Armillary_" + p, b(), material) for p, b in
                           (("Ring1", build_ring1), ("Ring2", build_ring2), ("Ring3", build_ring3),
                            ("Sphere", build_sphere))}
    pivot, parts = shared["pivot"], shared["parts"]
    W = root.matrix_world
    at = W @ Matrix.Translation(pivot)
    r1 = add(col, name + "_Ring1", parts["Ring1"], root, at)
    r2 = add(col, name + "_Ring2", parts["Ring2"], r1, at)
    r3 = add(col, name + "_Ring3", parts["Ring3"], r2, at)
    sp = add(col, name + "_Sphere", parts["Sphere"], root, at)
    # Axis marks: each sits on its part's turning axis, in the space of the part's parent.
    for part, holder, axis in ((r1, root, Z), (r2, r1, X), (r3, r2, Y), (sp, root, sphere_axis())):
        add(col, part.name + "_axis", None, holder, at @ Matrix.Translation(axis * .7))
    print(f"{name}: pivot {tuple(round(x, 4) for x in (W @ pivot))}")
    return [r1, r2, r3, sp]


def move_to_start():
    """Drops the goal's other armillary and stands the kept one, scaled, beside the start's war map."""
    gone = bpy.data.objects[ARMILLARIES[0]]
    for ob in [gone] + list(gone.children_recursive):
        bpy.data.objects.remove(ob)
    root = bpy.data.objects[ARMILLARIES[1]]
    for ob in [root] + list(root.children_recursive):
        ob.name = ob.name.replace(ARMILLARIES[1], START_NAME, 1)
    root.matrix_world = Matrix.Translation(START_AT) @ Matrix.Scale(START_SCALE, 4)
    print(f"{START_NAME}: at {tuple(round(x, 3) for x in START_AT)}, scale {START_SCALE:.3f}")


def preview(out_dir):
    """Renders the armillary beside the map at rest and in three turned poses (Unity's speeds at t = 0, 6, 13, 21 s)."""
    bpy.context.view_layer.update()     # the children's world matrices still show the goal until this
    root = bpy.data.objects[START_NAME]
    made = [bpy.data.objects[START_NAME + p] for p in ("_Ring1", "_Ring2", "_Ring3", "_Sphere")]
    keep = {root} | set(root.children_recursive)
    for ob in list(bpy.data.objects):   # the ceiling (everything above 6 m) would hide the view
        if ob.type == "MESH" and ob not in keep:
            ob.hide_render = min((ob.matrix_world @ Vector(c)).z for c in ob.bound_box) > 6
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.show_cavity = True
    scene.render.resolution_x = scene.render.resolution_y = 800
    cam = bpy.data.objects.new("preview camera", bpy.data.cameras.new("preview camera"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    r1, r2, r3, sp = made
    rest = {ob: ob.matrix_basis.copy() for ob in made}
    centre = r1.matrix_world.translation.copy()
    speeds = ((r1, Z, 9.0), (r2, X, -15.0), (r3, Y, 22.0), (sp, sphere_axis(), 30.0))
    for t in (0, 6, 13, 21):
        for ob, axis, speed in speeds:
            ob.matrix_basis = rest[ob] @ Matrix.Rotation(math.radians(speed * t), 4, axis)
        bpy.context.view_layer.update()
        # From the course (+y) looking back at the armillary and the map, and closer from its outer side.
        # From above the start of the course (clear of the balconies) and from the start floor in front of it.
        for view, eye, target in (("course", Vector((0, -4, 7)), centre + Vector((2.2, 0, -.9))),
                                  ("near", Vector((-6, -14.5, 3.2)), centre)):
            cam.location = eye
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(out_dir, f"armillary_t{t:02d}_{view}.png")
            bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    if bpy.data.objects.get(START_NAME) is not None:
        print(f"{START_NAME}: already in place")
        return
    shared = {}
    for n in ARMILLARIES:
        make_movable(col, n, shared)
    print("clearances (m):", tuple(round(c, 4) for c in CLEAR))
    move_to_start()
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])
        return
    view.active_layer_collection = view.layer_collection.children[col.name]
    path = os.path.join(os.path.dirname(FBX), "PawnRushLibraryEnvironment.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {path}")


main()
