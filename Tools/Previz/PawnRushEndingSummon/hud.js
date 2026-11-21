// Ending summons previz - what is drawn over the picture. Two layers: the game's own HUD as it stands in the code
// (the top strip, my piece in the corner, the summons band of ThroneContest.SummonBanner, the contest box, name tags)
// and the previz's notes (whose screen this is, a caption, sound cues as subtitles, the plan's title and end card).
import { W, H } from './kit.js';
export const DISP = '"Black Han Sans", "Malgun Gothic", sans-serif';
export const BODY = '"IBM Plex Sans KR", "Malgun Gothic", sans-serif';
const SYM = '"Segoe UI Symbol", "Malgun Gothic", sans-serif';
const C = { ally: '#46d2ff', enemy: '#ff8a2a', gold: '#ffc94a', red: '#ff5a48', cream: '#f3ebdd', ink: '#2a2421' };

function rrect(g, x, y, w, h, r) { g.beginPath(); g.moveTo(x + r, y); g.arcTo(x + w, y, x + w, y + h, r); g.arcTo(x + w, y + h, x, y + h, r); g.arcTo(x, y + h, x, y, r); g.arcTo(x, y, x + w, y, r); g.closePath(); }
function outlined(g, text, x, y, fill, stroke, lw) { g.lineJoin = 'round'; g.lineWidth = lw; g.strokeStyle = stroke; g.strokeText(text, x, y); g.fillStyle = fill; g.fillText(text, x, y); }

/**
 * f: { tags: [{ x, y, text, team: 'w'|'b'|'me', a }], me: { glyph, name, rank }, strip: { dots: [{ p, team, me }] },
 *      contest: { state, w, b }, band: { n, sub, red }, big: { text, a, red, size, y }, vig, streaks,
 *      slate: { a, head, sub, tag }, view, caption, sound, white, black, card }
 */
export function drawHud(g, f) {
  g.clearRect(0, 0, W, H);
  if (f.vig > 0) { const gr = g.createRadialGradient(W / 2, H / 2, H * 0.28, W / 2, H / 2, H * 0.9); gr.addColorStop(0, 'rgba(0,0,0,0)'); gr.addColorStop(1, `rgba(16,0,0,${0.9 * f.vig})`); g.fillStyle = gr; g.fillRect(0, 0, W, H); }
  if (f.tint && f.tint.a > 0) { g.fillStyle = `rgba(${f.tint.c},${f.tint.a})`; g.fillRect(0, 0, W, H); }
  if (f.streaks > 0) streaks(g, f.streaks);
  for (const t of f.tags ?? []) tag(g, t);
  if (f.me) me(g, f.me);
  if (f.strip) strip(g, f.strip);
  if (f.contest) contest(g, f.contest);
  if (f.band) band(g, f.band, f.strip || f.contest ? 64 : 14);
  if (f.big && f.big.a > 0) {
    g.save(); g.globalAlpha = f.big.a; g.textAlign = 'center'; g.textBaseline = 'middle'; const s = f.big.scale ?? 1;
    g.translate(W / 2, f.big.y ?? H * 0.44); g.scale(s, s); g.font = `${f.big.size ?? 130}px ${DISP}`;
    outlined(g, f.big.text, 0, 0, f.big.red ? C.red : '#ffd36a', 'rgba(20,10,0,0.85)', 12); g.restore();
  }
  if (f.white > 0) { g.fillStyle = `rgba(255,252,240,${Math.min(1, f.white)})`; g.fillRect(0, 0, W, H); }
  if (f.black > 0) { g.fillStyle = `rgba(0,0,0,${Math.min(1, f.black)})`; g.fillRect(0, 0, W, H); }
  if (f.view) note(g, f.view, H - 50, '#ffd36a', 18);
  if (f.caption) note(g, f.caption, H - 98, '#f6efe2', 19);
  if (f.sound) {
    g.font = `600 20px ${BODY}`; g.textAlign = 'center'; g.textBaseline = 'middle'; const text = '♪ ' + f.sound, w = g.measureText(text).width + 34;
    g.fillStyle = 'rgba(0,0,0,0.64)'; rrect(g, W - 24 - w, H - 68, w, 38, 19); g.fill(); g.fillStyle = '#ffe7a8'; g.fillText(text, W - 24 - w / 2, H - 49);
  }
  if (f.slate && f.slate.a > 0) slate(g, f.slate);
  if (f.card && f.card.a > 0) card(g, f.card);
}

