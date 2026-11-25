// Ending summons previz - the shared look. The pieces (with the game's googly eyes), the crown, the player's hand,
// the sky, the materials and the easing below are copied from the ultimates previz (Tools/Previz/PawnRushUltimates
// on JY-skill-lab: stage.js, casts.js, 2026-11-21) so that this folder stands on its own branch.
import * as THREE from 'three';
export { THREE };
export const W = 1280, H = 720;


export const COL = { ally: 0x46d2ff, enemy: 0xff8a2a, gold: 0xffc94a, red: 0xff3030, holy: 0xfff0b0, violet: 0xb48cff, white: 0xffffff, cyan: 0x7fe9ff };

export const clamp = (x, a, b) => Math.min(b, Math.max(a, x));
export const lerp = (a, b, t) => a + (b - a) * t;
export const prog = (t, t0, t1) => clamp((t - t0) / (t1 - t0), 0, 1);
export const smooth = t => { t = clamp(t, 0, 1); return t * t * (3 - 2 * t); };
export const easeOut = t => { t = clamp(t, 0, 1); return 1 - Math.pow(1 - t, 3); };
export const easeIn = t => { t = clamp(t, 0, 1); return t * t * t; };
export const back = t => { t = clamp(t, 0, 1); const c = 1.7; return 1 + (c + 1) * Math.pow(t - 1, 3) + c * Math.pow(t - 1, 2); };
export const fade = (t, t0, t1, fi = 0.1, fo = 0.2) =>
  (t < t0 || t > t1) ? 0 : Math.min(fi > 0 ? prog(t, t0, t0 + fi) : 1, fo > 0 ? 1 - prog(t, t1 - fo, t1) : 1);
export const lerpAng = (a, b, t) => { const d = ((b - a) % (2 * Math.PI) + 3 * Math.PI) % (2 * Math.PI) - Math.PI; return a + d * t; };
export const V = (x = 0, y = 0, z = 0) => new THREE.Vector3(x, y, z);
/** Puts an object (and all inside it) on layer 1 too: it keeps its colour when the world goes black and white. */
export const keepColour = o => { o.traverse(m => m.layers.enable(1)); return o; };
export function rng(seed) {
  let s = (seed * 2654435761) >>> 0 || 1;
  return () => { s ^= s << 13; s >>>= 0; s ^= s >> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; };
}


