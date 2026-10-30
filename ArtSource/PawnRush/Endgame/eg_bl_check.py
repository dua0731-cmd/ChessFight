"""Checks on the built scene (brief §9.10):
  (a) interference: every moving part sampled over one cycle every 0.05 s, its skin shrunk by 2 cm, against the
      static course and the other devices (BVH overlap);
  (b) path: required gaps, ledges, walls, rails, widths, unrailed edges, checkpoints clear of hazards;
  (c) safe windows (eg_timing);
  (d) budget: triangles per asset and per review camera, materials, textures.
"""
import math

import bpy
import numpy as np
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

import eg_bl_scene as S
import eg_layout as E
import eg_timing as T

SHRINK = 0.02
STEP = 0.05

# which parts of a rig move (indices into Rig.parts); the rest are static and join the course
MOVING = {"board": [0], "spinbar": [0], "ferry": [0], "crate": [0], "rookscreen": [0], "sliding": [0, 2],
          "clockgates": [1, 2], "pendulum": [1], "lifttiles": [0, 1, 2, 3], "fallingpiece": [1],
          "foldingbridge": [2, 3]}
NOT_SOLID = ("warning ring", "compass", "arrow sign")


def _mesh_arrays(o, shrink=0.0):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg)
    me = ev.to_mesh()
    me.calc_loop_triangles()
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(n, 3)
    if shrink:
        nor = np.empty(n * 3)
        me.vertices.foreach_get("normal", nor)
        co = co - nor.reshape(n, 3) * shrink
    tris = np.empty(len(me.loop_triangles) * 3, dtype=np.int32)
    me.loop_triangles.foreach_get("vertices", tris)
    ev.to_mesh_clear()
    return co, tris.reshape(-1, 3)


def _world(co, mat):
    m = np.array(mat)
    return co @ m[:3, :3].T + m[:3, 3]


class Shape:
    """A part's local mesh (shrunk), transformed per sample."""

    def __init__(self, o, shrink):
        self.o = o
        self.co, self.tris = _mesh_arrays(o, shrink)

    def bvh(self):
        w = _world(self.co, self.o.matrix_world)
        return BVHTree.FromPolygons(w.tolist(), self.tris.tolist(), all_triangles=True), w


def static_bvh(objs):
    verts, tris, owner = [], [], []
    base = 0
    for o in objs:
        co, tr = _mesh_arrays(o)
        w = _world(co, o.matrix_world)
        verts.append(w)
        tris.append(tr + base)
        owner += [o.name] * len(tr)
        base += len(w)
    if not verts:
        return None, []
    v = np.concatenate(verts)
    t = np.concatenate(tris)
    return BVHTree.FromPolygons(v.tolist(), t.tolist(), all_triangles=True), owner


def cycle_of(r):
    d, k = r.d, r.d["type"]
    tm = d.get("timing")
    if k == "sliding":
        return 4.5
    if k == "clockgates":
        return 5.0
    if k == "lifttiles":
        return 6.0
    if tm is None:
        return 0.0
    return tm.cycle()


def interference(rigs, static_objs):
    bpy.context.view_layer.update()
    sbvh, owner = static_bvh([o for o in static_objs if not any(s in o.name for s in NOT_SOLID)])
    results = []
    movers = []
    for r in rigs:
        idx = MOVING.get(r.d["type"], [])
        for i in idx:
            movers.append((r, r.parts[i]))
    shapes = {p.name: Shape(p, SHRINK) for _, p in movers}
    rig_static = {r.d["id"]: [p for i, p in enumerate(r.parts) if i not in MOVING.get(r.d["type"], [])] for r in rigs}
    for r in rigs:
        idx = MOVING.get(r.d["type"], [])
        if not idx:
            continue
        period = cycle_of(r)
        n = max(1, int(round(period / STEP)))
        own_static = [p for p in rig_static[r.d["id"]] if not any(s in p.name for s in NOT_SOLID)]
        own_bvh = static_bvh(own_static)[0] if own_static and r.d["type"] != "clockgates" else None
        same_beat = [(rr, p) for rr, p in movers if rr is not r and rr.d["beat"] == r.d["beat"]]
        hits, worst_t = {}, {}
        for s in range(n):
            t = s * STEP
            S.pose(rigs, t)
            bpy.context.view_layer.update()
            for i in idx:
                part = r.parts[i]
                b, _ = shapes[part.name].bvh()
                own_names = {p.name for p in r.parts}
                for a, _b in b.overlap(sbvh):
                    key = owner[_b]
                    if key in own_names:
                        continue
                    hits[key] = hits.get(key, 0) + 1
                    worst_t.setdefault(key, round(t, 2))
                if own_bvh is not None:
                    if b.overlap(own_bvh):
                        key = f"{r.d['id']} own static part"
                        hits[key] = hits.get(key, 0) + 1
                        worst_t.setdefault(key, round(t, 2))
                for rr, p in same_beat:
                    b2, _ = shapes[p.name].bvh()
                    if b.overlap(b2):
                        key = f"{p.name}"
                        hits[key] = hits.get(key, 0) + 1
                        worst_t.setdefault(key, round(t, 2))
        results.append(dict(device=r.d["id"], period=period, samples=n,
                            hits=[dict(with_=k, samples=v, first_t=worst_t[k]) for k, v in sorted(hits.items())]))
    S.pose(rigs, 0.0)
    return results


