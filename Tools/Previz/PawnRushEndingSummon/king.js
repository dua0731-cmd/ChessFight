// Ending summons previz - ⑤ the two kings call (the user's plan, 2026-11-22). At the far end of the hall the white king and
// the black king - no player's pieces in this mode - stand in the back corners behind the places of their armies; the empty
// throne between them is what the two teams will fight for. When the 30 s run out the hall goes dark and the kings' eyes
// open; each strikes his staff on the floor ('탁!'), the floor's chessboard lights in his colour from his feet, his aurora
// (the skill cast's) wraps him, a pillar of light rises for each of his players, and his team is drawn in - those inside fly
// across the dark hall, those on the course are sucked up through a rush of stars and broken board and come down their
// pillars. Then both staffs light the crown over the platform, 3 · 2 · 1, both strike together, and the platform lights.
import { THREE, V, TEX, MAT, lerp, prog, smooth, easeOut, easeIn, fade, rng, HEIGHT, makePiece, makeCrown } from './kit.js';
import { HALL } from './hall.js';
import { LOOK, INSIDE, ME_R, ME_Q, cx, yawTo, cue, runner, tagOf, chase, mix, shake, countdown, bells, lowerCrown, endCard } from './clips.js';

const KS = 2.2, DAIS = 1.0, SS = KS / 1.6;   // a king stands 2.2 times a player's piece, on a 1 m dais; his staff grows with him
const KINGS = { w: { x: -10, z: 62.7 }, b: { x: 10, z: 62.7 } };
const COL = { w: { main: 0x46d2ff, pale: 0xcff6ff, css: '#46d2ff' }, b: { main: 0xff8a2a, pale: 0xffdcb8, css: '#ff9a3c' } };
const STRIKE = { w: 0, b: 0.45 }, PULL = 1.6, BEAM = 4.6, START = 8.6, BELLS = [5.6, 6.6, 7.6];
const headOf = team => V(KINGS[team].x, DAIS + HEIGHT.king * KS + 0.2, KINGS[team].z);
const kingYaw = team => Math.atan2(0 - KINGS[team].x, HALL.circleZ - KINGS[team].z);
// two ranks of three before each king: who stands where, and where each was at the call ('hall': inside already)
const TEAM = {
  w: [['queen', -8, 59.2, 'hall', 0, 38], ['rook', -10, 59.2, 'me'], ['knight', -12, 59.2, 'course'], ['bishop', -8, 60.8, 'course'], ['pawn', -10, 60.8, 'hall', -6, 38], ['pawn2', -12, 60.8, 'course']],
  b: [['queen', 8, 59.2, 'course'], ['rook', 10, 59.2, 'course'], ['knight', 12, 59.2, 'course'], ['bishop', 8, 60.8, 'hall', 3.5, 43], ['pawn', 10, 60.8, 'course'], ['pawn2', 12, 60.8, 'course']],
};
const ARR = { w: { queen: 2.3, rook: 3.0, knight: 3.2, bishop: 3.4, pawn: 2.3, pawn2: 3.6 }, b: { queen: 3.5, rook: 3.1, knight: 3.3, bishop: 2.3, pawn: 3.65, pawn2: 3.8 } };
const MUSTER = { w: 3.75, b: 3.95 };   // the last of each team is in
const ME = { x: -10, z: 59.2 };
const base = k => k.replace('2', '');
const dim = s => smooth(prog(s, -2.4, -1.4)) * (1 - smooth(prog(s, START, START + 0.25)));   // the hall goes dark for the call

function obj(st, team, kind) {
  if (kind !== 'pawn2') { const p = st.pieces[team][kind]; p.visible = true; return p; }
  return st.extra(team + 'pawn2', () => { const p = makePiece('pawn', team === 'w' ? 'ally' : 'enemy', null, { cute: true }); p.rotation.order = 'YXZ'; return p; });
}
function place(st, team, kind, x, y, z, yaw = 0, lean = 0, roll = 0) { const p = obj(st, team, kind); p.scale.setScalar(1); p.position.set(x, y, z); p.rotation.set(lean, yaw, roll); return p.position; }
const glint = (st, p, size, op, color) => { const f = st.take('flare'); f.position.copy(p); f.scale.setScalar(size); f.material.color.setHex(color); f.material.opacity = op; };
const keyed = (keys, s) => { for (let i = 1; i < keys.length; i++) if (s <= keys[i][0]) { const [a, u] = keys[i - 1], [b, v] = keys[i]; return lerp(u, v, smooth(prog(s, a, b))); } return keys[keys.length - 1][1]; };

