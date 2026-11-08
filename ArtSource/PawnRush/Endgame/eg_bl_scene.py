"""Assemble a layout (eg_layout) in Blender: per-beat static meshes, rails from the kit, device roots with their
skins (poses from the shared-clock functions), signals and the hall. Records every placement for the manifest."""
import math

import bmesh
import bpy
from mathutils import Vector

import eg_bl_kit as K
import eg_bl_util as B
import eg_layout as E

PLACED = []      # manifest rows: dict(name, fbx, position, euler, scale, role, skins, pivot, attach)
FLOOR_INLAY = 4.0


def record(obj, part, role, skins="", pivot="", attach="", unity_euler=None):
    # matrix_world is only brought up to date by a depsgraph update, which has not run for an object just placed:
    # reading it here wrote every static and decor row at (0, 0, 0) (R255, found in Unity).
    if obj.parent is None:
        loc = obj.location.copy()
    else:
        loc = (obj.parent.matrix_world @ obj.matrix_parent_inverse @ obj.matrix_basis).to_translation()
    eul = unity_euler if unity_euler is not None else [0.0, round(-math.degrees(obj.rotation_euler.z), 3), 0.0]
    PLACED.append(dict(name=obj.name, fbx=f"{part}/{part}.fbx", position=B.unity_pos(loc), euler=eul,
                       scale=[round(obj.scale.x, 4), round(obj.scale.z, 4), round(obj.scale.y, 4)], role=role,
                       skins=skins, pivot=pivot, attach=attach))


# ---------------------------------------------------------------------------------------------- static course
def beat_floor_mesh(L, beat, coll):
    """All walked floors of one beat: marble top 0.15 m, teal fascia, a gold band under the lip, gold inlay lines
    every 4 m across (world-aligned, like the gray box)."""
    floors = [f for f in L.floors if f["beat"] == beat]
    if not floors:
        return None
    bm = bmesh.new()
    for f in floors:
        x0, x1, z0, z1, top, bot = f["x0"], f["x1"], f["z0"], f["z1"], f["top"], f["bottom"]
        B.box_bm(bm, x0, x1, z0, z1, top - 0.15, top, bevel=0.025, segments=2, mat=0)
        B.box_bm(bm, x0 + 0.02, x1 - 0.02, z0 + 0.02, z1 - 0.02, bot, top - 0.15, bevel=0.02, segments=1, mat=1)
        B.box_bm(bm, x0 - 0.005, x1 + 0.005, z0 - 0.005, z1 + 0.005, top - 0.24, top - 0.17, bevel=0.0, mat=2)
        z = math.ceil(z0 / FLOOR_INLAY) * FLOOR_INLAY
        while z < z1 - 0.1:
            if z > z0 + 0.1:
                B.box_bm(bm, x0 + 0.3, x1 - 0.3, z - 0.06, z + 0.06, top, top + 0.012, mat=2)
            z += FLOOR_INLAY
    o = B.mesh_object(f"PR_EG_Floor_{beat}", bm, [K.M["EG_Marble_Cream"], K.M["EG_Teal"], K.M["EG_Gold"]], coll)
    B.weighted_normals(o)
    record(o, o.name, "static", pivot="world origin (Unity 0,0,0); vertices in course coordinates")
    return o


def beat_wall_mesh(L, beat, coll):
    walls = [w for w in L.walls if w["beat"] == beat and "map table" not in w["name"] and "crate" not in w["name"]
             and "frame" not in w["name"]]
    decor = [d for d in L.decor if d["beat"] == beat]
    if not walls and not decor:
        return None
    names = ["EG_Stone", "EG_Teal", "EG_Gold", "EG_Walnut", "EG_Marble_Cream"]
    bm = bmesh.new()
    for w in walls + decor:
        mi = names.index(w["mat"]) if w["mat"] in names else 0
        B.box_bm(bm, w["x0"], w["x1"], w["z0"], w["z1"], w["y0"], w["y1"], bevel=0.03, segments=2, mat=mi)
    o = B.mesh_object(f"PR_EG_Walls_{beat}", bm, [K.M[n] for n in names], coll)
    B.weighted_normals(o)
    record(o, o.name, "static", pivot="world origin; vertices in course coordinates")
    return o


