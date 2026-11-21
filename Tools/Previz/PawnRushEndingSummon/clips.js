// Ending summons previz - the clips: the common call, then the four plans of PAWN_RUSH_ENDING_SUMMON_STAGING_v0.1 §4.
// Every picture is one player's own third-person view (behind and above the piece). The plans differ in what happens
// to the pieces and to the hall, never in taking the camera away.
import { V, clamp, lerp, prog, smooth, easeOut, easeIn, fade, HEIGHT, NAME, GLYPH } from './kit.js';
import { HALL, COURSE } from './hall.js';

const KO = { w: '백', b: '흑' };
const nameOf = (team, kind) => `${KO[team]} ${NAME[kind]}`;
const LOOK = V(0, HALL.top + 1.4, HALL.circleZ);   // what a piece beside the altar looks at: the platform
const OUTSIDE = '알현실로! 시간이 다 되면 남은 사람은 모두 소환됩니다';
const INSIDE = '먼저 왔다 · 자리를 잡으세요 (발판은 소환 뒤에 켜집니다)';
const ME_R = { glyph: GLYPH.rook, name: '백 룩', rank: '9위' };
const ME_Q = { glyph: GLYPH.queen, name: '백 퀸', rank: '1위' };
const TEAM_COL = { w: 0x46d2ff, b: 0xff8a2a }, MINE_COL = { w: 0x9ff0ff, b: 0xffd08a };
const cx = lx => COURSE.x + lx;

/** The floor of hall B as the graybox has it: the altar top, the grand stair, the teams' side stairs. */
export function floorY(x, z) {
  const ax = Math.abs(x);
  if (ax <= 6 && z >= HALL.near && z <= HALL.back) return HALL.top;
  if (ax <= 6 && z >= HALL.altarFoot && z < HALL.near) return Math.min(9, Math.floor((z - HALL.altarFoot) / 0.75) + 1) * 0.25;
  if (z >= 52 && z <= 58 && ax > 6 && ax <= 12.75) return Math.min(9, Math.floor((12.75 - ax) / 0.75) + 1) * 0.25;
  return 0;
}
const yawTo = (x, z, t = LOOK) => Math.atan2(t.x - x, t.z - z);

// The summoned stand in their team's rows beside the altar, each on a 2 m floor square (the game: x ±12, z 48.5;
// the previz pulls them 2 m in, so the camera fits behind them).
const ROW = {
  w: [['knight', -10, 45], ['bishop', -10, 47], ['rook', -10, 49], ['king', -10, 51]],
  b: [['pawn', 8, 47], ['knight', 10, 49], ['bishop', 10, 51], ['rook', 12, 49], ['queen', 12, 51]],
};
// Those who came early keep their places: the leader and the second on the altar top, a pawn by the stair.
const EARLY = [['w', 'queen', -4, 53], ['b', 'king', 4, 53], ['w', 'pawn', -4, 41]];
const MY = { team: 'w', kind: 'rook', x: -10, z: 49 };   // "me" in the plans: the white rook, still on the course at the call

const cue = (s, list) => { for (const [a, b, text] of list) if (s >= a && s < b) return text; return null; };
function runPose(s, phase = 0, amp = 1) { const w = s * 11 + phase; return { bob: Math.abs(Math.sin(w)) * 0.1 * amp, roll: Math.sin(w) * 0.09 * amp, lean: 0.16 * amp }; }
function runner(st, team, kind, x, z, yaw, s, phase = 0, y = 0) { const r = runPose(s, phase); return st.put(team, kind, x, y + r.bob, z, yaw, r.lean, r.roll).position; }
function tagOf(team, kind, p, a = 1, mine = false) {
  return { pos: V(p.x, p.y + HEIGHT[kind] + 0.55, p.z), text: mine ? `나 · ${nameOf(team, kind)}` : nameOf(team, kind), team: mine ? 'me' : team, a };
}
function idle(st, team, kind, x, z, s, o = {}) {
  const y = floorY(x, z), roll = Math.sin(s * 1.7 + x * 0.7 + z) * 0.025 + (o.roll ?? 0);
  return st.put(team, kind, x + (o.dx ?? 0), y, z + (o.dz ?? 0), o.yaw ?? yawTo(x, z), o.lean ?? 0, roll).position;
}
// Who stands in the hall: the early ones always, the summoned rows when o.rows; o.skip lists the ones a clip moves itself.
function crowd(st, s, tags, o = {}) {
  const list = [...EARLY];
  if (o.rows) for (const team of ['w', 'b']) for (const [kind, x, z] of ROW[team]) list.push([team, kind, x, z]);
  for (const [team, kind, x, z] of list) {
    if (o.skip?.includes(team + kind)) continue;
    const p = idle(st, team, kind, x, z, s, o.wobble ? o.wobble(x, z) : {});
    tags.push(tagOf(team, kind, p, 1, team === MY.team && kind === MY.kind));
  }
}

// The game's camera: behind the piece on the line from what it looks at, a little above; kept inside the hall's walls.
function chase(p, target, o = {}) {
  const d = o.dist ?? 4.6, up = o.up ?? 2.2, k = o.k ?? 0.22, eye = o.eye ?? 0.6;
  const dx = p.x - target.x, dz = p.z - target.z, L = Math.hypot(dx, dz) || 1;
  let x = p.x + dx / L * d, z = p.z + dz / L * d; const y = p.y + up;
  if (o.walls !== false && y < HALL.height) { x = clamp(x, -14.2, 14.2); z = clamp(z, 1, 63.2); }
  return { p: V(x, y, z), l: V(lerp(p.x, target.x, k), lerp(p.y + eye, target.y, k), lerp(p.z, target.z, k)), fov: o.fov ?? 55 };
}
const mix = (a, b, k) => ({ p: a.p.clone().lerp(b.p, k), l: a.l.clone().lerp(b.l, k), fov: lerp(a.fov ?? 55, b.fov ?? 55, k) });
function shake(c, amp, s) { if (amp > 0.001) c.p.add(V(Math.sin(s * 61) * amp, Math.sin(s * 47 + 1) * amp, Math.sin(s * 53 + 2) * amp)); return c; }

