// Plaza explainer - the 3D side: the pieces in the game's look, the accessories, the kingdom hall as a cutaway
// model and a plain studio floor. A schematic, not a look proposal: simple shapes on paper.
// Nothing here keeps state between frames: a scene sets every pose from the time it is given.
import * as THREE from 'three';
import { clamp, lerp, rng } from './kit.js';
export { THREE };

export const W = 1920, H = 1080;
const V = (x = 0, y = 0, z = 0) => new THREE.Vector3(x, y, z);

function canvasTex(w, h, draw) {
  const c = document.createElement('canvas'); c.width = w; c.height = h;
  draw(c.getContext('2d'), w, h);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
  return t;
}
const std = (color, o = {}) => new THREE.MeshStandardMaterial(Object.assign({ color, roughness: 0.8 }, o));
export const M = {
  white: new THREE.MeshPhysicalMaterial({ color: 0xf4ead8, roughness: 0.42, clearcoat: 0.4, clearcoatRoughness: 0.3 }),
  black: new THREE.MeshPhysicalMaterial({ color: 0x35302d, roughness: 0.4, clearcoat: 0.6, clearcoatRoughness: 0.25 }),
  dark: std(0x231f1d, { roughness: 0.5 }), eye: std(0xffffff, { roughness: 0.25 }), pupil: std(0x111111, { roughness: 0.2 }),
  gold: std(0xd2a23a, { metalness: 0.65, roughness: 0.35 }), slit: std(0x3b2b20),
  stone: std(0xe9dfce, { roughness: 0.95 }), stone2: std(0xd9cdb8, { roughness: 0.95 }), cut: std(0xbfb19a, { roughness: 1 }),
  wood: std(0x7a5236, { roughness: 0.7 }), woodDark: std(0x4a3226, { roughness: 0.7 }), wine: std(0x97303f, { roughness: 0.9 }),
  glass: std(0xfff1cc, { emissive: 0xffdf9a, emissiveIntensity: 0.55, roughness: 0.6 }), mirror: std(0xbfdbe6, { roughness: 0.15, metalness: 0.3 }),
  cream: std(0xf1e6d2), teal: std(0x2a9d94), pink: std(0xf08aa5), yellow: std(0xf2c230), green: std(0x4f9a4c), brown: std(0x5a3a26), blue: std(0x4d8fe8),
  capsule: new THREE.MeshBasicMaterial({ color: 0x1e8c86, wireframe: true, transparent: true, opacity: 0.75 }),
};
function mesh(geo, mat, x = 0, y = 0, z = 0, shadow = true) {
  const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z);
  if (shadow) { m.castShadow = true; m.receiveShadow = true; }
  return m;
}
const box = (w, h, d, mat, x = 0, y = 0, z = 0) => mesh(new THREE.BoxGeometry(w, h, d), mat, x, y, z);
const ball = (r, mat, x = 0, y = 0, z = 0) => mesh(new THREE.SphereGeometry(r, 20, 14), mat, x, y, z);
const cyl = (rt, rb, h, mat, x = 0, y = 0, z = 0, seg = 28) => mesh(new THREE.CylinderGeometry(rt, rb, h, seg), mat, x, y, z);

