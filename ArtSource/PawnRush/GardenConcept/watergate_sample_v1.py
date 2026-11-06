# Royal water garden - concept-art dressing: the water gate of the east channel inside the castle wall (R483).
#
# 승규 11-06: "castle gate 3d model.glb" + "1. 보트가 내려 갈 때는 상호 작용 안해도 돼 사운드는 아직 넣지마 2. 보트
# 애니메이션을 둘거야 에셋 설치 해주고 그 구간에서 배가 떠 다니는 애니메이션 만들어줘", where: "성벽 안 수로 수문", then
# "꼼꼼히 텍스쳐 해" with the gate's reference picture.
#
# The east channel: the lake (-0.55) between the lock's east wall (x 35 .. 35.6, z -0.3 .. 32.6, top 5.6) and the
# castle bank (x 45.8 on, top 0.6), from the weirs' line (z -15.5) to the reservoir's east terrace (z 33). Nobody walks
# it: it is seen from the lock, the robot dyke's east end and the plaza. 승규's gate (gate_asset.py: the arcaded front,
# the gatehouse behind, the arch right through) spans it as its water gate:
#   - mirrored (its little tower over the bank, by the castle wall; its cypress against the lock wall) and 17.2 times
#     its own size: the front 16.2 m long (x 35.7 .. 51.9), the gatehouse 14 m deep (z 3.9 .. 18.0), its roof 4.8 m;
#     its front faces south (the plaza);
#   - standing at the bank's height (0.6) on two stone footings in the water, one each side of the passage, so the
#     arch's clear opening (2.75 m wide, x 41.2 .. 43.96) stands 2.7 m over the water;
#   - "Lake boat south 1 / 2, north 1 / 2" (R487; R486 three, R485 the "Watergate boat"): the garden's royal boat
#     (royal_boat.py) at 0.75 of its size (4.8 m, 1.8 m over the fenders, its lanterns 1.7 m over the water), two on
#     each of the lake's two routes in the wide waters, each in its own colours. They ride in Unity (GardenLakeBoat):
#     each one's origin is its waterline's middle, its bow faces north (+y here, the section's +z), it rests on its
#     route (lake_routes.py: boat 1 at its first control point, boat 2 at its middle one). No boat goes through the
#     gate since R487 (the wide waters only).
# Nothing here collides (the channel is out of reach; the lock wall and the bank keep theirs).
#
# Run:  blender -b --python gate_asset.py -- "<castle gate 3d model.glb>"   (once: GateAsset_v1.blend)
#       blender -b --python watergate_sample_v1.py
#       blender -b --python handmade_pass.py -- WatergateSample_v1.blend --step 4
#       blender -b --python export_concept.py -- WatergateSample_v1.blend "Watergate dressing" GardenWatergate
# Out:  WatergateSample_v1.blend, render_watergate_v1_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Watergate dressing"
os.environ["GC_SEED"] = "483"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403
import water_tiers as wt  # noqa: E402

LAKE = wt.MIDDLE                       # -0.55
BANK = .6                              # the castle bank's top (RoyalWaterGardenBuilder.CastleBankY)
GATE_ASSET = os.path.join(HERE, "GateAsset_v1.blend")
S = 17.2                               # the asset's units to metres
X_WEST = 35.7                          # where the asset's front ends by the lock wall (its x' -0.45, mirrored)
Y_FACE = 6.0                           # the piers' front (the asset's y -0.30), the section's z
Z0 = BANK                              # its base
CLEAR = (-.03, .13)                    # the asset's clear opening (x, over the whole depth, up to z 0.085)
STEM_X = X_WEST + (.45 - sum(CLEAR) / 2) * S      # 42.58: the passage's middle, the boat's way through
BOAT_SCALE = .75
BOAT_START = (STEM_X, 2.0)             # the south boat's first route point (GardenLakeBoat)


def wx(mx):
    return X_WEST + (.45 - mx) * S


def wy(my):
    return Y_FACE + (my + .30) * S


# ------------------------------------------------------------------ materials (the garden's, by name)
def gate_stone_texture():
    """2 m of the reference gate's stone (sRGB) - walls_sample_v2.py's, the castle walls' stone."""
    n = 1024
    t = np.clip(.7 + .3 * periodic_noise(n, n, 31, 2.3, 2), 0, 1)
    t = t * t * (3 - 2 * t)
    light, deep = np.array([.99, .905, .82]), np.array([.965, .845, .735])
    rgb = deep * (1 - t[..., None]) + light * t[..., None]
    rgb = rgb * (1 + .012 * periodic_noise(n, n, 32, .4, 80))[..., None]
    pits = np.random.default_rng(33).random((n, n))
    rgb[pits < .0012] *= .87
    return save_image("Stone_Gate", np.clip(rgb, 0, 1))


