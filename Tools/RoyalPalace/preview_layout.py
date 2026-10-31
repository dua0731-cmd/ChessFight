#!/usr/bin/env python3
"""Plot the SAVED prefab geometry, not concept art. Requires matplotlib and PyYAML."""
import os
os.environ.setdefault('MPLCONFIGDIR','/tmp/chessfight-matplotlib')
os.environ.setdefault('XDG_CACHE_HOME','/tmp/chessfight-cache')
from pathlib import Path
import math
import re
import yaml
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Polygon

ROOT=Path(__file__).resolve().parents[2]

def read(path):
    result={}
    for block in re.split(r'(?=^--- !u!)',path.read_text(),flags=re.M):
        match=re.match(r'--- !u!(\d+) &(\d+)',block)
        if match:result[int(match[2])]=(int(match[1]),next(iter(yaml.safe_load(block[block.index('\n')+1:]).values())))
    return result

def rotate(q,v):
    x,y,z,w=(q[k] for k in 'xyzw');a,b,c=v
    tx,ty,tz=2*(y*c-z*b),2*(z*a-x*c),2*(x*b-y*a)
    return (a+w*tx+y*tz-z*ty,b+w*ty+z*tx-x*tz,c+w*tz+x*ty-y*tx)

def world(data,tid,p=(0,0,0)):
    t=data[tid][1]
    p=rotate(t['m_LocalRotation'],tuple(p[i]*t['m_LocalScale'][k] for i,k in enumerate('xyz')))
    p=tuple(p[i]+t['m_LocalPosition'][k] for i,k in enumerate('xyz'))
    return world(data,t['m_Father']['fileID'],p) if t['m_Father']['fileID'] else p

def main():
    colors={}
    for p in (ROOT/'Assets/Maps/RoyalPalace/Materials').glob('*.mat'):
        guid=re.search(r'guid: (\w+)',Path(str(p)+'.meta').read_text())[1]
        c=re.search(r'_Color: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)',p.read_text())
        colors[guid]=tuple(float(n) for n in c.groups())
    fig,axes=plt.subplots(1,5,figsize=(20,6),facecolor='#101923',layout='constrained')
    names=['3-1  WAR ROOM','3-2  VAULT','3-3  STUDY','3-4  WARDROBE','3-5  OBSERVATORY']
    for ax,path,title in zip(axes,sorted((ROOT/'Assets/Maps/RoyalPalace/Courses').glob('*.prefab')),names):
        data=read(path);transforms={v['m_GameObject']['fileID']:key for key,(kind,v) in data.items() if kind==4}
        meshes={v['m_GameObject']['fileID']:v['m_Mesh']['fileID'] for kind,v in data.values() if kind==33}
        shapes=[]
        for kind,v in data.values():
            if kind!=23:continue
            go=v['m_GameObject']['fileID'];tid=transforms[go]
            color=colors.get(v['m_Materials'][0].get('guid'),(.5,.5,.5))
            if meshes[go] in (10206,10207):points=[world(data,tid,(.5*math.sin(i*math.pi/12),.5,.5*math.cos(i*math.pi/12))) for i in range(24)]
            else:points=[world(data,tid,p) for p in ((-.5,.5,-.5),(.5,.5,-.5),(.5,.5,.5),(-.5,.5,.5))]
            shapes.append((world(data,tid)[1],points,color))
        for y,points,color in sorted(shapes):
            ax.add_patch(Polygon([(p[0],p[2]) for p in points],facecolor=color,edgecolor='#18212b',linewidth=.15))
        for kind,v in data.values():
            if kind==114 and 'courseId' in v:
                route=[world(data,p['fileID']) for p in v['route']]
                ax.plot([p[0] for p in route],[p[2] for p in route],color='#63e6c7',lw=1,ls='--',alpha=.8)
                for i,p in enumerate(v['checkpoints']):
                    x,y,z=world(data,p['fileID']);ax.text(x,z,str(i+1),color='white',ha='center',va='center',fontsize=8,bbox=dict(boxstyle='circle',fc='#247862',ec='none'))
        ax.set(xlim=(-15,150),ylim=(-80,82),aspect='equal');ax.set_facecolor('#101923')
        ax.set_title(title,color='white',fontsize=11,pad=14);ax.axis('off')
    fig.suptitle('ROYAL PALACE / FIVE SAVED COURSE PREFABS\nTop view of authored geometry | dashed line: route | green circles: checkpoints | lower hall: minigame placeholder',color='white',fontsize=14)
    out=ROOT/'Docs/KingRush/ROYAL_PALACE_LAYOUT.svg';fig.savefig(out,facecolor=fig.get_facecolor())
    out.write_text('\n'.join(line.rstrip() for line in out.read_text().splitlines())+'\n');print(out)
if __name__=='__main__':main()
