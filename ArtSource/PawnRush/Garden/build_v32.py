"""R307: v31 -> v32. Run in Blender 5.2: blender --background --python build_v32.py

User request 11-03 ("지금 seoungtest에 정원 맵에 저 배치로 바꿔 없는 프리팹은 블록 아웃 처리 해서 넣어줘" -> "모델을 옮겨
쓰고"): the garden takes main's v31 layout (Docs/KingRush/PAWN_RUSH_ROYAL_GARDEN.md, RoyalWaterGardenBuilder: a folded
way round the walled square instead of straight up its middle). Unity builds that layout as a graybox from code; this
file moves the garden's own models onto it. Models are moved and turned only, never reshaped or cut (11-02 rule):
- stay where they are: the castle walls and towers, the moat, the round plaza and its piers, the start pad, the
  terraces and their trees and planters, the west tank and its falls, the east water gate.
- gate group (arch, joins, towers and roofs, yard blocks, checker floor) 1.2 m down: v31 walks at y 3.8, the art at 5.0.
- the boarding blocks 2 m south and 1.2 m down onto the v31 quay (x -10..10, z 27..33).
- the six drums ("C platform zigzag") onto the bobbing platforms P1..P6, two linked copies onto P7 and P8; Unity
  hangs them on the code's bobbing movers (their drivers are cleared here).
- the two robot arms (pendulum supports, hinges, arms; their Blender swing is kept) to the robot dyke: south of it
  in the water, swinging north across it, at x 22.5 and 10.5 (v31: 26 and 13 - moved so their supports clear the
  lock wall and the shortcut's lotus pad L1).
Deleted, because the v31 layout has no place for them (reported in v32_report.json): the old straight way's walls,
ramp and pillars, the upper reservoir with its walls and side / front / centre falls, the team docks and rails, the
east tank (it stands on the dyke), the big plaza spin bar (five small bars replace it), the royal team boats (4.5 m
wide: the v31 lanes are 3.6 m apart and the tunnels 2.4 m wide, so the boats stay graybox), the parade boat, the old
planters and the low-poly V04 dressing along the old way, and the lily pads that floated on the old reservoir.
"""
import bpy, json, math, re
from pathlib import Path
from mathutils import Vector, Matrix

folder = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder / 'RoyalWaterGarden_v31.blend'))
scene = bpy.context.scene
scene.frame_set(1)
O = bpy.data.objects
report = {'deleted': [], 'moved': {}, 'added': [], 'notes': []}

UPPER = 3.8   # v31 walking height above the lower water (RoyalWaterGardenBuilder.Upper)


def world_bounds(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])


def remove_tree(o):
    names = [c.name for c in o.children_recursive] + [o.name]
    for name in names:
        if name in O:
            report['deleted'].append(name)
            O.remove(O[name])


def remove_where(test):
    for name in [o.name for o in O if test(o)]:
        if name in O:
            remove_tree(O[name])


def move(o, matrix, why):
    """Moves an object (and what hangs on it) by a world transform, keeping its own shape and animation."""
    o.matrix_world = matrix @ o.matrix_world
    report['moved'][o.name] = why


# ------------------------------------------------------------------ delete what v31 has no place for

ROTOR = ('회전봉', 'V04_회전봉', '초반_회전봉광장', 'V20 | clockwise plaza rotor')
OLD_WAY = ('V19 | V03_', 'V19 | decorative brick pillar', 'V07 Water | upper reservoir', 'V09 | reservoir wall',
           'V23 | planter bushes', 'V23 | planter flowers',
           'V04_지형_갓돌과기둥', 'V04_지형_깃발과화분', 'V04_지형_폭포', 'V04_장식정원_꽃과폭포')
DOCKS = ('부두난간', '팀분리난간', '계류기둥', '승선부두')
FALLS = re.compile(r'^V2[36] \| (fall spray|fall curtain|fall streams|spout shadow|spout stone) (centre|front|side|tank E)')

remove_where(lambda o: o.name.startswith(ROTOR))
remove_where(lambda o: o.name.startswith('V30 | parade'))
remove_where(lambda o: o.name.endswith('보트_PIVOT'))
remove_where(lambda o: o.name.startswith(OLD_WAY))
remove_where(lambda o: any(w in o.name for w in DOCKS))
remove_where(lambda o: FALLS.match(o.name) is not None)
remove_where(lambda o: o.name.startswith('V23 | tank pier E'))
remove_where(lambda o: o.name.startswith(('V19 | V02_장식_폭포수조', 'V22 | tank water')) and world_bounds(o)[0].x > 0)

# Lily pads: a whole object goes if any pad floats on the old upper reservoir (cutting single pads out is not allowed).
for name in ('V04_수면_연잎', 'V24 | lily pads'):
    o = O.get(name)
    if o and o.type == 'MESH' and any((o.matrix_world @ v.co).z > 0 for v in o.data.vertices):
        remove_tree(o)

