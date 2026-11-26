// Ending summons previz - ⑤ the two kings call, third pass (the user, 2026-11-22: "다른 자리로 하고, 실제 체스랑 같은 기물
// 위치로 소환됐으면 좋겠고, 이펙트는 노란색이고, 소환되는 주자는 그냥 앞으로 빠르게 빨려들어가면서 해당 위치까지 가는 연출이면
// 충분해. 우주같은거 안나오고. 그리고 이펙트 빛 색은 두 팀 다 노란색으로"). A chessboard is inlaid in the nave before the altar:
// 2 m squares, files a-h along the hall from the door's side, ranks 1-8 across it, white's ranks 1-2 on the left and black's 7-8
// on the right, so the platform on the altar is as far from either army. The kings - no player's pieces - stand on e1 and e8.
// When the 30 s run out the hall dims, the kings' eyes light, each strikes his staff ('탁!'), the board lights from his square,
// pillars rise on his players' starting squares, and everyone is drawn there: pulled forward, fast, along the way to the hall
// and down the nave onto the square. Both staffs light the crown, 3 · 2 · 1, both strike, and the platform lights. Every effect
// is yellow, the same for both teams.
import { THREE, V, TEX, MAT, lerp, prog, smooth, easeOut, easeIn, fade, GLYPH, makePiece, makeCrown } from './kit.js';
import { HALL } from './hall.js';
import { LOOK, INSIDE, ME_Q, cx, yawTo, cue, runner, tagOf, chase, mix, shake, countdown, bells, lowerCrown, endCard } from './clips.js';

const YEL = 0xffd34a, PALE = 0xfff4c8, YEL_CSS = '#ffd34a';
const KS = 1.5, SS = KS / 1.6;   // the kings stand 1.5 times a player's piece, their staffs grown with them
const SQ = (file, rank) => V(-9 + 2 * rank, 0, 27 + 2 * file);   // file 1-8 = a-h, rank 1-8
const KINGS = { w: SQ(5, 1), b: SQ(5, 8) };   // e1, e8
const FACE = { w: Math.PI / 2, b: -Math.PI / 2 };   // each army looks across the board at the other
const STRIKE = { w: 0, b: 0.45 }, PULL = 1.6, MUSTER = 3.45, BEAM = 3.9, TURN = 4.0, START = 7.6, BELLS = [4.6, 5.6, 6.6];
const ME_B = { glyph: GLYPH.bishop, name: '백 비숍', rank: '9위' };
const DOOR = V(0, 0, -2);
// six a team on their starting squares (the king is the hall's, on e1 / e8): kind, file, rank, where it was at the call,
// when it reaches its square, and (inside already) where it stood
const TEAM = {
  w: [['rook', 1, 1, 'course', 2.7], ['knight', 2, 1, 'course', 2.85], ['bishop', 3, 1, 'me', 2.6], ['queen', 4, 1, 'hall', 2.1, V(0, 1.25, 47)], ['pawn', 4, 2, 'hall', 2.05, V(-3, 0, 22)], ['pawn2', 5, 2, 'course', 3.05]],
  b: [['rook', 1, 8, 'course', 2.75], ['knight', 2, 8, 'course', 2.95], ['bishop', 3, 8, 'hall', 2.1, V(3, 0, 20)], ['queen', 4, 8, 'course', 3.1], ['pawn', 4, 7, 'course', 3.2], ['pawn2', 5, 7, 'course', 3.3]],
};
const base = k => k.replace('2', '');
const dim = s => smooth(prog(s, -2.4, -1.4)) * (1 - smooth(prog(s, START, START + 0.25)));
const bellPulse = s => BELLS.reduce((a, b) => a + (s >= b ? Math.exp(-(s - b) / 0.25) : 0), 0);

function obj(st, team, kind) {
  if (kind !== 'pawn2') { const p = st.pieces[team][kind]; p.visible = true; return p; }
  return st.extra(team + 'pawn2', () => { const p = makePiece('pawn', team === 'w' ? 'ally' : 'enemy', null, { cute: true }); p.rotation.order = 'YXZ'; return p; });
}
function place(st, team, kind, x, y, z, yaw = 0, lean = 0, roll = 0) { const p = obj(st, team, kind); p.scale.setScalar(1); p.position.set(x, y, z); p.rotation.set(lean, yaw, roll); return p; }
const glint = (st, p, size, op, color = YEL) => { const f = st.take('flare'); f.position.copy(p); f.scale.setScalar(size); f.material.color.setHex(color); f.material.opacity = op; };
const keyed = (keys, s) => { for (let i = 1; i < keys.length; i++) if (s <= keys[i][0]) { const [a, u] = keys[i - 1], [b, v] = keys[i]; return lerp(u, v, smooth(prog(s, a, b))); } return keys[keys.length - 1][1]; };

