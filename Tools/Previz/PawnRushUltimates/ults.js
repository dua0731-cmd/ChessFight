// Pawn Rush ultimates previz - twelve options, two for each piece, each scripted shot by shot.
// Scene time s runs the world; clip time c runs the screen (the cut-in holds the world for a second).
import { THREE, V, HALF, SQ, COL, MAT, TEX, lerp, clamp, prog, smooth, easeOut, easeIn, back, fade, rng, cellX, snap, makePiece, basic, ADD } from './stage.js';

const ACT = 3.6, CUT = 1.0;   // the Q press (scene = clip time) and the cut-in's length on screen
/** clip -> scene knots: normal until the press, the cut-in (1 s of screen = 0.1 s of world), then [clip s, scene s] pairs */
function warp(...pairs) {
  const k = [[0, 0], [ACT, ACT], [ACT + CUT, ACT + 0.1]];
  for (const [dc, ds] of pairs) { const l = k[k.length - 1]; k.push([l[0] + dc, l[1] + ds]); }
  const l = k[k.length - 1]; k.push([l[0] + 60, l[1] + 60]); return k;
}
/** slow motion at speed f for d scene seconds from scene time s0 */
const slow = (s0, d, f) => warp([s0 - (ACT + 0.1), s0 - (ACT + 0.1)], [d / f, d]);

const KINDS = ['pawn', 'knight', 'bishop', 'rook', 'queen', 'king'];
const SPD = { pawn: 6.0, knight: 6.08, bishop: 5.96, rook: 6.04, queen: 6.0, king: 5.92 };
const ALLY0 = [[0, 50], [-3.2, 46], [3.4, 45], [-5.6, 43], [5.4, 42], [-0.8, 40]];   // the caster takes the first spot
const ENEMY0 = { queen: [0.6, 70], knight: [-4.2, 67], rook: [3.8, 65], bishop: [-1.6, 62.5], pawn: [5.2, 60], king: [-5.4, 58] };
const PACK = { queen: [0.5, 67], knight: [-2.6, 65.6], rook: [2.8, 64.6], bishop: [-0.6, 63.2], pawn: [3.6, 62], king: [-3.4, 61] };
function teams(st, caster, o = {}) {
  const A = {}, E = {};
  [caster, ...KINDS.filter(k => k !== caster)].forEach((k, i) => { const [x, z] = o.ally?.[k] ?? ALLY0[i]; A[k] = st.actor(k, 'ally', x, z, { speed: SPD[k] }); });
  for (const k of KINDS) { const [x, z] = o.enemy?.[k] ?? ENEMY0[k]; E[k] = st.actor(k, 'enemy', x, z, { speed: SPD[k] + 0.03 }); }
  return { A, E, me: A[caster], allies: KINDS.map(k => A[k]), enemies: KINDS.map(k => E[k]) };
}
const mid = s => 58 + 6 * s;
function opening(st, me, line = '상대가 20 m 앞 · 우리 팀 게이지가 가득 찼다') {
  st.shot(0, (c, s) => ({ p: V(15, 8.5, mid(s) - 17), l: V(-1, 0.5, mid(s) + 5), fov: 46 }));
  st.shot(2.7, (c, s) => { const p = me.pos(s); return { p: p.clone().add(V(2.8, 2.4, -7.2)), l: p.clone().add(V(0, 0.4, 9)), fov: 52 }; }, { blend: 0.45 });
  st.shot(ACT, (c, s) => { const p = me.pos(s), k = smooth(prog(c, ACT, ACT + CUT)); return { p: p.clone().add(V(lerp(2.8, 1.8, k), lerp(2.4, 1.5, k), lerp(-7.2, -4.4, k))), l: p.clone().add(V(0, 0.5, 7)), fov: 50 }; });
  st.cap(2.75, ACT, line, '게이지 꽉 참 — 반짝 알림음');
}
const follow = (fn, off, look, fov = 50) => (c, s) => { const p = fn(s); return { p: p.clone().add(off), l: p.clone().add(look), fov }; };
const fixed = (p, l, fov = 50) => () => ({ p, l, fov });
function firstHit(f, s0, s1, dt = 0.005) { for (let s = s0; s <= s1; s += dt) if (f(s)) return s; return null; }
function centroid(list, s) { let x = 0, z = 0; for (const a of list) { const p = a.pred(s); x += p.x; z += p.z; } return { x: x / list.length, z: z / list.length }; }
function finish(st, cSpec) { st.specAt = cSpec; st.dur = cSpec + 3.0; }

/** Off the board: flies out over the side, falls, and comes back `back` m behind. */
function knockOff(st, a, s, side, o = {}) {
  const p = a.pred(s), d = o.d ?? 1.3, xOut = side * (HALF + 4 + (o.far ?? 2));
  a.until(s).fly(d, xOut, p.z + (o.dz ?? 4), { h: o.h ?? 5, spins: o.spins ?? 2, y1: -16 }).set({ hide: 1 });
  st.ko = (st.ko ?? 0) + 1;
  const bk = o.back ?? 20, sr = s + d + (o.wait ?? 1.4), x = side * (2 + (st.ko % 3) * 1.8), z = p.z - bk;
  a.hold(sr - a.t).set({ hide: 0, x, z, y: 0, pitch: 0, roll: 0, yaw: 0, run: 0, sq: 1 }).stand(0.5).run(30);
  st.label(V(side * (HALF + 1.5), 0.3, p.z + 4), s + d * 0.55, s + d + 0.9, '장외!', { color: '#ff7b6b' });
  st.label(V(x, 1.5, z), sr, sr + 1.5, `${bk} m 뒤에서 다시`, { size: 20, color: '#ffd7a0' });
  st.pillar(x, z, sr - 0.15, 0.9, 0.7, 6, COL.enemy, { op: 0.55 });
}
function dizzy(st, a, s0, s1) {
  for (let i = 0; i < 3; i++) st.aura(s => { const p = a.pos(s, 0.75), th = s * 6 + i * 2.1; return V(p.x + Math.cos(th) * 0.5, p.y, p.z + Math.sin(th) * 0.5); }, s0, s1, COL.gold, 0.4, { op: 1, pulse: 0 });
}
function xMark(st, x, z, s0, s1, color = COL.red) {
  const g = new THREE.Group(), m = basic(color, 0.9, ADD);
  for (const r of [Math.PI / 4, -Math.PI / 4]) { const b = new THREE.Mesh(new THREE.PlaneGeometry(1.5, 0.2), m); b.rotation.set(-Math.PI / 2, 0, r); g.add(b); }
  g.position.set(x, 0.06, z);
  st.obj(g, s => { g.visible = s >= s0 && s <= s1; m.opacity = 0.9 * fade(s, s0, s1, 0.12, 0.3); });
}
function bolt(st, x, z, s0, d = 0.4) {
  const r = rng((s0 * 1000) | 0), pts = [];
  for (let i = 0; i <= 7; i++) { const e = i === 0 || i === 7 ? 0 : 1; pts.push(V(x + e * (r() - 0.5) * 2.4, lerp(42, 0.9, i / 7), z + e * (r() - 0.5) * 1.6)); }
  for (let i = 0; i < 7; i++) st.beam(pts[i], pts[i + 1], s0, s0 + d, 0.22, 0xff4040, { fi: 0.01, fo: 0.25 });
  st.flash(V(x, 1, z), s0, 0.5, 9, 0xff6060); st.sparks(V(x, 0.8, z), s0, 26, { color: 0xff7070, speed: 9 });
  st.screen(s0, 0.18, [1, 0.85, 0.85], 0.45); st.shake(s0, 0.3, 0.25);
}
/** the 8 squares a knight on (x, z) attacks, k squares per step */
const knightSquares = (x, z, k = 1) => [[1, 2], [2, 1], [2, -1], [1, -2], [-1, -2], [-2, -1], [-2, 1], [-1, 2]].map(([a, b]) => ({ x: x + a * SQ * k, z: z + b * SQ * k }));
/** the squares of an L: along the file first, then along the rank */
function lPath(x0, z0, x1, z1) {
  const c = [], i0 = Math.round((x0 - cellX(0)) / SQ), i1 = Math.round((x1 - cellX(0)) / SQ), j0 = Math.round((z0 - 1) / SQ), j1 = Math.round((z1 - 1) / SQ);
  const dj = Math.sign(j1 - j0) || 1, di = Math.sign(i1 - i0) || 1;
  for (let j = j0; j !== j1 + dj; j += dj) c.push({ x: cellX(clamp(i0, 0, 7)), z: j * SQ + 1 });
  for (let i = i0 + di; i !== i1 + di && Math.abs(i1 - i0) > 0; i += di) c.push({ x: cellX(clamp(i, 0, 7)), z: j1 * SQ + 1 });
  return c;
}

