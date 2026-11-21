// Ending summons previz - ⑤ the king calls (the user's plan, 2026-11-22): the hall's king - no player's piece in this mode -
// stands on his throne's dais and strikes his staff on the floor ('탁!'); his eyes light, the skill-cast aurora wraps him,
// pillars of light rise beside him, one for each player of each team, and everyone is drawn into them. The summoned see it
// from behind their own piece: caught by a pillar on the course, sucked up, a rush of light, down their pillar by the king.
import { THREE, V, TEX, MAT, lerp, prog, smooth, easeOut, easeIn, fade, HEIGHT, makePiece, makeCrown } from './kit.js';
import { HALL } from './hall.js';
import { LOOK, INSIDE, ME_R, ME_Q, TEAM_COL, cx, yawTo, cue, runner, tagOf, chase, mix, shake, countdown, bells, lowerCrown, endCard } from './clips.js';

const TEAL = 0x1fb99a, TEAL2 = 0x3fe0bf, PALE = 0xc8fff0;
const KING = { x: 0, y: HALL.top + 0.75, z: 58.6, s: 1.6 };   // standing on the throne's seat, facing the hall
const HEAD = V(KING.x, KING.y + HEIGHT.king * KING.s, KING.z);
// the pillars: a line on each side of the platform, from the king's side outwards; one per player of the team
const PZ = [51.6, 53.1, 54.6, 56.1, 57.6, 59.1], PX = { w: -4.75, b: 4.75 };
// Six a team and no king among them (the king is the hall's). 'hall': already inside at the call, where it stood.
const TEAM = {
  w: [['pawn', 'hall', -6, 38], ['pawn2', 'course'], ['knight', 'course'], ['rook', 'me'], ['bishop', 'course'], ['queen', 'hall', -3, 41]],
  b: [['pawn', 'course'], ['pawn2', 'course'], ['knight', 'course'], ['rook', 'course'], ['bishop', 'hall', 3.5, 43], ['queen', 'course']],
};
const PULL = 1.6;   // everyone is taken at once; those inside arrive first, the course's runners come down their pillars after
const ARR = { w: { pawn: 2.1, pawn2: 3.15, knight: 3.3, rook: 3.0, bishop: 3.45, queen: 2.1 }, b: { pawn: 3.1, pawn2: 3.25, knight: 3.4, rook: 3.55, bishop: 2.1, queen: 3.7 } };
const ME = { x: PX.w, z: PZ[3] };
const base = k => k.replace('2', '');

function obj(st, team, kind) {
  if (kind !== 'pawn2') { const p = st.pieces[team][kind]; p.visible = true; return p; }
  return st.extra(team + 'pawn2', () => { const p = makePiece('pawn', team === 'w' ? 'ally' : 'enemy', null, { cute: true }); p.rotation.order = 'YXZ'; return p; });
}
function place(st, team, kind, x, y, z, yaw = 0, lean = 0, roll = 0) { const p = obj(st, team, kind); p.scale.setScalar(1); p.position.set(x, y, z); p.rotation.set(lean, yaw, roll); return p.position; }
const glint = (st, p, size, op, color = TEAL2) => { const f = st.take('flare'); f.position.copy(p); f.scale.setScalar(size); f.material.color.setHex(color); f.material.opacity = op; };

