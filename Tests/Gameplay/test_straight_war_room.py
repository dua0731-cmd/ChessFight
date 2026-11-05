"""Straight minimap contracts; not a Unity playthrough."""
import unittest
import re
import test_war_room_complete as room
from test_obstacle_assets import fields
from test_royal_palace_courses import number,vector,transform

class StraightWarRoom(unittest.TestCase):
    @classmethod
    def setUpClass(cls):room.CompleteWarRoom.setUpClass();cls.t=room.CompleteWarRoom()
    def test_monotonic_route_and_all_checkpoints_inside_square(self):
        t=self.t;_,f,b=t.scripts_of('CourseModule')[0];route=t.route(b,'route')
        self.assertTrue(all(abs(p[0])<.01 for p in route))
        self.assertTrue(all(a[2]<=b[2] for a,b in zip(route,route[1:])))
        self.assertEqual(route[0],(0,0,-142));self.assertEqual(route[-1],(0,0,-4))
        for p in t.route(b,'checkpoints'):self.assertIn(p,route)
        self.assertEqual(transform(t.data,number(f['exit'])),(0,0,169))
        self.assertEqual(vector(f['m_Center']) if 'm_Center' in f else (0,20,10),(0,20,10))
    def test_scroll_pits_have_no_fixed_bypass_floor(self):
        t=self.t;bridges=t.scripts_of('RollingMapBridge');self.assertEqual(len(bridges),2)
        for _,f,b in bridges:
            segments=re.findall(r'^  - \{fileID: (\d+)\}',b,re.M)
            self.assertEqual(len(segments),8)
            self.assertNotEqual(number(f['handle']),0)
            origin=transform(t.data,t.owners[number(f['m_GameObject'])])
            for z in (origin[2]+1,origin[2]+15):
                floors=[name for name,lo,hi,_ in t.boxes if lo[0]<0<hi[0] and lo[2]<z<hi[2] and -.5<hi[1]<.1]
                self.assertTrue(floors);self.assertTrue(all(n=='Walkable parchment' for n in floors),floors)
    def test_concept_obstacles_and_preview_sources(self):
        t=self.t
        self.assertEqual(len(t.scripts_of('CommandStampPress')),2)
        self.assertEqual(len(t.scripts_of('TroopFormationSlide')),2)
        self.assertEqual(len(t.scripts_of('TacticalMapBarrage')),0)
        self.assertEqual(len(t.scripts_of('DualApprovalGate')),0)
        labels=[f['label'].strip('"') for _,f,_ in t.scripts_of('WarRoomScoreLabel')]
        self.assertEqual(labels,['1. Nf3','11... Na4','17... Be6','41... Rc2#'])
    def test_hq_and_entrance_share_one_square(self):
        t=self.t
        for name,lo,hi,_ in t.boxes:
            self.assertGreaterEqual(lo[0],-162);self.assertLessEqual(hi[0],162)
            self.assertGreaterEqual(lo[2],-152);self.assertLessEqual(hi[2],172)
