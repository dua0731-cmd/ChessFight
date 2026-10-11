// 소드 파이트 스킬 프리비즈 — 화면 구성(참고: 킹 러시 스킬 프리비즈 합본)과 재생·내보내기.
import { THREE, buildWorld, Builder, clamp, lerp, smooth, prog } from './engine.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { SKILLS, RULES } from './skills.js';

const W = 1280, H = 720, FPS = 30;
const FONT = '"Noto Sans KR", "Malgun Gothic", sans-serif';
const CSS = { ally: '#4fd6e6', enemy: '#ff9a3c', gold: '#f6c344', violet: '#c9b2ff', white: '#f2f2f2', red: '#ff7d7d', grey: '#c3cadb' };

// ---------- 3D ----------
const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
renderer.setPixelRatio(1); renderer.setSize(W, H, false);
renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 0.95;
renderer.outputColorSpace = THREE.SRGBColorSpace;
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x151b2e);
scene.environment = new THREE.PMREMGenerator(renderer).fromScene(new RoomEnvironment(), 0.04).texture;
scene.environmentIntensity = 0.35;
scene.add(new THREE.HemisphereLight(0xe4ecff, 0x5a4330, 1.0));
const sun = new THREE.DirectionalLight(0xfff1dc, 2.0);
sun.position.set(-9, 18, 7); sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048); sun.shadow.bias = -0.0004; sun.shadow.normalBias = 0.02;
Object.assign(sun.shadow.camera, { left: -16, right: 16, top: 16, bottom: -16, near: 1, far: 50 });
scene.add(sun); scene.add(sun.target);
const world = buildWorld(scene);
const camera = new THREE.PerspectiveCamera(40, W / H, 0.1, 200);

// ---------- 화면 합성 ----------
const out = document.getElementById('out');
const ctx = out.getContext('2d');

function rrect(x, y, w, h, r, fill) { ctx.beginPath(); ctx.roundRect(x, y, w, h, r); ctx.fillStyle = fill; ctx.fill(); }
function text(s, x, y, size, color, weight = 500, align = 'left', base = 'alphabetic') {
  ctx.font = `${weight} ${size}px ${FONT}`; ctx.fillStyle = color; ctx.textAlign = align; ctx.textBaseline = base; ctx.fillText(s, x, y);
}
function measure(s, size, weight = 500) { ctx.font = `${weight} ${size}px ${FONT}`; return ctx.measureText(s).width; }
function wrap(s, size, weight, maxW) {
  const words = s.split(' '), lines = []; let cur = '';
  for (const w of words) { const n = cur ? cur + ' ' + w : w; if (measure(n, size, weight) > maxW && cur) { lines.push(cur); cur = w; } else cur = n; }
  if (cur) lines.push(cur); return lines;
}
function project(p) {
  const v = new THREE.Vector3(...p).project(camera);
  return { x: (v.x + 1) / 2 * W, y: (1 - v.y) / 2 * H, ok: v.z < 1 && v.z > -1 };
}

