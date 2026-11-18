"""Preview of the giant's acting (R6 "거인 시스템", 11-18): the same pose maths as Assets/Scripts/RagdollLab/GiantGrandmaster.cs
(lean and turn, head look, two-bone arm IK with the clavicle share, finger curl, the mood and reaction targets), run on
the Blender rig, frozen at full strength, so the poses can be judged before Unity.

    blender -b rl_giant_a.blend --python rl_giant_acting_stills.py -- <out folder>

Writes giant_acting_moods.png (rest, think, study, look around) and giant_acting_reactions.png (pleased, startled,
win, lose), each from the front, three-quarter and side. Keep the numbers in step with GiantGrandmaster.cs.
"""
import math, os, sys
import bpy
from mathutils import Vector, Matrix, Quaternion
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rl_giant_model as g

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else g.STILLS
rig = bpy.data.objects["GiantRig"]
UP = Vector((0, 0, 1))
CLAVICLE, MAX_LEAN, MAX_TURN, MAX_HEAD = .3, 50, 40, 65
HEAD_R = 8.73


def W(b):
    return rig.matrix_world @ rig.pose.bones[b].matrix


def P(b):
    return W(b).translation.copy()


def rotate(b, q):
    """Unity's transform.rotation = q * transform.rotation: turn a bone (and its children) about its head."""
    M = W(b); T = Matrix.Translation(M.translation)
    rig.pose.bones[b].matrix = rig.matrix_world.inverted() @ (T @ q.to_matrix().to_4x4() @ T.inverted() @ M)
    bpy.context.view_layer.update()


def angle_axis(deg, axis):
    return Quaternion(Vector(axis).normalized(), math.radians(deg))


def from_to(a, b):
    return Vector(a).rotation_difference(Vector(b))


def slerp_id(q, t):
    return Quaternion().slerp(q, t)


def flat(v):
    return Vector((v.x, v.y, 0))


def reset():
    for b in rig.pose.bones:
        b.rotation_mode = "QUATERNION"; b.rotation_quaternion = (1, 0, 0, 0); b.location = (0, 0, 0)
    bpy.context.view_layer.update()


reset()
REST_FWD = flat(P("shin.L") - P("thigh.L")).normalized()
GRIP_LOCAL, UPPER_LEN, LOWER_LEN = {}, {}, {}
for t in "LR":
    gp = P("hand." + t).lerp(P("fingers." + t), .8) - UP * 1.4
    GRIP_LOCAL[t] = W("hand." + t).inverted() @ gp
    UPPER_LEN[t] = (P("upperarm." + t) - P("forearm." + t)).length
    LOWER_LEN[t] = (P("forearm." + t) - gp).length
HEAD_CENTRE_LOCAL = W("head").inverted() @ (P("head") + UP * 8.4 + REST_FWD * .9)
HEAD_FWD_LOCAL = W("head").to_3x3().inverted() @ REST_FWD
HEAD_UP_LOCAL = W("head").to_3x3().inverted() @ UP


def grip(t):
    return W("hand." + t) @ GRIP_LOCAL[t]


def apply_lean(axis, lean, yaw):
    rotate("spine", angle_axis(lean * .45, axis) @ angle_axis(yaw * .4, UP))
    rotate("chest", angle_axis(lean * .55, axis) @ angle_axis(yaw * .6, UP))


def solve_arm(t, target, weight, pole):
    s0 = P("upperarm." + t); rest_upper = P("forearm." + t) - s0
    goal0 = grip(t).lerp(target, weight)
    rotate("shoulder." + t, slerp_id(from_to(rest_upper, goal0 - s0), CLAVICLE * weight))
    s = P("upperarm." + t); goal = grip(t).lerp(target, weight)
    a, b = UPPER_LEN[t], LOWER_LEN[t]
    to_goal = goal - s
    dd = min(max(to_goal.length, abs(a - b) + .01), a + b - .01)
    n = to_goal.normalized()
    perp = pole - n * pole.dot(n); perp = perp.normalized() if perp.length > 1e-3 else -UP
    x = (a * a - b * b + dd * dd) / (2 * dd); h = math.sqrt(max(a * a - x * x, 0))
    elbow = s + n * x + perp * h
    rotate("upperarm." + t, from_to(P("forearm." + t) - s, elbow - s))
    e = P("forearm." + t)
    rotate("forearm." + t, from_to(grip(t) - e, s + n * dd - e))


def curl(t, amount):
    f = P("fingers." + t) - P("hand." + t)
    lat = f.cross(UP)
    if lat.length > 1e-3:
        rotate("fingers." + t, angle_axis(-60 * amount, lat))
    M = W("thumb." + t); tdir = M.to_3x3() @ Vector((0, 1, 0))
    close = tdir.cross(f)
    if close.length > 1e-3:
        rotate("thumb." + t, angle_axis(30 * amount, close))


