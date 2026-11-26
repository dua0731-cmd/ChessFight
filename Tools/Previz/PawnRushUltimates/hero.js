// Pawn Rush ultimates previz - "the moment this piece is the hero", in the game's own terms: every player keeps
// their third-person view from start to end; the moment is told by a voice everyone hears, the music switching to
// the caster's theme, the world's light (dimmed, a spotlight from the sky, the sky's colour), huge world effects
// and a banner. The same queen sacrifice is shown from three players' screens.
import { THREE, V, MAT, TEX, lerp, clamp, prog, smooth, easeOut, easeIn, back, fade, rng, makePiece, keepColour } from './stage.js';
import { ACT, finish } from './ults.js';
import { queenScene } from './casts.js';

function canvasTex(w, h, draw) { const c = document.createElement('canvas'); c.width = w; c.height = h; draw(c.getContext('2d'), w, h); const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t; }
let TT = null;
function townTex() {
  if (TT) return TT;
  const r = rng(21);
  const floor = canvasTex(512, 512, (g, w) => {
    const n = 8, s = w / n;
    for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) { const v = 0.9 + r() * 0.14; g.fillStyle = `rgb(${(228 * v) | 0},${(199 * v) | 0},${(154 * v) | 0})`; g.fillRect(i * s, j * s, s, s); }
    for (let k = 0; k < 3000; k++) { g.fillStyle = `rgba(120,90,50,${0.03 + r() * 0.05})`; g.fillRect(r() * w, r() * w, 2 + r() * 6, 1 + r() * 2); }
    g.strokeStyle = 'rgba(150,115,70,0.85)'; g.lineWidth = 3;
    for (let i = 0; i <= n; i++) { g.beginPath(); g.moveTo(i * s, 0); g.lineTo(i * s, w); g.stroke(); g.beginPath(); g.moveTo(0, i * s); g.lineTo(w, i * s); g.stroke(); }
  });
  floor.wrapS = floor.wrapT = THREE.RepeatWrapping; floor.anisotropy = 16;
  const wall = canvasTex(256, 256, (g, w) => {
    g.fillStyle = '#c9a877'; g.fillRect(0, 0, w, w);
    for (let row = 0; row < 8; row++) for (let col = -1; col < 5; col++) { const x = col * 64 + (row % 2) * 32, y = row * 32, v = 0.88 + r() * 0.16; g.fillStyle = `rgb(${(222 * v) | 0},${(190 * v) | 0},${(140 * v) | 0})`; g.fillRect(x + 2, y + 2, 60, 28); }
  });
  wall.wrapS = wall.wrapT = THREE.RepeatWrapping;
  const facade = canvasTex(256, 256, (g, w) => {
    g.fillStyle = '#e8d2ad'; g.fillRect(0, 0, w, w);
    for (const [x, y] of [[40, 50], [150, 50], [40, 150], [150, 150]]) { g.fillStyle = '#6a4a30'; g.fillRect(x - 4, y - 4, 64, 78); g.fillStyle = '#2f3446'; g.fillRect(x + 2, y + 2, 52, 66); g.fillStyle = '#6a4a30'; g.fillRect(x + 26, y, 4, 70); }
  });
  facade.wrapS = facade.wrapT = THREE.RepeatWrapping;
  return (TT = { floor, wall, facade });
}
/** A sandy castle-town street like the game's first section, in place of the floating board. */
function buildTown(st) {
  const tx = townTex(), g = new THREE.Group(), r = rng(33);
  const ft = tx.floor.clone(); ft.needsUpdate = true; ft.repeat.set(26 / 12, 330 / 12);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(26, 330), new THREE.MeshStandardMaterial({ map: ft, roughness: 0.85 })); floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, 120); floor.receiveShadow = true; g.add(floor);
  const wt = tx.wall.clone(); wt.needsUpdate = true; wt.repeat.set(90, 1);
  const wallMat = new THREE.MeshStandardMaterial({ map: wt, roughness: 0.9 }), capMat = new THREE.MeshStandardMaterial({ color: 0xd8bc8c, roughness: 0.8 });
  for (const sx of [-1, 1]) {
    const w = new THREE.Mesh(new THREE.BoxGeometry(0.9, 1.2, 330), wallMat); w.position.set(sx * 12.9, 0.6, 120); w.castShadow = w.receiveShadow = true; g.add(w);
    const cap = new THREE.Mesh(new THREE.BoxGeometry(1.25, 0.18, 330), capMat); cap.position.set(sx * 12.9, 1.29, 120); g.add(cap);
    for (let z = -40; z < 285; z += 4.5) { const p = new THREE.Mesh(new THREE.BoxGeometry(1.25, 1.75, 1.25), wallMat); p.position.set(sx * 12.9, 0.88, z); p.castShadow = true; g.add(p); }
  }
  const roofMats = [0xb5553a, 0x8a4a2e, 0x46607e].map(c => new THREE.MeshStandardMaterial({ color: c, roughness: 0.7 }));
  for (let z = -30; z < 250; z += 13) for (const sx of [-1, 1]) {
    const w = 8 + r() * 5, d = 8 + r() * 4, h = 7 + r() * 8;
    const ftx = tx.facade.clone(); ftx.needsUpdate = true; ftx.repeat.set(Math.max(1, Math.round(w / 4)), Math.max(1, Math.round(h / 4)));
    const m = new THREE.MeshStandardMaterial({ map: ftx, color: new THREE.Color().setHSL(0.09 + r() * 0.03, 0.3 + r() * 0.15, 0.78 + r() * 0.08), roughness: 0.9 });
    const b = new THREE.Mesh(new THREE.BoxGeometry(d, h, w), m); b.position.set(sx * (15 + d / 2), h / 2, z + r() * 4); b.castShadow = b.receiveShadow = true; g.add(b);
    const roof = new THREE.Mesh(new THREE.ConeGeometry(Math.max(w, d) * 0.72, 3 + r() * 2, 4), roofMats[(r() * 3) | 0]); roof.position.set(b.position.x, h + 1.6, b.position.z); roof.rotation.y = Math.PI / 4; roof.castShadow = true; g.add(roof);
  }
  for (let z = 0; z < 240; z += 16) for (const sx of [-1, 1]) {
    const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 4.4, 8), MAT.dark); pole.position.set(sx * 12.9, 3.4, z); g.add(pole);
    const flag = new THREE.Mesh(new THREE.PlaneGeometry(1.3, 2.3), new THREE.MeshStandardMaterial({ color: sx > 0 ? 0x2c4b9a : 0xa8282e, side: THREE.DoubleSide, roughness: 0.8 })); flag.position.set(sx * 12.2, 4.2, z); flag.rotation.y = Math.PI / 2; g.add(flag);
  }
  for (const [x, h, rr] of [[-15, 26, 4], [15, 26, 4], [0, 34, 5.5]]) {
    const t = new THREE.Mesh(new THREE.CylinderGeometry(rr, rr * 1.1, h, 20), wallMat); t.position.set(x, h / 2, 268); t.castShadow = true; g.add(t);
    const roof = new THREE.Mesh(new THREE.ConeGeometry(rr * 1.3, rr * 2, 20), roofMats[2]); roof.position.set(x, h + rr, 268); g.add(roof);
  }
  const cw = new THREE.Mesh(new THREE.BoxGeometry(44, 14, 3), wallMat); cw.position.set(0, 7, 264); g.add(cw);
  st.dyn.add(g);
  st.obj(null, () => { st.course.visible = false; });
}
/** A spotlight from the sky on the hero, with a visible beam everyone can see. */
function spotlight(st, target, s0, s1, color = 0xfff2cc) {
  const spot = new THREE.SpotLight(color, 0, 120, 0.32, 0.5, 2), tgt = new THREE.Object3D(); spot.target = tgt; st.dyn.add(spot, tgt);
  const beam = new THREE.Mesh(new THREE.CylinderGeometry(0.5, 4.6, 46, 32, 1, true), new THREE.MeshBasicMaterial({ color, map: TEX.vgrad, transparent: true, opacity: 0.2, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide, toneMapped: false }));
  st.obj(beam, s => { const k = fade(s, s0, s1, 0.25, 0.5); beam.visible = k > 0; spot.intensity = 2600 * k; if (!beam.visible) return; const p = target(s); tgt.position.copy(p); spot.position.set(p.x, p.y + 42, p.z - 3); beam.position.set(p.x, p.y + 23, p.z); beam.material.opacity = 0.13 * k; });
}
/** The piece's spirit: a huge see-through piece rising where everyone can see it. */
function spirit(st, kind, at, s0, s1, color) {
  const mat = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.16, depthWrite: false });
  const g = makePiece(kind, null, mat); g.traverse(m => { if (m.isMesh) m.castShadow = false; });
  st.obj(g, s => { const k = fade(s, s0, s1, 0.3, 0.6); g.visible = k > 0; if (!g.visible) return; g.position.set(at.x, -24 * (1 - easeOut(prog(s, s0, s0 + 0.6))), at.z); g.scale.setScalar(13); g.rotation.y = 0.25 * Math.sin(s * 1.3); mat.opacity = 0.17 * k; });
}
/** A third-person chase camera: behind and above, lagging a little, as the game's camera does. */
const behind = (src, o = {}) => (c, s) => {
  const p = V(), n = 6; for (let k = 0; k < n; k++) p.add(src(Math.max(0, s - k * 0.05))); p.multiplyScalar(1 / n);
  const sc = o.scale ? o.scale(s) : 1;
  return { p: p.clone().add(V(0, (o.up ?? 3.3) * sc, -(o.back ?? 7) * sc)), l: p.clone().add(V(0, (o.look ?? 1.0) * sc, (o.ahead ?? 6) * sc)), fov: o.fov ?? 60 };
};

