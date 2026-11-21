// Pawn Rush skills previz - the presentation language taken from chess edit videos (the user's three shorts):
// move marks (📖 ! ★ !! on the mover, ?! ? ?? on the one hit), lit squares, analysis arrows, a move list,
// punch-ins and hit stops for the normal skills, and for an ultimate a two-second edit cut on the beat.
import { THREE, V, SQ, MAT, TEX, lerp, clamp, prog, smooth, easeOut, easeIn, back, fade, rng, cellX, snap, makePiece, makeCrown, basic, ADD, keepColour, GLYPH, NAME } from './stage.js';
import { BADGE } from './hud.js';
import { ACT, centroid, finish, PACK, teams, opening } from './ults.js';
import { queenScene } from './casts.js';

const TIER = { book: 0xe8913a, great: 0x4f86e6, best: 0x6fae2f, brilliant: 0x1fb99a, red: 0xe5453a };
const colOf = x => clamp(Math.round((x - cellX(0)) / SQ), 0, 7);
const sqOf = (x, z) => ({ x: cellX(colOf(x)), z: snap(z) });

/** clip -> scene knots: normal time with slow spans [scene start, scene length, speed] */
function knotsOf(slows) {
  const k = [[0, 0]]; let c = 0, s = 0;
  for (const [s0, d, f] of slows) { c += s0 - s; s = s0; k.push([c, s]); c += d / f; s += d; k.push([c, s]); }
  k.push([c + 60, s + 60]); return k;
}
/** squares lit (scene time, or clip time with o.clip); they keep their colour in a grey world */
function cells(st, list, t0, t1, color, o = {}) {
  const geo = new THREE.PlaneGeometry(SQ * 0.94, SQ * 0.94); geo.rotateX(-Math.PI / 2);
  const im = new THREE.InstancedMesh(geo, basic(0xffffff, o.op ?? 0.9, ADD), list.length); keepColour(im); im.frustumCulled = false;
  const m4 = new THREE.Matrix4(), base = new THREE.Color(color), k = new THREE.Color();
  list.forEach((q, i) => { m4.makeTranslation(q.x, o.y ?? 0.045, q.z); im.setMatrixAt(i, m4); im.setColorAt(i, k.setRGB(0, 0, 0)); });
  st.obj(im, (s, c) => {
    const t = o.clip ? c : s; im.visible = t >= t0 && t <= t1; if (!im.visible) return;
    list.forEach((q, i) => { let f = fade(t, t0 + (q.d ?? 0), t1, o.fi ?? 0.04, o.fo ?? 0.25); if (o.pulse) f *= 0.7 + 0.3 * Math.sin(t * o.pulse); im.setColorAt(i, k.copy(base).multiplyScalar(f)); });
    im.instanceColor.needsUpdate = true;
  });
  return im;
}
/** a flat analysis arrow on the board from a to b */
function arrow(st, a, b, t0, t1, color, o = {}) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.max(0.5, Math.hypot(dx, dz)), w = o.w ?? 0.55, hl = o.hl ?? 1.3, hw = o.hw ?? 1.6, L = Math.max(0.1, len - hl);
  const sh = new THREE.Shape(); sh.moveTo(-w / 2, 0); sh.lineTo(w / 2, 0); sh.lineTo(w / 2, L); sh.lineTo(hw / 2, L); sh.lineTo(0, L + hl); sh.lineTo(-hw / 2, L); sh.lineTo(-w / 2, L); sh.closePath();
  const geo = new THREE.ShapeGeometry(sh); geo.rotateX(-Math.PI / 2);
  const m = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ color, transparent: true, opacity: o.op ?? 0.8, depthWrite: false, side: THREE.DoubleSide, toneMapped: false })); keepColour(m);
  const g = new THREE.Group(); g.add(m); g.position.set(a.x, o.y ?? 0.09, a.z); g.rotation.y = Math.atan2(-dx, -dz);
  st.obj(g, (s, c) => { const t = o.clip ? c : s; g.visible = t >= t0 && t <= t1; if (!g.visible) return; g.scale.set(1, 1, Math.max(0.01, easeOut(prog(t, t0, t0 + (o.grow ?? 0.15))))); m.material.opacity = (o.op ?? 0.8) * fade(t, t0, t1, 0.02, 0.2); });
}
const mark = (st, pos, t0, t1, kind, size = 22, clip = false) => st.label(pos, t0, t1, BADGE[kind].t, { badge: true, size, rise: 8, bg: BADGE[kind].bg, clip });
function glow(st, pos, t0, t1, color, size, o = {}) {
  const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: o.tex ?? TEX.dot, color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false })); keepColour(sp);
  st.obj(sp, (s, c) => { const t = o.clip ? c : s; sp.visible = t >= t0 && t <= t1; if (!sp.visible) return; sp.position.copy(typeof pos === 'function' ? pos(s, c) : pos); const k = size * (1 + 0.15 * Math.sin(t * 18)); sp.scale.set(k, k, k); sp.material.opacity = (o.op ?? 0.8) * fade(t, t0, t1, 0.05, 0.2); });
}
/** flat chess-diagram pieces for the board view */
const TOK = {};
function tokenTex(kind, team) {
  const key = kind + team; if (TOK[key]) return TOK[key];
  const c = document.createElement('canvas'); c.width = c.height = 256; const g = c.getContext('2d');
  g.font = '210px "Segoe UI Symbol", "Malgun Gothic"'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.lineJoin = 'round';
  g.lineWidth = 16; g.strokeStyle = team === 'enemy' ? '#efefef' : '#161616'; g.strokeText(GLYPH[kind], 128, 142);
  g.fillStyle = team === 'enemy' ? '#24211f' : '#ffffff'; g.fillText(GLYPH[kind], 128, 142);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return (TOK[key] = t);
}
function tokens(st, actors, on) {
  for (const a of actors) {
    const m = new THREE.Mesh(new THREE.PlaneGeometry(1.95, 1.95), new THREE.MeshBasicMaterial({ map: tokenTex(a.kind, a.team), transparent: true, depthWrite: false, toneMapped: false }));
    m.rotation.set(-Math.PI / 2, 0, Math.PI); keepColour(m);
    st.obj(m, (s, c) => { m.visible = on(c) && !a.now.hide; if (!m.visible) return; m.position.set(a.now.x, 0.1, a.now.z); });
  }
}
/** board tiles broken loose and floating (clip time) */
function debris(st, ctr, t0, t1, n, o = {}) {
  const im = new THREE.InstancedMesh(new THREE.BoxGeometry(SQ * 0.96, 0.45, SQ * 0.96), new THREE.MeshStandardMaterial({ roughness: 0.55 }), n); im.frustumCulled = false; keepColour(im);
  const r = rng(o.seed ?? 5), parts = [], light = new THREE.Color(0xefdbb3), dark = new THREE.Color(0xa87a50);
  for (let i = 0; i < n; i++) { const a = r() * Math.PI * 2, d = 4 + r() * (o.r ?? 18); parts.push({ x: ctr.x + Math.cos(a) * d, y: ctr.y + (r() - 0.5) * (o.h ?? 22), z: ctr.z + Math.sin(a) * d, ax: V(r() - 0.5, r() - 0.5, r() - 0.5).normalize(), w: 0.3 + r(), vy: 0.6 + r() * 1.6 }); im.setColorAt(i, r() > 0.5 ? light : dark); }
  const m4 = new THREE.Matrix4(), q = new THREE.Quaternion(), one = V(1, 1, 1), p = V();
  st.obj(im, (s, c) => { const w = o.win(c); im.visible = !!w; if (!w) return; const t = w.t;
    parts.forEach((P, i) => { p.set(P.x, P.y + P.vy * t, P.z); q.setFromAxisAngle(P.ax, P.w * t + i); m4.compose(p, q, one); im.setMatrixAt(i, m4); }); im.instanceMatrix.needsUpdate = true; });
}
function stars(st, ctr, on, n = 600) {
  const r = rng(9), arr = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { const th = r() * Math.PI * 2, ph = Math.acos(2 * r() - 1), R = 260; arr.set([ctr.x + R * Math.sin(ph) * Math.cos(th), ctr.y + R * Math.cos(ph), ctr.z + R * Math.sin(ph) * Math.sin(th)], i * 3); }
  const geo = new THREE.BufferGeometry(); geo.setAttribute('position', new THREE.BufferAttribute(arr, 3));
  const pts = new THREE.Points(geo, new THREE.PointsMaterial({ color: 0xffffff, size: 2, sizeAttenuation: false, transparent: true, toneMapped: false, fog: false })); pts.frustumCulled = false; keepColour(pts);
  st.obj(pts, (s, c) => { pts.visible = on(c); });
}

