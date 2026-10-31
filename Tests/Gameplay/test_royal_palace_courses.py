"""Saved course asset contracts, not a substitute for a Unity/PhysX playthrough."""
from pathlib import Path
import json
import math
import re
import unittest
from test_obstacle_assets import blocks, fields

ROOT = Path(__file__).resolve().parents[2]
COURSES = ROOT / 'Assets/Maps/RoyalPalace/Courses'

def number(value): return int(re.search(r'fileID: (\d+)', value)[1])
def vector(value): return tuple(float(n) for n in re.findall(r'[xyzw]: ([\d.eE+-]+)',value))
def rotate(q, v):
    x,y,z,w=q; a,b,c=v
    tx,ty,tz=2*(y*c-z*b),2*(z*a-x*c),2*(x*b-y*a)
    return (a+w*tx+y*tz-z*ty,b+w*ty+z*tx-x*tz,c+w*tz+x*ty-y*tx)

def transform(records, tid, point=(0,0,0)):
    f=fields(records[str(tid)])
    scaled=tuple(a*b for a,b in zip(point,vector(f['m_LocalScale'])))
    p=tuple(a+b for a,b in zip(rotate(vector(f['m_LocalRotation']),scaled),vector(f['m_LocalPosition'])))
    parent=number(f['m_Father'])
    return transform(records,parent,p) if parent else p

