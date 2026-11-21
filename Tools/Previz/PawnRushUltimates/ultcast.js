// Pawn Rush ultimates previz - the ultimate's cast, one for every piece, and what two pieces' own skills do after it.
// The cast is the same for all, only the names change: Q, the caster's chess square bursts teal under it and rises
// as a square column of light, a teal aurora wraps the caster, a square wave runs over the ground and shows the board
// for a moment, the world drains to grey, a heartbeat, the targets' squares go red one by one with ??, a beat of
// silence, the call. What the skill then does is each piece's own design (the knight's fork and the queen's sacrifice
// stand in here); the stronger the skill, the bigger its show. The colours only say what they mean: teal is the
// caster's move, red is what it attacks, white is light. No camera is ever turned for it: the caster's camera keeps
// the player's direction and the cast only plays with its distance, height, field of view, roll and shake; everyone
// sees the caster's face live in the banner's window instead.
import { THREE, V, TEX, lerp, clamp, prog, smooth, easeOut, easeIn, fade, rng, keepColour, makePiece, NAME, GLYPH } from './stage.js';
import { ACT, finish, teams, centroid, PACK } from './ults.js';
import { queenScene } from './casts.js';
import { buildTown } from './hero.js';
import { glowTiles, emblem, plight, ripple, brokenTiles } from './heroedit.js';
import { sqC, four, followSquare, forkLine } from './knightedit.js';

const TEAL = 0x1fb99a, TEAL2 = 0x3fe0bf, PALE = 0xc8fff0, RED = 0xff2a3a;
/** after Q (s): the heartbeats, the silence, the call; the targets turn red from MARK */
const BEATS = [0.55, 1.05, 1.45, 1.75, 1.95], HOLD = 2.1, GO = 2.4, MARK = 0.95;
/** put something in the banner's live window (layer 2) */
const pipOn = o => { o.traverse(m => m.layers.enable(2)); return o; };
const ADDM = (color, o = {}) => new THREE.MeshBasicMaterial({ color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false, ...o });
/** a 0.12 s hit stop at the impact - a picture-only freeze, as the game's hits already do; no slow motion for anyone */
const hitStop = s => [[0, 0], [s, s], [s + 0.12, s + 0.003], [s + 60.12, s + 60.003]];

/** four thin bars on the ground: a square outline that keeps its line width at any size */
function squareLine(color) {
  const g = new THREE.Group(), mat = ADDM(color), geo = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2);
  for (let i = 0; i < 4; i++) g.add(new THREE.Mesh(geo, mat));
  g.userData.mat = mat;
  g.userData.set = (h, w) => { const [a, b, c, d] = g.children, L = 2 * h + w;
    a.position.set(h, 0, 0); a.scale.set(w, 1, L); b.position.set(-h, 0, 0); b.scale.set(w, 1, L); c.position.set(0, 0, h); c.scale.set(L, 1, w); d.position.set(0, 0, -h); d.scale.set(L, 1, w); };
  return keepColour(g);
}
/** a square wave running out over the ground */
function squareWave(st, c, s0, d, h0, h1, color, o = {}) {
  const g = squareLine(color), base = new THREE.Color(color); g.position.set(c.x, o.y ?? 0.06, c.z);
  st.obj(g, s => { const u = (s - s0) / d; g.visible = u >= 0 && u <= 1; if (!g.visible) return;
    g.userData.set(lerp(h0, h1, easeOut(u)), o.w ?? 0.2); g.userData.mat.color.copy(base).multiplyScalar((o.k ?? 1.4) * Math.pow(1 - u, 1.3)); });
}
/** a still square outline on a square (an aim, a reach) */
function squareMark(st, c, s0, s1, color, o = {}) {
  const g = squareLine(color), base = new THREE.Color(color); g.position.set(c.x, 0.055, c.z); g.userData.set(o.h ?? 1.38, o.w ?? 0.1);
  st.obj(g, s => { const k = fade(s, s0, s1, 0.03, 0.3); g.visible = k > 0; if (!g.visible) return;
    g.userData.mat.color.copy(base).multiplyScalar(k * (o.k ?? 1) * (0.8 + 0.2 * Math.sin(s * 9)) * (1 + 1.2 * Math.exp(-Math.max(0, s - s0) / 0.12))); });
}