// =====================================================================================================
const pawnA = {
  kind: 'pawn', opt: 'A', tag: '새 개념', name: '승격: 거대 퀸', nota: 'e8=Q!!',
  line: '폰이 그 자리에서 키 7 m 거대 퀸으로 승격 — 5초 동안 앞길의 적을 볼링 핀처럼 쓸어 버린다', cut: ACT, dur: 14,
  spec: [
    ['효과', '5초 동안 거대 퀸 (키 7 m) · 초속 16 m 직진 · 넘어지지 않음 · 장애물 무시'],
    ['맞으면', '정면은 볼링 핀처럼 날아가 2.5초 쓰러짐 (코스 밖이면 20 m 뒤에서) · 옆 6 m 안은 걸음 충격으로 1.6초'],
    ['범위', '내 앞길 전체 — 몸 폭 4 m, 걸음 충격 폭 12 m'],
    ['체스', '승격 — 끝 줄에 닿은 폰이 퀸이 되는 엔드게임의 꿈 (e8=Q)'],
    ['맞서기', '진로에서 옆으로 크게 비키기'],
  ],
  build(st) {
    const T = teams(st, 'pawn', { enemy: { queen: [0.4, 69], knight: [-2.6, 67], rook: [2.6, 65.5], bishop: [-0.8, 63], pawn: [3.6, 61], king: [-3.8, 59] } });
    const P = T.me, sG = ACT + 0.7, sR = sG + 0.55, sE = sR + 5.0, vq = 16;
    P.until(ACT).stand(0.15).custom(0.55, (u, t, a) => ({ ...a, y: 1.4 * easeOut(u), yaw: a.yaw + u * 7, run: 0 })).set({ hide: 1, track: 1 });
    const x0 = P.end.x, z0 = P.end.z;
    const qx = s => (s < sR ? x0 : x0 + 1.3 * Math.sin(1.6 * (Math.min(s, sE) - sR))), qz = s => (s < sR ? z0 : z0 + vq * (Math.min(s, sE) - sR));
    P.custom(sE + 0.3 - P.t, (u, t, a) => ({ ...a, x: qx(sG + t), z: qz(sG + t) })).set({ hide: 0, track: 0, y: 0, yaw: 0, run: 0 }).stand(0.35).run(20);
    // the giant
    const giant = makePiece('queen', null, MAT.giant);
    st.obj(giant, s => {
      giant.visible = s >= sG && s < sE + 0.3; if (!giant.visible) return;
      const k = 0.25 + 4.75 * (s < sR ? back(prog(s, sG, sG + 0.45)) : 1) * (1 - easeIn(prog(s, sE, sE + 0.3)));
      const g = Math.max(0, qz(s) - z0) / 2.7, run = s >= sR && s < sE;
      giant.position.set(qx(s), run ? 0.45 * Math.abs(Math.sin(g)) : 0, qz(s)); giant.scale.setScalar(k);
      giant.rotation.set(run ? 0.09 : 0, 0, run ? 0.07 * Math.sin(g) : 0);
    });
    st.aura(s => V(qx(s), 4.2, qz(s)), sG + 0.2, sE, COL.gold, 15, { op: 0.28, pulse: 0.05 });
    st.tiles([...Array(8)].map((_, i) => ({ x: cellX(i), z: snap(z0), d: Math.abs(cellX(i) - x0) * 0.03 })), ACT + 0.1, sR + 0.8, COL.gold, { op: 1 });
    st.pillar(x0, z0, ACT + 0.12, 1.4, 1.0, 30, COL.gold, { rise: 0.15, op: 0.55 });
    st.flash(V(x0, 2, z0), sG, 0.45, 12, 0xfff0c0); st.screen(sG, 0.2, [1, 0.93, 0.7], 0.45);
    st.ring(x0, z0, sR - 0.08, 0.9, 1, 20, COL.gold, { w: 0.08 }); st.dust(x0, z0, sR - 0.05, { n: 14, r: 7, size: 3.5 }); st.shake(sG + 0.35, 0.6, 0.4);
    for (let s = sR + 0.14; s < sE; s += 0.29) { st.ring(qx(s), qz(s), s, 0.55, 1.5, 7, 0xffe2a0, { op: 0.45, w: 0.07 }); st.dust(qx(s), qz(s), s, { n: 5, r: 3, size: 2.4, life: 0.9 }); st.shake(s, 0.2, 0.12); }
    let first = null;
    for (const e of T.enemies) {
      const sh = firstHit(s => Math.abs(qz(s) - e.pred(s).z) < 1.0, sR, sE);
      if (sh === null) { e.run(20); continue; }
      const ep = e.pred(sh), dx = ep.x - qx(sh), side = dx >= 0 ? 1 : -1;
      if (Math.abs(dx) < 2.3) {
        first = first === null ? sh : Math.min(first, sh);
        const tx = ep.x + side * (7 + Math.abs(dx) * 2.2);
        if (Math.abs(tx) > HALF + 0.5) knockOff(st, e, sh, side, { d: 1.25, h: 7, dz: 9, back: 20, far: 4 });
        else e.until(sh).fly(1.25, tx, ep.z + 10, { h: 7, spins: 2 }).lie(2.4).getUp(0.5).run(20);
        st.flash(V(ep.x, 1.2, ep.z), sh, 0.35, 7, 0xfff0c0); st.sparks(V(ep.x, 1, ep.z), sh, 26, { speed: 9 }); st.shake(sh, 0.3, 0.3);
        st.label(s => e.pos(s, 0.8), sh, sh + 0.9, '쾅!', { color: '#ffc94a' });
      } else {
        e.until(sh).fly(0.6, ep.x + side * 2.2, ep.z + 1.5, { h: 1.0, spins: 0 }).lie(1.6).getUp(0.5).run(20);
        st.label(s => e.pos(s, 0.6), sh, sh + 0.8, '휘청', { size: 20, color: '#ffe0a0' });
      }
    }
    for (const a of T.allies) if (a !== P) a.run(20);
    st.knots = slow(first ?? sR + 1.5, 0.3, 0.3);
    st.flash(V(qx(sE + 0.3), 1, qz(sE + 0.3)), sE + 0.25, 0.4, 8); st.sparks(V(qx(sE), 2, qz(sE)), sE + 0.25, 40, { speed: 7 });
    opening(st, P);
    st.shot(st.C(sG - 0.3), (c, s) => ({ p: V(x0 + 6.5, 1.0, z0 + 11.5), l: V(x0, lerp(1.4, 5.5, smooth(prog(s, sG, sR))), z0), fov: 56 }));
    st.shot(st.C(sR + 0.45), (c, s) => { const q = V(qx(s), 0, qz(s)); return { p: q.clone().add(V(13, 4.5, 6)), l: q.clone().add(V(0, 3, 7)), fov: 50 }; });
    st.shot(st.C(sR + 2.4), (c, s) => { const q = V(qx(s), 0, qz(s)); return { p: q.clone().add(V(-4, 13, -17)), l: q.clone().add(V(0, 1, 12)), fov: 52 }; }, { blend: 0.6 });
    st.shot(st.C(sE + 0.1), (c, s) => { const q = V(qx(sE), 0, qz(sE)); return { p: q.clone().add(V(7, 4.5, 10)), l: q.clone().add(V(0, 0.5, -14)), fov: 52 }; });
    st.cap(st.C(sG - 0.25), st.C(sR + 0.45), '폰이 그 자리에서 승격 — 키 7 m 거대 퀸', '쿵! 땅 울림 + 성가대 화음 "아—"');
    st.cap(st.C(sR + 0.45), st.C(sR + 2.4), '5초 동안 초속 16 m로 직진 · 안 넘어진다 · 장애물도 밟고 넘는다', '걸음마다 쿵 — 화면이 흔들림');
    st.cap(st.C(sR + 2.4), st.C(sE + 0.1), '정면은 볼링 핀처럼 날아가고, 옆 6 m 안은 걸음 충격에 넘어진다', '와장창 — 볼링 스트라이크 소리');
    st.cap(st.C(sE + 0.1), st.C(sE + 1.3), '5초 끝 — 펑, 다시 폰. 하지만 이미 선두', '펑!');
    st.stamp(st.C(sG + 0.05), 1.6, 'e8=Q!!', { size: 124 });
    finish(st, st.C(sE + 1.3));
  },
};

// =====================================================================================================
const pawnB = {
  kind: 'pawn', opt: 'B', tag: '새 개념', name: '폰 스톰', nota: 'a4–h4!!',
  line: '우리 팀 뒤에서 코스 폭 가득 거대 폰 8개가 한 줄로 솟아, 아군은 태우고 적은 깔아뭉개며 60 m 돌진한다', cut: ACT, dur: 14,
  spec: [
    ['효과', '내 뒤 12 m에서 코스 폭 가득 거대 폰 8개 (키 2.7 m)가 한 줄로 솟아 3.3초 동안 60 m 돌진'],
    ['아군', '줄에 닿으면 머리에 태워 같이 간다 — 팀 전체가 한 번에 최대 60 m'],
    ['적', '줄에 닿으면 깔려서 3초 납작 (그 자리에 그대로)'],
    ['체스', '폰 스톰 — 폰들이 줄지어 밀고 올라가는 공격 · 폰은 혼자보다 줄일 때 강하다'],
    ['맞서기', '거의 없음 — 줄보다 60 m 넘게 앞서 있으면 안 닿는다'],
  ],
  build(st) {
    const T = teams(st, 'pawn', { enemy: { queen: [0.6, 66], knight: [-4.2, 63.5], rook: [3.8, 62], bishop: [-1.6, 60], pawn: [5.2, 58.5], king: [-5.4, 57] } });
    const P = T.me, sUp = ACT + 0.15, sGo = sUp + 0.6, dur = 3.3, sStop = sGo + dur, v = 18;
    const zW0 = snap(P.pred(sUp).z - 12) - 1, zW = s => zW0 + v * clamp(s - sGo, 0, dur);
    const gm = MAT.ghost.clone();
    const ghosts = [...Array(8)].map((_, i) => { const g = makePiece('pawn', null, gm); st.obj(g, s => {
      g.visible = s >= sUp && s < sStop + 0.45; if (!g.visible) return;
      const rise = easeOut(prog(s, sUp, sUp + 0.5)), burst = prog(s, sStop, sStop + 0.45), march = s > sGo && s < sStop ? 0.25 * Math.abs(Math.sin((s - sGo) * 9 + i * 0.8)) : 0;
      g.position.set(cellX(i), lerp(-3, 0, rise) + march, zW(s)); g.scale.setScalar(3 * (1 + 0.25 * burst)); g.rotation.x = s > sGo && s < sStop ? 0.12 : 0;
      gm.opacity = 0.62 * (1 - burst);
    }); return g; });
    void ghosts;
    st.tiles([...Array(8)].map((_, i) => ({ x: cellX(i), z: zW0, d: Math.abs(i - 3.5) * 0.04 })), sUp, sGo + 0.5, COL.cyan, { op: 1 });
    const band = new THREE.Mesh(new THREE.PlaneGeometry(HALF * 2, 4), basic(COL.cyan, 0.4, ADD)); band.rotation.x = -Math.PI / 2;
    st.obj(band, s => { band.visible = s >= sGo && s <= sStop; band.position.set(0, 0.05, zW(s) - 1.5); band.material.opacity = 0.4 * fade(s, sGo, sStop, 0.1, 0.2); });
    for (let s = sGo; s < sStop; s += 0.33) { st.shake(s, 0.25, 0.15); for (const i of [1, 4, 6]) st.dust(cellX(i), zW(s), s, { n: 4, r: 2, size: 2.2, life: 0.8 }); }
    st.flash(V(0, 2, zW(sStop)), sStop, 0.5, 20, 0xc8f4ff); st.sparks(V(0, 2, zW(sStop)), sStop, 70, { color: COL.cyan, speed: 10, up: 0.3 });
    const zEnd = zW(sStop);
    for (const a of T.allies) {
      const sp = firstHit(s => zW(s) >= a.pred(s).z - 0.5, sGo, sStop - 0.15);
      if (sp === null) { a.run(20); continue; }
      const cx = cellX(clamp(Math.round((a.pred(sp).x - cellX(0)) / SQ), 0, 7));
      a.until(sp).custom(sStop - sp, (u, t, a0) => { const s = sp + t, k = smooth(prog(t, 0, 0.28));
        return { ...a0, x: lerp(a0.x, cx, k), z: lerp(a0.z, zW(s) - 0.3, k), y: lerp(0, 2.72, k) + 0.12 * Math.abs(Math.sin(s * 9)), run: 0, pitch: -0.12 * k, roll: 0.1 * Math.sin(s * 5) }; })
        .hop(0.45, cx, zEnd + 2.5, { h: 1.0, y1: 0 }).run(20);
      st.label(s => a.pos(s, 0.9), sp + 0.1, sp + 0.9, '탑승!', { size: 20, color: '#7fe9ff' });
    }
    for (const e of T.enemies) {
      const sh = firstHit(s => zW(s) >= e.pred(s).z - 0.6, sGo, sStop);
      if (sh === null) { e.run(20); continue; }
      e.until(sh).flat(3.0).run(20);
      st.dust(e.pred(sh).x, e.pred(sh).z, sh, { n: 6, r: 1.6, size: 1.6 });
      st.label(s => e.pos(s, 0.4), sh, sh + 1.0, '납작!', { color: '#ff9a5a' });
    }
    opening(st, P, '상대가 20 m 앞 · 우리 팀은 뭉쳐서 달리는 중');
    st.shot(st.C(sUp - 0.05), (c, s) => ({ p: V(0, 4.2, zW0 - 11), l: V(0, 2, zW0 + 7), fov: 55 }));
    st.shot(st.C(sGo + 0.4), (c, s) => ({ p: V(14, 6.5, zW(s) + 9), l: V(0, 2.2, zW(s) + 2), fov: 52 }));
    st.shot(st.C(sGo + 2.0), (c, s) => ({ p: V(0, 15, zW(s) - 16), l: V(0, 0, zW(s) + 6), fov: 54 }), { blend: 0.5 });
    st.shot(st.C(sStop + 0.2), fixed(V(6, 4.5, zEnd + 13), V(0, 0.8, zEnd - 8), 52));
    st.cap(st.C(sUp), st.C(sGo + 0.4), '우리 팀 뒤로 코스 폭 가득, 거대 폰 8개가 한 줄로 솟는다', '둥 — 둥 — 전쟁 북');
    st.cap(st.C(sGo + 0.4), st.C(sGo + 2.0), '한 줄이 3.3초 동안 60 m 돌진 · 닿은 아군은 머리에 태워 같이 간다', '행진 북소리 + 함성 "와아—"');
    st.cap(st.C(sGo + 2.0), st.C(sStop + 0.2), '닿은 적은 깔려서 3초 납작 — 줄은 그 위를 그냥 지나간다', '뿍! 뿍! (깔리는 소리)');
    st.cap(st.C(sStop + 0.2), st.C(sStop + 1.5), '줄이 부서지며 아군 전원 착지 — 팀 전체가 통째로 선두', '와르르 + 착지 툭툭');
    st.stamp(st.C(sGo), 1.5, '폰 스톰!!', { size: 112, sub: 'a4 b4 c4 d4 e4 f4 g4 h4' });
    finish(st, st.C(sStop + 1.5));
  },
};