function drawPanel(skill) {
  const tw = Math.max(measure(skill.name, 30, 700), measure(skill.spec, 15, 400)) + 36;
  rrect(24, 20, tw, 114, 10, 'rgba(14,19,34,0.9)');
  text(skill.piece, 42, 50, 14, '#b9c3da', 500);
  text(skill.name, 42, 88, 30, '#ffffff', 700);
  text(skill.spec, 42, 118, 15, '#cfd6e6', 400);
}
function drawTag(s) {
  const w = measure(s, 17, 700) + 28;
  rrect(24, 140, w, 38, 8, '#f6c344'); text(s, 38, 166, 17, '#1b2034', 700);
}
function drawSpeed(speed) {
  const s = speed < 1 ? `슬로 ${speed.toFixed(1)}x` : '실제 속도 1.0x';
  const w = measure(s, 19, 700) + 28;
  rrect(W - 24 - w, 20, w, 42, 10, 'rgba(14,19,34,0.9)'); text(s, W - 24 - w / 2, 48, 19, '#f6c344', 700, 'center');
  const lw = 150;
  rrect(W - 24 - lw, 70, lw, 64, 10, 'rgba(14,19,34,0.9)');
  rrect(W - 24 - lw + 12, 82, 12, 12, 3, CSS.ally); text('아군 스킬·아군 기물', W - 24 - lw + 30, 93, 13, '#e6ebf5', 500);
  rrect(W - 24 - lw + 12, 106, 12, 12, 3, CSS.enemy); text('적 스킬·적 기물', W - 24 - lw + 30, 117, 13, '#e6ebf5', 500);
}
function drawScore(sc, t) {
  let w = 0, b = 0; for (const e of sc.score || []) if (t >= e.t) e.team === 'white' ? w++ : b++;
  const cx = W / 2;
  rrect(cx - 92, 20, 184, 42, 10, 'rgba(14,19,34,0.9)');
  text('백', cx - 66, 48, 16, '#e6ebf5', 700, 'center');
  text(`${w}`, cx - 30, 49, 24, CSS.ally, 700, 'center'); text(':', cx, 47, 20, '#8f98b0', 700, 'center'); text(`${b}`, cx + 30, 49, 24, CSS.enemy, 700, 'center');
  text('흑', cx + 66, 48, 16, '#e6ebf5', 700, 'center');
  const last = (sc.score || []).filter(e => t >= e.t).pop();
  if (last && t - last.t < 1.2) { const a = 1 - prog(t, last.t + 0.8, last.t + 1.2); ctx.globalAlpha = a; text('장외 +1점', cx, 84, 15, last.team === 'white' ? CSS.ally : CSS.enemy, 700, 'center'); ctx.globalAlpha = 1; }
}
function drawLabels(b, sc, t) {
  for (const L of sc.labels || []) {
    if (t < L.t0 || t >= L.t1) continue;
    let p;
    if (typeof L.at === 'string') { const a = b.actors[L.at]; const q = a.pos(t); p = [q[0], q[1] + (a.lying(t) ? 0.75 : a.h + 0.45) + (L.dy || 0), q[2]]; }
    else p = L.at;
    const s = project(p); if (!s.ok) continue;
    const a = Math.min(prog(t, L.t0, L.t0 + 0.08), 1 - prog(t, L.t1 - 0.12, L.t1));
    const w = measure(L.text, 15, 700) + 20;
    s.x = clamp(s.x, w / 2 + 14, W - w / 2 - 14); s.y = clamp(s.y, 222, H - 160);   // 위 패널·아래 자막과 겹치지 않게
    ctx.globalAlpha = a;
    rrect(s.x - w / 2, s.y - 26, w, 28, 6, 'rgba(16,21,38,0.86)');
    text(L.text, s.x, s.y - 7, 15, CSS[L.col || 'white'], 700, 'center');
    ctx.globalAlpha = 1;
  }
}
function drawCaption(sc, t) {
  const c = (sc.captions || []).find(c => t >= c.t0 && t < c.t1) || (sc.captions || []).filter(c => t >= c.t0).pop();
  if (!c) return;
  const w = measure(c.text, 22, 700) + 40;
  rrect(W / 2 - w / 2, H - 146, w, 46, 9, 'rgba(14,19,34,0.9)');
  text(c.text, W / 2, H - 115, 22, '#ffffff', 700, 'center');
  if (c.sound) {
    const s = '♪ ' + c.sound, sw = measure(s, 15, 500) + 28;
    rrect(W / 2 - sw / 2, H - 94, sw, 30, 8, 'rgba(26,31,50,0.88)');
    text(s, W / 2, H - 73, 15, '#ece4cf', 500, 'center');
  }
}
const SEG = { cyan: '#2fc4d6', white: '#ffffff', grey: '#9aa6c2', red: '#ff8d8d', gold: '#f6c344', violet: '#b9a0ff' };
function drawTimeline(skill, sc, t) {
  const phases = sc.phases || skill.phases, cd = skill.cooldown;
  const x0 = 24, x1 = W - 24, y = H - 44, hgt = 26;
  const total = phases.reduce((s, p) => s + p.d, 0);
  const actW = (x1 - x0) * 0.5, unit = actW / Math.max(total, 1.2);
  let x = x0;
  rrect(x0, y, x1 - x0, hgt, 4, 'rgba(14,19,34,0.9)');
  const el = t - sc.skillStart;
  const segs = [];
  for (const p of phases) { const w = Math.max(p.d * unit, p.min || 56); segs.push({ p, x, w }); x += w + 3; }
  const cdx = x, cdw = x1 - x;
  for (const s of segs) {
    rrect(s.x, y, s.w, hgt, 3, SEG[s.p.c] || SEG.grey);
    text(s.p.n, s.x + s.w / 2, y + 18, 13, s.p.c === 'white' ? '#1b2034' : '#0f1424', 700, 'center');
  }
  rrect(cdx, y, cdw, hgt, 3, '#5f6b8c'); text(`쿨타임 ${cd}초 →`, cdx + cdw / 2, y + 18, 13, '#f0f3fa', 700, 'center');
  // 지금 위치
  let px = x0;
  if (el > 0) {
    let acc = 0, done = false;
    for (const s of segs) { if (el < acc + s.p.d) { px = s.x + s.w * (el - acc) / s.p.d; done = true; break; } acc += s.p.d; }
    if (!done) px = cdx + cdw * clamp((el - acc) / cd, 0, 1);
    ctx.fillStyle = '#ffffff'; ctx.fillRect(px - 1.5, y - 6, 3, hgt + 12);
  }
  text(el > 0 ? `${el.toFixed(2)}s` : '', x1 - 8, y + 18, 13, '#dfe5f2', 500, 'right');
}
function drawTitle(lines) {
  ctx.fillStyle = 'rgba(9,12,22,0.8)'; ctx.fillRect(0, 0, W, H);
  ctx.letterSpacing = '4px'; text(lines.kicker, W / 2, 252, 20, '#a9b4cc', 500, 'center'); ctx.letterSpacing = '0px';
  text(lines.title, W / 2, 350, 60, '#f5efe2', 700, 'center');
  let y = 410;
  for (const l of wrap(lines.desc, 21, 400, 1000)) { text(l, W / 2, y, 21, '#e4e4e4', 400, 'center'); y += 34; }
  if (lines.list) { y += 6; for (const l of lines.list) { text(l, W / 2, y, 17, '#cdd5e6', 500, 'center'); y += 28; } }
  text(lines.foot, W / 2, y + 26, 15, '#8f98b0', 400, 'center');
}