// ---------------------------------------------------------------- the cast, the same for every piece
/** the caster's own square: bursts at Q, beats with the heart, holds its breath, flares at the call */
function casterSquare(st, c, T) {
  const fill = new THREE.Mesh(new THREE.PlaneGeometry(2.9, 2.9).rotateX(-Math.PI / 2), ADDM(TEAL)); fill.position.set(c.x, 0.035, c.z); keepColour(fill);
  const frame = squareLine(PALE); frame.position.set(c.x, 0.05, c.z); frame.userData.set(1.5, 0.12);
  const tF = new THREE.Color(TEAL), tL = new THREE.Color(PALE); pipOn(fill); pipOn(frame);
  st.obj(frame, () => {});
  st.obj(fill, s => { const on = fade(s, T.sQ, T.sGo + 0.7, 0.03, 0.5); fill.visible = frame.visible = on > 0; if (!fill.visible) return;
    const k = on * (T.held(s) ? 0.5 : 1) * (0.55 + 0.06 * Math.sin(s * 10) + T.pulse(s));
    fill.material.color.copy(tF).multiplyScalar(k); frame.userData.mat.color.copy(tL).multiplyScalar(0.8 * k); });
}
/** a low curtain of light rising from the caster's square's edges: up at Q, beating, drawn in at the silence, gone at the call */
function squareColumn(st, c, T) {
  const R = 1.5 * Math.SQRT2, mat = ADDM(TEAL2, { map: TEX.vgrad, side: THREE.DoubleSide }), base = new THREE.Color(TEAL2);
  const m = new THREE.Mesh(new THREE.CylinderGeometry(R, R, 1, 4, 1, true).rotateY(Math.PI / 4), mat); keepColour(m);
  st.obj(m, s => { const on = fade(s, T.sQ, T.sGo + 0.6, 0.02, 0.3); m.visible = on > 0; if (!m.visible) return;
    let h = lerp(3, 1.3, smooth(prog(s, T.sQ + 0.05, T.sQ + 0.6))), k = 0.2 + 0.12 * T.pulse(s);
    for (const b of T.B) if (s >= b) h += 0.6 * Math.exp(-(s - b) / 0.16);
    if (s >= T.sHold && s < T.sGo) { const u = smooth(prog(s, T.sHold, T.sHold + 0.2)); h = lerp(h, 0.3, u); k *= lerp(1, 0.8, u); }
    if (s >= T.sGo) { const t = s - T.sGo; h = lerp(0.3, 5, easeOut(Math.min(1, t / 0.2))); k = 0.5 * Math.exp(-t / 0.18); }
    m.scale.set(1, h, 1); m.position.set(c.x, h / 2, c.z); mat.color.copy(base).multiplyScalar(k * on); });
}
/** as the square wave passes, the board's lines show on the ground for a moment */
function boardFlash(st, c, s0, R = 18, v = 22) {
  const list = [];
  for (let i = -7; i <= 7; i++) for (let j = -7; j <= 7; j++) {
    const x = c.x + i * 3 - 1.5, z = c.z + j * 3 - 1.5;
    for (const q of [{ x: x + 1.5, z, h: 1 }, { x, z: z + 1.5, h: 0 }]) { q.d = Math.max(Math.abs(q.x - c.x), Math.abs(q.z - c.z)); if (q.d <= R && Math.abs(q.x) < 12.8) list.push(q); }
  }
  const im = new THREE.InstancedMesh(new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2), ADDM(0xffffff), list.length); im.frustumCulled = false; keepColour(im);
  const m4 = new THREE.Matrix4(), q0 = new THREE.Quaternion(), base = new THREE.Color(TEAL2), k = new THREE.Color();
  list.forEach((q, i) => { m4.compose(V(q.x, 0.045, q.z), q0, q.h ? V(3.08, 1, 0.08) : V(0.08, 1, 3.08)); im.setMatrixAt(i, m4); im.setColorAt(i, k.setRGB(0, 0, 0)); });
  const end = s0 + R / v + 1.6;
  st.obj(im, s => { im.visible = s >= s0 && s <= end; if (!im.visible) return; const t = s - s0, front = v * t;
    list.forEach((q, i) => { const x = (q.d - front) / 1.4, after = t - q.d / v, f = 1.0 * Math.exp(-x * x) + (after > 0 ? 0.35 * Math.exp(-after / 0.55) : 0); im.setColorAt(i, k.copy(base).multiplyScalar(f * (1 - 0.65 * q.d / R))); });
    im.instanceColor.needsUpdate = true; });
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
/** a teal aurora round the caster: three curtains of light turning about it */
function aurora(st, pos, T, sEnd) {
  [[0.72, 2.4, 0.9, 1.3], [0.95, 3.2, -0.6, 1.1], [1.18, 4.0, 0.45, 1.5]].forEach(([r, h, w, arc], i) => {
    const mat = new THREE.ShaderMaterial({ vertexShader: AUR_VS, fragmentShader: AUR_FS, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide,
      uniforms: { t: { value: 0 }, k: { value: 0 }, col: { value: new THREE.Color(TEAL) }, hi: { value: new THREE.Color(PALE) } } });
    const m = new THREE.Mesh(new THREE.CylinderGeometry(r, r * 1.25, h, 64, 8, true, 0, arc * Math.PI), mat); keepColour(m); pipOn(m);
    st.obj(m, s => { const on = fade(s, T.sQ + 0.05, sEnd, 0.35, 0.35); m.visible = on > 0; if (!m.visible) return; const p = pos(s);
      let sc = 1, go = 1; if (T.held(s)) sc = lerp(1, 0.72, smooth(prog(s, T.sHold, T.sHold + 0.2))); if (s >= T.sGo) { sc = lerp(0.72, 1.35, easeOut(Math.min(1, (s - T.sGo) / 0.25))); go = Math.exp(-(s - T.sGo) / 0.15); }
      m.position.set(p.x, p.y - 0.55 + h / 2 * sc, p.z); m.scale.setScalar(sc); m.rotation.y = i * 2.1 + w * s;
      mat.uniforms.t.value = s + i * 3.7; mat.uniforms.k.value = go * on * (0.9 + 0.5 * T.pulse(s)) * (T.held(s) ? 0.6 : 1); });
  });
}
/** teal and white flames rising round a piece */
function tealFlames(st, pos, s0, s1, n = 16) {
  const cols = [TEAL, TEAL2, PALE];
  for (let i = 0; i < n; i++) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.smoke, color: cols[i % 3], transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false })); keepColour(sp); pipOn(sp);
    const th = (i / n) * Math.PI * 2, rate = 1.2 + (i % 5) * 0.12;
    st.obj(sp, s => { const k = fade(s, s0, s1, 0.4, 0.3); sp.visible = k > 0; if (!sp.visible) return; const u = ((s - s0) * rate + i / n) % 1, p = pos(s);
      sp.position.set(p.x + Math.cos(th + s * 2) * 0.6 * (1 - u), p.y - 0.5 + u * 3.0, p.z + Math.sin(th + s * 2) * 0.6 * (1 - u)); const z = 0.8 + 1.6 * u; sp.scale.set(z, z * 1.3, 1); sp.material.opacity = 0.13 * k * (1 - u) * Math.min(1, u * 6); });
  }
}
/** afterimages behind something moving, in a colour that keeps */
function trail(st, pos, s0, s1, color, o = {}) {
  const n = o.n ?? 12, dt = o.dt ?? 0.025;
  for (let i = 1; i <= n; i++) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.dot, color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false })); keepColour(sp);
    st.obj(sp, s => { const ts = s - i * dt; sp.visible = s >= s0 && ts <= s1 && ts >= s0; if (!sp.visible) return; sp.position.copy(pos(ts));
      const k = (o.size ?? 1.1) * (1 - i / (n + 1)); sp.scale.set(k, k, k); sp.material.opacity = (o.op ?? 0.55) * (1 - i / (n + 1)); });
  }
}
/** red light falling from the sky on someone hit: a thin red beam with a pale core */
function hitBeam(st, pos, s0) {
  const g = new THREE.Group(), outer = new THREE.Mesh(new THREE.CylinderGeometry(0.42, 0.42, 50, 16, 1, true), ADDM(0xff2030)), core = new THREE.Mesh(new THREE.CylinderGeometry(0.1, 0.1, 50, 8, 1, true), ADDM(0xff9a9a));
  g.add(outer, core); keepColour(g);
  st.obj(g, s => { const t = s - s0; g.visible = t >= 0 && t <= 0.8; if (!g.visible) return; const p = pos(s0), k = 1 - prog(t, 0.2, 0.8);
    g.position.set(p.x, lerp(80, 25, easeIn(Math.min(1, t / 0.1))), p.z); outer.material.color.setHex(0xff2030).multiplyScalar(0.9 * k); core.material.color.setHex(0xff9a9a).multiplyScalar(0.8 * k); });
}
/** the caster's pose through the cast: rises a little and trembles, then crouches for the silence */
const chargePose = () => (u, t, a) => ({ ...a, run: 0, y: 0.3 * smooth(prog(t, 0.4, 2.0)) + 0.04 * Math.sin(t * 30) * prog(t, 0.6, HOLD) * (t < HOLD ? 1 : 0),
  sq: t < HOLD ? 1 : lerp(1, 0.86, smooth(prog(t, HOLD, HOLD + 0.15))), pitch: -0.22 * smooth(prog(t, 0, 0.6)), roll: t < HOLD ? 0.03 * Math.sin(t * 23) : 0 });