// =====================================================================================================
const knightA = {
  kind: 'knight', opt: 'A', tag: '새 개념', name: '패밀리 포크', nota: 'Nf7+!!',
  line: '나이트가 하늘 높이 뛰어 적이 가장 몰린 한가운데 착지 — 반경 14 m 적 전원이 하늘로 솟구쳐 20 m 뒤로 떨어진다', cut: ACT, dur: 14,
  spec: [
    ['효과', '하늘로 도약 → 내 앞 60 m 안에서 적이 가장 몰린 곳에 착지 · 반경 14 m 적 전원 2초 공중 → 20 m 뒤로 떨어져 2초 쓰러짐'],
    ['범위', '착지 반경 14 m — 적이 뭉쳐 있을수록 세다'],
    ['체스', '포크 — 나이트 한 수로 킹 · 퀸 · 룩을 한꺼번에 노리는 패밀리 포크'],
    ['맞서기', '흩어져 달리기 · 도약이 보이면 1초 안에 붉은 칸 밖으로'],
  ],
  build(st) {
    const T = teams(st, 'knight', { enemy: PACK });
    const K = T.me, sC = ACT + 0.1, sJ = sC + 0.25, sL = sJ + 1.1;
    const L = centroid(T.enemies, sL), lx = snap(L.x), lz = snap(L.z);
    K.until(ACT).stand(0.1).custom(0.25, (u, t, a) => ({ ...a, sq: lerp(1, 0.72, smooth(u)), run: 0 })).set({ sq: 1 })
      .hop(sL - sJ, lx, lz, { h: 16 }).custom(0.5, (u, t, a) => ({ ...a, sq: u < 0.3 ? lerp(0.7, 1, u / 0.3) : 1, pitch: 0, y: 0 })).run(20);
    st.trail(K, sJ, sL, COL.cyan, { n: 12, dt: 0.03, size: 1.4 });
    st.tiles(knightSquares(lx, lz, 2).map((q, i) => ({ ...q, d: i * 0.05 })), sJ + 0.1, sL + 0.3, COL.red, { op: 1, pulse: 18 });
    st.tiles([{ x: lx, z: lz }], sJ + 0.1, sL + 0.4, COL.red, { op: 1 });
    st.flash(V(lx, 1.5, lz), sL, 0.35, 11); st.screen(sL, 0.12, [1, 1, 1], 0.45); st.shake(sL, 0.9, 0.6);
    st.ring(lx, lz, sL, 0.9, 1, 16, COL.cyan, { w: 0.1, op: 0.8 }); st.ring(lx, lz, sL + 0.06, 1.1, 1, 22, COL.white, { w: 0.05, op: 0.35 });
    st.dust(lx, lz, sL, { n: 16, r: 10, size: 4, life: 1.6 });
    knightSquares(lx, lz, 2).forEach((q, i) => st.pillar(q.x, q.z, sL + 0.03 + i * 0.02, 1.2, 0.55, 18, COL.cyan, { rise: 0.12, op: 0.42 }));
    for (const e of T.enemies) {
      const p = e.pred(sL), d = Math.hypot(p.x - lx, p.z - lz);
      if (d > 14) { e.run(20); continue; }
      const s0 = sL + 0.04 + d * 0.004, side = p.x >= lx ? 1 : -1, r = rng(Math.round(p.z * 10));
      e.until(s0).fly(2.3, clamp(p.x + side * (3 + r() * 3), -7, 7), p.z - (18 + r() * 6), { h: 11 + r() * 3, spins: 2 + (r() > 0.5 ? 1 : 0), roll: 1.2 }).lie(2.0).getUp(0.5).run(20);
      st.label(s => e.pos(s, 0.8), s0 + 0.1, s0 + 1.4, '포크!', { color: '#7fe9ff', size: 22 });
    }
    for (const a of T.allies) if (a !== K) a.run(20);
    st.knots = slow(sL + 0.05, 0.5, 0.33);
    opening(st, K, '상대 6명이 한데 몰려 20 m 앞');
    const k0 = K.pred(sC);
    st.shot(st.C(sC), (c, s) => { const p = K.pos(s); return { p: V(k0.x + 2.4, 1.2, k0.z - 5.5), l: p.clone().add(V(0, 1.5, 3)), fov: 56 }; });
    st.shot(st.C(sJ + 0.4), fixed(V(-17, 15, lz - 20), V(0, 4, lz - 3), 54), { blend: 0.4 });
    st.shot(st.C(sL - 0.02), (c, s) => ({ p: V(lx + 9, 1.5, lz - 12), l: V(lx, lerp(3, 9, smooth(prog(s, sL, sL + 1.2))), lz), fov: 60 }));
    st.shot(st.C(sL + 1.3), (c, s) => ({ p: V(9, 9, lz + 13), l: V(0, 1, lz - 15), fov: 56 }), { blend: 0.6 });
    st.cap(st.C(sC), st.C(sL - 0.02), '나이트가 하늘 높이 도약 — 적이 가장 몰린 곳 한가운데로', '"히이잉—" 말 울음 + 바람 소리');
    st.cap(st.C(sL), st.C(sL + 1.3), '착지 — 나이트가 가는 8칸에서 빛기둥 · 반경 14 m 적 전원이 하늘로', '쾅! 그리고 0.5초 정적');
    st.cap(st.C(sL + 1.3), st.C(sL + 3.2), '공중 2초 → 저마다 20 m 뒤로 떨어져 2초 쓰러짐', '와장창 착지 소리 여섯 번');
    st.stamp(st.C(sL + 0.02), 1.7, 'Nf7+!!', { size: 124, sub: '패밀리 포크' });
    finish(st, st.C(sL + 3.2));
  },
};

// =====================================================================================================
const knightB = {
  kind: 'knight', opt: 'B', tag: '지금 안을 키움', name: '나이트 투어', nota: 'Nf5–d6–e8!!',
  line: '나이트가 L자 점프로 적의 머리에서 머리로 — 6명을 전부 밟아 납작하게 만들고, 적 선두 10 m 앞에 내린다', cut: ACT, dur: 14,
  spec: [
    ['효과', '적 머리를 L자 점프로 차례로 밟음 (최대 6명, 거리 제한 없음) · 밟힌 적은 3초 납작 · 마지막은 적 선두 10 m 앞에 착지'],
    ['범위', '코스 위 적 전원 — 가까운 순서'],
    ['체스', '나이트 투어 — 말이 64칸을 한 번씩 다 밟는 퍼즐 · 다른 말을 뛰어넘는 유일한 말'],
    ['지금 안과', '지금 안 (적 사이를 건너 맨 앞으로)을 키움 — 밟힌 적이 3초 납작, 거리 제한 없음'],
  ],
  build(st) {
    const T = teams(st, 'knight', { enemy: { queen: [1.0, 74], knight: [-4.4, 70], rook: [4.2, 67], bishop: [-1.8, 64.5], pawn: [5.0, 61], king: [-4.8, 58.5] } });
    const K = T.me, hd = 0.34, s0 = ACT + 0.2;
    const order = [...T.enemies].sort((a, b) => a.pred(s0).z - b.pred(s0).z);
    K.until(ACT).stand(0.2);
    let s = s0, last = K.pred(s0);
    order.forEach((e, i) => {
      s += hd; const p = e.pred(s), h = e.h;
      st.tiles(lPath(snap(last.x), snap(last.z), snap(p.x), snap(p.z)).map((q, k) => ({ ...q, d: k * 0.02 })), s - hd, s + 1.2, i % 2 ? COL.cyan : COL.gold, { op: 0.9 });
      K.hop(hd, p.x, p.z, { h: 2.2, y1: h * 0.98 }).hold(0.04);
      e.until(s).flat(3.0).run(20);
      st.ring(p.x, p.z, s, 0.5, 0.5, 3.2, COL.cyan, { w: 0.15 }); st.flash(V(p.x, h + 0.2, p.z), s, 0.3, 4, 0xbff4ff);
      st.label(V(p.x, h + 1.6, p.z), s - hd - 0.1, s + 1.4, String(i + 1), { size: 30, color: '#1c1500', bg: '#ffc94a' });
      st.label(s2 => e.pos(s2, 0.2), s + 0.05, s + 0.9, '납작!', { size: 18, color: '#ff9a5a' });
      s += 0.04; last = p;
    });
    const lead = Math.max(...T.enemies.map(e => e.pred(s + 0.5).z)), fx = 0, fz = snap(lead + 10);
    K.hop(0.5, fx, fz, { h: 3.2, y1: 0 }).custom(0.4, (u, t, a) => ({ ...a, sq: u < 0.3 ? lerp(0.75, 1, u / 0.3) : 1, y: 0 })).run(20);
    const sEnd = s + 0.5;
    st.ring(fx, fz, sEnd, 0.8, 1, 9, COL.gold, { w: 0.1 }); st.flash(V(fx, 1, fz), sEnd, 0.4, 10); st.sparks(V(fx, 1, fz), sEnd, 40, { speed: 8 }); st.shake(sEnd, 0.4, 0.3);
    st.trail(K, s0, sEnd, COL.cyan, { n: 10, dt: 0.022, size: 1.1 });
    for (const a of T.allies) if (a !== K) a.run(20);
    st.knots = warp([s0 - 3.7, s0 - 3.7], [(sEnd - s0) / 0.7, sEnd - s0]);
    opening(st, K, '상대 6명이 흩어져 20 m 앞');
    st.shot(st.C(s0 - 0.05), (c, s2) => { const p = K.pos(s2); return { p: V(p.x + 9, p.y + 10, p.z - 10), l: V(p.x * 0.5, 0.5, p.z + 5), fov: 54 }; }, { blend: 0.3 });
    st.shot(st.C(sEnd - 0.05), fixed(V(fx + 3.5, 2.6, fz + 9.5), V(fx, 1, fz - 10), 54));
    st.cap(st.C(s0), st.C(s0 + 1.3), '판 위에 L자 길과 번호가 뜬다 — 가까운 적부터 1번, 2번, …', '체스 시계 똑딱 + 말발굽 준비');
    st.cap(st.C(s0 + 1.3), st.C(sEnd - 0.05), 'L자 점프로 머리에서 머리로 — 밟힌 적은 3초 납작', '퐁! 퐁! 퐁! — 밟을 때마다 음이 한 칸씩 올라감');
    st.cap(st.C(sEnd - 0.05), st.C(sEnd + 1.6), '마지막 한 칸은 적 선두보다 10 m 앞 — 투어 완성', '마지막 착지 "짠!"');
    st.stamp(st.C(sEnd), 1.6, '투어 완성!!', { size: 112, sub: '6 / 6' });
    finish(st, st.C(sEnd + 1.6));
  },
};