// ---------- 프로그램(영상 하나) ----------
function camAt(sc, t) {
  const k = sc.cam; let i = 0; while (i < k.length - 1 && t >= k[i + 1].t) i++;
  const a = k[i], b = k[Math.min(i + 1, k.length - 1)];
  const u = b === a ? 0 : smooth((t - a.t) / (b.t - a.t));
  return { pos: a.pos.map((v, j) => lerp(v, b.pos[j], u)), look: a.look.map((v, j) => lerp(v, b.look[j], u)) };
}
const TAIL = 0.6;
function skillProgram(skill) {
  const segs = [{ type: 'title', skill, scene: skill.order[0][0], dur: 3.4 }];
  for (const [key, speed] of skill.order) {
    const sc = skill.scenes[key];
    segs.push({ type: 'scene', skill, scene: key, speed, replay: speed >= 1, dur: sc.dur / speed + TAIL });
  }
  return { name: skill.key, segs };
}
const PROGRAMS = {};
for (const s of SKILLS) PROGRAMS[s.key] = skillProgram(s);
PROGRAMS.rules = { name: 'rules', segs: [{ type: 'rules', skill: SKILLS[0], scene: SKILLS[0].order[0][0], dur: 6.5 }] };
PROGRAMS.all = { name: 'all', segs: [...PROGRAMS.rules.segs, ...SKILLS.flatMap(s => PROGRAMS[s.key].segs)] };
const progLen = p => p.segs.reduce((s, g) => s + g.dur, 0);