/** The cast. o: { A (caster), kind, skill (its name), nota, sQ, sImp, at (where it lands), targets, sub, aimEnd }.
 *  Sets the grade, light and sky, the knots (a hit stop at the impact), the banner with the live window, the voices,
 *  the music and the marks; the skill adds its own effects and the camera's after-call change. */
function ultCast(st, o) {
  const { A, sQ, sImp } = o, B = BEATS.map(b => sQ + b), sHold = sQ + HOLD, sGo = sQ + GO;
  st.knots = hitStop(sImp);
  const a0 = A.pred(sQ), c = sqC(a0.x, a0.z);
  const T = { sQ, B, sHold, sGo, sImp, c, held: s => s >= sHold && s < sGo,
    pulse: s => { let k = s >= sQ ? 1.4 * Math.exp(-(s - sQ) / 0.16) : 0; for (const b of B) if (s >= b) k += 0.7 * Math.exp(-(s - b) / 0.14); if (s >= sGo) k += 1.6 * Math.exp(-(s - sGo) / 0.2); return k; } };
  // the world drains to grey, darker in the silence; only what glows keeps its colour
  const gray = s => fade(s, sQ, sImp + 2.4, 0.8, 1.6);
  const cI = st.C(sImp);
  st.gradeFn = (s, cc) => { const k = gray(s), hit = cc >= cI && cc < cI + 0.12 ? 1 : 0; let p = 0, hb = 0; for (const b of [sQ, sGo]) if (s >= b) p += Math.exp(-(s - b) / 0.1); for (const b of B) if (s >= b) hb += Math.exp(-(s - b) / 0.15);
    const hold = T.held(s) ? smooth(prog(s, sHold, sHold + 0.15)) : 0;
    return { sat: lerp(1, 0.06, k), contrast: lerp(1, 1.12, k) + 0.3 * hit, dimW: lerp(1, 0.46, k) * (1 - 0.25 * hold), maskOn: k > 0.01 ? 1 : 0, vig: 0.22 + 0.38 * k + 0.12 * Math.min(1, hb) + 0.15 * hold, bloom: 0.6, rgb: 0.005 * Math.min(1, p) + 0.009 * hit, flashA: 0.05 * Math.min(1, p) }; };
  st.lightFn = s => { const k = gray(s); return { sun: 1 - 0.45 * k, hemi: 1 - 0.35 * k }; };
  st.sky.material.uniforms.darkCol.value.set(0x07070b); st.skyFn = s => 0.85 * gray(s);
  const fogA = new THREE.Color(0xd2deec), fogB = new THREE.Color(0x15161a); st.obj(null, s => { st.scene.fog.color.lerpColors(fogA, fogB, gray(s)); });
  // Q: the caster's square, its column, the square wave and the board, the aurora, the flames, the light
  casterSquare(st, c, T); squareColumn(st, c, T);
  squareWave(st, c, sQ, 0.9, 1.5, 19, TEAL2, { k: 1.1 }); squareWave(st, c, sQ + 0.1, 1.0, 1.5, 15, PALE, { w: 0.1, k: 0.6 });
  boardFlash(st, c, sQ + 0.02);
  for (const b of B) squareWave(st, c, b, 0.45, 1.5, 4.5, TEAL2, { w: 0.12, k: 0.7 });
  const pos = s => A.pos(s);
  aurora(st, pos, T, sGo + 0.55);
  tealFlames(st, pos, sQ + 0.12, o.flameEnd ?? sGo + 0.1);
  plight(st, s => A.pos(s, 0.8), sQ + 0.1, sGo + 0.2, 0x1fe0c0, 8, 10); plight(st, s => A.pos(s, 1.6).add(V(0, 0, -1.6)), sQ + 0.1, sGo + 0.2, 0xffffff, 7, 8);
  for (let s = sQ + 0.3, i = 0; s < sGo; s += 0.14, i++) pipOn(keepColour(st.sparks(A.pos(s, -0.3), s, 4, { color: i % 2 ? PALE : TEAL2, speed: 2.2, life: 1.0, grav: -3, size: 0.22 })));
  keepColour(A.pivot); pipOn(A.pivot);
  // the targets: their squares turn red one by one, ?? over each
  o.targets.forEach((e, i) => { const s0 = sQ + MARK + i * 0.16; followSquare(st, e, s0, sImp + 0.25, RED); emblem(st, s => e.pos(s, 1.8), s0, sImp + 1.9, '??', '#e5453a', 1.3); });
  // the call: the square bursts upward, white light
  keepColour(st.flash(A.pos(sGo), sGo, 0.25, 6, PALE, { tex: TEX.dot, op: 0.6 }));
  keepColour(st.pillar(c.x, c.z, sGo, 0.8, 0.45, 40, TEAL2, { rise: 0.08, op: 0.75, hold: 0.25, spread: 0.6 }));
  squareWave(st, c, sGo, 0.8, 1.5, 24, TEAL2, { w: 0.3, k: 1.4 });
  // the impact, the same judgement for all: white light, square waves, !! in the sky
  keepColour(st.flash(V(o.at.x, 1.5, o.at.z), sImp, 0.22, 5, 0xffffff, { tex: TEX.dot, op: 0.3 })); st.shake(sImp, 0.9, 0.7);
  squareWave(st, o.at, sImp, 1.0, 1.5, 22, TEAL2, { w: 0.3, k: 1.8 }); squareWave(st, o.at, sImp + 0.06, 1.2, 1.5, 30, PALE, { w: 0.12, k: 0.8 });
  emblem(st, () => V(o.at.x, 6.5, o.at.z), sImp + 0.04, sImp + 1.8, '!!', '#1fb99a', 3.2, { always: true });
  // the camera's beats (field of view only - the direction stays the player's)
  st.camMods.push({ c0: st.C(sQ), d: 0.45, fov: -4, roll: 0.02 }, ...B.map(b => ({ c0: st.C(b), d: 0.3, fov: -1.6 })), { c0: st.C(sGo), d: 0.8, fov: 11, roll: -0.03 }, { c0: st.C(sImp), d: 0.9, fov: 9, roll: 0.04 });
  // what everyone hears and reads; the banner's window shows the caster's face live
  const face = A.h * 0.78;
  const pip = s => { const u = smooth(prog(s, sQ, sGo)); return { p: V(a0.x + 0.55, face + 0.28 - 0.1 * u, a0.z + lerp(2.9, 2.1, u)), l: V(a0.x, face - 0.05, a0.z), fov: 34 }; };
  st.banners.push({ c0: sQ + 0.05, c1: st.C(sImp + 1.6), text: `백팀 ${NAME[o.kind]} · ${o.skill}!!`, sub: o.sub, glyph: GLYPH[o.kind], color: 'rgba(18,140,120,0.92)', color2: 'rgba(10,80,90,0.92)', pip, pipEnd: st.C(sGo + 0.7), pipTag: '시전자 실시간' });
  st.voices.push({ c0: sQ + 0.15, c1: sHold, text: `${NAME[o.kind]}…`, size: 50, color: '#c8fff0' }, { c0: st.C(sGo), c1: st.C(sGo) + 0.8, text: `${o.skill}!!`, size: 84, color: '#3fe0bf' }, { c0: st.C(sImp) + 0.15, c1: st.C(sImp) + 1.3, text: '탁월한 수!!', size: 74, color: '#3fe0bf', y: 410 });
  st.music.push({ c0: sQ, c1: sHold, text: '경기 음악 뚝 — 심장 소리', bg: 'rgba(24,28,30,0.88)' }, { c0: sHold, c1: sGo, text: '— 정적 —', bg: 'rgba(8,9,11,0.9)' }, { c0: sGo, c1: st.C(sImp + 3.0), text: `${NAME[o.kind]}의 테마 — 드롭!`, bg: 'rgba(12,74,64,0.9)' });
  st.stamp(st.C(sGo) + 0.02, 1.3, o.nota + '!!', { size: 110, color: '#3fe0bf', y: 250, rot: -6 });
  return T;
}
/** the caster's own chase camera: it keeps the player's direction; the cast only plays with its distance, height,
 *  field of view and roll (o.after(s, m, p) is the skill's change after the call) */
