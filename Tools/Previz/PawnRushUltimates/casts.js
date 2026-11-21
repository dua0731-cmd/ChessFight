// Pawn Rush ultimates previz - ways to cast an ultimate. The queen sacrifice cast four ways (the cut-in, the
// game at the board, the awakening, the declaration), and the recommended set - the chess clock, the player's
// hand from the sky, the evaluation bar with !! and ?? - on the table flip and the grand castling.
import { THREE, V, HALF, SQ, COL, MAT, lerp, clamp, prog, smooth, easeOut, easeIn, back, fade, rng, cellX, snap, makePiece, makeCrown, basic, ADD, keepColour, TEX } from './stage.js';
import { DISP } from './hud.js';
import { ACT, warp, teams, opening, fixed, centroid, finish } from './ults.js';

/** clip -> scene knots without the cut-in's freeze: [clip s, scene s] pairs after the press */
function flow(...pairs) {
  const k = [[0, 0], [ACT, ACT]];
  for (const [dc, ds] of pairs) { const l = k[k.length - 1]; k.push([l[0] + dc, l[1] + ds]); }
  const l = k[k.length - 1]; k.push([l[0] + 60, l[1] + 60]); return k;
}
const flowSlow = (s0, d, f) => flow([s0 - ACT, s0 - ACT], [d / f, d]);

// ---------------------------------------------------------------- the chess clock beside the course
function makeClock() {
  const g = new THREE.Group();
  const wood = new THREE.MeshStandardMaterial({ color: 0x6b3f22, roughness: 0.5 }), ink = new THREE.MeshStandardMaterial({ color: 0x151515, roughness: 0.5 });
  const red = new THREE.MeshStandardMaterial({ color: 0xd8262e, roughness: 0.4, side: THREE.DoubleSide, emissive: 0x500000 });
  const body = new THREE.Mesh(new THREE.BoxGeometry(4.4, 2.2, 1.7), wood); body.position.y = 1.1; g.add(body);
  const dials = [], hands = [], flags = [], buttons = [];
  [-1, 1].forEach((sx, i) => {
    const dm = new THREE.MeshStandardMaterial({ color: 0xfbf7ee, roughness: 0.4, emissive: 0xff2020, emissiveIntensity: 0 });
    const dial = new THREE.Mesh(new THREE.CylinderGeometry(0.82, 0.82, 0.1, 40), dm); dial.rotation.x = Math.PI / 2; dial.position.set(sx * 1.08, 1.15, 0.86); g.add(dial);
    const rim = new THREE.Mesh(new THREE.TorusGeometry(0.84, 0.07, 8, 40), MAT.gold); rim.position.set(sx * 1.08, 1.15, 0.9); g.add(rim);
    for (let t = 0; t < 12; t++) { const a = t / 12 * Math.PI * 2, tk = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.14, 0.02), ink); tk.position.set(sx * 1.08 + Math.sin(a) * 0.68, 1.15 + Math.cos(a) * 0.68, 0.93); tk.rotation.z = -a; g.add(tk); }
    const hand = new THREE.Group(); hand.position.set(sx * 1.08, 1.15, 0.95); const hm = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.6, 0.03), ink); hm.position.y = 0.27; hand.add(hm); g.add(hand);
    const flag = new THREE.Group(); flag.position.set(sx * 1.08 + 0.1, 1.15 + 0.6, 0.96); const fm = new THREE.Mesh(new THREE.PlaneGeometry(0.14, 0.3), red); fm.position.y = 0.15; flag.add(fm); g.add(flag);
    const btn = new THREE.Mesh(new THREE.CylinderGeometry(0.3, 0.34, 0.42, 20), i === 0 ? MAT.white : MAT.black); btn.position.set(sx * 1.08, 2.42, 0); g.add(btn);
    dials.push(dm); hands.push(hand); flags.push(flag); buttons.push(btn);
  });
  g.traverse(m => { if (m.isMesh) m.castShadow = true; });
  return { g, dials, hands, flags, buttons };
}
/** Our button goes down at sPress ("탁!"); their side runs red until the flag falls at sFlag. */
function clockFx(st, at, sPress, sFlag, sGone, ry = 2.25) {
  const ck = makeClock(); ck.g.position.copy(at); ck.g.rotation.y = ry;
  st.obj(ck.g, s => {
    ck.g.visible = s >= sPress - 0.3 && s < sGone; if (!ck.g.visible) return;
    ck.g.scale.setScalar(1.7 * Math.max(0.01, back(prog(s, sPress - 0.3, sPress - 0.02))));
    ck.buttons[0].position.y = s >= sPress ? 2.22 : 2.42;
    const run = s >= sPress && s < sFlag;
    ck.dials[1].emissiveIntensity = run ? 0.3 + 0.3 * Math.sin(s * 22) : s >= sFlag ? 0.55 : 0;
    ck.hands[1].rotation.z = -Math.max(0, Math.min(s, sFlag) - sPress) * 14; ck.hands[0].rotation.z = -0.8;
    ck.flags[1].rotation.z = -1.6 * easeIn(prog(s, sFlag - 0.04, sFlag + 0.12));
  });
  st.label(at.clone().add(V(0, 6.2, 0)), sPress, sPress + 0.9, '탁!', { size: 36, color: '#ffffff', bg: 'rgba(20,20,24,0.88)' });
  st.label(at.clone().add(V(0, 6.2, 0)), sFlag, sFlag + 1.3, '시간패!', { size: 28, color: '#ff6b6b', bg: 'rgba(20,20,24,0.88)' });
  st.flash(at.clone().add(V(0, 4.2, 0)), sPress, 0.25, 3, 0xffffff); st.shake(sPress, 0.15, 0.12);
  return ck;
}

