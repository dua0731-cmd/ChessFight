# ChessFight look rebuild: what a kit material wears. The forms are stylized; the surfaces are real (the Mario Kart
# World recipe). A material's colour still comes from the mesh ('Col'); this lays a surface set from tex/ over it
# (textures.py makes the sets): fine colour variation, roughness, relief, and two things a hand-finished game asset
# has: lighter worn edges, and grime where surfaces meet.
#
# The sets are laid on the three world planes and blended by the surface's facing (no UVs needed, any geometry).
# A set with a direction (wood: the grain runs along the image's V) is laid in each piece's own frame instead: grain()
# finds every loose piece of a wood batch, takes its longest axis (a plank's length, a post's height, a stave's run)
# and stores it per face corner as 'Grain', so the grain follows every board however it lies.
#
# Unity reads the same sets and the same 'Grain' (exported in UV channels): Assets/Look/Shaders/LookLit.shader.
import bpy, os, json, math
import numpy as np

TEX = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tex'))
try:
    with open(os.path.join(TEX, 'sets.json')) as f:
        SETS = json.load(f)
except Exception:
    SETS = {}

# kit material key: its set, and how strongly it shows (1 = as made)
#   var    the set's colour variation        cav   darkening in the set's hollows       bump  relief
#   wear   worn edges: lighter and smoother  dirt  grime where surfaces meet (ambient occlusion)
WEAR = {
    'stone': dict(set='limestone', var=1.0, cav=0.5, bump=1.0, wear=0.55, dirt=0.40),
    'sand': dict(set='sand', var=1.0, cav=0.4, bump=1.0, wear=0.0, dirt=0.22),
    'wood': dict(set='wood', var=1.0, cav=0.4, bump=1.0, wear=0.5, dirt=0.38),
    'paint': dict(set='paint', var=1.0, cav=0.3, bump=1.0, wear=0.55, dirt=0.28),
    'cloth': dict(set='canvas', var=1.0, cav=0.4, bump=1.0, wear=0.0, dirt=0.18),
    'gold': dict(set='brass', var=1.0, cav=0.3, bump=1.0, wear=0.35, dirt=0.30),
    'iron': dict(set='iron', var=1.0, cav=0.4, bump=1.0, wear=0.65, dirt=0.18),
    'leaf': dict(set='leaf', var=1.0, cav=0.6, bump=1.0, wear=0.0, dirt=0.0),
}
DIRT_TINT = (0.60, 0.52, 0.44)      # grime multiplies the colour by this
DIRT_REACH = 0.45                   # metres