// A piece set down: its square lights in the team's colour, a puff of dust.
function land(st, team, x, y0, z, a, mine = false) {
  const g = st.take('glow'); g.position.set(x, y0 + 0.035, z); g.scale.set(0.98, 0.98, 1);
  g.material.color.setHex(mine ? MINE_COL[team] : TEAM_COL[team]); g.material.opacity = (mine ? 0.34 : 0.16) + 0.75 * (1 - prog(a, 0, 0.7));
  if (a < 0.8) for (let k = 0; k < 2; k++) {
    const u = prog(a, 0, 0.8), d = st.take('dust');
    d.position.set(x + (k ? 0.5 : -0.5) * (0.4 + u), y0 + 0.25 + 0.3 * u, z + (k ? -0.3 : 0.3) * u); d.scale.setScalar(0.7 + 1.7 * u); d.material.opacity = 0.6 * (1 - u);
  }
}
// One hand setting pieces down one after another. steps: [{ team, kind, x, z, t, d, yaw }] - the piece rides down from
// above for d seconds, lands at t ('탁'), and the hand lets go and rises away.
function placing(st, i, steps, s, tags) {
  let held = null;
  for (const q of steps) {
    const a = s - q.t, d = q.d ?? 0.3, y0 = floorY(q.x, q.z), grip = HEIGHT[q.kind] * 0.92, yaw = q.yaw ?? 0;
    if (a < -d) continue;
    const face = yawTo(q.x, q.z);
    if (a < 0) {
      const k = smooth(prog(a, -d, 0)), gy = lerp(y0 + grip + (q.from ?? 26), y0 + grip, k);
      st.put(q.team, q.kind, q.x, gy - grip, q.z, face, 0, Math.sin(s * 8) * 0.1 * (1 - k));
      held = [V(q.x, gy, q.z), 0.95, yaw];
    } else {
      const p = st.put(q.team, q.kind, q.x, y0, q.z, face, 0, 0.12 * Math.exp(-7 * a) * Math.sin(a * 26)).position;
      tags.push(tagOf(q.team, q.kind, p, smooth(prog(a, 0, 0.15))));
      if (a < 0.24) { const u = prog(a, 0.05, 0.24); held = [V(q.x - Math.sin(yaw) * 1.5 * u, y0 + grip + 22 * easeIn(u), q.z - Math.cos(yaw) * 1.5 * u), lerp(0.95, 0.1, prog(a, 0, 0.05)), yaw]; }
      land(st, q.team, q.x, y0, q.z, a);
    }
  }
  if (held) st.setHand(i, held[0], { yaw: held[2], curl: held[1] });
}

// The standoff the game already has (ThroneContest.Waiting), dressed up: 3 · 2 · 1 with a bell and a ring of light
// from the crown each, then the platform lights. Returns the beat's zoom kick for the camera.
function countdown(st, s, s0, hud, crownY = 5.8) {
  let kick = 0;
  if (s >= s0 - 0.8 && s < s0 + 3) hud.contest = { state: '곧 발판이 켜집니다' };
  for (let i = 0; i < 3; i++) {
    const a = s - (s0 + i);
    if (a >= 0 && a < 1) hud.big = { text: String(3 - i), a: fade(a, 0, 1, 0.03, 0.16), scale: 1 + 0.4 * (1 - easeOut(prog(a, 0, 0.22))) };
    if (a >= 0 && a < 1.1) {
      const k = easeOut(prog(a, 0, 1.0)), r = st.take('ring');
      r.position.set(0, crownY, HALL.circleZ); r.scale.setScalar(1.8 + 11 * k); r.material.color.setHex(0xffe08a); r.material.opacity = 0.9 * (1 - k);
      kick = Math.max(kick, 1 - prog(a, 0, 0.35)); st.hall.setCircle(0.4 * (1 - prog(a, 0, 0.6)));
    }
  }
  const a = s - (s0 + 3);
  if (a >= 0) {
    st.hall.setCircle(1); st.hall.spot.intensity = 520 * smooth(prog(a, 0, 0.3));
    hud.big = { text: '시작!', a: fade(a, 0, 1.5, 0.03, 0.4), scale: 1 + 0.3 * (1 - easeOut(prog(a, 0, 0.25))), size: 112 };
    const k = easeOut(prog(a, 0, 0.8)), r = st.take('ring'); r.position.set(0, HALL.top + 0.09, HALL.circleZ); r.scale.setScalar(3.6 + 2.4 * k); r.material.color.setHex(0xffd36a); r.material.opacity = 0.95 * (1 - k);
    hud.contest = { state: '발판 켜짐 — 올라서라!', hot: true };
    const b = st.take('beam'); b.position.set(0, HALL.top + 9, HALL.circleZ); b.scale.set(HALL.circleR, 18, HALL.circleR); b.material.color.setHex(0xffd36a); b.material.opacity = 0.15 + 0.4 * (1 - prog(a, 0, 0.8));
    kick = Math.max(kick, 1 - prog(a, 0, 0.3));
  }
  return kick;
}
const bells = s0 => [[s0, s0 + 0.55, '댕! (종 · 3)'], [s0 + 1, s0 + 1.55, '댕! (종 · 2)'], [s0 + 2, s0 + 2.55, '댕! (종 · 1)'], [s0 + 3, s0 + 3.7, '빠밤! (발판이 켜진다)']];
function lowerCrown(st, s, s0, s1, y1 = 5.8) { const c = st.hall.crown; c.position.y = lerp(22, y1, easeOut(prog(s, s0, s1))); c.rotation.y = s * 0.6; }
const STRIP = (me, at) => ({ dots: [['w', 'queen', at.q ?? 0.9], ['b', 'king', at.k ?? 0.86], ['w', 'pawn', at.p ?? 0.82], ['b', 'pawn', 0.72], ['w', 'knight', 0.73],
  ['b', 'knight', 0.69], ['w', 'bishop', 0.67], ['b', 'bishop', 0.7], ['w', 'rook', at.r ?? 0.62], ['b', 'rook', 0.64], ['w', 'king', 0.58], ['b', 'queen', 0.61]]
  .map(([team, kind, p]) => ({ team, p, me: team + kind === me })) });