// ---------------------------------------------------------------- the kings, their staffs and eyes, their auroras
function makeKing(team) {
  const g = makePiece('king', team === 'w' ? 'ally' : 'enemy', null, { cute: true }); g.rotation.order = 'YXZ'; g.scale.setScalar(KS);
  const eyes = [], glints = [];
  g.traverse(o => { if (o.isMesh && o.geometry.parameters?.radius === 0.06) { o.material = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: COL[team].main, emissiveIntensity: 0, roughness: 0.3 }); eyes.push(o); } });
  for (const e of eyes) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.flash, color: COL[team].main, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
    sp.position.copy(e.position).add(V(0, 0, 0.06)); sp.scale.setScalar(0.5); g.add(sp); glints.push(sp);
  }
  g.userData = { eyes, glints }; return g;
}
function makeStaff(team) {   // the Queen of the Hill king's sceptre grown to his height: a gold rod, a crown, a light in his colour
  const g = new THREE.Group();
  const rod = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.07, 2.5, 16), MAT.gold); rod.position.y = 1.25; g.add(rod);
  for (const y of [0.12, 0.8, 1.6, 2.3]) { const b = new THREE.Mesh(new THREE.TorusGeometry(0.085, 0.028, 8, 20), MAT.gold); b.rotation.x = Math.PI / 2; b.position.y = y; g.add(b); }
  const crown = makeCrown(0.17); crown.position.y = 2.58; g.add(crown);
  const orb = new THREE.Mesh(new THREE.SphereGeometry(0.11, 16, 12), new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: COL[team].main, emissiveIntensity: 0.3 })); orb.position.y = 2.74; g.add(orb);
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
const bellPulse = s => BELLS.reduce((a, b) => a + (s >= b ? Math.exp(-(s - b) / 0.25) : 0), 0);
function aurora(st, team, s) {
  const t0 = STRIKE[team], on = fade(s, t0 + 0.02, START + 0.6, 0.3, 0.6); if (on <= 0) return;
  const K = KINGS[team], pulse = 1.4 * Math.exp(-Math.max(0, s - t0) / 0.16) + 0.6 * bellPulse(s) + (s >= MUSTER[team] ? 0.8 * Math.exp(-(s - MUSTER[team]) / 0.3) : 0);
  AUR.forEach(([r, h, w, arc], i) => {
    const m = st.extra(`aur${team}${i}`, () => new THREE.Mesh(new THREE.CylinderGeometry(r * KS, r * 1.25 * KS, h * KS, 64, 8, true, 0, arc * Math.PI),
      new THREE.ShaderMaterial({ vertexShader: AUR_VS, fragmentShader: AUR_FS, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide,
        uniforms: { t: { value: 0 }, k: { value: 0 }, col: { value: new THREE.Color(COL[team].main) }, hi: { value: new THREE.Color(COL[team].pale) } } })));
    const sc = 0.6 + 0.4 * easeOut(prog(s, t0, t0 + 0.35));
    m.position.set(K.x, DAIS + h * KS / 2 * sc - 0.2, K.z); m.scale.setScalar(sc); m.rotation.y = i * 2.1 + w * s;
    m.material.uniforms.t.value = s + i * 3.7; m.material.uniforms.k.value = 0.7 * on * (0.9 + 0.5 * pulse);
  });
  for (let i = 0; i < 12; i++) {   // flames of his colour rising round him
    const u = (s * 1.3 + i / 12) % 1, th = i / 12 * Math.PI * 2 + s * 2;
    glint(st, V(K.x + Math.cos(th) * 1.7 * (1 - u), DAIS + 0.2 + u * 6, K.z + Math.sin(th) * 1.7 * (1 - u)), 1.1 + 1.8 * u, 0.2 * on * (1 - u), i % 3 ? COL[team].main : COL[team].pale);
  }
}
// the staff: raised and struck at his call, raised to call his army, raised high when they are in, struck again at the start
const LIFT = { w: [[-9, 0], [-0.6, 0], [-0.15, 0.7], [-0.06, 0.7], [0, 0], [0.9, 0], [1.3, 1.0], [2.0, 1.0], [2.4, 0], [3.75, 0], [4.15, 1.2], [START - 0.08, 1.2], [START, 0], [99, 0]],
  b: [[-9, 0], [-0.15, 0], [0.3, 0.7], [0.39, 0.7], [0.45, 0], [0.9, 0], [1.3, 1.0], [2.0, 1.0], [2.4, 0], [3.95, 0], [4.35, 1.2], [START - 0.08, 1.2], [START, 0], [99, 0]] };
