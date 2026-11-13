#!/usr/bin/env python3
"""R120 complete war-room prefab. --write creates it; --overwrite discards edits;
--check is read-only. The scene keeps a single instance plus its camera and light.
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path
import build_courses as kit
from stable_prefab_ids import stabilize

ROOT=kit.ROOT
PREFAB='Assets/Resources/RoyalPalace/WarRoomComplete.prefab'
SCENE='Assets/Scenes/GiantChessCourse.unity'
SOURCE='Assets/Resources/RoyalPalace/Courses/3-1_RoyalWarRoom.prefab'
REPORT='Tools/RoyalPalace/war_room_report.json'

def blocks(text):
    return {int(m[1]):b for b in re.split(r'(?=^--- !u!)',text,flags=re.M) if (m:=re.match(r'--- !u!\d+ &(\d+)',b))}
def fid(block,key): return int(re.search(r'^  '+key+r': \{fileID: (\d+)',block,re.M)[1])
def refs(block,key):
    m=re.search(r'^  '+key+r':\n((?:  - .*\n)+)',block,re.M)
    return [int(x) for x in re.findall(r'fileID: (\d+)',m[1])] if m else []
def clean(text): return '\n'.join(line.rstrip() for line in text.splitlines())+'\n'

class LegacyBuilder:
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
                    if not (x==60 and side<0 and z==62):self.shelf(display,(0,0,0))
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
            raisedHeight=6,restSeconds=2,warningSeconds=1.4,slamSeconds=.18,holdSeconds=.6,retractSeconds=2.5,phaseOffset=offset,pushForce=10,upwardForce=2))
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
        self.barrage();self.formations()
        self.report['new_obstacles']=self.c.units
    def barrage(self):
        self.beat(2)
        g=self.c.group('Tactical map barrage - follow the green safe squares',13,kind='tactical_barrage')
        ctx=self.c.context(g);areas=[];warn=[];safe=[];strike=[]
        self.box(g,'Parchment map surface',(0,.015,0),(12,.025,13.5),'Ivory',False)
        for row in range(3):
            for col in range(3):
                cell=self.group(f'Tactical sector {row+1}-{col+1}',g,((col-1)*4,0,(row-1)*4.5))
                areas.append(self.d.box(cell,size=(4,6,4.5),center=(0,3,0),trigger=True))
                self.box(cell,'Map grid outline',(0,.04,0),(3.98,.025,4.48),'Gold',False)
                self.box(cell,'Map grid parchment',(0,.06,0),(3.8,.025,4.3),'Ivory',False)
                warn.append(self.box(cell,'Barrage warning',(0,.08,0),(3.7,.025,4.2),'Warning',False))
                safe.append(self.box(cell,'Safe sector',(0,.08,0),(3.7,.025,4.2),'Safe',False))
                blast=self.group('Impact columns - visual only',cell)
                for x in (-1.4,1.4):self.box(blast,'Map artillery flash',(x,1.5,0),(.18,3,3.4),'Gold',False)
                strike.append(blast)
                for v in (warn[-1],safe[-1],strike[-1]):v['active']=0
        for z in (-8,8):self.box(g,'Map observation strip',(0,.04,z),(12,.04,1),'Safe',False)
        body=kit.fields(context=kit.ref(ctx),columns=3,warningSeconds=2.2,strikeSeconds=.35,restSeconds=.8,phaseOffset=0,pushForce=9,upwardForce=2,localPushDirection=(0,0,-1))
        for name,values in (('dangerAreas',areas),('warningVisuals',[v['go'] for v in warn]),('safeVisuals',[v['go'] for v in safe]),('strikeVisuals',[v['go'] for v in strike])):
            body+='  '+name+':\n'+''.join('  - '+kit.ref(v)+'\n' for v in values)
        self.d.script(g,'TacticalMapBarrage',body)
    def formations(self):
        self.beat(2)
        # Clear the existing decorative pillars from the moving formation's swept volume.
        beat=self.c.beat['tr']
        for b in self.raw.values():
            if not b.startswith('--- !u!4 ') or fid(b,'m_Father')!=beat:continue
            go=fid(b,'m_GameObject');obj=self.raw[go]
            if any('m_Name: '+name+'\n' in obj for name in ('Gallery pillar','Capital','Map display')):
                pos=re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)',b)
                if pos and float(pos[3]) in (64,66):self.raw[go]=obj.replace('m_IsActive: 1','m_IsActive: 0')
        for z,phase in ((59,0),(64.5,.5)):
            g=self.c.group('Troop redeployment - moving passage',z,kind='troop_formation');ctx=self.c.context(g)
            self.box(g,'Deployment rail',(0,.02,0),(12,.04,1.8),'Gold',False)
            body=self.group('Pawn formation - movement and impact settings',g)
            self.d.rigidbody(body,True)
            motion=self.d.script(body,'TroopFormationSlide',kit.fields(travelDistance=2.5,period=6,phaseOffset=phase,pushForce=7,upwardForce=1))
            for side in (-1,1):
                # 3.5m gap, centre +/-2.5m. Outer ends always cover the entire 12m lane.
                row=self.group('Linked pawn regiment',body,(side*5.125,0,0))
                self.box(row,'Formation plinth',(0,.3,0),(6.75,.6,1.6),'Wood')
                self.d.box(row,size=(6.75,5,1.6),center=(0,2.5,0))
                contact=self.group('Regiment contact',row,(0,2.5,0));self.c.impact(contact,motion,ctx,(6.75,5,1.6))
                for x in (-2.25,0,2.25):
                    self.box(row,'Pawn base',(x,.7,0),(1.8,.3,1.8),'White',False,shape='Cylinder')
                    self.box(row,'Pawn stem',(x,2,0),(.9,1.1,.9),'White',False,shape='Cylinder')
                    self.box(row,'Pawn collar',(x,3.1,0),(1.5,.2,1.5),'Gold',False,shape='Cylinder')
                    self.box(row,'Pawn head',(x,4,0),(1.6,1.6,1.6),'White',False,shape='Sphere')
            # Pawn mouldings hint at the passage without a solid crossbar across the gap.
            for x in (-1.75,1.75):self.box(body,'Passage boundary',(x,.05,0),(.12,.06,1.7),'Safe',False)
    def approval_archive(self):
        go=next(k for k,b in self.raw.items() if 'm_Name: Transition hall - fixed connection geometry' in b)
        tid=next(k for k,b in self.raw.items() if b.startswith('--- !u!4 ') and fid(b,'m_GameObject')==go)
        hall=self.foreign(tid);g=self.group('Orders approval archive - cooperative shortcut and solo route',hall)
        # Keep a 6m solo passage at the far left; the central door is a bonus, never a hard lock.
        self.box(g,'Approval archive left partition',(-10,5,14),(12,10,1.2),'Wood')
        self.box(g,'Approval archive right partition',(13,5,14),(18,10,1.2),'Wood')
        self.box(g,'Approval archive header',(0,8.5,14),(8,3,1.2),'Wood')
        self.map(g,(-10,6,13.3));self.map(g,(13,6,13.3))
        self.box(g,'Records aisle divider',(-9,4,9),(1,8,10),'Wood')
        self.shelf(g,(-10,0,9),kit.yaw(90))
        gate=self.group('Dual seal approval gate - two operators',g,(0,0,14));ctx=self.c.context(gate)
        plates=[];indicators=[];leaves=[]
        for side in (-1,1):
            plate=self.group('Royal seal approval plate',gate,(side*3,0,-5))
            self.box(plate,'Inset seal',(0,.035,0),(2.8,.07,2.8),'Gold',False,shape='Cylinder')
            plates.append(self.d.box(plate,size=(2.8,1.2,2.8),center=(0,.6,0),trigger=True))
            indicator=self.box(plate,'Occupied seal indicator',(0,.09,0),(2,.08,2),'Safe',False,shape='Cylinder');indicator['active']=0
            indicators.append(indicator)
            leaf=self.group('Approval door leaf',gate,(side*2,0,0));leaves.append(self.d.rigidbody(leaf,True))
            self.box(leaf,'Sealed orders door',(0,3.5,0),(4,7,.6),'Purple')
            self.box(leaf,'Seal emblem',(0,3.5,-.35),(1.4,1.4,.1),'Gold',False,shape='Cylinder')
            self.box(g,'Seal to door inlay',(side*3,.035,11.5),(.15,.04,2),'Gold',False)
        volume=self.d.box(gate,size=(10,9,5),center=(0,4.5,0),trigger=True)
        self.d.script(gate,'DualApprovalGate',kit.fields(context=kit.ref(ctx),firstPlate=kit.ref(plates[0]),secondPlate=kit.ref(plates[1]),
            firstIndicator=kit.ref(indicators[0]['go']),secondIndicator=kit.ref(indicators[1]['go']),doorwayVolume=kit.ref(volume),
            leftLeaf=kit.ref(leaves[0]),rightLeaf=kit.ref(leaves[1]),approvalSeconds=1.25,openSeconds=8,openDistance=4.5,moveSpeed=3))
        # Extend the central shortcut across the old gap; side exits remain the solo route.
        self.box(g,'Approved dispatch bridge',(0,-.4,28),(8,.8,6),'Ivory')
        for k,b in list(self.raw.items()):
            if 'm_Name: Decorative central closed door' in b:self.raw[k]=b.replace('m_IsActive: 1','m_IsActive: 0')
        route=[self.group('Solo archive route '+str(i+1),hall,p) for i,p in enumerate(((-18,0,2),(-18,0,18),(-14,0,28),(-14,0,34)))]
        self.report['solo_archive_route']=[v['tr'] for v in route]
        self.report['approval_gate']=gate['tr']
        self.report['new_obstacles'].append({'beat':'archive','kind':'dual_approval','name':'Two royal seals open the dispatch shortcut'})
        # Replace the old diagonal route through the new wall with the permanently open aisle.
        mid,source=self.source_module
        oldroute=refs(source,'route');oldside=next(t for t in oldroute if 'm_Name: Route side exit' in self.raw[fid(self.raw[t],'m_GameObject')])
        ids=[]
        for t in oldroute:ids.extend([v['tr'] for v in route] if t==oldside else [t])
        source=re.sub(r'  route:\n(?:  - .*\n)+','  route:\n'+''.join('  - '+kit.ref(t)+'\n' for t in ids),source)
        self.raw[mid]=source;self.source_module=(mid,source)
        # A visible gold trail marks the solo path; two seal icons mark the central option.
        points=[(0,0,0)]+[v['pos'] for v in route]
        import math
        for a,b in zip(points,points[1:]):
            length=math.dist(a,b);angle=math.degrees(math.atan2(b[0]-a[0],b[2]-a[2]))
            self.box(g,'Solo archive guide inlay',((a[0]+b[0])/2,.025,(a[2]+b[2])/2),(.2,.03,length),'Gold',False,kit.yaw(angle))
    def square_route(self):
        # Keep four gameplay lanes. Two former lanes become an inaccessible staff briefing bay.
        disabled=[]
        for i in (3,4):
            beat=self.beat(i);go=fid(self.raw[beat['tr']],'m_GameObject')
            self.raw[go]=self.raw[go].replace('m_IsActive: 1','m_IsActive: 0');disabled.append(beat['tr'])
        g=self.group('Square operations room - abbreviated north cross passage',self.prefix)
        self.box(g,'North connecting floor',(84,-.4,72),(24,.8,12),'Slate')
        self.box(g,'North passage guide',(84,.025,72),(72,.04,.2),'Gold',False)
        # Explicit outer shell: X[-20,140], Z[-76,84]. The old front wall is an interior partition.
        self.wall(g,'Square operations south facade',(47,8,-76),(134,24,1.6))
        self.wall(g,'Square operations south return',(133,8,-76),(14,24,1.6))
        self.wall(g,'Square operations exit header',(120,14,-76),(12,12,1.6))
        for x in (-20,140):self.wall(g,'Square operations side extension',(x,8,-46),(1.6,24,60))
        self.wall(g,'Square operations south roof',(60,20.4,-46),(161,.8,60))
        # Carved back wall and briefing furniture make the retired bays read as offices.
        self.wall(g,'Staff briefing bay north wall',(84,8,65),(48,24,1.2))
        self.map(g,(84,10,65.7),False,True)
        self.box(g,'Staff strategy table',(84,1,30),(30,2,22),'Wood',False)
        self.box(g,'Staff table map',(84,2.1,30),(26,.1,18),'Ivory',False)
        for x in (67,101):self.box(g,'Staff chair',(x,1,30),(3,2,6),'Purple',False)
        for x in (10,50,90):self.map(g,(x,9,-74.9),False,True)
        module,source=self.source_module
        cps=refs(source,'checkpoints');cp=cps[1]
        self.raw[cp]=re.sub(r'  m_LocalPosition: .*','  m_LocalPosition: {x: 96, y: 0, z: 72}',self.raw[cp])
        self.raw[cp]=re.sub(r'  m_LocalRotation: .*','  m_LocalRotation: {x: 0, y: 0.7071067812, z: 0, w: 0.7071067812}',self.raw[cp])
        self.box(g,'Relocated checkpoint 2',(96,.025,72),(6,.04,11),'Safe',False)
        # Hide the old checkpoint inlay at (96,0,1).
        for key,b in list(self.raw.items()):
            if b.startswith('--- !u!4 ') and 'm_LocalPosition: {x: 96, y: 0.025, z: 1}' in b:
                go=fid(b,'m_GameObject');self.raw[go]=self.raw[go].replace('m_IsActive: 1','m_IsActive: 0')
        route=[]
        for tid in refs(source,'route'):
            parent=fid(self.raw[tid],'m_Father') if tid in self.raw else 0
            if parent in disabled:continue
            route.append(tid)
            if tid in self.raw and 'm_Name: Route 3 end' in self.raw[fid(self.raw[tid],'m_GameObject')]:route.append(cp)
        source=re.sub(r'  route:\n(?:  - .*\n)+','  route:\n'+''.join('  - '+kit.ref(t)+'\n' for t in route),source)
        source=source.replace('targetSeconds: 240','targetSeconds: 150')
        self.raw[module]=source;self.source_module=(module,source)
        self.report['operations_footprint_m']=[160,160]
        self.report['active_beats']=[1,2,3,6]
        self.report['operations_target_seconds']=[120,180]
        self.report['timing_scope']='Entry to headquarters doorway; giant chess crossing excluded'
    def headquarters(self):
        g=self.group('Headquarters - enclosed command chamber');self.hq=g
        # Full height facade, side walls, rear wall and ceiling hide both board and giants.
        for x in (-66,66):self.wall(g,'HQ front facade',(x,24,-10),(116,68,2),True)
        self.wall(g,'HQ doorway header',(0,32.4,-10),(16,51.2,2),True)
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
        for x in (-8.7,8.7):self.box(g,'HQ door carved jamb',(x,3.4,-11.3),(1.4,6.8,1),'Wood')
        for x in (-9,9):self.banner(g,(x,9,-11.5))
        self.box(g,'HQ door lintel',(0,7.3,-11.3),(19,1,1),'Gold')
        self.box(g,'HQ royal crest',(0,14,-11.3),(4,3,.5),'Gold',False)
        gate=self.group('Headquarters privacy doors - proximity reveal',g,(0,0,-10))
        volume=self.d.box(gate,size=(18,12,12),center=(0,6,0),trigger=True)
        bodies=[]
        for side in (-1,1):
            leaf=self.group('Solid command door leaf',gate,(side*4,0,0))
            bodies.append(self.d.rigidbody(leaf,True))
            self.box(leaf,'Oak command door',(0,3.4,0),(8,6.8,.6),'Wood')
            self.box(leaf,'Brass inset',(0,3.4,-.35),(6,5,.1),'Gold',False)
            self.box(leaf,'Purple panel',(0,3.4,-.42),(5.6,4.6,.08),'Purple',False)
            self.report['occluder_boxes'].append({'name':'Closed HQ door','center':(side*4,3.4,-10),'size':(8,6.8,.6)})
        self.d.script(gate,'HeadquartersPrivacyDoor',kit.fields(leftLeaf=kit.ref(bodies[0]),rightLeaf=kit.ref(bodies[1]),presenceVolume=kit.ref(volume),
            openDistance=9,moveSpeed=4,holdOpenSeconds=1.5))
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
        self.offices();self.obstacles();self.approval_archive();self.square_route();self.headquarters();self.vestibule()
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
            exit=kit.ref(self.exit['tr']),minigameSlot=kit.ref(self.hq['tr']),targetSeconds=240,laneWidth=12)+
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
        generated=blocks(self.d.render(scene=False))
        for obj in self.d.all:
            if obj.get('active')==0:
                generated[obj['go']]=generated[obj['go']].replace('m_IsActive: 1','m_IsActive: 0')
        text=kit.HEAD+''.join(generated.values())+''.join(self.raw.values())
        text,ids=stabilize(clean(text))
        self.serialized_root=ids[self.root['tr']]
        self.report['root']=ids[self.root['go']];self.report['root_transform']=self.serialized_root
        self.report['solo_archive_route']=[ids[t] for t in self.report['solo_archive_route']]
        self.report['approval_gate']=ids[self.report['approval_gate']]
        self.report['local_id_scheme']='hierarchy-type-sha256-v2'
        return text

class Builder(LegacyBuilder):
    def __init__(self):
        self.d=kit.Doc(2000000);self.root=self.d.go('WarRoomComplete - Operations Offices and Headquarters')
        self.raw={};self.fake={};self.report={'prefab':PREFAB,'scene':SCENE,'new_obstacles':[],'occluder_boxes':[], 'transition_route':[], 'footprint_m':[320,320]}
        self.c=kit.Course.__new__(kit.Course);self.c.d=self.d;self.c.root=self.root;self.c.accent='Teal';self.c.units=[];self.c.cuts=[];self.c.bi=0
        self.c.beat=self.root
    def table(self,name,a,b,width=12,solid=True,top=0):
        # Preserve the playable top at Y=0; the furniture extends down into the room.
        self.box(self.root,name,(0,top-.6,(a+b)/2),(width,1.2,b-a),'Wood',solid)
        self.box(self.root,name+' brass edge',(0,top-1.25,(a+b)/2),(width+.15,.15,b-a+.15),'Gold',False)
        for side in (-1,1):
            self.box(self.root,name+' apron '+str(side),(side*(width/2-.4),top-2,(a+b)/2),(.5,2,b-a-1),'Wood',False)
            for z in (a+1.2,b-1.2):
                self.box(self.root,name+' carved leg '+str(side)+' '+str(z),(side*(width/2-1.2),-11,z),(1.8,20,1.8),'Wood',False)
                self.box(self.root,name+' leg ferrule '+str(side)+' '+str(z),(side*(width/2-1.2),-20.5,z),(2,1,2),'Gold',False)
    def book(self,z,phase):
        self.table('Royal folio desk '+str(z),z,z+16,18,True,-.85)
        g=self.group('Closing royal folio '+str(z),pos=(0,0,z))
        width=18;length=16
        self.box(g,'Walkable book pages',(0,-.25,8),(width,.5,length),'Ivory')
        self.box(g,'Fixed leather cover',(0,-.65,8),(width+.4,.3,length+.4),'Purple')
        self.box(g,'Gilded book spine',(-width/2,-.1,8),(.5,.8,length+.6),'Gold',False,shape='Cylinder')
        for line in range(7):
            self.box(g,'Printed folio line '+str(line),(0,.015,2+line*1.8),(12,.02,.07),'Slate',False)
        hinge=self.group('Horizontal closing cover',g,(-width/2,.35,8))
        self.box(hinge,'Moving leather cover',(-width/2,0,0),(width,.3,length+.4),'Purple',False)
        self.box(hinge,'Moving ivory page',(-width/2,.18,0),(width-.3,.04,length),'Ivory',False)
        self.box(hinge,'Royal cover crest',(-width/2,-.18,0),(4,.04,5),'Gold',False)
        strip=self.box(g,'Book warning bookmark',(width/2-.4,.025,8),(.4,.04,length),'Gold',False)
        volume=self.d.box(g,size=(width,4,16),center=(0,1.7,8),trigger=True)
        recovery=self.group('Previous safe book point',g,(0,0,-6 if z==-100 else -4))
        self.d.script(g,'ClosingBook',kit.fields(cover=kit.ref(hinge['tr']),recoveryPoint=kit.ref(recovery['tr']),crushVolume=kit.ref(volume),warningStrip=kit.ref(strip['comps'][2]),openSeconds=6,warningSeconds=1,closeSeconds=.65,closedSeconds=2,reopenSeconds=1.3,respawnSeconds=2,phaseSeconds=phase))
    def library_shelf(self,x,z,side):
        g=self.group('Royal library bookcase '+str(x)+' '+str(z),pos=(x,-22.5,z),rot=kit.yaw(-90 if side<0 else 90))
        self.box(g,'Walnut bookcase back',(0,17,1),(14,34,1),'Wood')
        for xside in (-7,7):self.box(g,'Carved upright',(xside,17,0),(1,34,3),'Wood')
        for level in range(5):
            y=level*8
            self.box(g,'Library shelf '+str(level),(0,y+.4,0),(14,.8,3),'Gold',False)
            if level==4:continue
            for i in range(9):
                height=4.5+(i+level)%3
                self.box(g,'Leather bound volume '+str(level)+' '+str(i),(-5.7+i*1.4,y+.8+height/2,-.5),(1.15,height,2),'Purple' if (i+level)%2 else 'Teal',False)
                self.box(g,'Book spine gold title '+str(level)+' '+str(i),(-5.7+i*1.4,y+2,-1.52),(.8,.3,.05),'Gold',False)
    def typewriter(self):
        import math
        g=self.group('Typewriter stairs - printed command',pos=(0,0,-51));ctx=self.c.context(g)
        areas=[];faces=[];visuals=[];keys=[];clues=[]
        # A black enamel case, brass round keys, exposed levers, ribbon spools and a wide paper carriage.
        self.box(g,'Black enamel keyboard bed',(0,-.55,7),(15.4,.3,14),'EnamelBlack')
        well=self.d.box(g,size=(15.4,2.8,14),center=(0,.7,7),trigger=True)
        for side in (-1,1):
            self.box(g,'Enamel side housing '+str(side),(side*10.1,2.1,20),(4,5.2,14),'EnamelBlack')
            self.box(g,'Low keyboard cheek '+str(side),(side*10.1,.35,6),(4,1.7,14),'EnamelBlack')
            self.box(g,'Polished side rail '+str(side),(side*8.2,1.25,6),(.25,.3,14),'Gold',False)
            self.box(g,'Curved front cheek '+str(side),(side*10,.3,0),(4,1.6,3),'EnamelBlack',False,shape='Sphere')
            spool=self.box(g,'Ink ribbon spool '+str(side),(side*8,5,17),(4,.25,4),'EnamelBlack',False,shape='Cylinder')
            self.box(g,'Spool brass hub '+str(side),(side*8,5.35,17),(.8,.15,.8),'Gold',False,shape='Cylinder')
            self.box(g,'Ribbon guide '+str(side),(side*4.7,5.4,18),(.2,1,.3),'Gold',False)
            self.box(g,'Carriage knob '+str(side),(side*11.7,7.5,21),(1.6,.5,1.6),'EnamelBlack',False,rot=(0,0,.70710678,.70710678),shape='Cylinder')
            for z in (1,23):self.box(g,'Rubber foot '+str(side)+' '+str(z),(side*9,-.8,z),(2,.4,2),'EnamelBlack',False,shape='Cylinder')
        self.box(g,'Crimson ink ribbon',(0,5.4,18),(16,.22,.12),'Purple',False)
        self.box(g,'Front brass rim',(0,-.05,-.3),(16,.3,.35),'Gold',False)
        for row in range(5):
            height=(row+1)*.32;z=row*2.8+1.4
            columns=5 if row%2==0 else 4
            for col in range(columns):
                x=(col-(columns-1)*.5)*3
                self.box(g,'Key stem '+str(row)+'-'+str(col),(x,height*.5-.3,z),(.3,max(.1,height*.5),.3),'Gold',False,shape='Cylinder')
                key=self.group('Key '+str(row)+'-'+str(col),g,(x,height-.15,z))
                self.d.rigidbody(key,True)
                keys.append(self.d.script(key,'TypewriterKey',kit.fields(pressDepth=.14,pressSpeed=.8,returnSpeed=1.5,jumpBonus=1.2)))
                rim=self.box(key,'Round brass key rim',(0,0,0),(2.8,.15,2.8),'Gold',True,shape='Cylinder')
                self.box(key,'Black key bezel',(0,.145,0),(2.64,.018,2.64),'EnamelBlack',False,shape='Cylinder')
                face=self.box(key,'Ivory letter disk',(0,.165,0),(2.42,.012,2.42),'Ivory',False,shape='Cylinder')
                visuals.append(face['comps'][2])
                faces.append(self.group('Key letter',key,(0,.195,0),kit.pitch(90))['tr'])
                areas.append(self.d.box(key,size=(2.8,.9,2.8),center=(0,.2,0),trigger=True))
        # The keyboard rises into the machine, through a high clear opening under its type basket.
        for i in range(17):
            angle=(i-8)*5
            self.box(g,'Type bar '+str(i),(math.sin(math.radians(angle))*5,5.6,17+math.cos(math.radians(angle))*2),
                (.12,.16,4),'Gold',False,rot=kit.yaw(angle))
        self.box(g,'Type basket fascia',(0,6.1,16.2),(13,1.2,.8),'EnamelBlack')
        self.box(g,'Inner sill',(0,1.3,16),(16,.6,4),'EnamelBlack')
        self.box(g,'Carriage steel rail',(0,7.2,21),(23,.3,.3),'TypewriterSteel',False)
        self.box(g,'Black platen roller',(0,7.8,21),(2.1,11,2.1),'EnamelBlack',False,rot=(0,0,.70710678,.70710678),shape='Cylinder')
        paper=self.group('Paper carriage',g,(0,11.5,22),kit.pitch(-8))
        self.box(paper,'Paper backing',(0,0,0),(17,7,.12),'White',False)
        self.box(g,'Paper pressure bar',(0,8.5,20.3),(19,.22,.25),'Gold',False)
        for line in range(7):self.box(paper,'Printed paper line '+str(line),(0,-2+line*.55,-.075),(12-line%3,.06,.025),'Slate',False)
        self.box(g,'Carriage nameplate',(0,9,20),(10,1.2,.3),'EnamelBlack',False)
        nameplate=self.group('Carriage branding',g,(0,9,19.8))
        self.box(g,'Keyboard instruction plate',(0,4.8,14.2),(10,1.5,.2),'Ivory',False)
        document=self.group('Printed order text',g,(0,4.8,14.08))
        # Enclosed continuation: climbing into the typewriter leads directly to the headquarters door.
        for side in (-1,1):self.wall(g,'Inside typewriter wall '+str(side),(side*8.4,3.4,27.5),(.8,6.8,27))
        self.box(g,'Inside typewriter roof',(0,7,27.5),(16,.4,27),'EnamelBlack')
        angle=math.atan2(1.6,6)
        self.box(g,'Typewriter interior descent',(0,.8-.08*math.cos(angle),21),
            (16,.16,math.sqrt(36+1.6**2)),'EnamelBlack',rot=kit.pitch(math.degrees(angle)))
        for i,z in enumerate((-138,-112,-80,-58,-53)):
            x=-8 if i%2==0 else 8
            plaque=self.group('Memory clue '+str(i+1),g,(x,3.8,z+51))
            self.box(plaque,'Brass clue frame',(0,0,0),(3.2,3.2,.3),'Gold',False)
            self.box(plaque,'Clue paper',(0,0,-.18),(2.9,2.9,.04),'White',False)
            clues.append(self.group('Random clue glyph',plaque,(0,0,-.22))['tr'])
        data=kit.fields(context=kit.ref(ctx),documentFace=kit.ref(document['tr']),nameplateFace=kit.ref(nameplate['tr']),keyboardWell=kit.ref(well),
            backwardSpeed=12,upwardSpeed=6,knockdownSeconds=.7,hitCooldown=1,flashSeconds=.3)
        for name,values in [('keyAreas',areas),('keyFaces',faces),('keyVisuals',visuals),('keys',keys),('clueFaces',clues)]:
            data+='  '+name+':\n'+''.join('  - '+kit.ref(x)+'\n' for x in values)
        self.d.script(g,'TypewriterStairs',data)
    def exhibit(self,pos,fraction,rotation=0):
        g=self.group('Random GM replay preview '+str(fraction),pos=pos,rot=kit.yaw(rotation))
        self.box(g,'Framed score board',(0,4,0),(10,10,.5),'Wood')
        for sq in range(64):
            self.box(g,'Square '+str(sq),(sq%8-3.5,4+sq//8-3.5,-.3),(.98,.98,.08),'White' if (sq%8+sq//8)%2 else 'Slate',False)
        self.d.script(g,'WarRoomScoreLabel',kit.fields(previewFraction=fraction))
    def save(self):
        for x in (-160,160):self.wall(self.root,'Square perimeter side '+str(x),(x,20,10),(2,60,320))
        for x in (-83,83):self.wall(self.root,'Square perimeter entrance '+str(x),(x,20,-150),(154,60,2))
        self.wall(self.root,'Entrance header',(0,20,-150),(12,20,2))
        self.box(self.root,'Operations ceiling',(0,30,-80),(320,1,140),'Slate')
        # An open room with discrete tables, not a walled corridor. Gaps stay open for jumps/maps.
        self.box(self.root,'Distant operations room floor',(0,-23,-80),(318,1,138),'Slate',False)
        self.table('Approach floor -146',-146,-129,18)
        self.table('Dispatch table after jump',-127,-100,18)
        self.table('Approach floor -60',-60,-51,18)
        self.table('Approach floor -27',-27,-8,16)
        self.table('Typewriter furniture base',-51,-27,25,True,-.8)
        self.press(-122,-3,6,0);self.press(-122,3,6,2.675)
        self.book(-100,0);self.book(-76,3)
        self.table('Fixed seal island',-84,-76,18)
        self.box(self.root,'Royal seal rest island',(0,.025,-80),(5,.05,5),'Gold',False,shape='Cylinder')
        self.typewriter()
        self.headquarters()
        for obj in self.d.all:
            if obj['name']=='HQ rear wall':obj['pos']=(-66,24,170);obj['scale']=(118,68,2)
        self.wall(self.hq,'HQ rear exit right',(66,24,170),(118,68,2))
        self.wall(self.hq,'HQ rear exit header',(0,34,170),(14,48,2))
        self.table('Final exit walkway',146,170)
        self.exit['pos']=(0,0,169)
        # The outer square's north edge is the HQ rear wall extended to both corners.
        for x in (-142,142):self.wall(self.root,'North perimeter return '+str(x),(x,20,170),(36,60,2))
        # Broad library walls leave the table route open, with framed score previews hung between shelves.
        for side in (-1,1):
            self.wall(self.root,'Royal library gallery wall '+str(side),(side*38,4,-80),(1.5,54,140))
            for z in (-140,-102,-64,-24):self.library_shelf(side*35,z,side)
        for pos,ply,rotation in (((-37,4,-121),0,-90),((37,4,-121),.25,90),((-37,4,-45),.55,-90),((37,4,-45),.9,90)):
            self.exhibit(pos,ply,rotation)
        entry=self.group('Entry - travel forward',pos=(0,0,-142))
        cps=[self.group('Checkpoint '+str(i),pos=(0,0,z)) for i,z in enumerate((-106,-80,-30),1)]
        end=self.group('Approach exit',pos=(0,0,-4));self.chessEntry=end
        route=[entry]+[self.group('Forward waypoint '+str(z),pos=(0,0,z)) for z in (-128,-106,-100,-84,-80,-76,-60,-49,-38,-30,-18)]+[end]
        def arr(name,objs):return '  '+name+':\n'+''.join('  - '+kit.ref(o['tr'])+'\n' for o in objs)
        module=self.d.script(self.root,'CourseModule',kit.fields(courseId='"3-1-HQ"',displayName='"왕의 서재와 거대 체스판"',entry=kit.ref(entry['tr']),exit=kit.ref(self.exit['tr']),minigameSlot=kit.ref(self.hq['tr']),targetSeconds=150,laneWidth=18)+arr('checkpoints',cps)+arr('route',route)+'  placementBounds:\n    m_Center: {x: 0, y: 20, z: 10}\n    m_Extent: {x: 162, y: 50, z: 162}\n')
        self.c.context(self.root);self.d.script(self.root,'PhysicsProfile',kit.fields(physicsRate=120,solverIterations=24))
        self.d.script(self.root,'GiantChessCourse',kit.fields(pawnPrefab='{fileID: 7684884123511159585, guid: fe69acb7e49fea745aa289c4d39f3e62, type: 3}',tuning='{fileID: 11400000, guid: f387368cafccca042ac935642cf094be, type: 2}',tileSize=16,replaySpeed=5,moveSeconds=3,warningSeconds=5,restSeconds=2,knockbackSpeed=12,upwardSpeed=9,botsEnabled=1,boardPreview=kit.ref(self.preview['go']),chessSpawnDepth=4))
        self.d.script(self.root,'GiantChessApproach',kit.fields(course=kit.ref(module),chessEntry=kit.ref(end['tr']))+'  transitionRoute: []\n')
        text,ids=stabilize(clean(self.d.render(scene=False)));self.serialized_root=ids[self.root['tr']]
        self.report.update(root=ids[self.root['go']],root_transform=self.serialized_root,local_id_scheme='hierarchy-type-sha256-v2',target_seconds=[120,180],preview_fractions=[0,.25,.55,.9])
        return text

def scene(prefab_root):
    text=(ROOT/SCENE).read_text();data=blocks(text)
    # Retain the existing camera/light/settings verbatim; replace only gameplay roots.
    kept=[]
    for key,b in data.items():
        if key>=9000000000000000000 or key in (1667498617,1667498618,1667498619,1667498620,1667498621,1667498622,950000,950001,950002,960000,960001):continue
        kept.append(b)
    guid=re.search(r'^guid: (\w+)',(ROOT/(PREFAB+'.meta')).read_text(),re.M)[1]
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
    outputs={PREFAB:prefab,SCENE:scene(b.serialized_root),REPORT:json.dumps(b.report,ensure_ascii=False,indent=2)+'\n'}
    if args.check:
        stale=[p for p,s in outputs.items() if not (ROOT/p).exists() or (ROOT/p).read_text()!=s]
        if stale:raise SystemExit('Differs from authoring source: '+', '.join(stale))
        print('Complete war-room prefab, scene and report match.');return
    if args.write and (ROOT/PREFAB).exists():raise SystemExit('Prefab exists. Use --check or explicitly --overwrite.')
    for p,s in outputs.items():(ROOT/p).write_text(s)
    subprocess.run([sys.executable,'Tools/Generators/mkmeta.py',PREFAB],cwd=ROOT,check=True)
    print('Saved one complete war-room prefab and its scene instance.')
if __name__=='__main__':main()