function endCard(t, t0, c) { return { ...c, a: smooth(prog(t, t0, t0 + 0.35)) }; }

// ---------------------------------------------------------------------------------------------------------- the call
const call = {
  title: { head: '공통 — 왕의 부름', sub: '1등이 엔드게임 코스를 지나면, 30초 뒤 남은 사람 모두를 알현실로 소환' },
  dur: 9.0, sheet: [0.6, 1.6, 2.4, 3.3, 4.2, 5.4, 6.3, 7.4, 8.6],
  frame(st, t) {
    const hud = {}, tags = []; let cam, focus;
    if (t < 2.9) {   // the leader on the course passes the exit arch
      const z = 17 + 6 * t, q = runner(st, 'w', 'queen', cx(0.8), z, 0, t);
      st.course.bar.rotation.y = t * 1.6;
      const pass = (30 - 17) / 6;
      if (t >= pass) { st.course.archFlash.material.opacity = 0.5 * (1 - prog(t, pass, pass + 0.35)); hud.band = { n: 30, sub: OUTSIDE }; }
      cam = chase(q, V(cx(0.8), 1.2, z + 20), { walls: false, dist: 4.6, up: 2.3, k: 0.2 }); focus = V(cx(0), 0, z);
      hud.me = ME_Q; hud.strip = STRIP('wqueen', { q: 0.9 + 0.02 * t });
      hud.view = '1등 화면 — 백 퀸';
      if (t >= pass) { hud.caption = '−30초: 1등이 출구 아치를 지나는 순간, 모든 사람 화면에 띠가 뜬다'; hud.sound = '댕— (종 한 번)'; }
    } else if (t < 5.9) {   // the leader inside: the hall wakes while she takes her place (fast forward)
      const k = (t - 2.9) / 3.0, x = lerp(-1.2, -4, smooth(k)), z = lerp(38, 53, k), r = runPose(t, 0, 0.6);
      st.put('w', 'queen', x, floorY(x, z) + r.bob, z, Math.atan2(-2.8, 15), r.lean, r.roll);
      const ySm = Math.abs(x) <= 6 ? clamp((z - HALL.altarFoot) / 6.75, 0, 1) * HALL.top : 0;
      cam = chase(V(x, ySm, z), V(-2, 4.5, 64), { dist: 4.4, up: 2.3, k: 0.25 }); focus = V(0, 0, 46);
      const lit = i => { const t0 = 3.05 + i * 0.42, v = prog(t, t0, t0 + 0.15); return v * (1 - 0.5 * Math.max(0, Math.sin((t - t0) * 40)) * (1 - prog(t, t0, t0 + 0.5))); };
      st.hall.setLamps(lit); let sum = 0; for (let i = 0; i < 6; i++) sum += lit(i);
      st.hemi.intensity = 0.14 + 0.36 * sum / 6; st.sun.intensity = 1.6;
      const kp = idle(st, 'b', 'king', 4, 53, t); tags.push(tagOf('b', 'king', kp));
      hud.band = { n: Math.max(6, Math.round(lerp(29, 6, prog(t, 3.0, 5.8)))), sub: INSIDE };
      hud.me = ME_Q; hud.strip = STRIP('wqueen', { q: 1, k: 0.97, p: 0.9 });
      hud.view = '먼저 온 사람 화면 — 백 퀸 (빨리 감기 ▸▸)';
      hud.caption = '−29 ~ −6초: 발판은 꺼진 채 — 홀의 불이 하나씩 켜지며 깨어난다';
      hud.sound = cue(t, [[3.0, 3.95, '음악이 점점 낮아진다'], [4.05, 4.65, '댕— (남은 20초)'], [5.25, 5.85, '댕— (남은 10초)']]);
    } else {   // a straggler on the course: the last three seconds
      const a = t - 5.9, z = -16 + 3.9 * a, me = runner(st, 'w', 'rook', cx(-1.6), z, 0, t, 0.5);
      const kn = runner(st, 'b', 'knight', cx(2.2), z + 1.4 + 0.3 * Math.sin(a * 2), 0, t, 2.1); tags.push(tagOf('b', 'knight', kn));
      st.course.bar.rotation.y = t * 1.6;
      cam = chase(me, V(cx(-0.6), 1.2, z + 20), { walls: false, dist: 4.6, up: 2.3, k: 0.2 }); focus = V(cx(0), 0, z);
      hud.band = { n: a < 1.03 ? 3 : a < 2.06 ? 2 : 1, sub: OUTSIDE, red: true }; hud.vig = 0.35 + 0.5 * prog(a, 0, 3.1);
      hud.me = ME_R; hud.strip = STRIP('wrook', { q: 1, k: 1, p: 0.97, r: 0.62 + 0.004 * a });
      hud.view = '늦은 사람 화면 — 백 룩 (아직 코스 위)';
      hud.caption = '−3 ~ 0초: 숫자가 빨개지고 화면 가장자리가 어두워진다 — 곧 끌려간다';
      hud.sound = '쿵… 쿵… (심장 박동)'; hud.black = prog(t, 8.75, 9.0);
    }
    return { cam, focus, hud, tags };
  },
};

