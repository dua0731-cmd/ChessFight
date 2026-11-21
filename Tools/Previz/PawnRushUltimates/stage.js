// Pawn Rush ultimates previz - the stage: course, pieces, motion, effects, camera and grade.
// Every picture depends only on the time (same time = same picture), so frames can be rendered one by one.
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { Pass } from 'three/addons/postprocessing/Pass.js';
import { drawHud, pipRect } from './hud.js';
export { THREE };

export const W = 1280, H = 720;
export const HALF = 8, SQ = 2, LEN = 240;          // course: 16 m wide (8 squares), crown at z = 240
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
export const cellX = i => -HALF + SQ / 2 + i * SQ;           // column 0..7 -> centre x
export const snap = v => Math.floor(v / SQ) * SQ + SQ / 2;    // a coordinate -> its square's centre
const at = (p, s) => (typeof p === 'function' ? p(s) : p);

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

// ---------- the course ----------
function buildCourse(scene) {
  const z0 = -40, L = LEN + 90;
  TEX.board.repeat.set(1, L / 16);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(HALF * 2, L), new THREE.MeshStandardMaterial({ map: TEX.board, roughness: 0.62 }));
  floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, z0 + L / 2); floor.receiveShadow = true; scene.add(floor);
  const slab = new THREE.Mesh(new THREE.BoxGeometry(HALF * 2 + 0.5, 1.6, L), MAT.dark);
  slab.position.set(0, -0.805, z0 + L / 2); slab.receiveShadow = true; scene.add(slab);
  const rim = new THREE.MeshStandardMaterial({ color: 0xd7a645, metalness: 0.7, roughness: 0.32 });
  for (const sx of [-1, 1]) {
    const r = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.22, L), rim); r.position.set(sx * (HALF + 0.12), 0.05, z0 + L / 2); r.castShadow = true; scene.add(r);
  }
  // posts every 16 m: they show the speed
  const postM = new THREE.MeshStandardMaterial({ color: 0x4a3020, roughness: 0.7 });
  for (let z = 0; z < LEN; z += 16) for (const sx of [-1, 1]) {
    const p = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.2, 1.6, 12), postM); p.position.set(sx * (HALF + 0.55), 0.8, z); p.castShadow = true; scene.add(p);
    const c = new THREE.Mesh(new THREE.SphereGeometry(0.2, 12, 8), MAT.gold); c.position.set(sx * (HALF + 0.55), 1.7, z); scene.add(c);
  }
  // the palace and its crown at the end
  for (const [x, s] of [[-9, 9], [9, 9], [0, 12]]) {
    const t = makePiece('rook', null, MAT.stone); t.scale.setScalar(s); t.position.set(x, -0.2, LEN + 22 + (x === 0 ? 8 : 0)); scene.add(t);
  }
  const crown = makeCrown(2.4); crown.position.set(0, 18, LEN + 26); scene.add(crown);
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(6000, 6000), new THREE.MeshStandardMaterial({ color: 0x9fb6cf, roughness: 1 }));
  ground.rotation.x = -Math.PI / 2; ground.position.y = -60; scene.add(ground);
}
function makeSky() {
  const m = new THREE.ShaderMaterial({
    side: THREE.BackSide, depthWrite: false, fog: false,
    uniforms: { top: { value: new THREE.Color(0x4f86de) }, mid: { value: new THREE.Color(0xc4dcf4) }, bot: { value: new THREE.Color(0xe9dcc8) }, dark: { value: 0 }, darkCol: { value: new THREE.Color(0x3a0806) } },
    vertexShader: 'varying vec3 vP; void main(){ vP = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
    fragmentShader: 'uniform vec3 top, mid, bot, darkCol; uniform float dark; varying vec3 vP; void main(){ float h = vP.y; vec3 c = h > 0.0 ? mix(mid, top, smoothstep(0.0, 0.55, h)) : mix(mid, bot, smoothstep(0.0, -0.2, h)); c = mix(c, darkCol * (0.6 + 0.6 * max(h, 0.0)), dark); gl_FragColor = vec4(c, 1.0); }',
  });
  const s = new THREE.Mesh(new THREE.SphereGeometry(1500, 32, 16), m); s.renderOrder = -10; s.frustumCulled = false;
  return s;
}
const GradeShader = {
  uniforms: { tDiffuse: { value: null }, dimW: { value: 1 }, blur: { value: new THREE.Vector2(0, 0) }, rgb: { value: 0 }, mask: { value: null }, maskOn: { value: 0 }, contrast: { value: 1 }, sat: { value: 1 }, tint: { value: new THREE.Vector3(1, 1, 1) }, vig: { value: 0.18 }, vigCol: { value: new THREE.Vector3(0, 0, 0) }, flashCol: { value: new THREE.Vector3(1, 1, 1) }, flashA: { value: 0 } },
  vertexShader: 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
  fragmentShader: `uniform sampler2D tDiffuse, mask; uniform vec2 blur; uniform float sat, vig, flashA, maskOn, contrast, rgb, dimW; uniform vec3 tint, vigCol, flashCol; varying vec2 vUv;
    void main(){ vec4 c = vec4(0.0); vec3 e = vec3(0.0);
      if (blur.x != 0.0 || blur.y != 0.0) { for (int i = 0; i < 11; i++) { vec2 q = vUv + blur * (float(i) / 10.0 - 0.5); c += texture2D(tDiffuse, q); e += texture2D(mask, q).rgb; } c /= 11.0; e /= 11.0; } else { c = texture2D(tDiffuse, vUv); e = texture2D(mask, vUv).rgb; }
      if (rgb > 0.0) { vec2 o = vec2(rgb, rgb * 0.3); c.r = texture2D(tDiffuse, vUv + o).r; c.b = texture2D(tDiffuse, vUv - o).b; e.r = texture2D(mask, vUv + o).r; e.b = texture2D(mask, vUv - o).b; }
      e *= maskOn; vec3 w = max(c.rgb - e, vec3(0.0));
      float l = dot(w, vec3(0.2126, 0.7152, 0.0722));
      vec3 gray = 0.18 * pow(vec3(l) / 0.18, vec3(contrast));
      c.rgb = mix(gray, w, sat) * tint * dimW + e;
      vec2 d = (vUv - 0.5) * vec2(1.5, 1.0); float v = smoothstep(0.32, 0.9, length(d));
      c.rgb = mix(c.rgb, vigCol, clamp(vig * v, 0.0, 1.0));
      c.rgb = mix(c.rgb, flashCol, flashA); gl_FragColor = c; }`,
};