let live = null; // { skill, key, b }
function sceneFor(skill, key) {
  if (live && live.skill === skill && live.key === key) return live.b;
  if (live) live.b.dispose();
  const sc = skill.scenes[key];
  const b = new Builder(scene, world); b.dur = sc.dur;
  sc.build(b, world);
  b.simulate();
  live = { skill, key, b };
  return b;
}

function renderAt(p, vt) {
  let acc = 0, seg = p.segs[p.segs.length - 1], lt = 0;
  for (const g of p.segs) { if (vt < acc + g.dur) { seg = g; lt = vt - acc; break; } acc += g.dur; }
  if (lt === 0 && vt >= acc) lt = seg.dur;
  const skill = seg.skill, sc = skill.scenes[seg.scene], b = sceneFor(skill, seg.scene);
  let gt = 0;
  if (seg.type === 'scene') gt = Math.min(lt * seg.speed, sc.dur);
  b.update(gt);
  const c = camAt(sc, gt);
  if (seg.type !== 'scene') { // 제목 화면: 천천히 도는 카메라
    const a = lt * 0.05, look = c.look, dx = c.pos[0] - look[0], dz = c.pos[2] - look[2];
    c.pos = [look[0] + dx * Math.cos(a) - dz * Math.sin(a), c.pos[1], look[2] + dx * Math.sin(a) + dz * Math.cos(a)];
  }
  camera.position.set(...c.pos); camera.lookAt(...c.look);
  renderer.render(scene, camera);
  ctx.drawImage(renderer.domElement, 0, 0, W, H);
  if (seg.type === 'title') drawTitle({ kicker: 'CHESS FIGHT · SWORD FIGHT · SKILL PREVIZ', title: `${skill.piece} · ${skill.name}`, desc: skill.desc, foot: '기획서 수치 기반 사전 시각화 · 실제 게임 화면 아님 · 효과음은 자막으로 표기 · 수치는 시험 시작값' });
  else if (seg.type === 'rules') drawTitle(RULES);
  else {
    drawPanel(skill);
    drawTag(seg.replay ? `실제 속도로 다시 보기 · ${sc.tag}` : sc.tag);
    drawSpeed(seg.speed); drawScore(sc, gt); drawLabels(b, sc, gt); drawCaption(sc, gt); drawTimeline(skill, sc, gt);
  }
  if (lt < 0.2) { ctx.fillStyle = `rgba(12,16,28,${1 - lt / 0.2})`; ctx.fillRect(0, 0, W, H); }
  return { seg, gt };
}

// ---------- 화면 조작 ----------
const sel = document.getElementById('prog'), scrub = document.getElementById('scrub'), info = document.getElementById('info');
for (const k of Object.keys(PROGRAMS)) { const o = document.createElement('option'); o.value = k; o.textContent = k; sel.appendChild(o); }
let cur = PROGRAMS[sel.value], vt = 0, playing = false, last = 0;
function show() { const r = renderAt(cur, vt); info.textContent = `${vt.toFixed(2)} / ${progLen(cur).toFixed(2)}s · ${r.seg.type} ${r.seg.scene} · 경기 ${r.gt.toFixed(2)}s`; scrub.value = vt / progLen(cur) * 1000; }
sel.onchange = () => { cur = PROGRAMS[sel.value]; vt = 0; show(); };
scrub.oninput = () => { vt = scrub.value / 1000 * progLen(cur); show(); };
document.getElementById('play').onclick = () => { playing = !playing; last = performance.now(); if (playing) requestAnimationFrame(loop); };
function loop(now) { if (!playing) return; vt += (now - last) / 1000; last = now; if (vt >= progLen(cur)) { vt = 0; } show(); requestAnimationFrame(loop); }