// ------------------------------------------------------------------------------------------ ① the king's hand
// s = time from the summons. Hands over the white row come from the west wall, over the black row from the east.
const W_KNIGHT = { team: 'w', kind: 'knight', x: -10, z: 45, t: 2.25, d: 0.75, from: 30, yaw: Math.PI / 2 };
const HAND_W2 = [W_KNIGHT, { team: 'w', kind: 'bishop', x: -10, z: 47, t: 2.75, yaw: Math.PI / 2 }, { team: 'w', kind: 'king', x: -10, z: 51, t: 4.3, yaw: Math.PI }];
const HAND_B = [{ team: 'b', kind: 'pawn', x: 8, z: 47, t: 2.25, d: 0.75, from: 30 }, { team: 'b', kind: 'knight', x: 10, z: 49, t: 2.85 }, { team: 'b', kind: 'bishop', x: 10, z: 51, t: 3.35 },
  { team: 'b', kind: 'rook', x: 12, z: 49, t: 3.85 }, { team: 'b', kind: 'queen', x: 12, z: 51, t: 4.35 }].map(q => ({ ...q, yaw: -Math.PI / 2 }));
const hand = {
  title: { head: '① 왕의 손', sub: '마지막 수를 둔다 — 보이지 않던 손이 내 기물을 집어 판에 놓는다', tag: '추천' },
  dur: 13.0, sheet: [0.7, 1.9, 2.5, 3.6, 4.6, 5.2, 6.2, 9.9, 11.8],
  frame(st, t) {
    const s = t - 1.5, hud = {}, tags = []; let cam, focus;
    st.sky.material.uniforms.dark.value = 0.25 * prog(s, -1.5, -0.6);
    if (s < 1.3) {   // on the course: the hand's shadow, then the hand
      const lx = -1.6, top = HEIGHT.rook * 0.92;
      const z = s < 0 ? -10 + 3.9 * (s + 1.5) : -4.15 + 0.5 * easeOut(prog(s, 0, 0.25));
      st.course.bar.rotation.y = t * 1.6 + 5;
      const sh = st.course.shadow, u = smooth(prog(s, -1.5, 0.05)); sh.position.set(lerp(lx - 9, lx, u), 0.04, lerp(z + 24, z + 0.4, u)); sh.scale.set(2.8, 5.0, 1);
      sh.material.opacity = 0.42 * prog(s, -1.5, -1.0) * (1 - prog(s, 0.5, 1.2));
      let ry = 0, me;
      if (s < 0) { me = runner(st, 'w', 'rook', cx(lx), z, 0, t, 0.5); }
      else {
        const lift = 30 * easeIn(prog(s, 0.42, 1.3)); ry = lift;
        const gy = s < 0.3 ? lerp(top + 9, top, easeOut(prog(s, 0, 0.3))) : top + lift;
        me = st.put('w', 'rook', cx(lx), ry, z, 0, -0.15 * prog(s, 0, 0.2), Math.sin(s * 9) * 0.16 * prog(s, 0.42, 0.8)).position;
        st.setHand(0, V(cx(lx), gy, z), { yaw: Math.PI / 2, curl: lerp(0.15, 0.95, prog(s, 0.3, 0.42)) });
      }
      const kn = runner(st, 'b', 'knight', cx(2.2), (s < 0 ? -10 + 3.9 * (s + 1.5) : -4.15 + 3.9 * s) + 1.6, 0, t, 2.1); tags.push(tagOf('b', 'knight', kn));
      const base = chase(V(cx(lx), 0, z), V(cx(-0.6), 1.2, z + 20), { walls: false, dist: 4.6, up: 2.3, k: 0.2 });
      if (s > 0.42) { base.p.y += ry * 0.72; base.l = V(cx(lx), ry + 0.6, z); base.fov = 55 + 8 * prog(s, 0.6, 1.3); }
      cam = base; focus = V(cx(0), 0, z);
      if (s < 0) { hud.band = { n: s < -1 ? 2 : 1, sub: OUTSIDE, red: true }; hud.vig = 0.75; }
      hud.me = ME_R; hud.strip = STRIP('wrook', { q: 1, k: 1, p: 0.97 });
      hud.streaks = prog(s, 0.75, 1.15); hud.white = prog(s, 1.18, 1.3);
      hud.view = '늦은 사람 화면 — 백 룩';
      hud.caption = s < 0 ? '−1.5초: 거대한 손의 그림자가 코스를 덮는다 (예고)' : '0초: 하늘에서 손이 내려와 내 기물을 집어 든다 — 카메라는 내 기물을 따라간다';
      hud.sound = cue(s, [[-1.5, -0.05, '휘이잉— (바람)'], [0.3, 0.85, '탁 (집는 소리)'], [0.85, 1.3, '슈우욱—']]);
    } else if (s < 2.7) {   // the leader's screen: the roof opens and hands bring the summoned in
      const open = smooth(prog(s, 1.4, 2.25)); st.setRoof(open);
      st.hemi.intensity = 0.45 + 0.2 * open; st.sun.intensity = 2.6 - 0.8 * open;
      crowd(st, s, tags, { skip: ['wqueen'] });
      const q = idle(st, 'w', 'queen', -4, 53, s, { yaw: Math.PI - 0.3 }); tags.push(tagOf('w', 'queen', q, 1, true));
      placing(st, 1, HAND_W2, s, tags); placing(st, 2, HAND_B, s, tags);
      cam = chase(V(-4, HALL.top, 53), V(-1, 12, 30), { dist: 5.0, up: 3.2, k: 0.25, eye: 1.1, fov: 70 }); focus = V(0, 0, 40);
      hud.me = ME_Q; hud.white = 1 - prog(s, 1.3, 1.42);
      hud.view = '먼저 온 사람 화면 — 백 퀸';
      hud.caption = '먼저 온 사람: 지붕이 열리고, 손들이 소환된 기물을 팀 자리에 내려놓는다';
      hud.sound = cue(s, [[1.35, 2.2, '쿠구궁— (지붕이 열린다)'], [2.2, 2.7, '탁 · 탁 (말 놓는 소리)']]);
    } else {   // back on my screen: down through the open roof onto my square, the crown, 3 · 2 · 1
      st.setRoof(1); st.hemi.intensity = 0.6; st.sun.intensity = 1.8;
      crowd(st, s, tags, { skip: ['wrook'] });
      placing(st, 1, HAND_W2, s, tags); placing(st, 2, HAND_B, s, tags);
      // the hand lets go a little above the board and I drop the last two metres onto my square ('탁')
      const top = HEIGHT.rook * 0.92, k = smooth(prog(s, 2.7, 3.6)), a = s - 3.6, LET = 3.45, yLet = 30 * (1 - smooth(prog(LET, 2.7, 3.6)));
      const y = s < LET ? 30 * (1 - k) : yLet * (1 - easeIn(prog(s, LET, 3.6)));
      const face = yawTo(MY.x, MY.z);
      const me = st.put('w', 'rook', MY.x, y, MY.z, face, a > 0 && s > 8.2 ? 0.18 : 0, a < 0 ? Math.sin(s * 8) * 0.12 * (1 - k) : 0.12 * Math.exp(-7 * a) * Math.sin(a * 26)).position;
      tags.push(tagOf('w', 'rook', me, prog(s, 3.6, 3.75), true));
      if (s < LET) st.setHand(0, V(MY.x, y + top, MY.z), { pitch: 1.45, yaw: Math.PI / 2, curl: 0.95 });
      else if (s < LET + 0.4) { const u = prog(s, LET, LET + 0.4); st.setHand(0, V(MY.x - 2 * u, yLet + top + 1.2 * u + 22 * easeIn(u), MY.z), { pitch: 1.45, yaw: Math.PI / 2, curl: lerp(0.95, 0.1, prog(s, LET, LET + 0.07)) }); }
      if (a >= 0) land(st, 'w', MY.x, 0, MY.z, a, true);
      lowerCrown(st, s, 4.0, 5.0);
      const hi = { p: V(MY.x - 5.2, y + 4.0, MY.z - 3.8), l: V(MY.x + 3, Math.max(0, y - 9), MY.z + 2.5), fov: 60 };
      if (hi.p.y < HALL.height) hi.p.x = Math.max(hi.p.x, -14.2);
      const kick = countdown(st, s, 5.2, hud);
      const lo = chase(me, LOOK, { dist: lerp(4.3, 3.9, prog(s, 4.0, 5.2)), fov: 55 - 5 * kick });
      cam = shake(mix(hi, lo, smooth(prog(s, 3.5, 4.1))), 0.1 * Math.max(0, 1 - Math.abs(a) * 5), s); focus = V(0, 0, 44);
      hud.me = ME_R;
      hud.view = '늦은 사람 화면 — 백 룩';
      hud.caption = s < 4.0 ? '열린 지붕으로 내려와 내 칸에 \'탁\' — 내 칸이 팀 색으로 빛나고, 손들이 다른 기물도 놓는다'
        : s < 5.2 ? '천장에서 왕관이 발판 위로 내려온다 — 음악이 끊긴다' : '3 · 2 · 1 종소리마다 왕관에서 빛 고리 → 발판이 켜지면 시작';
      hud.sound = cue(s, [[2.7, 3.5, '휘— (내려간다)'], [3.6, 4.0, '탁! (말 놓는 소리)'], [4.0, 5.2, '(음악이 끊긴다) 쿵… 쿵…'], ...bells(5.2)]);
      hud.card = endCard(t, 10.4, CARD.hand);
    }
    return { cam, focus, hud, tags };
  },
};