def rails(L, kit, coll):
    """Rails from 4/2/1/0.5 m kit segments, a post at every run end and every 8 m."""
    pieces = [(4.0, kit["rail4"]), (2.0, kit["rail2"]), (1.0, kit["rail1"]), (0.5, kit["rail05"])]
    for r in L.rails:
        x = (r["x0"] + r["x1"]) / 2
        z, z1, y = r["z0"], r["z1"], r["floor"]
        posts = [z + 0.42, z1 - 0.42] if z1 - z > 1.0 else []
        k = 0
        while z < z1 - 0.01:
            left = z1 - z
            for ln, src in pieces:
                if left >= ln - 0.01 or ln == 0.5:
                    seg = min(ln, left)
                    o = B.instance(src, f"{r['name']} {k}", coll, B.U(x, y, z + seg / 2), 0.0,
                                   (1, seg / ln, 1))
                    record(o, src.name, "static", pivot="bottom centre of the segment")
                    z += seg
                    k += 1
                    if k % 2 == 0 and r["z0"] + 1.0 < z < z1 - 1.0:
                        posts.append(z)
                    break
        for pz in sorted(set(round(p, 2) for p in posts)):
            o = B.instance(kit["post"], f"{r['name']} post {pz}", coll, B.U(x, y, pz))
            record(o, kit["post"].name, "static", pivot="bottom centre")


# ---------------------------------------------------------------------------------------------- devices
class Rig:
    """A device root (an Empty named like the Unity root) with skin parts as children, posed by time."""

    def __init__(self, d, coll):
        self.d = d
        px, py, pz = d["pos"]
        self.root = B.empty(f"Device_{d['id']}", coll, B.U(px, py, pz), size=1.0, kind="ARROWS")
        self.root.rotation_euler = (0, 0, B.yaw_to_rz(d.get("yaw", 0)))
        self.parts = []

    def part(self, src, name, local=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), coll=None):
        o = B.instance(src, name, coll or self.root.users_collection[0], Vector(local), 0.0, scale)
        o.rotation_euler = rot
        o.parent = self.root
        self.parts.append(o)
        return o


def build_devices(L, kit, colls, t=0.0):
    rigs = []
    for d in L.devices:
        coll = colls.get(d["beat"]) or colls["EG_Devices"]
        k = d["type"]
        r = Rig(d, coll)
        if k == "board":
            r.part(kit["war_table"], f"{d['id']} skin", (0, 0, 0))
            r.root["eg_pivot"] = "top centre"
        elif k == "spinbar":
            r.root.location.z += 0.65
            r.part(kit["sweep_bar"], f"{d['id']} skin")
        elif k == "ferry":
            r.part(kit["ferry"], f"{d['id']} skin")
        elif k == "hurdle":
            r.root.location.z += (d["y0"] + d["y1"]) / 2
            r.part(kit["hurdle"], f"{d['id']} skin")
        elif k == "sliding":
            for side in (-1, 1):
                r.part(kit["shelf_wall"], f"{d['id']} {'L' if side < 0 else 'R'} skin", (side * d["rest"], side * 1.8, 1.5))
                h = r.part(kit["shelf_housing"], f"{d['id']} {'L' if side < 0 else 'R'} housing", (side * 6.0, side * 1.8, 0.0),
                           rot=(0, 0, 0 if side > 0 else math.pi))
        elif k == "clockgates":
            r.part(kit["gate_frame"], f"{d['id']} frame")
            for side in (-1, 1):
                r.part(kit["gate_door"], f"{d['id']} {'L' if side < 0 else 'R'} door", (side * 2.08, 0, 1.6))
        elif k == "rookscreen":
            r.root.location.z += 1.6
            r.part(kit["rook_screen"], f"{d['id']} skin")
        elif k == "pendulum":
            r.part(kit["gantry"], f"{d['id']} gantry")
            r.part(kit["pendulum"], f"{d['id']} weight", (0, 0, 7.3))
        elif k == "lifttiles":
            for ix, sx in enumerate((-1, 1)):
                for iz, sz in enumerate((-1, 1)):
                    r.part(kit["chest_tile"], f"{d['id']} tile {ix}-{iz}", (sx * 1.575, sz * 1.575, -0.2))
            # pit rims and the shaft (looks only)
            r.part(kit["lift_shaft"], f"{d['id']} shaft")
        elif k == "vent":
            r.part(kit["vent"], f"{d['id']} skin")
        elif k == "conveyor":
            scale_y = d["length"] / 10.0
            r.part(kit["belt"], f"{d['id']} skin", scale=(1, scale_y, 1))
        elif k == "fallingpiece":
            # the ring is cut at the floor edge: turned so the cut faces the nearer edge
            r.part(kit["crown_circle"], f"{d['id']} warning ring", rot=(0, 0, math.pi if d["pos"][0] < 0 else 0.0))
            r.part(kit["pawn"], f"{d['id']} piece", (0, 0, 9.0))
        elif k == "crate":
            r.part(kit["crate"], f"{d['id']} skin")
        elif k == "disc":
            r.part(kit["disc"], f"{d['id']} skin")
        elif k == "foldingbridge":
            for side in (-1, 1):
                r.part(kit["bridge_landing"], f"{d['id']} landing {'L' if side < 0 else 'R'}", (side * 5.55, 0, 0))
            r.part(kit["bridge_panel"], f"{d['id']} panel L", (-4.0, 0, 0))
            r.part(kit["bridge_panel"], f"{d['id']} panel R", (4.0, 0, 0), rot=(0, 0, math.pi))
        rigs.append(r)
    pose(rigs, t)
    return rigs