// ---------------------------------------------------------------- the player's hand from the sky
function makeHand(o = {}) {
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
const EU = new THREE.Euler();
const tipOffset = (h, k) => h.tip.clone().multiplyScalar(h.scale).applyEuler(EU.set(k.pitch ?? 1.2, k.yaw ?? 0, k.roll ?? 0, 'YXZ'));
/** A hand that moves by its grab point: fn(s) -> { g: grab point, pitch, yaw, roll, curl } or null (hidden). */
function handFx(st, h, fn) {
  st.obj(h.root, s => {
    const k = fn(s); h.root.visible = !!k; if (!k) return;
    h.root.position.copy(k.g).sub(tipOffset(h, k)); h.root.rotation.set(k.pitch ?? 1.2, k.yaw ?? 0, k.roll ?? 0, 'YXZ');
    h.root.scale.setScalar(h.scale); h.pose(k.curl ?? 0.15);
  });
}
/** a path through keyed points: [[s, V, ease?], ...] */
function path(keys) {
  return s => {
    if (s <= keys[0][0]) return keys[0][1].clone();
    for (let i = 1; i < keys.length; i++) if (s <= keys[i][0]) return keys[i - 1][1].clone().lerp(keys[i][1], (keys[i][2] ?? smooth)(prog(s, keys[i - 1][0], keys[i][0])));
    return keys[keys.length - 1][1].clone();
  };
}

// ---------------------------------------------------------------- the queen's true form, shards, a word in the air
function makeTrueQueen() {
  const g = new THREE.Group();
  const gown = new THREE.MeshPhysicalMaterial({ color: 0xf7f1e6, roughness: 0.45, sheen: 1, sheenColor: new THREE.Color(0xffe2a8) });
  const red = new THREE.MeshStandardMaterial({ color: 0x9e1b2f, roughness: 0.6, side: THREE.DoubleSide });
  const skin = new THREE.MeshStandardMaterial({ color: 0xf2c9a6, roughness: 0.6 }), hair = new THREE.MeshStandardMaterial({ color: 0x3a2416, roughness: 0.7 });
  const skirt = new THREE.Mesh(new THREE.ConeGeometry(0.85, 1.45, 32), gown); skirt.position.y = 0.72; g.add(skirt);
  const waist = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.3, 0.6, 20), gown); waist.position.y = 1.62; g.add(waist);
  const belt = new THREE.Mesh(new THREE.TorusGeometry(0.27, 0.035, 8, 24), MAT.gold); belt.rotation.x = Math.PI / 2; belt.position.y = 1.36; g.add(belt);
  const head = new THREE.Mesh(new THREE.SphereGeometry(0.19, 20, 14), skin); head.position.y = 2.12; g.add(head);
  const hairM = new THREE.Mesh(new THREE.SphereGeometry(0.215, 20, 14, 0, Math.PI * 2, 0, Math.PI * 0.62), hair); hairM.position.set(0, 2.14, -0.02); g.add(hairM);
  const crown = makeCrown(0.15); crown.position.y = 2.27; g.add(crown);
  const cape = new THREE.Mesh(new THREE.CylinderGeometry(0.32, 0.98, 1.95, 24, 1, true, Math.PI * 0.55, Math.PI * 0.9), red); cape.position.y = 1.0; g.add(cape);
  const armL = new THREE.Mesh(new THREE.CapsuleGeometry(0.07, 0.6, 4, 8), gown); armL.position.set(-0.3, 1.55, 0.05); armL.rotation.z = 0.25; g.add(armL);
  const armR = new THREE.Group(); armR.position.set(0.3, 1.86, 0); g.add(armR);
  const upper = new THREE.Mesh(new THREE.CapsuleGeometry(0.07, 0.6, 4, 8), gown); upper.position.y = -0.34; armR.add(upper);
  const sc = new THREE.Group(); sc.position.y = -0.7; armR.add(sc);
  const rod = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 1.2, 10), MAT.gold); rod.rotation.x = Math.PI / 2; rod.position.z = 0.3; sc.add(rod);
  const gem = new THREE.Mesh(new THREE.SphereGeometry(0.09, 14, 10), new THREE.MeshStandardMaterial({ color: 0xff3050, emissive: 0xff1030, emissiveIntensity: 1.5 })); gem.position.z = 0.92; sc.add(gem);
  g.traverse(m => { if (m.isMesh) m.castShadow = true; });
  return { g, armR };
}
function shards(st, pos, s0, n, mat, o = {}) {
  const r = rng(((s0 * 1000) | 0) + n);
  for (let i = 0; i < n; i++) {
    const m = new THREE.Mesh(new THREE.TetrahedronGeometry(0.1 + r() * 0.14), mat);
    const v = V(r() - 0.5, r() * 0.8 + 0.2, r() - 0.5).normalize().multiplyScalar(3 + r() * 5), p0 = V(pos.x + (r() - 0.5) * 0.5, pos.y + r() * 1.4, pos.z + (r() - 0.5) * 0.5), sp = V(r() * 9, r() * 9, r() * 9);
    if (o.keep) keepColour(m);
    st.obj(m, s => { const t = s - s0; m.visible = t >= 0 && t <= (o.life ?? 1.6); if (!m.visible) return; m.position.set(p0.x + v.x * t, Math.max(0.05, p0.y + v.y * t - 4.9 * t * t), p0.z + v.z * t); m.rotation.set(sp.x * t, sp.y * t, sp.z * t); });
  }
}
function textSprite(text, color, h) {
  const c = document.createElement('canvas'); c.width = 1024; c.height = 256;
  const tex = new THREE.CanvasTexture(c); tex.colorSpace = THREE.SRGBColorSpace;
  const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true, depthWrite: false, depthTest: false, toneMapped: false }));
  sp.scale.set(h * 4, h, 1); let drawn = false;
  sp.userData.draw = () => { if (drawn) return; drawn = true; const g = c.getContext('2d'); g.font = `170px ${DISP}`; g.textAlign = 'center'; g.textBaseline = 'middle'; g.lineJoin = 'round'; g.lineWidth = 24; g.strokeStyle = '#1a1206'; g.strokeText(text, 512, 134); g.fillStyle = color; g.fillText(text, 512, 134); tex.needsUpdate = true; };
  return sp;
}
const badge = (st, pos, s0, s1, text) => st.label(pos, s0, s1, text, { badge: true, size: 24, rise: 10, bg: text === '!!' ? '#1fb99a' : text === '??' ? '#e5453a' : '#e8a317' });

