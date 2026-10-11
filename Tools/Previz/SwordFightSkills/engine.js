// 소드 파이트 스킬 프리비즈 — 체스판·말·칼·이펙트·간단한 움직임 계산.
// 모든 장면은 시간 t(경기 초)만으로 정해진다: 같은 t면 같은 그림이 나와야 프레임 단위로 뽑을 수 있다.
import * as THREE from 'three';
export { THREE };

export const HALF = 12;          // 경기장 24 m × 24 m
export const TILE = 1.5;         // 16 × 16 칸
export const COL = { ally: 0x38cfe0, enemy: 0xf28c2c, gold: 0xf2c14e, violet: 0xa98bff, white: 0xffffff, red: 0xff6b6b, cream: 0xfff3d6 };

export const clamp = (x, a, b) => Math.min(b, Math.max(a, x));
export const lerp = (a, b, t) => a + (b - a) * t;
export const smooth = t => { t = clamp(t, 0, 1); return t * t * (3 - 2 * t); };
export const easeOut = t => { t = clamp(t, 0, 1); return 1 - (1 - t) * (1 - t) * (1 - t); };
export const prog = (t, t0, t1) => clamp((t - t0) / (t1 - t0), 0, 1);
export const fade = (t, t0, t1, fi = 0.08, fo = 0.15) =>
  (t < t0 || t >= t1) ? 0 : Math.min(fi > 0 ? prog(t, t0, t0 + fi) : 1, fo > 0 ? 1 - prog(t, t1 - fo, t1) : 1);
export const yawTo = (dx, dz) => Math.atan2(dx, dz);
export function rng(seed) {
  let s = (seed * 2654435761) >>> 0 || 1;
  return () => { s ^= s << 13; s >>>= 0; s ^= s >> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; };
}

