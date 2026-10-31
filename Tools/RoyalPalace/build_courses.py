#!/usr/bin/env python3
"""Build ONLY the R115 palace prototypes. Never runs the legacy scene generator.
Use --write for first creation, --overwrite to intentionally discard Unity edits,
or --check for a read-only reproducibility check against the saved files.
"""
import argparse
import ast
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
# Reuse the established YAML serializer without importing its destructive entrypoint.
source = ast.parse((ROOT / 'Tools/Generators/gen_scenes.py').read_text())
wanted = {'HEAD','BUILTIN','MESH','RENDERER','COLLIDER_COMMON','RIGIDBODY',
          'Doc','meta_guid','v3','q4','fmt'}
nodes = [n for n in source.body if isinstance(n,(ast.FunctionDef,ast.ClassDef)) and n.name in wanted
         or isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id in wanted for t in n.targets)]
ns = {'re':re, 'math':math}
exec(compile(ast.Module(body=nodes,type_ignores=[]),'<serializer>','exec'),ns)
Doc, HEAD, v3 = ns['Doc'], ns['HEAD'], ns['v3']
ns['MESH']['Cylinder'] = 10206
SCRIPT = {p.stem: re.search(r'guid: (\w+)',p.with_suffix('.cs.meta').read_text())[1]
          for p in (ROOT/'Assets').rglob('*.cs') if p.with_suffix('.cs.meta').exists()}
ns['SCRIPT'] = SCRIPT
BASE = 'Assets/Maps/RoyalPalace'
outputs = {}
reports = []

def guid(path): return hashlib.md5(('chessfight:'+path).encode()).hexdigest()
def ref(n): return '{fileID: %d}' % n
def external(n,path): return '{fileID: %d, guid: %s, type: 3}' % (n,guid(path))
def yaw(deg): return (0,math.sin(math.radians(deg)/2),0,math.cos(math.radians(deg)/2))
def pitch(deg): return (math.sin(math.radians(deg)/2),0,0,math.cos(math.radians(deg)/2))
def fields(**kwargs):
    return ''.join('  '+key+': '+(str(value) if not isinstance(value,(list,tuple)) else v3(value))+'\n' for key,value in kwargs.items())

def material(name,color):
    path=f'{BASE}/Materials/{name}.mat'
    shader=re.search(r'guid: (\w+)',(ROOT/'Assets/Materials/NetworkColor.shader.meta').read_text())[1]
    outputs[path]=HEAD+f'''--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {shader}, type: 3}}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats: []
    m_Colors:
    - _Color: {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}
  m_BuildTextureStacks: []
'''
    return guid(path)

MAT={name:material(name,color) for name,color in {
    'Ivory':(.83,.78,.63),'Slate':(.15,.21,.29),'Gold':(.88,.59,.16),
    'Purple':(.27,.12,.39),'Wood':(.27,.13,.08),'Teal':(.12,.42,.40),
    'Warning':(.93,.28,.12),'Night':(.08,.12,.24),'White':(.94,.92,.84),
    'Safe':(.28,.65,.48),'Blue':(.22,.43,.70)}.items()}