// ---------- pieces (the same lathe shapes as Tools/Previz/PawnRushUltimates, with the game's hands, shoes and eyes) ----------
export const HEIGHT = { pawn: 0.9, rook: 1.1, knight: 1.25, bishop: 1.32, queen: 1.45 };
export const NAME = { pawn: '폰', knight: '나이트', bishop: '비숍', rook: '룩', queen: '퀸' };
const LEG = 0.1;
const BASE = [[0.0001, 0], [0.36, 0], [0.375, 0.035], [0.36, 0.07], [0.32, 0.09], [0.31, 0.12], [0.27, 0.14]];
const arc = (cy, r, a0, a1, n = 10) => { const o = []; for (let i = 0; i <= n; i++) { const a = lerp(a0, a1, i / n); o.push([Math.max(0.0001, Math.cos(a) * r), cy + Math.sin(a) * r]); } return o; };
const PROFILE = {
  pawn: [...BASE, [0.2, 0.2], [0.15, 0.3], [0.12, 0.44], [0.2, 0.48], [0.21, 0.51], [0.19, 0.54], [0.11, 0.56], ...arc(0.72, 0.17, -1.2, Math.PI / 2)],
  rook: [...BASE, [0.25, 0.2], [0.22, 0.3], [0.22, 0.62], [0.25, 0.75], [0.3, 0.8], [0.31, 0.83], [0.28, 0.85], [0.3, 0.88], [0.3, 0.98], [0.22, 0.98], [0.22, 0.93], [0.0001, 0.93]],
  bishop: [...BASE, [0.23, 0.2], [0.18, 0.32], [0.15, 0.62], [0.16, 0.75], [0.24, 0.8], [0.25, 0.83], [0.17, 0.86], [0.13, 0.88], [0.16, 0.92], [0.185, 0.98], [0.185, 1.04], [0.165, 1.1], [0.125, 1.16], [0.07, 1.2], [0.03, 1.22], [0.03, 1.24], ...arc(1.28, 0.05, -0.9, Math.PI / 2, 6)],
  queen: [...BASE, [0.24, 0.2], [0.19, 0.32], [0.14, 0.7], [0.13, 0.95], [0.2, 1.0], [0.21, 1.03], [0.14, 1.06], [0.13, 1.1], [0.19, 1.25], [0.225, 1.3], [0.19, 1.31], [0.12, 1.32], [0.0001, 1.33]],
  knight: [...BASE, [0.25, 0.2], [0.22, 0.28], [0.25, 0.34], [0.0001, 0.36]],
};
const GEO = {};
const lathe = kind => (GEO[kind] ??= (() => { const g = new THREE.LatheGeometry(PROFILE[kind].map(p => new THREE.Vector2(p[0], p[1])), 48); g.computeVertexNormals(); return g; })());
function knightHead() {
  if (GEO.head) return GEO.head;
  const s = new THREE.Shape();
  const P = [[-0.2, 0.34], [-0.23, 0.6], [-0.2, 0.86], [-0.12, 1.02], [-0.05, 1.12], [-0.02, 1.24], [0.04, 1.13], [0.12, 1.05], [0.22, 0.93], [0.3, 0.82], [0.3, 0.73], [0.2, 0.69], [0.08, 0.66], [0.04, 0.6], [0.12, 0.48], [0.16, 0.34]];
  s.moveTo(P[0][0], P[0][1]); for (let i = 1; i < P.length; i++) s.lineTo(P[i][0], P[i][1]); s.closePath();
  const g = new THREE.ExtrudeGeometry(s, { depth: 0.24, bevelEnabled: true, bevelThickness: 0.04, bevelSize: 0.035, bevelSegments: 3, curveSegments: 4 });
  g.translate(0, 0, -0.12); g.rotateY(-Math.PI / 2); g.computeVertexNormals();
  return (GEO.head = g);
}
// Where things sit on each piece (the design's "보정표"): the head item [x, y, z, scale], the eyes [y, z, r, half gap],
// the back [y, z, scale].
const ANCH = {
  pawn: { top: [0, 0.875, 0, 1], eye: [0.74, 0.16, 0.065, 0.075], back: [0.5, -0.02, 1] },
  rook: { top: [0, 0.93, 0, 0.72], eye: [0.8, 0.235, 0.07, 0.08], back: [0.6, -0.04, 1.15] },
  knight: { top: [0, 1.2, -0.03, 0.5], eye: [0.98, 0.1, 0.06, 0.15], back: [0.62, -0.06, 1.15] },
  bishop: { top: [0, 1.31, 0, 0.5], eye: [0.93, 0.165, 0.06, 0.07], back: [0.62, -0.02, 1.05] },
  queen: { top: [0, 1.44, 0, 0.5], eye: [1.03, 0.15, 0.06, 0.07], back: [0.66, -0.02, 1.05] },
};

const ITEM = {
  party() {
    const g = new THREE.Group();
    g.add(mesh(new THREE.ConeGeometry(0.14, 0.34, 24), M.wine, 0, 0.17, 0), ball(0.045, M.cream, 0, 0.36, 0));
    const brim = mesh(new THREE.TorusGeometry(0.14, 0.018, 8, 28), M.gold, 0, 0.01, 0); brim.rotation.x = Math.PI / 2; g.add(brim);
    for (let i = 0; i < 3; i++) { const r = mesh(new THREE.TorusGeometry(0.105 - i * 0.03, 0.012, 6, 24), M.cream, 0, 0.09 + i * 0.075, 0); r.rotation.x = Math.PI / 2; g.add(r); }
    g.rotation.z = -0.12;
    return g;
  },
  top() {
    const g = new THREE.Group();
    g.add(cyl(0.2, 0.2, 0.025, M.dark, 0, 0.012, 0), cyl(0.125, 0.115, 0.22, M.dark, 0, 0.13, 0), cyl(0.127, 0.12, 0.05, M.wine, 0, 0.05, 0));
    g.rotation.z = 0.1;
    return g;
  },
  flower() {
    const g = new THREE.Group();
    g.add(cyl(0.012, 0.012, 0.14, M.green, 0, 0.07, 0, 8), ball(0.04, M.yellow, 0, 0.16, 0));
    for (let i = 0; i < 6; i++) { const a = i / 6 * Math.PI * 2, p = ball(0.045, M.pink, Math.cos(a) * 0.065, 0.16, Math.sin(a) * 0.065); p.scale.y = 0.55; g.add(p); }
    g.rotation.z = 0.25;
    return g;
  },
  glasses(a) {
    const g = new THREE.Group(), r = a.eye[2] * 1.25, x = Math.max(a.eye[3], r + 0.012);
    for (const s of [-1, 1]) g.add(mesh(new THREE.TorusGeometry(r, 0.014, 8, 28), M.dark, s * x, 0, 0));
    g.add(box(Math.max(0.02, 2 * (x - r)), 0.014, 0.014, M.dark, 0, 0.01, 0));
    return g;
  },
  mustache(a) {
    const g = new THREE.Group();
    for (const s of [-1, 1]) { const m = ball(0.05, M.brown, s * 0.055, -a.eye[2] * 1.5, 0); m.scale.set(1.5, 0.5, 0.5); m.rotation.z = -s * 0.35; g.add(m); }
    return g;
  },
  cape() {
    const mat = std(0x97303f, { roughness: 0.9, side: THREE.DoubleSide });
    const g = new THREE.Group(), c = mesh(new THREE.CylinderGeometry(0.2, 0.36, 0.52, 28, 1, true, Math.PI / 2 + 0.25, Math.PI - 0.5), mat, 0, -0.14, 0);
    const col = mesh(new THREE.TorusGeometry(0.2, 0.025, 8, 28, Math.PI - 0.5), M.gold, 0, 0.12, 0); col.rotation.x = Math.PI / 2; col.rotation.z = Math.PI + 0.25;
    g.add(c, col);
    return g;
  },
  pack() {
    const g = new THREE.Group();
    g.add(box(0.3, 0.3, 0.16, M.teal, 0, -0.06, -0.3), box(0.22, 0.1, 0.03, M.cream, 0, -0.1, -0.39), box(0.32, 0.07, 0.18, M.brown, 0, 0.1, -0.3));
    return g;
  },
};

