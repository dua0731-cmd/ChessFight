# R684: the Royal Carriage Yard's collider audit (CarriageYardColliderAudit) summed up by object, where a pawn can be.
#   python reach.py <Logs/YardColliderAudit folder> <scan label, e.g. map_t1.5> <out.txt> [climb m, default 1.15]
# A flood over the 0.3 m standing places from the start court (and past the giant wheel's gap, on the loft and on the
# platform, which a pawn reaches on a jump and a dive, a hoist pallet, the lifting wheel), stepping 0.9 m at most and
# climbing at most `climb` (Pawn Rush's jump 0.87 m and its ledge help 0.2), never under the water (y < -1). Then, at
# reachable places only: TOP drawn above / below the collider stood on, colliders with nothing drawn under them, drawn
# up-faces in a jump's reach with no collider; SIDE rays (nearer hit within 1.5 m) where the drawn or the colliding hit
# comes first by more than 10 cm. Run it with Blender's Python (`python` on these PCs is a Store stub).
import csv, sys, math, collections, re, os
D = os.path.join(sys.argv[1], '')
label = sys.argv[2]; out = open(sys.argv[3], 'w', encoding='utf-8')
CLIMB = float(sys.argv[4]) if len(sys.argv) > 4 else 1.15
names = {int(r['id']): r['name'] for r in csv.DictReader(open(D + label + '_names.csv', encoding='utf-8'))}
def nm(i):
    s = names.get(int(i), '?')
    s = s.replace('Built/', '').replace('3-4 (own frame)/', '').replace(' (model)', '')
    s = re.sub(r'/Rcy\w+/Rcy\w+/', '/', s)
    return s
S = []; Dr = []
for r in csv.DictReader(open(D + label + '_surfaces.csv', encoding='utf-8')):
    x = float(r['x']); z = float(r['z']); y = float(r['y']); oy = float(r['oy']) if r['oy'] != 'nan' else None
    (S if r['kind'] == 'S' else Dr).append((x, z, y, oy, int(r['cid']), int(r['did'])))
# coarse 0.3 m surfaces for the flood fill
def key(x, z): return (round(x / .3), round(z / .3))
coarse = collections.defaultdict(list)
for (x, z, y, oy, c, d) in S:
    if y < -1.0: continue   # under the water (kill volumes)
    k = key(x, z)
    if all(abs(y - yy) > .05 for yy in coarse[k]): coarse[k].append(y)
seeds = [(0, 0, -7), (-4.6, 0, 70), (4.6, 0, 70), (0, 8, 141.6), (2.1, 2.4, 112.2), (-2.1, 2.4, 112.2), (0, 8, 150)]
reach = set(); q = collections.deque()
for sx, sy, sz in seeds:
    k = key(sx, sz)
    for yy in coarse.get(k, []):
        if abs(yy - sy) < .6: reach.add((k, yy)); q.append((k, yy))
R = 3  # 0.9 m
while q:
    (k, y) = q.popleft()
    for dx in range(-R, R + 1):
        for dz in range(-R, R + 1):
            if dx * dx + dz * dz > R * R: continue
            kk = (k[0] + dx, k[1] + dz)
            for yy in coarse.get(kk, ()):
                if yy - y <= CLIMB and (kk, yy) not in reach:
                    reach.add((kk, yy)); q.append((kk, yy))
byk = collections.defaultdict(list)
for (k, y) in reach: byk[k].append(y)
def reachable(x, z, y, slack=.3, climb=CLIMB):
    k = key(x, z)
    for dx in (-1, 0, 1):
        for dz in (-1, 0, 1):
            for yy in byk.get((k[0] + dx, k[1] + dz), ()):
                if abs(yy - y) <= slack: return True
    return False
def near_reach_below(x, z, y, climb=CLIMB):
    # a drawn surface at y a pawn could reach: a reachable surface within ~1 m whose top is below y by at most climb
    k = key(x, z)
    best = None
    for dx in range(-3, 4):
        for dz in range(-3, 4):
            for yy in byk.get((k[0] + dx, k[1] + dz), ()):
                if 0.1 < y - yy <= climb: return yy
    return None
