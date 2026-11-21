// Ending summons previz - the stage: renderer, lights, the hall and the course, the pieces, the hands, pools of
// effects, and one frame of the loaded clip. A frame depends only on its time, so frames can be drawn one by one.
import { THREE, W, H, V, TEX, basic, ADD, prog, smooth, initLook, makePiece, makeHand, makeSky, makeCrown } from './kit.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { buildHall, buildCourse, HALL } from './hall.js';
import { drawHud } from './hud.js';

export const KINDS = ['pawn', 'knight', 'bishop', 'rook', 'queen', 'king'];
const SLATE = 1.3;   // the plan's name on black before its clip
const EU = new THREE.Euler();

// Effect pools: each frame takes what it needs; what is not taken stays hidden.
const MAKE = {
  ring: () => { const m = new THREE.Mesh(new THREE.RingGeometry(0.9, 1, 96), basic(0xffd36a, 1, ADD)); m.rotation.x = -Math.PI / 2; return m; },
  vring: () => new THREE.Mesh(new THREE.RingGeometry(0.86, 1, 96), basic(0xffe6a0, 1, ADD)),
  glow: () => { const m = new THREE.Mesh(new THREE.PlaneGeometry(2, 2), basic(0x46d2ff, 1, ADD)); m.rotation.x = -Math.PI / 2; return m; },
  trail: () => { const m = new THREE.Mesh(new THREE.PlaneGeometry(0.8, 1), basic(0x46d2ff, 1, { map: TEX.vgrad })); m.rotation.x = -Math.PI / 2; return m; },
  dust: () => new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.smoke, color: 0xd8cdbb, transparent: true, depthWrite: false, opacity: 0.8 })),
  flare: () => new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.flash, color: 0xffffff, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false })),
  beam: () => new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 1, 48, 1, true), basic(0xfff0b0, 1, { ...ADD, map: TEX.vgrad })),
};

