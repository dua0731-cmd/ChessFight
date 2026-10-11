// 소드 파이트 기물 스킬 5종 장면 대본. 시간은 모두 경기 초(슬로는 화면에서만 늘어난다).
// 규칙(저장소): 모두 칼을 듦 · 장외로 보내면 1점 · 체력 없음 · 폰은 스킬 없음. 수치는 시험 시작값.
import { THREE, COL, HALF, TEX, basic, fade, prog, lerp, clamp, easeOut, smooth, yawTo } from './engine.js';

const R90 = Math.PI / 2;
const unit = (x, z) => { const l = Math.hypot(x, z) || 1; return [x / l, z / l]; };

// ---------- 스킬별 이펙트 ----------
function guardDome(b, a, t0, t1, blocked) {
  const x = a.x0, z = a.z0;
  const grp = new THREE.Group(); grp.position.set(x, 0, z);
  const mD = basic(COL.gold, 0.18, { polygonOffset: false }), mR = basic(COL.gold, 0.85);
  const dome = new THREE.Mesh(new THREE.SphereGeometry(0.95, 40, 16, 0, Math.PI * 2, 0, Math.PI / 2), mD);
  const ring = new THREE.Mesh(new THREE.RingGeometry(0.88, 0.97, 64).rotateX(-R90), mR); ring.position.y = 0.04;
  const band = new THREE.Mesh(new THREE.TorusGeometry(0.93, 0.018, 8, 64), mR); band.rotation.x = R90; band.position.y = 0.55;
  grp.add(dome, ring, band);
  b.add(grp, t => {
    const a1 = fade(t, t0, t1 + (blocked ? 0.12 : 0.2), 0.06, blocked ? 0.12 : 0.2); grp.visible = a1 > 0;
    const pulse = 1 + 0.03 * Math.sin(t * 40);
    const pop = blocked && t > t1 ? 1 + 0.5 * prog(t, t1, t1 + 0.12) : 1;
    grp.scale.setScalar(pulse * pop * lerp(0.7, 1, easeOut(prog(t, t0, t0 + 0.08))));
    mD.opacity = 0.18 * a1; mR.opacity = 0.85 * a1;
  });
}
function skewer(b, x, z, yaw, len, t0, dur) {
  const grp = new THREE.Group(); grp.position.set(x, 0.62, z); grp.rotation.y = yaw;
  const mk = (w, c, o) => { const m = basic(c, o, { polygonOffset: false }); const g = new THREE.PlaneGeometry(w, 1).rotateX(-R90).translate(0, 0, 0.5); return [new THREE.Mesh(g, m), m]; };
  const [glowH, mg] = mk(0.55, COL.ally, 0.45), [coreH, mc] = mk(0.12, 0xffffff, 1);
  const glowV = glowH.clone(); glowV.rotation.z = R90; const coreV = coreH.clone(); coreV.rotation.z = R90;
  const tip = new THREE.Mesh(new THREE.ConeGeometry(0.11, 0.45, 4).rotateX(R90), mc);
  grp.add(glowH, coreH, glowV, coreV, tip);
  b.add(grp, t => {
    const k = prog(t, t0, t0 + dur); const a = t < t0 + dur ? 1 : 1 - prog(t, t0 + dur, t0 + dur + 0.3);
    grp.visible = t >= t0 && a > 0; if (!grp.visible) return;
    const L = Math.max(0.01, len * easeOut(k));
    for (const m of [glowH, coreH, glowV, coreV]) m.scale.z = L;
    tip.position.z = L; tip.visible = k < 1;
    mg.opacity = 0.45 * a; mc.opacity = a;
  });
}
function groundWave(b, { x, z, yaw, s0, s1, t0, speed = 18, blocked = false, seed = 5 }) {
  const [fx, fz] = [Math.sin(yaw), Math.cos(yaw)];
  const tEnd = t0 + (s1 - s0) / speed;
  const grp = new THREE.Group(); grp.position.set(x, 0, z); grp.rotation.y = yaw;
  const mW = basic(0xe8fdff, 0.95, { map: TEX.vgrad, polygonOffset: false }), mC = basic(COL.ally, 0.8, { map: TEX.vgrad, polygonOffset: false }), mF = basic(COL.ally, 0.85);
  const wall = new THREE.Mesh(new THREE.CylinderGeometry(0.85, 0.85, 1.0, 32, 1, true, -0.85, 1.7).translate(0, 0.5, -0.55), mW);
  const wall2 = new THREE.Mesh(new THREE.CylinderGeometry(1.0, 1.0, 1.4, 32, 1, true, -0.8, 1.6).translate(0, 0.7, -0.75), mC);
  const foot = new THREE.Mesh(new THREE.PlaneGeometry(1.5, 0.7).rotateX(-R90).translate(0, 0.04, -0.2), mF);
  const head = new THREE.Group(); head.add(wall, wall2, foot); grp.add(head);
  const crackTex = TEX.crack.clone(); crackTex.needsUpdate = true; crackTex.wrapT = THREE.RepeatWrapping;
  const mK = new THREE.MeshBasicMaterial({ map: crackTex, transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3, toneMapped: false });
  const crack = new THREE.Mesh(new THREE.PlaneGeometry(1.1, 1).rotateX(-R90).translate(0, 0.028, 0.5), mK); crack.position.z = s0;
  grp.add(crack);
  b.add(grp, t => {
    grp.visible = t >= t0 && t < tEnd + 1.6; if (!grp.visible) return;
    const s = Math.min(s1, s0 + (t - t0) * speed);
    head.position.z = s; head.visible = t < tEnd + 0.06;
    const hk = t < tEnd ? 1 : 1 - prog(t, tEnd, tEnd + 0.06);
    mW.opacity = 0.95 * hk; mC.opacity = 0.8 * hk; mF.opacity = 0.85 * hk;
    crack.scale.z = Math.max(0.01, s - s0); crackTex.repeat.set(1, (s - s0) / 2.4);
    mK.opacity = 0.85 * (1 - prog(t, tEnd + 0.8, tEnd + 1.6));
  });
  for (let d = s0 + 0.4, i = 0; d < s1; d += 0.75, i++)
    b.burst({ x: x + fx * d, z: z + fz * d, t0: t0 + (d - s0) / speed, n: 5, seed: seed + i, speed: 1.6, up: 2.2, life: 0.55, size: 0.32, col: 0xcfb28a, grav: 4 });
  const ex = x + fx * s1, ez = z + fz * s1;
  if (blocked) {
    b.flash({ x: ex, y: 0.6, z: ez, t0: tEnd, dur: 0.18, size: 2.4 });
    b.burst({ x: ex, z: ez, t0: tEnd, n: 18, seed: seed + 99, speed: 2.8, up: 3, life: 0.8, size: 0.4, col: 0xbfa27c, grav: 5, dir: [-fx, -fz], spread: 1.3 });
    b.burst({ x: ex, y: 0.6, z: ez, t0: tEnd, n: 10, seed: seed + 98, speed: 3.5, up: 2.5, life: 0.5, size: 0.12, col: 0x6d5642, grav: 9, dir: [-fx, -fz], spread: 1.0, grow: 0.1, op: 1 });
  }
  return { tAt: d => t0 + (d - s0) / speed, tEnd };
}
function bishopRays(b, { bx, bz, ax, az, t0, tf, col = COL.violet }) {
  const [dx, dz] = unit(ax - bx, az - bz), px = -dz, pz = dx;
  for (const s of [-1, 1]) {
    const from = [bx + px * s * 1.0, 1.2, bz + pz * s * 1.0];
    const [ux, uz] = unit(ax - from[0], az - from[2]);
    const to = [ax + ux * 1.7, 0.42, az + uz * 1.7];
    b.beam({ from, to, t0, t1: tf, col, w: 0.012, glow: 0.05, op: 0.9, opFn: t => (t >= t0 && t < tf) ? lerp(0.25, 0.8, prog(t, t0, tf)) : 0 });
    b.beam({ from, to, t0: tf, t1: tf + 0.4, col, w: 0.05, glow: 0.15, opFn: t => (t >= tf && t < tf + 0.4) ? (t < tf + 0.1 ? 1 : 1 - prog(t, tf + 0.1, tf + 0.4)) : 0 });
  }
  // 바닥 X 표시
  const grp = new THREE.Group(); grp.position.set(ax, 0.035, az); grp.rotation.y = yawTo(dx, dz);
  const m = basic(col, 0.9);
  for (const r of [Math.PI / 4, -Math.PI / 4]) { const bar = new THREE.Mesh(new THREE.PlaneGeometry(0.16, 1.9).rotateX(-R90), m); bar.rotation.y = r; grp.add(bar); }
  b.add(grp, t => { const a = fade(t, t0, tf + 0.25); grp.visible = a > 0; m.opacity = 0.9 * a; grp.scale.setScalar(lerp(1.4, 1, easeOut(prog(t, t0, t0 + 0.15)))); });
  b.circle({ x: ax, z: az, r: 1.0, col, t0, t1: tf + 0.2, fill: [t0, tf], dashed: true, spin: 0.8 });
  b.flash({ x: ax, y: 0.55, z: az, t0: tf, dur: 0.18, size: 1.8, col: 0xe6dcff });
  b.burst({ x: ax, y: 0.5, z: az, t0: tf, n: 12, seed: 21, speed: 2.8, up: 1.5, life: 0.4, size: 0.12, col: 0xd9c9ff, grav: 5, grow: 0.2, op: 1 });
}
function followRing(b, a, t0, t1, col, r = 0.55, pulse = true) {
  const m = basic(col, 0.9), m2 = basic(col, 0.5);
  const r1 = new THREE.Mesh(new THREE.RingGeometry(r - 0.07, r, 48).rotateX(-R90), m);
  const r2 = new THREE.Mesh(new THREE.RingGeometry(r - 0.04, r, 48).rotateX(-R90), m2);
  b.add(r1); b.add(r2, t => {
    const k = fade(t, t0, t1); r1.visible = r2.visible = k > 0; if (!k) return;
    const p = a.pos(t); r1.position.set(p[0], 0.04, p[2]); r2.position.set(p[0], 0.04, p[2]);
    const w = pulse ? (t * 2.2) % 1 : 0; r2.scale.setScalar(1 + w * 0.8); m2.opacity = 0.5 * k * (1 - w); m.opacity = 0.9 * k;
  });
}
function shackle(b, a, t0, t1) {
  const x = a.x0, z = a.z0, grp = new THREE.Group(); grp.position.set(x, 0, z);
  const mG = new THREE.MeshStandardMaterial({ color: 0xe0b44a, metalness: 0.8, roughness: 0.3, emissive: 0x5a3fa0, emissiveIntensity: 0.5 });
  const cuff = new THREE.Mesh(new THREE.TorusGeometry(0.42, 0.045, 10, 40), mG); cuff.rotation.x = R90; cuff.position.y = 0.3; grp.add(cuff);
  const glow = basic(COL.gold, 0.5);
  const floor = new THREE.Mesh(new THREE.RingGeometry(0.62, 0.8, 48).rotateX(-R90), glow); floor.position.y = 0.035; grp.add(floor);
  const stakes = [];
  for (let i = 0; i < 4; i++) {
    const ang = i / 4 * Math.PI * 2 + Math.PI / 4, sx = Math.sin(ang) * 0.75, sz = Math.cos(ang) * 0.75;
    const st = new THREE.Mesh(new THREE.ConeGeometry(0.05, 0.36, 6).rotateX(Math.PI), mG); st.position.set(sx, 0.16, sz);
    const ch = new THREE.Mesh(new THREE.CylinderGeometry(0.014, 0.014, 1, 6), mG);
    const from = new THREE.Vector3(sx, 0.28, sz), to = new THREE.Vector3(Math.sin(ang) * 0.42, 0.3, Math.cos(ang) * 0.42);
    ch.position.copy(from).lerp(to, 0.5); ch.scale.y = from.distanceTo(to); ch.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), to.clone().sub(from).normalize());
    grp.add(st, ch); stakes.push(st);
  }
  grp.traverse(o => { if (o.isMesh) o.castShadow = true; });
  b.add(grp, t => {
    grp.visible = t >= t0 && t < t1; if (!grp.visible) return;
    const k = easeOut(prog(t, t0, t0 + 0.12));
    for (const s of stakes) s.position.y = lerp(0.9, 0.16, k);
    cuff.scale.setScalar(lerp(1.8, 1, k)); glow.opacity = 0.5 + 0.2 * Math.sin(t * 30);
  });
  b.flash({ x, y: 0.4, z, t0: t1, dur: 0.16, size: 1.5, col: 0xffe2a0 });
  b.burst({ x, y: 0.3, z, t0: t1, n: 10, seed: 41, speed: 2.5, up: 2, life: 0.4, size: 0.1, col: 0xe8c063, grav: 8, grow: 0.1, op: 1 });
}
function knightAim(b, { x0, z0, x1, z1, t0, tl, h, spots, col = COL.ally }) {
  const pts = [];
  for (let i = 1; i < 22; i++) { const p = i / 22; pts.push([lerp(x0, x1, p), 0.5 + 4 * h * p * (1 - p), lerp(z0, z1, p)]); }
  b.dots({ pts, t0, t1: tl + 0.05 });
  b.circle({ x: x1, z: z1, r: 0.55, col: 0xffffff, t0, t1: tl + 0.05, dashed: true, op: 0.05, spin: 2 });
  for (const [sx, sz] of spots) {
    b.circle({ x: sx, z: sz, r: 1.2, col, t0, t1: tl + 0.12, fill: [t0, tl], op: 0.14 });
    b.shock({ x: sx, z: sz, t0: tl, dur: 0.13, r0: 0.3, r1: 1.25, col, h: 0.55 });
    b.burst({ x: sx, z: sz, t0: tl, n: 10, seed: Math.round(sx * 13 + sz * 7), speed: 2.4, up: 1.8, life: 0.55, size: 0.32, col: 0xd2b68e, grav: 4 });
  }
  b.burst({ x: x1, z: z1, t0: tl, n: 14, seed: 77, speed: 2.0, up: 1.2, life: 0.6, size: 0.36, col: 0xd2b68e, grav: 3 });
  b.flash({ x: x1, y: 0.3, z: z1, t0: tl, dur: 0.14, size: 1.6 });
}
function fallDust(b, x, z, t0, seed = 9) { b.burst({ x, z, t0, n: 8, seed, speed: 1.6, up: 1, life: 0.5, size: 0.3, col: 0xd2b68e, grav: 3 }); }

