// Pawn Rush ultimates previz - the knight's family fork as the chess edit shorts do it (the user's "Most FIRE
// Knight FORK", "KNIGHTMARE", "For Love Sacrifices Are Made", "Brilliant Moves"), in the caster's own third-person
// view. A long breath before the move: the world drains to grey and only what glows keeps its colour - neon
// squares lighting one by one on the knight's eight squares, a violet flame around the knight, red squares and
// fork lines under the enemies - while the camera swings round to look up at the knight. A beat of silence, the
// call, the leap, the strike with red light falling from the sky, and a slow return of colour with red petals.
import { THREE, V, TEX, lerp, clamp, prog, smooth, easeOut, easeIn, back, fade, rng, keepColour } from './stage.js';
import { ACT, finish, teams, centroid, PACK } from './ults.js';
import { buildTown, behind } from './hero.js';
import { glowTiles, emblem, plight, TX0, TZ0 } from './heroedit.js';

const NEON = [0x1fb99a, 0x6fae2f, 0x4f86e6, 0x9a5cff, 0xff8a2a, 0xffd23f, 0xff4fa0, 0x3fe0ff];
/** a chess square here = 2 x 2 street tiles on a 3 m grid */
const sqC = (x, z) => ({ x: TX0 + 0.75 + 3 * Math.round((x - TX0 - 0.75) / 3), z: TZ0 + 0.75 + 3 * Math.round((z - TZ0 - 0.75) / 3) });
const four = (c, d = 0, w = 1) => [[-0.75, -0.75], [0.75, -0.75], [-0.75, 0.75], [0.75, 0.75]].map(([a, b]) => ({ x: c.x + a, z: c.z + b, d, w }));