function castCam(src, T, o = {}) {
  return (c, s) => {
    const p = V(), n = 6; for (let k = 0; k < n; k++) p.add(src(Math.max(0, s - k * 0.05))); p.multiplyScalar(1 / n);
    const u = smooth(prog(s, T.sQ + 0.1, T.sHold));
    let m = { back: lerp(7, 5.2, u), up: lerp(3.3, 1.6, u), look: lerp(0.8, 1.1, u), ahead: lerp(7, 5, u), fov: lerp(60, 50, u), roll: 0.035 * u };
    if (s >= T.sHold) { const h = smooth(prog(s, T.sHold, T.sGo)); m.back -= 0.35 * h; m.fov -= 1.5 * h; }
    if (s >= T.sGo) { const g = smooth(prog(s, T.sGo, T.sGo + 0.45)); m = { back: lerp(m.back, 7, g), up: lerp(m.up, 3.3, g), look: lerp(m.look, 0.8, g), ahead: lerp(m.ahead, 7, g), fov: lerp(m.fov, 62, g), roll: lerp(m.roll, 0, g) }; if (o.after) m = o.after(s, m, p); }
    return { p: p.clone().add(V(0, m.up, -m.back)), l: p.clone().add(V(0, m.look, m.ahead)), fov: m.fov, roll: m.roll };
  };
}

