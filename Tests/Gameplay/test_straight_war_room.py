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
    def test_books_replace_maps_and_return_to_previous_table(self):
        t=self.t;books=t.scripts_of('ClosingBook');self.assertEqual(len(books),2)
        self.assertEqual(len(t.scripts_of('RollingMapBridge')),0)
        for _,f,b in books:
            origin=transform(t.data,t.owners[number(f['m_GameObject'])])
            recovery=transform(t.data,number(f['recoveryPoint']))
            self.assertIn(recovery[2],(-106,-80))
            self.assertLess(recovery[2],origin[2])
            self.assertEqual(float(f['respawnSeconds']),2)
            for z in (origin[2]+1,origin[2]+15):
                floors=[name for name,lo,hi,_ in t.boxes if lo[0]<0<hi[0] and lo[2]<z<hi[2] and -.1<hi[1]<.1]
                self.assertEqual(floors,['Walkable book pages'])
            hinge=transform(t.data,number(f['cover']))
            self.assertAlmostEqual(hinge[0],-9)
    def test_stamps_unchanged_and_library_walls_have_previews(self):
        t=self.t
        for _,f,_ in t.scripts_of('CommandStampPress'):
            self.assertAlmostEqual(float(f['slamSeconds']),.18)
            self.assertAlmostEqual(float(f['retractSeconds']),2.5)
        walls=[(lo,hi) for name,lo,hi,_ in t.boxes if name.startswith('Royal library gallery wall')]
        self.assertEqual(len(walls),2)
        for _,f,b in t.scripts_of('WarRoomScoreLabel'):
            p=transform(t.data,t.owners[number(f['m_GameObject'])])
            self.assertAlmostEqual(abs(p[0]),37)
        self.assertEqual(len([name for name in t.names.values() if name.startswith('Royal library bookcase')]),13)

    def test_concept_obstacles_and_preview_sources(self):
        t=self.t
        self.assertEqual(len(t.scripts_of('CommandStampPress')),2)
        self.assertEqual(len(t.scripts_of('TroopFormationSlide')),0)
        self.assertEqual(len(t.scripts_of('TypewriterStairs')),1)
        self.assertEqual(len(t.scripts_of('TacticalMapBarrage')),0)
        self.assertEqual(len(t.scripts_of('DualApprovalGate')),0)
        fractions=[float(f['previewFraction']) for _,f,_ in t.scripts_of('WarRoomScoreLabel')]
        self.assertEqual(fractions,[0,.25,.55,.9])
    def test_hq_and_entrance_share_one_square(self):
        t=self.t
        for name,lo,hi,_ in t.boxes:
            self.assertGreaterEqual(lo[0],-162);self.assertLessEqual(hi[0],162)
            self.assertGreaterEqual(lo[2],-152);self.assertLessEqual(hi[2],172)

    def test_asymmetric_library_and_portal(self):
        t=self.t;left=[];right=[]
        for go,name in t.names.items():
            if name.startswith('Royal library bookcase'):
                p=transform(t.data,t.owners[go]);(left if p[0]<0 else right).append(p[2])
        self.assertNotEqual(sorted(left),sorted(right))
        self.assertEqual(len([n for n in t.names.values() if n.startswith('Giant chess conservatory plant')]),4)
        self.assertEqual(len([n for n in t.names.values() if n.startswith('Library gallery houseplant')]),3)
        portal=t.scripts_of('RoyalLibraryPortal');self.assertEqual(len(portal),1)
        _,f,_=portal[0]
        self.assertAlmostEqual(transform(t.data,number(f['threshold']))[2],-136.4)
        self.assertEqual(float(f['revealSeconds']),1.6)
        self.assertNotEqual(number(f['veil']),0)