// ---------------------------------------------------------------- the board in the nave
function makeBoard() {
  const N = 1024, c = document.createElement('canvas'); c.width = c.height = N; const g = c.getContext('2d'), m = N / 16.5, b = 0.25 * m;
  const gr = g.createLinearGradient(0, 0, N, N); gr.addColorStop(0, '#a87c1c'); gr.addColorStop(0.5, '#e6c15a'); gr.addColorStop(1, '#a87c1c'); g.fillStyle = gr; g.fillRect(0, 0, N, N);
  for (let r = 1; r <= 8; r++) for (let f = 1; f <= 8; f++) { g.fillStyle = (r + f) % 2 === 0 ? '#3b322b' : '#efe6d3'; g.fillRect(b + (r - 1) * 2 * m, b + (f - 1) * 2 * m, 2 * m + 0.5, 2 * m + 0.5); }
  g.fillStyle = '#3a2a08'; g.font = `bold ${Math.round(0.22 * m)}px serif`; g.textAlign = 'center'; g.textBaseline = 'middle';
  for (let f = 1; f <= 8; f++) { const y = b + (f - 0.5) * 2 * m, L = 'abcdefgh'[f - 1]; g.fillText(L, b / 2, y); g.fillText(L, N - b / 2, y); }
  for (let r = 1; r <= 8; r++) { const x = b + (r - 0.5) * 2 * m; g.fillText(String(r), x, b / 2); g.fillText(String(r), x, N - b / 2); }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
  const mesh = new THREE.Mesh(new THREE.PlaneGeometry(16.5, 16.5), new THREE.MeshStandardMaterial({ map: t, roughness: 0.45 }));
  mesh.rotation.x = -Math.PI / 2; mesh.position.set(0, 0.025, 36); mesh.receiveShadow = true; return mesh;
}
const TARGET = new Map();   // "file,rank" -> [team, arrival]
for (const team of ['w', 'b']) for (const [, f, r, , arr] of TEAM[team]) TARGET.set(`${f},${r}`, [team, arr]);
function boardCells(st) {
  return st.extra('boardCells', () => {
    const im = new THREE.InstancedMesh(new THREE.PlaneGeometry(1.86, 1.86).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }), 64);
    const m4 = new THREE.Matrix4(), c0 = new THREE.Color(0, 0, 0);
    for (let n = 0; n < 64; n++) { const p = SQ(n % 8 + 1, Math.floor(n / 8) + 1); m4.makeTranslation(p.x, 0.04, p.z); im.setMatrixAt(n, m4); im.setColorAt(n, c0); }
    im.frustumCulled = false; return im;
  });
}
const CY = new THREE.Color(YEL), CC = new THREE.Color();
function lightBoard(st, s) {
  const im = boardCells(st), keep = fade(s, 0.2, START + 0.6, 0.4, 0.6), bp = bellPulse(s);
  for (let n = 0; n < 64; n++) {
    const f = n % 8 + 1, r = Math.floor(n / 8) + 1, p = SQ(f, r); let v = 0;
    for (const team of ['w', 'b']) {   // a wave of light over the board from each king's square
      const a = s - STRIKE[team]; if (a < 0) continue;
      const d = p.distanceTo(KINGS[team]), e = (d - 22 * a) / 1.8;
      v += 1.3 * Math.exp(-e * e) * Math.max(0, 1 - a / 1.6);
      if (d < 22 * a) v += (0.05 + 0.03 * ((f + r) % 2)) * keep + 0.18 * bp * keep;
    }
    const t = TARGET.get(`${f},${r}`);   // a square someone is called to burns until it comes
    if (t) { const [team, arr] = t, up = prog(s, STRIKE[team] + 0.15, STRIKE[team] + 0.5); v += s < arr ? 0.45 * up * (0.8 + 0.2 * Math.sin(s * 10)) : 0.45 * Math.exp(-(s - arr) / 0.25) + 0.08 * keep; }
    const a2 = s - START;   // the start: a wave from the board's middle
    if (a2 >= 0) { const d = Math.hypot(p.x, p.z - 36), e = (d - 20 * a2) / 2.2; v += 1.2 * Math.exp(-e * e) * Math.max(0, 1 - a2 / 1.4); }
    im.setColorAt(n, CC.copy(CY).multiplyScalar(v));
  }
  im.instanceColor.needsUpdate = true;
}