// ================================================================ the normal skills (basic 📖, middle !, high ★)
const SK = (kind, name, line, spec) => ({ kind, opt: '', cut: 99, cutin: false, noStrip: true, noGauge: true, nota: '', dur: 13, chip: `${NAME[kind]} · 스킬 연출`, tag: '체스 에디트 문법', name, cornerHead: NAME[kind], cornerTail: '스킬 연출', line, spec });
const chase = (a, o = {}) => (c, s) => { const p = a.pos(s); return { p: p.clone().add(V(o.x ?? 2.8, o.y ?? 3.2, o.z ?? -8)), l: p.clone().add(V(0, 0.5, o.look ?? 9)), fov: o.fov ?? 52 }; };
function skillShots(st, a) {
  st.shot(0, (c, s) => { const p = a.pos(s); return { p: p.clone().add(V(9, 6.5, -12)), l: p.clone().add(V(-1, 0, 10)), fov: 48 }; });
  st.shot(2.5, chase(a), { blend: 0.5 });
}
/** the moment a middle skill lands: 0.1 s hold, a push in, a small flash */
function landed(st, s, pos, o = {}) {
  st.camMods.push({ c0: st.C(s), d: o.d ?? 0.5, fov: o.fov ?? -5, roll: o.roll ?? 0.05 });
  st.flash(pos, s, 0.25, o.flash ?? 4, 0xffffff); st.shake(s, 0.2, o.shake ?? 0.12);
}
const whipGrade = st => { const list = []; st.gradeFn = (s, c) => { for (const w of list) if (c >= w.c0 && c <= w.c0 + w.d) { const k = 1 - prog(c, w.c0, w.c0 + w.d); return { blur: [w.x * k, w.y * k], rgb: 0.003 * k }; } return {}; }; return list; };

const pawnSk = {
  ...SK('pawn', '첫 두 걸음 · 쫓아내기', '초급은 📖 주황 칸과 "탁, 탁", 중급은 ! 파랑 — 맞히면 0.1초 멈칫하며 화면이 살짝 밀고 들어간다', [
    ['초급 📖', '첫 두 걸음: 앞 두 칸이 "탁, 탁" 주황으로 켜지고 그 위를 질주 · 비스듬히 치면 e.p. 글씨와 잡는 소리 · 맞은 적 ?'],
    ['중급 !', '쫓아내기: 폰이 노리는 두 칸이 빨강 + 파란 화살표 · 맞히면 0.1초 멈칫, 살짝 줌인 · 폰 !, 쫓겨난 적 ?'],
    ['기보', '쓸 때마다 오른쪽 기보 목록에 한 줄 — 킬 로그 대신'],
    ['카메라', '초급은 그대로 · 중급은 맞힌 사람 화면만 0.1초'],
  ]),
  build(st) {
    const sB = 3.4, sE = 3.6, sM = 6.6, sH = sM + 0.22;
    st.knots = knotsOf([[sE, 0.02, 0.15], [sH, 0.02, 0.15]]);
    const w = whipGrade(st);
    const P = st.actor('pawn', 'ally', 1.0, 30, { speed: 6 }), EP = st.actor('pawn', 'enemy', -1.0, 35.3, { speed: 5.0 });
    st.actor('rook', 'enemy', -5, 62, { speed: 5.6 }).run(30); st.actor('knight', 'ally', -4.5, 25, { speed: 6 }).run(30);
    P.until(sB); const p0 = P.pred(sB);
    cells(st, [sqOf(p0.x, p0.z + 2), { ...sqOf(p0.x, p0.z + 4), d: 0.1 }], sB - 0.25, sB + 0.9, TIER.book);
    P.to(0.35, p0.x, p0.z + 4.2, { ease: easeOut });
    mark(st, s => P.pos(s, 1.0), sB, sB + 1.2, 'book', 18);
    const pe = EP.pred(sE);
    EP.until(sE).fly(0.5, pe.x - 2.2, pe.z + 0.6, { h: 1.0, spins: 0 }).lie(1.4).getUp(0.5).run(30);
    st.flash(V(pe.x, 0.8, pe.z), sE, 0.25, 4, 0xffffff); cells(st, [sqOf(pe.x, pe.z)], sE, sE + 0.5, 0xffffff, { op: 0.7 });
    st.label(V(pe.x, 2.4, pe.z), sE, sE + 1.0, 'e.p.', { size: 22, color: '#ffffff' });
    mark(st, s => EP.pos(s, 0.9), sE + 0.05, sE + 1.6, 'mistake', 20);
    st.feed.push({ c0: st.C(sE), text: '♙ 두 걸음 e.p. → ♟', mark: 'mistake' });
    P.until(sM); const pm = P.pred(sM);
    const EN = st.actor('knight', 'enemy', pm.x + 2, pm.z + 4.2 - 5.2 * sH, { speed: 5.2 });
    const en = EN.pred(sH);
    cells(st, [sqOf(pm.x - 2, pm.z + 2), sqOf(pm.x + 2, pm.z + 2)], sM - 0.5, sM + 0.4, TIER.red, { pulse: 18 });
    arrow(st, pm, en, sM - 0.45, sM + 0.3, TIER.great);
    P.to(sH - sM, en.x - 0.6, en.z - 0.9, { ease: easeIn }).run(30);
    EN.until(sH).fly(0.7, en.x + 4.5, en.z - 3, { h: 1.6, spins: 1 }).lie(1.6).getUp(0.5).run(30);
    landed(st, sH, V(en.x, 1, en.z)); w.push({ c0: st.C(sH) + 0.12, d: 0.2, x: 0.025, y: 0 });
    cells(st, [sqOf(en.x, en.z)], sH, sH + 0.6, TIER.red);
    mark(st, s => P.pos(s, 1.0), sH, sH + 1.4, 'great', 22); mark(st, s => EN.pos(s, 0.9), sH + 0.05, sH + 1.8, 'mistake', 22);
    st.feed.push({ c0: st.C(sH), text: '♙ 쫓아내기 → ♞', mark: 'mistake' });
    skillShots(st, P);
    st.cap(st.C(sB - 0.35), st.C(sB + 1.7), '초급 📖 첫 두 걸음 — 앞 두 칸이 "탁, 탁" 주황으로, 비스듬히 치면 e.p.', '나무 말 소리 탁·탁 + 잡는 소리 딱');
    st.cap(st.C(sM - 0.55), st.C(sH + 1.6), '중급 ! 쫓아내기 — 노리는 두 칸 빨강 + 파란 화살표, 맞히면 0.1초 멈칫', '탁 + 딱 + 짧은 저음 쿵');
    finish(st, st.C(sH + 1.8));
  },
};