class Course:
    def __init__(self, design, slug, accent):
        self.design=design; self.slug=slug; self.accent=accent
        self.d=Doc(100000); self.root=self.d.go(slug); self.context(self.root)
        self.beats=self.d.go('Beats - edit each obstacle component',parent=self.root)
        self.props=self.d.go('Architecture and theme',parent=self.root)
        self.anchors=self.d.go('Connectors and checkpoints',parent=self.root)
        self.units=[]; self.cuts=[]; self.elevations=[]; self.beat=None; self.bi=0
        self.route=[]; self.cps=[]
        self.entry=self.marker('Entry - travel forward',(0,0,-6))
        self.route.append(self.entry)
        self.cube(self.props,'Entry apron',(0,-.4,-6),(12,.8,12),'Ivory')
        self.arch(self.props,(0,0,-10),'ENTRY')
    def cube(self,parent,name,pos,size,mat='Ivory',solid=True,rot=(0,0,0,1),shape='Cube'):
        g=self.d.go(name,pos,rot,size,parent)
        self.d.mesh(g,shape,MAT[mat])
        if solid:
            if shape=='Cube': self.d.box(g)
            else:
                self.d.raw(g,'64','MeshCollider',ns['COLLIDER_COMMON']%0+f'  serializedVersion: 5\n  m_Convex: 1\n  m_CookingOptions: 30\n  m_Mesh: {{fileID: {ns["MESH"][shape]}, guid: {ns["BUILTIN"]}, type: 0}}\n')
        return g
    def marker(self,name,pos,rot=(0,0,0,1),parent=None):
        return self.d.go(name,pos,rot,parent=parent or self.anchors)
    def group(self,name,z,x=0,y=0,rot=(0,0,0,1),kind=None):
        g=self.d.go(name,(x,y,z),rot,parent=self.beat)
        if kind: self.units.append({'beat':self.bi+1,'kind':kind,'name':name,'local_z':z})
        return g
    def context(self,g): return self.d.script(g,'ObstacleContext')
    def cut(self,a,b): self.cuts.append((a,b))
    def impact(self,shape,motion,ctx,size,pendulum=False):
        hit=self.d.go('Contact impulse - trigger',scale=tuple(s*1.04 for s in size),parent=shape)
        self.d.box(hit,trigger=True)
        self.d.script(hit,'ObstacleImpact',fields(**{'pendulum' if pendulum else 'motion':ref(motion),'context':ref(ctx)}))
    def arch(self,parent,pos,name):
        g=self.d.go(name+' open arch',pos,parent=parent)
        for x in (-6.65,6.65):
            self.cube(g,'Ivory pier',(x,2.8,0),(1.3,5.6,1.4),'Ivory')
            self.cube(g,'Gold capital',(x,5.4,0),(1.6,.35,1.7),'Gold')
        self.cube(g,'Lintel - clear height 5.5m',(0,6.1,0),(14.6,1.2,1.4),'Gold')
        for x in (-7.5,7.5):
            self.cube(g,'Tied purple curtain',(x,3.2,.2),(1,4.6,.6),'Purple',False)
            self.cube(g,'Curtain tie',(x,2.4,.15),(1.15,.3,.8),'Gold',False)
    def conveyor(self,z,x=0,width=12,length=12):
        g=self.group('08 Conveyor - reverse travel',z,x,kind='conveyor'); self.context(g)
        self.cut(z-length/2,z+length/2)
        self.cube(g,'Belt deck',(0,-.3,0),(width,.6,length),'Slate')
        marks=[]
        for n in range(6):
            marks.append(self.cube(g,'Belt direction stripe',(0,.015,-length/2+n*length/6),(width,.025,.12),self.accent,False))
        self.d.script(g,'ConveyorChessboard',fields(direction=(0,0,-1),conveyorSpeed=1.4,beltLength=length)+
                      '  beltMarkers:\n'+''.join('  - '+ref(m['tr'])+'\n' for m in marks))
    def bar(self,z):
        g=self.group('Low rotating pointer - jump or time',z,y=.38,kind='low_bar');ctx=self.context(g);self.d.rigidbody(g,True)
        mo=self.d.script(g,'RotatingHammer',fields(axis=(0,1,0),rotationSpeed=30,pushForce=5,lift=1,hitCooldown=.65))
        self.cube(g,'Gold pointer',(0,0,0),(12,.45,.45),'Gold')
        self.impact(g,mo,ctx,(12,.5,.5))
    def disc(self,z,rotating=True):
        g=self.group('01 Round strategy table' if rotating else '01 Static vortex surface',z,kind='round_table' if rotating else 'vortex')
        self.context(g);self.cut(z-6,z+6)
        surface=self.d.go('Turntable geometry' if rotating else 'Static geometry',scale=(1.75,1,1.75),parent=g)
        self.cube(surface,'Circular deck',(0,-.3,0),(8,.3,8),self.accent,shape='Cylinder')
        for i in range(8):
            t=i*math.pi/4
            self.cube(surface,'Rim marker',(6/1.75*math.sin(t),.025,6/1.75*math.cos(t)),(.7,.04,.7),'Gold',False)
        self.d.script(g,'SpinningDisc',fields(diameter=14,rotationDirection=0,rotationSpeed=18,
                          geometryRoot=ref(surface['tr']),rotatingSurface=ref(surface['tr'] if rotating else 0)))
    def pusher(self,z,opposite=False):
        sign=-1 if opposite else 1
        g=self.group('03 High pusher - wait for withdrawal',z,6*sign,kind='pusher'); ctx=self.context(g);self.d.rigidbody(g,True)
        mo=self.d.script(g,'ReciprocatingPusher',fields(axis=(sign,0,0),distance=6,moveSpeed=6,phase=-.25,useLocalTranslationAxes=1,pushForce=8,lift=1.5))
        self.cube(g,'High strategy rod',(0,.8,0),(12,1.6,1.2),self.accent)
        hit=self.d.go('Pusher contact',(0,.8,0),parent=g);self.impact(hit,mo,ctx,(12,1.6,1.2))
    def sliders(self,z):
        for sign in (-1,1):
            g=self.group('06 Sliding wall '+str(sign),z,3*sign,kind='sliding_wall');ctx=self.context(g);self.d.rigidbody(g,True)
            mo=self.d.script(g,'SlidingWall',fields(axis=(sign,0,0),distance=6,moveSpeed=8,phase=0,useLocalTranslationAxes=1,strength=6,lift=1))
            self.cube(g,'Wall panel',(0,2,0),(6,4,1.2),self.accent)
            for y in (.4,3.6): self.cube(g,'Panel trim',(0,y,-.63),(5.6,.15,.08),'Gold',False)
            hit=self.d.go('Wall contact',(0,2,0),parent=g);self.impact(hit,mo,ctx,(6,4,1.2))
    def gate(self,z):
        unit=self.group('09 Paired chess-clock gate',z,kind='clock_gate')
        for sign in (-1,1):
            g=self.d.go('Alternating gate leaf',(3*sign,0,0),parent=unit);ctx=self.context(g);self.d.rigidbody(g,True)
            self.d.script(g,'ChessClockGate',fields(axis=(0,1,0),distance=5,moveSpeed=9,phase=0 if sign<0 else .5,useLocalTranslationAxes=1))
            self.cube(g,'Portcullis',(0,2,0),(5.9,4,.7),'Slate')
            self.cube(g,'Clock face',(0,3,-.4),(2,.8,.1),'White',False)
            self.cube(g,'Clock hand',(0,3,-.48),(.1,.6,.05),'Gold',False)
    def lifts(self):
        for z,phase in ((21,-.25),(42,.25)):
            g=self.group('05 Rising floor - ride to upper gallery',z,y=1.5,kind='rising_tiles');self.context(g);self.d.rigidbody(g,True)
            self.d.script(g,'RisingFallingTiles',fields(axis=(0,1,0),distance=1.5,moveSpeed=1.1,phase=phase,useLocalTranslationAxes=1))
            self.cube(g,'Elevator deck',(0,-.35,0),(12,.7,6),self.accent)
            for x in (-5.7,5.7): self.cube(g,'Gold edge',(x,.02,0),(.12,.04,6),'Gold',False)
            self.cut(z-3,z+3)
        self.elevations.append((24,39,3))
        # Handholds belong to the designated 3m rise, not an invisible ordinary jump.
        for x in (-5.4,5.4):
            self.cube(self.beat,'Climb wall',(x,1.5,24.4),(.8,3,.8),'Slate')
            for y in (.5,1.1,1.7,2.3,2.9): self.cube(self.beat,'Gold climb hold',(x,y,23.9),(1,.16,.35),'Gold')
    def hammer(self,z):
        g=self.group('04 Vertical armillary hammer',z,y=5.7,kind='hammer');ctx=self.context(g);self.d.rigidbody(g,True)
        mo=self.d.script(g,'RotatingHammer',fields(axis=(0,0,1),rotationSpeed=35,pushForce=9,lift=2))
        self.cube(g,'Arm',(0,-2.4,0),(.4,4.8,.4),'Gold')
        self.cube(g,'Hammer head',(0,-4.8,0),(3,1.8,1.8),self.accent)
        hit=self.d.go('Head contact',(0,-4.8,0),parent=g);self.impact(hit,mo,ctx,(3,1.8,1.8))
    def pendulum(self,z):
        g=self.group('11 Royal pendulum',z,y=7,kind='pendulum');ctx=self.context(g);self.d.rigidbody(g,True)
        self.cube(g,'Chain',(0,-2.7,0),(.18,5.4,.18),'Gold',False)
        ball=self.cube(g,'Swinging weight',(0,-5.4,0),(2.4,2.4,2.4),self.accent,shape='Sphere')
        mo=self.d.script(g,'PendulumSwing',fields(context=ref(ctx),Ball=ref(ball['tr']),SwingAngle=60,SwingDuration=2.8,KnockbackForce=10,UpwardForce=2))
        hit=self.d.go('Weight contact',(0,-5.4,0),parent=g);self.impact(hit,mo,ctx,(2.4,2.4,2.4),True)
    def falling(self,z,theme='Crown',phase=0):
        g=self.group('10 Falling '+theme,z,kind='falling_piece');ctx=self.context(g)
        p=self.d.go(theme+' visual',(0,9,0),parent=g)
        self.cube(p,'Base',(0,.3,0),(3,.6,3),'Gold',False,shape='Cylinder')
        self.cube(p,'Body',(0,1.5,0),(1.6,2,1.6),self.accent,False)
        if theme=='Knight':
            self.cube(p,'Horse head',(0,2.8,-.5),(1.2,1,2),'Ivory',False)
            for x in (-.4,.4): self.cube(p,'Ear',(x,3.5,0),(.3,.6,.4),'Gold',False)
        else:
            for x in (-.9,0,.9): self.cube(p,'Crown prong',(x,2.9,0),(.4,1,1.5),'Gold',False)
        warning=self.cube(g,'Red warning radius',(0,.035,0),(1,.02,1),'Warning',False,shape='Cylinder')
        shock=self.d.go('Shock wave',parent=g)
        for i in range(12):
            a=i*math.pi/6
            self.cube(shock,'Shock segment',(.48*math.sin(a),.16,.48*math.cos(a)),(.07,.08,.07),'Gold',False)
        self.d.script(g,'FallingChessPiece',fields(context=ref(ctx),piece=ref(p['tr']),warning=ref(warning['tr']),shockRing=ref(shock['tr']),
            dropHeight=9,warningSeconds=1.5,fallSeconds=.7,shockSeconds=.8,restSeconds=2,phaseOffset=phase,shockRadius=7,knockback=10,lift=2,coverBlocksShock=1))
        for sign in (-1,1): self.cube(self.beat,'Shock cover',(4.3*sign,1.1,z-5),(2.4,2.2,1),'Slate')
    def bridge(self,z):
        unit=self.group('12 Upward royal drawbridge - timed variant',z,kind='drawbridge')
        self.cut(z-6,z+6)
        for sign in (-1,1):
            g=self.d.go('Hinged leaf - angle and timing',(0,0,6*sign),parent=unit);self.context(g);self.d.rigidbody(g,True)
            self.d.script(g,'RoyalDrawbridgeLeaf',fields(openAngle=65,closedSeconds=5,travelSeconds=2,openSeconds=2,reverse=1 if sign>0 else 0))
            self.cube(g,'Leaf deck',(0,-.25,-3*sign),(12,.5,6),'Wood')
            for x in (-5.6,5.6): self.cube(g,'Leaf gilded edge',(x,.015,-3*sign),(.15,.03,6),'Gold',False)
    def jump(self):
        for x in (-3,3):
            g=self.group('02 Jump pad - steer in air',22,x,kind='jump_pad');self.context(g)
            self.cube(g,'Pad',(0,-.2,0),(6,.4,4),'Gold')
            self.d.script(g,'JumpPad',fields(jumpHeight=4.1,airMoveSpeed=6,airAcceleration=12,launchCooldown=.7))
        self.cut(20,26)
        self.elevations.append((26,44,1.5))
        # A long ramp returns to floor height after the broad elevated landing.
        self.cut(44,50)
        self.ramp(self.beat,'Landing exit ramp',(0,.75,47),12,6,-1.5)
        for x in (-5.4,5.4):
            for y in (.4,.9,1.4): self.cube(self.beat,'Landing handhold',(x,y,25.8),(1,.16,.4),'Gold')
    def ramp(self,parent,name,pos,width,length,rise):
        # Thin box top is continuous at both endpoints; visual stairs can sit beneath it.
        angle=-math.degrees(math.atan2(rise,length))
        self.cube(parent,name,(pos[0],pos[1]-.1,pos[2]),(width,.2,math.hypot(length,rise)), 'Ivory',rot=pitch(angle))
    def cavalry(self,z):
        g=self.group('17 Knight cavalry charge',z,kind='cavalry');ctx=self.context(g)
        start=self.marker('Start',(0,0,10),parent=g);land=self.marker('Landing',(0,0,4),parent=g);end=self.marker('Charge end',(0,0,-10),parent=g)
        body=self.d.go('Kinematic knight',(0,0,10),parent=g);rb=self.d.rigidbody(body,True)
        visual=self.d.go('Knight visual',parent=body)
        self.cube(visual,'Horse body',(0,1,0),(3,2,3),self.accent,False)
        self.cube(visual,'Horse head',(0,2.6,-.7),(1.6,1.2,2.4),'Ivory',False)
        collider=self.d.box(body,size=(3,3,3),center=(0,1.5,0),trigger=True)
        warn=self.cube(g,'Landing warning',(0,.03,4),(8,.04,5),'Warning',False)
        charge=self.cube(g,'Charge warning',(0,.04,-3),(4,.04,14),'Gold',False)
        self.d.script(g,'KnightCavalryCharge',fields(context=ref(ctx),KnightRoot=ref(rb),Visual=ref(visual['tr']),StartPoint=ref(start['tr']),
            LandingPoint=ref(land['tr']),ChargeEndPoint=ref(end['tr']),BodyCollider=ref(collider),LandingWarning=ref(warn['go']),ChargeWarning=ref(charge['go']),
            LeapDuration=1.2,ChargeDuration=2,LandingRadius=4,ChargeKnockbackForce=10,ChargeUpwardForce=2,CooldownDuration=3))
    def castling(self,z):
        g=self.group('18 Castling crossing platforms',z,rot=yaw(90),kind='castling');ctx=self.context(g)
        self.cut(z-11.3,z+11.3)
        static=self.d.go('StaticStructure - decks have clear sweeps',parent=g)
        docks=[self.marker('Dock '+str(s),(s*8,0,0),parent=g) for s in (-1,1)]
        rbs=[];cols=[]
        for i,s in enumerate((-1,1)):
            b=self.d.go('Transport deck '+str(i),(s*8,0,0),parent=g);rbs.append(self.d.rigidbody(b,True))
            self.cube(b,'Deck visual',(0,-.35,0),(6,.7,12),self.accent,False)
            cols.append(self.d.box(b,size=(6,.7,12),center=(0,-.35,0)))
        self.d.script(g,'CastlingCrossingBridges',fields(context=ref(ctx),BridgeA=ref(rbs[0]),BridgeB=ref(rbs[1]),DeckA=ref(cols[0]),DeckB=ref(cols[1]),
            LeftDock=ref(docks[0]['tr']),RightDock=ref(docks[1]['tr']),StaticStructure=ref(static['tr']),DetourDistance=7,DockedDuration=4,SwapDuration=8,MinimumClearance=.1))
        # Observation dais on the approach, with ordinary sub-0.6m steps.
        for i in range(4): self.cube(self.beat,'Observation step',(4.5,.2*(i+1)-.4,z-17+i),(3,.8,1),'Slate')
    def vent(self,z):
        g=self.group('15 Rectangular observatory wind',z,kind='air_vent');ctx=self.context(g)
        trigger=self.d.box(g,size=(12,4,8),center=(0,2,0),trigger=True)
        warn=self.cube(g,'Warning grille',(0,.025,0),(12,.03,8),'Warning',False)
        wind=self.d.go('Wind stream markers',parent=g)
        for x in (-4,-2,0,2,4): self.cube(wind,'Stream',(x,1,0),(.12,.12,6),'Blue',False)
        self.d.script(g,'AirVent',fields(context=ref(ctx),WindTrigger=ref(trigger),WarningVisual=ref(warn['go']),WindVisual=ref(wind['go']),
            WindDirection=(0,0,-1),WindStrength=8,MaxPushSpeed=5,IdleDuration=4,WarningDuration=1,BlowDuration=3))
    def begin(self,i):
        self.bi=i;self.cuts=[];self.elevations=[]
        self.beat=self.d.go(f'Beat {i+1:02} - {self.design["beats"][i]["seconds"]}s target',(24*i,0,0 if i%2==0 else 72),yaw(0 if i%2==0 else 180),parent=self.beats)
        self.route.append(self.marker(f'Route {i+1} start',(0,0,0),parent=self.beat))
    def finish_beat(self):
        breaks=sorted(set([0,72]+[v for cut in self.cuts for v in cut]+[v for a,b,h in self.elevations for v in (a,b)]))
        for a,b in zip(breaks,breaks[1:]):
            mid=(a+b)/2
            if any(ca<mid<cb for ca,cb in self.cuts):continue
            h=next((h for ea,eb,h in self.elevations if ea<=mid<=eb),0)
            self.cube(self.beat,'Fixed route floor',(0,h-.4,mid),(12,.8,b-a),'Ivory')
            for x in (-5.8,5.8): self.cube(self.beat,'Inlaid lane edge',(x,h+.015,mid),(.12,.025,b-a),'Gold',False)
        # Distinct room dressing is outside the traversable strip, with no bypass floor.
        for z in (8,36,64):
            if any(u['beat']==self.bi+1 and u['kind']=='castling' and abs(u['local_z']-z)<16 for u in self.units): continue
            for x in (-8,8):
                self.cube(self.beat,'Gallery pillar',(x,3,z),(1.2,6,1.2),'Slate')
                self.cube(self.beat,'Capital',(x,6,z),(1.8,.4,1.8),'Gold',False)
                if self.slug=='RoyalVault':
                    for y in (.3,.7,1.1):self.cube(self.beat,'Coin stack',(x,y,z+2),(2,.2,2),'Gold',False,shape='Cylinder')
                elif self.slug=='RoyalWardrobe':
                    self.cube(self.beat,'Cape display',(x,3,z+2),(1.5,4,.4),'Purple',False)
                elif self.slug=='RoyalObservatory':
                    self.cube(self.beat,'Star globe',(x,4,z+2),(2,2,2),'Blue',False,shape='Sphere')
                else:
                    self.cube(self.beat,'Map display',(x,3,z+2),(1.8,2.4,.3),self.accent,False)
        self.route.append(self.marker(f'Route {self.bi+1} end',(0,0,72),parent=self.beat))
        if self.bi<5:
            x=24*self.bi+12;z=72 if self.bi%2==0 else 0
            self.cube(self.props,'Turn connector',(x,-.4,z),(24,.8,12),'Slate')
            # The only cross-lane connections sit at the ends of alternating lanes.
            for s in (-1,1):self.cube(self.props,'Turn outer rail',(x,.55,z+s*6),(12,1.1,.25),'Gold')
        if self.bi in (1,3):
            cp=self.marker('Checkpoint '+str(len(self.cps)+1),(24*(self.bi+1),0,0));self.cps.append(cp)
            self.cube(self.props,'Checkpoint green inlay',(24*(self.bi+1),.025,1),(11,.04,2),'Safe',False)
    def boundary(self):
        # Negative Z is travel after the last lane. All hazards end before this runout.
        g=self.d.go('Boundary - 18m quiet runout and replaceable hall',(120,0,0),yaw(180),parent=self.root)
        self.cube(g,'Quiet runout',(0,-.4,9),(12,.8,18),'Ivory')
        self.cube(g,'Checkpoint terrace 12 x 6',(0,-.4,21),(12,.8,6),'Ivory')
        self.cps.append(self.marker('Checkpoint 3 terrace',(0,0,21),parent=g))
        self.route.append(self.cps[-1])
        self.arch(g,(0,0,23),'OPEN BOUNDARY')
        for i in range(4):self.cube(g,'Visual stair 0.25m',(0,-.25*(i+1)-.2,24.5+i),(12,.4,1),'Ivory')
        self.ramp(g,'Continuous walk collision',(0,-.5,26),12,4,-1)
        self.cube(g,'Minigame entry strip',(0,-1.4,30),(12,.8,4),'Slate')
        hall=self.d.go('Transition hall - fixed connection geometry',(0,-1,32),parent=g)
        slot=self.d.go('MinigameSlot - replace contents only',parent=hall)
        self.cube(hall,'Hall 44 x 25',(0,-.4,12.5),(44,.8,25),'Slate')
        for x,color in ((-12,'White'),(12,'Night')):
            self.cube(slot,'Team 20 x 20 placeholder',(x,.015,12),(20,.03,20),color,False)
            for dz in (2,22):self.cube(slot,'Area trim',(x,.04,dz),(20,.04,.12),'Gold',False)
        for x in (-22.3,22.3):self.cube(hall,'Hall outer wall',(x,2.5,12.5),(.6,5,25),'Ivory')
        self.cube(hall,'Decorative central closed door',(0,2.5,24.6),(4,5,.6),'Purple')
        for x in (-14,14):
            self.arch(hall,(x,0,25),'TEAM EXIT')
            self.cube(hall,'Exit approach',(x,-.4,28),(12,.8,6),'Ivory')
        self.cube(hall,'Shared exit apron',(0,-.4,34),(40,.8,6),'Ivory')
        self.cube(hall,'Merged next-course socket',(0,-.4,40),(12,.8,6),'Ivory')
        exit=self.marker('Exit - travel forward',(0,0,42),parent=hall)
        # The route follows a side door, not the decorative closed central door.
        self.route.extend([self.marker('Route hall entry',(0,0,0),parent=hall),self.marker('Route side exit',(-14,0,28),parent=hall),self.marker('Route merge',(0,0,34),parent=hall),exit])
        return slot,exit
    def save(self):
        hall,exit=self.boundary()
        def world(g):
            p=g['pos']; parent=g['parent']
            while parent:
                p=tuple(a*b for a,b in zip(p,parent['scale']))
                x,y,z,w=parent['rot'];a,b,c=p
                tx,ty,tz=2*(y*c-z*b),2*(z*a-x*c),2*(x*b-y*a)
                p=(a+w*tx+y*tz-z*ty,b+w*ty+z*tx-x*tz,c+w*tz+x*ty-y*tx)
                p=tuple(a+b for a,b in zip(p,parent['pos']));parent=parent['parent']
            return p
        script=self.d.script(self.root,'CourseModule',fields(courseId=json.dumps(self.design['id']),displayName=json.dumps(self.design['name'],ensure_ascii=False),
                entry=ref(self.entry['tr']),exit=ref(exit['tr']),minigameSlot=ref(hall['tr']),targetSeconds=240,laneWidth=12)+
                '  checkpoints:\n'+''.join('  - '+ref(p['tr'])+'\n' for p in self.cps)+
                '  route:\n'+''.join('  - '+ref(p['tr'])+'\n' for p in self.route)+
                '  placementBounds:\n    m_Center: {x: 67, y: 5, z: 1}\n    m_Extent: {x: 80, y: 15, z: 81}\n')
        path=f'{BASE}/Courses/{self.design["id"]}_{self.slug}.prefab'
        outputs[path]=self.d.render(scene=False)
        reports.append({'id':self.design['id'],'name':self.design['name'],'prefab':path,'root':self.root['go'],'transform':self.root['tr'],'component':script,'hazards':self.units,
            'route_length_m':round(sum(math.dist(world(a),world(b)) for a,b in zip(self.route,self.route[1:])),2),'checkpoints':3,'target_seconds':240,'prototype_only':True})
        return path,script