// ---------------------------------------------------------------- the king, his staff, his eyes, the aurora
function makeKing() {
  const ivory = new THREE.MeshPhysicalMaterial({ color: 0xfff3dc, roughness: 0.3, clearcoat: 0.8, clearcoatRoughness: 0.2, emissive: 0x3a2a10, emissiveIntensity: 0.25 });
  const g = makePiece('king', null, ivory, { cute: true }); g.rotation.order = 'YXZ'; g.scale.setScalar(KING.s);
  g.traverse(o => { if (o.isMesh && o.material === ivory && o.geometry.type === 'BoxGeometry') o.material = MAT.gold; });
  const eyes = [], glints = [];
  g.traverse(o => { if (o.isMesh && o.geometry.parameters?.radius === 0.06) { o.material = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: TEAL2, emissiveIntensity: 0, roughness: 0.3 }); eyes.push(o); } });
  for (const e of eyes) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.flash, color: TEAL2, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
    sp.position.copy(e.position).add(V(0, 0, 0.06)); sp.scale.setScalar(0.42); g.add(sp); glints.push(sp);
  }
  g.userData = { eyes, glints }; return g;
}
function makeStaff() {   // the Queen of the Hill king's sceptre, grown to his height: a turned gold rod, a crown, a light at its top
  const g = new THREE.Group();
  const rod = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.07, 2.5, 16), MAT.gold); rod.position.y = 1.25; g.add(rod);
  for (const y of [0.12, 0.8, 1.6, 2.3]) { const b = new THREE.Mesh(new THREE.TorusGeometry(0.085, 0.028, 8, 20), MAT.gold); b.rotation.x = Math.PI / 2; b.position.y = y; g.add(b); }
  const crown = makeCrown(0.17); crown.position.y = 2.58; g.add(crown);
  const orb = new THREE.Mesh(new THREE.SphereGeometry(0.1, 16, 12), new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: TEAL2, emissiveIntensity: 0.3 })); orb.position.y = 2.74; g.add(orb);
  g.traverse(o => { if (o.isMesh) o.castShadow = true; }); g.userData.orb = orb; return g;
}
// the skill-cast aurora (the ultimates' cast: PawnRushSkillFx.Ult.cs, previz ultcast.js) - three curtains turning round him
const AUR_VS = `uniform float t; varying vec2 vUv; varying vec3 vN, vV; void main(){ vUv = uv; vec3 p = position; float a = atan(p.z, p.x);
  p.xz *= 1.0 + 0.1 * sin(uv.y * 6.0 - t * 2.6 + a * 2.0); vec4 mv = modelViewMatrix * vec4(p, 1.0);
  vN = normalize(normalMatrix * normal); vV = normalize(-mv.xyz); gl_Position = projectionMatrix * mv; }`;
const AUR_FS = `uniform float t, k; uniform vec3 col, hi; varying vec2 vUv; varying vec3 vN, vV;
  void main(){ float y = vUv.y, prof = smoothstep(0.0, 0.12, y) * (1.0 - smoothstep(0.2, 1.0, y));
    float r = 0.5 + 0.5 * sin(vUv.x * 52.0 + t * 2.1 + 2.4 * sin(y * 4.0 + t * 1.3)), rays = 0.06 + pow(r, 6.0);
    float ends = smoothstep(0.0, 0.14, vUv.x) * (1.0 - smoothstep(0.86, 1.0, vUv.x));
    float rim = pow(1.0 - abs(dot(normalize(vN), normalize(vV))), 1.6);
    gl_FragColor = vec4(mix(hi, col, smoothstep(0.0, 0.6, y)) * prof * rays * ends * (0.12 + 0.88 * rim) * k, 1.0); }`;