def pose(rigs, t):
    """Put every moving part where the shared clock says it is at time t (seconds)."""
    for r in rigs:
        d, k = r.d, r.d["type"]
        tm = d.get("timing")
        if k in ("board", "spinbar"):
            r.parts[0].rotation_euler.z = math.radians(-tm.value(t))
        elif k in ("ferry", "crate"):
            r.parts[0].location.y = tm.value(t)
        elif k == "rookscreen":
            r.parts[0].location.x = tm.value(t)
        elif k == "sliding":
            base = E.Motion("gate", distance=d["travel"], period=4.5).shift(d.get("phase_s", 0.0))
            for i, side in enumerate((-1, 1)):
                m = E.Motion("gate", distance=d["travel"], period=4.5, phase=E.repeat(base.phase + (0.0 if side < 0 else 0.5), 1.0))
                r.parts[2 * i].location.x = side * (d["rest"] - m.value(t))
        elif k == "clockgates":
            for i, (side, ph) in enumerate(((-1, 0.0), (1, 0.5))):
                m = E.Motion("gate", distance=4.1, period=5.0, phase=ph).shift(d.get("phase_s", 0.0))
                r.parts[1 + i].location.z = 1.6 + m.value(t)
        elif k == "pendulum":
            r.parts[1].rotation_euler.y = math.radians(-tm.angle(t))
        elif k == "lifttiles":
            for p in r.parts[:4]:
                ix, iz = p.name.split()[-1].split("-")
                ph = 0.0 if ix == iz else 0.5
                m = E.Motion("translate", distance=3.0, period=6.0, phase=ph).shift(d.get("phase_s", 0.0))
                p.location.z = -0.2 + m.value(t)
        elif k == "fallingpiece":
            r.parts[1].location.z = tm.piece_height(t)
        elif k == "foldingbridge":
            _, ang = tm.state(t)
            r.parts[2].rotation_euler.y = math.radians(-ang)
            r.parts[3].rotation_euler.y = math.radians(-ang)


def lift_shaft_kit():
    """Pit rims (prefab 05: 6.7 x 0.3 x 0.2 at +-3.25) and a walnut shaft under them, 3.6 m deep (looks only)."""
    bm = bmesh.new()
    for s in (-1, 1):
        B.box_bm(bm, -3.35, 3.35, s * 3.25 - 0.1, s * 3.25 + 0.1, -0.3, 0.0, bevel=0.02, mat=1)
        B.box_bm(bm, s * 3.25 - 0.1, s * 3.25 + 0.1, -3.35, 3.35, -0.3, 0.0, bevel=0.02, mat=1)
        B.box_bm(bm, -3.35, 3.35, s * 3.25 - 0.1, s * 3.25 + 0.1, -3.9, -0.3, mat=0)
        B.box_bm(bm, s * 3.25 - 0.1, s * 3.25 + 0.1, -3.35, 3.35, -3.9, -0.3, mat=0)
    o = B.mesh_object("PR_EG_LiftShaft", bm, [K.M["EG_Walnut"], K.M["EG_Gold"]], K.KIT)
    return K.finish(o)