// =====================================================================================================
const bishopA = {
  kind: 'bishop', opt: 'A', tag: '새 개념', name: '심판의 대각선', nota: 'Bh1–a8!!',
  line: '하늘에서 X자 빛 칼날 두 개가 내려와 1.6초 만에 앞 80 m를 쓸어 버린다 — 맞은 적은 대각선으로 튕겨 장외', cut: ACT, dur: 14,
  spec: [
    ['효과', '하늘에서 X자 빛 칼날 둘 · 1.6초 동안 내 앞 80 m를 코스 폭 전체로 쓸고 감'],
    ['맞으면', '대각선으로 튕겨 코스 밖 (장외) → 20 m 뒤에서 다시'],
    ['범위', '내 앞 80 m · 코스 폭 전체 — 빠져나갈 틈 없음'],
    ['체스', '비숍의 긴 대각선 — 판 끝에서 끝까지 한 번에 닿는 사거리 · 성직자의 심판 빛'],
    ['맞서기', '없음 — 대신 0.6초 전에 하늘이 갈라지고 대각선 칸이 빛나며 예고'],
  ],
  build(st) {
    const T = teams(st, 'bishop');
    const B = T.me, sUp = ACT + 0.1, sGo = sUp + 0.6, sw = 1.6, reach = 80;
    B.until(ACT).stand(0.1).custom(sGo + sw - sUp, (u, t, a) => ({ ...a, y: 0.9 * easeOut(prog(t, 0, 0.4)) + 0.06 * Math.sin(t * 6), run: 0 })).run(20);
    const zb = B.end.z, zc = s => zb + 2 + reach * easeIn(prog(s, sGo, sGo + sw)) ** 0.8;
    // the lit diagonals (a bishop's X from its square, bouncing off the rails)
    const cells = []; const i0 = Math.round((B.end.x - cellX(0)) / SQ), j0 = Math.round((zb - 1) / SQ);
    for (const dir of [1, -1]) { let i = i0, di = dir; for (let k = 1; k <= reach / SQ; k++) { i += di; if (i < 0 || i > 7) { di = -di; i += 2 * di; } cells.push({ x: cellX(i), z: (j0 + k) * SQ + 1, d: k * 0.012 }); } }
    st.tiles(cells, sUp, sGo + sw + 0.6, COL.holy, { op: 0.6 });
    st.aura(s => B.pos(s, 0.6), sUp, sGo + sw, COL.holy, 3, { op: 0.6 });
    st.pillar(B.end.x, zb, sUp, sGo - sUp + 0.2, 0.7, 26, COL.holy, { rise: 0.3, hold: 0.7, op: 0.4 });
    st.skyFn = s => 0.45 * fade(s, sUp, sGo + sw + 0.4, 0.3, 0.5);
    st.gradeFn = s => ({ tint: [1, lerp(1, 0.96, fade(s, sUp, sGo + sw, 0.3, 0.4)), lerp(1, 0.88, fade(s, sUp, sGo + sw, 0.3, 0.4))], bloom: 0.5 });
    for (const sg of [1, -1]) {
      const blade = new THREE.Group(), L = HALF * 2 * Math.SQRT2 + 1;
      const core = new THREE.Mesh(new THREE.BoxGeometry(0.3, 7, L), basic(0xfffbe8, 0.75, ADD));
      const glow = new THREE.Mesh(new THREE.BoxGeometry(1.4, 9, L), basic(COL.holy, 0.16, ADD));
      const strip = new THREE.Mesh(new THREE.PlaneGeometry(2.2, L), basic(COL.holy, 0.5, ADD)); strip.rotation.x = -Math.PI / 2; strip.position.y = -3.45;
      core.position.y = glow.position.y = 0; blade.add(core, glow, strip); blade.rotation.y = sg * Math.PI / 4;
      st.obj(blade, s => { blade.visible = s >= sGo - 0.3 && s <= sGo + sw + 0.15; if (!blade.visible) return;
        const drop = easeOut(prog(s, sGo - 0.3, sGo)); blade.position.set(0, lerp(40, 3.5, drop), zc(s)); const k = fade(s, sGo - 0.3, sGo + sw + 0.15, 0.2, 0.15);
        core.material.opacity = 0.75 * k; glow.material.opacity = 0.16 * k; strip.material.opacity = 0.5 * k * drop; });
    }
    st.shake(sGo, sw, 0.18); st.screen(sGo, 0.3, [1, 0.97, 0.85], 0.5);
    for (const e of T.enemies) {
      const sh = firstHit(s => { const p = e.pred(s); return Math.abs((p.z - zc(s)) - Math.abs(p.x)) < 1.1 || Math.abs((zc(s) - p.z) - Math.abs(p.x)) < 1.1; }, sGo, sGo + sw);
      if (sh === null) { e.run(20); continue; }
      const p = e.pred(sh), side = p.x >= 0 ? 1 : -1;
      knockOff(st, e, sh, side, { d: 1.2, h: 4, dz: 7, back: 20, far: 3, spins: 2 });
      st.flash(V(p.x, 1, p.z), sh, 0.3, 6, 0xfff6d0); st.sparks(V(p.x, 1, p.z), sh, 22, { color: COL.holy, speed: 9 });
    }
    for (const a of T.allies) if (a !== B) a.run(20);
    const zHit = centroid(T.enemies, sGo + 1).z;
    opening(st, B, '상대가 20 m 앞, 코스 폭 가득 흩어져 달린다');
    st.shot(st.C(sUp), (c, s) => ({ p: V(B.end.x + 3, 1.2, zb + 6), l: V(B.end.x, lerp(1.5, 6, smooth(prog(s, sUp, sGo))), zb - 1), fov: 58 }));
    st.shot(st.C(sGo - 0.2), (c, s) => ({ p: V(-19, 26, zc(s) - 15), l: V(1, 0, zc(s) + 5), fov: 56 }), { blend: 0.35 });
    st.shot(st.C(sGo + sw + 0.1), fixed(V(-18, 7, zHit - 2), V(7, -2, zHit + 6), 60));
    st.shot(st.C(sGo + sw + 1.6), (c, s) => { const p = B.pos(s); return { p: p.clone().add(V(5, 5, -12)), l: p.clone().add(V(0, 0, 14)), fov: 54 }; });
    st.cap(st.C(sUp), st.C(sGo - 0.2), '비숍이 떠오르자 — 판의 대각선이 X자로 빛난다 (앞 80 m)', '성당 종 "데엥—" + 하늘 갈라지는 소리');
    st.cap(st.C(sGo - 0.2), st.C(sGo + sw + 0.1), '하늘에서 X자 빛 칼날 둘 · 1.6초 만에 앞 80 m를 쓸고 간다', '쉬이이잉— 거대한 칼날 소리');
    st.cap(st.C(sGo + sw + 0.1), st.C(sGo + sw + 1.6), '맞은 적은 대각선으로 튕겨 코스 밖 — 장외, 20 m 뒤에서 다시', '팅! 팅! + 떨어지는 비명');
    st.cap(st.C(sGo + sw + 1.6), st.C(sGo + sw + 2.8), '그 사이 우리 팀은 앞으로', '');
    st.stamp(st.C(sGo), 1.6, 'Bh1–a8!!', { size: 116, sub: '롱 다이아고널' });
    finish(st, st.C(sGo + sw + 2.8));
  },
};