/** What keeps its colour in a grey world (objects on layer 1), drawn alone in their own colours behind the
 *  world's depth, so the grade can take exactly their light out of the picture, grey the rest and add it back:
 *  a teal glow stays teal instead of tinting the sand under it. Runs inside the composer, after the main
 *  render, so it reuses that render's shadow map. */
const lightsOn1 = sc => sc.traverse(o => { if (o.isLight) { o.layers.enable(1); o.layers.enable(2); } });
class KeepPass extends Pass {
  constructor(st) { super(); this.st = st; this.needsSwap = false; }
  render(r) {
    const st = this.st; if (!st.keepOn) return;
    const sc = st.scene, cam = st.camera, hidden = [];
    lightsOn1(sc);
    sc.traverse(o => { if (!o.visible || o.isLight || o === sc) return; const m = o.material, see = m && (Array.isArray(m) ? m.some(x => x.transparent) : m.transparent);
      if ((o.layers.mask & 2) || see || o.isSprite || o.isPoints) { o.visible = false; hidden.push(o); } });
    const auto = r.shadowMap.autoUpdate, ac = r.autoClear; r.shadowMap.autoUpdate = false;
    r.setRenderTarget(st.maskRT); r.setClearColor(0x000000, 1); r.clear();
    sc.overrideMaterial = st.depthMat; r.autoClear = false; r.render(sc, cam); sc.overrideMaterial = null;
    for (const o of hidden) o.visible = true;
    cam.layers.set(1); r.render(sc, cam); cam.layers.set(0);
    r.autoClear = ac; r.shadowMap.autoUpdate = auto;
  }
}