// ---------------------------------------------------------------- the clips
const SPEC_COMMON = [
  ['공통 시전', 'Q → 2.1초 차오름 (심장 다섯 번) → 0.3초 정적 → 스킬 이름 → 기물 스킬 → !! "탁월한 수!!" → 색이 돌아옴 · 이름만 바뀌고 모든 기물이 같음'],
  ['색', '청록 = 시전자의 수 (내 칸 · 빛기둥 · 오로라 · 판의 줄) · 빨강 = 공격받는 칸과 적 (??) · 흰색 = 빛 — 그 밖의 색은 없음'],
  ['카메라', '방향은 끝까지 플레이어 것 — 연출은 거리 · 높이 · 화각 · 기울기 · 흔들림만 (차오를 때 낮게 붙고, 부를 때 넓게 튕김) · 시전자 얼굴은 모두의 배너 창에 실시간으로'],
  ['조작', '차오르는 2.4초 = 조준 시간 · 다른 유저는 시점도 조작도 그대로 · 슬로모 없이 0.12초 히트 스톱만'],
];
const UC = { opt: '', cut: ACT, cutin: false, dur: 16, tag: '공통 궁극기 시전', cornerHead: '공통 궁극기 시전' };
const KM = [[1, 2], [2, 1], [2, -1], [1, -2], [-1, -2], [-2, -1], [-2, 1], [-1, 2]];

