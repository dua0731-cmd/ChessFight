// Ending summons previz - the places: the audience hall B (the cathedral, with the numbers of the game's graybox,
// CathedralHallSection) and a strip of the endgame course far to the west, for the shots on the way.
import { THREE, V, MAT, TEX, makeCrown, basic, ADD, rng } from './kit.js';

export const HALL = { half: 15, len: 64, height: 24, door: 6, doorH: 10, altarFoot: 44, top: 2.5, near: 50.75, back: 60, circleZ: 55.5, circleR: 3.5 };
export const COURSE = { x: -400, exitZ: 30 };   // the course strip's middle line and its exit arch

function tex(w, h, draw) {
  const c = document.createElement('canvas'); c.width = w; c.height = h; draw(c.getContext('2d'), w, h);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.anisotropy = 16; return t;
}
const marble = tex(512, 512, (g, w) => {
  const n = 8, s = w / n, r = rng(7);
  for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) { g.fillStyle = (i + j) % 2 ? '#3d3632' : '#e9e2d4'; g.fillRect(i * s, j * s, s, s); }
  for (let k = 0; k < 900; k++) { g.strokeStyle = `rgba(${r() < 0.5 ? '120,110,100' : '255,255,255'},${0.05 + r() * 0.08})`; g.lineWidth = 1 + r() * 2;
    g.beginPath(); const x = r() * w, y = r() * w; g.moveTo(x, y); g.bezierCurveTo(x + r() * 40, y + r() * 40, x + r() * 60, y - r() * 30, x + r() * 90, y + r() * 20); g.stroke(); }
  g.strokeStyle = 'rgba(30,25,20,0.35)'; g.lineWidth = 2;
  for (let i = 0; i <= n; i++) { g.beginPath(); g.moveTo(i * s, 0); g.lineTo(i * s, w); g.stroke(); g.beginPath(); g.moveTo(0, i * s); g.lineTo(w, i * s); g.stroke(); }
});