// =====================================================================================================
const bishopB = {
  kind: 'bishop', opt: 'B', tag: '지금 안을 키움', name: '그리스 선물', nota: 'B×h7+!!',
  line: '비숍이 리본 달린 선물 상자가 되어 적 무리 한가운데로 — 펑! 반경 25 m 적 전원을 빛 실로 묶어 비숍이 떠난 자리까지 끌어온다', cut: ACT, dur: 14,
  spec: [
    ['효과', '비숍이 선물 상자가 되어 적 무리로 → 펑! 반경 25 m 적 전원을 빛 실로 비숍이 떠난 자리까지 끌어옴 + 2초 어지러움 (좌우 반대)'],
    ['대가', '비숍은 잡힘 — 3초 뒤 떠난 자리에서 다시'],
    ['범위', '내 앞 50 m 안에서 적이 가장 몰린 곳 · 반경 25 m'],
    ['체스', '그리스 선물 — 비숍을 h7에 희생해 킹을 끌어내는 고전 희생수'],
    ['지금 안과', '지금 안 (선두 1~3명을 끌어옴)을 키움 — 반경 25 m 전원, 어지러움 추가'],
  ],
  build(st) {
    const T = teams(st, 'bishop');
    const B = T.me, sT = ACT + 0.1, sLand = sT + 1.15, sBoom = sLand + 0.32, sPull = sBoom + 0.1, pullD = 1.2;
    const L = centroid(T.enemies, sLand), lx = clamp(L.x, -5, 5), lz = L.z;
    const t0 = B.pred(sT), mx = t0.x + (lz - t0.z) * 0.18, mz = (t0.z + lz) / 2;
    const box = s => { const u = prog(s, sT, sLand); const a = u < 0.5 ? smooth(u * 2) : smooth((u - 0.5) * 2);
      const x = u < 0.5 ? lerp(t0.x, mx, a) : lerp(mx, lx, a), z = u < 0.5 ? lerp(t0.z, mz, a) : lerp(mz, lz, a);
      return V(x, 0.9 + 5 * Math.sin(Math.PI * u) + (u >= 1 ? 0 : 0), z); };
    B.until(sT).set({ hide: 1, track: 1 }).custom(sBoom - sT, (u, t, a) => { const p = box(sT + t); return { ...a, x: p.x, z: p.z }; })
      .hold(0.05).set({ track: 0, x: t0.x, z: t0.z }).hold(sBoom + 3.0 - (sBoom + 0.05)).set({ hide: 0, run: 0, y: 0 }).stand(0.4).run(20);
    st.label(V(t0.x, 1.6, t0.z), sBoom + 3.0, sBoom + 4.3, '비숍 다시', { size: 20, color: '#7fe9ff' });
    st.pillar(t0.x, t0.z, sBoom + 2.9, 0.9, 0.7, 6, COL.ally, { op: 0.6 });
    const g = new THREE.Group();
    const cube = new THREE.Mesh(new THREE.BoxGeometry(1.5, 1.5, 1.5), MAT.box); cube.castShadow = true;
    const r1 = new THREE.Mesh(new THREE.BoxGeometry(1.56, 1.56, 0.3), MAT.ribbon), r2 = new THREE.Mesh(new THREE.BoxGeometry(0.3, 1.56, 1.56), MAT.ribbon);
    const bow1 = new THREE.Mesh(new THREE.TorusGeometry(0.32, 0.1, 8, 20), MAT.ribbon), bow2 = bow1.clone();
    bow1.position.set(-0.28, 0.92, 0); bow1.rotation.y = Math.PI / 2; bow2.position.set(0.28, 0.92, 0); bow2.rotation.y = Math.PI / 2;
    g.add(cube, r1, r2, bow1, bow2);
    st.obj(g, s => { g.visible = s >= sT && s < sBoom; if (!g.visible) return; const p = box(Math.min(s, sLand));
      const wob = s > sLand ? 0.12 * Math.sin((s - sLand) * 40) : 0; g.position.set(p.x, p.y - 0.15 + (s > sLand ? 0.0 : 0), p.z); g.rotation.set(wob, (s - sT) * 4, wob * 0.5);
      g.scale.setScalar(s > sLand ? 1 + 0.2 * prog(s, sLand, sBoom) : 1); });
    st.trail({ pos: s => box(s) }, sT, sLand, COL.holy, { n: 10, dt: 0.03, size: 1.2 });
    st.flash(V(t0.x, 1, t0.z), sT, 0.4, 6, 0xfff4c0); st.sparks(V(t0.x, 1, t0.z), sT, 30, { speed: 5, color: COL.holy });
    st.flash(V(lx, 1.5, lz), sBoom, 0.6, 26); st.screen(sBoom, 0.25, [1, 0.95, 0.8], 0.6); st.shake(sBoom, 0.7, 0.5);
    st.ring(lx, lz, sBoom, 0.8, 1, 25, COL.gold, { w: 0.06 }); st.sparks(V(lx, 1.4, lz), sBoom, 90, { speed: 14, up: 0.3 });
    st.label(V(lx, 3.2, lz), sBoom + 0.1, sBoom + 1.4, '비숍 잡힘', { size: 20, color: '#c8d4e0' });
    st.aura(V(t0.x, 1.2, t0.z), sBoom, sPull + pullD, COL.gold, 3.5, { op: 0.9 });
    T.enemies.forEach((e, i) => {
      const p = e.pred(sLand), dd = Math.hypot(p.x - lx, p.z - lz);
      if (dd > 25) { e.run(20); return; }
      const face = Math.atan2(lx - p.x, lz - p.z);
      e.until(sLand + 0.05).custom(sPull - (sLand + 0.05), (u, t, a) => ({ ...a, run: 0, z: a.z + 1.2 * easeOut(prog(t, 0, 0.2)), yaw: lerp(a.yaw, face, smooth(prog(t, 0, 0.15))) }));
      st.label(s => e.pos(s, 0.85), sLand + 0.06, sBoom, '?', { size: 26, color: '#ffffff' });
      const dx = t0.x + (i - 2.5) * 1.6, dz = t0.z - 1 + (i % 3);
      e.drag(pullD, dx, dz, { ease: q => easeIn(q) ** 0.7 }).lie(0.5, { slide: 0.3 }).getUp(0.5).custom(2.0, (u, t, a) => ({ ...a, run: 1, z: a.z + 2.2 * t, x: a.x + 0.9 * Math.sin(t * 5), gait: a.gait + 2.2 * t, yaw: 0.5 * Math.sin(t * 5) })).run(20);
      dizzy(st, e, sPull + pullD + 0.4, sPull + pullD + 3.0);
      st.beam(V(t0.x, 1.2, t0.z), s => e.pos(s), sBoom + 0.04, sPull + pullD, 0.06, COL.gold, { grow: 0.16, fo: 0.15 });
    });
    for (const a of T.allies) if (a !== B) a.run(20);
    opening(st, B);
    st.shot(st.C(sT), (c, s) => { const p = box(Math.min(s, sLand)); return { p: p.clone().add(V(0, 4.5, -9)), l: p.clone().add(V(0, -0.5, 6)), fov: 54 }; });
    st.shot(st.C(sLand - 0.1), fixed(V(lx + 5, 2.3, lz - 6.5), V(lx, 0.8, lz), 54));
    st.shot(st.C(sBoom), fixed(V(17, 13, (t0.z + lz) / 2 - 8), V(0, 0, (t0.z + lz) / 2 + 1), 56));
    st.shot(st.C(sPull + pullD + 0.3), (c, s) => { const p = T.A.knight.pos(s); return { p: p.clone().add(V(4, 4.5, 9)), l: p.clone().add(V(-1, 0, -14)), fov: 56 }; }, { blend: 0.5 });
    st.cap(st.C(sT), st.C(sLand - 0.1), '비숍이 리본 달린 선물 상자로 — 대각선으로 두 번 꺾어 적 무리 한가운데로', '뿅! + 포장지 바스락');
    st.cap(st.C(sLand - 0.1), st.C(sBoom), '적들이 무심코 선물을 본다', '…?');
    st.cap(st.C(sBoom), st.C(sPull + pullD + 0.3), '펑! 빛 실이 반경 25 m 적 전원에게 — 비숍이 떠난 자리까지 끌려온다', '펑! + 실 감기는 소리 "슈르르륵"');
    st.cap(st.C(sPull + pullD + 0.3), st.C(sPull + pullD + 2.2), '끌려온 적은 2초 어지러움 · 비숍은 희생 — 3초 뒤 떠난 자리에서 다시', '띠용띠용');
    st.stamp(st.C(sBoom), 1.6, 'B×h7+!!', { size: 120, sub: '그리스 선물' });
    finish(st, st.C(sPull + pullD + 2.2));
  },
};

// =====================================================================================================
const rookA = {
  kind: 'rook', opt: 'A', tag: '새 개념', name: '대캐슬링', nota: 'O-O-O!!',
  line: '우리 팀 6명과 상대 팀 6명이 순위대로 하늘로 엇갈려 날아 — 자리를 통째로 맞바꾼다', cut: ACT, dur: 14,
  spec: [
    ['효과', '우리 팀 6명과 상대 팀 6명이 순위대로 자리를 통째로 맞바꿈 (1등↔1등 … 6등↔6등) · 1.8초 비행'],
    ['상대', '착지 후 1.5초 비틀 — 우리 팀은 바로 달린다'],
    ['범위', '코스 위 전원'],
    ['체스', '캐슬링 — 킹과 룩이 서로를 넘어 자리를 바꾸는, 한 수에 두 말이 움직이는 단 하나의 수'],
    ['주의', '뒤집힌 쪽이 곧바로 역전 배율 2배 — 서로 주고받는 핑퐁이 날 수 있다'],
  ],
  build(st) {
    const T = teams(st, 'rook');
    const R = T.me, sMark = ACT + 0.1, sUp = sMark + 0.55, fl = 1.8, sDown = sUp + fl;
    const A = [...T.allies].sort((a, b) => b.pred(sUp).z - a.pred(sUp).z), E = [...T.enemies].sort((a, b) => b.pred(sUp).z - a.pred(sUp).z);
    const tower = makePiece('rook', null, MAT.stone);
    st.obj(tower, s => { tower.visible = s >= sMark && s < sDown + 0.6; if (!tower.visible) return; const p = R.pred(sMark);
      tower.position.set(p.x, lerp(-8, 0, easeOut(prog(s, sMark, sMark + 0.4))) - 8 * easeIn(prog(s, sDown, sDown + 0.6)), p.z - 4); tower.scale.setScalar(5); });
    for (let i = 0; i < 6; i++) {
      const a = A[i], e = E[i], pa = a.pred(sUp), pe = e.pred(sUp);
      st.ring(pa.x, pa.z, sMark, 0.6, 0.4, 1.2, COL.ally, { w: 0.25 }); st.ring(pe.x, pe.z, sMark, 0.6, 0.4, 1.2, COL.enemy, { w: 0.25 });
      st.beam(V(pa.x, 0.25, pa.z), V(pe.x, 0.25, pe.z), sMark + i * 0.04, sUp + 0.2, 0.06, COL.gold, { grow: 0.25 });
      a.until(sMark).stand(sUp - sMark).hop(fl, pe.x, pe.z, { h: 8 + i * 0.4, yaw: 0 }).custom(0.3, (u, t, q) => ({ ...q, sq: u < 0.4 ? lerp(0.75, 1, u / 0.4) : 1, y: 0 })).run(20);
      e.until(sMark).stand(sUp - sMark).fly(fl, pa.x, pa.z, { h: 9.5 + i * 0.4, spins: 1, ease: q => q }).lie(0.6, { slide: 0.4 }).getUp(0.5).custom(1.0, (u, t, q) => ({ ...q, run: 0, roll: 0.15 * Math.sin(t * 9) })).run(20);
      st.dust(pe.x, pe.z, sDown, { n: 5, r: 1.6, size: 1.6 }); st.dust(pa.x, pa.z, sDown, { n: 5, r: 1.6, size: 1.6 });
      st.trail(a, sUp, sDown, COL.ally, { n: 8, dt: 0.04, size: 0.9 }); st.trail(e, sUp, sDown, COL.enemy, { n: 8, dt: 0.04, size: 0.9 });
      st.label(s => e.pos(s, 0.8), sDown + 0.2, sDown + 1.5, '비틀', { size: 18, color: '#ffb070' });
    }
    st.shake(sDown, 0.5, 0.35);
    st.knots = slow(sUp + 0.5, 0.8, 0.4);
    const zm = (A[0].pred(sUp).z + E[5].pred(sUp).z) / 2;
    opening(st, R, '상대가 20 m 앞 · 순위로 치면 우리 팀 6명이 전부 뒤');
    st.shot(st.C(sMark), fixed(V(20, 18, zm - 10), V(0, 0, zm + 2), 52));
    st.shot(st.C(sUp + 0.1), (c, s) => ({ p: V(30, 5, zm), l: V(0, 6.5, zm), fov: 54 }), { blend: 0.4 });
    st.shot(st.C(sDown + 0.2), (c, s) => { const p = E[0].pred(sUp); return { p: V(4, 3.4, p.z + 6), l: V(0, 1, p.z - 13), fov: 56 }; });
    st.cap(st.C(sMark), st.C(sUp + 0.1), '룩이 성탑을 세우자 — 양 팀 12명 발밑에 빛 원, 순위대로 짝이 이어진다', '쇠사슬 "철컹"');
    st.cap(st.C(sUp + 0.1), st.C(sDown + 0.2), '전원이 하늘로 — 짝과 엇갈려 날아 자리를 통째로 맞바꾼다', '휘이익 — 열두 개 바람 소리');
    st.cap(st.C(sDown + 0.2), st.C(sDown + 2.0), '착지 — 이제 우리 팀이 그만큼 앞 · 상대는 1.5초 비틀', '쿵 — 관중 "오오오?!"');
    st.stamp(st.C(sDown + 0.02), 1.6, 'O-O-O!!', { size: 132, sub: '대캐슬링' });
    finish(st, st.C(sDown + 2.0));
  },
};