// A name tag over a piece, as the game draws them: cream for white, dark for black, my own in gold.
function tag(g, t) {
  g.font = `700 15px ${BODY}`; const w = g.measureText(t.text).width + 18;
  g.save(); g.globalAlpha = t.a ?? 1; rrect(g, t.x - w / 2, t.y - 26, w, 24, 7);
  g.fillStyle = t.team === 'b' ? 'rgba(30,26,24,0.88)' : 'rgba(246,240,228,0.92)'; g.fill();
  g.strokeStyle = t.team === 'me' || t.team === 'k' ? C.gold : t.team === 'b' ? C.enemy : C.ally; g.lineWidth = t.team === 'me' || t.team === 'k' ? 3 : 2; g.stroke();
  g.fillStyle = t.team === 'b' ? '#f4ead8' : '#221d1a'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(t.text, t.x, t.y - 13.5); g.restore();
}
// My piece and place, top left.
function me(g, m) {
  g.fillStyle = 'rgba(8,10,16,0.62)'; rrect(g, 16, 12, 262, 42, 12); g.fill();
  g.textBaseline = 'middle'; g.textAlign = 'left'; g.font = `26px ${SYM}`; g.fillStyle = '#fff'; g.fillText(m.glyph, 26, 34);
  g.font = `700 18px ${BODY}`; g.fillStyle = C.gold; g.fillText('나', 58, 34); g.fillStyle = '#fff'; g.font = `600 18px ${BODY}`; g.fillText(m.name, 84, 34);
  if (m.rank) { g.textAlign = 'right'; g.font = `22px ${DISP}`; g.fillStyle = '#dfe6ef'; g.fillText(m.rank, 266, 35); }
}
// The race strip at the top: start to the hall, a dot per piece.
function strip(g, s) {
  const x0 = 470, x1 = 850, y = 33;
  g.fillStyle = 'rgba(8,10,16,0.58)'; rrect(g, 400, 10, 520, 46, 14); g.fill();
  g.strokeStyle = 'rgba(255,255,255,0.35)'; g.lineWidth = 3; g.beginPath(); g.moveTo(x0, y); g.lineTo(x1, y); g.stroke();
  g.font = `600 14px ${BODY}`; g.textBaseline = 'middle'; g.fillStyle = '#cfd8e3'; g.textAlign = 'right'; g.fillText('출발', x0 - 10, y);
  g.textAlign = 'left'; g.fillStyle = C.gold; g.fillText('알현실', x1 + 10, y);
  for (const d of s.dots) {
    const X = x0 + Math.min(1, Math.max(0, d.p)) * (x1 - x0), Y = d.team === 'w' ? y - 8 : y + 8;
    g.beginPath(); g.arc(X, Y, d.me ? 7 : 5.5, 0, Math.PI * 2); g.fillStyle = d.team === 'w' ? C.cream : C.ink; g.fill();
    g.lineWidth = d.me ? 3 : 2.2; g.strokeStyle = d.me ? C.gold : d.team === 'w' ? C.ally : C.enemy; g.stroke();
  }
}
// ThroneContest's box: white's bar, who holds the platform, black's bar.
function contest(g, c) {
  const w = 600, x = (W - w) / 2, y = 10, h = 52;
  g.fillStyle = 'rgba(15,13,10,0.86)'; g.fillRect(x, y, w, h);
  const side = 200;
  for (const [team, bx, v] of [['w', x + 10, c.w ?? 0], ['b', x + w - 10 - side, c.b ?? 0]]) {
    g.fillStyle = 'rgba(255,255,255,0.14)'; g.fillRect(bx, y + 28, side, 12);
    g.fillStyle = team === 'w' ? C.cream : '#57504a'; g.fillRect(team === 'w' ? bx : bx + side * (1 - v), y + 28, side * v, 12);
    g.font = `700 14px ${BODY}`; g.textBaseline = 'middle'; g.textAlign = team === 'w' ? 'left' : 'right'; g.fillStyle = team === 'w' ? C.cream : '#d8cfc4';
    g.fillText(team === 'w' ? '백팀' : '흑팀', team === 'w' ? bx : bx + side, y + 15);
  }
  g.textAlign = 'center'; g.font = `700 16px ${BODY}`; g.fillStyle = c.hot ? C.gold : '#f2eadb'; g.fillText(c.state, W / 2, y + 27);
}
// ThroneContest.SummonBanner: "왕의 부름까지 N" and the line for those inside or still on the way.
function band(g, b, y) {
  const w = 560, x = (W - w) / 2;
  g.fillStyle = 'rgba(15,13,10,0.86)'; g.fillRect(x, y, w, 70);
  g.textAlign = 'center'; g.textBaseline = 'middle';
  g.font = `32px ${DISP}`; g.fillStyle = b.red ? C.red : '#ffd873'; g.fillText(`왕의 부름까지 ${b.n}`, W / 2, y + 26);
  g.font = `600 15px ${BODY}`; g.fillStyle = '#f5ebd1'; g.fillText(b.sub, W / 2, y + 54);
}
function streaks(g, a) {
  g.save(); g.globalAlpha = a; g.strokeStyle = 'rgba(255,255,255,0.75)';
  for (let i = 0; i < 46; i++) {
    const ang = i * 2.399, r0 = 220 + (i * 37) % 160, r1 = r0 + 140 + (i * 53) % 220;
    g.lineWidth = 1 + (i % 3); g.beginPath(); g.moveTo(W / 2 + Math.cos(ang) * r0, H / 2 + Math.sin(ang) * r0 * 0.62); g.lineTo(W / 2 + Math.cos(ang) * r1, H / 2 + Math.sin(ang) * r1 * 0.62); g.stroke();
  }
  g.restore();
}
// The plan's name on black before its clip.
function slate(g, t) {
  g.save(); g.globalAlpha = t.a; g.fillStyle = '#0c0a09'; g.fillRect(0, 0, W, H);
  g.textAlign = 'left'; g.textBaseline = 'alphabetic'; g.font = `72px ${DISP}`; g.fillStyle = '#ffd36a'; g.fillText(t.head, 96, 340);
  if (t.tag) { const hw = g.measureText(t.head).width; g.font = `700 21px ${BODY}`; const tw = g.measureText(t.tag).width + 24; g.fillStyle = '#ffd36a'; rrect(g, 116 + hw, 294, tw, 36, 9); g.fill(); g.fillStyle = '#1a1410'; g.textBaseline = 'middle'; g.fillText(t.tag, 128 + hw, 313); g.textBaseline = 'alphabetic'; }
  g.font = `600 27px ${BODY}`; g.fillStyle = '#f6efe2'; g.fillText(t.sub, 98, 392);
  g.font = `500 17px ${BODY}`; g.fillStyle = 'rgba(240,230,210,0.55)'; g.fillText('폰 러시 엔딩 · 30초 소환 연출 프리비즈', 98, 440);
  g.restore();
}
// A previz note at the bottom left (whose screen, what happens).
function note(g, text, y, color, size) {
  g.font = `600 ${size}px ${BODY}`; g.textAlign = 'left'; g.textBaseline = 'middle'; const w = Math.min(W - 420, g.measureText(text).width + 28);
  g.fillStyle = 'rgba(12,10,9,0.74)'; rrect(g, 18, y - 19, w, 38, 9); g.fill(); g.fillStyle = color; g.fillText(text, 32, y + 1);
}