const mesh = (geo, mat) => { const m = new THREE.Mesh(geo, mat); m.castShadow = true; m.receiveShadow = true; return m; };
// A box by its min and max corners.
function span(parent, mat, x0, y0, z0, x1, y1, z1) {
  const m = mesh(new THREE.BoxGeometry(Math.abs(x1 - x0), Math.abs(y1 - y0), Math.abs(z1 - z0)), mat);
  m.position.set((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2); parent.add(m); return m;
}

export function buildHall(scene) {
  const h = HALL, root = new THREE.Group(); scene.add(root);
  const M = {
    wall: new THREE.MeshStandardMaterial({ color: 0xbdb3a3, roughness: 0.95 }),
    column: new THREE.MeshStandardMaterial({ color: 0xe9e1d1, roughness: 0.75 }),
    step: new THREE.MeshStandardMaterial({ color: 0xe2dacb, roughness: 0.8 }),
    top: new THREE.MeshStandardMaterial({ color: 0xd6ccbb, roughness: 0.75 }),
    carpet: new THREE.MeshStandardMaterial({ color: 0x8e1f2c, roughness: 0.9 }),
    velvet: new THREE.MeshStandardMaterial({ color: 0x4b2a78, roughness: 0.85 }),
    door: new THREE.MeshStandardMaterial({ color: 0x5b3a22, roughness: 0.7 }),
    white: new THREE.MeshStandardMaterial({ color: 0xf2eee6, roughness: 0.9 }),
    black: new THREE.MeshStandardMaterial({ color: 0x24201e, roughness: 0.9 }),
    floor: new THREE.MeshStandardMaterial({ map: marble, roughness: 0.42 }),
    bulb: basic(0xffd08a, 1, ADD),
    circle: new THREE.MeshStandardMaterial({ color: 0xc9a23a, emissive: 0xffc94a, emissiveIntensity: 0, metalness: 0.6, roughness: 0.35 }),
  };
  const floor = mesh(new THREE.PlaneGeometry(2 * h.half, h.len), M.floor); floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, h.len / 2);
  marble.repeat.set(h.half / 8, h.len / 16); floor.castShadow = false; root.add(floor);
  // the ground outside (seen from above)
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(400, 400), new THREE.MeshStandardMaterial({ color: 0x2c2a2e, roughness: 1 }));
  ground.rotation.x = -Math.PI / 2; ground.position.set(0, -0.06, 32); ground.receiveShadow = true; root.add(ground);

  // walls: solid to 12 m, high windows every 6 m to 20 m, solid above; the south door 12 m wide, 10 m high
  const w = h.half + 0.6;
  for (const s of [-1, 1]) {
    const x0 = s < 0 ? -w : h.half, x1 = s < 0 ? -h.half : w;
    span(root, M.wall, x0, 0, 0, x1, 12, h.len + 0.6); span(root, M.wall, x0, 20, 0, x1, h.height, h.len + 0.6);
    let z = 0; for (let k = 0; k <= 10; k++) { const m = 3 + 6 * k; span(root, M.wall, x0, 12, z, x1, 20, m - 1.25); z = m + 1.25; }
    span(root, M.wall, x0, 12, z, x1, 20, h.len + 0.6);
  }
  span(root, M.wall, -w, 0, h.len, w, h.height, h.len + 0.6);
  span(root, M.wall, -w, 0, 0, -h.door, h.height, 0.6); span(root, M.wall, h.door, 0, 0, w, h.height, 0.6);
  span(root, M.wall, -h.door, h.doorH, 0, h.door, h.height, 0.6);
  const roof = new THREE.Group(); root.add(roof); span(roof, M.wall, -w, h.height, 0, w, h.height + 0.6, h.len + 0.6);

  // the nave: columns, capitals, the carpet, the banners
  for (let k = 0; k < 7; k++) for (const s of [-1, 1]) {
    const c = mesh(new THREE.CylinderGeometry(1, 1, 20, 28), M.column); c.position.set(s * 9, 10, 6 + 6 * k); root.add(c);
    span(root, M.column, s * 9 - 1.4, 20, 6 + 6 * k - 1.4, s * 9 + 1.4, 20.8, 6 + 6 * k + 1.4);
  }
  span(root, M.carpet, -2, 0, 0.6, 2, 0.02, h.altarFoot);
  span(root, M.white, -h.half + 0.02, 3, 45.5, -h.half + 0.1, 11, 51.5); span(root, M.black, h.half - 0.1, 3, 45.5, h.half - 0.02, 11, 51.5);

  // the altar: grand stair (ten 0.25 m risers on 0.75 m treads), the top, the teams' stairs on each side
  for (let k = 1; k < 10; k++) span(root, M.step, -6, 0, h.altarFoot + (k - 1) * 0.75, 6, k * 0.25, h.altarFoot + k * 0.75);
  span(root, M.top, -6, 0, h.near, 6, h.top, h.back);
  for (const s of [-1, 1]) for (let k = 1; k < 10; k++) {
    const a = s * (6 + 6.75 - (k - 1) * 0.75), b = s * (6 + 6.75 - k * 0.75);
    span(root, M.step, Math.min(a, b), 0, 52, Math.max(a, b), k * 0.25, 58);
  }
  span(root, M.carpet, -2, h.top, h.near, 2, h.top + 0.02, 58);
  const circle = mesh(new THREE.CircleGeometry(h.circleR, 64), M.circle); circle.rotation.x = -Math.PI / 2; circle.position.set(0, h.top + 0.03, h.circleZ); circle.castShadow = false; root.add(circle);
  const glow = new THREE.Mesh(new THREE.RingGeometry(h.circleR - 0.2, h.circleR + 0.5, 64), basic(0xffd36a, 0, ADD)); glow.rotation.x = -Math.PI / 2; glow.position.set(0, h.top + 0.06, h.circleZ); root.add(glow);

  // the throne (one and a half times hall A's), the drape and canopy behind it
  const T = MAT.gold, f = { x: 0, y: h.top, z: 58 }, k = 1.5;
  span(root, T, -k, f.y, f.z, k, f.y + 0.5 * k, f.z + 1.2 * k);
  span(root, T, -1.25 * k, f.y, f.z + 0.9 * k, 1.25 * k, f.y + 2.6 * k, f.z + 1.2 * k);
  for (const s of [-1, 1]) span(root, T, s * 1.25 * k, f.y, f.z, s * k, f.y + 0.9 * k, f.z + 0.9 * k);
  span(root, M.velvet, -4.5, h.top, h.back - 0.3, 4.5, h.top + 15, h.back);
  span(root, M.velvet, -5, h.top + 15, 56, 5, h.top + 15.5, h.back + 0.5);

  // the great doors (two leaves hinged at the jambs; open = 1 swings them into the hall)
  const doors = [-1, 1].map(s => { const p = new THREE.Group(); p.position.set(s * h.door, 0, 0.3); root.add(p);
    const leaf = span(p, M.door, 0, 0, -0.15, -s * h.door, h.doorH, 0.15); return p; });
  const setDoors = open => { doors[0].rotation.y = -open * Math.PI * 0.45; doors[1].rotation.y = open * Math.PI * 0.45; };
  setDoors(0);

  // lamps down the nave and by the altar (a bulb you can see and a light), the light on the throne
  const lamps = [[0, 14, 8], [0, 14, 18], [0, 14, 28], [0, 14, 38], [-9, 9, 54], [9, 9, 54]].map(([x, y, z]) => {
    const l = new THREE.PointLight(0xffc37a, 0, 30, 1.6); l.position.set(x, y, z); root.add(l);
    const b = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.dot, color: 0xffcf86, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
    b.position.set(x, y, z); b.scale.setScalar(2.2); root.add(b); return { l, b };
  });
  const setLamps = level => lamps.forEach((q, i) => { const v = typeof level === 'function' ? level(i) : level; q.l.intensity = 260 * v; q.b.material.opacity = v; q.b.visible = v > 0.01; });
  const spot = new THREE.SpotLight(0xfff0d0, 0, 40, 0.42, 0.6, 1.2); spot.position.set(0, 22, h.circleZ); spot.target.position.set(0, h.top, h.circleZ);
  spot.castShadow = true; spot.shadow.mapSize.set(1024, 1024); root.add(spot, spot.target);

  const crown = makeCrown(1.6); crown.position.set(0, 9.5, h.circleZ); root.add(crown);
  const setCircle = on => { M.circle.emissiveIntensity = 1.4 * on; glow.material.opacity = 0.9 * on; };
  return { root, roof, doors, setDoors, lamps, setLamps, spot, crown, setCircle, circle };
}