/** One character: a chess piece on little shoes with mitten hands. All poses are set from outside, every frame. */
export class Actor {
  constructor(kind = 'pawn', team = 'white') {
    this.kind = kind; this.team = team; this.a = ANCH[kind];
    const mat = team === 'black' ? M.black : M.white;
    const g = this.g = new THREE.Group(), tilt = this.tilt = new THREE.Group(), body = this.body = new THREE.Group();
    g.rotation.order = 'YXZ';      // turn first, then tip over in its own frame (tip())
    g.add(tilt); tilt.add(body); body.position.y = LEG;
    body.add(mesh(lathe(kind), mat));
    if (kind === 'knight') body.add(mesh(knightHead(), mat));
    if (kind === 'rook') for (let i = 0; i < 5; i++) { const a = i / 5 * Math.PI * 2, b = box(0.13, 0.12, 0.1, mat, Math.sin(a) * 0.26, 1.03, Math.cos(a) * 0.26); b.rotation.y = a; body.add(b); }
    if (kind === 'bishop') { const sl = box(0.025, 0.2, 0.42, M.slit, 0.03, 1.08, 0); sl.rotation.z = 0.6; body.add(sl); }
    if (kind === 'queen') {
      for (let i = 0; i < 9; i++) { const a = i / 9 * Math.PI * 2; body.add(ball(0.036, M.gold, Math.sin(a) * 0.21, 1.33, Math.cos(a) * 0.21)); }
      body.add(ball(0.07, M.gold, 0, 1.39, 0));
    }
    const [ey, ez, er, eg] = this.a.eye;
    for (const s of [-1, 1]) body.add(ball(er, M.eye, s * eg, ey, ez), ball(er * 0.52, M.pupil, s * eg, ey - er * 0.1, ez + er * 0.62));
    this.handY = LEG + 0.3 + 0.12 * (HEIGHT[kind] - 0.9);
    this.handL = ball(0.1, M.dark); this.handR = ball(0.1, M.dark); tilt.add(this.handL, this.handR);
    this.footL = ball(0.1, M.dark); this.footR = ball(0.1, M.dark);
    for (const f of [this.footL, this.footR]) { f.scale.set(1, 0.62, 1.5); g.add(f); }
    this.items = {};
    this.shell = null;
    this.pose();
  }
  /** Puts on what is named (and takes everything else off): wear({head: 'party', face: 'glasses', back: 'cape'}). */
  wear(w = {}) {
    for (const k in this.items) this.items[k].visible = false;
    for (const slot of ['head', 'face', 'back']) {
      const name = w[slot]; if (!name) continue;
      let it = this.items[name];
      if (!it) {
        it = this.items[name] = new THREE.Group(); it.add(ITEM[name](this.a)); this.body.add(it);
        if (slot === 'head') { const [x, y, z, s] = this.a.top; it.position.set(x, y, z); it.scale.setScalar(s); it.userData.s = s; }
        if (slot === 'face') { const [ey, ez, er] = this.a.eye; it.position.set(0, ey, ez + er * 0.9); it.userData.s = 1; }
        if (slot === 'back') { const [y, z, s] = this.a.back; it.position.set(0, y, z); it.scale.setScalar(s); it.userData.s = s; }
      }
      it.visible = true;
    }
    return this;
  }
  /** An item's size, 0..1 of its fitted size (for popping it on). */
  size(name, k) { const it = this.items[name]; if (it) { it.visible = k > 0.01; it.scale.setScalar(Math.max(0.001, k) * it.userData.s); } }
  /** The collision shape drawn as a wire capsule (it does not change with what is worn). */
  capsule(on) {
    if (!this.shell) { this.shell = new THREE.Mesh(new THREE.CapsuleGeometry(0.4, HEIGHT[this.kind] - 0.55, 3, 10), M.capsule); this.shell.position.y = LEG + HEIGHT[this.kind] / 2; this.g.add(this.shell); }
    this.shell.visible = on;
  }
  place(x, y, z, yaw = 0) { this.g.position.set(x, y, z); this.g.rotation.set(0, yaw, 0); this.g.visible = true; return this; }
  /** Tips the whole piece over: back (negative) or forward about its own side axis, and sideways. */
  tip(x, z = 0) { this.g.rotation.x = x; this.g.rotation.z = z; return this; }
  /**
   * The body's pose. walk: 0..1 how much it is running, phase: the step, emote: a name with w (0..1, how far in)
   * and t (seconds since it began), lean / roll: extra tilt, squash: 1 = none.
   */
  pose(o = {}) {
    const walk = o.walk ?? 0, ph = o.phase ?? 0, t = o.t ?? 0, w = o.w ?? 0, hy = this.handY;
    const tilt = this.tilt, sin = Math.sin, hop = Math.abs(sin(ph)) * 0.07 * walk + (o.idle ?? 1) * (1 - walk) * sin(t * 2.6 + (o.seed ?? 0)) * 0.012;
    tilt.position.set(0, hop, 0);
    tilt.rotation.set(0.17 * walk + (o.lean ?? 0), 0, (o.roll ?? 0) + sin(ph) * 0.05 * walk);
    const sq = o.squash ?? 1;
    this.body.scale.set(1 / Math.sqrt(sq), sq, 1 / Math.sqrt(sq));
    const L = V(-0.37, hy, 0.04 + sin(ph + Math.PI) * 0.17 * walk), R = V(0.37, hy, 0.04 + sin(ph) * 0.17 * walk);
    this.footL.position.set(-0.15, 0.062 + Math.max(0, Math.cos(ph)) * 0.08 * walk, 0.07 + sin(ph) * 0.15 * walk);
    this.footR.position.set(0.15, 0.062 + Math.max(0, -Math.cos(ph)) * 0.08 * walk, 0.07 - sin(ph) * 0.15 * walk);
    if (w > 0 && o.emote) {
      const top = LEG + HEIGHT[this.kind], l = (v, x, y, z) => v.lerp(V(x, y, z), w);
      switch (o.emote) {
        case 'laugh':   // holds its belly and shakes
          l(L, -0.17, hy + 0.06, 0.34); l(R, 0.17, hy + 0.06, 0.34);
          tilt.rotation.x += w * (-0.24 + sin(t * 15) * 0.07); tilt.position.y += w * Math.abs(sin(t * 15)) * 0.03; break;
        case 'wave':    // one hand up, waving
          l(R, 0.46 + sin(t * 11) * 0.1, top * 0.92, 0.06); tilt.rotation.z += w * sin(t * 5.5) * 0.06; break;
        case 'cheer':   // both hands up, bouncing
          l(L, -0.3, top * 0.96 + sin(t * 9) * 0.04, 0.04); l(R, 0.3, top * 0.96 + sin(t * 9 + 1) * 0.04, 0.04); tilt.position.y += w * Math.abs(sin(t * 7)) * 0.12; break;
        case 'taunt':   // hands by the ears, wagging
          l(L, -0.34 + sin(t * 13) * 0.05, top * 0.86, 0.1); l(R, 0.34 - sin(t * 13) * 0.05, top * 0.86, 0.1); tilt.rotation.z += w * sin(t * 6.5) * 0.14; break;
        case 'yawn':    // a hand to the mouth, leaning back
          l(R, 0.1, top * 0.74, 0.3); tilt.rotation.x += w * -0.2; break;
        case 'shove':   // both hands out to the front
          l(L, -0.2, hy + 0.12, 0.5); l(R, 0.2, hy + 0.12, 0.5); tilt.rotation.x += w * 0.22; break;
        case 'stand':   // arms folded: the pose a lobby figure stands in
          l(L, -0.3, hy + 0.02, 0.16); l(R, 0.3, hy + 0.02, 0.16); break;
      }
    }
    this.handL.position.copy(L); this.handR.position.copy(R);
    return this;
  }
}

