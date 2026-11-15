# Small image helpers for the bishop work, on Blender's own image loader (no PIL on this PC): load and save as numpy
# arrays with rows from the top, crop with a labelled pixel grid, resize, paste, tiny digit labels.
import bpy
import numpy as np

def load(path):
    """(h, w, 4) float32, rows from the top, values as stored (sRGB for 8-bit files)."""
    im = bpy.data.images.load(path)
    w, h = im.size
    buf = np.empty(w * h * 4, dtype=np.float32)
    im.pixels.foreach_get(buf)
    bpy.data.images.remove(im)
    return buf.reshape(h, w, 4)[::-1].copy()

def save(path, arr):
    arr = np.asarray(arr, dtype=np.float32)
    if arr.shape[2] == 3:
        arr = np.concatenate([arr, np.ones(arr.shape[:2] + (1,), dtype=np.float32)], axis=2)
    h, w = arr.shape[:2]
    im = bpy.data.images.new("out", w, h, alpha=True)
    im.pixels.foreach_set(np.ascontiguousarray(np.clip(arr, 0, 1)[::-1]).ravel())
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)

def on_white(x):
    """Alpha composited onto white, alpha 1 (8-bit files load with straight alpha)."""
    al = x[..., 3:4]
    return np.concatenate([np.clip(x[..., :3] * al + (1 - al), 0, 1), np.ones_like(al)], axis=2)

def resize(x, w, h):
    """Bilinear (area-averaged when shrinking by whole steps is not needed here: inputs are smooth renders)."""
    H, W = x.shape[:2]
    ys = (np.arange(h) + 0.5) * H / h - 0.5
    xs = (np.arange(w) + 0.5) * W / w - 0.5
    y0 = np.clip(np.floor(ys).astype(int), 0, H - 1); y1 = np.clip(y0 + 1, 0, H - 1)
    x0 = np.clip(np.floor(xs).astype(int), 0, W - 1); x1 = np.clip(x0 + 1, 0, W - 1)
    fy = np.clip(ys - y0, 0, 1)[:, None, None]; fx = np.clip(xs - x0, 0, 1)[None, :, None]
    a = x[y0][:, x0]; b = x[y0][:, x1]; c = x[y1][:, x0]; d = x[y1][:, x1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy

def shrink(x, k):
    """Box filter by an integer factor."""
    H, W = x.shape[:2]
    H2, W2 = H // k, W // k
    return x[:H2 * k, :W2 * k].reshape(H2, k, W2, k, x.shape[2]).mean(axis=(1, 3))

FONT = {
    "0": ["111", "101", "101", "101", "111"], "1": ["010", "110", "010", "010", "111"],
    "2": ["111", "001", "111", "100", "111"], "3": ["111", "001", "111", "001", "111"],
    "4": ["101", "101", "111", "001", "001"], "5": ["111", "100", "111", "001", "111"],
    "6": ["111", "100", "111", "101", "111"], "7": ["111", "001", "001", "001", "001"],
    "8": ["111", "101", "111", "101", "111"], "9": ["111", "101", "111", "001", "111"],
    "-": ["000", "000", "111", "000", "000"], ".": ["000", "000", "000", "000", "010"],
    " ": ["000", "000", "000", "000", "000"],
}

def text(img, s, x, y, scale=2, colour=(0.85, 0.0, 0.0)):
    for ch in str(s):
        g = FONT.get(ch, FONT[" "])
        for r, line in enumerate(g):
            for c, bit in enumerate(line):
                if bit == "1":
                    y0, x0 = y + r * scale, x + c * scale
                    if 0 <= y0 < img.shape[0] - scale and 0 <= x0 < img.shape[1] - scale:
                        img[y0:y0 + scale, x0:x0 + scale, :3] = colour
        x += 4 * scale

def grid_crop(x, x0, y0, x1, y1, zoom=1, step=50, colour=(0.1, 0.35, 0.95)):
    """The crop [x0, x1) x [y0, y1) of image x, enlarged `zoom` times, with grid lines every `step` source pixels
    labelled in source coordinates."""
    c = x[y0:y1, x0:x1, :].copy()
    if c.shape[2] == 3:
        c = np.concatenate([c, np.ones(c.shape[:2] + (1,), dtype=np.float32)], axis=2)
    c[..., 3] = 1
    if zoom != 1:
        c = np.repeat(np.repeat(c, zoom, axis=0), zoom, axis=1)
    col = np.array(colour, dtype=np.float32)
    for gx in range((x0 // step + 1) * step, x1, step):
        px = (gx - x0) * zoom
        a = 0.55 if gx % (2 * step) == 0 else 0.25
        c[:, px, :3] = c[:, px, :3] * (1 - a) + col * a
        if gx % (2 * step) == 0:
            text(c, gx, px + 3, 3)
    for gy in range((y0 // step + 1) * step, y1, step):
        py = (gy - y0) * zoom
        a = 0.55 if gy % (2 * step) == 0 else 0.25
        c[py, :, :3] = c[py, :, :3] * (1 - a) + col * a
        if gy % (2 * step) == 0:
            text(c, gy, 3, py + 3)
    return c

def body_mask(x):
    """The cream body against the white page: warm (red over blue) and not near black."""
    rgb = x[..., :3]
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    return (rgb[..., 0] - rgb[..., 2] > 0.05) & (lum > 0.25)

def runs(row, min_len=3, max_gap=2):
    """[(start, end)] of True runs in a 1-D mask (end exclusive), gaps up to max_gap closed, short runs dropped."""
    d = np.diff(np.concatenate([[0], row.astype(np.int8), [0]]))
    s, e = np.nonzero(d == 1)[0], np.nonzero(d == -1)[0]
    out = []
    for a, b in zip(s, e):
        if out and a - out[-1][1] <= max_gap:
            out[-1] = (out[-1][0], int(b))
        else:
            out.append((int(a), int(b)))
    return [(a, b) for a, b in out if b - a >= min_len]