// ---------------------------------------------------------------- the kings, their staffs, eyes and auroras - all in yellow
function makeKing(team) {
  const g = makePiece('king', team === 'w' ? 'ally' : 'enemy', null, { cute: true }); g.rotation.order = 'YXZ'; g.scale.setScalar(KS);
  const eyes = [], glints = [];
  g.traverse(o => { if (o.isMesh && o.geometry.parameters?.radius === 0.06) { o.material = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: YEL, emissiveIntensity: 0, roughness: 0.3 }); eyes.push(o); } });
  for (const e of eyes) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.flash, color: YEL, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
    sp.position.copy(e.position).add(V(0, 0, 0.06)); sp.scale.setScalar(0.5); g.add(sp); glints.push(sp);
  }
  g.userData = { eyes, glints }; return g;
}
function makeStaff() {   // the Queen of the Hill king's sceptre grown to his height: a gold rod, a crown, a light at its top
  const g = new THREE.Group();
  const rod = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.07, 2.5, 16), MAT.gold); rod.position.y = 1.25; g.add(rod);
  for (const y of [0.12, 0.8, 1.6, 2.3]) { const b = new THREE.Mesh(new THREE.TorusGeometry(0.085, 0.028, 8, 20), MAT.gold); b.rotation.x = Math.PI / 2; b.position.y = y; g.add(b); }
  const crown = makeCrown(0.17); crown.position.y = 2.58; g.add(crown);
  const orb = new THREE.Mesh(new THREE.SphereGeometry(0.11, 16, 12), new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: YEL, emissiveIntensity: 0.3 })); orb.position.y = 2.74; g.add(orb);
  g.traverse(o => { if (o.isMesh) o.castShadow = true; }); g.userData.orb = orb; return g;
}
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
function aurora(st, team, s) {
  const t0 = STRIKE[team], on = fade(s, t0 + 0.02, START + 0.6, 0.3, 0.6); if (on <= 0) return;
  const K = KINGS[team], pulse = 1.4 * Math.exp(-Math.max(0, s - t0) / 0.16) + 0.6 * bellPulse(s) + (s >= MUSTER ? 0.8 * Math.exp(-(s - MUSTER) / 0.3) : 0);
  AUR.forEach(([r, h, w, arc], i) => {
    const m = st.extra(`aur${team}${i}`, () => new THREE.Mesh(new THREE.CylinderGeometry(r * KS, r * 1.25 * KS, h * KS, 64, 8, true, 0, arc * Math.PI),
      new THREE.ShaderMaterial({ vertexShader: AUR_VS, fragmentShader: AUR_FS, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide,
        uniforms: { t: { value: 0 }, k: { value: 0 }, col: { value: new THREE.Color(YEL) }, hi: { value: new THREE.Color(PALE) } } })));
    const sc = 0.6 + 0.4 * easeOut(prog(s, t0, t0 + 0.35));
    m.position.set(K.x, h * KS / 2 * sc - 0.2, K.z); m.scale.setScalar(sc); m.rotation.y = i * 2.1 + w * s;
    m.material.uniforms.t.value = s + i * 3.7; m.material.uniforms.k.value = 0.75 * on * (0.9 + 0.5 * pulse);
  });
  for (let i = 0; i < 10; i++) { const u = (s * 1.3 + i / 10) % 1, th = i / 10 * Math.PI * 2 + s * 2;
    glint(st, V(K.x + Math.cos(th) * 1.2 * (1 - u), 0.2 + u * 4.2, K.z + Math.sin(th) * 1.2 * (1 - u)), 0.9 + 1.4 * u, 0.22 * on * (1 - u), i % 3 ? YEL : PALE); }
}
const LIFT = { w: [[-9, 0], [-0.6, 0], [-0.15, 0.7], [-0.06, 0.7], [0, 0], [0.9, 0], [1.3, 1.0], [2.0, 1.0], [2.4, 0], [MUSTER, 0], [MUSTER + 0.4, 1.2], [START - 0.08, 1.2], [START, 0], [99, 0]],
  b: [[-9, 0], [-0.15, 0], [0.3, 0.7], [0.39, 0.7], [0.45, 0], [0.9, 0], [1.3, 1.0], [2.0, 1.0], [2.4, 0], [MUSTER, 0], [MUSTER + 0.4, 1.2], [START - 0.08, 1.2], [START, 0], [99, 0]] };
