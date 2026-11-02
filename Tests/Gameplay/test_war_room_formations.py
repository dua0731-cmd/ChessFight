"""Saved formation clearance and map-sector coverage; Unity contact behavior is not tested."""
import math
from pathlib import Path
import re
import unittest
from test_obstacle_assets import blocks, fields
from test_royal_palace_courses import number, vector, transform

ROOT = Path(__file__).resolve().parents[2]


class WarRoomFormations(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = blocks((ROOT / 'Assets/Maps/RoyalPalace/WarRoomComplete.prefab').read_text())
        cls.transforms = {number(fields(b)['m_GameObject']): int(k)
                          for k, b in cls.data.items() if b.startswith('--- !u!4 ')}
        cls.gos = {int(k): fields(b) for k, b in cls.data.items() if b.startswith('--- !u!1 ')}
        bodies = {number(fields(b)['m_GameObject']) for b in cls.data.values() if b.startswith('--- !u!54 ')}
        cls.ancestors = {}
        for tid in cls.transforms.values():
            parents = []
            parent = tid
            while parent:
                parents.append(parent)
                parent = number(fields(cls.data[str(parent)])['m_Father'])
            cls.ancestors[tid] = parents
        cls.boxes = []
        for key, b in cls.data.items():
            if not b.startswith('--- !u!65 '):
                continue
            f = fields(b)
            tid = cls.transforms[number(f['m_GameObject'])]
            owners = [number(fields(cls.data[str(t)])['m_GameObject']) for t in cls.ancestors[tid]]
            if any(cls.gos[g]['m_IsActive'] == '0' for g in owners):
                continue
            size, centre = vector(f['m_Size']), vector(f['m_Center'])
            corners = [transform(cls.data, tid, tuple(c + s * v * .5 for c, s, v in zip(centre, size, (x, y, z))))
                       for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
            lo = tuple(min(p[i] for p in corners) for i in range(3))
            hi = tuple(max(p[i] for p in corners) for i in range(3))
            cls.boxes.append((int(key), tid, lo, hi, f['m_IsTrigger'] == '1', any(g in bodies for g in owners)))

    def scripts(self, name):
        meta = ROOT / ('Assets/Scripts/Gameplay/ImportedObstacles/' + name + '.cs.meta')
        guid = re.search(r'guid: (\w+)', meta.read_text())[1]
        return [(fields(b), b) for b in self.data.values() if 'guid: ' + guid + ',' in b]

    def test_complete_swept_formations_clear_physical_props_and_walls(self):
        formations = self.scripts('TroopFormationSlide')
        self.assertEqual(len(formations), 2)
        for motion, _ in formations:
            tid = self.transforms[number(motion['m_GameObject'])]
            travel = float(motion['travelDistance'])
            origin = transform(self.data, tid)
            right = transform(self.data, tid, (1, 0, 0))
            sweep = tuple(abs(a - b) * travel for a, b in zip(right, origin))
            hulls = [b for b in self.boxes if not b[4] and tid in self.ancestors[b[1]]]
            self.assertGreaterEqual(len(hulls), 2)
            for _, _, lo, hi, _, _ in hulls:
                lo = tuple(a - d for a, d in zip(lo, sweep))
                hi = tuple(a + d for a, d in zip(hi, sweep))
                for _, other, lower, upper, trigger, moving in self.boxes:
                    if trigger or moving:
                        continue
                    overlap = all(min(h, u) - max(l, v) > .01 for l, h, v, u in zip(lo, hi, lower, upper))
                    owner = number(fields(self.data[str(other)])['m_GameObject'])
                    self.assertFalse(overlap, (origin, self.gos[owner]['m_Name'], lower, upper))

    def test_moving_opening_stays_walkable_without_permanent_straight_lane(self):
        for motion, _ in self.scripts('TroopFormationSlide'):
            tid = self.transforms[number(motion['m_GameObject'])]
            origin = transform(self.data, tid)
            hulls = [(lo[0] - origin[0], hi[0] - origin[0])
                     for _, child, lo, hi, trigger, _ in self.boxes
                     if not trigger and tid in self.ancestors[child] and lo[1] < origin[1] + 1.2 < hi[1]]
            self.assertEqual(len(hulls), 2)
            hulls.sort()
            gap = (hulls[0][1], hulls[1][0])
            self.assertGreaterEqual(gap[1] - gap[0], 3.4)
            travel = float(motion['travelDistance'])
            # Intersect all free X intervals. There must be no always-safe straight stripe.
            permanent_lo, permanent_hi = -6, 6
            floors = [(lo, hi) for _, _, lo, hi, trigger, moving in self.boxes
                      if not trigger and not moving and abs(hi[1] - origin[1]) < .01]
            for i in range(65):
                shift = travel * math.sin(i * 2 * math.pi / 64)
                left, right = gap[0] + shift, gap[1] + shift
                self.assertGreaterEqual(left, -6)
                self.assertLessEqual(right, 6)
                self.assertLessEqual(hulls[0][0] + shift, -6)
                self.assertGreaterEqual(hulls[1][1] + shift, 6)
                permanent_lo = max(permanent_lo, left)
                permanent_hi = min(permanent_hi, right)
                for x in (left + .6, (left + right) / 2, right - .6):
                    p = (origin[0] + x, origin[1], origin[2])
                    self.assertTrue(any(lo[0] <= p[0] <= hi[0] and lo[2] <= p[2] <= hi[2] for lo, hi in floors), p)
            self.assertGreater(permanent_lo, permanent_hi)

    def test_barrage_cells_cover_whole_lane_with_matching_visuals(self):
        maps = self.scripts('TacticalMapBarrage')
        self.assertEqual(len(maps), 1)
        config, body = maps[0]
        arrays = {}
        for name in ('dangerAreas', 'warningVisuals', 'safeVisuals', 'strikeVisuals'):
            raw = re.search(r'^  ' + name + r':\n((?:  - .*\n)+)', body, re.M)[1]
            arrays[name] = [int(i) for i in re.findall(r'fileID: (\d+)', raw)]
            self.assertEqual(len(arrays[name]), 9)
            self.assertEqual(len(set(arrays[name])), 9)
        self.assertEqual(int(config['columns']), 3)
        grid = []
        for cid in arrays['dangerAreas']:
            f = fields(self.data[str(cid)])
            self.assertEqual(f['m_IsTrigger'], '1')
            self.assertEqual(vector(f['m_Size']), (4, 6, 4.5))
            self.assertEqual(vector(f['m_Center']), (0, 3, 0))
            tid = self.transforms[number(f['m_GameObject'])]
            grid.append(transform(self.data, tid))
        for row in range(3):
            self.assertAlmostEqual(abs(grid[row * 3][0] - grid[row * 3 + 2][0]) + 4, 12)
            for col in range(2):
                self.assertAlmostEqual(abs(grid[row * 3 + col][0] - grid[row * 3 + col + 1][0]), 4)
        for row in range(2):
            self.assertAlmostEqual(abs(grid[row * 3][2] - grid[(row + 1) * 3][2]), 4.5)
        for i, area in enumerate(arrays['dangerAreas']):
            sector = self.transforms[number(fields(self.data[str(area)])['m_GameObject'])]
            for name in ('warningVisuals', 'safeVisuals', 'strikeVisuals'):
                visual = arrays[name][i]
                self.assertIn(sector, self.ancestors[self.transforms[visual]])
                self.assertEqual(self.gos[visual]['m_IsActive'], '0')


if __name__ == '__main__':
    unittest.main()
