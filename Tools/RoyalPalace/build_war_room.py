#!/usr/bin/env python3
"""R118 complete war-room prefab. --write creates it; --overwrite discards edits;
--check is read-only. The scene keeps a single instance plus its camera and light.
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path
import build_courses as kit

ROOT=kit.ROOT
PREFAB='Assets/Maps/RoyalPalace/WarRoomComplete.prefab'
SCENE='Assets/Scenes/GiantChessCourse.unity'
SOURCE='Assets/Maps/RoyalPalace/Courses/3-1_RoyalWarRoom.prefab'
REPORT='Tools/RoyalPalace/war_room_report.json'

def blocks(text):
    return {int(m[1]):b for b in re.split(r'(?=^--- !u!)',text,flags=re.M) if (m:=re.match(r'--- !u!\d+ &(\d+)',b))}
def fid(block,key): return int(re.search(r'^  '+key+r': \{fileID: (\d+)',block,re.M)[1])
def refs(block,key):
    m=re.search(r'^  '+key+r':\n((?:  - .*\n)+)',block,re.M)
    return [int(x) for x in re.findall(r'fileID: (\d+)',m[1])] if m else []
def clean(text): return '\n'.join(line.rstrip() for line in text.splitlines())+'\n'

class Builder:
    def __init__(self):
        self.original=blocks((ROOT/SOURCE).read_text())
        self.raw=dict(self.original)
        self.d=kit.Doc(2000000)
        self.root=self.d.go('WarRoomComplete - Operations Offices and Headquarters')
        self.fake={}
        self.report={'prefab':PREFAB,'scene':SCENE,'source':SOURCE,'new_obstacles':[], 'occluder_boxes':[], 'transition_route':[]}
        self.c=kit.Course.__new__(kit.Course)
        self.c.d=self.d;self.c.root=self.root;self.c.accent='Teal';self.c.units=[];self.c.cuts=[];self.c.bi=0
        self.prefix=self.foreign(100002)
        self.root['kids'].append(self.prefix)
        self.prefix['parent']=self.root
        self.raw[100002]=self.raw[100002].replace('  m_Father: {fileID: 0}',f'  m_Father: {kit.ref(self.root["tr"])}')
        self.raw[100002]=re.sub(r'  m_LocalPosition: .*','  m_LocalPosition: {x: 120, y: 1, z: -134}',self.raw[100002])
        self.raw[100002]=re.sub(r'  m_LocalRotation: .*','  m_LocalRotation: {x: 0, y: 1, z: 0, w: 0}',self.raw[100002])
        self.raw[100001]=self.raw[100001].replace('m_Name: RoyalWarRoom','m_Name: Operations Offices - approach course')
        self.source_module=next((k,b) for k,b in self.raw.items() if '  courseId: "3-1"' in b)
        for k,b in list(self.raw.items()):
            if 'm_Name: MinigameSlot - replace contents only' in b:self.raw[k]=b.replace('m_IsActive: 1','m_IsActive: 0')
    def foreign(self,tid):
        if tid not in self.fake:self.fake[tid]={'tr':tid,'kids':[],'parent':None}
        return self.fake[tid]
    def group(self,name,parent=None,pos=(0,0,0),rot=(0,0,0,1)):
        return self.d.go(name,pos,rot,parent=parent or self.root)
    def box(self,p,name,pos,size,color='Slate',solid=True,rot=(0,0,0,1),shape='Cube'):
        return self.c.cube(p,name,pos,size,color,solid,rot,shape)
    def wall(self,p,name,pos,size,occluder=False):
        self.box(p,name,pos,size,'Slate')
        if occluder:self.report['occluder_boxes'].append({'name':name,'center':pos,'size':size})
    def map(self,p,pos,side=False,flipped=False):
        g=self.group('Framed campaign map',p,pos,kit.yaw((90 if side else 0)+(180 if flipped else 0)))
        self.box(g,'Carved walnut frame',(0,0,0),(12,7,.5),'Wood',False)
        self.box(g,'Parchment',(0,0,-.3),(11.3,6.3,.1),'Ivory',False)
        for i in range(4):
            x=-4+i*2.6;y=-1.5+(i%2)*2
            self.box(g,'Fortress marker',(x,y,-.4),(1.1,.8,.1),'Teal',False)
            self.box(g,'Advance route',(x+.7,y+.4,-.45),(1.6,.13,.1),'Gold',False,rot=(0,0,.15,.988686))
    def banner(self,p,pos,side=False,flipped=False):
        g=self.group('Royal standard',p,pos,kit.yaw((90 if side else 0)+(180 if flipped else 0)))
        self.box(g,'Hanging cloth',(0,0,0),(3,7,.2),'Purple',False)
        self.box(g,'Crown emblem',(0,.8,-.15),(1.8,1.4,.1),'Gold',False)
        for x in (-.6,0,.6):self.box(g,'Crown point',(x,1.8,-.15),(.3,.8,.1),'Gold',False)
        self.box(g,'Banner rail',(0,3.6,0),(3.8,.2,.3),'Gold',False)
    def shelf(self,p,pos,rot=(0,0,0,1)):
        g=self.group('Orders archive and trophies',p,pos,rot)
        self.box(g,'Cabinet',(0,2.2,0),(9,4.4,1.5),'Wood')
        for i in range(6):
            self.box(g,'Bound strategy manual',(-3.5+i*1.3,3,-.85),(.8,2,.25),'Teal' if i%2 else 'Purple',False)
        self.box(g,'Top parchment',(0,4.45,0),(6,.08,1.3),'Ivory',False)
        self.box(g,'Ink bottle',(3,4.9,0),(.7,.45,.7),'Night',False,shape='Cylinder')
        self.box(g,'Royal seal',(-3,4.65,0),(.8,.2,.8),'Gold',False,shape='Cylinder')
        self.box(g,'Captured pawn base',(0,4.7,0),(1.4,.2,1.4),'White',False,shape='Cylinder')
        self.box(g,'Captured pawn neck',(0,5.4,0),(.5,.55,.5),'White',False,shape='Cylinder')
        self.box(g,'Captured pawn head',(0,6.1,0),(.8,.8,.8),'White',False,shape='Sphere')
    def offices(self):
        g=self.group('Enclosed Operations Offices - walls and briefing displays',self.prefix)
        self.wall(g,'West exterior wall',(-20,8,34),(1.6,24,100))
        self.wall(g,'East exterior wall',(140,8,34),(1.6,24,100))
        self.wall(g,'Rear exterior wall',(60,8,84),(160,24,1.6))
        # Only the final 12m passage opens toward the headquarters approach.
        self.wall(g,'Entry wall and briefing facade',(46.5,8,-16),(133,24,1.6))
        self.wall(g,'Exit facade return',(133.5,8,-16),(13,24,1.6))
        self.wall(g,'Exit lintel',(120,14,-16),(14,12,1.6))
        self.wall(g,'Operations ceiling',(60,20.4,34),(161,.8,101))
        # Three paired-lane departments; gaps at each end leave the authored turns clear.
        self.wall(g,'Intelligence partition',(34,8,43),(1.2,24,46))
        self.wall(g,'Logistics partition',(82,8,40),(1.2,24,52))
        # Enclose every lane. Pocket recesses preserve the existing pusher/wall sweeps.
        for start,end in ((6,26),(34,66)):
            self.wall(g,'Pointer department dividing wall',(12,8,(start+end)/2),(1.2,24,end-start))
        self.wall(g,'Pusher pocket back',(5.5,8,30),(1,24,8))
        for z in (26,34):self.wall(g,'Pusher pocket return',(8.75,8,z),(6.5,24,1))
        self.wall(g,'Dispatch department dividing wall',(60,8,36),(1.2,24,60))
        for start,end in ((6,51),(59,66)):
            self.wall(g,'Final department dividing wall',(108,8,(start+end)/2),(1.2,24,end-start))
        self.wall(g,'Sliding wall pocket back',(110.5,8,55),(1,24,8))
        for z in (51,59):self.wall(g,'Sliding wall pocket return',(109.25,8,z),(2.5,24,1))
        for x in (12,60,108):
            for z in (12,44,62):
                # The sliding wall's pocket is kept clear of display cabinets.
                if x==108 and z==62:continue
                for side in (-1,1):
                    display=self.group('Department map and records',g,(x+side,0,z),kit.yaw(90 if side<0 else -90))
                    self.map(display,(0,8,0))
                    self.shelf(display,(0,0,0))
        for x in (-16,18,54,102,136):
            self.map(g,(x,8,82.9))
            self.shelf(g,(x,0,81.8))
        for x in (-10,24,58,92):self.banner(g,(x,10,-15),False,True)
        for x in (34.9,82.9):
            for z in (30,56):self.map(g,(x,8,z),True,True)
        for z in (8,36,64):
            self.banner(g,(-18.9,10,z),True,True)
            self.banner(g,(138.9,10,z),True)
        # Close the remaining transition hall as an archive vestibule, not an outdoor apron.
        for x in (97,143):self.wall(g,'Archive gallery side',(x,6,-49),(1.2,20,54))
        self.wall(g,'Archive ceiling',(120,16.4,-49),(47,.8,54))
        for x in (105.5,134.5):
            self.wall(g,'Archive end return',(x,6,-76),(17,20,1.2))
            self.wall(g,'Archive entrance return',(x,6,-24),(17,20,1.2))
        self.wall(g,'Archive end lintel',(120,12,-76),(12,8,1.2))
        self.wall(g,'Archive entrance lintel',(120,12,-24),(12,8,1.2))
        for x in (113,127):self.wall(g,'Terrace passage side',(x,6,-20),(1.2,20,8))
        self.wall(g,'Terrace passage roof',(120,16.4,-20),(15,.8,8))
        for x in (105,135):self.map(g,(x,7,-74.9),False,True)
        self.map(g,(98,7,-42),True,True);self.map(g,(142,7,-42),True)
        # A decorative floor lies below the existing kill height and never creates a bypass.
        self.box(g,'Recessed chamber floor - no collision',(60,-11,34),(160,1,100),'Wood',False)
    def beat(self,i):
        go=next(k for k,b in self.raw.items() if f'm_Name: Beat {i+1:02} -' in b)
        tid=next(k for k,b in self.raw.items() if b.startswith('--- !u!4 ') and fid(b,'m_GameObject')==go)
        self.c.bi=i;self.c.beat=self.foreign(tid)
        return self.c.beat
    def press(self,z,x=0,width=12,offset=0):
        g=self.c.group('Command stamp press - warning then strike',z,x,kind='stamp_press')
        context=self.c.context(g)
        area=self.d.box(g,size=(width,3,6),center=(0,1.5,0),trigger=True)
        warning=self.box(g,'Rectangular strike warning',(0,.04,0),(width,.05,6),'Warning',False)
        self.box(g,'Safe waiting inlay',(0,.035,-5),(width,.04,1),'Safe',False)
        head=self.group('Stamp head - edit timing and impulse',g)
        self.d.rigidbody(head,True)
        self.box(head,'Press brass head',(0,.8,0),(width-.2,1.6,5.8),'Gold',False)
        self.box(head,'Crown die',(0,2,0),(width*.55,1,3),'Purple',False)
        self.d.script(head,'CommandStampPress',kit.fields(context=kit.ref(context),impactArea=kit.ref(area),warningVisual=kit.ref(warning['go']),
            raisedHeight=6,restSeconds=2,warningSeconds=1.4,slamSeconds=.35,holdSeconds=.6,retractSeconds=1,phaseOffset=offset,pushForce=10,upwardForce=2))
    def pointers(self,z):
        g=self.c.group('Opposed command pointers - read the closing gap',z,kind='pointer_gate');context=self.c.context(g)
        for side in (-1,1):
            body=self.group('Pointer pivot - angle period and force',g,(6.3*side,1.15,0),kit.yaw(180 if side>0 else 0))
            self.d.rigidbody(body,True)
            motion=self.d.script(body,'CommandPointerSweep',kit.fields(swingAngle=55,period=6,phaseOffset=0,pushForce=8,upwardForce=1))
            self.box(body,'Sweeping pointer',(3.15,0,0),(6.3,.4,.4),'Gold')
            relay=self.group('Pointer contact',body,(3.15,0,0));self.c.impact(relay,motion,context,(6.3,.5,.5))
            self.box(g,'Pointer stand',(6.3*side,.55,0),(.5,1.1,.5),'Wood',False)
    def obstacles(self):
        self.beat(0);self.press(35,-3,6,0);self.press(35,3,6,2.675)
        self.beat(1);self.pointers(9)
        self.beat(2);self.c.vent(37)
        self.beat(3);before=len(self.d.all);self.c.gate(31);self.d.all[before]['pos']=(0,3,31)
        self.beat(4);self.pointers(41)
        self.beat(5);self.press(44)
        self.report['new_obstacles']=self.c.units
    def headquarters(self):
        g=self.group('Headquarters - enclosed command chamber');self.hq=g
        # Full height facade, side walls, rear wall and ceiling hide both board and giants.
        for x in (-65,65):self.wall(g,'HQ front facade',(x,24,-10),(118,68,2),True)
        self.wall(g,'HQ doorway header',(0,34,-10),(12,48,2),True)
        for x in (-124,124):self.wall(g,'HQ side wall',(x,24,80),(2,68,182),True)
        self.wall(g,'HQ rear wall',(0,24,170),(250,68,2),True)
        self.wall(g,'HQ coffered ceiling',(0,58.5,80),(250,1,182),True)
        for x in (-96,-48,0,48,96):self.box(g,'Carved ceiling beam',(x,57.5,80),(2,1,180),'Wood',False)
        for z in (0,40,80,120,160):self.box(g,'Ceiling cross beam',(0,57.3,z),(248,1,2),'Wood',False)
        # Saved static table/chairs are visible in Prefab Mode, unlike the old runtime-only dressing.
        self.box(g,'Giant oak table',(0,-2.8,64),(130,4,168),'Wood',False)
        self.box(g,'Room floor below fall reset',(0,-23,80),(248,2,178),'Slate',False)
        for side in (-1,1):
            x=86*side
            self.box(g,'Commander chair seat',(x,1,64),(22,4,26),'Wood',False)
            self.box(g,'Commander chair back',(x+side*11,18,64),(4,34,28),'Purple',False)
            self.box(g,'Chair crown',(x+side*8.8,28,64),(.5,7,7),'Gold',False)
            for z in (51,77):self.box(g,'Chair arm rest',(x,8,z),(22,2,3),'Wood',False)
            for z in (-10,138):
                self.box(g,'Table leg',(side*56,-12,z),(7,20,7),'Wood',False)
                self.box(g,'Brass leg collar',(side*56,-19,z),(8,2,8),'Gold',False)
            for z in (10,45,85,130):
                self.banner(g,(side*122,24,z),True,side<0)
                self.shelf(g,(side*120,0,z),kit.yaw(-90 if side<0 else 90))
            for z in (-3,140):
                self.shelf(g,(side*80,0,z))
                self.box(g,'Open command report',(side*78,6.5,z),(8,.1,5),'Ivory',False)
                self.box(g,'Command pointer at rest',(side*79,6.7,z),(12,.3,.3),'Gold',False)
        frame=self.group('Grand campaign map',g,(0,26,168),rot=(0,0,0,1))
        frame['scale']=(5,4,1)
        self.map(frame,(0,0,0))
        for x in (-44,44):self.banner(g,(x,30,168))
        # Foreground banner/crest and paneled door jambs read as a command entrance.
        for x in (-7,7):self.box(g,'HQ door carved jamb',(x,5,-11.3),(1.4,10,1),'Wood')
        for x in (-9,9):self.banner(g,(x,9,-11.5))
        self.box(g,'HQ door lintel',(0,10.5,-11.3),(16,1,1),'Gold')
        self.box(g,'HQ royal crest',(0,14,-11.3),(4,3,.5),'Gold',False)
        gate=self.group('Headquarters privacy doors - proximity reveal',g,(0,0,-10))
        volume=self.d.box(gate,size=(14,12,12),center=(0,6,0),trigger=True)
        bodies=[]
        for side in (-1,1):
            leaf=self.group('Solid command door leaf',gate,(side*3,0,0))
            bodies.append(self.d.rigidbody(leaf,True))
            self.box(leaf,'Oak command door',(0,5,0),(6,10,.6),'Wood')
            self.box(leaf,'Brass inset',(0,5,-.35),(4,7,.1),'Gold',False)
            self.box(leaf,'Purple panel',(0,5,-.42),(3.6,6.6,.08),'Purple',False)
            self.report['occluder_boxes'].append({'name':'Closed HQ door','center':(side*3,5,-10),'size':(6,10,.6)})
        self.d.script(gate,'HeadquartersPrivacyDoor',kit.fields(leftLeaf=kit.ref(bodies[0]),rightLeaf=kit.ref(bodies[1]),presenceVolume=kit.ref(volume),
            openDistance=7,moveSpeed=4,holdOpenSeconds=1.5))
        preview=self.group('Board preview - replaced by live chess on Play',g)
        for x in range(8):
            for z in range(8):self.box(preview,'Chess square preview',((x-3.5)*16,-.4,(z+.5)*16),(16,.8,16),'White' if (x+z)%2==0 else 'Night',False)
        self.preview=preview
        self.exit=self.group('Complete course exit - forward',self.root,(0,0,143))
    def vestibule(self):
        g=self.group('Screened briefing vestibule - two turns before HQ')
        self.box(g,'Vestibule walking floor',(18,-.4,-36),(60,.8,56),'Ivory')
        for x in (-12,48):self.wall(g,'Vestibule side screen',(x,6,-36),(1.2,20,56),True)
        self.wall(g,'Vestibule opaque roof',(18,16.4,-36),(61,.8,56),True)
        # First screen opens at X32; the HQ facade opens at X0. Their overlap blocks the outside view.
        self.wall(g,'Archive sight screen', (7,6,-38),(38,20,1.6),True)
        self.wall(g,'Archive screen return',(43,6,-38),(10,20,1.6),True)
        self.wall(g,'Archive screen lintel',(32,13,-38),(12,6,1.6),True)
        # Rear doorway meets the saved approach floor, without opening the sides to a bypass.
        for x,w in ((-9,6),(27,42)):self.wall(g,'Vestibule entrance return',(x,6,-64),(w,20,1.2),True)
        self.wall(g,'Vestibule entrance header',(0,13,-64),(12,6,1.2),True)
        self.map(g,(6,8,-39));self.shelf(g,(6,0,-39.8))
        self.map(g,(47,8,-24),True);self.banner(g,(-10.8,9,-24),True,True)
        self.transitions=[]
        for i,pos in enumerate(((0,0,-60),(0,0,-44),(32,0,-44),(32,0,-24),(0,0,-24),(0,0,-4))):
            self.transitions.append(self.group(f'HQ approach waypoint {i+1}',g,pos))
            self.report['transition_route'].append(pos)
        self.chessEntry=self.transitions[-1]
    def save(self):
        self.offices();self.obstacles();self.headquarters();self.vestibule()
        self.c.context(self.root)
        self.d.script(self.root,'PhysicsProfile',kit.fields(physicsRate=120,solverIterations=24))
        self.d.script(self.root,'GiantChessCourse',kit.fields(
            pawnPrefab='{fileID: 7684884123511159585, guid: fe69acb7e49fea745aa289c4d39f3e62, type: 3}',
            tuning='{fileID: 11400000, guid: f387368cafccca042ac935642cf094be, type: 2}',tileSize=16,replaySpeed=5,
            moveSeconds=3,warningSeconds=5,restSeconds=2,knockbackSpeed=12,upwardSpeed=9,botsEnabled=1,boardPreview=kit.ref(self.preview['go']),chessSpawnDepth=4))
        self.d.script(self.root,'GiantChessApproach',kit.fields(course=kit.ref(self.source_module[0]),chessEntry=kit.ref(self.chessEntry['tr']))+
            '  transitionRoute:\n'+''.join('  - '+kit.ref(p['tr'])+'\n' for p in self.transitions))
        source=self.source_module[1]
        route=refs(source,'route')+[p['tr'] for p in self.transitions]+[self.exit['tr']]
        checkpoints=refs(source,'checkpoints')
        self.d.script(self.root,'CourseModule',kit.fields(courseId='"3-1-HQ"',displayName='"작전실 · 총지휘부"',entry=kit.ref(fid(source,'entry')),
            exit=kit.ref(self.exit['tr']),minigameSlot=kit.ref(self.hq['tr']),targetSeconds=360,laneWidth=12)+
            '  checkpoints:\n'+''.join('  - '+kit.ref(p)+'\n' for p in checkpoints)+
            '  route:\n'+''.join('  - '+kit.ref(p)+'\n' for p in route)+
            '  placementBounds:\n    m_Center: {x: 5, y: 20, z: -23}\n    m_Extent: {x: 133, y: 45, z: 197}\n')
        # Add new children to the flattened source hierarchy without touching the source prefab.
        for tid,parent in self.fake.items():
            if not parent['kids']:continue
            add=''.join('  - '+kit.ref(k['tr'])+'\n' for k in parent['kids'])
            b=self.raw[tid]
            b=b.replace('  m_Children: []\n','  m_Children:\n')
            b=b.replace('  m_Father:',add+'  m_Father:')
            self.raw[tid]=b
        text=self.d.render(scene=False)+''.join(self.raw.values())
        self.report['root']=self.root['go'];self.report['root_transform']=self.root['tr']
        return clean(text)

def scene(prefab_root):
    text=(ROOT/SCENE).read_text();data=blocks(text)
    # Retain the existing camera/light/settings verbatim; replace only gameplay roots.
    kept=[]
    for key,b in data.items():
        if key>=9000000000000000000 or key in (1667498617,1667498618,1667498619,1667498620,1667498621,1667498622,950000,950001,950002,960000,960001):continue
        kept.append(b)
    guid=kit.guid(PREFAB)
    instance=f'''--- !u!1001 &960000
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications: []
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}
--- !u!4 &960001 stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {prefab_root}, guid: {guid}, type: 3}}
  m_PrefabInstance: {{fileID: 960000}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {{fileID: 960001}}
  - {{fileID: 1312111601}}
  - {{fileID: 439410733}}
'''
    return clean(kit.HEAD+''.join(kept)+instance)

def main():
    parser=argparse.ArgumentParser(description=__doc__);m=parser.add_mutually_exclusive_group(required=True)
    for flag in ('write','overwrite','check'):m.add_argument('--'+flag,action='store_true')
    args=parser.parse_args();b=Builder();prefab=b.save()
    outputs={PREFAB:prefab,SCENE:scene(b.root['tr']),REPORT:json.dumps(b.report,ensure_ascii=False,indent=2)+'\n'}
    if args.check:
        stale=[p for p,s in outputs.items() if not (ROOT/p).exists() or (ROOT/p).read_text()!=s]
        if stale:raise SystemExit('Differs from authoring source: '+', '.join(stale))
        print('Complete war-room prefab, scene and report match.');return
    if args.write and (ROOT/PREFAB).exists():raise SystemExit('Prefab exists. Use --check or explicitly --overwrite.')
    for p,s in outputs.items():(ROOT/p).write_text(s)
    subprocess.run([sys.executable,'Tools/Generators/mkmeta.py',PREFAB],cwd=ROOT,check=True)
    print('Saved one complete war-room prefab and its scene instance.')
if __name__=='__main__':main()