# ---------------------------------------------------------------------------------------------- (b) path
def path_check(L):
    out = dict(gaps=[], ledges=[], rails=[], widths=[], unrailed=[], checkpoints=[], notes=[])
    floors = sorted(L.floors, key=lambda f: (f["z0"], f["x0"]))
    # static floor-to-floor gaps along the way (same level or a step)
    for a in floors:
        nxt = [b for b in floors if b is not a and b["z0"] >= a["z1"] - 0.01 and b["z0"] - a["z1"] < 3.0
               and min(a["x1"], b["x1"]) - max(a["x0"], b["x0"]) > 1.0]
        if nxt:
            first = min(b["z0"] for b in nxt)
            nxt = [b for b in nxt if b["z0"] <= first + 0.01]
        for b in nxt:
            gap = round(b["z0"] - a["z1"], 2)
            step = round(b["top"] - a["top"], 2)
            if gap > 0.01:
                out["gaps"].append(dict(frm=a["name"], to=b["name"], gap=gap, ok=gap <= E.GAP_MAX))
            if abs(step) > 0.01:
                out["ledges"].append(dict(frm=a["name"], to=b["name"], step=step,
                                          ok=step <= E.STEP_UP or step < 0 or step <= E.LEDGE_SURE))
    # devices that carry the way: their gaps at the windows
    res = T.run_all(L.name)
    s2 = res["S2"]
    out["gaps"].append(dict(frm="S2 floor", to="ferry deck", gap=f"0.2 at the ends; <= 1.5 for {s2['board']['longest']} s a cycle",
                            ok=s2["min_gap"] <= E.GAP_MAX))
    if res["R5b"]:
        for h in res["R5b"]["hops"]:
            out["gaps"].append(dict(frm="R5b", to=h["hop"], gap=f"{h['gap_min']}..{h['gap_max']}, <= 1.5 for {h['longest']} s",
                                    ok=h["gap_min"] <= E.GAP_MAX and h["longest"] >= 1.0))
    disc = [d for d in L.devices if d["type"] == "disc"]
    for d in disc:
        r = d["diameter"] / 2
        ledge = max(f["z1"] for f in L.floors if f["z1"] <= d["pos"][2] - r + 0.01)
        land = min(f["z0"] for f in L.floors if f["z0"] >= d["pos"][2] + r - 0.01)
        half_ok = math.sqrt(max(0.0, r * r - (r - (E.GAP_MAX - (d["pos"][2] - r - ledge))) ** 2))
        out["gaps"].append(dict(frm=f"ledge z {ledge}", to=f"{d['id']} (8 m disc)", gap=f"<= 1.5 for |x| <= {half_ok:.2f}",
                                ok=half_ok >= 3.0))
        out["gaps"].append(dict(frm=f"{d['id']}", to=f"landing z {land}", gap=f"<= 1.5 for |x| <= {half_ok:.2f}",
                                ok=half_ok >= 3.0))
    for r in L.rails:
        out["rails"].append(dict(name=r["name"], h=r["h"], ok=abs(r["h"] - E.RAIL_H) < 0.01))
    # widths of the narrowest required places
    for b in L.beats:
        fl = [f for f in L.floors if f["beat"] == b["id"]]
        if fl:
            w = min(f["x1"] - f["x0"] for f in fl)
            out["widths"].append(dict(beat=b["id"], narrowest_floor=round(w, 2), ok=w >= 6.0))
    out["widths"].append(dict(beat="S2", narrowest_floor="ferry deck 8.0", ok=True))
    out["widths"].append(dict(beat="R5b", narrowest_floor="crates 6.0", ok=True))
    out["widths"].append(dict(beat="R2", narrowest_floor="gate doors 3.8 m over 0.6 m (short)", ok=True))
    # unrailed walked edges
    railed = [(r["x0"], r["x1"], r["z0"], r["z1"], r["floor"]) for r in L.rails]
    for f in L.floors:
        for side, x in (("W", f["x0"]), ("E", f["x1"])):
            covered = 0.0
            for rx0, rx1, rz0, rz1, ry in railed:
                if abs(ry - f["top"]) < 0.01 and (abs(rx1 - x) < 0.6 or abs(rx0 - x) < 0.6):
                    covered += max(0.0, min(rz1, f["z1"]) - max(rz0, f["z0"]))
            if covered < (f["z1"] - f["z0"]) - 0.05:
                out["unrailed"].append(dict(floor=f["name"], side=side, length=round(f["z1"] - f["z0"] - covered, 2)))
    # checkpoints clear of every hazard
    hazards = []
    for d in L.devices:
        if d["type"] == "fallingpiece":
            hazards.append((d["id"], d["pos"], 5.35))
        elif d["type"] in ("rookscreen", "pendulum", "sliding", "clockgates"):
            hazards.append((d["id"], d["pos"], 4.0))
    for idx, x, y, z in L.checkpoints:
        near = [h[0] for h in hazards if abs(h[1][2] - z) < h[2] and abs(h[1][1] - y) < 2.5]
        out["checkpoints"].append(dict(cp=idx, z=z, y=y, clear=not near, near=near))
    ex = getattr(L, "exit", None)
    strong = [d for d in L.devices if d["type"] in ("rookscreen", "fallingpiece", "pendulum", "foldingbridge")]
    last = max(d["pos"][2] + (1.0 if d["type"] == "rookscreen" else 7.05 if d["type"] == "foldingbridge" else 0.0)
               for d in strong)
    if ex:
        out["notes"].append(dict(what="runout after the last push", metres=round(ex["terrace"] - last, 2),
                                 ok=12.0 <= ex["terrace"] - last <= 18.0))
    return out, res