export class Stage {
  constructor(gl, hud) {
    const r = this.renderer = new THREE.WebGLRenderer({ canvas: gl, antialias: true, preserveDrawingBuffer: true, powerPreference: 'high-performance' });
    r.setPixelRatio(1); r.setSize(W, H, false);
    r.outputColorSpace = THREE.SRGBColorSpace; r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = 1.05;
    r.shadowMap.enabled = true; r.shadowMap.type = THREE.PCFSoftShadowMap;
    this.g = hud.getContext('2d');
    const sc = this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(55, W / H, 0.1, 3000);
    initLook();
    this.sky = makeSky(); sc.add(this.sky);
    this.hemi = new THREE.HemisphereLight(0xe8f0ff, 0x6a5848, 0.7); sc.add(this.hemi);
    const sun = this.sun = new THREE.DirectionalLight(0xfff0dc, 2.6); sun.castShadow = true; sun.shadow.mapSize.set(2048, 2048);
    Object.assign(sun.shadow.camera, { left: -50, right: 50, top: 50, bottom: -50, near: 1, far: 320 }); sun.shadow.bias = -0.0004; sun.shadow.normalBias = 0.03;
    sc.add(sun, sun.target);
    this.hall = buildHall(sc); this.course = buildCourse(sc);
    // the roof in two halves, for the plan where it opens
    const roofMat = new THREE.MeshStandardMaterial({ color: 0xbdb3a3, roughness: 0.95 });
    this.roofHalves = [-1, 1].map(s => { const m = new THREE.Mesh(new THREE.BoxGeometry(15.6, 0.6, HALL.len + 0.6), roofMat); m.castShadow = m.receiveShadow = true; m.userData.s = s; sc.add(m); return m; });
    this.pieces = { w: {}, b: {} };
    for (const team of ['w', 'b']) for (const k of KINDS) {
      const p = makePiece(k, team === 'w' ? 'ally' : 'enemy', null, { cute: true }); p.rotation.order = 'YXZ'; p.visible = false; sc.add(p); this.pieces[team][k] = p;
    }
    this.hands = [0, 1, 2].map(() => { const h = makeHand({ scale: 1.0 }); h.root.visible = false; sc.add(h.root); return h; });
    this.bigCrown = makeCrown(2.4); this.bigCrown.visible = false; sc.add(this.bigCrown);
    this.pools = {}; this.used = {};
    // metal needs something to reflect: a soft room for everything golden
    const env = new THREE.PMREMGenerator(r).fromScene(new RoomEnvironment(), 0.04).texture;
    sc.traverse(o => { const m = o.material; if (m && m.metalness > 0.3) { m.envMap = env; m.envMapIntensity = 0.9; m.needsUpdate = true; } });
    this.clip = null; this.dur = 0;
  }
  load(clip) { this.clip = clip; this.slate = clip.title ? SLATE : 0; this.dur = clip.dur + this.slate; clip.build?.(this); }
  take(kind) {
    const pool = this.pools[kind] ??= [], i = this.used[kind] = (this.used[kind] ?? 0) + 1;
    if (pool.length < i) { const o = MAKE[kind](); o.renderOrder = 2; this.scene.add(o); pool.push(o); }
    const o = pool[i - 1]; o.visible = true; return o;
  }
  reset() {
    for (const t of ['w', 'b']) for (const k of KINDS) { const p = this.pieces[t][k]; p.visible = false; p.scale.setScalar(1); p.rotation.set(0, 0, 0); }
    for (const h of this.hands) h.root.visible = false;
    this.bigCrown.visible = false; this.bigCrown.rotation.set(0, 0, 0);
    for (const m of this.roofHalves) m.visible = false;
    const h = this.hall; h.roof.visible = true; h.setDoors(0); h.setLamps(1); h.spot.intensity = 0; h.setCircle(0);
    h.crown.visible = true; h.crown.position.set(0, 9.5, HALL.circleZ); h.crown.rotation.set(0, 0, 0);
    this.course.archFlash.material.opacity = 0; this.course.shadow.material.opacity = 0;
    this.hemi.intensity = 0.7; this.sun.intensity = 2.6; this.sky.material.uniforms.dark.value = 0;
    for (const k in this.pools) for (const o of this.pools[k]) o.visible = false;
    this.used = {};
  }
  /** Shows a piece: at (x, y, z), facing yaw (0 = +z), leaning forward and rolling sideways. */
  put(team, kind, x, y, z, yaw = 0, lean = 0, roll = 0) { const p = this.pieces[team][kind]; p.visible = true; p.position.set(x, y, z); p.rotation.set(lean, yaw, roll); return p; }
  /** The roof's two halves, open = 0 (shut) to 1 (slid off the walls). */
  setRoof(open) {
    this.hall.roof.visible = false;
    for (const m of this.roofHalves) { m.visible = true; m.position.set(m.userData.s * (7.8 + open * 17), HALL.height + 0.3 + open * 2, HALL.len / 2); }
  }
  /** Hand i by its grab point (between thumb and fingers): k = { pitch, yaw, roll, curl }. */
  setHand(i, grab, k = {}) {
    const h = this.hands[i]; h.root.visible = true;
    EU.set(k.pitch ?? 1.2, k.yaw ?? 0, k.roll ?? 0, 'YXZ');
    const off = h.tip.clone().multiplyScalar(h.scale).applyEuler(EU);
    h.root.position.copy(grab).sub(off); h.root.rotation.copy(EU); h.root.scale.setScalar(h.scale); h.pose(k.curl ?? 0.15);
  }
  project(p) { const v = p.clone().project(this.camera); if (v.z > 1 || v.z < -1 || Math.abs(v.x) > 1.2 || Math.abs(v.y) > 1.2) return null; return { x: (v.x + 1) / 2 * W, y: (1 - v.y) / 2 * H }; }
  render(t) {
    this.reset();
    const f = this.clip.frame(this, Math.max(0, t - this.slate)) ?? {};
    const c = f.cam;
    this.camera.position.copy(c.p); this.camera.up.copy(c.up ?? V(0, 1, 0)); this.camera.lookAt(c.l);
    if (c.roll) this.camera.rotateZ(c.roll);
    this.camera.fov = c.fov ?? 55; this.camera.updateProjectionMatrix(); this.camera.updateMatrixWorld();
    this.sky.position.copy(this.camera.position);
    const focus = f.focus ?? V(0, 0, 40);
    this.sun.target.position.copy(focus); this.sun.position.copy(focus).add(V(-42, 74, -26));
    this.renderer.render(this.scene, this.camera);
    const hud = f.hud ?? {};
    if (t < this.slate) hud.slate = { ...this.clip.title, a: 1 - smooth(prog(t, this.slate - 0.3, this.slate)) };
    hud.tags = (f.tags ?? []).map(q => { const s = this.project(q.pos); return s ? { ...s, text: q.text, team: q.team, a: q.a } : null; }).filter(Boolean);
    drawHud(this.g, hud);
  }
}