function staffAt(team, s) { const K = KINGS[team], y = FACE[team], ox = -0.6, oz = 0.25; return V(K.x + ox * Math.cos(y) + oz * Math.sin(y), keyed(LIFT[team], s) * SS, K.z + oz * Math.cos(y) - ox * Math.sin(y)); }
function king(st, team, s, tags) {
  const K = KINGS[team], k = st.extra('king' + team, () => makeKing(team)), staff = st.extra('staff' + team, makeStaff);
  const hit = Math.max(s >= STRIKE[team] ? Math.exp(-(s - STRIKE[team]) / 0.12) : 0, s >= START ? Math.exp(-(s - START) / 0.12) : 0);
  k.position.set(K.x, 0, K.z); k.rotation.set(0.1 * hit, FACE[team], s > STRIKE[team] && s < START ? 0.015 * Math.sin(s * 29 + K.x) : 0);
  const sp = staffAt(team, s); staff.scale.setScalar(SS); staff.position.copy(sp);
  const eye = 0.55 * smooth(prog(s, -1.3, -0.9)) + 0.9 * (s >= STRIKE[team] ? Math.exp(-(s - STRIKE[team]) / 0.5) : 0) + (s >= MUSTER ? 0.5 : 0) - (s > START + 0.8 ? 1.5 * prog(s, START + 0.8, START + 1.4) : 0);
  for (const e of k.userData.eyes) e.material.emissiveIntensity = 5 * Math.max(0, eye);
  for (const g of k.userData.glints) g.material.opacity = Math.min(1, Math.max(0, eye));
  staff.userData.orb.material.emissiveIntensity = 0.3 + 4 * Math.max(0, eye);
  const light = st.extra('light' + team, () => new THREE.PointLight(YEL, 0, 12, 1.6)); light.position.set(K.x, 3.2, K.z);
  light.intensity = 70 * fade(s, STRIKE[team], START + 0.6, 0.2, 0.6) + 60 * hit + 30 * smooth(prog(s, -1.3, -0.9)) * (s < STRIKE[team] ? 1 : 0);
  aurora(st, team, s);
  for (const t0 of [STRIKE[team], START]) {   // the strike: a flash at the staff's foot and dust
    const a = s - t0; if (a < 0 || a > 0.8) continue;
    const foot = V(sp.x, 0.15, sp.z);
    if (a < 0.35) glint(st, foot, 1 + 6 * (1 - a / 0.35), 0.9 * (1 - a / 0.35), PALE);
    for (let i = 0; i < 6; i++) { const u = a / 0.8, th = i / 6 * Math.PI * 2, d = st.take('dust'); d.position.set(foot.x + Math.cos(th) * (0.3 + 1.6 * u), 0.2 + 0.3 * u, foot.z + Math.sin(th) * (0.3 + 1.6 * u)); d.scale.setScalar(0.6 + 1.8 * u); d.material.opacity = 0.5 * (1 - u); }
  }
  tags.push({ pos: V(K.x, 1.62 * KS + 0.7, K.z), text: team === 'w' ? '백 킹 (NPC) · e1' : '흑 킹 (NPC) · e8', team: 'k', a: 1 });
  return hit;
}