# ---------------------------------------------------------------------------------------------- (d) budget
def budget(kit_coll, cams):
    dg = bpy.context.evaluated_depsgraph_get()
    tri_cache = {}

    def tris(o):
        key = o.data.name if o.type == "MESH" else None
        if key is None:
            return 0
        if key not in tri_cache:
            ev = o.evaluated_get(dg)
            me = ev.to_mesh()
            tri_cache[key] = sum(len(p.vertices) - 2 for p in me.polygons)
            ev.to_mesh_clear()
        return tri_cache[key]

    assets = []
    for o in sorted(kit_coll.objects, key=lambda o: o.name):
        if o.type != "MESH":
            continue
        kind = "skin" if any(s in o.name for s in ("WarTable", "SweepBar", "Ferry", "Hurdle", "ShelfWall", "Gate", "RookScreen",
                                                   "Pendulum", "MapChest", "WindVent", "MapBelt", "GiantPawn", "Crown",
                                                   "HangingCrate", "CompassDisc", "BridgePanel", "BridgeLanding")) else \
            "hall" if any(s in o.name for s in ("Hall", "Bookshelf", "Banner", "ArchWindow", "Ladder", "Balcony", "Chandelier",
                                                "Lamp", "CeilingBeam", "Pillar")) else "course"
        n = tris(o)
        limit = 20000 if kind == "skin" else 5000 if kind == "hall" else None
        assets.append(dict(asset=o.name, kind=kind, tris=n, materials=len([m for m in o.data.materials if m]),
                           limit=limit, ok=limit is None or n <= limit))
    uniques = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith(("PR_EG_Floor_", "PR_EG_Walls_"))]
    for o in uniques:
        assets.append(dict(asset=o.name, kind="static", tris=tris(o), materials=len([m for m in o.data.materials if m]),
                           limit=None, ok=True))
    views = []
    sc = bpy.context.scene
    scene_objs = [o for o in bpy.data.objects if o.type == "MESH" and not o.hide_render and kit_coll not in o.users_collection
                  and "GameplayRef" not in o.name and "_sheet" not in o.name]
    for cam in cams:
        total = 0
        for o in scene_objs:
            bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
            vis = False
            for p in bb + [sum(bb, Vector()) / 8]:
                v = world_to_camera_view(sc, cam, p)
                if 0.0 <= v.x <= 1.0 and 0.0 <= v.y <= 1.0 and 0.0 < v.z < cam.data.clip_end:
                    vis = True
                    break
            if vis:
                total += tris(o)
        views.append(dict(camera=cam.name, visible_tris=total, ok=total <= 1_500_000))
    mats = sorted({m.name for o in scene_objs for m in o.data.materials if m})
    images = [dict(name=i.name, size=list(i.size)) for i in bpy.data.images if i.size[0] > 0]
    return dict(assets=assets, views=views, materials=mats, material_count=len(mats), textures=images,
                scene_tris=sum(tris(o) for o in scene_objs))