// ------------------------------------------------------------------------------------------ ② the procession
const LINE = { w: [['king', 42], ['knight', 40.4], ['bishop', 38.8], ['rook', 37.2]], b: [['queen', 42], ['rook', 40.4], ['bishop', 38.8], ['knight', 37.2], ['pawn', 35.6]] };
const LX = { w: -3.2, b: 3.2 }, MARCH = 44, M0 = 2.1, M1 = 5.4;
const marchZ = (z1, s) => z1 - MARCH * (1 - smooth(prog(s, M0, M1)));
const procession = {
  title: { head: '② 알현 행렬', sub: '문이 열리고 나팔 — 소환된 사람은 큰 문 앞에서 융단을 따라 들어간다' },
  dur: 12.9, sheet: [0.4, 1.4, 2.0, 3.2, 4.4, 5.9, 7.2, 9.8, 11.6],
  frame(st, s) {
    const hud = {}, tags = [];
    const open = smooth(prog(s, 1.0, 2.1)); st.hall.setDoors(open);
    const outside = s < 3.0;
    if (outside) { st.sky.material.uniforms.dark.value = 0.55; st.hemi.intensity = 0.35; st.sun.intensity = 1.1; } else st.hemi.intensity = 0.5;
    if (open > 0) { const f = st.take('flare'); f.position.set(0, 5, 1.2); f.scale.setScalar(4 + 14 * open); f.material.color.setHex(0xffd8a0); f.material.opacity = 0.55 * open * (1 - prog(s, 3.5, 4.5)); }
    const sp = 6 * MARCH * prog(s, M0, M1) * (1 - prog(s, M0, M1)) / (M1 - M0);   // the march's speed
    if (s > M0 && s < M1) { const g = st.take('glow'); g.position.set(0, 0.045, 22.3); g.scale.set(2, 21.7, 1); g.material.color.setHex(0xffc94a); g.material.opacity = 0.3 * Math.sin(Math.PI * prog(s, M0, M1)); }
    let me;
    for (const team of ['w', 'b']) LINE[team].forEach(([kind, z1], i) => {
      const z = marchZ(z1, s), x = LX[team], bob = Math.abs(Math.sin(s * 8 + i)) * 0.05 * (sp > 1 ? 1 : 0);
      const yaw = s < M1 + 0.4 ? 0 : lerp(0, yawTo(x, z1), smooth(prog(s, M1 + 0.4, M1 + 1.0)));
      const p = st.put(team, kind, x, bob, z, yaw, -0.012 * sp, 0).position;
      const mine = team === MY.team && kind === MY.kind; if (mine) me = p;
      if (!outside || mine) tags.push(tagOf(team, kind, p, 1, mine));
      if (sp > 1) { const tr = st.take('trail'), L = 0.2 * sp; tr.position.set(x, 0.3, z - L / 2); tr.scale.set(1, L, 1); tr.material.color.setHex(TEAM_COL[team]); tr.material.opacity = Math.min(0.8, sp / 16); }
    });
    // those who came early watch from the altar top and by a column
    for (const [team, kind, x, z, yaw] of [['w', 'queen', -4, 53, null], ['b', 'king', 4, 53, null], ['w', 'pawn', -6, 27, Math.PI / 2]]) {
      const p = idle(st, team, kind, x, z, s, yaw == null ? {} : { yaw }); if (!outside) tags.push(tagOf(team, kind, p));
    }
    lowerCrown(st, s, 5.4, 6.4);
    const kick = countdown(st, s, 6.5, hud);
    const c0 = chase(me, V(-1.5, 3, me.z + 24), { walls: me.z - 4.6 > 1.2, dist: 4.6, up: 2.4, k: 0.22 }), c1 = chase(me, LOOK, { dist: 4.4, up: 2.5, fov: 55 - 5 * kick });
    const cam = mix(c0, c1, smooth(prog(s, M1 - 0.6, M1 + 0.5)));
    hud.me = ME_R; hud.black = 1 - prog(s, 0.4, 0.9);
    hud.view = '소환된 사람 화면 — 백 룩 (큰 문 앞으로 소환)';
    hud.caption = s < M0 ? '0 ~ 2초: 소환된 사람은 큰 문 앞에 두 줄로 선다 — 나팔, 문이 열린다'
      : s < M1 ? '2 ~ 5초: 융단이 빛나며 두 줄이 왕좌 앞까지 미끄러져 들어간다 (먼저 온 사람은 이미 안에)'
      : s < 6.5 ? '왕좌 앞: 천장에서 왕관이 발판 위로 내려온다' : '3 · 2 · 1 → 발판이 켜지면 시작';
    hud.sound = cue(s, [[0, 1.0, '빠바밤— (나팔)'], [1.0, 3.6, '전령: “백의 군대, 흑의 군대 — 국왕 앞으로!”'], [3.6, M1, '쿵 · 쿵 · 쿵 (북 · 행진)'], [M1, 6.4, '둥— (북)'], ...bells(6.5)]);
    hud.card = endCard(s, 10.3, CARD.procession);
    return { cam, focus: V(0, 0, outside ? 14 : 40), hud, tags };
  },
};