const knightSk = {
  ...SK('knight', 'ㄱ자 도약 · 포크', '초급은 뛰는 순간 바닥에 ㄱ자 빛, 중급 포크는 착지하는 순간 두 적에게 빨간 선 — 체스 에디트의 포크 장면 그대로', [
    ['초급 📖', 'ㄱ자 도약: 뛰는 순간 바닥에 ㄱ자 주황 빛, 내릴 칸이 먼저 켜짐 · 착지 "탁" + 발굽 자국 빛'],
    ['중급 !', '포크: 착지하는 순간 나이트에서 두 적에게 빨간 화살표 · 두 칸 빨강, 나이트 칸 파랑 ! · 맞은 둘에게 ?'],
    ['카메라', '포크가 맞으면 내 화면만 0.1초 멈칫 + 줌인'],
  ]),
  build(st) {
    const sB = 3.3, sM = 6.4, sL = sM + 0.55;
    st.knots = knotsOf([[sL, 0.02, 0.15]]);
    const w = whipGrade(st);
    const K = st.actor('knight', 'ally', 1.0, 30, { speed: 6 });
    st.actor('pawn', 'enemy', 1.0, 35, { speed: 5 }).run(30);
    st.actor('queen', 'ally', -4.5, 24, { speed: 6 }).run(30);
    K.until(sB); const k0 = K.pred(sB), l1 = { x: k0.x + 2, z: k0.z + 4.4 };
    cells(st, [sqOf(k0.x, k0.z + 2), { ...sqOf(k0.x, k0.z + 4), d: 0.05 }, { ...sqOf(l1.x, l1.z), d: 0.1 }], sB - 0.2, sB + 0.8, TIER.book, { op: 0.75 });
    cells(st, [sqOf(l1.x, l1.z)], sB - 0.3, sB + 0.95, TIER.book, { op: 1, pulse: 20 });
    K.hop(0.55, l1.x, l1.z, { h: 2.6 }).custom(0.2, (u, t, a) => ({ ...a, sq: u < 0.4 ? lerp(0.8, 1, u / 0.4) : 1, y: 0 }));
    st.sparks(V(l1.x, 0.2, l1.z), sB + 0.55, 18, { color: 0xffd28a, speed: 3, life: 0.6, up: 0.2 });
    mark(st, s => K.pos(s, 1.1), sB, sB + 1.2, 'book', 18);
    st.feed.push({ c0: st.C(sB + 0.55), text: '♘ ㄱ자 도약', mark: 'book' });
    K.until(sM); const km = K.pred(sM), l2 = { x: km.x - 2, z: km.z + 4.4 };
    const EB = st.actor('bishop', 'enemy', l2.x - 2.4, l2.z + 0.3 - 5.3 * sL, { speed: 5.3 }), ER = st.actor('rook', 'enemy', l2.x + 2.4, l2.z - 0.2 - 5.3 * sL, { speed: 5.3 });
    const eb = EB.pred(sL), er = ER.pred(sL);
    cells(st, [sqOf(km.x, km.z + 2), { ...sqOf(km.x, km.z + 4), d: 0.05 }, { ...sqOf(l2.x, l2.z), d: 0.1 }], sM - 0.2, sM + 0.6, TIER.great, { op: 0.6 });
    K.hop(sL - sM, l2.x, l2.z, { h: 2.6 }).custom(0.3, (u, t, a) => ({ ...a, sq: u < 0.4 ? lerp(0.8, 1, u / 0.4) : 1, y: 0 })).run(30);
    arrow(st, l2, eb, sL, sL + 0.9, TIER.red, { w: 0.45 }); arrow(st, l2, er, sL, sL + 0.9, TIER.red, { w: 0.45 });
    cells(st, [sqOf(eb.x, eb.z), sqOf(er.x, er.z)], sL, sL + 0.8, TIER.red); cells(st, [sqOf(l2.x, l2.z)], sL, sL + 0.9, TIER.great);
    EB.until(sL + 0.03).fly(0.7, eb.x - 4, eb.z + 1, { h: 1.8, spins: 1 }).lie(1.5).getUp(0.5).run(30);
    ER.until(sL + 0.03).fly(0.7, er.x + 4, er.z + 1, { h: 1.8, spins: 1 }).lie(1.5).getUp(0.5).run(30);
    landed(st, sL, V(l2.x, 1, l2.z), { fov: -6 }); w.push({ c0: st.C(sL) + 0.12, d: 0.18, x: 0, y: 0.02 });
    mark(st, s => K.pos(s, 1.1), sL, sL + 1.5, 'great', 22); mark(st, s => EB.pos(s, 0.9), sL + 0.05, sL + 1.7, 'mistake', 22); mark(st, s => ER.pos(s, 0.9), sL + 0.05, sL + 1.7, 'mistake', 22);
    st.feed.push({ c0: st.C(sL), text: '♘ 포크 → ♝ ♜', mark: 'mistake' });
    skillShots(st, K);
    st.cap(st.C(sB - 0.35), st.C(sB + 1.5), '초급 📖 ㄱ자 도약 — 뛰는 순간 ㄱ자 빛, 내릴 칸이 먼저 켜진다', '말발굽 "탁" + 발굽 자국 반짝');
    st.cap(st.C(sM - 0.3), st.C(sL + 1.6), '중급 ! 포크 — 착지하는 순간 두 적에게 빨간 선, 둘 다 ?', '탁 + 딱딱 (둘을 한 번에)');
    finish(st, st.C(sL + 1.8));
  },
};