def build():
    data=json.loads((ROOT/'Tools/RoyalPalace/design.json').read_text())
    slugs=['RoyalWarRoom','RoyalVault','RoyalStudy','RoyalWardrobe','RoyalObservatory']
    accents=['Teal','Gold','Wood','Purple','Blue']
    for n,(design,slug,accent) in enumerate(zip(data,slugs,accents)):
        c=Course(design,slug,accent)
        for i in range(6):
            c.begin(i)
            if n==0:
                if i==0:c.conveyor(22);c.conveyor(48);c.bar(22);c.bar(48)
                if i==1:c.disc(20);c.pusher(42);c.pusher(58,True)
                if i==2:c.falling(24,'Knight');c.falling(50,'Knight',2)
                if i==3:c.lifts()
                if i==4:c.cavalry(24);c.sliders(55)
                if i==5:c.bridge(26);c.gate(53)
            if n==1:
                if i==0:c.lifts();c.bar(12);c.bar(58)
                if i==1:c.gate(16);c.sliders(36);c.gate(58)
                if i==2:c.hammer(23);c.hammer(51)
                if i==3:c.jump()
                if i==4:c.falling(18,'Crown chest');c.conveyor(40);c.conveyor(59)
                if i==5:c.bridge(36)
            if n==2:
                if i==0:c.conveyor(22);c.conveyor(48);c.bar(22);c.bar(48)
                if i==1:c.sliders(23);c.pusher(52)
                if i==2:c.falling(24,'Royal seal');c.falling(50,'Royal seal',2)
                if i==3:c.lifts()
                if i==4:c.hammer(23);c.hammer(51)
                if i==5:c.pendulum(24);c.gate(52)
            if n==3:
                if i==0:c.disc(32);c.bar(14);c.bar(56)
                if i==1:c.sliders(23);c.gate(52)
                if i==2:c.pendulum(23);c.pendulum(51)
                if i==3:c.lifts()
                if i==4:c.falling(18,'Crown box');c.falling(42,'Crown box',2);c.conveyor(30);c.conveyor(57)
                if i==5:c.disc(20,False);c.bridge(52)
            if n==4:
                if i==0:c.bar(14);c.disc(34,False);c.bar(56)
                if i==1:c.lifts()
                if i==2:c.pusher(16);c.pusher(34,True);c.sliders(57)
                if i==3:c.castling(36)
                if i==4:c.hammer(16);c.hammer(36);c.vent(57)
                if i==5:c.conveyor(14);c.conveyor(33);c.bridge(57)
            c.finish_beat()
        c.save()
    scene()
    outputs['Tools/RoyalPalace/layout_report.json']=json.dumps(reports,ensure_ascii=False,indent=2)+'\n'