// ================================================================ the queen sacrifice, the same scene for every way of casting it
const QPACK = { queen: [0.5, 67], knight: [-2.6, 65.6], rook: [2.8, 64.6], bishop: [-0.6, 63.2], pawn: [3.6, 62], king: [-6.6, 52] };
function queenScene(st, o) {
  const T = teams(st, 'queen', { enemy: QPACK }), Q = T.me, E = T.E, sImp = o.sImp, sWarn = o.sWarn;
  const C0 = centroid([E.queen, E.knight, E.rook, E.bishop, E.pawn], sImp), tx = clamp(C0.x, -3, 3), tz = C0.z;
  // the straggling king sees the target and stops: he ends just outside it
  if (!o.noDodge) E.king.until(sWarn + 0.25).custom(sImp - sWarn + 0.6, (u, t, a) => ({ ...a, run: 0, z: a.z + 0.6 * easeOut(prog(t, 0, 0.3)), pitch: -0.12 * smooth(prog(t, 0, 0.3)), roll: 0.07 * Math.sin(t * 9) })).run(20);
  const disc = []; for (let i = -10; i <= 10; i++) for (let j = -10; j <= 10; j++) { const x = snap(tx) + i * SQ, z = snap(tz) + j * SQ; if (Math.abs(x) < HALF && Math.hypot(x - tx, z - tz) < 20) disc.push({ x, z, d: Math.hypot(x - tx, z - tz) * 0.01 }); }
  st.tiles(disc, sWarn, sImp + 0.1, COL.red, { op: 0.45, pulse: 14 });
  for (const r of [6, 13, 20]) st.ring(tx, tz, sWarn, sImp - sWarn + 0.15, r, r, COL.red, { w: 0.04, op: 1, keep: true });
  const big = makePiece('queen', null, MAT.fire);
  st.obj(big, s => {
    if (s < sImp) { const k = o.giantPre(s); big.visible = !!k; if (!k) return; big.position.set(k.x, k.y, k.z); big.scale.setScalar(k.k ?? 8); big.rotation.set(0, k.ry ?? s * 0.6, 0); return; }
    big.visible = s < sImp + 1.3; if (!big.visible) return; big.position.set(tx, -14 * easeIn(prog(s, sImp + 0.4, sImp + 1.3)), tz); big.scale.setScalar(8);
  });
  st.flash(V(tx, 3, tz), sImp, o.soft ? 0.35 : 0.9, o.soft ? 34 : 60); st.screen(sImp, o.soft ? 0.12 : 0.45, [1, 1, 1], o.soft ? 0.7 : 0.95); st.shake(sImp, 1.4, 1.1);
  st.ring(tx, tz, sImp, 1.2, 2, 34, 0xffd090, { w: 0.06 }); st.ring(tx, tz, sImp + 0.05, 1.6, 2, 46, 0xffffff, { w: 0.03, op: 0.5 });
  st.dust(tx, tz, sImp, { n: 22, r: 16, size: 6, life: 2.2, color: 0xc9a98a }); st.sparks(V(tx, 2, tz), sImp, 140, { color: 0xffa040, speed: 18, up: 0.4, life: 1.6 });
  const crater = new THREE.Mesh(new THREE.RingGeometry(1.5, 7.5, 48), basic(0x1a0c06, 0.85)); crater.rotation.x = -Math.PI / 2; crater.position.set(tx, 0.04, tz);
  st.obj(crater, s => { crater.visible = s >= sImp && s < sImp + 4.4; crater.material.opacity = 0.85 * fade(s, sImp, sImp + 4.4, 0.05, 0.6); });
  st.tiles(disc.filter(q => Math.hypot(q.x - tx, q.z - tz) < 7), sImp + 0.1, sImp + 4.3, 0xff5a1a, { op: 0.55, pulse: 6 });
  const hit = [];
  for (const e of T.enemies) {
    const p = e.pred(sImp), d = Math.hypot(p.x - tx, p.z - tz);
    if (d > 20) { if (e !== E.king || o.noDodge) e.run(20); continue; }
    hit.push(e);
    const r = rng(Math.round(p.z * 10)), ang = Math.atan2(p.x - tx, p.z - tz);
    e.until(sImp + 0.03).fly(1.9, clamp(p.x + Math.sin(ang) * (3 + r() * 3), -7, 7), p.z - (27 + r() * 7), { h: 12 + r() * 4, spins: 2 + (r() > 0.5 ? 1 : 0), roll: 1.4 }).lie(3.0).getUp(0.5).run(20);
  }
  for (const a of T.allies) if (a !== Q) a.run(20);
  return { T, Q, E, tx, tz, hit };
}
/** the shots and words after the impact, the same for every way */
function queenAfter(st, q, sImp, o = {}) {
  st.shot(st.C(sImp - 0.02), () => ({ p: V(q.tx + 15, 2.2, q.tz - 15), l: V(q.tx, 5, q.tz), fov: 62 }));
  st.shot(st.C(sImp + 1.4), fixed(V(8, 9, q.tz + 16), V(0, 1, q.tz - 16), 56), { blend: 0.6 });
  st.cap(st.C(sImp), st.C(sImp + 1.4), o.hitLine ?? '쾅 — 반경 20 m 적 다섯이 30 m 뒤로 · 뒤에서 멈춘 킹만 아슬하게 피함', o.hitMemo ?? '콰과광!! + 0.5초 정적');
  st.stamp(st.C(sImp + 0.02), 1.6, 'Q×h7+!!', { size: 118, sub: '퀸 희생' });
}
const qBase = { kind: 'queen', opt: '', cut: ACT, nota: 'Q×h7+!!', dur: 14, cornerHead: '퀸 희생' };

const qCut = {
  ...qBase, chip: '퀸 희생 · 시전 연출 ①', tag: '지금 영상', name: '필살기 컷인', cornerTail: '① 필살기 컷인',
  line: 'Q를 누르면 모든 화면에 1초 동안 필살기 띠 — 지금 영상에 넣은 방식. 비교를 위한 기준',
  spec: [
    ['시작', 'Q → 모든 화면 1초 필살기 띠 (기물 그림 · 이름 · 기보), 세계는 그동안 0.1초만'],
    ['멈춤', '1초 — 온라인은 그림만 멈춤 (지금 히트 스톱과 같은 길)'],
    ['좋은 점', '누가 썼는지 확실 · 화면 표시만이라 만들기 쉬움'],
    ['아쉬운 점', '격투 게임 문법이라 체스 맛은 약함 · 1초 동안 시야를 가림'],
  ],
  build(st) {
    st.knots = warp([5.65 - 3.7, 5.65 - 3.7], [0.55 / 0.3, 0.55]);
    const sUp = ACT + 0.1, sGone = sUp + 0.6, sImp = 5.65, sFall = sImp - 0.9;
    const q = queenScene(st, { sImp, sWarn: sGone - 0.15, giantPre: s => (s < sFall ? null : { x: 0, y: 0, z: 0, ...tgt(s) }) });
    const tgt = s => ({ x: q.tx, y: lerp(95, 0, easeIn(prog(s, sFall, sImp))), z: q.tz });
    fallFx(st, q, sFall, sImp);
    const Q = q.Q, q0 = Q.pred(sUp);
    Q.until(ACT).stand(0.1).custom(sGone - sUp, (u, t, a) => ({ ...a, y: 70 * easeIn(u), yaw: a.yaw + u * 10, run: 0 })).set({ hide: 1, track: 1, y: 0 })
      .hold(sImp + 3.6 - sGone).set({ hide: 0, track: 0, x: q0.x, z: q0.z, y: 0, run: 0 }).stand(0.4).run(20);
    st.trail(Q, sUp, sGone, COL.gold, { n: 14, dt: 0.02, size: 1.6 });
    redSky(st, sUp + 0.2, sImp + 1.0);
    opening(st, Q, '상대 6명이 몰려서 20 m 앞 — 같은 장면을 네 가지 시전 연출로');
    st.shot(st.C(sUp), (c, s) => ({ p: V(q0.x + 5, 1.2, q0.z - 7), l: V(q0.x, Math.min(Q.at(Math.min(s, sGone - 0.01)).y + 2, 30), q0.z + 2), fov: 62 }));
    st.shot(st.C(sGone), fixed(V(-11, 15, q.tz - 36), V(q.tx, 10, q.tz), 56));
    st.cap(st.C(sUp), st.C(sImp), '컷인이 지나가면 퀸이 하늘로 — 1.5초 뒤 적 무리에 거대 퀸 낙하', '컷인 "쉬익—쾅" + 성가대');
    queenAfter(st, q, sImp);
    finish(st, st.C(sImp + 3.2));
  },
};
function fallFx(st, q, sFall, sImp) {
  const yAt = s => lerp(95, 0, easeIn(prog(s, sFall, sImp)));
  st.aura(s => V(q.tx, yAt(s) + 6, q.tz), sFall, sImp, 0xff7a2a, 30, { op: 0.55 });
  for (let s = sFall; s < sImp; s += 0.08) st.sparks(V(q.tx, yAt(s) + 10, q.tz), s, 10, { color: 0xff8a3a, speed: 4, life: 0.7, grav: -6 });
}
function redSky(st, s0, s1) {
  st.skyFn = s => 0.9 * fade(s, s0, s1, 0.4, 0.8);
  st.gradeFn = s => { const k = fade(s, s0, s1, 0.4, 0.8); return { tint: [1, lerp(1, 0.7, k), lerp(1, 0.62, k)], sat: lerp(1, 0.8, k), vig: 0.2 + 0.5 * k, vigCol: [0.25 * k, 0, 0], bloom: 0.6 + 0.4 * k }; };
}