const ultKnight = {
  ...UC, kind: 'knight', nota: 'Nf7+!!', chip: '공통 시전 + 나이트 스킬', cornerTail: '나이트', view: '시전자 (백 나이트)',
  name: '공통 궁극기 시전 — 나이트',
  line: '모든 기물이 같은 시전: 내 칸이 청록으로 터져 빛기둥이 되고, 청록 오로라, 판의 줄, 회색 세상, 심장 박동, 빨간 표적, 정적 — 그다음은 나이트만의 스킬',
  spec: [...SPEC_COMMON, ['나이트 스킬', '(기물마다 따로 디자인) 착지 칸과 거기서 닿는 8칸 · 포크 선 · 맞은 적에 붉은 빛 — 강한 스킬일수록 화려하게']],
  build(st) {
    st.cute = true;
    buildTown(st);
    const sQ = ACT, sGo = sQ + GO, sL = sGo + 1.15;
    const Tm = teams(st, 'knight', { enemy: PACK }), K = Tm.me;
    const L0 = centroid(Tm.enemies, sL), lc = sqC(clamp(L0.x, -6, 6), L0.z);
    K.until(sQ).custom(GO, chargePose()).hop(sL - sGo, lc.x, lc.z, { h: 15 }).custom(0.45, (u, t, a) => ({ ...a, y: 0, sq: u < 0.3 ? lerp(0.65, 1, u / 0.3) : 1, pitch: 0 })).stand(0.6).run(20);
    const hit = [];
    for (const e of Tm.enemies) {
      const p = e.pred(sL), d = Math.hypot(p.x - lc.x, p.z - lc.z), r = rng(Math.round(p.z * 10));
      if (d > 15) { e.run(20); continue; }
      hit.push(e);
      e.until(sL + 0.04 + d * 0.004).fly(2.3, clamp(p.x + (p.x >= lc.x ? 1 : -1) * (3 + r() * 3), -7, 7), p.z - (18 + r() * 6), { h: 11 + r() * 3, spins: 2 + (r() > 0.5 ? 1 : 0), roll: 1.2 }).lie(2.0).getUp(0.5).run(20);
    }
    for (const a of Tm.allies) if (a !== K) a.run(20);
    const T = ultCast(st, { A: K, kind: 'knight', skill: '패밀리 포크', nota: 'Nf7+', sQ, sImp: sL, at: lc, targets: hit, sub: 'Nf7+ · 착지 반경 14 m · 빨간 칸에서 벗어나면 피한다', flameEnd: sL });
    // the knight's own (a stand-in for its separate design): the landing square and its eight squares as the aim, fork lines
    glowTiles(st, four(lc, 0, 1.3), T.B[1], sL + 0.6, TEAL, { pulse: 9 }); squareMark(st, lc, T.B[1], sL + 0.6, PALE, { h: 1.5, w: 0.12 });
    KM.forEach(([a, b], i) => squareMark(st, { x: lc.x + a * 3, z: lc.z + b * 3 }, T.B[1 + (i >> 1)], sL + 0.4, TEAL2, { k: 0.7 }));
    hit.forEach((e, i) => forkLine(st, lc, e, sQ + MARK + i * 0.16 + 0.05, sL + 0.1, RED));
    // the leap and the landing: teal afterimages; teal light on the eight squares, red light on everyone hit
    trail(st, s => K.pos(s), sGo, sL, TEAL2, { n: 14, size: 1.1 });
    KM.forEach(([a, b], i) => keepColour(st.pillar(lc.x + a * 3, lc.z + b * 3, sL + 0.02 + i * 0.03, 1.4, 0.38, 9, TEAL2, { rise: 0.12, op: 0.3 })));
    hit.forEach((e, i) => hitBeam(st, s => e.pos(s), sL + 0.05 + i * 0.05));
    plight(st, () => V(lc.x, 4, lc.z), sL, sL + 0.45, 0xffffff, 110, 40);
    // the camera: the player's own, always behind; in the leap it rises and pulls back with the knight to keep the landing in view
    st.shot(0, castCam(s => K.pos(s), T, { after: (s, m, p) => { const y = Math.max(0, p.y - 0.62), e = smooth(prog(s, sL - 0.5, sL + 0.2)) * (1 - smooth(prog(s, sL + 1.6, sL + 2.6)));
      return { back: m.back + 0.4 * y + 4 * e, up: m.up + 0.12 * y + 4.5 * e, look: m.look - 0.85 * y - 2.2 * e, ahead: m.ahead + 0.5 * y, fov: m.fov + 0.3 * y, roll: m.roll }; } }));
    st.cap(2.7, sQ, '상대 6명이 몰려 20 m 앞 — 내 나이트, 팀 게이지 가득', '평소 경기 음악');
    st.cap(sQ + 0.05, T.B[1], '[공통] Q — 내 칸이 청록으로 터져 빛기둥이 솟고, 청록 오로라 · 판의 줄이 번지고 세상의 색이 빠진다', '음악 뚝 → 심장 "쿵" + 속삭임 "나이트…"');
    st.cap(T.B[1], T.sHold, '[공통] 박동마다 내 칸이 뛰고, 맞을 적 발밑이 하나씩 빨갛게 ?? · [나이트] 착지 칸과 그 8칸이 조준처럼', '쿵 · 쿵 · 쿵 — 점점 빨라짐');
    st.cap(T.sHold, T.sGo, '[공통] — 한 박 정적 — 오로라와 빛기둥이 안으로 빨려든다', '완전한 무음');
    st.cap(T.sGo, st.C(sL), '[공통] "패밀리 포크!!" 내 칸이 하늘로 터진다 · [나이트] 청록 잔상을 끌며 도약', '드롭 + 말 울음');
    st.cap(st.C(sL), st.C(sL + 1.4), '[나이트] 쾅 — 8칸에 청록 빛기둥, 맞은 적마다 붉은 빛 · [공통] 하늘에 !! "탁월한 수!!"', '쾅!! — 0.12초 히트 스톱');
    st.cap(st.C(sL + 1.4), st.C(sL + 3.2), '[공통] 색이 천천히 돌아온다 — 카메라는 처음부터 끝까지 내 방향 그대로', '나이트의 테마 → 경기 음악');
    finish(st, st.C(sL + 3.2));
  },
};

