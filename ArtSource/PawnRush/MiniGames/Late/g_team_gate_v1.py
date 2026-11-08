# G team exit gate (R504): the user's Tripo "Classical Marble Archway" (Source/R504_G_E1_TeamGate.fbx, 50k, from the
# asset photo Docs/PawnRush/ConceptArt/R504_RoyalProcession/Assets/G_E1_team_gate.png): the portal frame, the double
# door, the rook crest. Fitted to the game (MiniGameModels.ProcessionWalls / RoyalProcessionStations):
#
#   frame  the opening (0.164 x 0.297 in the source) scaled to the game's 4.5 m width (x27.4, uniform in x and up,
#          so the arch stays round: the opening is 8.1 m high, the walk-through height stays the game's door and
#          tunnel); cut to the column spacing (6.93 m, the wall columns cover the cut edges) and at the 12 m ceiling;
#          depth squeezed to 1.6 m (the old lintel's), so it does not stand over the last road square.
#          Origin = opening bottom centre, back face; Blender -y = front.
#   door   uniform x18.7: 4.5 m wide, 6 m high (its top hides under the tunnel's pocket roof when shut and in the
#          wall when risen). Origin bottom centre. Painted wood faces get their own slot "PR_Late_G_GatePaint",
#          which the game tints with the team colour; iron and brass stay on "PR_Late_G_Gate".
#   crest  2.2 m across, origin at its back centre; it hangs on the frame above the arch (its photo seat is above
#          the ceiling after the scale).
# Gold -> matte brass on the frame and crest (decorative). Smooth by angle (Tripo is flat-shaded).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python g_team_gate_v1.py -- [--export] [--render]
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_props_v1 as props

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SRC = "R504_G_E1_TeamGate.fbx"
OPEN_X0, OPEN_X1 = -.366, -.208            # opening edges (source), measured with rays
OPEN_W = OPEN_X1 - OPEN_X0
K = 4.5 / OPEN_W                           # ~28.5
FRAME_W, FRAME_H, FRAME_D = 6.93, 12., 1.6
TARGETS = {"G_TeamGateFrame": 10000, "G_TeamGateDoor": 6000, "G_TeamGateCrest": 3000}


def bisect(me, co, no):
    """Cut the mesh with a plane and keep the side the normal points away from."""
    bm = bmesh.new(); bm.from_mesh(me)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, clear_outer=True)
    bm.to_mesh(me); bm.free(); me.update()


def fit_frame(ob):
    me = ob.data
    zlo = min(v.co.z for v in me.vertices)
    ylo = min(v.co.y for v in me.vertices); yhi = max(v.co.y for v in me.vertices)
    cx = (OPEN_X0 + OPEN_X1) / 2
    sy = FRAME_D / (yhi - ylo)
    me.transform(Matrix.Diagonal((K, sy, K, 1.)) @ Matrix.Translation((-cx, -yhi, -zlo)))   # origin: opening, back, floor
    bisect(me, (FRAME_W / 2, 0, 0), (1, 0, 0))
    bisect(me, (-FRAME_W / 2, 0, 0), (-1, 0, 0))
    bisect(me, (0, 0, FRAME_H), (0, 0, 1))


def fit_uniform(ob, scale, origin):
    """origin: 'bottom' (bottom centre) or 'back' (centre of its back face)."""
    me = ob.data
    co = np.array([v.co[:] for v in me.vertices]); lo, hi = co.min(0), co.max(0)
    c = (lo + hi) / 2
    pivot = (c[0], c[1], lo[2]) if origin == "bottom" else (c[0], hi[1], c[2])
    me.transform(Matrix.Scale(scale, 4) @ Matrix.Translation([-p for p in pivot]))