const qBoard = {
  ...qBase, chip: '퀸 희생 · 시전 연출 ②', tag: '추천', name: '대국: 시계 · 손 · 평가 막대', cornerTail: '② 대국', cutin: false,
  line: 'Q를 누르면 코스 옆 대국 시계 "탁!" — 상대 시계가 1.5초 동안 빨갛게 돌고, 하늘에서 우리 손이 내려와 퀸을 둔다',
  spec: [
    ['시작', 'Q → 코스 옆에 대국 시계, 우리 버튼 "탁!" · 상대 시계가 1.5초 빨갛게 (예고)'],
    ['본', '하늘에서 우리 팀의 손이 내려와 퀸을 집어 들고, 시간패 깃발이 떨어지는 순간 적 무리에 쾅'],
    ['끝', '평가 막대가 우리 쪽으로 출렁 · 둔 수에 !!, 맞은 적마다 ??, 아슬하게 피한 적은 ?!'],
    ['멈춤', '없음 — 온라인 그대로'],
    ['만들 것', '손 모델과 궁극기마다 손동작 · 시계 · 평가 막대'],
  ],
  build(st) {
    const sPress = ACT + 0.03, sDesc = ACT + 0.05, sGrab = 4.15, sLift = 4.35, sFire = 4.75, sTop = 4.95, sWind = 5.1, sImp = 5.25;
    st.knots = flowSlow(sImp, 0.55, 0.3);
    st.sky.material.uniforms.darkCol.value.set(0x0c0a24);
    let G = null, K = null;
    const q = queenScene(st, { sImp, sWarn: sPress + 0.2, giantPre: s => (s < sFire ? null : { ...base(s), k: 8, ry: 0.4 }) });
    const Q = q.Q, qp = Q.pred(4.0), hd = V(qp.x, 1.45, qp.z);
    const top = V(q.tx, 23, q.tz - 3), slam = V(q.tx, 11.6, q.tz);
    G = path([[sDesc, hd.clone().add(V(0, 40, -16))], [sGrab, hd, easeOut], [sLift, hd], [sTop, top], [sWind, top.clone().add(V(0, 5, -1)), easeOut], [sImp, slam, easeIn], [sImp + 0.35, slam], [sImp + 1.3, slam.clone().add(V(0, 60, -20)), easeIn]]);
    const arc = s => G(s).add(V(0, s > sLift && s < sTop ? 7 * Math.sin(Math.PI * prog(s, sLift, sTop)) : 0, 0));
    K = s => 1 + 7 * easeOut(prog(s, sLift, sFire + 0.05));
    const base = s => { const g = arc(s); return { x: g.x, y: g.y - 1.45 * K(s), z: g.z }; };
    const h = makeHand();
    handFx(st, h, s => (s < sDesc || s > sImp + 1.3 ? null : { g: arc(s), pitch: 1.2, curl: s < sGrab ? 0.15 : s < sLift ? lerp(0.15, 0.85, smooth(prog(s, sGrab, sLift))) : s < sImp + 0.15 ? 0.85 : lerp(0.85, 0.12, smooth(prog(s, sImp + 0.15, sImp + 0.35))) }));
    Q.until(4.0).custom(sGrab - 4.0, (u, t, a) => ({ ...a, run: 0, pitch: -0.25 * smooth(u) })).set({ hide: 1, track: 1 })
      .custom(sImp - sGrab, (u, t, a) => { const g = arc(sGrab + t); return { ...a, x: g.x, z: g.z }; })
      .hold(3.6).set({ hide: 0, track: 0, x: qp.x, z: qp.z, y: 0, pitch: 0, run: 0 }).stand(0.4).run(20);
    const white = makePiece('queen', 'ally');
    st.obj(white, s => { white.visible = s >= sGrab && s < sFire; if (!white.visible) return; const b = base(s); white.position.set(b.x, b.y, b.z); white.scale.setScalar(K(s)); white.rotation.set(0.05 * Math.sin(s * 7), 0, 0.06 * Math.sin(s * 5)); });
    st.flash(s => { const b = base(sFire); return V(b.x, b.y + 6, b.z); }, sFire, 0.4, 16, 0xffb060); st.sparks(V(0, 0, 0).copy(arc(sFire)).add(V(0, -6, 0)), sFire, 50, { color: 0xff8a3a, speed: 8 });
    for (let s = sFire; s < sImp; s += 0.08) st.sparks(arc(s).add(V(0, -5, 0)), s, 8, { color: 0xff8a3a, speed: 4, life: 0.6, grav: -6 });
    st.skyFn = s => 0.6 * fade(s, sPress, sImp + 1.2, 0.4, 0.8);
    st.gradeFn = s => ({ vig: 0.2 + 0.25 * fade(s, sPress, sImp + 1, 0.3, 0.8) });
    const ck = clockFx(st, V(-12.5, 2.4, qp.z + 7), sPress, sImp, sImp + 3.2);
    void ck;
    st.evalOn = true; st.evalKick = sImp;
    badge(st, V(q.tx, 15, q.tz), sImp + 0.05, sImp + 2.2, '!!');
    for (const e of q.hit) badge(st, s => e.pos(s, 1.2), sImp + 0.15, sImp + 2.0, '??');
    badge(st, s => q.E.king.pos(s, 1.4), sImp + 0.05, sImp + 2.0, '?!');
    opening(st, Q, '상대 6명이 몰려서 20 m 앞 — 같은 장면을 네 가지 시전 연출로');
    st.shot(ACT, () => ({ p: V(qp.x + 11, 5, qp.z - 19), l: V(qp.x - 2, 10, qp.z + 4), fov: 60 }));
    st.shot(st.C(sLift - 0.05), () => ({ p: V(-32, 21, (qp.z + q.tz) / 2 - 20), l: V(2, 13, (qp.z + q.tz) / 2 + 4), fov: 58 }), { blend: 0.35 });
    st.cap(ACT + 0.02, st.C(sLift - 0.05), 'Q — 코스 옆 대국 시계 "탁!" · 상대 시계가 빨갛게 돌기 시작 (1.5초)', '시계 치는 소리 "탁!" + 초침 "틱틱틱"');
    st.cap(st.C(sLift - 0.05), st.C(sImp), '하늘에서 우리 팀의 손 — 퀸을 집어 들어 적 무리 위로', '구름 갈라지는 소리 + 성가대');
    queenAfter(st, q, sImp, { hitLine: '시간패 깃발과 함께 쾅 — 평가 막대가 우리 쪽으로, 맞은 적마다 ??', hitMemo: '"탁!" — 쾅!! — 관중 "오오—"' });
    finish(st, st.C(sImp + 3.2));
  },
};