class RoyalPalaceCourses(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.prefabs=sorted(COURSES.glob('*.prefab'))
        cls.assets={p:blocks(p.read_text()) for p in cls.prefabs}
        cls.guids={re.search(r'guid: (\w+)',p.read_text())[1]:p for p in (ROOT/'Assets').rglob('*.meta') if re.search(r'guid: (\w+)',p.read_text())}
        cls.script={p.stem:re.search(r'guid: (\w+)',Path(str(p)+'.meta').read_text())[1] for p in (ROOT/'Assets').rglob('*.cs') if Path(str(p)+'.meta').exists()}

    def scripts(self,records,name):
        return [(key,fields(b),b) for key,b in records.items() if 'guid: '+self.script[name]+',' in b]

    def test_five_independent_prefabs_and_all_references_resolve(self):
        self.assertEqual(len(self.prefabs),5)
        paths=self.prefabs+[ROOT/'Assets/Scenes/RoyalPalaceCourseTest.unity']
        for path in paths:
            text=path.read_text(); data=blocks(text)
            self.assertEqual(len(data),len(re.findall(r'^--- !u!',text,re.M)))
            for fid,tail in re.findall(r'\{fileID: (\d+)([^}]*)\}',text):
                if 'guid:' in tail:
                    guid=re.search(r'guid: (\w+)',tail)[1]
                    self.assertTrue(guid.startswith('0000000000000000') or guid in self.guids,(path,guid))
                elif int(fid):self.assertIn(fid,data,(path,fid))
            # Reciprocal transform ownership catches broken prefab hierarchies.
            for key,b in data.items():
                f=fields(b)
                if not b.startswith('--- !u!4 ') or 'stripped' in b.splitlines()[0]:continue
                parent=number(f['m_Father'])
                if parent:self.assertRegex(data[str(parent)],r'(?m)^  - \{fileID: '+key+r'\}$')

    def test_connectors_checkpoints_route_and_unit_scale(self):
        for path,data in self.assets.items():
            _,module,body=self.scripts(data,'CourseModule')[0]
            transforms=[(k,fields(b)) for k,b in data.items() if b.startswith('--- !u!4 ')]
            roots=[(k,f) for k,f in transforms if number(f['m_Father'])==0]
            self.assertEqual(len(roots),1)
            self.assertEqual(vector(roots[0][1]['m_LocalScale']),(1,1,1))
            route=re.search(r'  route:\n((?:  - .*\n)+)',body)[1]
            points=[transform(data,int(n)) for n in re.findall(r'fileID: (\d+)',route)]
            length=sum(math.dist(a,b) for a,b in zip(points,points[1:]))
            self.assertGreater(length,550);self.assertLess(length,680)
            cp=re.search(r'  checkpoints:\n((?:  - .*\n)+)',body)[1]
            self.assertEqual(len(re.findall('fileID',cp)),3)
            self.assertEqual(points[0],(0,0,-6))
            self.assertAlmostEqual(points[-1][1],-1)
            self.assertEqual(int(module['targetSeconds']),240)
            # All authored geometry stays inside the reserved placement volume.
            for tid,_ in transforms:
                p=transform(data,int(tid))
                self.assertTrue(-13<=p[0]<=147 and -80<=p[2]<=82,(path,tid,p))

    def test_no_static_floor_under_transport_centres(self):
        for path,data in self.assets.items():
            gos={k:fields(b) for k,b in data.items() if b.startswith('--- !u!1 ')}
            trs={number(fields(b)['m_GameObject']):(k,fields(b)) for k,b in data.items() if b.startswith('--- !u!4 ')}
            floors=[]
            for k,g in gos.items():
                if g['m_Name']=='Fixed route floor':
                    tid,t=trs[int(k)];floors.append((transform(data,int(tid)),vector(t['m_LocalScale'])))
            for name in ('ConveyorChessboard','SpinningDisc','RisingFallingTiles','RoyalDrawbridgeLeaf'):
                for _,f,_ in self.scripts(data,name):
                    tid,t=trs[number(f['m_GameObject'])]
                    sample=transform(data,int(tid),(0,0,(-3 if f.get('reverse')=='1' else 3) if name=='RoyalDrawbridgeLeaf' else 0))
                    for centre,size in floors:
                        self.assertFalse(abs(sample[0]-centre[0])<size[0]/2-.01 and abs(sample[2]-centre[2])<size[2]/2-.01,(path,name,sample,centre))

    def test_jump_pad_actual_fields_and_reachable_landing(self):
        data=self.assets[next(p for p in self.prefabs if 'Vault' in p.name)]
        pads=self.scripts(data,'JumpPad');self.assertEqual(len(pads),2)
        for _,f,_ in pads:
            height=float(f['jumpHeight']);speed=float(f['airMoveSpeed'])
            t=6/min(speed,5.5)
            self.assertGreater(math.sqrt(2*9.81*height)*t-.5*9.81*t*t,1.5)
            self.assertIn('launchCooldown',f)
        for data in self.assets.values():
            for _,f,_ in self.scripts(data,'ConveyorChessboard'):self.assertEqual(float(f['conveyorSpeed']),1.4)
            for _,f,_ in self.scripts(data,'SpinningDisc'):self.assertNotEqual(number(f['geometryRoot']),0)

    def test_manifest_beat_counts_and_complex_hazard_contracts(self):
        # Counts of designed units are independent from component/leaf counts.
        units=[16,16,15,16,16]
        report=json.loads((ROOT/'Tools/RoyalPalace/layout_report.json').read_text())
        self.assertEqual([len(r['hazards']) for r in report],units)
        for path,data in self.assets.items():
            self.assertFalse(self.scripts(data,'WeightedBridge'))
            self.assertFalse(self.scripts(data,'FoldingBridge'))
            self.assertFalse(self.scripts(data,'SpringPillar'))
            for _,f,_ in self.scripts(data,'KnightCavalryCharge'):
                for name in ('KnightRoot','Visual','StartPoint','LandingPoint','ChargeEndPoint','BodyCollider'):
                    self.assertNotEqual(number(f[name]),0)
                self.assertEqual(fields(data[str(number(f['BodyCollider']))])['m_IsTrigger'],'1')
            for _,f,_ in self.scripts(data,'CastlingCrossingBridges'):
                self.assertNotEqual(f['BridgeA'],f['BridgeB'])
                left=transform(data,number(f['LeftDock']));right=transform(data,number(f['RightDock']))
                self.assertGreater(math.dist(left,right),6.2)
                self.assertGreater(2*float(f['DetourDistance']),12.2)
                for key in ('DeckA','DeckB'):
                    collider=fields(data[str(number(f[key]))]);self.assertEqual(collider['m_IsTrigger'],'0')
            for _,f,_ in self.scripts(data,'RoyalDrawbridgeLeaf'):
                self.assertLessEqual(float(f['openAngle']),85)
                self.assertGreaterEqual(float(f['closedSeconds']),5)

if __name__=='__main__':unittest.main()