// A strip of the endgame course at x = COURSE.x: a 16 m way of boards between low walls, a swinging bar, the exit arch.
export function buildCourse(scene) {
  const g = new THREE.Group(); g.position.x = COURSE.x; scene.add(g);
  const board = new THREE.MeshStandardMaterial({ map: TEX.board, roughness: 0.6 });
  const floor = mesh(new THREE.PlaneGeometry(16, 120), board); floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, 0); floor.castShadow = false; g.add(floor);
  TEX.board.repeat.set(1, 120 / 16);
  const wall = new THREE.MeshStandardMaterial({ map: TEX.brick, roughness: 0.9 });
  for (const s of [-1, 1]) span(g, wall, s * 8, 0, -60, s * 8.6, 1.4, 60);
  const bar = new THREE.Group(); bar.position.set(0, 1.2, 4); g.add(bar);
  span(bar, MAT.ribbon, -7, -0.2, -0.2, 7, 0.2, 0.2); const hub = mesh(new THREE.CylinderGeometry(0.5, 0.5, 2.4, 20), MAT.gold); hub.position.y = -1.2; bar.add(hub);
  const arch = new THREE.Group(); arch.position.set(0, 0, COURSE.exitZ); g.add(arch);
  for (const s of [-1, 1]) span(arch, MAT.gold, s * 7, 0, -0.5, s * 8, 7, 0.5);
  span(arch, MAT.gold, -8, 7, -0.5, 8, 8, 0.5);
  const archFlash = new THREE.Mesh(new THREE.PlaneGeometry(14, 7), basic(0xfff0b0, 0, ADD)); archFlash.position.set(0, 3.5, 0); arch.add(archFlash);
  const shadow = new THREE.Mesh(new THREE.CircleGeometry(1, 48), new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0, depthWrite: false }));
  shadow.rotation.x = -Math.PI / 2; shadow.position.y = 0.04; shadow.scale.set(7, 11, 1); g.add(shadow);
  return { g, bar, archFlash, shadow };
}