// ---------- 텍스처 ----------
function canvasTex(w, h, draw, srgb = true) {
  const c = document.createElement('canvas'); c.width = w; c.height = h;
  draw(c.getContext('2d'), w, h);
  const t = new THREE.CanvasTexture(c);
  if (srgb) t.colorSpace = THREE.SRGBColorSpace;
  return t;
}
export const TEX = {};
function makeTextures() {
  TEX.dot = canvasTex(64, 64, g => {
    const gr = g.createRadialGradient(32, 32, 0, 32, 32, 32);
    gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(0.45, 'rgba(255,255,255,0.6)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = gr; g.fillRect(0, 0, 64, 64);
  });
  TEX.flash = canvasTex(128, 128, g => {
    const gr = g.createRadialGradient(64, 64, 0, 64, 64, 64);
    gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(0.18, 'rgba(255,248,210,1)');
    gr.addColorStop(0.4, 'rgba(255,205,80,0.75)'); gr.addColorStop(1, 'rgba(255,150,40,0)');
    g.fillStyle = gr; g.fillRect(0, 0, 128, 128);
  });
  TEX.vgrad = canvasTex(4, 64, g => {   // 아래 진하고 위로 사라지는 벽
    const gr = g.createLinearGradient(0, 64, 0, 0);
    gr.addColorStop(0, 'rgba(255,255,255,1)'); gr.addColorStop(0.6, 'rgba(255,255,255,0.35)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
    g.fillStyle = gr; g.fillRect(0, 0, 4, 64);
  });
  TEX.crack = canvasTex(128, 256, (g, w, h) => {
    const r = rng(7);
    g.clearRect(0, 0, w, h);
    g.strokeStyle = 'rgba(60,34,18,0.95)'; g.lineCap = 'round';
    for (let k = 0; k < 3; k++) {
      let x = w / 2 + (r() - 0.5) * 30, y = 0;
      g.lineWidth = k === 0 ? 7 : 3; g.beginPath(); g.moveTo(x, y);
      while (y < h) { y += 10 + r() * 18; x = clamp(x + (r() - 0.5) * 34, 14, w - 14); g.lineTo(x, y); }
      g.stroke();
    }
    for (let k = 0; k < 9; k++) {
      let x = w / 2 + (r() - 0.5) * 40, y = r() * h; g.lineWidth = 2; g.beginPath(); g.moveTo(x, y);
      for (let s = 0; s < 3; s++) { x += (r() < 0.5 ? -1 : 1) * (8 + r() * 16); y += (r() - 0.3) * 16; g.lineTo(x, y); }
      g.stroke();
    }
  });
  TEX.crack.wrapT = THREE.RepeatWrapping;
}

function woodBoard() {
  const N = 16, P = 128;
  return canvasTex(N * P, N * P, g => {
    const r = rng(11);
    for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) {
      const light = (i + j) % 2 === 0;
      const x0 = i * P, y0 = j * P;
      const v = (r() - 0.5) * 14;
      const base = light ? [226 + v, 204 + v, 168 + v] : [126 + v, 84 + v * 0.7, 54 + v * 0.5];
      g.fillStyle = `rgb(${base.map(c => clamp(c | 0, 0, 255)).join(',')})`;
      g.fillRect(x0, y0, P, P);
      const vertical = (i * 3 + j) % 2 === 0;
      for (let k = 0; k < 26; k++) {
        const off = r() * P, amp = 1 + r() * 3, freq = 0.02 + r() * 0.05, ph = r() * 6;
        const dark = r() < 0.7;
        g.strokeStyle = dark ? `rgba(${light ? '120,80,40' : '60,32,14'},${0.07 + r() * 0.12})` : `rgba(255,240,210,${0.05 + r() * 0.08})`;
        g.lineWidth = 0.6 + r() * 2.2; g.beginPath();
        for (let s = 0; s <= P; s += 4) {
          const w = off + Math.sin(s * freq + ph) * amp;
          if (vertical) { s === 0 ? g.moveTo(x0 + w, y0 + s) : g.lineTo(x0 + w, y0 + s); }
          else { s === 0 ? g.moveTo(x0 + s, y0 + w) : g.lineTo(x0 + s, y0 + w); }
        }
        g.stroke();
      }
      g.strokeStyle = 'rgba(40,22,10,0.22)'; g.lineWidth = 1.5; g.strokeRect(x0 + 0.75, y0 + 0.75, P - 1.5, P - 1.5);
    }
  });
}

// ---------- 재질 ----------
export const MAT = {};
function makeMaterials() {
  MAT.white = new THREE.MeshPhysicalMaterial({ color: 0xf3ebdd, roughness: 0.36, clearcoat: 0.5, clearcoatRoughness: 0.25 });
  MAT.black = new THREE.MeshPhysicalMaterial({ color: 0x2a2421, roughness: 0.3, clearcoat: 0.85, clearcoatRoughness: 0.18 });
  MAT.gold = new THREE.MeshStandardMaterial({ color: 0xd9a637, metalness: 0.85, roughness: 0.28 });
  MAT.slit = new THREE.MeshStandardMaterial({ color: 0x3b2b20, roughness: 0.8 });
  MAT.blade = new THREE.MeshStandardMaterial({ color: 0xe9eef4, metalness: 0.75, roughness: 0.2 });
  MAT.grip = new THREE.MeshStandardMaterial({ color: 0x5b3820, roughness: 0.7 });
  MAT.ringAlly = new THREE.MeshStandardMaterial({ color: COL.ally, emissive: COL.ally, emissiveIntensity: 0.9, roughness: 0.4 });
  MAT.ringEnemy = new THREE.MeshStandardMaterial({ color: COL.enemy, emissive: COL.enemy, emissiveIntensity: 0.9, roughness: 0.4 });
  MAT.stone = new THREE.MeshStandardMaterial({ color: 0x5a4636, roughness: 0.75 });
  MAT.stoneTop = new THREE.MeshStandardMaterial({ color: 0x6d5642, roughness: 0.7 });
}
export function basic(color, opacity = 1, extra = {}) {
  return new THREE.MeshBasicMaterial(Object.assign({ color, transparent: true, opacity, depthWrite: false, side: THREE.DoubleSide, toneMapped: false, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 }, extra));
}

// ---------- 세계 ----------
export function buildWorld(scene) {
  makeTextures(); makeMaterials();
  const top = new THREE.MeshStandardMaterial({ map: woodBoard(), roughness: 0.5 });
  top.map.anisotropy = 8;
  const side = new THREE.MeshStandardMaterial({ color: 0x4e3220, roughness: 0.75 });
  const slab = new THREE.Mesh(new THREE.BoxGeometry(HALF * 2, 1.4, HALF * 2), [side, side, top, side, side, side]);
  slab.position.y = -0.7; slab.receiveShadow = true; scene.add(slab);
  // 금색 테두리: 여기서 한 발 더 나가면 장외
  const rimM = new THREE.MeshStandardMaterial({ color: 0xd7a645, metalness: 0.7, roughness: 0.32 });
  for (const s of [-1, 1]) {
    const a = new THREE.Mesh(new THREE.BoxGeometry(HALF * 2 + 0.2, 0.08, 0.16), rimM); a.position.set(0, 0.0, s * (HALF + 0.02)); scene.add(a);
    const b = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.08, HALF * 2 + 0.2), rimM); b.position.set(s * (HALF + 0.02), 0.0, 0); scene.add(b);
    const c = new THREE.Mesh(new THREE.BoxGeometry(HALF * 2 + 0.2, 0.06, 0.08), rimM); c.position.set(0, -1.38, s * (HALF + 0.02)); scene.add(c);
    const d = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.06, HALF * 2 + 0.2), rimM); d.position.set(s * (HALF + 0.02), -1.38, 0); scene.add(d);
  }
  // 모서리 안쪽 낮은 룩 모양 장애물 4개 (기획 초안 §6)
  const obstacles = [];
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    const o = obstacleRook(); o.position.set(sx * 6.75, 0, sz * 6.75); scene.add(o); obstacles.push(o);
  }
  return { obstacles };
}
function obstacleRook() {
  const g = new THREE.Group();
  const body = new THREE.Mesh(new THREE.CylinderGeometry(0.86, 0.95, 1.0, 10), MAT.stone); body.position.y = 0.5;
  const lip = new THREE.Mesh(new THREE.CylinderGeometry(0.98, 0.9, 0.16, 10), MAT.stoneTop); lip.position.y = 1.06;
  g.add(body, lip);
  for (let i = 0; i < 6; i++) {
    const a = i / 6 * Math.PI * 2;
    const m = new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.22, 0.26), MAT.stoneTop);
    m.position.set(Math.sin(a) * 0.78, 1.25, Math.cos(a) * 0.78); m.rotation.y = a; g.add(m);
  }
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  return g;
}