const bishopSk = {
  ...SK('bishop', '옆구리 사격 · 긴 대각선 저격', '초급은 쏘는 대각선이 잠깐 주황으로, 상급 ★은 모으는 동안 칸이 초록으로 길어지고 쏘는 순간 화면이 기울며 느려진다', [
    ['초급 📖', '옆구리 사격: 쏘는 대각선 칸이 0.3초 주황 · 빛 탄이 그 칸을 따라감 · 맞은 적 ?! (둔화, 파란 기운)'],
    ['상급 ★', '긴 대각선 저격: 모으는 동안 칸이 한 칸씩 초록으로 + 초록 화살표 · 쏘면 내 화면이 기울며 0.3초 느리게 · 맞은 적 ??'],
    ['조준', '겨누는 스킬은 조준선을 체스 분석 화살표로'],
  ]),
  build(st) {
    const sB = 3.4, sC = 6.0, sF = 7.0;
    st.knots = knotsOf([[sF, 0.35, 0.3]]);
    const B = st.actor('bishop', 'ally', -1.0, 30, { speed: 6 });
    st.actor('rook', 'ally', 4.5, 23, { speed: 6 }).run(30);
    B.until(sB); const b0 = B.pred(sB), c0 = colOf(b0.x), r0 = Math.round((snap(b0.z) - 1) / SQ);
    const diag = []; for (let k = 1; k <= 4 && c0 + k <= 7; k++) diag.push({ x: cellX(c0 + k), z: (r0 + k) * SQ + 1, d: k * 0.05 });
    const tg = diag[diag.length - 1], sHit = sB + 0.25;
    const E1 = st.actor('knight', 'enemy', tg.x, tg.z - 5.5 * sHit, { speed: 5.5 });
    cells(st, diag, sB - 0.05, sB + 0.5, TIER.book, { op: 0.85 });
    glow(st, s => V(lerp(b0.x, tg.x, prog(s, sB, sHit)), 1.0, lerp(b0.z, tg.z, prog(s, sB, sHit))), sB, sHit, 0xffc080, 1.4);
    E1.until(sHit).custom(2.0, (u, t, a) => ({ ...a, run: 1, z: a.z + 3.0 * t, gait: a.gait + 3.0 * t })).run(30);
    glow(st, s => E1.pos(s), sHit, sHit + 2.0, 0x7fb8ff, 1.8, { op: 0.45 });
    st.flash(V(tg.x, 1, tg.z), sHit, 0.25, 3.5, 0xffe0b0);
    mark(st, s => B.pos(s, 1.1), sB, sB + 1.2, 'book', 18); mark(st, s => E1.pos(s, 0.9), sHit, sHit + 1.6, 'inacc', 20);
    st.feed.push({ c0: st.C(sHit), text: '♗ 옆구리 사격 → ♞', mark: 'inacc' });
    B.until(sC); const bc = B.pred(sC);
    B.custom(sF - sC, (u, t, a) => ({ ...a, run: 1, z: a.z + 2 * t, gait: a.gait + 2 * t })).run(30);
    const bf = { x: bc.x, z: bc.z + 2 }, et = { x: -4.6, z: bf.z + 26 };
    const E2 = st.actor('queen', 'enemy', et.x, et.z - 6 * sF, { speed: 6 });
    const line = []; const seen = new Set();
    for (let k = 1; k <= 13; k++) { const q = sqOf(lerp(bf.x, et.x, k / 13), lerp(bf.z, et.z, k / 13)); const key = q.x + ',' + q.z; if (!seen.has(key)) { seen.add(key); line.push({ ...q, d: line.length * 0.07 }); } }
    cells(st, line, sC, sF + 0.5, TIER.best, { op: 0.75 });
    arrow(st, bf, et, sC, sF + 0.3, TIER.best, { grow: sF - sC, w: 0.4 });
    st.camMods.push({ c0: st.C(sC), d: st.C(sF + 0.4) - st.C(sC), fov: -4, roll: 0.07 });
    st.beam(s => B.pos(s, 0.3), V(et.x, 1, et.z), sF, sF + 0.3, 0.16, 0xd8ffb0, { grow: 0.08 });
    E2.until(sF + 0.08).fly(1.0, et.x - 1, et.z - 6, { h: 2.2, spins: 1 }).lie(2.0).getUp(0.5).run(30);
    st.flash(V(et.x, 1.2, et.z), sF + 0.08, 0.35, 6, 0xffffff); st.screen(sF + 0.02, 0.15, [1, 1, 1], 0.35);
    mark(st, s => B.pos(s, 1.1), sF, sF + 1.6, 'best', 24); mark(st, s => E2.pos(s, 0.9), sF + 0.1, sF + 1.9, 'blunder', 24);
    st.feed.push({ c0: st.C(sF + 0.1), text: '♗ 긴 대각선 → ♛', mark: 'blunder' });
    skillShots(st, B);
    st.cap(st.C(sB - 0.3), st.C(sB + 1.6), '초급 📖 옆구리 사격 — 쏘는 대각선이 잠깐 주황, 맞은 적은 ?! 둔화', '피융 + 맞을 때 "틱"');
    st.cap(st.C(sC - 0.1), st.C(sF + 1.0), '상급 ★ 긴 대각선 저격 — 모으는 동안 칸이 초록으로 길어지고, 쏘면 화면이 기울며 느리게', '모으는 소리 웅— → 쏘는 순간 "쿵" + 맞으면 ??');
    finish(st, st.C(sF + 1.2));
  },
};

const rookSk = {
  ...SK('rook', '사슬 · 성벽 돌진', '중급 사슬은 그 줄이 파랗게 켜지고 걸리면 끌려온다, 상급 ★ 성벽 돌진은 앞줄이 초록, 성벽 서는 칸에 "탁" 도장', [
    ['중급 !', '사슬: 겨누면 그 줄이 파랗게 + 파란 화살표 · 걸린 적 빨간 칸 + ? · 끌려올 때 모션 블러'],
    ['상급 ★', '성벽 돌진: 앞줄이 초록 · 돌진은 속도선 · 성벽 서는 칸에 "탁" 도장 · 튕긴 적 ?!'],
  ]),
  build(st) {
    const sA = 3.2, sF = 3.5, sCa = sF + 0.18, sD = 6.6;
    st.knots = knotsOf([[sCa, 0.02, 0.15]]);
    const w = whipGrade(st);
    const R = st.actor('rook', 'ally', 1.0, 30, { speed: 6 });
    st.actor('pawn', 'ally', -4.5, 22, { speed: 6 }).run(30);
    R.until(sA); const r0 = R.pred(sA);
    const EC = st.actor('knight', 'enemy', r0.x, r0.z + 9 - 5.6 * sA, { speed: 5.6 });
    const fileC = []; for (let k = 1; k <= 4; k++) fileC.push({ ...sqOf(r0.x, r0.z + 2 * k), d: k * 0.04 });
    cells(st, fileC, sA, sCa + 0.5, TIER.great, { op: 0.65 });
    const ec = EC.pred(sCa);
    arrow(st, r0, ec, sA, sF + 0.15, TIER.great);
    R.stand(sCa + 0.65 - sA);
    const rr = R.pred(sCa + 0.65);
    EC.until(sCa).drag(0.6, r0.x + 0.2, rr.z + 1.8).lie(0.7).getUp(0.5).run(30);
    st.beam(s => R.pos(s, 0.3), s => (s < sCa ? V(ec.x, 0.9, lerp(r0.z, ec.z, prog(s, sF, sCa))) : EC.pos(s)), sF, sCa + 0.65, 0.07, 0xa8c4ff);
    landed(st, sCa, V(ec.x, 1, ec.z)); w.push({ c0: st.C(sCa) + 0.12, d: 0.3, x: 0, y: -0.03 });
    cells(st, [sqOf(ec.x, ec.z)], sCa, sCa + 0.7, TIER.red);
    mark(st, s => R.pos(s, 1.1), sCa, sCa + 1.5, 'great', 22); mark(st, s => EC.pos(s, 0.9), sCa + 0.05, sCa + 1.8, 'mistake', 22);
    st.feed.push({ c0: st.C(sCa), text: '♖ 사슬 → ♞', mark: 'mistake' });
    R.run(sD - R.t); const rd = R.pred(sD);
    const EW = st.actor('pawn', 'enemy', rd.x + 1.3, rd.z + 5 - 5 * (sD + 0.25), { speed: 5 });
    const ahead = []; for (let k = 1; k <= 5; k++) ahead.push({ ...sqOf(rd.x, rd.z + 2 * k), d: k * 0.03 });
    cells(st, ahead, sD - 0.25, sD + 0.6, TIER.best, { op: 0.7 });
    R.to(0.45, rd.x, rd.z + 9, { ease: easeOut }).run(30);
    const ew = EW.pred(sD + 0.25);
    EW.until(sD + 0.25).fly(0.6, ew.x + 3.5, ew.z + 1.5, { h: 1.2, spins: 0 }).lie(1.2).getUp(0.5).run(30);
    w.push({ c0: st.C(sD), d: 0.45, x: 0, y: 0.03 });
    const wall = new THREE.Group(); const brick = new THREE.Mesh(new THREE.BoxGeometry(6, 2.4, 0.9), MAT.wall); brick.position.y = 1.2; brick.castShadow = true; wall.add(brick);
    for (let i = 0; i < 3; i++) { const m = new THREE.Mesh(new THREE.BoxGeometry(1.1, 0.6, 0.9), MAT.wallTop); m.position.set(-2 + i * 2, 2.7, 0); wall.add(m); }
    wall.position.set(rd.x, 0, rd.z + 0.6);
    st.obj(wall, s => { wall.visible = s >= sD + 0.05 && s < sD + 3.5; if (!wall.visible) return; wall.position.y = -2.6 * (1 - back(prog(s, sD + 0.05, sD + 0.3))); });
    cells(st, [sqOf(rd.x - 2, rd.z), sqOf(rd.x, rd.z), sqOf(rd.x + 2, rd.z)], sD + 0.1, sD + 1.2, TIER.best);
    st.label(V(rd.x, 3.6, rd.z + 0.6), sD + 0.12, sD + 1.0, '탁', { size: 26, color: '#ffffff' }); st.shake(sD + 0.12, 0.2, 0.15);
    mark(st, s => R.pos(s, 1.1), sD + 0.4, sD + 1.9, 'best', 24); mark(st, s => EW.pos(s, 0.9), sD + 0.3, sD + 1.7, 'inacc', 20);
    st.feed.push({ c0: st.C(sD + 0.4), text: '♖ 성벽 돌진', mark: 'best' });
    skillShots(st, R);
    st.shot(st.C(sD + 0.5), (c, s) => { const p = R.pos(s); return { p: p.clone().add(V(5, 4.2, 7)), l: V(rd.x, 1.2, rd.z), fov: 56 }; }, { blend: 0.4 });
    st.cap(st.C(sA - 0.2), st.C(sCa + 1.5), '중급 ! 사슬 — 그 줄이 파랗게, 걸린 적은 빨간 칸 + ?, 끌려온다', '쇠사슬 "촤르륵" + 걸릴 때 "철컹"');
    st.cap(st.C(sD - 0.35), st.C(sD + 1.9), '상급 ★ 성벽 돌진 — 앞줄 초록, 지나간 자리에 성벽 "탁"', '돌진 "쿠웅" + 성벽 도장 "탁"');
    finish(st, st.C(sD + 2.1));
  },
};