// ---------- 스킬 ----------
export const SKILLS = [
  {
    key: 'king', piece: '킹', name: '왕의 반격',
    spec: '준비 자세 0.5초 · 근거리 칼 1회를 받아내고 반경 2.5m 밀침(2.8m) · 쿨타임 8초',
    desc: '짧은 준비 자세로 칼 한 번을 받아내고 주변을 밀쳐 낸다. 가장자리에 몰렸을 때 거꾸로 상대를 장외로 보내는 한 수. 상대는 거리를 두거나 공격 타이밍을 늦춰 대응한다.',
    phases: [{ n: '받아내기 0.5s', d: 0.5, c: 'cyan' }, { n: '반격', d: 0.15, c: 'white' }, { n: '후딜 0.4s', d: 0.4, c: 'grey' }],
    cooldown: 8,
    order: [['A', 0.4], ['B', 0.5], ['A', 1]],
    scenes: {
      A: {
        tag: 'A. 가장자리에 몰렸을 때 — 받아내고 거꾸로 장외', dur: 3.4, skillStart: 0.55,
        phases: [{ n: '받아내기 (0.37s에 막음)', d: 0.37, c: 'cyan', min: 180 }, { n: '반격', d: 0.15, c: 'white' }, { n: '후딜 0.4s', d: 0.4, c: 'grey' }],
        build(b) {
          const K = b.actor({ id: 'K', kind: 'king', team: 'ally', x: 10.0, z: 0.2, yaw: -R90 });
          const e1 = b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: 11.3, z: -2.7, yaw: 0 });
          const e2 = b.actor({ id: 'e2', kind: 'pawn', team: 'enemy', x: 6.9, z: 1.1, yaw: R90 });
          e1.walk(0, 11.1, -1.2, 2.3).face(0.68, 10.0, 0.2).slash(0.75);
          e2.walk(0, 8.6, 0.7, 2.4).face(0.75, 10.0, 0.2).act(0.8, 'raise', 0.12);
          K.act(0.55, 'guard', 0.06).act(0.92, 'swing', 0.16).act(1.4, 'rest', 0.12);
          e1.push(0.93, 1.1, -1.4, 2.8); e2.push(0.93, -1.4, 0.5, 2.8);
          guardDome(b, K, 0.55, 0.9, true);
          b.circle({ x: 10.0, z: 0.2, r: 2.5, col: COL.ally, t0: 0.55, t1: 1.3, dashed: true, op: 0.07, spin: 0.6 });
          b.shock({ x: 10.0, z: 0.2, t0: 0.92, dur: 0.16, r0: 0.5, r1: 2.6, col: COL.ally, h: 0.8 });
          b.hitSpark({ x: 10.36, y: 0.95, z: -0.25, t0: 0.9, col: 0xffe28a });
          b.flash({ x: 10.0, y: 0.9, z: 0.2, t0: 0.92, dur: 0.18, size: 2.2, col: 0xfff0c0 });
          fallDust(b, 8.6, 0.7, 1.05, 3);
        },
        cam: [{ t: 0, pos: [7.2, 4.8, 6.6], look: [10.0, 0.3, -0.6] }, { t: 3.4, pos: [7.8, 4.6, 6.0], look: [10.4, 0, -1.0] }],
        score: [{ t: 1.45, team: 'white' }],
        labels: [
          { t0: 0.55, t1: 0.92, at: 'K', text: '준비 자세 0.5초', col: 'ally' },
          { t0: 0.55, t1: 1.3, at: [10.0, 0.1, 2.75], text: '반격 범위 2.5m', col: 'ally' },
          { t0: 0.92, t1: 1.8, at: 'K', text: '칼 1회 받아냄 → 반격', col: 'ally' },
          { t0: 1.0, t1: 1.45, at: 'e1', text: '가장자리 쪽 → 장외', col: 'enemy' },
          { t0: 1.15, t1: 3.4, at: 'e2', text: '안쪽 적: 넘어지기만', col: 'enemy' },
          { t0: 1.5, t1: 3.4, at: [12.3, 0.4, -3.4], text: '장외 +1 (백팀)', col: 'ally' },
        ],
        captions: [
          { t0: 0, t1: 0.9, text: '가장자리에 몰린 킹 — 준비 자세 0.5초 동안 칼 한 번을 받아낸다', sound: '자세 잡을 때 금속 “스릉”' },
          { t0: 0.9, t1: 9, text: '받아낸 순간 반경 2.5m를 밀어냄 — 가장자리 쪽 적은 장외, 안쪽 적은 넘어지기만', sound: '막을 때 “챙!” · 밀어낼 때 “쿵” · 떨어질 때 “휘이—”' },
        ],
      },
      B: {
        tag: 'B. 상대의 대응 — 공격을 늦추면 빈틈', dur: 2.8, skillStart: 0.3,
        phases: [{ n: '받아내기 0.5s', d: 0.5, c: 'cyan' }, { n: '빈틈 0.6s', d: 0.6, c: 'red' }],
        build(b) {
          const K = b.actor({ id: 'K', kind: 'king', team: 'ally', x: 1.6, z: 0, yaw: -R90 });
          const e1 = b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: 0.1, z: 0.1, yaw: yawTo(1.5, -0.1) });
          K.act(0.3, 'guard', 0.06).act(0.8, 'low', 0.12).act(1.9, 'rest', 0.12);
          e1.slash(0.95);
          K.push(1.1, 1, 0.05, 1.6);
          guardDome(b, K, 0.3, 0.8, false);
          b.circle({ x: 1.6, z: 0, r: 0.62, col: COL.red, t0: 0.8, t1: 1.12, op: 0.12 });
          b.hitSpark({ x: 1.2, y: 0.95, z: 0.05, t0: 1.1, col: 0xffffff });
          fallDust(b, 2.4, 0.05, 1.3, 5);
        },
        cam: [{ t: 0, pos: [0.6, 4.0, 5.6], look: [0.85, 0.5, 0] }, { t: 2.8, pos: [0.9, 3.8, 4.9], look: [1.1, 0.4, 0] }],
        labels: [
          { t0: 0.3, t1: 0.8, at: 'K', text: '준비 자세 0.5초', col: 'ally' },
          { t0: 0.35, t1: 0.92, at: 'e1', text: '칼을 참고 기다림', col: 'enemy' },
          { t0: 0.8, t1: 1.12, at: 'K', text: '빈틈 0.6초', col: 'red' },
          { t0: 1.2, t1: 2.8, at: 'K', text: '킹이 맞고 넘어짐', col: 'ally' },
        ],
        captions: [
          { t0: 0, t1: 0.8, text: '공격이 오지 않으면 받아낼 것도 없다 — 상대는 타이밍을 늦춰 대응', sound: '자세 잡을 때 “스릉”' },
          { t0: 0.8, t1: 9, text: '자세가 풀리면 0.6초 빈틈 · 쿨타임 8초는 그대로 돈다', sound: '자세 풀림 “츠—” · 맞을 때 “퍽”' },
        ],
      },
    },
  },
  {
    key: 'queen', piece: '퀸', name: '꼬치 베기',
    spec: '자유 조준 · 길이 6m 폭 1.2m 직선 베기 · 첫째 3.0m / 둘째 2.1m / 셋째 1.5m 밀림 · 쿨타임 8초',
    desc: '자유롭게 조준한 방향으로 긴 검격을 예고하고, 앞의 적을 밀어내며 뒤쪽까지 꿰뚫는다. 줄지어 선 적에게 강하지만 뒤로 갈수록 덜 밀려서 한 번에 여럿이 장외로 가지는 않는다.',
    phases: [{ n: '예고 0.45s', d: 0.45, c: 'cyan' }, { n: '베기', d: 0.12, c: 'white' }, { n: '후딜 0.5s', d: 0.5, c: 'grey' }],
    cooldown: 8,
    order: [['A', 0.4], ['B', 0.5], ['A', 1]],
    scenes: {
      A: {
        tag: 'A. 줄지어 선 적을 한 번에', dur: 3.2, skillStart: 0.3,
        build(b) {
          const Q = b.actor({ id: 'Q', kind: 'queen', team: 'ally', x: 4.6, z: -2.0, yaw: R90 });
          const e1 = b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: 7.2, z: -2.1, yaw: -R90 });
          const e2 = b.actor({ id: 'e2', kind: 'bishop', team: 'enemy', x: 10.6, z: -1.9, yaw: -R90 });
          const e3 = b.actor({ id: 'e3', kind: 'pawn', team: 'enemy', x: 8.6, z: 0.9, yaw: -R90 });
          e3.walk(0.1, 8.0, 0.3, 1.2);
          Q.act(0.3, 'pull', 0.1).act(0.75, 'thrust', 0.12).act(0.87, 'hold', 0.05).act(1.37, 'rest', 0.15);
          b.rect({ x: 4.95, z: -2.0, yaw: R90, len: 6, wid: 1.2, t0: 0.3, t1: 0.95, fill: [0.3, 0.75] });
          skewer(b, 4.95, -2.0, R90, 6, 0.75, 0.12);
          e1.push(0.795, 1, 0, 3.0); e2.push(0.863, 1, 0, 2.1);
          b.hitSpark({ x: 6.9, z: -2.05, t0: 0.795 }); b.hitSpark({ x: 10.3, z: -1.9, t0: 0.863, seed: 8 });
          fallDust(b, 9.6, -2.1, 1.1, 4);
        },
        cam: [{ t: 0, pos: [2.6, 4.7, 3.4], look: [8.4, 0.2, -2.0] }, { t: 3.2, pos: [3.2, 4.5, 2.8], look: [9.0, 0.2, -2.0] }],
        score: [{ t: 1.55, team: 'white' }],
        labels: [
          { t0: 0.3, t1: 0.75, at: 'Q', text: '예고 0.45초', col: 'ally' },
          { t0: 0.85, t1: 2.6, at: 'e1', text: '첫째 3.0m', col: 'enemy' },
          { t0: 0.9, t1: 1.4, at: 'e2', text: '둘째 2.1m (70%)', col: 'enemy' },
          { t0: 0.9, t1: 2.4, at: 'e3', text: '폭 밖: 영향 없음', col: 'enemy' },
          { t0: 1.5, t1: 3.2, at: [12.3, 0.4, -1.9], text: '장외 +1 (백팀)', col: 'ally' },
        ],
        captions: [
          { t0: 0, t1: 0.75, text: '조준한 방향으로 6m 직선 예고 — 줄지어 선 적이 모두 들어온다', sound: '칼 당길 때 “스윽”' },
          { t0: 0.75, t1: 9, text: '첫째 3.0m · 둘째 2.1m(70%) 밀림 — 가장자리 쪽 적만 장외', sound: '꿰뚫을 때 “쉭—” · 맞을 때 “퍽 퍽”' },
        ],
      },
      B: {
        tag: 'B. 예고를 보고 옆으로 피함', dur: 2.6, skillStart: 0.2,
        build(b) {
          const Q = b.actor({ id: 'Q', kind: 'queen', team: 'ally', x: -1.0, z: 0, yaw: R90 });
          const e1 = b.actor({ id: 'e1', kind: 'knight', team: 'enemy', x: 0.9, z: 0.1, yaw: -R90 });
          Q.act(0.2, 'pull', 0.1).act(0.65, 'thrust', 0.12).act(0.77, 'hold', 0.05).act(1.12, 'rest', 0.1);
          b.rect({ x: -0.65, z: 0, yaw: R90, len: 6, wid: 1.2, t0: 0.2, t1: 0.85, fill: [0.2, 0.65] });
          skewer(b, -0.65, 0, R90, 6, 0.65, 0.12);
          e1.walk(0.25, 1.0, 1.25, 2.8, true).walk(0.68, -0.2, 0.6, 3.4).slash(0.95);
          Q.push(1.1, -0.8, -0.6, 1.6);
          b.hitSpark({ x: -0.7, y: 0.85, z: 0.25, t0: 1.1 });
          fallDust(b, -2.2, -0.9, 1.3, 6);
        },
        cam: [{ t: 0, pos: [0.4, 4.6, 6.4], look: [0.8, 0.3, 0.3] }, { t: 2.6, pos: [0.8, 4.3, 5.8], look: [0.6, 0.3, 0.2] }],
        labels: [
          { t0: 0.28, t1: 0.95, at: 'e1', text: '예고 보고 옆으로 한 걸음', col: 'enemy' },
          { t0: 0.78, t1: 1.3, at: [4.9, 0.3, 0], text: '빗나감', col: 'white' },
          { t0: 0.78, t1: 1.1, at: 'Q', text: '후딜 0.5초', col: 'red' },
          { t0: 1.2, t1: 2.6, at: 'Q', text: '퀸이 맞고 넘어짐', col: 'ally' },
        ],
        captions: [
          { t0: 0, t1: 0.65, text: '예고가 0.45초 보이니 직선 밖으로 한 걸음이면 피한다', sound: '칼 당길 때 “스윽”' },
          { t0: 0.65, t1: 9, text: '빗나가면 후딜 0.5초 — 그 사이 칼 한 번을 그대로 맞는다', sound: '헛베기 “쉭” · 맞을 때 “퍽”' },
        ],
      },
    },
  },
  {
    key: 'rook', piece: '룩', name: '열린 파일 포격',
    spec: '조준 방향 지면 충격파 · 길이 10m 폭 1.5m · 가까이 2.6m / 멀리 1.6m 밀림 · 장애물에 막힘 · 쿨타임 7초',
    desc: '칼을 땅에 내리쳐 바닥을 타고 달리는 직선 충격파를 보낸다. 열린 통로를 장악하지만 단단한 장애물에 막히고, 발동 중에는 방향을 꺾을 수 없다. 경기장 전체를 가로지르지 않도록 길이를 10m로 끊었다.',
    phases: [{ n: '예고 0.5s', d: 0.5, c: 'cyan' }, { n: '충격파 0.55s', d: 0.55, c: 'white' }, { n: '후딜 0.4s', d: 0.4, c: 'grey' }],
    cooldown: 7,
    order: [['A', 0.4], ['B', 0.5], ['C', 0.5], ['A', 1]],
    scenes: {
      A: {
        tag: 'A. 열린 통로를 따라 밀어냄', dur: 3.4, skillStart: 0.2,
        build(b) {
          const R = b.actor({ id: 'R', kind: 'rook', team: 'ally', x: 0.8, z: -3.5, yaw: R90 });
          const e1 = b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: 4.4, z: -3.6, yaw: -R90 });
          const e2 = b.actor({ id: 'e2', kind: 'queen', team: 'enemy', x: 10.9, z: -3.3, yaw: -R90 });
          R.act(0.2, 'overhead', 0.15).act(0.7, 'slam', 0.1).act(1.65, 'rest', 0.2);
          b.rect({ x: 1.3, z: -3.5, yaw: R90, len: 10, wid: 1.5, t0: 0.2, t1: 1.3, fill: [0.2, 0.7] });
          b.circle({ x: 0.8, z: -3.5, r: 0.7, col: COL.ally, t0: 0.2, t1: 1.25, op: 0.2 });
          const w = groundWave(b, { x: 0.8, z: -3.5, yaw: R90, s0: 0.6, s1: 10.5, t0: 0.7 });
          e1.push(w.tAt(3.6), 1, 0, 2.6); e2.push(w.tAt(10.1), 1, 0, 1.6);
          b.hitSpark({ x: 4.2, z: -3.6, t0: w.tAt(3.6) }); b.hitSpark({ x: 10.7, z: -3.3, t0: w.tAt(10.1), seed: 9 });
          b.flash({ x: 1.4, y: 0.25, z: -3.5, t0: 0.7, dur: 0.16, size: 1.8 });
          fallDust(b, 6.8, -3.6, 1.2, 7);
        },
        cam: [{ t: 0, pos: [-5.2, 5.6, 0.0], look: [5.5, 0, -3.6] }, { t: 3.4, pos: [-4.4, 5.4, -0.4], look: [6.5, 0, -3.6] }],
        score: [{ t: 1.9, team: 'white' }],
        labels: [
          { t0: 0.2, t1: 0.7, at: 'R', text: '예고 0.5초 · 제자리 고정', col: 'ally' },
          { t0: 0.25, t1: 0.7, at: [6.3, 0.15, -4.6], text: '길이 10m · 폭 1.5m', col: 'white' },
          { t0: 0.95, t1: 2.6, at: 'e1', text: '가까이: 2.6m', col: 'enemy' },
          { t0: 1.28, t1: 1.75, at: 'e2', text: '끝까지 와도 1.6m', col: 'enemy' },
          { t0: 1.85, t1: 3.4, at: [12.3, 0.4, -3.3], text: '장외 +1 (백팀)', col: 'ally' },
        ],
        captions: [
          { t0: 0, t1: 0.7, text: '조준 방향으로 10m 직선 예고 — 열린 통로 전체가 위험해진다', sound: '칼 들어 올림 “우웅”' },
          { t0: 0.7, t1: 9, text: '바닥을 타고 10m — 가까울수록 세게(2.6m), 멀면 약하게(1.6m) 밀어냄', sound: '내려칠 때 “쾅!” · 땅 갈라짐 “드드득”' },
        ],
      },
      B: {
        tag: 'B. 단단한 장애물 뒤는 안전', dur: 2.4, skillStart: 0.2,
        build(b, world) {
          const R = b.actor({ id: 'R', kind: 'rook', team: 'ally', x: 0.2, z: 6.75, yaw: R90 });
          b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: 8.8, z: 6.9, yaw: -R90 });
          R.act(0.2, 'overhead', 0.15).act(0.7, 'slam', 0.1).act(1.4, 'rest', 0.2);
          b.rect({ x: 0.7, z: 6.75, yaw: R90, len: 5.0, wid: 1.5, t0: 0.2, t1: 1.05, fill: [0.2, 0.7] });
          const w = groundWave(b, { x: 0.2, z: 6.75, yaw: R90, s0: 0.6, s1: 5.55, t0: 0.7, blocked: true, seed: 31 });
          const ob = world.obstacles.find(o => o.position.x > 0 && o.position.z > 0), bx = ob.position.x;
          b.each(t => { const k = prog(t, w.tEnd, w.tEnd + 0.25); ob.position.x = bx + (k > 0 && k < 1 ? Math.sin(k * 40) * 0.05 * (1 - k) : 0); });
        },
        cam: [{ t: 0, pos: [-2.0, 6.2, 2.4], look: [4.8, 0.4, 6.9] }, { t: 2.4, pos: [-1.4, 6.0, 2.2], look: [5.2, 0.4, 6.9] }],
        labels: [
          { t0: 0.25, t1: 0.95, at: [5.3, 1.1, 6.75], text: '예고선도 장애물에서 끊김', col: 'white' },
          { t0: 1.0, t1: 2.4, at: 'e1', text: '장애물 뒤: 영향 없음', col: 'enemy' },
          { t0: 1.0, t1: 1.4, at: 'R', text: '후딜 0.4초', col: 'grey' },
        ],
        captions: [
          { t0: 0, t1: 0.98, text: '단단한 장애물은 충격파를 막는다 — 예고선도 거기서 끊긴다', sound: '칼 들어 올림 “우웅”' },
          { t0: 0.98, t1: 9, text: '장애물 뒤의 적은 안전 — 룩은 0.4초 동안 다시 못 움직인다', sound: '부딪힐 때 “쿵—” · 돌 부스러기 “타닥”' },
        ],
      },
      C: {
        tag: 'C. 발동 중에는 방향을 못 꺾음', dur: 2.2, skillStart: 0.2,
        build(b) {
          const R = b.actor({ id: 'R', kind: 'rook', team: 'ally', x: -4.5, z: 0.2, yaw: R90 });
          const e1 = b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: 1.2, z: 0.2, yaw: -R90 });
          R.act(0.2, 'overhead', 0.15).act(0.7, 'slam', 0.1).act(1.65, 'rest', 0.2);
          b.rect({ x: -4.0, z: 0.2, yaw: R90, len: 10, wid: 1.5, t0: 0.2, t1: 1.4, fill: [0.2, 0.7] });
          groundWave(b, { x: -4.5, z: 0.2, yaw: R90, s0: 0.6, s1: 10.5, t0: 0.7, seed: 51 });
          e1.walk(0.3, 1.6, 1.95, 2.6);
        },
        cam: [{ t: 0, pos: [-2.6, 5.2, 7.0], look: [-1.6, 0.2, 0.8] }, { t: 2.2, pos: [-2.2, 5.0, 6.6], look: [-1.2, 0.2, 0.8] }],
        labels: [
          { t0: 0.35, t1: 1.3, at: 'e1', text: '예고 보고 비켜 섬', col: 'enemy' },
          { t0: 0.7, t1: 1.45, at: 'R', text: '발동 중 회전 불가', col: 'ally' },
        ],
        captions: [
          { t0: 0, t1: 0.7, text: '예고 0.5초 동안 룩은 그 자리에 고정된다', sound: '칼 들어 올림 “우웅”' },
          { t0: 0.7, t1: 9, text: '한 번 내려치면 방향을 못 꺾는다 — 통로 밖으로 비키면 끝', sound: '내려칠 때 “쾅!” · 땅 갈라짐 “드드득”' },
        ],
      },
    },
  },
  {
    key: 'bishop', piece: '비숍', name: '관통 핀',
    spec: '조준점(최대 8m)으로 모이는 X자 광선 · 감속 40% 1.5초 · 맞은 적 뒤 2m 안이 가장자리면 묶임 0.8초 · 쿨타임 8초',
    desc: '조준점으로 모이는 두 광선이 X자로 교차하며 적을 느리게 한다. 맞은 적의 뒤가 가장자리면 잠깐 묶여 장외 위기가 된다. 조준점과 묶임 조건은 상대에게도 보인다. 세계 대각선 제한은 없다.',
    phases: [{ n: '예고 0.4s', d: 0.4, c: 'cyan' }, { n: '광선', d: 0.1, c: 'white' }, { n: '후딜 0.35s', d: 0.35, c: 'grey' }],
    cooldown: 8,
    order: [['A', 0.4], ['B', 0.4], ['B', 1]],
    scenes: {
      A: {
        tag: 'A. 가운데서 맞으면 감속', dur: 3.0, skillStart: 0.2,
        build(b) {
          const B = b.actor({ id: 'B', kind: 'bishop', team: 'ally', x: -3.0, z: 1.0, yaw: R90 });
          const e1 = b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: 2.8, z: 1.1, yaw: -R90 });
          const a1 = b.actor({ id: 'a1', kind: 'pawn', team: 'ally', x: 0.3, z: 3.8, yaw: R90 });
          B.act(0.2, 'point', 0.1).act(1.0, 'rest', 0.15);
          e1.walk(0, 3.2, 2.0, 1.0, true).walk(0.7, 5.6, 2.5, 2.4).slow(0.6, 2.1, 0.6);
          a1.walk(0.5, 3.4, 2.65, 2.7).face(1.6, 4.5, 2.2).slash(1.6);
          e1.push(1.75, 1.13, -0.47, 1.6);
          bishopRays(b, { bx: -3.0, bz: 1.0, ax: 3.0, az: 1.5, t0: 0.2, tf: 0.6 });
          followRing(b, e1, 0.6, 2.1, COL.violet);
          b.hitSpark({ x: 4.0, y: 0.8, z: 2.35, t0: 1.75 });
          fallDust(b, 5.7, 1.6, 1.95, 12);
        },
        cam: [{ t: 0, pos: [-2.6, 5.4, 6.8], look: [0.8, 0.3, 1.6] }, { t: 3.0, pos: [-2.0, 5.2, 6.4], look: [1.8, 0.3, 1.8] }],
        labels: [
          { t0: 0.2, t1: 0.6, at: [3.0, 0.15, 2.95], text: '조준점 X — 상대에게도 보임', col: 'white' },
          { t0: 0.65, t1: 1.7, at: 'e1', text: '감속 40% · 1.5초', col: 'violet' },
          { t0: 1.0, t1: 1.62, at: 'a1', text: '느려진 적을 따라잡음', col: 'ally' },
          { t0: 1.85, t1: 3.0, at: 'e1', text: '맞고 넘어짐', col: 'enemy' },
        ],
        captions: [
          { t0: 0, t1: 0.6, text: '조준점으로 모이는 두 광선 — X 자리는 0.4초 전부터 모두에게 보인다', sound: '모일 때 “찌잉—”' },
          { t0: 0.6, t1: 9, text: '맞으면 1.5초 동안 40% 느려짐 — 아군이 따라잡을 시간', sound: '꿰뚫을 때 “파칭!” · 느려질 때 낮은 “우웅”' },
        ],
      },
      B: {
        tag: 'B. 맞은 적 뒤가 가장자리면 묶임', dur: 3.2, skillStart: 0.2,
        build(b) {
          const ax = 10.9, az = -1.0;
          const B = b.actor({ id: 'B', kind: 'bishop', team: 'ally', x: 4.3, z: -1.5, yaw: yawTo(ax - 4.3, az + 1.5) });
          const e1 = b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: ax, z: az, yaw: yawTo(4.3 - ax, -1.5 - az) });
          const a1 = b.actor({ id: 'a1', kind: 'pawn', team: 'ally', x: 8.6, z: -2.9, yaw: R90 });
          B.act(0.2, 'point', 0.1).act(1.0, 'rest', 0.15);
          e1.slow(0.6, 1.17, 0);
          a1.walk(0.3, 9.6, -1.3, 2.6).face(0.97, ax, az).slash(1.02);
          e1.push(1.17, 1.3, 0.3, 1.6);
          bishopRays(b, { bx: 4.3, bz: -1.5, ax, az, t0: 0.2, tf: 0.6, col: COL.gold });
          const [dx, dz] = unit(ax - 4.3, az + 1.5);
          b.rect({ x: ax + dx * 0.5, z: az + dz * 0.5, yaw: yawTo(dx, dz), len: 0.75, wid: 0.5, col: COL.gold, t0: 0.2, t1: 0.75, op: 0.3 });
          shackle(b, e1, 0.6, 1.17);
          b.hitSpark({ x: 10.4, y: 0.8, z: -1.12, t0: 1.17 });
        },
        cam: [{ t: 0, pos: [5.6, 5.6, 3.8], look: [10.4, 0, -1.3] }, { t: 3.2, pos: [6.2, 5.4, 3.2], look: [10.8, -0.3, -1.3] }],
        score: [{ t: 2.0, team: 'white' }],
        labels: [
          { t0: 0.22, t1: 0.6, at: 'e1', text: '뒤 2m 안이 가장자리 → 묶임 조건', col: 'gold' },
          { t0: 0.65, t1: 1.15, at: 'e1', text: '묶임 0.8초', col: 'gold' },
          { t0: 0.75, t1: 1.15, at: 'a1', text: '그 사이 아군 칼', col: 'ally' },
          { t0: 1.9, t1: 3.2, at: [12.3, 0.4, -0.7], text: '장외 +1 (백팀)', col: 'ally' },
        ],
        captions: [
          { t0: 0, t1: 0.6, text: '맞을 적 뒤 2m 안이 가장자리면 X가 금색으로 바뀌어 묶임 조건을 알린다', sound: '조건 성립 “딩”' },
          { t0: 0.6, t1: 9, text: '묶임 0.8초 — 그 사이 아군 칼 한 번이면 장외', sound: '묶일 때 쇠사슬 “철컥” · 떨어질 때 “휘이—”' },
        ],
      },
    },
  },
  {
    key: 'knight', piece: '나이트', name: '포크 강하',
    spec: '원하는 착지점 조준(최대 6m) · 도약 0.55초 · 착지 때 두 자리 충격(반경 1.2m, 2.4m 밀림) · 쿨타임 7초',
    desc: '원하는 곳으로 짧게 뛰어올라, 착지할 때 두 방향의 작은 충격으로 두 대상을 함께 압박한다. 충격이 생길 두 자리는 뛰기 전부터 모두에게 보인다. ㄱ자 위치 강제는 없다.',
    phases: [{ n: '도약 0.55s', d: 0.55, c: 'cyan' }, { n: '착지', d: 0.1, c: 'white' }, { n: '후딜 0.35s', d: 0.35, c: 'grey' }],
    cooldown: 7,
    order: [['A', 0.4], ['B', 0.5], ['A', 1]],
    scenes: {
      A: {
        tag: 'A. 두 적을 함께 압박', dur: 3.2, skillStart: 0.4,
        build(b) {
          const N = b.actor({ id: 'N', kind: 'knight', team: 'ally', x: 4.3, z: 0, yaw: R90 });
          const e1 = b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: 10.9, z: 1.4, yaw: yawTo(-1, -0.2) });
          const e2 = b.actor({ id: 'e2', kind: 'rook', team: 'enemy', x: 10.2, z: -1.5, yaw: yawTo(-1, 0.2) });
          N.act(0.15, 'overhead', 0.15).act(0.88, 'slam', 0.08).act(1.3, 'rest', 0.2);
          N.jump(0.4, 9.0, 0, 0.55, 1.5);
          const s = 1.8, c = Math.cos(50 * Math.PI / 180), sn = Math.sin(50 * Math.PI / 180);
          knightAim(b, { x0: 4.3, z0: 0, x1: 9.0, z1: 0, t0: 0.15, tl: 0.95, h: 1.5, spots: [[9 + s * c, s * sn], [9 + s * c, -s * sn]] });
          e1.push(0.96, 1.9, 1.4, 2.4); e2.push(0.96, 1.2, -1.5, 2.4);
          b.hitSpark({ x: 10.7, z: 1.25, t0: 0.96 }); b.hitSpark({ x: 10.1, z: -1.35, t0: 0.96, seed: 6 });
          fallDust(b, 11.7, -3.37, 1.25, 13);
        },
        cam: [{ t: 0, pos: [5.4, 5.3, 5.2], look: [9.6, 0.2, 0.1] }, { t: 3.2, pos: [5.9, 5.0, 4.7], look: [10.0, 0, 0] }],
        score: [{ t: 1.45, team: 'white' }],
        labels: [
          { t0: 0.2, t1: 0.9, at: [9.0, 0.25, 0], text: '착지 전에 두 자리를 미리 보여 줌', col: 'white' },
          { t0: 1.0, t1: 1.4, at: 'e1', text: '장외 쪽으로 2.4m', col: 'enemy' },
          { t0: 1.05, t1: 3.2, at: 'e2', text: '넘어짐', col: 'enemy' },
          { t0: 1.0, t1: 1.3, at: 'N', text: '후딜 0.35초', col: 'grey' },
          { t0: 1.45, t1: 3.2, at: [12.3, 0.4, 3.0], text: '장외 +1 (백팀)', col: 'ally' },
        ],
        captions: [
          { t0: 0, t1: 0.95, text: '원하는 곳에 착지점 조준 — 충격이 생길 두 자리는 뛰기 전부터 보인다', sound: '뛸 때 “휙”' },
          { t0: 0.95, t1: 9, text: '착지하면 두 자리를 함께 친다 — 한 명은 장외, 한 명은 넘어짐', sound: '착지 “쿵!” 두 번 겹쳐 “콰직”' },
        ],
      },
      B: {
        tag: 'B. 보이는 자리에서 한 걸음 비키기', dur: 2.6, skillStart: 0.4,
        build(b) {
          const N = b.actor({ id: 'N', kind: 'knight', team: 'ally', x: -4.5, z: -1.0, yaw: R90 });
          const e1 = b.actor({ id: 'e1', kind: 'pawn', team: 'enemy', x: 1.4, z: 0.5, yaw: -R90 });
          const e2 = b.actor({ id: 'e2', kind: 'pawn', team: 'enemy', x: 1.3, z: -2.4, yaw: -R90 });
          N.act(0.15, 'overhead', 0.15).act(0.88, 'slam', 0.08).act(1.3, 'rest', 0.2);
          N.jump(0.4, 0, -1.0, 0.55, 1.5);
          const s = 1.8, c = Math.cos(50 * Math.PI / 180), sn = Math.sin(50 * Math.PI / 180);
          knightAim(b, { x0: -4.5, z0: -1.0, x1: 0, z1: -1.0, t0: 0.15, tl: 0.95, h: 1.5, spots: [[s * c, -1 + s * sn], [s * c, -1 - s * sn]] });
          e1.walk(0.35, 2.6, 1.6, 2.6);
          e2.push(0.96, 1.3, -1.4, 2.4);
          b.hitSpark({ x: 1.2, z: -2.25, t0: 0.96 });
        },
        cam: [{ t: 0, pos: [-2.6, 5.6, 5.4], look: [0.3, 0.2, -0.8] }, { t: 2.6, pos: [-2.0, 5.3, 5.0], look: [0.8, 0.2, -0.8] }],
        labels: [
          { t0: 0.2, t1: 0.9, at: [0, 0.25, -1.0], text: '두 자리는 상대에게도 보임', col: 'white' },
          { t0: 0.4, t1: 1.4, at: 'e1', text: '원 밖으로 비킴', col: 'enemy' },
          { t0: 1.0, t1: 2.6, at: 'e2', text: '제자리: 맞음', col: 'enemy' },
        ],
        captions: [
          { t0: 0, t1: 0.95, text: '두 자리는 상대에게도 보인다 — 공중 0.55초 안에 원 밖으로 나가면 피한다', sound: '뛸 때 “휙”' },
          { t0: 0.95, t1: 9, text: '비킨 적은 무사, 남은 적만 넘어진다', sound: '착지 “쿵!”' },
        ],
      },
    },
  },
];

export const RULES = {
  kicker: 'CHESS FIGHT · SWORD FIGHT · SKILL PREVIZ',
  title: '소드 파이트 · 기물 스킬 5종',
  desc: '모두 칼을 든다 · 상대를 장외로 보내면 1점 · 체력 없음 · 폰은 스킬 없음. 점수가 모두 1점이라 모든 스킬의 가치는 “얼마나 가장자리로 밀어내느냐”로 정해진다.',
  list: ['킹 · 왕의 반격    퀸 · 꼬치 베기    룩 · 열린 파일 포격', '비숍 · 관통 핀    나이트 · 포크 강하'],
  foot: '경기장 24m × 24m (1칸 1.5m) · 금색 테두리 밖은 장외 · 기획서 수치 기반 사전 시각화 · 실제 게임 화면 아님',
};