// ---------- footprints ----------
/** A trail of little stars on the floor behind a walking piece. */
export class Stars {
  constructor(scene, n = 16) {
    const s = new THREE.Shape();
    for (let i = 0; i < 10; i++) { const a = i / 10 * Math.PI * 2 - Math.PI / 2, r = i % 2 ? 0.055 : 0.13; i ? s.lineTo(Math.cos(a) * r, Math.sin(a) * r) : s.moveTo(Math.cos(a) * r, Math.sin(a) * r); }
    const geo = new THREE.ShapeGeometry(s); geo.rotateX(-Math.PI / 2);
    this.list = [];
    for (let i = 0; i < n; i++) { const m = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ color: 0xe9a91f, transparent: true, opacity: 0, depthWrite: false })); m.visible = false; scene.add(m); this.list.push(m); }
  }
  hide() { for (const m of this.list) m.visible = false; }
  /** path(u) -> [x, z] at the time u; a star is left every `every` seconds and lasts `life`. */
  draw(t, path, y = 0.012, every = 0.22, life = 2.2, from = 0) {
    const first = Math.max(Math.ceil(from / every), Math.ceil((t - life) / every)), last = Math.floor(t / every);
    this.hide();
    for (let k = first, i = 0; k <= last && i < this.list.length; k++, i++) {
      const u = k * every, [x, z] = path(u), m = this.list[i], age = (t - u) / life;
      m.visible = true; m.position.set(x + (k % 2 ? 0.12 : -0.12), y, z); m.rotation.y = k * 1.3;
      m.material.opacity = 0.95 * (1 - age) * clamp((t - u) * 8, 0, 1); m.scale.setScalar(0.75 + 0.25 * (1 - age));
    }
  }
}