function staffTop(team, s) {
  const K = KINGS[team], y = kingYaw(team), ox = -0.75 * SS, oz = 0.2 * SS;
  return V(K.x + ox * Math.cos(y) + oz * Math.sin(y), DAIS + keyed(LIFT[team], s) * SS, K.z + oz * Math.cos(y) - ox * Math.sin(y));
}
function king(st, team, s, tags) {
  const K = KINGS[team], k = st.extra('king' + team, () => makeKing(team)), staff = st.extra('staff' + team, () => makeStaff(team));
  const hit = Math.max(s >= STRIKE[team] ? Math.exp(-(s - STRIKE[team]) / 0.12) : 0, s >= START ? Math.exp(-(s - START) / 0.12) : 0);
  const rise = 0.12 * smooth(prog(s, STRIKE[team], STRIKE[team] + 0.6)) * (1 - smooth(prog(s, START, START + 0.5)));
  const dais = st.extra('dais' + team, () => { const g = new THREE.Group(), stone = new THREE.Mesh(new THREE.BoxGeometry(2.8, DAIS, 2.8), MAT.stone), trim = new THREE.Mesh(new THREE.BoxGeometry(2.95, 0.12, 2.95), MAT.gold);
    stone.position.y = DAIS / 2; trim.position.y = DAIS - 0.06; g.add(stone, trim); g.traverse(o => { if (o.isMesh) { o.castShadow = o.receiveShadow = true; } }); return g; });
  dais.position.set(K.x, 0, K.z);
  k.position.set(K.x, DAIS + rise, K.z); k.rotation.set(0.1 * hit, kingYaw(team), s > STRIKE[team] && s < START ? 0.015 * Math.sin(s * 29 + K.x) : 0);
  const sp = staffTop(team, s); staff.scale.setScalar(SS); staff.position.set(sp.x, sp.y + rise, sp.z);
  const eye = 0.55 * smooth(prog(s, -1.3, -0.9)) + 0.9 * (s >= STRIKE[team] ? Math.exp(-(s - STRIKE[team]) / 0.5) : 0) + (s >= MUSTER[team] ? 0.5 : 0) - (s > START + 0.8 ? 1.5 * prog(s, START + 0.8, START + 1.4) : 0);
  for (const e of k.userData.eyes) e.material.emissiveIntensity = 5 * Math.max(0, eye);
  for (const g of k.userData.glints) g.material.opacity = Math.min(1, Math.max(0, eye));
  staff.userData.orb.material.emissiveIntensity = 0.3 + 4 * Math.max(0, eye);
  const light = st.extra('light' + team, () => new THREE.PointLight(COL[team].main, 0, 14, 1.6)); light.position.set(K.x, DAIS + 3.2, K.z - 2.2);
  light.intensity = 90 * fade(s, STRIKE[team], START + 0.6, 0.2, 0.6) + 60 * hit + 40 * smooth(prog(s, -1.3, -0.9)) * (s < STRIKE[team] ? 1 : 0);
  aurora(st, team, s);
  for (const t0 of [STRIKE[team], START]) {   // the strike: a flash at the staff's foot and dust
    const a = s - t0; if (a < 0 || a > 0.8) continue;
    const foot = V(staff.position.x, DAIS + 0.15, staff.position.z);
    if (a < 0.35) glint(st, foot, 1 + 6 * (1 - a / 0.35), 0.9 * (1 - a / 0.35), t0 === START ? 0xffe8a0 : COL[team].pale);
    for (let i = 0; i < 6; i++) { const u = a / 0.8, th = i / 6 * Math.PI * 2, d = st.take('dust'); d.position.set(foot.x + Math.cos(th) * (0.3 + 1.6 * u), foot.y + 0.1 + 0.3 * u, foot.z + Math.sin(th) * (0.3 + 1.6 * u)); d.scale.setScalar(0.6 + 1.8 * u); d.material.opacity = 0.5 * (1 - u); }
  }
  tags.push({ pos: headOf(team).add(V(0, 0.7 + rise, 0)), text: team === 'w' ? '백 킹 (NPC)' : '흑 킹 (NPC)', team: 'k', a: 1 });
  return hit;
}

// ---------------------------------------------------------------- the floor's chessboard lit from each king's feet
function floorGrid(st) {
  return st.extra('floorGrid', () => {
    const cells = []; for (let i = 0; i < 15; i++) for (let j = 0; j < 32; j++) cells.push([-14 + 2 * i, 1 + 2 * j]);
    const im = new THREE.InstancedMesh(new THREE.PlaneGeometry(1.84, 1.84).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }), cells.length);
    const m4 = new THREE.Matrix4(), c0 = new THREE.Color(0, 0, 0); cells.forEach(([x, z], n) => { m4.makeTranslation(x, 0.035, z); im.setMatrixAt(n, m4); im.setColorAt(n, c0); });
    im.userData.cells = cells; im.frustumCulled = false; return im;
  });
}
const CW = new THREE.Color(COL.w.main), CB = new THREE.Color(COL.b.main), CG = new THREE.Color(0xffd36a), CC = new THREE.Color();
function lightFloor(st, s) {
  const im = floorGrid(st), keep = fade(s, 0.3, START + 0.6, 0.4, 0.6), bp = bellPulse(s);
  im.userData.cells.forEach(([x, z], n) => {
    let r = 0, g = 0, b = 0;
    for (const team of ['w', 'b']) {
      const a = s - STRIKE[team]; if (a < 0) continue;
      const K = KINGS[team], d = Math.hypot(x - K.x, z - K.z), front = 26 * a, e = (d - front) / 2.4;
      let v = 1.2 * Math.exp(-e * e) * Math.max(0, 1 - a / 2.6);
      const mine = team === 'w' ? x < 0 : x > 0;   // his half of the board keeps his colour once the wave has passed
      if (mine && d < front) v += (0.07 + 0.04 * (((x + 14) / 2 + (z - 1) / 2) % 2 === 0 ? 1 : 0)) * keep + 0.25 * bp * keep;
      const c = team === 'w' ? CW : CB; r += c.r * v; g += c.g * v; b += c.b * v;
    }
    const a2 = s - START;   // the start: a gold wave from the platform outwards
    if (a2 >= 0) { const d = Math.hypot(x, z - HALL.circleZ), e = (d - 30 * a2) / 2.6, v = 1.3 * Math.exp(-e * e) * Math.max(0, 1 - a2 / 1.6); r += CG.r * v; g += CG.g * v; b += CG.b * v; }
    im.setColorAt(n, CC.setRGB(r, g, b));
  });
  im.instanceColor.needsUpdate = true;
}