// ---------- 체스 말 ----------
export const HEIGHT = { pawn: 0.9, rook: 1.1, knight: 1.25, bishop: 1.32, queen: 1.45, king: 1.62 };
export const NAME = { pawn: '폰', rook: '룩', knight: '나이트', bishop: '비숍', queen: '퀸', king: '킹' };
const BASE = [[0.0001, 0], [0.36, 0], [0.375, 0.035], [0.36, 0.07], [0.32, 0.09], [0.31, 0.12], [0.27, 0.14]];
function arc(cy, r, a0, a1, n = 10) {
  const o = []; for (let i = 0; i <= n; i++) { const a = lerp(a0, a1, i / n); o.push([Math.max(0.0001, Math.cos(a) * r), cy + Math.sin(a) * r]); } return o;
}
function lathe(pts) { const g = new THREE.LatheGeometry(pts.map(p => new THREE.Vector2(p[0], p[1])), 48); g.computeVertexNormals(); return g; }
const PROFILE = {
  pawn: [...BASE, [0.2, 0.2], [0.15, 0.3], [0.12, 0.44], [0.2, 0.48], [0.21, 0.51], [0.19, 0.54], [0.11, 0.56], ...arc(0.72, 0.17, -1.2, Math.PI / 2)],
  rook: [...BASE, [0.25, 0.2], [0.22, 0.3], [0.22, 0.62], [0.25, 0.75], [0.3, 0.8], [0.31, 0.83], [0.28, 0.85], [0.3, 0.88], [0.3, 0.98], [0.22, 0.98], [0.22, 0.93], [0.0001, 0.93]],
  bishop: [...BASE, [0.23, 0.2], [0.18, 0.32], [0.15, 0.62], [0.16, 0.75], [0.24, 0.8], [0.25, 0.83], [0.17, 0.86], [0.13, 0.88], [0.16, 0.92], [0.185, 0.98], [0.185, 1.04], [0.165, 1.1], [0.125, 1.16], [0.07, 1.2], [0.03, 1.22], [0.03, 1.24], ...arc(1.28, 0.05, -0.9, Math.PI / 2, 6)],
  queen: [...BASE, [0.24, 0.2], [0.19, 0.32], [0.14, 0.7], [0.13, 0.95], [0.2, 1.0], [0.21, 1.03], [0.14, 1.06], [0.13, 1.1], [0.19, 1.25], [0.225, 1.3], [0.19, 1.31], [0.12, 1.32], [0.0001, 1.33]],
  king: [...BASE, [0.25, 0.2], [0.2, 0.32], [0.15, 0.72], [0.14, 1.0], [0.21, 1.05], [0.22, 1.08], [0.15, 1.11], [0.14, 1.15], [0.2, 1.3], [0.225, 1.34], [0.16, 1.36], [0.15, 1.4], [0.08, 1.44], [0.0001, 1.45]],
  knight: [...BASE, [0.25, 0.2], [0.22, 0.28], [0.25, 0.34], [0.0001, 0.36]],
};
function knightHead() {
  const s = new THREE.Shape();
  const P = [[-0.2, 0.34], [-0.23, 0.6], [-0.2, 0.86], [-0.12, 1.02], [-0.05, 1.12], [-0.02, 1.24], [0.04, 1.13], [0.12, 1.05], [0.22, 0.93], [0.3, 0.82], [0.3, 0.73], [0.2, 0.69], [0.08, 0.66], [0.04, 0.6], [0.12, 0.48], [0.16, 0.34]];
  s.moveTo(P[0][0], P[0][1]); for (let i = 1; i < P.length; i++) s.lineTo(P[i][0], P[i][1]); s.closePath();
  const g = new THREE.ExtrudeGeometry(s, { depth: 0.24, bevelEnabled: true, bevelThickness: 0.04, bevelSize: 0.035, bevelSegments: 3, curveSegments: 4 });
  g.translate(0, 0, -0.12); g.rotateY(-Math.PI / 2); g.computeVertexNormals();
  return g;
}
export function makePiece(kind, team) {
  const g = new THREE.Group();
  const m = team === 'ally' ? MAT.white : MAT.black;
  g.add(new THREE.Mesh(lathe(PROFILE[kind]), m));
  if (kind === 'knight') g.add(new THREE.Mesh(knightHead(), m));
  if (kind === 'rook') for (let i = 0; i < 5; i++) {
    const a = i / 5 * Math.PI * 2, b = new THREE.Mesh(new THREE.BoxGeometry(0.13, 0.12, 0.1), m);
    b.position.set(Math.sin(a) * 0.26, 1.03, Math.cos(a) * 0.26); b.rotation.y = a; g.add(b);
  }
  if (kind === 'bishop') { const sl = new THREE.Mesh(new THREE.BoxGeometry(0.025, 0.2, 0.42), MAT.slit); sl.position.set(0.03, 1.08, 0); sl.rotation.z = 0.6; g.add(sl); }
  if (kind === 'queen') {
    for (let i = 0; i < 9; i++) { const a = i / 9 * Math.PI * 2, b = new THREE.Mesh(new THREE.SphereGeometry(0.036, 12, 8), MAT.gold); b.position.set(Math.sin(a) * 0.21, 1.33, Math.cos(a) * 0.21); g.add(b); }
    const t = new THREE.Mesh(new THREE.SphereGeometry(0.07, 16, 12), MAT.gold); t.position.y = 1.39; g.add(t);
  }
  if (kind === 'king') {
    const v = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.24, 0.06), MAT.gold); v.position.y = 1.55;
    const h = new THREE.Mesh(new THREE.BoxGeometry(0.18, 0.06, 0.06), MAT.gold); h.position.y = 1.58; g.add(v, h);
  }
  const ring = new THREE.Mesh(new THREE.TorusGeometry(0.385, 0.03, 10, 48), team === 'ally' ? MAT.ringAlly : MAT.ringEnemy);
  ring.rotation.x = Math.PI / 2; ring.position.y = 0.045; g.add(ring);
  g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  return g;
}
function makeSword() {
  const g = new THREE.Group();
  const blade = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.66, 0.014), MAT.blade); blade.position.y = 0.45;
  const tip = new THREE.Mesh(new THREE.ConeGeometry(0.035, 0.09, 4), MAT.blade); tip.position.y = 0.825; tip.rotation.y = Math.PI / 4; tip.scale.z = 0.3;
  const guard = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.035, 0.05), MAT.gold); guard.position.y = 0.11;
  const grip = new THREE.Mesh(new THREE.CylinderGeometry(0.02, 0.02, 0.13, 8), MAT.grip); grip.position.y = 0.03;
  const pom = new THREE.Mesh(new THREE.SphereGeometry(0.032, 10, 8), MAT.gold); pom.position.y = -0.045;
  g.add(blade, tip, guard, grip, pom);
  g.traverse(o => { if (o.isMesh) o.castShadow = true; });
  return g;
}