// ---------- the kingdom hall, as a cutaway model ----------
// 19 m wide like the lobby's hall: the great gate at z = -25, the grand stair at z = +6.5 and up, the stage board at 0.
export const HALL = { halfW: 9.5, gateZ: -25, stairZ: 6.3, backZ: 16, wardrobe: V(-6.4, 0, 9.2), shop: V(6.4, 0, 9.2), pads: V(-5.4, 0, -13), ring: V(5.4, 0, -13) };
export const PAD = 1.0;                    // the knight pads: 5 x 5, one metre apart
export const padAt = (i, j) => V(HALL.pads.x + (i - 2) * PAD, 0.16, HALL.pads.z + (j - 2) * PAD);

function buildHall() {
  const scene = new THREE.Scene(), P = { install: {}, pads: [] };
  scene.add(new THREE.HemisphereLight(0xfff6e6, 0xd6c3a6, 1.55));
  const sun = new THREE.DirectionalLight(0xfff0d6, 2.5); sun.position.set(16, 30, -16); sun.target.position.set(0, 0, -4);
  sun.castShadow = true; sun.shadow.mapSize.set(4096, 4096); sun.shadow.bias = -0.0005; sun.shadow.normalBias = 0.03;
  Object.assign(sun.shadow.camera, { left: -28, right: 28, top: 28, bottom: -28, near: 1, far: 90 }); sun.shadow.camera.updateProjectionMatrix();
  scene.add(sun, sun.target);

  const floorTex = canvasTex(760, 1640, (g, w, h) => {
    const n = 40, r = rng(7);
    for (let i = 0; i < w / n; i++) for (let j = 0; j < h / n; j++) { g.fillStyle = (i + j) % 2 ? '#e7dcc9' : '#ded1ba'; g.fillRect(i * n, j * n, n, n); }
    for (let k = 0; k < 2500; k++) { g.fillStyle = `rgba(120,95,60,${0.02 + r() * 0.03})`; g.fillRect(r() * w, r() * h, 2 + r() * 14, 1); }
    g.strokeStyle = 'rgba(120,100,70,0.22)'; g.lineWidth = 1.5;
    for (let i = 0; i <= w / n; i++) { g.beginPath(); g.moveTo(i * n, 0); g.lineTo(i * n, h); g.stroke(); }
    for (let j = 0; j <= h / n; j++) { g.beginPath(); g.moveTo(0, j * n); g.lineTo(w, j * n); g.stroke(); }
  });
  const len = HALL.backZ - HALL.gateZ, midZ = (HALL.backZ + HALL.gateZ) / 2;
  scene.add(box(20, 0.7, len + 1, M.cut, 0, -0.36, midZ));
  const floor = mesh(new THREE.PlaneGeometry(19, len), std(0xffffff, { map: floorTex, roughness: 0.95 }), 0, 0, midZ); floor.rotation.x = -Math.PI / 2; floor.castShadow = false; scene.add(floor);

  // side walls, cut low so the hall can be looked into; pilasters, lanterns and windows on the inside
  for (const s of [-1, 1]) {
    scene.add(box(0.5, 3.2, len + 1, M.stone, s * 9.75, 1.6, midZ), box(0.52, 0.06, len + 1, M.cut, s * 9.75, 3.23, midZ));
    for (let z = -21.8; z < 15; z += 5.4) {
      scene.add(box(0.5, 3.7, 0.8, M.stone2, s * 9.3, 1.85, z), ball(0.14, M.glass, s * 8.95, 2.55, z));
      if (z + 2.7 < 14) { const win = mesh(new THREE.PlaneGeometry(1.7, 1.9), M.glass, s * 9.49, 1.85, z + 2.7, false); win.rotation.y = -s * Math.PI / 2; scene.add(win); }
    }
  }
  // the gate end
  const gz = HALL.gateZ - 0.25;
  scene.add(box(7, 5.6, 0.5, M.stone, -6, 2.8, gz), box(7, 5.6, 0.5, M.stone, 6, 2.8, gz), box(5, 1.2, 0.5, M.stone, 0, 5, gz), box(19.5, 0.06, 0.52, M.cut, 0, 5.63, gz));
  const archMat = M.stone2;
  const arch = mesh(new THREE.TorusGeometry(2.5, 0.28, 10, 30, Math.PI), archMat, 0, 4.4, gz + 0.3); scene.add(arch);
  scene.add(box(0.5, 4.4, 0.6, archMat, -2.75, 2.2, gz + 0.3), box(0.5, 4.4, 0.6, archMat, 2.75, 2.2, gz + 0.3));
  for (const s of [-1, 1]) {
    const hinge = new THREE.Group(); hinge.position.set(s * 2.5, 0, HALL.gateZ + 0.05);
    const leaf = box(2.5, 4.4, 0.18, M.woodDark, -s * 1.25, 2.2, 0); hinge.add(leaf);
    for (const y of [1.1, 2.2, 3.3]) hinge.add(box(2.3, 0.1, 0.22, M.gold, -s * 1.25, y, 0));
    hinge.add(ball(0.11, M.gold, -s * 2.2, 2.2, 0.14));
    scene.add(hinge); P[s < 0 ? 'leafL' : 'leafR'] = hinge;
  }
  P.glowMat = new THREE.MeshBasicMaterial({ color: 0xfff3c4, transparent: true, opacity: 0 });
  scene.add(mesh(new THREE.PlaneGeometry(5, 4.4), P.glowMat, 0, 2.2, HALL.gateZ - 1.2, false));
  P.spillMat = new THREE.MeshBasicMaterial({ color: 0xffe9a8, transparent: true, opacity: 0, depthWrite: false });
  const spillShape = new THREE.Shape(); spillShape.moveTo(-2.4, 0); spillShape.lineTo(2.4, 0); spillShape.lineTo(4.6, 9); spillShape.lineTo(-4.6, 9); spillShape.closePath();
  const spill = mesh(new THREE.ShapeGeometry(spillShape), P.spillMat, 0, 0.03, HALL.gateZ + 0.2, false); spill.rotation.x = Math.PI / 2; spill.scale.y = 1; scene.add(spill);
  P.gateLight = new THREE.PointLight(0xffd98a, 0, 26, 1.6); P.gateLight.position.set(0, 2.6, HALL.gateZ + 1.5); scene.add(P.gateLight);

  // the stair end: a wide stair up to a gallery, a round window above
  for (let i = 0; i < 9; i++) scene.add(box(6.4, 0.31 * (i + 1), 0.64, i % 2 ? M.stone2 : M.stone, 0, 0.155 * (i + 1), HALL.stairZ + 0.64 * i), box(3, 0.02, 0.64, M.wine, 0, 0.31 * (i + 1) + 0.011, HALL.stairZ + 0.64 * i));
  scene.add(box(19, 0.4, 4.3, M.stone2, 0, 2.6, 13.9), box(19.5, 6.4, 0.5, M.stone, 0, 3.2, HALL.backZ + 0.25), box(19.5, 0.06, 0.52, M.cut, 0, 6.43, HALL.backZ + 0.25));
  for (const s of [-1, 1]) { scene.add(box(6.3, 0.14, 0.24, M.stone, s * 6.35, 3.72, 11.9)); for (let x = 3.5; x < 9.4; x += 0.7) scene.add(box(0.16, 0.9, 0.16, M.stone, s * x, 3.25, 11.9)); }
  scene.add(mesh(new THREE.CircleGeometry(1.5, 40), M.glass, 0, 4.65, HALL.backZ - 0.01, false));
  const rose = mesh(new THREE.TorusGeometry(1.5, 0.12, 10, 40), M.gold, 0, 4.65, HALL.backZ); scene.add(rose);
  for (let i = 0; i < 4; i++) { const sp = box(2.9, 0.07, 0.07, M.gold, 0, 4.65, HALL.backZ + 0.01); sp.rotation.z = i * Math.PI / 4; scene.add(sp); }
  // carpet, and the stage board where the party stands
  scene.add(box(3, 0.02, 20.6, M.wine, 0, 0.011, -14.7), box(3, 0.02, 2.2, M.wine, 0, 0.011, 5.2));
  scene.add(box(8.2, 0.26, 8.2, M.woodDark, 0, 0.13, 0), box(7.5, 0.05, 7.5, M.gold, 0, 0.262, 0));
  const boardTex = canvasTex(512, 512, (g, w) => { const s = w / 8; for (let i = 0; i < 8; i++) for (let j = 0; j < 8; j++) { g.fillStyle = (i + j) % 2 ? '#b98a5c' : '#f0dfbb'; g.fillRect(i * s, j * s, s, s); } });
  const board = mesh(new THREE.PlaneGeometry(7.2, 7.2), std(0xffffff, { map: boardTex, roughness: 0.6 }), 0, 0.292, 0); board.rotation.x = -Math.PI / 2; board.castShadow = false; scene.add(board);

  // the installations: each is a group that can rise out of the floor
  const put = (name, at, yaw = 0) => { const g = new THREE.Group(); g.position.copy(at); g.rotation.y = yaw; scene.add(g); P.install[name] = g; return g; };
  const wd = put('wardrobe', HALL.wardrobe, Math.PI);          // its front faces the hall (towards -z)
  wd.add(box(2.6, 2.9, 0.9, M.wood, 0, 1.45, 0), box(2.8, 0.14, 1.0, M.woodDark, 0, 2.95, 0), box(1.05, 2.2, 0.06, M.mirror, -0.62, 1.5, 0.46), box(1.05, 2.2, 0.06, M.woodDark, 0.62, 1.5, 0.46), ball(0.07, M.gold, 0.2, 1.5, 0.52));
  const h1 = ITEM.party(); h1.position.set(-0.7, 3.02, 0.1); h1.scale.setScalar(1.6); const h2 = ITEM.top(); h2.position.set(0.5, 3.02, 0.1); h2.scale.setScalar(1.6); wd.add(h1, h2);
  const sh = put('shop', HALL.shop, Math.PI);
  const awn = canvasTex(256, 64, (g, w, h) => { for (let i = 0; i < 8; i++) { g.fillStyle = i % 2 ? '#f3e7d3' : '#97303f'; g.fillRect(i * w / 8, 0, w / 8, h); } });
  sh.add(box(3.4, 1.05, 1.0, M.wood, 0, 0.525, 0.3), box(3.6, 0.1, 1.2, M.woodDark, 0, 1.1, 0.3), box(3.4, 2.5, 0.3, M.woodDark, 0, 1.25, -0.5));
  for (const x of [-1.6, 1.6]) sh.add(cyl(0.07, 0.07, 2.7, M.woodDark, x, 1.35, 0.85, 10));
  const aw = box(3.8, 0.08, 1.7, std(0xffffff, { map: awn, roughness: 0.8 }), 0, 2.75, 0.45); aw.rotation.x = 0.28; sh.add(aw);
  const s1 = ITEM.top(); s1.position.set(-1.0, 1.15, 0.3); s1.scale.setScalar(1.5); const s2 = ITEM.flower(); s2.position.set(0, 1.15, 0.3); s2.scale.setScalar(1.8); const s3 = ITEM.party(); s3.position.set(1.0, 1.15, 0.3); s3.scale.setScalar(1.5); sh.add(s1, s2, s3);
  const pd = put('pads', HALL.pads);
  pd.add(box(5.5, 0.08, 5.5, M.woodDark, 0, 0.04, 0));
  for (let j = 0; j < 5; j++) for (let i = 0; i < 5; i++) {
    const m = box(0.9, 0.08, 0.9, std((i + j) % 2 ? 0xb98a5c : 0xf0dfbb, { roughness: 0.6, emissive: 0x000000 }), (i - 2) * PAD, 0.12, (j - 2) * PAD);
    m.userData.base = m.material.color.clone(); pd.add(m); P.pads.push(m);
  }
  const rg = put('ring', HALL.ring);
  rg.add(cyl(2.6, 2.7, 0.4, M.woodDark, 0, 0.2, 0, 48), cyl(2.4, 2.4, 0.04, M.cream, 0, 0.42, 0, 48));
  const stripe = mesh(new THREE.TorusGeometry(1.6, 0.07, 6, 48), M.wine, 0, 0.445, 0); stripe.rotation.x = Math.PI / 2; rg.add(stripe);
  const stripe2 = mesh(new THREE.TorusGeometry(2.3, 0.07, 6, 48), M.wine, 0, 0.445, 0); stripe2.rotation.x = Math.PI / 2; rg.add(stripe2);

  P.stars = new Stars(scene);
  return { scene, P, actors: {} };
}