// ---------------------------------------------------------------- the pillars and those drawn into them
function pillar(st, team, i, s) {
  const [kind, x, z] = TEAM[team][i], rise = STRIKE[team] + 0.2 + i * 0.07, arr = ARR[team][kind];
  if (s < rise) return;
  const up = easeOut(prog(s, rise, rise + 0.35)), after = s - arr, keep = after > 0 ? 1 - prog(after, 0.05, 0.3) : 1, flare = after > 0 && after < 0.25 ? 1 - after / 0.25 : 0;
  if (keep > 0) {
    const h = 12 * up, o = st.take('beam'), c = st.take('beam'), w = 1 + 0.3 * flare;
    o.position.set(x, h / 2, z); o.scale.set(0.7 * w, h, 0.7 * w); o.material.color.setHex(COL[team].main); o.material.opacity = (0.2 + 0.05 * Math.sin(s * 9 + z)) * keep + 0.12 * flare;
    c.position.set(x, h / 2, z); c.scale.set(0.22, h, 0.22); c.material.color.setHex(COL[team].pale); c.material.opacity = 0.32 * keep + 0.18 * flare;
  }
  const g = st.take('glow'); g.position.set(x, 0.045, z); g.scale.set(0.72, 0.72, 1); g.material.color.setHex(COL[team].main); g.material.opacity = 0.15 + 0.45 * up * keep + 0.4 * flare;
}
function comet(st, team, kind, x, z, s, t1, d = 0.3) {   // a runner from the course comes down its pillar, a tail of light above it
  const a = s - t1; if (a < -d) return null;
  const y = a < 0 ? lerp(24, 0, easeIn(prog(a, -d, 0))) : 0;
  const p = place(st, team, kind, x, y, z, yawTo(x, z), 0, a >= 0 ? 0.12 * Math.exp(-7 * a) * Math.sin(a * 26) : 0);
  if (a < 0) { obj(st, team, kind).scale.set(0.8, 1.35, 0.8); for (let i = 1; i <= 4; i++) glint(st, V(x, y + 0.6 + i * 1.2, z), 1.2 - i * 0.2, 0.5 - i * 0.09, i % 2 ? COL[team].main : COL[team].pale); }
  else if (a < 0.35) glint(st, V(x, 0.6, z), 0.5 + 1.8 * (1 - a / 0.35), 0.6 * (1 - a / 0.35), COL[team].pale);
  return p;
}
// those already inside fly across the dark hall to their king: an arc with a tail of his colour
function flightPos(fx, fz, x, z, u) { return V(lerp(fx, x, u), 5 * Math.sin(Math.PI * u), lerp(fz, z, u)); }
function hallWorld(st, s, tags, skip = '') {
  let hit = 0;
  for (const team of ['w', 'b']) {
    hit = Math.max(hit, king(st, team, s, tags));
    TEAM[team].forEach(([kind, x, z, from, fx, fz], i) => {
      pillar(st, team, i, s);
      if (from === 'me' || team + kind === skip) return;
      const arr = ARR[team][kind]; let p;
      if (from === 'hall') {
        if (s < PULL) p = place(st, team, kind, fx, 0, fz, yawTo(fx, fz, headOf(team)), 0, Math.sin(s * 1.7 + fx) * 0.025);
        else if (s < arr) {
          const u = easeIn(prog(s, PULL, arr)), q = flightPos(fx, fz, x, z, u); p = place(st, team, kind, q.x, q.y, q.z, Math.atan2(x - fx, z - fz), 0.5, 0);
          for (let j = 1; j <= 5; j++) { const v = Math.max(0, u - j * 0.05); glint(st, flightPos(fx, fz, x, z, v).add(V(0, 0.6, 0)), 1.5 - j * 0.2, 0.5 - j * 0.08, COL[team].main); }
        } else { const a = s - arr; p = place(st, team, kind, x, 0, z, yawTo(x, z), 0, 0.12 * Math.exp(-7 * a) * Math.sin(a * 26)); }
      } else p = comet(st, team, kind, x, z, s, arr);
      if (p) tags.push(tagOf(team, base(kind), p, 1));
    });
  }
  return hit;
}
// both staffs light the crown: a ray of each king's colour from his staff's light to the crown over the platform
function ray(st, key, a, b, r, color, op) {
  const m = st.extra(key, () => new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 1, 12, 1, true), new THREE.MeshBasicMaterial({ transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false })));
  const d = b.clone().sub(a), L = d.length(); m.position.copy(a).addScaledVector(d, 0.5); m.quaternion.setFromUnitVectors(V(0, 1, 0), d.normalize()); m.scale.set(r, L, r);
  m.material.color.setHex(color); m.material.opacity = op;
}
function crownRays(st, s) {
  const on = fade(s, BEAM, START + 0.15, 0.12, 0.15); if (on <= 0) return;
  const c = st.hall.crown.position.clone().add(V(0, 0.8, 0)), grow = easeOut(prog(s, BEAM, BEAM + 0.3)), beat = 0.7 + 0.3 * Math.sin(s * 14) + 0.6 * bellPulse(s);
  for (const team of ['w', 'b']) {
    const from = staffTop(team, s).add(V(0, 2.74 * SS, 0)), to = from.clone().lerp(c, grow);
    ray(st, 'rayO' + team, from, to, 0.26, COL[team].main, 0.35 * on * beat); ray(st, 'rayC' + team, from, to, 0.07, COL[team].pale, 0.9 * on);
    glint(st, from, 1.2 + 0.4 * beat, 0.8 * on, COL[team].pale);
  }
  glint(st, c, (3 + 2 * beat) * grow, 0.75 * on * grow, 0xfff0c0);
}