def scene():
    # Preserve the known-good lab camera, directional light and physics fixture only.
    template=(ROOT/'Assets/Scenes/ObstacleRagdollTest.unity').read_text()
    blocks=re.split(r'(?=^--- !u!)',template,flags=re.M)
    selected=[]
    for block in blocks:
        match=re.match(r'--- !u!\d+ &(\d+)',block)
        if match and (int(match[1]) in (1,2,3,4) or 800001<=int(match[1])<=800011):
            selected.append(block.replace('  - component: {fileID: 800494}\n','').replace('Ragdoll Obstacle Test','Royal Palace Course Test'))
    d=Doc(900000);g=d.go('Course selection and playtest')
    d.script(g,'RoyalPalaceCourseTest',fields(pawnPrefab='{fileID: 7684884123511159585, guid: fe69acb7e49fea745aa289c4d39f3e62, type: 3}',
        tuning='{fileID: 11400000, guid: f387368cafccca042ac935642cf094be, type: 2}',cameraRig=ref(800005),initialInstance=ref(950002),fallHeight=-8)+
        '  courses:\n'+''.join('  - '+external(r['component'],r['prefab'])+'\n' for r in reports))
    first=reports[0];pguid=guid(first['prefab'])
    instance=f'''--- !u!1001 &950000
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
  m_SourcePrefab: {{fileID: 100100000, guid: {pguid}, type: 3}}
--- !u!4 &950001 stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {first['transform']}, guid: {pguid}, type: 3}}
  m_PrefabInstance: {{fileID: 950000}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!114 &950002 stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {{fileID: {first['component']}, guid: {pguid}, type: 3}}
  m_PrefabInstance: {{fileID: 950000}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT['CourseModule']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {{fileID: 800002}}
  - {{fileID: 800007}}
  - {{fileID: 800010}}
  - {{fileID: {g['tr']}}}
  - {{fileID: 950001}}
'''
    outputs['Assets/Scenes/RoyalPalaceCourseTest.unity']=HEAD+''.join(selected)+d.render(scene=False)[len(HEAD):]+instance

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    mode=parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--write',action='store_true');mode.add_argument('--overwrite',action='store_true');mode.add_argument('--check',action='store_true')
    args=parser.parse_args();build()
    for path in outputs: outputs[path] = "\n".join(line.rstrip() for line in outputs[path].splitlines())+"\n"
    if args.check:
        stale=[p for p,s in outputs.items() if not (ROOT/p).exists() or (ROOT/p).read_text()!=s]
        if stale: raise SystemExit('Different from generator (may be intentional Unity edits): '+', '.join(stale))
        print(f'{len(outputs)} generated files match.');return
    if args.write and any((ROOT/p).exists() for p in outputs):raise SystemExit('Output already exists; use --check or explicitly --overwrite.')
    for p,s in outputs.items():
        target=ROOT/p;target.parent.mkdir(parents=True,exist_ok=True);target.write_text(s)
    assets=[p for p in outputs if p.startswith('Assets/')]
    folders=set()
    for p in assets:
        for parent in Path(p).parents:
            if str(parent) in ('.','Assets'):break
            if not (ROOT/(str(parent)+'.meta')).exists():folders.add(str(parent))
    subprocess.run([sys.executable,'Tools/Generators/mkmeta.py',*sorted(folders),*assets],cwd=ROOT,check=True,stdout=subprocess.DEVNULL)
    for path in [*sorted(folders),*assets]:
        meta=ROOT/(path+'.meta')
        meta.write_text('\n'.join(line.rstrip() for line in meta.read_text().splitlines())+'\n')
    print(f'Wrote {len(reports)} course prefabs, one test scene and {len(MAT)} materials.')
if __name__=='__main__':main()