const AUR = [[0.72, 2.4, 0.9, 1.3], [0.95, 3.2, -0.6, 1.1], [1.18, 4.0, 0.45, 1.5]];
const auraOn = s => fade(s, 0.02, 4.0, 0.3, 0.6);
function aurora(st, s) {
  const on = auraOn(s); if (on <= 0) return;
  const pulse = (s >= 0 ? 1.4 * Math.exp(-s / 0.16) : 0) + 0.25 * Math.max(0, Math.sin(s * 6));
  AUR.forEach(([r, h, w, arc], i) => {
    const m = st.extra('aur' + i, () => new THREE.Mesh(new THREE.CylinderGeometry(r * KING.s, r * 1.25 * KING.s, h * KING.s, 64, 8, true, 0, arc * Math.PI),
      new THREE.ShaderMaterial({ vertexShader: AUR_VS, fragmentShader: AUR_FS, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide,
        uniforms: { t: { value: 0 }, k: { value: 0 }, col: { value: new THREE.Color(TEAL) }, hi: { value: new THREE.Color(PALE) } } })));
    const sc = 0.6 + 0.4 * easeOut(prog(s, 0, 0.35));
    m.position.set(KING.x, KING.y + h * KING.s / 2 * sc - 0.2, KING.z); m.scale.setScalar(sc); m.rotation.y = i * 2.1 + w * s;
    m.material.uniforms.t.value = s + i * 3.7; m.material.uniforms.k.value = on * (0.9 + 0.5 * pulse);
  });
  for (let i = 0; i < 12; i++) {   // teal flames rising round him
    const u = (s * 1.3 + i / 12) % 1, th = i / 12 * Math.PI * 2 + s * 2;
    glint(st, V(KING.x + Math.cos(th) * 1.3 * (1 - u), KING.y + 0.2 + u * 4.4, KING.z + Math.sin(th) * 1.3 * (1 - u)), 0.9 + 1.5 * u, 0.32 * on * (1 - u), i % 3 ? TEAL2 : PALE);
  }
}
// a square of light running out over the altar from where the staff struck
function squareLine(st, key) {
  return st.extra(key, () => {
    const g = new THREE.Group(), mat = new THREE.MeshBasicMaterial({ color: TEAL2, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false });
    const geo = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2); for (let i = 0; i < 4; i++) g.add(new THREE.Mesh(geo, mat)); g.userData.mat = mat; return g;
  });
}
function wave(st, key, c, a, d, h0, h1, color, w) {
  const u = a / d; if (u < 0 || u > 1) return;
  const g = squareLine(st, key), h = lerp(h0, h1, easeOut(u)), L = 2 * h + w, [p, q, r, t] = g.children;
  p.position.set(h, 0, 0); p.scale.set(w, 1, L); q.position.set(-h, 0, 0); q.scale.set(w, 1, L); r.position.set(0, 0, h); r.scale.set(L, 1, w); t.position.set(0, 0, -h); t.scale.set(L, 1, w);
  g.position.copy(c); g.userData.mat.color.setHex(color).multiplyScalar(1.5 * Math.pow(1 - u, 1.3));
}
function strike(st, s, s0, color) {
  const a = s - s0; if (a < 0 || a > 1.3) return 0;
  const foot = V(KING.x - 0.78, KING.y + 0.15, KING.z - 0.2), c = V(KING.x, HALL.top + 0.06, KING.z - 0.8);
  wave(st, 'wave0', c, a, 0.9, 1.2, 9, color, 0.28); wave(st, 'wave1', c, a - 0.08, 1.0, 1.2, 7, PALE, 0.12);
  if (a < 0.35) glint(st, foot, 1 + 5 * (1 - a / 0.35), 0.9 * (1 - a / 0.35), 0xffffff);
  if (a < 0.8) for (let i = 0; i < 6; i++) { const u = a / 0.8, th = i / 6 * Math.PI * 2, d = st.take('dust');
    d.position.set(foot.x + Math.cos(th) * (0.3 + 1.4 * u), foot.y + 0.3 * u, foot.z + Math.sin(th) * (0.3 + 1.4 * u)); d.scale.setScalar(0.6 + 1.6 * u); d.material.opacity = 0.55 * (1 - u); }
  return Math.exp(-a / 0.12);
}
// the staff: raised before the call and struck at 0; raised again for the start and struck at 8.4, when the platform lights
const staffLift = s => s < -0.6 ? 0 : s < -0.06 ? 0.7 * smooth(prog(s, -0.6, -0.15)) : s < 0 ? lerp(0.7, 0, prog(s, -0.06, 0))
  : s < 4.2 ? 0 : s < 8.34 ? 1.0 * smooth(prog(s, 4.2, 4.9)) : s < 8.4 ? lerp(1.0, 0, prog(s, 8.34, 8.4)) : 0;