// ---------- actors: a piece and its plan, a chain of steps that each start where the last one ended ----------
const S0 = () => ({ x: 0, y: 0, z: 0, yaw: 0, pitch: 0, roll: 0, s: 1, sq: 1, gait: 0, run: 0, stone: 0, hide: 0 });
const EUL = new THREE.Euler();
let seq = 0;
export class Actor {
  constructor(st, kind, team, x, z, o = {}) {
    this.st = st; this.kind = kind; this.team = team; this.h = HEIGHT[kind]; this.strip = o.strip ?? !!team;
    this.pivot = new THREE.Group(); this.body = makePiece(kind, team, o.mat, { cute: o.cute ?? st.cute }); this.body.position.y = -this.h / 2; this.pivot.add(this.body);
    st.dyn.add(this.pivot);
    this.meshes = []; this.body.traverse(m => { if (m.isMesh && !m.userData.ring) this.meshes.push([m, m.material]); });
    this.speed = o.speed ?? 6;
    this.segs = []; this.t = 0;
    this.end = Object.assign(S0(), { x, z, yaw: o.yaw ?? 0, gait: (seq++ * 1.37) % 6, run: 1 });
    this.first = { ...this.end }; this.now = this.first; this.isStone = false;
  }
  seg(d, f) { const s0 = { ...this.end }, t0 = this.t; this.segs.push({ t0, t1: t0 + d, d, s0, f }); this.end = f(1, d, s0); this.t += d; return this; }
  at(s) {
    if (!this.segs.length || s <= 0) return this.segs.length ? this.segs[0].f(0, 0, this.segs[0].s0) : this.first;
    for (const g of this.segs) if (s < g.t1) return g.f(g.d > 0 ? (s - g.t0) / g.d : 1, s - g.t0, g.s0);
    return this.end;
  }
  /** Where it will be at time s if it keeps running from the end of its plan so far. */
  pred(s) { const e = this.end; return s <= this.t ? this.at(s) : { ...e, z: e.z + (e.run ? this.speed * (s - this.t) : 0) }; }
  pos(s, dy = 0) { const a = this.at(s); return V(a.x, a.y + this.h * 0.5 * a.s + dy, a.z); }
  head(s) { const a = this.at(s); return V(a.x, a.y + this.h * a.s * a.sq, a.z); }

  set(f) { return this.seg(0, (u, t, a) => ({ ...a, ...f })); }
  hold(d) { return this.seg(d, (u, t, a) => a); }
  until(s, o) { return s > this.t ? this.run(s - this.t, o) : this; }
  run(d, o = {}) {
    const v = o.speed ?? this.speed;
    return this.seg(d, (u, t, a) => ({ ...a, x: o.x === undefined ? a.x : lerp(a.x, o.x, smooth(u)), z: a.z + v * t, y: 0, pitch: 0, roll: 0, sq: 1,
      yaw: lerpAng(a.yaw, o.yaw ?? 0, smooth(prog(t, 0, 0.25))), gait: a.gait + v * t, run: 1 }));
  }
  stand(d) { return this.seg(d, (u, t, a) => ({ ...a, run: 0, y: 0, pitch: 0, roll: 0 })); }
  to(d, x, z, o = {}) {
    return this.seg(d, (u, t, a) => { const k = (o.ease ?? (q => q))(u), dist = Math.hypot(x - a.x, z - a.z);
      return { ...a, x: lerp(a.x, x, k), z: lerp(a.z, z, k), gait: a.gait + dist * k, run: 1, y: 0, pitch: 0, roll: 0, yaw: o.face ? Math.atan2(x - a.x, z - a.z) : lerpAng(a.yaw, 0, smooth(u)) }; });
  }
  /** Thrown: an arc with tumbles; lands lying (or below the board with o.y1 < 0). */
  fly(d, x, z, o = {}) {
    const h = o.h ?? 3, spins = o.spins ?? 1;
    return this.seg(d, (u, t, a) => {
      const yaw = o.yaw ?? Math.atan2(x - a.x, z - a.z);
      const p0 = a.pitch % (Math.PI * 2), pe = spins * Math.PI * 2 + Math.PI / 2;
      const y0 = a.y, y1 = o.y1 ?? 0;
      return { ...a, x: lerp(a.x, x, u), z: lerp(a.z, z, u), y: lerp(y0, y1, o.y1 !== undefined ? u * u : u) + 4 * h * u * (1 - u),
        yaw: lerpAng(a.yaw, yaw, smooth(prog(u, 0, 0.12))), pitch: lerp(p0, pe, o.ease ? o.ease(u) : u), run: 0, sq: 1, roll: (o.roll ?? 0.5) * Math.sin(u * Math.PI) };
    });
  }
  lie(d, o = {}) {
    const sl = o.slide ?? 1.0;
    return this.seg(d, (u, t, a) => { const k = 1 - Math.exp(-t * 5);
      return { ...a, x: a.x + Math.sin(a.yaw) * sl * k, z: a.z + Math.cos(a.yaw) * sl * k, y: 0.3 * Math.abs(Math.sin(t * 9)) * Math.exp(-t * 6),
        pitch: Math.PI / 2, roll: 0.08 * Math.sin(t * 3.1), run: 0, sq: 1 }; });
  }
  getUp(d = 0.5) {
    return this.seg(d, (u, t, a) => ({ ...a, pitch: lerp(a.pitch % (Math.PI * 2), 0, back(u)), y: 0.45 * Math.sin(u * Math.PI), yaw: lerpAng(a.yaw, 0, smooth(u)), roll: 0, run: 0 }));
  }
  /** Pulled along the floor, lying, feet first. */
  drag(d, x, z, o = {}) {
    return this.seg(d, (u, t, a) => { const k = (o.ease ?? smooth)(u), face = Math.atan2(a.x - x, a.z - z), e = smooth(prog(u, 0, 0.1));
      return { ...a, x: lerp(a.x, x, k), z: lerp(a.z, z, k), y: lerp(a.y, 0, e) + 0.12 * Math.abs(Math.sin(t * 13)) * (1 - u),
        pitch: lerp(a.pitch, Math.PI / 2, e), yaw: lerpAng(a.yaw, face, e), run: 0, roll: 0.15 * Math.sin(t * 9) }; });
  }
  /** An upright jump (the knight's). */
  hop(d, x, z, o = {}) {
    const h = o.h ?? 2.5, y1 = o.y1 ?? 0;
    return this.seg(d, (u, t, a) => ({ ...a, x: lerp(a.x, x, u), z: lerp(a.z, z, u), y: lerp(a.y, y1, u) + 4 * h * u * (1 - u),
      yaw: o.yaw ?? (Math.hypot(x - a.x, z - a.z) > 0.01 ? Math.atan2(x - a.x, z - a.z) : a.yaw), pitch: 0.3 * Math.sin(u * Math.PI), roll: 0, run: 0, sq: 1 }));
  }
  /** Pancaked flat for d seconds. */
  flat(d) {
    return this.seg(d, (u, t, a) => { const i = 0.07, o = 0.35; let sq;
      if (t < i) sq = lerp(1, 0.18, t / i); else if (t > d - o) sq = lerp(0.18, 1, back(prog(t, d - o, d))); else sq = 0.18 + 0.04 * Math.sin(t * 22) * Math.exp(-(t - i) * 5);
      return { ...a, sq, run: 0, pitch: 0, roll: 0, y: a.y > 0.5 ? a.y : 0 }; });
  }
  stoneFor(d) { return this.seg(d, (u, t, a) => ({ ...a, stone: u < 1 ? 1 : 0 })); }
  /** Standing still, trying not to move (wobbling on the spot). */
  wobble(d, o = {}) {
    const k = o.amp ?? 0.14;
    return this.seg(d, (u, t, a) => ({ ...a, run: 0, y: 0, pitch: k * Math.sin(t * 7.3) * Math.exp(-t * 0.7) + 0.03 * Math.sin(t * 2.3), roll: k * 0.8 * Math.sin(t * 5.1 + 1) * Math.exp(-t * 0.6) }));
  }
  custom(d, f) { return this.seg(d, f); }