// ------------------------------------------------------------------------------------------ ③ the board is set
const ORDER = ['pawn', 'knight', 'bishop', 'rook', 'queen', 'king'], DROP0 = 0.6, GAP = 0.42, HOVER = 14, FALL = 0.32;
const dropAt = kind => DROP0 + ORDER.indexOf(kind) * GAP;
const MY3 = { team: 'b', kind: 'pawn', x: 8, z: 47 }, ME_P = { glyph: GLYPH.pawn, name: '흑 폰', rank: '11위' };
const setup = {
  title: { head: '③ 판이 차려진다', sub: '천장 아래 떠 있다가 폰 → 킹 순서로 자기 칸에 \'쿵\'' },
  dur: 10.8, sheet: [0.35, 0.75, 1.25, 1.6, 1.95, 2.75, 3.9, 7.6, 9.8],
  frame(st, s) {
    const hud = {}, tags = [];
    st.hemi.intensity = 0.5;
    crowd(st, s, tags);   // the early ones
    let me, myY = HOVER;
    for (const team of ['w', 'b']) for (const [kind, x, z] of ROW[team]) {
      const tl = dropAt(kind), a = s - tl, mine = team === MY3.team && kind === MY3.kind;
      let y = HOVER + Math.sin(s * 2 + x + z) * 0.12;
      if (a >= -FALL) y = a >= 0 ? 0 : HOVER * (1 - easeIn(prog(a, -FALL, 0)));
      const p = st.put(team, kind, x, y, z, yawTo(x, z), 0, a >= 0 ? 0.14 * Math.exp(-7 * a) * Math.sin(a * 24) : Math.sin(s * 2.3 + z) * 0.05).position;
      if (mine) { me = p; myY = y; }
      if (a >= 0) { land(st, team, x, 0, z, a, mine); tags.push(tagOf(team, kind, p, smooth(prog(a, 0, 0.15)), mine)); }
      else {   // its square waits, lit
        const g = st.take('glow'); g.position.set(x, 0.035, z); g.scale.set(0.98, 0.98, 1); g.material.color.setHex(mine ? MINE_COL[team] : TEAM_COL[team]);
        g.material.opacity = (mine ? 0.6 : 0.3) * (0.75 + 0.25 * Math.sin(s * 9)) * prog(s, 0.15, 0.4);
        if (mine) tags.push(tagOf(team, kind, p, 1, true));
      }
    }
    lowerCrown(st, s, 3.0, 4.0);
    const kick = countdown(st, s, 4.4, hud);
    const myLand = dropAt(MY3.kind), a = s - myLand;
    const hi = chase(V(MY3.x, HOVER, MY3.z), LOOK, { dist: 4.6, up: 3.4, k: 0.12, eye: -6, fov: 60 });
    // after landing: look up a little while the others hang overhead and come down, then at the platform
    const up = 1 - smooth(prog(s, 2.9, 3.6));
    const lo = chase(me, LOOK, { fov: lerp(55 - 5 * kick, 65, up) }); lo.l.y += 2.2 * up;
    let cam;
    if (a < -FALL) cam = hi;
    else {   // I fall and the camera dives after me, a beat behind
      const f = easeIn(prog(a, -FALL, 0.1)), c = chase(V(MY3.x, Math.max(myY, HOVER * (1 - f)), MY3.z), LOOK, { dist: 4.6, up: lerp(3.4, 2.2, f), k: 0.12, eye: lerp(-6, 0.6, f), fov: 60 });
      cam = mix(mix(hi, c, prog(a, -FALL, -FALL + 0.1)), lo, smooth(prog(a, 0.05, 0.6)));
    }
    shake(cam, 0.14 * Math.max(0, 1 - Math.abs(a) * 4) * (a > 0 ? 1 : 0), s);
    for (const kind of ORDER) { const b = s - dropAt(kind); if (b > 0 && b < 0.25 && kind !== MY3.kind) shake(cam, 0.05 * (1 - b * 4), s); }
    hud.me = ME_P; hud.white = 1 - prog(s, 0.08, 0.35);
    hud.view = s < myLand ? '소환된 사람 화면 — 흑 폰 (천장 아래, 폰이 먼저)' : '소환된 사람 화면 — 흑 폰';
    hud.caption = s < 3.0 ? '폰 → 나이트 → 비숍 → 룩 → 퀸 → 킹 순서로 떨어진다 — 칸은 미리 팀 색으로 빛난다' : s < 4.4 ? '왕관이 발판 위로 내려온다' : '3 · 2 · 1 → 발판이 켜지면 시작';
    hud.sound = cue(s, [[0, 0.4, '쿵 (번쩍)'], ...ORDER.map(k => [dropAt(k) - 0.04, dropAt(k) + 0.36, k === MY3.kind ? `쿵! ${NAME[k]} — 나` : `쿵! ${NAME[k]}`]), ...bells(4.4)]);
    hud.card = endCard(s, 8.2, CARD.setup);
    return { cam, focus: V(0, 0, 44), hud, tags };
  },
};

