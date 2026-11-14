import os,sys,json
SCRIPT="C:/Work/ChessFight/ArtSource/PawnRush/Optimization20261113/course_mesh_pass.py"
exec(compile(open(SCRIPT,encoding="utf8").read().split("results=json.load")[0],SCRIPT,"exec"),globals())
for key in ["boats","plinths"]:
 rel="Assets/Resources/PawnRushGardenConcept/GardenBoats.fbx" if key=="boats" else "Assets/Resources/PawnRushGrandStairHall/StairStatuesV36.fbx"
 obs=load(os.path.join(OUT,"original",rel))
 focus=[o for o in obs if o.name==("Boat 1-1" if key=="boats" else "StairStatue_Knight_plinth")]
 render(focus,os.path.join(OUT,key+"_detail_before.png"))
 render(focus,os.path.join(OUT,key+"_detail_side_before.png"),(-5,-3,2))
 for tag in ["0p25","0p5","1p0"]:
  obs=load(os.path.join(OUT,"candidates",tag,rel))
  focus=[o for o in obs if o.name==("Boat 1-1" if key=="boats" else "StairStatue_Knight_plinth")]
  render(focus,os.path.join(OUT,key+"_detail_"+tag+"_after.png"))
  render(focus,os.path.join(OUT,key+"_detail_side_"+tag+"_after.png"),(-5,-3,2))