// ---------- 칼 자세 ----------
// 말의 앞 = +z, 오른쪽 = -x. φ는 앞에서 오른쪽으로 잰 각.
const D2R = Math.PI / 180;
const V = (x, y, z) => new THREE.Vector3(x, y, z);
const around = (phi, r, y) => V(-Math.sin(phi) * r, y, Math.cos(phi) * r);
const outDir = phi => V(-Math.sin(phi), 0, Math.cos(phi));
function swordPose(a, s, h) {
  const up = V(0, 1, 0), fwd = V(0, 0, 1);
  switch (a) {
    case 'raise': return { p: around(150 * D2R, 0.4, 0.62 * h), b: outDir(150 * D2R).multiplyScalar(0.6).add(V(0, 0.75, 0)).normalize() };
    case 'swing': { const phi = lerp(150, -110, easeOut(s)) * D2R; return { p: around(phi, 0.42, 0.5 * h), b: outDir(phi).add(V(0, -0.05, 0)).normalize(), phi }; }
    case 'guard': return { p: V(-0.36, 0.72 * h, 0.42), b: V(1, 0.08, 0).normalize() };
    case 'low': return { p: around(110 * D2R, 0.4, 0.3 * h), b: V(0, -0.45, 1).normalize() };
    case 'pull': return { p: V(-0.3, 0.55 * h, -0.22), b: fwd.clone() };
    case 'thrust': return { p: V(-0.12, 0.55 * h, lerp(-0.22, 0.6, easeOut(s))), b: fwd.clone() };
    case 'hold': return { p: V(-0.12, 0.55 * h, 0.6), b: fwd.clone() };
    case 'overhead': return { p: V(-0.18, 0.98 * h, -0.08), b: V(0, 1, -0.3).normalize() };
    case 'slam': return { p: V(-0.16, lerp(0.98, 0.32, easeOut(s)) * h, lerp(-0.08, 0.55, easeOut(s))), b: V(0, 1, -0.3).normalize().lerp(V(0, -0.75, 0.66).normalize(), easeOut(s)).normalize() };
    case 'point': return { p: V(-0.22, 0.66 * h, 0.36), b: V(0, 0.28, 1).normalize() };
    default: return { p: around(100 * D2R, 0.42, 0.42 * h), b: up.clone().add(outDir(100 * D2R).multiplyScalar(0.15)).add(fwd.clone().multiplyScalar(0.45)).normalize() };
  }
}

