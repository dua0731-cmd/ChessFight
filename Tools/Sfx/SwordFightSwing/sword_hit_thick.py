# sword_hit_thick.py (10-30, 승규 님: "좀 더 굵직하게"): SwordHit take B made beefier - sub rumble cut, low shelf + low-mid body,
# a little less edge, gentle saturation.  python sword_hit_thick.py in.mp3 SwordHit.wav
import sys, wave, numpy as np, miniaudio
src, out = sys.argv[1], sys.argv[2]
d = miniaudio.decode_file(src, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1)
sr = d.sample_rate; x = np.array(d.samples, dtype=np.float64)
def biquad(x, b, a):
    y = np.zeros_like(x); x1=x2=y1=y2=0.0
    for i,v in enumerate(x):
        o = (b[0]*v + b[1]*x1 + b[2]*x2 - a[1]*y1 - a[2]*y2)/a[0]
        x2,x1,y2,y1 = x1,v,y1,o; y[i]=o
    return y
def lowshelf(x, f, g):
    A=10**(g/40); w=2*np.pi*f/sr; c=np.cos(w); s=np.sin(w); al=s/2*np.sqrt(2)
    b=[A*((A+1)-(A-1)*c+2*np.sqrt(A)*al), 2*A*((A-1)-(A+1)*c), A*((A+1)-(A-1)*c-2*np.sqrt(A)*al)]
    a=[(A+1)+(A-1)*c+2*np.sqrt(A)*al, -2*((A-1)+(A+1)*c), (A+1)+(A-1)*c-2*np.sqrt(A)*al]
    return biquad(x,b,a)
def peak(x, f, g, q):
    A=10**(g/40); w=2*np.pi*f/sr; al=np.sin(w)/(2*q); c=np.cos(w)
    return biquad(x,[1+al*A,-2*c,1-al*A],[1+al/A,-2*c,1-al/A])
def hpf(x, f):
    w=2*np.pi*f/sr; c=np.cos(w); al=np.sin(w)/(2*0.707)
    return biquad(x,[(1+c)/2,-(1+c),(1+c)/2],[1+al,-2*c,1-al])
x = hpf(hpf(x, 45), 45)        # drop the sub rumble so the boost lands where it is heard
y = lowshelf(x, 160, 7.0)      # weight
y = peak(y, 280, 3.0, 0.9)     # body
y = peak(y, 3500, -3.0, 0.8)   # a little less bright edge
y = y / np.max(np.abs(y))
y = np.tanh(1.8*y) / np.tanh(1.8)  # thicker, denser hit
y -= np.mean(y)
y = y / np.max(np.abs(y)) * 0.94
with wave.open(out, "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes((y*32767).astype("<i2").tobytes())
print(out, sr, f"{len(y)/sr:.2f}s")
