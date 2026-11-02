"""Square footprint and shortened playable route; timing is a budget, not a playtest."""
import math
import unittest
import test_war_room_complete as room
from test_obstacle_assets import fields
from test_royal_palace_courses import number, transform, vector

class SquareOperations(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        room.CompleteWarRoom.setUpClass()
        cls.t=room.CompleteWarRoom()
    def test_shortened_route_budget_and_active_checkpoints(self):
        t=self.t;_,binding,body=t.scripts_of('GiantChessApproach')[0]
        course=t.data[str(number(binding['course']))]
        points=t.route(course,'route')+t.route(body,'transitionRoute')
        length=sum(math.dist(a,b) for a,b in zip(points,points[1:]))
        self.assertTrue(630<length<650,length)
        self.assertTrue(120<length/5.5+30<180)
        self.assertEqual(fields(course)['targetSeconds'],'150')
        active=[]
        for k,name in t.names.items():
            if name.startswith('Beat '):
                if fields(t.data[str(k)])['m_IsActive']=='1':active.append(int(name.split()[1]))
        self.assertEqual(sorted(active),[1,2,3,6])
        cps=t.route(course,'checkpoints')
        self.assertEqual(cps[1],(24,1,-206))
        self.assertIn(cps[1],points)
        # The new crossover stays on supported floors, without a fixed wall blocking it.
        for x in range(0,73):
            p=(x,1,-206)
            self.assertTrue(any(not moving and abs(hi[1]-1)<.01 and lo[0]<=x<=hi[0] and lo[2]<=p[2]<=hi[2] for _,lo,hi,moving in t.boxes),p)
            for name,lo,hi,moving in t.boxes:
                if not moving and lo[1]<2.2<hi[1]:
                    self.assertFalse(lo[0]-.45<x<hi[0]+.45 and lo[2]-.45<p[2]<hi[2]+.45,(name,p))
    def test_square_shell_and_exit_aperture(self):
        t=self.t
        # Saved outer wall centre lines bound a 160m by 160m square in world space.
        names=('West exterior wall','East exterior wall','Rear exterior wall','Square operations south facade')
        centres={name:tuple((l+h)/2 for l,h in zip(lo,hi)) for name,lo,hi,_ in t.boxes if name in names}
        width=abs(centres[names[0]][0]-centres[names[1]][0])
        depth=abs(centres[names[2]][2]-centres[names[3]][2])
        self.assertEqual(width,160);self.assertEqual(width,depth)
