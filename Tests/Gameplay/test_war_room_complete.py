"""Saved geometry/visibility contracts for the complete prefab; not Unity physics tests."""
import math
from pathlib import Path
import re
import unittest
from test_obstacle_assets import blocks, fields
from test_royal_palace_courses import number, vector, transform

ROOT=Path(__file__).resolve().parents[2]
PREFAB=ROOT/'Assets/Maps/RoyalPalace/WarRoomComplete.prefab'

def segment_hits(a,b,lo,hi):
    near,far=0.,1.
    for x,y,l,h in zip(a,b,lo,hi):
        d=y-x
        if abs(d)<1e-8:
            if x<l or x>h:return False
        else:
            u,v=(l-x)/d,(h-x)/d
            near=max(near,min(u,v));far=min(far,max(u,v))
            if far<near:return False
    return far>1e-5 and near<1-1e-5

class CompleteWarRoom(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data=blocks(PREFAB.read_text())
        cls.scripts={p.stem:re.search(r'guid: (\w+)',Path(str(p)+'.meta').read_text())[1] for p in (ROOT/'Assets').rglob('*.cs') if Path(str(p)+'.meta').exists()}
        cls.owners={number(fields(b)['m_GameObject']):int(k) for k,b in cls.data.items() if b.startswith('--- !u!4 ')}
        cls.names={int(k):fields(b)['m_Name'] for k,b in cls.data.items() if b.startswith('--- !u!1 ')}
        cls.bodies={number(fields(b)['m_GameObject']) for b in cls.data.values() if b.startswith('--- !u!54 ')}
        cls.boxes=[]
        for k,b in cls.data.items():
            if not b.startswith('--- !u!65 '):continue
            f=fields(b)
            if f['m_IsTrigger']=='1':continue
            owner=number(f['m_GameObject']);tid=cls.owners[owner]
            centre,size=vector(f['m_Center']),vector(f['m_Size'])
            corners=[transform(cls.data,tid,tuple(c+s*v*.5 for c,s,v in zip(centre,size,(x,y,z)))) for x in (-1,1) for y in (-1,1) for z in (-1,1)]
            lo=tuple(min(p[i] for p in corners) for i in range(3));hi=tuple(max(p[i] for p in corners) for i in range(3))
            moving=False;ancestor=tid
            while ancestor:
                t=fields(cls.data[str(ancestor)])
                moving |= number(t['m_GameObject']) in cls.bodies
                ancestor=number(t['m_Father'])
            cls.boxes.append((cls.names[owner],lo,hi,moving))
    def scripts_of(self,name):
        return [(k,fields(b),b) for k,b in self.data.items() if 'guid: '+self.scripts[name]+',' in b]
    def route(self,body,key):
        text=re.search(r'^  '+key+r':\n((?:  - .*\n)+)',body,re.M)[1]
        return [transform(self.data,int(n)) for n in re.findall(r'fileID: (\d+)',text)]
    def test_one_scene_instance_and_self_contained_references(self):
        scene=(ROOT/'Assets/Scenes/GiantChessCourse.unity').read_text()
        guid=re.search(r'guid: (\w+)',Path(str(PREFAB)+'.meta').read_text())[1]
        self.assertEqual(scene.count('PrefabInstance:\n'),1)
        self.assertIn('guid: '+guid,scene)
        self.assertEqual(len(self.scripts_of('GiantChessCourse')),1)
        self.assertEqual(len(self.scripts_of('GiantChessApproach')),1)
        roots=[b for b in self.data.values() if b.startswith('--- !u!4 ') and fields(b)['m_Father']=='{fileID: 0}']
        self.assertEqual(len(roots),1)
        self.assertEqual(len(self.data),len(re.findall(r'^--- !u!',PREFAB.read_text(),re.M)))
        guids={re.search(r'guid: (\w+)',p.read_text())[1] for p in (ROOT/'Assets').rglob('*.meta') if re.search(r'guid: (\w+)',p.read_text())}
        for fid,tail in re.findall(r'\{fileID: (\d+)([^}]*)\}',PREFAB.read_text()):
            if 'guid:' in tail:
                external=re.search(r'guid: (\w+)',tail)[1]
                self.assertTrue(external.startswith('0000000000000000') or external in guids,external)
            elif int(fid):self.assertIn(fid,self.data)
        self.assertFalse((ROOT/'Assets/Scenes/RoyalPalaceCourseTest.unity').exists())
    def test_transition_is_continuous_and_static_walls_do_not_block_walking(self):
        _,a,body=self.scripts_of('GiantChessApproach')[0]
        course=fields(self.data[str(number(a['course']))])
        route=self.route(body,'transitionRoute')
        self.assertEqual(transform(self.data,number(course['exit'])),route[0])
        self.assertEqual(route[0],(0,0,-60));self.assertEqual(route[-1],(0,0,-4))
        self.assertEqual(transform(self.data,number(a['chessEntry'])),route[-1])
        floors=[(lo,hi) for _,lo,hi,moving in self.boxes if not moving and abs(hi[1])<.01]
        # The live controller's existing entry platform covers Z[-20,0].
        tile=float(self.scripts_of('GiantChessCourse')[0][1]['tileSize'])
        floors.append(((-4*tile,-.8,-20),(4*tile,0,0)))
        for start,end in zip(route,route[1:]):
            steps=int(math.dist(start,end)*2)+1
            for j in range(steps+1):
                p=tuple(a+(b-a)*j/steps for a,b in zip(start,end))
                self.assertTrue(any(lo[0]-.01<=p[0]<=hi[0]+.01 and lo[2]-.01<=p[2]<=hi[2]+.01 for lo,hi in floors),p)
                for name,lo,hi,moving in self.boxes:
                    if moving or not(lo[1]<p[1]+1.2<hi[1]):continue
                    self.assertFalse(lo[0]-.5<p[0]<hi[0]+.5 and lo[2]-.5<p[2]<hi[2]+.5,(name,p))
    def test_outside_route_cannot_see_hq_even_when_door_is_open(self):
        _,binding,_=self.scripts_of('GiantChessApproach')[0]
        approach=self.data[str(number(binding['course']))]
        sources=self.route(approach,'route')
        targets=[((x-3.5)*16,.2,(z+.5)*16) for x in range(8) for z in range(8)]+[(-86,44,64),(86,44,64)]
        # Only actual saved HQ/vestibule colliders, excluding the moving reveal door.
        walls=[(lo,hi) for name,lo,hi,moving in self.boxes if not moving and any(n in name for n in ('HQ ','Vestibule ','Archive sight screen','Archive screen'))]
        self.assertGreater(len(walls),10)
        for p in sources:
            for eye in (2,6,12):
                source=(p[0],p[1]+eye,p[2])
                for target in targets:
                    self.assertTrue(any(segment_hits(source,target,lo,hi) for lo,hi in walls),(source,target))
    def test_closed_door_blocks_final_approach_and_volume_covers_the_leaves(self):
        doors=[(lo,hi) for name,lo,hi,_ in self.boxes if name=='Oak command door']
        self.assertEqual(len(doors),2)
        for x in (-4,0,4):
            self.assertTrue(any(segment_hits((x,2,-24),(x,2,16),lo,hi) for lo,hi in doors))
        _,f,_=self.scripts_of('HeadquartersPrivacyDoor')[0]
        volume=fields(self.data[str(number(f['presenceVolume']))])
        self.assertEqual(volume['m_IsTrigger'],'1')
        self.assertGreaterEqual(vector(volume['m_Size'])[1],12)
        self.assertGreaterEqual(float(f['holdOpenSeconds']),1)
    def test_new_hazards_have_telegraphs_and_no_crushing_head_colliders(self):
        presses=self.scripts_of('CommandStampPress');self.assertEqual(len(presses),3)
        self.assertEqual(len(self.scripts_of('CommandPointerSweep')),4)
        self.assertEqual(len(self.scripts_of('AirVent')),1)
        self.assertEqual(len(self.scripts_of('ChessClockGate')),4)
        for _,f,_ in presses:
            self.assertGreaterEqual(float(f['warningSeconds']),1.4)
            self.assertGreaterEqual(float(f['restSeconds']),2)
            self.assertNotEqual(number(f['warningVisual']),0)
            area=fields(self.data[str(number(f['impactArea']))]);self.assertEqual(area['m_IsTrigger'],'1')
            owner=number(f['m_GameObject']);head=self.owners[owner]
            for box in self.data.values():
                if not box.startswith('--- !u!65 '):continue
                tid=self.owners[number(fields(box)['m_GameObject'])]
                while tid:
                    self.assertNotEqual(tid,head,'Press heads must not pin a ragdoll against the floor')
                    tid=number(fields(self.data[str(tid)])['m_Father'])
    def test_entry_spawns_and_board_preview_are_not_blocked_by_saved_walls(self):
        _,game,_=self.scripts_of('GiantChessCourse')[0]
        self.assertEqual(float(game['tileSize']),16);self.assertEqual(float(game['replaySpeed']),5)
        self.assertEqual(float(game['knockbackSpeed']),12)
        depth=float(game['chessSpawnDepth'])
        for i in range(12):
            p=((-1 if i<6 else 1)*16+(i%3-1)*2.5,1.2,-depth-i%2*3)
            for name,lo,hi,moving in self.boxes:
                if moving:continue
                self.assertFalse(all(l<v<h for l,v,h in zip(lo,p,hi)),(name,p))
        self.assertIn(str(number(game['boardPreview'])),self.data)

if __name__=='__main__':unittest.main()