const queenSk = {
  ...SK('queen', '여왕의 활주 · 연속 체크', '초급 활주는 출발·도착 칸이 마지막 수처럼 남고, 중급 연속 체크는 이을수록 커진다: + → + + → #', [
    ['초급 📖', '여왕의 활주: 출발 칸과 도착 칸이 마지막 수 표시처럼 남고, 그 사이를 빛 줄이 잇는다'],
    ['중급 !', '연속 체크: 맞힐 때마다 적 칸 빨강 + "+" + 체크 알림음 · 2번째는 화면 기울기 · 3번째는 "#" 큰 번쩍임 + 퀸 ★'],
    ['소리', '체크 알림음이 이을수록 한 음씩 높아짐'],
  ]),
  build(st) {
    const sB = 3.2, s1 = 5.6, s2 = 6.5, s3 = 7.4;
    st.knots = knotsOf([[s1, 0.02, 0.15], [s2, 0.02, 0.15], [s3, 0.04, 0.12]]);
    const Q = st.actor('queen', 'ally', 0.0, 30, { speed: 6 });
    st.actor('king', 'ally', -5, 22, { speed: 5.9 }).run(30);
    Q.until(sB); const q0 = Q.pred(sB), g1 = { x: q0.x + 5, z: q0.z + 8 };
    cells(st, [sqOf(q0.x, q0.z)], sB - 0.05, sB + 1.6, TIER.book, { op: 0.55 }); cells(st, [sqOf(g1.x, g1.z)], sB + 0.35, sB + 1.8, TIER.book, { op: 0.8 });
    st.beam(V(q0.x, 0.5, q0.z), V(g1.x, 0.5, g1.z), sB, sB + 0.6, 0.12, TIER.book, { grow: 0.35 });
    Q.to(0.4, g1.x, g1.z, { ease: easeOut });
    mark(st, s => Q.pos(s, 1.1), sB + 0.1, sB + 1.3, 'book', 18);
    st.feed.push({ c0: st.C(sB + 0.4), text: '♕ 활주', mark: 'book' });
    Q.until(s1 - 0.25); const qa = Q.pred(s1 - 0.25);
    const hits = [{ x: qa.x - 2.5, z: qa.z + 3.6 }]; hits.push({ x: hits[0].x - 3, z: hits[0].z + 5.2 }); hits.push({ x: hits[1].x + 5, z: hits[1].z + 5.2 });
    const times = [s1, s2, s3], kinds = ['pawn', 'knight', 'bishop'], marks = ['check', 'check', 'mate'], words = ['체크 +', '체크 + +', '체크메이트 #'];
    hits.forEach((h, i) => {
      const s = times[i], E = st.actor(kinds[i], 'enemy', h.x, h.z - 5.4 * s, { speed: 5.4 });
      if (i === 0) Q.to(0.25, h.x + 0.3, h.z - 0.9, { ease: easeIn }); else Q.to(s - times[i - 1], h.x + 0.3, h.z - 0.9, { ease: smooth });
      const big = i === 2;
      E.until(s + 0.02).fly(big ? 0.9 : 0.6, h.x + (i % 2 ? -3 : 3), h.z + (big ? 5 : 1.5), { h: big ? 3 : 1.2, spins: big ? 2 : 0 }).lie(big ? 2 : 1.2).getUp(0.5).run(30);
      cells(st, [sqOf(h.x, h.z)], s, s + 0.9, TIER.red, { pulse: big ? 24 : 0 });
      st.camMods.push({ c0: st.C(s), d: big ? 0.7 : 0.45, fov: [-3, -5, -9][i], roll: [0, 0.06, -0.09][i] });
      st.flash(V(h.x, 1, h.z), s, 0.3, [3, 4.5, 8][i], 0xffffff); st.shake(s, 0.25, [0.08, 0.15, 0.3][i]);
      if (big) st.screen(s, 0.2, [1, 1, 1], 0.5);
      mark(st, s2_ => E.pos(s2_, 0.9), s + 0.03, s + 1.5, marks[i], big ? 26 : 22);
      st.stamp(st.C(s), 0.9, words[i], { size: [60, 72, 92][i], y: 150, color: big ? '#ff5a4a' : '#ffffff' });
      st.feed.push({ c0: st.C(s), text: `♕ ${words[i]}`, mark: big ? 'best' : 'check' });
    });
    Q.run(30);
    mark(st, s => Q.pos(s, 1.1), s3, s3 + 1.6, 'best', 26);
    skillShots(st, Q);
    st.cap(st.C(sB - 0.3), st.C(sB + 1.6), '초급 📖 여왕의 활주 — 출발 칸과 도착 칸이 마지막 수처럼 남는다', '슈욱 + 멈출 때 "탁"');
    st.cap(st.C(s1 - 0.4), st.C(s3 + 1.4), '중급 ! 연속 체크 — 이을수록 커진다: + → + + → # (마지막은 퀸 ★)', '체크 알림음 띵 · 띵↑ · 띵↑↑ + 쾅');
    finish(st, st.C(s3 + 1.6));
  },
};

