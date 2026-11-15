# How several same-sized images differ from the first: mean absolute difference and the box where they differ.
# blender -b --factory-startup -P img_diff.py -- <image> <image> ...
import bpy, sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
ims = [bi.load(p)[..., :3] for p in a]
for i, x in enumerate(ims):
    m = bi.body_mask(np.concatenate([x, np.ones(x.shape[:2] + (1,), dtype=np.float32)], axis=2))
    rows = np.nonzero(m.any(axis=1))[0]
    cols = np.nonzero(m.any(axis=0))[0]
    print("IMG %d %s size %dx%d body rows %d-%d cols %d-%d mean %.4f" % (
        i + 1, os.path.basename(a[i])[-12:], x.shape[1], x.shape[0], rows.min(), rows.max(), cols.min(), cols.max(), x.mean()))
for i in range(1, len(ims)):
    if ims[i].shape != ims[0].shape:
        print("DIFF 1-%d different size" % (i + 1))
        continue
    d = np.abs(ims[i] - ims[0]).max(axis=2)
    big = d > 0.08
    if big.any():
        rows = np.nonzero(big.any(axis=1))[0]
        cols = np.nonzero(big.any(axis=0))[0]
        box = "rows %d-%d cols %d-%d" % (rows.min(), rows.max(), cols.min(), cols.max())
    else:
        box = "none"
    print("DIFF 1-%d mean %.4f, pixels over 0.08: %d (%s)" % (i + 1, d.mean(), int(big.sum()), box))