/** a glowing square that follows a moving piece */
function followSquare(st, a, s0, s1, color, size = 2.6) {
  const m = new THREE.Mesh(new THREE.PlaneGeometry(size, size).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false })); keepColour(m);
  st.obj(m, s => { const k = fade(s, s0, s1, 0.04, 0.3); m.visible = k > 0; if (!m.visible) return; const p = a.pos(s); m.position.set(p.x, 0.04, p.z); m.material.opacity = k * (0.75 + 0.25 * Math.sin(s * 16)) * (1 + 0.8 * Math.exp(-Math.max(0, s - s0) / 0.12)); });
}
/** a neon line on the ground from a point to a moving piece (the fork's line) */
function forkLine(st, from, a, s0, s1, color) {
  const m = new THREE.Mesh(new THREE.PlaneGeometry(0.28, 1).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.95, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false, side: THREE.DoubleSide })); keepColour(m);
  st.obj(m, s => { const k = fade(s, s0, s1, 0.02, 0.25); m.visible = k > 0; if (!m.visible) return;
    const p = a.pos(s), g = easeOut(prog(s, s0, s0 + 0.18)), tx = lerp(from.x, p.x, g), tz = lerp(from.z, p.z, g), dx = tx - from.x, dz = tz - from.z, len = Math.max(0.05, Math.hypot(dx, dz));
    m.position.set((from.x + tx) / 2, 0.06, (from.z + tz) / 2); m.rotation.y = Math.atan2(dx, dz); m.scale.set(1 + 0.3 * Math.sin(s * 20), 1, len); m.material.opacity = 0.95 * k; });
}
/** violet-blue flames wrapped round a piece */
function flames(st, pos, s0, s1, n = 20) {
  for (let i = 0; i < n; i++) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.smoke, color: i % 3 === 0 ? 0x4f86ff : i % 3 === 1 ? 0x9a5cff : 0xc06cff, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false })); keepColour(sp);
    const th = (i / n) * Math.PI * 2, rate = 1.3 + (i % 5) * 0.12;
    st.obj(sp, s => { const k = fade(s, s0, s1, 0.5, 0.4); sp.visible = k > 0; if (!sp.visible) return; const u = ((s - s0) * rate + i / n) % 1, p = pos(s);
      sp.position.set(p.x + Math.cos(th + s * 2) * 0.55 * (1 - u), p.y - 0.6 + u * 3.4, p.z + Math.sin(th + s * 2) * 0.55 * (1 - u)); const z = 0.9 + 1.9 * u; sp.scale.set(z, z * 1.3, 1); sp.material.opacity = 0.34 * k * (1 - u) * Math.min(1, u * 6); });
  }
}
/** red light falling from the sky onto a piece */
function redBeam(st, pos, s0) {
  const g = new THREE.Group(); const outer = new THREE.Mesh(new THREE.CylinderGeometry(0.9, 0.9, 50, 16, 1, true), new THREE.MeshBasicMaterial({ color: 0xff2030, transparent: true, opacity: 0.45, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false })), core = new THREE.Mesh(new THREE.CylinderGeometry(0.25, 0.25, 50, 12, 1, true), new THREE.MeshBasicMaterial({ color: 0xffe0e0, transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
  g.add(outer, core); keepColour(g);
  st.obj(g, s => { const t = s - s0; g.visible = t >= 0 && t <= 0.9; if (!g.visible) return; const p = pos(s0); g.position.set(p.x, lerp(80, 25, easeIn(Math.min(1, t / 0.12))), p.z); const k = 1 - prog(t, 0.25, 0.9); outer.material.opacity = 0.45 * k; core.material.opacity = 0.9 * k; g.scale.set(1 + 0.6 * prog(t, 0.12, 0.9), 1, 1 + 0.6 * prog(t, 0.12, 0.9)); });
}
/** red petals drifting down through the grey */
function petals(st, ctr, s0, s1, n = 70) {
  const im = new THREE.InstancedMesh(new THREE.PlaneGeometry(0.32, 0.22), new THREE.MeshBasicMaterial({ color: 0xff2a4a, side: THREE.DoubleSide, toneMapped: false }), n); im.frustumCulled = false; keepColour(im);
  const r = rng(13), P = []; for (let i = 0; i < n; i++) P.push({ x: ctr.x + (r() - 0.5) * 22, y: 3 + r() * 10, z: ctr.z + (r() - 0.5) * 22, v: 0.5 + r() * 0.8, sw: 0.6 + r() * 1.2, ph: r() * 6, ax: V(r() - 0.5, r() - 0.5, r() - 0.5).normalize() });
  const m4 = new THREE.Matrix4(), q = new THREE.Quaternion(), one = V(1, 1, 1), p = V();
  st.obj(im, s => { im.visible = s >= s0 && s <= s1; if (!im.visible) return; const t = s - s0;
    P.forEach((a, i) => { p.set(a.x + Math.sin(t * a.sw + a.ph) * 1.2, Math.max(0.05, a.y - a.v * t), a.z + Math.cos(t * a.sw * 0.7 + a.ph) * 0.8); q.setFromAxisAngle(a.ax, t * 3 + a.ph); m4.compose(p, q, one); im.setMatrixAt(i, m4); }); im.instanceMatrix.needsUpdate = true; });
}

const knightFork = {
  kind: 'knight', opt: '', cut: ACT, cutin: false, nota: 'Nf7+!!', dur: 16, tag: '쇼츠 연출 · 3인칭', chip: '주인공의 순간 · 패밀리 포크', cornerHead: '패밀리 포크', cornerTail: '시전자 화면', view: '시전자 (백 나이트)',
  name: '패밀리 포크 — 긴 호흡',
  line: '쇼츠처럼: 세상의 색이 빠지고 빛나는 것만 남는다. 나이트의 8칸이 하나씩 네온으로 켜지고, 적들 발밑에 붉은 칸과 포크 선 — 한 박 쉬고, 도약, 쾅',
  spec: [
    ['호흡', 'Q → 2.4초 차오름 (색이 빠지고, 8칸이 하나씩, 포크 선) → 0.3초 정적 → 도약 → 쾅 → 2초 여운 (색이 천천히 돌아옴)'],
    ['색', '세상은 회색, 빛나는 것만 제 색 — 여덟 가지 네온 칸, 보라 불꽃, 붉은 칸과 포크 선, 내리꽂히는 붉은 빛, 붉은 꽃잎'],
    ['시점', '내 3인칭 그대로 — 차오르는 동안 카메라가 앞으로 돌아 나이트를 올려다보고, 도약은 뒤에서 따라가고, 착지는 옆에서'],
    ['소리', '음악 뚝 → 심장 소리 + 속삭임 "나이트…" → 정적 → "패밀리 포크!!" + 드롭 → 쾅 "탁월한 수!!"'],
    ['맞서기', '차오르는 2.4초 동안 빨간 칸에서 벗어나면 피한다 — 포크 선이 예고'],
  ],
  build(st) {
    st.cute = true;
    buildTown(st);
    const sQ = ACT, sHold = 5.7, sLeap = 6.0, sApex0 = 6.45, sApex1 = 6.75, sL = 7.2, sAfter = sL + 0.5;
    st.knots = [[0, 0], [sApex0, sApex0], [sApex0 + 0.3 / 0.35, sApex1], [sApex0 + 0.3 / 0.35 + (sL - sApex1), sL], [sApex0 + 0.3 / 0.35 + (sL - sApex1) + 0.5 / 0.3, sAfter], [sApex0 + 0.3 / 0.35 + (sL - sApex1) + 0.5 / 0.3 + 60, sAfter + 60]];
    const T = teams(st, 'knight', { enemy: PACK }), K = T.me;
    const L0 = centroid(T.enemies, sL), land = { x: clamp(L0.x, -6, 6), z: L0.z }, lc = sqC(land.x, land.z);
    K.until(sQ); const k0 = K.pred(sQ), kc = sqC(k0.x, k0.z);
    K.custom(sLeap - sQ, (u, t, a) => ({ ...a, run: 0, y: 0.35 * smooth(prog(t, 0.4, 2.2)) + 0.04 * Math.sin(t * 30) * prog(t, 0.6, 2.3), pitch: -0.28 * smooth(prog(t, 0, 0.6)), roll: 0.03 * Math.sin(t * 23) }))
      .hop(sL - sLeap, lc.x, lc.z, { h: 16 }).custom(0.45, (u, t, a) => ({ ...a, y: 0, sq: u < 0.3 ? lerp(0.65, 1, u / 0.3) : 1, pitch: 0 })).stand(0.6).run(20);
    // the enemies: the ones within reach fly
    const hit = [];
    for (const e of T.enemies) {
      const p = e.pred(sL), d = Math.hypot(p.x - lc.x, p.z - lc.z), r = rng(Math.round(p.z * 10));
      if (d > 15) { e.run(20); continue; }
      hit.push(e);
      e.until(sL + 0.04 + d * 0.004).fly(2.3, clamp(p.x + (p.x >= lc.x ? 1 : -1) * (3 + r() * 3), -7, 7), p.z - (18 + r() * 6), { h: 11 + r() * 3, spins: 2 + (r() > 0.5 ? 1 : 0), roll: 1.2 }).lie(2.0).getUp(0.5).run(20);
    }
    for (const a of T.allies) if (a !== K) a.run(20);
    // 1. the colour drains; the world darkens; only what glows keeps its colour
    const gray = s => fade(s, sQ, sL + 2.6, 0.8, 1.6);
    st.gradeFn = s => { const k = gray(s); let p = 0; for (const b of [sLeap, sL]) if (s >= b) p += Math.exp(-(s - b) / 0.1);
      return { sat: lerp(1, 0.05, k), contrast: lerp(1, 1.15, k), dimW: lerp(1, 0.3, k), maskOn: k > 0.01 ? 1 : 0, vig: 0.25 + 0.45 * k, tint: [1, 1, lerp(1, 1.04, k)], bloom: 0.62, rgb: 0.006 * Math.min(1, p), flashA: 0.08 * Math.min(1, p) }; };
    st.lightFn = s => { const k = gray(s); return { sun: 1 - 0.45 * k, hemi: 1 - 0.35 * k }; };
    st.sky.material.uniforms.darkCol.value.set(0x07070b); st.skyFn = s => 0.88 * gray(s);
    const fogA = new THREE.Color(0xd2deec), fogB = new THREE.Color(0x141418); st.obj(null, s => { st.scene.fog.color.lerpColors(fogA, fogB, gray(s)); });
    // 2. the breath: the knight's square, its flame, its eight squares one by one, the enemies' red squares and fork lines
    glowTiles(st, four(kc, 0, 1.2), sQ + 0.1, sLeap + 0.3, 0x9a5cff, { pulse: 12 });
    flames(st, s => K.pos(s), sQ + 0.15, sL + 0.3);
    plight(st, s => K.pos(s, 0.6), sQ + 0.15, sL + 0.2, 0x8a5cff, 60, 14);
    keepColour(K.pivot);
    for (let s = sQ + 0.4; s < sLeap; s += 0.16) keepColour(st.sparks(K.pos(s, -0.2), s, 5, { color: s % 0.32 < 0.16 ? 0xb07cff : 0xff9a4a, speed: 2.5, life: 0.9, grav: -3, size: 0.25 }));
    const KM = [[1, 2], [2, 1], [2, -1], [1, -2], [-1, -2], [-2, -1], [-2, 1], [-1, 2]];
    const beats8 = KM.map((_, i) => sQ + 0.35 + i * 0.25);
    KM.forEach(([a, b], i) => { const c = { x: kc.x + a * 3, z: kc.z + b * 3 }; glowTiles(st, four(c, 0, 1), beats8[i], sLeap + 0.25, NEON[i], { pulse: 10 }); keepColour(st.pillar(c.x, c.z, beats8[i], 0.5, 0.9, 5.5, NEON[i], { rise: 0.2, op: 0.6 })); });
    hit.forEach((e, i) => { const s0 = 4.55 + i * 0.18; followSquare(st, e, s0, sL + 0.25, 0xff2a3a); forkLine(st, lc, e, s0 + 0.05, sL + 0.1, 0xff3040); emblem(st, s => e.pos(s, 1.8), s0, sL + 1.9, '??', '#e5453a', 1.3); });
    glowTiles(st, [...four(lc, 0, 1.4), ...four({ x: lc.x + 3, z: lc.z }, 0.05, 0.5), ...four({ x: lc.x - 3, z: lc.z }, 0.05, 0.5), ...four({ x: lc.x, z: lc.z + 3 }, 0.05, 0.5), ...four({ x: lc.x, z: lc.z - 3 }, 0.05, 0.5)], 4.5, sL + 0.6, 0x3fe0ff, { pulse: 9 });
    // 3. the leap: a violet trail; the landing: eight neon pillars, red light from the sky on each enemy, a white flash, !!
    st.trail(K, sLeap, sL, 0xa070ff, { n: 14, dt: 0.025, size: 1.1, op: 0.55 });
    st.flash(V(lc.x, 1.5, lc.z), sL, 0.22, 8, 0xffffff, { op: 0.7 }); st.screen(sL, 0.06, [1, 1, 1], 0.35); st.shake(sL, 1.0, 0.7);
    keepColour(st.ring(lc.x, lc.z, sL, 1.0, 1, 18, 0x3fe0ff, { w: 0.08 })); keepColour(st.ring(lc.x, lc.z, sL + 0.05, 1.2, 1, 26, 0xffffff, { w: 0.03, op: 0.5 }));
    KM.forEach(([a, b], i) => keepColour(st.pillar(lc.x + a * 3, lc.z + b * 3, sL + 0.02 + i * 0.03, 1.4, 0.75, 22, NEON[i], { rise: 0.12, op: 0.55 })));
    hit.forEach((e, i) => redBeam(st, s => e.pos(s), sL + 0.05 + i * 0.05));
    st.dust(lc.x, lc.z, sL, { n: 14, r: 9, size: 4, life: 1.6, color: 0x9a9a9a });
    emblem(st, () => V(lc.x, 13, lc.z), sL + 0.04, sL + 1.8, '!!', '#1fb99a', 7, { always: true });
    plight(st, () => V(lc.x, 4, lc.z), sL, sL + 0.5, 0xffffff, 2500, 60);
    petals(st, V(lc.x, 0, lc.z), sL + 0.3, sL + 6);
    // what everyone hears and reads
    st.banners.push({ c0: ACT + 0.05, c1: st.C(sL + 1.6), text: '백팀 나이트 · 패밀리 포크!!', sub: 'Nf7+ · 착지 반경 14 m · 전원 하늘로', glyph: '♞︎', color: 'rgba(90,50,170,0.92)', color2: 'rgba(40,20,90,0.92)' });
    st.voices.push({ c0: ACT + 0.15, c1: 4.6, text: '나이트…', size: 50, color: '#cbb6ff' }, { c0: st.C(sLeap), c1: st.C(sLeap) + 0.8, text: '패밀리 포크!!', size: 84, color: '#b48cff' }, { c0: st.C(sL) + 0.15, c1: st.C(sL) + 1.3, text: '탁월한 수!!', size: 74, color: '#3fe0bf', y: 410 });
    st.music.push({ c0: ACT, c1: st.C(sHold), text: '경기 음악 뚝 — 심장 소리', bg: 'rgba(30,24,44,0.88)' }, { c0: st.C(sHold), c1: st.C(sLeap), text: '— 정적 —', bg: 'rgba(10,10,14,0.88)' }, { c0: st.C(sLeap), c1: st.C(sL + 3.2), text: '나이트의 테마 — 드롭!', bg: 'rgba(80,40,150,0.9)' });
    st.stamp(st.C(sLeap) + 0.02, 1.3, 'Nf7+!!', { size: 110, color: '#b48cff', y: 250, rot: -6 });
    // the camera: behind; then round to the front, looking up; behind again for the leap; the side for the landing; behind again
    const kp = s => K.pos(s);
    st.shot(0, behind(kp, { look: 0.8, ahead: 7 }));
    st.shot(sQ, (c, s) => { // round to the side and up, to see the squares light; then down in front, looking up at the knight
      const u1 = smooth(prog(c, sQ, st.C(4.9))), u2 = smooth(prog(c, st.C(4.9), st.C(sHold))), p = kp(s);
      const th = lerp(0, -1.35, u1) + lerp(0, -1.15, u2), r = lerp(lerp(7, 9, u1), 4.3, u2), h = lerp(lerp(3.3, 6.5, u1), 0.8, u2);
      return { p: V(p.x + Math.sin(th) * r, h, p.z - Math.cos(th) * r), l: V(p.x + lerp(0, 1.5, u1) * (1 - u2), lerp(lerp(1.2, 0.2, u1), 1.9, u2), p.z + lerp(lerp(6, 3, u1), 0, u2)), fov: lerp(60, 52, u2), roll: 0.05 * u2 }; });
    st.shot(st.C(sLeap), (c, s) => { const p = kp(s), u = prog(s, sLeap, sL); return { p: p.clone().add(V(2.2, lerp(-0.2, 3.5, u), -7.5)), l: V(lerp(p.x, lc.x, 0.5), lerp(p.y + 1, 0, u), lerp(p.z, lc.z, 0.6) + 4), fov: 62 }; }, { blend: 0.35 });
    st.shot(st.C(sL) - 0.05, (c, s) => ({ p: V(lc.x + 8.5, 1.3, lc.z - 6), l: V(lc.x, lerp(2.5, 8, smooth(prog(s, sL, sL + 1.2))), lc.z + 1), fov: 64, roll: -0.06 }));
    st.shot(st.C(sL + 1.4), (c, s) => { const p = kp(s), u = smooth(prog(s, sL + 1.4, sL + 3)); return { p: p.clone().add(V(lerp(8, 0, u), lerp(1.3, 3.3, u), lerp(-6, -7, u))), l: p.clone().add(V(0, 1, lerp(1, 6, u))), fov: 60 }; }, { blend: 0.6 });
    st.cap(2.7, ACT, '상대 6명이 몰려 20 m 앞 — 내 나이트, 팀 게이지 가득', '평소 경기 음악');
    st.cap(ACT + 0.05, 4.6, 'Q — 세상의 색이 빠지고, 내 발밑이 보랏빛으로, 나이트에 보라 불꽃', '음악 뚝 → 심장 소리 "쿵…" + 속삭임 "나이트…"');
    st.cap(4.6, st.C(sHold), '나이트가 가는 8칸이 하나씩 네온으로 — 적들 발밑은 빨갛게, 포크 선이 그어진다', '쿵 · 쿵 · 쿵 — 점점 빨라짐');
    st.cap(st.C(sHold), st.C(sLeap), '— 한 박 정적 —', '완전한 무음');
    st.cap(st.C(sLeap), st.C(sL), '"패밀리 포크!!" — 보라 불꽃을 두르고 하늘로', '드롭 + 말 울음');
    st.cap(st.C(sL), st.C(sL + 1.4), '쾅 "탁월한 수!!" — 8칸에서 빛기둥, 붉은 빛이 적들 위로 내리꽂힌다', '쾅!! → 0.5초 정적');
    st.cap(st.C(sL + 1.4), st.C(sL + 3.2), '색이 천천히 돌아오고, 붉은 꽃잎과 불씨가 남는다', '나이트의 테마가 잦아들며 경기 음악으로');
    finish(st, st.C(sL + 3.2));
  },
};

export const KNIGHT_EDITS = { knightFork };
