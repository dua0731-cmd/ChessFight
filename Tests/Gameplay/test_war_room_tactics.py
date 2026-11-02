"""Authored archive routes and approval-gate safety, not a Unity physics simulation."""
import math
import re
import unittest
import test_war_room_complete as room
import test_royal_palace_courses as courses
from test_obstacle_assets import fields


class WarRoomTactics(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        room.CompleteWarRoom.setUpClass()
        cls.data = room.CompleteWarRoom.data
        cls.names = room.CompleteWarRoom.names
        cls.owners = room.CompleteWarRoom.owners
        cls.scripts = room.CompleteWarRoom.scripts
        cls.boxes = []
        for key, body in cls.data.items():
            if not body.startswith('--- !u!65 '):
                continue
            f = fields(body)
            owner = courses.number(f['m_GameObject'])
            tid = cls.owners[owner]
            active, moving, ancestor = True, False, tid
            while ancestor:
                t = fields(cls.data[str(ancestor)])
                go = courses.number(t['m_GameObject'])
                active &= fields(cls.data[str(go)])['m_IsActive'] == '1'
                moving |= go in room.CompleteWarRoom.bodies
                ancestor = courses.number(t['m_Father'])
            if not active or f['m_Enabled'] != '1':
                continue
            centre, size = courses.vector(f['m_Center']), courses.vector(f['m_Size'])
            corners = [courses.transform(cls.data, tid, tuple(c + s * v * .5 for c, s, v in zip(centre, size, (x, y, z))))
                       for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
            cls.boxes.append(dict(id=int(key), owner=owner, name=cls.names[owner],
                                  trigger=f['m_IsTrigger'] == '1', moving=moving,
                                  lo=tuple(min(p[i] for p in corners) for i in range(3)),
                                  hi=tuple(max(p[i] for p in corners) for i in range(3))))

    def component(self, name):
        matches = [body for body in self.data.values() if 'guid: ' + self.scripts[name] + ',' in body]
        self.assertEqual(len(matches), 1, name)
        return fields(matches[0]), matches[0]

    def transform_named(self, name):
        matches = [self.owners[owner] for owner, value in self.names.items() if value == name]
        self.assertEqual(len(matches), 1, name)
        return matches[0]

    def assert_walkable(self, route, ignore_moving=False):
        # Sample the centre and footprint edges, so a diagonal cannot cut a floor corner.
        floors = [b for b in self.boxes if not b['trigger'] and not b['moving']]
        solids = [b for b in self.boxes if not b['trigger'] and (not ignore_moving or not b['moving'])]
        for start, end in zip(route, route[1:]):
            steps = max(1, math.ceil(math.dist(start, end) / .25))
            for i in range(steps + 1):
                p = tuple(a + (b - a) * i / steps for a, b in zip(start, end))
                for dx, dz in ((0, 0), (-.5, 0), (.5, 0), (0, -.5), (0, .5)):
                    supported = any(abs(b['hi'][1] - p[1]) < .03 and
                                    b['lo'][0] - .01 <= p[0] + dx <= b['hi'][0] + .01 and
                                    b['lo'][2] - .01 <= p[2] + dz <= b['hi'][2] + .01 for b in floors)
                    self.assertTrue(supported, ('Unsupported walking footprint', p, dx, dz))
                for b in solids:
                    # Ignore floors below the feet and lintels above a standing character.
                    if b['hi'][1] <= p[1] + .1 or b['lo'][1] >= p[1] + 2.2:
                        continue
                    self.assertFalse(b['lo'][0] - .5 < p[0] < b['hi'][0] + .5 and
                                     b['lo'][2] - .5 < p[2] < b['hi'][2] + .5,
                                     ('Blocked walking footprint', b['name'], p))

    def test_authored_solo_route_is_supported_and_clear_with_approval_door_closed(self):
        approach, _ = self.component('GiantChessApproach')
        course = self.data[str(courses.number(approach['course']))]
        route = re.search(r'^  route:\n((?:  - .*\n)+)', course, re.M)[1]
        ids = [int(n) for n in re.findall(r'fileID: (\d+)', route)]
        first = ids.index(self.transform_named('Route hall entry'))
        final = ids.index(self.transform_named('Exit - travel forward'))
        self.assertGreater(final, first)
        points = [courses.transform(self.data, tid) for tid in ids[first:final + 1]]
        self.assert_walkable(points)
        # The deprecated decorative door is disabled, including its child collider.
        self.assertNotIn('Decorative central closed door', [b['name'] for b in self.boxes])

    def test_central_shortcut_has_floor_from_hall_to_shared_apron(self):
        hall = self.transform_named('Transition hall - fixed connection geometry')
        route = [courses.transform(self.data, hall, (0, 0, z)) for z in (2, 14, 24, 28, 34)]
        self.assert_walkable(route, ignore_moving=True)
        self.assertTrue(any(b['name'] == 'Approved dispatch bridge' and not b['trigger'] and not b['moving']
                            for b in self.boxes))

    def test_approval_plates_are_separate_and_occupancy_volume_protects_doorway(self):
        gate, _ = self.component('DualApprovalGate')
        first = next(b for b in self.boxes if b['id'] == courses.number(gate['firstPlate']))
        second = next(b for b in self.boxes if b['id'] == courses.number(gate['secondPlate']))
        self.assertTrue(first['trigger'] and second['trigger'])
        self.assertGreater(second['lo'][0] - first['hi'][0], 2)
        self.assertGreaterEqual(float(gate['approvalSeconds']), 1)
        self.assertGreaterEqual(float(gate['openSeconds']), 8)
        volume = next(b for b in self.boxes if b['id'] == courses.number(gate['doorwayVolume']))
        doors = [b for b in self.boxes if b['name'] == 'Sealed orders door']
        self.assertEqual(len(doors), 2)
        self.assertTrue(volume['trigger'])
        for door in doors:
            self.assertTrue(door['moving'])
            for axis in range(3):
                self.assertLessEqual(volume['lo'][axis], door['lo'][axis])
                self.assertGreaterEqual(volume['hi'][axis], door['hi'][axis])
        self.assertGreater(volume['hi'][1] - volume['lo'][1], max(b['hi'][1] - b['lo'][1] for b in doors))
        door_width = doors[0]['hi'][0] - doors[0]['lo'][0]
        self.assertGreaterEqual(float(gate['openDistance']), door_width)


if __name__ == '__main__':
    unittest.main()