# ------------------------------------------------------------------ move what stays

down = Matrix.Translation((0, 0, UPPER - 5.0))
GATE = ('V17 | finish castle arch', 'V17 | gate to tower', 'V19 | 도착_', '도착_', 'V04_도착_')
for o in [o for o in O if o.name.startswith(GATE) and not (o.parent and o.parent.name.startswith(GATE))]:
    move(o, down, 'gate group 1.2 m down to the v31 yard (y 3.8)')
for o in [o for o in O if o.name.startswith('V19 | V02_공용승선대기장')]:
    move(o, Matrix.Translation((0, -2.0, UPPER - 5.0)), 'boarding block onto the v31 quay (z 27..33, y 3.8)')


def up_surface(o, step=.05):
    """The height of the biggest upward-facing area: a drum's walking top."""
    mw = o.matrix_world
    n3 = mw.to_3x3()
    area = {}
    for p in o.data.polygons:
        if (n3 @ p.normal).normalized().z > .9:
            z = round((mw @ p.center).z / step) * step
            area[z] = area.get(z, 0.0) + p.area
    return max(area, key=area.get)


# Drums -> bobbing platforms. v31: top middle of platform i at (-21 + 6i, 2.35 + 0.25i, z 2.5 / 6.5), half way up its bob.
def platform_top(i):
    return Vector((-21.0 + 6.0 * i, 2.5 if i % 2 == 0 else 6.5, 2.0 + .25 * i + .35))


drums = [O['V16 | C platform zigzag %d %s' % (k, 'left' if k % 2 else 'right')] for k in range(1, 7)]
for d in drums:
    d.animation_data_clear()   # the bob comes from Unity's ObstacleMotion now; the pose of frame 1 stays
for k in (7, 8):
    src = drums[(k - 7)]
    copy = O.new('V32 | platform drum P%d' % k, src.data)
    copy.matrix_world = src.matrix_world.copy()
    for c in src.users_collection:
        c.objects.link(copy)
    drums.append(copy)
    report['added'].append(copy.name)
bpy.context.view_layer.update()
attach = {}
for i, d in enumerate(drums):
    lo, hi = world_bounds(d)
    top = up_surface(d)
    want = platform_top(i)
    move(d, Matrix.Translation((want.x - (lo.x + hi.x) / 2, want.y - (lo.y + hi.y) / 2, want.z - top)),
         'drum onto bobbing platform P%d' % (i + 1))
    attach[d.name] = 'Bobbing platform P%d' % (i + 1)

# Robots -> the dyke. A mount at the old way's middle line carries the support, the hinge and the swinging pivot, so
# the arm keeps its swing; turned so it swings north across the dyke (x 8..35, z 24..30) from south of it.
ROBOTS = [  # pivot, support, hinge, the old way's middle (y), turn, new x
    ('V02_필수진자_PIVOT', 'V18 | pendulum 1 | fixed support', 'V22 | robot 1 shoulder hinge', 22.0, 90.0, 22.5),
    ('V02_필수진자_PIVOT.001', 'V18 | pendulum 2 | fixed support', 'V22 | robot 2 shoulder hinge', 27.0, -90.0, 10.5),
]
for n, (pivot, support, hinge, old_y, turn, new_x) in enumerate(ROBOTS, 1):
    mount = O.new('V32 | robot %d mount' % n, None)
    scene.collection.objects.link(mount)
    report['added'].append(mount.name)
    for name in (pivot, support, hinge):
        o = O[name]
        world = o.matrix_world.copy()
        o.parent = mount
        o.matrix_parent_inverse = Matrix.Identity(4)
        o.matrix_world = world
    move(mount, Matrix.Translation((new_x, 27.0, UPPER - 5.0)) @ Matrix.Rotation(math.radians(turn), 4, 'Z') @ Matrix.Translation((0, -old_y, 0)),
         'robot %d to the dyke at x %.1f, swinging north across it' % (n, new_x))

bpy.context.view_layer.update()
for o in O:
    if o.type == 'MESH' and not o.hide_render and o.name.startswith(('V17 | finish castle arch', 'V29 | castle wall N2')):
        lo, hi = world_bounds(o)
        mw = o.matrix_world
        band = [mw @ v.co for v in o.data.vertices]
        z0 = lo.z + 1.0
        band = [p for p in band if z0 < p.z < z0 + 2.0]
        left = max((p.x for p in band if p.x < 0), default=None)
        right = min((p.x for p in band if p.x > 0), default=None)
        report['notes'].append('%s: opening at its foot from x %s to %s' % (o.name, left and round(left, 2), right and round(right, 2)))

report['attach'] = attach
bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'RoyalWaterGarden_v32.blend'))
(folder / 'v32_report.json').write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding='utf8')
print('V32', len(report['deleted']), 'deleted', len(report['moved']), 'moved', report['notes'], flush=True)