def act(think=0, study=0, chin_arm="R", pleased=0, startled=0, win=0, lose=0, rt=.4, look=None):
    """One frozen frame of GiantGrandmaster.Tick (no reach)."""
    reset()
    acting = max(pleased, startled, win, lose)
    idle_lean = think * 16 + study * 20
    mood_lean = idle_lean * (1 - acting) + pleased * -5 + startled * -12 + win * -9 + lose * 18
    d = REST_FWD; yaw = 0
    axis = UP.cross(d).normalized()
    lean = mood_lean
    apply_lean(axis, lean, yaw)
    face = angle_axis(lean, axis) @ REST_FWD
    look = look if look is not None else P("head") + REST_FWD * 60 + Vector((0, 0, -30))
    to_look = (look - P("head")).normalized()
    ang = math.degrees(face.angle(to_look))
    turn = min(ang, MAX_HEAD) / max(ang, .01)
    rotate("head", slerp_id(from_to(face, to_look), turn * .75 * (1 - acting * .5)))
    side = UP.cross(face).normalized()
    nod = pleased * math.sin(rt * 11) * 7 + think * 6 - startled * 12 - win * 14 + lose * 22
    rotate("head", angle_axis(nod, side) @ angle_axis(think * (7 if chin_arm == "L" else -7), face))
    fwd = face
    hc = W("head") @ HEAD_CENTRE_LOCAL
    hf = W("head").to_3x3() @ HEAD_FWD_LOCAL; hu = W("head").to_3x3() @ HEAD_UP_LOCAL
    chin = hc + (hf * .62 - hu * .78).normalized() * HEAD_R
    brow = hc + (hf * .9 + hu * .3).normalized() * HEAD_R
    for i, t in enumerate("LR"):
        outward = flat(P("upperarm." + t) - P("chest")).normalized()
        acc, wsum, csum = Vector(), 0.0, 0.0

        def add(p, w, c):
            nonlocal acc, wsum, csum
            if w > 0:
                acc += p * w; wsum += w; csum += c * w
        chin_w = think * (1 if t == chin_arm else 0) * (1 - acting)
        add(chin + hf * 1.6 - hu * 2.4, chin_w, 1)
        add(P("shin." + t) + UP * 4.9 - fwd * 1.5, study * .85 * (1 - acting), .35)
        front = P("chest") + fwd * 8
        add(front - UP * 3 + outward * (1.4 + math.sin(rt * 14 + i * 3) * .6), pleased, .3)
        add(P("upperarm." + t) + fwd * 6 + UP * 3.5 + outward * 2, startled * .85, 0)
        add(P("upperarm." + t) + UP * (18 + math.sin(rt * 7) * 1.5) + outward * 7, win, 1)
        add(brow + hf * 2.2 - hu * .4, lose * (1 if t == "R" else 0), 0)
        if wsum > .002:
            pole = (outward * .7 - UP * .7 - d * .2).lerp(fwd * .5 - UP * .85 + outward * .2, chin_w / wsum)
            solve_arm(t, acc / wsum, min(wsum, 1), pole)
            if csum / wsum > .002:
                curl(t, csum / wsum)


def sheet(name, frames):
    rows = []
    for label, kw in frames:
        act(**kw)
        sc, cam = g.setup_render(420, 520)
        paths = []
        for view, dv in (("front", Vector((0, -1, .1))), ("tq", Vector((-.8, -1, .35))), ("side", Vector((-1, -.15, .05)))):
            centre, bw, bh = g.bounds_in_view(dv)
            cam.data.ortho_scale = max(bw * 1.1 * 520 / 420, bh * 1.06)
            cam.location = centre + dv.normalized() * 400
            cam.rotation_euler = (-dv).to_track_quat("-Z", "Y").to_euler()
            sc.render.filepath = os.path.join(OUT, "_act_%s_%s.png" % (label, view))
            bpy.ops.render.render(write_still=True); paths.append(sc.render.filepath)
        rows.append(paths)
    out = g.sheet(rows, os.path.join(OUT, name))
    for r in rows:
        for f in r:
            os.remove(f)
    print("[rl_giant] acting", out)


board = P("head") + REST_FWD * 70 + Vector((0, 0, -38))
sheet("giant_acting_moods.png", [("rest", {}), ("think", {"think": 1}), ("study", {"study": 1}),
                                 ("look", {"look": P("head") + Vector((-40, -50, -20))})])
sheet("giant_acting_reactions.png", [("pleased", {"pleased": 1}), ("startled", {"startled": 1}), ("win", {"win": 1}),
                                     ("lose", {"lose": 1})])