GS = textured("GC | castle stone", gate_stone_texture(), (1.0, .93, .86), .74, .5)
material("GC | gate gold", (.98, .82, .30), .3, .75)
material("GC | gate blue", (.18, .38, .78), .55)
import royal_boat  # noqa: E402  (its own materials, the lantern glow among them)

# ------------------------------------------------------------------ the context (grey)
ctx = Mesh("Context blockout", CTX)
ctx.box((26.4, -1.0, DEEP), (35.0, 24.0, 3.8), M["clay"])            # the lock (beat 4)
ctx.box((35.0, -.3, DEEP), (35.6, 32.6, 5.6), M["clay"])             # its east wall
ctx.box((8.0, 24.0, DEEP), (35.6, 33.0, 3.8), M["clay"])             # the robot dyke (beat 5)
ctx.box((26.0, 33.0, DEEP), (44.0, 48.6, 3.8), M["clay"])            # the reservoir's east terrace
ctx.box((45.8, -35.08, DEEP), (54.5, 44.41, BANK), M["clay"])        # the castle bank
ctx.box((52.2, -12.0, DEEP), (54.6, 34.0, 12.3), M["clay"])          # the castle wall (its inner face)
ctx.done()
water = bpy.data.materials.new("Context | water")
water.use_nodes = True
_p = water.node_tree.nodes["Principled BSDF"]
_p.inputs["Base Color"].default_value = (*srgb((.02, .58, .82)), 1)
_p.inputs["Roughness"].default_value = .06
wm = Mesh("Context water", CTX)
wm.box((20.0, -15.5, LAKE - .05), (46.5, 33.0, LAKE), water)
wm.done()

# ------------------------------------------------------------------ the gate (gate_asset.py)
with bpy.data.libraries.load(GATE_ASSET, link=False) as (src, dst):
    dst.meshes = [n for n in src.meshes]
me = max(dst.meshes, key=lambda m: len(m.polygons))
for i, m in enumerate(me.materials):
    base = m.name.rsplit(".", 1)[0] if m.name[-4:-3] == "." else m.name
    if base != m.name and base in bpy.data.materials:
        me.materials[i] = bpy.data.materials[base]               # the garden's own material of that name
place = Matrix.Translation((X_WEST + .45 * S, Y_FACE + .30 * S, Z0)) @ Matrix.Diagonal((-S, S, S, 1))
me.transform(place)
me.flip_normals()                                                # mirrored
for p in me.polygons:
    p.use_smooth = True
me.set_sharp_from_angle(angle=math.radians(40))                  # smooth, but for its edges
uv = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
for p in me.polygons:
    n = p.normal
    for li in p.loop_indices:
        co = me.vertices[me.loops[li].vertex_index].co
        uv.data[li].uv = (co.x, co.y) if abs(n.z) > .7 else ((co.y, co.z) if abs(n.x) > abs(n.y) else (co.x, co.z))
me.name = "Watergate"
gate = bpy.data.objects.new("Watergate", me)
DRESS.objects.link(gate)
xs = [v.co.x for v in me.vertices]
ys = [v.co.y for v in me.vertices]
zs = [v.co.z for v in me.vertices]
print("WATERGATE x %.2f .. %.2f, z %.2f .. %.2f, height %.2f .. %.2f, passage x %.2f .. %.2f, faces %d"
      % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs), wx(CLEAR[1]), wx(CLEAR[0]), len(me.polygons)))

# ------------------------------------------------------------------ its footings in the water
# Two stone piers under it from the lake's bed to the bank's height, either side of the passage, in courses with a
# proud band at the top; the east one runs on into the bank.
foot = Mesh("Watergate footings")
y0, y1 = wy(-.409) - .15, wy(.409) + .15
for x0, x1 in ((35.0, wx(CLEAR[1]) - .05), (wx(CLEAR[0]) + .05, 52.0)):
    foot.box((x0, y0, -2.25), (x1, y1, Z0 - .02), GS, .04)
    foot.box((max(35.0, x0 - .07), y0 - .07, Z0 - .36), (x1 + .07, y1 + .07, Z0 - .04), GS, .03)   # the band
    for z in (-1.35, -.55 + .32):                                 # a course line just over the water and one under it
        foot.box((max(35.0, x0 - .02), y0 - .02, z - .03), (x1 + .02, y1 + .02, z), M["stone_dark"], .01)
foot.done()