  update(s) {
    const a = this.at(s); this.now = a;
    const p = this.pivot; p.visible = !a.hide && !(this.st.pieceHide && this.st.pieceHide(s, this.st.nowC)); if (!p.visible) return;
    let y = a.y, pitch = a.pitch, roll = a.roll;
    if (a.run > 0) { const g = a.gait * 1.55; y += 0.11 * Math.abs(Math.sin(g)) * a.run; roll += 0.09 * Math.sin(g) * a.run; pitch += 0.13 * a.run; }
    const cy = (this.h / 2 * a.sq * Math.abs(Math.cos(pitch)) + 0.33 * Math.abs(Math.sin(pitch))) * a.s;
    p.position.set(a.x, y + cy, a.z);
    p.quaternion.setFromEuler(EUL.set(pitch, a.yaw, roll, 'YXZ'));
    const sx = a.s / Math.sqrt(a.sq); p.scale.set(sx, a.s * a.sq, sx);
    const stone = a.stone > 0;
    if (stone !== this.isStone) { this.isStone = stone; for (const [m, mat] of this.meshes) m.material = stone ? MAT.stone : mat; }
  }
}

// ---------- the stage: one clip at a time ----------
export class Stage {
  constructor(gl, hud) {
    const r = this.renderer = new THREE.WebGLRenderer({ canvas: gl, antialias: false, preserveDrawingBuffer: true, powerPreference: 'high-performance' });
    r.setPixelRatio(1); r.setSize(W, H, false);
    r.outputColorSpace = THREE.SRGBColorSpace; r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = 1.0;
    r.shadowMap.enabled = true; r.shadowMap.type = THREE.PCFSoftShadowMap;
    const sc = this.scene = new THREE.Scene(); sc.fog = new THREE.Fog(0xd2deec, 120, 560);
    this.camera = new THREE.PerspectiveCamera(50, W / H, 0.1, 4000);
    makeTextures(); makeMaterials(); this.course = new THREE.Group(); sc.add(this.course); buildCourse(this.course);
    this.sky = makeSky(); sc.add(this.sky);
    this.hemi = new THREE.HemisphereLight(0xe8f0ff, 0x7a6450, 1.15); sc.add(this.hemi);
    const sun = this.sun = new THREE.DirectionalLight(0xfff0dc, 2.7); sun.castShadow = true; sun.shadow.mapSize.set(2048, 2048);
    Object.assign(sun.shadow.camera, { left: -45, right: 45, top: 45, bottom: -45, near: 1, far: 260 }); sun.shadow.bias = -0.0004; sun.shadow.normalBias = 0.03;
    sc.add(sun, sun.target);
    const rt = new THREE.WebGLRenderTarget(W, H, { type: THREE.HalfFloatType, samples: 4 });
    const cp = this.composer = new EffectComposer(r, rt);
    // what keeps its colour when the world goes grey: things on layer 1, in their own colours (see KeepPass)
    this.maskRT = new THREE.WebGLRenderTarget(W, H, { type: THREE.HalfFloatType, samples: 4 }); this.depthMat = new THREE.MeshBasicMaterial({ colorWrite: false });
    cp.addPass(new RenderPass(sc, this.camera));
    cp.addPass(new KeepPass(this));
    // grade before bloom: a world drained to grey still glows in the colours of what kept them
    this.grade = new ShaderPass(GradeShader); this.grade.uniforms.mask.value = this.maskRT.texture; cp.addPass(this.grade);
    this.bloom = new UnrealBloomPass(new THREE.Vector2(W, H), 0.55, 0.55, 0.86); cp.addPass(this.bloom);
    cp.addPass(new OutputPass());
    this.g = hud.getContext('2d');
    this.dyn = new THREE.Group(); sc.add(this.dyn);
  }
  load(clip) {
    this.scene.remove(this.dyn); this.dyn = new THREE.Group(); this.scene.add(this.dyn);
    Object.assign(this, { actors: [], fx: [], shots: [], caps: [], stamps: [], labels: [], shakes: [], flashes: [], gradeFn: null, skyFn: null, specAt: null, cracks: [], evalOn: false, evalKick: null, camMods: [], pieceHide: null, feed: [], typing: [], lightFn: null, banners: [], voices: [], music: [], cute: false, clip });
    this.knots = clip.warp ?? [[0, 0], [3.6, 3.6], [4.6, 3.7], [64.6, 63.7]]; this.dur = clip.dur;
    this.sky.material.uniforms.darkCol.value.set(0x3a0806); this.course.visible = true; this.scene.fog.color.set(0xd2deec);
    clip.build(this);
    this.shots.sort((a, b) => a.c0 - b.c0);
  }
  /** clip time -> scene time (slow motion and the cut-in's freeze) */
  S(c) { const k = this.knots; for (let i = 1; i < k.length; i++) if (c <= k[i][0]) return lerp(k[i - 1][1], k[i][1], prog(c, k[i - 1][0], k[i][0])); const l = k[k.length - 1]; return l[1] + c - l[0]; }
  /** scene time -> clip time */
  C(s) { const k = this.knots; for (let i = 1; i < k.length; i++) if (s <= k[i][1] && k[i][1] > k[i - 1][1]) return lerp(k[i - 1][0], k[i][0], prog(s, k[i - 1][1], k[i][1])); const l = k[k.length - 1]; return l[0] + s - l[1]; }