// ---------- 배우 ----------
const DT = 1 / 240, FRIC = 2.6, GRAV = 9.8;
let actorSeq = 0;
export class Actor {
  constructor(o) {
    this.id = o.id; this.kind = o.kind; this.team = o.team; this.h = HEIGHT[o.kind];
    this.x0 = o.x; this.z0 = o.z; this.yaw0 = o.yaw ?? 0;
    this.cmds = []; this.swords = [{ t: -10, a: 'rest', d: 0.01 }]; this.wins = []; this.seq = actorSeq++;
    this.root = new THREE.Group();
    this.root.add(makePiece(o.kind, o.team));
    this.sword = makeSword(); this.sword.scale.setScalar(o.kind === 'pawn' ? 1.1 : 1.2); this.root.add(this.sword);
    this.trailMat = basic(o.team === 'ally' ? 0xc8f7ff : 0xffd9b0, 0.6, { polygonOffset: false });
    this.trail = new THREE.Mesh(new THREE.BufferGeometry(), this.trailMat); this.trail.frustumCulled = false; this.root.add(this.trail);
  }
  walk(t, x, z, speed = 2.4, keepFace = false) { this.cmds.push({ t, type: 'walk', x, z, speed, keepFace }); return this; }
  face(t, x, z) { this.cmds.push({ t, type: 'face', x, z }); return this; }
  push(t, dx, dz, dist, knock = true) { const l = Math.hypot(dx, dz) || 1; this.cmds.push({ t, type: 'push', dx: dx / l, dz: dz / l, dist, knock }); return this; }
  pushFrom(t, ox, oz, dist, at) { const p = at || this.posAtStart; return this.push(t, p[0] - ox, p[1] - oz, dist); }
  jump(t, x, z, d, h) { this.cmds.push({ t, type: 'jump', x, z, d, h }); return this; }
  act(t, a, d = 0.2) { this.swords.push({ t, a, d }); this.swords.sort((p, q) => p.t - q.t); return this; }
  slash(t) { return this.act(t, 'raise', 0.1).act(t + 0.1, 'swing', 0.2).act(t + 0.34, 'rest', 0.1); } // t+0.18 쯤 맞음
  slow(t0, t1, f) { this.wins.push({ t0, t1, f }); return this; }

  simulate(T) {
    const n = Math.ceil(T / DT) + 4;
    const tr = { X: new Float32Array(n), Z: new Float32Array(n), Y: new Float32Array(n), YAW: new Float32Array(n), TILT: new Float32Array(n), TD: new Float32Array(n), SPD: new Float32Array(n) };
    const s = { x: this.x0, z: this.z0, y: 0, vx: 0, vz: 0, vy: 0, yaw: this.yaw0, yawT: this.yaw0, mode: 'idle', knocked: false, knockT: 0, kdir: 0, getup: -1, slideEnd: -1, fall: false, tumble: 0, jump: null };
    const cmds = [...this.cmds].sort((a, b) => a.t - b.t); let ci = 0;
    for (let i = 0; i < n; i++) {
      const t = i * DT;
      while (ci < cmds.length && cmds[ci].t <= t) {
        const c = cmds[ci++];
        if (s.fall) continue;
        if (c.type === 'walk') { s.mode = 'walk'; s.tx = c.x; s.tz = c.z; s.speed = c.speed; s.keepFace = c.keepFace; }
        else if (c.type === 'face') { s.yawT = yawTo(c.x - s.x, c.z - s.z); }
        else if (c.type === 'push') {
          s.jump = null; s.mode = 'slide'; s.vx = c.dx * c.dist * FRIC; s.vz = c.dz * c.dist * FRIC;
          if (c.knock) { s.knocked = true; s.knockT = t; s.kdir = yawTo(c.dx, c.dz); s.getup = -1; }
        } else if (c.type === 'jump') { s.jump = { t, x0: s.x, z0: s.z, x1: c.x, z1: c.z, d: c.d, h: c.h }; s.mode = 'jump'; s.yawT = yawTo(c.x - s.x, c.z - s.z); }
      }
      let mul = 1; for (const w of this.wins) if (t >= w.t0 && t < w.t1) mul = Math.min(mul, w.f);
      let spd = 0;
      if (s.fall) { s.vy -= GRAV * DT; s.y += s.vy * DT; s.x += s.vx * DT; s.z += s.vz * DT; s.tumble += DT * 4.5; }
      else if (s.jump) {
        const j = s.jump, p = (t - j.t) / j.d;
        if (p >= 1) { s.x = j.x1; s.z = j.z1; s.y = 0; s.jump = null; s.mode = 'idle'; }
        else { s.x = lerp(j.x0, j.x1, p); s.z = lerp(j.z0, j.z1, p); s.y = 4 * j.h * p * (1 - p); }
      } else if (s.mode === 'slide') {
        const f = Math.exp(-FRIC * DT); s.vx *= f; s.vz *= f; s.x += s.vx * DT; s.z += s.vz * DT;
        if (Math.hypot(s.vx, s.vz) < 0.08) { s.vx = s.vz = 0; s.mode = 'idle'; s.slideEnd = t; }
      } else if (s.mode === 'walk' && !s.knocked) {
        const dx = s.tx - s.x, dz = s.tz - s.z, d = Math.hypot(dx, dz), v = s.speed * mul;
        if (d < 0.02) s.mode = 'idle';
        else if (v > 0) { const st = Math.min(d, v * DT); s.x += dx / d * st; s.z += dz / d * st; spd = v; if (!s.keepFace) s.yawT = yawTo(dx, dz); }
      }
      if (!s.fall && !s.jump && (Math.abs(s.x) > HALF + 0.12 || Math.abs(s.z) > HALF + 0.12)) {
        s.fall = true; s.vy = 0;
        let sp = Math.hypot(s.vx, s.vz);
        if (sp < 1.4) { const ox = Math.abs(s.x) > HALF ? Math.sign(s.x) : 0, oz = Math.abs(s.z) > HALF ? Math.sign(s.z) : 0; s.vx += ox * 1.4; s.vz += oz * 1.4; }
        if (!s.knocked) { s.knocked = true; s.knockT = t; s.kdir = yawTo(s.vx, s.vz); }
      }
      if (!s.knocked) { let d = s.yawT - s.yaw; d = Math.atan2(Math.sin(d), Math.cos(d)); s.yaw += clamp(d, -12 * DT, 12 * DT); }
      let tilt = 0;
      if (s.knocked) {
        const kt = t - s.knockT;
        tilt = 1.45 * smooth(kt / 0.2);
        if (s.fall) tilt += s.tumble;
        else {
          if (s.getup < 0 && kt > 0.85 && s.mode !== 'slide') s.getup = t;
          if (s.getup >= 0) { const g = (t - s.getup) / 0.4; tilt = 1.45 * (1 - smooth(g)); if (g >= 1) { s.knocked = false; s.getup = -1; s.yaw = s.yawT; } }
        }
      }
      tr.X[i] = s.x; tr.Z[i] = s.z; tr.Y[i] = s.y; tr.YAW[i] = s.yaw; tr.TILT[i] = tilt; tr.TD[i] = s.kdir; tr.SPD[i] = spd;
    }
    this.tr = tr; this.n = n;
  }
  idx(t) { return clamp(Math.round(t / DT), 0, this.n - 1); }
  pos(t) { const i = this.idx(t); return [this.tr.X[i], this.tr.Y[i], this.tr.Z[i]]; }
  lying(t) { return this.tr.TILT[this.idx(t)] > 0.6; }