// ---------------------------------------------------------------- the pillars, and everyone drawn onto its square
function pillar(st, team, i, s) {
  const [, f, r, , arr] = TEAM[team][i], p = SQ(f, r), rise = STRIKE[team] + 0.2 + i * 0.06;
  if (s < rise) return;
  const up = easeOut(prog(s, rise, rise + 0.35)), after = s - arr, keep = after > 0 ? 1 - prog(after, 0.05, 0.3) : 1, flare = after > 0 && after < 0.25 ? 1 - after / 0.25 : 0;
  if (keep <= 0) return;
  const h = 10 * up, o = st.take('beam'), c = st.take('beam'), w = 1 + 0.3 * flare;
  o.position.set(p.x, h / 2, p.z); o.scale.set(0.75 * w, h, 0.75 * w); o.material.color.setHex(YEL); o.material.opacity = (0.18 + 0.05 * Math.sin(s * 9 + p.z)) * keep + 0.12 * flare;
  c.position.set(p.x, h / 2, p.z); c.scale.set(0.22, h, 0.22); c.material.color.setHex(PALE); c.material.opacity = 0.3 * keep + 0.18 * flare;
}
// the way in: from the hall's door down the nave, turning onto the square at the end (or, from inside, straight there)
function path(from, to, u, inside) {
  if (inside) return from.clone().lerp(to, u).setY(lerp(from.y, 0, u) + 1.2 * Math.sin(Math.PI * u));
  const c = V(lerp(from.x, to.x, 0.12), 0, to.z - 3), a = from.clone().lerp(c, u), b = c.lerp(to, u); return a.lerp(b, u);
}
const DUR = { course: 0.55, hall: 0.5, me: 0.6 };
// a piece being drawn: fast at first, slowing onto its square; stretched, a yellow tail behind it; '탁' and a flash when it lands
function drawn(st, team, kind, f, r, from, s, arr, inside, tags, mine = false) {
  const to = SQ(f, r), d = inside ? DUR.hall : mine ? DUR.me : DUR.course, a = s - arr;
  if (a < -d - 1e-6) return null;   // (a little slack: a frame's time can land a hair before the start of the draw)
  let p, pos;
  if (a < 0) {
    const u = easeOut(prog(a, -d, 0)), q = path(from, to, u, inside), ahead = path(from, to, Math.min(1, u + 0.02), inside);
    p = place(st, team, kind, q.x, q.y, q.z, Math.atan2(ahead.x - q.x, ahead.z - q.z) || FACE[team], 0.35, 0); p.scale.set(0.88, 1.18, 0.88);
    for (let j = 1; j <= 6; j++) glint(st, path(from, to, Math.max(0, u - j * 0.045), inside).add(V(0, 0.6, 0)), 1.4 - j * 0.17, 0.55 - j * 0.08, j % 2 ? YEL : PALE);
  } else {
    const turn = smooth(prog(s, TURN, TURN + 0.5)), yaw = lerp(FACE[team], yawTo(to.x, to.z), turn);
    p = place(st, team, kind, to.x, 0, to.z, yaw, s > START ? 0.18 : 0, 0.12 * Math.exp(-7 * a) * Math.sin(a * 26));
    if (a < 0.35) glint(st, V(to.x, 0.6, to.z), 0.5 + 2.2 * (1 - a / 0.35), 0.65 * (1 - a / 0.35), PALE);
    if (a < 0.7) for (let i = 0; i < 2; i++) { const u = a / 0.7, dd = st.take('dust'); dd.position.set(to.x + (i ? 0.5 : -0.5) * (0.4 + u), 0.25 + 0.3 * u, to.z); dd.scale.setScalar(0.7 + 1.6 * u); dd.material.opacity = 0.5 * (1 - u); }
  }
  tags.push(tagOf(team, base(kind), p.position, a < 0 ? 0.6 : 1, mine));
  return p;
}
function hallWorld(st, s, tags, skip = '') {
  st.extra('board', makeBoard); lightBoard(st, s);
  // the nave's columns that would stand on the board's edge are left out (where the board goes, the columns move)
  st._boardCols ??= (() => { const l = []; st.hall.root.traverse(o => { if (o.isMesh && Math.abs(Math.abs(o.position.x) - 9) < 0.01 && [30, 36, 42].some(z => Math.abs(o.position.z - z) < 0.01)) l.push(o); }); return l; })();
  for (const o of st._boardCols) st.hide(o);
  let hit = 0;
  for (const team of ['w', 'b']) {
    hit = Math.max(hit, king(st, team, s, tags));
    TEAM[team].forEach(([kind, f, r, from, arr, at], i) => {
      pillar(st, team, i, s);
      if (from === 'me' || team + kind === skip) return;
      if (from === 'hall' && s < arr - DUR.hall) { const p = place(st, team, kind, at.x, at.y, at.z, yawTo(at.x, at.z, KINGS[team]), 0, Math.sin(s * 1.7 + at.x) * 0.025); tags.push(tagOf(team, base(kind), p.position, 1)); }
      else drawn(st, team, kind, f, r, from === 'hall' ? at : DOOR, s, arr, from === 'hall', tags);
    });
  }
  return hit;
}
// both staffs light the crown: a yellow ray from each staff's light to the crown over the platform
function ray(st, key, a, b, r, op) {
  const m = st.extra(key, () => new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 1, 12, 1, true), new THREE.MeshBasicMaterial({ transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false })));
  const d = b.clone().sub(a), L = d.length(); m.position.copy(a).addScaledVector(d, 0.5); m.quaternion.setFromUnitVectors(V(0, 1, 0), d.normalize()); m.scale.set(r, L, r);
  m.material.color.setHex(r > 0.1 ? YEL : PALE); m.material.opacity = op;
}
function crownRays(st, s) {
  const on = fade(s, BEAM, START + 0.15, 0.12, 0.15); if (on <= 0) return;
  const c = st.hall.crown.position.clone().add(V(0, 0.8, 0)), grow = easeOut(prog(s, BEAM, BEAM + 0.3)), beat = 0.7 + 0.3 * Math.sin(s * 14) + 0.6 * bellPulse(s);
  for (const team of ['w', 'b']) {
    const from = staffAt(team, s).add(V(0, 2.74 * SS, 0)), to = from.clone().lerp(c, grow);
    ray(st, 'rayO' + team, from, to, 0.24, 0.32 * on * beat); ray(st, 'rayC' + team, from, to, 0.07, 0.9 * on); glint(st, from, 1.1 + 0.4 * beat, 0.8 * on, PALE);
  }
  glint(st, c, (3 + 2 * beat) * grow, 0.75 * on * grow, 0xfff0c0);
}