const qWake = {
  ...qBase, chip: '퀸 희생 · 시전 연출 ③', tag: '각성', name: '각성: 흑백 세계 · 진짜 모습', cornerTail: '③ 각성', cutin: false,
  line: 'Q를 누르면 세상이 흑과 백만 남고 퀸만 색을 가진다 — 체스말 껍데기가 깨지며 진짜 여왕이 나와 하늘로',
  spec: [
    ['시작', 'Q → 파동과 함께 세상이 흑과 백만 남고 시전자만 색 · 0.9초 정지'],
    ['본', '체스말 껍데기가 깨지며 진짜 모습 (지팡이 든 여왕) → 유리 깨지듯 색이 돌아오고 하늘로'],
    ['끝', '거대 퀸 낙하 · 5초 뒤 다시 체스말로'],
    ['멈춤', '0.9초 — 온라인은 그림만 멈춤'],
    ['만들 것', '기물마다 진짜 모습 모델 하나씩 (6개) · 흑백 화면 효과'],
  ],
  build(st) {
    const sCrack = ACT + 0.03, sShow = ACT + 0.035, sBack = ACT + 0.12, sLeap0 = 3.78, sLeap1 = 4.35, sImp = 5.5, sFall = sImp - 0.9;
    st.knots = flow([1.1, 0.12], [sImp - sBack, sImp - sBack], [0.55 / 0.3, 0.55]);
    const q = queenScene(st, { sImp, sWarn: 4.05, giantPre: s => (s < sFall ? null : { x: q.tx, y: lerp(95, 0, easeIn(prog(s, sFall, sImp))), z: q.tz }) });
    fallFx(st, q, sFall, sImp);
    const Q = q.Q, q0 = Q.pred(ACT);
    keepColour(Q.pivot);
    Q.until(ACT).stand(sShow - ACT).set({ hide: 1, track: 1 }).hold(sImp + 3.6 - sShow).set({ hide: 0, track: 0, x: q0.x, z: q0.z, y: 0, run: 0 }).stand(0.4).run(20);
    const tq = makeTrueQueen(); keepColour(tq.g);
    const ty = s => (s < sLeap0 ? 0 : 70 * easeIn(prog(s, sLeap0, sLeap1)));
    st.obj(tq.g, s => {
      tq.g.visible = s >= sShow && s < sLeap1; if (!tq.g.visible) return;
      tq.g.position.set(q0.x, ty(s), q0.z); tq.g.scale.setScalar(1.15 * (0.7 + 0.3 * back(prog(s, sShow, sShow + 0.03))));
      tq.armR.rotation.x = -lerp(0.2, 2.7, smooth(prog(s, sShow + 0.02, sShow + 0.07))); tq.g.rotation.y = s > sLeap0 ? (s - sLeap0) * 12 : 0;
    });
    const tr = { pos: s => V(q0.x, ty(s) + 1.2, q0.z) };
    st.trail(tr, sLeap0, sLeap1, COL.gold, { n: 14, dt: 0.02, size: 1.6 });
    shards(st, V(q0.x, 0, q0.z), sCrack, 30, MAT.white, { keep: true });
    st.ring(q0.x, q0.z, ACT, 0.08, 0.5, 45, 0xffffff, { w: 0.03 });
    st.aura(V(q0.x, 1.4, q0.z), sShow, sBack, 0xffd890, 4, { op: 0.6 });
    st.gradeFn = s => { const bw = s >= ACT && s < sBack ? 1 : 0; return { sat: bw ? 0 : 1, contrast: bw ? 1.45 : 1, maskOn: bw, vig: bw ? 0.45 : 0.2, tint: bw ? [0.92, 0.92, 0.92] : [1, 1, 1] }; };
    st.cracks.push({ c0: st.C(sBack), d: 0.5 });
    st.label(V(q0.x, 3.4, q0.z), sShow + 0.02, sBack, '진짜 모습', { size: 22, color: '#ffe8a0' });
    opening(st, Q, '상대 6명이 몰려서 20 m 앞 — 같은 장면을 네 가지 시전 연출로');
    st.shot(ACT, (c) => { const k = smooth(prog(c, ACT, ACT + 1.1)); return { p: V(q0.x + lerp(3.2, 2.2, k), lerp(2.0, 1.7, k), q0.z + lerp(6.0, 4.4, k)), l: V(q0.x, 1.4, q0.z), fov: 50 }; });
    st.shot(st.C(sBack + 0.02), (c, s) => ({ p: V(q0.x + 5, 1.2, q0.z - 7), l: V(q0.x, Math.min(ty(s) + 2, 30), q0.z + 2), fov: 62 }));
    st.shot(st.C(sLeap1), fixed(V(-11, 15, q.tz - 36), V(q.tx, 10, q.tz), 56));
    st.stamp(st.C(sShow + 0.03), 1.2, '각성!!', { size: 112, sub: '체스말 껍데기를 깨고' });
    st.cap(ACT + 0.05, st.C(sBack), 'Q — 세상이 흑과 백만 남고, 퀸만 색을 가진다', '모든 소리가 뚝 — 심장 소리 "쿵… 쿵…"');
    st.cap(st.C(sBack), st.C(sImp), '유리 깨지듯 색이 돌아오고 — 진짜 여왕이 하늘로, 1.2초 뒤 거대 퀸', '쨍그랑 + 성가대');
    queenAfter(st, q, sImp);
    finish(st, st.C(sImp + 3.2));
  },
};