function king(st, s, tags) {
  const k = st.extra('npcKing', makeKing), staff = st.extra('npcStaff', makeStaff);
  const hit = strike(st, s, 0, TEAL2) + strike(st, s, 8.4, 0xffd36a);
  const rise = 0.15 * smooth(prog(s, 0.05, 0.6)) * (1 - smooth(prog(s, 3.8, 4.4))), shiver = s > 0.1 && s < 3.8 ? 0.02 * Math.sin(s * 31) : 0;
  k.position.set(KING.x, KING.y + rise, KING.z); k.rotation.set((s < 0 ? -0.07 * prog(s, -0.6, -0.1) : 0) + 0.1 * hit, Math.PI, shiver);
  staff.position.set(KING.x - 0.78, KING.y + staffLift(s) + rise, KING.z - 0.2);
  const eye = fade(s, 0, 4.6, 0.06, 0.6) + (s >= 8.4 ? Math.exp(-(s - 8.4) / 0.4) : 0);
  for (const e of k.userData.eyes) e.material.emissiveIntensity = 4 * eye;
  for (const g of k.userData.glints) g.material.opacity = Math.min(1, eye);
  staff.userData.orb.material.emissiveIntensity = 0.3 + 3 * eye;
  const light = st.extra('npcLight', () => new THREE.PointLight(TEAL2, 0, 16, 1.4)); light.position.set(KING.x, KING.y + 2.2, KING.z - 1.4); light.intensity = 170 * auraOn(s) + 60 * hit;
  aurora(st, s);
  tags.push({ pos: V(KING.x, HEAD.y + 0.75, KING.z), text: '국왕 (NPC)', team: 'k', a: 1 });
  return hit;
}

// ---------------------------------------------------------------- the pillars and those drawn into them
const riseAt = i => 0.3 + (5 - i) * 0.12;   // from the king's side outwards
function pillar(st, team, i, s) {
  const x = PX[team], z = PZ[i], rise = riseAt(i), arr = ARR[team][TEAM[team][i][0]];
  if (s < rise) return;
  const up = easeOut(prog(s, rise, rise + 0.35)), after = s - arr, keep = after > 0 ? 1 - prog(after, 0.05, 0.3) : 1, flare = after > 0 && after < 0.25 ? 1 - after / 0.25 : 0;
  if (keep > 0) {
    const h = 14 * up, o = st.take('beam'), c = st.take('beam'), w = 1 + 0.3 * flare;
    o.position.set(x, HALL.top + h / 2, z); o.scale.set(0.7 * w, h, 0.7 * w); o.material.color.setHex(TEAL2); o.material.opacity = (0.16 + 0.05 * Math.sin(s * 9 + z)) * keep + 0.12 * flare;
    c.position.set(x, HALL.top + h / 2, z); c.scale.set(0.22, h, 0.22); c.material.color.setHex(PALE); c.material.opacity = 0.26 * keep + 0.18 * flare;
  }
  const g = st.take('glow'); g.position.set(x, HALL.top + 0.04, z); g.scale.set(0.72, 0.72, 1); g.material.color.setHex(TEAM_COL[team]); g.material.opacity = 0.15 + 0.45 * up * keep + 0.4 * flare;
}
// a runner from the course comes down its pillar: from high up the column to its place, a tail of light above it
function comet(st, team, kind, x, z, s, t1, d = 0.3) {
  const a = s - t1; if (a < -d) return null;
  const y = a < 0 ? lerp(HALL.top + 24, HALL.top, easeIn(prog(a, -d, 0))) : HALL.top;
  const p = place(st, team, kind, x, y, z, yawTo(x, z), 0, a >= 0 ? 0.12 * Math.exp(-7 * a) * Math.sin(a * 26) : 0);
  if (a < 0) { obj(st, team, kind).scale.set(0.8, 1.35, 0.8); for (let i = 1; i <= 4; i++) glint(st, V(x, y + 0.6 + i * 1.2, z), 1.2 - i * 0.2, 0.5 - i * 0.09, i % 2 ? TEAL2 : PALE); }
  else if (a < 0.35) glint(st, V(x, HALL.top + 0.6, z), 0.5 + 1.8 * (1 - a / 0.35), 0.6 * (1 - a / 0.35), PALE);
  return p;
}
function hallWorld(st, s, tags, skip = '') {
  const hit = king(st, s, tags);
  for (const team of ['w', 'b']) TEAM[team].forEach(([kind, from, fx, fz], i) => {
    pillar(st, team, i, s);
    if (from === 'me' || team + kind === skip) return;   // the clip moves the one whose screen it is itself
    const x = PX[team], z = PZ[i], arr = ARR[team][kind]; let p;
    if (from === 'hall') {
      if (s < PULL) p = place(st, team, kind, fx, 0, fz, yawTo(fx, fz, HEAD), 0, Math.sin(s * 1.7 + fx) * 0.025);
      else if (s < arr) { const u = smooth(prog(s, PULL, arr)); p = place(st, team, kind, lerp(fx, x, u), lerp(0, HALL.top, u) + 2.4 * Math.sin(Math.PI * u), lerp(fz, z, u), yawTo(x, z), -0.2, 0); glint(st, p.clone().add(V(0, 0.6, 0)), 1.6, 0.6); }
      else { const a = s - arr; p = place(st, team, kind, x, HALL.top, z, yawTo(x, z), 0, 0.12 * Math.exp(-7 * a) * Math.sin(a * 26)); }
    } else p = comet(st, team, kind, x, z, s, arr);
    if (p) tags.push(tagOf(team, base(kind), p, 1));
  });
  return hit;
}

