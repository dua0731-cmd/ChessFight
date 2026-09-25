"""Pawn body model: bind positions and joint offsets copied from Prefabs/RagdollPawn.prefab,
body shapes from the collision hulls in Generated/ (skirt and chest) and the primitive colliders."""
import os, re, struct, numpy as np
from scipy.spatial import ConvexHull

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
GEN = os.path.join(ROOT, 'Assets', 'ChessFight', 'RagdollLab', 'Generated') + os.sep
NAMES = ['Hips','Chest','Head','ArmL','HandL','ArmR','HandR','ThighL','FootL','ThighR','FootR']
PARENT = [-1,0,1,1,3,1,5,0,7,0,9]
BIND = {  # world positions of body origins at bind (all rotations identity)
 'Hips':(0,0.256,0.006),'Chest':(0,0.327,0.001),'Head':(0,0.609,-0.016),
 'ArmL':(-0.126,0.519,-0.022),'HandL':(-0.246,0.519,-0.022),'ArmR':(0.12,0.519,-0.022),'HandR':(0.239,0.519,-0.022),
 'ThighL':(-0.048,0.217,0.011),'FootL':(-0.144,0.066,0.002),'ThighR':(0.048,0.217,0.0),'FootR':(0.141,0.064,-0.009)}
BIND = {k: np.array(v, float) for k, v in BIND.items()}

def hull_vertices(name):
    s = open(GEN + name + '.asset', encoding='utf-8').read()
    n = int(re.search(r'm_VertexCount: (\d+)', s).group(1))
    hexs = re.search(r'_typelessdata: ([0-9a-f]+)', s).group(1)
    raw = bytes.fromhex(hexs)
    v = np.array(struct.unpack('<%df' % (n * 3), raw[:n * 12]), float).reshape(n, 3)
    return v

def sample_hull(verts, density=900):
    h = ConvexHull(verts)
    pts, nrm = [], []
    areas = []
    for simp, eq in zip(h.simplices, h.equations):
        a, b, c = verts[simp]
        areas.append(np.linalg.norm(np.cross(b - a, c - a)) / 2)
    areas = np.array(areas); tot = areas.sum()
    for (simp, eq), ar in zip(zip(h.simplices, h.equations), areas):
        a, b, c = verts[simp]
        k = max(3, int(density * ar / tot * 40))
        r1 = np.random.rand(k, 1); r2 = np.random.rand(k, 1)
        m = (r1 + r2) > 1; r1[m] = 1 - r1[m]; r2[m] = 1 - r2[m]
        p = a + r1 * (b - a) + r2 * (c - a)
        pts.append(p); nrm.append(np.repeat(eq[None, :3], k, 0))
    return np.vstack(pts), np.vstack(nrm)

def sample_sphere(center, r, n=3000):
    d = np.random.randn(n, 3); d /= np.linalg.norm(d, axis=1, keepdims=True)
    return center + d * r, d

def sample_ellipsoid(center, radii, n=2500):
    d = np.random.randn(n, 3); d /= np.linalg.norm(d, axis=1, keepdims=True)
    p = d * radii
    nn = d / radii; nn /= np.linalg.norm(nn, axis=1, keepdims=True)
    return center + p, nn

def sample_capsule(a, b, r, n=2500):
    u = np.random.rand(n, 1)
    axis = b - a; L = np.linalg.norm(axis); ax = axis / L
    d = np.random.randn(n, 3); d -= (d @ ax)[:, None] * ax; d /= np.linalg.norm(d, axis=1, keepdims=True)
    p = a + u * axis + d * r
    s1, n1 = sample_sphere(a, r, n // 3); s2, n2 = sample_sphere(b, r, n // 3)
    return np.vstack([p, s1, s2]), np.vstack([d, n1, n2])

SKIN = np.array([0.93, 0.80, 0.68]); DARK = np.array([0.22, 0.17, 0.16]); COLLAR = np.array([0.6, 0.6, 0.85])

def build():
    """Shapes in each body's local frame (body origin at bind position, identity rotation)."""
    np.random.seed(1)
    shapes = {}
    hv = hull_vertices('PawnHipsHull'); cv = hull_vertices('PawnChestHull')
    p, n = sample_hull(hv); shapes['Hips'] = [(p, n, SKIN)]
    p, n = sample_hull(cv); shapes['Chest'] = [(p, n, SKIN)]
    head_c = np.array([-0.001, 0.822, 0.0]) - BIND['Head']
    p, n = sample_sphere(head_c, 0.195, 14000); shapes['Head'] = [(p, n, SKIN)]
    # eyes (front of the head, +z) so the facing reads
    for sx in (-0.06, 0.06):
        e, en = sample_sphere(head_c + np.array([sx, 0.0, 0.17]), 0.03, 300)
        shapes['Head'].append((e, en, DARK))
    for side, arm, hand, sgn in (('L', 'ArmL', 'HandL', -1), ('R', 'ArmR', 'HandR', 1)):
        a = np.zeros(3); b = BIND[hand] - BIND[arm]
        p, n = sample_capsule(a, b, 0.05); shapes[arm] = [(p, n, SKIN)]
        hc = np.array([0.316 * sgn, 0.508, -0.014]) - BIND[hand]
        p, n = sample_sphere(hc, 0.08); shapes[hand] = [(p, n, DARK)]
    for thigh, foot in (('ThighL', 'FootL'), ('ThighR', 'FootR')):
        a = np.zeros(3); b = BIND[foot] - BIND[thigh]
        p, n = sample_capsule(a, b, 0.045); shapes[thigh] = [(p, n, SKIN * 0.9)]
        fc = np.array([np.sign(BIND[foot][0]) * 0.148, 0.059, 0.0]) - BIND[foot]
        p, n = sample_ellipsoid(fc, np.array([0.09, 0.055, 0.11])); shapes[foot] = [(p, n, DARK)]
    return shapes

OFFSET = {NAMES[i]: (BIND[NAMES[i]] - BIND[NAMES[PARENT[i]]]) if PARENT[i] >= 0 else None for i in range(11)}