  apply(t) {
    const i = this.idx(t), tr = this.tr;
    let tilt = tr.TILT[i], kd = tr.TD[i];
    const spd = tr.SPD[i];
    let y = tr.Y[i];
    if (tilt === 0 && spd > 0.3) { y += 0.05 * Math.abs(Math.sin(t * 11 + this.seq)); tilt = 0.09; kd = tr.YAW[i]; }
    y += 0.24 * Math.sin(Math.min(tilt, Math.PI / 2));
    this.root.position.set(tr.X[i], y, tr.Z[i]);
    const qy = new THREE.Quaternion().setFromAxisAngle(V(0, 1, 0), tr.YAW[i]);
    const qt = new THREE.Quaternion().setFromAxisAngle(V(Math.cos(kd), 0, -Math.sin(kd)), tilt);
    this.root.quaternion.copy(qt.multiply(qy));
    this.root.visible = tr.Y[i] > -16;
    this.applySword(t);
  }
  applySword(t) {
    let k = 0; for (let j = 0; j < this.swords.length; j++) if (this.swords[j].t <= t) k = j;
    const cur = this.swords[k], prev = this.swords[Math.max(0, k - 1)];
    const s = clamp((t - cur.t) / cur.d, 0, 1);
    const P = swordPose(cur.a, s, this.h);
    const blend = k > 0 && cur.a !== 'swing' && cur.a !== 'thrust' && cur.a !== 'slam' ? smooth((t - cur.t) / 0.07) : 1;
    if (blend < 1) { const Q = swordPose(prev.a, 1, this.h); P.p.lerpVectors(Q.p, P.p, blend); P.b.lerpVectors(Q.b, P.b, blend).normalize(); }
    this.sword.position.copy(P.p);
    this.sword.quaternion.setFromUnitVectors(V(0, 1, 0), P.b);
    // 휘두른 자리 흰 자국
    let show = 0, phiEnd = 0;
    if (cur.a === 'swing') { show = 1; phiEnd = P.phi; }
    else if (prev.a === 'swing' && t - cur.t < 0.14) { show = 1 - (t - cur.t) / 0.14; phiEnd = -110 * D2R; }
    this.trail.visible = show > 0;
    if (show > 0) {
      const phi0 = 150 * D2R, N = 28, pos = [], idx = [], col = [];
      const span = phiEnd - phi0, y = 0.5 * this.h + 0.02;
      for (let j = 0; j <= N; j++) {
        const ph = phi0 + span * (j / N), a = (j / N);
        const i1 = around(ph, 0.5, y), o1 = around(ph, 1.18, y);
        pos.push(i1.x, i1.y, i1.z, o1.x, o1.y, o1.z);
        col.push(a, a, a, a, a, a);
        if (j > 0) { const b = j * 2; idx.push(b - 2, b - 1, b, b - 1, b + 1, b); }
      }
      const g = this.trail.geometry; g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setIndex(idx);
      this.trailMat.opacity = 0.55 * show;
    }
  }
}

