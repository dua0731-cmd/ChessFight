# Before/after pairs from Unity preview captures: <dir> <name> <views comma> <out.png>
import sys, json
from PIL import Image, ImageDraw
import numpy as np
d,name,views,out=sys.argv[1],sys.argv[2],sys.argv[3].split(","),sys.argv[4]
BG=np.array([31,36,46]); W=520; rows=[]; res={}
for v in views:
    a=np.asarray(Image.open(f"{d}/{name}_{v}_before.png").convert("RGB")).astype(int)
    b=np.asarray(Image.open(f"{d}/{name}_{v}_after.png").convert("RGB")).astype(int)
    fa=np.abs(a-BG).max(2)>6; fb=np.abs(b-BG).max(2)>6; fg=fa|fb
    diff=np.abs(a-b).max(2)
    res[v]=dict(silhouetteIoU=round(float((fa&fb).sum()/max(fg.sum(),1)),5),meanDiff=round(float(diff[fg].mean()),2),pctOver24=round(float((diff[fg]>24).mean()*100),3))
    heat=np.clip(diff*8,0,255).astype(np.uint8)
    ims=[Image.fromarray(a.astype(np.uint8)),Image.fromarray(b.astype(np.uint8)),Image.fromarray(np.stack([heat,heat//3,heat//3],2))]
    row=Image.new("RGB",(W*3,W+22),(18,18,22));dr=ImageDraw.Draw(row)
    for i,im in enumerate(ims):row.paste(im.resize((W,W)),(i*W,22))
    dr.text((6,5),f"{name} {v}: before | after | diff x8   mean {res[v]['meanDiff']}  >24 {res[v]['pctOver24']}%  IoU {res[v]['silhouetteIoU']}",fill=(230,230,230))
    rows.append(row)
s=Image.new("RGB",(rows[0].width,sum(r.height for r in rows)));y=0
for r in rows:s.paste(r,(0,y));y+=r.height
s.save(out);print(json.dumps(res))