// ---------------------------------------------------------------- the clip
const CARD5 = { title: '⑤ 두 킹이 부른다 — 체스 시작 자리로', tag: '사용자 안', line: '알현실 바닥의 체스판 e1 · e8에 선 백 킹 · 흑 킹(NPC)이 각자 자기 팀을 실제 체스 시작 칸으로 부른다. 이펙트는 두 팀 다 노란색.',
  rows: [['흐름', ['불이 꺼지고 두 킹의 눈이 노랗게 → 백 킹 \'탁!\', 흑 킹 \'탁!\' — 판이 킹 칸에서부터 노랗게 켜지고', '부를 칸마다 빛기둥 → 모두 자기 칸으로 빨려 온다 → 두 지팡이가 왕관을 비추고 3 · 2 · 1 → 함께 \'탁!\'']],
    ['빨려 오는 쪽', ['앞으로 빠르게 빨려 가며 자기 칸까지 — 코스에서는 길을 따라 앞으로, 홀에서는 문에서 신랑을 타고', '칸 위에서 멈춘다. 내 기물 뒤 3인칭 그대로, 우주 · 터널 없음']],
    ['정할 것', ['판의 자리 (프리비즈: 대성당 신랑 바닥, 백 1 · 2단은 왼쪽, 흑 7 · 8단은 오른쪽 — 제단 발판까지 같은 거리)', '6명이 서는 칸 (프리비즈: 룩 a1 · 나이트 b1 · 비숍 c1 · 퀸 d1 · 폰 d2 · e2), 먼저 온 사람도 부를지 (프리비즈: 모두)']],
    ['만들 것', ['판 무늬와 칸 좌표, 킹 NPC 둘과 지팡이 치기 · 눈 빛, 노란 오로라 · 빛기둥 · 판 물결', '빨려 오는 길 (길을 따라 빠르게 → 칸에서 멈춤), 왕관을 비추는 두 빛줄기']]] };
const ZOOM = [[-2.6, 0.22, 55], [-0.6, 0.25, 50], [-0.02, 0.5, 40], [0.12, 0.8, 26], [0.4, 0.75, 28], [0.58, 0.8, 26], [0.9, 0.75, 30], [1.3, 0.3, 56], [1.6, 0.3, 58]];
function zoomAt(s) { for (let i = 1; i < ZOOM.length; i++) if (s <= ZOOM[i][0]) { const [a, k0, f0] = ZOOM[i - 1], [b, k1, f1] = ZOOM[i], u = smooth(prog(s, a, b)); return [lerp(k0, k1, u), lerp(f0, f1, u)]; } return ZOOM[ZOOM.length - 1].slice(1); }
// the leader looks at the board, at the white king as he strikes, at the black king as he answers, then at all of it
const LOOK_A = s => { const c = V(0, 1, 36), w = KINGS.w.clone().setY(2.2), b = KINGS.b.clone().setY(2.2);
  return s < 0.3 ? c.lerp(w, smooth(prog(s, -0.6, 0.05))) : s < 1.0 ? w.lerp(b, smooth(prog(s, 0.3, 0.5))) : b.lerp(c, smooth(prog(s, 1.0, 1.3))); };