// ---------------------------------------------------------------- the clip
// camera keys for the leader's screen: [s, how far the look leans to the king, field of view] - the direction stays hers
const ZOOM = [[-1.8, 0.22, 55], [-0.6, 0.22, 55], [-0.02, 0.5, 42], [0.12, 0.8, 24], [0.9, 0.8, 26], [1.4, 0.3, 60], [1.6, 0.3, 60]];
function zoomAt(s) { for (let i = 1; i < ZOOM.length; i++) if (s <= ZOOM[i][0]) { const [a, k0, f0] = ZOOM[i - 1], [b, k1, f1] = ZOOM[i], u = smooth(prog(s, a, b)); return [lerp(k0, k1, u), lerp(f0, f1, u)]; } return ZOOM[ZOOM.length - 1].slice(1); }
const CARD5 = { title: '⑤ 킹이 부른다 — 지팡이로 \'탁!\'', tag: '사용자 안', line: '알현실의 킹(플레이어 기물이 아니다)이 지팡이로 바닥을 치면, 곁의 빛기둥으로 모두가 빨려 온다.',
  rows: [['흐름', ['지팡이 \'탁!\' → 킹의 눈이 빛나고 시전 오로라 → 곁에 빛기둥', '→ 모두 빨려 온다 → 킹이 지팡이를 들어 3 · 2 · 1 → 다시 \'탁!\' 하면 발판이 켜진다']],
    ['빨려 오는 쪽', ['내 기물 뒤 3인칭 그대로: 빛기둥에 붙잡혀 하늘로 → 빛 터널', '→ 킹 곁의 내 기둥으로 내려꽂힌다']],
    ['정할 것', ['먼저 온 사람도 기둥으로 부를지 (지금 규칙은 그 자리에 둔다)', '기둥 수와 자리 (프리비즈: 팀마다 6개, 발판 양옆에 한 줄씩)']],
    ['만들 것', ['킹 NPC와 지팡이 치기 · 눈 빛, 오로라는 궁극기 시전 것 그대로', '빛기둥, 빨려 오는 길과 빛 터널']]] };