function buildStudio() {
  const scene = new THREE.Scene();
  scene.add(new THREE.HemisphereLight(0xfff6e6, 0xdccab0, 1.6));
  const sun = new THREE.DirectionalLight(0xfff0d6, 2.4); sun.position.set(5, 9, 6); sun.castShadow = true; sun.shadow.mapSize.set(2048, 2048); sun.shadow.bias = -0.0005; sun.shadow.normalBias = 0.02;
  Object.assign(sun.shadow.camera, { left: -7, right: 7, top: 7, bottom: -7, near: 1, far: 30 }); sun.shadow.camera.updateProjectionMatrix();
  scene.add(sun);
  const P = {};
  P.floor = mesh(new THREE.CircleGeometry(1, 72), std(0xe6d9c2, { roughness: 1 }), 0, 0, 0); P.floor.rotation.x = -Math.PI / 2; P.floor.castShadow = false; scene.add(P.floor);
  P.rim = mesh(new THREE.RingGeometry(0.985, 1, 72), std(0xcdbb9c, { roughness: 1 }), 0, 0.004, 0, false); P.rim.rotation.x = -Math.PI / 2; scene.add(P.rim);
  // a running lane: stripes that slide under a piece running on the spot
  P.lane = new THREE.Group(); scene.add(P.lane); P.stripes = [];
  for (let i = 0; i < 7; i++) { const s = mesh(new THREE.PlaneGeometry(1.1, 0.16), new THREE.MeshBasicMaterial({ color: 0xab9571, transparent: true, opacity: 0.9 }), 0, 0.006, 0, false); s.rotation.x = -Math.PI / 2; P.lane.add(s); P.stripes.push(s); }
  P.stars = new Stars(scene);
  return { scene, P, actors: {} };
}