  // --- building blocks for the clips ---
  actor(kind, team, x, z, o) { const a = new Actor(this, kind, team, x, z, o); this.actors.push(a); return a; }
  obj(o, upd) { if (o) this.dyn.add(o); this.fx.push({ o, upd }); return o; }
  shot(c0, fn, o = {}) { this.shots.push({ c0, fn, blend: o.blend ?? 0 }); }
  cap(c0, c1, text, memo) { this.caps.push({ c0, c1, text, memo }); }
  stamp(c0, d, text, o = {}) { this.stamps.push({ c0, d, text, ...o }); }
  label(pos, s0, s1, text, o = {}) { this.labels.push({ pos, s0, s1, text, ...o }); }
  shake(s0, d, amp) { this.shakes.push({ s0, d, amp }); }
  screen(s0, d, color = [1, 1, 1], a = 0.8) { this.flashes.push({ s0, d, color, a }); }

  ring(x, z, s0, d, r0, r1, color, o = {}) {
    const w = o.w ?? 0.12;
    const m = new THREE.Mesh(new THREE.RingGeometry(1 - w, 1, 96), basic(color, 1, ADD)); m.rotation.x = -Math.PI / 2; m.position.set(x, o.y ?? 0.06, z);
    return this.obj(m, s => { const u = (s - s0) / d; m.visible = u >= 0 && u <= 1; if (!m.visible) return; const r = lerp(r0, r1, easeOut(u)); m.scale.set(r, r, r); m.material.opacity = (o.op ?? 1) * (o.keep ? 1 - prog(u, 0.8, 1) : Math.pow(1 - u, 1.4)); });
  }
  pillar(x, z, s0, d, r, h, color, o = {}) {
    const m = new THREE.Mesh(new THREE.CylinderGeometry(r, r * (o.taper ?? 1), 1, 32, 1, true), basic(color, 1, { ...ADD, map: TEX.vgrad }));
    m.position.set(x, 0, z);
    return this.obj(m, s => { const u = (s - s0) / d; m.visible = u >= 0 && u <= 1; if (!m.visible) return;
      const hh = Math.max(0.01, h * easeOut(prog(u, 0, o.rise ?? 0.2))); const sp = 1 + u * (o.spread ?? 0.25);
      m.scale.set(sp, hh, sp); m.position.y = (o.y ?? 0) + hh / 2; m.material.opacity = (o.op ?? 0.9) * (1 - prog(u, o.hold ?? 0.55, 1)); });
  }
  flash(pos, s0, d, size, color = 0xffffff, o = {}) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: o.tex ?? TEX.flash, color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false, depthTest: o.depthTest ?? true }));
    return this.obj(sp, s => { const u = (s - s0) / d; sp.visible = u >= 0 && u <= 1; if (!sp.visible) return; sp.position.copy(at(pos, s));
      const k = size * (0.35 + 0.65 * easeOut(u * 2.5)); sp.scale.set(k, k, k); sp.material.opacity = (o.op ?? 1) * Math.pow(1 - u, 1.6); });
  }
  /** A burst of sparks with simple ballistic motion (pos may follow something until s0). */
  sparks(pos, s0, n, o = {}) {
    const r = rng(o.seed ?? ((s0 * 977) | 0) + n * 7), life = o.life ?? 1.1, g = o.grav ?? 9, sp = o.speed ?? 8;
    const vel = []; for (let i = 0; i < n; i++) {
      const th = r() * Math.PI * 2, up = (o.up ?? 0.6) + r() * (1 - (o.up ?? 0.6)), k = sp * (0.35 + 0.65 * r()), hz = Math.sqrt(Math.max(0, 1 - up * up));
      vel.push([Math.cos(th) * hz * k + (o.dir?.x ?? 0), up * k + (o.dir?.y ?? 0), Math.sin(th) * hz * k + (o.dir?.z ?? 0)]);
    }
    const arr = new Float32Array(n * 3), geo = new THREE.BufferGeometry(); geo.setAttribute('position', new THREE.BufferAttribute(arr, 3));
    const mat = new THREE.PointsMaterial({ map: TEX.dot, color: o.color ?? COL.gold, size: o.size ?? 0.4, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false });
    const pts = new THREE.Points(geo, mat); pts.frustumCulled = false;
    return this.obj(pts, s => { const t = s - s0; pts.visible = t >= 0 && t <= life; if (!pts.visible) return; const p0 = at(pos, s0);
      for (let i = 0; i < n; i++) { const v = vel[i]; arr[i * 3] = p0.x + v[0] * t; arr[i * 3 + 1] = Math.max(0.05, p0.y + v[1] * t - 0.5 * g * t * t); arr[i * 3 + 2] = p0.z + v[2] * t; }
      geo.attributes.position.needsUpdate = true; mat.opacity = 1 - Math.pow(t / life, 2); });
  }
  /** Dust puffs rolling out on the floor. */
  dust(x, z, s0, o = {}) {
    const n = o.n ?? 10, r = rng(((s0 * 131) | 0) + n), life = o.life ?? 1.4, rad = o.r ?? 3;
    for (let i = 0; i < n; i++) {
      const th = (i / n) * Math.PI * 2 + r() * 0.5, k = 0.6 + r() * 0.6;
      const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.smoke, color: o.color ?? 0xe4d6c0, transparent: true, depthWrite: false }));
      this.obj(sp, s => { const t = s - s0; sp.visible = t >= 0 && t <= life; if (!sp.visible) return; const u = t / life, e = easeOut(u);
        sp.position.set(x + Math.cos(th) * rad * k * e, 0.4 + u * (o.rise ?? 1.2), z + Math.sin(th) * rad * k * e);
        const sz = (o.size ?? 2) * (0.5 + e); sp.scale.set(sz, sz, sz); sp.material.opacity = (o.op ?? 0.75) * (1 - u); });
    }
  }
  /** A glowing beam between two points (functions of time or vectors). */
  beam(a, b, s0, s1, w, color, o = {}) {
    const g = new THREE.Group();
    const outer = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 1, 16, 1, true), basic(color, 0.55, ADD));
    const core = new THREE.Mesh(new THREE.CylinderGeometry(0.4, 0.4, 1, 12, 1, true), basic(0xffffff, 0.9, ADD));
    g.add(outer, core); const up = V(0, 1, 0);
    return this.obj(g, s => { g.visible = s >= s0 && s <= s1; if (!g.visible) return;
      const pa = at(a, s), pb0 = at(b, s), pb = o.grow ? pa.clone().lerp(pb0, easeOut(prog(s, s0, s0 + o.grow))) : pb0;
      const dir = pb.clone().sub(pa), len = Math.max(0.01, dir.length());
      g.position.copy(pa).addScaledVector(dir, 0.5); g.quaternion.setFromUnitVectors(up, dir.normalize());
      const k = fade(s, s0, s1, o.fi ?? 0.05, o.fo ?? 0.2), ww = w * (o.pulse ? 1 + 0.25 * Math.sin(s * 40) : 1);
      g.scale.set(ww, len, ww); outer.material.opacity = 0.55 * k; core.material.opacity = 0.9 * k; });
  }
  /** Squares of the board lit up: cells = [{x, z, d}] (d = delay after s0). */
  tiles(cells, s0, s1, color, o = {}) {
    const geo = new THREE.PlaneGeometry(SQ * 0.92, SQ * 0.92); geo.rotateX(-Math.PI / 2);
    const im = new THREE.InstancedMesh(geo, basic(0xffffff, o.op ?? 0.85, ADD), cells.length);
    const m4 = new THREE.Matrix4(), c = new THREE.Color(color), k = new THREE.Color();
    cells.forEach((q, i) => { m4.makeTranslation(q.x, o.y ?? 0.03, q.z); im.setMatrixAt(i, m4); im.setColorAt(i, k.setRGB(0, 0, 0)); });
    im.frustumCulled = false;
    return this.obj(im, s => { im.visible = s >= s0 && s <= s1; if (!im.visible) return;
      cells.forEach((q, i) => { let f = fade(s, s0 + (q.d ?? 0), s1, o.fi ?? 0.08, o.fo ?? 0.3); if (o.pulse) f *= 0.65 + 0.35 * Math.sin((s - s0) * o.pulse); im.setColorAt(i, k.copy(c).multiplyScalar(f)); });
      im.instanceColor.needsUpdate = true; });
  }
  /** A glow that follows a point. */
  aura(pos, s0, s1, color, size, o = {}) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: o.tex ?? TEX.dot, color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
    return this.obj(sp, s => { sp.visible = s >= s0 && s <= s1; if (!sp.visible) return; sp.position.copy(at(pos, s));
      const k = size * (1 + (o.pulse ?? 0.12) * Math.sin(s * (o.rate ?? 9))); sp.scale.set(k, k, k); sp.material.opacity = (o.op ?? 0.8) * fade(s, s0, s1, 0.1, 0.25); });
  }
  /** Afterimages behind a moving actor. */
  trail(actor, s0, s1, color, o = {}) {
    const n = o.n ?? 10, dt = o.dt ?? 0.025;
    for (let i = 1; i <= n; i++) {
      const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.dot, color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
      this.obj(sp, s => { const ts = s - i * dt; sp.visible = s >= s0 && ts <= s1 && ts >= s0; if (!sp.visible) return; sp.position.copy(actor.pos(ts));
        const k = (o.size ?? 1.2) * (1 - i / (n + 1)); sp.scale.set(k, k, k); sp.material.opacity = (o.op ?? 0.7) * (1 - i / (n + 1)); });
    }
  }

  camAt(c, s) {
    const sh = this.shots; let i = 0; while (i + 1 < sh.length && sh[i + 1].c0 <= c) i++;
    const cur = sh[i].fn(c, s);
    if (i > 0 && sh[i].blend > 0 && c < sh[i].c0 + sh[i].blend) {
      const prev = sh[i - 1].fn(c, s), k = smooth(prog(c, sh[i].c0, sh[i].c0 + sh[i].blend));
      return { p: prev.p.clone().lerp(cur.p, k), l: prev.l.clone().lerp(cur.l, k), fov: lerp(prev.fov ?? 50, cur.fov ?? 50, k), roll: lerp(prev.roll ?? 0, cur.roll ?? 0, k) };
    }
    return cur;
  }
  render(c) {
    const s = this.S(c); this.nowC = c;
    for (const a of this.actors) a.update(s);
    for (const f of this.fx) f.upd(s, c);
    const cam = this.camAt(c, s);
    const off = V();
    for (const q of this.shakes) { if (s < q.s0 || s > q.s0 + q.d) continue; const e = q.amp * Math.pow(1 - (s - q.s0) / q.d, 2);
      off.x += e * (Math.sin(c * 53.1) * 0.6 + Math.sin(c * 31.7 + 1) * 0.4); off.y += e * (Math.sin(c * 47.3 + 2) * 0.6 + Math.sin(c * 29.9) * 0.4); off.z += e * Math.sin(c * 41.9 + 3) * 0.5; }
    let fov = cam.fov ?? 50, roll = cam.roll ?? 0;
    for (const m of this.camMods) { if (c < m.c0 || c > m.c0 + m.d) continue; const e = Math.min(1, (c - m.c0) / (m.d * 0.12)) * Math.pow(1 - prog(c, m.c0 + m.d * 0.12, m.c0 + m.d), 2); fov += (m.fov ?? 0) * e; roll += (m.roll ?? 0) * e; }
    this.camera.position.copy(cam.p).add(off); this.camera.lookAt(cam.l.clone().addScaledVector(off, 0.5));
    if (roll) this.camera.rotateZ(roll);
    this.camera.fov = fov; this.camera.updateProjectionMatrix();
    this.sky.position.copy(this.camera.position);
    this.sun.position.set(cam.l.x - 24, cam.l.y + 52, cam.l.z - 22); this.sun.target.position.copy(cam.l);
    const L = this.lightFn ? this.lightFn(s, c) : null; this.sun.intensity = 2.7 * (L?.sun ?? 1); this.hemi.intensity = 1.15 * (L?.hemi ?? 1);
    const gr = this.gradeFn ? this.gradeFn(s, c) : {};
    const u = this.grade.uniforms;
    u.sat.value = gr.sat ?? 1; u.tint.value.set(...(gr.tint ?? [1, 1, 1])); u.vig.value = gr.vig ?? 0.2; u.vigCol.value.set(...(gr.vigCol ?? [0, 0, 0]));
    let fa = 0, fc = [1, 1, 1];
    for (const f of this.flashes) { if (s < f.s0 || s > f.s0 + f.d) continue; const a = f.a * Math.pow(1 - (s - f.s0) / f.d, 2); if (a > fa) { fa = a; fc = f.color; } }
    if ((gr.flashA ?? 0) > fa) { fa = gr.flashA; fc = gr.flashCol ?? [1, 1, 1]; }
    u.flashA.value = fa; u.flashCol.value.set(...fc);
    u.contrast.value = gr.contrast ?? 1; u.maskOn.value = gr.maskOn ?? 0; u.blur.value.set(...(gr.blur ?? [0, 0])); u.rgb.value = gr.rgb ?? 0; u.dimW.value = gr.dimW ?? 1;
    this.keepOn = u.maskOn.value > 0;
    this.sky.material.uniforms.dark.value = this.skyFn ? this.skyFn(s, c) : 0;
    this.bloom.strength = gr.bloom ?? 0.55;
    this.composer.render();
    for (const q of this.banners) if (q.pip && c >= q.c0 && c <= (q.pipEnd ?? q.c1)) this.pipDraw(q, c, s);
    drawHud(this, c, s);
  }
  /** A live close-up in a banner's window (everyone's screen): what the clip put on layer 2 - the caster, its
   *  aura, its square - seen from in front, on a dark ground. Nobody's own camera moves for it. */
  pipDraw(q, c, s) {
    const R = pipRect(this, q, c); if (R.h < 2) return;
    const r = this.renderer, pc = this.pipCam ?? (this.pipCam = new THREE.PerspectiveCamera(30, 1, 0.1, 400)), v = q.pip(s, c), y = H - R.y - R.h;
    pc.aspect = R.w / R.h; pc.fov = v.fov ?? 30; pc.position.copy(v.p); pc.lookAt(v.l); pc.updateProjectionMatrix(); pc.layers.set(2);
    lightsOn1(this.scene);
    r.setRenderTarget(null); r.setViewport(R.x, y, R.w, R.h); r.setScissor(R.x, y, R.w, R.h); r.setScissorTest(true);
    r.setClearColor(q.pipBg ?? 0x03120f, 1); r.clear();
    const auto = r.shadowMap.autoUpdate, fog = this.scene.fog; r.shadowMap.autoUpdate = false; this.scene.fog = null;
    r.render(this.scene, pc);
    this.scene.fog = fog; r.shadowMap.autoUpdate = auto; r.setScissorTest(false); r.setViewport(0, 0, W, H);
  }
  /** Screen position of a world point, or null behind the camera. */
  project(p) { const v = p.clone().project(this.camera); if (v.z > 1) return null; return { x: (v.x + 1) / 2 * W, y: (1 - v.y) / 2 * H }; }
}
