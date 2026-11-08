"""Saved geometry/visibility contracts for the complete prefab; not Unity physics tests."""
import math
from pathlib import Path
import re
import unittest
from test_obstacle_assets import blocks, fields
from test_royal_palace_courses import number, vector, transform

ROOT=Path(__file__).resolve().parents[2]
PREFAB=ROOT/'Assets/Prefabs/Obstacles/WarRoomComplete.prefab'

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
