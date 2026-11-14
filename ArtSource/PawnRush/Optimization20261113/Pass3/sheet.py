# Build a views x candidates comparison sheet with simple metrics against the source render.
import sys, json, os
from PIL import Image, ImageDraw
import numpy as np
d,out=sys.argv[1],sys.argv[2]; tags=sys.argv[3].split(",")
views=["front","side","back","bottom"]; W=360; res={}
rows=[]
for v in views:
    src=np.asarray(Image.open(f"{d}/{v}_source.png").convert("RGBA")).astype(int)
    cells=[]
    for t in ["source"]+tags:
        im=Image.open(f"{d}/{v}_{t}.png").convert("RGBA"); a=np.asarray(im).astype(int)
        if t!="source":
            ma,mb=src[...,3]>8,a[...,3]>8
            iou=(ma&mb).sum()/max((ma|mb).sum(),1)
            both=ma&mb; diff=np.abs(src[...,:3]-a[...,:3]).max(2)[both]
            res.setdefault(t,{})[v]=dict(silhouetteIoU=round(float(iou),5),meanDiff=round(float(diff.mean()),2),pctOver24=round(float((diff>24).mean()*100),2))
        bg=Image.new("RGBA",im.size,(28,30,36,255)); bg.alpha_composite(im); cells.append(bg.convert("RGB").resize((W,W)))
    row=Image.new("RGB",(W*len(cells),W+22),(18,18,22)); dr=ImageDraw.Draw(row)
    for i,c in enumerate(cells):
        row.paste(c,(i*W,22)); t=(["source"]+tags)[i]
        lab=t if t=="source" else f"{t}  diff {res[t][v]['meanDiff']}  >24 {res[t][v]['pctOver24']}%"
        dr.text((i*W+6,5),f"{v}: {lab}",fill=(230,230,230))
    rows.append(row)
s=Image.new("RGB",(rows[0].width,sum(r.height for r in rows)));y=0
for r in rows:s.paste(r,(0,y));y+=r.height
s.save(out);print(json.dumps(res))