class Build:
    """Short hands for wiring shader nodes."""
    def __init__(self, nt):
        self.nt = nt

    def node(self, kind, **props):
        n = self.nt.nodes.new(kind)
        for key, value in props.items():
            setattr(n, key, value)
        return n

    def put(self, socket, value):
        if hasattr(value, 'is_output'):
            self.nt.links.new(value, socket)
        else:
            socket.default_value = value

    def math(self, op, a, b=None, c=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        for i, v in enumerate((a, b, c)):
            if v is not None:
                self.put(n.inputs[i], v)
        return n.outputs[0]

    def vec(self, op, a, b=None, scale=None):
        n = self.node('ShaderNodeVectorMath', operation=op)
        self.put(n.inputs[0], a)
        if b is not None:
            self.put(n.inputs[1], b)
        if scale is not None:
            self.put(n.inputs['Scale'], scale)
        return n.outputs['Value'] if op in ('DOT_PRODUCT', 'LENGTH') else n.outputs['Vector']

    def xyz(self, x, y, z):
        n = self.node('ShaderNodeCombineXYZ')
        for i, v in enumerate((x, y, z)):
            self.put(n.inputs[i], v)
        return n.outputs[0]

    def mix(self, a, b, t):
        """a + (b - a) * t, for colours and vectors."""
        return self.vec('ADD', a, self.vec('SCALE', self.vec('SUBTRACT', b, a), scale=t))

    def image(self, img, vector, box=False):
        n = self.node('ShaderNodeTexImage', image=img, extension='REPEAT', interpolation='Linear')
        if box:
            n.projection = 'BOX'
            n.projection_blend = 0.28
        self.nt.links.new(vector, n.inputs['Vector'])
        return n


def _set_image(name):
    path = os.path.join(TEX, name + '_mask.png')
    if not os.path.exists(path):
        return None
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = 'Non-Color'
    img.alpha_mode = 'CHANNEL_PACKED'
    return img


def _world_planes(b, img, geo, tile):
    tex = b.image(img, b.vec('SCALE', geo.outputs['Position'], scale=1.0 / tile), box=True)
    return tex.outputs['Color'], tex.outputs['Alpha']


def _grain_planes(b, img, geo, tile):
    """The set laid in the piece's own frame: its grain (image V) along the corner attribute 'Grain'."""
    attr = b.node('ShaderNodeAttribute', attribute_name='Grain')
    g = b.vec('NORMALIZE', b.vec('ADD', attr.outputs['Vector'], (0.0, 0.0, 1e-3)))      # no attribute: straight up
    sep = b.node('ShaderNodeSeparateXYZ')
    b.nt.links.new(g, sep.inputs[0])
    steep = b.math('GREATER_THAN', b.math('ABSOLUTE', sep.outputs['Z']), 0.9)
    aside = b.mix((0.0, 0.0, 1.0), (1.0, 0.0, 0.0), steep)
    b1 = b.vec('NORMALIZE', b.vec('CROSS_PRODUCT', g, aside))
    b2 = b.vec('CROSS_PRODUCT', g, b1)
    P = b.vec('SCALE', geo.outputs['Position'], scale=1.0 / tile)
    N = geo.outputs['Normal']
    qx, qy, qz = b.vec('DOT_PRODUCT', P, b1), b.vec('DOT_PRODUCT', P, b2), b.vec('DOT_PRODUCT', P, g)
    w = [b.math('POWER', b.math('ABSOLUTE', b.vec('DOT_PRODUCT', N, axis)), 4.0) for axis in (b1, b2, g)]
    total = b.math('ADD', b.math('ADD', w[0], w[1]), b.math('ADD', w[2], 1e-5))
    faces = (b.image(img, b.xyz(qy, qz, 0.0)), b.image(img, b.xyz(qx, qz, 0.0)), b.image(img, b.xyz(qx, qy, 0.0)))
    color, alpha = None, None
    for tex, weight in zip(faces, w):
        share = b.math('DIVIDE', weight, total)
        c = b.vec('SCALE', tex.outputs['Color'], scale=share)
        a = b.math('MULTIPLY', tex.outputs['Alpha'], share)
        color = c if color is None else b.vec('ADD', color, c)
        alpha = a if alpha is None else b.math('ADD', alpha, a)
    return color, alpha


def dress(material, key, scale=1.0):
    """Lays the key's surface set over a kit material. False when there is none (the material stays as it was)."""
    w = WEAR.get(key)
    spec = SETS.get(w['set']) if w else None
    img = _set_image(w['set']) if spec else None
    if img is None:
        return False
    nt = material.node_tree
    bsdf = nt.nodes['Principled BSDF']
    cols = [n for n in nt.nodes if n.type == 'ATTRIBUTE' and n.attribute_name == 'Col']
    if not cols:
        return False
    b = Build(nt)
    geo = b.node('ShaderNodeNewGeometry')
    tile = spec['tile'] * scale
    color, alpha = (_grain_planes if spec.get('grain') else _world_planes)(b, img, geo, tile)
    sep = b.node('ShaderNodeSeparateColor')
    nt.links.new(color, sep.inputs[0])
    rough, cav, height = sep.outputs[0], sep.outputs[1], sep.outputs[2]

    vary = b.math('MULTIPLY_ADD', b.math('SUBTRACT', alpha, 0.5), 0.5 * w['var'], 1.0)
    hollow = b.math('MULTIPLY_ADD', b.math('SUBTRACT', cav, 1.0), w['cav'], 1.0)
    base = b.vec('SCALE', cols[0].outputs['Color'], scale=b.math('MULTIPLY', vary, hollow))
    if w['wear'] > 0:
        edge = b.node('ShaderNodeMapRange', clamp=True)
        nt.links.new(geo.outputs['Pointiness'], edge.inputs['Value'])
        edge.inputs['From Min'].default_value, edge.inputs['From Max'].default_value = 0.54, 0.66
        worn = b.math('MULTIPLY', edge.outputs[0], w['wear'])
        base = b.mix(base, b.vec('ADD', b.vec('SCALE', base, scale=1.22), (0.04, 0.04, 0.035)), worn)
        rough = b.math('MULTIPLY', rough, b.math('MULTIPLY_ADD', worn, -0.3, 1.0))
    if w['dirt'] > 0:
        ao = b.node('ShaderNodeAmbientOcclusion', samples=6)
        ao.inputs['Distance'].default_value = DIRT_REACH
        grime = b.math('MULTIPLY', b.math('POWER', b.math('SUBTRACT', 1.0, ao.outputs['AO']), 1.5), w['dirt'], clamp=True)
        base = b.mix(base, b.vec('MULTIPLY', base, DIRT_TINT), grime)
        rough = b.math('MULTIPLY_ADD', grime, 0.25, rough, clamp=True)
    nt.links.new(base, bsdf.inputs['Base Color'])
    nt.links.new(rough, bsdf.inputs['Roughness'])
    bump = b.node('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 1.0
    bump.inputs['Distance'].default_value = spec['depth'] * w['bump']
    nt.links.new(height, bump.inputs['Height'])
    nt.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return True


# ---------------------------------------------------------------- the grain of every loose piece
def piece_axes(mesh):
    """For every vertex the longest axis of the loose piece it belongs to (unit vectors, the larger part positive)."""
    nv = len(mesh.vertices)
    co = np.empty(nv * 3, np.float32)
    mesh.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3).astype(np.float64)
    ev = np.empty(len(mesh.edges) * 2, np.int32)
    mesh.edges.foreach_get('vertices', ev)
    ev = ev.reshape(-1, 2)
    label = np.arange(nv)
    for _ in range(400):
        low = np.minimum(label[ev[:, 0]], label[ev[:, 1]])
        new = label.copy()
        np.minimum.at(new, ev[:, 0], low)
        np.minimum.at(new, ev[:, 1], low)
        new = new[new]
        if np.array_equal(new, label):
            break
        label = new
    _, piece = np.unique(label, return_inverse=True)
    count = np.bincount(piece).astype(np.float64)
    mean = np.stack([np.bincount(piece, weights=co[:, i]) / count for i in range(3)], -1)
    d = co - mean[piece]
    cov = np.zeros((len(count), 3, 3))
    for i in range(3):
        for j in range(i, 3):
            cov[:, i, j] = cov[:, j, i] = np.bincount(piece, weights=d[:, i] * d[:, j]) / count
    values, vectors = np.linalg.eigh(cov)
    axis = vectors[:, :, 2]
    # a piece with no clear length (a ball, a cube) stands its grain up
    round_ = values[:, 2] < values[:, 1] * 1.3
    axis[round_] = (0.0, 0.0, 1.0)
    big = np.take_along_axis(axis, np.abs(axis).argmax(1)[:, None], 1)[:, 0]
    axis *= np.where(big < 0, -1.0, 1.0)[:, None]
    return axis[piece].astype(np.float32)


def write_grain(mesh):
    if not len(mesh.vertices) or not len(mesh.loops):
        return
    axes = piece_axes(mesh)
    lv = np.empty(len(mesh.loops), np.int32)
    mesh.loops.foreach_get('vertex_index', lv)
    old = mesh.attributes.get('Grain')
    if old is not None:
        mesh.attributes.remove(old)
    attr = mesh.attributes.new('Grain', 'FLOAT_VECTOR', 'CORNER')
    attr.data.foreach_set('vector', axes[lv].ravel())


def grain(k):
    """After k.finish(): 'Grain' on every batch whose material wears a set with a direction."""
    keys = {m: key for key, m in k.mats.items()}
    done = []
    for name, batch in k.batches.items():
        w = WEAR.get(keys.get(batch.mat))
        obj = getattr(batch, 'obj', None)
        if w and obj is not None and SETS.get(w['set'], {}).get('grain'):
            write_grain(obj.data)
            done.append(name)
    return done
