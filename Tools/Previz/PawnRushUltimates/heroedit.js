// Pawn Rush ultimates previz - the user's chess edit shorts, brought into the game's third-person view.
// Nothing cuts away: the edits' language happens in the world and on the beat. The ground's tiles light in the
// move colours (teal !! under the caster, red ?? under the ones it will hit), !! and ?? marks float over the
// pieces, the camera kicks on every beat of the ultimate's track, the sky turns to a starry space, and at the
// impact the ground ripples and its tiles break loose and float - as the board breaks apart in the shorts.
import { THREE, V, TEX, lerp, clamp, prog, smooth, easeOut, easeIn, fade, rng, makePiece, keepColour } from './stage.js';
import { DISP } from './hud.js';
import { ACT, finish } from './ults.js';
import { queenScene } from './casts.js';
import { buildTown, spotlight, spirit, behind } from './hero.js';

const TL = 1.5, TX0 = -12.25, TZ0 = -44.25;   // the town street's floor tiles
const tileOf = (x, z) => ({ x: TX0 + Math.round((x - TX0) / TL) * TL, z: TZ0 + Math.round((z - TZ0) / TL) * TL });

/** floor tiles lighting up in a move colour, each bursting bright when it comes on */
function glowTiles(st, list, s0, s1, color, o = {}) {
  const geo = new THREE.PlaneGeometry(TL * 0.95, TL * 0.95); geo.rotateX(-Math.PI / 2);
  const im = new THREE.InstancedMesh(geo, new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: o.op ?? 0.95, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }), list.length);
  im.frustumCulled = false; keepColour(im);
  const m4 = new THREE.Matrix4(), base = new THREE.Color(color), k = new THREE.Color();
  list.forEach((q, i) => { m4.makeTranslation(q.x, 0.03, q.z); im.setMatrixAt(i, m4); im.setColorAt(i, k.setRGB(0, 0, 0)); });
  st.obj(im, s => {
    im.visible = s >= s0 && s <= s1; if (!im.visible) return;
    list.forEach((q, i) => { const t0 = s0 + (q.d ?? 0); let f = fade(s, t0, s1, 0.03, o.fo ?? 0.3); f *= 1 + 0.9 * Math.exp(-Math.max(0, s - t0) / 0.12); if (o.pulse) f *= 0.78 + 0.22 * Math.sin(s * o.pulse); im.setColorAt(i, k.copy(base).multiplyScalar(f * (q.w ?? 1))); });
    im.instanceColor.needsUpdate = true;
  });
}
/** a move mark (!!, ??) floating in the world */
const ETEX = {};
function emblemTex(text, bg) {
  const key = text + bg; if (ETEX[key]) return ETEX[key];
  const c = document.createElement('canvas'); c.width = c.height = 256;
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  t.userData.draw = () => {
    if (t.userData.done) return; t.userData.done = true; const g = c.getContext('2d');
    g.beginPath(); g.arc(128, 128, 110, 0, Math.PI * 2); g.fillStyle = bg; g.fill(); g.lineWidth = 12; g.strokeStyle = '#ffffff'; g.stroke();
    g.font = `${text.length > 1 ? 118 : 150}px ${DISP}`; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillStyle = '#ffffff'; g.fillText(text, 128, 140); t.needsUpdate = true;
  };
  return (ETEX[key] = t);
}
function emblem(st, pos, s0, s1, text, bg, size, o = {}) {
  const tex = emblemTex(text, bg);
  const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true, depthWrite: false, depthTest: !o.always, toneMapped: false })); keepColour(sp); if (o.always) sp.renderOrder = 10;
  st.obj(sp, s => { const k = fade(s, s0, s1, 0.02, 0.25); sp.visible = k > 0; if (!sp.visible) return; tex.userData.draw();
    sp.position.copy(pos(s)); const sz = size * (1 + 0.7 * (1 - easeOut(prog(s, s0, s0 + 0.16)))) * (1 + 0.04 * Math.sin(s * 9)); sp.scale.set(sz, sz, 1); sp.material.opacity = k; });
}
/** the sky turned to a starry space, with a few nebulae */
function space(st, ctr, s0, s1) {
  const r = rng(9), n = 1000, arr = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { const th = r() * Math.PI * 2, ph = Math.acos(r() * 0.97), R = 320; arr.set([ctr.x + R * Math.sin(ph) * Math.cos(th), R * Math.cos(ph) - 10, ctr.z + R * Math.sin(ph) * Math.sin(th)], i * 3); }
  const geo = new THREE.BufferGeometry(); geo.setAttribute('position', new THREE.BufferAttribute(arr, 3));
  const mat = new THREE.PointsMaterial({ color: 0xffffff, size: 2.2, sizeAttenuation: false, transparent: true, toneMapped: false, fog: false, depthWrite: false });
  const pts = new THREE.Points(geo, mat); pts.frustumCulled = false; keepColour(pts);
  st.obj(pts, s => { const k = fade(s, s0, s1, 0.3, 0.8); pts.visible = k > 0; mat.opacity = k; });
  const cols = [0x7a4dff, 0x1fb99a, 0xff4fa0, 0x4f86e6];
  for (let i = 0; i < 8; i++) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.smoke, color: cols[i % 4], transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false, fog: false })); keepColour(sp);
    const th = r() * Math.PI * 2, ph = 0.25 + r() * 0.9, R = 290, p = V(ctr.x + R * Math.sin(ph) * Math.cos(th), R * Math.cos(ph), ctr.z + R * Math.sin(ph) * Math.sin(th)), size = 150 + r() * 130;
    st.obj(sp, s => { const k = fade(s, s0, s1, 0.4, 0.8); sp.visible = k > 0; if (!sp.visible) return; sp.position.copy(p); sp.scale.set(size, size, 1); sp.material.opacity = 0.32 * k; });
  }
}
/** the ground ripples out from the impact, tile by tile */
function ripple(st, ctr, s0, R = 22, v = 24) {
  const list = [];
  for (let x = TX0; x <= 12.3; x += TL) for (let z = ctr.z - R; z <= ctr.z + R; z += TL) { const q = tileOf(x, z), d = Math.hypot(q.x - ctr.x, q.z - ctr.z); if (d <= R && d > 6) list.push({ ...q, d }); }
  const im = new THREE.InstancedMesh(new THREE.BoxGeometry(TL * 0.97, 0.35, TL * 0.97), new THREE.MeshStandardMaterial({ roughness: 0.85 }), list.length);
  im.castShadow = im.receiveShadow = true; im.frustumCulled = false;
  const r = rng(4); list.forEach((q, i) => im.setColorAt(i, new THREE.Color().setHSL(0.09, 0.42, 0.66 + r() * 0.08)));
  const m4 = new THREE.Matrix4(), qt = new THREE.Quaternion(), e = new THREE.Euler(), one = V(1, 1, 1), p = V(), end = s0 + R / v + 0.6;
  st.obj(im, s => { im.visible = s >= s0 && s <= end; if (!im.visible) return; const t = s - s0, front = v * t;
    list.forEach((q, i) => { const x = (q.d - front) / 1.7, h = 1.1 * Math.exp(-x * x) * Math.max(0, 1 - t / 1.4); p.set(q.x, h - 0.2, q.z); e.set(-0.15 * h * Math.sign(q.z - ctr.z), 0, 0.15 * h * Math.sign(q.x - ctr.x)); qt.setFromEuler(e); m4.compose(p, qt, one); im.setMatrixAt(i, m4); });
    im.instanceMatrix.needsUpdate = true; });
}
/** tiles around the impact break loose and float up into the starry sky; dark holes where they were */
function brokenTiles(st, ctr, s0, s1, n = 46) {
  const im = new THREE.InstancedMesh(new THREE.BoxGeometry(TL * 0.97, 0.35, TL * 0.97), new THREE.MeshStandardMaterial({ roughness: 0.85 }), n); im.castShadow = true; im.frustumCulled = false;
  const holes = new THREE.InstancedMesh(new THREE.PlaneGeometry(TL * 0.97, TL * 0.97).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0x120a10 }), n); holes.frustumCulled = false;
  const r = rng(7), parts = [], m4 = new THREE.Matrix4(), q = new THREE.Quaternion(), one = V(1, 1, 1), p = V(), seen = new Set();
  for (let i = 0; i < n * 3 && parts.length < n; i++) {
    const a = r() * Math.PI * 2, d = 1 + r() * 9, t = tileOf(ctr.x + Math.cos(a) * d, ctr.z + Math.sin(a) * d), key = t.x + ',' + t.z; if (seen.has(key)) continue; seen.add(key);
    parts.push({ x: t.x, z: t.z, up: 2.5 + r() * 8, vx: (t.x - ctr.x) * 0.12, vz: (t.z - ctr.z) * 0.12, ax: V(r() - 0.5, r() - 0.5, r() - 0.5).normalize(), w: 0.8 + r() * 2 });
  }
  parts.forEach((P, i) => { im.setColorAt(i, new THREE.Color().setHSL(0.09, 0.42, 0.64 + r() * 0.1)); m4.makeTranslation(P.x, 0.02, P.z); holes.setMatrixAt(i, m4); });
  im.count = holes.count = parts.length;
  st.obj(im, s => { im.visible = s >= s0 && s <= s1; if (!im.visible) return; const t = s - s0;
    parts.forEach((P, i) => { p.set(P.x + P.vx * t, 0.2 + P.up * (1 - Math.exp(-1.3 * t)) + 0.25 * Math.sin(t * 2 + i), P.z + P.vz * t); q.setFromAxisAngle(P.ax, P.w * t); m4.compose(p, q, one); im.setMatrixAt(i, m4); });
    im.instanceMatrix.needsUpdate = true; });
  st.obj(holes, s => { holes.visible = s >= s0 && s <= s1; });
}
function plight(st, pos, s0, s1, color, power, dist = 20) {
  const L = new THREE.PointLight(color, 0, dist, 2); st.dyn.add(L);
  st.obj(null, s => { const k = fade(s, s0, s1, 0.04, 0.3); L.intensity = power * k * (1 + 0.7 * Math.exp(-Math.max(0, s - s0) / 0.14)); L.position.copy(pos(s)); });
}