const qSay = {
  ...qBase, chip: '퀸 희생 · 시전 연출 ④', tag: '가장 단순', name: '선언하고 둔다', cornerTail: '④ 선언',
  cutin: false,
  line: 'Q를 누르면 퀸 머리 위 공중에 기보가 커다랗게 — 맞을 칸이 바닥에 깔리고, 1초 뒤에 떨어진다. 아무것도 멈추지 않는다',
  spec: [
    ['시작', 'Q → 시전자 머리 위에 3D 기보 "Q×h7+!!" · 바닥에 맞을 칸이 빨갛게'],
    ['예고', '1초 — 모두가 경기 화면 안에서 본다'],
    ['멈춤', '없음 — 온라인 그대로'],
    ['좋은 점', '가장 싸고 빠르다 · 상대에게 피할 1초'],
    ['아쉬운 점', '조용하다 — 극적인 맛은 덜함'],
  ],
  build(st) {
    const sSay = ACT + 0.02, sLeap0 = 4.6, sLeap1 = 4.95, sImp = 5.2, sFall = sImp - 0.5;
    st.knots = flowSlow(sImp, 0.55, 0.3);
    const q = queenScene(st, { sImp, sWarn: sSay, giantPre: s => (s < sFall ? null : { x: q.tx, y: lerp(95, 0, easeIn(prog(s, sFall, sImp))), z: q.tz }) });
    fallFx(st, q, sFall, sImp);
    const Q = q.Q, q0 = Q.pred(sLeap0);
    Q.until(sLeap0 - 0.1).custom(0.1, (u, t, a) => ({ ...a, run: 0, sq: lerp(1, 0.8, u) })).set({ sq: 1 })
      .custom(sLeap1 - sLeap0, (u, t, a) => ({ ...a, y: 70 * easeIn(u), yaw: a.yaw + u * 10, run: 0 })).set({ hide: 1, track: 1, y: 0 })
      .hold(sImp + 3.6 - sLeap1).set({ hide: 0, track: 0, x: q0.x, z: q0.z, y: 0, run: 0 }).stand(0.4).run(20);
    st.trail(Q, sLeap0, sLeap1, COL.gold, { n: 14, dt: 0.02, size: 1.6 });
    const word = textSprite('Q×h7+!!', '#ffc94a', 2.2);
    st.obj(word, s => { word.visible = s >= sSay && s < sLeap1; if (!word.visible) return; word.userData.draw();
      const p = Q.pos(Math.min(s, sLeap0), 2.9); word.position.set(p.x, p.y + (s > sLeap0 ? 70 * easeIn(prog(s, sLeap0, sLeap1)) : 0), p.z);
      const k = back(prog(s, sSay, sSay + 0.2)) * (1 + 0.06 * Math.sin(s * 14)); word.scale.set(8.8 * k, 2.2 * k, 1); word.material.opacity = 1 - prog(s, sLeap0 + 0.1, sLeap1); });
    st.ring(Q.pred(sSay).x, Q.pred(sSay).z, sSay, 1.0, 4, 0.8, COL.gold, { w: 0.15, op: 0.9 });
    st.aura(s => Q.pos(s), sSay, sLeap0, COL.gold, 2.4, { op: 0.6, rate: 14 });
    st.label(V(q.tx, 3, q.tz), sSay + 0.1, sLeap0, '1초 뒤', { size: 24, color: '#ff8080' });
    opening(st, Q, '상대 6명이 몰려서 20 m 앞 — 같은 장면을 네 가지 시전 연출로');
    st.shot(ACT, (c, s) => { const p = Q.pos(Math.min(s, sLeap0)); return { p: p.clone().add(V(3.5, 3.4, -8.5)), l: p.clone().add(V(0, 2.2, 9)), fov: 54 }; });
    st.shot(st.C(sLeap0 - 0.15), fixed(V(-11, 15, q.tz - 36), V(q.tx, 9, q.tz), 56), { blend: 0.3 });
    st.cap(ACT + 0.03, st.C(sLeap0), 'Q — 퀸 머리 위 공중에 기보가 새겨지고, 맞을 칸이 바닥에 깔린다', '"퀸, h7!" 외치는 목소리 + 체크 알림음');
    st.cap(st.C(sLeap0), st.C(sImp), '1초 뒤 퀸이 하늘로 — 거대 퀸이 바로 떨어진다', '슈웅 — 쾅');
    queenAfter(st, q, sImp);
    finish(st, st.C(sImp + 3.2));
  },
};