# ---------------------------------------------------------------------------------------------- signals and dressing
def signals(L, kit, coll):
    for w in L.walls:
        if "map table" in w["name"]:
            length = w["z1"] - w["z0"]
            o = B.instance(kit["map_table"], w["name"], coll, B.U((w["x0"] + w["x1"]) / 2, w["y0"], (w["z0"] + w["z1"]) / 2),
                           0.0, (1, length / 6.0, 1))
            record(o, kit["map_table"].name, "static", pivot="bottom centre (the table foot, 2.3 m under its top)")
    s3 = [b for b in L.beats if b["id"] == "S3"]
    if s3:
        o = B.instance(kit["compass"], "S3 compass", coll, B.U(0, 0, getattr(L, "compass_z", 100.0)))
        record(o, kit["compass"].name, "decor", pivot="centre on the floor")
    for b in L.beats:
        if b["id"] in ("S1", "R2", "S3", "R4", "R5", "R6", "EX"):
            y = b["y"]
            o = B.instance(kit["arrow"], f"{b['id']} arrow sign", coll, B.U(6.25, y + 1.32, b["z0"] + 1.5), math.radians(180))
            record(o, kit["arrow"].name, "decor", pivot="foot of the post (stands on the rail cap)")
    ex = getattr(L, "exit", None)
    if ex:
        for sx in (-1, 1):
            o = B.instance(kit["rook"], f"Rook statue {'W' if sx < 0 else 'E'}", coll, B.U(sx * 9.0, ex["y"] - 1.0, ex["arch"] + 3.0))
            record(o, kit["rook"].name, "decor", pivot="base centre")
            o = B.instance(kit["plinth"], f"Rook plinth {'W' if sx < 0 else 'E'}", coll, B.U(sx * 9.0, ex["y"] - 1.0, ex["arch"] + 3.0))
            record(o, kit["plinth"].name, "decor", pivot="top centre")
        o = B.instance(kit["arch_crest"], "Exit arch crest", coll, B.U(0, ex["y"] + 6.5, ex["arch"]))
        record(o, kit["arch_crest"].name, "decor", pivot="bottom centre (on the lintel)")


def plinth_kit():
    bm = bmesh.new()
    B.box_bm(bm, -1.6, 1.6, -1.6, 1.6, -18.0, 0.0, bevel=0.05, mat=0)
    B.box_bm(bm, -1.7, 1.7, -1.7, 1.7, -0.3, 0.0, bevel=0.03, mat=1)
    return K.finish(B.mesh_object("PR_EG_Plinth", bm, [K.M["EG_Stone"], K.M["EG_Gold"]], K.KIT))


def arch_crest_kit():
    """The gold coat-of-arms over the exit arch: a half-round crest with a rook between two crowns (looks only)."""
    bm = bmesh.new()
    seg = 32
    outline = [(-7.0, 0.0), (7.0, 0.0)]
    for i in range(1, seg):
        a = math.pi * i / seg
        outline.append((7.0 * math.cos(a), 2.6 * math.sin(a)))
    v = B.prism_bm(bm, outline, -0.3, 0.3, mat=0)
    from mathutils import Matrix
    bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    shield = [(-1.1, 0.3), (1.1, 0.3), (1.1, 1.9), (0.0, 2.5), (-1.1, 1.9)]
    v = B.prism_bm(bm, shield, -0.42, 0.42, mat=1)
    bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    for x in (-3.6, 3.6):
        crown = [(x - 0.8, 0.5), (x + 0.8, 0.5), (x + 0.8, 1.3), (x + 0.4, 1.0), (x, 1.5), (x - 0.4, 1.0), (x - 0.8, 1.3)]
        v = B.prism_bm(bm, crown, -0.4, 0.4, mat=0)
        bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    return K.finish(B.mesh_object("PR_EG_ArchCrest", bm, [K.M["EG_Gold"], K.M["EG_Teal"]], K.KIT))


