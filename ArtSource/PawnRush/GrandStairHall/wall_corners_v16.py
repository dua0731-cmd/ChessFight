# The hall's outer walls (v9 Hall_wall_E/W/S (arch)), 승규 11-03:
#   - the gap under them: the stretched modules' bottom stones stop up to 13 cm over the hall floor (-4), so a dark slot
#     shows along the foot. A plinth course runs along each inner face from the floor up 0.6 m, standing 0.3 m proud.
#   - the corner gaps: where the south walls meet the side walls (the side walls start at y 12, the south walls' depth
#     reaches 10.7) and where the side walls end at the round wall (y 92). Each gets the supplied stone column
#     (source/stone_column.glb), its base and capital kept in proportion and only its shaft stretched to the wall's
#     height (-4 .. 26), its footprint over the open square.
# Nothing walked changes (the hall floor is the respawn floor). No paint: v9's grey Arch_Clay.
# Run: blender -b grand_stair_hall_v15.blend -P wall_corners_v16.py   -> grand_stair_hall_v16.blend
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
FLOOR, TOP = -4.0, 26.0
PLINTH_H, PLINTH_OUT, PLINTH_IN = 0.6, 0.3, 0.25     # height, proud of the wall's inner face, into the wall
CLAY = bpy.data.materials["Arch_Clay"]
sc = bpy.context.scene
col = bpy.data.collections.new("Wall_corners_v16"); sc.collection.children.link(col)

# (name, centre x, centre y, footprint): each column stands in the corner, its shaft 0.8 m proud of both wall faces
# (the shaft is two thirds of the base, so further back it would vanish into the walls), its back in the walls.
CORNERS = [("Corner_column_SE", 34.6, 13.2, 4.2), ("Corner_column_SW", -34.6, 13.2, 4.2),
           ("Corner_column_NE", 34.6, 92.0, 4.2), ("Corner_column_NW", -34.6, 92.0, 4.2)]

def plinth_runs():
    """(start, along, inward normal, length): the inner faces between the corner columns."""
    runs = []
    for s in (1, -1):
        se, ne = CORNERS[0 if s > 0 else 1], CORNERS[2 if s > 0 else 3]
        y0, y1 = se[2] + se[3] / 2, ne[2] - ne[3] / 2
        runs.append((Vector((34 * s, y0, 0)), Vector((0, 1, 0)), Vector((-s, 0, 0)), y1 - y0))
        x_in = se[1] - s * se[3] / 2
        a, b = sorted((21 * s, x_in))
        runs.append((Vector((a, 14, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)), b - a))
    return runs


def plinth():
    bm = bmesh.new()
    prof = [(-PLINTH_IN, 0), (PLINTH_OUT, 0), (PLINTH_OUT, PLINTH_H - .18), (PLINTH_OUT - .06, PLINTH_H - .12),
            (PLINTH_OUT - .06, PLINTH_H - .06), (PLINTH_OUT - .14, PLINTH_H), (-PLINTH_IN, PLINTH_H)]
    for start, along, nrm, length in plinth_runs():
        rings = []
        for u in (0.0, length):
            rings.append([bm.verts.new(start + along * u + nrm * d + Vector((0, 0, FLOOR - .02 + z))) for d, z in prof])
        for i in range(len(prof)):
            j = (i + 1) % len(prof)
            bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i]))
        bm.faces.new(rings[0]); bm.faces.new(list(reversed(rings[1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("Outer_wall_plinth"); bm.to_mesh(me); bm.free()
    me.materials.append(CLAY)
    ob = bpy.data.objects.new("Outer_wall_plinth", me); col.objects.link(ob)
    return ob


def column_mesh():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", "stone_column.glb"))
    new = [o for o in bpy.data.objects if o not in before]
    src = [o for o in new if o.type == "MESH"][0]
    me = src.data.copy(); me.transform(src.matrix_world)
    for o in new:
        bpy.data.objects.remove(o)
    me.materials.clear(); me.materials.append(CLAY)
    tmp = bpy.data.objects.new("_col", me); sc.collection.objects.link(tmp)
    bpy.context.view_layer.objects.active = tmp
    mod = tmp.modifiers.new("d", "DECIMATE"); mod.ratio = 0.15
    bpy.ops.object.modifier_apply(modifier=mod.name)
    sc.collection.objects.unlink(tmp)
    vs = [v.co for v in me.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
    me.transform(Matrix.Translation((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z)))
    return me, hi - lo


def column(name, x, y, foot, me, size):
    """Base below 18 % and capital above 82 % of the source keep their shape (scaled as the footprint); the shaft
    between stretches to fill the wall's height."""
    sx, sy = foot / size.x, foot / size.y
    s = (sx + sy) / 2                                # the base and capital's own height scale
    h = TOP + .6 - FLOOR
    zb, zc = .18 * size.z, .82 * size.z
    base_h, cap_h = zb * s, (size.z - zc) * s
    shaft = h - base_h - cap_h
    m = me.copy(); m.name = name
    for v in m.vertices:
        z = v.co.z
        if z <= zb:
            nz = z * s
        elif z >= zc:
            nz = base_h + shaft + (z - zc) * s
        else:
            nz = base_h + (z - zb) / (zc - zb) * shaft
        v.co = Vector((v.co.x * sx, v.co.y * sy, nz))
    ob = bpy.data.objects.new(name, m); ob.location = (x, y, FLOOR - .02); col.objects.link(ob)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    return ob


p = plinth()
me, size = column_mesh()
for name, x, y, foot in CORNERS:
    o = column(name, x, y, foot, me, size)
    print("COLUMN", name, len(o.data.polygons), "faces")
print("PLINTH", len(p.data.polygons), "faces", [round(r[3], 2) for r in plinth_runs()])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v16.blend"))