const kings = {
  title: { head: '⑤ 두 킹이 부른다', sub: '체스판 e1 · e8의 백 킹과 흑 킹이 지팡이로 \'탁!\' — 각자 자기 팀을 실제 체스 시작 칸으로', tag: '사용자 안' },
  dur: 14.3, sheet: [1.6, 2.7, 3.3, 4.5, 5.1, 5.6, 6.6, 7.6, 11.1],
  frame(st, t) {
    const hud = {}, tags = []; let cam, focus = V(0, 0, 40);
    const A = t < 4.9, s = A ? t - 2.6 : t - 3.3;   // the leader's screen runs to 2.3; the straggler's picks up again at 1.6
    const dk = dim(s); st.hall.setLamps(1 - 0.8 * dk); st.hemi.intensity = 0.5 - 0.3 * dk; st.sun.intensity = 2.6 - 1.8 * dk;
    st.hall.setDoors(smooth(prog(s, 1.45, 1.75)));   // the great doors fly open for those who come in
    if (A) {   // the leader's screen (she came first): dark, the eyes, the two strikes, the board, the pillars, and her own draw to d1
      const hit = hallWorld(st, s, tags, 'wqueen');
      const at = V(0, 1.25, 47);
      let q;
      if (s < 2.1 - DUR.hall) { q = place(st, 'w', 'queen', at.x, at.y, at.z, Math.PI, 0, 0); tags.push(tagOf('w', 'queen', q.position, 1, true)); }
      else q = drawn(st, 'w', 'queen', 4, 1, at, s, 2.1, true, tags, true);
      const qp = q.position;
      if (s < PULL) { const [k, fov] = zoomAt(s); cam = shake(chase(qp, LOOK_A(s), { k, fov, up: 2.3 }), 0.07 * hit, s); }
      else { const to = SQ(4, 1), dir = to.clone().sub(at).setY(0).normalize(); cam = chase(qp, qp.clone().addScaledVector(dir, 20), { dist: 5, up: 2.4, k: 0.2, fov: lerp(58, 72, prog(s, PULL, 2.1)) }); }
      if (s < 0) { hud.band = { n: s < -1.6 ? 3 : s < -0.6 ? 2 : 1, sub: INSIDE, red: true }; hud.vig = 0.4 + 0.4 * dk; }
      hud.streaks = s > PULL && s < 2.1 ? 0.5 * prog(s, PULL, PULL + 0.2) : 0;
      hud.me = ME_Q; hud.view = '먼저 온 사람 화면 — 백 퀸';
      hud.caption = s < -1.3 ? '30초가 끝나 간다 — 홀의 불이 꺼진다' : s < 0 ? '바닥 체스판 e1 · e8에 선 두 킹의 눈이 노랗게 뜬다' : s < 0.9 ? '백 킹 \'탁!\' · 흑 킹 \'탁!\' — 판이 킹의 칸에서부터 노랗게 켜지고, 부를 칸마다 빛기둥'
        : s < PULL ? '두 킹이 자기 팀을 부른다 — 큰 문이 열린다' : '나도 내 칸 d1로 — 앞으로 빠르게 빨려 간다';
      hud.sound = cue(s, [[-2.6, -1.3, '음악이 멎는다'], [-1.3, -0.1, '우웅… (두 킹의 눈이 뜬다)'], [-0.1, 0.42, '탁!! (백 킹)'], [0.42, 0.9, '탁!! (흑 킹)'],
        [0.9, PULL, '백 킹 “백의 군대여, 제자리로!” · 흑 킹 “흑의 군대여, 제자리로!”'], [PULL, 2.4, '쉬익—']]);
    } else if (s < 2.0) {   // the straggler on the course: caught, and pulled forward along the way - fast
      const lx = -1.6, z0 = -8 + 3.9 * (Math.min(s, PULL) - 1.2), pull = 70 * easeIn(prog(s, 1.62, 2.0)), z = z0 + pull;
      st.course.bar.rotation.y = s * 1.6;
      const kn = runner(st, 'b', 'knight', cx(2.2), z0 + 1.6 + 70 * easeIn(prog(s, 1.66, 2.0)), 0, s, 2.1); tags.push(tagOf('b', 'knight', kn));
      if (s < PULL) runner(st, 'w', 'bishop', cx(lx), z, 0, s, 0.5);
      else {
        const u = prog(s, 1.62, 2.0), p = obj(st, 'w', 'bishop'); p.position.set(cx(lx), 0.3 * u, z); p.rotation.set(0.4 * u, 0, 0); p.scale.set(1 - 0.12 * u, 1 + 0.25 * u, 1 - 0.12 * u);
        glint(st, V(cx(lx), 0.8, z), 2.2 + 2 * Math.exp(-(s - PULL) / 0.1), 0.6);
        for (let j = 1; j <= 6; j++) glint(st, V(cx(lx), 0.6, z - j * (0.6 + 2.6 * u)), 1.4 - j * 0.17, 0.5 - j * 0.07, j % 2 ? YEL : PALE);
      }
      cam = chase(V(cx(lx), 0, z), V(cx(-0.6), 1.2, z + 20), { walls: false, dist: 4.6 + 1.4 * prog(s, 1.62, 2.0), up: 2.3, k: 0.2, fov: lerp(55, 88, prog(s, 1.62, 2.0)) });
      focus = V(cx(0), 0, z);
      hud.streaks = prog(s, 1.7, 1.95); hud.white = Math.max(0.6 * prog(s, PULL, PULL + 0.03) * (1 - prog(s, PULL + 0.03, PULL + 0.15)), prog(s, 1.94, 2.0));
      hud.me = ME_B; hud.view = '같은 순간, 늦은 사람 화면 — 백 비숍 (아직 코스 위)';
      hud.caption = '순간이동이 아니라 — 붙잡혀 앞으로 빠르게 빨려 간다 (코스의 출구를 지나 홀로)';
      hud.sound = cue(s, [[1.2, PULL, '헉헉 (달리는 중)'], [PULL, 1.7, '웅! (붙잡힌다)'], [1.7, 2.0, '쉬이이익—']]);
    } else {   // in through the great door, down the nave onto c1; the team comes in; the rays; we turn to the throne; 3 · 2 · 1; both strike
      hallWorld(st, s, tags); crownRays(st, s);
      const me = drawn(st, 'w', 'bishop', 3, 1, DOOR, s, 2.6, false, tags, true), mp = me.position, a = s - 2.6;
      lowerCrown(st, s, BEAM, BEAM + 1.0);
      const kick = countdown(st, s, BELLS[0], hud);
      let c;
      if (a < 0) { const to = SQ(3, 1), u = easeOut(prog(a, -DUR.me, 0)), ahead = path(DOOR, to, Math.min(1, u + 0.05), false); c = chase(mp, ahead.clone().addScaledVector(ahead.clone().sub(mp).setY(0).normalize(), 12).setY(1.2), { dist: 4.8, up: 2.3, k: 0.2, fov: lerp(88, 60, u) }); }
      else {
        const across = chase(mp, V(7, 1.5, 36), { dist: 4.8, up: 2.4, k: 0.24, fov: 56 }), throne = chase(mp, LOOK, { dist: 5.0, up: 3.2, k: 0.28, fov: 55 - 5 * kick });
        c = mix(across, throne, smooth(prog(s, TURN, TURN + 0.6)));
        if (a < 0.4) c = mix(chase(mp, V(-7 + 8, 1.2, 36), { dist: 4.8, up: 2.3, k: 0.2, fov: 60 }), c, smooth(prog(a, 0, 0.4)));
      }
      cam = shake(c, 0.1 * Math.max(0, 1 - Math.abs(a) * 5) + 0.12 * (s >= START ? Math.exp(-(s - START) / 0.2) : 0), s);
      if (s >= MUSTER && s < MUSTER + 0.9) hud.big = { text: '양 팀 집결!', a: fade(s, MUSTER, MUSTER + 0.9, 0.05, 0.3), scale: 1 + 0.3 * (1 - easeOut(prog(s, MUSTER, MUSTER + 0.2))), size: 74, y: 230, color: YEL_CSS };
      hud.streaks = a < 0 ? 0.7 * (1 - prog(a, -DUR.me, 0)) : 0; hud.white = Math.max(1 - prog(s, 2.0, 2.08), 0.2 * (s >= START ? Math.exp(-(s - START) / 0.15) : 0));
      hud.me = ME_B; hud.view = '늦은 사람 화면 — 백 비숍';
      hud.caption = s < 2.6 ? '열린 큰 문으로 — 신랑을 따라 내 시작 칸 c1까지 빨려 와 멈춘다' : s < MUSTER ? '팀원들이 하나씩 자기 칸으로 빨려 와 선다 — 마주 선 두 군대' : s < BEAM ? '모였다 — 두 킹이 지팡이를 높이 든다'
        : s < BELLS[0] ? '두 지팡이에서 빛줄기 — 제단 위 왕관을 비춘다. 모두 왕좌 쪽으로 돌아선다' : s < START ? '3 · 2 · 1' : '두 킹이 함께 \'탁!\' — 불이 돌아오고 발판이 켜진다';
      hud.sound = cue(s, [[2.0, 2.6, '쉬이익— 탁!'], [2.6, MUSTER, '슉 · 슉 · 슉 (팀원들이 빨려 온다)'], [MUSTER, BEAM, '두 킹 “왕좌를 차지하라!”'], [BEAM, BELLS[0], '지이잉— (두 빛줄기가 왕관에)'],
        ...bells(BELLS[0]).slice(0, 3), [START, START + 0.7, '탁!! 탁!! 빠밤! (두 킹이 함께)']]);
      hud.card = endCard(s, START + 0.8, CARD5);
    }
    return { cam, focus, hud, tags };
  },
};

export const KING_CLIPS = { king: kings };