def hall(L, kit, coll, z0=-12.0, z1=None, low=-30.0, high=18.0, side=26.0):
    """The hall round the course (looks only, no colliders): giant chessboard floor far below, stone pillars every
    12 m with lamps, bookshelf walls with a gallery, arched windows, banners, beams and chandeliers."""
    if z1 is None:
        z1 = max(b["z1"] for b in L.beats) + 30.0
    rng = __import__("random").Random(7)
    bay = 0
    z = z0
    shelves = kit["shelves"]
    while z < z1:
        for sx in (-1, 1):
            x = sx * side
            rz = 0.0 if sx < 0 else math.pi
            o = B.instance(kit["pillar"], f"Hall pillar {bay}{'W' if sx < 0 else 'E'}", coll, B.U(x, low, z))
            record(o, kit["pillar"].name, "decor", pivot="base centre")
            o = B.instance(kit["lamp"], f"Hall lamp {bay}{'W' if sx < 0 else 'E'}", coll, B.U(x - sx * 1.0, 7.0, z),
                           math.radians(90 if sx < 0 else -90))
            o.rotation_euler.z = -math.pi / 2 if sx < 0 else math.pi / 2
            record(o, kit["lamp"].name, "decor", pivot="wall end of the bracket")
            wall = B.instance(kit["wall_panel"], f"Hall wall {bay}{'W' if sx < 0 else 'E'}", coll,
                              B.U(sx * (side + 2.0), low, z + 6.0), math.radians(90) if sx < 0 else math.radians(-90))
            record(wall, kit["wall_panel"].name, "decor", pivot="bottom centre of the inner face")
            mid = z + 6.0
            face = sx * (side + 1.5)
            if bay % 2 == 0:
                # a library bay: 3 x 6 shelf units, a gallery at 8 m, a ladder
                for row in range(6):
                    for col in range(3):
                        src = shelves[(bay * 7 + row * 3 + col + (0 if sx < 0 else 5)) % len(shelves)]
                        o = B.instance(src, f"Hall shelf {bay}{'W' if sx < 0 else 'E'} {row}-{col}", coll,
                                       B.U(face, -10.0 + row * 3.0, mid + (col - 1) * 2.4))
                        # the books face -y in the unit: turn them toward the course
                        o.rotation_euler.z = math.pi / 2 if sx < 0 else -math.pi / 2
                        record(o, src.name, "decor", pivot="bottom centre")
                o = B.instance(kit["balcony"], f"Hall gallery {bay}{'W' if sx < 0 else 'E'}", coll, B.U(face - sx * 0.3, 8.0, mid),
                               0.0)
                o.rotation_euler.z = 0.0 if sx < 0 else math.pi
                record(o, kit["balcony"].name, "decor", pivot="wall edge, deck top")
                if rng.random() < 0.6:
                    o = B.instance(kit["ladder"], f"Hall ladder {bay}{'W' if sx < 0 else 'E'}", coll,
                                   B.U(face - sx * 1.4, -4.0, mid + rng.uniform(-2.5, 2.5)))
                    o.rotation_euler = (0, math.radians(-8 * sx), 0)
                    record(o, kit["ladder"].name, "decor", pivot="foot centre")
            else:
                o = B.instance(kit["window"], f"Hall window {bay}{'W' if sx < 0 else 'E'}", coll, B.U(face + sx * 0.2, 2.0, mid))
                o.rotation_euler.z = -math.pi / 2 if sx < 0 else math.pi / 2
                record(o, kit["window"].name, "decor", pivot="sill centre")
                o = B.instance(kit["banner"], f"Hall banner {bay}{'W' if sx < 0 else 'E'}", coll,
                               B.U(face - sx * 1.2, 16.5, mid + (3.6 if sx < 0 else -3.6)))
                o.rotation_euler.z = -math.pi / 2 if sx < 0 else math.pi / 2
                record(o, kit["banner"].name, "decor", pivot="top of the rod")
        o = B.instance(kit["beam"], f"Hall beam {bay}", coll, B.U(0, 23.0, z))
        record(o, kit["beam"].name, "decor", pivot="top centre")
        if bay % 2 == 1:
            for sx in (-1, 1):
                o = B.instance(kit["chandelier"], f"Hall chandelier {bay}{'W' if sx < 0 else 'E'}", coll, B.U(sx * 14.0, 10.0, z + 6.0))
                record(o, kit["chandelier"].name, "decor", pivot="lowest ring centre")
        for ix in range(-2, 2):
            o = B.instance(kit["floor_tile"], f"Hall floor {bay} {ix}", coll, B.U(ix * 12.0 + 6.0, low, z + 6.0))
            record(o, kit["floor_tile"].name, "decor", pivot="top centre")
        z += 12.0
        bay += 1