// =====================================================================================================
const rookB = {
  kind: 'rook', opt: 'B', tag: '지금 안을 키움', name: '7랭크 성벽', nota: 'Re7!!',
  line: '적 선두 앞에 코스 폭 성벽이 쾅 — 성벽이 룩 쪽으로 밀려오며 적 전원을 룩이 있던 줄까지 쓸어 낸다', cut: ACT, dur: 15,
  spec: [
    ['효과', '적 선두 14 m 앞에 코스 폭 성벽 (높이 5 m) 낙하 → 3.7초 동안 룩이 있던 줄까지 밀려오며 적 전원을 쓸어 냄 → 2초 더 서서 적만 막음'],
    ['아군', '성벽을 그냥 통과'],
    ['범위', '코스 폭 전체 · 적 선두 앞부터 내 자리까지 (밀리는 거리 최대 40 m)'],
    ['체스', '7랭크의 룩 — 상대 진영 7번째 줄을 가로막고 쓸어버리는 룩'],
    ['지금 안과', '지금 안 (적 선두 앞에 서 있는 벽)을 키움 — 서 있는 벽 → 밀고 오는 벽'],
  ],
  build(st) {
    const T = teams(st, 'rook', { ally: { rook: [0, 48] } });
    const R = T.me, sDrop = ACT + 0.6, sHit = sDrop + 0.28, sMove = sHit + 0.15, mv = 3.7, sStop = sMove + mv, sFall = sStop + 2.0;
    const lead = Math.max(...T.enemies.map(e => e.pred(sHit).z)), zW0 = snap(lead + 14), zR = Math.round(R.pred(ACT).z);
    const wz = s => (s < sMove ? zW0 : lerp(zW0, zR, smooth(prog(s, sMove, sStop)) * 0.15 + prog(s, sMove, sStop) * 0.85));
    const wall = new THREE.Group();
    const body = new THREE.Mesh(new THREE.BoxGeometry(HALF * 2 + 1, 5, 1.6), [MAT.wall, MAT.wall, MAT.wallTop, MAT.wallTop, MAT.wall, MAT.wall]);
    body.material[0].map.repeat.set(3.5, 1.2); body.position.y = 2.5; body.castShadow = true; wall.add(body);
    for (let i = 0; i < 9; i++) { const m = new THREE.Mesh(new THREE.BoxGeometry(1.1, 0.9, 1.6), MAT.wallTop); m.position.set(-HALF + 0.5 + i * 1.95, 5.45, 0); m.castShadow = true; wall.add(m); }
    st.obj(wall, s => { wall.visible = s >= sDrop && s < sFall + 1.2; if (!wall.visible) return;
      const y = s < sHit ? lerp(40, 0, easeIn(prog(s, sDrop, sHit))) : -7 * easeIn(prog(s, sFall, sFall + 1.2)); wall.position.set(0, y, wz(s)); wall.rotation.z = s > sFall ? 0.05 * Math.sin(s * 30) : 0; });
    st.tiles([...Array(8)].map((_, i) => ({ x: cellX(i), z: zW0 })), ACT + 0.1, sHit + 0.2, COL.red, { op: 1, pulse: 20 });
    st.flash(V(0, 2, zW0), sHit, 0.5, 26); st.screen(sHit, 0.2, [1, 1, 1], 0.5); st.shake(sHit, 0.8, 0.7);
    st.dust(-5, zW0 - 1.5, sHit, { n: 10, r: 6, size: 4 }); st.dust(5, zW0 - 1.5, sHit, { n: 10, r: 6, size: 4 }); st.ring(0, zW0, sHit, 0.9, 2, 22, 0xffe0b0, { w: 0.06, op: 0.6 });
    for (let s = sMove; s < sStop; s += 0.3) { st.dust(cellX((s * 7 | 0) % 8), wz(s) - 1.2, s, { n: 3, r: 1.5, size: 2.2, life: 0.8 }); st.shake(s, 0.3, 0.12); }
    st.dust(0, zR - 1, sStop, { n: 14, r: 7, size: 3.5 }); st.shake(sStop, 0.4, 0.4);
    st.sparks(V(0, 4, zR), sFall, 60, { color: 0xb8ad9e, speed: 7, grav: 12, size: 0.6 }); st.dust(0, zR, sFall, { n: 16, r: 8, size: 4, life: 1.8 });
    T.enemies.forEach((e, i) => {
      const sh = firstHit(s => e.pred(s).z >= wz(s) - 1.3, sMove - 0.3, sStop);
      if (sh === null) { e.run(20); return; }
      const off = 1.3 + (i % 3) * 0.7;
      e.until(sh).custom(sStop - sh, (u, t, a) => { const s = sh + t; return { ...a, z: wz(s) - off - 0.4 * Math.abs(Math.sin(t * 6)), x: a.x + 0.3 * Math.sin(t * 3 + i), y: 0.25 * Math.abs(Math.sin(t * 7 + i)), pitch: Math.PI / 2 * smooth(prog(t, 0, 0.25)), yaw: Math.PI, run: 0 }; })
        .lie(0.8, { slide: 0 }).getUp(0.5).custom(sFall - (sStop + 1.3), (u, t, a) => ({ ...a, run: 0, z: zR - 1.3 - 0.15 * Math.abs(Math.sin(t * 4)), pitch: 0.12 * Math.sin(t * 5), yaw: 0 })).run(20);
      st.label(s => e.pos(s, 0.6), sh, sh + 0.8, '쾅!', { size: 20, color: '#ffc94a' });
    });
    for (const a of T.allies) {
      const sx = firstHit(s => a.pred(s).z >= wz(s), sHit, sFall);
      a.run(20);
      if (sx !== null) { const p = a.pred(sx); st.flash(V(p.x, 1.2, wz(sx)), sx, 0.4, 3.5, 0x9fefff); st.ring(p.x, wz(sx), sx, 0.4, 0.3, 2.2, COL.cyan, { y: 1.0, w: 0.2 }); st.label(V(p.x, 2.2, wz(sx) + 0.5), sx, sx + 0.8, '통과', { size: 18, color: '#7fe9ff' }); }
    }
    opening(st, R);
    st.shot(st.C(ACT + 0.12), (c, s) => { const p = R.pos(s); return { p: V(6, 14, p.z - 12), l: V(0, 4, zW0 - 6), fov: 54 }; });
    st.shot(st.C(sMove + 0.2), (c, s) => ({ p: V(17, 6.5, wz(s) - 8), l: V(0, 2, wz(s) - 3), fov: 52 }), { blend: 0.5 });
    st.shot(st.C(sStop), fixed(V(6, 4.5, zR + 22), V(0, 2, zR - 1), 54));
    st.shot(st.C(sFall - 0.1), fixed(V(13, 5, zR - 11), V(0, 2, zR), 54));
    st.cap(st.C(ACT + 0.12), st.C(sMove + 0.2), '적 선두 14 m 앞, 하늘에서 코스 폭 성벽이 쾅', '쿠구궁 — 쾅! 먼지 폭풍');
    st.cap(st.C(sMove + 0.2), st.C(sStop), '성벽이 룩 쪽으로 밀려온다 — 적 전원을 쓸어 담아 룩이 있던 줄까지', '바닥 긁히는 굉음 "그그그극"');
    st.cap(st.C(sStop), st.C(sFall - 0.1), '아군은 성벽을 그대로 통과 · 성벽은 2초 더 서서 적만 막는다', '통과할 때 "슝" (물결 소리)');
    st.cap(st.C(sFall - 0.1), st.C(sFall + 1.2), '무너짐 — 그 사이 우리 팀은 저 앞', '와르르');
    st.stamp(st.C(sHit), 1.5, 'Re7!!', { size: 128, sub: '7랭크 성벽' });
    finish(st, st.C(sFall + 1.2));
  },
};

// =====================================================================================================
const queenA = {
  kind: 'queen', opt: 'A', tag: '지금 안을 키움', name: '퀸 희생', nota: 'Q×h7+!!',
  line: '퀸이 하늘로 솟구쳐 사라지고 — 불타는 거대 퀸이 운석처럼 적 무리 한가운데 떨어진다. 반경 20 m 적 전원 30 m 뒤로', cut: ACT, dur: 14,
  spec: [
    ['효과', '퀸이 하늘로 → 1.2초 뒤 적이 가장 몰린 곳에 거대 퀸 낙하 · 반경 20 m 적 전원 30 m 뒤로 날아가 3초 쓰러짐 · 구덩이 4초 동안 적만 느림'],
    ['대가', '퀸은 희생 — 5초 뒤 떠난 자리에서 다시'],
    ['범위', '코스 어디든 — 적이 가장 몰린 곳을 자동으로'],
    ['체스', '퀸 희생 — 가장 센 말을 내주고 판을 이기는 탁월한 수 (Q×h7+!!)'],
    ['맞서기', '붉은 과녁 밖으로 1.2초 안에 · 지금 안 (10~20 m)을 키움'],
  ],
  build(st) {
    const T = teams(st, 'queen', { enemy: PACK });
    const Q = T.me, sUp = ACT + 0.1, sGone = sUp + 0.6, sFall = sGone + 0.45, sImp = sFall + 0.9;
    const C0 = centroid(T.enemies, sImp), tx = clamp(C0.x, -3, 3), tz = C0.z;
    const q0 = Q.pred(sUp);
    Q.until(ACT).stand(0.1).custom(sGone - sUp, (u, t, a) => ({ ...a, y: 70 * easeIn(u), yaw: a.yaw + u * 10, run: 0 })).set({ hide: 1, track: 1, y: 0 })
      .hold(sImp + 3.6 - sGone).set({ hide: 0, track: 0, x: q0.x, z: q0.z, y: 0, run: 0 }).stand(0.4).run(20);
    st.trail(Q, sUp, sGone, COL.gold, { n: 14, dt: 0.02, size: 1.6 });
    st.label(V(q0.x, 1.6, q0.z), sImp + 3.6, sImp + 4.9, '퀸 다시', { size: 20, color: '#7fe9ff' });
    st.pillar(q0.x, q0.z, sImp + 3.5, 0.9, 0.7, 6, COL.ally, { op: 0.6 });
    st.skyFn = s => 0.9 * fade(s, sUp + 0.2, sImp + 1.0, 0.4, 0.8);
    st.gradeFn = s => { const k = fade(s, sUp + 0.2, sImp + 1.0, 0.4, 0.8); return { tint: [1, lerp(1, 0.7, k), lerp(1, 0.62, k)], sat: lerp(1, 0.8, k), vig: 0.2 + 0.5 * k, vigCol: [0.25 * k, 0, 0], bloom: 0.6 + 0.4 * k }; };
    const disc = []; for (let i = -10; i <= 10; i++) for (let j = -10; j <= 10; j++) { const x = snap(tx) + i * SQ, z = snap(tz) + j * SQ; if (Math.abs(x) < HALF && Math.hypot(x - tx, z - tz) < 20) disc.push({ x, z, d: Math.hypot(x - tx, z - tz) * 0.01 }); }
    st.tiles(disc, sGone - 0.2, sImp + 0.1, COL.red, { op: 0.45, pulse: 14 });
    for (const r of [6, 13, 20]) st.ring(tx, tz, sGone - 0.2, sImp - sGone + 0.3, r, r, COL.red, { w: 0.04, op: 1, keep: true });
    const big = makePiece('queen', null, MAT.fire);
    st.obj(big, s => { big.visible = s >= sFall && s < sImp + 1.3; if (!big.visible) return; const k = 8;
      const y = s < sImp ? lerp(95, 0, easeIn(prog(s, sFall, sImp))) : -14 * easeIn(prog(s, sImp + 0.4, sImp + 1.3)); big.position.set(tx, y, tz); big.scale.setScalar(k); big.rotation.y = s * 0.6; });
    st.aura(s => V(tx, lerp(95, 0, easeIn(prog(s, sFall, sImp))) + 6, tz), sFall, sImp, 0xff7a2a, 30, { op: 0.55 });
    for (let s = sFall; s < sImp; s += 0.08) st.sparks(V(tx, lerp(95, 0, easeIn(prog(s, sFall, sImp))) + 10, tz), s, 10, { color: 0xff8a3a, speed: 4, life: 0.7, grav: -6 });
    st.flash(V(tx, 3, tz), sImp, 0.9, 60); st.screen(sImp, 0.45, [1, 1, 1], 0.95); st.shake(sImp, 1.4, 1.1);
    st.ring(tx, tz, sImp, 1.2, 2, 34, 0xffd090, { w: 0.06 }); st.ring(tx, tz, sImp + 0.05, 1.6, 2, 46, 0xffffff, { w: 0.03, op: 0.5 });
    st.dust(tx, tz, sImp, { n: 22, r: 16, size: 6, life: 2.2, color: 0xc9a98a }); st.sparks(V(tx, 2, tz), sImp, 140, { color: 0xffa040, speed: 18, up: 0.4, life: 1.6 });
    const crater = new THREE.Mesh(new THREE.RingGeometry(1.5, 7.5, 48), basic(0x1a0c06, 0.85)); crater.rotation.x = -Math.PI / 2; crater.position.set(tx, 0.04, tz);
    st.obj(crater, s => { crater.visible = s >= sImp && s < sImp + 4.4; crater.material.opacity = 0.85 * fade(s, sImp, sImp + 4.4, 0.05, 0.6); });
    st.tiles(disc.filter(q => Math.hypot(q.x - tx, q.z - tz) < 7), sImp + 0.1, sImp + 4.3, 0xff5a1a, { op: 0.55, pulse: 6 });
    for (const e of T.enemies) {
      const p = e.pred(sImp), d = Math.hypot(p.x - tx, p.z - tz);
      if (d > 20) { e.run(20); continue; }
      const r = rng(Math.round(p.z * 10)), ang = Math.atan2(p.x - tx, p.z - tz);
      const ex = clamp(p.x + Math.sin(ang) * (3 + r() * 3), -7, 7), ez = p.z - (27 + r() * 7);
      e.until(sImp + 0.03).fly(1.9, ex, ez, { h: 12 + r() * 4, spins: 2 + (r() > 0.5 ? 1 : 0), roll: 1.4 }).lie(3.0).getUp(0.5).run(20);
    }
    for (const a of T.allies) if (a !== Q) a.run(20);
    st.knots = slow(sImp, 0.55, 0.3);
    opening(st, Q, '상대 6명이 한데 몰려 20 m 앞');
    st.shot(st.C(sUp), (c, s) => ({ p: V(q0.x + 5, 1.2, q0.z - 7), l: V(q0.x, Math.min(Q.at(Math.min(s, sGone - 0.01)).y + 2, 30), q0.z + 2), fov: 62 }));
    st.shot(st.C(sGone), fixed(V(-11, 15, tz - 36), V(tx, 10, tz), 56));
    st.shot(st.C(sImp - 0.02), (c, s) => ({ p: V(tx + 15, 2.2, tz - 15), l: V(tx, 5, tz), fov: 62 }));
    st.shot(st.C(sImp + 1.4), fixed(V(8, 9, tz + 16), V(0, 1, tz - 16), 56), { blend: 0.6 });
    st.cap(st.C(sUp), st.C(sGone), '퀸이 하늘로 솟구쳐 사라진다 — 하늘이 붉게 어두워지고', '슈우웅— 그리고 낮은 북소리');
    st.cap(st.C(sGone), st.C(sImp - 0.02), '적이 가장 몰린 곳에 지름 40 m 붉은 과녁 · 1.2초 뒤', '경보 "웅— 웅—" + 불타는 소리');
    st.cap(st.C(sImp), st.C(sImp + 1.4), '거대 퀸 낙하 — 반경 20 m 적 전원이 30 m 뒤로 날아가 3초 쓰러짐', '콰과광!! (화면 하얗게) + 0.5초 정적');
    st.cap(st.C(sImp + 1.4), st.C(sImp + 3.4), '구덩이는 4초 동안 적만 느리게 · 퀸은 희생 — 5초 뒤 떠난 자리에서 다시', '불씨 타닥타닥');
    st.stamp(st.C(sImp + 0.02), 1.8, 'Q×h7+!!', { size: 124, sub: '퀸 희생' });
    finish(st, st.C(sImp + 3.4));
  },
};