const kingSk = {
  ...SK('king', '왕의 한 걸음 · 왕의 부름', '초급은 킹 둘레 8칸이 희미하게 켜지고 고른 칸만 진하게, 중급 왕의 부름은 팀원 칸이 파랗게 켜지며 화살표가 킹에게 모인다', [
    ['초급 📖', '왕의 한 걸음: 킹 둘레 8칸이 희미하게 (킹이 갈 수 있는 칸) · 고른 칸만 진하게 · "탁" · 비켜 간 적 ?!'],
    ['중급 !', '왕의 부름: 팀원 발밑 칸이 파랗게 + 킹에게 파란 화살표 · 끌려와 둘러섬 · 킹 머리 위 왕관'],
  ]),
  build(st) {
    const sB = 3.4, sM = 6.4;
    const K = st.actor('king', 'ally', 0.0, 30, { speed: 5.9 });
    K.until(sB); const k0 = K.pred(sB);
    const ring8 = [[1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1], [0, -1], [1, -1]].map(([a, b]) => sqOf(k0.x + a * SQ, k0.z + b * SQ));
    cells(st, ring8, sB - 0.15, sB + 0.5, TIER.book, { op: 0.35 });
    const dz = sqOf(k0.x - SQ, k0.z); cells(st, [dz], sB - 0.1, sB + 0.9, TIER.book, { op: 1 });
    K.to(0.25, dz.x, k0.z + 1.2, { ease: easeOut });
    const ER = st.actor('rook', 'enemy', k0.x + 0.3, k0.z - 3 - 8 * (sB - 0.1), { speed: 8 });
    ER.until(sB - 0.1).to(0.5, k0.x + 0.3, k0.z + 5, { ease: easeOut }).custom(0.6, (u, t, a) => ({ ...a, run: 0, pitch: 0.3 * Math.sin(u * Math.PI), roll: 0.2 * Math.sin(t * 9) })).run(30);
    mark(st, s => K.pos(s, 1.2), sB, sB + 1.3, 'book', 18); mark(st, s => ER.pos(s, 0.9), sB + 0.4, sB + 1.6, 'inacc', 20);
    st.label(s => ER.pos(s, 1.6), sB + 0.4, sB + 1.3, '헛스윙', { size: 18, color: '#ffd0a0' });
    st.feed.push({ c0: st.C(sB + 0.3), text: '♔ 한 걸음 (피함)', mark: 'book' });
    K.until(sM); const km = K.pred(sM);
    const mates = [['pawn', -4.5, 9], ['knight', 4.0, 12], ['bishop', -2.0, 15]].map(([k, x, back_]) => st.actor(k, 'ally', x, km.z - back_ - 5.5 * sM, { speed: 5.5 }));
    const kk = K.pred(sM + 0.8);
    const spots = [[-2, -1], [2, -1], [0, -2.4]];
    mates.forEach((a, i) => {
      const p = a.pred(sM);
      cells(st, [sqOf(p.x, p.z)], sM, sM + 0.8, TIER.great);
      arrow(st, p, { x: km.x, z: km.z - 1 }, sM + 0.05 + i * 0.06, sM + 0.8, TIER.great, { w: 0.4 });
      a.until(sM + 0.3).to(0.5, kk.x + spots[i][0], kk.z + spots[i][1], { ease: smooth, face: true }).run(30);
    });
    K.run(30);
    const crown = makeCrown(0.4); keepColour(crown);
    st.obj(crown, s => { crown.visible = s >= sM && s < sM + 2.2; if (!crown.visible) return; const p = K.pos(s, 1.7); crown.position.copy(p); crown.rotation.y = s * 2; crown.scale.setScalar(back(prog(s, sM, sM + 0.25))); });
    glow(st, s => K.pos(s, 1.9), sM, sM + 2.0, 0xffd77a, 2.2, { op: 0.5 });
    mark(st, s => K.pos(s, 1.2), sM + 0.1, sM + 1.8, 'great', 22);
    st.feed.push({ c0: st.C(sM + 0.3), text: '♔ 왕의 부름 → ♙ ♘ ♗', mark: 'great' });
    skillShots(st, K);
    st.shot(st.C(sM - 0.2), (c, s) => { const p = K.pos(s); return { p: p.clone().add(V(4, 6, -12)), l: p.clone().add(V(0, 0, 3)), fov: 54 }; }, { blend: 0.4 });
    st.cap(st.C(sB - 0.35), st.C(sB + 1.6), '초급 📖 왕의 한 걸음 — 둘레 8칸이 희미하게, 고른 칸만 진하게', '"탁" + 적이 헛치는 바람 소리');
    st.cap(st.C(sM - 0.2), st.C(sM + 2.0), '중급 ! 왕의 부름 — 팀원 칸이 파랗게, 화살표가 킹에게 모인다', '나팔 짧게 + 발소리 다다닥');
    finish(st, st.C(sM + 2.2));
  },
};

// ================================================================ the ultimate: a two-second edit cut on the beat, then once more slowly
/** One pass of the cut from clip time c0 at speed sp. o: caster, nota, targets [{a, p}], arrows [[from, to, color]], hero(st, win), heroCam(c, u) */
function editCut(st, o, c0, sp, beatsWords) {
  const B = k => c0 + [0, 0.2, 0.67, 1.14, 1.61][k] / sp, b = [0, 1, 2, 3, 4].map(B);
  const cp = o.caster.pred(ACT), cs = sqOf(cp.x, cp.z), tc = o.center;
  const win = (k0, k1) => c => (c >= b[k0] && c < b[k1] ? { t: (c - b[k0]) * sp, u: (c - b[k0]) / (b[k1] - b[k0]) } : null);
  // 0: hold, dim, colour drains
  st.shot(b[0], (c) => { const u = prog(c, b[0], b[1]); return { p: V(cp.x + lerp(3, 1.8, u), lerp(2.2, 1.6, u), cp.z - lerp(6, 3.6, u)), l: V(cp.x, 1.2, cp.z + 2), fov: 50 }; });
  // 1: the board seen as a diagram, the caster's square bursts with !!
  st.shot(b[1], (c) => ({ p: V(cs.x, 30, cs.z - 4), l: V(cs.x, 0, cs.z), fov: 36, roll: 0.26 }));
  st.camMods.push({ c0: b[1], d: 0.4 / sp, fov: -6 });
  cells(st, [cs], b[1], b[3], 0x1fb99a, { clip: true, op: 1, pulse: 22 });
  cells(st, [[1, 0], [-1, 0], [0, 1], [0, -1]].map(([x, z]) => ({ x: cs.x + x * SQ, z: cs.z + z * SQ })), b[1] + 0.04 / sp, b[2], 0x1fb99a, { clip: true, op: 0.35 });
  mark(st, V(cs.x, 0.3, cs.z), b[1] + 0.03 / sp, b[3], 'brilliant', 34, true);
  st.typing.push({ c0: b[1] + 0.02 / sp, d: b[4] - b[1], text: o.nota, color: '#3fe0bf', size: 72, y: 548, per: 0.045 / sp });
  // 2: whip to the targets, their squares go red on the sixteenths
  st.shot(b[2], (c) => { const u = smooth(prog(c, b[2], b[2] + 0.14 / sp)); return { p: V(lerp(cs.x, tc.x, u), 32, lerp(cs.z, tc.z, u) - 5), l: V(lerp(cs.x, tc.x, u), 0, lerp(cs.z, tc.z, u)), fov: 40, roll: lerp(0.26, -0.18, u) }; });
  o.targets.forEach((t, i) => {
    const d = (0.08 + i * 0.06) / sp, sqT = sqOf(t.p.x, t.p.z);
    cells(st, [sqT], b[2] + d, b[3] + 0.02, 0xe5453a, { clip: true, op: 1 });
    mark(st, V(t.p.x, 0.4, t.p.z), b[2] + d, b[3], 'blunder', 26, true);
  });
  for (const [f, t, col] of o.arrows) arrow(st, f, t, b[2] + 0.04 / sp, b[3], col, { clip: true, grow: 0.12 / sp, w: 0.5 });
  // 3: the hero shot in space
  o.hero(st, win(3, 4), b[3], b[4]);
  st.shot(b[3], (c) => o.heroCam(c, prog(c, b[3], b[4])));
  debris(st, V(tc.x, 14, tc.z), b[3], b[4], 70, { win: win(3, 4), r: 20 });
  // words for the slow pass
  if (beatsWords) [['0박 — 모두 멈추고 색이 빠진다', '음악 뚝 + 상승음 "쉬익"'], ['1박 — 체스판 시점, 시전자 칸이 청록으로 터지며 !! · 기보가 타이핑', '베이스 드롭 "둠"'],
    ['2박 — 휙! 표적 칸이 박자마다 빨갛게, 적마다 ??', '"탁·탁·탁" (말 두는 소리)'], ['3박 — 판이 깨진 우주, 히어로 숏 (슬로모)', '늘어지는 저음']]
    .forEach(([t, m], k) => st.cap(b[k], b[k + 1], t, m));
  return b;
}
function editGrade(st, passes) {
  st.pieceHide = (s, c) => passes.some(b => c >= b[1] && c < b[4]);
  st.gradeFn = (s, c) => {
    for (const b of passes) {
      if (c >= b[0] && c < b[1]) { const u = prog(c, b[0], b[1]); return { sat: lerp(1, 0.35, u), tint: [lerp(1, 0.8, u), lerp(1, 0.8, u), lerp(1, 0.85, u)], vig: 0.2 + 0.35 * u }; }
      if (c >= b[1] && c < b[3]) { const w = c >= b[2] && c < b[2] + (b[3] - b[2]) * 0.28 ? 1 - prog(c, b[2], b[2] + (b[3] - b[2]) * 0.28) : 0, red = c >= b[2] ? fade(c, b[2], b[3], 0.05, 0.25) : 0;
        return { sat: 0, contrast: 1.3, maskOn: 1, vig: 0.4 + 0.3 * red, vigCol: [0.5 * red, 0, 0], bloom: 0.85, blur: [0.07 * w, 0.025 * w], rgb: 0.004 * w }; }
      if (c >= b[3] && c < b[4]) return { sat: 1.1, bloom: 1.25, vig: 0.35, tint: [0.95, 0.9, 1.12] };
      if (c >= b[4] && c < b[4] + 0.25) { const k = 1 - prog(c, b[4], b[4] + 0.25); return { flashA: 0.9 * k, rgb: 0.008 * k }; }
    }
    return {};
  };
  st.skyFn = (s, c) => (passes.some(b => c >= b[3] && c < b[4]) ? 1 : 0);
  st.sky.material.uniforms.darkCol.value.set(0x05030f);
  st.obj(null, (s, c) => { st.course.visible = !passes.some(b => c >= b[3] && c < b[4]); });
}
const UB = { opt: '', cut: ACT, cutin: false, dur: 16, tag: '체스 에디트', cornerHead: '에디트 컷' };