print(label, 'coarse surfaces', sum(len(v) for v in coarse.values()), 'reachable', len(reach), file=out)
class G:
    __slots__ = ('n', 'mx', 'sm', 'x0', 'x1', 'y0', 'y1', 'z0', 'z1', 'w')
    def __init__(s): s.n = 0; s.mx = -1; s.sm = 0; s.x0 = s.y0 = s.z0 = 1e9; s.x1 = s.y1 = s.z1 = -1e9; s.w = None
    def add(s, x, y, z, d):
        s.n += 1; s.sm += d
        if d > s.mx: s.mx = d; s.w = (x, y, z)
        s.x0 = min(s.x0, x); s.x1 = max(s.x1, x); s.y0 = min(s.y0, y); s.y1 = max(s.y1, y); s.z0 = min(s.z0, z); s.z1 = max(s.z1, z)
def dump(title, groups, unit, minn):
    print('\n==', title, file=out)
    for k, g in sorted(groups.items(), key=lambda kv: -kv[1].n):
        if g.n < minn: continue
        print(f"{g.n*unit:8.2f}{'m2' if unit < 1 else 'ry'} max{g.mx:5.2f} mean{g.sm/g.n:5.2f} x{g.x0:.1f}..{g.x1:.1f} y{g.y0:.2f}..{g.y1:.2f} z{g.z0:.1f}..{g.z1:.1f} worst{tuple(round(v,2) for v in g.w)}  {k}", file=out)
top_hi = collections.defaultdict(G); top_lo = collections.defaultdict(G); top_none = collections.defaultdict(G)
for (x, z, y, oy, c, d) in S:
    if y < -1 or not reachable(x, z, y): continue
    if oy is None: top_none[nm(c)].add(x, y, z, 1.5)
    elif oy > y + .1: top_hi[nm(d) + '  over  ' + nm(c)].add(x, oy, z, oy - y)
    elif oy < y - .1: top_lo[nm(c) + '  over  ' + nm(d)].add(x, y, z, y - oy)
dump('TOP drawn above the collider you stand on (pawn sinks into the model)', top_hi, .01, 8)
dump('TOP collider above the drawn surface (pawn floats / stands on nothing drawn)', top_lo, .01, 8)
dump('TOP collider with nothing drawn within 1.5 m below (invisible floor)', top_none, .01, 8)
dnone = collections.defaultdict(G)
for (x, z, y, oy, c, d) in Dr:
    r = near_reach_below(x, z, y)
    if r is None: continue
    dnone[nm(d) + '  (collider under: ' + nm(c) + ')'].add(x, y, z, y - (oy if oy is not None else r))
dump('DRAWN up-face within jump reach with no collider within 10 cm under it (pawn passes through)', dnone, .01, 8)
sv = collections.defaultdict(G); sp = collections.defaultdict(G)
for r in csv.DictReader(open(D + label + '_side.csv', encoding='utf-8')):
    x = float(r['sx']); y = float(r['sy']); z = float(r['sz'])
    if y < -1 or not reachable(x, z, y, .05): continue
    dv = float(r['dv']); dp = float(r['dp']); h = r['h']; a = math.radians(float(r['dir']))
    if min(dv, dp) > 1.5: continue
    px = x + math.sin(a) * min(dv, dp); pz = z + math.cos(a) * min(dv, dp)
    if dv < dp: sv[nm(r['did']) + '  (collider behind: ' + nm(r['cid']) + ')'].add(round(px, 2), y + float(h), round(pz, 2), dp - dv)
    else: sp[nm(r['cid']) + '  (drawn behind: ' + nm(r['did']) + ')'].add(round(px, 2), y + float(h), round(pz, 2), dv - dp)
dump('SIDE drawn in front of the collider (pawn walks into the model)', sv, 1, 4)
dump('SIDE collider in front of the drawn surface (invisible wall)', sp, 1, 4)