const QPK = 'Q×h7+';
function queenEdit(st, view) {
  st.cute = true;
  buildTown(st);
  const b1 = ACT, sLeap = 4.1, sFire = 4.7, sImp = 5.65, sR = sImp - 0.35, sBack = sImp + 0.6;
  const reds = [4.6, 4.75, 4.9, 5.05, 5.2];
  st.knots = [[0, 0], [sR, sR], [sR + 0.35 / 0.3, sImp], [sR + 0.35 / 0.3 + 0.1, sImp + 0.004], [sR + 0.35 / 0.3 + 60.1, sImp + 60.004]];
  let path = null;
  const q = queenScene(st, { sImp, sWarn: 4.55, soft: true, giantPre: s => (s < sFire ? null : { ...path(s), ry: 0.3 }) });
  const Q = q.Q, q0 = Q.pred(sLeap), tgt = V(q.tx, 0, q.tz);
  path = s => { const u = prog(s, sLeap, sImp), b = V(q0.x, 0, q0.z).lerp(tgt, smooth(u * 0.85 + u * u * 0.15)); return { x: b.x, y: 4 * 30 * u * (1 - u), z: b.z, k: 1 + 7 * easeIn(u) }; };
  const ctr = s => { if (s < sLeap) return Q.pos(s); const p = path(Math.min(s, sImp)); return V(p.x, p.y + 0.72 * p.k, p.z); };
  Q.until(ACT).custom(sLeap - ACT, (u, t, a) => ({ ...a, run: 0, sq: 1 - 0.18 * Math.sin(Math.PI * prog(u, 0.55, 1)), pitch: -0.2 * smooth(prog(u, 0, 0.4)) }))
    .set({ hide: 1, track: 1 }).custom(sImp - sLeap, (u, t, a) => { const p = path(sLeap + t); return { ...a, x: p.x, z: p.z }; })
    .hold(3.6).set({ hide: 0, track: 0, x: q0.x, z: q0.z, y: 0, pitch: 0, sq: 1, run: 0 }).stand(0.4).run(20);
  const white = makePiece('queen', 'ally', null, { cute: true });
  st.obj(white, s => { white.visible = s >= sLeap && s < sFire; if (!white.visible) return; const p = path(s); white.position.set(p.x, p.y, p.z); white.scale.setScalar(p.k); white.rotation.set(-0.25, 0, 0); });
  st.trail({ pos: s => ctr(s) }, sLeap, sImp, 0x3fe0bf, { n: 12, dt: 0.022, size: 1.2, op: 0.45 });
  st.flash(s => ctr(s), sFire, 0.4, 14, 0xffb060);
  for (let s = sFire; s < sImp; s += 0.07) st.sparks(ctr(s), s, 8, { color: 0xff8a3a, speed: 4, life: 0.6, grav: -6 });
  // the move colours on the ground: teal under the caster, red under the ones it will hit, one per sixteenth
  const ct = tileOf(q0.x, q0.z);
  glowTiles(st, [[0, 0, 1.4], [1, 0], [-1, 0], [0, 1], [0, -1], [1, 1, 0.45], [1, -1, 0.45], [-1, 1, 0.45], [-1, -1, 0.45]].map(([a, b, w]) => ({ x: ct.x + a * TL, z: ct.z + b * TL, w: w ?? 0.75 })), b1, sLeap + 0.7, 0x1fb99a, { pulse: 14 });
  const victims = [...q.hit].sort((a, b) => a.pred(sImp).z - b.pred(sImp).z).slice(0, reds.length);
  const redTiles = [];
  victims.forEach((e, i) => { const p = e.pred(reds[i]), t = tileOf(p.x, p.z); redTiles.push({ ...t, d: reds[i] - reds[0], w: 1.3 }); for (const [a, b] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) redTiles.push({ x: t.x + a * TL, z: t.z + b * TL, d: reds[i] - reds[0], w: 0.55 }); });
  glowTiles(st, redTiles, reds[0], sImp + 0.15, 0xe5453a);
  // the marks: !! over the caster (seen through walls too), ?? over each one about to be hit, a huge !! where it lands
  emblem(st, s => ctr(Math.min(s, sImp)).clone().add(V(0, 2.3 + (s < sLeap ? 0 : path(Math.min(s, sImp)).k * 1.1), 0)), b1 + 0.02, sImp, '!!', '#1fb99a', 2.2, { always: true });
  emblem(st, () => V(q.tx, 15, q.tz), sImp + 0.02, sImp + 1.7, '!!', '#1fb99a', 10, { always: true });
  victims.forEach((e, i) => emblem(st, s => e.pos(s, 1.7), reds[i], sImp + 1.9, '??', '#e5453a', 1.25));
  // light: dusk at the press, a teal glow on the caster's square, red over the targets, white at the impact
  plight(st, () => V(ct.x, 1.2, ct.z), b1, sLeap + 0.5, 0x1fe0c0, 110);
  plight(st, () => V(q.tx, 2.5, q.tz), reds[0], sImp + 0.3, 0xff3030, 260, 22);
  plight(st, () => V(q.tx, 5, q.tz), sImp, sImp + 0.4, 0xffffff, 9000, 70);
  st.lightFn = s => { const k = fade(s, b1, sImp + 1.9, 0.3, 0.8); return { sun: 1 - 0.72 * k, hemi: 1 - 0.55 * k }; };
  st.sky.material.uniforms.darkCol.value.set(0x0b0726);
  st.skyFn = s => Math.max(0.55 * fade(s, b1, sImp + 2.4, 0.3, 0.8), fade(s, sLeap, sImp + 2.1, 0.3, 0.9));
  const fogA = new THREE.Color(0xd2deec), fogB = new THREE.Color(0x1a1230);
  st.obj(null, s => { st.scene.fog.color.lerpColors(fogA, fogB, Math.max(0.5 * fade(s, b1, sImp + 2.3, 0.3, 0.8), fade(s, sLeap, sImp + 2.1, 0.3, 0.9))); });
  space(st, V(q.tx, 0, q.tz), sLeap, sImp + 2.3);
  // the beat: every hit of the ultimate's track kicks the camera, flashes and splits the colour a little
  const beats = [b1, sLeap, ...reds, sImp];
  st.camMods.push({ c0: st.C(b1), d: 0.45, fov: -6, roll: 0.04 }, { c0: st.C(sLeap), d: 0.55, fov: -9, roll: -0.07 }, ...reds.map(b => ({ c0: st.C(b), d: 0.24, fov: -2.5 })), { c0: st.C(sImp), d: 0.9, fov: -11, roll: 0.09 });
  st.gradeFn = s => { const k = fade(s, b1, sImp + 2.2, 0.3, 0.8); let p = 0; for (const b of beats) if (s >= b) p += Math.exp(-(s - b) / 0.09);
    return { vig: 0.25 + 0.3 * k, vigCol: [0.06 * k, 0.02 * k, 0.14 * k], tint: [lerp(1, 0.95, k), lerp(1, 0.93, k), 1], bloom: 0.55 + 0.1 * k, rgb: 0.005 * Math.min(1, p), flashA: 0.07 * Math.min(1, p) }; };
  // the ground answers: a ripple and tiles that break loose and float
  ripple(st, tgt, sImp + 0.01);
  brokenTiles(st, tgt, sImp + 0.01, sImp + 4.5);
  spotlight(st, ctr, b1 + 0.02, sImp, 0xc8fff0);
  spirit(st, 'queen', V(q0.x + 5, 0, q0.z + 24), b1 + 0.1, sBack + 0.8, 0x5ff0d0);
  // what everyone hears and reads
  const enemyView = view === 'victim';
  st.banners.push({ c0: ACT + 0.05, c1: st.C(sBack + 1.4), text: enemyView ? '백팀 퀸의 퀸 희생!!' : '백팀 퀸 · 퀸 희생!!', sub: enemyView ? `${QPK} — 빨간 칸에서 벗어나!` : `${QPK} · 반경 20 m · 적 전원 30 m 뒤로`, glyph: '♛︎', color: enemyView ? 'rgba(170,28,32,0.92)' : 'rgba(18,140,120,0.92)', color2: enemyView ? 'rgba(110,10,14,0.92)' : 'rgba(10,80,90,0.92)' });
  st.voices.push({ c0: ACT + 0.06, c1: sLeap, text: '퀸…', size: 56 }, { c0: sLeap, c1: sLeap + 0.7, text: '희생!!', size: 84, color: '#3fe0bf' }, { c0: st.C(sImp) + 0.1, c1: st.C(sImp) + 1.1, text: '탁월한 수!!', size: 74, color: '#3fe0bf', y: 400 });
  st.music.push({ c0: ACT, c1: sLeap, text: '경기 음악 뚝 — 상승음', bg: 'rgba(30,30,36,0.85)' }, { c0: sLeap, c1: st.C(sBack + 2.2), text: '궁극기 비트 — 드롭!', bg: 'rgba(20,80,74,0.9)' });
  st.stamp(st.C(sLeap), 1.2, 'Q×h7+!!', { size: 104, color: '#3fe0bf', y: 252, rot: -6 });
  if (enemyView) for (const b of reds) st.screen(b, 0.1, [1, 0.25, 0.25], 0.18);
  return { q, Q, ctr, sLeap, sImp, sBack, q0, reds };
}
const HE = { opt: '', cut: ACT, cutin: false, nota: 'Q×h7+!!', dur: 14, tag: '쇼츠 연출 · 3인칭', chip: '주인공의 순간 · 퀸 희생', cornerHead: '퀸 희생', kind: 'queen' };
const SPEC = [
  ['칸', '시전자 발밑 칸이 청록 (!!)으로 터지고, 맞을 적의 칸이 박자마다 빨갛게 (??) — 실제 바닥에서, 모두의 화면에'],
  ['아이콘', '시전자 위 청록 !! (벽 너머로도 보임) · 맞을 적마다 빨간 ?? · 터지는 곳 하늘에 거대한 !!'],
  ['박자', 'Q "둠" → 드롭 "희생!!" → 빨간 칸 탁·탁·탁·탁·탁 → 슬로모 → 쾅 "탁월한 수!!" — 박자마다 카메라가 툭, 번쩍, 색 번짐'],
  ['우주와 판', '날아오르는 순간 하늘이 별빛 우주로 · 터지면 바닥이 물결치고 타일이 깨져 공중에 뜬다'],
  ['시점', '모두 자기 3인칭 그대로 — 컷 없음'],
];
const heroE1 = {
  ...HE, name: '퀸 희생 — 시전자 화면', cornerTail: '시전자 화면', view: '시전자 (백 퀸)', spec: SPEC,
  line: '쇼츠의 문법(판정 색 칸, !!·??, 박자, 우주, 깨지는 판)을 내 3인칭 화면 안에서 — 퀸을 따라 별빛 하늘로 올라갔다가 빨간 칸 위로 떨어진다',
  build(st) {
    const m = queenEdit(st, 'caster');
    st.shot(0, behind(s => m.ctr(Math.min(s, m.sImp)), { scale: s => (s < m.sLeap ? 1 : 1 + 0.32 * Math.min(7, Math.max(0, m.ctr(Math.min(s, m.sImp)).y) / 4)), look: 0.6, ahead: 7 }));
    st.shot(st.C(m.sImp + 0.02), (c, s) => { const t = s - m.sImp; return { p: V(m.q.tx, 10 + 1.5 * t, m.q.tz - 15 - t), l: V(m.q.tx, 2 + 0.8 * t, m.q.tz + 2), fov: 60 }; }, { blend: 0.35 });
    st.cap(2.7, ACT, '상대 6명이 몰려 20 m 앞 — 내 퀸, 팀 게이지 가득', '평소 경기 음악');
    st.cap(ACT + 0.05, m.sLeap, 'Q "둠" — 내 발밑 칸이 청록으로 터지고 머리 위에 !!, 세상이 어두워진다', '음악 뚝 → 상승음 → "퀸…"');
    st.cap(m.sLeap, st.C(m.reds[0]), '드롭 — "희생!!", 퀸이 날아오르고 하늘이 별빛 우주로', '궁극기 비트 드롭');
    st.cap(st.C(m.reds[0]), st.C(m.sImp), '탁·탁·탁 — 맞을 적의 칸이 박자마다 빨갛게, 머리 위에 ?? · 떨어지며 슬로모', '말 두는 소리 다섯 번 → 늘어지는 저음');
    st.cap(st.C(m.sImp), st.C(m.sImp + 2.2), '쾅 "탁월한 수!!" — 바닥이 물결치고 타일이 깨져 우주로 떠오른다', '쾅!! + 차임');
    finish(st, st.C(m.sImp + 2.2));
  },
};
const heroE2 = {
  ...HE, name: '퀸 희생 — 맞는 적 화면', cornerTail: '맞는 적 화면', view: '맞는 적 (흑 나이트)', spec: SPEC,
  line: '같은 순간을 맞는 쪽 3인칭으로 — 내 발밑 칸이 빨갛게 터지고 머리 위에 ??, 화면이 박자마다 붉게 번쩍인다',
  build(st) {
    const m = queenEdit(st, 'victim'), N = m.q.E.knight;
    st.shot(0, behind(s => N.pos(s), { up: 3.6, back: 7.5, look: 2.2, ahead: 6 }));
    st.cap(2.7, ACT, '흑 나이트 — 앞서 달리는 중', '평소 경기 음악');
    st.cap(ACT + 0.05, m.sLeap, '갑자기 세상이 어두워지고 빨간 배너 "백팀 퀸의 퀸 희생!!"', '음악 뚝 → "퀸…"');
    st.cap(m.sLeap, st.C(m.sImp), '하늘이 우주로 — 우리 발밑 칸이 박자마다 빨갛게, 머리 위에 ??, 화면이 붉게 번쩍', '"희생!!" + 비트 + 경고음 탁·탁·탁');
    st.cap(st.C(m.sImp), st.C(m.sImp + 2.4), '쾅 — 날아가는 내 말을 카메라가 그대로 따라간다, 깨진 타일이 하늘로', '쾅!! "탁월한 수!!" → 쿵 (착지)');
    finish(st, st.C(m.sImp + 2.4));
  },
};
const heroE3 = {
  ...HE, name: '퀸 희생 — 멀리 있는 팀원 화면', cornerTail: '멀리 있는 팀원 화면', view: '멀리 있는 팀원 (백 킹)', spec: SPEC,
  line: '30 m 뒤의 팀원도 자기 3인칭 그대로 — 저 앞의 청록 칸과 !!, 우주가 된 하늘, 빨간 칸 위로 떨어지는 불기둥을 같은 박자로 본다',
  build(st) {
    const m = queenEdit(st, 'far'), K = m.q.T.A.king;
    st.shot(0, behind(s => K.pos(s), { up: 3.4, back: 7, look: 3.5, ahead: 10, fov: 62 }));
    st.cap(2.7, ACT, '백 킹 — 우리 팀 맨 뒤에서 달리는 중', '평소 경기 음악');
    st.cap(ACT + 0.05, m.sLeap, '저 앞에서 우리 퀸 발밑 칸이 청록으로, 머리 위에 !! (벽 너머로도 보인다)', '음악 뚝 → "퀸…"');
    st.cap(m.sLeap, st.C(m.sImp), '하늘이 우주로 — 적 무리 발밑이 박자마다 빨갛게, 퀸이 그 위로 떨어진다', '"희생!!" + 궁극기 비트');
    st.cap(st.C(m.sImp), st.C(m.sImp + 2.2), '멀리서도 번쩍, 바닥이 출렁이고 타일이 떠오른다 — 하늘에 거대한 !!', '쾅!! "탁월한 수!!" → 함성');
    finish(st, st.C(m.sImp + 2.2));
  },
};

export const HERO_EDITS = { heroE1, heroE2, heroE3 };