// The end card: the plan's name, one line, and what is good, what worries, what to make.
function card(g, c) {
  g.save(); g.globalAlpha = c.a; g.fillStyle = 'rgba(12,10,9,0.92)'; g.fillRect(0, 0, W, H);
  g.textAlign = 'left'; g.textBaseline = 'alphabetic';
  g.font = `52px ${DISP}`; g.fillStyle = '#ffd36a'; g.fillText(c.title, 80, 160);
  if (c.tag) { const tw0 = g.measureText(c.title).width; g.font = `700 20px ${BODY}`; const w = g.measureText(c.tag).width + 22; g.fillStyle = '#ffd36a'; rrect(g, 98 + tw0, 124, w, 34, 8); g.fill(); g.fillStyle = '#1a1410'; g.textBaseline = 'middle'; g.fillText(c.tag, 109 + tw0, 142); g.textBaseline = 'alphabetic'; }
  g.font = `600 24px ${BODY}`; g.fillStyle = '#f2eadb'; g.fillText(c.line, 82, 212);
  let y = 292;
  for (const [k, v] of c.rows) {
    g.font = `700 22px ${BODY}`; g.fillStyle = '#ffd36a'; g.fillText(k, 82, y);
    g.font = `500 21px ${BODY}`; g.fillStyle = '#efe6d6';
    for (const line of [].concat(v)) { g.fillText(line, 210, y); y += 34; }
    y += 22;
  }
  g.font = `500 16px ${BODY}`; g.fillStyle = 'rgba(240,230,210,0.6)'; g.fillText('사전 시각화 — 실제 게임 화면이 아니다 · 소리는 자막(♪) · 무대: 국왕 알현실 B 대성당 · 모든 화면은 각자 자기 기물 뒤 3인칭', 82, H - 46);
  g.restore();
}

/** The fixed strings of the HUD, for loading the web fonts' Korean subsets before the first frame. */
export const HUD_TEXT = '폰 러시 엔딩 30초 소환 연출 프리비즈 왕의 부름까지 0123456789 출발 알현실 나 위 백팀 흑팀 ♪ 사전 시각화 실제 게임 화면이 아니다 소리는 자막 무대 국왕 알현실 B 대성당 모든 화면은 각자 자기 기물 뒤 3인칭 · — ( ) !';