const kingCall = {
  title: { head: '⑤ 킹이 부른다', sub: '알현실의 킹이 지팡이로 바닥을 \'탁!\' — 곁의 빛기둥으로 모두가 빨려 온다', tag: '사용자 안' },
  dur: 13.6, sheet: [0.8, 1.9, 2.8, 3.7, 4.2, 4.65, 5.2, 7.8, 10.4],
  frame(st, t) {
    const s = t - 1.8, hud = {}, tags = []; let cam, focus = V(0, 0, 50);
    st._canopy ??= (() => { let c = null; st.hall.root.traverse(o => { if (o.isMesh && Math.abs(o.position.y - (HALL.top + 15.25)) < 0.01) c = o; }); return c; })();
    if (st._canopy) st._canopy.visible = !(s >= 2.6 && s < 3.05);
    st.hemi.intensity = 0.5;
    if (s < 1.5) {   // the leader's screen (she came first): the king rises, strikes, the pillars rise
      const hit = hallWorld(st, s, tags, 'wqueen');
      const q = place(st, 'w', 'queen', -3, 0, 41, yawTo(-3, 41, HEAD), 0, s > 1.2 ? -0.1 : 0);
      tags.push(tagOf('w', 'queen', q, 1, true));
      if (s > 1.2) glint(st, q.clone().add(V(0, 0.8, 0)), 1.2 + 2 * prog(s, 1.2, 1.5), 0.7);
      const [k, fov] = zoomAt(s);
      cam = shake(chase(q, HEAD, { k, fov }), 0.06 * hit, s);
      if (s < 0) { hud.band = { n: s < -1 ? 2 : 1, sub: INSIDE, red: true }; hud.vig = 0.55; }
      hud.me = ME_Q; hud.view = '먼저 온 사람 화면 — 백 퀸';
      hud.caption = s < 0 ? '30초가 다 되어 간다 — 알현실의 킹이 왕좌에서 일어나 지팡이를 든다' : s < 1.2 ? '킹이 지팡이로 바닥을 \'탁!\' — 눈이 빛나고 시전 오로라, 곁에 빛기둥이 솟는다' : '내 기둥이 나를 부른다 (먼저 온 사람도 함께)';
      hud.sound = cue(s, [[-1.8, -0.6, '음악이 낮아진다'], [-0.6, 0, '스르릉 (지팡이를 든다)'], [0, 0.7, '탁!! (지팡이가 바닥을 친다)'], [0.7, 1.5, '우우웅— (오로라 · 빛기둥)']]);
    } else if (s < 2.15) {   // a straggler's screen on the course: a pillar comes down on it and it is sucked up
      const lx = -1.6, z = -8 + 3.9 * (Math.min(s, PULL) - 1.5) + 0.3 * smooth(prog(s, PULL, 1.75));
      st.course.bar.rotation.y = s * 1.6;
      const kn = runner(st, 'b', 'knight', cx(2.2), z + 1.6, 0, s, 2.1); tags.push(tagOf('b', 'knight', kn));
      let y = 0, me;
      if (s < PULL) me = runner(st, 'w', 'rook', cx(lx), z, 0, s, 0.5);
      else {
        const u = prog(s, 1.68, 2.15); y = 40 * easeIn(u);
        const p = obj(st, 'w', 'rook'); p.position.set(cx(lx), y, z); p.rotation.set(-0.2, 8 * u * u, 0); p.scale.set(1 - 0.25 * u, 1 + 0.5 * u, 1 - 0.25 * u); me = p.position;
        const col = st.take('beam'); col.position.set(cx(lx), 20, z); col.scale.set(1.0, 40, 1.0); col.material.color.setHex(TEAL2); col.material.opacity = 0.75 * fade(s, PULL, 2.3, 0.03, 0.2);
        glint(st, V(cx(lx), y + 0.7, z), 2.5 + 3 * Math.exp(-(s - PULL) / 0.1), 0.8);
      }
      cam = s < 1.68 ? chase(V(cx(lx), 0, z), V(cx(-0.6), 1.2, z + 20), { walls: false, dist: 4.6, up: 2.3, k: 0.2 })
        : { p: V(cx(lx), 2.3 + y * 0.8, z - 4.6 + y * 0.04), l: V(cx(lx), y + 0.8, z), fov: lerp(55, 85, prog(s, 1.68, 2.15)) };
      focus = V(cx(0), 0, z);
      hud.streaks = prog(s, 1.8, 2.1); hud.tint = { c: '31,224,191', a: 0.25 * prog(s, 1.7, 2.15) }; hud.white = prog(s, PULL, PULL + 0.03) * (1 - prog(s, PULL + 0.03, PULL + 0.15)) * 0.7;
      hud.me = ME_R; hud.view = '늦은 사람 화면 — 백 룩 (아직 코스 위)';
      hud.caption = '순간이동이 아니라 — 빛기둥에 붙잡혀 킹 곁의 내 자리로 빨려 들어간다';
      hud.sound = cue(s, [[1.5, PULL, '헉헉 (달리는 중)'], [PULL, 1.68, '부웅— (빛기둥이 내려꽂힌다)'], [1.68, 2.15, '휘익— (빨려 올라간다)']]);
    } else if (s < 2.6) {   // the rush: nothing but light round me
      const u = prog(s, 2.15, 2.6), p = obj(st, 'w', 'rook');
      p.position.set(0, 400, 0); p.rotation.set(-0.3, 4 + 10 * u, 0.25 * Math.sin(s * 13)); p.scale.set(0.75, 1.5, 0.75);
      st.sky.material.uniforms.darkCol.value.setHex(0x0a5048); st.sky.material.uniforms.dark.value = 0.9;
      for (let i = 0; i < 6; i++) glint(st, V(Math.sin(i * 2.1 + s * 9) * 1.2, 400.4 + Math.cos(i * 1.7 + s * 7) * 1.1, -1.5 - i * 1.4), 1.2, 0.5, i % 2 ? TEAL2 : PALE);
      cam = { p: V(0, 401.7, -4.4), l: V(0, 400.6, 2), fov: 85 }; focus = V(0, 400, 0);
      hud.streaks = 1; hud.tint = { c: '31,224,191', a: 0.22 }; hud.white = prog(s, 2.5, 2.6);
      hud.me = ME_R; hud.view = '늦은 사람 화면 — 백 룩'; hud.caption = '빛 터널 — 눈 깜짝할 사이 (0.45초)';
      hud.sound = '쉬이이이익—';
    } else {   // down my pillar beside the king, then the others come in; the king raises his staff; 3 · 2 · 1; '탁!' again
      hallWorld(st, s, tags);
      const a = s - ARR.w.rook, y = a < 0 ? lerp(HALL.top + 24, HALL.top, easeIn(prog(s, 2.6, ARR.w.rook))) : HALL.top;
      const p = obj(st, 'w', 'rook'); p.position.set(ME.x, y, ME.z); p.rotation.set(0, yawTo(ME.x, ME.z), a >= 0 ? 0.12 * Math.exp(-7 * a) * Math.sin(a * 26) : 0);
      if (a < 0) { p.scale.set(0.8, 1.35, 0.8); for (let i = 1; i <= 4; i++) glint(st, V(ME.x, y + 0.6 + i * 1.2, ME.z), 1.7 - i * 0.28, 0.7 - i * 0.13, i % 2 ? TEAL2 : PALE); }
      else if (a < 0.35) glint(st, V(ME.x, HALL.top + 0.6, ME.z), 0.5 + 3.5 * (1 - a / 0.35), 0.9 * (1 - a / 0.35), PALE);
      const me = p.position; tags.push(tagOf('w', 'rook', me, a < 0 ? 0 : 1, true));
      lowerCrown(st, s, 4.3, 5.3);
      const kick = countdown(st, s, 5.4, hud);
      const hi = { p: V(ME.x - 4.2, y + 4, ME.z + 0.5), l: V(-1.5, Math.max(HALL.top, y - 5), 55.8), fov: 70 };
      cam = shake(mix(hi, chase(me, LOOK, { fov: 55 - 5 * kick }), smooth(prog(s, 2.95, 3.4))), 0.1 * Math.max(0, 1 - Math.abs(a) * 5), s);
      hud.white = 1 - prog(s, 2.6, 2.7);
      hud.me = ME_R; hud.view = '늦은 사람 화면 — 백 룩';
      hud.caption = s < 3.05 ? '킹 곁의 내 빛기둥으로 내려꽂힌다' : s < 4.2 ? '빛기둥마다 기물이 빨려 들어와 선다 — 두 팀이 발판 양옆에 줄지어'
        : s < 5.4 ? '킹이 지팡이를 높이 든다 — 왕관이 발판 위로' : '3 · 2 · 1 → 킹이 다시 \'탁!\' 하면 발판이 켜진다';
      hud.sound = cue(s, [[2.6, 3.05, '슈웅— 탁!'], [3.05, 3.9, '슝 · 슝 · 슝 (모두 빨려 온다)'], [4.2, 5.35, '킹: “짐의 왕좌를 차지해 보아라!”'], ...bells(5.4).slice(0, 3), [8.4, 9.1, '탁!! 빠밤! (발판이 켜진다)']]);
      hud.card = endCard(s, 9.2, CARD5);
    }
    return { cam, focus, hud, tags };
  },
};

export const KING_CLIPS = { king: kingCall };