// ---------- 장면 짓는 도구 ----------
export class Builder {
  constructor(scene, world) { this.scene = scene; this.world = world; this.g = new THREE.Group(); scene.add(this.g); this.actors = {}; this.fx = []; this.dur = 3; }
  actor(o) { const a = new Actor(o); this.g.add(a.root); this.actors[o.id] = a; return a; }
  add(obj, update) { this.g.add(obj); if (update) this.fx.push(update); return obj; }
  each(update) { this.fx.push(update); }
  simulate() { for (const a of Object.values(this.actors)) a.simulate(this.dur + 1.5); }
  update(t) { for (const a of Object.values(this.actors)) a.apply(t); for (const f of this.fx) f(t); }
  dispose() {
    this.scene.remove(this.g);
    this.g.traverse(o => { if (o.geometry) o.geometry.dispose(); });
  }

  // 바닥 예고 사각형 (시작점에서 yaw 방향으로 len)
  rect({ x, z, yaw, len, wid, col = COL.ally, t0, t1, fill, op = 0.2, edge = 0.9 }) {
    const grp = new THREE.Group(); grp.position.set(x, 0.025, z); grp.rotation.y = yaw;
    const plane = (w, l) => { const g = new THREE.PlaneGeometry(w, l); g.rotateX(-Math.PI / 2); g.translate(0, 0, l / 2); return g; };
    const mBase = basic(col, op), mFill = basic(col, op * 1.5), mEdge = basic(col, edge);
    const base = new THREE.Mesh(plane(wid, len), mBase);
    const pf = new THREE.Mesh(plane(wid, 1), mFill); pf.position.y = 0.002;
    const e1 = new THREE.Mesh(plane(0.07, len), mEdge); e1.position.x = wid / 2;
    const e2 = new THREE.Mesh(plane(0.07, len), mEdge); e2.position.x = -wid / 2;
    const e3 = new THREE.Mesh(plane(wid + 0.07, 0.07), mEdge); e3.position.z = len - 0.07;
    grp.add(base, pf, e1, e2, e3);
    this.add(grp, t => {
      const a = fade(t, t0, t1); grp.visible = a > 0;
      mBase.opacity = op * a; mFill.opacity = op * 1.5 * a; mEdge.opacity = edge * a;
      const p = fill ? prog(t, fill[0], fill[1]) : 0; pf.visible = p > 0; pf.scale.z = Math.max(0.001, p * len);
    });
    return grp;
  }
  // 바닥 원 (테두리 + 채움, dashed면 끊어진 테두리)
  circle({ x, z, r, col = COL.ally, t0, t1, fill, op = 0.16, edge = 0.9, dashed = false, width = 0.07, spin = 0 }) {
    const grp = new THREE.Group(); grp.position.set(x, 0.03, z);
    const mBase = basic(col, op), mFill = basic(col, op * 1.6), mEdge = basic(col, edge);
    const flat = g => { g.rotateX(-Math.PI / 2); return g; };
    const base = new THREE.Mesh(flat(new THREE.CircleGeometry(r, 64)), mBase);
    const pf = new THREE.Mesh(flat(new THREE.CircleGeometry(1, 64)), mFill); pf.position.y = 0.002;
    grp.add(base, pf);
    const ring = new THREE.Group(); grp.add(ring);
    if (dashed) { const N = 28; for (let i = 0; i < N; i++) ring.add(new THREE.Mesh(flat(new THREE.RingGeometry(r - width, r, 6, 1, i / N * Math.PI * 2, Math.PI * 2 / N * 0.55)), mEdge)); }
    else ring.add(new THREE.Mesh(flat(new THREE.RingGeometry(r - width, r, 64)), mEdge));
    this.add(grp, t => {
      const a = fade(t, t0, t1); grp.visible = a > 0;
      mBase.opacity = op * a; mFill.opacity = op * 1.6 * a; mEdge.opacity = edge * a;
      const p = fill ? prog(t, fill[0], fill[1]) : 0; pf.visible = p > 0; pf.scale.setScalar(Math.max(0.001, p * r));
      ring.rotation.y = spin * t;
    });
    return grp;
  }
  // 퍼지는 충격 고리 + 낮은 빛 벽
  shock({ x, z, t0, dur = 0.18, r0 = 0.3, r1 = 2.5, col = COL.ally, h = 0.6, op = 0.85 }) {
    const grp = new THREE.Group(); grp.position.set(x, 0.035, z);
    const mR = basic(col, op), mW = basic(col, op * 0.6, { map: TEX.vgrad, polygonOffset: false });
    const ring = new THREE.Mesh(new THREE.RingGeometry(0.82, 1, 72).rotateX(-Math.PI / 2), mR);
    const wall = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, h, 72, 1, true).translate(0, h / 2, 0), mW);
    grp.add(ring, wall);
    this.add(grp, t => {
      const life = dur + 0.25; const k = (t - t0) / dur;
      grp.visible = t >= t0 && t < t0 + life;
      if (!grp.visible) return;
      const r = lerp(r0, r1, easeOut(k)); ring.scale.set(r, 1, r); wall.scale.set(r, 1 - 0.5 * clamp(k, 0, 1), r);
      const a = k <= 1 ? 1 : 1 - (t - t0 - dur) / 0.25;
      mR.opacity = op * a; mW.opacity = op * 0.6 * a;
    });
  }
  // 흙먼지·불똥: 정해진 시드로 같은 모양이 나온다
  burst({ x, y = 0.1, z, t0, n = 12, seed = 1, speed = 2.5, up = 1.5, life = 0.6, size = 0.35, col = 0xd8c09a, grav = 3, dir, spread = Math.PI, op = 0.85, grow = 1.6 }) {
    const r = rng(seed), parts = [];
    const base = dir ? Math.atan2(dir[0], dir[1]) : 0;
    for (let i = 0; i < n; i++) {
      const a = dir ? base + (r() - 0.5) * 2 * spread : r() * Math.PI * 2;
      const sp = speed * (0.4 + r() * 0.8);
      const m = new THREE.SpriteMaterial({ map: TEX.dot, color: col, transparent: true, depthWrite: false, opacity: op, toneMapped: false });
      const sprite = new THREE.Sprite(m);
      parts.push({ s: sprite, m, vx: Math.sin(a) * sp, vz: Math.cos(a) * sp, vy: up * (0.5 + r()), lf: life * (0.6 + r() * 0.6), sz: size * (0.6 + r() * 0.8) });
      this.g.add(sprite);
    }
    this.each(t => {
      for (const p of parts) {
        const age = t - t0; const k = age / p.lf;
        p.s.visible = age >= 0 && k < 1; if (!p.s.visible) continue;
        p.s.position.set(x + p.vx * age, Math.max(0.05, y + p.vy * age - 0.5 * grav * age * age), z + p.vz * age);
        p.s.scale.setScalar(p.sz * (1 + grow * k)); p.m.opacity = op * (1 - k) * (1 - k);
      }
    });
  }
  flash({ x, y = 0.8, z, t0, dur = 0.16, size = 1.4, col = 0xffffff }) {
    const m = new THREE.SpriteMaterial({ map: TEX.flash, color: col, transparent: true, depthWrite: false, depthTest: false, toneMapped: false });
    const s = new THREE.Sprite(m); s.position.set(x, y, z); s.renderOrder = 10;
    this.add(s, t => { const k = (t - t0) / dur; s.visible = k >= 0 && k < 1; if (!s.visible) return; s.scale.setScalar(size * (0.5 + 0.8 * easeOut(k))); m.opacity = 1 - k * k; });
  }
  hitSpark({ x, y = 0.75, z, t0, seed = 3, col = 0xfff0b0 }) {
    this.flash({ x, y, z, t0, dur: 0.14, size: 1.2 });
    this.burst({ x, y, z, t0, n: 9, seed, speed: 3.2, up: 1.2, life: 0.28, size: 0.12, col, grav: 6, grow: 0.2, op: 1 });
  }
  // 두 점 사이 빛줄기
  beam({ from, to, t0, t1, col = COL.violet, core = 0xffffff, w = 0.06, glow = 0.22, op = 1, opFn }) {
    const a = V(...from), b = V(...to), d = b.clone().sub(a), len = d.length();
    const grp = new THREE.Group(); grp.position.copy(a); grp.quaternion.setFromUnitVectors(V(0, 1, 0), d.normalize());
    const g = new THREE.CylinderGeometry(1, 1, 1, 14, 1, true).translate(0, 0.5, 0);
    const mc = basic(core, op, { polygonOffset: false }), mg = basic(col, op * 0.45, { polygonOffset: false });
    const c = new THREE.Mesh(g, mc); c.scale.set(w, len, w);
    const gl = new THREE.Mesh(g, mg); gl.scale.set(glow, len, glow);
    grp.add(c, gl);
    this.add(grp, t => { const k = opFn ? opFn(t) : fade(t, t0, t1); grp.visible = k > 0; mc.opacity = op * k; mg.opacity = op * 0.45 * k; });
    return grp;
  }
  // 점선 포물선 (나이트 도약 길)
  dots({ pts, t0, t1, col = 0xffffff, r = 0.045 }) {
    const m = basic(col, 0.95, { polygonOffset: false }), g = new THREE.SphereGeometry(r, 10, 8), grp = new THREE.Group();
    for (const p of pts) { const s = new THREE.Mesh(g, m); s.position.set(...p); grp.add(s); }
    this.add(grp, t => { const a = fade(t, t0, t1); grp.visible = a > 0; m.opacity = 0.95 * a; });
  }
}
