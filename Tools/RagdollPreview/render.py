import numpy as np
from PIL import Image, ImageDraw
import model

SHAPES = model.build()

def Rx(a):
    a = np.radians(a); c, s = np.cos(a), np.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
def Ry(a):
    a = np.radians(a); c, s = np.cos(a), np.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
def Rz(a):
    a = np.radians(a); c, s = np.cos(a), np.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])
def Euler(x, y, z):   # Unity: Z, then X, then Y
    return Ry(y) @ Rx(x) @ Rz(z)

def chain(hips_pos, hips_rot, local):
    """local: dict name -> 3x3 local rotation (relative to parent). Returns name -> (R, p)."""
    out = {'Hips': (hips_rot, np.asarray(hips_pos, float))}
    for i, n in enumerate(model.NAMES):
        if i == 0: continue
        pn = model.NAMES[model.PARENT[i]]
        Rp, pp = out[pn]
        out[n] = (Rp @ local.get(n, np.eye(3)), pp + Rp @ model.OFFSET[n])
    return out

def lowest_foot(bodies):
    lo = 9.0
    for n in ('FootL', 'FootR'):
        R, p = bodies[n]
        for pts, _, _ in SHAPES[n]:
            lo = min(lo, (pts @ R.T + p)[:, 1].min())
    return lo

def render(bodies, eye, target, size=(260, 300), fov=40.0, ground=True, shadow=True):
    W, H = size
    f = np.asarray(target, float) - np.asarray(eye, float); f /= np.linalg.norm(f)
    r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r)   # screen right (left-handed world: up x forward)
    u = np.cross(f, r)
    focal = (H / 2) / np.tan(np.radians(fov / 2))
    img = np.ones((H, W, 3)) * np.array([0.93, 0.95, 0.97])
    zb = np.full((H, W), np.inf)
    L = np.array([0.4, 0.8, -0.45]); L /= np.linalg.norm(L)
    def splat(P, N, col, rad=1):
        d = P - eye
        z = d @ f; x = d @ r; y = d @ u
        ok = z > 0.05
        sx = (W / 2 + focal * x[ok] / z[ok]).astype(int); sy = (H / 2 - focal * y[ok] / z[ok]).astype(int)
        zz = z[ok]
        sh = 0.35 + 0.65 * np.clip(N[ok] @ L, 0, 1)
        c = col[None, :] * sh[:, None] if col.ndim == 1 else col[ok]
        for dx in range(-rad, rad + 1):
            for dy in range(-rad, rad + 1):
                X = sx + dx; Y = sy + dy
                m = (X >= 0) & (X < W) & (Y >= 0) & (Y < H)
                Xm, Ym, Zm, Cm = X[m], Y[m], zz[m], c[m]
                order = np.argsort(-Zm)          # far first, near overwrites
                Xm, Ym, Zm, Cm = Xm[order], Ym[order], Zm[order], Cm[order]
                closer = Zm < zb[Ym, Xm]
                zb[Ym[closer], Xm[closer]] = Zm[closer]
                img[Ym[closer], Xm[closer]] = Cm[closer]
    if ground:
        g = np.mgrid[-1.5:1.5:0.01, -1.5:1.5:0.01].reshape(2, -1).T
        cx, cz = target[0], target[2]
        G = np.c_[g[:, 0] + cx, np.zeros(len(g)), g[:, 1] + cz]
        chk = ((np.floor(G[:, 0] / 0.25) + np.floor(G[:, 2] / 0.25)) % 2)[:, None]
        col = np.where(chk > 0, 0.86, 0.95) * np.ones((1, 3))
        splat(G, np.tile([0, 1, 0], (len(G), 1)), col, rad=1)
        img_ground_z = zb.copy()
    world_pts = []
    for n, (R, p) in bodies.items():
        for pts, nrm, col in SHAPES[n]:
            P = pts @ R.T + p; N = nrm @ R.T
            world_pts.append(P)
            splat(P, N, col, rad=1)
    if shadow:
        P = np.vstack(world_pts); S = P.copy(); S[:, 1] = 0.001
        d = S - eye; z = d @ f; x = d @ r; y = d @ u
        sx = (W / 2 + focal * x / z).astype(int); sy = (H / 2 - focal * y / z).astype(int)
        m = (sx >= 0) & (sx < W) & (sy >= 0) & (sy < H)
        # darken ground pixels under the body (only where the ground is what is visible)
        mask = np.zeros((H, W), bool); mask[sy[m], sx[m]] = True
        vis = np.isclose(zb, img_ground_z)
        img[mask & vis] *= 0.8
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