# ------------------------------------------------------------------ the lake's four boats (royal_boat.py, smaller), at rest
# R486 (승규 "총 3개가 나올 수 있게 하고 각자 택스쳐가 다르게 해줘"): each boat in its own colours. R487 (승규 "2개씩 나와야
# 하고 텀이 길어 넓은 쪽으로 와야 해", "총 6척이 아니라 4척인데 그 주기 가 짧고 플레이어들이 볼 수 있어야 돼"): four, two on
# each of the two short routes in the wide waters by the way (lake_routes.py): the south lake - the reservoir's royal
# blue with purple velvet, and crimson with teal velvet; north of the platforms - ivory with an emerald band and
# emerald velvet, and violet with crimson velvet. The gold, wood, cream upholstery, ropes and lanterns stay the royal
# boat's.
CRIMSON = {
    "GC | boat blue": ("GC | boat crimson", (.66, .09, .13), .45),
    "GC | boat blue seam": ("GC | boat crimson seam", (.40, .05, .08), .6),
    "GC | velvet purple": ("GC | velvet teal", (.07, .42, .45), .75),
    "GC | velvet purple light": ("GC | velvet teal light", (.13, .55, .56), .7),
    "GC | fender blue": ("GC | fender crimson", (.62, .08, .11), .35),
}
IVORY = {
    "GC | boat blue": ("GC | boat ivory", (.95, .91, .82), .45),
    "GC | boat blue seam": ("GC | boat ivory seam", (.78, .70, .56), .6),
    "GC | boat cream": ("GC | boat emerald band", (.06, .44, .25), .5),
    "GC | velvet purple": ("GC | velvet emerald", (.06, .44, .25), .75),
    "GC | velvet purple light": ("GC | velvet emerald light", (.12, .58, .34), .7),
    "GC | fender blue": ("GC | fender emerald", (.05, .40, .22), .35),
}
VIOLET = {
    "GC | boat blue": ("GC | boat violet", (.40, .19, .62), .45),
    "GC | boat blue seam": ("GC | boat violet seam", (.24, .10, .40), .6),
    "GC | velvet purple": ("GC | velvet crimson", (.62, .08, .14), .75),
    "GC | velvet purple light": ("GC | velvet crimson light", (.76, .16, .22), .7),
    "GC | fender blue": ("GC | fender violet", (.38, .17, .60), .35),
}
# name: colours, where it rests (its route's first control point for boat 1, the middle one for boat 2:
# GardenLakeBoat.Rest), its bow north
BOATS = {
    "south 1": ({}, (26.0, -3.9)),
    "south 2": (CRIMSON, (-21.4, -9.6)),
    "north 1": (IVORY, (-25.0, 29.7)),
    "north 2": (VIOLET, (-4.0, 12.2)),
}
made = {}
for name, (swaps, rest) in BOATS.items():
    boat = royal_boat.build("Lake boat " + name, 0, 0)
    bm_ = boat.data
    bm_.transform(Matrix.Rotation(math.radians(-90), 4, "Z") @ Matrix.Diagonal((BOAT_SCALE,) * 3 + (1,))
                  @ Matrix.Translation((0, 0, -3.0)))           # its waterline (the reservoir, 3.0) to 0, bow north
    for i, m in enumerate(bm_.materials):
        if m.name in swaps:
            new, rgb, rough = swaps[m.name]
            if new not in made:
                made[new] = material(new, rgb, rough)
            bm_.materials[i] = made[new]
    boat.location = (rest[0], rest[1], LAKE)
    bz = [v.co.z for v in bm_.vertices]
    print("LAKE BOAT %s at %s, height over the water %.2f, draught %.2f, materials %s"
          % (name, rest, max(bz), -min(bz), sorted(m.name for m in bm_.materials)))

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, py, pz in ((34.0, 26.0, 3.8), (30.5, 12.0, 3.8)):
    pawn(scale, px, py, pz)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "WatergateSample_v1.blend"))
shoot("watergate_v1", "south", (41.0, -12.0, 4.5), (43.0, 8.0, 2.5), 28)
shoot("watergate_v1", "from_dyke", (34.5, 29.0, 6.2), (43.0, 12.0, 1.5), 26)
shoot("watergate_v1", "arch", (42.4, -2.0, .9), (42.6, 8.0, 1.4), 30)
shoot("watergate_v1", "overview", (24.0, -14.0, 22.0), (43.0, 10.0, 1.0), 24)
shoot("watergate_v1", "boat_south", (21.0, -7.5, 2.2), (26.0, -3.9, .3), 35)
shoot("watergate_v1", "boat_violet", (0.0, 8.0, 2.4), (-4.0, 12.2, .3), 35)
print("WATERGATE SAMPLE DONE")