const queenCut = {
  ...UB, kind: 'queen', nota: 'Q×h7+!!', chip: '궁극기 · 에디트 컷', name: '퀸 희생 — 에디트 컷', cornerTail: '퀸 희생',
  line: 'Q를 누르면 2초 동안 그 수의 체스 에디트: 멈춤 → 체스판 시점 !! → 휙, 표적 ?? → 우주 히어로 숏 → 번쩍, 경기로. 뒤에 같은 컷을 느리게 한 번 더',
  spec: [
    ['박자', '4박 (한 박 0.47초) — 0박 멈춤 · 1박 !! · 2박 ?? · 3박 히어로 숏 · 4박 번쩍'],
    ['보이는 것', '체스판 시점 (회색 판, 말 아이콘, 색 칸) · 청록 !! · 빨강 ?? · 깨진 판 조각과 우주 · 기보 타이핑'],
    ['소리', '음악 뚝 → 베이스 드롭 → 말 두는 소리 셋 → 늘어지는 저음 → 쾅 + 차임'],
    ['멈춤', '모든 화면 2초 (호스트 물리도) · 한 판에서 두 번째부터는 1초 판'],
  ],
  build(st) {
    const sImp = 3.77, cut0 = ACT, cEnd = 5.21, cAfter = 8.0, sAfter = 3.62 + (cAfter - 5.36) - 0.3 * 0 ;
    const q = queenScene(st, { sImp, sWarn: 3.62, noDodge: true, giantPre: s => (s < 3.62 ? null : { x: q.tx, y: lerp(18, 0, easeIn(prog(s, 3.62, sImp))), z: q.tz }) });
    const Q = q.Q, q0 = Q.pred(ACT);
    Q.until(ACT).set({ hide: 1, track: 1 }).hold(sImp + 3.5 - ACT).set({ hide: 0, track: 0, x: q0.x, z: q0.z, y: 0, run: 0 }).stand(0.4).run(20);
    const cR = cAfter + 0.4, sp = 0.35, cRend = cR + 1.61 / sp;
    const sEnd = sImp + 0.3 + (cAfter - 5.36 - 1.2);
    st.knots = [[0, 0], [ACT, ACT], [cEnd, 3.62], [5.36, sImp], [6.56, sImp + 0.3], [cAfter, sEnd], [cAfter + 0.001, ACT], [cRend, ACT + 0.0001], [cRend + 0.001, sEnd], [cRend + 60, sEnd + 60]];
    void sAfter;
    const targets = q.T.enemies.map(a => ({ a, p: a.pred(ACT) }));
    const opt = {
      caster: Q, nota: 'Q×h7+!!', center: { x: q.tx, z: q.tz }, targets, arrows: [[{ x: q0.x, z: q0.z }, { x: q.tx, z: q.tz }, 0x1fb99a]],
      hero: (st2, win, t0, t1) => {
        const g = makePiece('queen', null, MAT.fire); keepColour(g);
        st2.obj(g, (s, c) => { const w = win(c); g.visible = !!w; if (!w) return; g.position.set(q.tx, lerp(60, 26, easeOut(w.u)), q.tz); g.scale.setScalar(8); g.rotation.y = w.t * 2; });
        glow(st2, (s, c) => { const w = win(c); return V(q.tx, (w ? lerp(60, 26, easeOut(w.u)) : 0) + 7, q.tz); }, t0, t1, 0xff7a2a, 30, { clip: true, op: 0.6 });
      },
      heroCam: (c, u) => ({ p: V(q.tx + 12 - 4 * u, 3, q.tz - 30), l: V(q.tx, 30 + 4 * u, q.tz), fov: 62, roll: -0.1 + 0.05 * u }),
    };
    const p1 = editCut(st, opt, cut0, 1, false), p2 = editCut(st, opt, cR, sp, true);
    editGrade(st, [p1, p2]);
    tokens(st, [...q.T.allies, ...q.T.enemies], c => (c >= p1[1] && c < p1[3]) || (c >= p2[1] && c < p2[3]));
    stars(st, V(q.tx, 20, q.tz), c => (c >= p1[3] && c < p1[4]) || (c >= p2[3] && c < p2[4]));
    st.feed.push({ c0: p1[4], text: '♕ 퀸 희생 Q×h7+', mark: 'brilliant' });
    for (const e of q.hit) mark(st, s => e.pos(s, 1.1), sImp + 0.1, sImp + 1.6, 'blunder', 24);
    opening(st, Q, '상대 6명이 몰려서 20 m 앞 — Q를 누르면 2초 에디트 컷');
    st.shot(p1[4], () => ({ p: V(q.tx + 15, 2.2, q.tz - 15), l: V(q.tx, 5, q.tz), fov: 62 }));
    st.shot(6.6, () => ({ p: V(8, 9, q.tz + 16), l: V(0, 1, q.tz - 16), fov: 56 }), { blend: 0.5 });
    st.shot(cRend, () => ({ p: V(8, 9, q.tz + 16), l: V(0, 1, q.tz - 16), fov: 56 }));
    st.cap(ACT, p1[4], '에디트 컷 2초 — 그대로의 속도', '둠 · 탁탁탁 · 쾅');
    st.cap(p1[4], cAfter, '4박 — 번쩍! 경기로 돌아오며 실제로 터진다 · 맞은 적마다 ??', '쾅 + 탁월한 수 차임 "띠링"');
    st.stamp(cAfter, 0.5, '느리게 한 번 더', { size: 64, y: 330, rot: 0, color: '#ffffff' });
    finish(st, cRend + 0.05);
  },
};