// =====================================================================================================
const queenB = {
  kind: 'queen', opt: 'B', tag: '새 개념', name: '체크!!', nota: 'Qh5+!!',
  line: '적 전원에게 5초 동안 체크 — 그동안 앞으로 움직이면 하늘에서 낙뢰. 멈추거나 뒤로만 갈 수 있다', cut: ACT, dur: 14,
  spec: [
    ['효과', '적 전원 5초 체크 · 그동안 앞으로 움직이면 낙뢰 → 15 m 뒤로 날아가 2초 쓰러짐 (체크는 이어짐)'],
    ['안전', '멈추기 · 뒤로 가기 · 옆으로 걷기 — 앞으로 1 m 넘게 가면 낙뢰'],
    ['범위', '코스 위 적 전원'],
    ['체스', '체크 — 무시할 수 없고 반드시 응수해야 하는 수 (킹을 지켜라)'],
    ['맞서기', '멈추면 된다 — 대신 5초 동안 우리 팀이 30 m 따라잡는다'],
  ],
  build(st) {
    const T = teams(st, 'queen');
    const Q = T.me, sC = ACT + 0.12, sEnd = sC + 5.0;
    const E = T.E;
    for (const e of T.enemies) {
      const st0 = sC + 0.05 + (e.kind.length % 3) * 0.05;
      if (e.kind === 'knight') {
        e.until(sC + 0.55); const p = e.pred(sC + 0.55); bolt(st, p.x, p.z, sC + 0.55);
        e.fly(1.1, p.x, p.z - 15, { h: 4, spins: 1 }).lie(1.6).getUp(0.5).wobble(sEnd - (sC + 0.55 + 3.2));
        st.label(V(p.x, 2.4, p.z), sC + 0.55, sC + 1.6, '움직였다!', { size: 20, color: '#ff7070' });
      } else if (e.kind === 'pawn') {
        e.until(st0); const p = e.pred(st0);
        e.to(0.3, p.x, p.z + 1.0, { ease: easeOut }).wobble(1.6).to(0.35, p.x, p.z + 2.4).custom(0, (u, t, a) => a);
        const sZ = st0 + 0.3 + 1.6 + 0.35; bolt(st, p.x, p.z + 2.4, sZ);
        e.fly(1.1, p.x, p.z - 12.6, { h: 4, spins: 1 }).lie(1.4).getUp(0.5).wobble(Math.max(0.1, sEnd - (sZ + 3.0)));
        st.label(V(p.x, 2.2, p.z + 2.4), sZ - 0.6, sZ, '살금…', { size: 18, color: '#ffe0a0' });
      } else if (e.kind === 'king') {
        e.until(st0); const p = e.pred(st0);
        e.to(0.3, p.x, p.z + 1.0, { ease: easeOut }).custom(sEnd - (st0 + 0.3), (u, t, a) => ({ ...a, z: a.z - 0.9 * t, run: 1, gait: a.gait - 0.9 * t, pitch: -0.05 }));
        st.label(s => e.pos(s, 0.9), st0 + 1.2, st0 + 3.0, '뒤로는 안전', { size: 18, color: '#9ff0a0' });
      } else {
        e.until(st0); const p = e.pred(st0);
        e.to(0.3, p.x, p.z + 1.1, { ease: easeOut }).wobble(sEnd - (st0 + 0.3), { amp: 0.17 });
      }
      e.run(20);
      st.label(s => e.pos(s, 0.95), sC, sEnd, '+', { size: 34, color: '#ff4a4a', rise: 0 });
      st.beam(s => Q.pos(s, 0.5), s => e.pos(s, 0.2), sC, sC + 0.9, 0.08, COL.red, { grow: 0.15 });
      st.aura(s => e.pos(s), sC, sEnd, COL.red, 2.2, { op: 0.45, rate: 7 });
    }
    for (const a of T.allies) a.run(20);
    st.gradeFn = s => ({ vig: 0.2 + 0.25 * fade(s, sC, sEnd, 0.1, 0.4), vigCol: [0.35, 0, 0] });
    st.screen(sC, 0.2, [1, 0.6, 0.6], 0.35);
    const zE = centroid([E.queen, E.rook, E.bishop], sC + 1).z;
    opening(st, Q);
    st.shot(st.C(sC - 0.02), (c, s) => { const p = Q.pos(s); return { p: p.clone().add(V(1.5, 3.2, -6)), l: p.clone().add(V(0, 0.5, 14)), fov: 54 }; });
    st.shot(st.C(sC + 0.9), fixed(V(14, 3.6, zE + 1), V(0, 1.2, zE - 1), 56));
    st.shot(st.C(sC + 3.1), (c, s) => ({ p: V(3, 3.2, zE + 20), l: V(0, 1, zE - 2), fov: 52 }), { blend: 0.6 });
    st.shot(st.C(sEnd + 0.3), (c, s) => { const p = Q.pos(s); return { p: p.clone().add(V(4, 4, -9)), l: p.clone().add(V(0, 0, 12)), fov: 54 }; });
    st.cap(st.C(sC), st.C(sC + 0.9), '퀸에게서 붉은 체크 선이 적 전원에게 — 5초 동안 체크', '"체크!" 외침 + 체스 시계 "탁!"');
    st.cap(st.C(sC + 0.9), st.C(sC + 2.0), '체크 중에 앞으로 움직이면 — 하늘에서 낙뢰, 15 m 뒤로', '콰직! (번개)');
    st.cap(st.C(sC + 2.0), st.C(sC + 3.6), '멈추거나 뒤로 가면 안전 · 래그돌이라 멈추다가 비틀거린다', '부들부들 + 관중 웃음');
    st.cap(st.C(sC + 3.6), st.C(sEnd + 0.3), '그 5초 동안 아군은 그 사이를 지나 30 m를 따라잡는다', '다다다다 — 우리 팀 발소리만');
    st.cap(st.C(sEnd + 0.3), st.C(sEnd + 1.3), '5초 끝 — 체크가 풀려도 이미 늦었다', '');
    st.stamp(st.C(sC + 0.05), 1.5, '체크!!', { size: 128, sub: 'Qh5+', color: '#ff5a4a' });
    finish(st, st.C(sEnd + 1.3));
  },
};

