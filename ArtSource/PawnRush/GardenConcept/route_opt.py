# R485: the water gate boat's route (GardenWatergateBoat.Route): a search over lollipop routes through the passage of
# watergate_sample_v1.py (x 42.58) for the roundest whose hull (4.8 x 1.8 m) keeps clear of the lock wall, the bank,
# the terrace and the passage's sides. Prints the best and its control points as C#.
#   python route_opt.py
import numpy as np, itertools, math
X=42.58
def route(cn, cs, r, a0, a1, step, neck):
    pts=[(X,2.),(X,11.),(X,neck)]
    for a in np.arange(a0, a1+1e-6, step):
        pts.append((cn[0]+r*math.cos(math.radians(a)), cn[1]+r*math.sin(math.radians(a))))
    pts += [(X,neck),(X,11.),(X,2.)]
    for a in np.arange(a0, a1+1e-6, step):   # the south loop mirrored about z = 11 (same x), so clockwise
        pts.append((cs[0]+r*math.cos(math.radians(a)), cs[1]-r*math.sin(math.radians(a))))
    return np.array(pts)
def evaluate(R, S=3000):
    n=len(R); P=[]
    for i in range(S):
        u=i/S*n; k=int(u)%n; t=u-int(u)
        a,b,c,d=R[(k-1)%n],R[k],R[(k+1)%n],R[(k+2)%n]
        P.append(.5*(2*b+(c-a)*t+(2*a-5*b+4*c-d)*t*t+(3*b-a-3*c+d)*t**3))
    P=np.array(P); L=np.linalg.norm(np.diff(np.vstack([P,P[:1]]),axis=0),axis=1).sum()
    ds=L/S; m=max(1,int(round(.8/ds)))
    xmin,xmax,zmax,zmin,dev,rmin=1e9,-1e9,-1e9,1e9,0,1e9
    for i in range(S):
        d=P[(i+m)%S]-P[(i-m)%S]; d/=np.linalg.norm(d); nr=np.array([-d[1],d[0]])
        cs=[P[i]+d*2.4,P[i]-d*2.4]+[P[i]+d*s1*1.6+nr*s2*.9 for s1 in(-1,1) for s2 in(-1,1)]
        xs=[c[0] for c in cs]; zs=[c[1] for c in cs]
        xmin=min(xmin,min(xs)); xmax=max(xmax,max(xs)); zmax=max(zmax,max(zs)); zmin=min(zmin,min(zs))
        if 3.7<P[i,1]<18.6: dev=max(dev,abs(P[i,0]-X)+.9*abs(nr[0])+2.4*abs(d[0]))
        a=P[(i-m)%S]; b=P[i]; c=P[(i+m)%S]; v1=b-a; v2=c-b
        ang=abs(math.atan2(v1[0]*v2[1]-v1[1]*v2[0], v1@v2)); 
        if ang>1e-6: rmin=min(rmin,(np.linalg.norm(v1)+np.linalg.norm(v2))/2/ang)
    return L,xmin,xmax,zmax,zmin,dev,rmin
best=None
for cx in (40.6,40.9,41.2):
  for r in (2.7,2.9,3.1):
    for a0 in (-40,-25,-10):
      for a1 in (220,235,250):
        for step in (30,40,45):
          for neck in (19.5,20.5):
            cn=(cx, neck+5.6+r-2.9); cs=(cx, 22-cn[1])
            R=route(cn,cs,r,a0,a1,step,neck)
            L,xmin,xmax,zmax,zmin,dev,rmin=evaluate(R,1500)
            ok = xmin>36.4 and xmax<45.2 and zmax<32.4 and zmin>-14 and dev<1.3
            if ok and (best is None or rmin>best[0]):
                best=(rmin,cx,r,a0,a1,step,neck,cn,L,xmin,xmax,zmax,dev)
print(best)
rmin,cx,r,a0,a1,step,neck,cn,L,*_=best
R=route(cn,(cx,22-cn[1]),r,a0,a1,step,neck)
print(evaluate(R))
print(",\n".join("new Vector2(%.2ff, %.2ff)" % (x,z) for x,z in R))