// ---------------------------------------------------------------- the rush: stars and broken board flying past
function makeStars() {
  const r = rng(5), n = 1200, pos = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { const th = r() * Math.PI * 2, ph = Math.acos(2 * r() - 1), d = 60 + r() * 60; pos[3 * i] = Math.sin(ph) * Math.cos(th) * d; pos[3 * i + 1] = 400 + Math.cos(ph) * d; pos[3 * i + 2] = Math.sin(ph) * Math.sin(th) * d; }
  const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  return new THREE.Points(g, new THREE.PointsMaterial({ color: 0xffffff, size: 0.5, transparent: true, depthWrite: false }));
}
function tile(st, i) {
  return st.extra('tile' + i, () => {
    const glow = i % 6 === 0, mat = glow ? new THREE.MeshBasicMaterial({ color: COL.w.main, toneMapped: false }) : i % 2 ? MAT.black : MAT.white;
    const m = new THREE.Mesh(new THREE.BoxGeometry(1.7, 0.2, 1.7), mat); m.userData.glow = glow; return m;
  });
}

// ---------------------------------------------------------------- the clip
const CARD5 = { title: '⑤ 두 킹이 부른다', tag: '사용자 안', line: '맵 끝 양 구석의 백 킹 · 흑 킹(플레이어 기물이 아니다)이 각자 자기 팀을 부른다 — 가운데 빈 왕좌를 두고.',
  rows: [['흐름', ['홀이 어두워지고 두 킹의 눈이 뜬다 → 백 킹 \'탁!\', 흑 킹 \'탁!\' — 바닥 체스판이 두 색으로', '갈라져 빛나고 오로라 · 빛기둥 → 팀이 빨려 온다 → 두 지팡이가 왕관을 비추고 3 · 2 · 1 → 함께 \'탁!\'']],
    ['빨려 오는 쪽', ['안에 있던 사람은 어두운 홀을 가로질러 날아가고, 코스의 사람은 빛기둥에 붙잡혀', '별과 부서진 판 사이로 빨려 올라가 킹 앞 자기 기둥으로 내려꽂힌다 — 내 기물 뒤 3인칭 그대로']],
    ['정할 것', ['킹과 팀 자리 (프리비즈: 왕좌 쪽 양 구석, 킹 앞에 두 줄 셋씩)', '먼저 온 사람도 부를지 (프리비즈: 모두 부른다, 지금 규칙: 제자리)']],
    ['만들 것', ['백 · 흑 킹 NPC와 지팡이 치기 · 눈 빛, 팀 색 오로라 (궁극기 시전 것)', '바닥 칸 물결, 빛기둥, 날아가는 길과 빛 터널, 왕관을 비추는 두 빛줄기']]] };