// 프레임을 20장씩 묶어 보낸다(한 장씩 보내면 요청마다 기다림이 길다)
async function exportProgram(name) {
  const p = PROGRAMS[name]; const n = Math.ceil(progLen(p) * FPS);
  let pack = [], first = 0, inflight = [];
  const flush = () => {
    if (!pack.length) return;
    inflight.push(fetch(`/batch/${name}/${first}`, { method: 'POST', headers: { 'X-Sizes': pack.map(b => b.size).join(',') }, body: new Blob(pack) }));
    pack = [];
  };
  for (let f = 0; f < n; f++) {
    renderAt(p, f / FPS);
    if (!pack.length) first = f;
    pack.push(await new Promise(r => out.toBlob(r, 'image/jpeg', 0.93)));
    if (pack.length === 20) { flush(); if (inflight.length > 3) await inflight.shift(); }
    if (f % 30 === 0) info.textContent = `내보내는 중 ${name} ${f}/${n}`;
  }
  flush(); await Promise.all(inflight);
  const r = await fetch(`/encode/${name}`, { method: 'POST' });
  return (await r.text());
}
// 구간만 뽑기 (창이 숨겨져 있으면 비동기 대기가 1초씩 늦어져서, 그림 → JPEG는 동기로 만들고 보내기만 묶는다)
function jpegBytes() {
  const bin = atob(out.toDataURL('image/jpeg', 0.93).slice(23)), a = new Uint8Array(bin.length);
  for (let k = 0; k < bin.length; k++) a[k] = bin.charCodeAt(k);
  return a;
}
async function exportRange(name, f0, f1) {
  const p = PROGRAMS[name], n = Math.ceil(progLen(p) * FPS); f1 = Math.min(f1, n);
  const sends = []; let pack = [], first = f0;
  const send = () => { sends.push(fetch(`/batch/${name}/${first}`, { method: 'POST', headers: { 'X-Sizes': pack.map(b => b.length).join(',') }, body: new Blob(pack) })); pack = []; };
  for (let f = f0; f < f1; f++) { renderAt(p, f / FPS); if (!pack.length) first = f; pack.push(jpegBytes()); if (pack.length === 20) send(); }
  if (pack.length) send();
  await Promise.all(sends);
  return { n, next: f1 };
}
async function encode(name) { return (await fetch(`/encode/${name}`, { method: 'POST' })).text(); }
async function concatAll() { return (await fetch('/concat/all', { method: 'POST', body: ['rules', ...SKILLS.map(s => s.key)].join('\n') })).text(); }
// 스킬 5개를 따로 뽑고, 합본은 규칙 카드 + 5개를 이어 붙인다
async function exportAll() {
  const log = [];
  for (const k of [...SKILLS.map(s => s.key), 'rules']) log.push(await exportProgram(k));
  const r = await fetch('/concat/all', { method: 'POST', body: ['rules', ...SKILLS.map(s => s.key)].join('\n') });
  log.push(await r.text());
  return log;
}
// 프레임 확인용: 한 장면의 여러 시각을 한 장에 모아 저장
async function sheet(name, times, as) {
  const p = PROGRAMS[name]; let i = 0;
  for (const t of times) { renderAt(p, t); const blob = await new Promise(r => out.toBlob(r, 'image/jpeg', 0.9)); await fetch(`/frame/${as}/${i++}`, { method: 'POST', body: blob }); }
  return i;
}
window.previz = { PROGRAMS, renderAt: (n, t) => { cur = PROGRAMS[n]; vt = t; show(); }, exportProgram, exportAll, exportRange, encode, concatAll, sheet, progLen: n => progLen(PROGRAMS[n]) };

await document.fonts.load(`700 20px "Noto Sans KR"`); await document.fonts.load(`500 20px "Noto Sans KR"`); await document.fonts.load(`400 20px "Noto Sans KR"`);
show();
window.previzReady = true;