// ---------- textures and materials ----------
function canvasTex(w, h, draw, srgb = true) {
  const c = document.createElement('canvas'); c.width = w; c.height = h;
  draw(c.getContext('2d'), w, h);
  const t = new THREE.CanvasTexture(c);
  if (srgb) t.colorSpace = THREE.SRGBColorSpace;
  return t;
}
export const TEX = {};
function makeTextures() {
  const radial = (stops) => canvasTex(128, 128, g => {
    const gr = g.createRadialGradient(64, 64, 0, 64, 64, 64);
    for (const [k, c] of stops) gr.addColorStop(k, c);
    g.fillStyle = gr; g.fillRect(0, 0, 128, 128);
  });
  TEX.dot = radial([[0, 'rgba(255,255,255,1)'], [0.4, 'rgba(255,255,255,0.55)'], [1, 'rgba(255,255,255,0)']]);
  TEX.flash = radial([[0, 'rgba(255,255,255,1)'], [0.2, 'rgba(255,250,220,1)'], [0.45, 'rgba(255,210,90,0.7)'], [1, 'rgba(255,150,40,0)']]);
  TEX.smoke = canvasTex(128, 128, g => {
    const r = rng(11);
    for (let k = 0; k < 14; k++) {
      const x = 40 + r() * 48, y = 40 + r() * 48, rad = 18 + r() * 26;
      const gr = g.createRadialGradient(x, y, 0, x, y, rad);
      gr.addColorStop(0, 'rgba(255,255,255,0.5)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
      g.fillStyle = gr; g.fillRect(0, 0, 128, 128);
    }
  });
  TEX.vgrad = canvasTex(4, 64, g => {
    const gr = g.createLinearGradient(0, 64, 0, 0);
    gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(0.55, 'rgba(255,255,255,0.4)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = gr; g.fillRect(0, 0, 4, 64);
  });
  TEX.board = canvasTex(512, 512, (g, w) => {
    const n = 8, s = w / n, r = rng(3);
    for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) {
      g.fillStyle = (i + j) % 2 ? '#b7875a' : '#efdbb3'; g.fillRect(i * s, j * s, s, s);
    }
    for (let k = 0; k < 4000; k++) { g.fillStyle = `rgba(90,55,25,${0.025 + r() * 0.04})`; g.fillRect(r() * w, r() * w, 2 + r() * 10, 1); }
    g.strokeStyle = 'rgba(70,45,20,0.35)'; g.lineWidth = 2;
    for (let i = 0; i <= n; i++) { g.beginPath(); g.moveTo(i * s, 0); g.lineTo(i * s, w); g.stroke(); g.beginPath(); g.moveTo(0, i * s); g.lineTo(w, i * s); g.stroke(); }
  });
  TEX.board.wrapS = TEX.board.wrapT = THREE.RepeatWrapping; TEX.board.anisotropy = 16;
  TEX.brick = canvasTex(256, 128, (g, w, h) => {
    g.fillStyle = '#8d8173'; g.fillRect(0, 0, w, h);
    const r = rng(5), bw = 64, bh = 32;
    for (let row = 0; row < h / bh; row++) for (let col = -1; col < w / bw + 1; col++) {
      const x = col * bw + (row % 2) * bw / 2, y = row * bh, v = 120 + r() * 40 | 0;
      g.fillStyle = `rgb(${v + 20},${v + 8},${v - 8})`; g.fillRect(x + 2, y + 2, bw - 4, bh - 4);
    }
  });
  TEX.brick.wrapS = TEX.brick.wrapT = THREE.RepeatWrapping;
}
export const MAT = {};
function makeMaterials() {
  MAT.white = new THREE.MeshPhysicalMaterial({ color: 0xf3ebdd, roughness: 0.36, clearcoat: 0.5, clearcoatRoughness: 0.25 });
  MAT.black = new THREE.MeshPhysicalMaterial({ color: 0x2a2421, roughness: 0.3, clearcoat: 0.85, clearcoatRoughness: 0.18 });
  MAT.gold = new THREE.MeshStandardMaterial({ color: 0xd9a637, metalness: 0.85, roughness: 0.28 });
  MAT.slit = new THREE.MeshStandardMaterial({ color: 0x3b2b20, roughness: 0.8 });
  MAT.ringAlly = new THREE.MeshStandardMaterial({ color: COL.ally, emissive: COL.ally, emissiveIntensity: 1.2, roughness: 0.4 });
  MAT.ringEnemy = new THREE.MeshStandardMaterial({ color: COL.enemy, emissive: COL.enemy, emissiveIntensity: 1.2, roughness: 0.4 });
  MAT.stone = new THREE.MeshStandardMaterial({ color: 0x9b958d, roughness: 0.95 });
  MAT.ghost = new THREE.MeshStandardMaterial({ color: 0xe6f8ff, emissive: 0x6fd8ff, emissiveIntensity: 0.8, transparent: true, opacity: 0.62, roughness: 0.3, depthWrite: false });
  MAT.giant = new THREE.MeshPhysicalMaterial({ color: 0xfff4e0, emissive: 0xffb830, emissiveIntensity: 0.22, roughness: 0.25, clearcoat: 1 });
  MAT.fire = new THREE.MeshStandardMaterial({ color: 0x3a1a0c, emissive: 0xff6a1a, emissiveIntensity: 1.6, roughness: 0.6 });
  MAT.wall = new THREE.MeshStandardMaterial({ map: TEX.brick, roughness: 0.92 });
  MAT.wallTop = new THREE.MeshStandardMaterial({ color: 0x9c8f7f, roughness: 0.9 });
  MAT.box = new THREE.MeshStandardMaterial({ color: 0xfaf3e6, roughness: 0.5 });
  MAT.ribbon = new THREE.MeshStandardMaterial({ color: 0xd0202a, roughness: 0.35, emissive: 0x400000 });
  MAT.dark = new THREE.MeshStandardMaterial({ color: 0x5a3a24, roughness: 0.8 });
}
export function basic(color, opacity = 1, extra = {}) {
  return new THREE.MeshBasicMaterial(Object.assign({ color, transparent: true, opacity, depthWrite: false, side: THREE.DoubleSide, toneMapped: false }, extra));
}
export const ADD = { blending: THREE.AdditiveBlending };


// ---------- pieces ----------
export const HEIGHT = { pawn: 0.9, rook: 1.1, knight: 1.25, bishop: 1.32, queen: 1.45, king: 1.62 };
export const NAME = { pawn: '폰', rook: '룩', knight: '나이트', bishop: '비숍', queen: '퀸', king: '킹' };
export const GLYPH = { pawn: '♟︎', knight: '♞︎', bishop: '♝︎', rook: '♜︎', queen: '♛︎', king: '♚︎' };
const BASE = [[0.0001, 0], [0.36, 0], [0.375, 0.035], [0.36, 0.07], [0.32, 0.09], [0.31, 0.12], [0.27, 0.14]];
function arc(cy, r, a0, a1, n = 10) {
  const o = []; for (let i = 0; i <= n; i++) { const a = lerp(a0, a1, i / n); o.push([Math.max(0.0001, Math.cos(a) * r), cy + Math.sin(a) * r]); } return o;
}
const PROFILE = {
  pawn: [...BASE, [0.2, 0.2], [0.15, 0.3], [0.12, 0.44], [0.2, 0.48], [0.21, 0.51], [0.19, 0.54], [0.11, 0.56], ...arc(0.72, 0.17, -1.2, Math.PI / 2)],
  rook: [...BASE, [0.25, 0.2], [0.22, 0.3], [0.22, 0.62], [0.25, 0.75], [0.3, 0.8], [0.31, 0.83], [0.28, 0.85], [0.3, 0.88], [0.3, 0.98], [0.22, 0.98], [0.22, 0.93], [0.0001, 0.93]],
  bishop: [...BASE, [0.23, 0.2], [0.18, 0.32], [0.15, 0.62], [0.16, 0.75], [0.24, 0.8], [0.25, 0.83], [0.17, 0.86], [0.13, 0.88], [0.16, 0.92], [0.185, 0.98], [0.185, 1.04], [0.165, 1.1], [0.125, 1.16], [0.07, 1.2], [0.03, 1.22], [0.03, 1.24], ...arc(1.28, 0.05, -0.9, Math.PI / 2, 6)],
  queen: [...BASE, [0.24, 0.2], [0.19, 0.32], [0.14, 0.7], [0.13, 0.95], [0.2, 1.0], [0.21, 1.03], [0.14, 1.06], [0.13, 1.1], [0.19, 1.25], [0.225, 1.3], [0.19, 1.31], [0.12, 1.32], [0.0001, 1.33]],
  king: [...BASE, [0.25, 0.2], [0.2, 0.32], [0.15, 0.72], [0.14, 1.0], [0.21, 1.05], [0.22, 1.08], [0.15, 1.11], [0.14, 1.15], [0.2, 1.3], [0.225, 1.34], [0.16, 1.36], [0.15, 1.4], [0.08, 1.44], [0.0001, 1.45]],
  knight: [...BASE, [0.25, 0.2], [0.22, 0.28], [0.25, 0.34], [0.0001, 0.36]],
};
const GEO = {};
function lathe(kind) {
  if (!GEO[kind]) { const g = new THREE.LatheGeometry(PROFILE[kind].map(p => new THREE.Vector2(p[0], p[1])), 48); g.computeVertexNormals(); GEO[kind] = g; }
  return GEO[kind];
}
function knightHead() {
  if (GEO.head) return GEO.head;
  const s = new THREE.Shape();
  const P = [[-0.2, 0.34], [-0.23, 0.6], [-0.2, 0.86], [-0.12, 1.02], [-0.05, 1.12], [-0.02, 1.24], [0.04, 1.13], [0.12, 1.05], [0.22, 0.93], [0.3, 0.82], [0.3, 0.73], [0.2, 0.69], [0.08, 0.66], [0.04, 0.6], [0.12, 0.48], [0.16, 0.34]];
  s.moveTo(P[0][0], P[0][1]); for (let i = 1; i < P.length; i++) s.lineTo(P[i][0], P[i][1]); s.closePath();
  const g = new THREE.ExtrudeGeometry(s, { depth: 0.24, bevelEnabled: true, bevelThickness: 0.04, bevelSize: 0.035, bevelSegments: 3, curveSegments: 4 });
  g.translate(0, 0, -0.12); g.rotateY(-Math.PI / 2); g.computeVertexNormals();
  return (GEO.head = g);
}
/** A chess piece; its base sits at y = 0. team 'ally' (white, blue ring), 'enemy' (black, orange ring) or null (no ring). */
export function makePiece(kind, team, mat, o = {}) {
  const g = new THREE.Group();
  const m = mat ?? (team === 'enemy' ? MAT.black : MAT.white);
  const acc = mat ?? MAT.gold;
  g.add(new THREE.Mesh(lathe(kind), m));
  if (kind === 'knight') g.add(new THREE.Mesh(knightHead(), m));
  if (kind === 'rook') for (let i = 0; i < 5; i++) {
    const a = i / 5 * Math.PI * 2, b = new THREE.Mesh(new THREE.BoxGeometry(0.13, 0.12, 0.1), m);
    b.position.set(Math.sin(a) * 0.26, 1.03, Math.cos(a) * 0.26); b.rotation.y = a; g.add(b);
  }
  if (kind === 'bishop') { const sl = new THREE.Mesh(new THREE.BoxGeometry(0.025, 0.2, 0.42), mat ?? MAT.slit); sl.position.set(0.03, 1.08, 0); sl.rotation.z = 0.6; g.add(sl); }
  if (kind === 'queen') {
    for (let i = 0; i < 9; i++) { const a = i / 9 * Math.PI * 2, b = new THREE.Mesh(new THREE.SphereGeometry(0.036, 12, 8), acc); b.position.set(Math.sin(a) * 0.21, 1.33, Math.cos(a) * 0.21); g.add(b); }
    const t = new THREE.Mesh(new THREE.SphereGeometry(0.07, 16, 12), acc); t.position.y = 1.39; g.add(t);
  }
  if (kind === 'king') {
    const v = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.24, 0.06), acc); v.position.y = 1.55;
    const h = new THREE.Mesh(new THREE.BoxGeometry(0.18, 0.06, 0.06), acc); h.position.y = 1.58; g.add(v, h);
  }
  if (o.cute) {   // the game's look: googly eyes and little ball hands
    const EYE = { pawn: [0.74, 0.16, 0.065], rook: [0.8, 0.235, 0.07], knight: [0.98, 0.1, 0.06], bishop: [0.93, 0.165, 0.06], queen: [1.03, 0.15, 0.06], king: [1.07, 0.16, 0.06] }[kind];
    const white = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.3 }), black = new THREE.MeshStandardMaterial({ color: 0x141414, roughness: 0.3 });
    for (const sx of [-1, 1]) {
      const e = new THREE.Mesh(new THREE.SphereGeometry(EYE[2], 14, 10), white); e.position.set(sx * (kind === 'knight' ? 0.15 : EYE[2] * 1.15), EYE[0], EYE[1]); g.add(e);
      const p = new THREE.Mesh(new THREE.SphereGeometry(EYE[2] * 0.52, 10, 8), black); p.position.set(e.position.x, EYE[0] - EYE[2] * 0.1, EYE[1] + EYE[2] * 0.62); g.add(p);
      const h = new THREE.Mesh(new THREE.SphereGeometry(0.1, 14, 10), new THREE.MeshStandardMaterial({ color: 0x1c1a18, roughness: 0.4 })); h.position.set(sx * 0.37, 0.3 + 0.12 * (HEIGHT[kind] - 0.9), 0.04); g.add(h);
    }
  }
  if (team) {
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.385, 0.035, 10, 48), team === 'ally' ? MAT.ringAlly : MAT.ringEnemy);
    ring.rotation.x = Math.PI / 2; ring.position.y = 0.045; ring.userData.ring = true; g.add(ring);
  }
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  return g;
}
export function makeCrown(r = 1, mat = MAT.gold) {
  const g = new THREE.Group();
  const band = new THREE.Mesh(new THREE.CylinderGeometry(r, r * 0.92, r * 0.5, 40, 1, true), mat); band.material.side = THREE.DoubleSide; g.add(band);
  for (let i = 0; i < 8; i++) {
    const a = i / 8 * Math.PI * 2, sp = new THREE.Mesh(new THREE.ConeGeometry(r * 0.16, r * 0.55, 8), mat);
    sp.position.set(Math.sin(a) * r * 0.96, r * 0.5, Math.cos(a) * r * 0.96); g.add(sp);
    const gem = new THREE.Mesh(new THREE.SphereGeometry(r * 0.07, 10, 8), MAT.ribbon); gem.position.set(Math.sin(a) * r * 0.98, r * 0.78, Math.cos(a) * r * 0.98); g.add(gem);
  }
  return g;
}