const ZOOM = [[-2.6, 0.22, 55], [-0.6, 0.25, 50], [-0.02, 0.5, 40], [0.12, 0.8, 22], [0.4, 0.72, 26], [0.58, 0.4, 44], [1.2, 0.3, 58], [1.6, 0.3, 60]];
function zoomAt(s) { for (let i = 1; i < ZOOM.length; i++) if (s <= ZOOM[i][0]) { const [a, k0, f0] = ZOOM[i - 1], [b, k1, f1] = ZOOM[i], u = smooth(prog(s, a, b)); return [lerp(k0, k1, u), lerp(f0, f1, u)]; } return ZOOM[ZOOM.length - 1].slice(1); }
const LOOK_A = s => V(-10, 4.4, 62.7).lerp(V(0, 4.2, 61), smooth(prog(s, 0.4, 0.7)));   // the leader looks at her king, then at both

const kings = {
  title: { head: '⑤ 두 킹이 부른다', sub: '맵 끝의 백 킹과 흑 킹이 지팡이로 \'탁!\' — 각자 자기 팀을 빛기둥으로 불러들인다', tag: '사용자 안' },
  dur: 15.3, sheet: [1.4, 2.7, 3.1, 4.5, 5.6, 6.0, 7.4, 8.4, 12.1],
  frame(st, t) {
    const hud = {}, tags = []; let cam, focus = V(0, 0, 50);
    const A = t < 4.9, s = A ? t - 2.6 : t - 3.3;   // the leader's screen runs to 2.3; the straggler's picks up again at 1.6
    const dk = dim(s); st.hall.setLamps(1 - 0.85 * dk); st.hemi.intensity = 0.5 - 0.33 * dk; st.sun.intensity = 2.6 - 1.9 * dk;
    if (A) {   // the leader's screen: dark, the eyes, the two strikes, the board in two colours, the pillars, and her own flight
      lightFloor(st, s); const hit = hallWorld(st, s, tags, 'wqueen');
      const [fx, fz] = [0, 38], [x, z] = [-8, 59.2];
      let q;
      if (s < PULL) q = place(st, 'w', 'queen', fx, 0, fz, Math.atan2(-10 - fx, 62.7 - fz), 0, 0);
      else { const u = easeIn(prog(s, PULL, ARR.w.queen)), p = flightPos(fx, fz, x, z, u); q = place(st, 'w', 'queen', p.x, p.y, p.z, Math.atan2(x - fx, z - fz), 0.5, 0);
        for (let j = 1; j <= 6; j++) glint(st, flightPos(fx, fz, x, z, Math.max(0, u - j * 0.045)).add(V(0, 0.6, 0)), 1.6 - j * 0.2, 0.55 - j * 0.08, COL.w.main); }
      tags.push(tagOf('w', 'queen', q, 1, true));
      if (s > 1.2 && s < PULL) glint(st, q.clone().add(V(0, 0.8, 0)), 1.2 + 2 * prog(s, 1.2, PULL), 0.7, COL.w.main);
      if (s < PULL) { const [k, fov] = zoomAt(s); cam = shake(chase(q, LOOK_A(s), { k, fov }), 0.07 * hit, s); }
      else { const dir = V(x - fx, 0, z - fz).normalize(); cam = chase(q, q.clone().addScaledVector(dir, 20).setY(q.y + 1), { walls: false, dist: 5, up: 2.2, k: 0.2, fov: lerp(60, 78, prog(s, PULL, ARR.w.queen)) }); }
      if (s < 0) { hud.band = { n: s < -1.6 ? 3 : s < -0.6 ? 2 : 1, sub: INSIDE, red: true }; hud.vig = 0.4 + 0.4 * dk; }
      hud.streaks = s > PULL ? 0.6 * prog(s, PULL, PULL + 0.3) : 0;
      hud.me = ME_Q; hud.view = '먼저 온 사람 화면 — 백 퀸';
      hud.caption = s < -1.3 ? '30초가 끝나 간다 — 홀의 불이 꺼진다' : s < 0 ? '어둠 속, 맵 끝 양 구석에서 두 킹의 눈이 뜬다' : s < 0.9 ? '백 킹 \'탁!\' · 흑 킹 \'탁!\' — 바닥 체스판이 두 색으로 갈라져 빛나고, 오로라와 빛기둥'
        : s < PULL ? '두 킹이 자기 팀을 부른다' : '나도 내 킹에게 — 어두운 홀을 가로질러 날아간다';
      hud.sound = cue(s, [[-2.6, -1.3, '음악이 멎는다'], [-1.3, -0.1, '우웅… (두 킹의 눈이 뜬다)'], [-0.1, 0.42, '탁!! (백 킹)'], [0.42, 0.9, '탁!! (흑 킹)'],
        [0.9, PULL, '백 킹 “백의 군대여, 내게로!” · 흑 킹 “흑의 군대여, 모여라!”'], [PULL, 2.4, '쉬이익—']]);
    } else if (s < 2.1) {   // the straggler on the course: a pillar of his king's colour comes down on him and he is sucked up
      const lx = -1.6, z = -8 + 3.9 * (Math.min(s, PULL) - 1.2) + 0.3 * smooth(prog(s, PULL, 1.75));
      st.course.bar.rotation.y = s * 1.6;
      const kn = runner(st, 'b', 'knight', cx(2.2), z + 1.6, 0, s, 2.1); tags.push(tagOf('b', 'knight', kn));
      let y = 0;
      if (s < PULL) runner(st, 'w', 'rook', cx(lx), z, 0, s, 0.5);
      else {
        const u = prog(s, 1.66, 2.1); y = 40 * easeIn(u);
        const p = obj(st, 'w', 'rook'); p.position.set(cx(lx), y, z); p.rotation.set(-0.2, 8 * u * u, 0); p.scale.set(1 - 0.25 * u, 1 + 0.5 * u, 1 - 0.25 * u);
        const col = st.take('beam'); col.position.set(cx(lx), 20, z); col.scale.set(1.0, 40, 1.0); col.material.color.setHex(COL.w.main); col.material.opacity = 0.7 * fade(s, PULL, 2.25, 0.03, 0.2);
        glint(st, V(cx(lx), y + 0.7, z), 2.5 + 3 * Math.exp(-(s - PULL) / 0.1), 0.8, COL.w.pale);
        const kc = st.take('beam'); kc.position.set(cx(2.2), 20, z + 1.6); kc.scale.set(0.9, 40, 0.9); kc.material.color.setHex(COL.b.main); kc.material.opacity = 0.6 * fade(s, PULL + 0.05, 2.25, 0.03, 0.2);
      }
      cam = s < 1.66 ? chase(V(cx(lx), 0, z), V(cx(-0.6), 1.2, z + 20), { walls: false, dist: 4.6, up: 2.3, k: 0.2 })
        : { p: V(cx(lx), 2.3 + y * 0.8, z - 4.6 + y * 0.04), l: V(cx(lx), y + 0.8, z), fov: lerp(55, 85, prog(s, 1.66, 2.1)) };
      focus = V(cx(0), 0, z);
      hud.streaks = prog(s, 1.75, 2.05); hud.tint = { c: '70,210,255', a: 0.2 * prog(s, 1.7, 2.1) }; hud.white = 0.7 * prog(s, PULL, PULL + 0.03) * (1 - prog(s, PULL + 0.03, PULL + 0.15));
      hud.me = ME_R; hud.view = '같은 순간, 늦은 사람 화면 — 백 룩 (아직 코스 위)';
      hud.caption = '순간이동이 아니라 — 내 킹의 빛기둥에 붙잡혀 빨려 올라간다 (옆의 흑 나이트는 흑 킹의 주황 기둥에)';
      hud.sound = cue(s, [[1.2, PULL, '헉헉 (달리는 중)'], [PULL, 1.66, '부웅— (빛기둥이 내려꽂힌다)'], [1.66, 2.1, '휘익— (빨려 올라간다)']]);
    } else if (s < 2.6) {   // the rush: a starry dark, pieces of a broken board flying past, his colour streaking
      const u = prog(s, 2.1, 2.6), p = obj(st, 'w', 'rook');
      p.position.set(0, 400, 0); p.rotation.set(-0.3, 4 + 10 * u, 0.25 * Math.sin(s * 13)); p.scale.set(0.75, 1.5, 0.75);
      st.sky.material.uniforms.darkCol.value.setHex(0x05070f); st.sky.material.uniforms.dark.value = 1;
      st.extra('stars', makeStars);
      for (let i = 0; i < 40; i++) {
        const m = tile(st, i), th = i * 2.399, r = 2.6 + (i % 5) * 1.25, zz = 64 - ((u * 140 + i * 9.7) % 92);
        m.position.set(Math.cos(th + s * 0.6) * r, 400 + Math.sin(th + s * 0.6) * r, zz); m.rotation.set(s * 3 + i, s * 2 + i * 0.5, s * 1.3);
        if (m.userData.glow) glint(st, m.position, 2.2, 0.5, COL.w.main);
      }
      for (let i = 0; i < 6; i++) glint(st, V(Math.sin(i * 2.1 + s * 9) * 1.2, 400.4 + Math.cos(i * 1.7 + s * 7) * 1.1, -1.5 - i * 1.4), 1.2, 0.5, i % 2 ? COL.w.main : COL.w.pale);
      cam = { p: V(0, 401.7, -4.4), l: V(0, 400.6, 2), fov: 85 }; focus = V(0, 400, 0);
      hud.streaks = 0.8; hud.tint = { c: '70,210,255', a: 0.1 }; hud.white = prog(s, 2.5, 2.6);
      hud.me = ME_R; hud.view = '늦은 사람 화면 — 백 룩'; hud.caption = '빛 터널 — 별하늘에 부서진 판이 스쳐 간다 (0.5초)';
      hud.sound = '쉬이이이익—';
    } else {   // down my pillar before my king; the team comes in; the camera swings round to show him; the rays; 3 · 2 · 1; both strike
      lightFloor(st, s); hallWorld(st, s, tags); crownRays(st, s);
      const a = s - ARR.w.rook, y = a < 0 ? lerp(19, 0, easeIn(prog(s, 2.6, ARR.w.rook))) : 0;
      const p = obj(st, 'w', 'rook'); p.position.set(ME.x, y, ME.z); p.rotation.set(s > START ? 0.18 : 0, yawTo(ME.x, ME.z), a >= 0 ? 0.12 * Math.exp(-7 * a) * Math.sin(a * 26) : 0);
      if (a < 0) { p.scale.set(0.8, 1.35, 0.8); for (let i = 1; i <= 4; i++) glint(st, V(ME.x, y + 0.6 + i * 1.2, ME.z), 1.2 - i * 0.2, 0.5 - i * 0.09, i % 2 ? COL.w.main : COL.w.pale); }
      else if (a < 0.35) glint(st, V(ME.x, 0.6, ME.z), 0.5 + 2.4 * (1 - a / 0.35), 0.7 * (1 - a / 0.35), COL.w.pale);
      const me = p.position; tags.push(tagOf('w', 'rook', me, a < 0 ? 0 : 1, true));
      lowerCrown(st, s, BEAM, BEAM + 1.0);
      const kick = countdown(st, s, BELLS[0], hud);
      // the camera swings round me to show my king behind us as he raises his staff, then back to the platform
      const swing = 1.45 * (smooth(prog(s, 3.45, 4.2)) - smooth(prog(s, 4.9, 5.6)));
      const rise = smooth(prog(s, BELLS[0], BELLS[0] + 1.4)), cb = chase(me, LOOK, { dist: 5.0 + 1.5 * rise, up: 4.2 + 2.6 * rise, k: 0.3 + 0.1 * rise, fov: 55 - 5 * kick + 6 * swing / 1.45 }), off = cb.p.clone().sub(me), r = Math.hypot(off.x, off.z), ang = Math.atan2(off.z, off.x) + swing;
      cb.p.set(me.x + Math.cos(ang) * r, cb.p.y + 0.6 * swing, me.z + Math.sin(ang) * r); cb.l.lerp(headOf('w'), 0.55 * swing / 1.45);
      const hi = { p: V(ME.x - 4.1, Math.min(y + 4, 22.5), ME.z + 1.5), l: V(-6, Math.max(0, y - 5), 57.9), fov: 70 };
      cam = shake(mix(hi, cb, smooth(prog(s, 2.95, 3.4))), 0.1 * Math.max(0, 1 - Math.abs(a) * 5) + 0.12 * (s >= START ? Math.exp(-(s - START) / 0.2) : 0), s);
      if (s >= MUSTER.w && s < MUSTER.w + 0.9) hud.big = { text: '백 집결!', a: fade(s, MUSTER.w, MUSTER.w + 0.9, 0.05, 0.3), scale: 1 + 0.3 * (1 - easeOut(prog(s, MUSTER.w, MUSTER.w + 0.2))), size: 78, y: 230, color: COL.w.css };
      hud.white = Math.max(1 - prog(s, 2.6, 2.7), 0.2 * (s >= START ? Math.exp(-(s - START) / 0.15) : 0));
      hud.me = ME_R; hud.view = '늦은 사람 화면 — 백 룩';
      hud.caption = s < 3.05 ? '내 킹 앞, 내 빛기둥으로 내려꽂힌다' : s < MUSTER.w ? '팀원들이 하나씩 빨려 들어와 킹 앞에 줄선다' : s < BEAM ? '모였다 — 뒤에 선 내 킹이 지팡이를 높이 든다'
        : s < BELLS[0] ? '두 킹의 지팡이에서 빛줄기 — 빈 왕좌 위 왕관을 비춘다' : s < START ? '3 · 2 · 1' : '두 킹이 함께 \'탁!\' — 불이 돌아오고 발판이 켜진다';
      hud.sound = cue(s, [[2.6, 3.05, '슈웅— 탁!'], [3.05, MUSTER.w, '슝 · 슝 · 슝 (팀원들이 빨려 온다)'], [MUSTER.w, BEAM, '백 킹 “모였구나. 왕좌를 차지하라!”'], [BEAM, BELLS[0], '지이잉— (두 빛줄기가 왕관에)'],
        ...bells(BELLS[0]).slice(0, 3), [START, START + 0.7, '탁!! 탁!! 빠밤! (두 킹이 함께)']]);
      hud.card = endCard(s, START + 0.8, CARD5);
    }
    return { cam, focus, hud, tags };
  },
};

export const KING_CLIPS = { king: kings };