// =====================================================================================================
const kingA = {
  kind: 'king', opt: 'A', tag: '새 개념', name: '스테일메이트', nota: '½–½!!',
  line: '킹이 땅을 내리찍자 잿빛 파동 — 상대 팀 전원이 4초 동안 돌 석상이 된다. 둘 곳이 하나도 없다', cut: ACT, dur: 14,
  spec: [
    ['효과', '잿빛 파동 → 상대 팀 전원 4초 돌 석상 (움직임 · 스킬 · 점프 전부 불가)'],
    ['석상', '아군이 밀면 넘어간다 — 풀릴 때 쓰러진 채로 시작'],
    ['범위', '코스 위 적 전원'],
    ['체스', '스테일메이트 — 둘 수 있는 수가 하나도 없는 상태 · 지는 쪽이 비기게 만드는 함정수'],
    ['맞서기', '없음 — 대신 4초로 짧게'],
  ],
  build(st) {
    const T = teams(st, 'king', { ally: { king: [0, 47] } });
    const K = T.me, sS = ACT + 0.1, sW = sS + 0.3, sFree = sW + 0.9 + 4.0;
    K.until(ACT).stand(0.1).hop(0.3, K.pred(sS).x, K.pred(sS).z, { h: 0.8 }).custom(0.25, (u, t, a) => ({ ...a, sq: u < 0.3 ? lerp(0.7, 1, u / 0.3) : 1, y: 0 })).run(20);
    const kz = K.pred(sW).z;
    st.ring(0, kz, sW, 0.9, 1, 66, 0xd8d8d8, { w: 0.04, op: 0.9 }); st.ring(0, kz, sW + 0.05, 1.0, 1, 60, 0xffffff, { w: 0.015, op: 0.6 });
    st.flash(V(0, 1, kz), sW, 0.4, 8); st.shake(sW, 0.4, 0.4); st.dust(0, kz, sW, { n: 10, r: 4, size: 2.5 });
    st.gradeFn = s => { const k = fade(s, sW, sW + 1.3, 0.25, 0.5); return { sat: lerp(1, 0.12, k), tint: [lerp(1, 0.95, k), lerp(1, 0.97, k), 1], vig: 0.2 + 0.25 * k }; };
    const shoved = T.E.king, shover = T.A.bishop, ring = (d) => 0.9 * (1 - Math.cbrt(Math.max(0, 1 - d / 66)));
    let sPush = null, pPush = null;
    for (const e of T.enemies) {
      const sSt = sW + ring(Math.max(0, e.pred(sW + 0.5).z - kz)), p = e.pred(sSt);
      e.until(sSt);
      if (e === shoved) {
        sPush = firstHit(s => shover.pred(s).z >= p.z - 0.9, sSt + 0.5, sFree - 0.8) ?? sSt + 2.2; pPush = p;
        e.stoneFor(sPush - sSt).set({ stone: 1 }).fly(0.7, p.x + 0.4, p.z + 2.2, { h: 0.6, spins: 0 }).custom(sFree - (sPush + 0.7), (u, t, a) => ({ ...a, stone: 1 }))
          .set({ stone: 0 }).lie(0.8, { slide: 0 }).getUp(0.5).run(20);
        shover.until(sPush).custom(0.3, (u, t, a) => ({ ...a, run: 0, pitch: 0.3 * Math.sin(u * Math.PI), z: a.z + 0.4 * u })).run(20);
        st.label(s => e.pos(s, 0.5), sPush + 0.1, sPush + 1.3, '쿵 — 넘어감', { size: 18, color: '#e0e0e0' });
        st.dust(p.x + 0.4, p.z + 2.2, sPush + 0.7, { n: 6, r: 2, size: 2 });
      } else e.stoneFor(sFree - sSt).run(20);
      const sq = { x: snap(p.x), z: snap(p.z) };
      for (const [a, b] of [[1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1], [0, -1], [1, -1]]) {
        const x = sq.x + a * SQ; if (Math.abs(x) > HALF) continue; xMark(st, x, sq.z + b * SQ, sSt + 0.1, sFree - 0.1);
      }
      st.sparks(V(p.x, 1, p.z), sFree, 30, { color: 0xc8c2b8, speed: 6, size: 0.5 });
      st.label(s => e.pos(s, 0.95), sSt + 0.2, sSt + 1.6, '둘 곳 없음', { size: 18, color: '#ff8080' });
    }
    for (const a of T.allies) if (a !== K && a !== shover) a.run(20);
    const zS = centroid(T.enemies, sW + 1).z, target = pPush;
    opening(st, K);
    st.shot(st.C(sS), (c, s) => { const p = K.pred(sS); return { p: V(p.x + 2.5, 1.6, p.z - 5), l: V(0, 1, p.z + 14), fov: 58 }; });
    st.shot(st.C(sW + 1.2), (c, s) => ({ p: V(-5.5, 2.4, zS - 7 + 3 * (s - sW - 1.2)), l: V(1, 1, zS + 4), fov: 54 }), { blend: 0.4 });
    st.shot(st.C(sPush - 0.8), fixed(V(target.x + 5, 2.2, target.z - 4.5), V(target.x, 0.8, target.z + 0.5), 56));
    st.shot(st.C(sFree - 0.3), fixed(V(6, 6, zS + 32), V(0, 1, zS), 54));
    st.cap(st.C(sS), st.C(sW + 1.2), '킹이 땅을 내리찍자 잿빛 파동이 판 전체로', '쿵 — 그리고 모든 소리가 뚝 끊김');
    st.cap(st.C(sW + 1.2), st.C(sPush - 0.8), '상대 팀 전원 4초 돌 석상 — 주위 8칸이 전부 X, 둘 곳이 없다', '정적 속에 우리 팀 발소리만');
    st.cap(st.C(sPush - 0.8), st.C(sFree - 0.3), '석상은 아군이 밀면 넘어간다 — 풀릴 때 쓰러진 채로 시작', '돌 넘어지는 "쿵"');
    st.cap(st.C(sFree - 0.3), st.C(sFree + 1.3), '4초 끝 — 돌이 깨지며 풀림 · 그 사이 우리 팀은 24 m를 따라잡았다', '쨍그랑');
    st.stamp(st.C(sW + 0.1), 1.6, '½–½!!', { size: 130, sub: '스테일메이트' });
    finish(st, st.C(sFree + 1.3));
  },
};

// =====================================================================================================
const kingB = {
  kind: 'king', opt: 'B', tag: '새 개념', name: '판 엎기', nota: '┻━┻!!',
  line: '킹이 분노의 발구르기 — 적이 몰린 코스 30 m를 통째로 뒤집어, 위에 있던 적 전원을 킹의 머리 위로 날려 버린다', cut: ACT, dur: 14,
  spec: [
    ['효과', '적이 가장 몰린 코스 30 m를 통째로 뒤집음 · 그 위 적 전원이 내 머리 위로 날아가 25~35 m 뒤에 떨어져 3초 쓰러짐'],
    ['범위', '내 앞 60 m 안, 적이 가장 많은 판 한 구역 (30 m × 코스 폭)'],
    ['체스', '판 엎기 — 지는 쪽이 판을 엎어 버리는, 체스 두는 사람이면 다 아는 그 장면'],
    ['맞서기', '붉은 금이 간 구역 밖으로 0.9초 안에'],
  ],
  build(st) {
    const T = teams(st, 'king', { ally: { king: [0, 46] }, enemy: { queen: [0.5, 68], knight: [-3.2, 66.5], rook: [3.0, 65], bishop: [-1, 63.5], pawn: [4.6, 62], king: [-4.6, 60.5] } });
    const K = T.me, sSt = ACT + 0.1, sF = sSt + 0.8, flip = 0.42, sTop = sF + flip, sBack = sTop + 0.5, sDown = sBack + 0.45;
    K.until(ACT).stand(0.05).hop(0.3, K.pred(sSt).x, K.pred(sSt).z, { h: 0.7 }).hop(0.35, K.pred(sSt).x, K.pred(sSt).z, { h: 1.0 }).custom(0.2, (u, t, a) => ({ ...a, y: 0 })).run(20);
    const rear = Math.min(...T.enemies.map(e => e.pred(sF).z)), z0 = Math.floor((rear - 4) / SQ) * SQ, LEN2 = 30, z1 = z0 + LEN2;
    const tex = TEX.board.clone(); tex.needsUpdate = true; tex.repeat.set(1, LEN2 / 16);
    const hinge = new THREE.Group(); hinge.position.set(0, 0, z0);
    const slab = new THREE.Mesh(new THREE.BoxGeometry(HALF * 2, 0.5, LEN2), [MAT.dark, MAT.dark, new THREE.MeshStandardMaterial({ map: tex, roughness: 0.6 }), MAT.dark, MAT.dark, MAT.dark]);
    slab.position.set(0, -0.24, LEN2 / 2); slab.castShadow = true; slab.receiveShadow = true; hinge.add(slab);
    const pit = new THREE.Mesh(new THREE.PlaneGeometry(HALF * 2, LEN2), basic(0x120a06, 1, { depthWrite: true })); pit.rotation.x = -Math.PI / 2; pit.position.set(0, 0.004, z0 + LEN2 / 2);
    st.obj(pit, s => { pit.visible = s > sF && s < sDown; });
    const ang = s => (s < sF ? 0 : s < sTop ? -1.95 * easeIn(prog(s, sF, sTop)) ** 0.8 : s < sBack ? -1.95 : -1.95 * (1 - easeIn(prog(s, sBack, sDown))));
    st.obj(hinge, s => { hinge.visible = s >= sSt; hinge.rotation.x = ang(s); slab.position.y = s > sF ? -0.24 + 0.01 : -0.24; });
    const edge = []; for (let i = 0; i < 8; i++) { edge.push({ x: cellX(i), z: z0 + 1, d: i * 0.02 }); edge.push({ x: cellX(i), z: z1 - 1, d: i * 0.02 }); }
    for (let j = 1; j < LEN2 / SQ - 1; j++) { edge.push({ x: cellX(0), z: z0 + 1 + j * SQ, d: j * 0.01 }); edge.push({ x: cellX(7), z: z0 + 1 + j * SQ, d: j * 0.01 }); }
    st.tiles(edge, sSt, sF, COL.red, { op: 1, pulse: 22 });
    st.shake(sSt + 0.3, 0.3, 0.35); st.shake(sSt + 0.65, 0.3, 0.45); st.dust(K.pred(sSt).x, K.pred(sSt).z, sSt + 0.3, { n: 6, r: 2, size: 2 });
    st.shake(sF, 0.6, 0.6); st.shake(sDown, 0.9, 0.9); st.screen(sDown, 0.2, [1, 1, 1], 0.45);
    st.dust(0, z0 + 4, sDown, { n: 14, r: 10, size: 5, life: 1.8 }); st.dust(0, z1 - 6, sDown, { n: 14, r: 10, size: 5, life: 1.8 });
    for (const e of T.enemies) {
      const p = e.pred(sF + 0.12);
      if (p.z < z0 || p.z > z1) { e.run(20); continue; }
      const r = rng(Math.round(p.z * 10)), rel = p.z - z0, sl = sF + 0.1 + (1 - rel / LEN2) * 0.12;
      e.until(sl).fly(2.1, clamp(p.x + (r() - 0.5) * 8, -7, 7), p.z - (26 + rel * 0.35 + r() * 4), { h: 15 + rel * 0.25, spins: 2 + (r() > 0.4 ? 1 : 0), roll: 1.6 }).lie(3.0).getUp(0.5).run(20);
      st.label(s => e.pos(s, 0.8), sl + 0.2, sl + 1.4, '으아악', { size: 18, color: '#ffb070' });
    }
    for (const a of T.allies) if (a !== K) a.run(20);
    st.knots = slow(sF + 0.2, 0.7, 0.35);
    opening(st, K, '상대 6명이 한 구역에 몰려 20 m 앞');
    st.shot(st.C(sSt), (c, s) => { const p = K.pos(s); return { p: p.clone().add(V(3, 2.4, -7)), l: p.clone().add(V(0, 1, 18)), fov: 54 }; });
    st.shot(st.C(sF - 0.1), fixed(V(32, 10, z0 - 8), V(0, 9, z0 - 2), 56));
    st.shot(st.C(sDown + 0.2), (c, s) => { const p = T.A.knight.pos(s); return { p: p.clone().add(V(4, 4.2, 8)), l: p.clone().add(V(0, 1, -16)), fov: 56 }; });
    st.cap(st.C(sSt), st.C(sF - 0.1), '킹이 분노의 발구르기 — 적이 선 판 30 m에 붉은 금이 간다', '쿵! 쿵! + 판 갈라지는 소리');
    st.cap(st.C(sF - 0.1), st.C(sDown + 0.2), '판이 통째로 뒤집힌다 — 위에 있던 적 전원이 킹의 머리 위로', '와당탕!!! (체스말 쏟아지는 소리)');
    st.cap(st.C(sDown + 0.2), st.C(sDown + 2.2), '25~35 m 뒤에 흩어져 떨어져 3초 쓰러짐 · 판은 쾅 제자리', '우르르 쾅 + 말들 굴러가는 소리');
    st.stamp(st.C(sF + 0.1), 1.8, '판 엎기!!', { size: 118, sub: '(╯°□°)╯︵ ┻━┻' });
    finish(st, st.C(sDown + 2.2));
  },
};

export const CLIPS = { pawnA, pawnB, knightA, knightB, bishopA, bishopB, rookA, rookB, queenA, queenB, kingA, kingB };
export const PROGRAMS = { pawn: ['pawnA', 'pawnB'], knight: ['knightA', 'knightB'], bishop: ['bishopA', 'bishopB'], rook: ['rookA', 'rookB'], queen: ['queenA', 'queenB'], king: ['kingA', 'kingB'] };

// the building blocks the casting-style clips (casts.js) reuse
export { ACT, CUT, KINDS, warp, slow, teams, opening, follow, fixed, firstHit, centroid, finish, knockOff, dizzy, PACK, mid };