// ------------------------------------------------------------------------------------------ ④ the bell of the decider
const IMPACT = 1.0;
const crown = {
  title: { head: '④ 결전의 종', sub: '번쩍이면 이미 팀 자리 — 거대한 왕관이 발판에 \'쾅\'' },
  dur: 8.2, sheet: [0.25, 0.55, 0.85, 1.04, 1.3, 2.0, 3.3, 5.1, 7.0],
  frame(st, s) {
    const hud = {}, tags = [], a = s - IMPACT;
    st.hemi.intensity = 0.5; st.hall.crown.visible = false;
    // the shockwave: those near the altar reel and slide back, the far ones only sway
    const wobble = (x, z) => {
      if (a < 0) return {};
      const d = Math.hypot(x, z - HALL.circleZ), A = 0.38 * Math.max(0, 1 - d / 16), push = 0.7 * Math.max(0, 1 - d / 7) * easeOut(prog(a, 0, 0.3));
      return { roll: A * Math.exp(-4.5 * a) * Math.sin(a * 20), dx: x / (d || 1) * push, dz: (z - HALL.circleZ) / (d || 1) * push, lean: -A * 0.6 * Math.exp(-5 * a) };
    };
    crowd(st, s, tags, { rows: true, wobble });
    const c = st.bigCrown; c.visible = true;
    const seat = HALL.top + 0.6, y = a < 0 ? lerp(22, seat, easeIn(prog(s, 0.6, IMPACT))) : lerp(seat, 6.6, easeOut(prog(a, 0.15, 0.75))) + Math.sin(s * 2) * 0.12 * prog(a, 0.75, 1.2);
    c.position.set(0, y, HALL.circleZ); c.rotation.y = s * 0.5; c.visible = s >= 0.6;
    if (a >= 0) {
      const top = st.take('ring'), k1 = easeOut(prog(a, 0, 0.35)); top.position.set(0, HALL.top + 0.1, HALL.circleZ); top.scale.setScalar(3 + 3.6 * k1); top.material.color.setHex(0xfff0c0); top.material.opacity = 0.95 * (1 - prog(a, 0.1, 0.45));
      const fl = st.take('ring'), k2 = easeOut(prog(a, 0.08, 0.7)); fl.position.set(0, 0.07, HALL.circleZ); fl.scale.setScalar(6.5 + 22 * k2); fl.material.color.setHex(0xffe0a0); fl.material.opacity = 0.8 * (1 - prog(a, 0.15, 0.7)) * (a > 0.08 ? 1 : 0);
      for (let i = 0; i < 10; i++) {
        const ang = i / 10 * Math.PI * 2, u = prog(a, 0, 1.1), d = st.take('dust'), r = 3.6 + 3 * u;
        d.position.set(Math.cos(ang) * r, HALL.top + 0.4 + 0.5 * u, HALL.circleZ + Math.sin(ang) * r); d.scale.setScalar(1.2 + 2.6 * u); d.material.opacity = 0.7 * (1 - u);
      }
      const f = st.take('flare'); f.position.set(0, y + 1.2, HALL.circleZ); f.scale.setScalar(6 + 10 * (1 - prog(a, 0, 0.5))); f.material.color.setHex(0xffd36a); f.material.opacity = 0.35 + 0.5 * (1 - prog(a, 0, 0.6));
    }
    const kick = countdown(st, s, 1.8, hud, 7.4);
    const me = st.pieces.w.rook.position.clone();
    const cam = chase(me, LOOK, { fov: 55 - 5 * kick }); cam.l.y += 0.8;
    const off = cam.p.clone().sub(me), r = Math.hypot(off.x, off.z), ang = Math.atan2(off.x, off.z) + lerp(1.25, 0, smooth(prog(s, 0.05, 0.8)));
    cam.p.set(clamp(me.x + Math.sin(ang) * r, -14.2, 14.2), cam.p.y, me.z + Math.cos(ang) * r);
    shake(cam, a >= 0 ? 0.32 * Math.exp(-6 * a) : 0, s);
    hud.me = ME_R; hud.white = 1 - prog(s, 0.05, 0.4);
    hud.view = '소환된 사람 화면 — 백 룩 (번쩍 → 이미 팀 자리)';
    hud.caption = s < 1.8 ? '천장에서 거대한 왕관이 떨어진다 — 충격파에 가까운 기물이 휘청인다 (그림만, 물리는 그대로)' : '종소리마다 왕관에서 빛 고리 — 3 · 2 · 1 → 발판이 켜지면 시작';
    hud.sound = cue(s, [[0, 0.5, '번쩍'], [0.6, IMPACT, '휘이이—'], [IMPACT, 1.7, '쾅!!'], ...bells(1.8)]);
    hud.card = endCard(s, 5.6, CARD.crown);
    return { cam, focus: V(0, 0, 44), hud, tags };
  },
};