const ultQueen = {
  ...UC, kind: 'queen', nota: 'Q×h7+!!', chip: '공통 시전 + 퀸 스킬', cornerTail: '퀸', view: '시전자 (백 퀸)',
  name: '공통 궁극기 시전 — 퀸',
  line: '같은 시전을 퀸이: 청록 칸 · 빛기둥 · 오로라 · 회색 세상 · 빨간 표적 · 정적까지 똑같고, 부른 다음은 퀸만의 스킬 — 가장 강한 기물이라 가장 화려하게',
  spec: [...SPEC_COMMON, ['퀸 스킬', '(기물마다 따로 디자인) 날아올라 거대한 흰 퀸으로 낙하 · 반경 20 m 빨간 칸 · 바닥 물결 · 깨진 타일 — 가장 강한 스킬이라 가장 크게']],
  build(st) {
    st.cute = true;
    buildTown(st);
    const sQ = ACT, sGo = sQ + GO, sFire = sGo + 0.6, sImp = sGo + 1.55;
    let path = null;
    const giant = new THREE.MeshStandardMaterial({ color: 0xf4efe6, emissive: TEAL, emissiveIntensity: 0.45, roughness: 0.35 });
    const q = queenScene(st, { sImp, sWarn: sQ + MARK, soft: true, pure: true, noDisc: true, giantMat: giant, giantPre: s => (s < sFire ? null : { ...path(s), ry: 0.3 }) });
    const Q = q.Q, q0 = Q.pred(sQ), tgt = V(q.tx, 0, q.tz);
    path = s => { const u = prog(s, sGo, sImp), b = V(q0.x, 0, q0.z).lerp(tgt, smooth(u * 0.85 + u * u * 0.15)); return { x: b.x, y: 4 * 30 * u * (1 - u), z: b.z, k: 1 + 7 * easeIn(u) }; };
    const ctr = s => { if (s < sGo) return Q.pos(s); const p = path(Math.min(s, sImp)); return V(p.x, p.y + 0.72 * p.k, p.z); };
    Q.until(sQ).custom(GO, chargePose()).set({ hide: 1, track: 1 }).custom(sImp - sGo, (u, t, a) => { const p = path(sGo + t); return { ...a, x: p.x, z: p.z }; })
      .hold(3.6).set({ hide: 0, track: 0, x: q0.x, z: q0.z, y: 0, pitch: 0, sq: 1, run: 0 }).stand(0.4).run(20);
    const T = ultCast(st, { A: Q, kind: 'queen', skill: '퀸 희생', nota: 'Q×h7+', sQ, sImp, at: { x: q.tx, z: q.tz }, targets: q.hit, sub: 'Q×h7+ · 반경 20 m · 빨간 칸에서 벗어나면 피한다' });
    // the queen's own (a stand-in for its separate design): the red squares of the whole area, the flight, the giant
    const area = [], tc = sqC(q.tx, q.tz);
    for (let i = -7; i <= 7; i++) for (let j = -7; j <= 7; j++) { const x = tc.x + i * 3, z = tc.z + j * 3, d = Math.hypot(x - q.tx, z - q.tz); if (d < 20 && Math.abs(x) < 12) area.push(...four({ x, z }, (d / 20) * 0.6, 0.32)); }
    glowTiles(st, area, sQ + MARK, sImp + 0.15, RED, { pulse: 12 });
    const white = keepColour(makePiece('queen', 'ally', null, { cute: true }));
    st.obj(white, s => { white.visible = s >= sGo && s < sFire; if (!white.visible) return; const p = path(s); white.position.set(p.x, p.y, p.z); white.scale.setScalar(p.k); white.rotation.set(-0.25, 0, 0); });
    trail(st, ctr, sGo, sImp, TEAL2, { n: 12, dt: 0.022, size: 1.3, op: 0.45 });
    keepColour(st.flash(s => ctr(s), sFire, 0.4, 14, PALE, { tex: TEX.dot, op: 0.8 }));
    keepColour(st.aura(s => V(q.tx, path(Math.min(s, sImp)).y + 6, q.tz), sFire, sImp, TEAL2, 26, { op: 0.4 }));
    for (let s = sFire, i = 0; s < sImp; s += 0.08, i++) keepColour(st.sparks(V(q.tx, path(s).y + 10, q.tz), s, 8, { color: i % 2 ? PALE : TEAL2, speed: 4, life: 0.7, grav: -6 }));
    // the impact: the ground ripples and its tiles break loose; red light on everyone hit; more square waves
    ripple(st, tgt, sImp + 0.01); brokenTiles(st, tgt, sImp + 0.01, sImp + 4.5);
    q.hit.forEach((e, i) => hitBeam(st, s => e.pos(s), sImp + 0.05 + i * 0.04));
    for (const k of [0.12, 0.24]) squareWave(st, tgt, sImp + k, 1.2, 3, 34, TEAL2, { w: 0.35, k: 1.4 });
    plight(st, () => V(q.tx, 5, q.tz), sImp, sImp + 0.4, 0xffffff, 250, 60);
    // the camera: the player's own, behind the queen; it rises with her flight and stays where she fell
    st.shot(0, castCam(s => ctr(Math.min(s, sImp)), T, { after: (s, m, p) => { const y = Math.max(0, p.y - 0.75), t = Math.max(0, s - sImp); return { back: m.back + 0.45 * y + 1.2 * Math.min(t, 3), up: m.up + 0.1 * y + 0.4 * Math.min(t, 3), look: m.look - 0.8 * y, ahead: m.ahead + 0.5 * y, fov: m.fov + 0.25 * y, roll: m.roll }; } }));
    st.cap(2.7, sQ, '상대 6명이 몰려 20 m 앞 — 내 퀸, 팀 게이지 가득', '평소 경기 음악');
    st.cap(sQ + 0.05, T.B[1], '[공통] Q — 나이트 때와 똑같이: 내 칸이 청록으로 터져 빛기둥, 청록 오로라, 판의 줄, 회색 세상', '음악 뚝 → 심장 "쿵" + 속삭임 "퀸…"');
    st.cap(T.B[1], T.sHold, '[공통] 박동마다 칸이 뛰고, 맞을 적 발밑이 빨갛게 ?? · [퀸] 반경 20 m가 빨간 칸으로 번진다', '쿵 · 쿵 · 쿵 — 점점 빨라짐');
    st.cap(T.sHold, T.sGo, '[공통] — 한 박 정적 —', '완전한 무음');
    st.cap(T.sGo, st.C(sImp), '[공통] "퀸 희생!!" · [퀸] 날아올라 거대한 흰 퀸이 되어 빨간 칸 위로', '드롭 + 성가대');
    st.cap(st.C(sImp), st.C(sImp + 1.4), '[퀸] 쾅 — 바닥이 물결치고 타일이 깨져 떠오른다, 맞은 적마다 붉은 빛 · [공통] !! "탁월한 수!!"', '콰과광!! — 0.12초 히트 스톱');
    st.cap(st.C(sImp + 1.4), st.C(sImp + 3.2), '[공통] 색이 돌아온다 — 퀸은 잠시 뒤 제자리에서 다시 달린다', '퀸의 테마 → 경기 음악');
    finish(st, st.C(sImp + 3.2));
  },
};

export const ULT_CASTS = { ultKnight, ultQueen };
export { ultCast, castCam, squareLine, squareWave, squareMark, aurora, tealFlames };