// ================================================================ the recommended set on the table flip and the grand castling
const flipBoard = {
  kind: 'king', opt: '', cut: ACT, cutin: false, nota: '┻━┻!!', dur: 14,
  chip: '대국 조합 · 킹 B안', tag: '추천 조합', name: '판 엎기 — 손이 판을 엎는다', cornerHead: '대국 조합', cornerTail: '판 엎기',
  line: '대국 시계 "탁!" — 하늘에서 내려온 우리 손이 판 모서리를 잡고, 시간패 깃발이 떨어지는 순간 판을 통째로 엎는다',
  spec: [
    ['시작', 'Q → 대국 시계 "탁!" · 상대 시계 1.5초 · 엎힐 구역이 빨갛게'],
    ['본', '하늘에서 손이 내려와 판 모서리를 잡고, 깃발이 떨어지는 순간 엎는다 · 엎은 판도 손이 다시 눌러 놓음'],
    ['끝', '평가 막대가 우리 쪽으로 · 둔 수에 !!, 날아간 적마다 ??'],
    ['소리', '킹: 나팔 팡파르 + 와당탕 (말 쏟아지는 소리)'],
  ],
  build(st) {
    const T = teams(st, 'king', { ally: { king: [0, 46] }, enemy: { queen: [0.5, 68], knight: [-3.2, 66.5], rook: [3.0, 65], bishop: [-1, 63.5], pawn: [4.6, 62], king: [-4.6, 60.5] } });
    const K = T.me, sPress = ACT + 0.03, sDesc = ACT + 0.08, sF = ACT + 1.5, flip = 0.42, sTop = sF + flip, sBack = sTop + 0.45, sDown = sBack + 0.42;
    st.knots = flowSlow(sF + 0.2, 0.7, 0.35);
    K.until(ACT).stand(0.05).hop(0.3, K.pred(ACT).x, K.pred(ACT).z, { h: 0.7 }).hop(0.35, K.pred(ACT).x, K.pred(ACT).z, { h: 1.0 }).custom(0.2, (u, t, a) => ({ ...a, y: 0 })).run(20);
    const rear = Math.min(...T.enemies.map(e => e.pred(sF).z)), z0 = Math.floor((rear - 4) / SQ) * SQ, L = 30, z1 = z0 + L;
    const tex = TEX.board.clone(); tex.needsUpdate = true; tex.repeat.set(1, L / 16);
    const hinge = new THREE.Group(); hinge.position.set(0, 0, z0);
    const slab = new THREE.Mesh(new THREE.BoxGeometry(HALF * 2, 0.5, L), [MAT.dark, MAT.dark, new THREE.MeshStandardMaterial({ map: tex, roughness: 0.6 }), MAT.dark, MAT.dark, MAT.dark]);
    slab.position.set(0, -0.24, L / 2); slab.castShadow = true; slab.receiveShadow = true; hinge.add(slab);
    const pit = new THREE.Mesh(new THREE.PlaneGeometry(HALF * 2, L), basic(0x120a06, 1, { depthWrite: true })); pit.rotation.x = -Math.PI / 2; pit.position.set(0, 0.004, z0 + L / 2);
    st.obj(pit, s => { pit.visible = s > sF && s < sDown; });
    const ang = s => (s < sF ? 0 : s < sTop ? -1.95 * easeIn(prog(s, sF, sTop)) ** 0.8 : s < sBack ? -1.95 : -1.95 * (1 - easeIn(prog(s, sBack, sDown))));
    st.obj(hinge, s => { hinge.visible = s >= ACT; hinge.rotation.x = ang(s); slab.position.y = s > sF ? -0.23 : -0.24; });
    const edgeAt = s => V(0, -L * Math.sin(ang(s)) + 0.4, z0 + L * Math.cos(ang(s)) + 0.6);
    const reach = path([[sDesc, V(0, 46, z1 + 6)], [sF - 0.3, edgeAt(sF), easeOut]]);
    const h = makeHand();
    handFx(st, h, s => {
      if (s < sDesc || s > sDown + 1.1) return null;
      if (s < sF) return { g: reach(s), pitch: 1.3, curl: s < sF - 0.3 ? 0.15 : lerp(0.15, 0.9, smooth(prog(s, sF - 0.3, sF - 0.05))) };
      if (s < sTop) return { g: edgeAt(s), pitch: 1.3 + 0.75 * ang(s), curl: 0.9 };
      if (s < sBack) return { g: edgeAt(sTop).add(V(0, 3 * smooth(prog(s, sTop, sBack)), -2 * smooth(prog(s, sTop, sBack)))), pitch: 1.3 + 0.75 * ang(sTop), curl: lerp(0.9, 0.1, smooth(prog(s, sTop, sTop + 0.2))) };
      if (s < sDown) return { g: edgeAt(s).add(V(0, 1.8, -3)), pitch: lerp(0.2, 1.45, prog(s, sBack, sDown)), curl: 0.1 };
      return { g: edgeAt(sDown).add(V(0, 1.8 + 60 * easeIn(prog(s, sDown + 0.1, sDown + 1.1)), -3)), pitch: 1.45, curl: 0.1 };
    });
    const edge = []; for (let i = 0; i < 8; i++) { edge.push({ x: cellX(i), z: z0 + 1, d: i * 0.02 }); edge.push({ x: cellX(i), z: z1 - 1, d: i * 0.02 }); }
    for (let j = 1; j < L / SQ - 1; j++) { edge.push({ x: cellX(0), z: z0 + 1 + j * SQ, d: j * 0.01 }); edge.push({ x: cellX(7), z: z0 + 1 + j * SQ, d: j * 0.01 }); }
    st.tiles(edge, sPress, sF, COL.red, { op: 1, pulse: 22 });
    st.shake(ACT + 0.35, 0.3, 0.35); st.shake(ACT + 0.7, 0.3, 0.45); st.shake(sF, 0.6, 0.6); st.shake(sDown, 0.9, 0.9); st.screen(sDown, 0.2, [1, 1, 1], 0.45);
    st.dust(0, z0 + 4, sDown, { n: 14, r: 10, size: 5, life: 1.8 }); st.dust(0, z1 - 6, sDown, { n: 14, r: 10, size: 5, life: 1.8 });
    for (const e of T.enemies) {
      const p = e.pred(sF + 0.12);
      if (p.z < z0 || p.z > z1) { e.run(20); continue; }
      const r = rng(Math.round(p.z * 10)), rel = p.z - z0, sl = sF + 0.1 + (1 - rel / L) * 0.12;
      e.until(sl).fly(2.1, clamp(p.x + (r() - 0.5) * 8, -7, 7), p.z - (26 + rel * 0.35 + r() * 4), { h: 15 + rel * 0.25, spins: 2 + (r() > 0.4 ? 1 : 0), roll: 1.6 }).lie(3.0).getUp(0.5).run(20);
      badge(st, s => e.pos(s, 1.1), sl + 0.25, sl + 2.0, '??');
    }
    for (const a of T.allies) if (a !== K) a.run(20);
    clockFx(st, V(-12.5, 2.4, K.pred(ACT).z + 8), sPress, sF, sDown + 2.5);
    st.evalOn = true; st.evalKick = sF + 0.3;
    badge(st, s => K.pos(s, 1.4), sF, sF + 2.0, '!!');
    st.skyFn = s => 0.5 * fade(s, sPress, sDown + 1, 0.4, 0.8); st.sky.material.uniforms.darkCol.value.set(0x0c0a24);
    opening(st, K, '상대 6명이 한 구역에 몰려 20 m 앞');
    st.shot(ACT, (c, s) => { const p = K.pos(s); return { p: p.clone().add(V(4.5, 3.2, -9)), l: p.clone().add(V(-2, 5, 16)), fov: 58 }; });
    st.shot(st.C(sF - 0.45), fixed(V(34, 12, z0 - 6), V(0, 10, z0 - 2), 58), { blend: 0.35 });
    st.shot(st.C(sDown + 0.2), (c, s) => { const p = T.A.knight.pos(s); return { p: p.clone().add(V(4, 4.2, 8)), l: p.clone().add(V(0, 1, -16)), fov: 56 }; });
    st.cap(ACT + 0.02, st.C(sF - 0.45), 'Q — 대국 시계 "탁!" · 상대 시계가 빨갛게 · 우리 손이 판 모서리를 잡는다', '"탁!" + 초침 "틱틱틱" + 나팔');
    st.cap(st.C(sF - 0.45), st.C(sDown + 0.2), '시간패 깃발이 떨어지는 순간 — 손이 판을 통째로 엎는다', '와당탕!!! (체스말 쏟아지는 소리)');
    st.cap(st.C(sDown + 0.2), st.C(sDown + 2.2), '손이 판을 다시 눌러 놓고 — 평가 막대가 우리 쪽으로, 날아간 적마다 ??', '쾅 + 관중 "오오오—"');
    st.stamp(st.C(sF + 0.1), 1.6, '판 엎기!!', { size: 118, sub: '(╯°□°)╯︵ ┻━┻' });
    finish(st, st.C(sDown + 2.2));
  },
};