// ------------------------------------------------------------------------------------------ the end cards
const CARD = {
  hand: { title: '① 왕의 손 — 마지막 수를 둔다', tag: '추천', line: '보이지 않던 손이 내 기물을 집어 들고, 열린 지붕으로 내려와 내 칸에 \'탁\' 놓는다.',
    rows: [['좋은 점', ['체스는 손이 말을 옮기는 게임 — \'기물이 된 우리\'가 가장 잘 보인다.', '궁극기의 \'하늘에서 우리 손\'과 같은 그림 언어로 이어진다.']],
      ['걱정', ['손 모델과 쥐기 · 놓기 동작을 새로 만들어야 한다.', '12명을 하나씩 놓으면 길다 → 손 여러 개가 함께 놓는다.']],
      ['만들 것', ['손 모델, 열리는 지붕, 내 기물을 따라가는 카메라, 놓는 순서표']],
      ['길이', ['소환 장면 5.2초 + 3 · 2 · 1 (지금도 있는 대기 3초)']]] },
  procession: { title: '② 알현 행렬 — 문이 열리고 나팔', line: '소환된 사람은 큰 문 앞에 두 줄로 서고, 문이 열리면 융단을 따라 왕좌 앞까지 들어간다.',
    rows: [['좋은 점', ['알현실이라는 장소가 가장 살아난다 — 긴 홀(B)의 크기가 그대로 느껴진다.', '나팔과 전령 목소리로 \'끝판\'이라는 게 분명하다.']],
      ['걱정', ['팀 자리가 융단 양옆 두 줄로 바뀐다 (지금은 홀 양옆).', '짧은 홀(A)에서는 행진이 짧다 · 네 안 중 가장 길다.']],
      ['만들 것', ['큰 문과 여는 동작, 융단 위로 미끄러지는 행진(빛 꼬리), 나팔 · 전령 소리']],
      ['길이', ['소환 장면 6.5초 + 3 · 2 · 1']]] },
  setup: { title: '③ 판이 차려진다 — 천장에서 자기 칸으로', line: '소환된 기물이 천장 아래 떠 있다가 폰 → 킹 순서로 자기 칸에 \'쿵\' 떨어진다.',
    rows: [['좋은 점', ['손 모델이 필요 없다 — 가장 싸다.', '판에 말을 세팅하는 순서라 체스 팬이 바로 알아본다 · 내 칸이 미리 빛난다.']],
      ['걱정', ['①보다 덜 특별하다 · 천장 아래 떠 있는 모습이 어색할 수 있다.']],
      ['만들 것', ['천장 아래 대기 자리, 떨어지는 동작과 먼지, 칸 빛, 이름표']],
      ['길이', ['소환 장면 4.4초 + 3 · 2 · 1']]] },
  crown: { title: '④ 결전의 종 — 왕관이 떨어진다', line: '번쩍이면 이미 팀 자리에 서 있고, 거대한 왕관이 발판에 \'쾅\' 떨어진다.',
    rows: [['좋은 점', ['가장 짧다 (1.8초) — 왕관이 \'여기를 차지하라\'를 바로 보여 준다.', '다른 안의 끝에 붙일 수 있다 (예: ①의 끝에 ④).']],
      ['걱정', ['\'끌려간다\'는 느낌은 없다 — 화면이 하얗게 바뀌면 끝.']],
      ['만들 것', ['거대 왕관과 떨어지는 동작, 종마다 빛 고리', '충격파 고리와 휘청임 (그림만, 물리는 그대로)']],
      ['길이', ['소환 장면 1.8초 + 3 · 2 · 1']]] },
};

export const CLIPS = { call, hand, procession, setup, crown };
// shared with the other plans' files (king.js)
export { LOOK, OUTSIDE, INSIDE, ME_R, ME_Q, TEAM_COL, cx, yawTo, cue, runPose, runner, tagOf, chase, mix, shake, land, countdown, bells, lowerCrown, STRIP, endCard };