def split_paint(ob, image):
    """Door: faces painted light wood -> slot 1 "PR_Late_G_GatePaint" (tinted by team in the game)."""
    small = image.copy(); small.scale(1024, 1024)
    px = np.array(small.pixels[:], np.float32).reshape(1024, 1024, 4)[..., :3]
    bpy.data.images.remove(small)
    hue, sat, val = props.hsv(px)
    metal = (val < .42) | ((np.abs(hue - 40) < 18) & (sat > .45))      # dark iron straps and studs, brass rings
    wood = ~metal
    me = ob.data
    paint = bpy.data.materials.get("PR_Late_G_GatePaint") or bpy.data.materials.new("PR_Late_G_GatePaint")
    if len(me.materials) < 2: me.materials.append(paint)
    uvl = me.uv_layers.active.data
    n = 0
    for p in me.polygons:
        u = sum(uvl[l].uv.x for l in p.loop_indices) / p.loop_total
        v = sum(uvl[l].uv.y for l in p.loop_indices) / p.loop_total
        is_wood = wood[min(1023, max(0, int(v * 1024))), min(1023, max(0, int(u * 1024)))]
        p.material_index = 1 if is_wood else 0
        n += int(is_wood)
    print("PAINT door: %d of %d faces team-painted wood" % (n, len(me.polygons)))


def main():
    src = props.load(SRC)
    co, lab, *_ = props.labels(src.data, 0)
    image = next(n.image for n in src.data.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE' and n.image.name.startswith("base"))
    made = {}
    for idx, name in ((0, "G_TeamGateFrame"), (1, "G_TeamGateDoor"), (2, "G_TeamGateCrest")):
        ob = props.cut(src, lab == idx, name)
        if name == "G_TeamGateFrame": fit_frame(ob)
        else:
            c = np.array([v.co[:] for v in ob.data.vertices]); size = c.max(0) - c.min(0)
            fit_uniform(ob, 4.5 / size[0] if name == "G_TeamGateDoor" else 2.2 / size[0], "bottom" if name == "G_TeamGateDoor" else "back")
            if name == "G_TeamGateDoor":      # Tripo made the leaves 1.2 m thick: squeeze to 0.35 m (the game's gate is 0.4)
                d = max(v.co.y for v in ob.data.vertices) - min(v.co.y for v in ob.data.vertices)
                ob.data.transform(Matrix.Diagonal((1., .35 / d, 1., 1.)))
        props.decimate(ob, TARGETS[name])
        c = np.array([v.co[:] for v in ob.data.vertices])
        print("OBJ %-16s tris %6d  lo %s  hi %s" % (name, props.tris(ob), np.round(c.min(0), 2), np.round(c.max(0), 2)))
        made[name] = ob
    # colour on the shared atlas: decorative gold of the frame and crest -> matte brass
    mat = made["G_TeamGateFrame"].data.materials[0]
    props.retexture("Gate", mat, [(made["G_TeamGateFrame"], None), (made["G_TeamGateCrest"], None)])
    split_paint(made["G_TeamGateDoor"], image)
    bpy.data.objects.remove(src)
    if "--export" in ARGS:
        for ob in made.values(): props.export(ob)
    if "--render" in ARGS:
        sc = bpy.context.scene
        for ob in made.values():
            if ob.name == "G_TeamGateCrest": ob.location = (0, -FRAME_D - .02, 10.4 - 1.1)
            if ob.name == "G_TeamGateDoor": ob.location = (0, -.8, 0)
        # wall columns either side, as in the game
        for x in (-FRAME_W / 2, FRAME_W / 2):
            bpy.ops.mesh.primitive_cylinder_add(radius=1.0, depth=12, location=(x, -1.2, 6))
        w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = .9; sc.world = w
        bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(20)))
        cd = bpy.data.cameras.new("c"); cd.lens = 28; cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
        cam.location = (-5, -16, 5); cam.rotation_euler = (Vector((0, 0, 5.5)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1200, 1200
        sc.render.filepath = os.path.join(HERE, "render_g_team_gate.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