export function makeSky() {
  const m = new THREE.ShaderMaterial({
    side: THREE.BackSide, depthWrite: false, fog: false,
    uniforms: { top: { value: new THREE.Color(0x4f86de) }, mid: { value: new THREE.Color(0xc4dcf4) }, bot: { value: new THREE.Color(0xe9dcc8) }, dark: { value: 0 }, darkCol: { value: new THREE.Color(0x3a0806) } },
    vertexShader: 'varying vec3 vP; void main(){ vP = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
    fragmentShader: 'uniform vec3 top, mid, bot, darkCol; uniform float dark; varying vec3 vP; void main(){ float h = vP.y; vec3 c = h > 0.0 ? mix(mid, top, smoothstep(0.0, 0.55, h)) : mix(mid, bot, smoothstep(0.0, -0.2, h)); c = mix(c, darkCol * (0.6 + 0.6 * max(h, 0.0)), dark); gl_FragColor = vec4(c, 1.0); }',
  });
  const s = new THREE.Mesh(new THREE.SphereGeometry(1500, 32, 16), m); s.renderOrder = -10; s.frustumCulled = false;
  return s;
}

// ---------------------------------------------------------------- the player's hand from the sky
export function makeHand(o = {}) {
  const side = o.left ? -1 : 1;
  const skin = new THREE.MeshStandardMaterial({ color: 0xffe2d0, roughness: 0.5, emissive: 0x3a2418, emissiveIntensity: 0.35 });
  const cloth = new THREE.MeshStandardMaterial({ color: o.sleeve ?? 0xf1ede4, roughness: 0.85 });
  const root = new THREE.Group();
  const palm = new THREE.Mesh(new THREE.SphereGeometry(1, 28, 18), skin); palm.scale.set(2.15, 0.72, 2.45); palm.position.set(0, 0, 2.35); root.add(palm);
  const kn = new THREE.Mesh(new THREE.CapsuleGeometry(0.55, 3.3, 4, 12), skin); kn.rotation.z = Math.PI / 2; kn.position.set(0, -0.05, 4.3); root.add(kn);
  const fingers = [];
  [[-1.45, 1.4, 0.36], [-0.48, 1.7, 0.41], [0.48, 1.62, 0.41], [1.42, 1.25, 0.34]].forEach(([x, L0, r]) => {
    const j0 = new THREE.Group(); j0.position.set(x * side, 0, 4.5); root.add(j0);
    const js = [j0]; let parent = j0;
    [L0, L0 * 0.72, L0 * 0.56].forEach((L, i) => {
      const cap = new THREE.Mesh(new THREE.CapsuleGeometry(r * (1 - i * 0.1), L, 4, 10), skin); cap.rotation.x = Math.PI / 2; cap.position.z = L / 2; parent.add(cap);
      if (i < 2) { const j = new THREE.Group(); j.position.z = L; parent.add(j); js.push(j); parent = j; }
    });
    fingers.push(js);
  });
  const t0 = new THREE.Group(); t0.position.set(-2.05 * side, -0.3, 1.5); root.add(t0);
  const tj = [t0]; let tp = t0;
  [1.5, 1.05].forEach((L, i) => { const cap = new THREE.Mesh(new THREE.CapsuleGeometry(0.47 - i * 0.05, L, 4, 10), skin); cap.rotation.x = Math.PI / 2; cap.position.z = L / 2; tp.add(cap); if (i < 1) { const j = new THREE.Group(); j.position.z = L; tp.add(j); tj.push(j); tp = j; } });
  const cuff = new THREE.Mesh(new THREE.CylinderGeometry(1.95, 1.95, 1.1, 28), new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.6 })); cuff.rotation.x = Math.PI / 2; cuff.position.z = -0.45; root.add(cuff);
  const sleeve = new THREE.Mesh(new THREE.CylinderGeometry(1.8, 2.0, 12, 28), cloth); sleeve.rotation.x = Math.PI / 2; sleeve.position.z = -7; root.add(sleeve);
  const link = new THREE.Mesh(new THREE.SphereGeometry(0.3, 12, 8), MAT.gold); link.position.set(1.95 * side, 0, -0.45); root.add(link);
  root.traverse(m => { if (m.isMesh) { m.castShadow = true; m.receiveShadow = true; } });
  const pose = c => {
    for (const js of fingers) js.forEach((j, i) => { j.rotation.x = c * [0.8, 1.05, 0.85][i]; });
    tj[0].rotation.set(0.35 + c * 0.5, side * (-0.7 + c * 0.55), 0); tj[1].rotation.x = c * 0.7;
  };
  pose(0.15);
  return { root, pose, tip: V(0, -0.9, 6.3), scale: o.scale ?? 2.4 };
}

// Textures and materials once, before anything is built.
export function initLook() { makeTextures(); makeMaterials(); }