const castleHands = {
  kind: 'rook', opt: '', cut: ACT, cutin: false, nota: 'O-O-O!!', dur: 14,
  chip: '대국 조합 · 룩 A안', tag: '추천 조합', name: '대캐슬링 — 두 손이 바꿔 놓는다', cornerHead: '대국 조합', cornerTail: '대캐슬링',
  line: '대국 시계 "탁!" — 하늘에서 우리 두 손이 내려와 양 팀을 한 줌씩 집어, 엇갈려 옮겨 놓는다',
  spec: [
    ['시작', 'Q → 대국 시계 "탁!" · 양 팀 발밑에 빛 원과 짝 선 (1.5초)'],
    ['본', '두 손이 우리 팀과 상대 팀을 한 줌씩 집어 들고, 공중에서 엇갈려 서로의 자리에 내려놓음'],
    ['끝', '시간패 깃발 · 평가 막대가 우리 쪽으로 · 상대는 ?? 찍힌 채 1.5초 비틀'],
    ['소리', '룩: 성문 쇠사슬 "철컹" + 체스말 달그락'],
  ],
  build(st) {
    const T = teams(st, 'rook');
    const R = T.me, sPress = ACT + 0.03, sDesc = ACT + 0.08, sGrab = ACT + 0.65, sLift = sGrab + 0.2, sOver = sLift + 1.1, sDrop = sOver + 0.15;
    st.knots = flowSlow(sLift + 0.35, 0.7, 0.4);
    const A = [...T.allies].sort((a, b) => b.pred(sGrab).z - a.pred(sGrab).z), E = [...T.enemies].sort((a, b) => b.pred(sGrab).z - a.pred(sGrab).z);
    const ca = centroid(A, sGrab), ce = centroid(E, sGrab), CA = V(ca.x, 2.0, ca.z), CE = V(ce.x, 2.0, ce.z);
    const carry = (from, to, side, lift) => s => {
      if (s < sGrab) return path([[sDesc, from.clone().add(V(side * 6, 42, -14))], [sGrab, from, easeOut]])(s);
      if (s < sLift) return from.clone();
      const u = smooth(prog(s, sLift, sOver)); const p = from.clone().lerp(to, u); p.x += side * 7 * Math.sin(Math.PI * u); p.y += lift * Math.sin(Math.PI * u) + 4 * Math.sin(Math.PI * Math.min(1, u * 1.2));
      if (s > sDrop) p.y += 50 * easeIn(prog(s, sDrop + 0.15, sDrop + 1.1));
      return p;
    };
    const GA = carry(CA, CE, -1, 12), GE = carry(CE, CA, 1, 7);
    const curl = s => (s < sGrab - 0.15 ? 0.15 : s < sLift ? lerp(0.15, 0.8, smooth(prog(s, sGrab - 0.15, sLift))) : s < sDrop ? 0.8 : lerp(0.8, 0.12, smooth(prog(s, sDrop, sDrop + 0.2))));
    const hl = makeHand({ left: true }), hr = makeHand();
    handFx(st, hl, s => (s < sDesc || s > sDrop + 1.1 ? null : { g: GA(s), pitch: 1.25, yaw: 0.1, curl: curl(s) }));
    handFx(st, hr, s => (s < sDesc || s > sDrop + 1.1 ? null : { g: GE(s), pitch: 1.25, yaw: -0.1, curl: curl(s) }));
    const hang = (a, G, c0, i) => { const p0 = a.pred(sGrab), off = V((p0.x - c0.x) * 0.4, -3.4 - (i % 2) * 0.5, (p0.z - c0.z) * 0.35);
      return (u, t, st0) => { const s = sGrab + t, k = smooth(prog(s, sGrab, sLift)), g = G(s).add(off);
        return { ...st0, x: lerp(p0.x, g.x, k), y: lerp(0, Math.max(0, g.y), k), z: lerp(p0.z, g.z, k), run: 0, pitch: 0.25 * Math.sin(s * 6 + i) * k, roll: 0.25 * Math.sin(s * 5 + i * 2) * k }; }; };
    for (let i = 0; i < 6; i++) {
      const a = A[i], e = E[i], pa = a.pred(sGrab), pe = e.pred(sGrab);
      st.ring(pa.x, pa.z, sMarkOf(), 0.6, 0.4, 1.2, COL.ally, { w: 0.25 }); st.ring(pe.x, pe.z, sMarkOf(), 0.6, 0.4, 1.2, COL.enemy, { w: 0.25 });
      st.beam(V(pa.x, 0.25, pa.z), V(pe.x, 0.25, pe.z), sPress + 0.1 + i * 0.04, sLift, 0.06, COL.gold, { grow: 0.25 });
      a.until(sGrab).custom(sDrop - sGrab, hang(a, GA, ca, i)).hop(0.45, pe.x, pe.z, { h: 1.6, yaw: 0 }).custom(0.25, (u, t, q) => ({ ...q, sq: u < 0.4 ? lerp(0.75, 1, u / 0.4) : 1, y: 0 })).run(20);
      e.until(sGrab).custom(sDrop - sGrab, hang(e, GE, ce, i)).fly(0.55, pa.x, pa.z, { h: 1.6, spins: 0 }).lie(0.7, { slide: 0.3 }).getUp(0.5).custom(1.0, (u, t, q) => ({ ...q, run: 0, roll: 0.15 * Math.sin(t * 9) })).run(20);
      st.dust(pe.x, pe.z, sDrop + 0.45, { n: 5, r: 1.6, size: 1.6 }); st.dust(pa.x, pa.z, sDrop + 0.55, { n: 5, r: 1.6, size: 1.6 });
      badge(st, s => e.pos(s, 1.1), sDrop + 0.6, sDrop + 2.2, '??');
    }
    function sMarkOf() { return sPress + 0.05; }
    clockFx(st, V(-12.5, 2.4, R.pred(ACT).z + 8), sPress, sDrop, sDrop + 3.0);
    st.evalOn = true; st.evalKick = sDrop + 0.4;
    badge(st, s => R.pos(s, 1.4), sDrop + 0.5, sDrop + 2.4, '!!');
    st.shake(sDrop + 0.5, 0.5, 0.35); st.sky.material.uniforms.darkCol.value.set(0x0c0a24); st.skyFn = s => 0.45 * fade(s, sPress, sDrop + 1.2, 0.4, 0.8);
    const zm = (ca.z + ce.z) / 2;
    opening(st, R, '상대가 20 m 앞 · 순위로 치면 우리 팀 6명이 전부 뒤');
    st.shot(ACT, () => ({ p: V(24, 20, ca.z - 20), l: V(-2, 6, zm), fov: 58 }));
    st.shot(st.C(sLift - 0.1), (c, s) => ({ p: V(38, 14, zm), l: V(0, 9, zm), fov: 56 }), { blend: 0.4 });
    st.shot(st.C(sDrop + 0.6), () => { const p = E[0].pred(sGrab); return { p: V(4, 3.4, p.z + 6), l: V(0, 1, p.z - 13), fov: 56 }; });
    st.cap(ACT + 0.02, st.C(sLift - 0.1), 'Q — 대국 시계 "탁!" · 하늘에서 우리 두 손이 내려와 양 팀을 한 줌씩', '"탁!" + 쇠사슬 "철컹"');
    st.cap(st.C(sLift - 0.1), st.C(sDrop + 0.6), '두 손이 공중에서 엇갈려 — 서로의 자리에 내려놓는다', '체스말 달그락 + 휘이익');
    st.cap(st.C(sDrop + 0.6), st.C(sDrop + 2.4), '시간패 깃발 — 평가 막대가 우리 쪽으로, 상대는 ?? 찍힌 채 비틀', '쿵 — 관중 "오오오?!"');
    st.stamp(st.C(sDrop + 0.5), 1.6, 'O-O-O!!', { size: 128, sub: '대캐슬링' });
    finish(st, st.C(sDrop + 2.4));
  },
};

export const CASTS = { qCut, qBoard, qWake, qSay, flipBoard, castleHands };
export { queenScene, queenAfter, fallFx, QPACK };