const QPK = 'Q×h7+';
function queenMoment(st, view) {
  st.cute = true;
  buildTown(st);
  const sLeap = 4.15, sFire = 4.75, sImp = 5.65, sBack = sImp + 0.45;
  st.knots = [[0, 0], [sImp, sImp], [sImp + 0.45 / 0.35, sImp + 0.45], [sImp + 0.45 / 0.35 + 60, sImp + 60.45]];
  let path = null;
  const q = queenScene(st, { sImp, sWarn: 4.25, soft: true, giantPre: s => (s < sFire ? null : { ...path(s), ry: 0.3 }) });
  const Q = q.Q, q0 = Q.pred(sLeap), tgt = V(q.tx, 0, q.tz);
  path = s => { const u = prog(s, sLeap, sImp), b = V(q0.x, 0, q0.z).lerp(tgt, smooth(u * 0.85 + u * u * 0.15)); return { x: b.x, y: 4 * 30 * u * (1 - u), z: b.z, k: 1 + 7 * easeIn(u) }; };
  const ctr = s => { if (s < sLeap) return Q.pos(s); const p = path(Math.min(s, sImp)); return V(p.x, p.y + 0.72 * p.k, p.z); };
  Q.until(ACT).custom(sLeap - ACT, (u, t, a) => ({ ...a, run: 0, sq: 1 - 0.18 * Math.sin(Math.PI * prog(u, 0.55, 1)), pitch: -0.2 * smooth(prog(u, 0, 0.4)) }))
    .set({ hide: 1, track: 1 }).custom(sImp - sLeap, (u, t, a) => { const p = path(sLeap + t); return { ...a, x: p.x, z: p.z }; })
    .hold(3.6).set({ hide: 0, track: 0, x: q0.x, z: q0.z, y: 0, pitch: 0, sq: 1, run: 0 }).stand(0.4).run(20);
  const white = makePiece('queen', 'ally', null, { cute: true });
  st.obj(white, s => { white.visible = s >= sLeap && s < sFire; if (!white.visible) return; const p = path(s); white.position.set(p.x, p.y, p.z); white.scale.setScalar(p.k); white.rotation.set(-0.25, 0, 0); });
  st.trail({ pos: s => ctr(s) }, sLeap, sImp, 0xffd27a, { n: 14, dt: 0.025, size: 2.2 });
  st.flash(s => ctr(s), sFire, 0.4, 14, 0xffb060);
  for (let s = sFire; s < sImp; s += 0.07) st.sparks(ctr(s), s, 8, { color: 0xff8a3a, speed: 4, life: 0.6, grav: -6 });
  // the world tells everyone: light, sky, spotlight, the spirit, the banner, the voice, the music
  st.lightFn = s => { const k = fade(s, ACT, sBack, 0.3, 0.12); return { sun: 1 - 0.65 * k + 0.9 * fade(s, sBack - 0.05, sBack + 0.5, 0.05, 0.45), hemi: 1 - 0.45 * k }; };
  st.skyFn = s => 0.85 * fade(s, ACT, sBack + 0.3, 0.5, 0.6);
  const fogA = new THREE.Color(0xd2deec), fogB = new THREE.Color(0x40202a);
  st.obj(null, s => { st.scene.fog.color.lerpColors(fogA, fogB, fade(s, ACT, sBack + 0.3, 0.4, 0.6)); });
  st.gradeFn = s => { const k = fade(s, ACT, sBack + 0.2, 0.3, 0.4); return { vig: 0.2 + 0.35 * k, vigCol: [0.12 * k, 0.02 * k, 0], tint: [1, lerp(1, 0.9, k), lerp(1, 0.84, k)], bloom: 0.5 - 0.15 * k }; };
  spotlight(st, ctr, ACT + 0.02, sImp);
  spirit(st, 'queen', V(q0.x + 5, 0, q0.z + 24), ACT + 0.1, sBack + 0.6, 0x9fdcff);
  st.ring(q0.x, q0.z, ACT + 0.02, 0.8, 0.5, 9, 0xfff0c0, { w: 0.08, op: 0.45 });
  const enemyView = view === 'victim';
  st.banners.push({ c0: ACT + 0.05, c1: st.C(sBack + 1.4), text: enemyView ? '백팀 퀸의 퀸 희생!!' : '백팀 퀸 · 퀸 희생!!', sub: enemyView ? `${QPK} — 붉은 원 밖으로!` : `${QPK} · 반경 20 m · 적 전원 30 m 뒤로`, glyph: '♛︎', color: enemyView ? 'rgba(170,28,32,0.92)' : 'rgba(26,120,170,0.92)', color2: enemyView ? 'rgba(110,10,14,0.92)' : 'rgba(14,70,120,0.92)' });
  st.voices.push({ c0: ACT + 0.08, c1: sLeap, text: '퀸…', size: 58 }, { c0: sLeap, c1: sLeap + 0.75, text: '희생!!', size: 86, color: '#ffd27a' });
  st.music.push({ c0: ACT, c1: sLeap, text: '경기 음악 뚝', bg: 'rgba(30,30,36,0.85)' }, { c0: sLeap, c1: st.C(sBack + 2.2), text: '퀸의 테마 — 성가대 + 북', bg: 'rgba(40,26,70,0.88)' });
  for (const e of q.hit) st.label(s => e.pos(s, 1.1), sImp + 0.1, sImp + 1.6, '??', { badge: true, size: 24, rise: 8, bg: '#e5453a' });
  return { q, Q, ctr, sLeap, sImp, sBack, q0 };
}
const HB = { opt: '', cut: ACT, cutin: false, nota: 'Q×h7+!!', dur: 14, tag: '3인칭 그대로', chip: '주인공의 순간 · 퀸 희생', cornerHead: '퀸 희생', kind: 'queen' };
const SPEC = [
  ['시점', '모든 플레이어가 자기 3인칭 그대로 — 화면을 뺏지 않는다'],
  ['소리', '모두에게 들리는 목소리 "퀸… 희생!!" · 경기 음악이 뚝 끊기고 퀸의 테마 · 임팩트에 0.5초 정적'],
  ['조명', '온 세상이 어두워지고 하늘이 붉게 · 하늘에서 시전자에게 스포트라이트 · 터지면 번쩍 원래대로'],
  ['이펙트', '시전자 앞에 20 m 유령 퀸 (어디서든 보임) · 날아오르는 퀸이 커지며 불타는 거대 퀸으로 · 충격파 · 모두 0.5초 슬로모'],
  ['화면 표시', '모두에게 상단 배너 — 우리 팀은 파랑, 상대 팀은 빨강 + "붉은 원 밖으로!"'],
];
const heroCaster = {
  ...HB, name: '퀸 희생 — 시전자 화면', cornerTail: '시전자 화면', view: '시전자 (백 퀸)', spec: SPEC,
  line: '카메라는 처음부터 끝까지 내 3인칭. 퀸을 따라 하늘로 올라가 적 무리에 떨어진다 — 전체 음성 · 음악 · 조명이 지금 이 퀸이 주인공이라고 알린다',
  build(st) {
    const m = queenMoment(st, 'caster');
    st.shot(0, behind(s => m.ctr(Math.min(s, m.sImp)), { scale: s => (s < m.sLeap ? 1 : 1 + 0.32 * (m.ctr(Math.min(s, m.sImp)).y > 0 ? Math.min(7, (m.ctr(Math.min(s, m.sImp)).y) / 4) : 0)), look: 0.6, ahead: 7 }));
    st.shot(st.C(m.sImp + 0.02), (c, s) => { const t = s - m.sImp; return { p: V(m.q.tx, 10 + 1.5 * t, m.q.tz - 15 - t), l: V(m.q.tx, 1, m.q.tz + 2), fov: 60 }; }, { blend: 0.35 });
    st.cap(2.7, ACT, '상대 6명이 몰려 20 m 앞 — 내 퀸, 팀 게이지 가득', '평소 경기 음악');
    st.cap(ACT + 0.05, m.sLeap, 'Q — 세상이 어두워지고, 하늘에서 내게 스포트라이트, 앞에 거대한 유령 퀸', '음악 뚝 → "퀸…"');
    st.cap(m.sLeap, st.C(m.sImp), '퀸이 날아오른다 — 카메라는 내 뒤를 따라 하늘로, 떨어지며 불타는 거대 퀸이 된다', '"희생!!" + 퀸의 테마 (성가대 + 북)');
    st.cap(st.C(m.sImp), st.C(m.sImp + 2.0), '쾅 — 모두 0.5초 슬로모, 반경 20 m 적 전원 30 m 뒤로 · 퀸은 희생 (구덩이 위에서 관전)', '쾅!! → 0.5초 정적 → 번쩍, 빛이 돌아온다');
    finish(st, st.C(m.sImp + 2.0));
  },
};
const heroVictim = {
  ...HB, name: '퀸 희생 — 맞는 적 화면', cornerTail: '맞는 적 화면', view: '맞는 적 (흑 나이트)', spec: SPEC,
  line: '같은 순간을 맞는 쪽의 3인칭으로. 빨간 배너와 목소리, 붉어지는 하늘, 발밑의 붉은 원 — 위에서 불타는 거대 퀸이 떨어진다',
  build(st) {
    const m = queenMoment(st, 'victim'), N = m.q.E.knight;
    st.shot(0, behind(s => N.pos(s), { up: 3.6, back: 7.5, look: 2.2, ahead: 6 }));
    st.cap(2.7, ACT, '흑 나이트 — 앞서 달리는 중', '평소 경기 음악');
    st.cap(ACT + 0.05, m.sLeap, '갑자기 세상이 어두워지고 빨간 배너 "백팀 퀸의 퀸 희생!!"', '음악 뚝 → "퀸…"');
    st.cap(m.sLeap, st.C(m.sImp), '하늘이 붉게, 발밑에 붉은 원 — 위에서 무언가 떨어진다', '"희생!!" + 불타는 소리가 점점 커짐');
    st.cap(st.C(m.sImp), st.C(m.sImp + 2.2), '쾅 — 날아가는 내 말을 카메라가 그대로 따라간다, 30 m 뒤에 떨어져 ??', '쾅!! → 0.5초 정적 → 쿵 (착지)');
    finish(st, st.C(m.sImp + 2.2));
  },
};
const heroFar = {
  ...HB, name: '퀸 희생 — 멀리 있는 팀원 화면', cornerTail: '멀리 있는 팀원 화면', view: '멀리 있는 팀원 (백 킹)', spec: SPEC,
  line: '30 m 뒤의 팀원도 자기 3인칭 그대로 — 저 앞에 솟는 유령 퀸과 스포트라이트, 하늘에서 떨어지는 불기둥을 보고 같은 목소리와 음악을 듣는다',
  build(st) {
    const m = queenMoment(st, 'far'), K = m.q.T.A.king;
    st.shot(0, behind(s => K.pos(s), { up: 3.4, back: 7, look: 3.5, ahead: 10, fov: 62 }));
    st.cap(2.7, ACT, '백 킹 — 우리 팀 맨 뒤에서 달리는 중', '평소 경기 음악');
    st.cap(ACT + 0.05, m.sLeap, '저 앞에서 우리 퀸에게 스포트라이트, 거대한 유령 퀸이 솟는다', '음악 뚝 → "퀸…"');
    st.cap(m.sLeap, st.C(m.sImp), '퀸이 하늘로 — 저 멀리 적 무리 위로 불기둥이 떨어진다', '"희생!!" + 퀸의 테마');
    st.cap(st.C(m.sImp), st.C(m.sImp + 2.2), '멀리서도 번쩍, 충격파 — 이제 우리 팀이 앞으로', '쾅!! (멀리서 울리는 소리) → 함성');
    finish(st, st.C(m.sImp + 2.2));
  },
};

export const HEROES = { heroCaster, heroVictim, heroFar };

export { buildTown, spotlight, spirit, behind };