export class World {
  constructor(canvas) {
    const r = this.r = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, preserveDrawingBuffer: true });
    r.setPixelRatio(1); r.setSize(W, H, false); r.setClearColor(0x000000, 0);
    r.shadowMap.enabled = true; r.shadowMap.type = THREE.PCFSoftShadowMap;
    r.outputColorSpace = THREE.SRGBColorSpace; r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = 1.0;
    this.cam = new THREE.PerspectiveCamera(40, W / H, 0.1, 300);
    this.hall = buildHall(); this.studio = buildStudio();
    this.canvas = canvas; this.tmp = V();
  }
  /** A character in a set, made on first use: world.actor('hall', 'me', 'pawn', 'white'). */
  actor(set, id, kind = 'pawn', team = 'white') {
    const s = this[set]; return (s.actors[id] ??= (() => { const a = new Actor(kind, team); a.g.visible = false; s.scene.add(a.g); return a; })());
  }
  /** Before a scene draws: nobody on stage, the hall as it stands when idle. */
  reset() {
    for (const set of [this.hall, this.studio]) { for (const k in set.actors) { const a = set.actors[k]; a.g.visible = false; a.g.rotation.set(0, 0, 0); a.g.scale.setScalar(1); if (a.shell) a.shell.visible = false; } set.P.stars.hide(); }
    const P = this.hall.P;
    for (const k in P.install) { P.install[k].scale.set(1, 1, 1); P.install[k].visible = true; }
    for (const m of P.pads) { m.material.color.copy(m.userData.base); m.material.emissive.setHex(0); m.position.y = 0.12; }
    this.gate(0);
    const S = this.studio.P; S.floor.scale.setScalar(3.2); S.rim.scale.setScalar(3.2); S.floor.position.set(0, 0, 0); S.rim.position.set(0, 0.004, 0); S.lane.visible = false;
  }
  /** How far the great gate stands open, 0..1, with its light. */
  gate(k) {
    const P = this.hall.P, a = k * 1.75;
    P.leafL.rotation.y = a; P.leafR.rotation.y = -a;
    P.glowMat.opacity = clamp(k * 1.4, 0, 1); P.spillMat.opacity = 0.5 * k; P.gateLight.intensity = 90 * k;
  }
  /** An installation rising out of the floor, 0..1. */
  rise(name, k) { const g = this.hall.P.install[name]; g.visible = k > 0.001; g.scale.set(1, Math.max(0.001, k), 1); }
  /** A knight pad: 0 plain, 1 lit gold (visited), 2 glowing teal (may step there). */
  pad(i, j, state, pulse = 0) {
    const m = this.hall.P.pads[j * 5 + i];
    if (state === 1) { m.material.color.setHex(0xe9b93a); m.material.emissive.setHex(0x8a5a00); }
    if (state === 2) { m.material.color.setHex(0x59c9bf); m.material.emissive.setHex(0x0c5f58); m.position.y = 0.12 + 0.03 * pulse; }
  }
  view(px, py, pz, tx, ty, tz, fov = 40) { this.cam.fov = fov; this.cam.position.set(px, py, pz); this.cam.lookAt(tx, ty, tz); this.cam.updateProjectionMatrix(); this.cam.updateMatrixWorld(); }
  /** A world point on the screen, in stage pixels; z > 1 means behind the camera. */
  project(x, y, z) { const v = this.tmp.set(x, y, z).project(this.cam); return { x: (v.x + 1) / 2 * W, y: (1 - v.y) / 2 * H, z: v.z }; }
  render(set) { this.canvas.style.visibility = set ? 'visible' : 'hidden'; if (set) this.r.render(this[set].scene, this.cam); }
}