const forkCut = {
  ...UB, kind: 'knight', nota: 'Nf7+!!', chip: '궁극기 · 에디트 컷', name: '패밀리 포크 — 에디트 컷', cornerTail: '패밀리 포크',
  line: '같은 2초 틀에 나이트를: 체스판 시점에서 포크 선이 상대 여섯에게 뻗고, 깨진 판 조각 사이로 나이트가 날아간다 (첫 영상의 "갤럭틱 나이트")',
  spec: [
    ['1박', '나이트 칸이 청록으로 !! · 기보 "Nf7+!!" 타이핑'],
    ['2박', '휙! 내릴 칸에서 적 여섯에게 빨간 포크 선 · 적마다 ??'],
    ['3박', '우주에 뜬 판 조각 사이로 나이트가 빛 꼬리를 끌며 날아감'],
    ['4박', '번쩍 → 경기: 하늘에서 나이트가 떨어지고 8칸 빛기둥, 반경 14 m 전원 하늘로'],
  ],
  build(st) {
    const cut0 = ACT, cEnd = 5.21, sL = 3.82, cAfter = 8.0;
    const T = teams(st, 'knight', { enemy: PACK }), K = T.me;
    const C0 = centroid(T.enemies, ACT), lx = snap(C0.x), lz = snap(C0.z);
    const k0 = K.pred(ACT);
    K.until(ACT).set({ hide: 1, track: 1 }).hold(3.62 - ACT).set({ hide: 0, track: 0, x: lx, z: lz, y: 14 }).custom(sL - 3.62, (u, t, a) => ({ ...a, y: 14 * (1 - easeIn(u)), run: 0 }))
      .custom(0.4, (u, t, a) => ({ ...a, y: 0, sq: u < 0.3 ? lerp(0.7, 1, u / 0.3) : 1 })).run(20);
    const sEnd = sL + 0.3 + (cAfter - 5.42 - 1.2);
    const cR = cAfter + 0.4, sp = 0.35, cRend = cR + 1.61 / sp;
    st.knots = [[0, 0], [ACT, ACT], [cEnd, 3.62], [5.42, sL], [6.62, sL + 0.3], [cAfter, sEnd], [cAfter + 0.001, ACT], [cRend, ACT + 0.0001], [cRend + 0.001, sEnd], [cRend + 60, sEnd + 60]];
    for (const e of T.enemies) {
      const p = e.pred(sL), d = Math.hypot(p.x - lx, p.z - lz), r = rng(Math.round(p.z * 10));
      if (d > 14) { e.run(20); continue; }
      e.until(sL + 0.04).fly(2.2, clamp(p.x + (p.x >= lx ? 1 : -1) * (3 + r() * 3), -7, 7), p.z - (18 + r() * 6), { h: 11 + r() * 3, spins: 2, roll: 1.2 }).lie(2.0).getUp(0.5).run(20);
      mark(st, s => e.pos(s, 1.0), sL + 0.1, sL + 1.6, 'blunder', 24);
    }
    for (const a of T.allies) if (a !== K) a.run(20);
    st.flash(V(lx, 1.5, lz), sL, 0.35, 11); st.shake(sL, 0.9, 0.6); st.ring(lx, lz, sL, 0.9, 1, 16, 0x7fe9ff, { w: 0.1, op: 0.8 });
    [[1, 2], [2, 1], [2, -1], [1, -2], [-1, -2], [-2, -1], [-2, 1], [-1, 2]].forEach(([a, b], i) => st.pillar(lx + a * 4, lz + b * 4, sL + 0.03 + i * 0.02, 1.2, 0.55, 18, 0x7fe9ff, { rise: 0.12, op: 0.42 }));
    st.dust(lx, lz, sL, { n: 14, r: 9, size: 4, life: 1.5 });
    const targets = T.enemies.map(a => ({ a, p: a.pred(ACT) }));
    const opt = {
      caster: K, nota: 'Nf7+!!', center: { x: lx, z: lz }, targets,
      arrows: [[{ x: k0.x, z: k0.z }, { x: lx, z: lz }, 0x1fb99a], ...targets.map(t => [{ x: lx, z: lz }, t.p, 0xe5453a])],
      hero: (st2, win, t0, t1) => {
        const g = makePiece('knight', 'ally'); keepColour(g);
        const path = u => V(lx - 22 + 44 * u, 16 + 5 * Math.sin(u * Math.PI), lz - 4 + 8 * u);
        st2.obj(g, (s, c) => { const w = win(c); g.visible = !!w; if (!w) return; g.position.copy(path(w.u)); g.scale.setScalar(3.2); g.rotation.set(-0.3, Math.PI / 2 - 0.3, 0.2 * Math.sin(w.t * 6)); });
        glow(st2, (s, c) => { const w = win(c); return w ? path(w.u).add(V(0, 2, 0)) : V(); }, t0, t1, 0x7fe9ff, 9, { clip: true, op: 0.55 });
        for (let i = 1; i <= 10; i++) glow(st2, (s, c) => { const w = win(c); return w ? path(Math.max(0, w.u - i * 0.03)).add(V(0, 2, 0)) : V(); }, t0, t1, 0x7fe9ff, 4 * (1 - i / 11), { clip: true, op: 0.5 * (1 - i / 11) });
      },
      heroCam: (c, u) => ({ p: V(lx - 10 + 20 * u, 13, lz - 16), l: V(lx - 14 + 30 * u, 16, lz + 2), fov: 58, roll: 0.12 - 0.2 * u }),
    };
    const p1 = editCut(st, opt, cut0, 1, false), p2 = editCut(st, opt, cR, sp, true);
    editGrade(st, [p1, p2]);
    tokens(st, [...T.allies, ...T.enemies], c => (c >= p1[1] && c < p1[3]) || (c >= p2[1] && c < p2[3]));
    stars(st, V(lx, 16, lz), c => (c >= p1[3] && c < p1[4]) || (c >= p2[3] && c < p2[4]));
    st.feed.push({ c0: p1[4], text: '♘ 패밀리 포크 Nf7+', mark: 'brilliant' });
    opening(st, K, '상대 6명이 한데 몰려 20 m 앞 — Q를 누르면 2초 에디트 컷');
    st.shot(p1[4], () => ({ p: V(lx + 9, 1.5, lz - 12), l: V(lx, 7, lz), fov: 60 }));
    st.shot(6.7, () => ({ p: V(9, 9, lz + 13), l: V(0, 1, lz - 15), fov: 56 }), { blend: 0.5 });
    st.shot(cRend, () => ({ p: V(9, 9, lz + 13), l: V(0, 1, lz - 15), fov: 56 }));
    st.cap(ACT, p1[4], '에디트 컷 2초 — 그대로의 속도', '둠 · 탁탁탁 · 쾅');
    st.cap(p1[4], cAfter, '4박 — 번쩍! 하늘에서 나이트가 떨어져 반경 14 m 전원을 띄운다', '쾅 + 탁월한 수 차임 "띠링"');
    st.stamp(cAfter, 0.5, '느리게 한 번 더', { size: 64, y: 330, rot: 0, color: '#ffffff' });
    finish(st, cRend + 0.05);
  },
};

export const EDITS = { pawnSk, knightSk, bishopSk, rookSk, queenSk, kingSk, queenCut, forkCut };
